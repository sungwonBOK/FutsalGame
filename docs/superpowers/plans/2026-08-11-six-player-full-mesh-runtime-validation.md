# Six-player Full-mesh P2P Runtime Validation

**Build:** `Builds/p2p-runtime-validation-6mesh/FutsalGame.exe`

This is a manual acceptance gate for the MPS control plane and direct WebRTC gameplay mesh. Passing EditMode tests does not replace any item below.

## Prerequisites

1. In Unity Dashboard/Editor Services, confirm the project is linked and MPS, Relay, and Authentication are enabled for the project ID used by this checkout. Rebuild if the Unity Services project-link warning remains.
2. Run six clients with distinct Unity Authentication persistent identities. Use separate devices or isolated user-data profiles; six launches sharing one local profile are not six MPS participants.
3. Use a network that permits direct WebRTC/STUN connectivity for every participant. Keep the MPS/NGO control connection alive throughout the match.

## Acceptance sequence

1. Client A creates a public MPS room. Clients B-F refresh the room browser, find that room, and join it.
   - Pass: all six clients see the same roster and team assignment.
2. On B-F, press `P2P 준비`. Do not press it on only a subset.
   - Pass: the Host cannot start until every non-Host is explicitly ready and every client reports its complete mesh.
3. Confirm the direct links become ready.
   - Pass: each client has five ready remote peers; together that is 15 peer pairs. A Host-only room is still allowed to start without a peer link.
4. Start the match from the Host.
   - Pass: every client transitions into the same match. Score, timer, match end, roster, and teams remain synchronized through NGO/MPS.
5. Exercise high-frequency gameplay from multiple non-Host clients.
   - Pass: movement snapshots, combat results/presentation, ball states/events, and ball authority transfers appear on every peer. No Host-only gameplay forwarding is required for those updates.
6. While the MPS/NGO control connection remains alive, interrupt one direct P2P peer link and observe the remaining peers.
   - Pass: the affected player remains in the match, freezes at its last received pose, and a held ball becomes unowned. Match score/timer/end control continues.
7. Restore the direct link and wait until the returning participant has rebuilt every required peer link and receives Host recovery approval.
   - Pass: the player resumes at the preserved pose with zero linear/angular velocity and without ball possession.

## Record for Issue #10

For each run, record the client count, MPS room creation/browse/join result, per-client ready/mesh status, start result, the direct-link failure/recovery outcome, and any Console or player log errors. A failure in one item does not establish a six-player runtime success.
