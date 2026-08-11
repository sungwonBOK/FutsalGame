# Six-player Full-mesh P2P Gameplay Design

## Goal

Keep Unity Multiplayer Services (MPS) and Netcode for GameObjects (NGO) as the public-room and authoritative control plane while moving high-frequency gameplay for up to six human players onto a direct WebRTC full mesh.

## Approved boundaries

- MPS/NGO owns public-room discovery, join/leave membership, roster/team assignment, explicit ready state, match start/end, score, timer, and reconnection approval.
- A gameplay mesh has one direct WebRTC connection for every unordered pair of active human participants: at six players, 15 connections and five remote peers per participant.
- Host control traffic routes peer-addressed offer, answer, and ICE messages but never relays movement, combat, ball, or presentation packets.
- Gameplay replicators depend only on a peer-connection registry. They do not import MPS or Relay APIs.
- The existing three-versus-three lobby defaults remain unchanged. A Host alone may start; otherwise every non-Host must be explicitly ready and all required peer channels must be open.

## Components

`IRoomService` remains the room-facing abstraction. The existing MPS adapter implements it now; a future Steam Lobby adapter can replace it without gameplay changes.

`IPeerSignalingTransport` carries `P2pPeerSignal` values addressed by sender and recipient client IDs. The NGO/MPS implementation forwards each message through the Host control plane. Its public contract has no Relay-specific types.

`P2pPeerConnection` owns one RTCPeerConnection and its gameplay channels for one remote client. `P2pPeerConnectionRegistry` owns the local collection keyed by remote client ID and exposes readiness, targeted send, broadcast, lifecycle, and packet events.

Movement, combat, ball authority, and presentation consume the registry's named packet APIs. Snapshot state uses lossy/unordered channels; combat and ball events remain reliable. The registry must not invent a Host gameplay fallback if a peer send fails.

## Lifecycle and failure policy

1. Control-plane membership changes update the active-human peer set.
2. Every local participant creates or tears down only the links it owns; deterministic client-ID ordering selects the offerer for each pair.
3. The Host permits match start only after non-Host ready acknowledgements and mesh readiness for every active participant.
4. A gameplay peer disconnect freezes that remote player at their most recently received pose. The control-plane member remains in the room and the match continues.
5. If the disconnected player owns the ball, its authority is immediately released to unowned state.
6. Reconnect requires all required direct links. On approval, restore the frozen pose, zero linear and angular velocity, clear possession, then resume the player.

## Explicit exclusions

- No Scene, Prefab, Input asset, ProjectSettings, or package YAML edits are part of this transition.
- No competitive anti-cheat or Host gameplay-forwarding fallback is introduced.
- Existing one-peer/Host-forwarded paths are removed only after their corresponding multi-peer paths are implemented and covered by focused tests.

## Verification

Focused EditMode tests cover registry membership/readiness, addressed signaling routing, start policy, peer broadcast/target selection, and disconnect/reconnect policy. Available full EditMode coverage and a diff review are required before handoff.

Runtime proof remains separate: staged two-, three-, and six-client tests must demonstrate complete mesh readiness, start gating, Host score/time/end control, direct gameplay packet flow, frozen disconnected players, ball release, and full reconnect restoration.
