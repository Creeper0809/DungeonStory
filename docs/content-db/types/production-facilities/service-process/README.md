# ServiceProcessSO

생산·시설 영역의 작성 콘텐츠 유형이다.

총 5개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/production-facilities/service-process.csv)
- [중첩 작성 필드 CSV](../../../fields/production-facilities/service-process.csv)
- [정방향 관계 CSV](../../../relations/production-facilities/service-process.csv)
- [역방향 관계 CSV](../../../incoming/production-facilities/service-process.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `service:bathing:wash` | service_bathing_wash | 생산·시설 영역의 ServiceProcessSO 규칙을 분리해 재사용한다. | cleanWater=0.45; wastewater=0.45; allowsManualWaterFallback=1 | catalog-registered-static-consumer | active-authored | 0 | [service_bathing_wash.asset](../../../../../Assets/Resources/SO/ServiceRooms/Processes/service_bathing_wash.asset) |
| `service:dining:meal` | service_dining_meal | 생산·시설 영역의 ServiceProcessSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [service_dining_meal.asset](../../../../../Assets/Resources/SO/ServiceRooms/Processes/service_dining_meal.asset) |
| `service:lodging:rest` | service_lodging_rest | 생산·시설 영역의 ServiceProcessSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [service_lodging_rest.asset](../../../../../Assets/Resources/SO/ServiceRooms/Processes/service_lodging_rest.asset) |
| `service:medical:treat` | service_medical_treat | 생산·시설 영역의 ServiceProcessSO 규칙을 분리해 재사용한다. | workTypeId=work:treat | catalog-registered-static-consumer | active-authored | 0 | [service_medical_treat.asset](../../../../../Assets/Resources/SO/ServiceRooms/Processes/service_medical_treat.asset) |
| `service:retail:sale` | service_retail_sale | 생산·시설 영역의 ServiceProcessSO 규칙을 분리해 재사용한다. | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [service_retail_sale.asset](../../../../../Assets/Resources/SO/ServiceRooms/Processes/service_retail_sale.asset) |
