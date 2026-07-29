# Basic Punch Balance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the no-ball LMB Basic Punch use its Action Catalog values, reduce its default knockback to 4, and keep the victim's ball possession.

**Architecture:** `CombatActionDefinition` is already the catalog data type for Basic Punch and Cross Punch. Route Basic Punch and its cooldown through that entry, then pass each action's ball-release policy into the shared hit path; Slide Tackle retains its existing release policy and force.

**Tech Stack:** Unity 6000.5.3f1, C#, Unity Test Framework EditMode, ScriptableObject configuration.

## Global Constraints

- Touch only Basic Punch tuning and its shared hit-policy connection; do not implement R enhancement, change input bindings, or alter BallController ownership authority.
- Preserve existing uncommitted Grab, Animator, and ProjectSettings changes.
- Update `DefaultCombatConfig.asset` only through Unity Editor/MCP; never edit Unity YAML directly.
- Verify named EditMode tests and report manual Play Mode feel checks separately.

---

### Task 1: Prove the Basic Punch catalog contract

**Files:**
- Modify: `Assets/_Game/Scripts/Tests/EditMode/CombatBallConfigTests.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Combat/CombatConfig.cs:95-143`
- Modify: `Assets/_Game/Scripts/Runtime/Combat/CombatController.cs:44-129`

**Interfaces:**
- Consumes: `CombatConfig.TryGetAction(CombatActionId, out CombatActionDefinition)`.
- Produces: `CombatController.PunchCooldown` and `CombatController.Punch(Vector3)` read `BasicPunch` catalog data.

- [x] **Step 1: Write failing catalog tests**

```csharp
[Test]
public void CombatConfig_DefaultBasicPunchUsesReducedKnockbackAndKeepsBall()
{
    CombatConfig config = ScriptableObject.CreateInstance<CombatConfig>();
    Assert.That(config.TryGetAction(CombatActionId.BasicPunch, out CombatActionDefinition basic), Is.True);
    Assert.That(basic.knockbackForce, Is.EqualTo(4f));
    Assert.That(basic.releaseBallOnHit, Is.False);
}
```

- [x] **Step 2: Run the named test and confirm RED**

Run in Unity Test Runner: `CombatBallConfigTests.CombatConfig_DefaultBasicPunchUsesReducedKnockbackAndKeepsBall`.

Expected: fail because the current defaults are knockback `8f` and `releaseBallOnHit == true`.

- [x] **Step 3: Implement the smallest catalog connection**

```csharp
public float PunchCooldown => Config.TryGetAction(CombatActionId.BasicPunch, out CombatActionDefinition basicPunch)
    ? basicPunch.cooldown
    : Config.Punch.cooldown;

if (!Config.TryGetAction(CombatActionId.BasicPunch, out CombatActionDefinition punch))
    return;
```

Set the default Basic Punch catalog definition to `knockbackForce: 4f` and `releaseBallOnHit: false`; retain Cross Punch values.

- [x] **Step 4: Run the named test and confirm GREEN**

Run the same named test in Unity Test Runner. Expected: pass.

### Task 2: Make shared hit resolution honor the action ball policy

**Files:**
- Modify: `Assets/_Game/Scripts/Tests/EditMode/CombatBallConfigTests.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Combat/CombatController.cs:328-361`

**Interfaces:**
- Consumes: `CombatActionDefinition.releaseBallOnHit` and `ballKnockbackForce`.
- Produces: Basic Punch does not release a ball-owning victim; Cross Punch and Slide Tackle retain release behavior.

- [x] **Step 1: Write a failing integration test**

Create attacker, victim, and `BallController` GameObjects; acquire the ball for the victim through `BallController.TryAcquire(owner)`, call `combat.Punch(Vector3.forward)`, and assert `ball.CurrentOwner` remains the victim handler.

- [x] **Step 2: Run the named test and confirm RED**

Run in Unity Test Runner: `CombatBallConfigTests.CombatController_BasicPunchKeepsVictimBallPossession`.

Expected: fail because `Hit()` currently unconditionally calls `victimBall.ForceRelease(...)`.

- [x] **Step 3: Implement the minimal hit-policy parameters**

```csharp
private bool Hit(CharacterState victim, float knockbackForce, float stunDuration,
    bool releaseBallOnHit, float ballKnockbackForce)
{
    // existing invulnerability/effects logic
    if (releaseBallOnHit && victimBall != null && victimBall.HasBall)
        victimBall.ForceRelease(dir * ballKnockbackForce + Vector3.up * (ballKnockbackForce * 0.3f));
}
```

Pass Basic/Cross Punch catalog values at their call sites. Pass `true` and `Config.Tackle.ballKnockForce` for Slide Tackle.

- [x] **Step 4: Run the named test and confirm GREEN**

Run the same named test. Expected: pass while the victim remains stunned/knocked back.

### Task 3: Apply the serialized balance and verify regression scope

**Files:**
- Modify through Unity Editor/MCP: `Assets/_Game/Settings/DefaultCombatConfig.asset`
- Modify if required by changed intent: `Assets/_Game/Scripts/Tests/EditMode/CombatBallConfigTests.cs`

**Interfaces:**
- Consumes: Basic Punch Action Catalog entry.
- Produces: Inspector-visible Basic Punch values of `4` knockback and disabled ball release.

- [x] **Step 1: Use Unity Editor/MCP to set Basic Punch**

Set only the Basic Punch Action Catalog entry: `knockbackForce = 4`, `releaseBallOnHit = false`, and `ballKnockbackForce = 0`.

- [x] **Step 2: Run focused and complete EditMode verification**

Run the two named Basic Punch tests, then the full EditMode suite. Inspect Unity Console errors after compilation.

- [x] **Step 3: Review the scoped diff and report manual checks**

Confirm the diff contains only combat/test/config/plan files for this task. Manual Play Mode checks: reduced LMB knockback, ball-owning victim retains possession, Cross Punch and Slide Tackle still release the ball.
