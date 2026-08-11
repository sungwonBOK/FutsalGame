# P2P Power Primary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make an armed R + LMB perform a replicated lob pass while possessing the ball or a defender-resolved 0.7-second stun while not possessing it.

**Architecture:** Reuse the current `PowerActivationController` as the local arm/consume gate. Extend the existing direct-P2P ball action event with a lob-pass discriminator and the existing direct-P2P combat request/result flow with a no-knockback power-stun discriminator; ball authority continues to publish the actual Rigidbody velocity.

**Tech Stack:** Unity, New Input System, Netcode for GameObjects, Unity WebRTC direct-P2P channels, NUnit EditMode tests.

## Global Constraints

- Preserve the existing dirty scene, prefab, MPS lobby, package, and project-setting changes.
- Do not edit Unity YAML directly; use Unity MCP for any required prefab configuration.
- Do not add animation, VFX, a new P2P channel, or a second power-gauge implementation.
- A rejected primary action leaves the armed full gauge intact.

---

### Task 1: Power-primary action contracts

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Characters/Power/PowerActivationState.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pBallProtocol.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pCombatProtocol.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/PowerActivationStateTests.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pBallProtocolTests.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pCombatProtocolTests.cs`

- [ ] Write failing tests for `EnhancedActionKind.PowerPrimary`, a round-trippable `LobPass` ball action, and a round-trippable `PowerStun` combat action.
- [ ] Run the focused EditMode tests and confirm each fails because the enum value or codec acceptance is absent.
- [ ] Add only the new enum members and extend existing known-value guards.
- [ ] Re-run the focused tests and confirm they pass.

### Task 2: Local primary effects and direct-P2P result routing

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Input/ContextualPlayerActionRouter.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Ball/BallInteractionController.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Combat/CombatController.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pCombatReplicator.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/BallAuthorityController.cs`
- Test: focused runtime and P2P protocol tests under `Assets/_Game/Scripts/Tests/EditMode/`

- [ ] Write failing tests showing an armed possession primary produces a pass impulse with positive vertical velocity and an armed no-possession primary only consumes after a valid front target is accepted.
- [ ] Run focused tests and confirm the expected missing-effect failures.
- [ ] Implement a fixed 0.7-second, no-knockback power stun through the defender-resolved combat path, and publish lob passes using the existing ball authority event/state path.
- [ ] Re-run focused tests and confirm they pass.

### Task 3: Network-player gauge availability and verification

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/NetworkPlayerAgent.cs`
- Modify through Unity MCP if required: `Assets/_Game/Prefabs/NetPlayer.prefab`
- Test: focused `NetworkPlayerAgent` or power-gauge EditMode test
- Modify: `IMPLEMENTATION_STATUS.md`

- [ ] Write a failing test for the selected network-player power-gauge setup path.
- [ ] Run it and confirm the gauge is absent or unconfigured before the implementation.
- [ ] Reuse `PowerGauge` and `PowerActivationController` for the locally owned network player; configure the existing default power gauge asset through the safe prefab path only if runtime wiring cannot preserve that reference.
- [ ] Run focused tests, the full EditMode suite, Unity compilation/console checks, and review the scoped diff.
- [ ] Record the remaining two-client Play Mode validation explicitly.
