using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// 초기 화면 → 온라인 접속 → 방(로비) 흐름을 담당한다.
/// 로비는 팀별 슬롯을 자유롭게 추가/삭제하고, 각 칸을 [빈칸 / AI / 접속한 사람]으로 배치한다(3v3 고정 아님).
/// 슬롯 구성은 NetworkList로 동기화되어 호스트가 편집하고 모든 클라가 본다.
///
/// 접속은 Unity Relay 기반이다: 호스트가 방을 만들면 조인코드가 나오고, 친구는 그 코드로 붙는다.
/// 포트포워딩이 필요 없어 서로 다른 네트워크(인터넷)에서도 연결된다.
/// 같은 공유기 안에서 빠르게 시험할 때를 위해 직접 IP(LAN) 경로도 남겨둔다.
///
/// 씬의 in-scene NetworkObject에 붙인다(호스트 시작 시 스폰). OnGUI는 스폰 전에도 돌기 때문에
/// 접속 전에는 메뉴/접속 UI를, 접속 후(IsSpawned)에는 방 UI를 그린다.
///
/// "게임 시작"을 누르면 MatchSpawner가 슬롯 구성대로 선수를 네트워크 스폰하고 경기를 연다.
/// </summary>
public enum Occupant : byte { Empty, Human, AI }

public struct TeamSlot : INetworkSerializeByMemcpy, IEquatable<TeamSlot>
{
    public byte team;       // 0 = Blue, 1 = Red
    public Occupant type;   // 빈칸 / 사람 / AI
    public ulong clientId;  // type==Human일 때 배치된 클라이언트 id

    public bool Equals(TeamSlot o) => team == o.team && type == o.type && clientId == o.clientId;
    public override bool Equals(object o) => o is TeamSlot s && Equals(s);
    public override int GetHashCode() => (team, (byte)type, clientId).GetHashCode();
}

public class LobbyController : NetworkBehaviour
{
    private enum Screen { Main, Online, Lan, Room }

    [Header("접속")]
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;
    [Tooltip("호스트를 제외한 최대 접속 인원. Relay 방 크기를 정한다.")]
    [SerializeField] private int maxConnections = MpsRoomDefinition.MaximumPlayers;

    [Header("로비 기본값")]
    [Tooltip("호스트 시작 시 팀당 기본 슬롯 수.")]
    [SerializeField] private int defaultSlotsPerTeam = 3;
    [Tooltip("팀당 최대 슬롯 수.")]
    [SerializeField] private int maxSlotsPerTeam = 5;

    // 동기화되는 슬롯 목록(호스트가 편집).
    private readonly NetworkList<TeamSlot> slots = new NetworkList<TeamSlot>();
    private readonly NetworkList<ulong> p2pParticipantClientIds = new NetworkList<ulong>();
    private readonly NetworkList<ulong> p2pMeshReadyClientIds = new NetworkList<ulong>();
    private readonly NetworkList<ulong> p2pRecoveryApprovedClientIds = new NetworkList<ulong>();
    private readonly NetworkList<ulong> gameReadyClientIds = new NetworkList<ulong>();
    private readonly NetworkVariable<bool> matchStarted = new NetworkVariable<bool>(false);

    private Screen screen = Screen.Main;
    private Vector2 scroll;

    // Relay 접속 상태 (OnGUI는 await를 못 하므로 비동기 결과를 필드로 받아 표시한다).
    private string joinCodeInput = "";
    private string hostJoinCode = "";
    private string statusMessage = "";
    private bool isConnecting;
    private string mpsRoomName = "Futsal Room";
    private MpsRoomDefinition[] mpsRooms = Array.Empty<MpsRoomDefinition>();
    private IRoomService mpsSessionRooms;
    private bool usesMpsRelaySession;
    private IPeerSignalingTransport p2pSignalRelay;
    private P2pPeerConnectionRegistry p2pConnections;
    private string p2pStatusMessage = "Waiting for P2P mesh participants.";
    private readonly P2pReconnectSchedule p2pReconnectSchedule = new P2pReconnectSchedule();
    private P2pSessionStatus p2pSessionStatus = P2pSessionStatus.Preparing;
    private Coroutine p2pReconnectRoutine;

    // ---------------- 네트워크 수명주기 ----------------

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (slots.Count == 0)
            {
                for (byte t = 0; t < 2; t++)
                    for (int i = 0; i < defaultSlotsPerTeam; i++)
                        slots.Add(new TeamSlot { team = t, type = Occupant.Empty, clientId = 0 });
            }
            NetworkManager.OnClientDisconnectCallback += HandleClientDisconnect;
            RefreshP2pParticipants();
        }

        matchStarted.OnValueChanged += HandleMatchStartedChanged;
        p2pParticipantClientIds.OnListChanged += HandleP2pParticipantsChanged;
        p2pRecoveryApprovedClientIds.OnListChanged += HandleP2pRecoveryApprovalsChanged;
        RefreshP2pRecoveryApprovals();
        if (MpsNetworkingModePolicy.RequiresDirectP2p(usesMpsRelaySession))
            StartP2pSignaling();
        else
            p2pStatusMessage = "MPS Relay session active.";

        // 경기가 이미 시작된 뒤에 들어온 사람은 값이 바뀌는 순간을 놓친다.
        // 그대로 두면 그 사람 화면에만 오프라인 캐릭터가 남아 경기에 섞인다.
        if (matchStarted.Value && !IsServer)
            EnterStartedMatch();

        screen = Screen.Room;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;

        matchStarted.OnValueChanged -= HandleMatchStartedChanged;
        p2pParticipantClientIds.OnListChanged -= HandleP2pParticipantsChanged;
        p2pRecoveryApprovedClientIds.OnListChanged -= HandleP2pRecoveryApprovalsChanged;
        P2pPeerRecoveryApprovals.SetApprovedPeerClientIds(null);
        StopP2pSignaling();
    }

    private void StartP2pSignaling()
    {
        p2pSignalRelay = new P2pLobbySignalRelay(NetworkManager);
        p2pSignalRelay.SignalReceived += HandleP2pSignal;
        p2pSignalRelay.Start();

        p2pConnections = gameObject.AddComponent<P2pPeerConnectionRegistry>();
        p2pConnections.SignalReady += SendP2pSignal;
        p2pConnections.PeerStateChanged += HandleP2pStateChanged;
        p2pConnections.GameplayReadinessChanged += UpdateP2pSessionStatus;

        if (IsServer)
            NetworkManager.OnClientConnectedCallback += HandleP2pClientConnected;

        ConfigureP2pPeers();
    }

    private void StopP2pSignaling()
    {
        if (p2pReconnectRoutine != null)
        {
            StopCoroutine(p2pReconnectRoutine);
            p2pReconnectRoutine = null;
        }

        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientConnectedCallback -= HandleP2pClientConnected;

        if (p2pSignalRelay != null)
        {
            p2pSignalRelay.SignalReceived -= HandleP2pSignal;
            p2pSignalRelay.Stop();
            p2pSignalRelay = null;
        }

        if (p2pConnections != null)
        {
            p2pConnections.SignalReady -= SendP2pSignal;
            p2pConnections.PeerStateChanged -= HandleP2pStateChanged;
            p2pConnections.GameplayReadinessChanged -= UpdateP2pSessionStatus;
            p2pConnections.Shutdown();
            Destroy(p2pConnections);
            p2pConnections = null;
        }
    }

    private void HandleP2pClientConnected(ulong clientId)
    {
        RefreshP2pParticipants();
        p2pStatusMessage = "Waiting for the P2P mesh to include the new participant.";
    }

    private void HandleP2pParticipantsChanged(NetworkListEvent<ulong> changeEvent)
    {
        ConfigureP2pPeers();
    }

    private void ConfigureP2pPeers()
    {
        if (p2pConnections == null || NetworkManager == null || !NetworkManager.IsListening)
            return;

        List<ulong> peerClientIds = new List<ulong>(p2pParticipantClientIds.Count);
        for (int i = 0; i < p2pParticipantClientIds.Count; i++)
            peerClientIds.Add(p2pParticipantClientIds[i]);

        p2pConnections.Configure(NetworkManager.LocalClientId, peerClientIds);
        p2pConnections.SendReadyForRequiredPeers();
        UpdateP2pSessionStatus();
    }

    private void RefreshP2pParticipants()
    {
        if (!IsServer || NetworkManager == null)
            return;

        p2pParticipantClientIds.Clear();
        foreach (ulong clientId in SortedConnectedClients())
            p2pParticipantClientIds.Add(clientId);

        for (int i = p2pMeshReadyClientIds.Count - 1; i >= 0; i--)
        {
            if (!p2pParticipantClientIds.Contains(p2pMeshReadyClientIds[i]))
                p2pMeshReadyClientIds.RemoveAt(i);
        }

        for (int i = p2pRecoveryApprovedClientIds.Count - 1; i >= 0; i--)
        {
            if (!p2pParticipantClientIds.Contains(p2pRecoveryApprovedClientIds[i]))
                p2pRecoveryApprovedClientIds.RemoveAt(i);
        }

        for (int i = gameReadyClientIds.Count - 1; i >= 0; i--)
        {
            if (!p2pParticipantClientIds.Contains(gameReadyClientIds[i]))
                gameReadyClientIds.RemoveAt(i);
        }
    }

    [Rpc(SendTo.Server)]
    private void ReportP2pMeshReadinessRpc(bool isReady, RpcParams rpcParams = default)
    {
        if (!IsServer || !p2pParticipantClientIds.Contains(rpcParams.Receive.SenderClientId))
            return;

        SetP2pMeshReady(rpcParams.Receive.SenderClientId, isReady);
        if (!isReady)
            SetP2pRecoveryApproved(rpcParams.Receive.SenderClientId, false);

        TryApproveRecoveredParticipants();
    }

    private void SetP2pMeshReady(ulong clientId, bool isReady)
    {
        int index = p2pMeshReadyClientIds.IndexOf(clientId);
        if (isReady && index < 0)
            p2pMeshReadyClientIds.Add(clientId);
        else if (!isReady && index >= 0)
            p2pMeshReadyClientIds.RemoveAt(index);
    }

    private bool AreAllP2pParticipantsMeshReady()
    {
        foreach (ulong clientId in p2pParticipantClientIds)
        {
            if (!p2pMeshReadyClientIds.Contains(clientId))
                return false;
        }

        return true;
    }

    [Rpc(SendTo.Server)]
    private void SetGameReadyRpc(bool isReady, RpcParams rpcParams = default)
    {
        if (!IsServer || !p2pParticipantClientIds.Contains(rpcParams.Receive.SenderClientId))
            return;

        SetGameReady(rpcParams.Receive.SenderClientId, isReady);
    }

    private void ToggleLocalGameReady()
    {
        ulong localClientId = NetworkManager.LocalClientId;
        bool willBeReady = !gameReadyClientIds.Contains(localClientId);
        if (IsServer)
            SetGameReady(localClientId, willBeReady);
        else
            SetGameReadyRpc(willBeReady);
    }

    private void SetGameReady(ulong clientId, bool isReady)
    {
        int index = gameReadyClientIds.IndexOf(clientId);
        if (isReady && index < 0)
            gameReadyClientIds.Add(clientId);
        else if (!isReady && index >= 0)
            gameReadyClientIds.RemoveAt(index);
    }

    private bool AreAllParticipantsGameReady()
    {
        foreach (ulong clientId in p2pParticipantClientIds)
        {
            if (!gameReadyClientIds.Contains(clientId))
                return false;
        }

        return true;
    }

    private void ClearGameReady()
    {
        if (IsServer)
            gameReadyClientIds.Clear();
    }

    private void SetP2pRecoveryApproved(ulong clientId, bool isApproved)
    {
        int index = p2pRecoveryApprovedClientIds.IndexOf(clientId);
        if (isApproved && index < 0)
            p2pRecoveryApprovedClientIds.Add(clientId);
        else if (!isApproved && index >= 0)
            p2pRecoveryApprovedClientIds.RemoveAt(index);
    }

    private void HandleP2pRecoveryApprovalsChanged(NetworkListEvent<ulong> changeEvent)
    {
        RefreshP2pRecoveryApprovals();
    }

    private void RefreshP2pRecoveryApprovals()
    {
        List<ulong> approvedClientIds = new List<ulong>(p2pRecoveryApprovedClientIds.Count);
        for (int i = 0; i < p2pRecoveryApprovedClientIds.Count; i++)
            approvedClientIds.Add(p2pRecoveryApprovedClientIds[i]);

        P2pPeerRecoveryApprovals.SetApprovedPeerClientIds(approvedClientIds);
    }

    private void TryApproveRecoveredParticipants()
    {
        if (!IsServer || !matchStarted.Value || !AreAllP2pParticipantsMeshReady())
            return;

        foreach (ulong clientId in p2pParticipantClientIds)
        {
            if (clientId != NetworkManager.ServerClientId)
                SetP2pRecoveryApproved(clientId, true);
        }
    }

    private void HandleP2pSignal(P2pPeerSignal signal)
    {
        if (p2pConnections == null || !p2pConnections.ReceiveSignal(signal))
            p2pStatusMessage = "Direct P2P setup received an unexpected peer signal.";
    }

    private void SendP2pSignal(P2pPeerSignal signal)
    {
        if (!p2pSignalRelay.TrySend(signal, out string error))
            p2pStatusMessage = "Direct P2P setup failed: " + error;
    }

    private void HandleP2pStateChanged(ulong peerClientId, P2pConnectionState state, string message)
    {
        if (IsServer && P2pPeerRecoveryPolicy.ShouldFreeze(state))
        {
            SetP2pMeshReady(peerClientId, false);
            SetP2pRecoveryApproved(peerClientId, false);
        }

        if (state == P2pConnectionState.Ready)
        {
            UpdateP2pSessionStatus();
            return;
        }

        if (state == P2pConnectionState.Failed)
        {
            SetP2pSessionStatus(P2pSessionStatus.PeerDisconnected);
            BeginMeshReconnect();
            return;
        }

        if (state == P2pConnectionState.Negotiating)
        {
            SetP2pSessionStatus(P2pSessionStatus.Preparing);
            return;
        }

        p2pStatusMessage = message;
    }

    private void UpdateP2pSessionStatus()
    {
        bool isLocallyMeshReady = p2pConnections != null && p2pConnections.IsGameplayReady;
        if (IsServer)
            SetP2pMeshReady(NetworkManager.LocalClientId, isLocallyMeshReady);
        else if (NetworkManager != null && NetworkManager.IsListening)
            ReportP2pMeshReadinessRpc(isLocallyMeshReady);

        if (isLocallyMeshReady && AreAllP2pParticipantsMeshReady())
        {
            TryApproveRecoveredParticipants();
            p2pReconnectSchedule.Reset();
            SetP2pSessionStatus(P2pSessionStatus.Ready);
            return;
        }

        SetP2pSessionStatus(P2pSessionStatus.Preparing);
    }

    private void SetP2pSessionStatus(P2pSessionStatus status)
    {
        p2pSessionStatus = status;
        p2pStatusMessage = P2pSessionStatusText.For(status);
    }

    private void BeginMeshReconnect()
    {
        if (p2pReconnectRoutine != null)
            return;

        if (NetworkManager == null || !NetworkManager.IsListening)
        {
            SetP2pSessionStatus(P2pSessionStatus.HostUnavailable);
            return;
        }

        p2pReconnectRoutine = StartCoroutine(ReconnectMeshP2p());
    }

    private IEnumerator ReconnectMeshP2p()
    {
        while (p2pConnections != null && !p2pConnections.IsGameplayReady)
        {
            SetP2pSessionStatus(P2pSessionStatus.Reconnecting);
            yield return new WaitForSeconds(p2pReconnectSchedule.NextDelaySeconds());

            if (NetworkManager == null || !NetworkManager.IsListening)
            {
                SetP2pSessionStatus(P2pSessionStatus.HostUnavailable);
                break;
            }

            p2pReconnectSchedule.RecordAttempt();
            p2pConnections.SendReadyForRequiredPeers();
        }

        p2pReconnectRoutine = null;
    }

    /// <summary>
    /// 호스트가 경기를 시작하면 클라이언트도 경기 화면으로 넘어간다.
    /// 경기 흐름(카운트다운·점수·시간)은 서버가 굴려서 복제하므로, 여기서는
    /// 씬에 고정된 오프라인 캐릭터만 치우면 된다.
    /// </summary>
    private void HandleMatchStartedChanged(bool previous, bool current)
    {
        if (!current || IsServer) return; // 서버는 SvStartMatch에서 이미 처리했다

        EnterStartedMatch();
    }

    private void EnterStartedMatch()
    {
        if (MatchSpawner.Instance != null)
            MatchSpawner.Instance.PrepareForNetworkMatch();
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (!IsServer) return;
        if (matchStarted.Value)
            SetP2pSessionStatus(P2pSessionStatus.PeerDisconnected);

        // 나간 사람이 배치된 칸을 빈칸으로.
        for (int i = 0; i < slots.Count; i++)
        {
            TeamSlot s = slots[i];
            if (s.type == Occupant.Human && s.clientId == clientId)
            {
                s.type = Occupant.Empty; s.clientId = 0;
                slots[i] = s;
            }
        }

        SetP2pMeshReady(clientId, false);
        SetP2pRecoveryApproved(clientId, false);
        RefreshP2pParticipants();
    }

    // ---------------- 서버(호스트) 슬롯 편집 ----------------

    private void SvAddSlot(byte team)
    {
        if (!IsServer) return;
        if (CountTeam(team) >= maxSlotsPerTeam) return;
        slots.Add(new TeamSlot { team = team, type = Occupant.Empty, clientId = 0 });
        ClearGameReady();
    }

    private void SvRemoveSlot(int index)
    {
        if (!IsServer || index < 0 || index >= slots.Count) return;
        slots.RemoveAt(index);
        ClearGameReady();
    }

    private void SvSet(int index, Occupant type, ulong clientId)
    {
        if (!IsServer || index < 0 || index >= slots.Count) return;
        TeamSlot s = slots[index];
        s.type = type; s.clientId = clientId;
        slots[index] = s;
        ClearGameReady();
    }

    /// <summary>
    /// 이 칸에 넣을 사람을 바꾼다. 누를 때마다 접속자들을 차례로 돌아가며 배치한다.
    ///
    /// 예전에는 "아직 배치 안 된 첫 사람"만 넣을 수 있어서, 누구를 어느 팀에 둘지 고를 수 없었고
    /// 이미 배치된 사람을 다른 팀으로 옮기려면 먼저 그 칸을 비워야 했다.
    /// </summary>
    private void SvAssignNextHuman(int index)
    {
        if (!IsServer || index < 0 || index >= slots.Count) return;

        List<ulong> connected = SortedConnectedClients();
        if (connected.Count == 0) return;

        TeamSlot slot = slots[index];
        int current = slot.type == Occupant.Human ? connected.IndexOf(slot.clientId) : -1;
        ulong next = connected[(current + 1) % connected.Count];

        // 한 사람이 두 자리를 차지하지 않도록 원래 있던 칸을 비운다.
        VacateHuman(next);
        SvSet(index, Occupant.Human, next);
    }

    [Rpc(SendTo.Server)]
    private void RequestJoinTeamRpc(byte team, RpcParams rpcParams = default)
    {
        if (!IsServer || matchStarted.Value || team > 1) return;

        int targetSlotIndex = LobbyTeamJoinPolicy.FindFirstEmptySlot(SnapshotSlots(), team);
        if (targetSlotIndex < 0) return;

        ulong joiningClientId = rpcParams.Receive.SenderClientId;
        VacateHuman(joiningClientId);
        SvSet(targetSlotIndex, Occupant.Human, joiningClientId);
    }

    /// <summary>접속자 목록을 항상 같은 순서로 돌기 위해 정렬해서 돌려준다.</summary>
    private List<ulong> SortedConnectedClients()
    {
        List<ulong> ids = new List<ulong>();
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
            ids.Add(id);

        ids.Sort();
        return ids;
    }

    private void VacateHuman(ulong clientId)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            TeamSlot s = slots[i];
            if (s.type == Occupant.Human && s.clientId == clientId)
            {
                s.type = Occupant.Empty;
                s.clientId = 0;
                slots[i] = s;
            }
        }
    }

    private void SvStartMatch()
    {
        if (!IsServer) return;

        bool isDirectP2pReady = AreAllP2pParticipantsMeshReady();
        bool areAllPlayersGameReady = AreAllParticipantsGameReady();
        if (MpsNetworkingModePolicy.RequiresDirectP2p(usesMpsRelaySession))
        {
            if (!isDirectP2pReady)
            {
                SetP2pSessionStatus(P2pSessionStatus.Preparing);
                return;
            }

            if (!P2pMatchStartPolicy.CanStart(
                    NetworkManager.ConnectedClientsIds.Count,
                    isDirectP2pReady,
                    areAllPlayersGameReady))
            {
                p2pStatusMessage = "Waiting for every player to mark game ready.";
                return;
            }
        }

        matchStarted.Value = true;

        // 슬롯 구성대로 선수를 네트워크 스폰한 뒤 경기를 연다.
        // 스폰이 먼저여야 킥오프 리셋이 스폰된 선수까지 포함한다.
        if (MatchSpawner.Instance != null)
            MatchSpawner.Instance.ServerSpawnTeams(SnapshotSlots());
        else
            Debug.LogWarning("[LobbyController] 씬에 MatchSpawner가 없어 선수를 스폰하지 못했습니다.", this);

        if (GameManager.Instance != null) GameManager.Instance.BeginMatch();
    }

    /// <summary>NetworkList를 스폰 로직에 넘기기 위해 일반 리스트로 복사한다.</summary>
    private List<TeamSlot> SnapshotSlots()
    {
        List<TeamSlot> copy = new List<TeamSlot>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
            copy.Add(slots[i]);
        return copy;
    }

    private int CountTeam(byte team)
    {
        int n = 0;
        for (int i = 0; i < slots.Count; i++) if (slots[i].team == team) n++;
        return n;
    }

    private int UnassignedHumanCount()
    {
        int n = 0;
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
        {
            bool placed = false;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].type == Occupant.Human && slots[i].clientId == id) { placed = true; break; }
            if (!placed) n++;
        }
        return n;
    }

    // ---------------- UI ----------------

    private void OnGUI()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return;

        // 경기 시작됐으면 로비 UI 숨김(게임 HUD가 나옴).
        if (matchStarted.Value)
        {
            DrawActiveP2pStatus();
            return;
        }

        GUI.skin.button.fontSize = 16;
        GUI.skin.label.fontSize = 16;

        if (!nm.IsListening)
        {
            if (screen == Screen.Room) screen = Screen.Main; // 연결 끊기면 메뉴로

            // 방이 닫히면 그 조인코드는 더 이상 쓸 수 없다. 새로 만들 수 있게 비운다.
            if (!isConnecting && !string.IsNullOrEmpty(hostJoinCode))
                hostJoinCode = "";

            DrawMainOrLan(nm);
            return;
        }

        if (IsSpawned)
            DrawRoom(nm);
    }

    private void DrawMainOrLan(NetworkManager nm)
    {
        // 온라인 화면은 조인코드·상태·진단까지 들어가 메뉴 화면보다 훨씬 길다.
        // 높이를 하나로 고정하면 아래쪽 버튼이 잘려 안 보인다.
        float w = Mathf.Min(400f, UnityEngine.Screen.width - 40f);
        float h = Mathf.Min(screen == Screen.Online ? 520f : 320f, UnityEngine.Screen.height - 40f);
        Rect area = new Rect((UnityEngine.Screen.width - w) / 2f, (UnityEngine.Screen.height - h) / 2f, w, h);
        GUILayout.BeginArea(area, GUI.skin.box);

        GUILayout.Space(10);
        GUILayout.Label("<size=28><b>FUTSAL</b></size>", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true });
        GUILayout.Space(20);

        if (screen == Screen.Main)
        {
            if (GUILayout.Button("게임 스타트", GUILayout.Height(48)))
            {
                if (GameManager.Instance != null) GameManager.Instance.BeginMatch();
                // 오프라인 시작: 메뉴를 닫기 위해 표시만 숨긴다.
                enabled = false;
            }
            GUILayout.Space(10);
            if (GUILayout.Button("온라인 플레이", GUILayout.Height(48)))
                screen = Screen.Online;
            GUILayout.Space(6);
            if (GUILayout.Button("직접 IP 접속 (LAN)", GUILayout.Height(32)))
                screen = Screen.Lan;
        }
        else if (screen == Screen.Online)
        {
            DrawOnlineScreen();
        }
        else // Lan (같은 공유기 안에서 빠르게 시험할 때)
        {
            GUILayout.Label("호스트 IP (Join 시):");
            ipAddress = GUILayout.TextField(ipAddress, GUILayout.Height(28));
            GUILayout.Space(8);
            UnityTransport utp = nm.GetComponent<UnityTransport>();
            if (GUILayout.Button("방 만들기 (Host)", GUILayout.Height(44)))
            {
                if (utp != null) utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");
                nm.StartHost();
            }
            GUILayout.Space(6);
            if (GUILayout.Button("접속 (Join)", GUILayout.Height(44)))
            {
                if (utp != null) utp.SetConnectionData(ipAddress, port);
                nm.StartClient();
            }
            GUILayout.Space(6);
            if (GUILayout.Button("← 뒤로"))
                screen = Screen.Main;
        }

        GUILayout.EndArea();
    }

    /// <summary>Relay 조인코드로 방을 만들거나 참가하는 화면.</summary>
    private void DrawOnlineScreen()
    {
        GUILayout.Label("MPS public rooms");
        mpsRoomName = GUILayout.TextField(mpsRoomName, 32, GUILayout.Height(28));

        GUI.enabled = !isConnecting;
        if (GUILayout.Button("Create MPS public room (max 6)", GUILayout.Height(36)))
            _ = HostViaMpsSessionAsync();

        if (GUILayout.Button("Refresh MPS rooms", GUILayout.Height(30)))
            _ = RefreshMpsRoomsAsync();

        for (int i = 0; i < mpsRooms.Length; i++)
        {
            MpsRoomDefinition room = mpsRooms[i];
            if (GUILayout.Button($"Join {room.Name} ({room.PlayerCount}/{room.MaxPlayers})", GUILayout.Height(30)))
                _ = JoinMpsRoomAsync(room);
        }

        GUI.enabled = true;
        GUILayout.Space(12);
        GUILayout.Label("Legacy Relay join code");

        // 이미 방을 만든 뒤 다시 누르면 새 코드가 발급되면서 먼저 알려준 코드가 죽는다.
        bool alreadyHosting = !string.IsNullOrEmpty(hostJoinCode);

        GUI.enabled = !isConnecting && !alreadyHosting;

        if (GUILayout.Button(alreadyHosting ? "방 생성됨" : "방 만들기 (조인코드 발급)", GUILayout.Height(44)))
            _ = HostViaRelayAsync();

        GUI.enabled = !isConnecting;

        GUILayout.Space(12);
        GUILayout.Label("친구에게 받은 조인코드:");
        joinCodeInput = GUILayout.TextField(joinCodeInput, 6, GUILayout.Height(28));
        GUILayout.Space(6);
        if (GUILayout.Button("코드로 접속", GUILayout.Height(44)))
            _ = JoinViaRelayAsync();

        GUI.enabled = true;

        GUILayout.Space(10);
        if (!string.IsNullOrEmpty(hostJoinCode))
        {
            GUILayout.Label("<b>내 조인코드: " + hostJoinCode + "</b>",
                            new GUIStyle(GUI.skin.label) { richText = true });
            if (GUILayout.Button("코드 복사", GUILayout.Height(26)))
                GUIUtility.systemCopyBuffer = hostJoinCode;
        }
        if (!string.IsNullOrEmpty(statusMessage))
            GUILayout.Label(statusMessage);

        DrawConnectionDiagnostics();

        // 위 내용이 길어져도 뒤로 버튼은 항상 아래에 남는다.
        GUILayout.FlexibleSpace();
        if (!isConnecting && GUILayout.Button("← 뒤로"))
            screen = Screen.Main;
    }

    /// <summary>
    /// 접속이 안 될 때 양쪽 화면만 비교하면 원인이 드러나도록 진단 정보를 보여준다.
    /// 환경이 다르면 같은 프로젝트라도 조인코드를 찾지 못한다.
    /// </summary>
    private void DrawConnectionDiagnostics()
    {
        GUILayout.Space(8);
        DrawConnectionReport();
        GUILayout.Label("Direct P2P: " + p2pStatusMessage,
            new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true });

        GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        GUILayout.Label("환경: " + RelayConnectionService.EnvironmentName, small);

        string playerId = RelayConnectionService.PlayerId;
        GUILayout.Label("플레이어 ID: " + (string.IsNullOrEmpty(playerId) ? "(로그인 전)" : playerId), small);
    }

    private void DrawActiveP2pStatus()
    {
        Rect area = new Rect(12f, 12f, 360f, 64f);
        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label("직접 대전: " + p2pStatusMessage,
            new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true });
        GUILayout.EndArea();
    }

    /// <summary>연결 도중 무슨 일이 있었는지(접속/끊김/사유)를 방을 만든 쪽과 들어간 쪽 모두에 보여준다.</summary>
    private void DrawConnectionReport()
    {
        NetworkConnectionReporter reporter = NetworkConnectionReporter.Instance;
        if (reporter == null || string.IsNullOrEmpty(reporter.LastMessage))
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        style.normal.textColor = reporter.LastMessageIsProblem
            ? new Color(1f, 0.55f, 0.45f)
            : new Color(0.6f, 0.95f, 0.6f);

        GUILayout.Label(reporter.LastMessage, style);
    }

    // ---------------- Relay 접속 ----------------

    private async Task HostViaRelayAsync()
    {
        if (isConnecting) return;
        isConnecting = true;
        statusMessage = "방 만드는 중...";
        hostJoinCode = "";
        NetworkConnectionReporter.Instance?.Clear(); // 이전 시도의 결과가 남아 헷갈리지 않게

        try
        {
            hostJoinCode = await RelayConnectionService.CreateAllocationAsync(maxConnections);
            if (!NetworkManager.Singleton.StartHost())
            {
                statusMessage = "호스트 시작에 실패했습니다.";
                hostJoinCode = "";
            }
            else
            {
                statusMessage = "친구에게 조인코드를 알려주세요.";
            }
        }
        catch (Exception e)
        {
            statusMessage = "방 만들기 실패 — " + RelayConnectionService.DescribeError(e);
            Debug.LogException(e, this);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private async Task JoinViaRelayAsync()
    {
        if (isConnecting) return;

        string code = RelayConnectionService.NormalizeJoinCode(joinCodeInput);
        if (string.IsNullOrEmpty(code))
        {
            statusMessage = "조인코드를 입력하세요.";
            return;
        }

        isConnecting = true;
        statusMessage = "접속 중...";
        NetworkConnectionReporter.Instance?.Clear();

        try
        {
            await RelayConnectionService.JoinAllocationAsync(code);

            // StartClient는 "시도를 시작했다"는 뜻일 뿐이다.
            // 실제로 붙었는지/왜 끊겼는지는 NetworkConnectionReporter가 알려준다.
            statusMessage = NetworkManager.Singleton.StartClient()
                ? "코드는 확인됐습니다. 호스트에 연결하는 중..."
                : "접속을 시작하지 못했습니다.";
        }
        catch (Exception e)
        {
            // 원인(코드 없음/권한/환경 등)이 그대로 보여야 다음 시도에서 헤매지 않는다.
            statusMessage = "접속 실패 — " + RelayConnectionService.DescribeError(e);
            Debug.LogException(e, this);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private IRoomService GetMpsSessionRooms()
    {
        if (mpsSessionRooms != null)
            return mpsSessionRooms;

        string buildKey = string.IsNullOrWhiteSpace(Application.version) ? "development" : Application.version;
        mpsSessionRooms = new MpsSessionRoomService(buildKey);
        return mpsSessionRooms;
    }

    private async Task HostViaMpsSessionAsync()
    {
        if (isConnecting) return;

        isConnecting = true;
        usesMpsRelaySession = true;
        statusMessage = "Creating MPS Relay room...";
        NetworkConnectionReporter.Instance?.Clear();

        try
        {
            MpsRoomDefinition room = await GetMpsSessionRooms().CreatePublicRoomAsync(mpsRoomName, MpsRoomDefinition.MaximumPlayers);
            statusMessage = $"MPS room created: {room.Name}";
        }
        catch (Exception e)
        {
            usesMpsRelaySession = false;
            statusMessage = "MPS room creation failed: " + RelayConnectionService.DescribeError(e);
            Debug.LogException(e, this);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private async Task RefreshMpsRoomsAsync()
    {
        if (isConnecting) return;

        isConnecting = true;
        statusMessage = "Refreshing MPS rooms...";

        try
        {
            mpsRooms = await GetMpsSessionRooms().BrowsePublicRoomsAsync();
            statusMessage = mpsRooms.Length == 0 ? "No compatible MPS rooms found." : $"Found {mpsRooms.Length} MPS room(s).";
        }
        catch (Exception e)
        {
            statusMessage = "MPS room refresh failed: " + RelayConnectionService.DescribeError(e);
            Debug.LogException(e, this);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private async Task JoinMpsRoomAsync(MpsRoomDefinition room)
    {
        if (isConnecting) return;

        isConnecting = true;
        usesMpsRelaySession = true;
        statusMessage = "Joining MPS room...";
        NetworkConnectionReporter.Instance?.Clear();

        try
        {
            await GetMpsSessionRooms().JoinPublicRoomAsync(room);
            statusMessage = "Joining MPS Relay room...";
        }
        catch (Exception e)
        {
            usesMpsRelaySession = false;
            statusMessage = "MPS room join failed: " + RelayConnectionService.DescribeError(e);
            Debug.LogException(e, this);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private void DrawRoom(NetworkManager nm)
    {
        // 화면보다 커지지 않게 제한한다(작은 해상도에서 버튼이 화면 밖으로 나가는 것 방지).
        float w = Mathf.Min(720f, UnityEngine.Screen.width - 40f);
        float h = Mathf.Min(560f, UnityEngine.Screen.height - 40f);
        Rect area = new Rect((UnityEngine.Screen.width - w) / 2f, (UnityEngine.Screen.height - h) / 2f, w, h);
        GUILayout.BeginArea(area, GUI.skin.box);

        GUILayout.Label("<size=22><b>방</b></size>   " + (IsServer ? "[호스트]" : "[참가자]") +
                        "   접속: " + nm.ConnectedClientsIds.Count + "명   미배치: " + UnassignedHumanCount() + "명",
                        new GUIStyle(GUI.skin.label) { richText = true });

        // 호스트는 방에서도 조인코드를 볼 수 있어야 친구를 뒤늦게 부를 수 있다.
        if (IsServer && !string.IsNullOrEmpty(hostJoinCode))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>조인코드: " + hostJoinCode + "</b>",
                            new GUIStyle(GUI.skin.label) { richText = true }, GUILayout.Width(240));
            if (GUILayout.Button("복사", GUILayout.Width(60)))
                GUIUtility.systemCopyBuffer = hostJoinCode;
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(6);

        // 높이를 고정하면 위아래에 줄이 하나만 늘어도 아래 버튼이 창 밖으로 밀려 사라진다.
        // 남는 공간을 목록이 차지하게 해서 "게임 시작"과 "나가기"가 항상 보이도록 한다.
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        GUILayout.BeginHorizontal();
        DrawTeamColumn(0, "BLUE");
        GUILayout.Space(12);
        DrawTeamColumn(1, "RED");
        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();

        // 방에서도 접속/이탈 상황이 보여야 상대가 왜 안 들어오는지 알 수 있다.
        DrawConnectionReport();
        GUILayout.Label("Direct P2P: " + p2pStatusMessage,
            new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true });
        bool requiresDirectP2p = MpsNetworkingModePolicy.RequiresDirectP2p(usesMpsRelaySession);
        if (requiresDirectP2p)
        {
            GUILayout.Label("Game ready: " + gameReadyClientIds.Count + "/" + p2pParticipantClientIds.Count,
                new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true });
        }

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (requiresDirectP2p)
        {
            bool isLocalGameReady = gameReadyClientIds.Contains(nm.LocalClientId);
            if (GUILayout.Button(isLocalGameReady ? "Ready cancel" : "Game ready", GUILayout.Width(120), GUILayout.Height(40)))
                ToggleLocalGameReady();
        }
        if (IsServer)
        {
            if (GUILayout.Button("게임 시작", GUILayout.Height(40)))
                SvStartMatch();
        }
        else
            GUILayout.Label("P2P 연결 상태: " + p2pStatusMessage);
        if (GUILayout.Button("나가기", GUILayout.Width(120), GUILayout.Height(40)))
            nm.Shutdown();
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private void DrawTeamColumn(byte team, string title)
    {
        GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(340));
        GUILayout.Label("<b>" + title + "</b>  (" + CountTeam(team) + ")", new GUIStyle(GUI.skin.label) { richText = true });

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].team != team) continue;
            TeamSlot s = slots[i];

            GUILayout.BeginHorizontal(GUI.skin.box);
            GUILayout.Label(SlotLabel(s), GUILayout.Width(150));

            if (IsServer)
            {
                if (GUILayout.Button("빈칸", GUILayout.Width(44))) SvSet(i, Occupant.Empty, 0);
                if (GUILayout.Button("AI", GUILayout.Width(36))) SvSet(i, Occupant.AI, 0);
                // 누를 때마다 다음 접속자로 바뀐다(같은 팀에 여러 명도 이렇게 배치한다).
                if (GUILayout.Button("사람▶", GUILayout.Width(58))) SvAssignNextHuman(i);
                if (GUILayout.Button("✕", GUILayout.Width(28))) SvRemoveSlot(i);
            }
            GUILayout.EndHorizontal();
        }

        if (IsServer && CountTeam(team) < maxSlotsPerTeam)
        {
            if (GUILayout.Button("+ 칸 추가"))
                SvAddSlot(team);
        }

        if (!IsServer)
            DrawClientTeamJoinButton(team, title);

        GUILayout.EndVertical();
    }

    private void DrawClientTeamJoinButton(byte team, string title)
    {
        if (IsLocalClientAssignedToTeam(team))
        {
            GUILayout.Label(title + " 참가 중");
            return;
        }

        bool hasEmptySlot = LobbyTeamJoinPolicy.FindFirstEmptySlot(SnapshotSlots(), team) >= 0;
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && hasEmptySlot;
        if (GUILayout.Button(hasEmptySlot ? title + " 참가" : title + " 팀 가득"))
            RequestJoinTeamRpc(team);
        GUI.enabled = wasEnabled;
    }

    private bool IsLocalClientAssignedToTeam(byte team)
    {
        ulong localClientId = NetworkManager.LocalClientId;
        for (int i = 0; i < slots.Count; i++)
        {
            TeamSlot slot = slots[i];
            if (slot.team == team && slot.type == Occupant.Human && slot.clientId == localClientId)
                return true;
        }

        return false;
    }

    private string SlotLabel(TeamSlot s)
    {
        switch (s.type)
        {
            case Occupant.AI: return "🤖 AI";
            case Occupant.Human:
                return (s.clientId == NetworkManager.LocalClientId ? "● 나 (" : "● P") + s.clientId + ")";
            default: return "— 빈칸 —";
        }
    }
}
