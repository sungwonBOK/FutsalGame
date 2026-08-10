using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PlayerActionInputReaderTests
{
    private static string PlayerInputPath => Path.Combine(
        Application.dataPath,
        "_Game/Scripts/Runtime/Input/PlayerInput.cs");

    private static string ContextualRouterPath => Path.Combine(
        Application.dataPath,
        "_Game/Scripts/Runtime/Input/ContextualPlayerActionRouter.cs");

    [Test]
    public void PlayerInput_UsesSemanticGameplayInputActionsInsteadOfRawControls()
    {
        string source = File.ReadAllText(PlayerInputPath);
        string routerSource = File.ReadAllText(ContextualRouterPath);

        Assert.That(source, Does.Contain("inputReader.ReadMove()"));
        Assert.That(source, Does.Contain("GameplayInputAction.Sprint"));
        Assert.That(source, Does.Contain("actionRouter?.Process"));
        Assert.That(routerSource, Does.Contain("GameplayInputAction.PrimaryAction"));
        Assert.That(routerSource, Does.Contain("GameplayInputAction.SecondaryAction"));
        Assert.That(routerSource, Does.Contain("GameplayInputAction.QueueOneTouchPass"));
        Assert.That(routerSource, Does.Contain("GameplayInputAction.QueueOneTouchShot"));
        Assert.That(routerSource, Does.Contain("GameplayInputAction.CancelAction"));
        Assert.That(routerSource, Does.Contain("GameplayInputAction.Dodge"));
        Assert.That(source, Does.Not.Contain("Keyboard.current"));
        Assert.That(source, Does.Not.Contain("Mouse.current"));
        Assert.That(source, Does.Not.Contain("PlayerActionBindings"));
        Assert.That(source, Does.Not.Contain("PlayerActionInputReader"));
    }

    [Test]
    public void PlayerInput_AndActionRouter_UseThePowerActivationInputContract()
    {
        string source = File.ReadAllText(PlayerInputPath);
        string routerSource = File.ReadAllText(ContextualRouterPath);

        Assert.That(source, Does.Contain("GameplayInputAction.PowerActivation"));
        Assert.That(source, Does.Contain("EnhancedActionKind.BurstSprint"));
        Assert.That(routerSource, Does.Contain("PowerActivationController"));
        Assert.That(routerSource, Does.Contain("EnhancedActionKind.Primary"));
        Assert.That(routerSource, Does.Contain("EnhancedActionKind.Secondary"));
        Assert.That(routerSource, Does.Contain("EnhancedActionKind.Defense"));
        Assert.That(routerSource, Does.Contain("EnhancedActionKind.Grab"));
        Assert.That(routerSource, Does.Contain("EnhancedActionKind.SlideTackle"));
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
