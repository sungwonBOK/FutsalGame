### Task 5: Wire the active scene and verify the final boundary

**Files:**
- Modify through Unity Editor/MCP: `Assets/_Game/Scenes/SampleScene.unity`
- Modify: `IMPLEMENTATION_STATUS.md`

**Interfaces:**
- Consumes: the action asset and `GameplayInputReader` from Tasks 1-4.
- Produces: assigned reader references on PlayerInput, GameManager, CameraViewSwitcher, and ViewHintUI.

- [ ] **Step 1: Inspect before scene mutation**

Use Unity MCP to verify the active `SampleScene`, the Player, GameManager, Main Camera, and UI host components, then confirm the editor is idle.

- [ ] **Step 2: Assign references through Unity Editor/MCP**

Add `GameplayInputReader` to the selected scene input host, assign `InputSystem_Actions.inputactions`, and set the same reader reference on PlayerInput, GameManager, CameraViewSwitcher, and ViewHintUI. Remove the obsolete PlayerActionBindings reference only after all references resolve.

- [ ] **Step 3: Wait for compilation and check the console**

Poll `mcpforunity://editor/state` until compilation and domain reload complete, then query Unity console errors and warnings.

Expected: no compile errors.

- [ ] **Step 4: Run concise automated verification**

Run: focused input tests, then the full Unity EditMode suite.

Expected: all discovered EditMode tests pass.

- [ ] **Step 5: Review and document**

Run `git diff --check`, inspect the changed-file list, and update `IMPLEMENTATION_STATUS.md` with the action-asset/reader boundary and test result. Post the actual file scope, verification, manual Play Mode checklist, and risks to issue #1, with coordinated notes for issues #2, #3, #4, #5, and #7.

- [ ] **Step 6: Commit the scene wiring and status**

```powershell
git add Assets/_Game/Scenes/SampleScene.unity IMPLEMENTATION_STATUS.md
git commit -m "feat: wire unified gameplay input"
```
