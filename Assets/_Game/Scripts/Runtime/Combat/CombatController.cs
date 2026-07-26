using System.Collections.Generic;
using UnityEngine;

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
        if (anim != null)
            anim.PlayPunch();

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
        if (anim != null)
            anim.PlayCrossPunch();

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
        if (anim != null)
            anim.PlaySlide();

        slideActiveUntil = Time.time + tackle.activeTime;
        hitThisSlide.Clear();
        motor.Dash(lockedDirection * Config.TackleVelocity, tackle.activeTime);

        if (slideDustPrefab != null)
            Instantiate(slideDustPrefab, transform.position + Vector3.down, Quaternion.identity, transform);
    }

    private void FixedUpdate()
    {
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

        if (hitEffectPrefab != null)
        {
            Vector3 hitPos = victim.transform.position + Vector3.up * 1f - dir * 0.3f;
            Instantiate(hitEffectPrefab, hitPos, Quaternion.LookRotation(-dir));
        }
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayHit();
        if (actionCamera != null)
            actionCamera.PlayHitShake();

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
}
