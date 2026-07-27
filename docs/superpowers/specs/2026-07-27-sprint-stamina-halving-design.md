# Sprint stamina halving

## Goal

Reduce both normal and burst sprint stamina consumption to half their current values without changing sprint speed, double-tap timing, animation speed, or ball-touch behavior.

## Scope

- `CharacterLocomotion` applies a `0.5` multiplier to both sprint drain paths.
- A focused EditMode test verifies normal sprint drains `13` stamina per second and burst sprint drains `23.4` stamina per second from the default `26` drain rate.

## Exclusions

- No changes to `CharacterMovementConfig` assets, scenes, prefabs, input bindings, animation controllers, ball interactions, or combat.
- Existing shared-worktree Grab and Combat changes remain untouched.

## Verification

1. Add the focused rule test and observe its expected RED failure.
2. Apply the minimal multiplier change.
3. Run the focused test and full EditMode suite.
4. Confirm Unity has no game-code compilation errors and review the scoped diff.

## Manual check

In Play Mode, compare the gauge rate during a normal Shift sprint and a double-Shift burst sprint. Both should be half their previous rate; burst remains 1.8 times the normal sprint rate.
