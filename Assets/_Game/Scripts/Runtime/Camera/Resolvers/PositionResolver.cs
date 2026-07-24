using UnityEngine;

public readonly struct CameraPositionResult
{
    public CameraPositionResult(CameraRigPose cameraPose, CameraRigPose followRigPose)
    {
        CameraPose = cameraPose;
        FollowRigPose = followRigPose;
    }

    public CameraRigPose CameraPose { get; }
    public CameraRigPose FollowRigPose { get; }
}

public sealed class PositionResolver
{
    private Vector3 positionVelocity;
    private float currentDistance;
    private float distanceVelocity;

    public void Initialize(float initialDistance)
    {
        currentDistance = initialDistance;
    }

    public CameraPositionResult Resolve(
        CameraModeResult mode,
        CameraLookState look,
        Vector3 aimTargetOffset,
        CameraContext context,
        ThirdPersonActionCameraSettings settings,
        bool useCinemachineBackend)
    {
        CameraRigPose followRigPose = BuildFollowRigPose(context.PlayerPosition, look, mode.Framing.LookAtHeight);
        if (useCinemachineBackend)
            return new CameraPositionResult(default, followRigPose);

        CameraLookState orbitLook = new CameraLookState(look.Yaw, 0f);
        Vector3 aimPoint = mode.LookPoint + aimTargetOffset;
        float desiredDistance = ResolveCollisionDistance(mode.LookPoint, orbitLook, mode.Framing, settings);
        float distanceSmoothTime = desiredDistance < currentDistance
            ? settings.collisionMoveInSmoothTime
            : settings.collisionReturnSmoothTime;
        currentDistance = Mathf.SmoothDamp(
            currentDistance,
            desiredDistance,
            ref distanceVelocity,
            distanceSmoothTime,
            Mathf.Infinity,
            context.DeltaTime);
        Vector3 desiredPosition = BuildCameraPosition(mode.LookPoint, orbitLook, currentDistance, mode.Framing.Height);
        desiredPosition = Vector3.SmoothDamp(
            context.CurrentCameraPosition,
            desiredPosition,
            ref positionVelocity,
            settings.positionSmoothTime,
            Mathf.Infinity,
            context.DeltaTime);
        return new CameraPositionResult(
            new CameraRigPose(desiredPosition, BuildStableLookRotation(desiredPosition, aimPoint)),
            followRigPose);
    }

    public static CameraRigPose BuildFollowRigPose(Vector3 playerPosition, CameraLookState look, float lookAtHeight)
    {
        Vector3 lookPoint = playerPosition + Vector3.up * lookAtHeight;
        return new CameraRigPose(lookPoint, Quaternion.Euler(0f, look.Yaw, 0f));
    }

    public static Quaternion BuildStableLookRotation(Vector3 cameraPosition, Vector3 lookPoint)
    {
        Vector3 lookDirection = lookPoint - cameraPosition;
        if (lookDirection.sqrMagnitude < 0.0001f)
            lookDirection = Vector3.forward;

        Vector3 euler = Quaternion.LookRotation(lookDirection.normalized, Vector3.up).eulerAngles;
        euler.z = 0f;
        return Quaternion.Euler(euler);
    }

    private static Vector3 BuildCameraPosition(Vector3 lookPoint, CameraLookState look, float distance, float height)
    {
        Vector3 forward = Quaternion.Euler(look.Pitch, look.Yaw, 0f) * Vector3.forward;
        return lookPoint - forward * distance + Vector3.up * height;
    }

    private static float ResolveCollisionDistance(Vector3 lookPoint, CameraLookState look, CameraFramingProfile framing, ThirdPersonActionCameraSettings settings)
    {
        Vector3 desiredPosition = BuildCameraPosition(lookPoint, look, framing.Distance, framing.Height);
        Vector3 toCamera = desiredPosition - lookPoint;
        float desiredDistance = toCamera.magnitude;
        if (desiredDistance <= 0.0001f)
            return settings.minCollisionDistance;

        Vector3 direction = toCamera / desiredDistance;
        if (Physics.SphereCast(lookPoint, settings.collisionRadius, direction, out RaycastHit hit, desiredDistance, settings.collisionMask, QueryTriggerInteraction.Ignore))
            return Mathf.Max(settings.minCollisionDistance, hit.distance - settings.collisionRadius);

        return framing.Distance;
    }
}
