Base: 6f6a5b1
Head: acfde4e

acfde4e test: strengthen gameplay input reader coverage
b5e4ceb feat: add gameplay input reader

 .superpowers/sdd/task-1-report.md                  |  51 ++++++++
 .../Scripts/Runtime/Input/GameplayInputAction.cs   |  28 ++++
 .../Runtime/Input/GameplayInputAction.cs.meta      |   2 +
 .../Scripts/Runtime/Input/GameplayInputReader.cs   |  68 ++++++++++
 .../Runtime/Input/GameplayInputReader.cs.meta      |   2 +
 .../Tests/EditMode/GameplayInputReaderTests.cs     | 143 +++++++++++++++++++++
 .../EditMode/GameplayInputReaderTests.cs.meta      |   2 +
 7 files changed, 296 insertions(+)

diff --git a/.superpowers/sdd/task-1-report.md b/.superpowers/sdd/task-1-report.md
new file mode 100644
index 0000000..2c0d6d8
--- /dev/null
+++ b/.superpowers/sdd/task-1-report.md
@@ -0,0 +1,51 @@
+# Task 1 report: gameplay input reader
+
+## Delivered files
+
+- `Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs`
+  - Adds the eleven semantic `GameplayInputAction` values and the independent `GameplayInputButtonState` value type.
+- `Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs`
+  - Serialized `InputActionAsset` reader that owns only the `Player` action map.
+  - Resolves semantic actions through one private mapping, exposes button/move/display reads, and returns neutral values for missing map/actions.
+- `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`
+  - Covers effective binding override display, missing Player map, missing action, and Player map enable/disable lifecycle.
+
+## TDD evidence
+
+1. RED: Created the display-override contract before runtime production code. Unity compilation failed with `CS0246` for missing `GameplayInputReader`; the focused job `1f62c8a36780493f865955c7cf8fdf3a` found zero tests because that compile failure prevented discovery.
+2. GREEN: Implemented the reader boundary and final focused job `d9b72795c636413ab0d0eb69b35f7345` passed all 4 EditMode tests in 0.095 seconds.
+
+## Unity checks
+
+- Targeted and pinned editor: `develop_merge_test@498cbd09b717313e`, Unity `6000.5.3f1`.
+- Editor was idle, not in Play Mode, and compilation/domain reload completed before each focused run.
+- Final console check had no compile errors. An existing MCP package transport warning (`WebSocket is not initialised`) was observed; it is outside project code and did not affect test execution.
+
+## Self-review
+
+- No changes to `PlayerInput`, scenes, input assets, ProjectSettings, or existing consumers.
+- `OnEnable`/`OnDisable` manipulate only the resolved `Player` map; missing assets/maps/actions are safe neutral reads.
+- Effective display strings are obtained from the action itself, so Unity Input System binding overrides are reflected.
+
+## Concerns / follow-up
+
+- `FutsalGame.EditModeTests.asmdef` does not reference `Unity.InputSystem`, while Task 1 scope excludes asmdef changes. The focused test therefore constructs and invokes the real Input System asset APIs through reflection; runtime production code directly uses the package as intended.
+- This task intentionally does not wire a reader into the scene or migrate consumers; that remains later approved tasks.
+
+## Review-fix report
+
+### Corrected lifecycle contract
+
+- Replaced the prior `Reader_EnablesAndDisablesOnlyItsPlayerMap` assertion, which observed only the Player map, with `Reader_EnablesOnlyPlayerMap_AndLeavesOtherMapDisabled`.
+- The amended asset includes a distinct `Other` map. The test proves Player is enabled by the reader while Other stays disabled, then proves both maps are disabled after `OnDisable`.
+
+### Fixture reduction
+
+- Reduced the focused fixture from four tests and a generic overload-resolution helper to three required contracts with direct reflected Input System signatures.
+- Kept only: effective override display, neutral values for absent map/action, and isolated Player-map ownership. The concise test remains necessary because the EditMode asmdef does not reference `Unity.InputSystem`; broader input feel remains a manual Play Mode concern for the later wiring task.
+
+### Amended verification
+
+- The original RED evidence above remains the Task 1 implementation RED (`CS0246` before the reader existed).
+- This review amendment is a coverage correction, not a behavior change: the existing production implementation already enables only `playerMap`, so the newly precise contract passed on its first valid run without a production edit. No artificial failing assertion or temporary production regression was introduced.
+- Unity EditMode job `e16be6b7d9b74f4f9a06b1caa9c48ee3`: **3 passed, 0 failed** in 0.091 seconds.
diff --git a/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs b/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs
new file mode 100644
index 0000000..9888375
--- /dev/null
+++ b/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs
@@ -0,0 +1,28 @@
+public enum GameplayInputAction
+{
+    Move,
+    Sprint,
+    Pass,
+    Shot,
+    CancelCharge,
+    Dodge,
+    Punch,
+    SlideTackle,
+    Pause,
+    Restart,
+    ToggleLegacyCamera
+}
+
+public readonly struct GameplayInputButtonState
+{
+    public GameplayInputButtonState(bool wasPressed, bool isPressed, bool wasReleased)
+    {
+        WasPressed = wasPressed;
+        IsPressed = isPressed;
+        WasReleased = wasReleased;
+    }
+
+    public bool WasPressed { get; }
+    public bool IsPressed { get; }
+    public bool WasReleased { get; }
+}
diff --git a/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs.meta b/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs.meta
new file mode 100644
index 0000000..cf2d61b
--- /dev/null
+++ b/Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs.meta
@@ -0,0 +1,2 @@
+fileFormatVersion: 2
+guid: c39b48c0fd949cd4f94a732e1a719d5b
\ No newline at end of file
diff --git a/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs b/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs
new file mode 100644
index 0000000..804a35c
--- /dev/null
+++ b/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs
@@ -0,0 +1,68 @@
+using System.Collections.Generic;
+using UnityEngine;
+using UnityEngine.InputSystem;
+
+public class GameplayInputReader : MonoBehaviour
+{
+    private static readonly IReadOnlyDictionary<GameplayInputAction, string> ActionNames =
+        new Dictionary<GameplayInputAction, string>
+        {
+            { GameplayInputAction.Move, "Move" },
+            { GameplayInputAction.Sprint, "Sprint" },
+            { GameplayInputAction.Pass, "Pass" },
+            { GameplayInputAction.Shot, "Shot" },
+            { GameplayInputAction.CancelCharge, "CancelCharge" },
+            { GameplayInputAction.Dodge, "Dodge" },
+            { GameplayInputAction.Punch, "Punch" },
+            { GameplayInputAction.SlideTackle, "SlideTackle" },
+            { GameplayInputAction.Pause, "Pause" },
+            { GameplayInputAction.Restart, "Restart" },
+            { GameplayInputAction.ToggleLegacyCamera, "ToggleLegacyCamera" }
+        };
+
+    [SerializeField] private InputActionAsset inputActions;
+
+    private InputActionMap playerMap;
+
+    private void OnEnable()
+    {
+        playerMap = inputActions != null ? inputActions.FindActionMap("Player", throwIfNotFound: false) : null;
+        playerMap?.Enable();
+    }
+
+    private void OnDisable()
+    {
+        playerMap?.Disable();
+    }
+
+    public GameplayInputButtonState ReadButton(GameplayInputAction action)
+    {
+        InputAction inputAction = ResolveAction(action);
+        return inputAction == null
+            ? default
+            : new GameplayInputButtonState(
+                inputAction.WasPressedThisFrame(),
+                inputAction.IsPressed(),
+                inputAction.WasReleasedThisFrame());
+    }
+
+    public Vector2 ReadMove()
+    {
+        InputAction inputAction = ResolveAction(GameplayInputAction.Move);
+        return inputAction != null ? inputAction.ReadValue<Vector2>() : Vector2.zero;
+    }
+
+    public string GetBindingDisplayString(GameplayInputAction action)
+    {
+        InputAction inputAction = ResolveAction(action);
+        return inputAction != null ? inputAction.GetBindingDisplayString() : string.Empty;
+    }
+
+    private InputAction ResolveAction(GameplayInputAction action)
+    {
+        if (playerMap == null || !ActionNames.TryGetValue(action, out string actionName))
+            return null;
+
+        return playerMap.FindAction(actionName, throwIfNotFound: false);
+    }
+}
diff --git a/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs.meta b/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs.meta
new file mode 100644
index 0000000..3bbdf9a
--- /dev/null
+++ b/Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs.meta
@@ -0,0 +1,2 @@
+fileFormatVersion: 2
+guid: f8f46aaa5a60f0b48afc5a8287f590e2
\ No newline at end of file
diff --git a/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
new file mode 100644
index 0000000..2b6661a
--- /dev/null
+++ b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
@@ -0,0 +1,143 @@
+using System;
+using System.Reflection;
+using NUnit.Framework;
+using UnityEngine;
+
+public class GameplayInputReaderTests
+{
+    private const string PlayerAndOtherMapsJson = @"{
+        ""name"": ""GameplayInputReaderTests"",
+        ""maps"": [
+            {
+                ""name"": ""Player"",
+                ""id"": ""9f5c8df2-8f9f-46db-8b7c-2bc95c6e3d90"",
+                ""actions"": [{ ""name"": ""ToggleLegacyCamera"", ""type"": ""Button"", ""id"": ""c1194513-d479-4684-8dc3-6c553ae94311"" }],
+                ""bindings"": [{ ""id"": ""d11a20e2-2a22-467e-9dc5-b51da781cb99"", ""path"": ""<Keyboard>/f5"", ""action"": ""ToggleLegacyCamera"" }]
+            },
+            {
+                ""name"": ""Other"",
+                ""id"": ""9bb8d8f6-e303-4f93-a582-9270c10e0ad9"",
+                ""actions"": [],
+                ""bindings"": []
+            }
+        ]
+    }";
+
+    [Test]
+    public void BindingDisplayString_UsesTheActionOverrideWhenPresent()
+    {
+        ScriptableObject asset = CreateInputAsset(PlayerAndOtherMapsJson);
+        GameplayInputReader reader = CreateReader(asset);
+        object action = FindAction(asset, "ToggleLegacyCamera");
+        ApplyBindingOverride(action, "<Keyboard>/f6");
+
+        try
+        {
+            Assert.That(reader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera), Is.EqualTo("F6"));
+        }
+        finally
+        {
+            DestroyReaderAndAsset(reader, asset);
+        }
+    }
+
+    [Test]
+    public void MissingMapOrAction_ReturnsNeutralStates()
+    {
+        ScriptableObject missingActionAsset = CreateInputAsset(PlayerAndOtherMapsJson);
+        GameplayInputReader missingActionReader = CreateReader(missingActionAsset);
+        ScriptableObject missingMapAsset = CreateInputAsset(@"{ ""name"": ""NoPlayerMap"", ""maps"": [] }");
+        GameplayInputReader missingMapReader = CreateReader(missingMapAsset);
+
+        try
+        {
+            GameplayInputButtonState missingAction = missingActionReader.ReadButton(GameplayInputAction.Pass);
+            GameplayInputButtonState missingMap = missingMapReader.ReadButton(GameplayInputAction.Pass);
+
+            Assert.That(missingAction.IsPressed || missingAction.WasPressed || missingAction.WasReleased, Is.False);
+            Assert.That(missingActionReader.ReadMove(), Is.EqualTo(Vector2.zero));
+            Assert.That(missingActionReader.GetBindingDisplayString(GameplayInputAction.Pass), Is.Empty);
+            Assert.That(missingMap.IsPressed || missingMap.WasPressed || missingMap.WasReleased, Is.False);
+        }
+        finally
+        {
+            DestroyReaderAndAsset(missingActionReader, missingActionAsset);
+            DestroyReaderAndAsset(missingMapReader, missingMapAsset);
+        }
+    }
+
+    [Test]
+    public void Reader_EnablesOnlyPlayerMap_AndLeavesOtherMapDisabled()
+    {
+        ScriptableObject asset = CreateInputAsset(PlayerAndOtherMapsJson);
+        GameplayInputReader reader = CreateReader(asset);
+        object playerMap = FindActionMap(asset, "Player");
+        object otherMap = FindActionMap(asset, "Other");
+
+        try
+        {
+            Assert.That(IsEnabled(playerMap), Is.True);
+            Assert.That(IsEnabled(otherMap), Is.False);
+
+            InvokeLifecycle(reader, "OnDisable");
+
+            Assert.That(IsEnabled(playerMap), Is.False);
+            Assert.That(IsEnabled(otherMap), Is.False);
+        }
+        finally
+        {
+            DestroyReaderAndAsset(reader, asset);
+        }
+    }
+
+    private static GameplayInputReader CreateReader(ScriptableObject asset)
+    {
+        GameObject host = new GameObject("GameplayInputReaderTests");
+        host.SetActive(false);
+
+        GameplayInputReader reader = host.AddComponent<GameplayInputReader>();
+        typeof(GameplayInputReader).GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reader, asset);
+        host.SetActive(true);
+        InvokeLifecycle(reader, "OnEnable");
+        return reader;
+    }
+
+    private static ScriptableObject CreateInputAsset(string json)
+    {
+        Type assetType = Type.GetType("UnityEngine.InputSystem.InputActionAsset, Unity.InputSystem");
+        return (ScriptableObject)assetType.GetMethod("FromJson", new[] { typeof(string) }).Invoke(null, new object[] { json });
+    }
+
+    private static object FindAction(ScriptableObject asset, string actionName)
+    {
+        return asset.GetType().GetMethod("FindAction", new[] { typeof(string), typeof(bool) }).Invoke(asset, new object[] { actionName, false });
+    }
+
+    private static object FindActionMap(ScriptableObject asset, string mapName)
+    {
+        return asset.GetType().GetMethod("FindActionMap", new[] { typeof(string), typeof(bool) }).Invoke(asset, new object[] { mapName, false });
+    }
+
+    private static void ApplyBindingOverride(object action, string path)
+    {
+        Type extensions = Type.GetType("UnityEngine.InputSystem.InputActionRebindingExtensions, Unity.InputSystem");
+        extensions.GetMethod("ApplyBindingOverride", new[] { action.GetType(), typeof(string), typeof(string), typeof(string) })
+            .Invoke(null, new object[] { action, path, null, null });
+    }
+
+    private static bool IsEnabled(object map)
+    {
+        return (bool)map.GetType().GetProperty("enabled").GetValue(map);
+    }
+
+    private static void InvokeLifecycle(GameplayInputReader reader, string methodName)
+    {
+        typeof(GameplayInputReader).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(reader, null);
+    }
+
+    private static void DestroyReaderAndAsset(GameplayInputReader reader, ScriptableObject asset)
+    {
+        UnityEngine.Object.DestroyImmediate(reader.gameObject);
+        UnityEngine.Object.DestroyImmediate(asset);
+    }
+}
diff --git a/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs.meta b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs.meta
new file mode 100644
index 0000000..c787e84
--- /dev/null
+++ b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs.meta
@@ -0,0 +1,2 @@
+fileFormatVersion: 2
+guid: 9ff307b99fc5b78418cf950801d39428
\ No newline at end of file
