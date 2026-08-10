public enum P2pSessionStatus : byte
{
    Preparing,
    Ready,
    Reconnecting,
    PeerDisconnected,
    HostUnavailable
}

/// <summary>
/// Keeps reconnect pacing independent from the lobby and WebRTC lifecycle.
/// The delay is intentionally capped so a recovered peer is noticed without an unbounded wait.
/// </summary>
public sealed class P2pReconnectSchedule
{
    private int attemptCount;

    public float NextDelaySeconds()
    {
        switch (attemptCount)
        {
            case 0:
                return 1f;
            case 1:
                return 2f;
            case 2:
                return 5f;
            default:
                return 8f;
        }
    }

    public void RecordAttempt()
    {
        if (attemptCount < 3)
            attemptCount++;
    }

    public void Reset()
    {
        attemptCount = 0;
    }
}

public static class P2pSessionStatusText
{
    public static string For(P2pSessionStatus status)
    {
        switch (status)
        {
            case P2pSessionStatus.Ready:
                return "직접 대전 준비 완료";
            case P2pSessionStatus.Reconnecting:
                return "상대 연결을 다시 확인 중입니다.";
            case P2pSessionStatus.PeerDisconnected:
                return "상대 연결이 끊겼습니다. 자유플레이를 계속하며 재연결을 시도합니다.";
            case P2pSessionStatus.HostUnavailable:
                return "방장이 나가 재연결할 수 없습니다.";
            default:
                return "상대와 직접 대전을 준비 중입니다.";
        }
    }
}
