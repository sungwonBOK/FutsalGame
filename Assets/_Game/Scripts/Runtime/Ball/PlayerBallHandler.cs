using UnityEngine;

[RequireComponent(typeof(CharacterState))]
public class PlayerBallHandler : MonoBehaviour
{
    public static PlayerBallHandler CurrentOwner =>
        BallController.ActiveBall != null ? BallController.ActiveBall.CurrentOwner : null;

    [Header("References")]
    [SerializeField] private Rigidbody ballRb;

    [Header("Config")]
    [SerializeField] private BallConfig config;
    [SerializeField] private bool startWithBall = false;

    [Header("Effects")]
    [SerializeField] private GameObject shootEffectPrefab;

    private CharacterState state;
    private CharacterAnimator anim;
    private ThirdPersonActionCamera actionCamera;
    private BallController ball;
    private BallPossessionController possession;
    private BallConfig runtimeConfig;

    private bool isCharging;
    private float chargeStartTime;
    private Vector3 chargeShotDirection = Vector3.forward;

    public bool HasBall => possession != null && possession.HasBall;
    public bool IsCharging => isCharging;
    public float ChargeAmount01 =>
        isCharging ? Mathf.Clamp01((Time.time - chargeStartTime) / Mathf.Max(0.0001f, Config.Shot.maxChargeTime)) : 0f;

    private BallConfig Config
    {
        get
        {
            if (config == null)
            {
                if (runtimeConfig == null)
                    runtimeConfig = ScriptableObject.CreateInstance<BallConfig>();
                return runtimeConfig;
            }

            return config;
        }
    }

    private void Awake()
    {
        state = GetComponent<CharacterState>();
        anim = GetComponent<CharacterAnimator>();

        if (Camera.main != null)
            actionCamera = Camera.main.GetComponent<ThirdPersonActionCamera>();

        ball = ResolveBallController();
        possession = new BallPossessionController(this, ball, Config);

        if (ball == null)
            Debug.LogWarning("[PlayerBallHandler] Ball Rigidbody is not assigned.", this);
    }

    private void Start()
    {
        possession.AcquireInitial(startWithBall);
    }

    private void Update()
    {
        if (ball == null)
            return;

        if (isCharging && (!HasBall || !GameManager.PlayActive || (state != null && state.IsStunned)))
            CancelCharge();

        if (!GameManager.PlayActive || (state != null && state.IsStunned))
            return;

        possession.TryAcquire(Time.time, true);
    }

    private void LateUpdate()
    {
        if (HasBall)
            ball.MoveToDribblePosition(this, transform.TransformPoint(Config.Dribble.offset));
    }

    public void Shoot()
    {
        Shoot(transform.forward);
    }

    public void Shoot(Vector3 actionDirection)
    {
        if (!HasBall)
            return;

        FireShot(Config.Shot.shootForce, CaptureShotDirection(actionDirection, transform.forward));
    }

    public void StartCharge()
    {
        StartCharge(transform.forward);
    }

    public void StartCharge(Vector3 actionDirection)
    {
        if (!HasBall || isCharging)
            return;

        isCharging = true;
        chargeStartTime = Time.time;
        chargeShotDirection = CaptureShotDirection(actionDirection, transform.forward);
    }

    public void ReleaseCharge()
    {
        if (!isCharging)
            return;

        float chargeAmount = ChargeAmount01;
        Vector3 lockedDirection = chargeShotDirection;
        isCharging = false;

        if (!HasBall)
            return;

        BallConfig.ShotSettings shot = Config.Shot;
        FireShot(Mathf.Lerp(shot.passForce, shot.maxShootForce, chargeAmount), lockedDirection);
    }

    public void CancelCharge()
    {
        isCharging = false;
    }

    public void ForceRelease(Vector3 impulse)
    {
        CancelCharge();
        ReleaseWithImpulse(impulse);
    }

    public static Vector3 CaptureShotDirection(Vector3 actionDirection, Vector3 fallbackForward)
    {
        Vector3 captured = CharacterMovementUtility.NormalizePlanar(actionDirection);
        if (captured.sqrMagnitude > 0.0001f)
            return captured;

        captured = CharacterMovementUtility.NormalizePlanar(fallbackForward);
        return captured.sqrMagnitude > 0.0001f ? captured : Vector3.forward;
    }

    private void OnDisable()
    {
        CancelCharge();
        possession.ClearIfOwner();
    }

    private void OnDestroy()
    {
        if (runtimeConfig != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeConfig);
            else
                DestroyImmediate(runtimeConfig);
        }
    }

    public static void ClearPossession()
    {
        BallController.ClearActiveOwner();
    }

    private BallController ResolveBallController()
    {
        if (ballRb == null)
        {
            GameObject ballGo = GameObject.Find("Ball");
            if (ballGo != null)
                ballRb = ballGo.GetComponent<Rigidbody>();
        }

        if (ballRb != null)
            return ballRb.GetComponent<BallController>() ?? ballRb.gameObject.AddComponent<BallController>();

        return BallController.ActiveBall;
    }

    private void FireShot(float force, Vector3 direction)
    {
        if (!HasBall)
            return;
        if (anim != null)
            anim.PlayShoot();

        if (shootEffectPrefab != null && ball != null)
            Instantiate(shootEffectPrefab, ball.transform.position, Quaternion.LookRotation(direction));
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayShoot();
        if (actionCamera != null)
            actionCamera.PlayShootShake();

        ReleaseWithImpulse(direction * force);
    }

    private void ReleaseWithImpulse(Vector3 impulse)
    {
        possession.Release(Time.time, impulse);
    }
}
