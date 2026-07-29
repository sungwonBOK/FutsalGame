# Task 5 report - final unified input scene wiring

## Commit

- `c45e06f feat: wire unified gameplay input`

## Actual scope

- Unity Editor/MCP saved `Assets/_Game/Scenes/SampleScene.unity`.
- Reused the existing `Player` `GameplayInputReader` (component instance `60588`) backed by `Assets/_Game/Settings/InputSystem_Actions.inputactions`.
- Verified before and after save that `PlayerInput`, `GameManager`, `CameraViewSwitcher`, and `ViewHintUI` all point at that same reader.
- Updated `IMPLEMENTATION_STATUS.md` with the reader boundary, evidence, and manual follow-up.
- No second reader was added. No ProjectSettings, runtime settings UI, or persistence implementation was changed by this task.

## Scene diff

- Unity save produced the user-approved scene reserialization: `430 insertions`, `427 deletions`.
- The three semantic scene additions are `inputReader: {fileID: 887825636}` on `GameManager`, `CameraViewSwitcher`, and `ViewHintUI`.
- The remainder is Unity-generated reordering/recreation of embedded GoalNet mesh/physics-material serialization. It was not manually edited.
- `git diff --cached --check` reports 30 trailing-whitespace lines inside Unity-generated YAML blocks; they are not hand-edited because direct YAML cleanup is prohibited.

## Verification

- Unity MCP focused EditMode: `GameplayInputReaderTests`, `PlayerActionInputReaderTests`, and `MatchResetTests` = `9/9 passed`.
- Unity MCP full EditMode: `52/52 passed`, `0 failed`, `0 skipped`.
- The editor-state resource remained stale after the test run, so direct NUnit evidence was also inspected at `C:\Users\sungw\AppData\LocalLow\DefaultCompany\FutsalGame\TestResults.xml`: `52/52`, `failed=0`, `result=Passed`, EditMode.
- Final console query had no gameplay/compiler diagnostic. It listed two Unity Test Runner `Saving results to ...TestResults.xml` entries as `Exception`, plus stale-job/Test Runner setup-cleanup warnings.

## Manual Play Mode follow-up

- Start the active `SampleScene`; verify no missing-reference messages.
- Verify `Escape` pauses/resumes through `GameManager`.
- Verify the camera-toggle action updates the camera path and `ViewHintUI` binding display.
- Verify WASD/arrow movement, sprint, dodge, combat, pass, and shot still flow through the single reader.

## Preserved worktree changes

- Left untouched: `ProjectSettings/ProjectSettings.asset`, `docs/superpowers/plans/2026-07-24-unified-runtime-rebindable-input.md`, and existing `.superpowers/sdd/` task artifacts.
