# BattlefieldModifierDefinitionSO

전장의 지형·환경 조건을 전투 규칙 변화로 변환한다

총 12개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/combat-health-world/battlefield-modifier.csv)
- [중첩 작성 필드 CSV](../../../fields/combat-health-world/battlefield-modifier.csv)
- [정방향 관계 CSV](../../../relations/combat-health-world/battlefield-modifier.csv)
- [역방향 관계 CSV](../../../incoming/combat-health-world/battlefield-modifier.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `battlefield:broken-pillars` | 무너진 기둥숲 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:flank | catalog-registered-static-consumer | active-authored | 3 | [battlefield_broken-pillars.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_broken-pillars.asset) |
| `battlefield:civilian-corridor` | 피난 통로 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:precision | catalog-registered-static-consumer | active-authored | 3 | [battlefield_civilian-corridor.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_civilian-corridor.asset) |
| `battlefield:collapsing-ceiling` | 붕괴 천장 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:mobile | catalog-registered-static-consumer | active-authored | 3 | [battlefield_collapsing-ceiling.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_collapsing-ceiling.asset) |
| `battlefield:elevated-gallery` | 고가 회랑 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:anti-air | catalog-registered-static-consumer | active-authored | 3 | [battlefield_elevated-gallery.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_elevated-gallery.asset) |
| `battlefield:flooded-floor` | 침수 바닥 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:insulated | catalog-registered-static-consumer | active-authored | 3 | [battlefield_flooded-floor.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_flooded-floor.asset) |
| `battlefield:hostage-chain` | 인질 구속줄 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:nonlethal | catalog-registered-static-consumer | active-authored | 3 | [battlefield_hostage-chain.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_hostage-chain.asset) |
| `battlefield:mana-storm` | 마나 폭풍 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:mana-grounding | catalog-registered-static-consumer | active-authored | 3 | [battlefield_mana-storm.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_mana-storm.asset) |
| `battlefield:narrow-bridge` | 좁은 교량 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:reach | catalog-registered-static-consumer | active-authored | 3 | [battlefield_narrow-bridge.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_narrow-bridge.asset) |
| `battlefield:powder-smoke` | 화약 연무 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:smoke-hood | catalog-registered-static-consumer | active-authored | 3 | [battlefield_powder-smoke.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_powder-smoke.asset) |
| `battlefield:sealed-exit` | 봉인된 퇴로 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:engineering | catalog-registered-static-consumer | active-authored | 3 | [battlefield_sealed-exit.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_sealed-exit.asset) |
| `battlefield:spore-bloom` | 포자 개화 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:fungicide | catalog-registered-static-consumer | active-authored | 3 | [battlefield_spore-bloom.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_spore-bloom.asset) |
| `battlefield:unstable-device` | 불안정 장치 | 전장의 지형·환경 조건을 전투 규칙 변화로 변환한다 | requiredCounterTag=counter:sabotage | catalog-registered-static-consumer | active-authored | 3 | [battlefield_unstable-device.asset](../../../../../Assets/Resources/SO/V20/Combat/Modifiers/battlefield_unstable-device.asset) |
