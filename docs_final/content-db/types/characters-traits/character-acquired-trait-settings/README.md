# CharacterAcquiredTraitSettingsSO

인물·특성 영역의 작성 콘텐츠 유형이다.

총 1개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/characters-traits/character-acquired-trait-settings.csv)
- [중첩 작성 필드 CSV](../../../fields/characters-traits/character-acquired-trait-settings.csv)
- [정방향 관계 CSV](../../../relations/characters-traits/character-acquired-trait-settings.csv)
- [역방향 관계 CSV](../../../incoming/characters-traits/character-acquired-trait-settings.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `character:acquired-trait-settings` | acquired-trait-settings | 인물·특성 영역의 CharacterAcquiredTraitSettingsSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [acquired-trait-settings.asset](../../../../../Assets/Resources/SO/V25/AcquiredTraits/acquired-trait-settings.asset) |
