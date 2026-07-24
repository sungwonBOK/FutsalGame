using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class ThirdPersonActionCamera : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Transform ballTarget;

    [Header("Settings")]
    [SerializeField] private ThirdPersonActionCameraSettings settings;

    [Header("Cinemachine Backend")]
    [SerializeField] private bool useCinemachineBackend;
    [SerializeField] private CinemachineActionCameraBackend cinemachineBackend;

    private CameraContextProvider contextProvider;
    private CameraDirector cameraDirector;
    private CameraLookController lookController;
    private PositionResolver positionResolver;
    private FovResolver fovResolver;
    private EffectResolver effectResolver;
    private CameraBackend cameraBackend;

    private ThirdPersonActionCameraSettings Settings
    {
        get
        {
            if (settings == null)
                settings = ScriptableObject.CreateInstance<ThirdPersonActionCameraSettings>();
            return settings;
        }
    }

    private void Awake()
    {
        Camera controlledCamera = GetComponent<Camera>();
        if (cinemachineBackend == null)
            cinemachineBackend = GetComponent<CinemachineActionCameraBackend>();

        contextProvider = new CameraContextProvider(playerTarget, playerRigidbody, ballTarget, transform);
        contextProvider.ResolveMissingTargets();
        cameraDirector = new CameraDirector();
        lookController = new CameraLookController();
        lookController.Initialize(transform.eulerAngles.y, NormalizePitch(transform.eulerAngles.x));
        positionResolver = new PositionResolver();
        fovResolver = new FovResolver();
        effectResolver = new EffectResolver();
        cameraBackend = new CameraBackend(controlledCamera, transform, useCinemachineBackend, cinemachineBackend);

        positionResolver.Initialize(Settings.distance);
        cameraBackend.ApplyInitialFieldOfView(Settings.baseFov);
    }

    private void LateUpdate()
    {
        bool isPlayActive = GameManager.PlayActive;
        MouseLookInput.SetCursorLocked(isPlayActive);
        if (!contextProvider.TryGet(Time.deltaTime, out CameraContext context))
            return;

        CameraLookState look = lookController.Update(
            isPlayActive ? MouseLookInput.ReadDelta() : Vector2.zero,
            Settings.mouseYawSensitivity,
            Settings.mousePitchSensitivity,
            Settings.invertMouseY,
            Settings.minPitch,
            Settings.maxPitch);
        ThirdPersonActionCameraSettings currentSettings = Settings;
        Vector3 aimTargetOffset = CameraLookOffsetResolver.Resolve(
            look.Pitch,
            Mathf.Max(Mathf.Abs(currentSettings.minPitch), Mathf.Abs(currentSettings.maxPitch)),
            currentSettings.maxPitchLookOffset);
        CameraModeResult modeResult = cameraDirector.Resolve(context, currentSettings, look);
        CameraPositionResult positionResult = positionResolver.Resolve(
            modeResult,
            look,
            aimTargetOffset,
            context,
            currentSettings,
            cameraBackend.UsesCinemachineBackend);
        CameraRigPose cameraPose = effectResolver.Resolve(
            positionResult.CameraPose,
            context,
            currentSettings,
            !cameraBackend.UsesCinemachineBackend);
        float fieldOfView = fovResolver.Resolve(cameraBackend.CurrentFieldOfView(currentSettings.baseFov), context, modeResult, currentSettings);
        cameraBackend.Apply(CameraPlanBuilder.Build(
            cameraPose,
            positionResult.FollowRigPose,
            fieldOfView,
            modeResult.Framing,
            aimTargetOffset));
    }

    public void SetTargets(Transform player, Rigidbody playerBody, Transform ball)
    {
        playerTarget = player;
        playerRigidbody = playerBody;
        ballTarget = ball;
        contextProvider.SetTargets(player, playerBody, ball);
    }

    public void AddShake(float strength)
    {
        effectResolver.AddShake(strength, Settings);
        cameraBackend.PlayImpulse(Mathf.Clamp01(strength * Settings.shakeStrength));
    }

    public void PlayShootShake()
    {
        AddShake(0.65f);
    }

    public void PlayHitShake()
    {
        AddShake(0.85f);
    }

    private static float NormalizePitch(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
