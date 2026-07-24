using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
            GameplayInputButtonState missingAction = missingActionReader.ReadButton(GameplayInputAction.Pass);
            GameplayInputButtonState missingMap = missingMapReader.ReadButton(GameplayInputAction.Pass);

            Assert.That(missingAction.IsPressed || missingAction.WasPressed || missingAction.WasReleased, Is.False);
            Assert.That(missingActionReader.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(missingActionReader.GetBindingDisplayString(GameplayInputAction.Pass), Is.Empty);
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
        AssertActionBindings(asset, "Pass", "<Mouse>/leftButton");
        AssertActionBindings(asset, "Shot", "<Mouse>/rightButton");
        AssertActionBindings(asset, "CancelCharge", "<Keyboard>/c");
        AssertActionBindings(asset, "Dodge", "<Keyboard>/l");
        AssertActionBindings(asset, "Punch", "<Keyboard>/j");
        AssertActionBindings(asset, "SlideTackle", "<Keyboard>/k");
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
