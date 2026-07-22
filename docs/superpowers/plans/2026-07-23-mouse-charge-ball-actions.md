# Mouse Charge Ball Actions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `F`/`Space` ball actions with configurable mouse charge-pass and charge-shot controls that use the camera direction at release time and support `C` cancellation.

**Architecture:** `PlayerActionBindings` stores editable mouse/key defaults. An input reader translates those bindings into button states. `PlayerInput` coordinates those states with `PlayerBallHandler`; `BallInteractionController` stores only action type and start time, then resolves force and the release-time planar camera direction.

**Tech Stack:** Unity 6000.5, Input System, C#, Unity Test Framework EditMode, Unity MCP.

## Global Constraints

- Keep `PlayerBallHandler` compatibility APIs for AI, combat, and match callers.
- Remove only the active `F`/`Space` player-input path.
- Do not modify Scene, Prefab, Input Actions, or ScriptableObject YAML directly.
- Keep pass and shot charges mutually exclusive, and make `C` cancel without any release impulse.

---

### Task 1: Add configurable action bindings

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs`
- Create: `Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs`
- Create: `Assets/_Game/Settings/DefaultPlayerActionBindings.asset` through Unity MCP

**Interfaces:**
- `PlayerMouseButton { None, Left, Right, Middle }`
- `PlayerActionBinding` has `MouseButton` and `KeyboardKey`.
- `PlayerActionBindings.Pass`, `.Shot`, `.Cancel` default to left/none, right/none, none/C.
- `PlayerActionInputReader.Read(...)` returns `ActionButtonState { WasPressed, IsPressed, WasReleased }`.

- [ ] **Step 1: Write failing default-binding tests**

```csharp
[Test]
public void DefaultBindings_UseMouseForBallActionsAndCForCancel()
{
    PlayerActionBindings bindings = ScriptableObject.CreateInstance<PlayerActionBindings>();
    Assert.That(bindings.Pass.MouseButton, Is.EqualTo(PlayerMouseButton.Left));
    Assert.That(bindings.Pass.KeyboardKey, Is.EqualTo(Key.None));
    Assert.That(bindings.Shot.MouseButton, Is.EqualTo(PlayerMouseButton.Right));
    Assert.That(bindings.Cancel.KeyboardKey, Is.EqualTo(Key.C));
}
```

- [ ] **Step 2: Run the named EditMode test and verify it fails**

Run `PlayerActionInputReaderTests.DefaultBindings_UseMouseForBallActionsAndCForCancel` through Unity MCP. Expected: compile error because the binding type does not exist.

- [ ] **Step 3: Implement the data asset and reader**

Create the types above. The reader must merge optional mouse and keyboard alternatives so one alternative releasing cannot report `WasReleased` while the other remains held. It reads no gameplay state and invokes no ball code.

- [ ] **Step 4: Run focused tests and create the asset safely**

Run `PlayerActionInputReaderTests` through Unity MCP; verify no console errors. Create and set the default asset using Unity SerializedObject tooling only.

- [ ] **Step 5: Commit the isolated binding layer**

```powershell
git add -- Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs Assets/_Game/Settings/DefaultPlayerActionBindings.asset
git commit -m "feat: add configurable player action bindings"
```

### Task 2: Add typed pass/shot charge and cancellation

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Ball/BallConfig.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Ball/BallInteractionController.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/BallInteractionControllerTests.cs`
- Modify: `Assets/_Game/Settings/DefaultBallConfig.asset` through Unity MCP

**Interfaces:**
- `BallChargeAction { None, Pass, Shot }`
- `TryStartCharge(float now, BallChargeAction action)`
- `TryReleaseCharge(float now, BallChargeAction action, Vector3 releaseDirection, Vector3 fallbackForward, out Vector3 impulse)`
- `CancelCharge()` clears the active action.

- [ ] **Step 1: Write failing interaction tests**

```csharp
[Test]
public void ReleaseCharge_UsesLatestDirectionAndPassForceRange()
{
    possession.AcquireInitial(true);
    config.Pass.minChargeForce = 3.5f;
    config.Pass.maxChargeForce = 7f;
    interaction.TryStartCharge(10f, BallChargeAction.Pass);

    Assert.That(interaction.TryReleaseCharge(11f, BallChargeAction.Pass, Vector3.right, Vector3.forward, out Vector3 impulse), Is.True);
    Assert.That(impulse, Is.EqualTo(Vector3.right * 7f));
}

[Test]
public void CancelCharge_PreventsLaterMatchingRelease()
{
    possession.AcquireInitial(true);
    interaction.TryStartCharge(10f, BallChargeAction.Shot);
    interaction.CancelCharge();

    Assert.That(interaction.TryReleaseCharge(11f, BallChargeAction.Shot, Vector3.right, Vector3.forward, out _), Is.False);
    Assert.That(ball.CurrentOwner, Is.Not.Null);
}
```

- [ ] **Step 2: Run named tests and verify they fail**

Run both new tests through Unity MCP. Expected: compile error because `BallChargeAction` and new charge methods do not exist.

- [ ] **Step 3: Implement the minimal state transition**

Store only active action and start time. Require a matching action on release; calculate the existing `Shot.maxChargeTime` ratio, resolve the supplied release direction, then interpolate Pass `minChargeForce` to `maxChargeForce` or Shot `minChargeForce` to `maxShootForce`. Clear the active action before releasing. Mismatched/cancelled releases return false without changing ownership.

Rename `Pass.force` to `[FormerlySerializedAs("force")] minChargeForce`, add `maxChargeForce`, and keep direct compatibility pass calls at the minimum force.

- [ ] **Step 4: Verify and safely patch defaults**

Run `BallInteractionControllerTests` through Unity MCP and check the console. Use SerializedObject patches for `Pass.minChargeForce=3.5`, `Pass.maxChargeForce=7`, existing Shot `3.5..13`, and `maxChargeTime=1`.

- [ ] **Step 5: Commit interaction behavior**

```powershell
git add -- Assets/_Game/Scripts/Runtime/Ball/BallConfig.cs Assets/_Game/Scripts/Runtime/Ball/BallInteractionController.cs Assets/_Game/Scripts/Tests/EditMode/BallInteractionControllerTests.cs Assets/_Game/Settings/DefaultBallConfig.asset
git commit -m "feat: support chargeable passes and cancel"
```

### Task 3: Wire configurable controls to the facade

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/CameraInputDirectionTests.cs`
- Modify: active `PlayerInput` binding reference only through Unity MCP if required
- Modify: `IMPLEMENTATION_STATUS.md`
- Modify: `PROJECT_STRUCTURE.md`

**Interfaces:**
- `PlayerInput.BuildPlanarCameraForward(Transform reference, Vector3 fallbackForward)` returns a normalized XZ direction.
- `PlayerBallHandler` exposes typed start/release calls while retaining `Shoot`, `Pass`, `StartCharge`, and `ReleaseCharge` compatibility methods.

- [ ] **Step 1: Write the failing planar-camera-direction test**

```csharp
[Test]
public void BuildPlanarCameraForward_UsesCameraHeadingWithoutVerticalPitch()
{
    GameObject reference = new GameObject("Camera Reference");
    reference.transform.rotation = Quaternion.Euler(45f, 90f, 0f);
    Vector3 direction = PlayerInput.BuildPlanarCameraForward(reference.transform, Vector3.forward);

    Assert.That(direction.y, Is.EqualTo(0f).Within(0.001f));
    Assert.That(Vector3.Dot(direction, Vector3.right), Is.EqualTo(1f).Within(0.001f));
    Object.DestroyImmediate(reference);
}
```

- [ ] **Step 2: Run the named test and verify it fails**

Run `CameraInputDirectionTests.BuildPlanarCameraForward_UsesCameraHeadingWithoutVerticalPitch` through Unity MCP. Expected: compile error because the method does not exist.

- [ ] **Step 3: Replace active F/Space input**

Read configured Pass, Shot, and Cancel states every active frame. Process Cancel before release. Start only when no charge is active; calculate planar camera direction only at matching action release. Remove the `kb.fKey` and `kb.spaceKey` branches. Play shoot-only presentation only for Shot release; a pass releases through the same possession physics without shoot presentation.

- [ ] **Step 4: Assign and verify safely**

Use Unity MCP to set `PlayerInput.actionBindings` to the default asset on active player components. Run `CameraInputDirectionTests`, `BallInteractionControllerTests`, then the full EditMode suite. Check for zero console errors and use a brief Play Mode smoke test for active camera and missing-reference warnings.

- [ ] **Step 5: Document, comment, and commit reviewed files**

Update the two project documents with actual boundaries. Add issue #3 completion details and remaining manual checks: both short releases, full charges, camera turn while charging, C cancellation, and remapped-key behavior. Run `git diff --check`, review only task files, and commit without staging unrelated dirty camera work.

## Plan Self-Review

- Covers configurable mouse/key bindings, chargeable pass/shot force ranges, release-time camera direction, cancellation, mutual exclusion, compatibility APIs, and focused/full Unity verification.
- No placeholders or type-name mismatches remain between producer and consumer tasks.
