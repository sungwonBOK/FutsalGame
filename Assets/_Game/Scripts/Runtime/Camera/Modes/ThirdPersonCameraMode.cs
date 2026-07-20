using UnityEngine;

public readonly struct CameraModeResult
{
    public CameraModeResult(float desiredYaw, Vector3 lookPoint, bool ballHintRequired)
    {
        DesiredYaw = desiredYaw;
        LookPoint = lookPoint;
        BallHintRequired = ballHintRequired;
    }

    public float DesiredYaw { get; }
    public Vector3 LookPoint { get; }
    public bool BallHintRequired { get; }
}

public sealed class ThirdPersonCameraMode
{
    public CameraModeResult Resolve(CameraContext context, ThirdPersonActionCameraSettings settings)
    {
        Vector3 heading = AimResolver.SelectHeading(
            context.HasMoveIntent,
            context.MoveIntent,
            context.ActionIntent,
            context.Velocity,
            context.TargetForward,
            context.CurrentYaw,
            settings.movementPrioritySpeed);
        float intentYaw = AimResolver.DirectionToYaw(heading);
        float desiredYaw = AimResolver.ApplyBallAssistYaw(
            intentYaw,
            context.PlayerPosition,
            context.BallPosition,
            settings.ballAssistEdgeAngle,
            settings.ballAssistMaxAngle,
            settings.ballAssistStrength,
            context.HasMoveIntent,
            intentYaw,
            settings.ballAssistActiveInputMaxAngle);
        bool ballHintRequired = context.HasBallTarget && AimResolver.BallNeedsHint(
            desiredYaw,
            context.PlayerPosition,
            context.BallPosition,
            settings.ballAssistMaxAngle);
        Vector3 lookPoint = context.PlayerPosition + Vector3.up * settings.lookAtHeight;
        return new CameraModeResult(desiredYaw, lookPoint, ballHintRequired);
    }
}
