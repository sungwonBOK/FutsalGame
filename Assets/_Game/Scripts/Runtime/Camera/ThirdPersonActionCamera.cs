using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class ThirdPersonActionCamera : MonoBehaviour
{
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

    [Header("Targets")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Transform ballTarget;

    [Header("Settings")]
    [SerializeField] private ThirdPersonActionCameraSettings settings;

    [Header("Cinemachine Backend")]
    [SerializeField] private bool useCinemachineBackend;
    [SerializeField] private CinemachineActionCameraBackend cinemachineBackend;

    private Camera controlledCamera;
    private Vector3 positionVelocity;
    private float yawVelocity;
    private float currentYaw;
    private float currentDistance;
    private float distanceVelocity;
    private float fovVelocity;
    private float shakeAmount;
    private float shakeTimeRemaining;

    public bool BallHintRequired { get; private set; }

    private bool HasCinemachineBackend => useCinemachineBackend
        && cinemachineBackend != null
        && cinemachineBackend.HasFollowRigTarget;

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
        controlledCamera = GetComponent<Camera>();
        if (cinemachineBackend == null)
            cinemachineBackend = GetComponent<CinemachineActionCameraBackend>();

        if (playerTarget == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
                playerTarget = player.transform;
        }

        if (ballTarget == null)
        {
            GameObject ball = GameObject.Find("Ball");
            if (ball != null)
                ballTarget = ball.transform;
        }

        if (playerRigidbody == null && playerTarget != null)
            playerRigidbody = playerTarget.GetComponent<Rigidbody>();

        currentYaw = transform.eulerAngles.y;
        currentDistance = Settings.distance;
        if (controlledCamera != null)
            controlledCamera.fieldOfView = Settings.baseFov;
        if (cinemachineBackend != null)
            cinemachineBackend.ApplyFieldOfView(Settings.baseFov);
    }

    private void LateUpdate()
    {
        if (playerTarget == null)
            return;

        ThirdPersonActionCameraSettings s = Settings;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 playerPosition = playerTarget.position;
        Vector3 velocity = playerRigidbody != null ? playerRigidbody.linearVelocity : Vector3.zero;
        Vector3 heading = SelectHeading(playerTarget.forward, velocity, currentYaw, s.movementPrioritySpeed);
        float desiredYaw = DirectionToYaw(heading);

        desiredYaw = ApplyBallAssistYaw(
            desiredYaw,
            playerPosition,
            ballTarget != null ? ballTarget.position : playerPosition,
            s.ballAssistEdgeAngle,
            s.ballAssistMaxAngle,
            s.ballAssistStrength);

        BallHintRequired = ballTarget != null && BallNeedsHint(desiredYaw, playerPosition, ballTarget.position, s.ballAssistMaxAngle);
        currentYaw = UpdateYaw(currentYaw, desiredYaw, ref yawVelocity, dt, s.rotationDeadZone, s.rotationSmoothTime, s.maxRotationSpeed);

        Vector3 lookPoint = playerPosition + Vector3.up * s.lookAtHeight;
        bool hasCinemachineBackend = HasCinemachineBackend;

        if (hasCinemachineBackend)
        {
            cinemachineBackend.ApplyRigPose(BuildFollowRigPose(playerPosition, currentYaw, s.lookAtHeight));
        }
        else
        {
            float desiredDistance = ResolveCollisionDistance(lookPoint, currentYaw, s);
            float distanceSmoothTime = desiredDistance < currentDistance ? s.collisionMoveInSmoothTime : s.collisionReturnSmoothTime;
            currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref distanceVelocity, distanceSmoothTime, Mathf.Infinity, dt);

            Vector3 desiredPosition = BuildCameraPosition(lookPoint, currentYaw, currentDistance, s.height);
            desiredPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref positionVelocity, s.positionSmoothTime, Mathf.Infinity, dt);

            Quaternion desiredRotation = BuildStableLookRotation(desiredPosition, lookPoint);
            ApplyShake(ref desiredRotation, ref desiredPosition, dt, s);

            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
        }

        float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        float targetFov = CalculateTargetFov(s.baseFov, speed, s.sprintSpeed, s.sprintFovBoost);
        float currentFov = controlledCamera != null ? controlledCamera.fieldOfView : s.baseFov;
        float smoothedFov = Mathf.SmoothDamp(currentFov, targetFov, ref fovVelocity, s.fovSmoothTime, Mathf.Infinity, dt);
        if (hasCinemachineBackend)
        {
            cinemachineBackend.ApplyFieldOfView(smoothedFov);
        }
        else if (controlledCamera != null)
        {
            controlledCamera.fieldOfView = smoothedFov;
        }
    }

    public void SetTargets(Transform player, Rigidbody playerBody, Transform ball)
    {
        playerTarget = player;
        playerRigidbody = playerBody;
        ballTarget = ball;
    }

    public void AddShake(float strength)
    {
        ThirdPersonActionCameraSettings s = Settings;
        float targetShake = Mathf.Clamp01(strength * s.shakeStrength);
        if (HasCinemachineBackend)
            cinemachineBackend.PlayImpulse(targetShake);

        shakeAmount = Mathf.Clamp01(Mathf.Max(shakeAmount, targetShake));
        shakeTimeRemaining = Mathf.Max(shakeTimeRemaining, 0.12f);
    }

    public void PlayShootShake()
    {
        AddShake(0.65f);
    }

    public void PlayHitShake()
    {
        AddShake(0.85f);
    }

    public static float UpdateYaw(
        float currentYaw,
        float desiredYaw,
        ref float yawVelocity,
        float deltaTime,
        float deadZone,
        float smoothTime,
        float maxRotationSpeed)
    {
        float delta = Mathf.DeltaAngle(currentYaw, desiredYaw);
        if (Mathf.Abs(delta) <= deadZone)
        {
            yawVelocity = 0f;
            return currentYaw;
        }

        float adjustedTarget = currentYaw + Mathf.Sign(delta) * (Mathf.Abs(delta) - deadZone);
        float smoothed = Mathf.SmoothDampAngle(
            currentYaw,
            adjustedTarget,
            ref yawVelocity,
            Mathf.Max(0.0001f, smoothTime),
            Mathf.Max(1f, maxRotationSpeed),
            Mathf.Max(0.0001f, deltaTime));

        return Mathf.MoveTowardsAngle(currentYaw, smoothed, maxRotationSpeed * deltaTime);
    }

    public static float ApplyBallAssistYaw(
        float currentYaw,
        Vector3 playerPosition,
        Vector3 ballPosition,
        float edgeAngle,
        float maxAssistAngle,
        float strength)
    {
        Vector3 toBall = ballPosition - playerPosition;
        toBall.y = 0f;
        if (toBall.sqrMagnitude < 0.0001f || strength <= 0f)
            return currentYaw;

        float ballYaw = DirectionToYaw(toBall);
        float delta = Mathf.DeltaAngle(currentYaw, ballYaw);
        float absDelta = Mathf.Abs(delta);

        if (absDelta <= edgeAngle || absDelta >= maxAssistAngle)
            return currentYaw;

        float edge01 = Mathf.InverseLerp(edgeAngle, maxAssistAngle, absDelta);
        return Mathf.LerpAngle(currentYaw, ballYaw, Mathf.Clamp01(strength) * edge01);
    }

    public static float CalculateTargetFov(float baseFov, float speed, float sprintSpeed, float sprintFovBoost)
    {
        float speed01 = Mathf.Clamp01(speed / Mathf.Max(0.0001f, sprintSpeed));
        return baseFov + Mathf.Clamp(sprintFovBoost, 0f, 5f) * speed01;
    }

    public static Quaternion BuildStableLookRotation(Vector3 cameraPosition, Vector3 lookPoint)
    {
        Vector3 lookDirection = lookPoint - cameraPosition;
        if (lookDirection.sqrMagnitude < 0.0001f)
            lookDirection = Vector3.forward;

        Quaternion rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        Vector3 euler = rotation.eulerAngles;
        euler.z = 0f;
        return Quaternion.Euler(euler);
    }

    public static CameraRigPose BuildFollowRigPose(Vector3 playerPosition, float yaw, float lookAtHeight)
    {
        Vector3 lookPoint = playerPosition + Vector3.up * lookAtHeight;
        Quaternion yawOnlyRotation = Quaternion.Euler(0f, yaw, 0f);
        return new CameraRigPose(lookPoint, yawOnlyRotation);
    }

    private static Vector3 SelectHeading(Vector3 targetForward, Vector3 velocity, float fallbackYaw, float movementPrioritySpeed)
    {
        Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z);
        if (flatVelocity.magnitude >= movementPrioritySpeed)
            return flatVelocity.normalized;

        Vector3 flatForward = new Vector3(targetForward.x, 0f, targetForward.z);
        if (flatForward.sqrMagnitude > 0.0001f)
            return flatForward.normalized;

        return Quaternion.Euler(0f, fallbackYaw, 0f) * Vector3.forward;
    }

    private static Vector3 BuildCameraPosition(Vector3 lookPoint, float yaw, float distance, float height)
    {
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        return lookPoint - forward * distance + Vector3.up * height;
    }

    private static float DirectionToYaw(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
            return 0f;
        return Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
    }

    private static bool BallNeedsHint(float yaw, Vector3 playerPosition, Vector3 ballPosition, float maxAssistAngle)
    {
        Vector3 toBall = ballPosition - playerPosition;
        toBall.y = 0f;
        if (toBall.sqrMagnitude < 0.0001f)
            return false;

        return Mathf.Abs(Mathf.DeltaAngle(yaw, DirectionToYaw(toBall))) >= maxAssistAngle;
    }

    private float ResolveCollisionDistance(Vector3 lookPoint, float yaw, ThirdPersonActionCameraSettings s)
    {
        Vector3 desiredPosition = BuildCameraPosition(lookPoint, yaw, s.distance, s.height);
        Vector3 toCamera = desiredPosition - lookPoint;
        float desiredDistance = toCamera.magnitude;
        if (desiredDistance <= 0.0001f)
            return s.minCollisionDistance;

        Vector3 direction = toCamera / desiredDistance;
        if (Physics.SphereCast(lookPoint, s.collisionRadius, direction, out RaycastHit hit, desiredDistance, s.collisionMask, QueryTriggerInteraction.Ignore))
            return Mathf.Max(s.minCollisionDistance, hit.distance - s.collisionRadius);

        return s.distance;
    }

    private void ApplyShake(ref Quaternion rotation, ref Vector3 position, float deltaTime, ThirdPersonActionCameraSettings s)
    {
        if (shakeTimeRemaining <= 0f || shakeAmount <= 0f)
            return;

        shakeTimeRemaining -= deltaTime;
        float seed = Time.time * s.shakeFrequency;
        float offsetX = (Mathf.PerlinNoise(seed, 0.17f) - 0.5f) * 2f;
        float offsetY = (Mathf.PerlinNoise(0.83f, seed) - 0.5f) * 2f;
        float yawKick = (Mathf.PerlinNoise(seed, seed) - 0.5f) * 2f;

        position += transform.right * (offsetX * s.maxShakeOffset * shakeAmount);
        position += Vector3.up * (offsetY * s.maxShakeOffset * shakeAmount);
        rotation = Quaternion.AngleAxis(yawKick * s.maxShakeAngle * shakeAmount, Vector3.up) * rotation;

        shakeAmount = Mathf.MoveTowards(shakeAmount, 0f, s.shakeDecay * deltaTime);
        if (shakeTimeRemaining <= 0f)
            shakeAmount = 0f;
    }
}
