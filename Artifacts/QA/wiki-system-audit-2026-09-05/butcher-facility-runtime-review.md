# 도축 시설 런타임 대조

- 현재 작성 `BuildingButcherAbility` 모집단은 D03 조리손질대 하나이며 `workSeconds=1`이다. D03은 조리 ability와 별개로 Butcher work completion handler를 등록한다.
- 후보는 금지되지 않은 수량 양수의 Stored·Loose·FacilityBuffer 야생 사체 또는 emergency 허용 인간형 사체다. Stored를 우선하고, 그 안에서는 남은 신선도가 낮은 사체부터 고른다.
- 완료 시 사체 stack 전체를 D03 중심 위치의 종별 ButcherYields 실물 stack으로 원자 변환하고, 출력 수량과 활동을 기록하며 freshness 추적을 제거한다. 산출 정의가 없거나 변환에 실패하면 완료되지 않는다.
- emergency 허용 인간형 사체는 고기4·뼈2로 변환되며, 작업자는 동족 여부에 따라 기분 -16/-9(900초)와 위생 -18, 목격 taboo event를 받는다. 공개 D03 페이지와 도축 work 문서는 D03의 1초 gate·선택·변환 및 이 예외 경계를 설명하지 않는다.
