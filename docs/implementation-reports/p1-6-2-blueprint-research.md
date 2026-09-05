# P1 6.2 연구시설과 설계도 구현 보고

## 목표

상점에서 산 설계도가 바로 해금되지 않고, 연구실에서 직원이 분석해야 새 기본 구매 목록이나 특수 조합식으로 이어지게 만든다.

핵심 흐름:

```text
설계도 구매
-> 연구 대기열 등록
-> 직원이 연구실에서 연구 작업 수행
-> 진행도 누적
-> 연구 완료
-> 기본 구매 시설 또는 조합식 해금
```

## 구현 파일

- `Assets/Scripts/Research/BlueprintResearchSystem.cs`
- `Assets/Scripts/Research/Editor/BlueprintResearchDebugScenarios.cs`
- `Assets/Scripts/FacilityShop/FacilityBlueprintSO.cs`
- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`
- `Assets/Scripts/FacilityShop/Editor/P1FacilityShopAssetBuilder.cs`
- `Assets/Scripts/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/Buildings/BuildableObject.cs`
- `Assets/Scripts/GameManager.cs`
- `docs/game-design-todo.md`

## 설계도 데이터

`FacilityBlueprintSO`에 연구 필요량을 추가했다.

```text
researchWorkRequired
```

P1 설계도는 `P1FacilityShopAssetBuilder`가 생성/갱신한다.

일반 설계도는 기본 구매 목록을 열고, 희귀 설계도는 조합식 ID를 연다.

## 연구 상태

`BlueprintResearchState`가 연구 진행 상태를 가진다.

관리하는 값:

```text
연구 작업 목록
완료된 설계도 ID
해금된 조합식 ID
```

각 연구는 `BlueprintResearchTask`로 표현한다.

```text
Blueprint
Progress
RequiredWork
ProgressRatio
IsCompleted
```

진행도는 작업을 조금 하다가 멈춰도 사라지지 않는다. 다음 연구 작업이 다시 같은 태스크에 진행도를 더한다.

## 연구 런타임

`BlueprintResearchRuntime`은 `GameManager`에 자동으로 붙는다.

역할:

- 설계도 구매 이벤트 수신
- 연구 대기열 등록
- 연구실 작업 진행도 적용
- 연구 완료 시 해금 처리
- 연구 완료 이벤트 알림 발생

설계도 구매 시에는 `FacilityShopPurchasedEvent`를 통해 연구 대기열에 들어간다.

## 직원 연구 작업

`AbilityWork`에 `FacilityWorkType.Research` 실행을 연결했다.

규칙:

- 연구할 설계도가 없으면 연구실은 작업 후보가 아니다.
- 연구할 설계도가 있으면 연구실이 작업 후보가 된다.
- 직원이 연구실에 도착하면 1초 단위로 연구 진행도를 올리고 행동을 종료한다.
- 아직 완료되지 않았으면 다음 AI 판단에서 다시 연구를 이어갈 수 있다.

연구 작업량은 다음 값을 사용한다.

```text
기본 연구량
* 캐릭터 연구 작업 속도
* 연구 시설 보정
```

캐릭터 연구 작업 속도에는 이미 구현된 다음 값이 포함된다.

```text
연구 능력치
종족 보정
개인 특성 보정
피로/부상 효율
```

## 시설 보정

`BlueprintResearchService.GetFacilityResearchMultiplier`가 연구 시설 보정을 계산한다.

초기 규칙:

```text
연구 역할 시설: 연구 속도 증가
마력 역할도 함께 가진 시설: 추가 보정 가능
필요 근무자 수가 있는 시설: 소폭 보정 가능
```

현재 P1 연구실은 연구 역할 보정을 받는다.

## 해금 처리

연구 완료 시 `BlueprintResearchService.ApplyCompletion`이 해금을 적용한다.

가능한 해금:

- `unlockBuildingIds`: 시설 자체를 건설 가능 상태로 만든다.
- `unlockBasicPurchaseBuildingIds`: 기본 구매 목록에 추가한다.
- `unlockRecipeIds`: 연구 상태의 조합식 ID 목록에 기록한다.

이렇게 해서 일반 설계도는 운 보정용 기본 구매 해금이 되고, 희귀 설계도는 이후 합성 시스템의 조합식 해금으로 연결된다.

## 이벤트

추가 이벤트:

```text
BlueprintResearchQueuedEvent
BlueprintResearchProgressEvent
BlueprintResearchCompletedEvent
```

플레이어 알림:

```text
연구 대기
연구 완료
```

연구 완료 알림은 우측 이벤트 알림 시스템에 기록된다.

## 왜 이렇게 구현했나

### 설계도 구매와 해금을 분리한 이유

설계도를 사는 순간 바로 시설이 열리면 연구시설의 존재 이유가 약해진다.

그래서 설계도는 `정보 아이템`이고, 실제 해금은 연구실에서 분석해야 발생하게 했다.

### 연구실이 빈 작업 후보가 되지 않게 한 이유

연구할 설계도가 없는데 직원이 연구실에 가면 플레이어 입장에서는 멈춘 것처럼 보인다.

그래서 연구 대기열이 있을 때만 연구실을 작업 후보로 인정한다.

### 1초 단위 연구 작업으로 만든 이유

긴 코루틴 하나가 연구를 끝까지 붙잡으면 중간에 스케줄, 피로, 긴급 작업 전환을 넣기 어렵다.

P1에서는 짧은 연구 작업을 반복하게 해서 다음 확장을 쉽게 했다.

## 검증

Unity MCP에서 6.2 전용 시나리오를 실행했다.

```text
DungeonStory/Debug/Research/Run P1 Blueprint Research Scenarios
```

검증 내용:

- 설계도에 연구 필요량이 있다.
- 설계도 구매 시 연구 대기열에 들어간다.
- 연구 진행도는 중단 후에도 유지된다.
- 연구 완료 시 기본 구매 목록이 해금된다.
- 연구 완료 시 조합식 ID가 해금된다.
- 연구 능력/종족/특성/시설 보정이 연구 속도 차이를 만든다.
- 연구할 설계도가 없으면 연구실은 작업 후보가 아니다.
- 연구할 설계도가 있으면 연구실은 작업 후보가 된다.

실행 결과:

```text
BlueprintResearch=True
```

전체 P1 회귀 검증도 통과했다.

```text
Grid=True
Facility=True
Customer=True
Character=True
Owner=True
WorkPriority=True
Command=True
StaffDuty=True
Operation=True
Feedback=True
Alerts=True
InvasionThreat=True
InvasionIntruder=True
Defense=True
CombatReport=True
FacilityShop=True
BlueprintResearch=True
```

Unity 콘솔 에러/경고:

```text
0 errors, 0 warnings
```
