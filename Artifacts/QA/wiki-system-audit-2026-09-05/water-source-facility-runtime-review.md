# 급수 시설 런타임 대조

- H03 세면대는 물4/0.9초/한파 차단, H04 목욕통은 물6/1.2초/한파 차단, L03 통더미는 물3/1.1초/한파 허용을 작성한다.
- `work:draw-water`는 WaterSource ability가 있고 한파 차단 시설이 ColdSnap이 아닐 때만 가능하다.
- 완료 시 `survival:clean_water`를 시설 중심의 loose physical item으로 생성하고, 생성 API 실패 때만 Water category warehouse 생산을 시도한다. 실제 수량 또는 실패 activity를 기록한다.
