# P2 9 런 변수와 이벤트 구현 보고

## 구현 목적

P2 9번의 목표는 같은 던전을 반복해서 돌릴 때도 매 런과 운영일, 침입 상황이 완전히 동일하게 흐르지 않게 만드는 것이다.

이번 구현은 선택지 많은 대형 이벤트 시스템이 아니라, 기존 경영/재고/상점/침입 시스템의 계산식에 작은 배율 변수를 얹는 방식으로 시작했다.

## 핵심 파일

- `Assets/Scripts/Run/RunVariableSystem.cs`
- `Assets/Scripts/Run/Editor/RunVariableDebugScenarios.cs`
- `Assets/Scripts/GameManager.cs`
- `Assets/Scripts/Character/CharacterSpawner.cs`
- `Assets/Scripts/Buildings/SO/StockInfo.cs`
- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`
- `Assets/Scripts/Invasion/InvasionThreatSystem.cs`
- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`

## 구현 로직

### 1. 런 시작 변수

`RunVariableRuntime.StartRun`은 런 시작 시 `RunStartVariableSnapshot`을 만든다.

스냅샷에 들어가는 값:

- 사장 종족
- 시작 시설 후보 ID
- 시작 손님층 후보
- 시작 설계도 후보 ID
- 초기 상점 시드
- 초기 던전 구조 ID
- 난이도
- 난이도 기반 위협 상승 계수

시설과 설계도 후보는 현재 `Resources/SO/Building`, `Resources/SO/Blueprint` 풀에서 뽑는다. 초기 상점 시드는 `FacilityShopService`의 일일 상품 랜덤에 섞어 같은 1일차라도 런에 따라 상품이 달라질 수 있게 했다. 던전 구조는 실제 맵 생성까지 연결하지 않고, 우선 `wet-front`, `training-front`, `research-front` 같은 구조 ID로 기록한다.

### 2. 운영 중 이벤트

운영 이벤트는 `RunVariableCatalog`에 데이터로 정의했다.

구현한 이벤트:

- 슬라임 단체 방문
- 오크 회식
- 뱀파이어 야간 방문
- 식재료 납품 지연
- 잡화 할인
- 마력 재고 과잉
- 방문 상인
- 방어 시설 할인
- 설계도 소문

각 운영일 시작 시 2일차부터 하나의 운영 이벤트를 뽑아 활성화한다. 활성 이벤트는 기본 1일 유지되고, 운영일 종료 시 만료된다.

적용 방식:

- 손님층 이벤트: `CharacterSpawner`가 해당 종족 손님의 재등장 간격을 줄인다.
- 재고 이벤트: `StockSupplyService.CreateDailyDeliveryOffers`가 카테고리별 납품 가격 배율을 적용한다.
- 시설 상점 이벤트: `FacilityShopService`가 시설/방어 시설/설계도 비용 배율을 적용한다.
- 알림: `EventAlertService`로 오른쪽/우하단 이벤트 버튼 시스템에 기록한다.

### 3. 침입 변수

침입 후보 이벤트가 발생하면 `RunVariableRuntime`이 침입 변수 하나를 선택한다.

구현한 침입 변수:

- 정찰 흔적
- 급습
- 무장한 침입자
- 약탈 우선
- 피로한 침입자

`InvasionDirectorRuntime`은 침입자를 스폰하기 직전에 `RunVariableRuntime.ApplyInvasionSettings`를 호출해 침입자 설정을 보정한다.

적용 값:

- 사장방 집중 시간
- 경로 재탐색 간격
- 시설 파손 주기
- 최종 교전 피해

침입이 해결되면 현재 침입 변수는 초기화된다.

## 설계 판단

이벤트를 별도 행동 시스템으로 만들지 않고, 기존 계산식에 배율로 붙였다.

이유:

- P2 단계에서는 이벤트의 재미가 있는지 빠르게 검증해야 한다.
- 재고, 상점, 손님 스폰, 침입자 로직을 새 이벤트 시스템이 직접 소유하면 커플링이 커진다.
- 배율 방식이면 이벤트 수를 늘려도 기존 시스템 수정이 작다.
- 이벤트가 즉시 큰 피해를 주기보다 우선순위를 바꾸는 압력으로 작동한다.

## 디버그 검증

추가한 메뉴:

```text
DungeonStory/Debug/Run/Run P2 Run Variable Scenarios
```

검증 항목:

- 런 시작 스냅샷 생성
- 운영 이벤트 9종 배율 적용
- 운영 이벤트 만료
- 재고/시설/설계도 비용 연결
- 침입 변수 5종 설정 보정
- 이벤트 알림 발행

## 남은 확장

- 시작 던전 구조 ID를 실제 맵 프리셋 생성과 연결
- 시작 시설 후보를 실제 선택 UI와 연결
- 운영 이벤트를 무작위 발동뿐 아니라 경영 상태 기반 발동으로 확장
- 침입 변수 선택을 완전 랜덤이 아니라 현재 위협 원인과 연결
- 이벤트 결과를 운영일 정산에 별도 카테고리로 더 자세히 표시
