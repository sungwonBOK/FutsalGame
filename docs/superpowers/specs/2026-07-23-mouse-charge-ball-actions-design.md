# Mouse Charge Pass and Shot Design

## Goal

Replace the current `F` pass and `Space` charge-shot controls with chargeable mouse actions. The player can turn the camera while charging and releases the ball toward the camera's horizontal forward direction at the moment the button is released.

## Controls

| Action | Default binding | Result |
| --- | --- | --- |
| Pass | Left mouse button | Hold to charge a pass; release to pass. |
| Shot | Right mouse button | Hold to charge a shot; release to shoot. |
| Cancel | `C` | Cancel the active pass or shot charge without releasing the ball. |

`F` and `Space` are removed from the active control path.

## Binding Boundary

Add a `PlayerActionBindings` ScriptableObject under `Runtime/Input/`.

- It owns the default mouse button and optional keyboard key for Pass, Shot, and Cancel.
- The default keyboard key for Pass and Shot is `None`; Cancel defaults to `C`.
- A small input reader converts those configured bindings into pressed, held, and released signals.
- `PlayerInput` consumes those signals and only coordinates movement, combat, and ball calls.

This keeps hardware choices out of gameplay code. A later settings UI or saved per-player binding system can update the same binding asset or replace it with a persistence-backed source without changing ball logic.

## Charge State and Direction

`BallInteractionController` owns one active charge:

- `None`, `Pass`, or `Shot`.
- Starting a charge stores only its action type and start time. It does not store aiming direction.
- Releasing the same configured action button calculates force from the held time and uses the current planar camera-forward direction.
- Pass and shot use their own configured minimum and maximum force. They share the existing maximum charge duration.
- The first action started owns the charge. A different action pressed while charging is ignored.
- `C` clears the active charge. The later button-release event finds no active charge and cannot launch the ball.
- Losing possession, stun, or inactive play continues to cancel the charge through the existing cancellation path.

The planar camera-forward helper removes the vertical component, normalizes the remaining vector, and falls back to the player's forward direction when a camera reference is unavailable.

## Data Flow

```text
PlayerActionBindings + mouse/keyboard state
  -> action input reader
  -> PlayerInput
  -> BallInteractionController (start / release / cancel)
  -> PlayerBallHandler facade
  -> BallPossessionController and BallController physics
```

## Scope and Verification

Modify only the input-to-ball action path, ball charge data, and focused tests. Do not modify Scene, Prefab, Input Actions YAML, camera setup, combat, match flow, UI, or networking.

Tests will cover:

1. Each action's charge amount and minimum/maximum force.
2. Release using the latest camera direction instead of the direction at charge start.
3. Cancel preventing a later release from launching the ball.
4. A second action not replacing an active charge.
5. Configured keyboard alternatives and default mouse bindings.
