# Contextual action catalog design

## Goal

Add a no-ball right-click Cross Punch while establishing a small, data-driven action boundary that can grow to future keyboard and mouse actions without coupling input code to Ball, Combat, or animation internals.

## User-visible behavior

- With no ball, left-click selects `BasicPunch`; right-click selects `CrossPunch`.
- With a ball, left-click and right-click retain their current pass-charge and shot-charge behavior.
- `CrossPunch` initially uses the same combat values as `BasicPunch`: range `1.3`, radius `0.7`, cooldown `1.2`, knockback `8`, hit stun `1`, and ball release on hit.
- The two punches track cooldowns independently.
- Cross Punch plays its own Animator state at speed `2.0`; Basic Punch remains speed `1.0`.
- Hit timing remains the existing immediate overlap check. Animation-event timing is outside this slice.

## Boundaries

| Concern | Owner | Contract |
|---|---|---|
| Physical bindings and button state | `Runtime/Input/GameplayInputReader` | Emits semantic primary, secondary, and future context actions; does not execute game behavior. |
| Context selection | `Runtime/Input` action resolver | Reads an immutable snapshot of possession context and produces an action request. It never changes ball ownership, physics, or combat state. |
| Action dispatch | `Runtime/Input` dispatcher | Routes a request to the owning Combat or Ball entry point; it does not contain hit or ball physics rules. |
| Combat tuning and execution | `Runtime/Combat` | Resolves combat action definitions, maintains per-action cooldowns, detects hits, applies knockback/stun, and requests a ball release only after a confirmed hit. |
| Ball authority | `Runtime/Ball` | `BallController` remains the only owner of actual possession and Rigidbody state. Combat reaches ball behavior only through `PlayerBallHandler.ForceRelease`. |
| Animation presentation | `Runtime/Characters` | Resolves an action's Animator trigger and speed; no hit, possession, or input rule is placed here. |

## Data model

`CombatConfig` becomes the single Inspector location for combat-action values. It contains an explicit catalog of `CombatActionDefinition` entries keyed by `CombatActionId`.

Each definition contains:

- `id`: `BasicPunch` or `CrossPunch`.
- `cooldown`, `range`, `radius`, `knockbackForce`, and `hitStunTime`.
- `releaseBallOnHit` and `ballKnockbackForce`.
- `animationTrigger` and `animationSpeed`.

`CombatController` owns a cooldown timestamp per `CombatActionId`, looks up the requested definition, and keeps one shared hit-resolution path. It does not duplicate overlap, target selection, knockback, audio, camera shake, or ball-release code for Cross Punch.

`BasicPunch` and `CrossPunch` start with equivalent combat values but remain separate entries so later balance changes are Inspector-only.

## Input and request flow

`ContextualPlayerActionRouter` retains charge, cancellation, dodge, and one-touch lifecycle handling. The direct action branch delegates context interpretation to a pure resolver:

1. The reader emits `PrimaryAction` or `SecondaryAction`.
2. The resolver receives a possession snapshot from `PossessionInputContext` and returns a `GameplayActionRequest`.
3. No-ball primary resolves to `BasicPunch`; no-ball secondary resolves to `CrossPunch`.
4. Possession primary and secondary resolve to the existing pass-charge and shot-charge requests.
5. The dispatcher invokes the owning Combat or Ball entry point.

The snapshot exposes only facts needed for selection: possession context, actual ownership, mouse-action suppression, charge state, and action direction. It is read-only, so selecting Cross Punch cannot claim, release, or move the ball.

## Animation

The Animator gains a distinct `CrossPunch` trigger and state. `CharacterAnimator` receives a generic action-presentation call that sets the action-speed parameter before triggering the requested state. The existing `Punch` state consumes speed `1.0`; `CrossPunch` consumes `2.0`. The controller is changed only through Unity Editor or Unity MCP.

## Error and compatibility behavior

- Unknown or unavailable action IDs are rejected without consuming cooldown or changing possession.
- Stunned and dodging checks remain before combat execution.
- A blocked possession input cannot fall through to Cross Punch.
- Existing AI compatibility callers of `CombatController.Punch()` remain mapped to `BasicPunch` until intentionally migrated.
- Existing `PlayerBallHandler` and `BallController` public contracts remain unchanged.

## Verification

- Unit tests cover no-ball primary/secondary selection, possession pass/shot preservation, suppression behavior, and resolver non-mutation of the supplied possession snapshot.
- Combat tests prove independent punch cooldowns and equivalent initial tuning, including ball release only on a successful hit.
- Animator tests verify the two action states use distinct triggers and configured speed values.
- Unity EditMode suite and Console checks provide compile/regression evidence.
- Manual Play Mode verification remains required for no-ball left/right feel, Cross Punch 2x presentation, ball-release feel, and possession transition behavior.

## Scope exclusions

- Animation-event-synchronized hit timing.
- New key bindings beyond the already-bound primary and secondary mouse actions.
- Runtime rebinding UI or binding persistence changes.
- Changes to BallController ownership, Rigidbody behavior, or possession acquisition rules.
