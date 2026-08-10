# Power Gauge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a player power gauge that slowly fills during play and gains configurable value from successful non-grab combat and evasion.

**Architecture:** `PowerGauge` owns value, clamping, passive gain, and match-reset behavior. `PowerGaugeConfig` owns all starting values and source rules. Existing combat success paths notify the acting or evading character's gauge; `AbilityCooldownUI` presents the local target's gauge above stamina.

**Tech Stack:** Unity 6000, C#, Unity Test Framework EditMode, Input System, uGUI.

## Global Constraints

- Preserve unrelated dirty combat, ball, and networking work.
- Do not add the future R enhancement behavior; only remove the current Restart action and R binding.
- Do not edit Unity YAML directly; create/configure ScriptableObject assets through Unity Editor/MCP.
- Initial tuning: capacity 100, passive 1/sec, basic punch 10, cross punch 15, slide tackle 15, defense 10, evade 10.

---

### Task 1: Gauge domain and configuration

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/Characters/Power/PowerGauge.cs`
- Create: `Assets/_Game/Scripts/Runtime/Characters/Power/PowerGaugeConfig.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/PowerGaugeTests.cs`

- [ ] Write EditMode tests proving passive gain clamps at capacity, disabled source gives no value, enabled source gives its configured value, and reset clears value.
- [ ] Run the new tests and confirm they fail because the gauge types do not exist.
- [ ] Add minimal serializable config rules and a `PowerGauge` component exposing `Value01`, `Add`, `Tick`, and `ResetGauge`.
- [ ] Run the focused EditMode tests until they pass.

### Task 2: Runtime success and match integration

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Combat/CombatController.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Characters/CharacterState.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Match/GameManager.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/PowerGaugeIntegrationTests.cs`

- [ ] Write tests proving actual hit and actual evade rewards are routed once, while no successful hit gives no reward.
- [ ] Run and confirm the tests fail before integration.
- [ ] Notify the attacker's gauge only after a local combat hit resolves, notify the evading character at its existing actual-evade method, and reset all gauges only when a new match starts.
- [ ] Run the focused integration tests until they pass.

### Task 3: HUD and input cleanup

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/UI/AbilityCooldownUI.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Match/GameManager.cs`
- Modify via Unity Editor/MCP: `Assets/_Game/Settings/InputSystem_Actions.inputactions`
- Create via Unity Editor/MCP: `Assets/_Game/Settings/DefaultPowerGaugeConfig.asset`
- Test: `Assets/_Game/Scripts/Tests/EditMode/PowerGaugeTests.cs`

- [ ] Add a failing UI-facing test for the target gauge lookup where practical; leave geometry and visual feel to Play Mode.
- [ ] Remove the Restart semantic input and its GameManager use, then remove the R binding through Unity.
- [ ] Add an above-stamina HUD bar with a full-state color and wire target changes to the `PowerGauge` component.
- [ ] Create the default config asset with the approved initial tuning and assign it to Player and Opponent through Unity Editor/MCP.
- [ ] Run focused and full EditMode tests, inspect the Unity console, review the diff, and list the manual Play Mode check.
