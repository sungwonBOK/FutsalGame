# Six-player Full-mesh P2P Gameplay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move six-player high-frequency gameplay from one-peer/Host-forwarded paths to a direct WebRTC full mesh while retaining MPS/NGO control authority.

**Architecture:** A client-ID-keyed registry owns isolated one-peer WebRTC connections and exposes packet broadcast/target APIs. NGO/MPS transports only membership, ready/start authority, and peer-addressed signaling; gameplay consumers depend on the registry rather than MPS or Relay.

**Tech Stack:** Unity 6, C#, NGO, Unity Multiplayer Services, `com.unity.webrtc`, NUnit EditMode tests.

## Global Constraints

- Maximum active room size is six; retain existing 3v3 lobby defaults.
- MPS/NGO retains roster, team, ready, start/end, score, timer, membership, and reconnect approval.
- Movement snapshots are lossy; combat and ball events are reliable; never use Host gameplay forwarding as fallback.
- Do not edit Unity YAML, prefabs, scenes, Input assets, packages, or ProjectSettings in this scope.

---

### Task 1: Addressed signaling and peer registry

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pPeerConnectionRegistry.cs`
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pPeerSignal.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pLobbySignalRelay.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pConnectionCoordinator.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pPeerConnectionRegistryTests.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pPeerSignalTests.cs`

**Interfaces:**
- Produces `P2pPeerSignal(ulong senderClientId, ulong recipientClientId, P2pSignalMessage signal)` and `P2pPeerConnectionRegistry.SetRequiredPeers(IReadOnlyCollection<ulong>)`.
- Produces `IsReadyFor(IReadOnlyCollection<ulong>)`, `TryBroadcast(P2pGameplayChannel, byte[])`, and `TrySendTo(ulong, P2pGameplayChannel, byte[])` for later consumers.

- [ ] **Step 1: Write failing policy tests** for self-target rejection, addressed message validation, required-peer readiness, and broadcast selection.
- [ ] **Step 2: Run the focused test assembly** and confirm compilation fails because the new types do not exist.
- [ ] **Step 3: Implement immutable addressed signaling and a pure registry policy** with client-ID validation and no Unity or Relay dependency.
- [ ] **Step 4: Run focused tests** and confirm all new policy tests pass.

### Task 2: Per-peer WebRTC lifecycle and NGO forwarding

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pConnectionCoordinator.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pLobbySignalRelay.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pGameplayReadinessTests.cs`

**Interfaces:**
- Consumes the Task 1 addressed signal and required-peer set.
- Produces a registry-backed, per-remote-peer connection lifecycle and Host-routed NGO signal transport.

- [ ] **Step 1: Add failing tests** demonstrating all required remote peers, rather than one global data channel, gate readiness.
- [ ] **Step 2: Run those tests** and verify the former single-peer readiness logic cannot satisfy them.
- [ ] **Step 3: Replace singleton-only connection usage** with a registry that creates, signals, and disposes one coordinator per remote client.
- [ ] **Step 4: Update the signal relay** so the Host forwards a signal only to its explicit recipient and clients submit signals to the Host.
- [ ] **Step 5: Run focused tests** and inspect that no gameplay packet uses NGO named messages.

### Task 3: Ready/start and gameplay packet routing

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsNetworkingModePolicy.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pMovementReplicator.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pCombatReplicator.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/BallAuthorityController.cs`
- Test: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pMatchStartPolicyTests.cs`

**Interfaces:**
- Consumes Task 2 registry readiness and send APIs.
- Produces MPS-enabled direct-P2P gating and registry-only high-frequency packet routing.

- [ ] **Step 1: Add failing policy tests** for Host-alone start and multi-participant all-ready plus complete-mesh start.
- [ ] **Step 2: Run focused tests** and confirm existing MPS policy rejects direct P2P.
- [ ] **Step 3: Permit direct P2P under MPS** and update LobbyController to derive required peers from active human membership.
- [ ] **Step 4: Route snapshot, combat, ball state/event, and presentation packets** through broadcast/target registry APIs.
- [ ] **Step 5: Run focused tests** and search gameplay paths for replaced single-peer sends.

### Task 4: Disconnect, reconnect, and documentation

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pPeerRecoveryPolicy.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/BallAuthorityController.cs`
- Modify: `IMPLEMENTATION_STATUS.md`
- Test: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pPeerRecoveryPolicyTests.cs`

**Interfaces:**
- Consumes control-plane membership and registry link readiness.
- Produces freeze/release/recovery decisions that leave membership and match state intact.

- [ ] **Step 1: Add failing tests** for disconnected freeze, ball release, incomplete-mesh reconnect denial, and restored pose/zero-velocity/unpossessed resume.
- [ ] **Step 2: Run focused tests** and confirm recovery policy is missing.
- [ ] **Step 3: Implement a pure recovery policy** and connect it to player pose and ball-authority boundaries.
- [ ] **Step 4: Update implementation status** with architecture changes and explicit staged runtime gates.
- [ ] **Step 5: Run focused and available full EditMode tests**, review `git diff --check`, then post an Issue #10 completion/risk comment.
