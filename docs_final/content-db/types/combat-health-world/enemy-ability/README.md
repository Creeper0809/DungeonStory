# EnemyAbilityDefinitionSO

전투·건강·세계 영역의 작성 콘텐츠 유형이다.

총 18개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/combat-health-world/enemy-ability.csv)
- [중첩 작성 필드 CSV](../../../fields/combat-health-world/enemy-ability.csv)
- [정방향 관계 CSV](../../../relations/combat-health-world/enemy-ability.csv)
- [역방향 관계 CSV](../../../incoming/combat-health-world/enemy-ability.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `enemy-ability:arcane-null` | 마법 차단 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=4 | catalog-registered-static-consumer | active-authored | 4 | [enemy-ability_arcane-null.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_arcane-null.asset) |
| `enemy-ability:armor-break` | 갑주 파쇄 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=3 | catalog-registered-static-consumer | active-authored | 4 | [enemy-ability_armor-break.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_armor-break.asset) |
| `enemy-ability:blood-drain` | 혈액 흡수 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=3 | catalog-registered-static-consumer | active-authored | 0 | [enemy-ability_blood-drain.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_blood-drain.asset) |
| `enemy-ability:charge` | 돌진 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=2 | catalog-registered-static-consumer | active-authored | 4 | [enemy-ability_charge.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_charge.asset) |
| `enemy-ability:core-repair` | 핵 긴급수리 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=4 | catalog-registered-static-consumer | active-authored | 3 | [enemy-ability_core-repair.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_core-repair.asset) |
| `enemy-ability:field-dressing` | 야전 처치 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=3 | catalog-registered-static-consumer | active-authored | 3 | [enemy-ability_field-dressing.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_field-dressing.asset) |
| `enemy-ability:frost-bind` | 서리 구속 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=3 | catalog-registered-static-consumer | active-authored | 2 | [enemy-ability_frost-bind.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_frost-bind.asset) |
| `enemy-ability:hook-pull` | 갈고리 끌기 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=3 | catalog-registered-static-consumer | active-authored | 2 | [enemy-ability_hook-pull.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_hook-pull.asset) |
| `enemy-ability:poison-cloud` | 독성 구름 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=4 | catalog-registered-static-consumer | active-authored | 3 | [enemy-ability_poison-cloud.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_poison-cloud.asset) |
| `enemy-ability:powder-shot` | 화약 일제 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=3 | catalog-registered-static-consumer | active-authored | 2 | [enemy-ability_powder-shot.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_powder-shot.asset) |
| `enemy-ability:rally` | 전열 재정비 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=4 | catalog-registered-static-consumer | active-authored | 9 | [enemy-ability_rally.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_rally.asset) |
| `enemy-ability:retreat-cover` | 퇴각 엄호 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=3 | catalog-registered-static-consumer | active-authored | 6 | [enemy-ability_retreat-cover.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_retreat-cover.asset) |
| `enemy-ability:rune-ward` | 룬 보호막 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=4 | catalog-registered-static-consumer | active-authored | 4 | [enemy-ability_rune-ward.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_rune-ward.asset) |
| `enemy-ability:shield-wall` | 방패벽 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=3 | catalog-registered-static-consumer | active-authored | 6 | [enemy-ability_shield-wall.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_shield-wall.asset) |
| `enemy-ability:smoke-screen` | 연막 전개 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=4 | catalog-registered-static-consumer | active-authored | 4 | [enemy-ability_smoke-screen.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_smoke-screen.asset) |
| `enemy-ability:summon-minion` | 지원체 소환 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=5 | catalog-registered-static-consumer | active-authored | 5 | [enemy-ability_summon-minion.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_summon-minion.asset) |
| `enemy-ability:suppressive-volley` | 제압 사격 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=3 | catalog-registered-static-consumer | active-authored | 7 | [enemy-ability_suppressive-volley.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_suppressive-volley.asset) |
| `enemy-ability:truth-seal` | 진실 봉인 | 전투·건강·세계 영역의 EnemyAbilityDefinitionSO 규칙을 분리해 재사용한다. | cooldownRounds=4 | catalog-registered-static-consumer | active-authored | 5 | [enemy-ability_truth-seal.asset](../../../../../Assets/Resources/SO/V20/Combat/Abilities/enemy-ability_truth-seal.asset) |
