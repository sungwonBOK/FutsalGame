Base: acfde4e
Head: 432f4d9

432f4d9 feat: define gameplay input actions

 .../Tests/EditMode/GameplayInputReaderTests.cs     |  41 +++++
 .../Settings/InputSystem_Actions.inputactions      | 203 +++++++++++++++++++++
 2 files changed, 244 insertions(+)

diff --git a/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
index 2b6661a..dfb30ab 100644
--- a/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
+++ b/Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
@@ -1,17 +1,21 @@
 using System;
+using System.Collections;
+using System.Collections.Generic;
 using System.Reflection;
 using NUnit.Framework;
+using UnityEditor;
 using UnityEngine;
 
 public class GameplayInputReaderTests
 {
+    private const string InputActionsAssetPath = "Assets/_Game/Settings/InputSystem_Actions.inputactions";
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
@@ -83,20 +87,39 @@ public class GameplayInputReaderTests
 
             Assert.That(IsEnabled(playerMap), Is.False);
             Assert.That(IsEnabled(otherMap), Is.False);
         }
         finally
         {
             DestroyReaderAndAsset(reader, asset);
         }
     }
 
+    [Test]
+    public void PlayerMap_ContainsTheGameplayInputBindingContract()
+    {
+        ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(InputActionsAssetPath);
+
+        Assert.That(asset, Is.Not.Null, $"Expected input action asset at {InputActionsAssetPath}.");
+        AssertActionBindings(asset, "Move", "<Keyboard>/w", "<Keyboard>/upArrow", "<Keyboard>/a", "<Keyboard>/leftArrow", "<Keyboard>/s", "<Keyboard>/downArrow", "<Keyboard>/d", "<Keyboard>/rightArrow");
+        AssertActionBindings(asset, "Sprint", "<Keyboard>/leftShift", "<Keyboard>/rightShift");
+        AssertActionBindings(asset, "Pass", "<Mouse>/leftButton");
+        AssertActionBindings(asset, "Shot", "<Mouse>/rightButton");
+        AssertActionBindings(asset, "CancelCharge", "<Keyboard>/c");
+        AssertActionBindings(asset, "Dodge", "<Keyboard>/l");
+        AssertActionBindings(asset, "Punch", "<Keyboard>/j");
+        AssertActionBindings(asset, "SlideTackle", "<Keyboard>/k");
+        AssertActionBindings(asset, "Pause", "<Keyboard>/escape");
+        AssertActionBindings(asset, "Restart", "<Keyboard>/r", "<Keyboard>/space");
+        AssertActionBindings(asset, "ToggleLegacyCamera", "<Keyboard>/f5");
+    }
+
     private static GameplayInputReader CreateReader(ScriptableObject asset)
     {
         GameObject host = new GameObject("GameplayInputReaderTests");
         host.SetActive(false);
 
         GameplayInputReader reader = host.AddComponent<GameplayInputReader>();
         typeof(GameplayInputReader).GetField("inputActions", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reader, asset);
         host.SetActive(true);
         InvokeLifecycle(reader, "OnEnable");
         return reader;
@@ -111,20 +134,38 @@ public class GameplayInputReaderTests
     private static object FindAction(ScriptableObject asset, string actionName)
     {
         return asset.GetType().GetMethod("FindAction", new[] { typeof(string), typeof(bool) }).Invoke(asset, new object[] { actionName, false });
     }
 
     private static object FindActionMap(ScriptableObject asset, string mapName)
     {
         return asset.GetType().GetMethod("FindActionMap", new[] { typeof(string), typeof(bool) }).Invoke(asset, new object[] { mapName, false });
     }
 
+    private static void AssertActionBindings(ScriptableObject asset, string actionName, params string[] expectedPaths)
+    {
+        object action = FindAction(asset, actionName);
+        Assert.That(action, Is.Not.Null, $"Expected Player/{actionName} action.");
+
+        IEnumerable bindings = (IEnumerable)action.GetType().GetProperty("bindings").GetValue(action);
+        List<string> actualPaths = new List<string>();
+        foreach (object binding in bindings)
+        {
+            string path = (string)binding.GetType().GetProperty("effectivePath").GetValue(binding);
+            if (!string.IsNullOrEmpty(path))
+                actualPaths.Add(path);
+        }
+
+        foreach (string expectedPath in expectedPaths)
+            Assert.That(actualPaths, Does.Contain(expectedPath), $"Expected Player/{actionName} to bind {expectedPath}.");
+    }
+
     private static void ApplyBindingOverride(object action, string path)
     {
         Type extensions = Type.GetType("UnityEngine.InputSystem.InputActionRebindingExtensions, Unity.InputSystem");
         extensions.GetMethod("ApplyBindingOverride", new[] { action.GetType(), typeof(string), typeof(string), typeof(string) })
             .Invoke(null, new object[] { action, path, null, null });
     }
 
     private static bool IsEnabled(object map)
     {
         return (bool)map.GetType().GetProperty("enabled").GetValue(map);
diff --git a/Assets/_Game/Settings/InputSystem_Actions.inputactions b/Assets/_Game/Settings/InputSystem_Actions.inputactions
index 1a12cb9..b54f78a 100644
--- a/Assets/_Game/Settings/InputSystem_Actions.inputactions
+++ b/Assets/_Game/Settings/InputSystem_Actions.inputactions
@@ -1,11 +1,12 @@
 {
+    "version": 1,
     "name": "InputSystem_Actions",
     "maps": [
         {
             "name": "Player",
             "id": "df70fa95-8a34-4494-b137-73ab6b9c7d37",
             "actions": [
                 {
                     "name": "Move",
                     "type": "Value",
                     "id": "351f2ccd-1f9f-44bf-9bec-d62ac5c5f408",
@@ -78,20 +79,101 @@
                     "initialStateCheck": false
                 },
                 {
                     "name": "Sprint",
                     "type": "Button",
                     "id": "641cd816-40e6-41b4-8c3d-04687c349290",
                     "expectedControlType": "Button",
                     "processors": "",
                     "interactions": "",
                     "initialStateCheck": false
+                },
+                {
+                    "name": "Pass",
+                    "type": "Button",
+                    "id": "5203143b-32fb-4c22-b28f-95f12b256bbd",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Shot",
+                    "type": "Button",
+                    "id": "35a8803a-f38e-4342-86e1-3f90014d42e8",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "CancelCharge",
+                    "type": "Button",
+                    "id": "d1fd99e4-012e-4ab8-b25d-d215274822bb",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Dodge",
+                    "type": "Button",
+                    "id": "8133584f-c720-4dd1-9ccd-3e5330bb36b8",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Punch",
+                    "type": "Button",
+                    "id": "7481b8d5-28ca-4005-aab5-706cb0378ec2",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "SlideTackle",
+                    "type": "Button",
+                    "id": "2a718105-47b7-4533-a629-027489effa25",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Pause",
+                    "type": "Button",
+                    "id": "54757210-5c16-4219-8f8e-6f7e6800c0c3",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "Restart",
+                    "type": "Button",
+                    "id": "54040be0-8416-4656-83e1-b944c29b7286",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
+                },
+                {
+                    "name": "ToggleLegacyCamera",
+                    "type": "Button",
+                    "id": "80a4171f-307c-466d-b87f-3a140f92a1fd",
+                    "expectedControlType": "",
+                    "processors": "",
+                    "interactions": "",
+                    "initialStateCheck": false
                 }
             ],
             "bindings": [
                 {
                     "name": "",
                     "id": "978bfe49-cc26-4a3d-ab7b-7d7a29327403",
                     "path": "<Gamepad>/leftStick",
                     "interactions": "",
                     "processors": "",
                     "groups": ";Gamepad",
@@ -465,20 +547,141 @@
                 {
                     "name": "",
                     "id": "36e52cba-0905-478e-a818-f4bfcb9f3b9a",
                     "path": "<Keyboard>/c",
                     "interactions": "",
                     "processors": "",
                     "groups": "Keyboard&Mouse",
                     "action": "Crouch",
                     "isComposite": false,
                     "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "844b139f-fa28-4c57-9815-e5cff92e0663",
+                    "path": "<Keyboard>/rightShift",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Sprint",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "f044cd22-e7ee-4d48-8929-310ce9386948",
+                    "path": "<Mouse>/leftButton",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Pass",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "1eb7686d-cea9-4ebf-8a28-457b95c96b07",
+                    "path": "<Mouse>/rightButton",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Shot",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "58b4b755-2a67-400f-b093-96b62d581a9b",
+                    "path": "<Keyboard>/c",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "CancelCharge",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "356bbece-ba8a-4534-9496-5ed2e00950f4",
+                    "path": "<Keyboard>/l",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Dodge",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "0adb7143-c6da-4c45-98b1-0ca26b3cdedd",
+                    "path": "<Keyboard>/j",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Punch",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "7566c631-45d3-433f-9976-15e21071a730",
+                    "path": "<Keyboard>/k",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "SlideTackle",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "3e660857-130d-4cfb-a695-be3e19c6f56a",
+                    "path": "<Keyboard>/escape",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Pause",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "af566c8a-1726-4bad-acc8-ae4dbb3c33ab",
+                    "path": "<Keyboard>/r",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Restart",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "f2982f8c-f6f3-4ac7-9be7-35b809fbd048",
+                    "path": "<Keyboard>/space",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "Restart",
+                    "isComposite": false,
+                    "isPartOfComposite": false
+                },
+                {
+                    "name": "",
+                    "id": "839a1278-86f6-478d-82c6-637542beb25d",
+                    "path": "<Keyboard>/f5",
+                    "interactions": "",
+                    "processors": "",
+                    "groups": "Keyboard&Mouse",
+                    "action": "ToggleLegacyCamera",
+                    "isComposite": false,
+                    "isPartOfComposite": false
                 }
             ]
         },
         {
             "name": "UI",
             "id": "272f6d14-89ba-496f-b7ff-215263d3219f",
             "actions": [
                 {
                     "name": "Navigate",
                     "type": "PassThrough",
