# P1 5.1 위협도 시스템 구현 보고

## 목표

고정 날짜/웨이브가 아니라, 던전의 성장 상태와 시간에 반응해서 침입 가능성이 올라가는 내부 위협도 시스템을 구현했다.

이번 단계에서는 실제 침입자 생성까지 만들지 않고, 다음 단계가 붙을 수 있는 `침입 후보 이벤트`까지 구현했다.

## 구현 파일

- `Assets/Scripts/Invasion/InvasionThreatSystem.cs`
- `Assets/Scripts/Invasion/Editor/InvasionThreatDebugScenarios.cs`
- `Assets/Scripts/GameManager.cs`

## 핵심 구조

### 내부 위협도

`InvasionThreatRuntime`이 `currentThreat`를 내부 값으로 관리한다.

플레이어 UI에는 이 값을 게이지로 보여주지 않는다. 대신 특정 단계에서 알림만 발생한다.

```text
0-69%: 평상시
70-99%: 침입 경고 가능
100% 이상: 침입 후보 지연 시작
랜덤 지연 종료: InvasionCandidateEvent 발행
침입 시작: 위협도 초기화 + 안전 시간
```

## 위협도 상승 로직

위협도 상승량은 `InvasionThreatCalculator.CalculateRisePerSecond`에서 계산한다.

```text
위협도 상승량 =
  기본 상승량
+ 던전 가치 * 던전 가치 가중치
+ 평판 * 평판 가중치
+ 시간 * 시간 가중치
+ 위험도 * 위험도 가중치

최종 상승량 = 위 값 * 난이도 배율
```

### 던전 가치

현재 배치된 `BuildableObject`를 보고 계산한다.

반영 요소:

- 배치 시설 수
- 시설 유지비
- 시설 역할 태그 존재 여부

즉 시설이 많고 비싼 던전일수록 모험가의 관심을 더 끈다.

### 평판

현재 활성화된 손님 수와 손님 기분 샘플을 본다.

P1에서는 아직 깊은 평판 시스템이 없으므로, 손님이 많고 분위기가 좋을수록 소문이 퍼진다는 단순 모델로 시작했다.

### 시간

마지막 침입 이후 흐른 시간을 반영한다.

시간은 무한히 커지지 않도록 내부적으로 제한한다.

### 위험도

현재 취약한 운영 흔적을 반영한다.

반영 요소:

- 파손 시설
- 재고 부족 상점
- 침입 결과에서 넘어오는 잔여 위험도

## 난이도

`InvasionThreatDifficulty`를 추가했다.

```text
Easy
Normal
Hard
```

초기 배율:

```text
Easy: 0.65
Normal: 1.0
Hard: 1.45
```

난이도는 위협도 상승량 전체에 곱해진다. 그래서 높은 난이도는 던전 가치, 평판, 시간, 위험도의 영향을 모두 더 크게 만든다.

## 경고 이벤트

위협도가 70% 이상이 되면 `InvasionThreatWarningEvent`가 발생한다.

동시에 일반 이벤트 알림 시스템으로 `침입 경고` 알림을 올린다.

플레이어에게 보이는 내용은 수치가 아니라 간접 신호다.

예시:

```text
모험가들의 소문이 늘고 있습니다.
징후: 던전 가치 상승, 소문 증가
```

경고는 한 사이클에 한 번만 발생하고, `warningCooldownSeconds`로 반복 알림 폭주를 막는다.

## 침입 후보 이벤트

위협도가 100% 이상이 되면 즉시 침입을 생성하지 않는다.

먼저 `candidateDelayRemaining`을 설정한다.

```text
위협도 100% 이상
-> 랜덤 지연 시작
-> 지연 종료
-> InvasionCandidateEvent 발생
-> 침입 임박 알림 발생
```

P1 기본값:

```text
5-12초 랜덤 지연
```

이 이벤트는 5.2 침입자 시스템이 받아서 실제 침입자를 생성하는 연결점이다.

## 침입 시작 후 초기화

`InvasionStartedEvent`를 받으면 위협도 사이클을 초기화한다.

처리:

- 위협도 0으로 초기화
- 마지막 침입 이후 시간 0으로 초기화
- 경고/후보 상태 초기화
- 안전 시간 시작

안전 시간 중에는 위협도가 오르지 않는다.

이렇게 해서 침입 직후 바로 다음 침입이 붙는 상황을 막는다.

## 침입 결과 연결

`InvasionResolvedEvent`를 추가했다.

아직 실제 전투 결과는 없지만, 이후 방어 실패나 피해 정도를 `residualRisk`로 넘기면 다음 위협도 상승에 반영할 수 있다.

```text
방어 성공: 잔여 위험도 낮음
방어 실패: 잔여 위험도 증가
```

## GameManager 연결

`GameManager.EnsureOperatingDaySystems()`에서 `InvasionThreatRuntime`을 자동으로 붙인다.

따라서 씬에 별도 오브젝트를 만들지 않아도 기존 운영일/알림 시스템과 같이 동작한다.

## 왜 이렇게 구현했나

### 침입자를 바로 만들지 않은 이유

5.1의 목표는 위협도 시스템이다.

실제 침입자 생성, 사장방 목표 이동, 시설 파손은 5.2 범위다.

그래서 이번 단계는 `InvasionCandidateEvent`까지만 만들고 멈췄다. 이러면 다음 구현이 이벤트를 구독해서 자연스럽게 이어진다.

### 게이지 UI를 만들지 않은 이유

설계에서 위협도는 내부 값이고, 플레이어에게 직접 게이지로 보여주지 않기로 했다.

따라서 UI는 다음 두 이벤트 알림만 사용한다.

- 침입 경고
- 침입 임박

### 계산식을 단순하게 둔 이유

P1에서는 정확한 밸런스보다 작동 구조 검증이 중요하다.

계수는 모두 `InvasionThreatSettings`에 노출해 두었기 때문에, 실제 플레이 감각을 보면서 조정할 수 있다.

## 검증

Unity MCP에서 다음을 실행했다.

```text
DungeonStory/Debug/Invasion/Run P1 Threat Scenarios
```

검증 항목:

- 위협도 상승 요인 계산
- 경고 이벤트와 알림 요청
- 100% 이후 침입 후보 지연 이벤트
- 침입 시작 후 위협도 초기화와 안전 시간

추가로 기존 P1 회귀 검증도 함께 통과했다.

```text
P1 regression:
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
```

## 다음 단계

다음은 `5.2 침입자 1종 구현`이다.

연결 방식:

```text
InvasionCandidateEvent
-> 침입자 스폰
-> 사장방/사장 목표 선택
-> 그리드 이동
-> 시간 경과에 따른 목표 방향 보정
-> 사장 도달 시 최종 교전
```
