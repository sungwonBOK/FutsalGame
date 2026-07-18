# Futsal Brawl 프로젝트 구조

업데이트: 2026-07-18
기준 브랜치: `develop_merge`

현재 체크아웃 기준의 파일 배치 규칙만 기록한다. 세부 구현 상태는 `IMPLEMENTATION_STATUS.md`에 둔다.

## 루트

- `AGENTS.md`
  - Codex/Claude 작업 규칙과 Unity 작업 제한

- `CLAUDE.md`
  - Claude 작업용 보조 규칙

- `Futsal Brawl Game Design Document.docx`
  - 게임 디자인 문서

- `IMPLEMENTATION_STATUS.md`
  - 현재 구현 상태와 검증 기록

- `PROJECT_STRUCTURE.md`
  - 현재 프로젝트 배치 기준

- `Packages/`
  - Unity 패키지 선언과 lock 파일

- `ProjectSettings/`
  - Unity 프로젝트 설정

## Assets 구조

```text
Assets/
  _Game/
    Scripts/
      Runtime/
      Tests/
    Scenes/
    Prefabs/
    Materials/
    Audio/
    Animation/
    Effects/
    Settings/
  Dev/
  ThirdParty/
  TutorialInfo/
  DefaultNetworkPrefabs.asset
```

## `Assets/_Game`

실제 Futsal Brawl 게임 코드와 게임 소유 에셋을 둔다.

- `Scripts/Runtime/`
  - 빌드에 포함되는 게임 실행 코드

- `Scripts/Tests/EditMode/`
  - 순수 계산, 정책, 작은 adapter 검증

- `Scripts/Tests/PlayMode/`
  - 씬, 물리, 프리팹 연결 검증용 테스트

- `Scenes/`
  - 직접 관리하는 게임 씬

- `Prefabs/`
  - 직접 관리하는 게임 프리팹

- `Materials/`, `Audio/`, `Animation/`, `Effects/`, `Settings/`
  - 게임 전용 리소스

## Runtime 폴더

- `Match/`
  - 경기 상태, 점수, 시간, 득점, 리셋

- `Characters/`
  - 캐릭터 이동, 상태, 애니메이션 연동

- `Ball/`
  - 공 소유, 드리블, 슛/패스, 공 충돌 처리

- `Combat/`
  - 펀치, 태클, 히트 판정, 스턴, 넉백, 쿨다운

- `Input/`
  - 사람 플레이어 입력 처리

- `Camera/`
  - Futsal 전용 카메라 정책과 Cinemachine backend adapter

- `AI/`
  - 간단 AI 판단과 행동 선택

- `UI/`
  - HUD, 경기 표시, 게이지, 쿨다운, 안내 표시

- `Audio/`
  - 오디오 재생 라우팅

- `VFX/`
  - 파티클 수명, 골망, 이펙트 보조 로직

- `ExperimentalNet/`
  - 확정 전 Netcode/LAN 실험 코드

## 외부/보조 폴더

- `Assets/Dev/`
  - 임시 실험, 캡처, 테스트용 보조 자료

- `Assets/ThirdParty/`
  - 직접 작성하지 않은 외부 에셋이나 플러그인 자료

- `Assets/TutorialInfo/`
  - Unity 템플릿/튜토리얼 안내 자료

- `Assets/DefaultNetworkPrefabs.asset`
  - Unity/Netcode가 루트에 유지하는 기본 네트워크 프리팹 목록

## 배치 원칙

- Runtime 코드와 Editor 코드는 분리한다.
- Editor 전용 코드는 `Assets/_Game/Scripts/Editor/`에 둔다.
- `.unity`, `.prefab`, `.asset`, `.inputactions`는 직접 YAML 편집하지 않는다.
- Scene/Prefab/Asset 변경은 Unity Editor, Unity MCP, 또는 반복 가능한 Editor Tool로 수행한다.
- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`는 관리 대상에서 제외한다.
- 기능 구현 중 대규모 구조 이동은 피하고, 구조 정리는 별도 작업으로 분리한다.
- `ExperimentalNet/` 코드는 승인 전 온라인 기능 확장 없이 실험/정리 대상으로만 다룬다.
