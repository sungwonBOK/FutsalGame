using NUnit.Framework;

public class MpsNetworkingModePolicyTests
{
    [Test]
    public void RequiresDirectP2p_UsesTheGameplayMeshForAnMpsRelaySession()
    {
        Assert.That(MpsNetworkingModePolicy.RequiresDirectP2p(true), Is.True);
    }

    [Test]
    public void RequiresDirectP2p_IsTrueForTheExistingExperimentalPath()
    {
        Assert.That(MpsNetworkingModePolicy.RequiresDirectP2p(false), Is.True);
    }
}
