# P1 6.3 시설물끼리의 합성 구현 보고

## 목표

시설 합성을 `재료 아이템 조합`이 아니라 실제 배치된 시설물끼리의 조합으로 구현한다.

핵심 흐름:

```text
시설 선택
-> 조합식 확인
-> 합성 실행
-> 재료 시설 제거
-> 결과 시설 배치
-> 결과 시설 레벨 계승
```

## 구현 파일

- `Assets/Scripts/Synthesis/FacilitySynthesisRecipeSO.cs`
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`
- `Assets/Scripts/Synthesis/Editor/P1FacilitySynthesisAssetBuilder.cs`
- `Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs`
- `Assets/Scripts/Buildings/BuildableObject.cs`
- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Resources/SO/Synthesis/P1/*.asset`
- `Assets/Resources/SO/Building/P1/P1_BattleDining.asset`
- `Assets/Resources/SO/Building/P1/P1_PremiumMeatRestaurant.asset`
- `Assets/Resources/SO/Building/P1/P1_VenomSpikeTrap.asset`
- `Assets/Resources/SO/Building/P1/P1_AlarmCoil.asset`
- `Assets/Resources/SO/Building/P1/P1_Barracks.asset`
- `docs/game-design-todo.md`

## 조합식 데이터

`FacilitySynthesisRecipeSO`를 추가했다.

주요 데이터:

```text
recipeId
displayName
description
resultBuilding
materialBuildings
publicByDefault
requiredResearchRecipeId
levelInheritanceRatio
```

일반 조합식은 `publicByDefault = true`로 처음부터 보인다.

특수 조합식은 `requiredResearchRecipeId`가 필요하며, 6.2 연구 시스템의 `BlueprintResearchState.UnlockedRecipeIds`에 같은 ID가 있을 때만 보인다.

## 합성 대상

P1에서는 합성 대상이 인벤토리 아이템이 아니라 `배치 중인 시설물`이다.

이렇게 정한 이유:

- 현재 게임은 시설 인벤토리보다 그리드 배치가 먼저 구현되어 있다.
- 플레이어가 실제 던전 구조를 바꾸는 감각이 더 강하다.
- 시설 위치, 파손 여부, 레벨 계승을 자연스럽게 합성 규칙에 포함할 수 있다.

## 합성 실행 규칙

`FacilitySynthesisRuntime.TrySynthesize`가 합성 실행을 담당한다.

검증 순서:

```text
조합식 데이터 유효성
조합식 해금 여부
재료 선택 여부
재료 파괴/파손 여부
중복 재료 여부
조합식과 재료 ID 일치 여부
같은 그리드 여부
결과 시설 배치 가능 여부
```

파손 시설은 수리 전까지 합성 재료로 사용할 수 없다.

## 결과 배치

결과 시설은 첫 번째로 선택한 재료 시설의 위치에 배치된다.

합성 실행:

```text
첫 번째 재료 위치 저장
모든 재료 시설을 그리드에서 제거
결과 시설 생성
결과 시설을 저장한 위치에 등록
```

결과 시설이 재료 위치 위에 배치 가능한지 미리 검사한다. 결과 시설 영역에 다른 시설이 있으면 합성하지 않는다.

## 레벨 계승

`BuildableObject`에 `FacilityLevel`을 추가했다.

합성 결과 레벨:

```text
결과 레벨 = round(재료 평균 레벨 * 조합식 계승 비율)
최소 1레벨
```

P1 일반 조합식은 대체로 `0.75` 계승률을 사용하고, 특수 조합식은 `0.8` 계승률을 사용한다.

예시:

```text
Lv.4 고기 식당 + Lv.2 휴식방
-> 평균 Lv.3
-> 0.75 계승
-> 결과 Lv.2
```

## 공개 조합식과 특수 조합식

공개 조합식:

```text
고기 식당 + 훈련장 -> 2성 전투 식당
고기 식당 + 휴식방 -> 2성 고급 고기 식당
가시 함정 + 독 웅덩이 -> 2성 맹독 가시 함정
경비실 + 훈련장 -> 2성 병영
```

특수 조합식:

```text
번개 기둥 + 경비실 -> 2성 경보 코일
필요 연구 ID: recipe_trap_chain_2
```

`recipe_trap_chain_2`는 6.2의 `연쇄 함정 설계도` 연구 완료로 열린다.

## 결과 시설 성격

`2성 전투 식당`:

- 평상시: 식당 역할
- 추가 성격: 훈련 역할, 경비 작업 지원
- 의도: 오크/전투형 던전 방향

`2성 고급 고기 식당`:

- 평상시: 식당 역할
- 추가 성격: 휴식 역할
- 의도: 체류/만족도 강화 방향

`2성 맹독 가시 함정`:

- 침입 시: 피해 + 부식
- 의도: 물리 피해와 독/부식 컨셉 결합

`2성 경보 코일`:

- 침입 시: 피해 + 축전 + 경비 교전
- 의도: 번개 연계와 경비 대응 결합

`2성 병영`:

- 평상시/운영: 훈련 성격
- 침입 시: 강화된 경비 교전
- 의도: 경비/훈련 계열의 첫 합성 결과

## 합성 UI

`FacilitySynthesisPanel`을 추가했다.

역할:

- 선택된 재료 시설 표시
- 현재 보이는 조합식 표시
- 연구 완료 또는 합성 완료 이벤트 후 갱신

P1에서는 최소 동작 가능한 UI 컴포넌트로 두었다. 실제 프리팹 연결과 상세 버튼 스타일링은 이후 UI 작업에서 확장하면 된다.

## 왜 이렇게 구현했나

### 결과를 첫 번째 재료 위치에 배치한 이유

합성 결과를 인벤토리에 넣는 방식은 시설 인벤토리, 배치 UI, 합성 UI를 동시에 요구한다.

현재 구조에서는 배치된 시설을 직접 합성하고 결과를 같은 위치에 배치하는 방식이 가장 명확하다.

플레이어 입장에서도 `이 시설을 상위 시설로 바꿨다`는 감각이 강하다.

### 재료 시설을 실제로 제거한 이유

던전메이커식 합성의 핵심은 시설을 재료로 소모해 새 시설을 만드는 성장감이다.

그래서 합성에 사용한 시설은 그리드 점유와 오브젝트 모두 제거한다.

### 특수 조합식을 연구 상태로 제어한 이유

6.2에서 설계도 연구가 이미 조합식 ID를 해금한다.

합성 시스템은 이 ID를 그대로 읽어 특수 조합식 표시 여부를 결정한다. 이렇게 하면 설계도 연구와 합성이 직접 연결된다.

## 검증

Unity MCP에서 6.3 전용 시나리오를 실행했다.

```text
DungeonStory/Debug/Synthesis/Run P1 Facility Synthesis Scenarios
```

검증 내용:

- P1 합성 결과 시설과 조합식 에셋이 생성된다.
- 일반 조합식은 처음부터 보인다.
- 특수 조합식은 연구 전에는 숨겨지고, 연구 ID 해금 후 보인다.
- 배치된 시설 2개를 합성하면 재료 시설이 제거되고 결과 시설이 배치된다.
- 결과 시설은 재료 레벨 일부를 계승한다.
- 파손 시설은 합성 재료로 사용할 수 없다.
- 합성 UI 컴포넌트가 선택 재료와 조합식을 표시한다.

실행 결과:

```text
FacilitySynthesis=True
```

전체 P1 회귀 검증도 통과했다.

```text
Grid=True
Facility=True
Customer=True
Character=True
Owner=True
WorkPriority=True
Command=True
StaffDuty=True
Operation=True
Feedback=True
Alerts=True
InvasionThreat=True
InvasionIntruder=True
Defense=True
CombatReport=True
FacilityShop=True
BlueprintResearch=True
FacilitySynthesis=True
```

Unity 콘솔 에러/경고:

```text
0 errors, 0 warnings
```
