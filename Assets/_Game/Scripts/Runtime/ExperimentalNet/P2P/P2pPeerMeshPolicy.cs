using System;
using System.Collections.Generic;

/// <summary>
/// Tracks which direct peers are required for this local player and whether
/// each has opened every gameplay channel. It is deliberately independent of
/// WebRTC and NGO so the room policy can be tested without a live transport.
/// </summary>
public sealed class P2pPeerMeshPolicy
{
    private readonly P2pGameplayReadiness gameplayReadiness;
    private readonly HashSet<ulong> requiredPeers = new HashSet<ulong>();
    private readonly Dictionary<ulong, P2pGameplayChannel> openChannelsByPeer =
        new Dictionary<ulong, P2pGameplayChannel>();

    public int RequiredPeerCount => requiredPeers.Count;

    public bool IsGameplayReady
    {
        get
        {
            foreach (ulong peerClientId in requiredPeers)
            {
                if (!openChannelsByPeer.TryGetValue(peerClientId, out P2pGameplayChannel openChannels)
                    || !gameplayReadiness.IsReady(openChannels))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public P2pPeerMeshPolicy(P2pGameplayChannel requiredChannels)
    {
        gameplayReadiness = new P2pGameplayReadiness(requiredChannels);
    }

    public void SetRequiredPeers(IEnumerable<ulong> peerClientIds)
    {
        if (peerClientIds == null)
            throw new ArgumentNullException(nameof(peerClientIds));

        requiredPeers.Clear();
        foreach (ulong peerClientId in peerClientIds)
            requiredPeers.Add(peerClientId);

        List<ulong> stalePeers = null;
        foreach (ulong peerClientId in openChannelsByPeer.Keys)
        {
            if (!requiredPeers.Contains(peerClientId))
                (stalePeers ??= new List<ulong>()).Add(peerClientId);
        }

        if (stalePeers == null)
            return;

        foreach (ulong peerClientId in stalePeers)
            openChannelsByPeer.Remove(peerClientId);
    }

    public void SetOpenChannels(ulong peerClientId, P2pGameplayChannel openChannels)
    {
        if (!requiredPeers.Contains(peerClientId))
            return;

        openChannelsByPeer[peerClientId] = openChannels;
    }

    public bool ContainsRequiredPeer(ulong peerClientId)
    {
        return requiredPeers.Contains(peerClientId);
    }
}
