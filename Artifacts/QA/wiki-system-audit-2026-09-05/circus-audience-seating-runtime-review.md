# 서커스 관람석 런타임 대조

## 작성값과 공개 공백

- `BuildingAudienceSeatingAbility` 작성 asset은 CS02 서커스 관람석 하나다. `capacity=2`, `sightQuality=0.8`이며 공개 `building-1202`은 ability 모듈명·분류·크기만 표시한다.
- 공개 공연 가이드는 포로/프로그램/잔혹성만 설명하며 관람석의 실제 방 조건, 관객 선택, 이동, 수익 및 비소비 필드를 설명하지 않는다.

## 실제 소비 경로

1. 공연을 시작하려면 사용 가능한 닫힌 방의 무대, 유효 관람석 하나 이상, 문 하나 이상이 필요하다. 관람석이 없으면 주문을 만들지 않는다.
2. 같은 방의 유효 관람석 `capacity`를 모두 합산해 최대 관객 수를 정한다. 살아 있는 `Customer`만 방 중심까지 Manhattan 거리 오름차순으로 골라 order의 audience ID로 고정한다.
3. 관객은 우선 각 관람석의 중심 cell 하나씩으로 이동한다. 관객 수가 유효 fixture 수보다 많으면 실제 좌석 칸을 늘리는 대신 같은 방의 walkable cell을 무대에서 먼 순으로 채운다. 따라서 CS02의 capacity 2는 customer 두 명의 선택 상한이지만, 2개의 고정 좌석 좌표를 보장하지 않는다.
4. 관객 입장이 끝나면 공연이 진행되고, 정산 수익은 `audience count × (ticketPrice + venueFlatRevenuePerAudience)`다. order의 audience ID/position·phase·수익 등은 Circus state capture/restore 대상이며 패널은 무대에서 프로그램 예측, 상태·진행률, 취소를 표시한다.

## 비소비 경계

- `sightQuality=0.8`의 비Editor 역검색 결과는 선언과 CS02 작성값뿐이다. 현재 관객 수, 좌석 배치, 만족·수익·사고 또는 UI에 사용되지 않으므로 시야 품질 효과로 공개하지 않는다.
- 이 보고서는 공연 프로그램의 포로 피해/공포/명성 공식과 다른 venue abilities를 소유하지 않는다.

## 정적 증거 한계

- Unity Play Mode 실행은 하지 않았다. 작성 asset과 현재 비Editor room/order/movement/save/UI 호출을 정적으로 대조했다.
