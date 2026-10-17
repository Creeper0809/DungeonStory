# 장기 보관함 런타임·공개 위키 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingOrganStorageAbility` 작성 시설은 M08 장기 보관함(id9508) 한 개다.
- M08은 자연 장기의 자동 물리 배송, 장기별 보존 용기, 별도 현장 연료, 신선도 감쇠와 저장 상태를 소유하며 수술실에서는 OrganStorage support tag와 무균 보정도 제공한다.
- 공개 M08 페이지와 의료 가이드는 이 계약을 설명하지 않는다. GAP-086은 음식 부패만 소유하고 장기 보관을 후속 조사로 남겼으며 GAP-174는 M08의 별도 일반 FuelConsumer 계약만 소유한다.
- 장기 보관 전용 계약은 GAP-182로 분리한다.

## 작성값과 두 용량

| 필드 | 작성값 | 현재 실행 |
| --- | ---: | --- |
| preservationDays | 15 | 직접 consumer 없음; runtime 상수 `2/15` 감쇠가 결과15일을 만듦 |
| fuelPerDay | 1 | 전용 현장 연료1개당180초 |
| capacity | 8 | 자동 route의 장기 수 gate와 패널 표시 |
| BuildingStorage maxStoredMassGrams | 12,500 | 장기 destination의 물리 질량 한도 |

- count capacity8과 물리 질량12,500g은 서로 다른 제한이다. 연료 destination은 선택 연료 한 개분800g의 별도 질량 한도를 가진다.
- M08은 수술시설로 primary가 아니며 OrganStorage tag, Sterility0.05, speed1/success0/anesthesia0을 같은 usable room에 제공한다.
- 현재47개 절차 중 OrganStorage tag를 요구하는 절차는0개다. 그래도 M08이 같은 방 support이면 Sterility0.05가 성공식에+0.004, 감염식에 clamp 전-0.0225 입력으로 적용된다.

## 자동 보관과 보존 수명

- 적출한 자연 장기는 loose freshness360초로 생성된다. 180초=1일 기준2일이다.
- 생성 순간 비파괴·비손상 M08 가운데 이미 destination이 지정된 organ stack 수가 capacity보다 작은 후보를 Manhattan 거리순으로 골라 실물 stack 배송을 한 번 요청한다. 거리 동률의 명시적 persistent-ID tie-break는 없다.
- `RequestOrganStorage` 호출은 자연 장기 생성 경로 한 곳뿐이다. 그 순간 후보가 없거나 배송 요청이 실패해도 이후 자동 재탐색하지 않는다.
- 장기 stack이 실제 M08 destination에 있고 시설이 비파괴·비손상·연료 가동 상태여야 working storage다.
- 각 자연 장기는 처음 보존될 때 M08 buffer의 `medical:organ-preservation-canister`1개를 물리 sink한다. 없으면 한 개 배송을 요청하고 그동안 일반1배로 신선도가 감소한다.
- 보존 용기 적용 provenance는 장기에 영구 기록된다. 이후 연료가 끊기면 일반1배로 돌아가지만 연료가 복구되면 새 용기 없이 보존 감쇠를 다시 적용한다.
- 보존 감쇠는 `StoredFreshnessRate=2/15` 상수다. freshness360초를2700초, 즉15일로 늘린다. authored preservationDays15는 직접 읽히지 않는다.
- 신선도가0 이하가 되면 장기 stack을 삭제하고 같은 위치에 `surgery:contaminated-tissue`1개를 생성한 뒤 surgical part 기록을 제거한다.

## 전용 연료와 일반 FuelConsumer의 분리

- M08 전용 연료 authority는 StockCategory.Fuel의 양수 물리질량을 내림차순, item ID를 오름차순으로 정렬해 첫 항목을 고정한다.
- 현재 후보는 low-fuel800g, coal800g, charcoal450g, candle200g이고 동률 ID 정렬로 `material:low-fuel`이 선택된다. General category인 log는 Fuel feature가 있어도 이 경로 후보가 아니다.
- 남은 연료가90초 이하이고 route된 연료가 없으면 low-fuel1개 배송을 요청한다.45초 이하에서 buffer의 가장 이른 stack ID 한 개를 sink해180초를 더한다.
- 이 로직은 저장 장기 수를 검사하지 않으므로 빈 M08도 연료를 요청하고 도착한 연료를 소비한다.
- M08의 `BuildingFuelConsumerAbility`는 GAP-174의 전역 창고 직접 차감·야간 lightSafety 경로이며 장기 냉각의 현장 low-fuel buffer/fuelSeconds와 별개로 동시에 존재한다.

## 저장·UI 경계

- surgical part는 freshness, storedFacilityId, preservationCanisterApplied와 sink operation/commit/source/mass provenance를 저장·검증한다.
- organ storage state는 facilityId, fuelSecondsRemaining, fuelDeliveryRequested를 저장하고 restore에서 실제 M08 존재를 검증한다.
- 건물 패널은 `장기 보관 stored/capacity`, 냉각 작동/중단, 남은 연료초 또는 일반 부패 속도를 표시한다. 보존 용기 대기, exact fuel ID/선정법, 장기별 freshness, 물리 질량 한도와 수술 sterility는 표시하지 않는다.
- 공개 building-9508 facts는 분류·크기뿐이고 summary는 연료 소비·내부 공정 재고·재고 보관·건설비만 설명한다.

## 정적 검증 경계

- ability/M08와 Fuel item 자산, 장기 생성·route·destination authority·canister sink·fuel·freshness·expiry, save/restore validation, 수술시설 query/risk, 건물 UI와 공개 페이지/가이드를 읽었다.
- 실제 적출 후 운반, capacity/질량 포화, 보존 용기와 low-fuel 배송·소비, 냉각 중단/복구, 만료 변환, 저장 왕복은 Play Mode에서 재현하지 않았다.
