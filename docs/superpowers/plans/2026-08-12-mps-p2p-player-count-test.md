# MPS P2P Player-Count Test Implementation Plan

> Superseded on 2026-08-12 by `2026-08-12-standard-mps-player-count-and-p2p-audit.md`; the dedicated shared test-room approach was removed in favor of the normal MPS public-room flow.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a shared one-to-six-player MPS test room and allow a host-alone match to start without direct P2P.

**Architecture:** `MpsSessionRoomService` remains the MPS adapter and gains explicit test-room create/query/join methods. A small pure policy describes when direct P2P and Game ready are required by connected-player count; `LobbyController` uses it for both the test UI and match start. Existing public-room and join-code paths stay untouched.

**Tech Stack:** Unity 6000.5.3f1, Unity Multiplayer Services 2.3.0, Netcode for GameObjects, Unity WebRTC, NUnit EditMode tests.

## Global Constraints

- Work only in `D:\Unity Projects\FutsalGame`; preserve unrelated worktree changes.
- The test room capacity is exactly six; one is a supported current-player count, not a session-capacity value.
- Do not change packages, ProjectSettings, scenes, prefabs, or WebRTC ICE configuration.
- Keep P2P transport readiness and user Game ready acknowledgements distinct for two through six players.
- Manual Editor-to-Editor validation is required before claiming runtime networking success.

---

### Task 1: Define and prove the player-count gate

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/P2pPlayerCountPolicy.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/P2pPlayerCountPolicyTests.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs`

**Interfaces:**
- Produces `P2pPlayerCountPolicy.RequiresDirectP2p(int connectedPlayerCount)` and `P2pPlayerCountPolicy.RequiresGameReady(int connectedPlayerCount)`.
- `LobbyController.SvStartMatch()` uses these rules before calling `P2pMatchStartPolicy`.

- [x] **Step 1: Write failing player-count tests**

```csharp
Assert.That(P2pPlayerCountPolicy.RequiresDirectP2p(1), Is.False);
Assert.That(P2pPlayerCountPolicy.RequiresDirectP2p(2), Is.True);
Assert.That(P2pPlayerCountPolicy.RequiresDirectP2p(6), Is.True);
Assert.That(P2pPlayerCountPolicy.IsSupported(7), Is.False);
```

- [x] **Step 2: Run the focused EditMode test and observe the missing-type failure**

Run: Unity Test Runner EditMode filter `P2pPlayerCountPolicyTests`.

- [x] **Step 3: Add the minimal policy and use it at the start gate**

```csharp
if (P2pPlayerCountPolicy.RequiresDirectP2p(connectedPlayerCount) && !isDirectP2pReady)
    return;
if (P2pPlayerCountPolicy.RequiresGameReady(connectedPlayerCount) && !areAllPlayersGameReady)
    return;
```

- [x] **Step 4: Re-run focused and full EditMode tests**

### Task 2: Add a six-player shared MPS test room

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/IRoomService.cs`
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsSessionRoomService.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/RoomAndSignalingContractTests.cs`

**Interfaces:**
- Produces `Task<MpsRoomDefinition> CreatePlayerCountTestRoomAsync()`.
- Produces `Task<MpsRoomDefinition> FindPlayerCountTestRoomAsync()` and `Task JoinPlayerCountTestRoomAsync()`.
- Test room sessions include a public indexed property unique to this test flow and use `MpsRoomDefinition.MaximumPlayers`.

- [x] **Step 1: Write failing contract tests**

```csharp
Assert.That(typeof(IRoomService).GetMethod("CreatePlayerCountTestRoomAsync"), Is.Not.Null);
Assert.That(typeof(IRoomService).GetMethod("JoinPlayerCountTestRoomAsync"), Is.Not.Null);
```

- [x] **Step 2: Run the focused EditMode test and observe the missing-method failure**

Run: Unity Test Runner EditMode filter `RoomAndSignalingContractTests`.

- [x] **Step 3: Implement the MPS test-room methods**

```csharp
new SessionProperty(PlayerCountTestPropertyValue, VisibilityPropertyOptions.Public, PropertyIndex.String2)
```

Create uses `MaxPlayers = MpsRoomDefinition.MaximumPlayers`; join queries available rooms by the build index and test-room index, then joins the most recently updated compatible room. No-match throws a clear `InvalidOperationException`.

- [x] **Step 4: Re-run focused and full EditMode tests**

### Task 3: Surface the test controls and status

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs`
- Modify: `IMPLEMENTATION_STATUS.md`

**Interfaces:**
- Consumes `CreatePlayerCountTestRoomAsync()` and `JoinPlayerCountTestRoomAsync()`.
- Adds `Create 1-6 player test room` and `Join 1-6 player test room` actions in the existing online screen.

- [x] **Step 1: Write the failing UI-facing pure-policy test**

```csharp
Assert.That(P2pPlayerCountPolicy.RequiresGameReady(1), Is.False);
Assert.That(P2pPlayerCountPolicy.RequiresGameReady(2), Is.True);
```

- [x] **Step 2: Run it and observe the required behavior is absent**

Run: Unity Test Runner EditMode filter `P2pPlayerCountPolicyTests`.

- [x] **Step 3: Add only the two test-room buttons and asynchronous handlers**

Handlers set the existing connection status, set `usesMpsRelaySession` before the MPS operation, clear it on failure, and leave the existing public MPS controls untouched.

- [x] **Step 4: Document the manual verification matrix**

Record the host-alone, two-editor, and six-editor gates in `IMPLEMENTATION_STATUS.md`; do not claim them as performed.

- [x] **Step 5: Run focused/full EditMode tests and inspect `git diff --check`**
