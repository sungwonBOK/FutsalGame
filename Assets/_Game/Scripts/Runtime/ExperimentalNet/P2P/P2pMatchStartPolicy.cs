public static class P2pMatchStartPolicy
{
    public const int MaximumPlayers = 6;

    public static bool CanStart(
        int connectedPlayerCount,
        bool isDirectP2pMeshReady)
    {
        if (connectedPlayerCount < 1 || connectedPlayerCount > MaximumPlayers)
            return false;

        return connectedPlayerCount == 1
            || isDirectP2pMeshReady;
    }

    public static bool CanStart(
        int connectedPlayerCount,
        P2pGameplayReadiness readiness,
        P2pGameplayChannel openChannels)
    {
        return CanStart(
            connectedPlayerCount,
            isDirectP2pMeshReady: readiness.IsReady(openChannels));
    }
}
