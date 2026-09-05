# CropDefinitionSO

생산·시설 영역의 작성 콘텐츠 유형이다.

총 12개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/production-facilities/crop.csv)
- [중첩 작성 필드 CSV](../../../fields/production-facilities/crop.csv)
- [정방향 관계 CSV](../../../relations/production-facilities/crop.csv)
- [역방향 관계 CSV](../../../incoming/production-facilities/crop.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `crop:bloodleaf` | 혈엽 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:pharmacology:herbalism; sowWork=4; harvestWork=7; dailyWater=0.35 | catalog-registered-static-consumer | active-authored | 0 | [crop_bloodleaf.asset](../../../../../Assets/Resources/SO/Economy/Crops/crop_bloodleaf.asset) |
| `crop:cave-mushroom` | 동굴버섯 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:agriculture:gathering; sowWork=3; harvestWork=5; dailyWater=0.2 | catalog-registered-static-consumer | active-authored | 0 | [crop_cave_mushroom.asset](../../../../../Assets/Resources/SO/Economy/Crops/crop_cave_mushroom.asset) |
| `crop:dreamleaf` | 몽엽 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:pharmacology:anesthesia; sowWork=5; harvestWork=8; dailyWater=0.3 | catalog-registered-static-consumer | active-authored | 0 | [crop_dreamleaf.asset](../../../../../Assets/Resources/SO/Economy/Crops/crop_dreamleaf.asset) |
| `crop:ember-cotton` | 잿불 목화 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:textile:fiber; sowWork=4; harvestWork=7; dailyWater=0.4 | catalog-registered-static-consumer | active-authored | 0 | [crop_ember-cotton.asset](../../../../../Assets/Resources/SO/Economy/Crops/V22Textiles/crop_ember-cotton.asset) |
| `crop:ember-root` | 잿불뿌리 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:agriculture:field; sowWork=4; harvestWork=7; dailyWater=0.25 | catalog-registered-static-consumer | active-authored | 0 | [crop_ember_root.asset](../../../../../Assets/Resources/SO/Economy/Crops/crop_ember_root.asset) |
| `crop:frost-flax` | 서리 아마 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:textile:fiber; sowWork=4; harvestWork=7; dailyWater=0.35 | catalog-registered-static-consumer | active-authored | 0 | [crop_frost-flax.asset](../../../../../Assets/Resources/SO/Economy/Crops/V22Textiles/crop_frost-flax.asset) |
| `crop:mire-reed` | 습지 갈대 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:textile:fiber; sowWork=4; harvestWork=7; dailyWater=0.45 | catalog-registered-static-consumer | active-authored | 0 | [crop_mire-reed.asset](../../../../../Assets/Resources/SO/Economy/Crops/V22Textiles/crop_mire-reed.asset) |
| `crop:moonflower` | 월화 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:pharmacology:herbalism; sowWork=5; harvestWork=9; dailyWater=0.4 | catalog-registered-static-consumer | active-authored | 0 | [crop_moonflower.asset](../../../../../Assets/Resources/SO/Economy/Crops/crop_moonflower.asset) |
| `crop:night-grape` | 밤포도 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:agriculture:irrigation; sowWork=5; harvestWork=8; dailyWater=0.5 | catalog-registered-static-consumer | active-authored | 0 | [crop_night_grape.asset](../../../../../Assets/Resources/SO/Economy/Crops/crop_night_grape.asset) |
| `crop:shade-fiber` | 그늘섬유 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:textile:fiber; sowWork=4; harvestWork=7; dailyWater=0.3 | catalog-registered-static-consumer | active-authored | 0 | [crop_shade_fiber.asset](../../../../../Assets/Resources/SO/Economy/Crops/crop_shade_fiber.asset) |
| `crop:spore-hemp` | 포자 삼 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:textile:fiber; sowWork=4; harvestWork=7; dailyWater=0.5 | catalog-registered-static-consumer | active-authored | 0 | [crop_spore-hemp.asset](../../../../../Assets/Resources/SO/Economy/Crops/V22Textiles/crop_spore-hemp.asset) |
| `crop:twilight-grain` | 황혼곡 | 생산·시설 영역의 CropDefinitionSO 규칙을 분리해 재사용한다. | requiredResearchId=research:agriculture:field; sowWork=3; harvestWork=6; dailyWater=0.35 | catalog-registered-static-consumer | active-authored | 0 | [crop_twilight_grain.asset](../../../../../Assets/Resources/SO/Economy/Crops/crop_twilight_grain.asset) |
