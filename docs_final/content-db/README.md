# DungeonStory 콘텐츠 데이터베이스

현재 Unity 작성 자산과 C# 직렬화 권위를 정적으로 교차해 생성한 콘텐츠 색인이다. 유형별 DB는 작성 필드, 관계, 런타임 도달 근거, 전략적 역할과 제거 영향을 분리해 제공한다.

행 기본키는 `record_key`다. 일반적으로 `콘텐츠 유형|안정 ID`이며, 같은 유형과 ID를 공유하는 자산이 실제로 공존하면 소스 경로까지 붙여 구분한다.

## 데이터베이스 구성

- [아이템](01-items.md)
- [생산·시설](02-production-and-facilities.md)
- [인물·특성·사회](03-characters-traits-and-society.md)
- [사건·캠페인](04-events-and-campaign.md)
- [연구·효과·진행](05-research-effects-and-progression.md)
- [전투·건강·세계](06-combat-health-and-world.md)
- [콘텐츠 유형별 CSV 색인](content-type-index.csv)
- [스키마](schema.md)
- [직렬화 enum 사전](enum-dictionary.md)
- [런타임 도달성 감사](runtime-coverage.md)
- [관계 감사](relationship-audit.md)
- [콘텐츠 참조 결함 후보](reference-gaps.md)
- [역할 중첩 감사](content-overlap-audit.md)
- 유형별 콘텐츠 CSV: `csv/<영역>/<유형>.csv`
- 유형별 전체 작성 필드 CSV: `fields/<영역>/<유형>.csv`
- 유형별 관계 CSV: `relations/<영역>/<유형>.csv`
- 유형별 역참조 CSV: `incoming/<영역>/<유형>.csv`
- 유형별 코드 소비처 CSV: `code-consumers/<영역>/<유형>.csv`
- [수동 검토 목록](manual-review.csv)
- [제외 자산 유형 감사](excluded-asset-types.csv)
- [별도 런타임 도메인 ID](runtime-domain-targets.csv)
- [파싱 오류](parse-errors.csv)
- [생성 요약 JSON](content-db-summary.json)
- [원본 파일 manifest](source-files.csv)
- [생성물 manifest](output-files.csv)
- [생성 상태](generation-manifest.json)

## 현재 스냅샷

- 전체 콘텐츠 행: 3,476
- 관계 행: 6,346
- 콘텐츠 유형별 CSV: 73개
- 관계 유형별 CSV: 73개
- 코드 소비처 근거: 1,226건
- 런타임 정적 근거 확인: 3,253
- 근거 확인: 3,427
- 수동 검토 필요: 49
- 동일 유형·안정 ID 중복 그룹: 10
- 파싱 오류: 0

| 영역 | 행 수 |
|---|---:|
| 아이템 | 1,090 |
| 생산·시설 | 958 |
| 인물·특성 | 288 |
| 사건·캠페인 | 194 |
| 연구·효과 | 589 |
| 전투·건강·세계 | 357 |

## 유형별 CSV

| 영역 | 콘텐츠 유형 | 콘텐츠 | 문서 | 작성 필드 | 관계 | 역참조 | 코드 소비처 |
|---|---|---:|---|---|---|---|---|
| 인물·특성 | `AgeConditionDefinitionSO` | 6 | [열기](types/characters-traits/age-condition/README.md) | [CSV](fields/characters-traits/age-condition.csv) | [CSV](relations/characters-traits/age-condition.csv) | [CSV](incoming/characters-traits/age-condition.csv) | [CSV](code-consumers/characters-traits/age-condition.csv) |
| 전투·건강·세계 | `AnatomyConditionLexiconSO` | 7 | [열기](types/combat-health-world/anatomy-condition-lexicon/README.md) | [CSV](fields/combat-health-world/anatomy-condition-lexicon.csv) | [CSV](relations/combat-health-world/anatomy-condition-lexicon.csv) | [CSV](incoming/combat-health-world/anatomy-condition-lexicon.csv) | [CSV](code-consumers/combat-health-world/anatomy-condition-lexicon.csv) |
| 전투·건강·세계 | `AnatomyProfileSO` | 12 | [열기](types/combat-health-world/anatomy-profile/README.md) | [CSV](fields/combat-health-world/anatomy-profile.csv) | [CSV](relations/combat-health-world/anatomy-profile.csv) | [CSV](incoming/combat-health-world/anatomy-profile.csv) | [CSV](code-consumers/combat-health-world/anatomy-profile.csv) |
| 생산·시설 | `ApparelDefinitionSO` | 56 | [열기](types/production-facilities/apparel/README.md) | [CSV](fields/production-facilities/apparel.csv) | [CSV](relations/production-facilities/apparel.csv) | [CSV](incoming/production-facilities/apparel.csv) | [CSV](code-consumers/production-facilities/apparel.csv) |
| 전투·건강·세계 | `BattlefieldModifierDefinitionSO` | 12 | [열기](types/combat-health-world/battlefield-modifier/README.md) | [CSV](fields/combat-health-world/battlefield-modifier.csv) | [CSV](relations/combat-health-world/battlefield-modifier.csv) | [CSV](incoming/combat-health-world/battlefield-modifier.csv) | [CSV](code-consumers/combat-health-world/battlefield-modifier.csv) |
| 생산·시설 | `BuildingSO` | 419 | [열기](types/production-facilities/building/README.md) | [CSV](fields/production-facilities/building.csv) | [CSV](relations/production-facilities/building.csv) | [CSV](incoming/production-facilities/building.csv) | [CSV](code-consumers/production-facilities/building.csv) |
| 인물·특성 | `CareerPositionDefinitionSO` | 6 | [열기](types/characters-traits/career-position/README.md) | [CSV](fields/characters-traits/career-position.csv) | [CSV](relations/characters-traits/career-position.csv) | [CSV](incoming/characters-traits/career-position.csv) | [CSV](code-consumers/characters-traits/career-position.csv) |
| 인물·특성 | `CharacterAmbitionDefinitionSO` | 18 | [열기](types/characters-traits/character-ambition/README.md) | [CSV](fields/characters-traits/character-ambition.csv) | [CSV](relations/characters-traits/character-ambition.csv) | [CSV](incoming/characters-traits/character-ambition.csv) | [CSV](code-consumers/characters-traits/character-ambition.csv) |
| 인물·특성 | `CharacterBackgroundDefinitionSO` | 12 | [열기](types/characters-traits/character-background/README.md) | [CSV](fields/characters-traits/character-background.csv) | [CSV](relations/characters-traits/character-background.csv) | [CSV](incoming/characters-traits/character-background.csv) | [CSV](code-consumers/characters-traits/character-background.csv) |
| 연구·효과 | `CharacterFunctionalCapacityDefinitionSO` | 14 | [열기](types/research-effects/character-functional-capacity/README.md) | [CSV](fields/research-effects/character-functional-capacity.csv) | [CSV](relations/research-effects/character-functional-capacity.csv) | [CSV](incoming/research-effects/character-functional-capacity.csv) | [CSV](code-consumers/research-effects/character-functional-capacity.csv) |
| 연구·효과 | `CharacterPerformanceFormulaDefinitionSO` | 107 | [열기](types/research-effects/character-performance-formula/README.md) | [CSV](fields/research-effects/character-performance-formula.csv) | [CSV](relations/research-effects/character-performance-formula.csv) | [CSV](incoming/research-effects/character-performance-formula.csv) | [CSV](code-consumers/research-effects/character-performance-formula.csv) |
| 인물·특성 | `CharacterSO` | 15 | [열기](types/characters-traits/character/README.md) | [CSV](fields/characters-traits/character.csv) | [CSV](relations/characters-traits/character.csv) | [CSV](incoming/characters-traits/character.csv) | [CSV](code-consumers/characters-traits/character.csv) |
| 인물·특성 | `CharacterSpeciesSO` | 10 | [열기](types/characters-traits/character-species/README.md) | [CSV](fields/characters-traits/character-species.csv) | [CSV](relations/characters-traits/character-species.csv) | [CSV](incoming/characters-traits/character-species.csv) | [CSV](code-consumers/characters-traits/character-species.csv) |
| 인물·특성 | `CharacterStartingHistorySO` | 9 | [열기](types/characters-traits/character-starting-history/README.md) | [CSV](fields/characters-traits/character-starting-history.csv) | [CSV](relations/characters-traits/character-starting-history.csv) | [CSV](incoming/characters-traits/character-starting-history.csv) | [CSV](code-consumers/characters-traits/character-starting-history.csv) |
| 인물·특성 | `CharacterStartingOriginSO` | 6 | [열기](types/characters-traits/character-starting-origin/README.md) | [CSV](fields/characters-traits/character-starting-origin.csv) | [CSV](relations/characters-traits/character-starting-origin.csv) | [CSV](incoming/characters-traits/character-starting-origin.csv) | [CSV](code-consumers/characters-traits/character-starting-origin.csv) |
| 인물·특성 | `CharacterTraitSO` | 113 | [열기](types/characters-traits/character-trait/README.md) | [CSV](fields/characters-traits/character-trait.csv) | [CSV](relations/characters-traits/character-trait.csv) | [CSV](incoming/characters-traits/character-trait.csv) | [CSV](code-consumers/characters-traits/character-trait.csv) |
| 사건·캠페인 | `ClimateZoneDefinitionSO` | 5 | [열기](types/events-campaign/climate-zone/README.md) | [CSV](fields/events-campaign/climate-zone.csv) | [CSV](relations/events-campaign/climate-zone.csv) | [CSV](incoming/events-campaign/climate-zone.csv) | [CSV](code-consumers/events-campaign/climate-zone.csv) |
| 전투·건강·세계 | `CombatArmorSO` | 21 | [열기](types/combat-health-world/combat-armor/README.md) | [CSV](fields/combat-health-world/combat-armor.csv) | [CSV](relations/combat-health-world/combat-armor.csv) | [CSV](incoming/combat-health-world/combat-armor.csv) | [CSV](code-consumers/combat-health-world/combat-armor.csv) |
| 전투·건강·세계 | `CombatShieldSO` | 9 | [열기](types/combat-health-world/combat-shield/README.md) | [CSV](fields/combat-health-world/combat-shield.csv) | [CSV](relations/combat-health-world/combat-shield.csv) | [CSV](incoming/combat-health-world/combat-shield.csv) | [CSV](code-consumers/combat-health-world/combat-shield.csv) |
| 전투·건강·세계 | `CombatWeaponSO` | 31 | [열기](types/combat-health-world/combat-weapon/README.md) | [CSV](fields/combat-health-world/combat-weapon.csv) | [CSV](relations/combat-health-world/combat-weapon.csv) | [CSV](incoming/combat-health-world/combat-weapon.csv) | [CSV](code-consumers/combat-health-world/combat-weapon.csv) |
| 생산·시설 | `CraftMaterialDefinitionSO` | 12 | [열기](types/production-facilities/craft-material/README.md) | [CSV](fields/production-facilities/craft-material.csv) | [CSV](relations/production-facilities/craft-material.csv) | [CSV](incoming/production-facilities/craft-material.csv) | [CSV](code-consumers/production-facilities/craft-material.csv) |
| 생산·시설 | `CropDefinitionSO` | 12 | [열기](types/production-facilities/crop/README.md) | [CSV](fields/production-facilities/crop.csv) | [CSV](relations/production-facilities/crop.csv) | [CSV](incoming/production-facilities/crop.csv) | [CSV](code-consumers/production-facilities/crop.csv) |
| 생산·시설 | `CropGenomeDefinitionSO` | 32 | [열기](types/production-facilities/crop-genome/README.md) | [CSV](fields/production-facilities/crop-genome.csv) | [CSV](relations/production-facilities/crop-genome.csv) | [CSV](incoming/production-facilities/crop-genome.csv) | [CSV](code-consumers/production-facilities/crop-genome.csv) |
| 사건·캠페인 | `CulturalPracticeDefinitionSO` | 20 | [열기](types/events-campaign/cultural-practice/README.md) | [CSV](fields/events-campaign/cultural-practice.csv) | [CSV](relations/events-campaign/cultural-practice.csv) | [CSV](incoming/events-campaign/cultural-practice.csv) | [CSV](code-consumers/events-campaign/cultural-practice.csv) |
| 전투·건강·세계 | `DefenseBurnEffectSO` | 2 | [열기](types/combat-health-world/defense-burn-effect/README.md) | [CSV](fields/combat-health-world/defense-burn-effect.csv) | [CSV](relations/combat-health-world/defense-burn-effect.csv) | [CSV](incoming/combat-health-world/defense-burn-effect.csv) | [CSV](code-consumers/combat-health-world/defense-burn-effect.csv) |
| 전투·건강·세계 | `DefenseChargeEffectSO` | 3 | [열기](types/combat-health-world/defense-charge-effect/README.md) | [CSV](fields/combat-health-world/defense-charge-effect.csv) | [CSV](relations/combat-health-world/defense-charge-effect.csv) | [CSV](incoming/combat-health-world/defense-charge-effect.csv) | [CSV](code-consumers/combat-health-world/defense-charge-effect.csv) |
| 전투·건강·세계 | `DefenseCorrosionEffectSO` | 3 | [열기](types/combat-health-world/defense-corrosion-effect/README.md) | [CSV](fields/combat-health-world/defense-corrosion-effect.csv) | [CSV](relations/combat-health-world/defense-corrosion-effect.csv) | [CSV](incoming/combat-health-world/defense-corrosion-effect.csv) | [CSV](code-consumers/combat-health-world/defense-corrosion-effect.csv) |
| 전투·건강·세계 | `DefenseDamageEffectSO` | 11 | [열기](types/combat-health-world/defense-damage-effect/README.md) | [CSV](fields/combat-health-world/defense-damage-effect.csv) | [CSV](relations/combat-health-world/defense-damage-effect.csv) | [CSV](incoming/combat-health-world/defense-damage-effect.csv) | [CSV](code-consumers/combat-health-world/defense-damage-effect.csv) |
| 전투·건강·세계 | `DefenseGuardAttackEffectSO` | 5 | [열기](types/combat-health-world/defense-guard-attack-effect/README.md) | [CSV](fields/combat-health-world/defense-guard-attack-effect.csv) | [CSV](relations/combat-health-world/defense-guard-attack-effect.csv) | [CSV](incoming/combat-health-world/defense-guard-attack-effect.csv) | [CSV](code-consumers/combat-health-world/defense-guard-attack-effect.csv) |
| 전투·건강·세계 | `DefenseSlowEffectSO` | 3 | [열기](types/combat-health-world/defense-slow-effect/README.md) | [CSV](fields/combat-health-world/defense-slow-effect.csv) | [CSV](relations/combat-health-world/defense-slow-effect.csv) | [CSV](incoming/combat-health-world/defense-slow-effect.csv) | [CSV](code-consumers/combat-health-world/defense-slow-effect.csv) |
| 전투·건강·세계 | `DiseaseDefinitionSO` | 16 | [열기](types/combat-health-world/disease/README.md) | [CSV](fields/combat-health-world/disease.csv) | [CSV](relations/combat-health-world/disease.csv) | [CSV](incoming/combat-health-world/disease.csv) | [CSV](code-consumers/combat-health-world/disease.csv) |
| 사건·캠페인 | `DungeonFactionDefinitionSO` | 12 | [열기](types/events-campaign/dungeon-faction/README.md) | [CSV](fields/events-campaign/dungeon-faction.csv) | [CSV](relations/events-campaign/dungeon-faction.csv) | [CSV](incoming/events-campaign/dungeon-faction.csv) | [CSV](code-consumers/events-campaign/dungeon-faction.csv) |
| 사건·캠페인 | `EndingDefinitionSO` | 9 | [열기](types/events-campaign/ending/README.md) | [CSV](fields/events-campaign/ending.csv) | [CSV](relations/events-campaign/ending.csv) | [CSV](incoming/events-campaign/ending.csv) | [CSV](code-consumers/events-campaign/ending.csv) |
| 전투·건강·세계 | `EnemyAbilityDefinitionSO` | 18 | [열기](types/combat-health-world/enemy-ability/README.md) | [CSV](fields/combat-health-world/enemy-ability.csv) | [CSV](relations/combat-health-world/enemy-ability.csv) | [CSV](incoming/combat-health-world/enemy-ability.csv) | [CSV](code-consumers/combat-health-world/enemy-ability.csv) |
| 전투·건강·세계 | `EnemyArchetypeDefinitionSO` | 36 | [열기](types/combat-health-world/enemy-archetype/README.md) | [CSV](fields/combat-health-world/enemy-archetype.csv) | [CSV](relations/combat-health-world/enemy-archetype.csv) | [CSV](incoming/combat-health-world/enemy-archetype.csv) | [CSV](code-consumers/combat-health-world/enemy-archetype.csv) |
| 생산·시설 | `EnvironmentalWorkwearSO` | 4 | [열기](types/production-facilities/environmental-workwear/README.md) | [CSV](fields/production-facilities/environmental-workwear.csv) | [CSV](relations/production-facilities/environmental-workwear.csv) | [CSV](incoming/production-facilities/environmental-workwear.csv) | [CSV](code-consumers/production-facilities/environmental-workwear.csv) |
| 생산·시설 | `EquipmentModuleDefinitionSO` | 20 | [열기](types/production-facilities/equipment-module/README.md) | [CSV](fields/production-facilities/equipment-module.csv) | [CSV](relations/production-facilities/equipment-module.csv) | [CSV](incoming/production-facilities/equipment-module.csv) | [CSV](code-consumers/production-facilities/equipment-module.csv) |
| 생산·시설 | `FacilityBlueprintSO` | 7 | [열기](types/production-facilities/facility-blueprint/README.md) | [CSV](fields/production-facilities/facility-blueprint.csv) | [CSV](relations/production-facilities/facility-blueprint.csv) | [CSV](incoming/production-facilities/facility-blueprint.csv) | [CSV](code-consumers/production-facilities/facility-blueprint.csv) |
| 생산·시설 | `FacilityEvolutionRecipeSO` | 6 | [열기](types/production-facilities/facility-evolution-recipe/README.md) | [CSV](fields/production-facilities/facility-evolution-recipe.csv) | [CSV](relations/production-facilities/facility-evolution-recipe.csv) | [CSV](incoming/production-facilities/facility-evolution-recipe.csv) | [CSV](code-consumers/production-facilities/facility-evolution-recipe.csv) |
| 생산·시설 | `FacilityEvolutionRecordTokenDefinitionSO` | 9 | [열기](types/production-facilities/facility-evolution-record-token/README.md) | [CSV](fields/production-facilities/facility-evolution-record-token.csv) | [CSV](relations/production-facilities/facility-evolution-record-token.csv) | [CSV](incoming/production-facilities/facility-evolution-record-token.csv) | [CSV](code-consumers/production-facilities/facility-evolution-record-token.csv) |
| 생산·시설 | `FacilitySynthesisRecipeSO` | 9 | [열기](types/production-facilities/facility-synthesis-recipe/README.md) | [CSV](fields/production-facilities/facility-synthesis-recipe.csv) | [CSV](relations/production-facilities/facility-synthesis-recipe.csv) | [CSV](incoming/production-facilities/facility-synthesis-recipe.csv) | [CSV](code-consumers/production-facilities/facility-synthesis-recipe.csv) |
| 사건·캠페인 | `FactionArcDefinitionSO` | 6 | [열기](types/events-campaign/faction-arc/README.md) | [CSV](fields/events-campaign/faction-arc.csv) | [CSV](relations/events-campaign/faction-arc.csv) | [CSV](incoming/events-campaign/faction-arc.csv) | [CSV](code-consumers/events-campaign/faction-arc.csv) |
| 사건·캠페인 | `FactionChapterDefinitionSO` | 36 | [열기](types/events-campaign/faction-chapter/README.md) | [CSV](fields/events-campaign/faction-chapter.csv) | [CSV](relations/events-campaign/faction-chapter.csv) | [CSV](incoming/events-campaign/faction-chapter.csv) | [CSV](code-consumers/events-campaign/faction-chapter.csv) |
| 사건·캠페인 | `FactionContractDefinitionSO` | 18 | [열기](types/events-campaign/faction-contract/README.md) | [CSV](fields/events-campaign/faction-contract.csv) | [CSV](relations/events-campaign/faction-contract.csv) | [CSV](incoming/events-campaign/faction-contract.csv) | [CSV](code-consumers/events-campaign/faction-contract.csv) |
| 인물·특성 | `FestivalDefinitionSO` | 20 | [열기](types/characters-traits/festival/README.md) | [CSV](fields/characters-traits/festival.csv) | [CSV](relations/characters-traits/festival.csv) | [CSV](incoming/characters-traits/festival.csv) | [CSV](code-consumers/characters-traits/festival.csv) |
| 인물·특성 | `FuneralCultureSO` | 10 | [열기](types/characters-traits/funeral-culture/README.md) | [CSV](fields/characters-traits/funeral-culture.csv) | [CSV](relations/characters-traits/funeral-culture.csv) | [CSV](incoming/characters-traits/funeral-culture.csv) | [CSV](code-consumers/characters-traits/funeral-culture.csv) |
| 연구·효과 | `GameplayEffectConditionDefinitionSO` | 45 | [열기](types/research-effects/gameplay-effect-condition/README.md) | [CSV](fields/research-effects/gameplay-effect-condition.csv) | [CSV](relations/research-effects/gameplay-effect-condition.csv) | [CSV](incoming/research-effects/gameplay-effect-condition.csv) | [CSV](code-consumers/research-effects/gameplay-effect-condition.csv) |
| 연구·효과 | `GameplayEffectDefinitionSO` | 63 | [열기](types/research-effects/gameplay-effect/README.md) | [CSV](fields/research-effects/gameplay-effect.csv) | [CSV](relations/research-effects/gameplay-effect.csv) | [CSV](incoming/research-effects/gameplay-effect.csv) | [CSV](code-consumers/research-effects/gameplay-effect.csv) |
| 아이템 | `GenericItemDefinitionSO` | 710 | [열기](types/items/generic-item/README.md) | [CSV](fields/items/generic-item.csv) | [CSV](relations/items/generic-item.csv) | [CSV](incoming/items/generic-item.csv) | [CSV](code-consumers/items/generic-item.csv) |
| 사건·캠페인 | `GuestRequestDefinitionSO` | 14 | [열기](types/events-campaign/guest-request/README.md) | [CSV](fields/events-campaign/guest-request.csv) | [CSV](relations/events-campaign/guest-request.csv) | [CSV](incoming/events-campaign/guest-request.csv) | [CSV](code-consumers/events-campaign/guest-request.csv) |
| 인물·특성 | `HeritableTraitDefinitionSO` | 24 | [열기](types/characters-traits/heritable-trait/README.md) | [CSV](fields/characters-traits/heritable-trait.csv) | [CSV](relations/characters-traits/heritable-trait.csv) | [CSV](incoming/characters-traits/heritable-trait.csv) | [CSV](code-consumers/characters-traits/heritable-trait.csv) |
| 사건·캠페인 | `LifeEventDefinitionSO` | 32 | [열기](types/events-campaign/life-event/README.md) | [CSV](fields/events-campaign/life-event.csv) | [CSV](relations/events-campaign/life-event.csv) | [CSV](incoming/events-campaign/life-event.csv) | [CSV](code-consumers/events-campaign/life-event.csv) |
| 전투·건강·세계 | `OffenseDecisionCardSO` | 49 | [열기](types/combat-health-world/offense-decision-card/README.md) | [CSV](fields/combat-health-world/offense-decision-card.csv) | [CSV](relations/combat-health-world/offense-decision-card.csv) | [CSV](incoming/combat-health-world/offense-decision-card.csv) | [CSV](code-consumers/combat-health-world/offense-decision-card.csv) |
| 전투·건강·세계 | `OffenseEncounterSO` | 36 | [열기](types/combat-health-world/offense-encounter/README.md) | [CSV](fields/combat-health-world/offense-encounter.csv) | [CSV](relations/combat-health-world/offense-encounter.csv) | [CSV](incoming/combat-health-world/offense-encounter.csv) | [CSV](code-consumers/combat-health-world/offense-encounter.csv) |
| 전투·건강·세계 | `OffenseSiteArchetypeSO` | 12 | [열기](types/combat-health-world/offense-site-archetype/README.md) | [CSV](fields/combat-health-world/offense-site-archetype.csv) | [CSV](relations/combat-health-world/offense-site-archetype.csv) | [CSV](incoming/combat-health-world/offense-site-archetype.csv) | [CSV](code-consumers/combat-health-world/offense-site-archetype.csv) |
| 전투·건강·세계 | `OffenseUrgentSiteDefinitionSO` | 6 | [열기](types/combat-health-world/offense-urgent-site/README.md) | [CSV](fields/combat-health-world/offense-urgent-site.csv) | [CSV](relations/combat-health-world/offense-urgent-site.csv) | [CSV](incoming/combat-health-world/offense-urgent-site.csv) | [CSV](code-consumers/combat-health-world/offense-urgent-site.csv) |
| 생산·시설 | `ProductionRecipeSO` | 355 | [열기](types/production-facilities/production-recipe/README.md) | [CSV](fields/production-facilities/production-recipe.csv) | [CSV](relations/production-facilities/production-recipe.csv) | [CSV](incoming/production-facilities/production-recipe.csv) | [CSV](code-consumers/production-facilities/production-recipe.csv) |
| 인물·특성 | `ProficiencyDefinitionSO` | 9 | [열기](types/characters-traits/proficiency/README.md) | [CSV](fields/characters-traits/proficiency.csv) | [CSV](relations/characters-traits/proficiency.csv) | [CSV](incoming/characters-traits/proficiency.csv) | [CSV](code-consumers/characters-traits/proficiency.csv) |
| 인물·특성 | `ReproductionProfileSO` | 10 | [열기](types/characters-traits/reproduction-profile/README.md) | [CSV](fields/characters-traits/reproduction-profile.csv) | [CSV](relations/characters-traits/reproduction-profile.csv) | [CSV](incoming/characters-traits/reproduction-profile.csv) | [CSV](code-consumers/characters-traits/reproduction-profile.csv) |
| 연구·효과 | `ResearchProjectSO` | 180 | [열기](types/research-effects/research-project/README.md) | [CSV](fields/research-effects/research-project.csv) | [CSV](relations/research-effects/research-project.csv) | [CSV](incoming/research-effects/research-project.csv) | [CSV](code-consumers/research-effects/research-project.csv) |
| 연구·효과 | `ResearchUnlockBundleDefinitionSO` | 180 | [열기](types/research-effects/research-unlock-bundle/README.md) | [CSV](fields/research-effects/research-unlock-bundle.csv) | [CSV](relations/research-effects/research-unlock-bundle.csv) | [CSV](incoming/research-effects/research-unlock-bundle.csv) | [CSV](code-consumers/research-effects/research-unlock-bundle.csv) |
| 아이템 | `ResourceItemDefinitionSO` | 365 | [열기](types/items/resource-item/README.md) | [CSV](fields/items/resource-item.csv) | [CSV](relations/items/resource-item.csv) | [CSV](incoming/items/resource-item.csv) | [CSV](code-consumers/items/resource-item.csv) |
| 아이템 | `SaleItem` | 4 | [열기](types/items/sale-item/README.md) | [CSV](fields/items/sale-item.csv) | [CSV](relations/items/sale-item.csv) | [CSV](incoming/items/sale-item.csv) | [CSV](code-consumers/items/sale-item.csv) |
| 사건·캠페인 | `SeasonalWorldEventDefinitionSO` | 28 | [열기](types/events-campaign/seasonal-world-event/README.md) | [CSV](fields/events-campaign/seasonal-world-event.csv) | [CSV](relations/events-campaign/seasonal-world-event.csv) | [CSV](incoming/events-campaign/seasonal-world-event.csv) | [CSV](code-consumers/events-campaign/seasonal-world-event.csv) |
| 사건·캠페인 | `ServiceIncidentDefinitionSO` | 8 | [열기](types/events-campaign/service-incident/README.md) | [CSV](fields/events-campaign/service-incident.csv) | [CSV](relations/events-campaign/service-incident.csv) | [CSV](incoming/events-campaign/service-incident.csv) | [CSV](code-consumers/events-campaign/service-incident.csv) |
| 생산·시설 | `ServiceProcessSO` | 5 | [열기](types/production-facilities/service-process/README.md) | [CSV](fields/production-facilities/service-process.csv) | [CSV](relations/production-facilities/service-process.csv) | [CSV](incoming/production-facilities/service-process.csv) | [CSV](code-consumers/production-facilities/service-process.csv) |
| 인물·특성 | `SpeciesCultureDefinitionSO` | 10 | [열기](types/characters-traits/species-culture/README.md) | [CSV](fields/characters-traits/species-culture.csv) | [CSV](relations/characters-traits/species-culture.csv) | [CSV](incoming/characters-traits/species-culture.csv) | [CSV](code-consumers/characters-traits/species-culture.csv) |
| 인물·특성 | `SpeciesLifeHistorySO` | 10 | [열기](types/characters-traits/species-life-history/README.md) | [CSV](fields/characters-traits/species-life-history.csv) | [CSV](relations/characters-traits/species-life-history.csv) | [CSV](incoming/characters-traits/species-life-history.csv) | [CSV](code-consumers/characters-traits/species-life-history.csv) |
| 아이템 | `StockInfo` | 11 | [열기](types/items/stock-info/README.md) | [CSV](fields/items/stock-info.csv) | [CSV](relations/items/stock-info.csv) | [CSV](incoming/items/stock-info.csv) | [CSV](code-consumers/items/stock-info.csv) |
| 전투·건강·세계 | `SurgicalProcedureSO` | 47 | [열기](types/combat-health-world/surgical-procedure/README.md) | [CSV](fields/combat-health-world/surgical-procedure.csv) | [CSV](relations/combat-health-world/surgical-procedure.csv) | [CSV](incoming/combat-health-world/surgical-procedure.csv) | [CSV](code-consumers/combat-health-world/surgical-procedure.csv) |
| 생산·시설 | `TextileMaterialDefinitionSO` | 12 | [열기](types/production-facilities/textile-material/README.md) | [CSV](fields/production-facilities/textile-material.csv) | [CSV](relations/production-facilities/textile-material.csv) | [CSV](incoming/production-facilities/textile-material.csv) | [CSV](code-consumers/production-facilities/textile-material.csv) |
| 사건·캠페인 | `WeatherFrontDefinitionSO` | 6 | [열기](types/events-campaign/weather-front/README.md) | [CSV](fields/events-campaign/weather-front.csv) | [CSV](relations/events-campaign/weather-front.csv) | [CSV](incoming/events-campaign/weather-front.csv) | [CSV](code-consumers/events-campaign/weather-front.csv) |
| 전투·건강·세계 | `WildlifeSpeciesSO` | 18 | [열기](types/combat-health-world/wildlife-species/README.md) | [CSV](fields/combat-health-world/wildlife-species.csv) | [CSV](relations/combat-health-world/wildlife-species.csv) | [CSV](incoming/combat-health-world/wildlife-species.csv) | [CSV](code-consumers/combat-health-world/wildlife-species.csv) |

## 존재 이유 판정

존재 이유는 작성 description만 반복하지 않는다. 생산 입출력, 아이템 feature, 연구 선행·해금, 시설 ability, 사건 requirement·choice·effect, 다른 콘텐츠의 참조를 함께 사용한다. 근거가 부족하거나 안정 ID가 합성된 행은 `수동 검토 필요`로 분리한다.

같은 유형과 안정 ID의 자산이 여러 경로에 공존하는 경우에도 어느 한쪽을 임의로 제거하지 않는다. 각 자산은 `record_key`로 분리하고 `manual-review.csv`에서 이관·호환 여부를 판단한다.

## 재생성

```powershell
python -X utf8 Tools/Documentation/generate_content_database.py --output-root docs_final/content-db
& Tools/Documentation/validate_content_database.ps1 -DatabaseRoot docs_final/content-db
python -X utf8 Tools/Documentation/verify_knowledge_base.py docs_final/content-db
```

세 명령은 Unity를 실행하지 않는다. 생성기는 Unity 작성 자산과 C# 직렬화 권위를 읽어 `docs_final/content-db/`를 갱신한다. 검증기는 스키마·관계·역참조·enum·링크를 검사하고, 마지막 명령은 원본 변경과 생성물 변조를 검출한다.
