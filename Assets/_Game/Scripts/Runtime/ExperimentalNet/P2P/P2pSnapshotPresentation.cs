using UnityEngine;

public static class P2pSnapshotPresentation
{
    public static void Step(
        Vector3 currentPosition,
        float currentYawDegrees,
        P2pPlayerSnapshot target,
        float interpolationFactor,
        out Vector3 position,
        out float yawDegrees)
    {
        float t = Mathf.Clamp01(interpolationFactor);
        position = Vector3.Lerp(currentPosition, target.Position, t);
        yawDegrees = Mathf.LerpAngle(currentYawDegrees, target.YawDegrees, t);
    }
}
