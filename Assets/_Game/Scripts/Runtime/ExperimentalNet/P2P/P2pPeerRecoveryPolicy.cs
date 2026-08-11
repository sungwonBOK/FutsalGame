using System;
using System.Collections.Generic;

/// <summary>
/// Pure recovery decisions shared by presentation code. An NGO control-plane
/// leave is closed, while a failed direct link keeps the participant present
/// and therefore frozen until the full mesh is re-established.
/// </summary>
public static class P2pPeerRecoveryPolicy
{
    public static bool ShouldFreeze(P2pConnectionState state)
    {
        return state == P2pConnectionState.Failed;
    }

    public static bool CanResume(P2pConnectionState state, bool isMeshReady)
    {
        return state == P2pConnectionState.Ready && isMeshReady;
    }
}

/// <summary>
/// Mirrors Host-approved recovery client IDs from the NGO control plane into a
/// gameplay-only lookup. P2P consumers never need to import MPS or Relay code.
/// </summary>
public static class P2pPeerRecoveryApprovals
{
    private static readonly HashSet<ulong> approvedPeerClientIds = new HashSet<ulong>();

    public static event Action Changed;

    public static bool IsApproved(ulong clientId)
    {
        return approvedPeerClientIds.Contains(clientId);
    }

    public static void SetApprovedPeerClientIds(IEnumerable<ulong> clientIds)
    {
        approvedPeerClientIds.Clear();
        if (clientIds != null)
        {
            foreach (ulong clientId in clientIds)
                approvedPeerClientIds.Add(clientId);
        }

        Changed?.Invoke();
    }
}
