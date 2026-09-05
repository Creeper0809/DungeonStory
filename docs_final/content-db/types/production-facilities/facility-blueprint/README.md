# FacilityBlueprintSO

설계도 비용과 연구 작업, 해금 대상을 시설·연구 진행에 연결한다

총 7개 항목이며, 비교군은 실제 대체 가능성을 검토하기 위한 후보군이다.

## 데이터

- [유형별 콘텐츠 CSV](../../../csv/production-facilities/facility-blueprint.csv)
- [중첩 작성 필드 CSV](../../../fields/production-facilities/facility-blueprint.csv)
- [정방향 관계 CSV](../../../relations/production-facilities/facility-blueprint.csv)
- [역방향 관계 CSV](../../../incoming/production-facilities/facility-blueprint.csv)

| 안정 ID | 이름 | 전략적 역할 | 비용·위험 | 런타임 상태 | 수명주기 | 역참조 | 구현 권위 |
|---|---|---|---|---|---|---:|---|
| `facility-blueprint:6101` | 상업 확장 설계도 | 설계도 비용과 연구 작업, 해금 대상을 시설·연구 진행에 연결한다 | defaultCost=120; researchWorkRequired=18 | catalog-registered-static-consumer | active-authored | 1 | [BP_CommercialBasics.asset](../../../../../Assets/Resources/SO/Blueprint/P1/BP_CommercialBasics.asset) |
| `facility-blueprint:6102` | 요새화 설계도 | 설계도 비용과 연구 작업, 해금 대상을 시설·연구 진행에 연결한다 | defaultCost=140; researchWorkRequired=22 | catalog-registered-static-consumer | active-authored | 1 | [BP_DefenseBasics.asset](../../../../../Assets/Resources/SO/Blueprint/P1/BP_DefenseBasics.asset) |
| `facility-blueprint:6103` | 생활 지원 설계도 | 설계도 비용과 연구 작업, 해금 대상을 시설·연구 진행에 연결한다 | defaultCost=130; researchWorkRequired=20 | catalog-registered-static-consumer | active-authored | 1 | [BP_SupportBasics.asset](../../../../../Assets/Resources/SO/Blueprint/P1/BP_SupportBasics.asset) |
| `facility-blueprint:6104` | 비전 연구 설계도 | 설계도 비용과 연구 작업, 해금 대상을 시설·연구 진행에 연결한다 | defaultCost=130; researchWorkRequired=24 | catalog-registered-static-consumer | active-authored | 1 | [BP_ArcaneBasics.asset](../../../../../Assets/Resources/SO/Blueprint/P1/BP_ArcaneBasics.asset) |
| `facility-blueprint:6191` | 상권 통합 설계도 | 설계도 비용과 연구 작업, 해금 대상을 시설·연구 진행에 연결한다 | defaultCost=260; researchWorkRequired=45 | catalog-registered-static-consumer | active-authored | 1 | [BP_BattleDining.asset](../../../../../Assets/Resources/SO/Blueprint/P1/BP_BattleDining.asset) |
| `facility-blueprint:6192` | 전술 지휘 설계도 | 설계도 비용과 연구 작업, 해금 대상을 시설·연구 진행에 연결한다 | defaultCost=280; researchWorkRequired=50 | catalog-registered-static-consumer | active-authored | 1 | [BP_TrapChain.asset](../../../../../Assets/Resources/SO/Blueprint/P1/BP_TrapChain.asset) |
| `facility-blueprint:6193` | 비전 공명 설계도 | 설계도 비용과 연구 작업, 해금 대상을 시설·연구 진행에 연결한다 | defaultCost=340; researchWorkRequired=60 | catalog-registered-static-consumer | active-authored | 1 | [BP_StormFireTrap.asset](../../../../../Assets/Resources/SO/Blueprint/P1/BP_StormFireTrap.asset) |
