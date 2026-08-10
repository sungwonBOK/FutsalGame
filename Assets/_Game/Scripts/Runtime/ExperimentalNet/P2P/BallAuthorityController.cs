using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Owns direct-P2P ball authority, authoritative state publishing, and authority handoff.
/// During a dribble the owner is the authority. A free ball retains the last authority
/// until a validated acquire request transfers it to the new owner.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BallController))]
public sealed class BallAuthorityController : MonoBehaviour
{
    private const float StateRateHz = 20f;
    private const float AcquireRequestRetrySeconds = 0.25f;

    public static BallAuthorityController Current { get; private set; }

    private BallController ball;
    private NetworkBall networkBall;
    private Rigidbody body;
    private P2pConnectionCoordinator connection;
    private P2pBallAuthorityState authorityState;
    private bool hasAuthorityState;
    private ushort nextSnapshotSequence;
    private ushort lastReceivedSnapshotSequence;
    private uint nextActionId = 1;
    private float nextStateAt;
    private ulong pendingAcquireFor;
    private float pendingAcquireExpiresAt;

    public bool UsesDirectP2pTransport
    {
        get { return connection != null && connection.IsBallReady; }
    }

    public bool IsDirectP2pActive
    {
        get { return UsesDirectP2pTransport && hasAuthorityState; }
    }

    public bool IsLocalAuthority
    {
        get { return IsDirectP2pActive && authorityState.AuthorityId == ResolveLocalPlayerId(); }
    }

    private void Awake()
    {
        ball = GetComponent<BallController>();
        networkBall = GetComponent<NetworkBall>();
        body = GetComponent<Rigidbody>();
        Current = this;
    }

    private void Update()
    {
        RefreshConnection();
        SeedInitialAuthorityIfNeeded();

        if (pendingAcquireFor != 0 && Time.time >= pendingAcquireExpiresAt)
            pendingAcquireFor = 0;

        if (!IsLocalAuthority || Time.time < nextStateAt)
            return;

        nextStateAt = Time.time + (1f / StateRateHz);
        SendState(BuildState(authorityState.AuthorityId, authorityState.OwnerId, authorityState.Epoch, nextSnapshotSequence++));
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (Current == this)
            Current = null;
    }

    public static bool TryHandleAcquire(BallController targetBall, PlayerBallHandler candidate, out bool acquired)
    {
        acquired = false;
        if (Current == null || Current.ball != targetBall || !Current.IsDirectP2pActive)
            return false;

        acquired = Current.TryAcquire(candidate);
        return true;
    }

    public bool TryPublishLocalAction(P2pBallActionKind actionKind)
    {
        if (!IsLocalAuthority || actionKind == P2pBallActionKind.None || ball.CurrentOwner != null)
            return false;

        P2pBallState released = BuildState(
            authorityState.AuthorityId,
            ownerId: 0,
            authorityState.Epoch,
            nextSnapshotSequence++);
        P2pBallEvent action = new P2pBallEvent(
            P2pBallEventKind.Action,
            actionKind,
            NextActionId(),
            authorityState.AuthorityId,
            released);

        if (!SendEvent(action))
            return false;

        authorityState = new P2pBallAuthorityState(
            authorityState.AuthorityId,
            ownerId: 0,
            authorityState.Epoch);
        return true;
    }

    /// <summary>
    /// Applies the ball consequence of a defender-resolved slide tackle. The existing combat
    /// rule releases the ball, so only authority moves to the attacker; Owner remains empty.
    /// </summary>
    public bool TryResolveTackle(uint combatActionId, P2pCombatActionKind actionKind, P2pCombatResolution resolution)
    {
        if (!IsLocalAuthority
            || actionKind != P2pCombatActionKind.SlideTackle
            || resolution != P2pCombatResolution.Hit
            || authorityState.OwnerId != ResolveLocalPlayerId())
        {
            return false;
        }

        ulong attackerId = ResolveRemoteHumanPlayerId();
        P2pBallAuthorityTransition transition = P2pBallAuthorityPolicy.ResolveTackle(
            authorityState,
            attackerId,
            resolution);
        if (transition.Kind != P2pBallAuthorityTransitionKind.TackleWon)
            return false;

        P2pBallAuthorityState next = transition.State;
        P2pBallState anchor = BuildState(next.AuthorityId, next.OwnerId, next.Epoch, sequence: 0);
        P2pBallEvent transfer = new P2pBallEvent(
            P2pBallEventKind.AuthorityChanged,
            P2pBallActionKind.None,
            combatActionId,
            authorityState.AuthorityId,
            anchor);
        if (!SendEvent(transfer))
            return false;

        authorityState = next;
        nextSnapshotSequence = 1;
        lastReceivedSnapshotSequence = 0;
        ApplyState(anchor, localAuthority: false);
        return true;
    }

    private bool TryAcquire(PlayerBallHandler candidate)
    {
        if (candidate == null || ball.CurrentOwner != null)
            return false;

        ulong candidateId = ResolvePlayerId(candidate);
        if (candidateId == 0)
            return false;

        if (IsLocalAuthority)
            return ConfirmAcquire(candidate, candidateId);

        if (candidateId != ResolveLocalPlayerId() || pendingAcquireFor == candidateId)
            return false;

        P2pBallAcquireRequest request = new P2pBallAcquireRequest(
            NextActionId(),
            candidateId,
            authorityState.Epoch);
        if (!P2pBallAcquireRequestCodec.TryEncode(request, out byte[] payload)
            || connection == null
            || !connection.TrySendBallEvent(payload))
        {
            return false;
        }

        pendingAcquireFor = candidateId;
        pendingAcquireExpiresAt = Time.time + AcquireRequestRetrySeconds;
        return false;
    }

    private bool ConfirmAcquire(PlayerBallHandler candidate, ulong candidateId)
    {
        if (!IsWithinAcquireRange(candidate) || !ball.TryAcquireFromP2pAuthority(candidate))
            return false;

        pendingAcquireFor = 0;
        if (candidateId == authorityState.AuthorityId)
        {
            authorityState = new P2pBallAuthorityState(candidateId, candidateId, authorityState.Epoch);
            return true;
        }

        P2pBallAuthorityState next = new P2pBallAuthorityState(
            candidateId,
            candidateId,
            authorityState.Epoch + 1);
        P2pBallState anchor = BuildState(next.AuthorityId, next.OwnerId, next.Epoch, sequence: 0);
        P2pBallEvent transfer = new P2pBallEvent(
            P2pBallEventKind.AuthorityChanged,
            P2pBallActionKind.None,
            NextActionId(),
            authorityState.AuthorityId,
            anchor);

        if (!SendEvent(transfer))
        {
            ball.ClearOwnerFromP2pAuthority();
            return false;
        }

        authorityState = next;
        nextSnapshotSequence = 1;
        return true;
    }

    private void RefreshConnection()
    {
        P2pConnectionCoordinator current = P2pConnectionCoordinator.Current;
        if (connection == current)
            return;

        Unsubscribe();
        connection = current;
        hasAuthorityState = false;
        pendingAcquireFor = 0;
        nextSnapshotSequence = 0;
        lastReceivedSnapshotSequence = 0;

        if (connection == null)
            return;

        connection.BallStateReceived += ReceiveState;
        connection.BallEventReceived += ReceiveEvent;
    }

    private void Unsubscribe()
    {
        if (connection == null)
            return;

        connection.BallStateReceived -= ReceiveState;
        connection.BallEventReceived -= ReceiveEvent;
        connection = null;
    }

    private void SeedInitialAuthorityIfNeeded()
    {
        if (hasAuthorityState
            || !UsesDirectP2pTransport
            || networkBall == null
            || !networkBall.IsServer)
        {
            return;
        }

        ulong localPlayerId = ResolveLocalPlayerId();
        if (localPlayerId == 0)
            return;

        ulong ownerId = ResolvePlayerId(ball.CurrentOwner);
        P2pBallState initial = BuildState(localPlayerId, ownerId, epoch: 1, sequence: 0);
        P2pBallEvent initialTransfer = new P2pBallEvent(
            P2pBallEventKind.AuthorityChanged,
            P2pBallActionKind.None,
            NextActionId(),
            localPlayerId,
            initial);
        if (!SendEvent(initialTransfer))
            return;

        authorityState = new P2pBallAuthorityState(localPlayerId, ownerId, 1);
        hasAuthorityState = true;
        nextSnapshotSequence = 1;
    }

    private void ReceiveState(byte[] payload)
    {
        if (!P2pBallStateCodec.TryDecode(payload, out P2pBallState state)
            || !hasAuthorityState
            || IsLocalAuthority
            || !P2pBallAuthorityPolicy.ShouldAcceptSnapshot(
                authorityState,
                lastReceivedSnapshotSequence,
                state.AuthorityId,
                state.Epoch,
                state.Sequence))
        {
            return;
        }

        lastReceivedSnapshotSequence = state.Sequence;
        authorityState = new P2pBallAuthorityState(state.AuthorityId, state.OwnerId, state.Epoch);
        ApplyState(state, localAuthority: false);
    }

    private void ReceiveEvent(byte[] payload)
    {
        if (P2pBallEventCodec.TryDecode(payload, out P2pBallEvent message))
        {
            ReceiveBallEvent(message);
            return;
        }

        if (P2pBallAcquireRequestCodec.TryDecode(payload, out P2pBallAcquireRequest request))
            ReceiveAcquireRequest(request);
    }

    private void ReceiveBallEvent(P2pBallEvent message)
    {
        switch (message.Kind)
        {
            case P2pBallEventKind.AuthorityChanged:
                ReceiveAuthorityChanged(message);
                break;
            case P2pBallEventKind.Action:
                ReceiveAction(message);
                break;
        }
    }

    private void ReceiveAuthorityChanged(P2pBallEvent message)
    {
        P2pBallState anchor = message.AnchorState;
        P2pBallAuthorityState next;
        if (!hasAuthorityState)
        {
            bool isInitialTransfer = anchor.Epoch == 1
                && message.SourceAuthorityId == anchor.AuthorityId;
            if (!isInitialTransfer)
                return;

            next = new P2pBallAuthorityState(anchor.AuthorityId, anchor.OwnerId, anchor.Epoch);
        }
        else if (!P2pBallAuthorityPolicy.TryApplyAuthorityTransfer(
                     authorityState,
                     message.SourceAuthorityId,
                     anchor.AuthorityId,
                     anchor.OwnerId,
                     anchor.Epoch,
                     out next))
        {
            return;
        }

        authorityState = next;
        hasAuthorityState = true;
        lastReceivedSnapshotSequence = anchor.Sequence;
        if (IsLocalAuthority)
            nextSnapshotSequence = (ushort)(anchor.Sequence + 1);
        pendingAcquireFor = 0;
        ApplyState(anchor, IsLocalAuthority);
    }

    private void ReceiveAction(P2pBallEvent message)
    {
        P2pBallState state = message.AnchorState;
        if (!hasAuthorityState
            || message.SourceAuthorityId != authorityState.AuthorityId
            || state.AuthorityId != authorityState.AuthorityId
            || state.Epoch != authorityState.Epoch)
        {
            return;
        }

        authorityState = new P2pBallAuthorityState(state.AuthorityId, state.OwnerId, state.Epoch);
        lastReceivedSnapshotSequence = state.Sequence;
        ApplyState(state, IsLocalAuthority);

        if (message.SourceAuthorityId == ResolveLocalPlayerId())
            return;

        PlayerBallHandler actor = FindHandler(message.SourceAuthorityId);
        actor?.GetComponent<P2pPresentationDispatcher>()?.TryPresent(new P2pPresentationRequest(
            message.ActionId,
            P2pPresentationRouting.FromBall(message.ActionKind),
            transform.position));
    }

    private void ReceiveAcquireRequest(P2pBallAcquireRequest request)
    {
        if (!IsLocalAuthority
            || request.ObservedEpoch != authorityState.Epoch
            || ball.CurrentOwner != null)
        {
            return;
        }

        PlayerBallHandler claimant = FindHandler(request.ClaimantId);
        if (claimant != null)
            ConfirmAcquire(claimant, request.ClaimantId);
    }

    private void ApplyState(P2pBallState state, bool localAuthority)
    {
        PlayerBallHandler owner = FindHandler(state.OwnerId);
        ball.ApplyP2pState(
            owner,
            state.Position,
            state.Rotation,
            state.Velocity,
            state.AngularVelocity,
            localAuthority);
    }

    private P2pBallState BuildState(ulong authorityId, ulong ownerId, uint epoch, ushort sequence)
    {
        Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
        Vector3 angularVelocity = body != null ? body.angularVelocity : Vector3.zero;
        return new P2pBallState(
            authorityId,
            ownerId,
            epoch,
            sequence,
            transform.position,
            transform.rotation,
            velocity,
            angularVelocity);
    }

    private bool SendState(P2pBallState state)
    {
        return P2pBallStateCodec.TryEncode(state, out byte[] payload)
            && connection != null
            && connection.TrySendBallState(payload);
    }

    private bool SendEvent(P2pBallEvent message)
    {
        return P2pBallEventCodec.TryEncode(message, out byte[] payload)
            && connection != null
            && connection.TrySendBallEvent(payload);
    }

    private bool IsWithinAcquireRange(PlayerBallHandler candidate)
    {
        Vector3 playerPosition = candidate.transform.position;
        Vector3 ballPosition = transform.position;
        playerPosition.y = 0f;
        ballPosition.y = 0f;
        float range = ball.OwnerMaxDistance;
        return (playerPosition - ballPosition).sqrMagnitude <= range * range;
    }

    private uint NextActionId()
    {
        uint next = nextActionId++;
        if (next == 0)
            next = nextActionId++;
        return next;
    }

    private static ulong ResolvePlayerId(PlayerBallHandler handler)
    {
        if (handler == null)
            return 0;

        NetworkObject networkObject = handler.GetComponentInParent<NetworkObject>();
        return networkObject != null && networkObject.IsSpawned ? networkObject.NetworkObjectId : 0;
    }

    private static PlayerBallHandler FindHandler(ulong objectId)
    {
        if (objectId == 0 || NetworkManager.Singleton == null)
            return null;

        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject networkObject)
            ? networkObject.GetComponent<PlayerBallHandler>()
            : null;
    }

    private static ulong ResolveLocalPlayerId()
    {
        NetworkPlayerAgent[] agents = FindObjectsByType<NetworkPlayerAgent>();
        foreach (NetworkPlayerAgent agent in agents)
        {
            if (agent != null && agent.IsLocalHumanPlayer)
                return agent.NetworkObjectId;
        }

        return 0;
    }

    private static ulong ResolveRemoteHumanPlayerId()
    {
        NetworkPlayerAgent[] agents = FindObjectsByType<NetworkPlayerAgent>();
        foreach (NetworkPlayerAgent agent in agents)
        {
            if (agent != null
                && agent.IsSpawned
                && !agent.IsAIControlled
                && !agent.IsLocalHumanPlayer)
            {
                return agent.NetworkObjectId;
            }
        }

        return 0;
    }
}
