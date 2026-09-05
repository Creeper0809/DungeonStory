# 의복 제작·품질·불합격품 처리 부분 감사

현재 의복 56종·원단 12종·재단시설 1개·특성 1개를 대조해 GAP-104~109를 추가했다. 전체 감사는 진행 중이다. 이 보고서는 정적 문서/원본 대조이며 실제 Unity·UI·저장 왕복 검증이 아니다.

## 기존 설명과 새 누락

공통 품질 공식과 등급표, 고정 난수, 반복 XP는 이미 설명되어 있어 다시 누락으로 세지 않았다. 새 누락은 아래 6건이며 전역 의미 중복 검토는 pending이다.

## GAP-104 의복 맞춤 제작의 원단 수량·크기·개조별 작업량 공식 누락

- 분류: 재료량·작업량 공식 누락
- 보완할 문서: production (species-culture-and-life·의복 도감에서 참조)
- 현재 문서: 생산 문서는 개별 제작식의 BOM과 WU를 도감에서 확인하도록 안내한다. 의복 56개 도감은 무게·적재·가격만 표시하며, 재단·재봉 작업대의 맞춤 제작이 원단 수와 WU를 별도로 계산한다는 설명은 없다.
- 추가·정정할 내용: 맞춤 의복 한 벌은 허용 태그에 맞는 한 종류의 원단을 max(1, ceil(2×재단계수))개 사용한다. 현재 56종의 필요량은 1~3개다. 제작 WU는 (10 + 12×clamp(ceil(재단계수),1,5) + 4×점유 부위 수 + 개조 작업량)에 크기 배율과 원단의 작업 배율을 곱한 뒤 2 WU 단위로 반올림하며 최소 2 WU다. 크기 배율은 소형 0.75·중형 1·대형 1.30이고 개조는 꼬리 4·날개 8·뿔 3 WU를 합산한다. 크기와 개조는 이 경로의 원단 개수를 늘리지 않는다. 원단 작업 배율은 별도 경제 프로필 또는 물리 아이템의 종류·태그·무게에서 계산하며 전투 소재 배율과 구분한다. 현행 V27 계산기는 이 의복 공식을 그대로 사용하고 2.25를 추가로 곱하지 않는다. 실제 노동 완료 전달의 APPAREL-WORK-U01 문제가 있어 계산 WU를 실제 소요 시간으로 단정하지 않는다.
- 원본: [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [V23BalanceWorkCalculator.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/V23BalanceWorkCalculator.cs>), [V27BalanceWorkCalculator.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs>), [MaterialEconomicProfileSO.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Models/Economy/Content/MaterialEconomicProfileSO.cs>), [production.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md>), [species-culture-and-life.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md>), [V22_9301_재단_재봉_작업대.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9301_재단_재봉_작업대.asset>), [building-9301.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9301.json>)
- 확인한 심볼: CreateCraft / V27BalanceWorkCalculator.CalculateApparel / V23BalanceWorkCalculator.CalculateApparel / RoundTo / ResourceMaterialEconomicProfileCatalog.GetWorkFactor
- 판정: owner-gap-confirmed / global_deduplication=pending

## GAP-105 의복 품질의 작업 기여·시설 숙련·복잡도 보정 누락

- 분류: 공통 공식에 들어가는 의복별 수치 누락
- 보완할 문서: production-quality-and-supply
- 현재 문서: 품질 문서는 공통 점수 공식·등급 경계·고정 난수는 설명하지만, 의복에서 가중 스킬·시설 보정·도구 보정·복잡도에 어떤 값을 넣는지 설명하지 않는다.
- 추가·정정할 내용: 의복 제작에 기여한 작업자의 품질 능력은 해당 시설의 제작 작업 프로필로 평가한 performance:work:craft:quality 값에 58을 곱하고 0~100으로 제한한다. 각 기여 구간의 인정 WU로 이 값을 가중 평균한다. 시설 보정은 (시설 제작 숙련 점수-50)×0.08, 도구 보정은 현재 0, 복잡도 감점은 max(0,재단계수-1)×4다. 이 입력을 기존 품질 공식에 대입하고 공통 등급표·고정 난수 설명은 복제하지 않는다. 시작·작업 시점에는 자격을 갖춘 작업자의 최고 값과 난수 합 최대 30으로 목표 가능 여부를 검사하며, 불가능하면 예약을 풀고 목표 달성 불가 상태로 기다린다. 가중 값이 0일 때 50으로 바꾸는 분기는 의도 확인 사항 APPAREL-CRAFT-U03으로 분리한다.
- 원본: [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [V23CraftingPrimitives.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs>), [ApparelBuildingPanelPresenter.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ApparelBuildingPanelPresenter.cs>), [production-quality-and-supply.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md>), [species-culture-and-life.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/species-culture-and-life.md>)
- 확인한 심볼: GetApparelQualitySkill / CraftContributionAccumulator.Add / WeightedRelevantSkill / ResolveCraft / CanPotentiallyReachCraftQuality
- 판정: owner-gap-confirmed / global_deduplication=pending

## GAP-106 신들린 영감의 신화 제작 조건·반복 권태·성공 기분 수치 누락

- 분류: 특성 조건·확률·기분 효과 누락
- 보완할 문서: character/trait-300 (production-quality-and-supply에서 참조)
- 현재 문서: 신들린 영감 도감은 이 특성으로 신화 품질을 만들 수 있다는 요약과 정체성 규칙 1개만 표시한다. 확률, 제작 기여 조건, 같은 물품 반복 시 권태와 성공 보상은 공개 본문에 없다.
- 추가·정정할 내용: 의복 제작에서 마지막으로 실제 WU를 기여한 작업자가 신들린 영감을 갖고, 그 작업자의 총 기여 비율이 60% 이상이며 정의가 영감을 허용하면 신화 승격을 3% 확률로 판정한다. 비교에는 0.0001 허용오차가 있다. 현재 의복 56종 모두 영감을 허용한다. 판정은 런·주문·품목·시도·제작자·특성 ID에서 고정되며 일반 품질 점수가 전설을 넘어 자동으로 신화가 되는 구조가 아니다. 같은 품목을 연속 완성하면 3회째부터 권태 -2, 이후 한 번마다 -2씩 최대 -10을 1일간 적용한다. 다른 품목을 완성하거나 마지막 완료 후 48게임시간이 지나면 연속 횟수를 1로 다시 센다. 신화 성공은 기분 +10을 2일간 적용하고 연속 횟수를 초기화한다. 횟수 초기화가 기존 권태 효과의 즉시 제거를 뜻하지는 않는다. 이 수치는 특성 도감 한 곳에서 설명하고 품질 문서는 참조한다. 무기·방어구의 완료 조건은 해당 실행기 대조 후 범위를 확장한다.
- 원본: [Trait_300_possessed-inspiration.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/V26/Traits/Founder/Trait_300_possessed-inspiration.asset>), [trait-300.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/character/trait-300.json>), [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [V23CraftingPrimitives.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs>), [CharacterIdentityRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityRuntime.cs>), [production-quality-and-supply.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md>)
- 확인한 심볼: ExtremeCraftInspirationRule / ResolveCraft / MythicCraftInspirationRules / RecordEligibleCompletion
- 판정: owner-gap-confirmed / global_deduplication=pending

## GAP-107 의복 반복 제작의 안전 한도·목표 수량·작업 예산 구분 누락

- 분류: 주문 설정·중단 조건 누락
- 보완할 문서: production-quality-and-supply
- 현재 문서: 목표 품질 반복 절에는 목표 난도와 일반 XP 비율만 있다. 의복 UI의 안전 한도/목표 품질까지, 불합격품 처리 선택, 합격 수량과 작업 예산의 관계를 설명하지 않는다.
- 추가·정정할 내용: 의복 UI 기본값은 목표 품질 보통·합격 1벌·자동 분해·안전 한도이며 maximumAttempts=10, workBudget=0이다. 작업 예산과 합격 수량을 바꾸는 컨트롤은 현재 의복 패널에 없다. 안전 한도는 시도 수 또는 양수 작업 예산에 걸리면 중단하고, 목표 품질까지 모드는 이 두 한도를 적용하지 않는다. 예산에는 제작과 불합격품 해체 WU를 누적하며 판정은 각 작업이 끝난 뒤라 한 작업만큼 예산을 넘을 수 있다. 합격 목표 수량을 채우면 먼저 완료한다. 기본값 10을 '실제로 10회 제작한다'고 안내해서는 안 된다. 다음 시도 번호를 먼저 증가시키는 현재 분기는 모두 불합격인 경우 9회 완료 뒤 실패할 수 있고, 저장 번호 불일치도 만든다(APPAREL-CRAFT-U01). 신화는 목표 선택 UI에 없고 이론 최고 품질 검사도 전설까지만 다루므로 신화 자동 반복을 지원한다고 설명하지 않는다(APPAREL-CRAFT-U02).
- 원본: [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [ApparelBuildingPanelPresenter.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ApparelBuildingPanelPresenter.cs>), [production-quality-and-supply.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md>), [production.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md>)
- 확인한 심볼: ApparelCraftUiSettings / ResolveCraft / PrepareNextCraftAttempt / HasReachedApparelRepeatLimit / CaptureOrders / PrepareRestoreOrders
- 판정: owner-gap-confirmed / global_deduplication=pending

## GAP-108 의복 불합격품 보관·판매 대기·자동 분해의 결과와 회수량 누락

- 분류: 불합격품 처리·회수 비용 누락
- 보완할 문서: production-quality-and-supply (production에서 참조)
- 현재 문서: 생산과 품질 문서는 출력 공간 대기와 일반 회수 가치 상한만 설명한다. 의복의 세 가지 불합격품 처리 방식, 실제 원단 회수 수량과 해체 작업량은 없다.
- 추가·정정할 내용: 불합격품 보관은 완성품을 남기고 다음 제작을 준비하며, 판매 대기는 판매용 목적지로 넘기는 표시이지 즉시 판매·수입 확정이 아니다. 자동 분해는 이미 만든 의복 한 벌을 소비하고 원래 원단을 floor(투입 원단 수×0.5×max(0,최종 제작자의 회수 수율 배율))개 돌려준다. 배율 1에서 원단 1/2/3개를 쓴 의복의 회수는 각각 0/1/1개다. 해체 작업량은 max(0.1,해당 제작 WU×0.2)이고 해체 완료 뒤 다음 제작을 준비한다. 회수 공간이 없으면 대기하며, 회수 질량이 소비한 의복 질량을 넘으면 물리 처리에서 거부한다. 안전 한도·예산 검사를 자동 분해 전에도 수행하므로 이미 한도에 도달한 불합격품은 자동 분해되지 않고 남을 수 있다. 신화 품질은 판매 대기로 보내지 않는다. 실제 운반·판매 정산·저장 재개는 별도 실행 확인과 구분해 안내한다.
- 원본: [ApparelWorkOrderRuntime.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkOrderRuntime.cs>), [ApparelPhysicalTransaction.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Infrastructure/Environment/ApparelPhysicalTransaction.cs>), [ApparelSpecialThroughputContributor.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/ApparelSpecialThroughputContributor.cs>), [ApparelBuildingPanelPresenter.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Views/Buildings/UI/ApparelBuildingPanelPresenter.cs>), [V23CraftingPrimitives.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Foundation/V23CraftingPrimitives.cs>), [production-quality-and-supply.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production-quality-and-supply.md>), [production.md](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/content/guides/production.md>)
- 확인한 심볼: ResolveCraft / ResolveRejectedApparelDismantle / ResolveRejectedRecoveryWork / ExecuteRejectedDismantleOrResume / MarketDestinationId
- 판정: owner-gap-confirmed / global_deduplication=pending

## GAP-109 재단·재봉 작업대 도감의 건설 작업량이 현재 작성값과 불일치

- 분류: 도감 수치 불일치
- 보완할 문서: facility/building-9301 (시설 도감 전수 수치 대조로 확장)
- 현재 문서: 재단·재봉 작업대 도감 요약은 건설 작업량 249를 표시한다. 현재 루트 카탈로그에 연결된 시설 자산의 constructionWorkRequired는 306이다. 목재 6·철괴 3은 양쪽이 일치한다.
- 추가·정정할 내용: 재단·재봉 작업대의 건설 WU를 현재 작성 권위 306과 맞춰야 한다. 건설 재료 목재 6개·철괴 3개는 변경할 필요가 없다. 이 수치는 시설을 짓는 비용이며 의복 한 벌의 맞춤 제작 WU나 operateWorkRequired=10과 혼동하지 않도록 구분한다. 현행 V27 CalculateConstruction은 이 작성 건설 WU를 직접 반환한다. 지금 확인한 불일치는 시설 1개이며 다른 시설의 비용도 같은 원인인지 전체 시설 대조에서 확인한다.
- 원본: [V22_9301_재단_재봉_작업대.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Building/V22Apparel/V22_9301_재단_재봉_작업대.asset>), [building-9301.json](<F:/01_Programming/01_Project/02_Unity/DungeonStory/wiki/game-versions/0.0.1v/data/entities/facility/building-9301.json>), [V27BalanceWorkCalculator.cs](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Scripts/Services/Economy/V27BalanceWorkCalculator.cs>), [GameDomainContentCatalog.asset](<F:/01_Programming/01_Project/02_Unity/DungeonStory/Assets/Resources/SO/Content/GameDomainContentCatalog.asset>)
- 확인한 심볼: BuildingWorkAmountAbility.constructionWorkRequired / V27BalanceWorkCalculator.CalculateConstruction
- 판정: owner-gap-confirmed / global_deduplication=pending

## 의복별 맞춤 제작 산술 목록

원단 수량과 면적 단계·점유 부위는 현재 작성 필드로 계산했다. WU 범위는 허용 원단, 소형/중형/대형, 지원 개조 조합을 독립 산술로 열거한 값이다. 5,454개 조합 열거를 실제 제작 성공 건수로 세지 않는다. 실제 노동 완료 전달 문제는 APPAREL-WORK-U01에 남아 있다.

| 의복 | 원단 개수 | 면적 단계 | 점유 부위 수 | 허용 원단 종류 | 독립 계산 WU 범위 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 작업 셔츠 | 2 | 1 | 3 | 10 | 24~56 |
| 날개 멜빵 | 2 | 1 | 3 | 12 | 24~46 |
| 날개 망토 | 2 | 1 | 3 | 10 | 24~46 |
| 무기 철야 망토 | 3 | 2 | 2 | 10 | 30~66 |
| 방수 작업복 | 3 | 2 | 7 | 12 | 44~104 |
| 조끼 | 2 | 1 | 1 | 10 | 18~46 |
| 속셔츠 | 2 | 1 | 1 | 10 | 18~46 |
| 튜닉 | 2 | 1 | 3 | 10 | 24~56 |
| 바지 | 2 | 1 | 3 | 10 | 24~50 |
| 꼬리 리본 | 2 | 1 | 1 | 10 | 18~34 |
| 꼬리 보호대 | 2 | 1 | 1 | 12 | 18~36 |
| 외과 앞치마 | 3 | 2 | 2 | 12 | 30~56 |
| 무균 가운 | 2 | 1 | 3 | 10 | 24~56 |
| 포자 방호 두건 | 3 | 2 | 2 | 10 | 30~60 |
| 포자 정원 망토 | 3 | 2 | 2 | 10 | 30~66 |
| 양말 | 2 | 1 | 2 | 10 | 20~40 |
| 연기 방호 두건 | 3 | 2 | 2 | 10 | 30~60 |
| 대장장이 앞치마 | 3 | 2 | 2 | 12 | 30~56 |
| 보온 점액 패드 | 3 | 2 | 1 | 10 | 26~50 |
| 잠옷 상의 | 2 | 1 | 3 | 10 | 24~56 |
| 잠옷 하의 | 2 | 1 | 3 | 10 | 24~50 |
| 하늘 합창 숄 | 2 | 1 | 2 | 10 | 20~40 |
| 치마 | 2 | 1 | 3 | 10 | 24~46 |
| 반바지 | 2 | 1 | 3 | 10 | 24~50 |
| 목도리 | 2 | 1 | 1 | 10 | 18~34 |
| 룬 방한복 | 3 | 2 | 7 | 12 | 44~104 |
| 의식 로브 | 3 | 2 | 7 | 10 | 44~102 |
| 우비 | 3 | 2 | 7 | 12 | 44~104 |
| 상복 | 2 | 1 | 7 | 10 | 34~86 |
| 광부 작업복 | 3 | 2 | 7 | 12 | 44~104 |
| 하의 속옷 | 2 | 1 | 1 | 10 | 18~40 |
| 내의 바지 | 2 | 1 | 3 | 10 | 24~50 |
| 허리 두름 속옷 | 2 | 1 | 1 | 10 | 18~34 |
| 사육사 외투 | 3 | 2 | 7 | 12 | 44~104 |
| 뿔 고리 | 1 | 1 | 1 | 12 | 18~36 |
| 후드 로브 | 3 | 2 | 8 | 10 | 46~108 |
| 내열 작업복 | 3 | 2 | 7 | 12 | 44~104 |
| 운반 멜빵 | 2 | 1 | 2 | 12 | 20~40 |
| 모자 | 2 | 1 | 1 | 10 | 18~38 |
| 골렘 기능성 내피 | 2 | 1 | 7 | 12 | 34~68 |
| 장갑 | 2 | 1 | 2 | 12 | 20~40 |
| 정장 외투 | 3 | 2 | 4 | 10 | 34~78 |
| 발싸개 | 2 | 1 | 2 | 10 | 20~40 |
| 축제 조끼 | 2 | 1 | 1 | 10 | 18~46 |
| 농부 작업복 | 2 | 1 | 7 | 10 | 34~86 |
| 사절 외투 | 3 | 2 | 4 | 10 | 34~78 |
| 일상 로브 | 2 | 1 | 7 | 10 | 34~86 |
| 계약 어깨띠 | 2 | 1 | 1 | 10 | 18~34 |
| 방한 작업복 | 3 | 2 | 7 | 12 | 44~104 |
| 망토 | 2 | 1 | 2 | 12 | 20~52 |
| 가슴 감개 | 2 | 1 | 1 | 10 | 18~34 |
| 예복 드레스 | 3 | 2 | 7 | 10 | 44~102 |
| 장화 | 2 | 1 | 2 | 12 | 20~40 |
| 블라우스 | 2 | 1 | 3 | 10 | 24~56 |
| 허리띠 | 2 | 1 | 1 | 12 | 18~36 |
| 앞치마 | 2 | 1 | 2 | 12 | 20~40 |

전체 입력·경로·해시는 [기계 판독 상세](apparel-crafting-review.json)에 있다. 56종 모두 신화 영감을 허용하며 도감 facts는 각각 무게·적재·가격 3개다.

## 원단 작업 배율

별도 MaterialEconomicProfileSO 작성 자산은 Resources의 MonoScript GUID 검색에서 나오지 않았다. 아래 값은 등록된 자원 아이템의 종류·태그·무게에 따른 유도식으로 계산했다. 전투용 CraftMaterialDefinitionSO의 배율과 구별한다.

| 물리 원단 ID | 작업 배율(반올림 표시) |
| --- | ---: |
| material:spore-hemp | 0.932622 |
| material:cloth | 0.932258 |
| material:rune-leather | 1.038371 |
| material:mire-canvas | 0.934647 |
| material:leather | 0.939637 |
| material:frost-wool | 0.934197 |
| material:frost-linen | 0.930864 |
| material:ember-cotton | 0.931344 |
| material:dreamweave | 1.028931 |
| material:deep-goat-wool | 0.934327 |
| material:common-wool | 0.933667 |
| material:cave-silk | 0.928445 |

재단·재봉 작업대는 명령39·재봉 태그·출력버퍼4회를 가지며 도메인 카탈로그에 등록되어 있다. 도감 건설 WU249와 작성306의 불일치는 GAP-109다. 의복 제작 WU와 시설 건설 WU, 운영10 WU는 서로 다른 값이다.

## 구현 확인 사항

### APPAREL-CRAFT-U01 반복 종료의 시도 번호 증가와 저장 검증 불일치

PrepareNextCraftAttempt는 qualityAttemptIndex를 먼저 증가시키고 한도에 걸리면 qualityRoll 갱신 전에 Failed로 반환한다. ApplyWork는 Failed를 유지하고 CaptureOrders는 이 주문도 저장한다. PrepareRestoreOrders는 두 attemptIndex가 다르면 예외를 던진다. maxAttempts10·전부 불합격·예산0 모델은 실제 완료9회, orderIndex9/rollIndex8이다.

판정: static-counterexample; actual save roundtrip not run.

### APPAREL-CRAFT-U02 신화 목표 반복은 UI와 사전 가능 판정에서 지원되지 않음

품질 올리기 버튼은6(전설)까지다. 사전 가능 판정도 일반 FromScore를 사용해 전설까지만 산출하고 영감 승격을 계산하지 않는다. 실제 완성품의 신화3% 경로와 목표 품질 반복은 구분해야 한다.

판정: static-path-confirmed; runtime scenario not run.

### APPAREL-CRAFT-U03 의복 품질의 0점 기여가 50점으로 대체됨

GetApparelQualitySkill은0까지 허용하지만 ResolveCraft는 WeightedRelevantSkill>0인 경우에만 그 값을 쓴다. 그렇지 않으면50을 넣는다. 정말0인 기여와 기여 기록 부재가 같은 처리이며 목표 가능 판정은 이 대체를 하지 않는다.

판정: intent-unconfirmed; no fallback introduced by this audit.

### APPAREL-CRAFT-U04 설계 수치 원장의 생산식 단일 권위 설명과 맞춤 의복 명령의 계산 차이

apparel-and-module-numeric-notes.md는 의복 BOM/WU가 ProductionRecipeSO에만 있다고 적지만 CreateCraft는 재단계수로 수량을 계산하고 등록된 V27→V23 의복 공식을 호출한다. 두 제작 경로의 의도·해금·공개 관계를 추가 대조해야 한다.

판정: authority-text-mismatch; other recipe/caller coverage remains open.

### APPAREL-CRAFT-U05 영감 시도 자격과 반복 기분 기록의 조건 차이

신화 판정에는60% 기여와 정의 허용 검사가 있지만 RecordEligibleCompletion 호출은 최종 작업자에게 규칙이 있는지만 확인한다. 연속 횟수 초기화 분기에는 기존 권태 효과 제거 호출이 없다. 기분 서비스의 기존 효과 갱신·만료 및 장비 제작과의 공유 상태는 미검토다.

판정: static-condition-difference; mood effect lifecycle remains open.

## 검증과 확인 한계

- 이전 원본 3,389개·산출물 26개 해시 변경 0. 이번 의복 56종의 선택 필드 280개를 현재 자산과 다시 대조해 오류 0.
- 이번 직접 근거 경로 159개. 해시 수집은 파일 전체 완독이나 플레이 경로 전체 검증을 뜻하지 않는다. 읽은 범위는 JSON readScope에 기록했다.
- 독립 산술 27건 오류 0. 원단 올림·회수 내림·기여 평균·품질 보정·권태·3% 경계·반복 횟수/저장 번호 반례를 포함한다. C# 실행 테스트가 아니다.
- 현재 원본의 의복 필드280개·원단 물리 아이템 필드48개·영감 규칙8개를 보고서와 대조해 총336개 오류0이다. PowerShell로 5,454개 조합을 별도 계산해56종의 개수·WU 범위를 대조한 결과도 오류0이다. 단정밀 경계와 실제 Unity 결과까지 인증한 것은 아니다.
- KB query: `ApparelCraftOrder CraftQuality ApparelRejectedDismantle`, areas `code, content, authority`, limit8, session43342, exit1, stale4건, 생성 행0개.
- Content source digest: `139a0a989275ecdd5a4a26c10ceb6a1931041c7c928ed0421628faea5cd928c6`.
- Knowledge-base source digest: `ceef8dc8f25f4d327205b15e12346aee0ebc5d6a84aa7eeb1f08af5ce14db0dd`.
- 생성물을 최신 근거로 사용하거나 재생성하지 않았다. 스크립트·자산·공개 위키·서버 변경 없음. 밸런스 영향 없음: 감사 산출물만 작성했다.

## 다음 조사

- 전체 시설 건설 WU·BOM과 도감 전수 비교
- 의복 오염·마모 생성자와 착용·교체 관리
- 실제 노동/품질 프로필·기분 효과 수명주기·영감 장비 경로
- 의복 연구 해금·관련 생산식과 맞춤 제작의 역할 구분
- 날씨의 작물·축산·원정 효과와 남은 전체 시스템/도감 의미 대조
- 최종 전역 의미 중복 제거 및 전체 모집단 미검토0 검증
