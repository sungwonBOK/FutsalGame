public static class MpsNetworkingModePolicy
{
    public static bool RequiresDirectP2p(bool isMpsRelaySession)
    {
        return !isMpsRelaySession;
    }
}
