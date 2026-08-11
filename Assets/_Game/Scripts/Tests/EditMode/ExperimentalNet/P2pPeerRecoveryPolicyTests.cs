using NUnit.Framework;

public class P2pPeerRecoveryPolicyTests
{
    [Test]
    public void FailedPeer_FreezesUntilTheCompleteMeshIsReadyAgain()
    {
        Assert.That(P2pPeerRecoveryPolicy.ShouldFreeze(P2pConnectionState.Failed), Is.True);
        Assert.That(P2pPeerRecoveryPolicy.CanResume(P2pConnectionState.Ready, isMeshReady: false), Is.False);
        Assert.That(P2pPeerRecoveryPolicy.CanResume(P2pConnectionState.Ready, isMeshReady: true), Is.True);
    }

    [Test]
    public void ClosedPeer_IsNotTreatedAsARecoverableGameplayDisconnect()
    {
        Assert.That(P2pPeerRecoveryPolicy.ShouldFreeze(P2pConnectionState.Closed), Is.False);
    }
}
