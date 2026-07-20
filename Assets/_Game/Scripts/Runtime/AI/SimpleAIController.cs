using UnityEngine;

/// <summary>
/// 더미 상대용 단순 상태 기반 AI.
/// 새 이동/전투 로직을 만들지 않는다 — 사람 플레이어와 "같은 몸"(CharacterMotor / CombatController /
/// PlayerBallHandler)을 그대로 호출해서 동작한다. PlayerInput이 하던 역할을 AI가 대신한다.
///
/// 상태:
///  - ChaseBall : 공이 무소유일 때, 공으로 이동(가까워지면 기존 소유 시스템이 알아서 줍는다).
///  - Attack    : 내가 공을 소유했을 때, 공격 목표 골로 드리블 후 사거리 안에서 슛.
///  - Defend    : 사람 플레이어가 공을 소유했을 때, 접근해서 태클/펀치로 공을 털어낸다.
///
/// 기절 중에는 아무 판단/행동도 하지 않는다(기존 기절 로직 존중).
/// </summary>
[RequireComponent(typeof(CharacterState))]
[RequireComponent(typeof(CombatController))]
[RequireComponent(typeof(PlayerBallHandler))]
public class SimpleAIController : MonoBehaviour
{
    public enum AIState { Idle, ChaseBall, Attack, Defend }

    [Header("Targets")]
    [Tooltip("AI가 공격(슛)할 목표 골. 보통 '플레이어 골'.")]
    [SerializeField] private Transform attackGoal;
    [Tooltip("AI가 지키는 자기 골 (참고용, 선택).")]
    [SerializeField] private Transform ownGoal;
    [Tooltip("공의 Rigidbody. 비워두면 이름 'Ball'로 자동 검색한다.")]
    [SerializeField] private Rigidbody ball;

    [Header("Ranges / Timing")]
    [Tooltip("슛 사거리: 공격 골까지 이 거리(평면) 안이고 골을 바라보면 슛한다.")]
    [SerializeField] private float shootRange = 8f;
    [Tooltip("태클 시도 거리: 공 가진 플레이어와 이 거리(평면) 안이면 슬라이딩을 시도한다.")]
    [SerializeField] private float tackleRange = 1.8f;
    [Tooltip("이 거리보다 더 가까우면 슬라이딩 대신 펀치를 쓴다.")]
    [SerializeField] private float punchDistance = 1.1f;
    [Tooltip("목표에 이 정도 붙으면 이동을 멈춘다(도착 판정).")]
    [SerializeField] private float arriveDistance = 0.3f;
    [Tooltip("슛 판정 시 골 방향과 정렬 임계값(내적). 1에 가까울수록 정확히 바라봐야 슛.")]
    [SerializeField] private float shootAlignDot = 0.9f;
    [Tooltip("판단 주기(초). 상태를 재평가하는 간격.")]
    [SerializeField] private float decisionInterval = 0.15f;

    private CharacterState state;
    private CharacterLocomotion locomotion;
    private CombatController combat;
    private PlayerBallHandler handler;

    private AIState current = AIState.Idle;
    private float nextDecisionTime;

    /// <summary>현재 AI 상태 (디버그/확인용).</summary>
    public AIState CurrentState => current;

    private void Awake()
    {
        state = GetComponent<CharacterState>();
        locomotion = GetComponent<CharacterLocomotion>();
        if (locomotion == null)
            locomotion = gameObject.AddComponent<CharacterLocomotion>();

        combat = GetComponent<CombatController>();
        handler = GetComponent<PlayerBallHandler>();

        if (ball == null)
        {
            GameObject ballGo = GameObject.Find("Ball");
            if (ballGo != null) ball = ballGo.GetComponent<Rigidbody>();
        }
        if (attackGoal == null)
            Debug.LogWarning("[SimpleAIController] Attack Goal이 지정되지 않았습니다. Inspector에서 지정하세요.", this);
    }

    private void Update()
    {
        // 킥오프 대기/경기 종료 중엔 판단/행동 정지.
        if (!GameManager.PlayActive)
        {
            locomotion.SetMoveInput(Vector3.zero);
            return;
        }

        // 기절 중이면 아무 판단/행동도 하지 않는다 (이동 정지).
        if (state.IsStunned)
        {
            locomotion.SetMoveInput(Vector3.zero);
            return;
        }

        // 주기적으로 상태 재평가.
        if (Time.time >= nextDecisionTime)
        {
            nextDecisionTime = Time.time + decisionInterval;
            current = DecideState();
        }

        switch (current)
        {
            case AIState.ChaseBall: DoChaseBall(); break;
            case AIState.Attack:    DoAttack();    break;
            case AIState.Defend:    DoDefend();    break;
            default:                locomotion.SetMoveInput(Vector3.zero); break;
        }
    }

    /// <summary>공 소유 상태에 따라 상태를 결정한다.</summary>
    private AIState DecideState()
    {
        PlayerBallHandler owner = PlayerBallHandler.CurrentOwner;
        if (owner == handler) return AIState.Attack;     // 내가 공 소유
        if (owner == null)    return AIState.ChaseBall;  // 무소유 → 주우러 간다
        return AIState.Defend;                           // 상대(사람)가 소유 → 수비/태클
    }

    // --- 상태별 행동 (모두 기존 몸 컴포넌트만 호출) ---

    private void DoChaseBall()
    {
        if (ball == null) { locomotion.SetMoveInput(Vector3.zero); return; }
        MoveToward(ball.position); // 가까워지면 PlayerBallHandler가 자동으로 소유
    }

    private void DoAttack()
    {
        if (attackGoal == null) { locomotion.SetMoveInput(Vector3.zero); return; }

        Vector3 goalPos = attackGoal.position;
        MoveToward(goalPos); // 골 방향으로 드리블 (motor가 그 방향을 바라보게 회전)

        // 사거리 안 + 골을 바라보면 슛.
        if (PlanarDistance(transform.position, goalPos) <= shootRange)
        {
            Vector3 toGoal = goalPos - transform.position;
            toGoal.y = 0f;
            if (toGoal.sqrMagnitude > 0.0001f &&
                Vector3.Dot(transform.forward, toGoal.normalized) >= shootAlignDot)
            {
                handler.Shoot();
            }
        }
    }

    private void DoDefend()
    {
        PlayerBallHandler owner = PlayerBallHandler.CurrentOwner;
        if (owner == null) { locomotion.SetMoveInput(Vector3.zero); return; }

        Vector3 targetPos = owner.transform.position;
        MoveToward(targetPos); // 공 가진 플레이어에게 접근

        float dist = PlanarDistance(transform.position, targetPos);
        if (dist <= tackleRange)
        {
            // 아주 가까우면 펀치, 조금 멀면 슬라이딩. 쿨다운은 각 기능이 내부에서 존중.
            if (dist <= punchDistance)
                combat.Punch();
            else
                combat.SlideTackle();
        }
    }

    // --- 유틸 ---

    /// <summary>목표 지점을 향해 이동 방향을 준다(부드럽게 직진). 도착 거리 안이면 정지.</summary>
    private void MoveToward(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        if (dir.magnitude <= arriveDistance)
        {
            locomotion.SetMoveInput(Vector3.zero);
            return;
        }
        locomotion.SetMoveInput(dir.normalized);
    }

    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
