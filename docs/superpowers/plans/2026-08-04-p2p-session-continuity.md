# P2P Session Continuity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the current 1:1 direct-P2P readiness gate and reconnect status explicit, testable, and ready for the later ball channel.

**Architecture:** Pure P2P session policy classes own required-channel calculation, retry timing, and status copy. `P2pConnectionCoordinator` reports its available channels, while `LobbyController` consumes those reports to gate start and display status. The gameplay/ball authority paths remain unchanged.

**Tech Stack:** Unity C#, Unity WebRTC, Netcode for GameObjects, NUnit EditMode tests.

## Global Constraints

- Continue only in `D:\Unity Projects\FutsalGame` and preserve existing dirty combat files and `IMPLEMENTATION_STATUS.md`.
- Do not edit Scene, Prefab, Input Asset, Package, or ProjectSettings YAML.
- Current required direct channels are snapshot movement and combat; ball is opt-in after its own P2P migration.
- No automatic Relay/NGO gameplay fallback is introduced by this slice.
- Host+Guest Play Mode remains a manual validation gate.

---

### Task 1: Test and add direct-gameplay readiness policy

**Files:**
- Create: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pGameplayReadinessTests.cs`
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pGameplayReadiness.cs`

**Interfaces:**
- Produces: `P2pGameplayChannel`, `P2pGameplayReadiness.RequiredChannels`, and `P2pGameplayReadiness.IsReady`.

- [ ] **Step 1: Write failing tests**

```csharp
Assert.That(new P2pGameplayReadiness(P2pGameplayChannel.Snapshot | P2pGameplayChannel.Combat)
    .IsReady(P2pGameplayChannel.Snapshot), Is.False);
```

- [ ] **Step 2: Run the named EditMode tests and confirm the missing-type failure.**

- [ ] **Step 3: Implement the minimal immutable readiness value.**

- [ ] **Step 4: Re-run the named EditMode tests and confirm they pass.**

### Task 2: Test and add reconnect scheduling/status policy

**Files:**
- Create: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pReconnectScheduleTests.cs`
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pReconnectSchedule.cs`

**Interfaces:**
- Produces: retry delay selection and `P2pSessionStatus` text without depending on Unity WebRTC.

- [ ] **Step 1: Write failing tests for the initial retry, capped backoff, and host-unavailable status.**
- [ ] **Step 2: Run the named EditMode tests and confirm the missing-type failure.**
- [ ] **Step 3: Implement the smallest stateful schedule and status formatter.**
- [ ] **Step 4: Re-run the named EditMode tests and confirm they pass.**

### Task 3: Wire direct readiness into WebRTC and the lobby

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pConnectionCoordinator.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pMatchStartPolicyTests.cs`

**Interfaces:**
- Consumes: `P2pGameplayReadiness` and `P2pReconnectSchedule`.
- Produces: `P2pConnectionCoordinator.IsGameplayReady` and the lobby's start gate/status message.

- [ ] **Step 1: Extend start-gate tests to require both snapshot and combat readiness.**
- [ ] **Step 2: Run the focused test and confirm it fails against the old snapshot-only gate.**
- [ ] **Step 3: Report snapshot/combat availability from the coordinator and make the lobby consume the aggregate result.**
- [ ] **Step 4: Re-run focused tests and check the Unity Console for compile errors.**

### Task 4: Verify without overstating runtime coverage

**Files:**
- Modify: `docs/superpowers/specs/2026-08-04-p2p-session-continuity-design.md` only if evidence changes.

- [ ] **Step 1: Run the new focused EditMode tests and then the full EditMode suite.**
- [ ] **Step 2: Review `git diff --check` and the narrow diff, preserving unrelated dirty files.**
- [ ] **Step 3: Record that Host+Guest Play Mode, ball P2P, ghost rendering, and full reconnect remain unverified.**
