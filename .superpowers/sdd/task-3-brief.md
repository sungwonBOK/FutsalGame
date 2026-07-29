### Task 3: Route player actions through semantic input

**Files:**
- Modify: `Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs`
- Delete after replacement: `Assets/_Game/Scripts/Runtime/Input/PlayerActionBindings.cs`
- Delete after replacement: `Assets/_Game/Scripts/Runtime/Input/PlayerActionInputReader.cs`
- Delete through Unity Editor/MCP after replacement: `Assets/_Game/Settings/DefaultPlayerActionBindings.asset`
- Modify: `Assets/_Game/Scripts/Tests/EditMode/PlayerActionInputReaderTests.cs`

**Interfaces:**
- Consumes: `GameplayInputReader.ReadMove()` and `ReadButton(GameplayInputAction action)` from Task 1.
- Produces: unchanged calls to `CharacterLocomotion`, `PlayerBallHandler`, and `CombatController`.

- [ ] **Step 1: Replace the legacy test with a failing semantic-routing test**

```csharp
[Test]
public void PlayerInput_UsesMoveAndSprintActionsInsteadOfRawKeyboardControls()
{
    string source = File.ReadAllText(PlayerInputPath);
    Assert.That(source, Does.Contain("inputReader.ReadMove()"));
    Assert.That(source, Does.Not.Contain("Keyboard.current"));
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run: Unity EditMode `PlayerActionInputReaderTests`.

Expected: FAIL because `PlayerInput` still reads keyboard and legacy bindings.

- [ ] **Step 3: Implement minimal semantic routing**

```csharp
Vector2 moveInput = inputReader.ReadMove();
bool sprint = inputReader.ReadButton(GameplayInputAction.Sprint).IsPressed;
GameplayInputButtonState pass = inputReader.ReadButton(GameplayInputAction.Pass);
```

Use the reader for `Move`, sprint, dodge, punch, slide, pass, shot, and cancel. Preserve the current `GameManager.PlayActive`, stun, charge-release, action-direction, and camera-relative movement logic. Remove raw `Keyboard`/`Mouse` use and the legacy binding asset reader only after the replacement compiles.

- [ ] **Step 4: Run focused input and existing movement/ball tests**

Run: Unity EditMode `PlayerActionInputReaderTests`, `CameraInputDirectionTests`, and `BallInteractionControllerTests`.

Expected: PASS.

- [ ] **Step 5: Commit player migration**

```powershell
git add Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs Assets/_Game/Scripts/Runtime/Input Assets/_Game/Scripts/Tests/EditMode
git commit -m "refactor: route player controls through input actions"
```

