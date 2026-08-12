public static class P2pPlayerCountPolicy
{
    public static bool IsSupported(int playerCount)
    {
        return playerCount >= 1 && playerCount <= MpsRoomDefinition.MaximumPlayers;
    }

    public static bool RequiresDirectP2p(int playerCount)
    {
        return IsSupported(playerCount) && playerCount >= 2;
    }

    public static bool RequiresGameReady(int playerCount)
    {
        return RequiresDirectP2p(playerCount);
    }

    public static bool CanStartWithoutDirectP2p(int playerCount, bool isPlayerCountTestRoom)
    {
        return isPlayerCountTestRoom && playerCount == 1;
    }
}
