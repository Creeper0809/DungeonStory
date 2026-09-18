# 콘텐츠 참조 결함 후보

현재 작성 자산의 ID를 콘텐츠 DB에서 해소하지 못한 참조다. 계획된 선행 콘텐츠, 구형 ID, 오기, effect kind/target 불일치가 섞일 수 있으므로 자동 삭제하거나 임의 치환하지 않는다.

| 출발 유형 | 필드 | 건수 | 대상 예시 | 판정 |
|---|---|---:|---|---|
| `SpeciesCultureDefinitionSO` | `forbiddenItemIds` | 9 | food:garlic-tonic, food:heavy-oil-stew, food:raw-monster-meat, food:tiny-portion, item:broken-tool, item:unsealed-oath | 문화·사건 요구 아이템이 현행 아이템 카탈로그에 없음 |
| `CharacterAcquiredTraitModuleSO` | `formula.appliedParameterIds` | 8 | magnitude | 대상 콘텐츠 미해소 |
| `CulturalPracticeDefinitionSO` | `requirements.items.0.itemDefinitionId` | 7 | food:blood-reserve, food:dried-fruit, food:mushroom-stew, item:bell, item:candle, resource:compost | 문화·사건 요구 아이템이 현행 아이템 카탈로그에 없음 |
| `SpeciesCultureDefinitionSO` | `preferredItemIds` | 4 | food:blood-reserve, food:dried-fruit, food:mushroom-stew, food:spiced-stew | 문화·사건 요구 아이템이 현행 아이템 카탈로그에 없음 |
| `LifeEventDefinitionSO` | `choices.0.effects.0.targetId` | 3 | life-event:captains-test, life-event:killer-sighted, life-event:masterpiece-commission | 효과 종류가 요구하는 대상 유형과 targetId가 일치하지 않음 |
| `CharacterAcquiredTraitModuleSO` | `drawbackBindingIds` | 1 | acquired-trait:module:night-vigil:effect:1 | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.0.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.1.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.10.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.11.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.12.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.13.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.14.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.15.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.16.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.17.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.18.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.19.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.2.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.20.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.21.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.22.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.3.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.4.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.5.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.6.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.7.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.8.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `CharacterAcquiredTraitSettingsSO` | `drawbackCapabilities.9.formula.appliedParameterIds` | 1 | magnitude | 대상 콘텐츠 미해소 |
| `GuestRequestDefinitionSO` | `serviceRequirements.facilities.0.buildingDefinitionId` | 1 | building:circus-arena | 요구 시설 ID가 현행 BuildingSO 안정 ID와 일치하지 않음 |
| `LifeEventDefinitionSO` | `automaticEffects.0.targetId` | 1 | life-event:tool-inheritance | 효과 종류가 요구하는 대상 유형과 targetId가 일치하지 않음 |

전체 행과 출발 자산 경로는 [unresolved-references.csv](unresolved-references.csv)에 보존한다.
