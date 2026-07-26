# Cross Punch animation replacement

## Goal

Make the existing `Punch` Animator trigger play `Assets/_Game/Characters/Cross Punch.fbx`.

## Scope

- Replace only the motion assigned to the `Punch` state in `Assets/_Game/Animation/FutsalCharacter.controller`.
- Apply the controller reference through the Unity Editor or Unity MCP; do not edit serialized Animator YAML directly.
- Preserve the existing `Punch` parameter, transitions, timing, and all runtime combat code.

## Explicitly out of scope

- Input mapping changes, including the planned no-ball left-click weak punch.
- Changes to hit timing, range, cooldown, knockback, stun, or ball ownership.
- New Animator parameters, states, or animation events.

## Runtime behavior

Every existing caller of `CharacterAnimator.PlayPunch()` continues to set the `Punch` trigger. The Animator enters its existing `Punch` state and plays Cross Punch. Hit detection and effects remain governed by the existing `CombatController` behavior.

## Verification

1. Confirm the `Punch` state references Cross Punch in the Animator after the Editor-safe change.
2. Wait for Unity compilation/import to finish and check the Console for errors.
3. Run the focused EditMode test set covering combat and contextual input, then inspect the diff to confirm no unrelated files are included.
4. Manually verify in Play Mode that an existing punch action visibly plays Cross Punch; this is required separately from EditMode evidence.
