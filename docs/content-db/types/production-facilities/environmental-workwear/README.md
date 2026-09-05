# EnvironmentalWorkwearSO

종족별 환경 보호 수단을 장비 아이템과 연구 해금에 연결한다

총 4개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/production-facilities/environmental-workwear.csv)
- [중첩 작성 필드 CSV](../../../fields/production-facilities/environmental-workwear.csv)
- [정방향 관계 CSV](../../../relations/production-facilities/environmental-workwear.csv)
- [역방향 관계 CSV](../../../incoming/production-facilities/environmental-workwear.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `workwear:cold-work-suit` | 방한 작업복 | 종족별 환경 보호 수단을 장비 아이템과 연구 해금에 연결한다 | workwearId=workwear:cold-work-suit; requiredResearchId=research:environment:cold-work | catalog-registered-static-consumer | active-authored | 0 | [ColdWorkSuit.asset](../../../../../Assets/Resources/SO/Environment/Workwear/ColdWorkSuit.asset) |
| `workwear:hauling-harness` | 운반 멜빵 | 종족별 환경 보호 수단을 장비 아이템과 연구 해금에 연결한다 | workwearId=workwear:hauling-harness; requiredResearchId=research:commerce:logistics | catalog-registered-static-consumer | active-authored | 0 | [HaulingHarness.asset](../../../../../Assets/Resources/SO/Environment/Workwear/HaulingHarness.asset) |
| `workwear:rune-cold-suit` | 룬 방한복 | 종족별 환경 보호 수단을 장비 아이템과 연구 해금에 연결한다 | workwearId=workwear:rune-cold-suit; requiredResearchId=research:environment:rune-insulation | catalog-registered-static-consumer | active-authored | 0 | [RuneColdSuit.asset](../../../../../Assets/Resources/SO/Environment/Workwear/RuneColdSuit.asset) |
| `workwear:slime-warming-pad` | 보온 점액 패드 | 종족별 환경 보호 수단을 장비 아이템과 연구 해금에 연결한다 | workwearId=workwear:slime-warming-pad; requiredResearchId=research:environment:cold-work | catalog-registered-static-consumer | active-authored | 0 | [SlimeWarmingPad.asset](../../../../../Assets/Resources/SO/Environment/Workwear/SlimeWarmingPad.asset) |
