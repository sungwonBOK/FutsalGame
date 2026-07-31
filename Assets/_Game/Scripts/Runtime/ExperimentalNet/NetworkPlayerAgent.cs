using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 네트워크로 스폰된 선수 한 명의 "정체성"(팀 / 사람인지 AI인지)을 담고,
/// 그에 맞춰 이 인스턴스에서 어떤 컴포넌트를 켜고 끌지 결정한다.
///
/// 권한 모델(하이브리드):
///  - 입력(PlayerInput)은 소유 클라이언트에서만 돈다. 다른 클라에서는 꺼두고 트랜스폼만 복제받는다.
///  - AI(SimpleAIController)는 서버에서만 돈다. AI도 결국 "입력을 만드는 쪽"이라 판단은 한 곳에서만 해야 한다.
///
/// 팀/AI 여부는 서버가 스폰 직후 NetworkVariable에 써서 모든 클라로 복제한다.
/// 클라는 값이 도착하는 시점이 한 틱 늦을 수 있으므로 OnValueChanged로도 다시 반영한다.
/// </summary>
/// <summary>클라이언트가 서버에 요청하는 전투 동작의 종류.</summary>
public enum CombatActionKind : byte
{
    Punch,
    SlideTackle,
    CrossPunch,
}

/// <summary>클라이언트가 서버에 요청하는 공 동작의 종류.</summary>
public enum BallActionKind : byte
{
    Shoot,
    Pass,
    StartChargeShot,
    StartChargePass,
    ReleaseChargeShot,
    ReleaseChargePass,
    CancelCharge,
    SprintDribbleOn,
    SprintDribbleBurstOn,
    SprintDribbleOff,
}

[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerAgent : NetworkBehaviour
{
    [Header("팀 색")]
    [Tooltip("팀 색을 입힐 렌더러. 비우면 자식 렌더러를 전부 사용한다.")]
    [SerializeField] private Renderer[] teamRenderers;
    [SerializeField] private Color blueTeamColor = new Color(0.25f, 0.5f, 1f);
    [SerializeField] private Color redTeamColor = new Color(1f, 0.3f, 0.3f);

    private readonly NetworkVariable<byte> team = new NetworkVariable<byte>(MatchSpawnPoints.TeamBlue);
    private readonly NetworkVariable<bool> controlledByAI = new NetworkVariable<bool>(false);

    // 서버가 Spawn() 전에 넣어두는 값. OnNetworkSpawn에서 NetworkVariable로 옮긴다.
    private byte pendingTeam;
    private bool pendingIsAI;

    private PlayerInput playerInput;
    private SimpleAIController aiController;
    private CharacterMotor motor;
    private PlayerBallHandler ballHandler;
    private CombatController combat;
    private CharacterState characterState;
    private MaterialPropertyBlock propertyBlock;

    /// <summary>기절 여부는 서버가 정해 모두에게 복제한다.</summary>
    private readonly NetworkVariable<bool> stunned = new NetworkVariable<bool>(false);

    /// <summary>이 선수의 팀 (0 = Blue, 1 = Red).</summary>
    public byte Team => team.Value;

    /// <summary>AI가 조종하는 선수인지.</summary>
    public bool IsAIControlled => controlledByAI.Value;

    /// <summary>이 클라이언트가 직접 조종하는 선수인지(= 카메라가 따라가야 할 대상).</summary>
    public bool IsLocalHumanPlayer => IsOwner && !controlledByAI.Value;

    private void Awake()
    {
        playerInput = GetComponent<PlayerInput>();
        aiController = GetComponent<SimpleAIController>();
        motor = GetComponent<CharacterMotor>();
        ballHandler = GetComponent<PlayerBallHandler>();
        combat = GetComponent<CombatController>();
        characterState = GetComponent<CharacterState>();
        if (GetComponent<P2pMovementReplicator>() == null)
            gameObject.AddComponent<P2pMovementReplicator>();

        // 정체가 정해지기 전에 잘못 움직이지 않도록 둘 다 꺼두고 시작한다.
        if (playerInput != null) playerInput.enabled = false;
        if (aiController != null) aiController.enabled = false;
    }

    /// <summary>서버가 Spawn() 직전에 호출해 이 선수의 정체를 정한다.</summary>
    public void ServerPrepare(byte teamIndex, bool isAI)
    {
        pendingTeam = teamIndex;
        pendingIsAI = isAI;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            team.Value = pendingTeam;
            controlledByAI.Value = pendingIsAI;
        }

        team.OnValueChanged += HandleTeamChanged;
        controlledByAI.OnValueChanged += HandleControlModeChanged;
        stunned.OnValueChanged += HandleStunChanged;

        ApplyIdentity();
        RegisterWithMatch();
    }

    private void Update()
    {
        // 서버가 기절 상태 변화를 감지해 복제한다(피격뿐 아니라 시간이 지나 풀리는 것도 포함).
        if (!IsSpawned || !IsServer || characterState == null)
            return;

        if (stunned.Value != characterState.IsStunned)
            stunned.Value = characterState.IsStunned;
    }

    private void HandleStunChanged(bool previous, bool current)
    {
        if (IsServer || characterState == null) return; // 서버는 이미 실제 상태를 갖고 있다

        characterState.MirrorStun(current);
    }

    public override void OnNetworkDespawn()
    {
        team.OnValueChanged -= HandleTeamChanged;
        controlledByAI.OnValueChanged -= HandleControlModeChanged;
        stunned.OnValueChanged -= HandleStunChanged;

        if (GameManager.Instance != null)
            GameManager.Instance.UnregisterCharacter(transform);
    }

    private void HandleTeamChanged(byte previous, byte current) => ApplyIdentity();
    private void HandleControlModeChanged(bool previous, bool current) => ApplyIdentity();

    /// <summary>복제된 팀/AI 값에 맞춰 컴포넌트 활성화와 팀 색을 다시 맞춘다.</summary>
    private void ApplyIdentity()
    {
        bool isAI = controlledByAI.Value;

        // 사람이 조종하는 선수의 입력은 그 사람의 클라이언트에서만 처리한다.
        if (playerInput != null)
            playerInput.enabled = IsOwner && !isAI;

        // AI 판단은 서버 한 곳에서만. 클라는 결과(트랜스폼/상태)만 받는다.
        if (aiController != null)
        {
            aiController.enabled = IsServer && isAI;
            if (IsServer && isAI && MatchSpawnPoints.Instance != null)
            {
                aiController.ConfigureGoals(
                    MatchSpawnPoints.Instance.GetAttackGoal(team.Value),
                    MatchSpawnPoints.Instance.GetOwnGoal(team.Value));
            }
        }

        // 이동은 소유자가 시뮬레이션하고 나머지는 트랜스폼을 복제받는다.
        // 비소유 인스턴스에서 모터가 Rigidbody를 건드리면 복제된 위치와 싸우게 된다.
        if (motor != null)
            motor.enabled = IsOwner;

        ApplyTeamColor();
        BindLocalPresentation();
    }

    private void ApplyTeamColor()
    {
        Renderer[] renderers = (teamRenderers != null && teamRenderers.Length > 0)
            ? teamRenderers
            : GetComponentsInChildren<Renderer>(includeInactive: true);
        if (renderers == null || renderers.Length == 0) return;

        propertyBlock ??= new MaterialPropertyBlock();
        Color color = team.Value == MatchSpawnPoints.TeamBlue ? blueTeamColor : redTeamColor;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            renderer.GetPropertyBlock(propertyBlock);
            // URP Lit은 _BaseColor, 구형 셰이더 호환을 위해 _Color도 같이 넣는다.
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_Color", color);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    /// <summary>
    /// 내가 조종하는 선수라면 카메라와 HUD가 나를 보게 한다.
    /// 씬에 미리 놓인 카메라/HUD는 오프라인 캐릭터를 가리키고 있으므로 여기서 갈아끼운다.
    /// </summary>
    private void BindLocalPresentation()
    {
        if (!IsLocalHumanPlayer) return;

        if (Camera.main != null)
        {
            ThirdPersonActionCamera actionCamera = Camera.main.GetComponent<ThirdPersonActionCamera>();
            if (actionCamera != null)
            {
                Transform ball = BallController.ActiveBall != null ? BallController.ActiveBall.transform : null;
                actionCamera.SetTargets(transform, GetComponent<Rigidbody>(), ball);
            }

            // 시점 전환(레거시 카메라)도 같은 선수를 보게 맞춘다.
            CameraViewSwitcher viewSwitcher = Camera.main.GetComponent<CameraViewSwitcher>();
            if (viewSwitcher != null)
                viewSwitcher.SetTarget(transform);
        }

        AbilityCooldownUI cooldownUI = FindAnyObjectByType<AbilityCooldownUI>();
        if (cooldownUI != null)
            cooldownUI.SetTarget(GetComponent<CombatController>());

        ChargeGaugeUI chargeUI = FindAnyObjectByType<ChargeGaugeUI>();
        if (chargeUI != null)
            chargeUI.SetTarget(GetComponent<PlayerBallHandler>());
    }

    /// <summary>리셋(킥오프/득점 후) 대상으로 경기 매니저에 등록한다.</summary>
    private void RegisterWithMatch()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterCharacter(transform, transform.position, transform.rotation);
    }

    // ---------------- 공 동작 요청/연출 ----------------

    /// <summary>
    /// 내가 조종하는 선수의 공 동작을 서버에 요청한다.
    /// 공을 실제로 움직이는 것은 서버뿐이므로, 클라이언트는 "무엇을 하려는지"만 보낸다.
    /// </summary>
    [Rpc(SendTo.Server)]
    public void RequestBallActionRpc(BallActionKind kind, Vector3 direction)
    {
        if (ballHandler != null)
            ballHandler.ExecuteRequestedAction(kind, direction);
    }

    /// <summary>슛한 본인 말고 나머지에게 슛 연출을 재생시킨다(본인은 이미 재생했다).</summary>
    public void BroadcastShotPresentation(Vector3 direction)
    {
        if (!IsServer || !IsSpawned) return;

        ShotPresentationRpc(direction);
    }

    [Rpc(SendTo.NotOwner)]
    private void ShotPresentationRpc(Vector3 direction)
    {
        if (ballHandler != null)
            ballHandler.PlayShotPresentationLocal(direction);
    }

    // ---------------- 전투 요청/결과 ----------------

    /// <summary>내가 조종하는 선수의 전투 동작을 서버에 요청한다. 맞았는지는 서버가 판정한다.</summary>
    [Rpc(SendTo.Server)]
    public void RequestCombatActionRpc(CombatActionKind kind, Vector3 direction)
    {
        if (combat == null) return;

        switch (kind)
        {
            case CombatActionKind.Punch: combat.Punch(direction); break;
            case CombatActionKind.CrossPunch: combat.CrossPunch(direction); break;
            default: combat.SlideTackle(direction); break;
        }
    }

    /// <summary>동작을 시작한 본인 말고 나머지에게 모션을 보여준다(본인은 이미 재생했다).</summary>
    public void BroadcastCombatAnimation(CombatActionKind kind)
    {
        if (!IsServer || !IsSpawned) return;

        CombatAnimationRpc(kind);
    }

    [Rpc(SendTo.NotOwner)]
    private void CombatAnimationRpc(CombatActionKind kind)
    {
        if (combat != null)
            combat.PlayActionPresentationLocal(kind);
    }

    /// <summary>서버가 피격 연출을 모든 클라이언트에서 재생시킨다.</summary>
    public void BroadcastHitPresentation(Vector3 hitPosition, Vector3 hitDirection, ulong victimObjectId)
    {
        if (!IsServer || !IsSpawned) return;

        HitPresentationRpc(hitPosition, hitDirection, victimObjectId);
    }

    [Rpc(SendTo.Everyone)]
    private void HitPresentationRpc(Vector3 hitPosition, Vector3 hitDirection, ulong victimObjectId)
    {
        if (combat == null) return;

        // 내가 때렸거나 내가 맞았을 때만 화면을 흔든다.
        bool involvesLocalPlayer = IsOwner || IsLocallyOwned(victimObjectId);
        combat.PlayHitPresentationLocal(hitPosition, hitDirection, involvesLocalPlayer);
    }

    private static bool IsLocallyOwned(ulong objectId)
    {
        if (objectId == 0 || NetworkManager.Singleton == null)
            return false;

        return NetworkManager.Singleton.SpawnManager.SpawnedObjects
                   .TryGetValue(objectId, out NetworkObject netObject)
               && netObject != null
               && netObject.IsOwner;
    }

    /// <summary>서버가 판정한 넉백을, 이동 권한을 가진 소유자에게 적용시킨다.</summary>
    public void ServerApplyKnockback(Vector3 impulse)
    {
        if (!IsServer || !IsSpawned) return;

        KnockbackRpc(impulse);
    }

    [Rpc(SendTo.Owner)]
    private void KnockbackRpc(Vector3 impulse)
    {
        if (characterState != null)
            characterState.ApplyKnockbackImpulse(impulse);
    }

    /// <summary>회피 무적을 서버에도 알린다. 서버가 모르면 무적이 판정에서 무시된다.</summary>
    [Rpc(SendTo.Server)]
    public void ReportInvulnerabilityRpc(float duration)
    {
        if (characterState != null)
            characterState.ApplyInvulnerability(duration);
    }

    // ---------------- 리셋(순간이동) ----------------

    /// <summary>
    /// 이 선수를 지정 위치로 되돌린다. 서버가 호출한다.
    ///
    /// 이동은 소유자 권한이라 서버가 남의 캐릭터를 직접 옮겨봐야 곧 덮어써진다.
    /// 그래서 실제 이동은 소유자에게 시켜야 한다.
    /// </summary>
    public void ServerRequestTeleport(Vector3 position, Quaternion rotation)
    {
        if (!IsServer || !IsSpawned) return;

        TeleportRpc(position, rotation);
    }

    [Rpc(SendTo.Owner)]
    private void TeleportRpc(Vector3 position, Quaternion rotation)
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        // 보간 때문에 이전 위치에서 미끄러지듯 오는 것을 막고 즉시 옮긴다.
        NetworkTransform networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform != null)
            networkTransform.Teleport(position, rotation, transform.localScale);
        else
            transform.SetPositionAndRotation(position, rotation);
    }
}
