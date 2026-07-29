# Contextual Action Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add no-ball RMB Cross Punch with independent tuning/cooldown and a safe contextual-action boundary.

**Architecture:** Input resolves an immutable action request from semantic button state and possession facts. Combat owns action definitions, cooldowns and hits; Ball remains authoritative. Characters receives trigger/speed presentation only.

**Tech Stack:** Unity 6000.5.3f1, Input System, NUnit, Animator Controller, ScriptableObject, Unity MCP.

## Global Constraints

- Use `D:\Unity Projects\FutsalGame-worktrees\develop_merge_test` on `develop_merge_test`.
- Preserve unrelated dirty files; use Unity Editor/MCP for serialized assets.
- Input never mutates Ball ownership/physics; confirmed combat hits request release only via `PlayerBallHandler.ForceRelease`.
- Keep with-ball pass/shot, one-touch, cancel, dodge, and possession protection behavior unchanged.

### Task 1: Pure action selection

**Files:** Create `Runtime/Input/Actions/GameplayActionId.cs`, `GameplayActionRequest.cs`, `GameplayActionContext.cs`, `GameplayActionResolver.cs`; create `Tests/EditMode/GameplayActionResolverTests.cs`.

- [ ] Write resolver tests: no-ball Primary -> `BasicPunch`, no-ball Secondary -> `CrossPunch`, possession Primary -> `PassCharge`, possession Secondary -> `ShotCharge`, and a blocked context -> `None` without changing the context.
- [ ] Run `GameplayActionResolverTests`; expect missing-type compile failure.
- [ ] Add immutable context/request structs plus `Resolve(GameplayActionSlot, GameplayActionContext)` with no Ball or component dependency.
- [ ] Re-run `GameplayActionResolverTests`; expect all cases pass.
- [ ] Commit `feat: add contextual action resolver`.

### Task 2: Catalog-driven combat

**Files:** Create `Runtime/Combat/CombatActionId.cs`, `CombatActionCooldownTracker.cs`; modify `CombatConfig.cs`, `CombatController.cs`, `DefaultCombatConfig.asset` through Unity MCP, and `Tests/EditMode/CombatBallConfigTests.cs`.

- [ ] Write tests proving Basic and Cross Punch definitions have initially equal hit values, Cross speed is `2`, and their cooldown tracker accepts each action independently.
- [ ] Run `CombatBallConfigTests`; expect missing catalog types.
- [ ] Add `CombatActionDefinition` catalog entries to `CombatConfig`; each includes cooldown, range, radius, knockback, stun, ball-release settings, animation trigger, and speed.
- [ ] Replace the single punch timestamp with `CombatActionCooldownTracker`; add `TryExecute(CombatActionId, Vector3)` while preserving `Punch()` as a Basic Punch compatibility wrapper and retaining one shared hit path.
- [ ] Save the two initial catalog entries through Unity MCP: equal combat values, Basic speed `1`, Cross speed `2`.
- [ ] Re-run `CombatBallConfigTests`; expect all pass. Commit `feat: add catalog-driven combat actions`.

### Task 3: Route and present Cross Punch

**Files:** Modify `Runtime/Input/ContextualPlayerActionRouter.cs`, `Runtime/Characters/CharacterAnimator.cs`, `FutsalCharacter.controller` through Unity MCP, and contextual-input tests; use `Characters/Cross Punch.fbx`.

- [ ] Write failing tests that no-ball RMB starts Cross cooldown without consuming Basic cooldown and possession RMB still selects ShotCharge.
- [ ] Run contextual-input tests; expect Cross behavior absent.
- [ ] Delegate direct primary/secondary selection to `GameplayActionResolver`; dispatch combat requests to `TryExecute` and preserve Ball charge requests.
- [ ] Add generic `CharacterAnimator.PlayAction(trigger, speed)`; configure CrossPunch trigger/state with `mixamo.com` clip and speed parameter `2` through Unity MCP. Keep original Punch motion at speed `1`.
- [ ] Run focused fixtures, full EditMode suite, Console check, and Animator state inspection. Commit `feat: dispatch contextual cross punch action`.

### Task 4: Record verified result

**Files:** Modify `IMPLEMENTATION_STATUS.md`.

- [ ] Record only verified Input/Combat/Ball/Characters ownership boundaries, EditMode results, and outstanding manual Play Mode checks.
- [ ] Commit `docs: record contextual action catalog`.

## Plan self-review

- Input selection is testable without Ball mutation; combat settings are collected in one config; cross presentation is isolated from hit logic.
- No direct serialized asset editing, raw key reads, or BallController authority changes are planned.
