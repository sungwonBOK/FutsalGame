using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 경기 진행 상태(점수·시간·킥오프/종료·중앙 메시지)를 서버 기준으로 복제한다.
///
/// 경기 흐름은 서버 한 곳에서만 굴러간다. 각자 클라이언트가 자기 타이머로 카운트다운을 돌리면
/// 시간과 점수가 조금씩 어긋나서, 한쪽은 끝난 경기를 다른 쪽은 계속 하고 있게 된다.
/// 그래서 서버는 매 프레임 GameManager의 상태를 그대로 퍼뜨리고, 클라이언트는 받아서 반영만 한다.
///
/// 득점 연출처럼 한 번만 터지는 것은 상태로 표현하기 어려우니 별도로 알린다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkMatchState : NetworkBehaviour
{
    public static NetworkMatchState Instance { get; private set; }

    /// <summary>경기 흐름을 직접 굴려도 되는지. 오프라인이면 항상 true, 온라인이면 서버만.</summary>
    public static bool LocalHasAuthority =>
        Instance == null || !Instance.IsSpawned || Instance.IsServer;

    private readonly NetworkVariable<byte> state = new NetworkVariable<byte>((byte)GameManager.MatchState.Kickoff);
    private readonly NetworkVariable<int> playerScore = new NetworkVariable<int>(0);
    private readonly NetworkVariable<int> opponentScore = new NetworkVariable<int>(0);
    private readonly NetworkVariable<float> timeRemaining = new NetworkVariable<float>(0f);
    private readonly NetworkVariable<bool> paused = new NetworkVariable<bool>(false);
    private readonly NetworkVariable<FixedString128Bytes> centerMessage =
        new NetworkVariable<FixedString128Bytes>(default);

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsSpawned) return;

        GameManager match = GameManager.Instance;
        if (match == null) return;

        if (IsServer)
            PublishFrom(match);
        else
            ApplyTo(match);
    }

    /// <summary>남은 시간은 초 단위로만 보이므로, 이만큼 차이 날 때만 새로 보낸다.</summary>
    private const float TimePublishThreshold = 0.05f;

    /// <summary>서버: 지금 경기 상태를 모두에게 퍼뜨린다.</summary>
    private void PublishFrom(GameManager match)
    {
        // 값이 실제로 달라졌을 때만 대입해 매 프레임 불필요한 전송이 생기지 않게 한다.
        byte currentState = (byte)match.State;
        if (state.Value != currentState) state.Value = currentState;
        if (playerScore.Value != match.PlayerScore) playerScore.Value = match.PlayerScore;
        if (opponentScore.Value != match.OpponentScore) opponentScore.Value = match.OpponentScore;
        if (paused.Value != match.IsPaused) paused.Value = match.IsPaused;

        if (Mathf.Abs(timeRemaining.Value - match.TimeRemaining) >= TimePublishThreshold)
            timeRemaining.Value = match.TimeRemaining;

        FixedString128Bytes message = default;
        if (!string.IsNullOrEmpty(match.CenterMessage))
            message = Truncate(match.CenterMessage);
        if (!centerMessage.Value.Equals(message))
            centerMessage.Value = message;
    }

    /// <summary>클라이언트: 복제받은 경기 상태를 그대로 반영한다.</summary>
    private void ApplyTo(GameManager match)
    {
        match.ApplyReplicatedState(
            (GameManager.MatchState)state.Value,
            playerScore.Value,
            opponentScore.Value,
            timeRemaining.Value,
            paused.Value,
            centerMessage.Value.ToString());
    }

    /// <summary>
    /// 고정 길이 문자열 용량을 넘지 않게 자른다.
    /// 실제 메시지("3", "GOAL!", "PLAYER WINS!\n(R: 재시작)")는 모두 이 한도 안이라 잘릴 일이 없고,
    /// 한글이 섞여 글자당 3바이트가 되더라도 여유가 남는다.
    /// </summary>
    private const int MaxMessageCharacters = 30;

    private static FixedString128Bytes Truncate(string text)
    {
        FixedString128Bytes result = default;
        result.Append(text.Length <= MaxMessageCharacters ? text : text.Substring(0, MaxMessageCharacters));
        return result;
    }

    // ---------------- 득점 연출 ----------------

    /// <summary>서버가 득점 연출을 모든 클라이언트에서 재생시킨다.</summary>
    public void BroadcastGoalPresentation(Vector3 goalPosition, bool playerScored)
    {
        if (!IsServer || !IsSpawned) return;

        GoalPresentationRpc(goalPosition, playerScored);
    }

    [Rpc(SendTo.Everyone)]
    private void GoalPresentationRpc(Vector3 goalPosition, bool playerScored)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.PlayGoalPresentationLocal(goalPosition, playerScored);
    }
}
