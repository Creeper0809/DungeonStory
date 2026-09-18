# 일반 생산 ability 7시설의 물리 stock 출력 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingProductionAbility` 작성 시설은 D01 간이 화덕, D02 고기 그릴, I16 전기 제련 도가니, 마력 계열 M01·M02·M04, Q02 연금술 작업대의 7개다.
- 이 ability는 일반 생산 bill의 recipe output과 별개로, Operate 또는 Research work 완료 때 category별 물리 item stock을 입고한다.
- 공개 시설 페이지와 생산 가이드는 역할명만 설명하며 category·기본량·수량식·창고 우선순위·실패/부분 입고·누적 저장 상태를 설명하지 않는다. 기존 감사 항목도 이 generic output 계약의 owner가 아니다.

## 작성값

| 시설 | facility ID | output category | 기본량 |
| --- | ---: | --- | ---: |
| D01 간이 화덕 | 1000 | Food | 4 |
| D02 고기 그릴 | 1001 | Food | 6 |
| I16 전기 제련 도가니 | 9825 | Material | 1 |
| M01 마력 수정 선반 | 1036 | Mana | 2 |
| M02 마력 저장조 | 1037 | Mana | 4 |
| M04 의식 초점석 | 1039 | Mana | 5 |
| Q02 연금술 작업대 | 1031 | Mana | 3 |

- ability 자체는 `amount > 0`일 때만 유효하며 각 시설별 state module을 생성하고 해당 category를 signal로 낸다.

## 완료 수량과 입고 경로

- 실행 work type은 정확히 Operate 또는 Research뿐이다. 다른 work completion은0을 반환한다.
- 요청량은 `ceil(max(0, 기본량) × 작업자 ProductionOutputMultiplier × max(0.05, evolutionOutputMultiplier)) + StockProductionBonus`다.
- 작업자의 ProductionOutputMultiplier는 management `output` bonus를0..3으로 clamp한 뒤 `1 + bonus`, 즉 1..4배다. StockProductionBonus는 management `stock` bonus를 반올림한 0 이상 정수다.
- 입고는 먼저 source와 같은 usable room의 warehouse들을 순회하고, 남은 수량이 있을 때만 grid 전체 usable warehouse를 다시 순회한다. usable은 warehouse inventory가 존재하고 warehouse inventory를 가진 경우다.
- 유효 창고가 없거나 수용량이 없으면 출력은0이다. 일부만 받아들일 수 있으면 그 실제 입고량만 출력으로 인정하고 나머지는 생성하지 않는다.

## category에서 구체 item으로

- warehouse 입고는 해당 StockCategory이고 `MaxStack > 1`인 authored item 중 `ItemId` 오름차순 첫 item을 선택한다. 따라서 이 ability가 item ID를 직접 작성하는 것이 아니라 category의 현재 authored catalog가 구체 output을 결정한다.
- 선택한 item의 warehouse mass-admission이 요청량 전부를 받지 못하면 `SpawnStock`은 false를 반환하지만, 호출 측은 반환 bool 대신 `spawned` 실제량을 합산한다. 부분 수용 시 부분량만 남는다.
- 물리 item runtime이 주입되지 않은 시설에서 생산을 시도하면 예외다. abstract counter만 증가시키는 대체 경로는 없다.

## 저장·활동 기록과 공개 경계

- 실제 입고량은 ability ID별 `BuildingProductionStateModule.producedStock`에0..`int.MaxValue` 범위로 누적된다. module version은1이며 JSON capture/restore 대상이다.
- 같은 실제량은 activity `stock:produce`의 quantity와 category 메시지로도 기록된다. 요청량이나 미입고 잔량은 이 state/activity에 기록되지 않는다.
- 공개 building-1000/1001/9825/1031/1036/1037/1039은 분류·크기·건설비와 포괄적인 생산/보급 역할을 표시할 뿐 위 표와 수량식, warehouse fallback, 결정적 item 선택, 부분 입고 및 saved cumulative output을 노출하지 않는다.

## 정적 검증 경계

- ability 정의·core adapter·runtime effects·작업자 modifier·warehouse spawn·state module과 일곱 작성 asset, 일곱 공개 entity, production guide를 읽었다.
- 실제 warehouse 후보 순서의 scene 배치별 결과, mass-admission 부분 수용, management/evolution 조합, 저장 왕복 및 UI activity 노출은 Play Mode에서 재현하지 않았다.
