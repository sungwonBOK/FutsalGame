# Defender-Resolved P2P Combat Design

**Date:** 2026-08-01
**Status:** Approved direction; implementation plan pending user review
**Scope:** Direct P2P 1v1 combat after the WebRTC DataChannel is ready.

## Goal

Keep local input and presentation responsive while making defense and evade results feel fair to the defending player. Direct P2P combat supplements the existing direct P2P movement path; when direct P2P is unavailable, the existing NGO/host combat path remains the fallback.

This design covers basic punch, cross punch, tackle, and grab. It does not add anti-cheat, full rollback netcode, multi-player target selection, or the future grab-escape skill.

## Player-facing timing

| Action | Input-to-interaction time | Intended response model |
|---|---:|---|
| Tackle (F) | 0.10 s | Predictive defense; players should already be blocking or dodging. |
| Grab (E) | 0.10 s | Predictive defense; players should already be blocking or dodging. |
| Basic punch | 0.30 s | Readable reaction window. |
| Cross punch | 0.60 s | Clearly readable reaction window. |

The 0.10-second actions are deliberately not guaranteed to be reactable after an opponent sees the remote attack. Network latency cannot make information arrive before the attack starts.

## Authority model

Combat uses a deliberately mixed authority model.

| Responsibility | Owner |
|---|---|
| Create an attack action, direction, timing, and local attack presentation | Attacker |
| Detect that the remote player entered the action's interaction-request volume | Attacker |
| Decide whether the defender was hit, blocked, or evaded | Defender |
| Apply the chosen outcome locally before notifying the attacker | Defender |
| Confirm the same outcome and present it on the attacker | Attacker after result receipt |

The attacker may request an interaction, but may never unilaterally turn that request into damage, hit stun, a successful grab, or an evade failure. The defender's result for an `actionId` is immutable.

## Interaction-request volume

An interaction-request volume is a small, action-specific tolerance beyond the visually expected interaction range. It is not a large latency-compensation radius, and it is not a player-visible warning zone.

The request volume is a real game-rule boundary: once the attacker sends an interaction request because the opponent entered it, the defender resolves that action immediately. Therefore it must remain close enough to the visual attack path that a player accepts the interaction as contact.

Initial tuning targets are forward-only extensions; lateral radius must not be enlarged by default.

| Action | Initial forward extension |
|---|---:|
| Basic punch | 0.08 m |
| Cross punch | 0.10 m |
| Tackle | 0.12 m |
| Grab | 0.05 m |

These are first play-test values, not RTT-derived values. In particular, the tackle moves too fast for a small distance extension to hide a large network delay; early action messaging and defender-side result resolution handle network timing instead. Grab is deliberately strict because success produces a 1.5-second restriction.

Each action may issue at most one interaction request to the opposing player. Repeated overlap checks must not create duplicate requests.

## Direct P2P event transport

The existing `futsal-snapshots` DataChannel remains unordered and no-retransmit for movement snapshots. Combat must use a separate reliable, ordered DataChannel so that a result cannot be lost or overtake its prerequisite action.

The direct combat messages are:

| Message | Sender | Purpose |
|---|---|---|
| `CombatActionStart` | Attacker | Announces `actionId`, action kind, start time, interaction time, origin, and direction immediately on input. Enables remote anticipation and presentation. |
| `CombatInteractionRequest` | Attacker | One request when the defender enters the action's interaction-request volume. Carries `actionId` and contact context. |
| `CombatInteractionResult` | Defender | Immutable `Hit`, `Block`, or `Evade` result with `actionId` and resolved time. |
| `GrabStarted` | Defender | Confirms a successful grab and identifies its `grabId` (the initiating `actionId`). |
| `GrabReleased` | Grab holder | Releases the specified active grab before expiry. |

All messages include an action/session identifier and sequence information sufficient to deduplicate retransmissions and ignore stale messages. A peer retains recently resolved identifiers for at least the maximum action lifetime plus a safety window.

## Resolution flow

1. The attacker assigns a monotonic `actionId`, starts its own animation immediately, and sends `CombatActionStart` reliably.
2. During the action's configured interaction moment or active path, the attacker detects the defender in the action-specific request volume and sends one `CombatInteractionRequest`.
3. On the defender, receipt of a new request performs one atomic local resolution:
   - inspect the defender's active block/dodge state and the request context;
   - choose `Hit`, `Block`, or `Evade`;
   - record the resolution against the `actionId`;
   - immediately apply the corresponding local state and animation;
   - send the result reliably.
4. The attacker applies damage, knockback, hit stun, block response, or evade outcome only after receiving the corresponding result.
5. Duplicate requests return the already recorded result without replaying effects. Unknown, expired, or out-of-order results do not alter current state.

The defender does not receive a universal input lock. The atomic result itself determines what becomes unavailable:

| Result | Immediate defender state |
|---|---|
| `Hit` | Enter hit stun; disable combat abilities incompatible with hit stun. |
| `Block` | Consume the applicable defense window and apply normal block recovery. |
| `Evade` | Commit the evade, including its ordinary invulnerability, recovery, and cooldown. |

Subsequent input affects only later actions and follows normal cooldown and recovery rules. It cannot revise an already resolved `actionId`.

## Grab lifecycle

`GrabStarted` is sent only after the defender resolves the initial grab as `Hit`.

1. Both peers enter the same grab session using `grabId`.
2. The session lasts 1.5 seconds from the resolved start time unless the holder releases first.
3. The grabbed player cannot use evade. Movement input may continue to drive a run-in-place presentation but must not move the grabbed player out of the session.
4. The holder sends `GrabReleased(grabId)` immediately on release. Both peers clear the grab session once; duplicate releases are ignored.
5. At the shared 1.5-second expiry, both peers clear the session even if the release message is delayed or lost.

The future escape skill is a separate action and is out of scope. It must be designed as an explicit transition from an active `grabId`, not as an exception that silently overwrites grab state.

## UX rules

- The defender always sees its resolved outcome immediately because it is the result owner.
- The attacker may show its attack animation and a light speculative contact effect immediately, but cannot show confirmed health loss, knockback, hit stun, or grab attachment until the defender result arrives.
- Late results are attached to their own `actionId`; they may complete their own presentation but cannot cancel or overwrite a newer action.
- Fast tackle and grab are communicated immediately with `CombatActionStart`, but their gameplay identity remains predictive rather than guaranteed reaction-defense moves.
- The interaction-request volume is never displayed as a hit warning and creates no effect if no request is issued.

## Failure handling and fallback

- Direct P2P combat is enabled only while the reliable combat channel is open.
- If it is unavailable before an action begins, use the existing NGO/host combat path for that action.
- If direct P2P closes during an unresolved action, cancel that direct action without damage, knockback, hit stun, or a grab transition. Do not reroute the same action through NGO because it could resolve twice. New actions use NGO/host combat until a direct channel is ready again.
- The direct P2P path must not silently claim that it provides TURN relay or anti-cheat. Those remain separate transport and authority concerns.

## Verification

Automated coverage must include:

- combat event codec round trips and malformed payload rejection;
- reliable ordered action/result routing separate from snapshot routing;
- one request per `actionId` and idempotent duplicate handling;
- defender result immutability and stale/out-of-order result rejection;
- action-specific request-volume boundary tests;
- hit, block, evade, grab start, early release, expiry, and repeated-release state tests;
- direct-channel-unavailable fallback selection.

Manual two-client validation must include:

- successful direct P2P on separate networks;
- both players taking turns attacking and defending under normal latency;
- 0.10-second tackle/grab accepted as prediction-based rather than reaction-based moves;
- high-latency and packet-loss simulation, with no duplicate damage, stuck grab, or result that overwrites a newer action;
- direct-channel failure during play, confirming that no unsupported P2P continuation is claimed.

## Out of scope

- Server-authoritative anti-cheat or rollback netcode.
- Automatic TURN relay configuration and Relay gameplay-data fallback implementation.
- More than one remote opponent.
- Grab escape mechanics.
- Movement interpolation changes.
