# Ball Dribble, Pass, and Shoot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add configurable sprint-touch dribbling and fixed-force F passing while preserving the current possession and charged-shot behavior.

**Architecture:** Keep `BallController` as the ball's physics and owner authority and keep `BallPossessionController` as the release/reacquisition authority. Add a small plain `BallInteractionController` that owns only the sprint-touch timer, pass release, charge state, and cancellation; `PlayerBallHandler` remains the compatibility facade and Unity presentation boundary.

**Tech Stack:** Unity 6, C#, New Input System keyboard polling, NUnit EditMode tests, Unity ScriptableObject configuration.

## Global Constraints

- Do not edit `.unity`, `.prefab`, `.asset`, or `.inputactions` YAML directly.
- Use Unity Editor/MCP to serialize new `DefaultBallConfig` fields while preserving its asset GUID.
- Do not modify the active camera files or alter movement profiles in `CharacterMovementConfig`.
- Do not add teammate selection, aim assist, lob passes, HUD, new animations, or a generic action state machine.
- Limit new automated coverage to the two rule-level tests below; validate feel manually in Play Mode.
- Preserve `PlayerBallHandler` APIs used by AI, combat, UI, and match reset.

---

## File Structure

- Create: `Assets/_Game/Scripts/Runtime/Ball/BallInteractionController.cs` — pure interaction timing, impulse calculation, charge state, and cancellation.
- Create: `Assets/_Game/Scripts/Tests/EditMode/BallInteractionControllerTests.cs` — concise sprint-touch and pass release rules.
- Modify: `Assets/_Game/Scripts/Runtime/Ball/BallConfig.cs` — add sprint-touch and pass tuning data; separate charged-shot minimum force from pass force.
- Modify: `Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs` — construct and delegate to the interaction controller while retaining facade APIs and charged-shot presentation.
- Modify: `Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs` — send Shift/action direction to ball interaction and call F-pass.
- Modify through Unity: `Assets/_Game/Settings/DefaultBallConfig.asset` — set `sprintTouchInterval = 0.5`, `sprintTouchForce = 3.5`, and `Pass.force = 3.5`.
- Update after verification only: `PROJECT_STRUCTURE.md`, `IMPLEMENTATION_STATUS.md` — append ball-boundary evidence without overwriting the unrelated camera edits already in the worktree.

## Interface Contract

```csharp
public sealed class BallInteractionController
{
    public bool IsCharging { get; }
    public float ChargeAmount01(float now);
    public void SetSprintInput(bool held, Vector3 actionDirection);
    public bool TryTick(float now, bool canInteract, Vector3 fallbackForward, out Vector3 sprintTouchImpulse);
    public bool TryPass(float now, Vector3 actionDirection, Vector3 fallbackForward, out Vector3 impulse);
    public bool StartCharge(float now, Vector3 actionDirection, Vector3 fallbackForward);
    public bool TryReleaseCharge(float now, Vector3 fallbackForward, out Vector3 impulse);
    public void CancelAll();
    public static Vector3 CaptureDirection(Vector3 actionDirection, Vector3 fallbackForward);
}
```

`TryTick`, `TryPass`, and `TryReleaseCharge` return `true` only after releasing through `BallPossessionController`. `PlayerBallHandler` applies existing shoot animation/audio/VFX/camera-shake only for the charged-shot result; pass and sprint touches change ball physics without new presentation work.

### Task 1: Add Rule-Level Interaction Tests

**Files:**
- Create: `Assets/_Game/Scripts/Tests/EditMode/BallInteractionControllerTests.cs`

**Consumes:** `BallConfig`, `BallController`, `BallPossessionController`, and `PlayerBallHandler`.

**Produces:** Failing executable specifications for the timer and F-pass contract.

- [ ] **Step 1: Write the two failing EditMode tests**

```csharp
[Test]
public void SprintTouch_ReleasesOnlyAfterConfiguredInterval()
{
    possession.AcquireInitial(true);
    interaction.SetSprintInput(true, Vector3.forward);

    Assert.That(interaction.TryTick(10f, true, Vector3.forward, out _), Is.False);
    Assert.That(interaction.TryTick(10.49f, true, Vector3.forward, out _), Is.False);
    Assert.That(interaction.TryTick(10.5f, true, Vector3.forward, out Vector3 impulse), Is.True);
    Assert.That(impulse, Is.EqualTo(Vector3.forward * config.Dribble.sprintTouchForce));
    Assert.That(ball.CurrentOwner, Is.Null);
}

[Test]
public void Pass_ReleasesWithTheSuppliedActionDirection()
{
    possession.AcquireInitial(true);

    Assert.That(interaction.TryPass(10f, Vector3.right, Vector3.forward, out Vector3 impulse), Is.True);
    Assert.That(impulse, Is.EqualTo(Vector3.right * config.Pass.force));
    Assert.That(ball.CurrentOwner, Is.Null);
}
```

The shared setup must create the ball Rigidbody, SphereCollider, `BallController`, owner `PlayerBallHandler`, `BallConfig`, `BallPossessionController`, and `BallInteractionController`. Set `config.Possession.reacquireDelay = 1f`, `config.Dribble.sprintTouchInterval = 0.5f`, `config.Dribble.sprintTouchForce = 3.5f`, and `config.Pass.force = 3.5f`.

- [ ] **Step 2: Run the focused test class before implementation**

Run in Unity EditMode Test Runner: `BallInteractionControllerTests`.

Expected: compilation failure because `BallInteractionController` and the new `BallConfig` members do not yet exist.

### Task 2: Add Configurable Interaction Logic

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Ball/BallInteractionController.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Ball/BallConfig.cs`

**Consumes:** Task 1's expected test interface and `BallPossessionController.Release(float, Vector3)`.

**Produces:** a tested, Unity-independent interaction component.

- [ ] **Step 1: Add the configuration groups**

```csharp
[Serializable]
public struct DribbleSettings
{
    public Vector3 offset;
    [Min(0f)] public float followSharpness;
    [Min(0f)] public float detachImpulse;
    [Min(0.01f)] public float sprintTouchInterval;
    [Min(0f)] public float sprintTouchForce;
}

[Serializable]
public struct PassSettings
{
    [Min(0f)] public float force;
    public PassSettings(float force) { this.force = force; }
}

public PassSettings Pass = new PassSettings(3.5f);
```

Rename `ShotSettings.passForce` to `minChargeForce` and annotate the new field with `[FormerlySerializedAs("passForce")]`. Update charged-shot interpolation to use `minChargeForce`; this preserves the existing `3.5` serialized value while giving F-pass its independent force.

- [ ] **Step 2: Implement the controller with only required state**

```csharp
private bool sprintHeld;
private Vector3 sprintActionDirection = Vector3.forward;
private float sprintTouchStartedAt = -1f;
private bool isCharging;
private float chargeStartedAt;
private Vector3 chargeDirection = Vector3.forward;

public bool TryTick(float now, bool canInteract, Vector3 fallbackForward, out Vector3 sprintTouchImpulse)
{
    sprintTouchImpulse = Vector3.zero;
    if (!canInteract || !possession.HasBall || !sprintHeld)
    {
        sprintTouchStartedAt = -1f;
        if (!canInteract || !possession.HasBall)
            isCharging = false;
        return false;
    }

    if (sprintTouchStartedAt < 0f)
    {
        sprintTouchStartedAt = now;
        return false;
    }

    if (now - sprintTouchStartedAt < config.Dribble.sprintTouchInterval)
        return false;

    sprintTouchStartedAt = -1f;
    sprintTouchImpulse = CaptureDirection(sprintActionDirection, fallbackForward)
        * config.Dribble.sprintTouchForce;
    return possession.Release(now, sprintTouchImpulse);
}

public bool TryPass(float now, Vector3 actionDirection, Vector3 fallbackForward, out Vector3 impulse)
{
    CancelAll();
    impulse = CaptureDirection(actionDirection, fallbackForward) * config.Pass.force;
    return possession.Release(now, impulse);
}
```

`StartCharge` must cancel only the pending sprint touch, record `now` and a captured direction, and require possession. `TryReleaseCharge` must clear `isCharging`, calculate `Mathf.Lerp(config.Shot.minChargeForce, config.Shot.maxShootForce, ChargeAmount01(now))`, then release once through possession. `CancelAll` clears both the sprint and charge fields.

- [ ] **Step 3: Run the focused test class**

Run in Unity EditMode Test Runner: `BallInteractionControllerTests`.

Expected: `2 passed, 0 failed`.

### Task 3: Delegate from the Existing Ball Facade and Wire Input

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs`

**Consumes:** `BallInteractionController` from Task 2.

**Produces:** player-facing F pass, Space charge, and Shift sprint-touch behavior without changing callers in AI, combat, UI, or match reset.

- [ ] **Step 1: Construct and expose the interaction controller through `PlayerBallHandler`**

Create the controller in `Awake` immediately after `BallPossessionController`:

```csharp
possession = new BallPossessionController(this, ball, Config);
interaction = new BallInteractionController(possession, Config);
```

Replace the facade charge properties with:

```csharp
public bool IsCharging => interaction != null && interaction.IsCharging;
public float ChargeAmount01 => interaction != null ? interaction.ChargeAmount01(Time.time) : 0f;
```

Add `SetSprintDribbleInput(bool held, Vector3 actionDirection)`, which only forwards to `interaction.SetSprintInput`. In `Update`, first call `interaction.TryTick(Time.time, canInteract, transform.forward, out _)`; then keep the existing `possession.TryAcquire(Time.time, true)` call. This ordering starts a fresh 0.5-second wait on the frame after reacquisition, rather than immediately releasing the newly acquired ball.

- [ ] **Step 2: Preserve the existing facade APIs**

Implement `Pass(Vector3 actionDirection)` as an immediate `interaction.TryPass` call. Implement `StartCharge`, `ReleaseCharge`, and `CancelCharge` by delegating to the controller. After a successful charged release only, call the existing shoot presentation and release the calculated impulse. `ForceRelease`, `OnDisable`, stun, and inactive-match paths must call `interaction.CancelAll()` before their existing ownership cleanup or release behavior.

Keep `Shoot()`, `Shoot(Vector3)`, `CurrentOwner`, `HasBall`, `ForceRelease`, and `ClearPossession` callable with their current signatures. Make `PlayerBallHandler.CaptureShotDirection` delegate to `BallInteractionController.CaptureDirection` so existing tests and AI callers remain valid.

- [ ] **Step 3: Wire keyboard input without modifying the Input Action asset**

After `actionDirection` is resolved in `PlayerInput.Update`, add:

```csharp
ball.SetSprintDribbleInput(sprint, actionDirection);

if (kb.fKey.wasPressedThisFrame)
    ball.Pass(actionDirection);
if (kb.spaceKey.wasPressedThisFrame)
    ball.StartCharge(actionDirection);
if (kb.spaceKey.wasReleasedThisFrame)
    ball.ReleaseCharge();
```

In the inactive/stunned early-return branch, call `ball.SetSprintDribbleInput(false, Vector3.zero)` before returning when `ball` is non-null.

- [ ] **Step 4: Run focused and full EditMode checks**

Run `BallInteractionControllerTests`, then the complete `FutsalGame.EditModeTests` assembly in the Unity Test Runner.

Expected: the new class reports `2 passed, 0 failed`; the full suite reports no failures. A zero-test PlayMode job is not evidence for this behavior.

### Task 4: Serialize Defaults, Review, and Document Verification

**Files:**
- Modify through Unity Editor/MCP: `Assets/_Game/Settings/DefaultBallConfig.asset`
- Modify after review: `PROJECT_STRUCTURE.md`, `IMPLEMENTATION_STATUS.md`

**Consumes:** successful compilation from Task 3.

**Produces:** scene-referenced tuning values and evidence-based project documentation.

- [ ] **Step 1: Update `DefaultBallConfig` through Unity Editor/MCP**

Set these values on the existing asset without replacing it:

```text
Dribble.sprintTouchInterval = 0.5
Dribble.sprintTouchForce = 3.5
Pass.force = 3.5
Shot.minChargeForce = 3.5
```

- [ ] **Step 2: Inspect compilation and the active scene references**

Use Unity to refresh/import the changed scripts, read the Console, and verify that the existing Ball, Player, and Opponent components still reference `DefaultBallConfig`. Expected: no game-code compiler errors or warnings.

- [ ] **Step 3: Manually verify feel in Play Mode**

Confirm each item directly:

1. Normal possession holds the ball at the default dribble offset.
2. Holding Shift with possession waits 0.5 seconds, releases forward, and permits normal sprint chase.
3. Holding Shift through reacquisition begins a fresh wait rather than releasing every frame.
4. Releasing Shift before 0.5 seconds prevents the forward touch.
5. F releases a fixed-force pass in the action direction.
6. Space still locks direction at press and releases a charged shot at key-up.
7. Stun or combat force release cancels a charge and pending sprint touch.

- [ ] **Step 4: Update documentation without staging unrelated camera edits**

Add the verified `BallInteractionController` responsibility and the exact test/Play Mode evidence to the Ball sections of `PROJECT_STRUCTURE.md` and `IMPLEMENTATION_STATUS.md`. Review their existing dirty camera diff first; stage only the ball lines if those documents can be isolated safely. Otherwise leave the mixed documentation changes unstaged and report the conflict risk in issue #12.

- [ ] **Step 5: Review the diff and report issue completion**

Run `git diff --check` only on new/modified ball and input code, inspect the scoped diff, then post the actual files changed, automated result, manual checks actually performed, and remaining risk to GitHub issue #3. Update issue #12 only if the documentation lines were safely changed.

## Plan Self-Review

Coverage: Task 1 and Task 2 cover configurable 0.5-second sprint touches and F-pass direction/release. Task 3 preserves the existing charged shot, facade callers, possession-speed-before-release behavior, and keyboard bindings. Task 4 covers Unity-only asset serialization, compilation, Play Mode evidence, and documentation.

No placeholders: the plan specifies all required files, interfaces, values, test cases, commands, and manual checks. Type consistency: all later facade and test calls use the `BallInteractionController` contract defined above.
