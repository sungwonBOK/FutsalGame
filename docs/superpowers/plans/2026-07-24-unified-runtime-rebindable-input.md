# Unified Runtime-Rebindable Input Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route all listed controls, including WASD and arrow movement, through Unity Input System actions while preserving gameplay behavior and creating a stable boundary for future runtime rebinding.

**Architecture:** `InputSystem_Actions.inputactions` owns default physical bindings. `GameplayInputReader` owns action-map state and exposes semantic states; player, match, camera, and UI consumers receive only those states. Runtime rebinding services and persistence remain deferred, but action names and effective display strings are provided now.

**Tech Stack:** Unity 6000.5.3f1, Unity Input System, C#, Unity Test Framework, Unity MCP.

## Global Constraints

- Preserve the camera-relative movement calculation and every requested default control value.
- Modify `.inputactions`, scene, and asset references only through Unity Editor/MCP operations.
- Do not touch the pre-existing `ProjectSettings/ProjectSettings.asset` change.
- Keep tests to small input-contract and consumer-routing coverage; manual Play Mode control verification is explicitly required.
- Do not add runtime rebinding UI, persistence files, conflict policy, or gamepad expansion beyond existing bindings.

---

### Task 1: Define action names and create the input reader

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs`
- Create: `Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`

**Interfaces:**
- Produces: `GameplayInputAction` enum values `Move`, `Sprint`, `Pass`, `Shot`, `CancelCharge`, `Dodge`, `Punch`, `SlideTackle`, `Pause`, `Restart`, and `ToggleLegacyCamera`.
- Produces: `GameplayInputReader.ReadButton(GameplayInputAction action)`, `ReadMove()`, and `GetBindingDisplayString(GameplayInputAction action)`.
- Consumes: a serialized `InputActionAsset` with a `Player` map.

- [ ] **Step 1: Write the failing reader-contract test**

```csharp
[Test]
public void BindingDisplayString_UsesTheActionOverrideWhenPresent()
{
    InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
    asset.AddActionMap("Player")
        .AddAction("ToggleLegacyCamera", InputActionType.Button)
        .AddBinding("<Keyboard>/f5");
    GameplayInputReader reader = CreateReader(asset);
    asset.FindAction("ToggleLegacyCamera").ApplyBindingOverride("<Keyboard>/f6");

    Assert.That(reader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera), Is.EqualTo("F6"));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run: Unity EditMode `GameplayInputReaderTests.BindingDisplayString_UsesTheActionOverrideWhenPresent`.

Expected: compile failure because `GameplayInputReader` and `GameplayInputAction` do not exist.

- [ ] **Step 3: Implement the minimal input boundary**

```csharp
public enum GameplayInputAction { Move, Sprint, Pass, Shot, CancelCharge, Dodge, Punch, SlideTackle, Pause, Restart, ToggleLegacyCamera }

public ActionButtonState ReadButton(GameplayInputAction action);
public Vector2 ReadMove();
public string GetBindingDisplayString(GameplayInputAction action);
```

Resolve each enum value through one private action-name map, return neutral states for a missing map/action, and enable/disable only the reader's `Player` map.

- [ ] **Step 4: Run the focused test and verify GREEN**

Run: Unity EditMode `GameplayInputReaderTests`.

Expected: PASS, including neutral-state and display-override assertions.

- [ ] **Step 5: Commit the reader boundary**

```powershell
git add Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
git commit -m "feat: add gameplay input reader"
```

### Task 2: Move every requested default binding into the Input Action asset

**Files:**
- Modify through Unity Editor/MCP: `Assets/_Game/Settings/InputSystem_Actions.inputactions`
- Test: `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`

**Interfaces:**
- Consumes: the `GameplayInputAction` names from Task 1.
- Produces: the `Player` map actions used by every consumer.

- [ ] **Step 1: Extend the failing binding-contract test**

```csharp
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
```

- [ ] **Step 2: Run the focused test and verify RED**

Run: Unity EditMode `GameplayInputReaderTests`.

Expected: FAIL because the newly named actions/bindings are absent or `Sprint` lacks right Shift.

- [ ] **Step 3: Update the action asset through Unity Editor/MCP**

In the existing `Player` map, retain and configure `Move` as the current keyboard composite plus arrow alternatives; add the missing right-Shift sprint binding; add `Pass`, `Shot`, `CancelCharge`, `Dodge`, `Punch`, `SlideTackle`, `Pause`, `Restart`, and `ToggleLegacyCamera` with exactly the bindings listed in Step 1. Do not delete the existing generic actions.

- [ ] **Step 4: Run the focused test and verify GREEN**

Run: Unity EditMode `GameplayInputReaderTests`.

Expected: PASS with every default binding present.

- [ ] **Step 5: Commit the input asset contract**

```powershell
git add Assets/_Game/Settings/InputSystem_Actions.inputactions Assets/_Game/Settings/InputSystem_Actions.inputactions.meta Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
git commit -m "feat: define gameplay input actions"
```

### Task 3: Route player actions through semantic input

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs`
- Delete after replacement: `Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs`
- Delete after replacement: `Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs`
- Delete through Unity Editor/MCP after replacement: `Assets/_Game/Settings/DefaultPlayerActionBindings.asset`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs`

**Interfaces:**
- Consumes: `GameplayInputReader.ReadMove()` and `ReadButton(GameplayInputAction action)` from Task 1.
- Produces: unchanged calls to `CharacterLocomotion`, `PlayerBallHandler`, and `CombatController`.

- [ ] **Step 1: Replace the legacy test with a failing semantic-routing test**

```csharp
[Test]
public void PlayerInput_UsesMoveAndSprintActionsInsteadOfRawKeyboardControls()
{
    string source = File.ReadAllText(PlayerInputPath);
    Assert.That(source, Does.Contain("inputReader.ReadMove()"));
    Assert.That(source, Does.Not.Contain("Keyboard.current"));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run: Unity EditMode `PlayerActionInputReaderTests`.

Expected: FAIL because `PlayerInput` still reads keyboard and legacy bindings.

- [ ] **Step 3: Implement minimal semantic routing**

```csharp
Vector2 moveInput = inputReader.ReadMove();
bool sprint = inputReader.ReadButton(GameplayInputAction.Sprint).IsPressed;
ActionButtonState pass = inputReader.ReadButton(GameplayInputAction.Pass);
```

Use the reader for `Move`, sprint, dodge, punch, slide, pass, shot, and cancel. Preserve the current `GameManager.PlayActive`, stun, charge-release, action-direction, and camera-relative movement logic. Remove raw `Keyboard`/`Mouse` use and the legacy binding asset reader only after the replacement compiles.

- [ ] **Step 4: Run focused input and existing movement/ball tests**

Run: Unity EditMode `PlayerActionInputReaderTests`, `CameraInputDirectionTests`, and `BallInteractionControllerTests`.

Expected: PASS.

- [ ] **Step 5: Commit player migration**

```powershell
git add Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs Assets/_Game/Scripts/Runtime/Input Assets/_Game/Scripts/Tests/EditMode
git commit -m "refactor: route player controls through input actions"
```

### Task 4: Route match, camera, and binding display consumers

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Match/GameManager.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs`
- Modify: `Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/MatchResetTests.cs`

**Interfaces:**
- Consumes: a serialized `GameplayInputReader` reference and its semantic button states.
- Produces: unchanged pause, restart, camera-toggle, and hint behavior.

- [ ] **Step 1: Write failing consumer-routing checks**

```csharp
Assert.That(File.ReadAllText(GameManagerPath), Does.Contain("GameplayInputAction.Pause"));
Assert.That(File.ReadAllText(CameraSwitcherPath), Does.Contain("GameplayInputAction.ToggleLegacyCamera"));
Assert.That(File.ReadAllText(ViewHintPath), Does.Contain("GetBindingDisplayString"));
```

- [ ] **Step 2: Run focused tests and verify RED**

Run: Unity EditMode `MatchResetTests` and the routing checks.

Expected: FAIL because each consumer still reads a raw key or embeds `F5`.

- [ ] **Step 3: Implement consumer routing**

```csharp
if (inputReader.ReadButton(GameplayInputAction.Pause).WasPressed)
    TogglePause();

if (inputReader.ReadButton(GameplayInputAction.ToggleLegacyCamera).WasPressed)
    thirdPerson = !thirdPerson;
```

Keep pause/restart readable outside active gameplay. Replace the visible fixed `F5` text with `inputReader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera)`.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run: Unity EditMode `MatchResetTests`, input routing checks, and `ThirdPersonActionCameraTests`.

Expected: PASS.

- [ ] **Step 5: Commit global-consumer migration**

```powershell
git add Assets/_Game/Scripts/Runtime/Match/GameManager.cs Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs Assets/_Game/Scripts/Tests/EditMode
git commit -m "refactor: route global controls through input actions"
```

### Task 5: Wire the active scene and verify the final boundary

**Files:**
- Modify through Unity Editor/MCP: `Assets/_Game/Scenes/SampleScene.unity`
- Modify: `IMPLEMENTATION_STATUS.md`

**Interfaces:**
- Consumes: the action asset and `GameplayInputReader` from Tasks 1-4.
- Produces: assigned reader references on PlayerInput, GameManager, CameraViewSwitcher, and ViewHintUI.

- [ ] **Step 1: Inspect before scene mutation**

Use Unity MCP to verify the active `SampleScene`, the Player, GameManager, Main Camera, and UI host components, then confirm the editor is idle.

- [ ] **Step 2: Assign references through Unity Editor/MCP**

Add `GameplayInputReader` to the selected scene input host, assign `InputSystem_Actions.inputactions`, and set the same reader reference on PlayerInput, GameManager, CameraViewSwitcher, and ViewHintUI. Remove the obsolete PlayerActionBindings reference only after all references resolve.

- [ ] **Step 3: Wait for compilation and check the console**

Poll `mcpforunity://editor/state` until compilation and domain reload complete, then query Unity console errors and warnings.

Expected: no compile errors.

- [ ] **Step 4: Run concise automated verification**

Run: focused input tests, then the full Unity EditMode suite.

Expected: all discovered EditMode tests pass.

- [ ] **Step 5: Review and document**

Run `git diff --check`, inspect the changed-file list, and update `IMPLEMENTATION_STATUS.md` with the action-asset/reader boundary and test result. Post the actual file scope, verification, manual Play Mode checklist, and risks to issue #1, with coordinated notes for issues #2, #3, #4, #5, and #7.

- [ ] **Step 6: Commit the scene wiring and status**

```powershell
git add Assets/_Game/Scenes/SampleScene.unity IMPLEMENTATION_STATUS.md
git commit -m "feat: wire unified gameplay input"
```
