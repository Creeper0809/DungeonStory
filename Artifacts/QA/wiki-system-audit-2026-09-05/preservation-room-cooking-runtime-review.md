# 보존 부품의 같은 방 조리 출력 런타임 대조

상태: 정적 감사 완료. Unity Play Mode 재현은 수행하지 않았다.

## 범위와 결론

- 현재 비deprecated `BuildingPreservationAbility` 작성 부품은 D10 식재료선반(id1009)과 L05 식재료저장함(id1054) 두 개다.
- 두 부품은 독립적인 조리 작업을 만들지 않는다. 일반 방의 조리 시설에서 Cook가 완료될 때 같은 운영 방의 part 가운데 Preservation ability가 있으면 산출 ID와 수량을 바꾼다.
- 공개 두 시설 페이지는 `부패 억제와 보존`이라는 역할 요약만 제공하고, 같은 방 적용 범위·산출 ID·수량·중복 배치 처리·부패 추적 제외를 설명하지 않는다.
- GAP-086은 추적되는 음식의 온도/보존상태 부패 배율, GAP-137~138은 식사 정의와 주민 소비를 소유한다. 이 조리 산출 전환 경로를 소유한 기존 GAP은 없어서 GAP-176으로 등록한다.

## 작성값과 활성 필드

| 부품 | id | freshnessMultiplier | preservedMealsPerCook | 현재 실행 판정 |
| --- | ---: | ---: | ---: | --- |
| D10 식재료선반 | 1009 | 3 | 1 | 같은 방 Cook 산출을 보존 식량1개로 변경 |
| L05 식재료저장함 | 1054 | 5 | 2 | 같은 방 Cook 산출을 보존 식량2개로 변경 |

`freshnessMultiplier`는 능력 정의와 작성 builder 외 production runtime 소비자를 찾지 못했다. 따라서 현재 신선도3배/5배 효과로 공개하면 안 된다. 실제 소비되는 필드는 `preservedMealsPerCook`다.

## 같은 방 조리 계약

1. Cook 작업 대상 자체에 `BuildingCookingAbility`가 있어야 한다. Preservation 부품만으로 Cook 작업이 생기지 않는다.
2. 현재 조리 대상은 D01 간이화덕(input1/output2/Fuel1), D02 고기그릴(input1/output3/Fuel1), D03 조리손질대(input1/output1/Fuel0) 세 개다.
3. 작업 가능 여부와 선소비는 대상 Cooking ability를 따른다. 전역 Food가 input 이상이고 필요한 경우 Fuel이 있어야 하며, 완료 때 Food와 Fuel을 먼저 withdraw한다.
4. `FindPreservationAbility`는 대상의 room operational profile 전체 part를 훑어 첫 Preservation ability를 찾는다. 있으면 일반 `survival:cooked_meal` 대신 exact `survival:preserved_food`를 고른다.
5. 출력 수량도 대상 Cooking ability의 `cookedMeals`가 아니라 찾은 Preservation ability의 `preservedMealsPerCook`로 덮는다. 따라서 조리시설별 기본 output2/3/1은 보존 부품이 있는 방에서 D10이면1, L05이면2가 된다.
6. D10과 L05가 같은 방에 함께 있어도 효과는 합산되지 않는다. room part 열거 뒤 `FirstOrDefault`를 쓰며, 이 경로에는 두 보존 부품 사이의 명시적 정렬·우선순위 규칙이 없다.

물리 stack spawn이 실패하면 공통 `ModularFacilityRuntimeEffects.Produce`가 Food 추상 재고를 생산한다. 성공/실패 어느 쪽이든 완료 activity는 보존식 생산으로 기록된다.

## 출력 음식과 부패 경계

- 일반 `survival:cooked_meal`: nutrition10, mood0, freshnessSeconds600, preserved=false.
- 보존 `survival:preserved_food`: nutrition10, mood0, freshnessSeconds0, preserved=true.
- `SurvivalFoodSpoilageRuntime`는 Food이면서 `freshnessSeconds > 0`인 definition만 추적한다. 따라서 이 경로의 보존 식량은 보존 0.25배로 천천히 감소하는 대상이 아니라, 현재 구현상 부패 추적에서 완전히 제외된다.
- GAP-086의 preserved multiplier는 freshnessSeconds가 양수인 추적 음식에만 적용된다. 두 계약을 같은 효과로 합치지 않는다.

## UI·공개 위키 경계

- 공개 D10/L05 페이지 facts는 분류·크기뿐이고 relations는0이다.
- 식량 가이드는 날 식재료를 조리대나 보존 설비로 보낸다고만 하며, same-room modifier와 exact output/수량을 설명하지 않는다.
- 런타임 UI/Codex에서 적용 중인 보존 부품이나 출력 수량을 표시하는 소비자를 찾지 못했다. 완료 activity의 `food-preserved` 결과가 사후 신호다.

## 정적 검증 경계

- 능력 정의, 두 작성 부품, 조리시설3개, room profile 수집, 조리 완료, exact item definition, 부패 추적 gate, 공개 시설/아이템 페이지와 식량 가이드를 직접 읽었다.
- 실제 방 배치, D10+L05 동시 배치의 열거 순서, 물리 stack spawn/fallback과 저장 왕복은 Play Mode에서 재현하지 않았다.
