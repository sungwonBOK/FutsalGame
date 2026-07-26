# Possession Input Context Design

Date: 2026-07-26
Status: approved for implementation

## Goal

Keep player action input stable while the ball briefly leaves a sprinting owner's feet, and prevent held combat input from becoming ball actions when possession changes.

## Boundaries

`BallController` remains the sole authority for actual ownership. `BallPossessionController` remains the sole authority for the configured acquire-range calculation. `PlayerBallHandler` exposes that existing range result as a read-only facade API.

`PossessionInputContext` is a pure `Runtime/Input` state object. It holds only effective input context, sprint grace, and the transition from a recent no-ball combat input to actual ball ownership. It never changes ball ownership, combat state, animation, or physics.

`ContextualPlayerActionRouter` asks the context for the effective possession state when a button is pressed, latches the selected action until release or completion, and calls the existing ball/combat behavior. F stays a no-op while possession is effective; it remains the existing tackle action only while not possessed.

## Rules

1. Actual self ownership immediately gives possession input context.
2. During active sprint, losing self ownership to a free ball inside the configured acquire range retains possession input for 0.65 seconds.
3. Another owner or an out-of-range free ball immediately removes that grace.
4. Primary and secondary action choice is latched at press. Active ball charge release uses that latched ball action even if ownership changes.
5. A no-ball punch or tackle press starts a 0.40-second combat transition protection window. While still without the ball, further combat presses remain valid and restart that timer. Only actual ownership gained inside that window blocks new pass/shot mouse actions until the window ends.
6. No-ball secondary remains a no-op. Possession F remains a no-op. No guard/defense system is introduced.

## Scope

```text
Assets/_Game/Scripts/Runtime/Input/PossessionInputContext.cs
Assets/_Game/Scripts/Runtime/Input/ContextualPlayerActionRouter.cs
Assets/_Game/Scripts/Runtime/Ball/BallPossessionController.cs
Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs
Assets/_Game/Scripts/Tests/EditMode/PossessionInputContextTests.cs
IMPLEMENTATION_STATUS.md
```

No input-action asset, scene, prefab, project setting, camera, or combat-controller change is required.

## Verification

Use small EditMode tests for the pure context: sprint grace, opponent/range termination, press latching, and protected held input suppression. Verify Unity compilation and inspect the console. Play Mode follow-up checks sprint dribble detachment/reacquisition, mouse hold/release behavior, rapid combat input followed by possession, and F's no-op possession behavior.
