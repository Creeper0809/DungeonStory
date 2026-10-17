# 서커스 진행자 단상 런타임 대조

- 작성 모집단은 CA01 진행자 단상 하나이며 `satisfactionBonus=6`, `preparationWorkMultiplier=0.9`다. 공개 도감은 ability 모듈명·분류·크기만 표시한다.
- 공연 venue 평가는 무대와 같은 방 furniture의 유효 진행자 단상마다 `max(0,satisfactionBonus)`를 합산하고 `clamp(preparationWorkMultiplier,0.5,1)`를 준비 작업 배율에 곱한다. 만족도 합계는 0~35로 제한된다.
- forecast는 중심 만족도에 venue satisfaction bonus를 더한 값을 0~100으로 제한한다. 주문 생성은 `max(1,stage preparationWork×venue PreparationWorkMultiplier)`와 venue satisfaction bonus를 고정한다.
- 공연 정산은 고정된 venue satisfaction bonus를 기본 settlement에 더해 0~100으로 제한한다. 주문의 준비 작업량과 venue 만족도 보정은 Circus order 저장/복원 numeric 검증 대상이다. Unity Play Mode는 실행하지 않았고 작성 asset과 비Editor venue/order/forecast 호출을 정적으로 대조했다.
