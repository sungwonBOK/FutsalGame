# Ball Dribble, Pass, and Shoot Design

## Goal

Make possession-based ball control easy to tune while keeping ball physics, possession rules, and player input responsibilities separate. This slice adds automatic close dribbling, timed sprint touches, direction-based passing, and the existing charged shot behavior.

## Scope

- `F`: immediately pass in the current action direction with a fixed force, then release possession.
- `Space`: keep the existing press-to-lock-direction, hold-to-charge, release-to-shoot behavior.
- `Shift` while possessing the ball: remain in the existing possession movement profile for 0.5 seconds, then kick the ball forward and release possession.
- After that release, the player uses the existing normal sprint profile to chase the free ball. Reacquiring the ball while Shift remains held starts a new 0.5-second wait.
- Releasing Shift, becoming stunned, forced release, passing, shooting, match inactivity, or losing possession cancels a pending sprint touch and any charge where applicable.

The first version deliberately excludes teammate targeting, aim assist, lob passes, pass/shot cooldown redesign, new animation states, new HUD, and Scene/Prefab/Input Action asset changes.

## Responsibility Boundaries

```text
PlayerInput
  Reads F, Space, Shift and supplies the current action direction.
        |
PlayerBallHandler
  Preserves the public facade used by AI, combat, UI, and match reset.
  Owns Unity presentation calls such as animation, sound, VFX, and camera shake.
        |
BallInteractionController
  Holds the small interaction rules: sprint-touch timer, pass request,
  charged-shot request, and cancellation.
        |
BallPossessionController       BallController
  acquire/reacquire/release     current owner, Rigidbody, Collider, free-ball physics
```

`BallInteractionController` is a plain runtime class in the existing `Ball` folder, following `BallPossessionController`. It does not introduce a generic action state machine. It uses only the small timer and flags required for the stated rules.

## Configuration

Keep all ball-behavior tuning in `BallConfig`; retain `CharacterMovementConfig` for character speed, acceleration, deceleration, and rotation profiles.

- `DribbleSettings` gains `sprintTouchInterval` with an initial value of `0.5` seconds and `sprintTouchForce` with an initial value of `3.5` for the forward impulse.
- A `PassSettings` group gains `force` with an initial value of `3.5` for the fixed F-pass impulse.
- `ShotSettings` keeps charged-shot values only. Its current minimum charged-shot force is separated conceptually from the new pass force, so changing pass distance cannot change minimum shot strength.
- `PossessionSettings.acquireRange` and `reacquireDelay` remain the authority for whether a player catches a free ball after a sprint touch or pass.

No new movement profile is added. Before a sprint touch the player still owns the ball and therefore uses the existing possession profile. Once the touch releases the ball, the existing sprint profile applies.

## Behavior Details

- Action direction is the active movement direction when present; otherwise it falls back to the player's facing direction.
- A pass takes the action direction at the F press. A sprint touch takes the current action direction at the moment its 0.5-second timer expires.
- A pass, shot, or forced release wins over a pending sprint touch and clears that timer.
- The free-ball reacquisition delay prevents the same player from instantly reclaiming a ball it has just released.
- AI-compatible no-argument shot APIs remain available through `PlayerBallHandler`.

## Files

Add:

- `Assets/_Game/Scripts/Runtime/Ball/BallInteractionController.cs`
- `Assets/_Game/Scripts/Tests/EditMode/BallInteractionControllerTests.cs` only for compact rule-level coverage

Modify:

- `Assets/_Game/Scripts/Runtime/Ball/BallConfig.cs`
- `Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs`
- `Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs`
- `Assets/_Game/Settings/DefaultBallConfig.asset` through Unity Editor/MCP, preserving its existing asset and GUID
- `PROJECT_STRUCTURE.md` and `IMPLEMENTATION_STATUS.md` only after the implementation is verified; preserve the current unrelated camera edits

No folder move or new folder is required. The existing `Runtime/Ball` and `Tests/EditMode` locations are the appropriate placements.

## Verification

Keep automated tests short and focused:

- sprint touch triggers only after its configured interval and is cancelled correctly;
- F-pass releases ownership and uses the supplied action direction;
- existing charged-shot direction capture remains intact;
- possession reacquisition delay still applies after an interaction release.

Manual Play Mode checks, rather than a large test suite, will confirm feel: basic dribble, 0.5-second sprint touch loop, Shift release cancellation, F-pass, charged shot, forced release, and reacquisition.
