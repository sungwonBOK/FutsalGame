using System.Collections.Generic;
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
    [SerializeField] private float dribbleCommitTime = 0.6f;
    [SerializeField] private float interceptLeadTime = 0.35f;
    [SerializeField] private float goalAimSpread = 1.1f;

    [Header("Sprint / Dodge")]
    [SerializeField] private float sprintDistance = 5f;
    [SerializeField] private float dodgeThreatRange = 3.2f;
    [SerializeField, Range(0f, 1f)] private float dodgeReactionChance = 0.65f;

    [Header("Defense")]
    [SerializeField] private float engageDistance = 4.5f;
    [SerializeField] private float goalSideOffset = 2.2f;
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
    private CombatController[] opponents;
    private float possessionStartTime;
    private Vector3 aimPoint;
    private bool hadBallLastFrame;
    private bool wasThreatened;

    /// <summary>현재 AI 상태 (디버그/확인용).</summary>
    public AIState CurrentState => current;

    /// <summary>
    /// 네트워크로 스폰된 AI는 팀이 정해진 뒤에야 공격/수비 골을 알 수 있으므로
    /// 서버가 스폰 직후 이 메서드로 목표 골대를 지정한다.
    /// </summary>
    public void ConfigureGoals(Transform attack, Transform own)
    {
        if (attack != null) attackGoal = attack;
        if (own != null) ownGoal = own;
    }

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
        CacheOpponents();
    }

    private void OnEnable()
    {
        // 네트워크 경기에서는 선수들이 순차적으로 스폰되므로, 켜지는 시점에 상대 목록을 다시 잡는다.
        CacheOpponents();
    }

    /// <summary>
    /// 상대 목록을 다시 수집한다. 네트워크 경기에서 모든 선수가 스폰된 뒤
    /// 서버가 호출해 캐시가 뒤처지지 않게 한다.
    /// </summary>
    public void RefreshOpponents() => CacheOpponents();

    private void CacheOpponents()
    {
        CombatController[] all = FindObjectsByType<CombatController>(FindObjectsInactive.Exclude);
        List<CombatController> others = new List<CombatController>(all.Length);
        foreach (CombatController candidate in all)
        {
            // 경기에서 빠진 캐릭터(온라인 경기의 오프라인 배치분)는 오브젝트가 남아 있어도
            // 컴포넌트가 꺼져 있다. 이런 상대를 쫓아다니지 않도록 걸러낸다.
            if (candidate != combat && candidate.isActiveAndEnabled)
                others.Add(candidate);
        }

        opponents = others.ToArray();
    }

    private void Update()
    {
        TrackPossessionChange();
        // 킥오프 대기/경기 종료 중엔 판단/행동 정지.
        if (!GameManager.PlayActive)
        {
            StopMoving();
            return;
        }

        // 기절 중이면 아무 판단/행동도 하지 않는다 (이동 정지).
        if (state.IsStunned)
        {
            StopMoving();
            return;
        }

        ReactToIncomingTackle();

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
            default:                StopMoving(); break;
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
        if (ball == null) { StopMoving(); return; }
        MoveToward(ball.position + ball.linearVelocity * interceptLeadTime);
    }

    private void DoAttack()
    {
        if (attackGoal == null) { StopMoving(); return; }

        Vector3 goalPos = aimPoint;
        MoveToward(goalPos); // 골 방향으로 드리블 (motor가 그 방향을 바라보게 회전)

        if (Time.time - possessionStartTime < dribbleCommitTime)
            return;

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
        if (owner == null) { StopMoving(); return; }

        Vector3 targetPos = owner.transform.position;
        float dist = PlanarDistance(transform.position, targetPos);
        MoveToward(dist > engageDistance ? GoalSideInterceptPoint(targetPos) : targetPos);
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
    private void TrackPossessionChange()
    {
        bool hasBall = handler != null && handler.HasBall;
        if (hasBall && !hadBallLastFrame)
        {
            possessionStartTime = Time.time;
            aimPoint = PickAimPoint();
        }

        hadBallLastFrame = hasBall;
    }

    private void ReactToIncomingTackle()
    {
        CombatController threat = FindIncomingTackle();
        bool threatened = threat != null;
        if (threatened && !wasThreatened && locomotion.CanDodge && Random.value <= dodgeReactionChance)
            locomotion.TryDodge(ChooseDodgeDirection(threat.transform.position));

        wasThreatened = threatened;
    }

    private CombatController FindIncomingTackle()
    {
        if (opponents == null)
            CacheOpponents();

        for (int i = 0; i < opponents.Length; i++)
        {
            CombatController opponent = opponents[i];
            if (opponent == null || !opponent.IsSliding)
                continue;

            Vector3 toSelf = transform.position - opponent.transform.position;
            toSelf.y = 0f;
            if (toSelf.magnitude > dodgeThreatRange)
                continue;

            if (toSelf.sqrMagnitude > 0.0001f && Vector3.Dot(opponent.transform.forward, toSelf.normalized) >= 0.4f)
                return opponent;
        }

        return null;
    }

    private Vector3 ChooseDodgeDirection(Vector3 threatPosition)
    {
        Vector3 awayFromThreat = transform.position - threatPosition;
        awayFromThreat.y = 0f;
        if (awayFromThreat.sqrMagnitude < 0.0001f)
            return -transform.forward;

        Vector3 side = Vector3.Cross(Vector3.up, awayFromThreat.normalized);
        Vector3 preferred = (handler.HasBall ? aimPoint : Vector3.zero) - transform.position;
        preferred.y = 0f;
        return Vector3.Dot(side, preferred) >= 0f ? side : -side;
    }

    private Vector3 GoalSideInterceptPoint(Vector3 carrierPosition)
    {
        if (ownGoal == null)
            return carrierPosition;

        Vector3 toOwnGoal = ownGoal.position - carrierPosition;
        toOwnGoal.y = 0f;
        return toOwnGoal.sqrMagnitude > 0.0001f
            ? carrierPosition + toOwnGoal.normalized * goalSideOffset
            : carrierPosition;
    }

    private Vector3 PickAimPoint()
    {
        if (attackGoal == null)
            return transform.position;

        Vector3 point = attackGoal.position;
        point.z += Random.Range(-goalAimSpread, goalAimSpread);
        return point;
    }

    private void StopMoving()
    {
        locomotion.SetPlayerMoveInput(Vector2.zero, Vector3.zero, sprint: false, hasBall: false);
    }

    private void MoveToward(Vector3 targetPos)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;
        float distance = dir.magnitude;
        if (distance <= arriveDistance)
        {
            StopMoving();
            return;
        }

        locomotion.SetPlayerMoveInput(
            Vector2.zero,
            dir / distance,
            sprint: distance > sprintDistance,
            hasBall: handler != null && handler.HasBall);
    }

    private static float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
