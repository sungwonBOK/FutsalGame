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
    public CameraPlan(CameraRigPose cameraPose, CameraRigPose followRigPose, float fieldOfView)
    {
        CameraPose = cameraPose;
        FollowRigPose = followRigPose;
        FieldOfView = fieldOfView;
    }

    public CameraRigPose CameraPose { get; }
    public CameraRigPose FollowRigPose { get; }
    public float FieldOfView { get; }
}

public static class CameraPlanBuilder
{
    public static CameraPlan Build(CameraRigPose cameraPose, CameraRigPose followRigPose, float fieldOfView)
    {
        return new CameraPlan(cameraPose, followRigPose, fieldOfView);
    }
}
