# Futsal Brawl 구현 상태

업데이트: 2026-07-18
기준 브랜치: `develop_merge`

현재 체크아웃 기준의 구현 상태만 기록한다. 세부 설계나 작업 이력은 이 문서에 길게 누적하지 않는다.

## 2026-08-11 Six-player MPS control plane and P2P gameplay mesh

- MPS/NGO remains the control plane for room membership, teams, match start/end, score, timer, and reconnect approval. MPS sessions now require the direct gameplay mesh instead of disabling it.
- `IRoomService` keeps the MPS public-room adapter behind a room-control contract, while `IPeerSignalingTransport` keeps the NGO addressed signaling relay behind a setup-only transport contract. Neither boundary exposes gameplay packets, so a Steam Lobby or Steam signaling implementation can replace it without changing P2P gameplay consumers.
- `P2pPeerConnectionRegistry` owns one WebRTC coordinator for every remote client ID. `P2pLobbySignalRelay` routes addressed offer/answer/ICE fragments through the Host control plane only; movement, combat, ball state/events, and presentation use registry broadcast or target APIs and do not use Host gameplay forwarding.
- The Host tracks all active participant mesh-ready reports. A Host-alone match may start; otherwise every participant must report a complete gameplay mesh; joining automatically begins setup signaling without a manual P2P-ready acknowledgement. The MPS room limit remains six, preserving the existing 3v3 lobby defaults.
- A failed direct peer link freezes that remote player at its most recently received pose. If the peer owned the ball, surviving peers clear its ownership locally. Resume requires rebuilt local mesh readiness plus an NGO Host-approved recovery ID; the restored pose is applied with zero linear and angular velocity and no ball possession.
- Verification: Unity imported the new scripts with no new Console errors. Focused control-plane/mesh/recovery contracts passed `18/18`, and the full EditMode suite passed `168/168`; the only compile warning remains the pre-existing `FindObjectsOfType` obsolete-API warning in `CombatController`. A single Editor Play Mode attempt stalled during Unity's post-test synchronous recompile/domain-reload transition without a gameplay exception, so it was stopped and is not runtime proof. Staged 2/3/6-client mesh, disconnect/reconnect, and score/time/end control-plane Play Mode proof are still required.
- A Windows development build is available at `Builds/p2p-runtime-validation-6mesh/FutsalGame.exe` (Unity build errors `0`). The build reports that Unity Services needs a linked cloud project; although `cloudProjectId` is populated, `cloudEnabled` is currently `0`, so a live MPS public-room validation requires the project owner to confirm the Editor/Dashboard Services linkage first.

## 2026-08-11 P2P room auto-connect and game-ready gate

- An addressed `Ready` signal that reaches a participant before its peer registry has received the participant list is retained only until that peer coordinator is created. Offer/answer/ICE messages are still rejected unless their coordinator already exists.
- Joining an MPS Relay room continues to start direct P2P negotiation automatically. Once the direct gameplay mesh is ready, each participant separately toggles `Game ready`; the Host can start only when every participant is game-ready. Any host team-slot edit clears those acknowledgements.
- Verification: Unity 6000.5.3f1 produced `Builds/p2p-auto-connect-20260811/FutsalGame.exe` successfully (return code `0`). The batch Test Runner exited `0` but did not create its requested result XML, so it is not treated as EditMode pass evidence. An actual Host+Guest room join must still confirm the participant no longer reports an unexpected peer signal and that start remains blocked until both players toggle `Game ready`.

## 2026-08-11 R power primary effects with direct P2P

- Armed R + LMB now performs a powered primary action. With real ball ownership it releases the normal minimum pass force plus the same upward force, so the Rigidbody follows a visible lob trajectory. Without the ball it selects the closest forward target in the normal basic-punch range and applies a zero-knockback 0.7-second stun; an unavailable or invulnerable target leaves the armed gauge intact.
- Direct P2P reuses the existing ball authority event/state flow with `LobPass`, including the released Rigidbody vertical velocity, and the existing defender-resolved combat request/result flow with `PowerStun`. `PowerStun` has no new animation or hit presentation; the target's existing stunned input and movement gates apply for the duration.
- `NetPlayer` now uses the existing `PowerGauge` with `DefaultPowerGaugeConfig`; `PlayerInput` therefore creates the existing `PowerActivationController` for the locally owned network player. No second gauge, transport, animation, VFX, or Input System asset change was added.
- Verified through Unity MCP: focused contracts/effects and the full EditMode suite `158/158` passed. Manual gate remains: two clients with the direct P2P channel ready must verify R+LMB lob velocity/trajectory and remote 0.7-second input suppression, including no-target, dodge/invulnerability, and re-arming cases.

## 2026-08-08 Power gauge

- `Player`와 `Opponent`는 `DefaultPowerGaugeConfig`를 참조하는 `PowerGauge`를 사용한다. 기본값은 최대 100, 경기 중 자연 충전 초당 1이며 기본 펀치 10, 크로스 펀치/슬라이딩 태클 15, 방어/회피 10이다.
- 보상은 실제 타격·방어 성공·회피 성공에만 적용한다. 잡기는 규칙에서 비활성화되어 있으며, 항목과 수치는 ScriptableObject 규칙으로 변경할 수 있다.
- 게이지는 최대치에서 유지되고 새 경기 시작에서만 초기화된다. 스코어 후 킥오프에서는 유지되며, HUD의 스태미나 바 위에 표시된다.
- 가득 찬 게이지에서 R을 누르면 강화 대기 상태가 된다. R 재입력 또는 C는 게이지를 보존한 채 대기를 취소하고, 좌/우클릭·Q·E·F·Shift 2연타가 실제로 시작된 경우에만 전량 소비한다. 이번 단계는 강화 컨텍스트 전달 기반만 제공하며 효과 수치와 P2P 패킷은 바꾸지 않는다.
- Unity EditMode 전체 142/142 통과. 실제 Play Mode에서의 R/C·각 행동 소비·HUD 구분과 2클라이언트 P2P 보상 경로는 별도 수동 확인이 필요하다.

## 2026-07-20 Update

- Combat tuning is separated into `CombatConfig` ScriptableObject data with `DefaultCombatConfig.asset` linked from scene and `NetPlayer` combat components.
- Ball possession, dribble, shot, and physics tuning is separated into `BallConfig` ScriptableObject data with `DefaultBallConfig.asset` linked from scene/player ball components.
- `Ball` now has an explicit `BallController` in the active scene instead of relying only on runtime attachment from `PlayerBallHandler`.
- `PlayerBallHandler` remains the compatibility facade for `CurrentOwner`, `HasBall`, `Shoot`, `ForceRelease`, `ClearPossession`, `IsCharging`, and `ChargeAmount01`.
- Player-specific initial acquisition, delayed reacquisition, release bookkeeping, and ownership cleanup now live in `BallPossessionController`; charge, shoot, dribble placement, and presentation remain in the facade.

## 2026-07-25 Unified input scene wiring

- `SampleScene` uses the single `GameplayInputReader` on `Player`, backed by `Assets/_Game/Settings/InputSystem_Actions.inputactions`.
- `PlayerInput`, `GameManager`, `CameraViewSwitcher`, and `ViewHintUI` all reference that same scene reader. `NetPlayer` no longer owns a second reader, so it cannot disable the shared action map; an unassigned player resolves the scene reader at runtime.
- `F5` now alternates ownership of the main camera between `ThirdPersonActionCamera` and the legacy `CameraViewSwitcher` view; the hint reports the active view.
- Verified in Unity EditMode: the full suite `56/56` passed, including actual arrow-key movement and right-Shift press/hold/release through the Input System test device.
- Manual Play Mode follow-up remains: confirm pause, camera-toggle hint/toggle, movement/actions, and no missing-reference messages using the active scene reader.

## 2026-07-26 Contextual player actions

- `InputSystem_Actions.inputactions` now exposes context-neutral `PrimaryAction`, `SecondaryAction`, one-touch Alt composites, `CancelAction`, `ContextQ`, `Grab`, `ContextF`, and Space `Dodge`. The retained WASD/arrow and both Shift bindings remain rebindable defaults.
- `GameplayInputReader` suppresses Primary/Secondary while their Alt one-touch composite is active, so action routing never receives the same click as both a direct and prepared action.
- `PlayerInput` keeps movement and sprint input only. `ContextualPlayerActionRouter` chooses pass-charge versus punch from ball possession, keeps secondary-without-ball/Q/E/F deferred, and clears pending state through `C`, disable, and match reset.
- `OneTouchIntentBuffer` retains the latest prepared pass or shot. `OneTouchActionExecutor` immediately executes it with possession, whiffs with only the existing shoot animation without possession, and consumes it only after a successful with-ball action.
- Verified in Unity EditMode: focused contextual-input regression tests and the full `60/60` suite passed. Manual Play Mode confirmation remains for no-ball whiff presentation and automatic prepared-action consumption after later possession.

## 2026-07-26 Possession input context

- `BallController` remains the authority for actual ball ownership. `PossessionInputContext` separately evaluates the effective player-input context without changing ownership, physics, combat, or animation state.
- While Sprint is held, a free ball inside the existing configured acquisition range retains the pass/shot input context for `0.65` seconds after ownership drops. An opponent owner or an out-of-range ball ends that input grace immediately.
- Primary/secondary ball actions are latched at press. If sprint touch has temporarily released the ball, a held possession-context mouse input waits for real reacquisition instead of becoming a punch or starting a new no-ball action.
- A no-ball primary punch or F tackle starts a `0.40`-second transition window. Further no-ball combat input remains available and refreshes that timer. Only actual ownership gained during the window blocks LMB/RMB and Alt mouse ball actions until the window ends, preventing combat spam from becoming an immediate pass or shot. F remains a no-op while possession context is active.
- Q spends the same 30 stamina as dodge and opens a `1.50`-second defense window regardless of ball possession. An actual punch, cross punch, tackle, or grab attempt inside the window is blocked and selects Right, Back, or Left Block from the attacker's clockwise angle around the defender. A blocked tackle resolves that target's full slide contact across later physics ticks; an evaded tackle remains eligible for a later overlap check. `DefenseController.TryBlockTackle` currently falls back to the directional block animation and is the extension point for a dedicated tackle-block animation and response. The current implementation is defense-only; the planned no-ball counterattack is not implemented yet.
- Automated coverage is limited to the pure possession-context timer/suppression rules. Manual Play Mode follow-up remains required for sprint-touch recovery, held/released mouse behavior across recovery, combat rapid-input recovery, and F's possession no-op behavior.

## 2026-07-31 Direct P2P setup foundation

- Added the pre-release `com.unity.webrtc` package and the `Unity.WebRTC` runtime assembly reference.
- `ExperimentalNet/P2P/` now separates setup-message validation/fragmentation, NGO lobby signaling, and WebRTC peer/DataChannel lifecycle.
- Existing NGO/Unity Relay remains the room and setup-message path only. SDP and ICE are split into 900-byte named-message fragments before relay.
- The lobby dynamically prepares one STUN-only direct peer connection for a two-player room and displays connection or failure status. No TURN server and no automatic Relay gameplay fallback were added.
- Direct P2P diagnostics log only role, signal kind/length, candidate counts, and ICE/DataChannel state transitions. SDP, candidate contents, and IP addresses are not logged.
- A participant can now select an available BLUE or RED slot themselves. The server accepts only the requesting client's ID, moves that client atomically from any prior team slot, and leaves the current assignment untouched when the requested team is full.
- `P2pMovementReplicator` now dynamically attaches to each network player. Once the direct channel is ready, local human players send 20 Hz position/yaw snapshots; the one remote human player rejects stale sequences and interpolates its visible movement. Its `ClientNetworkTransform` is disabled during that P2P path so NGO does not compete for the transform.
- Ball, combat, match state, AI, and action/result traffic still use their existing NGO paths. A two-player room cannot start the match unless the direct P2P channel is ready, so P2P failure does not silently fall back to Relay movement.
- Verified in Unity EditMode: full suite 102/102 passed. Manual evidence (2026-08-01): direct P2P succeeded when the laptop used a mobile-data hotspot instead of the nested home Wi-Fi router. The earlier wired-PC/home-Wi-Fi failure is therefore consistent with that local double-NAT or NAT-hairpin path, not proof that the signaling path is broken. This does not validate general NAT traversal: TURN and an automatic Relay gameplay fallback are still absent, and broader two-client/Play Mode coverage remains a manual gate.

## 2026-07-24 Update

- `CharacterLocomotion` owns stamina, sprint drain/regeneration, dodge timing, and dodge availability; `CharacterMotor` remains responsible for applying the resolved movement and dash velocity.
- Dodge grants temporary invulnerability through `CharacterState`; combat rejects punch/slide attempts while dodging and ignores hits against an invulnerable target.
- Ball dribble placement now uses bounded smooth follow and rolling rotation. Shots preserve owner momentum, add force-scaled loft, and receive a short first-touch bonus after possession.
- `SimpleAIController` predicts free-ball motion, commits to a brief dribble before shooting, defends goal-side when distant, and can dodge an incoming slide.
- `AbilityCooldownUI` reads `CharacterLocomotion` to render stamina and dodge status alongside the combat cooldowns.

## 2026-07-19 Update

- Keyboard movement now combines WASD and arrow keys into a single normalized `Vector2` before passing player intent to `CharacterLocomotion`.
- Player movement responsibility is split across `CharacterMovementConfig`, `CharacterMovementUtility`, `CharacterLocomotion`, and `CharacterMotor`. `CharacterMotor` now applies resolved movement profiles to Rigidbody movement/rotation instead of owning input intent or profile selection.
- Third-person camera yaw now prefers locomotion intent, uses quick-turn handling for side turns while leaving near-180 degree reversals on the normal rotation limit, and only applies ball-assist yaw while movement input is active.
- Combat and charged shots now accept locked action directions from input time while preserving no-argument AI fallbacks.
- `CameraViewSwitcher` now defers to `ThirdPersonActionCamera` when the action camera is enabled, preventing competing `LateUpdate` camera pose writes.

## 현재 요약

- 로컬 3D 풋살 프로토타입이 구현되어 있다.
- 캐릭터 이동, 공 소유/슛/패스, 전투, 득점/리셋, HUD가 기본 동작한다.
- 카메라는 Futsal 전용 정책 코드와 Cinemachine backend를 함께 사용한다.
- Netcode/LAN 코드는 `ExperimentalNet` 아래의 실험 코드로 유지한다.

## 사용 중인 주요 기술

- Unity 3D / URP
- New Input System
- UGUI
- Cinemachine 3.1.7
- Netcode for GameObjects
- Unity MCP

## 구현 상태

### Match

- `Assets/_Game/Scripts/Runtime/Match/GameManager.cs`
  - `Kickoff`, `Playing`, `GameOver` 경기 상태
  - 카운트다운, 경기 시간, 점수, 일시정지, 재시작
  - 득점 후 캐릭터와 공 리셋

- `Assets/_Game/Scripts/Runtime/Match/GoalTrigger.cs`
  - 공 골인 트리거 처리

### Characters

- `Assets/_Game/Scripts/Runtime/Characters/CharacterMotor.cs`
  - `Rigidbody` 기반 이동/회전
  - 스턴 중 이동 제한
  - 슬라이딩 dash 속도 적용

- `Assets/_Game/Scripts/Runtime/Characters/Movement/`
  - `CharacterMovementConfig` ScriptableObject balance data
  - `CharacterLocomotion` movement intent, action direction, and profile selection
  - `CharacterMovementUtility` pure movement/input direction calculations

- `Assets/_Game/Scripts/Runtime/Characters/CharacterState.cs`
  - 캐릭터 상태 관리

- `Assets/_Game/Scripts/Runtime/Characters/CharacterAnimator.cs`
  - 이동/전투 상태와 애니메이션 연동

### Ball and Combat

- `Assets/_Game/Scripts/Runtime/Ball/PlayerBallHandler.cs`
  - Compatibility facade for ball ownership, dribble positioning, charge state, shooting, and forced release

- `Assets/_Game/Scripts/Runtime/Ball/BallConfig.cs`
  - ScriptableObject balance data for possession, dribble, shot, and ball physics tuning

- `Assets/_Game/Scripts/Runtime/Ball/BallController.cs`
  - Ball physics ownership, current owner, possession release, and free-ball restore

- `Assets/_Game/Scripts/Runtime/Ball/BallPossessionController.cs`
  - Player-specific acquisition, release delay, ownership release, and cleanup rules behind `PlayerBallHandler`

- `Assets/_Game/Scripts/Runtime/Ball/BallInteractionController.cs`
  - Sprint-touch timing, direction-based pass release, charged-shot state, and interaction cancellation behind `PlayerBallHandler`
  - Verified by `BallInteractionControllerTests` and the Unity EditMode suite (`37/37 passed` on 2026-07-22)

- `Assets/_Game/Scripts/Runtime/Ball/BallImpactEffect.cs`
  - 공 충돌 이펙트 처리

- `Assets/_Game/Scripts/Runtime/Combat/CombatController.cs`
  - Punch, tackle, cooldown, hit, knockback, ball release, and effects orchestration

- `Assets/_Game/Scripts/Runtime/Combat/CombatConfig.cs`
  - ScriptableObject balance data for punch, tackle, hit stun, knockback, and direction assist tuning

### Camera

- `Assets/_Game/Scripts/Runtime/Camera/ThirdPersonActionCamera.cs`
  - context 수집, CameraDirector 실행, plan 조립, backend 적용만 담당하는 thin orchestrator
  - 이동 방향 우선 yaw, 회전 deadzone/max speed, 약한 ball assist, FOV boost clamp, no-roll rig pose, capped shake 규칙은 분리된 resolver가 유지

- `Assets/_Game/Scripts/Runtime/Camera/Core/`, `Modes/`, `Resolvers/`, `Backends/`
  - `CameraContext`는 `BallController.CurrentOwner`를 기준으로 대상 플레이어의 공 소유 사실을 전달하며, `CameraModeResult`와 framing profile은 mode 공통 Core 계약으로 둔다.
  - default third-person과 possession base mode가 framing을 선택하고, aim/position/FOV/effect resolver 및 Unity/Cinemachine backend가 그 결과만 적용한다.

- `Assets/_Game/Scripts/Runtime/Camera/CinemachineActionCameraBackend.cs`
  - Cinemachine follow rig target, framing 거리/높이, pitch aim offset, lens FOV, impulse 전달 adapter

- Active scene camera 구성
  - `Main Camera`: `CinemachineBrain`, `ThirdPersonActionCamera`, `CinemachineActionCameraBackend`
  - `Futsal Cinemachine Third Person Camera`: `CinemachineCamera`, `CinemachineThirdPersonFollow`, `CinemachineHardLookAt`, `CinemachineImpulseListener`
  - `CameraViewSwitcher`는 Main Camera transform 충돌 방지를 위해 비활성화 상태

### UI, Audio, VFX

- `Assets/_Game/Scripts/Runtime/UI/`
  - 경기 UI, 차징 게이지, 쿨다운 HUD, 시점 힌트 표시

- `Assets/_Game/Scripts/Runtime/Audio/AudioManager.cs`
  - 게임 오디오 호출 관리

- `Assets/_Game/Scripts/Runtime/VFX/`
  - 파티클 자동 제거, 골망 처리

### ExperimentalNet

- `Assets/_Game/Scripts/Runtime/ExperimentalNet/`
  - LAN Host/Join, 방 슬롯 UI, 간단 NetworkTransform 실험
  - 온라인 기능 확장은 별도 승인 전까지 보류

## 주요 에셋

- `Assets/_Game/Scenes/SampleScene.unity`
- `Assets/_Game/Prefabs/NetPlayer.prefab`
- `Assets/DefaultNetworkPrefabs.asset`
- `Assets/_Game/Settings/InputSystem_Actions.inputactions`
- `Packages/manifest.json`
- `Packages/packages-lock.json`

## 최근 검증

- EditMode: `35/35 passed` through Unity MCP on 2026-07-21.
- PlayMode Test Runner: `FutsalGame` suite `Passed` 반환, summary count는 `0`
- Play Mode smoke:
  - Cinemachine Brain active camera: `Futsal Cinemachine Third Person Camera`
  - FOV: `85.00`
  - Aim: `CinemachineHardLookAt`
  - Unity console game error/warning: 0

## 남은 확인 항목

- Play Mode에서 실제 키보드 조작, 득점, 리셋 흐름 수동 확인
- LAN Host/Join 수동 확인
- `CameraViewSwitcher`를 제거할지, Cinemachine priority 전환 방식으로 재구성할지 결정
- 캐릭터 컨트롤 기준을 `Rigidbody`로 유지할지 `CharacterController`로 바꿀지 결정

### 2026-07-22 Manual camera look

- `MouseLookInput`은 매치 활성 중 마우스 delta를 읽고 커서를 잠근다.
- `CameraLookController`는 yaw/pitch, 감도, Y축 반전, pitch 제한을 소유한다. 마우스 이동은 캐릭터 이동이나 facing을 바꾸지 않는다.
- `ThirdPersonActionCamera`는 이 상태를 `PositionResolver`에 전달해 direct-camera와 Cinemachine follow-rig 경로에 적용한다.
- Cinemachine follow rig은 yaw만 회전한다. pitch는 `CinemachineHardLookAt.LookAtOffset`으로 전달하므로 상하 시선 조작이 카메라 리그의 높이를 끌어올리지 않는다.
- 카메라 모드는 프레이밍만 선택한다. 이동 기반 heading, quick-turn yaw, ball-assist yaw와 `AimResolver`는 제거했다.
- `ThirdPersonActionCameraSettings.asset`은 Unity SerializedObject로 yaw/pitch 감도 `0.12`, Y축 기본 방향, pitch `-35`~`65`도, aim offset 최대 `2.4`, 기본 거리 `6.4`, 높이 `3.5`로 설정했다.

### 2026-07-23 Mouse charge pass and shot

- `InputSystem_Actions.inputactions`은 기본적으로 좌클릭 차지 패스, 우클릭 차지 슛, `C` 차지 취소를 제공하며, 행동별 기본 바인딩은 향후 런타임 재지정과 저장의 기준값이다.
- `PlayerInput`은 버튼 해제 순간의 카메라 수평 전방 방향만 `PlayerBallHandler`에 전달한다. 차지 중 마우스 시점 전환은 다음 해제 방향에 반영되고 캐릭터 facing은 바꾸지 않는다.
- `BallInteractionController`는 동시에 하나의 `Pass` 또는 `Shot` 차지만 보유한다. 취소 또는 다른 행동 해제는 소유 공을 발사하지 않는다.
- `BallConfig`은 패스 차지 힘 `3.5`~`7.0`, 슛 차지 힘 `3.5`~`13.0`, 최대 차지 시간 `1.0`초를 사용한다.
- Unity MCP EditMode 전체 `40/40 passed`; Play Mode에서 활성 Cinemachine 카메라, `PlayerInput`의 바인딩 에셋 참조, 콘솔 오류/경고 0을 확인했다. 실제 마우스 조작감은 수동 확인이 필요하다.
