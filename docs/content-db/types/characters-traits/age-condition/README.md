# AgeConditionDefinitionSO

인물·특성 영역의 작성 콘텐츠 유형이다.

총 6개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/characters-traits/age-condition.csv)
- [중첩 작성 필드 CSV](../../../fields/characters-traits/age-condition.csv)
- [정방향 관계 CSV](../../../relations/characters-traits/age-condition.csv)
- [역방향 관계 CSV](../../../incoming/characters-traits/age-condition.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `condition:age-cardiac-degeneration` | 심장 기능 퇴행 | 인물·특성 영역의 AgeConditionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [AgeCondition_age-cardiac-degeneration.asset](../../../../../Assets/Resources/SO/Population/AgeConditions/AgeCondition_age-cardiac-degeneration.asset) |
| `condition:age-neural-degeneration` | 신경 기능 퇴행 | 인물·특성 영역의 AgeConditionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [AgeCondition_age-neural-degeneration.asset](../../../../../Assets/Resources/SO/Population/AgeConditions/AgeCondition_age-neural-degeneration.asset) |
| `condition:age-organ-fibrosis` | 장기 섬유화 | 인물·특성 영역의 AgeConditionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [AgeCondition_age-organ-fibrosis.asset](../../../../../Assets/Resources/SO/Population/AgeConditions/AgeCondition_age-organ-fibrosis.asset) |
| `condition:core-corrosion` | 핵 부식 | 인물·특성 영역의 AgeConditionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [AgeCondition_core-corrosion.asset](../../../../../Assets/Resources/SO/Population/AgeConditions/AgeCondition_core-corrosion.asset) |
| `condition:frame-fatigue` | 골격 피로 | 인물·특성 영역의 AgeConditionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [AgeCondition_frame-fatigue.asset](../../../../../Assets/Resources/SO/Population/AgeConditions/AgeCondition_frame-fatigue.asset) |
| `condition:rune-circuit-wear` | 룬 회로 마모 | 인물·특성 영역의 AgeConditionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [AgeCondition_rune-circuit-wear.asset](../../../../../Assets/Resources/SO/Population/AgeConditions/AgeCondition_rune-circuit-wear.asset) |
