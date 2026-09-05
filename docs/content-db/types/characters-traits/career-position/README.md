# CareerPositionDefinitionSO

인물·특성 영역의 작성 콘텐츠 유형이다.

총 6개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/characters-traits/career-position.csv)
- [중첩 작성 필드 CSV](../../../fields/characters-traits/career-position.csv)
- [정방향 관계 CSV](../../../relations/characters-traits/career-position.csv)
- [역방향 관계 CSV](../../../incoming/characters-traits/career-position.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `career-position:chief-physician` | 수석 의사 | 인물·특성 영역의 CareerPositionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [career-position_chief-physician.asset](../../../../../Assets/Resources/SO/Population/Careers/career-position_chief-physician.asset) |
| `career-position:chief-researcher` | 수석 연구원 | 인물·특성 영역의 CareerPositionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [career-position_chief-researcher.asset](../../../../../Assets/Resources/SO/Population/Careers/career-position_chief-researcher.asset) |
| `career-position:foreman` | 작업반장 | 인물·특성 영역의 CareerPositionDefinitionSO 규칙을 분리해 재사용한다. | requiredFacilityTag=research-overhaul | catalog-registered-static-consumer | active-authored | 0 | [career-position_foreman.asset](../../../../../Assets/Resources/SO/Population/Careers/career-position_foreman.asset) |
| `career-position:guard-captain` | 경비대장 | 인물·특성 영역의 CareerPositionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [career-position_guard-captain.asset](../../../../../Assets/Resources/SO/Population/Careers/career-position_guard-captain.asset) |
| `career-position:mentor` | 멘토 | 인물·특성 영역의 CareerPositionDefinitionSO 규칙을 분리해 재사용한다. | requiredFacilityTag=workstation:v19:mentor-academy | catalog-registered-static-consumer | active-authored | 0 | [career-position_mentor.asset](../../../../../Assets/Resources/SO/Population/Careers/career-position_mentor.asset) |
| `career-position:steward` | 관리인 | 인물·특성 영역의 CareerPositionDefinitionSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [career-position_steward.asset](../../../../../Assets/Resources/SO/Population/Careers/career-position_steward.asset) |
