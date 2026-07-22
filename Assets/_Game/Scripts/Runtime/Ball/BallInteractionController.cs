using UnityEngine;

public sealed class BallInteractionController
{
    private readonly BallPossessionController possession;
    private readonly BallConfig config;

    private bool sprintHeld;
    private Vector3 sprintActionDirection = Vector3.forward;
    private float sprintTouchStartedAt = -1f;

    private bool isCharging;
    private float chargeStartedAt;
    private Vector3 chargeDirection = Vector3.forward;

    public bool IsCharging => isCharging;

    public BallInteractionController(BallPossessionController possession, BallConfig config)
    {
        this.possession = possession;
        this.config = config;
    }

    public float ChargeAmount01(float now)
    {
        if (!isCharging)
            return 0f;

        return Mathf.Clamp01((now - chargeStartedAt) / Mathf.Max(0.0001f, config.Shot.maxChargeTime));
    }

    public void SetSprintInput(bool held, Vector3 actionDirection)
    {
        sprintHeld = held;
        sprintActionDirection = actionDirection;

        if (!sprintHeld)
            sprintTouchStartedAt = -1f;
    }

    public bool TryTick(float now, bool canInteract, Vector3 fallbackForward, out Vector3 sprintTouchImpulse)
    {
        sprintTouchImpulse = Vector3.zero;

        if (!canInteract || !possession.HasBall)
        {
            CancelAll();
            return false;
        }

        if (isCharging)
        {
            sprintTouchStartedAt = -1f;
            return false;
        }

        if (!sprintHeld)
        {
            sprintTouchStartedAt = -1f;
            return false;
        }

        if (sprintTouchStartedAt < 0f)
        {
            sprintTouchStartedAt = now;
            return false;
        }

        if (now - sprintTouchStartedAt < config.Dribble.sprintTouchInterval)
            return false;

        sprintTouchStartedAt = -1f;
        sprintTouchImpulse = CaptureDirection(sprintActionDirection, fallbackForward) * config.Dribble.sprintTouchForce;
        return possession.Release(now, sprintTouchImpulse);
    }

    public bool TryPass(float now, Vector3 actionDirection, Vector3 fallbackForward, out Vector3 impulse)
    {
        CancelAll();
        impulse = CaptureDirection(actionDirection, fallbackForward) * config.Pass.force;
        return possession.Release(now, impulse);
    }

    public bool StartCharge(float now, Vector3 actionDirection, Vector3 fallbackForward)
    {
        if (!possession.HasBall || isCharging)
            return false;

        sprintTouchStartedAt = -1f;
        isCharging = true;
        chargeStartedAt = now;
        chargeDirection = CaptureDirection(actionDirection, fallbackForward);
        return true;
    }

    public bool TryReleaseCharge(float now, Vector3 fallbackForward, out Vector3 impulse)
    {
        impulse = Vector3.zero;
        if (!isCharging)
            return false;

        float chargeAmount = ChargeAmount01(now);
        Vector3 direction = CaptureDirection(chargeDirection, fallbackForward);
        isCharging = false;

        if (!possession.HasBall)
            return false;

        impulse = direction * Mathf.Lerp(config.Shot.minChargeForce, config.Shot.maxShootForce, chargeAmount);
        return possession.Release(now, impulse);
    }

    public void CancelAll()
    {
        sprintHeld = false;
        sprintTouchStartedAt = -1f;
        isCharging = false;
    }

    public void CancelCharge()
    {
        isCharging = false;
    }

    public static Vector3 CaptureDirection(Vector3 actionDirection, Vector3 fallbackForward)
    {
        Vector3 captured = CharacterMovementUtility.NormalizePlanar(actionDirection);
        if (captured.sqrMagnitude > 0.0001f)
            return captured;

        captured = CharacterMovementUtility.NormalizePlanar(fallbackForward);
        return captured.sqrMagnitude > 0.0001f ? captured : Vector3.forward;
    }
}
