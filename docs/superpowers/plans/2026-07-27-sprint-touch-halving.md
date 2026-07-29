# Sprint Touch Halving Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Halve current possession sprint and burst-sprint forward touch forces without changing their relative 1.4 burst multiplier.

**Architecture:** `BallConfig` supplies the default possession sprint-touch multiplier consumed by `BallInteractionController`. Reducing its default and compatibility fallback from `2f` to `1f` yields a normal `3.5` touch and a burst `4.9` touch from the existing `3.5` base force and unchanged `1.4` burst multiplier. Existing behavior tests observe the emitted release impulse.

**Tech Stack:** Unity C#, NUnit EditMode tests.

## Global Constraints

- Modify only `BallConfig.cs` and the focused `BallInteractionControllerTests.cs` behavior assertions.
- Do not alter pass/shot forces, movement, stamina, animation, serialized assets, scenes, prefabs, or Input Actions.
- Preserve all unrelated shared-worktree changes.

---

### Task 1: Halve default possession sprint-touch force

**Files:**
- Modify: `Assets/_Game/Scripts/Tests/EditMode/BallInteractionControllerTests.cs:47-75`
- Modify: `Assets/_Game/Scripts/Runtime/Ball/BallConfig.cs:49-51,131-132`

**Interfaces:**
- Consumes: `BallInteractionController.TryTick(float, bool, Vector3, out Vector3)` and `BallConfig.Dribble.sprintTouchForce`.
- Produces: normal sprint touch impulse `Vector3.forward * 3.5f`; burst sprint touch impulse `Vector3.forward * 4.9f`.

- [ ] **Step 1: Write the failing tests**

```csharp
Assert.That(impulse, Is.EqualTo(Vector3.forward * 3.5f));
Assert.That(impulse, Is.EqualTo(Vector3.forward * 4.9f));
```

- [ ] **Step 2: Run the focused test to verify it fails**

Run the two sprint-touch tests in `BallInteractionControllerTests` through Unity EditMode.

Expected: FAIL because the current defaults emit `7.0` and `9.8` impulses.

- [ ] **Step 3: Write the minimal implementation**

```csharp
possessionSprintTouchMultiplier = 1f;
public float PossessionSprintTouchMultiplier => Dribble.possessionSprintTouchMultiplier > 0f
    ? Dribble.possessionSprintTouchMultiplier
    : 1f;
```

- [ ] **Step 4: Run tests to verify the new forces pass**

Run the focused test, then the full EditMode suite. Confirm no game-code compilation errors in Unity Console.

- [ ] **Step 5: Review and commit scoped changes**

```powershell
git diff --check -- Assets/_Game/Scripts/Runtime/Ball/BallConfig.cs Assets/_Game/Scripts/Tests/EditMode/BallInteractionControllerTests.cs
git add -- Assets/_Game/Scripts/Runtime/Ball/BallConfig.cs Assets/_Game/Scripts/Tests/EditMode/BallInteractionControllerTests.cs
git commit -m "tune: halve sprint touch force"
```
