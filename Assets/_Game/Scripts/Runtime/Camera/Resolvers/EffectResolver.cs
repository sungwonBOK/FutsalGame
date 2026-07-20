using UnityEngine;

public sealed class EffectResolver
{
    private float shakeAmount;
    private float shakeTimeRemaining;

    public void AddShake(float strength, ThirdPersonActionCameraSettings settings)
    {
        shakeAmount = Mathf.Clamp01(Mathf.Max(shakeAmount, strength * settings.shakeStrength));
        shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, 0.12f);
    }

    public CameraRigPose Resolve(CameraRigPose pose, CameraContext context, ThirdPersonActionCameraSettings settings, bool applyShake)
    {
        if (!applyShake || shakeTimeRemaining <= 0f || shakeAmount <= 0f)
            return pose;

        shakeTimeRemaining -= context.DeltaTime;
        float seed = Time.time * settings.shakeFrequency;
        float offsetX = (Mathf.PerlinNoise(seed, 0.17f) - 0.5f) * 2f;
        float offsetY = (Mathf.PerlinNoise(0.83f, seed) - 0.5f) * 2f;
        float yawKick = (Mathf.PerlinNoise(seed, seed) - 0.5f) * 2f;
        Vector3 position = pose.Position
            + context.CameraRight * (offsetX * settings.maxShakeOffset * shakeAmount)
            + Vector3.up * (offsetY * settings.maxShakeOffset * shakeAmount);
        Quaternion rotation = Quaternion.AngleAxis(yawKick * settings.maxShakeAngle * shakeAmount, Vector3.up) * pose.Rotation;
        shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, settings.shakeDecay * context.DeltaTime);
        if (shakeTimeRemaining <= 0f)
            shakeAmount = 0f;
        return new CameraRigPose(position, rotation);
    }
}
