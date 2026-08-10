# P2P Session Continuity Design

**Date:** 2026-08-04  
**Status:** Approved for the 1:1 foundation implementation

## Goal

Make direct-P2P match readiness and interruption states understandable without silently switching gameplay traffic back to Relay/NGO. Preserve the current 1:1 host-signaling topology for the first manual validation, while leaving explicit boundaries for a later 4:4/5:5 direct-P2P mesh.

## Current 1:1 UX

| State | Player-facing message | Behavior |
|---|---|---|
| Preparing | `상대와 직접 대전을 준비 중입니다` | Team selection remains available; match start waits. |
| Ready | `직접 대전 준비 완료` | Movement and combat channels are open. The future ball channel becomes another required channel when it is migrated. |
| Reconnecting | `상대 연결을 다시 확인 중입니다` | The remaining player continues local free play; no gameplay packet is rerouted through NGO. |
| Peer absent | `연결 끊김` | The remote player is intended to remain visually frozen and non-interactive. Ball possession releases immediately. |
| Rejoined | `상대가 돌아왔습니다 · 동기화 중` | Both sides wait for a three-second resynchronization countdown before direct play resumes. |
| Host absent | `방장이 나가 재연결할 수 없습니다` | The guest may continue free play or leave. Current host-based signaling cannot restore that room. |

## Scope of This Slice

- Add a composable readiness value for direct gameplay channels. The current required set is movement snapshots plus combat; ball is added only with its P2P migration.
- Gate 1:1 match start on the required direct channels, not merely the movement snapshot channel.
- Add a pure retry schedule and user-readable direct-P2P status mapping so reconnect behavior can be tested without WebRTC.
- Keep current NGO/Relay only for room setup and WebRTC signaling. This slice does not migrate ball state or rewrite existing gameplay authority.

## Deferred Work

- Runtime ghost persistence after a full NGO disconnect. Current Netcode despawns the remote player, so this needs a separately designed detached visual replica.
- Full Relay-room rejoin after a guest loses its NGO connection, and any recovery after the host exits.
- Direct-P2P ball state, match state, and 4:4/5:5 peer mesh.
- The external membership/signaling service required before a 4:4/5:5 match can survive the departure of the room creator.

## Extension Boundary

`P2pGameplayReadiness` owns only which direct gameplay channels are required. Each future subsystem (ball, match state, multi-peer session) reports its own channel availability; the lobby does not infer it from WebRTC internals.

## Verification

- EditMode tests cover channel readiness, start gating, retry timing, and status text selection.
- Unity Host+Guest Play Mode remains mandatory after the ball P2P slice: capture `DataChannel Open -> IsReady -> SvStartMatch -> matchStarted`, then exercise combat and disconnect/reconnect states.
