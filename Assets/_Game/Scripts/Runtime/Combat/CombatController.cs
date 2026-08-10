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
    private enum CombatHitKind
    {
        Standard,
        Tackle
    }

    private enum CombatHitResolution
    {
        Applied,
        Blocked,
        Evaded
    }

    [Header("Config")]
    [SerializeField] private CombatConfig config;

    [Header("Effects")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private GameObject slideDustPrefab;

    private CharacterState state;
    private CharacterMotor motor;
    private CharacterLocomotion locomotion;
    private CharacterAnimator anim;
    private GrabController grab;
    private DefenseController defense;
    private ThirdPersonActionCamera actionCamera;
    private CombatConfig runtimeConfig;
    private NetworkPlayerAgent netAgent;

    /// <summary>온라인 경기로 스폰된 선수인지.</summary>
    private bool IsNetworked => netAgent != null && netAgent.IsSpawned;

    /// <summary>내가 조종하지만 서버는 아닌 경우 — 판정을 서버에 맡겨야 한다.</summary>
    private bool ForwardsToServer => IsNetworked && netAgent.IsOwner && !netAgent.IsServer;

    /// <summary>맞았는지 판정해도 되는지. 오프라인이면 항상, 온라인이면 서버만.</summary>
    private bool HasHitAuthority => !IsNetworked || netAgent.IsServer;

    private P2pCombatReplicator DirectP2p => GetComponent<P2pCombatReplicator>();

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

    public float PunchCooldown => Config.TryGetAction(CombatActionId.BasicPunch, out CombatActionDefinition basicPunch)
        ? basicPunch.cooldown
        : Config.Punch.cooldown;
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
        grab = GetComponent<GrabController>();
        if (grab == null)
            grab = gameObject.AddComponent<GrabController>();
        defense = GetComponent<DefenseController>();
        if (defense == null)
            defense = gameObject.AddComponent<DefenseController>();

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

    public bool TryPunch(Vector3 actionDirection)
    {
        if (state != null && (state.IsStunned || state.IsGrabRestricted))
            return false;
        if (locomotion != null && locomotion.IsDodging)
            return false;
        if (!Config.TryGetAction(CombatActionId.BasicPunch, out _) || !IsPunchReady)
            return false;

        Punch(actionDirection);
        return true;
    }

    public void Punch(Vector3 actionDirection)
    {
        if (state != null && (state.IsStunned || state.IsGrabRestricted))
            return;
        if (locomotion != null && locomotion.IsDodging)
            return;

        if (!Config.TryGetAction(CombatActionId.BasicPunch, out CombatActionDefinition punch))
            return;
        if (!actionCooldowns.TryConsume(CombatActionId.BasicPunch, Time.time, punch.cooldown))
        {
            LastPunchRejectedTime = Time.time;
            return;
        }

        Vector3 lockedDirection = ResolveCombatDirection(actionDirection);
        if (TryBeginDirectP2pAction(P2pCombatActionKind.Punch, lockedDirection))
            return;

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
            Hit(nearest, punch.knockbackForce, punch.hitStunTime, punch.releaseBallOnHit, punch.ballKnockbackForce, CombatHitKind.Standard, PowerGaugeGainSource.BasicPunchHit);
    }

    public void CrossPunch(Vector3 actionDirection)
    {
        if (state != null && (state.IsStunned || state.IsGrabRestricted))
            return;
        if (locomotion != null && locomotion.IsDodging)
            return;

        if (!Config.TryGetAction(CombatActionId.CrossPunch, out CombatActionDefinition crossPunch))
            return;
        if (!actionCooldowns.TryConsume(CombatActionId.CrossPunch, Time.time, crossPunch.cooldown))
            return;

        Vector3 lockedDirection = ResolveCombatDirection(actionDirection);

        if (TryBeginDirectP2pAction(P2pCombatActionKind.CrossPunch, lockedDirection))
            return;

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
            Hit(nearest, crossPunch.knockbackForce, crossPunch.hitStunTime, crossPunch.releaseBallOnHit, crossPunch.ballKnockbackForce, CombatHitKind.Standard, PowerGaugeGainSource.CrossPunchHit);
    }

    public bool TryCrossPunch(Vector3 actionDirection)
    {
        if (state != null && (state.IsStunned || state.IsGrabRestricted))
            return false;
        if (locomotion != null && locomotion.IsDodging)
            return false;
        if (!Config.TryGetAction(CombatActionId.CrossPunch, out _) || !IsCrossPunchReady)
            return false;

        CrossPunch(actionDirection);
        return true;
    }

    public void SlideTackle()
    {
        SlideTackle(transform.forward);
    }

    public bool TrySlideTackle(Vector3 actionDirection)
    {
        if (state != null && (state.IsStunned || state.IsGrabRestricted))
            return false;
        if (locomotion != null && locomotion.IsDodging)
            return false;
        if (!IsSlideReady)
            return false;

        SlideTackle(actionDirection);
        return true;
    }

    public void SlideTackle(Vector3 actionDirection)
    {
        if (state != null && (state.IsStunned || state.IsGrabRestricted))
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

        if (TryBeginDirectP2pAction(P2pCombatActionKind.SlideTackle, lockedDirection))
            return;

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
        if (!HasHitAuthority || IsDirectP2pActive)
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
                CombatHitResolution resolution = Hit(
                    victim,
                    tackle.knockbackForce,
                    tackle.hitStunTime,
                    true,
                    tackle.ballKnockForce,
                    CombatHitKind.Tackle,
                    PowerGaugeGainSource.SlideTackleHit);
                if (resolution != CombatHitResolution.Evaded)
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
        grab?.Release();
    }

    public bool TryGrab(Vector3 actionDirection)
    {
        if (state == null || state.IsStunned || state.IsGrabRestricted || locomotion != null && locomotion.IsDodging)
            return false;

        Vector3 lockedDirection = ResolveCombatDirection(actionDirection);
        if (TryBeginDirectP2pAction(P2pCombatActionKind.Grab, lockedDirection))
            return true;

        return grab != null && grab.TryStart(Config.Grab, lockedDirection);
    }

    public bool TryCancelGrab()
    {
        bool cancelled = grab != null && grab.TryCancel();
        if (cancelled && DirectP2p != null && DirectP2p.HasActiveLocalGrab)
            DirectP2p.SendGrabReleased();
        return cancelled;
    }

    public bool TryEscapeGrab(Vector3 actionDirection)
    {
        if (state != null && state.IsHeld && IsDirectP2pActive)
            return false;

        if (state == null || !state.TryEscapeGrab())
            return false;

        return locomotion != null && locomotion.TryDodge(actionDirection);
    }

    public bool TryStartDefense()
    {
        return defense != null && defense.TryStartDefense();
    }

    public bool IsGrabRestricted => state != null && state.IsGrabRestricted;
    public bool IsHoldingGrab => state != null && state.IsHolding;
    public bool IsHeldByGrab => state != null && state.IsHeld;

    private bool IsDirectP2pActive => DirectP2p != null && DirectP2p.IsReady;

    private bool TryBeginDirectP2pAction(P2pCombatActionKind actionKind, Vector3 direction)
    {
        P2pCombatReplicator directP2p = DirectP2p;
        if (directP2p == null || !directP2p.IsReady)
            return false;

        return directP2p.TryBeginLocalAction(
            actionKind,
            direction,
            GetP2pInteractionDelay(actionKind),
            GetP2pActionLifetime(actionKind));
    }

    public bool TryFindP2pInteractionTarget(P2pCombatActionKind actionKind, Vector3 direction, out CharacterState target)
    {
        float range;
        float radius;
        switch (actionKind)
        {
            case P2pCombatActionKind.Punch:
                if (!Config.TryGetAction(CombatActionId.BasicPunch, out CombatActionDefinition punch))
                {
                    target = null;
                    return false;
                }
                range = punch.range + 0.08f;
                radius = punch.radius;
                break;

            case P2pCombatActionKind.CrossPunch:
                if (!Config.TryGetAction(CombatActionId.CrossPunch, out CombatActionDefinition crossPunch))
                {
                    target = null;
                    return false;
                }
                range = crossPunch.range + 0.10f;
                radius = crossPunch.radius;
                break;

            case P2pCombatActionKind.SlideTackle:
                range = 0f;
                radius = Config.Tackle.hitRadius + 0.12f;
                break;

            default:
                range = Config.Grab.range + 0.05f;
                radius = Config.Grab.radius;
                break;
        }

        Vector3 center = transform.position + CharacterMovementUtility.FlattenOrFallback(direction, transform.forward) * range;
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer);
        target = null;
        float nearestDistanceSq = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            CharacterState candidate = overlapBuffer[i].GetComponentInParent<CharacterState>();
            NetworkPlayerAgent candidateAgent = candidate != null ? candidate.GetComponent<NetworkPlayerAgent>() : null;
            if (candidate == null || candidate == state || candidateAgent == null || candidateAgent.IsOwner || candidateAgent.IsAIControlled)
                continue;

            float distanceSq = (candidate.transform.position - center).sqrMagnitude;
            if (distanceSq < nearestDistanceSq)
            {
                target = candidate;
                nearestDistanceSq = distanceSq;
            }
        }

        return target != null;
    }

    public P2pCombatResolution ResolveP2pInteraction(P2pCombatActionKind actionKind, Vector3 attackerOrigin)
    {
        bool blocked = defense != null && (actionKind == P2pCombatActionKind.SlideTackle
            ? defense.TryBlockTackle(attackerOrigin)
            : defense.TryBlockAttack(attackerOrigin));
        if (blocked)
            return P2pCombatResolution.Block;

        if (state == null || state.IsInvulnerable)
        {
            state?.NotifyEvaded();
            return P2pCombatResolution.Evade;
        }

        if (actionKind == P2pCombatActionKind.Grab)
        {
            GrabController attackerGrab = FindRemoteHumanCombatant()?.GetComponent<GrabController>();
            return attackerGrab != null && attackerGrab.BeginP2pSession(state, Config.Grab)
                ? P2pCombatResolution.Hit
                : P2pCombatResolution.Evade;
        }

        GetP2pHitValues(actionKind, out float knockbackForce, out float stunDuration, out bool releaseBall, out float ballKnockbackForce);
        ApplyP2pHit(attackerOrigin, knockbackForce, stunDuration, releaseBall, ballKnockbackForce);
        return P2pCombatResolution.Hit;
    }

    public void PlayP2pRemoteActionPresentation(P2pCombatActionKind actionKind)
    {
        if (actionKind != P2pCombatActionKind.Grab)
            PlayActionPresentationLocal(ToCombatActionKind(actionKind));
    }

    public void PlayP2pPresentation(P2pPresentationAction action, P2pPresentationProfile profile, Vector3 attackerOrigin)
    {
        if (action == P2pPresentationAction.Block)
        {
            DefenseBlockDirection blockDirection = DefenseWindow.ResolveDirection(
                transform.position,
                transform.forward,
                attackerOrigin);
            anim?.PlayP2pPresentation(action, profile.ClipStartOffset, blockDirection);
            return;
        }

        if (action == P2pPresentationAction.Tackle && slideDustPrefab != null)
            Instantiate(slideDustPrefab, transform.position + Vector3.down, Quaternion.identity, transform);

        anim?.PlayP2pPresentation(action, profile.ClipStartOffset, DefenseBlockDirection.Right);
    }

    public void CancelP2pPresentation(P2pPresentationCancelStyle cancelStyle)
    {
        anim?.CancelP2pPresentation(cancelStyle);
    }

    public void PlayP2pResultPresentation(P2pCombatActionKind actionKind, P2pCombatResolution resolution, Vector3 attackerOrigin, Vector3 actionDirection)
    {
        if (resolution == P2pCombatResolution.Block)
            return;

        CharacterState remoteTarget = FindRemoteHumanCombatant();
        if (remoteTarget == null)
            return;

        if (resolution != P2pCombatResolution.Hit || actionKind == P2pCombatActionKind.Grab)
            return;

        PowerGaugeGainSource source = actionKind == P2pCombatActionKind.Punch
            ? PowerGaugeGainSource.BasicPunchHit
            : actionKind == P2pCombatActionKind.CrossPunch
                ? PowerGaugeGainSource.CrossPunchHit
                : PowerGaugeGainSource.SlideTackleHit;
        GetComponent<PowerGauge>()?.TryAdd(source);

        Vector3 hitDirection = remoteTarget.transform.position - attackerOrigin;
        hitDirection.y = 0f;
        hitDirection = CharacterMovementUtility.FlattenOrFallback(hitDirection, actionDirection);
        Vector3 hitPosition = remoteTarget.transform.position + Vector3.up - hitDirection * 0.3f;
        PlayHitPresentationLocal(hitPosition, hitDirection, involvesLocalPlayer: true);
    }

    public void BeginP2pGrabWithRemote()
    {
        CharacterState remoteTarget = FindRemoteHumanCombatant();
        if (remoteTarget != null)
            grab?.BeginP2pSession(remoteTarget, Config.Grab);
    }

    public void ReleaseP2pGrabWithRemote()
    {
        GrabController remoteGrab = FindRemoteHumanCombatant()?.GetComponent<GrabController>();
        remoteGrab?.Release();
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

    private CombatHitResolution Hit(
        CharacterState victim,
        float knockbackForce,
        float stunDuration,
        bool releaseBallOnHit,
        float ballKnockbackForce,
        CombatHitKind hitKind,
        PowerGaugeGainSource gainSource)
    {
        DefenseController defense = victim.GetComponent<DefenseController>();
        if (defense != null)
        {
            bool blocked = hitKind == CombatHitKind.Tackle
                ? defense.TryBlockTackle(transform.position)
                : defense.TryBlockAttack(transform.position);
            if (blocked)
                return CombatHitResolution.Blocked;
        }

        if (victim.IsInvulnerable)
        {
            victim.NotifyEvaded();
            return CombatHitResolution.Evaded;
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
        if (releaseBallOnHit && victimBall != null && victimBall.HasBall)
        {
            Vector3 ballImpulse = dir * ballKnockbackForce + Vector3.up * (ballKnockbackForce * 0.3f);
            victimBall.ForceRelease(ballImpulse);
        }

        victim.ApplyHit(dir * knockbackForce, stunDuration);
        GetComponent<PowerGauge>()?.TryAdd(gainSource);
        return CombatHitResolution.Applied;
    }

    private float GetP2pInteractionDelay(P2pCombatActionKind actionKind)
    {
        switch (actionKind)
        {
            case P2pCombatActionKind.Punch: return 0.30f;
            case P2pCombatActionKind.CrossPunch: return 0.60f;
            case P2pCombatActionKind.SlideTackle:
            case P2pCombatActionKind.Grab: return 0.10f;
            default: return 0f;
        }
    }

    private float GetP2pActionLifetime(P2pCombatActionKind actionKind)
    {
        return actionKind == P2pCombatActionKind.SlideTackle
            ? Mathf.Max(0.5f, Config.Tackle.activeTime + 0.25f)
            : Mathf.Max(0.8f, GetP2pInteractionDelay(actionKind) + 0.25f);
    }

    private void GetP2pHitValues(
        P2pCombatActionKind actionKind,
        out float knockbackForce,
        out float stunDuration,
        out bool releaseBall,
        out float ballKnockbackForce)
    {
        if (actionKind == P2pCombatActionKind.SlideTackle)
        {
            CombatConfig.TackleSettings tackle = Config.Tackle;
            knockbackForce = tackle.knockbackForce;
            stunDuration = tackle.hitStunTime;
            releaseBall = true;
            ballKnockbackForce = tackle.ballKnockForce;
            return;
        }

        CombatActionId actionId = actionKind == P2pCombatActionKind.CrossPunch
            ? CombatActionId.CrossPunch
            : CombatActionId.BasicPunch;
        if (!Config.TryGetAction(actionId, out CombatActionDefinition action))
        {
            knockbackForce = 0f;
            stunDuration = 0f;
            releaseBall = false;
            ballKnockbackForce = 0f;
            return;
        }

        knockbackForce = action.knockbackForce;
        stunDuration = action.hitStunTime;
        releaseBall = action.releaseBallOnHit;
        ballKnockbackForce = action.ballKnockbackForce;
    }

    private void ApplyP2pHit(
        Vector3 attackerOrigin,
        float knockbackForce,
        float stunDuration,
        bool releaseBallOnHit,
        float ballKnockbackForce)
    {
        Vector3 direction = transform.position - attackerOrigin;
        direction.y = 0f;
        direction = CharacterMovementUtility.FlattenOrFallback(direction, transform.forward);
        Vector3 hitPosition = transform.position + Vector3.up - direction * 0.3f;
        PlayHitPresentationLocal(hitPosition, direction, involvesLocalPlayer: true);

        PlayerBallHandler victimBall = GetComponent<PlayerBallHandler>();
        if (releaseBallOnHit && victimBall != null && victimBall.HasBall)
        {
            Vector3 ballImpulse = direction * ballKnockbackForce + Vector3.up * (ballKnockbackForce * 0.3f);
            victimBall.ForceRelease(ballImpulse);
        }

        state.ApplyDirectP2pHit(direction * knockbackForce, stunDuration);
    }

    private CharacterState FindRemoteHumanCombatant()
    {
        NetworkPlayerAgent[] agents = FindObjectsOfType<NetworkPlayerAgent>();
        foreach (NetworkPlayerAgent candidate in agents)
        {
            if (candidate == null || candidate == netAgent || candidate.IsOwner || candidate.IsAIControlled)
                continue;

            return candidate.GetComponent<CharacterState>();
        }

        return null;
    }

    private static CombatActionKind ToCombatActionKind(P2pCombatActionKind actionKind)
    {
        switch (actionKind)
        {
            case P2pCombatActionKind.Punch: return CombatActionKind.Punch;
            case P2pCombatActionKind.CrossPunch: return CombatActionKind.CrossPunch;
            default: return CombatActionKind.SlideTackle;
        }
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
