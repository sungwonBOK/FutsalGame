public sealed class CameraDirector
{
    private readonly ThirdPersonCameraMode defaultMode = new ThirdPersonCameraMode();
    private readonly PossessionCameraMode possessionMode = new PossessionCameraMode();

    public CameraModeResult Resolve(CameraContext context, ThirdPersonActionCameraSettings settings, CameraLookState look)
    {
        return context.IsTargetBallOwner
            ? possessionMode.Resolve(context, settings, look)
            : defaultMode.Resolve(context, settings, look);
    }
}
