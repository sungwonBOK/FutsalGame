using Unity.Netcode;
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
    private MaterialPropertyBlock propertyBlock;

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

        ApplyIdentity();
        RegisterWithMatch();
    }

    public override void OnNetworkDespawn()
    {
        team.OnValueChanged -= HandleTeamChanged;
        controlledByAI.OnValueChanged -= HandleControlModeChanged;

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

        ApplyTeamColor();
        BindLocalCamera();
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

    /// <summary>내가 조종하는 선수라면 씬 카메라가 나를 따라오게 한다.</summary>
    private void BindLocalCamera()
    {
        if (!IsLocalHumanPlayer || Camera.main == null) return;

        ThirdPersonActionCamera actionCamera = Camera.main.GetComponent<ThirdPersonActionCamera>();
        if (actionCamera == null) return;

        Rigidbody body = GetComponent<Rigidbody>();
        Transform ball = BallController.ActiveBall != null ? BallController.ActiveBall.transform : null;
        actionCamera.SetTargets(transform, body, ball);
    }

    /// <summary>리셋(킥오프/득점 후) 대상으로 경기 매니저에 등록한다.</summary>
    private void RegisterWithMatch()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterCharacter(transform, transform.position, transform.rotation);
    }
}
