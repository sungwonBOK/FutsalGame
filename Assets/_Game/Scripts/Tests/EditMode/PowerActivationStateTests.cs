using NUnit.Framework;

public class PowerActivationStateTests
{
    [Test]
    public void TryArm_WhenGaugeIsFull_EntersArmedState()
    {
        var state = new PowerActivationState();

        bool armed = state.TryArm(isGaugeFull: true);

        Assert.That(armed, Is.True);
        Assert.That(state.IsArmed, Is.True);
    }

    [Test]
    public void TryArm_WhenGaugeIsNotFull_DoesNotEnterArmedState()
    {
        var state = new PowerActivationState();

        bool armed = state.TryArm(isGaugeFull: false);

        Assert.That(armed, Is.False);
        Assert.That(state.IsArmed, Is.False);
    }

    [Test]
    public void TryConsume_WhenEligibleActionIsRejected_PreservesArmedState()
    {
        var state = new PowerActivationState();
        state.TryArm(isGaugeFull: true);

        bool consumed = state.TryConsume(EnhancedActionKind.Primary, wasAccepted: false);

        Assert.That(consumed, Is.False);
        Assert.That(state.IsArmed, Is.True);
    }

    [Test]
    public void TryConsume_WhenEligibleActionIsAccepted_ConsumesArmedStateOnce()
    {
        var state = new PowerActivationState();
        state.TryArm(isGaugeFull: true);

        bool consumed = state.TryConsume(EnhancedActionKind.BurstSprint, wasAccepted: true);

        Assert.That(consumed, Is.True);
        Assert.That(state.IsArmed, Is.False);
        Assert.That(state.TryConsume(EnhancedActionKind.BurstSprint, wasAccepted: true), Is.False);
    }

    [Test]
    public void CancelAndReset_ClearArmedStateWithoutRequiringAnAction()
    {
        var state = new PowerActivationState();
        state.TryArm(isGaugeFull: true);

        Assert.That(state.TryCancel(), Is.True);
        Assert.That(state.IsArmed, Is.False);

        state.TryArm(isGaugeFull: true);
        state.Reset();

        Assert.That(state.IsArmed, Is.False);
    }
}
