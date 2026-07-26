using NUnit.Framework;

public class PossessionInputContextTests
{
    [Test]
    public void Update_KeepsPossessionContextForSprintGraceWhileFreeBallIsRecoverable()
    {
        var context = new PossessionInputContext();

        context.Update(10f, actuallyHasBall: true, opponentHasBall: false, withinAcquireRange: true, sprintHeld: true);
        context.Update(10.64f, actuallyHasBall: false, opponentHasBall: false, withinAcquireRange: true, sprintHeld: true);

        Assert.That(context.HasPossessionContext, Is.True);

        context.Update(10.66f, actuallyHasBall: false, opponentHasBall: false, withinAcquireRange: true, sprintHeld: true);

        Assert.That(context.HasPossessionContext, Is.False);
    }

    [Test]
    public void Update_EndsSprintGraceImmediatelyForOpponentOwnerOrOutOfRangeBall()
    {
        var context = new PossessionInputContext();

        context.Update(10f, actuallyHasBall: true, opponentHasBall: false, withinAcquireRange: true, sprintHeld: true);
        context.Update(10.1f, actuallyHasBall: false, opponentHasBall: true, withinAcquireRange: true, sprintHeld: true);

        Assert.That(context.HasPossessionContext, Is.False);

        context.Update(11f, actuallyHasBall: true, opponentHasBall: false, withinAcquireRange: true, sprintHeld: true);
        context.Update(11.1f, actuallyHasBall: false, opponentHasBall: false, withinAcquireRange: false, sprintHeld: true);

        Assert.That(context.HasPossessionContext, Is.False);
    }

    [Test]
    public void CombatProtection_DoesNotBlockMouseActionsWhilePlayerDoesNotHaveBall()
    {
        var context = new PossessionInputContext();

        context.BeginCombatProtection(10f);
        context.Update(10.2f, actuallyHasBall: false, opponentHasBall: false, withinAcquireRange: true, sprintHeld: false);

        Assert.That(context.AreMouseActionsBlocked(10.29f), Is.False);
    }

    [Test]
    public void CombatProtection_BlocksMouseActionsOnlyAfterPossessionArrivesBeforeTimerExpires()
    {
        var context = new PossessionInputContext();

        context.BeginCombatProtection(10f);
        context.Update(10.2f, actuallyHasBall: true, opponentHasBall: false, withinAcquireRange: true, sprintHeld: false);

        Assert.That(context.AreMouseActionsBlocked(10.39f), Is.True);
        Assert.That(context.AreMouseActionsBlocked(10.41f), Is.False);
    }

    [Test]
    public void CombatProtection_DoesNotBlockMouseActionsWhenPossessionArrivesAfterTimerExpires()
    {
        var context = new PossessionInputContext();

        context.BeginCombatProtection(10f);
        context.Update(10.41f, actuallyHasBall: true, opponentHasBall: false, withinAcquireRange: true, sprintHeld: false);

        Assert.That(context.AreMouseActionsBlocked(10.41f), Is.False);
    }
}
