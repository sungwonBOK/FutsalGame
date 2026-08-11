using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns this client's direct WebRTC links. Each coordinator represents exactly
/// one remote NGO client; gameplay callers use broadcast/target methods and
/// never need to know the signaling or room-service implementation.
/// </summary>
[DisallowMultipleComponent]
public sealed class P2pPeerConnectionRegistry : MonoBehaviour
{
    private static readonly P2pGameplayChannel RequiredGameplayChannels =
        P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat | P2pGameplayChannel.Ball;

    private readonly Dictionary<ulong, P2pConnectionCoordinator> connections =
        new Dictionary<ulong, P2pConnectionCoordinator>();
    private readonly Dictionary<ulong, PeerEventHandlers> handlersByPeer =
        new Dictionary<ulong, PeerEventHandlers>();
    private readonly P2pPeerMeshPolicy meshPolicy = new P2pPeerMeshPolicy(RequiredGameplayChannels);

    private ulong localClientId;
    private bool isConfigured;

    public static P2pPeerConnectionRegistry Current { get; private set; }
    public ulong LocalClientId => localClientId;
    public bool IsGameplayReady => meshPolicy.IsGameplayReady;
    public int RequiredPeerCount => meshPolicy.RequiredPeerCount;

    public event Action<P2pPeerSignal> SignalReady;
    public event Action<ulong, P2pConnectionState, string> PeerStateChanged;
    public event Action GameplayReadinessChanged;
    public event Action<ulong, byte[]> SnapshotReceived;
    public event Action<ulong, byte[]> CombatReceived;
    public event Action<ulong, byte[]> BallStateReceived;
    public event Action<ulong, byte[]> BallEventReceived;

    private void Awake()
    {
        Current = this;
    }

    public void Configure(ulong localId, IEnumerable<ulong> requiredPeerClientIds)
    {
        if (requiredPeerClientIds == null)
            throw new ArgumentNullException(nameof(requiredPeerClientIds));

        if (isConfigured && localClientId != localId)
            throw new InvalidOperationException("The local client ID cannot change during a P2P session.");

        localClientId = localId;
        isConfigured = true;

        HashSet<ulong> nextPeers = new HashSet<ulong>();
        foreach (ulong peerClientId in requiredPeerClientIds)
        {
            if (peerClientId != localClientId)
                nextPeers.Add(peerClientId);
        }

        RemoveStalePeers(nextPeers);
        foreach (ulong peerClientId in nextPeers)
            EnsurePeer(peerClientId);

        meshPolicy.SetRequiredPeers(nextPeers);
        RefreshReadiness();
    }

    /// <summary>
    /// Announces that this peer has configured its signal receiver for every
    /// required remote. Negotiation starts only when the matching Ready signal
    /// arrives, preventing an offer from racing a late-joining client's NGO
    /// message handler.
    /// </summary>
    public void SendReadyForRequiredPeers()
    {
        EnsureConfigured();
        if (!P2pSignalMessage.TryCreate(P2pSignalKind.Ready, "ready", out P2pSignalMessage ready))
            throw new InvalidOperationException("Could not create the P2P readiness signal.");

        foreach (ulong peerClientId in connections.Keys)
        {
            if (P2pPeerSignal.TryCreate(localClientId, peerClientId, ready, out P2pPeerSignal signal))
                SignalReady?.Invoke(signal);
        }
    }

    public bool ReceiveSignal(P2pPeerSignal signal)
    {
        EnsureConfigured();
        if (signal.RecipientClientId != localClientId
            || signal.SenderClientId == localClientId
            || !connections.TryGetValue(signal.SenderClientId, out P2pConnectionCoordinator connection))
        {
            return false;
        }

        if (signal.Signal.Kind == P2pSignalKind.Ready)
        {
            if (connection.State == P2pConnectionState.Idle
                || connection.State == P2pConnectionState.Failed
                || connection.State == P2pConnectionState.Closed)
            {
                connection.Begin(P2pOfferSelector.IsLocalOfferer(localClientId, signal.SenderClientId));
            }

            return true;
        }

        connection.ReceiveSignal(signal.Signal);
        return true;
    }

    public bool TryBroadcast(P2pGameplayChannel channel, byte[] payload)
    {
        EnsureConfigured();
        foreach (KeyValuePair<ulong, P2pConnectionCoordinator> pair in connections)
        {
            if (!TrySend(pair.Value, channel, payload))
                return false;
        }

        return true;
    }

    public bool TrySendTo(ulong peerClientId, P2pGameplayChannel channel, byte[] payload)
    {
        EnsureConfigured();
        return connections.TryGetValue(peerClientId, out P2pConnectionCoordinator connection)
            && TrySend(connection, channel, payload);
    }

    public bool TryBroadcastBallState(byte[] payload)
    {
        EnsureConfigured();
        foreach (KeyValuePair<ulong, P2pConnectionCoordinator> pair in connections)
        {
            if (!pair.Value.TrySendBallState(payload))
                return false;
        }

        return true;
    }

    public bool TryBroadcastBallEvent(byte[] payload)
    {
        EnsureConfigured();
        foreach (KeyValuePair<ulong, P2pConnectionCoordinator> pair in connections)
        {
            if (!pair.Value.TrySendBallEvent(payload))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Sends a recovery-critical ball event to every gameplay-ready peer. Unlike a normal
    /// broadcast, an already failed link is skipped so a surviving mesh can converge after
    /// its previous authority disconnects.
    /// </summary>
    public bool TryBroadcastBallEventToReadyPeers(byte[] payload)
    {
        EnsureConfigured();
        foreach (KeyValuePair<ulong, P2pConnectionCoordinator> pair in connections)
        {
            if (!pair.Value.IsGameplayReady)
                continue;

            if (!pair.Value.TrySendBallEvent(payload))
                return false;
        }

        return true;
    }

    public bool ContainsPeer(ulong peerClientId)
    {
        return connections.ContainsKey(peerClientId);
    }

    public bool IsPeerGameplayReady(ulong peerClientId)
    {
        return connections.TryGetValue(peerClientId, out P2pConnectionCoordinator connection)
            && connection.IsGameplayReady;
    }

    public void Shutdown()
    {
        foreach (KeyValuePair<ulong, P2pConnectionCoordinator> pair in connections)
            pair.Value.Shutdown();
    }

    private void EnsurePeer(ulong peerClientId)
    {
        if (connections.ContainsKey(peerClientId))
            return;

        P2pConnectionCoordinator connection = gameObject.AddComponent<P2pConnectionCoordinator>();
        connection.ConfigureRemotePeer(peerClientId);

        PeerEventHandlers handlers = new PeerEventHandlers(
            signal => HandleSignalReady(peerClientId, signal),
            (state, message) => HandlePeerStateChanged(peerClientId, state, message),
            _ => HandleGameplayChannelsChanged(peerClientId),
            payload => SnapshotReceived?.Invoke(peerClientId, payload),
            payload => CombatReceived?.Invoke(peerClientId, payload),
            payload => BallStateReceived?.Invoke(peerClientId, payload),
            payload => BallEventReceived?.Invoke(peerClientId, payload));
        handlers.Subscribe(connection);

        connections.Add(peerClientId, connection);
        handlersByPeer.Add(peerClientId, handlers);
    }

    private void RemoveStalePeers(HashSet<ulong> nextPeers)
    {
        List<ulong> stalePeers = null;
        foreach (ulong peerClientId in connections.Keys)
        {
            if (!nextPeers.Contains(peerClientId))
                (stalePeers ??= new List<ulong>()).Add(peerClientId);
        }

        if (stalePeers == null)
            return;

        foreach (ulong peerClientId in stalePeers)
        {
            P2pConnectionCoordinator connection = connections[peerClientId];
            handlersByPeer[peerClientId].Unsubscribe(connection);
            handlersByPeer.Remove(peerClientId);
            connections.Remove(peerClientId);
            connection.Shutdown();
            Destroy(connection);
        }
    }

    private void HandleSignalReady(ulong peerClientId, P2pSignalMessage message)
    {
        if (P2pPeerSignal.TryCreate(localClientId, peerClientId, message, out P2pPeerSignal signal))
            SignalReady?.Invoke(signal);
    }

    private void HandlePeerStateChanged(ulong peerClientId, P2pConnectionState state, string message)
    {
        meshPolicy.SetOpenChannels(peerClientId, P2pGameplayChannel.None);
        PeerStateChanged?.Invoke(peerClientId, state, message);
        RefreshReadiness();
    }

    private void HandleGameplayChannelsChanged(ulong peerClientId)
    {
        if (connections.TryGetValue(peerClientId, out P2pConnectionCoordinator connection))
            meshPolicy.SetOpenChannels(peerClientId, connection.OpenGameplayChannels);

        RefreshReadiness();
    }

    private void RefreshReadiness()
    {
        GameplayReadinessChanged?.Invoke();
    }

    private static bool TrySend(P2pConnectionCoordinator connection, P2pGameplayChannel channel, byte[] payload)
    {
        switch (channel)
        {
            case P2pGameplayChannel.Snapshot:
                return connection.TrySendSnapshot(payload);
            case P2pGameplayChannel.Combat:
                return connection.TrySendCombat(payload);
            case P2pGameplayChannel.Ball:
                return connection.TrySendBallEvent(payload);
            default:
                return false;
        }
    }

    private void EnsureConfigured()
    {
        if (!isConfigured)
            throw new InvalidOperationException("Configure the P2P peer registry before using it.");
    }

    private void OnDestroy()
    {
        Shutdown();
        if (Current == this)
            Current = null;
    }

    private sealed class PeerEventHandlers
    {
        private readonly Action<P2pSignalMessage> signalReady;
        private readonly Action<P2pConnectionState, string> stateChanged;
        private readonly Action<P2pGameplayChannel> channelsChanged;
        private readonly Action<byte[]> snapshotReceived;
        private readonly Action<byte[]> combatReceived;
        private readonly Action<byte[]> ballStateReceived;
        private readonly Action<byte[]> ballEventReceived;

        public PeerEventHandlers(
            Action<P2pSignalMessage> signalReady,
            Action<P2pConnectionState, string> stateChanged,
            Action<P2pGameplayChannel> channelsChanged,
            Action<byte[]> snapshotReceived,
            Action<byte[]> combatReceived,
            Action<byte[]> ballStateReceived,
            Action<byte[]> ballEventReceived)
        {
            this.signalReady = signalReady;
            this.stateChanged = stateChanged;
            this.channelsChanged = channelsChanged;
            this.snapshotReceived = snapshotReceived;
            this.combatReceived = combatReceived;
            this.ballStateReceived = ballStateReceived;
            this.ballEventReceived = ballEventReceived;
        }

        public void Subscribe(P2pConnectionCoordinator connection)
        {
            connection.SignalReady += signalReady;
            connection.StateChanged += stateChanged;
            connection.GameplayChannelsChanged += channelsChanged;
            connection.SnapshotReceived += snapshotReceived;
            connection.CombatReceived += combatReceived;
            connection.BallStateReceived += ballStateReceived;
            connection.BallEventReceived += ballEventReceived;
        }

        public void Unsubscribe(P2pConnectionCoordinator connection)
        {
            connection.SignalReady -= signalReady;
            connection.StateChanged -= stateChanged;
            connection.GameplayChannelsChanged -= channelsChanged;
            connection.SnapshotReceived -= snapshotReceived;
            connection.CombatReceived -= combatReceived;
            connection.BallStateReceived -= ballStateReceived;
            connection.BallEventReceived -= ballEventReceived;
        }
    }
}
