using UnityEngine;

/// <summary>
/// Routes human-player movement through the direct mesh snapshot channel.
/// Each remote player accepts packets only from the peer that owns it.
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
    private P2pPeerConnectionRegistry connections;
    private ushort nextSequence;
    private float nextSnapshotAt;
    private bool isFrozen;
    private bool hasLastReceivedPose;
    private Vector3 lastReceivedPosition;
    private float lastReceivedYawDegrees;

    private void Awake()
    {
        playerAgent = GetComponent<NetworkPlayerAgent>();
        ngoTransform = GetComponent<ClientNetworkTransform>();
        body = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        P2pPeerRecoveryApprovals.Changed += HandleRecoveryApprovalsChanged;
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
            connections.TryBroadcast(P2pGameplayChannel.Snapshot, payload);
    }

    private void FixedUpdate()
    {
        if (isFrozen)
        {
            ApplyFrozenPose();
            return;
        }

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
        P2pPeerRecoveryApprovals.Changed -= HandleRecoveryApprovalsChanged;
        if (connections != null)
        {
            connections.SnapshotReceived -= ReceiveSnapshot;
            connections.PeerStateChanged -= HandlePeerStateChanged;
            connections.GameplayReadinessChanged -= HandleGameplayReadinessChanged;
        }

        connections = null;
        if (ngoTransform != null)
            ngoTransform.enabled = true;
    }

    private void RefreshConnection()
    {
        P2pPeerConnectionRegistry current = P2pPeerConnectionRegistry.Current;
        if (connections == current)
            return;

        if (connections != null)
        {
            connections.SnapshotReceived -= ReceiveSnapshot;
            connections.PeerStateChanged -= HandlePeerStateChanged;
            connections.GameplayReadinessChanged -= HandleGameplayReadinessChanged;
        }

        connections = current;
        remoteSnapshots.Clear();

        if (connections != null)
        {
            connections.SnapshotReceived += ReceiveSnapshot;
            connections.PeerStateChanged += HandlePeerStateChanged;
            connections.GameplayReadinessChanged += HandleGameplayReadinessChanged;
        }
    }

    private void ReceiveSnapshot(ulong senderClientId, byte[] payload)
    {
        if (!IsRemoteHumanPlayer()
            || playerAgent.OwnerClientId != senderClientId
            || !P2pSnapshotCodec.TryDecode(payload, out P2pPlayerSnapshot snapshot))
            return;

        remoteSnapshots.TryAccept(snapshot);
        hasLastReceivedPose = true;
        lastReceivedPosition = snapshot.Position;
        lastReceivedYawDegrees = snapshot.YawDegrees;
    }

    private void HandlePeerStateChanged(ulong peerClientId, P2pConnectionState state, string message)
    {
        if (!IsRemoteHumanPlayer() || playerAgent.OwnerClientId != peerClientId)
            return;

        if (P2pPeerRecoveryPolicy.ShouldFreeze(state))
            FreezeAtLastReceivedPose();
        else if (P2pPeerRecoveryPolicy.CanResume(state, CanResumePeer()))
            ResumeFromFrozenPose();
    }

    private void HandleGameplayReadinessChanged()
    {
        if (isFrozen && CanResumePeer())
            ResumeFromFrozenPose();
    }

    private void HandleRecoveryApprovalsChanged()
    {
        if (isFrozen && CanResumePeer())
            ResumeFromFrozenPose();
    }

    private bool CanResumePeer()
    {
        return connections != null
            && connections.IsGameplayReady
            && playerAgent != null
            && P2pPeerRecoveryApprovals.IsApproved(playerAgent.OwnerClientId);
    }

    private void FreezeAtLastReceivedPose()
    {
        isFrozen = true;
        if (!hasLastReceivedPose)
        {
            lastReceivedPosition = transform.position;
            lastReceivedYawDegrees = transform.eulerAngles.y;
        }

        ApplyFrozenPose();
    }

    private void ResumeFromFrozenPose()
    {
        if (!isFrozen)
            return;

        isFrozen = false;
        ApplyFrozenPose();
    }

    private void ApplyFrozenPose()
    {
        Quaternion rotation = Quaternion.Euler(0f, lastReceivedYawDegrees, 0f);
        if (body != null && !body.isKinematic)
        {
            body.position = lastReceivedPosition;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            return;
        }

        transform.SetPositionAndRotation(lastReceivedPosition, rotation);
    }

    private bool ShouldUseDirectP2p()
    {
        return playerAgent != null
            && playerAgent.IsSpawned
            && connections != null
            && connections.IsGameplayReady
            && (playerAgent.IsLocalHumanPlayer || IsRemoteHumanPlayer());
    }

    private bool IsRemoteHumanPlayer()
    {
        return playerAgent != null && !playerAgent.IsOwner && !playerAgent.IsAIControlled;
    }
}
