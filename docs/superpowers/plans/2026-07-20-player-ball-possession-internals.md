# Player Ball Possession Internals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract player-specific possession rules from `PlayerBallHandler` without changing its public caller-facing API or Unity asset wiring.

**Architecture:** `BallController` remains the single owner of authoritative ball ownership and Rigidbody/collider state. A private, non-MonoBehaviour `BallPossessionController` is composed by `PlayerBallHandler` and owns initial acquisition, delayed reacquisition, release bookkeeping, and cleanup. Charge, shoot, dribble placement, and presentation remain in the facade for this increment.

**Tech Stack:** Unity 6000.5.3f1, C#, NUnit EditMode tests, Unity MCP.

## Global Constraints

- Preserve `PlayerBallHandler.CurrentOwner`, `HasBall`, `Shoot`, `ForceRelease`, `ClearPossession`, `IsCharging`, and `ChargeAmount01`.
- Do not edit `.unity`, `.prefab`, or `.asset` YAML; this increment requires no asset wiring.
- Do not modify `BallController` ownership/physics behavior or `CombatController`.
- Preserve unrelated dirty worktree changes.

---

### Task 1: Possession helper and facade delegation

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Ball/BallPossessionController.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/PlayerBallPossessionTests.cs`

**Interfaces:**
- Consumes: `BallController.TryAcquire(PlayerBallHandler)`, `HasOwner(PlayerBallHandler)`, `Release(PlayerBallHandler, Vector3)`, and `ClearOwner()`.
- Produces: `BallPossessionController.HasBall`, `AcquireInitial()`, `TryAcquire(float, bool)`, `Release(Vector3)`, and `ClearIfOwner()`.

- [ ] **Step 1: Write failing ownership-delegation tests.**

```csharp
[Test]
public void Release_RecordsReleaseTimeAndPreventsImmediateReacquire()
{
    possession.Release(Vector3.forward);
    Assert.That(possession.TryAcquire(0.5f, true), Is.False);
}

[Test]
public void ClearIfOwner_ReleasesOnlyItsOwnBallPossession()
{
    possession.ClearIfOwner();
    Assert.That(ball.CurrentOwner, Is.Null);
}
```

- [ ] **Step 2: Run the focused EditMode test to verify it fails.**

Run: Unity MCP EditMode test filter `PlayerBallPossessionTests`.

Expected: compilation failure because `BallPossessionController` does not yet exist.

- [ ] **Step 3: Add the minimal helper implementation.**

```csharp
public bool TryAcquire(float now, bool canAcquire)
{
    return canAcquire && !HasBall && ball.CurrentOwner == null &&
        now - lastReleaseTime >= config.Possession.reacquireDelay &&
        IsWithinAcquireRange() && ball.TryAcquire(owner);
}

public bool Release(Vector3 impulse)
{
    if (!ball.Release(owner, impulse)) return false;
    lastReleaseTime = Time.time;
    return true;
}
```

- [ ] **Step 4: Delegate `PlayerBallHandler` possession behavior.**

```csharp
public bool HasBall => possession != null && possession.HasBall;

private void Start() => possession?.AcquireInitial(startWithBall);

private void OnDisable()
{
    CancelCharge();
    possession?.ClearIfOwner();
}
```

Keep `Update`, `LateUpdate`, charge state, direction capture, effects, and all public methods in the facade. Replace only possession checks, acquire attempts, and impulse release with helper calls.

- [ ] **Step 5: Run focused and full EditMode tests.**

Run: Unity MCP EditMode filter `PlayerBallPossessionTests`, then the complete EditMode suite.

Expected: all tests pass with no changed behavior for existing facade callers.

### Task 2: Runtime verification and status record

**Files:**
- Modify: `IMPLEMENTATION_STATUS.md`

- [ ] **Step 1: Check Unity compilation and Console.**

Run: Unity MCP editor-state query until `is_compiling` is false, then Console query for errors and warnings.

Expected: no compilation errors or warnings.

- [ ] **Step 2: Inspect the focused diff.**

Run: `git diff --check` and inspect only the helper, facade, test, and status-document changes.

Expected: no whitespace errors and no scene/prefab/asset diff introduced by this task.

- [ ] **Step 3: Record verified scope and remaining risk.**

Add one concise status entry: possession internals are split behind the stable facade; Play Mode possession/charge/shoot feel remains a manual check.
