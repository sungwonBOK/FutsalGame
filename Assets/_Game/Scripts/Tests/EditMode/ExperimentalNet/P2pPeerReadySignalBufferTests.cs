using NUnit.Framework;

public sealed class P2pPeerReadySignalBufferTests
{
    [Test]
    public void EarlyReadyForTheLocalPeer_IsRememberedUntilThePeerIsConfigured()
    {
        P2pPeerReadySignalBuffer buffer = new P2pPeerReadySignalBuffer();

        Assert.That(buffer.TryRemember(CreateSignal(senderClientId: 1, recipientClientId: 0), localClientId: 0), Is.True);
        Assert.That(buffer.Consume(1), Is.True);
        Assert.That(buffer.Consume(1), Is.False);
    }

    [Test]
    public void NonReadySignal_IsNotRememberedBeforeThePeerIsConfigured()
    {
        P2pPeerReadySignalBuffer buffer = new P2pPeerReadySignalBuffer();
        Assert.That(P2pSignalMessage.TryCreate(P2pSignalKind.Offer, "offer", out P2pSignalMessage offer), Is.True);
        Assert.That(P2pPeerSignal.TryCreate(1, 0, offer, out P2pPeerSignal signal), Is.True);

        Assert.That(buffer.TryRemember(signal, localClientId: 0), Is.False);
    }

    private static P2pPeerSignal CreateSignal(ulong senderClientId, ulong recipientClientId)
    {
        Assert.That(P2pSignalMessage.TryCreate(P2pSignalKind.Ready, "ready", out P2pSignalMessage ready), Is.True);
        Assert.That(P2pPeerSignal.TryCreate(senderClientId, recipientClientId, ready, out P2pPeerSignal signal), Is.True);
        return signal;
    }
}
