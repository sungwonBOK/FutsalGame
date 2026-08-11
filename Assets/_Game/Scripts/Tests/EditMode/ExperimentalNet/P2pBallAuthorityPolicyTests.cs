using NUnit.Framework;

public class P2pBallAuthorityPolicyTests
{
    [Test]
    public void ResolveTackle_WhenPassWasCommitted_DoesNotTransferTheFreeBall()
    {
        P2pBallAuthorityState freeBall = new P2pBallAuthorityState(
            authorityId: 10,
            ownerId: 0,
            epoch: 4);

        P2pBallAuthorityTransition transition = P2pBallAuthorityPolicy.ResolveTackle(
            freeBall,
            attackerId: 20,
            resolution: P2pCombatResolution.Hit);

        Assert.That(transition.Kind, Is.EqualTo(P2pBallAuthorityTransitionKind.NoBallTransfer));
        Assert.That(transition.State.AuthorityId, Is.EqualTo(10));
        Assert.That(transition.State.OwnerId, Is.EqualTo(0));
        Assert.That(transition.State.Epoch, Is.EqualTo(4));
    }

    [Test]
    public void ResolveTackle_WhenOwnerStillDribbles_TransfersAuthorityButLeavesTheBallFree()
    {
        P2pBallAuthorityState dribbling = new P2pBallAuthorityState(
            authorityId: 10,
            ownerId: 10,
            epoch: 4);

        P2pBallAuthorityTransition transition = P2pBallAuthorityPolicy.ResolveTackle(
            dribbling,
            attackerId: 20,
            resolution: P2pCombatResolution.Hit);

        Assert.That(transition.Kind, Is.EqualTo(P2pBallAuthorityTransitionKind.TackleWon));
        Assert.That(transition.State.AuthorityId, Is.EqualTo(20));
        Assert.That(transition.State.OwnerId, Is.EqualTo(0));
        Assert.That(transition.State.Epoch, Is.EqualTo(5));
    }

    [Test]
    public void AcceptSnapshot_RejectsAnOldAuthorityEvenWhenItsSequenceIsNewer()
    {
        P2pBallAuthorityState current = new P2pBallAuthorityState(
            authorityId: 20,
            ownerId: 20,
            epoch: 5);

        Assert.That(
            P2pBallAuthorityPolicy.ShouldAcceptSnapshot(
                current,
                lastAcceptedSequence: 2,
                snapshotAuthorityId: 10,
                snapshotEpoch: 4,
                snapshotSequence: 100),
            Is.False);
    }

    [Test]
    public void AcceptAuthorityTransfer_RequiresTheCurrentAuthorityAndNextEpoch()
    {
        P2pBallAuthorityState current = new P2pBallAuthorityState(
            authorityId: 10,
            ownerId: 10,
            epoch: 4);

        Assert.That(
            P2pBallAuthorityPolicy.TryApplyAuthorityTransfer(
                current,
                sourceAuthorityId: 10,
                nextAuthorityId: 20,
                nextOwnerId: 20,
                nextEpoch: 5,
                out P2pBallAuthorityState next),
            Is.True);
        Assert.That(next.AuthorityId, Is.EqualTo(20));

        Assert.That(
            P2pBallAuthorityPolicy.TryApplyAuthorityTransfer(
                current,
                sourceAuthorityId: 20,
                nextAuthorityId: 20,
                nextOwnerId: 20,
                nextEpoch: 5,
                out _),
            Is.False);
    }

    [Test]
    public void AcceptPeerDisconnectTransfer_RequiresTheDisconnectedAuthorityAndLeavesBallUnowned()
    {
        P2pBallAuthorityState current = new P2pBallAuthorityState(
            authorityId: 10,
            ownerId: 10,
            epoch: 4);

        Assert.That(
            P2pBallAuthorityPolicy.TryApplyPeerDisconnectTransfer(
                current,
                disconnectedAuthorityId: 10,
                sourceAuthorityId: 20,
                nextAuthorityId: 20,
                nextOwnerId: 0,
                nextEpoch: 5,
                out P2pBallAuthorityState next),
            Is.True);
        Assert.That(next.AuthorityId, Is.EqualTo(20));
        Assert.That(next.OwnerId, Is.EqualTo(0));

        Assert.That(
            P2pBallAuthorityPolicy.TryApplyPeerDisconnectTransfer(
                current,
                disconnectedAuthorityId: 10,
                sourceAuthorityId: 20,
                nextAuthorityId: 20,
                nextOwnerId: 20,
                nextEpoch: 5,
                out _),
            Is.False);
    }
}
