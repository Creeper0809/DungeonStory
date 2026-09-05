# 의복 상태·환복 및 운반 멜빵 대조

상태: 해당 경로의 정적 대조 완료, 전체 시스템·위키 전수 감사는 진행 중이다. 스크립트·자산·공개 위키는 변경하지 않았다. 밸런스 영향 없음이다.

## 범위와 기존 판정

기존 의복 56종·작업복 4종의 작성 수치는 [의복·환경 대조](apparel-environment-review.md)에 있다. 이번에는 일반 의복의 상태 변경과 후보 조회를 추적하고, 운반 멜빵의 물리 아이템 하나에 연결된 작성 정의 2개·공개 도감 2개 및 가이드 2개를 직접 대조했다. 근거 파일 24개의 해시는 전문 완독 수가 아니다.

- 새 공백은 GAP-112 한 건이다. 운반 +25%와 필요 연구는 이미 공개되어 있으므로 누락으로 세지 않는다.
- 방한 자동착용·체형/레이어·소재·세탁/수선은 GAP-085/096/098~103을 참조한다.
- 일반 자동 환복 미확인은 APPAREL-U04의 후속 근거다. 이번 확인 사항 6개를 전부 독립 문서 공백으로 세지 않는다.

## 운반 멜빵의 실제 경로

| 상황 | 정적 소스에서 확인한 처리 |
| --- | --- |
| 운반 시작, 환경 작업복 없음 | 창고 구획 연구, 사용 가능한 실물과 체형 조건을 검사해 착용을 시도한다. 실패 결과를 운반 시작 거부로 사용하지 않는다. |
| 이미 멜빵을 착용 | 운반 한도 보정을 받지만 이번 운반에서 새로 입었다는 플래그는 false다. |
| 다른 환경 작업복 착용 | 기존 옷을 유지하고 멜빵 자동 착용을 생략한다. |
| 이번 운반에서 새로 착용하고 정상 완료 | 도구 내구도를1 줄이고 벗기를 시도한다. |
| 중단·재계획·복원 회수 등 다섯 종료 호출 | 도구 내구도를 줄이지 않고 벗기를 시도한다. |
| 벗기 성공 | 현재 주민 위치에서 apparel-recovery-locker 목적지의 Stored 상태로 옮긴다. 원래 걸이로 걸어서 돌려놓는 과정은 아니다. |

실물 후보는 수량1·유효한 인스턴스ID·사용 가능한 수량·금지 아님을 요구한다. 바닥, 보관, 시설 출력 버퍼 중 아직 착용 목적지가 아닌 실물을 고르며 보관 상태가 우선이고 다음은 StackId순이다. 후보 조회와 운반 한도 계산에는 도구 내구도0을 거르는 조건이 없다.

운반 한도의 25kg·1.25배·최대 운반 배율은 이미 가이드에 있다. 한도 산술 예시에서 운반 기능 배율1과 기본 최대 배율1.5를 쓰면 멜빵 착용 시 기본 한도31.25kg, 최대46.875kg다. 이 값을 별도 새 공백으로 세지 않는다.

## 내구도를 한 가지 상태로 읽으면 안 되는 이유

| 상태 | 초기/최대값 | 확인한 쓰기 경로 |
| --- | --- | --- |
| 도구 Durability.current | 120 | 멜빵의 정상 완료 운반에서1 감소 |
| 의복 Apparel.durability | 100 | 의복 생성과 수선. 수선은60 이상이면 최대100까지25 회복, 그 미만의 유효 수선 대상은70으로 회복 |
| 의복 Apparel.moisture | 0 | 손세탁100, 동력 세탁·건조0 |
| 의복 Apparel.contamination | 0 | 세탁0 |

수선은 의복 컴포넌트를 바꾸며, 운반에서 읽는 도구 내구도를 회복하는 코드로 연결되지 않는다. 물리 아이템의 일반 오염 컴포넌트 역시 의복 오염 값과 구별해야 한다.

CreateDurability는 음수 인자를 '새 도구의 기본값'으로 처리한다. 그래서 정상 완료마다 새로 착용하고 쓰기가 성공한다는 조건에서 120번째 완료 후0, 121번째 완료 후120이 된다. 독립 산술로 이 분기를 확인했지만 실제 플레이 재현은 하지 않았다. '120회 후 파손'이라는 정상 규칙을 문서에 추가해서는 안 된다.

## 의복 상태와 자동 환복 조사

오염이0보다 크면 Contaminated, 그렇지 않고 젖음이20 이상이면 Wet, 나머지는 Ready다. 일반 후보 조회는 오염된 옷을 제외하고 기본값으로 젖은 옷도 제외한다. 후보는 품질, 내구도, StackId순으로 최대8개다. 그러나 해당 조회를 실제로 구성해 호출하는 일반 환복 경로는 비Editor·비Debug C# 역검색에서 찾지 못했다. 실제 환경 작업복 착용은 별도 물리 후보 선택을 사용한다.

의복 상태 필드 대입·codec 쓰기·컴포넌트 참조를 역검색했다. 생성, 세탁·건조, 수선, 개조, 복사·복원 경로는 보이지만 비나 착용시간, 작업으로 의복 오염·젖음·마모를 누적하는 생산자는 확인되지 않았다. 이 결과는 일일 감소율을 추정할 근거가 아니며 전체 게임의 모든 간접 상태 변경이 없다는 증명도 아니다.

## 추가 설명이 필요한 항목

## GAP-112 운반 멜빵의 자동 착용·반납 조건과 내구도 소모 수치

- 분류: 조건·수치·예외 누락
- 보완할 문서: inventory-and-carrying (entry/item/tool-hauling-harness와 entry/combat/workwear-hauling-harness에서 참조)
- 현재 문서: 재고 가이드는 멜빵의 운반 한도 1.25배를, 작업복 도감은 적재 +25%·완료 시 내구도 소모와 필요 연구를 이미 설명한다. 하지만 자동 착용을 시도하는 시점, 다른 작업복 착용 시의 예외, 정상 완료와 중단의 소모 차이, 도구 내구도 120과 회당 소모 1은 없다. 아이템 도감의 질량·적재·가격과 세계관 설명도 이를 보완하지 않는다.
- 추가·정정할 내용: 운반 시작 시 다른 환경 작업복을 입지 않았고 창고 구획 연구·사용 가능한 실물·체형 조건을 충족하면 멜빵 착용을 시도한다. 실패해도 운반은 기본 한도로 진행하며, 기존 작업복을 멜빵으로 자동 교체하지 않는다. 해당 운반에서 새로 입은 멜빵만 정상 완료 시 도구 내구도 1을 줄이고 벗으며, 중단·재계획·복원 회수는 소모 없이 벗는 경로다. 이미 입고 있던 멜빵에는 이 운반의 소모·반납 처리가 적용되지 않는다. 도구 최대 내구도는 120이지만 현재 0에서 추가 완료 시 음수 기본값 처리로 120이 되는 정적 반례가 있으므로 '120회 사용 후 파손'을 확정 규칙으로 쓰지 않는다. 의복 내구도 0~100 및 수선과 도구 내구도는 별개다. 벗기는 실제 경로는 원래 걸이로 걸어가는 과정이 아니라 현재 위치의 회수 목적지로 라우팅하므로 원위치 반납을 보장한다고 설명하지 않는다. 일반 방한 자동착용·체형 조건은 GAP-085/098을 참조하고 구현 경계는 apparel-lifecycle-review의 확인 사항으로 분리한다.
- 원본: [CharacterCarryInventory.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/CharacterCarryInventory.cs>), [ItemPrimitives.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Items/Core/ItemPrimitives.cs>), [AbilityHaul.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/AbilityHaul.cs>), [WorldItemStackRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/WorldItemStackRuntime.cs>), [WorldItemSpawner.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/WorldItemSpawner.cs>), [EnvironmentalWorkwearRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs>), [CharacterApparelRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterApparelRuntime.cs>), [ApparelItemStateCodec.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelItemStateCodec.cs>), [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [HaulingHarness.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Environment/Workwear/HaulingHarness.asset>), [apparel_hauling_harness.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Apparel/Definitions/apparel_hauling_harness.asset>), [inventory-and-carrying.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/inventory-and-carrying.md>), [tool-hauling-harness.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/tool-hauling-harness.json>), [workwear-hauling-harness.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/combat/workwear-hauling-harness.json>)
- 확인한 심볼: TryPrepareHaulingHarness / CompleteHaulingHarness / IsHaulingHarnessEquipped / DurableToolItemRules.CreateDurability / AbilityHaul terminal paths / TryEquip / TryUnequip
- 판정: owner-gap-confirmed / global_deduplication=pending

## 구현 확인 사항

### APPAREL-LIFE-U01 일상 착용에 따른 의복 오염·젖음·마모 발생 경로 미확인

비Editor·비Debug C#의 필드 대입, codec 작성 호출과 컴포넌트 참조를 역검색했다. 의복 상태는 생성 시 100/0/0, 세탁·건조 시 moisture 0 또는100·contamination 0, 수선 시 durability 회복으로 기록된다. 비·침수·작업·착용시간에 따라 이 상태가 증가하거나 마모되는 직접 생산자는 찾지 못했다. 물리 아이템의 일반 contamination과 의복 컴포넌트의 contamination을 같은 값으로 취급하지 않는다.

일일 오염량·자연 건조·착용 마모율을 임의로 만들어 문서 누락으로 세지 않는다. 실제 상태 생산자의 존재와 의도된 연결부터 확인해야 한다.

연결된 기존 판정: GAP-100, GAP-101, APPAREL-U01.

### APPAREL-LIFE-U02 일반 의복 후보 조회가 자율 환복으로 이어지지 않음

ApparelAvailabilityIndex 전문은 최대8개, 품질 내림차순·내구도 내림차순·StackId순의 후보 조회와 오염·젖음 필터를 제공한다. 그러나 ApparelSelectionQuery는 선언·매개변수로만 나타나고 실제 생성 호출을 찾지 못했다. IApparelAvailabilityIndex는 정의·DI·Aggregate의 Invalidate 의존으로만 나타난다. 실제 환경 작업복은 별도의 물리 후보 선택으로 TryEquip를 호출한다.

기존 APPAREL-U04의 후속 근거이며 새 독립 공백으로 계수하지 않는다. 주민이 모든 옷을 비교해 자동 교체한다고 설명하지 않는다.

연결된 기존 판정: APPAREL-U04, GAP-098.

### APPAREL-LIFE-U03 멜빵의 내구도0 추가 사용이 최대값120으로 복귀

CompleteHaulingHarness는 current-1을 CreateDurability로 넘긴다. CreateDurability는 current<0이면 최대값120을 선택한다. TryEquip와 운반 한도 조회에는 도구 내구도>0 검사가 없고 TrySetInstanceComponent는 유효한 기존 스택의 컴포넌트를 교체한다. 정상 완료·이번 운반에서 새 착용·컴포넌트 쓰기 성공을 전제로 120→119→…→0→120이 된다.

정적 반례이며 게임 실행으로 재현한 결함이라고 보고하지 않는다. 파손/소모 수명을 확정하기 전에 경계 검증이 필요하다.

연결된 기존 판정: GAP-112.

### APPAREL-LIFE-U04 도구 내구도와 의복 수선 내구도가 별도로 존재

멜빵 운반은 Durability 컴포넌트의 current/maximum을 읽고 쓴다. ApparelItemStateCodec는 Apparel 컴포넌트의 durability를0~100으로 저장한다. ResolveRepair는 후자를 회복하며 운반 도구 current를 갱신하지 않는다. WorldItemSpawner는 도구 컴포넌트가 없으면120을 생성한다.

옷 수선으로 멜빵의 운반 내구도가 회복된다고 단정하지 않는다. 동일 실물의 두 상태를 UI가 어떻게 구분하는지는 미검증이다.

연결된 기존 판정: GAP-101, GAP-112.

### APPAREL-LIFE-U05 멜빵 사용 플래그·중단·반납 실패와 저장 재개의 일치성 미검증

이미 착용한 멜빵은 TryPrepareHaulingHarness가true를 반환해도 equippedForThisRun은false다. AbilityHaul의 플래그는 private bool이며 검색된 사용은 착용 결과와 종료 시 false 설정뿐이다. CaptureDeliveryIntentForSave에는 해당 플래그를 담는 코드가 보이지 않는다. CompleteHaulingHarness는 컴포넌트 쓰기와 TryUnequip의 성공 여부도 소비하지 않는다. WorkTaskExecutor에 별도 작업복 반납 경로가 있으므로 '영구 미반납'으로 일반화하지 않는다.

저장 직전/직후 완료의 내구도 차이와 회수 실패 처리를 실제 경로에서 검증해야 한다. 확인 전에는 항상 같은 비용·원래 위치 반납을 보장한다고 쓰지 않는다.

연결된 기존 판정: GAP-112.

### APPAREL-LIFE-U06 시작 의복의 지정 착용자 표시는 착용 제한으로 소비되지 않음

PreparedStartPartyGameplayApplier는 designatedWearerCharacterId에 시작 주민ID를 쓴다. 검색된 다른 사용은 정의·codec 저장/복원·복사뿐이다. CanEquip는 몸 형태·부착점·크기·열린 구멍을 검사하고 지정 주민을 비교하지 않는다.

지정된 주민만 그 옷을 입을 수 있는 소유권 규칙으로 설명하지 않는다. 지정값의 UI/배정 의도는 확인 필요다.

연결된 기존 판정: GAP-098.

## 검증과 한계

- 독립 산술20건 통과: 내구도 생성5건, 완료 횟수 경계4건, 의복 상태 분류6건, 운반 한도5건이다. 소스 실행 테스트나 Unity 검증으로 세지 않는다.
- 실제 UI 입력, 착용 이동, 세탁·수선 실행, 저장 왕복은 미실행이다. 사용 플래그와 기존 작업복 반납의 교차 경로도 실행 검증이 남았다.
- KB query: ApparelRuntime HaulingHarness durability. 영역 code/authority/content, limit6, session11261, exit1, stale4건, 생성 행0개다. 생성 인덱스 대신 원본을 읽었으며 재생성하지 않았다.
- content source digest: `139a0a989275ecdd5a4a26c10ceb6a1931041c7c928ed0421628faea5cd928c6`.
- knowledge-base source digest: `ceef8dc8f25f4d327205b15e12346aee0ebc5d6a84aa7eeb1f08af5ce14db0dd`.
- 전체 함수·필드 전수 대조, 남은 시스템/도감 조사와 최종 의미 중복 정리는 완료하지 않았다.

## 직접 근거

- [CharacterCarryInventory.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/CharacterCarryInventory.cs>)
- [ItemPrimitives.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Items/Core/ItemPrimitives.cs>)
- [AbilityHaul.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/AbilityHaul.cs>)
- [WorldItemStackRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/WorldItemStackRuntime.cs>)
- [WorldItemSpawner.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Items/WorldItemSpawner.cs>)
- [ApparelAvailabilityIndex.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelAvailabilityIndex.cs>)
- [ApparelItemStateCodec.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelItemStateCodec.cs>)
- [CharacterApparelRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/CharacterApparelRuntime.cs>)
- [EnvironmentalWorkwearRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearRuntime.cs>)
- [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>)
- [ApparelPhysicalTransaction.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelPhysicalTransaction.cs>)
- [EnvironmentalWorkwearProductionOutputHandler.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearProductionOutputHandler.cs>)
- [EnvironmentalWorkwearProductionOutputRestoreCapabilityValidator.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/EnvironmentalWorkwearProductionOutputRestoreCapabilityValidator.cs>)
- [PreparedStartPartyGameplayApplier.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Core/PreparedStartPartyGameplayApplier.cs>)
- [ApparelDefinitionSO.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/ApparelDefinitionSO.cs>)
- [DungeonWorldSimulationRegistration.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs>)
- [WorkTaskExecutor.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs>)
- [HaulingHarness.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Environment/Workwear/HaulingHarness.asset>)
- [apparel_hauling_harness.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Apparel/Definitions/apparel_hauling_harness.asset>)
- [inventory-and-carrying.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/inventory-and-carrying.md>)
- [species-culture-and-life.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md>)
- [tool-hauling-harness.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/item/tool-hauling-harness.json>)
- [workwear-hauling-harness.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/combat/workwear-hauling-harness.json>)
- [apparel-and-module-numeric-notes.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/docs_final/game-design/content-plans/items/apparel-and-module-numeric-notes.md>)

구체적인 읽기 범위와 해시는 [기계 판독 결과](apparel-lifecycle-review.json)에 보존했다.

