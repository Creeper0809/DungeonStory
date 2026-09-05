# P2 8.1 단골과 영입 구현 보고

## 목표

좋은 고객층을 만드는 일이 곧 좋은 인력 풀을 만드는 흐름을 만든다.

이번 단계에서는 손님 방문 기록, 평균 만족도, 단골화, 영입 후보화, 영입 후 손님 풀 제외까지 구현했다.

## 구현 파일

- `Assets/Scripts/Recruitment/RegularCustomerSystem.cs`
- `Assets/Scripts/Recruitment/Editor/RegularCustomerDebugScenarios.cs`
- `Assets/Scripts/Character/CharacterSpawner.cs`
- `Assets/Scripts/GameManager.cs`
- `docs/game-design-todo.md`

## 핵심 구조

### RegularCustomerState

단골 시스템의 실제 상태를 가진다.

기록 단위는 현재 스폰 구조에 맞춰 `CharacterSO.id`를 사용한다.

기록 값:

```text
손님 ID
표시 이름
종족 태그
방문 횟수
평균 만족도
단골 여부
영입 후보 여부
영입 완료 여부
영입 후 가능 역할
```

현재 가능 역할은 다음 세 가지를 모두 가진다.

```text
직원
방어
원정
```

실제 배치 UI와 로스터 UI는 후속 단계에서 연결한다.

### RegularCustomerRuntime

`FacilityVisitEvent`를 듣고 손님 방문 기록을 갱신한다.

기본 기준:

```text
단골:
방문 3회 이상
평균 만족도 65 이상

영입 후보:
방문 4회 이상
평균 만족도 75 이상
```

단골이 되거나 영입 후보가 되면 이벤트 알림을 띄운다.

이벤트:

- `RegularCustomerUpdatedEvent`
- `RegularCustomerBecameRegularEvent`
- `RecruitCandidateDiscoveredEvent`
- `CustomerRecruitedEvent`

### 영입 처리

`RegularCustomerRuntime.TryRecruit(customerId, out result)`로 영입한다.

영입 성공 시:

- 단골 기록 상태가 `Recruited`가 된다.
- `CustomerRecruitedEvent`가 발생한다.
- 이벤트 알림에 영입 완료가 기록된다.
- 해당 `CharacterSO.id`는 다시 손님으로 스폰되지 않는다.

`CharacterSpawner`는 스폰 전에 `RegularCustomerService.CanSpawnAsCustomer`를 확인한다.

즉, 영입은 단순 보너스가 아니라 소비 고객 하나를 실제로 잃는 선택이 된다.

## 설계 판단

완전한 개체 단위 영속 고객 풀은 아직 없다.

현재 `CharacterSpawner`는 `CharacterSO[]`를 반복 스폰하는 구조라서, 이번 단계에서는 `CharacterSO.id`를 하나의 고객 카드처럼 취급한다.

이 방식의 장점:

- 기존 스폰 구조를 크게 바꾸지 않는다.
- 단골/영입 시스템을 바로 이벤트에 연결할 수 있다.
- 후속 고객 풀 시스템이 생겨도 상태 구조를 재사용할 수 있다.

주의점:

- 같은 `CharacterSO`를 여러 개체처럼 스폰하는 구조로 바뀌면, 고객 식별자를 별도 런타임/저장 ID로 분리해야 한다.

## 검증 시나리오

`RegularCustomerDebugScenarios`를 추가했다.

Unity 메뉴:

```text
DungeonStory/Debug/Recruitment/Run P2 Regular Customer Scenarios
```

검증 항목:

- 방문 횟수와 평균 만족도 기록
- 단골 기준 달성
- 영입 후보 기준 달성
- 영입 성공 후 손님 재스폰 제외
- 낮은 만족도 손님은 단골 제외
- `RegularCustomerRuntime` 이벤트 연결

## 검증 상태

현재 작업 시점에는 Unity Editor 프로세스가 종료되어 있어 Unity MCP 검증을 완료하지 못했다.

MCP 실행 시도 결과:

```text
Named pipe socket file not found: /tmp/unity-mcp-66aef8ce-177337
```

확인된 상태:

- 코드 구조는 기존 `FacilityVisitEvent`와 `EventAlertService`를 재사용한다.
- `CharacterSpawner`는 영입된 고객을 다시 손님으로 스폰하지 않도록 연결했다.
- Unity Editor가 다시 열리면 `RegularCustomerDebugScenarios.RunAll(false)`를 먼저 실행하면 된다.
