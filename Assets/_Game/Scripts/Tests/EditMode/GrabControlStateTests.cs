using NUnit.Framework;

public class GrabControlStateTests
{
    [Test]
    public void Holding_AllowsGrabCancellationOnlyAfterHalfSecond()
    {
        GrabControlState state = new GrabControlState();
        state.BeginHolding(10f);

        Assert.That(state.CanUse(GameplayInputAction.Grab, 10.49f), Is.False);
        Assert.That(state.CanUse(GameplayInputAction.Grab, 10.5f), Is.True);
        Assert.That(state.CanUse(GameplayInputAction.Dodge, 10.5f), Is.False);
    }

    [Test]
    public void Held_AllowsOnlyDodge()
    {
        GrabControlState state = new GrabControlState();
        state.BeginHeld();

        Assert.That(state.CanUse(GameplayInputAction.Dodge, 0f), Is.True);
        Assert.That(state.CanUse(GameplayInputAction.Grab, 0f), Is.False);
        Assert.That(state.CanUse(GameplayInputAction.PrimaryAction, 0f), Is.False);
    }

    [Test]
    public void Clear_RestoresNormalInputAccess()
    {
        GrabControlState state = new GrabControlState();
        state.BeginHeld();
        state.Clear();

        Assert.That(state.CanUse(GameplayInputAction.PrimaryAction, 0f), Is.True);
    }

    [Test]
    public void Holding_ScalesMovementWhileHeldBlocksMovement()
    {
        GrabControlState state = new GrabControlState();
        state.BeginHolding(0f, movementMultiplier: 0.15f);

        Assert.That(state.MovementMultiplier, Is.EqualTo(0.15f));

        state.BeginHeld();
        Assert.That(state.MovementMultiplier, Is.Zero);

        state.Clear();
        Assert.That(state.MovementMultiplier, Is.EqualTo(1f));
    }
}
