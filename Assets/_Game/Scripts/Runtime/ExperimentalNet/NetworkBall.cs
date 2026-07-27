using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 공을 호스트(서버) 권한으로 만든다.
///
/// 공은 누가 잡느냐에 따라 물리 상태가 통째로 바뀌고(소유 중엔 kinematic, 놓으면 물리 복귀),
/// 각자 클라이언트가 "가까우니 내가 잡았다"를 따로 판단하면 서로 다른 사람이 동시에 공을 갖게 된다.
/// 그래서 공에 관한 판단은 전부 서버에서만 하고, 클라이언트는 결과만 받는다.
///  - 위치/회전: NetworkTransform이 서버 기준으로 복제
///  - 누가 갖고 있는지: 이 컴포넌트가 NetworkVariable로 복제
///
/// 클라이언트에서 공을 바꾸려는 시도는 BallController 쪽에서 전부 막는다(LocalHasAuthority).
/// 오프라인(싱글) 플레이에서는 네트워크가 없으므로 모든 권한이 로컬에 있다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(BallController))]
public class NetworkBall : NetworkBehaviour
{
    /// <summary>씬에 있는 공의 네트워크 계층. 오프라인 플레이에서는 스폰되지 않는다.</summary>
    public static NetworkBall Instance { get; private set; }

    /// <summary>
    /// 지금 이 프로세스가 공 상태를 바꿔도 되는지.
    /// 오프라인이면 항상 true, 온라인이면 서버(호스트)만 true.
    /// </summary>
    public static bool LocalHasAuthority =>
        Instance == null || !Instance.IsSpawned || Instance.IsServer;

    /// <summary>공을 가진 선수의 NetworkObjectId. 0이면 무소유.</summary>
    private readonly NetworkVariable<ulong> ownerPlayerObjectId = new NetworkVariable<ulong>(0);

    private BallController ball;

    private void Awake()
    {
        ball = GetComponent<BallController>();
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnNetworkSpawn()
    {
        ownerPlayerObjectId.OnValueChanged += HandleOwnerChanged;

        // 늦게 접속한 사람도 현재 소유 상태를 곧바로 반영해야 한다.
        if (!IsServer)
            ApplyReplicatedOwner(ownerPlayerObjectId.Value);
    }

    public override void OnNetworkDespawn()
    {
        ownerPlayerObjectId.OnValueChanged -= HandleOwnerChanged;
    }

    /// <summary>서버가 공 소유가 바뀔 때마다 호출해 모든 클라에 알린다.</summary>
    public void ServerPublishOwner(PlayerBallHandler owner)
    {
        if (!IsServer || !IsSpawned) return;

        ownerPlayerObjectId.Value = ResolveObjectId(owner);
    }

    private static ulong ResolveObjectId(PlayerBallHandler owner)
    {
        if (owner == null) return 0;

        NetworkObject netObject = owner.GetComponentInParent<NetworkObject>();
        return netObject != null && netObject.IsSpawned ? netObject.NetworkObjectId : 0;
    }

    private void HandleOwnerChanged(ulong previous, ulong current)
    {
        if (IsServer) return; // 서버는 이미 실제 상태를 갖고 있다

        ApplyReplicatedOwner(current);
    }

    /// <summary>복제받은 소유자를 로컬 공 상태에 반영한다(물리는 건드리지 않는다).</summary>
    private void ApplyReplicatedOwner(ulong objectId)
    {
        ball.MirrorOwner(FindHandler(objectId));
    }

    private static PlayerBallHandler FindHandler(ulong objectId)
    {
        if (objectId == 0 || NetworkManager.Singleton == null)
            return null;

        return NetworkManager.Singleton.SpawnManager.SpawnedObjects
                   .TryGetValue(objectId, out NetworkObject netObject) && netObject != null
            ? netObject.GetComponent<PlayerBallHandler>()
            : null;
    }
}
