# DungeonFactionDefinitionSO

세력의 종족 정체성, 관계·거래 태그와 보급 구성을 외교·계약 시스템에 제공한다

총 12개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/events-campaign/dungeon-faction.csv)
- [중첩 작성 필드 CSV](../../../fields/events-campaign/dungeon-faction.csv)
- [정방향 관계 CSV](../../../relations/events-campaign/dungeon-faction.csv)
- [역방향 관계 CSV](../../../incoming/events-campaign/dungeon-faction.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `faction:dungeon:beastkin` | 붉은발 역참 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | tradeCooldownDays=7; supplyCooldownDays=20; reinforcementCooldownDays=10 | catalog-registered-static-consumer | duplicate-authority-review | 53 | [Faction_Beastkin_RedPawPost.asset](../../../../../Assets/Resources/SO/Factions/Dungeons/Faction_Beastkin_RedPawPost.asset) |
| `faction:dungeon:beastkin` | 붉은발 역참 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | stable-id-literal-consumer | duplicate-authority-review | 53 | [faction_dungeon_beastkin.asset](../../../../../Assets/Resources/SO/Factions/faction_dungeon_beastkin.asset) |
| `faction:dungeon:demon` | 잿불 계약정 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | tradeCooldownDays=23; supplyCooldownDays=84; reinforcementCooldownDays=10 | catalog-registered-static-consumer | duplicate-authority-review | 52 | [Faction_Demon_AshContractCourt.asset](../../../../../Assets/Resources/SO/Factions/Dungeons/Faction_Demon_AshContractCourt.asset) |
| `faction:dungeon:demon` | 잿불 계약정 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | stable-id-literal-consumer | duplicate-authority-review | 52 | [faction_dungeon_demon.asset](../../../../../Assets/Resources/SO/Factions/faction_dungeon_demon.asset) |
| `faction:dungeon:golem` | 석맥 주조소 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | tradeCooldownDays=27; supplyCooldownDays=99; reinforcementCooldownDays=10 | catalog-registered-static-consumer | duplicate-authority-review | 53 | [Faction_Golem_StoneveinFoundry.asset](../../../../../Assets/Resources/SO/Factions/Dungeons/Faction_Golem_StoneveinFoundry.asset) |
| `faction:dungeon:golem` | 석맥 주조소 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | stable-id-literal-consumer | duplicate-authority-review | 53 | [faction_dungeon_golem.asset](../../../../../Assets/Resources/SO/Factions/faction_dungeon_golem.asset) |
| `faction:dungeon:harpy` | 폭풍 둥지 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | tradeCooldownDays=16; supplyCooldownDays=22; reinforcementCooldownDays=10 | catalog-registered-static-consumer | duplicate-authority-review | 52 | [Faction_Harpy_StormNest.asset](../../../../../Assets/Resources/SO/Factions/Dungeons/Faction_Harpy_StormNest.asset) |
| `faction:dungeon:harpy` | 폭풍 둥지 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | stable-id-literal-consumer | duplicate-authority-review | 52 | [faction_dungeon_harpy.asset](../../../../../Assets/Resources/SO/Factions/faction_dungeon_harpy.asset) |
| `faction:dungeon:kobold` | 심층 톱니굴 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | tradeCooldownDays=25; supplyCooldownDays=49; reinforcementCooldownDays=10 | catalog-registered-static-consumer | duplicate-authority-review | 53 | [Faction_Kobold_DeepGearWarren.asset](../../../../../Assets/Resources/SO/Factions/Dungeons/Faction_Kobold_DeepGearWarren.asset) |
| `faction:dungeon:kobold` | 심층 톱니굴 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | stable-id-literal-consumer | duplicate-authority-review | 53 | [faction_dungeon_kobold.asset](../../../../../Assets/Resources/SO/Factions/faction_dungeon_kobold.asset) |
| `faction:dungeon:myconid` | 균사 심림 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | tradeCooldownDays=22; supplyCooldownDays=38; reinforcementCooldownDays=10 | catalog-registered-static-consumer | duplicate-authority-review | 53 | [Faction_Myconid_MycelialGrove.asset](../../../../../Assets/Resources/SO/Factions/Dungeons/Faction_Myconid_MycelialGrove.asset) |
| `faction:dungeon:myconid` | 균사 심림 | DungeonFactionDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | stable-id-literal-consumer | duplicate-authority-review | 53 | [faction_dungeon_myconid.asset](../../../../../Assets/Resources/SO/Factions/faction_dungeon_myconid.asset) |
