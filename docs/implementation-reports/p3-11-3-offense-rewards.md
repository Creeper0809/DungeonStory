# P3 11.3 오펜스 보상 구현 보고

## 구현 파일

- `Assets/Scripts/Offense/OffenseRewardSystem.cs`
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`
- `Assets/Scripts/Offense/Editor/OffenseRewardDebugScenarios.cs`
- `Assets/Scripts/GameManager.cs`

## 구현 목표

오펜스 원정 성공이 단순 결과 문구로 끝나지 않고, 런 상태에 실제 보상으로 반영되게 했다.

## 핵심 구조

`OffenseRewardPreview`는 월드맵에 보이는 예상 보상 데이터로 유지했다.

실제 지급은 `OffenseRewardRuntime`과 `OffenseRewardService`가 담당한다.

```text
원정 성공
-> OffenseExpeditionRuntime
-> OffenseRewardRuntime.ApplyExpeditionRewards
-> OffenseRewardService.GrantRewards
-> 돈/재고/시설/설계도/세력 상태/후보 상태 반영
```

## 보상별 처리

- 돈: `GameData.holdingMoney`에 즉시 더한다.
- 재고: `StockSupplyService.GrantReward`로 창고에 입고한다.
- 희귀 시설: 2성 이상 시설을 골라 해금하고, 2성 이하는 기본 구매에도 넣는다.
- 설계도: 조건에 맞는 설계도를 획득 상태로 표시하고 연구 대기열에 넣는다.
- 세력 약화: 인간 세력 또는 경쟁 세력 약화 값을 `OffenseRewardState`에 누적한다.
- 새 손님/직원 후보: `RecruitCandidateCount`에 누적한다.
- 포로/특수 몬스터: `PrisonerCount`, `SpecialMonsterCount`에 누적한다.

기본 월드맵의 `경쟁 던전 전초기지`는 희귀 시설, 세력 약화, 직원 후보, 특수 몬스터 보상을 함께 제공한다.

## 결과 표시

`OffenseExpeditionResult`에 `grantedRewards`를 추가했다.

실제 지급 결과가 있으면 결과 상세의 `지급 결과`에 표시하고, 보상 런타임이 없는 테스트 상황에서는 기존 보상 요약을 그대로 사용한다.

## 검증 시나리오

`DungeonStory/Debug/Offense/Run P3 Reward Scenarios` 메뉴를 추가했다.

검증 항목:

- 돈, 재고, 세력 약화, 직원 후보, 포로 보상 지급
- 희귀 시설과 특수 설계도 지급
- 원정 완료 시 실제 보상 지급 연결

## 설계 의도

오펜스는 보상 버튼이 아니라 바깥 세계를 건드리는 선택이어야 한다.

그래서 보상은 단순 아이템 지급이 아니라 다음 시스템으로 이어지는 상태를 남긴다.

- 재고 보상은 현재 던전 운영을 보완한다.
- 시설/설계도 보상은 합성 목표를 만든다.
- 세력 약화는 이후 침입 압력 조절에 연결할 수 있다.
- 후보/포로/특수 몬스터는 영입 시스템과 연결할 수 있다.
