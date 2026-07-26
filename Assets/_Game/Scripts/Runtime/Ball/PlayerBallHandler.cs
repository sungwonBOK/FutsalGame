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
    private Rigidbody ownerBody;
    private ThirdPersonActionCamera actionCamera;
    private BallController ball;
    private BallPossessionController possession;
    private BallInteractionController interaction;
    private BallConfig runtimeConfig;

    public bool HasBall => possession != null && possession.HasBall;
    public bool IsWithinAcquireRange => possession != null && possession.IsWithinAcquireRange;
    public bool IsCharging => interaction != null && interaction.IsCharging;
    public float ChargeAmount01 => interaction != null ? interaction.ChargeAmount01(Time.time) : 0f;
    public bool LastShotWasFirstTouch { get; private set; }

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
        ownerBody = GetComponent<Rigidbody>();

        if (Camera.main != null)
            actionCamera = Camera.main.GetComponent<ThirdPersonActionCamera>();

        ball = ResolveBallController();
        possession = new BallPossessionController(this, ball, Config);
        interaction = new BallInteractionController(possession, Config);

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

        bool canInteract = GameManager.PlayActive && (state == null || !state.IsStunned);
        Vector3 sprintTouchImpulse;
        interaction.TryTick(Time.time, canInteract, transform.forward, out sprintTouchImpulse);

        if (!canInteract)
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

        interaction.CancelAll();
        FireShot(Config.Shot.shootForce, CaptureShotDirection(actionDirection, transform.forward));
    }

    public void StartCharge()
    {
        StartCharge(transform.forward);
    }

    public void StartCharge(Vector3 actionDirection)
    {
        StartCharge(BallChargeAction.Shot);
    }

    public void ReleaseCharge()
    {
        ReleaseCharge(BallChargeAction.Shot, transform.forward);
    }

    public void StartCharge(BallChargeAction action)
    {
        interaction?.TryStartCharge(Time.time, action);
    }

    public void ReleaseCharge(BallChargeAction action, Vector3 releaseDirection)
    {
        Vector3 impulse;
        if (interaction != null && interaction.TryReleaseCharge(Time.time, action, releaseDirection, transform.forward, out impulse))
        {
            if (action == BallChargeAction.Shot)
            {
                PlayShotPresentation(CaptureShotDirection(impulse, transform.forward));
                ApplyShotReleaseModifiers(impulse);
            }
        }
    }

    public void CancelCharge()
    {
        if (interaction != null)
            interaction.CancelCharge();
    }

    public void SetSprintDribbleInput(bool held, Vector3 actionDirection)
    {
        if (interaction != null)
            interaction.SetSprintInput(held, actionDirection);
    }

    public void Pass(Vector3 actionDirection)
    {
        TryPass(actionDirection);
    }

    public bool TryPerformOneTouch(OneTouchIntent intent, Vector3 actionDirection)
    {
        if (!HasBall)
            return false;

        switch (intent)
        {
            case OneTouchIntent.Pass:
                return TryPass(actionDirection);
            case OneTouchIntent.Shot:
                Shoot(actionDirection);
                return true;
            default:
                return false;
        }
    }

    public void PlayOneTouchWhiff()
    {
        if (anim != null)
            anim.PlayShoot();
    }

    private bool TryPass(Vector3 actionDirection)
    {
        if (!HasBall || interaction == null)
            return false;

        Vector3 impulse;
        return interaction.TryPass(Time.time, actionDirection, transform.forward, out impulse);
    }

    public void ForceRelease(Vector3 impulse)
    {
        if (interaction != null)
            interaction.CancelAll();
        ReleaseWithImpulse(impulse);
    }

    public static Vector3 CaptureShotDirection(Vector3 actionDirection, Vector3 fallbackForward)
    {
        return BallInteractionController.CaptureDirection(actionDirection, fallbackForward);
    }

    private void OnDisable()
    {
        if (interaction != null)
            interaction.CancelAll();
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

        float resolvedForce = ResolveShotForce(force);
        Vector3 impulse = direction * resolvedForce;
        PlayShotPresentation(direction);
        ReleaseWithImpulse(impulse);
        ApplyShotMotion(resolvedForce);
    }

    private void PlayShotPresentation(Vector3 direction)
    {
        if (anim != null)
            anim.PlayShoot();

        if (shootEffectPrefab != null && ball != null)
            Instantiate(shootEffectPrefab, ball.transform.position, Quaternion.LookRotation(direction));
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayShoot();
        if (actionCamera != null)
            actionCamera.PlayShootShake();
    }

    private void ReleaseWithImpulse(Vector3 impulse)
    {
        possession.Release(Time.time, impulse);
    }

    private float ResolveShotForce(float force)
    {
        LastShotWasFirstTouch = possession != null && Time.time - possession.LastAcquireTime <= Config.FirstTouchWindow;
        return LastShotWasFirstTouch ? force * Config.FirstTouchBonus : force;
    }

    private void ApplyShotReleaseModifiers(Vector3 baseImpulse)
    {
        float baseForce = baseImpulse.magnitude;
        if (baseForce <= 0.0001f) return;

        float resolvedForce = ResolveShotForce(baseForce);
        if (resolvedForce > baseForce)
            ball.AddReleaseImpulse(baseImpulse.normalized * (resolvedForce - baseForce));
        ApplyShotMotion(resolvedForce);
    }

    private void ApplyShotMotion(float force)
    {
        if (ball == null) return;

        ball.AddReleaseImpulse(Vector3.up * (force * Config.ShotLoftPerForce));
        if (ownerBody != null)
        {
            Vector3 inheritedVelocity = ownerBody.linearVelocity;
            inheritedVelocity.y = 0f;
            ball.AddReleaseVelocity(inheritedVelocity * Config.ShotMomentumInherit);
        }
    }
}
