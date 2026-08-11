public static class P2pMatchStartPolicy
{
    public const int MaximumPlayers = 6;

    public static bool CanStart(
        int connectedPlayerCount,
        bool areAllNonHostPlayersReady,
        bool isDirectP2pMeshReady)
    {
        if (connectedPlayerCount < 1 || connectedPlayerCount > MaximumPlayers)
            return false;

        return connectedPlayerCount == 1
            || (areAllNonHostPlayersReady && isDirectP2pMeshReady);
    }

    public static bool CanStart(
        int connectedPlayerCount,
        P2pGameplayReadiness readiness,
        P2pGameplayChannel openChannels)
    {
        return CanStart(
            connectedPlayerCount,
            areAllNonHostPlayersReady: true,
            isDirectP2pMeshReady: readiness.IsReady(openChannels));
    }
}
