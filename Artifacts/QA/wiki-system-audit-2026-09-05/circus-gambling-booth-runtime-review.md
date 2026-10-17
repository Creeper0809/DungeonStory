# 서커스 도박 창구 런타임 대조

- 작성 모집단은 CG01 서커스 도박 창구 하나이며 `revenuePerAudience=3`, `satisfactionVariance=5`다. 공개 도감은 ability 모듈명·분류·크기만 표시한다.
- 공연 venue 평가는 무대와 같은 방 furniture의 유효 도박 창구마다 `max(0,revenuePerAudience)`를 관객당 가산 수익에 더하고 `max(0,satisfactionVariance)`를 도박 변동폭에 더한다. CG01 하나면 관객당 +3, 만족도 변동폭 ±5다.
- forecast는 `audience count×(ticketPrice+venueFlatRevenuePerAudience)`로 수익을 계산하고 중심 만족도에서 변동폭을 뺀/더한 범위를 표시한다. 주문 생성은 가산 수익과 변동폭을 order에 고정한다.
- 공연 정산은 결정론적 난수로 `[-venueGamblingVariance,+venueGamblingVariance]` swing을 뽑아 기본 settlement와 venue 만족도 보정에 합산한 뒤 0~100으로 제한한다. 수익은 고정된 관객당 가산으로 계산한다. 변동폭은 Circus order 저장/복원 numeric 검증 대상이다. Unity Play Mode는 실행하지 않았고 작성 asset과 비Editor venue/order/forecast 호출을 정적으로 대조했다.
