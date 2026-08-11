using NUnit.Framework;

public class P2pMatchStartPolicyTests
{
    [Test]
    public void HostAlone_CanStartWithoutRemoteReadyOrMesh()
    {
        Assert.That(P2pMatchStartPolicy.CanStart(1, false), Is.True);
    }

    [Test]
    public void ParticipantMatch_RequiresACompleteMeshButNotManualReadyAcknowledgements()
    {
        Assert.That(P2pMatchStartPolicy.CanStart(3, true), Is.True);
        Assert.That(P2pMatchStartPolicy.CanStart(3, false), Is.False);
    }

    [Test]
    public void PlayerCountOutsideTheSixPlayerMeshLimit_CannotStart()
    {
        Assert.That(P2pMatchStartPolicy.CanStart(0, true), Is.False);
        Assert.That(P2pMatchStartPolicy.CanStart(7, true), Is.False);
    }
}
