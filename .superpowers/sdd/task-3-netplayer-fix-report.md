# Task 3 NetPlayer prefab input-reference fix

## Scope

- Changed only `Assets/_Game/Prefabs/NetPlayer.prefab`.
- Added `GameplayInputReader` to the NetPlayer root.
- Assigned `Assets/_Game/Settings/InputSystem_Actions.inputactions` to its `inputActions` field.
- Assigned `PlayerInput.inputReader` to that same root component.

## Unity verification

- Target instance: `develop_merge_test@498cbd09b717313e` (Unity 6000.5.3f1).
- Saved the prefab through Unity's prefab stage, then reopened it. MCP component inspection confirmed the reader's input-actions asset reference and the PlayerInput reference to the reader.
- Editor returned to idle with no compilation/domain reload pending.
- Unity console errors after testing: 0. One existing MCP WebSocket warning remained; it is from `com.coplaydev.unity-mcp`, not project gameplay code.

## Focused tests

Unity EditMode job `06686649348f4ad3902d578be74f176a`: 5 passed, 0 failed, 0 skipped.

- `GameplayInputReaderTests` (4 input-action binding/reader tests)
- `PlayerActionInputReaderTests.PlayerInput_UsesSemanticGameplayInputActionsInsteadOfRawControls`

## Boundaries and remaining validation

- No NetworkManager, spawn/ownership, network behavior, scene, input asset, ProjectSettings, or gameplay source was changed.
- No additional test was added: this is a serialized-prefab wiring repair and Unity MCP structural verification covers the new references without expanding the test suite.
- Manual Host/Join Play Mode validation remains appropriate before claiming an end-to-end networked control flow; it was not run here.
