# Task 3 Report: Route player controls through input actions

## Commit

- `53c02d7 refactor: route player controls through input actions`

## Scope completed

- `PlayerInput` now reads `Move`, `Sprint`, `Dodge`, `Punch`, `SlideTackle`, `Pass`, `Shot`, and `CancelCharge` from its serialized `GameplayInputReader`.
- The reader reference is nullable: an unwired reader yields neutral movement/button states, so Task 5 can assign scene references without this task changing a scene or prefab.
- Preserved the existing inactive/stunned reset, camera-relative direction conversion, `CharacterLocomotion`/`CombatController`/`PlayerBallHandler` calls, and release-time planar camera direction for charged pass and shot actions.
- Removed `PlayerActionBindings`, `PlayerActionInputReader`, `ActionButtonState`, and `DefaultPlayerActionBindings.asset`. The default asset was removed through Unity MCP `manage_asset(delete)`; no YAML was edited directly.
- Replaced the obsolete legacy-binding tests with a routing test that asserts semantic action calls and rejects raw keyboard/mouse and legacy binding-reader references in `PlayerInput`.

## TDD evidence

1. Added `PlayerInput_UsesSemanticGameplayInputActionsInsteadOfRawControls` first.
2. Unity EditMode RED: 1 test failed as expected because the pre-change source lacked `inputReader.ReadMove()` and still used `Keyboard.current` / legacy bindings.
3. Implemented the minimal semantic routing and confirmed the focused test GREEN: 1 passed, 0 failed.

## Verification

- Unity instance: `develop_merge_test@498cbd09b717313e` (Unity 6000.5.3f1).
- Preflight: editor idle, not in Play Mode, not compiling, ready for tools.
- Post-change compile: Unity refresh/domain reload completed; `read_console` returned 0 errors.
- Focused Unity EditMode suite: `PlayerActionInputReaderTests`, `CameraInputDirectionTests`, `BallInteractionControllerTests` = 16 passed, 0 failed, 0 skipped.
- Scoped `git diff --check` passed before commit.
- Project-wide source search found no remaining legacy binding/reader/state use outside the new test's negative assertions.

## Deliberate non-changes and remaining manual work

- Did not modify SampleScene, prefabs, `InputSystem_Actions.inputactions`, ProjectSettings, GameManager, CameraViewSwitcher, ViewHintUI, or gameplay behavior outside input wiring.
- No Play Mode/manual control-feel verification was performed. Task 5 must assign the shared reader and action asset in the scene, then manually verify movement, sprint, combat actions, and charged ball release behavior.
- Pre-existing uncommitted files remain untouched: `ProjectSettings/ProjectSettings.asset`, the unified-input plan document, and existing `.superpowers/sdd` task artifacts.

## Approved scene-wiring finalization (2026-07-25)

- The user explicitly approved committing the Unity-generated `SampleScene` serialization produced while wiring the Task 3 player input. No scene YAML was hand-edited.
- Committed the approved scene wiring as `b5e53e4 refactor: wire player input reader in sample scene`, following the Task 3 migration commit `53c02d7 refactor: route player controls through input actions`.
- Live Unity inspection of `develop_merge_test@498cbd09b717313e` (Unity `6000.5.3f1`) after an AssetDatabase refresh found active `SampleScene` and active `Player` object `53944`. `Player` contains `GameplayInputReader`; its live `GameplayInputReader.inputActions` is `Assets/_Game/Settings/InputSystem_Actions.inputactions`; and the live `PlayerInput.inputReader` resolves to that same reader. The obsolete `actionBindings` serialized property is absent.
- Unity refresh with a compile request completed with the editor idle, not playing, not compiling, no pending domain reload, and ready for tools. The focused EditMode job `e4f80c92117c4a47a4ea738e78cc6ea4` completed `16/16` passed, `0` failed, `0` skipped: `PlayerActionInputReaderTests`, `CameraInputDirectionTests`, and `BallInteractionControllerTests`.
- Console review found no C# compiler diagnostic. It did retain one MCP transport warning plus the Test Framework's informational pre/post-build setup warnings; the only entry returned by the `error` filter was Unity's `Saving results to ... TestResults.xml` message after the successful test job, with no stack trace or failed test.
- The approved scene-only diff is `406` insertions / `373` deletions. In addition to the intended `PlayerInput.actionBindings` removal, `PlayerInput.inputReader` assignment, and added `GameplayInputReader.inputActions` assignment, Unity reserialized inline goal-net meshes and related object/file IDs. This full-scene reserialization is the remaining review risk: it is Unity-generated and live scene references/tests pass, but creates a broad textual diff unrelated to gameplay behavior.
- Scoped `git diff --check -- Assets/_Game/Scenes/SampleScene.unity` reports Unity-generated trailing whitespace in moved mesh serialization blocks; it was intentionally not normalized because doing so would hand-edit generated YAML and broaden the approved change.
