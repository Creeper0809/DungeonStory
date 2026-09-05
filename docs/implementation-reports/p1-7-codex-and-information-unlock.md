# P1 7 도감과 정보 해금 구현 보고

## 목표

전투 결과 화면이 직접 처방을 하지 않아도, 플레이어가 도감에서 다음 실험의 단서를 얻을 수 있게 한다.

도감은 다음 세 갈래로 시작한다.

- 몬스터 도감
- 침략 도감
- 시설 도감

## 구현 파일

- `Assets/Scripts/Codex/CodexSystem.cs`
- `Assets/Scripts/Codex/UI/CodexPanel.cs`
- `Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/FacilityShop/Editor/P1FacilityShopAssetBuilder.cs`

## 구조

### CodexState

도감의 실제 상태를 가진다.

구조:

```text
카테고리 + 항목 ID
-> 도감 항목
-> 정보 라인 목록
```

정보 라인은 중복으로 쌓이지 않는다.

정보 출처는 다음으로 구분한다.

```text
System
Observation
Research
Synthesis
Evolution
```

### CodexRuntime

게임 이벤트를 듣고 도감 상태를 갱신한다.

연결한 이벤트:

- `FacilityVisitEvent`: 손님 종족과 방문 시설 관찰
- `DefenseFacilityTriggeredEvent`: 방어 시설 효과와 침입자 약점 관찰
- `InvasionCombatReportReadyEvent`: 침입 결과의 관찰 정보 누적
- `InvasionFacilityDamagedEvent`: 침입자의 시설 파괴 성향 기록
- `InvasionSpawnedEvent`: 침입자 항목 발견
- `BlueprintResearchCompletedEvent`: 연구로 해금된 시설/조합식 기록
- `FacilitySynthesisCompletedEvent`: 합성 결과와 조합식 기록
- `FacilityEvolutionCompletedEvent`: 시설 계보 진화 결과, 정체성, 서사, 변이 태그 기록

`GameManager`는 시작 시 `CodexRuntime`을 자동으로 붙인다.

## 몬스터 도감

몬스터 도감은 `CharacterSpeciesSO` 기준으로 만든다.

기록하는 정보:

- 짧은 설명
- 선호 시설
- 기피 환경
- 사고 위험
- 사고 완화 역할
- 방문 관찰 로그

손님이 시설을 방문하면 해당 종족 항목에 관찰 정보가 추가된다.

## 침략 도감

P1에서는 `돌파형 침입자` 항목부터 시작한다.

기본 정보:

```text
주의: 사장 캐릭터 처치
주의: 사장방 돌파
성향: 시간이 지날수록 사장 위치 추적
저항: 공포 효과
```

관찰로 추가되는 정보:

```text
약점: 감속
약점: 근접 교전
약점: 방어력 감소
약점: 축전 연계
약점: 지속 피해
약점: 직접 피해
성향: 시설 파괴 우선
```

문구는 전부 현재형 태그로 둔다.

## 시설 도감

시설 도감은 `BuildingSO` 기준으로 만든다.

기록하는 정보:

- 역할
- 작업 종류
- 수용 인원
- 재고 필요 여부
- 별 등급
- 공격 컨셉
- 발동 조건
- 대상 규칙
- 방어 효과
- 합성 조합식
- 계보 진화 기록
- 진화 정체성/서사/변이 태그

공개 조합식은 바로 표시한다.

특수 조합식은 연구 전에는 일부 힌트만 표시한다.

예시:

```text
특수 조합식 힌트: 번개 계열 연구 필요
```

연구 후에는 실제 조합식을 표시한다.

예시:

```text
조합식: 2성 경보 코일 + 1성 화염 분사구 -> 3성 폭뢰 분사구
```

계보 진화 완료 시에는 결과 시설 도감에 다음 형태로 기록한다.

```text
계보 진화: 고기 식당 -> 2성 전투 식당 (2성)
진화식: 전투 식당 진화
정체성: 용병 이용 기록과 전투 기물이 강하게 누적된 고기 식당
진화 기록: 용병들이 식탁을 전투 전 회의 장소로 바꾸었습니다.
해석 출처: LocalLLM
변이: Combat
```

## 설계도 보완

6.4에서 추가한 `폭뢰 분사구`는 `recipe_trap_chain_3` 연구가 필요하다.

이번 단계에서 실제로 그 레시피를 열 수 있도록 `폭뢰 분사구 설계도`를 추가했다.

```text
폭뢰 분사구 설계도
-> recipe_trap_chain_3 해금
```

## UI

`CodexPanel`은 P1용 최소 UI다.

표시 항목:

- 몬스터 도감
- 침략 도감
- 시설 도감

정식 메뉴 UI가 아니라, 현재 도감 상태를 한 화면에 요약해서 확인하는 패널이다.

## 검증 시나리오

`CodexDebugScenarios`를 추가했다.

검증 항목:

- 몬스터/침략/시설 기준 데이터 생성
- 특수 조합식이 연구 전에는 힌트만 보이는지
- 특수 조합식이 연구 후 실제 조합식으로 보이는지
- 방어 시설 발동 관찰이 침략 도감 약점으로 기록되는지
- 손님 방문이 몬스터 도감 관찰 정보로 기록되는지
- 시설 계보 진화 완료 이벤트가 시설 도감에 계보/정체성/서사/변이 정보를 남기는지
- `CodexPanel`이 세 도감 섹션을 렌더하는지

Unity 메뉴:

```text
DungeonStory/Debug/Codex/Run P1 Codex Scenarios
```

## 검증 상태

```text
Generated: 2026-07-04 06:19:15
CodexDebugScenarios.RunAll = True
ImplementedScenarioDebugRunner.RunAll = True
Suites: 29
Passed: 29
Failed: 0
Console errors: 0
Console warnings: 9
```

콘솔 경고 9건은 기존 `UIBuildingInfo`, `Meta/Offense` TMP obsolete 경고와 Behavior Designer 패키지 obsolete 경고다.
