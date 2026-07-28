using System;
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
    [SerializeField] private int maxConnections = 9;

    [Header("로비 기본값")]
    [Tooltip("호스트 시작 시 팀당 기본 슬롯 수.")]
    [SerializeField] private int defaultSlotsPerTeam = 3;
    [Tooltip("팀당 최대 슬롯 수.")]
    [SerializeField] private int maxSlotsPerTeam = 5;

    // 동기화되는 슬롯 목록(호스트가 편집).
    private readonly NetworkList<TeamSlot> slots = new NetworkList<TeamSlot>();
    private readonly NetworkVariable<bool> matchStarted = new NetworkVariable<bool>(false);

    private Screen screen = Screen.Main;
    private Vector2 scroll;

    // Relay 접속 상태 (OnGUI는 await를 못 하므로 비동기 결과를 필드로 받아 표시한다).
    private string joinCodeInput = "";
    private string hostJoinCode = "";
    private string statusMessage = "";
    private bool isConnecting;

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
        }

        matchStarted.OnValueChanged += HandleMatchStartedChanged;
        screen = Screen.Room;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;

        matchStarted.OnValueChanged -= HandleMatchStartedChanged;
    }

    /// <summary>
    /// 호스트가 경기를 시작하면 클라이언트도 경기 화면으로 넘어간다.
    /// 경기 흐름(카운트다운·점수·시간)은 서버가 굴려서 복제하므로, 여기서는
    /// 씬에 고정된 오프라인 캐릭터만 치우면 된다.
    /// </summary>
    private void HandleMatchStartedChanged(bool previous, bool current)
    {
        if (!current || IsServer) return; // 서버는 SvStartMatch에서 이미 처리했다

        if (MatchSpawner.Instance != null)
            MatchSpawner.Instance.PrepareForNetworkMatch();
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (!IsServer) return;
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
    }

    // ---------------- 서버(호스트) 슬롯 편집 ----------------

    private void SvAddSlot(byte team)
    {
        if (!IsServer) return;
        if (CountTeam(team) >= maxSlotsPerTeam) return;
        slots.Add(new TeamSlot { team = team, type = Occupant.Empty, clientId = 0 });
    }

    private void SvRemoveSlot(int index)
    {
        if (!IsServer || index < 0 || index >= slots.Count) return;
        slots.RemoveAt(index);
    }

    private void SvSet(int index, Occupant type, ulong clientId)
    {
        if (!IsServer || index < 0 || index >= slots.Count) return;
        TeamSlot s = slots[index];
        s.type = type; s.clientId = clientId;
        slots[index] = s;
    }

    private void SvAssignFirstHuman(int index)
    {
        if (!IsServer) return;
        ulong c = FirstUnassignedHuman();
        if (c == ulong.MaxValue) return; // 배치 가능한 미배치 인원 없음
        SvSet(index, Occupant.Human, c);
    }

    private void SvStartMatch()
    {
        if (!IsServer) return;
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

    /// <summary>어느 Human 슬롯에도 배치되지 않은 접속 클라 중 첫 번째. 없으면 ulong.MaxValue.</summary>
    private ulong FirstUnassignedHuman()
    {
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
        {
            bool placed = false;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].type == Occupant.Human && slots[i].clientId == id) { placed = true; break; }
            if (!placed) return id;
        }
        return ulong.MaxValue;
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
        if (matchStarted.Value) return;

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
        float w = 360, h = 300;
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

        GUILayout.Space(6);
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

        GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        GUILayout.Label("환경: " + RelayConnectionService.EnvironmentName, small);

        string playerId = RelayConnectionService.PlayerId;
        GUILayout.Label("플레이어 ID: " + (string.IsNullOrEmpty(playerId) ? "(로그인 전)" : playerId), small);
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

    private void DrawRoom(NetworkManager nm)
    {
        float w = 720, h = 460;
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

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(330));
        GUILayout.BeginHorizontal();
        DrawTeamColumn(0, "BLUE");
        GUILayout.Space(12);
        DrawTeamColumn(1, "RED");
        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();

        // 방에서도 접속/이탈 상황이 보여야 상대가 왜 안 들어오는지 알 수 있다.
        DrawConnectionReport();

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (IsServer)
        {
            if (GUILayout.Button("게임 시작", GUILayout.Height(40)))
                SvStartMatch();
        }
        else
        {
            GUILayout.Label("호스트가 게임을 시작하기를 기다리는 중...");
        }
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
                if (GUILayout.Button("사람", GUILayout.Width(46))) SvAssignFirstHuman(i);
                if (GUILayout.Button("✕", GUILayout.Width(28))) SvRemoveSlot(i);
            }
            GUILayout.EndHorizontal();
        }

        if (IsServer && CountTeam(team) < maxSlotsPerTeam)
        {
            if (GUILayout.Button("+ 칸 추가"))
                SvAddSlot(team);
        }

        GUILayout.EndVertical();
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
