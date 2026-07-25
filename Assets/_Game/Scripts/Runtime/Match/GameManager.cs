using System.Collections;
using UnityEngine;

/// <summary>
/// 경기 전체 흐름(게임 루프)을 총괄한다.
/// 상태: Kickoff(카운트다운) → Playing(진행) → GameOver(종료). Pause 액션으로 일시정지/재개.
/// 점수·타이머·승패를 관리하고, 각 상태에 맞춰 PlayActive로 입력·AI·공 소유를 잠그거나 푼다.
///
/// 표현(UI)은 이 매니저가 직접 그리지 않는다 — MatchUI가 아래 공개 상태
/// (State/PlayerScore/OpponentScore/TimeRemaining/CenterMessage/IsPaused)를 읽어 담당한다.
/// 즉 "로직=GameManager, 표시=MatchUI"로 분리한다.
///
/// 기존 플레이 로직(이동/슛/전투/득점 판정)은 변경하지 않는다.
/// 모든 플레이어/AI/공은 GameManager.PlayActive만 확인하므로, 상태 전환만으로 락이 걸린다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum MatchState { Kickoff, Playing, GameOver }

    public static GameManager Instance { get; private set; }

    /// <summary>플레이 활성 상태. Playing이면서 일시정지가 아닐 때만 true → 입력/AI/공 소유가 동작한다.</summary>
    public static bool PlayActive { get; private set; }

    [Header("Scene References")]
    [Tooltip("공 Rigidbody. 비우면 이름 'Ball'로 자동 검색.")]
    [SerializeField] private Rigidbody ball;
    [SerializeField] private Transform player;
    [SerializeField] private Transform opponent;
    [SerializeField] private GameplayInputReader inputReader;

    [Header("Match Rules")]
    [Tooltip("경기 제한 시간(초). 기본 180초 = 3분. Playing 중에만 흐른다.")]
    [SerializeField] private float matchDuration = 180f;
    [Tooltip("이 점수에 먼저 도달하면 시간과 무관하게 즉시 경기 종료. 0 이하면 비활성(시간 제한만 사용).")]
    [SerializeField] private int targetScore = 0;

    [Header("Kickoff / Timing")]
    [Tooltip("킥오프 카운트다운 시작 숫자 (3 → \"3, 2, 1, START!\").")]
    [SerializeField] private int countdownFrom = 3;
    [Tooltip("카운트다운 숫자 하나당 표시 시간(초).")]
    [SerializeField] private float countdownStep = 1f;
    [Tooltip("\"START!\" 표시 시간(초).")]
    [SerializeField] private float startFlashDuration = 0.6f;
    [Tooltip("득점 시 \"GOAL!\"을 표시하는 시간(초). 이후 킥오프 카운트다운으로 이어진다.")]
    [SerializeField] private float goalFlashDuration = 1.4f;
    [Tooltip("득점 후 공이 네트로 날아가 펄럭이도록 공 리셋을 지연하는 시간(초). 캐릭터는 즉시 리셋.")]
    [SerializeField] private float netCelebrationDelay = 0.7f;

    [Header("Ball Safety Net")]
    [SerializeField] private Vector2 ballBoundsHalfExtents = new Vector2(22f, 13f);
    [SerializeField] private float ballMinHeight = -3f;
    [SerializeField] private float ballMaxHeight = 25f;

    [Header("Effects")]
    [Tooltip("득점 축하 파티클 프리팹 (득점 팀 색으로 tint).")]
    [SerializeField] private GameObject goalEffectPrefab;

    [Header("Flow")]
    [Tooltip("Play 시 자동으로 경기를 시작할지. 메뉴/로비에서 시작을 제어하려면 끈다(메뉴가 BeginMatch 호출).")]
    [SerializeField] private bool autoStartMatch = true;

    // --- 공개 상태 (MatchUI가 읽는다) ---
    public MatchState State { get; private set; }
    public bool IsPaused { get; private set; }
    public int PlayerScore { get; private set; }
    public int OpponentScore { get; private set; }
    public float TimeRemaining { get; private set; }
    /// <summary>화면 중앙에 크게 표시할 메시지(카운트다운/GOAL!/결과). 빈 문자열이면 표시하지 않는다.</summary>
    public string CenterMessage { get; private set; } = "";

    // 시작 상태 저장 (리셋용)
    private Vector3 ballStart, playerStart, opponentStart;
    private Quaternion playerStartRot, opponentStartRot;
    private Collider ballCollider;
    private ThirdPersonActionCamera actionCamera;
    private bool scoringLocked; // 한 골에 대한 중복 처리 방지

    private void Awake()
    {
        Instance = this;
        State = MatchState.Kickoff;
        IsPaused = false;
        Time.timeScale = 1f;
        RefreshPlayActive();

        if (ball == null)
        {
            GameObject b = GameObject.Find("Ball");
            if (b != null) ball = b.GetComponent<Rigidbody>();
        }
        if (ball != null) ballCollider = ball.GetComponent<Collider>();
        if (Camera.main != null) actionCamera = Camera.main.GetComponent<ThirdPersonActionCamera>();
    }

    private void Start()
    {
        // 시작 위치/회전 저장 (리셋에 사용).
        if (ball != null) ballStart = ball.position;
        if (player != null) { playerStart = player.position; playerStartRot = player.rotation; }
        if (opponent != null) { opponentStart = opponent.position; opponentStartRot = opponent.rotation; }

        if (autoStartMatch)
            StartCoroutine(NewMatchRoutine());
    }

    /// <summary>메뉴/로비에서 경기를 시작시킬 때 호출. (autoStartMatch를 끈 경우)</summary>
    public void BeginMatch()
    {
        StopAllCoroutines();
        StartCoroutine(NewMatchRoutine());
    }

    private void Update()
    {
        // Pause: 일시정지/재개 토글 (종료 화면에서는 무시).
        if (inputReader != null &&
            inputReader.ReadButton(GameplayInputAction.Pause).WasPressed &&
            State != MatchState.GameOver)
            TogglePause();

        // 종료 화면: Restart 액션으로 새 경기.
        if (State == MatchState.GameOver && !IsPaused &&
            inputReader != null &&
            inputReader.ReadButton(GameplayInputAction.Restart).WasPressed)
        {
            BeginMatch();
            return;
        }

        // 경기 시간은 Playing 중에만 흐른다. (일시정지 시 timeScale=0이라 deltaTime=0이지만 상태로도 이중 차단.)
        if (State == MatchState.Playing && !IsPaused)
        {
            EnforceBallBounds();
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                EndMatch();
            }
        }
    }

    /// <summary>일시정지/재개 토글. Time.timeScale로 게임 전체를 멈춘다.</summary>
    private void TogglePause()
    {
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        RefreshPlayActive();
    }

    /// <summary>현재 상태/일시정지 여부로 PlayActive를 갱신한다.</summary>
    private void RefreshPlayActive()
    {
        PlayActive = (State == MatchState.Playing) && !IsPaused;
    }

    // --- 경기 흐름 ---

    /// <summary>0:0, 시간 리셋, 위치 리셋 후 킥오프부터 새 경기 시작. (최초 시작/재시작 공용)</summary>
    private IEnumerator NewMatchRoutine()
    {
        // 재시작이 일시정지 화면에서 눌릴 수 있으니 확실히 복구.
        IsPaused = false;
        Time.timeScale = 1f;

        PlayerScore = 0;
        OpponentScore = 0;
        TimeRemaining = matchDuration;
        scoringLocked = false;

        yield return StartCoroutine(KickoffRoutine());
    }

    /// <summary>"3, 2, 1, START!" 카운트다운. 카운트 동안 PlayActive=false로 모두 정지.</summary>
    private IEnumerator KickoffRoutine()
    {
        State = MatchState.Kickoff;
        RefreshPlayActive();

        ResetCharacters();
        ResetBall();

        for (int n = countdownFrom; n >= 1; n--)
        {
            CenterMessage = n.ToString();
            yield return new WaitForSeconds(countdownStep);
        }
        CenterMessage = "START!";
        yield return new WaitForSeconds(startFlashDuration);

        CenterMessage = "";
        scoringLocked = false;
        State = MatchState.Playing;
        RefreshPlayActive();
    }

    /// <summary>골 트리거가 호출한다. playerScored=true면 플레이어 득점, false면 상대(AI) 득점.</summary>
    public void GoalScored(bool playerScored, Vector3 goalPos)
    {
        if (State != MatchState.Playing || IsPaused || scoringLocked) return;
        scoringLocked = true;

        if (playerScored) PlayerScore++;
        else OpponentScore++;

        // 연출: 골 축하 파티클(득점 팀 색으로 tint) + 소리 (기존 그대로).
        if (goalEffectPrefab != null)
        {
            GameObject fx = Instantiate(goalEffectPrefab, goalPos + Vector3.up * 1f, Quaternion.identity);
            Color c = playerScored ? new Color(1f, 0.85f, 0.2f) : new Color(0.7f, 0.3f, 0.9f);
            foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystem.MainModule m = ps.main;
                m.startColor = c;
            }
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayGoal();
        if (actionCamera != null) actionCamera.AddShake(0.35f);

        bool matchPoint = targetScore > 0 && (PlayerScore >= targetScore || OpponentScore >= targetScore);
        StartCoroutine(GoalRoutine(matchPoint));
    }

    /// <summary>"GOAL!" 표시 → (네트 연출) → 킥오프 카운트다운 또는 경기 종료.</summary>
    private IEnumerator GoalRoutine(bool endMatchAfter)
    {
        State = MatchState.Kickoff; // 득점 연출/리셋 동안 플레이 잠금
        RefreshPlayActive();

        CenterMessage = "GOAL!";
        ResetCharacters();                                    // 캐릭터는 즉시 시작 위치로
        yield return new WaitForSeconds(netCelebrationDelay); // 공이 네트로 날아가 펄럭이는 시간
        ResetBall();                                          // 그 다음 공을 중앙으로

        float remain = goalFlashDuration - netCelebrationDelay;
        if (remain > 0f) yield return new WaitForSeconds(remain);

        if (endMatchAfter)
            EndMatch();
        else
            yield return StartCoroutine(KickoffRoutine());
    }

    /// <summary>경기 종료: 점수를 비교해 승/패/무를 표시하고 재시작 입력을 기다린다.</summary>
    private void EndMatch()
    {
        State = MatchState.GameOver;
        IsPaused = false;
        Time.timeScale = 1f;
        RefreshPlayActive();

        ResetCharacters();
        ResetBall();

        string result;
        if (PlayerScore > OpponentScore) result = "PLAYER WINS!";
        else if (OpponentScore > PlayerScore) result = "AI WINS!";
        else result = "DRAW";

        CenterMessage = result + "\n(R: 재시작)";
    }

    // --- 리셋 유틸 (기존 로직 유지) ---

    /// <summary>공을 소유 해제하고 중앙으로 되돌린다(속도 0).</summary>
    private void ResetBall()
    {
        PlayerBallHandler.ClearPossession();
        if (ball != null)
        {
            if (ballCollider != null) ballCollider.enabled = true;
            ball.isKinematic = false;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            ball.position = ballStart;
            ball.transform.position = ballStart;
        }
    }

    private void EnforceBallBounds()
    {
        if (ball == null || PlayerBallHandler.CurrentOwner != null)
            return;

        Vector3 position = ball.position;
        bool outside =
            Mathf.Abs(position.x) > ballBoundsHalfExtents.x ||
            Mathf.Abs(position.z) > ballBoundsHalfExtents.y ||
            position.y < ballMinHeight ||
            position.y > ballMaxHeight;

        if (outside)
            ResetBall();
    }

    /// <summary>플레이어와 AI를 시작 위치로 되돌린다.</summary>
    private void ResetCharacters()
    {
        ResetCharacter(player, playerStart, playerStartRot);
        ResetCharacter(opponent, opponentStart, opponentStartRot);
    }

    private void ResetCharacter(Transform t, Vector3 pos, Quaternion rot)
    {
        if (t == null) return;

        Rigidbody rb = t.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        t.position = pos;
        t.rotation = rot;

        CharacterState cs = t.GetComponent<CharacterState>();
        if (cs != null) cs.ResetState(); // 기절 등 초기화

        CharacterLocomotion locomotion = t.GetComponent<CharacterLocomotion>();
        if (locomotion != null) locomotion.ResetMobilityState();

        PlayerInput playerInput = t.GetComponent<PlayerInput>();
        if (playerInput != null) playerInput.ClearPreparedActions();

        CombatController combat = t.GetComponent<CombatController>();
        if (combat != null) combat.ResetCombatState();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        PlayActive = true;   // 안전 복구
        Time.timeScale = 1f; // 일시정지 중 파괴돼도 타임스케일 복구
    }
}
