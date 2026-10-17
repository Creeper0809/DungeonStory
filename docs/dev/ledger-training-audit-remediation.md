# 원장·학습 입력 연결 재감사 수정 실행 설계 — Unity

작성일: 2026-09-26 · 버전: 설계 r1 · 상태: **구현·검증 진행 중 / 전체 미완**.

이 문서는 [source/replay 감사](../../Artifacts/QA/WorkProductAudit-20260926-r1/unity-source-replay-review.md)의 5개 결함과 [B0/B1 증거 감사](../../Artifacts/QA/WorkProductAudit-20260926-r1/unity-evidence-review.md)를 닫는 Unity 구현 절차다. AI 쪽 전체 순서와 생성·검수·학습 게이트는 [AI 수정 실행 설계](C:/Users/vulpo/Documents/DungeonStoryNarrativeAI/docs/AUDIT_REMEDIATION_IMPLEMENTATION_PLAN.md)를 따른다. 과거 계획의 게임 행동 수정 분모는 유지한다.

문서 작성은 수정·시험·학습 승인이 아니다. 아래 `U0~U6`는 전체 완료되지 않았으며 최신 실제 수용 범위는 루트 `HANDOFF.md`, `task_plan.md`, `progress.md`를 따른다. 기존 기능의 완료 여부는 요구 범위에 맞는 증거로 판정한다.

## 1. 목표와 고정 경계

목표는 “계획 노드 → 실제 행동 → 실제 원장/상태 → 그 시점의 실제 기능 요청 → 모델 선택 → C# 확정/효과 → 저장”을 같은 캠페인·주체의 계보로 연결하는 것이다.

- `NarrativeMechanicCatalog`는 정의·합법성 계약이지 플레이 원장이나 캐릭터 저장 파일이 아니다. 카탈로그만 고정했다고 장기 실행 입력이 연결된 것으로 세지 않는다.
- 통제 실행도 학습 원본이 될 수 있다. 다만 상태를 이어 실제 게임 명령을 실행한 `controlled-campaign`과 독립 `integration-fixture`를 구분한다. 자연 플레이 관측으로 표기하지 않는다.
- SO 정의/Runtime 상태/저장 DTO 권위를 유지한다. 문구·Python·Editor helper에서 능력치나 사망을 임의 보정하지 않는다.
- 구 r6/r8, source r33, replay r13 및 모든 실패 증거는 불변 보존한다. 새 revision으로만 교정하고 기존 승인/완료 verdict·flag를 자동 승계하지 않는다. 해시 고정된 과거 underlying artifact는 U0 영향 분석 뒤 새 matrix가 근거로 참조할 수 있다.
- 모델 계약은 현행 protocol 3의 여섯 필드다. 원래의 `combinationId`/`selectedIndex` 계약으로 되돌리거나 모델에 수치 배분을 맡기지 않는다.
- 실행 담당은 메인 한 명이다. SOURCE_EDIT/UNITY_VERIFY를 분리하고 Windows Unity는 `Tools/QA/run_isolated_unity_batch.ps1`의 비대화형 station/desktop 프로브를 통과한 격리에서만 직렬 실행한다. 숨김 창·`-batchmode -nographics`·BelowNormal만으로 포커스 비간섭을 충족하지 않는다. 일반 Unity 실행·사용자 데스크톱 전환·브라우저/마우스 자동화·Unity MCP·dungeon-player는 금지한다. 시험 runner의 종료 방식에 맞춰 `-quit`를 사용하며 PlayMode 시험이 끝나기 전 강제 종료로 PASS를 만들지 않는다. 현재 Unity 권위와 실행 조건은 이 저장소의 루트 지침·인수인계·계획을 따른다.

## 2. 구현 지도와 의존성

아래 경로는 이 Unity 저장소 기준이다. 신규 타입명은 권장 책임명이며 불필요하게 별도 프레임워크를 만들라는 뜻이 아니다.

| 단계 | 수정 대상 | 선행 | 완료 게이트 |
|---|---|---|---|
| U0 증거 분모 | `Tools/Documentation/verify_v25_narrative_mechanic_handoff.py`, B1 evidence index | 없음 | 91행 요구·증거·영향 범위 판정 |
| U1 생사/owner | `Assets/Scripts/Services/Character/AI/Editor/CharacterCombatModuleSelectionSourceCapture.cs`의 SelectionActor/capture/replay | 없음 | 사망·부상·기억·성장이 하나의 복원 상태 |
| U2 시간 | `Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/MigratedProducerCombatLifeCycleCausalDebugScenarios.cs`, source row validator | U1과 병행 가능 | 실제 발생·저장·전달 시점 일관 |
| U3 연속 실행 intake | `LongHorizonNarrativeExecutionExporter.cs`, `ProductionCausalPlanBatchExporter.cs`, `FormulaPresentationProductionSourceV8Exporter.cs` | U1/U2 | 계획이 아니라 실제 원장/요청을 export |
| U4 의미 replay | `GameplayModuleSelectionProductionReplayExporter.cs`, profile별 source capture | U1/U2/U3 | 단계별 실제 상태·근거·효과 관측 |
| U5 후보·근거 | live evidence query/composer, 네 profile의 live builder | U3, 현행 자산 | 다양한 합법 후보, 관점·장기 기억 유지 |
| U6 봉인 전달 | catalog/source/replay exporters, handoff verifier | U0~U5 검증 후 U6-S; 응답 생성 후 U6-R | source와 응답별 replay의 단일 current-source 계보 |

`LongHorizonNarrativeExecutionExporter.cs`와 `ProductionCausalPlanBatchExporter.cs`는 `Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/` 아래다. source/replay/package exporter는 `Assets/Scripts/Services/Character/AI/Editor/` 아래다. 시설 capture는 `Assets/Scripts/Services/FacilityEvolution/Editor/FacilityEvolutionModuleSelectionSourceCapture.cs`다.

## 3. U0 — B0/B1의 증거를 먼저 재분류한다

### 절차

1. 후속 수정 계획 §3의 **91개 묶음 행**을 추출해 신규 `b1-completion-matrix.json`을 만든다. B0의 11개 우선 ID도 같은 표의 관련 행에 태그로 연결한다. “기존 Batch0~5”와 “후속 B0/B1” 명칭을 혼용하지 않는다.
2. 각 행의 실제 entry/필수 branch를 원본 코드에서 확인한다. 이미 있는 14개 manifest(15행), Phase80 142 step, B0 10 runner group, 과거 323-candidate closure를 대응시킨다. 문자열이 비슷하다는 이유만으로 매핑하지 않는다.
3. 행 상태를 `current-evidence-complete`, `implemented-evidence-gap`, `partial`, `not-implemented`, `not-applicable`, `covered-by`로 구분한다. 마지막 둘은 원래 요구를 없애는 수단이 아니며 근거와 참조 행이 필수다. 중복 참조는 비순환이어야 한다.
4. 빠진 76개 명시 매핑은 먼저 기존 증거에서 찾는다. 코드와 시험이 충분하면 새 연결 기록만 만든다. 실제 결함/빠진 분기만 구현·시험한다.
5. preserved-failure 18개의 현재 파일 길이/SHA를 새 index에 추가한다. 이는 지금 확보한 파일 봉인이지 과거 실행 시점의 원본성을 소급 증명하는 것이 아니므로 `sealedAt`/역사적 provenance 한계를 기록한다.
6. A01/A03/A04 stale manifest와 sourceFiles가 없는 manifest는 수정하지 않는다. 새 index에 `supersedes`/과거 artifact 해시/후속 시험 step을 연결한다. 과거 파일에 현재 source 해시를 덧붙여 당시 증거처럼 만들지 않는다.
7. 영향 표 `changed file → shared seam → row/branch → 필요한 시험`을 작성한다. 검사기는 A05/G13뿐 아니라 전체 행의 존재·필수 증거·실제 PASS 상태·해시를 확인한다.

### 행별 최소 계약

`rowId, requirement, disposition, entryPoints, requiredBranches, sourceIdentity, sourceFiles, evidenceRefs, faultSaveCoverage, runnerSteps, exportExamples, supersedes, rationale`를 가진다. 적용되지 않는 fault/save 경계는 null이 아니라 이유를 기록한다. 실제 버그 여부를 모르면 `implemented-evidence-gap` 또는 미확인 사유로 남긴다.

`sourceIdentity`는 `frozenSourceInventorySha256 + gameCommit + dirtySourceSha256 + contentSha256 + contract/schema versions` 또는 정확히 이 정보를 가진 고정 root 참조다. inventory는 코드·자산·계약 파일 목록이며 자기 자신·matrix·후속 실행 산출물을 포함하지 않아 순환 해시를 피한다. verifier가 행→inventory root→파일 byte hash와 행→시험 artifact/proof revision을 모두 추적한다.

### 시험 재사용 기준

- 행 전용 코드 변경: 해당 entry·필수 실패·save 회귀만 재실행한다.
- 공용 ledger/save/outbox/query 변경: 실제 의존 행 시험과 통합 Phase80을 실행한다.
- exporter/verifier/schema만 변경: 계약 부정 시험, current-source 동결, 동일 snapshot의 두 번 export와 package 검증을 실행한다. 무관한 UI를 전부 재플레이하지 않는다.
- 역사 증거를 current 근거로 승계하려면 관련 source/자산/계약이 동일하거나 변경 영향이 없다는 추적 근거가 있어야 한다. 바뀐 hash만 새로 찍는 것으로 PASS를 승계하지 않는다.

통과: 91행 분모 일치, 미분류 0, 순환·누락 참조 0. 이 시점의 분류 완료는 B1 구현 완료가 아니다. `current-source complete`는 활성 요구의 부분/미구현/필수 증거 gap이 0일 때만 허용한다.

### 검증 모드 분리

- **snapshot-integrity:** 봉인 파일·기록 당시 commit/digest/참조 관계의 내부 무결성만 확인한다. 현재 worktree와 동일하다고 요구하지 않으며 runtime PASS를 새로 주장하지 않는다. r6/r8 같은 과거 자료의 재현·참조 검증은 이 모드다.
- **require-current-source:** 새 권위 승격 시 현재 HEAD/고정 dirty-content inventory/계약/시험 증거와 대조한다. 과거 r6/r8에 이 모드를 적용하면 현재와 다르다는 실패를 그대로 보존한다.
- 내부 무결성 PASS를 current-source PASS 또는 학습 적격으로 자동 전환하지 않는다. 구 패키지 파일을 고쳐 current-source 비교를 통과시키지 않는다.

## 4. U1/U2 — 같은 인물·같은 시간으로 복원한다

### U1 수정 순서

1. `SelectionActor`가 별도 fresh actor를 만드는 경로와 `evidenceOwnerCheckpoint`의 실제 owner를 하나의 공식 aggregate restore 경로로 합친다. 작은 progression snapshot 옆에 별도 health 기본값을 두지 않는다.
2. 요청에 영향을 주는 identity, body-health/lifecycle, progression/narrative/acquired-trait, 장비 참조, pending request 및 해당 world registry 의존성을 공식 저장 DTO에서 복원한다. 원본에 없는 필드는 “건강함”으로 채우지 않고 `INCOMPLETE_OWNER_CHECKPOINT`로 거절한다.
3. 복원 후 persistent ID뿐 아니라 lifecycle revision·owner revision·해시를 비교한다. actor/피해자/목격자/장비 사용자를 서로 바꾸지 않는다.
4. 사망 owner에 새 기술·특성 요청이 생기지 않도록 **실제 live 서비스의 적격 검사**를 재사용한다. exporter만 필터링해서 런타임 결함을 가리지 않는다. 현재 게임에 없는 부활·승계 규칙은 추가하지 않는다.
5. 살아 있으나 downed인 경우는 사망과 다르다. 현행 게임의 요청 가능 정책을 그대로 적용하고 `allowed/deferred/rejected` 이유를 export한다. 임의 완치 후 재요청하지 않는다.
6. 요청 대기 중 사망·owner revision 변경은 stale 요청으로 취소/거절하고 아무 효과·이정표 소비도 추가하지 않는다. 허용된 새 요청은 새 ID/revision으로 만든다.

### U2 수정 순서

1. lifecycle fixture의 body-health, combat, death, recovery, ledger producer에 같은 game-time/calendar authority를 주입한다. save에 실제 game time을 포함하고 복원한다.
2. 구 `7→0` 자료의 날짜만 정렬하거나 max값으로 덮지 않는다. 원래 실행을 새 버전에서 다시 수행한다.
3. 시간 의미를 분리한다: `occurredAt`은 게임 사건 발생 시점, `recordedAt`/sequence는 원장 전달·확정 순서다. 즉시 전투→회복 경로는 발생 시점도 비감소해야 한다.
4. outbox 지연 전달은 원래 발생 날짜를 보존한다. 모든 append sequence에 무조건 발생 날짜 단조성을 강제해 정상 지연을 거절하지 않는다. 이 경우 원 operation ID, 발생 시점, 기록 시점, 지연 사유가 있어야 하며 인과 부모의 실제 발생은 자식보다 늦을 수 없다.
5. query/export는 capture 시점 이후 사실, 미확정 사건, 잘못된 시간 출처를 거절한다. 압축 기억은 원본 first/last 구간과 발생 횟수를 보존한다.

### 필수 회귀

| ID | 입력/진입 | 기대 결과 |
|---|---|---|
| U-STATE-01 | 생존 owner 저장→새 composition 복원→요청 | 동일 health/lifecycle/ID/revision/공개 근거 |
| U-STATE-02 | 기존 stage3 계열 사망 owner | 기술·특성 요청 불가, 원장/이정표/효과 추가 0 |
| U-STATE-03 | 요청 뒤 사망·revision 변화 | stale 응답 거절, 중복 적용·소비 0 |
| U-STATE-04 | downed와 dead 각각 | 현행 서로 다른 정책 보존, 자동 부활 없음 |
| U-TIME-01 | 부상→회복→재전투, 중간 save/restore | 동기 사건의 시간 역전 0 |
| U-TIME-02 | 부모 commit 뒤 child/outbox 전달 지연 | 원 발생 시각 유지, 기록 시각/인과 구분, 중복 0 |
| U-TIME-03 | 미래/다른 시계/부모 이후로 위조 | 정확한 사유로 거절; 순서 바꿔 통과시키지 않음 |

## 5. U3/U5 — 위상 계획에서 실제 학습 입력으로 이어 붙인다

### U3 연결 절차

1. 기존 위상 시나리오/장기 runner의 계획은 **행동 요청**으로만 사용한다. 능력치·자원·보유 상태·관계 등은 선택 가중치/가드에 영향을 주되 낮은 전투력의 강적 도전을 일률 차단하지 않는다. 전투 승패나 손실을 계획에 적힌 대로 강제하지 않는다.
2. 실제 C# 명령을 실행하고 결과 receipt·상태 전후·인과 operation을 받는다. 실패·부분 성공·자원 손실도 실제 결과로 남기고 후속 가용 노드를 재계산한다. 실행하지 못한 계획 노드에 원장 fact를 만들지 않는다.
3. 기존 장기 export가 필요한 owner/world DTO를 전부 담는지 검사한다. 부족하면 **runner가 실행 중인 그 시점**에 공식 aggregate checkpoint를 추가 캡처한다. 옛 로그 문장으로 world state를 역생성하지 않는다.
4. 기술 후보/3·8·20 특성/장비·시설 진화의 현행 live 트리거가 생겼을 때 pending request를 캡처한다. milestone을 직접 올리거나 두 번째 recipe를 테스트 편의로 열지 않는다.
5. 실제 transport 직전 messages/schema와 pending selection IDs를 봉인한다. 이후 검증을 위해 새 요청을 만들어 ID를 바꾸지 않고 저장된 pending request를 복원한다. 같은 serializer로 wire 재산출 가능 시 원본 바이트와 비교한다.
6. `FormulaPresentationProductionSourceV8Exporter`에 연속 실행 패키지 intake 경로를 추가한다. 100행을 만드는 기존 네 fixture 루프는 `integration-fixture` 전용으로 남기며 학습 입력 경로에서 암묵 호출하지 않는다.
7. 실패 시 reject report를 남긴다. 요청 미발생은 `not-eligible`이지 오류 없는 가짜 학습 행이 아니다. 목표 행 수가 부족하면 동일 상태를 복제하지 말고 추가 실제 캠페인을 실행한다.

### U5 근거·후보 절차

- `GameplayOutcomeNarrativeEvidenceQuery`, `GameplayOutcomeEvidencePublicMaterialComposer`, `GameplayOutcomeEvidenceBindingAuthority`는 기존 역할/인지/망각/instance 관점을 보존한다. 근거는 해당 요청 시점에 알 수 있는 사실이어야 한다.
- 직접 원인과 보조 근거를 구분하고 단순 시간 순서·같은 atomic batch를 인과로 둔갑시키지 않는다. public fact 제한은 유지하되 필수 부모가 누락되면 맥락 압축 또는 명시적 거절을 한다.
- 내부 계보의 폐쇄성과 인물에게 공개 가능한 근거는 다르다. 인물이 모르는 부모 사건은 검증 sidecar에서만 연결하고 공개 맥락에는 넣지 않는다. 그 요청은 알려진 결과만 표현하거나, 공개되지 않은 원인이 반드시 필요하면 사유를 남겨 제외한다. 폐쇄성을 맞추려고 망각/인지 제한을 우회하지 않는다.
- exact/compacted/legacy의 동일 원천 사건은 예산 영향력을 한 번만 센다. 압축 수치와 사건 내용이 정답에 사용되는 시험을 포함한다.
- 여러 합법 후보를 제공하는 실제 자산·캐릭터/시설/장비 상태를 사용한다. 후보 수를 늘리려고 임의 모듈을 합법 목록에 넣거나 budget/conflict 조건을 완화하지 않는다.
- 기존 카탈로그로 선택 다양성 목표를 충족할 수 없다면 profile별 가능한 후보 공간과 부족 자산을 보고하고 그 profile의 확대를 보류한다. 새 게임 효과·밸런스 추가는 이 계획의 자동 승인 범위가 아니다.
- 과거 진화 결과는 당시 실제 발생한 역사 문맥으로는 허용한다. 현재 요청의 미래 결과·배분·정답을 입력에 넣지 않는다. 과거 동일 선택만 반복하는 가족은 선택 다양성 실적으로 세지 않는다.

통과: 실제 원장→동일 owner checkpoint→live trigger/pending→wire의 계보가 모든 학습 후보에 존재한다. 생활/노동·건설/시설·관계·경제·전투/부상 등 선택된 캠페인에서 실행되지 않은 분야를 coverage에 포함하지 않는다. 게임 전반 coverage는 U0의 행별 표와 별도로 계산한다.

## 6. U4 — replay가 무엇을 증명하는지 분리한다

### 적용 단계 계약

| 단계 | 실제 관측 | 원장 영향 |
|---|---|---|
| request accepted | 저장된 pending와 parser/후보 수/ID/조합 검증 | 선택 사실을 복사한 목록은 관측 증거 아님 |
| candidate stored | 액티브 후보·formula draft 상태와 저장 왕복 | 플레이어 확정 전이라면 influence 소비 0이 정상 |
| selection committed | 플레이어 확정 또는 passive/ultimate/trait 자동 확정의 실제 상태 delta | 현행 정책에 따른 use/revision 변경을 before/after로 관측 |
| mechanic executed | 실제 발동/시설 산출/장비·특성 반응의 권위 상태·결과 | 작성 상태만 바뀐 경우에는 이 단계를 PASS로 쓰지 않음 |

1. `evidenceInfluenceFactIds`를 response에서 복사하는 경로를 제거하거나 이름을 `selectedEvidenceFactIds`로 한정한다. 별도의 `observedInfluenceDeltas`는 exact/compacted 권위 anchor의 before/after를 읽어 만든다.
2. profile와 단계별 예상 delta를 정한다. 액티브 후보 3개를 만드는 것과 그중 1개를 플레이어가 고르는 것을 별도 처리한다. 자동 선택을 live 게임 정책으로 추가하지 않는다.
3. 모든 파일럿 요청은 parser/service·각 후보 합법성·단계에 맞는 상태 저장 왕복을 증명한다. 실제 효과 실행은 효과 계약 fingerprint(모듈 조합/주효과/대상/트리거/도메인/공식 버전)별로 대표 케이스와 실패 경계를 연결한다. 매 행의 효과 발동을 실행하지 않았다면 그렇게 명시한다.
4. 액티브 확정 시험은 원 checkpoint를 복제한 별도 시험 분기에서 명시적인 simulated-player 선택을 수행한다. 후보별 검증 분기가 원 캠페인이나 다른 후보의 상태를 오염시키지 않게 한다.
5. 배분의 주효과는 첫 positive다. HP와 방어력의 원시 숫자 크기를 비교하지 말고 C#의 서로 다른 단위·예산 기여도·주효과 정책을 검증한다. 모델 선택을 hash/첫 후보로 교체하면 실패다.
6. replay 시작 시 현행 코드·asset·catalog·application contract identity를 다시 계산해 source의 허용 identity와 대조한다. source의 hash 문자열을 proof에 복사하는 것으로 확인을 대신하지 않는다. 실행한 빌드/assembly 및 관련 exporter 파일 hash, Unity 버전·runner 설정을 봉인한다.

### 반드시 남기는 시험

- 네 profile의 합법 선택 유지, 불법/중복 ID·순서 오류·요청 후보 수 불일치·예산/충돌 위반 거절.
- 후보 저장과 최종 확정의 influence 정책, 동일 응답 재전달·save/restore 뒤 중복 영향 0.
- 실제 스킬 3종과 주요 trigger/target/domain, 특성 효과 적용·제거, 장비 내구 등 선택 효과, 시설 비용·플레이어 결정·실제 결과.
- 후천 특성 3/8/20 및 소거 상태 회귀를 재사용한다. 망각의 인장 실패 시 미소모, 원본 원장 보존, 같은 이정표 즉시 재발현 방지, 기존/유전 특성과 분리 저장을 유지한다.
- commit 거절·저장 실패·부분 적용·재시도 시 기존 트랜잭션 정책에 맞게 rollback 또는 durable outbox 복구를 확인한다. 성공으로 조용히 대체하지 않는다.

## 7. 두 저장소 사이의 새 전달 계약 — 설계 기준

### 버전과 호환

제안: source schema **9**, replay proof schema **2**, review/dataset schema **2**. live response protocol **3은 유지**한다. 구현 착수 시 동일 버전이 먼저 사용됐으면 다음 미사용 버전을 선택하고 양쪽 reader/writer와 golden 계약을 함께 고정한다. 현재 v8/v1은 역사 읽기/회귀용으로 유지하며 자동 학습 승격하지 않는다. 이름만 바꿔 v8을 v9로 선언하는 변환은 없다.

### 패키지 종류와 결박

| 패키지 | 생산/책임 | 필수 결박 |
|---|---|---|
| mechanics | Unity 정의·합법성 | game commit, dirty/content/catalog digest, B1 matrix/evidence hash |
| executed campaign | Unity 실제 연속 실행 | campaign/run/branch lineage, plan hash(비사실), operation/result, 공식 owner/world checkpoint, 시간 출처 |
| selection source | Unity 요청 시점 | 상위 두 패키지 manifest hash, profile/target/owner/revision, trigger/pending ID, checkpoint bytes, exact wire/schema bytes |
| authored response | AI | request/wire hash, 원문·시도·작성자·채택 응답 hash; 내부 sidecar 비공개 |
| replay | Unity | source/response의 외부 고정 manifest hash, 실제 실행 code identity, before/applied/restored artifacts와 단계별 proof |
| review/dataset | AI | 위 패키지 roots, 행별 편집 판정, split 계보, 사용자 승인 별도 |

각 manifest는 필수 파일의 상대 경로·길이·바이트 SHA-256을 열거한다. 파일 hash와 canonical value hash는 필드명/알고리즘 버전으로 구분한다. 자기 manifest hash는 manifest 내부에 넣지 않고 delivery/index 또는 호출 인자로 외부 고정한다.

### source의 필수 의미 필드

- `sourceTier`: `integration-fixture`, `controlled-campaign`, `natural-play` 중 실제 생성 방식. fixture는 학습 풀에서 제외한다.
- `lineage`: campaign instance/run/branch/owner/target, root operations, 원본 plan/template family, parent execution manifest/hash. display name·row index로 identity를 대체하지 않는다.
- `capture`: 실제 game 시점/clock identity, owner/world checkpoint 경로·hash·revision, pending request/trigger. 가변 state의 두 번째 사본을 권위로 두지 않는다.
- `evidence`: exact/compacted ID와 원천 연결, 참가 역할·인지·visibility·발생/기록 시간, 부모/보조 관계, 실제 전후 값. 모델 공개 분량과 검증용 sidecar를 분리한다.
- `wire`: 전송한 messages/schema의 정확한 bytes/hash, profile별 제한·candidate count·selection IDs. 미공개 정답·후속 결과가 없음을 검증한다.

### replay proof의 필수 의미 필드

`sourceManifestSha256`, `responseManifestSha256`, `requestId`, `responseSha256`, `replayCodeIdentity`, `stage`, `before/applied/restored artifact refs`, `selected modules in order`, `formula IDs`, `selectedEvidenceFactIds`, `observedInfluenceDeltas`, `stateAssertions`, `effectCoverageRefs`, `failures`를 포함한다.

stage별 `passed/failed/not-applicable/not-executed`와 이유를 사용한다. candidate-stored에서 effect-executed를 true로 채우지 않는다. 적용된 상태 artifact를 실제 byte hash와 canonical semantic hash 양쪽으로 묶고, AI가 파일 없이 임의 해시만 제출해도 통과할 수 없게 한다.

### 원본 보존·실패

새 패키지는 staging에서 완전히 검증한 뒤 create-only final 디렉터리로 확정한다. 실패 staging은 실패 report와 함께 별도 보존하고 정식 index에 넣지 않는다. 잘못된 기존 데이터를 자동 고치지 않는다. 기존 적용된 게임 save는 schema 갱신만으로 수치/효과 재배분하지 않는다. 구 pending 복구 불가는 명시적으로 거절/취소 후 새 요청하며 원래 상태와 실패를 보존한다.

## 8. U6 — 최종 봉인·전달 절차

U6는 두 번의 명시적 handoff다. **U6-S**는 91행 요구·현재 코드/자산 기준을 닫고 mechanics/executed-campaign/selection-source를 봉인해 AI R4/R5로 전달한다. **U6-R**은 AI가 작성한 응답을 받은 뒤 같은 동결 코드/상태에서 replay를 실행·봉인한다. U4에서 미리 검증할 수 있는 테스트 응답은 integration 전용이며 최종 응답 replay를 대신하지 않는다.

1. 작성자 동결→관련 current-source compile/집중 회귀→필요한 Phase80 통합 순서로 수행한다. 과거 DLL 실행은 새 증거가 아니다.
2. U0 matrix의 미닫힌 행·불일치·불명확한 N/A를 확인한다. B1 전부가 닫히지 않았으면 domain-qualified 제한 export는 가능하나 전체 완료·학습 승인 패키지로 승격하지 않는다.
3. 같은 **동결된 입력 snapshot**으로 두 번 export해 의미 payload·열거 파일 digest의 결정성을 확인한다. 실행 시각/PID 같은 운영 메타데이터는 별도 envelope로 두어 결정성 대상과 구분한다.
4. U6-S에서는 source/export manifest를, 응답 수신 후 U6-R에서는 response-bound replay manifest를 봉인하고 각 실패 이력 index를 보존한다. verifier의 `require-current-source` 모드는 행 매핑·필수 증거·current source·경로·바이트 hash·schema·profile별 parity를 검사한다. `test_evidence=not-supplied`는 진단 전달에만 허용한다.
5. 새 패키지를 AI로 복사해 전 파일 해시를 확인한다. Unity 권위 버전과 AI 소비 버전은 동일 package ID/input digest/delivery manifest hash여야 한다. 단지 catalog hash가 같다고 r6/r8을 혼용하지 않는다.
6. 두 저장소 HANDOFF에는 자기 책임의 결과와 동일 export pin만 반영한다. AI 구현 상세를 Unity 일지에, Unity 행별 로그를 AI 일지에 복사하지 않는다.

U6-S 이후 코드/자산/계약 root가 바뀌면 기존 source를 현재 것으로 간주하지 않고 새 U6-S부터 시작한다. 응답만 바뀐 경우 source는 보존할 수 있으나 해당 응답의 U6-R·편집 검수·dataset/approval은 새 hash로 다시 결박한다.

## 9. 마감 체크리스트

- [ ] U0: 91행 분류 및 증거 연결 완료; current-source 완료 표기는 실제 gap 0일 때만.
- [ ] U1/U2: 생사 owner 불일치·혼합 시계 제거, 지연 전달 시각 보존 회귀 PASS.
- [ ] U3/U5: 실제 연속 실행에서 요청 캡처, 역할/망각/장기 근거·복수 후보 검증 PASS.
- [ ] U4: 후보/확정/실제 효과의 proof를 구분하고 관측된 상태 delta·코드 identity 결박.
- [ ] U6: current-source stale/missing/hash mismatch 0, 새 단일 패키지 pin, 기존 증거 불변.

이 체크리스트를 문서 작성 중 채우지 않는다. Unity 완료는 AI 편집 품질·사람 승인·학습 허가를 뜻하지 않는다.
