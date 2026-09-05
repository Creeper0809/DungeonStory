# P1 1.3 캐릭터 모델 구현 보고

## 목표

`docs/game-design-todo.md`의 `1.3 캐릭터 모델`을 구현했다.

이번 단계의 목표는 캐릭터를 단순한 손님/직원 prefab이 아니라, 역할이 바뀌어도 유지되는 하나의 몬스터 개체로 다룰 수 있게 만드는 것이다.

구조:

```text
캐릭터 = 능력 + 개인 특성 + 종족 특성
```

## 데이터 구조

추가한 핵심 파일:

```text
Assets/Scripts/Character/SO/CharacterModelData.cs
Assets/Scripts/Character/SO/CharacterSpeciesSO.cs
Assets/Scripts/Character/SO/CharacterTraitSO.cs
```

능력치:

```text
Attack: 공격
Sales: 영업
Research: 연구
MoveSpeed: 이동속도
Strength: 힘
Toughness: 맷집
Dexterity: 손재주
Cleaning: 청소
Endurance: 지구력
```

1차 능력과 2차 능력을 같은 `CharacterStatBlock`에 둔 이유는, 후속 시스템에서 전투/경영/물류가 같은 캐릭터 모델을 참조하게 하기 위해서다.

## 최종 계산 위치

최종 계산은 `CharacterRuntimeProfile`이 담당한다.

계산 순서:

```text
기본 능력치
-> 종족 능력 보정
-> 개인 특성 능력 보정
-> 종족/특성 배율 보정
-> 최종 효율 반환
```

외부 시스템은 개별 SO를 직접 계산하지 않고 다음 메서드를 사용한다.

```text
character.GetCharacterStat(...)
character.GetMoveSpeed()
character.GetConsumptionMultiplier()
character.GetWorkSpeedMultiplier(...)
character.GetWorkPreferenceScore(...)
character.GetFacilityPreferenceScore(...)
character.GetAccidentChanceMultiplier()
```

이렇게 해서 능력/개인 특성/종족 특성 보정이 한 곳에서 합쳐진다.

## 종족 특성

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

각 종족은 다음 값을 가진다.

```text
종족 태그
표시 이름
설명
능력치 보정
시설 역할 선호/기피
작업 종류 선호/기피
소비/지출/작업/연구/전투/사고 배율
```

## 개인 특성

위치:

```text
Assets/Resources/SO/Character/Traits
```

초기 8개:

```text
대식가
절약가
부자
성급함
소심함
싸움꾼
깔끔함
연구가
```

개인 특성은 같은 종족 안에서도 다른 결과를 만들기 위한 보정이다.

예시:

```text
오크 + 대식가:
음식 소비와 사고 위험이 증가

오크 + 절약가:
소비와 사고 위험이 감소

오크 + 싸움꾼:
전투/경비/훈련 쪽으로 강해지지만 사고 위험 증가

오크 + 연구가:
종족상 연구가 약하더라도 연구 작업 성능을 일부 보완
```

## 런타임 연결

`CharacterSO`에 다음 필드를 추가했다.

```text
species
baseStats
traits
```

`Character.Initialization`에서 `CharacterRuntimeProfile`을 생성한다.

역할은 `characterType`으로 구분하지만, 능력/특성/종족 프로필은 역할보다 위에 있다. 그래서 손님이 직원이나 방어 인력으로 전환되어도 같은 캐릭터 데이터와 보정값을 유지할 수 있다.

## 기존 시스템 연결

### 이동

`AbilityMove`는 이제 `CharacterSO.moveSpeed`만 보지 않고 `character.GetMoveSpeed()`를 사용한다.

반영 요소:

```text
기본 이동속도
MoveSpeed 능력치
종족/특성 이동속도 배율
```

### 소비

`AbilityShopping`은 소지금과 구매 횟수에 캐릭터 프로필을 반영한다.

반영 요소:

```text
Sales 능력치
spendingMultiplier
consumptionMultiplier
```

예시:

```text
부자: 소지금 증가
대식가: 구매/소비 횟수 증가
절약가: 소비 성향 감소
```

### 손님 시설 선택

`FacilityCandidateScorer`는 기존 시설의 `preferredSpeciesTags`뿐 아니라 캐릭터 프로필의 시설 역할 선호/기피도 함께 본다.

즉, 종족 태그와 캐릭터 모델 선호가 같이 반영된다.

### 직원 작업 선택

`AbilityWork`는 이제 방문 가능한 시설만 보지 않고, 도달 가능한 전체 시설 중 작업 가능한 시설을 찾는다.

작업 대상 점수:

```text
작업 선호 점수 70%
작업 속도 점수 30%
```

이 변경으로 창고처럼 손님 방문 시설이 아닌 시설도 직원 작업 후보가 될 수 있다.

## 검증

추가한 검증 스크립트:

```text
Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs
```

검증 항목:

- 종족 SO 3개 존재
- 개인 특성 SO 8개 존재
- 기본 능력치 + 종족 보정 + 개인 특성 보정 합산
- 같은 오크라도 대식가/절약가/싸움꾼에 따라 소비와 사고 배율이 달라짐
- 싸움꾼/연구가/깔끔함에 따라 작업 속도와 작업 선호가 달라짐
- `characterType`이 바뀌어도 프로필 계산이 유지됨
- `Character` 런타임에서 `SpeciesTag`, 연구 능력, 시설 선호, 작업 속도 보정을 읽을 수 있음

Unity MCP로 다음 결과를 확인했다.

```text
P1 character model scenarios passed.
P1 regression scenarios passed. Facility=True, Customer=True, Character=True
```

## 아직 남은 점

만족도 사고 시스템 자체는 아직 구현하지 않았다.

이번 단계에서는 사고 시스템이 나중에 사용할 수 있도록 다음 값을 캐릭터 모델에 먼저 열어두었다.

```text
character.GetAccidentChanceMultiplier()
```

실제 난동, 도난, 오염, 공포 같은 사고 발생 로직은 이후 만족도/사고 시스템 구현 단계에서 이 값을 사용한다.
