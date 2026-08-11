using System.Collections.Generic;

/// <summary>
/// Keeps only a valid Ready signal that arrived before the local registry had
/// created that sender's coordinator. SDP and ICE messages are never buffered.
/// </summary>
public sealed class P2pPeerReadySignalBuffer
{
    private readonly HashSet<ulong> peerClientIds = new HashSet<ulong>();

    public bool TryRemember(P2pPeerSignal signal, ulong localClientId)
    {
        if (signal.RecipientClientId != localClientId
            || signal.SenderClientId == localClientId
            || signal.Signal.Kind != P2pSignalKind.Ready)
        {
            return false;
        }

        peerClientIds.Add(signal.SenderClientId);
        return true;
    }

    public bool Consume(ulong peerClientId)
    {
        return peerClientIds.Remove(peerClientId);
    }

    public void RetainOnly(IEnumerable<ulong> activePeerClientIds)
    {
        HashSet<ulong> activePeerIds = new HashSet<ulong>(activePeerClientIds);
        peerClientIds.RemoveWhere(peerClientId => !activePeerIds.Contains(peerClientId));
    }

    public void Clear()
    {
        peerClientIds.Clear();
    }
}
