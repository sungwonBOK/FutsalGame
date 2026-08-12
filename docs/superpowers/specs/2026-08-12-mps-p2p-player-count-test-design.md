# MPS P2P Player-Count Test Design

> Superseded on 2026-08-12 by `2026-08-12-standard-mps-player-count-and-p2p-audit-design.md`; the dedicated shared test-room approach was removed in favor of the normal MPS public-room flow.

## Goal

Provide one shared Editor test room for one to six players without room-name entry, room-list refresh, or join-code sharing. A lone host can start without creating a WebRTC peer; two to six connected players require the existing direct-P2P mesh and every player's Game ready acknowledgement.

## Scope

- Add a test-only MPS room creation and discovery path behind the existing `IRoomService` boundary.
- Keep the existing public MPS room browser and legacy Relay join-code path unchanged.
- Add two explicit UI actions: host creates the shared test room, and every other editor joins the currently available test room.
- Fix the match-start gate so one connected player does not wait for P2P readiness.
- Do not modify packages, project settings, scenes, prefabs, WebRTC transport settings, or gameplay replication.

## Test Room Flow

1. The host selects `Create 1-6 player test room`. The service creates a public, six-player MPS Relay session with the current application-version property and an indexed test-room property. The normal public-room browser excludes that marker.
2. Each guest selects `Join 1-6 player test room`. The service searches only compatible, non-full test rooms and joins the most recently updated one.
3. The first host can start alone: no remote peer is required, P2P signaling starts but the mesh has zero required peers, and start policy permits the one-player case without a Game ready acknowledgement. This exception is scoped to the shared player-count test room.
4. With two to six connected players, every player must report a complete direct-P2P mesh and toggle Game ready before the host can start.
5. A seventh player cannot join because the MPS session capacity is six.

## Boundaries and Failure Handling

- The UI reports that no test room is available when a guest starts before a host. It never silently creates a second room.
- The test-room query also filters by the current build key, so editors with different `Application.version` values do not mix.
- The existing MPS session lifetime remains authoritative. This slice does not add host migration or stale-room cleanup; a host must leave/close its previous test room before running another isolated group.

## Verification

- EditMode policy tests prove one player bypasses P2P and Game ready, while two through six require both gates and seven is rejected.
- EditMode room-definition tests prove the test-room capacity and filter contract.
- Manual Editor acceptance is required: host-alone start, two-player P2P readiness/start, and six-player join-limit behavior. The manual run must record P2P diagnostic state separately from Game ready state.
