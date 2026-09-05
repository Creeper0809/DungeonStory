# FacilitySynthesisRecipeSO

시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다

총 9개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/production-facilities/facility-synthesis-recipe.csv)
- [중첩 작성 필드 CSV](../../../fields/production-facilities/facility-synthesis-recipe.csv)
- [정방향 관계 CSV](../../../relations/production-facilities/facility-synthesis-recipe.csv)
- [역방향 관계 CSV](../../../incoming/production-facilities/facility-synthesis-recipe.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `recipe_arcane_alchemy_1` | 연금술작업대 개조 | 시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [RS_AlchemyBench.asset](../../../../../Assets/Resources/SO/Synthesis/P1/RS_AlchemyBench.asset) |
| `recipe_arcane_reservoir_1` | 마력저장조 조립 | 시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [RS_ManaReservoir.asset](../../../../../Assets/Resources/SO/Synthesis/P1/RS_ManaReservoir.asset) |
| `recipe_arcane_ritual_2` | 의식초점석 조율 | 시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다 | requiredResearchRecipeId=recipe_arcane_ritual_2 | catalog-registered-static-consumer | active-authored | 1 | [RS_RitualFocus.asset](../../../../../Assets/Resources/SO/Synthesis/P1/RS_RitualFocus.asset) |
| `recipe_commerce_secure_display_2` | 잠금진열장 개조 | 시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다 | requiredResearchRecipeId=recipe_commerce_secure_display_2 | catalog-registered-static-consumer | active-authored | 1 | [RS_SecureDisplay.asset](../../../../../Assets/Resources/SO/Synthesis/P1/RS_SecureDisplay.asset) |
| `recipe_commercial_grill_1` | 고기그릴 개량 | 시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [RS_CommercialGrill.asset](../../../../../Assets/Resources/SO/Synthesis/P1/RS_CommercialGrill.asset) |
| `recipe_fortress_banner_2` | 전투깃발 제작 | 시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다 | requiredResearchRecipeId=recipe_fortress_banner_2 | catalog-registered-static-consumer | active-authored | 1 | [RS_BattleBanner.asset](../../../../../Assets/Resources/SO/Synthesis/P1/RS_BattleBanner.asset) |
| `recipe_fortress_patrol_1` | 순찰상황판 제작 | 시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [RS_PatrolBoard.asset](../../../../../Assets/Resources/SO/Synthesis/P1/RS_PatrolBoard.asset) |
| `recipe_fortress_tactical_1` | 전술지도탁자 제작 | 시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [RS_TacticalTable.asset](../../../../../Assets/Resources/SO/Synthesis/P1/RS_TacticalTable.asset) |
| `recipe_logistics_shelf_1` | 대형보관선반 조립 | 시설 모듈을 조합해 새로운 시설 구성을 만드는 경로를 규정한다 | 작성 자산에서 직접 비용·위험 수치를 확인할 수 없음 | catalog-registered-static-consumer | active-authored | 0 | [RS_LogisticsShelf.asset](../../../../../Assets/Resources/SO/Synthesis/P1/RS_LogisticsShelf.asset) |
