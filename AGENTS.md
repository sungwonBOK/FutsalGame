# Futsal Brawl 에이전트 규칙

## 고정 기준
- Unity 3D URP 기반, 초기 목표 플랫폼은 PC Steam이다.
- Unity New Input System과 Cinemachine을 사용한다.
- 플레이어는 `CharacterController`, 공은 `Rigidbody`를 사용한다.
- 현재 단계는 애니메이션 없는 Capsule 기반 로컬 프로토타입이다.
- 별도 승인 전 제외: 온라인, 스킨, 애니메이션, 잡기, 업어치기, 다운/그라운드 전투.



## 작업 규칙
- 작업 시작전 github issue tracker에 있는 진행상황 확인 후 진행한다.
- 한 작업에서는 하나의 시스템만, 동작에 필요한 최소 변경만 수행한다.
- 기존 패턴을 우선하며 추측성 추상화, 무관한 리팩터링, 기능 확장을 하지 않는다.
- 명시적 승인 없이 패키지·네트워크 라이브러리·`ProjectSettings`를 추가하거나 변경하지 않는다.
- Runtime과 Editor 코드를 분리하고 Editor 전용 코드는 `Assets/_Game/Scripts/Editor/`에 둔다.
- `.unity`, `.prefab`, `.asset` YAML을 직접 수정하지 않는다. Unity Editor, Unity MCP 또는 Editor Tool을 사용한다.
- `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`는 수정하거나 커밋하지 않는다.
- `.meta`와 GUID를 보존하고, 에셋 이동·이름 변경은 가능하면 Unity에서 수행한다.
- 반복 가능한 Scene/Prefab 설정은 Editor Tool로 만든다.
- 다른 활성 작업이 소유한 파일은 수정하지 않는다.
- Subagent는 독립적인 조사·검증에만 사용하며 겹치는 파일을 병렬 수정하지 않는다.

## 변경 전
- `git status`, 대상 코드, 기존 테스트를 확인한다.
- Scene, Prefab, Input Asset, Package, ProjectSettings 변경 전 소유권을 확인한다.

## 검증과 인수인계
- 가능한 컴파일 검사와 테스트를 실행한다.
- 실제 확인하지 않은 Unity Console 또는 Play Mode 성공을 주장하지 않는다.
- `git diff`를 검토하고 무관하거나 자동 생성된 변경을 제거한다.
- 변경 파일, 동작, 실제 검증 결과, 필요한 Unity 수동 확인, 남은 위험을 보고한다.
- github에 작업한 파일을 올릴시 github issue tracker에 진행상황을 업데이트한다.

협업 절차는 `WORKFLOW.md`를 따른다.
