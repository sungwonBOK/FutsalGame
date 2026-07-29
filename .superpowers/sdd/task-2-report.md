# Task 2 Report: Player input action asset contract

## Scope

- Updated only the `Player` map in `Assets/_Game/Settings/InputSystem_Actions.inputactions`.
- Extended `Assets/_Game/Scripts/Tests/EditMode/GameplayInputReaderTests.cs` with an asset-level default-binding contract.
- Did not modify PlayerInput, scenes, ProjectSettings, runtime consumers, or existing generic Player actions/bindings.

## TDD evidence

1. RED: `GameplayInputReaderTests.PlayerMap_ContainsTheGameplayInputBindingContract` failed because `Sprint` did not contain `<Keyboard>/rightShift`.
2. GREEN: `GameplayInputReaderTests` EditMode run passed: 4 total, 4 passed, 0 failed, 0 skipped.

## Unity-safe asset operation

The change was performed through the pinned Unity Editor instance `develop_merge_test@498cbd09b717313e` using Unity MCP `execute_code`, `InputActionAsset` setup APIs, `ToJson`, and `AssetDatabase.ImportAsset`. The first in-memory dirty/save attempt did not persist this importer-backed JSON asset, so the final operation serialized the edited `InputActionAsset` through its API and reimported it through the AssetDatabase.

## Result

- Preserved existing generic actions and bindings.
- Retained the existing Move keyboard composite and arrow alternatives.
- Added right Shift to Sprint.
- Added Pass, Shot, CancelCharge, Dodge, Punch, SlideTackle, Pause, Restart, and ToggleLegacyCamera with exactly the specified default bindings.
- `InputSystem_Actions.inputactions.meta` remained unchanged and was not staged.

## Deliberately excluded worktree changes

- `ProjectSettings/ProjectSettings.asset`
- `docs/superpowers/plans/2026-07-24-unified-runtime-rebindable-input.md`
- Existing `.superpowers/sdd` files other than this report
