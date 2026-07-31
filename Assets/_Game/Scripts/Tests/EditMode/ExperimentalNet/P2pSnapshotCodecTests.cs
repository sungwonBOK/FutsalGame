using NUnit.Framework;
using UnityEngine;

public class P2pSnapshotCodecTests
{
    [Test]
    public void SnapshotCodec_RoundTripsPositionYawAndSequence()
    {
        P2pPlayerSnapshot source = new P2pPlayerSnapshot(42, new Vector3(1.25f, -3f, 8.5f), 135f);

        Assert.That(P2pSnapshotCodec.TryEncode(source, out byte[] payload), Is.True);
        Assert.That(P2pSnapshotCodec.TryDecode(payload, out P2pPlayerSnapshot decoded), Is.True);
        Assert.That(decoded.Sequence, Is.EqualTo(42));
        Assert.That(decoded.Position, Is.EqualTo(source.Position));
        Assert.That(decoded.YawDegrees, Is.EqualTo(source.YawDegrees));
    }

    [Test]
    public void SnapshotBuffer_RejectsAnOlderSequence()
    {
        P2pRemoteSnapshotBuffer buffer = new P2pRemoteSnapshotBuffer();
        P2pPlayerSnapshot newest = new P2pPlayerSnapshot(12, Vector3.one, 0f);
        P2pPlayerSnapshot stale = new P2pPlayerSnapshot(11, Vector3.zero, 0f);

        Assert.That(buffer.TryAccept(newest), Is.True);
        Assert.That(buffer.TryAccept(stale), Is.False);
        Assert.That(buffer.Latest.Position, Is.EqualTo(Vector3.one));
    }
}
