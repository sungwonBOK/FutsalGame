public sealed class CameraDirector
{
    private readonly ThirdPersonCameraMode defaultMode = new ThirdPersonCameraMode();

    public CameraModeResult Resolve(CameraContext context, ThirdPersonActionCameraSettings settings)
    {
        return defaultMode.Resolve(context, settings);
    }
}
