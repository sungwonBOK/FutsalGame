# Direct P2P-first 1:1 Implementation Plan

**Goal:** Use the existing NGO/Unity Relay room only to exchange WebRTC setup messages, then allow a 1:1 match to start only when a direct P2P DataChannel is open.

**Constraints:** No custom signaling server, TURN server, automatic host-authoritative gameplay fallback, scene/prefab edits, or gameplay-rule migration in this slice. P2P failure shows a clear connection-failed state and does not start the match. Existing Relay host/join remains available as its unchanged legacy path.

## File responsibilities

- `ExperimentalNet/P2P/P2pSignalMessage.cs`: small serializable offer/answer/candidate envelope; no WebRTC lifecycle.
- `ExperimentalNet/P2P/P2pConnectionCoordinator.cs`: WebRTC lifecycle, STUN config, deterministic offerer selection, state callbacks; no gameplay packets.
- `ExperimentalNet/P2P/P2pSnapshotCodec.cs`: compact latest-only player snapshot encoding/decoding; no MonoBehaviour or WebRTC calls.
- `ExperimentalNet/P2P/RemoteSnapshotBuffer.cs`: sequence rejection and presentation interpolation only.
- `ExperimentalNet/LobbyController.cs`: relays setup envelopes through the existing NGO room and gates game start on the P2P-ready state.
- `ExperimentalNet/NetworkConnectionReporter.cs`: P2P connection/failure reason display.
- `Tests/EditMode/ExperimentalNet/*Tests.cs`: one focused fixture per pure protocol/buffer type.

## Tasks

### 1. P2P setup signaling

- [x] Write failing EditMode tests for invalid signal envelopes and deterministic offerer selection.
- [x] Add the focused P2P files and make the tests pass.
- [x] Add NGO named-message relay support in `LobbyController`; the host forwards only opaque setup messages and never inspects or decides gameplay.
- [x] Configure one STUN endpoint for the direct-only spike. Do not add TURN credentials or fallback code.
- [x] Add direct P2P ready/failed status to the lobby.

### 2. Direct P2P movement proof

- [x] Write failing codec, stale-sequence, presentation, and start-gate tests.
- [x] Implement snapshot codec and buffer, then run the focused tests.
- [x] Send local movement snapshots only after `P2pReady`; remote presentation consumes the buffered snapshots.
- [ ] Verify with two remote clients. If ICE cannot open a direct channel, show `P2pFailed`; do not call `BeginMatch`.

### 3. Controlled expansion

- [ ] Migrate one action/result family at a time only after movement works remotely.
- [ ] Keep combat and ball work behind issue-owner agreement (#4 and #3 respectively).
- [ ] Add TURN or a separate signaling service only when the direct-only failure rate makes it necessary.
