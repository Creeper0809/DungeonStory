# 주민 식사 소비·효과 후속 감사

상태: 정적 대조 완료. Unity 실행·UI 시연·저장 왕복 검증은 수행하지 않았다. 게임 코드·자산·공개 위키는 수정하지 않았다.

## 판정 요약

- `FoodItemFeature`를 가진 자산은 47개지만 `CharacterConsumablesRuntime.TryGetMeal`이 요구하는 `ResourceItemDefinitionSO`까지 만족하는 실제 식사 후보는 22개다. 제외 25개는 야생 사체20, 어둠의 인간형 고기1, 생존 placeholder4다.
- 실제 식사 22개는 모두 `ItemDefinitionCatalog.asset`에 정확히 한 번 등록되고, 공개 item entry와 `food-and-ecology.md`에 연결된다. 페이지 자체의 누락은 없다.
- 공개 entry 22개는 무게·스택·가격 3개 facts만 가지며 nutrition, mood, freshness, preserved, diet category, meal tier, quality band, meal role을 표시하지 않는다.
- 가이드는 식단·품질·문화 선호와 운반을 일반적으로 설명하지만 자동 후보 필터, 점수·경로 shortlist, 물리 예약/commit, 효과 영수증, 배달 재시도, 일별 식사 ledger와 저장 권위를 설명하지 않는다.
- 음식 접촉 감염은 GAP-022, 저장 온도와 부패 배율은 GAP-086의 소유 범위다. 이 보고서는 실제 섭취 판정과 식사 효과만 새 항목으로 센다.

## 실제 식사 22종

| item ID | diet | tier / band / role | nutrition | mood | freshness(s) | preserved |
| --- | --- | --- | ---: | ---: | ---: | --- |
| food:boar-stew | Mixed | Fine / Fine / Full | 55 | 7 | 390 | no |
| food:cheese-mushroom | Vegetarian | Fine / Decent / Full | 50 | 5 | 390 | no |
| food:egg-pancake | Vegetarian | Fine / Decent / Full | 50 | 4 | 360 | no |
| food:expedition-ration-pack | Mixed | Preserved / Decent / FieldRation | 50 | 1 | 1800 | yes |
| food:fermented-pickle | Vegan | Preserved / Simple / Snack | 15 | 2 | 1440 | yes |
| food:fresh-curd | Vegetarian | Fine / Decent / LightMeal | 25 | 3 | 240 | no |
| food:garden-meal | Vegan | Fine / Decent / Full | 50 | 4 | 420 | no |
| food:grain-porridge | Vegan | Simple / Poor / Full | 35 | 0 | 360 | no |
| food:grape-syrup | Vegan | Fine / Fine / Snack | 15 | 3 | 900 | yes |
| food:jerky | Carnivore | Preserved / Poor / FieldRation | 35 | -1 | 1440 | yes |
| food:lavish-meat | Mixed | Lavish / Lavish / Full | 65 | 12 | 480 | no |
| food:lavish-vegan | Vegan | Lavish / Lavish / Full | 60 | 10 | 480 | no |
| food:malt-porridge | Vegan | Simple / Simple / Full | 40 | 2 | 360 | no |
| food:meat-pie | Mixed | Fine / Fine / Full | 55 | 7 | 450 | no |
| food:mushroom-soup | Vegan | Simple / Poor / Full | 36 | 1 | 330 | no |
| food:preserved-ration | Vegan | Preserved / Simple / FieldRation | 40 | 0 | 1800 | yes |
| food:preserved-vegetable | Vegan | Preserved / Simple / Full | 40 | 2 | 1440 | yes |
| food:roasted-meat | Carnivore | Simple / Simple / Full | 42 | 3 | 300 | no |
| food:root-stew | Vegan | Simple / Simple / Full | 40 | 2 | 360 | no |
| food:salted-meat-stew | Mixed | Fine / Fine / Full | 55 | 6 | 720 | yes |
| food:stuffed-mushroom | Mixed | Fine / Decent / Full | 50 | 5 | 360 | no |
| food:vegetable-pie | Vegetarian | Fine / Decent / Full | 50 | 5 | 450 | no |

분포는 diet Vegan10/Vegetarian4/Carnivore2/Mixed6, tier Simple5/Fine10/Lavish2/Preserved5, quality Poor3/Simple6/Decent7/Fine4/Lavish2, role Full16/LightMeal1/Snack2/FieldRation3/EmergencyOnly0이다. 영양15~65, 기분-1~12, 신선도240~1800초이며 preserved는 7개다. 가이드의 `food-night-spirit`, `food-twilight-beer`는 음료라 이 식사 모집단에서 제외한다.

## 정책과 자동 후보 선택

- 식단 허용: Vegan은 Vegan만, Vegetarian은 Vegan/Vegetarian, CarnivorePreferred는 Carnivore/Mixed, StrictTaboo는 종 문화의 금지 재료를 거부하고 Free는 전부 허용한다. 종 문화의 선호·금지는 `SpeciesCultureDefinitionSO.preferredItemIds/forbiddenItemIds`에서 온다.
- 주민별 품질 상한은 존재한다. `Inherit`는 현재 `Fine`으로 해석된다. 기본 UI 순환은 식단 정책만 바꾸며 비Editor의 `SetMealQualityLimit` 호출자는 찾지 못했다.
- 자동 식사는 배고픔50 이하에서 시작하고 20 이하는 emergency다. 75까지 회복을 목표로 하며 하루 감소량은50이다. 의식 단식은 시작을 막고 Snack/LightMeal은15초 후속 cooldown을 둔다. 명시적 command는 자동 배고픔/cooldown gate를 우회하지만 정책·오염 검사는 유지한다.
- field 후보는 사용 가능 loose/stored stack, 신선도4.25초 초과, 금지 아님, 품질 상한 이하를 요구한다. 비상 전용은 emergency에서만, 평시는 식단 허용과 contamination 0.01 이하가 필요하다.
- facility 후보는 식단 허용 +10000, 문화 선호 +1000, 품질 band ±100, 오래된 음식 최대 +120, nutrition+mood×2와 행동 utility를 합산하고 오염을 감점한다. 최선 ETA+8초 shortlist의 상위7개만 exact route로 다시 평가하고, 부패 임박18초 이하는 최대8초 rescue를 적용한 뒤 ETA를 감점한다.
- 기본 기분35 미만은 높은 품질을, 그 이상은 낮은 품질을 선호한다. 최종 동률은 meal ID와 stack ID로 결정한다.

## 물리 소비·효과·재시도

- 시설 식사는 `max(1, EffectiveCapacity)` 슬롯과 1회분 수량 lease를 잡고 4초 eating 단계에 들어간다. 시작은 신선도4.25초 초과를 요구한다.
- commit 시 주민·시설·정의·정책·오염·스택·신선도·lease·buffer를 다시 검증한다. 오염이 0.01을 넘었거나 완전 부패하면 중단한다.
- 성공은 정확히 1회분을 내구성 pending receipt로 기록한다. 효과를 한 번 발행한 뒤 `EffectsPublished`로 저장하고 물리 ack를 수행하므로 ack 재시도가 효과를 반복하지 않는다.
- 배고픔 회복은 nutrition×`performance:survival:nutrition-efficiency` 결과다. 영양 공식은 섭취처리0.55(병목), 정화0.20, 동력순환0.15, 활력반응0.10이다.
- 식사 기분은 180초 `meal:best-active`이며 더 낮은 새 식사 기분은 현재의 더 높은 효과를 덮지 않는다. 식단 위반은 기분-9와 서사를 남긴다.
- 오염 식사는 `performance:survival:food-poisoning` 확률을 투영한다. 이 공식은 면역0.35·정화0.35 병목, 섭취0.20, 활력0.10이다. 당첨 시 기분-7·피해3이며, 성공한 오염 식사는 별도로 `disease:gut-rot` Food 노출(24, intensity1)을 기록한다.
- 능동 배달 probe는 1초마다 ID 순 첫 배고픈 비단식 주민과 거리/ID 순 Meal 시설을 고른다. item/facility 목적지로 1개를 요청하고 실패 route는45초 후 재시도하며 운반자 1명에게 재계획을 요청한다.

## ledger·저장 권위

- consumables save v8은 sequence, 주민별 식단/품질 정책, pending delivery, 완료 operation, snack cooldown, active meal plan과 substance 상태/plan을 저장한다.
- meal ledger는 최대512개를 보존하고 조회는 최근100개다. day, character, facility, item/display, diet, quality, nutrition, policyViolation, contaminated, amount1을 기록한다.
- 전날 식사가 없으면 `MealMissedEvent(consecutiveMisses=1)`을 발행한다. 일별 필요/섭취/누락 수와 전역 연속 부족일을 계산한다.
- 구형 `FacilityStockConsumedEvent`의 Meal/Food 기록 경로도 남아 있지만 현재 물리 식사 event와 중복 기록되는지는 이번 정적 대조로 확정하지 않았다.

## 구현 차이 후보: 호화식 자동 소비

`food:lavish-meat`와 `food:lavish-vegan`은 가이드가 주민용 호화식으로 제시하고 실제 band도 Lavish다. 그러나 자동 facility/field 선택은 주민 품질 상한을 적용하며 기본 `Inherit`를 Fine으로 해석한다. 비Editor에서 품질 상한을 올리는 `SetMealQualityLimit` 호출자와 특정 stack을 고르는 `ConsumeMealCommand` 생산자를 찾지 못했다. 따라서 현재 확인한 production 자동 경로에서는 두 호화식에 도달할 수 없는 정적 차이로 DIFF-035에 기록한다. 이는 Editor/debug 경로나 향후 동적 호출 가능성까지 부정하는 실행 재현 판정이 아니다.

## 근거 범위

- 콘텐츠/공개: `FoodItemFeature`, `ItemDefinitionCatalog.asset`, 공개 item entries, `food-and-ecology.md`, `residents-and-work.md`, `need-references.json`.
- 실행/저장: `CharacterConsumablesRuntime`, contracts/state/persistence, application adapter/input owner, `SurvivalFoodRuntime`, spoilage/state/stock, `SurvivalMealLedger`, `AIEat`, `AIPrimitiveFieldMeal`, `CharacterConsumablesSaveSection`, `PopulationHealthRuntime`.
- 감사 KB query `food consumption meal nutrition mood contamination ingestion hunger thirst`는 stale 2429 / 반환0행이었다. 읽기 전수 작업이므로 재생성하지 않고 원본과 공개 문서를 직접 대조했다.

