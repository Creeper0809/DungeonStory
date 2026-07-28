# DungeonStory 게임 전체 문서화 계획

## 목표

현재 저장소의 코드, 씬, 프리팹, ScriptableObject, 프로젝트 설정, 기존 설계/감사 기록을 교차 분석해 다음 내용을 하나의 최신 Markdown 문서로 만든다.

- 실제 구현된 게임 기능
- 플레이어 관점의 전체 게임 루프와 하위 루프
- 코드와 데이터에서 드러나는 콘텐츠 기획
- 구현 완료도, 연결 상태, 미완성/위험 요소
- 주요 시스템과 근거 파일

## 산출물

- `docs/DungeonStory_Game_Design_and_Implementation.md`

## 단계

| 단계 | 범위 | 상태 |
|---|---|---|
| 1 | 기존 계획/감사 문서와 저장소 지침 복구 | 완료 |
| 2 | 런타임 C#·어셈블리·생명주기·이벤트 구조 인벤토리 | 완료 |
| 3 | 씬·프리팹·ScriptableObject·데이터 콘텐츠 인벤토리 | 완료 |
| 4 | 기능/게임 루프/기획 콘텐츠/완성도 종합 | 완료 |
| 5 | 단일 Markdown 문서 작성 | 완료 |
| 6 | 근거 경로·누락·문서 구조 검증 | 완료 |

## 결정

- 기존 루트 `task_plan.md`, `findings.md`, `progress.md`와 활성 계획은 수정하지 않는다.
- 문서는 코드상 존재와 실제 런타임 연결을 구분해 표현한다.
- 확인할 수 없는 의도는 사실처럼 단정하지 않고 `코드에서 추론되는 기획`으로 표시한다.
- `Library`, `Temp`, `obj`, 빌드 산출물, 외부 에셋 소스는 핵심 게임 로직 분석에서 제외하되 실제 사용 흔적은 필요할 때 확인한다.

## 오류 기록

| 오류 | 시도 | 해결 |
|---|---:|---|
| 광범위 타입 인벤토리 명령이 1,200줄 출력 후 종료 코드 1 | 1 | 출력 제한 때문에 DI 파일 읽기가 누락됨. 타입 검색을 도메인별로 분할하고 DI 파일은 정확한 경로 검색 후 별도로 읽는다. |
| 존재하지 않는 `GameRuntimeServices.cs`를 함께 조회해 명령 종료 코드 1 | 1 | `rg -l`로 실제 `DungeonRuntimeLifetimeScope.cs`만 확인하고 단독으로 읽었다. |
| `Assets/Scripts/Services/Buildings/Work/WorkOrderRuntime.cs` 경로가 존재하지 않음 | 1 | `rg -l 'class WorkOrderRuntime'`로 실제 위치를 다시 찾은 뒤 읽는다. 생산 bill/연구 queue 파일은 정상적으로 읽었다. |
| 진화 모델 파일명을 추정해 `EvolutionModels.cs`, `InstanceFacilityEvolutionModels.cs` 조회 실패 | 1 | `rg --files`와 타입명 검색으로 실제 파일명을 찾고, 확인된 `EvolutionModuleRegistry.cs` 내용은 보존한다. |
