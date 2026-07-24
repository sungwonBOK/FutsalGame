# Futsal Brawl 구현 상태

업데이트: 2026-07-18
기준 브랜치: `develop_merge`

현재 체크아웃 기준의 구현 상태만 기록한다. 세부 설계나 작업 이력은 이 문서에 길게 누적하지 않는다.

## 2026-07-20 Update

- Combat tuning is separated into `CombatConfig` ScriptableObject data with `DefaultCombatConfig.asset` linked from scene and `NetPlayer` combat components.
- Ball possession, dribble, shot, and physics tuning is separated into `BallConfig` ScriptableObject data with `DefaultBallConfig.asset` linked from scene/player ball components.
- `Ball` now has an explicit `BallController` in the active scene instead of relying only on runtime attachment from `PlayerBallHandler`.
- `PlayerBallHandler` remains the compatibility facade for `CurrentOwner`, `HasBall`, `Shoot`, `ForceRelease`, `ClearPossession`, `IsCharging`, and `ChargeAmount01`.
- Player-specific initial acquisition, delayed reacquisition, release bookkeeping, and ownership cleanup now live in `BallPossessionController`; charge, shoot, dribble placement, and presentation remain in the facade.

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
  - `CameraContext`/`CameraPlan` 데이터, default third-person mode, aim/position/FOV/effect resolver, Unity/Cinemachine 최종 적용 경계를 제공

- `Assets/_Game/Scripts/Runtime/Camera/CinemachineActionCameraBackend.cs`
  - Cinemachine follow rig target, lens FOV, impulse 전달 adapter

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

- EditMode: `32/32 passed` through Unity MCP on 2026-07-20.
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
