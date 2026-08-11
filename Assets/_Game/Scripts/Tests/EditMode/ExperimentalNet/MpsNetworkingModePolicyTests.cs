using NUnit.Framework;

public class MpsNetworkingModePolicyTests
{
    [Test]
    public void RequiresDirectP2p_IsFalseForAnMpsRelaySession()
    {
        Assert.That(MpsNetworkingModePolicy.RequiresDirectP2p(true), Is.False);
    }

    [Test]
    public void RequiresDirectP2p_IsTrueForTheExistingExperimentalPath()
    {
        Assert.That(MpsNetworkingModePolicy.RequiresDirectP2p(false), Is.True);
    }
}
