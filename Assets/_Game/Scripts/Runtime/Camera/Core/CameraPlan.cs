using UnityEngine;

public readonly struct CameraRigPose
{
    public CameraRigPose(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
}

public readonly struct CameraPlan
{
    public CameraPlan(
        CameraRigPose cameraPose,
        CameraRigPose followRigPose,
        float fieldOfView,
        CameraFramingProfile framing,
        Vector3 aimTargetOffset)
    {
        CameraPose = cameraPose;
        FollowRigPose = followRigPose;
        FieldOfView = fieldOfView;
        Framing = framing;
        AimTargetOffset = aimTargetOffset;
    }

    public CameraRigPose CameraPose { get; }
    public CameraRigPose FollowRigPose { get; }
    public float FieldOfView { get; }
    public CameraFramingProfile Framing { get; }
    public Vector3 AimTargetOffset { get; }
}

public static class CameraPlanBuilder
{
    public static CameraPlan Build(
        CameraRigPose cameraPose,
        CameraRigPose followRigPose,
        float fieldOfView,
        CameraFramingProfile framing,
        Vector3 aimTargetOffset)
    {
        return new CameraPlan(cameraPose, followRigPose, fieldOfView, framing, aimTargetOffset);
    }
}
