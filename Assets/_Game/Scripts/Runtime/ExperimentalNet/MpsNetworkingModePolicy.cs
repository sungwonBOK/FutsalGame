public static class MpsNetworkingModePolicy
{
    public static bool RequiresDirectP2p(bool isMpsRelaySession)
    {
        // MPS/Relay provides room and control-plane connectivity only. Gameplay
        // always waits for the direct mesh, including public MPS sessions.
        return true;
    }
}
