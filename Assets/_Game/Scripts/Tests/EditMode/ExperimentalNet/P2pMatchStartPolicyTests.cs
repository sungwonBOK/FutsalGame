using NUnit.Framework;

public class P2pMatchStartPolicyTests
{
    [Test]
    public void TwoPlayerMatch_RequiresTheDirectP2pChannelToBeReady()
    {
        Assert.That(P2pMatchStartPolicy.CanStart(2, false), Is.False);
        Assert.That(P2pMatchStartPolicy.CanStart(2, true), Is.True);
    }

    [Test]
    public void TwoPlayerMatch_RequiresEveryConfiguredGameplayChannel()
    {
        P2pGameplayReadiness readiness = new P2pGameplayReadiness(
            P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat);

        Assert.That(
            P2pMatchStartPolicy.CanStart(2, readiness, P2pGameplayChannel.Snapshot),
            Is.False);
        Assert.That(
            P2pMatchStartPolicy.CanStart(
                2,
                readiness,
                P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat),
            Is.True);
    }
}
