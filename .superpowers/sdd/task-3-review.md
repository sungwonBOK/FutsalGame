Base: 432f4d9
Head: 53c02d7

53c02d7 refactor: route player controls through input actions

 .../Scripts/Runtime/Input/PlayerActionBindings.cs  | 40 --------------
 .../Runtime/Input/PlayerActionBindings.cs.meta     |  2 -
 .../Runtime/Input/PlayerActionInputReader.cs       | 62 ----------------------
 .../Runtime/Input/PlayerActionInputReader.cs.meta  |  2 -
 Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs  | 53 ++++++------------
 .../Tests/EditMode/PlayerActionInputReaderTests.cs | 43 +++++++--------
 .../Settings/DefaultPlayerActionBindings.asset     | 23 --------
 .../DefaultPlayerActionBindings.asset.meta         |  8 ---
 8 files changed, 33 insertions(+), 200 deletions(-)

diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs b/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs
deleted file mode 100644
index c599be2..0000000
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs
+++ /dev/null
@@ -1,40 +0,0 @@
-using System;
-using UnityEngine;
-using UnityEngine.InputSystem;
-
-public enum PlayerMouseButton
-{
-    None,
-    Left,
-    Right,
-    Middle
-}
-
-[Serializable]
-public struct PlayerActionBinding
-{
-    [SerializeField] private PlayerMouseButton mouseButton;
-    [SerializeField] private Key keyboardKey;
-
-    public PlayerActionBinding(PlayerMouseButton mouseButton, Key keyboardKey)
-    {
-        this.mouseButton = mouseButton;
-        this.keyboardKey = keyboardKey;
-    }
-
-    public PlayerMouseButton MouseButton => mouseButton;
-    public Key KeyboardKey => keyboardKey;
-    public string KeyboardKeyName => keyboardKey.ToString();
-}
-
-[CreateAssetMenu(menuName = "Futsal Brawl/Input/Player Action Bindings")]
-public class PlayerActionBindings : ScriptableObject
-{
-    [SerializeField] private PlayerActionBinding pass = new PlayerActionBinding(PlayerMouseButton.Left, Key.None);
-    [SerializeField] private PlayerActionBinding shot = new PlayerActionBinding(PlayerMouseButton.Right, Key.None);
-    [SerializeField] private PlayerActionBinding cancel = new PlayerActionBinding(PlayerMouseButton.None, Key.C);
-
-    public PlayerActionBinding Pass => pass;
-    public PlayerActionBinding Shot => shot;
-    public PlayerActionBinding Cancel => cancel;
-}
diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs.meta b/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs.meta
deleted file mode 100644
index dd68da8..0000000
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs.meta
+++ /dev/null
@@ -1,2 +0,0 @@
-fileFormatVersion: 2
-guid: 3a27b83d671d68846aacb9d2a0265062
\ No newline at end of file
diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs b/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs
deleted file mode 100644
index c333ac8..0000000
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs
+++ /dev/null
@@ -1,62 +0,0 @@
-using UnityEngine.InputSystem;
-using UnityEngine.InputSystem.Controls;
-
-public readonly struct ActionButtonState
-{
-    public ActionButtonState(bool wasPressed, bool isPressed, bool wasReleased)
-    {
-        WasPressed = wasPressed;
-        IsPressed = isPressed;
-        WasReleased = wasReleased;
-    }
-
-    public bool WasPressed { get; }
-    public bool IsPressed { get; }
-    public bool WasReleased { get; }
-}
-
-public static class PlayerActionInputReader
-{
-    public static ActionButtonState Read(PlayerActionBinding binding)
-    {
-        return Combine(
-            ReadControl(ResolveMouseControl(binding.MouseButton)),
-            ReadControl(ResolveKeyboardControl(binding.KeyboardKey)));
-    }
-
-    public static ActionButtonState Combine(ActionButtonState first, ActionButtonState second)
-    {
-        bool isPressed = first.IsPressed || second.IsPressed;
-        return new ActionButtonState(
-            first.WasPressed || second.WasPressed,
-            isPressed,
-            !isPressed && (first.WasReleased || second.WasReleased));
-    }
-
-    private static ActionButtonState ReadControl(ButtonControl control)
-    {
-        return control == null
-            ? default
-            : new ActionButtonState(control.wasPressedThisFrame, control.isPressed, control.wasReleasedThisFrame);
-    }
-
-    private static ButtonControl ResolveMouseControl(PlayerMouseButton button)
-    {
-        Mouse mouse = Mouse.current;
-        if (mouse == null)
-            return null;
-
-        return button switch
-        {
-            PlayerMouseButton.Left => mouse.leftButton,
-            PlayerMouseButton.Right => mouse.rightButton,
-            PlayerMouseButton.Middle => mouse.middleButton,
-            _ => null
-        };
-    }
-
-    private static ButtonControl ResolveKeyboardControl(Key key)
-    {
-        return key == Key.None || Keyboard.current == null ? null : Keyboard.current[key];
-    }
-}
diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs.meta b/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs.meta
deleted file mode 100644
index 4e39154..0000000
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs.meta
+++ /dev/null
@@ -1,2 +0,0 @@
-fileFormatVersion: 2
-guid: 88d48d42bbed2394c8f0038238589ee1
\ No newline at end of file
diff --git a/Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs b/Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs
index 79033e0..f6eea57 100644
--- a/Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs
+++ b/Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs
@@ -1,82 +1,58 @@
 using UnityEngine;
-using UnityEngine.InputSystem;
 
 public class PlayerInput : MonoBehaviour
 {
     [SerializeField] private Transform movementReference;
-    [SerializeField] private PlayerActionBindings actionBindings;
+    [SerializeField] private GameplayInputReader inputReader;
 
     private CharacterLocomotion locomotion;
     private CombatController combat;
     private PlayerBallHandler ball;
     private CharacterState state;
-    private PlayerActionBindings runtimeActionBindings;
-
-    private PlayerActionBindings ActionBindings
-    {
-        get
-        {
-            if (actionBindings != null)
-                return actionBindings;
-
-            if (runtimeActionBindings == null)
-                runtimeActionBindings = ScriptableObject.CreateInstance<PlayerActionBindings>();
-            return runtimeActionBindings;
-        }
-    }
 
     private void Awake()
     {
         locomotion = GetComponent<CharacterLocomotion>();
         if (locomotion == null)
             locomotion = gameObject.AddComponent<CharacterLocomotion>();
 
         combat = GetComponent<CombatController>();
         ball = GetComponent<PlayerBallHandler>();
         state = GetComponent<CharacterState>();
 
         if (movementReference == null && Camera.main != null)
             movementReference = Camera.main.transform;
     }
 
     private void Update()
     {
-        Keyboard kb = Keyboard.current;
-        if (kb == null && Mouse.current == null)
-            return;
-
         if (!GameManager.PlayActive || (state != null && state.IsStunned))
         {
             locomotion.SetPlayerMoveInput(Vector2.zero, sprint: false, hasBall: ball != null && ball.HasBall);
             if (ball != null)
                 ball.SetSprintDribbleInput(false, Vector3.zero);
             return;
         }
 
-        Vector2 moveInput = BuildMoveInput(
-            kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed),
-            kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed),
-            kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed),
-            kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed));
-
-        bool sprint = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
+        Vector2 moveInput = inputReader != null ? inputReader.ReadMove() : Vector2.zero;
+        bool sprint = inputReader != null && inputReader.ReadButton(GameplayInputAction.Sprint).IsPressed;
         bool hasBall = ball != null && ball.HasBall;
         Vector3 moveDirection = BuildCameraRelativeMoveDirection(moveInput, movementReference);
         locomotion.SetPlayerMoveInput(moveInput, moveDirection, sprint, hasBall);
 
         Vector3 actionDirection = locomotion.ActionDirection;
-        if (kb != null && kb.lKey.wasPressedThisFrame)
+        if (inputReader != null && inputReader.ReadButton(GameplayInputAction.Dodge).WasPressed)
             locomotion.TryDodge(actionDirection);
-        if (kb != null && kb.jKey.wasPressedThisFrame && combat != null)
+        if (inputReader != null && inputReader.ReadButton(GameplayInputAction.Punch).WasPressed && combat != null)
             combat.Punch(actionDirection);
-        if (kb != null && kb.kKey.wasPressedThisFrame && combat != null)
+        if (inputReader != null && inputReader.ReadButton(GameplayInputAction.SlideTackle).WasPressed && combat != null)
             combat.SlideTackle(actionDirection);
 
         if (ball != null)
         {
             ball.SetSprintDribbleInput(sprint, actionDirection);
             HandleBallActions();
         }
     }
 
     public static Vector2 BuildMoveInput(bool leftPressed, bool rightPressed, bool downPressed, bool upPressed)
@@ -106,23 +82,29 @@ public class PlayerInput : MonoBehaviour
         direction = direction.normalized;
         if (direction.sqrMagnitude > 0.0001f)
             return direction;
 
         fallbackForward.y = 0f;
         return fallbackForward.sqrMagnitude > 0.0001f ? fallbackForward.normalized : Vector3.forward;
     }
 
     private void HandleBallActions()
     {
-        ActionButtonState cancel = PlayerActionInputReader.Read(ActionBindings.Cancel);
-        ActionButtonState pass = PlayerActionInputReader.Read(ActionBindings.Pass);
-        ActionButtonState shot = PlayerActionInputReader.Read(ActionBindings.Shot);
+        GameplayInputButtonState cancel = inputReader != null
+            ? inputReader.ReadButton(GameplayInputAction.CancelCharge)
+            : default;
+        GameplayInputButtonState pass = inputReader != null
+            ? inputReader.ReadButton(GameplayInputAction.Pass)
+            : default;
+        GameplayInputButtonState shot = inputReader != null
+            ? inputReader.ReadButton(GameplayInputAction.Shot)
+            : default;
 
         if (cancel.WasPressed)
         {
             ball.CancelCharge();
             return;
         }
 
         if (ball.IsCharging)
         {
             Vector3 cameraForward = BuildPlanarCameraForward(movementReference, transform.forward);
@@ -132,16 +114,11 @@ public class PlayerInput : MonoBehaviour
                 ball.ReleaseCharge(BallChargeAction.Shot, cameraForward);
             return;
         }
 
         if (pass.WasPressed)
             ball.StartCharge(BallChargeAction.Pass);
         else if (shot.WasPressed)
             ball.StartCharge(BallChargeAction.Shot);
     }
 
-    private void OnDestroy()
-    {
-        if (runtimeActionBindings != null)
-            Destroy(runtimeActionBindings);
-    }
 }
diff --git a/Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs b/Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs
index 9306cc1..3d9b6c8 100644
--- a/Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs
+++ b/Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs
@@ -1,36 +1,29 @@
+using System.IO;
 using NUnit.Framework;
 using UnityEngine;
 
 public class PlayerActionInputReaderTests
 {
-    [Test]
-    public void DefaultBindings_UseMouseForBallActionsAndCForCancel()
-    {
-        PlayerActionBindings bindings = ScriptableObject.CreateInstance<PlayerActionBindings>();
-        try
-        {
-            Assert.That(bindings.Pass.MouseButton, Is.EqualTo(PlayerMouseButton.Left));
-            Assert.That(bindings.Pass.KeyboardKeyName, Is.EqualTo("None"));
-            Assert.That(bindings.Shot.MouseButton, Is.EqualTo(PlayerMouseButton.Right));
-            Assert.That(bindings.Shot.KeyboardKeyName, Is.EqualTo("None"));
-            Assert.That(bindings.Cancel.MouseButton, Is.EqualTo(PlayerMouseButton.None));
-            Assert.That(bindings.Cancel.KeyboardKeyName, Is.EqualTo("C"));
-        }
-        finally
-        {
-            Object.DestroyImmediate(bindings);
-        }
-    }
+    private static string PlayerInputPath => Path.Combine(
+        Application.dataPath,
+        "_Game/Scripts/Runtime/Input/PlayerInput.cs");
 
     [Test]
-    public void Combine_ReportsReleaseOnlyAfterEveryConfiguredAlternativeIsReleased()
+    public void PlayerInput_UsesSemanticGameplayInputActionsInsteadOfRawControls()
     {
-        ActionButtonState mouseState = new ActionButtonState(wasPressed: false, isPressed: true, wasReleased: false);
-        ActionButtonState keyboardState = new ActionButtonState(wasPressed: false, isPressed: false, wasReleased: true);
-
-        ActionButtonState combined = PlayerActionInputReader.Combine(mouseState, keyboardState);
+        string source = File.ReadAllText(PlayerInputPath);
 
-        Assert.That(combined.IsPressed, Is.True);
-        Assert.That(combined.WasReleased, Is.False);
+        Assert.That(source, Does.Contain("inputReader.ReadMove()"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Sprint"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Pass"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Shot"));
+        Assert.That(source, Does.Contain("GameplayInputAction.CancelCharge"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Dodge"));
+        Assert.That(source, Does.Contain("GameplayInputAction.Punch"));
+        Assert.That(source, Does.Contain("GameplayInputAction.SlideTackle"));
+        Assert.That(source, Does.Not.Contain("Keyboard.current"));
+        Assert.That(source, Does.Not.Contain("Mouse.current"));
+        Assert.That(source, Does.Not.Contain("PlayerActionBindings"));
+        Assert.That(source, Does.Not.Contain("PlayerActionInputReader"));
     }
 }
diff --git a/Assets/_Game/Settings/DefaultPlayerActionBindings.asset b/Assets/_Game/Settings/DefaultPlayerActionBindings.asset
deleted file mode 100644
index d8c5e02..0000000
--- a/Assets/_Game/Settings/DefaultPlayerActionBindings.asset
+++ /dev/null
@@ -1,23 +0,0 @@
-%YAML 1.1
-%TAG !u! tag:unity3d.com,2011:
---- !u!114 &11400000
-MonoBehaviour:
-  m_ObjectHideFlags: 0
-  m_CorrespondingSourceObject: {fileID: 0}
-  m_PrefabInstance: {fileID: 0}
-  m_PrefabAsset: {fileID: 0}
-  m_GameObject: {fileID: 0}
-  m_Enabled: 1
-  m_EditorHideFlags: 0
-  m_Script: {fileID: 11500000, guid: 3a27b83d671d68846aacb9d2a0265062, type: 3}
-  m_Name: DefaultPlayerActionBindings
-  m_EditorClassIdentifier: FutsalGame.Runtime::PlayerActionBindings
-  pass:
-    mouseButton: 1
-    keyboardKey: 0
-  shot:
-    mouseButton: 2
-    keyboardKey: 0
-  cancel:
-    mouseButton: 0
-    keyboardKey: 17
diff --git a/Assets/_Game/Settings/DefaultPlayerActionBindings.asset.meta b/Assets/_Game/Settings/DefaultPlayerActionBindings.asset.meta
deleted file mode 100644
index bb3faa6..0000000
--- a/Assets/_Game/Settings/DefaultPlayerActionBindings.asset.meta
+++ /dev/null
@@ -1,8 +0,0 @@
-fileFormatVersion: 2
-guid: a0d3b780fd6f5f5469d556bfba2eae03
-NativeFormatImporter:
-  externalObjects: {}
-  mainObjectFileID: 11400000
-  userData: 
-  assetBundleName: 
-  assetBundleVariant: 
