# P1 3.2 초반 수급 구현 보고

## 목표

초반 경영 루프가 `돈으로 재고를 사고 -> 창고에 넣고 -> 직원이 시설로 보충하고 -> 손님이 소비해 돈을 번다`로 이어지게 만드는 것이 목표다.

이번 단계에서는 운영일 정산 UI까지 만들지 않고, 정산/이벤트 UI가 소비할 수 있는 결과 데이터와 이벤트를 먼저 만들었다.

## 구현 내용

### 창고 용량

`WarehouseInventory`에 최대 용량과 남은 용량을 추가했다.

- `MaxCapacity`: 창고 최대 수용량
- `RemainingCapacity`: 현재 남은 공간
- `Deposit`: 남은 공간 안에서만 입고
- `CanStore`: 입고 가능 여부 확인

기존 창고 초기 재고는 `CreateSeeded`에서 최대 용량만큼 자동 배분된다.

### 운영일 납품

`StockSupplyService.CreateDailyDeliveryOffers(day)`를 추가했다.

운영일마다 다음 재고 계열의 구매 제안을 만들 수 있다.

- 식재료
- 잡화
- 무기
- 마력

일차가 올라가면 소량 증가하는 구조만 넣었다. 난이도, 상인 등급, 평판 보정은 이후 확장 지점이다.

### 돈으로 재고 구매

`StockSupplyService.TryPurchaseDelivery`를 추가했다.

성공 조건:

- 납품 제안이 유효해야 한다.
- 보유 자금이 충분해야 한다.
- 창고 전체 남은 공간이 충분해야 한다.

초기 구현은 부분 입고를 허용하지 않는다. 창고 공간이 부족하면 구매 전체가 실패한다. 초반 플레이어 입장에서는 `돈은 있는데 창고가 꽉 차서 못 받았다`는 원인이 명확한 편이 낫기 때문이다.

### 침입 보상 입고

`StockSupplyService.GrantReward`를 추가했다.

침입 방어 보상, 이벤트 보상, 오펜스 보상은 모두 이 API로 창고에 재고를 넣을 수 있다. 비용은 0으로 기록된다.

### 내부 생산 확장 슬롯

`StockProductionRule`과 `StockSupplyService.RunInternalProduction`을 추가했다.

첫 버전에서는 내부 생산 시설을 자동으로 돌리지 않는다. 다만 나중에 농장, 마력 생성기, 제련소 같은 생산 시설이 생기면 같은 입고 경로를 사용하게 할 수 있다.

### 결과 표시용 이벤트

`StockSupplyResult`와 `StockSupplyEvent`를 추가했다.

구매, 보상, 생산 결과는 모두 `StockSupplyResult`로 표현된다.

기록되는 값:

- 성공 여부
- 재고 계열
- 요청 수량
- 실제 입고 수량
- 비용
- 출처
- 실패 원인

이 이벤트는 이후 `4.1 운영일 정산`, `4.3 이벤트 알림`에서 우하단 버튼/상세 패널로 연결하면 된다.

### 시설 요약 UI

창고 요약 UI는 이제 `현재 재고 / 최대 용량`을 보여준다.

## 구현 로직

수급은 다음 순서로 처리된다.

```text
운영일 납품 제안 생성
-> 플레이어가 구매 선택
-> 자금 확인
-> 창고 공간 확인
-> 돈 차감
-> 창고에 계열 재고 입고
-> StockSupplyEvent 발행
```

방어 보상과 내부 생산은 돈 차감만 빠지고 같은 입고 경로를 사용한다.

```text
보상/생산 결과 발생
-> 창고 공간 확인
-> 창고에 계열 재고 입고
-> StockSupplyEvent 발행
```

## 검증

Unity MCP로 P1 회귀 시나리오를 실행했다.

결과:

```text
Facility=True
Customer=True
Character=True
Owner=True
WorkPriority=True
Command=True
StaffDuty=True
```

추가된 시설 시나리오:

- 운영일 납품 제안 생성
- 돈으로 창고 재고 구매
- 자금 부족 실패
- 창고 공간 부족 실패
- 침입 방어 보상 재고 입고
- 내부 생산 확장 슬롯 입고

콘솔 에러는 0개다.

남아 있는 경고는 기존 경고다.

- `UIBuildingInfo.cs` unreachable code
- Behavior Designer deprecated API 경고

## 다음 연결 지점

다음 단계는 `4.1 운영일 정산`이다.

연결 방식:

- 운영일이 끝날 때 `CreateDailyDeliveryOffers`로 납품 목록 생성
- 플레이어 구매 선택 시 `TryPurchaseDelivery` 호출
- `StockSupplyEvent`를 우하단 이벤트 버튼에 누적
- 버튼 클릭 시 구매/입고/실패 상세를 보여줌

이 단계가 붙으면 초반 경영 루프가 화면에서 더 명확하게 보인다.
