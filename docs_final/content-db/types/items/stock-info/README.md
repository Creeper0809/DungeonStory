# StockInfo

구형 상점 재고 구성을 특정 상점 식별자와 연결한다

총 1개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/items/stock-info.csv)
- [중첩 작성 필드 CSV](../../../fields/items/stock-info.csv)
- [정방향 관계 CSV](../../../relations/items/stock-info.csv)
- [역방향 관계 CSV](../../../incoming/items/stock-info.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `shop-stock:3000` | S01_SalesCounterStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [S01_SalesCounterStock.asset](../../../../../Assets/Resources/SO/Stock/Modular/S01_SalesCounterStock.asset) |
