# 시설 계보 진화

## 요약

이 문서는 기존의 `시설끼리 합성`과 `상위 방` 아이디어를 정리하고, 최종 방향을 `시설 계보 진화`로 재정의한다.

시설 합성, 방 파츠, 운영 기록, 소문/평판, 범죄/위생, LLM 역할을 하나로 묶은 최신 결정본은 [컨텍스트 기반 시설 계보 진화 최종안](context-driven-facility-evolution.md)을 기준으로 한다.

방의 벽, 문, 가구, 동선, 손님층, 직원 상태, 사건, 평판을 실제 판정값으로 압축하는 상세 규칙은 [방 맥락 기반 시설 계보 진화](room-context-lineage-evolution.md)를 기준으로 한다.

핵심 결론:

- 시설끼리 직접 합성하지 않는다.
- 방 자체가 상위 방으로 변하는 구조도 쓰지 않는다.
- 합성/진화 대상은 `현재 배치된 시설 그 자체`다.
- 방은 진화 결과물이 아니라, 진화 조건과 운영 기록을 제공하는 환경이다.
- LLM은 한 번만 개입하지 않고, 시설이 다음 계보로 갈 때마다 현재 기록을 해석해 후보와 서사를 제안한다.
- 최종 결과, 성능, 비용, 성급, 해금 여부는 게임 코드와 기획 데이터가 검증한다.

한 줄 정의:

```text
현재 시설
+ 방 조건
+ 기물/환경 태그
+ 고객 이용 기록
+ 사건/평판/소문
+ 재료/연구/설계도
+ LLM 해석
-> 같은 위치의 다음 계보 시설로 진화
```

예:

```text
일반 식당
+ 훈련 요소
+ 용병 이용 기록
+ 고급 고기
+ LLM 해석
= 전투 식당

전투 식당
+ 침입 방어 기록
+ 경비대 이용 기록
+ 피 묻은 전리품
+ LLM 해석
= 전장의 식당
```

## 버리는 개념

### 시설끼리 직접 합성

이전 방향:

```text
고기 식당 + 훈련장 = 전투 식당
```

문제:

- 림월드식 방/가구 파츠 구조와 충돌한다.
- 훈련장이 재료로 사라지는 감각이 경영 시뮬레이션과 어색할 수 있다.
- 방 안의 수많은 구성물 중 어떤 것은 합성 대상이고 어떤 것은 아닌지 감각이 흔들린다.

최종 방향:

```text
고기 식당이 훈련 요소와 용병 이용 기록을 받아 전투 식당으로 진화한다.
```

훈련장은 직접 소모되는 합성 재료가 아니라, 전투 식당으로 갈 수 있음을 증명하는 환경/기록/태그를 제공한다.

### 상위 방

이전 방향:

```text
식당 방 + 훈련장 방 = 전투 식당 방
```

문제:

- 상위 방을 만드는 요소가 애매하다.
- 합성 후 벽, 문, 가구, 방 크기, 재료 방의 처리 규칙이 복잡해진다.
- `방이 합성되는가`, `시설이 합성되는가`, `업태가 바뀌는가`가 섞인다.

최종 방향:

```text
방은 조건과 기록을 제공한다.
시설이 진화한다.
```

방은 여전히 중요하다.

다만 방은 결과물이 아니라 다음 역할을 한다.

- 닫힌 공간인지 검증
- 필요한 기물/태그가 있는지 검증
- 고객 이용 기록과 사건 기록 제공
- 시설이 작동할 수 있는 환경 제공
- LLM이 해석할 맥락 제공

### 코어

`코어`라는 용어는 사용하지 않는다.

이유:

- 추상적이고 모바일 RPG 강화 재료처럼 느껴진다.
- 방/가게/던전 운영 감각과 맞지 않는다.
- 플레이어가 실제로 보고 만지는 시설의 존재감이 약해진다.

최종 용어:

```text
시설
업태
계보
진화
성급
운영 기록
기록 토큰
```

내부 데이터에서 추상 ID는 필요하지만, 기획/UX 용어는 `시설 계보 진화`로 통일한다.

## 핵심 판타지

플레이어가 느껴야 하는 감각:

```text
내 식당이 그냥 수치 업그레이드된 게 아니다.
이 방에서 실제로 용병들이 먹고 떠들고 싸웠기 때문에
전투 식당이라는 계보로 진화했다.
```

그리고 여기서 끝나지 않는다.

```text
전투 식당도 계속 기록을 쌓는다.
누가 이용했는지, 어떤 사건이 있었는지, 어떤 침입을 막았는지에 따라
전장의 식당, 약탈자의 식당, 용병 연회장 같은 상위 계보로 이어진다.
```

즉 이 시스템은 단순 업그레이드가 아니라 `운영으로 증명되는 시설 진화`다.

## 최종 설계 감각

이 시스템에서 플레이어가 조작하는 것은 `방 파츠 배치`이고, 성장하는 것은 `시설의 계보`다.

```text
벽/문/가구/동선/방 크기
+ 고객 이용 기록
+ 사건/소문/평판
+ 재료/연구/설계도
-> 현재 시설의 다음 계보 후보가 열린다.
```

핵심은 모든 파츠마다 조합식을 따로 만드는 게 아니다.

테이블, 의자, 장식, 훈련 더미, 카운터, 문, 방 크기 같은 요소는 각각 독립적인 합성 재료가 아니라 `RoomProfile`을 만드는 입력이다. 게임은 이 입력을 다음처럼 압축한다.

```text
절대 수량: 좌석 수, 테이블 수, 장식 수
비율: 좌석 밀도, 테이블 밀도, 좌석당 장식 점수, 좌석당 서비스 점수
공간성: 좌석 간격, 방 면적, 출입구 수, 동선 혼잡
운영 기록: 방문 수, 회전율, 평균 지불액, 체류 시간, 만족도
고객층: 용병/귀족/범죄자/단골 비율
사건성: 소란, 도난, 침입 피해, 경비 대응, 재고 실패
```

따라서 `테이블/의자가 많으면 대중 식당`, `테이블/의자가 적으면 고급 식당`처럼 바로 판정하지 않는다.

```text
수량
+ 방 크기와 좌석 밀도
+ 동선과 대기열
+ 좌석당 장식/서비스
+ 손님층과 평균 지불액
+ 회전율과 체류 시간
+ 위생/소문/사건 기록
-> 업태 정체성
-> 시설 계보 후보
```

예를 들어 같은 `일반 식당`이라도 다음처럼 갈라진다.

```text
테이블과 의자가 많음
+ 좌석 밀도 높음
+ 회전율 높음
+ 많은 손님을 빠르게 처리함
-> 대중 식당 / 군중 식당 계보

테이블과 의자가 적음
+ 좌석 간격 넓음
+ 좌석당 장식/서비스 점수 높음
+ 평균 지불액 높음
+ 귀족 손님 비율 높음
-> 고급 식당 / 귀족 식당 계보

식당에 훈련 요소가 섞임
+ 전투형 손님 이용 기록
+ 고기 소비량 높음
+ 소란 또는 무용담 기록
-> 전투 식당 / 용병 식당 계보
```

따라서 `상위 방`이 따로 존재하는 것이 아니다.

방은 계속 방이고, 방의 배치와 역사가 현재 시설을 어느 방향으로 밀어 올리는지 결정한다.

계보도 단순한 상하 등급표가 아니다.

```text
일반 식당
-> 대중 식당
-> 군중 급식소
-> 전시 보급 식당

일반 식당
-> 고급 식당
-> 귀족 만찬장
-> 왕실 연회장

일반 식당
-> 전투 식당
-> 용병 연회장
-> 전장의 식당
```

모두 `식당`에서 출발하지만, 어떤 배치와 운영 기록이 쌓였는지에 따라 다른 축으로 깊어진다.

그래서 진화의 감각은 `상위 방으로 업그레이드`가 아니라 `운영으로 증명된 업태가 다음 시설 계보로 굳어짐`이다.

이 구조에서 합성 감각은 다음처럼 재정의된다.

```text
기존 합성: A 시설 + B 시설 = C 시설
최종 합성: A 시설 + 방의 정체성 + 운영으로 증명된 기록 + 재료 = A의 다음 계보 시설
```

LLM은 이 흐름에서 `결과를 창조하는 신`이 아니라 `맥락 해석기`다.

- 게임 엔진이 먼저 가능한 후보 풀을 만든다.
- LLM은 후보 풀 안에서 현재 시설의 정체성과 어울리는 순서를 제안한다.
- LLM은 이유, 서사, 흐릿한 영감, 변이 수식어 후보를 만든다.
- LLM은 존재하지 않는 시설 ID, 비용, 수치, 성능을 만들 수 없다.
- 게임 코드는 최종 조건, 비용, 성급, 결과 시설, 밸런스를 검증한다.

이렇게 하면 던전메이커식 `계속 합성해서 성급을 올리는 재미`와 림월드식 `공간/가구/운영 맥락이 방의 성격을 만드는 재미`를 같이 살릴 수 있다.

### 상위 방 없이 깊이를 만드는 법

`상위 방`을 빼도 성장 깊이는 줄어들지 않는다.

깊이는 방 등급이 아니라 `현재 시설의 계보`, `방의 정체성`, `운영 기록`, `연구/재료`가 매 단계 다시 결합하면서 생긴다.

```text
1성 일반 식당
-> 2성 대중 식당
-> 3성 군중 급식소
-> 4성 전시 보급 식당
-> 5성 마왕군 대식당
```

```text
1성 일반 식당
-> 2성 고급 식당
-> 3성 귀족 만찬장
-> 4성 왕실 연회장
-> 5성 마왕의 연회궁
```

```text
1성 일반 식당
-> 2성 전투 식당
-> 3성 용병 연회장
-> 4성 군단 식당
-> 5성 전장의 심장
```

이때 각 단계는 단순 레벨업이 아니다.

```text
현재 시설
+ 현재 방의 새 배치
+ 그동안 누적된 손님층
+ 사건/소문/평판
+ 직원 관리 상태
+ 새로 해금한 연구와 재료
-> 다음 계보 후보
```

즉 `전투 식당`이 되면 LLM 개입이 끝나는 것이 아니라, 전투 식당 역시 다음 운영 기록을 다시 읽는다.

예:

```text
전투 식당
+ 침입 방어 성공
+ 경비 직원 반복 이용
+ 용병 손님 단골화
+ 무기 장식과 고기 소비
-> 용병 연회장 또는 전장의 식당 후보
```

반대로 같은 전투 식당이라도 운영이 달라지면 다른 방향으로 깊어진다.

```text
전투 식당
+ 절도 소문
+ 범죄 손님 장기 체류
+ 낮은 경비 커버리지
+ 악명 상승
-> 약탈자의 식당 또는 무법 투기 주점 후보
```

이 구조에서 합성 실행감은 다음 장치로 만든다.

- 현재 시설은 결과 시설로 실제 교체된다.
- 성급은 오른다.
- 재료, 돈, 마나, 일부 기록 토큰을 소모한다.
- 기존 시설의 히스토리와 변이 태그 일부를 계승한다.
- 결과 시설은 역할, 고객층, AI 선호, 방어/경영 효과가 달라진다.
- 도감에는 `어떤 운영 때문에 이 계보가 열렸는지`가 기록된다.

따라서 플레이 감각은 `방을 업그레이드했다`가 아니라 다음에 가깝다.

```text
이 시설이 이 방에서 이렇게 굴러왔기 때문에
다음 성급의 다른 업태로 합성 진화했다.
```

## 기본 구조

```text
시설
-> 현재 계보와 성급을 가짐
-> 방 안에서 운영됨
-> 이용 기록과 사건 기록을 쌓음
-> 조건이 맞으면 진화 후보가 열림
-> 플레이어가 재료를 지불하고 진화 실행
-> 같은 위치의 다음 계보 시설로 교체
```

방은 다음을 제공한다.

```text
RoomProfile
- 닫힌 방 여부
- 문 여부
- 면적/동선
- 기물 태그
- 환경 점수
- 밀도/비율 지표
- 좌석/장식/서비스 품질
- 인접 시설/방
- 고객 이용 기록
- 사건 기록
- 평판/소문
```

시설은 다음을 제공한다.

```text
FacilityLineageState
- currentFacilityId
- lineageTags
- starGrade
- discoveredEvolutionIds
- mutationTags
- historySummary
```

진화식은 다음을 본다.

```text
EvolutionRecipe
- 현재 시설 ID 또는 계보 태그
- 필요 성급
- 필요 방 조건
- 필요 기물/환경 태그
- 필요 기록 토큰
- 필요 재료
- 필요 연구/설계도
- 결과 시설 ID
```

## 처리 플로우

### 1. 트리거

진화 후보 확인은 다음 상황에서 일어난다.

- 플레이어가 시설을 선택하고 `진화 확인` 버튼을 누름
- 운영일 정산 시 새 후보가 생겼는지 갱신
- 특정 기록 토큰이 생성됨
- 연구/설계도가 완료됨
- 시설 성급이나 계보 조건이 바뀜

### 2. 컨텍스트 수집

게임 엔진이 현재 시설과 방 정보를 압축한다.

예:

```json
{
  "facilityId": "meat_restaurant",
  "facilityName": "고기 식당",
  "starGrade": 1,
  "lineageTags": ["Meal", "Meat"],
  "roomProfile": {
    "isClosed": true,
    "hasDoor": true,
    "area": 6,
    "tags": ["Dining", "Meat", "TrainingAdjacent", "WeaponDecor"],
    "scores": {
      "Dining": 52,
      "Training": 18,
      "Combat": 24,
      "Luxury": 4
    }
  },
  "recordTokens": {
    "MercenaryHangout": 2,
    "HighMeatConsumption": 3,
    "FrequentBrawls": 1
  },
  "recentEvents": [
    "용병 손님이 고기 식당을 반복 이용함",
    "식사 후 훈련 더미를 이용함",
    "손님 사이 소란이 발생함"
  ],
  "candidateEvolutionIds": [
    "evolve_meat_restaurant_to_combat_tavern",
    "evolve_meat_restaurant_to_noble_meat_hall",
    "evolve_meat_restaurant_to_outlaw_pub"
  ]
}
```

원본 로그 전체를 LLM에 보내지 않는다.

방과 시설의 현재 정체성을 판단할 수 있을 만큼만 압축한다.

### 3. 후보 풀 생성

게임 엔진이 먼저 후보를 만든다.

후보 풀 생성 기준:

- 현재 시설에서 갈 수 있는 계보인가
- 현재 성급에서 다음 단계인가
- 연구/설계도 조건상 후보로 볼 수 있는가
- 현재 방 조건상 완전히 불가능하지 않은가

중요:

LLM이 후보를 무한히 만들어내는 게 아니다.

LLM은 게임 엔진이 제공한 후보 목록 안에서 해석하고 추천한다.

### 4. LLM 해석

LLM은 다음을 생성한다.

- 현재 시설 정체성 요약
- 후보별 어울리는 이유
- 조건 미달 후보에 대한 흐릿한 영감
- 진화 실행 시 보여줄 flavor text
- 변이 수식어 후보

예:

```json
{
  "facilityIdentitySummary": "용병들이 자주 모이는 거친 고기 식당",
  "proposalIds": [
    "evolve_meat_restaurant_to_combat_tavern",
    "evolve_meat_restaurant_to_outlaw_pub"
  ],
  "proposalReasons": {
    "evolve_meat_restaurant_to_combat_tavern": "용병 이용 기록과 전투 기물 태그가 전투 식당 계보와 잘 맞습니다.",
    "evolve_meat_restaurant_to_outlaw_pub": "소란 기록은 있지만 악명 조건이 아직 부족합니다."
  },
  "mutationTagSuggestions": [
    "Brutal"
  ],
  "flavorText": "고기 냄새와 무기 소리가 뒤섞이며, 이 식당은 싸움을 앞둔 자들의 식탁이 되어가고 있습니다.",
  "confidence": 0.84
}
```

### 5. 게임 검증

게임 코드는 LLM 제안을 다시 검증한다.

검증 순서:

```text
1. 후보 ID가 실제 데이터에 존재하는가
2. 현재 시설에서 해당 후보로 갈 수 있는가
3. 성급 조건이 맞는가
4. 방 조건이 맞는가
5. 기물/환경 태그 조건이 맞는가
6. 기록 토큰 조건이 맞는가
7. 재료/돈/마나 조건이 맞는가
8. 연구/설계도 조건이 맞는가
9. 결과 시설 배치가 가능한가
10. 밸런스 제한을 넘지 않는가
```

통과한 후보만 UI에 노출한다.

조건 미달 후보는 `흐릿한 영감`으로 표시할 수 있다.

### 6. 플레이어 선택

플레이어는 승인된 후보 중 하나를 선택한다.

선택 화면에는 다음을 보여준다.

- 결과 시설 이름
- 결과 성급
- 계보 변화
- 필요한 재료
- 필요한 기록
- 필요한 방 조건
- LLM이 해석한 이유
- 진화 후 기능 변화

### 7. 진화 실행

진화 실행 결과:

- 현재 시설이 결과 시설로 교체된다.
- 위치는 기본적으로 유지한다.
- 결과 시설의 크기가 다르면 배치 가능 여부를 먼저 검사한다.
- 필요한 재료를 소모한다.
- 소모형 기록 토큰을 차감한다.
- 시설 계보 기록을 갱신한다.
- 도감 발견 정보를 갱신한다.
- LLM flavor text를 시설 히스토리에 저장한다.

## 진화 대상

진화 대상은 `현재 시설`이다.

예:

```text
고기 식당
-> 전투 식당
-> 전장의 식당
-> 군단 연회장
```

이때 다른 시설은 직접 합성 재료로 소모하지 않는다.

다른 시설은 다음 형태로 영향을 준다.

- 같은 방의 기물 태그
- 인접 시설 태그
- 같은 층/구역의 운영 기록
- 직원/고객 이동 기록
- 연구/설계도 조건
- 사건 기록

예:

```text
훈련장이 옆에 있음
-> TrainingAdjacent 태그 제공

훈련 더미가 같은 방에 있음
-> Training 점수 제공

용병이 식당과 훈련장을 연속 이용함
-> MercenaryHangout / TrainingUse 토큰 제공
```

따라서 플레이어 감각은 이렇게 된다.

```text
훈련장을 재료로 먹인 게 아니라,
식당이 훈련장과 함께 운영되면서 전투 식당으로 진화했다.
```

## 방의 역할

방은 진화 대상이 아니다.

방의 역할:

- 시설이 작동할 수 있는 물리 조건
- 시설의 업태를 뒷받침하는 환경
- 운영 기록이 쌓이는 공간
- LLM이 해석할 맥락
- 진화 후보를 제한하거나 강화하는 조건

예:

```text
전투 식당 후보 조건:
- 현재 시설: 고기 식당
- 방: 닫힌 방, 문 있음
- 방 점수: Dining >= 40
- 전투 환경: Combat >= 20 또는 Training >= 15
- 기록: MercenaryHangout >= 1
- 재료: 고급 고기 10, 마나석 3
```

방이 전투 식당으로 변하는 게 아니다.

고기 식당 시설이 전투 식당 시설로 진화하고, 그 시설이 들어 있는 방이 전투 식당 운영 조건을 만족하는 것이다.

## 컨텍스트 기반 진화

시설 계보 진화는 단순히 `필수 가구가 있는가`만 보지 않는다.

가능한 한 많은 컨텍스트를 반영하되, 원본 데이터를 그대로 진화식에 넣지 않고 의미 있는 지표로 압축한다.

목표:

```text
플레이어가 같은 식당을 만들더라도
좌석을 빽빽하게 넣었는지,
테이블을 적게 두고 장식과 동선을 넓게 잡았는지,
어떤 손님이 주로 이용했는지,
어떤 사건이 반복됐는지에 따라
다른 계보 후보가 자연스럽게 열리게 한다.
```

### 통합 판정 방식

진화 판단은 단일 조건으로 끝내지 않는다.

예를 들어 `테이블과 의자가 많다`는 사실만으로 일반 식당이 되면 안 된다.

방 크기가 큰데 테이블이 조금 많은 것인지, 작은 방에 좌석을 빽빽하게 넣은 것인지, 손님 회전이 실제로 빠른지, 평균 지불액이 낮은지까지 같이 봐야 한다.

반대로 `테이블과 의자가 적다`는 사실만으로 고급 식당이 되면 안 된다.

좌석 간격이 넓고, 좌석당 장식/서비스 점수가 높고, 고소비 손님이 오래 머물며, 불만과 소란이 낮을 때 비로소 고급화 방향으로 읽는다.

따라서 진화식은 다음처럼 구성한다.

```text
하드 조건
- 현재 시설 계보
- 필요 성급
- 닫힌 방/문/도달 가능성
- 연구/설계도/재료

맥락 조건
- 방 크기 대비 좌석/테이블 밀도
- 좌석당 장식/서비스/조리 점수
- 고객층, 평균 지불액, 회전율, 체류 시간
- 사건, 평판, 소문, 직원 상태

LLM 해석
- 현재 시설 정체성 요약
- 후보 추천 이유
- 조건 미달 후보의 흐릿한 영감
- 진화 성공 서사
```

게임 코드는 하드 조건과 맥락 조건을 수치로 검증한다.

LLM은 이 수치를 바탕으로 `왜 이 시설이 그 계보로 가려 하는지`를 설명하고, 후보 우선순위와 서사를 돕는다.

중요한 기준:

```text
절대 수량보다 비율을 우선한다.
단일 지표보다 조합된 정체성을 우선한다.
LLM 문장보다 게임 데이터 검증을 우선한다.
```

### 컨텍스트 반영 원칙

이 시스템에서 `컨텍스트를 전부 반영한다`는 뜻은 모든 원본 데이터를 조건식에 직접 나열한다는 뜻이 아니다.

원본 데이터는 너무 많고, 그대로 쓰면 다음 문제가 생긴다.

- 테이블 4개 같은 절대 수량이 방 크기에 따라 전혀 다른 의미가 된다.
- 같은 고급 장식이라도 좌석이 2개인 방과 좌석이 20개인 방에서 의미가 달라진다.
- 손님이 많다는 사실만으로 좋은 운영인지, 혼잡한 운영인지 구분하기 어렵다.
- 소란이 한 번 있었다는 기록과 반복되는 소란 문화는 다른 의미인데, 단순 이벤트 수로는 구분이 약하다.

따라서 모든 컨텍스트는 다음 4단계를 거쳐 진화 판단에 들어간다.

```text
Raw Context
-> Normalized Metric
-> Identity Pressure
-> Candidate-Specific Score
```

#### 1. Raw Context

게임에서 실제로 일어난 원본 정보다.

예:

```text
테이블 수
의자 수
방 면적
좌석 간 거리
문 위치
장식 점수
서비스 점수
방문한 손님 유형
평균 지불액
체류 시간
소란/절도/칭찬/불만
직원 스트레스
재고 부족
인접 방
구역 평판
```

#### 2. Normalized Metric

원본 정보를 방 크기, 좌석 수, 시간, 방문 횟수에 맞춰 정규화한다.

예:

```text
seatDensity = seatCount / roomArea
luxuryPerSeat = luxuryScore / seatCount
servicePerSeat = serviceScore / seatCount
turnoverRate = completedVisits / timeWindow
crowdStress = highSeatDensity + waitTime + complaintRate
premiumSignal = luxuryPerSeat + averageSpend + nobleVisitorRatio
combatCulture = trainingScore + mercenaryRatio + brawlMemory
```

#### 3. Identity Pressure

정규화된 지표를 방의 성격 압력으로 환산한다.

예:

```text
Crowd
- seatDensity
- turnoverRate
- queueSpace
- averageSpend 낮음
- highTurnoverService 기록

Luxury
- lowSeatDensity
- luxuryPerSeat
- servicePerSeat
- averageSpend
- nobleVisitorRatio

Combat
- trainingScore
- combatFixtureScore
- mercenaryVisitorRatio
- highMeatConsumption
- brawlMemory

Outlaw
- criminalVisitorRatio
- theftCount
- negativeRumor
- lowSecurityCoverage
```

#### 4. Candidate-Specific Score

각 진화 후보는 자신에게 필요한 정체성 압력만 본다.

예:

```text
대중 식당 후보
+ Crowd
+ turnoverRate
- Luxury가 지나치게 높음

고급 식당 후보
+ Luxury
+ Service
- Crowd
- brawlMemory

전투 식당 후보
+ Combat
+ MercenaryHangout
+ HighMeatConsumption
- Luxury가 높지만 전투 기록이 없음

무법자 주점 후보
+ Outlaw
+ longStayDuration
+ negativeRumor
- Security
```

이렇게 하면 같은 `테이블과 의자`라도 맥락에 따라 다르게 읽힌다.

```text
테이블 많음 + 작은 방 + 높은 회전율 + 낮은 객단가
-> Crowd 압력
-> 대중 식당/군중 식당 후보

테이블 많음 + 큰 방 + 넓은 좌석 간격 + 높은 장식/서비스 + 높은 객단가
-> Luxury와 Crowd가 함께 있음
-> 대형 고급 연회장, 귀족 만찬장 후보

테이블 적음 + 넓은 간격 + 높은 장식/서비스 + 귀족 방문
-> Luxury 압력
-> 고급 식당/귀족 식당 후보

테이블 적음 + 낮은 장식/서비스 + 낮은 방문 수
-> Luxury가 아니라 비활성/미성숙 상태
-> 고급화 후보 비노출 또는 흐릿한 영감

큰 테이블 + 고기 소비 + 용병 손님 + 소란 기록
-> Combat/Crowd 압력
-> 전투 식당/용병 연회장 후보
```

핵심은 `많다/적다`가 아니라 `왜 그렇게 배치됐고 실제로 어떻게 운영됐는가`다.

### 정체성 압력 기반 판정

진화 후보는 단일 조건을 통과했다고 바로 열리지 않는다.

먼저 방과 시설의 현재 상태를 여러 방향의 `정체성 압력`으로 환산한다.

예:

```text
Crowd     = 많은 좌석, 높은 회전율, 낮은 대기 시간, 낮은 객단가
Luxury    = 낮은 좌석 밀도, 높은 좌석당 장식/서비스, 높은 객단가, 귀족 손님
Combat    = 전투/훈련 태그, 용병 손님, 소란, 침입 방어 직후 이용
Outlaw    = 범죄 성향 손님, 절도/소란, 나쁜 소문, 낮은 통제력
Rest      = 긴 체류 시간, 회복/위생/휴식 점수, 높은 만족도
Service   = 직원 숙련도, 청결, 빠른 보충, 낮은 실패율
Ritual    = 마력/제단/묘지/종교 태그, 특정 종족 반복 이용
Security  = 경비 인접, 침입 대응 기록, 병목, 높은 구역 안전도
Logistics = 창고 접근성, 재고 안정성, 낮은 보충 지연
```

흐름은 다음과 같다.

```text
Raw Layout / Raw Event
-> Metric
-> Token / Tag / Score
-> IdentityPressure
-> CandidateEvolutionScore
-> LLM reason
-> Engine validation
```

중요한 점은 `많은 테이블 = 대중 식당` 같은 직접 연결을 피하는 것이다.

테이블이 많아도 방이 넓고, 좌석 간격이 충분하고, 장식/서비스가 높고, 귀족 손님이 오래 머문다면 대중 식당이 아니라 대형 고급 연회장 방향일 수 있다.

반대로 테이블이 적어도 장식과 서비스가 낮고, 평균 지불액도 낮고, 손님이 거의 없으면 고급 식당이 아니라 그냥 비어 있는 작은 식당이다.

따라서 각 진화식은 다음 두 층을 가진다.

```text
게이트 조건
- 현재 시설 계보
- 필요 성급
- 닫힌 방/문/도달 가능성
- 연구/설계도/재료
- 절대적으로 필요한 기록 토큰

정체성 점수
- 후보 계보와 맞는 IdentityPressure 합산
- 상충 신호에 따른 감점
- 최근 기록과 장기 기록의 가중치
- LLM 추천 순서 보정
```

예:

```text
전투 식당 후보
게이트:
- currentFacilityId = meat_restaurant
- Dining >= 40
- Combat >= 15 or Training >= 15
- MercenaryHangout >= 1

정체성 점수:
+ Combat pressure
+ HighMeatConsumption
+ FrequentBrawls
+ largeTableRatio
+ combatVisitorRatio
- Luxury pressure가 지나치게 높고 brawlCount가 0이면 전투성 감점
```

```text
고급 식당 후보
게이트:
- currentFacilityId = meat_restaurant
- Dining >= 40
- Luxury >= 20
- 도달 가능한 닫힌 방

정체성 점수:
+ Luxury pressure
+ lowSeatDensity
+ highLuxuryPerSeat
+ highServiceScorePerSeat
+ averageSpend
+ nobleVisitorRatio
- brawlCount
- stockoutCount
- highCrowdPressure
```

후보가 여러 개 동시에 높게 나오면 UI에 모두 보여준다.

플레이어가 선택하면서 시설의 계보가 갈라지는 구조가 된다.

### 충돌과 모순 처리

컨텍스트가 많아질수록 서로 충돌하는 신호가 생긴다.

이때 시스템은 한쪽을 즉시 버리지 않고, 다음 규칙으로 처리한다.

```text
하드 조건 실패
-> 후보 비노출 또는 흐릿한 영감

정체성 점수 부족
-> 후보 비노출

정체성 점수는 높지만 일부 신호가 충돌
-> 후보 노출, LLM 이유에서 불안정한 정체성 언급 가능

둘 이상의 정체성 점수가 비슷하게 높음
-> 여러 후보 노출

최근 기록과 장기 기록이 충돌
-> 최근 기록은 추천 순서에 강하게, 장기 기록은 해금/계보 안정성에 강하게 반영
```

예:

```text
좌석 밀도 높음 + 귀족 손님 많음 + 평균 지불액 높음
-> 대중 식당과 고급 식당이 모두 후보가 될 수 있음
-> LLM은 "붐비는 귀족 식당" 같은 중간 정체성을 설명
-> 게임은 기획된 후보 안에서 귀족 연회장, 대형 고급 식당 같은 후보를 우선 노출
```

```text
훈련 더미 있음 + 용병 손님 없음 + 고기 소비 낮음
-> 전투 식당 게이트는 열릴 수 있어도 정체성 점수가 낮음
-> UI에는 "전투의 낌새가 있지만 아직 이 시설의 역사로 굳지는 않았습니다" 같은 흐릿한 영감만 표시 가능
```

```text
테이블 적음 + 좌석 간격 넓음 + 장식 낮음 + 지불액 낮음
-> 고급 식당이 아님
-> 작은 식당, 비활성 식당, 개선 필요 상태로 본다
```

### 식당 판정 예시

같은 `일반 식당` 출발이라도 방과 운영 맥락에 따라 후보가 달라진다.

```text
대중 식당 / 군중 식당
- seatDensity 높음
- tableDensity 높음
- turnoverRate 높음
- averageSpend 낮음 또는 중간
- averageWaitTime 낮음
- Crowd 태그 또는 HighTurnoverService 토큰
-> 많은 손님을 빠르게 받는 계보

고급 식당 / 귀족 식당
- seatDensity 낮음
- averageSeatSpacing 높음
- luxuryPerSeat 높음
- serviceScorePerSeat 높음
- averageSpend 높음
- nobleVisitorRatio 높음
- brawlCount 낮음
-> 적은 손님에게 비싸고 조용한 경험을 제공하는 계보

전투 식당 / 용병 식당
- Dining 점수 충분
- Combat 또는 Training 점수 높음
- combatVisitorRatio 높음
- MercenaryHangout 기록 있음
- HighMeatConsumption 또는 FrequentBrawls 기록 있음
- largeTableRatio 높으면 연회장 방향 강화
-> 전투형 손님이 모여 먹고 떠드는 계보

무법자 주점 / 암시장 식당
- counterRatio 높음
- averageStayDuration 길음
- criminalVisitorRatio 높음
- theftCount 또는 crimeCount 있음
- OutlawRumor 또는 negativeMentionCount 높음
-> 불법 거래와 소문이 엮이는 계보

휴식형 식당 / 라운지 식당
- seatDensity 낮음 또는 중간
- Rest/Hygiene/Luxury 점수 높음
- averageStayDuration 길음
- averageSatisfaction 높음
- workerStressAverage 낮음
-> 식사보다 체류와 회복 경험이 강한 계보
```

이 구조의 장점은 플레이어가 같은 재료와 같은 시작 시설을 써도 배치와 운영으로 다른 결과를 만든다는 점이다.

```text
테이블과 의자를 많이 넣고 빠르게 돌린 식당
-> 대중 식당 쪽으로 성장

테이블과 의자를 적게 넣고 비싼 장식과 좋은 서비스를 붙인 식당
-> 고급 식당 쪽으로 성장

큰 테이블과 고기 소비, 용병 손님, 소란이 쌓인 식당
-> 전투 식당 쪽으로 성장
```

### 컨텍스트 계층

진화 판단은 다음 계층의 컨텍스트를 함께 본다.

```text
1. 공간 컨텍스트
2. 기물 컨텍스트
3. 배치/밀도 컨텍스트
4. 운영 컨텍스트
5. 고객 컨텍스트
6. 직원 컨텍스트
7. 경제 컨텍스트
8. 사건/위험 컨텍스트
9. 인접/구역 컨텍스트
10. 평판/소문 컨텍스트
```

### 공간 컨텍스트

방 자체의 형태와 접근성을 본다.

지표 예:

```text
roomArea
isClosed
hasDoor
doorCount
walkableCellCount
usableInteriorRatio
mainFacilityReachability
averagePathDistanceFromEntrance
queueSpace
chokePointScore
privacyScore
```

예:

```text
작고 닫힌 방 + 문 1개 + 좌석 적음
-> 개인실, 고급 식당, 비밀 상점 계열에 유리

넓고 접근성 좋음 + 대기 공간 큼
-> 대중 식당, 시장, 연회장 계열에 유리

병목이 강함 + 방어 시설 근접
-> 전술 방어, 검문소, 포획 구역 계열에 유리
```

### 기물 컨텍스트

방 안에 어떤 기물이 있는지 본다.

하지만 기물 이름을 모두 진화식에 직접 나열하지 않는다.

기물은 태그와 점수를 제공한다.

```text
Dining
Cooking
Meat
Luxury
Service
Training
Combat
Defense
Storage
Hygiene
Rest
Research
Mana
Brutal
Sacred
Outlaw
Noble
Fear
Crowd
Quiet
Security
```

예:

```text
식탁과 의자: Dining, Seat
고기 조리대: Cooking, Meat
훈련 더미: Training
무기 거치대: Combat
고급 그림: Luxury
피 묻은 장식: Brutal, Fear
```

### 배치/밀도 컨텍스트

이 부분이 중요하다.

같은 기물이 있어도 `얼마나 많이`, `얼마나 빽빽하게`, `어떤 비율로`, `얼마나 여유 있게` 배치됐는지가 업태를 바꾼다.

식당 예:

```text
테이블과 의자가 많음
+ 좌석 밀도 높음
+ 회전율 높음
-> 일반 식당, 대중 식당, 군중 식당 계열

테이블과 의자가 적음
+ 좌석 간격 넓음
+ 장식 점수/좌석 수 높음
+ 평균 지불액 높음
-> 고급 식당, 귀족 식당, 만찬실 계열

큰 테이블 비율 높음
+ 단체 손님 기록
+ 고기 소비량 높음
-> 연회장, 부족 회식장, 용병 연회장 계열

테이블보다 카운터/술통 비중 높음
+ 체류 시간 길음
+ 소란 기록 있음
-> 주점, 무법자 주점 계열
```

주요 지표:

```text
seatCount
tableCount
seatPerTable
seatDensity = seatCount / roomArea
tableDensity = tableCount / roomArea
averageSeatSpacing
luxuryPerSeat = LuxuryScore / max(1, seatCount)
serviceScorePerSeat = ServiceScore / max(1, seatCount)
cookingScorePerSeat = CookingScore / max(1, seatCount)
counterRatio
largeTableRatio
privateSeatRatio
decorToUtilityRatio
clutterScore
```

주의:

- `테이블 3개 이상` 같은 절대 조건만 쓰면 방 크기에 따라 어색해진다.
- `좌석 밀도`, `좌석당 장식`, `면적당 서비스 점수` 같은 비율 지표가 더 자연스럽다.
- 절대 수량은 P1에서 단순한 힌트로만 쓰고, 핵심 판정은 비율과 점수를 같이 봐야 한다.

### 같은 식당의 세부 분기 예

`일반 식당`, `대중 식당`, `고급 식당`, `전투 식당`은 서로 다른 상위 방이 아니다.

같은 식당 시설이 어떤 방에서 어떤 기록을 쌓았는지에 따라 다음 계보가 다르게 열리는 것이다.

```text
기본 식당형
- Dining 점수 충분
- 좌석/테이블이 역할에 맞게 배치됨
- 방문과 식사 완료 기록이 안정적으로 있음
- 강한 Crowd/Luxury/Combat/Outlaw 압력이 아직 없음
-> 일반 식당 계보 유지 또는 2성 기본 식당 계보

대중 식당형
- 좌석 수와 테이블 수가 많음
- seatDensity 높음
- turnoverRate 높음
- averageSpend 낮거나 중간
- waitTime/complaintRate가 너무 높지는 않음
-> 대중 식당, 군중 급식소, 전시 보급 식당 계보

고급 식당형
- 좌석 수는 적거나 중간
- averageSeatSpacing 높음
- luxuryPerSeat/serviceScorePerSeat 높음
- averageSpend 높음
- nobleVisitorRatio 또는 regularCustomerRatio 높음
- brawlMemory/negativeRumor 낮음
-> 고급 식당, 귀족 식당, 왕실 연회장 계보

전투 식당형
- Dining 기반은 유지됨
- Training/Combat 점수 또는 인접 훈련 요소가 있음
- mercenaryVisitorRatio 높음
- HighMeatConsumption, MercenaryHangout, FrequentBrawls 기록이 쌓임
- Rest/Luxury 압력보다 Combat 압력이 강함
-> 전투 식당, 용병 연회장, 전장의 식당 계보

무법 주점형
- 체류 시간이 길고 계산/서비스 실패가 반복됨
- criminalVisitorRatio 높음
- theftCount, negativeRumor, OutlawRumor가 쌓임
- Security 압력이 낮거나 경비 대응 실패가 있음
-> 무법자 주점, 암시장 식당, 은닉 거래소 계보
```

여기서 중요한 점은 `테이블과 의자가 많으면 무조건 대중 식당`, `적으면 무조건 고급 식당`이 아니라는 것이다.

예를 들어 테이블이 적어도 장식/서비스/고소비 기록이 없으면 고급 식당이 아니라 미성숙한 식당으로 본다.

반대로 테이블이 많아도 방이 충분히 크고 서비스 점수와 객단가가 높으면 대중 식당이 아니라 대형 고급 연회장 쪽으로 읽을 수 있다.

즉 진화 후보는 다음처럼 판단한다.

```text
시설의 현재 계보
+ 공간/배치 비율
+ 기물 태그와 점수
+ 실제 이용 기록
+ 손님층
+ 직원 운영 품질
+ 사건/소문/평판
-> 정체성 압력
-> 후보별 점수
-> 게임 검증
-> LLM 해석/서사
```

### 운영 컨텍스트

시설이 실제로 어떻게 운영됐는지를 본다.

지표 예:

```text
visitCount
uniqueVisitorCount
repeatVisitorRatio
averageSatisfaction
averageWaitTime
turnoverRate
averageStayDuration
failedVisitCount
noPathFailureCount
stockoutCount
maintenanceDowntime
```

예:

```text
방이 빽빽하고 회전율이 높음
-> 대중 식당 계열

체류 시간이 길고 만족도가 높음
-> 휴식형 식당, 라운지, 고급 식당 계열

재고 부족이 잦고 불만이 높음
-> 무질서/저가/암시장 계열 변이 가능
```

### 고객 컨텍스트

어떤 손님이 시설을 사용했는지 본다.

지표 예:

```text
speciesRatio
archetypeRatio
wealthTierRatio
combatVisitorRatio
nobleVisitorRatio
criminalVisitorRatio
regularCustomerRatio
averageSpendByArchetype
favoriteSpecies
avoidedSpecies
```

예:

```text
용병/전투형 손님 비율 높음
-> 전투 식당, 용병 연회장

귀족/고소비 손님 비율 높음
-> 고급 식당, 귀족 식당

슬라임 비율 높음 + 습기 환경
-> 점액 식당, 습지 휴식실

범죄 성향 손님 비율 높음
-> 무법자 주점, 암시장
```

### 직원 컨텍스트

시설을 누가 관리했는지도 반영한다.

지표 예:

```text
staffedRatio
primaryWorkerSkill
workerSpecies
workerMoodAverage
workerStressAverage
serviceQuality
repairResponseTime
cleaningFrequency
trainingStaffPresence
guardStaffPresence
```

예:

```text
숙련 직원이 안정적으로 관리
-> 고급/전문 업태에 유리

전투형 직원이 자주 대기
-> 전투 식당, 병영, 경비 거점에 유리

직원 스트레스 높음 + 소란 잦음
-> 무법/피로/반란 계열 변이 위험
```

### 경제 컨텍스트

시설이 어떤 경제적 성격을 띠는지 본다.

지표 예:

```text
totalRevenue
averageSpend
profitPerVisit
stockCostPerVisit
premiumItemRatio
discountUseRatio
wasteRatio
highValueTransactionCount
```

예:

```text
낮은 단가 + 높은 회전율
-> 대중 식당, 군중 상점

높은 단가 + 낮은 회전율 + 높은 만족도
-> 고급 식당, 귀족 상점

비싼 물건 거래 + 절도 위험
-> 암시장, 보물 상점
```

### 사건/위험 컨텍스트

좋은 기록만 진화 재료가 아니다.

나쁜 사건도 특정 계보의 촉매가 될 수 있다.

지표 예:

```text
brawlCount
crimeCount
theftCount
vandalismCount
staffConflictCount
intruderDamageDealt
intruderDelayTime
facilityDamageTaken
repairCount
deathOrSevereIncidentCount
```

예:

```text
소란 많음 + 용병 많음
-> 거친 전투 식당, 무법자 주점

침입자 피해 기여 높음
-> 전장의 식당, 피의 식당, 방어형 하이브리드

절도 많음 + 고가 거래 많음
-> 암시장 계열
```

### 인접/구역 컨텍스트

시설은 혼자 존재하지 않는다.

주변 시설과 구역의 성격도 본다.

지표 예:

```text
adjacentFacilityTags
sameZoneDominantTags
distanceToEntrance
distanceToStorage
distanceToTraining
distanceToGuardPost
distanceToLuxuryRoom
trafficThroughRoom
zoneSafetyScore
```

예:

```text
식당이 훈련장과 가깝고 용병이 둘을 연속 이용
-> 전투 식당 계열

상점이 경비실과 가깝고 절도 기록이 있음
-> 보안 상점, 암시장 감시소

휴식방이 위생 시설과 가깝고 청결 유지가 좋음
-> 고급 휴식실, 회복실
```

### 평판/소문 컨텍스트

LLM을 쓰기 좋은 영역이다.

평판과 소문은 수치와 서사를 연결한다.

지표 예:

```text
reputationTags
rumorTags
recentRumorSummary
positiveMentionCount
negativeMentionCount
speciesSpecificReputation
factionSpecificReputation
```

예:

```text
"용병들이 좋아하는 밥집" 소문
-> 전투 식당 후보 추천 강화

"비싸지만 조용하다" 평판
-> 고급 식당 후보 추천 강화

"훔친 물건이 돈다" 소문
-> 암시장/무법 계열 흐릿한 영감
```

## 컨텍스트 압축 규칙

컨텍스트는 많을수록 좋지만, 진화식과 LLM 입력이 원본 데이터에 파묻히면 안 된다.

따라서 원본 데이터를 세 단계로 압축한다.

```text
Raw Event
-> Metric
-> Token / Score / Tag
```

예:

```text
Raw Event:
용병 손님 30명이 식당 이용

Metric:
combatVisitorRatio = 0.72
repeatVisitorRatio = 0.38

Token:
MercenaryHangout +2
```

예:

```text
Raw Layout:
방 면적 12, 의자 10, 테이블 4, 장식 점수 6

Metric:
seatDensity = 0.83
luxuryPerSeat = 0.6

Tag:
CrowdedDining
```

### 엔진이 보는 것

게임 엔진은 수치 검증을 위해 Metric과 Token을 본다.

```text
seatDensity >= 0.7
MercenaryHangout >= 1
DiningScore >= 40
```

### LLM이 보는 것

LLM은 압축된 맥락과 대표 이벤트만 본다.

```text
이 방은 좌석 밀도가 높고 회전율이 빠릅니다.
용병 손님 비율이 높고 소란이 몇 번 발생했습니다.
최근 소문은 "싸움 전 밥 먹는 곳"입니다.
```

LLM에는 100개 이벤트를 직접 보내지 않는다.

### UI가 보여주는 것

UI는 플레이어가 이해할 수 있는 아이콘/문장으로 보여준다.

```text
빽빽한 좌석
용병 단골화
고기 소비 많음
소란 잦음
```

수치 전체는 디버그 UI에서만 보여준다.

## 기물과 환경

방 안의 모든 구성물을 진화식에 직접 나열하지 않는다.

기물은 태그와 점수로 압축된다.

예:

```text
식탁: Dining +10
고기 조리대: Dining +20, Meat +20
훈련 더미: Training +25
무기 거치대: Combat +20
고급 샹들리에: Luxury +20
피 묻은 장식: Brutal +15
```

진화식은 이렇게 쓴다.

나쁜 방식:

```text
식탁 A + 조리대 B + 훈련 더미 C + 무기 거치대 D
= 전투 식당
```

좋은 방식:

```text
현재 시설: 고기 식당
방 조건: Dining >= 40
환경 조건: Training >= 15 or Combat >= 20
기록 조건: MercenaryHangout >= 1
결과: 전투 식당
```

유니크 기물은 예외적으로 직접 요구할 수 있다.

예:

```text
마왕의 식탁
피의 왕관
고대 화로
영웅의 부서진 검
```

일반 진화식은 태그/점수 기반으로 관리하고, 특수 진화식만 유니크 기물을 직접 요구한다.

## 운영 기록

운영 기록은 이 시스템의 핵심이다.

시설이 왜 다음 계보로 갈 수 있는지 증명하는 데이터다.

기록 예시:

- 고객 이용 횟수
- 고객 종족 비율
- 고객 직업/성향 비율
- 평균 만족도
- 대기 시간
- 매출
- 재고 소비량
- 직원 근무 기록
- 파손/수리 기록
- 소란/범죄 사건
- 침입 방어 기여
- 인접 시설과의 연속 이용 기록

원본 기록은 `기록 토큰`으로 압축한다.

예:

```text
MercenaryHangout
- 용병/전투형 손님 이용 비율이 높음

HighMeatConsumption
- 고기 재고 소비량이 높음

FrequentBrawls
- 소란/싸움 사건이 반복됨

NoblePatronage
- 고소비/귀족형 손님 방문이 많음

GuardRallyPoint
- 경비 인력이 자주 모이는 장소

IntruderBloodied
- 침입자 피해 기여가 높음
```

기록 토큰은 세 가지 용도로 쓴다.

```text
1. 진화 조건
2. 후보 정렬/추천
3. 변이 수식어
```

## LLM의 지속 개입

LLM은 일반 식당이 전투 식당으로 갈 때만 쓰고 끝나는 장치가 아니다.

각 계보 단계마다 현재 시설의 기록을 다시 해석한다.

예:

```text
1성 고기 식당:
용병 손님이 늘고 훈련 요소가 가까움
-> 전투 식당 후보 추천

2성 전투 식당:
침입 방어 직후 경비대와 용병이 모이는 장소가 됨
-> 전장의 식당 후보 추천

3성 전장의 식당:
군단급 손님층과 대규모 방어 성공 기록이 쌓임
-> 군단 연회장 후보 추천
```

LLM은 매 단계에서 다음을 바꿀 수 있다.

- 후보 추천 순서
- 후보 이유
- 흐릿한 영감
- flavor text
- 변이 수식어 제안
- 도감 기록 문장

하지만 최종 판정은 바꾸지 못한다.

## 데이터 모델

### FacilityEvolutionState

시설 인스턴스가 가진 계보 상태.

```text
facilityInstanceId
baseFacilityId
currentFacilityId
starGrade
lineageTags
mutationTags
evolutionHistory
lastIdentitySummary
lastIdentityPressures
dominantIdentityTags
recordTokenOverrides
```

### FacilityEvolutionRecipe

진화식 데이터.

```text
id
displayName
fromFacilityIds
fromLineageTags
requiredStarGrade
toFacilityId
toStarGrade
requiredRoomConditions
requiredRoomScores
requiredRoomMetrics
requiredEnvironmentTags
requiredRecordTokens
requiredMaterials
requiredResearchIds
requiredUniqueFixtures
identityPressureWeights
minimumIdentityScore
conflictPenaltyRules
allowedMutationTags
visibilityRule
consumePolicy
```

예:

```text
id: evolve_meat_restaurant_to_combat_tavern
displayName: 전투 식당 진화
fromFacilityIds:
  - meat_restaurant
requiredStarGrade: 1
toFacilityId: combat_tavern
toStarGrade: 2
requiredRoomScores:
  Dining: 40
requiredRoomMetrics:
  seatDensity:
    min: 0.45
    max: 0.85
  combatVisitorRatio:
    min: 0.35
requiredEnvironmentTags:
  - Combat: 20
requiredRecordTokens:
  MercenaryHangout: 1
identityPressureWeights:
  Combat: 0.45
  Crowd: 0.15
  Service: 0.10
  Luxury: -0.15
minimumIdentityScore: 0.55
requiredMaterials:
  high_grade_meat: 10
  mana_stone: 3
allowedMutationTags:
  - Brutal
  - Tactical
  - Mercenary
```

### RoomProfile

방이 제공하는 조건.

```text
roomId
isClosed
hasDoor
area
roles
scores
metrics
tags
fixtures
adjacentFacilities
recentEvents
recordTokens
identityPressures
dominantSignals
conflictingSignals
```

### FacilityIdentitySnapshot

후보 산출과 LLM 입력 사이에서 공유되는 압축 스냅샷.

```text
facilityInstanceId
facilityName
currentFacilityId
starGrade
lineageTags
roomSummary
topMetrics
topRecordTokens
identityPressures
dominantSignals
conflictingSignals
recentEventSummary
longTermHistorySummary
candidateEvolutionIds
```

이 스냅샷은 원본 이벤트 로그가 아니다.

게임 엔진은 원본 이벤트와 배치를 수치로 계산하고, LLM에는 이 스냅샷만 전달한다.

### RecordTokenDefinition

기록 토큰 정의.

```text
id
displayName
description
sourceMetric
threshold
decayPolicy
consumePolicy
recipeTags
uiHint
```

기록 토큰은 모두 같은 방식으로 소비되지 않는다.

```text
Preserve
- 진화 조건으로만 쓰고 기록은 보존한다.
- MercenaryHangout, NoblePatronage처럼 시설의 역사/평판을 증명하는 토큰에 사용한다.

ConsumeRequiredAmount
- 요구량만큼 소모한다.
- 일회성 의식 재료, 특별 주문권, 소모성 계약 같은 토큰에 사용한다.

ConsumeAll
- 해당 토큰을 모두 소모한다.
- 불안정한 저주, 폭주 게이지, 정화 대상 낙인처럼 진화 후 사라져야 하는 기록에 사용한다.
```

레시피의 `consumePolicy`는 큰 방향이고, 실제 소모 방식은 `RecordTokenDefinition.consumePolicy`가 우선한다.

정의가 없는 토큰은 기존 호환성을 위해 `ConsumeRequiredAmount`로 처리한다.

### LlmEvolutionProposal

LLM 출력.

```text
facilityIdentitySummary
proposalIds
proposalReasons
mutationTagSuggestions
flavorText
rejectedHintText
confidence
```

## 예시 계보

### 식당 계보

```text
1성 고기 식당
-> 2성 전투 식당
   조건: Dining, Combat/Training, MercenaryHangout, 중간 이상 좌석 밀도, 고급 고기

-> 2성 고급 고기 식당
   조건: Dining, Luxury, NoblePatronage, 낮은 좌석 밀도, 높은 좌석당 장식 점수, 고급 장식

-> 2성 피의 식당
   조건: Dining, Brutal/Blood, FrequentBrawls 또는 IntruderBloodied
```

### 전투 식당 계보

```text
2성 전투 식당
-> 3성 전장의 식당
   조건: GuardRallyPoint, IntruderBloodied, Combat 점수

-> 3성 약탈자의 식당
   조건: RareLootDemand, MercenaryHangout, 전리품 재료

-> 3성 용병 연회장
   조건: MercenaryHangout 3, HighMeatConsumption 3, 큰 테이블 비율, 단체 손님 기록, 높은 매출
```

### 전장의 식당 계보

```text
3성 전장의 식당
-> 4성 군단 연회장
   조건: 대규모 침입 방어 성공, 경비대 이용 기록, 희귀 고기

-> 4성 피의 군량소
   조건: IntruderBloodied 5, FrequentBrawls 3, Blood 계열 연구
```

### 고급 식당 계보

```text
2성 고급 고기 식당
-> 3성 귀족의 식당
   조건: NoblePatronage, CleanServiceStreak, Luxury 점수, 낮은 좌석 밀도, 높은 평균 지불액

-> 3성 대식 연회장
   조건: HighMeatConsumption, 대형 식탁, 창고 연계
```

### 상점 계보

```text
1성 잡화점
-> 2성 무기 상점
   조건: 무기 재고 소비, 전투형 손님 구매 기록

-> 2성 암시장
   조건: TheftPressure, Notoriety, 특수 설계도

-> 2성 고급 상점
   조건: HighValueTrade, NoblePatronage
```

### 방어 시설 계보

```text
1성 가시 함정
-> 2성 맹독 가시 함정
   조건: 독 재료, 침입 피해 기록, 독 연구

-> 2성 피의 가시 함정
   조건: IntruderBloodied, FrequentRepair, Blood 태그
```

방어 시설도 시설이므로 같은 원칙을 따른다.

방어 시설끼리 직접 합성하기보다, 해당 방어 시설이 쌓은 발동 기록과 환경 조건으로 다음 계보에 진화한다.

## UI

### 시설 선택 패널

시설을 선택하면 다음을 보여준다.

- 현재 시설 이름
- 성급
- 계보 태그
- 위치한 방의 조건
- 주요 환경 태그
- 주요 기록 토큰
- 진화 후보
- 흐릿한 영감

예:

```text
고기 식당 / 1성
계보: 식당, 고기
방 조건: 닫힌 방, 식당 점수 52
기록: 용병 단골화 2, 고기 소비 3, 소란 1

진화 후보:
- 전투 식당
- 고급 고기 식당

흐릿한 영감:
- 무법자 주점: 악명이 더 필요합니다.
```

### 진화 후보 카드

카드 구성:

- 결과 시설 이름
- 결과 성급
- 계보 변화
- 필요 조건
- 충족/미충족 표시
- 필요한 재료
- LLM 해석 문장
- 게임 데이터 기반 효과 설명

중요:

LLM 문장은 flavor와 이유만 담당한다.

성능 설명은 반드시 게임 데이터에서 가져온다.

### 흐릿한 영감

조건이 부족한 후보는 실행 불가 카드로 보여줄 수 있다.

예:

```text
흐릿한 영감: 무법자 주점
이 식당은 거친 분위기를 띠지만, 아직 악명이 부족합니다.
```

정확한 수치 공개 여부는 연구/도감 상태에 따라 달라질 수 있다.

## 소비 규칙

기본 소비:

- 재료 소모
- 돈/마나 소모
- 일부 기록 토큰 소모 또는 단계 감소
- 현재 시설은 결과 시설로 교체

소모하지 않는 것:

- 방 자체
- 벽/문
- 일반 가구
- 인접 시설
- 기록 전체

특수 예외:

- 유니크 기물을 요구하는 진화식은 해당 기물을 소모할 수 있다.
- 저주/피/희생 계열은 일부 시설이나 가구를 소모하는 특수 진화식을 가질 수 있다.
- 이런 예외는 UI에서 명확히 경고한다.

## 검증 규칙

필수 검증:

- LLM 없이도 공개 진화식은 작동해야 한다.
- LLM이 알 수 없는 후보 ID를 제안하면 폐기한다.
- LLM이 수치를 제안해도 무시한다.
- 조건 미달 후보는 실행되지 않는다.
- 방 조건이 바뀌어 진화 조건을 잃으면 후보에서 빠진다.
- 결과 시설이 현재 위치/방에 배치 불가능하면 실행되지 않는다.
- 진화 후 고객 AI의 시설 역할이 갱신된다.
- 진화 후 도감/히스토리가 갱신된다.

## 디버그 패널

표시 항목:

```text
facilityInstanceId
currentFacilityId
starGrade
lineageTags
roomId
roomScores
roomTags
recordTokens
candidateEvolutionIds
llmProposalIds
approvedEvolutionIds
rejectedEvolutionReasons
lastLlmPrompt
lastLlmResponse
```

검증 로그 예:

```text
[FacilityEvolution]
facility=meat_restaurant#12
candidate=evolve_meat_restaurant_to_combat_tavern
result=approved

[FacilityEvolution]
facility=meat_restaurant#12
candidate=evolve_meat_restaurant_to_outlaw_pub
result=rejected
reason=Notoriety token 18/50
```

## 구현 단계

### 1단계: 기존 합성 용어 정리

목표:

- 시설끼리 합성한다는 표현을 줄인다.
- `시설 계보 진화`를 공식 명칭으로 사용한다.
- 기존 대표 합성 트리를 시설 진화 트리로 재해석한다.

### 2단계: RoomProfile

목표:

- 방의 조건, 기물 태그, 점수를 계산한다.
- 진화식이 방 구성물을 직접 나열하지 않고 RoomProfile을 보게 한다.
- 좌석 밀도, 좌석 간격, 좌석당 장식/서비스, 회전율, 체류 시간 같은 비율/맥락 지표를 만든다.

### 2.5단계: IdentityPressure

목표:

- RoomProfile과 운영 기록을 `Crowd`, `Luxury`, `Combat`, `Outlaw`, `Rest`, `Service`, `Security` 같은 정체성 압력으로 압축한다.
- 진화식이 단일 가구 수량이 아니라 정체성 점수 조합을 보게 한다.
- 상충 신호를 감점으로 처리하고, 여러 방향이 동시에 강하면 후보를 여러 개 노출한다.

### 3단계: RecordToken

목표:

- 시설/방 이용 기록을 토큰으로 압축한다.
- 고객 유형, 재고 소비, 사건, 방어 기여를 기록한다.

초기 토큰:

- MercenaryHangout
- HighMeatConsumption
- FrequentBrawls
- NoblePatronage
- GuardRallyPoint
- IntruderBloodied

### 4단계: LLM 없는 진화 검증

목표:

- 공개 진화식은 LLM 없이도 UI에 뜨고 실행된다.
- 코드 검증만으로 결과가 결정된다.
- 하드 조건과 정체성 점수만으로 후보 풀을 만들 수 있어야 한다.

### 5단계: LLM 추천과 서사

목표:

- 엔진이 후보 풀을 만든다.
- LLM은 후보 정렬, 이유, 힌트, flavor text를 만든다.
- LLM 입력은 FacilityIdentitySnapshot으로 제한하고 원본 로그 전체를 보내지 않는다.
- 검증은 코드가 한다.

### 6단계: 변이 수식어

목표:

- 조건을 과달성하면 제한된 변이 태그를 붙인다.
- LLM이 제안한 변이도 게임 데이터의 증거가 없으면 폐기한다.
- 레시피의 `allowedMutationTags`에 없는 태그는 절대 저장하지 않는다.
- 한 번의 진화에서 붙는 변이 태그 수는 제한한다.

처리 흐름:

```text
recipe.allowedMutationTags
+ LLM mutationTagSuggestions
+ RoomProfile identityPressures
+ RecordTokens / Metrics / Scores
-> MutationResolver
-> 검증된 mutationTags만 FacilityEvolutionState에 저장
```

예:

```text
LLM 제안: Brutal, Combat
현재 기록: 용병 단골화 2, 전투 압력 높음, 소란 기록 없음
레시피 허용: Brutal, Combat

결과:
- Combat 채택
- Brutal 탈락
```

`Brutal`은 허용 태그지만 소란, 유혈, 잔혹 기물 같은 증거가 없으므로 저장하지 않는다.

반대로 LLM이 아무 변이를 제안하지 않아도, 정체성 점수와 기록 토큰이 충분히 과달성되면 게임 로직이 허용된 변이를 붙일 수 있다.

예:

- Brutal
- Tactical
- Noble
- Blood
- Outlaw
- Clean

## P1 권장 범위

P1에서는 식당 계보만 먼저 검증한다.

시설:

```text
1성 고기 식당
2성 전투 식당
2성 고급 고기 식당
3성 전장의 식당
3성 귀족의 식당
```

기록 토큰:

```text
MercenaryHangout
HighMeatConsumption
FrequentBrawls
NoblePatronage
CleanServiceStreak
```

정체성 압력:

```text
Crowd
Luxury
Combat
Rest
Outlaw
Service
```

P1 판정 예:

```text
좌석 밀도 높음 + 회전율 높음
-> Crowd 압력 증가

좌석 밀도 낮음 + 좌석당 장식/서비스 높음 + 평균 지불액 높음
-> Luxury 압력 증가

Training/Combat 태그 + 용병 손님 + 고기 소비 + 소란
-> Combat 압력 증가
```

LLM:

```text
현재 시설 정체성 요약
진화 후보 추천 이유
흐릿한 영감 문장
진화 성공 flavor text
```

P1에서 하지 않는 것:

- 모든 계보 5성 완성
- 모든 가구 개별 합성
- 방 자체 합성
- LLM 신규 시설 생성
- 무한 변이 태그

## 최종 결론

최종 방향은 다음과 같다.

```text
시설끼리 합성하지 않는다.
방을 합성하지 않는다.
코어를 쓰지 않는다.

현재 시설이
방 조건, 기물 태그, 운영 기록, 재료, 연구, LLM 해석을 통해
다음 계보 시설로 진화한다.
```

이 구조의 장점:

- 던전메이커식 성급 계보 성장감이 유지된다.
- 림월드식 방/가구 구성과 충돌하지 않는다.
- LLM이 매 단계 개입할 명분이 생긴다.
- 플레이어의 운영 기록이 시설 성장에 직접 반영된다.
- 합성 대상이 애매하지 않다. 항상 `현재 시설`이 진화 대상이다.

플레이 감각:

```text
내가 이 식당을 이렇게 운영했기 때문에 전투 식당이 되었다.
그리고 이 전투 식당이 앞으로 어떤 계보로 갈지는
내가 어떤 손님을 받고, 어떤 사건을 만들고, 어떤 방 조건을 갖추느냐에 달려 있다.
```
