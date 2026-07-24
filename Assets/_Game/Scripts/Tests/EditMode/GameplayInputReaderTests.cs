using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameplayInputReaderTests
{
    private const string PlayerMapJson = @"{
        ""name"": ""GameplayInputReaderTests"",
        ""maps"": [
            {
                ""name"": ""Player"",
                ""id"": ""9f5c8df2-8f9f-46db-8b7c-2bc95c6e3d90"",
                ""actions"": [
                    {
                        ""name"": ""ToggleLegacyCamera"",
                        ""type"": ""Button"",
                        ""id"": ""c1194513-d479-4684-8dc3-6c553ae94311"",
                        ""expectedControlType"": ""Button""
                    }
                ],
                ""bindings"": [
                    {
                        ""name"": """",
                        ""id"": ""d11a20e2-2a22-467e-9dc5-b51da781cb99"",
                        ""path"": ""<Keyboard>/f5"",
                        ""action"": ""ToggleLegacyCamera""
                    }
                ]
            }
        ]
    }";

    [Test]
    public void BindingDisplayString_UsesTheActionOverrideWhenPresent()
    {
        ScriptableObject asset = CreateInputAsset(PlayerMapJson);
        GameplayInputReader reader = CreateReader(asset);
        object action = Invoke(asset, "FindAction", "ToggleLegacyCamera");
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
    public void MissingMap_ReturnsNeutralStatesAndEmptyDisplayString()
    {
        ScriptableObject asset = CreateInputAsset(@"{ ""name"": ""NoPlayerMap"", ""maps"": [] }");
        GameplayInputReader reader = CreateReader(asset);

        try
        {
            GameplayInputButtonState state = reader.ReadButton(GameplayInputAction.Pass);

            Assert.That(state.WasPressed, Is.False);
            Assert.That(state.IsPressed, Is.False);
            Assert.That(state.WasReleased, Is.False);
            Assert.That(reader.ReadMove(), Is.EqualTo(Vector2.zero));
            Assert.That(reader.GetBindingDisplayString(GameplayInputAction.Pass), Is.Empty);
        }
        finally
        {
            DestroyReaderAndAsset(reader, asset);
        }
    }

    [Test]
    public void MissingAction_ReturnsNeutralButtonStateAndEmptyDisplayString()
    {
        ScriptableObject asset = CreateInputAsset(PlayerMapJson);
        GameplayInputReader reader = CreateReader(asset);

        try
        {
            GameplayInputButtonState state = reader.ReadButton(GameplayInputAction.Pass);

            Assert.That(state.WasPressed, Is.False);
            Assert.That(state.IsPressed, Is.False);
            Assert.That(state.WasReleased, Is.False);
            Assert.That(reader.GetBindingDisplayString(GameplayInputAction.Pass), Is.Empty);
        }
        finally
        {
            DestroyReaderAndAsset(reader, asset);
        }
    }

    [Test]
    public void Reader_EnablesAndDisablesOnlyItsPlayerMap()
    {
        ScriptableObject asset = CreateInputAsset(PlayerMapJson);
        GameplayInputReader reader = CreateReader(asset);
        object playerMap = Invoke(asset, "FindActionMap", "Player");

        try
        {
            Assert.That((bool)playerMap.GetType().GetProperty("enabled").GetValue(playerMap), Is.True);

            typeof(GameplayInputReader)
                .GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(reader, null);

            Assert.That((bool)playerMap.GetType().GetProperty("enabled").GetValue(playerMap), Is.False);
        }
        finally
        {
            DestroyReaderAndAsset(reader, asset);
        }
    }

    private static GameplayInputReader CreateReader(ScriptableObject asset)
    {
        GameObject host = new GameObject("GameplayInputReaderTests");
        host.SetActive(false);

        GameplayInputReader reader = host.AddComponent<GameplayInputReader>();
        typeof(GameplayInputReader)
            .GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(reader, asset);

        host.SetActive(true);
        typeof(GameplayInputReader)
            .GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(reader, null);
        return reader;
    }

    private static ScriptableObject CreateInputAsset(string json)
    {
        Type assetType = Type.GetType("UnityEngine.InputSystem.InputActionAsset, Unity.InputSystem");
        MethodInfo fromJson = assetType.GetMethod("FromJson", new[] { typeof(string) });
        return (ScriptableObject)fromJson.Invoke(null, new object[] { json });
    }

    private static void ApplyBindingOverride(object action, string path)
    {
        Type extensionsType = Type.GetType("UnityEngine.InputSystem.InputActionRebindingExtensions, Unity.InputSystem");
        MethodInfo method = extensionsType.GetMethods(BindingFlags.Static | BindingFlags.Public)
            .Single(candidate => candidate.Name == "ApplyBindingOverride"
                && candidate.GetParameters().Length >= 2
                && candidate.GetParameters()[0].ParameterType.IsInstanceOfType(action)
                && candidate.GetParameters()[1].ParameterType == typeof(string)
                && candidate.GetParameters().Skip(2).All(parameter => parameter.IsOptional));

        object[] arguments = new object[method.GetParameters().Length];
        arguments[0] = action;
        arguments[1] = path;
        for (int index = 2; index < arguments.Length; index++)
            arguments[index] = Type.Missing;

        method.Invoke(null, arguments);
    }

    private static object Invoke(object target, string methodName, params object[] suppliedArguments)
    {
        MethodInfo method = target.GetType().GetMethods()
            .Single(candidate => candidate.Name == methodName
                && candidate.GetParameters().Length >= suppliedArguments.Length
                && suppliedArguments.Select((argument, index) =>
                    argument == null || candidate.GetParameters()[index].ParameterType.IsInstanceOfType(argument)).All(matches => matches)
                && candidate.GetParameters().Skip(suppliedArguments.Length).All(parameter => parameter.IsOptional));

        object[] arguments = new object[method.GetParameters().Length];
        Array.Copy(suppliedArguments, arguments, suppliedArguments.Length);
        for (int index = suppliedArguments.Length; index < arguments.Length; index++)
            arguments[index] = Type.Missing;

        return method.Invoke(target, arguments);
    }

    private static void DestroyReaderAndAsset(GameplayInputReader reader, ScriptableObject asset)
    {
        UnityEngine.Object.DestroyImmediate(reader.gameObject);
        UnityEngine.Object.DestroyImmediate(asset);
    }
}
