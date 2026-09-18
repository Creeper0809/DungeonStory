# 서커스 치료 구역 런타임 대조

- 작성 모집단은 CM01 공연 치료 구역 하나이며 `accidentDamageMultiplier=0.65`다. 공개 도감은 ability 모듈명·분류·크기만 표시한다.
- 공연 venue 평가는 무대와 같은 방의 유효 치료 구역마다 `clamp(accidentDamageMultiplier,0.25,1)`를 곱하고 최종값도 0.25~1로 제한한다.
- 주문 생성은 이 venue 피해 배율을 고정한다. 사고가 실제 발생하면 무작위 생존 performer 하나에 `8×clamp(order venueAccidentDamageMultiplier,0.25,1)` 피해를 준다. CM01 하나면 5.2 피해다.
- 이 값은 사고 확률을 낮추거나 치료 작업을 직접 수행하지 않으며, Circus order 저장/복원 numeric 검증 대상이다. Unity Play Mode는 실행하지 않았고 작성 asset과 비Editor venue/order/accident 호출을 정적으로 대조했다.
