# 공개 엔티티 facts·relations 전수 대조

상태: **전수 구조 대조 완료, 의미 차이는 WIM 원장에 귀속**. 이 보고서는 공개 위키 2,905개 엔티티의 구조화된 facts·relations가 현재 작성 권위와 같은지, 각 원본 타입에 production 소비자가 존재하는지를 전수 확인한다. Unity Play Mode 실행 증거를 대신하지 않는다.

## 모집단과 재생성 증거

- 현재 Authority에서 별도 조사 루트로 content DB를 재생성했다: source record 3,476, source relation 6,346, parse error 0.
- content source digest: `76cce09a76ded556dc74ac400c571e92136f17f86fc3697dfb9eb30dca669871`.
- fresh content output digest: `7b9090b99aca7196d49a56b2d78c41ed45016fe40507e8f6c4cccdf61f50032a`.
- 같은 content DB와 현재 위키 generator로 `0.0.1v`를 별도 조사 루트에 재생성했다: 공개 엔티티 2,905, facts 6,364, relations 3,846.
- fresh wiki digest: `b1ffc64dc9eb85791fb843bbb15bd7a4a2a96ccde2367e8021ab342931958911`.
- 기존 공개 엔티티와 fresh projection의 typed URL 집합은 2,905/2,905로 같고 신규·누락 URL은 0이다.
- 구조 비교 결과: title 차이 0, relation 차이 0, fact 차이 198, summary 차이 144.
- fact 차이 198개는 모두 아이템 `기준 가격`이며 WIM-058이 소유한다. 아이템 무게·한 칸 적재 2,150개는 일치한다.
- summary 차이 144개는 모두 일반 시설의 건설 WU 또는 BOM이며 WIM-059가 소유한다.
- 기후 5개의 숫자는 같지만 `annualAmplitudeC`를 연교차라고 부르는 의미 오류는 양쪽 generator 입력에도 있으므로 byte 대조로 검출되지 않는다. 런타임 계절 공식과 직접 비교한 WIM-060이 소유한다.

`runtime_confirmed`는 content DB의 타입 참조·save 참조를 출발점으로 삼되, 아래 표의 대표 소비자는 비 Editor C# 원본을 직접 연 경로다. “현재 projection 일치”는 작성값의 최신성 증거이며, 해당 설명의 모든 효과가 실제 플레이에서 완성됐다는 뜻은 아니다. 닫히지 않은 효과·UI·저장 의미는 마지막 열의 WIM으로 분리했다.

## 56개 공개 원본 타입 전수표

| 원본 타입 | 종류 | 엔티티 | facts | relations | 대표 production 소비자 | 판정 / 관련 WIM |
| --- | --- | ---: | ---: | ---: | --- | --- |
| AgeConditionDefinitionSO | character | 6 | 0 | 0 | `CharacterSpeciesCatalog`, character age runtime/view | 구조 일치 |
| CareerPositionDefinitionSO | character | 6 | 0 | 0 | `CareerPositionDefinitionCatalog` | 구조 일치 |
| CharacterAmbitionDefinitionSO | character | 18 | 18 | 0 | `CharacterNarrativeCatalog`, run runtime | 구조 일치 |
| CharacterBackgroundDefinitionSO | character | 12 | 12 | 0 | `CharacterNarrativeAggregate`, offense runtime | 구조 일치 |
| CharacterSO | character | 14 | 0 | 21 | `GameManager`, character/invasion/offense/recruitment runtime | projection 일치; 시작 UI WIM-010·042 |
| CharacterSpeciesSO | character | 10 | 9 | 30 | `CharacterSpeciesCatalog`, medical/effects/economy runtime | projection 일치; 환경 적응 WIM-004 |
| CharacterStartingHistorySO | character | 9 | 27 | 0 | `CharacterStartingProfileDomain` | projection 일치; 시작 UI WIM-010·042 |
| CharacterStartingOriginSO | character | 6 | 6 | 0 | `CharacterStartingHistorySO` runtime | projection 일치; 시작 UI WIM-010·042 |
| CharacterTraitSO | character | 100 | 349 | 0 | character AI/effects/offense/run runtime | projection 일치; 일부 파생효과 WIM-006·010 |
| FestivalDefinitionSO | character | 12 | 0 | 12 | `FestivalDefinitionCatalog`, campaign runtime | projection 일치; 참가·현장 작업 WIM-041 |
| FuneralCultureSO | character | 10 | 0 | 0 | species definition, funeral runtime | 구조 일치 |
| HeritableTraitDefinitionSO | character | 24 | 24 | 0 | narrative/offense runtime | 구조 일치 |
| ProficiencyDefinitionSO | character | 9 | 0 | 0 | `CharacterNarrativeAggregate`, character view | 구조 일치 |
| ReproductionProfileSO | character | 10 | 0 | 0 | `CharacterSpeciesCatalog` | 구조 일치 |
| SpeciesCultureDefinitionSO | character | 10 | 0 | 27 | `CharacterCultureGameplayRuntime`, survival/offense runtime | projection 일치; 축제 현장 WIM-041 |
| SpeciesLifeHistorySO | character | 10 | 0 | 0 | `CharacterSpeciesCatalog` | 구조 일치 |
| AnatomyProfileSO | medical | 12 | 0 | 5 | medical anatomy models, combat/medical runtime | projection 일치; 종족 이식 WIM-023 |
| DiseaseDefinitionSO | medical | 16 | 0 | 0 | `PopulationHealthRuntime`, run/infrastructure runtime | 직접 배율·해독 WIM-005·028 |
| SurgicalProcedureSO | medical | 47 | 141 | 101 | surgery contracts, medical/character runtime, UI | 수치·관계 일치; WIM-021~028 |
| BattlefieldModifierDefinitionSO | world | 12 | 0 | 0 | offense battle/run runtime | 구조 일치 |
| CombatArmorSO | combat | 21 | 62 | 56 | `CombatEquipmentLoadoutRuntime`, offense runtime | projection 일치; 질량 WIM-020 |
| CombatShieldSO | combat | 9 | 35 | 23 | `CombatEquipmentLoadoutRuntime`, defense runtime | projection 일치; 질량 WIM-020 |
| CombatWeaponSO | combat | 31 | 121 | 197 | combat loadout/ammunition/economy/offense runtime | projection 일치; 질량 WIM-020 |
| EnemyAbilityDefinitionSO | world | 18 | 18 | 0 | `EnemyEncounterFactory` | 구조 일치 |
| EnemyArchetypeDefinitionSO | world | 36 | 0 | 109 | invasion director, offense runtime | projection 일치; 경고·대피·스케일 WIM-047~049 |
| OffenseEncounterSO | world | 36 | 0 | 72 | offense authored port/run/item runtime | projection 일치; 원정 결산 WIM-053 |
| OffenseSiteArchetypeSO | world | 12 | 48 | 0 | offense authored port, faction/invasion runtime | projection 일치; 원정 결산 WIM-053 |
| OffenseUrgentSiteDefinitionSO | world | 6 | 12 | 0 | offense authored port/item runtime | projection 일치; 원정 결산 WIM-053 |
| WildlifeSpeciesSO | nature | 18 | 0 | 69 | wildlife/economy/medical/offense/run runtime | 종별 이동·산업·갑각 WIM-019·029·030 |
| ClimateZoneDefinitionSO | nature | 5 | 5 | 0 | `ClimateRuntime` | 숫자 일치; 표시 의미 WIM-060, 이동 배율 WIM-017 |
| WeatherFrontDefinitionSO | nature | 6 | 42 | 0 | `ClimateRuntime`, survival runtime, view | 수치 일치; 원정 이동 WIM-017 |
| CropDefinitionSO | nature | 12 | 12 | 24 | crop/economy/infrastructure runtime | projection 일치; 생육 규칙 WIM-016·043 |
| CropGenomeDefinitionSO | nature | 32 | 0 | 0 | crop definition/economy runtime | 구조 일치 |
| CulturalPracticeDefinitionSO | event | 20 | 20 | 53 | narrative catalog/campaign runtime | projection 일치; 사건 도메인 WIM-037~041 |
| EndingDefinitionSO | event | 9 | 0 | 0 | `V20CampaignRuntime` | 구조 일치; 런 결산 WIM-050 |
| FactionArcDefinitionSO | event | 6 | 0 | 72 | `V20CampaignRuntime` | projection 일치; 사건 도메인 WIM-037~040 |
| FactionChapterDefinitionSO | event | 36 | 0 | 72 | `V20CampaignApplicationAdapter` | projection 일치; 사건 도메인 WIM-037~040 |
| FactionContractDefinitionSO | event | 18 | 36 | 18 | faction/run/economy runtime | 요구물품·수량 일치; 진입·납품·결제 WIM-032~034 |
| GuestRequestDefinitionSO | event | 14 | 28 | 13 | economy catalog/campaign runtime | projection 일치; 실행·빈도·UI WIM-037~040 |
| LifeEventDefinitionSO | event | 32 | 32 | 49 | narrative aggregate/campaign runtime | projection 일치; 실행·빈도·UI WIM-037~040·044 |
| SeasonalWorldEventDefinitionSO | event | 28 | 84 | 7 | `V20CampaignRuntime` | authored 수치 일치; pressure·농업 WIM-009·043 |
| ServiceIncidentDefinitionSO | event | 8 | 0 | 18 | campaign application/runtime | 실행·빈도·UI WIM-037~040 |
| GenericItemDefinitionSO | item | 710 | 2,130 | 0 | item/survival/combat/medical/captivity runtime | 무게·적재 일치; 가격 WIM-058, 소비 WIM-035·036 |
| ResourceItemDefinitionSO | item | 365 | 1,095 | 0 | item/economy/infrastructure/medical/offense runtime | 무게·적재 일치; 가격 WIM-058 |
| ApparelDefinitionSO | combat | 56 | 56 | 56 | apparel/economy/infrastructure runtime, view | 환복·상태·질량 WIM-001·003·020 |
| EnvironmentalWorkwearSO | combat | 4 | 4 | 8 | economy/infrastructure/item runtime | projection 일치; 적응·질량 WIM-004·020 |
| EquipmentModuleDefinitionSO | combat | 20 | 0 | 0 | combat/effects runtime, view | projection 일치; 자동 품질 WIM-006 |
| BuildingSO | facility | 398 | 796 | 0 | building controller와 20개 이상 gameplay service | facts 일치; summary 144개 WIM-059, 시설별 의미 WIM-006~014·031·045·046 |
| FacilityBlueprintSO | facility | 7 | 0 | 7 | blueprint unlock/shop/research runtime | 구조 일치 |
| FacilityEvolutionRecipeSO | facility | 6 | 0 | 22 | facility evolution runtime/view | projection 일치 |
| FacilitySynthesisRecipeSO | facility | 9 | 0 | 27 | synthesis/infrastructure/codex runtime | projection 일치; drain/retarget WIM-057 |
| ServiceProcessSO | facility | 5 | 0 | 0 | service-room/item runtime | 구조 일치 |
| CraftMaterialDefinitionSO | world | 12 | 12 | 12 | item/economy/combat/defense runtime | projection 일치 |
| TextileMaterialDefinitionSO | world | 12 | 12 | 12 | apparel/economy/infrastructure runtime | 보호 소비 WIM-002 |
| ProductionRecipeSO | recipe | 355 | 758 | 1,353 | production/economy/infrastructure/combat runtime | authored graph 일치; 품질 경고 WIM-056, 일부 소비 WIM-035 |
| ResearchProjectSO | research | 180 | 360 | 1,301 | research/effects/invasion/offense/run runtime, UI | work·graph 일치; 예상 기간 WIM-015 |

합계는 엔티티 **2,905**, facts **6,364**, relations **3,846**, 구조화 claim **10,210**이다. 표의 56개 타입 합계와 파일 전수 합계가 일치한다.

## 공개 제외 타입과 경계

- 기술 내부형 8개는 generator 정책상 공개하지 않는다: `AnatomyConditionLexiconSO`, `CharacterFunctionalCapacityDefinitionSO`, `CharacterPerformanceFormulaDefinitionSO`, `FacilityEvolutionRecordTokenDefinitionSO`, `GameplayEffectConditionDefinitionSO`, `GameplayEffectDefinitionSO`, `OffenseDecisionCardSO`, `ResearchUnlockBundleDefinitionSO`.
- 방어 effect 내부형 7개는 runtime 확인이 없어 공개하지 않는다: burn, charge, corrosion, damage, guard-attack, slow 계열 정의.
- `DungeonFactionDefinitionSO` 12 source row는 `manual-review`, `SaleItem` 4 row는 `non-active-lifecycle`로 전부 제외된다. 따라서 공개 원본 타입 수는 58이 아니라 **56**이다.
- 제외 자체가 정당한지는 역방향 문서 공백 원장의 소유 범위이며, 이 위키→구현 WIM 원장에서는 구현 누락으로 중복 집계하지 않는다.

## 결론

현재 public entity의 구조화된 10,210개 claim은 198개 가격을 제외하면 현재 Authority에서 재생성한 값과 일치한다. 관계 3,846개는 전부 동일하다. 다만 relation이 존재한다는 사실은 그 효과가 실제 도메인 명령으로 완성됐다는 증명이 아니므로, 사건·계절 pressure·의료·원정 등 의미 단절은 WIM에 계속 남겼다. 시설 summary 144개와 기후 의미 5개도 구조화 fact 밖의 별도 차이로 등록했다.

게임 코드, ScriptableObject, 공개 위키는 수정하지 않았다.
