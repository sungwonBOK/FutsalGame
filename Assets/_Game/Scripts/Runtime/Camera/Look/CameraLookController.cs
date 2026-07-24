using UnityEngine;

public sealed class CameraLookController
{
    private float yaw;
    private float pitch;

    public void Initialize(float initialYaw, float initialPitch)
    {
        yaw = initialYaw;
        pitch = initialPitch;
    }

    public CameraLookState Update(
        Vector2 mouseDelta,
        float yawSensitivity,
        float pitchSensitivity,
        bool invertY,
        float minPitch,
        float maxPitch)
    {
        yaw += mouseDelta.x * Mathf.Max(0f, yawSensitivity);

        float pitchDirection = invertY ? -1f : 1f;
        pitch += mouseDelta.y * Mathf.Max(0f, pitchSensitivity) * pitchDirection;
        pitch = Mathf.Clamp(pitch, Mathf.Min(minPitch, maxPitch), Mathf.Max(minPitch, maxPitch));

        return new CameraLookState(yaw, pitch);
    }
}
