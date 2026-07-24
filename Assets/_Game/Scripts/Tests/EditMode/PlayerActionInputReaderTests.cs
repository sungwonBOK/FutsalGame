using System.IO;
using NUnit.Framework;
using UnityEngine;

public class PlayerActionInputReaderTests
{
    private static string PlayerInputPath => Path.Combine(
        Application.dataPath,
        "_Game/Scripts/Runtime/Input/PlayerInput.cs");

    [Test]
    public void PlayerInput_UsesSemanticGameplayInputActionsInsteadOfRawControls()
    {
        string source = File.ReadAllText(PlayerInputPath);

        Assert.That(source, Does.Contain("inputReader.ReadMove()"));
        Assert.That(source, Does.Contain("GameplayInputAction.Sprint"));
        Assert.That(source, Does.Contain("GameplayInputAction.Pass"));
        Assert.That(source, Does.Contain("GameplayInputAction.Shot"));
        Assert.That(source, Does.Contain("GameplayInputAction.CancelCharge"));
        Assert.That(source, Does.Contain("GameplayInputAction.Dodge"));
        Assert.That(source, Does.Contain("GameplayInputAction.Punch"));
        Assert.That(source, Does.Contain("GameplayInputAction.SlideTackle"));
        Assert.That(source, Does.Not.Contain("Keyboard.current"));
        Assert.That(source, Does.Not.Contain("Mouse.current"));
        Assert.That(source, Does.Not.Contain("PlayerActionBindings"));
        Assert.That(source, Does.Not.Contain("PlayerActionInputReader"));
    }
}
