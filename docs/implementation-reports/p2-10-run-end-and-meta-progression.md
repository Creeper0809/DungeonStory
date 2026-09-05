# P2 10 런 종료와 계승 구현 보고

## 구현 범위

P2 10은 로그라이트 런의 끝과 다음 런으로 이어지는 계승 구조를 1차 구현한다.

이번 구현의 핵심은 다음 세 가지다.

- 사장이 죽으면 런이 끝난다.
- 런 결과를 지표로 정산해 계승 화폐를 지급한다.
- 계승 화폐로 다음 런의 선택지와 초반 반복 피로를 줄인다.

## 런 종료 흐름

런 종료 조건은 `사장방이 뚫림`이 아니라 `사장 캐릭터 사망`이다.

흐름:

```text
침입자가 사장 위치에 도달
-> InvasionIntruderRuntime.ApplyFinalCombat
-> 사장 Character.ApplyDamage
-> 체력 0 이하
-> Character.Die
-> OwnerRunManager.HandleOwnerDeath
-> OwnerRunEndedEvent
-> MetaProgressionRuntime.EndRun
```

`MetaProgressionRuntime.EndRun`은 다음 처리를 한다.

- 런 결과 스냅샷 생성
- 계승 화폐 계산
- 메타 진행 상태에 화폐 누적
- 특수 조합식 기록 보존
- `RunResultReadyEvent` 발행
- 우하단/오른쪽 이벤트 알림 발행
- 간단한 런 결과 패널 표시

결과 상세에는 현재 런의 돈, 재고, 배치 시설이 계승되지 않는다는 내용을 명시한다.

## 계승 화폐 계산

계승 화폐는 `MetaProgressionCalculator.CalculateLegacyCurrency`에서 계산한다.

반영 지표:

- 생존 시간
- 생존 운영일
- 운영일 정산 횟수
- 막아낸 침입 수
- 도달한 최대 위협 단계
- 최종 침입 강도
- 최초 발견 시설 수
- 최초 해금 조합식 수
- 오펜스 성과 수
- 난이도 배율

계산 방향:

```text
계승 화폐 =
생존 보상
+ 방어 보상
+ 위협 단계 보상
+ 발견/조합식 보상
+ 오펜스 보상
* 난이도 배율
```

중요한 설계 판단:

- 오래 버틴 보상이 가장 크다.
- 발견/조합식 보상은 상한을 둬서 짧게 죽고 보너스만 챙기는 플레이가 최적이 되지 않게 한다.
- 오펜스는 아직 P3이므로 `RecordOffenseSuccess` API만 먼저 열어두었다.

## 초기 계승 트리

`MetaProgressionCatalog`는 3개 갈래를 가진다.

```text
운영 지식
설계 보존
사장 생존
```

초기 강화:

- 시작 시설 후보 +1
- 시작 사장 특성 후보 +1
- 1성 기본 시설 구매 목록 확장
- 특수 조합식 기록 일부 보존
- 사장 생존력 소폭 증가
- 침입 전 경고 정확도 증가

## 실제 연결 지점

### 시작 시설 후보 증가

`RunVariableRuntime.CreateStartVariables`에서 시작 시설 후보 수를 계산할 때 `MetaProgressionRuntime.GetStartingFacilityCandidateBonus`를 더한다.

### 1성 기본 구매 목록 확장

`FacilityShopService.CreateBasicPurchaseOffers`가 기본 구매 후보를 만들 때 `MetaProgressionRuntime.GetExpandedBasicPurchaseBuildingIds`를 추가 허용 목록으로 사용한다.

### 특수 조합식 기록 보존

런 중 해금한 조합식은 `MetaProgressionRuntime`에 누적된다.

런 종료 시 `SpecialRecipeRecordSlot` 레벨만큼 조합식 ID를 보존한다.

`FacilitySynthesisService.IsRecipeVisible`은 연구 해금 여부와 별개로 보존된 연구 키 또는 실제 합성 `recipeId`도 표시 가능하게 본다.

### 사장 생존력 증가

`Character.RecalculateVitals`에서 사장 캐릭터의 최대 체력에 `MetaProgressionRuntime.GetOwnerMaxHealthMultiplier`를 곱한다.

### 침입 전 경고 정확도 증가

`InvasionThreatRuntime`의 경고/후보 임계치 계산에 `MetaProgressionRuntime.GetInvasionWarningThresholdMultiplier`를 곱한다.

값이 낮아질수록 같은 위협도에서 더 빨리 경고가 뜬다.

## 디버그 시나리오

추가 파일:

```text
Assets/Scripts/Meta/Editor/MetaProgressionDebugScenarios.cs
```

검증 항목:

- 사장 사망 시 런 결과가 생성되는가
- 생존 보상이 발견 보상보다 우선되는가
- 운영 지식 강화가 시작 시설 후보와 기본 구매 목록에 반영되는가
- 설계 보존 강화가 조합식 기록 보존에 반영되는가
- 사장 생존 강화가 체력/특성 후보/경고 임계치에 반영되는가

Unity 메뉴:

```text
DungeonStory/Debug/Meta/Run P2 Meta Progression Scenarios
```

## 남은 의도적 미구현

- 계승 상태 저장/로드
- 메타 강화 전용 UI
- 런 재시작 버튼과 결과 화면 UX polish
- 오펜스 성과의 실제 이벤트 연결
- 시작 사장 특성 후보 선택 UI 연결

이번 단계에서는 런 종료와 계승 계산, 다음 런 보정 API를 먼저 고정했다.
