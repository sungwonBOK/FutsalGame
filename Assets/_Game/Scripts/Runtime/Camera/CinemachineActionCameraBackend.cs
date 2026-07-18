using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CinemachineActionCameraBackend : MonoBehaviour
{
    [SerializeField] private Transform followRigTarget;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    public Transform FollowRigTarget
    {
        get => followRigTarget;
        set => followRigTarget = value;
    }

    public CinemachineCamera CinemachineCamera
    {
        get => cinemachineCamera;
        set => cinemachineCamera = value;
    }

    public CinemachineImpulseSource ImpulseSource
    {
        get => impulseSource;
        set => impulseSource = value;
    }

    public bool HasFollowRigTarget => followRigTarget != null;

    public void ApplyRigPose(ThirdPersonActionCamera.CameraRigPose pose)
    {
        if (followRigTarget == null)
            return;

        followRigTarget.SetPositionAndRotation(pose.Position, pose.Rotation);
    }

    public void ApplyFieldOfView(float fieldOfView)
    {
        if (cinemachineCamera == null)
            return;

        LensSettings lens = cinemachineCamera.Lens;
        lens.FieldOfView = fieldOfView;
        cinemachineCamera.Lens = lens;
    }

    public void PlayImpulse(float strength)
    {
        if (impulseSource == null)
            return;

        impulseSource.GenerateImpulseWithForce(Mathf.Clamp01(strength));
    }
}
