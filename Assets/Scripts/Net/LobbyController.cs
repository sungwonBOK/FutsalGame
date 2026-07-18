using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// 초기 화면 → LAN 접속 → 방(로비) 흐름을 담당한다.
/// 로비는 팀별 슬롯을 자유롭게 추가/삭제하고, 각 칸을 [빈칸 / AI / 접속한 사람]으로 배치한다(3v3 고정 아님).
/// 슬롯 구성은 NetworkList로 동기화되어 호스트가 편집하고 모든 클라가 본다.
///
/// 씬의 in-scene NetworkObject에 붙인다(호스트 시작 시 스폰). OnGUI는 스폰 전에도 돌기 때문에
/// 접속 전에는 메뉴/LAN UI를, 접속 후(IsSpawned)에는 방 UI를 그린다.
///
/// NOTE: 구성한 팀을 실제 네트워크 경기로 스폰/동기화하는 것은 게임로직 네트워크화(다음 단계)에서 연결한다.
/// 지금 "게임 시작"은 기존 경기(GameManager.BeginMatch)를 시작하고 로비를 닫는다.
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
    private enum Screen { Main, Lan, Room }

    [Header("접속")]
    [SerializeField] private string ipAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

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
        screen = Screen.Room;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
            NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnect;
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
        // 기존 경기 시작(placeholder). 팀별 스폰은 다음 단계에서 연결.
        if (GameManager.Instance != null) GameManager.Instance.BeginMatch();
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
            if (GUILayout.Button("온라인 플레이 (LAN)", GUILayout.Height(48)))
                screen = Screen.Lan;
        }
        else // Lan
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

    private void DrawRoom(NetworkManager nm)
    {
        float w = 720, h = 460;
        Rect area = new Rect((UnityEngine.Screen.width - w) / 2f, (UnityEngine.Screen.height - h) / 2f, w, h);
        GUILayout.BeginArea(area, GUI.skin.box);

        GUILayout.Label("<size=22><b>방 (LAN)</b></size>   " + (IsServer ? "[호스트]" : "[참가자]") +
                        "   접속: " + nm.ConnectedClientsIds.Count + "명   미배치: " + UnassignedHumanCount() + "명",
                        new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Space(6);

        scroll = GUILayout.BeginScrollView(scroll, GUILayout.Height(330));
        GUILayout.BeginHorizontal();
        DrawTeamColumn(0, "BLUE");
        GUILayout.Space(12);
        DrawTeamColumn(1, "RED");
        GUILayout.EndHorizontal();
        GUILayout.EndScrollView();

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
