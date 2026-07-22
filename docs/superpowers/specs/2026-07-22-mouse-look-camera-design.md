# Mouse Look Camera Design

## Goal

Let the mouse rotate only the third-person camera. The character must move and rotate only while keyboard movement input is active.

## Scope

- Keep the current keyboard movement path: `PlayerInput` converts WASD to a camera-relative world direction and sends it to `CharacterLocomotion`.
- Add mouse look to the camera system only.
- Preserve the existing third-person and possession framing, collision, FOV, shake, and Cinemachine backend boundaries.
- Do not edit scene, prefab, input-action, or settings YAML directly.

## Architecture

`MouseLookInput` reads only the current mouse delta. `CameraLookController` owns yaw, pitch, sensitivity, inversion, and pitch clamping. `ThirdPersonActionCamera` samples the resulting view state and assembles the existing camera plan; it does not read the mouse or decide automatic heading.

`CameraDirector` and its modes continue to select a framing profile. They no longer select yaw from movement, velocity, action direction, or ball position. `PositionResolver` receives the manual yaw and pitch to place either the direct camera or the Cinemachine follow-rig target.

## Control Rules

- Moving the mouse changes camera yaw and pitch only.
- WASD movement remains relative to the camera's horizontal forward and right vectors.
- Character rotation remains the responsibility of `CharacterMotor`, triggered by non-zero movement from `CharacterLocomotion`.
- While the character is stationary, combat and ball actions keep using the character's current facing direction. Camera movement does not change that direction.
- Possession mode changes framing only; it must not take camera rotation away from the mouse.

## File Boundaries

- `Assets/_Game/Scripts/Runtime/Input/MouseLookInput.cs`: raw mouse-delta reader only.
- `Assets/_Game/Scripts/Runtime/Camera/Look/CameraLookController.cs`: manual yaw/pitch state and bounds only.
- `Assets/_Game/Scripts/Runtime/Camera/Look/CameraLookState.cs`: immutable yaw/pitch value passed to camera calculations.
- `Assets/_Game/Scripts/Runtime/Camera/ThirdPersonActionCamera.cs`: thin assembly of input, framing, pose, effects, FOV, and backend application.
- `CameraContext`, camera modes, and `CameraModeResult`: framing data only; no automatic-yaw fields.
- `PositionResolver`: converts a manual view state into a camera pose and collision distance.

## Removal Policy

After Play Mode confirmation, remove the obsolete auto-yaw code: `AimResolver`, the yaw compatibility methods on `ThirdPersonActionCamera`, and their tests. `CameraViewSwitcher` and `ViewHintUI` are separate cleanup work because the UI depends on the old F5 view-switch state.

## Verification

- EditMode tests prove yaw sensitivity, pitch clamping, and an unchanged look state for zero delta.
- Existing camera framing, FOV, and direct/Cinemachine backend tests remain green after auto-yaw tests are replaced.
- Play Mode confirms cursor locking, mouse-only camera rotation, camera-relative WASD movement, no character rotation from mouse-only movement, collision, and possession framing.
