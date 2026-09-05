# P1 4.1 운영일 정산 구현 보고

## 목표

운영이 실시간으로 흐르더라도 하루 단위로 플레이어가 결과를 되짚어볼 수 있게 만드는 것이 목표다.

정산은 강제 모달이 아니라 림월드식 이벤트 알림처럼 오른쪽/우하단 버튼으로 뜨고, 플레이어가 원할 때 상세 내용을 확인하는 구조로 잡았다.

## 구현 내용

### 운영일 이벤트

`GameManager`의 기존 180초 운영일 타이머에 하루 종료/시작 이벤트를 연결했다.

흐름:

```text
운영일 진행
-> 180초 경과
-> OperatingDayEndedEvent
-> day 증가
-> OperatingDayStartedEvent
```

`GameManager`는 세부 정산 데이터를 직접 모으지 않는다. 날짜 흐름만 발행하고, 정산 전용 런타임이 데이터를 수집한다.

### 정산 런타임

`OperatingDaySettlementRuntime`을 추가했다.

이 런타임은 하루 동안 다음 이벤트를 누적한다.

- `FacilityVisitEvent`: 손님 방문
- `FacilityRevenueEvent`: 매출 발생
- `FacilityStockConsumedEvent`: 시설 재고 소비
- `FacilityRestockEvent`: 보충 성공/실패
- `StockSupplyEvent`: 납품, 보상, 내부 생산 입고 결과

하루가 끝나면 `OperatingDayReport`를 만들고 `OperatingDayReportEvent`를 발행한다.

### 정산 보고서

`OperatingDayReport`는 정산 상세 패널에서 바로 표시할 수 있는 구조다.

포함 항목:

- 총 매출
- 시설별 매출
- 방문 손님 수
- 종족별 방문 수
- 평균 만족도
- 발생한 사고
- 파손 시설 목록
- 수리 비용 예상
- 직원 피로와 작업 요약
- 직원 불만 사건
- 재고 부족 시설
- 창고 재고 잔량
- 보충 실패 횟수
- 새로 해금된 도감 정보
- 상점 판매 목록 갱신 결과
- 재고 수급 결과
- 소비된 재고

아직 사고/도감 시스템은 없으므로 해당 항목은 빈 목록으로 표시된다. 슬롯은 먼저 만들어두었고, 이후 사고/도감 이벤트가 생기면 같은 보고서 필드에 연결하면 된다.

### 이벤트 상세 UI

초기 구현에서는 정산 전용 UI를 사용했지만, `4.3 이벤트 알림` 구현 이후 공용 `EventAlertRuntime`으로 통합했다.

동작:

```text
OperatingDayReportEvent 수신
-> OperatingDayReportAlertBridge
-> 우하단 이벤트 버튼 표시
-> 버튼 클릭
-> 오른쪽 상세 패널 토글
```

이 패널은 `UIManager`의 팝업 스택을 사용하지 않는다. 즉, 필수 모달이 아니며 플레이어 입력을 강제로 막지 않는다.

프리팹이 없어도 실행 중 Canvas 아래에 버튼과 패널을 만든다. 프로젝트의 한글 TMP 폰트가 있으면 에디터에서 자동 연결한다.

### 자동 연결

`GameManager.Awake`에서 다음 컴포넌트를 보장한다.

- `OperatingDaySettlementRuntime`
- `EventAlertRuntime`
- `OperatingDayReportAlertBridge`

씬마다 직접 붙이지 않아도 우선 동작하게 하기 위한 P1용 조립 방식이다. 나중에 UI 프리팹이 고정되면 serialized 참조로 교체할 수 있다.

## 구현 로직

매출은 실제 상점 상호작용 결과를 기준으로 기록한다.

```text
손님이 상점 이용
-> 구매 성공
-> 재고 1개 소비 이벤트
-> 매출 이벤트
-> GameData.holdingMoney 증가
```

방문 수와 만족도는 시설 이용 시작 시점에 기록한다.

```text
시설 TryBeginUse 성공
-> FacilityVisitEvent
-> Customer만 방문 손님 수에 반영
-> 현재 MOOD를 만족도 샘플로 저장
```

하루 종료 시점에는 현재 씬 상태도 함께 스캔한다.

```text
모든 BuildableObject 확인
-> 파손 시설
-> 재고 부족 상점
-> 창고 재고 잔량

모든 Character 확인
-> 직원 수
-> 근무 중 직원 수
-> 비번 직원 수
-> 평균 피로/기분
-> 기분 낮은 직원 사건
```

상점 판매 목록 갱신은 다음 날 납품 목록 생성으로 처리한다.

```text
하루 종료
-> StockSupplyService.CreateDailyDeliveryOffers(day + 1)
-> 보고서에 갱신 결과 기록
```

## 검증

Unity MCP로 운영일 정산 시나리오와 전체 P1 회귀 시나리오를 실행했다.

결과:

```text
Facility=True
Customer=True
Character=True
Owner=True
WorkPriority=True
Command=True
StaffDuty=True
Operation=True
```

검증한 내용:

- 운영일 보고서가 방문, 매출, 재고 소비, 보충 실패, 재고 수급 결과를 수집한다.
- 보고서가 다음 날 납품 목록을 생성한다.
- 직원 피로/기분 요약이 보고서에 들어간다.
- 정산 UI가 보고서를 받고 버튼/상세 패널을 토글한다.
- 콘솔 에러와 경고가 0개다.

## 다음 연결 지점

다음 단계는 `4.2 캐릭터 피드백`이다.

연결 방향:

- 캐릭터 머리 위 이모티콘 풍선
- 캐릭터 클릭 로그 UI
- 짧은 원인 태그 표시

이 작업이 붙으면 일일 정산은 큰 흐름을 보여주고, 캐릭터 로그는 개별 원인을 보여주는 구조가 된다.
