using NUnit.Framework;

public class P2pSignalingTests
{
    [Test]
    public void OffererSelection_UsesTheLexicographicallyLowerPeerId()
    {
        Assert.That(P2pOfferSelector.IsLocalOfferer("alpha", "bravo"), Is.True);
        Assert.That(P2pOfferSelector.IsLocalOfferer("bravo", "alpha"), Is.False);
    }

    [Test]
    public void SignalMessage_RejectsPayloadThatExceedsTheConfiguredLimit()
    {
        string oversizedPayload = new string('x', P2pSignalMessage.MaxPayloadCharacters + 1);

        Assert.That(P2pSignalMessage.TryCreate(P2pSignalKind.Offer, oversizedPayload, out _), Is.False);
    }

    [Test]
    public void SignalFragments_ReassembleTheOriginalPayload()
    {
        string payload = new string('x', P2pSignalFragmenter.MaxFragmentPayloadBytes + 200);
        Assert.That(P2pSignalMessage.TryCreate(P2pSignalKind.Answer, payload, out P2pSignalMessage message), Is.True);

        P2pSignalReassembler reassembler = new P2pSignalReassembler();
        P2pSignalMessage reassembled = default;

        foreach (P2pSignalFragment fragment in P2pSignalFragmenter.Split(message, 7))
            reassembler.TryAdd(fragment, out reassembled);

        Assert.That(reassembled.Kind, Is.EqualTo(P2pSignalKind.Answer));
        Assert.That(reassembled.Payload, Is.EqualTo(payload));
    }
}
