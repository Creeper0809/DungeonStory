# 야수 우리 런타임 대조

## 작성값과 공개 공백

- `BuildingBeastPenAbility` 작성 asset은 CB01 야수 우리 한 곳이다. `capacity=2`, `baseSecurity=55`, `dailyFood=2`, `dailyWater=2`, `tamingWork=18`, `productCollectionWork=8`이다.
- 공개 `building-1203`은 ability 모듈명·분류·크기만 표시한다. 축산 가이드는 우리/사료/물/노동을 일반적으로 말하지만 CB01의 실제 수용·보안·입력·작업 계약을 제공하지 않는다.

## 포획·수용과 돌봄

1. 생포는 살아 있고 체력이 최대의 35% 이하인 야생동물, 살아 있는 active NPC 운반자, 빈 유효 야수 우리를 요구한다. 점유는 같은 pen ID의 escaped=false 개체로 세며 capacity2 이상이면 포획과 출생 등록을 거부한다.
2. 우리는 usable closed room 안에 있어야 하고 포획 야생동물이 통과할 수 있는 문이 하나라도 있으면 거부된다. 배치 위치는 우리 footprint 밖의 walkable·비점유 cell 중 우리 중심에 가장 가까운 cell이다.
3. 관리 중인 각 동물의 5초 돌봄은 `dailyFood=2`, `dailyWater=2`를 각각 식량/물 need와 물리 input destination에 적용한다. `baseSecurity=55`는 길들임 여부별 계수(길들임0.08, 미길들임0.35)로 `(100-baseSecurity)`를 escape risk에 더하며, 배고픔·갈증·오염과 불안전 문 보정도 함께 적용한다.

## 축산 작업과 저장

- 길들이기 필요 WU는 `18×(1+species tamingDifficulty)`다. 산물 수거는8 WU, 분뇨 수거는 `max(4,8×0.65)=5.2` WU다. 도축 WU는 CB01 작업량이 아니라 종의 bodySize로 `10+bodySize×8`이다.
- 포획 상태에는 pen ID/위치, 운반 상태·예약 운반자, nextCare·탈출/질병/입력 outbox가 저장되며, 축산 상태는 별도 동물·정책 state에서 저장/복원된다. 이 수치는 실제 물리 재료 commit과 결과 출력 경로와 함께 설명해야 한다.

## 정적 증거 한계

- Unity Play Mode 실행은 하지 않았다. 작성 asset, 비Editor 포획/돌봄/축산/저장 호출을 정적으로 대조했다.
