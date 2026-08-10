using NUnit.Framework;

public class P2pGameplayReadinessTests
{
    [Test]
    public void IsReady_RequiresEveryConfiguredGameplayChannel()
    {
        P2pGameplayReadiness readiness = new P2pGameplayReadiness(
            P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat);

        Assert.That(readiness.IsReady(P2pGameplayChannel.Snapshot), Is.False);
        Assert.That(readiness.IsReady(P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat), Is.True);
    }

    [Test]
    public void IsReady_RequiresBallOnlyAfterItBecomesARequiredChannel()
    {
        P2pGameplayReadiness readiness = new P2pGameplayReadiness(
            P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat | P2pGameplayChannel.Ball);

        Assert.That(readiness.IsReady(P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat), Is.False);
        Assert.That(readiness.IsReady(
            P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat | P2pGameplayChannel.Ball), Is.True);
    }
}
