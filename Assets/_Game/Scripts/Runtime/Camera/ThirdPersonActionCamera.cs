using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class ThirdPersonActionCamera : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private CharacterLocomotion playerLocomotion;
    [SerializeField] private Transform ballTarget;

    [Header("Settings")]
    [SerializeField] private ThirdPersonActionCameraSettings settings;

    [Header("Cinemachine Backend")]
    [SerializeField] private bool useCinemachineBackend;
    [SerializeField] private CinemachineActionCameraBackend cinemachineBackend;

    private CameraContextProvider contextProvider;
    private CameraDirector cameraDirector;
    private AimResolver aimResolver;
    private PositionResolver positionResolver;
    private FovResolver fovResolver;
    private EffectResolver effectResolver;
    private CameraBackend cameraBackend;
    private float currentYaw;
    private float yawVelocity;

    public bool BallHintRequired { get; private set; }

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

        contextProvider = new CameraContextProvider(playerTarget, playerRigidbody, playerLocomotion, ballTarget, transform);
        contextProvider.ResolveMissingTargets();
        cameraDirector = new CameraDirector();
        aimResolver = new AimResolver();
        positionResolver = new PositionResolver();
        fovResolver = new FovResolver();
        effectResolver = new EffectResolver();
        cameraBackend = new CameraBackend(controlledCamera, transform, useCinemachineBackend, cinemachineBackend);

        currentYaw = transform.eulerAngles.y;
        positionResolver.Initialize(Settings.distance);
        cameraBackend.ApplyInitialFieldOfView(Settings.baseFov);
    }

    private void LateUpdate()
    {
        if (!contextProvider.TryGet(currentYaw, Time.deltaTime, out CameraContext context))
            return;

        ThirdPersonActionCameraSettings currentSettings = Settings;
        CameraModeResult modeResult = cameraDirector.Resolve(context, currentSettings);
        BallHintRequired = modeResult.BallHintRequired;
        currentYaw = aimResolver.UpdateYaw(currentYaw, modeResult.DesiredYaw, ref yawVelocity, context.DeltaTime, currentSettings);

        CameraPositionResult positionResult = positionResolver.Resolve(
            modeResult,
            currentYaw,
            context,
            currentSettings,
            cameraBackend.UsesCinemachineBackend);
        CameraRigPose cameraPose = effectResolver.Resolve(
            positionResult.CameraPose,
            context,
            currentSettings,
            !cameraBackend.UsesCinemachineBackend);
        float fieldOfView = fovResolver.Resolve(cameraBackend.CurrentFieldOfView(currentSettings.baseFov), context, currentSettings);
        cameraBackend.Apply(CameraPlanBuilder.Build(cameraPose, positionResult.FollowRigPose, fieldOfView));
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

    public static float UpdateYaw(float currentYaw, float desiredYaw, ref float yawVelocity, float deltaTime, float deadZone, float smoothTime, float maxRotationSpeed)
    {
        return AimResolver.UpdateYaw(currentYaw, desiredYaw, ref yawVelocity, deltaTime, deadZone, smoothTime, maxRotationSpeed, float.PositiveInfinity, smoothTime, maxRotationSpeed);
    }

    public static float UpdateYaw(float currentYaw, float desiredYaw, ref float yawVelocity, float deltaTime, float deadZone, float smoothTime, float maxRotationSpeed, float quickTurnAngle, float quickTurnSmoothTime, float quickTurnMaxRotationSpeed)
    {
        return AimResolver.UpdateYaw(currentYaw, desiredYaw, ref yawVelocity, deltaTime, deadZone, smoothTime, maxRotationSpeed, quickTurnAngle, quickTurnSmoothTime, quickTurnMaxRotationSpeed);
    }

    public static float ApplyBallAssistYaw(float currentYaw, Vector3 playerPosition, Vector3 ballPosition, float edgeAngle, float maxAssistAngle, float strength)
    {
        return AimResolver.ApplyBallAssistYaw(currentYaw, playerPosition, ballPosition, edgeAngle, maxAssistAngle, strength, false, currentYaw, 0f);
    }

    public static float ApplyBallAssistYaw(float currentYaw, Vector3 playerPosition, Vector3 ballPosition, float edgeAngle, float maxAssistAngle, float strength, bool hasActiveMoveInput, float activeMoveYaw, float maxActiveInputAssistAngle)
    {
        return AimResolver.ApplyBallAssistYaw(currentYaw, playerPosition, ballPosition, edgeAngle, maxAssistAngle, strength, hasActiveMoveInput, activeMoveYaw, maxActiveInputAssistAngle);
    }

    public static float CalculateTargetFov(float baseFov, float speed, float sprintSpeed, float sprintFovBoost)
    {
        return FovResolver.CalculateTargetFov(baseFov, speed, sprintSpeed, sprintFovBoost);
    }

    public static Quaternion BuildStableLookRotation(Vector3 cameraPosition, Vector3 lookPoint)
    {
        return PositionResolver.BuildStableLookRotation(cameraPosition, lookPoint);
    }

    public static CameraRigPose BuildFollowRigPose(Vector3 playerPosition, float yaw, float lookAtHeight)
    {
        return PositionResolver.BuildFollowRigPose(playerPosition, yaw, lookAtHeight);
    }

    public static Vector3 SelectHeading(bool hasMoveIntent, Vector3 moveIntent, Vector3 actionIntent, Vector3 velocity, Vector3 targetForward, float fallbackYaw, float movementPrioritySpeed)
    {
        return AimResolver.SelectHeading(hasMoveIntent, moveIntent, actionIntent, velocity, targetForward, fallbackYaw, movementPrioritySpeed);
    }
}
