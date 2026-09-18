# 서커스 위험 장치 런타임 대조

- 작성 모집단은 CH01 공연 위험 장치 하나이며 `accidentRiskBonus=0.08`, `satisfactionBonus=8`이다. 공개 도감은 ability 모듈명·분류·크기만 표시한다.
- 공연 venue 평가는 무대와 같은 방 furniture의 유효 위험 장치마다 양 값을 음수 방지로 합산한다. 만족도 합계는 0~35, 사고 확률 가산은 0~0.5로 제한된다.
- forecast는 기본 사고 확률과 venue 사고 가산을 합쳐 0~1로 제한하고, 중심 만족도에는 venue 만족도 보정을 더해 0~100으로 제한한다. 주문 생성 시 두 venue 값을 고정한다.
- 공연 중 사고 판정은 쇼 진행 25% 이후 한 번, `clamp01(program baseAccidentRisk+order venueAccidentRiskBonus)`와 결정론적 난수를 비교한다. 저장 복원 numeric 검증은 두 고정 venue 값을 검사한다. Unity Play Mode는 실행하지 않았고 작성 asset과 비Editor venue/order/forecast 호출을 정적으로 대조했다.
