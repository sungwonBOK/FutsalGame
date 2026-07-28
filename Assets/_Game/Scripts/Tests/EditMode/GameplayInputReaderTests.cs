using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameplayInputReaderTests
{
    private const string InputActionsAssetPath = "Assets/_Game/Settings/InputSystem_Actions.inputactions";
    private const string PlayerAndOtherMapsJson = @"{
        ""name"": ""GameplayInputReaderTests"",
        ""maps"": [
            {
                ""name"": ""Player"",
                ""id"": ""9f5c8df2-8f9f-46db-8b7c-2bc95c6e3d90"",
                ""actions"": [{ ""name"": ""ToggleLegacyCamera"", ""type"": ""Button"", ""id"": ""c1194513-d479-4684-8dc3-6c553ae94311"" }],
                ""bindings"": [{ ""id"": ""d11a20e2-2a22-467e-9dc5-b51da781cb99"", ""path"": ""<Keyboard>/f5"", ""action"": ""ToggleLegacyCamera"" }]
            },
            {
                ""name"": ""Other"",
                ""id"": ""9bb8d8f6-e303-4f93-a582-9270c10e0ad9"",
                ""actions"": [],
                ""bindings"": []
            }
        ]
    }";

    [Test]
    public void BindingDisplayString_UsesTheActionOverrideWhenPresent()
    {
        ScriptableObject asset = CreateInputAsset(PlayerAndOtherMapsJson);
        GameplayInputReader reader = CreateReader(asset);
        object action = FindAction(asset, "ToggleLegacyCamera");
        ApplyBindingOverride(action, "<Keyboard>/f6");

        try
        {
            Assert.That(reader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera), Is.EqualTo("F6"));
        }
        finally
        {
            DestroyReaderAndAsset(reader, asset);
        }
    }

    [Test]
    public void MissingMapOrAction_ReturnsNeutralStates()
    {
        ScriptableObject missingActionAsset = CreateInputAsset(PlayerAndOtherMapsJson);
        GameplayInputReader missingActionReader = CreateReader(missingActionAsset);
        ScriptableObject missingMapAsset = CreateInputAsset(@"{ ""name"": ""NoPlayerMap"", ""maps"": [] }");
        GameplayInputReader missingMapReader = CreateReader(missingMapAsset);

        try
        {
            GameplayInputButtonState missingAction = missingActionReader.ReadButton(GameplayInputAction.PrimaryAction);
            GameplayInputButtonState missingMap = missingMapReader.ReadButton(GameplayInputAction.PrimaryAction);

            Assert.That(missingAction.IsPressed || missingAction.WasPressed || missingAction.WasReleased, Is.False);
            Assert.That(missingActionReader.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(missingActionReader.GetBindingDisplayString(GameplayInputAction.PrimaryAction), Is.Empty);
            Assert.That(missingMap.IsPressed || missingMap.WasPressed || missingMap.WasReleased, Is.False);
        }
        finally
        {
            DestroyReaderAndAsset(missingActionReader, missingActionAsset);
            DestroyReaderAndAsset(missingMapReader, missingMapAsset);
        }
    }

    [Test]
    public void Reader_EnablesOnlyPlayerMap_AndLeavesOtherMapDisabled()
    {
        ScriptableObject asset = CreateInputAsset(PlayerAndOtherMapsJson);
        GameplayInputReader reader = CreateReader(asset);
        object playerMap = FindActionMap(asset, "Player");
        object otherMap = FindActionMap(asset, "Other");

        try
        {
            Assert.That(IsEnabled(playerMap), Is.True);
            Assert.That(IsEnabled(otherMap), Is.False);

            InvokeLifecycle(reader, "OnDisable");

            Assert.That(IsEnabled(playerMap), Is.False);
            Assert.That(IsEnabled(otherMap), Is.False);
        }
        finally
        {
            DestroyReaderAndAsset(reader, asset);
        }
    }

    [Test]
    public void PlayerMap_ContainsTheGameplayInputBindingContract()
    {
        ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(InputActionsAssetPath);

        Assert.That(asset, Is.Not.Null, $"Expected input action asset at {InputActionsAssetPath}.");
        AssertActionBindings(asset, "Move", "<Keyboard>/w", "<Keyboard>/upArrow", "<Keyboard>/a", "<Keyboard>/leftArrow", "<Keyboard>/s", "<Keyboard>/downArrow", "<Keyboard>/d", "<Keyboard>/rightArrow");
        AssertActionBindings(asset, "Sprint", "<Keyboard>/leftShift", "<Keyboard>/rightShift");
        AssertActionBindings(asset, "PrimaryAction", "<Mouse>/leftButton");
        AssertActionBindings(asset, "SecondaryAction", "<Mouse>/rightButton");
        AssertCompositeBinding(asset, "QueueOneTouchPass", "<Keyboard>/leftAlt", "<Mouse>/leftButton");
        AssertCompositeBinding(asset, "QueueOneTouchPass", "<Keyboard>/rightAlt", "<Mouse>/leftButton");
        AssertCompositeBinding(asset, "QueueOneTouchShot", "<Keyboard>/leftAlt", "<Mouse>/rightButton");
        AssertCompositeBinding(asset, "QueueOneTouchShot", "<Keyboard>/rightAlt", "<Mouse>/rightButton");
        AssertActionBindings(asset, "CancelAction", "<Keyboard>/c");
        AssertActionBindings(asset, "ContextQ", "<Keyboard>/q");
        AssertActionBindings(asset, "Grab", "<Keyboard>/e");
        AssertActionBindings(asset, "ContextF", "<Keyboard>/f");
        AssertActionBindings(asset, "Dodge", "<Keyboard>/space");
        Assert.That(FindAction(asset, "Pass"), Is.Null);
        Assert.That(FindAction(asset, "Shot"), Is.Null);
        Assert.That(AllBindingPaths(asset), Does.Not.Contain("<Keyboard>/k"));
        Assert.That(AllBindingPaths(asset), Does.Not.Contain("<Keyboard>/l"));
        AssertActionBindings(asset, "Pause", "<Keyboard>/escape");
        AssertActionBindings(asset, "Restart", "<Keyboard>/r", "<Keyboard>/space");
        AssertActionBindings(asset, "ToggleLegacyCamera", "<Keyboard>/f5");
    }

    private static GameplayInputReader CreateReader(ScriptableObject asset)
    {
        GameObject host = new GameObject("GameplayInputReaderTests");
        host.SetActive(false);

        GameplayInputReader reader = host.AddComponent<GameplayInputReader>();
        typeof(GameplayInputReader).GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reader, asset);
        host.SetActive(true);
        InvokeLifecycle(reader, "OnEnable");
        return reader;
    }

    private static ScriptableObject CreateInputAsset(string json)
    {
        Type assetType = Type.GetType("UnityEngine.InputSystem.InputActionAsset, Unity.InputSystem");
        return (ScriptableObject)assetType.GetMethod("FromJson", new[] { typeof(string) }).Invoke(null, new object[] { json });
    }

    private static object FindAction(ScriptableObject asset, string actionName)
    {
        return asset.GetType().GetMethod("FindAction", new[] { typeof(string), typeof(bool) }).Invoke(asset, new object[] { actionName, false });
    }

    private static object FindActionMap(ScriptableObject asset, string mapName)
    {
        return asset.GetType().GetMethod("FindActionMap", new[] { typeof(string), typeof(bool) }).Invoke(asset, new object[] { mapName, false });
    }

    private static void AssertActionBindings(ScriptableObject asset, string actionName, params string[] expectedPaths)
    {
        object action = FindAction(asset, actionName);
        Assert.That(action, Is.Not.Null, $"Expected Player/{actionName} action.");

        IEnumerable bindings = (IEnumerable)action.GetType().GetProperty("bindings").GetValue(action);
        List<string> actualPaths = new List<string>();
        foreach (object binding in bindings)
        {
            string path = (string)binding.GetType().GetProperty("effectivePath").GetValue(binding);
            if (!string.IsNullOrEmpty(path))
                actualPaths.Add(path);
        }

        foreach (string expectedPath in expectedPaths)
            Assert.That(actualPaths, Does.Contain(expectedPath), $"Expected Player/{actionName} to bind {expectedPath}.");
    }

    private static void AssertCompositeBinding(ScriptableObject asset, string actionName, string modifierPath, string buttonPath)
    {
        object action = FindAction(asset, actionName);
        Assert.That(action, Is.Not.Null, $"Expected Player/{actionName} action.");

        IEnumerable bindings = (IEnumerable)action.GetType().GetProperty("bindings").GetValue(action);
        bool foundComposite = false;
        foreach (object binding in bindings)
        {
            bool isComposite = (bool)binding.GetType().GetProperty("isComposite").GetValue(binding);
            if (!isComposite)
                continue;

            string path = (string)binding.GetType().GetProperty("path").GetValue(binding);
            if (path != "OneModifier")
                continue;

            foundComposite = HasCompositePart(bindings, "modifier", modifierPath)
                && HasCompositePart(bindings, "binding", buttonPath);
            if (foundComposite)
                break;
        }

        Assert.That(foundComposite, Is.True, $"Expected Player/{actionName} to have OneModifier {modifierPath} + {buttonPath}.");
    }

    private static bool HasCompositePart(IEnumerable bindings, string partName, string expectedPath)
    {
        foreach (object binding in bindings)
        {
            string name = (string)binding.GetType().GetProperty("name").GetValue(binding);
            string path = (string)binding.GetType().GetProperty("effectivePath").GetValue(binding);
            if (string.Equals(name, partName, StringComparison.OrdinalIgnoreCase) && path == expectedPath)
                return true;
        }

        return false;
    }

    private static List<string> AllBindingPaths(ScriptableObject asset)
    {
        var paths = new List<string>();
        IEnumerable maps = (IEnumerable)asset.GetType().GetProperty("actionMaps").GetValue(asset);
        foreach (object map in maps)
        {
            IEnumerable bindings = (IEnumerable)map.GetType().GetProperty("bindings").GetValue(map);
            foreach (object binding in bindings)
            {
                string path = (string)binding.GetType().GetProperty("effectivePath").GetValue(binding);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }
        }

        return paths;
    }

    private static void ApplyBindingOverride(object action, string path)
    {
        Type extensions = Type.GetType("UnityEngine.InputSystem.InputActionRebindingExtensions, Unity.InputSystem");
        extensions.GetMethod("ApplyBindingOverride", new[] { action.GetType(), typeof(string), typeof(string), typeof(string) })
            .Invoke(null, new object[] { action, path, null, null });
    }

    private static bool IsEnabled(object map)
    {
        return (bool)map.GetType().GetProperty("enabled").GetValue(map);
    }

    private static void InvokeLifecycle(GameplayInputReader reader, string methodName)
    {
        typeof(GameplayInputReader).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(reader, null);
    }

    private static void DestroyReaderAndAsset(GameplayInputReader reader, ScriptableObject asset)
    {
        UnityEngine.Object.DestroyImmediate(reader.gameObject);
        UnityEngine.Object.DestroyImmediate(asset);
    }
}

public class GameplayInputReaderDeviceTests : InputTestFixture
{
    private const string InputActionsAssetPath = "Assets/_Game/Settings/InputSystem_Actions.inputactions";

    [Test]
    public void ReadMove_ReturnsValueFromArrowKeyAlternativeBinding()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        GameplayInputReader reader = CreateReader(out InputActionAsset runtimeAsset);

        try
        {
            Press(keyboard.rightArrowKey);

            Assert.That(reader.ReadMove(), Is.EqualTo(Vector2.right));
        }
        finally
        {
            DestroyReaderAndAsset(reader, runtimeAsset);
        }
    }

    [Test]
    public void ReadButton_ReportsRightShiftPressHoldAndRelease()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        GameplayInputReader reader = CreateReader(out InputActionAsset runtimeAsset);

        try
        {
            Press(keyboard.rightShiftKey);
            GameplayInputButtonState pressed = reader.ReadButton(GameplayInputAction.Sprint);

            InputSystem.Update();
            GameplayInputButtonState held = reader.ReadButton(GameplayInputAction.Sprint);

            Release(keyboard.rightShiftKey);
            GameplayInputButtonState released = reader.ReadButton(GameplayInputAction.Sprint);

            Assert.That(pressed.WasPressed, Is.True);
            Assert.That(pressed.IsPressed, Is.True);
            Assert.That(pressed.WasReleased, Is.False);
            Assert.That(held.WasPressed, Is.False);
            Assert.That(held.IsPressed, Is.True);
            Assert.That(held.WasReleased, Is.False);
            Assert.That(released.WasPressed, Is.False);
            Assert.That(released.IsPressed, Is.False);
            Assert.That(released.WasReleased, Is.True);
        }
        finally
        {
            DestroyReaderAndAsset(reader, runtimeAsset);
        }
    }

    [Test]
    public void AltLeftClick_TriggersOnlyTheOneTouchPassAction()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        GameplayInputReader reader = CreateReader(out InputActionAsset runtimeAsset);

        try
        {
            Press(keyboard.leftAltKey);
            Press(mouse.leftButton);

            Assert.That(reader.ReadButton(GameplayInputAction.QueueOneTouchPass).WasPressed, Is.True);
            Assert.That(reader.ReadButton(GameplayInputAction.PrimaryAction).WasPressed, Is.False);
        }
        finally
        {
            DestroyReaderAndAsset(reader, runtimeAsset);
        }
    }

    private static GameplayInputReader CreateReader(out InputActionAsset runtimeAsset)
    {
        InputActionAsset sourceAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsAssetPath);
        Assert.That(sourceAsset, Is.Not.Null, $"Expected input action asset at {InputActionsAssetPath}.");

        runtimeAsset = InputActionAsset.FromJson(sourceAsset.ToJson());
        GameObject host = new GameObject("GameplayInputReaderDeviceTests");
        GameplayInputReader reader = host.AddComponent<GameplayInputReader>();
        typeof(GameplayInputReader)
            .GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(reader, runtimeAsset);
        typeof(GameplayInputReader)
            .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(reader, null);
        return reader;
    }

    private static void DestroyReaderAndAsset(GameplayInputReader reader, InputActionAsset runtimeAsset)
    {
        typeof(GameplayInputReader)
            .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(reader, null);
        UnityEngine.Object.DestroyImmediate(reader.gameObject);
        UnityEngine.Object.DestroyImmediate(runtimeAsset);
    }
}

public class ContextualPlayerActionRouterTests : InputTestFixture
{
    private const string ContextFInputJson = @"{
        ""name"": ""ContextualPlayerActionRouterTests"",
        ""maps"": [
            {
                ""name"": ""Player"",
                ""id"": ""e8f7c1cb-0bcb-40e4-b754-53218eacfe58"",
                ""actions"": [
                    { ""name"": ""ContextF"", ""type"": ""Button"", ""id"": ""985a5fc4-0583-4310-a2c8-0b2e2fa42a37"" },
                    { ""name"": ""SecondaryAction"", ""type"": ""Button"", ""id"": ""9cc128f2-0e0a-4c9c-b785-449a3d4df9d3"" }
                ],
                ""bindings"": [
                    { ""id"": ""c7994d7c-f230-4994-9b02-a6c53aea5ae8"", ""path"": ""<Keyboard>/f"", ""action"": ""ContextF"" },
                    { ""id"": ""7dbd2a8a-af4e-4dfa-b1cb-45b89fbc493a"", ""path"": ""<Mouse>/rightButton"", ""action"": ""SecondaryAction"" }
                ]
            }
        ]
    }";

    [Test]
    public void ContextF_StartsTackleWhenPlayerDoesNotHaveBall()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        InputActionAsset inputAsset = InputActionAsset.FromJson(ContextFInputJson);
        GameplayInputReader reader = CreateReader(inputAsset);
        GameObject player = new GameObject("Context F Player");
        player.AddComponent<Rigidbody>();
        CharacterState state = player.AddComponent<CharacterState>();
        CharacterMotor motor = player.AddComponent<CharacterMotor>();
        CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
        CombatController combat = player.AddComponent<CombatController>();
        ContextualPlayerActionRouter router = new ContextualPlayerActionRouter(locomotion, combat, ball: null);

        try
        {
            InvokeAwake(state);
            InvokeAwake(motor);
            InvokeAwake(locomotion);
            InvokeAwake(combat);

            Press(keyboard.fKey);
            Assert.That(reader.ReadButton(GameplayInputAction.ContextF).WasPressed, Is.True);

            router.Process(reader, Vector3.forward, Vector3.forward);

            Assert.That(motor.IsDashing, Is.True);
        }
        finally
        {
            typeof(GameplayInputReader)
                .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(reader, null);
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(reader.gameObject);
            UnityEngine.Object.DestroyImmediate(inputAsset);
        }
    }

    [Test]
    public void SecondaryAction_StartsCrossPunchWhenPlayerDoesNotHaveBall()
    {
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        InputActionAsset inputAsset = InputActionAsset.FromJson(ContextFInputJson);
        GameplayInputReader reader = CreateReader(inputAsset);
        GameObject player = new GameObject("Cross Punch Player");
        player.AddComponent<Rigidbody>();
        CharacterState state = player.AddComponent<CharacterState>();
        CharacterMotor motor = player.AddComponent<CharacterMotor>();
        CharacterLocomotion locomotion = player.AddComponent<CharacterLocomotion>();
        CombatController combat = player.AddComponent<CombatController>();
        ContextualPlayerActionRouter router = new ContextualPlayerActionRouter(locomotion, combat, ball: null);

        try
        {
            InvokeAwake(state);
            InvokeAwake(motor);
            InvokeAwake(locomotion);
            InvokeAwake(combat);

            Press(mouse.rightButton);
            Assert.That(reader.ReadButton(GameplayInputAction.SecondaryAction).WasPressed, Is.True);

            router.Process(reader, Vector3.forward, Vector3.forward);

            Assert.That(combat.CrossPunchRemaining, Is.GreaterThan(0f));
        }
        finally
        {
            typeof(GameplayInputReader)
                .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(reader, null);
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(reader.gameObject);
            UnityEngine.Object.DestroyImmediate(inputAsset);
        }
    }

    private static GameplayInputReader CreateReader(InputActionAsset inputAsset)
    {
        GameObject host = new GameObject("ContextualPlayerActionRouterTests");
        host.SetActive(false);

        GameplayInputReader reader = host.AddComponent<GameplayInputReader>();
        typeof(GameplayInputReader)
            .GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(reader, inputAsset);
        host.SetActive(true);
        typeof(GameplayInputReader)
            .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(reader, null);
        return reader;
    }

    private static void InvokeAwake(Component component)
    {
        component.GetType()
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(component, null);
    }
}
