# TextileMaterialDefinitionSO

생산·시설 영역의 작성 콘텐츠 유형이다.

총 12개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/production-facilities/textile-material.csv)
- [중첩 작성 필드 CSV](../../../fields/production-facilities/textile-material.csv)
- [정방향 관계 CSV](../../../relations/production-facilities/textile-material.csv)
- [역방향 관계 CSV](../../../incoming/production-facilities/textile-material.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `textilematerial:23101` | 그늘천 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.25; requiredResearchId=research:textile:fiber | catalog-registered-static-consumer | active-authored | 0 | [textile_shade_cloth.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_shade_cloth.asset) |
| `textilematerial:23102` | 서리 린넨 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.3; requiredResearchId=research:textile:fiber | catalog-registered-static-consumer | active-authored | 0 | [textile_frost_linen.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_frost_linen.asset) |
| `textilematerial:23103` | 잿불 면직물 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.22; requiredResearchId=research:textile:fiber | catalog-registered-static-consumer | active-authored | 0 | [textile_ember_cotton.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_ember_cotton.asset) |
| `textilematerial:23104` | 습지 캔버스 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.72; requiredResearchId=research:textile:layered | catalog-registered-static-consumer | active-authored | 0 | [textile_mire_canvas.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_mire_canvas.asset) |
| `textilematerial:23105` | 포자 삼베 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.35; requiredResearchId=research:textile:layered | catalog-registered-static-consumer | active-authored | 0 | [textile_spore_hemp.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_spore_hemp.asset) |
| `textilematerial:23106` | 일반 모직 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.28; requiredResearchId=research:textile:fiber | catalog-registered-static-consumer | active-authored | 0 | [textile_common_wool.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_common_wool.asset) |
| `textilematerial:23107` | 서리 양모직 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.38; requiredResearchId=research:textile:layered | catalog-registered-static-consumer | active-authored | 0 | [textile_frost_wool.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_frost_wool.asset) |
| `textilematerial:23108` | 심층 염소모직 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.42; requiredResearchId=research:textile:layered | catalog-registered-static-consumer | active-authored | 0 | [textile_deep_goat_wool.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_deep_goat_wool.asset) |
| `textilematerial:23109` | 동굴 비단 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.45; requiredResearchId=research:textile:tailoring | catalog-registered-static-consumer | active-authored | 0 | [textile_cave_silk.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_cave_silk.asset) |
| `textilematerial:23110` | 몽직물 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.5; requiredResearchId=research:textile:tailoring | catalog-registered-static-consumer | active-authored | 0 | [textile_dreamweave.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_dreamweave.asset) |
| `textilematerial:23111` | 가죽 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.62; requiredResearchId=research:textile:tailoring | catalog-registered-static-consumer | active-authored | 0 | [textile_leather.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_leather.asset) |
| `textilematerial:23112` | 룬가죽 | 생산·시설 영역의 TextileMaterialDefinitionSO 규칙을 분리해 재사용한다. | waterResistance=0.74; requiredResearchId=research:textile:tailoring | catalog-registered-static-consumer | active-authored | 0 | [textile_rune_leather.asset](../../../../../Assets/Resources/SO/Apparel/Materials/textile_rune_leather.asset) |
