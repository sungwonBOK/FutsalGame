using UnityEngine;

public sealed class PossessionCameraMode
{
    public CameraModeResult Resolve(CameraContext context, ThirdPersonActionCameraSettings settings, CameraLookState look)
    {
        CameraFramingProfile framing = CameraFramingProfile.FromPossession(settings);
        Vector3 forward = Quaternion.Euler(0f, look.Yaw, 0f) * Vector3.forward;
        Vector3 lookPoint = context.PlayerPosition
            + Vector3.up * framing.LookAtHeight
            + forward * framing.LookForwardOffset;
        return new CameraModeResult(CameraBaseMode.Possession, lookPoint, framing);
    }
}
