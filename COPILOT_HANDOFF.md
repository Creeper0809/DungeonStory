# DungeonStory Copilot 마무리 인수인계

## 1. 목표

현재 구현을 다시 설계하지 말고, 남아 있는 **최종 검증 계약 연결과 Unity-loaded 인수 테스트**를 끝낸다.

완료 판정에는 아래 증거가 모두 필요하다.

- Architecture EditMode `131/131`
- Transactional Restore `33/33`
- Synchronous Final Acceptance `33/33`
- Full World 저장 섹션 `54/54`, canonical baseline 복원 성공
- Final PlayMode 대상 `7개`, fresh 캡처 `30개`
- 필수 해상도 `1600x900`, `900x1600`
- 장비 시설 흐름 마커 `FACILITY_FLOW=RF42,RF43,RF44,I17,I18`
- 최종 Unity Console `Error 0 / Warning 0`

## 2. 반드시 지킬 범위와 안전 규칙

- 저장 루트 V18은 동결한다. 실제 round-trip/atomicity 결함이 나오지 않으면 저장 구조를 다시 뜯지 않는다.
- `Assembly-CSharp` 파일 수 0은 완료 조건이 아니다. Phase 117의 위험 기반 분리 계약이 현재 권위다.
- 현재 dirty worktree는 기존 작업물이다. `git reset --hard`, `git checkout --`, `git clean`을 사용하지 않는다.
- Unity `.asset`, `.prefab`, `.unity`, `.meta`의 trailing whitespace를 일괄 정리하지 않는다. 현재 Unity 직렬화 파일의 공백 진단은 별도 부채이며 이번 마감 범위가 아니다.
- 운영체제 마우스/키보드 자동화를 사용하지 않는다. UI 검증은 Unity EventSystem과 Unity MCP만 사용한다.
- Unity에 `Scene(s) Have Been Modified`가 뜨면 자동으로 Save/Discard하지 말고 사용자에게 요청한다.
- `.vscode/mcp.json`은 프로젝트 로컬 Unity MCP 설정이다. 한 번에 직접 연결 클라이언트 하나만 사용한다.
- 모든 하위 Codex 에이전트는 종료됐다. 현재 Copilot과 파일을 동시에 수정하는 에이전트는 없다.

## 3. 이미 완료된 구현

- SO/콘텐츠 카탈로그 단일 권위와 런타임 콘텐츠 합성 제거
- V18 저장 구조와 staged restore/rollback 구조
- 물리 아이템, 창고 조회, 전투 장비, 개량 부품의 단일 상태 권위
- RF42 감정대, RF43 복원대, RF44 정밀 장착대, I17 룬 조율실, I18 계보 기록실
- 시설 로컬 버퍼 기반 감정/복원/장착/제거/계보 이전
- `item:equipment-module` 독립 물리 아이템 저장/복원/유실
- 168 연구, 생산 분기, 연료/사료, 장비 잠금, 화약무기 역할 회귀
- Full World 54-section 검증기와 canonical baseline 복원 검사
- 7개 PlayMode 대상과 현재 합계 30개 캡처 경로
- 장비 UI 보고서의 시설 마커 및 coordinator 필수 마커 검사
- 최신 오프라인 ArchitectureMetrics hard gate 0건
- 최신 오프라인 Foundation -> Items -> Combat -> Runtime -> Editor 컴파일 진단 0건
- meta 누락 0, GUID 중복 0

## 4. 방금 반영됐지만 fresh loaded 검증이 필요한 변경

### CharacterId 형식 강제

`Assets/Scripts/Services/Foundation/PersistentEntityIds.cs`

- `CharacterId.IsValid`는 이제 `owner` 또는 `character:*`만 허용한다.
- 임의 이름형 ID와 다른 타입 prefix는 거부한다.

관련 회귀가 다음 파일에 추가/보정됐다.

- `Assets/Scripts/Services/Infrastructure/Editor/PersistentIdentityDebugScenarios.cs`
- `Assets/Scripts/Services/Items/Editor/RuntimeAuthorityV18Validator.cs`
- `Assets/Scripts/Services/Infrastructure/Editor/CharacterProgressionSavePlayModeFacade.cs`
- `Assets/Scripts/Services/Combat/Editor/CombatSystemDebugScenarios.cs`

확인할 계약:

- `owner` PASS
- `character:fixture` PASS
- `name-like` FAIL
- `building:fixture` FAIL
- 저장 복원에서 잘못된 CharacterId가 live state를 바꾸지 않고 실패

### 정확한 테스트 개수 고정

다음 변경은 소스에 들어갔지만 최종 coordinator와 아직 연결되지 않았다.

- `Assets/Tests/EditMode/ArchitectureTestBatchRunner.cs`
  - `ExpectedTestCount = 131`
  - `RunForFinalGate(out string detail)`
  - 발견 수와 PASS 수가 모두 정확히 131이어야 성공
- `Assets/Tests/EditMode/TransactionalRestoreTestRunner.cs`
  - `ExpectedTestCount = 33`
  - `RunForFinalGate(out string detail)`
  - 발견 수와 PASS 수가 모두 정확히 33이어야 성공
- `Assets/Scripts/Editor/DungeonStoryFinalAcceptanceRunner.cs`
  - `ExpectedAcceptanceStepCount = 33`
  - 실제 step 수가 정확히 33이어야 성공

## 5. 남은 코드 작업

### A. 131/33/33을 Final PlayMode 요청의 필수 선행조건으로 연결

주 파일:

- `Assets/Scripts/Editor/DungeonFinalPlayModeAcceptanceRequestFacade.cs`

`RequestRunFromMenu()`가 request 파일을 만들기 전에 아래 세 검사를 현재 실행에서 직접 수행해야 한다.

1. `ArchitectureTestBatchRunner.RunForFinalGate(out detail)` -> true
2. `TransactionalRestoreTestRunner.RunForFinalGate(out detail)` -> true
3. `DungeonStoryFinalAcceptanceRunner.RunAll(true)` -> true

`DungeonStory.Architecture.Tests.asmdef`는 `autoReferenced: false`이고 별도 Editor test assembly다. 기본 Editor assembly에서 직접 참조할 수 없다면 reflection을 사용한다.

- assembly 이름: `DungeonStory.Architecture.Tests`
- type:
  - `DungeonStory.Tests.Architecture.ArchitectureTestBatchRunner`
  - `DungeonStory.Tests.Architecture.TransactionalRestoreTestRunner`
- method: `RunForFinalGate(out string detail)`
- assembly/type/method 누락, invocation exception, false 결과는 모두 FAIL 처리한다.
- stale artifact를 읽어서 대신 통과시키지 않는다. 현재 메서드를 직접 호출한 결과가 권위다.
- 세 검사 중 하나라도 실패하면 `RequestPath`와 `StatePath`를 만들지 않는다.
- 실패 상세를 Console Error와 별도 preflight report에 남긴다.

### B. Final PlayMode 계약을 정확히 7개 대상/30개 캡처로 고정

같은 coordinator에 fail-fast contract validation을 추가한다.

- `ExpectedTargetCount = 7`
- `ExpectedCaptureCount = 30`
- 대상 이름과 캡처 수:
  - `ResolutionMatrix`: 15
  - `FullWorldRoundTrip`: 0
  - `ResearchTree`: 3
  - `Production`: 2
  - `ServiceRoom`: 2
  - `CharacterSummaryMedical`: 4
  - `EquipmentExpeditionUiMatrix`: 4
- 모든 캡처 경로는 비어 있지 않고 전체에서 중복이 없어야 한다.
- 필수 캡처 집합에서 `1600x900`, `900x1600`을 모두 강제한다.
- Research/Production/ServiceRoom/Character/Equipment 대상이 각자 의도한 두 해상도 증거를 계속 제공하는지 확인한다.
- 위 계약이 틀리면 persistence snapshot이나 scene switch 전에 즉시 실패한다.

### C. Coordinator-wide Console 0/0을 fail-closed로 연결

현재 개별 verifier는 자기 실행 구간의 Warning/Error를 검사하지만, scene switch와 runner `Awake` 사이 로그를 놓칠 수 있다.

최종 coordinator가 요청 시작부터 완료 직전까지 전역으로 다음을 수집해야 한다.

- `LogType.Warning`
- `LogType.Error`
- `LogType.Exception`
- `LogType.Assert`

도메인 reload와 PlayMode 전환을 견디도록 파일 버퍼를 사용한다. Full World facade의 early Console buffer 구현을 참고한다.

- 시작 전에 이전 buffer 삭제
- active marker가 있을 때만 `Application.logMessageReceived` 내용을 append
- domain reload 후 `[InitializeOnLoad]` static constructor가 다시 구독
- 최종 report를 쓰기 전에 warning/error 수를 계산
- 하나라도 있으면 최종 PASS를 FAIL로 변경
- report에 `consoleWarnings`, `consoleErrors`, 메시지 preview 기록
- coordinator 자신의 최종 성공/실패 로그를 buffer에 다시 넣지 않도록 active marker를 먼저 해제

### D. 기존 131개 NUnit 수를 함부로 늘리지 않기

새 `[Test]`를 추가하면 Architecture expected count도 바뀐다. 가능하면 기존 architecture ratchet test 안에 새 coordinator invariant assertion을 추가한다.

- 테스트 삭제/추가로 131이 바뀌면 단순히 상수만 맞추지 말고 왜 계약이 바뀌는지 검토한다.
- 이번 마감 목표는 131을 유지하는 것이다.

## 6. 현재 stale 산출물

아래 파일은 최종 증거로 사용할 수 없다.

- `Artifacts/QA/architecture-editmode-report.txt`: 현재 없음
- `Artifacts/QA/transactional-restore-editmode-report.txt`
  - stale
  - `startedTestCases=136`, `pass=33`
- `Artifacts/QA/final-acceptance-report.txt`
  - 이전 `33/33` PASS지만 최신 CharacterId/count guard 이전 결과
- `Artifacts/QA/final-playmode-acceptance-report.txt`
  - 이전 ResolutionMatrix FAIL 결과

모두 최신 소스 컴파일 후 새로 생성해야 한다.

## 7. Unity 실행 및 검증 순서

### 7.1 연결 확인

`.vscode/mcp.json`의 프로젝트 로컬 `unity-mcp`를 사용한다.

Unity MCP에서 먼저 다음을 확인한다.

- 프로젝트가 DungeonStory인지
- active scene이 `Assets/Scenes/TitleScene.unity`인지
- scene dirty가 false인지
- compiling/playing이 false인지

다른 프로젝트/Unity instance에 연결됐으면 작업하지 않는다. PID는 재시작 때 바뀌므로 코드나 설정에 고정하지 않는다.

### 7.2 fresh refresh/compile

PowerShell helper를 사용할 경우:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File tools/unity-mcp/Invoke-ProjectRefresh.ps1 `
  -WaitSeconds 20
```

Unity가 import/compile/domain reload를 마칠 때까지 poll한다. 그 뒤 Console을 clear하고 새 로그만 검사한다.

필요하면 Unity 메뉴에서 explicit content catalog를 다시 빌드한다.

```text
Tools/DungeonStory/Content/Rebuild Explicit Content Catalog
```

SO/YAML을 손으로 대량 수정하지 않는다.

### 7.3 Architecture 131

Unity 메뉴:

```text
DungeonStory/Debug/Architecture/Run EditMode Tests
```

필수 report:

```text
Artifacts/QA/architecture-editmode-report.txt
ARCHITECTURE_EDITMODE RESULT=PASS
tests=131
expectedTests=131
pass=131
fail=0
```

### 7.4 Transactional 33

Unity 메뉴:

```text
DungeonStory/Debug/Architecture/Run Transactional Restore Tests
```

필수 report:

```text
Artifacts/QA/transactional-restore-editmode-report.txt
started=true
completed=true
expectedTestCases=33
startedTestCases=33
pass=33
fail=0
skip=0
```

`startedTestCases=136`인 기존 파일은 stale이므로 실패로 취급한다.

### 7.5 Synchronous Final 33

PowerShell helper:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File tools/unity-mcp/Invoke-FinalAcceptance.ps1 `
  -WaitSeconds 60
```

또는 Unity 메뉴:

```text
DungeonStory/QA/Run Final Acceptance
```

필수 report:

```text
Artifacts/QA/final-acceptance-report.txt
DungeonStory final acceptance passed.
ExpectedSteps: 33
ActualSteps: 33
Passed: 33
Failed: 0
```

### 7.6 Final PlayMode 7/30

Unity 메뉴:

```text
DungeonStory/QA/Request Final PlayMode Acceptance
```

상태는 `DungeonFinalPlayModeAcceptanceRequestFacade.GetStatusForMcp()`를 Unity MCP `Unity_RunCommand`로 주기적으로 호출해 확인한다. 운영체제 입력 자동화를 사용하지 않는다.

필수 target:

1. ResolutionMatrix
2. FullWorldRoundTrip
3. ResearchTree
4. Production
5. ServiceRoom
6. CharacterSummaryMedical
7. EquipmentExpeditionUiMatrix

필수 최종 report:

```text
Artifacts/QA/final-playmode-acceptance-report.txt
FINAL_PLAYMODE_ACCEPTANCE RESULT=PASS
persistenceRestoredNow=True
```

추가 확인:

- target PASS 7개
- fresh/non-empty capture 30개
- `1600x900`, `900x1600`
- `FACILITY_FLOW=RF42,RF43,RF44,I17,I18`
- `Artifacts/QA/full-world-round-trip-playmode-report.txt`에서:
  - `RESULT=PASS`
  - section count 54
  - canonical baseline matched
  - Console warnings 0 / errors 0
- `Artifacts/QA/equipment-expedition-ui-matrix-report.txt`에서 시설 마커와 두 해상도 PASS
- coordinator-wide `consoleWarnings=0`, `consoleErrors=0`

### 7.7 최종 Unity Console

PlayMode suite 완료 뒤 Unity MCP로 Console을 조회한다.

- Error 0
- Warning 0

stale 로그가 아니라 suite 시작 뒤 로그를 기준으로 판정한다.

## 8. 정적 최종 검사

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File Tools/ArchitectureMetrics/Run-ArchitectureMetrics.ps1 `
  -Verify
```

필수 hard gate:

- mutableStatics 0
- oversizedTypes 0
- largeConstructors 0
- crossDomainCycleCandidates 0
- contentEscapes 0
- directSessionMutations 0

추가 검사:

- C# compile diagnostics 0
- missing meta 0
- duplicate GUID 0
- `item:equipment-module` item catalog 등록 1회
- RF42/RF43/RF44/I17/I18 domain catalog 등록 각 1회
- 변경한 C# 파일에 대해 scoped `git diff --check`

Unity serialized YAML의 기존 trailing whitespace 때문에 global `git diff --check`를 완료 조건으로 사용하지 않는다.

## 9. 완료 후 문서 동기화

실제 fresh 결과를 확인한 뒤에만 아래 문서를 갱신한다.

- `task_plan.md`
- `findings.md`
- `progress.md`

기록할 것:

- 실행 시각
- 각 report 경로
- 131/131, 33/33, 33/33
- 7 targets / 30 captures
- Full World 54/54
- Console 0/0
- 실패가 있었다면 원인과 수정 파일

## 10. Copilot에 붙여넣을 작업 지시

```text
이 프로젝트의 COPILOT_HANDOFF.md를 먼저 끝까지 읽고 현재 worktree를 권위로 삼아라.

남은 마무리만 수행해라. V18 저장 구조와 완료된 SO/아이템/장비 구조를 다시 리팩터링하지 말고, Assembly-CSharp 파일 수 0을 목표로 삼지 마라. dirty worktree를 reset/clean하지 말고 Unity YAML/meta whitespace를 일괄 정리하지 마라.

우선 현재 반영된 CharacterId strict validation과 Architecture 131 / Transactional 33 / Final 33 exact-count 변경을 fresh Unity compile로 확인해라. 그 다음 DungeonFinalPlayModeAcceptanceRequestFacade에 다음을 구현해라:

1) ArchitectureTestBatchRunner.RunForFinalGate, TransactionalRestoreTestRunner.RunForFinalGate, DungeonStoryFinalAcceptanceRunner.RunAll을 현재 실행에서 직접 호출하는 fail-closed preflight. test assembly/type/method 누락도 실패해야 한다.
2) target 정확히 7개, capture 정확히 30개, 중복 경로 0, 1600x900 및 900x1600 필수 계약.
3) 요청 시작부터 종료까지 domain reload와 scene switch 사이를 포함하는 coordinator-wide Console Warning/Error/Exception/Assert 파일 버퍼. 하나라도 있으면 최종 PASS 금지.

기존 NUnit [Test] 수 131은 유지하고 가능하면 기존 ratchet test에 assertion을 보강해라. 운영체제 마우스/키보드 자동화는 사용하지 말고 Unity MCP와 Unity EventSystem만 사용해라. dirty scene modal이 생기면 자동 Save/Discard하지 말고 사용자에게 알려라.

수정 후 순서대로 fresh Unity refresh/compile, Architecture 131/131, Transactional 33/33, Final 33/33, Full World 54-section, Final PlayMode 7 targets/30 fresh captures, 1600x900+900x1600, FACILITY_FLOW marker, Console Error 0/Warning 0을 실제 report와 Unity Console로 증명해라. stale report는 증거로 쓰지 마라. 완료 후 task_plan.md/findings.md/progress.md를 실제 증거로 동기화해라.
```
