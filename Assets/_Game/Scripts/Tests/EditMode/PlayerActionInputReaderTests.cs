using NUnit.Framework;
using UnityEngine;

public class PlayerActionInputReaderTests
{
    [Test]
    public void DefaultBindings_UseMouseForBallActionsAndCForCancel()
    {
        PlayerActionBindings bindings = ScriptableObject.CreateInstance<PlayerActionBindings>();
        try
        {
            Assert.That(bindings.Pass.MouseButton, Is.EqualTo(PlayerMouseButton.Left));
            Assert.That(bindings.Pass.KeyboardKeyName, Is.EqualTo("None"));
            Assert.That(bindings.Shot.MouseButton, Is.EqualTo(PlayerMouseButton.Right));
            Assert.That(bindings.Shot.KeyboardKeyName, Is.EqualTo("None"));
            Assert.That(bindings.Cancel.MouseButton, Is.EqualTo(PlayerMouseButton.None));
            Assert.That(bindings.Cancel.KeyboardKeyName, Is.EqualTo("C"));
        }
        finally
        {
            Object.DestroyImmediate(bindings);
        }
    }

    [Test]
    public void Combine_ReportsReleaseOnlyAfterEveryConfiguredAlternativeIsReleased()
    {
        ActionButtonState mouseState = new ActionButtonState(wasPressed: false, isPressed: true, wasReleased: false);
        ActionButtonState keyboardState = new ActionButtonState(wasPressed: false, isPressed: false, wasReleased: true);

        ActionButtonState combined = PlayerActionInputReader.Combine(mouseState, keyboardState);

        Assert.That(combined.IsPressed, Is.True);
        Assert.That(combined.WasReleased, Is.False);
    }
}
