using UnityEngine;

/// <summary>
/// 한 선수의 공 다루기(획득/드리블/패스/슛)를 담당한다.
///
/// 온라인 경기에서는 공을 실제로 바꾸는 일은 서버만 한다(BallController가 막는다).
/// 내가 조종하는 선수라면 "슛했다/패스했다" 같은 의도만 서버로 보내고(NetworkPlayerAgent의 RPC),
/// 서버가 그 의도대로 공을 처리한 뒤 결과를 모두에게 복제한다.
///
/// 차지 게이지처럼 눈으로 보이는 값은 응답을 기다리면 답답하므로 로컬에서도 같이 굴린다.
/// 이때 공을 건드리는 부분은 어차피 권한 검사에서 막히므로 상태가 어긋나지 않는다.
/// </summary>
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
    private NetworkPlayerAgent netAgent;
    private bool lastSentSprintHeld;   // 클라: 마지막으로 서버에 보낸 스프린트 상태
    private bool serverSprintHeld;     // 서버: 원격 선수가 스프린트를 누르고 있는지

    /// <summary>온라인 경기로 스폰된 선수인지. 오프라인 씬 캐릭터는 false.</summary>
    private bool IsNetworked => netAgent != null && netAgent.IsSpawned;

    /// <summary>내가 조종하지만 서버는 아닌 경우 — 의도를 서버로 보내야 한다.</summary>
    private bool ForwardsToServer => IsNetworked && netAgent.IsOwner && !netAgent.IsServer;

    /// <summary>
    /// 연출을 이 자리에서 바로 재생할지.
    /// 서버가 남의 선수 요청을 대신 실행하는 중이라면 여기서 재생하지 않고 브로드캐스트에 맡긴다
    /// (그래야 호스트 화면에서 두 번 나오지 않는다).
    /// </summary>
    private bool PlaysPresentationLocally => !IsNetworked || netAgent.IsOwner;

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
        netAgent = GetComponent<NetworkPlayerAgent>();

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

        RefreshRemoteSprintDirection();

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
        if (ForwardsToServer)
        {
            if (!HasBall)
                return;

            // 슛 모션은 서버 응답을 기다리지 않고 바로 보여준다.
            PlayShotPresentationLocal(CaptureShotDirection(actionDirection, transform.forward));
            netAgent.RequestBallActionRpc(BallActionKind.Shoot, actionDirection);
            return;
        }

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
        // 차지는 게이지가 바로 반응해야 하므로 로컬에서도 같이 시작하고, 서버에도 알린다.
        if (ForwardsToServer)
            netAgent.RequestBallActionRpc(ToChargeStartKind(action), transform.forward);

        interaction?.TryStartCharge(Time.time, action);
    }

    public void ReleaseCharge(BallChargeAction action, Vector3 releaseDirection)
    {
        if (ForwardsToServer)
        {
            if (HasBall && action == BallChargeAction.Shot)
                PlayShotPresentationLocal(CaptureShotDirection(releaseDirection, transform.forward));

            netAgent.RequestBallActionRpc(ToChargeReleaseKind(action), releaseDirection);
            interaction?.CancelCharge(); // 로컬 게이지도 같이 내린다
            return;
        }

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
        if (ForwardsToServer)
            netAgent.RequestBallActionRpc(BallActionKind.CancelCharge, transform.forward);

        if (interaction != null)
            interaction.CancelCharge();
    }

    public void SetSprintDribbleInput(bool held, Vector3 actionDirection)
    {
        // 매 프레임 방향까지 보내면 낭비라, 눌림 상태가 바뀔 때만 알린다.
        // 서버는 복제된 캐릭터 방향을 대신 쓴다.
        if (ForwardsToServer && held != lastSentSprintHeld)
        {
            lastSentSprintHeld = held;
            netAgent.RequestBallActionRpc(
                held ? BallActionKind.SprintDribbleOn : BallActionKind.SprintDribbleOff,
                actionDirection);
        }

        if (interaction != null)
            interaction.SetSprintInput(held, actionDirection);
    }

    public void Pass(Vector3 actionDirection)
    {
        TryPass(actionDirection);
    }

    public bool TryPerformOneTouch(OneTouchIntent intent, Vector3 actionDirection)
    {
        if (ForwardsToServer)
        {
            if (intent != OneTouchIntent.Pass && intent != OneTouchIntent.Shot)
                return false;

            netAgent.RequestBallActionRpc(
                intent == OneTouchIntent.Pass ? BallActionKind.Pass : BallActionKind.Shoot,
                actionDirection);
            return true;
        }

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

    /// <summary>
    /// 원격 선수의 스프린트 드리블 방향을 서버에서 매 프레임 갱신한다.
    /// 눌림 상태만 RPC로 받기 때문에, 방향은 복제된 캐릭터가 바라보는 쪽을 그대로 쓴다.
    /// </summary>
    private void RefreshRemoteSprintDirection()
    {
        if (interaction == null) return;
        if (!IsNetworked || !netAgent.IsServer || netAgent.IsOwner) return;

        interaction.SetSprintInput(serverSprintHeld, transform.forward);
    }

    private static BallActionKind ToChargeStartKind(BallChargeAction action) =>
        action == BallChargeAction.Pass ? BallActionKind.StartChargePass : BallActionKind.StartChargeShot;

    private static BallActionKind ToChargeReleaseKind(BallChargeAction action) =>
        action == BallChargeAction.Pass ? BallActionKind.ReleaseChargePass : BallActionKind.ReleaseChargeShot;

    /// <summary>
    /// 서버가 클라이언트의 요청대로 공 동작을 실행한다.
    /// 여기서는 이미 서버이므로 각 메서드가 그대로 실제 처리를 한다.
    /// </summary>
    public void ExecuteRequestedAction(BallActionKind kind, Vector3 direction)
    {
        switch (kind)
        {
            case BallActionKind.Shoot: Shoot(direction); break;
            case BallActionKind.Pass: Pass(direction); break;
            case BallActionKind.StartChargeShot: StartCharge(BallChargeAction.Shot); break;
            case BallActionKind.StartChargePass: StartCharge(BallChargeAction.Pass); break;
            case BallActionKind.ReleaseChargeShot: ReleaseCharge(BallChargeAction.Shot, direction); break;
            case BallActionKind.ReleaseChargePass: ReleaseCharge(BallChargeAction.Pass, direction); break;
            case BallActionKind.CancelCharge: CancelCharge(); break;
            case BallActionKind.SprintDribbleOn: serverSprintHeld = true; SetSprintDribbleInput(true, direction); break;
            case BallActionKind.SprintDribbleOff: serverSprintHeld = false; SetSprintDribbleInput(false, direction); break;
        }
    }

    public void PlayOneTouchWhiff()
    {
        if (anim != null)
            anim.PlayShoot();
    }

    private bool TryPass(Vector3 actionDirection)
    {
        if (ForwardsToServer)
        {
            netAgent.RequestBallActionRpc(BallActionKind.Pass, actionDirection);
            return true;
        }

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

    /// <summary>
    /// 슛 연출(모션/이펙트/소리/카메라 흔들림)을 재생한다.
    /// 온라인에서는 서버만 슛을 처리하므로, 서버가 모든 클라에 재생을 알린다.
    /// </summary>
    private void PlayShotPresentation(Vector3 direction)
    {
        // 쏜 본인은 이미 재생했으므로, 서버는 나머지에게만 알린다.
        if (IsNetworked && netAgent.IsServer)
            netAgent.BroadcastShotPresentation(direction);

        if (PlaysPresentationLocally)
            PlayShotPresentationLocal(direction);
    }

    /// <summary>이 클라이언트에서만 슛 연출을 재생한다(연출 복제의 도착 지점).</summary>
    public void PlayShotPresentationLocal(Vector3 direction)
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
