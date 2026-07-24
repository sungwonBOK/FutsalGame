# Task 1 report: gameplay input reader

## Delivered files

- `Assets/_Game/Scripts/Runtime/Input/GameplayInputAction.cs`
  - Adds the eleven semantic `GameplayInputAction` values and the independent `GameplayInputButtonState` value type.
- `Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs`
  - Serialized `InputActionAsset` reader that owns only the `Player` action map.
  - Resolves semantic actions through one private mapping, exposes button/move/display reads, and returns neutral values for missing map/actions.
- `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs`
  - Covers effective binding override display, missing Player map, missing action, and Player map enable/disable lifecycle.

## TDD evidence

1. RED: Created the display-override contract before runtime production code. Unity compilation failed with `CS0246` for missing `GameplayInputReader`; the focused job `1f62c8a36780493f865955c7cf8fdf3a` found zero tests because that compile failure prevented discovery.
2. GREEN: Implemented the reader boundary and final focused job `d9b72795c636413ab0d0eb69b35f7345` passed all 4 EditMode tests in 0.095 seconds.

## Unity checks

- Targeted and pinned editor: `develop_merge_test@498cbd09b717313e`, Unity `6000.5.3f1`.
- Editor was idle, not in Play Mode, and compilation/domain reload completed before each focused run.
- Final console check had no compile errors. An existing MCP package transport warning (`WebSocket is not initialised`) was observed; it is outside project code and did not affect test execution.

## Self-review

- No changes to `PlayerInput`, scenes, input assets, ProjectSettings, or existing consumers.
- `OnEnable`/`OnDisable` manipulate only the resolved `Player` map; missing assets/maps/actions are safe neutral reads.
- Effective display strings are obtained from the action itself, so Unity Input System binding overrides are reflected.

## Concerns / follow-up

- `FutsalGame.EditModeTests.asmdef` does not reference `Unity.InputSystem`, while Task 1 scope excludes asmdef changes. The focused test therefore constructs and invokes the real Input System asset APIs through reflection; runtime production code directly uses the package as intended.
- This task intentionally does not wire a reader into the scene or migrate consumers; that remains later approved tasks.

## Review-fix report

### Corrected lifecycle contract

- Replaced the prior `Reader_EnablesAndDisablesOnlyItsPlayerMap` assertion, which observed only the Player map, with `Reader_EnablesOnlyPlayerMap_AndLeavesOtherMapDisabled`.
- The amended asset includes a distinct `Other` map. The test proves Player is enabled by the reader while Other stays disabled, then proves both maps are disabled after `OnDisable`.

### Fixture reduction

- Reduced the focused fixture from four tests and a generic overload-resolution helper to three required contracts with direct reflected Input System signatures.
- Kept only: effective override display, neutral values for absent map/action, and isolated Player-map ownership. The concise test remains necessary because the EditMode asmdef does not reference `Unity.InputSystem`; broader input feel remains a manual Play Mode concern for the later wiring task.

### Amended verification

- The original RED evidence above remains the Task 1 implementation RED (`CS0246` before the reader existed).
- This review amendment is a coverage correction, not a behavior change: the existing production implementation already enables only `playerMap`, so the newly precise contract passed on its first valid run without a production edit. No artificial failing assertion or temporary production regression was introduced.
- Unity EditMode job `e16be6b7d9b74f4f9a06b1caa9c48ee3`: **3 passed, 0 failed** in 0.091 seconds.
