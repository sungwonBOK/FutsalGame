# Possession Input Context Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stabilize contextual ball/combat input across short possession changes without changing actual ball ownership.

**Architecture:** A pure `PossessionInputContext` evaluates actual ownership, sprint grace, and whether ownership was gained inside the most recent combat-transition window. The router owns latches and delegates only existing ball/combat operations; `PlayerBallHandler` exposes the already-owned acquire-range test.

**Tech Stack:** Unity 6000.5.3f1, C#, Unity Test Framework.

## Global Constraints

- Work only in `develop_merge_test`; preserve every pre-existing dirty file change.
- No YAML asset edits, no new defense/guard action, no camera or combat-controller modification.
- Use 0.65 seconds for sprint grace and 0.40 seconds for combat protection after each no-ball combat input.
- Keep no-ball secondary and possession F as no-ops.

---

### Task 1: Define pure effective-possession state

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Input/PossessionInputContext.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/PossessionInputContextTests.cs`

**Interfaces:** `Update(now, actuallyHasBall, opponentHasBall, withinAcquireRange, sprintHeld)` updates `HasPossessionContext`; `BeginCombatProtection(now)`, `ShouldSuppressHeldInput`, and `ReleaseInput` govern post-combat ownership changes.

- [ ] Write tests for sprint grace, opponent/range cancellation, and protected held input.
- [ ] Run the focused test and observe the expected missing-type failure.
- [ ] Implement the minimal timer and button suppression state.
- [ ] Re-run the focused test and confirm it passes.

### Task 2: Expose the existing acquisition distance through the ball facade

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Ball/BallPossessionController.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs`

**Interfaces:** `BallPossessionController.IsWithinAcquireRange` is a read-only public property; `PlayerBallHandler.IsWithinAcquireRange` delegates to it. Neither changes ownership or physics.

- [ ] Add the narrow public read APIs.
- [ ] Confirm existing possession tests still compile.

### Task 3: Latch and route contextual actions

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Input/ContextualPlayerActionRouter.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs` only if a concise router test can use the existing fixture.

**Interfaces:** Router updates `PossessionInputContext` once per frame, decides action only on press, releases matching ball charge action on release, and clears suppression only after matching release. It preserves the current direction split.

- [ ] Add or reuse a concise regression test; if fixture setup would expand substantially, leave test creation to the user as authorized.
- [ ] Implement latching, grace, protection, and explicit no-op F possession behavior.
- [ ] Run the focused regression tests and check compilation.

### Task 4: Verify and record the boundary

**Files:**
- Modify: `IMPLEMENTATION_STATUS.md`

- [ ] Run Unity compilation and inspect Console diagnostics.
- [ ] Run all relevant EditMode tests available in the current editor.
- [ ] Review the scoped diff, excluding unrelated dirty changes.
- [ ] Record automated evidence and remaining manual Play Mode checks.
