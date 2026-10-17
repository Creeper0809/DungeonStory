# 서커스 매표소 런타임 대조

- 작성 모집단은 CT01 서커스 매표소 하나이며 `revenueMultiplier=1.15`, `flatRevenuePerAudience=1`이다. 공개 도감은 ability 모듈명·분류·크기만 표시한다.
- 공연 venue 평가는 무대와 같은 방 furniture의 유효 매표소마다 `max(1,revenueMultiplier)`를 곱하고 `max(0,flatRevenuePerAudience)`를 더한 뒤 총 수익 배율을 1~2.5로 제한한다. CT01 하나면 1.15배와 관객당 +1이다.
- 주문 생성 시 표값은 `max(1,round(stage baseTicketPrice×venue revenueMultiplier))`, 매표소 가산은 order의 `venueFlatRevenuePerAudience`로 고정된다. 정산 수익은 `audience count×(ticketPrice+venueFlatRevenuePerAudience)`이며 forecast도 같은 계산을 쓴다.
- ticket price와 venue 가산·수익은 Circus order 저장/복원 검증 대상이다. Unity Play Mode는 실행하지 않았고 작성 asset과 비Editor venue/order/forecast 호출을 정적으로 대조했다.
