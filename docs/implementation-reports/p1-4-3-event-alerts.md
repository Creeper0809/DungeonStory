# P1 4.3 이벤트 알림 구현 보고

## 목표

침입 결과, 운영일 정산, 직원 불만, 설계도 획득 같은 중요한 사건이 서로 다른 UI로 흩어지지 않고 같은 알림 구조를 공유하게 만드는 것이 목표다.

알림은 강제 모달이 아니라 우하단 버튼과 오른쪽 상세 패널로 처리한다.

## 구현 내용

### 공용 이벤트 알림 모델

`EventAlertSystem.cs`를 추가했다.

핵심 타입:

- `EventAlertImportance`: 낮음, 중간, 높음
- `EventAlertChoice`: 선택 이벤트의 선택지
- `EventAlertRequest`: 새 알림 요청 데이터
- `EventAlertRecord`: 실제 이벤트 로그에 남는 기록
- `EventAlertRequestedEvent`: 알림 요청 이벤트
- `EventAlertLoggedEvent`: 알림이 로그에 남았음을 알리는 이벤트
- `EventAlertService`: 다른 시스템에서 쉽게 알림을 발행하는 helper

### 공용 이벤트 알림 UI

`EventAlertRuntime`이 우하단 버튼 목록과 오른쪽 상세 패널을 만든다.

동작:

```text
EventAlertRequestedEvent 수신
-> 이벤트 로그에 기록
-> 우하단 버튼 생성
-> 버튼 클릭
-> 오른쪽 상세 패널 열림
```

중요도별 버튼 색을 다르게 두었다.

```text
Low: 낮은 강조
Medium: 중간 강조
High: 높은 강조
```

같은 제목, 분류, 중요도, 상세 내용의 이벤트는 버튼을 새로 늘리지 않고 카운트를 올린다.

```text
직원 불만
직원 불만
-> 직원 불만 x2
```

### 선택 이벤트

선택 이벤트는 최대 3개 선택지를 지원한다.

선택지 구조:

```text
라벨
설명
선택 시 콜백
```

상세 패널 하단에 선택 버튼을 표시하고, 선택하면 콜백을 실행한 뒤 상세 패널을 닫는다.

### 운영일 정산 연결

`OperatingDayReportAlertBridge`를 추가했다.

운영일 정산이 끝나면 다음 흐름으로 공용 알림에 연결된다.

```text
OperatingDayReportEvent
-> Day N 정산 알림 생성
-> 우하단 이벤트 버튼 표시
-> 클릭 시 정산 상세 표시
```

직원 불만이 있는 경우에는 별도 `직원 불만` 알림도 발행한다.

### 이벤트 로그와 정산 연결

`OperatingDaySettlementRuntime`이 `EventAlertLoggedEvent`를 수집하게 했다.

즉, 하루 중 발생한 공용 이벤트 알림은 운영일 정산의 `이벤트 로그` 항목에 남는다.

이 구조 덕분에 다음 시스템도 같은 경로를 쓸 수 있다.

```text
침입 결과 -> EventAlertService.RaiseInvasionResult
설계도 획득 -> EventAlertService.RaiseBlueprintAcquired
직원 불만 -> EventAlertService.RaiseStaffComplaint
```

## 구조 정리

4.1에서 만들었던 정산 전용 `OperatingDayReportUI`는 제거했다.

현재 구조:

```text
운영일 정산
-> OperatingDayReportEvent
-> OperatingDayReportAlertBridge
-> EventAlertRuntime
```

정산은 이제 공용 이벤트 알림 시스템을 사용한다.

## 검증

Unity MCP로 이벤트 알림 시나리오와 전체 P1 회귀 시나리오를 실행했다.

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
Feedback=True
Alerts=True
```

검증한 내용:

- 이벤트 알림 버튼이 생성된다.
- 이벤트 클릭 시 상세 패널이 열린다.
- 같은 이벤트가 반복되면 `x2`로 병합된다.
- 선택 이벤트는 3개 선택지만 사용하고 콜백을 실행한다.
- 이벤트 로그가 운영일 정산에 남는다.

콘솔 에러는 0개다.

남아 있는 경고는 기존 경고다.

- `UIBuildingInfo.cs` unreachable code
- Behavior Designer deprecated API 경고

## 다음 연결 지점

다음 단계는 `5. 침입과 방어`다.

침입 결과 화면을 새로 만들기보다, 침입 결과 요약을 `EventAlertService.RaiseInvasionResult`로 발행하면 현재 알림 구조를 그대로 사용할 수 있다.
