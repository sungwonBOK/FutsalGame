### Task 4: Route match, camera, and binding display consumers

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Match/GameManager.cs`
- Modify: `Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs`
- Modify: `Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/MatchResetTests.cs`

**Interfaces:**
- Consumes: a serialized `GameplayInputReader` reference and its semantic button states.
- Produces: unchanged pause, restart, camera-toggle, and hint behavior.

- [ ] **Step 1: Write failing consumer-routing checks**

```csharp
Assert.That(File.ReadAllText(GameManagerPath), Does.Contain("GameplayInputAction.Pause"));
Assert.That(File.ReadAllText(CameraSwitcherPath), Does.Contain("GameplayInputAction.ToggleLegacyCamera"));
Assert.That(File.ReadAllText(ViewHintPath), Does.Contain("GetBindingDisplayString"));
```

- [ ] **Step 2: Run focused tests and verify RED**

Run: Unity EditMode `MatchResetTests` and the routing checks.

Expected: FAIL because each consumer still reads a raw key or embeds `F5`.

- [ ] **Step 3: Implement consumer routing**

```csharp
if (inputReader.ReadButton(GameplayInputAction.Pause).WasPressed)
    TogglePause();

if (inputReader.ReadButton(GameplayInputAction.ToggleLegacyCamera).WasPressed)
    thirdPerson = !thirdPerson;
```

Keep pause/restart readable outside active gameplay. Replace the visible fixed `F5` text with `inputReader.GetBindingDisplayString(GameplayInputAction.ToggleLegacyCamera)`.

- [ ] **Step 4: Run focused tests and verify GREEN**

Run: Unity EditMode `MatchResetTests`, input routing checks, and `ThirdPersonActionCameraTests`.

Expected: PASS.

- [ ] **Step 5: Commit global-consumer migration**

```powershell
git add Assets/_Game/Scripts/Runtime/Match/GameManager.cs Assets/_Game/Scripts/Runtime/Camera/CameraViewSwitcher.cs Assets/_Game/Scripts/Runtime/UI/ViewHintUI.cs Assets/_Game/Scripts/Tests/EditMode
git commit -m "refactor: route global controls through input actions"
```

