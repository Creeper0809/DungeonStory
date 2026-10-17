# 조리 ability 시설 런타임 대조

- 작성 모집단은 D01 간이화덕(식량1→식사2, 1.1초, 연료 필요), D02 고기그릴(1→3, 1.3초, 연료 필요), D03 조리손질대(1→1, 1초, 연료 불필요)다.
- `work:cook` 후보는 Food 재고가 해당 input 이상이고, 연료 요구 시설이면 Fuel 재고가 1 이상일 때만 활성이다. production bill이 선택되면 해당 bill 경로가 우선하며, 수동 fallback을 허용하지 않는 production workstation은 bill 실패 뒤에 legacy 조리를 수행하지 않는다.
- ability 완료의 survival 경로는 Food input과 필요한 Fuel 1을 먼저 withdraw하고, 기본 `survival:cooked_meal`을 시설 중심의 loose physical item으로 생성한다. 생성 API가 실패하면 warehouse 생산 경로를 시도하며, 작업자에게 기분 +2/120초와 `food-cooked` activity를 남긴다.
- 같은 방 Preservation ability가 있으면 기본 식사 대신 보존식 item과 preservation authored 수량을 사용한다. 이는 기존 GAP-176 소유이며, 이 보고서는 기본 Cooking ability 계약만 소유한다.
