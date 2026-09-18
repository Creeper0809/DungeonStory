# 감방 구속대 런타임 대조

## 작성값과 실제 수용 경로

- `BuildingCaptiveHousingAbility` 작성 asset은 CP01 감방 구속대 한 곳이다. `capacity=1`, `restraintSlots=1`, `baseSecurity=45`, `acceptsHumanoids=true`다.
- 실제 validity는 `capacity>0 && acceptsHumanoids`다. capture는 가까운 유효 감방을 찾고, 이미 수용 중인 포로의 같은 housing ID 점유가 capacity 이상이면 거부한다.
- 감방은 destroyed가 아니고 usable closed room 안에 있어야 하며, 포로가 쓸 수 있는 문이 하나라도 있으면 거부된다. facility footprint 밖의 walkable 빈 cell이 있어야 배정·호송을 시작한다.

## 실제 사용 범위와 저장

- CP01의 capacity1은 포획 배정과 restore 시 housing occupancy 검증에 쓰인다. 유효한 감방은 포로 관리 interaction, 노역 도구/돌봄 입력 및 재사회화 시설 gate에도 쓰인다.
- `restraintSlots=1`과 `baseSecurity=45`는 선언/작성 외의 비Editor consumer가 없다. 포로 구속구 1개 요구나 탈출 위험을 이 두 facility 필드의 효과로 설명하면 안 된다. `acceptsHumanoids=true`은 현재 종족별 선별 기능이 아니라 validity gate다.
- Captivity save v3는 housing building ID/position, 구속구 stack·item·수량, 운반자/간수 예약, 수용/interaction/labor 상태를 저장한다. restore는 housing ID가 현존하는 유효 감방인지와 occupancy가 capacity를 넘지 않는지 다시 검증한다.

## 정적 증거 한계

- Unity Play Mode 실행은 하지 않았다. 작성 asset, 비Editor 수용/상태/복원/UI 호출을 정적으로 대조했다.
