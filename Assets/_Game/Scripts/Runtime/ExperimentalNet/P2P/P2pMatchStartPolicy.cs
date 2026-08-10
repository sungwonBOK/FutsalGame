public static class P2pMatchStartPolicy
{
    public static bool CanStart(int connectedPlayerCount, bool isDirectP2pReady)
    {
        return connectedPlayerCount != 2 || isDirectP2pReady;
    }

    public static bool CanStart(
        int connectedPlayerCount,
        P2pGameplayReadiness readiness,
        P2pGameplayChannel openChannels)
    {
        return connectedPlayerCount != 2 || readiness.IsReady(openChannels);
    }
}
