public static class P2pDiagnosticFormatter
{
    public static string ConnectionPrepared(bool isOfferer, int generation)
    {
        return "[P2P:" + Role(isOfferer) + "] Connection prepared (attempt=" + generation + ").";
    }

    public static string Signal(bool isOfferer, string direction, P2pSignalKind kind, int payloadLength)
    {
        return "[P2P:" + Role(isOfferer) + "] Signal " + direction + ": " + kind + " (" + payloadLength + " chars).";
    }

    public static string Candidate(bool isOfferer, string stage, int count, int pendingCount)
    {
        return "[P2P:" + Role(isOfferer) + "] Candidate " + stage + " #" + count + " (pending=" + pendingCount + ").";
    }

    public static string IceState(bool isOfferer, string state, int generatedCount, int receivedCount, int appliedCount, int pendingCount)
    {
        return "[P2P:" + Role(isOfferer) + "] ICE " + state
            + " (generated=" + generatedCount
            + ", received=" + receivedCount
            + ", applied=" + appliedCount
            + ", pending=" + pendingCount + ").";
    }

    public static string PeerState(bool isOfferer, string state)
    {
        return "[P2P:" + Role(isOfferer) + "] Peer connection " + state + ".";
    }

    public static string DataChannel(bool isOfferer, string state)
    {
        return "[P2P:" + Role(isOfferer) + "] DataChannel " + state + ".";
    }

    private static string Role(bool isOfferer)
    {
        return isOfferer ? "Host" : "Guest";
    }
}
