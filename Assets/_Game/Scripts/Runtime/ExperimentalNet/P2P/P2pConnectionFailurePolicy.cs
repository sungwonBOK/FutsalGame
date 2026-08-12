public static class P2pConnectionFailurePolicy
{
    public static bool ShouldFailOnDataChannelClose(P2pConnectionState state)
    {
        return state == P2pConnectionState.Negotiating || state == P2pConnectionState.Ready;
    }

    public static bool ShouldFailOnTransportTerminalState(bool failed, bool closed)
    {
        return failed || closed;
    }
}
