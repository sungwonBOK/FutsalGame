using NUnit.Framework;

public class GameplayActionResolverTests
{
    [TestCase(GameplayActionSlot.Primary, false, GameplayActionId.BasicPunch)]
    [TestCase(GameplayActionSlot.Secondary, false, GameplayActionId.CrossPunch)]
    [TestCase(GameplayActionSlot.Primary, true, GameplayActionId.PassCharge)]
    [TestCase(GameplayActionSlot.Secondary, true, GameplayActionId.ShotCharge)]
    public void Resolve_MapsSlotsByPossession(
        GameplayActionSlot slot,
        bool hasPossessionContext,
        GameplayActionId expected)
    {
        GameplayActionContext context = new GameplayActionContext(
            hasPossessionContext,
            mouseActionsBlocked: false,
            isCharging: false);

        GameplayActionRequest request = GameplayActionResolver.Resolve(slot, context);

        Assert.That(request.Id, Is.EqualTo(expected));
    }

    [Test]
    public void Resolve_BlockedMouseAction_ReturnsNoneWithoutChangingContext()
    {
        GameplayActionContext context = new GameplayActionContext(
            hasPossessionContext: true,
            mouseActionsBlocked: true,
            isCharging: false);

        GameplayActionRequest request = GameplayActionResolver.Resolve(GameplayActionSlot.Secondary, context);

        Assert.That(request.Id, Is.EqualTo(GameplayActionId.None));
        Assert.That(context.HasPossessionContext, Is.True);
        Assert.That(context.MouseActionsBlocked, Is.True);
    }
}
