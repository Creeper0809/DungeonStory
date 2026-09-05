# P1 3.1 재고 계열 구현 보고

## 목표

`docs/game-design-todo.md`의 `3.1 재고 계열`을 구현했다.

이번 단계의 목표는 상점이 무한히 물건을 파는 구조에서 벗어나, 시설 내부 재고와 창고 재고를 분리하는 것이다.

```text
창고 재고
-> 직원 보충 작업
-> 시설 내부 재고
-> 손님/직원 이용으로 소비
```

## 구현 요약

추가/확장된 축:

- 재고 계열 `StockCategory`
- 판매 아이템의 재고 계열
- 런타임 창고 인벤토리 `WarehouseInventory`
- 창고 시설 판정 `IWarehouseFacility`
- 상점 내부 재고 보충 API
- `Restock` 작업의 실제 보충 처리
- 시설 요약 UI의 재고/보충 요청 표시

## 재고 계열

`SaleItem`에 재고 계열을 추가했다.

파일:

```text
Assets/Scripts/Buildings/SO/SaleItem.cs
```

초기 계열:

```text
Food
General
Weapon
Mana
```

현재 P1 아이템 연결:

```text
햄버거 -> Food
도란검 -> Weapon
도란방패 -> Weapon
```

`General`, `Mana`는 아직 실제 판매 아이템이 적지만, 창고와 보충 구조에서는 계열로 존재한다. 잡화/마력 아이템이 추가되면 같은 필드로 연결한다.

## 창고 재고

창고는 `WarehouseInventory`를 가진다.

파일:

```text
Assets/Scripts/Buildings/SO/StockInfo.cs
Assets/Scripts/Buildings/IInteractable.cs
Assets/Scripts/Buildings/Facility.cs
```

창고 판정:

```text
FacilityRole.Logistics 포함
internalStockMax > 0
```

이 조건을 만족하는 시설은 초기화 시 런타임 창고 재고를 만든다.

현재 `P1_Warehouse`는 `internalStockMax = 80`이므로 계열별 기본 재고를 자동 배분한다.

```text
식재료: 40%
잡화: 25%
무기: 25%
마력: 나머지
```

이 배분은 P1 검증용 기본값이다. 이후 원재료 구매/원정/일일 상점이 생기면 창고 입고 루프로 대체한다.

## 시설 내부 재고

상점은 기존처럼 내부 재고를 가진다.

파일:

```text
Assets/Scripts/Buildings/Shop.cs
```

핵심 값:

```text
CurrentStock
MaxInternalStock
MissingStock
NeedsRestock
```

손님과 비번 직원이 상점을 이용하면 상점 내부 재고만 감소한다.

상점 이용은 창고 재고를 직접 소비하지 않는다.

## 직원 보충 작업

`AbilityWork`의 `Restock` 작업에 실제 보충 처리를 연결했다.

파일:

```text
Assets/Scripts/Character/Ability/AbilityWork.cs
```

흐름:

```text
직원이 재고 부족 상점을 작업 대상으로 선택
-> 상점까지 이동
-> 도달 가능한 창고 목록 수집
-> 상점 판매 아이템의 StockCategory 확인
-> 창고 계열 재고 차감
-> 상점 내부 재고 증가
-> 작업 종료
```

보충은 1회성 작업이다.

즉 `Operate`처럼 시설에 계속 서 있는 작업이 아니라, 필요한 양을 옮긴 뒤 AI가 다음 행동을 다시 판단한다.

## 후보 제외와 실패 원인

기존 방문 후보 제거 규칙은 유지된다.

```text
상점 내부 재고 0
-> CanVisit 실패
-> 실패 원인: 재고 없음
-> 손님/직원 식사/구매 후보에서 제외
```

보충할 창고 재고가 없으면 상점 내부 재고가 채워지지 않고 로그가 남는다.

```text
보충 실패: 창고 재고 부족
```

## UI 표시

시설 요약 UI에 재고 정보를 보강했다.

파일:

```text
Assets/Scripts/UI/BuildingSummaryInfo.cs
```

상점 표시:

```text
남은 재고 : 현재 / 최대
보충 요청
```

창고 표시:

```text
창고 재고 : 총 재고
```

이번 단계에서는 총량만 표시한다. 계열별 상세 표시는 정산/창고 UI 단계에서 확장한다.

## 검증

확장한 검증 파일:

```text
Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs
```

검증 항목:

- P1 시설/재고 에셋 수가 유지된다.
- 재고 0 상점은 방문 후보에서 제외된다.
- 판매 아이템에 올바른 `StockCategory`가 들어간다.
- 창고 시설은 런타임 `WarehouseInventory`를 가진다.
- 창고 총 재고가 `internalStockMax`와 일치한다.
- 창고 계열 재고로 상점 내부 재고를 보충한다.
- 보충 시 창고 계열 재고가 실제로 감소한다.

Unity MCP 검증 결과:

```text
P1 regression passed. Facility=True, Customer=True, Character=True, Owner=True, WorkPriority=True, Command=True, StaffDuty=True, Inventory=True
```

## 남은 연결 지점

이번 단계에서 남긴 부분:

- 돈으로 원재료를 사서 창고에 입고하는 루프
- 일일 정산에서 재고 부족/창고 잔량 표시
- 창고 UI의 계열별 상세 표시
- 직원이 창고까지 갔다가 상점으로 이동하는 2단계 운반 애니메이션
- 마력/잡화 판매 아이템 추가
- 침입 방어 보상으로 창고 재고를 지급하는 루프

이 부분은 `3.2 원재료 수급`, `4. 일일 정산`, `5. 침입과 방어` 단계에서 이어서 연결한다.
