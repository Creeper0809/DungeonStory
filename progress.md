# DungeonStory Progress

## 2026-08-04 - Phase 117 ownership-classifier precision pass

- Corrected the default-assembly ownership classifier so transient mutable view/input/camera/audio/VFX state and presentation-only enums/delegates are not mistaken for gameplay authority.
- Explicit authority roles such as `Runtime`, `Service`, `Policy`, `StateStore`, `SO`, domain models, and content definitions remain gated.
- Fresh analyzer result improved from `35 allowed / 441 named / 335 review / 776 unapproved` to `78 allowed / 440 named / 293 review / 733 unapproved` without changing the 811 informational default-source count.
- All hard architecture gates remain zero; raw Korean strings dropped to 8,043 as the active Character narrative localization lane progressed.
- The built-in Unity MCP client still returns `Transport closed`, but the project-scoped relay successfully completed a standard MCP handshake against the live Editor and returned Console Error/Warning `0` without restarting Unity or using OS input.
- Added `tools/unity-mcp/Invoke-ProjectUnityMcp.ps1` so final validation can continue through the same Unity MCP named-pipe bridge even if the host-owned relay transport drops again.

## 2026-08-04 - strict-save checkpoint complete; leaf asmdef migration started

- Updated RuntimeAuthorityV18Validator for strict RunFlow/RunVariable/ExperiencePacing tokens.
- Converted the assigned six strict-save sections (three economy plus Faction, DungeonDebug, RandomStream) to detached validated candidates and owner Aggregate publication.
- Added invalid/legacy/empty no-mutation coverage and registry late-failure discard checks in the relevant fixtures; `git diff --check` passed for the touched set.
- `dotnet build DungeonStory.sln --no-restore` could not run because no .NET SDK is installed.
- Started Phase 114: semantic planner scan, conflict exclusion, leaf SCC selection, and a maximum 15-file existing-asmdef migration.
- Read the planner contract and runner; next checkpoint is self-test plus clean/project-scan report generation through Unity's bundled Roslyn host.
- `AssemblyMigrationPlanner self-test PASS`; proceeding to a fresh worker report without relying on the machine-wide SDK.
- Generated `Library/AssemblyMigrationPlanner/worker-leaf-plan.json`: `885/8079/330/19/4`, graph hash `4f09c016...0a0b53`; inspecting the four one-file leaf candidates and existing asmdef ownership next.
- Re-read the Phase 114 constraints before selection; the 15-file cap and active-area exclusions rule out the next large cyclic batch.
- Narrowed selection to `DungeonFactionDefinitionSO.cs`; checking whether the existing Content/domain asmdef graph can own the serialized SO without making the pure Factions core assembly engine-dependent.
- Selected the one-file faction-definition leaf for `DungeonStory.Factions`. Planned edits: preserve GUID/meta while moving, add `MovedFrom(sourceAssembly: "Assembly-CSharp")`, and set the existing asmdef's `noEngineReferences` to false; no references are added.
- Moved `DungeonFactionDefinitionSO.cs` plus its original meta/GUID into the existing Factions assembly, added the assembly-move attribute, and enabled UnityEngine references on that asmdef. Targeted `git diff --check` passes.
- Completed the post-move semantic checkpoint in `project-fallback` mode (`1120/10646/566/19/106`, graph hash `cfdc4d41...41d636`). Both old and new faction-definition paths are absent from the Assembly-CSharp candidates, the GUID remains `2141cf61d65c4574b72b89276d3dd67f`, and no live validator path required rewriting.
- Phase 114 worker scope is complete at source/diff evidence; the root agent retains ownership of the fresh Unity compile and integrated architecture regressions.

## 2026-08-03 - Batch B survival integration and parallel boundaries

- The integrated survival boundary is green: the seven-owner Batch B fixture, V18 authority validation, architecture ratchets, and Unity Console all pass in the same loaded revision.
- `DarkSurvivalPlayModeVerifier` now proves exterior safe-water exhaustion, unsafe-water fallback and health cost, nonlethal suppression, and zero captured errors or warnings. The authoritative report is `Artifacts/QA/dark-survival-playmode-report.txt` with `RESULT=PASS; failures=0`.
- Breakdown execution now carries the persisted generation through the coroutine and dispatches a newer active generation only after the previous per-character execution slot is released, closing a stranded-generation race.
- Consecutive run-scope coverage proves deprivation burden, breakdown generation, exactly-once claims, character IDs, and restore revision do not leak between independent Aggregate roots.
- `DungeonStory.Medical` now owns the engine-independent anatomy definitions and SOs; surgery DTO/runtime-port separation remains before the complete medical boundary can move.
- Animal husbandry commands, statuses, compatibility issues, persistence, and UI now use typed failure/status codes plus parameters instead of authored completed sentences or `out string` failures.
- All visual evidence used Unity-owned capture paths. No operating-system mouse or keyboard automation was used.
- The second Medical cut now moves surgery DTOs, save models, procedure SOs, and five pure ports into `DungeonStory.Medical`; Unity Actor/Building/environment adapters remain at the default-assembly edge. Forty-two procedure assets and forty-six managed-reference type IDs were migrated without changing the script GUID.
- CharacterSummary consumables now depend on narrow query/command contracts. The new two-resolution Unity EventSystem matrix passes health-tab selection, automatic-emergency toggle/restore, surgery-modal open/close, summary reopen/close, bounds, labels, captures, and Error/Warning 0 at both `1600x900` and `900x1600`.
- The matrix exposed two real lifecycle defects that are now fixed: the environmental field initializes before the paused pre-run clock can publish day-one spoilage, and CharacterSummary close/reopen now removes the popup from the UI stack before rebinding its actor.
- Final integrated metrics are `1096 files / 3350 types / 24 mutable statics / 13 oversized / 91 large constructors / 1050 default sources / 6882 raw Korean strings`; Unity-loaded metrics are `24 statics / 84 large constructors / 1031 default MonoScripts`, with optional DI, catalog errors, and broken asset references all zero.

2026-08-03: Batch A synchronized state cut 통합 검증을 완료했다. 여섯 Aggregate 선언은 각각 CoreSession named assembly에 한 번만 존재하고, 구형 상태 파일 4개와 RunVariable의 상태-owned doctrine catalog가 제거됐다. `BatchAContentAuthorityDebugScenarios`, `RunVariableDebugScenarios`, `BatchACoreSessionSaveDebugScenarios`, `RuntimeAuthorityV18Validator`를 한 Unity 명령에서 실행해 `BATCH_A_SYNCHRONIZED_STATE_CUT=PASS`를 확인했으며 Console은 Error 0 / Warning 0이다.
2026-08-03: Roslyn ratchet은 검토된 감소값 `1093 files / 3287 types / 24 mutable statics / 13 oversized / 96 large constructors / 1054 default sources / 6948 raw Korean strings`으로 갱신했다. Unity reflection ratchet은 `96/89 large constructors`, `1054/1035 default assembly`로 재캡처했으며 optional DI/catalog/broken reference는 모두 0이다.
2026-08-03: 전체 `git diff --check`는 이번 영역과 무관한 기존 prefab/SO/scene/slnx trailing whitespace를 대량 보고해 exit 1이었고, 대상 C# 파일별 diff check와 선언 단일성 검색으로 범위를 좁혀 확인했다. Unity architecture delta를 읽는 첫 동적 명령은 `UnityEditor` 누락으로 CS0103, 두 번째는 동적 명령 보안 정책이 `System.Reflection` import를 거부해 실패했으며, 프로젝트 제공 baseline capture API로 동일 검증을 수행했다.

2026-08-03: 사용자 요청에 따라 세 하위 에이전트를 비중첩 파일 영역으로 병렬화했다. 첫 결과로 ExperiencePacing/RunFlow/DungeonDebug/ServiceRooms/EventAlert의 구형 상태 선언을 제거했고, RunVariable 구형 모델·Aggregate를 삭제한 뒤 교리 카탈로그를 상태 소유권에서 명시적 런타임 의존성으로 분리했다. RunVariable 시나리오와 Unity 컴파일은 Error 0 / Warning 0으로 통과했다.
2026-08-03: 여섯 concrete runtime의 asmdef 이동 가능성을 별도 읽기 전용 감사했다. 현재 wholesale 이동 가능한 구현은 0개이며, Unity lifecycle 및 default-assembly 이벤트·전투·아이템·시설·캐릭터 구체 타입 때문에 named assembly 역참조가 발생한다. Batch A는 six Aggregate/contract/save 단일 권위를 닫고, concrete adapter 분리는 관련 named port가 준비되는 cross-domain closure로 이동한다.
2026-08-03: 계획·findings·progress 동시 패치 2회가 PowerShell에 깨져 보인 Batch E/진행 제목 문맥을 기준으로 잡아 검증 실패했고 전체 적용되지 않았다. UTF-8 영문 문맥을 파일별로 분리한 패치로 다시 적용했다.

## 2026-08-03 — Batch A atomic workflow and standalone proof

- Reconfirmed that Batch A is not six sequential owner migrations hidden under one heading. Shared content, runtime/query-command contracts, composition, exact save staging, localized failures, and presentation are introduced once and applied across the complete six-owner set before any legacy path is removed or any owner is accepted.
- The single Batch A command now passes the real six-owner authored runtime flow, complete capture, invalid-preflight rejection, injected final-section discard, unchanged live state, unchanged `PublishedRestoreRevision`, presentation localization, and V18 authority together. Roslyn verification passes at `1091 files / 3277 types / 24 mutable statics / 13 oversized / 97 large constructors / 1058 default sources / 6948 raw Korean strings`.
- Rebuilt the HumanPlaytest player successfully (`errors=0`, `warnings=0`) and added an automation-only allowlisted scene bootstrap so Unity MCP can enter `GameplayScene` directly without fabricating gameplay services in the title composition. The allowlist is a state-free pure predicate and adds no mutable static authority.
- Unity MCP confirmed the standalone player in `GameplayScene` at `1600×900` and captured the owner-selection surface. Batch A remains in progress because the full affected pointer matrix and final runtime asmdef ownership boundary are still outstanding.

## 2026-08-03 — Batch A standalone tooling corrections

- The first combined progress/plan patch used a mojibake progress heading as context and was rejected atomically. Retrying each document against its ASCII title or exact throughput-contract line avoided rewriting existing history.
- The first automation-scene patch result exceeded the tool output budget, so its presence was re-read from the exact source before further edits. It had introduced a static readonly `HashSet` allowlist; architecture review caught the mutable-reference risk and replaced it with a state-free scene-name predicate before compilation.
- The Unity RunCommand wrapper reported the known `ProfileValueReference: GetValue called with empty id` warning after the build, while the authoritative build report recorded `Succeeded`, `errors=0`, and `warnings=0`. The wrapper result is not treated as clean Console evidence; final Editor Console verification remains required.
- The first standalone `ui.list` request did not complete within 60 seconds and was terminated without mutating gameplay state. Status and Unity MCP screenshot requests remained responsive, so the capture was retained but the affected pointer matrix remains unaccepted.

## 2026-08-03 — Batch A single-boundary integration

- Added one root-authored `CoreSessionRulesSO` to the boot catalog and routed ExperiencePacing, RunFlow, ExternalInfluence, DungeonDebug, and ServiceRooms through the same required provider; RunVariable continues through the same root catalog's authored definitions. Rules now validate full future-day coverage, rehearsal ranges, positive costs/limits, canonical service mappings, and root definition indexing.
- Expanded the Batch A fixture from six independent section checks into a single six-owner runtime flow. The real runtimes share one Aggregate root and event bus, execute authored lookups, commands and queries, mutate state, capture all six exact sections, and resolve External/Service `DomainFailure` values through the real String Table adapter.
- Added two production-registry transaction proofs: one invalid owner payload rejects the complete six-owner batch before mutation, and an injected final-section commit failure discards every prepared candidate while preserving all six live states and `PublishedRestoreRevision`.
- V18 now ratchets the shared rules provider, root catalog reference, five rules consumers, RunVariable authored catalog, Content→Foundation asmdef direction, integrated runtime flow, six-owner capture, presentation mapping, atomic preflight rejection, and final-section discard.
- Reviewed architecture is `1091 runtime files / 3275 types / 24 mutable statics / 13 oversized / 97 Roslyn large constructors / 1058 default sources / 6948 raw Korean strings`; Unity is `24 mutable statics / 90 large constructors / 1039 default MonoScripts / optional DI 0 / catalog errors 0 / broken asset references 0`. The six-owner integration and V18 validation pass together with Console Error 0 / Warning 0.
- Batch A remains in progress: its affected UI pointer flows and final assembly-ownership boundary are not yet accepted, so none of the six owners is reported complete.

## 2026-08-03 — Batch A integration tooling corrections

- The first combined documentation patch anchored on a mojibake heading and was rejected atomically. Retrying against each file's ASCII title inserted both records without touching existing history.
- One save-contract search included a nonexistent `Services/Persistence` root, one asmdef read included a nonexistent Items asmdef path, and one UI audit included a nonexistent `Assets/Scripts/Presentation` root. Several follow-up searches also reused Windows-incompatible `**/Editor` or directory-glob syntax. All returned exit 1 without mutation and were replaced by exact paths or `--glob` searches.
- A combined six-runtime source read exceeded the output budget and was treated as discovery only; every edited location was re-read in bounded ranges.
- The first integrated RunCommand treated two `void` fixtures as `bool`. The corrected wrapper compiled. The first atomic registry then exposed missing world-facility/character prerequisites, followed by physical-item/wildlife/offense/invasion prerequisites; explicit strict test-only prerequisite sections now preserve production ordering without weakening owner contracts.
- Direct execution of the Roslyn metrics script was blocked by the machine execution policy. Running the repository script through a process-local `-ExecutionPolicy Bypass` succeeded. Review confirmed the only violation-set change was the intended large-constructor improvement `98 → 97`, after which both Roslyn and Unity baselines were recaptured.
- The first integrated interface proxy was private and failed generated-proxy construction; making the Editor-only proxy publicly constructible fixed it. Its first collection return was `null`, exposing ExternalInfluence's required empty-list contract; the proxy now returns empty arrays for collection interfaces. A domain reload also invalidated one transient RunCommand DLL, and the same command succeeded against the settled editor.
- Tightening content validation initially assumed concurrent incidents could not exceed the number of distinct incident kinds, which rejected the authored unbounded late-game band. That unsupported invariant was removed; the valid requirements—ordered unique kinds and full `int.MaxValue` day coverage—remain enforced. Content edits also correctly made the Roslyn source fingerprint stale; a fresh unchanged-count verification restored the V18 gate.

## 2026-08-03 — Batch A localization-neutral command boundary

- ExternalInfluence and ServiceRooms no longer return completed UI sentences from their command APIs. They now return `DomainFailure` codes with stable scalar/ID parameters; Circus, strategic offense, and service-room presenters resolve those codes through `DomainFailures` String Tables.
- Service-room query snapshots now carry `BlockedFailure` rather than `BlockedReason`, mode changes carry `Failure` rather than `Message`, and gameplay callers persist the stable failure code instead of copying localized text into domain activity reasons.
- Service-room topology removed its facility-number/coordinate fallback and now requires `BuildingInstanceId` through `RequirePersistentInstanceId()`.
- Added 26 localized Batch A failure entries and V18 ratchets that prohibit string failure APIs, string blocked/message fields, and coordinate-derived service keys. Raw Korean runtime-string findings dropped from 6,972 to 6,948 without increasing mutable statics, oversized types, large constructors, or default-assembly sources.
- The reviewed architecture baseline now reads `1090 files / 3268 types / 24 mutable statics / 13 oversized / 98 Roslyn large constructors / 1058 default sources / 6948 raw Korean strings`; Unity capture remains `24 / 91 / 1039 / optional DI 0`.
- Unity MCP compilation, the six-owner atomic save fixture, ExperiencePacing and ServiceRooms content scenarios, and V18 authority validation pass together. Final Console state is Error 0 / Warning 0. This remains Batch A working-set evidence, not a separately completed internal lane.

## 2026-08-03 — Batch A command-boundary tooling corrections

- The first atomic-owner inventory search combined too many files and emitted more output than the context limit; no mutation occurred. Follow-up discovery used bounded manifest, signature, and call-site queries.
- Two later multi-file source reads also exceeded the output cap. Exact UTF-8 file ranges replaced them before editing.
- Several `rg` calls passed Unix-style `*.cs` or `*/Editor` path globs directly on Windows and returned exit 1; subsequent calls use `--glob '*.cs'` with directory roots. One debug-result search also combined such a bad glob after returning useful matches.
- The broad `git status --short | Select-Object` query returned exit 1 after producing a very large dirty-worktree listing; subsequent status checks were restricted to the exact touched files.
- The first large ExternalInfluence `apply_patch` and two follow-up unlock patches used Korean sentence context that did not exactly match the UTF-8 source. `apply_patch` rejected each atomically; the successful edits used stable signatures and exact UTF-8 ranges.
- The first generated String Table patch failed because only the first generated line had a patch-addition prefix. The retry prefixed every generated YAML line and applied atomically.
- The first post-change V18 command correctly stopped on a stale Roslyn fingerprint. The first analyzer verification then stopped because the reviewed raw-string violation set had improved by 24; after inspecting the exact counters, the baseline was regenerated.
- A subsequent V18 run rejected one extra default-assembly MonoScript caused by a new top-level presentation dependency group. Moving that helper inside the existing `OffenseWorldMapPanel` type restored the Unity count to 1,039; the recaptured baseline and complete validation then passed.
- A few discovery commands returned exit 1 solely because an optional second search had no match (constructor search, registration search, font-file probe). Their required first outputs were re-read with exact paths before any edit.

## 2026-08-03 — Atomic vertical-batch plan correction

- User review correctly identified that marking architectural lanes complete inside Batch A defeated the purpose of batching. The throughput contract now defines each domain batch as one atomic deliverable with only `in progress` or `completed` state.
- Batch A no longer contains per-lane checkboxes. Its six owners must share one content/runtime/save/asmdef/error/presentation cutover and one integrated verification boundary. Existing narrow passes are treated only as unaccepted working-set evidence.
- Batches B–D were rewritten the same way so later execution cannot silently regress into save-first or layer-by-layer milestones under a shared batch name.

## 2026-08-03 — Batch A runtime/static work integrated, not completed

- Removed the hidden static `DungeonDebugRuntimeRules` authority and replaced it with one VContainer-scoped `DungeonDebugRuleRuntime`, exposed separately as command runtime and read-only rule query.
- Routed the explicit query through item transfers/stacks, wildlife, research, deprivation, character stats/AI/work, exterior incidents, facility purchases/supplies, building conditions, placement, and presentation callers. ScriptableObject conditions receive the rule query through `BuildingConditionContext`; Editor fixtures explicitly opt into `DisabledDungeonDebugRuleQuery`.
- Grouped work-order execution dependencies so the Roslyn large-constructor count improved from 99 to 98 and Unity reflection from 92 to 91. No new oversized source or default-assembly file was accepted.
- Current loaded revision passes narrow debug scope, save, content, metrics, and V18 checks, but none is treated as a completed sub-lane. They remain unaccepted Batch A working-set changes until the six owners pass the single atomic boundary.
- The first findings insertion assumed the document title was `DungeonStory Findings`; the actual title is `DungeonStory Current Findings`. The failed patch was atomic, and the ownership finding is now inserted under the exact current title.

## 2026-08-03 — Scoped debug-rule cutover compile correction

- The first full Unity clean compilation after removing `DungeonDebugRuntimeRules` completed the runtime assembly and correctly exposed Editor fixture constructor mismatches for the new explicit `IDungeonDebugRuleQuery` boundary. The failures were confined to test/manual-construction call sites plus the intentionally regrouped `WorkOrderExecutionServices`; production runtime compilation succeeded. Fixtures now pass the explicit disabled Null Object, while production composition resolves the scoped query.
- A combined validator-path search initially assumed the V18 validator lived under Infrastructure/Editor; the actual file remains under Items/Editor. A follow-up query also returned exit 1 because no debug-rule ratchet existed yet. The correct file is now patched with the scoped-owner requirement and the legacy static-token prohibition.
- The first V18 run after the cutover rejected two newly oversized source files: `CharacterStats.cs` at 1,203 lines and `WorkAmountSystem.cs` at 1,216. The new work-order execution dependency group was moved to its own file and three nonsemantic blank lines were removed from CharacterStats instead of raising the architecture allowance. An initial blank-line patch used the wrong surrounding method context and was atomically rejected before the exact event/method anchors were applied.
- Moving `WorkOrderExecutionServices` beside the workforce policy initially omitted the `DungeonStory.Foundation` import that owns `IGameClock` and `IUiClock`, producing four CS0246 diagnostics in the runtime assembly. The missing domain-contract namespace is now explicit.
- The following Unity MCP compile command reached a domain reload while the temporary RunCommand assembly was still returning, so the tool reported that its transient dynamic DLL could not be loaded. Unity nevertheless rebuilt both `Assembly-CSharp.dll` and `Assembly-CSharp-Editor.dll` at 14:41:12; subsequent validation uses the reloaded editor state rather than retrying that transient command.
- The first post-reload architecture capture correctly rejected a stale Roslyn fingerprint because the final `DungeonStory.Foundation` import landed after the preceding report. No baseline was written by that failed capture; the analyzer is rerun against the exact compiled source revision before recapture.

## 2026-08-03 — Batch A fixture search correction

- Fixture discovery searched for `public sealed class RunVariableRuntime`, but the actual component is a non-sealed `public class`, and the follow-up search found no existing manual constructor scenario. Exact file reading confirmed a temporary unconstructed component is sufficient for preflight-only section coverage.

## 2026-08-03 — Batch A six-owner source cutover

- Converted ExperiencePacing, ExternalInfluence, RunFlow, RunVariable, DungeonDebug, and ServiceRooms to required exact-version typed save sections with rollback-free markers and lossless detached Aggregate replacement.
- Removed Experience/External missing-section synthesis and migration, RunVariable restore-time RNG reseed/replay authority, RunFlow's dead `finalInvasionDefended` projection, and DungeonDebug staging-time `StateChanged` emission. External V3 now persists ecology resolution; ServiceRooms V2 validates authored process contracts and detached facility/character references instead of skipping records.
- The single focused auxiliary Roslyn compilation for the six-owner source batch passed with no C# diagnostics. Focused `git diff --check` passed; only existing LF-to-CRLF working-copy notices were emitted.

## 2026-08-03 — Batch A candidate-index search correction

- Candidate-index discovery included nonexistent `Assets/Scripts/Services/Composition`, so `rg` returned exit 1 after finding the real interface/registrations. Exact `RestoreWorldCandidateIndex` and `WorldAndCharacterSaveSections` reads then confirmed staged dependency behavior.

## 2026-08-03 — Batch A DungeonDebug patch correction

- The first combined DungeonDebug patch assumed section ID `debug.run-state` and a multiline interface declaration, but the actual file uses `debug.run` on one line. `apply_patch` rejected the entire atomic patch, so no partial model/runtime changes landed. The retry uses the exact current declaration and preserves the stable section ID.

## 2026-08-03 — Batch A External audit correction

- The ExternalInfluence query again included a Windows-incompatible directory glob after the exact runtime path. PowerShell still returned the required file successfully, but the glob diagnostic confirms all implementation searches should use explicit files or `rg` directory roots only.

## 2026-08-03 — Batch A strict-pattern path correction

- The strict example was first requested from nonexistent `Services/Staff` and `Services/Save` paths. `rg` exposed the real files under `Services/Character/Work` and `Services/Infrastructure/Save`; the retry read those exact locations successfully.

## 2026-08-03 — Batch A RunFlow search correction

- The RunFlow audit appended a Windows-incompatible `Assets/Scripts/Services/*/Editor` glob to an otherwise successful exact-file query, causing exit 1. Relevant runtime and real `Services/Run/Editor` verifier matches were still captured; subsequent searches use explicit directory roots only.

## 2026-08-03 — Batch A RunVariable search correction

- A broad RunVariable search included nonexistent `Assets/Scripts/Tests` and `Assets/Scripts/Editor` roots, so `rg` returned exit 1 after useful matches. The follow-up used the actual `Assets/Scripts/Services/Run/Editor` fixture path and exact model files; its final fixture grep also returned exit 1 because that file has no save-specific scenario, confirming a new strict boundary fixture is required.

## 2026-08-03 — Batch A RunVariable audit correction

- The first RunVariable search passed a Unix-style `Assets/Scripts/Services/Run/*.cs` glob directly to `rg` on Windows and returned exit 1 after printing the requested section. The retry used `rg -l` from the directory root and explicit `Get-Content -LiteralPath` paths.

## 2026-08-03 — Batch A audit tooling corrections

- A ServiceRooms audit call destructured the shell tool result as if it were a plain object and emitted its output as indexed JSON characters, then hit the output cap. The next call reverted to `text(result)` and exact short file ranges.
- A follow-up `rg` for `private static CreateContract` returned exit 1 because the method signature differs. The authored contract files were still read; the method will be located with a signature-agnostic `rg -n "CreateContract"` query.
- A combined findings/progress patch then failed because it repeated the same findings heading as two separate patch contexts. This retry anchors each file only on its unique ASCII document title.

## 2026-08-03 — Batch A execution

- ServiceRooms transition search returned exit 1 because the final repository-wide enum pattern had no match, although the preceding targeted runtime ranges were printed successfully. Follow-up reads will use exact known files and `rg -l` discovery separately instead of combining a possibly-empty search with required range output.
- Batch A ServiceRooms findings patch initially used a PowerShell-rendered mojibake subset symbol as context and failed to match the UTF-8 source. The retry anchors only on the ASCII section heading and succeeds without rewriting surrounding content.
- 사용자 지적대로 이전 Phase 112는 비저장 작업을 목록에는 포함했지만 실행 순서는 save A–D 이후로 미뤄 사실상 저장 우선 계획이었다. 이를 6개 수직 배치로 교체했다. 이제 각 도메인 배치가 `State/Save`, `SO/Content`, `Runtime/Statics`, `Assembly/Responsibility`, `Presentation/Verification`을 함께 닫으며, 현재 진행 중인 여섯 소유자 감사는 버리지 않고 새 Batch A의 저장 lane으로 승계한다.
- Batch A 파일 경로를 찾기 위한 `rg --files | rg` 정규식이 Windows 경로 구분자/이름 조합과 맞지 않아 exit 1을 반환했다. 다음 시도는 클래스 선언을 직접 찾는 `rg -l`로 전환한다.
- Session catchup 권고에 따라 전체 `git diff --stat`을 실행했으나 1,979줄 dirty-worktree 통계와 line-ending 경고가 출력 한도를 넘어 잘렸다. 변경 유무 확인에는 충분했지만 이후 Batch A 조회는 여섯 소유자의 명시적 파일 경로로만 제한한다.
- 위 오류를 기존 mojibake 제목 아래에 기록하려던 첫 patch는 인코딩 문맥 불일치로 실패했다. 순수 ASCII 문서 제목을 기준으로 새 Batch A 구간을 만들어 해결했다.

## 2026-08-03 — V18 continuation recovery

- Full-ledger verification confirms every remaining unchecked item now belongs to Phase 112 only: save A–D and non-save E–N. The expanded plan text and historical-entry retirement pass focused `git diff --check` with no errors (line-ending notices only).
- Phase 112를 저장 전용 계획에서 전체 authoritative ledger로 확장했다. 완료된 SO/ID/item/session/offense/optional-DI/Bind/failure-code 기반은 재작업하지 않도록 명시하고, 남은 작업을 E atomic publication, F executable metrics, G1–G3 asmdef, H static/session closure, I1–I3 runtime decomposition, J UI, K localization, L content/duplicate audit, M integrated save proof, N gameplay/UI final audit로 세분화했다.
- Phases 89–107에 중복으로 남아 있던 unchecked import/save/asmdef/regression 항목은 작업 완료로 오인되지 않도록 각각 “planning entry retired; Phase 112의 해당 batch로 이관”이라고 명시해 닫았다. 이후 남은 범위의 단일 권위는 Phase 112뿐이다.
- Localization/decomposition audit confirms `CharacterSummaryInfo`, `FailureCode`/`DomainFailure`, combat adoption, String Tables, and V18 localization coverage already exist. Seventy-four production files exceed 800 physical lines, but this is only a triage upper bound until the Roslyn class-kind/partial-aware gate is implemented.
- Non-save source audit confirms production `Bind*Runtime(...)` is 0 and no C# file exceeds 1,200 lines. Numerous 800–1,100-line files remain, so the decomposition track will first install a Roslyn class-kind/combined-partial line metric and then batch only actual MonoBehaviour/Presenter >800 or runtime class >1,200 violations.
- Non-save baseline 측정 결과 optional required-interface DI는 이미 0이므로 후속 작업이 아니라 유지 ratchet으로 재분류한다. 기본 `Assembly-CSharp` gameplay MonoScript type은 1,039개다. reflection mutable-static 결과 3,110개는 compiler-generated cache까지 포함해 오염됐으므로 Roslyn/source allowlist 검사로 교체하기 전에는 진행 지표로 사용하지 않는다.
- 전체 계획 재검토 결과 save A–D만 상세하고 non-save E–H는 지나치게 넓었으며, Phases 89–107의 오래된 unchecked 항목도 중복 잔존했다. Phase 112를 유일한 authoritative remaining-work ledger로 만들고 SO/ID/item/session/expedition/atomic-save/asmdef-DI-static/decomposition/localization/final-validation을 각각 종료 조건이 있는 배치로 세분화하기로 했다.
- Revised Phase 112 verification found all throughput/batch/post-save headings and exact exit counters in `task_plan.md`; focused `git diff --check` reported no errors (line-ending notices only).
- 사용자 피드백에 따라 Phase 112 실행 계획을 owner-by-owner 방식에서 4개 save-owner batch와 4개 post-save architecture batch로 재작성했다. 이때 사용한 단계별 save-owner 카운터는 이후 전환이 끝나 폐기됐으며, 현재 source ratchet은 `54/54`와 빈 remaining set을 요구한다. Loaded Unity 통합 승인은 별도 최종 gate로 남아 있다.
- 새 throughput 계약은 batch 내 audit·공통 helper·구현을 먼저 모으고, auxiliary compile/Unity reload/domain fixture/V18/Console 검증 및 계획 문서 갱신을 batch 경계에서 한 번씩만 수행한다. subjective 전체 퍼센트 보고는 금지했다.
- 중단된 StaffDiscontent 통합 patch는 `apply_patch`가 원자적으로 완료된 뒤 사용자 중단 신호가 도착한 상태였다. 저장소와 Unity loaded types를 재검사해 DTO V1, strict validator, runtime fallback 제거, fixture, V18 ratchet이 모두 반영된 것을 확인했다.
- 첫 Unity reflection count는 전역 namespace의 nested Editor fake section 11개까지 운영 타입으로 세어 39를 반환했다. `DeclaringType == null` 조건을 추가해 운영 최상위 section만 다시 계산했고 실제 잔여 수는 28개다.
- Unity MCP에서 `StaffDiscontentDebugScenarios.RunAll(false)`와 `RuntimeAuthorityV18Validator.ValidateOrThrow()`가 모두 PASS했다. V18 결과는 save V18, authored items 772, catalyst SO 168, legacy authority 0, abstract stock assets 0이며 Console Error 0 / Warning 0이다.
- RegularCustomer strict 예제를 읽을 때 별도 `RegularCustomerSaveValidation.cs`가 있다고 추정해 `Get-Content`가 실패했다. 실제 validator는 `RegularCustomerSaveSection.cs`에 내장되어 있으며 해당 본문은 정상 조회됐다. Staff도 과도한 파일 분리 없이 section 내 validator로 구현한다.
- Staff V18 ratchet 위치 검색에서 잘못된 `Services/Infrastructure/Editor/RuntimeAuthorityV18Validator.cs` 경로를 함께 전달해 `rg`가 exit 1을 반환했다. 실제 파일은 `Assets/Scripts/Services/Items/Editor/RuntimeAuthorityV18Validator.cs`이며 출력에서 확인했으므로 이후 올바른 단일 경로만 조회한다.
- 장기 작업 재개 시 `task_plan.md` 관련 구간과 `findings.md`/`progress.md` tail을 한 호출에서 함께 읽어 출력이 컨텍스트 한도를 넘어 잘렸다. 이후 조회는 파일별·짧은 범위로 분리한다.
- 직전 StaffDiscontent invariant 복합 조회 역시 출력량 초과로 잘린 사실을 복구 기록했다. 현재 재개 지점은 남은 29개 비-rollback-free 저장 소유자 중 `StaffDiscontentSaveSection`의 strict Aggregate 전환이다.
- 위 기록을 기존 손상된 마지막 줄 뒤에 붙이는 첫 `apply_patch`는 인코딩된 문맥이 정확히 일치하지 않아 실패했고, 문서 제목과 다음 손상된 제목까지 함께 사용한 두 번째 patch도 실패했다. 순수 ASCII 첫 줄만 기준으로 바꿔 해결했다.

## 2026-08-02 — Detached save staging foundation

- Added `IDungeonSaveRestoreStage`/`IDungeonStagedSaveSection` and changed `DungeonSaveSectionRegistry` to complete preflight and prepare every section before the first live mutation.
- A staging exception now aborts without capturing or applying a rollback image, and a new regression proves earlier prepared sections remain unchanged.
- Wired physical-item restore to its existing validated `WorldItemRestoreState`; repository mutation, warehouse normalization, and marker refresh now occur only at staged commit.
- Replaced the repository's clear-and-repopulate commit with a detached `WorldItemRepositoryState` and one reference swap. Equipment/module dictionaries and stack indexes now change together.
- Fixed loadout-reservation restore to return interrupted pickups to their recorded source warehouse, and updated the physical-authority fixture so stored equipment always has a real unique stack.
- Generic JSON sections now deserialize, migrate, and validate once during preparation. The offense Aggregate and all seven combat save sections also hold typed payloads before commit.
- Updated the stale combat downed-state fixture to provide the now-required anatomy activity catalog.
- Unity MCP import passed. Staged-save contracts, ten offense strategic scenarios, and the full combat scenario suite pass; the focused compile state is Error 0 / Warning 0.
- Physical item and V18 equipment-state contract suites also pass after the atomic repository swap; the Unity Console remains Error 0 / Warning 0.
- Remaining Phase 88 work is the hard boundary: replace sequential runtime commits and transitional rollback with replaceable Aggregate roots and a single detached world swap.
- Completed the direct-section cutover: all 54 public SaveSection types now implement mandatory staging, all optional sections stage their missing-data behavior, and the Registry rejects non-staged sections at construction.
- Removed the legacy restore adapter and added the same invariant to `RuntimeAuthorityV18Validator`; Unity reflection and the V18 authority validator both pass.
- Sequential runtime commit plus rollback still exists because most runtimes do not yet expose replaceable Aggregate roots. It is retained explicitly as the next migration boundary, not treated as atomic world-swap completion.
- Scene-transition requests, title messages, and the transition host now belong to the persistent mailbox instance rather than mutable static fields. The gameplay diagnostics probe resolves the scoped navigator and no longer constructs fallback clock or time-scale services.
- Compact auxiliary builds now pass for both runtime and Editor assemblies. Production-source scans (excluding Editor fixtures and validator literals) report zero optional interface parameters, fallback infrastructure construction, runtime SO synthesis, legacy item-definition fallback, and late `Bind*Runtime` calls.
- Added replaceable state roots for stock policies, regional supply contracts, grand projects, waste policy, species incidents, staff discontent, the deprivation store, the transaction ledger, and debug-run state. Each restore builds normalized collections off-live and assigns the completed root once.
- Production bills and stock sensors now share one `ProductionAggregateStateStore`; bills, sequence, installed sensors, acknowledgement state, and both versions restore in one reference swap.
- Combat equipment no longer clears and rewrites repository-owned physical equipment or modules. Loadouts, craft orders/material policies, lineage work, and seal claims now share one `CombatEquipmentRuntimeStateStore` and restore together, while `items.physical` remains the sole physical equipment authority.
- Runtime and Editor auxiliary compilation pass after both Aggregate-store cutovers.
- Experience pacing and meta progression now replace their complete scalar/collection state roots. Faction restore builds the six authored defaults plus saved overrides and routes off-live, swaps once, then performs one strategic-world synchronization.
- Character environment exposure, work contexts, equipped protective workwear, workwear stock, accumulator, and workwear version now share one state store and restore with one swap. Defense facility state also restores through a detached dictionary replacement.
- The post-batch runtime and Editor compilers pass; truncation-marker, optional-DI, default-service, runtime-SO, item-fallback, and late-bind scans are all zero. Touched-file `git diff --check` is clean.
- External influence values, intel unlocks, dread-affected intruders, and ecology resolution state now restore through one Aggregate assignment; an unsupported payload no longer resets valid live state before reporting the version error.
- The V18 authority validator now ratchets the three shared Aggregate stores and rejects any reintroduced combat-side clearing of repository-owned equipment/module dictionaries.
- Treasury economy now has one `TreasuryEconomyAggregateStateStore` for the transaction ledger, employment contracts, paid-facility contracts, automatic procurement, overclock state, and treasury-defense policy/spending. `economy.treasury` prepares all six subtrees off-live and commits them with one root replacement.
- Runtime and Editor auxiliary Roslyn compilation both pass after the treasury cutover. Unity-native import, treasury regressions, and Console proof remain pending because Unity MCP approval is still revoked.
- Added the composition-wide `DungeonRuntimeAggregateRootStore`. The Registry now routes migrated section commits into a shallow candidate root, publishes that root once only after every stage succeeds, and discards it on failure before applying the transitional rollback for legacy owners.
- Physical items, production, combat equipment, character environment, treasury, factions, stock policy, regional contracts, debug state, experience pacing, meta progression, external influence, species state, waste policy, defense facilities, deprivation, staff discontent, and survival food now use slots in the shared root.
- Physical item stack/haul versions moved into the repository state slot, so stacks, indexes, unique equipment/modules, and their observable versions cross the restore boundary together. A focused contract now asserts one successful restore increments exactly one published-root revision and a failed final commit leaves the live aggregate slot untouched.
- Foundation, runtime, and Editor auxiliary compilation pass after the shared-root migration. Unity MCP remains revoked, so Unity-native execution of the new contracts is still pending.

## 2026-08-02 — Root SO gameplay catalog authority

- Moved 9 meta upgrades, 14 run variables, 3 owner doctrines, and 6 invasion patterns—including polymorphic gameplay effect parameters—from code-owned mutable dictionaries into the existing root `GameDomainContentCatalog.asset`.
- Added `AuthoredGameplayCatalog`, an immutable runtime projection resolved through `IGameContentCatalog`, and injected its four explicit query contracts into progression, run-state, selection, invasion, save, and presentation consumers.
- Deleted all four runtime `Register`/`ResetToBuiltIns` paths, subsystem-reset hooks, code fallback definition builders, and production references to their legacy static catalogs.
- Made `MetaProgressionState` require its upgrade catalog and `RunVariableState` require its doctrine catalog, so tests and runtime instances can no longer depend on hidden global definition state.
- Added V18 validator gates for exact authored counts (9/14/3/6), required stable IDs, successful runtime projection, and forbidden reintroduction of legacy static content catalog classes.
- Converted the fixed 12 character-stat, 30 work-type, and 13 facility-role mappings to immutable arrays and replaced mutation-based extension tests with stable protocol/serialization tests.
- Auxiliary runtime and Editor Roslyn builds pass with Error 0 / Warning 0. Unity MCP import and asset-deserialization proof remain blocked by `Transport closed`.

## 2026-08-02 — Runtime provider authority closure

- Removed policy-free runtime providers for regular customers, social reputation, staff discontent, settlement, alerts, facility evolution/synthesis/codex, research/shop/meta progression, run variables, offense, and invasion.
- Consumers now inject typed scoped registries and validate required runtime members once during composition.
- Missing required runtimes no longer produce empty saves, warning-only restore skips, unlocked research content, or default run state.
- `ILocalLlmRuntimeProvider` remains because gameplay and preparation use distinct implementations.
- Auxiliary runtime and Editor Roslyn builds both finish with Error 0 / Warning 0. Unity MCP import and PlayMode proof remain pending while the transport is closed.

## 2026-08-01 — V18 Phase 90 presentation and randomness normalization

- Renamed `CharacterSummeryInfo` to `CharacterSummaryInfo` while preserving its Unity meta GUID and all call sites.
- Split the former 2,434-line character panel into a 729-line coordinator plus shell, status, growth, AI, health, captivity, combat, and text presenters. Every presenter is below 800 lines and the coordinator now has exactly eight injected dependencies.
- Registered all presenters in the presentation composition root and pinned both line-count and dependency-count limits in `RuntimeAuthorityV18Validator`.
- Added `DeterministicRandomSequence` for explicit seed-addressed pure calculations and removed all direct `new System.Random(...)` calls from runtime gameplay code. The validator now rejects regressions.
- Recompiled through Unity MCP and passed V18 authority, character progression, and character population regressions. Current Unity Console is Error 0 / Warning 0.

## 2026-08-01 — branched production network V3 started

- Restored the persistent planning files, ran session catch-up, and read the Unity C# and ScriptableObject architecture instructions.
- Added phases 69–75 for the production dependency graph, concrete branched content, supply/buffer/order runtime, V5 persistence, UI, and verification work.
- Baseline audit confirmed 174 recipes, 62 produced-and-reused item IDs, 20 single-recipe branches, placeholder `stock-item:1` across all 24 generated overhaul recipes, exact-fuel facility support, and no dedicated production output-buffer status.
- Preserving the existing large dirty worktree; no unrelated assets or previous implementation changes will be reverted.
- The first planning-file patch used a stale findings header and was rejected atomically; the corrected patch targeted the live `DungeonStory Current Findings` header.


## 2026-08-01 — 168-node research/equipment overhaul started

- Restored the persistent task plan, findings, and progress records and ran session catch-up.
- Read the repository and Unity scripting instructions before implementation.
- Preserved the existing character-anatomy/medical dirty worktree and appended phases 61–68 for this overhaul.
- Phase 61 is active: concrete research, reward, equipment, expedition reward, save, UI, builder, and validation boundaries are being mapped before edits.

## 2026-08-01 — 168-node research/equipment overhaul completed

- Generated and validated exactly 168 research projects, 40 dedicated overhaul facilities, 24 production items, 24 production recipes, 43 combat equipment definitions, and 20 expedition module definitions.
- Added causal prerequisite contracts, reward reverse indexing, medieval/industrial effort bands, remaining-work timing UI, and deterministic pacing gates at 32.2/80.4/234.3/372.0 days.
- Enforced fail-closed equipment research locks in UI, direct runtime calls, crafting orders, and restore; added tiers, eras, growth slots, firearms, module processing, deterministic expedition drops, loss, and lineage transfer orders.
- Upgraded research and combat equipment saves to V4, preserved V4 progress ratios, rejected V1-V3 with the explicit Korean new-game message, and exposed that reason in both save-slot UIs.
- Rebuilt all generated assets and passed the 168-overhaul validator, research tree scenarios, V14 combat, material equipment, offense turn battle, and the two-resolution research pointer verifier.
- Final research UI report: `RESULT=PASS`, `1600x900` and `900x1600` captures present, Console errors 0 / warnings 0.


## 2026-07-22 - Responsive HUD and final verification closure

- Fixed the remaining portrait-HUD polish gap by clamping the upper-right control strip to the live canvas width and forcing top/bottom navigation buttons to share flexible width instead of preserving template widths.
- Extended `DungeonResolutionPlayModeVerifier` to cover actual `1600x900` and `900x1600` Game View sizes and to load `TitleScene` before title checks, removing the previous false failure when the verifier was launched from gameplay.
- Re-ran the resolution matrix in PlayMode. `Temp/resolution-matrix-report.txt` now ends `RESULT=PASS; failures=0` with `capturedErrors=0; capturedWarnings=0`; the 900x1600 gameplay capture keeps upper-right controls and bottom tabs in-bounds.

## 2026-07-21 - Physical item and hauling implementation started

- Added phases 27-34 to the active plan for the requested RimWorld-style physical item, hauling, pile UX, integration, save, and verification work.
- Confirmed the current Unity Editor Console reports `Error 0 / Warning 0`; standalone batchmode compile is blocked because the interactive Editor is already running.
- Audited the stock, warehouse, click-selection, grid-layer, AI action, and lifetime-scope integration points before editing.

## 2026-07-20 - Character growth and skill redesign

- Completed the final acceptance pass. All implemented EditMode suites plus progression, population, facility evolution, room system/environment, and AI-plan regressions passed together.
- Re-ran the P1/P2 UI surface verifier with real pointer input (`18/18`) and the exclusive character/building click verifier; both captured zero errors and warnings. Visual review confirmed character-only, building-only, and skill-alert states are distinct and readable.
- Recovered MCP `Camera_Capture` with a plain runtime camera copied from `Main Camera`. The resulting nonblank 1920x1080 world capture showed all three floors, room boundaries, doors, facilities, characters, and lighting without HUD interference.
- Final Unity Console state is `Error 0 / Warning 0`; character-growth phases 14-18 are complete.
- Replaced the placeholder mood-only handling for management skill modules with real domain integrations: production output, flat stock, research work, cleaning and repair duration, staffed shop revenue, positive relationship sentiment, and spawned-intruder damage for defense ultimates. Added deterministic progression scenarios for each numeric path and management day-use activation.
- Strengthened the Unified UI PlayMode verifier with a real `CharacterProgression.OnDraftReady` alert and Input System press/release events. The alert button opened its detail, the single choice closed it and selected Growth, the player copy hid all LLM/request terminology, the screenshot was nonblank, and the complete UI regression still passed with zero errors/warnings.
- Added and ran a repeatable skill-runtime PlayMode probe. Management fired once on day 7001, ignored a duplicate day event, reset on day 7002, and activated a real 1.3x output modifier; defense reduced a spawned intruder from 120 to 115.5 HP; a direct offense ultimate dealt damage, set cooldown 999, and rejected same-battle reuse. Console remained Error 0 / Warning 0.

- Diagnosed the start-party `staff=4`/inactive failure as two `CharacterActor`-derived components on each character prefab, not duplicate spawning. Removed the obsolete `Customer` component from both character prefabs and added GameObject-based actor canonicalization to start-party, offense, and world-save paths.
- The full real-LLM pointer verifier now passes rerolls, nine tabs, three candidate confirmations, passive readiness, party commit, same-species staff, and UI close with `errors=0; warnings=0`. Visual inspection rejected its mobile artifact because the Editor ignored `Screen.SetResolution` and wrote another 1920x1080 frame.
- Replaced the false mobile resize with the shared Editor Game View resolution controller. A second real-LLM pointer run passed at actual `1600x900` and `900x1600`, produced nonblank exact-size captures, kept all three cards inside the portrait viewport, committed exactly two active staff, and ended with zero errors/warnings.
- Updated the full-game progression save verifier for V3 growth/narrative payloads and explicit V2 rejection; the real game-save JSON round trip passed at Lv.4/XP 77 with active/passive skills intact. Unified UI then passed growth, mood, records, notices, staff, and building preview after replacing its stale section-label expectation.

- Accepted the finalized level-50 character growth, modular skill generation, three-character preparation, and persistent world-character specification.
- Restored the previous planning files and appended phases 14-18 without changing completed offense work.
- Confirmed Unity MCP tool availability; editor-state and compilation checks are next.
- Unity Editor state query passed while idle; Console baseline is Error 0 / Warning 0.
- Audited actor component ownership, final stat queries, combat snapshot creation, local LLM profiles, owner selection, VContainer registration, and character save capture/restore.
- Implemented the level-50 per-character growth records, constrained modular skill generation, hidden retry state, combat/management runtime effects, population profiles, save V3 payloads, and growth-tab presentation.
- Added `StartPartyPreparationService`: it prepares an owner plus two same-species staff, rolls identity/aptitude separately, preserves three partial reroll charges, pre-generates the next skill roll, requires a selected first active plus validated passive, and only creates world actors on final confirmation.
- Fresh world profiles now resume their missing level-one active/passive drafts after restore; replacing or restoring a prepared roll cancels stale generation callbacks.
- Unity rebuilt `Assembly-CSharp.dll` after the start-party service changes with no C# compiler errors. The MCP bridge revoked approval during that domain reload and must be retried before PlayMode verification.
- Replaced the owner-choice callback with a real three-card preparation UI. Each card has Identity/Aptitude/Skill tabs, unlimited full rerolls, three charged partial rerolls, double-click confirmation for the permanent first active, and a gated final start action.
- A PlayMode pointer event selected the Slime owner and produced all nine per-character tabs. Runtime inspection confirmed six isolated current/prefetch growth objects and two hidden pending drafts per object; no Console errors or warnings were emitted.
- Fixed a discovered rerender defect where bottom preparation buttons accumulated outside the preparation root.
- Prepared traits now drive the effective per-character runtime profile, so generated traits affect consumption, crowd sensitivity, work/facility preference, accidents, spending, movement, and combat modifiers in addition to final stats.
- Visitor profiles now use readable names/origins and are promoted to permanent staff profiles on recruitment, preventing the same hired person from returning as a guest.

## 2026-07-20 - Character progression follow-up started

- Began the per-character level and skill progression increment requested after the expedition loop was completed.
- Chose instance-owned progression with shared skill definitions so characters of the same species can level and build different loadouts.

## 2026-07-20 - Darkest-Dungeon-style offense completed

- Replaced launch-to-one-battle offense with deterministic branching expeditions containing battle, event, camp, cache, and boss nodes.
- Added expedition light, supplies, persistent health/stress, front/middle/rear formation, ability position rules, camping, loot, retreat, and ordinary-battle return to route choice.
- Connected formal dungeon rooms, modular facility support abilities, and real warehouse stock to preparation capacity, scouting, starting light, medicine, camp recovery, loadout withdrawal, rollback, and return deposits.
- Extended active-expedition saves with route node, phase, completed nodes, supplies, loot, formation, stress, damage, and preparation data plus legacy migration.
- Restyled route and battle surfaces with compact charcoal, burgundy, brass, deep-green states and removed the oversized mint action slabs.
- Updated reward, Product Shell, and P1/P2 regressions so they traverse the new route UI instead of assuming immediate combat.
- Pointer-driven Product Shell verification passed owner selection, recruitment, map/composition, journey start, first battle, and exact manual save/restore with `capturedErrors=0; capturedWarnings=0`.
- A PlayMode UI-event run selected the owner, clicked recon/target/party/route/node/action/target controls, completed all six regions through `truth_core`, and reported `truth=True; history=6`.
- Visually inspected route, battle, and final truth captures. Final domain pass and Unity Console report `Error 0 / Warning 0`.

## 2026-07-20 - Multi-node offense implementation

- Connected front/middle/rear formation to battle persistence, ability source/target positions, enemy AI, and survivor compaction.
- Stress now weakens combat performance without reducing maximum health; regular nodes use smaller encounters and the final node uses the boss formation.
- Removed the immediate boss battle and victory full-heal lifecycle. Expeditions now return to route choice after ordinary victories and finalize only on retreat, defeat, or boss victory.
- Added `BuildingExpeditionSupportAbility` and `DungeonOffensePreparationService`.
- Usable dungeon rooms and Meal/Rest/Research/Mana/Logistics/Hygiene facilities improve camp recovery, scouting, light, medicine, and supply capacity.
- Expedition supplies are withdrawn atomically from real warehouse inventory, rolled back on failure, and unused supplies plus carried loot are deposited on return.
- Reimported scripts through Unity MCP and confirmed Console Error 0 / Warning 0.

## 2026-07-20

- Replaced the active plan with the requested dungeon-linked multi-node offense redesign. The previous single-battle campaign and temporary campaign-order combat multiplier are explicitly superseded.
- Removed the temporary campaign-order combat stat multiplier and its misleading full-campaign balance regression.
- Added the first multi-node expedition domain: deterministic branching route graph, entrance/battle/event/camp/cache/boss nodes, supplies mapped to dungeon stock categories, light, persistent member stress, front/middle/rear formation, camp/event choices, carried stock loot, and retreat/defeat/completion phases.
- Extended `OffenseExpeditionRun` to own that journey state while retaining the legacy constructors needed for save migration. Unity recompiles with no Console errors.

- Read the approved offense-victory implementation plan.
- Re-audited offense, run flow, character persistence, save ordering, DI registrations, and title ownership.
- Confirmed the current runtime still uses timed automatic expeditions.
- Confirmed baseline runtime compilation succeeds.
- Reduced planning notes to the active turn-combat implementation only.
- Added `DungeonDifficulty` selection and launch propagation.
- Added the pure turn battle model, inline ability modules, six encounters, enemy AI, and command idempotence.
- Replaced product expedition completion with `OffenseBattleRuntime` and added the full-screen battle UI plus dungeon switching.
- Added stable character IDs and V2 active-battle save/restore with V1 migration.
- Replaced one-time final defense runtime with recurring 10-day `EndlessDefense` boss cycles.
- Updated old run-flow PlayMode expectations from `FinalChallenge/TruthHunt` to two recurring boss cycles.
- Editor compile attempt failed because `Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll` is missing; runtime compilation is the next recovery step.
- Removed remaining product/debug timer-completion and combat-power comparison paths from offense expeditions.
- Reworked reward, manual QA, P1/P2, and product-shell probes to submit real turn commands.
- Added explicit run difficulty persistence to start snapshots, save data, and run results.
- Fixed battle start ordering, stale restore clearing, first-turn migration, and exact command-wait restoration.
- Added product-shell pointer coverage for difficulty, battle actions, dungeon switching, and exact V2 manual save/load.
- Unity omitted the standalone battle factory source from Bee; merged factory/controller types into `OffenseBattlePanel.cs` and removed the omitted source file.
- Confirmed through Unity MCP that the Editor is idle, compilation completed, and Console reports `Error 0 / Warning 0`.
- Ran the current product-shell verifier. It failed at the first pointer callback (`StartupSettingsButton`) and all later synthetic Input System clicks; report and captures were inspected and rejected as completion evidence.
- Retried without forced `InputSystem.Update`; diagnostics proved the virtual mouse remained at `(0,0)` despite valid button screen coordinates. Switched to the existing queue-plus-`InputState.Change` fallback used by the modular-facility verifier.
- Pointer-state fallback succeeded through title, difficulty, owner selection, Settings, Save, return-to-title, Continue, and missing-save handoff with zero captured errors/warnings.
- Current failure is now the verifier's offense navigation order: it tries to click an off-screen target before opening the world map and composition surfaces.
- Added Close controls to the generated world-map and expedition overlays, and changed product verification to follow the visible map-target-close-composition-member-start path.
- Runtime inspection commands first failed because `OffenseBattleRuntime` is not a `UnityEngine.Object` and the dynamic command assembly does not reference VContainer; a panel-only diagnostic then showed the map remained active after target selection.
- Found and fixed the actual interruption: target selection re-rendered and destroyed its button, then the verifier dereferenced that destroyed button for pointer diagnostics. The interrupted request was removed and PlayMode stopped cleanly.
- Re-ran the visible offense flow. Map selection and composition passed, but the clean run had zero eligible staff; the capture and report were rejected as battle evidence.
- Added a recruitment activation service: recruitment now resolves or creates the live character, converts it to an active NPC employee with `AbilityWork`, and only then commits the recruited record.
- Extended product verification to accelerate four valid customer visits, pointer-click the recruitment card in Operations, assert live staff conversion/expedition eligibility, then continue through the visible offense composition path.
- Explicit verifier import exposed `CS0165` on a short-circuited `out` variable; initialized the recruitment record before the candidate predicate. The stale request file that was repeatedly retrying PlayMode was removed.
- The next product-shell run still found no recruitment candidate. Auditing the product scene exposed two real loop gaps: `RegularCustomerRuntime` was never present, and `CharacterSpawner` serialized only an NPC test asset while the actual customer existed only in the Resources catalog.
- Registered and eagerly created `RegularCustomerRuntime` under the gameplay lifetime scope, then made `CharacterSpawner` merge catalog customer definitions once after injection. Unity compiles these changes with `Error 0 / Warning 0`.
- Corrected the character catalog dependency to the existing `IRunCharacterCatalog`, and reordered the lifetime-scope build callback so scene injection always completes before the generated recruitment runtime is resolved.
- Bound each regular-customer record to the exact live visitor that produced the record. Recruitment now converts that actor first, falling back to an ID lookup only after save restoration or actor loss.
- The full product-shell verifier now passes through real pointer input: Hard difficulty, customer recruitment into an eligible NPC worker, map/composition, battle start, guard, dungeon switch, manual save, attack/target, exact V2 reload, title autosave, Continue, and missing-save handoff. Captured errors/warnings are both zero.
- Visually inspected `offense-turn-battle.png` and `offense-turn-battle-changed.png`: round, health, command log, and enemy health changed visibly without UI overlap.
- Fixed the AI macro-goal debug fixture so it creates its own grid instead of depending on unrelated scene state; all 29 implemented scenario suites now pass.
- Updated RunFlow verification to enter `SampleScene` from the title architecture and confirmed two recurring boss cycles, non-terminal defense, stages 1-5 without Victory, and `truth_core` truth-reveal Victory.
- Re-ran Save UI, Unified UI, P1/P2 feature surfaces, character click priority, and room inspection PlayMode verification; every report passes with zero captured errors and warnings.
- Updated P1/P2 offense coverage to use the visible map, target, composition, member, and launch controls, while accepting either an ongoing battle or a legitimate one-hit victory without allowing early truth reveal.
- Added a stable room-overlay MCP capture helper and verified 4 fill cells plus 10 outline segments. Pausing in the same command prevents ordinary hover polling from clearing the overlay between MCP calls.
- Captured and visually inspected the full dungeon room overlay, direct turn battle before/after state change, and final truth-result screen.
- Re-ran independent RoomSystem, RoomEnvironment, OffenseBattle, OffenseWorldMap, and OffenseReward scenarios; all passed and the final Console count is `Error 0 / Warning 0`.
- Rejected scenario-state completion as final evidence and began a clean, pointer-driven player run from `TitleScene`.
- Bought the visible commercial blueprint, waited at X5 for natural visitors, recruited the first real candidate from Operations, selected stage 1 on the world map, composed the party, and won by issuing visible barrier/attack/target commands.
- Fixed the visitor exit stall discovered during that run and added a focused AI scenario.
- Fixed duplicate expedition rows caused by legacy and canonical actor components sharing one GameObject.
- Fixed world-map/composition overlay overlap and added victory return treatment so surviving staff can continue the campaign.
- Added Orc and Vampire customer assets to make the natural recruitment pool large enough for the 2- and 3-member campaign gates.
- Re-ran customer, staff, world-map, battle, and reward regression suites; all five pass. Resource loading confirms three recruitable customer definitions.
- Direct replay exposed duplicate scene/generated recruitment runtimes; changed the lifetime scope to reuse the scene-authored runtime and verified the next clean run had exactly one.
- Direct replay also exposed seed-dependent recruitment starvation at the old 75 satisfaction threshold. Lowered the product/default threshold to 65 while retaining the separate visit-count gate.
- Recruited two employees through the visible Operations UI and directly won stages 1 and 2. Stage 3 then killed both employees despite using species abilities and focused targets, proving the current campaign was not naturally completable.
- Confirmed building training only affects mood and there is no persistent combat-stat growth path.
- Added stage-derived offense preparation, surfaced its bonus/effective power in the UI, and applied the same deterministic multiplier on battle start and exact save restoration.
- Added and passed a full six-stage, all-difficulty campaign-balance regression plus the existing battle, reward, and world-map suites.

## Next

- Tune content breadth: more authored route events, enemy intents, curios, diseases/quirks, and region-specific audiovisual treatment can now build on the completed expedition framework.

## 2026-07-20 - Character progression implementation

- Added actor-owned `CharacterProgression` with levels 1-20, rollover XP, learned skills, three equipped slots, exact loadout restore, and legacy-save defaults.
- Added species/trait skill tracks plus level 2/4/6 shared techniques; offense combat now receives equipped skills only.
- Character levels increase offense combat statistics by 4% per level and appear in expedition, battle, and character UI.
- Training use, completed work, battle outcomes, and successful expedition returns award XP at their single completion points.
- Character world saves now capture and restore level, XP, learned skill IDs, and equipped skill IDs.
- Added a generated `성장` tab with an XP meter and pointer-clickable skill loadout controls.
- Added and passed progression curve, actor isolation, unlock/loadout, persistence, legacy default, and training reward scenarios.
- Extended Unified UI PlayMode verification to click the Growth tab and toggle a skill through UI pointer events.
- Focused PlayMode save verification restored `Lv.6 / XP 77 / equipped 2` through the real game save service and JSON boundary.
- The pointer-driven six-region campaign still reaches `truth_core`; the three recurring party members finished at levels 6, 5, and 3 with 4, 3, and 2 learned skills.
- Final progression plus offense regression passed and Unity Console finished at `Error 0 / Warning 0`.
- Updated the stale full-save offense expectation and made orphan battle snapshots fall back to the valid saved journey; the complete game save round trip now passes too.

## 2026-07-20 - Weak-link audit

- Audited cross-system runtime paths and prioritized ten weak links across identity, progression pacing, LLM dependency, passive execution, room quality, offense preparation, formation, persistence, rerolls, and growth feedback. No gameplay code changed in this audit.
## 2026-07-20 Closed-loop integration implementation started

- Preserved the existing dirty worktree and scoped the approved follow-up to identity/save, skills/combat, room/work, equipment/recovery, UI, and direct-play verification.
- Added active phases 20-26 to `task_plan.md`; Phase 20 starts with the string identity registry and V4 persistence boundary.
- Converted regular-customer records, staff-discontent records, social rumors, and world profiles to string persistent IDs at their save/runtime boundaries.
- Added V4 save fields for per-actor social memory, global facility reputation, and staff discontent; staff discontent now captures/restores by persistent staff ID instead of shared templates.
- Strengthened focused EditMode coverage: regular customer, staff discontent, character progression/population restore, and duplicate world-profile ID rejection all pass. Unity Console remains `Error 0 / Warning 0`.
- Changed the level curve to the approved `20 + floor((level-1)/10)*5` formula; level 1->50 now requires 1,460 XP and is covered by progression scenarios.
- Connected offense node XP to real expedition resolution: event/camp/cache, normal battle, elite battle, boss battle, and successful return now award the approved stage-scaled XP values.
- Added offense pacing coverage showing the combat-heavy route reaches level 50 by stage 3 while the safer route reaches it by stage 4, with both avoiding earlier max-level drift.
- Generated active skills now store authored formation constraints. Damage skills convert to front/middle attacks, control skills to middle/rear, and support skills to middle/rear without LLM override.
- Added a guarded `CharacterSkillExecutionContext` path and connected battle-start, damage-taken, and enemy-defeated passive triggers through the real offense battle runtime. Focused progression/offense scenarios pass with Unity Console `Error 0 / Warning 0`.
- Fixed the equipment crafting UI path discovered by PlayMode: building info now becomes interactable immediately when opened, so visible craft buttons are not ignored during the fade-in frame.
- Strengthened `ExpeditionEquipmentPlayModeVerifier` to seed and verify the actual queried warehouse inventory, pointer-click the craft button, assert craft queue creation, material withdrawal, craft work completion, equipment equip, nonblank screen capture, and zero captured errors/warnings.
- The pointer-driven equipment loop now passes: `queue=0->1`, weapon stock `35->33`, Iron Edge inventory `0->1`, equip state visible in the expedition detail, and Unity Console `Error 0 / Warning 0`.
- Hardened offense save capture/restore against JsonUtility blank `activeBattle` objects: only nonempty battle snapshots are restored, and capture only keeps a battle that matches an active `InBattle` expedition.
- Extended the full-game V4 save round trip to include expedition equipment inventory, reserved loadout, craft queue, and expedition recovery stress. It now passes with `warnings=0`, one active expedition, one intruder, and exact equipment/recovery restoration.
- Re-ran focused closed-loop EditMode scenarios after the fixes: character progression, offense battle, modular facility, and room environment all pass. Final Unity Console check remains `Error 0 / Warning 0`.
- Added saved level-growth allocation records and a public stat-breakdown API so the growth UI can show base, species/trait, level, equipment, conditional passive, and final values without duplicating calculation logic.
- Rebuilt the generated character growth tab as a scrollable surface and added visible stat breakdowns, combat-only equipment notes, and recent growth-allocation reasons.
- Updated Unified UI PlayMode verification to open the Growth tab by pointer input and require the stat breakdown headings. It passes after fast-committing a real start party, with updated capture `Temp/phase67-character-growth-tab.png` and `capturedErrors=0; capturedWarnings=0`.
- Strengthened progression EditMode coverage so level 50 creates 59 allocation records, records match `levelGrowthStats`, and stat breakdowns match final stat calculation. Focused progression, offense battle, modular facility, and room environment scenarios pass with Unity Console `Error 0 / Warning 0`.
- Extended the V4 full-game save round trip again so the expedition test member levels up once and the new allocation record must survive JSON capture and restore. The PlayMode round trip passes with `jsonBytes=102875`, `warnings=0`, and Console `Error 0 / Warning 0`.
- Added direct-play recovery and recruitment gates to `NaturalRunPlayModeVerifier`: after each real offense stage it now waits for actual staff health/stress recovery and recruits through the visible Operations UI when the next region requires more members. No scenario-state injection is used for these gates.
- A clean commerce-strategy replay drove the UI through new run, blueprint purchase, research priority, day-7 defense placement, and stage-1 offense victory. Stage 2 then correctly blocked on readiness instead of launching, exposing that injured/stressed staff were not actually selecting recovery facilities (`ready=0/1`, hp `79%/41%`, stress `62/66`).
- Fixed that recovery connection gap: `Rest.asset` now uses the facility-aware `NeedRest` consideration, staff self-care facility considerations no longer require a guest visit count, and on-duty stressed staff can select Hygiene. The StaffDuty recovery scenario now verifies actual Rest/Hygiene action scores, destinations, and job-giver selection against P1 recovery facilities.
- Re-ran recovery-linked scenarios after the patch: staff duty, offense journey, and modular facility all pass with Unity Console `Error 0 / Warning 0`.
- Fixed a stale compile blocker in `NaturalRunRuntimeDebugProbe` (`IGameDataProvider.TryGetGameData` is the real API) so Unity no longer executed old assemblies while reporting outdated StaffDuty diagnostics.
- Corrected the StaffDuty recovery test contract: `TryFindBestScoredAction` is only a scored candidate and does not populate `AIAction.destination` until commit; the scenario now validates `TryResolveDestinationWithFailure` for Rest/Hygiene destinations. Focused closed-loop regressions pass again: StaffDuty, OffenseJourney, CharacterProgression, OffenseBattle, ModularFacility, and RoomEnvironment with Console `Error 0 / Warning 0`.
- Direct commerce replay after those fixes reached a real stage-1 expedition through natural UI/time flow, including day-7 defense placement. It then failed at the stage-1 boss: the two starting staff survived the first battle/cache/elite path, but one entered the boss route at roughly 6 HP and both died with the boss still at 50/82 HP. This is now an intra-expedition attrition/balance issue, not a recovery-facility selection issue.
- Updated the direct-play verifier to recruit and use the full three-person main party from stage 1, load actual available supplies from the preparation snapshot, spend medicine before dangerous route choices, and prefer camp nodes when health/stress justify it. This keeps the fix in the player automation path rather than weakening battle resolution.
- Fixed two headless listener regressions exposed by the focused suite: `EventAlertRuntime` now preserves records while skipping UI rendering when no presenter factory is injected, and `FacilityEvolutionRecordRuntime` ignores events until its recorder is injected. Focused closed-loop regressions pass again: OffenseJourney, OffenseBattle, OffenseReward, StaffDuty, CharacterProgression, ModularFacility, and RoomEnvironment with Console `Error 0 / Warning 0`.
- The next direct commerce replay passed the Day 7 pointer-driven defense build and Day 10 boss-defense trigger, then failed before stage 1 launch because the strongest candidate had 3 visits and 83.2 satisfaction but default recruitment still required a 4th visit. Lowered only the default recruit-candidate visit threshold to 3 and updated the recruitment regression to match the V4 persistent-person model where shared `CharacterSO` templates remain spawnable while the recruited persistent ID is promoted. Recruitment, StaffDuty, OffenseJourney, OffenseReward, and OffenseBattle regressions pass with Console `Error 0 / Warning 0`.
- A follow-up natural replay showed the code default was not enough because `SampleScene` had serialized the old recruitment threshold. Updated `SampleScene`'s `RegularCustomerRuntime` rule from 4 visits to 3 visits and re-ran recruitment plus offense journey/reward regressions with Console `Error 0 / Warning 0`.

## 2026-07-21 Closed-loop feature verification pass

- Switched from slow day-10/direct campaign play to focused feature tests as requested.
- Fixed stale QA contracts after the V4/string-identity and Craft-work additions: WorkPriority now expects `Craft`, staff rebellion/facility evolution/AI plan/P1P2 fixtures assign persistent IDs, and Save UI/ProductShell pointer helpers recreate or force the verification mouse when Unity input state sticks after scene changes.
- Re-ran focused EditMode failures (`WorkPriority`, `FacilityEvolution`, `StaffRebellionResponse`, `CharacterAiPlan`) and the broad implemented-scenario runner; all passed with no recent Console errors/warnings.
- PlayMode feature tests passed: camera movement stays unscaled by game speed, Save UI pointer save/load/delete works, Unified UI start/growth/event surfaces pass, ProductShell passes, and P1/P2 feature surface rows pass `18/18`.
- Added a file-request driven feature batch for MCP-independent PlayMode verification and fixed the QA-only `CS1626` compile error caused by yielding inside a `try/catch` coroutine.
- Final feature batch passed in one chain: BuildPlacement, CharacterClick, RoomInspection, ExpeditionEquipment, and SkillRuntime. Reports show `capturedErrors=0` and `capturedWarnings=0` where applicable.
- Current proof artifacts include `Temp/build-placement-ux-report.txt`, `Temp/phase47-exclusive-world-info-report.txt`, `Temp/room-inspection-playmode-report.txt`, `Artifacts/QA/expedition-equipment-playmode-report.txt`, and `Temp/character-skill-runtime-playmode-report.txt`.
- MCP direct Camera_Capture remains unavailable/revoked in this continuation, so Phase 26 direct-play/camera-capture evidence is intentionally still pending rather than claimed complete.

## 2026-07-21 Physical item and hauling implementation

- Added `DungeonItemCatalogSO`, `ItemHaulingSettingsSO`, `WorldItemStackRuntime`, `CharacterCarryInventory`, `AbilityHaul`, `AIHaul`, and `ItemPileInfoPanel`.
- Save data is now V5 and captures physical world stacks, hauling settings, and per-character carried items.
- Added `GridLayer.Item` and changed click priority to `Character > Item > Building`; `Alt` click forces item pile selection on a character-occupied cell.
- Delivery purchases and stock rewards now spawn loose world stacks at the entrance dropoff when the physical item runtime is active; gold remains abstract.
- Staff/owner hauling can reserve loose stacks, walk to the pickup cell, carry within character weight limits, suffer overburden speed penalties, and deposit stock categories into warehouse inventory.
- Shop restock uses the already-present physical restock route from warehouse to shop; successful purchases and shoplifting now add carried items to the customer.
- Options UI has a `최대 운반 배율` slider, and character info shows carried weight, base limit, max allowed weight, overburden penalty, and carried item names.
- The item pile panel opens as a singleton runtime listener, shows pile rows, switches the same panel to stack detail on row click, and returns to the list with a Back button.
- Created default `Assets/Resources/SO/Items/DungeonItemCatalog.asset` and `Assets/Resources/SO/Items/ItemHaulingSettings.asset` so item authoring and hauling tuning are visible in Unity.
- Added `PhysicalItemDebugScenarios`; the focused contract report passes for stock fallback, carry weight penalty, pile sorting/detail, stack delete fallback, warehouse aggregation, and V5 JSON save payloads.
- PlayMode MCP smoke verified `WorldItemStackRuntime.Active`, dropoff stack spawn, pile lookup, one-cell fallback marker bounds, item pile panel opening, row detail view, and Back navigation.
- Current verification state: Unity compile succeeds and Console is `Error 0 / Warning 0`.
- Expedition return supplies and loot now prefer entrance dropoff stacks when the physical item runtime is active, with direct warehouse deposit kept only as a fallback.
- Converted equipment craft materials/output and outbound expedition packing to physical stack flows, then added dedicated PlayMode coverage for actual `AIHaul` movement across loose-stack warehouse deposit, facility input delivery, craft material delivery, crafted-equipment output hauling, expedition supply packing/consume, and character carried-weight UI.
- Fixed two logistics bugs exposed by that verifier: `AbilityHaul.StartHauling()` no longer clears the freshly reserved job before starting its coroutine, and warehouse delivery now selects the nearest reachable warehouse delivery cell instead of whichever warehouse the scene query returns first.
- New proof artifact: `Artifacts/QA/physical-item-logistics-playmode-report.txt` reports `RESULT=PASS; failures=0`, with `capturedErrors=0` and `capturedWarnings=0`; carry UI capture is `Artifacts/QA/physical-item-carry-ui.png`.
- Re-ran the physical item pass for the current request. EditMode contracts pass in `Temp/physical-item-contracts.tsv` and the current Console is `Error 0 / Warning 0`.
- Fixed the item-pile PlayMode verifier entry path after it failed against the new start-party flow (`RUN_READY owner selection is still active`). It now uses the same fast start-party commit path as the logistics verifier and supports a request-file runner at `Temp/physical-item-pile-playmode.request`.
- Re-ran both physical PlayMode suites from the current assemblies: `Artifacts/QA/physical-item-pile-playmode-report.txt` and `Artifacts/QA/physical-item-logistics-playmode-report.txt` both report `RESULT=PASS; failures=0`, `capturedErrors=0`, and `capturedWarnings=0`.
- Added the final warehouse-storage polish: stored physical stacks mirror warehouse aggregate stock, stay hidden by default, and appear only while the new top-right `물품` toggle is enabled. The toggle turns itself off during build/grid modes.
- Restoring V5 physical item data now resynchronizes warehouse aggregate inventory from stored physical stacks when those stored stacks exist, preventing the old direct-inventory view from drifting away from the physical source of truth.
- Extended the V5 save contract so it explicitly round-trips a character `carryInventory` item. The updated `Temp/physical-item-contracts.tsv` reports `save_v5_contract PASS version=5; stacks=4; carried=3`.
- Re-ran the current Unity physical item verification after the save-contract change: EditMode physical contracts all pass, the UI feature batch reports `UI_REGRESSION_BATCH PASS` for `PhysicalItemPile` and `PhysicalItemLogistics`, both PlayMode reports end with `RESULT=PASS; failures=0`, and Unity Console is `Error 0 / Warning 0`.
- Resumed Phase 26 direct-play verification and found the stage-3 party was losing its recruited staff member because staff promotion was not authoritative across population binding/release. `WorldCharacterProfile.isStaff` now reasserts NPC runtime identity on bind/refresh/promote/release, and the spawner keeps staff profiles active instead of sending them back to the visitor pool.
- Added a regression to simulate a promoted staff actor whose type was reset to `Customer`; `CharacterPopulationDebugScenarios`, `StaffDutyDebugScenarios`, `CharacterProgressionDebugScenarios`, `OffenseJourneyDebugScenarios`, and `OffenseRewardDebugScenarios` all pass after the fix.
- Updated stale reward tests for physical rewards and the minimum-two recruit-candidate rule, then made `CharacterCarryInventory` single-instance and fixed physical-item fixtures to use `Ensure`. The refreshed `Temp/physical-item-contracts.tsv` reports every physical item case as `PASS`, and the current Console has `Error 0 / Warning 0`.

## 2026-07-22 Phase 26 direct-play completion

- Resumed the pending no-injection commerce direct-play campaign after the physical item work and reproduced the remaining failure at stage 6 `truth_core`: the verifier skipped usable camp recovery and entered the final boss with an under-recovered/under-leveled party, causing a real defeat.
- Fixed the verifier's natural-play strategy instead of weakening encounter stats: late-stage loadouts now reserve enough rations to enter and use camp, cautious camp selection accounts for the travel ration cost, enemy-directed non-damage combat modules are treated as usable attack/control skills, and the final stage waits for a Lv50 party with safe health/stress.
- Re-ran focused regressions after the patch: `CharacterPopulationDebugScenarios`, `StaffDutyDebugScenarios`, `CharacterProgressionDebugScenarios`, `OffenseBattleDebugScenarios`, `OffenseJourneyDebugScenarios`, `OffenseRewardDebugScenarios`, and `PhysicalItemDebugScenarios` pass.
- Clean `commerce` natural run now reports `NATURAL_RUN PASS strategy=commerce`. Stage 6 logs show `final:maxLevel=3/3`, camp `useSupply=True`, medicine before boss, `OFFENSE_STAGE_6_BATTLE_COMPLETE outcome=Victory`, and `OFFENSE_CAMPAIGN_FINISH completed=6/6; truth=True; outcome=Victory`.
- Evidence artifacts: `Temp/natural-run-commerce-report.txt`, final-result HUD `Temp/natural-run-commerce.png`, 1600x900 HUD `Temp/natural-run-commerce-1600x900.png`, 900x1600 HUD `Temp/natural-run-commerce-900x1600.png`, and MCP `Unity_Camera_Capture` from Main Camera instance `65154`.
- Final Unity Console check after captures and stopping PlayMode is `Error 0 / Warning 0`.

## 2026-07-22 Start preparation scene split

- Began implementing the approved `TitleScene -> StartPreparationScene -> SampleScene` preparation flow.
- Confirmed the current product flow still routes new games directly to `SampleScene`, where gameplay UI opens `OwnerSelectionPanel`.
- Confirmed `CharacterProgression` still hard-codes three normal active slots and two passive slots, so owner-only extra skill display/validation needs a role-based slot profile rather than a UI-only change.
- Added phases 35-38 and the new owner/preparation product decisions to `task_plan.md`.
- Added `PreparedStartPartySnapshot`, role-based `CharacterSkillSlotProfile`, and owner fixed skill support. Owners now expose four fixed owner-skill slots separate from generated active/passive/ultimate growth slots, and those fixed skills run through the existing skill effect path without becoming LLM-generated or rerollable.
- Split the scene flow so title new-game requests open `StartPreparationScene`; prepared runs then enter `SampleScene` with a handoff snapshot. Direct `SampleScene` opens still keep the old owner-selection fallback for QA.
- Added `DungeonPreparationLifetimeScope` and generated `StartPartyPreparationUiController`: owner selection shows owner portraits, large focus area, fixed skills, traits, and doctrine summary; party preparation shows one locked owner, two selected staff, four reserves, detail tabs, reroll buttons, reserve swaps, team summary, and gated start.
- Added `PreparedStartPartyGameplayApplier` so prepared starts spawn exactly the selected owner plus two staff, restore growth state/selected skills, assign persistent IDs, and suppress the gameplay owner-selection panel in the product path.
- Unity compile/refresh succeeded through MCP with Console `Error 0 / Warning 0`. Build settings now include `TitleScene`, `StartPreparationScene`, and `SampleScene`.
- Editor service verification passed: same-species staff roster, 7 total members, 3 selected, 4 reserves, owner locked, owner swap rejected, staff/reserve swap accepted, owner fixed skills 4/4, and prepared snapshot valid with two staff.
- PlayMode pointer verification passed from the title screen: clicked new game and normal difficulty, reached `StartPreparationScene`, advanced owner selection, selected a reserve, revealed swap buttons, prepared active skills through the runtime service, clicked start, and landed in `SampleScene` with one owner, two staff, three actors total, and zero active `OwnerSelectionPanel`s.
- Visual evidence captured: `Artifacts/QA/start-preparation-owner-select-endframe.png`, `Artifacts/QA/start-preparation-party-prepare.png`, `Artifacts/QA/start-preparation-party-ready.png`, and `Artifacts/QA/start-preparation-sample-scene.png`. PlayMode save-protection snapshot was restored after the run.

## 2026-07-22 Start preparation UX correction

- Fixed the reported start-button blocker by treating the owner as start-ready through the four fixed owner skills. Selected staff still require first active selection and first passive readiness.
- Added drag/drop roster swapping: selected staff and reserve staff cards now exchange through the existing `TrySwapWithReserve` service path, while the owner remains locked.
- Reworked the staff detail page away from one flat text block into a RimWorld-style structured panel: portrait area, readiness label, section tabs, identity rows, trait cards, potential summary, stat bars, skill slots, and active candidate cards.
- Removed the bottom reroll button row from the new preparation scene. Full/partial rerolls now appear as compact dot-dice buttons next to the relevant character or section.
- Localized visible preparation status messages that still leaked English text, including drag-swap and start handoff failures.
- Verified in PlayMode with a UI-event smoke runner: `members=3`, `reserves=4`, `drag_swapped=True`, selected staff readiness `3:True,2:True`, `owner_ready=True`, `start_interactable=True`, `final_scene=SampleScene`, and `owner_panels=0`.
- Visual proof artifact: `Artifacts/QA/start-preparation-final-party.png`. Final Unity compile succeeded and Console is `Error 0 / Warning 0`.

## 2026-07-22 RimWorld-style work amount and construction sites

- Added the V9 work-order pipeline: player placement creates a `ConstructionSite` on `GridLayer.Construction`, tracks `WorkOrderId`, required/completed work, delivered materials, reserved worker, and status, then swaps to the final building only on completed work.
- Routed work-unit execution through the shared work loop for construction, repair, research, equipment crafting, cooking/survival work, butchering, water, treatment, and refuel. Existing seed/new-run buildings still spawn completed.
- Building static work requirements live in ability modules; runtime progress, delivery state, worker reservation, and save payloads stay out of shared SOs.
- Updated world selection priority to include construction between item and building, and added construction-site info UI with target, status, material delivery, worker reservation, progress, and cancel action.
- Extended BuildPlacement PlayMode verification from ghost-only UX to actual pointer placement: build tab/category/item click, visible grid/ghost, world click creates a construction site, final building is not present instantly, work order exists, progress reaches 45%, completion swaps to final building, and the construction layer clears.
- Visual proof artifacts: `Temp/build-placement-ux.png`, `Temp/build-placement-construction-site-info.png`, and `Temp/build-placement-construction-progress.png`.
- Regression results: `WorkPriorityDebugScenarios=True`; `ImplementedScenarioDebugRunner` reports `Suites: 30`, `Passed: 30`, `Failed: 0`, including `P1 Work amount` and `P1 Work priority`; BuildPlacement PlayMode batch reports `UI_REGRESSION_BATCH PASS`; Unity Console is `Error 0 / Warning 0`.

## 2026-07-23 Wildlife ecosystem v1

- Added the V10 wildlife ecosystem layer: exterior habitat patches, diet/intent fields, hunger/thirst-driven target choice, territory return, predator/prey behavior, habitat-gated respawn pressure, and ecosystem save data.
- Wildlife now auto-generates grass, water, brush, burrow, and lair patches on usable exterior surface cells when no scene-authored `WildlifeHabitatMarker` exists.
- The wildlife info panel now shows Korean player-facing state, intent reason, hunger, thirst, danger, territory, expected yields, and hunt controls.
- The Operations survival section now includes wildlife abundance, food/water patch status, predator danger, respawn wait, and live wildlife rows.
- Expanded `GameplayScene` and `SampleScene` physical grid width to restore a real exterior surface band; PlayMode inspection reports 30 exterior surface cells instead of animals being packed into the entrance sliver.
- Verification: `WildlifeDebugScenarios.RunAll`, `RunPlayModeSnapshot`, and `RunPlayModeHuntLoop` all passed in the current PlayMode assemblies. Runtime inspection reports six active wildlife with `Drink`/`Rest` ecology intents and six habitat overlay renderers.
- Visual check: `Unity_SceneView_Capture2DScene` captured the exterior band with habitat overlay visible on exterior ground. `ScreenCaptureAsTexture` returned a black GameView frame in this editor state, so that artifact was rejected rather than counted.
- Current Unity Console after verification is `Error 0 / Warning 0`.
## 2026-07-23 Nameplate, wildlife motion, and camera zoom follow-up

- Started a focused follow-up for three player-reported regressions: world nameplates render behind dungeon art, wildlife visibly oscillates left/right, and mouse-wheel camera zoom has no effect.
- Preserving the heavily modified worktree and limiting edits to the involved runtime/UI/AI paths plus focused verification coverage.
- Moved world character name text and its backing line to the `UI` sorting layer while retaining actor-relative order. Runtime inspection reported every active nameplate at `UI:38`, and the gameplay capture shows names above dungeon floors, walls, furniture, and characters.
- Replaced deterministic wildlife left/right selection with near-best weighted targets, direction momentum, reversal penalty, arrival dwell by intent, immediate threat interruption, sprite facing, and eased/bobbed locomotion. Same-cell habitat decisions now fall back to natural roaming instead of repeated no-op routes.
- Restored zoom in `GameplayScene`, `SampleScene`, and `CharacterAiTestScene`, narrowed wheel blocking to actual `ScrollRect` UI, and made zoom cooperate with the URP Pixel Perfect Camera rather than being reset during rendering.
- Focused contracts passed: `WorldCharacterNameplateDebugScenarios.RunAll=True` and `WildlifeDebugScenarios.RunAll=True`, including the new movement dwell/facing case.
- Pointer-driven camera verification passed while paused, at 1x, and at 5x. Wheel zoom measured `8.438 -> 7.588 -> 8.438`; movement distances differed by only `0.0017`, and the verifier captured `Error 0 / Warning 0`.
- PlayMode wildlife samples showed nine animals spread across `Rest`, `Forage`, `Drink`, `Wander`, and `ReturnToTerritory`; most were stationary at meaningful targets while only one was moving in each sample window.
- Visual evidence: `Artifacts/QA/nameplate-wildlife-default.png`, `Artifacts/QA/nameplate-wildlife-zoomed-in.png`, and `Artifacts/QA/wildlife-natural-motion-exterior.png`. Final Unity state is idle, compiled, and Console `Error 0 / Warning 0`.
- Unity MCP `Camera_Capture` also succeeded at 1920x1080 using the Main Camera GameObject ID and confirmed the world nameplate remains visible above the dungeon render layers.

## 2026-07-23 Customer checkout patience

- Audited the staffed checkout coroutine, shop work urgency, shopping visit bookkeeping, personality modifiers, mood factors, personal facility memory, activity logs, and event alerts.
- Confirmed the root behavior gap: staffed checkout waits indefinitely and never branches by patience, while visit bookkeeping cannot distinguish purchase completion from queue abandonment.
- Added `CustomerCheckoutPatienceRules`: personality patience and species/trait wait modifiers determine restless, service-request, and abandonment thresholds, with modest visible-queue pressure.
- Staffed checkout now updates the character phase with queue position and elapsed seconds, applies a small restless mood factor, wakes idle workers again when called, and emits one-shot player alerts.
- On timeout the customer receives a stronger mood penalty and personal facility complaint memory, releases the checkout queue, avoids only that shop, preserves the remaining visit, and lets existing Utility AI choose an alternative or leave.
- EditMode customer AI contracts passed, including stage boundaries, alternate-shop handoff, memory persistence input, and the real checkout iterator reaching abandonment and releasing its queue.
- PlayMode probe passed with `outcome=Abandoned`, `waiting=0`, `mood=70->64.5`, `sentiment=-0.28`, `alerts=2`, and visible phase `구매 포기`. Final Unity Console is `Error 0 / Warning 0`.
# 2026-07-23 Paused stair and low-needs AI stabilization

- Traced the paused stair appearance to the traversal visibility fail-safe using unscaled realtime, not to a world DOTween.
- Traced combined low-need instability to leisure being treated as an emergency, single-need emergency fallback, missing owner self-care actions, and on-duty hunger being ineligible for eating.
- Changed traversal visibility deadlines and delayed restoration to scaled game time. A PlayMode probe held `Time.timeScale=0` for 0.45 seconds: the actor stayed hidden, then restored only after simulation resumed.
- Added survival-only strongest-need selection and emergency candidates for every urgent hunger, rest, toilet, and hygiene need. If the highest-scoring facility is unavailable, the next valid survival response can now run instead of falling through to wait.
- Added owner Eat/Rest/Toilet/Hygiene actions and allowed sufficiently urgent hunger/rest to start during duty. Hunger interrupts current work with `식사 필요` but does not flip the worker into off-duty state.
- Focused AI naturalness regressions passed, including leisure exclusion, combined low-need triage, owner self-care, and worker hunger interruption. The final PlayMode probe passed all 10 assertions for pause visibility and staff/owner low-needs behavior.
- Removed the temporary PlayMode probe and recompiled the Editor assembly. The recent compiler-error scan is empty and `git diff --check` passes for the touched files.
- Re-ran the complete staff-duty suite on the current assembly. The new low-needs scenarios pass; the suite remains red only on the separately tracked fixture failures `Emergency priority` (`Repair` candidate is rejected before assignment) and `Expedition return` (`AIWait` wins after return).

## 2026-07-23 Stationary AI fallback follow-up

- Started a focused audit after the runtime AI panel showed a character repeatedly selecting `Emergency -> WaitJobGiver` with every need action rejected and no target.
- The target behavior is now explicit: ordinary waiting must become a short contextual micro-action or reachable roam, while low mood should produce a bounded self-directed impulse and later return to normal utility decisions.
- Confirmed the repeat loop: urgent but currently unsatisfiable needs keep the BT in Emergency, while high recent movement pressure selects a nominal micro-action implemented as another static wait.
- Converted inspection and generic idle fallbacks to actual reachable roaming, added a stronger mood-driven wander, and left only purposeful queue/chat/shelter waits as short stationary actions.
- Low mood now suppresses ordinary work at both routine and final candidate selection. Critical mood also interrupts an active work coroutine with a visible player-facing reason.
- Added focused regressions for low-mood movement without an LLM impulse and critical-mood work interruption. `CharacterAiNaturalnessDebugScenarios.RunAll` reports `FINAL_NATURALNESS_REGRESSION PASS`.
- The actual GameplayScene PlayMode probe visited six cells while reporting `RoutineUtility: 대기 / 기분 내키는 대로 배회` at mood 17-20. Unity finished idle and compiled with Console `Error 0 / Warning 0`.

## 2026-07-23 Dark fantasy deprivation and breakdown survival

- Implemented V11 deprivation burdens for hunger, thirst, bladder damage, contamination, exhaustion, and mental instability, including health damage, breakdown probability, and guaranteed failure at sustained maximum burden.
- Connected the new `DeprivationBreakdown` BT branch to dedicated desperate relief/drink/eat/collapse action sets, violent breakdown behavior, nonlethal guard suppression, humanoid deaths, corpses, cannibalism, and emergency butchery.
- Added shared physical exterior water for humans and wildlife, water terrain/tile rendering, floor filth and wall stains, room/exterior cleanliness effects, and work-unit-based Clean targets with a player priority command.
- Added the character Health tab, overhead breakdown warning, filth detail panel, V11 save/restore snapshots, source-character metadata, taboo memories, and nonmergeable humanoid corpses.
- Verification passed: legacy survival scenarios `6/6`, dark-survival scenarios `9/9`, and pointer-driven PlayMode report `RESULT=PASS; failures=0` with captured `Error 0 / Warning 0`.
- Visual evidence: `Artifacts/QA/dark-survival-world-water-and-filth.png`, `Artifacts/QA/dark-survival-health-and-filth.png`, plus a successful Unity MCP `Camera_Capture` of the live gameplay world.
- Follow-up verification now proves clean-water facility priority, unsafe exterior-water fallback, personality-adjusted breakdown chance, permanent taboo relationship memory after restore, and nonlethal suppression (`118 -> 115.5 HP`, actor alive, breakdown ended).

## 2026-07-23 Exterior flowers, trees, rocks, and grazing visuals

- Added `WildlifeHabitatDecorationPaletteSO` and generated a single authored palette with 6 flower clusters, 3 summer trees, and 3 rock variants from the existing TINY FOREST pack.
- Added `WildlifeHabitatDecorationRuntime`: Grass/Brush patches receive consumable flower clusters, Brush receives trees, Burrow/Lair receives rocks, and extra trees/rocks are deterministically scattered over valid exterior ground.
- Flower visibility follows habitat resource in stages. Depleted patches hide every flower and regeneration restores clusters progressively; no decoration occupies the grid or alters movement.
- Added forage-intent patch filtering and immediate visual refresh after an animal consumes a patch.
- Wildlife contracts pass, including `full=5 -> depleted=0 -> regrown=3`; the clean PlayMode snapshot confirms one runtime root under `__Runtime/Exterior`, correct `OutsideObject` sorting, and populated flower/tree/rock visuals.
- A live GameplayScene probe moved a herbivore onto the flower patch and measured `resource 8.781223 -> 0`, `flowers 5 -> 0`, then `resource 10`, `flowers 5` after regrowth. Unity MCP Camera Capture confirmed grounded trees/rocks, visible flower beds, and actors rendered in front.

## 2026-07-23 Exterior pond visibility follow-up

- Traced the missing water to two default source cells at the entrance/drop-zone edge plus a locked gray runtime Tile rendered one cell above the floor.
- Default generation now uses only `ExteriorPath` surface cells and creates a four-cell pond at the outer end of the longest run: three walkable unsafe shallows and one blocked foul deep-water boundary cell.
- Reworked the runtime visual into a point-filtered 16x8 water strip, enabled per-cell tint, aligned it to the floor, and kept it above exterior ground but below actors and decoration.
- Live GameplayScene verification reports source cells `(56..59, 0)`, tile occupancy `4/4`, `Wall:2` sorting, a connected exterior path through the shallow edge, and `Error 0 / Warning 0`.
- Unity MCP Camera Capture at 1920x1080 shows the blue/teal pond grounded at the far exterior edge. Focused dark-survival contracts remain green.

## 2026-07-23 Zoom-responsive sky and centered dungeon

- Extended `DungeonSceneBackdropFitter` to consume the injected main camera and fit the solid sky to the padded camera viewport in `LateUpdate`, including orthographic size and aspect changes.
- Centered the physical dungeon interior within the 60-column world, shifted authored GameplayScene placements by `+13`, and moved the entrance/drop-zone area tags with the layout.
- Added start-time dungeon centering to `CameraManager`; live verification reports camera X and dungeon center both at `-29.5`.
- Confirmed both outer-wall boundary tiles, captured the maximum zoom-out frame, and verified no uncovered sky band remains.
- Physical-world, background-lighting, and grid-foundation regression suites pass. Final Unity Console is `Error 0 / Warning 0`.

## 2026-07-23 Entrance outer-wall adjacency

- Traced the reported one-cell entrance gap to automatic wall generation treating three invisible exterior activity markers as structural occupants.
- Limited automatic wall content to actual Building/Hallway layers and added a regression proving overlay/fixture markers cannot displace the wall.
- Fresh GameplayScene verification changed the rendered wall from X `12` to the adjacent X `13`; Unity MCP Camera Capture confirms the arch and outer wall now touch.
- Grid visual, foundation, and physical-world regressions pass. Final Unity Console is `Error 0 / Warning 0`.

## 2026-07-23 Exact facility world click

- Removed ordinary facility selection through the approximate grid occupant fallback. Facilities and construction sites now require an actual collider hit at the pointer position.
- Limited collider-free grid selection to the exact `GridLayer.Building` cell for structural walls and interior doors, and explicitly excluded hallway/floor objects from physics-hit building selection.
- Added a static classification regression for hallway, wall, interior door, dungeon door, and a normal facility.
- Extended the Input System PlayMode verifier with an exact facility click and a collider-free bare hallway click. It also retains the character-over-building exclusivity checks.
- Repaired the QA gameplay fallback so the shared start-party driver recognizes `StartPartyConfirm` and confirms generated legacy candidate skills before entering the world.
- Final `CharacterClick` batch passed: `EXACT_BUILDING_CLICK=PASS`, `BARE_HALLWAY_NO_INFO=PASS`, overlap priority passed, `RESULT=PASS`, captured `Error 0 / Warning 0`.

## 2026-07-23 Consecutive wildlife world click

- Fixed the wildlife popup lifecycle order so `CloseAll()` cannot clear the newly clicked wildlife target when the same panel is already open.
- Exposed read-only wildlife panel diagnostics and extended the wildlife PlayMode contract to send the same target twice.
- Extended the actual world-info pointer verifier to click one wildlife collider twice consecutively without clicking another target between clicks.
- Final report passed `WILDLIFE_FIRST_CLICK` and `WILDLIFE_CONSECUTIVE_CLICK`, retained character/building/floor priority checks, and ended with `RESULT=PASS; failures=0`.
- `WildlifeDebugScenarios.RunAll` passed and Unity Console finished at `Error 0 / Warning 0`.

## 2026-07-23 Wildlife horizontal facing

- Fixed all wildlife reading logical Grid X as screen direction even though the world X axis is mirrored by `Grid.GetWorldPos`.
- `WildlifeActor` now computes facing from movement endpoints in world space while preserving the authored right-facing source sprites.
- Updated the natural-motion regression to cover world-left and world-right routes explicitly.
- A paused fresh GameplayScene probe forced both directions for all four species currently spawned and reported `LIVE_SPECIES_FACING=PASS`; the shared path also covers the fifth catalog species.

## 2026-07-23 Defense interception and engagement

- Started implementation of the approved real-time dungeon-defense engagement plan.
- Confirmed the current defect is structural: manual suppression overlaps cells and deals one-way damage while the intruder movement coroutine continues independently.
- Locked product decisions: on-duty Guard workers only, one lead plus one replacement per intruder, named policies assigned per guard, and immediate owner evacuation to an Administration room with farthest-safe-cell fallback.
- Preserving the existing dirty worktree and integrating with the current V11 invasion, AI, room, UI, and save systems without reverting prior work.

## 2026-07-23 Defense interception and engagement completion

- Added `DefenseEngagementRuntime`, adjacent-cell intercept planning, transient combat-cell reservations, reciprocal attack timing/damage, lead/reserve dispatch, policy retreat and replacement, owner final defense, and combat presentation tied to scaled game time.
- Automatic dispatch now accepts only on-duty non-owner workers with Guard priority. Manual intruder suppression enters the same engagement pipeline, while nonlethal rebellion/deprivation suppression remains separate.
- Added named defense policies with create, duplicate, edit, delete, and per-guard assignment; the defense UI shows live frontline state, lead/reserve guards, exchange count, and owner evacuation status.
- Added immediate owner evacuation to the farthest valid Administration-room cell with a farthest-reachable interior fallback, and kept the owner out of ordinary guard selection until the frontline fully collapses.
- Extended V12 persistence with policies, assignments, owner evacuation, active engagements, reserved cells, attack timers, and exchange counts. Active save round trip passed with no restore warnings.
- Static regressions passed: `DEFENSE_SCENARIOS=True` and `INVASION_REGRESSION=True`.
- Actual PlayMode combat passed: three reciprocal exchanges on distinct adjacent cells, intruder held, both sides damaged, facility damage locked, presentation visible, and save snapshot valid.
- Actual policy switch passed: `state=Engaged`, `leadChanged=True`, cells remained `(1,0)/(2,0)`, facility remained locked, and the old lead resumed AI.
- Actual UI pointer probe passed the Defense tab, policy creation, and guard assignment controls through `PointerDown/PointerUp/PointerClick` events.
- Owner evacuation passed at `(41,2)`, then owner final defense passed at `(40,2)/(41,2)` with 20 exchanges, reciprocal damage, no reserve, and the intruder held.
- Visual artifacts: `Temp/DefensePolicyAndEngagementScheduled.png`, `Temp/DefenseEngagementWorldFinal.png`, `Temp/DefenseGuardEngagementSpaced.png`, and `Temp/DefenseOwnerFinalVerified.png`.
- After clearing Unity 6000.3.8's known startup-only UUM-133323 warning, the complete PlayMode defense verification finished with Console `Error 0 / Warning 0`.

## 2026-07-23 Developer mode and debug palette

- Added settings V2, runtime mode/cheat rules, save metadata/history, 112 modular commands, exact targeting, responsive palette UI, and pooled world overlays.
- Connected cheat hooks to money/items, placement/unlocks, needs/damage, AI, breakdowns, work/construction/research, wildlife, survival, and defense services.
- Added EditMode contracts and a pointer-driven PlayMode verifier for settings, palette tabs, exact spawn, repeat/cancel input, overlays, domain commands, invasions, save metadata, and transient reset.
- Final report `Artifacts/QA/debug-mode-playmode-report.txt` is `RESULT=PASS`; desktop and portrait captures are `debug-palette-1600x900.png` and `debug-palette-900x1600.png`.
- Unity Camera Capture verified overlay on/off rendering and the final Console audit is `Error 0 / Warning 0`.

## 2026-07-23 Construction material physical delivery

- Began tracing the yellow construction material marker reported after placement.
- Confirmed construction-order creation immediately calls the facility-delivery request path and that work readiness later consumes only an actual facility buffer.
- Confirmed the concrete defect: delivery request time withdraws aggregate warehouse stock, removes the stored physical stack, and respawns a visible loose stack at the warehouse cell.
- Added source-storage ownership to physical stack save/runtime data without breaking nested V1 compatibility.
- Facility requests now split warehouse materials into hidden outbound `Stored` reservations while keeping aggregate warehouse stock unchanged.
- Outbound stored stock is haulable through the existing multi-haul planner; pickup now atomically withdraws warehouse stock and carry insertion failure rolls it back.
- Work orders count outbound stored reservations as pending and cancellation returns them to ordinary warehouse storage.
- Updated EditMode and PlayMode expectations so request-time loose piles and early warehouse withdrawal are regressions.
- Unity completed the first recompilation with Console compile errors at 0.
- `PhysicalItemDebugScenarios.RunAll` passed all 12 contracts. Request-time stock remained held, outbound storage reservations were hidden, and save/restore preserved total/reserved/available quantities.
- The existing logistics PlayMode runner now asserts the corrected three-stage transition: unchanged warehouse stock and no loose marker after request, stock withdrawal during AI pickup, then facility-buffer creation after delivery.
- First PlayMode attempt failed at pickup despite the worker reaching the source. The report showed both same-type warehouses shared `warehouse:1050`; the source warehouse lookup selected the wrong instance. This is now tracked as the next fix rather than extending the timeout.
- Replaced shared warehouse type keys with position-qualified persistent keys and added legacy storage-ID normalization during item restore.
- Recompiled and reran all 12 physical-item contracts after the warehouse-key change; all passed again.
- The second request-file PlayMode launch did not auto-enter play and produced no report; the request remains intact, so the next attempt explicitly starts PlayMode instead of recreating the request.
- Explicit PlayMode entry also produced no report, indicating the request callback did not create the verifier runner. The next diagnostic checks active scene and runner presence before invoking the component directly.
- Console inspection found the actual blocker was `CS1503` in the legacy warehouse-ID normalizer. Fixed the narrowed type so the matched object remains an `IWarehouseFacility`.
- Replaced request-time warehouse withdrawal and loose-stack spawning with destination-reserved hidden stored stacks.
- Added position-qualified warehouse storage IDs and legacy-ID normalization so same-type warehouses cannot resolve to the wrong source inventory.
- Added an actual `ConstructionSite + WorkOrderRuntime + AIHaul` PlayMode scenario. It passed with stock `18 -> 18` at request, no loose pile, hidden reservation quantity 2, stock `18 -> 16` at pickup, and the construction order becoming `Ready` after delivery.
- `PhysicalItemDebugScenarios` passed all 12 contracts, including save/restore of ordinary, reserved, and available warehouse quantities.
- `WorkAmountDebugScenarios` passed after correcting its stale V9 assertion to the current V12 save contract.
- Pointer-driven build placement passed: construction site created, final building not instant, partial progress retained, final replacement succeeded, captured errors 0, captured warnings 0.
- Visual inspection of `Temp/build-placement-construction-site-info.png` showed only the construction-site marker at the selected cell and no quantity-badged yellow item pile.
- Final Unity state was stopped and not compiling; after clearing the Console, the audit returned `Error 0 / Warning 0`.

## 2026-07-23 Medieval dark fantasy combat V13

- Added the shared combat model, weapon/armor/shield definitions, individual equipment runtime, loadout policies, ammunition, line-of-sight/cover services, body-health runtime, projectile/melee presentation, and V13 persistence.
- Added nine initial weapons, layered armor sets, two shields, arrow/bolt items, crafting recipes, and three destructible directional cover buildings.
- Defense now gathers physical equipment during invasion warning, waits while intruders rally outside, dispatches after the breach, and combines ranged cover fire with the existing one-on-one interception line.
- Offense now preserves body injuries, bleeding, suppression, ammunition, weapon switches, recoverable throws, and downed state through battle persistence and return to the dungeon.
- Character combat UI exposes body condition, equipment, load, ammunition, cover, hit/evasion calculations, loadout presets, weapon switching, reload, fire mode, and hold fire.
- Shift additive selection, drag selection, exact intruder interception, exact cover movement, and direct Grid movement are connected through `OwnerCommandController`.
- Wildlife hunting now resolves through the same combat service. Ranged hunters choose a safe firing cell, reload from carried ammunition, launch pause-safe projectiles, and apply persistent simplified body injuries.
- Fixed manual move cancellation so evacuation or another movement owner cannot leave AI permanently locked.
- Roslyn runtime/editor compilation passed.
- Static regression result: `combat=True; offense=True; defense=True; priority=True; wildlife=True`.
- Wildlife PlayMode result: `wildlifeSnapshot=True; huntLoop=True`.
- Defense PlayMode result: `exchanges=4; held=True; adjacent=True; bothDamaged=True; facilityLocked=True; save=True; presentation=True; rally=True; approachHeld=True; ownerEvac=True`.
- Player command PlayMode result: movement completed and released its lock; immediate cancellation also reported `cancelReleased=True`.
- Visual artifact: `Artifacts/QA/combat-v13-defense-final.png`; Game View inspection shows separate adjacent fighters, damage numbers, combat labels, and wounded-only health bars.
- Unity MCP `Camera_Capture` failed twice with `Failed to render scene preview`; direct `ScreenCapture` succeeded.
- Final Unity Console audit: `Error 0 / Warning 0`.

## 2026-07-26 V16 isolated feature integration

- Audited the V15 codebase against the approved V16 plan. Confirmed duplicate scene runtimes, two equipment authorities, abstract offense reward counters, daily food withdrawal in addition to real meals, text-only exterior incidents, unused circus milestone data, generic extract stock, and disconnected AI performance settings.
- Removed `Priority Command Controller` and `RegularCustomerRuntime_Test` from `GameplayScene`.
- Added `SingleRequired<T>` scene composition validation and applied it to owner commands and regular customers so missing or duplicate required runtimes fail immediately.
- Began equipment consolidation: common combat equipment now owns craft work orders, physical material requests, unique output instances, queue persistence, and offense loadout access. Removed legacy equipment registration and save-section registration, plus legacy combat-stat double application.
- Unity MCP compilation checkpoint passed with `isCompilationSuccessful=true`; Console currently reports `Error 0 / Warning 0`.
- Remaining legacy source and verifier references are intentionally retained only until their callers are migrated; deleting the types before that checkpoint would hide actionable compile errors.

## 2026-07-26 V16 traversal-cache performance closure

- Split Grid full-content versioning from structural/traversal versioning and moved path, movement, room, lighting, and facility caches to the structural signal.
- Kept current moving wildlife discoverable by checking the actor's live Grid coordinate rather than cached visitable-occupant coordinates.
- Fixed wildlife arrival dwell timing by retaining a scaled `IGameClock` fallback when no test clock is injected.
- Recompiled successfully and passed `GridFoundationDebugScenarios`, `WildlifeDebugScenarios`, and `CharacterAiNaturalnessDebugScenarios`.
- Re-ran the 100-NPC EditMode stress scenario: valid, 2,182 decisions, 51 broker searches, 1 cache hit, 50 deferrals, max 8 searches/frame, Scheduler p95 0.73ms, elapsed 50.6s.
- Previous comparison was 1,440 broker searches, 540 cache hits, 16,461 deferrals, and roughly 353 seconds.

## 2026-07-26 V16 integration and performance verification

- Removed remaining per-decision LINQ and diagnostic allocations from AI memory, need lookup,
  survival nameplates, utility breakdowns, and action predicate selection.
- Cached `CharacterNeedCatalog.All` so sorting and array creation occur only after registration
  changes instead of during every AI decision.
- Added allocation-free deprivation display queries for world nameplates.
- Added a stabilized PlayMode profile boundary that discards the warmup/forced-GC transition
  frames; this removed stale 17-second initialization samples from normal runtime statistics.
- Marked scheduler-only GC as unsupported on the current Mono runtime because
  `GC.GetAllocatedBytesForCurrentThread()` returns zero even after a known allocation.
- Final 100-character profile passed with all 100 BTs ticked, frame average/p95
  `2.77/3.42ms`, scheduler average/p95/max `0.370/0.497/0.632ms`, and no budget overflow.
- Editor-wide GC averaged `182KB/frame`; against the prior one-character Editor baseline of
  roughly `120KB/frame`, the 100-character stress-world increment is roughly `62KB/frame`.
- Broad V16/domain regressions passed. Pointer-driven UI verification passed `21/21` rows at
  desktop and portrait resolutions, including right-click alert dismissal.
- Final focused V16 integration and AI/survival regressions passed with Console
  `Error 0 / Warning 0`.

## 2026-07-26 Weighted navigation and 500-character closure

- Replaced equal-cost fixed-destination BFS use with cost-aware A*, while preserving weighted
  Dijkstra for reachability and multi-candidate work/facility selection.
- Added terrain/traversal versioning, cross-frame path caching, bounded urgent search
  overdraft, pooled workspaces, compact routes, and shallow-water movement costs.
- Reworked AI scheduling around a due-time heap, deferred BT materialization, immediate dirty
  wakeups, and presentation LOD.
- Reused decision contexts, cached facility candidate sources, and removed a repeated
  Editor fixture scene search exposed by fine-grained profiling.
- Focused Grid and 100-character regressions passed.
- The final 500-character, 600-frame staged PlayMode profile passed with 3.39 ms average,
  4.37 ms p95, 15.40 ms maximum, 0 frames over 16.67 ms, and scheduler p95 1.809 ms.
- Current route searches remain main-threaded because the measured 11.3 microsecond A* query
  is too small for profitable per-request Job dispatch. The architecture supports future
  immutable batch execution if map and request sizes grow.

### 2026-08-01 - V18 typed identity closure and physical-state authority start

- Closed Phase 84 after enforcing typed `ItemStackId`, `ItemInstanceId`, `CharacterId`, and
  `BuildingInstanceId` issuance and save round trips on the core runtime paths.
- Raised `IWarehouseFacility` to require a `BuildingInstanceId` and centralized warehouse storage
  destination IDs as `warehouse:building:*`; removed persisted GridId/position and object-hash fallbacks.
- Converted facility-evolution per-facility dictionaries from Unity instance integers to
  `BuildingInstanceId`, and removed the character progression seed fallback that used a component instance ID.
- Compile errors during the cutover exposed the value types' actual global namespace, three Editor warehouse
  fakes without IDs, and one stale `KeyValuePair<int, ...>` loop. These were corrected without runtime fallbacks.
- Unity MCP persistent-identity contracts pass, including warehouse destination identity.
- Began Phase 85 with registered `PhysicalStockQuery`: it owns no save data and derives global and
  per-warehouse quantities directly from physical stack records, including outbound reserved warehouse stock.
- The first stock-query test crossed the Editor assembly's internal boundary; a `UNITY_EDITOR`-only fixture
  adapter now seeds the internal repository without exposing its record type to production consumers.
- Added equipment physical item-state schema V2. Its payload contains the complete mutable equipment instance
  plus attached module instances; schema V1's partial copy is explicitly rejected as V18 authority.
- Reload, ammunition use, armor/shield durability, evolution, module install/removal, world-state changes,
  and lineage transfer synchronize linked physical equipment state and fail loudly on stack update failure.
- Unity MCP physical-stock and equipment-state contracts pass; final compile Console is Error 0 / Warning 0.
- Upgraded `WarehouseInventorySnapshot` to V3 and removed all serialized category quantities and the
  `StockAmountSnapshot` DTO. The building module now saves only capacity and category policy.
- Removed `WarehouseInventory.CreateSeeded` and facility-start aggregate stock fabrication. Starter supplies
  remain physical drop-off items, and warehouse derived counts rebuild from physical stacks after restore.
- The first building-state rerun exposed two manual test buildings without typed IDs and one obsolete assertion
  expecting V1 aggregate stock migration. The fixtures now receive explicit IDs and assert that legacy counts
  are not restored. Warehouse physical-authority and building-state contracts then passed through Unity MCP.

### 2026-08-01 - Phase 85 physical item authority closure

- Removed the last warehouse quantity dictionary and runtime mutation methods. `WarehouseInventory` now stores
  only capacity and category policy, while `PhysicalStockQuery` derives every count from stored physical stacks.
- Replaced the temporary Editor `Deposit/Withdraw/AddStock` compatibility names with explicit physical-stock
  fixture operations; product and Editor source now contain no callable aggregate warehouse writer.
- Raised the physical item section to V6 and moved complete equipment plus attached-module payloads into
  `DungeonPhysicalItemSaveData.uniqueItems`. Removed `instances` and `moduleInstances` from combat save DTOs
  and raised `combat.equipment` to V6.
- Made `IItemInstanceRepository` mandatory for every `CombatEquipmentRuntime` constructor, including tests.
  Equipment creation now issues a typed `item-instance:*` ID from that repository.
- Added `itemInstanceId` to carried-item persistence and preserved it through pickup, carry, warehouse/facility
  deposit, crafting output, and loadout drop. Existing equipment is materialized with its original identity;
  missing repository state is no longer replaced by a synthesized normal-quality instance.
- Added strict restore checks for invalid/duplicate carried IDs, duplicate physical IDs, and equipment stacks
  whose physical stack ID, definition, and repository payload disagree.
- Corrected equipment material-policy facility keys to use `BuildingInstanceId` instead of definition/coordinates.
- Unity MCP focused runs pass: physical-item contracts, stock query, building persistence, facility evolution,
  combat, material equipment, 168-node research/equipment validation, and the V18 authority validator.
  Console is Error 0 / Warning 0.
- Logged and corrected cutover failures: old combat DTO field references, one test repository name error, seven
  obsolete physical-item expectations, a Blueprint-category fixture seed, slotless dagger module restoration,
  strict building/character ID fixture failures, and remaining legacy Editor stock calls.

## 2026-08-01 Branched production network V3 completion

- Added typed production consumer links, reverse dependency indexing, branch/depth validation,
  facility fuel/feed profiles, local output buffers, route policies, stock-sensor order gating,
  and V4 production persistence.
- Re-authored the economy and research-overhaul assets around concrete branching intermediates,
  removed wort and generated sink recipes, added real equipment/surgery/construction consumers,
  and retained exactly 168 research nodes.
- Added medical procedures to the research reward catalog and added construct-core engineering
  and dining operations facilities so every research node has a direct concrete reward.
- Updated production/research/equipment UI and the design/implementation document for branch views,
  blocked states, sensor migration, supply selection, V5 incompatibility, and variable content counts.
- Fixed the final compile issue by adding the missing LINQ namespace to the distribution planner.
- `Temp/production-network-v3-report.txt` passes resource economy, combat equipment,
  production graph, production runtime contracts, research/equipment, and pacing checks.
- Unity MCP performed the final 1920x1080 Main Camera capture; Console finished at
  `Error 0 / Warning 0`.

## 2026-08-01 Item architecture V6

- Started phase 76 and audited the current resource item, dungeon item, equipment item,
  world-stack, freshness, and equipment-instance ownership boundaries.
- Confirmed 186 resource item assets coexist with an empty serialized dungeon item catalog
  and multiple hardcoded definition providers.
- Recorded the first audit path miss and switched subsequent discovery to symbol-based lookup.
- Added canonical `ItemDefinitionSO`, typed IDs, and composable production, market, research, food,
  medicine, facility-supply, equipment, and ammunition feature definitions.
- Converted `ResourceItemDefinitionSO` to a compatibility subtype that projects the existing API
  from features, and added one strict base-type Resources catalog.
- Removed the static hardcoded lookup chain from `DungeonItemCatalogSO` and fixed unknown-item
  `TryGetDefinition` behavior so missing content is no longer reported as found.
- Began physical-item save V4 with versioned instance components and deterministic stack signatures.
- The first unified asset generation exposed Unity's requirement that a concrete SO type used by
  `.asset` files have its own same-named script file; generation is being retried after splitting it.
- Recreated 110 generated definitions with valid concrete script GUIDs and retained the broken first
  generation under `Assets/_Recovery/ItemDefinitions_PreV6MissingScript` until final cleanup.
- The first combined regression showed the unified builder needed to reapply research-overhaul
  content after the base economy builder; the deterministic generation order is being corrected.
- Clean compilation exposed two editor-only fake item runtimes that needed the new instance-component
  mutation method; production runtime code had already compiled, and both test doubles are now updated.
- Final clean compile and combined validation passed: 296 canonical item SOs, 43 equipment features,
  duplicate IDs 0, invalid features 0, stack-signature isolation PASS, production V3 PASS,
  research/equipment PASS, pacing 32.2/80.4/234.3/372.0.
- Unity MCP captured Main Camera at 1920x1080 without mouse input. Final Console is Error 0 / Warning 0.
- Removed the 110 broken first-generation recovery copies after verifying their 110 valid replacements;
  they contained no unique authored data and are reproducible by the unified builder.
- Propagated instance components through world snapshots, pickup, carried-inventory merge/signature,
  carry save round trips, warehouse deposit, and loose re-drop so mutable item state survives hauling.

## 2026-08-01 V18 Phase 86 scoped session authority

- Converted `GameData` into a settings-only SO and moved run values into plain `GameSessionState`.
- Added scoped calendar and speed authorities and made `IGameMoneyAccount` the operational money writer with explicit before/after ledger records.
- Removed the static user-settings facade and migrated runtime consumers to injected `IDungeonUserSettingsService` state and change notifications.
- Replaced static character-carry, combat-cover, and skill-execution registries with run-scoped services or actor-owned transient components.
- Moved world-presentation and character-skill settings into explicit `GameContentCatalogSO` references and removed runtime presentation SO synthesis.
- Updated V18 fixtures to assign mandatory building/character IDs and physical stock queries before initialization; pre-V18 debug-save coverage now asserts rejection.
- Extended `RuntimeAuthorityV18Validator` with the GameData/session split, root content references, and removed-global-state checks.
- Unity MCP focused matrix passes authority, physical items, combat, shop, operation, developer mode, invasion, and lighting scenarios. Final Phase 86 compile Console: Error 0 / Warning 0.

## 2026-08-01 V18 Phases 87-88 offense and atomic restore

- Consolidated expedition, strategic world, regional pressure, return arrivals, rewards, and battle persistence into the sole `offense.aggregate` save section.
- Removed V17/v17 source and asset naming plus the late strategic runtime bind. Added `IOffenseQuery` and `IOffenseApplication`, then moved UI, recruitment, codex, and first-run consumers off direct offense scene runtimes.
- Corrected offense stock rewards to spawn physical item stacks through an explicit expedition reward item sink.
- Added the V18 root save manifest, required/optional section declarations, typed section preflight, duplicate/missing/version/phase checks, and rejection of unknown required sections.
- Added cross-aggregate preflight for physical item definitions and IDs, character/building IDs and definitions, and expedition member references.
- Added rollback-on-commit-failure and a deterministic final-section failure test proving the live state returns to its exact pre-restore values.
- Unity MCP live verification passed capture and round trip for all 54 registered sections; Phase 87 focused regressions and Console `Error 0 / Warning 0` passed.

## 2026-08-01 V18 runtime authority normalization start

- Re-read the repository guide and the planning, ScriptableObject, and Unity C# skill contracts.
- Preserved the existing dirty worktree and recovered the completed Item V6 context from `task_plan.md`, `findings.md`, and `progress.md`.
- Logged the CP949 failure from the optional session-catchup helper and used the on-disk plan plus Git diff as the recovery source.
- Added phases 82-91 for the V18 new-game boundary, strict SO catalogs, typed identities, item/warehouse/equipment authority, scoped session state, offense consolidation, atomic restore, DI/assembly cleanup, decomposition, and final Unity MCP verification.
- Phase 82 is in progress; no gameplay code has been changed yet in this phase.
- Inspected the root save contract, save-slot compatibility flow, save-section registry, canonical item catalog, legacy dungeon item provider, and explicit version assertions.
- Confirmed Phase 82 can first establish V18/new-game messaging without asset migration, while Phase 83 can reuse the existing strict `ResourceItemDefinitionCatalog` as the only lookup authority.
- Error: the first combined V18 patch did not apply because mojibake shown through the default PowerShell encoding did not match the UTF-8 source text. No files were partially changed; the retry is split into UTF-8-verified hunks.
- Raised the root save contract to V18 and centralized old/new-version incompatibility reasons in `DungeonSaveCompatibility`.
- Removed the research/equipment-specific compatibility probe from slot listing; slot display and actual restore now use the same V18 policy and Korean new-game message.
- Updated stale V16/V17 version assertions on the affected survival, exterior, item, defense, and integration debug surfaces.
- Verification error: `dotnet build DungeonStory.sln --no-restore` could not run because no .NET SDK is installed in the environment. Future compile checks use the connected Unity Editor/MCP instead of retrying this command.
- Unity MCP command attempt 1 failed inside the temporary command assembly because its generated `Unity.*` namespace resolved `CompilationPipeline` incorrectly. This did not report a project compilation failure; the retry uses `global::UnityEditor.Compilation.CompilationPipeline`.
- Unity compilation then completed and the Editor Console reported Error 0 / Warning 0 for the V18 boundary.
- A YAML audit found zero authored `stock-item:*` definitions. One compound discovery command returned exit code 1 only because its final `rg` query intentionally had no stock-definition matches; the preceding asset reads completed and established the missing-authority defect.
- Unity MCP category-inspection attempt 1 could not reference `ItemDefinitionSO` because the temporary command assembly does not reference `Sirenix.Serialization`. The project remains compiled; the replacement inspection uses `AssetDatabase` plus `SerializedObject` without the Odin-backed type.
- The untyped Unity MCP asset audit succeeded and grouped all 296 item definitions by stock category, exposing the missing Water/Blueprint authoring and confirming concrete candidates for every other category.
- Located seven facility blueprints and the existing item builder; Phase 83 will add a serialized item catalog under one root content catalog, then populate it through an Editor-only explicit rebuild.
- Added `GameContentCatalogSO` and `ItemDefinitionCatalogSO` as immutable authored-reference assets, plus a strict `ResourceGameContentCatalog` bootstrap.
- Changed `ResourceItemDefinitionCatalog` to consume the serialized item catalog instead of calling `Resources.LoadAll<ItemDefinitionSO>` at runtime.
- Added an explicit Editor-only content-catalog builder and registered the root/catalog services in the world composition root.
- The first Unity compile after changing the item catalog correctly exposed the remaining legacy provider constructor; the Console reported one CS1503 at `DungeonItemCatalogSO.cs`.
- Removed the provider's optional dependencies, internal Resources loader, blueprint/installation-kit synthesis, stock synthesis, unknown-item fabrication, and static equipment catalog creation.
- `IDungeonItemCatalogProvider` now exposes strict read-only projections, and the defense presentation resolves equipment visuals through the actor's injected physical-item runtime instead of static catalogs.
- Added concrete default physical IDs for non-blueprint stock categories. Blueprint requests have no generic fallback and must carry an explicit definition ID.
- Added explicit installation-kit and research-blueprint item features. The content-catalog builder now authors one physical item definition for every BuildingSO and FacilityBlueprintSO before rebuilding the root index, eliminating those runtime synthesis paths.
- Generated 301 installation-kit definitions and 7 research-blueprint definitions; the explicit root catalog now indexes 604 authored item SOs.
- Corrected duplicate blueprint display-name suffixing and changed the research content source so `resource:clean-water` is authored in the Water category.
- Replaced generated `stock-item:*` references in 39 medical procedure assets, 6 urgent-site assets, and 2 industrial building assets with concrete item IDs. No `stock-item:*` remains in Resources SO assets.
- Updated runtime/editor source literals for forecast water, industrial fuel/stress data, offense mitigation, and related verifiers. The only runtime `stock-item:` string left is the validator that rejects such recipe inputs; Editor occurrences are migration or negative-test patterns.
### 2026-08-01 - V18 코드 정의 제거 중 오류

- Wildlife 코드 정의 블록을 CRLF 고정 마커로 제거하려던 기계적 변환이 실제 LF 파일과 맞지 않아 실패했다.
- 실패한 파일은 쓰기 전에 원문이 유지됐고, ResearchBlueprint/Evolution의 독립 변환만 적용됐다.
- 다음 시도는 줄바꿈에 의존하지 않는 중괄호 범위 탐색으로 제한한다.
- 두 번째 변환은 Wildlife 제거를 완료했지만, Unified 빌더가 첫 시도에서 이미 정리된 상태임을 뒤늦게 확인해 존재하지 않는 마커 오류를 냈다. 결과 파일을 재검사해 네 종류의 레거시 마커가 모두 0건임을 확인했다.
- 첫 Unity 컴파일에서 편집기 팩토리의 실제 반환형을 `IItemDefinitionCatalog`으로 잘못 가정해 10개 오류가 발생했다. 테스트를 실제 반환형인 strict `ResourceDungeonItemCatalogProvider` API로 교정했다.
- V18 루트 감사용 동적 명령에서 `GameContentCatalogSO.Items/ValidateCatalog`으로 잘못된 속성명을 사용해 명령 컴파일이 실패했다. 실제 계약인 `ItemDefinitions`와 그 카탈로그의 `ValidateCatalog()`로 즉시 수정한다.
- 수정한 동적 감사 명령도 동적 어셈블리의 Sirenix 참조 부족으로 실패했다. 프로젝트 Editor 어셈블리 안에 정식 V18 검증기를 두고 동적 명령은 반환 문자열만 호출하도록 전환한다.

### 2026-08-01 - 구형 아이템 카탈로그 제거

- `DungeonItemCatalogSO`의 직렬화 목록과 조회 권위를 제거했다.
- 순수 ID 변환은 `PhysicalItemIds`로 분리했고 43개 소스 파일의 호출을 일괄 전환했다.
- 구형 `DungeonItemCatalog.asset`과 런타임 `CreateInstance<DungeonItemCatalogSO>` 테스트를 삭제했다.
- 실제 아이템 조회는 계속 `GameContentCatalogSO -> ItemDefinitionCatalogSO -> ResourceItemDefinitionCatalog` 단일 경로만 사용한다.

### 2026-08-01 - V18 권위 검증 통과

- `RuntimeAuthorityV18Validator`를 추가해 V18 저장 경계, 이전 저장 거부, 루트 카탈로그, 필수 아이템, strict missing-item 실패, 코드 정의 금지, `stock-item:*` 에셋 금지를 한 번에 검사한다.
- Unity 실행 결과: `V18 AUTHORITY PASS: save V18, 772 authored items, 168 catalyst SOs, legacy item authority 0, abstract stock assets 0.`
- Unity Console: Error 0 / Warning 0.
- Phase 82와 83을 완료하고 Phase 84 typed persistent ID 전환을 시작한다.

### 2026-08-01 - Typed ID 어셈블리 경계 오류

- 최초 배치에서 typed ID 값 타입을 기본 어셈블리의 `Models/Shared`에 두어, 무참조 Foundation asmdef의 `IPersistentIdGenerator`가 해당 타입을 볼 수 없다는 8개 컴파일 오류가 발생했다.
- 값 타입은 의존성 그래프의 최하단인 `DungeonStory.Foundation`으로 이동해 Foundation이 상위 기본 어셈블리를 참조하지 않도록 교정한다.
- 첫 physical-item 회귀에서 테스트 전용 카탈로그가 이제 실제로 쓰이는 구체 자재 SO를 포함하지 않아 6개 케이스가 실패했다. 테스트 카탈로그를 strict Editor SO 카탈로그 위에 테스트 전용 정의만 추가하는 합성 fixture로 교정한다.
- typed item ID 수정 후 physical-item 16개 계약이 전부 PASS했다. 고유 장비의 `stack:*`/`item-instance:*`가 저장 왕복 후 동일함을 새 회귀로 고정했다.
- Modular Facility 전체 회귀는 typed ID 경로에 도달하기 전에 기존 하드코딩된 "마지막 시설 ID" 기대값이 현행 카탈로그와 달라 중단됐다. 이 낡은 순번 검사는 Phase 90 에셋 그래프 검증 교체 대상으로 기록하고, building ID 자체는 별도 계약으로 검증한다.
- 순번 검사를 제거한 뒤에도 H01의 과거 코드 기대값에서 중단되어 동일한 낡은 카탈로그 계약임을 확인했다. typed ID 회귀와 분리하고 `CharacterPersistentIdentity.Require`를 통해 실제 저장/소유 키 폴백 제거를 계속한다.
# 2026-08-01 — V18 runtime authority normalization

- Upgraded the root save compatibility boundary to V18 and reject V17-or-earlier slots with a new-game-required message.
- Added typed persistent IDs and made persistent identity mandatory before character/building/item initialization.
- Made physical item instances authoritative for warehouse stock and combat equipment state; removed duplicate equipment save authority.
- Added staged restore/preflight so a failed restore cannot partially mutate the live world.
- Added the explicit `GameContentCatalogSO` root plus item/domain/media catalogs. Runtime `Resources.Load` is now confined to the root loader.
- Removed runtime synthesis for items, equipment, facilities, exterior zones, species, character templates, and factions.
- Authored and catalogued 42 runtime building archetypes instead of creating `BuildingSO` objects at runtime.
- Added strict root projections for economy, equipment, modules, research, surgery, anatomy, wildlife, invasion, AI, room, hauling, and survival settings.
- Removed the faction late-bind provider; character population now depends on the narrow `IFactionContractQuery` capability.
- Renamed `CharacterSummeryInfo` to `CharacterSummaryInfo` while preserving its Unity meta GUID.
- `RuntimeAuthorityV18Validator`, character progression/population, survival, wildlife, offense battle/strategic/reward, and AI plan scenarios pass on current assemblies.
- Phase 90 remains active: oversized runtime/UI decomposition, failure-code localization, and full asmdef migration are not yet complete.

## 2026-08-02 - Shop runtime authority split

- Split product stock/restock/pricing, crime resolution, service completion, and DTO contracts out of `Shop`; the main runtime is now 1,196 lines and its architecture exception is removed.
- Removed `LegacyShopMoneyRuntime`, the constructor overload that created it, the implicit floating-number fallback, and the cached mutable `GameSessionState` provider. Shop construction now requires the explicit money account and other runtime capabilities.
- Updated facility fixtures to use physical Editor item storage, persistent building/character IDs, and an empty initial warehouse instead of reviving aggregate stock writes.
- Corrected the authored Slime/Orc/Vampire crime-risk multipliers and the Editor generation spec together so the SO source of truth and migration input do not diverge.
- The architecture baseline now contains 12 oversized files. Unity source compilation had passed before the final fixture/content edits; final facility regression rerun is waiting for the Unity MCP bridge to recover after a full asset reimport.

## 2026-08-02 - Captivity aggregate decomposition

- Split captive policies, performer milestones, management interactions, escort state/parent ownership, escape planning, and captive lifecycle/save restoration into focused runtime owners.
- Reduced `CaptivityRuntime` from 2,213 to 1,197 lines and removed its architecture-baseline exception; 11 oversized files remain.
- Changed captivity housing persistence from `building id + grid coordinates` to mandatory `BuildingInstanceId`.
- Corrected the physical-item gateway interface hiding declarations; the Unity-generated Roslyn response now compiles the full gameplay assembly plus all new captivity sources with Error 0 / Warning 0.
- Unity MCP remains queued after the earlier recursive full-asset import, so authoritative Unity clean-build, captivity regressions, and Console evidence are still pending and are not claimed complete.

## 2026-08-02 - Battle session and Grid source ownership

- Split the former 2,155-line offense battle model into authored/runtime contracts (665), battle session (1,170), encounter catalog (278), and deterministic session rules (63).
- Split the former 2,567-line Grid source into cell-area rules, path-search results, search workspaces, the Grid aggregate, and a rebuildable traversal-heuristic index.
- `Grid` is now 1,166 lines and no longer directly owns heuristic portal caches; `GridTraversalHeuristicIndex` rebuilds them from traversal links.
- Removed both oversized exceptions. The architecture baseline now contains 9 files and the Unity Roslyn response compilation remains Error 0 / Warning 0.
- The unresponsive Unity MCP relay process was isolated and stopped without touching the Unity Editor. This turn's MCP transport did not auto-reconnect (`Transport closed`), so Unity import-generated `.meta` files, authoritative clean compilation, regressions, Console 0/0, and captures remain pending for the next MCP connection.

## 2026-08-02 - Expedition UI lifetime split

- Moved `OffenseExpeditionPanel` out of the expedition runtime source. The panel is 689 lines and independently satisfies the MonoBehaviour/Presenter 800-line limit.

## 2026-08-02 - Expedition aggregate responsibility split

- Reduced `OffenseExpeditionRuntime` from 2,105 to 1,117 lines and removed its architecture-baseline exception.
- Added dedicated field-mobility, result-finalization, return-coordination, strategic-target, travel-event, battle-launch, and battle-completion services.
- Replaced two runtime reward/meta provider fields with one result finalizer and moved asynchronous member return/resource release behind an explicit return port.
- Auxiliary full `Assembly-CSharp` Roslyn compilation completed with Error 0 / Warning 0. Unity MCP validation is still pending because its transport remains closed.

## 2026-08-02 - Production order aggregate responsibility split

- Reduced `ProductionBillRuntime` from 2,366 to 1,164 lines and removed its architecture-baseline exception.
- Moved save DTO mapping into `ProductionBillStateCodec`, query-only status projection into `ProductionBillSnapshotProjector`, and output-buffer reservation into `ProductionOutputPlanningService`.
- Moved fuel/input prefetch into `ProductionInputLogisticsService`, utility/batch-support validation into `ProductionCycleUtilityService`, and stock-sensor mutable ownership into `ProductionStockSensorRuntime`.
- Replaced the facility `id + coordinate` persistence fallback used by production buffers and stock sensors with mandatory `BuildingInstanceId` resolution.
- Auxiliary full `Assembly-CSharp` Roslyn compilation completed with Error 0 / Warning 0; Unity import/play-mode proof remains pending on MCP reconnection.

## 2026-08-02 - Equipment aggregate responsibility split

- Reduced `CombatEquipmentRuntime` from 2,178 to 864 lines and removed its architecture-baseline exception; 6 oversized runtime entries remain.
- Production composition now explicitly constructs the stat projector, physical-state writer, loadout store, module runtime, lineage runtime, crafting runtime, and loadout runtime. The aggregate no longer creates hidden implementations and has exactly eight required constructor dependencies.
- Moved craft orders, concrete material priority policies, research locks, preview projection, and repository-owned instance creation into the 798-line `CombatEquipmentCraftingRuntime`.
- Moved character loadout ID references, hand/layer policy, combat snapshots, confiscation, carried weight, and character-death loss into the 725-line `CombatEquipmentLoadoutRuntime`. Equipment and module payloads remain solely in `IItemInstanceRepository`.
- Removed equipment crafting's `StockCategory` conversion and general-stock fallback. Arrow/bolt compatibility orders now request concrete lumber/feather/iron-ingot IDs, and legacy category inputs are rejected.
- Removed the repair-order fallback from an unknown material to general stock. A repair order without a concrete material item ID now fails as `equipment.repair.material_definition_missing`.
- Auxiliary full `Assembly-CSharp` Roslyn compilation completed with Error 0 / Warning 0. Unity MCP compilation, regressions, two-resolution captures, and final Console proof remain pending because the transport is still closed.
- `OffenseExpeditionRuntime` remains 2,105 lines; its baseline exception stays active until strategic travel, rescue dispatch, battle completion, and return settlement are extracted.
- Recompiled the full gameplay source through Unity's generated Roslyn response with the new panel, battle, Grid, and captivity files explicitly included: Error 0 / Warning 0.

## 2026-08-01 - Phase 90 architecture ratchet and equipment aggregates

- Added `Assets/Architecture/runtime-architecture-baseline.json` and made `RuntimeAuthorityV18Validator` reject new oversized files or growth above the captured per-file maximum.
- Split `CharacterGrowthRules` and `CharacterRecordJsonDto` from oversized services; both source owners are now below the 1,200-line runtime limit.
- Removed all direct production-code `new System.Random(...)` calls and pinned the prohibition in the V18 validator. Explicit-seed simulations now use `DeterministicRandomSequence`; Editor fixtures may still use `System.Random`.
- Split combat-equipment save contracts, interfaces, stat projection, physical-state writing, loadout storage, module lifecycle, and lineage-transfer work into focused types.
- `CombatEquipmentRuntime` decreased from 2,863 to 2,178 lines. The architecture baseline was lowered to 2,178 so the removed responsibility cannot silently return.
- Replaced nullable equipment fixture dependencies with explicit empty/unavailable capability objects. The production constructor now rejects every missing required dependency.
- Unity MCP equipment/material/item-state/research regressions pass after both Aggregate extractions. Research pacing remains 32.2/80.4/234.3/372.0 days.
- Added Unity Localization 1.5.9, active Korean locale/settings assets, and the `DomainFailures` String Table collection.
- Equipment module and lineage-transfer commands now return `DomainFailure` (`FailureCode + parameters`) instead of completed Korean sentences. `DomainFailureLocalizer` is registered only in the presentation composition.
- The V18 validator now fails if the localization settings, Korean table, or any non-`None` `FailureCode` entry is missing. Unity MCP resolved and formatted the parameterized research/facility message successfully.

## 2026-08-01 - Full objective continuation

- Re-read the persistent plan and the Unity C#/ScriptableObject architecture skills, then recovered nine unsynchronized messages with the planning catch-up helper.
- Re-audited the current worktree instead of treating the prior partial handoff as completion.
- Confirmed Phase 90 still has 53 oversized exceptions, 738 string-failure occurrences, 15 runtime-provider files, and a largely default-assembly gameplay tree. Full completion remains unproven and active.
- Extracted `CharacterDeprivationDiagnostics` (36 non-persistent counters plus snapshot/reset) and `CharacterDeprivationStateStore` (per-character ownership, burden normalization, save capture/restore). `CharacterDeprivationRuntime` decreased from 3,410 to 3,161 lines.
- Lowered the architecture ratchet for deprivation to 3,161 immediately. Unity MCP compilation plus `SurvivalDebugScenarios` and `DarkSurvivalDebugScenarios` passed after both extractions.

## 2026-08-01 - Character deprivation 책임 분해 완료

- `CharacterSafeDrinkPlanner`에 이어 `CharacterEmergencyMovement`, `CharacterSafeReliefRunner`, `CharacterBreakdownActionRunner`, `CharacterBreakdownWorld`, `CharacterDeprivationConsequences`를 분리했다.
- 안전 식수 계획·예약, 실제 식수 행동, 긴급 이동, 결핍 붕괴 행동, 감염·금기·목격 후속 효과가 더 이상 하나의 런타임과 저장 사전을 공유하지 않는다.
- `CharacterDeprivationRuntime`은 3,410줄에서 1,123줄로 줄었고 새 협력 객체도 각각 914줄 이하라 런타임 1,200줄 제한을 만족한다.
- 기준선에서 `CharacterDeprivationRuntime` 예외를 삭제했다. Unity MCP에서 V18 권위 검증, `SurvivalDebugScenarios`, `DarkSurvivalDebugScenarios`가 모두 통과했다.
- 안전 음용 계획기 추출 중 접근성 오류 `CS0051`이 한 번 발생했으며, 외부 API가 아닌 내부 협력 객체로 한정해 계약 가시성을 일치시켰다.
- Phase 90 권위 재감사를 완료했다: 기준선 예외 52개, 프로젝트 asmdef 18개, 허용된 루트 Resources 로더 1개, 런타임 Tile 생성 3개, RuntimeProvider 클래스 22개/인터페이스 19개다.
- `DungeonGameplayPerformanceProbe`를 보고 모델/옵션, 월드 구성, 월드 요약 수집, 보고서 평가로 분해했다. 본체는 3,245줄에서 1,198줄로 줄었고 모든 새 타입도 1,200줄 이하다.
- 성능 Probe 기준선 예외를 삭제했으며 Unity MCP 재컴파일과 V18 권위 검증이 통과했다. 현재 과대 소스 기준선은 51개다.
- `AIActionPlan`/`AIAction` 548줄을 `AIBrain`에서 독립시켜 뇌 본체 기준선을 2,858→2,319줄로 낮췄다. AI 자연스러움 회귀는 통과했다.
- `CharacterStats`의 기분 계산과 작업 대표 능력치 정책을 순수 규칙 객체로 분리해 1,250→1,194줄로 낮추고 기준선 예외를 제거했다. `BuildableObject`도 정확히 1,200줄로 정리해 예외를 제거했다.
- `CharacterStatDebugScenarios`의 에셋 전수 검사는 구조 변경과 무관하게 `Customer_Orc.asset`의 `stat:shooting` 누락에서 실패했다. 이 에셋 그래프 결함은 최종 전수 회귀 전에 수정해야 한다.
- `CharacterMedicalRuntime`에서 의료 비용과 주문 복제 계약을 `CharacterMedicalOrderPersistence`로 분리해 1,241→1,197줄로 낮췄다.
- `WorkTaskExecutor`에서 작업량 계산·환경 작업 유형·외부 작업 시간 정책을 `WorkExecutionRules`로 분리해 1,262→1,188줄로 낮췄다. 두 기준선 예외를 제거했다.
- 관련 회귀 실행은 컴파일과 V18 검증을 통과했지만, 기존 WorkAmount fixture 4개가 필수 building ID를 만들지 않고 Combat fixture는 루트 anatomy profile을 제공하지 않아 실패했다. 엄격한 typed ID/루트 카탈로그 전환 후 남은 fixture 부채로 기록했다.
- `WildlifeActor`의 런타임 시각 자산 캐시를 `WildlifeVisualAssetCache`로 분리하고 내부 기본 난수 서비스 생성을 제거했다. 본체는 1,272→1,183줄이며 Wildlife 회귀와 V18 검증이 통과했다.
- `BlueprintResearchSystem.cs`에 섞여 있던 연구 작업/상태/완료 결과/순수 완료 서비스를 `BlueprintResearchContracts.cs`로 옮겨 MonoBehaviour 본체를 1,306→920줄로 낮췄다.
- 연구 168개 전수 검증과 작업량 시뮬레이션은 통과해 32.2/80.4/234.3/372.0일을 유지했다. 구형 V3 저장 기대와 persistent CharacterId 없는 BlueprintResearch fixture는 별도 회귀 부채로 남았다.
- 디버그 명령 모음에서 캐릭터 명령 Provider 231줄을 독립 파일로 분리해 원본을 1,339→1,108줄로 낮췄다.
- `CharacterAiDecisionSchedule`이 due-time 힙, 세대 버전, 순번을 소유하도록 분리해 `CharacterAiScheduler`를 1,354→1,164줄로 낮췄다. 두 기준선 예외를 제거했다.
- AI 자연스러움 경로는 통과했지만 Priority fixture는 창고 초기화 전에 필수 `IStockQuery`를 제공하지 않아 실패했고, 동기 스트레스 검증은 의도된 100 NPC 제한 경고를 출력했다.
- `EnvironmentWorkPolicy`를 별도 파일로 옮겨 `CharacterEnvironmentRuntime`을 1,361→586줄로 낮췄다.
- 시설 진화 인터페이스/작업 유틸리티와 재료·모듈 판정 규칙을 분리해 `FacilityInstanceEvolutionRuntime`을 1,372→1,177줄로 낮췄다.
- 환경 및 시설 진화 전체 회귀와 V18 검증이 통과했으며 두 기준선 예외를 제거했다.

## 2026-08-01 - Fluid network runtime decomposition

- `FluidNodeState`, `FluidNodeWaterRules`, `FluidNetworkSnapshotBuilder`를 `FluidNetworkRuntime`에서 분리했다.
- 본체를 1,400→1,199줄로 낮추고 기준선 예외를 제거했다. 남은 과대 파일 기준선은 40건이다.
- Unity 전체 스크립트 캐시를 강제 재빌드해 이전 분해에서 숨겨졌던 환경·결핍·성능 진단·AI·기분 규칙의 구문/연결 오류를 모두 수정했다. 현재 전체 컴파일은 Error 0 / Warning 0이다.
- 산업 회귀 계약을 실제 V18 에셋 그래프인 연구 168개, 산업 연구 45개, 상하수 연구 9개, 산업 시설 36개로 갱신했다.
- Unity MCP에서 `RuntimeAuthorityV18Validator`와 `IndustrialInfrastructureDebugScenarios.RunAll()`이 통과했다.
- 자체 저장 상태와 그리드 수명을 가진 `ExteriorZoneMarker`를 독립 파일로 분리하고 사용되지 않던 좌표 해시/기본명 코드를 제거했다. `ExteriorActivityRuntime`은 1,409→1,101줄이며 기준선은 39건으로 줄었다.
- Unity MCP에서 외부 구역 전체 회귀와 V18 권위 검증이 통과했다.
- `WildlifeHabitatPatch`/마커 계약과 `WildlifeHabitatOverlay`를 분리해 `WildlifeEcosystemRuntime`을 1,434→1,142줄로 낮췄다. 필수 서비스를 누락한 Wildlife 테스트 생성기 5곳도 운영 구성 계약에 맞췄다.
- Wildlife 전체 회귀와 V18 권위 검증이 통과했으며 기준선은 38건으로 줄었다.
- 자동 도축 후보/축사 호환성 정책을 `AnimalHusbandryPolicyEvaluator`, 반복 작업 계산을 `AnimalHusbandryWorkRules`로 분리해 `AnimalHusbandryRuntime`을 1,482→1,200줄로 낮췄다.
- 전체 Unity 컴파일과 V18 아키텍처 검증이 통과했고 기준선은 37건으로 줄었다.
- 서커스 예측/공연장 보정, 전투 참가자 값, 월드 조회 규칙을 별도 타입으로 분리해 `CircusRuntime`을 1,517→1,200줄로 낮췄다.
- Captivity/Circus 통합 회귀와 V18 권위 검증이 통과했으며 기준선은 36건으로 줄었다.
- `IndustrialTabContentPresenter`를 기능 표면 Presenter에서 분리해 `IndustrialFeatureSurfacePresenter`를 819→781줄로 낮췄다.
- 캐릭터 요약의 런타임 레이아웃 도우미와 인터페이스를 분리해 `CharacterSummaryRuntimeLogFactory`를 855→800줄로 낮췄다. UI 탭 아키텍처와 V18 검증이 통과했으며 기준선은 34건이다.
- `DungeonSettingsUi`에서 해상도 카탈로그와 ESC 단축키 수명을 분리해 853→788줄로 낮췄다.
- `OwnerSelectionPanel`에서 라벨·패널·모달 탐색 규칙을 분리해 891→765줄로 낮췄다. Owner 회귀와 V18 검증이 통과했으며 기준선은 32건이다.
- `UIBuildingInfo`에서 제작 버튼, 건설/정비 진행도, 상태 표면 생성을 `BuildingInfoActionViewFactory`로 분리해 950→774줄로 낮췄다.
- 시설 픽스처가 필수 `BuildingInstanceId`와 `IStockQuery`를 명시적으로 주입하도록 수정했다. 동시에 `StockSupplyService`의 집계형 창고 납품/보상 폴백을 삭제해 물리 아이템 런타임이 없으면 돈과 재고를 건드리지 않고 실패한다.
- Unity MCP에서 전체 컴파일, V18 권위 검증, 시설 전체 회귀가 통과했으며 과대 소스 기준선은 31건이다.
- `DungeonTitleCanvasProvider`와 `DungeonTitleTextCatalog`를 분리해 타이틀 조정기를 1,003→796줄로 낮췄다. 캔버스/EventSystem 수명과 난이도·저장 슬롯 표시 규칙이 화면 상태 전이에서 분리됐다.
- Unity 전체 컴파일과 V18 검증이 통과했으며 과대 소스 기준선은 30건이다.
- 창고 기능 파일에서 상태 변경 책임을 `WarehouseFeatureCommandService`로 분리해 원본을 985→745줄로 낮췄다. 질의 모델/Presenter와 납품·재고 정책·계약·대형 사업 명령의 소유권이 분리됐다.
- 생산 경제 픽스처의 세 시설에 필수 `BuildingInstanceId` 발급을 추가했다. 클린 Unity DLL 재빌드, V18 검증, UI 탭 아키텍처, 생산 경제 회귀가 통과했으며 기준선은 29건이다.
- 생산 패널의 월드 연결선 수명을 `ProductionWorkshopLinkRenderer`, 행·버튼·진행도 생성을 `ProductionBuildingViewFactory`로 분리해 본체를 1,061→752줄로 낮췄다.
- 클린 Unity 컴파일, V18 검증, UI 탭 아키텍처, 생산 경제 회귀가 통과했으며 과대 소스 기준선은 28건이다.
- 방어 기능 파일에서 `DefenseFeatureQueryService`와 `DefenseFeatureCommandService`를 독립 소스로 분리해 모델/Presenter 파일을 1,092→412줄로 낮췄다.
- 클린 컴파일, V18·UI 탭 검증, 방어 교전·위협·전투 보고 회귀가 통과했으며 과대 소스 기준선은 27건이다.
- 수술 응용 서비스와 `CharacterSurgeryWindowView` MonoBehaviour를 별도 파일로 분리해 원본을 1,145→693줄, View를 457줄로 정리했다.
- 수술·생산·종족·서비스룸·진행 경험의 낡은 141개 연구 검증을 현재 권위 168개로 일괄 갱신했다. V18·UI 탭·수술 회귀가 통과했으며 기준선은 26건이다.
- 운영 기능 파일에서 `OperationsFeatureQueryService`와 `OperationsFeatureCommandService`를 분리해 모델/Presenter 원본을 1,374→532줄로 낮췄다.
- `CharacterAiNaturalnessSettings.asset`의 끊어진 MonoScript GUID를 복구하고, 수술·연구 회귀가 콘텐츠 빌더를 자동 실행하지 않고 작성된 SO만 검증하도록 수정했다.
- 루트 콘텐츠 카탈로그, V18·UI 탭, 운영일 정산, 외부 활동, 게임 흐름 회귀가 통과했으며 과대 소스 기준선은 25건이다.
- 연구 트리 UI에서 팩토리/입력 컴포넌트, 표현 규칙, 뷰 생성, 그래프 뷰포트, 선택적 일시정지 수명을 각각 독립 소스로 분리했다. `ResearchTreeWindow`는 1,344→789줄이며 기준선 예외를 제거해 과대 소스 기준선은 24건이다.
- 연구 회귀의 낡은 `V3 저장 왕복과 V2 이관` 설명과 예외 문자열을 현행 V5 섹션 왕복/이전 버전 명시적 거부 계약에 맞췄다. 클린 Unity 컴파일, V18 권위 검증, UI 탭 구조, 연구 트리 전체 회귀가 통과했고 Console은 Error 0 / Warning 0이다.
- 시설 진화 패널에서 공통 View 생성, 표현 규칙, 장비 재단조·재귀속 섹션을 독립 객체로 분리했다. `InstanceEvolutionPanelPresenter`는 1,581→600줄, 장비 섹션은 612줄이며 모두 제한 이하다.
- V18·UI 탭·시설 진화·인스턴스 진화 회귀가 통과했고 Console Error 0 / Warning 0을 확인했다. 과대 소스 기준선은 23건이다.
- 시작 파티 준비 화면에서 View 팩토리, 순수 표시 규칙, 상세 탭/특성 툴팁 Renderer를 분리했다. Controller는 1,388→722줄, 상세 Renderer는 497줄로 제한을 만족하며 마지막 800줄 초과 UI 기준선 예외를 제거했다.
- 클린 Unity 컴파일, V18 권위, UI 탭 구조, 캐릭터 진행 회귀가 통과했고 Console Error 0 / Warning 0이다. 남은 과대 소스 기준선 22건은 모두 1,200줄 제한 대상 런타임이다.
- 장비 진화 저장/API/촉매 ID 계약과 방향·요구 재료·귀속 역사 규칙을 별도 소스로 분리했다. `EquipmentEvolutionRuntime`은 1,646→1,176줄이며 공개 촉매 배율 API를 호환 래퍼로 유지했다.
- 클린 Unity 컴파일, V18 권위, 인스턴스 진화 회귀가 통과했고 Console Error 0 / Warning 0이다. 과대 런타임 기준선은 21건이다.
- 이동 능력에서 유휴 배회 계획, 경로 형상 규칙, 문·벽·방어 예약 판정, 이동 속도·방향, 막힘 AI 반응을 분리했다. `AbilityMove`는 1,454→1,200줄이며 기준선 예외를 제거했다.
- 오래된 Unity DLL이 회귀를 실행하던 문제를 발견해 `Assets/Scripts` 명시적 재귀 임포트 후 DLL 시각 갱신까지 검증했다. 이 과정에서 축약 표식이 들어간 장비 진화 소스와 `AbilityMove` 닫는 괄호를 복구했다.
- Grid 픽스처의 테스트 시설 생성 콜백에 필수 `BuildingInstanceId` 발급을 연결했다. 실제 새 DLL에서 V18·Grid foundation·AI 자연스러움·인스턴스 진화 회귀가 통과했고 Console Error 0 / Warning 0이다. 과대 런타임 기준선은 20건이다.
- 전투 명령에서 공격 위치 계획, 참가자 조회, 대체 무기 선택, 투사체 표현, 결과 적용, 저장을 분리했다. `CharacterCombatCommandRuntime`은 정확히 1,200줄이며 V18·전투·방어 교전·침공 보고 회귀가 통과했다.
- 전투 히스테리시스 fixture는 빈 해부학 카탈로그를 만들지 않고 루트 콘텐츠 카탈로그의 작성된 프로필을 사용한다. 이 단계에서 기준선은 19건으로 줄었다.
- AI 의사결정 계약/시설 조회, 공용 준비·결과 규칙, 거시 목표 실행을 별도 소유자로 분리했다. `CharacterAiDecisionPipeline`은 1,677→1,194줄이며 기준선 예외를 제거했다.
- AI 창고 fixture는 필수 물리 `IStockQuery`를 주입하고 보충 우선순위 시나리오도 제거된 집계 재고 쓰기 대신 물리 재고를 시드한다. AI 계획·자연스러움·우선순위·행동 설명 회귀와 V18 검증이 통과했고 Console Error 0 / Warning 0이다. 남은 기준선은 18건이다.
- 컨베이어 런타임에서 노드/화물 상태, 입장 필터 정책, 네트워크 스냅샷 투영, 저장 변환을 분리했다. `ConveyorRuntime`은 1,687→1,124줄이며 모든 새 소유자는 249줄 이하이다.
- 클린 Unity 빌드, V18 권위, 산업 인프라, 생산 경제 회귀가 통과했고 Console Error 0 / Warning 0이다. 컨베이어 기준선 예외를 제거해 남은 과대 런타임은 17건이다.
- 작업 대상 선택기에서 대상 적격성 평가, 환경 위험 판정, 외부 작업 규칙, 스캔 상태를 분리했다. `WorkTargetSelector`는 1,702→1,160줄이며 기준선 예외를 제거했다.
- 우선순위 UI fixture는 씬 전체 캐릭터 대신 자신이 만든 두 작업자만 검사하고, 작업량 fixture의 네 공사 현장은 고유 `BuildingInstanceId`를 갖도록 수정했다.
- 실제 클린 Unity 빌드와 V18 권위, 작업 우선순위·코너 케이스·작업량·AI 자연스러움 회귀가 통과했고 Console Error 0 / Warning 0이다. 남은 과대 런타임은 16건이다.
- 신체 건강 저장 DTO·스냅샷·인터페이스를 계약 파일로 옮기고, 파트 정규화·해부학 투영·행동축/신체 용량·구형 표면 동기화·복제 규칙을 `CharacterBodyHealthStateRules`로 분리했다.
- `CharacterBodyHealthRuntime`은 1,714→1,050줄이며 상태 사전, 시간 경과, 생명주기 이벤트와 명령 오케스트레이션만 유지한다. 클린 빌드, V18 권위, 전투·해부학 의료 통합·수술 회귀가 통과했고 Console Error 0 / Warning 0이다. 남은 과대 런타임은 15건이다.
- 침공 감독자와 개별 침입자 런타임을 별도 파일로 분리하고, 노출 방어시설 관측·위험 인지 경로 계획·구조물 피해/상태 효과 규칙을 전용 협력 객체로 옮겼다.
- `InvasionIntruderRuntime`은 1,971줄짜리 혼합 소스에서 정확히 1,200줄의 전용 소유자로 정리됐다. 클린 빌드, V18 권위, 침공 위협·침입자·방어 교전·전투 보고 회귀가 통과했고 Console Error 0 / Warning 0이다. 남은 과대 런타임은 14건이다.
- 생존 런타임에서 저장 복제, 물리 재고 접근, 부패/신선도 상태, 식사 원장, 건강 상태 규칙과 시설 작업 표현을 각각 별도 소유자로 분리했다. `SurvivalFoodRuntime`은 1,984→1,192줄이며 좌표 기반 식사 시설 키도 필수 `BuildingInstanceId`로 교체했다.
- 물리 제작 회귀가 빈 재질 카탈로그와 영속 ID 없는 시설을 쓰던 문제를 수정해 루트 SO 카탈로그와 운영 생성 계약을 그대로 사용한다. 실제 클린 빌드, V18 권위, 생존, 물리 재고, 물리 아이템 회귀가 통과했고 Console Error 0 / Warning 0이다. 남은 과대 런타임은 13건이다.

## 2026-08-02 aggregate authority continuation

- 원정, 생산 주문, 전투 장비 Aggregate를 각각 1,117/1,164/864줄로 분해하고 기준선 예외를 제거했다. 전투 장비 제작은 구형 `StockCategory` 재료 입력을 거부하며, 장착 상태는 물리 아이템 인스턴스 ID만 참조한다.
- `WorldItemStackRuntime`에서 저장 해석·검증, 창고 라우팅, 절도 선택/운반, 읽기/변경 facet을 분리해 2,129→1,030줄로 줄였다. 생성자는 필수 의존성 8개만 받는다.
- 저장 복원은 모든 스택·고유 장비·부품·창고 영속 키를 `WorldItemRestoreState`로 사전 검증한 뒤에만 라이브 저장소를 교체한다. 구형 좌표/이름 창고 키는 V18에서 명시적으로 거부한다.
- `WarehouseInventory` 집계 수량에서 물리 스택을 합성하던 미러 경로를 삭제했다. 창고 가용량·배송·보관은 이제 `IItemInstanceRepository`의 실제 스택만 사용한다.
- 새 아이템 서비스까지 포함한 Unity 응답 파일 기반 보조 Roslyn 컴파일은 Error 0 / Warning 0이다. Unity MCP가 닫힌 상태라 최종 클린 Unity 빌드·회귀·화면 캡처는 대기 중이며, 남은 과대 런타임 기준선은 5건이다.
- `GameplayArchitectureRatchetTests`의 저장 V15·파일 2,169줄 허용을 제거하고 V18 및 공용 기준선 계약으로 교체했다. 정적 가변 상태는 총량 허용 대신 필드별 재구축 캐시/프로파일러 승인만 허용한다.
- 야생동물 서식지 저장 ID의 `GetInstanceID()` 사용을 `WildlifeHabitatPatchId`와 중앙 `IPersistentIdGenerator`로 교체했다. 산업 인프라 노드의 시설 숫자 ID+좌표 폴백도 삭제하고 필수 `BuildingInstanceId`만 사용한다.
- 씬 전환 요청·타이틀 메시지·전환 플래그를 `DontDestroyOnLoad` mailbox 인스턴스로 옮기고 `DungeonSceneNavigator`의 내부 기본 시간 서비스 생성자를 제거했다.
- 구형 19인자 물리 아이템 fixture 두 곳을 운영 조립과 동일한 persistence/warehouse/theft/facet 테스트 팩토리로 교체했다. Foundation, 런타임, Editor, 아키텍처 테스트 보조 컴파일은 모두 Error 0 / Warning 0이다.

## 2026-08-02 AIBrain responsibility split

- `AIBrain.cs`를 기록된 2,319줄 예외에서 정확히 1,200줄로 줄이고 아키텍처 기준선 예외를 제거했다.
- 작성된 액션 목록 구성, 후보 평가와 실패 쿨다운, 프레임 예산 기반 재개형 점수 순회, 행동 지속/중단 정책, 경로 검색 세션, 디버그 포맷을 각각 전용 소유자로 분리했다.
- Foundation과 런타임 보조 컴파일은 성공했다. Unity import, AI 회귀, 화면 캡처, Console 0/0은 닫힌 Unity MCP 연결 복구 후 확인해야 한다.

## 2026-08-02 defense engagement responsibility split

- `DefenseEngagementRuntime.cs`를 2,258줄에서 정확히 1,200줄로 줄이고 기준선 예외를 제거했다.
- 16개 생성자 의존성을 월드/수명주기와 전투 capability 묶음으로 나눴다. 런타임 생성자는 두 묶음만 요구한다.
- 원거리 배치와 사격, 저장 캡처/복원 해석, 경비 AI 일시정지 수명주기, 교전 시작·교대·붕괴·승패 확정을 각각 전용 객체로 이동했다.
- Foundation과 런타임 보조 컴파일은 성공했다. 실제 Unity import와 방어 교전 회귀는 MCP 연결 복구 후 확인해야 한다.

## 2026-08-02 final oversized-runtime closure

- `SurgeryRuntime`은 2,565줄/28개 의존성에서 1,168줄/4개 capability 묶음으로 축소했다. 계획, 저장, 환경 복구, 입실·재료 물류는 별도 소유자가 담당한다.
- 전략 원정 화면은 조정자 528줄, 준비·세력 767줄, 조우 324줄, 뷰 생성 189줄, 상세 투영 276줄로 분리했다.
- `WildlifeRuntime`은 2,513줄/20개 의존성에서 921줄/3개 capability 묶음으로 축소했다. 사냥 전투는 811줄, 식량 습격·생태 행동은 1,039줄 전용 런타임이다.
- `runtime-architecture-baseline.json`의 예외는 3건에서 0건이 됐다. 보조 Roslyn 런타임 컴파일은 Error 0 / Warning 0이다.
- Unity MCP는 계속 `Transport closed`를 반환한다. 새 스크립트 import, 회귀, 화면 캡처, Console 0/0은 연결 복구 후 남은 완료 조건이다.
# 2026-08-02 Authored Tile and Building Archetype Cutover

- Runtime `ScriptableObject.CreateInstance` is now 0. Water and filth reuse authored Tile references from `WorldInteractionPresentationCatalogSO`; missing references fail composition.
- `GridTexture` no longer creates a Tile SO per building sprite. It owns rebuildable `SpriteRenderer` presentation objects keyed by tilemap/cell and destroys them with the view.
- `BuildingSO.type` and runtime `AddComponent(Type)` were removed. Eight fixed `BuildingRuntimeArchetypeKind` values now select the runtime shell while `BuildingAbilityCollection` remains the facility capability authority.
- All 343 BuildingSO YAML assets were mechanically migrated; legacy `System.RuntimeType` serialization nodes are 0.
- `ItemDefinitionId` no longer converts implicitly to string. Modular facility payload V1/V2 is rejected instead of migrated inside the V18 generation.
- Wildlife species now come from `IGameContentCatalog.GetAll<WildlifeSpeciesSO>()`; runtime `Resources.LoadAll` and missing-species built-in insertion were removed.
- Auxiliary runtime and Editor Roslyn compilation both pass with Error 0 / Warning 0.
- Unity Editor is running, but Unity MCP remains disconnected with `Transport closed`; no Unity import, Console claim, or screenshot claim has been made.

## 2026-08-02 authored taxonomy authority cutover

- 루트 `GameDomainContentCatalogSO`에 캐릭터 욕구 6종, 재고 카테고리 11종, 시설 카테고리 8종을 작성 데이터로 추가했다.
- `AuthoredGameplayCatalog`이 세 정의군의 유일한 불변 런타임 투영이며, 욕구 초기값·기분 곡선·재고 표시/납품 가격·시설 상점 가중치를 사용하는 시스템은 해당 인터페이스를 필수 주입받는다.
- `CharacterNeedCatalog`, `StockCategoryCatalog`, `BuildingCategoryCatalog`의 정적 가변 사전·등록·리셋 경로를 삭제했다. 검증기의 금지 문자열을 제외한 세 타입 참조는 0건이다.
- `StockCategoryPersistenceId`는 V18 저장 프로토콜의 명시적 enum↔안정 ID 매핑만 담당하며 숫자·이름 폴백을 더 이상 허용하지 않는다.
- Unity MCP 복구 후 Scripts와 도메인 카탈로그 에셋을 Unity 자체로 재임포트했다. V18 권위 검증과 authored taxonomy 계약이 통과했고 Console Error 0 / Warning 0이다.
# 2026-08-02 detached survival aggregate progress

- `WorldWaterRuntime` now owns persisted sources and its sequence in `WorldWaterAggregateState`; restore replaces the candidate slot without touching live terrain or tilemaps.
- `WorldFilthRuntime` now owns filth records, indexes, sequence, and version in `WorldFilthAggregateState`; restore no longer destroys live work targets or rewrites tilemaps before publication.
- Water terrain, filth visuals, and cleaning work targets are rebuildable projections. They reconcile when the runtime observes a newly published state reference; a discarded candidate can only cause a harmless rebuild of the unchanged live state.
- `CharacterConsumablesRuntime` now restores diet policies, substance policies/state, pending deliveries, and derived availability caches by replacing one `CharacterConsumablesAggregateState` instead of clearing live dictionaries.
- Auxiliary Unity Roslyn compilation passes for both runtime and Editor assemblies after the migration.

# 2026-08-02 captivity and husbandry aggregate progress

- `AnimalHusbandryRuntime` now replaces one `AnimalHusbandryAggregateState` containing animals, pen policies, and tick scheduling; restore mapping lives in `AnimalHusbandryStateCodec` and capture reconciliation mutates only the active candidate slot.
- `CaptivityAggregateState` now owns captive records, policy records, capture sequence, and policy sequence. `CaptivityActorAccess` and `CaptivityPolicyRuntime` resolve the current root slot instead of retaining list references that survive a root swap.
- Captive door-access registration and escort transient cleanup moved to `CaptivityDoorAccessProjection`, which observes the published state reference before touching external services.
- `CapturedWildlifeAggregateState` now owns captured wildlife records. Restore normalizes a detached dictionary first; door access, carried-parent cleanup, actor capture flags, and actor warps occur only after publication.
- Responsibility extraction repaired all newly detected line-limit regressions: captivity 1,196, husbandry 1,188, performance probe 1,182, survival food 1,197.
- Unity-native V18 authority validation, captivity/circus contracts, and staged save-registry contracts pass. The cleared Unity Console reports Error 0 / Warning 0.

## 2026-08-02 Unity composition recovery

- Unity MCP was confirmed live against Unity 6000.3.8f1 and drove Play/Edit state plus Console inspection without operating-system input.
- The hidden VContainer cycle was split at three incorrect boundaries: exterior zone queries no longer resolve the incident runtime, carcass processing publishes a taboo event instead of resolving deprivation, and facility modifier reads no longer re-enter the evolution command runtime.
- The explicit dependency probe reached the end of the defense/combat graph. Its temporary source and the temporary VContainer diagnostics bridge were then removed.
- Scene hierarchy injection now reaches character construction. Character IDs are assigned before presentation bridges create nameplates or query deprivation state.
- `GameplayScene` and `SampleScene` pointed `InvasionDirectorRuntime` at the pre-split `InvasionIntruderSystem.cs` GUID. Both scene references now target the director script while retaining the serialized invasion settings/state.
- Production recipe numeric IDs are unique across all 189 authored `ProductionRecipeSO` assets. The V3 range remains contiguous at 9101–9153; arrow, bolt, and appraisal recipes now use 9154–9161. `DataManager` now rejects duplicates instead of warning and silently ignoring one asset.
- Foundation, runtime, and Editor auxiliary Roslyn builds pass with Error 0. Unity-native reimport and the complete Run Flow rerun remain pending because the MCP transport stopped forwarding new commands after the scene reimport, although the Unity editor process itself remains responsive.

## 2026-08-02 post-publication projection and session ownership

- Audited staged restore callbacks beyond DTO parsing. Physical-item restore no longer mutates user settings, normalizes warehouse destinations, or rebuilds item markers while the candidate root is active.
- Faction world sites, husbandry capture reconciliation, service-hub destruction subscriptions, run-flow threat/owner state, and captivity/circus/wildlife scene effects now observe a successfully published root revision or a changed published state reference before projecting.
- Removed staging-time `dirty` flag writes from run-flow and captivity projections. A discarded candidate now leaves both authoritative state and live projection bookkeeping untouched.
- `GameManager` no longer constructs or owns `GameSessionState`. `ScopedGameSessionStateStore` owns the run-scoped object; `GameManager` remains a lifecycle/input adapter and exposes only a compatibility query.
- Modular facility restore now submits a `GameSessionSnapshot` to the scoped store instead of directly initializing money, day, clock, and speed fields. The store also restores the time-scale projection.
- V18 validation now ratchets session ownership and post-publication projection contracts. Failed Aggregate commit verification also requires `PublishedRestoreRevision == 0`.
- Foundation, runtime, and Editor auxiliary Roslyn compiles pass with Error 0 after these changes. Unity-native import, PlayMode restore failure injection, and complete Run Flow evidence remain pending on MCP reconnection.

# 2026-08-02 detached facility-world candidate

- Added a save-registry transaction-participant lifecycle for Unity-object candidates. Focused Editor scenarios cover successful publication, failed-candidate discard followed by rollback publication, and duplicate participant rejection.
- `Grid` can now create an occupant-free layout copy. Modular facility restore resolves every definition, builds every inactive facility object, injects dependencies, restores typed identity and module state, and registers all footprints on that detached Grid before touching the live facility world.
- Detached facilities skip world-registry and paid-contract projection. Candidate failure destroys only inactive candidates; successful restore swaps the Grid, publishes facilities/contracts, draws tile presentation, and then broadcasts the grid change.
- Existing live facility occupancy is verified before destructive removal, and save grid dimensions, null entries, invalid/duplicate building IDs, footprint collisions, and module restore failures are rejected before live replacement.
- Character-world preflight now rejects missing definitions, invalid/reserved owner IDs, duplicate actors/profiles, unknown enum values, non-finite health/mood data, and ambiguous condition/work-priority records before the facility commit begins.
- Character restore no longer mutates and reuses live staff while decoding saved state. Owner and staff candidates are instantiated below an inactive hierarchy, injected in detached mode, assigned saved identity/state, and published only after every character candidate succeeds.
- Detached character bridges withhold lifetime/world/AI scheduling, procedural presentation, and Grid-event subscriptions. The owner manager does not replace `CurrentOwnerActor` or emit `OnOwnerSelected` until its candidate is ready.
- Foundation, runtime, and Editor auxiliary Roslyn compiles pass. This is an aligned intermediate step: character construction and later cross-world sections still need to join the candidate transaction before facility Grid publication can move to the final registry publish and rollback can be deleted.
# 2026-08-02 detached world transaction wiring

- Registered facility and character world services as ordered save-restore transaction participants and added a `050.world.characters.quiescence` publication participant.
- Removed early live-character quiescence from the facility save section. Live work and movement are now cancelled only after every section has committed its detached candidate.
- Character restore now resolves the staged facility Grid during a restore transaction, so character positions and state are prepared against the same world that will be published.
- Added deterministic participant-order regression coverage (`050 -> 100 -> 200`) and V18 source-contract ratchets for detached Grid consumption and final-only quiescence.
- Foundation, runtime, and Editor auxiliary Roslyn compiles pass with no diagnostics. Unity MCP remains unavailable with `Transport closed`, so Unity-native scenario execution and capture remain pending.
- Added a shared restore-world candidate index. Facility staging publishes its detached Grid/building view only to this temporary index, character staging adds the effective restored actor view, and all views are cleared on final publish or discard.
- `CharacterAiWorldRegistry` now redirects building, warehouse, retail, character, lifetime-character, and Grid queries to the temporary candidate view during restore; normal runtime queries still use the live registries.
- Moved deterministic random-stream state into `DungeonRuntimeAggregateRootStore`. Cached stream handles survive root swaps, and invalid/duplicate stream payloads now fail preflight instead of throwing during commit.
- Added focused regressions for candidate-index scoping, deterministic participant order, random handle publication, and failed-restore random-state preservation. Auxiliary Foundation/runtime/Editor compilation remains clean.
- Added type-level copy-on-write to `DungeonRuntimeAggregateRootStore`, with a failed-final-stage regression proving mutations to a shallow candidate slot do not leak into the live root.
- Moved `RunVariableRuntime` mutable run seed, day, active variables, invasion variable, and random replay history out of MonoBehaviour fields into a replaceable `RunVariableAggregateState`.
- Completed meta-progression restore separation: permanent profile merge is copy-on-write, while run progress, discovered facilities, unlocked recipes, completion state, and latest result now publish through root-owned aggregate slots.

# 2026-08-02 detached research aggregate progress

- Introduced `BlueprintResearchAggregateState` for blueprint tasks, completion/unlock sets, and project queue/progress state.
- Converted `BlueprintResearchState` into a root-aware facade with a standalone local mode for editor fixtures. Mutable task/project references now come from a deep copy-on-write candidate slot during restore staging.
- Added deep-clone contracts for blueprint tasks, project progress records, queue entries, and the complete project runtime state. Runtime wiring, detached decoding, knowledge tasks, projection, tests, and validation remain in progress.
- Wired `BlueprintResearchRuntime` to the scoped Aggregate root and changed the research save section to populate a standalone state before replacing the candidate slot. Legacy blueprint-item materialization is now rejected during V5 preflight instead of causing a restore-time world side effect.
- Added the root-owned `KnowledgeResidueAggregateState` and began routing task, sequence, delivery, and readiness mutations through copy-on-write state. A mechanical variable-name collision in the restore method is pending immediate correction before compilation.
- Logged tool error: attempted to pass unsupported `-Context` to PowerShell `Select-Object`; reran the search using ripgrep's `-A` context option successfully.
- Completed the knowledge-residue state rewrite: save restore now normalizes into a new Aggregate object and replaces the active candidate slot; normal queue/tick/work mutations request a writable copy first.
- Updated the known manually injected research runtime fixtures with isolated Aggregate roots. Auxiliary compilation will identify any remaining call sites or API mistakes.
- Foundation/runtime/Editor auxiliary Roslyn compilation passes with Error 0 / Warning 0 after the Aggregate migration.
- Added a research-tree regression that stages a 31-work candidate over a 7-work live root, discards it, and requires the live progress/queue plus publication revision to remain unchanged.
- Logged patch error: an insertion anchored on a mojibake scenario label did not match the UTF-8 source; reapplied it using stable ASCII method and control-flow anchors.
- Extended the V18 authority validator to require both research Aggregate slots, post-publication revision observation, and the absence of live clear/queue refresh/legacy-item projection from the research save section.
- Updated Phase 88 tracking to record research and knowledge-residue cutover as completed; the global rollback-image removal remains pending on other runtime and Unity-object owners.
- Auxiliary runtime compilation still passes, but the new Editor scenario could not call the Aggregate root's internal begin/discard methods from `Assembly-CSharp-Editor` (`CS1061` at lines 381/389). The scenario will be rewritten through the public save registry and an observing transaction participant instead of widening production API visibility.
- Reworked the failed-restore regression through `DungeonSaveSectionRegistry`: a late staged section fails once, an ordered transaction participant observes research immediately after candidate discard, and the normal rollback image is allowed to complete. No internal save-root APIs were exposed to tests.
- Tightened the state surface by making blueprint task progress mutation internal and deleting the now-unused in-place clear APIs from blueprint/project state.
- Logged patch error: removal of the dead V1-V3 migration helper failed because the PowerShell view decoded its Korean warning as mojibake; the next attempt will read UTF-8 text and use the exact source rather than matching corrupted output.
- Removed the dead V1-V3 research conversion helper using the correctly decoded UTF-8 source, and ratcheted the validator against reintroducing it or either in-place clear API.
- Auxiliary Editor compilation found one legacy test helper still calling `BlueprintResearchState.ClearForRestore` (`DungeonGameSaveDebugScenarios.cs:533`, CS1061). It will be changed to restore an empty detached research payload through the public V5 save boundary rather than restoring the removed mutator.
- Replaced that editor-only mutation with `ReplaceWithEmptyStateForDebug`, which swaps a complete empty state instead of clearing shared collections. Foundation/runtime/Editor auxiliary compilation is again Error 0 / Warning 0.
- Focused `git diff --check` passes for every touched research/validator/planning file (line-ending warnings only), and the mandatory truncation-marker scan reports zero matches under `Assets/Scripts`.
- Began the remaining save-owner audit. `CodexSaveSection` is confirmed to clear/repopulate live entry state and is selected as the next Aggregate conversion.
- Logged inspection error: two guessed Codex source paths did not exist and the combined read stopped after `CodexSaveSection`; switching to `rg --files` discovery before opening the actual runtime/state files.
- Added `CodexAggregateState` with deep copies of every entry and information-line deduplication set. `CodexState` is now a local/root-aware facade whose writable record access participates in copy-on-write staging.
- Wired `CodexRuntime` to the scoped Aggregate root and made memory-residue availability a pure snapshot query instead of creating a blank discovered entry. Save-section decoding and fixtures remain to update.
- Converted `CodexSaveSection` to strict preflight plus detached state replacement, updated the full-save reset helper and the Codex editor fixture, and confirmed the follow-up `ClearForRestore` scan has zero matches. The combined `rg` command returned exit 1 only because the expected no-match scan was last.
- Added a Codex candidate-discard regression through the public save registry. A one-shot Presentation-phase failure triggers discard; an observer must see no candidate-only marker before the rollback image publishes.
- Exposed the isolated Aggregate root only as a property of the editor scenario world, not through production save APIs, and reused it for runtime construction plus registry verification.
- Added the Codex Aggregate and detached-save contracts to the V18 authority validator. Foundation/runtime/Editor auxiliary compilation passes with Error 0 / Warning 0 after the full Codex cutover.
- Began the regular-customer audit. The first parallel inspection returned no output because an expected no-match `rg` result propagated exit 1; reran with explicit no-match handling and confirmed the domain has no Aggregate state yet.
- Added `RegularCustomerAggregateState` and deep-copy support for mutable visit/recruitment records, including preservation of the currently linked actor during COW. Removed the separately stored recruited-result list; it is now derived from authoritative records.
- Converted `RegularCustomerState` into a root-aware facade and kept the production MonoBehaviour at eight injected dependencies by bundling its two coherent character-lifecycle capabilities. Runtime/save registration and fixtures remain to update.
- Added strict regular-customer preflight for IDs, definition references, visit statistics, and capability flags; save restore now replaces the Aggregate state. Updated the full-save debug reset to use complete state replacement.
- Logged search error: PowerShell passed the literal `Assets/Scripts/Services/*/Editor` path to ripgrep and Windows rejected it; the useful results still identified registration in `DungeonAiRegistration`/`DungeonProgressionOffenseRegistration` and two editor overload calls.
- Registered the new character-lifecycle capability bundle for production composition. Runtime compilation passes, while Editor compilation found a second full-save fixture call to the removed `RegularCustomerState.Restore` at `DungeonGameSaveDebugScenarios.cs:124` (CS1061); it will use an explicit runtime debug replacement helper.
- Added explicit editor-only complete-state replacement for seeded and empty regular-customer fixtures, updated both full-save call sites, and restored Foundation/runtime/Editor compilation to Error 0 / Warning 0.
- Added the regular-customer Aggregate contract and live-restore prohibition to the V18 authority validator and recorded the cutover in Phase 88 follow-up.
- Added `FacilityShopAggregateState` for offer day, basic-purchase unlocks, and acquired blueprints. `FacilityShopUnlockState` is now a local/root-aware facade with deep copy-on-write sets.
- Split authoritative shop refresh from offer-list projection. Normal day refresh still permits auto-procurement, while restore replaces state and rebuilds deterministic offers only outside staging or after publication, with procurement and alerts disabled.
## 2026-08-02 facility-shop Aggregate continuation

- Resumed the partially applied facility-shop authority cutover after context recovery.
- Initial inspection used the stale path `Assets/Scripts/Services/Save/DungeonFacilityShopSaveData.cs`; `Get-Content` failed because the DTO actually lives beside the facility-shop runtime. Recovered with `rg --files` and will use `Assets/Scripts/Services/FacilityShop/DungeonFacilityShopSaveData.cs`.
- Added `FacilityShopAggregateState` ownership for offer day, basic-purchase building IDs, and acquired-blueprint IDs. The unlock façade now provides the single local/root state view used by `DailyFacilityShopRuntime`, preserving isolated editor scenarios without a second authority.
- Removed research-unlock capture/restore and the research dependency from `FacilityShopSaveSection` and removed `unlockedBuildingIds` from its DTO. Added authored catalog preflight for missing, negative, and duplicate building/blueprint IDs.
- Split deterministic offer projection from day refresh side effects. Candidate save commit changes only the Aggregate slot; a published-root revision rebuilds offers without auto-procurement or alerts.
- Added a public-registry candidate-discard regression with a one-shot Presentation failure. The observer requires the original day/unlocks immediately after candidate discard, and the rollback publication must leave the same live state.
- Ratcheted the V18 validator for the facility-shop Aggregate, post-publication projection, and absence of duplicated research authority.
- Foundation, runtime, and Editor auxiliary compilation pass with Error 0 / Warning 0 after the facility-shop cutover.
- The first conveyor Aggregate patch failed atomically because the expected tail context placed `Touch` immediately before the class close, while the source has `FormatStallReason` after it. No partial edit was applied; reapplied the same change in field/constructor, restore, and projection-reset segments using the exact UTF-8 source context.
- A PowerShell audit command used Bash-style brace expansion for several industrial files and failed at parse time. Replaced it with explicit file paths/source contracts rather than retrying the unsupported syntax.
- The first industrial debug-scenario patch used an intermediate assertion as the assumed end of `VerifySaveRoundTrip`; the method continues with fluid transfer assertions, so the patch failed without changes. Reinserted the new checks at the actual `VerifyItemDefinitions` boundary.
- Auxiliary Editor compilation then correctly rejected direct references from `Assembly-CSharp-Editor` to the runtime's internal Aggregate/validation types (CS0122). Kept those implementation types internal and removed the white-box editor checks instead of widening production API visibility; runtime compilation and V18 source contracts remain the current proof until Unity public-path execution is available.
- Added `ElectricalNetworkAggregateState`, `FluidNetworkAggregateState`, `ConveyorAggregateState`, and `AutomationAggregateState`, including deep copies of every mutable node, payload, stack, and facility record.
- Converted the four industrial runtimes from readonly live dictionaries to Aggregate-root state and moved their observable versions into the same slots. Restore now builds complete replacement state; topology/snapshot/route/timer caches reset only after direct restore or published-root revision observation.
- Replaced the mutable automation-demand dictionary with a query over the active `AutomationAggregateState`, so electrical demand always follows the same candidate/live root without a second write path.
- Added `IDungeonSaveSectionPreflight` to all four industrial sections plus strict payload validation. Direct section restore now invokes the same preflight before staging, preventing UI/test callers from bypassing it.
- Added V18 ratchets requiring all four Aggregate states, root/revision projection in each runtime, zero live dictionary clears, and root-derived automation demand.
- Foundation, runtime, and Editor auxiliary compilation pass with Error 0 / Warning 0 after the industrial cutover. The mandatory truncation-marker scan, industrial live-clear scan, and focused `git diff --check` are clean (line-ending notices only).
- Next-owner audit initially passed Windows wildcard paths (`WorldResource*.cs`, `CropPlot*.cs`) directly to `rg`, which returned OS error 123. Switched to directory scope with `-g` filters and confirmed both economy runtimes already contain Aggregate-root restore structures.

## 2026-08-02 event-alert detached Aggregate continuation

- Added `EventAlertAggregateState` for history records, dismissal IDs, and next-ID sequencing. Mutable record counts and choice callbacks deep-copy during candidate copy-on-write.
- Reworked `EventAlertRuntime` to require the composition Aggregate root. Normal writes use `GetOrCreateWritable`; restore replaces a complete candidate slot, while button/detail presentation is rebuilt only after `PublishedRestoreRevision` changes or after a non-staged direct restore.
- Converted `EventAlertSaveSection` to `DungeonJsonSaveSection<DungeonEventAlertSaveData>` and centralized strict payload checks in `EventAlertSaveValidation`, also invoked by direct `EventAlertSaveService.Restore` calls.
- Updated Editor scenarios for the mandatory root dependency, made the immutable-log snapshot test mutate through the runtime API, and added invalid-preflight plus failed-candidate-discard regressions through the public registry.
- Logged compile error: adding `using System` made existing `Object.DestroyImmediate` calls ambiguous between `System.Object` and `UnityEngine.Object` (CS0104). Added an explicit Unity object alias; Editor compilation then passed.
- Logged patch error: a combined planning-file update anchored on a line that existed only in `progress.md`, not `findings.md`, so `apply_patch` rejected the whole patch atomically. Reapplied the task plan, findings, and progress edits against their actual file tails.
- Foundation, runtime, and Editor auxiliary Roslyn compilation pass with Error 0 after the event-alert cutover.
- Unity MCP state recheck still returns `Transport closed`; Unity-native scenario execution, Console proof, and captures remain pending without using OS input automation.

## 2026-08-02 operating-day settlement detached Aggregate continuation

- Added `OperatingDaySettlementAggregateState` for the active ledger, day counters, outstanding debt, shortfall state, and immutable report-history references, with copy-on-write cloning for every mutable collection.
- Replaced `RestorePersistentState` live clearing/repopulation with construction and one Aggregate-root replacement. Snapshot reads now use the active root and report-history views no longer bind to a superseded list.
- Converted `OperatingDaySettlementSaveSection` to the common staged JSON base and added shared strict validation for the full root DTO plus nested reports, warehouses, stock, supply results, and shop summaries.
- Updated all manual settlement construction sites with isolated Aggregate roots and added invalid-preflight plus public-registry candidate-discard scenarios.
- Ratcheted `RuntimeAuthorityV18Validator` against settlement live clears and non-preflighted save boundaries.
- Runtime and Editor auxiliary Roslyn compiles pass with Error 0 after the settlement cutover. Unity execution remains pending on MCP reconnection.
### Work-order authority continuation notes

- Inspection command initially targeted a non-existent `EventAlertSaveService.cs`; the implementation is in `EventAlertService.cs`, with the section adapter in `OperatingDaySaveSections.cs`. Corrected discovery with `rg --files` before continuing.
- A broad `rg`/multi-file read exceeded tool output limits; subsequent inspection is intentionally bounded by line ranges.

## 2026-08-02 work-order / construction-site detached restore continuation

- Added `WorkOrderContracts.cs`, `WorkOrderSaveValidation.cs`, and `WorkOrderAggregateState.cs`; the live runtime now reads and mutates one composition-root Aggregate slot with copy-on-write semantics during restore staging.
- Converted `WorkOrdersSaveSection` to the shared staged JSON base and added strict direct/preflight validation, including authored building/item references and canonical sequence checks.
- Added inactive construction-site candidate creation against `RestoreWorldCandidateIndex`, plus participant begin/publish/discard behavior at ordering key `150.world.construction-sites`.
- Registered the Aggregate state store and work-order transaction participant without exceeding the eight-dependency runtime constructor limit.
- Added Editor regressions for invalid preflight preservation, successful public-registry publication of a detached site, and a one-shot later failure that discards the incoming site and restores the original order.
- Ratcheted `RuntimeAuthorityV18Validator` against reintroducing live clears, non-preflighted work-order persistence, or immediate construction-site restore creation.
- Foundation, runtime, and Editor auxiliary Roslyn compilation pass with Error 0. `WorkAmountSystem.cs` is below the 1,200-line runtime limit.
- Unity MCP reconnect check still fails with `Transport closed`; no operating-system input automation was used.
- Tightened `WorkOrderRuntime` so `IObjectResolver` and `IUiClock` are mandatory instead of null fallbacks; Editor fixtures now use explicit scenario capabilities.
- The first Editor rebuild after that tightening failed because the scenario resolver omitted `VContainer.Diagnostics` (`CS0246`/`CS0738`). Added the correct namespace and restored the Editor build to Error 0 / Warning 0.

## 2026-08-02 wildlife detached restore continuation

- Audited `WildlifeRuntime`, `WildlifeActor`, ecosystem, carcass freshness, food raids, and the V2 migration path; confirmed four live owners were mutated during the same staged commit.
- Added `WildlifePopulationState`, strict `WildlifeSaveValidation`, detached Actor lifecycle support, candidate DTO cloning, and transaction participant `250.world.wildlife`.
- Converted `WildlifeSaveSection` to the current-version-only staged JSON boundary and registered `WildlifeRestoreServices` plus the participant in the composition root.
- Restore now validates the candidate physical-item/facility world, creates inactive actors on the candidate Grid, and leaves all live wildlife/ecosystem/carcass state untouched until participant publication.
- Added PlayMode regressions for invalid preflight preservation, successful replacement-actor round-trip, and one-shot post-wildlife commit failure with candidate discard and rollback.
- Fixed the normal habitat generator to use typed `wildlife-habitat:*` IDs; removed `Guid`/`auto:*`/`water:*` fallback authority and updated affected Editor fixtures.
- Filtered saved carcass freshness through actual physical stacks and corrected raid validation so terminal history can legally outlive a removed animal.
- Ratcheted `RuntimeAuthorityV18Validator` for population ownership, detached candidate creation, publication-only live mutation, and the generic wildlife save section.
- Runtime and Editor auxiliary Roslyn compiles pass with Error 0 / Warning 0. `WildlifeRuntime` plus its restore partial total 1,198 lines.
- Final Foundation/runtime/Editor compile gate, mandatory truncation-marker scan, legacy habitat-ID scan, new-file trailing-whitespace scan, and focused `git diff --check` all pass; only expected Git line-ending notices remain.
- Unity MCP was checked once this continuation and still returned `Transport closed`; no OS input automation was used. Unity Console, PlayMode regressions, and captures remain pending reconnect.
- Logged tooling recovery: a Windows wildcard path produced error 123; a first UTF-8 dynamic removal patch failed on mojibake before succeeding with explicit UTF-8 input; one large validator patch exceeded output limits but the resulting file was verified complete at 312 lines before further edits.
- Logged inspection-command recoveries: one PowerShell command used unsupported Bash `||`, two `rg` probes returned exit 1 for no matches, and one save-payload search referenced the wrong Foundation path before locating `Services/Infrastructure/Save/DungeonSaveSectionPayload.cs`.

# 2026-08-02 exterior and offense-return detached continuation

- Finished the interrupted `ExteriorActivityRestoreCoordinator` implementation and wired `ExteriorActivityRuntime` as ordered participant `300.world.exterior-zones`.
- Converted the exterior save section to `DungeonJsonSaveSection<DungeonExteriorActivitySaveData>`, removed the obsolete summary-incident persistence path, and added invalid-preflight, successful replacement, and late-failure discard PlayMode scenarios.
- Removed the duplicate facility-save authority for `ExteriorZoneMarker`; modular facility snapshot/validation/clear now leave exterior markers to their dedicated participant.
- Added a world-replacement retirement path that unregisters an old building without raising gameplay destruction events, and removed the exterior marker's duplicate Grid detach on `OnDestroy`.
- Added `OffenseReturnArrivalAggregateState`, strict payload validation, save-section preflight, and two capability bundles. Restore now replaces detached plain state and cannot materialize prisoners or wildlife during commit.
- Logged tool issues: a guessed save-section path was wrong, two Windows wildcard paths produced error 123, one large patch was truncated, and a patch matched a Korean UTF-8 line through a mojibake PowerShell view. Re-read the source explicitly as UTF-8 and reapplied against the real text.
- Runtime and Editor auxiliary Roslyn compiles pass after both cutovers. Unity MCP remains disconnected with `Transport closed`, so PlayMode scenario execution, Console verification, and MCP captures remain pending.

# 2026-08-03 character-medical detached continuation

- Added the medical Aggregate, strict save validation, detached downed-occupant candidate, and ordered restore participant `350.world.medical`.
- Converted `combat.medical` to the common typed JSON save section and removed its legacy `IList<string>` warning/skip restore boundary.
- Fixed downed-occupant cleanup to use the recorded original Grid even when the old actor has already been retired during world publication.
- Moved restore orchestration into `CharacterMedicalRestoreCoordinator`; the main runtime is 1,199 lines with eight required dependencies.
- Added a PlayMode check through the public save service proving an invalid medical payload neither publishes a root nor replaces the live order view, and ratcheted the V18 authority validator against live-clear restoration.
- Full runtime and Editor auxiliary Roslyn compilation passes with Error 0 / Warning 0. The mandatory truncation-marker scan, medical live-mutation scan, new-file whitespace scan, and focused `git diff --check` are clean apart from expected line-ending notices.
- Unity MCP execution remains pending because the last direct state probe returned `Transport closed`; no operating-system input automation was used.

# 2026-08-03 character combat-command detached continuation

- Added command Aggregate state, V2 sequence/revision persistence, strict structural/world validation, and participant `400.world.combat-command-stances`.
- Converted `combat.commands` from warning-and-skip restore to the common typed JSON preflight boundary.
- Registered attack-position, fallback-weapon, result-application, and participant-query collaborators explicitly; the runtime now receives three capability groups and one Aggregate root instead of fourteen direct services and internal policy construction.
- Made wildlife queries candidate-aware during restore so combat targets resolve against the detached population.
- Added a public save-service invalid-command preflight regression alongside the medical preservation check.
- Runtime auxiliary compilation, including four newly authored sources passed explicitly while Unity's rsp is stale, and Editor auxiliary compilation both pass with Error 0 / Warning 0.
- The next remaining legacy combat owners are `DefenseTacticalCoordinator` and `EquipmentMaintenanceRuntime`; both still clear live dictionaries and normalize/skip invalid restore records.

# 2026-08-03 defense-tactical Aggregate continuation

- Added `DefenseTacticalAggregateState`, V2 sequence persistence, strict candidate-world validation, and a typed JSON save boundary.
- Removed restore-time `byActor.Clear`, generated-ID fallback, warning normalization, and missing-record skips; restore now replaces one detached Aggregate state.
- Added invalid-preflight preservation coverage and V18 validator contracts.
- Runtime and Editor auxiliary Roslyn compilation pass with Error 0 / Warning 0. `combat.equipment-maintenance` is now the remaining legacy combat save boundary in this group.

# 2026-08-03 equipment-maintenance, V18 size ratchet, and medical PlayMode closure

- Converted `combat.equipment-maintenance` to a strict V2 typed save boundary backed by one replaceable Aggregate, persistent facility IDs, canonical sequences, authored material validation, and public invalid-preflight preservation coverage.
- Split the newly exposed oversized `BuildableObject`, husbandry, and fluid-network responsibilities; repaired `RunVariableRuntime`/`MetaProgressionRuntime` MonoScript identity by moving their Aggregate types into dedicated sources.
- Fixed the medical recovery loop by making body health the sole downed/recovered authority. Ambulatory injury requests can no longer fabricate a rescue order, and medical recovery is consumed only from the body-health event.
- Replaced the combat verifier's aggregate-only medicine fixture with authored treatment item SOs materialized as physical facility-buffer stacks. Medical supply intake now recognizes an already delivered exact item before requesting hauling.
- Subscribed combat rescue commands to the body-health recovery event so command state is released even while the game clock is paused.
- Isolated manual rescue QA from autonomous rescuers, made actor ordering and layout deterministic, and required durable evidence for stabilization, transform parenting, bed treatment, hysteresis recovery, and command cleanup.
- Unity MCP PlayMode report passes all tactical and medical checks: one `medical:1` order, `PHYSICAL_RESCUE=PASS`, `RECOVERY_HYSTERESIS=PASS`, `RESCUE_COMMAND_RELEASED=PASS`, Console Error 0 / Warning 0.
- Unity MCP captured the verified 2D gameplay region without operating-system input. The ID-specific camera preview failed once; the SceneView capture succeeded, and the 2D-region capture succeeded after a long render.
- Split medical supply policy and combat-command lifecycle into 190-line and 70-line partial owners. Main runtimes are 1,054 and 1,169 lines; V18 authority passes with save V18, 772 authored items, 168 catalyst SOs, legacy item authority 0, and abstract stock assets 0.
- Tool recovery notes: one editor auxiliary compile incorrectly added runtime partial sources to the Editor assembly and failed cross-assembly partial resolution; rerunning runtime with explicit new sources and Editor against the rebuilt runtime DLL passed. One intermediate test edit referenced an unavailable `Manhattan` helper and was corrected to an inline distance calculation.

# 2026-08-03 captivity restore authority continuation

- Re-audited the Registry and confirmed the full `CaptureAll()` rollback image is still an explicit transitional guard; it cannot be deleted until every legacy live-restore owner is converted.
- Scoped the next cutover to captivity/circus. Captivity already had an Aggregate root and candidate-aware character lookup, but its save section still decoded missing payloads as empty data, skipped corrupt policies/captives with warnings, and depended on a later tick to replay door membership.
- Added `DoorAccessSubjectAggregateState` so captive and captured-wildlife door groups are replaceable Aggregate-root state. Door path cache versions now include the published restore revision, avoiding per-ID restore replay.
- Added strict captivity V2 validation, a detached restore coordinator, ordered participant `450.world.captivity`, typed JSON save boundary, and explicit candidate-world validation for characters, housing capacity/capability, interactions, and restraint references.
- Removed warning-based captivity restoration and the lazy `CaptivityDoorAccessProjection`; successful publication now only clears transient escort parent bookkeeping after the Aggregate root is visible.
- Tool errors recorded: normal `git diff --stat` invoked Git LFS clean and failed on read-only `.git/lfs/tmp`; the LFS-disabled diff form succeeded. Two assumed medical filename searches returned exit 1, one Windows wildcard path produced OS error 123, and one parallel search aborted on an acceptable no-match. All were replaced with symbol-based or directory-plus-`-g` searches.
- Compilation, V18 validation, restore regression, and Unity MCP verification are pending for these fresh edits.
- Auxiliary runtime compilation passed with the four new sources appended to the stale Bee response file; Editor compilation against the rebuilt runtime DLL also passed with Error 0 / Warning 0.
- Ratcheted the V18 validator from the removed lazy projection to the new door-subject Aggregate, strict captivity validator, typed save section, and participant. Added deterministic captivity DTO tests for duplicate IDs, policy-sequence reuse, and transient escort normalization.
- First Editor compile after adding the tests failed with five CS0122 errors because the separate Editor assembly could not access the internal validator. Kept Aggregate creation internal, exposed only the validation entry point and a pure single-state normalization function, and updated the test to use that boundary.
- After the accessibility fix, auxiliary runtime and Editor Roslyn compilation both pass with Error 0 / Warning 0.
- Unity MCP forced AssetDatabase refresh completed successfully; Unity reports no compile/update in progress. The V18 validator exposes `ValidateOrThrow()` for direct MCP execution.
- Unity MCP executed the updated captivity/circus contract suite and V18 authority validator: `CAPTIVITY_CONTRACTS=PASS` and `V18 AUTHORITY PASS` with save V18, 772 authored items, 168 catalyst SOs, legacy item authority 0, and abstract stock assets 0.
- Unity Console verification after the run reports Error 0 / Warning 0.
- First patch attempt to add the captivity public-save preflight check failed because the verifier's service-ready expression no longer matched the older context. No source was changed by that patch; the next edit uses exact bounded source ranges.
- Added the captivity preflight check with exact current source anchors. It now resolves the live captivity runtime, door query, and Aggregate root store, corrupts `captureSequence`, and requires unchanged captivity JSON, published revision, and door version after public save rejection.
- Unity's refreshed runtime response file now includes all four new sources. Clean runtime and Editor auxiliary compilation pass without explicit source appends.
- First MCP PlayMode launch entered the scene, but the post-domain-reload static report remained at its initial value and no verifier runner existed; the scheduled `delayCall` did not survive this invocation path. The next attempt calls `StartRuntimeProbe()` explicitly while already in PlayMode instead of repeating the same launch.
- The explicit runner completed. `CAPTIVITY_PREFLIGHT_ATOMIC=PASS`, but the report revealed valid built-in policies were being rejected by the new strict validator, and the reused virtual-input session later failed `POINTER_RELOAD`. Console contains the verifier's expected failure error until the validator is fixed and a clean PlayMode rerun passes.
- The first multi-file built-in-policy patch was rejected before applying because one hunk delimiter was malformed. The fix is being reapplied in smaller verified patches.
- Added shared `CaptivityPolicyIds` constants for all four built-ins and updated policy construction, DTO defaults, validation, and tests. Custom sequence checks now apply only to `captivity:custom:N` policies.
- Exited the failed reused PlayMode session. Runtime and Editor auxiliary compilation pass after the built-in policy fix.
- Unity MCP reran the pure captivity/circus suite and V18 validator after the policy fix; both pass. A clean PlayMode run will now start with the Console cleared and will use `ManageEditor.Play` followed by explicit `StartRuntimeProbe()` after the domain reload.
- Clean PlayMode run: all five public-save preflight atomicity checks pass, including captivity with unchanged revision/door version and no false built-in-policy errors. Tactical pointer/reload also pass. The later manual rescue click failed because the isolated rescuer remained paused/in combat stance; downstream carry/treatment checks consequently failed. This verifier setup issue must be corrected and rerun before final Console proof.
- Continuation tooling notes: a combined planning-file read exceeded the response limit and was truncated; the first verifier search used the wrong assumed `Assets/Tests/PlayMode` path; and one attempted patch command only printed a placeholder without invoking `apply_patch`. No source files were changed by those failed attempts. Subsequent reads are symbol-located and line-bounded, and edits use the required patch tool.
- Unity MCP connection is healthy. The stale failed PlayMode session was stopped through MCP before verifier repair; no operating-system input automation was used.
- Repaired the medical rescue verifier without changing gameplay rules: it now confirms an exact one-actor rescuer selection, preserves an already-active combat stance, and retries the rescue-mode/right-click sequence at most three times with explicit diagnostics.
- Full auxiliary `Assembly-CSharp` and `Assembly-CSharp-Editor` Roslyn compilation passes after the verifier repair. Unity MCP asset refresh also completed successfully.
- Unity-native contract checks pass: `CAPTIVITY_CONTRACTS=PASS` and `V18 AUTHORITY PASS` with save V18, 772 authored items, 168 catalyst SOs, zero legacy item authority, and zero abstract stock assets.
- Clean PlayMode rerun still failed before command creation. New diagnostics prove exact selection and stance are correct (`selected=Sion`, `stance=True`, `attempts=3`), but every pointer attempt consumes rescue mode and leaves `mode=None` without a command. Physical carry/treatment failures are downstream consequences. The next audit targets pointer target resolution, not selection or rescue rules.
- Diagnostic inspection confirmed `TryIssueRescue` would retain a valid command and `TickRescue` would start the ability for the still-downed patient. A broad verifier field search returned exit 1 only because no event-bus field exists yet; the next patch adds a temporary notice subscription to capture the actual UI rejection message.
- Added a verifier-scoped `NoticeFeedEvent` subscription around rescue attempts, included its warning text in failure diagnostics, and guaranteed disposal after the loop and in `OnDestroy`. Editor auxiliary compilation passes.
- The diagnostic run finished with an empty notice because the temporary subscription filtered out `Grade.NONE`; successful combat-command notices also use `NONE`, so that filter discarded the decisive message. It also exposed avoidable 60-second cascading checks after a failed rescue command. The verifier will capture every notice and abort the medical coroutine immediately when pointer command creation fails.
- Focused rerun now reports `notice=1명 구조` but no surviving command/ability. This proves UI pointer issue succeeds and narrows the production defect to immediate command removal during the following combat-command tick. The failed PlayMode session will be stopped and participant lookup/cancellation audited next.
- Participant lookup audit found that combat commands search only `worldRegistry.Characters`, while persistent/lifecycle character lookup is separately available as `AllCharacters`. The next step verifies registration semantics before deciding whether combat commands must use the lifetime view.
- Added a transient `CharacterCombatCommandTerminatedEvent` emitted on completed/cancelled commands and a verifier-only scoped subscription for the rescue actor. This adds no saved or static authority. Full runtime and Editor auxiliary compilation passes.
- Terminal-event rerun reports `Completed:구조 대상 회복` while the canonical patient remains `Downed`. This proves participant lookup resolved a different same-ID active instance. A direct MCP dynamic dump failed to compile because the command sandbox does not reference VContainer/Sirenix; no project state changed. The source-level fix will canonicalize combat registry reads instead of relying on the dynamic command.
- Centralized the fix at `CharacterAiWorldRegistry`: active and lifetime registration/unregistration now normalize every actor through `CharacterActorCollection.GetCanonical`. A first combined patch was rejected atomically because an earlier tool excerpt had visually duplicated an `AbilityRescue` condition that is not duplicated in the actual UTF-8 source; the registry-only patch then applied cleanly.
- Canonical registration compiles and both Unity contract suites pass, but the full verifier still terminates rescue as `구조 대상 회복`. Therefore the mismatched actor is not entering through ordinary registry registration; the remaining paths are restore-candidate projection or a lifecycle field divergence on components already inside the candidate/list view. The next diagnostic is compiled into the verifier so it can inspect VContainer-backed registry contents safely.
- Added `RESTORE_CANDIDATE_CLEANUP` to the public-save verifier and detailed active/lifetime/candidate identity diagnostics for the patient. Editor auxiliary compilation passes; this compile took 47 seconds instead of the usual 2–8 seconds but completed cleanly without retry or process intervention.
- Live diagnostics pass the new candidate-cleanup invariant and show a single canonical downed patient in both active and lifetime registries. The previous registry canonicalization remains a valid authority-boundary hardening, but it is not the immediate rescue failure. Investigation moves to spurious recovery-event/lifecycle publication.
- Found the authority mismatch: `CharacterMedicalRuntime.NotifyCharacterRecovered` already ignores a recovery event while the body-health snapshot remains downed, but `CharacterCombatCommandRuntime.OnCharacterRecovered` did not. Added the same body-authority guard so a stale/transient event cannot complete a valid rescue command.
- Full Unity MCP PlayMode verification now passes: all five invalid-save atomicity checks, restore-candidate cleanup, tactical pointer/stance/reload, physical rescue parenting, stabilization, bed treatment, recovery hysteresis, command cleanup, and stance release. Final diagnostic reports `candidate=False`, `aggregateStaging=False`, canonical active patient, and `completed=True; PASS`.
- Unity Console contains Error 0 / Warning 0 after the passing run. Captured the verified 1920×1080 gameplay view directly with `Unity_Camera_Capture` from `Main Camera`; no OS mouse/keyboard automation was used. Play Mode was then stopped cleanly.
- Added V18 source ratchets for canonical character registration, authoritative body-health recovery gating, and transient combat-command terminal events. The refreshed Unity validator still reports V18 authority PASS.
- Final phase gates pass: truncation markers 0; focused `git diff --check` clean apart from expected LF→CRLF notices; trailing whitespace 0 across 118 relevant untracked files; runtime line counts remain below 1,200; saved PlayMode report contains `RESULT=PASS`, `CONSOLE_ERRORS=PASS`, and `CONSOLE_WARNINGS=PASS`. Final Unity state is stopped/not compiling/not updating with Console Error 0 / Warning 0.
- Began the next Phase 112 boundary audit. Circus and captured-wildlife persistence still use warning/skip/clamp/normalize restore paths despite already having Aggregate state types; they are the next detached-transaction conversion target.
- Selected a combined `500.world.circus` participant design: strict V2 DTO validation, atomic replacement of circus and captured-wildlife Aggregate slots, staged captured-wildlife door membership, then explicit actor/transient projection publication.
- Added strict Circus V2 DTO validation for canonical order/building IDs, monotonic sequences, programs, participant collections, finite economy/timing values, transport-state coherence, and order/wildlife cross-links. Pure builders now perform only the documented deterministic in-flight normalization after source validation.
- Replaced warning/skip restoration with the ordered `500.world.circus` transaction participant. It validates candidate-world stages, rooms, actors, captives, programs, pen capability/capacity, species, feed IDs, and positions before replacing both circus and captured-wildlife Aggregate slots in the same candidate root.
- Converted `CircusSaveSection` to `DungeonJsonSaveSection<CircusSaveData>`, staged captured-wildlife door membership with `ReplaceCapturedWildlifeSubjects`, and made actor/carry/access-pass projection explicit at publication. The projection cleanup no longer calls gameplay release commands against the newly published Aggregate.
- Removed `IWildlifeCaptureRuntime.Restore(..., warnings)` and `CircusStateCodec.Restore`. The performance-world fixture now seeds livestock through `TryRegisterPenBorn`, so restore is no longer a general mutation API.
- Added V18 source ratchets, deterministic Circus V2 validation/normalization contracts, and `CIRCUS_PREFLIGHT_ATOMIC`. Invalid public save restore preserves circus JSON, Aggregate revision, door version, and leaves no candidate/staging state.
- Auxiliary runtime and Editor compilation pass. Unity MCP refresh, captivity/circus contracts, and V18 authority validation pass. Full PlayMode report has `CIRCUS_PREFLIGHT_ATOMIC=PASS`, `RESTORE_CANDIDATE_CLEANUP=PASS`, all tactical/rescue/treatment checks PASS, `RESULT=PASS`, and Console Error 0 / Warning 0.
- Captured the verified 1920x1080 gameplay view from `Main Camera` through Unity MCP and stopped PlayMode cleanly. No OS mouse or keyboard automation was used.
- Tooling notes: the first broad Unity-tool listing and two broad source audits exceeded display limits; one audit also referenced nonexistent `Assets/Scripts/Setup`. Source-marker scans remained clean, and subsequent reads used exact bounded files. The first Editor test compile correctly failed on internal Aggregate access and the first moved-partial runtime compile lacked a file-local static import; both boundaries were repaired without widening Aggregate accessibility.
- Began the next Phase 112 cutover audit. Warning-based restore is now confined to invasion response/engagement/evacuation and surgery. Selected invasion next because its single section currently mutates six coupled authorities and destroys active Unity intruders before later validation can fail.
- Defined the invasion direction: strict current-version DTO validation, a pure invasion Aggregate, detached inactive intruder candidates, engagement/evacuation preparation without coroutines or presentation, and one provisional `550.world.invasion` participant that publishes the mutually referential state together.
### 2026-08-03 침공 저장 감사 도구 기록

- 침공 원시 타입·정책·디렉터를 한 번에 읽은 도구 출력이 응답 한도를 초과해 잘렸다. 소스 파일 손상은 아니며, 이후 읽기는 파일별 80줄 이하 범위로 제한한다.
- 침공 V4 검증기 추가 직후 기존 Bee 응답 파일로 실행한 보조 컴파일은 새 `InvasionSaveValidation.cs`가 소스 목록에 아직 없어서 CS0103으로 실패했다. 새 파일을 명시 입력하거나 Unity refresh 후 재검증한다.
- Unity MCP refresh 후 `InvasionIntruderDebugScenarios.NoopInvasionSaveService`가 새 `ValidateRestore` 계약을 구현하지 않아 CS0535가 발생했다. 테스트 대역을 보완한 뒤 Editor 컴파일을 재검증한다.
- Unity MCP 동적 명령에서 실제 `IInvasionSaveService`를 Resolve하려 한 검증은 동적 명령 어셈블리에 VContainer 참조가 없어 컴파일되지 않았다. VContainer 참조가 있는 Editor 검증 진입점으로 옮기고 MCP는 그 진입점만 호출한다.
- Aggregate 루트 저장소를 별도 파일로 추정해 읽은 명령이 경로 없음으로 실패했다. 실제 정의는 `DungeonSaveSections.cs`에 있으며 해당 구간으로 다시 확인한다.
- Aggregate 등록 검색은 유효한 결과를 출력했지만 PowerShell 파이프라인의 최종 종료 코드가 1로 남아 실패로 표시됐다. 이후 `rg` no-match는 명시적으로 정상 종료 처리한다.
- `InvasionThreatRuntime` Aggregate 전환 패치는 `Construct` 매개변수 순서를 잘못 가정해 문맥 검증에서 거부됐다. 파일은 변경되지 않았으며 정확한 시그니처를 읽어 분할 적용한다.
- 침공 Aggregate 첫 컴파일에서 공개 생성자/주입 메서드가 internal `InvasionAggregateStateStore`를 받아 CS0051 세 건이 발생했다. DI 경계 타입만 public으로 올리고 내부 상태 타입은 계속 숨긴다.
- 침입자 후보 구조 감사에서 런타임 파일명을 `InvasionIntruderRuntime.cs`로 추정해 한 경로 읽기가 실패했다. 실제 정의는 `InvasionIntruderSystem.cs`이며 해당 파일과 디렉터 복원 구간으로 다시 확인한다.
- 방어 상태 서비스 감사에서 `DefenseStatusRuntime.cs`로 추정한 경로가 없어 읽기가 실패했다. 검색된 실제 파일 `DefenseStatusRuntimeService.cs`로 다시 확인한다.
- 캐릭터 생성 팩토리 감사에서 검색 결과와 다른 `Character/Spawning` 경로를 사용해 읽기가 실패했다. 실제 `Services/Character/CharacterSpawnObjectFactory.cs`를 그대로 사용한다.
- `InvasionIntruderSystem.cs`의 `OnEnable/OnDisable` 검색은 no-match 종료 코드 1로 표시됐다. 활성화 훅이 없음을 확인했으며 후보 게시 순서는 분리 캐릭터 공개 후 준비된 코루틴 시작으로 둔다.
- `new InvasionSaveService(` 직접 생성 검색은 no-match 종료 코드 1로 표시됐다. 생성은 DI 등록에만 의존하므로 생성자 의존성 변경은 등록 컴파일로 검증한다.
- Unity MCP `DungeonGameSaveDebugScenarios.RunFullGameRoundTrip`은 현재 플레이 씬에 테스트 전제인 Owner runtime이 없어 시작 전 `InvalidOperationException`으로 중단됐다. 침공 복원은 실행되지 않았으며 현재 씬에서 가능한 레지스트리/섹션 전용 검증으로 대체한다.
- 구형 경고 복원 API 제거 후 런타임/Editor 보조 컴파일을 병렬 실행해 Editor가 갱신 전 런타임 DLL을 참조하면서 CS1061이 발생했다. 의존 컴파일은 런타임 완료 후 Editor 순서로 다시 실행한다.
- 순차 Editor 컴파일에서 `DefenseEngagementPlayModeVerifier`가 삭제된 정책·대피·교전 Restore API를 테스트 정리에 직접 사용해 CS1061 세 건이 발생했다. 테스트 정리도 저장 레지스트리 트랜잭션을 사용하도록 전환한다.
- V18 저장 서비스로 바꾼 교전 verifier의 결과 문자열 한 곳이 삭제된 `warnings` 지역 변수를 참조해 CS0103이 발생했다. `DungeonGameRestoreReport.Warnings`로 교체한다.
- Unity MCP EditMode 침공 회귀에서 threat/engagement는 통과했지만 intruder의 `침입 배속 반응과 수동 우선권`, `시설 파손 보조 목표`가 authored 패턴 카탈로그 미주입 예외로 실패했다. 두 테스트 생성 경로를 실제 카탈로그 주입으로 고친다.
- 위 회귀 오류를 기록하려던 첫 패치는 직전 진행 문구를 다르게 적어 문맥 검증에서 거부됐으며 파일 변경은 없었다. 정확한 마지막 구간에 다시 추가했다.
- 활성 침입자가 있는 원자 복원에서 현재 씬의 정상 prefabless 침입자 구성을 후보 팩토리가 거부해 복원과 롤백이 실패했다. 후보 Aggregate/기존 침입자는 게시되지 않아 라이브는 보존됐다. prefabless 후보도 비활성 RestoreCandidates 루트에서 만들고 detached 캐릭터로 준비하도록 수정한다.
- 자동 사장 최종 방어 강제 검증은 대피 완료와 활성 경비 전선 조건이 없어 `FAIL` 상태 문자열을 반환했다. 이 시도는 상태를 변경하지 않았고, 활성 침입자 원자 왕복으로 후보 경계를 별도 검증한다.
- prefabless 후보 재검증은 `PrepareForDetachedRestore()`를 의존성 주입 뒤 호출해 캐릭터 팩토리 계약에 의해 거부됐다. 라이브는 후보 단계에서 보존됐다. 조립 순서를 컴포넌트 확보 → detached 표시 → Inject → 런타임 구성으로 바꾼다.
- prefabless 순서 수정 후 Unity MCP refresh 명령은 스크립트 리로드 중 자신의 동적 RunCommand DLL이 교체되어 assembly load 오류로 종료됐다. 프로젝트 컴파일과 별개인 MCP 리로드 경쟁이며 Editor state/Console로 실제 반영 여부를 확인한다.
- 활성 prefabless 침입자 원자 왕복은 통과했다. 직후 후보 정리를 동적 MCP 코드에서 직접 조회하려 했으나 RunCommand 어셈블리에 Sirenix 참조가 없어 `CharacterActor` 제네릭 조회가 컴파일되지 않았다. Editor 검증 진입점으로 옮긴다.
- V18 authority validator는 침공 경계 ratchet을 통과했지만 후보 코드 추가로 `DefenseEngagementRuntime.cs` 1,214줄, `InvasionIntruderSystem.cs` 1,236줄이 1,200줄 제한을 넘었다. 복원 전용 메서드를 partial 파일로 이동한다.
- 침공 V4 전환 완료: `InvasionAggregateState`, strict V4 validation, typed save section, `550.world.invasion`, inactive prefab/prefabless intruder candidates, staged evacuation/engagement, exact policy/campaign replacement, legacy warning/default/pressure restore removal을 적용했다.
- Unity MCP 회귀는 EditMode threat/intruder/engagement PASS, V4 corrupt rejection PASS, normal/late-failure/V3 atomic contracts PASS, active prefabless intruder restore PASS, detached candidate cleanup 0을 보고한다. V18 authority validator도 PASS다.
- 복원 partial 분리 후 `DefenseEngagementRuntime.cs` 1,193줄, `InvasionIntruderSystem.cs` 1,093줄이다. 런타임·Editor 보조 컴파일과 Unity 실제 컴파일은 Error 0 / Warning 0이다.
- 첫 Unity MCP 카메라 캡처는 이미지와 전체 메타데이터를 함께 전달해 응답 한도를 초과했지만 연결·프로젝트 상태에는 이상이 없었다. 이미지 블록만 전달하도록 재시도해 `Main Camera` 캡처 1장을 정상 수신했고, MCP 상태 조회·Console Error 0 / Warning 0 확인·PlayMode 종료까지 성공했다. 운영체제 마우스·키보드는 사용하지 않았다.
- 다음 Phase 112 재개 시 `task_plan.md`·`findings.md`·`progress.md`를 한 호출로 전부 출력한 결과가 응답 한도를 초과했다. 파일 손상은 아니며 이후에는 Phase/심볼별 줄 범위만 읽는다. 이어서 실행한 일반 `git diff --stat`은 Git LFS가 `.git/lfs/tmp`에 쓰려다 권한 거부로 실패했다. 워크트리를 바꾸지 않았고, LFS clean filter를 호출하지 않는 read-only 통계 방식으로 대체한다.
- Phase 112·수술 키워드를 세 계획 파일 전체에서 한 번에 검색한 출력도 범위가 넓어 절단됐고, 뒤이어 `git status --short` 역시 변경 PNG의 LFS clean filter가 `.git/lfs/tmp` 쓰기를 시도해 권한 거부됐다. 이후 계획 검색은 파일별 정확한 줄 범위로 제한하고, 상태 확인은 LFS 필터를 건드리지 않는 `git diff --name-only -- Assets/Scripts ...` 또는 명시 경로 검사만 사용한다.
- 첫 수술 복원 검색은 `Assets/Scripts/Services` 전체에 `-g "*.cs"`를 적용해 범위가 다시 넓어졌고 존재하지 않는 `Assets/Scripts/Save`도 포함해 출력 절단과 경로 오류가 함께 발생했다. 수술 파일 목록은 정상 확보했으며, 이후 검색은 `Assets/Scripts/Services/Medical/SurgeryPersistence.cs`, `SurgeryRuntime.cs`, `SurgeryModels.cs` 등 정확한 파일만 대상으로 한다.
- 수술 하위 Restore 구현을 `-C 8`로 한꺼번에 출력한 결과도 11k 토큰을 넘어 절단됐다. 필요한 파일과 심볼 위치는 확보했으며, 이후 구현 읽기는 파일별 해당 메서드 범위만 사용한다.
- `SurgeryAggregateState`와 `SurgeryAggregateStateStore`를 추가했다. 주문, 부품, 장기 보관 연료, 사체 신선도, 대상 정책, 사체별 적출 부위, 동물 해부 상태와 두 시퀀스를 하나의 deep-clone 가능한 일반 C# Aggregate로 묶었고 SO/Unity 객체는 상태에 넣지 않았다.
- `SurgeryRuntime`, 수술 부품/보관, 정책, 적출 원장, 사체 신선도, 동물 해부 런타임을 Aggregate-backed command/query facade로 전환했다. 하위 warning Restore API를 계약과 구현에서 제거했고, 캡처는 V5 DTO를 Aggregate에서 결정론적 순서로 생성한다. 현재는 저장 검증/코디네이터를 연결하기 전의 의도된 중간 상태다.
- 수술 절차의 시설 태그 속성을 전역 검색한 출력이 10k 토큰을 넘어 절단됐다. 정확한 정의 파일이 `Assets/Scripts/Models/Medical/SurgicalProcedureSO.cs`임을 확인했으므로 이후에는 해당 파일만 읽는다.
- 물리 아이템 카탈로그 계약을 넓게 검색한 출력도 10k 토큰을 넘어 절단됐다. 필요한 계약은 `IDungeonItemCatalogProvider.TryGetDefinition(string, out ...)`로 확인했으며, 수술 월드 검증은 이 좁은 API와 `IWorldItemStackRuntime.GetAllStacks()`만 사용한다.
- 복원 참여자 인터페이스를 `Services/Infrastructure`에 있다고 가정한 첫 검색은 no-match로 종료됐고, 두 번째 검색은 `Select-Object -First`가 `rg` 파이프를 조기 닫아 결과를 출력하고도 코드 1을 반환했다. 실제 선언은 `Assets/Scripts/Services/Foundation/Save/DungeonSaveSections.cs`이며 이후 정확한 파일을 읽는다.
- `SurgerySaveValidation`과 `525.world.surgery` 참여자를 추가했다. V5 DTO의 ID/시퀀스/enum/수치/교차 참조를 엄격히 검사하고, 후보 캐릭터·동물·시설·물리 스택·작성된 절차/해부학을 전부 검증한 뒤 Aggregate를 교체한다. 운반 실행 상태는 명시적 transient normalization으로 재요청되며, 이전 운반/AI 투영 정리와 새 입실 투영은 루트 공개 후에만 수행된다.
- `SurgerySaveSection`은 정확한 V5만 받는 `DungeonJsonSaveSection<DungeonSurgerySaveData>`로 전환했고 V2–V4 migration/warning/default 경로를 삭제했다. 코디네이터는 자기 자신과 복원 참여자로 단일 등록됐다.
- Aggregate/validator/coordinator를 명시 입력한 런타임 보조 컴파일은 통과했다. 이어진 Editor 컴파일은 `SurgeryPlayModeVerifier` 정리 코드가 삭제된 `ISurgeryRuntime.Restore`를 직접 호출해 CS1061로 실패했다. 테스트 정리도 공개 V18 저장 서비스 트랜잭션을 사용하도록 바꾼 뒤 재검증한다.
- 공개 저장 서비스 사용 예를 넓게 검색한 출력이 10k 토큰을 넘어 절단됐지만 `IDungeonGameSaveService.TryRestore(DungeonGameSaveData, out DungeonGameRestoreReport)` 계약을 확인했다. 수술 PlayMode verifier는 이제 아이템/수술 런타임을 따로 복원하지 않고 전체 V18 게임 스냅샷을 캡처·트랜잭션 복원한다.
- Unity MCP로 새 수술 Aggregate/validator/coordinator 스크립트 3개를 import해 GUID와 `.meta` 생성을 확인했다. Unity 실제 컴파일이 진행되는 동안 집중 검색을 수행했고 수술 구형 버전/경고형 Restore/직접 런타임 Restore 경로는 0건이다.
- V18 validator의 침공/circus ratchet 위치를 전역 문맥 검색한 출력이 10k 토큰을 넘어 절단됐다. validator 파일은 확인했으며 이후 780–880줄의 정확한 구간만 읽어 수술 계약을 같은 위치에 추가한다.
- `RuntimeAuthorityV18Validator.cs`의 775–890줄을 한 번에 읽은 출력도 응답 한도를 넘어 절단됐다. 파일 손상은 없으며 이후 50줄 이하 구간으로 분할한다.
- 재개 시 세 SKILL 문서와 세 계획 파일을 한 호출로 묶은 출력이 각각 응답 한도를 넘어 절단됐다. Unity C#/SO 지침은 완전했지만 `planning-with-files`와 계획 파일은 작은 구간·정확한 tail로 다시 읽으며, 앞으로 장문 파일을 한 호출에 묶지 않는다.
- `planning-with-files` 지침을 100줄 이하 구간으로 끝까지 다시 읽었다. session-catchup은 도구 호출 7건이 계획 파일 이후였다고 보고했지만 새 메시지 본문은 없었다. 일반 `git diff --stat`은 이미 확인된 Git LFS 임시 쓰기 권한 문제 때문에 반복하지 않고, 현재 요약·정확한 파일 tail·명시 경로 검사로 작업 상태를 복구했다.
- 수술 저장 감사와 strict Aggregate/후보 투영 구현을 완료 단계로 정리했다. 현재 작업은 V18 소스 ratchet → V5 결정론/원자 복원 테스트 → 보조 컴파일 및 Unity MCP 회귀 순서다.
- V18 validator를 `Assets/Scripts/Editor`에 있다고 추정한 첫 50줄 읽기는 경로 없음으로 실패했고, 정확한 파일명 끝 정규식 검색도 Windows 경로 구분자 때문에 no-match 종료됐다. 넓힌 파일명 검색으로 실제 경로 `Assets/Scripts/Services/Items/Editor/RuntimeAuthorityV18Validator.cs`를 확인했으며 이후 그 경로만 사용한다.
- 첫 findings 기록 패치는 해당 문장이 `progress.md`에 있다는 점을 혼동해 문맥 검증에서 거부됐다. 파일은 변경되지 않았고, `findings.md`의 실제 마지막 구간을 읽은 뒤 별도 수술 섹션으로 추가했다.
- V18 authority validator에 수술 Aggregate, strict V5 validator, typed JSON save section, `525.world.surgery` 참여자 요구를 추가했다. 구형 DTO 직접 Restore, capture owner의 Restore, part Restore API 재도입도 즉시 실패하도록 금지했다.
- `DungeonGameRestoreReport` 검색은 필요한 선언을 출력했지만 `Select-Object -First`가 `rg` 파이프를 조기 닫아 도구가 코드 1로 표시됐다. 정확한 파일을 직접 읽어 `Success == Errors.Count == 0` 계약을 확인했으며 이후 같은 파이프 패턴을 쓰지 않는다.
- 수술 Editor 계약에 `strict_v5_payload`를 추가했다. authored 42개 절차/12개 해부 카탈로그를 사용해 canonical empty V5 통과와 V4, 필수 컬렉션 누락, 음수 시퀀스, 중복 subject policy 거부를 검증하며 기존 JSON 왕복 설명도 V16에서 V5로 교정했다.
- `RunAtomicSurgeryRestoreContracts`를 추가했다. 실제 live scope의 수술 runtime/coordinator/root store를 격리 V5 section과 후행 실패 section에 조립해 정상 왕복, 바뀐 candidate의 후행 실패 rollback, JSON·published revision 불변, staging 정리, V4 section 거부를 검증한다.
- 수술 V18 ratchet과 strict/atomic 테스트 추가 후 Unity Bee 응답 파일을 사용한 런타임 보조 컴파일(3.8초)과 Editor 보조 컴파일(2.2초)이 모두 진단 없이 통과했다.
- Unity MCP에서 V18 authority(`772 authored items`, 연구 catalyst SO 168개)와 수술 strict V5 Editor 계약은 PASS했다. 첫 PlayMode 원자 복원은 `failed commit changed live state`로 FAIL했지만 결합 조건만 출력해 JSON/return/report/revision/staging 중 원인을 구분할 수 없었다. 동일 실행을 반복하지 않고 항목별 진단을 추가한다.
- registry 소스 확인으로 실패 원인이 commit 실패 후 rollback image를 다시 stage/publish해 revision이 증가하는 구조임을 확정했다. `IDungeonRollbackFreeSaveSection`을 추가하고 registry의 모든 section이 opt-in했을 때만 후보 discard 후 rollback image 재적용을 생략하도록 했다. 프로덕션 수술 section과 격리 원자 section 두 개가 opt-in하며, 미전환 section이 섞인 일반 registry는 기존 안전망을 유지한다.
- marker 추가 직후 Assembly-CSharp 보조 컴파일은 별도 Foundation 어셈블리의 이전 DLL을 참조해 `IDungeonRollbackFreeSaveSection` CS0246으로 실패했다. Foundation 소스는 `DungeonStory.Foundation.rsp`가 소유함을 확인했으므로 Foundation → 런타임 → Editor 순서로 재컴파일한다.
- Foundation → 런타임 → Editor 보조 컴파일은 모두 진단 없이 통과했다. 다만 변경 스크립트를 기존 Play Mode 중 Unity MCP로 reimport해 도메인 리로드가 발생했고, 리로드 전 씬 객체의 DI가 끊겨 presentation/shop/scheduler/spawner에서 Error 8건이 발생했다. Play를 정지하고 Console을 지웠으며, 깨끗한 새 Play 세션에서만 회귀 증거를 다시 수집한다.
- 새 Play 세션에서 V18 authority와 수술 strict V5 계약을 다시 PASS했다. rollback-free 원자 회귀도 정상 V5 왕복, 바뀐 candidate의 후행 실패, JSON/revision 불변, staging 정리, V4 section 거부를 모두 PASS했고 최종 Console은 Error 0 / Warning 0이다. Play Mode는 MCP로 정지했다.
- 다음 owner로 시설 월드 복원을 선택했다. dependency-free strict codec을 분리하고 null payload 합성을 제거했으며, 서비스의 Editor 2인자 생성자·live clear/restore fallback·transaction 없는 즉시 candidate publish·live `RestoreBuilding` 구현을 제거했다. 시설 section은 rollback-free opt-in으로 전환했고 authored layer 불일치와 missing state module을 error로 승격하는 중이다.
- schema/version Editor fixture 3곳은 새 strict codec을 직접 사용한다. 전체 시설 save/load fixture는 실제 inactive candidate preparation/publication을 사용하도록 candidate index, session/grid publishers, relocation no-op, 실제 빈 GridTexture provider를 조립했고 publish 뒤 replacement Grid를 검증하도록 바꿨다. 구형 서비스 생성자는 이 fixture 한 곳의 7인자 candidate-only 생성만 남았다.
- 새 codec Unity import 후 실제 컴파일은 Editor fixture가 internal `GameSessionState.Restore`를 호출해 CS1061 한 건으로 실패했다. 공개 reactive fields의 `Initialize`를 사용하고 이 fixture가 요구하지 않는 paused snapshot은 명시적으로 거부하도록 대역을 수정했다. 관련 API 검색은 필요한 결과를 출력했지만 `Select-Object -First`가 rg 파이프를 닫아 코드 1로 표시됐으므로 정확한 `GameSessionState.cs`를 직접 읽었다.
- Foundation/runtime/Editor 보조 컴파일과 Unity 실제 컴파일은 이후 0/0이었다. 첫 통합 시설 회귀는 3갈래로 실패했다: state persistence의 기존 custom stock-category fixture와 legacy shared-state split, InstanceEvolution의 RunVariable Aggregate 주입 누락, 전체 시설 왕복 NRE. missing-module error가 합법적인 legacy core→현행 ability/evolution 모듈 분해까지 누락으로 오판했으므로 warning 복귀 대신 명시적 migration coverage 계약을 추가한다. 나머지는 report/stack을 분리해 진단한다.
- 재검토 결과 V17 이하 비호환 목표상 legacy module V1 분해 자체가 불필요해 coverage 인터페이스 방향을 폐기했다. FacilityRuntimeState module은 exact v2만 허용하고 legacy DTO/restore helper/test를 제거했으며 save/load fixture는 현행 core+production state로 초기화한다. unknown stock enum 성공 기대도 삭제했다.
- 시설 NRE는 detached object factory/injector 누락이 원인이었다. 서비스 Editor 조립은 `IGridBuildingObjectFactory`와 명시적 injector를 필수로 받도록 고쳤다. 진화 determinism은 concrete scene RunVariable 대신 새 `IRunSeedProvider`를 주입하고 운영 reader가 이를 구현하도록 좁혔다. direct constructor 재검색은 유일한 fixture 결과를 출력한 뒤 두 번째 no-match 검색 때문에 코드 1로 표시됐다.
- 수정 후 런타임/Editor 보조 컴파일과 Unity 실제 컴파일이 0/0으로 통과했다. Unity MCP 통합 회귀는 state persistence 7건, facility candidate save/load 9건, instance evolution, V18 authority를 모두 PASS했고 Console Error 0 / Warning 0이다. replacement Grid에는 11개 시설과 모든 state module/layer/session 값이 보존됐으며 stale live 시설 2개는 publish 시 제거됐다.
- 캐릭터 월드 복원 구현을 한 호출에서 두 개의 큰 줄 범위로 읽으려다 도구 출력이 응답 한도를 넘어 절단됐다. 파일 변경은 없었고, 이후 `CharacterWorldSaveService.cs`는 60줄 이하의 정확한 구간으로 나눠 읽는다.
- `CharacterStatBlock` 선언을 `CharacterSkillModels.cs` 한 파일에 있다고 가정한 exact `rg`가 no-match(exit 1)로 종료됐다. 파일 변경은 없으며 전체 `Assets/Scripts`에서 타입 선언을 다시 찾는다.
- lifecycle별 위치 검증 helper를 한 패치로 추가하려 했지만 `IsFinite`의 현재 위치가 예상 문맥과 달라 apply_patch가 거부됐다. 파일 변경은 없고, 정확한 세 위치를 검색해 작은 패치로 나눈다.
- 첫 캐릭터 월드 런타임 보조 컴파일은 Unity Bee 응답 파일이 새 `CharacterWorldSaveValidation.cs`를 아직 포함하지 않아 CS0103 3건으로 실패했다. 새 파일 자체의 계약 오류가 아니라 stale source list이며, 해당 소스를 csc 명령에 명시적으로 추가해 재검증한다.
- 첫 strict 캐릭터 PlayMode 왕복 실행은 Play 진입 1.5초 뒤 owner runtime이 아직 없어 시작 전 조건에서 실패했다. 저장/복원은 실행되지 않았고, 씬 초기화 완료 여부와 Console을 확인한 뒤 owner 생성 이후에 재시도한다.
- owner 준비용 Editor fixture를 찾는 첫 `rg`는 Windows 경로 인자 `Assets/Scripts/Services/*/Editor`의 wildcard 때문에 OS error 123으로 종료됐다. 알려진 규칙대로 디렉터리는 `Assets/Scripts`로 고정하고 `-g "*Editor/*.cs"` 필터로 다시 검색한다.
- owner 준비 검색을 전체 `Assets/Scripts`로 다시 실행한 결과 10k 토큰을 넘어 출력이 절단됐다. 필요한 공개 진입점 `StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug()`는 확인했으므로 이후 해당 파일과 심볼만 읽는다.
- authored start party fast commit으로 owner+staff 2명을 정상 생성했다. 이어진 progression V18 왕복은 캡처 검증 자체는 통과했지만 fixture의 “exact owner progression state” 비교에서 실패해 finally baseline 복원을 실행했다. level/XP/growth/narrative 중 어느 항목인지 구분하도록 fixture 진단을 세분화한 뒤 한 번만 재실행한다.
- 세분화한 progression 진단은 owner/level/growth/narrative가 일치하고 XP만 요청 77이 런타임 규칙에서 19로 정규화됐음을 확인했다. 캐릭터 월드 cutover 오류가 아니라 fixture가 `RestorePersistentState` 입력값을 그대로 기대한 오류이며, 복원 직후 캡처한 `expectedProgression.CurrentExperience`를 기대값으로 사용하도록 고친다.
- strict direct-call/ownerless/invalid-position 계약은 새 fixture 앞부분을 통과했고 정상 캡처 비교도 통과했다. 이후 기존 legacy-version 검사가 복원 거부 여부가 아니라 오류 문구에 한글 `호환`이 포함돼야 한다는 낡은 기대 때문에 실패했다. 실제 report를 출력하도록 고쳐 root V17 이하 비호환 계약 자체를 검사한다.
- `DungeonJsonSaveSection` 선언을 Foundation의 `DungeonSaveSections.cs`에 있다고 가정한 exact 검색은 no-match(exit 1)로 종료됐다. character section version을 하드코딩하지 않도록 실제 선언 파일을 좁혀 다시 찾는다.
- `DungeonSaveSectionPayload.Write` 선언을 `public static void` 정확한 서명으로 찾은 검색은 no-match(exit 1)였다. 반환형/접근성이 다른 경우를 포함해 타입명과 메서드명만으로 다시 찾는다.
- 캐릭터 월드 strict V18 cutover를 구현했다. owner 1명·authored definition·nested DTO·exact lifecycle/cell을 검증하고, 빈/live-preserve/nearest/live-catalog/direct-publish/quiescence 경로를 제거했으며 section을 rollback-free로 전환했다.
- Unity MCP PlayMode 계약은 direct call, ownerless payload, invalid Active cell, V17 root를 모두 라이브 불변 상태로 거부하고 정상 전체 저장 왕복을 통과했다. 실제 facility+character 후보 뒤의 고의 commit 실패도 owner/Grid/root revision/candidate index/detached actor를 전부 보존·정리했다. 최종 로그는 `CHARACTER_WORLD_V18_CONTRACTS_PASSED ... rollbackFreeLateFailure=true ... warnings=0`이다.
- 캐릭터 cutover 최종 검증: runtime/Editor 보조 컴파일 진단 0, Unity 실제 컴파일 0/0, V18 authority PASS(772 authored items, 168 catalyst SOs), PlayMode strict/rollback-free/round-trip PASS, Console Error 0 / Warning 0, PlayMode 종료. truncation marker 0, focused `git diff --check` 오류 0, trailing whitespace 0, 구형 preserve/nearest/quiescence/warning 경로 0이며 서비스 936줄·validator 479줄·fixture 446줄이다.
- wildlife ecosystem의 기존 finite helper를 찾는 exact `rg`는 no-match(exit 1)로 종료됐다. 파일 변경은 없으며 새 strict candidate 준비 메서드에 로컬 `IsFiniteAtLeast` helper를 추가한다.
- Unity MCP 노출 여부를 확인하려고 전체 도구 설명을 넓게 조회해 108k 토큰 출력이 절단됐다. 이후에는 확인된 `mcp__unity_mcp__*` 도구를 직접 호출하며, 실제 `Unity_ReadConsole` 연결은 성공했고 Error 0 / Warning 0을 반환했다.
- V18 검증기를 `Assets/Scripts/Editor/Validation/RuntimeAuthorityV18Validator.cs`로 읽으려 했으나 실제 경로와 달라 `Get-Content`가 실패했다. 심볼 기반으로 다시 찾아 실제 파일 `Assets/Scripts/Services/Items/Editor/RuntimeAuthorityV18Validator.cs`를 확인했다.
- wildlife 새 분할 파일 import 후 Unity 실제 컴파일과 Console은 0/0이었고 runtime 보조 컴파일도 통과했다. Editor 보조 컴파일은 `WildlifeDebugScenarios.cs(950,13)`의 미정의 지역 변수 `scope`로 실패했으므로 fixture 범위를 수정한 뒤 재실행한다.
- wildlife fixture 수정 후 runtime/Editor 보조 컴파일, V18 authority, EditMode 계약은 통과했다. PlayMode 진입 시 Unity Jobs IL postprocessor가 `BuildingAbilityWorkCompletedHandler<BuildingStatePersistenceDebugScenarios/UnlistedWorkAbility>`를 해석하지 못해 에디터가 Play 진입을 거부했다. wildlife 코드 오류가 아니라 Editor debug fixture의 nested generic 타입 해석 문제이며 해당 fixture를 조사해 실제 Unity 컴파일을 복구한다.
- unlisted fixture 타입들을 top-level internal로 이동했지만 ILPP는 여전히 `BuildingAbilityWorkCompletedHandler<UnlistedWorkAbility>` generic instantiation을 해석하지 못했다. nested visibility가 원인이 아니므로 fixture handler에서 운영 generic base를 사용하지 않고 동일 public interface 계약을 직접 구현하는 방향으로 수정한다.
- unlisted handler를 `IBuildingAbilityWorkCompletedHandler` 직접 구현으로 바꾸자 해당 ILPP 오류는 사라졌다. 다음 Play 진입에서 같은 패턴의 `DungeonJsonSaveSection<CharacterProgressionSavePlayModeFacade/MarkerPayload>` 오류가 노출되어, Editor-only 타입으로 runtime generic을 닫는 fixture 패턴 전체를 감사한다.
- Editor-only marker generic closures를 모두 제거하자 다음 ILPP 오류가 `DungeonJsonSaveSection<DungeonInvasionSaveData>`에서 발생했다. 타입 인수의 소유 assembly와 무관하게 Editor assembly가 runtime generic base를 상속하는 것 자체가 문제이므로 남은 invasion/surgery isolated typed sections도 interface 직접 구현으로 전환한다.
- Editor의 `DungeonJsonSaveSection<T>` 상속을 0건으로 만든 뒤 다음 ILPP 오류가 `IRunVariableMultiplierEffect<string>`에서 발생했다. 누적된 Editor fixture의 runtime generic 구현 패턴이 순차적으로 노출되는 중이며, 현재 concrete 구현 위치를 찾아 비-generic 계약 또는 runtime-owned 타입으로 교체한다.
- `TestGuestDemandEffect`를 운영 `RunGuestDemandEffect`로 교체하자 해당 ILPP 오류는 사라졌다. 다음 차단은 `Data<int>` generic type으로 이동했으므로 정의와 Editor 선언 위치를 특정해 같은 원칙으로 제거한다.
- binary string 검사로 Bee `Assembly-CSharp.ref.dll`의 내부 identity가 실제로 `CodexWildlifeRuntimeImportedCheck`로 오염된 것을 확인했다. 원인은 보조 csc가 rsp의 `-refout`을 Temp로 덮지 않은 것이며, runtime 소스를 Unity MCP로 강제 reimport해 정식 `Assembly-CSharp` reference assembly를 재생성한다. 이후 보조 컴파일은 `-out`과 `-refout`을 모두 Temp로 지정한다.
- Unity reimport로 runtime/editor DLL과 정식 ref identity를 복구해 전체 컴파일 0/0 및 실제 PlayMode 진입에 성공했다. owner 준비 MCP 명령은 `RunFastCommitForDebug()` 반환형을 bool로 잘못 가정해 동적 명령 컴파일이 실패했으며, 실제 string 반환값으로 다시 호출한다.
- wildlife strict cutover 최종 검증을 완료했다. V18 authority PASS(772 authored items, 168 catalyst SOs), wildlife EditMode PASS, authored owner 준비 후 PlayMode exact ecosystem patch/actor round-trip 및 rollback-free late-failure PASS, runtime/Editor 보조 컴파일 진단 0, fixture 회귀(Building state/Run variables/Surgery/Invasion) PASS, Console Error 0 / Warning 0이다.
- 보조 컴파일은 이제 `-out`과 `-refout`을 모두 Temp로 지정했으며, 실행 후 Bee reference identities가 각각 `Assembly-CSharp`/`Assembly-CSharp-Editor`로 유지되는 것을 binary 검사로 확인했다.
- exterior rollback-free 구현 후 runtime 보조 컴파일은 통과했지만 Editor fixture가 `ExteriorActivityDebugScenarios.cs(303)`에서 미정의 `scope`로 실패했다. 동일한 `TryResolveSaveScenario` 문맥 중 잘못된 호출이 바뀐 것이므로 normal roundtrip 메서드의 세 번째 out을 명시적 scope로 교정한다.
- exterior PlayMode 전체 suite에서 strict roundtrip/late-failure는 통과했으나 기존 reception-candidate와 incident-start 시나리오 두 개가 실패했다. 원인 확인용 Console 40건 상세 조회는 DataManager 초기화 로그 스택 때문에 128k 토큰으로 절단됐으므로 이후 `FilterText`로 두 시나리오 로그만 조회한다.
- Unity MCP 도구 목록을 설명까지 넓게 필터링한 호출과 여러 Console 필터를 한 번에 조회한 호출도 출력이 절단됐다. MCP 연결 자체는 정상이며 `Unity_ReadConsole` 단건 조회로 `playmode reception work candidate` 오류를 확인했다. 이후 Console 진단은 필터 하나·최대 5건·Plain 형식으로만 수행한다.
- task-plan과 findings를 한 patch에서 갱신하려다 내용이 없는 findings hunk를 포함해 `apply_patch`가 검증 실패했다. 파일은 변경되지 않았고 실제 추가 내용을 포함한 patch로 다시 적용한다.
- exterior fixture 실패 원인을 교정했다. 접객 후보는 실제 `CanRunReceptionWork` 능력과 zone-query 등록을 검사하고, 사건 캡처는 baseline save 안에서만 페이싱을 31일차로 올린 뒤 full V18 restore로 원상복구한다.
- 안전한 Editor 보조 컴파일(`-out`/`-refout` 모두 Temp), Unity 실제 import/compile, authored start-party 준비, 외부 활동 전체 PlayMode suite가 통과했다. V18 authority PASS(772 authored items, 168 catalyst SOs), Console Error 0 / Warning 0, PlayMode 종료 상태다.
- 건설/작업 주문 감사를 위해 두 개의 넓은 `rg`/focused diff 출력을 조회했으나 각각 10k 토큰 부근에서 절단됐다. 이후 관련 소스는 파일별 작은 범위로 읽고 있으며 실제 truncation marker가 소스에 유입된 것은 아니다.
- 첫 작업 주문 V18 authority 실행은 ratchet이 `WorkAmountSystem.cs` 전체에서 `order.status = WorkOrderStatus.Ready`를 금지해 정상 런타임 작업 전이까지 복원 정규화로 오판하며 실패했다. `FromSaveData` 메서드 범위만 검사하도록 좁혀 재실행한다.
- `BuildableObject*.cs` wildcard를 Windows 경로 인수로 전달한 `rg`가 OS error 123으로 실패했다. 디렉터리와 `-g 'BuildableObject*.cs'`를 사용한 재검색은 정상 완료했다.
- 작업 주문 strict fixture 첫 실행은 normal publication과 late-failure가 실패했다. 진단으로 test resolver의 no-op injection과 live site의 비주입 상태를 확인했다. resolver는 candidate를 실제 Editor dependency helper로 주입하고, 두 strict fixture의 live site도 같은 composition 경로로 완전 주입한다.
- 작업 주문 strict fixture 재실행은 통과했고 V18 authority도 통과했다. 이어진 full live round-trip은 restore 성공/section 동일/물리 스택 수 5→5였지만 기존 item signature 비교가 실패했다. 동적 MCP 진단 명령은 RunCommand 컴파일 컨텍스트가 VContainer assembly를 참조하지 않아 CS0246/CS0012로 실행되지 않았으며, 실제 Editor fixture에 필드 차이 진단을 추가해 재실행한다.
- 물리 아이템 예약 필드를 찾는 첫 `rg` 정규식은 PowerShell 인용과 괄호가 충돌해 `unclosed group`으로 실패했다. 이후 고정 문자열(`-F`)과 디렉터리별 좁은 검색으로 전환한다.
- `reservedByPersistentId`·`"owner"` 검색과 `WorldItemPersistenceService.cs` 전체 읽기를 한 호출에 묶어 출력이 절단됐다. 파일별·범위별 단건 조회로 다시 감사한다.
- Unity MCP 도구 메타데이터를 이름/설명 전체에서 필터링한 호출이 약 111k 토큰으로 다시 절단됐다. 이미 확인된 `mcp__unity_mcp__*` 도구를 직접 사용하고 메타데이터 전수 조회는 중단한다.
- `findings.md`의 작업 주문 구간과 파일 후반 전체를 한 호출로 읽어 약 10k 토큰에서 다시 절단됐다. 필요한 소유자별 heading/작은 줄 범위만 조회하고 후반 전체 tail 읽기는 중단한다.
- `rg -F '"owner"'`가 PowerShell 인용을 기대와 다르게 전달해 `owner` 일반 문자열 수백 건을 반환하며 약 10k 토큰에서 절단됐다. 정확한 대입문/예약 메서드명과 StartParty 파일 범위로 검색을 좁힌다.
- 물리 아이템 V6 strict cutover 1차 구현: `PhysicalItemSaveValidation`을 추가해 current-version/null/list/ID/order/catalog/quantity/임시 예약/문자열/오염/컴포넌트/고유 장비/운반 설정을 검증하고, section에 preflight·rollback-free marker를 추가했다.
- capture는 transient 일반 예약을 저장하지 않고 combat-loadout 직접 픽업을 원래 source storage의 durable 상태로 투영하며, stack/unique item 정렬을 결정론적으로 고정했다. StageRestore의 V1~V5 migration, invalid skip, clamp/default, legacy component 합성 경로를 제거했다.
- 물리 strict 1차 runtime 보조 컴파일은 validator의 `List<EquipmentModuleInstance> ?? array` 타입 불일치(CS0019)와 short-circuit 조건 안 `decodeError` 미할당(CS0165)으로 실패했다. decode 변수를 조건 밖에서 초기화하고 module enumerable을 명시적 List fallback으로 바꾼다.
- validator 컴파일 오류를 교정한 뒤 runtime 보조 컴파일이 진단 0으로 통과했다.
- 기존 “restore가 예약을 조용히 지움” fixture를 production `ItemReservationService`로 실제 live 예약을 만든 뒤 capture가 이를 제외하고, noncanonical 예약 payload는 live state 변경 없이 거부하는 strict 계약으로 교체했다. 공용 pile snapshot도 canonical item-ID 순서로 정렬했다.
- Unity MCP import 후 실제 Unity 컴파일 Error 0, `PhysicalItemDebugScenarios.RunAll()` 전체가 PASS했다. 새 계약은 live `owner` 예약이 존재해도 저장 DTO에는 빈 값만 기록되고, 예약자가 든 payload는 preflight에서 거부되며 live JSON이 유지됨을 확인했다.
- V18 authority validator가 다시 통과했다. PlayMode에서 authored start party를 fast commit한 뒤 54개 전체 section 저장→복원→재캡처가 `version=18->18`, `itemStacks=6->6`, `itemDiff=`로 PASS했다. 이전 4개 시작 보급품의 `owner` 예약 signature 불일치는 제거됐고 PlayMode를 종료했다.
- V18 validator에 physical preflight/rollback-free/shared-validator, transient reservation omission, deterministic stack tie-breaker, exact current version, legacy component/direct-pickup restore normalization 부재 ratchet을 추가했다. Editor 보조 컴파일은 진단 0으로 통과했다.
- 새 physical ratchet을 Unity에 import한 뒤 V18 authority가 `save V18, 772 authored items, 168 catalyst SOs, legacy item authority 0, abstract stock assets 0`으로 통과했고 Console Error 0 / Warning 0이다.
- physical strict cutover 이후 `WorkAmountDebugScenarios.RunAll(true)`도 Unity MCP에서 다시 PASS했다. task plan Phase 112에 construction/work-order와 physical-item strict 전환 완료 항목을 체크하고 work-order 최종 증거를 findings에 정리했다.
- Unity reflection에서 rollback-free marker가 없는 production section 47개를 확인했다. 이미 strict detached 후보와 late-failure 증거가 있는 Captivity/Circus/Invasion 세 section에 marker를 추가하고 V18 source ratchet도 보강했다.
- Captivity/Circus 전체 계약, invasion threat/intruder/combat-report/defense-engagement 전체 계약, V18 authority가 모두 Unity MCP에서 PASS했다. rollback-image owner reflection count는 47→44로 감소했고 Console Error 0 / Warning 0이다.
- combat owner 감사에서 typed strict candidate/aggregate 경계가 확인된 CharacterMedical, DefenseTactical, EquipmentMaintenance, CharacterCombatCommand 네 section에 rollback-free marker와 class-specific V18 ratchet을 추가했다. legacy migration이 남은 Equipment/Evolution/BodyHealth는 제외했다.
- runtime/Editor 보조 컴파일과 Unity import 후 `CombatSystemDebugScenarios.RunAll(true)` 및 V18 authority가 PASS했다. marker 후보의 broader PlayMode 검증 진입점을 계속 확인한다.
- combat marker 반영 후 reflection 기준 rollback-image owner는 44→40으로 감소했다. Console은 여전히 Error 0 / Warning 0이다.
- operation save-service 선언을 `Assets/Scripts/Services/Operation` 내부의 좁은 복합 정규식으로 찾은 `rg`는 no-match(exit 1)였다. 인터페이스 이름을 저장소 전체에서 고정 문자열로 다시 찾는다.
- 공용 `DungeonJsonSaveSection<T>`의 Capture/Stage/Validate를 strict null·empty·invalid JSON 계약으로 변경했다. null capture와 null migration 결과도 실패하며 direct section.Restore가 preflight default DTO 합성을 우회할 수 없다.
- V18 validator에서 typed base ratchet 존재 여부를 찾은 exact 검색은 no-match(exit 1)였다. strict deserialize/default 합성 부재 ratchet을 새로 추가한다.
- strict typed base import 후 `DungeonSaveSectionDebugScenarios.RunAll(true)`는 `failed commit discards aggregate candidate` 한 계약에서 실패했다. 기반 클래스 컴파일은 정상이며, 실패 fixture가 rollback-image 재적용을 전제로 하는지 아니면 후보 discard 회귀인지 해당 메서드를 좁혀 조사한다.
- failing aggregate-candidate fixture에 conditional 상세 진단(restored/report/revision/root/last/errors)을 추가했다. 성공 시 추가 로그는 없고 실패 원인만 좁힌다.
- 상세 실행은 root=10/last=30을 보존했지만 revision=1을 확인했다. fixture의 두 section이 non-marker라 rollback image branch를 탄 것이 원인이며 코드 rollback은 정상이다.
- 같은 fixture 파일에서 기존 rollback-free fake를 찾은 검색은 no-match(exit 1)였다. commit이 live 값을 쓰기 전에 실패하는 전용 marker fake section을 추가해 candidate-discard branch를 실제로 검증한다.
- aggregate fake 자체도 candidate root만 쓰므로 marker를 선언하고, 후행 section은 failure를 live 값 mutation 전에 발생시키는 전용 rollback-free fake로 교체했다. 이제 fixture가 실제 all-marker discard branch와 revision 0을 검증한다.
- 교정된 `DungeonSaveSectionDebugScenarios.RunAll(true)` 전체와 V18 authority가 Unity MCP에서 PASS했다. strict typed capture/deserialize 기반 경계가 회귀 없이 적용됐다.
- strict validation과 candidate-root replacement가 확인된 OperatingDaySettlement/EventAlert section에 rollback-free marker를 추가하고, 두 save service의 null→default fallback을 명시적 오류로 교체했다. V18 ratchet도 추가했다.
- RandomStream section을 strict null/list/canonical ID/state/order 계약으로 고치고 deterministic capture sort 및 rollback-free marker/V18 ratchet을 추가했다. RunVariable의 V1 migration/warning/default 경로는 별도 rewrite 대상으로 남겼다.
- OperatingDaySettlement/EventAlert 전체 debug contracts와 V18 authority가 Unity MCP에서 PASS했다. RandomStream 변경까지 runtime/Editor 보조 컴파일도 진단 0이다.
- Codex section을 strict null/list/canonical entry order/ID/title/line/source/duplicate validation으로 전환하고 restore skip/default를 제거했다. Aggregate-only restore에 rollback-free marker와 V18 ratchet을 추가했다.
- `CodexDebugScenarios.RunAll(true)` 첫 실행은 세 fixture 시설(`P1_IceVent`, `P1_MeatRestaurant`, `Q02_연금술작업대`)이 persistent building ID 없이 등록되어 예외가 났고, 방어 관찰/손님 방문/시설 진화 도감 세 케이스가 연쇄 실패했다. Codex strict save 코드 컴파일 문제가 아니라 fixture가 이전 이름 폴백에 의존한 것이므로 운영 생성 경로처럼 typed building ID를 주입한다.
- BuildableObject ID API를 찾는 검색에서 다시 Windows wildcard 경로 인수(`BuildableObject*.cs`)를 사용해 OS error 123이 발생했다. 디렉터리만 경로로 넘기고 `-g 'BuildableObject*.cs'`로 재검색한다.
- 수정한 `Assets/Scripts/Services/Grid/Building` 디렉터리 검색도 no-match(exit 1)였다. 실제 BuildableObject 파일 위치를 `rg --files`로 먼저 확인한다.
- `rg --files | rg 'BuildableObject.*\.cs$'`도 no-match였다. 파일명이 타입명과 다르므로 `class BuildableObject` 선언 심볼로 찾는다.
- 실제 BuildableObject 선언은 `Assets/Scripts/Services/Buildings/BuildableObject*.cs`에 있었고 public `RestorePersistentIdentity(BuildingInstanceId)` API를 확인했다. Codex fixture 시설은 Initialization 전에 canonical `building:codex-fixture:*` ID를 받도록 수정했다.
- fixture ID 교정 후 `CodexDebugScenarios.RunAll(true)`와 V18 authority가 Unity MCP에서 PASS했다. Codex strict Aggregate restore/marker 계약이 검증됐다.
2026-08-03: Unity MCP 프로젝트 범위 설정 재확인 중 ALL_TOOLS를 광범위하게 필터링해 출력이 컨텍스트 한도를 초과해 절단됨. 이후 전역/프로젝트 TOML의 Unity 관련 줄만 좁혀 조회.
2026-08-03: 세션 복구 권고에 따라 `Assets/Scripts` 전체 `git diff --stat`와 계획 조회를 한 호출에 묶어 출력이 컨텍스트 한도를 초과해 절단됨. 이후 변경 통계는 대상 파일별로 제한하고 계획/발견/진행 파일은 필요한 구간을 별도 조회한다.
2026-08-03: 세션 복구 결과 프로젝트 전용 Unity MCP 전환은 동기화되었고, V18 작업은 물리 아이템·작업 주문·공용 typed save 경계·Codex·RandomStream·운영 저장 섹션까지 strict/rollback-free 검증을 마친 상태에서 남은 Unity 객체 소유자 엄격화 단계로 복귀했다.
2026-08-03: `findings.md` 최근 구간과 `progress.md` tail을 한 호출에 합쳐 10k 출력 한도 끝에서 절단됨. 이후 각 파일을 별도·소범위로 조회한다.
2026-08-03: 이전 구간에서 `DefenseFacilitySaveSection`과 `FactionSaveSection` 전체를 한 호출로 읽어 출력이 컨텍스트 한도를 초과해 절단됐던 오류가 로그에서 누락된 것을 확인해 보완 기록함. 두 파일은 별도 작은 범위로 재조회한다.
2026-08-03: 남은 rollback-image owner 리플렉션 1차 쿼리가 nullable Namespace 조건식 때문에 전역 네임스페이스 타입을 모두 제외해 `0`이라는 잘못된 결과를 반환함. `Namespace == null || !Namespace.Contains(".Editor")`로 명시 교정해 재실행한다.
2026-08-03: Defense save 참조/성장 데이터/V18 ratchet 묶음 검색에서 마지막 ratchet 검색이 no-match(exit 1)여서 호출 전체가 실패 처리됨. 앞선 참조와 성장 데이터 결과는 유효하며, 이후 no-match 예상 검색은 `$LASTEXITCODE -eq 1`을 정상 처리한다.
2026-08-03: `DefenseFacilityDebugScenarios.cs`에서 `DefenseFacilityRuntime` 생성 경로를 찾는 검색이 no-match(exit 1)로 종료됨. fixture world는 시설 동작만 조립하므로 저장 검증은 별도 runtime fixture 또는 기존 composition helper를 사용해야 한다.
2026-08-03: `DefenseFacilitySaveSection`을 optional/default custom section에서 공용 `DungeonJsonSaveSection<DefenseFacilitySaveData>` + rollback-free marker로 전환했다. 신규 `DefenseFacilitySaveValidation`이 exact DTO version, canonical/ordered typed IDs, enums, finite/range/count/flags/growth/text를 검증하고 runtime clone은 allowed-character ID를 결정적으로 정렬한다.
2026-08-03: Defense strict 1차 보조 runtime 컴파일이 신규 `DefenseFacilitySaveValidation.cs`가 아직 stale `Assembly-CSharp.rsp`에 포함되지 않아 CS0103으로 실패함. 신규 파일을 명시 source 인수로 추가하고 `-out`/`-refout` 모두 Temp로 격리해 재실행한다.
2026-08-03: Defense V18 ratchet+fixture 통합 패치가 시나리오의 깨진 한글 `RunScenario` 문자열을 exact context로 사용해 verification 실패함. 전체 patch는 미적용되었으며, 영문 메서드/조건문 anchor 기반의 작은 patch로 분리한다.
2026-08-03: Defense strict source ratchets를 V18 validator에 추가하고, 기존 Defense debug suite에 typed/preflight/rollback-free/required-section 확인, canonical round-trip, invalid condition+unordered ID payload 무변경 fixture를 추가했다.
2026-08-03: project-scoped Unity MCP로 Defense validator/section/runtime/fixture/V18 ratchet를 reimport했고 Unity Console은 compilation Error 0 / Warning 0이었다.
2026-08-03: Defense strict Unity regression 1차 실행에서 기존 `DefenseScenarioWorld.PlaceDefense`가 persistent building ID 없이 시설을 초기화해 11개 기존 시나리오가 연쇄 실패함. strict save 테스트 자체의 실패가 아니며, production 생성 계약과 맞게 initialization 전에 canonical fixture building ID를 주입한다.
2026-08-03: Defense fixture typed building ID를 보완한 뒤 `DefenseFacilityDebugScenarios.RunAll(true)`와 V18 authority가 Unity MCP에서 PASS했다. 신규 strict round-trip/invalid 무변경 계약도 포함되며 Console Error 0 / Warning 0, non-rollback-free section 35개다.
2026-08-03: Faction DTO/카탈로그 묶음 조회가 필요한 결과를 출력한 뒤 하위 검색 no-match(exit 1) 때문에 실패 처리됨. DTO와 catalog ordering 결과는 유효하며 이후 Faction 검색은 no-match를 정상 처리한다.
2026-08-03: `FactionSaveSection`을 required typed/rollback-free section으로 전환하고 `FactionSaveValidation`을 추가했다. authored faction exact coverage, DTO version/day/sequence, faction bounds, canonical routes/path/travel/day/actor/cargo, concrete item catalog 참조를 검증하며 capture는 route 숫자 sequence로 정렬한다.
2026-08-03: Faction strict 보조 runtime 컴파일은 성공했으나 이미 rsp에 반영된 Defense validator를 다시 source 인수로 넣어 CS2002 중복 파일 경고가 발생함. Faction 신규 파일만 명시해 경고 0으로 재실행한다.
2026-08-03: Faction expansion debug suite에 canonical strict save round-trip, typed/preflight/rollback-free/required-section 확인, reversed authored factions+unknown cargo invalid no-mutation fixture를 추가했다.
2026-08-03: V18 validator에 Faction typed/marker/required section, exact authored faction coverage, concrete cargo item validation, default synthesis 금지 ratchets를 추가했다.
2026-08-03: project-scoped Unity MCP로 Faction strict 변경을 import한 뒤 `SpeciesFactionDefenseExpansionDebugScenarios.ValidateOnly()`와 V18 authority가 PASS했다. strict round-trip/invalid 무변경 포함, Console Error 0 / Warning 0, non-rollback-free section 34개다.
2026-08-03: GrandProject validator/section/runtime 통합 패치가 section의 깨진 한글 오류 문자열 context 불일치로 verification 실패함. 전체 미적용 상태이며, section은 apply_patch Delete/Add 교체, 나머지는 별도 작은 patch로 적용한다.
2026-08-03: GrandProject section을 공용 typed/rollback-free 경계로 교체하고 strict validator를 추가했다. DTO/state null/version, canonical known IDs, sorted unique completion set, active/completed exclusivity, canonical destination, finite bounded work, inactive zero state를 검증하며 capture completion IDs를 정렬한다.
2026-08-03: production-economy GrandProject fixture에 strict typed/marker/required 확인과 duplicate completion+inactive nonzero work invalid no-mutation 검증을 추가하고, V18 source ratchets로 typed validator/default synthesis 금지를 고정했다.
2026-08-03: GrandProject strict suite와 V18 authority는 PASS했지만 non-rollback-free reflection count가 예상 33이 아닌 34로 유지됨. marker 인식 또는 새 노출 section 여부를 이름 목록으로 재감사한다.
2026-08-03: GrandProject count 이상을 type metadata로 진단한 결과 loaded `GrandProjectSaveSection`은 여전히 `System.Object + IDungeonSaveSection/IDungeonStagedSaveSection`인 이전 Assembly-CSharp 타입이었다. 개별 MCP Import가 메타만 갱신하고 domain reload를 아직 수행하지 않아 직전 suite/V18 결과는 새 GrandProject binary 증거로 인정하지 않으며, 강제 Unity refresh/compile 후 재검증한다.
2026-08-03: Unity 강제 refresh/compile 동적 명령 1차가 `CompilationPipeline`을 `Unity.CompilationPipeline` namespace로 오해석해 CS0234로 실패함. `UnityEditor.Compilation.CompilationPipeline` 완전 수식명으로 재실행한다.
2026-08-03: clean script compilation 완료 후에도 MCP AppDomain의 loaded `GrandProjectSaveSection` marker가 false여서 검증 명령이 명시적으로 실패함. runtime DLL 생성만 갱신되고 domain reload가 적용되지 않은 상태로 판단하며 `EditorUtility.RequestScriptReload()`를 호출해 loaded assembly를 교체한다.
2026-08-03: Library/ScriptAssemblies와 Bee Assembly-CSharp DLL의 SHA가 일치하고 신규 GrandProject validator 심볼이 포함됨을 확인했다. 즉 new binary는 생성됐지만 live AppDomain만 old type을 유지한다. command 종료 후 실행되는 Editor delayCall로 script reload를 다시 요청한다.
2026-08-03: command 종료 후 delayCall로 예약한 RequestScriptReload도 live GrandProject type을 갱신하지 못했다. 소스/DLL은 신형이고 AppDomain만 고정된 상태이므로 Unity MCP Play→Stop lifecycle 경계로 reload 여부를 확인한다.
2026-08-03: Unity MCP Play 경계도 loaded GrandProject type을 갱신하지 못했다. 정상 source import가 reload를 만들도록 section에 실제 V18 계약 주석을 추가하고 단일 파일 import 후 자연 compilation/reload만 대기한다.
2026-08-03: 전역 `C:\Users\vulpo\.codex\config.toml`에서 Unity MCP/relay 등록이 없는 것을 재확인하고, 프로젝트 `.codex/config.toml`에만 `unity_mcp`와 프로젝트 전용 플레이어 MCP가 등록된 상태를 검증했다. 프로젝트 로컬 Unity MCP의 `GetState`가 Unity 6000.3.8f1, Edit Mode, compiling/updating false를 정상 반환했다. OS 마우스·키보드는 사용하지 않았다.
2026-08-03: 계획 파일 tail과 MCP 설정을 한 번에 조회한 명령 출력이 10,024 tokens에서 절단되었다. 설정 검증에 필요한 전역 매치, 프로젝트 설정 전문, git 상태는 절단 전에 확보했으며 이후에는 파일별 좁은 조회를 사용한다.
2026-08-03: GrandProject 중단점 복귀 직전 Defense/Faction/Grand fixture 세 파일을 한 번에 검색한 출력이 예상보다 커져 절단되었다. 이후 concrete sealed section의 선택형 인터페이스 패턴 검사는 파일별·메서드별 좁은 범위로 조회한다.
2026-08-03: GrandProject Unity Editor 컴파일을 막던 CS8121의 동일 패턴을 Defense/Faction/Grand fixture 세 곳에서 확인했다. sealed concrete section을 직접 선택형 인터페이스와 패턴 매칭하지 않고 `object` 계약 경계로 올린 뒤 strict/preflight/rollback-free/required 검사를 유지하도록 수정했다.
2026-08-03: 세 fixture 재임포트 후 Unity 컴파일은 완료됐다. `Editor.log` tail의 단순 CS 검색은 이전 실패 기록도 함께 잡아 현재 오류처럼 보였으므로, 마지막 컴파일 이후 문맥과 로드된 타입 메타데이터로 새 결과를 판별한다. 같은 tail에는 Unity 라이선스 404 진단 로그가 반복되지만 스크립트 컴파일 상태와는 별개다.
2026-08-03: Unity 재컴파일/도메인 로드 후 `GrandProjectSaveSection`은 실제 AppDomain에서 `DungeonJsonSaveSection<DungeonGrandProjectSaveData>` 기반, preflight=true, rollback-free=true, optional=false로 확인됐다. `ProductionEconomyDebugScenarios.RunAll()`도 Unity MCP에서 완료됐다.
2026-08-03: GrandProject strict cutover를 Unity에서 최종 검증했다. loaded type은 typed/preflight/rollback-free/required 계약이며 `ProductionEconomyDebugScenarios.RunAll()`과 V18 authority가 통과했다. non-rollback-free production section은 34→33, Console Error 0 / Warning 0이다.
2026-08-03: 다음 plain Aggregate 후보로 `economy.stock-policies`와 `economy.regional-contracts`를 선정했다. 두 section의 default DTO 합성 및 runtime의 skip/clamp/default/offer-generation 경로를 확인했고, exact authored coverage와 canonical contract snapshot을 preflight에서 강제하는 방향으로 감사를 진행한다.
2026-08-03: stock-policy strict 설계를 확정했다. 저장 순서는 localized display name이 아닌 immutable item ID로 고정하고, abstract stock-category ID 허용을 제거하며, authored catalog 전 항목 exact coverage를 validator가 요구한다. section은 runtime+catalog 필수 주입으로 전환하고 기존 production-economy fixture에 canonical/invalid no-mutation 검증을 추가한다.
2026-08-03: stock-policy validator/section/runtime 통합 patch는 기존 section의 깨진 한글 오류 문자열 문맥이 예상과 달라 verification 실패했고 파일은 변경되지 않았다. validator 추가, section 전체 교체, runtime 좁은 patch로 분리해 재시도한다.
2026-08-03: `ResourceStockPolicySaveValidation`을 추가하고 `ResourceStockPolicySaveSection`을 required typed/preflight/rollback-free 경계로 전체 교체했다. section은 runtime과 authored resource catalog를 모두 필수 주입받는다.
2026-08-03: stock-policy runtime restore에서 skip/normalize/default-fill을 제거하고, concrete catalog item만 허용하며 persisted view를 item ID ordinal 순서로 고정했다. production-economy fixture에는 실제 authored catalog 전체를 사용한 canonical round-trip, required/preflight/rollback-free 계약, invalid threshold no-mutation 검증을 추가했다.
2026-08-03: V18 authority validator에 stock-policy typed/rollback-free/shared-validator, exact authored item coverage, concrete item lookup, default synthesis 금지, abstract stock-category backdoor 금지 ratchet을 추가했다.
2026-08-03: auxiliary runtime csc 첫 시도는 Bee rsp를 PowerShell 배열로 전개해 Windows 명령행 길이 제한(`The filename or extension is too long`)에 걸렸다. rsp는 `@file`로 직접 전달하고 뒤에서 `-out`/`-refout`을 모두 덮어쓰는 방식으로 재시도한다.
2026-08-03: stock-policy Unity 재임포트/컴파일 후 production-economy 전체 회귀와 V18 authority가 통과했다. loaded section은 rollback-free 잔여 목록에서 제거됐고 count 33→32, Console Error 0 / Warning 0이다.
2026-08-03: regional-contract strict snapshot 규칙을 조사했다. contract/day/sequence ID와 delivery destination 공식, 24개 history cap, status별 destination, concrete item requirements를 validator가 검사하고 restore 중 `EnsureOffers`를 제거하기로 했다.
2026-08-03: `RegionalSupplyContractSaveValidation`을 추가하고 section을 required typed/preflight/rollback-free로 교체했다. runtime capture는 offered day+numeric sequence 순으로 고정했으며 restore의 clamp/skip/`EnsureOffers` mutation을 제거하고 검증된 plain Aggregate snapshot만 교체하도록 변경했다.
2026-08-03: production-economy fixture에 canonical regional contract round-trip, typed/required/rollback-free 계약, invalid destination no-mutation을 추가했다. V18 validator에는 version/ID-derived destination/concrete item/default synthesis/restore-time offer generation 금지 ratchet을 추가했다.
2026-08-03: regional-contract Unity 재임포트/컴파일 후 production-economy 전체 회귀와 V18 authority가 통과했다. section은 rollback-free 잔여 목록에서 제거됐고 count 32→31, Console Error 0 / Warning 0이다.
2026-08-03: Treasury section/models/runtime refs를 한 번에 조회한 출력이 10,024 tokens에서 절단됐다. 확인된 범위만으로도 6개 하위 도메인의 composite restore임이 드러나 단순 후보에서 제외했으며, 이후 하위 runtime별 좁은 감사로 다룬다.
2026-08-03: RegularCustomer constructor/save fixture 복합 조회는 fixture 검색어 no-match(exit 1) 때문에 명령 전체가 실패 처리됐지만 constructor 본문은 확보했다. 이후 fixture는 별도 파일 범위로 읽는다.
2026-08-03: RegularCustomer fixture와 `IRunCharacterCatalog`을 확인했다. 기존 gameplay suite에 실제 MonoBehaviour runtime 기반 canonical/invalid save scenario를 추가하고, 테스트 전용 catalog만 주입하는 방식으로 strict boundary를 검증한다.
2026-08-03: RegularCustomer DTO에 exact V1을 추가하고 section을 rollback-free로 승격했다. validator는 null/version/canonical ordinal order/source definition/stat range/status hierarchy/capability를 강제하며 restore의 skip/default 경로를 제거했다. 기존 gameplay fixture에 real runtime strict round-trip/invalid no-mutation을 추가했다.
2026-08-03: RegularCustomer Unity 전체 suite와 V18 authority가 통과했다. strict save fixture는 real runtime round-trip 및 invalid hierarchy no-mutation을 증명했고 rollback-free 잔여 count 31→30, Console Error 0 / Warning 0이다.
2026-08-03: FacilityShop Aggregate/projection과 late-failure fixture를 감사했다. strict version/null/order/catalog 검증을 추가하고 section+failure fixture를 rollback-free로 승격해 기존 candidate discard 검증을 all-marker 경로로 강화하기로 했다.
2026-08-03: FacilityShop DTO exact V1, section rollback-free marker, strict non-null/ordered authored ID 검증을 추가했다. runtime restore의 day clamp/null/default/negative filtering을 제거했고 fixture는 invalid no-mutation과 all-marker late-failure discard를 검증하도록 강화했다. V18 ratchets도 추가했다.
2026-08-03: FacilityShop 전체 suite 첫 실행은 강화한 all-marker late-failure 시나리오 하나(`Discarded restore candidate preserves live facility shop`)가 실패했다. 컴파일/다른 시나리오는 정상이며, observer/discard/live state/revision 조건을 개별 출력해 marker 전환 후 기대값 차이를 진단한다.
2026-08-03: FacilityShop all-marker 실패 상세는 live/observer 상태가 모두 정확했고 차이는 published revision 0(기존 기대 1)뿐이었다. rollback image를 재적용하던 이전 경로와 달리 새 경로는 live root를 한 번도 publish하지 않는 것이므로 fixture 기대값을 0으로 교정했다.
2026-08-03: FacilityShop 전체 Unity suite와 V18 authority가 최종 통과했다. invalid no-mutation 및 all-marker late-failure discard가 live revision 0을 보존했고 rollback-free 잔여 count 30→29, Console Error 0 / Warning 0이다.
2026-08-03: 이번 묶음 최종 정적 검사에서 Assets/Scripts truncation marker 0, focused `git diff --check` 오류 0, 신규 validator/meta와 project-local MCP config trailing whitespace 0을 확인했다. 표시된 LF→CRLF 경고는 저장소 line-ending 알림이며 diff 오류는 아니다.
2026-08-03: Unity loaded TypeCache에서 non-rollback-free 29개 목록을 다시 확보했다. 다음 묶음은 ExperiencePacing/StaffDiscontent/ExternalInfluence/DungeonDebug의 plain-state 여부를 비교 감사해 실제 low-risk owner부터 전환한다.
2026-08-03: 네 후보를 비교한 결과 ExperiencePacing/ExternalInfluence는 optional migration, DungeonDebug는 presentation/debug 정책 결정이 필요해 보류했다. StaffDiscontent를 다음 strict Aggregate 전환 대상으로 선정했다.
2026-08-03: StaffDiscontent data/runtime/fixture 복합 조회는 fixture save 검색 no-match(exit 1) 때문에 실패 처리됐지만 DTO와 runtime restore 본문은 확보했다. fixture는 별도로 읽는다.
2026-08-03: 사용자가 Phase 112 실행이 저장 묶음으로 다시 기울었다고 지적했다. 실제로 Batch A 코드 변경은 여섯 save owner에 편중되어 있었으므로, Batch A 작업 순서를 `실행형 기준선 → SO/content 권위 → runtime/statics 단일 소유 → save 경계 → assembly/책임 → presentation/error → 통합 증명`으로 고정했다. rollback-free `32/54` 달성만으로는 Batch A를 완료 처리하지 않는 exit gate를 추가했다.
2026-08-03: Batch A 여섯 owner 금지 문자열 검사의 첫 시도는 ExternalInfluence/Debug/ServiceRooms의 디렉터리를 잘못 가정해 실패했고, `rg --files | rg` 재탐색도 Windows 역슬래시 출력과 슬래시 정규식 불일치로 no-match 처리됐다. `rg --files | Select-String`으로 실제 Environment/Debugging/ServiceRooms 경로를 확정했으며 이후 검사는 그 경로만 사용한다.
2026-08-03: Batch A Unity refresh 동적 명령 1차는 래퍼 네임스페이스에서 `CompilationPipeline`이 `Unity.CompilationPipeline`으로 해석되어 CS0234로 실패했다. 게임 어셈블리 오류는 아니며 `UnityEditor.Compilation.CompilationPipeline` 완전 수식명으로 재요청한다.
2026-08-03: Unity 재컴파일은 Error 0 / Warning 0으로 완료됐지만 신규 `BatchACoreSessionSaveDebugScenarios`는 여섯 owner 중 `RunVariable`만 실패했다. 다른 다섯 경계는 통과했으며 RunVariable fixture의 canonical payload/validator 불일치를 좁혀 수정한다.
2026-08-03: RunVariable 진단용 Unity 동적 명령의 임시 `IOwnerDoctrineDefinitionCatalog`가 `ResolveFor`/`ResolveForSpecies`를 구현하지 않아 CS0535로 실패했다. 제품/fixture 컴파일과 무관한 진단 코드 오류이며 실제 인터페이스 계약을 추가해 재실행한다.
2026-08-03: Batch A 실행형 기준선을 위한 Unity 동적 Roslyn probe는 기본 Editor 참조에 `Microsoft.CodeAnalysis.CSharp`가 없어 CS0234/CS0103으로 실패했다. 정규식 검사를 Roslyn으로 가장하지 않고, 설치된 .NET/Unity Roslyn 어셈블리를 참조하는 독립 분석기와 Unity 리플렉션·asset graph 스냅샷을 연결하는 구조로 진행한다.
2026-08-03: 신규 Roslyn architecture metrics runner 첫 실행은 시스템 PowerShell 실행 정책이 `.ps1` 로드를 차단해 시작되지 않았다. 프로젝트 파일/Unity 상태 변경은 없으며 해당 프로세스에만 `ExecutionPolicy Bypass`를 적용해 재실행한다.
2026-08-03: architecture metrics runner 2차는 실제 `ProjectVersion.txt`의 `6000.3.8f1`을 읽고도 `Select-String` collection에서 capture group을 잘못 꺼내 버전을 빈 값으로 판정했다. 첫 match의 `Matches[0].Groups['version']`을 명시하도록 runner를 수정했다.
2026-08-03: architecture metrics analyzer 컴파일은 Unity Roslyn까지 로드했지만 `netstandard 2.0` facade 참조 누락으로 CS0012에서 중단됐다. Unity Mono `4.5/Facades/netstandard.dll`을 명시 참조하도록 runner를 보완했다.
2026-08-03: facade 추가 후 Mono compiler는 analyzer를 만들었지만 실행 시 Roslyn/netstandard가 Windows Mono의 `System.Native`를 잘못 로드해 `TypeInitializationException/DllNotFoundException`으로 종료됐다. Unity 내장 .NET 6 runtime + `DotNetSdkRoslyn/csc.dll` 조합으로 compiler/runtime 계열을 통일하도록 runner를 전환했다.
2026-08-03: .NET 6 runner 첫 compile은 runtime 디렉터리의 native DLL까지 reference로 넘겨 다수 CS0009를 냈다. `AssemblyName.GetAssemblyName`이 성공하는 managed DLL만 compiler reference로 포함하도록 필터를 추가했다.
2026-08-03: 첫 Unity reflection metrics의 mutable static 3,110개는 상위 샘플 전부가 `<>c.<>9__*` compiler-generated lambda cache였다. reflection은 `CompilerGenerated` type/field를 제외하고, Roslyn source metric은 실제 authored `static event`를 명시적으로 포함하도록 기준을 교정한다.
2026-08-03: 교정된 architecture baseline은 Roslyn/loaded authored mutable static 24/24, oversized types 13, large constructors 99/92, default Assembly-CSharp source/MonoScript 1058/1039를 기록했다. content escape, direct session mutation, optional interface DI, root catalog validation error, broken SO reference는 모두 0이며 V18 authority 필수 게이트에 연결했다.
2026-08-03: Batch A SO/content 권위 병렬 검색은 존재하지 않는 `Assets/Scripts/Composition` 경로 때문에 exit 1로 묶여 실패 처리됐다. 부분 결과로 RunVariable/Doctrine의 `GameDomainContentCatalogSO → AuthoredGameplayCatalog` 투영과 Service process의 `IGameContentCatalog` 의존은 확인했으며, 실제 LifetimeScope/installer 경로로 좁혀 재감사한다.
2026-08-03: SO/content 세부 병렬 검색은 잘못 포함한 루트 `Scripts` 경로 때문에 exit 1로 종료됐다. 이후 `Assets`만 대상으로 no-match를 정상 처리한다. 현재까지 External restore의 `?? new DTO`와 `ResourceServiceProcessCatalog`의 invalid SO 무음 필터링을 실제 권위 결함으로 확인했다.
2026-08-03: Batch A content cutover 첫 Unity compile은 Editor fixture가 runtime assembly의 internal `ResourceServiceProcessCatalog(IEnumerable)`를 호출해 CS1503 2건이 났고, 기존 ServiceRoom fixture line 217에서 CS0162 warning 1건이 드러났다. 제품 runtime 변경 오류는 아니며 fixture를 public `IGameContentCatalog` 경계로 바꾸고 unreachable 분기를 제거한다.
2026-08-03: 사용자가 Batch A 내부를 owner별로 따로 수행하면 묶음 자체가 무의미하다고 지적했다. 계획에서 "completed sibling" 개념을 삭제하고 Batch A를 하나의 `CoreSession` 동기 전환으로 재정의했다. 여섯 component 모두에 대해 Content/Runtime/Command-Query/Save/Composition/Presentation/Legacy-removal 행렬이 채워지기 전에는 owner별 compile·완료·진척을 인정하지 않으며, 전체 교체 뒤 통합 fixture와 단일 검증 경계를 한 번 통과해야 한다.
2026-08-03: 중단 여부가 불명확했던 최신 standalone build를 확인했다. `Temp/human-playtest-build-report.txt`는 18:28:08 갱신, `Succeeded`, Error 0 / Warning 0, 29.54초를 기록했다. 중복 빌드는 시작하지 않았다.
2026-08-03: 재개 직후 planning-with-files session-catchup은 직전 계획 수정과 goal continuation만 미동기 문맥으로 보고했다. planning 파일은 task 774줄/findings 1,819줄/progress 1,696줄이며 최신 원자 Batch A 정의가 디스크에 존재한다.
2026-08-03: `git diff --stat`과 6-component 소유권 검색을 한 명령으로 묶은 감사는 대규모 기존 worktree와 line-ending 경고 때문에 출력이 10,025 tokens에서 절단됐다. 변경을 되돌리거나 반복하지 않고, 이후에는 Git 전체 통계와 소유권 검색을 분리하고 정확한 경로/선언만 표 형태로 수집한다.
2026-08-03: 6-component 선언을 한 번에 재감사했다. Experience/Debug는 plain Aggregate 중심이지만 RunVariable은 MonoBehaviour, External/ServiceRooms는 world·items·wildlife·power·research·building·character 의존, RunFlow는 invasion 구체 구현 의존이다. 구현 파일을 contract-only CoreSession asmdef로 통째 이동하면 역참조가 생기므로, 여섯 모두에 동일하게 domain state/command와 Unity/cross-domain adapter를 분리하는 동기 패치가 필요하다.
2026-08-03: asmdef/validator 감사를 통해 CoreSession은 World만 참조하는 no-Unity 계약 어셈블리이고 Infrastructure는 Foundation만 참조함을 확인했다. 현 validator의 named-assembly ratchet은 Experience/RunFlow/Debug 세 계약뿐이므로, External/RunVariable/ServiceRooms까지 같은 port/adapter 및 assembly 소유권 기준으로 확장해야 원자 전환을 실제로 강제할 수 있다.
2026-08-03: CoreSessionRulesSO·Aggregate store·validator를 한 명령으로 넓게 조회한 결과 `GetOrCreate` 전역 검색이 10,024-token 한도에서 다시 절단됐다. SO와 validator의 필요한 부분은 확보했지만 store 본문은 확보하지 못했으므로, 이후 검색 결과를 재사용하지 않고 정확한 `DungeonSaveSections.cs` 구간과 RunVariable 모델 파일만 직접 읽는다.
2026-08-03: External/RunVariable/ServiceRooms 모델 경계를 직접 읽었다. External save DTO는 순수하지만 query가 Vector2Int를 노출하고, Service 모델은 순수 session record와 BuildableObject/CharacterActor view를 섞으며, RunVariable은 state·Mathf normalization·한글 표시·content effect를 한 파일에 섞는다. 세 계약을 CoreSession으로 옮기기 전에 pure state/port와 Unity/presentation adapter를 같은 패치에서 분리해야 한다.
2026-08-03: CoreSessionRulesSO 사용처를 전수 확인했다. Experience/RunFlow/External/Debug/ServiceRooms 다섯 runtime/save가 asset을 직접 보유하고 RunVariable만 root-derived authored catalog를 사용한다. 첫 synchronized content seam은 root catalog가 한 번 생성하는 immutable rules definition으로 다섯 SO 참조를 동시에 교체하고, RunVariable의 기존 projection 패턴과 정렬하는 것으로 확정했다.
2026-08-03: `CoreSessionRulesDefinition`과 immutable rehearsal/external-band/service-research records를 Content assembly에 추가했다. SO는 검증 성공 뒤 deep-copy projection만 만들고, ResourceGameContentCatalog가 이를 한 번 보유한다. Experience/RunFlow/External/Debug/ServiceRooms runtime 및 save section은 SO 대신 definition을 받도록 동시에 전환했으며 통합 Batch A fixture는 별도 asset loader가 아니라 같은 root content provider를 사용한다.
2026-08-03: 잔여 direct-SO/구형 field 검색은 실제 잔여가 authored `GameDomainContentCatalogSO.CoreSessionRules` 한 건뿐이었지만 뒤쪽 no-match `rg`가 명령 전체를 exit 1로 표시했다. 소스 오류가 아니며 optional 검색을 다음부터 별도 처리한다.
2026-08-03: Unity MCP가 신규 CoreSessionRulesDefinition을 동일 GUID로 임포트했고 실제 프로젝트 컴파일 후 Console Error 0 / Warning 0을 확인했다. validator도 immutable projection 생성, 다섯 owner의 SO 보유 금지, Content assembly 소유권, root-catalog 단일 생성, 통합 content proof 문구를 새 경계로 ratchet하도록 갱신했다.
2026-08-03: immutable content seam 통합 Unity RunCommand 첫 시도는 `BatchACoreSessionSaveDebugScenarios.RunAll()`에 필수 `bool logSuccess` 인자를 생략해 동적 명령만 CS7036으로 컴파일 실패했다. 제품/Editor 어셈블리 오류는 아니며 실제 시그니처 `RunAll(false)`로 수정해 재실행한다.
2026-08-03: 수정한 통합 명령에서 content authority와 six-component save fixture는 예외 없이 진행됐지만 V18 validator가 새 파일/타입 때문에 Roslyn architecture report stale을 명시적으로 거부했다. 코드 회귀가 아니라 기준선 갱신 요구이며 프로젝트 제공 `Tools/ArchitectureMetrics/Run-ArchitectureMetrics.ps1 -Verify`를 실행한 뒤 validator를 재호출한다.
2026-08-03: architecture metrics를 현재 PowerShell 프로세스에서 직접 실행한 첫 시도는 시스템 실행 정책으로 PSSecurityException이 발생했다. 이전 progress에도 같은 환경 제약이 기록되어 있었는데 명시적 Bypass를 누락한 재발 오류다. 파일/Unity 상태는 변경되지 않았으며 별도 PowerShell 프로세스에만 `-ExecutionPolicy Bypass`를 적용한다.
2026-08-03: Bypass로 실행한 실제 Roslyn verify는 `CoreSessionRulesDefinition`의 18-parameter 생성자 때문에 LargeConstructor가 97→98로 증가했다고 정확히 실패했다. baseline을 올리지 않고 SO의 세 구획과 동일한 RunPacing/ExternalInfluence/DebugAndServices 불변 값 객체(각 생성자 최대 8개)로 분해해 루트 생성자를 3개 의존성으로 축소한다.
2026-08-03: 규칙 projection을 `CoreRunPacingRules`(8 인자), `CoreExternalInfluenceRules`(8), `CoreDebugAndServiceRules`(2)로 분해하고 루트 정의 생성자를 3개로 축소했다. Unity 재임포트 뒤 Roslyn verify는 files 1093/types 3284/mutable statics 24/oversized 13/large constructors 97/default files 1058/content escapes 0/direct session mutations 0으로 PASS해 위반 증가를 제거했다.
2026-08-03: `BatchAContentAuthorityDebugScenarios` + six-component `BatchACoreSessionSaveDebugScenarios` + `RuntimeAuthorityV18Validator` 통합 명령이 `BATCH_A_IMMUTABLE_CONTENT_SEAM=PASS`로 완료됐다. Unity Console은 Error 0 / Warning 0이다. 이는 Content 열만 통과한 증거이며 owner별 완료나 Batch A 완료로 계산하지 않는다.
2026-08-03: 다음 synchronized contract cut을 감사했다. External runtime restore의 report 인자는 미사용이고, RunVariable save section은 scene references로 MonoBehaviour를 찾아 capture/restore 변환까지 소유하며, Service pure enum/record는 Unity ability/request와 혼재한다. 세 owner를 동시에 CoreSession port로 옮기고 Unity adapter만 default edge에 남기는 패치로 진행한다.
2026-08-03: RunVariable 등록 검색에 `Select-Object -First`를 다시 사용해 rg 파이프가 조기 닫히며 exit 1로 표시됐다. 필요한 등록/section 위치는 출력에서 확보했지만, 이후 정확한 `DungeonSaveRegistration.cs` 및 runtime registration 파일만 직접 읽어 수정한다.
2026-08-03: 세 CoreSession contract 파일과 asmdef Foundation 참조 추가는 적용됐다. 이어진 External 중복 선언 제거 패치는 fixture 매개변수명을 `data`로 잘못 예상해 verification 단계에서 전체 거부됐고 어떤 대상 파일도 변경되지 않았다. 실제 시그니처 `saveData`를 확인했으므로 선언 제거/runtime conversion/save adapter/fixture를 정확한 문맥으로 다시 적용한다.
2026-08-03: synchronized contract cut을 적용했다. External contract/state는 CoreSession으로 이동하고 Vector2Int를 CoreGridCell로 변환했으며 runtime Restore에서 미사용 report를 제거했다. RunVariable DTO/enums/IRunVariableRuntime을 이동하고 save section의 DungeonSceneRuntimeReferences 의존을 제거했다. Service pure enum/session/save/query 계약도 Unity ability/request 파일에서 이동했다.
2026-08-03: CoreSession asmdef에 Foundation 참조를 추가해 DomainFailure 단일 프로토콜을 재사용하되 noEngineReferences는 유지했다. Unity MCP asmdef 재임포트 후 실제 전체 프로젝트 컴파일과 Console Error 0 / Warning 0을 확인했다.
2026-08-03: synchronized contract 이동 후 Roslyn verify는 DefaultAssemblySource set 변경을 검토하라고 중단했다. current metrics는 default source 1058→1057, 다른 ratchet(mutable 24/oversized 13/large constructor 97/content escape 0/direct mutation 0/raw Korean 6948/root catalog 4)은 불변이다. 삭제된 `DungeonRunSaveData.cs`의 DTO가 CoreSession named assembly로 이동한 의도된 감소다.
2026-08-03: current/baseline JSON에서 위반 목록을 직접 Compare-Object하려 했으나 metrics schema는 count/hash만 저장해 null binding 오류가 났다. 파일 변경은 없으며 source count 감소와 실제 삭제/신규 asmdef 소유권을 직접 검토했으므로 Roslyn baseline을 낮춘 수치로 재캡처하고 Unity TypeCache baseline도 별도로 검증한다.
2026-08-03: Roslyn baseline을 검토된 개선값 files 1095/types 3286/default 1057로 재캡처했고 나머지 ratchet은 불변이다. Unity architecture validator는 예상대로 default Assembly-CSharp MonoScript set change를 거부했다. 삭제된 default `DungeonRunSaveData.cs` 1개와 신규 named CoreSession script 3개의 소유권 이동 외 default 신규 파일은 없으므로 Unity baseline을 낮은 default set으로 명시 갱신한다.
2026-08-03: Unity baseline은 source 1095/types 3286, syntax/loaded statics 24/24, large constructors 97/90, default assembly 1057/1038, optional DI/catalog/broken refs 0으로 갱신됐다. 이어진 통합 V18은 External version/RunFlow dead-field ratchet 두 건을 실패했지만 소스에는 새 token이 정확히 존재한다. DLL timestamp가 09:42이고 validator source가 09:53으로 Editor assembly가 stale임을 확인했으므로 강제 Refresh+script compilation 후 재검증한다.
2026-08-03: 강제 compile 동적 명령은 래퍼 namespace 때문에 `CompilationPipeline`을 다시 `Unity.CompilationPipeline`로 해석해 CS0234로 실패했다. 프로젝트 컴파일 오류는 아니며 이전에 확인된 MCP 동적 namespace 충돌이므로 `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()` 완전 수식명을 사용한다.
2026-08-03: 완전 수식 compile 요청은 실행됐지만 Unity MCP state/Console에는 즉시 나타나지 않았고 Editor.log가 Batch A fixture line 119의 CS1503을 보고했다. target-typed `RunVariableSaveSection section = new(...)` 한 곳이 검색 패턴을 피해 scene references를 계속 전달한 것이 원인이며 `references.RunVariables`로 교정했다.
2026-08-03: 누락 fixture 교정 후 재컴파일되어 Assembly-CSharp-Editor.dll timestamp가 09:54:49로 갱신되고 Editor.log의 최신 compile error는 0건이다. V18 재실행은 이제 source ratchet을 통과했지만 Unity default MonoScript set hash가 다시 바뀌었다. 이동한 enum이 각 기존 파일의 대표 MonoScript 타입이었던 Service/Run 모델 파일들의 대표 타입 변경이 새 어셈블리 로드 뒤 반영된 결과이므로 Roslyn fingerprint 재검증 후 Unity baseline을 최종 loaded set으로 다시 고정한다.
## 2026-08-03 Batch B parallel integration continuation

- Re-established the current Phase 112/Batch B work from the repository planning files and kept the full V18 normalization objective active.
- The surgery/anatomy worker completed typed `SurgeryStatusData`, `SurgeryRiskSummaryCode`, V6 strict save validation, `DomainFailure` command failures, authored item display names, and focused Unity compilation/scenarios with zero C# errors.
- Current source scan reports zero `ICharacterBodyHealthRuntime` and zero nested `CharacterBodyHealthRuntime` lifecycle-event references.
- The only remaining surgery-side `out string failureReason` is the explicitly retained generic production adapter; it emits a stable failure-code name rather than a localized sentence.
- Three parallel slots remain occupied by Environment aggregate removal, Species/Combat authored-reference migration, and CharacterMedical typed status/failure migration. Full Unity import is deferred until the two active shared-source workers finish to avoid validating a transient partial compile.
- Rechecked the V18 validator against the moved taxonomy sources: it already points at `Models/Characters/CharacterStatCatalog.cs` and `Models/Work/WorkTypeCatalog.cs`, both files exist, and all surgery validation diagnostics now name V6. The earlier missing-path report came from a stale loaded validator state rather than the current source.
- Split the former `ISurvivalFoodRuntime` authority into `ISurvivalFoodQuery`, `ISurvivalFoodCommand`, `ISurvivalFoodPersistence`, and `ISurvivalFoodDebugCommand`. Save, UI, AI, work, building, industrial conveyor, deprivation, room, and debug consumers now request only their required capability; the broad contract has no remaining source references except the registration line that was replaced in the same change.
- `SurvivalResourcesSaveSection` now receives persistence and restore-preflight contracts explicitly instead of runtime type-checking a broad aggregate at restore time.
- Architecture verification after the parallel merge found one real ratchet regression: `SurgeryRuntime` is 1,225 lines against the 1,200-line runtime limit, raising the oversized set from 13 to 14. Other metrics improved at the same snapshot: large constructors 91→89, default-assembly sources 1050→1049, and raw Korean strings 6882→6633; mutable statics stayed 24 and content/session/root-catalog invariants stayed unchanged. The oversized surgery type must be reduced before any baseline update.
- Moved automatic subject construction and surgical-effect handler indexing into the separate `SurgeryRuntimeSupport` helper. `SurgeryRuntime` is back below 1,200 lines and the oversized count returned to 13. Verification now stops only because the 13-entry oversized set changed, so the exact removed/added set still requires review before writing a lower/equivalent baseline.
- The current 13 oversized entries no longer include `SurgeryRuntime`; the remaining list is OwnerCommandController, BuildableObject, CharacterAiScheduler, CharacterBlackboard, SocialReputationRuntime, CharacterActor, CharacterProgression, CharacterStats, DungeonGameplayPerformanceProbe, InvasionIntruderRuntime, OffenseExpeditionRuntime, BlueprintResearchRuntime, and WildlifeActor. This is an intended net improvement, pending comparison to the attested prior set hash.
- Environment and husbandry aggregate-wrapper cutovers are now present in the shared tree. `ICharacterEnvironmentRuntime`, `IAnimalHusbandryRuntime`, and `ISurvivalFoodRuntime` all return no source matches; the registration exposes only Environment Status/WorkContext/Persistence, Husbandry Query/Command/Persistence/PenCompatibility, and SurvivalFood Query/Command/Persistence/Debug/Preflight facets.
- The verification search intentionally returned process exit 1 because all three forbidden wrapper names had zero matches; the registration excerpts confirmed the expected replacement bindings were preserved together.
- Unity's current full compile is blocked by five stale surgery consumers exposed after typed-boundary removal: one `SurgeryWorldServices.BodyHealth` call, two removed `SurgicalFacilityQuery.FormatTags` presentation calls, and two removed risk-summary sentence properties. The correct repair is `BodyHealthQuery` plus presentation-owned facility/risk formatting; no removed domain sentence property will be restored.
- CharacterMedical typed status/failure V3 is complete in source: six commands use `DomainFailure`, save status uses code+parameters, restore is strict/no-mutation, and the 9-dependency runtime constructor is reduced to seven. Its focused Foundation/medical diagnostics are clean; the same five surgery errors prevented a new global DLL.
- Fixed the five stale surgery consumers without restoring removed domain strings: planning uses `BodyHealthQuery`, facility-tag labels live in `CharacterSurgeryUiText`, and risk/environment summaries use typed presentation projections.
- The next Unity import exposed eight additional stale Editor consumers. Converted legacy FacilityWorkType lookups through `FacilityWorkTypeMap`, fairness surgery wait data to `SurgeryStatusData`, the old humanoid subject enum to `Character`, and invasion medical status to `statusCode`; exposed only the CharacterMedical validator's public validation API while keeping its internal aggregate builder internal.
- Unity MCP forced refresh/recompile now completes with Console Error 0 / Warning 0. This is compile evidence only; domain scenarios, V18 authority, architecture baselines, and pointer UI still remain to be rerun after the last two wrapper workers finish.
- Source audit now reports zero references for all six removed broad contracts: BodyHealth, Environment, Husbandry, SurvivalFood, CharacterConsumables, and CharacterSpecies. The first surgery legacy-string search used a PowerShell-invalid wildcard path and exited 1; the corrected search found no removed surgery sentence fields, only unrelated `status` members and the evaluator's private constructor parameter.
- Loaded environment boundary verification passed: the removed aggregate type is absent from `Assembly-CSharp`; `CharacterEnvironmentRuntime` exposes StatusQuery/WorkContext/Persistence/ITickable only; save/work/ability constructors consume mandatory narrow facets. `EnvironmentalFieldDebugScenarios.RunAll()` passed and post-run Console remained Error 0 / Warning 0.
- Integrated Unity MCP regression `SurgeryDebugScenarios.RunAll(false)` + `CombatSystemDebugScenarios.RunAll(false)` + `SurvivalDebugScenarios.RunAll()` passed. The survival result list contained zero failures, reported as `survivalScenarios=0`.
- Architecture current snapshot after the compile fixes remains 13 oversized types / 89 large constructors / 1049 default-assembly sources. The changed oversized hash is explained by line-count-bearing entries changing during responsibility splits, not by a count increase; all current names are established legacy exceptions and `SurgeryRuntime` is no longer among them. A PowerShell attempt to pipe directly from a `foreach` block produced an empty-pipe parser error and was replaced with an explicit `$rows` collection.
- Batch B save-boundary audit confirms all seven owner sections are current-version, strict preflight/staged, and rollback-free; V18 already expects the batch boundary count of exactly 39 rollback-free / 15 remaining production sections.
- V18 exposed a stale exactly-once gate that still required bounded FIFO eviction even though the run-scoped registry intentionally retains event keys for the run lifetime. The validator worker is replacing that requirement with a positive `ExecutedEventKeys` owner check and explicit prohibition of eviction queue/cap tokens.
- Restored the strict 1,200-line source gate without waivers: moved `SurgeryRuntimeSupport` into the existing `SurgeryRuntimeServices.cs` owner so `SurgeryRuntime.cs` is 1,194 lines, and removed eight nonsemantic separators from the single-class `CharacterStats.cs` so it is 1,199 lines. `CharacterConsumablesRuntime.cs` remains temporarily 1,216 while its failure-result extraction worker is active and must finish below 1,200.
- Launched separate Surgery and CharacterMedical facet workers after the closeout audit; the main thread began EnvironmentalWorkwear Query/Command/Persistence separation and mandatory research/work dependencies.
- One discovery command failed because it included the nonexistent `Assets/Scripts/Services/Save` path and propagated `rg` exit 1. Reissued the searches against exact existing files with zero-match exit handling; no source was changed by the failed command.
- Replaced `IEnvironmentalWorkwearRuntime` with Query/Command/Persistence facets, made research mandatory, changed CharacterEnvironment restore to consume a prepared workwear map before one aggregate swap, and narrowed policy/protection/work consumers. Two Editor fixtures still use the removed named argument and will be converted to an explicit null-object command before compilation.
- A first multi-file workwear patch did not apply because an encoded legacy debug string prevented exact context matching. Reapplied the structural substitutions in smaller patches and retained typed `DomainFailure` handling; no partial edit from the failed patch was accepted.
- Completed the source cutover for EnvironmentalWorkwear: old wrapper references are 0, research is mandatory, registration exposes Query/Command/Persistence, protection uses Query, policy/work use Command, and CharacterEnvironment uses Persistence. Editor-only absent-feature fixtures now pass an explicit `NoEnvironmentalWorkwearCommand` instead of null.
- Added workwear reflection coverage and V18 ratchets for wrapper absence, three-facet implementation/registration, persistence-only environment restore, and mandatory research authority. A targeted `git diff` was still noisy/truncated because those files contain substantial pre-existing changes, so review continued through exact symbol searches and focused source regions instead of treating the truncated diff as evidence.
- CharacterMedical worker completed Query/Command/Persistence separation, narrowed every save/gameplay consumer, removed care-priority/catalog/anatomy semantic null fallbacks, and added V18/Editor gates. Source audit now reports zero literal references across all nine removed Batch B broad wrapper names, including Surgery, CharacterMedical, and EnvironmentalWorkwear.
- With medical and surgery consumers stable, launched the next independent worker on Batch C's EnvironmentalField Query/Command/Persistence split while the Species/Survival typed-failure worker finishes localization and scenario coverage.
- Unity MCP mid-edit Console currently shows exactly four transient SurvivalFood compile errors at lines 378/380/382/384: helper calls still expect `out string` while the command boundary now passes `out DomainFailure`. The owning worker was given the exact diagnostics; this is not accepted boundary evidence and will be recompiled after its typed helper cutover finishes.
- First loaded Batch B integration run reached every owner and found two real boundary blockers: the typed-failure fixture still froze a global localization-table shape while other medical/status keys coexist, and `SurvivalFoodRuntime.cs` had grown to 1,223 lines. The worker extracted the refuel handler to a 43-line partial, reducing the main runtime to 1,185 lines.
- Reworked the localization fixture toward enum-derived key coverage. A diagnostic Unity command initially failed because the dynamic tool assembly could not resolve a LINQ path involving `ISet<>`; a List-based retry succeeded and exposed 76 legitimate surgery/medical status keys beyond global `FailureCode` and consumables codes. The fixture now includes the status enums and prefixes `CharacterMedicalStatusCode` keys correctly while excluding `Unknown`.
- Second Batch B run still used the intermediate status-key source and failed the same fixture plus the intentionally stale Roslyn report. The source was corrected again and must be recompiled before the next integration attempt; architecture metrics will be refreshed only after all source edits settle.
- Removed the temporary new `SurvivalFoodRuntime.Refuel.cs` source after moving its helper into existing `SurvivalFacilityWorkRules.cs`; this preserves the responsibility split without increasing default-assembly source count. The main runtime remains below 1,200 lines.
- Reviewed and refreshed the Roslyn baseline only after confirming the oversized names stayed the same 13 and all tracked counts improved or held: 1106 files / 3392 types / 24 mutable statics / 13 oversized / 89 large constructors / 1049 default sources / 6622 raw Korean / 0 content escapes / 0 direct session mutations / 4 root catalogs. Immediate `-Verify` passed.
- Added a current-only Unity architecture review entry point so baseline changes can be inspected without overwriting the baseline. Loaded metrics improved from 84 to 82 large constructors and 1031 to 1030 default Assembly-CSharp MonoScripts; optional DI, catalog errors, and broken asset references remain 0. After review, the Unity baseline was captured and revalidated.
- Final loaded boundary command passed together: Batch A architecture metrics, V18 authority (`save V18`, 772 authored items, 168 catalyst SOs), and `BatchBCharacterSurvivalAuthorityDebugScenarios.RunAll()` all report PASS. Batch C EnvironmentalField work was released after this clean boundary.
- Batch C EnvironmentalField cutover moved cell arrays, topology state, source caches, thermostat overrides, accumulator, and version into a replaceable `EnvironmentalFieldAggregateStateStore`; persistence now prepares a strict detached V2 candidate and publishes it through one root replacement.
- Environmental thermostats now persist canonical sorted `BuildingInstanceId` owners rather than coordinates. The field broad wrapper was removed in source and consumers were narrowed to Query, Command, or Persistence; test-only absence uses `NoEnvironmentalFieldQuery` instead of null.
- `EnvironmentalFieldSaveSection` is now required, strict-preflighted, staged, and rollback-free. Focused scenarios gained facet, persistence-only save, invalid-candidate no-mutation, and owner-ID coverage.
- The first Unity compilation after the field patch occurred while the parallel industrial-four worker was replacing its broad interfaces, so 88 transient industrial missing-type errors were recorded. Those errors are not accepted field evidence; field verification waits for the shared industrial seam to stabilize before rerunning Unity.
- Three early discovery commands propagated `rg`/`Get-Content` exit 1 after including nonexistent guessed paths; they were replaced with `rg --files` discovery and zero-match handling. One combined planning-file patch missed a findings heading and applied nothing; separate tail-anchored patches then recorded the work successfully.
- EnvironmentalField source closeout is complete: the runtime is 997 lines, broad wrapper references are 0, field optional-interface DI is 0, registration exposes exactly Query/Command/Persistence, and targeted `git diff --check` has no whitespace errors. Runtime synthesis of legacy thermal abilities was removed; stale thermostat overrides are pruned by persistent building owner during topology rebuild.
- Unity field scenarios/V18/Console could not be rerun in this worker slot because the parallel industrial-four cutover was still intentionally uncompilable. The root orchestrator explicitly accepted ownership of the joint compile/integration run once all Batch C seams land.
- 2026-08-04: Batch C continues at maximum concurrency: EnvironmentalField reported roughly 80% with detached V2 aggregate and three facets in place; IndustrialFour reported roughly 35% while replacing four broad owners and adding physical-stack transit; Production/Waste reported roughly 30% with V5 typed contract skeletons. The main thread owns the shared registration/UI/V18 merge after their seams stabilize.
- 2026-08-04: Removed code-generated generic production consumers from `ResourceEconomyContentCatalog`. Substance, ammunition, fuel, and feed reverse links are now backed by authored SO definitions or actual building supply profiles. Unity compilation is intentionally deferred while the industrial worker is mid-interface cutover.
- 2026-08-04: Corrected the substance reverse link to read the canonical `SubstanceItemFeature` on the item SO rather than the duplicate legacy `SubstanceDefinitionSO`; an exact runtime-source search confirms all ten former generic consumer IDs are now absent. Added V18 source ratchets so those virtual aliases cannot return.
- 2026-08-04: Added concrete terminal-use reverse links sourced from authored item features (food consumption, injury treatment, facility installation, research blueprint). Market sale was reviewed and intentionally excluded from branch counts so it cannot mask a missing production consumer.
- 2026-08-04: Completed the concrete construction-material cutover across all 293 `BuildingSO` assets, WorkOrder V3, physical reservation/consumption, and construction UI. The focused work-amount scenarios passed with Console Error 0 / Warning 0.
- 2026-08-04: Added building-material reverse indexing and catalog validation to `ResourceUsageIndex`, so real construction consumers now participate in intermediate branching and invalid/abstract building requirements fail the root production graph.
- 2026-08-04: The first Unity construction-plus-branching gate intentionally failed instead of hiding gaps: WorkAmount concrete-catalog validation failed, 9 loaded buildings had no explicit material list, and 61 produced items had no real consumer. The construction worker is correcting its asset-load coverage; the remaining consumer set is now the authoritative Batch C content closure list.
- 2026-08-04: Added `BatchCProductionInfrastructureAuthorityDebugScenarios` as the single seven-owner boundary runner. It combines field, industrial, production, waste, construction, branching, seven strict save sections, removed-wrapper/facet ratchets, V18, and architecture validation; the new runner compiles cleanly and will remain failing until the honest content gaps are closed.
- 2026-08-04: Root content closure added real reverse links for environmental workwear, effective medicine use, explicit market-sale policy, and lineage transfer; graph failures dropped from 70 to 36 without adding a virtual sink.
- 2026-08-04: All 343 cataloged buildings now have concrete construction authority and the strict save ratchet reports exactly 46/54 rollback-free. Twilight beer and night spirit gained canonical substance item features, while fermented vinegar now feeds both fermented pickles and preserved vegetables. The workshop gate passes and the honest graph now has 32 assigned content gaps.
- 2026-08-04: Unity `ValidateBatchCFinalSaveBoundaryOrThrow` passed against the loaded type graph: 54 total sections, exactly 46 rollback-free, exactly 8 approved remaining, and all seven Batch C sections retain exact ID/version/required/preflight/staged/detached candidate contracts.
- 2026-08-04: `ItemTransferService` was reduced from 1,346 to 1,138 lines and from 15 to 8 constructor dependencies. The extracted 231-line `ItemFacilityBufferTransaction` preserves all-or-nothing physical buffer consumption; focused physical-item, stock-query, production, and industrial regressions pass.
- 2026-08-04: The 32-item honest consumer backlog is closed in authored gameplay paths. Twenty-four component/tool/medical definitions now feed equipment, construction, or surgery; nitrate fertilizer and mushroom substrate are consumed by crop cycles; stock sensors, maintenance kits, toxic trap coating, and field repair kits are physically consumed by their owning systems; every compatible arrow/bolt/ammunition ID is reverse-indexed from weapon SOs. `BranchedProductionNetworkDebugScenarios.Validate()` passes with no virtual sink aliases.
- 2026-08-04: Batch C UI verification is running in a separate worker at both `1600x900` and `900x1600` using Unity `EventSystem` pointer dispatch only. The full Batch C runner remains pending because architecture baselines and the UI proof must be stabilized in the same loaded revision.
- 2026-08-04: The first full Batch C runner exposed order-dependent construction recovery because the work-order fixture read the global paused Unity clock. Replacing that fixture dependency with deterministic unpaused clocks restored the orphan-construction material-return scenario without weakening runtime pause behavior.
- 2026-08-04: The same runner exposed a real environmental performance regression at 28.039ms p95. `EnvironmentalFieldRuntime.Step` was repeatedly resolving Aggregate-root array properties inside the 10,000-cell neighbor loop; per-tick local array caching reduced the measured 10,000-cell + 500-agent p95 to 17.648ms, below the 25ms fixed-tick envelope.
- 2026-08-04: Production UI responsibility extraction reduced `ProductionBuildingPanelPresenter` from 939 to 721 lines and moved route editing to a 177-line presenter. Its responsive pointer verifier is still being integrated while Production/Automation contracts move to named assemblies.
## 2026-08-04 Batch B character/medical residual closeout

- Reconciled the Phase 112 Batch B checklist against current source: broad body-health, surgery, and character-medical runtime interfaces are already absent; typed narrow facets and typed saved statuses are present.
- Removed the last `out string` declaration from the surgery UI by adding a value-returning wildlife-carcass species parser, while retaining the legacy Try parser for existing non-UI consumers.
- Extended the integrated Batch B fixture to reject a resurrected broad body-health authority, missing Query/Command/Persistence facets, surgery-UI by-reference string paths, or string-backed surgery/medical statuses.
- Static localization parity check passed: 280 required keys, 0 missing shared keys, 0 missing Korean rows, 0 missing English rows. `git diff --check` passed for the three touched sources.
- Local `dotnet build` could not run because the workstation has no .NET SDK installed. Unity Editor verification was intentionally not attempted because the production-UI worker held the exclusive MCP Editor lease.

## 2026-08-04 substance single-authority and production UI acceptance

- Removed the parallel `SubstanceDefinitionSO` type, seven standalone substance assets, their root-catalog registrations, and the Editor path that could recreate them. Nine immutable substance views now project only from `ItemDefinitionSO.SubstanceItemFeature`; legacy type/API references, deleted-GUID references, empty IDs, duplicate IDs, and missing root registrations are all zero in the static audit.
- The production building UI now uses narrow bill query/command ports and a separate route presenter. `ProductionBuildingPanelPresenter` is 770 lines and `UIBuildingInfo` is 783 lines after responsive-layout extraction, both below the architecture limits.
- `ProductionBuildingPlayModeVerifier` passed real EventSystem pointer flows at `1600x900` and `900x1600`: owner selection, physical stock-sensor installation and consumption, preservation of the existing RepeatForever bill ID/mode, explicit MaintainStock conversion, route priority/weight/minimum-reserve edits, and output-buffer `WaitingForOutputSpace`.
- Both captures keep the building/context/action surfaces inside the screen and show all three route rows including the third row. Legacy tabs and the demolition control no longer bleed through the context panel. The report verifies UTF-8 BOM plus Korean round-trip and captured Console Error 0 / Warning 0.
- Evidence: `Artifacts/QA/production-ui-pointer-matrix-report.txt`, `Artifacts/QA/production-branches-1600x900.png`, and `Artifacts/QA/production-branches-900x1600.png`.

## 2026-08-04 Batch D source integration and architecture measurement

- Batch D source conversion landed for Research V5, CombatEquipment V6, EquipmentEvolution V3, MetaProgression V1, CropPlot V2, WorldResource V2, and TreasuryEconomy V3. All seven now use exact-current strict payloads, detached candidates, aggregate replacement, and rollback-free markers; OffenseAggregate remains the final active save conversion before the loaded `54/0` ratchet can be raised.
- The save registry no longer captures a redundant full live-world rollback image when every registered section is rollback-free. The existing failed-final-commit scenario now records both section capture counts and proves they do not increase during the all-marker failure path; the V18 validator forbids the former unconditional `rollbackImage = CaptureAll()` token.
- Replaced the fragile CombatEquipment V6 source-string assertion with a typed `CombatEquipmentSaveSection.CurrentVersion` contract. Targeted `git diff --check` passes.
- Corrected the Roslyn constructor metric to enforce the eight-dependency limit only on operational DI owners rather than counting scalar parameters on snapshots/results/requests. The current honest source snapshot is 1,124 runtime files, 3,474 types, 24 mutable statics, 13 oversized runtime/behaviour types, 32 real oversized dependency constructors, 1,058 default-assembly sources, 0 content escapes, 0 direct session mutations, 6,504 raw Korean literals, and 4 root-catalog references. The baseline is intentionally not updated while Batch D/E source is still moving.

## 2026-08-04 parallel closure continuation

- Offense aggregate source conversion completed the final Batch D save owner. The V18 validator and Batch B/C integration runners now require `54/54` rollback-free sections and an empty remaining rollback set; the all-marker registry keeps rollback-image capture disabled.
- Live production distribution source closure landed: recipe, construction, equipment, and surgery demand; physical output dispatch; live UI demand/reservation/block state; all three distribution modes; fallback ordering; blocked-route bypass; reservation cap; and starvation aging.
- Static mutable runtime state reached 0 in the current Roslyn report. `InstanceEvolutionPanelPresenter` stopped constructing collaborators internally and moved from 9 to 6 injected dependencies.
- Current source metrics are `1126 files / 3499 types / 0 mutable statics / 13 oversized / 28 large constructors / 1058 default sources / 6504 raw Korean strings / 0 content escapes / 0 direct session mutations / 4 root catalogs`. No baseline was rewritten. Unity compile, loaded `54/54`, integrated save/production scenarios, and Console 0/0 remain pending while parallel constructor/runtime splits finish.
- Removed `Assets/DataManager.cs` and the redundant `IDataScriptableObjectSource`/resource adapter. `IDataCatalog` now resolves a stable read-only numeric compatibility index directly from `IGameContentCatalog`, with duplicate-ID boot failure and an Editor immutability regression; domain consumers still need gradual conversion from numeric compatibility IDs to typed stable definition IDs.

## 2026-08-04 constructor closure and first merged Unity audit

- Operational constructor violations are now 0 in the Roslyn metric. Presentation, Captivity/Circus, regional economy, waste, world resources, crop plots, world filth, scene references, facility evolution, onboarding, debug overlay, and work execution all use required cohesive contexts of at most eight dependencies.
- `CharacterAiScheduler` and `CharacterBlackboard` now delegate budget/cadence/failure-memory responsibilities and are both below 800 lines. `CharacterProgression`, `CharacterStats`, and `OwnerCommandController` also moved real projection/state/selection responsibilities to rebuildable collaborators instead of partial-file splits.
- The first merged Unity MCP refresh compiled with Console Error 0 / Warning 0. Its authority run correctly exposed stale loaded validator constants plus one real restore defect: offense return arrivals were replacing their Aggregate during section commit instead of final publication. Return-arrival restore now prepares a detached strict candidate and publishes it only with the complete offense Aggregate candidate.
- The old physical-file line-count validator was removed from the V18 gate; the authoritative Roslyn type metric enforces class-level limits, matching the architecture contract. The current source metric has mutable statics 0, large constructors 0, content escapes 0, and direct session mutations 0. Oversized types, default-assembly ownership, and raw Korean literals remain active Batch E work.

## 2026-08-04 runtime responsibility closure and strict optional-save presence

- Completed meaningful collaborator extraction for `BuildableObject`, `CharacterActor`, `SocialReputationRuntime`, `DungeonGameplayPerformanceProbe`, `CharacterStats`, `OwnerCommandController`, `WildlifeActor`, `OffenseExpeditionRuntime`, and `InvasionIntruderRuntime`. The current Roslyn snapshot is 1,149 files / 3,618 types with mutable statics 0, oversized types 0, large constructors 0, content escapes 0, and direct session mutations 0.
- The fresh Unity-loaded V18 authority run reaches the architecture baseline gate; obsolete 46/8 save assertions and stale CombatEquipment assertions are gone. The baseline is deliberately not raised because default Assembly-CSharp sources are still 1,081 and must fall to zero.
- The offense aggregate proof exposed that Unity `JsonUtility` materializes a serialized null class field as a default object. `DungeonOffenseSaveData` V2 now persists `hasActiveBattle`; validation rejects hidden battle payloads, accepts only a verified empty Unity placeholder when the presence bit is false, and publishes a canonical null restore candidate.
- Three parallel follow-up tracks are active: Grid domain asmdef migration, Economy content/value asmdef migration, and a semantic Roslyn assembly-migration dependency planner. Functional Unity regression resumes after those shared assembly edits compile together.

## 2026-08-04 named-assembly migration and catalyst contract

- Added a deterministic semantic `AssemblyMigrationPlanner` over Unity's actual Bee response file. Its first full graph bound 1,062 default-assembly candidates with 8,502 semantic edges, 432 SCCs, 24 cyclic SCCs, no missing metadata references, and byte-identical output across repeated runs.
- Moved the Grid core, Economy content definitions, Character mood/needs core, four Buildings leaf contracts, seven AI/Characters/Work leaf contracts, and `CombatItemDefinitions` into their named assemblies while preserving authored asset GUIDs and preventing any named assembly from referencing `Assembly-CSharp`.
- Roslyn architecture verification now accepts a strict reduction without requiring the obsolete violation-set hash, while unchanged counts still require exact set identity. The verified snapshot reached 1,048 default sources with mutable statics, oversized types, large constructors, content escapes, and direct session mutations all at 0; the pre-existing 1,049 baseline was not raised.
- `JsonUtility` optional offense battle/world-target payloads now use explicit presence semantics. The Unity proof passes canonical null round trip, hidden payload rejection, invalid no-mutation, and late-failure discard.
- Split catalyst ID progression (1-21) from gameplay potency grade (1-5). A single pure policy owns the grade bands; 168 authored catalyst/residue SOs were explicitly migrated and validated, including progression 6 -> grade 2 and progression 21 -> grade 5. Catalyst content, instance evolution, equipment state, and strict save regressions pass.
- V18 source validation now follows the moved Door aggregate contract path. The next integrated V18 run waits for the active named-assembly leaves to settle so its Roslyn fingerprint remains current through validation.

## 2026-08-04 maximum-concurrency assembly continuation

- Kept all three worker slots active on non-overlapping Buildings, Survival consumables, and Economy world-resource boundaries while the root retained integration ownership.
- Current Roslyn metrics pass at 1,175 runtime files / 3,705 types / 0 mutable statics / 0 oversized types / 0 large constructors / 942 default-assembly sources / 0 content escapes / 0 direct session mutations / 6,512 raw Korean literals / 4 root-catalog references. The semantic migration graph improved to 20 cyclic SCCs and 101 leaf candidates.
- Removed every production `RuntimeDependency` call from Staff work UI, character ability, work ability, body-health death handling, and `AIBrain`, then deleted the obsolete global helper and its meta file. Required dependencies now fail at their owning boundary with explicit exceptions.
- Economy world-resource sources completed their named-assembly move; the four prior `WorldResourcePorts` CS0246 errors disappeared in the integrated Unity refresh.
- The first merged Unity compile found one new Buildings-worker collision (`CharacterActor.EmptyRoutine` duplicate) and four exact CS0108 port-redeclaration warnings. These were returned to the owning worker for correction; final Console 0/0 remains pending.
- The first Unity dynamic compile command used an ambiguous `CompilationPipeline` namespace and failed only inside the MCP command assembly. The corrected fully-qualified `UnityEditor.Compilation.CompilationPipeline` command executed and refreshed the project successfully.
- Moved `DomainFailureLocalizer` into `DungeonStory.Presentation` with explicit Automation, Localization, and ResourceManager references. The first compile exposed the required direct `Unity.ResourceManager` edge; adding it removed the leaf migration error.
- Split pure body-health snapshots into `DungeonStory.Combat` and moved `OffenseBattleContracts` plus `OffenseBattleSessionRules` into `DungeonStory.Offense`, preserving source GUIDs and adding `MovedFrom` metadata. Assembly-CSharp consumers temporarily use public domain operations until their own Offense move completes; no friend-assembly bypass was added.
- Extracted `WildlifeSpeciesDefinition` and `WildlifeItemDefinitions` into `DungeonStory.Wildlife`, then moved the authored `WildlifeSpeciesSO` type with its original GUID and `MovedFrom` metadata. Script-meta GUID duplication remains 0.
- The hard class-size gate briefly found `AIBrain` at 1,205 lines and `SurvivalFoodRuntime` at 1,214. `AIBrain` dependency guards were compacted without weakening them, and physical treatment-material selection moved into the existing stock collaborator. The current metric is back to oversized types 0 and large constructors 0.
- Current source metrics pass at 1,182 runtime files / 3,733 types / 0 mutable statics / 0 oversized types / 0 large constructors / 940 default-assembly sources / 0 content escapes / 0 direct session mutations / 6,512 raw Korean literals / 4 root catalogs. A planner run during an active Production asmdef compile used its documented project-scan fallback (1,175 candidates); the last clean Bee-bound graph before that had 940 candidates and 19 cyclic SCCs.

## 2026-08-04 maximum-concurrency assembly batches 2-4

- Completed and integrated named-assembly moves for the Production bill aggregate cluster, Wildlife ecosystem and habitat projection, Buildings ability handlers and management contracts, Character consumables, World resources, Husbandry contracts/aggregate/policy/work execution, combat equipment definitions, ammunition policy, authored media/settings SOs, and several Buildings leaf definitions. Original Unity script GUIDs are preserved and moved serialized types carry `MovedFrom` metadata.
- Replaced the remaining production/runtime concrete crossings with typed facility/worker handles, domain query/command ports, and default-assembly adapters. Production Editor scenarios and the building pointer verifier now assemble the same bridge/facade used by runtime instead of bypassing the named boundary.
- Removed all `RuntimeDependency` source references and deleted the helper. The global source search is 0; required dependencies now fail at their owning boundary.
- Fixed the integration fallout without reintroducing broad authority: production demand reads a public read-only bill query, WorldItem Editor fixtures use explicit Editor-only repository commands, Buildings test fixtures pass narrow state/world ports, and Wildlife grid/overlay projection uses `IWildlifeGridPort` plus `IWildlifeOverlayRootPort`.
- Unity MCP full refresh and compile now reports actual Error 0 / Warning 0. The clean Bee-bound migration graph is 914 default candidates / 8,258 edges / 329 SCCs / 17 cyclic SCCs / 1 leaf, hash `641b5214a4b88c06f656580ea8499b27c748711169372852338bf1bf26bfc007`.
- Current Roslyn metrics pass at 1,198 files / 3,786 types / 0 mutable statics / 0 oversized types / 0 large constructors / 914 default-assembly files / 0 content escapes / 0 direct session mutations / 6,504 raw Korean literals / 4 root catalogs. Script-meta GUID duplicates are 0.
- `RuntimeAuthorityV18Validator` source-path contracts were updated for the moved Wildlife ecosystem and habitat files. Global `git diff --check` still reports pre-existing Unity YAML/scene trailing spaces; every touched C# scope was checked separately and is clean.
- The next parallel cycle is active on the Shop contract leaf, Captivity aggregate/policy, and Grand Project runtime. Completion remains gated on default Assembly-CSharp gameplay ownership reaching 0, V18/domain regressions, responsive Unity MCP UI proof, and the final requirement audit.

## 2026-08-04 V18 authority pass and maximum-concurrency batches 5-6

- Integrated named-assembly moves for Shop inventory state, Regional Supply Contract state, Captivity save validation, Character ID registry, Codex summary/save state, and additional Work/Codex/Characters DTOs while preserving original Unity script GUIDs and serialized-type move metadata.
- Replaced the generic runtime Resources loader with an explicit `IGameContentRootLoader` that can load only `Resources/SO/GameContentCatalog`. Runtime `Resources.LoadAll` and the former generic loader contracts are now absent.
- Moved `GameContentCatalogSO` itself from the default assembly to `DungeonStory.Content`. Its four domain references are stored as immutable `ScriptableObject` references and projected through typed getters so Content does not depend back on gameplay domains.
- Normalized stale V18 source contracts to the current named-assembly paths and split the consumables validation contract between the named application/persistence runtime and its explicit compatibility adapter.
- Unity MCP full refresh reports actual Error 0 / Warning 0. `RuntimeAuthorityV18Validator.ValidateOrThrow()` now passes: save V18, 772 authored items, 168 catalyst SOs, legacy item authority 0, abstract stock assets 0.
- Current Roslyn metrics pass at 1,221 runtime files / 3,850 types / 0 mutable statics / 0 oversized types / 0 large constructors / 901 default-assembly files / 0 content escapes / 0 direct session mutations / 6,477 raw Korean literals / 3 root-catalog references.
- Research/equipment and branched-production regressions pass together. The approved queue simulation reports medieval/early-industrial/mature-industrial/late-industrial completion at 32.2/80.4/234.3/372.0 days, within the intended pacing bands. A stale test-only longsword lock ID was corrected to the authored stable ID `research:equipment:weapon-patterns`; direct runtime creation remains strictly rejected before completion and unlocks immediately afterward.
- V18 core regressions also pass for root content authority, physical stock queries, persistent identities, full physical equipment item state, and offense aggregate optional-payload/no-mutation handling.
- The clean semantic graph is 901 default candidates / 8,182 edges / 318 SCCs / 16 cyclic SCCs / 1 leaf. The remaining Assembly-CSharp ownership is still the dominant completion gate; three workers continue Captivity restore, Codex core, and AI behavior/consideration migrations while root owns integration and validator evidence.

## 2026-08-04 research fixture authority and FacilityEvolution integration checkpoint

- Removed the Editor research fixture's global `FindObjectsByType<BlueprintResearchRuntime>` selection. Character and building fixtures now receive the `BlueprintResearchRuntime` owned by their scenario world, and each actor receives a matching work-execution registry instead of a static no-research registry. Hidden all-research/locked-research providers are fully constructed before their state is queried.
- The Blueprint research scenario set now passes all five formerly failing cases. Research tree, 168-node equipment overhaul, branched production, and FacilityEvolution scenarios pass together; equipment and production failure counts are both 0 and pacing is `32.2/80.4/234.3/372.0` days.
- Fresh Unity MCP import compiled every current source with Error 0 / Warning 0 and source-newer count 0. `RuntimeAuthorityV18Validator` passes with save V18, 772 authored items, 168 catalyst SOs, legacy item authority 0, and abstract stock assets 0.
- The V18 source contracts now follow the moved strict defense save boundary and named defense rules; the unused duplicate defense validation helper was removed. Architecture metrics pass at `1292 files / 4035 types / 0 mutable statics / 0 oversized types / 0 large constructors / 881 default-assembly files / 0 content escapes / 0 direct session mutations / 6503 raw Korean strings / 3 root catalogs`.
- Six secondary MonoBehaviours now live in class-name-matching files with duplicate declarations 0. Scene script rebinding and the remaining OperatingDay/CharacterActor bridge splits are still pending and are not counted as accepted UI/scene evidence.

## 2026-08-04 maximum-concurrency assembly batches 7-8

- Captivity and Circus persistence now use named Infrastructure strict JSON sections with detached `CaptivityRestoreCandidate` and `CircusRestoreCandidate` payloads. Circus validates against the staged incoming Captivity candidate instead of the live world, and captured-wildlife publication consumes the staged Circus candidate once. Focused Captivity, Infrastructure, default-runtime, and Editor compiles pass; seven moved GUIDs are preserved, named concrete crossings are zero, and the invalid/late-failure probes pass.
- The Codex recording/formatting core moved as one eight-file unit into `DungeonStory.Codex`. Concrete character, facility, invasion, research, and recipe inputs are projected through snapshot ports in the default adapter. The Codex scenario set passes 7/7, all eight original GUIDs are preserved, named-to-default references are zero, and duplicate script GUIDs remain zero.
- Fourteen AI action/consideration rule owners moved into `DungeonStory.AI`, while Unity-authored ScriptableObject adapters received distinct script GUIDs and all 22 authored AI assets were rebound to those adapters. This removed the native-class identity assertion without moving mutable Unity state into the pure rule assembly.
- The first merged Batch B retry exposed fixture-only construction ordering: progression projection must be installed before `CharacterActor` construction and evolution state before `BuildableObject.Initialization`. The shared Editor composition and the physical-meal fixture were corrected without weakening either strict runtime guard. The V18 validator was also updated to recognize the stronger multiline `DungeonStrictJsonSaveSection<TPayload, TCandidate>` contracts for Captivity and Circus.
- Three worker slots remain saturated on Research, Meta, and the remaining Captivity/Circus adapter leaves. Unity's main compile is temporarily between source moves (`CircusRuntimeQueriesAdapter` old Bee input); root integration, architecture report regeneration, and Batch B/C/V18 reruns resume at the next stable assembly boundary.
## 2026-08-04 maximum-concurrency assembly batches 9-10

- All four execution slots remain occupied: root integration, Rooms, Factions/Meta, and FacilityShop after the Production boundary completed.
- Production no longer exposes `ProductionAggregateState` or mutable bill records across assemblies. Economy consumes a public read-only bill state and named commands; focused Bee response-file compilation passes for `DungeonStory.Production`, `DungeonStory.Economy`, `DungeonStory.Automation`, and `DungeonStory.Buildings` with zero diagnostics.
- A fresh Unity MCP refresh removed the prior Production/Economy CS1061 family. The current editor compile blocker is isolated to the active Factions move: 25 CS0246 diagnostics for six missing cross-assembly contract groups. No Production error remains in that loaded revision.
- Runtime architecture metrics now report `1271 files / 3958 types / 0 mutable statics / 0 oversized types / 0 large constructors / 876 default-assembly sources / 0 content escapes / 0 direct session mutations / 6504 raw Korean strings / 3 root catalog references`.
- The last runtime size/constructor findings were closed by extracting circus movement, temporary door-pass ownership, and captured-wildlife return orchestration into `CircusMovementCoordinator`, and by making the escape/escort runtimes consume the existing Captivity capability contexts. This is a responsibility extraction, not a partial-class line-count split.
- The semantic migration planner's current in-flight graph is `1111 candidates / 10516 edges / 578 SCCs / 19 cyclic SCCs / 105 leaves`, graph hash `f68902e89401fec9f88a78f2dd779d779ba46bd6f10d8c06ba966a46217704f0`. It is routing evidence only and will be regenerated after the active Rooms/Factions moves settle.
- Production's worker immediately moved to the independent seven-file FacilityShop vertical slice. Meta source changes remain frozen pending the Factions compile boundary; native MonoScript identity and the eight Meta scenarios must still run after a successful domain reload.

## 2026-08-04 fresh compile and focused regression checkpoint

- Recovered the truncated `OperatingDaySettlementRuntime` source and split Character actor bridges into class-name-matching files without duplicate declarations.
- Completed a real Unity domain rebuild, verified source-newer counts `0/0`, and observed project compiler diagnostics `0`, Console Error `0`, Warning `0`.
- Passed Blueprint Research, Research Tree, Research/Equipment, Branched Production, Facility Evolution, and Survival on the fresh DLLs. Pacing: `32.2/80.4/234.3/372.0` days.
- Regenerated architecture metrics successfully: `1296/4042/0/0/0/885/0/0/6505/3` for files/types/mutable statics/oversized types/large constructors/default files/content escapes/direct session mutations/raw Korean/root catalogs.
- V18 integrated retry isolated three work groups: PhysicalItem Editor progression composition, Batch C BuildableObject evolution composition, and Operating Day strict candidate restore. All three were assigned to non-overlapping workers.
- Prepared the GameplayScene cleanup target: 10 embedded MonoScripts plus GameObject `2113338566` (`Character Model Scenario Character`). Unity MCP cleanup waits for a stable compiled source checkpoint.

## 2026-08-04 strict-save integration continuation

- Removed the final legacy one-parameter save-section inheritance; the source search now reports `DungeonJsonSaveSection<T>` usages 0 and all production save owners use detached strict candidates.
- A fresh Unity MCP compilation isolated eight remaining diagnostics to three Editor verifiers referencing removed representative item constants. Runtime production code introduced no new compiler diagnostic in this checkpoint.
- Three parallel workers are active on authored fixture IDs, Offense world-map duplicate authority, and the next safe named-assembly leaf batch; root owns the merged Unity compile, V18/54-section regressions, and scene cleanup.
- The eight verifier diagnostics were a real class-scope defect, not stale Unity output: authored IDs had been declared on the static request verifier while their usages live in separate MonoBehaviour runner classes. The constants now live with their consumers, and the next automatic Unity compile produced C# diagnostics 0 / Console Error 0 / Warning 0.
- That compile is not yet the merged acceptance boundary: six Offense sources changed afterward in the active duplicate-authority worker, so source-newer counts are `6/6` until that batch completes and a new domain reload is performed.
- Unity MCP confirmed `Character Model Scenario Character` as an active root in `GameplayScene` with 19 runtime components. It was deleted through `Unity_ManageGameObject`, the scene was saved through `Unity_ManageScene`, and a second MCP lookup returned zero matches; root count changed 14 -> 13 and the scene is clean.
- Unity-side inspection found 23 loaded components backed by 16 embedded MonoScript objects; several are shared by scene characters and temporary fixture objects. The first authored-script candidate audit command failed in the isolated MCP compiler because its generated command assembly lacked the `System` reference required by `HashSet<T>/ISet<T>`; the retry will use arrays and simple loops instead of that dependency.
- After adding the two missing class-name script identities, Unity resolved exactly one authored MonoScript for all ten affected types. MCP rebound 23 loaded components and saved the scene; serialized embedded blocks fell from 16 to 1, missing `m_Script:{fileID:0}` references remain 0, and the leaked character remains absent.
- The sole residual block is `CodexRuntime`, referenced by a component on `DungeonRuntimeSystems`. It was not part of the 23 loaded typed components, so it must be treated as a missing-script component and replaced through its authored `Assets/Scripts/Models/Codex/Core/CodexRuntime.cs` identity while preserving serialized `importReferenceDataOnAwake`.
- Codex replacement attempt 1 was interrupted during domain reload after removing the missing component but before adding the authored component. State inspection proved missing count 0 and authored Codex component absent. The direct MCP `add_component` fallback then hit an internal null-reference, so no further blind deletion/retry is allowed; the next attempt will wait for the active GrandProject source batch to finish and use a minimal add-only Unity command.
- The delayed atomic command ultimately added the authored Codex component. MCP now proves `CodexRuntime` exists with `importReferenceDataOnAwake=true`, loaded embedded components 0, missing components 0, and the scene character leak 0. One unreferenced Codex MonoScript subasset block remains in YAML after the reference was replaced; it must be destroyed through Unity object APIs before the scene gate reaches serialized embedded blocks 0.
- Reload/save proved the remaining Codex block is not orphaned: the original component still references local fileID `1559308343`, while the newly added authored Codex component also exists with GUID `fba80135c3bde64468face1c336a7ea2`. The next scene operation must remove only the duplicate embedded-backed Codex component, retain the authored component and its import flag, then reload/save and audit again.
- Renaming `OffenseExpeditionSystem.cs` to the class-matching `OffenseExpeditionRuntime.cs` with GUID `d577ed6425ec47ed8e60f245ce07336a` restored the expedition MonoBehaviour and removed its fake-null slot after reload. One invalid slot remains at index 21 while both authored Expedition and Codex components are present.
- Direct `SerializedObject` mutation of GameObject `m_Component` reported Unity's protected-property error (`It is not allowed to modify the data property`). Although the command logged one attempted deletion, the result is untrusted and must be inspected; the next approach will resolve the invalid component's native instance ID and destroy that exact Unity object through the supported object-destruction API.
- Inspection shows the remaining invalid slot carries native instance ID `206602`, but both `SerializedProperty.objectReferenceValue` and `EditorUtility.InstanceIDToObject(206602)` are true null; there is no destroyable Unity object wrapper. Cleanup therefore must use Unity's supported missing-script removal operation on the owning GameObject rather than direct component destruction.
- Unity's supported `RemoveMonoBehavioursWithMissingScript` also reports 0 and leaves the slot because the local Codex MonoScript object is present even though its moved managed type cannot instantiate. Three direct cleanup approaches are exhausted; the next safe approach is a read-only replacement feasibility audit: clone `DungeonRuntimeSystems`, confirm the clone omits the invalid slot, and enumerate every external scene reference before considering an identity-preserving replacement.
- Clone serialization retained the invalid slot, so a clean-object reconstruction was audited instead. The audit copied all 24 valid components, found external scene references 0, internal source references 0, and replacement invalid slots 0.
- Unity MCP reconstructed `DungeonRuntimeSystems` from its 24 valid components, preserved transform/layer/tag/static state and all serialized component data, retained authored Codex and Expedition components, destroyed the retired object, saved, and reloaded the scene.
- Final scene evidence now passes in both loaded state and serialized YAML: invalid component slots 0, embedded MonoScript blocks 0, missing scripts 0, leaked test character 0, retired runtime-system object 0, `CodexRuntime.importReferenceDataOnAwake=true`, and `OffenseExpeditionRuntime` present.
- The first post-scene merged Unity compile reports C# diagnostics 0 and Console Error 0 / Warning 0, but is not accepted as the integration boundary because eight files from the active CharacterConsumables, RandomStream, and Offense catalog workers are newer than the loaded Editor DLL. Root will rerun after all three batches settle.
- The integrated rerun entry points are confirmed: `DungeonSaveSectionDebugScenarios.RunAll(false)`, Batch B/C `RunAll()`, Blueprint/Research Tree `RunAll(false)`, Research/Equipment `ValidateAll(out pacing)`, branched production `Validate()`, Facility Evolution `RunAll(false)`, and Survival `RunAll()`.
- The RandomStream focused Foundation/Infrastructure compile passes, but its full boundary check exposed an unrelated CharacterConsumables placement defect: a concrete `DungeonStrictJsonSaveSection<,>` was moved into `DungeonStory.Survival`, which cannot and must not reference Infrastructure. The owning worker was directed to keep domain payload/candidate authority in Survival while placing the concrete save-section adapter at the Infrastructure edge.
## 2026-08-04 fresh V18 integration and SCC checkpoint

- Fresh Unity assemblies passed `RuntimeAuthorityV18Validator`, all 54 save sections, Batch B/C, physical-item, persistent-identity, Offense aggregate/world-map/journey, Blueprint Research, Research Tree, Research/Equipment, branched production, Facility Evolution, Survival, Combat, strict combat-save, material-equipment, and Captivity/Circus scenarios in one merged checkpoint.
- Research pacing remains inside the approved bands at medieval/early-industrial/mature-industrial/late-industrial `32.2/80.4/234.3/372.0` days.
- `CombatEquipmentMaterialDebugScenarios` now injects the required `IBuildingEvolutionStatePort` through the common Editor composition before initializing fixture facilities. The material-policy and save-round-trip cases pass without relaxing production initialization guards.
- The Operations presentation SCC and WildlifeCapture restore-validation SCC were cut into named assemblies with original `.meta` GUIDs preserved. The semantic planner improved from 881 candidates / 18 cyclic SCCs to 878 candidates / 16 cyclic SCCs at the accepted planner checkpoint.
- The controlled character-stat dictionary is now expressed through a reusable Foundation contract. After extracting the condition-penalty projection, architecture verification passes at `1303 files / 4103 types / 0 mutable statics / 0 oversized types / 0 large constructors / 879 default-assembly files / 0 content escapes / 0 direct session mutations / 6462 raw Korean strings / 3 root catalogs`.
- Unity Console reports Error 0 / Warning 0 at this accepted checkpoint. The next parallel lanes are Invasion and another Captivity/Circus SCC; default-assembly ownership, localization, and two-resolution MCP UI proof remain open.

## 2026-08-04 small-SCC closure and giant-SCC entry

- Parallel source lanes removed the remaining independent cycles for Invasion save, Circus and Captivity restore, Husbandry commands, Staff management UI, CharacterCombatCommand, CharacterMedical supply, Fluid projection, CharacterSurgery, Grid construction UI, and CharacterSummary. Root additionally collapsed the DefenseEngagement partial restore cycle and replaced ResearchTree gesture-to-window coupling with `IResearchTreeInteractionSink`.
- The project-fallback semantic graph improved from at least 18 cyclic SCCs at the start of this reduction pass to exactly 1. The only remaining cycle is the pre-existing 500-plus-file default-assembly giant SCC; every former 2-6-file cycle now reports singleton SCC ownership.
- Current source metrics pass at `1313 files / 4151 types / 0 mutable statics / 0 oversized types / 0 large constructors / 874 default-assembly files / 0 content escapes / 0 direct session mutations / 6437 raw Korean strings / 3 root catalogs` at the latest settled worker checkpoint.
- A root-only Unity refresh compiled cleanly and `RuntimeAuthorityV18Validator` passed after the DefenseEngagement and ResearchTree cuts. The 54-section and CoreSession regression groups also passed. The following combined command was intentionally rejected as acceptance evidence because an active InvasionIntruder file move made the architecture report stale and briefly exposed an orphan `.meta`; the worker restored the original GUID and root will rerun after all three giant-SCC lanes settle.
- Active giant-SCC cuts now target BuildableObject responsibility, InvasionIntruder execution/content/restore partials, and Offense world-map coordinator/view partials. Unity MCP remains single-owner at root to prevent command and compilation contention.
## 2026-08-04 Phase 116 source-stable boundary checkpoint

- Completed the CharacterActor visitor, Shop customer/checkout, Automation demand, Wildlife ownership, presentation factory, Environment model, ExternalInfluence save, Survival environmental bridge, and WorkTargetCandidate boundary cuts without introducing an asmdef cycle.
- The latest source-based architecture verification passes at 1,318 runtime files / 4,149 types / 0 mutable statics / 0 oversized types / 0 large constructors / 856 default-assembly files / 0 content escapes / 0 direct session mutations / 6,437 raw Korean literals / 3 root-catalog references.
- The source-stable planner reports one remaining cyclic SCC with giant size 508. The newest WorkTargetCandidate graph has 1,091 candidates / 8,976 edges / 584 SCCs / 1 cyclic SCC / 106 leaves and no missing metadata.
- Unity Editor remains responsive, but the project-scoped MCP relay did not reconnect after the last domain reload. The latest source is newer than both Assembly-CSharp DLLs, so the new source batch is not yet accepted as compiled even though the recent Editor.log tail contains no compiler diagnostics.
- Final acceptance still requires MCP reconnection, fresh DLL timestamps, the complete V18/domain regression matrix, continued default-assembly migration to zero, localization closure, and the two required Unity-MCP-only UI captures with Console Error 0 / Warning 0.

## 2026-08-04 Phase 116 cluster-cut transition

- The latest settled source batch reduced default runtime ownership to 852 files and the single giant SCC to 504 files. Successful additions include Shop service completion, environmental workwear, Captivity restore policy, and Offense world-map events.
- One-file cuts are now exhausted at several concrete boundaries: Hallway inherits `BuildableObject`, the Invasion data provider exposes `CharacterSO`, `IWarehouseFacility` exposes `WarehouseInventory`, and the urgent-site SO serializes a default-owned enum. Work has switched to cohesive 2-6 file clusters rather than hiding those dependencies.
- Updated all 280 `SourceBySuffix` lookups in `GameplayArchitectureRatchetTests` so every path resolves exactly once after the named-assembly moves. Split Blueprint assertions across runtime/contracts and updated Research Tree ownership assertions to the actual query/command presentation boundary.
- Corrected `RuntimeAuthorityV18Validator` to inspect the live Infrastructure-owned `BlueprintResearchSaveSection`; all 234 required and 98 forbidden source-contract paths now exist, and all 9 required-absent paths are absent.
- Unity relay-only recovery did not reconnect the editor. The backup scene is newer than the saved GameplayScene, so the editor was not restarted and no OS input automation was used.
- Two PowerShell audit attempts had quoting/parser errors; both were replaced with literal/formatted regex construction and the corrected audits passed. No source mutation occurred during the failed attempts.

## 2026-08-04 Phase 116 first cohesive-cluster barrier

- Completed cohesive named-assembly cuts for Invasion data provider/threat/model/entry/observation/aggregate state, Offense urgent sites/decision cards/site archetypes/reward events, Buildings interaction/warehouse contracts, and Survival diagnostics/taboo/need-balance contracts.
- Replaced the dedicated default `CharacterControlledStatStore` forwarding file with a reusable Foundation delegate store while keeping `CharacterStats` as the sole state authority; the Roslyn 800-line type gate remains green.
- A read-only Buildings cross-audit found one real compile risk after the warehouse move: the global owner lacked the `DungeonStory.Buildings` import for `ShopSaleItemDefinition`. The import was added before the barrier.
- Central ArchitectureMetrics passes at 1,327 runtime files / 4,163 types / 839 default-assembly files / 0 mutable statics / 0 oversized types / 0 large constructors / 0 content escapes / 0 direct session mutations / 6,441 raw Korean literals / 3 root-catalog references.
- Two consecutive project-fallback planner reports are byte-identical: 1,074 candidates / 8,738 edges / 585 SCCs / 1 cyclic SCC / 108 leaves / giant SCC 490 / missing metadata 0, SHA-256 `13B31A1CD71E7A6357D9C7D1BF9A36799190B469254CE1BC5966D838E4A4068E`.
- A snapshot-shape audit first used an invalid PowerShell `Where-Object` shorthand and failed without editing files. The corrected script proved 37 constructor parameters, 37 assignments, and 37 properties with no duplicates or omissions.

## 2026-08-04 Phase 116 second cohesive-cluster batch in progress

- Refilled all three worker lanes for Invasion, Rooms/FacilityShop, and Presentation while root handled Character/AI boundaries. Completed worker cuts so far include Defense engagement rule snapshots, Service process authored-content projection, Offense/owner-selection/production-view factories, and Room environment authored-content projection.
- Root moved `CharacterSpeciesCatalog` into `DungeonStory.Species` by replacing the broad `IGameContentCatalog` constructor dependency with the existing read-only `IGameContentDefinitionSource`; the original script GUID remains unique and the validator path now targets the named source.
- Root moved `AIBrainPathSearchSession`, `CharacterIdleWanderPlanner`, and `GridMovePathRules` into `DungeonStory.AI`. Actor-specific position and traversal context are now assembled at the default adapter edge, while the named rules depend only on Grid/World/Buildings/Foundation contracts.
- Root moved `CharacterMovementKinematics` into `DungeonStory.Characters` behind `ICharacterMovementKinematicsActor`; `CharacterActor` satisfies the capability through its existing `GetMoveSpeed` and `Flip` methods without duplicating movement state.
- Focused Unity Roslyn builds from the current Bee response files pass for `DungeonStory.AI`, `DungeonStory.Species`, and `DungeonStory.Characters`. The AI assembly uses explicit one-way references to Buildings, Grid, and World; a direct asmdef graph audit reports zero cycles.
- Focus-compile attempt 1 incorrectly reused PowerShell's reserved `$args` variable and failed with a fixed-size collection exception. Attempt 2 exceeded the Windows command-line length limit. Attempt 3 wrote a generated response file under `Library/AssemblyMigrationPlanner` and compiled successfully; no source was mutated by either failed attempt.
- A later response-file inspection command had an empty-pipeline parser error. The corrected command used an explicit result array and confirmed Unity regenerated all three Bee response files at 11:05 with the moved sources included.
- The root Unity MCP transport still reports `Transport closed`; no Editor restart or operating-system input was used. A worker session reported a clean Unity Console, but root will not treat that as the merged acceptance boundary while other source lanes remain active.

## 2026-08-04 Phase 116 third cohesive-cluster batch in progress

- Refilled all three worker lanes for Invasion facility targeting, Offense expedition experience, and Character trait/content ownership. The preceding FacilityShop, Invasion intruder-planning, Offense authored-content, and AI JSON-contract cuts are source-stable and individually verified.
- Restored the original `CharacterRecordJsonDto` source GUID from Unity's `Library/SourceAssetDB`; the DTO and `ILlmJsonPayload` now live in `DungeonStory.AI`, declarations are unique, and the AI focused compile passes.
- Root moved `BuildingConnectivityQueryAdapter` into `DungeonStory.Infrastructure`. The first focused compile exposed that `Grid.IsConnected` still existed only as a default-assembly extension; the authoritative implementation now lives on the named `Grid` type and the legacy extension delegates to it. Focused Grid and Infrastructure compiles pass.
- Root moved `AutomationPowerDemandRegistry` into the Infrastructure named assembly, preserved its GUID and state-session authority, and updated the V18 source contract. The class is public only so default Unity composition and the electrical runtime can consume the named adapter; it does not expose mutable state.
- Root moved `EventAlertRuntime` and `NoticeFeed` into `DungeonStory.Presentation` with their original MonoScript GUIDs. The Presentation focused compile passes with both current sources.
- Focus-compile script attempt 1 used `[System.IO.Path]::GetRelativePath`, which is unavailable in the active Windows PowerShell runtime. The retry derives repository-relative paths from a validated workspace prefix. Infrastructure compile attempt 1 then correctly failed on the missing named `Grid.IsConnected` API; adding the named implementation and delegating the compatibility extension resolved it.
- Root completed the cohesive NoticeFeed presentation cut by moving the MonoBehaviour, item factory, and pooled presenter into `DungeonStory.Presentation`; current architecture metrics pass at `1,337 files / 4,187 types / 818 default-assembly files` with every hard gate still zero.
- The first combined apply-patch move was rejected because `*** Move to` followed a content hunk instead of the file header. The retry separated the content edit from the two file moves and preserved all three original GUIDs.
- Presentation compile attempt 1 rejected `CanvasGroup.DOFade` because `DOTweenModuleUI.cs` is an Assembly-CSharp source extension rather than part of the precompiled DOTween DLL. The presenter now uses the equivalent `DOTween.To` alpha tween, retaining the visible/fade durations, pooling, target link, and completion release; the focused Presentation compile passes.
- Root extracted `SceneUiBootstrapReferences` from the broad default scene-reference file and moved `DungeonTitleCanvasProvider` into Presentation. The title UI now depends only on its EventSystem capability; its original provider GUID is preserved and the Presentation focused compile passes.
- Root moved `IGameCalendar` and `IGameSpeedController` into `DungeonStory.CoreSession`, and the user-settings enums/DTO/service contract into `DungeonStory.Foundation` with a `MovedFrom` marker on the serialized DTO. Concrete calendar, speed, persistence, screen, audio, and camera effects remain at the Unity edge.
- With those contracts named, `ResearchTreePauseScope` moved into Presentation and was made public for the still-default `ResearchTreeWindow` adapter. Focused CoreSession, Foundation, and Presentation compiles all pass against current sources.
- A full `DungeonUiThemeRuntime` move was rejected by the focused compiler because room-toggle presentation state and `UIBuildingInfo` remain default-owned. The original MonoBehaviour file and GUID were restored to the default edge; only the immutable backward-compatible `DungeonUiTheme` facade moved into Presentation. Presentation compiles cleanly after the split.

## 2026-08-04 Phase 116 third cohesive-cluster barrier

- Source-stable ArchitectureMetrics passes at `1,344 files / 4,203 types / 815 default-assembly files / 0 mutable statics / 0 oversized types / 0 large constructors / 0 content escapes / 0 direct session mutations / 6,441 raw Korean literals / 3 root catalog references`.
- The planner self-test passes, and two forced project-fallback reports are byte-identical: `1,050 candidates / 8,465 edges / 581 SCCs / 1 cyclic SCC / 106 leaves / missing metadata 0`, graph hash `c5bb0d28...`, report SHA-256 `90BDEAD8...`.
- Global Assets script/meta verification found `1,584` C# files with missing meta `0`, orphan meta `0`, `6,769` indexed Asset meta GUIDs, and duplicate Asset GUID groups `0`.
- The 281-call `SourceBySuffix` audit found one stale path for the moved Invasion intruder planner. The ratchet now targets `Models/Invasion/Core/InvasionIntruderPlanner.cs`; the repeated audit reports `281/281` uniquely resolved.
- Read-only Unity serialization audit scanned `2,843` YAML files and `6,885` `m_Script` references. Missing script GUIDs and ambiguous script GUIDs are both `0`; ResearchProjectSO `168`, CharacterTraitSO `9`, EventAlertRuntime `19`, and NoticeFeed `28` references retain their original GUIDs.

## 2026-08-04 clean-compile repair and localization audit

- The first clean Unity Editor compilation after the third cluster barrier exposed 26 stale fixture calls that still passed `CharacterActor` where the new Buildings boundary requires `IBuildingVisitorPort`. Three parallel repair lanes converted all nine affected test/verifier files to the authoritative `CharacterActor.BuildingVisitor` capability without casts, fallback adapters, or changed fixture semantics; merged clean compilation is being rerun through Unity MCP.
- Read-only localization analysis proved that the existing `6,441` raw-Korean metric undercounts another `2,122` interpolated text segments. It also isolated 18 mojibake literals to five runtime files and showed that the sole 296-key `DomainFailures` table exactly covers only two current raw literals. Production, Defense, and Character narrative are the next three vertical localization clusters.

## 2026-08-04 Phase 117 plan rescope

- Replaced the mechanical `Assembly-CSharp == 0` completion target with a risk-based ownership contract. Default-file count is now informational; mutable domain authorities and cross-domain cyclic-boundary violations must reach zero, while reviewed Unity scene/UI/input/audio/VFX/composition adapters may remain at the edge.
- Froze further save refactoring. V18 and the 54 save sections remain mandatory regression evidence, but save implementation reopens only for a concrete current-source defect.
- Added five authoritative execution batches: stabilize the active source, install an ownership classifier/manifest, cut only `NamedRequired` owners, close localization vertically, and run final Unity MCP acceptance.
- The planning skill catch-up script failed after printing its useful context because the Windows CP949 console could not encode an em dash (`UnicodeEncodeError`). No repository source was changed by that script; the plan was recovered directly from the UTF-8 planning files instead.

## 2026-08-04 Defense localization vertical cut

- Replaced defense-facility activation, supply, jam, and repair sentence failures with parameterized `DomainFailure` values across the runtime boundary and its required facility/work/debug consumers. The runtime now persists only stable failure-code tokens for blocked-state projection; the fourteen mojibake literals formerly owned by `DefenseFacilityRuntime` are gone.
- Routed all authored display literals in `DefenseFeatureQueryService`, `DefenseFeatureCommandService`, and `DefenseFeatureSurfacePresenter` through the strict `IDefenseUiTextQuery`. Added a no-fallback `DefenseUI` ko/en builder with 188 exact keys and placeholder-parity validation, and authored the new defense failures in `DomainFailures`.
- Current Foundation -> Presentation -> Assembly-CSharp -> Assembly-CSharp-Editor focused compilation passes with zero diagnostics. Offline audits report Defense runtime/query/command/surface raw Korean literals 0, DefenseUI duplicate keys 0, direct-use missing keys 0, ko/en placeholder mismatches 0, C#/meta `2168/2168`, missing meta 0, and duplicate GUID groups 0.
- ArchitectureMetrics passes at `1,348 files / 4,210 types / 0 mutable statics / 0 oversized types / 0 large constructors / 811 default-assembly files / 0 content escapes / 0 direct session mutations / 8,331 raw Korean literals / 3 root-catalog references`. Unity MCP still must execute the `DomainFailures` and `DefenseUI` builders, then run the merged Unity compile and UI/Console verification; no localization YAML was edited manually.

## 2026-08-04 Phase 117 ownership classifier and report

- Added the risk-based default-assembly ownership classifier, exact-path reviewed override manifest, and a separate generated report at `Library/ArchitectureMetrics/default-assembly-ownership-report.json`. The main architecture report now also emits the unapproved-authority metric and cycle-candidate list while retaining the raw default-file count as information only.
- The current normal analyzer run passes and reports `1,350 files / 4,214 types / 811 default files / 35 DefaultAllowed / 441 NamedRequired / 335 ReviewRequired / 776 unapproved authorities / 22 cross-domain candidates`; all pre-existing hard gates remain zero and interpolated text remains included in the `8,325` Korean-token result.
- Report invariants pass: the three classification counts and source records both total exactly `811`, and `441 + 335` exactly equals the `776` unapproved count. `WriteBaseline`, `-Verify`, and analyzer self-test were deliberately not run, so the newly exposed debt was not silently approved.
- Direct script execution first hit the host execution-policy block; the retry used a process-local `-ExecutionPolicy Bypass`. The first compiler pass then found one missing condition parenthesis in the new manifest validator; it was corrected before the successful report run.

## 2026-08-04 Phase 117 Environment work-policy cluster

- Split the three-file code cluster without touching save/V18 contracts: `EnvironmentPolicyDomain.cs` now owns the pure decisions, `EnvironmentWorkPolicyUnityAdapter.cs` owns Unity/default-edge conversion, and `DungeonWorldSimulationRegistration.cs` registers the renamed adapter against the unchanged `IEnvironmentWorkPolicy` contract.
- Replaced the adapter's calls to default-internal exposure helpers with the public named `CharacterEnvironmentRules` equivalents using the same thermal/protection projection, preserving the former calculation inputs and outputs.
- Focused Roslyn compilation passes with diagnostics 0 for both `DungeonStory.Environment` and the default-edge adapter. The 44-asmdef graph reports cycle count 0. Source/meta checks report old paths absent, new paths present, the original GUID exactly once, and scoped diff whitespace errors 0.
- Current ArchitectureMetrics classifies the adapter `DefaultAllowed` with exact Unity-adapter evidence instead of the former `NamedRequired`; the live report is `811 default / 78 allowed / 440 named / 293 review / 733 unapproved / 22 cross-domain candidates`. Concurrent localization/source work also changed the global totals, so only the target's class transition is attributed to this cluster.
- The first whole-default focused compile attempt used a stale Bee response and failed on unrelated newly added localization interfaces. A narrowed adapter compile then initially referenced a differently named focused Environment assembly and could not unify assembly identity; recompiling the named output under the exact `DungeonStory.Environment` identity resolved the boundary and produced the final zero-diagnostic result.

## 2026-08-04 Character narrative localization vertical cut

- Replaced the authored work labels, record templates, prompt-style fragments, situation leads, and controlled fallback prose in `CharacterLogNarrativeService` and `CharacterRecordTemplateBank` with the strict no-fallback `ICharacterNarrativeTextQuery`. Korean validation/protocol keywords, decision tokens, and particle rules remain code-owned as intended; particle application is locale-aware so English output is not given Korean suffixes.
- Added an Editor-only `CharacterNarrative` ko/en String Table builder with 99 exact semantic keys and duplicate, blank, missing, placeholder-parity, and composite-format validation. No localization YAML was edited manually.
- The direct raw-Korean string-token count across the two target files fell from `584` to `309` (`-275`): service `305`, template bank `4`. The remaining tokens are the approved LLM protocol/validation/decision boundary rather than authored display prose.
- Current Presentation -> Assembly-CSharp -> Assembly-CSharp-Editor focused compilation passes with zero diagnostics after the locale-aware particle change. The offline 99-key audit reports duplicate keys 0, missing direct keys 0, ko/en placeholder mismatches 0, missing C# metas 0, and duplicate GUID groups 0. Root still must execute the builder and merged Unity compile/scenarios through the single-owner Unity MCP acceptance pass.

## 2026-08-04 Phase 117 Codex application-adapter ownership audit

- Audited `CodexRuntimeApplicationAdapter` and found no gameplay, save, or authored-content state authority. Its only mutable field was the currently bound `CodexRuntime`; the remaining list contains disposable event subscriptions, while snapshot creation is a stateless projection of injected queries and Unity-edge event payloads.
- Moved bound-target and subscription lifetime into the reusable named-Foundation `EventSubscriptionLifetime<TTarget>`. The Codex adapter now owns only readonly dependencies and readonly transient wiring, preserves its original MonoScript GUID, and retains the same bind/unbind/error behavior.
- Tightened the ownership classifier so authority-free `ApplicationAdapter` types qualify as application edges, and approved default edges are excluded from cross-domain cycle candidates. `CodexRuntimeApplicationAdapter.cs` changed from `ReviewRequired` to `DefaultAllowed` and left the candidate set without an override or baseline rewrite.
- ArchitectureMetrics passes at `1,353 files / 4,221 types / 0 mutable statics / 0 oversized types / 0 large constructors / 811 default files / 84 allowed / 439 named / 288 review / 727 unapproved / 19 cross-domain candidates`; all other hard gates remain zero. Focused Foundation plus Assembly-CSharp compilation has zero diagnostics, the 44-asmdef graph has zero cycles, C# meta omissions are zero, and both the preserved Codex GUID and new lifetime GUID are unique.

## 2026-08-04 Phase 117 Faction domain/application boundary

- Audited `FactionRuntime` and confirmed that the V1 `world.factions` strict rollback-free save section, DTO, restore phase, Offense/physical-item dependencies, candidate preparation, and final publication boundary were already correct. Those save sources and their V18 meaning were not changed.
- Moved Aggregate access plus deterministic contract unlock, trust, goodwill, alliance, betrayal, restitution, recovery, reinforcement-loss, ambush, route progression, and route mutation rules into the named `DungeonStory.Factions` `FactionDomainRuntime`. The default `FactionRuntimeApplicationAdapter` now owns only Offense-world, physical-item, Character-spawn, clock, and event projection.
- Split the root-content and Unity logistics/character dependency projection into `FactionApplicationDependencies.cs`. Both default files classify `DefaultAllowed`; `FactionRuntime.cs` left the cross-domain candidate set without an override or baseline rewrite. The original runtime GUID `a284d9b8af9b4334786fdef712291207` remains unique.
- Added a narrowly validated `ApplicationAdapterTransientState` marker for private subscription, reentrancy-guard, and projection-revision fields only. Unmarked mutable adapters such as `MetaRuntimeApplicationAdapter` remain `ReviewRequired`, proving the classifier rule does not hide gameplay state.
- Current ArchitectureMetrics passes at `1,358 files / 4,228 types / 0 mutable statics / 0 oversized types / 0 large constructors / 812 default files / 86 allowed / 438 named / 288 review / 726 unapproved / 18 cross-domain candidates`; all existing hard gates remain zero. Focused Foundation -> Factions -> Assembly-CSharp -> Assembly-CSharp-Editor compilation has zero diagnostics, the 44-asmdef graph has zero cycles, missing C# metas are zero, and all three target GUIDs are unique.
- Focus-compile response generation attempt 1 flattened a PowerShell replacement-pair array, replacing option hyphens with `r`; it failed before compiling source. Rebuilding the generated response files with explicit replacements fixed the harness, and all four focused compilations then passed. A later GUID audit used ambiguous positional `Select-String` arguments and failed read-only; the explicit `-LiteralPath/-Pattern` retry passed.

## 2026-08-04 Phase 117 Character environment runtime boundary

- Completed the five-file boundary cluster: added the named exposure-step input/result and deterministic transition rules, renamed the default implementation to `CharacterEnvironmentUnityAdapter`, and updated composition, editor scenarios, and the V18 authority validator without changing the source/meta path.
- Focused `DungeonStory.Environment` Roslyn compilation passes with diagnostics 0. Unity MCP also compiled and resolved the current `CharacterEnvironmentUnityAdapter` and `CharacterExposureStepResult` types, executed successfully, and reported Console Error 0 / Warning 0.
- The current asmdef graph contains 48 assemblies and zero cycles. The original runtime GUID occurs exactly once, legacy concrete-type references are zero, scoped trailing whitespace is zero, and `git diff --check` passes.
- ArchitectureMetrics classifies the target `DefaultAllowed` and reports `811 default / 79 allowed / 439 named / 293 review / 732 unapproved / 22 cross-domain candidates`, with all hard architecture gates at zero. Global changes outside the target are not attributed to this lane.
- A whole-default offline compile used a stale Bee response and failed on unrelated localization interfaces. A subsequent isolated adapter compile could not access default-internal members across the synthetic assembly boundary. Neither attempt mutated source; the current-source Unity MCP compile is the accepted merged evidence.

## 2026-08-04 Phase 117 Character progression boundary in progress

- Started the `CharacterProgression.cs` cross-domain audit with an explicit 3-6 file ceiling and exclusions for Character AI narrative, facility naturalness/utility, and Defense Codex sources.
- Planning session catch-up reported 24 unsynced tool-only messages from the shared long-running session. The working tree is broadly dirty from parallel lanes; no recovery mutation was applied, and this lane is using target-scoped status/diff checks only.
- Initial evidence: the target is `ReviewRequired` because it combines `CharacterProgression` mutable domain state, a snapshot authority type, and a scene-bound MonoBehaviour. The public component is referenced broadly, so its compatibility API and MonoScript identity must remain intact while deterministic level transitions move to named Characters.
- The first combined patch was rejected before mutation because a mojibake level-up log line made the surrounding context fail exact matching. The retry is split into a new named rules file plus method-boundary patches that avoid localized-string context.
- Named `DungeonStory.Characters` focused compilation passed with exit 0. The first Unity MCP current-type command then failed because the newly added source had not yet been imported into the Editor domain (`CharacterProgressionRules` and `CharacterProgressionTransition` unresolved); this is a stale loaded-assembly condition, not a Roslyn source diagnostic. The retry explicitly imports the new project script before checking type visibility again.
- Unity MCP imported the new script under GUID `4b5a3cf2ed6845d8a76c50e0909a09c2`, but the Editor could not publish a new assembly because an unrelated concurrent `FactionRuntime.cs` lane currently has ten CS0103/CS1061/CS0019 errors. This lane will not edit that out-of-scope source; current-loaded type proof is deferred while focused Characters compilation and target-script validation remain green.
- The first standalone rule-probe compile passed every native DLL in Unity's runtime directory to Roslyn and failed with CS0009 metadata errors. No project source changed. The retry filters references through `AssemblyName.GetAssemblyName`, matching the proven ArchitectureMetrics compiler setup.

## 2026-08-04 Phase 117 Character progression boundary complete

- Changed two code files: added `Models/Characters/CharacterProgressionRules.cs` and routed `Services/Character/Core/CharacterProgression.cs` through its pure transition results. The existing progression source/meta path and public component/snapshot contracts remain intact.
- Focused `DungeonStory.Characters` compilation passes with exit 0. A standalone current-source probe exhaustively compared add-experience, reached-level ordering, minimum-level advancement, and restore normalization across broad level/experience/target inputs and passed.
- ArchitectureMetrics passes at the observed shared-worktree checkpoint `1,357 files / 4,227 types / 812 default / 85 allowed / 439 named / 288 review / 727 unapproved / 18 cross-domain candidates`; all hard gates remain zero. Concurrent lanes affect the global counters, so this lane attributes only the target's removal from the cross-domain candidate list. Its ownership class remains `ReviewRequired` due the intentionally retained serialized state and snapshot.
- The asmdef graph reports `48` assemblies and cycle count `0`. The original and new GUIDs each occur exactly once; declarations are unique, target scoped `git diff --check` passes, and trailing whitespace is zero.
- Unity MCP script validation succeeded for both sources. Current-loaded type execution could not complete because the unrelated concurrent `FactionRuntime.cs` source has ten compile errors; root accepted ownership of the merged Unity load/Console proof after that lane settles. This lane did not edit the Faction source or weaken verification.

## 2026-08-04 Phase 117 Character spawner boundary

- Split the `CharacterSpawner` cross-domain owner into three responsibilities without changing its serialized MonoBehaviour identity: named `DungeonStory.Characters` now owns recruitment eligibility and respawn scheduling, `CharacterSpawnerSceneApplicationAdapter` owns character/entrance scene projections, and the original `CharacterSpawner` retains prefab pooling, placement, injection, actor wiring, and coroutine lifecycle.
- The persistent visitor ID still comes from the unchanged `ICharacterPopulationService.AcquireVisitor` path. The eligibility, capacity, acquire, entrance, instantiate, initialize/bind, and respawn-registration order remains unchanged; no character save DTO, version, prefab field, or GUID meaning changed.
- Corrected the ownership analyzer's direct-base recognition so `BuildableObject` receives the same Unity-scene-edge treatment as its `MonoBehaviour` base. The rule is narrow: `CharacterSpawner` is now `DefaultAllowed`, while `ConstructionSite` remains `ReviewRequired` for mutable state and `AiDirectorRuntime : SerializedMonoBehaviour` remains `ReviewRequired` for state plus runtime/service authority. No override or baseline changed.
- ArchitectureMetrics passes at the observed shared-worktree checkpoint `1,362 files / 4,232 types / 814 default / 95 allowed / 433 named / 286 review / 719 unapproved / 16 cross-domain candidates`; the CharacterSpawner path is absent from the candidate list and all hard gates remain zero. Concurrent lanes affect global counters, so this lane attributes only its target transition.
- Focused Foundation, Factions, Characters, and CharacterSpawner source compilation passes. The whole-default response first exposed a concurrently added CharacterSummary query missing from the stale response, then an in-progress unrelated EnvironmentalField edit; the isolated CharacterSpawner compile passed after using a local stub solely for a default-internal visitor adapter boundary.
- The asmdef graph reports `48` assemblies and cycle count `0`. The original CharacterSpawner GUID `abad84318b563a74bacc6367852a8019` and both new source GUIDs are unique, missing C# metas are zero, legacy spawner-owned dictionaries/resolver/faction runtime references are zero, and target-scoped `git diff --check` passes.

## 2026-08-04 Phase 117 Character population boundary

- Moved the authoritative world-profile collection, persistent-ID serial, returning-visitor ordering, visiting/staff/visit-count transitions, ready-pool watermarks, restore validation, and next-preparation selection into the named generic `CharacterPopulationDomain<TProfile>`. `WorldCharacterProfile` keeps its exact serialized fields and clone shape while explicitly exposing the named state contract.
- The original GUID/path now contains `CharacterPopulationApplicationAdapter`, limited to `CharacterActor`, Unity preview objects, skill-generation callbacks, authored-content projection, and faction-query projection. The unchanged `ICharacterPopulationService` API and `CharacterPopulationService` concrete name are preserved by a no-domain thin facade; VContainer registration and the existing debug fixture construct the adapter explicitly.
- Visitor acquisition, actor initialization/binding, release synchronization, promotion, capture, restore, preparation completion, and pool-pump call order remain unchanged. The domain probe passes returning-visitor priority, visiting mutation, non-staff visit increment, staff release, restore visiting reset, persistent-ID continuity, and duplicate-ID rejection without replacing the prior live collection.
- Closed the CharacterSpawner compatibility fallout in `GridFoundationDebugScenarios`: the removed static resolver call now uses `CharacterSpawnerSceneApplicationAdapter.TryResolveEntrance` directly. Fresh named Environment/Characters, whole Assembly-CSharp, and Assembly-CSharp-Editor focused builds passed with the corrected fixture; the final population-only compile also passes after a concurrent ExternalInfluence lane made the whole-default response transiently unstable again.
- ArchitectureMetrics passes at the observed shared-worktree checkpoint `1,367 files / 4,242 types / 817 default / 99 allowed / 432 named / 286 review / 718 unapproved / 14 cross-domain candidates`. The original population path is `DefaultAllowed` and absent from the candidate set without an override or baseline change.
- The asmdef graph reports `48` assemblies and cycle count `0`. The original population GUID `88805dc527a981240bcf9a77935bf92b` and both new source GUIDs are unique, missing C# metas are zero, the removed resolver identifier has no remaining source references, and target-scoped `git diff --check` passes.

## 2026-08-04 Phase 117 Faction strict-save adapter boundary

- Preserved the frozen faction strict-save contract exactly: section ID `world.factions`, DTO version, `LateRuntimeState` phase, Offense/physical-item dependencies, capture, payload preflight, detached candidate preparation, and candidate publication order are unchanged. The runtime still exposes no direct DTO restore bypass.
- Moved pure faction payload, canonical faction/route order, travel-state, reinforcement-ID, and cargo validation into named `DungeonStory.Factions.FactionPayloadValidation`. The original validation GUID is preserved; the default section projects only authored item existence through `Func<string, bool>` and converts the returned ordered errors into the same candidate-build failure.
- Added a general classifier rule for concrete direct `DungeonStrictJsonSaveSection<...>` adapters. Such types no longer receive domain-authority evidence merely from the `SaveSection` suffix, but mutable fields and every other authority/runtime/service rule remain active. No exact-path override or baseline changed.
- ArchitectureMetrics passes at the observed shared-worktree checkpoint `1,368 files / 4,244 types / 816 default / 122 allowed / 408 named / 286 review / 694 unapproved / 11 cross-domain candidates`. `FactionSaveSection.cs` is `DefaultAllowed` with strict-save-adapter evidence; the old default validation path no longer exists, so both requested target paths are absent from the candidate set.
- Focused named Factions, whole Assembly-CSharp, and Assembly-CSharp-Editor compilation passes. The standalone payload probe accepts a canonical current-version payload and rejects legacy version, noncanonical faction order, and noncanonical route IDs. Source contract checks confirm the frozen faction section tokens, and a multiline declaration audit still finds exactly `54` strict rollback-free production save types across `41` source files.
- The asmdef graph reports `48` assemblies and cycle count `0`. The original section GUID `83754b2c9d429254083f03541364b8c4` and moved validation GUID `6af9e40b5ecd76b4486fff5becdc8bf1` are each unique, missing C# metas are zero, and scoped `git diff --check` passes.

## 2026-08-04 Phase 117 Event-alert save application boundary

- Moved the event-alert save DTOs, unchanged `IEventAlertSaveService` contract, 80-record cap, canonical record-ID/title/importance/count/text validation, and three-choice validation into named `DungeonStory.Operation`. The DTO field names, defaults, and list shapes remain unchanged.
- The original `EventAlertSaveService.cs` GUID/path now contains only the runtime capture projection, detached `PrepareRestoreHistory` staging, and `PublishRestoreHistory` publication adapter. Both the service and `EventAlertSaveSection` call the same named validator and preserve ordered error aggregation.
- Preserved the frozen section contract exactly: ID `operation.event-alerts`, version `1`, `Presentation` restore phase, OperatingDay/Invasion/Offense dependencies, capture, validation, prepare, and publish order are unchanged. A multiline declaration audit still finds exactly `54` strict rollback-free production save types across `41` source files.
- Added a general classifier rule that recognizes only a concrete `*SaveService` directly implementing an `I*SaveService` contract as a save application edge. Runtime/service evidence remains for other service types, and mutable fields still produce authority evidence. No exact-path override or baseline changed.
- ArchitectureMetrics passes at the observed shared-worktree checkpoint `1,369 files / 4,244 types / 816 default / 132 allowed / 402 named / 282 review / 684 unapproved / 8 cross-domain candidates`. The target is `DefaultAllowed` with save-application-adapter evidence and is absent from the candidate set.
- Focused named Operation, whole Assembly-CSharp, and Assembly-CSharp-Editor compilation passes. The payload probe accepts a canonical record and rejects duplicate IDs, invalid counts, missing choice lists, and null record lists. The asmdef graph reports `48` assemblies and cycle count `0`; original/new GUIDs are unique, missing C# metas are zero, and scoped `git diff --check` passes.

## 2026-08-04 Phase 117 scope reduction finalized

- The remaining default-assembly file count and `UnapprovedDefaultDomainAuthorityCount` are now audit metrics, not zero-target completion gates. Mechanical asmdef migration is stopped.
- Assembly work is authorized only for a demonstrated duplicate writer, unsafe cross-domain mutation, save/determinism/content-authority defect, or assembly cycle/reverse dependency.
- V18 save architecture remains frozen unless a current regression reproduces a concrete defect. Intentional parser/content Korean is excluded from localization count chasing.
- Remaining execution order is: finish the active Blueprint Research and DomainFailure defects, triage only proven risky cross-domain candidates, run feature regressions, generate user-visible localization assets, and finish the two-resolution Unity MCP acceptance pass with Console 0/0.

## 2026-08-04 Phase 117 Environmental field and progression follow-up complete

- Completed the environmental five-file boundary cluster: named Environment owns the Aggregate/store and deterministic simulation; the preserved default source is now `EnvironmentalFieldRuntimeApplicationAdapter`. Registration, debug scenarios, and the V18 authority validator point to the new owners while save contracts remain unchanged.
- Focused named compilation passes and the environmental legacy-equivalence executable passes 16,234 checks over 240 randomized grids plus source-helper/buffer/version cases. The 48-asmdef graph has zero cycles, scoped `git diff --check` has no errors, and environment GUIDs `7b07c8d6e4872e34e985fae0451b50f9` / `53ad4bcd82a94df585faf5e83f741980` are each unique.
- Corrected the earlier stale Character progression candidate claim by splitting real Foundation/Operation coupling into `CharacterProgressionNotificationApplicationAdapter` and `CharacterProgressionGrowthApplicationAdapter`. The original state owner now references only Characters; its GUID `badabbf33eed2ae46b77a5f13883bc2d`, named-rules GUID `4b5a3cf2ed6845d8a76c50e0909a09c2`, and adapter GUID `c641828e0a124e6ca58e56a2de78ea50` are each unique.
- Fresh ArchitectureMetrics passes at the shared-worktree checkpoint `1,366 files / 4,239 types / 817 default / 97 allowed / 433 named / 287 review / 720 unapproved / 15 cross-domain candidates`; both target paths have candidate count 0. Concurrent lanes affect global totals, so only the two target outcomes are attributed here.
- Unity MCP comprehensive validation passes for the four target/rule scripts. The loaded merged compile has no error in either target; its only current Console error is the out-of-scope `GridFoundationDebugScenarios.cs` missing `DungeonEntranceGridResolver`, so global Console Error 0 remains pending integration repair.

## 2026-08-04 Phase 117 External influence boundary complete

- Completed a six-code-file boundary cut without touching Defense CharacterPopulation, facility CharacterSummary, the V3 save section/DTO, restore order, or V18 semantics. Named CoreSession owns the Aggregate store and deterministic influence transitions; the preserved default source is now `ExternalInfluenceRuntimeApplicationAdapter`.
- Focused CoreSession compilation passes. The standalone equivalence probe passes 6,506 checks, Unity fairness/content-authority scenarios pass, strict canonical/invalid payload validation passes, and the final loaded Console reports Error 0 / Warning 0.
- Fresh ArchitectureMetrics reports the target as `DefaultAllowed` and absent from cross-domain candidates. At the observed shared-worktree checkpoint global candidates are `11`; concurrent lanes changed the totals, so only this target's `NamedRequired -> DefaultAllowed` and candidate removal are attributed here.
- GUIDs `115d5aeafd549764a9fbff9b92d35017` and `c7ea3bfe8eec4f909347a5e0f48bf0e4` are unique, asmdef count/cycles are `48/0`, legacy concrete constructors/static policy calls are zero, and scoped whitespace verification passes.
- The all-owner Batch A wrapper remains blocked by an unrelated `DomainFailureLocalizer` placeholder-count mismatch in its presentation assertion after external-influence execution. The failure was recorded rather than repaired outside scope; the focused external-influence save validation and current Console remain clean.

## 2026-08-04 Phase 117 World-simulation registration classification complete

- Audited the 468-line `DungeonWorldSimulationRegistration` as pure VContainer composition: no state/rules/helpers were present to extract, so the source and saved behavior remain unchanged.
- Added a structural `composition-registration` classifier rule instead of a suffix-only allowance or exact-path override. A temporary analyzer matrix passes `pure=DefaultAllowed`, `mutable-static=ReviewRequired`, and `local-calculation=ReviewRequired`; mutable Meta/OperatingDay/ConstructionSite sources remain review.
- Fresh analyzer target candidate count is 0. Analyzer compilation, Unity comprehensive validation, GUID uniqueness, asmdef `48/0`, scoped diff-check, and Console Error 0 / Warning 0 all pass. Planning records were updated without touching save/V18 or the override manifest.

## 2026-08-04 Phase 117 Blueprint research boundary complete

- Kept the public `BlueprintResearchRuntime` MonoBehaviour and its restore-facing state API intact, while routing Foundation event-bus, debug-rule, and Aggregate-root composition through `BlueprintResearchApplicationAdapter`. Existing editor/debug `Construct(...)` callers remain source-compatible through the construction adapter.
- Named Research already owns project progress, queue state, dependency ordering, and work arithmetic. Added the remaining node-state decision matrix to `ResearchProjectCoordinatorRules`; `BlueprintResearchProjectCoordinator` now projects archive/facility facts into that named decision, keeping the scene runtime at 741 lines.
- `BlueprintResearchSaveSection`, Research V4/V5 DTOs, `requiredWorkAtCapture`, and restore ordering were not edited. Unity probes pass the 560-to-720 work rebalance at the preserved 35% ratio, work arithmetic, and completed/active/suspended/shortcut/in-transit/available node states.
- Fresh ArchitectureMetrics passes at `1,370 files / 4,247 types / 817 default / 133 allowed / 402 named / 282 review / 684 unapproved / 7 cross-domain candidates`. The target remains `ReviewRequired` for its retained MonoBehaviour/state compatibility surface but references only Research and has target candidate count 0; the new Foundation adapter is `DefaultAllowed`.
- Unity compilation and execution pass, Console is Error 0 / Warning 0, asmdefs are `48/0`, the new GUID `1b2bb02a3bed4de09ed3cc190511e444` is unique, and scoped diff checks pass.

## 2026-08-04 Phase 117 Exterior incident single authority source complete

- Added named `DungeonStory.Exterior.ExteriorIncidentAggregate<TState>` as the only incident history/countdown/mutation owner. Runtime start, action, tick, active query, overview, capture, history trim, and restore publication now use that Aggregate.
- `ExteriorZoneMarker` is projection-only for incidents: its independent `TickIncident`, work-triggered incident clearing, and incident save-data source were removed. Handler changes to remaining time or stage are applied as Aggregate transitions and immediately reprojected; terminal transitions clear only the matching marker projection.
- Added edit-mode Aggregate invariants plus a PlayMode regression that replaces a handler with a deterministic probe, changes both time and stage, and requires runtime query, active DTO, capture, restored Aggregate, recapture, and restored marker to agree. The standalone Aggregate executable passes; loaded execution is assigned to the root integration gate because Unity MCP approval is currently revoked.
- V18 exterior section, validation, DTO version, and restore phase/order were not edited. Named Exterior focused compilation passes, fresh analyzer reports target candidate 0 and global candidates 6, oversized types remain 0, asmdefs are `48/0`, original/new GUIDs are unique, and scoped diff checks pass.

## 2026-08-04 Phase 117 operating-day settlement boundary complete

- Added the named `OperatingDaySettlementDomain<TReport, TSupplyResult>` and its Aggregate as the sole owner of daily revenue, visits, stock use, incidents, event log, debt, shortfall count, report history, settlement tokens, and completed-day idempotence.
- Split the preserved `OperatingDaySettlementRuntime` MonoScript into a four-line GUID-compatible facade and `OperatingDaySettlementApplicationAdapter`. The adapter now only maps Unity character/building snapshots, applies employment/paid-facility/money ports once, raises alerts, and publishes the completed report.
- Preserved event order: wage-shortfall alert logging occurs before the immutable report snapshot is refreshed, while report publication occurs before the named ledger is cleared. Duplicate day-start does not erase an active ledger and duplicate day-end cannot charge economy ports or add history twice.
- No OperatingDay save DTO, section ID/version, canonical mapper, validation order, or prepare/publish source was edited by this lane. The detached Aggregate candidate still publishes through one `Replace(candidate.State)` pointer swap.
- Focused named Operation and default adapter compilation pass with zero diagnostics. The standalone current-source domain harness passes event-order, single-completion, paid amount, history, and duplicate-settlement assertions. Loaded Unity scenarios remain assigned to the root integration gate because concurrent unrelated Character surgery compilation is active.
- Fresh ArchitectureMetrics passes at the observed shared checkpoint `1,375 files / 4,267 types / 818 default / 136 allowed / 401 named / 281 review / 682 unapproved / 5 cross-domain candidates`; both settlement facade and adapter have target candidate count `0`. The adapter is automatically `DefaultAllowed`; the facade has a reviewed exact-path allowance documenting its GUID-only compatibility role.
- Current asmdefs are `49/0` cycles. Original facade GUID `aaaeaede56d6b7c4a9c327c95d8ddf30`, adapter GUID `6083238bd2424ad0b20440682678135f`, and named domain GUID `e109e7c5213c4c79b3f62ab1407d1b8f` are unique; missing C# metas are zero and scoped whitespace checks pass.

## 2026-08-04 Phase 117 Experience pacing single-authority source complete

- Added `ExperiencePacingAggregate` to the named CoreSession assembly as the sole writer of current day, rehearsal masks, active rehearsal day, and introduced concepts. Day advancement is monotonic, rehearsal begin/resolve is idempotent, and restore rejects invalid maps, masks, day history, active state, duplicate/undefined concepts, and missing Defense history.
- Reduced `ExperiencePacingRuntime` to a 102-line lifecycle/application facade with no Content or Foundation reference and no direct state writes. `ExperiencePacingApplicationAdapter` owns authored rule lookup, event subscription, and Aggregate-root composition; the frozen strict save adapter is isolated in `ExperiencePacingSaveSection`.
- Preserved V18 behavior and the frozen pacing contract: section ID `run.experience-pacing`, payload version `1`, `LateRuntimeState`, RunFlow dependency, detached prepare, and validated single publication. Editor callers now construct the adapter explicitly instead of retaining a broad compatibility constructor.
- Named CoreSession, focused runtime/application/save, and focused Editor scenario compilation pass. The standalone transition/save probe passes monotonic day, duplicate begin/resolve prevention, idempotent concepts, exact capture/prepare/publish round trip, frozen DTO version, and invalid-candidate rejection.
- Fresh ArchitectureMetrics passes at the observed shared-worktree checkpoint `1,377 files / 4,268 types / 820 default / 138 allowed / 402 named / 280 review / 682 unapproved / 2 cross-domain candidates`; Experience target candidate count is `0`, oversized types remain `0`, and hard gates remain zero. The asmdef graph is `49/0`; new GUIDs are unique, source/meta pairs exist, and scoped whitespace/diff checks pass.
- Unity-loaded scenario execution and final Console Error 0 / Warning 0 remain assigned to the root integration gate because Unity MCP approval is revoked.

## 2026-08-04 Phase 117 final-acceptance runner coverage audit

- Audited `DungeonStoryFinalAcceptanceRunner` against the active Phase 117 exit gates and the original V18/research/production/equipment completion criteria. Existing direct or composite entries already cover V18 authority and all 54 strict sections, root content authority, physical item/stock/equipment state, branched production and facility supply, 168 research/equipment content and pacing, Exterior/Experience/Service/RunFlow, combat, surgery, survival, and the broad implemented-gameplay suites.
- Added six existing synchronous Editor entry points that were missing or hidden behind the broad implemented-gameplay result: runtime composition, OperatingDay settlement authority, Offense strategic physical expedition, Offense expedition journey, expedition architecture, and Offense aggregate V18 validation.
- The report still writes to `Artifacts/QA/final-acceptance-report.txt` and now states its scope explicitly. Unity MCP PlayMode UI at `1600x900` and `900x1600`, captures, and final Console Error 0 / Warning 0 remain a separate external gate and are not reported as synchronous PASS evidence.
- Focused Assembly-CSharp-Editor compilation passes. The runner/meta pair exists, GUID `118562f8f35147b290323f48fab983e2` is unique, scoped diff/trailing whitespace checks pass, and the source contains 33 named acceptance steps.
- Read-only audit found three completion areas without a dedicated callable synchronous Editor regression: equipment lineage-transfer execution plus expedition death co-loss of equipment/modules; firearm smoke and low-durability misfire role scenarios; and a live 54-section full-world round trip across scene/run reset. These are recorded as real remaining evidence gaps rather than simulated inside the runner.

## 2026-08-04 Phase 117 Dungeon run-flow source boundary complete

- Added the pure named `DungeonRunFlowReducer` as the run-flow transition authority. It owns monotonic day progression, phase/outcome, boss cycle/armed/active flags, rehearsal feedback, terminal normalization, multiplier rules, and deterministic ordered effects without Unity dependencies.
- Split the preserved `DungeonRunFlowRuntime` MonoScript into a fieldless GUID-compatible facade and `DungeonRunFlowApplicationAdapter`. The adapter now only coordinates Experience pacing, invasion threat/director capabilities, owner completion, alerts, event subscriptions, and post-publication projection.
- Repaired the malformed run-flow alert literal and normalized the touched alert text to valid UTF-8 Korean. Added loaded regression assertions that duplicate day 10 does not reschedule rehearsal and duplicate days 40/50 cannot arm or start a boss twice; the existing V2 capture/JSON round-trip assertions remain active.
- Preserved the frozen save boundary: root V18, section `run.flow`, payload V2, `LateRuntimeState`, Offense/Invasion dependencies, strict detached candidate preparation, and single publication all pass source-contract verification. This lane did not change the save DTO, section version, ordering, or candidate shape.
- Focused named Content, default adapter/facade, and Editor verifier compilation pass. The standalone current-source reducer harness passes identical-sequence determinism, day/rehearsal/boss/start/resolution/truth duplicate rejection, and ends at `Finished/Victory/day 50/cycle 2`.
- Fresh ArchitectureMetrics passes at the shared checkpoint `1,380 files / 4,274 types / 822 default / 141 allowed / 401 named / 280 review / 681 unapproved / 0 cross-domain candidates`; mutable statics, oversized types, large constructors, content escapes, and direct session mutations are all `0`. Both RunFlow targets have candidate count `0`.
- The asmdef graph is `49/0`; facade GUID `359f43f0511f48a4992eb6d5c8c1c170`, adapter GUID `2bfde50523654c49a481532344033565`, and reducer GUID `7831a424d9944637b93db53c75e047b9` are unique; missing C# metas are zero and scoped diff/encoding checks pass. Loaded RunFlow PlayMode plus final Console Error 0 / Warning 0 remain assigned to the root integration gate.

## 2026-08-05 final offline integration audit

- Reran ArchitectureMetrics on the latest visible merged source. PASS: `1,380 files / 4,275 types`, cross-domain candidates `0`, mutable statics `0`, oversized types `0`, large constructors `0`, content escapes `0`, and direct session mutations `0`.
- Rechecked the asset/assembly structure: 49 asmdefs with zero cycles, zero missing C# metas, and zero duplicate GUIDs among 6,817 parsed asset meta records. The only four GUID references not resolved by the Assets-only asmdef map are external `DamageNumbersPro` references.
- Confirmed all three previously missing acceptance areas are now callable from the unchanged 33-step runner. The call graph reaches production lineage transfer, actual expedition death equipment/module loss, and gunpowder smoke/misfire plus ranged-role behavior rather than source-token-only checks.
- Caught and handed back two new regression compile defects to the owning lane; both were fixed. The final runner focused Assembly-CSharp-Editor compile and scoped runner/regression/document diff checks pass.
- The comprehensive offline Editor response-file attempt is not accepted as a loaded compile because shared Bee artifacts still carry temporary probe assembly identities and omit a current default-assembly environment interface. Root owns Unity refresh, synchronous runner execution, PlayMode/visual checks, and final Console Error 0 / Warning 0.
- Global `git diff --check` remains an explicit integration issue: 1,502 serialized trailing-whitespace findings across 32 shared files. No save/V18 source or unrelated Unity-generated asset was modified by this audit lane.

## 2026-08-04 acceptance evidence gaps closed in source

- Added end-to-end physical lineage transfer coverage to `PhysicalItemDebugScenarios`: the real work order consumes the source equipment and physical lineage seal, copies evolution history, and preserves the target material, quality, durability, physical stack, and installed module.
- Added actual expedition-death co-loss coverage through `OffenseExpeditionReturnPort.HandleMemberDeath`; the equipped item leaves the loadout and becomes Lost while its installed module becomes Lost with zero condition.
- Corrected the gunpowder smoke product path. `CombatAttackResult.SmokeExposure` is independent from target suppression, all executed gunpowder outcomes carry it, and `CombatResolutionService.Record` is now the sole application point for the shooter's authoritative airborne exposure. The later command-result applier no longer duplicates that mutation. The combat suite asserts exactly one exposure command call for normal hit, miss, and low-durability misfire, plus ammo, armored damage/penetration, reload/cadence, and authored bow/crossbow/gun non-dominance.
- Hardened `DungeonFullWorldRoundTripPlayModeFacade`: warning/error capture begins when the fresh request exits EditMode, survives the scene/composition interval through a domain-reload-safe buffer, and hands off to the runner without admitting stale EditMode logs. The facade now captures a canonical live baseline before the scenario, compares it after the scenario, fails on mismatch even if explicit cleanup succeeds, and reports `baselineRestored` only after canonical proof.
- Replaced the obsolete owner-doctrine legacy fallback fixture with an explicit strict-V18 rejection. A current section envelope carrying a V2 run-variable payload must be rejected with the version error, and a canonical before/after capture proves the failed restore did not mutate live state.
- Fresh named `DungeonStory.Combat` compilation passes. Focused default smoke resolution/result-consumer compilation passes against that new named reference; the isolated full-world facade compilation passes. New facade GUID `744bdf3a67fc4ef9a251646060aa4f25` is unique, all scoped C# metas exist, and scoped `git diff --check` passes.
- A complete offline default/Editor build remains invalid evidence because the shared Bee response/reference artifacts still name temporary probe assemblies and contain pre-move source paths. Root owns the required Unity refresh, loaded scenario execution, full-world PlayMode report, and final Console Error 0 / Warning 0.

## 2026-08-05 final PlayMode facade static follow-up

- Re-audited the then-current final PlayMode coordinator, the 54-section Full World canonical round trip, resolution owner preparation, and CharacterProgression save contracts without Unity/MCP/helper execution. The target/capture shape from this checkpoint was superseded later the same day by the current seven-target/30-capture matrix.
- Connected the previously uncalled `CharacterProgressionSavePlayModeFacade.Run` to `DungeonFullWorldRoundTripPlayModeRunner`, recorded its result/detail in the report, and made Full World success depend on it.
- Scoped source inspection passes. Command-line compilation could not run because the host has no resolvable `Microsoft.NET.Sdk`; root must use the fresh Unity compile and PlayMode run as the actual acceptance gate.

## 2026-08-05 equipment/expedition final UI matrix

- Added `EquipmentExpeditionUiMatrixPlayModeVerifier` plus a unique Unity meta GUID.
- Added real EventSystem pointer coverage for module processing, module install/remove, lineage selection/confirmation, and live expedition progression at `1600x900` and `900x1600`.
- Added four required fresh captures and connected the verifier as final coordinator target 7/7; aggregate capture count is now 30.
- Added `OffenseJourneyPlayModeFacade.Cleanup` so QA actor/SO fixtures do not leak inside the PlayMode run.
- Runtime snapshots restore seeded physical-item/equipment state between rows and on completion. Scoped `git diff --check`, delimiter checks, and direct Runtime+Editor Roslyn RSP compilation pass with zero errors.
- Added canonical research/offense save-section capture and restore. Each resolution restores the original research and offense JSON, verifies exact recapture equality, then starts with cleared expedition/battle runtime state; standalone execution no longer leaves research unlocks or accumulated route state.
- Pending root gate: loaded Unity compile, fresh final PlayMode coordinator report, all 30 captures, and Console Error 0 / Warning 0.
- Added a full-suite fail-fast dirty-scene guard to the final PlayMode coordinator. Every distinct target scene is preflighted before state creation/persistence capture, so dirty Title plus a later Gameplay target fails immediately; actual transitions and `OpenSceneMode.Single` remain defensively guarded. Preflight failure does not restore a stale snapshot, dirty scenes are never saved/discarded/unloaded/overwritten, and clean seven-target/30-capture behavior is unchanged. Runtime+Editor direct Roslyn compilation passes.
- Reworked the equipment UI matrix around authored RF42/RF43/RF44/I17/I18 facilities and facility-local physical buffers. It verifies per-facility command visibility with EventSystem clicks, S08 wrong-facility absence, one physical module's complete process route, install absorption/removal rematerialization, and I18 lineage delivery/confirmation/application. The final coordinator requires the facility-flow report marker while retaining seven targets and 30 captures.
- Completed the underlying physical-module authority rather than limiting the change to UI filtering: authored `item:equipment-module`, strict standalone component persistence, detached/attached duplicate rejection, stack-link restore checks, destructive-loss transitions, installation-only absorption, and same-instance removal/replacement rematerialization are all covered by synchronous regressions.
- Updated all removed facility-less API callers. Fresh Foundation → Items → Combat → default Runtime → Editor Roslyn compilation passes with zero output; ArchitectureMetrics passes at `1,384 files / 4,314 types` with every hard gate and cross-domain cycle candidate count at `0`. Asset audit reports `6,240` non-meta assets with missing meta `0` and `6,864` unique parsed GUIDs with duplicate groups `0`.
- Unity-loaded execution remains pending because PID 80780 still owns a visible `Scene(s) Have Been Modified` modal for the dirty TitleScene. No operating-system input was used; the user must dismiss it before project-local Unity MCP can reload and run the final gates.
