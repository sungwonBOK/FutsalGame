using UnityEngine;

/// <summary>
/// Routes only 1:1 human-player movement through the direct snapshot channel.
/// Ball, combat, match state, and AI retain their existing NGO paths.
/// </summary>
[DisallowMultipleComponent]
public sealed class P2pMovementReplicator : MonoBehaviour
{
    private const float SnapshotRateHz = 20f;
    private const float RemoteInterpolationSpeed = 12f;

    private readonly P2pRemoteSnapshotBuffer remoteSnapshots = new P2pRemoteSnapshotBuffer();

    private NetworkPlayerAgent playerAgent;
    private ClientNetworkTransform ngoTransform;
    private Rigidbody body;
    private P2pConnectionCoordinator connection;
    private ushort nextSequence;
    private float nextSnapshotAt;

    private void Awake()
    {
        playerAgent = GetComponent<NetworkPlayerAgent>();
        ngoTransform = GetComponent<ClientNetworkTransform>();
        body = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        RefreshConnection();
        bool useDirectP2p = ShouldUseDirectP2p();

        if (ngoTransform != null)
            ngoTransform.enabled = !useDirectP2p;

        if (!useDirectP2p || !playerAgent.IsLocalHumanPlayer || Time.time < nextSnapshotAt)
            return;

        nextSnapshotAt = Time.time + (1f / SnapshotRateHz);
        P2pPlayerSnapshot snapshot = new P2pPlayerSnapshot(
            nextSequence++,
            transform.position,
            transform.eulerAngles.y);

        if (P2pSnapshotCodec.TryEncode(snapshot, out byte[] payload))
            connection.TrySendSnapshot(payload);
    }

    private void FixedUpdate()
    {
        if (!ShouldUseDirectP2p() || !IsRemoteHumanPlayer() || !remoteSnapshots.HasSnapshot)
            return;

        P2pSnapshotPresentation.Step(
            transform.position,
            transform.eulerAngles.y,
            remoteSnapshots.Latest,
            RemoteInterpolationSpeed * Time.fixedDeltaTime,
            out Vector3 position,
            out float yawDegrees);

        Quaternion rotation = Quaternion.Euler(0f, yawDegrees, 0f);
        if (body != null && !body.isKinematic)
        {
            body.MovePosition(position);
            body.MoveRotation(rotation);
            return;
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    private void OnDisable()
    {
        if (connection != null)
            connection.SnapshotReceived -= ReceiveSnapshot;

        connection = null;
        if (ngoTransform != null)
            ngoTransform.enabled = true;
    }

    private void RefreshConnection()
    {
        P2pConnectionCoordinator current = P2pConnectionCoordinator.Current;
        if (connection == current)
            return;

        if (connection != null)
            connection.SnapshotReceived -= ReceiveSnapshot;

        connection = current;
        remoteSnapshots.Clear();

        if (connection != null)
            connection.SnapshotReceived += ReceiveSnapshot;
    }

    private void ReceiveSnapshot(byte[] payload)
    {
        if (!IsRemoteHumanPlayer() || !P2pSnapshotCodec.TryDecode(payload, out P2pPlayerSnapshot snapshot))
            return;

        remoteSnapshots.TryAccept(snapshot);
    }

    private bool ShouldUseDirectP2p()
    {
        return playerAgent != null
            && playerAgent.IsSpawned
            && connection != null
            && connection.IsReady
            && (playerAgent.IsLocalHumanPlayer || IsRemoteHumanPlayer());
    }

    private bool IsRemoteHumanPlayer()
    {
        return playerAgent != null && !playerAgent.IsOwner && !playerAgent.IsAIControlled;
    }
}
