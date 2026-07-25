# Contextual Player Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace role-specific mouse/key bindings with contextual player actions, and add a persistent Alt one-touch intent that whiffs safely without possession.

**Architecture:** `InputSystem_Actions.inputactions` owns context-neutral physical actions and `GameplayInputReader` exposes their button states. `PlayerInput` retains movement and delegates gameplay action precedence to `ContextualPlayerActionRouter`; `Ball/OneTouchIntentBuffer` retains the latest intent while `Ball/OneTouchActionExecutor` owns immediate with-ball execution and no-ball whiff presentation.

**Tech Stack:** Unity 6000.5.3f1, Unity Input System, C#, Unity Test Framework, Unity MCP.

**Implementation note (2026-07-26):** The user requested short automated coverage. Buffer/input-contract/router-boundary tests were added and passed; with-ball presentation and Play Mode feel remain manual checks instead of a large scene fixture.

## Global Constraints

- Modify the Input Action asset only through Unity Editor/MCP; never edit its YAML directly.
- Do not touch `ProjectSettings/ProjectSettings.asset`.
- Mouse look is out of scope. K and L are not retained as aliases.
- Do not add strong attack, protect-ball, guard/parry, grab, through pass, special pass, or preparation movement penalties.
- A one-touch intent persists until C cancels it, an execution succeeds, or player/match reset clears it.

---

### Task 1: Add pure one-touch intent state

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Ball/OneTouchIntent.cs`
- Create: `Assets/_Game/Scripts/Runtime/Ball/OneTouchIntentBuffer.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/OneTouchIntentBufferTests.cs`

**Interfaces:** Produce `OneTouchIntent { None, Pass, Shot }` and a pure buffer with `Intent`, `IsPreparing`, `Queue`, `Clear`, and `Consume`. Queueing Pass or Shot replaces the prior intent; Queueing None clears it.

- [ ] **Step 1: Write the failing buffer tests**

```csharp
[Test]
public void Queue_ReplacesPriorIntentAndMarksPreparation()
{
    var buffer = new OneTouchIntentBuffer();
    buffer.Queue(OneTouchIntent.Pass);
    buffer.Queue(OneTouchIntent.Shot);

    Assert.That(buffer.Intent, Is.EqualTo(OneTouchIntent.Shot));
    Assert.That(buffer.IsPreparing, Is.True);
}

[Test]
public void ClearAndConsume_RemoveTheQueuedIntent()
{
    var buffer = new OneTouchIntentBuffer();
    buffer.Queue(OneTouchIntent.Pass);
    Assert.That(buffer.Consume(), Is.EqualTo(OneTouchIntent.Pass));
    Assert.That(buffer.IsPreparing, Is.False);

    buffer.Queue(OneTouchIntent.Shot);
    buffer.Clear();
    Assert.That(buffer.Intent, Is.EqualTo(OneTouchIntent.None));
}
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run Unity EditMode `OneTouchIntentBufferTests`. Expected: compile failure because the one-touch type and buffer do not exist.

- [ ] **Step 3: Implement the minimal state holder**

```csharp
public enum OneTouchIntent { None, Pass, Shot }

public sealed class OneTouchIntentBuffer
{
    public OneTouchIntent Intent { get; private set; }
    public bool IsPreparing => Intent != OneTouchIntent.None;
    public void Queue(OneTouchIntent intent) => Intent = intent;
    public void Clear() => Intent = OneTouchIntent.None;
    public OneTouchIntent Consume() { var intent = Intent; Clear(); return intent; }
}
```

- [ ] **Step 4: Run the focused tests and verify GREEN**

Run Unity EditMode `OneTouchIntentBufferTests`. Expected: replacement and clear/consume tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Assets/_Game/Scripts/Runtime/Ball/OneTouchIntent.cs Assets/_Game/Scripts/Runtime/Ball/OneTouchIntentBuffer.cs Assets/_Game/Scripts/Tests/EditMode/OneTouchIntentBufferTests.cs
git commit -m "feat: add one touch intent buffer"
```

### Task 2: Define conflict-free contextual input actions

**Files:**
- Modify through Unity Editor/MCP: `Assets/_Game/Settings/InputSystem_Actions.inputactions`
- Modify: `Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`

**Interfaces:** Keep Move, Sprint, Dodge, Pause, Restart, and ToggleLegacyCamera. Replace reader-visible Pass, Shot, CancelCharge, Punch, and SlideTackle with PrimaryAction, SecondaryAction, QueueOneTouchPass, QueueOneTouchShot, CancelAction, ContextQ, Grab, and ContextF. Primary/secondary bind only plain LMB/RMB; every queue action has separate left-Alt and right-Alt OneModifier composites.

- [ ] **Step 1: Write the failing action-asset contract**

```csharp
AssertActionBindings(asset, "PrimaryAction", "<Mouse>/leftButton");
AssertActionBindings(asset, "SecondaryAction", "<Mouse>/rightButton");
AssertCompositeBinding(asset, "QueueOneTouchPass", "OneModifier", "<Keyboard>/leftAlt", "<Mouse>/leftButton");
AssertCompositeBinding(asset, "QueueOneTouchPass", "OneModifier", "<Keyboard>/rightAlt", "<Mouse>/leftButton");
AssertCompositeBinding(asset, "QueueOneTouchShot", "OneModifier", "<Keyboard>/leftAlt", "<Mouse>/rightButton");
AssertCompositeBinding(asset, "QueueOneTouchShot", "OneModifier", "<Keyboard>/rightAlt", "<Mouse>/rightButton");
AssertActionBindings(asset, "Dodge", "<Keyboard>/space");
Assert.That(AllKeyboardMouseBindings(asset), Does.Not.Contain("<Keyboard>/k"));
Assert.That(AllKeyboardMouseBindings(asset), Does.Not.Contain("<Keyboard>/l"));
```

- [ ] **Step 2: Run the focused contract test and verify RED**

Run Unity EditMode `GameplayInputReaderTests.PlayerMap_ContainsTheGameplayInputBindingContract`. Expected: the new actions and composites are absent.

- [ ] **Step 3: Update the reader and action asset**

Update enum and name map. Through Unity MCP, remove old active keyboard/mouse bindings for Attack, Pass, Shot, Interact, Jump, Punch, SlideTackle, CancelCharge, K, and L; add the declared actions and bindings. Preserve no stale LMB/RMB, E, or Space binding read by gameplay.

- [ ] **Step 4: Add a device-level modifier-precedence test and verify GREEN**

```csharp
[Test]
public void AltLeftClick_TriggersOnlyTheOneTouchPassAction()
{
    Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
    Mouse mouse = InputSystem.AddDevice<Mouse>();
    GameplayInputReader reader = CreateReader(out InputActionAsset asset);

    Press(keyboard.leftAltKey);
    Press(mouse.leftButton);

    Assert.That(reader.ReadButton(GameplayInputAction.QueueOneTouchPass).WasPressed, Is.True);
    Assert.That(reader.ReadButton(GameplayInputAction.PrimaryAction).WasPressed, Is.False);
}
```

Run Unity EditMode `GameplayInputReaderTests` and `GameplayInputReaderDeviceTests`. Expected: binding and modifier-precedence tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Assets/_Game/Settings/InputSystem_Actions.inputactions Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
git commit -m "feat: define contextual player input actions"
```

### Task 3: Execute and retain one-touch attempts safely

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Ball/OneTouchActionExecutor.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/OneTouchIntentBufferTests.cs`

**Interfaces:** `OneTouchActionExecutor.TryAttempt(intent, handler, direction)` returns true only for a with-ball execution. `TryExecuteQueued(buffer, handler, direction)` consumes only after a true result. `PlayerBallHandler.TryPerformOneTouch(intent, direction)` invokes current immediate pass/shot paths with ball; `PlayOneTouchWhiff()` invokes only the existing shoot animation trigger without ball physics, audio, VFX, or camera shake.

- [ ] **Step 1: Write failing executor tests**

```csharp
[Test]
public void TryExecuteQueued_LeavesIntentWhenHandlerDoesNotOwnBall()
{
    var buffer = new OneTouchIntentBuffer();
    buffer.Queue(OneTouchIntent.Pass);
    var executor = new OneTouchActionExecutor();

    Assert.That(executor.TryExecuteQueued(buffer, handlerWithoutBall, Vector3.forward), Is.False);
    Assert.That(buffer.Intent, Is.EqualTo(OneTouchIntent.Pass));
}

[Test]
public void TryExecuteQueued_ConsumesIntentAfterSuccessfulWithBallAttempt()
{
    var buffer = new OneTouchIntentBuffer();
    buffer.Queue(OneTouchIntent.Shot);
    var executor = new OneTouchActionExecutor();

    Assert.That(executor.TryExecuteQueued(buffer, handlerWithBall, Vector3.forward), Is.True);
    Assert.That(buffer.Intent, Is.EqualTo(OneTouchIntent.None));
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run Unity EditMode `OneTouchIntentBufferTests`. Expected: compile failure because executor and handler API are absent.

- [ ] **Step 3: Implement immediate attempt and whiff behavior**

No-ball `TryAttempt` calls `PlayOneTouchWhiff` and returns false. Queued execution does nothing without possession and consumes only after a successful pass/shot. With ball, Pass maps to the existing immediate pass path and Shot maps to the existing immediate Shoot path.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run Unity EditMode `OneTouchIntentBufferTests`. Expected: no-ball attempts retain intent and with-ball attempts consume it.

- [ ] **Step 5: Commit**

```powershell
git add Assets/_Game/Scripts/Runtime/Ball/OneTouchActionExecutor.cs Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs Assets/_Game/Scripts/Tests/EditMode/OneTouchIntentBufferTests.cs
git commit -m "feat: execute persistent one touch intents"
```

### Task 4: Route contextual actions from the player bridge

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Input/ContextualPlayerActionRouter.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/ContextualPlayerActionRouterTests.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs`

**Interfaces:** `ContextualPlayerActionRouter.Process(reader, actionDirection)` is called once per active gameplay frame. It receives CharacterLocomotion, CombatController, and PlayerBallHandler at construction, owns one buffer and executor, exposes `OneTouchIntent` for UI/test observation, and provides `QueueOneTouch(OneTouchIntent, Vector3)` for the input bridge. It reads deferred actions without calling a nonexistent gameplay API.

- [ ] **Step 1: Write failing router tests**

```csharp
[Test]
public void PrimaryAction_WithBall_StartsPassCharge_WithoutBall_Punches()
{
    RouterFixture withBall = RouterFixture.Create(hasBall: true, GameplayInputAction.PrimaryAction);
    RouterFixture withoutBall = RouterFixture.Create(hasBall: false, GameplayInputAction.PrimaryAction);

    withBall.Router.Process(withBall.Reader, Vector3.forward);
    withoutBall.Router.Process(withoutBall.Reader, Vector3.forward);

    Assert.That(withBall.Ball.IsCharging, Is.True);
    Assert.That(withoutBall.Combat.LastPunchRejectedTime, Is.Not.EqualTo(-999f));
}

[Test]
public void QueueOneTouchPass_PreventsPrimaryActionInTheSameFrame()
{
    RouterFixture fixture = RouterFixture.Create(
        hasBall: false,
        GameplayInputAction.QueueOneTouchPass,
        GameplayInputAction.PrimaryAction);

    fixture.Router.Process(fixture.Reader, Vector3.forward);

    Assert.That(fixture.Router.OneTouchIntent, Is.EqualTo(OneTouchIntent.Pass));
    Assert.That(fixture.Ball.IsCharging, Is.False);
    Assert.That(fixture.Combat.LastPunchRejectedTime, Is.EqualTo(-999f));
}

[Test]
public void CancelAction_ClearsChargeAndQueuedIntent()
{
    RouterFixture fixture = RouterFixture.Create(hasBall: true, GameplayInputAction.CancelAction);
    fixture.Ball.StartCharge(BallChargeAction.Pass);
    fixture.Router.QueueOneTouch(OneTouchIntent.Pass, Vector3.forward);

    fixture.Router.Process(fixture.Reader, Vector3.forward);

    Assert.That(fixture.Ball.IsCharging, Is.False);
    Assert.That(fixture.Router.OneTouchIntent, Is.EqualTo(OneTouchIntent.None));
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run Unity EditMode `ContextualPlayerActionRouterTests`. Expected: compilation failure because the router does not exist.

- [ ] **Step 3: Implement the minimal precedence**

```csharp
if (reader.ReadButton(GameplayInputAction.CancelAction).WasPressed)
{
    ball?.CancelCharge();
    oneTouchBuffer.Clear();
    return;
}

if (TryQueueOneTouch(reader, actionDirection))
    return;

executor.TryExecuteQueued(oneTouchBuffer, ball, actionDirection);

if (ball != null && ball.IsCharging)
    HandleChargeRelease(reader, actionDirection);
else if (reader.ReadButton(GameplayInputAction.PrimaryAction).WasPressed)
    HandlePrimaryAction(actionDirection);

if (reader.ReadButton(GameplayInputAction.Dodge).WasPressed)
    locomotion.TryDodge(actionDirection);
```

Primary starts a pass charge with ball and uses existing `CombatController.Punch` without ball. Secondary starts a shot charge only with ball. Deferred secondary-without-ball, Q, Grab, and F branches have no effect yet.

- [ ] **Step 4: Run focused and regression tests and verify GREEN**

Run Unity EditMode `ContextualPlayerActionRouterTests`, `PlayerActionInputReaderTests`, `BallInteractionControllerTests`, `CombatBallConfigTests`, and `CharacterMovementResponsibilityTests`.

- [ ] **Step 5: Commit**

```powershell
git add Assets/_Game/Scripts/Runtime/Input/ContextualPlayerActionRouter.cs Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs Assets/_Game/Scripts/Tests/EditMode/ContextualPlayerActionRouterTests.cs Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs
git commit -m "feat: route contextual player actions"
```

### Task 5: Verify the scene boundary and document it

**Files:**
- Modify only if required through Unity MCP: `Assets/_Game/Scenes/SampleScene.unity`
- Modify: `IMPLEMENTATION_STATUS.md`
- Modify: `PROJECT_STRUCTURE.md`

**Interfaces:** The existing scene `GameplayInputReader` remains the only player-map owner. No scene consumer refers to removed action-reader types.

- [ ] **Step 1: Inspect scene reader and consumers**

Use Unity MCP to confirm SampleScene has exactly one GameplayInputReader and that PlayerInput, GameManager, CameraViewSwitcher, and ViewHintUI reference it.

- [ ] **Step 2: Make only needed Unity-safe assignments**

Assign a new serialized dependency only if the final router requires one; otherwise leave scene serialization untouched.

- [ ] **Step 3: Compile and inspect console**

Refresh Unity, wait for compilation/domain reload, then inspect errors.

- [ ] **Step 4: Run complete EditMode suite**

Run Unity EditMode assembly `FutsalGame.EditModeTests`. Expected: all discovered tests pass with zero failures.

- [ ] **Step 5: Review, document, and commit**

Document contextual actions, persistent one-touch behavior, no-ball whiff safety, and manual Play Mode checks. Exclude `ProjectSettings/ProjectSettings.asset` and `.superpowers/sdd/`.

```powershell
git add Assets/_Game/Settings/InputSystem_Actions.inputactions Assets/_Game/Scripts/Runtime/Input Assets/_Game/Scripts/Runtime/Ball Assets/_Game/Scripts/Tests/EditMode IMPLEMENTATION_STATUS.md PROJECT_STRUCTURE.md
git commit -m "docs: record contextual input controls"
```
