using NUnit.Framework;

public class P2pMatchStartPolicyTests
{
    [Test]
    public void HostAlone_CanStartWithoutRemoteReadyOrMesh()
    {
        Assert.That(P2pMatchStartPolicy.CanStart(1, false, false), Is.True);
    }

    [Test]
    public void ParticipantMatch_RequiresEveryNonHostReadyAndACompleteMesh()
    {
        Assert.That(P2pMatchStartPolicy.CanStart(3, false, true), Is.False);
        Assert.That(P2pMatchStartPolicy.CanStart(3, true, false), Is.False);
        Assert.That(P2pMatchStartPolicy.CanStart(3, true, true), Is.True);
    }

    [Test]
    public void PlayerCountOutsideTheSixPlayerMeshLimit_CannotStart()
    {
        Assert.That(P2pMatchStartPolicy.CanStart(0, true, true), Is.False);
        Assert.That(P2pMatchStartPolicy.CanStart(7, true, true), Is.False);
    }
}
