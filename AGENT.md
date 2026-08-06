# DungeonStory Agent Guide

## Fallback Policy

- Do not add fallback behavior by default.
- Prefer failing loudly with a clear reason, debug state, and test coverage over silently substituting another behavior.
- Add a fallback only when it is explicitly required by the design, requested by the user, or needed for a deliberate compatibility path.
- Any fallback must be visible: log or expose the exact fallback reason, source action, target action, and affected object.
- Any fallback that changes gameplay behavior must have a focused regression test for the original failure case and the fallback path.
- Avoid fallback chains. If more than one fallback step is needed, stop and model the state explicitly instead.
- Never use fallback movement or fallback AI actions to hide missing grid, missing destination, invalid data, or unreachable path errors.
- For character AI, a failed action should usually be reported as unavailable/cannot-start/no-path rather than replaced with another action unless the design document names that replacement.

이 문서는 DungeonStory 프로젝트에서 Codex/에이전트가 작업할 때 지켜야 할 기본 규칙이다.

## Unity MCP 사용

- Unity 관련 작업에서는 Unity MCP를 사용해도 된다.
- 코드 변경 후에는 가능하면 Unity MCP로 에디터 컴파일 상태와 콘솔 로그를 확인한다.
- `Unity_RunCommand`는 에디터 타입 로드, `AssetDatabase.Refresh()`, 간단한 검증 스크립트 실행에 사용할 수 있다.
- `Unity_GetConsoleLogs`는 컴파일 에러, 런타임 에러, 기존 경고 구분에 사용한다.
- Unity MCP에서 보이는 기존 경고와 새로 만든 에러를 구분해서 보고한다.

## 프리팹/에셋 변경 고지

- 코드 분리는 좋지만, 기존 프리팹, 씬, ScriptableObject 에셋, 인스펙터 연결을 수정해야 하는 변경은 작업 전에 먼저 말한다.
- 사용자가 직접 Unity 에디터에서 연결해야 하는 필드가 생기면 반드시 미리 알린다.
- 기존 에셋 수정을 요구하지 않는 순수 코드 리팩터링이라면 그대로 진행해도 된다.
- 런타임에 기존 방식과 호환되도록 만들 수 있으면 우선 그 방향을 선택한다.
- 프리팹 기반 구조로 바꾸는 경우에도 기존 `BuildingSO.type` 같은 현재 데이터 흐름과의 마이그레이션 경로를 함께 제시한다.

## 구현 전 설계 게이트

새 기능, 데이터 구조 변경, 저장 계약 변경, 여러 도메인에 걸친 수정은 코드를 먼저 작성하지 않는다. 구현 전에 아래 구조 계약을 작성하고 서로 모순이 없는지 확인한다. 계약이 불완전하거나 동일 상태의 권위가 둘 이상이면 구현을 시작하지 않는다.

필수 구조 계약:

| 항목 | 구현 전에 확정할 내용 |
|---|---|
| 콘텐츠 정의 | 어떤 ScriptableObject와 카탈로그가 불변 정의의 최종 원본인지 |
| 런타임 상태 | 어떤 Aggregate 또는 상태 저장소가 가변 상태의 유일한 쓰기 권위인지 |
| 명령 | 상태를 변경할 수 있는 유일한 API와 호출 주체가 무엇인지 |
| 조회 | UI, AI, 검증기가 어떤 읽기 전용 Query 또는 파생 인덱스를 사용하는지 |
| 식별자 | Definition ID, Instance ID, 주문/거래 ID의 타입, 문법, 발급 주체가 무엇인지 |
| 저장 | 저장할 원본 상태, 저장하지 않을 파생 상태, DTO 버전, 복원 및 실패 원자성이 무엇인지 |
| 의존성 | 도메인과 어셈블리의 참조 방향, 이벤트 발행자와 소비자가 누구인지 |
| 실패 정책 | 누락, 중복, 끊어진 참조, 잘못된 상태를 어디에서 어떤 오류로 거부하는지 |
| 전환 범위 | 제거할 구형 읽기/쓰기 경로와 필요한 명시적 마이그레이션 경계가 무엇인지 |
| 검증 | 실제 명령, 우회 차단, 저장 왕복, 실패 원자성, UI 입력을 어떤 테스트로 증명하는지 |

진행 규칙:

- 동일한 데이터에 SO, 런타임 사전, 저장 DTO 등 둘 이상의 쓰기 권위를 만들지 않는다.
- SO는 불변 콘텐츠 정의, 일반 런타임 객체는 가변 상태, 저장 DTO는 직렬화 경계로 분리한다.
- 저장 DTO, Query, 캐시, UI ViewModel을 새로운 게임 상태 권위로 만들지 않는다.
- 누락된 콘텐츠를 런타임 SO 합성, 기본 정의, 이름/좌표 기반 ID, 암묵적 문자열 변환으로 은폐하지 않는다.
- 기존 시스템을 대체할 때 새 경로를 추가하는 것만으로 끝내지 않는다. 전환 단계가 끝나면 구형 쓰기 경로를 제거하고 이중 쓰기를 금지한다.
- 기능 범위, 저장 호환성, 에셋 구조, 사용자 데이터에 영향을 주는 선택은 구현 전에 사용자에게 구조 계약과 영향 범위를 알린다.
- 작은 국소 버그 수정은 장문의 설계 문서를 요구하지 않지만, 상태 권위와 영향 범위를 확인한 뒤 수정한다.

## 수직 슬라이스 우선

- 대규모 일괄 구현 전에 하나의 실제 플레이 경로를 끝까지 완성한다.
- 예를 들어 아이템 기능은 `SO 정의 -> 물리 아이템 생성 -> 보관/운반 -> 제작 소비 -> 장착/사용 -> 저장 왕복` 한 경로를 먼저 통과시킨다.
- 첫 수직 슬라이스는 임시 우회 API나 테스트 전용 상태 주입이 아니라 운영 코드와 실제 권위를 사용해야 한다.
- 수직 슬라이스에서 구조 계약과 저장 왕복이 통과한 뒤 같은 패턴으로 콘텐츠와 기능을 확장한다.
- 구현 중 구조 계약이 틀렸음이 드러나면 기능을 계속 붙이지 말고 계약과 영향 범위를 먼저 수정한다.

## 기능 완료 기준

컴파일 성공이나 UI 표시만으로 완료 처리하지 않는다. 변경 위험에 맞춰 다음 항목을 증명한다.

- 실제 운영 명령 경로가 동작한다.
- UI, 직접 런타임 호출, 저장 데이터 등 우회 경로로 규칙을 건너뛸 수 없다.
- 저장 -> 복원 -> 재저장 결과가 정규화 계약에 맞게 동일하다.
- 잘못된 입력과 복원 실패가 라이브 상태를 부분 변경하지 않는다.
- 실제 Unity EventSystem 입력과 필요한 해상도에서 UI 흐름이 동작한다.
- 관련 회귀 테스트, Unity 컴파일, Console Error/Warning 기준을 통과한다.
- 구현 결과가 최초 구조 계약과 달라졌다면 완료 전에 계약 문서와 영향 범위를 갱신한다.

## API 계층 원칙

가장 상위 API는 게임에 종속적이어도 된다.

예를 들어 건물 배치의 상위 API는 다음 흐름을 오케스트레이션할 수 있다.

```text
입력/선택
-> 배치 검증
-> 비용/조건 처리
-> GameObject 조립
-> Grid 등록
-> 타일/비주얼 반영
-> 이벤트 발행
```

하지만 하위 API는 가능한 한 모듈화/라이브러리화되어도 될 정도로 분리한다.

여기서 말하는 분리는 `Unity와 무관한 알고리즘 덩어리`로 만들라는 뜻이 아니다. 목표는 다른 Unity 프로젝트에도 가져가 쓸 수 있는 에셋/패키지처럼 만드는 것이다. Grid 코어는 `UnityEngine`, `Vector2Int`, `Vector3` 같은 Unity 런타임 타입을 사용해도 된다. 제거해야 하는 것은 DungeonStory의 현재 씬 구성, 특정 싱글톤, 프리팹 조립 방식, UI 표시 방식, `BuildingSO`/`BuildableObject` 같은 이 게임 전용 모델에 대한 직접 결합이다.

원칙:

- 하위 로직은 `GridSystemManager.Instance`, `DataManager.Instance`, `GameManager.Instance` 같은 전역 싱글톤에 직접 의존하지 않게 한다.
- 계산, 검증, 탐색, 점수 계산, 효과 계산은 Unity 오브젝트 생성과 분리한다.
- Unity `GameObject`, `MonoBehaviour`, `Tilemap` 조작은 Factory, Presenter, Adapter, Service 같은 가장자리 계층에 둔다.
- 재사용 가능한 Grid 모듈은 `Occupant`, `Layer`, `Path`, `Movement` 같은 범용 게임 규칙 언어를 사용해도 된다.
- `BuildingSO`, `BuildableObject`, `GridTexture`, `DataManager` 같은 DungeonStory 전용 연결은 별도 통합 계층에 둔다.
- 데이터 정의, 검증, 생성, 표현, 입력 처리를 한 클래스에 몰아넣지 않는다.
- 상위 서비스는 게임 규칙을 조립하고, 하위 클래스는 자기 책임만 수행하게 만든다.

## 설계 방향

필요하면 디자인 패턴을 사용한다. 다만 패턴 자체가 목적이 되면 안 된다.

권장되는 분리 예:

- Factory: `GameObject`, Collider, Rigidbody, Tilemap 같은 Unity 표현 생성
- Strategy: 시설 효과, 전투 효과, AI 점수 계산처럼 교체 가능한 행동
- Service: 건설, 삭제, 합성, 연구, 물류 같은 유스케이스 오케스트레이션
- Adapter: Unity API나 싱글톤 접근을 하위 로직에서 격리
- Event/Observer: 건물 변경, 피해 발생, 전투 로그, UI 갱신 통지
- State: 건설 모드, 파괴 모드, 침입 이벤트 진행 상태

좋은 의존 방향:

```text
DungeonStory 통합 계층
-> 재사용 가능한 Unity Grid 에셋 코어
```

주의할 방향:

```text
재사용 가능한 Grid 도메인 코어
-> GridSystemManager.Instance
-> Unity Scene Object
-> BuildingSO / BuildableObject
-> UI
```

하위 API가 게임 전체를 알아야 한다면, 그 API는 아직 너무 높은 책임을 가지고 있는 것이다.

## 리팩터링 기준

- 기존 동작을 깨지 않는 작은 단계로 분리한다.
- 한 번에 프리팹 구조, 데이터 구조, 런타임 생성 방식을 모두 바꾸지 않는다.
- 컴파일 가능한 상태를 자주 확인한다.
- 사용자가 건드린 변경 사항은 되돌리지 않는다.
- 변경 후 남은 결합 지점을 짧게 보고하고, 다음 분리 후보를 제안한다.
