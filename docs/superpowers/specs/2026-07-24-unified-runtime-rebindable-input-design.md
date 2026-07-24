# Unified Runtime-Rebindable Input Design

Date: 2026-07-24

## Goal

Make every listed gameplay control configurable through Unity Input System actions, including WASD and arrow-key movement. Keep camera-relative movement behavior unchanged. Prepare the input boundary so a future settings screen can let a player rebind controls during play and preserve those choices across launches.

## Scope

The affected default controls are:

- `Move`: W/A/S/D and arrow keys, camera-relative movement.
- `Sprint`: left and right Shift; the existing possession dribble-touch behavior remains tied to the sprint action.
- `Pass`: left mouse button.
- `Shot`: right mouse button.
- `CancelCharge`: C.
- `Dodge`: L.
- `Punch`: J.
- `SlideTackle`: K.
- `Pause`: Escape.
- `Restart`: R and Space.
- `ToggleLegacyCamera`: F5.

Mouse look stays outside this change. `MouseLookInput` continues to read pointer delta because it is not one of the requested rebindable controls.

## Architecture

`Assets/_Game/Settings/InputSystem_Actions.inputactions` is the authoritative source of default bindings. The existing `Player` action map retains `Move` and `Sprint`; it gains explicit, gameplay-named actions for the remaining controls. Existing unused generic actions are not removed in this change.

`Assets/_Game/Scripts/Runtime/Input/GameplayInputReader.cs` owns the enabled action map and exposes typed, key-name-free action states. It is assigned the action asset through a serialized reference and is placed on the scene's input host. It does not invoke gameplay behavior itself.

Gameplay consumers keep their current responsibilities:

- `PlayerInput` reads `Move`, `Sprint`, ball-action, dodge, and combat states from `GameplayInputReader`, then routes them to locomotion, ball, and combat APIs.
- `GameManager` reads `Pause` and `Restart` from the reader.
- `CameraViewSwitcher` reads `ToggleLegacyCamera` from the reader.
- `ViewHintUI` queries the reader for the effective display binding of the camera-toggle action instead of embedding `F5` in its text.

No consumer checks `Keyboard.current`, `Mouse.current`, or a key name for these actions after migration. Input dependencies point outward from `Input`; gameplay folders do not depend on each other to interpret controls.

## Runtime Rebinding Preparation

The default action asset ships in every build and is never mutated at runtime. Future rebinding will use `InputAction` binding overrides:

1. A settings UI asks an `InputRebindService` in `Input/` to perform interactive rebinding for an action binding.
2. The service applies the override to the active action asset copy.
3. `InputBindingOverridesStore` serializes `SaveBindingOverridesAsJson()` data to an application persistent-data file.
4. Startup loads that JSON through `LoadBindingOverridesFromJson()` after the default asset is enabled.
5. Reset removes the overrides and restores the default asset bindings.

Those two services and the settings UI are explicitly deferred. The initial implementation exposes stable action names and effective binding-display lookup so they can be added without rewriting gameplay consumers.

## Scene and Asset Safety

The `.inputactions` asset and SampleScene references are changed only through Unity Editor/MCP-safe operations. The existing `PlayerActionBindings` asset and its reader are removed only after the action asset, reader, scene references, and tests fully replace them. No `.unity`, `.prefab`, `.asset`, or `.inputactions` YAML is edited directly.

The pre-existing `ProjectSettings/ProjectSettings.asset` modification is out of scope and must remain untouched.

## Error and State Rules

- A missing or disabled input reader produces neutral action states rather than throwing.
- `Pause` and `Restart` remain readable even when gameplay movement is disabled by kickoff, stun, pause, or game-over state.
- A charging pass or shot preserves the existing mutually-exclusive charge and cancellation behavior.
- Multiple bindings for one action remain alternatives: either Shift sprints and either R or Space restarts.

## Tests and Verification

Tests are written first and prove:

1. The action asset contains each required action with the requested default bindings, including both WASD/arrow movement and both Shift keys.
2. `GameplayInputReader` produces action states from Input System test devices and supports alternative bindings.
3. Player, match, and camera consumers route semantic input states without direct raw keyboard reads.
4. The effective binding label changes when a binding override is applied.
5. Existing camera-relative movement, charge cancellation, pause/restart, and camera-toggle behavior remain covered.

After implementation, Unity compilation, focused EditMode tests, the full EditMode suite, console review, scene reference inspection, and diff review are required. Manual Play Mode confirmation of each listed control and the visible binding hint remains a separate check.
