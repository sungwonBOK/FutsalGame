using UnityEngine;

public static class CameraLookOffsetResolver
{
    public static Vector3 Resolve(float pitch, float maxPitch, float maxVerticalOffset)
    {
        float safeMaxPitch = Mathf.Max(0.0001f, Mathf.Abs(maxPitch));
        float normalizedPitch = Mathf.Clamp(pitch / safeMaxPitch, -1f, 1f);
        return Vector3.up * (normalizedPitch * Mathf.Max(0f, maxVerticalOffset));
    }
}
