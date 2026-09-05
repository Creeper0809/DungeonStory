# FacilityEvolutionRecordTokenDefinitionSO

운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다

총 9개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/production-facilities/facility-evolution-record-token.csv)
- [중첩 작성 필드 CSV](../../../fields/production-facilities/facility-evolution-record-token.csv)
- [정방향 관계 CSV](../../../relations/production-facilities/facility-evolution-record-token.csv)
- [역방향 관계 CSV](../../../incoming/production-facilities/facility-evolution-record-token.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `CleanServiceStreak` | 청결한 서비스 연속 | 운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다 | threshold=0.7 | catalog-registered-static-consumer | active-authored | 0 | [RT_CleanServiceStreak.asset](../../../../../Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_CleanServiceStreak.asset) |
| `FrequentBrawls` | 잦은 소란 | 운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다 | threshold=1 | catalog-registered-static-consumer | active-authored | 0 | [RT_FrequentBrawls.asset](../../../../../Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_FrequentBrawls.asset) |
| `GuardRallyPoint` | 경비 집결지 | 운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다 | threshold=0.4 | catalog-registered-static-consumer | active-authored | 0 | [RT_GuardRallyPoint.asset](../../../../../Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_GuardRallyPoint.asset) |
| `HighMeatConsumption` | 고기 소비 과다 | 운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다 | threshold=1 | catalog-registered-static-consumer | active-authored | 0 | [RT_HighMeatConsumption.asset](../../../../../Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_HighMeatConsumption.asset) |
| `HighTurnoverService` | 빠른 회전 서비스 | 운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다 | threshold=0.65 | catalog-registered-static-consumer | active-authored | 0 | [RT_HighTurnoverService.asset](../../../../../Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_HighTurnoverService.asset) |
| `IntruderBloodied` | 침입자 유혈 기록 | 운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다 | threshold=1 | catalog-registered-static-consumer | active-authored | 0 | [RT_IntruderBloodied.asset](../../../../../Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_IntruderBloodied.asset) |
| `MercenaryHangout` | 용병 단골화 | 운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다 | threshold=0.25 | catalog-registered-static-consumer | active-authored | 0 | [RT_MercenaryHangout.asset](../../../../../Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_MercenaryHangout.asset) |
| `NoblePatronage` | 귀족 후원 | 운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다 | threshold=0.25 | catalog-registered-static-consumer | active-authored | 0 | [RT_NoblePatronage.asset](../../../../../Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_NoblePatronage.asset) |
| `OutlawRumor` | 무법자 소문 | 운영 기록 지표를 시설 진화 조건으로 변환하고 소비·감쇠 정책을 규정한다 | threshold=1 | catalog-registered-static-consumer | active-authored | 0 | [RT_OutlawRumor.asset](../../../../../Assets/Resources/SO/FacilityEvolution/RecordTokens/P1/RT_OutlawRumor.asset) |
