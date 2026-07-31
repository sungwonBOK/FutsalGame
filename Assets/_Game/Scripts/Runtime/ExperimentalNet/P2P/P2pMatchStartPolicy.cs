public static class P2pMatchStartPolicy
{
    public static bool CanStart(int connectedPlayerCount, bool isDirectP2pReady)
    {
        return connectedPlayerCount != 2 || isDirectP2pReady;
    }
}
