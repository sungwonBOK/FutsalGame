# Task 4 Report: Global Semantic Input Consumers

## Scope

Updated only the approved global input consumers and their focused EditMode checks:

- `GameManager`: nullable serialized `GameplayInputReader`; semantic `Pause` and `Restart` button reads. Match timing still runs with no reader, while pause/restart safely do nothing until Task 5 assigns the scene reference.
- `CameraViewSwitcher`: nullable serialized reader and semantic `ToggleLegacyCamera` toggle; existing snap-on-toggle behavior is unchanged.
- `ViewHintUI`: nullable serialized reader and `GetBindingDisplayString(ToggleLegacyCamera)` label; existing visibility and layout are unchanged.
- `MatchResetTests`: three concise routing checks for pause, camera toggle, and binding display.

No scene, prefab, input-action asset, ProjectSettings, or gameplay logic was edited.

## TDD evidence

1. RED: Added the three consumer-routing checks first.
2. Unity EditMode job `feedc43ef8c141d1803b7730d23efcb0` failed as intended: each new assertion reported its missing semantic call; the pre-existing reset test passed.
3. GREEN: Implemented the minimum nullable-reader routing. NUnit result XML then recorded all selected tests passing.

## Verification

- Unity `develop_merge_test@498cbd09b717313e` (`6000.5.3f1`) refreshed and compiled after the production changes and after comment cleanup. `read_console` returned zero compiler diagnostics each time.
- Focused Unity EditMode run `d43361c382b046459fce648abcccdd15`: NUnit XML at `C:\Users\sungw\AppData\LocalLow\DefaultCompany\FutsalGame\TestResults.xml` records **18 passed, 0 failed, 0 skipped** in `0.2153873s`:
  - `MatchResetTests`: 4/4, including all three routing checks.
  - `ThirdPersonActionCameraTests`: 14/14.
- Scoped `git diff --check` passed.
- Direct source search found no `Keyboard.current` in `GameManager` or `CameraViewSwitcher`, and no visible `"F5:"` string in `ViewHintUI`.

## Concern / follow-up

- Unity MCP left focused job `d43361c382b046459fce648abcccdd15` marked `running` after it wrote the passing NUnit XML. A full EditMode retry was rejected with `tests_running`; this is a test-runner status-tracking limitation, not a test failure. Do not claim a fresh full-suite result from this task.
- Task 5 must assign the serialized readers in the scene. Until then null handling prevents exceptions; the view hint's binding prefix is empty rather than hard-coded.
- Manual Play Mode checks for pause/restart, camera toggle, and visible rebinding remain outstanding.

## Commit

`ae3fad5 refactor: route global controls through input actions`
