# 구현 증거 매트릭스

이 문서는 `docs/game-design-todo.md`의 1.1 이후 항목을 보고서, 디버그 시나리오, 현재 검증 상태와 연결한다.

최종 완료 판정은 이 표와 `Temp/DungeonStoryImplementedScenarioReport.txt`를 함께 보고 결정한다.

## 검증 상태 기준

- 문서화됨: 구현 보고서와 로직 요약이 존재한다.
- 시나리오 있음: 통합 러너에서 호출되는 디버그 시나리오가 존재한다.
- Unity MCP 통합 검증 통과: Unity MCP에서 `ImplementedScenarioDebugRunner.RunAll(true)`가 성공했고 리포트에 `[PASS]`로 기록됐다.

## P1 캐릭터 AI 계획

| 항목 | 구현 보고서 | 디버그 시나리오 | 현재 상태 |
|---|---|---|---|
| Behavior Designer + Utility AI + Local LLM 계획 | [plan.md](../game-design/plan.md) | `CharacterAiPlanDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |

## P1 경영 루프

| 항목 | 구현 보고서 | 디버그 시나리오 | 현재 상태 |
|---|---|---|---|
| 1.1 초기 시설 세트 | [p1-1-initial-facilities.md](p1-1-initial-facilities.md) | `FacilityDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 1.2 손님 AI | [p1-2-customer-ai.md](p1-2-customer-ai.md) | `CustomerAiDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 1.3 캐릭터 모델 | [p1-3-character-model.md](p1-3-character-model.md) | `CharacterModelDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 1.4 종족 1차 구현 | [p1-4-species.md](p1-4-species.md) | `CharacterModelDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 1.5 사장 캐릭터 | [p1-5-owner-character.md](p1-5-owner-character.md) | `OwnerDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |

## P1 직원과 작업

| 항목 | 구현 보고서 | 디버그 시나리오 | 현재 상태 |
|---|---|---|---|
| 2.1 작업 우선순위 | [p1-2-1-work-priority.md](p1-2-1-work-priority.md) | `WorkPriorityDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 2.2 우선 지정 | [p1-2-2-priority-command.md](p1-2-2-priority-command.md) | `PriorityCommandDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 2.3 근무와 비번 | [p1-2-3-duty-off-duty.md](p1-2-3-duty-off-duty.md) | `StaffDutyDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |

## P1 재고와 운영일

| 항목 | 구현 보고서 | 디버그 시나리오 | 현재 상태 |
|---|---|---|---|
| 3.1 재고 계열 | [p1-3-1-inventory-logistics.md](p1-3-1-inventory-logistics.md) | `FacilityDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 3.2 초반 수급 | [p1-3-2-early-stock-supply.md](p1-3-2-early-stock-supply.md) | `FacilityDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 4.1 운영일 정산 | [p1-4-1-operating-day-settlement.md](p1-4-1-operating-day-settlement.md) | `OperatingDaySettlementDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 4.2 캐릭터 피드백 | [p1-4-2-character-feedback.md](p1-4-2-character-feedback.md) | `CharacterFeedbackDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 4.3 이벤트 알림 | [p1-4-3-event-alerts.md](p1-4-3-event-alerts.md) | `EventAlertDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |

## P1 침입과 방어

| 항목 | 구현 보고서 | 디버그 시나리오 | 현재 상태 |
|---|---|---|---|
| 5.1 위협도 시스템 | [p1-5-1-invasion-threat.md](p1-5-1-invasion-threat.md) | `InvasionThreatDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 5.2 침입자 1종 | [p1-5-2-breakthrough-intruder.md](p1-5-2-breakthrough-intruder.md) | `InvasionIntruderDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 5.3 초기 방어 시설 | [p1-5-3-defense-facilities.md](p1-5-3-defense-facilities.md) | `DefenseFacilityDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 5.4 전투 효과 구조 | [p1-5-4-combat-effects.md](p1-5-4-combat-effects.md) | `DefenseFacilityDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 5.5 자동 전투 피드백 | [p1-5-5-auto-combat-feedback.md](p1-5-5-auto-combat-feedback.md) | `InvasionCombatReportDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |

## P1 연구와 합성

| 항목 | 구현 보고서 | 디버그 시나리오 | 현재 상태 |
|---|---|---|---|
| 6.1 상점과 기본 구매 | [p1-6-1-facility-shop-and-basic-purchase.md](p1-6-1-facility-shop-and-basic-purchase.md) | `FacilityShopDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 6.2 연구시설과 설계도 | [p1-6-2-blueprint-research.md](p1-6-2-blueprint-research.md) | `BlueprintResearchDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 6.3 시설물끼리의 합성 | [p1-6-3-facility-synthesis.md](p1-6-3-facility-synthesis.md) | `FacilitySynthesisDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 6.4 대표 합성 트리 | [p1-6-4-representative-synthesis-tree.md](p1-6-4-representative-synthesis-tree.md) | `FacilitySynthesisDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 6.5 시설 계보 진화 | [p1-6-5-facility-lineage-evolution.md](p1-6-5-facility-lineage-evolution.md) | `FacilityEvolutionDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 7 도감과 정보 해금 | [p1-7-codex-and-information-unlock.md](p1-7-codex-and-information-unlock.md) | `CodexDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |

## P2 내부 위협과 런

| 항목 | 구현 보고서 | 디버그 시나리오 | 현재 상태 |
|---|---|---|---|
| 8.1 단골과 영입 | [p2-8-1-regular-customer-recruitment.md](p2-8-1-regular-customer-recruitment.md) | `RegularCustomerDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 8.2 직원 불만 단계 | [p2-8-2-staff-discontent.md](p2-8-2-staff-discontent.md) | `StaffDiscontentDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 8.3 반란 대응 | [p2-8-3-rebellion-response.md](p2-8-3-rebellion-response.md) | `StaffRebellionResponseDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 9 런 변수와 이벤트 | [p2-9-run-variables-and-events.md](p2-9-run-variables-and-events.md) | `RunVariableDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 10 런 종료와 계승 | [p2-10-run-end-and-meta-progression.md](p2-10-run-end-and-meta-progression.md) | `MetaProgressionDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |

## P3 오펜스

| 항목 | 구현 보고서 | 디버그 시나리오 | 현재 상태 |
|---|---|---|---|
| 11.1 월드맵과 정찰 | [p3-11-1-offense-world-map.md](p3-11-1-offense-world-map.md) | `OffenseWorldMapDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 11.2 원정 편성 | [p3-11-2-offense-expedition.md](p3-11-2-offense-expedition.md) | `OffenseExpeditionDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |
| 11.3 오펜스 보상 | [p3-11-3-offense-rewards.md](p3-11-3-offense-rewards.md) | `OffenseRewardDebugScenarios` | 문서화됨, 시나리오 있음, Unity MCP 통합 검증 통과 |

## 통합 검증

통합 실행 메뉴:

```text
DungeonStory/Debug/Run All Implemented Scenarios
```

batchmode 자동 검증 진입점:

```text
ImplementedScenarioDebugRunner.RunForBatchMode
```

결과 파일:

```text
Temp/DungeonStoryImplementedScenarioReport.txt
Temp/DungeonStoryImplementedScenarioReport.json
```

현재 통합 러너는 29개의 시나리오 묶음을 모두 호출한다.

최근 Unity MCP 통합 검증:

```text
Generated: 2026-07-04 06:19:15
Suites: 29
Passed: 29
Failed: 0
Console errors: 0
Console warnings: 9
```

최종 완료 판정 조건:

```text
1. 통합 텍스트 결과 파일에서 모든 묶음이 [PASS]로 표시된다.
2. 통합 JSON 결과 파일에서 success가 true, failed가 0으로 표시된다.
3. Play Mode에서만 증명 가능한 항목은 별도 Play Mode probe 결과로 갱신된다.
4. current-verification-audit.md의 미검증 상태가 해소된다.
```
