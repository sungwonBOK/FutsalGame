# MPS Relay Room Discovery Design

**Date:** 2026-08-10
**Status:** User approved
**Scope:** First serverless public-room discovery and Relay host-connection slice for FutsalGame.

## Goal

Add Unity Multiplayer Services (MPS) Sessions to the Online screen so players can create, browse, and join compatible public rooms. A player-hosted NGO match remains the game authority; Unity Relay carries traffic and no dedicated game server is introduced. The existing manual Relay join-code path remains available until the MPS two-client acceptance gate passes.

## Locked decisions

- Product target is 3v3 (six human players). The first runtime acceptance gate remains a real 1v1 internet session; 3v3 is enabled only after that gate passes.
- Use `com.unity.services.multiplayer`, Unity Authentication, Unity Relay, and existing Netcode for GameObjects.
- `SessionOptions.WithRelayNetwork` starts the Relay-backed NGO connection as part of Session create/join. The existing lobby remains the pre-match team-selection surface.
- Public discovery exposes only validated room name, occupied/max slots, and build compatibility key. Private sessions, Session-code entry, region/map labels, and ready-state networking are deferred.
- The existing direct WebRTC P2P path remains an ExperimentalNet experiment and is not the default MPS gameplay path.
- Host departure ends the match in this slice. Host migration, quick-match, reconnect, 3v3 performance tuning, and 5v5 are deferred.

## Player flow

1. The player opens Online and is anonymously authenticated with the existing UGS initialization.
2. They create a public Session or browse compatible public Sessions.
3. MPS configures Relay and starts the existing NGO host/client connection while creating or joining the Session.
4. The existing team-slot UI continues to represent match membership.
5. The host starts the existing host-authoritative match after team selection.

## Boundaries

- `MpsRoomDefinition` is a pure, testable value that validates and normalizes room data at the client/service boundary.
- `MpsSessionRoomService` wraps the MPS SDK. It owns Session create, query, and join-by-id with Relay network options. It exposes application-owned room data rather than leaking MPS types to UI.
- `LobbyController` owns only UI and existing team/match orchestration. It consumes the service and never constructs a Relay allocation directly.
- `RelayConnectionService` remains the shared UGS initialization/error boundary and supports the retained legacy join-code path. MPS and legacy Relay are alternative entry paths, not simultaneous transports for one match.

## Failure rules

- Blank or overlong room data is rejected before calling Unity services.
- Query and join errors remain user-visible through the existing status message; raw exception details remain in the Unity Console only.
- A stale, full, or incompatible room cannot be joined and never starts NGO.
- An MPS failure must not fall back to direct WebRTC P2P or raw IP networking.

## Verification

- EditMode tests first prove name normalization, capacity limits, public compatibility filtering, and the no-gameplay-before-start policy.
- Unity package resolution and script compilation must be clean before scene use.
- Required manual gate: two separate builds/profiles complete public create -> browse -> join -> team selection -> host start -> Relay NGO match. Capture Relay connection status and test host/client departure.
- 3v3 is not claimed until a six-client host CPU/upload/RTT/jitter/loss matrix is recorded.
