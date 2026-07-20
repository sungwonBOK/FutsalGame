using UnityEngine;

public sealed class CameraBackend
{
    private readonly Camera controlledCamera;
    private readonly Transform cameraTransform;
    private readonly bool useCinemachineBackend;
    private readonly CinemachineActionCameraBackend cinemachineBackend;

    public CameraBackend(
        Camera controlledCamera,
        Transform cameraTransform,
        bool useCinemachineBackend,
        CinemachineActionCameraBackend cinemachineBackend)
    {
        this.controlledCamera = controlledCamera;
        this.cameraTransform = cameraTransform;
        this.useCinemachineBackend = useCinemachineBackend;
        this.cinemachineBackend = cinemachineBackend;
    }

    public bool UsesCinemachineBackend => useCinemachineBackend
        && cinemachineBackend != null
        && cinemachineBackend.HasFollowRigTarget;

    public float CurrentFieldOfView(float fallback)
    {
        return controlledCamera != null ? controlledCamera.fieldOfView : fallback;
    }

    public void Apply(CameraPlan plan)
    {
        if (UsesCinemachineBackend)
        {
            cinemachineBackend.Apply(plan);
            return;
        }

        cameraTransform.SetPositionAndRotation(plan.CameraPose.Position, plan.CameraPose.Rotation);
        if (controlledCamera != null)
            controlledCamera.fieldOfView = plan.FieldOfView;
    }

    public void ApplyInitialFieldOfView(float fieldOfView)
    {
        if (controlledCamera != null)
            controlledCamera.fieldOfView = fieldOfView;
        if (cinemachineBackend != null)
            cinemachineBackend.ApplyFieldOfView(fieldOfView);
    }

    public void PlayImpulse(float strength)
    {
        if (UsesCinemachineBackend)
            cinemachineBackend.PlayImpulse(strength);
    }
}
