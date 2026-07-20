using UnityEngine;

public sealed class FovResolver
{
    private float fovVelocity;

    public float Resolve(float currentFov, CameraContext context, ThirdPersonActionCameraSettings settings)
    {
        float speed = new Vector3(context.Velocity.x, 0f, context.Velocity.z).magnitude;
        float targetFov = CalculateTargetFov(settings.baseFov, speed, settings.sprintSpeed, settings.sprintFovBoost);
        return Mathf.SmoothDamp(currentFov, targetFov, ref fovVelocity, settings.fovSmoothTime, Mathf.Infinity, context.DeltaTime);
    }

    public static float CalculateTargetFov(float baseFov, float speed, float sprintSpeed, float sprintFovBoost)
    {
        float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.0001f, sprintSpeed));
        return baseFov + Mathf.Clamp(sprintFovBoost, 0f, 5f) * speed01;
    }
}
