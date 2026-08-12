# Standard MPS Player-Count and Direct-P2P Audit Design

## Goal

Use the existing public MPS room flow for one through six player tests, retain all four WebRTC DataChannels, and make direct-P2P setup failures transition into the existing reconnect path.

## MPS Room Flow

- Remove the shared player-count test-room buttons, property marker, discovery methods, and room-service contract members.
- Keep the existing public MPS create, refresh, and join controls as the only MPS room flow. Session capacity remains six.
- A public MPS room with one connected player may start without a remote WebRTC peer or Game ready acknowledgement. This exception applies only to MPS sessions.
- A public MPS room with two through six connected players must retain the current full direct-P2P mesh and every participant's Game ready acknowledgement before the Host can start.
- Legacy Relay join-code and LAN flows retain their current direct-P2P behavior.

## Four-Channel Direct-P2P Contract

- Retain one `RTCPeerConnection` for each remote participant and four DataChannels on that connection: unordered/unreliable snapshot, ordered/reliable combat, unordered/unreliable ball state, and ordered/reliable ball events.
- The four channels do not create four IP addresses, ports, ICE negotiations, or UDP hole-punch attempts. ICE candidate exchange and the selected UDP path belong to the one peer connection.
- NGO/MPS Relay remains an addressed signaling control plane for Ready, SDP offer/answer, and ICE candidates; gameplay packets stay on WebRTC DataChannels.
- Candidate arrivals before a remote description remain queued and applied after the description is set. A Ready signal arriving before a local coordinator is retained until that coordinator exists.

## Failure Handling

- If a DataChannel closes while direct P2P is negotiating or ready, transition the coordinator to `Failed` so `LobbyController` starts its existing reconnect coroutine.
- Treat ICE or peer-connection `Closed`, like `Failed`, as a terminal setup failure while the session is active.
- Do not claim general NAT traversal success from static or EditMode evidence. The current setup is STUN-only and requires a two-Editor/PC runtime check on the target networks.

## Verification

- EditMode policy tests prove MPS one-player bypass, MPS two-to-six P2P/Game-ready requirement, removal of the dedicated test-room contract, and terminal P2P failure classification.
- Unity compiles with no new Console errors; full EditMode suite must pass.
- Manual runtime gates: MPS host-alone start; two Editor P2P candidate/ICE/DataChannel/ready/start chain; and six-player capacity with a seventh join refusal.

## Implementation Verification

- Unity compiled with zero Console errors after the change.
- Focused MPS/signaling/mesh/failure EditMode coverage passed `24/24`; the full EditMode suite passed `185/185`.
- These checks do not replace the target-network runtime gates because the current ICE configuration is STUN-only.
