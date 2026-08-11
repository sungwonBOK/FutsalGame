using System;

/// <summary>
/// A WebRTC setup message addressed to exactly one active NGO client.
/// The signaling transport may relay this envelope through the Host, but the
/// sender and recipient always identify the direct WebRTC pair.
/// </summary>
public readonly struct P2pPeerSignal
{
    public ulong SenderClientId { get; }
    public ulong RecipientClientId { get; }
    public P2pSignalMessage Signal { get; }

    private P2pPeerSignal(ulong senderClientId, ulong recipientClientId, P2pSignalMessage signal)
    {
        SenderClientId = senderClientId;
        RecipientClientId = recipientClientId;
        Signal = signal;
    }

    public static bool TryCreate(
        ulong senderClientId,
        ulong recipientClientId,
        P2pSignalMessage signal,
        out P2pPeerSignal peerSignal)
    {
        peerSignal = default;
        if (senderClientId == recipientClientId)
            return false;

        peerSignal = new P2pPeerSignal(senderClientId, recipientClientId, signal);
        return true;
    }
}
