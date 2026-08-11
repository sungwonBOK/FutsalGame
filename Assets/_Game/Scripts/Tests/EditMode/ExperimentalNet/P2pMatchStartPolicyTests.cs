using System.Reflection;
using NUnit.Framework;

public class P2pMatchStartPolicyTests
{
    [Test]
    public void HostAlone_CanStartWithoutRemoteReadyOrMesh()
    {
        Assert.That(CanStart(1, isDirectP2pMeshReady: false, areAllPlayersGameReady: false), Is.True);
    }

    [Test]
    public void ParticipantMatch_RequiresBothACompleteMeshAndEveryPlayersGameReadyInput()
    {
        Assert.That(CanStart(3, isDirectP2pMeshReady: true, areAllPlayersGameReady: true), Is.True);
        Assert.That(CanStart(3, isDirectP2pMeshReady: true, areAllPlayersGameReady: false), Is.False);
        Assert.That(CanStart(3, isDirectP2pMeshReady: false, areAllPlayersGameReady: true), Is.False);
    }

    [Test]
    public void PlayerCountOutsideTheSixPlayerMeshLimit_CannotStart()
    {
        Assert.That(CanStart(0, isDirectP2pMeshReady: true, areAllPlayersGameReady: true), Is.False);
        Assert.That(CanStart(7, isDirectP2pMeshReady: true, areAllPlayersGameReady: true), Is.False);
    }

    private static bool CanStart(int playerCount, bool isDirectP2pMeshReady, bool areAllPlayersGameReady)
    {
        MethodInfo canStart = typeof(P2pMatchStartPolicy).GetMethod(
            "CanStart",
            new[] { typeof(int), typeof(bool), typeof(bool) });
        Assert.That(canStart, Is.Not.Null, "Participant matches need a game-ready gate in addition to P2P readiness.");
        return (bool)canStart.Invoke(null, new object[] { playerCount, isDirectP2pMeshReady, areAllPlayersGameReady });
    }
}
