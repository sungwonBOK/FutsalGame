### Task 2: Move every requested default binding into the Input Action asset

**Files:**
- Modify through Unity Editor/MCP: `Assets/_Game/Settings/InputSystem_Actions.inputactions`
- Test: `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`

**Interfaces:**
- Consumes: the `GameplayInputAction` names from Task 1.
- Produces: the `Player` map actions used by every consumer.

- [ ] **Step 1: Extend the failing binding-contract test**

```csharp
AssertActionBindings(asset, "Move", "<Keyboard>/w", "<Keyboard>/upArrow", "<Keyboard>/a", "<Keyboard>/leftArrow", "<Keyboard>/s", "<Keyboard>/downArrow", "<Keyboard>/d", "<Keyboard>/rightArrow");
AssertActionBindings(asset, "Sprint", "<Keyboard>/leftShift", "<Keyboard>/rightShift");
AssertActionBindings(asset, "Pass", "<Mouse>/leftButton");
AssertActionBindings(asset, "Shot", "<Mouse>/rightButton");
AssertActionBindings(asset, "CancelCharge", "<Keyboard>/c");
AssertActionBindings(asset, "Dodge", "<Keyboard>/l");
AssertActionBindings(asset, "Punch", "<Keyboard>/j");
AssertActionBindings(asset, "SlideTackle", "<Keyboard>/k");
AssertActionBindings(asset, "Pause", "<Keyboard>/escape");
AssertActionBindings(asset, "Restart", "<Keyboard>/r", "<Keyboard>/space");
AssertActionBindings(asset, "ToggleLegacyCamera", "<Keyboard>/f5");
```

- [ ] **Step 2: Run the focused test and verify RED**

Run: Unity EditMode `GameplayInputReaderTests`.

Expected: FAIL because the newly named actions/bindings are absent or `Sprint` lacks right Shift.

- [ ] **Step 3: Update the action asset through Unity Editor/MCP**

In the existing `Player` map, retain and configure `Move` as the current keyboard composite plus arrow alternatives; add the missing right-Shift sprint binding; add `Pass`, `Shot`, `CancelCharge`, `Dodge`, `Punch`, `SlideTackle`, `Pause`, `Restart`, and `ToggleLegacyCamera` with exactly the bindings listed in Step 1. Do not delete the existing generic actions.

- [ ] **Step 4: Run the focused test and verify GREEN**

Run: Unity EditMode `GameplayInputReaderTests`.

Expected: PASS with every default binding present.

- [ ] **Step 5: Commit the input asset contract**

```powershell
git add Assets/_Game/Settings/InputSystem_Actions.inputactions Assets/_Game/Settings/InputSystem_Actions.inputactions.meta Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs
git commit -m "feat: define gameplay input actions"
```

