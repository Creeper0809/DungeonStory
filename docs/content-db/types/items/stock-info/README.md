# StockInfo

구형 상점 재고 구성을 특정 상점 식별자와 연결한다

총 11개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/items/stock-info.csv)
- [중첩 작성 필드 CSV](../../../fields/items/stock-info.csv)
- [정방향 관계 CSV](../../../relations/items/stock-info.csv)
- [역방향 관계 CSV](../../../incoming/items/stock-info.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `asset:New Stock Info` | New Stock Info | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [New Stock Info.asset](../../../../../Assets/Resources/SO/Stock/New Stock Info.asset) |
| `asset:무기상점` | 무기상점 | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [무기상점.asset](../../../../../Assets/Resources/SO/Stock/무기상점.asset) |
| `shop-stock:10` | P1_LowFoodShopStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [P1_LowFoodShopStock.asset](../../../../../Assets/Resources/SO/Stock/P1/P1_LowFoodShopStock.asset) |
| `shop-stock:11` | P1_MeatRestaurantStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [P1_MeatRestaurantStock.asset](../../../../../Assets/Resources/SO/Stock/P1/P1_MeatRestaurantStock.asset) |
| `shop-stock:12` | P1_GeneralStoreStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [P1_GeneralStoreStock.asset](../../../../../Assets/Resources/SO/Stock/P1/P1_GeneralStoreStock.asset) |
| `shop-stock:13` | P1_WeaponShopStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [P1_WeaponShopStock.asset](../../../../../Assets/Resources/SO/Stock/P1/P1_WeaponShopStock.asset) |
| `shop-stock:3000` | S01_SalesCounterStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [S01_SalesCounterStock.asset](../../../../../Assets/Resources/SO/Stock/Modular/S01_SalesCounterStock.asset) |
| `shop-stock:50` | P1_BattleDiningStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [P1_BattleDiningStock.asset](../../../../../Assets/Resources/SO/Stock/P1/P1_BattleDiningStock.asset) |
| `shop-stock:51` | P1_PremiumMeatRestaurantStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [P1_PremiumMeatRestaurantStock.asset](../../../../../Assets/Resources/SO/Stock/P1/P1_PremiumMeatRestaurantStock.asset) |
| `shop-stock:55` | P1_BattlefieldDiningStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [P1_BattlefieldDiningStock.asset](../../../../../Assets/Resources/SO/Stock/P1/P1_BattlefieldDiningStock.asset) |
| `shop-stock:56` | P1_NobleDiningStock | 구형 상점 재고 구성을 특정 상점 식별자와 연결한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | legacy-authoring-review | 0 | [P1_NobleDiningStock.asset](../../../../../Assets/Resources/SO/Stock/P1/P1_NobleDiningStock.asset) |
