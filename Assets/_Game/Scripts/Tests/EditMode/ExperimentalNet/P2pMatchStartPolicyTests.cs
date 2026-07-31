using NUnit.Framework;

public class P2pMatchStartPolicyTests
{
    [Test]
    public void TwoPlayerMatch_RequiresTheDirectP2pChannelToBeReady()
    {
        Assert.That(P2pMatchStartPolicy.CanStart(2, false), Is.False);
        Assert.That(P2pMatchStartPolicy.CanStart(2, true), Is.True);
    }
}
