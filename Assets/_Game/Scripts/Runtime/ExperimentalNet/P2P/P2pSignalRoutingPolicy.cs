public static class P2pSignalRoutingPolicy
{
    /// <summary>
    /// A guest always sends setup traffic to the Host control plane. The Host
    /// validates and forwards that traffic to the addressed participant.
    /// </summary>
    public static bool CanSendToRecipient(
        bool isServer,
        ulong localClientId,
        ulong serverClientId,
        ulong recipientClientId)
    {
        return recipientClientId != localClientId
            && (isServer || localClientId != serverClientId);
    }
}
