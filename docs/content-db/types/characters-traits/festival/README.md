# FestivalDefinitionSO

인물·특성 영역의 작성 콘텐츠 유형이다.

총 20개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/characters-traits/festival.csv)
- [중첩 작성 필드 CSV](../../../fields/characters-traits/festival.csv)
- [정방향 관계 CSV](../../../relations/characters-traits/festival.csv)
- [역방향 관계 CSV](../../../incoming/characters-traits/festival.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `festival:ash-oath` | 재의 맹세일 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:faction-audience-hall | catalog-registered-static-consumer | active-authored | 0 | [festival_ash-oath.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_ash-oath.asset) |
| `festival:blood-lantern` | 혈향 등불제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=workstation:v19:memorial-room | catalog-registered-static-consumer | active-authored | 0 | [festival_blood-lantern.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_blood-lantern.asset) |
| `festival:clear-confluence` | 맑은 합류제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:clean-water-reservoir | catalog-registered-static-consumer | active-authored | 0 | [festival_clear-confluence.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_clear-confluence.asset) |
| `festival:core-resonance` | 핵 공명일 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:rune-tuning-room | catalog-registered-static-consumer | active-authored | 0 | [festival_core-resonance.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_core-resonance.asset) |
| `festival:dungeon-accord-day` | 던전 협약 기념일 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:faction-audience-hall | catalog-registered-static-consumer | active-authored | 0 | [festival_dungeon-accord-day.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_dungeon-accord-day.asset) |
| `festival:frontier-map-night` | 지도에 불을 밝히는 밤 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:expedition-map-room | catalog-registered-static-consumer | active-authored | 0 | [festival_frontier-map-night.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_frontier-map-night.asset) |
| `festival:high-sun` | 고일제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | type-consumer-registration-unverified | duplicate-authority-review | 0 | [festival_high-sun.asset](../../../../../Assets/Resources/SO/Population/Festivals/festival_high-sun.asset) |
| `festival:high-sun` | 고일제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:festival-common-hall | catalog-registered-static-consumer | duplicate-authority-review | 0 | [festival_high-sun.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_high-sun.asset) |
| `festival:long-night-memorial` | 긴밤 추모제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | type-consumer-registration-unverified | duplicate-authority-review | 0 | [festival_long-night-memorial.asset](../../../../../Assets/Resources/SO/Population/Festivals/festival_long-night-memorial.asset) |
| `festival:long-night-memorial` | 긴밤 추모제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=workstation:v19:memorial-room | catalog-registered-static-consumer | duplicate-authority-review | 0 | [festival_long-night-memorial.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_long-night-memorial.asset) |
| `festival:many-tables` | 열 문화의 식탁 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:festival-common-hall | catalog-registered-static-consumer | active-authored | 0 | [festival_many-tables.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_many-tables.asset) |
| `festival:open-sky-chorus` | 열린 하늘 합창 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:weather-observation-tower | catalog-registered-static-consumer | active-authored | 0 | [festival_open-sky-chorus.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_open-sky-chorus.asset) |
| `festival:pack-first-hunt` | 무리의 첫사냥 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:festival-common-hall | catalog-registered-static-consumer | active-authored | 0 | [festival_pack-first-hunt.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_pack-first-hunt.asset) |
| `festival:spore-bloom` | 포자 개화제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:cave-growing-rack | catalog-registered-static-consumer | active-authored | 0 | [festival_spore-bloom.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_spore-bloom.asset) |
| `festival:sprout` | 새싹제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | type-consumer-registration-unverified | duplicate-authority-review | 0 | [festival_sprout.asset](../../../../../Assets/Resources/SO/Population/Festivals/festival_sprout.asset) |
| `festival:sprout` | 새싹제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:festival-common-hall | catalog-registered-static-consumer | duplicate-authority-review | 0 | [festival_sprout.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_sprout.asset) |
| `festival:storage` | 저장제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | type-consumer-registration-unverified | duplicate-authority-review | 0 | [festival_storage.asset](../../../../../Assets/Resources/SO/Population/Festivals/festival_storage.asset) |
| `festival:storage` | 저장제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:festival-common-hall | catalog-registered-static-consumer | duplicate-authority-review | 0 | [festival_storage.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_storage.asset) |
| `festival:tool-clan-fair` | 도구씨족 품평회 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:apprentice-workbench | catalog-registered-static-consumer | active-authored | 0 | [festival_tool-clan-fair.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_tool-clan-fair.asset) |
| `festival:weapon-vigil` | 무기 철야제 | 인물·특성 영역의 FestivalDefinitionSO 규칙을 분리해 재사용한다. | requiredBuildingDefinitionId=building:armory | catalog-registered-static-consumer | active-authored | 0 | [festival_weapon-vigil.asset](../../../../../Assets/Resources/SO/V20/Society/Festivals/festival_weapon-vigil.asset) |
