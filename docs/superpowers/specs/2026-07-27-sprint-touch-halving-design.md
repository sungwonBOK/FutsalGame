# Sprint touch halving

## Goal

Halve the current forward touch force applied while possessing the ball during both normal sprint and burst sprint.

## Scope

- Change the default possession sprint-touch multiplier from `2.0` to `1.0`.
- Keep the burst sprint-touch multiplier at `1.4`.
- With the existing base touch force of `3.5`, normal sprint becomes `3.5` and burst sprint becomes `4.9`.

## Exclusions

- Do not change pass or shot force, movement speed, stamina drain, animation speed, input timing, scenes, prefabs, or serialized assets.
- Preserve the shared-worktree Grab, Combat, and ProjectSettings changes.

## Verification

1. Add a focused EditMode assertion for normal `3.5` and burst `4.9` sprint touches and observe the expected RED failure.
2. Apply the one default-multiplier change.
3. Run the focused test and full EditMode suite, then review the scoped diff.

## Manual check

In Play Mode, compare the post-release distance of normal Shift sprint and double-Shift burst sprint touches. Both should be half of their current distance, with burst remaining 1.4 times the normal touch.
