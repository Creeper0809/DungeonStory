# CraftMaterialDefinitionSO

생산·시설 영역의 작성 콘텐츠 유형이다.

총 12개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/production-facilities/craft-material.csv)
- [중첩 작성 필드 CSV](../../../fields/production-facilities/craft-material.csv)
- [정방향 관계 CSV](../../../relations/production-facilities/craft-material.csv)
- [역방향 관계 CSV](../../../incoming/production-facilities/craft-material.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `material:blacksteel-ingot` | 흑강 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:metallurgy:blacksteel | catalog-registered-static-consumer | active-authored | 8 | [material_blacksteel.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_blacksteel.asset) |
| `material:cloth` | 천 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:textile:fiber | catalog-registered-static-consumer | active-authored | 76 | [material_cloth.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_cloth.asset) |
| `material:dreamweave` | 몽직물 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:textile:dreamweave | catalog-registered-static-consumer | active-authored | 4 | [material_dreamweave.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_dreamweave.asset) |
| `material:gold-ingot` | 금 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:metallurgy:precious | catalog-registered-static-consumer | active-authored | 8 | [material_gold.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_gold.asset) |
| `material:iron-ingot` | 철 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:metallurgy:iron | catalog-registered-static-consumer | active-authored | 15 | [material_iron.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_iron.asset) |
| `material:leather` | 가죽 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:textile:tanning | catalog-registered-static-consumer | active-authored | 12 | [material_leather.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_leather.asset) |
| `material:lumber` | 목재 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:forestry:sawmill | catalog-registered-static-consumer | active-authored | 29 | [material_wood.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_wood.asset) |
| `material:rune-leather` | 룬가죽 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:textile:rune-leather | catalog-registered-static-consumer | active-authored | 4 | [material_rune_leather.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_rune_leather.asset) |
| `material:steel-ingot` | 강철 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:metallurgy:steel | catalog-registered-static-consumer | active-authored | 18 | [material_steel.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_steel.asset) |
| `material:stone-block` | 석재 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:mining:stonecutting | catalog-registered-static-consumer | active-authored | 5 | [material_stone.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_stone.asset) |
| `resource:bone` | 뼈·뿔 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:metallurgy:primitive | catalog-registered-static-consumer | active-authored | 5 | [material_bone.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_bone.asset) |
| `resource:wool` | 털 | 생산·시설 영역의 CraftMaterialDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:husbandry:selective | catalog-registered-static-consumer | active-authored | 6 | [material_wool.asset](../../../../../Assets/Resources/SO/Economy/Materials/material_wool.asset) |
