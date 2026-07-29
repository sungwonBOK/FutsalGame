using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 접속이 성공/실패했을 때 무슨 일이 있었는지 사람이 읽을 수 있는 문장으로 남긴다.
///
/// 접속은 여러 단계로 나뉘고(Relay 조회 → 호스트에 연결 → 승인 → 로비 진입) 어디서 끊겼는지에 따라
/// 대처가 완전히 다르다. 그런데 NetworkManager는 콜백으로만 알려주기 때문에,
/// 그대로 두면 화면에는 아무 설명 없이 조용히 실패한 것처럼 보인다.
/// 여기서 이벤트를 한곳에 모아 해석하고, 로비 UI가 그 문장을 그대로 보여준다.
///
/// 방을 만든 사람과 들어가는 사람이 보는 정보가 다르다는 점도 감안한다.
///  - 들어가는 사람: 왜 못 들어갔는지(코드 문제인지, 호스트가 없는지)
///  - 방을 만든 사람: 누가 들어왔고 누가 나갔는지
/// 다만 조인코드 조회 단계에서 실패하면 호스트에는 아무 기록도 남지 않는다.
/// 그 시도는 호스트까지 도달조차 하지 못하기 때문이다.
/// </summary>
public class NetworkConnectionReporter : MonoBehaviour
{
    public static NetworkConnectionReporter Instance { get; private set; }

    /// <summary>가장 최근 연결 관련 상황 설명. 없으면 빈 문자열.</summary>
    public string LastMessage { get; private set; } = "";

    /// <summary>문제 상황이면 true (UI에서 경고색으로 보여주기 위한 구분).</summary>
    public bool LastMessageIsProblem { get; private set; }

    private NetworkManager subscribed;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
    }

    // NetworkManager는 접속을 시작할 때 준비되므로, 매 프레임 확인해 늦지 않게 붙는다.
    private void Update()
    {
        NetworkManager current = NetworkManager.Singleton;
        if (current == subscribed) return;

        Unsubscribe();
        if (current == null) return;

        current.OnConnectionEvent += HandleConnectionEvent;
        current.OnTransportFailure += HandleTransportFailure;
        current.OnClientStopped += HandleClientStopped;
        subscribed = current;
    }

    private void Unsubscribe()
    {
        if (subscribed == null) return;

        subscribed.OnConnectionEvent -= HandleConnectionEvent;
        subscribed.OnTransportFailure -= HandleTransportFailure;
        subscribed.OnClientStopped -= HandleClientStopped;
        subscribed = null;
    }

    /// <summary>새 접속 시도를 시작할 때 이전 결과를 지운다.</summary>
    public void Clear()
    {
        LastMessage = "";
        LastMessageIsProblem = false;
    }

    public void Report(string message, bool isProblem)
    {
        LastMessage = message;
        LastMessageIsProblem = isProblem;
    }

    private void HandleConnectionEvent(NetworkManager nm, ConnectionEventData data)
    {
        bool isAboutMe = data.ClientId == nm.LocalClientId;

        switch (data.EventType)
        {
            case ConnectionEvent.ClientConnected:
                if (isAboutMe && !nm.IsServer)
                    Report("호스트에 연결되었습니다.", false);
                else if (nm.IsServer && !isAboutMe)
                    Report($"참가자가 들어왔습니다 (ID {data.ClientId}).", false);
                break;

            case ConnectionEvent.PeerConnected:
                Report($"다른 참가자가 들어왔습니다 (ID {data.ClientId}).", false);
                break;

            case ConnectionEvent.ClientDisconnected:
                if (isAboutMe && !nm.IsServer)
                    Report(DescribeLocalDisconnect(nm), true);
                else if (nm.IsServer && !isAboutMe)
                    Report($"참가자가 나갔습니다 (ID {data.ClientId}).", true);
                break;

            case ConnectionEvent.PeerDisconnected:
                Report($"다른 참가자가 나갔습니다 (ID {data.ClientId}).", true);
                break;
        }
    }

    /// <summary>내 연결이 끊겼을 때, 호스트가 알려준 사유가 있으면 그대로 쓰고 없으면 짚어줄 만한 것을 안내한다.</summary>
    private static string DescribeLocalDisconnect(NetworkManager nm)
    {
        string reason = nm.DisconnectReason;
        if (!string.IsNullOrEmpty(reason))
            return "연결이 끊겼습니다 — " + reason;

        return "호스트에 연결하지 못했습니다. " +
               "방이 이미 닫혔거나, 호스트가 플레이를 종료했거나, 양쪽 버전이 다를 수 있습니다.";
    }

    private void HandleTransportFailure()
    {
        Report("네트워크 전송이 끊겼습니다. 호스트가 종료되었거나 인터넷 연결에 문제가 있습니다.", true);
    }

    private void HandleClientStopped(bool wasHost)
    {
        // 호스트가 스스로 닫은 경우까지 실패로 보이면 헷갈리므로, 사유가 있을 때만 남긴다.
        if (wasHost) return;

        NetworkManager nm = NetworkManager.Singleton;
        if (nm != null && !string.IsNullOrEmpty(nm.DisconnectReason))
            Report("연결이 끊겼습니다 — " + nm.DisconnectReason, true);
    }
}
