# Ball Dribble, Pass, and Shoot Design

## Goal

Make ball control easy to tune while keeping ball physics, possession rules, and player input responsibilities separate. This slice adds automatic close dribbling, timed sprint touches, charged direction-based passing, charged shots, and intentional no-ball kick presentation.

## Scope

- `F`: press to lock the current action direction and start a pass charge; release to pass. A short tap uses the existing base pass force and a hold increases the force up to the configured maximum.
- `Space`: keep the existing press-to-lock-direction, hold-to-charge, release-to-shoot behavior.
- F and Space may start charging whether or not the player owns the ball. On release, owning the ball applies the calculated impulse and releases possession; not owning it plays the kick presentation only (a whiff) and does not affect ball physics.
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
  charged pass/shot requests, their locked directions, and cancellation.
        |
BallPossessionController       BallController
  acquire/reacquire/release     current owner, Rigidbody, Collider, free-ball physics
```

`BallInteractionController` is a plain runtime class in the existing `Ball` folder, following `BallPossessionController`. It does not introduce a generic action state machine. It uses only the small timer and flags required for the stated rules.

## Configuration

Keep all ball-behavior tuning in `BallConfig`; retain `CharacterMovementConfig` for character speed, acceleration, deceleration, and rotation profiles.

- `DribbleSettings` gains `sprintTouchInterval` with an initial value of `0.5` seconds and `sprintTouchForce` with an initial value of `3.5` for the forward impulse.
- `PassSettings` keeps `force` as the minimum/short-tap F-pass impulse (`3.5`) and adds `maxChargeForce` (`7.0`) plus `maxChargeTime` (`1.0` second). These values tune pass distance independently from shots.
- `ShotSettings` keeps charged-shot values only. Its current minimum charged-shot force is separated conceptually from the new pass force, so changing pass distance cannot change minimum shot strength.
- `PossessionSettings.acquireRange` and `reacquireDelay` remain the authority for whether a player catches a free ball after a sprint touch or pass.

No new movement profile is added. Before a sprint touch the player still owns the ball and therefore uses the existing possession profile. Once the touch releases the ball, the existing sprint profile applies.

## Behavior Details

- Action direction is the active movement direction when present; otherwise it falls back to the player's facing direction.
- A pass and shot take the action direction at their respective key press. A sprint touch takes the current action direction at the moment its 0.5-second timer expires.
- A short F tap releases a pass at `Pass.force`; holding F interpolates from `Pass.force` to `Pass.maxChargeForce` over `Pass.maxChargeTime`.
- A charge is allowed without possession. The release-time ownership check decides whether the kick releases the ball or is a whiff. Acquiring the ball during a charge therefore permits a normal release.
- Shot and pass releases always use the existing shot presentation. A whiff must not call `BallPossessionController.Release` or add force to a free ball.
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
- short and fully charged F-passes calculate the configured force and keep the direction captured at F press;
- charged pass and shot may start without ownership, while their release produces no possession release when ownership is still absent;
- existing charged-shot direction capture remains intact;
- possession reacquisition delay still applies after an interaction release.

Manual Play Mode checks, rather than a large test suite, will confirm feel: short/charged F-pass, short/charged Space shot, no-ball whiffs, acquire-during-charge release, basic dribble, 0.5-second sprint touch loop, Shift release cancellation, forced release, and reacquisition.
