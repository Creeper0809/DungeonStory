# CombatShieldSO

방패의 방어 범위와 운용 부담을 전투 선택에 연결한다

총 9개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/combat-health-world/combat-shield.csv)
- [중첩 작성 필드 CSV](../../../fields/combat-health-world/combat-shield.csv)
- [정방향 관계 CSV](../../../relations/combat-health-world/combat-shield.csv)
- [역방향 관계 CSV](../../../incoming/combat-health-world/combat-shield.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `equipment-item:shield:blacksteel` | 흑강 방패 | 방패의 방어 범위와 운용 부담을 전투 선택에 연결한다 | requiredCraftWork=312; requiredResearchId=research:industry:dark-foundry | catalog-registered-static-consumer | active-authored | 0 | [S07_BlacksteelShield.asset](../../../../../Assets/Resources/SO/Combat/Equipment/S07_BlacksteelShield.asset) |
| `equipment-item:shield:buckler` | 버클러 | 방패의 방어 범위와 운용 부담을 전투 선택에 연결한다 | requiredCraftWork=100; requiredResearchId=research:equipment:weapon-patterns | catalog-registered-static-consumer | active-authored | 0 | [S03_Buckler.asset](../../../../../Assets/Resources/SO/Combat/Equipment/S03_Buckler.asset) |
| `equipment-item:shield:iron` | 철 방패 | 방패의 방어 범위와 운용 부담을 전투 선택에 연결한다 | requiredCraftWork=176; requiredResearchId=research:metallurgy:iron | catalog-registered-static-consumer | active-authored | 0 | [S02_IronShield.asset](../../../../../Assets/Resources/SO/Combat/Equipment/S02_IronShield.asset) |
| `equipment-item:shield:mana-buckler` | 마나 버클러 | 방패의 방어 범위와 운용 부담을 전투 선택에 연결한다 | requiredCraftWork=256; requiredResearchId=research:equipment:rune-module-tuning | catalog-registered-static-consumer | active-authored | 0 | [S08_ManaBuckler.asset](../../../../../Assets/Resources/SO/Combat/Equipment/S08_ManaBuckler.asset) |
| `equipment-item:shield:pavise` | 파비스 | 방패의 방어 범위와 운용 부담을 전투 선택에 연결한다 | requiredCraftWork=232; requiredResearchId=research:defense:siege-fortification | catalog-registered-static-consumer | active-authored | 0 | [S09_Pavise.asset](../../../../../Assets/Resources/SO/Combat/Equipment/S09_Pavise.asset) |
| `equipment-item:shield:powered` | 동력 방패 | 방패의 방어 범위와 운용 부담을 전투 선택에 연결한다 | requiredCraftWork=352; requiredResearchId=research:equipment:powered-armor | catalog-registered-static-consumer | active-authored | 0 | [S06_PoweredShield.asset](../../../../../Assets/Resources/SO/Combat/Equipment/S06_PoweredShield.asset) |
| `equipment-item:shield:rune` | 룬 방패 | 방패의 방어 범위와 운용 부담을 전투 선택에 연결한다 | requiredCraftWork=312; requiredResearchId=research:equipment:rune-module-tuning | catalog-registered-static-consumer | active-authored | 0 | [S05_RuneShield.asset](../../../../../Assets/Resources/SO/Combat/Equipment/S05_RuneShield.asset) |
| `equipment-item:shield:tower` | 대형 방패 | 방패의 방어 범위와 운용 부담을 전투 선택에 연결한다 | requiredCraftWork=192; requiredResearchId=research:defense:fortification | catalog-registered-static-consumer | active-authored | 5 | [S04_TowerShield.asset](../../../../../Assets/Resources/SO/Combat/Equipment/S04_TowerShield.asset) |
| `equipment-item:shield:wood` | 나무 방패 | 방패의 방어 범위와 운용 부담을 전투 선택에 연결한다 | requiredCraftWork=80 | catalog-registered-static-consumer | active-authored | 1 | [S01_WoodShield.asset](../../../../../Assets/Resources/SO/Combat/Equipment/S01_WoodShield.asset) |
