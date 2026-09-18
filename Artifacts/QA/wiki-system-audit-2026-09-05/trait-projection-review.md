# 특성100종 효과·조건·정체성 규칙 공개 후속 감사

상태: root content catalog의 생산 특성100개와 공개 entity100개를 정적 전수 대조했다. Unity 실행, 실제 UI 시연, 게임/위키 원본 수정은 하지 않았다.

## 모집단

디스크에는 `CharacterTraitSO` 자산113개가 있다. 이 가운데 ID231~234, 236~238, 240~244, 246의13개는 `CharacterProgressionProfileProjector.RetiredFounderTraitIds`이며 `V26FounderTraitAuditScenario`도 root catalog에서 제외됐음을 검사한다. 공개 대상과 생산 선택 권위는 나머지100개다.

- legacy ID101~109: 9개
- retained general/founder: 91개
- 생산 권위 합계: 100개, ID 고유100
- 공개 `data/entities/character/trait-*.json`: 100개, exact ID 대응100
- 공개 측 고아0, 생산 권위의 페이지 누락0

따라서 디스크113 대 공개100의 단순 차이13을 위키 누락으로 세지 않는다.

## 공개 facts와 실제 작성 payload

공개100페이지의 facts는 총349개다.

- 희귀도100, 성향100
- 효과 개수 fact99: 효과0인 `trait-300`만 해당 fact를 생략한다.
- 정체성 규칙 개수 fact50
- relations: 100페이지 모두0

작성 효과 binding은167개이며 공개 효과 개수의 합도167로 일치한다. 그러나 공개 facts는 개수만 싣고 아래 payload를 노출하지 않는다.

- effect target49종과 각 값·연산 의미
- 조건부 effect50개의 condition45종
- 조건이 없을 때와 있을 때의 적용 범위
- 동일 target에 여러 binding이 합쳐지는 관계

상위 target 분포는 작업속도27, 사고확률17, 전투력10, 소비9, 이동속도7, 사회 시작XP7, 냉기노출6, 학술 시작XP6이다. definition GUID167개와 condition GUID50개는 모두 현재 효과/조건 자산으로 해석됐다.

## 정체성 규칙108개

공개 page의 “정체성 규칙 n개” 합108은 작성 개수와 일치하지만 규칙 종류·event/action tag·수치·기간·조건·저장 상태는 노출하지 않는다.

| 규칙 종류 | 작성 수 |
| --- | ---: |
| BehaviorUtilityRule | 43 |
| EventMoodRule | 41 |
| PersistentNeedRule | 9 |
| MoodImmunityRule | 3 |
| RelationshipMemoryRule | 2 |
| ArcaneOverchargeRule | 1 |
| AutonomousWorkRestrictionRule | 1 |
| ExtremeCraftInspirationRule | 1 |
| ForbiddenResearchLeapRule | 1 |
| GoldenHarvestRule | 1 |
| LastStandRule | 1 |
| MiracleSurgeryRule | 1 |
| MoodTransformRule | 1 |
| PostActionConsequenceRule | 1 |
| ProductionLimitBreakRule | 1 |

일반 effect는 선택 특성을 `IGameplayEffectSource`로 수집하는 `CharacterGameplayEffectProjector` 경로가 있고, identity rule은 `CharacterIdentityRuleRouter`와 등록된 event adapter들이 선택 특성을 읽는다. 이 전수표는 각167/108 payload 모두의 개별 생산 소비 완료를 뜻하지 않으며, 소비가 확인되지 않은 condition·특수 규칙은 후속 구현 대조 대상으로 남긴다.

## 기존 소유자와 신규 판정

- GAP-048: trait-302 금단의 도약의 구체 실행
- GAP-106: trait-300 신들린 영감의 구체 제작 실행
- GAP-156: trait-201/222/245의 방 condition 반응

위 세 항목은 이번 공통 도감 투영 공백의 구체 사례이므로 독립 효과 수만큼 다시 세지 않는다. 신규 GAP-157은 공개 특성100페이지의 actual effect/condition 및 identity rule payload를 한 항목으로 소유한다.

전체 특수 규칙의 gameplay 호출·저장 왕복, 특성 선택 충돌·확률, 표시 UI는 후속 감사 범위다.
