# P2 8.2 직원 불만 단계 구현 보고

## 목표

직원 관리 실패가 외부 침입과 다른 내부 위협으로 느껴지게 한다.

이번 단계에서는 직원 기분이 낮아질 때 단순 불만 로그에서 끝나지 않고, 효율 저하, 태업/결근, 이탈, 국지 반란으로 단계화되도록 구현했다.

## 구현 파일

- `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs`
- `Assets/Scripts/Character/AI/Editor/StaffDiscontentDebugScenarios.cs`
- `Assets/Scripts/Character/Core/Character.cs`
- `Assets/Scripts/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/GameManager.cs`
- `docs/game-design-todo.md`

## 단계 구조

직원 불만 단계는 다음과 같다.

```text
Stable
-> LowSatisfaction
-> EfficiencyDrop
-> WorkDisruption
-> Departure
-> LocalRebellion
```

기본 기분 기준:

```text
만족도 낮음: 50 이하
효율 저하: 35 이하
태업/결근: 25 이하
이탈: 15 이하
국지 반란: 8 이하
```

같은 저기분 상태가 여러 날 유지되어도 단계가 올라간다.

## 실제 게임 연결

### 1단계 효율 저하

`Character.GetWorkSpeedMultiplier`에 직원 불만 배율을 곱한다.

기본 배율:

```text
만족도 낮음: 0.95
효율 저하: 0.8
태업/결근: 0.6
이탈/반란: 0
```

즉, 낮은 만족도는 실제 작업 속도와 작업 후보 점수에 영향을 준다.

### 2단계 태업/결근

`AbilityWork.CanStartWorkAction`에서 `StaffDiscontentRuntime.ShouldBlockWork`를 확인한다.

태업/결근 단계부터는 일반 작업을 시작하지 않고 비번/대기 흐름으로 빠진다.

긴급 우선 작업과 제압 명령은 기존 우선 처리 구조를 유지한다.

### 3단계 이탈

이탈 단계가 되면 영구 손실로 기록한다.

처리:

```text
StaffDiscontentRecord.IsPermanentLoss = true
StaffDiscontentRecord.IsDeparted = true
Character.LifecycleState = Despawned
```

### 4단계 국지 반란

국지 반란 단계가 되면 직원은 더 이상 정상 인력이 아니다.

처리:

```text
StaffDiscontentRecord.IsPermanentLoss = true
StaffDiscontentRecord.IsInLocalRebellion = true
```

반란은 바로 사장 캐릭터를 노리는 것이 아니라 `국지 반란: 주변 피해 시작` 로그와 이벤트 알림으로 시작한다.

장기 방치 시 `OwnerThreat` 상태가 켜진다.

## 이벤트

추가 이벤트:

- `StaffDiscontentChangedEvent`
- `StaffPermanentLossEvent`

알림:

- 만족도 낮음
- 효율 저하
- 태업/결근
- 이탈
- 국지 반란
- 반란 확산

알림은 기존 `EventAlertService.RaiseStaffComplaint`를 사용한다.

## 검증 시나리오

`StaffDiscontentDebugScenarios`를 추가했다.

Unity 메뉴:

```text
DungeonStory/Debug/Character/Run P2 Staff Discontent Scenarios
```

검증 항목:

- 만족도 낮음 상태
- 1단계 효율 저하
- 2단계 태업 작업 차단
- 3단계 이탈 영구 손실
- 4단계 국지 반란
- 반란 장기 방치 사장 위협

## 남은 연결

8.3에서 다음 시스템이 붙어야 한다.

- 반란 직원 제압 대상으로 인식
- 자동 제압
- 우선 제압 명령
- 격리
- 협상/진정

이번 단계는 반란 상태와 영구 손실, 사장 위협 확산 훅까지 준비한 상태다.

## 검증 상태

현재 작업 시점에는 Unity Editor 프로세스가 종료되어 있어 Unity MCP 검증을 완료하지 못했다.

MCP 실행 시도 결과:

```text
Named pipe socket file not found: /tmp/unity-mcp-66aef8ce-177337
```

확인된 상태:

- `git diff --check` 기준 신규 whitespace 오류 없음
- 직원 불만 단계는 작업 속도와 작업 시작 가능 여부에 연결됨
- Unity Editor가 다시 열리면 `StaffDiscontentDebugScenarios.RunAll(false)`를 먼저 실행하면 된다.
