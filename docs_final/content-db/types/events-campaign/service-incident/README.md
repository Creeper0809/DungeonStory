# ServiceIncidentDefinitionSO

사건·캠페인 영역의 작성 콘텐츠 유형이다.

총 8개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/events-campaign/service-incident.csv)
- [중첩 작성 필드 CSV](../../../fields/events-campaign/service-incident.csv)
- [정방향 관계 CSV](../../../relations/events-campaign/service-incident.csv)
- [역방향 관계 CSV](../../../incoming/events-campaign/service-incident.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `service-incident:brawl` | 난투 | ServiceIncidentDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | riskTier=2; riskReason=작성된 대응에 비치명 체력 피해 -3이 있다. | catalog-registered-static-consumer | active-authored | 0 | [service-incident_brawl.asset](../../../../../Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_brawl.asset) |
| `service-incident:contamination` | 객실 오염 | ServiceIncidentDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | riskTier=2; riskReason=작성된 대응에 활성 압력 식별자를 추가하는 Threat 6이 있다. | catalog-registered-static-consumer | active-authored | 0 | [service-incident_contamination.asset](../../../../../Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_contamination.asset) |
| `service-incident:culturalinsult` | 문화적 모욕 | ServiceIncidentDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | riskTier=1; riskReason=작성된 대응에 세력 원한 8이 있다. | catalog-registered-static-consumer | active-authored | 0 | [service-incident_culturalinsult.asset](../../../../../Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_culturalinsult.asset) |
| `service-incident:envoyconflict` | 외교 사절 충돌 | ServiceIncidentDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | riskTier=1; riskReason=작성된 대응에 작업 지연 1일 또는 세력 원한 8이 있다. | catalog-registered-static-consumer | active-authored | 0 | [service-incident_envoyconflict.asset](../../../../../Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_envoyconflict.asset) |
| `service-incident:forbiddenmeal` | 금기 음식 제공 | ServiceIncidentDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | riskTier=1; riskReason=작성된 대응에 돈 -60 또는 세력 원한 5가 있다. | catalog-registered-static-consumer | active-authored | 0 | [service-incident_forbiddenmeal.asset](../../../../../Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_forbiddenmeal.asset) |
| `service-incident:medicalcollapse` | 의료 응급상황 | ServiceIncidentDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | riskTier=1; riskReason=작성된 대응에 작업 지연 1일 또는 세력 원한 10이 있다. | catalog-registered-static-consumer | active-authored | 0 | [service-incident_medicalcollapse.asset](../../../../../Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_medicalcollapse.asset) |
| `service-incident:sabotage` | 내부 파괴 공작 | ServiceIncidentDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | riskTier=2; riskReason=작성된 대응에 활성 압력 식별자를 추가하는 Threat 3이 있다. | catalog-registered-static-consumer | active-authored | 0 | [service-incident_sabotage.asset](../../../../../Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_sabotage.asset) |
| `service-incident:theft` | 절도 | ServiceIncidentDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | riskTier=1; riskReason=작성된 대응에 작업 지연 1일 또는 돈 -100이 있다. | catalog-registered-static-consumer | active-authored | 0 | [service-incident_theft.asset](../../../../../Assets/Resources/SO/V20/Society/Services/Incidents/service-incident_theft.asset) |
