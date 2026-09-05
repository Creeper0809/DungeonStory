# P1 5.5 자동 전투 피드백 구현 보고

## 목표

자동 전투가 운처럼 보이지 않게, 침입 중 어떤 방어 시설이 작동했고 어떤 결과를 만들었는지 기록한다.

초기 구현에서는 침입 경로 리플레이를 만들지 않는다. 대신 전투 중 짧은 발동 표시와 침입 종료 후 결과 요약을 제공한다.

## 구현 파일

- `Assets/Scripts/Invasion/InvasionCombatReportSystem.cs`
- `Assets/Scripts/Invasion/Editor/InvasionCombatReportDebugScenarios.cs`
- `Assets/Scripts/GameManager.cs`
- `docs/game-design-todo.md`

## 구조

새 런타임은 `InvasionCombatReportRuntime`이다.

이 런타임은 다음 이벤트를 듣는다.

```text
InvasionStartedEvent
InvasionSpawnedEvent
DefenseFacilityTriggeredEvent
InvasionFacilityDamagedEvent
InvasionFinalCombatStartedEvent
InvasionResolvedEvent
```

침입이 시작되면 `InvasionCombatReport`를 만들고, 침입이 끝날 때까지 전투 기록을 모은다.

기록하는 항목:

```text
방어 시설별 총 피해
방어 시설별 최대 지연 시간
방어 시설 발동 로그
시설 파손 로그
최종 교전 여부
효과 태그 기반 관찰 정보
효과 태그 기반 연계 로그
```

## 전투 중 발동 표시

방어 시설이 발동하면 `DefenseActivationReport`를 받아 짧은 문구를 만든다.

예시:

```text
가시 함정 발동 / 피해 18 / 가시 피해
냉기 분사구 발동 / 피해 8 / 지연 1.5초 / 감속
```

이 문구는 두 곳으로 나간다.

```text
InvasionCombatFeedbackEvent
NoticeFeedEvent
```

`InvasionCombatFeedbackEvent`는 나중에 전투 전용 UI를 붙이기 위한 이벤트다. `NoticeFeedEvent`는 현재 P1에서 즉시 확인 가능한 짧은 화면 피드백이다.

## 침입 결과 요약

침입이 종료되면 기존 이벤트 알림 시스템으로 `침입 결과` 알림을 올린다.

즉, 별도 결과 UI를 새로 만들지 않고 이미 구현된 우하단/오른쪽 알림 버튼과 상세 패널을 사용한다.

요약 항목:

```text
방어 결과
잔여 위험도
가장 많은 피해를 준 시설
가장 오래 지연시킨 시설
결정적 방어
피해를 입은 시설
파손 시설
획득한 관찰 정보
작동한 연계
전투 중 발동
```

## 추천 대응을 넣지 않은 이유

전투 결과는 플레이어에게 `무엇을 하라`고 말하지 않는다.

이번 결과 화면은 다음 정보만 보여준다.

```text
무슨 시설이 발동했는가
얼마나 피해를 줬는가
얼마나 지연시켰는가
무엇이 파손됐는가
어떤 효과가 관찰됐는가
```

대응 힌트는 나중에 침략 도감/시설 도감에서 제공하는 쪽이 맞다. 전투 직후 화면은 공략 추천이 아니라 관측 기록이어야 한다.

## 결정적 방어 판정

초기 판정은 단순하게 둔다.

```text
최종 교전이 있었다면:
사장 최종 교전 생존/패배를 기록한다.

최종 교전이 없고 방어 성공했다면:
가장 많은 피해를 준 시설을 결정적 방어로 기록한다.

피해 기록이 없고 지연 기록이 있다면:
가장 오래 지연시킨 시설을 기록한다.
```

이 방식은 리플레이 없이도 전투의 핵심 장면을 짧게 추정하게 해준다.

## 연계 로그

효과 태그를 사용해 짧은 연계 로그를 만든다.

예시:

```text
경비실: 직원/경비 교전
번개 기둥: 번개 축전 방전
독 웅덩이: 독 피해와 부식
화염 분사구: 화염 피해와 연소
냉기 분사구: 냉기 피해와 이동 지연
```

이것은 정확한 공략 추천이 아니라 `내가 준비한 시설 조합이 실제로 작동했다`는 감각을 주기 위한 기록이다.

## GameManager 연결

`GameManager.EnsureOperatingDaySystems()`에서 `InvasionCombatReportRuntime`을 자동으로 추가한다.

따라서 별도 프리팹 수정 없이 플레이 모드에서 침입 전투 리포트 시스템이 붙는다.

## 검증

Unity MCP에서 5.5 전용 시나리오를 실행했다.

```text
DungeonStory/Debug/Invasion/Run P1 Combat Report Scenarios
```

검증 내용:

- 방어 시설 발동 시 `InvasionCombatFeedbackEvent`가 발생한다.
- 침입 종료 시 `침입 결과` 알림이 발생한다.
- 결과 상세에 가장 많은 피해, 가장 긴 지연, 피해 시설, 파손 시설, 관찰 정보, 발동 로그가 포함된다.
- 결과 상세에 추천 대응 문구가 들어가지 않는다.

실행 결과:

```text
CombatReport=True
```

전체 P1 회귀 검증도 통과했다.

```text
Grid=True
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
InvasionIntruder=True
Defense=True
CombatReport=True
```

Unity 콘솔 에러/워닝:

```text
0
```
