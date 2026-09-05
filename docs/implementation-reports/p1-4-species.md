# P1 1.4 종족 1차 구현 보고

## 목표

`docs/game-design-todo.md`의 `1.4 종족 1차 구현`을 완료했다.

이번 단계의 목표는 종족을 단순 태그가 아니라 경영, 작업, 전투, 사고 위험에 영향을 주는 데이터로 만드는 것이다.

## 구현한 종족

위치:

```text
Assets/Resources/SO/Character/Species
```

초기 3종:

```text
Slime
Orc
Vampire
```

각 종족에는 다음 값이 들어간다.

```text
선호 시설 라벨
기피 환경 라벨
소비 성향
체류 성향
작업 적성
전투 성향
사고 타입
사고 설명
UI용 짧은 설명
```

## 데이터 구조

`CharacterSpeciesSO`에 운영 데이터를 추가했다.

핵심 필드:

```text
shortDescription
preferredFacilityLabels
dislikedEnvironmentLabels
stayDurationMultiplier
incidentType
incidentName
incidentDescription
incidentMitigatingRoles
```

사고 타입:

```text
SlimeContamination
OrcRampage
VampireFear
```

이 값은 아직 사고를 실제로 발생시키지는 않는다. 이후 만족도/사고 시스템에서 어떤 사고를 띄울지 판단하는 입력으로 사용한다.

## 종족별 방향

### 슬라임

요약:

```text
대량 방문/저소비/청소/오염 관리
```

선호:

```text
저가 음식점
휴식방
청소 시설
```

기피:

```text
화염 시설
건조한 구역
훈련장 중심 구역
```

운영 성향:

- 소비와 지출이 낮다.
- 체류 시간이 짧아 대량 회전에 어울린다.
- 청소, 보충, 유지보수에 강하다.
- 전투력은 낮다.
- 사고는 `슬라임 오염`이다.

### 오크

요약:

```text
고소비/전투/훈련/난동 위험
```

선호:

```text
고기 식당
무기점
훈련장
```

기피:

```text
조용한 연구 구역
마력 시설
좁은 휴식 구역
```

운영 성향:

- 소비와 지출이 높다.
- 전투, 경비, 수리에 강하다.
- 혼잡에 비교적 둔감하다.
- 사고 위험이 가장 높다.
- 사고는 `오크 난동`이다.

### 뱀파이어

요약:

```text
장기 체류/연구/마력/공포 위험
```

선호:

```text
연구실
마력 저장소
어둠 휴식 환경
```

기피:

```text
밝은 시설
성스러운 장식
혼잡한 상업 구역
```

운영 성향:

- 식사 부담이 낮다.
- 지출 성향은 높다.
- 체류 시간이 길다.
- 연구와 마력 운영에 강하다.
- 혼잡에 민감하다.
- 사고는 `뱀파이어 공포`다.

## 런타임 연결

`CharacterRuntimeProfile`에 종족 운영 값을 읽는 메서드를 추가했다.

```text
GetStayDurationMultiplier()
GetCrowdSensitivityMultiplier()
GetIncidentType()
GetIncidentName()
GetIncidentDescription()
GetShortDescription()
```

`Character`도 같은 값을 외부 시스템에 전달한다.

```text
character.GetStayDurationMultiplier()
character.GetCrowdSensitivityMultiplier()
character.GetIncidentType()
character.GetSpeciesShortDescription()
```

## 실제 반영 지점

### 시설 체류 시간

`Facility.Interact`에서 시설 이용 시간이 캐릭터의 체류 성향을 반영한다.

```text
최종 이용 시간 = 시설 기본 이용 시간 * 캐릭터 체류 배율
```

그래서 뱀파이어는 연구실/마력 시설 같은 일반 시설에서 더 오래 머물 수 있고, 슬라임은 상대적으로 빠르게 회전한다.

### 혼잡 민감도

`FacilityCandidateScorer`의 혼잡 점수에 캐릭터 혼잡 민감도를 반영했다.

```text
혼잡 감점 = 현재 이용자 비율 * 혼잡 민감도
```

오크는 혼잡에 덜 민감하고, 뱀파이어는 혼잡에 더 민감하다.

### 시설 선택

종족/개인 특성의 시설 역할 선호는 기존 손님 AI 점수 계산에 들어간다.

예시:

```text
슬라임: 식사/휴식 선호
오크: 식사/구매/훈련 선호
뱀파이어: 연구/마력/휴식 선호
```

## 검증

검증 스크립트:

```text
Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs
```

추가 검증 항목:

- 종족별 UI 짧은 설명이 존재한다.
- 종족별 선호 시설과 기피 환경이 존재한다.
- 종족별 사고 타입과 사고 설명이 존재한다.
- 슬라임, 오크, 뱀파이어의 체류 성향이 다르다.
- 오크의 전투/사고 성향이 슬라임/뱀파이어와 다르다.
- 뱀파이어는 오크보다 혼잡에 더 민감하다.

Unity MCP로 다음 결과를 확인했다.

```text
P1 character/species scenarios passed.
P1 regression scenarios passed. Facility=True, Customer=True, Character=True
```

## 아직 남은 점

사고 발생 시스템은 아직 없다.

이번 단계는 다음 단계에서 사고 시스템이 사용할 수 있도록 종족별 사고 타입과 위험 배율을 먼저 마련한 것이다.
