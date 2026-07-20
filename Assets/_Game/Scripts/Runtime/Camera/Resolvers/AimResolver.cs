using UnityEngine;

public sealed class AimResolver
{
    public float UpdateYaw(
        float currentYaw,
        float desiredYaw,
        ref float yawVelocity,
        float deltaTime,
        ThirdPersonActionCameraSettings settings)
    {
        return UpdateYaw(
            currentYaw,
            desiredYaw,
            ref yawVelocity,
            deltaTime,
            settings.rotationDeadZone,
            settings.rotationSmoothTime,
            settings.maxRotationSpeed,
            settings.quickTurnAngle,
            settings.quickTurnSmoothTime,
            settings.quickTurnMaxRotationSpeed);
    }

    public static float UpdateYaw(
        float currentYaw,
        float desiredYaw,
        ref float yawVelocity,
        float deltaTime,
        float deadZone,
        float smoothTime,
        float maxRotationSpeed,
        float quickTurnAngle,
        float quickTurnSmoothTime,
        float quickTurnMaxRotationSpeed)
    {
        float delta = Mathf.DeltaAngle(currentYaw, desiredYaw);
        float absDelta = Mathf.Abs(delta);
        if (absDelta <= deadZone)
        {
            yawVelocity = 0f;
            return currentYaw;
        }

        float adjustedTarget = currentYaw + Mathf.Sign(delta) * (absDelta - deadZone);
        if (absDelta >= quickTurnAngle && absDelta < 135f)
        {
            maxRotationSpeed = Mathf.Max(maxRotationSpeed, quickTurnMaxRotationSpeed);
            yawVelocity = 0f;
            return Mathf.MoveTowardsAngle(currentYaw, adjustedTarget, maxRotationSpeed * deltaTime);
        }

        float smoothed = Mathf.SmoothDampAngle(
            currentYaw,
            adjustedTarget,
            ref yawVelocity,
            Mathf.Max(0.0001f, smoothTime),
            Mathf.Max(1f, maxRotationSpeed),
            Mathf.Max(0.0001f, deltaTime));
        return Mathf.MoveTowardsAngle(currentYaw, smoothed, maxRotationSpeed * deltaTime);
    }

    public static float ApplyBallAssistYaw(
        float currentYaw,
        Vector3 playerPosition,
        Vector3 ballPosition,
        float edgeAngle,
        float maxAssistAngle,
        float strength,
        bool hasActiveMoveInput,
        float activeMoveYaw,
        float maxActiveInputAssistAngle)
    {
        Vector3 toBall = ballPosition - playerPosition;
        toBall.y = 0f;
        if (toBall.sqrMagnitude < 0.0001f || strength <= 0f || !hasActiveMoveInput)
            return currentYaw;

        float ballYaw = DirectionToYaw(toBall);
        float delta = Mathf.DeltaAngle(currentYaw, ballYaw);
        float absDelta = Mathf.Abs(delta);
        if (absDelta <= edgeAngle || absDelta >= maxAssistAngle)
            return currentYaw;

        float edge01 = Mathf.InverseLerp(edgeAngle, maxAssistAngle, absDelta);
        float assistedYaw = Mathf.LerpAngle(currentYaw, ballYaw, Mathf.Clamp01(strength) * edge01);
        float assistOffset = Mathf.DeltaAngle(activeMoveYaw, assistedYaw);
        float maxOffset = Mathf.Max(0f, maxActiveInputAssistAngle);
        return Mathf.Abs(assistOffset) <= maxOffset
            ? assistedYaw
            : activeMoveYaw + Mathf.Sign(assistOffset) * maxOffset;
    }

    public static Vector3 SelectHeading(
        bool hasMoveIntent,
        Vector3 moveIntent,
        Vector3 actionIntent,
        Vector3 velocity,
        Vector3 targetForward,
        float fallbackYaw,
        float movementPrioritySpeed)
    {
        Vector3 flatMoveIntent = new Vector3(moveIntent.x, 0f, moveIntent.z);
        if (hasMoveIntent && flatMoveIntent.sqrMagnitude > 0.0001f)
            return flatMoveIntent.normalized;

        Vector3 flatActionIntent = new Vector3(actionIntent.x, 0f, actionIntent.z);
        if (flatActionIntent.sqrMagnitude > 0.0001f)
            return flatActionIntent.normalized;

        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
        if (flatVelocity.magnitude >= movementPrioritySpeed)
            return flatVelocity.normalized;

        Vector3 flatForward = new Vector3(targetForward.x, 0f, targetForward.z);
        if (flatForward.sqrMagnitude > 0.0001f)
            return flatForward.normalized;

        return Quaternion.Euler(0f, fallbackYaw, 0f) * Vector3.forward;
    }

    public static float DirectionToYaw(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    public static bool BallNeedsHint(float yaw, Vector3 playerPosition, Vector3 ballPosition, float maxAssistAngle)
    {
        Vector3 toBall = ballPosition - playerPosition;
        toBall.y = 0f;
        return toBall.sqrMagnitude >= 0.0001f
            && Mathf.Abs(Mathf.DeltaAngle(yaw, DirectionToYaw(toBall))) >= maxAssistAngle;
    }
}
