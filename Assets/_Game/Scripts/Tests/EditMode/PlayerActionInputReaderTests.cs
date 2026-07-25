using System.IO;
using System.Reflection;
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

    [Test]
    public void PlayerInput_UsesSceneGameplayInputReaderWhenReferenceIsNull()
    {
        GameObject readerHost = new GameObject("Scene Input");
        GameplayInputReader sceneReader = readerHost.AddComponent<GameplayInputReader>();
        GameObject player = new GameObject("Net Player");
        player.SetActive(false);

        try
        {
            PlayerInput playerInput = player.AddComponent<PlayerInput>();
            player.SetActive(true);
            typeof(PlayerInput)
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(playerInput, null);

            GameplayInputReader resolvedReader = (GameplayInputReader)typeof(PlayerInput)
                .GetField("inputReader", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(playerInput);

            Assert.That(resolvedReader, Is.Not.Null);
            Assert.That(resolvedReader.gameObject, Is.Not.SameAs(player));
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(readerHost);
        }
    }
}
