# P1 6.1 상점과 기본 구매 구현 보고

## 목표

운영일 정산 이후 시설/설계도 상점 목록이 갱신되고, 운에 막히지 않도록 연구로 해금되는 기본 구매 목록의 기반을 만든다.

6.1의 핵심은 UI 완성이 아니라 규칙과 데이터 흐름이다.

```text
운영일 종료
-> 다음 날 시설 상점 목록 생성
-> 시설/설계도 상품 표시 가능
-> 구매 시 돈 차감
-> 시설은 건설 가능 상태로 해금
-> 설계도는 획득 상태로 기록
```

## 구현 파일

- `Assets/Scripts/FacilityShop/FacilityBlueprintSO.cs`
- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`
- `Assets/Scripts/FacilityShop/Editor/P1FacilityShopAssetBuilder.cs`
- `Assets/Scripts/FacilityShop/Editor/FacilityShopDebugScenarios.cs`
- `Assets/Resources/SO/Blueprint/P1/*.asset`
- `Assets/Scripts/Operation/OperatingDaySettlement.cs`
- `Assets/Scripts/GameManager.cs`
- `docs/game-design-todo.md`

## FacilityBlueprintSO

`FacilityBlueprintSO`는 설계도 데이터다.

설계도는 합성 재료가 아니다. 연구시설에서 분석해서 새 기본 구매 항목이나 조합식을 여는 정보 아이템이다.

데이터:

```text
blueprintName
description
rarity
defaultCost
researchWorkRequired
unlockBuildingIds
unlockBasicPurchaseBuildingIds
unlockRecipeIds
```

6.1에서는 설계도 구매와 보유 상태까지만 처리한다. 실제 연구 작업과 분석 완료 처리는 6.2에서 연결한다.

## P1 설계도 에셋

P1 설계도 에셋은 `P1FacilityShopAssetBuilder`가 생성한다.

생성된 설계도:

```text
상업 기초 설계도
방어 기초 설계도
지원 기초 설계도
마력 기초 설계도
전투 식당 설계도
연쇄 함정 설계도
```

일반 설계도는 낮은 별 시설의 기본 구매 해금을 위한 데이터이고, 희귀 설계도는 이후 특수 조합식 연구로 이어질 자리다.

## 일일 시설 상점

`FacilityShopService.CreateDailyOffers(day)`가 하루 판매 목록을 만든다.

초기 규칙:

```text
시설 상품 3개
일반 설계도 1개
희귀 설계도 확률 등장
```

랜덤은 `day`를 seed로 사용해 같은 날에는 같은 목록이 나오게 했다. 이렇게 하면 테스트와 저장/로드 확장이 쉬워진다.

시설 상품 후보:

```text
이동 시설 제외
벽 제외
1성/2성까지만 후보
```

3성 이상은 일일 상점과 기본 구매에서 제외한다. 이 등급은 합성, 침입 방어 보상, 오펜스 보상 쪽에 남겨둔다.

## 기본 구매 목록

`FacilityShopUnlockState`가 기본 구매 해금 상태를 가진다.

주요 API:

```text
UnlockBasicPurchase(BuildingSO building)
IsBasicPurchaseUnlocked(BuildingSO building)
MarkBlueprintAcquired(FacilityBlueprintSO blueprint)
IsBlueprintAcquired(FacilityBlueprintSO blueprint)
```

기본 구매 목록은 `FacilityShopService.CreateBasicPurchaseOffers`로 만든다.

규칙:

```text
1성 시설: 기본 구매 가능
일부 2성 시설: 기본 구매 가능
3성 이상: 기본 구매 제외
```

6.2 연구 시스템은 설계도 분석 완료 시 `UnlockBasicPurchase`를 호출하면 된다.

## 구매 처리

`FacilityShopService.TryPurchaseOffer`가 구매를 처리한다.

공통:

```text
상품 유효성 확인
자금 확인
돈 차감
구매 결과 이벤트 발생
```

시설 구매:

```text
BuildingSO.unlocked = true
```

현재 프로젝트의 건설 탭은 이미 `BuildingSO.unlocked`를 보고 버튼을 만든다. 그래서 P1에서는 시설 인벤토리 없이 구매한 시설을 곧바로 건설 가능 상태로 만든다.

설계도 구매:

```text
FacilityShopUnlockState에 설계도 획득 기록
EventAlertService.RaiseBlueprintAcquired 호출
```

설계도 획득은 즉시 해금이 아니라 연구시설 분석으로 이어질 상태만 만든다.

## 운영일 정산 연결

`DailyFacilityShopRuntime`이 `OperatingDayEndedEvent`를 받아 다음 날 상품 목록을 갱신한다.

`GameManager.EnsureOperatingDaySystems()`에서 자동으로 붙는다.

또한 `OperatingDayReport`에 다음 항목을 추가했다.

```text
refreshedFacilityShopOffers
```

정산 상세에는 `시설 상점 갱신` 목록이 표시된다.

## 왜 이렇게 구현했나

### 시설 인벤토리를 만들지 않은 이유

궁극적으로는 시설을 구매해서 보관하고, 배치하거나 합성 재료로 쓰는 인벤토리가 필요할 수 있다.

하지만 현재 건설 시스템은 `BuildingSO.unlocked` 기반이다. 여기서 바로 시설 인벤토리를 만들면 건설, 합성, 상점 UI를 동시에 바꿔야 한다.

그래서 6.1에서는 다음처럼 좁혔다.

```text
상점 구매 = 건설 가능 해금
설계도 구매 = 연구 대기 정보 획득
```

이 구조는 현재 구현과 맞고, 6.2 연구/6.3 합성에서 인벤토리나 조합식 데이터가 필요해질 때 확장할 수 있다.

### 희귀 상품을 설계도 중심으로 둔 이유

1성 시설이 계속 안 나오는 것은 재미보다 스트레스가 크다.

반대로 희귀 설계도는 빌드 방향을 바꾸는 선택지가 될 수 있다. 그래서 랜덤성은 희귀 시설/특수 설계도 쪽에 남기고, 기본 시설은 연구로 보정하는 방향을 유지했다.

## 검증

Unity MCP에서 6.1 전용 시나리오를 실행했다.

```text
DungeonStory/Debug/Facility Shop/Run P1 Facility Shop Scenarios
```

검증 내용:

- 일일 상점에 시설과 설계도가 함께 나온다.
- 희귀 상품은 어떤 날에는 나오고 어떤 날에는 나오지 않는다.
- 연구 상태에 들어간 1성 시설은 기본 구매 목록에 들어간다.
- 일부 2성 시설은 기본 구매 목록에 들어갈 수 있다.
- 3성 시설은 기본 구매 목록에서 제외된다.
- 시설 구매는 돈을 차감하고 건설 가능 상태로 만든다.
- 설계도 구매는 돈을 차감하고 설계도 획득 알림을 발생시킨다.
- 운영일 종료 후 상점 갱신 이벤트가 발생한다.
- 운영일 정산 보고서에 시설 상점 갱신 목록이 들어간다.

실행 결과:

```text
FacilityShop=True
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
```

Unity 콘솔 에러/워닝:

```text
0
```
