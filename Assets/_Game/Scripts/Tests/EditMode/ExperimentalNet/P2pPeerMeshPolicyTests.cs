using NUnit.Framework;

public class P2pPeerMeshPolicyTests
{
    [Test]
    public void TryCreateSignal_RejectsSelfAddressedSignal()
    {
        bool created = P2pPeerSignal.TryCreate(
            senderClientId: 4,
            recipientClientId: 4,
            signal: CreateReadySignal(),
            out _);

        Assert.That(created, Is.False);
    }

    [Test]
    public void ClientIdOfferOrdering_UsesNumericIdsInsteadOfLexicalTextOrder()
    {
        Assert.That(P2pOfferSelector.IsLocalOfferer(2, 10), Is.True);
        Assert.That(P2pOfferSelector.IsLocalOfferer(10, 2), Is.False);
    }

    [Test]
    public void RequiredPeers_AreReadyOnlyWhenEveryPeerHasRequiredChannels()
    {
        P2pPeerMeshPolicy policy = new P2pPeerMeshPolicy(
            P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat | P2pGameplayChannel.Ball);
        policy.SetRequiredPeers(new ulong[] { 2, 5 });
        policy.SetOpenChannels(2, P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat | P2pGameplayChannel.Ball);
        policy.SetOpenChannels(5, P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat);

        Assert.That(policy.IsGameplayReady, Is.False);

        policy.SetOpenChannels(5, P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat | P2pGameplayChannel.Ball);

        Assert.That(policy.IsGameplayReady, Is.True);
    }

    [Test]
    public void SetRequiredPeers_RemovesStalePeerReadiness()
    {
        P2pPeerMeshPolicy policy = new P2pPeerMeshPolicy(P2pGameplayChannel.Snapshot);
        policy.SetRequiredPeers(new ulong[] { 2, 5 });
        policy.SetOpenChannels(2, P2pGameplayChannel.Snapshot);
        policy.SetOpenChannels(5, P2pGameplayChannel.Snapshot);

        policy.SetRequiredPeers(new ulong[] { 2 });

        Assert.That(policy.RequiredPeerCount, Is.EqualTo(1));
        Assert.That(policy.IsGameplayReady, Is.True);
        Assert.That(policy.ContainsRequiredPeer(5), Is.False);
    }

    private static P2pSignalMessage CreateReadySignal()
    {
        Assert.That(P2pSignalMessage.TryCreate(P2pSignalKind.Ready, "ready", out P2pSignalMessage signal), Is.True);
        return signal;
    }
}
