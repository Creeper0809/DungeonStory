# 시설 이용 완료 효과 대조

조사일: 2026-09-06  
범위: active-authored `BuildingNeedRecoveryAbility` 19개, `BuildingTrainingAbility` 4개, `BuildingExpeditionRecoveryAbility` 4개와 실제 이용 완료 경로·공개 facility page·가이드  
판정 범위: 정적 원본·runtime consumer·생성 위키 대조. Unity Play Mode와 저장 왕복은 실행하지 않았다.

## 결론

- 세 ability는 모두 실제 시설 이용 완료 경로에 연결된 active 기능이다.
- 공개 대상 27개 ability row는 해당 facility page가 모두 존재하지만 facts는 분류·크기 2개, relations는 0개다. summary는 “욕구 회복”, “훈련”, “원정대 회복”이라는 역할명만 보여 준다.
- 시설별 6축 욕구 회복량, 훈련의 일반 경험치·전투 숙련·기분, 원정 회복의 체력·부상·stress 수치가 공개 문서에 없다.
- 서로 다른 사용자 질문과 progression/state 권위를 가지므로 GAP-166(욕구), GAP-167(훈련), GAP-168(원정 회복)로 분리한다.

## 실제 이용 완료 경로

`Facility.Use`가 물·서비스·식사/기호품 처리를 성공적으로 끝내면 다음 경로가 실행된다.

```text
일반 시설: ApplyConfiguredUseRecovery
모든 성공 이용: ApplyFacilityUseCompleted
  → ModularFacilityRuntimeEffects.ApplyUseCompleted
  → 모든 IBuildingUseCompletedRuntimeAbility.ApplyUseCompleted
  → combat training award / routine need notification
```

- 작성 `FacilityNeedRecoveryData`가 한 축이라도 있으면 6축 값을 그대로 적용하고 role fallback은 더하지 않는다.
- 작성 recovery가 없을 때만 역할별 fallback(Rest sleep35/mood12, Training fun15/mood5, Research fun10/mood8, Mana mood10, Logistics mood3, Meal hunger35/mood5, Toilet excretion70/mood2, Hygiene hygiene60/mood4)을 합산한다.
- Training/ExpeditionRecovery는 모든 use-completed ability를 순회하는 dispatcher에 연결된다.

## 시설별 욕구 회복 19개

0인 축은 표에서 생략했다.

| 시설 | 작성 회복량 |
| --- | --- |
| 간이화덕 | hunger35, mood4 |
| 배식카운터 | hunger25, mood3 |
| 고기그릴 | hunger48, mood8 |
| 목욕통 | sleep12, hygiene85, mood10 |
| 세면대 | hygiene45, mood3 |
| 변기 | excretion75, mood2 |
| 샤워 시설 | hygiene72, mood4 |
| 마력수정선반 | mood6 |
| 마력저장조 | mood10 |
| 의식초점석 | fun5, mood12 |
| 연구책상 | fun10, mood4 |
| 연금술작업대 | fun12, mood7 |
| 간이침대 | sleep35, mood4 |
| 정식침대 | sleep52, mood10 |
| 이층침대 | sleep42, mood3 |
| 휴식용소파 | sleep25, fun8, mood6 |
| 훈련허수아비 | fun18, mood4 |
| 사격과녁 | fun16, mood3 |
| 중량훈련석 | fun12, mood5 |

회복은 `BuildingNeedRecoverySnapshot`으로 sleep/mood/fun/hunger/excretion/hygiene와 시설 persistent instance ID, 현재 room condition IDs를 actor에 전달한다.

## 훈련 완료 4개

| 시설 | 일반 경험치 | 기분 | 기간 | 추가 전투 숙련 |
| --- | ---: | ---: | ---: | --- |
| 훈련허수아비 | 24 | +4 | 180초 | primary가 근접이면 근접0.50 |
| 사격과녁 | 24 | +3 | 180초 | primary가 원거리면 원거리0.50 |
| 중량훈련석 | 24 | +5 | 180초 | 근접전투 +0.50 |
| 대련매트 | 24 | +4 | 180초 | 근접전투 +0.50 |

일반 경험치는 캐릭터 level progression에, combat training 0.50은 별도 proficiency command에 들어간다. 작성 primary는 훈련허수아비·중량훈련석·대련매트가 근접전투, 사격과녁이 원거리전투이므로 네 시설 모두 두 진척축을 함께 준다. mood factor key는 시설 persistent instance ID와 ability ID를 포함한다. 공개 가이드의 “직접 훈련1 XP로 마지막 연습 갱신”은 이 시설 이용당 일반 경험치24와 별도 전투 숙련0.50을 대체 설명하지 않는다.

## 원정 회복 4개

| 시설 | 최대 체력 회복 | 부상 severity 감소 | 원정 stress 감소 |
| --- | ---: | ---: | ---: |
| 목욕통 | 8% | 0.12 | 26 |
| 간이침대 | 12% | 0.03 | 12 |
| 정식침대 | 22% | 0.08 | 22 |
| 이층침대 | 18% | 0.05 | 18 |

ability 이름은 ExpeditionRecovery지만 이용자에게 별도 원정 귀환 조건을 검사하지 않는다. 정상 시설 이용 완료 시 최대체력 비율 회복, 현재 injury severity 감소, expedition recovery stress 감소를 각각 적용한다.

`P1_RestRoom`과 `P1_Washroom`에도 compatibility ability가 남아 있으나 두 자산은 deprecated이며 공개 제외된다. 위 표와 GAP 분모에는 포함하지 않았다.

## 공개 문서와 저장 경계

- 대상 page27행은 page 누락0, facts2 전부, relations0 전부다.
- summary는 역할명을 표시하지만 숫자·적용 시점·두 경험치 축·작성 recovery 우선 규칙을 표시하지 않는다.
- 가이드 검색에서도 facility-specific payload는0건이다.
- 캐릭터 save에는 currentHealth, injurySeverity, condition 값, moodFactors의 value/remainingSeconds, level/currentExperience, `CharacterExpeditionRecoveryState.stress`가 있다. facility 자체에 별도 소비 상태를 저장하는 효과가 아니라 완료 시 character state에 즉시 반영된다.

## 권장 문서 경계

- 각 facility entity에 이용 시간/역할과 함께 실제 회복·훈련 payload를 표로 노출한다.
- residents-and-work에는 훈련 완료가 일반 경험치24와 조건부 전투 숙련0.50이라는 서로 다른 진척을 준다는 점을 설명한다.
- expeditions/health 문서에는 네 회복 시설의 HP·부상·stress 효과와 “귀환자 전용 조건 없음”을 정확히 설명한다.
- 19개 recovery는 작성 값이 role fallback을 대체한다는 우선순위를 문서화한다.

## 직접 확인한 원본

- `Assets/Resources/SO/Building/Modular/`
- `Assets/Resources/SO/Building/Industrial/I14_샤워_시설.asset`
- `Assets/Scripts/Services/Buildings/Facility.cs`
- `Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs`
- `Assets/Scripts/Services/Buildings/ModularFacilityRuntimeEffects.cs`
- `Assets/Scripts/Services/Character/Core/CharacterBuildingVisitorPort.cs`
- `Assets/Scripts/Services/Character/Core/CharacterLifecycle.cs`
- `Assets/Scripts/Services/Character/Ability/AbilityWork.cs`
- `Assets/Scripts/Services/Character/Core/DungeonCharacterSaveData.cs`
- `wiki/game-versions/0.0.1v/data/entities/facility/`
- `wiki/game-versions/0.0.1v/content/guides/residents-and-work.md`
- `wiki/game-versions/0.0.1v/content/guides/expeditions.md`
