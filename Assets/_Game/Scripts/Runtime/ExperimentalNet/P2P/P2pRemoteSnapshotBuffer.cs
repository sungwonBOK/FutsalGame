public sealed class P2pRemoteSnapshotBuffer
{
    public P2pPlayerSnapshot Latest { get; private set; }
    public bool HasSnapshot { get; private set; }

    public bool TryAccept(P2pPlayerSnapshot snapshot)
    {
        if (HasSnapshot && !IsNewer(snapshot.Sequence, Latest.Sequence))
            return false;

        Latest = snapshot;
        HasSnapshot = true;
        return true;
    }

    public void Clear()
    {
        Latest = default;
        HasSnapshot = false;
    }

    private static bool IsNewer(ushort candidate, ushort current)
    {
        ushort difference = (ushort)(candidate - current);
        return difference != 0 && difference < 32768;
    }
}
