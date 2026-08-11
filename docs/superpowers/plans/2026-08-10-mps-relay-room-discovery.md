# MPS Relay Room Discovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Implementation note (2026-08-10):** MPS 2.3.0 was integrated with `SessionOptions.WithRelayNetwork`, which starts the existing NGO Relay connection during Session create/join. This first slice therefore implements public create/browse/join-by-id only. Deferred Relay start, private-code join, region/map metadata, and ready-state networking remain follow-up work.

**Goal:** Let FutsalGame players create, browse, and join MPS-backed public/private rooms, then begin the existing NGO match through Unity Relay without a dedicated game server.

**Architecture:** `MpsRoomDefinition` and `MpsRoomBrowserPolicy` are pure domain boundaries with focused EditMode tests. `MpsSessionRoomService` adapts the Unity Multiplayer Services SDK and exposes room records to `LobbyController`; the UI never sees SDK objects. MPS owns Lobby/Relay lifecycle while existing NGO match spawning stays untouched.

**Tech Stack:** Unity 6000.5.3f1, Unity Multiplayer Services, Unity Authentication, Unity Relay, Netcode for GameObjects 2.13.1, NUnit EditMode.

## Global Constraints

- Add `com.unity.services.multiplayer` only through Unity Package Manager/MCP; do not hand-edit package JSON.
- Do not edit Scene, Prefab, Input Asset, ProjectSettings, ball, combat, or Direct P2P/WebRTC files in this slice.
- Preserve user-owned dirty files: `NetPlayer.prefab`, `SampleScene.unity`, `URPProjectSettings.asset`, and AfricanFootballPlayer assets/docs.
- Public rooms use a fixed build compatibility key and a maximum of six players; Session properties never carry arbitrary user data.
- Host migration, quick match, reconnect, and 3v3 performance claims are not part of this slice.

---

### Task 1: Add the MPS package and verify its API surface

**Files:**
- Modify through Unity Package Manager: `Packages/manifest.json`, `Packages/packages-lock.json`
- Modify: the ExperimentalNet assembly definition only if Unity requires an MPS reference.

**Interfaces:**
- Consumes: Unity Package Manager and existing Authentication/NGO packages.
- Produces: `Unity.Services.Multiplayer` API available to runtime scripts.

- [ ] **Step 1: Add the package through Unity MCP**

Run:

```text
action=add_package
package=com.unity.services.multiplayer
```

- [ ] **Step 2: Verify package resolution**

Poll `mcpforunity://editor/state` until compiling is false, then inspect Console errors.

- [ ] **Step 3: Verify exact MPS APIs**

Use Unity reflection for `MultiplayerService`, `SessionOptions`, session create/query/join, and delayed Relay start. No SDK-facing source is written before this check.

- [ ] **Step 4: Commit package-only changes**

```powershell
git add Packages/manifest.json Packages/packages-lock.json
git commit -m "build: add unity multiplayer services"
```

### Task 2: Add pure room contracts and browser policy

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsRoomDefinition.cs`
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsRoomBrowserPolicy.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/MpsRoomDefinitionTests.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/MpsRoomBrowserPolicyTests.cs`

**Interfaces:**
- Consumes: no Unity or MPS SDK types.
- Produces: `MpsRoomDefinition.TryCreate`, `MpsRoomDefinition.IsCompatibleWith`, and `MpsRoomBrowserPolicy.FilterCompatible`.

- [ ] **Step 1: Write failing tests**

```csharp
[Test]
public void TryCreate_TrimsNameAndKeepsTheSixPlayerLimit()
{
    bool created = MpsRoomDefinition.TryCreate("  Friday Futsal  ", 6, true, "build-1", out MpsRoomDefinition room);

    Assert.That(created, Is.True);
    Assert.That(room.Name, Is.EqualTo("Friday Futsal"));
    Assert.That(room.MaxPlayers, Is.EqualTo(6));
}

[Test]
public void FilterCompatible_ExcludesFullAndDifferentBuildRooms()
{
    MpsRoomDefinition compatible = MpsRoomDefinition.ForRemote("A", 6, 2, true, "build-1");
    MpsRoomDefinition full = MpsRoomDefinition.ForRemote("B", 6, 6, true, "build-1");
    MpsRoomDefinition differentBuild = MpsRoomDefinition.ForRemote("C", 6, 1, true, "build-2");

    Assert.That(MpsRoomBrowserPolicy.FilterCompatible(
        new[] { compatible, full, differentBuild }, "build-1"),
        Is.EqualTo(new[] { compatible }));
}
```

- [ ] **Step 2: Run tests and verify RED**

Expected: the fixtures fail because the contract types are missing.

- [ ] **Step 3: Write minimal implementations**

```csharp
public static bool TryCreate(string name, int maxPlayers, bool isPrivate, string buildKey, out MpsRoomDefinition room)
{
    string normalized = name == null ? string.Empty : name.Trim();
    if (normalized.Length == 0 || normalized.Length > 32 || maxPlayers < 2 || maxPlayers > 6 || string.IsNullOrEmpty(buildKey))
    {
        room = default;
        return false;
    }

    room = new MpsRoomDefinition(normalized, maxPlayers, 1, isPrivate, buildKey);
    return true;
}
```

- [ ] **Step 4: Re-run focused and ExperimentalNet tests**

Expected: new fixtures and existing ExperimentalNet tests pass.

- [ ] **Step 5: Commit**

```powershell
git add Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsRoomDefinition.cs Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsRoomBrowserPolicy.cs Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/MpsRoomDefinitionTests.cs Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/MpsRoomBrowserPolicyTests.cs
git commit -m "feat: add mps room discovery contracts"
```

### Task 3: Adapt MPS Sessions behind one service

**Files:**
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsSessionRoomService.cs`
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsRoomLifecyclePolicy.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/MpsRoomLifecyclePolicyTests.cs`

**Interfaces:**
- Consumes: `MpsRoomDefinition`, `MpsRoomBrowserPolicy`, existing UGS initialization, and Task 1 verified MPS APIs.
- Produces: `CreateRoomAsync`, `FindPublicRoomsAsync`, `JoinRoomAsync`, `JoinByCodeAsync`, `StartRelayNetworkAsync`, and `LeaveAsync`.

- [ ] **Step 1: Write the failing network-start policy test**

```csharp
[Test]
public void CanStartNetwork_RequiresHostAndEveryMemberReady()
{
    Assert.That(MpsRoomLifecyclePolicy.CanStartNetwork(false, true), Is.False);
    Assert.That(MpsRoomLifecyclePolicy.CanStartNetwork(true, false), Is.False);
    Assert.That(MpsRoomLifecyclePolicy.CanStartNetwork(true, true), Is.True);
}
```

- [ ] **Step 2: Run the fixture and verify RED**

Expected: failure because `MpsRoomLifecyclePolicy` does not exist.

- [ ] **Step 3: Implement the minimal policy and service**

The service initializes UGS, maps only `Name`, `MaxPlayers`, `IsPrivate`, and `BuildKey` to Session properties, maps query results back to `MpsRoomDefinition`, and calls the verified MPS delayed Relay-start method only when `CanStartNetwork` returns true. Catch SDK exceptions into a user-safe operation result.

- [ ] **Step 4: Verify GREEN**

Run lifecycle and room contract tests, then inspect Unity Console after compilation.

- [ ] **Step 5: Commit**

```powershell
git add Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsSessionRoomService.cs Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsRoomLifecyclePolicy.cs Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/MpsRoomLifecyclePolicyTests.cs
git commit -m "feat: add mps session room service"
```

### Task 4: Replace only the Online entry UI path

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs`
- Create: `Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsRoomCreatePolicy.cs`
- Create: `Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/MpsRoomCreatePolicyTests.cs`
- Modify: `IMPLEMENTATION_STATUS.md` only when no longer user-dirty.

**Interfaces:**
- Consumes: MPS service operation results and `MpsRoomDefinition`.
- Produces: create, refresh/public browse, join-by-id, private-code join, and host-start Relay lifecycle through existing OnGUI.

- [ ] **Step 1: Write the failing create-request test**

```csharp
[Test]
public void CanSubmit_RejectsBlankNameBeforeTheServiceCall()
{
    Assert.That(MpsRoomCreatePolicy.CanSubmit("   ", 6, "build-1"), Is.False);
    Assert.That(MpsRoomCreatePolicy.CanSubmit("Friday", 6, "build-1"), Is.True);
}
```

- [ ] **Step 2: Run the fixture and verify RED**

Expected: failure because `MpsRoomCreatePolicy` does not exist.

- [ ] **Step 3: Implement the smallest UI replacement**

Keep the existing Online and team-slot UI. Add room name, public/private toggle, create, refresh, a Join button for each compatible public room, and code input. Replace only the Online screen's direct allocation calls. Keep LAN and all direct-P2P status code untouched.

- [ ] **Step 4: Verify GREEN and inspect scope**

Run full EditMode suite, `git diff --check`, and Unity Console. Confirm Scene/Prefab/URP/character files are absent from the diff.

- [ ] **Step 5: Commit**

```powershell
git add Assets/_Game/Scripts/Runtime/ExperimentalNet/LobbyController.cs Assets/_Game/Scripts/Runtime/ExperimentalNet/MpsRoomCreatePolicy.cs Assets/_Game/Scripts/Tests/EditMode/ExperimentalNet/MpsRoomCreatePolicyTests.cs
git commit -m "feat: add mps relay room browser"
```

### Task 5: Manual two-client Relay acceptance

**Files:**
- Modify: `IMPLEMENTATION_STATUS.md` only if it is clean and the test is actually performed.

- [ ] **Step 1: Verify a public room on two UGS profiles**

Host creates a public six-player room; a guest sees it after refresh with one occupied slot and can join.

- [ ] **Step 2: Verify private code join**

A private room never appears in browse results and joins only with its Session code.

- [ ] **Step 3: Verify match start**

Both members choose teams and ready. Host starts; capture Relay/NGO connection confirmation and match start. Verify departure shuts down cleanly.

- [ ] **Step 4: Record only performed observations**

Do not claim 3v3 or host migration. Record profile/network topology, Console errors, and the remaining six-client performance gate.

## Self-review

- Tasks 1-4 cover package adoption, validated room contracts, Session create/query/join, delayed Relay start, and the existing UI path.
- Direct P2P, gameplay authority changes, scene/prefab edits, host migration, matchmaking, reconnect, and 3v3 tuning are excluded.
- MPS APIs are verified after package installation before SDK-facing code is written.
