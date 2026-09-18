# 생산 보조시설 FuelSupply 선택·저장 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingFacilitySupplyAbility` 작성 시설은 WS08 화덕·가마솥(id1607)과 WS09 벽돌 오븐(id1608) 두 개다.
- 두 시설의 Fuel profile, 생산 지원 연결, 실제 생산 bill 연료 선택, 물리 입력 비용, 저장·복원과 UI를 대조했다.
- 공개 시설 페이지는 `연료 소비, 생산 보조`라는 역할명과 건설비만 표시하고 facts는 분류·크기, relations는 0이다. `production-quality-and-supply`도 연료 경쟁과 보급 경로를 정성적으로만 설명한다.
- 기존 GAP-074는 생산 보조시설의 급배수 필드를 소유하지만 FuelSupply 허용·금지·선택 계약은 소유하지 않는다. 다른 기존 GAP에서도 관련 symbol/source 소유자는 없었다.
- 따라서 구현에서 공개 위키로 빠진 독립 항목을 GAP-175로 등록한다.

## 작성 프로필

두 시설의 profile은 동일하다.

| 필드 | 작성값 | 실행 판정 |
| --- | --- | --- |
| kind | Fuel | 연료값으로 후보를 평가 |
| requiredTags | None | allowed 목록이 비어 있을 때만 쓰므로 현재는 비활성 |
| minimumValue | 2 | FuelValue 2 이상만 허용 |
| bufferCapacity | 24 | semantic snapshot/digest에는 들어가지만 production runtime 제한 소비자 없음 |
| allowedItemIds | log, low-fuel, coal, charcoal, silage, candle | exact allow list |
| forbiddenItemIds | black-powder | allow보다 먼저 거부 |
| priorityItemIds | log → low-fuel → coal → charcoal → silage → candle | 결정론 선택 1순위 |

허용품의 현재 FuelValue는 통나무10, 저급연료6, 석탄20, 숯24, 사일리지4, 양초2로 모두 minimum 2를 통과한다.

## 생산 연결

- WS08은 `support:hearth`이며 cookbench/kitchen-basic 작업대에 최대1개 연결된다.
- WS09는 `support:oven`이며 cookbench 작업대에 최대1개 연결된다.
- 둘 다 `requiresFuel=true`, fallback `resource:log`, `fuelPerCycle=1`이다.
- 조합식의 required support tag가 실제 지원시설로 해결됐을 때만 해당 support의 FuelSupply profile이 생산 입력 비용에 참여한다.

## 선택·고정·물리 비용

1. production bill에 `fuel:<supportNodeId>` 선택값이 있고 해당 item이 아직 profile상 허용되면 재고 유무와 관계없이 그 값을 그대로 재사용한다.
2. 선택값이 없으면 허용 후보 중 현재 material destination을 제외한 가용 재고가 있는 후보 집합을 먼저 사용한다. 하나도 없으면 허용 후보 전체를 사용한다.
3. 후보는 priority index → unitPrice/FuelValue → oldest available stack ID → item ID 순으로 정렬한다. 현재 여섯 후보가 전부 priority 목록에 있으므로 실질적으로 작성 순서가 우선한다.
4. 고른 item ID를 production bill에 저장하고 support의 `fuelPerCycle=1`을 기존 조합식 입력에 더한다. 이후 일반 생산 입력 물류가 그 exact item을 목적지로 운반·소비한다.
5. 선택값은 `ProductionBillStateCodec`의 `selectedSupplies`로 정렬 저장되고 복원 검증을 거친다.

이미 선택된 item의 현재 재고는 재선택 조건이 아니다. 따라서 선택 후 품절되면 다른 허용 연료가 있어도 자동 교체하지 않고 기존 exact item 공급을 기다리는 것이 현재 계약이다.

## UI·공개 위키 경계

- 생산시설 패널은 지원시설 요구에 `물리 연료`만 표시한다.
- 선택된 연료 ID, 허용/금지 목록, 최소 FuelValue, priority, 품절 후 고정 동작은 표시하지 않는다.
- 시설 페이지 1607/1608도 위 값을 제공하지 않는다.
- `bufferCapacity=24`는 현재 실행 buffer 정원으로 문서화하지 않는다. production runtime에서 해당 필드 소비자를 찾지 못했다.

## 정적 검증 경계

- 능력 정의, 두 작성 자산, support 연결 필드, 생산 입력 선택·비용 코드, bill 저장 codec, 패널과 공개 페이지/가이드를 직접 읽었다.
- 실제 조리 bill 실행, 품절 후 대기, 저장 왕복과 물리 운반을 Play Mode에서 재현하지 않았다.
