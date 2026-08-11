using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Routes human combat through the reliable direct mesh. The defending local
/// player resolves an interaction exactly once; NGO remains the fallback while
/// the mesh is unavailable.
/// </summary>
[DisallowMultipleComponent]
public sealed class P2pCombatReplicator : MonoBehaviour
{
    private const float ResolvedIdRetentionSeconds = 8f;

    private readonly Dictionary<uint, PendingAction> pendingActions = new Dictionary<uint, PendingAction>();
    private readonly Dictionary<uint, P2pCombatMessage> resolvedRequests = new Dictionary<uint, P2pCombatMessage>();
    private readonly Dictionary<uint, float> resolvedRequestExpiresAt = new Dictionary<uint, float>();
    private readonly Dictionary<uint, float> retainedIds = new Dictionary<uint, float>();

    private NetworkPlayerAgent playerAgent;
    private CombatController combat;
    private P2pPresentationDispatcher presentation;
    private P2pPeerConnectionRegistry connections;
    private uint nextActionId;
    private ushort nextSequence;
    private uint activeGrabId;

    public bool IsReady => connections != null && connections.IsGameplayReady && IsHumanMatchPlayer();
    public bool HasActiveLocalGrab => activeGrabId != 0;

    private void Awake()
    {
        playerAgent = GetComponent<NetworkPlayerAgent>();
        combat = GetComponent<CombatController>();
        presentation = GetComponent<P2pPresentationDispatcher>();
    }

    private void Update()
    {
        RefreshConnection();
        PruneRetainedIds();

        if (!IsReady)
        {
            pendingActions.Clear();
            return;
        }

        if (!playerAgent.IsLocalHumanPlayer)
            return;

        List<uint> expired = null;
        List<uint> requestsToSend = null;
        foreach (KeyValuePair<uint, PendingAction> pair in pendingActions)
        {
            PendingAction pending = pair.Value;
            if (Time.time >= pending.ExpiresAt)
            {
                (expired ??= new List<uint>()).Add(pair.Key);
                continue;
            }

            if (pending.RequestSent || Time.time < pending.ContactAt)
                continue;

            if (!combat.TryFindP2pInteractionTarget(pending.ActionKind, pending.Direction, out CharacterState target)
                || !TryGetOwnerClientId(target, out ulong targetClientId))
                continue;

            pending.TargetClientId = targetClientId;
            pendingActions[pair.Key] = pending;
            (requestsToSend ??= new List<uint>()).Add(pair.Key);
        }

        if (requestsToSend != null)
        {
            foreach (uint actionId in requestsToSend)
            {
                PendingAction pending = pendingActions[actionId];
                pending.RequestSent = true;
                pendingActions[actionId] = pending;
                SendTo(pending.TargetClientId, new P2pCombatMessage(
                    P2pCombatMessageKind.InteractionRequest,
                    actionId,
                    nextSequence++,
                    pending.ActionKind,
                    P2pCombatResolution.Hit,
                    pending.Origin,
                    pending.Direction));
            }
        }

        if (expired != null)
        {
            foreach (uint actionId in expired)
                pendingActions.Remove(actionId);
        }
    }

    private void OnDisable()
    {
        if (connections != null)
            connections.CombatReceived -= ReceiveCombat;

        connections = null;
        pendingActions.Clear();
    }

    public bool TryBeginLocalAction(P2pCombatActionKind actionKind, Vector3 direction, float contactDelay, float lifetime)
    {
        RefreshConnection();
        if (!IsReady || !playerAgent.IsLocalHumanPlayer)
            return false;

        uint actionId = ++nextActionId;
        if (actionId == 0)
            actionId = ++nextActionId;

        Vector3 normalizedDirection = CharacterMovementUtility.FlattenOrFallback(direction, transform.forward);
        PendingAction pending = new PendingAction
        {
            ActionKind = actionKind,
            Origin = transform.position,
            Direction = normalizedDirection,
            ContactAt = Time.time + Mathf.Max(0f, contactDelay),
            ExpiresAt = Time.time + Mathf.Max(contactDelay, lifetime)
        };
        pendingActions.Add(actionId, pending);
        if (!Send(new P2pCombatMessage(
                P2pCombatMessageKind.ActionStart,
                actionId,
                nextSequence++,
                actionKind,
                P2pCombatResolution.Hit,
                pending.Origin,
                pending.Direction)))
        {
            pendingActions.Remove(actionId);
            return false;
        }

        if (actionKind != P2pCombatActionKind.PowerStun)
        {
            presentation?.TryPresent(new P2pPresentationRequest(
                actionId,
                P2pPresentationRouting.FromCombat(actionKind),
                pending.Origin));
        }
        return true;
    }

    public bool TryCancelPendingLocalAction(uint actionId)
    {
        if (!pendingActions.TryGetValue(actionId, out PendingAction pending) || pending.RequestSent)
            return false;

        if (presentation == null || !presentation.TryCancel(actionId))
            return false;

        if (!Send(new P2pCombatMessage(
                P2pCombatMessageKind.ActionCancel,
                actionId,
                nextSequence++,
                pending.ActionKind,
                P2pCombatResolution.Hit,
                pending.Origin,
                pending.Direction)))
        {
            return false;
        }

        pendingActions.Remove(actionId);
        return true;
    }

    public void SendGrabReleased()
    {
        if (activeGrabId == 0)
            return;

        Send(new P2pCombatMessage(
            P2pCombatMessageKind.GrabReleased,
            activeGrabId,
            nextSequence++,
            P2pCombatActionKind.Grab,
            P2pCombatResolution.Hit,
            transform.position,
            transform.forward));
        activeGrabId = 0;
    }

    private void RefreshConnection()
    {
        P2pPeerConnectionRegistry current = P2pPeerConnectionRegistry.Current;
        if (connections == current)
            return;

        if (connections != null)
            connections.CombatReceived -= ReceiveCombat;

        connections = current;
        pendingActions.Clear();
        if (connections != null)
            connections.CombatReceived += ReceiveCombat;
    }

    private void ReceiveCombat(ulong senderClientId, byte[] payload)
    {
        if (!P2pCombatCodec.TryDecode(payload, out P2pCombatMessage message))
            return;

        bool isFromThisRemotePlayer = IsRemoteHumanPlayer() && playerAgent.OwnerClientId == senderClientId;
        bool isForLocalPlayer = IsLocalHumanPlayer();

        switch (message.Kind)
        {
            case P2pCombatMessageKind.ActionStart:
                if (isFromThisRemotePlayer && message.ActionKind != P2pCombatActionKind.PowerStun)
                {
                    presentation?.TryPresent(new P2pPresentationRequest(
                        message.ActionId,
                        P2pPresentationRouting.FromCombat(message.ActionKind),
                        message.Origin));
                }
                break;

            case P2pCombatMessageKind.InteractionRequest:
                if (isForLocalPlayer)
                    ResolveIncomingInteraction(message, senderClientId);
                break;

            case P2pCombatMessageKind.InteractionResult:
                if (isFromThisRemotePlayer && message.Resolution == P2pCombatResolution.Block)
                {
                    presentation?.TryPresent(new P2pPresentationRequest(
                        message.ActionId,
                        P2pPresentationAction.Block,
                        message.Origin));
                }
                if (isForLocalPlayer && !IsRetained(message.ActionId))
                    ReceiveInteractionResult(message);
                break;

            case P2pCombatMessageKind.ActionCancel:
                if (isFromThisRemotePlayer)
                    presentation?.TryCancel(message.ActionId);
                break;

            case P2pCombatMessageKind.GrabStarted:
                if (isForLocalPlayer && message.ActionKind == P2pCombatActionKind.Grab)
                {
                    combat.BeginP2pGrabWithRemote();
                    activeGrabId = message.ActionId;
                }
                break;

            case P2pCombatMessageKind.GrabReleased:
                if (isForLocalPlayer && message.ActionId == activeGrabId)
                {
                    combat.ReleaseP2pGrabWithRemote();
                    activeGrabId = 0;
                }
                break;
        }
    }

    private void ResolveIncomingInteraction(P2pCombatMessage request, ulong attackerClientId)
    {
        if (!IsLocalHumanPlayer())
            return;

        if (resolvedRequests.TryGetValue(request.ActionId, out P2pCombatMessage previous))
        {
            Send(previous);
            return;
        }

        P2pCombatResolution resolution = combat.ResolveP2pInteraction(request.ActionKind, request.Origin);
        P2pCombatMessage result = new P2pCombatMessage(
            P2pCombatMessageKind.InteractionResult,
            request.ActionId,
            nextSequence++,
            request.ActionKind,
            resolution,
            request.Origin,
            request.Direction);
        resolvedRequests.Add(request.ActionId, result);
        resolvedRequestExpiresAt[request.ActionId] = Time.time + ResolvedIdRetentionSeconds;
        Send(result);

        if (request.ActionKind == P2pCombatActionKind.SlideTackle
            && resolution == P2pCombatResolution.Hit
            && BallAuthorityController.Current != null)
        {
            BallAuthorityController.Current.TryResolveTackle(
                request.ActionId,
                request.ActionKind,
                resolution,
                attackerClientId);
        }

        if (request.ActionKind == P2pCombatActionKind.Grab && resolution == P2pCombatResolution.Hit)
        {
            activeGrabId = request.ActionId;
            Send(new P2pCombatMessage(
                P2pCombatMessageKind.GrabStarted,
                request.ActionId,
                nextSequence++,
                request.ActionKind,
                resolution,
                request.Origin,
                request.Direction));
        }
    }

    private void ReceiveInteractionResult(P2pCombatMessage result)
    {
        if (!IsLocalHumanPlayer() || !pendingActions.Remove(result.ActionId))
            return;

        Retain(result.ActionId);
        presentation?.MarkResolved(result.ActionId);
        combat.PlayP2pResultPresentation(result.ActionKind, result.Resolution, result.Origin, result.Direction);
    }

    private bool Send(P2pCombatMessage message)
    {
        return P2pCombatCodec.TryEncode(message, out byte[] payload)
            && connections != null
            && connections.TryBroadcast(P2pGameplayChannel.Combat, payload);
    }

    private bool SendTo(ulong peerClientId, P2pCombatMessage message)
    {
        return P2pCombatCodec.TryEncode(message, out byte[] payload)
            && connections != null
            && connections.TrySendTo(peerClientId, P2pGameplayChannel.Combat, payload);
    }

    private static bool TryGetOwnerClientId(Component component, out ulong ownerClientId)
    {
        ownerClientId = 0;
        NetworkObject networkObject = component != null ? component.GetComponentInParent<NetworkObject>() : null;
        if (networkObject == null || !networkObject.IsSpawned)
            return false;

        ownerClientId = networkObject.OwnerClientId;
        return true;
    }

    private bool IsHumanMatchPlayer()
    {
        return playerAgent != null && playerAgent.IsSpawned && !playerAgent.IsAIControlled;
    }

    private bool IsLocalHumanPlayer()
    {
        return IsHumanMatchPlayer() && playerAgent.IsLocalHumanPlayer;
    }

    private bool IsRemoteHumanPlayer()
    {
        return IsHumanMatchPlayer() && !playerAgent.IsLocalHumanPlayer;
    }

    private bool IsRetained(uint actionId)
    {
        return retainedIds.TryGetValue(actionId, out float expiresAt) && Time.time < expiresAt;
    }

    private void Retain(uint actionId)
    {
        retainedIds[actionId] = Time.time + ResolvedIdRetentionSeconds;
    }

    private void PruneRetainedIds()
    {
        List<uint> expired = null;
        foreach (KeyValuePair<uint, float> pair in retainedIds)
        {
            if (Time.time >= pair.Value)
                (expired ??= new List<uint>()).Add(pair.Key);
        }

        if (expired != null)
        {
            foreach (uint actionId in expired)
                retainedIds.Remove(actionId);
        }

        List<uint> expiredRequests = null;
        foreach (KeyValuePair<uint, float> pair in resolvedRequestExpiresAt)
        {
            if (Time.time >= pair.Value)
                (expiredRequests ??= new List<uint>()).Add(pair.Key);
        }

        if (expiredRequests == null)
            return;

        foreach (uint actionId in expiredRequests)
        {
            resolvedRequestExpiresAt.Remove(actionId);
            resolvedRequests.Remove(actionId);
        }
    }

    private struct PendingAction
    {
        public P2pCombatActionKind ActionKind;
        public Vector3 Origin;
        public Vector3 Direction;
        public float ContactAt;
        public float ExpiresAt;
        public bool RequestSent;
        public ulong TargetClientId;
    }
}
