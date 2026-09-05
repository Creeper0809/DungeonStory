# P2 8.3 반란 대응 구현 보고

## 목표

직원 반란 대응도 직접 전투 조작이 아니라 자동 AI 목표 지정으로 처리한다.

이번 단계에서는 자동 제압, 우선 제압 명령, 격리, 협상/진정 API를 구현했다.

## 구현 파일

- `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs`
- `Assets/Scripts/Character/Work/WorkPriorityProfile.cs`
- `Assets/Scripts/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/Character/AI/Editor/StaffRebellionResponseDebugScenarios.cs`
- `docs/game-design-todo.md`

## 자동 제압

국지 반란이 발생하면 `StaffDiscontentRuntime.DispatchAutoSuppress`가 실행된다.

흐름:

```text
직원 기분 악화
-> 국지 반란 단계 진입
-> 경비 작업이 켜진 직원 탐색
-> AbilityWork.TrySetPrioritySuppressTarget 호출
-> AIWork가 제압 목적지를 잡고 이동/공격
```

즉, 플레이어가 직접 전투를 조작하지 않아도 경비 역할의 직원이 반란 대상에게 자동으로 붙는다.

## 제압 명령

`WorkCommandResolver.IsSuppressTarget`이 이제 두 대상을 제압 대상으로 본다.

```text
침입자
국지 반란 직원
```

따라서 기존 우클릭 명령 흐름을 그대로 재사용한다.

```text
OwnerCommandController
-> WorkCommandResolver.TryResolveSuppressCommand
-> AbilityWork.TrySetPrioritySuppressTarget
```

제압 중 대상이 사망하면 `AbilityWork`가 대상 종류에 따라 처리한다.

```text
침입자: InvasionIntruderRuntime.ResolveSuppressedBy
반란 직원: StaffDiscontentRuntime.ResolveSuppressedRebel
```

## 격리

`StaffDiscontentRuntime.TryIsolateRebel`을 추가했다.

격리 성공 시:

- 반란 직원은 격리 상태가 된다.
- 영구 손실 상태는 유지된다.
- 사장 위협 확산은 막힌다.
- 이벤트 알림에 격리 결과가 남는다.

격리는 반란을 되돌리는 기능이 아니라, 피해 확산을 막는 대응이다.

## 협상/진정

`StaffDiscontentRuntime.TryCalmStaff`를 추가했다.

진정은 반란 직전 단계에서만 의미가 있다.

대상:

```text
만족도 낮음
효율 저하
태업/결근
```

이미 이탈했거나 국지 반란으로 영구 손실 상태가 된 직원은 정상 직원으로 되돌리지 않는다.

진정 성공 시:

- 기분이 회복된다.
- 저기분 누적일이 초기화된다.
- 불만 단계가 다시 계산된다.

## 이벤트

추가 이벤트:

- `StaffRebellionResponseEvent`

응답 종류:

```text
AutoSuppress
SuppressCommand
Isolate
Calm
```

## 검증 시나리오

`StaffRebellionResponseDebugScenarios`를 추가했다.

Unity 메뉴:

```text
DungeonStory/Debug/Character/Run P2 Staff Rebellion Response Scenarios
```

검증 항목:

- 국지 반란 발생 시 경비 직원에게 자동 제압 목표가 배정된다.
- 반란 직원이 우클릭 제압 명령 대상으로 인식된다.
- 격리하면 사장 위협 확산이 차단된다.
- 반란 직전 직원은 진정으로 단계가 내려간다.
- 제압 완료 후 반란 대상에서 해제된다.

## 검증 상태

현재 작업 시점에는 Unity Editor 프로세스가 종료되어 있어 Unity MCP 검증을 완료하지 못했다.

MCP 실행 시도 결과:

```text
Named pipe socket file not found: /tmp/unity-mcp-66aef8ce-177337
```

확인된 상태:

- `git diff --check` 기준 신규 whitespace 오류 없음
- 반란 대상 판정은 기존 우클릭 제압 명령 흐름과 연결됨
- 자동 제압은 경비 작업 우선순위와 `AbilityWork` 제압 루틴을 재사용함
- Unity Editor가 다시 열리면 `StaffRebellionResponseDebugScenarios.RunAll(false)`를 먼저 실행하면 된다.
