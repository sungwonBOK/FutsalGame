# Futsal Brawl 구현 상태

업데이트: 2026-07-18
기준 브랜치: `develop_merge`

이 문서는 현재 체크아웃된 파일 기준으로 구현 상태만 간단히 기록한다.

## 현재 요약

현재 프로젝트에는 로컬 3D 풋살 프로토타입, 전투/차징 HUD, 카메라 전환, LAN 로비 실험 코드가 함께 들어 있다.

## 사용 중인 주요 기술

- Unity 3D 프로젝트
- URP 패키지: `com.unity.render-pipelines.universal`
- New Input System 패키지: `com.unity.inputsystem`
- UGUI 패키지: `com.unity.ugui`
- Netcode for GameObjects 패키지: `com.unity.netcode.gameobjects`
- Unity MCP 패키지: `com.coplaydev.unity-mcp`

주의: AGENTS 규칙상 별도 승인 전 온라인 기능 확장은 제외 범위다. 현재 Netcode/LAN 코드는 기존 실험 코드로 기록하되, 추가 확장은 승인 후 진행한다.

## 구현 상태

### 경기 흐름

- `Assets/Scripts/GameManager.cs`
  - 경기 상태: `Kickoff`, `Playing`, `GameOver`
  - 카운트다운, 경기 시간, 점수, 일시정지, 재시작 흐름 관리
  - 득점 후 캐릭터와 공을 시작 위치로 리셋

### 캐릭터 이동

- `Assets/Scripts/CharacterMotor.cs`
  - `Rigidbody` 기반 이동/회전
  - 외부 입력 방향 주입
  - 스턴 중 이동 제한
  - 슬라이딩용 `Dash` 속도 적용

### 전투

- `Assets/Scripts/CombatController.cs`
  - 펀치와 슬라이딩 태클
  - 쿨다운과 준비 상태 제공
  - 히트 시 넉백, 스턴, 공 소유 해제, 이펙트/오디오 훅 호출

### UI

- `Assets/Scripts/ChargeGaugeUI.cs`
  - 플레이어 슛/패스 차징 게이지 표시

- `Assets/Scripts/AbilityCooldownUI.cs`
  - 펀치와 슬라이딩 쿨다운 HUD 표시

- `Assets/Scripts/ViewHintUI.cs`
  - F5 카메라 전환 힌트와 현재 시점 표시

### 카메라

- `Assets/Scripts/CameraViewSwitcher.cs`
  - F5 입력으로 기본 고정 시점과 3인칭 추적 시점 전환

### LAN/Netcode 실험

- `Assets/Scripts/Net/LobbyController.cs`
  - OnGUI 기반 메인 메뉴, LAN Host/Join, 방 UI
  - `NetworkList<TeamSlot>` 기반 팀 슬롯 동기화
  - "게임 시작"은 현재 `GameManager.BeginMatch()` 호출 placeholder

- `Assets/Scripts/Net/NetworkHudUI.cs`
  - Host/Join/Disconnect용 간단 OnGUI HUD

- `Assets/Scripts/Net/ClientNetworkTransform.cs`
  - 소유자 권위 `NetworkTransform` 파생 클래스

## 현재 주요 에셋

- `Assets/Scenes/SampleScene.unity`
- `Assets/Prefabs/NetPlayer.prefab`
- `Assets/DefaultNetworkPrefabs.asset`
- `Assets/InputSystem_Actions.inputactions`
- `Assets/Animation/`
- `Assets/Audio/`
- `Assets/Characters/`
- `Assets/Effects/`
- `Assets/Materials/`
- `Assets/Settings/`
- `ProjectSettings/`
- `Packages/manifest.json`
- `Packages/packages-lock.json`

## 남은 확인 항목

- Play Mode에서 실제 조작, 득점, 리셋 흐름 확인
- LAN Host/Join 수동 확인
- 캐릭터 컨트롤 기준을 `Rigidbody`로 유지할지 `CharacterController`로 맞출지 결정
