# WeatherFrontDefinitionSO

사건·캠페인 영역의 작성 콘텐츠 유형이다.

총 6개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/events-campaign/weather-front.csv)
- [중첩 작성 필드 CSV](../../../fields/events-campaign/weather-front.csv)
- [정방향 관계 CSV](../../../relations/events-campaign/weather-front.csv)
- [역방향 관계 CSV](../../../incoming/events-campaign/weather-front.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `weather:clear` | 맑음 | WeatherFrontDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | minimumDurationDays=1; maximumDurationDays=3 | catalog-registered-static-consumer | active-authored | 0 | [WeatherFront_clear.asset](../../../../../Assets/Resources/SO/World/Climate/WeatherFront_clear.asset) |
| `weather:cold-snap` | 한파 | WeatherFrontDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | minimumDurationDays=2; maximumDurationDays=5 | catalog-registered-static-consumer | active-authored | 0 | [WeatherFront_cold-snap.asset](../../../../../Assets/Resources/SO/World/Climate/WeatherFront_cold-snap.asset) |
| `weather:fog` | 안개 | WeatherFrontDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | minimumDurationDays=1; maximumDurationDays=3 | catalog-registered-static-consumer | active-authored | 0 | [WeatherFront_fog.asset](../../../../../Assets/Resources/SO/World/Climate/WeatherFront_fog.asset) |
| `weather:heatwave` | 폭염 | WeatherFrontDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | minimumDurationDays=2; maximumDurationDays=5 | catalog-registered-static-consumer | active-authored | 0 | [WeatherFront_heatwave.asset](../../../../../Assets/Resources/SO/World/Climate/WeatherFront_heatwave.asset) |
| `weather:rain` | 비 | WeatherFrontDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | minimumDurationDays=2; maximumDurationDays=4 | catalog-registered-static-consumer | active-authored | 0 | [WeatherFront_rain.asset](../../../../../Assets/Resources/SO/World/Climate/WeatherFront_rain.asset) |
| `weather:storm` | 폭풍 | WeatherFrontDefinitionSO 계열에서 조건·선택·결과를 통해 캠페인 상태 변화를 만든다. | minimumDurationDays=1; maximumDurationDays=2 | catalog-registered-static-consumer | active-authored | 0 | [WeatherFront_storm.asset](../../../../../Assets/Resources/SO/World/Climate/WeatherFront_storm.asset) |
