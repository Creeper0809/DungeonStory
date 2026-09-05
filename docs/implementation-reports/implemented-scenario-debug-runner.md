# 통합 디버그 시나리오 러너 구현 보고

## 구현 파일

- `Assets/Scripts/Editor/ImplementedScenarioDebugRunner.cs`

## 목표

P0부터 P3 11.3까지 흩어져 있는 디버그 시나리오를 한 번에 실행할 수 있게 했다.

Unity MCP나 수동 에디터 검증을 할 때 개별 메뉴를 하나씩 누르지 않아도 전체 구현 범위를 빠르게 확인하기 위한 장치다.

## 실행 메뉴

```text
DungeonStory/Debug/Run All Implemented Scenarios
DungeonStory/Debug/Open Last Implemented Scenario Report
```

batchmode 자동 검증 진입점:

```text
ImplementedScenarioDebugRunner.RunForBatchMode
```

이 진입점은 모든 시나리오가 통과하면 exit code `0`, 하나라도 실패하거나 예외가 나면 exit code `1`로 Unity Editor를 종료한다.

## 실행 흐름

```text
P1 방어/상점/합성 에셋 보정
-> P0 Grid foundation 시나리오
-> P1 Behavior Designer + Utility AI + Local LLM 계획 시나리오
-> P1 경영/AI/침입/연구/합성/시설 계보 진화/도감 시나리오
-> P2 영입/내부 위협/런 변수/메타 진행 시나리오
-> P3 오펜스 월드맵/원정/보상 시나리오
-> 전체 성공/실패 요약 출력
```

## 결과 파일

메뉴 실행 시 콘솔 로그와 함께 다음 파일에 같은 요약을 저장한다.

```text
Temp/DungeonStoryImplementedScenarioReport.txt
Temp/DungeonStoryImplementedScenarioReport.json
```

실패가 섞여 있어도 전체 묶음을 끝까지 실행한 뒤 `[PASS]`, `[FAIL]` 목록을 남긴다.

저장된 결과는 `DungeonStory/Debug/Open Last Implemented Scenario Report` 메뉴로 바로 열 수 있다.

각 묶음에는 실행 시간과 실패 상세도 함께 남긴다.

```text
[PASS] P1 Customer AI (12ms)
[FAIL] P1 Facility synthesis (4ms) / InvalidOperationException: ...
```

JSON 리포트는 자동 검증에서 `success`, `passed`, `failed`, `results[]`를 기계적으로 확인하기 위한 파일이다.

## 설계 의도

실패가 나도 즉시 중단하지 않고 다음 시나리오를 계속 실행한다.

이렇게 하면 한 시스템이 깨져도 나머지 시스템 상태를 같이 볼 수 있고, 마지막 요약에서 어떤 묶음이 실패했는지 확인할 수 있다.

## 현재 한계

이 러너는 Unity Editor 안에서 실행해야 한다.

batchmode 실행은 프로젝트가 이미 Unity Editor에 열려 있으면 동시 실행 제한에 걸릴 수 있다.

따라서 자동화 환경에서는 Editor 인스턴스 상태를 정리한 뒤 `ImplementedScenarioDebugRunner.RunForBatchMode`를 호출해야 한다.

## 최근 검증

```text
Generated: 2026-07-04 06:19:15
Suites: 29
Passed: 29
Failed: 0
P1 Plan character AI: PASS
P1 Facility evolution: PASS
P1 Codex: PASS
Console errors: 0
Console warnings: 9
```
