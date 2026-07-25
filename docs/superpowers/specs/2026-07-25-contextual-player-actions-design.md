# Contextual Player Actions Design

Date: 2026-07-25

## Goal

Route one physical input through exactly one gameplay action according to the player's possession state, while allowing Alt one-touch intents to be attempted and retained whether or not the player owns the ball.

## Input Boundary

`InputSystem_Actions.inputactions` remains the authoritative default-binding asset. The `Player` map exposes physical, context-neutral actions:

- `PrimaryAction`: LMB
- `SecondaryAction`: RMB
- `QueueOneTouchPass`: Left Alt+LMB and Right Alt+LMB
- `QueueOneTouchShot`: Left Alt+RMB and Right Alt+RMB
- `ContextQ`: Q
- `Grab`: E
- `ContextF`: F
- `Dodge`: Space
- `Sprint`: Left Shift and Right Shift
- `CancelAction`: C

The former LMB/RMB and E/Space duplicate bindings (`Attack`, `Pass`, `Shot`, `Interact`, `Jump`) are removed from the active gameplay input boundary. K and L are not retained as aliases. Mouse look remains raw pointer-delta input and is out of scope.

`GameplayInputReader` translates only these actions to button states. It never reads ball possession, combat state, or gameplay timing.

## Router and Context Rules

`PlayerActionRouter` owns action precedence and reads a value-only context from the existing player components: `HasBall`, `IsCharging`, stun, dodge, and action direction. It performs no ball physics, combat hit detection, or movement calculations.

Processing order per frame is:

1. `CancelAction`: cancel a current ball charge and clear the one-touch intent, then stop processing this frame.
2. `QueueOneTouchPass` or `QueueOneTouchShot`: queue/replace the one-touch intent, attempt it immediately, then prevent the same LMB/RMB from becoming a primary or secondary action.
3. If a ball charge is active, process only release of its matching primary/secondary action.
4. Otherwise route context-sensitive actions once:
   - `PrimaryAction`: pass charge with ball; existing quick punch without ball.
   - `SecondaryAction`: shot charge with ball; reserve the strong-attack call without implementing combat behavior.
   - `ContextQ`: reserve protect-ball/guard/parry calls without implementing behavior.
   - `Grab`: reserve grab call without implementing behavior.
   - `ContextF`: reserve through/special-pass or tackle calls without implementing new behavior.
   - `Dodge`: existing dodge regardless of possession.
   - `Sprint`: existing sprint and dribble-touch behavior regardless of possession.

The router replaces the current direct mixing of input, ball charge, and combat key semantics in `PlayerInput`. `PlayerInput` continues to provide camera-relative movement and action direction.

## One-Touch Intent

`Ball/OneTouchIntentBuffer` is a per-player gameplay state holder, separate from `BallInteractionController` because the intent is valid without possession.

It stores `None`, `Pass`, or `Shot`; the latest Alt input replaces the prior intent. It persists indefinitely until one of these events: `C` clears it, a matching one-touch execution succeeds and consumes it, or match/player reset explicitly clears it.

`OneTouchActionExecutor` attempts the queued intent immediately every time it is created or when possession becomes available:

- With ball: execute the matching immediate pass/shot and consume the intent.
- Without ball: play the corresponding whiff presentation without ball physics and retain the intent.

The buffer exposes `IsPreparing`; future locomotion or animation rules may consume that state to reduce movement speed or apply preparation constraints. This change does not add those modifiers.

## Alt Combination Safety

Each Alt action uses a `OneModifier` composite. The modifier binding is either left Alt or right Alt and the primary binding is LMB or RMB. Because the composite binding is more specific than a simple mouse-button binding in the same map, the Input System processes it first. The router also explicitly gives queued one-touch actions precedence, so a composite cannot produce an ordinary pass, shot, or attack in the same frame.

## Deferred Gameplay

No new strong attack, protect-ball, guard/parry, grab, through pass, special pass, speed penalty, or new animation asset is added in this slice. Their action names and router branch points are established without inventing temporary gameplay effects.

The no-ball one-touch attempt uses the existing available shot/punch presentation path only if it can be invoked without ball physics; otherwise it records the intent without a presentation. It must not create, move, or release a ball when `HasBall` is false.

## Verification

EditMode coverage proves:

1. The action asset has only the intended active keyboard/mouse bindings and both Alt composites.
2. Alt+LMB/RMB signal their one-touch actions without also signalling primary/secondary actions.
3. The buffer keeps the most recent intent, clears on cancel, and reports preparation state.
4. With-ball one-touch consumes after execution; no-ball one-touch retains the intent and performs no ball physics.
5. Existing pass/shot charge, quick punch, dodge, sprint, and camera-relative movement behavior remains covered.

Manual Play Mode verification confirms the selected whiff presentation and the visible feel of queued one-touch intent.
