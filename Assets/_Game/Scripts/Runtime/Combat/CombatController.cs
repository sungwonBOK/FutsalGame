using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 펀치와 슬라이딩 태클을 담당한다.
///
/// 온라인 경기에서는 "누가 맞았는가"를 서버가 정한다.
/// 각자 클라이언트가 자기 화면 기준으로 판정하면 서로 다른 결과가 나오기 때문이다.
///  - 모션과 대시는 조작한 본인이 바로 실행한다(반응이 늦으면 조작감이 나빠지고, 이동은 본인 권한이다).
///  - 맞았는지 판정과 그 결과(기절/넉백/공 뺏기)는 서버만 처리한다.
///  - 연출(이펙트/소리/카메라 흔들림)은 서버가 모두에게 재생시킨다.
/// 오프라인 플레이에서는 전부 로컬에서 그대로 처리된다.
/// </summary>
[RequireComponent(typeof(CharacterState))]
[RequireComponent(typeof(CharacterMotor))]
public class CombatController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private CombatConfig config;

    [Header("Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject slideDustPrefab;

    private CharacterState state;
    private CharacterMotor motor;
    private CharacterLocomotion locomotion;
    private CharacterAnimator anim;
    private ThirdPersonActionCamera actionCamera;
    private CombatConfig runtimeConfig;
    private NetworkPlayerAgent netAgent;

    /// <summary>온라인 경기로 스폰된 선수인지.</summary>
    private bool IsNetworked => netAgent != null && netAgent.IsSpawned;

    /// <summary>내가 조종하지만 서버는 아닌 경우 — 판정을 서버에 맡겨야 한다.</summary>
    private bool ForwardsToServer => IsNetworked && netAgent.IsOwner && !netAgent.IsServer;

    /// <summary>맞았는지 판정해도 되는지. 오프라인이면 항상, 온라인이면 서버만.</summary>
    private bool HasHitAuthority => !IsNetworked || netAgent.IsServer;

    private readonly CombatActionCooldownTracker actionCooldowns = new CombatActionCooldownTracker();
    private float lastSlideTime = -999f;
    private float slideActiveUntil = -999f;
    private readonly HashSet<CharacterState> hitThisSlide = new HashSet<CharacterState>();
    private static readonly Collider[] overlapBuffer = new Collider[16];

    private CombatConfig Config
    {
        get
        {
            if (config == null)
            {
                if (runtimeConfig == null)
                    runtimeConfig = ScriptableObject.CreateInstance<CombatConfig>();
                return runtimeConfig;
            }

            return config;
        }
    }

    public float PunchCooldown => Config.Punch.cooldown;
    public float CrossPunchCooldown => Config.TryGetAction(CombatActionId.CrossPunch, out CombatActionDefinition crossPunch)
        ? crossPunch.cooldown
        : 0f;
    public float SlideCooldown => Config.Tackle.cooldown;
    public float PunchRemaining => actionCooldowns.GetRemaining(CombatActionId.BasicPunch, Time.time, PunchCooldown);
    public float CrossPunchRemaining => actionCooldowns.GetRemaining(CombatActionId.CrossPunch, Time.time, CrossPunchCooldown);
    public float SlideRemaining => Mathf.Max(0f, SlideCooldown - (Time.time - lastSlideTime));
    public float PunchCooldown01 => Mathf.Clamp01(PunchRemaining / Mathf.Max(0.0001f, PunchCooldown));
    public float SlideCooldown01 => Mathf.Clamp01(SlideRemaining / Mathf.Max(0.0001f, SlideCooldown));
    public bool IsPunchReady => PunchRemaining <= 0f;
    public bool IsCrossPunchReady => CrossPunchRemaining <= 0f;
    public bool IsSlideReady => SlideRemaining <= 0f;
    public bool IsSliding => Time.time < slideActiveUntil;
    public float LastPunchRejectedTime { get; private set; } = -999f;
    public float LastSlideRejectedTime { get; private set; } = -999f;

    private void Awake()
    {
        state = GetComponent<CharacterState>();
        motor = GetComponent<CharacterMotor>();
        locomotion = GetComponent<CharacterLocomotion>();
        anim = GetComponent<CharacterAnimator>();
        netAgent = GetComponent<NetworkPlayerAgent>();

        if (Camera.main != null)
            actionCamera = Camera.main.GetComponent<ThirdPersonActionCamera>();
    }

    private void OnDestroy()
    {
        if (runtimeConfig != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeConfig);
            else
                DestroyImmediate(runtimeConfig);
        }
    }

    public void Punch()
    {
        Punch(transform.forward);
    }

    public void Punch(Vector3 actionDirection)
    {
        if (state != null && state.IsStunned)
            return;
        if (locomotion != null && locomotion.IsDodging)
            return;

        CombatConfig.PunchSettings punch = Config.Punch;
        if (!actionCooldowns.TryConsume(CombatActionId.BasicPunch, Time.time, punch.cooldown))
        {
            LastPunchRejectedTime = Time.time;
            return;
        }

        Vector3 lockedDirection = ResolveCombatDirection(actionDirection);
        // 동작한 본인은 곧바로, 나머지는 아래 브로드캐스트로 한 번씩만 재생한다.
        if (PlaysPresentationLocally)
            PlayActionPresentationLocal(CombatActionKind.Punch);

        if (ForwardsToServer)
        {
            // 판정은 서버가 한다. 모션은 위에서 이미 내 화면에 나갔다.
            netAgent.RequestCombatActionRpc(CombatActionKind.Punch, actionDirection);
            return;
        }

        if (IsNetworked)
            netAgent.BroadcastCombatAnimation(CombatActionKind.Punch);

        if (!HasHitAuthority)
            return;

        Vector3 center = transform.position + lockedDirection * punch.range;
        int count = Physics.OverlapSphereNonAlloc(center, punch.radius, overlapBuffer);
        CharacterState nearest = null;
        float nearestDistanceSq = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider c = overlapBuffer[i];
            CharacterState victim = c.GetComponentInParent<CharacterState>();
            if (victim == null || victim == state)
                continue;

            float distanceSq = (victim.transform.position - center).sqrMagnitude;
            if (distanceSq < nearestDistanceSq)
            {
                nearest = victim;
                nearestDistanceSq = distanceSq;
            }
        }

        if (nearest != null)
            Hit(nearest, punch.knockbackForce, punch.hitStunTime);
    }

    public void CrossPunch(Vector3 actionDirection)
    {
        if (state != null && state.IsStunned)
            return;
        if (locomotion != null && locomotion.IsDodging)
            return;

        if (!Config.TryGetAction(CombatActionId.CrossPunch, out CombatActionDefinition crossPunch))
            return;
        if (!actionCooldowns.TryConsume(CombatActionId.CrossPunch, Time.time, crossPunch.cooldown))
            return;

        Vector3 lockedDirection = ResolveCombatDirection(actionDirection);

        // 기본 펀치와 같은 규칙: 본인은 바로 재생, 판정은 서버.
        if (PlaysPresentationLocally)
            PlayActionPresentationLocal(CombatActionKind.CrossPunch);

        if (ForwardsToServer)
        {
            netAgent.RequestCombatActionRpc(CombatActionKind.CrossPunch, actionDirection);
            return;
        }

        if (IsNetworked)
            netAgent.BroadcastCombatAnimation(CombatActionKind.CrossPunch);

        if (!HasHitAuthority)
            return;

        Vector3 center = transform.position + lockedDirection * crossPunch.range;
        int count = Physics.OverlapSphereNonAlloc(center, crossPunch.radius, overlapBuffer);
        CharacterState nearest = null;
        float nearestDistanceSq = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider c = overlapBuffer[i];
            CharacterState victim = c.GetComponentInParent<CharacterState>();
            if (victim == null || victim == state)
                continue;

            float distanceSq = (victim.transform.position - center).sqrMagnitude;
            if (distanceSq < nearestDistanceSq)
            {
                nearest = victim;
                nearestDistanceSq = distanceSq;
            }
        }

        if (nearest != null)
            Hit(nearest, crossPunch.knockbackForce, crossPunch.hitStunTime);
    }

    public void SlideTackle()
    {
        SlideTackle(transform.forward);
    }

    public void SlideTackle(Vector3 actionDirection)
    {
        if (state != null && state.IsStunned)
            return;
        if (locomotion != null && locomotion.IsDodging)
            return;

        CombatConfig.TackleSettings tackle = Config.Tackle;
        if (Time.time - lastSlideTime < tackle.cooldown)
        {
            LastSlideRejectedTime = Time.time;
            return;
        }

        Vector3 lockedDirection = ResolveCombatDirection(actionDirection);
        lastSlideTime = Time.time;

        slideActiveUntil = Time.time + tackle.activeTime;
        hitThisSlide.Clear();
        // 대시는 이동이라 소유자만 실제로 움직인다(비소유 인스턴스는 모터가 꺼져 있어 무시된다).
        motor.Dash(lockedDirection * Config.TackleVelocity, tackle.activeTime);

        if (PlaysPresentationLocally)
            PlayActionPresentationLocal(CombatActionKind.SlideTackle);

        if (ForwardsToServer)
        {
            // 서버도 같은 슬라이딩 창을 열어야 그 동안의 접촉을 판정할 수 있다.
            netAgent.RequestCombatActionRpc(CombatActionKind.SlideTackle, actionDirection);
            return;
        }

        if (IsNetworked)
            netAgent.BroadcastCombatAnimation(CombatActionKind.SlideTackle);
    }

    /// <summary>
    /// 동작 연출을 이 자리에서 바로 재생할지.
    /// 온라인에서 남의 선수를 대신 처리하는 중이라면(서버가 원격 선수의 요청을 실행하는 경우)
    /// 여기서 재생하지 않고 브로드캐스트에 맡겨야 호스트에서 두 번 나오지 않는다.
    /// </summary>
    private bool PlaysPresentationLocally => !IsNetworked || netAgent.IsOwner;

    /// <summary>전투 동작 연출(모션/먼지)을 이 클라이언트에서만 재생한다(판정 없음).</summary>
    public void PlayActionPresentationLocal(CombatActionKind kind)
    {
        if (anim == null && kind != CombatActionKind.SlideTackle)
            return;

        switch (kind)
        {
            case CombatActionKind.Punch:
                anim.PlayPunch();
                return;

            case CombatActionKind.CrossPunch:
                anim.PlayCrossPunch();
                return;

            default:
                if (anim != null) anim.PlaySlide();
                if (slideDustPrefab != null)
                    Instantiate(slideDustPrefab, transform.position + Vector3.down, Quaternion.identity, transform);
                return;
        }
    }

    private void FixedUpdate()
    {
        // 슬라이딩 도중의 접촉 판정도 서버만 한다.
        if (!HasHitAuthority)
            return;

        if (Time.time >= slideActiveUntil)
            return;

        int count = Physics.OverlapSphereNonAlloc(transform.position, Config.Tackle.hitRadius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider c = overlapBuffer[i];
            CharacterState victim = c.GetComponentInParent<CharacterState>();
            if (victim != null && victim != state && !hitThisSlide.Contains(victim))
            {
                CombatConfig.TackleSettings tackle = Config.Tackle;
                if (Hit(victim, tackle.knockbackForce, tackle.hitStunTime))
                    hitThisSlide.Add(victim);
            }
        }
    }

    public void ResetCombatState()
    {
        actionCooldowns.Clear();
        lastSlideTime = -999f;
        slideActiveUntil = -999f;
        hitThisSlide.Clear();
    }

    public static Vector3 CorrectActionDirectionTowardTarget(
        Vector3 origin,
        Vector3 intendedDirection,
        Vector3 targetPosition,
        float maxAngle,
        float strength)
    {
        Vector3 intended = CharacterMovementUtility.NormalizePlanar(intendedDirection);
        if (intended.sqrMagnitude < 0.0001f)
            intended = Vector3.forward;

        Vector3 toTarget = targetPosition - origin;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f)
            return intended;

        Vector3 targetDirection = toTarget.normalized;
        float angle = Vector3.Angle(intended, targetDirection);
        if (angle > maxAngle)
            return intended;

        return Vector3.Slerp(intended, targetDirection, Mathf.Clamp01(strength)).normalized;
    }

    private Vector3 ResolveCombatDirection(Vector3 requestedDirection)
    {
        Vector3 direction = CharacterMovementUtility.NormalizePlanar(requestedDirection);
        if (direction.sqrMagnitude < 0.0001f)
            direction = CharacterMovementUtility.ResolveActionDirection(false, Vector3.zero, transform.forward);

        CharacterState target = FindForwardAssistTarget(direction);
        if (target == null)
            return direction;

        CombatConfig.AssistSettings assist = Config.Assist;
        return CorrectActionDirectionTowardTarget(
            transform.position,
            direction,
            target.transform.position,
            assist.forwardAutoAimAngle,
            assist.forwardAutoAimStrength);
    }

    private CharacterState FindForwardAssistTarget(Vector3 direction)
    {
        CombatConfig.AssistSettings assist = Config.Assist;
        Collider[] cols = Physics.OverlapSphere(transform.position, Mathf.Max(0.01f, assist.forwardAutoAimRange));
        CharacterState best = null;
        float bestAngle = float.PositiveInfinity;

        foreach (Collider c in cols)
        {
            CharacterState candidate = c.GetComponentInParent<CharacterState>();
            if (candidate == null || candidate == state)
                continue;

            Vector3 toCandidate = candidate.transform.position - transform.position;
            toCandidate.y = 0f;
            if (toCandidate.sqrMagnitude < 0.0001f)
                continue;

            float angle = Vector3.Angle(direction, toCandidate.normalized);
            if (angle <= assist.forwardAutoAimAngle && angle < bestAngle)
            {
                best = candidate;
                bestAngle = angle;
            }
        }

        return best;
    }

    private bool Hit(CharacterState victim, float knockbackForce, float stunDuration)
    {
        if (victim.IsInvulnerable)
        {
            victim.NotifyEvaded();
            return false;
        }

        Vector3 dir = victim.transform.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = transform.forward;
        dir.Normalize();

        Vector3 hitPosition = victim.transform.position + Vector3.up * 1f - dir * 0.3f;
        if (IsNetworked)
        {
            NetworkPlayerAgent victimAgent = victim.GetComponent<NetworkPlayerAgent>();
            ulong victimObjectId = victimAgent != null && victimAgent.IsSpawned ? victimAgent.NetworkObjectId : 0;
            netAgent.BroadcastHitPresentation(hitPosition, dir, victimObjectId); // 호스트 자신도 포함해 재생된다
        }
        else
        {
            PlayHitPresentationLocal(hitPosition, dir, involvesLocalPlayer: true);
        }

        PlayerBallHandler victimBall = victim.GetComponent<PlayerBallHandler>();
        if (victimBall != null && victimBall.HasBall)
        {
            float ballKnockForce = Config.Tackle.ballKnockForce;
            Vector3 ballImpulse = dir * ballKnockForce + Vector3.up * (ballKnockForce * 0.3f);
            victimBall.ForceRelease(ballImpulse);
        }

        victim.ApplyHit(dir * knockbackForce, stunDuration);
        return true;
    }

    /// <summary>
    /// 피격 연출을 이 클라이언트에서만 재생한다(연출 복제의 도착 지점).
    /// 이펙트와 소리는 누구의 싸움이든 보여주되, 카메라 흔들림은 내가 때렸거나 맞았을 때만 준다.
    /// 남들끼리 부딪힐 때마다 내 화면이 흔들리면 정신없기 때문이다.
    /// </summary>
    public void PlayHitPresentationLocal(Vector3 hitPosition, Vector3 hitDirection, bool involvesLocalPlayer)
    {
        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, hitPosition, Quaternion.LookRotation(-hitDirection));

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayHit();

        if (involvesLocalPlayer && actionCamera != null)
            actionCamera.PlayHitShake();
    }
}
