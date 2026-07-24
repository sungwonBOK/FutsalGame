using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CinemachineActionCameraBackend : MonoBehaviour
{
    [SerializeField] private Transform followRigTarget;
    [SerializeField] private CinemachineCamera cinemachineCamera;
    [SerializeField] private CinemachineImpulseSource impulseSource;

    private CinemachineThirdPersonFollow thirdPersonFollow;
    private CinemachineHardLookAt hardLookAt;

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

    public void Apply(CameraPlan plan)
    {
        ApplyRigPose(plan.FollowRigPose);
        ApplyFraming(plan.Framing);
        ApplyAimTargetOffset(plan.AimTargetOffset);
        ApplyFieldOfView(plan.FieldOfView);
    }

    public void ApplyRigPose(CameraRigPose pose)
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

    private void ApplyFraming(CameraFramingProfile framing)
    {
        ResolveCameraComponents();
        if (thirdPersonFollow == null)
            return;

        thirdPersonFollow.CameraDistance = Mathf.Max(0.1f, framing.Distance);
        thirdPersonFollow.VerticalArmLength = Mathf.Max(0f, framing.Height);
    }

    private void ApplyAimTargetOffset(Vector3 aimTargetOffset)
    {
        ResolveCameraComponents();
        if (hardLookAt != null)
            hardLookAt.LookAtOffset = aimTargetOffset;
    }

    private void ResolveCameraComponents()
    {
        if (cinemachineCamera == null)
            return;

        if (thirdPersonFollow == null)
            thirdPersonFollow = cinemachineCamera.GetComponent<CinemachineThirdPersonFollow>();
        if (hardLookAt == null)
            hardLookAt = cinemachineCamera.GetComponent<CinemachineHardLookAt>();
    }

    public void PlayImpulse(float strength)
    {
        if (impulseSource == null)
            return;

        impulseSource.GenerateImpulseWithForce(Mathf.Clamp01(strength));
    }
}
