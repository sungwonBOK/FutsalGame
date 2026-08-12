# Standard MPS Player-Count and Direct-P2P Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the dedicated MPS test-room flow with the existing public MPS flow while retaining four-channel WebRTC P2P and repairing terminal setup failure classification.

**Architecture:** `P2pPlayerCountPolicy` supplies the one-player MPS exception. `MpsSessionRoomService` returns to its public-room-only boundary. `P2pConnectionFailurePolicy` centralizes the decision to turn a premature channel/transport close into a failed coordinator so the current Lobby reconnect path runs.

**Tech Stack:** Unity 6000.5.3f1, Unity Multiplayer Services, Netcode for GameObjects, Unity WebRTC, NUnit EditMode tests.

## Global Constraints

- Keep all four existing DataChannels and their QoS options.
- Do not change ICE server settings, packages, ProjectSettings, scenes, prefabs, or gameplay packet routes.
- One-player bypass applies only when `usesMpsRelaySession` is true; two through six MPS participants still require mesh plus Game ready.
- Preserve the legacy Relay join-code and LAN routes.

---

### Task 1: Restore public-MPS-only room controls

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/IRoomService.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsSessionRoomService.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/RoomAndSignalingContractTests.cs`

**Interfaces:**
- `IRoomService` exposes only `CreatePublicRoomAsync`, `BrowsePublicRoomsAsync`, and `JoinPublicRoomAsync`.

- [x] Write a failing contract test that dedicated test-room methods are absent.
- [x] Run focused EditMode tests and observe the contract failure.
- [x] Remove dedicated room methods, markers, UI buttons, handlers, and test-only state; keep the normal public MPS methods unchanged.
- [x] Re-run focused EditMode tests.

### Task 2: Apply one-to-six policy to standard MPS rooms

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pPlayerCountPolicy.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pPlayerCountPolicyTests.cs`

**Interfaces:**
- `CanStartWithoutDirectP2p(int playerCount, bool isMpsRelaySession)` returns true only for one MPS player.
- `LobbyController` passes its current session mode to that policy and resets the MPS mode before a legacy Relay or LAN connection starts.

- [x] Write failing tests for one-player bypass and two-player requirement without a test-room flag.
- [x] Run focused EditMode tests and observe the missing-signature failure.
- [x] Implement the minimal policy and use it in the MPS start and room UI gates.
- [x] Re-run focused EditMode tests.

### Task 3: Classify terminal direct-P2P closures as reconnectable failures

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pConnectionFailurePolicy.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2P/P2pConnectionCoordinator.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pConnectionFailurePolicyTests.cs`

**Interfaces:**
- `ShouldFailOnDataChannelClose(P2pConnectionState state)` is true for `Negotiating` and `Ready`.
- `ShouldFailOnTransportTerminalState(bool failed, bool closed)` is true when either terminal condition is true.

- [x] Write failing policy tests.
- [x] Run focused EditMode tests and observe the missing-type failure.
- [x] Add the policy and apply it to all four channel close callbacks and ICE/peer connection failed-or-closed callbacks.
- [x] Re-run focused EditMode tests.

### Task 4: Record static audit and verify

**Files:**
- Modify: `IMPLEMENTATION_STATUS.md`
- Modify: `docs/superpowers/specs/2026-08-12-standard-mps-player-count-and-p2p-audit-design.md`

- [x] Record the one-peer-connection/four-channel ICE contract and STUN-only runtime limitation.
- [x] Run Unity compile check, focused tests, full EditMode tests, and `git diff --check`.
