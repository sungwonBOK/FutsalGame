# Sprint Stamina Halving Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce normal and burst sprint stamina drain to half their current rates while preserving every other sprint behavior.

**Architecture:** Keep the movement rules in `CharacterLocomotion`. Extract the two drain rates into a small pure helper so the gameplay calculation can be verified without an Input System, Animator, or Rigidbody fixture. `Update()` continues to own applying the resolved drain over `Time.deltaTime`.

**Tech Stack:** Unity 6000.5.3f1, C#, NUnit EditMode tests.

## Global Constraints

- Work only in `D:\Unity Projects\FutsalGame-worktrees\develop_merge_test`.
- Normal sprint drain is `13` stamina per second from the existing base rate of `26`.
- Burst sprint drain is `23.4` stamina per second: `26 * 1.8 * 0.5`.
- Do not change sprint speed, double-tap timing, animation speed, ball touch, input bindings, scenes, prefabs, Animator Controller, ScriptableObject assets, combat, or existing Grab work.
- Preserve every pre-existing dirty file change outside the explicit file list below.
- Use Unity EditMode evidence; Play Mode gauge feel remains a manual check.

---

### Task 1: Halve sprint drain rates

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Characters/Movement/CharacterLocomotion.cs:170-189`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/CharacterMovementResponsibilityTests.cs`

**Interfaces:**
- Consumes: `CharacterMovementConfig.SprintDrainPerSecond`, `CharacterLocomotion.IsSprinting`, and `CharacterLocomotion.IsBurstSprinting`.
- Produces: `public static float CharacterLocomotion.ResolveSprintStaminaDrain(float baseDrainPerSecond, bool burstSprint)`.

- [ ] **Step 1: Write the failing test**

Add this test to `CharacterMovementResponsibilityTests`:

```csharp
[Test]
public void ResolveSprintStaminaDrain_HalvesNormalAndBurstRates()
{
    Assert.That(CharacterLocomotion.ResolveSprintStaminaDrain(26f, burstSprint: false), Is.EqualTo(13f));
    Assert.That(CharacterLocomotion.ResolveSprintStaminaDrain(26f, burstSprint: true), Is.EqualTo(23.4f));
}
```

- [ ] **Step 2: Run the focused test to verify RED**

Run Unity EditMode test `CharacterMovementResponsibilityTests.ResolveSprintStaminaDrain_HalvesNormalAndBurstRates`.

Expected: compilation failure because `CharacterLocomotion.ResolveSprintStaminaDrain` does not exist.

- [ ] **Step 3: Write the minimal implementation**

Add this method to `CharacterLocomotion`:

```csharp
public static float ResolveSprintStaminaDrain(float baseDrainPerSecond, bool burstSprint)
{
    float multiplier = burstSprint ? 1.8f : 1f;
    return Mathf.Max(0f, baseDrainPerSecond) * multiplier * 0.5f;
}
```

Replace the two `SpendStamina` rate expressions in `Update()` with:

```csharp
SpendStamina(ResolveSprintStaminaDrain(Config.SprintDrainPerSecond, burstSprint: true) * Time.deltaTime);
```

and:

```csharp
SpendStamina(ResolveSprintStaminaDrain(Config.SprintDrainPerSecond, burstSprint: false) * Time.deltaTime);
```

- [ ] **Step 4: Run focused test to verify GREEN**

Run Unity EditMode test `CharacterMovementResponsibilityTests.ResolveSprintStaminaDrain_HalvesNormalAndBurstRates`.

Expected: 1 passed, 0 failed.

- [ ] **Step 5: Run full EditMode regression**

Run all Unity EditMode tests.

Expected: 0 failed tests and no game-code compilation errors in the Unity Console.

- [ ] **Step 6: Review the scoped diff and commit**

Run:

```powershell
git -C 'D:\Unity Projects\FutsalGame-worktrees\develop_merge_test' diff --check -- Assets/_Game/Scripts/Runtime/Characters/Movement/CharacterLocomotion.cs Assets/_Game/Scripts/Tests/EditMode/CharacterMovementResponsibilityTests.cs
```

Then stage only the two changed files and commit:

```powershell
git -C 'D:\Unity Projects\FutsalGame-worktrees\develop_merge_test' add -- Assets/_Game/Scripts/Runtime/Characters/Movement/CharacterLocomotion.cs Assets/_Game/Scripts/Tests/EditMode/CharacterMovementResponsibilityTests.cs
git -C 'D:\Unity Projects\FutsalGame-worktrees\develop_merge_test' commit -m "feat: halve sprint stamina drain"
```
