using NUnit.Framework;

public class P2pPlayerCountPolicyTests
{
    [Test]
    public void OnePlayer_DoesNotRequireDirectP2pOrGameReady()
    {
        Assert.That(P2pPlayerCountPolicy.IsSupported(1), Is.True);
        Assert.That(P2pPlayerCountPolicy.RequiresDirectP2p(1), Is.False);
        Assert.That(P2pPlayerCountPolicy.RequiresGameReady(1), Is.False);
    }

    [Test]
    public void OnePlayer_CanBypassDirectP2pOnlyForAnMpsSession()
    {
        Assert.That(P2pPlayerCountPolicy.CanStartWithoutDirectP2p(1, true), Is.True);
        Assert.That(P2pPlayerCountPolicy.CanStartWithoutDirectP2p(1, false), Is.False);
        Assert.That(P2pPlayerCountPolicy.CanStartWithoutDirectP2p(2, true), Is.False);
        Assert.That(P2pPlayerCountPolicy.CanStartWithoutDirectP2p(7, true), Is.False);
    }

    [TestCase(2)]
    [TestCase(6)]
    public void ParticipantMatches_RequireDirectP2pAndGameReady(int playerCount)
    {
        Assert.That(P2pPlayerCountPolicy.IsSupported(playerCount), Is.True);
        Assert.That(P2pPlayerCountPolicy.RequiresDirectP2p(playerCount), Is.True);
        Assert.That(P2pPlayerCountPolicy.RequiresGameReady(playerCount), Is.True);
    }

    [TestCase(0)]
    [TestCase(7)]
    public void CountsOutsideTheTestRoomRange_AreNotSupported(int playerCount)
    {
        Assert.That(P2pPlayerCountPolicy.IsSupported(playerCount), Is.False);
    }
}
