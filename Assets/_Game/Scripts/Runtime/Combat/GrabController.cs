using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterState))]
public sealed class GrabController : MonoBehaviour
{
    private static readonly Collider[] OverlapBuffer = new Collider[16];

    private CharacterState state;
    private CharacterLocomotion locomotion;
    private CharacterAnimator characterAnimator;
    private CharacterState heldTarget;
    private float releaseAt = float.NegativeInfinity;

    public bool IsActive => heldTarget != null;

    private void Awake()
    {
        state = GetComponent<CharacterState>();
        locomotion = GetComponent<CharacterLocomotion>();
        characterAnimator = GetComponent<CharacterAnimator>();
    }

    private void Update()
    {
        if (IsActive && Time.time >= releaseAt)
            Release();
    }

    private void OnDisable()
    {
        Release();
    }

    public bool TryStart(CombatConfig.GrabSettings settings, Vector3 actionDirection)
    {
        if (state == null || state.IsStunned || state.IsGrabRestricted || locomotion != null && locomotion.IsDodging)
            return false;

        CharacterState target = FindNearestTarget(settings, actionDirection);
        if (target == null)
            return false;

        DefenseController defense = target.GetComponent<DefenseController>();
        if (defense != null && defense.TryBlockAttack(transform.position))
            return false;

        return BeginP2pSession(target, settings);
    }

    public bool TryCancel()
    {
        if (state == null || !state.CanUseGrabAction(GameplayInputAction.Grab, Time.time))
            return false;

        Release();
        return true;
    }

    public bool Release()
    {
        if (!IsActive)
            return false;

        CharacterState target = heldTarget;
        heldTarget = null;
        releaseAt = float.NegativeInfinity;
        state?.ClearGrabState();
        target?.ClearGrabState(this);
        characterAnimator?.PlayGrabRelease();
        return true;
    }

    /// <summary>Starts an already-resolved direct-P2P grab without another overlap or defense check.</summary>
    public bool BeginP2pSession(CharacterState target, CombatConfig.GrabSettings settings)
    {
        if (target == null || state == null || IsActive || state.IsStunned || state.IsGrabRestricted || target.IsGrabRestricted)
            return false;

        state.BeginHolding(Time.time, settings.cancelDelay, settings.holderMovementMultiplier);
        target.BeginHeld(this);
        heldTarget = target;
        releaseAt = Time.time + settings.duration;
        heldTarget.GetComponent<CharacterLocomotion>()?.StopForControlRestriction();
        characterAnimator?.PlayGrabStart();
        return true;
    }

    private CharacterState FindNearestTarget(CombatConfig.GrabSettings settings, Vector3 actionDirection)
    {
        Vector3 direction = CharacterMovementUtility.NormalizePlanar(actionDirection);
        if (direction.sqrMagnitude < 0.0001f)
            direction = CharacterMovementUtility.FlattenOrFallback(transform.forward, Vector3.forward);

        Vector3 center = transform.position + direction * settings.range;
        int count = Physics.OverlapSphereNonAlloc(center, settings.radius, OverlapBuffer);
        CharacterState nearest = null;
        float nearestDistanceSq = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            CharacterState candidate = OverlapBuffer[i].GetComponentInParent<CharacterState>();
            if (candidate == null || candidate == state || candidate.IsInvulnerable || candidate.IsGrabRestricted)
                continue;

            float distanceSq = (candidate.transform.position - center).sqrMagnitude;
            if (distanceSq < nearestDistanceSq)
            {
                nearest = candidate;
                nearestDistanceSq = distanceSq;
            }
        }

        return nearest;
    }
}
