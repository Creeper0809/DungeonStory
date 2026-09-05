# 방 맥락 기반 시설 계보 진화

## 목적

이 문서는 `시설 계보 진화`를 실제 게임 판정으로 옮길 때, 방의 배치와 운영 기록을 얼마나 넓게 반영할지 정리한다.

시설 합성, 방 파츠, 운영 기록, 소문/평판, 범죄/위생, LLM 역할을 하나로 묶은 최신 결정본은 [컨텍스트 기반 시설 계보 진화 최종안](context-driven-facility-evolution.md)을 기준으로 한다.

핵심 결론:

- 방 자체를 상위 방으로 합성하지 않는다.
- 시설끼리 직접 먹여서 합성하지 않는다.
- 현재 방 안에서 운영 중인 `시설 인스턴스`가 진화 대상이다.
- 벽, 문, 가구, 동선, 손님층, 직원 상태, 사건, 소문, 평판은 모두 진화의 맥락으로 사용한다.
- 모든 가구마다 개별 조합식을 만들지 않는다.
- 가구와 운영 기록은 `RoomProfile`, `IdentityPressure`, `RecordToken`으로 압축한다.
- LLM은 후보를 창조하는 권한자가 아니라, 엔진이 만든 후보 풀 안에서 맥락을 해석하고 서사를 붙이는 역할이다.

한 줄 정의:

```text
현재 시설
+ 방의 물리 조건
+ 가구/기물의 밀도와 품질
+ 실제 이용 기록
+ 손님/직원/사건/평판 맥락
+ 재료/연구/설계도
+ LLM 해석
-> 다음 계보 시설로 진화
```

## 최종 판정 철학

이 시스템은 `정답 배치 조합식`을 맞히는 게임이 아니다.

플레이어가 배치한 테이블, 의자, 장식, 문, 화장실, 세면대, 훈련 기물, 경비 동선은 모두 중요하지만, 각각이 곧바로 조합식 재료가 되지는 않는다.

핵심은 다음 순서다.

```text
구체 배치
-> 방 안에서 가능한 운영 방식
-> 실제 손님/직원 이용 기록
-> 반복된 사건, 소문, 평판
-> 방의 업태 정체성
-> 현재 시설의 다음 계보 후보
```

예를 들어 `식당에 테이블과 의자가 많다`는 그 자체로 결과가 아니다.

```text
테이블과 의자가 많음
+ 좌석 밀도 높음
+ 빠른 회전율
+ 낮은/중간 객단가
+ 대기열과 위생 불만이 관리됨
-> 대중 식당

테이블과 의자가 많음
+ 큰 방
+ 좌석 간격이 답답하지 않음
+ 단체 손님 반복 이용
+ 체류 시간이 길고 행사 기록이 있음
-> 연회장

테이블과 의자가 많음
+ 작은 방
+ 대기열 과다
+ 위생 불만
+ 직원 스트레스 누적
-> 혼잡한 식당, 사고 위험, 저가 식당
```

반대로 `테이블과 의자가 적다`도 그 자체로 고급화를 의미하지 않는다.

```text
테이블과 의자가 적음
+ 넓은 좌석 간격
+ 좌석당 장식과 서비스 높음
+ 고소비 손님 반복 이용
+ 높은 평균 지불액
+ 조용하고 깨끗한 평판
-> 고급 식당

테이블과 의자가 적음
+ 낮은 장식
+ 낮은 방문 수
+ 낮은 매출
+ 특별한 평판 없음
-> 미성숙 식당

테이블과 의자가 적음
+ 장기 체류
+ 범죄 성향 손님
+ 보안 사각지대
+ 나쁜 소문 누적
-> 무법자 주점 후보
```

따라서 진화 판정의 핵심 문장은 다음이다.

```text
수량을 보되, 수량만 보지 않는다.
배치를 보되, 배치만 보지 않는다.
그 배치가 실제 운영에서 어떤 업태로 굳어졌는지를 본다.
```

## 컨텍스트를 전부 반영한다는 의미

여기서 `컨텍스트를 전부 반영한다`는 뜻은 모든 정보를 조합식의 필수 재료로 넣는다는 뜻이 아니다.

방 안의 모든 벽, 문, 테이블, 의자, 장식, 손님 기록을 다음처럼 직접 나열하면 시스템이 금방 무너진다.

```text
테이블 3개
+ 의자 10개
+ 고기 조리대 1개
+ 훈련 더미 1개
+ 오크 손님 12명
+ 소란 2회
= 전투 식당
```

이 방식은 콘텐츠를 조금만 늘려도 조합식 수가 폭발하고, 플레이어도 무엇이 중요한지 읽기 어렵다.

대신 모든 컨텍스트는 먼저 `방의 성격`과 `운영의 역사`로 압축된다.

```text
가구와 배치 -> RoomProfile
손님과 직원 이용 -> UsageProfile
사건과 소문 -> RecordToken
재고와 매출 -> EconomyProfile
위생과 배변 -> SanitationProfile
동선과 접근성 -> AccessibilityProfile
보안과 범죄 -> SecurityProfile

이 압축 결과
-> IdentityPressure
-> 진화 후보 점수
-> 엔진 검증
-> LLM 해석
```

즉 플레이어 입장에서는 `내가 놓은 것`, `내가 유도한 손님`, `내가 방치하거나 관리한 사건`이 모두 반영된다.

개발자 입장에서는 개별 오브젝트 조합식이 아니라, 안정적인 지표와 압력으로 판정할 수 있다.

### 컨텍스트 반영 계층

컨텍스트는 영향력이 서로 다르다.

```text
Hard Gate
- 반드시 만족해야 하는 조건
- 예: 현재 시설 계보, 성급, 연구 해금, 닫힌 방, 도달 가능성, 필수 유니크 기물

Primary Pressure
- 후보 방향을 강하게 미는 핵심 성격
- 예: Crowd, Luxury, Combat, Outlaw, Sanitation, StaffCare

Secondary Modifier
- 후보 점수를 보정하는 주변 맥락
- 예: 인접 훈련장, 인접 화장실, 경비 접근성, 조명, 소음, 습기

History Evidence
- 그 방이 실제로 그렇게 운영되었다는 증거
- 예: MercenaryHangout, NoblePatronage, FrequentBrawls, TheftPressure

Flavor Source
- 기능 판정보다는 LLM 서사와 도감 문장에 더 크게 쓰이는 맥락
- 예: 최근 소문, 단골 이름, 특이 사건, 직원 불만, 손님 평가
```

이렇게 나누면 `모든 것이 반영된다`와 `판정이 논리적으로 통제된다`가 동시에 가능하다.

### 컨텍스트 입력별 사용 규칙

모든 컨텍스트는 반영하되, 모두 같은 권한을 갖지 않는다.

아래 표는 각 입력이 엔진 판정과 LLM 해석에서 어떤 역할을 갖는지 고정하기 위한 기준이다.

| 입력 축 | 엔진이 만드는 지표 | 주로 미는 압력 | LLM 사용처 | 결과 확정 권한 |
| --- | --- | --- | --- | --- |
| 벽/문/닫힌 공간 | isClosed, entranceCount, pathReachable | 없음 또는 Accessibility | 방 요약 | Hard Gate |
| 면적/동선 | roomArea, pathWidth, congestion | Crowd, Luxury, Service | 빽빽함/여유로움 설명 | 점수 보정 |
| 테이블/의자 | seatCount, seatDensity, seatPerTable | Crowd, Luxury, Banquet | 업태 이유 설명 | 단독 확정 불가 |
| 장식/조명/소음 | luxuryPerSeat, comfort, noise | Luxury, Rest, SpeciesAffinity | 고급/불쾌 평판 설명 | 점수 보정 |
| 조리대/카운터/창고 접근 | servicePerSeat, stockLatency, turnoverRate | Service, Logistics, Crowd | 운영 안정성 설명 | 점수 보정 |
| 훈련/무기/전투 기물 | combatFixtureScore, trainingAdjacency | Combat, Security | 전투 문화 설명 | 기록 없으면 단독 확정 불가 |
| 화장실/세면/청소 | toiletCoverage, hygienePerUser, cleaningDelay | Sanitation, StaffCare | 위생/악취/청결 서사 | 고급 계열 게이트 보정 |
| 손님층 | visitorArchetypeRatio, spendTierRatio | Crowd, Luxury, Combat, Outlaw | 단골/고객층 설명 | History Evidence |
| 직원 이용 | staffVisitRatio, fatigueRecovered, stressDelta | StaffCare, Service | 직원용 공간 서사 | History Evidence |
| 매출/재고 | averageSpend, revenue, stockoutRate | Luxury, Crowd, Logistics | 잘 팔리는 이유 설명 | 점수 보정 |
| 범죄/소란/파손 | theftCount, brawlCount, damageCount | Outlaw, Combat, Security | 소문, 악명, 위험 서사 | History Evidence |
| 침입 방어 기여 | intruderDamage, delayTime, guardResponse | Combat, Security | 전장의 기억 서사 | History Evidence |
| 소문/평판/관계 | reputationTags, rumorPolarity, regularCustomerNotes | 모든 압력 보조 | 가장 강한 서사 재료 | 단독 확정 불가 |
| 시간 누적 | repeatedDays, trendDirection, streak | 신뢰도 보정 | 우연이 아니라 습관이라는 설명 | 승인 신뢰도 |

이 표의 핵심은 `단독 확정 불가`다.

테이블 수, 전투 기물, 소문 같은 신호는 강력하지만 혼자서는 결과를 확정하지 않는다.

```text
전투 기물 있음
+ 전투형 손님 기록 없음
-> 전투 식당 승인 아님
-> 전투 식당 near-miss 또는 흐릿한 영감
```

```text
고급 소문 있음
+ 평균 지불액 낮음
+ 좌석당 장식 낮음
+ 위생 불만 높음
-> 고급 식당 승인 아님
-> "가능성은 있지만 운영 품질이 부족함"으로 표시
```

반대로 물리 조건과 운영 기록이 서로 맞으면 승인 신뢰도가 높아진다.

```text
낮은 좌석 밀도
+ 높은 좌석당 장식
+ 높은 평균 지불액
+ 고소비 손님 반복 이용
+ 낮은 소란과 높은 위생
-> Luxury 압력 상승
-> 고급 식당 승인 후보
```

### 중요한 원칙

```text
많이 배치했다
-> 그 자체로 결과가 되지 않는다.

많이 배치했고, 그 배치가 방 크기와 동선 안에서 특정 운영 방식을 만들었고,
그 운영 방식이 실제 이용 기록으로 증명되었다
-> 진화 후보가 된다.
```

예를 들어 의자 수가 많아도 다음 결과는 달라진다.

```text
작은 방 + 의자 많음 + 빠른 회전 + 낮은 객단가
-> 대중 식당

큰 방 + 의자 많음 + 단체 손님 + 적당한 좌석 간격
-> 연회장

작은 방 + 의자 많음 + 대기열 + 위생 불만 + 직원 스트레스
-> 혼잡한 식당, 저가 식당, 사고 위험
```

반대로 의자 수가 적어도 다음 결과는 달라진다.

```text
의자 적음 + 장식 높음 + 서비스 높음 + 고소비 손님
-> 고급 식당

의자 적음 + 장식 낮음 + 방문 적음 + 매출 낮음
-> 미성숙 식당

의자 적음 + 장기 체류 + 범죄 손님 + 낮은 보안
-> 무법자 주점 후보
```

따라서 이 시스템의 핵심은 `수량 판정`이 아니라 `수량이 만든 운영 형태 판정`이다.

### 배치가 업태로 읽히는 방식

플레이어가 놓는 것은 테이블, 의자, 조리대, 장식, 문, 세면대 같은 구체물이다.

하지만 게임이 최종적으로 읽어야 하는 것은 `이 방이 어떤 업태로 굳어졌는가`다.

```text
구체 배치
-> 방 안에서 가능한 행동
-> 손님과 직원의 실제 이용 패턴
-> 반복된 사건과 평판
-> 업태 정체성
-> 시설 계보 후보
```

그래서 같은 테이블과 의자 배치라도 다음 축을 반드시 함께 본다.

```text
물리 축
- 방 면적, 좌석 수, 테이블 수, 좌석 간격, 문 위치, 동선 폭

품질 축
- 좌석당 장식, 좌석당 서비스, 조명, 위생, 소음, 습기, 접근성

운영 축
- 회전율, 체류 시간, 대기 시간, 재고 소비, 직원 작업량, 청소 빈도

고객 축
- 손님 종족, 직업/성향, 단골 비율, 평균 지불액, 불만/칭찬

사건 축
- 소란, 절도, 파손, 침입 방어 기여, 위생 실패, 배변 실패, 소문

시간 축
- 한 번 발생한 우연인지, 여러 운영일 동안 반복된 정체성인지
```

이 중 하나만으로 결과를 확정하지 않는다.

예를 들어 `의자가 적다`는 물리 신호일 뿐이다.

```text
의자 적음
+ 넓은 좌석 간격
+ 높은 장식/서비스
+ 높은 평균 지불액
+ 고소비 손님 반복 이용
+ 낮은 소란/높은 위생
-> 고급 식당
```

하지만 같은 `의자 적음`이라도 다음이면 전혀 다른 의미가 된다.

```text
의자 적음
+ 낮은 장식/서비스
+ 낮은 방문 수
+ 낮은 매출
+ 특별한 평판 없음
-> 미성숙 식당
```

즉 배치 신호는 항상 `운영으로 증명`되어야 한다.

### 식당 판정 매트릭스

초기 식당 계보는 아래처럼 판정한다.

| 후보 업태 | 물리 신호 | 운영 증거 | 고객/사건 증거 | 막히는 조건 |
| --- | --- | --- | --- | --- |
| 대중 식당 | 높은 좌석 밀도, 많은 좌석, 빠른 출입 동선 | 높은 회전율, 낮은/중간 객단가, 짧은 체류 | 많은 일반 손님, 불만이 낮음 | 대기열/위생 불만/직원 스트레스가 과도함 |
| 고급 식당 | 낮은 좌석 밀도, 넓은 좌석 간격, 높은 좌석당 장식 | 높은 평균 지불액, 긴 체류, 높은 서비스 안정성 | 귀족/고소비 손님, 조용한 평판 | 소란, 위생 실패, 낮은 서비스, 낮은 매출 |
| 연회장 | 큰 방, 큰 테이블, 많은 좌석이지만 답답하지 않음 | 단체 손님 처리, 중간 이상 객단가 | 집단 방문, 축제/행사 기록 | 작은 방의 단순 과밀 배치 |
| 전투 식당 | 식당 기능 + 훈련/무기/전투 장식 | 고기 소비, 전투형 손님 반복 이용 | 용병/오크, 소란/무용담, 인접 훈련 이용 | 전투 손님 기록 없이 전투 기물만 있음 |
| 무법자 주점 | 장기 체류하기 쉬운 구조, 보안 사각지대 | 늦은 체류, 불법 거래/절도 기록 | 범죄 성향 손님, 부정적 소문, 악명 | 경비 커버리지 높음, 범죄 기록 부족 |
| 직원 식당 | 직원 동선 가까움, 고객 동선과 일부 분리 | 직원 이용률, 피로 회복, 안정적 보급 | 직원 만족, 낮은 스트레스 | 손님 점유가 과도해 직원 이용 증거 부족 |

중요한 감각:

```text
테이블/의자 많음
-> 대중 식당이 아니라,
-> 많이 받고 빠르게 처리하는 운영이 증명되면 대중 식당

테이블/의자 적음
-> 고급 식당이 아니라,
-> 적게 받고 비싸게 오래 머물게 하는 운영이 증명되면 고급 식당
```

이 규칙 덕분에 플레이어는 단순히 정답 배치를 외우는 것이 아니라, 실제 운영 결과를 보며 업태를 유도하게 된다.

## 왜 이 방향인가

처음 아이디어는 던전메이커처럼 시설끼리 합성해서 성급을 올리는 감각이었다.

하지만 방을 림월드처럼 벽, 문, 가구, 동선으로 구성하려면 문제가 생긴다.

```text
식당 + 훈련장 = 전투 식당
```

이 방식은 직관적이지만, 다음 질문에 막힌다.

- 훈련장은 왜 사라지는가?
- 식탁, 의자, 조리대, 장식, 문, 벽은 합성 대상인가 아닌가?
- 방 안에 기물이 수십 개라면 조합식을 전부 만드는가?
- 방이 진화하는가, 시설이 진화하는가, 업태가 바뀌는가?

최종 방향은 다음이다.

```text
고기 식당이
훈련 요소가 가까운 방에서
용병 손님을 많이 받고
고기를 많이 소비하고
소란과 무용담 기록을 쌓았기 때문에
전투 식당 계보로 진화한다.
```

이렇게 하면 던전메이커식 성장감과 림월드식 공간 맥락을 동시에 살릴 수 있다.

## 역할 분리

### 방

방은 결과물이 아니라 맥락 생성기다.

방이 제공하는 것:

- 닫힌 공간 여부
- 문과 출입구
- 면적
- 내부 동선
- 좌석/테이블/카운터/장식/기물 배치
- 위생, 소음, 조명, 습기, 마력 같은 환경값
- 인접 시설과 구역 정보
- 손님과 직원의 이용 기록
- 사건, 소문, 평판

방은 `전투 식당 방`으로 승급하지 않는다.

방 안에서 운영 중인 식당 시설이 `전투 식당`으로 진화한다.

### 시설

시설은 계보와 성급을 가진 실제 성장 대상이다.

시설이 제공하는 것:

- 현재 시설 ID
- 현재 계보 태그
- 성급
- 기능과 역할
- 운영 히스토리
- 진화 히스토리
- 변이 수식어

예:

```text
1성 고기 식당
-> 2성 전투 식당
-> 3성 전장의 식당
-> 4성 군단 연회장
```

계보는 한 번 정해지면 고정된 직선 트리로만 가는 것이 아니다.

`전투 식당`이 된 뒤에도 다음 진화는 다시 현재 방의 맥락을 읽는다.

예:

```text
2성 전투 식당
+ 용병/오크 단골
+ 고기 소비 많음
+ 소란이 있지만 통제됨
+ 인접 훈련장 이용 많음
-> 3성 전장의 식당

2성 전투 식당
+ 범죄 손님 증가
+ 절도/밀거래 소문
+ 경비 사각지대
+ 악명 상승
-> 3성 무법 투기 주점

2성 전투 식당
+ 경비 직원 이용 많음
+ 침입 방어 기여 높음
+ 보안 압력 높음
-> 3성 경비대 식당

2성 전투 식당
+ 좌석 밀도 낮음
+ 고급 장식과 서비스
+ 귀족 전투광 손님
-> 3성 결투 귀빈식당
```

즉 `전투 식당`은 LLM 개입이 끝나는 최종 결과가 아니라, 다음 맥락 판정의 현재 시설이 된다.

이 구조 덕분에 던전메이커식 성급 상승 감각을 유지하면서도, 림월드식으로 플레이어의 배치와 운영사가 계속 결과를 바꾼다.

### 가구와 기물

가구는 대부분 직접 합성 재료가 아니다.

가구는 방의 성격을 만드는 입력이다.

```text
식탁: Dining +10
의자: Seat +1
고기 조리대: Dining +20, Meat +20
훈련 더미: Training +25
무기 거치대: Combat +20
고급 샹들리에: Luxury +20
세면대: Hygiene +15
화장실 칸: Toilet +20, Sanitation +10
피 묻은 장식: Brutal +15
```

일반 진화식은 개별 가구 이름보다 태그와 점수를 본다.

나쁜 방식:

```text
식탁 2개 + 의자 8개 + 조리대 1개 + 훈련 더미 1개 = 전투 식당
```

좋은 방식:

```text
현재 시설: 고기 식당
방 조건: Dining >= 40
환경 조건: Combat >= 20 또는 Training >= 15
기록 조건: MercenaryHangout >= 1
결과: 전투 식당
```

예외적으로 유니크 기물은 직접 요구하거나 소모할 수 있다.

예:

- 마왕의 식탁
- 영웅의 부서진 검
- 피의 왕관
- 고대 화로
- 저주받은 세면대

이런 예외는 특수 진화식에서만 쓰고, UI에서 명확히 경고한다.

## 컨텍스트 파이프라인

모든 원본 데이터를 진화식에 직접 넣지 않는다.

다음 흐름으로 압축한다.

```text
Raw Context
-> Normalized Metric
-> Identity Pressure
-> Candidate Score
-> Engine Validation
-> LLM Interpretation
-> Player Choice
```

### 1. Raw Context

게임에서 실제로 발생한 원본 정보다.

예:

- 방 면적
- 벽과 문
- 테이블 수
- 의자 수
- 좌석 간격
- 장식 점수
- 조리대 수
- 서비스 카운터 수
- 화장실/세면 시설 수
- 방문 손님 수
- 평균 지불액
- 평균 체류 시간
- 대기 시간
- 재고 소비량
- 직원 근무 시간
- 직원 기분과 스트레스
- 청소 빈도
- 위생 사고
- 배변 욕구 실패
- 소란, 절도, 난동, 파손
- 침입자 피해 기여
- 인접 방과 구역
- 최근 소문과 평판

### Raw Context 사용처

원본 컨텍스트는 모두 같은 방식으로 쓰지 않는다.

```text
벽/문/닫힌 공간
-> Hard Gate, AccessibilityProfile
-> 방이 성립하는지, 손님과 직원이 도달 가능한지 판단

면적/동선/좌석 간격
-> Normalized Metric, Crowd/Luxury 압력
-> 빽빽한가, 여유로운가, 대기열이 생기는가 판단

테이블/의자 수
-> Dining 수용력, seatDensity, turnoverRate
-> 대중 식당, 연회장, 고급 식당 방향을 가르는 입력

장식/조명/소음/습기
-> Luxury, Rest, SpeciesAffinity, Sanitation 보정
-> 고급화, 휴식 특화, 종족 특화, 불쾌감 판단

조리대/카운터/창고 접근성
-> Service, Logistics, turnoverRate
-> 빠른 회전, 품절 위험, 직원 부담 판단

훈련 더미/무기 거치대/전투 장식
-> Combat 압력
-> 전투 식당, 경비대 시설, 투기장 계열 후보 강화

화장실/세면대/청소 기록
-> SanitationProfile
-> 고급 시설 보정, 위생 실패 계열, 직원 만족 판단

손님 종족/성향/소비액/체류 시간
-> UsageProfile, EconomyProfile, SpeciesAffinity
-> 대중, 고급, 전투, 무법, 종족 특화 후보 판정

직원 이용/피로/스트레스/청소 대응
-> StaffCare, Service, Sanitation
-> 직원 식당, 휴게실, 운영 안정성 판단

절도/난동/불법 거래/악명
-> Outlaw, Security, RecordToken
-> 무법자 주점, 암시장, 위험 계열 후보 판단

침입 방어 기여/경비 동선
-> Security, Combat, IntruderBloodied
-> 방어형 시설, 경비대 식당, 전장의 식당 후보 판단

소문/평판/단골 이야기
-> History Evidence, Flavor Source
-> LLM의 이유, 도감 문장, 흐릿한 영감에 사용
```

이 표에서 중요한 것은 `소문`이나 `평판`도 단순 장식이 아니라는 점이다.

예를 들어 범죄 소문은 바로 무법자 주점을 승인하지는 않지만, Outlaw 압력과 near-miss 힌트를 강하게 만든다.

반대로 고급 손님 평판은 Luxury 압력을 강화하되, 장식과 서비스, 평균 지불액이 없으면 승인 조건을 통과하지 못한다.

### 2. Normalized Metric

원본 정보를 방 크기, 좌석 수, 시간, 방문 횟수 기준으로 정규화한다.

예:

```text
seatDensity = seatCount / roomArea
tableDensity = tableCount / roomArea
seatPerTable = seatCount / tableCount
luxuryPerSeat = luxuryScore / max(1, seatCount)
servicePerSeat = serviceScore / max(1, seatCount)
hygienePerUser = hygieneScore / max(1, averageOccupancy)
toiletCoverage = toiletCapacity / max(1, visitorAndStaffDemand)
turnoverRate = completedVisits / timeWindow
averageSpend = revenue / max(1, completedVisits)
crowdStress = waitTime + complaintRate + seatDensityPenalty
premiumSignal = luxuryPerSeat + servicePerSeat + averageSpend + nobleVisitorRatio
combatCulture = combatFixtureScore + mercenaryRatio + brawlMemory
sanitationRisk = toiletDemandFailures + hygieneComplaints + cleaningDelay
```

중요한 점은 `많다/적다`를 그대로 쓰지 않는 것이다.

의자가 8개여도 방이 작으면 빽빽한 식당이고, 방이 크면 여유 있는 식당일 수 있다.

### 3. Identity Pressure

정규화된 지표를 방과 시설의 정체성 압력으로 환산한다.

기본 압력:

```text
Crowd
Luxury
Combat
Outlaw
Rest
Service
Security
Logistics
Research
Magic
Sanitation
StaffCare
SpeciesAffinity
Ritual
```

예:

```text
Crowd
- 높은 좌석 밀도
- 높은 회전율
- 낮은 평균 지불액
- 짧은 체류 시간
- 낮은 좌석당 장식

Luxury
- 낮은 좌석 밀도
- 높은 좌석당 장식
- 높은 좌석당 서비스
- 높은 평균 지불액
- 귀족/고소비 손님 비율
- 낮은 소란율

Combat
- 훈련/무기/전투 기물
- 용병/오크/전투형 손님 비율
- 고기 소비
- 소란 또는 무용담 기록
- 경비 인력 이용 기록

Outlaw
- 범죄 성향 손님 비율
- 절도/밀거래/소란 기록
- 낮은 경비 커버리지
- 부정적 소문

Sanitation
- 높은 청결도
- 충분한 화장실/세면 커버리지
- 낮은 위생 불만
- 빠른 청소 대응

StaffCare
- 직원 전용 이용 기록
- 피로 회복 기록
- 낮은 직원 스트레스
- 높은 휴식 접근성
```

### 4. Candidate Score

각 진화 후보는 자신에게 필요한 압력만 본다.

예:

```text
대중 식당 후보
+ Crowd
+ Service
- Luxury가 지나치게 높음
- SanitationRisk가 지나치게 높음

고급 식당 후보
+ Luxury
+ Service
+ Sanitation
- Crowd
- Combat
- Outlaw

전투 식당 후보
+ Combat
+ Crowd 또는 Meat
+ MercenaryHangout
- Luxury가 높지만 전투 기록이 없음

무법자 주점 후보
+ Outlaw
+ longStayDuration
+ negativeRumor
- Security

직원 식당 후보
+ StaffCare
+ Service
+ staffVisitRatio
- customerPriority가 지나치게 높음
```

### 상충 신호 처리

후보 점수는 단순히 높은 압력을 더하는 방식이면 안 된다.

서로 맞지 않는 신호가 함께 있을 때는 `혼합 후보`, `미성숙 후보`, `near-miss`로 분리한다.

예:

```text
Luxury 높음 + Crowd 높음
-> 고급 식당과 대중 식당이 동시에 승인되지 않는다.
-> 좌석 간격, 평균 지불액, 서비스 점수, 체류 시간을 보고 우세한 쪽을 고른다.
-> 둘 다 애매하면 연회장, 인기 식당, 혼잡한 고급 식당 같은 중간 후보를 본다.

Combat 높음 + Luxury 높음
-> 전투 식당을 바로 고급 식당으로 덮어쓰지 않는다.
-> 전투 손님 기록이 강하면 결투 귀빈식당, 기사단 식당 같은 혼합 계보를 본다.
-> 전투 기록이 약하면 고급 식당 쪽 near-miss로만 남긴다.

Outlaw 높음 + Security 높음
-> 무법자 주점 승인 가능성이 낮아진다.
-> 대신 감시 주점, 경비대 잠복소 같은 보안형 변이를 본다.

Sanitation 높음 + Crowd 높음
-> 대중 식당의 안정성 보정으로 쓴다.
-> 고급 식당으로 자동 이동하지 않는다.

Sanitation 낮음 + Luxury 높음
-> 고급 식당 승인을 막는다.
-> LLM은 "격은 있으나 냄새가 발목을 잡는다" 같은 흐릿한 영감만 쓴다.
```

우선순위:

```text
1. Hard Gate 실패 후보는 탈락
2. 후보의 Primary Pressure가 부족하면 탈락 또는 near-miss
3. 충돌 신호가 강하면 혼합 후보를 먼저 확인
4. 혼합 후보도 없으면 가장 강한 정체성만 승인
5. 탈락한 후보는 LLM이 흐릿한 영감으로 설명
```

### 5. Engine Validation

점수가 높아도 하드 조건을 통과해야 한다.

검증 항목:

```text
현재 시설에서 갈 수 있는 계보인가
필요 성급을 만족하는가
필요 연구/설계도가 해금되었는가
방이 닫혀 있고 도달 가능한가
필요한 방 점수와 지표를 만족하는가
필요 기록 토큰이 있는가
재료, 돈, 마나가 충분한가
결과 시설이 현재 위치에 배치 가능한가
밸런스 제한을 넘지 않는가
```

### 6. LLM Interpretation

LLM은 검증을 통과한 후보와, 거의 도달한 후보를 읽고 설명한다.

LLM이 하는 일:

- 현재 시설 정체성 요약
- 후보 추천 순서 제안
- 후보별 이유 작성
- 미달 후보에 대한 흐릿한 영감 작성
- 진화 성공 flavor text 작성
- 허용된 변이 수식어 후보 제안
- 도감에 남길 히스토리 문장 작성

LLM이 하지 않는 일:

- 존재하지 않는 시설 ID 만들기
- 수치 만들기
- 성능 만들기
- 비용 만들기
- 조건을 무시하고 승인하기

## 식당 예시

사용자가 말한 `테이블과 의자가 많으면 일반 식당, 적으면 고급화 식당` 방향은 맞다.

다만 단순 수량이 아니라 다음처럼 읽어야 한다.

### 대중 식당

```text
테이블과 의자가 많음
+ 방 면적 대비 좌석 밀도 높음
+ 회전율 높음
+ 평균 지불액 낮음 또는 중간
+ 대기열 처리 빠름
+ 큰 불만 없이 많은 손님을 처리
-> 대중 식당 / 군중 식당
```

이 방은 `많이 앉히고 빠르게 먹이는 업태`다.

### 고급 식당

```text
테이블과 의자가 적음
+ 좌석 간격 넓음
+ 좌석당 장식 점수 높음
+ 좌석당 서비스 점수 높음
+ 평균 지불액 높음
+ 귀족/고소비 손님 비율 높음
+ 소란 낮음
+ 위생 상태 좋음
-> 고급 식당 / 귀족 식당
```

이 방은 `적게 받고 비싸게, 오래 머무르게 하는 업태`다.

### 미성숙 식당

```text
테이블과 의자가 적음
+ 장식도 낮음
+ 서비스도 낮음
+ 방문 수도 낮음
+ 평균 지불액도 낮음
-> 고급 식당 후보 아님
```

테이블이 적다고 자동으로 고급이 되는 것이 아니다.

고급화는 `희소성 + 품질 + 실제 고소비 기록`이 함께 있어야 한다.

### 연회장

```text
테이블과 의자가 많음
+ 방 면적도 큼
+ 좌석 간격이 답답하지 않음
+ 큰 테이블 비율 높음
+ 단체 손님 기록 많음
+ 평균 지불액 중간 이상
-> 연회장 / 대형 식당
```

같은 `좌석이 많다`라도 작은 방에 빽빽하게 넣으면 대중 식당이고, 큰 방에 넉넉하게 넣고 단체 손님을 받으면 연회장이 된다.

### 전투 식당

```text
고기 식당
+ 훈련 더미 또는 무기 기물
+ 용병/오크 손님 비율 높음
+ 고기 소비량 높음
+ 소란 또는 무용담 기록
+ 인접 훈련장 이용 기록
-> 전투 식당
```

전투 식당은 테이블 수로 결정되지 않는다.

식당의 운영 문화가 전투 손님과 결합했는지가 핵심이다.

### 무법자 주점

```text
식당 또는 주점 계열
+ 장기 체류 손님
+ 범죄 성향 손님 비율
+ 절도/난동/불법 거래 소문
+ 낮은 경비 커버리지
+ 악명 기록
-> 무법자 주점
```

이 후보는 반드시 코드 검증으로 악명, 사건, 보안 조건을 확인해야 한다.

### 같은 테이블/의자 배치의 다른 결과

같은 `테이블 4개, 의자 16개`라도 결과는 하나가 아니다.

```text
작은 방
+ 좌석 밀도 높음
+ 빠른 회전율
+ 평균 지불액 낮음
+ 큰 불만 없음
-> 대중 식당

큰 방
+ 좌석 간격 적당함
+ 단체 손님 기록 많음
+ 평균 지불액 중간 이상
+ 대기열 안정적
-> 연회장

큰 방
+ 좌석 간격 넓음
+ 고급 장식과 서비스 높음
+ 고소비 손님 많음
+ 체류 시간 김
-> 고급 식당 또는 귀족 식당

작은 방
+ 좌석 밀도 높음
+ 대기열 길음
+ 위생 불만 높음
+ 직원 스트레스 높음
-> 미성숙 대중 식당, 혼잡한 식당, 사고 위험

중간 방
+ 용병 손님 많음
+ 훈련 기물 인접
+ 고기 소비 높음
+ 소란 기록 있음
-> 전투 식당

중간 방
+ 장기 체류 손님 많음
+ 절도 소문 있음
+ 보안 낮음
+ 악명 상승
-> 무법자 주점
```

이 판정이 중요한 이유:

```text
배치 수량
-> 방의 물리적 성격
-> 실제 이용 패턴
-> 운영 기록
-> 시설 계보
```

순서로 읽기 때문에, 플레이어가 같은 오브젝트를 써도 운영 방식에 따라 다른 성장이 나온다.

## 방별 예시 확장

### 상점

```text
잡화점
+ 높은 회전율
+ 낮은 객단가
+ 다양한 재고 소비
-> 대중 상점

잡화점
+ 고가 거래
+ 낮은 방문 수
+ 높은 평균 지불액
+ 귀족 손님
-> 고급 상점

잡화점
+ 절도 기록
+ 범죄 손님
+ 음성적 소문
+ 보안 사각지대
-> 암시장
```

### 휴식방

```text
휴식방
+ 높은 직원 이용률
+ 피로 회복 기록
+ 손님보다 직원 우선 정책
-> 직원 휴게실

휴식방
+ 높은 위생 점수
+ 세면/화장실 인접
+ 낮은 불만
+ 긴 체류 시간
-> 고급 휴식실

휴식방
+ 습기 환경
+ 슬라임 이용률
+ 점액 관련 긍정 소문
-> 점액 휴식실
```

### 위생/배변 시설

배변과 위생은 별도 욕구로 분리하는 것이 좋다.

둘은 모두 청결과 만족도에 영향을 주지만, 플레이 감각과 운영 결과가 다르다.

```text
배변
- 화장실 수용량
- 접근 거리
- 대기열
- 실패 시 사고/오염/불만

위생
- 세면대/목욕/청소
- 직원 청소 빈도
- 악취/오염 회복
- 식당, 휴식방, 고급 시설 품질 보정
```

진화 반영 예:

```text
식당 + 높은 위생 + 높은 서비스 + 고소비 손님
-> 고급 식당 후보 강화

휴식방 + 세면/목욕 + 청결 유지 기록
-> 고급 휴식실 또는 회복실 후보

화장실 + 높은 이용률 + 청소 안정성
-> 공중 화장실, 직원 화장실, 고급 세면실 계보

위생 실패 + 악취 소문 + 범죄/무법 손님
-> 지저분한 주점, 오물 구덩이 같은 특수 계보의 흐릿한 영감
```

## 기록 토큰

운영 기록은 원본 로그 전체를 들고 다니지 않고 토큰으로 압축한다.

예:

```text
MercenaryHangout
- 용병/전투형 손님의 반복 이용

HighMeatConsumption
- 고기 재고 소비량이 높음

FrequentBrawls
- 소란/싸움 사건 반복

NoblePatronage
- 귀족/고소비 손님 방문 많음

CleanServiceStreak
- 청결과 서비스가 일정 기간 안정적

SanitationFailure
- 배변/위생 실패가 반복됨

GuardRallyPoint
- 경비 인력이 자주 모이는 장소

IntruderBloodied
- 침입자 피해 기여가 높음

TheftPressure
- 절도/분실/암거래 기록 증가

StaffFavorite
- 직원 비번 이용률이 높음
```

기록 토큰 소비 정책:

```text
Preserve
- 시설의 역사와 평판을 증명하는 토큰
- 진화 후에도 히스토리로 남긴다.

ConsumeRequiredAmount
- 의식, 계약, 특별 주문처럼 일부만 소모되는 토큰

ConsumeAll
- 저주, 폭주, 정화 대상처럼 진화 후 사라져야 하는 토큰
```

대부분의 운영 기록은 소모하지 않고 보존하는 편이 좋다.

그래야 플레이어가 `내가 이 시설을 이렇게 운영했다`는 감각을 잃지 않는다.

## 재료와 합성 감각

시설끼리 직접 합성하지 않더라도 합성 감각은 남길 수 있다.

소모되는 것:

- 돈
- 재료
- 마나
- 설계도 연구 해금
- 일부 소모형 기록 토큰
- 특수 유니크 기물

소모되지 않는 것:

- 방 자체
- 벽
- 문
- 일반 테이블/의자
- 인접 시설
- 운영 기록 전체

플레이어 감각:

```text
내가 이 시설을 운영해서 계보 조건을 만들었고,
필요한 재료를 모아
현재 시설을 다음 성급 시설로 합성 진화시켰다.
```

이렇게 하면 던전메이커식 `성급을 올리는 합성`은 살아 있고, 림월드식 `방과 가구가 이야기를 만든다`도 살아 있다.

## LLM 입력과 출력

### 입력

LLM에는 원본 로그 전체를 보내지 않는다.

`FacilityIdentitySnapshot`만 보낸다.

```text
facilityInstanceId
facilityName
currentFacilityId
starGrade
lineageTags
roomSummary
topMetrics
identityPressures
topRecordTokens
dominantSignals
conflictingSignals
recentEventSummary
longTermHistorySummary
candidateEvolutionIds
nearMissEvolutionIds
```

예:

```json
{
  "facilityName": "고기 식당",
  "starGrade": 1,
  "roomSummary": "닫힌 8칸 식당. 좌석 밀도는 높고 장식은 낮다.",
  "topMetrics": {
    "seatDensity": 0.82,
    "turnoverRate": 0.76,
    "averageSpend": 0.42,
    "combatVisitorRatio": 0.61,
    "hygienePerUser": 0.48
  },
  "identityPressures": {
    "Crowd": 0.78,
    "Combat": 0.66,
    "Luxury": 0.12,
    "Sanitation": 0.38
  },
  "topRecordTokens": {
    "MercenaryHangout": 2,
    "HighMeatConsumption": 3,
    "FrequentBrawls": 1
  },
  "candidateEvolutionIds": [
    "evolve_meat_restaurant_to_combat_tavern",
    "evolve_meat_restaurant_to_mass_diner"
  ],
  "nearMissEvolutionIds": [
    "evolve_meat_restaurant_to_outlaw_pub"
  ]
}
```

### 출력

LLM 출력은 제한된 스키마를 따른다.

```text
facilityIdentitySummary
rankedProposalIds
proposalReasons
nearMissHintText
mutationTagSuggestions
flavorText
codexHistoryText
confidence
```

중요:

- `rankedProposalIds`는 입력 후보 ID 안에 있는 값만 허용한다.
- `mutationTagSuggestions`는 레시피의 `allowedMutationTags` 안에서만 검증 가능하다.
- `flavorText`는 기능 설명이 아니다.
- 최종 승인과 수치는 엔진이 결정한다.

## UI 설계

시설 선택 패널에는 다음 정보를 보여준다.

```text
현재 시설 이름
성급
계보 태그
방 요약
주요 정체성 압력
주요 기록 토큰
진화 후보
흐릿한 영감
```

예:

```text
고기 식당 / 1성
방 성격: 빽빽한 좌석, 빠른 회전, 용병 단골
정체성: Crowd 높음, Combat 높음, Luxury 낮음
기록: 용병 단골화 2, 고기 소비 3, 소란 1

진화 후보:
- 전투 식당
- 대중 식당

흐릿한 영감:
- 무법자 주점: 악명과 범죄 기록이 더 필요합니다.
```

디버그 패널에는 수치를 모두 보여준다.

```text
RoomProfile
NormalizedMetrics
IdentityPressures
RecordTokens
CandidateScores
EngineValidationResults
LlmInputSnapshot
LlmOutput
FinalApprovedCandidates
RejectedReasons
```

디버그 로그 예:

```text
[RoomContext]
room=room_dining_003
seatDensity=0.82
luxuryPerSeat=0.14
combatVisitorRatio=0.61
Crowd=0.78
Combat=0.66
Luxury=0.12

[FacilityEvolution]
facility=meat_restaurant#12
candidate=evolve_meat_restaurant_to_combat_tavern
score=0.72
validation=approved

[FacilityEvolution]
facility=meat_restaurant#12
candidate=evolve_meat_restaurant_to_outlaw_pub
score=0.51
validation=rejected
reason=Notoriety 18/50, TheftPressure 0/2
```

## 구현 순서

### 1단계: RoomProfile

목표:

- 현재 방의 면적, 닫힘 여부, 도달 가능성, 가구 점수, 기본 지표를 계산한다.
- 기존 Building 기반 구조에서도 우선 시설 주변/점유 셀을 임시 방으로 취급할 수 있다.

필수 지표:

```text
area
isClosed
hasDoor
reachable
seatCount
tableCount
seatDensity
luxuryScore
serviceScore
hygieneScore
combatFixtureScore
trainingScore
```

### 2단계: Context Profile 분리

목표:

- RoomProfile 하나에 모든 정보를 밀어 넣지 않는다.
- 물리 배치, 이용 기록, 경제, 위생, 보안, 접근성을 서로 다른 프로필로 분리한다.
- 각 프로필은 원본 로그를 직접 들고 있지 않고, 후보 점수 계산에 필요한 압축 지표만 가진다.

초기 프로필:

```text
RoomProfile
- 면적, 닫힘, 문, 동선, 가구/기물 점수

UsageProfile
- 방문 수, 손님층, 직원 이용률, 체류 시간, 회전율

EconomyProfile
- 평균 지불액, 매출, 재고 소비, 품절 횟수

SanitationProfile
- 청결도, 화장실 커버리지, 세면 커버리지, 위생 실패, 청소 대응

AccessibilityProfile
- 출입 가능성, 대기열, 창고 접근성, 직원 동선, 손님 동선

SecurityProfile
- 경비 커버리지, 절도/난동 기록, 침입 방어 기여, 보안 사각지대
```

이 분리는 나중에 코드 구조에서도 중요하다.

식당 진화 시스템이 화장실 로그, 범죄 로그, 직원 스트레스 로그를 직접 뒤지는 구조가 되면 커플링이 심해진다.

각 도메인은 자기 프로필을 만들고, 진화 판정기는 압축된 프로필만 읽는다.

### 3단계: 운영 기록 토큰

목표:

- 시설 이용 이벤트를 기록 토큰으로 압축한다.
- 고객층, 재고 소비, 소란, 위생 실패, 방어 기여를 기록한다.

초기 토큰:

```text
MercenaryHangout
HighMeatConsumption
FrequentBrawls
NoblePatronage
CleanServiceStreak
SanitationFailure
IntruderBloodied
TheftPressure
```

### 4단계: IdentityPressure

목표:

- RoomProfile과 기록 토큰을 정체성 압력으로 변환한다.
- 후보별 점수 계산을 만든다.

초기 압력:

```text
Crowd
Luxury
Combat
Outlaw
Service
Sanitation
StaffCare
Security
```

### 5단계: LLM 없는 엔진 검증

목표:

- 후보 풀 생성과 승인 검증은 엔진만으로 작동한다.
- 공개 진화식은 LLM 문장 없이도 UI에 뜨고 실행된다.

### 6단계: LLM 해석 연결

목표:

- 엔진이 만든 후보 풀과 스냅샷을 LLM에 전달한다.
- LLM이 이유, 흐릿한 영감, flavor text, 도감 문장을 반환한다.
- 반환값은 다시 엔진 검증을 통과해야 UI에 반영된다.

### 7단계: UI와 디버그

목표:

- 플레이어용 UI는 정체성 압력과 후보 이유를 읽기 쉽게 보여준다.
- 개발자용 디버그는 원본 지표, 점수, 거절 사유, LLM 입출력을 모두 보여준다.

## 최종 감각

이 시스템에서 플레이어는 다음을 하게 된다.

```text
방을 어떻게 나눌지 정한다.
테이블, 의자, 장식, 위생 시설, 훈련 기물을 어떻게 놓을지 정한다.
어떤 손님층을 유도할지 정한다.
직원을 어떻게 배치할지 정한다.
사건과 문제를 방치할지 관리할지 정한다.
그 결과 열린 시설 계보 중 하나를 선택해 성급을 올린다.
```

즉 `상위 방을 만드는 게임`이 아니라,

```text
방의 배치와 역사가 현재 시설의 계보를 밀어 올리는 게임
```

이다.

이 방향이면 같은 식당도 플레이어의 운영 방식에 따라 완전히 다른 시설이 된다.

```text
빽빽하고 빠른 식당 -> 대중 식당
넓고 비싼 식당 -> 고급 식당
용병이 모이는 식당 -> 전투 식당
범죄 소문이 도는 식당 -> 무법자 주점
직원이 자주 쓰는 식당 -> 직원 식당
위생과 서비스가 뛰어난 식당 -> 귀족 식당
```

그리고 각 계보는 다시 다음 성급으로 이어진다.

```text
고기 식당
-> 전투 식당
-> 전장의 식당
-> 군단 연회장

고기 식당
-> 고급 식당
-> 귀족의 식당
-> 왕실 연회장

고기 식당
-> 대중 식당
-> 군중 급식소
-> 전시 보급 식당
```

이것이 던전메이커식 합성과 림월드식 방 맥락을 결합한 최종 방향이다.
