using NUnit.Framework;
using UnityEngine;

public class P2pBallProtocolTests
{
    [Test]
    public void StateCodec_RoundTripsAuthorityEpochSequenceAndMotion()
    {
        P2pBallState source = new P2pBallState(
            authorityId: 10,
            ownerId: 20,
            epoch: 4,
            sequence: 12,
            position: new Vector3(1f, 2f, 3f),
            rotation: Quaternion.Euler(10f, 20f, 30f),
            velocity: new Vector3(4f, 5f, 6f),
            angularVelocity: new Vector3(7f, 8f, 9f));

        Assert.That(P2pBallStateCodec.TryEncode(source, out byte[] payload), Is.True);
        Assert.That(P2pBallStateCodec.TryDecode(payload, out P2pBallState decoded), Is.True);
        Assert.That(decoded.AuthorityId, Is.EqualTo(10));
        Assert.That(decoded.OwnerId, Is.EqualTo(20));
        Assert.That(decoded.Epoch, Is.EqualTo(4));
        Assert.That(decoded.Sequence, Is.EqualTo(12));
        Assert.That(decoded.Position, Is.EqualTo(source.Position));
        Assert.That(decoded.Velocity, Is.EqualTo(source.Velocity));
        Assert.That(decoded.AngularVelocity, Is.EqualTo(source.AngularVelocity));
    }

    [Test]
    public void EventCodec_RoundTripsAnAuthorityTransferAnchor()
    {
        P2pBallState anchor = new P2pBallState(
            authorityId: 20,
            ownerId: 20,
            epoch: 5,
            sequence: 0,
            position: Vector3.one,
            rotation: Quaternion.identity,
            velocity: Vector3.zero,
            angularVelocity: Vector3.zero);
        P2pBallEvent source = new P2pBallEvent(
            P2pBallEventKind.AuthorityChanged,
            P2pBallActionKind.None,
            actionId: 42,
            sourceAuthorityId: 10,
            anchorState: anchor);

        Assert.That(P2pBallEventCodec.TryEncode(source, out byte[] payload), Is.True);
        Assert.That(P2pBallEventCodec.TryDecode(payload, out P2pBallEvent decoded), Is.True);
        Assert.That(decoded.Kind, Is.EqualTo(P2pBallEventKind.AuthorityChanged));
        Assert.That(decoded.ActionId, Is.EqualTo(42));
        Assert.That(decoded.SourceAuthorityId, Is.EqualTo(10));
        Assert.That(decoded.AnchorState.AuthorityId, Is.EqualTo(20));
        Assert.That(decoded.AnchorState.Epoch, Is.EqualTo(5));
    }
}
