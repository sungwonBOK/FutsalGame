using System;

public enum P2pSignalKind : byte
{
    Ready,
    Offer,
    Answer,
    Candidate
}

public readonly struct P2pSignalMessage
{
    public const int MaxPayloadCharacters = 16384;

    public P2pSignalKind Kind { get; }
    public string Payload { get; }

    private P2pSignalMessage(P2pSignalKind kind, string payload)
    {
        Kind = kind;
        Payload = payload;
    }

    public static bool TryCreate(P2pSignalKind kind, string payload, out P2pSignalMessage message)
    {
        message = default;

        if (!Enum.IsDefined(typeof(P2pSignalKind), kind) || string.IsNullOrEmpty(payload) || payload.Length > MaxPayloadCharacters)
            return false;

        message = new P2pSignalMessage(kind, payload);
        return true;
    }
}

public static class P2pOfferSelector
{
    public static bool IsLocalOfferer(ulong localClientId, ulong remoteClientId)
    {
        if (localClientId == remoteClientId)
            throw new ArgumentException("Distinct peer IDs are required.", nameof(remoteClientId));

        return localClientId < remoteClientId;
    }

    public static bool IsLocalOfferer(string localPeerId, string remotePeerId)
    {
        if (string.IsNullOrEmpty(localPeerId))
            throw new ArgumentException("A local peer ID is required.", nameof(localPeerId));

        if (string.IsNullOrEmpty(remotePeerId))
            throw new ArgumentException("A remote peer ID is required.", nameof(remotePeerId));

        return string.CompareOrdinal(localPeerId, remotePeerId) < 0;
    }
}
