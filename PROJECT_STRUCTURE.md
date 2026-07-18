# Futsal Brawl 프로젝트 구조

업데이트: 2026-07-18
기준 브랜치: `develop_merge`

이 문서는 현재 체크아웃된 파일 배치를 기준으로 작성한다.

## 루트

- `AGENTS.md`
  - Codex/Claude 작업 규칙과 Unity 작업 제한 사항

- `CLAUDE.md`
  - Claude 작업용 보조 규칙

- `Futsal Brawl Game Design Document.docx`
  - 게임 디자인 문서

- `IMPLEMENTATION_STATUS.md`
  - 현재 구현 상태 기록

- `PROJECT_STRUCTURE.md`
  - 현재 파일/폴더 배치 기준

- `.gitignore`
  - Git 제외 규칙

## Unity 폴더

### `Assets/`

게임 에셋과 Runtime 스크립트가 들어 있는 Unity 루트다.

- `Assets/DefaultNetworkPrefabs.asset`
  - Netcode 기본 프리팹 목록 에셋

- `Assets/InputSystem_Actions.inputactions`
  - Input System 액션 정의

- `Assets/Animation/`
  - 캐릭터 및 게임 오브젝트 애니메이션

- `Assets/Audio/`
  - 게임 오디오 에셋

- `Assets/Characters/`
  - 캐릭터 관련 에셋

- `Assets/Effects/`
  - VFX와 파티클 관련 에셋

- `Assets/Materials/`
  - 게임용 머티리얼

- `Assets/Prefabs/`
  - 게임 프리팹

- `Assets/Scenes/`
  - Unity 씬

- `Assets/Scripts/`
  - Runtime C# 스크립트

- `Assets/Scripts/Net/`
  - Netcode/LAN 실험용 Runtime C# 스크립트

- `Assets/Screenshots/`
  - 개발 중 캡처 이미지

- `Assets/Settings/`
  - URP 관련 렌더링 설정 에셋

### `Assets/Scripts/`

현재 별도 `_Game` 폴더 없이 스크립트가 바로 배치되어 있다.

- `AudioManager.cs`
  - 게임 오디오 호출 관리

- `AutoDestroyParticle.cs`
  - 파티클 자동 제거

- `BallImpactEffect.cs`
  - 공 충돌 이펙트 처리

- `CharacterAnimator.cs`
  - 캐릭터 애니메이션 상태 연동

- `CharacterMotor.cs`
  - 캐릭터 이동/회전, 슬라이딩 dash 처리

- `CharacterState.cs`
  - 캐릭터 상태 관리

- `CombatController.cs`
  - 펀치, 슬라이딩 태클, 히트 처리, 쿨다운 상태 제공

- `GameManager.cs`
  - 경기 상태, 카운트다운, 득점, 일시정지, 리셋 흐름

- `GoalNet.cs`
  - 골대/골망 관련 처리

- `GoalTrigger.cs`
  - 득점 트리거 처리

- `MatchUI.cs`
  - 경기 UI 표시

- `PlayerBallHandler.cs`
  - 공 소유, 슛, 패스 처리

- `PlayerInput.cs`
  - 플레이어 입력 처리

- `SimpleAIController.cs`
  - 간단 AI 제어

- `ChargeGaugeUI.cs`
  - 플레이어 차징 게이지 표시

- `AbilityCooldownUI.cs`
  - 펀치/슬라이딩 쿨다운 HUD

- `CameraViewSwitcher.cs`
  - F5 카메라 시점 전환

- `ViewHintUI.cs`
  - F5 시점 전환 힌트 표시

### `Assets/Scripts/Net/`

- `LobbyController.cs`
  - OnGUI 기반 메인 메뉴, LAN Host/Join, 방 슬롯 UI

- `NetworkHudUI.cs`
  - 간단한 Host/Join/Disconnect HUD

- `ClientNetworkTransform.cs`
  - 소유자 권위 NetworkTransform

## 프로젝트 설정

- `ProjectSettings/`
  - Unity 프로젝트 설정

- `Packages/manifest.json`
  - Unity 패키지 선언

- `Packages/packages-lock.json`
  - Unity 패키지 lock 파일

## 권장 목표 구조

새 파일을 추가할 때는 기존 배치가 정리되기 전까지 무리하게 대규모 이동하지 않는다. 이후 구조 정리 작업을 승인받으면 다음처럼 정리하는 것을 목표로 한다.

- `Assets/_Game/Scripts/Runtime/`
  - 실제 게임 Runtime 코드

- `Assets/_Game/Scripts/Editor/`
  - Editor 전용 도구 코드

- `Assets/_Game/Scenes/`
  - 직접 관리하는 게임 씬

- `Assets/_Game/Prefabs/`
  - 직접 관리하는 게임 프리팹

- `Assets/_Game/Materials/`
  - 게임용 머티리얼

- `Assets/_Game/Audio/`
  - 게임용 오디오

- `Assets/_Game/Effects/`
  - VFX 프리팹과 이펙트 소재

## 배치 원칙

- Runtime 코드와 Editor 코드는 분리한다.
- `.unity`, `.prefab`, `.asset`, `.inputactions`는 직접 YAML 편집하지 않는다.
- Scene/Prefab 설정은 가능하면 Unity Editor, Unity MCP, 또는 반복 가능한 Editor Tool로 변경한다.
- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`는 관리 대상에서 제외한다.
- 기능 구현 중 구조 이동은 최소화한다. 구조 정리는 별도 작업으로 분리한다.
- Netcode/LAN 관련 파일은 승인 전 기능 확장하지 않고 현재 상태 기록 또는 정리 대상으로만 다룬다.
