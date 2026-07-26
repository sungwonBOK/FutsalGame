# Ball Action Aim Direction Design

Date: 2026-07-26
Status: approved for planning

## Goal

Use the screen-centre camera heading only for player pass and shot actions. Keep every non-ball action on its existing character or movement direction.

## Current Boundary

`PlayerInput` computes `locomotion.ActionDirection` and supplies that one value to `ContextualPlayerActionRouter`. The router currently uses it for combat, dodge, sprint-related actions, direct charged ball releases, and one-touch execution.

`PlayerBallHandler` and `BallInteractionController` already accept a supplied direction and do not need to know about the camera.

## Direction Policy

| Action | Direction | Lock time |
| --- | --- | --- |
| Primary/Secondary release while holding the ball | Planar camera forward | Matching mouse button release |
| Immediate or queued one-touch pass/shot | Planar camera forward | Execution frame |
| No-ball primary punch | Existing character/movement direction | Button press |
| Any existing no-ball secondary combat behavior | Existing character/movement direction | Button press |
| `F` tackle, dodge, sprint dribble, and other keyboard combat | Existing character/movement direction | Existing behavior |

This slice does not introduce or alter a no-ball secondary attack. It only preserves its character-direction policy if that behavior is added or exists in another gameplay branch.

## Design

### PlayerInput

`PlayerInput.Update` calculates two values after movement is resolved:

```csharp
Vector3 characterActionDirection = locomotion.ActionDirection;
Vector3 ballAimDirection = BuildPlanarCameraForward(movementReference, transform.forward);
```

It keeps using `characterActionDirection` for sprint dribble input. It passes both directions to the contextual router. `BuildPlanarCameraForward` is the existing, test-covered helper: it flattens the active camera forward vector and falls back to player forward if the reference is unavailable.

No screen raycast, target selection, auto-pass, aim UI, new scene reference, or camera-system change is introduced. On the flat current pitch, planar camera forward is the intended screen-centre heading and is the smallest stable solution.

### ContextualPlayerActionRouter

Change the router entry point to accept both semantic directions:

```csharp
Process(GameplayInputReader inputReader,
        Vector3 characterActionDirection,
        Vector3 ballAimDirection)
```

Use `characterActionDirection` for `Dodge`, `ContextF` tackle, and no-ball combat calls. Use `ballAimDirection` for:

1. releasing a charged pass or shot;
2. an immediate Alt one-touch pass or shot; and
3. executing a queued one-touch intent after possession is gained.

The one-touch buffer continues to store only the intent. It deliberately resolves aim at execution time, matching the existing release-time charge behavior and letting the player turn the camera while waiting for the ball.

### Ball and Combat Layers

`PlayerBallHandler`, `BallInteractionController`, `CombatController`, movement code, the Input Action asset, and camera code retain their responsibilities and public behavior. The router selects the direction; the ball layer launches with a supplied vector; combat receives only character-direction vectors.

## File Scope

```text
Assets/_Game/Scripts/Runtime/Input/PlayerInput.cs
Assets/_Game/Scripts/Runtime/Input/ContextualPlayerActionRouter.cs
Assets/_Game/Scripts/Tests/EditMode/ContextualPlayerActionRouterAimTests.cs (new)
```

Do not modify `Ball/`, `Combat/`, `Camera/`, input-action assets, Scene/Prefab assets, or ProjectSettings. Preserve the pre-existing uncommitted `ContextF` tackle changes in the router and its existing test file.

## Verification

EditMode tests prove:

1. a ball-charge release receives planar camera direction when player forward differs;
2. an immediate and a queued one-touch pass/shot use the execution-frame camera direction; and
3. no-ball punch, `F` tackle, and dodge keep the supplied character direction.

Run the focused tests, refresh and compile Unity, run the relevant EditMode suite, and inspect the Console. Manual Play Mode confirmation remains required for camera-facing pass/shot feel, no-ball combat direction, and one-touch timing.

## Coordination

This is an input-to-ball routing change within GitHub issue #3. It does not modify active camera-system issue #2 files. A start note was recorded on issue #3 before implementation planning.
