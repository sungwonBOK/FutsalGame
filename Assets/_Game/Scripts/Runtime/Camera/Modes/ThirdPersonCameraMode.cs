using UnityEngine;

public sealed class ThirdPersonCameraMode
{
    public CameraModeResult Resolve(CameraContext context, ThirdPersonActionCameraSettings settings, CameraLookState look)
    {
        CameraFramingProfile framing = CameraFramingProfile.FromThirdPerson(settings);
        Vector3 lookPoint = context.PlayerPosition + Vector3.up * framing.LookAtHeight;
        return new CameraModeResult(CameraBaseMode.ThirdPerson, lookPoint, framing);
    }
}
