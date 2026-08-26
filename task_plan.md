# DungeonStory Active Plan

## 2026-08-16 50 WU 전역 밸런스 재조정 인벤토리 (in progress)

- [x] 현재 3-seed 5일 실측으로 `50 actual WU/성인·일`, `45 output-equivalent WU/성인·일` 기준을 확정한다.
- [x] 전역 밸런스 기준서를 끝까지 읽고 모든 조정 축과 완료 계약을 추출한다.
- [x] 실제 코드·ScriptableObject·카탈로그를 대조해 정의 단위와 데이터 권위 위치를 목록화한다.
- [x] 음식 생산량, 설치 자원, WU, 시간, 공간, 유틸리티, 물류, 위험, 가격, 보상, AI 수요를 포함하는 전수 재조정 체크리스트 문서를 작성한다.
- [x] 누락 도메인과 교차 시스템 악용 루프를 점검하고, 조정 순서와 검증 기준을 고정한다.

이번 단계에서는 수치를 즉시 변경하지 않는다. `docs/game-design/whole-game-balance-recalibration-inventory.md`에 무엇을 한 행씩 다시 맞춰야 하는지 현재 콘텐츠 권위와 연결해 고정했다. 기준서의 과거 `20/99 WU` 직접 사용 구간도 모두 파생 재산정 대상으로 포함한다. 다음 수치 작업은 문서 24장의 다섯 표 중 `일일 생존 수요표`부터 시작한다.

## 2026-08-16 post-AI-stabilization WU remeasurement (complete)

- [x] Confirm that `19.882 WU/actor-day` predates the completed AI ownership and lifecycle stabilization and is not acceptable as the final live baseline.
- [x] Freeze the current measurement authority: three founders, five game-days, central actual/output-equivalent labor accounting, physical need cadence, runtime ownership conservation, harmful-stall zero, and Console zero.
- [x] Run seed `157181` from a clean disk-authoritative GameplayScene and capture the full report.
- [x] Run seed `157182` from a clean disk-authoritative GameplayScene and capture the full report.
- [x] Run seed `157183` from a clean disk-authoritative GameplayScene and capture the full report.
- [x] Aggregate only uncontaminated PASS samples and compare the distribution with the provisional `20 WU/actor-day`.

Final current-main evidence: all three clean five-day runs PASS at `44.418`, `48.882`, and `53.126 actual WU/actor-day`; mean `48.809`, sample SD `4.354`, range `8.708`, CV `8.92%`. Output-equivalent mean is `44.971 WU/actor-day`. The provisional 20 WU baseline is rejected; a rounded authored baseline of 50 actual WU/adult-day is the recommended next balance revision, while 45 output-equivalent WU/adult-day should be retained as the physical project-throughput reference.

## Phase 157D - source-derived exhaustive AI closure audit (in progress)

- [x] Derive the complete AI surface from production code and authored assets instead of trusting the existing coverage manifest.
- [x] Map every authored action, runtime-only intent, work type, behavior-tree/job-giver path, and domain AI route to deterministic production-live evidence.
- [ ] Audit cross-fault coverage for lifecycle transitions, target/item/facility loss, path invalidation, emergency suspension, and mid-action save/load.
- [ ] Implement and verify every confirmed missing row or production defect without weakening failure visibility.
- [ ] Re-run all affected focused matrices, three five-day seeds, 100/500 scale gates, large-grid/dense-dungeon gates, save/load, alert, and final Console 0/0.
- [ ] Accept completion only when the source-derived inventory has zero uncovered rows and all evidence was produced from the current source revision.

## 2026-08-25 V27 temporal-stasis maintenance physical outbox (static implementation complete, Unity pending)

- [x] Remove the callerless direct physical age-treatment interface and retain surgery as the only activation/regeneration authority.
- [x] Replace temporal-stasis seasonal count consumption with one exact two-item physical Sink receipt.
- [x] Persist intent/outcome/receipt provenance in CharacterLife current-format state and recover acknowledgement without a second debit or day extension.
- [x] Add no-partial-debit, acknowledgement-fault and uncommitted-intent focused coverage; compile Species, Runtime and Editor assemblies.
- [x] Record the checkpoint in the V27 implementation plan and whole-game balance baseline.
- [ ] Run the focused menu, surgery/whole-save regression and live facility PlayMode after Unity MCP approval is restored.

Current inventory authority is source-derived: authored action assets `19`, production concrete action types `22`, logical deprivation actions `5`, branches `25`, behavior operations `13`, BehaviorDesigner tasks `50`, job givers `11`, external-intent callsites `5` across `4` owner families, work types `31`, and registered AI domains `16`. The honest manifest intentionally remains failed while current-source live rows are missing or stale; the previous `uncovered=0` result was a reflection/entrypoint false-green and is no longer acceptance evidence.

2026-08-21 V27 retail boundary: exact item-definition restock selection, generic/unique retail lots, external Sink checkout, and current-format source-activation save joins are implemented. Official VisitorControl v5 is fresh PASS with exact quantity/mass consumption and a whole-save duplicate-activation tamper rejected atomically; Console is 0/0. Remaining work is the production-wide legacy count/untyped mutation inventory, then final PhysicalItem and mid-action SaveLoad regressions before this slice can close.

2026-08-15 continuation boundary: finish the alarm responder epoch/type carry-forward contract and the captivity-wildlife delivery terminal invariant first, then require a clean Unity compile and fresh focused reports for both before regenerating stale suites. The goal remains incomplete while any current-source manifest row is stale, missing, or failed.

2026-08-16 current boundary: expedition package participant 219, destination claims 220, and haul intents 225 compile on rebuilt current assemblies. Focused Offense Strategic is 11/11 with Console 0/0. Full Physical Logistics and mid-action SaveLoad are the next production-live authorities before the next non-construction FacilityBuffer claim slice.

2026-08-15 current closure status: the real strategic Offense UI journey now has a fresh terminal PASS with typed allied outcomes, round advancement, HP deltas, return, reward exactly once, and clean ownership. An independent audit still requires invalid allied interception to preserve the enemy intent at full execution strength and planned-round initialization to be symmetric; focused production regressions are in progress. Primitive five-day is fresh PASS after environmental safe-drink route admission and target-authoritative rumor mood stacking corrections. Primitive focused, VisitorControl, FirstRun, OffenseTactical, Daily seeds 157181..157183, and the final source-derived manifest must be regenerated after the final production source revision.

2026-08-16 active boundary: the newest full Offense Journey is red on a different current-source liveness defect. A Front unarmed party and a Rear melee enemy advance director turns while every empty-skill strategic card and enemy intent is decoded as BasicAttack, so `lastProcessedCommandId` and all combat effects remain zero. Replace the implicit empty-skill decoding with a persisted typed action contract, author an existing `Advance` card without changing deck size, derive enemy intents from the session's legal tactical command, retain the completed-turn trace across the next draw, and filter non-initiative objectives out of command decks. Then require focused and full production UI evidence before Daily seeds.

### Errors encountered

- Unity MCP was visible in the tool catalog but the fresh compile request again returned `Connection revoked. Go to Unity Editor > Project Settings > AI > Unity MCP to change approval.` No editor or scene action ran. The dirty GameplayScene remains untouched; this exact MCP request will not be repeated until approval state changes.

- `apply_patch` rejected a single patch that tried to delete and re-add the coordinator path in one operation. No file changed; the rewrite is split into an explicit delete patch followed immediately by an add patch.

- The first bounded coordinator-interface patch used an earlier comment sentence as its anchor and was rejected atomically. The exact current file was read and the retry targets the live declaration only.

- The first manual Assembly-CSharp compile after the V16 actor-release DTO change used the stale Items assembly and therefore reported missing new DTO members plus the old outbox interface. The retry compiled the Items response file first, then runtime and Editor in dependency order.
- The first ordered runtime compile exposed a missing `System.Linq` import and a definite-assignment error in the admission helper. Both were corrected before the next static compile.
- The first coordinator compile exposed an unassigned `preflightFailure` local. The control flow was made explicit and the ordered static compile then passed.
- The first Editor compile after adding the repository dependency to `WarehouseMassAdmissionService` found four focused-fixture constructor calls missing that argument. The fixtures now pass their exact repository; the ordered Editor compile then passed.

- A local `dotnet build DungeonStory.slnx --no-restore` compile probe could not start because the shell-visible `dotnet` host has no SDK installed. No build or file mutation occurred. Compilation will use the already-authorized Unity MCP/editor pipeline and will not repeat this unavailable SDK path.
- A PowerShell source-range helper again omitted the required space in `foreach ($r in $ranges)` and failed at parse time before reading or changing files. The retry uses a fully expanded `foreach ($range in $ranges)` form and this compact spelling will not be reused.
- A physical-drain source search included the guessed nonexistent `ProductionFacilityDestructiveDrainOwnerSourceProjection.cs` path. `rg` reported OS error 2 without changing files; the actual owner projection is `ProductionFacilityDestructiveDrainPlannedOwnerSaveProjection.cs`, which is now used directly.

- A bounded resource-catalog read guessed the nonexistent `ResourceEconomyDefinitions.cs` path after the preceding `rg` had already located the interface in `ResourceEconomyModels.cs`. The search portion succeeded, the read failed without changing files, and subsequent inspection uses the exact discovered path.
- A JavaScript orchestration cell mistakenly began with a PowerShell `$files=@(...)` declaration and failed with `SyntaxError: Invalid or unexpected token` before any nested command executed. No project file changed. The retry uses only valid JavaScript string commands and does not mix shell syntax into the orchestration layer.

- The first durable Physical rerun monitor assumed the verifier retained the previous artifact and repeatedly called `Get-Item` after the verifier had intentionally deleted it at run start. The PlayMode run and project state were unaffected. Subsequent polling checks `Test-Path` before reading and does not repeat the stale-mtime assumption.

- The first Gate S2 editor compile exposed CS0122 because the focused Editor scenario called an internal runtime mass-subject adapter, plus CS0165 from an `out commitFailure` declared inside a short-circuit assertion and read in its failure message. A stale Editor assembly still printed the previous PASS log, which is rejected. The adapter's immutable subject creation boundary is now public and commit evaluation is explicit before assertion; a fresh assembly timestamp and rerun are required.

- A focused verifier search used the stale `Services/Infrastructure/Editor/PhysicalItemLogisticsPlayModeVerifier.cs` path and returned OS error 2. The file was resolved with `rg --files` at `Services/Items/Editor/PhysicalItemLogisticsPlayModeVerifier.cs`; no project file changed in the failed read.

- A focused registration read used the nonexistent `Assets/Scripts/Services/Infrastructure/DependencyInjection/DungeonWorldSimulationRegistration.cs` path after `rg` had already shown the file lives under `Infrastructure/Registration`. No file changed; all following reads use the resolved exact path.

- The 13–18 final checkpoint batch stopped only at encounter 18; 13–17 wrote fresh PASS reports first. The next attempt will inspect the exact 18 band failure and fit only that capture row rather than repeating the whole batch.
- The first protect-objective tactics regression correctly failed after the initial target-score-only bias: the low-health decoy's execute utility still exceeded the modest objective score adjustment, so production selected the decoy. The fix is not a retry of the same weighting; protected-objective priority is now an explicit dominant utility term for legal hostile basic/ability actions, matching the session selector's lexicographic contract.

- 첫 ScriptableObject 유형별 에셋 수 집계 PowerShell은 `foreach` 블록 결과를 바로 파이프에 연결해 빈 파이프 요소 parser error가 났다. 파일은 변경되지 않았고, 결과를 `$rows` 배열에 모은 뒤 파이프하는 방식으로 재실행해 정상 집계했다.

- The first planning-with-files catch-up invocation tried to use Node's unavailable `process.cwd()` inside the tool orchestration sandbox and failed before launching Python. The retry passed the explicit workspace path and completed successfully; no project file changed in the failed attempt.

- Two planning-file patches used stale anchors and were rejected atomically without changing any file. The final retry targets only anchors verified by `rg` immediately beforehand.

- Unity's MCP Console reported 0/0 after a requested compile, but `Assembly-CSharp.dll` remained older than the changed sources. `Editor.log` exposed the real CS1061: `ItemTransferService.ReleaseDestination` called `InvalidateStack` on `IItemQuantityReservationService`; that API belongs to the separately injected `IItemQuantityLeaseMutation`. The old assembly caused the first Offense focused run to execute stale code. The fix uses the correct mutation authority and acceptance now requires updated DLL timestamps in addition to Console 0/0.
- A stale-assembly diagnosis search included nonexistent `Library/PlayerScriptAssemblies`; `rg` returned OS error 2 after the timestamp query. No source changed; subsequent checks use only the verified `Library/ScriptAssemblies` path.
- A PowerShell source-display command interpolated `"==== $f:$s-$e"`; the colon was parsed as a scoped-variable separator and the command failed before reading any file. No source changed; the retry uses `${f}`-delimited interpolation.
- A Windows `rg` audit passed a wildcard file path (`Assets/Scripts/Services/Items/*Haul*Restore*.cs`) literally and returned OS error 123. No source changed; the coordinator was located with `rg --files` at `Assets/Scripts/Services/Infrastructure/Save/HaulDeliveryIntentRestoreCoordinator.cs`, and later audits use the exact path.

- A planning-log patch targeted historical headings that were not present at the current file tails and failed without modifying any file. The retry uses the exact current tail lines and appends a dated section.
- A dynamic Unity compile command imported `UnityEditor.Compilation` but unqualified `CompilationPipeline` resolved to the enclosing `Unity.CompilationPipeline` namespace and produced temporary-command CS0234. Project source was unchanged; the corrected command uses fully-qualified `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()`.
- A PowerShell source-display helper used `foreach($z in$ranges)` and failed with a parser error before reading any file. No source changed; the corrected `foreach ($z in $ranges)` command completed and the failed command will not be repeated.

- The first focused equipment-haul command compiled against a stale Editor assembly and reported CS0117 because the newly added public runner was not yet visible, even though a prior asset refresh returned success and the Console was empty. No scenario executed. The next attempt explicitly requests Unity script compilation and waits for the assembly/domain reload before invoking the runner.
- After an explicit compile cycle reached `IsCompiling=false` with Console 0/0, the second focused invocation still saw the old type and returned the same CS0117. Repeating refresh is no longer acceptable; inspect Editor.log/compiled assembly timestamps and use reflection to determine whether project compilation failed silently or the dynamic command resolves a different assembly/type.
- Editor.log exposed the hidden project errors behind the stale assembly: the new focused test referenced three internal repository members from `Assembly-CSharp-Editor` (CS1061 at the operation allocator and intent runtime). The test is being rewritten against the public real-grid haul-planning path; production APIs will not be widened for verifier convenience.

- A first call-site inventory command combining `rg -l` with a second redirected search returned no output despite known matches. No source changed. The retry used one fixed-string `rg` invocation and returned the authoritative file list.

- The first Captivity/Wildlife verifier compile used a typed `CharacterId` directly against the persisted string `reservedCarrierId` (CS0019). The run was correctly blocked before PlayMode; the verifier now compares `CharacterPersistentIdentity.Require(worker).Value`, and the pending request is retained for the clean recompile.
- The first production-pickup transport rerun revealed that the live wildlife actor could leave its planned source cell while the incremental pickup preflight yielded. `TryOrderCapture` returned accepted, then its transport immediately failed and released the animal at the carrier. Capture eligibility/damage is now applied immediately after the exact source assertion, before any preflight yield, so the same production pickup path is warmed against a stable target.
- The stationary-target rerun completed real pickup and delivery with a typed Completed terminal, then exposed two later gates: the verifier rejected the expected exact carrier occupant on the delivery cell, and an invasion-driven Amber-to-Red alert escalation attempted to bind the new epoch over the retained Amber emergency gate. The verifier now allows only the exact carrier occupant; production now uses an explicit monotonic gate-epoch advance while stale, reversed, or mismatched ownership still throws.

- A parallel FacilityEvolution source audit returned shell exit 1 because one optional `rg` pattern had no matches; another parallel result still surfaced unrelated output, so the batch was not authoritative. No source changed. Subsequent searches explicitly normalize `rg` no-match to success and use narrower files.
- A follow-up `rg` passed a Windows-invalid wildcard path (`RestoreWorldCandidate*`) and flooded the batched output with unrelated matches before exiting 1. No source changed. The retry resolves the exact filename with `rg --files` first and reads only that file.
- A batched fake-implementation search aborted because an `rg` no-match exit was not normalized in every branch. No source changed. Remaining searches are issued in one PowerShell script with explicit `$LASTEXITCODE` handling.

- A PowerShell display helper used `[string]::Join` on a null `rg` result while listing work-type PlayMode references. The search itself completed and no files changed. Future inventory uses array coercion or direct pipeline output instead of repeating that formatter.
- One `rg` call used a Unix-style wildcard directory (`Assets/Scripts/Services/*/Work`) that PowerShell passed literally on Windows. The contract file was still read successfully; subsequent searches use `--glob` from a real root directory.
- First batched Unity compile exposed verifier API mismatches that static brace/symbol scans missed: `ICaptivityEscapeRuntime.TryBeginEscapeAttempt` does not exist, and the visitor verifier referenced nonexistent `IFactionRuntime` / `FactionContractKind`. PlayMode was correctly blocked; both owners were sent back to the production registered contracts instead of adding compatibility fallbacks.
- The second compile reduced the errors to one unresolved `IGameEventBus` namespace in the captivity/wildlife verifier. The event-driven escape route remains the correct production boundary; only its owning namespace/import must be corrected before any PlayMode evidence is accepted.
- The strict source-derived coverage capture intentionally threw `CHARACTER_AI_COVERAGE_MANIFEST=FAIL; uncovered=68`. This is the correct fail-loud result: 3 WorkType rows are current LiveExecuted, the focused visitor/offense/captivity runs are red, and most earlier artifacts are stale against the changed C# source. Do not rerun the strict command until those rows are repaired; use the nonthrow capture only for intermediate inventory snapshots.
- A dynamic Unity refresh command imported `UnityEditor.Compilation` but resolved `CompilationPipeline` as the enclosing `Unity.CompilationPipeline` namespace, causing CS0234 in the temporary command only. The project source was untouched. The retry used a distinct command containing only fully-qualified `UnityEditor.AssetDatabase.Refresh`, after which Unity completed compilation with Console Error/Warning `0/0`.
- Unity's delayed Editor-assembly compile then exposed one project error in the new restock fixture: `Shop.TryGetSaleItem` does not exist (CS1061 at `CharacterAiWorkTypeLiveMatrixPlayModeVerifier.cs:885`). The attempted visitor rerun never entered PlayMode and its old artifact was not accepted. The fixture owner is replacing the call with the actual Shop stock authority.
- A clean compile request initially appeared green in the MCP Console, but `Editor.log` showed `CS0136` in `WorkTargetSelector.cs`: the new reachable-resource branch declared `activeGrid` inside a scope whose fallback later declared the same name. Renamed the branch-local variable to `reachableGrid`; do not trust a stale `Library/ScriptAssemblies` timestamp or an empty Console alone when Bee reports failure.
- `CharacterAiPlanDebugScenarios.RunAll` emitted `GameManager requires a GameData settings asset` even while reporting logical PASS. The static service-locator removal audit instantiated `GameManager`, invoking `Awake` without the authored settings asset. It now verifies the removed method by reflection without creating a runtime object; a clean-console rerun remains required.
- A batched post-freeze `rg` audit used a PowerShell-escaped regex whose grouping was malformed, so the search command exited before returning the parallel Unity state. No project source changed; the retry uses fixed-string searches and separates Unity state from shell diagnostics.
- The first post-freeze Offense audit found two exact-once P0 gaps before Unity import: Director finalized/cleared its command turn without observing `FinalizePlannedTurn` failure, and the first planned round initialized only the initiative actor so cooldown progression was asymmetric. Unity compilation was deliberately held; the owner is correcting both contracts with focused regressions.
- A progress log patch targeted a sentence that exists in `findings.md`, not `progress.md`, and therefore failed without modifying either file. The retry appends a standalone dated progress section instead of relying on a stale context anchor.
- A root-wide `git diff --stat` attempted to invoke the Git LFS clean filter for a screenshot and failed because `.git/lfs/tmp` was not writable in the managed workspace. No source changed; subsequent diff checks are scoped to the relevant C# and planning files instead of touching LFS artifacts.
- Fresh full Offense Journey reached 12 planned turns, terminal battle, return and exactly-one reward, then failed the Console gate with `NullReferenceException` in `OffenseBattleDirector.FinishTurnCards`. Successful runtime finalization synchronously cleared the Director state before post-finalize card cleanup accessed `State.decks`; the terminal cleanup ordering is now the active production fix and the FAIL artifact is not accepted.

## Phase 157C - exhaustive AI scenario matrix (in progress)

- [x] Reconfirm the current action catalog, 31 work types, domain AI runtimes, and existing verifiers from the live worktree.
- [x] Preserve `PathSearchDeferred` as a bounded retry instead of terminating a committed route as `NoPath`.
- [x] Make building destruction finish occupancy, assignment, registry, and grid teardown even when a destruction subscriber throws; prove later subscribers and repeated teardown remain deterministic.
- [x] Separate actual gameplay progress from scheduler/path/reservation bookkeeping so PlayMode stalls cannot be hidden by administrative ticks.
- [x] Complete the registered action x phase x injected-fault coverage manifest and make uncovered rows fail the audit.
- [x] Add live mid-action save/load rows for partial work, facility queue, meal commit, and suspended emergency work.
- [x] Add combat, medical, captivity, wildlife, hunting, rescue, substance, and expedition lifecycle/fault rows represented by the registered manifest.
- [x] Re-run three five-day seeds and the 100/500 actor scheduler, gameplay-progress, fairness, performance, and GC gates.

Current evidence: the expanded live Brain/BT/movement/facility matrices, lifecycle/save/alert/cross-domain verifiers, three five-day seeds, and 100/500 actor gates pass. Overall exhaustive coverage is still not complete until Phase 157D proves the registered manifest covers the full production surface and refreshes stale evidence.

## Phase 157B - AI no-progress and decision-conflict hardening (in progress)

- [x] Add bounded action lifecycle, replan and committed failure counters.
- [x] Preserve exact execution failure kind/reason through the decision pipeline, blackboard, activity history and bounded counters.
- [x] Guard facility coroutines against destroyed targets and replaced actions after every yield.
- [x] Keep tight repeated failures visible across action restarts with a bounded diagnostic window.
- [x] Prove in-flight facility destruction and action replacement with real actor/facility coroutine regressions, including visit state, occupancy, recovery and replacement preservation.
- [x] Move facility resource consumption to the final commit boundary and route committed facility failures into typed AI diagnostics.
- [x] Apply post-yield action/facility validation and commit-token ownership to shop browsing, checkout, purchases and meal service.
- [x] Prove delayed purchase cannot commit money/on-buy effects after its owning AI action is replaced.
- [x] Add five-day branch dwell and harmful no-progress evidence.
- [x] Add bounded hierarchical job-giver rejection counters by branch and failure kind.
- [x] Include selected utility, route, mood/macro bias and typed rejection evidence in stall reports.
- [x] Correct omitted thirst pressure in work/wait scoring and prevent survival-due waits from presenting as social short-chat.
- [x] Recompile and rerun deterministic isolated five-day observation through Unity MCP. Three diagnostic runs reproduced and then eliminated the false destinationless-Wait path deferral; the latest remains failed on higher-level survival stalls/cadence and is not completion evidence.
- [x] Pass focused survival priority, destination failure, path deferral, work interruption, naturalness and emergency-thirst routing regressions.
- [ ] Pass save round-trip and Console Warning/Error gates; update the long-run evidence report.

Current unit: the queue-aware five-day rerun passes at `62.485 WU/actor-day`, with primitive fallback, meal execution failures and harmful stalls all zero. Recheck a clean PlayMode startup after deleting the stale scene object, then complete save-round-trip and multi-seed evidence before replacing the provisional baseline.

2026-08-13 current unit update: local meal-buffer availability no longer depends on a transient exact-path budget. Three corrected same-floor samples now produce 54.721-62.944 actual WU/adult-day with zero harmful stalls; the latest passes all cadence and physical-consumption gates. FIFO refresh/cancellation, meal spoil-abort diagnostics and 500-NPC performance pass focused audits. Finish facility-destruction/save-round-trip proof, then run multi-seed and technology-stage WU matrices before replacing the provisional baseline.

2026-08-13 queue projection update: a new five-day run produced `52.524 WU/adult-day` and zero harmful stalls but caught one real priority conflict: a same-cell toilet occupied for one short service caused emergency primitive latrine fallback. Replace the fixed emergency-minus-ten cutoff with projected need after travel/queue/service ETA, isolate the order-dependent priority-corner fixture failure, then rerun the same five-day gate. Retain the one typed meal `ConsumptionFailed` as a separate contention signal until its operation detail is captured or eliminated.

2026-08-13 queue projection result: the same five-day gate now passes at `62.485 WU/adult-day`; the false primitive fallback and the meal `ConsumptionFailed` are both zero. Priority-corner and customer suites pass. A stale uninitialized editor fixture was removed from the scene; clean startup, save round-trip and multi-seed evidence remain.

## Phase 157 - Single worker-intent authority and live WU recovery (in progress)

- [x] Inventory the competing character action owners. Routine safe relief, emergency relief, primitive survival, deprivation breakdown and direct movement previously mutated one `AIBrain` through unrelated booleans/coroutines.
- [x] Add a typed external intent lease with owner ID, priority kind and monotonic epoch; only the current lease can update presentation, commit physical effects or end the action.
- [x] Route safe relief, primitive survival and breakdown runners through the lease authority; deferred retry no longer locks the actor while waiting.
- [x] Make violent breakdown a bounded episode. Completion clears the active breakdown and lowers its cause below the retrigger threshold instead of reissuing the same action every decision tick.
- [x] Protect direct player movement from autonomous external intents: manual mode retires the prior external owner and new external intent acquisition is rejected until the command ends.
- [x] Add intent diagnostics to the five-day report: owner/kind/epoch, transitions, preemptions, rejected acquisitions and stale completions.
- [x] Fix editor AI fixture composition so carry inventory receives the same typed catalog/settings authority as a live world; focused naturalness and deprivation authority regressions pass through Unity MCP.
- [x] Repair ordinary work resumption/parallel assignment after routine needs. Construction reservations now use stable `CharacterId` across the actor/visitor adapter boundary; the focused regression passes and the isolated five-day run reaches three active workers / 2.60 effective workers.
- [ ] Add bounded cumulative AI diagnostics for action/phase transitions, committed execution failures, candidate rejections, repeated identical failures, replans and harmful no-progress intervals. Preserve the current-state trace but do not rely on it as five-day evidence.
- [ ] Diagnose the remaining recreation cadence (`0.4/actor-day`) and the large `need travel=36.508s`, `other travel=24.556s`, `idle/other=39.097s` channels from cumulative evidence before changing need or facility balance values.
- [ ] Re-run the isolated five-day sample until exact physical needs, multi-worker construction, cumulative diagnostics, harmful-stall gate and Console 0/0 all pass together. Only then re-author the provisional 20-WU baseline if the stable mean differs materially.

### Phase 157 intent authority record

- Authority: at most one externally driven action lease per actor. An owner is identified by a stable string and an epoch; priority is `RoutineNeed < EmergencyNeed < ProtectedAction < Breakdown`, while a manual command excludes all autonomous external acquisition.
- Commit rule: a coroutine may only consume an item, recover a need, damage a target, update debug state or end the action when its exact lease is still current.
- Retry rule: waiting for a future facility/stock retry is not an action and owns no actor lock.
- Breakdown rule: violent impulse is one bounded episode. Persistent deprivation may cause a later new episode through the normal burden/check interval, never by leaving `breakdown.active` true after the routine ends.
- Player rule: direct movement cancels/retires the current autonomous external owner and remains immune to stale autonomous completion until it ends.
- Current evidence: focused AI naturalness and deprivation-authority suites pass; Unity compile and edit-mode Console Warning/Error are 0/0. First instrumented five-day run exposed 29/83 repeated breakdown transitions; after the bounded-episode fix the next run had 4/4/4 transitions, zero preemptions/rejections/stale completions at end, and no active breakdown.
- Remaining evidence: stable parallel construction and five-day labor/cadence pass. Balance remains `기준 배정`, not complete.

## Phase 147 - V26 whole-game balance continuation (in progress)

- [x] Audit the current new-game three-founder random proficiency/reroll system end to end before changing initial balance: generation distribution, species/trait/owner modifiers, reroll scope and limits, UI preview/commit, deterministic seed behavior, and save authority. The current system has unlimited employee full rerolls, restartable partial limits, no role/total-XP constraint, non-isolated group rerolls, a preparation RNG not directly reseeded from the new run seed, and globally rollable traits.
- [ ] Replace the flat founder packet with age-bound starting profiles: four subgrades inside every major proficiency rank, explicit primary/secondary specializations, independently authored origin and past-history bonuses, species-relative starting age, age-based proficiency caps, and optional initial age conditions projected through the existing life/health authority. Implementation, age-health retuning, 18,000-profile deterministic audit, 20,000-roster no-reroll coverage audit, strict character-save round trip and runtime catalog pass; preparation-scene visual verification and full-world round trip remain.
- [x] Replace the fixed three-trait founder roll with a weighted 1-4 trait identity: 15/40/35/10% count weights, mandatory mutually-exclusive trait families, lower weights for strong net-positive traits, species eligibility, four-trait UI/save support and a deterministic distribution audit. Retuned dominant legacy traits; Fast Learner now grants x1.30 approved-work XP. The 100,000-profile Unity MCP audit passed at 15.203/40.029/34.664/10.104%, mean 2.397, 56/56 reachable, zero family/species leaks, four-trait save round trip and Console 0/0.
- [x] Inventory and evaluate all 56 founder traits against one common rubric: live authored mechanics, rarity/family, operational strength, real downside, same-family competition and implementation gaps. The full report covers 56/56 traits with ratings S3/A8/B10/C5/D22/F8 and identifies the rolled-trait behavior/mood authority mismatch plus unused event-weight path. No gameplay value changed.
- [x] Add a real-time playtime axis to the industrial checkpoints using the authoritative 180-second day and x1~x5 speed range. The table separates pause-exclusive simulation time from a provisional effective-speed playtime band that still requires observed calibration.
- [x] Re-certify the V26 nine-proficiency mapping across all 31 work types and authored building/recipe/equipment/apparel content. Current audit covers 419 buildings, 354 recipes, 61 combat equipment definitions and 56 apparel definitions; focused aggregate, 100,000-sample quality, 960-day decay and 2,000-resident allocation probes pass.
- [x] Re-run the current full-world save round trip after the single-authority proficiency change. Latest gate passed registered/captured/post `68/68/68`, canonical baseline restoration, character progression contracts, and Console 0/0 after removing read-side loadout creation.
- [x] Trace the authoritative invasion scheduling, threat growth, expedition unlock and warning paths against the intended day 1-10 settlement / day 10-30 rehearsal-combat cadence.
- [x] Implement a deterministic early-game non-forced-combat contract without suppressing player-initiated training or later invasion pressure. World-map planning remains open; actual launch requires authored field-rations research at both UI and direct runtime boundaries.
- [x] Run focused pacing and direct-launch-bypass probes, compile, confirm Console 0/0, and update balance evidence.
- [x] Make expedition readiness use the same equipped weapon, armor and shield authority as actual combat, with proficiency remaining the primary power source and loadout contribution capped.
- [ ] Audit population growth, proficiency growth and equipment acquisition together against settlement labor capacity and threat scaling across 1/30/120/240/400/960-day checkpoints.
- [x] Generate a non-circular checkpoint report from the proficiency projection, actual authored equipment snapshots and authored campaign requirements; do not derive resident power from the target requirement being tested. Six checkpoints pass and the report is at `Artifacts/QA/v26-population-power-checkpoints.md`.
- [x] Cap successful guest/mercenary recruitment to one resident per 10 absolute days, persist the successful recruitment day, and give late recruits a two-specialization proficiency floor without granting Expert/Master ranks.
- [x] Run the actual-rule 256-seed 1~960-day population/labor policy envelope with reproduction success, adulthood delay, regular recruitment cadence, natural mortality and proficiency-derived daily EWU.
- [ ] Extend the long-horizon population probe with captive recruitment, faction joiners, golem assembly and actual housing/food/medical capacity before treating the day-960 target as complete.
- [x] Measure checkpoint loadout production throughput against combat-ready headcount using live equipment BOM, direct craft WU, embedded work, research access and quality-attempt cost; expedition-party, new-slot and full-reserve envelopes now pass/report separately across all six checkpoints.

### Phase 147 balance record

- Target authority: `balance:early-settlement-combat-cadence-v26`.
- Growth stage: day 1-10 survival settlement, day 10-30 first warning and rehearsal invasion.
- Physical/economic constraint: the peace window changes scheduling only; it creates no resources, removes no invasion cost and does not reduce later threat accumulation without an authored catch-up rule.
- Player decision: training and voluntary preparation remain available; external combat and hostile invasion must not be the optimal or mandatory first-day action.
- Exploit boundary: saving, pausing, changing scenes or withholding a trigger cannot reset the absolute-day gate.
- Save authority: absolute calendar and existing invasion scheduler state; no parallel timer.
- Required evidence: authored threshold audit, day-boundary deterministic probe, warning lead-time probe, save/reload invariance and Console 0/0.

### Phase 147 founder-profile balance record

- Target authority: `balance:founder-age-background-proficiency-v26`.
- Growth stage: new-game owner plus two selected staff before day 1; later work, training and mentorship retain the existing proficiency authority.
- Physical/economic constraint: starting experience represents work already performed before arrival and creates no material, item, research or completed work order. It only changes the speed, quality, accident and combat projections of the selected founders.
- Player decision: compare age and health risk against primary/secondary specialization, origin and past history instead of rerolling nine unrelated numbers.
- Exploit boundary: every proficiency is capped by the rolled species-relative age band; no founder may start at Technician or above, and origin/history bonuses cannot bypass the cap. Identity and aptitude rerolls must regenerate the whole dependent profile together rather than preserve the best cross-group pieces.
- Reroll policy: unlimited manual reroll is an accepted player-side optimization and is not a balance-completion blocker. All expected-value and milestone evidence uses the clearly labelled no-reroll natural distribution.
- Revised age-health target: because specialization now accelerates future learning and unlimited manual reroll is accepted, Elder incidence rises to 65-80% with any condition and 25-45% with multiple conditions. At the 5% Elder weight this keeps a healthy Elder with one specified primary specialization near 1% per seven-candidate natural roster.
- Specialization growth target: primary proficiency earns x1.50 XP, secondary earns x1.20 XP and all others earn x1.00. The factor applies to approved work, combat, training and mentorship/direct learning before existing daily caps; initial packets and campaign catch-up floors are not learning events and are not multiplied.
- Save authority: the existing character growth state stores the prepared profile and starting proficiency packet; the existing character-life Aggregate owns biological age and age conditions after publication. No parallel current-proficiency or health cache is introduced.
- Required evidence: exact IV/III/II/I boundaries for all five major ranks, primary > secondary > unrelated expected XP, x1.50/x1.20/x1.00 learning across work/direct/combat paths with unchanged caps, monotonic age caps, all nine specializations represented by authored histories, deterministic generation, initial-life publication, strict save round trip, owner-plus-six candidate selection coverage, preparation UI projection, compile and Console 0/0.

### Phase 147 founder-trait balance record

- Target authority: `balance:founder-weighted-traits-v26`.
- Growth stage: new-game owner and staff identity generation; later trait state continues to use the existing character growth/save authority.
- Physical/economic constraint: traits create no items, materials, research or completed WU. Their speed, learning, consumption, combat, accident, mood and behavior effects must be paid through the normal simulation paths.
- Player decision: accept fewer coherent traits or reroll for a rarer high-upside identity; a fourth trait increases breadth rather than guaranteeing another strong effect.
- Count target: 1/2/3/4 trait weights are 15/40/35/10%, for an expected 2.40 traits per naturally generated adult.
- Rarity target: common/uncommon/rare/exceptional selection weights are 100/55/25/10 before eligibility. Strong net-positive traits must be rare or exceptional; mixed and drawback traits may remain common/uncommon.
- Exploit boundary: every trait has one authored family and no generated identity may contain two traits from the same family. Explicit pair conflicts and species eligibility are enforced in addition to family exclusion. Unlimited manual reroll remains accepted, while all reported rates use the no-reroll natural distribution.
- Distinctiveness target: every trait needs at least one operational modifier or behavior/mood/event consequence. Fast Learner modifies earned XP, strong legacy multipliers receive an explicit downside or reduced magnitude, and species-named traits cannot roll outside their eligible species.
- Save authority: existing `CharacterGrowthState.traitIds`; selection metadata remains authored definition data and is not copied into saves. The saved maximum expands from three to four IDs without a parallel trait state.
- Required evidence: deterministic 100,000-profile count/rarity/family audit, all 56 general traits reachable, no duplicate ID/family/pair conflict, species filter proof, learning-path proof, four-trait save/UI projection, compile and Console 0/0.

### Phase 147 loadout-power balance record

- Target authority: `balance:expedition-loadout-power-v26`.
- Growth stage: day 10 through EndlessAge; the same projection is used at every checkpoint while proficiency, health and equipment inputs change.
- Physical/economic constraint: only equipment physically assigned to the selected character contributes. Material, quality, durability and ammunition readiness change the contribution; no inventory-wide or definition-only bonus is allowed.
- Player decision: roster experience and equipment investment are complementary. Better gear improves readiness, but cannot replace trained residents or expand the five-member party cap.
- Exploit boundary: total loadout contribution is capped at 60% of current character power; weapon/armor/shield subcaps are 35%/30%/15%. Broken, missing or unloaded equipment cannot report full value, and UI preview and committed expedition power must call the same query.
- Save authority: existing character proficiency/health state, combat equipment instances/loadouts and expedition snapshot. No new saved power stat or cache is introduced.
- Required evidence: unarmed < early loadout < advanced loadout, quality/durability/ammunition monotonicity, contribution cap, production UI/launch call-path identity, focused deterministic probe, compile and Console 0/0.

### Phase 147 population-power checkpoint record

- Target authority: `balance:population-power-checkpoints-v26`.
- Growth stage: absolute days 1/30/120/240/400/960, separating total population, working adults, dependents and combat-ready adults.
- Physical/economic constraint: births remain dependents until species adulthood; early worker growth therefore comes from recruitment, captivity conversion and constructed golems, while every combat-ready resident also consumes housing, food, training time and a physically produced loadout.
- Player decision: expand dependents for long-term continuity, recruit adults for near-term labor, or divert trained adults and equipment from defense/production into expeditions.
- Exploit boundary: party size remains five; population outside the selected party provides reserve depth rather than additive expedition power. Checkpoint power is computed before reading the target requirement and cannot be reverse-authored from it.
- Save authority: existing population/life-stage, proficiency, equipment-loadout, research and campaign aggregates. The checkpoint table and derived power are QA reports, not saved gameplay state.
- Required evidence: authored adult-age delays, recruitment/reproduction entry paths, actual equipment definition snapshots, proficiency-only base power, loadout caps, campaign member/power requirements and a generated 1/30/120/240/400/960-day report.
- Focused supporting evidence: `V19LifeSimulationDebugScenarios` passed 200,000 aging samples and 4/6 daily aging; `RegularCustomerDebugScenarios` and `CharacterPopulationDebugScenarios` passed recruitment activation, identity, save and bounded non-staff population. These do not replace date-indexed throughput telemetry.

### Phase 147 recruitment-throughput record

- Target authority: `balance:regular-recruitment-throughput-v26`.
- Growth stage: day 1~960; one successful general recruitment every 10 absolute days across both employee and mercenary paths.
- Physical/economic constraint: recruited adults still consume housing, food, wages, equipment and training capacity; the pacing rule creates no residents or resources.
- Player decision: accept the current suitable specialist or keep the global slot for a later candidate.
- Exploit boundary: candidate switching, save/load and employee/mercenary path switching do not reset the last successful recruitment day.
- Save authority: `DungeonRegularCustomerRecordSaveData.recruitedAbsoluteDay` in the existing regular-customer section; proficiency floors use the existing character narrative aggregate.
- Required evidence: day 1/10/11 boundary, save/restore identity, catch-up floor matrix and later multi-seed population simulation.

### Phase 147 population-labor multi-seed record

- Target authority: `balance:population-labor-multiseed-v26`.
- Growth stage: absolute days 1/30/120/240/400/960 across conservative, balanced and expansion policy envelopes.
- Physical/economic constraint: the audit uses authored adulthood, reproduction phases, base success, natural age-condition onset, regular-recruit cooldown and proficiency work rates. It assumes safe housing/health/nutrition and therefore remains an upper envelope before physical capacity.
- Player decision: recruit adults now, accept reproduction for later continuity, or preserve labor and housing capacity.
- Exploit boundary: every non-golem profile must begin with `Attempt`; base success cannot be bypassed, births cannot skip dependent time and regular recruitment cannot run faster than the saved 10-day global cadence.
- Save authority: existing life, reproduction, regular-customer and character-proficiency aggregates; the generated report is not saved gameplay state.
- Result: balanced median total population is 3/5/11/20/30/64; conservative day 960 is 33 and expansion is 100. The balanced day-960 shortfall of 16 requires physical captive/faction/golem inflow at roughly one adult per 60 days rather than an implicit birth-rate buff.
- Evidence: `SettlementPopulationLaborSimulationDebugScenarios`, 3 policies × 3 starter species × 256 seeds × 960 days, `Artifacts/QA/v26-population-labor-multiseed.md`.

### Phase 147 equipment-readiness throughput record

- Target authority: `balance:equipment-readiness-throughput-v26`.
- Growth stage: absolute days 1/30/120/240/400/960, using the checkpoint loadouts already selected without deriving costs from campaign requirements.
- Physical/economic constraint: every ready slot requires a physical weapon and minimum protective equipment. The audit uses live default-material BOM, upstream EWU, direct craft WU, research gates and quality-attempt pressure; it creates no free equipment or abstract stock.
- Player decision: equip the minimum expedition party first, outfit newly combat-ready residents, or refresh the wider reserve with contemporary equipment while preserving labor for survival, research and construction.
- Exploit boundary: existing equipment remains physical reserve stock; the audit reports a conservative full-reserve refresh separately and does not assume discarded gear vanishes, upgrades itself or supplies free salvage. Quality targets cannot be injected without their expected repeat cost.
- Capacity comparison: period labor uses 99 WU per working adult per day and the baseline 35% lower growth/production allocation. Direct workstation pressure and total embedded-work pressure are reported separately.
- Save authority: existing equipment definitions, recipes, materials, research and combat-ready population targets. The generated throughput report is QA evidence and is not saved gameplay state.
- Required evidence: all loadout definitions and physical BOM resolve; checkpoint research is reachable; direct and embedded work are finite; expedition-party and new-ready-slot envelopes fit their windows; full-reserve pressure is visible; compile and Console 0/0.
- Result: the manufacturable party loadouts retain non-circular power ratios 1.26/1.52/1.55/1.83/1.56 at days 30/120/240/400/960. Party equipment consumes 32.5%/24.2%/75.6%/90.9%/27.1% of the conservative period production floor; newly ready slots consume 0%/2.3%/2.0%/2.3%/1.1%. Research/equipment validation passes with zero stale absorbed research IDs.

### Phase 147 error log

- Reflection confirmed the V26 builder type was not loaded. A Unity MCP project-data diagnostic then took roughly 158 minutes before returning, although it confirmed the new script and generated `.meta` exist in the project. No mutation occurred during the wait. Further diagnostics avoid the heavy project-data endpoint and inspect the MonoScript/compiler state directly.
- The Unity menu retry reported that the V26 builder menu was not registered even though the refresh command itself compiled and Console had no entries. No builder mutation occurred. The next diagnostic inspects loaded assemblies for the type and invokes it through reflection if present, avoiding both compile-time Editor assembly references and menu registration assumptions.
- The first Unity MCP invocation of `V26FounderTraitContentBuilder.Build()` failed because the dynamic command assembly cannot directly reference the project's Editor-only default assembly. No assets were created; the retry uses Unity's registered menu item through `EditorApplication.ExecuteMenuItem` instead of a compile-time type reference.
- A guessed `Assets/Scripts/Content/CharacterSpeciesSO.cs` read failed after `rg` identified the actual file under `Assets/Scripts/Models/Species/Core`. No mutation occurred; the confirmed file was then read directly.
- A combined trait-authority/starting-proficiency search returned exit code 1 after useful results because one broad branch had no matches and the output was truncated at 300 lines. No mutation occurred; follow-up inspection uses confirmed symbols and bounded files only.

- The first Elder founder health tuning multiplied the natural per-birthday onset probability by 12. The 18,000-profile audit produced only 13.8% Elders with any condition but 10.9% with multiple conditions, showing undesirable correlation and missing the 25-45% incidence target. It was replaced with an explicit age-progressive accumulated starting-burden distribution; published conditions still use the existing CharacterLife authority.
- A snapshot/random-provider discovery command included a nonexistent guessed `Assets/Scripts/Foundation` directory, so the final `rg` returned exit code 1 after the requested confirmed-file content was printed. No mutation occurred; the provider was then opened at its actual `Assets/Scripts/Services/Foundation/Random` path.
- A combined founder skill/UI/persistence inspection returned exit code 1 after its useful bounded source output because the trailing broad `rg` pipeline had unmatched/overflowing branches. No file changed; subsequent persistence inspection uses only confirmed files and exact symbols.
- A founder-reroll search combined an invalid Windows wildcard path (`Assets/Scripts/Models/Character*`) with a nonexistent guessed settings directory, so `rg` returned exit code 1 after producing partial matches. No file changed; the retry used confirmed source and asset paths.
- The first playtime arithmetic check piped a bare PowerShell `foreach` block and failed with `EmptyPipeElement`; it changed no files. The retry collected rows into an array, confirmed all six values, and `git diff --check` passed.
- The first industrial-playtime documentation patch expected the wrong `findings.md` title and was rejected atomically by `apply_patch`; no file changed. The retry used the actual `DungeonStory Current Findings` heading.
- The first Unity MCP refresh command resolved `CompilationPipeline` inside the command runner namespace and failed before execution. No project asset was changed by that command; the retry uses the fully qualified `UnityEditor.Compilation.CompilationPipeline` type.
- The first project compilation after adding the production research dependency exposed one explicit 23-argument Editor fixture call. The fixture now supplies an authored completed field-rations research state; production enforcement remains fail-closed and lightweight unit constructors remain unchanged.
- The first combined focused-run command assumed the legacy pacing probe returned text, but it returns `void`; the command failed before execution. The retry invokes it separately and logs a completion marker.
- Final `git status`/`git diff --check` hit the repository's existing Git LFS temp permission failure under `.git/lfs/tmp`. No file operation was attempted; status verification is retried read-only with the LFS clean filter disabled.
- The first clean compile of equipment-aware expedition power used `CharacterActor.PersistentId`, but the authoritative ID is `CharacterActor.Identity.PersistentId`. Compilation stopped before execution and changed no runtime state; the query now resolves the existing identity component exactly like `OffenseExpeditionRuntime`.
- A read-only equipment discovery command passed Windows wildcard paths directly to `rg`, which rejects those path arguments. It changed nothing; the retry searches the equipment directory with `-g` filters.
- The first checkpoint invocation attempted reflection through the Unity command runner, whose current safety policy rejects `System.Reflection`; nothing executed. The retry calls the compiled public audit entry point directly.
- The first checkpoint run found day-1 dagger plus shield projected above the day-30 set because the very fast dagger cycle dominated the readiness score. The day-1 baseline now uses the authored two-handed spear and cloth hood without a shield, matching an actual starting loadout and the no-external-expedition phase; campaign requirements were not used to choose the replacement.
- The next checkpoint run found the day-400 matchlock-pistol profile below the day-240 melee profile because reload time is intentionally penalized. The general progression checkpoint now uses the powered gauntlet/harness/shield package unlocked by the authored mature-industry cadence; firearm readiness remains a separate ammunition and formation role rather than the universal progression baseline.
- A clean compilation exposed two earlier incremental-build gaps: the late-recruit catch-up rule lived in a later services assembly while its pure test lived in the recruitment model assembly, and the activation service requested a nonexistent `IGameCalendarQuery` instead of the existing `IGameCalendar`. The pure rule now lives with the recruitment model and the activation service uses the existing calendar contract.
- The first population simulator run used noncanonical process and character ID prefixes, so the reproduction constructor rejected the first attempted process before any live-world mutation. The simulator now uses `reproduction:` and `character:` IDs and its deterministic rerun passes.
- The first equipment-throughput inspection command referenced the private `V23BalanceAudit.EditorContentSource`, so Unity MCP dynamic compilation stopped before execution. No project asset or runtime state changed; the retry moved to the public root content catalog.
- The public-root dynamic inspection still could not compile Odin-backed recipe/material generic calls because the Unity MCP command assembly does not reference `Sirenix.Serialization`. No project asset or runtime state changed; the throughput audit will run as a normal project Editor scenario through Unity MCP instead of duplicating or bypassing the catalog.
- The first compiled throughput report passed `content.GetAll<ItemDefinitionSO>()` to the EWU calculator, but the public catalog's generic method reads the domain catalog while root item definitions live under `content.Items.Definitions`. This made four late equipment items appear unresolved even though the authoritative V23 audit reports zero unresolved items. The audit now uses the root item-definition authority.
- Re-running the full V23 audit through Unity MCP confirmed `unresolved_items=0` but failed on pre-existing guest reward, market recovery and one retail-price calibration set outside the equipment-throughput scope. No balance assets were rewritten by the audit; keep this evidence separate from the current equipment-capacity diagnosis.
- A normal `git status` retried during the continuation again hit the existing `.git/lfs/tmp` permission failure before it could report status. No file mutation came from Git; targeted read-only status continued with the LFS clean filter disabled.
- The first manufacturable day-30 candidate combined the authored two-handed spear with a wood shield. The real loadout authority rejected it with `equipment.loadout.insufficient_hands`; no asset was changed by the failed audit, and the party checkpoint now uses the one-handed non-growth falchion.
- The first split throughput run correctly exposed 22 stale `requiredResearchId` values that still named V21-absorbed research projects. A read-only Unity MCP census bounded the scope, then the same consolidation authority normalized exactly 11 item/recipe pairs; the final full census reports zero stale IDs.
- A PowerShell discovery command passed two wildcard strings to the single-string `-Filter` parameter and failed without mutation. The retry filtered the enumerated asset list with `Where-Object`.

## Phase 146 - V25 narrative AI workspace extraction (in progress)

- [x] Inventory every V25 training source, generated artifact, notebook, launcher, report, and game-runtime integration that currently lives under DungeonStory.
- [x] Classify files as standalone AI workspace, game-runtime contract, generated artifact, or compatibility bridge; identify all hard-coded paths before moving anything.
- [x] Choose and verify an explicit destination outside the Unity project, then move only the standalone training workspace while preserving Git history and Colab/Drive resume paths.
- [ ] Replace cross-workspace dependencies with a small versioned export/import contract and keep Unity MCP configuration out of the AI workspace.
- [ ] Verify training/reviewer commands from the new workspace and verify the Unity project still compiles against the retained runtime contract.

### Phase 146 handoff

- Created `tools/v25_narrative_training/HANDOFF.md` as the migration-safe authority that travels with the AI tooling directory.
- It records the exact SFT rejection evidence, Colab/Drive run, generator root causes, dirty-file boundaries, standalone/runtime classification, migration procedure and the first tasks for the new Codex session.
- Moved the verified workspace to `F:\01_Programming\01_Project\02_Unity\DungeonStoryNarrativeAI`; the handoff now lives at that workspace root and records the completed move.
- Added explicit sibling-content resolution to the standalone dataset builder, verifier and human-review merger so they no longer treat the AI workspace as the Unity content root.

### Phase 146 safety notes

- No files will be moved until the destination is resolved to an explicit absolute path and every moved source is confirmed to be inside the intended AI subtree.
- The active Codex thread keeps already-loaded MCP tools until a new session is opened; moving files or changing `.codex/config.toml` cannot detach MCP from this existing thread.

### Phase 146 error log

- The first parallel inventory batch was aborted because a PowerShell `foreach` block was piped directly to `Format-Table`, producing `EmptyPipeElement`. No files changed and the other read-only results were suppressed. The retry assigns rows to a variable before formatting and runs each independent inventory command sequentially.
- The first planning-ledger update expected the heading `# DungeonStory Findings`, but the file uses `# DungeonStory Current Findings`; the combined patch was atomically rejected. No planning file changed in that attempt, and the retry uses each exact document heading.
- The first copy-verification script used unavailable `[System.IO.Path]::GetRelativePath` on the installed PowerShell/.NET runtime and therefore reported synthetic missing paths. No source was removed; the compatible substring-based retry verified 103 files with zero mismatches before deletion.
- The first root-entry copy command again piped a `foreach` block directly into `Format-Table`, causing a parse-time `EmptyPipeElement`. Nothing executed; the retry collected rows before formatting and verified all three copied root files.
- The first standalone reviewer run passed the full corpus verification but failed one merger test because `apply_human_review.py` still used the AI workspace as the source-asset root. Adding the same explicit sibling `--content-root` contract made all six reviewer tests pass.

## Phase 145 - V25 SFT data remediation design (in progress)

- [ ] Trace BubbleLine schema contamination, Korean particle errors, malformed compound names, and repeated prose skeletons to their exact generator functions.
- [ ] Define deterministic generator changes, profile-isolated validation, dataset rebuild rules, and pre-training acceptance gates.
- [ ] Define the smallest retraining/quality sequence that avoids wasting A100 time or promoting a collapsed SFT candidate into DPO.

### Phase 145 error log

- The planning-session `git diff --stat` check invoked Git LFS clean filtering and failed on `.git/lfs/tmp/...` with access denied while examining an unrelated image. No files changed. Retry read-only status with LFS filters disabled and keep V25 inspection scoped to narrative tooling.
- The first scoped PowerShell `rg` command had an unterminated quoted pattern caused by an escaped `line\"` alternative. No search ran and no files changed. Split the file listing and fixed-phrase search into quoting-safe commands.

## V25 held-out QA browser injection note

- The first attempt to paste the Colab evaluation cell through an inline JavaScript template was rejected before browser execution with `Illegal Unicode escape sequence`. No Colab cell content or model artifact changed. The evaluation code is now stored as a repository Python script and will be injected from the file verbatim.
- The first smoke execution stopped before model loading with `BadGzipFile: Not a gzipped file (b've')`. The Colab clone contains a Git LFS pointer for the held-out archive, not the archive bytes. No model inference or QA artifact was produced; materialize that one LFS object and rerun.
- After materialization, the second smoke execution stopped before model loading with `KeyError: 'profile'`. The held-out record contract uses a different profile field name than the first evaluator draft assumed. Inspect the actual keys, patch the reusable evaluator, and rerun; no inference artifact exists yet.
- The first contract-inspection cell was appended to the preceding LFS verification cell by Colab editor focus behavior and produced a `SyntaxError` at `)import gzip`. It changed no files or model state. Reuse the explicit empty cell instead of the last editor locator.
- A read-only attempt to call `inputValue()` on Colab's browser locator failed because that locator wrapper does not expose the method. No page or file changed; use the known empty second-to-last cell identified in the DOM snapshot.

## Phase 144 retail-search note

- The combined SaleItem search returned exit code 1 because the optional resource filename filter ended without a complete type match; valid source output still identified the single modular-shop sale item path. No mutation occurred. Continue with exact `SaleItem` declaration and builder method.

## Phase 144 service-search note

- The combined service settlement search produced useful shop/meal findings but exceeded the direct output budget and was truncated. No mutation occurred. Further tracing will use exact revenue-event emission sites and bounded source ranges.

## Phase 144 work-authority search note

- A broad recipe-work search piped into `Select-Object -First` returned exit code 1 after the downstream consumer closed the pipe. It made no changes and repeated a known search anti-pattern. Continue with exact `ProductionRecipeSO` and V23 builder filenames only.

## Phase 144 inspection-output note

- The first material-profile search was too broad and its output was truncated after matching unrelated `UnitPrice` usages. It changed nothing. Exact authority files are now known: `MaterialEconomicProfileSO.cs` and `V23BalanceWorkCalculator.cs`.

## Phase 144 market-calibration feedback loop

- After market calibration, the audit reported widespread recipe-work mismatches. Compilation succeeded; the failure is a real circular authority: changing `UnitPrice` altered material economic profiles used by `V23BalanceWorkCalculator`, which changed calculated recipe work. Do not rerun the calibrator until `IntrinsicValue`/handling difficulty is decoupled from market price or the calibrated work is reauthored from a stable material authority.

## Phase 144 command-escaping note

- A targeted file read did not execute because the JavaScript workdir string contained an unescaped `\01` sequence and was rejected as an octal escape. No tool or file mutation occurred; reuse the fully escaped workspace path.

## Phase 144 builder-search note

- The builder search included a nonexistent optional `Assets/Scripts/Services/Equipment` directory and returned exit code 1 after yielding valid results from the rest of the tree. No mutation occurred; use the located Research and V22 builder files directly.

## Phase 144 targeted-search note

- A targeted combined search returned exit code 1 because the optional `ConfigureCore` pattern was absent from the crop builder; it still located `V23EmbeddedWorkValueCalculator.cs`. No files changed. Inspect that exact file and the seed asset creation method separately.

## Phase 144 market-audit recovery notes

- The first market audit correctly failed because 14 market-eligible definitions have no positive EWU: two unique/special items and twelve seed lots. This is a real authority mismatch, not a compile failure; they must be explicitly non-market or receive an acquisition-value authority.
- The first PowerShell summary regex did not match the generated `SALE_EWU` formatting and returned no parsed rows. It changed nothing; inspect a small exact sample before revising the parser.

## Phase 144 diagnostics added 2026-08-09

- A broad PowerShell search for `ResourceItemDefinitionSO` and surplus-sale policy exceeded the 10-second limit. It still returned the class-level `MarketSaleRate` evidence, but asset discovery was incomplete; continue with exact source paths and catalog audits.
- The first planning-log patch in this continuation expected an obsolete `## Errors / Recovery Log` heading and failed without changing files. Subsequent log updates anchor at the stable document titles.

## Phase 144 - Whole-game live balance calibration (in progress)

> Apply the authoritative theoretical baseline to live catalogs and runtime formulas, beginning with dependency-ordered economy/progression foundations and ending with cross-system simulation evidence.

- [ ] Inventory every current balance authority, generated catalog, formula, verifier, and report; classify each subsystem as measured, contradictory, incomplete, or missing.
- [ ] Establish a machine-readable baseline/audit layer that compares live facilities, items, recipes, equipment, apparel, research, agriculture, medicine, combat, events, factions, and milestones against the authority document.
- [ ] Rebalance upstream foundations first: time/labor, construction BOM/work, raw production, processing, logistics, maintenance, and recovery loops.
- [ ] Rebalance progression and survival: research pacing, food/water/temperature, agriculture/livestock, apparel, medicine, aging, population, and gold/service economy.
- [ ] Rebalance conflict and long-term systems: equipment, ammunition, defense, expeditions, encounters, captivity, factions, events, milestones, and EndlessAge pressure.
- [ ] Run deterministic economy/progression/combat/population simulations, remove dominant or positive-value loops, and regenerate authoritative evidence reports.
- [ ] Verify catalog coverage, Unity compilation, focused PlayMode scenarios, save invariants, and document the remaining human-playtest-only gates without overstating completion.

### Phase 144 fixed constraints

- Follow `docs/game-design/whole-game-balance-baseline.md` and the root `AGENT.md` balance gate for every changed value.
- Preserve research `180 / 138,824`, V20 pure-new `450`, save sections `68`, and other explicit content-count contracts unless the user separately changes them.
- Do not balance by research order, asset index, arbitrary rarity inflation, fake sinks, or abstract stock copies.
- Apply patches upstream to downstream; do not hide a broken input economy by changing only rewards, milestone dates, or encounter strength.
- Formula coverage and passing catalog validation are necessary but not sufficient evidence of balance.
- Preserve active Colab training and unrelated working-tree changes.

### Phase 144 errors

- After the finite-ammo recovery regression passed, the full outcome probe still reported the same 17 stalled samples. This proves the empty-weapon fallback itself works but is not the remaining stall source. Do not repeat that fix; inspect the exact stalled encounters and terminal objective conditions next.
- The first combined post-decoupling combat run passed the static 36-encounter audit but the outcome probe hard-failed on 17 stalled battles. Finite magazines exposed a real missing fallback: ranged-only combatants with empty magazines and no reserve repeatedly guard because they cannot reload, switch weapons or use unarmed attacks. The generated outcome report is diagnostic only and no balance value will be accepted until the stall path is fixed.
- The first probe-equipment lookup passed a wildcarded asset path directly to `rg`; the bounded source range succeeded, but the optional asset glob made the command exit 1. No files changed; equipment IDs were already confirmed in the builder and no retry is needed.
- The first offense-target asset search guessed nonexistent `Assets/Resources/SO/Offense` and optional V20 subpaths, so the second search exited 1 after the first command correctly located `Assets/Resources/SO/Content/OffenseCampaignCatalog.asset`. No files changed; subsequent inspection uses that exact catalog path.
- The first Unity refresh command referenced `CompilationPipeline` without a fully qualified name inside the MCP-generated `Unity.AI...` namespace, so it resolved as `Unity.CompilationPipeline` and failed dynamic compilation before execution. No project state changed; the retry uses `UnityEditor.Compilation.CompilationPipeline` explicitly.
- The first combat-diagnostics progress update expected the obsolete heading `# DungeonStory Progress Log`; the file uses `# DungeonStory Progress`, so the patch was rejected atomically. No progress or gameplay file changed in that attempt; the retry uses the exact heading.
- A broad tranquilizer search piped into `Select-Object -First` returned exit code 1 when the downstream reader closed after enough matches. It still exposed the exact ammunition profile and crossbow asset paths; no files changed. Further inspection uses those exact files without a truncating pipeline.
- A combined context-restoration read of `task_plan.md`, `progress.md`, and `findings.md` exceeded the direct output budget and was truncated after useful plan data. No project content changed; subsequent recovery and combat inspection use bounded ranges and exact files only.

- Converting authored daily procurement unit cost from int to float exposed two ShopFeatureSurfacePresenter locals typed as int (CS0266 at lines 269 and 402). The dynamic command again ran the previous assembly, so its truncated-price report was discarded. The UI calculations must preserve float unit cost and round only final gold totals.

- The first procurement-EWU audit source used `goldPerEwu` inside a method that already had a local array with the same name, causing CS0136 after Unity refreshed. The command had executed the previous compiled audit, so that report was discarded. The local was renamed before regenerating evidence.

- A targeted candidate-ID search guessed a standalone `PhysicalItemIds.cs` path that does not exist and used broad symbol alternatives over all scripts, producing noisy truncated output. No files changed. Exact item IDs were already established by the Unity catalog inspection; subsequent searches use their known asset paths directly.

- The first read-only Unity candidate inspection imported `System.Reflection`, which the MCP command sandbox forbids. It failed before execution and changed no assets. The retry uses `SerializedObject` over untyped Unity objects instead of reflection.

- The first focused market search used the guessed path `Economy/Planning/AutoProcurementRuntime.cs`; the file actually lives under `Economy/Treasury`. The companion repository-wide scoped search still found the relevant lines. No files changed; all further inspection uses the discovered exact path.

- A combined findings/progress patch omitted a terminating hunk context before the second file header and was rejected before changing either file. The retry applies each planning-file update as a separate valid hunk.

- Another planning-log patch omitted the required space in two `Update File:` headers and was rejected before any change. The corrected patch uses valid headers; no gameplay/source files were affected.

- A planning-log patch had a malformed `*** Update File:progress.md` header and was rejected before changing any file. The corrected patch uses the required space after `Update File:`.

- A parallel inspection batch was suppressed because the current generated report has no `LOW_WORK` rows yet and fixed-string `rg` returned exit 1. No files changed. The retry handles absent matches explicitly and inspects the `m06` asset independently.

- Three asset-field inventory commands used `rg -h`, which means help in the installed ripgrep version rather than "hide filenames"; they returned help text instead of recipe tags. No files changed. The retry uses `rg --no-filename` and fixed-string patterns.

- A compound helper-location search returned exit 1 because one optional symbol (`ConfigureRecipe`) does not exist, although it successfully returned all needed `RecipeSpec`, `Source*`, `CreateBatch` and `CreateRecipe` line numbers. No files changed; subsequent inspection uses those exact ranges instead of repeating the absent-symbol search.

- Disabling LFS filters through per-command Git config did not stop the existing Git LFS process filter; the read-only diff summary again timed out on `.git/lfs/tmp` access. No content changed. Further balance work will use scoped source/catalog inspection and `git status --short` rather than another whole-worktree diff until the LFS lock clears.

- The session catch-up `git diff --stat` triggered Git LFS clean-filter access to `.git/lfs/tmp` and failed with access denied on a generated image, suppressing the companion search outputs. No content changed; the retry disables LFS filtering for this read-only diff summary and runs source searches separately.

- A context-restoration range command treated multiple `Phase 143` matches as one line-number scalar, causing a PowerShell subtraction error and suppressing the parallel read output. No project content changed; the retry uses the first matching heading explicitly and separates catch-up from bounded reads.

- A planning-log patch assumed `findings.md` and `progress.md` began with generic `# Findings` / `# Progress` headings; their actual headings differ, so the patch failed without changing files. The retry will inspect only the first lines and patch against the real headings.

- Two initial bounded `rg` inventory commands produced useful output but exited 1 after `Select-Object -First` closed the pipeline. No files changed; subsequent audits will avoid this bounded-pipeline pattern or explicitly separate discovery from limiting.
- A targeted search included a guessed `Assets/Scripts/Services/Apparel/Editor` path that does not exist; other requested paths returned valid evidence. Directory discovery will precede subsequent scoped searches.
- A literal search for `new FacilitySpec(` was over-escaped as a regular expression and failed with an unclosed-group parse error. No files changed; fixed-string search will be used for this literal.
- The first Unity MCP regeneration command assumed `ResearchOverhaulContentAssetBuilder` lived in `DungeonStory.Services.Research.Editor`; the editor builder is intentionally in the global namespace, so the dynamic command failed to compile before execution. No project assets changed; retry without the namespace import.
- A parallel negative-ID/`BuildingSO` path discovery batch returned exit 1 without useful output because the `rg --` argument placement caused the file globs to be treated incorrectly and the filename filter found no exact suffix match. No files changed; rerun as separate, simpler fixed-string/path queries.
- A PowerShell `rg` call passed `*` wildcards inside literal Windows path arguments while checking research-overhaul item IDs; Windows rejected those path strings. Partial unrelated rune-item output was ignored. No files changed; query the containing directory and filter filenames instead.
- A combined modular-builder symbol search again over-escaped a literal parenthesis and failed with an unclosed-group regex error; the parallel batch suppressed the companion snippet output. No files changed; use fixed-string searches or inspect the known line range independently.
- A P1 defense builder symbol search passed `new(` to `Select-String` without `-SimpleMatch`, causing another unmatched-parenthesis regex error and suppressing its parallel snippet. No files changed; retry with exact known ranges and simple literal patterns.
- A duplicate-helper check searched `RoundTo(` with an over-escaped regex and failed; no files changed. Use `rg -F` for literal method names containing parentheses.


## Phase 143 - Authoritative balance baseline and agent enforcement (completed)

> Persist the approved whole-game theoretical balance framework in one authoritative project document and require future content/value changes to consult and validate against it.

- [x] Audit the existing root `AGENT.md` instruction structure and current design-document authority boundaries.
- [x] Create one dedicated balance baseline authority covering common units, target bands, subsystem rules, validation matrices, and change-control expectations.
- [x] Add a concise mandatory workflow to `AGENT.md` for new or changed facilities, items, recipes, equipment, apparel, research, events, factions, combat encounters, milestones, and balance values.
- [x] Link the new authority from the main game-design document without duplicating ownership.
- [x] Verify links, terminology, Markdown structure, and scoped diffs; record completion evidence.

### Phase 143 enforcement hardening - 2026-08-09

- [x] Add the conventional root `AGENTS.md` auto-discovery entrypoint without duplicating the full authority in `AGENT.md`.
- [x] Require future gameplay-content and numerical changes to consult the balance baseline and produce mandatory balance evidence before completion claims.

### Phase 143 fixed constraints

- `docs/DungeonStory_Game_Design_and_Implementation.md` remains the overall game-design authority; the new document is the detailed numerical/theoretical balance authority.
- `AGENT.md` must require consultation and evidence, not merely recommend it.
- New gameplay content may not bypass physical BOM, work, execution, save ownership, cross-system trade-offs, or balance audit coverage.
- Generated reports are evidence, not hand-edited authority.
- Preserve active Colab training and unrelated working-tree changes.


## Phase 142 - Theoretical whole-game balance framework (completed)

> Establish a coherent theoretical baseline for every gameplay economy before changing live values: time, labor, resources, risk, recovery, information, progression, combat, social systems, generations, and endless pressure.

- [x] Inventory the current authoritative time, labor, economy, research, combat, survival, event, faction, milestone, apparel, quality, and performance contracts.
- [x] Define common balance currencies and reference units that let different systems be compared without flattening their distinct roles.
- [x] Define target bands by run phase and difficulty, including expected surplus, failure pressure, recovery cost, variance, and player decision cadence.
- [x] Specify subsystem formulas, cross-system exchange rates, anti-dominance constraints, and exploit-prevention invariants.
- [x] Specify deterministic simulation matrices, telemetry, acceptance thresholds, and a safe order for applying future value patches.
- [x] Deliver a complete design proposal and record the approved balance authority boundary.

### Phase 142 fixed constraints

- This phase designs theoretical baselines only; it does not silently rebalance live content values.
- A shared reference unit may compare costs, but each system must preserve its own qualitative trade-offs and physical resource constraints.
- Balance targets use bands and distributions, not a single exact outcome or guaranteed win rate.
- All assumptions must be testable by deterministic probes and later human playtests.
- Existing active Colab training and unrelated working-tree changes must be preserved.

### Phase 142 errors

- The first context-restoration command used the skill documentation's legacy `.claude` script path, but this installation is under `.codex`; the planning files were read successfully before the missing-script error. The retry uses the actual installed skill path and does not repeat the bad path.

## Phase 141 - Gameplay integration and balance evidence audit (completed)

> Determine whether the implemented systems are actually exposed through normal gameplay and whether current balance claims are supported by runtime evidence rather than formulas alone.

- [x] Inventory authoritative balance validators, probes, PlayMode scenarios, and generated balance reports.
- [x] Compare completed evidence against the design document's explicit remaining product gates.
- [x] Identify unverified cross-system loops, economy exploits, progression pacing, combat outcomes, and player-facing UX gaps.
- [x] Produce a prioritized completion verdict and the smallest next validation program.

### Phase 141 fixed constraints

- Do not equate catalog completeness, formula assignment, or deterministic unit tests with fun or live balance.
- Do not change gameplay values during this audit; report evidence and gaps first.
- Preserve the active Colab SFT run and unrelated working-tree changes.

## Phase 140 - Colab Pro full SFT migration (in progress)

> Move the V25 QLoRA workload away from the unstable local Windows GPU path, connect Google Drive checkpoints, validate that the exact training pipeline enters real optimization, and run the explicitly authorized full SFT with visible progress.

- [x] Verify the signed-in Colab session and select a supported L4/A100-class GPU runtime.
- [x] Add a reproducible Colab notebook that fetches only the required Git/LFS corpus and writes all checkpoints/evidence to Google Drive.
- [x] Run environment, dataset, CUDA capability, and storage preflight checks.
- [x] Run the corrected canary long enough to verify model download, 4-bit loading, GPU allocation, and sustained optimization; stop it at the user's explicit request before checkpoint-20.
- [x] Obtain the explicit full-run decision and launch the 38,000-record × 2 epoch SFT with unbuffered visible output in a clean Drive directory.
- [ ] Monitor full-run checkpoints and verify final adapter/training evidence when the run completes.

### Phase 140 fixed constraints

- Never upload or resume the corrupt local `checkpoint-20`.
- Do not use a T4 runtime; require compute capability 8.0 or newer.
- Keep canary and full-run outputs separate so test scheduler state cannot contaminate the real run.
- Persist checkpoints and training evidence under Google Drive, not ephemeral `/content` storage.
- Do not begin the full SFT merely because the canary passes; Colab compute consumption remains an explicit user decision.
- The user explicitly authorized the full SFT on 2026-08-09 after the corrected canary sustained real training for more than 26 minutes.

### Phase 140 errors

- The first automated click on Colab's Drive authorization button found no matching control because the transient prompt disappeared between snapshot and action. No permission choice was made by that failed click; the next step inspects the running cell and any newly opened authorization surface instead of repeating it blindly.
- Re-inspection of the still-running Drive mount caused two browser-control timeouts while the Colab runtime itself remained alive. Because Google requires an account-scoped Drive consent action and the transient consent control is no longer exposed to automation, the user must stop/re-run cell 2 and press `Google Drive에 연결` once.

- The first canary click created its output directory before visible output appeared. The later forced retry reused that path and correctly failed closed with `FileExistsError`; no training or checkpoint write occurred. The notebook now selects a new `-retryN` directory without deleting or overwriting the earlier path.

## Phase 139 - Repair and publish the unpushed main commit (complete)

> Preserve local training artifacts, remove derived checkpoints that violate GitHub blob limits from Git tracking, repair ephemeral broken refs, and publish the existing work to `origin/main`.

- [x] Audit the unpushed commit, oversized artifacts, ignore rules, and broken refs.
- [x] Keep local training outputs on disk while removing `Artifacts/Training/V25/models/` from Git tracking.
- [x] Repair broken `refs/codex/turn-diffs/checkpoints/*` refs and amend the unpushed commit.
- [x] Validate repository integrity, confirm no oversized non-LFS blobs remain, and push `main`.

### Phase 139 completion evidence - 2026-08-09

- Git connectivity and LFS integrity pass; ordinary Git blobs at or above 50 MiB in the published range are zero.
- All 41 local training-model files (602,705,393 bytes) and nine Python cache files remain on disk while none are tracked.
- The repaired push uploaded 40 LFS objects (about 1.4 GB) and advanced `origin/main` from `d4c49395` to `60b7bebe` without a remote rejection.

### Phase 139 fixed constraints

- Do not delete local training outputs or the mounted release GGUF.
- Do not rewrite any remote history; only amend the single local commit that GitHub already rejected.
- Keep the release GGUF and supported binary artifacts under Git LFS.
- Back up broken ephemeral refs before removing them from Git's active ref namespace.

### Phase 139 errors

- `session-catchup.py` read the existing planning files but failed while printing recovered context because the Windows CP949 console could not encode U+2014. This does not affect repository data; subsequent work uses direct repository inspection.
- Two diagnostic PowerShell commands initially failed at parse time because a `foreach` statement was piped without expression grouping. The corrected command captured the large-blob and ref evidence; no repository state changed in either failed attempt.
- The first Python-cache untrack command passed a PowerShell pipeline to `--pathspec-from-file=-`, which introduced a BOM into the first path and failed without removing caches. The retry passed the nine explicit paths as native arguments and succeeded.
- Initial ref cleanup assumed Git's reported zero SHA was the file content; direct inspection showed both refs target valid objects. The actual defect was the 270-character Windows ref path. No ref was moved on the failed attempt; the corrected validation backed up and moved both overlong refs.
- A subsequent read-only ref-inspection command hit the same PowerShell `foreach` pipeline parse rule; the grouped retry succeeded without repository changes.
- Whole-range `git diff --check` reports existing trailing spaces in generated Unity YAML/meta files and the vendored VContainer package. These are unrelated to the Git publish repair and are not bulk-rewritten; repository/LFS integrity and oversized-blob checks pass independently.

## Phase 138 - Mount the current base narrative model (complete)

> Integrate the untrained Qwen3-1.7B base model as the current offline narrative expression model, without resuming GPU training, while preserving deterministic rule fallbacks and V25 structured-output safety.

- [x] Audit the existing V25 host, backend registration, model-file format, native binary, runtime settings, and build packaging gaps.
- [x] Prepare a verified inference-ready model artifact and manifest without using the corrupt SFT checkpoint.
- [x] Connect automatic offline host startup, capability/fallback status, and model selection to the normal game composition path.
- [x] Add focused lifecycle, missing/corrupt model, structured output, and fallback validation.
- [x] Verify Unity compilation and a real base-model request without starting any training workload.

### Phase 138 fixed constraints

- Never load or resume `checkpoint-20`; the current model means the clean Qwen3-1.7B base model.
- Inference may use the GPU only if explicitly enabled and must default to a conservative CPU-safe configuration after the training BSOD.
- LLM failure, missing files, timeout, or unsupported platform must preserve mechanically complete rule-based gameplay.
- The model is an expression layer only; rules, legal evolution candidates, effects, costs, and facts remain C# authority.
- Release integration must not depend on Ollama or an external network service.

### Phase 138 completion evidence - 2026-08-09

- Mounted the official `ggml-org/Qwen3-1.7B-GGUF` Q4_K_M base model (`1,282,439,264` bytes, SHA-256 `d2387ca2...d9bc7b5`) and the official llama.cpp `b10331` Windows CPU host under `Assets/StreamingAssets/DungeonStoryLlm`.
- The manifest records `trainingState=base-untrained` and `releaseCertified=false`; the corrupt `checkpoint-20`, adapters, CUDA, Ollama, and external runtime APIs are not used.
- The normal `LocalLlmRequestQueue` now waits for background hash/model startup instead of failing early requests, exposes starting/running/version/certification state, uses loopback bearer authentication, and retains the existing deterministic fallback on every failure path.
- First runtime smoke exposed a real readiness defect: an open TCP port still returned `503 Loading model`. The launcher now waits for authenticated `/health` HTTP 200 before publishing the endpoint. The failed run cleaned the child process (`0 -> 0`) and the corrected run passed.
- Unity 6000.3.8f1 compiled with exit 0; V25 inference contracts passed `8/8`; the real CPU structured-generation smoke passed in `14,944 ms` with valid `F01/M01` JSON and zero remaining `DungeonStoryLlmHost` processes.
- This completes the current Windows development mount. Fine-tuned prose quality, held-out release certification, and a native Linux/Steam Deck host remain later release gates rather than hidden claims of this base model.

## Phase 137 - Reviewer semantic UX, hard-negative repair, and SFT handoff (in progress)

> Make human review readable without exposing raw JSON as the primary surface, replace the repeated trivial DPO negative with deterministic hard negatives, and establish the correct SFT-before-DPO training handoff from evidence on the local machine.

- [x] Replace raw candidate JSON with profile-aware semantic cards and field-level highlighting; keep raw JSON only as an advanced disclosure.
- [x] Replace the page-scrolling review layout with a desktop viewport workspace whose context and candidate panes scroll independently, plus a mobile fallback.
- [x] Replace raw rewrite editing with candidate-based prose-field forms while retaining strict server-side JSON/mechanic/reference validation.
- [x] Replace the single repeated rejected phrase with stratified, varied hard negatives; regenerate and verify all V25 corpus/review artifacts reproducibly.
- [ ] Audit local GPU/training dependencies, add or complete the reproducible SFT entry point, and run the feasible SFT validation/training stage before DPO handoff.

### Phase 137 current evidence — 2026-08-09

- The rebuilt 50,000/40,000/38,000/8,000 corpus passes validation; a second clean build matches 19/19 files byte-for-byte.
- The rejected fixed fallback appears zero times. Hard negatives are split across `generic_safe`, `fact_distortion`, and `motif_listing`; the 8,000-row reviewer now reports 2,347 real unknown-reference warnings and zero manufactured fixed-cliche warnings.
- Reviewer backend/static suite passes 6/6 and JavaScript syntax validation passes. Profile-specific rendering, prose-only rewrite forms, advanced raw JSON, and fixed desktop panes are present.
- RTX 4080 Laptop 12GB and CUDA 12.6 are available. The pinned Python 3.11 environment imports PyTorch 2.7.1, Transformers 4.53.3, TRL 0.19.1, PEFT 0.16.0 and bitsandbytes 0.46.1 successfully.
- The 64-record/two-step QLoRA smoke completed on the RTX 4080: global step 2, loss 2.699822, 108,930 trained tokens and mean token accuracy 0.610006. The full 38,000-record/two-epoch SFT remains the current long-running stage; no final SFT model is claimed until its own `training_evidence.json` exists.
- The full SFT was restarted with a 20-step checkpoint interval, but Windows bugchecked at step 20 and the run is now deliberately stopped. Event Viewer records `0x1E / c0000005`, an NVIDIA UVM error, and `GPU recovery ... Node Reboot Required`; the coincident checkpoint has readable adapter tensors but a corrupt optimizer archive and no trainer/scheduler/RNG state, so it is not resumable evidence. Do not restart full training until the GPU-driver/thermal path and external-USB output path are remediated.

### Phase 137 fixed constraints

- The chosen SFT completion remains grounded and is never replaced by a review negative.
- Rejected candidates must be useful preference contrasts, not a repeated phrase that makes DPO trivial or encourages mode collapse.
- Reviewer edits expose prose fields only by default; rule-owned numeric/enumerated fields remain read-only and server-validated.
- Existing human decisions must not be silently overwritten during a corpus rebuild. A rebuild is permitted only while production reviewer state/export are absent, otherwise an explicit migration is required.
- Human review supplies preference labels after the SFT candidate exists. DPO is never run on synthetic system preference alone.

## Phase 136 - Local narrative review workbench (implementation complete; visual browser check pending)

> Replace direct CSV editing with a dependency-free localhost review UI that preserves the CSVs as exchange artifacts while making 8,000-row review practical and resumable.

- [x] Audit the review CSV/key/schema/merge contracts and choose a non-destructive persistence/export boundary.
- [x] Implement a localhost-only Python server with indexed records, atomic autosave/resume, progress/distribution APIs, deterministic warning analysis, similarity clusters, filtering, and bounded bulk actions.
- [x] Implement a responsive Korean review UI with readable facts/motifs, side-by-side A/B candidates, keyboard shortcuts, rewrite/drop flows, automatic highlights, filters, progress, and batch controls.
- [x] Add export/merge compatibility, launch documentation, and focused backend tests without pre-filling or mutating the original review CSVs.
- [ ] Run local browser interaction checks for navigation, shortcuts, autosave/reload, filters, bulk confirmation, rewrite, export, responsive layout, and console/network cleanliness.

### Phase 136 current evidence — 2026-08-09

- Python/HTTP/static contract suite passes 6/6. It covers immutable source hashes, indexed warnings/clusters, autosave/restart, verdict-preserving drafts, undo, bounded confirmed bulk actions, rewrite schema/mechanic/reference protection, 8,000-row export, actual merge-tool compatibility, token rejection, CSP, unique DOM IDs, keyboard bindings and responsive CSS.
- JavaScript syntax validation passes with `node --check`.
- No production `reviewer_state.json` or `reviewer_export.csv` was created during tests; all mutable test state used temporary paths and was removed.
- In-app browser discovery returned zero available browser bindings, so pointer screenshots and live responsive visual evidence remain pending without blocking use of the implemented tool.

### Phase 136 fixed constraints

- The eight original review CSVs remain immutable inputs. Autosave writes a separate atomic state file; export produces a merge-ready CSV explicitly.
- The server binds to loopback only, serves no third-party resources, and does not upload narrative data.
- Bulk operations require an explicit filtered scope, confirmation, and a bounded record count; they remain reversible through per-record history during the session.
- Automatic warnings assist review but never count as human approval. Only explicit A/B/REWRITE/DROP actions change review state.
- The held-out split remains identifiable and exportable without being mixed into training automatically.

### Phase 136 errors

- Local browser selection returned `No browser is available`; troubleshooting discovery confirmed an empty browser list. Do not substitute an unrelated browser-control surface. Backend, HTTP, static DOM/CSP and responsive-contract tests continue locally, while visual pointer verification remains pending until a browser binding is available.
- Static UI contract test attempt 1 found `bulkLaunch` was created dynamically and therefore absent from the HTML ID inventory. The control is now authored in HTML and only populated/enabled by JavaScript, improving accessibility and testability.
- Browser-test server cleanup stopped the verified reviewer PID, but the first directory removal raced the redirected stderr handle and left that single log file. Retry only after confirming the listener and reviewer process are gone.

## Phase 135 - V25 narrative training corpus and human-review package (complete)

> Build a copyright-safe, game-grounded corpus from authoritative DungeonStory content and researched linguistic/cultural references, then emit deterministic raw, filtered, and human-review artifacts.

- [x] Audit the existing V25 schemas, training configuration, game-content authorities, artifact policy, and repository cleanliness before generation.
- [x] Research primary/public reference sources for Korean language, folklore, classical/martial vocabulary, Qwen training format, and source licensing; record use boundaries and provenance without copying modern fiction prose.
- [x] Implement a deterministic scenario generator and quality filter for exactly 50,000 raw scenarios and approximately 40,000 SFT-ready records.
- [x] Produce a stratified 8,000-record human-review package, including a 6,000-record preference/training pool and a leakage-isolated 2,000-record evaluation pool.
- [x] Verify schemas, counts, hashes, split isolation, grounding, duplicate rates, copyright/source rules, and reproducibility; document commands and review workflow.

### Phase 135 completion evidence — 2026-08-09

- Generated 50,000 raw scenarios, selected 40,000 grounded candidates, emitted 38,000 SFT candidates, and prepared 6,000 preference plus 2,000 held-out review rows.
- All 8,000 review rows are blank and split across eight UTF-8-BOM CSV files; no human approval is claimed.
- Full validation passes with zero held-out family leakage, fixed rule-field preservation, valid request-local references, existing source-asset provenance, and 18 manifest-tracked files.
- Corpus audit passes with 100% Korean prose coverage, zero selected generic fallback phrases, and 0.9934% exact duplication among prose fields of at least 40 characters.
- An independent same-seed rebuild produced 19/19 byte-identical files with zero missing files and zero SHA-256 mismatches.

### Phase 135 fixed constraints

- Internet sources inform taxonomies, vocabulary classes, style constraints, and licensing only. The corpus must not reproduce passages from copyrighted fantasy or martial-arts fiction.
- Every generated claim must be grounded in a supplied DungeonStory fact packet; invented names may identify only generated scenario actors and may not impersonate source authors or franchises.
- The 2,000-record evaluation split is selected before template/output expansion by stable scenario-family hash and may not leak into SFT or DPO training data.
- Human review remains a real user action. Generated files expose blank verdict, rewrite, issue-tag, and reviewer-note fields and must never mark records as human-approved in advance.
- All generated files are deterministic from a versioned configuration and seed, with SHA-256 manifests and exact record-count validation.

## Phase 134 - V25 dedicated narrative model and safe local inference (runtime complete; release artifacts pending)

> Replace the release Ollama dependency with a fail-closed DungeonStory host boundary, context-affinity scheduling, rule-authoritative historical evolution, and reproducible model-training/quality gates.

- [x] Add the release host protocol and fail-closed Unity launcher: Windows Job Object, lifetime EOF, asynchronous heartbeat/log drain, singleton ownership, binary/model hashes, and rule-only fallback.
- [x] Replace FIFO/priority-only narrative dispatch with deadline-aware prefix-affinity scheduling and bounded single-request multi-perspective contracts.
- [x] Add static choice grammars, canonical prompt enforcement, validated equipment-candidate selection, and deterministic rule fallback.
- [x] Replace string-derived equipment history with typed evidence and split mechanical unlock, narrative readiness, and UI visibility.
- [x] Add reproducible SFT/DPO dataset contracts, training configuration, diversity evaluation, and GGUF release-manifest gates without checking in a fabricated model.
- [x] Compile and run focused V24/V25 contracts, update the design document from evidence, and record native binary/model gates separately.
- [ ] Build and certify the llama.cpp-backed Windows/Linux `DungeonStoryLlmHost`, including host-side PDEATHSIG/process-group/lifetime/heartbeat and tokenizer grammar self-tests.
- [ ] Train, review, evaluate, quantize, license-check, and hardware-certify the dedicated Qwen3 1.7B model before packaging it into StreamingAssets.

### Phase 134 fixed constraints

- Rules own every legal action/effect, numeric value, cost, condition, and state transition. The LLM may only express history or select an index from rule-authored legal equipment candidates.
- Release builds may not require Ollama or an external API. If the signed native host/model is absent, invalid, busy, or unsafe, gameplay continues through deterministic rule prose and rule-ranked choices.
- Windows host launch is fail-closed behind a kill-on-close Job Object. Linux uses parent-death signal, process group, lifetime EOF, and heartbeat; no uncontained host fallback is permitted.
- Lifetime EOF and heartbeat are independent channels. Heartbeat permits one outstanding asynchronous write and may never block Unity's main thread.
- Request schemas and choice grammars are static. Candidate IDs, character facts, and events must never generate request-specific grammar.
- Existing 68 save sections remain; leases, KV caches, host handles, and scheduler caches are rebuilt after restore.

### Phase 134 external artifact boundary

- The repository can implement and verify host/source/build/training/release tooling, but a fine-tuned Q4_K_M model can only be marked present after training, conversion, hashing, licensing review, and hardware acceptance actually pass.

## Phase 133 - V24 static structured narrative generation (authoritative)

> Replace prompt-only JSON with profile-static Ollama schemas, project visible character facts into request-local references, and accept grounded soft-pass prose without dynamic grammar generation.

- [x] Audit all nine local-LLM profiles, DTOs, prompt builders, persistence owners, and current fallback paths.
- [x] Add byte-stable static schemas and switch the queue to Ollama `/api/chat` structured output with capability/diagnostic reporting.
- [x] Add deterministic culture motifs, request-local fact references, narrative context projection, and C# authoritative quality validation.
- [x] Connect persistent naming/history generators and transient prose profiles without regenerating existing names.
- [x] Add focused schema/hash/context/quality/backend tests, compile, run live Ollama probes, and update documentation from passing evidence.

### Phase 133 fixed constraints

- Exactly one static schema per profile/version; no request data may alter schema bytes or schema hash.
- Request-local `Fxx`/`Mxx` references remain plain strings in the schema and are validated/resolved by C#.
- Unrevealed latent traits and facts outside the speaker/player knowledge boundary may never enter prompts or persisted traces.
- Style weakness is a soft-pass concern; only structural/domain/grounding violations cause hard rejection and retry.
- Preserve all user-owned worktree changes and the existing 68 save-section contract.

### Phase 133 errors

- Unity dynamic refresh command attempt 1: `CompilationPipeline.RequestScriptCompilation` resolved as `Unity.CompilationPipeline`; use `UnityEditor.Compilation.CompilationPipeline` explicitly.
- The first full import exposed an invalid AI-to-main-assembly dependency. Static reference/context/quality/trace contracts were moved into `DungeonStory.AI`; only aggregate projection and bootstrap remain in the main gameplay assembly.
- The first hidden-latent isolation fixture used default ID structs whose null backing values made `IsValid` throw. The fixture now supplies explicit empty IDs and the production projection continues to expose only revealed latent traits.
- The first live-probe Editor compile imported the new source without rebuilding, then the forced Unity compilation exposed an ambiguous `Debug` symbol from `System.Diagnostics`. The probe now aliases only `Stopwatch`, leaving logging explicitly under `UnityEngine.Debug` resolution.
- The first actual nine-profile smoke probe found one malformed `CharacterSkill` schema delimiter and proved that stable fact IDs were visible to the model. The schema is structurally validated at catalog startup, the missing delimiter is fixed, and the Ollama boundary now rewrites internal `Fxx|stableId|label` metadata to model-visible `Fxx = label` lines.
- The first 20/profile acceptance run produced 0 parse errors but passed 167/180. Twelve of thirteen failures came from optional `BubbleLine` outputs filling nonexistent F02-F04 tokens; its profile-static schema now intentionally exposes only `line`, while grounding remains optional by contract. The remaining CharacterRecord miss is retested with a realistic four-fact context rather than a one-fact synthetic edge fixture.
- A broad recursive `%LOCALAPPDATA%` log discovery timed out without changing project files. Ollama liveness was verified through `/api/ps` and direct `llama-server` CPU deltas instead; subsequent log inspection uses the known `%LOCALAPPDATA%/Ollama` path.
- The second 20/profile run reached 179/180 accepted with one SocialRumor response truncated before its closing brace at the inherited 256-token cap. SocialRumor now uses a 384-token ceiling; structured generation still stops on schema completion. The final run also includes centralized profile-specific style intensity guidance.
- The third 20/profile run again reached 179/180; all prior SocialRumor/Bubble failures were eliminated, but one complex FacilityEvolution response hit its inherited 256-token cap. FacilityEvolution now uses 768 tokens, matching CharacterSkill's complex structured-output budget, and the live fixture supplies one bounded proposal so the test measures grammar rather than unconstrained list expansion.

### Phase 133 current evidence — 2026-08-08

- Unity rebuilt `DungeonStory.AI`, `DungeonStory.Evolution`, `Assembly-CSharp`, and `Assembly-CSharp-Editor` after the V24 integrations with no compiler errors.
- The focused V24 Editor suite passes 6/6: nine fixed schema profiles, 10,000-context hash stability, no dynamic reference enums, warm schema lookup 0B, native `/api/chat` body shape, deterministic F/M references, hard/soft/strong quality outcomes, and hidden latent-fact isolation.
- Final local-model acceptance passed with `llama3.1:latest`: 179/180 accepted (99.4%), parse failures 0, fallbacks 1/180 (0.6%). All persistent/naming profiles and BubbleLine passed 20/20; the one SocialRumor fallback was a correctly hard-rejected use of F01 as a target CharacterId.
- Profile TTFT medians were 697.1–700.1 ms and p95 values 698.8–897.1 ms in the final 20/profile run. The generated report is `Artifacts/QA/v24-narrative-live-probe.txt`.
- Documentation now defines V24 as the current narrative authority. The final clean Unity capture also passed: focused suite 6/6, Console Error 0 / Warning 0.

## Phase 132 - Player-facing gameplay UI, debug-mode separation, and presentation (authoritative)

> Replace mixed debug/gameplay surfaces with coherent player-facing flows. Debug-only controls and diagnostics must be hidden by default and become visible only when the persisted Debug Mode option is enabled.

- [x] Inventory every runtime gameplay panel, debug control, debug scene root, overlay, menu command, and settings persistence path; classify player, advanced, and debug-only surfaces.
- [x] Add one persisted presentation/settings authority for Debug Mode and expose it through the normal options UI without adding a save section.
- [x] Gate all in-game debug-only controls, diagnostics, test launchers, identifiers, and `__Debug` scene presentation through that authority while retaining Editor-only menu tooling.
- [x] Rework construction, production, equipment, apparel, character, medical, research, faction, expedition, and alert flows into consistent player-facing hierarchy, copy, feedback, empty/loading/blocked states, and progressive disclosure.
- [x] Add lightweight presentation choreography for panel open/close, section reveal, confirmation, success, warning, and blocked feedback without changing gameplay authority or adding sound.
- [x] Compile, run focused UI/debug visibility scenarios, verify both required resolutions and pointer paths, then update the design document only from passing evidence.

### Phase 132 completion evidence — 2026-08-08

- Unity MCP PlayMode report: `Artifacts/QA/debug-mode-playmode-report.txt` — all checks PASS, Console Error 0 / Warning 0.
- Pointer-driven settings → Debug Mode → palette → command/target/overlay → disable flow passed at `1600×900`; portrait bounds and capture passed at `900×1600`.
- Both authored `__Debug` and runtime `__Runtime/Debug` roots, plus the character AI diagnostics tab, were hidden by default, revealed by the persisted option, and hidden again when disabled.
- Unity/Bee Roslyn response compilation passed for Presentation, Assembly-CSharp, Assembly-CSharp-Editor, and Architecture.Tests.

### Phase 132 fixed UX rules

- Debug Mode defaults off in player builds. It may reveal diagnostics and test controls, but may never change simulation rules, random outcomes, saves, or command validation.
- Stable IDs, raw enum names, stack/order IDs, developer counters, validation launchers, and direct state mutation belong to Debug Mode; actionable failure causes and player-relevant numbers remain visible normally.
- Primary action, current state, cost, requirements, expected consequence, and cancellation must be readable without Debug Mode.
- Advanced worker/material/quality policies use progressive disclosure rather than cycling opaque presets or flooding the default panel.
- Presentation uses the existing runtime-created UI system and theme; no sound, external art dependency, or animation-rig rewrite is introduced.

## Phase 131 - V23 grade-free materials, worker policies, and quality-target pipelines (authoritative)

> Implement the approved V23 new-game-only vertical while preserving the fixed
> 180-research / 138,824-work, V20 450-definition, and 68-save-section contracts.
> Existing V21/V22 worktree changes are user-owned and must be preserved.

- [ ] Replace quality-bearing stackable materials with definition/state-only stacks; keep only finished facility/apparel/combat-equipment craftsmanship quality.
- [ ] Add material economic profiles and deterministic work calculators, then rebalance all catalogued player buildings, production recipes, combat equipment, and apparel without index-based placeholder costs.
- [ ] Add persistent worker-selection policies, eligibility queries, weighted contribution ledgers, and assignment UI across construction, production, apparel, equipment, repair, alteration, and dismantling.
- [ ] Add save-stable quality rolls and bounded/unlimited quality-target pipelines with physical reject disposition, dismantling, salvage, and same-footprint facility rebuilds.
- [ ] Update V23 save-generation rejection, UI/documentation/reporting, and focused domain/PlayMode/performance validation.
- [ ] Pass the final 68/68/68 canonical round trip with atomic failure and Unity Console Error 0 / Warning 0.

### Phase 131 fixed constraints

- Stackable ingredients, seeds, fibres, yarn, textiles, timber, ingots, leather, medicines, and components have no quality tier or continuous quality score.
- `Ready/Wet/Contaminated`, genome/pathogen, durability, and unique-component state remain where physically meaningful.
- Expensive/difficult materials increase required work but never directly improve the craftsmanship roll.
- Quality rerolls consume real work and materials; every attempt has a persisted deterministic random component and cannot be save-scummed.
- Default repetition uses attempt/reserve/work-budget safety limits; `UnlimitedUntilSuccess` remains an explicit player option and must idle safely at blockers.
- Every player-buildable catalog entry has an authored or classified BOM, work amount, and real execution role; runtime archetypes are excluded.

### Phase 131 current evidence — 2026-08-08

- Unity 6 Roslyn compiles Foundation, Buildings, Work, Production, Combat, Items, Economy, main, and Editor assemblies with zero compiler errors.
- Current authoritative asset counts are 368 player-building assets, 354 production recipes, 61 combat equipment definitions, and 56 apparel definitions; all 368 building assets serialize a construction BOM.
- Stackable textile and seed state no longer serializes material quality. Facility, apparel, and combat-equipment craftsmanship use the shared seven-tier deterministic quality resolver.
- Construction, production, apparel, and combat equipment persist worker policies and weighted contributor ledgers. Facility, apparel, and equipment quality targets now release input reservations when no eligible worker exists or the target is theoretically unreachable.
- The 24 production-network omissions are mapped to their real typed runtime command owners rather than synthetic sink recipes.
- Remaining completion evidence is the live V23 focused menu run and generated appendix, interruption PlayMode verticals, the full economic-cycle proof, UI pointer captures, and V23 68/68/68 with Console 0/0.

## Phase 130 - V22 anatomy/material-lineage apparel and textile industry (authoritative)

> Implement the approved V22 apparel vertical without changing the fixed
> 180-research / 138,824-work, V20 450-definition, or 68-save-section contracts.
> V22 is a new-game-only save generation and must preserve physical-item,
> transactional restore, and authored-content authority.

- [x] Freeze the V22 anatomy, apparel, textile-quality, finite-batch, reservation-lease, safe-recovery, save-generation, and compatibility-adapter contracts in code.
- [x] Add the immutable apparel/textile definitions and the mutable `CharacterApparelAggregate`, material projection cache, attachment query, indexed AI selection, and physical item components.
- [x] Implement atomic craft/equip/alter/launder/dry/repair/recover workflows, bounded reservation leases, starter underwear/supplies, and the environmental-workwear compatibility adapter.
- [x] Author and register 56 apparel definitions, 10 textile materials, four fiber crops with 12 genomes/cultivars, three husbandry outputs, 14 facilities, recipes, and reverse research rewards while keeping 180/138,824.
- [x] Connect functional UI, V22 capture/restore within the existing 68 sections, V21-and-earlier rejection, and deterministic content/link validation.
- [ ] Run focused domain/compile/PlayMode/performance/save validation, update the comprehensive document only from passing evidence, then run the final 68/68/68 and Console 0/0 gate.

### Phase 130 current evidence — 2026-08-08

- Focused V22 apparel/content contracts pass against the authoritative domain catalog: 56 apparel, 10 woven plus 2 non-woven materials, 12 total crops, 32 total genomes, 14 facilities, and 89 recipes.
- Research/equipment validation passes at 180 projects and total work 138,824; the V22 facilities extend the research-facility reward count to 115 without adding research nodes.
- The current-code full-world PlayMode round trip passes 68/68/68 with canonical baseline match, live baseline restoration, and Console Error 0 / Warning 0.
- Remaining acceptance evidence is the 2,000-agent allocation/CPU profile, ten-year lot-growth simulation, full apparel vertical PlayMode matrix, and both-resolution pointer-flow capture. The broader pre-existing V21 production graph also still reports 24 fake-consumer debts outside the V22 focused slice.

### Phase 130 fixed acceptance constraints

- No species allowlist: attachment points, prostheses, size, layer occupancy, and alterations alone decide equip legality; slime is humanoid and only golem is construct.
- Textile stacks merge by item, four-tier quality, and three-band condition only; ancestry is bounded to 64 recent records per material and compressed thereafter.
- AI receives at most eight indexed candidates and performs no normal per-decision full-stock scan or steady-state allocation.
- Apparel replacement never removes current clothing before the replacement reaches the changing point; all invalid reservations release within six game hours.
- Missing body parts, unsafe terrain, combat callbacks, or full inventories may defer recovery but may never delete an apparel instance.
- Underwear shortage may affect mood, hygiene, and service satisfaction but may not prohibit ordinary work, rescue, treatment, combat, or bathing.

## Phase 123 - V19 life, generations, climate, and disease simulation (authoritative)

> Implement the approved 216-research V19 design on top of the clean V18
> baseline. Preserve single-authority state, atomic staged restore, authored
> content SOs, root-only Unity MCP, and the existing focused-validation ladder.

- [x] Freeze the implemented V19 domain IDs, section dependencies, V18 rejection boundary, value-only character spawn/profile contract, and focused structural validation entry points.
- [x] Extend the authoritative calendar with the 120-day year, seasons, deterministic climate zones/fronts, regional time projection, and V19 persistence.
- [x] Complete immutable species life/reproduction content and character life, aging-condition, kinship, grief/trauma, household-room, reproduction, and career Aggregates. Funeral culture/festivals, bounded grief conversion, birthdays/adulthood, retiree safe-work limits, scoped positions, and physical mentor-academy XP application are connected.
- [x] Add typed child-safety traversal context and actor-aware hazard routing without UI, direct-runtime, or path-cache bypasses.
- [x] Complete named disease/immunity/epidemic integration with existing body health. Air/droplet, contaminated meals, unsafe/foul water, persistent water pathogens, slime-water contamination, vaccination, immunity/outbreak state, anatomy burden, infection death causes, real medical blood contact, and real mana-facility exposure are connected.
- [x] Extend crop plots with physical seed lots, fertility, rotation, pests, crop disease, cultivar genomes, physical treatments, and deterministic persistence.
- [x] Add research IDs 7248-7295, physical facility/item/procedure rewards, exact BOMs, and validate the 216-node / 108-node closure / 95,448-work / 964.1-day graph.
- [x] Complete the V19 root save generation and staged aggregate validation. The final manifest has 63 strict sections; full-world capture/restore/recapture and late-failure atomicity pass.
- [x] Wire player-facing calendar/life/family/disease/safety/crop/career surfaces and validate pointer flows at both required resolutions.
- [x] Run focused compile/domain/save tests, then one final Unity MCP coordinator with fresh captures and Console Error 0 / Warning 0.

### Phase 123 final evidence — 2026-08-07

- Final PlayMode coordinator: `PASS`, seven of seven targets and 32 fresh Unity captures at `1600×900` and `900×1600`.
- Full World round trip: 63 registered/captured/recaptured sections, canonical baseline matched, original live state restored, and character progression contracts passed.
- Research: 216 runtime/catalog nodes with the V19 7248-7295 slice present; detail, reward, queue, search, pan, zoom, and both-resolution pointer evidence passed.
- Production: 11 direct consumer routes remain simultaneously visible in portrait, stock-sensor migration and physical buffer routing passed, and no upstream deadlock was introduced.
- Character/medical and equipment/expedition surfaces passed their complete EventSystem pointer matrices, including child-safety text, surgery, module processing, lineage transfer, and live expedition action.
- Architecture `154/154`, transactional restore `33/33`, synchronous acceptance `33/33`, ArchitectureMetrics hard gates `0`, and final Console warnings/errors/exceptions/asserts `0/0/0/0`.
- `Assets/Scenes/GameplayScene.unity` is saved and clean with only `__Scene`, `__Systems`, `__Runtime`, and `__Debug` roots; the final run did not require an additional scene write.

### Phase 123 constraints

- No runtime ScriptableObject synthesis and no saved mutable SO state.
- No personal currency, personal-item ownership, or family-property ledger; room and bed assignment only.
- No hard maximum lifespan or age-triggered direct death.
- Do not mutate, save, or discard a dirty user scene for test cleanup.
- Unity MCP remains root-only and operating-system input automation remains forbidden.

### Phase 123 current integration boundary

- [x] Remove `CharacterSO`, species SO, and trait SO references from `CharacterRuntimeProfile`; runtime creation now passes stable IDs through `ICharacterRuntimeProfileFactory`.
- [x] Assign stable archetype/visual IDs to all 14 root archetypes and add an explicit authored Adventurer life/species definition instead of synthesizing missing content.
- [x] Implement geriatric/chronic care, rune hibernation, blood rejuvenation, whole-body regeneration, and supply/power-gated temporal stasis with physical inputs and save state.
- [x] Replace the Unity-object-bearing `CharacterDeathEvent` with the approved value payload and one-way actor lookup at presentation/application boundaries.
- [x] Drive age-condition organ damage and all natural death exclusively through the existing body-health Aggregate; owner-only aging failure clamps authoritative vitality to one.
- [x] Publish reproduction completion through the value-only profile factory, deterministic heredity, kinship/guardian links, newborn life registration, and a persisted result character ID that prevents duplicate publication after load.
- [x] Connect authored funeral cultures and the four fixed festivals to real memorial facilities; persist one-per-year attendance and convert long-night grief exactly once.
- [x] Enforce retired safe-work routing and the four-hour/day cap at both assignment and ongoing-work boundaries; persist actual worked seconds.
- [x] Connect six authored career positions and physical mentor academies to one-award-per-student/day progression XP without copying active skills.
- [x] Route contaminated physical meals and successful world-water consumption into population-health exposure, persist exact water pathogen IDs, and make slime contamination infect the nearest real water source.
- [x] Route extracted-blood treatment contact and real mana-facility work/use into the matching blood-wasting and mana-pox exposure paths without adding another health authority.

## Phase 121 - Current-source integration closure (authoritative)

> Copilot provenance is no longer part of the scope. The current worktree is the
> authority; completion is based only on fresh compile, restore, gameplay, UI,
> capture, and Console evidence.

- [x] Complete early-V18 `CharacterId` compatibility before aggregate cross-reference preflight without mutating the input save or live world.
- [x] Replace the partial field-name/reflection compatibility path with exhaustive typed save-DTO normalization and exact legacy grammar.
- [x] Restore a fresh loaded Unity compile and pass Architecture `131/131`, Transactional Restore `33/33`, and synchronous Final Acceptance `33/33`.
- [x] With a clean loaded scene, run the project-scoped Unity MCP coordinator and prove 7 targets, 30 fresh captures, `1600x900`, `900x1600`, Full World `54/54`, `FACILITY_FLOW=RF42,RF43,RF44,I17,I18`, persistence restoration, and Console Error `0` / Warning `0`.
- [x] Synchronize QA artifacts and planning records only from the final fresh evidence.

### Final evidence — 2026-08-06

- Final PlayMode coordinator: `PASS`, seven of seven targets, 30 fresh captures, both required resolutions.
- Full World round trip: `54/54/54`, canonical baseline matched and restored.
- Console capture: warnings `0`, errors `0`, exceptions `0`, asserts `0`.
- ArchitectureMetrics: 1,388 runtime files / 4,330 types; every hard gate and cross-domain cycle candidate count is `0`.
- Test-only camera/layout dirtiness was removed only after byte-identical comparison against the on-disk Gameplay scene. The user's scene was not saved or overwritten.

### Current constraints

- Unity MCP is root-only; subagents perform source/offline work only.
- Do not save or discard a dirty Unity scene without explicit user authorization.
- Do not broaden completion back into mechanical `Assembly-CSharp` zero or another save-system redesign.

### Validation execution policy

- Use the smallest focused compile, EditMode gate, or single PlayMode target that covers each source change.
- Do not restart the seven-target PlayMode coordinator while another target-specific failure remains unresolved.
- After a full-suite failure, reproduce and close only the failed target; retain fresh PASS evidence from unaffected targets.
- Run the complete seven-target / 30-capture coordinator only as the final integration gate after all focused targets pass.
- Never ask the user to save or click for test-only residue. Diagnose through project-scoped Unity MCP, preserve real user changes, and clear only dirtiness proven byte-identical to the saved scene.


## Phase 120 - CharacterId save-contract repair and final evidence (authoritative)

- [ ] Make faction reinforcement and offense return-prisoner captures restoreable with canonical `CharacterId` values.
- [ ] Replace global field-name guessing for V18 character references with explicit typed/section-scoped normalization, including combat, defense, surgery, exterior, faction, offense, and invasion references.
- [ ] Preserve early-V18 operational actor IDs or explicitly reject unsupported shapes without partial live-state mutation; add creation -> capture -> restore regressions.
- [ ] Make final target report parsing fail closed on conflicting or duplicate result declarations.
- [ ] Close content provenance/type-validation gaps without reintroducing broad asset saves or runtime SO mutation.
- [ ] Recompile in the loaded Unity Editor and pass Architecture 131, Transactional Restore 33, and synchronous Final Acceptance 33 on the repaired source.
- [ ] After the user saves or reverts the dirty Title scene, run the project-scoped Unity MCP PlayMode coordinator and prove 7 targets, 30 fresh captures, both required resolutions, Full World 54/54, facility-flow marker, persistence restoration, and Console Error 0 / Warning 0.
- [ ] Synchronize planning and QA artifacts only from the final fresh evidence.

### Parallel ownership

- Root: integration, cross-domain compatibility policy, Unity compile/gates, PlayMode, and final documentation.
- `copilot_diff_review`: faction/offense canonical save contracts and focused regressions; no Unity/MCP.
- `architecture_quality_review`: typed V18 character-reference normalization and mixed-section regressions; no Unity/MCP.
- `acceptance_quality_review`: fail-closed report parser and content provenance/type-validation hardening; no Unity/MCP.

## Phase 119 - Post-Copilot final acceptance closure (authoritative)

- [x] Audit Copilot changes against fresh reports and a fresh Unity import.
- [ ] Restore a clean Unity compile after removing diagnostic-code regressions.
- [ ] Pass the exact Architecture 131, Transactional Restore 33, and synchronous Final Acceptance 33 gates.
- [ ] Keep equipment modules non-craftable while indexing real expedition-reward producers and real processing/installation consumers.
- [ ] Harden the final coordinator to fail closed on console-capture failure and require the exact seven target identities and 30 target-specific captures.
- [ ] Run the final PlayMode coordinator through the project-scoped Unity MCP relay only; prove seven targets, 30 fresh captures, both required resolutions, and Console Error 0 / Warning 0.
- [ ] Synchronize `task_plan.md`, `findings.md`, `progress.md`, and QA artifacts with the final evidence.

### Parallel ownership

- Root: combat/survival diagnostics, merge, Unity compile, all final runtime gates, and documentation.
- `final_gate_hardening`: final coordinator only; no Unity/MCP.
- `equipment_module_graph`: dependency catalog and production fixture only; no Unity/MCP.
- `architecture_failures`: CharacterActor/facade and architecture tests only; no Unity/MCP.


## Phase 118 - 기능 중심 최종 마감 계획 (현재 권위 계획)

이 단계는 아래의 두 가지 기계적 목표를 폐기한다.

- `Assembly-CSharp`의 게임플레이 파일 수를 0으로 만드는 전면 asmdef 이동
- 현재 정상 동작하는 V18 저장 구조를 다시 분해하거나 재설계하는 작업

저장과 어셈블리는 더 이상 별도 구현 작업이 아니다. 기존 구조를 동결하고,
현재 기능에서 실제 결함이 발견될 때만 해당 결함 범위 안에서 수정한다.

### 남은 필수 작업

1. **실제 권위 결함 마감** — 완료
   - 중복 상태 소유, 도메인 우회 쓰기, asmdef 순환 후보를 0으로 유지한다.
   - mutable static, 거대 타입, 과도한 생성자, 런타임 콘텐츠 합성,
     세션 직접 변경 하드 게이트를 0으로 유지한다.
2. **핵심 게임 기능 회귀 검증** — 구현 완료, Unity 적재 검증 대기
   - 장비 계보 이전: 원본/인장 소비, 새 장비 속성 유지, 역사 이전.
   - 원정 사망: 장비와 장착 부품의 동시 유실.
   - 화약 무기: 탄약, 재장전, 연기, 오발, 관통 및 활/석궁과의 역할 분리.
   - 168개 연구, 분기형 생산망, 시설 연료/사료, 장비 잠금과 개량 부품.
3. **통합 경계 확인** — 구현 작업이 아닌 회귀 게이트
   - 현재 소스로 Unity 컴파일 오류/경고 0.
   - V18과 54개 저장 섹션의 전체 월드 왕복이 기존 상태를 정확히 복원.
   - 실패 복원이 라이브 월드를 부분 변경하지 않음.
4. **사용자 화면 검증**
   - Unity MCP와 Unity EventSystem만 사용한다.
   - `1600x900`, `900x1600`에서 연구/생산/장비/원정 관련 포인터 흐름과
     캡처를 확인한다.
   - 사용자에게 실제 노출되는 깨진 문자열, 누락 키, 포맷 불일치만 수정한다.
5. **최종 승인**
   - 동기 기능 회귀 33단계 PASS.
   - PlayMode 수용성 매트릭스 PASS.
   - 최종 Unity Console Error 0 / Warning 0.

### 명시적 보류 작업

- 단지 파일 수를 줄이기 위한 asmdef 이동과 인터페이스 재배치.
- 현재 실패 증거가 없는 저장 DTO/섹션/버전의 추가 리팩터링.
- 화면에 노출되지 않는 의도된 한국어 콘텐츠·파서 토큰의 일괄 치환.
- 숫자 감소만을 목적으로 하는 Provider/인터페이스/어댑터 정리.

### 수정 후 완료 기준

완료 여부는 구조 변경량이나 이동한 파일 수로 판단하지 않는다. 현재 소스가
실제 Unity에서 컴파일되고, 핵심 게임 회귀와 두 해상도 UI 흐름이 통과하며,
Console이 Error 0 / Warning 0인지를 기준으로 판단한다.

---

### Phase 117 - Risk-based domain boundary closure (authoritative current plan)

> This phase supersedes every older requirement that mechanically demanded
> `Assembly-CSharp` runtime ownership reach zero. File count remains an
> informational trend only; moving an approved Unity-edge adapter solely to
> reduce that number is explicitly out of scope.

#### 2026-08-04 실행 범위 축소 확정

- `Assembly-CSharp` 잔존 파일 수와 `UnapprovedDefaultDomainAuthorityCount`는
  완료 게이트가 아니라 감사 지표로만 기록한다. 수치를 줄이기 위한 전면 이동은
  중단한다.
- 어셈블리 분리는 다음 실제 결함을 제거할 때만 수행한다.
  - 동일 상태를 둘 이상의 런타임이 쓰는 이중 권위
  - 다른 도메인의 가변 상태를 포트 없이 직접 변경하는 경계
  - 저장·복원 원자성, 결정론, 콘텐츠 단일 원본을 깨뜨리는 의존성
  - asmdef 순환 또는 테스트/컴파일을 막는 역참조
- 이미 통과한 V18 저장 구조는 동결한다. 현재 회귀에서 구체적 결함이 재현되지
  않는 한 저장 섹션 재분해·DTO 재설계·버전 변경을 하지 않는다.
- 현지화는 사용자에게 실제 노출되는 화면, 오류, 깨진 인코딩과 placeholder
  불일치만 닫는다. LLM parser 토큰이나 의도된 한국어 콘텐츠를 숫자 감소만을
  위해 옮기지 않는다.
- 최종 완료 기준은 현재 소스의 Unity 컴파일, 핵심 게임플레이 회귀, 168 연구와
  생산/장비 회귀, 두 해상도 UI 캡처, Console Error 0 / Warning 0이다.

#### 남은 실행 순서

1. **진행 중 경계만 마감한다.** Blueprint Research처럼 실제 교차 도메인 후보인
   작업과 DomainFailure 296키의 fallback/placeholder 결함을 완료한다.
2. **잔여 후보를 위험도로 판정한다.** 최신 cross-domain 후보 각각을 조사해
   이중 권위·직접 상태 변경·순환이 재현되는 것만 수정한다. 단순 Unity adapter,
   save adapter, composition wiring은 근거를 기록하고 그대로 둔다.
3. **기능 회귀를 우선한다.** 아이템/SO 단일 원본, 물리 재고·장비 인스턴스,
   생산 분기·시설 공급, 연구 168개, 장비 잠금·부품·계보를 현재 소스에서 검증한다.
4. **사용자 노출면을 닫는다.** String Table builder를 Unity에서 실행하고 실제
   화면의 누락 키, 깨진 한글, format 인자 불일치만 수정한다.
5. **Unity MCP로 최종 인수한다.** `1600x900`과 `900x1600` 포인터 흐름 및
   캡처, 전체 회귀, V18 validator, 54개 저장 섹션, Console 0/0을 한 번에 증명한다.

#### 명시적으로 제외하는 작업

- 모든 기본 어셈블리 파일을 named asmdef로 옮기는 작업
- 분리 효과 없이 파일 수·인터페이스 수·라인 수만 줄이는 기계적 리팩터링
- 정상 통과 중인 V18 저장 섹션을 다시 묶거나 재작성하는 작업
- 사용자에게 노출되지 않는 의도된 한국어 데이터와 LLM 계약의 전면 현지화

#### Revised completion contract

- [x] Freeze the completed V18 save architecture. Do not start another save
  refactor unless a current round-trip, atomicity, or compatibility regression
  proves a concrete defect.
- [x] Add a source-syntax ownership classifier and reviewed manifest for every
  runtime file that remains in default `Assembly-CSharp`.
  - `NamedRequired`: mutable domain state/Aggregates, state stores, pure rules
    and calculators, content/SO definition authority, persistent contracts,
    domain command/query policy, deterministic reward/research/production logic.
  - `DefaultAllowed`: scene-bound `MonoBehaviour` adapters, Unity input/camera/
    audio/VFX bridges, prefab/view wiring, Presentation-only Views, and the
    Composition Root that assembles named implementations.
  - `ReviewRequired`: any file that mixes an allowed Unity edge with gameplay
    state or rules. It must be split; it cannot be approved by an explanation
    alone.
- [x] Treat `UnapprovedDefaultDomainAuthorityCount` and `defaultAssemblyFiles`
  as review metrics only. Neither count has a zero target; only concrete
  authority conflicts and unsafe cross-domain mutations are blockers.
- [x] Reduce cross-domain cyclic-boundary violations to `0` in the current
  source audit:
  - asmdef cycles remain `0`;
  - no cyclic source SCC may contain a `NamedRequired` owner;
  - no remaining default-edge SCC may directly bypass the approved command,
    query, capability, or DTO boundary between gameplay domains.
- [x] Keep mutable statics, oversized types, large constructors, runtime
  content synthesis/escapes, and direct session mutations at `0`.
- [ ] Finish localization by user-visible vertical surface rather than by blind
  literal replacement: visible mojibake `0`, visible missing keys `0`, and
  visible placeholder mismatches `0`. Intentional parser/content Korean is not
  a completion metric.
- [ ] Run the fresh Unity integration boundary: compile diagnostics `0`, V18
  validator, all 54 save sections, gameplay/domain regression matrix,
  `1600x900` and `900x1600` pointer captures, Console Error `0` / Warning `0`.

#### Execution batches

1. **Stabilize the current source batch.** Finish the in-flight Production UI,
   Defense localization, Invasion policy, and Offense reward-policy cuts;
   regenerate the semantic graph and compile on fresh Unity assemblies.
2. **Install the risk classifier.** Replace the `defaultAssemblyFiles == 0`
   completion gate with the reviewed role manifest plus
   `UnapprovedDefaultDomainAuthorityCount` and cross-domain-cycle gates.
3. **Cut only proven high-risk owners.** Review the remaining cross-domain
   candidates and change a file only when a duplicate writer, unsafe direct
   mutation, deterministic/save defect, or cyclic dependency is demonstrated.
   `NamedRequired` and `ReviewRequired` classifications alone do not authorize
   a migration; approved Unity-edge adapters stay in place.
   - Character progression boundary: the experience curve and deterministic
     add/minimum/restore transitions now belong to named Characters. The
     public scene component and save snapshot remain at the existing edge;
     Foundation/Operation notifications and deterministic random-stream
     allocation are isolated behind explicit application adapters. A fresh
     analyzer now sees only the Characters domain on the state owner, so the
     target is absent from the cross-domain candidate set without an override.
   - Environmental field boundary: the array Aggregate, root-store access,
     diffusion/exterior exchange, source transitions, buffer swaps, and version
     touch now belong to named Environment. The preserved default source/GUID
     is a Grid/building/power/clock application adapter and is `DefaultAllowed`.
     Randomized legacy-equivalence coverage passes for 240 grid scenarios.
   - External influence boundary: named CoreSession now owns the Aggregate
     store plus reputation/dread/rumor/scouting, daily ecology pressure, raid,
     intel-payment, and invasion-defense transitions. The preserved default
     source/GUID is a Content/clock/economy/item/wildlife/event application
     adapter and is absent from the fresh cross-domain candidate set.
   - Composition registration boundary: a default static `*Registration` is
     allowed only under `Infrastructure/Registration` when every member is a
     stateless `void Register*` method rooted at `IContainerBuilder` and its
     body contains registration/exposure wiring only. World-simulation wiring
     satisfies this shape; mutable or calculating lookalikes remain review.
4. **Close localization vertically.** Production UI -> Defense UI/failures ->
   Character narrative/templates -> remaining UI/domain clusters. Generate
   String Tables through Unity Editor builders; do not hand-author YAML.
5. **Final acceptance.** Re-run the complete current-source regression and
   capture matrix through Unity MCP only. Save remains a regression gate, not
   an implementation workstream.

#### Scope reduction and scheduling rule

- Current default runtime ownership is approximately `811` files. This plan
  does **not** estimate completion from `811 -> 0`; the classifier audit will
  produce the exact `NamedRequired` residual set.
- Expected migration reduction is substantial (working estimate `70-85%` of
  the former assembly-move workload), but the estimate is not an acceptance
  criterion. Only the reviewed violation sets determine completion.
- An approved adapter must not be moved merely because it is easy. Every move
  must remove a domain-authority violation, a cross-domain cycle, or a proven
  hidden dependency.

### Phase 116 - Default-assembly giant-SCC decomposition

- [x] Remove the independent RunFlow, Offense save, Invasion save, Medical supply, Husbandry, Circus, Staff UI, CharacterCombatCommand, Fluid, CharacterSurgery, Captivity restore, Grid construction, DefenseEngagement, ResearchTree, and CharacterSummary cyclic boundaries without named-to-default backreferences.
- [x] Reduce the semantic planner from 18 cyclic SCCs to the single remaining default-assembly giant SCC while preserving Unity script GUIDs and the V18 restore ordering contracts.
- [x] Keep the architecture ratchets at mutable statics 0, oversized types 0, large constructors 0, content escapes 0, and direct session mutations 0; reduce raw Korean literals below the prior 6,462 checkpoint.
- [x] Complete the BuildableObject, InvasionIntruder, Offense world-map, CharacterActor visitor, Shop customer, Environment bridge, Survival bridge, and WorkTargetCandidate bounded cuts inside the giant SCC.
- [ ] Reconnect the project-scoped Unity MCP after the domain-reload relay disconnect, refresh Unity, regenerate the Bee-bound graph, and rerun the complete V18/domain regression matrix on fresh DLLs.
- [x] Retire the mechanical zero-default-assembly target in favor of the
  risk-based ownership and cyclic-boundary gates in Phase 117.
  - Current execution mode: use cohesive 2-10 source clusters after the safe one-file leaves were exhausted by concrete `CharacterSO`, `BuildableObject`, `WarehouseInventory`, and Offense enum boundaries.
  - Historical checkpoint: `815` default-assembly files and one cyclic SCC of `470` files; this count is no longer the completion oracle.
- [ ] Finish localization and the required `1600x900` / `900x1600` Unity MCP pointer-and-capture evidence with Console Error 0 / Warning 0.

### Phase 115 - Fresh V18 integration and cyclic-SCC reduction

- [x] Run the merged V18 authority, all 54 save sections, Batch B/C, physical-item, persistent-ID, and Offense aggregate/world-map/journey regressions on fresh Unity assemblies.
- [x] Run Blueprint Research, Research Tree, 168-node research/equipment, branched production, Facility Evolution, Survival, Combat, strict combat-save, material-equipment, and Captivity/Circus regressions together.
- [x] Correct the material-equipment Editor fixture to inject the required facility-evolution state instead of weakening `BuildableObject` initialization.
- [x] Move the Operations presentation boundary and WildlifeCapture restore validation policy into named assemblies while preserving Unity script GUIDs.
- [x] Generalize the controlled-stat dictionary through Foundation and restore the hard size gates to mutable statics 0 / oversized types 0 / large constructors 0.
- [x] Complete the Invasion, Captivity/Circus, medical, production, and presentation SCC cuts; their current continuation is tracked by Phase 116.
- [x] Retire the zero-file migration target; Phase 117 now owns the remaining
  risk-classified domain boundaries. Localization and two-resolution Unity MCP
  proof remain final gates.

### Phase 114 - Leaf named-assembly migration checkpoint

- [x] Finish the assigned strict-save six-section source checkpoint and record the unavailable local SDK build boundary.
- [x] Run the semantic AssemblyMigrationPlanner in clean/project-scan mode and exclude active Offense, modular/character world save, work-order, service-room, and combat-save ownership.
- [x] Select the smallest safe leaf SCC/file batch, capped at 15 source files, and map it to existing domain asmdefs without an Assembly-CSharp dependency.
- [x] Move sources with original `.meta` GUIDs, add `MovedFrom` only where serialized type identity requires it, and repair port/asmdef boundaries.
- [x] Confirm that no architecture-validator source path targets this leaf and finish with planner/source/diff checkpoints; Unity compilation remains a root-agent responsibility.

### Phase 113 - Completion-audit corrections and final closure

- [x] Reject the stale-DLL clean result, recover the truncated Operating Day source, and establish a fresh Unity compile with source-newer counts `0/0`.
- [x] Re-run the 168-node research/equipment, branched production, Facility Evolution, and Survival focused regressions on the fresh assemblies.
- [x] Remove top-level Offense direct-restore bypasses and the category-to-representative-item runtime authority.
- [x] Finish strict detached-candidate conversion for every remaining one-parameter `DungeonJsonSaveSection<T>`; source usage is 0 and the 54-section late-failure regression passes.
- [x] Remove the remaining public Offense subsystem restore bypasses and merge the legacy campaign world map into the strategic Offense aggregate.
- [x] Rebind all embedded GameplayScene MonoScripts, remove the leaked scenario character, and save the scene through Unity MCP.
- [x] Retire the mechanical zero-file goal; Phase 117 requires zero
  unapproved domain authorities and zero cross-domain cyclic-boundary
  violations instead.
- [x] Run the complete V18, physical item, production, combat, Offense, and architecture regressions on fresh DLLs.
- [ ] Verify the required `1600x900` and `900x1600` pointer workflows and captures through Unity MCP only.
- [ ] Complete a requirement-by-requirement source/asset/runtime audit and finish with Console Error `0` / Warning `0`.

> Audit correction: older phases marked generic save staging or full authority closure as complete based on marker/source checks. The 2026-08-04 audit found 32 one-parameter generic save sections whose fallible candidate construction can still occur during commit. Phase 113 is the authoritative reopening until those concrete boundaries are converted and verified.

### Phase 107 — Unity composition and clean-run recovery

- [x] Break the exterior-zone query cycle by projecting physical zone markers through a read-only `IExteriorZoneQuery` owner.
- [x] Replace wildlife-carcass → deprivation direct mutation with a scoped taboo incident event.
- [x] Make facility-evolution modifier evaluation a pure component-state query instead of re-entering the evolution command runtime.
- [x] Assign mandatory `CharacterId` values before character presentation/runtime bridges query persisted character state.
- [x] Repair stale `InvasionDirectorRuntime` scene script references without discarding serialized invasion state.
- [x] Reassign eight colliding production numeric IDs and make duplicate catalog IDs fail composition; add editor validation coverage.
- [x] Remove temporary VContainer diagnostics and dependency probes after the complete composition probe passed.
- [x] Planning entry retired; remaining full import, Run Flow, and Console proof is tracked only by Phase 112 final verification batches.

### Phase 106 — Detached save staging cutover

- [x] Add an explicit immutable `IDungeonSaveRestoreStage` contract and split registry restore into preflight, prepare-all, and commit phases.
- [x] Guarantee that a staging failure leaves every live section untouched; retain rollback only as a transitional guard for legacy commit implementations.
- [x] Connect physical items to their existing detached `WorldItemRestoreState` and commit only after every section has prepared successfully.
- [x] Move all generic JSON sections, the offense Aggregate, and all seven combat sections to prepare payloads before live mutation.
- [x] Add and pass a focused staging-failure regression; update the stale combat DI fixture exposed by the full combat regression.
- [x] Convert all 54 public SaveSection implementations to mandatory detached preparation, including optional missing-data stages.
- [x] Remove the transitional legacy-section adapter and add a reflection gate that rejects any new direct-restore section.
- [x] Remove mutable scene-transition statics and resolve the scoped navigator in diagnostics instead of constructing fallback clock/time-scale services.
- [x] Reconfirm the runtime and Editor assemblies compile cleanly; runtime scans report zero optional interface parameters, fallback infrastructure construction, runtime SO synthesis, item-definition fallback, and late runtime binds.
- [x] Cut production bills + stock sensors and combat loadouts + craft/history orders over to shared replaceable Aggregate state stores; remove combat's second write to physical equipment state.
- [x] Convert the independent economy, species, staff-discontent, deprivation, ledger, and debug restore collections to build-then-swap roots.
- [x] Convert faction, experience pacing, meta progression, defense facilities, and the combined exposure/workwear environment Aggregate to detached state replacement.
- [x] Delay physical-item markers, warehouse normalization, faction sites, husbandry reconciliation, service-hub subscriptions, run-flow effects, and captivity projections until the shared Aggregate root is actually published.
- [x] Move `GameSessionState` ownership out of `GameManager` into a scoped store and route modular-world session restoration through its explicit restore API.
- [x] Add a composition-wide `IDungeonRestoreTransactionParticipant` lifecycle so inactive Unity candidates can begin, publish, and discard with the same save transaction as DTO Aggregate roots.
- [x] Build modular facilities on an occupant-free Grid with inactive GameObjects, restored modules, persistent IDs, and no world/contract registration before replacing the live facility Grid.
- [x] Create owner and staff restore candidates under an inactive hierarchy; suppress lifetime/world/AI/presentation/Grid-event registration until their full state is applied and explicitly published.
- [x] Planning entry retired; remaining candidate-world indexing and publication work is tracked only by Phase 112 Batch E.
- [x] Planning entry retired; remaining Aggregate-root and rollback-image work is tracked only by Phase 112 save batches and Batch E.

### Phase 104 — Root-SO gameplay catalog cutover

- [x] Add authored meta-upgrade, run-variable, owner-doctrine, and invasion-pattern records to `GameDomainContentCatalogSO`.
- [x] Migrate the exact 9/14/3/6 live definitions and effect parameters into `GameDomainContentCatalog.asset`.
- [x] Replace four mutable static dictionaries with one injected immutable `AuthoredGameplayCatalog` projection.
- [x] Remove production `Register`, `ResetToBuiltIns`, runtime-reset hooks, code fallback construction, and all production references to the four legacy catalogs.
- [x] Make meta progression state and run-variable state retain their required catalog authority explicitly.
- [x] Add V18 validation for authored counts, required IDs, projection construction, and forbidden legacy catalog classes.
- [x] Freeze character-stat, work-type, and facility-role enum/bit mappings as immutable protocol tables; remove their global registration/reset APIs.
- [x] Move the remaining character-need, stock-category, and building-category balance/display records into authored SO content.
- [x] Import and execute the new catalog projection through Unity MCP.

### Phase 102 — Authored presentation and building archetype authority

- [x] Remove every runtime `ScriptableObject.CreateInstance` path; authored water/filth tiles now come from `GameContentCatalogSO`.
- [x] Replace the `GridTexture` runtime Tile wrapper with rebuildable `SpriteRenderer` presentation objects.
- [x] Remove `BuildingSO.type` and `AddComponent(Type)` from runtime construction.
- [x] Migrate all 343 `BuildingSO` assets from Odin `System.RuntimeType` nodes to one of eight fixed runtime archetypes.
- [x] Remove implicit `ItemDefinitionId -> string` conversion and reject modular-facility V1/V2 migration inside the V18 generation.
- [x] Load wildlife SOs through the root domain-content catalog instead of `Resources.LoadAll` and code fallback insertion.
- [x] Add V18 regression gates for authored world tiles, runtime SO synthesis, building archetypes, and legacy Type nodes.
- [x] Planning entry retired; final asset import/meta/graph proof is tracked only by Phase 112 Batch M/N.

### Phase 103 — Remaining authority gaps

- [x] Authored need/work/facility-role/stock/building authority completed in Phase 104; final asset closure is tracked by Phase 112 Batch M.
- [x] Planning entry retired; remaining atomic world-swap work is tracked only by Phase 112 Batch E.
- [x] Planning entry retired; remaining default-assembly migration is tracked only by Phase 112 Batches G/H.
- [x] Planning entry retired; final regressions and captures are tracked only by Phase 112 Batch N.

### Phase 101 — Policy-free runtime provider removal

- [x] Replace facility, progression, run-variable, offense, and invasion forwarding providers with scoped domain runtime registries.
- [x] Make required runtime absence a composition failure instead of an empty/default save or gameplay result.
- [x] Keep `ILocalLlmRuntimeProvider` as the sole provider boundary because it has two environment-specific implementations.
- [x] Update runtime and Editor fixtures; auxiliary Roslyn compilation passes with Error 0 / Warning 0.
- [x] Planning entry retired; final provider/import regression is tracked only by Phase 112 Batch N.

## V18 Runtime Authority Normalization (Active)

| Phase | Scope | Status |
|---|---|---|
| 82 | Freeze V18 incompatibility boundary and authority baseline | Completed |
| 83 | Establish `GameContentCatalogSO` root and strict domain projections | Completed |
| 84 | Introduce typed persistent IDs and remove persistence fallbacks | Completed |
| 85 | Make physical item repository authoritative for stock and equipment | Completed |
| 86 | Move mutable `GameData` and static run state into scoped services | Completed |
| 87 | Consolidate offense state into one aggregate/save section | Completed |
| 88 | Stage and preflight restore before live-world commit | In Progress — prepare-all pipeline and detached physical-item state are live; final Aggregate-root swap remains |
| 89 | Remove runtime SO synthesis, catalog bypasses, optional DI, and late provider binding | In Progress — runtime synthesis/provider paths removed; code-owned catalogs remain |
| 90 | Split oversized runtime/UI classes, domain errors, localization, and domain asmdefs | In Progress |
| 91 | Run full regressions, two-resolution MCP UI proof, and Console Error/Warning 0 | Pending |

Current Phase 90 order:

1. ~~Rename `CharacterSummeryInfo` to `CharacterSummaryInfo` without breaking Unity GUID references.~~ Completed.
2. ~~Extract character-summary tab presenters and view models until the coordinator is below 800 lines.~~ Completed: coordinator 729 lines, 8 injected dependencies, presenters 147–516 lines.
3. Remove direct runtime `System.Random` construction and pin the rule in the V18 validator. Completed.
4. Split save DTO/query/policy responsibilities from the remaining largest runtime/UI classes. In progress: all UI exceptions are removed; `EquipmentEvolutionRuntime` is 1,176 lines, `AbilityMove` is 1,200 lines, and `CombatEquipmentRuntime` is 864 lines. The oversized-source baseline now contains 6 runtime entries.
5. Add `FailureCode + parameters` contracts and String Table presentation mapping. In progress: equipment module/lineage commands now return `DomainFailure`; the Korean `DomainFailures` Unity String Table has 21 validated entries.
6. Move only `NamedRequired` gameplay authority out of `Assembly-CSharp` and
   retain reviewed Unity-edge adapters under the Phase 117 manifest.
7. Replace the remaining regex ratchet with source-syntax and assembly/asset-graph validation.

The V18 validator currently enforces save V18, strict authored item authority, no legacy
item catalogs, no runtime content-SO synthesis, no direct runtime `Resources.Load`, no optional
interface injection, scoped session state, physical equipment authority, and one offense aggregate.
It now also enforces the character-summary size/dependency boundaries and zero direct runtime
`System.Random` construction.

## Goal

Complete V16 by removing isolated or duplicate gameplay authorities and proving that the connected
equipment, offense rewards, arrivals, exterior incidents, nutrition, circus resources, and AI
performance paths work together:

```text
physical production and meals -> persistent characters/items/equipment
-> offense and exterior outcomes -> physical arrivals and regional pressure
-> captivity/circus/survival follow-up work
-> V16 save round trip, pointer UI, visual evidence, and performance closure
```

## Phases

| Phase | Scope | Status |
|---|---|---|
| 1 | Audit offense, facilities, rooms, stock, staff, rewards, and save contracts | Completed |
| 2 | Implement route nodes, supplies, stress, formation, retreat, and expedition state | Completed |
| 3 | Connect dungeon rooms/facilities/stock to preparation, recovery, and expedition modifiers | Completed |
| 4 | Replace offense UI with preparation, route, node, and formation-aware battle surfaces | Completed |
| 5 | Persist and restore active multi-node expeditions with migration | Completed |
| 6 | Verify formulas and state transitions in EditMode | Completed |
| 7 | Verify pointer-driven recruitment, journey, battle, save/restore, and `truth_core` completion with MCP captures | Completed |
| 8 | Audit character identity, stats, training, battle abilities, UI, and save ownership for per-character progression | Completed |
| 9 | Implement per-character level, experience, learned skills, and equipped skill slots | Completed |
| 10 | Connect training and offense outcomes to experience and skill unlocks | Completed |
| 11 | Surface level, experience, learned/equipped skills in character and offense UI | Completed |
| 12 | Persist progression and migrate existing characters and saves | Completed |
| 13 | Verify progression formulas, combat skill legality, UI input, and save round trip | Completed |
| 14 | Replace legacy progression with level-50 potential, stat growth, narrative ledger, modular skills, passives, and ultimates | Completed |
| 15 | Add constrained LLM skill generation, validation, persistent retry, and hidden request state | Completed |
| 16 | Replace owner selection with three-character start preparation and persistent world population | Completed |
| 17 | Integrate growth/event UI, save V3 incompatibility handling, and combat/operation triggers | Completed |
| 18 | Verify growth generation, save restore, pointer workflows, world population, ultimates, captures, and regressions | Completed |
| 19 | Audit weak links between completed gameplay systems and prioritize missing feedback loops | Completed |
| 20 | Unify persistent character identity, social memory, and V4 save validation | Completed |
| 21 | Rebalance level-50 progression and connect generated skill modules to runtime events and formations | Completed |
| 22 | Share cached room environment queries with AI, mood, guest, and work duration systems | Completed |
| 23 | Add equipment catalog, crafting queue, expedition loadout, death loss, and facility recovery | Completed |
| 24 | Surface stat breakdowns, crafting, equipment, stress, and readiness in product UI | Completed |
| 25 | Add deterministic EditMode and pointer-driven PlayMode coverage for the new closed loop | Completed |
| 26 | Direct-play the campaign through `truth_core`, capture desktop/mobile/world evidence, and clear the Console | Completed |
| 27 | Add physical item catalog, world stack runtime, pile marker, and V5 save payloads | Completed |
| 28 | Connect delivery, rewards, warehouse aggregation, carried inventory, and hauling limits | Completed |
| 29 | Add Haul work type, AI hauling action, pickup/dropoff pathing, and overburden movement penalty | Completed |
| 30 | Add item pile UX with marker badges, list/detail panel, Alt-click override, and character-first selection | Completed |
| 31 | Convert shop restock, purchases/theft, crafting input/output, and expedition packing to physical stack flows | Completed |
| 32 | Add item/hauling EditMode coverage for stack merging, reservation, weight, save restore, and pile UX sorting | Completed |
| 33 | Add pointer-driven PlayMode coverage for item piles, hauling, warehouse/shop/craft/expedition flows | Completed |
| 34 | Capture stack marker, pile list/detail, carry UI, and clear Console Error/Warning 0 | Completed |
| 35 | Split new-run owner and start-party preparation into a dedicated preparation scene | Completed |
| 36 | Add owner fixed skill slots and reserve staff roster preparation | Completed |
| 37 | Build owner-select and RimWorld-style party preparation UI | Completed |
| 38 | Verify preparation scene navigation, selection, reroll, start handoff, and compile state | Completed |
| 39 | Fix start-preparation roster drag swap, RimWorld-style detail layout, dice reroll placement, and start-button gate | Completed |
| 40 | Add unified work-order runtime, construction sites, work units, and V9 save payloads | Completed |
| 41 | Route placement, AI work, materials, crafting, research, cooking, butchering, water, treatment, and refuel through work units | Completed |
| 42 | Surface construction/work progress in UI and character labels | Completed |
| 43 | Verify compile, focused contracts, pointer gameplay, save/restore, and visual captures for work progress | Completed |
| 44 | Diagnose and fix world nameplate occlusion and readability across dungeon layers | Completed |
| 45 | Replace wildlife horizontal oscillation with habitat-aware varied path movement and stable intent timing | Completed |
| 46 | Restore player camera zoom input with unscaled controls and verify nameplates, wildlife motion, and zoom in PlayMode | Completed |
| 47 | Audit staffed checkout waiting, customer patience, mood, memory, and alternate-shop handoff | Completed |
| 48 | Add patience-scaled checkout stages, service calls, complaints, abandonment, and alternate shopping | Completed |
| 49 | Surface checkout wait position, elapsed time, and reactions through character phases and event alerts | Completed |
| 50 | Verify patience rules, visit handoff, personal facility memory, PlayMode behavior, and Console state | Completed |
| 51 | Audit paused stair traversal visibility and multi-low-need AI triage | Completed |
| 52 | Make stair traversal visibility obey scaled simulation time | Completed |
| 53 | Fix survival-only emergency triage, fallback selection, and worker/owner self-care access | Completed |
| 54 | Add paused traversal and combined low-need regression coverage | Completed |
| 55 | Verify the fixes in PlayMode and clear Console errors/warnings | Completed |
| 56 | Audit repeated emergency wait and stationary-character fallback paths | Completed |
| 57 | Replace stationary wait fallback with contextual micro-actions and reachable roaming | Completed |
| 58 | Connect low mood to bounded autonomous impulses instead of passive waiting | Completed |
| 59 | Add anti-stall detection, retry/backoff, and regression coverage | Completed |
| 60 | Verify moving fallback and low-mood behavior in PlayMode | Completed |
| 61 | Audit the live research, reward, equipment, expedition, save, and UI contracts for the 168-node overhaul | Completed |
| 62 | Add causal prerequisite links, reward reverse indexing, 168 research specs, effort bands, and timing simulation | Completed |
| 63 | Add research-linked facilities, production items, recipes, and the 24-equipment content expansion | Completed |
| 64 | Enforce equipment research locks and implement tier, growth-slot, ammunition, smoke, reload, and misfire rules | Completed |
| 65 | Add expedition-only module instances, deterministic drops, appraisal/restoration/fitting/tuning, loss, and persistence | Completed |
| 66 | Add lineage seals, transfer orders, category-safe history inheritance, and form-neutral evolution contracts | Completed |
| 67 | Upgrade research/equipment saves to V4 incompatibility, expose unlock/lock/module/lineage UI, and add validation | Completed |
| 68 | Run focused compile, deterministic scenarios, pacing/content/save/UI verification, regenerate assets, and update docs | Completed |
| 69 | Audit the live recipe, item, equipment, construction, medical, supply, bill, conveyor, save, and UI graphs for V3 | Completed |
| 70 | Add production dependency contracts, reverse indexing, depth/branch validation, and concrete supply metadata | Completed |
| 71 | Re-author concrete branched intermediates, recipes, research rewards, facilities, equipment materials, and consumers | Completed |
| 72 | Implement repeat-forever/stock-sensor gating, local output buffers, fair branch distribution, fuel/feed selection, and V5 persistence | Completed |
| 73 | Surface dependency branches, route policy, stock-sensor unlock, and distinct blocked states in production/research/equipment UI | Completed |
| 74 | Add deterministic graph, runtime, logistics, save, compatibility, pacing, and two-resolution pointer coverage | Completed |
| 75 | Regenerate assets, compile, run focused and broad regressions, update docs, and clear Console Error/Warning 0 | Completed |
| 76 | Audit every item-definition authority, lookup fallback, feature field, instance side table, and save bridge | In Progress |
| 77 | Introduce one canonical ItemDefinitionSO base, composable immutable features, typed IDs, and strict validation | Pending |
| 78 | Migrate resource, equipment, survival, medical, wildlife, industrial, and special items into generated canonical assets | Pending |
| 79 | Replace permissive and hardcoded lookup chains with one indexed catalog and compatibility-only read adapters | Pending |
| 80 | Add generic versioned item-instance components, stack signatures, save persistence, and equipment/freshness bridges | Pending |
| 81 | Regenerate assets, compile, run item/production/equipment/save regressions, update docs, MCP capture, and clear Console | Pending |

## Product Decisions

- Party size remains 3 and the owner cannot join.
- Party positions are front, middle, and rear; skills declare usable and target positions.
- An expedition contains multiple connected nodes, not one battle.
- Supplies come from dungeon stock and are consumed during the expedition.
- Health and stress persist between nodes. Retreat preserves survivors and collected loot but forfeits unsecured rewards.
- Dungeon rooms and facility abilities provide preparation capacity, recovery, scouting, and supply efficiency.
- Death remains permanent. Returning survivors recover through dungeon services rather than automatic full healing.
- Campaign regions end in bosses; only the final `truth_core` boss reveals the truth and wins the run.
- The temporary campaign-order combat-stat multiplier is not part of the target design and must be removed.
- Character level, experience, learned skills, and equipped skills are per-character runtime/save state; `CharacterSO` remains immutable species/archetype authoring data.
- Skill definitions are shared data, while unlock and loadout state belong to each character instance.
- Character skill slots are fixed at one species active, three normal actives, two passives, and one ultimate.
- Potential affects only normal-active rarity odds; traits remain identity modifiers and passives remain event-triggered learned abilities.
- Generated skill state, drafts, narrative facts, retry keys, and use limits are per-world-character save data, never mutable shared ScriptableObject state.
- Skill rules choose rarity, budget, allowed module IDs, and variants before the LLM; invalid output retries under the same hidden request key with no player-facing fallback or generation status.
- The run begins with an owner and two same-species employees after all three have a selected level-one active and validated first passive.
- Old progression saves are intentionally incompatible with the new growth schema and must start a new game.
- `CharacterIdentity.PersistentId` is the sole runtime identity; template IDs never key per-person state.
- Room quality affects facility choice, mood, guests, and eligible work duration only; it never modifies offense stats directly.
- Facilities affect expeditions through crafted equipment and completed recovery use, not ambient combat bonuses.
- Generated skills have rule-authored formation masks and every accepted module must execute in an allowed runtime context.
- V4 rejects duplicate persistent IDs and V3-or-older saves instead of silently merging or migrating person state.
- Gold remains abstract money; non-gold delivery, reward, loot, crafting, shop, and expedition supplies become physical world stacks.
- `DungeonItemCatalogSO` owns item authoring; runtime stack/carry/save state must not be stored on shared ScriptableObjects.
- Item click priority is Character > Item > Building, with Alt-click forcing item pile selection on occupied cells.
- Warehouse inventory becomes an aggregate view of stored physical stacks while loose/carried/reserved items remain visible as separate states.
- Hauling capacity is character-owned and overburden affects movement speed, not global time scale.
- Stored warehouse stacks are hidden in normal play and become visible only through the `물품` view toggle; when stored stacks exist in V5 saves, they resynchronize the warehouse aggregate on restore.
- New runs use `StartPreparationScene` between title and gameplay. Gameplay-scene owner selection remains only as a direct-scene QA fallback.
- Owners have four fixed owner-skill slots in addition to normal generated growth slots. Fixed owner skills are authored static identity data, not LLM-generated or rerolled state.
- Start preparation contains one locked owner, two selected same-species staff, and four reserve staff candidates. Only selected staff enter the run.
- Start preparation treats the owner as ready through fixed owner skills; only selected staff must complete first active/passive start choices.
- Selected and reserve staff can be swapped by dragging roster cards onto each other.
- Player-placed buildings become construction sites with material delivery and work-unit progress; default/new-run seed buildings remain completed.
- Shared SOs may define static work requirements, but delivered materials, reservations, completed work, and queues are runtime/save state.
- Production recipes may reference only concrete item IDs; abstract `stock-item:*` matching remains available only through facility fuel/feed supply profiles.
- Every shared `Intermediate` item has at least two real downstream consumers, while fake `sink:*` recipes never satisfy branch validation.
- Production transformations after raw acquisition have a maximum dependency depth of four; single-purpose assemblies are finished installation components rather than fake intermediates.
- Facility input/output buffers, reservations, distribution policies, pending order-mode transitions, and chosen concrete supplies are per-facility runtime/save state, never mutable shared asset state.
- V3 production-network content intentionally rejects the preceding V4 research/equipment compatibility generation and requires a new V5 run.
- Item authoring has one authority: every physical item is an `ItemDefinitionSO` asset indexed by one strict catalog; domain catalogs are derived read-only views.
- Optional item behavior is authored through immutable feature modules, while freshness, durability, quality, provenance, and other mutable values use versioned runtime instance components.

## Verification Gate

1. Runtime and Editor assemblies compile with Console `Error 0 / Warning 0`.
2. Route generation, node transitions, supplies, stress, formation legality, retreat, death, loot, and boss completion have deterministic tests.
3. Dungeon stock and eligible room/facility effects visibly change expedition preparation and outcomes.
4. UI pointer input can prepare a party, buy/load supplies, choose route branches, resolve nodes, issue formation-valid combat commands, camp, and retreat.
5. Save/load restores the exact route node, party order, health, stress, supplies, loot, battle turn, cooldowns, and statuses without duplication.
6. A clean direct-player run completes all regions and reveals the truth at `truth_core`; no scenario-state injection counts as completion evidence.
7. MCP captures prove readable preparation, route, combat, return, and truth-result screens without overlap or input leakage.
8. Physical item work compiles cleanly before verification; no PlayMode result counts while Unity is running stale assemblies.
9. Stack pile list/detail, carried weight, hauling, warehouse/shop/crafting/expedition item flows, and Alt-click priority are verified by actual pointer/UI tests.
10. V5 save checks include world stacks, stored-warehouse mirrors, hauling settings, and per-character carried inventory.
11. Start preparation checks include title-to-preparation routing, owner fixed skill display, selected/reserve staff swap, prepared snapshot handoff, and no gameplay owner-selection panel in the product flow.
12. V9 work-order checks include construction-site placement, material delivery, partial progress, save/restore, and final building replacement without instant completion.

## Errors Encountered

- 2026-08-26: The first integrated Editor compile of the new terminal-producer upper-join fixture failed with CS0117 because `Assembly-CSharp.ref.dll` intentionally omits the validator's internal static join methods from the Editor assembly surface. Runtime compilation passed. These pure, read-only cross-aggregate validation entry points will be made public (they are not gameplay mutation APIs), then the Editor compile will be retried; no live state or asset changed.

- 2026-08-26: A restore-flow inspection used the stale guessed path `Assets/Scripts/Services/Infrastructure/Save/DungeonGameSaveService.cs`; `rg` returned OS error 2 after the contracts read succeeded. No file changed. The next inspection resolves the exact path with `rg --files` and will not reuse the guessed location.

- 2026-08-26: The first Editor static compile after adding the generic producer upper join found one direct focused-fixture construction of `ProductionFacilityDestructiveDrainCrossAggregateSaveValidation` missing the new pure generic validator dependency (CS7036 at `ProductionPreparedOutputFullPersistenceDebugScenarios.cs:404`). Runtime compilation had already passed. The fixture constructor will be updated with the real stateless validator before retrying; no production state or asset changed.
| Broad SelfCare verifier `rg` query returned exit 1 while printing unrelated matches | 1 | Treat as a search-pattern miss, not a code failure; switch to filename discovery with `rg --files` before querying exact source. |
| Combined primitive-runner inspection returned exit 1 because optional `AIPrimitiveSurvivalAction.cs` path does not exist | 1 | The requested runner sections were still read; locate the actual primitive base/action file before any follow-up instead of retrying the missing path. |
| Explicit GameplayScene load was refused because the already-active scene was marked dirty | 1 | Do not save/discard user scene changes. Verified the active PlayMode scene is already `Assets/Scenes/GameplayScene.unity`; proceed only with the verifier's full-save baseline/restore and manually exit afterward. |
| Primitive focused report said PASS but a late FacilityEvolution projection enumeration exception appeared in Unity Console | 1 | Reject the artifact as false-green; inspect `FacilityEvolutionActivationProjection.Reconcile` and verifier restore timing, then add/repair the exact teardown-time ownership fence before rerun. |

- 2026-08-09: 전투 콘텐츠 검색에 존재하지 않는 `Assets/Scripts/Content`를 포함해 `rg`가 유효 결과를 출력한 뒤 exit 1을 반환했다. 이후 실제 `Assets/Scripts/Services/Offense`와 에셋 루트를 사용한다.
- 2026-08-09: `encounter_01~06`도 V20 폴더에 있다고 추정해 읽었으나 기존 6개는 `Assets/Resources/SO/Offense/Encounters`에 있고 V20 폴더에는 07~36만 있다. ID 검색으로 실제 루트를 분기한다.
- 2026-08-09: Unity `ManageScript`의 `newText`에 `\\n`을 전달했더니 실제 줄바꿈이 아니라 문자 `\\n`이 삽입되어 `using System;`이 주석에 포함됐고 기본 어셈블리 컴파일이 실패했다. 즉시 `apply_patch`로 실제 개행으로 교정한다. 이 강제 컴파일 덕분에 이전 일반 요청이 갱신되지 않았던 사실과 함께 `ICombatEquipmentRuntime`→fallback 포트 변환 오류도 드러났다.
- 2026-08-09: Unity `ManageScript.apply_text_edits`로 즉시 재임포트를 유도할 때 SHA 사전조건 없이 호출해 `precondition_required`가 반환됐다. 도구가 돌려준 현재 SHA를 넣어 동일한 원자 편집을 재시도한다.
- 2026-08-09: 동적 진단 어셈블리 버전을 `System.Reflection`으로 확인하려 했으나 Unity MCP가 해당 네임스페이스를 승인하지 않아 거부했다. 소스에 진단 버전 표식을 넣고 보고서 헤더로 확인한다.
- 2026-08-09: `findings.md`의 `## Combat Outcome Calibration` 제목을 추정해 패치했으나 실제 제목과 달라 실패했다. 문서 끝부분과 실제 제목을 확인한 뒤 정확한 위치에 추가한다.
- 2026-08-09: `OffenseBattleModel.ResolveOutcome`와 결과 프로브 범위를 한 호출에서 함께 읽으려다 도구 출력이 컨텍스트 한도를 초과해 잘렸다. 이후에는 50~80행 이하의 좁은 범위를 파일별로 따로 읽는다.

- 2026-08-09: Ordered V23 recipe rebuild stopped during workshop normalization because `recipe:ammo:armor-piercing-cartridge` was an existing research-overhaul asset without an authored process class. Cause: the first builder normalized the shared recipe root before later builders had migrated their owned assets. Required fix: normalize only each builder's owned recipes during migration, then run the full-root fail-loud audit after all builders complete; do not add a fallback process.
- 2026-08-09: The second ordered rebuild stopped at research recipe tag `workstation:v3:subterranean` (`work:craft`, Transform) because no exact process-class mapping existed. This is the intended fail-loud behavior; inspect the owned recipe definitions and assign a semantic process explicitly rather than using substring inference.
- 2026-08-09: The third ordered rebuild successfully rebuilt resource/workshop, research, and apparel recipes, then `SurgeryContentAssetBuilder.RebuildAll()` triggered the broader research-project consumer validator. It reported four produced items with zero real consumers: `component:factory-installation-plan`, `component:paper-paste`, `component:rune-bus-coupler`, and `tool:precision-gauge`. These are genuine production-graph defects, not recipe-process errors; they must receive real gameplay consumers or be reclassified/removed before the full gate can pass.
- 2026-08-09: Wiring `component:factory-installation-plan` through research-project ID failed because V21 consolidates `research:industry:factory-layout` into `research:industry:powered-tools`, so the original project is absent from the 180-node survivor dictionary. Fix must target the exact facility semantic tag rather than broad survivor-project unlocks, otherwise the component would incorrectly be added to every facility in the merged package.
- 2026-08-09: A broad EWU/source search returned useful matches but exited with code 1 because one combined search branch had no matches. No project mutation occurred; use narrower scoped searches instead of treating optional absence as a command failure.
- 2026-08-09: Initial EWU audit left 160+ items unresolved because crop, livestock, disease-sample, combat-loot, and other non-recipe domains are legitimate physical acquisition sources outside `ProductionRecipeSO`. The calculator must seed only leaf items with no recipe producer using an explicit non-market acquisition-work formula and report those seeds separately. The same run found medical facilities 9509/9510 at 86.6%/85.6% dismantle EWU because specialized medical facilities were incorrectly using the general-facility salvage class.
- 2026-08-09: External EWU seeding resolved all production items, but medical facilities 9509/9510/9512/9513 still exceeded the 85% salvage cap. Cause: their effective-use classification is a workstation/service classification, so construction-class-only salvage routing does not identify the medical domain. The fix must use an authored medical semantic/ability marker or add one in the medical builder, not numeric ID ranges or asset paths.
- 2026-08-09: A combined medical-facility search returned the required ability evidence but exited code 1 because the optional helper-signature branch had no exact match. No mutation occurred. Use the discovered authored medical/surgical abilities for classification and avoid optional multi-pattern commands.

| Error | Attempt | Resolution |
|---|---|---|
| Single-battle campaign became unwinnable at stage 3 | Direct Normal playthrough | Rejected the thin-loop balance patch; redesign offense around persistent multi-node expedition progression and dungeon support. |
| Initial audit searched a nonexistent `Assets/Scripts/Stock` folder | 1 | Located stock runtime under `Buildings/SO/StockInfo.cs` and warehouse query services. |
| Reward regression still expected launch-to-boss and victory full heal | 6 | Reworked it to traverse route nodes, resolve encounters, defeat the boss, grant rewards, and retain survivor injuries. |
| Product-shell verification clicked a recruit card behind the bottom HUD | 7 | Scrolled the card into a 140px bottom-safe region; the pointer-driven product shell then passed. |
| Full-campaign UI verifier selected old alert buttons with matching labels | 7 | Scoped pointer lookup to the active offense map, expedition, or battle panel. |
| QA batch runner introduced `CS1626` by yielding inside `try/catch` | 25 | Moved coroutine yields outside the exception-handled block before rerunning feature tests. |
| Physical item pile PlayMode verifier still used the legacy owner-option flow | 30 | Replaced it with the current start-party fast commit path and added a request-file runner; the pile verifier then passed. |
| Physical item batch wait script looked for `PhysicalItemPile: PASS` while the report writes `[PASS] PhysicalItemPile` | 33 | Treated the shell wait exit code as a harness-string mismatch, then verified the actual batch report, target reports, and Console directly. |
| Recruited staff disappeared from later direct-play expedition candidates | 26 | Made `WorldCharacterProfile.isStaff` authoritative during population bind/refresh/promote/release and prevented the spawner from returning staff profiles to the visitor pool. |
| Offense reward regression still expected instant warehouse stock after physicalization | 26 | Updated reward tests to accept warehouse delta plus physical dropoff stack delta, and aligned recruit-candidate expectations with the handler's minimum-two rule. |
| Physical item theft test could read a duplicate empty carry inventory | 32 | Marked `CharacterCarryInventory` as single-instance and updated fixtures to resolve inventories through `CharacterCarryInventory.Ensure`. |
| Runtime visual-inspection command referenced TMP and wildlife properties that do not exist | 46 | Read the concrete APIs, then queried the TMP `MeshRenderer` sorting data and `WildlifeActor.DisplayName`; the corrected command passed. |
| First zoom persistence fix targeted the legacy `UnityEngine.U2D.PixelPerfectCamera` type | 46 | Runtime component inspection found `UnityEngine.Rendering.Universal.PixelPerfectCamera`; switched the alias to the URP type and reran the pointer test successfully. |
| Pond route probe called a nonexistent two-argument `Grid.SearchPath` overload | 47 | Read the concrete Grid API and used `GetMovePath(start, endPredicate)` instead. |
| Pond route probe started on the occupied entrance-door cell and reported no generic move path | 47 | Tested continuity from the first exterior surface cell; the exterior route and all shallow pond cells are reachable while only the boundary deep-water cell blocks movement. |
| Unity MCP approval was revoked during exact world-click verification | 48 | Used the compiled in-project UI regression request runner to execute the same Input System pointer path and collect the final report without bypassing gameplay input. |
| Physical delivery worker reached the source but could not pick up | Construction material delivery PlayMode attempt 1 | Found warehouse storage IDs were based on shared building definition `GridId`; replace them with a unique building instance key before rerunning. |
| Physical logistics rerun request did not auto-enter PlayMode | Construction material delivery PlayMode attempt 2 | Request file remained pending with Editor idle; enter PlayMode explicitly so the registered runner consumes the same request. |
| PlayMode could not start after warehouse-key migration edit | Construction material delivery compile attempt 2 | Preserved `IWarehouseFacility` type while matching a `BuildableObject` instead of passing the narrowed base type to the storage-key helper. |
| Combined regional-pressure patch did not match mojibake reward strings | V16 regional pressure attempt 1 | Split the change into focused structural patches and replace handler bodies using ASCII-only method boundaries. |
| Assumed `InvasionIntruderRuntime.cs` was a standalone file | V16 invasion pressure audit | Located the runtime in `InvasionIntruderSystem.cs` and switched subsequent reads to the concrete file. |
| MCP checkpoint command referenced the wrong `CompilationPipeline` namespace | V16 regional pressure compile attempt 1 | Use the MCP command's own pre-execution project compilation with a simple `AssetDatabase.Refresh` command. |
| Initial item-authority audit assumed `WildlifeItemDefinitions.cs` lived under `Services/Wildlife` | 76 | Locate the symbol by `rg --files` before reading the concrete definition file. |
| Assumed character body-health models were in a standalone file | V16 return-arrival audit | Locate the concrete interface by symbol before reading; the captivity eligibility check itself was confirmed in `CaptivityRuntime.cs`. |

## Dark Survival V11 Completion

- [x] Add per-character deprivation burdens, health damage, probabilistic/forced breakdowns, and BT priority handling.
- [x] Add desperate relief, unsafe-water drinking, starvation violence/cannibalism, collapse, and nonlethal suppression paths.
- [x] Add physical exterior water, floor filth, wall stains, clean work targets, humanoid corpse metadata, and emergency butchery.
- [x] Add the character health tab, world breakdown warning, filth information/priority command, and V11 persistence.
- [x] Verify focused EditMode contracts, pointer-driven PlayMode behavior, camera/screen captures, and Console `Error 0 / Warning 0`.

## Exterior Habitat Decoration Completion

- [x] Build one static wildlife decoration palette from the authored TINY FOREST flower, tree, and rock sprites.
- [x] Place deterministic, nonblocking flowers, trees, and rocks only on walkable exterior surface cells.
- [x] Bind Grass/Brush flower density to habitat resource so grazing removes flowers and regeneration restores them progressively.
- [x] Keep decoration runtime state derived from habitat patches; do not add duplicate save data or per-decoration SO assets.
- [x] Verify EditMode contracts, the live herbivore grazing loop, hierarchy cleanup, PlayMode snapshot, camera capture, and Console `Error 0 / Warning 0`.

## Exterior Pond Visibility Completion

- [x] Exclude the entrance and drop zone from default water generation.
- [x] Place one bounded four-cell pond at the outer edge of the longest exterior surface run.
- [x] Ground-align the water visual, unlock per-cell tint, and render a readable pixel-water strip above terrain.
- [x] Keep three shallow cells walkable, the outer deep cell blocked, and the exterior route connected.
- [x] Verify runtime positions, tile occupancy, camera capture, focused contracts, and Console `Error 0 / Warning 0`.

## Zoom Sky / Centered Dungeon Completion

- [x] Resize and reposition the solid sky from the live orthographic camera viewport whenever zoom, aspect, or camera position changes.
- [x] Center the 27-column dungeon interior inside the 60-column physical world and shift every authored GameplayScene placement by the same offset.
- [x] Center the gameplay camera on the resolved dungeon interior at scene start.
- [x] Verify left and right outer-wall tiles after the shift and visually inspect the maximum zoom-out frame.
- [x] Run physical-world, background-lighting, and grid-foundation regressions with Console `Error 0 / Warning 0`.

### Verification Notes

- Minimum zoom: camera Y `1.25..7.75`, sky Y `-2..11`, coverage `true`.
- Maximum zoom-out: camera Y `-6..15`, sky Y `-8..17`, coverage `true`.
- Runtime dungeon interior is Grid X `17..43`, authored placement shift is `+13`, and camera X matches the dungeon world center at `-29.5`.
- A broader runtime-composition policy scan still reports unrelated pre-existing direct-access violations in other systems; the focused changed-surface regressions pass.

## Entrance Outer-Wall Adjacency Fix

- [x] Reproduce the one-cell gap beside the centered dungeon entrance.
- [x] Exclude characters, wildlife, items, and nonstructural exterior markers from automatic side-wall structure detection.
- [x] Confirm the outer wall moves from Grid X `12` to the correct adjacent cell X `13` beside the three-cell dungeon door.
- [x] Add a marker-overlap regression and verify the repaired entrance with Unity MCP Camera Capture.
- [x] Run grid visual, foundation, and physical-world regressions with Console `Error 0 / Warning 0`.

## Exact Facility World Click Completion

- [x] Remove the arbitrary `GridCell.GetBuilding()` fallback from ordinary facility selection.
- [x] Require an actual `Physics2D.OverlapPointAll` collider hit for facilities and construction sites.
- [x] Keep exact-cell fallback only for structural walls and interior doors that are rendered without normal colliders.
- [x] Reject hallway/floor definitions even if a hallway collider is present.
- [x] Verify actual facility click, bare hallway click, character-over-building priority, and exclusive info panels through Input System pointer events.
- [x] Finish with the UI regression batch at `RESULT=PASS`, captured `Error 0 / Warning 0`.

## Consecutive Wildlife Click Completion

- [x] Reproduce the same-animal consecutive click failure in the popup lifecycle.
- [x] Close the previously registered popup before assigning the newly clicked wildlife target.
- [x] Add current-target/open-state diagnostics and a repeated-event regression.
- [x] Add two consecutive Input System pointer clicks to the world-info PlayMode verifier.
- [x] Verify wildlife contracts, UI regression batch, and Console `Error 0 / Warning 0`.

## Wildlife World-Facing Completion

- [x] Trace wildlife facing against the project's mirrored Grid-to-world X mapping.
- [x] Derive horizontal facing from world-space movement instead of logical Grid X delta.
- [x] Update the natural-motion regression to assert left/right in world space.
- [x] Verify every wildlife species present in GameplayScene in both horizontal directions.
- [x] Finish with wildlife contracts and Console `Error 0 / Warning 0`.

## Defense Interception And Engagement V12

- [x] Audit current invasion movement, guard commands, defense UI, DI, persistence, and compile state.
- [x] Add adjacent-cell interception, reciprocal combat, one lead guard, and one replacement guard.
- [x] Add RimWorld-style defense policies assigned per guard and owner evacuation to an administration room.
- [x] Connect manual suppression, skill events, combat presentation, player-facing status, and defense UI.
- [x] Persist policies, assignments, owner evacuation, and active engagements in V12 saves.
- [x] Verify focused contracts, pointer-driven PlayMode combat, captures, and Console gameplay `Error 0 / Warning 0` after the known Unity 6000.3.8 startup warning.

### Defense Decisions

- Automatic interception is limited to on-duty non-owner staff with Guard priority enabled.
- Melee combat is one blocker versus one intruder on separate adjacent cells; a second guard may wait behind for replacement but cannot attack through the lead guard.
- Defense behavior is configured through named policies and each guard is assigned one policy.
- The owner never auto-dispatches. Every invasion cancels the owner's current action and evacuates them to an Administration room, or the farthest reachable interior safe cell when no valid room exists.
- Empty frontline means the intruder resumes advancing immediately; zero health continues to use the existing permanent-death flow.

## Developer Mode And Debug Palette

- [x] Add settings schema V2 with developer mode disabled by default and a dedicated Development tab.
- [x] Add a center-top Debug button, responsive non-modal palette, search, numeric input, eight command tabs, and exact world targeting.
- [x] Register 112 modular commands across cheats, spawning, characters, building/work, survival/wildlife, defense/events, overlays, and history.
- [x] Connect persistent `debugModified` metadata and a 50-entry command history while resetting transient cheats and overlays after load.
- [x] Verify pointer targeting, Shift repeat, right-click/Escape cancellation, commands, invasions, save behavior, overlays, and both supported aspect ratios.
- [x] Finish with EditMode PASS, PlayMode `RESULT=PASS`, Camera Capture comparison, and Console `Error 0 / Warning 0`.

## Construction Material Physical Delivery

- [x] Trace construction placement, delivery request, warehouse reservation, pickup, and site deposit.
- [x] Remove any construction-site material spawning or teleporting at placement time.
- [x] Keep materials physically stored until a worker picks up the reserved quantity and deposits it into the site buffer.
- [x] Add regressions for no-stock waiting, partial reservation, pickup, deposit, and construction readiness.
- [x] Verify the live placement-to-haul flow and Console `Error 0 / Warning 0`.

## Medieval Dark Fantasy Combat V13

- [x] Add shared melee, ranged, and recoverable-throw resolution with range bands, fire modes, evasion, directional cover, friendly-fire gating, armor penetration, body parts, bleeding, suppression, and pause-safe presentation.
- [x] Add individual weapon, armor, shield, and ammunition data plus persistent equipment instances, quality, armor durability, loadouts, reloads, crafting recipes, and V13 save state.
- [x] Connect defense to rally-time physical loadout pickup, post-breach melee interception, ranged line-of-sight combat, reciprocal damage, owner evacuation, and recoverable thrown equipment.
- [x] Connect offense to the same resolver, formation distance, cover, weapon switching, ammunition, body-part injuries, suppression turn loss, and persistent return-state wounds.
- [x] Add combat UI, cover buildings, exact multi-select/direct movement commands, fire-mode/hold-fire controls, and player-facing combat status.
- [x] Connect wildlife hunting and retaliation to the shared combat resolver, ranged firing positions, scaled-time reloads, simplified persistent body profiles, and real armor/body damage on hunters.
- [x] Verify static combat/offense/defense/priority/wildlife contracts, PlayMode wildlife loop, defense rally and engagement, direct movement and cancellation, visual capture, and Console `Error 0 / Warning 0`.

### V13 Verification Notes

- Defense PlayMode: rally held outside, four reciprocal exchanges on distinct adjacent cells, both sides damaged, intruder movement and facility attacks locked, owner evacuation and save snapshot valid.
- Player command PlayMode: `Cain (19,0) -> (17,0)` completed with manual lock released; a second move cancelled immediately also released its lock.
- Wildlife PlayMode: runtime snapshot and hunt/carcass/butcher loop passed; limb injury lowers mobility and survives capture/restore.
- `ScreenCapture`: `Artifacts/QA/combat-v13-defense-final.png`.
- Unity MCP `Camera_Capture` was attempted twice against the live Main Camera but the connector returned `Failed to render scene preview`; the direct Game View capture rendered correctly.

## V16 Isolated Feature Integration

- [x] Audit duplicate scene runtimes, legacy equipment, abstract rewards, food consumption, exterior incidents, circus milestones, extract resources, and AI performance diagnostics.
- [x] Remove duplicate GameplayScene command/customer runtimes and enforce exact-one composition lookup.
- [x] Remove the legacy expedition equipment stack and make common combat equipment authoritative for crafting, storage, loadout, defense, and offense.
- [x] Replace abstract offense weakening and reward counters with regional pressure and physical return arrivals.
- [x] Connect exterior incidents, reception, patrol readiness, weather, sanitation, and night danger to physical actors and outcomes.
- [x] Remove daily abstract food withdrawal and make completed character meals the sole nutrition consumption path.
- [x] Connect circus fame milestones, injury gating, Biological blood, and Knowledge memory residue to work-unit consumers.
- [x] Wire allocation-free AI performance recording and remove unused expedition support ability and mojibake on changed surfaces.
- [x] Finish broad regressions, pointer-driven PlayMode verification, captures, performance checks, and Console `Error 0 / Warning 0`.

### V16 Performance Closure

- [x] Split full Grid content changes from structural/traversal changes so items, wildlife, and filth do not invalidate route and room caches.
- [x] Preserve current wildlife target reachability without relying on stale cached occupant positions.
- [x] Repair wildlife arrival dwell to use one game-clock time base and pass Grid/Wildlife/AI focused regressions.
- [x] Re-run 100-NPC EditMode stress: elapsed `353s -> 50.6s`, broker searches `1440 -> 51`, deferrals `16461 -> 50`, Scheduler p95 `0.73ms`.
- [x] Run PlayMode profiling and the broad V16/domain regression matrix.
- [x] Perform current visual capture and final stopped-editor Console audit.

### V16 Verification Notes

- Broad domain matrix passed for V16 integration, save sections, survival, exterior activity,
  captivity/circus, offense reward/battle, combat, defense, work amount, Grid, AI naturalness,
  wildlife, and physical items.
- Pointer-driven UI verification passed `21/21` rows at `1600x900` and `900x1600`, including
  alert right-click dismissal, with captured `Error 0 / Warning 0`.
- The stabilized 100-character PlayMode profile recorded frame `2.77ms average / 3.42ms p95`
  and scheduler `0.370ms average / 0.497ms p95 / 0.632ms max`, with all 100 behavior trees
  ticked and no decision/path-budget overflow.
- Unity Editor-wide GC averaged `182KB/frame`; subtracting the measured one-character Editor
  baseline of about `120KB/frame` leaves about `62KB/frame` attributable to the stress world.
  The Mono backend does not support `GC.GetAllocatedBytesForCurrentThread`, so the report marks
  scheduler-only allocation as unsupported instead of falsely reporting zero.
- `Artifacts/QA/v16-gameplay-world.png` and
  `Temp/p1-p2-ui-surface-verification.png` provide current world and HUD evidence. Direct
  `Camera_Capture` still returns `Failed to render scene preview`; direct Game View capture works.

### V16 Decisions

- V16 is new-game only; V15 and older saves are rejected with a Korean explanation.
- Common combat equipment is the only authoritative equipment runtime.
- Food is consumed only when a character completes a real meal.
- Prisoners, special wildlife, and recruits return as physical or persistent world entities rather than counters.
- Strategic pressure is regional with a 25% same-faction spillover.
- Blood and memory residue remain physical resources with multiple work-based consumers.

## V17 Weighted Navigation and 500-Character Performance

- [x] Add deterministic terrain/traversal costs and weighted path results.
- [x] Use exact A* for fixed destinations and weighted Dijkstra for multi-target selection.
- [x] Add versioned broker caching, bounded search budgets, and reusable search workspaces.
- [x] Replace per-frame actor polling with due-time scheduling and immediate dirty wakeups.
- [x] Remove benchmark scene scans and hot-path decision/presentation allocations.
- [x] Pass focused Grid/100-character regressions and the staged 500-character profile.

### V17 Verification Notes

- 500 actors, 600 sampled frames: 3.39 ms average, 4.37 ms p95, 15.40 ms maximum,
  and 0 frames over 16.67 ms.
- Scheduler average/p95/max: 1.228/1.809/2.580 ms.
- Broker: 527 searches, 8,674 cache hits, bounded at 7 searches and 8 deferrals per frame.
- Incremental GC after the same-world Editor baseline: 36.0 KB/frame.
- Per-request Jobs/Burst are intentionally deferred: current weighted A* measures about
  11.3 microseconds/query, below practical scheduling overhead. Future parallelization must
  batch immutable offscreen work.

## Item Architecture V6

- [x] Phase 76: Audit all item-definition authorities, hardcoded fallbacks, side tables, saves, and generators.
- [x] Phase 77: Add canonical `ItemDefinitionSO`, typed IDs, composable features, and strict validation.
- [x] Phase 78: Generate canonical SO assets for resource, equipment, survival, medical, wildlife, industrial, and special items.
- [x] Phase 79: Replace permissive lookup chains with one strict indexed catalog and compatibility-only adapters.
- [x] Phase 80: Add versioned instance components, stack signatures, persistence, hauling propagation, equipment and freshness bridges.
- [x] Phase 81: Regenerate, clean-compile, run V3/research/pacing regressions, update docs, capture through Unity MCP, and finish Console 0/0.

### Item V6 Verification Notes

- 296 canonical item SOs; 43 equipment item features; duplicate IDs 0; invalid features 0.
- 110 generated compatibility/equipment assets, all with valid concrete script references.
- Stack-component signature isolation and hauling/carry-save/deposit propagation pass.
- Production V3 and research/equipment regressions pass; pacing is 32.2/80.4/234.3/372.0 days.
- Unity MCP Main Camera capture is 1920x1080; final Console is Error 0 / Warning 0.
- The legacy physical-item verifier still names its global save assertion `save_v10_contract`
  and expects V10 although the current global contract is V17.

## Runtime Data, SO, and Save Authority Normalization V18

- [x] Phase 82: Establish the V18 new-game-only boundary and executable architecture baseline.
- [x] Phase 83: Make authored SO catalogs the only content-definition authority and remove item fallbacks.
- [x] Phase 84: Introduce mandatory typed persistent IDs for item, character, and building instances.
- [x] Phase 85: Make physical item instances authoritative for warehouse stock and equipment state.
- [x] Phase 86: Move mutable `GameData` and static run state into scoped session services.
- [x] Phase 87: Consolidate legacy and V17 offense into one runtime and save aggregate.
- [x] Phase 88: Add staged, preflighted, atomic aggregate restore for V18 saves.
- [x] Phase 89 planning entry retired: optional required-interface DI and `Bind*Runtime` are already zero; remaining asmdef/static closure is tracked only by Phase 112 Batches F–I.
- [x] Phase 90 planning entry retired: remaining Roslyn validation, decomposition, and localization adoption are tracked only by Phase 112 Batches F/J/K/L.
- [x] Phase 91 planning entry retired: the full regression/capture gate is tracked only by Phase 112 Batch N.

### Phase 88 detached-root follow-up

- [x] Make physical items, production, combat equipment, character environment, and treasury economy restore through replaceable Aggregate roots.
- [x] Add a composition-wide candidate root and publish migrated Aggregate slots with one successful root swap.
- [x] Remove combat equipment's duplicate physical equipment/module restore writes.
- [x] Move dark-survival deprivation, world-water, world-filth, and character-consumable state into detached Aggregate slots; delay Unity terrain/tile/work-target projection until the published root is observed.
- [x] Move husbandry animals/policies, captives/policies/sequences, and captured wildlife into detached Aggregate slots; defer door, carry-parent, actor capture, warp, and other scene projections until publication.
- [x] Move deterministic random-stream state into the composition root while preserving stable injected stream handles across root publication/discard.
- [x] Move run seed/day/variable/replay state into the composition root and add type-level copy-on-write for mutations during shallow candidate staging.
- [x] Make meta-profile merge copy-on-write and move per-run meta progress/result lifecycle into replaceable root slots.
- [x] Move research task/progress/queue/unlock state and knowledge-residue processing into replaceable Aggregate slots; defer queue/workforce projection until publication.
- [x] Move Codex entry/title/information-line state into a deep-copy Aggregate slot and replace live clear/repopulate restore with strict detached decoding.
- [x] Move regular-customer visit/recruitment records into one deep-copy Aggregate slot and derive recruited-result views instead of storing a second list.
- [x] Move facility-shop offer day and purchase unlocks into one Aggregate slot, remove duplicated research unlock data from its save section, and rebuild deterministic offers only outside candidate commit.
- [x] Move power, fluid, conveyor, and automation infrastructure state into four deep-copy Aggregate slots; make automation demand a root-derived projection and add strict industrial save preflight.
- [x] Move event-alert history, dismissals, and ID sequencing into a deep-copy Aggregate slot; validate one DTO contract at every restore entry point and rebuild Unity UI only after root publication.
- [x] Move operating-day ledgers, debt, and report history into a deep-copy Aggregate slot; share strict nested payload validation and prove candidate discard preserves the live ledger.
- [x] Move work-order progress/sequence state into a deep-copy Aggregate slot; prepare construction sites inactive on the detached facility Grid and publish them in the `100 facilities -> 150 sites -> 200 characters` world boundary.
- [x] Move wildlife population/raid scheduling into one runtime Aggregate, prepare inactive actors on the detached Grid, and publish population, ecosystem, and carcass projections at participant `250.world.wildlife`.
- [x] Make exterior activity the sole owner of exterior-zone markers, exclude them from facility persistence, and publish inactive restored zones at participant `300.world.exterior-zones`.
- [x] Move offense return-arrival queues/barriers into a replaceable Aggregate and defer prisoner/wildlife materialization until normal post-publication ticking.
- [x] Planning entry retired; the exact remaining save-owner list and rollback-image removal are tracked only by Phase 112 Batches A–E.

### Phase 96 — AIBrain responsibility closure

- [x] Replace the 12-parameter AIBrain construction path with explicit decision/execution capability bundles.
- [x] Extract authored action-list configuration from mutable decision state.
- [x] Give action evaluation, cooldowns, resumable candidate scoring, continuation policy, path search, and debug formatting dedicated owners.
- [x] Reduce `AIBrain` from 2,319 lines to the enforced 1,200-line runtime boundary and remove its baseline exception.
- [x] Planning entry retired; final AI import/regression proof is tracked only by Phase 112 Batch N.

### Phase 97 — Defense engagement responsibility closure

- [x] Replace the 16-parameter defense engagement constructor with two explicit eight-capability service bundles.
- [x] Move ranged-position planning and ranged-support movement/fire state to dedicated owners.
- [x] Move defense save mapping/restore interpretation, guard pause control, and engagement combat lifecycle to dedicated owners.
- [x] Reduce `DefenseEngagementRuntime` from 2,258 lines to the enforced 1,200-line boundary and remove its baseline exception.
- [x] Planning entry retired; final defense regression proof is tracked only by Phase 112 Batch N.

### Phase 98 — Surgery runtime responsibility closure

- [x] Replace the 28-parameter surgery constructor with four explicit capability bundles.
- [x] Move order validation, save mapping, environment recovery, and patient/material logistics to dedicated owners.
- [x] Remove stock-category-derived medical material IDs in favor of concrete authored item IDs.
- [x] Reduce `SurgeryRuntime` from 2,565 lines to 1,168 lines and remove its baseline exception.
- [x] Planning entry retired; final surgery/save regression proof is tracked only by Phase 112 Batch N.

### Phase 99 — Strategic offense presentation closure

- [x] Separate strategic preparation/factions, encounters, view construction, and detail projection from the screen coordinator.
- [x] Keep every strategic presentation source below the 800-line Presenter limit.
- [x] Reduce `OffenseWorldMapPanelStrategic.cs` from 2,044 lines to 528 lines and remove its baseline exception.
- [x] Planning entry retired; both strategic layouts and pointer flow are tracked only by Phase 112 Batch N.

### Phase 100 — Wildlife runtime responsibility closure

- [x] Replace the 20-parameter wildlife constructor with world, combat, and execution capability bundles.
- [x] Move hunt combat to `WildlifeHuntRuntime` and food-raid/ecology movement to `WildlifeBehaviorRuntime`.
- [x] Remove the hunter-name reservation-key fallback and require typed character identity.
- [x] Reduce `WildlifeRuntime` from 2,513 lines to 921 lines; all runtime helpers remain below 1,200 lines.
- [x] Remove the final runtime architecture-baseline exception; remaining exception count is zero.
- [x] Run wildlife, hunt, food-raid, save, and ecology regressions through Unity after MCP reconnects.

### V18 Decisions

- V17 and older saves are rejected with `대규모 데이터·식별자·저장 구조 개편 이전 저장 — 새 게임 필요`; there is no automatic migration.
- Authored SO assets and one explicit root catalog are the content source of truth. Editor builders are bootstrap/migration tools only.
- Each phase removes the old write path before completion; no dual-write compatibility layer may survive a phase boundary.
- ScriptableObjects contain immutable authored definitions only. Mutable run state belongs to scoped plain C# services and versioned save DTOs.
- Derived indexes are allowed only when they are non-persistent and fully rebuildable from authoritative state.

### V18 Authority Verification Notes

- Global save root is V18 and V17-or-older slots are rejected through one compatibility policy with the exact new-game-required message.
- `GameContentCatalogSO` is the single Resources bootstrap root; its explicit item catalog currently contains 772 validated SO definitions.
- The obsolete `DungeonItemCatalogSO` type/asset, code-owned item-definition factories, unknown-item synthesis, and abstract `stock-item:*` authored inputs are removed.
- Dynamic evolution drops resolve to 147 authored catalyst SOs and 21 authored residue SOs; potency is bounded to the authored 1-21 range.
- `RuntimeAuthorityV18Validator` passes with legacy item authority 0, duplicate/invalid item definitions 0, and Unity Console Error 0 / Warning 0.
- Item stacks, unique items, characters, buildings, and warehouse destinations now use distinct typed persistent IDs; warehouse storage keys no longer fall back to grid coordinates or object hashes.
- The registered `IStockQuery` is a rebuildable view over physical stacks, and equipment item-state schema V2 round-trips the full equipment snapshot plus attached module state.
- `WarehouseInventory` owns only capacity/category policy; all runtime and Editor aggregate `Deposit/Withdraw/AddStock` entry points are gone.
- `items.physical` V6 is the only equipment/module instance save authority. `combat.equipment` V6 stores loadouts, work orders, material policies, lineage orders, and seal claims only.
- Equipment creation, physical materialization, carry, storage, facility buffering, and save/restore preserve one typed `ItemInstanceId`; mismatched and duplicate identities fail explicitly.
- Phase 85 focused contracts pass for physical items, physical stock queries, building persistence, facility evolution, combat equipment, material equipment, and the 168-node research/equipment overhaul.
- `GameData` now contains authored starting settings only. Mutable money, calendar, pause, and speed live in a plain run-scoped `GameSessionState` and are changed through `IGameMoneyAccount`, `IGameCalendar`, and `IGameSpeedController`.
- Character carry lookup, combat-cover durability, skill execution deduplication, user settings, and presentation/skill catalog access no longer use static mutable run registries or runtime SO synthesis.
- The root catalog explicitly references authored world-presentation and character-skill settings assets; the V18 validator enforces this SO/session boundary and the removed global registries.
- Phase 86 focused regressions pass for V18 authority, physical items, combat, facility shop, operating-day settlement, developer mode, invasion, and UI lighting. Unity Console is Error 0 / Warning 0.
- The four offense save authorities were replaced by the sole `offense.aggregate` section. All V17 names and the late strategic runtime bind were removed, and non-offense runtime code now uses `IOffenseQuery`/`IOffenseApplication` rather than scene MonoBehaviour providers.
- Expedition rewards materialize through the physical reward item sink; reward state no longer writes aggregate warehouse stock. Strategic, expedition, map, reward, recruitment, and save-section regressions pass together.
- V18 saves now carry an explicit compatibility manifest. Restore preflights manifest/sections, typed JSON, persistent identities, authored item/building references, and offense-to-character references before mutation.
- A full rollback image is captured before commit. The injected final-section failure regression proves earlier mutations are reverted, and the live 54-section PlayMode save round trip passes.
- Phase 90 decomposition progress: `CharacterDeprivationRuntime` is 1,123 lines and no longer needs an architecture-baseline exception; safe-relief planning/execution, emergency movement, breakdown actions, world access, and consequences are focused collaborators below their limits.
- Phase 90 decomposition progress: `FluidNetworkRuntime` is 1,199 lines after extracting node-water rules and snapshot projection. The architecture baseline is down to 40 exceptions, clean Unity compilation is Error 0 / Warning 0, and the V18 authority plus industrial infrastructure regressions pass.
- Phase 90 decomposition progress: `ExteriorActivityRuntime` is 1,101 lines after moving the stateful `ExteriorZoneMarker` facility into its own source owner. The baseline is down to 39 exceptions and exterior regressions pass.
- Phase 90 decomposition progress: `WildlifeEcosystemRuntime` is 1,142 lines after separating habitat definitions/markers and the rebuildable overlay cache. Wildlife regressions pass and the baseline is down to 38 exceptions.
- Phase 90 decomposition progress: `AnimalHusbandryRuntime` is 1,200 lines after moving auto-slaughter/compatibility policy and reusable work rules into focused collaborators. Clean compilation and V18 validation pass; 37 exceptions remain.
- Phase 90 decomposition progress: `CircusRuntime` is 1,200 lines after extracting forecast/venue calculations, combatant values, and world queries. Captivity/circus regressions pass; 36 exceptions remain.
- Phase 90 decomposition progress: the industrial surface presenter is 781 lines and the character-summary runtime factory is 800 lines after extracting their separate tab/layout owners. UI architecture validation passes; 34 exceptions remain.
- Phase 90 decomposition progress: settings UI is 788 lines and owner selection is 765 lines after extracting platform resolution/input and view-only rules. Owner regressions pass; 32 exceptions remain.
- Phase 90 decomposition progress: `UIBuildingInfo` is 774 lines after extracting action/progress/status view creation. Facility fixtures now provide typed building IDs and stock-query capability, aggregate stock-supply fallback is removed, and V18/facility regressions pass; 31 exceptions remain.
- Phase 90 decomposition progress: `DungeonTitleUiController` is 796 lines after extracting canvas/EventSystem lifetime and title text/slot formatting. Clean compilation and V18 validation pass; 30 exceptions remain.
- Phase 90 decomposition progress: the warehouse feature source is 745 lines after extracting mutation commands. Production fixtures now issue typed building IDs; clean DLL rebuild, V18, UI architecture, and production-economy regressions pass; 29 exceptions remain.
- Phase 90 decomposition progress: `ProductionBuildingPanelPresenter` is 752 lines after extracting workshop-link rendering and stateless production view creation. Clean compilation and production/UI regressions pass; 28 exceptions remain.
- Phase 90 decomposition progress: the defense model/presenter source is 412 lines after moving query and command implementations to their own owners. Defense threat/engagement/report regressions pass; 27 exceptions remain.
- Phase 90 decomposition progress: surgery application service and MonoBehaviour view are separate 693/457-line owners. All stale 141-research fixture assertions are updated to 168, and surgery regressions pass; 26 exceptions remain.
- Phase 90 decomposition progress: the operations model/presenter source is 532 lines after extracting query and command owners. A broken AI settings script reference is repaired, surgery/research tests no longer rebuild content, and operations/content regressions pass; 25 exceptions remain.
- Phase 90 decomposition progress: `WorkTargetSelector` is 1,160 lines after extracting target eligibility, environment assessment, exterior-work rules, and scan state. Isolated UI participants and typed construction-site fixture IDs repair two stale test authorities. Clean compilation, V18 authority, work-priority/corner-case/work-amount/naturalness regressions pass; 16 exceptions remain.
- Phase 90 decomposition progress: `CharacterBodyHealthRuntime` is 1,050 lines after moving contracts and deterministic state normalization/anatomy projection into focused owners. Clean compilation, V18 authority, combat, anatomy-medical integration, and surgery regressions pass; 15 exceptions remain.
- Phase 90 decomposition progress: invasion director and intruder are separate owners; defense observation, awareness-aware path planning, and combat math/status rules are extracted. `InvasionIntruderRuntime` is exactly 1,200 lines. Clean compilation plus threat/intruder/engagement/report regressions pass; 14 exceptions remain.
- Phase 90 decomposition progress: `SurvivalFoodRuntime` is 1,192 lines after extracting state persistence, physical-stock access, spoilage/freshness synchronization, meal ledger, health rules, and facility-work rules. Its meal ledger now requires typed building identity. The physical-craft fixture uses the root SO material catalog and a persistent facility ID. Clean compilation plus V18 authority, survival, physical-stock, and physical-item regressions pass; 13 exceptions remain.
- Phase 90 decomposition progress: `Shop` is 1,196 lines after extracting product inventory/pricing, crime resolution, service completion, and save/read contracts. The legacy money adapter, implicit feedback fallback, and cached mutable session provider are removed. The architecture baseline is down to 12 exceptions; facility regressions are being normalized to mandatory typed IDs and physical-stock-only warehouse semantics.
- Phase 90 decomposition progress: `CaptivityRuntime` is 1,197 lines after separating policy ownership, performer progression, management interactions, escort state, escape planning, and lifecycle/save state. Housing persistence now uses `BuildingInstanceId` rather than type/coordinates. The architecture baseline is down to 11 exceptions; standalone Unity Roslyn compilation is Error 0 / Warning 0 while Unity MCP regression execution awaits bridge recovery.
- Phase 90 decomposition progress: `OffenseBattleModel` now contains only the 1,170-line battle session; contracts, encounter content, and deterministic session rules have separate owners. `Grid` is 1,166 lines after separating cell rules, path results, search workspaces, and traversal-heuristic indexing. The architecture baseline is down to 9 exceptions and standalone Unity Roslyn compilation remains Error 0 / Warning 0.
- Phase 90 decomposition progress: the 689-line `OffenseExpeditionPanel` MonoBehaviour now has its own source owner instead of sharing `OffenseExpeditionSystem.cs` with the expedition Aggregate. The remaining expedition runtime is 2,105 lines and still requires strategic-travel/battle-result decomposition before its exception can be removed.
- Phase 91 expedition aggregate decomposition: `OffenseExpeditionRuntime` is now 1,117 lines. Field mobility, result finalization, asynchronous return processing, strategic target/travel handling, battle launch, and battle completion are explicit services; the runtime line-limit exception was removed.
- Phase 92 production-order decomposition: `ProductionBillRuntime` is now 1,164 lines. Output reservations, stock-sensor ownership, utility validation, input logistics, save mapping, and query projection each have one explicit owner; the production line-limit exception was removed.
- Phase 93 equipment aggregate decomposition: `CombatEquipmentRuntime` is now 864 lines with exactly eight required constructor dependencies. Craft orders/material policies/unlock checks live in `CombatEquipmentCraftingRuntime`; loadout references, hand/layer policy, snapshots, confiscation, and character-death loss live in `CombatEquipmentLoadoutRuntime`; physical equipment and module payloads remain owned only by `IItemInstanceRepository`. The runtime no longer constructs its own policy implementations, equipment crafting no longer converts `StockCategory` to an abstract item, and the line-limit exception was removed.
- Phase 94 physical-item aggregate decomposition: `WorldItemStackRuntime` is now 1,030 lines with exactly eight required dependencies. Persistence, warehouse routing, theft, and read/mutation facets have explicit owners. Restore now validates a complete `WorldItemRestoreState` before clearing the live repository, and warehouse stock is never synthesized from `WarehouseInventory`. The line-limit exception was removed; five oversized-runtime exceptions remain.
- Phase 95 V18 architecture-ratchet repair: stale V15/2,169-line tests now enforce save V18 and the shared 1,200/800 architecture baseline. Mutable static declarations use an explicit cache/profiler approval set instead of a numeric allowance. Wildlife habitat and industrial infrastructure persistence now require typed generated IDs; scene-transition requests live in a persistent mailbox rather than static fields.

### Phase 105 — Authored taxonomy authority cutover

- [x] Author 6 character needs, 11 stock categories, and 8 building categories on `GameDomainContentCatalogSO`.
- [x] Project those records through the immutable, injected `AuthoredGameplayCatalog`.
- [x] Remove the three mutable static catalogs and all production/Editor call sites.
- [x] Keep stock persistence IDs as a fixed V18 protocol while display and balance data remain SO-authored.
- [x] Pass Unity V18 authority and authored taxonomy validation after a Unity-native reimport.
- [x] Build facilities and characters as inactive restore candidates and register both as final transaction participants.
- [x] Restore characters against the detached facility Grid and quiesce the live character world only at final publication.
- [x] Planning entry retired; this scope is tracked only by the exact Phase 112 Batches A–E.

### Phase 106 — Character medical detached restore authority

- [x] Move medical orders and sequence into one replaceable Aggregate slot.
- [x] Reject malformed medical payloads and broken patient/facility references before live publication.
- [x] Prepare downed-character Grid occupants on the detached facility Grid and publish them at participant order `350.world.medical`.
- [x] Convert `combat.medical` to the shared typed JSON preflight boundary and remove the legacy warning/skip restore path.
- [x] Split restore orchestration from `CharacterMedicalRuntime`; the runtime source is 1,199 lines and has eight required dependencies.
- [x] Add a public save-service PlayMode regression for invalid medical preflight preserving the live Aggregate view.
- [x] Execute medical PlayMode restore scenarios and Console verification through Unity MCP after transport recovery.

### Phase 107 — Character combat-command detached restore authority

- [x] Store combat commands, stance membership, actor revisions, and the command ID sequence in one Aggregate.
- [x] Upgrade `combat.commands` to strict V2 typed preflight; do not migrate or normalize the removed V1 contract.
- [x] Validate candidate actors, targets, cells, and physical weapon instances before replacing the Aggregate slot.
- [x] Publish AI pause/stance presentation only at participant order `400.world.combat-command-stances`.
- [x] Replace fourteen direct runtime dependencies and four internally constructed collaborators with three explicit capability groups plus the Aggregate root.
- [x] Make wildlife world queries candidate-aware so downstream combat target validation never observes the retired live population.
- [x] Add public save-service invalid-preflight preservation coverage and pass runtime/Editor auxiliary compilation.
- [x] Convert defense-tactical reservations and equipment-maintenance orders, the remaining legacy combat save boundaries.

### Phase 108 — Defense-tactical reservation Aggregate

- [x] Persist the reservation ID sequence with the reservation set in one Aggregate.
- [x] Upgrade `combat.defense-tactics` to strict V2 typed preflight.
- [x] Validate canonical IDs, actor/cell uniqueness, enums/scores, candidate actors/targets, and candidate Grid walkability before replacement.
- [x] Replace one Aggregate slot without clearing or normalizing live reservations.
- [x] Add public save-service invalid-preflight preservation coverage and V18 source ratchets.
- [x] Convert `combat.equipment-maintenance`, the final legacy combat save boundary in this group.

### Phase 109 — Equipment-maintenance Aggregate and final combat save boundary

- [x] Move maintenance policies, assignments, orders, and both ID sequences into one replaceable Aggregate.
- [x] Upgrade `combat.equipment-maintenance` to strict V2 typed preflight with authored item/facility/equipment validation.
- [x] Remove coordinate-derived facility persistence, duplicate material fields, warning normalization, and live-clear restore.
- [x] Add public invalid-preflight preservation coverage and V18 source ratchets.

### Phase 110 — Runtime size and MonoScript identity closure

- [x] Split `BuildableObject`, husbandry work rules, and fluid-network projection below runtime limits.
- [x] Move run-variable and meta-run Aggregate types away from scene-bound MonoScript GUID owners.
- [x] Keep medical and combat-command behavior additions below 1,200 lines through focused partial owners.
- [x] Pass auxiliary runtime/Editor compilation and Unity V18 authority validation.

### Phase 111 — Unity MCP tactical and medical regression closure

- [x] Replace category-only medicine fixtures with authored physical medicine stacks.
- [x] Make body health the sole downed/recovered authority and prevent ambulatory rescue-order fabrication.
- [x] Complete rescue commands from the authoritative recovery event.
- [x] Isolate autonomous rescuers and deterministic pointer layout in the PlayMode verifier.
- [x] Pass tactical controls, strict save preflights, physical rescue parenting, bed treatment, recovery hysteresis, command cleanup, and Console Error 0 / Warning 0.
- [x] Capture the verified gameplay region through Unity MCP only; do not use operating-system mouse/keyboard automation.

### Phase 112 — Remaining full V18 program

- [x] Convert captivity state and door-access subjects to strict detached Aggregate restoration with public invalid-preflight preservation proof.
- [x] Restore canonical character registry ownership and guard rescue completion with authoritative body-health state; pass the full Unity MCP tactical/medical regression with Console Error 0 / Warning 0.
- [x] Convert circus orders and captured wildlife to strict combined `500.world.circus` restoration, stage door membership, remove restore-based fixture seeding, and prove public invalid-preflight preservation.
- [x] Convert invasion threat/campaign/policies, active intruders, owner evacuation, and defense engagements to strict V4 restoration through one `550.world.invasion` participant; prove active prefabless candidate cleanup and rollback preservation.
- [x] Convert surgery orders/parts/storage/policies/corpse/anatomy state to strict V5 restoration through `525.world.surgery`; add an opt-in rollback-free section contract and prove failed detached candidates leave JSON and the published root revision unchanged.
- [x] Remove modular-facility live restore and warning/default backdoors; require transaction-only inactive candidates, exact codec/module versions, rollback-free section commits, and replacement-Grid round-trip proof.
- [x] Convert character-world restore to authored-catalog-only detached candidates; require one owner, strict nested state and exact cells/lifecycle, remove preserve-live/nearest/direct-publish/quiescence paths, and prove rollback-free late-failure cleanup plus full V18 round trip.
- [x] Convert wildlife ecosystem, population, raid, and carcass freshness to strict detached candidates; remove default-generation and warning/clamp restore paths, require rollback-free publication, and prove exact patch round trip plus synchronous failed-candidate cleanup.
- [x] Convert exterior-zone restore to strict canonical detached candidates; preserve payload order, require rollback-free synchronous publication/cleanup, and prove exact round trip plus late-failure live-world preservation.
- [x] Convert construction work orders/sites to canonical detached Aggregate and inactive Unity candidates; remove transient worker persistence, require synchronous replacement cleanup and rollback-free publication, and prove normal/invalid/late-failure contracts.
- [x] Convert physical items to strict current-version detached Aggregate restoration; omit transient hauling reservations, preserve durable direct-pickup source state, reject lossy legacy/default payloads, and prove the full 54-section live V18 round trip with zero item diff.
- [x] Convert defense facilities, factions, and grand projects to required typed rollback-free save boundaries with canonical payload validation and Unity regression proof.
- [x] Convert stock policies, regional supply contracts, regular customers, and facility shop state to strict required rollback-free snapshots; prove canonical/invalid and all-marker late-failure behavior.
- [x] Convert staff-discontent state to exact V1 required typed rollback-free restoration; remove trim/clamp/skip fallbacks and prove canonical round-trip plus invalid hierarchy preservation.

#### Phase 112 throughput contract — vertical batches

- Current source contract is fixed at 54 production save sections: 54 strict rollback-free, 0 remaining. Loaded Unity acceptance of all 54 remains pending under Batch D/F and the final root gates.
- Each domain batch is one atomic vertical deliverable. Save state, runtime authority, SO/catalog authority, dependency boundary, class responsibility, UI/error boundary, and tests are changed in the same working set rather than completed as separate internal phases.
- Atomic applies to the execution method, not only the completion checkbox: do not migrate owner 1 to completion and then repeat the same stack for owner 2. Establish each shared seam once, cut the complete owner set through it in the same revision, and remove the superseded paths across the set only after the integrated fixture passes.
- Audit all owners in a batch once, introduce shared contracts/helpers once, cut every owner over, remove all legacy paths, then compile/reload and verify once at the batch boundary. Individual owners or architectural layers are never reported as completed milestones.
- `State/Save`, `SO/Content`, `Runtime/Statics`, `Assembly/Responsibility`, and `Presentation/Verification` are simultaneous exit dimensions, not sub-batches or an execution order. A batch has only `in progress` or `completed` state.
- There are no "completed sibling" owners inside a batch. A failure is diagnosed at the narrowest owner, but the entire batch remains one unaccepted working set and is verified again through the shared boundary after the fix.
- A save section counts as converted only when it is required, exact-version typed, strict-preflighted, rollback-free, free of trim/clamp/skip/default restore fallbacks, and covered by canonical round-trip plus invalid no-mutation proof.
- Update `task_plan.md`, `findings.md`, and `progress.md` at a vertical-batch boundary or on a concrete failure. Report exact counters and named gates, never subjective whole-project percentages.

#### Completed foundation — maintain, do not redo

- [x] V18 new-game-only boundary and manifest compatibility; V17-and-earlier restoration remains rejected.
- [x] Root SO/content catalog authority, authored item definitions, typed persistent IDs, physical item/warehouse/equipment single ownership, scoped session state, and consolidated offense runtime/save Aggregate.
- [x] Runtime content `ScriptableObject.CreateInstance`, policy-free runtime providers, optional required-interface DI, and production `Bind*Runtime(...)` call sites are zero and remain ratcheted.
- [x] `CharacterSummaryInfo`, `FailureCode`/`DomainFailure`, String Tables, and initial combat-equipment failure-code adoption exist; each vertical batch completes adoption for its own domain.

#### Vertical execution batches

- [x] Batch A — core/session and transaction authority.
  - This is one `CoreSession` migration, not six owner migrations collected under one heading. `ExperiencePacing`, `ExternalInfluence`, `RunFlow`, `RunVariable`, `DungeonDebug`, and `ServiceRooms` are components of the same cutover unit and have no independent implementation order, completion state, compile gate, or acceptance result.
  - Establish one shared seam first: the authored `CoreSession` content projection, scoped root/transaction boundary, command/query/result contracts, exact V18 capture/restore manifest, presentation mapping, asmdef references, and composition registration table must describe all six components before any legacy implementation is removed.
  - Perform one synchronized replacement pass across all six components: move engine-independent Aggregate state and contracts to `DungeonStory.CoreSession`, route every persisted mutation/query through the shared seam, register every save participant in the same detached transaction, and delete all six sets of duplicate state/static/direct-save paths. Concrete Unity lifecycle and cross-domain adapters remain at the default edge until their event/item/invasion/building/character ports exist; moving them wholesale is forbidden because it would create a reverse dependency on `Assembly-CSharp`.
  - Keep one executable cutover matrix keyed by the six components and the same seven columns: `Content`, `Runtime state`, `Command/query`, `Save participant`, `Composition`, `Presentation`, and `Legacy removal`. Work may be edited file by file, but testing and acceptance start only when every cell is implemented.
  - Existing save/content/static changes are unaccepted working-set material until assembly ownership, command/query presentation boundaries, localized failures, and cross-owner transaction behavior are implemented in the same revision.
  - One integrated fixture must exercise all six owners through authored lookup, commands/queries, mutation, capture, invalid preflight, detached restore, final-section failure, publication, and presentation mapping. Separate narrow PASS results cannot complete the batch.
  - One boundary verification: Roslyn/reflection/asset graph, auxiliary compile, Unity reload, the integrated six-component fixture, V18 authority, and Console Error 0 / Warning 0 must pass together. Concrete-adapter UI pointer flows remain part of Batch E/F verification after their cross-domain ports exist.
  - Accepted evidence: all six Aggregate states and contracts are owned by `DungeonStory.CoreSession`; duplicate state declarations are absent; content authority, RunVariable state behavior, the six-component detached restore/failure fixture, V18 authority, and architecture ratchets pass in one loaded Unity revision. Metrics are `1093 files / 3287 types / 24 mutable statics / 13 oversized / 96 large constructors / 1054 default sources`, and Console is Error 0 / Warning 0. This completes the batch as one unit; no owner received an independent completion state.

- [ ] Batch B — characters, survival, work, and medical authority.
  - Atomic owner set: `AnimalHusbandry`, `CharacterBodyHealth`, `CharacterConsumables`, `CharacterEnvironment`, `SpeciesRuntime`, `SurvivalResources`, and `DarkSurvival` plus their character/work/medical UI and content.
  - One shared cutover moves authored species/needs/roles/diets/materials, entity state and IDs, seven exact save boundaries, Characters/Work/Survival/Medical asmdefs, confirmed class splits, command/query presenters, and localized failures together. Active inventory and event/skill deduplication become scoped in the same change; names, coordinates, and `GetInstanceID()` remain forbidden persistence keys.
  - One boundary proves all seven owners, `22 → 15`, character-summary/medical pointer flows, static reset, save round trip, V18, and Console 0/0 together.
  - Current accepted evidence: the integrated seven-owner survival fixture, Dark Survival PlayMode report, V18 authority, architecture ratchets, and Console Error 0 / Warning 0 pass together. Deprivation consecutive-run isolation, typed Husbandry failure/status presentation, narrow CharacterSummary consumables contracts, and anatomy plus surgery DTO/SO ownership in `DungeonStory.Medical` are covered.
  - The Unity EventSystem pointer matrix passes at `1600x900` and `900x1600`, including CharacterSummary close/reopen, health tab, automatic emergency surgery toggle/restore, surgery modal/footer flow, bounds, labels, four captures, and captured Error/Warning 0.
  - The stale medical residuals are closed: `ICharacterBodyHealthRuntime`, `ISurgeryRuntime`, and `ICharacterMedicalRuntime` are absent; body health uses Query/Command/Persistence facets; surgery and medical save typed statuses; the surgery UI has no by-reference string result; and all 280 required shared/ko/en String Table keys exist. The remaining Batch B exit item is concrete Characters/Work/Survival/Medical Unity-adapter ownership, which is part of the Batch E default-assembly cutover rather than another medical state migration.

- [ ] Batch C — production, facilities, automation, and world-resource authority.
  - Atomic owner set: `AutomationInfrastructure`, `ConveyorInfrastructure`, `FluidInfrastructure`, `PowerInfrastructure`, `ProductionBills`, `WasteProcessing`, and `EnvironmentalField` plus their buildings, resources, routing UI, and root content graph.
  - One shared cutover moves recipes/archetypes/capabilities/supplies/buffers/routes/branches, scoped facility/world state, seven exact save boundaries, Buildings/Production/Automation/World/Wildlife asmdefs, confirmed runtime splits, passive production presenters, and localized failures together while preserving physical items and transactional transfer as the only stock authority.
  - One boundary proves all seven owners, `15 → 8`, dependency depth/branching, buffer/backpressure/fair routing, responsive pointer flows, save round trip, V18, and Console 0/0 together.
  - Accepted UI evidence: Unity MCP/EventSystem pointer flows pass at `1600x900` and `900x1600`; both captures keep the complete building/context surfaces and all three route rows in bounds, including the third row; stock-sensor physical install preserves the existing RepeatForever bill before explicit MaintainStock conversion; route policy edits and `WaitingForOutputSpace` pass; the report proves UTF-8 Korean round-trip and captured Console 0/0. `ProductionBuildingPanelPresenter` is 770 lines and `UIBuildingInfo` is 783 lines.
  - Live routing source is now connected: recipe/construction/equipment/surgery demand providers populate `currentDemand`, `reservedQuantity`, and `blockedReason`; the output-buffer path calls `ProductionDistributionPlanner.SelectNext`, physically transfers the selected stack, and implements demand/minimum/target/warehouse/overflow/local fallback with strict, weighted, ratio, blocked-skip, reservation-cap, and starvation-aging behavior. Batch C still requires the integrated Unity-loaded scenario and Console 0/0 against the merged source revision before acceptance.

- [ ] Batch D — combat, equipment, economy, research, progression, and offense authority.
  - Atomic owner set: `CropPlot`, `WorldResource`, `TreasuryEconomy`, `CombatEquipment`, `EquipmentEvolution`, `OffenseAggregate`, `BlueprintResearch`, and `MetaProgression` plus their catalogs and screens.
  - One shared cutover moves equipment/module/lineage/research/reward/economy/world/offense/progression content, money/ID/cover/strategy state, eight exact save boundaries, Combat/Economy/Research/Offense/Captivity/Defense/Invasion asmdefs, confirmed class splits, command/query screens, and localized failures together; duplicate authority for any concept must be zero.
  - One boundary proves all eight owners, `8 → 0`, the complete 54-section round trip, equipment loss/lineage, 168 research, economy/defense/offense pointer flows, V18, and Console 0/0 together.
  - Current source state: all eight owners—Research V5, CombatEquipment V6, EquipmentEvolution V3, MetaProgression V1, CropPlot V2, WorldResource V2, TreasuryEconomy V3, and OffenseAggregate V2—use exact-current strict detached candidates and rollback-free boundaries. The source ratchet now requires `54/54` with an empty remaining set.
  - The all-marker registry path skips rollback-image capture, and its failed-final-commit fixture proves zero additional section captures plus zero published-root mutation. Batch D remains unaccepted until Unity loads all 54 types, canonical/invalid/late-failure scenarios pass in one revision, and Console is Error 0 / Warning 0.

- [ ] Batch E — cross-domain edges and composition closure.
  - Move Presentation to query/command contracts, Unity adapters and save/content loaders to Infrastructure, and all construction to Composition roots. Split the six Batch A concrete adapters only after their event/item/invasion/building/character ports have entered named contract assemblies; never make a named assembly depend on `Assembly-CSharp` to force an early move.
  - **Superseded by Phase 117:** do not reduce every default-assembly
    MonoScript to zero. Reduce unapproved domain authorities and cross-domain
    cyclic-boundary violations to zero while retaining reviewed Unity-edge
    adapters; asmdef cycles and reverse Presentation/Infrastructure references
    must still be zero.
  - Enforce executable gates: optional required-interface DI `0`, production `Bind*Runtime` `0`, authored mutable runtime statics `0`, direct `GameData` mutation `0`, invalid persistence keys `0`, runtime classes `≤1200`, MonoBehaviour/Presenter classes `≤800`, constructor dependencies `≤8`, architecture waivers `0`.
  - Finish String Table coverage for every non-`None` failure code, remove duplicate UI literals, normalize broken encoding, and rerun the complete root catalog/asset graph with runtime SO synthesis, gameplay `Resources.LoadAll`, code-owned mutable catalogs, destructive Editor builders, duplicate IDs, and broken references all at `0`.
  - The Roslyn constructor gate now measures operational DI owners rather than value/DTO field constructors. Its current actionable count is 32; the current default-assembly source count is 1,058 while the first CoreSession runtime leaf migration is active. Neither count is accepted as a new baseline yet.
  - Current merged source closes the operational constructor gate at `0` without waivers. Mutable runtime statics, content escapes, and direct session mutations also remain `0`. The remaining exit work is the Phase 117 ownership classification, typed-ID retirement of numeric compatibility consumers, and localization/encoding cleanup.
  - Current Roslyn evidence also closes the real oversized-type set at `0`. Grid, Economy content, Character mood/needs, Buildings leaf contracts, AI/Characters/Work leaves, and the first Combat contract are now in named assemblies. The semantic planner is deterministic with no missing metadata references. Historical default-file counts remain trend data only; Phase 117 owns the remaining risk-classified cutover.

- [ ] Batch F — integrated gameplay, save, and UI proof.
  - Execute V18 manifest/header/section/reference validation, full detached staging, injected late-failure no-mutation, repeated new game/save/load/scene transitions, stable typed IDs, physical stock/equipment/lineage/expedition loss, money ledger, and static-state leak checks as one clean-run matrix.
  - Run all research, production branching/buffers/supplies, facilities, combat/equipment/modules/lineage, medical, defense, wildlife, offense, and economy regressions.
  - Verify `1600×900` and `900×1600` through Unity MCP captures only; never use operating-system mouse/keyboard automation.
  - Regenerate `OVER_SEPARATION_AUDIT.md`, `task_plan.md`, `findings.md`, and `progress.md` from the same validator output and finish Unity Console Error `0` / Warning `0`.

#### Phase 117 risk-based Blueprint research cut

- [x] Preserve the `BlueprintResearchRuntime` public/serialized compatibility surface while moving Foundation root/event/debug composition to an application adapter.
- [x] Keep named Research ownership of queue, progress, dependency, and work rules; add the final node lock/unlock decision matrix there.
- [x] Leave Research V4/V5 DTOs, `requiredWorkAtCapture`, 168-node completion semantics, and restore order unchanged.
- [x] Pass Unity compile/execution, node-state and work-ratio probes, fresh analyzer target candidate `0`, asmdef cycle `0`, unique GUID, scoped diff, and Console Error `0` / Warning `0`.

#### Phase 117 risk-based Exterior incident authority cut

- [x] Replace runtime-list plus marker dual countdowns with one named Exterior incident Aggregate.
- [x] Make marker incident state projection-only; remove marker ticking, self-resolution, and save-data production.
- [x] Route handler time/stage changes, query, overview, capture, history trimming, and restore publication through the Aggregate without changing the V18 DTO/section/version/order.
- [x] Add deterministic Aggregate and handler query/capture/restore agreement regressions; pass named compile, standalone probe, target candidate `0`, oversized `0`, asmdef cycle `0`, GUID/meta, and scoped diff gates.
- [ ] Rerun the loaded PlayMode regression and Console 0/0 in the root integration gate after Unity MCP approval is restored.

#### Phase 117 risk-based operating-day settlement cut

- [x] Move revenue, visits, stock, incidents, debt, shortfall, report history, and settlement idempotence into one named Operation Aggregate/domain service.
- [x] Preserve the original MonoScript GUID/type through a fieldless facade and leave Unity snapshots, economy ports, alerts, and event publication in an application adapter.
- [x] Preserve OperatingDay save DTOs, section ID/version, canonical order, validation, detached prepare, and single-pointer publication without editing save sources.
- [x] Add duplicate day-start/end and money-ledger one-time regressions; pass the standalone domain harness, focused named/default compilation, target candidate `0`, asmdef cycle `0`, GUID/meta, and scoped diff gates.
- [ ] Run the loaded operating-day debug scenarios and final Console 0/0 in the root integration gate after concurrent source lanes settle.

#### Phase 117 risk-based Experience pacing authority cut

- [x] Move current day, rehearsal masks/active day, and introduced concepts into one named CoreSession Aggregate with monotonic/idempotent transitions.
- [x] Isolate Foundation event subscription and authored Content rule lookup in a recognized application adapter; leave the runtime with no direct state writes or cross-domain references.
- [x] Preserve the frozen V18 pacing section ID, DTO version, restore phase/dependency, detached preparation, and single publication while isolating the strict save adapter.
- [x] Add monotonic day, duplicate transition, concept uniqueness, mask/day/active invariants, exact capture/prepare/publish round-trip, and invalid candidate regressions.
- [x] Pass named/focused/Editor compilation, standalone transition/save probe, target candidate `0`, oversized `0`, asmdef cycle `0`, GUID/meta, and scoped whitespace/diff gates.
- [ ] Run the loaded pacing scenario and final Console 0/0 in the root integration gate after Unity MCP approval is restored.

#### Phase 117 final-acceptance runner coverage audit

- [x] Map every synchronous final-runner step to V18/54, content, item/equipment, production/supply, research, CoreSession, combat/medical/survival, and implemented-gameplay completion contracts.
- [x] Add the missing callable synchronous entries for runtime composition, OperatingDay authority, strategic physical expedition, expedition journey/architecture, and Offense aggregate V18.
- [x] Keep the report at `Artifacts/QA/final-acceptance-report.txt` and label PlayMode UI/resolution captures plus Console 0/0 as a deferred external Unity MCP gate.
- [x] Pass focused Assembly-CSharp-Editor compilation, unique runner GUID/meta, scoped diff, and trailing-whitespace checks.
- [x] Add callable regression evidence for equipment lineage transfer, expedition equipment/module co-loss, and firearm smoke/misfire plus bow/crossbow/gun role balance; retain the live full-world 54-section round trip with run/scene isolation for the loaded Unity gate.
- [x] Keep canonical shop-category drift under `Batch A content authority` and stable `(Kind, Id)` circus identity under `Implemented gameplay scenarios`; the synchronous runner remains 33 top-level steps.
- [ ] Run the loaded 33-step runner after the merged Unity refresh and require both nested shop-category and circus-identity contracts to pass before accepting the synchronous gate.
- [ ] Run the separate Unity MCP `1600x900` and `900x1600` pointer/capture matrix and final Console Error 0 / Warning 0 after project approval is restored.

#### Phase 117 risk-based Dungeon run-flow authority cut

- [x] Move day, phase, outcome, recurring-boss scheduling, and terminal transition decisions into the pure named `DungeonRunFlowReducer` with ordered effects.
- [x] Preserve the original `DungeonRunFlowRuntime` GUID/type as a fieldless compatibility facade and isolate pacing, invasion, owner, alert, and restore projection in `DungeonRunFlowApplicationAdapter`.
- [x] Preserve the frozen root V18/run-flow V2 strict save contract, section ID, restore phase/dependencies, detached candidate preparation, and single publication.
- [x] Add duplicate day-10 rehearsal, day-40/day-50 boss schedule, boss start/resolution, truth completion, deterministic sequence, and existing save round-trip regressions.
- [x] Pass named/default/Editor focused compilation, standalone reducer harness, fresh analyzer target candidate `0` and hard gates `0`, oversized `0`, asmdef cycle `0`, unique GUID/meta, save-contract, encoding, and scoped diff gates.
- [ ] Run the loaded RunFlow PlayMode regression and final Console Error 0 / Warning 0 in the root integration gate after concurrent source lanes settle.

#### Phase 118 equipment/expedition UI evidence matrix

- [x] Add a dedicated final coordinator target for equipment and expedition UI instead of treating unrelated responsive screenshots as evidence.
- [x] Exercise equipment appraisal, restoration, rune tuning, installation, removal, and lineage source/target/seal/confirm through Unity EventSystem pointer events.
- [x] Exercise a live expedition journey action through Unity EventSystem and require the phase/node state to change.
- [x] Require equipment and expedition captures at both `1600x900` and `900x1600`; the current final contract is seven targets and 30 captures.
- [x] Restore seeded physical-item and combat-equipment runtime snapshots and retain the final coordinator persistence snapshot boundary.
- [x] Capture and canonically restore the research and offense Aggregate save sections for every resolution row and final cleanup, including standalone runs.
- [x] Clear verifier-owned expedition and battle state after each offense baseline restore so the two resolution rows cannot accumulate journey progress.
- [x] Preflight every scene path required by the whole suite before state/persistence capture, then recheck actual `OpenSceneMode.Single` transitions; preserve the clean seven-target flow and never save/discard user scene changes automatically.
- [x] Replace the arbitrary equipment forge surface with authored RF42/RF43/RF44/I17/I18 facility panels, require facility-local physical delivery, and reject progression commands on S08 or the wrong dedicated facility.
- [x] Prove standalone module-stack absorption on install, same-instance facility-buffer rematerialization on removal, and I18-local source/target/seal lineage confirmation and work application.
- [x] Make detached modules authored unique physical items with strict save/restore linkage; reject detached/attached duplication, mark destructive removal as Lost, and keep installation absorption non-destructive.
- [x] Remove all facility-less progression command callers and pass fresh Foundation, Items, Combat, Runtime, Editor, ArchitectureMetrics, asset-meta, GUID, and scoped diff gates.
- [x] Require the authored facility-flow marker in the final coordinator without changing the seven-target/30-capture contract.
- [ ] Run the loaded seven-target matrix in Unity and require 30 fresh captures plus Console Error 0 / Warning 0.

#### Phase 117 final offline integration audit

- [x] Rerun fresh ArchitectureMetrics after all visible source-lane changes and confirm every hard gate plus global cross-domain candidate count is `0`.
- [x] Confirm the 49-asmdef graph has zero cycles, all C# sources have metas, and all 6,817 asset GUID records are unique.
- [x] Prove the 33-step final runner reaches the physical lineage transfer, expedition-death equipment/module co-loss, and gunpowder smoke/misfire/ranged-role regressions through its existing scenario calls.
- [x] Pass the focused Assembly-CSharp-Editor runner compilation and scoped source/document whitespace checks.
- [ ] Regenerate stale Unity/Bee assembly artifacts, run loaded synchronous/PlayMode acceptance, and finish Console Error 0 / Warning 0 in the root Unity MCP gate.
- [x] Preserve the pre-existing Unity-serialized trailing whitespace reported by global `git diff --check` (`1,502` lines across `32` files). Bulk normalization would rewrite unrelated user-owned scene/prefab data; the final-audit source/doc scope itself is clean.

#### Phase 117 acceptance evidence-gap closure

- [x] Execute physical lineage transfer through its real queue/work/physical-item authority and verify source/seal consumption plus target history and property/module preservation.
- [x] Execute expedition death through `OffenseExpeditionReturnPort` and verify unique equipment and installed module co-loss plus loadout removal.
- [x] Separate gunpowder smoke from suppression, apply hit/miss/misfire smoke exactly once at the shared resolver boundary, and prove authored bow/crossbow/gun combat roles through actual resolution/preview/timing APIs.
- [x] Add a fresh-request PlayMode facade for the actual 54-section full-world round trip, pre-composition warning/error capture, canonical baseline restoration proof, and EditMode return.
- [x] Replace the invalid legacy owner-doctrine fallback fixture with strict current-version rejection and canonical live-state non-mutation proof.
- [x] Pass named Combat, focused smoke consumer, isolated full-world facade, GUID/meta, and scoped diff gates.
- [ ] After Unity refreshes stale Bee artifacts, run the loaded synchronous runner and `DungeonFullWorldRoundTripPlayModeFacade`, require fresh `RESULT=PASS`, and finish Console Error 0 / Warning 0.

#### Phase 122 V18 identity, sequence, and final-evidence closure

- [x] Normalize every recognized early-V18 operational CharacterId before aggregate cross-reference validation while preserving non-character runtime keys.
- [x] Enforce exact raw typed IDs and canonical numeric ID grammar across combat, defense, medical, maintenance, surgery, production, and consumables restore paths.
- [x] Separate consumables external idempotency IDs from `auto:v1` generated IDs, preserve legacy V18 D16 watermarks, and reject generated-ID injection at public command ingress.
- [x] Prevent combat, defense, medical, maintenance, surgery, surgical-part, production-bill, and consumables sequence overflow before mutating runtime state.
- [x] Remove final UI false passes by requiring real EventSystem top-hit/viewport pointer flow and model-bound research detail fields at both resolutions.
- [x] Pass fresh Unity compilation, ArchitectureMetrics hard gates, architecture `131/131`, transactional restore `33/33`, and synchronous final acceptance `33/33`.
- [ ] After the user explicitly saves or reverts the dirty Gameplay scene, run the root-only Unity MCP final matrix and require seven targets, 32 valid captures, `63/63/63` full-world sections, canonical baseline restoration, and Console Error/Warning `0/0`.

#### Phase 123 V19 feature and cohesion closure

- [x] Pass the current synchronous Unity acceptance runner at `33/33`, including V19 save atomicity, 216 research, child safety, 200,000-subject life simulation, production, combat, medical, survival, and Offense regressions.
- [x] Replace the historical hard 800/1,200 line gates with review thresholds; enforce hard failures only above 2,000 lines or 16 constructor dependencies.
- [x] Review every current size/dependency finding by responsibility instead of line count: keep `CharacterActor`, `CharacterBodyHealthRuntime`, `DungeonAggregateReferencePreflight`, and `PhysicalAgeTreatmentRuntime` with recorded reasons.
- [x] Audit V19 ScriptableObject, catalog, save-section, presenter-formatting, and application-adapter boundaries; find no new pure pass-through provider, sibling-only runtime source, or dependency-bag abstraction requiring a merge.
- [x] Remove four stale `RegularCustomerRuntime` Editor-fixture roots from the dirty Gameplay scene with Unity Undo, add `try/finally` cleanup to the runtime-event scenario, and prove two consecutive runs leave exactly one production runtime and zero test debris.
- [x] Pass the standalone V19 character-summary/medical UI matrix at `1600x900` and `900x1600`: six Unity captures, real EventSystem pointer flow, and captured Error/Warning `0/0`.
- [x] Pass fresh ArchitectureMetrics at `1,431 files / 4,532 types / review types 3 / hard oversized 0 / review constructors 1 / hard large constructors 0 / mutable statics 0`.
- [x] Clear stale Unity Console history through the project-local MCP bridge and confirm a fresh Error/Warning query returns zero entries; current DLL timestamps and the Editor-log compiler scan remain the separate compilation proof.
- [ ] Run the final seven-target PlayMode matrix after the dirty Gameplay scene is explicitly saved or reverted; the safety preflight currently blocks scene switching without mutating or saving the user's scene.

#### Phase 124 V20 content-density, narrative, faction, combat, and endless expansion

- [x] Freeze the V20 authority contract: exactly 216 research nodes, exactly 450 new hand-authored content definitions, immutable SO definitions, plain runtime Aggregates, typed IDs, strict root-catalog registration, and V19-or-earlier rejection.
- [x] Establish the V20 content schemas and validators for character narrative, cultures, shared society events, faction campaigns, enemy archetypes/abilities, encounters/modifiers, seasonal events, milestones, relics, and landmarks without runtime SO synthesis or fallback definitions.
- [x] Implement the character narrative vertical slice through authored background/culture/ambition/event definitions, deterministic daily event scheduling, bounded histories, command/query APIs, and save/restore.
- [x] Implement faction campaigns, contracts, guest requests, service incidents, physical relic rewards, and cross-faction chapter progression using existing item/facility authorities.
- [x] Replace code-authored enemy templates with SO-authored archetypes and abilities, separate tactical decisions from combat execution, and implement 36 objective-driven encounters plus 12 battlefield modifiers.
- [x] Treat enemy archetypes only as tactical/loadout templates. Spawn every enemy as a persistent character with deterministic per-instance age, background, culture, traits, hereditary traits, skills, ambition, injuries, and loyalty; offense and defense must share this path, and capture/recruitment must preserve the same CharacterId and instance state rather than converting the archetype into a generic recruit.
- [x] Expand wildlife, disease, festivals, seasonal incidents, and crop cultivars through the existing ecology, population-health, climate, and crop authorities.
- [x] Implement nine automatic non-terminal milestones, nine physical landmarks, LegacyAge, EndlessAge, deterministic endless crisis composition, and one-time reward/counter-pressure rules.
- [x] Add five V20 save sections for a canonical total of 68, update offense/faction section versions, enforce full detached staging and late-failure atomicity, and reject every V19-or-earlier slot with the approved message.
- [x] Author and root-register the exact 450-definition manifest by category, validate stable IDs/references/gameplay consequences, and keep research count and reachability unchanged.
- [ ] Pass focused domain tests, 10,000-character/2,000-population/10-year deterministic simulations, combat/timing balance probes, V20 68/68/68 round trip, Unity MCP resolution evidence, and final Console Error 0 / Warning 0.

#### Phase 125 design-document consolidation

- [x] Audit the existing comprehensive document structure and compare its stated scope with the implemented V19/V20 authorities.
- [x] Reframe the document around the intended player fantasy, emotional arc, nested gameplay loops, and long-run progression rather than a flat subsystem inventory.
- [x] Integrate the current 216-research, 450-definition V20 content map and the V19 life, generation, climate, disease, crop, child-safety, and career rules without retaining contradictory legacy counts.
- [x] Explain how production, research, characters, factions, offense/defense, captivity, ecology, milestones, and EndlessAge consume and reinforce one another through concrete player-facing examples.
- [x] Separate design intent, implemented authority, verified evidence, and deferred final integration evidence so the document never presents an unrun test as complete.
- [x] Run document consistency checks for headings, version/count claims, terminology, encoding, links, and whitespace; update findings/progress from the same result.

#### Phase 126 exhaustive authored-content intent catalog

- [x] Inventory every registered facility definition from the authored SO/catalog sources and distinguish canonical gameplay facilities from legacy duplicates, fixtures, and editor-only artifacts.
- [x] Inventory every authored event-like definition: life events, festivals, seasonal events, faction chapters/contracts, guest requests, service incidents, encounters, milestones, and other player-facing event contracts.
- [x] Inventory every general and hereditary trait definition, including the preserved legacy traits, and extract its actual mechanical consequence fields.
- [x] Write one explicit intent entry per canonical facility, event, and trait: player problem, decision/tradeoff, concrete inputs/effects, and system connections. Counts may remain only as navigation summaries.
- [x] Integrate the exhaustive catalogs into `docs/DungeonStory_Game_Design_and_Implementation.md` without replacing authored details with generated filler or grouping away individual entries.
- [x] Cross-check documented stable IDs/names against source authority, detect omissions/duplicates, and run Markdown/encoding/whitespace consistency checks.
- [x] Record exact catalog coverage and any definitions whose current SO lacks enough mechanics to justify a distinct gameplay intent.

#### Phase 127 V21 research consolidation and physical unlock expansion
- [x] Freeze save policy: V20 and earlier are rejected; no save-file migration or legacy DTO conversion is implemented

- [x] Freeze the V21 authority contract: 180 research projects, 138,824 total work, immutable authored SO definitions, research Aggregate completion authority, physical item/equipment instance authority, and V20-or-earlier rejection.
- [x] Replace the 216-node generated tree with the approved 180-node merge map; union rewards/work/prerequisites, remove internal/transitive links, preserve causal metadata, and prove the five pacing closures.
- [x] Extend the research reward reverse index and presentation contracts for craft materials, crops, environmental workwear, ammunition, installation components, and authored unlock bundles without duplicating lock authority.
- [x] Author and root-register the twelve branched materials, six facilities, physical utility/medical/installation items, eighteen equipment definitions, and ten ammunition/consumable definitions with concrete recipes and consumers.
- [x] Route the new production outputs, construction inputs, equipment crafting, ammunition use, and medical/service consumption through the existing physical item and runtime command authorities; add no runtime SO synthesis or fallback definitions.
- [x] Raise the root save generation and research payload boundary to V21, reject V20 and earlier with the approved message, and preserve same-generation progress ratios through requiredWorkAtCapture.
- [x] Update research UI/package projection and the design authority document so facilities, materials, items, equipment, ammunition, and downstream uses are visible without count-padding.
- [x] Pass content-graph, research-graph, focused runtime, equipment-role, save-boundary, Unity compilation, Console 0/0, and project-scoped Unity MCP evidence gates.

Phase 127 implementation contract:
- SO assets and explicit root catalogs own immutable definitions only; no completion, stock, item-instance, or save state is written to SOs.
- `requiredResearchId` remains the sole gameplay lock declaration on each reward definition. Unlock bundles are authored presentation metadata and must not become a second lock authority.
- Production Aggregates own orders/reservations/buffers, the item repository owns physical stacks and unique equipment, combat owns actions only, and the research Aggregate owns progress/completion/queue state.
- Every new intermediate has at least two concrete consumers, strategic intermediates have at least three, post-resource depth is at most four, and explicit crafting inputs contain no `stock-item:*` entries.
- V21 is a hard new-game boundary. No deleted research ID is remapped during restore and no missing definition is synthesized.

#### Phase 128 V21 actual-gameplay connection closure

> Complete the user-approved vertical-path contract for authored V21 content. A
> catalog entry or debug snapshot is not completion: each accepted feature must
> have a real command entry, authoritative requirements, physical reservation or
> durability cost where applicable, domain effects, atomic failure, and V21 save
> ownership.

- [x] Replace partial society/content resolution with a preflight-and-commit command that evaluates typed requirements/effects and cannot leave an event resolved when any physical or domain effect fails. (`WorkDelayDays` now persists scoped end days in `society.events` and modifies the authoritative work-speed projection.)
- [x] Add stable saved alert action IDs and route society events, faction choices, festivals, funerals, counseling, reproduction, and age treatment through functional UI dispatchers.
  - Saved action/source IDs now drive society and faction resolution, planned reproduction start, due festivals, recent funerals, counseling, and all five age-treatment surgery orders through the existing alert UI.
- [x] Connect character traits, hereditary traits, backgrounds, ambitions, cultures, practices, reproduction, grief/funeral/festival/counseling, and age treatment to their owning Aggregates and real work/item/facility requirements.
  - [x] Replace all nine legacy placeholder trait tags with authored behavior, mood, and event-weight consequences; route meal, research, invasion, festival, room-environment, and checkout-wait events through the typed trait-reaction runtime.
  - [x] Add authored hereditary costs for nutrition, reproduction nutrition, sleep, movement, mana overload, and rapid-cell aging, and consume them in survival, reproduction, environment exposure, and aging projections.
  - [x] Connect culture meal refusal/preference, typed room/environment AI scoring, etiquette/attitude incident weighting, one-time inter-culture relationship memories, practice-only assimilation, and persisted perform/neglect practice execution.
  - [x] Connect planned reproduction, funeral/festival/counseling, and five physical age-treatment order paths.
  - [x] Make fertility treatment an optional persisted reproduction choice that consumes its physical medicine only at atomic start and changes conception/gestation calculations; make trait analysis consume its kit at an operational analyzer and persist latent-trait visibility.
- [x] Remove `GuestSupplies` fake consumption and connect V21 tools, kits, installation parts, seed lots, greenhouse/fungal inputs, and medical supplies only to their intended work, construction, maintenance, ceremony, or procedure.
  - [x] Remove fabricated guest sinks; connect certified seed kits, greenhouse nutrients, inoculated logs, cross-lineage medium, isolation/trauma kits, age-treatment supplies, and installation components to their owning commands or cycles.
  - [x] Make reinforced restraints and prisoner work kits unique durable physical instances: capture and labor now wait for the selected instance/delivery, retain its ID and durability in captivity save state, apply restraint security and labor wear, and return the same instance when custody/labor ends.
  - [x] Connect `medical:fertility-treatment` and `medical:trait-analysis-kit` to their intended reproduction and hereditary-analysis commands instead of guest-service sinks.
- [x] Give all 101 research-reward facilities truthful roles/capabilities and require at least one real recipe or domain command executor; add the missing 8897-8901 facility intent entries to the design authority.
  - [x] Audit exact execution coverage: 63 facilities own a concrete production recipe and 38 own a typed domain command, with zero overlap, missing executor, or unclassified facility.
  - [x] Connect typed facilities to crop, husbandry, workforce, circus sanitation, equipment crafting/tuning, mentorship, diagnosis, fluid metering, expedition planning, secure trade, and defense control; all commands also execute through the normal saved building-work path.
- [x] Persist loaded ammunition identity and implement special-ammunition, role-equipment, enemy physical loadout, tactical ability, boss-phase, counter-tag, loot, and handcrafted encounter execution in offense and defense.
  - [x] Persist loaded ammunition identity; implement the ten special ammunition effects, role equipment, enemy loadout, tactical weights/formation/boss phases, counter evaluation, explicit 36-encounter authoring, and physical offense return loot.
- [x] Apply wildlife ecology metadata, disease symptoms/field responses, and all six crop-genome loci in the daily authoritative simulations.
- [x] Make faction campaign state the single relationship authority, diversify all chapter outcomes, and connect milestone conditions, landmark locks, permanent rewards, and pressures to actual systems.
  - [x] Replace the 36 repeated chapter signatures with atomic full-cost/half-cost/refusal paths and counterpart changes for all six rival chapters.
- [x] Generate and pass definition-to-entry-to-effect-to-save connection reports, atomic last-item failures, focused UI/playmode scenarios, deterministic scale probes, V21 section round trips, and Unity Console Error 0 / Warning 0.
  - [x] Generate the 1,194-row connection report with zero unlinked definitions, pass atomic last-item rollback, the five functional alert UI routes, deterministic scale probes, and the focused V21 vertical gate with zero script warnings.
  - [x] Pass the existing full 68/68/68 world PlayMode gate and its integrated Console Error 0 / Warning 0 condition. The final isolated run restored and recaptured all 68 sections, matched the canonical baseline, restored the live baseline, and reported Console `0/0`.

Phase 128 implementation order:
- Stabilize atomic content execution first, then remove fabricated consumers before adding intended ones.
- Complete one persisted vertical slice per domain and keep existing SO/state/save authorities; do not introduce a second mutable content registry.
- Preserve the exact 180 research / 138,824 work / 450 V20 net-new / 68 save-section contracts and the V21 new-game-only boundary.

Phase 128 validation errors:
- Direct execution of Unity's legacy Mono Roslyn `csc.exe` could not resolve `System.Text.Encoding.CodePages`; a second Mono-hosted attempt asserted inside Mono. Both targeted only `Temp/CodexCompile` and changed no source. Validation then switched to Unity 6's supported `Data/DotNetSdkRoslyn/csc.dll` under the installed dotnet host.
- The first temporary `Assembly-CSharp.rsp` rewrite replaced the Economy implementation DLL path, while Unity references the `.ref.dll`; compilation correctly failed on the missing new phenotype type. Rewriting the exact reference path fixed the harness, after which Economy, runtime, and Editor assemblies compiled cleanly.
- The first crop-domain discovery output was truncated because the combined source read exceeded the shell result budget. Narrow range reads were used before editing; no mutation was based on truncated content.
- A post-edit optional `rg` batch returned exit code 1 because no matching scenario/meta existed yet. The successful `git diff --check` output in the same batch remained valid; no mutation occurred.
- The project Unity MCP bridge still reports four older pending requests and did not return the new refresh response. The Editor process remains responsive, so it was not restarted or killed; live compilation and Console evidence remain pending.
- A GUID uniqueness scan over the full `Assets` tree timed out after confirming the new scenario meta did not yet exist. A project-local unique GUID was then added explicitly; the scan made no mutation.
- A PowerShell `rg` call passed `BuildableObject*.cs` as a literal Windows path and failed with an invalid-path error after returning useful matches. Subsequent searches use the containing directory plus `-g` filters.
- A facility search included the nonexistent legacy path `Assets/Scripts/Content/BuildingSO.cs`; the authoritative file is `Assets/Scripts/Services/Buildings/SO/BuildingSO.cs`. No mutation occurred.
- A later optional discovery command again passed `Assets/Scripts/Services/*/Editor` as a literal Windows path. It changed no files; all later searches use a real parent path and `-g` filters.
- An item-query inspection guessed the nonexistent `WorldItemQueries.cs`; the authoritative query implementation is `WorldItemQueryService.cs`. Optional `rg` no-match exits in the same audit changed no files.

#### Phase 129 exhaustive research-node intent catalog

- [x] Inventory all 180 V21 research projects from the authored project assets and verify the exact 138,824 total work contract.
- [x] Reconstruct each node's direct causal prerequisites and reverse-indexed physical unlocks from the current research/content authorities.
- [x] Add one explicit document entry per research node, grouped for navigation and including stable ID, work, direct prerequisites, concrete unlocks, and player-facing intent.
- [x] Cross-check the document against source authority for 180/180 coverage, zero duplicate/missing IDs, exact work sum, and resolvable prerequisite/reward references.
- [x] Run Markdown, encoding, whitespace, and scoped diff checks; update findings and progress with the final evidence.

Phase 129 validation errors:
- The planning skill's `session-catchup.py` found unsynced historical context but terminated while printing it because the Windows CP949 console could not encode an em dash. Current planning files were read directly and no project file was mutated by the failed recovery command.
- An optional sample-building `rg` pipeline exited 1 after yielding useful runtime-archetype and recipe samples because the final search branch had no match. It made no changes; the canonical building sample was then located directly by numeric ID.
- The first inline PowerShell inventory prototype returned no output when piped into a nested PowerShell process. It changed no files. The extractor will instead be a directly invokable, reviewable script under `Tools`.
- Direct invocation of the new PowerShell extractor was blocked by the host's default script execution policy. No data or source changed; subsequent validation invokes the same checked-in script with process-scoped `-ExecutionPolicy Bypass`.
- Windows PowerShell 5 then parsed the UTF-8-without-BOM script as the active ANSI code page and also rejected leading-line boolean operators, producing parser errors before execution. The operators were made PS5-compatible and the script will be loaded explicitly with `Get-Content -Encoding UTF8`.
- Explicit UTF-8 ScriptBlock loading does not populate `$PSScriptRoot`, so the extractor's first encoded invocation stopped before scanning. It now falls back to the current workspace when invoked this way while retaining normal file-invocation behavior.
- The first Markdown sample interpolated prerequisite IDs as PowerShell subexpressions (`$(research:...)`) instead of Markdown code spans. The formatter now uses an explicit format string; no document content had been inserted yet.
- Review of the first four inserted fields found two extractor presentation defects: unsupported mirror definitions escaped the PowerShell `switch` with an empty reward kind, and project-owned recipe unlocks without a matching `requiredResearchId` displayed raw IDs. The extractor now rejects unrecognized reward types after the switch and uses a global recipe-name map; the affected four tables will be regenerated before continuing.
- A review-only command tried to pipe directly from a PowerShell `foreach` statement and failed parsing before execution. It made no changes; subsequent filtered previews assign the loop output before piping.
- The first concise final-validation command had an unmatched PowerShell subexpression parenthesis and failed before reading or changing project data. The already-passing full validation remains valid; the concise check will be reissued using intermediate variables.

Phase 126 validation errors:
- The first optional multi-pattern discovery batch used `Promise.all`; one no-match `rg` exit code aborted presentation of the other successful reads. No mutation occurred. Further optional searches normalize exit code 1 or run sequentially.
- The first grouped facility-list helper used PowerShell backtick-tab syntax inside a JavaScript template literal and failed during JavaScript parsing. No tool or file mutation occurred; the replacement uses `[string]::Join([char]9, ...)`.
- The first seasonal-event extraction guessed `Assets/Resources/SO/V20/Society/SeasonalEvents`, which does not exist. Guest-request and incident extraction in the same sequential batch succeeded. Seasonal assets will be located by class identifier before retrying.

Phase 124 implementation order is contract and validator first, then one complete narrative save vertical slice, followed by content domains in dependency order. Existing dirty changes outside direct V20 scope are preserved and never reset or bulk-rewritten.

Phase 124 validation errors:
- PowerShell/ripgrep does not expand the Unix-style `Assets/.../*.asset` path in this environment; use `rg ... Assets/... -g "*.asset"` for asset-tree searches.
- The V20 wildlife ecology extension initially referenced `Season` without adding the existing `DungeonStory.CoreSession` assembly dependency. The fix is an explicit one-way Wildlife -> CoreSession asmdef reference; do not duplicate the calendar season enum.
- The original combat-authoring pass treated 36 enemy entries as reusable combat archetypes. They are not recruitable character species or fixed personalities. Runtime integration must layer those templates over the normal character profile factory, persist the individual before combat, and retain the individual through prisoner recruitment.

- `dotnet build DungeonStory.sln --no-restore` cannot run because this host has no installed .NET SDK. Use the loaded Unity compiler/Editor log or the project compiler harness instead; do not repeat the dotnet command.
- The project-local dungeon-player status bridge did not respond within 30 seconds after the contract edit. The request was terminated without input or scene mutation; use independent Unity log/assembly evidence and retry the bridge only after confirming the editor is responsive.
- The first Unity refresh command executed and rebuilt the changed assemblies, but the relay lost JSON-RPC response id 2 during the requested domain reload. Editor evidence exposed one real compile error: the new global `FactionContractKind` collided with the existing faction-domain enum. Rename the V20 content enum and rerun the Unity compile gate.
- After the enum rename, the second project-scoped refresh completed with `Tundra build success`, rebuilt `Assembly-CSharp`, and reported no compiler errors. The helper still reports a transport failure because Unity disconnects the relay during its intentional domain reload; use Editor-log and DLL evidence for refresh commands, then use non-reloading MCP tools for console proof.
- The first narrative compile found `IsExternalInit` errors because this Unity project's API compatibility profile does not support `init` accessors. Replace the snapshot's `init` accessors with assembly-internal setters; do not add an `IsExternalInit` compatibility shim.
- The immediate MCP retry after fixing `init` was rejected because the prior domain reload had not republished discovery. A broad recursive temp-file search then timed out. The Editor subsequently completed the import, rebuilt `Assembly-CSharp` at 20:16:13, reloaded the domain, and republished the project connection; avoid recursive temp searches and wait for the known project connection file instead.
- A first `Unity_GetConsoleLogs` call passed raw JSON through nested PowerShell quoting and failed before opening the relay. Reissued the request using the helper's Base64 argument path; the project-scoped Unity Console returned Error `0` / Warning `0`.
- A planning-file update patch briefly failed because its progress-file path omitted `DungeonStory`; no file was changed by that patch. The corrected absolute workspace path is used here.
- A broad enemy/captivity `rg | Select-Object -First` inspection returned exit code 1 after emitting 400 truncated matches because the downstream PowerShell consumer closed the pipe. No files changed; subsequent inspection uses declaration-specific searches and concrete files.
- A second broad invasion search repeated the same PowerShell closed-pipe failure after valid offense-return output. No mutation occurred; do not pipe broad `rg` output into `Select-Object -First` again.
- The guessed `Assets/Scripts/Models/Offense/DungeonStory.Offense.asmdef` path did not exist. The same non-terminating PowerShell batch still returned the combat builder and progression findings; locate the actual asmdef with `rg --files` before editing references.
- A declaration search for Character Life `Register` returned exit code 1 only because the final optional pattern had no match; the same batch successfully read `CharacterTraitSO` and located the authoritative interfaces. No mutation occurred.
- The V20 ecology builder compiled and executed successfully through project-scoped Unity MCP after the asmdef fix. It upgraded the 5/8/8 preserved definitions and authored the planned 13 wildlife, 8 diseases, and 12 cultivars with validation before catalog publication.
- The first enemy-individual compile refresh again lost JSON-RPC response id 2 during the intentional domain reload. This is the known transport behavior, not compile evidence; inspect the republished project connection and Console before changing code.
- The first combined return-arrival integration patch failed verification because it matched a mojibake status literal as context. `apply_patch` changed none of the files. Reapply in small structural chunks anchored on ASCII declarations and member names.
- A second insertion still included the same encoded literal and was rejected without mutation. The successful third form anchors only on the structural `}; arrivals.Add(state)` sequence and delegates generation to a new ASCII-named method.
- A combined restore/spawn patch was rejected because its spawn hunk still included the encoded GameObject-name line. No partial mutation occurred. Restore validation and spawn replacement were then applied as separate ASCII-only hunks.
- A direct display-name override patch first repeated the encoded-line mismatch and was rejected; the corrected hunk anchors on the following transform/initialize lines. The subsequent compile refresh lost the relay during domain reload as expected; post-reload Console is the authority.
- A documentation-only `apply_patch` attempted to include an empty `findings.md` update hunk and was rejected before changing any file. The corrected patch updates only files with concrete context.
- The deterministic-age refresh again produced the known relay EPIPE during Unity domain reload; the republished project-scoped Console subsequently confirmed Error 0 / Warning 0.
- A combined invasion inspection command returned exit code 1 because one declaration-specific `rg` pattern had no match, although its other read commands emitted valid source. No files changed; subsequent inspection searched the persistence type across the full script tree.
- A parallel declaration search for director construction/persistence calls also returned exit code 1 when one piped filter found no match, suppressing otherwise independent output. It was repeated with direct patterns and no filesystem mutation.
- The defense-individual refresh again lost the relay response during the expected domain reload. The republished project-scoped Console is authoritative and returned Error 0 / Warning 0.
- The first milestone builder execution stopped after creating nine ending assets and the first landmark because `material:engineering-blueprint` is referenced by some older content but has no registered physical item definition. The idempotent builder now uses the authored `component:prototype-package`; partial assets remain owned by the same builder and are completed/validated on retry.
- The immediate milestone-builder retry hit a stale Unity discovery window after script recompilation. The relay reconnected at the end of the call; the next retry completed successfully.
- The first dynamic exact-manifest probe failed to compile because its generic helper constrained `CharacterTraitSO` through a missing Odin serialization reference in the ephemeral command assembly. The replacement probe counts registered definitions by runtime type name and inspects landmark serialized properties without introducing an assembly dependency.
- The replacement manifest probe executed successfully and logged `V20_MANIFEST=PASS`; the PowerShell helper then missed response id 2 despite printing the successful tool result. Treat the Unity execution log as evidence and do not rerun solely for the wrapper's post-result parsing defect.
- A combined V20 save-registration patch was rejected before mutation because it anchored on a mojibake rendering of the Korean incompatibility string. The save-phase, DI, version, and message edits were reapplied in UTF-8-safe structural patches.
- One parallel save-inspection command had an unterminated PowerShell quote, and a later read guessed a nonexistent `DungeonStrictJsonSaveSection.cs` path. Neither changed files; the actual base class is `Core/Save/DungeonJsonSaveSection.cs`.
- The first V20 save compile exposed one real error: `FactionSaveSection` lives in the `DungeonStory.Infrastructure` namespace. Added the explicit namespace import; no save dependency ID was duplicated.
- The next V20 save compile found one stale editor-facade reference to the renamed pre-V20 incompatibility constant. Updated the facade to the V20 constant; the broader 63-to-68 and V19-to-V20 test ratchets remain a separate mechanical update.
- The first full V20 campaign simulation exposed four seasonal events whose faction effects still targeted pre-campaign semantic IDs. They now reference canonical dungeon factions, while guest/life/service events persist a deterministic contextual faction ID and the catalog rejects unknown faction-effect targets at boot.
- The ten-year campaign test then exposed an event-queue bug: emergency candidates did not consume ordinary capacity and could be admitted more than once. Ordinary and emergency capacities are now independent (`1/2` ordinary by era plus exactly one emergency), with exact restore validation.
- Society recurrence is now scoped correctly: per-character and per-generation keys are persisted separately from once-per-run completions, and a three-day category cooldown prevents same-category event spam.
- The full-world request entered Unity's full domain-reload path and left the Editor main thread nonresponsive before PlayMode. Restored project-local Enter Play Mode Options to disable domain reload (`enabled=1`, `options=1`); do not force-kill the user's Editor. A restart is required before the 68/68/68 gate can be rerun.
### 2026-08-09 기준 문서 최종 확인 중 진단 오류

- PowerShell 기본 출력 인코딩으로 `AGENTS.md`, `AGENT.md`, 전역 밸런스 기준 문서의 한글이 mojibake로 표시되었다. 파일 손상 여부는 UTF-8 바이트 기준으로 다시 확인한다.
- `git status --short` 실행 중 `.git/lfs/tmp` 접근 거부로 LFS clean filter가 실패했다. 기준 문서 연결 검증과는 분리해 다루며, 사용자 파일을 변경하거나 LFS 임시 데이터를 삭제하지 않는다.
# 오류 기록 2026-08-09

- 서비스 가격 경로 병렬 검색 중 존재하지 않는 `Assets/Scripts/Services/Visitor` 경로를 포함해 `rg`가 종료 코드 2를 반환했다. 유효한 `SaleItem`/가격 배율 결과는 보존하고, 이후 실제 존재하는 `Buildings`, `Models`, 전체 `Assets/Scripts` 범위로 좁혀 재검색한다.
- `SaleItem` 에셋을 찾기 위해 모든 `.asset`에서 일반 `MonoBehaviour` 문자열까지 포함한 검색은 범위가 지나치게 넓었고, 병렬 호출 중 한 명령의 종료 코드 1 때문에 다른 유효 출력도 묶여 반환되지 않았다. 타입 기반 Unity 에셋 조회 또는 해당 스크립트 GUID 역참조로 대체한다.
- `ShopInventoryRuntime`과 revenue 문자열을 묶은 검색에서 `Get-Content` 경로는 수정되어 성공했지만, 선택적 `rg`가 일치 항목 없음으로 종료 코드 1을 반환했다. 조회된 런타임 내용은 유효하며 이후 선택적 검색은 종료 코드에 의존하지 않도록 분리한다.
# 오류 기록 2026-08-09 소매 감사

- 소매 감사 첫 실행은 장검·나무 방패의 장비 아이템 ID가 일반 생산 레시피 EWU 그래프에 직접 연결되지 않아 양의 EWU를 찾지 못했고, 장검의 최대 프리미엄 마진이 반올림으로 35.1%가 되어 실패했다. 장비 정의의 BOM+제작 작업량을 장비 아이템 EWU로 투영하고, 프리미엄 상단은 반올림 허용 오차 또는 가격 내림 규칙으로 정리한다.
- 장비 EWU 연결 탐색에서 `equipment-item:*` 문자열이 에셋 YAML에 직접 나타나지 않아 선택적 검색이 종료 코드 1을 반환했다. 장비 아이템은 `ItemDefinitionSO.EquipmentId` 역참조와 전투 장비 정의 카탈로그로 연결한다.
- 장비 정의 조회는 유효한 장검/방패 `itemId`와 `equipmentId`를 찾았지만, 마지막 선택적 속성 검색이 일치 없음으로 종료 코드 1을 반환했다. 필요한 연결은 `CombatEquipmentDefinitionSO.ItemId`로 충분하다.
- `V23EmbeddedWorkValueCalculator.cs`를 고정 경로로 읽으려 한 호출은 실제 파일 위치가 달라 출력 없이 종료됐다. 먼저 `rg --files`로 선언 파일을 찾은 뒤 읽는다.
- 서비스실 코드 병렬 조회에서 선택적 패턴 검색 하나가 종료 코드 1을 반환해 다른 파일 출력이 묶여 유실됐다. 확인된 핵심 가격·시간 경로는 유효하며, `ServiceRoomAbilities`, `CreateContract`, 에셋 빌더를 고정 범위로 나눠 읽는다.
- 서비스 모델 검색에 Windows 경로 와일드카드 `Assets/Scripts/Models/Service*`를 직접 넘겨 종료 코드 1이 발생했다. 고정된 서비스실 경로만 사용한다.
- 계약 검색에 Windows에서 유효하지 않은 와일드카드 경로 `Services/Faction*`, `Models/Faction*`를 사용해 종료 코드 1이 발생했고 병렬 출력이 유실됐다. 실제 존재하는 `Services/Run`, `Models/Run`, 전체 `Assets/Scripts` 범위로 재조회한다.
# 오류 기록 2026-08-09 생산 경제 회귀

- `ProductionEconomyDebugScenarios.RunAll()`은 물리 생산 청구서 시나리오에서 `WorkOrderWorkerIneligible`로 실패했다. 계약 보상 변경과 직접 관련된 계산 오류가 아니라 V23 작업자 정책 이후 테스트 작업자/주문 조건이 맞지 않는 회귀 픽스처 문제다. 해당 시나리오의 정책·작업자 설정을 확인해 실제 런타임 계약에 맞춘다.
- 생산 회귀 테스트의 작업자 픽스처 검색은 실제 fake worker 선언이 없어 선택적 `rg`가 종료 코드 1을 반환했다. `BeginWork`의 작업자 타입과 자격 판정 어댑터를 직접 읽어 최소 유효 테스트 작업자를 구성한다.
- null 작업자를 실제 `CharacterActor`로 바꾼 뒤 회귀 테스트는 한 단계 진행했지만, 테스트 캐릭터에 `CharacterStatsProjectionService`가 주입되지 않아 실패했다. 게임 런타임은 컨테이너가 주입하므로 제품 버그가 아니라 에디터 픽스처 주입 누락이다. 기존 `CharacterAiEditorTestDependencies`의 캐릭터 주입 경로를 적용한다.
- 작업자 주입 후 생산 회귀는 다음 단계까지 진행했으나 테스트 레시피 두 개가 V23의 필수 `ProductionProcessClass`를 작성하지 않아 실패했다. 실제 카탈로그 354개는 모두 작성되어 통과한 상태이며, 테스트 레시피에 제분/발효에 맞는 공정 분류를 명시한다.
- 테스트 레시피 공정 분류 패치는 발효 레시피 `ConfigureWorkshop`의 실제 인자 구성이 예상과 달라 적용되지 않았다. 정확한 두 호출부를 다시 읽고 좁은 앵커로 수정한다.
### Error log — 2026-08-09

- A combined inspection command used a Windows-invalid wildcard path (`Assets/Scripts/Content/V20*`) with `rg`, causing exit code 1 after the useful file excerpt was printed. Resolved by reading `V20AuthoredContentContracts.cs` directly; no project state changed.
- The first authored-contract report patch referenced a not-yet-existing `GoldEconomyBalanceRules.WorkUnitsPerWorkerDay` constant. Repository search confirmed no such shared constant exists; the report was corrected to use the authoritative 99 WU/WD value locally pending a later shared time-baseline refactor.
- A multi-file legacy-ID patch failed atomically because the Korean description text in `V20SocietyWorldContentAssetBuilder.cs` did not exactly match the assumed wording. No file was changed. Resolution: patch only the stable ASCII ID fragments in smaller, independently verifiable edits.
- A multi-file cooldown patch reported failure at a later context while earlier file hunks had already applied. Verified actual filesystem state: definition, snapshot and builder cooldown fields/mappings are present; runtime enforcement is not. Resolution: treat `apply_patch` as potentially partially applied across files, inspect all touched targets, then patch the missing runtime hunk separately.
- Guest-request repricing correctly failed fast on legacy nonphysical ID `food:luxury-feast`. The builder stopped before catalog publication. Resolution: trace all 14 request IDs to registered physical items, correct the manifest, then rebuild and rerun the premium-margin audit.
## 오류 기록 - 2026-08-09

- `V23BalanceAudit.cs`의 위치를 확인하는 검색에 존재하지 않는 `Assets/Scripts/Editor/V23BalanceAudit.cs`를 함께 넘겨 검색 명령이 종료 코드 1을 반환했다. 실제 파일은 `Assets/Scripts/Services/Economy/Editor/V23BalanceAudit.cs`이며, 이후 정확한 경로만 사용한다.
- `GoldEconomyBalanceRules`를 읽을 때 요약에 적힌 파일명을 `Assets/Scripts/Services/Buildings/SO/StockCategoryCatalog.cs`로 추정했으나 실제 경로와 달라 검색이 실패했다. `rg --files`/정의 검색으로 실제 위치를 먼저 확정한다.
- `ResourceStockPolicyRuntime.cs`를 Economy 루트에 있다고 가정한 명시 경로가 틀려 검색 종료 코드 1이 발생했다. 실제 파일은 `Assets/Scripts/Services/Economy/Planning/ResourceStockPolicyRuntime.cs`다.
- `FacilityDebugScenarios.cs`의 깨진 한글 표시 문자열을 패치 문맥으로 사용해 검증 시나리오 삽입이 실패했다. 이후 인코딩 영향을 받지 않는 메서드 호출명과 메서드 선언을 앵커로 사용한다.
- 구매 원자성 수정 후 전체 `FacilityDebugScenarios`를 실행했으나 새 부분 정산 검사가 아니라 기존 `운영일 납품 제안` 시나리오가 실패했다. V23 감사는 명령 중 먼저 생성되었을 수 있으나 전체 명령은 실패로 처리됐다. 일일 제안의 현재 카탈로그 기대와 테스트의 낡은 조건을 추적한 뒤 재실행한다.
- 일일 납품 테스트의 `Weapon` 기대를 현재 구체 조달 카테고리인 `Ammunition`으로 고쳤지만 시나리오는 여전히 실패했다. 실제 `CharacterAiEditorTestDependencies.AuthoredGameplay`가 반환하는 제안 목록과 가격을 Unity에서 직접 출력해 남은 불일치를 확인한다.
- Unity 동적 명령에서 `CharacterAiEditorTestDependencies`가 internal이라 직접 접근하는 진단 스크립트가 컴파일 실패했다. 편집기 테스트 파일 내부에 일시적 진단 API를 추가하지 않고, 소스의 `AuthoredGameplay` 구성과 기본 배수를 직접 추적한다.
- internal 카탈로그를 읽기 위한 Unity 동적 리플렉션 진단은 MCP의 `System.Reflection` 보안 제한으로 거부됐다. `FacilityDebugScenarios`에 좁은 내부 진단 문자열 메서드를 추가해 같은 값을 안전하게 노출한다.
- 진단 메서드 추가 직후 동적 명령을 컴파일했으나 Unity 스크립트 어셈블리가 아직 재컴파일되지 않아 새 메서드를 찾지 못했다. 스크립트 에셋을 명시적으로 Import한 뒤 Unity 컴파일 완료를 기다리고 재시도한다.
- 스크립트 Import 성공 뒤에도 동적 명령이 이전 `FacilityDebugScenarios` 어셈블리를 참조했다. 콘솔 컴파일 오류 또는 도메인 리로드 정체 여부를 먼저 확인하며, 같은 명령을 반복하지 않는다.
- 동적 명령에서 `CompilationPipeline.RequestScriptCompilation()`이 생성 namespace의 이름 해석과 충돌해 `Unity.CompilationPipeline`로 잘못 해석되어 컴파일 실패했다. 완전 수식 이름 `UnityEditor.Compilation.CompilationPipeline`을 사용한다.
- 완전 수식 컴파일 요청은 성공했지만 5초 뒤에도 e6 Unity MCP 인스턴스의 동적 명령은 이전 편집기 어셈블리를 참조했다. 도메인 리로드 뒤 활성 MCP 인스턴스가 바뀌었을 가능성이 있으므로 연결된 두 Unity 엔드포인트의 프로젝트 정보를 비교한다.
- f764 보조 Unity MCP도 동일하게 이전 `FacilityDebugScenarios` 어셈블리를 참조했다. 연결 선택 문제가 아니라 스크립트 컴파일/리로드가 아직 완료되지 않은 상태다. CleanBuildCache 컴파일을 요청하고 충분히 기다린 뒤 콘솔과 API를 재확인한다.
- CleanBuildCache 요청과 20초 대기 후에도 MCP 동적 컴파일러가 이전 편집기 어셈블리를 참조했다. Console에는 오류가 없다. Unity 편집기 도메인 리로드가 MCP 세션에 반영되지 않는 환경 문제로 기록하고, 로컬 C# 프로젝트 빌드와 정적 검증을 병행한다.
- `dotnet build DungeonStory.sln --no-restore`는 이 PC에 .NET SDK가 설치되어 있지 않아 실행할 수 없었다. 새 의존성을 설치하지 않고, 설치된 MSBuild 존재 여부를 확인하고 없으면 Unity 컴파일 재연결을 최종 검증 경로로 유지한다.
- PATH에서 `MSBuild.exe`를 찾지 못했다. Unity 6000.3.8f1에 포함된 빌드 런타임이 있으면 그것을 사용하고, 없으면 외부 빌드는 생략한다.
2026-08-09 오류: `git status --short`가 Git LFS 임시 파일(`.git/lfs/tmp/...`) 쓰기 권한 부족으로 실패했다. 이번 작업은 Git 변경 명령 없이 파일 단위 검증을 계속한다.
2026-08-09 오류: `Assets/Scripts/Services/Economy/Runtime/ResourceStockPolicyRuntime.cs` 경로를 가정한 조회가 실패했다. 파일 위치를 `rg --files`로 다시 확인한다.
2026-08-09 오류: 목적지·등록 전역 검색 결과가 488행 이상으로 잘려 핵심 구현을 식별하기 어려웠다. 이후 `ItemTransferService.TryRequestItemDelivery`와 등록 파일의 좁은 범위만 조회한다.
2026-08-09 오류: 의복 물리 정의를 `apparel-item:` 접두사로 찾는 검색이 결과 없음(exit 1)으로 결합 조회를 실패시켰다. 실제 ApparelDefinitionSO.PhysicalItemId 값을 먼저 확인한다.
2026-08-09 참고: `Assets`/`docs` 아래 추가 AGENT(S).md 검색은 결과 없음(exit 1)이었다. 루트 지침만 적용한다.
2026-08-09 오류: ResourceStockPolicyRuntime 대규모 패치가 `IsOutboundDestination` 실제 구현 문맥 불일치로 전체 거부됐다. 파일을 범위별로 다시 읽고 작은 패치로 분할한다.
2026-08-09 참고: `ICombatEquipmentRuntime` 직접 구현체 패턴 검색이 결과 없음(exit 1)으로 결합 검색이 실패했다. 실제 구현체와 테스트 대역 누락은 Unity 컴파일 및 `: ICombatEquipmentRuntime` 일반 검색으로 확인한다.
2026-08-09 오류: Unity MCP 동적 명령에서 `CompilationPipeline.RequestScriptCompilation`이 래퍼 네임스페이스 때문에 `Unity.CompilationPipeline`로 잘못 해석되어 CS0234가 발생했다. `UnityEditor.Compilation.CompilationPipeline` 완전 수식명으로 재시도한다.
2026-08-09 오류: Unity clean compile 후 기존 구매 정산 테스트 9곳에서 `StockSupplyService.CalculateSettledDeliveryCost` 누락 CS0117이 발생했다. 소스에 메서드가 실제로 존재하는지와 클래스 경계를 확인해 선행 컴파일 오류부터 수정한다.
2026-08-09 참고: V23 감사와 물리 아이템 시나리오 결합 조회 중 한 검색이 결과 없음(exit 1)으로 실패했다. 물리 아이템 시나리오 구조는 확인됐고 V23 감사는 별도로 읽는다.
2026-08-09 오류: V23 감사 뒤 전체 PhysicalItemDebugScenarios 실행에서 기존 시나리오 3건이 실패했다: facility_delivery_buffer 다중 매치, physical_craft_material_gate 조기 작업 가능, equipment_module_physical_authority 모듈 누락. 신규 시나리오 자체 실패는 보고되지 않았다. 각각 신규 배송 변경의 회귀인지 기존 전체 시나리오 상태 오염인지 분리 실행한다.
# 2026-08-09 진행 중 오류 기록

- `TryRouteStackToDestination` 검색·출력 범위가 도구 출력 한계로 잘렸다. 품질 미달 고유 장비의 시장 도착·소비 시나리오를 완성하기 전에 좁은 범위로 다시 확인한다.
- 전체 물리 계약 실행에서 기존 3개 시나리오가 실패했다. `HasPendingWork`의 재료 게이트 누락은 실제 결함으로 확인했으며, 복수 스택 전달 검증과 모듈 로컬 버퍼 실패는 추가 조사한다.
- 인터페이스 위치를 함께 찾던 `rg`가 일치 항목 부족으로 종료 코드 1을 반환했다. 필요한 `HasLocalStack` 본문은 정상 확인했으며 검색 범위를 분리한다.
- `NormalizeModuleDestination` 검색은 해당 메서드가 없어 종료 코드 1을 반환했다. 생성 코드는 전달받은 destinationId를 Trim만 하며 별도 재작성하지 않는다.
- 수정 후 전체 물리 계약은 4개 관련 시나리오 중 시설 전달, 제작 재료 게이트, 품질 미달 고유품 판매가 통과했고 `equipment_module_physical_authority` 1건만 `EquipmentModuleMissing`으로 남았다.
- 내구 도구 정의 검색 명령은 출력 파이프의 종료 코드 1을 반환했지만 관련 사용 코드는 읽혔다. 모듈 실패 원인은 모듈 부재가 아니라 감정에 필요한 시험편·검사 게이지·룬 렌즈를 시나리오가 지급하지 않은 오래된 테스트 계약으로 확인했다.
- 물류·생존 검증기 전체 검색이 출력 한계/파이프 종료로 코드 1을 반환했다. 핵심 진입점은 좁혀서 `Batch B`, `Batch C`, `NeedBalanceCalibrationScenario`로 확인했다.
- `BatchCProductionInfrastructureAuthorityDebugScenarios`는 현재 V23과 맞지 않는 여러 고정 구버전 계약 때문에 실패했다: 저장 루트 V21 기대, ProductionBill V5 기대(실제 V6), 작업복 3개 기대(실제 4개), 산업 프로젝트 46개 기대(실제 31개), 고정 작업 타입 30개 기대(실제 31개), 구형 로컬라이제이션 키 집합, 오래된 아키텍처 보고서 등. 균형 수치와 구버전 검증 부채를 분리해 다룬다.
- 최신 `IndustrialInfrastructureStressProbe`도 10K 토폴로지는 만들었지만 2,000개 화물 경로 일부가 실패했다. 컨베이어 출력 포트 연결 규칙과 스트레스 픽스처의 포트 방향을 조사한다.
- 컨베이어 스트레스 크기를 2x1부터 100x100까지 줄여도 모두 실패했다. 대규모 성능 문제가 아니라 경로 성공 판정 또는 기본 그래프 계약의 논리 오류다.
- 최소 2x1 진단 결과 `success=0/1`, `NoRoute`, 경로 길이 0이다. 경로 길이 비교가 아니라 그래프 연결 자체가 생성되지 않는다.
- 컨베이어 능력/노드 정의 검색은 두 번째 패턴이 일치하지 않아 종료 코드 1을 반환했지만 능력 필드는 확인했다.
### 2026-08-09 validation note

- A broad infection/disease/epidemic scenario search produced more output than the tool context could retain. Treat that attempt as inconclusive; repeat it with file-only and domain-limited searches before selecting focused probes.
- Unity MCP rejected a temporary focused industrial runner because dynamic commands may not import `System.Reflection`. Do not bypass the guard; expose an explicit source-level focused entry point that calls the existing private checks instead.
- Focused industrial contracts exposed a current content defect: `장기 재생 수술실` supports surgery but lacks `BuildingProcessFluidAbility`, so sanitation/process-fluid validation stops before the remaining contracts. Inspect the asset/builder and add the missing clean-water/wastewater authority if consistent with its domain role.
- The first search for the organ-regeneration facility was too broad and truncated because numeric ID `8868` matched generated catalogs. Subsequent investigation must target the exact building asset and the research-overhaul builder methods only.
- A V23 audit rerun command incorrectly assumed `V23BalanceAudit.Generate()` returned a string; it returns `void`. The dynamic command failed compilation before execution and made no project change. Rerun it as a void call and read its generated report separately.
- After regenerating research-overhaul assets, the V23 audit failed broadly: generated item prices and guest/retail rewards reverted to their pre-calibration values. This is builder-order drift, not evidence that the prior economic formulas were wrong. Find and rerun the authoritative V23 balance authoring step, then ensure future research regeneration invokes or preserves it before auditing.
- Focused `PlayerFairnessDebugScenarios` currently fails only because its fixture asserts `DungeonWildlifeSaveData.CurrentVersion == 3`. Inspect the actual current wildlife save contract and update the stale fixture if the version bump is intentional and covered by strict restore validation.
- A focused environment command incorrectly assumed `EnvironmentalFieldDebugScenarios.RunAll()` returned `bool`; it returns `void`. Compilation failed before execution. Rerun it as a void call.
- Focused environmental contracts failed in two places: the fixture still expects 3 authored workwear definitions although V22/V23 has 4, and a valid empty character-environment payload does not pass/publish. Update the stale count, then inspect the strict payload validation to determine whether the empty-payload failure is a fixture omission or a real restore defect.
- After updating V5 arrays and the count, the environmental suite advances but cannot resolve `equipment:cold-work-suit` as a unique item through its legacy `Assets/Resources/SO/Economy/Items` lookup. Inspect the current item catalog/path and update the fixture to use the root physical item authority rather than assuming the old folder.
## 작업 중 오류 기록 (2026-08-09 추가)

- 전투·원정·캠페인 디버그 시나리오의 공개 진입점을 여러 대형 파일에서 한 번에 검색해 출력이 컨텍스트 한도를 초과했다. 이후 파일별 제한 검색으로 전환한다.
- 전투·원정·캠페인 Editor 폴더에서 Monte Carlo·승률·백분위·대규모 시드 프로브 명칭을 검색했으나 일치 파일이 없어 `rg`가 종료 코드 1을 반환했다. 이는 도구 실패가 아니라 해당 검증기의 부재 증거로 기록한다.
- 전투·원정·캠페인 집중 계약 실행에서 `CombatSystemDebugScenarios`가 `qa:combat-craft`, `qa:building-craft` 주문의 작업량 계약 위반으로 실패했고 탄약 소비 권위 시나리오도 실패했다. V23 작업량 권위 변경과 오래된 전투 픽스처의 불일치인지 실제 런타임 회귀인지 분리 진단한다.
- 탄약 소비 검증 메서드를 `private static void`로 가정해 검색했으나 실제 시그니처가 달라 일치하지 않았다. 메서드명 자체로 다시 제한 검색한다.
- Unity 탄약 권위 덤프 동적 명령이 제네릭 메서드 그룹 추론과 잘못 가정한 `CombatWeaponSO.DefinitionId` 때문에 컴파일되지 않았다. 명시 람다와 실제 ID 프로퍼티를 확인한 뒤 재실행한다.
- 장비 정의 파일명을 추정해 제한 검색했으나 두 추정 경로가 존재하지 않아 `rg`가 오류를 반환했다. 클래스 선언 위치를 먼저 검색해 실제 파일을 사용한다.
- 수정한 Unity 탄약 덤프도 동적 명령 어셈블리에 Sirenix.Serialization 참조가 없어 `ResourceItemDefinitionSO`를 직접 로드하는 지점에서 컴파일되지 않았다. 프로젝트 내부 검증 클래스를 보강하거나 YAML/기존 카탈로그 API를 통해 우회한다.
- `ResourceItemDefinitionSO` 위치와 필드를 찾는 검색을 잘못된 추정 경로 및 넓은 `Assets/Scripts/Models` 범위로 함께 실행해 출력이 다시 잘렸다. 확인된 실제 파일 `Assets/Scripts/Models/Economy/Content/ResourceItemDefinitionSO.cs`만 읽는다.
- `ResourceGameContentCatalog.GetAll<ResourceItemDefinitionSO>()` 우회도 동적 명령 어셈블리의 Sirenix 참조 부재 때문에 컴파일되지 않았다. 동적 덤프 대신 프로젝트에 이미 컴파일되는 검증 코드를 현재 권위 계약으로 직접 수정한다.
- 단검 정의의 `RequiredCraftWork`를 사용하도록 픽스처를 바꾼 뒤에도 두 복원 주문이 `invalid work`로 실패했다. V23 저장 계약에 추가된 작업량 캡처/기여 필드 또는 작업량 정수 정규화 조건을 확인한다.
- `CraftQualityRollSaveData` 검색은 필요한 선언을 찾았지만 PowerShell 파이프 실행이 종료 코드 1로 보고됐다. 출력 자체에서 필드 계약은 확인했으므로 제한된 직접 파일 조회로 후속 검증한다.
- 픽스처에 품질 롤을 추가한 직후 Unity 동적 명령이 임시 `AssistantRunCommand` DLL 또는 종속성을 로드하지 못했다. 코드 계약 실패가 아닌 에디터 재컴파일 경합으로 보고 컴파일 안정화 후 재실행한다.
- Offense Editor 시나리오에서 문자열 `new EnemyEncounterFactory`를 검색했으나 target-typed `new(...)` 또는 별도 팩토리 헬퍼를 사용해 일치하지 않았다. 타입명 전체 참조로 다시 찾는다.
- `EnemyCombatContentCatalog`을 별도 파일로 추정해 생성자 검색했으나 실제로는 `EnemyIndividualRuntime.cs`에 함께 선언되어 한 경로가 없었다. 확인된 파일만 제한해서 읽는다.
- 기본 공격 처리 검색은 유효한 결과를 출력했지만 PowerShell 파이프가 종료 코드 1로 보고됐다. 필요한 전투 공식과 공개 미리보기 API는 확인했으며 이후 직접 범위 조회를 사용한다.
- 최초 전투 콘텐츠 예산 프로브가 116건 실패했다. 보고서를 통해 실제 이상치와 프로브의 보상 ID 해석 오류를 분리한 뒤 게이트를 확정한다.
- `offense:unappraised-loot` 참조 검색은 실제 정의·감정 조합식·소비 경로를 찾았지만 PowerShell 파이프가 종료 코드 1로 보고됐다. 보상 ID 자체는 유효하고, 프로브의 ItemDefinition 수집 방식이 루트 카탈로그의 구체 하위 타입을 빠뜨린 것으로 진단한다.
- 전투 예산 프로브의 아이템 수집·강도 밴드 수정 패치를 한 번에 적용했으나 하단 `ParseEncounterNumber` 주변 문맥이 달라 전체 패치가 거부됐다. 파일을 구간별로 읽고 작은 패치로 나눈다.
- 캠페인 기준으로 전환하던 전투 예산 프로브 패치가 중간에 끊겨 호출부와 메서드 시그니처, 이전 `strengthMedians` 참조가 동시에 남았다. 파일 전체를 다시 읽고 한 번의 일관된 패치로 복구했다.
- 캠페인 카탈로그와 목표 ID를 프로젝트 전체에서 한 번에 찾는 검색이 10초 제한을 초과했다. 결과에 실제 파일 위치가 포함됐으므로 이후 `OffenseCampaignCatalogSO.cs`, 해당 에셋과 보상 서비스만 직접 조회한다.
- 첫 전략 콘텐츠 밸런스 감사가 9건 실패했다. 보고서를 읽어 실제 계약/장 비용 불균형과 감사식의 잘못된 가정을 분리한 뒤 콘텐츠 또는 밴드를 교정한다.
- Windows 경로에 `Equipment*` 와일드카드를 직접 넘긴 검색 한 구간이 잘못된 경로 구문으로 오류를 냈다. 필요한 계보 인장 생성·클레임 참조는 다른 명시 경로 검색에서 확보했으므로 이후 실제 파일만 조회한다.
- 전략 보고서 발췌와 함께 실행한 `git status --short`가 `.git/lfs/tmp` 접근 거부로 종료 코드 1을 반환했다. 보고서 조회는 성공했으며 Git 상태 문제는 밸런스 런타임 검증과 분리한다.
- 계절 사건 빌더 조회와 후속 시나리오 검색을 한 명령에 묶었는데, 빌더 조회는 성공하고 두 번째 검색이 일치 항목 없음으로 종료 코드 1을 반환했다. 기존 별도 계절 밸런스 시나리오는 없다는 증거로 취급한다.
- 축제 정의 위치 검색은 실제 파일이 `Assets/Scripts/Models/Species/Core`에 있음을 찾았지만 이어서 추정한 `Assets/Scripts/Content` 경로를 읽어 실패했다. 확인된 실제 경로만 다시 읽는다.
- 계절·축제·서비스 강도 게이트의 첫 실행이 16건 실패했다. 실제 보고서 분포를 읽어 기준 밴드가 과도한지, 특정 축제 투입량이 비정상인지 분리한다.
- 축제 미해석 물품 검색에서 존재하지 않는 추정 폴더 `Assets/Resources/SO/V22`를 함께 넘겨 종료 코드 1이 발생했다. 실제 경제 아이템·레시피 결과는 확보했으며 확인된 경로만 사용한다.
- 축제 빌더의 여러 행을 한 번에 교체한 패치가 파일의 현재 줄 문맥과 일치하지 않아 전체 거부됐다. 정확한 현재 행을 다시 읽고 작은 ID/수량 치환 패치로 나눈다.
- 축제 에셋 재생성 후 전략 감사가 16건에서 4건으로 줄었지만 아직 실패했다. 남은 네 축제의 실제 EWU/참가자 분포를 확인해 수량 또는 허용 상한을 교정한다.
- 밸런스 기준 문서와 작업 계획을 한 번에 넓게 조회해 출력이 컨텍스트 한도를 넘었다. 이후 전투·전략·이정표 범위만 줄 단위로 나누어 읽는다.
- 오류 기록을 먼저 추가하려던 `apply_patch`가 내용 없는 hunk라 거부됐다. 파일에는 변화가 없었고, 실제 오류 기록 문맥을 확인한 뒤 정상 패치로 다시 추가했다.
- `findings.md` 갱신을 시작하며 다시 내용 없는 hunk를 보낸 패치가 거부됐다. 파일 변화는 없으며 이후에는 확인한 마지막 문맥과 실제 추가 내용을 포함해 패치한다.
- Unity MCP 도구 이름을 찾는 첫 필터가 모든 Unity 도구 설명까지 출력해 컨텍스트 한도를 넘었다. 이후 이름만 추출하고 `Unity_RunCommand`, `Unity_ReadConsole`로 범위를 제한한다.
- 최종 검증 진입점 검색에서 `V23BalanceAudit.cs`의 경로를 잘못 추정해 결합 명령이 종료 코드 1로 중단됐다. 실제 파일 위치를 `rg --files`로 확인하고, 저장 러너 조회는 별도 명령으로 다시 수행한다.
- 빈 PlayMode에서 `DungeonGameSaveDebugScenarios.RunFullGameRoundTrip()`을 직접 호출하자 시작 주인 Aggregate가 준비되지 않아 `Owner runtime is missing`으로 중단됐다. 부분 변경은 러너의 baseline/finally 범위 전 단계에서 발생하지 않았다. 전용 `DungeonFullWorldRoundTripPlayModeFacade`가 준비하는 씬·시드·주인을 사용해 재실행한다.
- 전용 전체 월드 왕복은 68/68/68, baselineRestored=True, canonicalBaselineMatched=True까지 도달했지만 V23 이전 테스트 계약 두 곳 때문에 최종 실패했다: `CharacterProgressionSavePlayModeFacade`의 구형 성장 저장 거부 문구 기대와 `qa-save-craft-order`의 오래된 작업량/품질 저장 픽스처. 현재 저장 권위에 맞춰 테스트 데이터를 교정한 뒤 재실행한다.
- 첫 픽스처 교정 후 캐릭터 진행 저장 계약은 통과했지만 장비 저장 캡처 검증이 여전히 구형 고정 작업량 `6.75/10`을 기대해 실패했다. 런타임은 정상적으로 68/68/68과 기준선 복원을 유지했다. 검증도 에셋 기반 작업량·진행값을 비교하도록 교정한다.
- 실제 전투 승률 프로브 API 검색을 서비스·모델 여러 경로에 넓게 걸어 한 경로의 불일치로 종료 코드 1이 발생하고 출력도 과도했다. 확인된 핵심 파일 `OffenseBattleModel.cs`, `OffenseBattleContracts.cs`, 기존 전투 시나리오만 범위별로 읽는다.
- 전투 장비 정의 파일을 `Models/Combat/Core/CombatEquipmentDefinitionSO.cs`로 추정해 조회했으나 실제 경로가 달라 결합 명령이 실패했다. 클래스 선언 위치를 먼저 찾아 확인된 파일만 읽는다.
- 장비 ID 목록 추출에서 GNU grep식 `rg -h`를 사용해 ripgrep 도움말이 출력됐다. `-h`를 쓰지 않고 경로 포함 결과에서 ID만 읽는다.
- 새 실제 전투 결과 프로브를 추가한 직후 동적 명령이 아직 재컴파일 전 어셈블리를 참조해 타입을 찾지 못했다. Unity Console의 실제 스크립트 컴파일 오류를 먼저 확인하고, 에셋 import/도메인 리로드 뒤 실행한다.
- 전투 결과 프로브의 컴파일 상태를 확인하려고 Unity Console 오류·경고 전체를 한 번에 요청해 출력이 컨텍스트 한도를 초과했다. 이후 최근 20건과 파일명 필터만 사용해 실제 컴파일 오류를 좁힌다.
- 전투 결과 프로브의 첫 실제 실행은 1,152회 중 114회가 종료되지 않아 게이트에서 중단됐다. 보고서상 원인은 단순 난이도 과다가 아니라 보호 목표 NPC 부재, 생포용 비살상 준비 누락, 사거리 밖에서 이동 행동이 없는 프로브 AI가 섞여 있으므로 수치 조정 전에 목표/행동 픽스처를 실제 플레이 계약과 맞춘다.
- 진정 탄약과 전투 스냅샷을 함께 찾던 결합 검색은 첫 검색 결과는 확보했지만 두 번째 클래스 위치 불일치로 종료 코드 1을 반환했다. 확인된 석궁 호환 진정 다트를 사용하고 스냅샷 선언은 클래스명 전역 검색으로 따로 찾는다.
- 원정 전투력 공식과 캠페인 에셋을 한 명령에서 읽을 때 추정한 캠페인 에셋 경로가 틀려 두 번째 조회가 실패했다. 실제 에셋은 `Assets/Resources/SO/Content/OffenseCampaignCatalog.asset`임을 확인했다.
- 전투력 배율 스윕 추가 직후 Unity 동적 명령 DLL이 재컴파일 경합 중 로드되지 않았다. 프로젝트 코드 오류로 단정하지 않고 에디터 컴파일 완료와 Console을 확인한 뒤 스윕만 다시 실행한다.
#### Phase 145 V25 nine-proficiency progression and decay integration

- [x] Establish the nine canonical proficiency definitions and map all authored work/content to an explicit primary or composite proficiency.
- [x] Replace routine generic-level XP with contribution-based proficiency XP, quality/speed/safety projections, combat practice, anti-repeat rules, and lazy expert/master decay.
- [x] Extend persistent character narrative and mentorship state without increasing the 68-section save contract.
- [x] Connect mentorship assignment, character proficiency presentation, worker filtering, and explicit failure reasons to normal gameplay UI.
- [x] Update the whole-game balance baseline, comprehensive design document, generated mapping evidence, and focused deterministic validation.

Phase 145 implementation contract:
- Immutable proficiency definitions belong to authored content/catalog authority; mutable XP, decay clocks, and mentorship assignments belong to runtime aggregates and save DTOs.
- Accepted work contribution is the only routine-work XP authority. Waiting, cancellation, missing materials, idle guard duty, and failed paths award zero proficiency XP.
- Existing unrelated dirty worktree changes are preserved. No bulk asset rebuild or unrelated formatting pass is allowed.
- Research 180/138,824, V20 net-new 450, and save section count 68 remain unchanged.

Phase 145 validation errors:
- Initial `git status --short; git diff --stat` invoked the Git LFS clean filter and failed because `.git/lfs/tmp` was not writable in the sandbox. It changed no tracked file. Subsequent read-only status checks will disable the LFS filter locally per command and all edits remain scoped to proficiency files.
- A discovery batch guessed `Assets/Scripts/Content/V20AuthoredContentSO.cs` and ended after that missing optional path. The authoritative base is `Assets/Scripts/Content/V20AuthoredContentContracts.cs`; no mutation occurred.

Phase 145 final evidence:
- Nine canonical proficiencies and all 31 work kinds have one explicit proficiency or intentional no-XP mapping.
- Authored mappings cover 419 buildings, 354 recipes, 61 combat equipment definitions, and 56 apparel definitions.
- The 100,000-sample quality probe, 960-day progression/decay simulation, and 2,000-resident lazy-settlement probe passed; the latter measured `0.459ms`, `0B`, and no hourly global resident scan.
- Mentorship conditions, daily limits, both 30 WU contributions, save restoration, and both target-resolution pointer flows passed.
- The final full-world gate passed `68/68/68`, canonical and live baseline restoration, and Unity Console Error 0 / Warning 0.

#### Phase 146 proficiency-derived detailed performance authority

- [x] Inventory every read/write/save/content path for the legacy twelve independently rolled and level-grown character stats.
- [x] Make the nine proficiencies plus innate proficiency aptitude the only persistent progression authority and define deterministic work-speed, quality, accident-risk, and compatibility projections.
- [x] Remove routine creation and level-growth writes to independent stat blocks; retain the serialized legacy block only as a non-authoritative compatibility boundary.
- [x] Route work speed, construction/equipment/apparel quality, active-work accident risk, surgery compatibility reads, and worker eligibility through the proficiency authority without dual counting.
- [x] Update character UI, worker selection, save rejection/versioning, documentation, and focused deterministic validation.
- [ ] Re-run the 31-work/content mapping, 100,000 quality sample, progression/decay, 2,000-character performance, target-resolution UI, and final 68/68/68 Console 0/0 gates.

Phase 146 structure contract:
- Immutable authority: the existing nine `ProficiencyDefinitionSO` assets and explicitly authored proficiency-to-performance projection rules.
- Mutable authority: current/lifetime proficiency milli-XP, practice/decay clocks, mentorship, and innate proficiency aptitudes on the character narrative/progression aggregate.
- Derived-only effects: work speed, completion quality, accident risk, and combat/service/research/medical outcomes. The twelve legacy numeric labels exist only inside a compatibility adapter for old consumers; they are never independently rolled, level-grown, saved as progression authority, or presented as a second player-facing system.
- Commands: approved work contribution, combat evidence, mentorship, and explicit life/content effects are the only proficiency mutations.
- Queries: UI, AI, work execution, combat, and quality systems read detailed performance through one projection service that combines proficiency, species/body, traits, equipment, health, fatigue, mood, and environment without mutating state.
- Save boundary: derived values and caches are not saved. The relevant existing section version is raised and pre-refactor development saves are explicitly rejected; total section count stays 68.
- Failure policy: missing proficiency definitions or projection mappings fail catalog validation and surface an exact unavailable reason; no silent neutral-stat fallback is introduced.
- Balance status: this phase targets formula/simulation verification only. Cross-domain live-play calibration remains separate until representative long-run telemetry exists.

Phase 146 validation errors:
- The first broad PowerShell discovery command ended with exit code 1 because a Windows `rg` argument used the literal wildcard path `CharacterNarrative*`. Earlier portions completed and changed no files; subsequent searches use directory plus `--glob` filters.

#### Phase 147 founder trait roster expansion to exactly 100

- [x] Inventory the live trait authority, current builder, save/UI projection, and focused distribution audit.
- [x] Define the final 100-trait manifest: retained identity traits, natural proficiency tradeoffs, simple positive traits, simple negative traits, and seven extreme traits.
- [x] Implement the shared data/runtime additions needed for proficiency deltas, polarity, identity effects, deterministic extreme triggers, strict identity-state persistence, and exact-count validation.
- [x] Build/index exactly 100 trait assets through Unity MCP without mouse input and retire development trait IDs without reusing their meanings.
- [x] Re-run deterministic distribution, family/species collision, shared-effect source, identity-state, Mythic, compilation, PlayMode, and founder production-impact verification after the latest runtime wiring. The 100k/1m audit, 10k-party bottom-up audit, equipment-readiness audit, compile, and 68/68/68 full-world gate pass.
- [x] Update the whole-game balance baseline, findings, and progress with the implementation contract and current unverified state; report balance complete only after the Unity MCP gates pass.

Phase 147 decisions:
- The final catalog count is exactly 100, including seven extreme traits.
- Natural proficiency tradeoffs raise one proficiency and lower a narratively related weakness; mirrored A/B pairs and rigid equal deltas are forbidden.
- Simple positive and simple negative traits are both expanded. Negative traits are weighted to form a meaningful minority without a forced negative slot.
- Existing 1/2/3/4 trait-count weights 15/40/35/10, unlimited manual reroll, family exclusion, species eligibility, and age proficiency caps remain authoritative.
- Unity asset generation and all Unity execution use Unity MCP only.

Phase 147 errors:
- Initial `git status --short; git diff --stat` invoked the Git LFS clean filter and failed because `.git/lfs/tmp` was not writable. No tracked file was changed; do not repeat the same status command.
- The first planning-file status patch used a stale checklist anchor and failed without changing files. Subsequent notes append under the stable Phase 147 headings.
- Unity MCP Console exposed four real compiler errors: `DungeonStory.Combat` could not see `IGameplayEffectSource`, `GameplayEffectSourceRef`, or `GameplayEffectBinding` because the contracts were authored in the predefined assembly. Move the dependency-free contracts to `DungeonStory.Foundation` and leave the higher-level projector outside that asmdef.
- The clean rebuild exposed two more errors hidden as Console `Log`: Foundation cannot inherit Economy's `DataScriptableObject` without a cycle. The effect/condition definitions retain their own stable numeric ID while inheriting directly from `ScriptableObject`; they remain normal catalogued SO content and do not create a reverse dependency.
- The next clean compile showed `CharacterTraitSO` could not see `CharacterIdentityRule` for the same asmdef reason. The dependency-free polymorphic rule payload definitions move to Foundation; runtime routers/state remain in the higher Character service.
- Splitting the projector exposed `GameplayEffectContribution.Definition` as assembly-internal. It is runtime trace data, so the definition reference is made publicly readable/writable across Foundation and the higher projector assembly.
- The builder ran, but Unity warned that effect/condition SOs had no matching script asset because both were declared in `GameplayEffectContracts.cs`. Split each UnityEngine.Object-derived type into a same-named file and let the builder replace only malformed generated V26 assets before rerunning.
- Starting-XP compilation found the validator accepts `IReadOnlyList` rather than `IList`; pass a deterministic array snapshot after mutation.
- Identity event compilation exposed that `CharacterId` intentionally has no `==`/`!=` operators; deterministic event filtering now uses value equality.
- Adding identity state to narrative save version 7 initially broke editor fixtures using the two-argument runtime constructor. Optional compatibility injection preserves those focused fixtures while live DI supplies the authoritative identity store and content catalog.
- Unity MCP stopped answering editor, console, and command calls after the Unity Licensing Client disconnected. No shell/editor restart or non-MCP Unity execution was used. Static source work continued, but the latest changes remain explicitly uncompiled and unaudited until MCP recovers.
- After MCP recovery, the first dynamic refresh command failed only inside the command assembly because `CompilationPipeline` resolved through the injected `Unity.*` namespace instead of `UnityEditor.Compilation`. No project mutation or project compile occurred; retry with an explicit `global::UnityEditor.Compilation.CompilationPipeline` qualifier.
- The recovered Unity compile exposed two project errors logged as Console `Log`: an anonymous object assigned to `CombatEquipmentRuntimeState`, and a conditionally assigned `ExtremeRiskResolution` read after short-circuiting. Instantiate the real runtime state and initialize the out payload before the guarded miracle-surgery call, then rerun the full compile.
- The next compile passed the two runtime fixes and exposed one stale Editor fixture constructing `ProductionBillSceneFacade` with the old four-argument signature. Supply isolated identity/extreme dependencies in the deterministic production fixture; live DI remains unchanged.
- The first recovered full-world round-trip run stalled before writing a report because `CharacterTransientGameplayEffectSourceQuery` requested unregistered concrete `BlueprintResearchRuntime`; scene injection then emitted repeated character/grid errors. Stop the failed PlayMode through Unity MCP and depend on the registered `IBlueprintResearchStateService` instead, preserving the research aggregate as state authority.
- After the DI fix, the full-world gate reached 68/68/68 but failed baseline restoration because the derived-stat read path called mutating `GetActiveProfileSnapshot`, creating saved default loadouts for transient QA/preparation characters. Add a non-mutating `TryGetActiveProfileSnapshot` query and use it for effect projection so read-only stat calculation cannot create combat state.
- The founder-industry audit correctly found zero no-research output because every authored gathering/production/apparel route is research-gated and Day 1 is explicitly starting-stock-only. The report still lacked the cost of leaving that state; add actual scholarship plus ResearchSpeed trait projection and minimum prerequisite-closure WU/days for the first raw/intermediate/food/finished/apparel unlock.

Phase 147 final direct-order and regression evidence:
- The staff work-priority surface previews trait-driven direct-order costs before a cleaning-priority downgrade. Trait 245 displays mood -2 / stress +3; clean `Off -> Priority1` and non-clean changes remain free.
- The cost is applied only after the player-facing priority mutation succeeds. The deterministic V26 audit verifies preview values, trigger policy, the resulting mood factor, and lifecycle stress.
- The final V26 audit remains PASS at 100 traits, 100,000 rerolls, 27.9204% negative slots, 0.9618% extreme slots, 0 multi-extreme results, and 3.0083% Mythic across 1,000,000 eligible rolls; normal Mythic remains 0.
- The P1 work-priority UI scenarios pass, and the final full-world PlayMode gate passes 68/68/68 with canonical baseline restoration and Unity Console Warning 0 / Error 0.

#### Phase 148 founder trait vector and daily-schedule balance simulation

- [x] Inventory the authoritative base workday, work-speed, food-consumption, accident exposure, successful-work XP, research prerequisite and equipment BOM/work formulas used by the current founder baseline.
- [x] Define and execute the first deterministic non-scalar trait projection: WU throughput, consumption index, expected accident count/damage, earned-XP multiplier and event/state-only identity outcomes remain separate axes.
- [ ] Simulate at least 10,000 naturally rolled three-founder parties under explicit balanced and research-focus daily schedules, reporting p10/median/p90 rather than only a mean ceiling.
- [x] Quantify the first 36-WU research unlock, essential-industry capacity, food demand, expected accident/damage exposure and trait-only gap between no-reroll, practical reroll and extreme bounds.
- [ ] Add exact accident-time lost WU, real meal/sleep/movement/mood interruption, founder proficiency growth at each readiness checkpoint and quality-value yield to the p10/median/p90 day-schedule simulation.
- [x] Run compilation and deterministic audits through Unity MCP, verify Console 0/0, and record the current formulas, limitations and evidence in the baseline, findings and progress.

Phase 148 implementation contract:
- WU remains physical approved work, not a universal trait score. Only actual speed effects modify WU throughput.
- Time/need traits modify available daily work time; consumption traits modify physical food demand; accident traits modify expected lost work/material; quality traits modify output-quality distribution; identity and extreme rules remain event/state outcomes.
- The simulation must use the same founder generation, proficiency caps, trait selector, shared-effect bindings, and typed identity-rule definitions as live content. It may not assign hand-authored trait scores.
- Average-only success is insufficient. Report natural party p10/median/p90 and expose impossible or assumption-dependent paths explicitly.
- This phase changes no gameplay numbers unless the measured result demonstrates a concrete violation and the user approves the correction target.

Phase 148 errors:
- The first broad need/time authority search returned useful matches but exceeded the output limit and ended with exit code 1. Subsequent reads are restricted to `NeedBalanceCalibrationScenario`, projection services, and exact trait builder ranges.
- A discovery read assumed the planned `GameplayEffectConditionEvaluator.cs` path, but the live condition context is implemented inside `GameplayEffectContracts.cs`; the missing optional file changed nothing.

#### Phase 149 founder-trait end-to-end connection closure (completed)

- [x] Add an explicit AGENT.md completion gate that forbids accepting definition/projection-only gameplay effects and requires producer -> authority -> condition -> consumer -> save/UI/test evidence for every authored binding and identity rule.
- [x] Inventory every founder-trait effect target, condition ID, identity event/rule, runtime command, public runtime API and serialized field; classify every endpoint as producer, projector, consumer, adapter, persistence, migration or audit and record every orphan in `Artifacts/QA/v26-founder-trait-connectivity-audit.md`.
- [x] Make the canonical derived-stat projection the only trait/equipment/species/status/research numeric authority and expose typed/named queries for every authored target.
- [x] Connect hunger/food demand, accident execution, temperature exposure, quality, salvage, haul capacity, fatigue, recovery, food poisoning, relationship recovery, negative mood duration and every retained custom target to their owning runtime systems.
- [x] Replace the coarse AI prefix matcher with exact action semantics supplied by real AI actions/work candidates; connect autonomous restrictions and direct-order consequences without silent fallback.
- [x] Connect typed identity event publishers/subscribers, persistent deprivation clocks, mood durations, product/work/social/health/expedition events, and all extreme-trait player/automatic command paths.
- [x] Add deterministic source-to-consumer coverage audits that enumerate all 100 traits plus every related public API and serialized field on every run, and fail for an unconsumed target, never-emitted condition, unreachable identity event, missing caller, duplicated authority or dead serialized field.
- [x] Rebuild assets, compile, inspect Console and run all focused/full regression gates through Unity MCP only; then rerun the founder WU/industry/readiness simulation using live effects.

Phase 149 fixed constraints:
- A serialized value is not implemented until a live producer supplies its context and a real domain consumer changes authoritative state or outcome.
- `CharacterGrowthState.traitIds` remains the sole selected-trait authority; definition SOs contain authored content only and no mutable runtime state.
- Every numeric trait effect must project into a named detailed stat. Facilities and work executors may consume typed query methods, but may not inspect trait IDs or bindings directly.
- Identity rules remain event/state behavior and are not collapsed into WU. Their event publishers, state keys, save/restore and observable consequence must all be proven.
- No mouse input is permitted. Unity refresh, asset build, compile, Console and tests use Unity MCP only.
- Existing dirty-worktree changes are preserved. Phase 149 edits are limited to the trait/effect/consumer/audit/agent-guidance paths required by this objective.

Phase 149 error log:
- Session catch-up reported 73 unsynced messages and a very large pre-existing dirty worktree. The target trait/effect files are existing Phase 147 changes or untracked additions; unrelated assets and generated training deletions must not be touched.
- Two combined planning-file patches used stale findings anchors and failed atomically without changing files. Subsequent planning updates patch each file independently at a stable end-of-file anchor.
- Unity Console의 컴파일 진단이 `Error`가 아니라 `Log` 유형으로 기록될 수 있음을 재확인했다. 이후 모든 컴파일 게이트는 `All` 유형과 `Assets/Scripts` 진단을 함께 검사한다.
- 최초 동적 API 감사는 `CharacterTraitSO`가 속한 어셈블리만 탐색해 다른 런타임 어셈블리의 `ArcaneOverchargeCommandRuntime` 타입을 찾지 못했다. `DungeonStory*`와 `Assembly-CSharp*` 전체 로드 어셈블리에서 타입을 해석하도록 수정했다.
- 추상 어댑터의 `Start` 의도 속성이 override 리플렉션에서 안정적으로 계승되지 않았다. 실제 파생 어댑터 13개의 `Start`에 의도 속성을 직접 부여했다.
- PowerShell에서 지원되지 않는 `??` 연산자를 사용한 읽기 전용 진단 명령이 실패했다. 파일 변경은 없었고 이후 명시적 null 분기로 교체했다.
- Unity 동적 명령의 `CompilationPipeline` 이름이 프로젝트 타입과 UnityEditor 타입 사이에서 모호했다. `UnityEditor.Compilation.CompilationPipeline` 완전 수식 이름을 사용했다.
- 전수 결과를 읽는 PowerShell 명령에서 `Select-Object -Index 130..175`를 배열로 감싸지 않아 읽기 전용 조회가 실패했다. 코드·에셋 변경은 없었고 이후 고정 범위 배열 또는 `Get-Content` 인덱싱을 사용한다.

Phase 149 connection closure progress:
- [x] `state:emergency-stocked`와 `stockpile:emergency-ready/shortage`를 품목별 저장 정책의 명시적 비상 지정, 기존 최소 재고, 실제 월드 스택, 창고 UI 명령, 일일 정체성 기분 판정에 연결했다.
- [x] `work:substitute-material`, `state:insulted`, `state:ritual-fasting`, `state:ritual-fast-ended`를 실제 명령·상태·소비자에 연결한다.
- [x] `consume:luxury`, 호화/기본 생활 지속 욕구, 장기 냉기·불쾌 온도·부패 오염 사건을 실제 식사 후보·식사 결과·환경 노출에 연결한다.
- [x] 정체성 사건, AI 의미 태그, 조건, 수치 target, 극한형 명령, snapshot/UI/구형 권위 경계와 자동 고아 감사를 모두 실제 실행 경로에 연결했다. 최종 manifest는 rows 541, targets 45, conditions 45, identity 63, behaviors 38, needs 9, extremes 7, publicApis 104, helperMethods 77, serializedFields 126, orphans 0이다.
- [x] 효과·정체성 범위의 public API 104개를 매 실행마다 동적으로 열거한다. 상태 변경·생명주기 함수는 `GameplayEntryPoint`, `GameplayInternalOnly`, `GameplayMigrationOnly` 중 정확히 하나의 의도 속성과 허용 호출 증거를 요구한다.
- [x] 같은 범위의 private/internal/protected helper도 동적으로 열거해 선언 외 호출·delegate 구독·내부 교차 파일 참조가 없는 죽은 함수를 실패시킨다.
- [x] 정체성 규칙 및 핵심 런타임·정의의 public 직렬화 필드 126개를 동적으로 열거한다. 실제 소비자가 없는 공용 effect parameter를 제거하고, 구형 mood reaction은 사유와 제거 조건이 있는 migration-only로 고정했다.
- [x] 극한형 규칙에 중복 직렬화된 전투·이동·작업·사고·피로·회복·수확·마력 배율을 제거했다. 공용 수치 권위는 `GameplayEffectBinding`만 남기고 정체성 규칙은 발동·확률·비용·상태만 소유한다.
- [x] 신화 3%와 최소 제작 기여율 60%를 우회하던 하드코딩을 제거했다. 장비·의복 완성 판정이 선택된 특성 300 규칙의 authored 값을 직접 읽으며 0%/100% 경계 검증을 통과한다.
- [x] 사선 각성 전투력의 규칙 직접 곱과 공용 effect 곱 이중 적용을 제거했다. 전투력은 공용 투영 한 번, 임계 체력·통증 무시는 정체성 상태 한 번만 적용한다.
- [x] 동일 시드의 무특성 대조군과 자연 특성군을 다시 계산하고, 6개 장비 준비 체크포인트를 자연 창립자 제작·연구 속도로 재계산했다.
- [x] 숨은 전체 저장 회귀 의존성(소유자 선행 생성, 비영속 침입 피해 대상)과 적 2종의 3손 장비 조합을 수정한 뒤 공식 Full World PlayMode 68/68/68, Console 0/0을 통과했다.

#### Phase 150 disease detailed-stat separation (completed)

- [x] Define four independent shared-effect targets: disease resistance, disease recovery speed, immunity gain, and immunity retention; keep `character:recovery-speed` limited to HP/wound healing.
- [x] Route infection probability, active-disease duration, vaccine/recovery immunity gain, and daily immunity decay through the canonical character derived-stat projection without reading trait IDs.
- [x] Persist deterministic per-infection recovery timing and immunity contracts without saving derived effect caches; preserve old population-health save compatibility.
- [x] Author the four effect-definition SOs and assign deliberate founder-trait values without changing the 100-trait count or physical BOM/WU.
- [x] Extend source-to-consumer and deterministic disease audits for neutral/positive boundaries, save round trip, and no double application.
- [x] Rebuild content, compile, run focused trait/disease audits and official 68/68/68 full-world regression using Unity MCP only; update balance evidence.

Phase 150 fixed constraints:
- Disease resistance is an infection-susceptibility divisor; higher is better and it may not alter exposure hours or environment authority.
- Disease recovery speed changes non-chronic active disease duration, never instant-cures chronic conditions, and is fixed deterministically when infection begins.
- Immunity gain scales vaccine and completed-disease immunity awards; immunity retention divides daily immunity decay. Both remain clamped to the existing 0..100 immunity domain.
- `character:recovery-speed` continues to affect only aggregate/body healing and does not silently alias any disease stat.
- No new item, BOM, WU, facility, research unlock or free medical outcome is introduced.
- All Unity asset creation, compilation, console inspection and tests use Unity MCP without mouse input.

#### Phase 151 unified character capacities and performance (in progress)

- [x] Add the 13 functional-capacity definitions, five composite performance indicators, deterministic formula contracts and contribution traces.
- [x] Build one vertical slice from anatomy/proficiency/shared effects through a live work executor and character detail UI.
- [x] Author all 31 work performance profiles with primary proficiency plus at most one 20% secondary proficiency and explicit required/bottleneck capacities.
- [x] Route combat, surgery, treatment, recovery, disease, survival and social outcomes through the single performance query.
- [x] Remove the 12 legacy `CharacterStatType` surfaces, projections, serialized authoring and direct consumers without leaving a dual authority.
- [x] Add the explicit legacy-save rejection boundary and rebuild authored ScriptableObject catalogs.
- [x] Replace symbol-presence-only connectivity checks with structural mapping plus live authoritative-state execution evidence. The focused live audit currently proves 11 formula endpoints across 12 consumers, including a forced work accident that damages and restores an anatomy node.
- [x] Run the full character-summary UI matrix and 68-section whole-world round-trip regression through Unity MCP.
- [ ] Add real daily-schedule movement/meal/sleep and policy-grounded injury/disease lost-WU to the deterministic founder/readiness calculation.

Phase 151 fixed constraints:
- Healthy baseline is `1.0 = 100%`; functional capacities have no global upper cap and reject negative or non-finite authored/projected values.
- Non-applicable capacities are excluded and weights are renormalized. Arcane conduction is applicable and visible for every character.
- Required applicable capacities below 10% fail explicitly; autonomous and direct commands receive the same physical failure reason.
- Formula data is authored in ScriptableObjects. Runtime health, proficiency and identity state remain in their existing aggregates; projected capacities and performance snapshots are never saved.
- Existing dirty-worktree changes are preserved. Unity asset generation, compile, console and tests use Unity MCP only.

Phase 151 verified evidence:
- V27 structural audit PASS: 13 capacities, 107 formulas, 30 work-speed mappings, 30 accident mappings, five composites, ten species/anatomy profiles, and uncapped 250% capacity.
- V25 authored mapping PASS: 419 buildings, all 125 operate facilities, 354 recipes, 61 equipment definitions and 56 apparel definitions. Missing facility proficiency no longer falls back from command kind.
- Legacy declaration/call/serialization/UI source audit is zero; the architecture ratchet now requires the deleted catalog to remain absent and the new Query/contracts to exist.
- V27 live/save audits pass for all 107 formula projections and V24 round trip with explicit `LegacyCharacterStatSchema` rejection.
- V27 live consumer audit PASS: 11 formulas / 12 consumers. Work accidents now damage an actual anatomy node rather than only a detached vitality surface.
- Character summary/medical UI matrix PASS at 1600x900 and 900x1600 with captured errors/warnings 0/0.
- Full-world round trip PASS: registered/captured/restored 68/68/68, baseline restored, canonical baseline matched, progression contracts true, Console warnings/errors 0/0.
- Capacity-aware 10,000-party founder audit PASS. No-reroll p10/median/p90 essential-industry throughput is 266.508 / 272.746 / 279.774 WU/day; median accident exposure is 0.324 events/day.
- Equipment-readiness audit PASS at six checkpoints. Minimum-kit supply exceeds lower-bound new-ready demand by 40.216x, 48.436x, 42.121x and 93.481x in the non-zero demand windows; completion crosses at Day 32.238, 122.478, 243.799 and 405.991.

Phase 151 remaining balance boundary:
- `밸런스 완료` is still forbidden. The present result is `밸런스 기준 배정 / 구조·연결 검증 통과` because the founder schedule still uses gross 99-WU availability and representative formulas for aggregate production regimes.
- Exact injury/disease lost WU needs an authored treatment/rest/exposure policy. The runtime now projects disease symptom work loss and anatomy damage, but inventing treatment delay or epidemic exposure cadence would create fake absolute loss numbers.

Phase 151 regression fixes discovered by the final gate:
- Detached staff restore candidates previously started AI work selection during `CharacterActor.Initialize`, before saved proficiencies were restored. `AbilityWork` now defers initial assignment for detached restore candidates; `ApplyActorState` restores authorities and requests the replan.
- The progression regression still expected the V23 compatibility string after the save root advanced to V24. It now asserts the authoritative `PreV24IncompatibilityReason` containing `LegacyCharacterStatSchema`.

#### Phase 152 14-capacity correction and end-to-end consumption (completed: structure and connection gate)

- [x] Replace `resource-efficiency` as a foundational capacity with derived performance results, and add `physical-power` plus `immune-defense` so the foundational catalog contains exactly 14 capacities.
- [x] Author real anatomy producers for both new capacities across all ten character species, with explicit N/A allowed only when the species truly does not require the function.
- [x] Rebuild composite and domain formulas so hauling, force work, melee power, disease resistance/recovery, immunity gain/retention, nutrition/resource use, temperature and fatigue consume the correct independent inputs.
- [x] Update every runtime consumer and character-detail UI row to use the canonical performance Query; remove stable-ID, enum, asset and formatter remnants of foundational `resource-efficiency`.
- [x] Extend structural, live-consumer, save/recompute and species audits to require 14/14 capacity definitions, actual producer coverage, result change, UI visibility and zero orphan consumers.
- [x] Rebuild ScriptableObject assets, compile, run focused V27 audits, UI matrix and official 68-section full-world regression through Unity MCP only.

Phase 152 completion evidence:
- V27 structural/live audits: 14 capacity definitions, 107 performance formulas, 10/10 character anatomy profiles with physical-power and immune-defense producers, zero obsolete foundational resource-efficiency references.
- Causal consumer slice: a reversible 20-point anatomy burden lowers physical power and therefore haul capacity/melee power; the same burden lowers immune defense and therefore disease resistance/immunity gain.
- Character summary/medical UI matrix: `RESULT=PASS` at 1600x900 and 900x1600, including detailed stats, proficiencies, mentorship and surgery; captured Error/Warning 0/0.
- Official full-world PlayMode round trip: `RESULT=PASS`, sections 68/68/68, baseline restored and canonical baseline matched, Error/Warning 0/0.
- Status is `밸런스 기준 배정 / 구조·연결 검증 통과`; all-species quantitative role and injury/disease comparison remains the next balance phase.

Phase 152 structure contract:
- Content authority: `CharacterFunctionalCapacityDefinitionSO`, `CharacterPerformanceFormulaDefinitionSO`, anatomy profile assets and their root catalogs remain the only authored definitions.
- Runtime authority: anatomy node health, population disease/immunity state, proficiency aggregate and selected effect sources remain the only mutable authorities. Capacity/performance snapshots are derived and never saved.
- Query authority: `ICharacterPerformanceQuery` is the only public calculation path for UI, AI, work, combat, medical and survival consumers.
- Identifier transition: remove `capacity:resource-efficiency`; add stable IDs `capacity:physical-power` and `capacity:immune-defense`. Missing, duplicate or stale IDs fail content/audit execution without fallback.
- Formula boundary: neural reaction remains a composite of mental/sensory/manipulation/mobility inputs; temperature regulation and resource efficiency remain final results derived from applicable capacities plus species/trait/equipment/environment effects.
- Save boundary: no new mutable save field is introduced. Existing V24 authority restores first, then all 14 capacities and downstream results are recomputed.
- Balance boundary: this is `밸런스 기준 배정 / 구조·연결 검증` until all-species quantitative role and injury/disease simulations are completed; no species multiplier or BOM/WU value changes in this phase.
- Failure policy: a species without a required producer, a formula without a live consumer, an obsolete resource-efficiency capacity reference, or a consumer bypassing the Query fails loudly and blocks completion.

Phase 152 errors:
- The first Unity MCP refresh command resolved `CompilationPipeline` against the dynamic command namespace and failed to find `RequestScriptCompilation`; no project code executed. Retry uses the fully qualified `UnityEditor.Compilation.CompilationPipeline` type, matching the previously documented Unity MCP ambiguity.
- A combined constants/builder/causality patch used a stale exact builder line and failed atomically; no file changed. The change is split into contracts, exact builder replacements, call site and helper insertion after re-reading stable anchors.
- A read-only PowerShell audit command used double-quoted regex text containing escaped C# quotes and failed before `rg` ran. No files changed; subsequent searches use single-quoted regex literals.
- The first focused PlayMode audit entered a scene with no active character, so its fallback fixture executed and exposed an existing stale dependency: `CharacterAiEditorTestDependencies.InjectCharacterStats` passed null for the now-required performance Query. The command stopped before the new causality slice ran. Fix the fixture to provide its deterministic performance test double, recompile, and rerun from a clean PlayMode state.
- The second focused audit reached the new causality slice and successfully damaged the selected `core`, but its cleanup assumed every anatomy uses biological `TryHealNode`; a maintenance-only construct rejected that restore. PlayMode was stopped to discard the runtime mutation. Cleanup must branch through the node's authored recovery policy and use `TryMaintainNode` for maintenance-only/replacement bodies.
- After cleanup was made state-aware, the audit exposed the real issue: `TryDamageNode(core)` returned true but the next anatomy Query showed no physical-power reduction. This indicates an anatomy synchronization path is overwriting or failing to project damage on the selected non-legacy node. Diagnose and correct the authority synchronization rather than weakening the causality assertion.
- Replacing destructive injury with the real saved node-burden command produced stable physical-power and immune-defense causality and let the focused live/consumer audit pass. Running the save audit immediately afterward failed on the consumer audit's temporary relationship-memory key (`actor:character:*`), not on capacity state. Stop PlayMode and run save validation in an isolated fresh session, then use the official full-world gate as the final persistence authority.

#### Phase 153 dungeon-species capacity allocation and upkeep closure (in progress)

- [x] Add 14 explicit capacity multipliers to each of the nine dungeon species and apply them between anatomy efficiency and performance formulas.
- [x] Remove species-wide work, research, combat, movement and accident multipliers while retaining authored social/economic effects.
- [x] Connect strong/weak work IDs to XP gain and autonomous utility without blocking direct orders.
- [x] Apply species sleep-rate multipliers exactly once and update Orc hunger/thirst costs.
- [x] Complete the Golem recharge vertical slice with a real mana-crystal reservation, 100 WU progress, AI/direct execution, cancellation safety and save round trip.
- [x] Route Golem work wear through saved anatomy burden and existing construct maintenance instead of a second performance multiplier. Integrity <=50 now exposes and consumes the existing power-core maintenance suggestion (26 WU, one lumber, 30 repair).
- [x] Rebuild content, run structural/causal/UI/save/full-world gates, then run equal-skill, natural-person, upkeep and injury/disease deterministic audits.
- [ ] Convert the existing injury/disease condition distributions into absolute treatment WU, recovery time, work-unavailable rate and death rate only after a live policy-grounded medical schedule exists; do not infer these values from static modifiers.

Phase 153 current evidence:
- PASS: structural 14 capacities / 107 formulas / 9x14 explicit species bindings; live 140 capacity projections; 11 live formula consumers; focused V3 wear/recharge save round trip; 100,000 natural-person samples; 9x10,000 damage/disease condition samples; neutral-fit 0.963~1.018; representative roles 1.075~1.115; aptitude-aware Pareto dominance 0; Golem 30-day net 0.965.
- PASS: official strict save-section regression (`68 strict save sections`).
- PASS: fresh synchronous final acceptance 33/33, final PlayMode acceptance 7/7 with 32 fresh captures, persistence restoration, and Console Warning/Error/Exception/Assert 0/0. The last live defects were a stale research-state reference after restore, a missing resonance-tuning support facility in the UI fixture, and an invalid rest-speed query in AI candidate scoring.
- OPEN: treatment-WU/death-rate conversion remains intentionally unreported until a policy-grounded live medical simulation exists. This is the remaining balance-evidence boundary, not an implementation or full-regression failure.

#### Phase 154 tavern physical recreation connection (completed)

- [x] Audit meal mood, substance mood/tolerance/addiction/overdose, D12 facility recovery and physical facility-buffer authorities.
- [x] Add an authored recreational-substance service ability to D12 and remove its free hunger/mood recovery.
- [x] Consume exactly one eligible recreational-substance stack from the D12 input buffer through the existing substance policy/runtime authority.
- [x] Apply venue fun recovery and social facility-memory/activity only after successful physical consumption; never duplicate the drink's authored mood effect.
- [x] Route automatic recreational substance use through an available D12 venue while preserving direct pickup as the no-venue fallback.
- [x] Rebuild the D12 ScriptableObject via Unity MCP and pass focused policy/stock/effect/save checks plus the full regression gate.

Phase 154 completion evidence:
- D12 is authored as `Entertainment` with `BuildingRecreationalSubstanceServiceAbility(fun=8, sentiment=0.25)` and no legacy need-recovery ability.
- Focused deterministic audit: `[Survival] tavern_recreational_substance_service: D12=entertainment; item=1->0; policy=preserved; fun=8; substance=active`.
- Fresh synchronous final acceptance passes 33/33. Final PlayMode acceptance passes 7/7 with 32 fresh captures, persistence restored, and Console Warning/Error/Exception/Assert 0/0.
- This closes the physical tavern/substance connection only. The project-wide treatment-WU/recovery/death balance evidence boundary remains open.

Phase 154 fixed constraints:
- Physical BOM is one authored beverage item per successful use. Failed policy, missing stock, cancelled movement and repeated commands must not consume or duplicate an item.
- The beverage runtime remains authoritative for mood, duration, tolerance, addiction, overdose, work speed and combat modifiers. The venue adds fun only and records a social/facility experience; it does not add free nutrition or a second beverage mood bonus.
- Tavern stock uses a facility-scoped destination ID and only that buffer is eligible. Global stored/loose stock may be requested for delivery but cannot be consumed remotely.
- D12 is an entertainment/recreation venue, not a hunger meal candidate. Ordinary food facilities continue to use the existing meal path unchanged.
- Mutable substance state and physical item stacks remain in their existing save authorities; the new building ability is immutable authored content and introduces no runtime save field.

#### Phase 155 daily time budget and technology-dependent net WU (in progress)

- [ ] Audit the live clock, need decay, sleep, meal, hygiene/toilet, recreation, movement, queueing and work-progress authorities; establish whether the current gross `99 WU/adult/day` already includes any of them.
- [ ] Produce an approval table for every neutral-adult primitive: decay, routine/emergency threshold, recovery, visit frequency, service duration, travel distance, queue capacity and non-work policy.
- [ ] Define deterministic daily-schedule policies for the no-research, early, mid and late technology checkpoints using actual authored facilities, research unlocks and interaction durations.
- [ ] Separate time availability from work efficiency: calculate clock hours spent on needs/travel/queues and apply capacity, proficiency, trait, species, equipment, facility and research multipliers only to active work.
- [ ] Charge research construction and operation as real opportunity cost before granting downstream time, movement, consumption or work-efficiency benefits.
- [ ] Produce per-person and three-founder p10/median/p90 reports for net work hours, net WU, essential-industry throughput and real playtime at each checkpoint.
- [ ] Add deterministic/live execution evidence and update the equipment-readiness crossover calculation with net rather than gross WU.

Phase 155 current design scope approved by user:
- Fix the complete chain in order: neutral healthy adult -> need rules -> facility service time -> reference travel/queue -> no-research schedule -> active work time -> net WU -> technology schedules -> species/proficiency/trait distribution -> first-party p10/median/p90.
- First deliverable is a complete authored-value proposal and calculated schedule table. Do not mutate runtime/content values until the user reviews that proposal.

Phase 155 fixed constraints:
- `24h - sleep - meals - hygiene/toilet - recreation - movement - queue/wait - illness/injury care = active-work clock time`; no category may be hidden inside both the time budget and a WU multiplier.
- Need recovery amount changes visit frequency; service speed changes visit duration; layout and facility capacity change travel/queue time. These are distinct channels.
- Work-speed technology changes WU produced per active work hour. Automation changes required worker attention or parallel capacity, not the length of the day.
- Research benefits start only after their actual research WU, prerequisite, facility and construction costs are paid. A checkpoint cannot receive a technology's benefit retroactively.
- Report both game day and estimated real playtime, and keep average days separate from shortage, injury and crisis tails.
- Existing gross 99-WU reports remain historical evidence until the daily schedule replaces them; they must not be silently relabeled as net output.

Phase 155 errors:
- The first parallel source-search wrapper assumed shell results exposed an `output` property and failed while formatting already-completed read-only results. No files changed. Subsequent searches print each returned result directly.
- A broad `rg | Select-Object` search returned exit code 1 after truncation and suppressed the two parallel line-range reads. No files changed. Continue with direct reads of the discovered `NeedBalanceCalibrationScenario` and narrow authority files instead of repeating the broad pipeline.
- A narrow FUN search included a nonexistent `Assets/Scripts/Models/Character` path, causing rg exit 1 and suppressing its parallel result. No files changed. The authoritative file was still located at `Services/Character/Core/CharacterNeedStateService.cs`; read it directly.
- A follow-up broad `fun` search matched `demolitionRefundRate` across content and exited after truncation, again suppressing the paired direct read. No files changed. Stop broad text searching; the already-proven schedule/FUN gap is sufficient to define the next implementation boundary.
- The first relevant-facility asset audit guessed three D01/D02/D04 Korean filenames incorrectly. Existing matched assets still showed the common 1.5-second use duration and capacities, but rg exited 1 and suppressed the paired AI read. Resolve exact paths with `rg --files` and then read the AI source separately.
- The first movement search repeated a nonexistent `Assets/Scripts/Models/Character` scope and suppressed its paired schedule result. No files changed. Use the known `AbilityMove.cs` directly and locate schedule declarations with a separate `rg -l` call.
- The need-definition/mood search again included the nonexistent Models/Character folder. No files changed. Restrict all remaining searches to known existing roots (`Assets/Scripts`, `Assets/Resources/SO`) and direct line ranges.
- The authored-need record search returned no match in the guessed asset/json patterns and caused the combined search call to exit 1. No files changed. Locate the configured `GameContentCatalogSO` asset first, then inspect its serialized need list directly.

Phase 153 structure contract:
- Content authority: `CharacterSpeciesSO` owns immutable capacity bindings, need multipliers and strong/weak work IDs; `GameplayEffectDefinitionSO` owns capacity target and stacking rules; authored facility capability and procedure assets own recharge/maintenance requirements.
- Runtime authority: anatomy node health/burden owns physical degradation; `CharacterSpeciesRuntime` owns charge and recharge-order state; proficiency aggregate owns XP. Capacity/performance snapshots remain derived and unsaved.
- Command authority: recharge can only begin/advance/complete through a validated recharge order that reserves a physical mana crystal and a capable facility. Arbitrary raw recharge is internal-only. Existing surgery/maintenance commands remain the only construct burden repair path.
- Query authority: `ICharacterPerformanceQuery.GetFunctionalCapacities()` applies anatomy efficiency followed by capacity-target effects and provides contribution trace. AI and XP affinity queries consume the resolved species definition and actual `WorkTypeId`.
- Identifier boundary: nine dungeon species are Slime, Orc, Vampire, Beastkin, Demon, Kobold, Myconid, Harpy and Golem. Adventurer/Human is the 100% comparator only.
- Save boundary: capacity and aptitude outputs are recomputed. Recharge order authority saves character, facility, reserved stack, accumulated WU and completion state atomically; anatomy burden continues through the existing health save section.
- Failure policy: missing/duplicate capacity binding, broad legacy species multiplier, unavailable facility/item, invalid reservation, duplicate completion or unknown saved order fails explicitly without free recharge or fallback work.
- Balance boundary: capacity values are fixed at 80-125%; upkeep can justify at most +5% raw mean. Completion requires the approved role bands, zero Pareto dominance, Golem effective WU <=105%, and deterministic multi-seed evidence.

#### Phase 156 shared quantity leases, buffer aggregation, ownership restore, and meal dispatch (implementation and Editor performance gate complete; Player authority pending)

- [x] Add the slice-based item quantity lease ledger, cached per-stack reserved totals, atomic single/batch reservation, renewal, release, revalidation, extraction and consumption APIs.
- [x] Change world-stack snapshots and every physical-item availability consumer from whole-stack owner strings to `TotalQuantity / ReservedQuantity / AvailableQuantity`, retaining the old string only as an empty compatibility field.
- [x] Add pickup-time child stack identity and quantity-conserving partial extraction; extend carried-item persistence with carried/source stack and owner-operation IDs.
- [x] Implement Meal/ProductionInput-only buffer aggregation with cohort/signature/freshness compatibility, max-stack repacking and atomic Lease Slice retargeting.
- [x] Migrate hauling, storage, conveyor, production, construction, medical, equipment, trade, waste and direct-order item consumers to quantity leases without silent fallback.
- [x] Implement authored meal quality/serving roles, 115 satiety cap, actual nutrition projection, base-mood quality choice, snack cooldown and exactly-once meal mood.
- [x] Add region-first bounded meal routing, seat-plus-food transaction, carried/buffer spoilage invalidation and begin/commit double validation.
- [x] Persist reservation claim hints on task intents, add the save mutation barrier and restore grandfather claims atomically before any priority scheduling or new AI.
- [x] Add item/buffer/task/restore diagnostics and deterministic structural, conservation, contention, save-round-trip, spoilage and path-budget audits.
- [ ] Close the dedicated performance gate: the baseline-relative Editor increment, absolute runaway guard and retained-heap soak now pass through Unity MCP. The separate Player steady-state average/p95/max measurement remains required before final performance authority is complete.
- [x] Update the balance authority and rerun Unity-MCP-only compilation, focused scenarios, PlayMode, 68/68/68 world save and Console 0/0.
- [ ] Recalculate Phase 155 technology-stage net WU from the stabilized live meal/logistics timings; this remains a separate balance task and is not part of Phase 156 structural completion.

Phase 156 fixed constraints:
- Reservation creates no physical child stack. A child exists only while an exact leased quantity is physically in transit.
- A lease owns one or more slices, never a durable raw stack ID. Aggregation may retarget or split slices without changing lease identity or total reserved quantity.
- Single-operation retries are idempotent. Batch reservation, extraction/aggregation mutation, save capture and grandfather restore are all-or-nothing.
- Only Meal and ProductionInput cohorts aggregate active leased stacks. Loose, carried, cross-facility, cross-cohort, unique, incompatible-quality/contamination/preservation/freshness stacks never merge.
- The runtime lease ledger, TTLs and indices are derived and unsaved. New saves persist exact ownership hints; restore rebinds all valid prior owners before priority competition and fails rather than substituting a same-item stack.
- Nutrition, mood and cooldown apply only after physical quantity consumption commits. Rotten/invalid food aborts fail-soft without consuming the item or leaving an actor/seat deadlock.
- Hot paths use indexed lookup and bounded queues; they may not scan all world stacks or allocate LINQ collections per candidate.
- Unity assets, editor commands, compilation, Console, PlayMode, save and profiling evidence use Unity MCP only and never mouse input.

Phase 156 implementation boundary:
- Preserve current user changes in the dirty worktree. Read overlapping diffs before every edit and keep compatibility wrappers only while live consumers are migrated.
- Phase 155 remains open and must use the new live meal/logistics timing rather than the historical gross 99-WU assumption after Phase 156 stabilizes.
- Do not report balance completion until quantity conservation, ownership restoration, food cadence/path/spoilage, memory bounds and full-world regression all pass.

Phase 156 current evidence:
- Unity MCP physical-item contracts contain 33 result rows and pass the active-ownership vertical slices: pickup retargets the original Lease to a `Carried/InTransit` physical child, compatible buffer deposit retargets the same Lease to the canonical stack, and exact consumption removes only its reserved quantity.
- The 100-owner stress contract reserves one 100-unit source without creating children, then performs 100 real pickups and deposits. `MaxStack=75` leaves exactly two physical buffer stacks, all 100 Lease quantities remain independently consumable, and completion leaves zero carried/in-transit dust stacks.
- The aggregation budget processes 64 arrivals in the current tick and defers the remaining 36 without loss. Meal routing uses the shared bounded path broker, at most eight exact candidates and a 2,048-node frame budget.
- A save captured while one quantity is carried stores origin and preferred physical stack separately. Restore recreates the same owner/stack/quantity before AI scheduling, and the restored claim remains consumable without changing the source remainder.
- Item-pile PlayMode diagnostics show available/reserved quantity, Lease/Slice count, cohort, physical/theoretical buffer stacks, pending aggregation and latest Grandfather restore statistics.
- Synchronous final acceptance passes 33/33. Official final PlayMode acceptance passes 7/7 with 32 fresh captures, strict world save `68/68/68`, baseline restoration and Console Warning/Error/Exception/Assert `0/0/0/0`.
- The first corrected-policy soak isolated a real idle hot path: healthy full-mana actors rebuilt the full 14-capacity/performance contribution graph every frame. Gating mana evaluation to blocked or depleted mana reduced the normal x1 profile from `3,881.6 KB/frame` average and `9,852.4 KB` p95 to `309.4 KB/frame` and `373.9 KB`, while the live 11-formula consumer audit still passed.
- The next soak found a periodic p95 spike because `FirstRunObjectiveRuntime` captured the collection-backed offense campaign four times per second even while an earlier onboarding milestone was unresolved. Mirroring the resolver's early-return gates reduced the official x1 Editor increment from `479.1 KB/frame` average and `2,518.0 KB` p95 to `280.0 KB/frame` and `281.2 KB`; the 512 KB / 2 MB budgets now pass without moving the thresholds.
- The final Unity MCP release soak passes reservation/facility/item invariants, frame p95 `42.81 ms`, retained Mono growth `19.40 MB`, Editor runaway average/max `1,011.6/70,669.6 KB`, save reload with 138 buildings and 3 characters, Console 0/0, and `RESULT=PASS`. A 512-combination resolver contract proves the offense snapshot gate matches every early-return combination.
- The replacement GC policy is fixed before rerun: Editor incremental average/p95 `512 KB / 2 MB`, Editor runaway average/max `16 MB / 256 MB`, Player steady average/p95/max `32 KB / 128 KB / 2 MB`, retained Mono growth `64 MB`. Release soak now captures 30 warmup + 120 paused-world baseline frames and evaluates active-minus-baseline rather than moving the old absolute threshold upward.
- The same policy now drives the common performance report: Editor reports require all 120 baseline samples and compare average/p95 deltas plus runaway guards; Player reports require absolute average/p95/max; retained Mono uses the shared 64 MB bound. Save/load and explicit bulk aggregation are excluded from steady-state samples and retain separate post-operation heap/entity-conservation gates. Unity MCP compilation, live-consumer audit, release soak and Console verification pass; a standalone Player measurement harness/run remains open.

#### Phase 157 technology-stage WU, project caps, emergency labor, alert hysteresis and population flow (in progress)

Phase 157 AI reliability continuation (2026-08-13):
- [x] Re-establish a clean Git/Unity MCP baseline and run the 12 focused AI/facility/survival scenario groups with Console 0/0.
- [x] Preserve exact execution failure authority through decision result, blackboard, bounded diagnostics and long-run report. Each actor now owns an allocation-free 32-event typed lifecycle ring; five-day reports format it only at capture time.
- [ ] Add destructive/stale cross-scenarios for facility destruction, route invalidation, quantity lease loss, spoilage, queue cancellation and actor lifecycle change while an action is running.
- [ ] Run natural-time multi-seed five-day routines and the 500-NPC scheduler profile without bulk `EditorApplication.Step()` artifacts. Current-code five-day seeds `157181`, `157182`, and `157183` now all pass with zero invariant anomalies/harmful stalls and exact safe-drink terminal conservation; the 100/500 stress and long soak gates remain open.
- [ ] Prove save/restore continuation and final Console Warning/Error 0/0 before declaring the AI reliability boundary complete.
  - 2026-08-13 fault-injection continuation: the live Brain -> BehaviorTree -> AIAction -> AbilityMove/AbilityShopping matrix now passes `47/47`, including topology replan, typed no-path terminal/release/replan, and facility destruction during approach/queue/interaction with alternate replanning and Console `0/0`. Lifecycle and mid-action save/load runners compile, but Unity currently refuses to attach these two newly added Editor MonoBehaviour types; do not claim those matrices passed until the harness binding is resolved.
  - 2026-08-13 concurrency/fairness checkpoint: retail stock commits are now atomic after delay and the two-buyer/one-unit regression passes. The scheduler's authored 16-decision ceiling is preserved while a bounded live-backlog floor prevents starvation; the clean 500-NPC run reports starvation `0`, max deferral `1.083s`, scheduler p95 `2.498ms`, and scheduler-owned GC `0 B/frame`. Whole Editor frame p95 is still `19.07ms`, so performance and the wider reliability boundary remain open.
  - 2026-08-13 final reliability evidence: the four-row Downed/Despawned/Disabled/Destroyed matrix passes action, movement, facility ownership, item Lease and emergency-ledger cleanup exactly once with late commits `0`. Mid-action save/load passes actor replacement, transient action/path discard, lifetime retirement, replan invariants and Console Error `0`. The 500-actor profile passes `500/500` typed progress, starvation/invariant/orphan/failure-loop `0`, max deferral `0.784s`, frame p95 `9.73ms`, scheduler p95 `1.91ms`, scheduler GC `0 B/frame`. Five-day seeds `157181/157183` pass; `157182` remains red only for the separate hygiene cadence gate `0.533 < 0.6`, with AI reliability gates green.

- [x] Define the authoritative 180-second day, 99-WU neutral baseline, actual/output-equivalent/realized-growth/guaranteed-growth channels and technology ROI contracts without double-counting time and throughput. All live character work loops now feed approved work through one accounting gate, and automatic production records only the bill progress physically accepted by the production authority. Physical loss producers remain open.
- [x] Add project worker caps and the approved deterministic contribution curves for facilities, landmarks and 1/2/4-person research; expose marginal completion-time evidence. Research and grand-project execution are connected. Ordinary facilities author Small/Medium/Industrial 2/3/4 caps, parallel reservations, automatic slot filling, diminished project contribution with raw-labor accounting and marginal UI. The focused Unity MCP PlayMode vertical consumed two units from a physical lumber stack (4 -> 2), delivered the exact construction BOM, joined three live founders to an Industrial project, and accepted exactly 2.60 WU from the 1.00/0.85/0.75 curve with maximum/automatic cap 4; `Artifacts/QA/construction-project-playmode-report.txt` passes with captured Warning/Error 0/0.
- [x] Assign one validated `EmergencyWorkFlags` contract to all 31 work types. Stricter facility/recipe/stage reclassification is exposed by the runtime API and remains to be authored at individual stage consumers.
- [x] Implement fixed-point milli-WU emergency accounting with idempotent register/progress/reclassify/remove, O(1) snapshots and deterministic ground-truth reconciliation; connect it to actual approved work progress and every central run termination path.
- [x] Add incident-ID aggregation, desired/committed threat levels, immediate escalation, two-game-hour staged downgrade hysteresis, alert epochs and reserve-coverage Schmitt thresholds.
- [ ] Preserve suspended work progress, material leases and reservations through alert transitions; throttle return planning to four characters per tick and prevent repeated context switches in one epoch. Work-order, persistent, captivity and repair-domain progress now suspend at safe checkpoints and resume by saved work/target authority; local one-shot loops still finish rather than suspend, and explicit item-Lease identity evidence remains open.
- [x] Rebuild accounting before saves/restores and reconcile at day end, capture, restore and qualified Red escalation; reject invalid ground truth rather than silently repairing it.
- [x] Add non-mutating disaster shadow simulations and the Day-120 six-worker essential-maintenance regression.
- [x] Remove population-count progress gates, introduce per-capita 30-day productivity conditions and connect relationship/capacity-based settlement intent without target-population correction.
- [x] Add labor, alert, reserve, project marginal-efficiency and reconciliation diagnostics to runtime UI. Unity visual observation remains part of the final MCP gate.
- [ ] Recalculate no-research/early/mid/late net WU and first-founder p10/median/p90 from live schedule/logistics, then run deterministic multi-seed, save, performance and official world regressions through Unity MCP. The deterministic authority pass now covers six technology stages and natural/compromise/upper founder p10/median/p90 in `Artifacts/QA/phase157-technology-founder-wu.md`; remaining closure is the live need/facility/queue/travel/work trace plus multi-seed, save, performance and official world regressions.
  - Live trace continuation (2026-08-12): all three long-running work execution loops now apply need depletion using elapsed game time and can yield to hunger, thirst, sleep, excretion and hygiene. Physical water consumption reached 3/3 founders in repeated MCP runs. Shopping completion now requests an immediate guarded replan instead of leaving an ended action resident.
  - AI forward-progress continuation: the adaptive scheduler was observed at decision budget 0 with 73 starved decisions for only three actors. It now guarantees at least one decision and one path-search slot per frame; the next trace reduced starved decisions to 1 and produced live construction activity for all three founders.
  - Five-day live trace gate (2026-08-12): the verifier now warms up for 130 game seconds and measures 900 game seconds / five complete day rollovers. It derives meal cadence from the selected food's actual nutrition, observes physical meal and water events, accumulates central labor across daily resets, and compares physical construction progress against output-equivalent rather than raw labor. The final Unity MCP run passes with 17 meals, 14 water events, `1.133/0.933` visits per actor-day, `73.419` actual/output WU, physical construction delta `73.418`, Console Warning/Error `0/0`, and `RESULT=PASS`.
  - Still open: the live founders produced only `4.895 actual WU / actor-day`, far below the authored neutral target of 99 WU. The five-day activity trace attributes 510-599 of 900 seconds per actor to idle/other, 79-162 seconds to active work, and unexpectedly long meal/drink action windows. Before technology-stage or founder-distribution balance is accepted, separate true AI idle, facility session time, need-action travel/queue and character performance coefficients, then rerun the same five-day gate.

Phase 157 fixed constraints:
- Work time and work performance remain separate channels. Need, travel, queue and interruption time may not also appear as a throughput multiplier.
- Reserve workers perform ordinary interruptible work in peace. Planning uses guaranteed growth WU; completed production uses realized growth WU.
- Automation output remains domain-bound and is credited only after physical inputs, fuel, hauling, maintenance, failures and spoilage.
- Runtime population never receives hidden correction toward checkpoint medians. Technology and milestones use per-capita productivity/service/coverage authorities, not headcount.
- Emergency hot paths are allocation-free incremental lookups after warm-up. Only day-end, capture, restore, qualified Red escalation and explicit developer audit may perform O(n) reconciliation.
- Threat alert and reserve coverage are independent state machines. Reserve vulnerability never causes weapon changes.
- Escalation is immediate; Red->Amber and Amber->Green each require two game hours of resolved conditions. Alert state, epoch, active incidents and suspended work survive save/restore.
- Balance completion remains forbidden until live consumers, UI observation, deterministic shadow scenarios, performance budgets, save round trip, Console 0/0 and official 68/68/68 all pass through Unity MCP.

Phase 157 recovery note:
- `planning-with-files` session catchup found six unsynced messages but its console print failed under Windows CP949 on an em dash (`UnicodeEncodeError`). No project file was changed by the script. Current `task_plan.md`, `findings.md`, `progress.md` and git diff remain the recovery authority.
- New bottom-up gate: before treating the neutral-facility five-day result as a starting balance, audit and then simulate a true new run with only the actual starter shell and starter supplies. Measure time-to-first food/water/sleep/toilet/hygiene/recreation service, construction BOM/WU feasibility and death/soft-lock conditions.
- Static preflight status: failed. The actual category-based grant supplies vinegar/restraints instead of edible rations/construction materials, the fallback shell has no survival facility, the three-person minimum service set costs about 924 WU plus unavailable exact materials, and onboarding prioritizes room/research rather than survival. Do not run or interpret the true-start five-day balance sample until the loadout authority and primitive survival transition are explicitly redesigned and implemented.
- True-start continuation (2026-08-12): the starter grant is now explicit (`24` preserved rations, `30` clean water, lumber `15`, cloth `9`, candles `10`, resin balm `5`, sewing supplies and underwear), vinegar is no longer accepted as emergency food, and primitive field-meal/floor-rest/latrine/bucket-wash actions exist. A five-day Unity run proves physical starter-stack conservation after fixing carried-to-warehouse identity transfer, but the primitive AI gate is still open: normal eating uses a later warehouse/meal candidate, toilet/hygiene are not selected before their needs collapse, and the existing verifier's fixed primitive-count assertions do not match authored need cadence.
- [x] Make routine survival needs preempt or suspend ordinary work at the authored thresholds and guarantee emergency forward progress for hunger, sleep, excretion and hygiene, not thirst alone.
- [x] Separate a natural five-day starter survival gate from focused primitive fallback proofs. Natural evidence measures deaths, breakdowns, physical food/water conservation and actual need outcomes; focused evidence proves each primitive command, event, cost and recovery without a starter service foundation.
- [x] Prevent coarse/stale facility-role presence from suppressing fallback actions. Primitive actions remain eligible from their own physical preconditions, and emergency tie ordering selects the primitive action before an unresolved facility action.
- [x] Re-run the starter survival gates through Unity MCP with Console Warning/Error 0/0.
- [ ] Resume Phase 157 downstream technology-stage net-WU recalculation from the now-proven true-start survival cadence; Phase 157 remains open until that trace and the broader official gates pass.
  - 2026-08-13 robustness checkpoint: true-start five-day survival now passes at `100/100/100` HP with physical ration/water conservation and no damage. The 100-NPC deterministic stress scenario passes after repairing authored phenotype and movement-guard fixture dependencies. The 500-NPC PlayMode profile is behavior-valid and allocation-free after warm-up, but remains CPU-invalid (latest detailed scheduler p95 about 128 ms), so AI/performance closure remains open.
- [ ] Correct the neutral-facility daily-routine trace before downstream recalculation: recreation currently completes `0.200/actor-day` and actual labor is `12.811 WU/actor-day`. The first confirmed defect is `LeisureVisit` receiving Idle rather than Leisure group priority; compact-layout travel, idle decomposition, food/water commit consistency and peak construction workforce evidence remain after that fix.
  - 2026-08-12 recovery evidence: exact three-founder isolation, distinct starting cells, physical food/water event conservation, and live industrial workforce contribution now work. The best stable five-day run reached meals/drinks/toilet `1.0/0.8/0.6` per actor-day, peak workers `3` / effective `2.6`, and `19.882 actual WU/actor-day` with Console `0/0`; hygiene `0.467` and recreation `0.533` still miss the cadence gate, while need/other travel remain excessive.
  - Shared meal authority now reads hunger routine/emergency thresholds from `ICharacterNeedBalanceRuntime`, and the authored asset uses approved `50/20`; the prior independent physical-consumption threshold is removed.
  - A stronger global facility travel penalty was tested and rejected because it caused cadence regression, two deprivation breakdowns, and console errors. Source/asset are restored to the last stable `4 free cells / 0.015 per cell / 0.35 max`. Next implementation must prefer a nearby roughly-equivalent candidate without making distant valid facilities unavailable.
  - Do not resume downstream technology/founder WU calibration until the neutral trace reaches stable cadence without breakdown, central output matches the physical project, and work/need/other-travel/idle decomposition is credible.

Phase 157 current errors:
- Unity compile attempt 1 after adding the workforce gate failed with `CS0117` because `ProjectScale.Industrial` is not a canonical enum member. Resolve against the existing enum rather than adding an alias or fallback.
- Unity Console once appeared clean while `Editor.log` held `CS0019` (`CharacterId == CharacterId`); use `.Equals` and require both editor compile state plus compiler-log evidence.
- Instrumented five-day attempt 1 allowed narrative-only local-LLM mood requests, produced repeated request timeouts, and Unity MCP approval was revoked mid-run. Suspend `AiDirectorRuntime` and its request queue only inside the deterministic WU fixture, then rerun after MCP reconnects.
# 2026-08-12 measured WU baseline correction

- [x] Treat `99 WU/adult-day` as the historical 100-second schedule envelope, not live output authority.
- [x] Use the clean five-day neutral-facility observation (`19.882 WU/adult-day`) as the provisional measured baseline and round the authored audit baseline to `20 WU/adult-day`.
- [x] Rescale technology checkpoints from `99→198` to `20→40` while preserving the `1.00→2.00` output-equivalent progression index.
- [x] Migrate direct fixed-99 consumers in settlement labor, faction contracts, research-day reporting, founder distribution reporting, population simulation, and equipment-readiness reporting.
- [x] Compile and run `PHASE157_EMERGENCY_LABOR` through Unity MCP: `liveBaseline=20WU`, `endless=2.00x`, Console Warning/Error `0/0`.
- [ ] Repair the still-failing five-day routine trace (meal/drink/toilet/recreation cadence and occasional deprivation breakdown) before promoting 20 WU from provisional live calibration to final multi-seed balance.
- [ ] Recalculate every legacy baseline record still documenting 99-WU results; do not silently relabel old numeric outputs as 20-WU evidence.
# 2026-08-12 AI intent arbitration consolidation

- [ ] Inventory every authority that can interrupt, begin, cancel, or resume worker actions.
- [ ] Define one explicit worker-intent state machine and deterministic transition table.
- [ ] Route routine needs, emergency needs, direct orders, protected actions, and work resume through the single arbiter.
- [ ] Make facility/item reservation ownership follow the active intent epoch; stale completions must not mutate current AI state.
- [ ] Add focused conflict regressions: simultaneous needs, reservation failure, emergency escalation, service completion, stale coroutine completion, and work resume.
- [ ] Compile and run focused Unity MCP scenarios, then repeat the isolated five-day trace.
- [ ] Accept the 20-WU baseline only when multi-day needs remain sustainable without breakdown or action thrashing.

## AI arbitration contract

- Authority: exactly one `CharacterActionIntent` per worker, owned by the worker AI arbiter.
- States: `Idle`, `Work`, `RoutineNeed`, `EmergencyNeed`, `ProtectedAction`, `DirectOrder`, `Breakdown`.
- Transition order: physical impossibility/death → breakdown → protected action → direct order → emergency need → current safe checkpoint → routine need → work/idle.
- A request does not directly stop an action. It submits a typed intent request; the arbiter either rejects, defers, or commits one transition with a monotonic epoch.
- Movement, facility use, item leases, and work suspension carry the committed epoch. Completion from an older epoch is ignored and cleans up only its own reservations.
- Routine service stores one suspended work intent and resumes it only after the same committed routine intent succeeds. Failure re-enters arbitration; it does not silently choose another service.
- Balance status remains `밸런스 기준 배정` until the five-day live trace is sustainable.
# Current AI stabilization continuation (2026-08-13)

- [x] Diagnose seed 157181 five-day failure without relaxing causal tolerance.
- [x] Fix labor/project precision drift across interrupted work operations.
- [x] Fix work-target destruction during coroutine finalization.
- [x] Preserve typed meal-commit failure details through visitor adapter.
- [ ] Run additional five-day seeds and correct any scenario-specific failure.
- [ ] Re-run 500-NPC fairness/frame/GC evidence.
- [ ] Run save/restore, console, and integrated AI regression matrix.

## 2026-08-15 continuation errors encountered

- A broad `rg` command included nonexistent `Assets/Scripts/Services/Alert` and `Assets/Scripts/Services/Defense` paths, so the batch exited 1 after returning the useful Character matches. Resolved by querying only existing files/directories.
- The first emergency-work gate compile failed with `CS0177` because a short-circuited exact-work rejection could leave `out WorkTargetCandidate candidate` unassigned. Initialized it to `default`, recompiled successfully, and confirmed Unity Console Error/Warning 0/0 before rerunning PlayMode.
# AI scenario verification status (2026-08-14)

- [x] Run three deterministic five-day routine seeds.
- [x] Run live alert suspend/hysteresis/resume integration.
- [x] Run 100-actor synchronous stress.
- [x] Run 500-actor fairness/frame/GC profile with typed queue liveness.
- [x] Run lifecycle, route/facility destruction, and mid-action save/load matrices.
- [ ] Maintain the scenario manifest as new actions, facilities, items, and incidents are added; no finite matrix proves all future states.
- [ ] Close the remaining physical-logistics cross-domain failures: equipment repair instance/destination continuity, repeated repair hauling, and expedition package visibility. The orphan-Lease save defect and fixture concurrency are fixed; the corrected matrix still has 7 failures.

## 2026-08-16 current continuation: structural AI boundary stabilization

- [x] Close the construction haul save/restore authority slice: canonical per-plan intent, exact live destination validation, inert participant-225 binding, Brain-owned single resume, tamper rollback, repeated-restore conservation.
- [ ] Replace non-construction FacilityBuffer same-cell fallback with an exact producer-owned destination claim authority, including restore-candidate ordering and orphan hauling-lease rejection.
- [ ] Re-run the focused construction and full physical-logistics suites after the haul save/restore slice passes.
- [ ] Freeze coverage-critical source, regenerate every stale AI scenario artifact, then run Daily seeds 157181/157182/157183 and the final source-derived manifest.
- [ ] Accept completion only with current-source `uncovered=0`, fresh artifacts, and Unity Console Warning/Error `0/0`.

Current gate: the save/load suite is fresh PASS with Console 0/0. The next production change is the non-construction destination claim authority; after that source freezes, rerun focused construction/full physical logistics before regenerating the broad AI matrix.

## 2026-08-16 Physical Logistics fresh failure triage

- [x] Classify the six fresh failures into independent roots rather than treating cascades as separate defects.
- [x] Confirm the repair root: `equipment-repair:*` has no exact claim and same-cell warehouse/maintenance-facility inference is ambiguous.
- [x] Confirm the expedition cancel fixture calls `ReturnSupplies` after a failed two-ration commit with only one ration remaining.
- [ ] Complete and compile the equipment-repair LiveFacility claim vertical slice.
- [ ] Make explicit-package returns fail closed for unknown/already-returned IDs and prove duplicate return cannot mint supplies.
- [ ] Rerun full Physical Logistics and then mid-action SaveLoad from rebuilt assemblies.

Current evidence: expedition hauling, repeated readiness polling, consumption and claim revoke already PASS. The remaining repair five-row cascade and cancel conservation row require the two focused corrections above.

Error log: the first dynamic compile command resolved `CompilationPipeline` as `Unity.CompilationPipeline` inside the tool-generated namespace and failed CS0234. Retry with the fully qualified `global::UnityEditor.Compilation.CompilationPipeline` symbol.

## 2026-08-16 research archive LiveBuilding continuation

- [x] Correct Q03 archive ownership from `LiveFacility` to append-only `LiveBuilding`; Q03 is an authored building with archive capability but intentionally has no visitor-style `FacilityData`.
- [x] Compile the runtime/editor assemblies and run the official FirstRun production path through Brain -> AIHaul -> Q03 archive with exact claim and Console 0/0.
- [x] Compile and run the focused detached-candidate restore/participant-220 publication/late-participant rollback regression.
- [ ] Re-run mid-action save/load and full physical logistics from the frozen research/destination source boundary.
- [ ] Regenerate remaining stale AI scenario artifacts, all three DailyRoutine seeds, and the final coverage manifest.

Current evidence: `Temp/first-run-objective-report.txt` is a current-source `FIRST_RUN_OBJECTIVE PASS`; Q03 claim, haul plan, Brain-owned AIHaul, FacilityBuffer terminal and cleanup all pass. The newly added focused EditMode regression uses the real authored Q03 inside a detached eligible research room, proves exact one-claim publication, and injects a later participant publish failure to require restoration of the previous claim image.

Current errors:
- The first recovery-scene dynamic command failed because nested PowerShell/C# quoting corrupted the command text. No project source changed. A placeholder-substitution retry created `Assets/_Recovery/Codex-FirstRun-20260816-065248.unity` successfully.
- One exploratory PowerShell command referenced an accidental nonexistent `AssetDatabaseNotAvailable` token. It changed no files and was not repeated.
- The first focused-regression static pass used a nonexistent `IDungeonRuntimeAggregateRootStore` name. The actual authority is the concrete `DungeonRuntimeAggregateRootStore`; the signature was corrected before Unity compilation.
- The first focused execution exposed a fixture dependency error (`Facility warehouse requires IStockQuery`) because the actual Q03 runtime archetype includes storage. The fixture now uses the existing full editor building injection, and construction is exception-safe. The next run cleaned the two leaked exact-ID candidates and passed all ResearchTree scenarios.
- The first MCP call immediately after domain reload found an empty tool list. Waiting for the bridge handshake before the next call resolved the transient condition.

## 2026-08-16 current-source SaveLoad continuation

- Fresh `DungeonAiActionSaveLoadPlayModeVerifier.RequestRun()` report at `2026-08-15T22:19:31.9894712Z` is newer than the final research/archive and haul-destination source changes and reports `result=PASS; failures=0`.
- The run proves five tampered destination/authority variants reject the whole restore atomically, two untouched restores bind the committed haul intent before AI wake, each resumes through Brain -> AIHaul exactly once, and both preserve the exact physical quantity with no unexpected Error/Exception/Assert logs.
- Three launch-diagnostic errors were non-project dynamic-command mistakes: `Unity_GetEditorState` was unavailable, `Unity_ManageEditor get_state` was invalid, and two Console-clear command attempts failed first on the ambiguous `Editor` name and then on the MCP namespace allow-list. Project source did not change; the verifier was launched by the direct public entrypoint without repeating those commands.
- One planning append used a stale cross-file anchor and was rejected atomically. The retry used each file's actual tail and appended this dated section.
- Next authority is a fresh full `PhysicalItemLogisticsPlayModeVerifier.RequestRunFromMenu()` on the same compiled source.

## 2026-08-16 current-source Physical Logistics acceptance

- Fresh `Artifacts/QA/physical-item-logistics-playmode-report.txt` at `2026-08-15T22:27:38.8225624Z` reports `RESULT=PASS; failures=0`.
- Production-live warehouse, facility buffer, construction commitment, equipment repair LiveFacility claim/preflight/delivery/revoke/salvage, and expedition ReservedTarget repeated-poll/consume/revoke/cancel/duplicate-return conservation all pass on the final archive/haul authority source.
- The request flag was consumed, PlayMode returned to EditMode automatically, and no FAIL row exists in the report.
- Next: recapture the nonthrow coverage manifest to derive the exact remaining current-source stale/missing suites before running them.

## 2026-08-16 manifest narrowing and Surgery acceptance

- The first nonthrow manifest MCP call completed in Unity but the helper missed response id 2 because the full report was emitted as one very large execution log. A retry logged only a compact completion message; the report itself remained the durable authority.
- Corrected the coverage audit's false `unexpected` rows by giving `OffenseJourneyPlayModeFacade` its exact expanded physical-supply dependency set while Strategic/Tactical retain the four battle dependencies. Unity rebuilt the Editor assembly after the change with no recent compiler errors.
- Current manifest narrowed from `uncovered=16` to `uncovered=9`: Daily three-seed Eat/Hygiene/Rest, Surgery, and three Offense domains only.
- Fresh Surgery report at `2026-08-15T22:36:21.8328221Z` reports `RESULT=PASS; failures=0`; exact claim, AIHaul material flow, no duplicate request, clinical work/terminal, completion revoke, cancel conservation and cancel revoke all pass.
- Next: Offense Tactical, Journey, and Strategic visual evidence, then Daily seeds 157181/157182/157183.

## 2026-08-16 final current-source AI acceptance

- [x] Compile and run the focused Offense turn-battle and Strategic scenario suites after introducing explicit persisted command action types.
- [x] Run the official pointer-driven Offense Journey and require real command/effect progress, terminal return, reward exact-once, ownership cleanup, and Console cleanliness.
- [x] Open the production world map through the Expedition tab and `P1Action_OffenseOpenMap`, then capture both strategic UI resolutions.
- [x] Run DailyRoutineWu seeds 157181, 157182, and 157183 as independent PlayMode sessions with five-day/gate-v3/current-source evidence.
- [x] Refresh the stale Offense Tactical evidence created before the strategic battle source change.
- [x] Recompute the source-derived manifest and require `uncovered=0` plus final Unity Console Warning/Error `0/0`.

Final gate: `result=PASS; authored=19; runtimeActions=22; deprivationLogical=5; workTypes=31; domains=16; uncovered=0`. No coverage-critical source was changed after the accepted runs.

## 2026-08-17 V27 combat table integration

- [x] Recompute and confirm all 36 combat encounters with fresh 1,000-seed checkpoints (`36/36 PASS`).
- [x] Move the accepted seven-axis values into one deterministic calibration authority and consume it from the builder, aggregate verifier, V27 ledger, and approved asset application.

Current errors:
- Two planning-file append attempts used stale or cross-file anchors; `apply_patch` rejected both patches atomically and no file changed. This retry uses the verified file tails.
- The first combat approval generation failed before writing approvals because legacy encounter YAML omits newly added default-valued multiplier scalars. The generic digest correctly required an existing scalar; combat now uses a dedicated source digest that canonicalizes all seven mutable combat fields while allowing a legacy default field to be absent before the first approved reserialization.
- The first applied-asset verification command omitted the `DungeonStory.Foundation` namespace and failed to compile; no project state changed. The corrected command then exposed that placeholder substitution still changed the digest when an omitted legacy field became a newly serialized line. Combat approval digests now remove all seven mutable scalar lines entirely, making pre/post-serialization representations identical while retaining every non-balance YAML byte as source authority.
- The first 36-encounter aggregate re-verification exceeded the Unity MCP 300-second call ceiling after progressing into the early rows. No aggregate PASS was written. The exact same 1,000-seed verifier will run in bounded six-encounter batches; only all six successful batches may produce the aggregate evidence.
- A six-encounter retry also hit the client ceiling because the original aggregate continued running inside Unity after the MCP call timed out, so the retry queued behind it rather than measuring a six-row runtime. No further Unity command will be queued until the in-editor aggregate writes its durable completion artifact or the Editor returns idle.
- The first fast aggregate finalizer compared the expected parameter row to report line 5 in one-based terms but indexed `lines[4]`, which is the objective row. It marked all 36 as failed and overwrote only the aggregate summary; the 36 individual PASS reports were untouched. The zero-based index is corrected to `lines[5]` before regenerating aggregate evidence.
- The first dynamic producer/consumer validation command embedded a newline escape that the Unity command normalizer mangled into invalid C#. No project source changed. The retry uses a plain `" | "` separator.
- Current-source 33-step final acceptance failed in three independent boundaries: runtime Unity-object lifetime policy (`FacilityCandidateScorer` reads `.name`), PhysicalItem facility delivery fixture lacks the new exact destination authority, and ImplementedScenario has Customer AI plus Staff duty failures. The other 30 implemented suites and the remaining acceptance steps passed; these failures must be classified and fixed before V27 completion.

## 2026-08-17 V27 whole-game coverage and final-acceptance blockers

- [x] Complete deterministic whole-game ledger coverage: 84,143 rows across 12 domains, 413 live item definitions, 354 authored recipes, 356 active buildings, and zero producer/consumer orphans.
- [x] Pass 256-seed economy audit, asymmetric mEWU/SCC contracts, RFC 4180 allocation/performance gates, approved asset application, no-op reapplication, and 36x1,000 combat encounter verification.
- [ ] Resolve eight current-source FinalAcceptance failures without weakening production authority or ratchets.
- [ ] Rerun V27 audit after the final source freeze and require `critical=0`, `integrity=0`, `differing=0`.
- [ ] Run the final PlayMode/freshness/Console gates, then commit, push, open the PR, obtain green review/CI, and merge to main.

Current FinalAcceptance failures: V19 runtime-authority validator (six stale-or-real boundaries), architecture metrics ratchet, Batch B survival fixtures (meal four-second commit and recreational beverage authority), PhysicalItem facility-buffer fixture authority, and ImplementedScenario Customer AI/Staff-duty rows. The durable report passed 25/33 steps, so these are treated as independent blockers rather than hidden by the aggregate.

Current errors:
- `Tools/ArchitectureMetrics/Run-ArchitectureMetrics.ps1 -Verify` regenerated the current report and failed because `OversizedType` increased from baseline 1 to current 5. The five types are pre-existing gameplay/AI runtimes, not the new V27 ledger. Their working-tree line increases are being attributed before choosing extraction versus an intentional reviewed ratchet update.
- One compact inspection attempted fields that do not exist because `oversizedTypes` is a string array, not an object array; the retry serialized the entries directly.
- One architecture diff command emitted an oversized/truncated report because the generated JSON contains large source arrays. Subsequent inspection uses parsed, bounded fields only.
- Unity MCP editor/console calls returned `Connection revoked`; no project state changed. Work continues through filesystem, static analyzers, and the checked-in test runners until the editor bridge is available for final PlayMode evidence.
- The installed `dotnet` host has no SDK, and Visual Studio MSBuild cannot resolve `Microsoft.NET.Sdk`; the generated Unity csproj therefore cannot be built from that host. This path is not repeated. Unity's bundled Roslyn compiler and the Editor's normal compilation remain the authoritative compile gates.

## 2026-08-17 focused FinalAcceptance repair

- [x] Replace newline-sensitive V19 source-contract probes with semantic single-token checks that survive LF/CRLF serialization.
- [x] Give the physical FacilityBuffer fixture a real walkable Grid authority while retaining its exact ReservedTarget claim.
- [x] Make the meal action fixture publish an Active lifecycle before starting and restoring its four-second action.
- [x] Re-run the focused authority/physical/survival/customer/staff suites: all gameplay rows PASS; only the stale architecture report remained.
- [x] Regenerate and verify the reviewed architecture baseline (`files=1570`, `types=5227`, `hardOversizedTypes=5`, no content escapes or direct session mutations).
- [ ] Re-run focused runtime authority against the refreshed architecture report, then run the full 33-step FinalAcceptance suite.

Current errors:
- The first focused poll used a hard-coded UTC comparison and timed out while the suite was still running. The durable report completed at `2026-08-17T02:58:51Z`; subsequent polling reads the file timestamp directly.
- Error log: a bounded AI coverage manifest inspection initially emitted too many long evidence rows and the tool output was truncated. Retried with exact result/count extraction only; current manifest is `result=FAIL; uncovered=71` because the committed CharacterAI external behavior source is newer than most durable suite artifacts. This is recorded as stale evidence, not misreported as a V27 economic failure or a fresh behavioral regression.
- Error log: the first vertical-slice poll reached its client timeout immediately before the durable report landed; direct artifact verification subsequently confirmed a fresh `RESULT=PASS; checks=11; failures=0` report.
- Error log: the first seed-157181 poll compared DateTime values with mismatched Kind metadata and timed out despite a completed report. The durable report was inspected directly, and seeds 157182/157183 used captured pre-run mtimes instead.
- Error log: one PowerShell evidence query used a double-quoted alternation expression and produced a ParserError. It performed no write; the query was rerun with single-quoted patterns.
- [x] Re-run the current-source 5-day compound evidence for seeds 157181/157182/157183 and record actual/effective WU statistics.
- [x] Run the V27 final evidence-bound audit twice and confirm byte-identical deterministic artifacts.
- [x] Run FinalAcceptance, the 256-seed economy audit, vertical-slice PlayMode, analyzer DSB001-DSB008 tests, YAML rollback/no-op, architecture metrics, and final Console 0/0.
- [ ] Keep the AI coverage manifest's 71 stale/ContractOnly scopes explicit; do not conflate them with the V27 ledger acceptance result or claim a fresh all-AI coverage sweep.
- Error log: GitHub had no Actions workflows and no protected-main rule. The only direct collaborator is the PR author, so a GitHub self-approval is impossible. Added a portable hosted-runner integrity gate rather than silently treating local evidence as remote CI.
- Error log: the first hosted V27 integrity run failed because three manifest-hashed text artifacts were generated as CRLF on Windows but checked out as LF on Linux. Added exact path-level `eol=crlf` attributes; no economic value or evidence content changed.
# 2026-08-18 V27 서비스 연속성·공간·Clutter·RNG 통합 완료 감사 (in progress)

- [x] 사용자 첨부 최종 계획을 다시 읽고 완료 조건을 1:1 복구한다.
- [x] Unity MCP 연결을 읽기 전용 상태 조회로 재확인하고 Computer Use 우회를 중단한다.
- [x] actor별 decision/movement RNG, key-addressed 외생 사건, named isolation 회귀를 구현·정적 검증한다.
- [x] 1/3/6/12/18/24명 포트폴리오와 실제 에셋 기반 256-seed 공간 Solver를 구현한다.
- [x] 공유 접근칸 합집합, 유일 접근칸 충돌, fixed-world-feature, 30% headroom, redundancy capital 지표를 원장에 연결한다.
- [x] 연구 확장 런타임의 27→49→65→81, 좌표 보존, 필수 시설 무철거 PlayMode 증거를 만든다.
- [x] 인구별 물리 수용력 PlayMode를 fresh PASS로 갱신한다.
- [x] 계획서 권위인 `research:mining:quarry/stonecutting/deep`와 현재 별도 `research:dungeon-expansion:*` 구현을 하나의 실제 연구 경로로 정합화한다.
- [x] 6인 서비스 장애/N+1 PlayMode의 `PRIMARY_AUTHORITY_PRESENT` 실패를 진단하고, 정확히 한 주 시설 장애와 실제 primitive fallback을 증명하도록 수정한다.
- [x] 6인 음식·물 폐쇄 루프와 모든 생존 서비스의 1일 장애·복구·비복제·비지배전략 계약을 fresh PASS로 갱신한다.
- [x] 32-seed 4-arm paired clutter run을 실행하고 필요 시 64 seed로 확대하여 median/p95 <10%, causal-cone 밖 RNG divergence 0을 증명한다.
- [x] 전수 원장, 256-seed, expansion, service continuity, SCC/economy, Analyzer DSB001–DSB008, RFC4180, YAML/no-op, 3-seed 실전을 현재 source revision으로 재생성한다.
- [x] 요구사항별 증거 감사에서 누락 0, unresolved Critical 0, 두 번째 실행 diff 0, Console Warning/Error 0/0을 확인한 뒤에만 완료 처리한다.

Current authoritative evidence:
- V27 audit `RESULT=PASS; ledgerRows=84287; integratedRows=222; stageWidths=6; critical=0; integrity=0`.
- Static and asset-backed layout solver `1536/1536 PASS`; minimum actual-asset headroom `30.7%`.
- Population-stage PlayMode `RESULT=PASS` for 1/3/6/12/18/24 with gross/net/recurring/growth/emergency and fixed-world-feature exact markers.
- Expansion PlayMode `RESULT=PASS; failures=0; liveResearchCompletions=3; publications=3`, including 27→49→65→81 and no-demolition marker.
- Expansion EditMode is fresh after the authority correction: the existing quarry/stonecutting/deep projects are the only gates, direct deep completion resolves 81 columns, save research/layout authority is exact, and the E-key remains developer-only.
- Expansion PlayMode is fresh after the authority correction at `2026-08-18T13:43:47Z`: all three live research completions, exact-once publication, entrance/coordinate preservation, and no-demolition markers PASS; Unity Console Warning/Error is 0/0.
- Random stream manifest `RESULT=PASS`, named isolation tests `11`, legacy global character streams `0`, direct runtime Unity Random uses `0`.
- Six-adult outage PlayMode is fresh green at `2026-08-18T13:17:45Z`: outage fallback 5/5, primary recovery 5/5, primitive recovery starts 0, exact facility restore, and final Console Warning/Error 0/0.
- Final paired run is green for 32 deterministic seeds / 4 arms / 512 windows / 640 floor rows: median, p95 and max clutter wait delta are 0; access/egress clutter and RNG cross-talk are 0.
- Final current-source audit is `RESULT=PASS; rows=84240; critical=0; collapsed=327; approved=327; scc=313; minimumMarginMilliEwu=-14364087; integrityFailures=0`.
- Final whole-game coverage is `RESULT=PASS; rows=84240; domains=12; producerOrphans=0; consumerOrphans=0; approvedUnapplied=0`; a repeated current-source capture produced identical bytes by design.
- Final deterministic regeneration is `ULTIMATE_NOOP_EXACT=true`; Unity is idle in EditMode and Console Warning/Error is 0/0.

# 2026-08-19 strict completion re-audit (active)

- [x] Re-read the attached V27 authority and compare every explicit requirement against current source and durable evidence.
- [x] Correct the RNG manifest aggregate-row counting error (`namedIsolationTests=10`, not 11) and regenerate the EditMode artifact.
- [x] Project every required capacity, continuity, clutter, RNG and expansion metric into the canonical V27 ledger rather than leaving it only in side reports.
- [x] Add a production output-capacity boundary for harvest/mining so one bounded source batch is allowed, full storage+containment blocks with a typed reason, and no output is lost or duplicated.
- [x] Add focused regressions for output saturation/recovery and regenerate the affected deterministic artifacts.
- [x] Freeze source, compile through Unity MCP, and rerun current-source production-live fault/logistics evidence without saving or overwriting the user's dirty GameplayScene.
- [x] Re-run final V27 audit, whole-game coverage, deterministic no-op, economy/SCC, three-seed evidence and Console 0/0.
- [ ] Commit only task-owned changes, push a follow-up branch, obtain green CI/review evidence, merge to main, and verify remote main.

Current correction: the strict gaps are now closed. Final current-source evidence is `RESULT=PASS; rows=84389; critical=0; integrityFailures=0`, all three five-day seeds pass with actual/effective means `53.277911/48.914867 WU/성인·일`, two whole-game captures are byte-identical across all 11 key artifacts, and Unity Console is `0/0`. Only Git publication, hosted CI/review, main merge, and remote-main verification remain.
# 2026-08-20 V27 물리 중량 Gate S1 구현 계속

- [x] L01 창고의 25,000g authored capacity, Stored gram index, warehouse-local revision, generic partial admission token, terminal tombstone, idempotent commit receipt를 연결했다.
- [x] 첫 production `SpawnStockInWarehouse` 유입을 gram admission transaction으로 전환하고 affected-record rollback journal을 검증했다.
- [x] detached facility candidate와 current-format physical restore를 exact owner/category/position으로 결합했다.
- [x] 정상 소유자의 39,300g/25,000g 초과 적재는 보존하고 신규 입고를 차단하며, orphan destination과 좌표 변조는 stage에서 원자 거절한다.
- [x] 선택된 production 창고의 building/card/tab UI를 canonical kg로 전환하고, 물리 아이템 개수와 질량 용량 차원을 분리했다. 실제 표시 `12kg/25kg`, legacy `/60` 부재를 PlayMode에서 확인했다.
- [x] 공식 `DungeonSaveSectionRegistry.RestoreAll` 전체 왕복에서 위 질량·초과 적재 계약을 증명했다.
- [x] stage/validation 중 live mutation 없이 root swap 뒤에만 exact 초과 적재 대피 요청을 공개하고, 원본 restore에서 정확히 정리한다.
- [x] 공개된 대피 요청을 destination mass admission과 결합해 실제 정리 운반 완료까지 닫았다. 과적 source에서 target으로 exact lot 15,000g을 AIHaul로 이동하고 token/reservation/pending을 0으로 정리했다.
- [x] positive gram warehouse의 lifecycle을 physical repository+inbound admission+active haul intent query에 연결하고, non-empty 철거/이전을 actual production command에서 무변경 거절했다.
- [x] combat equipment의 base+attached module+loaded ammunition 동적 gram을 immutable prepared subject, warehouse admission/restore, actual AI Stored 입고까지 연결하고 10,000회 p95≤2ms·0B를 증명한다.
- [x] apparel component를 immutable prepared mass subject로 연결하고 material·quality·내구·오염·수분을 질량 불변으로 고정한다. 멜빵 physical 1,150g을 equipped burden에 정확히 한 번 포함하고 carry 수량·UI projection을 같은 질량 query로 연결한다.
- [x] 18개 live wildlife species의 CarcassWeight와 physical carcass definition gram을 exact projector로 결합하고, 누락 13개 definition을 deterministic builder로 생성하며, 사냥 사체 exactly-once와 누락 정의 fail-loud를 증명한다.
- [x] 사체 부패·도축의 입력 선삭제를 제거하고 output preflight·gram receipt·실패 source 보존을 갖춘 atomic Transform으로 전환한다.
- [x] 사체·packaged projection 변경 뒤 authored 414-item/355-recipe/1,074-weight-site inventory와 writer/source digest를 deterministic recapture한다.
- [x] conveyor overflow의 count-only warehouse 선택을 제거하고 exact lot gram admission·commit receipt·부분 수용 무변경 거절·InTransit 보존으로 전환한다.
- [ ] packaged lot, exact-instance 외부 retail handoff 및 나머지 ingress를 gram admission으로 전환한다.
  - [x] packaged-lot authoring feature, immutable content/tare runtime projection, exact container-mass validation과 production DI를 추가한다.
  - [x] 수술 FacilityBuffer 재료 소비를 pending Sink receipt로 이관하고 packaged tare 부산물을 commit marker 기반 exact-once Loose 출력으로 연결한다.
  - [x] tare publication을 Items 공용 outbox 서비스와 좁은 output gateway로 분리하고 식사·물질 사용 acknowledgement도 동일 경계를 통과시킨다.
  - [x] `medicine:anesthetic` 전용 reusable `container:medical-vial` 폐쇄 루프를 deterministic economy builder에 작성하고, 900g 철괴→30×30g 바이알 및 120g 마취제→30g 바이알 회수 계약을 fail-loud topology 검증으로 고정한다.
  - [x] 공통 tare focused contract를 Unity Editor에서 실행하고 Console Warning/Error 0/0을 확보한다.
  - [x] economy builder를 Unity에서 실행해 실제 SO와 item/domain catalog를 재생성하고 canonical item 414·recipe 355·weight site 1,074·package contract 1을 전수 감사한다.
  - [x] 실제 foreign-body 수술에서 authored 마취약 2개를 AI haul/Sink하고 바이알 2개/60g을 exact-once 반환하며 current-format restore 뒤 `2→2` no-duplicate를 증명한다.
  - [x] 만료 식사 delivery 교체가 old pending route를 남기던 current-format 결함을 원자 swap으로 수정하고 Survival/full save regressions를 통과한다.
  - [x] `SerializeReference` feature의 managed RID churn을 semantic no-op dirty gate로 차단하고 Economy asset 494개 연속 rebuild 합성 hash identity를 증명한다.
  - [x] 반환 바이알의 일반 AI warehouse 재입고와 후속 마취제 재생산 1회까지 하나의 production-live 순환으로 연결한다.

## Gate S2e exact-lot retail stock (in progress)

- [x] 상점 보충·내부 재고·구매·저장 DTO의 현재 count/category 경계를 전수 식별한다.
- [x] current-format `RetailStockLot`의 exact item/instance/components/quantity/gram/source operation DTO와 core aggregate 권위를 추가한다.
- [x] checkout/shoplifting을 aggregate의 deterministic exact-lot removal command로 이관하고 terminal receipt를 commit result에 보존한다.
- [x] warehouse restock을 category count 제거가 아닌 exact physical lot transfer로 전환한다.
- [x] 외부 고객 구매를 exact lot terminal Sink receipt로 원자 커밋하고 unique instance를 보존한다.
- [x] restock 후보·가용성 판정을 category aggregate가 아닌 exact `itemDefinitionId` physical quantity로 전환하고 same-category decoy 회귀를 고정한다.
- [x] runtime DI 순환을 retail-only equipment authority로 분리하고 authored source activation/save capture를 persistent identity 이후의 명시적 동기화 경계로 고정한다.
- [x] restock/purchase 실패 rollback, current-format save roundtrip, idempotent purchase receipt와 full restore tamper/atomic rollback을 검증한다.
- [x] 실제 Brain/BT Shopping과 Restock PlayMode, Unity compile, Console 0/0을 통과시켜 이 Gate를 닫는다.

Structure contract draft:
- Content definition: `SaleItem.ItemDefinitionId`와 physical item catalog가 판매 가능 definition의 immutable authority다.
- Runtime authority: shop aggregate가 exact `RetailStockLot` 목록을 유일하게 소유하며 `RemainStock` count projection은 UI/offer read model로만 둔다.
- Commands: warehouse→shop restock coordinator가 exact source stack slices를 준비/커밋하고, purchase command가 exact retail lot을 terminal external Sink로 커밋한다.
- Query: AI/UI는 sale item별 available quantity와 derived gram만 읽으며 physical identity mutation API를 노출하지 않는다.
- IDs: saleItemId는 catalog key, item definition/instance/component fingerprint는 physical identity, restock/purchase operation+commit IDs는 exact-once authority다.
- Save: current-format shop lot DTO가 exact identity와 quantity/source operation을 저장한다. count projection과 derived mass는 저장하지 않는다. 과거 save migration은 제외한다.
- Dependencies: Buildings core owns immutable retail lot/save state; Assembly-CSharp integration owns repository/warehouse/economy atomic orchestration. Buildings core가 Items concrete service를 참조하지 않는다.
- Failure: missing/mismatched definition, unique instance/component, source revision, payment or lot quantity rejects before mutation. partial physical/stock/payment commit rolls back all touched authorities.
- Cutover: category-only `ConsumeWarehouseStock`, saleItem count-only restock, unique purchase silent return을 production에서 제거한다.
- Evidence: generic+unique restock, exact purchase Sink, insufficient stock/payment interruption, repeated commit, save/restore, actual AI shopping and restock.

Current Gate S2 evidence: focused combat-equipment 5개 marker와 apparel/harness 5개 marker PASS. full `Artifacts/QA/physical-item-logistics-playmode-report.txt` UTC `2026-08-20T12:49:29.8397855Z`, `RESULT=PASS; failures=0`, crafted dagger `700g/0.7kg`, warehouse evacuation `15,000g`, carry UI `2.4/19.13/28.69kg`, Unity Console Warning/Error `0/0`.

## Gate S2f typed disposition and WIP cutover (in progress)

- [x] Partial physical transform preserves the unconsumed quantity under the original stack ID.
- [x] Multi-input transform consumes deterministic exact slices across several stacks and emits one gram-conserving receipt.
- [x] Invalid output preflight and injected post-commit publication failure both preserve exact source identities, quantities, positions, and prior outputs.
- [x] Catalyst dismantle/refine/advance, catalyst exchange, and exterior cargo damage no longer use consume-then-spawn.
- [x] Classify every production `TryConsumeStackQuantity` call as Source, Transfer, Transform, or Sink and remove all direct production callsites outside the typed Items boundary.
- [x] Add typed disposition receipts and migrate terminal food/wildlife/breakdown/medical/captivity consumers.
- [x] Add atomic batch Transfer for apparel, facility evolution, faction goods, waste feed, and equipment lineage inputs.
- [x] Persist production WIP input commit ID, exact quantity/mass, and per-cycle sequence in current-format Production V8.
- [x] Resolve probabilistic production outputs once, persist the complete result vector, and prove output-block save/restore does not reroll under a different RNG seed.
- [x] Persist per-output committed quantity so a later output failure/save/restore resumes only the missing units instead of duplicating earlier outputs.
- [ ] Persist global disposition receipt/outbox authority and make retry by commit ID idempotent across save/restore.
  - [x] Production WIP multi-stack input uses a V10 pending batch receipt, exact retry replay, conflict rejection, restore validation, and idempotent acknowledge.
  - [x] Surgical-part installation uses a per-order/per-part operation ID, persists exact commit/source/subject provenance in current-format Surgery V9, reconciles the physical pending receipt before Surgery candidate publication, rejects tampering without losing the receipt, and acknowledges exact-once after durable domain terminalization.
  - [x] Captive labor-tool assignment uses a per-captive/per-instance operation ID, persists exact commit/source provenance in current-format Captivity V3, reconciles the physical pending receipt before captive candidate publication, rejects tampering without losing the receipt, and acknowledges exact-once after durable assignment publication.
  - [x] Faction restitution uses a scar-unique operation ID, persists exact physical/campaign provenance in current-format Faction V2, finalizes faction restitution and the absolute campaign grievance target before exact acknowledgement, and reconciles incomplete provenance after physical/offense restore candidates are available.
  - [x] Facility evolution aggregates all material requirements into one per-facility/per-history pending batch, preserves the source facility on replacement failure, and replays the same pending receipt on retry without a second material debit.
  - [x] Persist the facility-evolution pending recipe/operation/commit and resolved mutation inputs in current-format Facility V4 state, validate the Physical receipt join in restore participant 224, and reconcile material-committed/domain-applied phases automatically without rerolling or replacing twice.
  - [x] Migrate apparel repair to a current-format durable material outbox: make work-order publication a reversible restore participant, persist exact original/resolved apparel payload plus Physical receipt and `MaterialCommitted|RepairApplied` phase, and prove no second material debit or durability application across retry/restore/tamper.
  - [x] Migrate wildlife food-raid theft to a current-format V5 pending Sink outbox with per-wildlife operation identity, exact receipt join, actor-loss finalization, retry/tamper evidence, and restore-participant reconciliation.
  - [x] Migrate recurring faction goodwill to a current-format Faction V3 pending Transfer outbox with a persisted monotonic operation sequence, absolute rapport target, same-day repeat isolation, exact acknowledgement, and restore reconciliation.
  - [x] Migrate captured-wildlife normal/waste feeding to a current-format Circus V3 + Wildlife V6 pending Sink outbox with a monotonic per-animal feed sequence, once-resolved disease outcome, absolute actor targets, restore reconciliation, focused fault coverage, and production-live FacilityBuffer consumption evidence.
  - [x] Migrate External Influence trail-charm intel unlock to a current-format V4 pending Sink outbox keyed by the one-time site ID, with domain-first publication, acknowledgement-only retry, strict receipt/save validation, restore reconciliation, and focused tamper/no-double-debit evidence.
  - [x] Add the Items-layer reserved pending Sink primitive: one unique operation atomically consumes its exact quantity lease and physical source, replays by pending receipt after the lease is gone, and restores both source quantity and the original lease when publication fails.
  - [x] Migrate Character Consumables meals to current-format V7: persist `ItemCommitted|EffectsPublished` receipt provenance, join the exact Physical pending Sink during restore, publish meal effects once, acknowledge after domain publication, and reject receipt tampering or missing committed custody without a second serving debit.
  - [x] Migrate primitive field meals to the same Character Consumables V7 pending Sink outbox: use the aggregate-generated unique operation, persist the exact receipt under a canonical virtual `BuildingInstanceId`, retain effects-published custody across acknowledgement failure, and restore without a second serving debit or hunger effect.
  - [x] Migrate Character Consumables substance use to current-format V8: commit Loose, FacilityBuffer, and Carried doses through one pending physical Sink receipt, persist once-resolved tolerance/addiction/overdose targets, publish effects once, acknowledge after publication, and reject receipt tampering or missing committed custody without a second dose debit.
  - [ ] Migrate remaining single-stack/terminal domains only after each owns a genuinely unique action/epoch ID; do not deduplicate recurring semantic IDs.
- [x] Close the remaining crash/exception window between a custom output handler's physical commit and WIP `committedAmount` advancement with a shared outbox/transaction journal.
- [ ] Close cancel/facility-destroy WIP salvage/loss and process-fluid/byproduct mass disposition.
  - [x] Output-free consumed WIP cancellation and missing/destroyed facility publish one current-format V10 terminal receipt with exact input commit, quantity, grams, and typed `ExplicitSink` loss; unconsumed routed stock is conservatively released.
  - [x] Persist, restore, canonical-sort, bound, and tamper-validate terminal WIP receipts; focused cancel/destroy/save/restore evidence passes.
  - [x] Reconcile WIP after one or more output units have already committed without double-counting emitted mass.
    - [x] Persist exact committed output grams per resolved output line for standard FacilityBuffer output, stateful apparel, and surgical-part custom handlers.
    - [x] Join committed output grams with process-fluid/byproduct grams before allowing partial-output terminalization.
  - [x] Replace split process-fluid consumption with one aggregate preflight/commit receipt and close wastewater/byproduct grams.
    - [x] Facility-authored and recipe-authored clean-water/wastewater demand is now summed before validation and consumed in one facility call, removing the former first-call-success/second-call-failure window.
    - [x] Persist exact clean-water/wastewater grams in Production V11 and include them with committed output grams in the terminal mass equation; partial-output cancel/destroy is now allowed only when the equation is non-negative and exact.
    - [x] Linked supports sharing one network use one deterministic network-wide preflight/commit transaction. Aggregate water shortage rejects without changing water, wastewater, or network revision; success commits all demands with one revision increment.
    - [x] Manual-container clean-water uses exact `resource:clean-water` stack slices, a pending physical Transfer receipt, V5 fluid reserve provenance, V12 production receipt persistence, conflict rejection, idempotent replay, and explicit acknowledgement. Same-category decoys are never consumed.
    - [x] Production V13 preserves typed wastewater components by composition, source kind, canonical source stable ID, authored units, and exact grams in active WIP and terminal receipts. All 43 nonzero authored recipe/facility/support sources are classified; mismatched sums, duplicate keys, and one-gram save tampering fail before mutation. Hydraulic volume remains aggregated only for network capacity.
- [x] Require the production raw-consume manifest count to reach zero and prove full current-format save/restore.

Current evidence: `V27_PARTIAL_TRANSFORM_QUANTITY_AND_MASS_EXACT`, `V27_INVALID_TRANSFORM_OUTPUT_FAILS_ATOMICALLY`, `V27_MULTI_INPUT_TRANSFORM_EXACT`, `V27_POST_COMMIT_EXCEPTION_RESTORES_EXACT_SOURCES`, and `V27_PRODUCTION_RAW_CONSUME_CALLS_ZERO` pass in the focused physical stock contracts. Evolution and Exterior Editor suites also pass after the live path migration. Fresh full `Artifacts/QA/physical-item-logistics-playmode-report.txt` UTC `2026-08-20T20:54:48.1823414Z` is `RESULT=PASS; failures=0` with captured Warning/Error `0/0`. Fresh `Artifacts/QA/ai-mid-action-save-load-playmode.txt` UTC `2026-08-20T20:55:07.2614796Z` is `result=PASS`, `failures=0`, `HAUL_SAVE_REPEATED_RESTORE_CONSERVATION_EXACT`, and no unexpected Error/Exception/Assert logs. The raw-consume/static and current-format full-save exit row is closed; the parent remains open for remaining genuinely unique terminal-domain outbox migrations.

Receipt/outbox contract correction:
- A permanent operation-ID tombstone is forbidden because recurring survival/consumable actions currently reuse semantic operation IDs across valid later consumptions.
- Physical authority persists only pending, unacknowledged receipts. An exact retry replays the same receipt without consuming again; a mismatched request using the same pending operation fails loudly.
- The consuming domain must durably record the receipt commit ID, then explicitly acknowledge it. Acknowledgement removes only the matching pending receipt so a later independent action may reuse a semantic prefix with a new action/epoch operation ID.
- Production WIP output commits require deterministic per-cycle/per-output-unit IDs and an output-side reconciliation marker; advancing `committedAmount` is allowed only after exact physical/custom output authority confirms that commit ID.

Current receipt evidence: Unity clean compile and `PhysicalStockQueryV18DebugScenarios.RunAll()` PASS with `V27_PENDING_DISPOSITION_RETRY_IDEMPOTENT`, `SAVE_RESTORE_EXACT`, `CONFLICT_FAILS_LOUD`, `ACK_IDEMPOTENT`, and `TAMPER_REJECTED`. `ProductionEconomyDebugScenarios.RunAll()` also remains PASS after WIP acknowledgement integration. Surgical-part V9, Captivity V3, Faction V2, and Facility V4 focused rows prove exact current-format provenance, tamper rejection without receipt loss, domain-first terminalization, and acknowledgement replay. Facility V4 persists the canonical recipe/operation/commit/source/quantity/grams, exact resolved result payload, mutation tags, and `MaterialCommitted|DomainApplied` phase. Restore participant `224.world.facility-evolution-materials` rejects authored-recipe or Physical receipt mismatch before restore completion; the post-restore projection publishes the stored result without re-running proposal, mutation, record-token, or material selection. Replacement failure, current-format JSON round trip, six tamper cases, acknowledgement-only replay, automatic projection, Dungeon save-section/composition, and Offense Strategic 11-row regressions pass with final Console Warning/Error `0/0`. The facility terminal slice is closed; the global parent remains open for remaining genuinely unique terminal domains.

Current output evidence: Production V9 persists a deterministic per-cycle/per-item/per-unit pending output commit ID and whether its physical/custom publication completed. Standard buffered output is atomically born with a temporary commit provenance component and acknowledges that marker only after the production aggregate advances. Environmental workwear and surgical-part custom handlers implement the same idempotent replay contract. An injected exception after the physical output commit, followed by save/restore under a new runtime, produced exactly the missing units with no duplicate or loss. `ProductionEconomyDebugScenarios.RunAll()`, `EnvironmentalFieldDebugScenarios.RunAll()`, and `SurgeryDebugScenarios.RunAll(true)` are PASS; Unity Console Warning/Error is `0/0`.

Current WIP terminal evidence: Production V13 owns canonical terminal receipts after the bill itself is removed. Active cancellation and facility disappearance record exact WIP input, process clean-water, typed process-wastewater components, already committed output, terminal reason, and the remaining declared loss. The focused partial fixture closes `3000g input + 100g clean water = 1000g output + 50g wastewater + 2050g loss`; current-format save/restore is byte-exact and one-gram output or wastewater-component tampering is atomically rejected. `ProductionEconomyDebugScenarios.RunAll()` is PASS. The process-fluid/byproduct gram sub-slice is complete; the parent remains pending for the broader disposition/outbox and downstream treatment/disposal scope.

Current apparel repair outbox evidence: current-format Character Environment V6 persists the exact repair operation/reason/commit/source vector, input quantity/grams, target stack, canonical original/resolved apparel payload and `MaterialCommitted|RepairApplied` phase. `226.world.apparel-work-orders` stages order publication, validates the Physical pending receipt join, and reconciles only the stored phase before completion. The focused real-physical fixture proves one thread+one scrap debit, durability `40→70` once, acknowledgement retry without a second debit, valid-looking commit tamper rejection with live order/item/receipt unchanged, and normal restore finalization. Unity compile plus Dungeon save-section/runtime-composition contracts pass with Console Warning/Error `0/0`. Broader V22 and PhysicalItem aggregate suites currently retain unrelated pre-existing fixture blockers (`dreamweave MaxStack=100`, `stored water mirror missing`); they are not counted as apparel outbox failures or silently waived.

Current wildlife food-raid outbox evidence: Wildlife V5 stores per-actor `wildlife-food-raid:{raidId}:{wildlifeId}` operation identity, exact Sink commit/source/quantity/grams/item and `ItemCommitted|RaidPublished` phase. The live tick, actor-death/removal boundary, and participant `250.world.wildlife` restore publication all reconcile the same receipt before allowing terminal departure. A focused real-physical fixture proves two wolves in one raid use distinct operations, each ration disappears once, acknowledgement retry does not reconsume, and a valid-looking commit tamper leaves the live pending receipt unchanged. `WildlifeDebugScenarios.RunAll(false)` and Dungeon save-section contracts pass after clean compile with Console Warning/Error `0/0`.

Current fluid evidence: facility ability, recipe demand, and every deduplicated linked support are collected before mutation. Piped demands are sorted by quality/node/input ordinal and committed by `IFluidInfrastructureBatchTransaction`; a real shared-network fixture proves that two individually affordable 3-unit demands against 5 units reject with water/wastewater/revision unchanged, then commit exactly 6 units of water and 2 units of wastewater with one revision after supply reaches 6. Manual clean water stages exact `resource:clean-water` lots through the Physical V10 pending Transfer boundary, stores source stack IDs/commit/grams in Fluid V5 and Production V13, preserves `0.8` authored units after a `0.2` use of one 500g bulk unit, rejects conflicting replay, and acknowledges after aggregate advancement. Typed wastewater classifies all 43 nonzero authored sources and preserves sorted component provenance. A real runtime fixture proves mismatched `0.3` wastewater versus `0.2` components changes neither wastewater nor pending transfer state, while exact `0.1 SanitaryWashwater + 0.2 Whey` commits 150g and returns the deterministic component vector. Production Economy, Production Workshop, Industrial Infrastructure, Environmental Field, Surgery, and focused physical suites pass; Console Warning/Error is `0/0`.

Current Gate S2c evidence: focused carcass mass/atomic transform markers 5개 PASS, full PhysicalItemLogistics UTC `2026-08-20T13:23:33Z` PASS, Wildlife hunt UTC `2026-08-20T13:21:29.7885162Z`에서 `HUNT_CARCASS_EXACTLY_ONCE`와 `RESULT=PASS; failures=0`, Console Warning/Error `0/0`. authored ledger 413개·catalog/serialized sites 1,060개·writer 18개·unknown 0으로 새 source inventory recapture도 byte-identical PASS다.

Current Gate S2d evidence: `ConveyorItemGateway`는 더 이상 `WarehouseInventory.CanStore(category, count)`로 Stored 전환하지 않는다. `ItemTransferService.TryCompleteTransitToWarehouse`가 exact item/instance/component lot, warehouse-local revision과 full quantity gram token을 예약하고 물리 `InTransit→Stored` 뒤 receipt를 커밋한다. 25,000g 창고에 25,200g 화물은 부분 수용 없이 전량 InTransit으로 보존되고 reserved grams=0이며, 2,400g 화물은 exact receipt로 입고된다. focused markers 2개와 fresh full PhysicalItemLogistics `RESULT=PASS; failures=0`, Console 0/0을 통과했다. fresh Industrial PlayMode도 기존 씬 노드 2개를 포함한 실제 28-capacity cyclic network를 Deadlocked로 관찰하고 exact overflow physical release 후 `result=PASS`했다. 이 Industrial 행은 컨베이어 주변 회귀 증거이며 warehouse gram commit 자체는 focused production gateway 계약이 직접 증명한다.

Current Gate S2e evidence: 상점 aggregate는 generic/unique `RetailStockLotSnapshot`을 exact item definition·instance·component fingerprint·quantity·gram·source operation으로 소유한다. 실제 Restock 작업은 exact item definition quantity lease, physical pickup, `TryTakeReservedRetailLots`, `TryReceiveExactRetailLots` 순으로 commit하며 실패 시 world stack·carry·equipment state를 복원한다. authored unique 외부 source는 `RetailStock` equipment state로 materialize되고 customer checkout은 terminal external Sink receipt 뒤 equipment instance를 exact-once 제거한다. focused `PhysicalItemDebugScenarios`의 unique warehouse→retail commit/rollback, `CustomerAiDebugScenarios`의 generic+unique source/Sink, `DungeonSaveSectionDebugScenarios`의 shop lot↔physical/equipment exact preflight join이 fresh PASS다. Restock planning의 마지막 category-only read도 exact `itemDefinitionId` physical query로 교체됐고 same-category decoy negative가 PASS한다. Production UI의 count-only 즉시 보충은 제거되어 실제 hauler replan만 요청한다. Fresh WorkType production-live에서 `work:restock`이 PASS했으며 aggregate FAIL은 후속 Repair fixture의 destroyed FX 참조다. 남은 Gate blocker는 targeted tamper/full-restore rollback, actual Shopping PlayMode, 최종 Physical/SaveLoad 및 Console 0/0이다.

Gate S2 error log:
- 초기 stateful mass 성능 실행은 component JSON 재파싱을 제거한 뒤에도 stale `Assembly-CSharp.dll`을 계속 실행해 7.6861/7.2306/5.5639ms 값을 냈다. MCP Console은 0/0이었지만 동적 reflection에서 신규 API가 없었고 DLL timestamp도 source보다 오래됐다. 이 값들은 최신 구현 증거로 사용하지 않는다.
- 숨은 실제 compile blocker는 `WorldItemWarehouseService` restore rebind에서 존재하지 않는 지역 `massQuery`를 참조한 CS0103이었다. `Editor.log`로 확인해 이미 질량 query를 소유한 `IWarehouseMassAdmissionService.PrepareMassSubject` 경계로 옮겼다.
- 첫 script-compilation 동적 명령은 unqualified `CompilationPipeline`이 잘못된 namespace로 resolve돼 temporary-command CS0234가 났다. project source는 변경되지 않았고 이후 fully-qualified `UnityEditor.Compilation.CompilationPipeline`만 사용한다.
- coverage capture가 durable manifest 파일을 쓸 것이라 추정한 경로는 존재하지 않았다. `CaptureReportWithoutThrowing()` 반환값을 즉시 필터링해 Physical row와 aggregate result만 캡처하도록 교정했다.
- 첫 conveyor full Industrial PlayMode 재실행은 기존 deadlock fixture가 merged network node ID를 `createdBuildings.First(...)`로 찾는 경계에서 `Sequence contains no matching element`로 실패했다. live node resolver로 교정했다. 두 번째 격리 시도는 초기 3행 던전에 12x3+halo 공간이 없어 실패해, 원본 physical/conveyor snapshot rollback을 유지한 채 기존 씬 노드까지 exact live network의 일부로 검증하도록 바꿨다. 세 번째 실행은 overflow 상태를 기다리지 않고 stack 존재만 기다리는 false-negative를 드러내 exact Loose 상태를 기다리도록 교정했다. 최종 fresh report는 `result=PASS`다.
- Unity dynamic compile 요청 한 번은 `Unity.CompilationPipeline` namespace로 잘못 해석돼 CS0234가 났고, `global::UnityEditor.Compilation.CompilationPipeline`으로 즉시 교정했다. 이후 domain reload 직후 한 번은 임시 RunCommand DLL load error가 났으나 `AssetDatabase.Refresh` 재시도 후 project compile과 Console 0/0을 확인했다.

## 2026-08-24 Unity MCP current-revision audit and economy source-rebase gate

- [x] Current Unity compile and Console Warning/Error `0/0`.
- [x] Builder no-clobber: 5 builders, 7,219 files, 0 changes.
- [x] Physical item contracts 44/44; ledger contracts 13/13.
- [x] Six-adult closed loop, N+1, static/asset spatial capacity, output capacity current-revision PASS.
- [x] Fresh paired-clutter PlayMode: 32 seeds, 512 windows, 0 failures, clean A/B exact, RNG cross-talk 0, Wait WU p95 0%, Console 0/0.
- [x] Added fail-loud, rollback-safe approval-refresh path for a previously approved scalar whose source BOM legitimately changed.
- [ ] Run the reviewed rebase/apply command after Unity MCP permission is restored; current MCP returns `Connection revoked`.
- [ ] Re-run 256-seed economy, whole-game coverage, labor/facility/market audit, and no-op artifact/asset diff.
- [ ] Continue 363 item semantics, 355 recipe mass audit, full kg After, EWU/price regeneration, and final 3-seed.
- Checkpoint plan SHA-256: `19DF6E6E434F0EC7D659A8BF429C09330F738E012C0688B8BB875D660ED15AA1`.

## 2026-08-24 Phase 2 authority-backed equipment/apparel semantic slice

- [x] Added explicit unit semantics from 61 exact combat-equipment mappings.
- [x] Exact-joined 56 apparel definitions to 56 distinct physical-item semantics, including four non-`apparel:` physical IDs.
- [x] Added IndividualEquipment haul class and separated commodity batch versus single-equipment validation.
- [x] Kept all authored kg/BOM/WU/EWU/price values unchanged.
- [x] Compiled DungeonStory.Economy and Assembly-CSharp-Editor with current Bee response files, exit 0.
- [ ] Run deterministic Unity recapture after MCP permission returns; cumulative expected 355/414 only after PASS.
- Revised checkpoint plan SHA-256: `5A8B4232D55F171A75C5CC8CA3BB9963C26DD722C1BBC3A767CE5CCFE305A06D`.

## 2026-08-24 Phase 2 exact non-packaged commodity semantic slice

- [x] Added exact unit semantics for 21 ammunition, 4 records, 4 waste bundles, 4 non-iron ingots, 20 fiber/yarn/textile items and dog food.
- [x] Used exact stable-ID allowlists and fixed expected count 54; no prefix-based future auto-approval.
- [x] Excluded medicine/drug/sample packaging decisions from this slice.
- [x] Kept current grams as Before/provisional After and made no authored asset mutation.
- [x] Verified current maxStack supports 6–11kg for every added Ordinary commodity; records use single-unit MicroUrgent semantics.
- [x] Current Bee/Roslyn compile exit 0.
- [ ] Unity deterministic recapture cumulative expected 355/414, remaining 59, after MCP permission returns.
- Revised checkpoint plan SHA-256: `1950A9EE99718975D48CFF8CC5584E5CD8A590311F8E79EC1971B364845A314A`.

## 2026-08-24 Phase 5 all-recipe mass inventory foundation

- [x] Added deterministic AuditOnly inventory for all 355 recipes.
- [x] Captures input/water, guaranteed/maximum/expected probabilistic output, wastewater and residual gram ranges.
- [x] Separates Source/Transform/Sink and flags role-shape drift.
- [x] Exact-joins the existing 38 reviewed transforms; never auto-approves residual loss.
- [x] Uses proposed semantic grams when available and marks every current-mass fallback as `unit-semantic-missing`.
- [x] Emits canonical `v27-recipe-mass-balance.csv` plus IN_PROGRESS audit summary.
- [x] Current Economy and Editor Bee/Roslyn compile exit 0.
- [ ] Execute Unity capture and close every mass-creation/missing-disposition row.
- Revised checkpoint plan SHA-256: `2BFDE11DA19B10FE79C44E1F35735A462BBEFDBA5814E92E41B516B09A346993`.
- Semantic-aware recipe-inventory plan SHA-256: `84BCDEA01EF4BAC13F74C5AE27664C01087DD597ADB4914415369F37494C1B3E`.

## 2026-08-24 Phase 2 exact raw-resource semantic slice

- [x] Added exact unit meanings for 22 unpackaged world/animal/mineral resources.
- [x] Separated blood liquid volume, mineral lots, stone block, animal bundles, herb bundles, catalyst/relic units and manure waste.
- [x] Classified feather, rune dust and trail charm as MicroUrgent because current max stacks cannot form 6kg ordinary batches.
- [x] Kept current grams provisional and all authored values unchanged.
- [x] Current Bee/Roslyn compile exit 0.
- [ ] Unity cumulative recapture expected 355/414, remaining 59.
- Revised checkpoint plan SHA-256: `84438B3D4ADF1E54088AF4F44A5578BAFCDD3808CE642ADE56C2411C4A4C5352`.

## 2026-08-24 Phase 2 tools/prosthetics/bedding semantic slice

- [x] Added exact semantics for 17 non-harness tools, 3 prosthetics and husbandry bedding.
- [x] Reused apparel semantic for hauling harness; no duplicate physical item semantic.
- [x] Separated 9 single reusable tools and 3 prosthetics from ordinary stocked kits.
- [x] Kept all grams and gameplay values provisional/unchanged.
- [x] Current Bee/Roslyn compile exit 0.
- [ ] Unity cumulative recapture expected 355/414, remaining 59.
- Revised checkpoint plan SHA-256: `8FA561A4B60726A13A088D182DC9494F9AB59E68E5D94162A6989BE1C4986708`.

## 2026-08-24 Phase 2 manufactured-component semantic slice

- [x] Added exact semantics for all 36 current `component:` ledger items.
- [x] Separated 2 engineering documents from 34 manufactured components/subassemblies.
- [x] Applied explicit current 2kg Small/Large review boundary and Ordinary batch gate.
- [x] Kept current grams provisional; no packaging or recipe conservation was pre-approved.
- [x] Current Bee/Roslyn compile exit 0.
- [ ] Unity cumulative recapture expected 355/414, remaining 59.
- Revised checkpoint plan SHA-256: `66D8AFDB7B1B362C0D08503C7556D789B617B8644A9D17DCC782B576B4765660`.

## 2026-08-24 warehouse candidate exact-item mass cutover

- [x] Changed production warehouse compatibility to require exact item ID plus stock category.
- [x] Changed haul destination prefilter and gameplay-flow diagnostics from count-only `CanStore(category, 1)` to `CanStoreItem(itemId, 1)`.
- [x] Confirmed production count-only one-item capacity callsites are 0.
- [x] Added a focused source-manifest regression gate and exact 10-accepted/11-rejected mass-boundary assertion.
- [x] Economy, runtime and Editor-with-new-recipe-inventory Roslyn compiles exit 0.
- [ ] Run focused Unity mass/logistics and production-output PlayMode evidence when MCP approval is restored.
- Checkpoint plan SHA-256: `66F207F70F18D55C822FB3F885B5AAB8371F3654E2E2E192FDEEA616126745FF`.

## 2026-08-24 unpackaged processed-material semantic slice

- [x] Corrected apparel authority from 52 namespace-matched IDs to all 56 catalog physical IDs; prevented duplicate cold-suit/harness semantics.
- [x] Added exact semantics for 40 unpackaged processed materials, 6 solid crafts and 2 small loot lots.
- [x] Used `BulkInfrastructureNotInUnit` for process lots and deferred every ambiguous packaged liquid/kit/dose.
- [x] Verified the new allowlist has exactly 48 candidate item IDs and current Editor compile exits 0.
- [ ] Unity cumulative recapture expected 355/414, remaining 59.
- Checkpoint plan SHA-256: `67CE55F060E6353518A4F0152FE08750258D24D57579DA8093FD987698B54CB2`.

## 2026-08-24 remaining packaging review ledger

- [x] Added deterministic AuditOnly inventory for all 59 remaining item semantics after the unpackaged meal/process slice.
- [x] Captures current package feature/tare/container plus exact producer and consumer recipe vectors.
- [x] Routes meals, bulk liquids, specimens, supplies, doses, medical kits and coatings to separate review contracts without auto-authoring tare.
- [x] Classifies source/transform/terminal/orphan recipe lifecycle and records the exact disposition proof required by each route.
- [x] Includes Survival/Surgery tare gateways in the source digest while keeping their per-item joins explicitly unproven.
- [x] Fixed the review scope to the canonical 414-item V23/V27 ledger instead of incorrectly treating the larger live ItemDefinitionCatalog as 414 items.
- [x] Requires every compiled semantic to remain inside that ledger and every ledger ID to exact-join a live item authority.
- [x] Records a deterministic ledger-scope digest and includes the catalog asset itself in the no-mutation source digest.
- [x] Added canonical CSV/report, double-capture identity and inspected-source no-mutation digest gates.
- [x] Editor compile including both new mass audit sources exits 0.
- [ ] Execute Unity capture and author every physical return/waste/transfer/sink contract.
- Latest checkpoint plan SHA-256: `F10663F3F8C78968BB2FB1D60AF9B0DE960C57E0E37F169E60BFE357A77948BC`.
- Ledger-scope correction plan SHA-256: `425B76CB7F06B38DE51160913BF3EAD929B1A055CA782F180006A5A24739EA15`.
- Current checkpoint plan SHA-256: `D51DA2E60BFF76D25F85F5136D23E46AEC13B73C1B7850845EC7F921022932D6`.

## 2026-08-24 unpackaged meal/process semantic slice

- [x] Added exact semantics for temporal seal, bulk alchemical solvent, three served meals and unpackaged jerky.
- [x] Separated reusable facility serving/process infrastructure from item tare.
- [x] Kept expedition/preserved ration packaging in the review queue.
- [x] Updated packaging review authority to 355 semantics / 59 missing; Editor compile exits 0.
- [ ] Unity semantic and packaging double-capture expected 355/414, remaining 59.
- Latest checkpoint plan SHA-256 after packaging lifecycle proof fields: `F10663F3F8C78968BB2FB1D60AF9B0DE960C57E0E37F169E60BFE357A77948BC`.

## 2026-08-24 destroyed package tare loss receipt

- [x] Replaced the unusable unconditional `DestroyedDuringUse` rejection with an exact parent-Sink-bound destroyed-tare mass receipt.
- [x] Kept reusable/disposable tare as physical outputs and kept `TransferredWithOutput` invalid at a terminal Sink.
- [x] Added exact `20g × 3 = 60g`, zero-spawn and identical replay focused assertions.
- [x] Runtime Roslyn compile exit 0; Editor compile with the updated runtime source explicitly included exits 0.
- [ ] Run the focused scenario in Unity and join every real destroyed-package item to a typed loss reason and saved parent receipt.
- Current checkpoint plan SHA-256: `6896E36AB704B63075A10CC0739C9FC13784A278809FFF9D14F1AA38EDD2C40A`.

## 2026-08-24 packaged consumable missing-service gate

- [x] Added exact-item packaged-lot detection before meal/substance physical Sink acknowledgement.
- [x] A packaged item now retains its pending receipt and returns typed failure when tare authority is absent.
- [x] Non-packaged items and non-physical fixtures are not rejected by a blanket constructor rule.
- [x] Runtime Roslyn compile and diff check exit 0.
- [ ] Run packaged missing-service and normal replay fault cases in Unity.
- Current checkpoint plan SHA-256: `27DC71D22BD8578B8470D7127A19981C7889094963D1429075F088E710103BFE`.

## 2026-08-24 exact 59-item packaging identity gate

- [x] Joined the canonical 414-row authority artifact with the current semantic source to enumerate the exact 59 unresolved IDs.
- [x] Included the previously easy-to-miss `medicine:mycelial-culture-pack` row.
- [x] Packaging capture now requires ordinal identity equality, not only a row count of 59.
- [x] Editor explicit-source Roslyn compile and diff check exit 0.
- [ ] Confirm the same 59 identities in Unity fresh capture.
- Current checkpoint plan SHA-256: `31FACF8C7D71F12E5750B81917D3C60C3887B1D4A3C04F91CC99EEE1AC9A7F13`.

## 2026-08-24 solid medical semantic promotion and Unity recertification gate

- [x] Promoted exactly five non-separable solid medical items from packaging review into authority-backed unit semantics.
- [x] Kept the organ-preservation canister unresolved because its storage/fuel/container lifecycle is distinct.
- [x] Updated the exact review identity vector to 54 rows; count 54, ordinal order exact, duplicates 0.
- [x] Current-source runtime compile exits 0 and Editor compile with the updated tare source exits 0.
- [ ] Unity fresh compile and Console Warning/Error 0/0; MCP currently returns `Connection revoked` despite the server endpoint being visible.
- [ ] Unity double-capture expected 360/414 semantics and exact 54 packaging-review rows.
- [ ] Run economy 256-seed rebaseline, 6-adult capacity, spatial 256-seed, continuity, paired clutter/RNG, Physical Logistics and Surgery after the fresh compile.
- Current checkpoint plan SHA-256: `37AEC10542054E5E0C33C3CD0336EC077AAC41801C98EA73EDA9E012DB17E93C`.

### Unity MCP approval-slot diagnosis

- [x] Read the project-local Unity MCP connection registry without modifying it.
- [x] Identified stale auto-approved `codex-mcp-client 0.148.0-alpha.9` connection `c977dcf0-a5af-40ca-8889-acf06556c03f` consuming the single 1/1 slot; its recorded PID 22072 is no longer running.
- [x] Identified the current `codex-local` request as rejected with `Your MCP connections limit is reached (1/1)`.
- [ ] User revokes/removes the stale approved connection and approves the current `codex-local` connection in Unity Project Settings.
- [ ] Re-run GetState, fresh Bee compile and all prepared Unity evidence gates after approval.

## 2026-08-24 packaging runtime-consumer authority join

- [x] Added the non-recipe physical runtime-consumer catalog to the packaging review source authority and no-mutation digest.
- [x] Validated canonical item/owner IDs, live catalog membership and duplicate link pairs.
- [x] Split recipe and runtime consumer columns and based lifecycle on their exact union.
- [x] Fixed the current review expectation at 11 runtime-consumer rows / 11 links within the exact remaining 54.
- [x] Bumped deterministic CSV schema to `v27.mass.packaging-review.2`; current-source Editor compile exits 0.
- [ ] Unity double-capture and live receipt/gateway joins remain pending after MCP approval cleanup.
- Current checkpoint plan SHA-256: `81EB55BA99F4B116DB4CF63DCEBA568A5C137BD52A25D5FEC74ACD9C6557FD14`.

## 2026-08-24 solid rune-hibernation catalyst semantic

- [x] Proved the catalyst is a solid one-unit surgical input from its exact solid BOM and procedure material route, with no authored liquid/container input.
- [x] Added it to authority-backed semantics without changing kg/BOM/WU/EWU/price/effect assets.
- [x] Updated exact current-source expectation to 361/414 and packaging review to 53 sorted unique IDs.
- [x] Current-source Editor Roslyn compile exits 0.
- [ ] Unity double-capture remains pending after MCP approval cleanup.
- Current checkpoint plan SHA-256: `C96A5BC07C459F62D77C520E90FD187AB715E9AB0CEB8F026E0A84D9FF1833A8`.

## 2026-08-24 integral poultice and inoculated-log semantics

- [x] Promoted herbal poultice as the complete fibrous treatment dressing, not content in a returned container.
- [x] Promoted inoculated log as the cultivation log body, not a supply package.
- [x] Updated current-source expectation to 363/414 and exact packaging review to 51 sorted unique IDs.
- [x] Current-source Editor Roslyn compile exits 0.
- [ ] Unity double-capture remains pending after MCP approval cleanup.
- Current checkpoint plan SHA-256: `F272BFAB2B23E5EEB25E2335D1416C569E27A3985D4D42332904982B835D1D67`.

## 2026-08-24 disease and offense runtime-consumer closure

- [x] Added the seven missing disease-response physical items; exact owner set is now 8.
- [x] Added all 11 Offense supply-package physical mappings under one real runtime owner.
- [x] Updated runtime consumer catalog exact count from 24 to 42 and added exact owner-set regressions.
- [x] Packaging review now expects 18 runtime-owner rows/links within the remaining 51 and hashes both owner implementations.
- [x] Runtime and current-source Editor Roslyn compiles exit 0.
- [ ] Replace disease untyped Sink and offense abstract-package deletion with pending typed receipts/outboxes.
- [ ] Unity focused and PlayMode evidence remains pending after MCP approval cleanup.
- Current checkpoint plan SHA-256: `28392506FAC15AC3EC180F65DF4D126849473C995060ED231A36B0BEC751C0A1`.
# Current implementation checkpoint (2026-08-24)

- [x] Static mass semantics prepared: current-source `363/414`, exact review `51`.
- [x] Disease field-response exact physical Sink + package tare + durable health outbox implemented and Roslyn-compiled.
- [ ] Run the new disease outbox fixture in freshly imported Unity and capture Console 0/0.
- [ ] Convert Offense supply package debit to exact physical custody `Transfer` with durable return ownership.
- [ ] Close remaining untyped removals and the remaining 51 semantic/package decisions.
- [ ] Regenerate canonical artifacts and run economy, six-adult, spatial, clutter and PlayMode gates.

## 2026-08-24 Offense physical custody Transfer checkpoint

- [x] Replaced expedition departure count deletion with exact physical pending `Transfer` custody.
- [x] Persisted custody/return phases, source stack provenance, input/return/loss gram closure and acknowledgement state in current-format Offense world schema v7.
- [x] Added deterministic, replay-safe Loose Source publication for survivor returns.
- [x] Reject unknown-package minting and returns above persisted owned quantities.
- [x] Added returned-item authored/preflight joins and focused transfer/return/replay/restore evidence.
- [x] Runtime and Editor Roslyn compiles plus scoped diff check exit 0.
- [ ] Run Offense strategic menu fixture in freshly imported Unity and capture Console Warning/Error 0/0.
- [ ] Run live departure/restore/partial-return/loss PlayMode coverage after MCP approval is restored.
- Checkpoint plan SHA-256: `EA22FD1C5BAB9CA4FCC818BC07EBEA3E1AD724C825E88F9AE09DECB896D4B6D7`.
- Checkpoint baseline SHA-256: `E62F0A577641E2ABB4974BCB878FCF117C233F295D7171E9DA1274C5583E5FA6`.

## 2026-08-24 urgent mitigation WIP/outbox checkpoint

- [x] Removed count-only mitigation `ConsumeDelivered` and destructive `RemoveDestination` completion.
- [x] Added pending physical Transfer-to-WIP, persisted before/after outcome and acknowledgement phases.
- [x] Added current-format restore validation plus exact Physical pending-receipt preflight join.
- [x] Added four exact runtime consumer links; catalog 46 and packaging review 18 rows/19 links.
- [x] Focused fault fixture proves one Transfer, one outcome and acknowledgement-only recovery after restore.
- [x] Runtime/Editor Roslyn compiles exit 0.
- [ ] Run Offense, V23 runtime-owner and packaging double-capture in fresh Unity.
- [ ] Run live facility delivery/work/whole-save recovery PlayMode.
- Checkpoint plan SHA-256: `DE15E4D1F9A3452C5EB76D7FACCDF08CFB9E5441E823F78EE6518912B331FEF8`.
- Checkpoint baseline SHA-256: `D6D072F129E4185EFAC040E12B09328D604D710D29BB903BD251A612FC3E6AD4`.

## 2026-08-24 physical vaccination Sink/outbox checkpoint

- [x] Removed vaccination count-only facility-buffer consumption and committed one exact physical Sink receipt instead.
- [x] Added independent current-format vaccination intent/outcome/sequence authority to PopulationHealth schema v3.
- [x] Added package-tare-before-immunity ordering, acknowledgement-only recovery and startup recovery.
- [x] Added exact whole-save joins for disease response and vaccination pending Sink receipts.
- [x] Registered the seven vaccine item/runtime-owner pairs; catalog 53 and packaging review 25 rows/26 links.
- [x] Added acknowledgement-fault/restore/replay focused fixture and compiled Species/runtime/Editor successfully.
- [ ] Run vaccination/V23/packaging scenarios in fresh Unity and capture Console Warning/Error 0/0.
- [ ] Resolve the current 1,540g input→1,600g vaccine output mass creation with unit semantics, reusable-vial BOM and explicit process loss before semantic approval.
- [ ] Run live facility delivery/vaccination/vial-return/whole-save PlayMode evidence.
- Checkpoint plan SHA-256: `5E4D80DC694831A86F09972EFE6622F7CD07014EE235826A8731980D8D09C90E`.
- Checkpoint baseline SHA-256: `0C3BC316614AB71631D85AB0B4E299BF90BA9A6FEB19C2FE0C68B0E1DED2AD2C`.

## 2026-08-25 character medical physical Sink/outbox checkpoint

- [x] Replaced character-medical count deletion with one exact FacilityBuffer physical Sink and package-tare-before-publication ordering.
- [x] Added current-format medical supply intent/outcome/sequence provenance, acknowledgement-only recovery and whole-save pending-receipt joins.
- [x] Replaced arbitrary `StockCategory.Biological` fallback with exact `captivity:extracted-blood`; repeated ticks do not duplicate its delivery request.
- [x] Split physical item restore validation onto the all-item catalog so generic extracted blood is valid without weakening medicine ranking authority.
- [x] Registered exact live treatment candidates 7 plus extracted blood under `runtime:character-medical-treatment`; total catalog 61 and remaining packaging review 28 rows/31 links.
- [x] Added medicine acknowledgement-fault/replay plus exact extracted-blood request/generic-restore focused coverage; runtime and Editor Roslyn compile exit 0.
- [ ] Run the medical fixture, V23 exact-owner gate and packaging double-capture in freshly imported Unity.
- [ ] Run production patient/facility/AI-delivery/cancel/death/whole-save PlayMode for both medicine and extracted blood.
- [ ] Resolve the five remaining packaged medicine unit/BOM/lifecycle contracts and extracted-blood final unit before kg/EWU approval.
- Checkpoint plan SHA-256: `F8758CEEE0BCCFDB637FD4B2DB6830F15982CBE6CACC1EC769E89A090B82090D`.
- Checkpoint baseline SHA-256: `4A9D03788505BA71546CCC60FD44C8337868D607822B5C56158793212C8A9560`.

## 2026-08-25 temporal-stasis maintenance checkpoint

- [x] Removed the callerless duplicate direct age-treatment service and kept surgery as the activation authority.
- [x] Converted seasonal maintenance to one exact two-input rune-conductor + mana-crystal Sink outbox.
- [x] Added CharacterLife v3 provenance, whole-save joins, acknowledgement-fault recovery and no-partial-debit focused coverage.
- [x] Species/runtime/Editor Roslyn compiles and scoped diff check exit 0.
- [ ] Run fresh Unity focused and live temporal-stasis facility scenarios after MCP approval is restored.

## 2026-08-25 generator fuel physical Sink/outbox checkpoint

- [x] Replaced the fuelled generator's count-only facility consume with one exact physical combustion Sink.
- [x] Added PowerInfrastructure current-format v3 node intent/outcome/sequence provenance and exact whole-save pending-receipt joins.
- [x] Preserved FuelSeconds consumption during acknowledgement failure and blocked a second fuel commit until recovery completes.
- [x] Tightened phase-empty provenance and ordinal source-stack validation; corrected the focused JSON fixture to Sink enum value 3.
- [x] Runtime and Editor Roslyn compiles plus scoped diff check exit 0; repository-wide untyped facility consume occurrences dropped 36→35.
- [ ] Run fresh Unity industrial fixture and an acknowledgement-fault/save-restore fuelled-generator PlayMode scenario after MCP approval is restored.
- [ ] Resolve all authored generator fuel unit grams, stack haul bands, combustion outputs/loss, secondsPerFuel, BOM/WU/EWU/price and six-adult power demand.
- Plan SHA-256: `C6C871390E65760605C056607DB55FBF99222E7DF237860E900231024093C85B`.
- Baseline SHA-256: `21F15BAC8A6D8CE51BB83ACA62BCCA5E6982972B40A382FF99669EC344433B24`.
- V27 implementation checklist is now `262/316` checked with `54` explicitly open (`82.9%` row completion). Weighted remaining work is larger than the row ratio because Unity/PlayMode and full authored kg regeneration remain open.

## 2026-08-25 equipment-module appraisal physical Sink/outbox checkpoint

- [x] Replaced count-only material-test coupon deletion with one exact pending physical Sink.
- [x] Added equipment-module item-state v2 appraisal intent/outcome/sequence provenance and deterministic operation identity.
- [x] Made module identification plus inspection-gauge and rune-lens wear exact before/after outcomes recovered before acknowledgement.
- [x] Added physical save owner/receipt joins and rejected orphan or mismatched appraisal receipts.
- [x] Blocked pending appraisal authority from being embedded inside an attached equipment payload.
- [x] Added normal, acknowledgement-fault, current-format restore, receipt-corruption and terminal replay focused source coverage.
- [x] Fixed exact coupon/gauge/lens runtime consumer pairs and removed dead direct-wear and untyped appraisal consume paths.
- [x] Combat/runtime/Editor Roslyn compiles and scoped diff check exit 0; repository-wide untyped facility consume occurrences are now 34.
- [ ] Run Physical Item Contracts and V23 crafting fixtures in fresh Unity and capture Console Warning/Error 0/0.
- [ ] Resolve coupon grams/BOM/WU, destructive-test debris or explicit mass loss, tool lifetime, appraisal EWU/price and haul bands.
- Plan SHA-256: `ABB3AB8D59813780ABE372F1DD34DF98B82C02A03257B3991B1DE162B2B26A50`.
- Baseline SHA-256: `D984CA888F2777F2ED223B3016FAF1F96C7874ACF599286B133576265AF95DB4`.
- V27 implementation checklist is now `274/330` checked with `56` explicitly open (`83.0%` row completion).

## 2026-08-25 regional-supply export Transfer slice

- [x] Replace the regional-contract application adapter's count-only facility-buffer deletion with exact physical `Transfer` selection.
- [x] Persist contract-owned `PhysicalCommitted/RewardPublished` provenance in current-format v2 and validate exact operation/commit/source IDs/quantity/grams.
- [x] Publish contract gold before acknowledgement and recover acknowledgement-only without a second Transfer or income event.
- [x] Add acknowledgement-fault, JSON round-trip and provenance-tamper focused evidence; register it in Physical Item Contracts.
- [x] Compile `DungeonStory.Economy`, current-source `Assembly-CSharp`, and current-source `Assembly-CSharp-Editor` with Roslyn.
- [x] Add plan checkpoint 69 and baseline record `balance:v27:regional-supply-contract-export-transfer-outbox-v1`.
- [ ] Run fresh Unity focused fixtures, Console 0/0 and real delivery PlayMode after MCP approval is restored.
- [ ] Recompute contract quantity/reward/haul/WU/food-reserve balance after final item kg authority is authored.
- Plan SHA-256: `CF00E3A67EE98C3BDD0B1D6B1A5B4BA9BCEE13CC755CD6431423012994EDED08`.
- Baseline SHA-256: `44ADCBA92D2898DC49A0BDAECA32D640A0B7E1260C676928B3193B369EB061E3`.
- V27 implementation checklist is now `287/345` checked with `58` explicitly open (`83.2%` row completion).

## 2026-08-25 regional-supply incoming physical restore cross-join

- [x] Proved that all section candidates stage before any commit, so live physical state cannot validate an incoming regional delivery receipt.
- [x] Added an immutable, stage-scoped physical pending-disposition candidate query on the existing physical runtime singleton.
- [x] Made the physical transactional stage discardable and cleared its candidate view on both discard and commit.
- [x] Split RegionalSupply local preflight from ordered staging and required an exact incoming physical candidate during staging.
- [x] Added bidirectional owner/receipt validation for valid, missing, mismatched and orphan `regional-supply-transfer:*` records.
- [x] Added focused real-runtime stage/discard/commit lifetime coverage and static source ratchets.
- [x] Passed current runtime and focused Editor Roslyn compilation, scoped diff check and GUID uniqueness.
- [ ] Run fresh Unity whole-save/focused fixture/Console evidence after MCP approval is restored.
- Plan SHA-256: `D5DA5DA9B387BC466E2523FBB3FF33214F0E59D2FF683C485AE5F5DF137FAE84`.
- Baseline SHA-256: `39A117D2474EF28A649DA8977EB10A54AD516B55029178937459A643D3B9A9B3`.
- V27 implementation checklist is now `295/354` checked with `59` explicitly open (`83.3%` row completion).

## 2026-08-25 resource stock-policy exact market Transfer outbox

- [x] Replaced generic stock-policy count deletion with exact unreserved FacilityBuffer lot `Transfer` and deterministic sale operation identity.
- [x] Added current-format v2 item-owned pending sales, global monotonic sequence, `PhysicalCommitted/IncomePublished` phases and exact source/quantity/gram/proceeds provenance.
- [x] Made income publication precede acknowledgement and retained acknowledgement-only recovery without a second Transfer or income.
- [x] Added bidirectional incoming Physical candidate joins for valid, missing, mismatched and orphan `stock-policy-sale:*` receipts.
- [x] Added focused acknowledgement-fault/JSON recovery evidence and registered it in Physical Item Contracts.
- [x] Added source ratchets; stock-policy production untyped consume is 0 and repository-wide text occurrences are 33.
- [x] Economy, current-source runtime and Editor Roslyn compiles plus scoped diff/GUID checks pass.
- [x] Added plan checkpoint 71 and baseline record `balance:v27:resource-stock-policy-market-transfer-outbox-v1`.
- [ ] Run fresh Unity focused/economy/whole-save fixtures and Console 0/0 after MCP approval is restored.
- [ ] Run the live AI market-haul/save/restore path and regenerate final kg/threshold/sale ROI/WU/EWU/price/6-adult evidence; unique rejected-quality sales remain separate.
- Plan SHA-256: `DAE97F2C9208F306134CE08AF422CE3260D43061EDDD735CBDCF4426B4C0A966`.
- Baseline SHA-256: `E6CFEE9E1712E02399435938954A1140B5D68C83372CAF5D783BBC39AC7CD08E`.
- V27 implementation checklist is now `304/365` checked with `61` explicitly open (`83.3%` row completion).

## 2026-08-25 callerless crop-treatment mutation removal

- [x] Proved `IPhysicalCropTreatmentService.TryApply` has no production/UI/AI/work-runner caller.
- [x] Removed the tracked dead runtime source/meta and its DI registration without changing authored treatment definitions, recipes or values.
- [x] Added source ratchets so the callerless service/file cannot silently return as live evidence.
- [x] Current-source runtime and Editor Roslyn compiles, scoped diff and deleted-GUID reference audit pass; untyped text occurrences are 32.
- [x] Added plan checkpoint 72 and baseline record `balance:v27:crop-treatment-dead-mutation-removal-v1`.
- [ ] Implement the real plot planner/runner/delivery/WU/Sink+package/ecology-outcome/save/UI-AI vertical slice.
- [ ] Approve treatment item grams, container/waste lifecycle, area/effect cadence, BOM/WU/EWU/price and agriculture closed-loop evidence.
- Plan SHA-256: `A80C22706341F53B247142EA9A2C8AD38C461B9FA05E932861FADE3F5A4921F8`.
- Baseline SHA-256: `01860CD85E8C3B865E00907EA1603A03AA8C4666068232EB5051C394D2DA2F57`.
- V27 implementation checklist is now `308/371` checked with `63` explicitly open (`83.0%` row completion).

## 2026-08-25 facility recalibration restore/replay closure

- [x] Join every pending recalibration material owner to the exact incoming physical Transfer receipt and reject the reverse orphan set.
- [x] Cover acknowledgement failure, JSON restore, acknowledgement-only recovery and terminal second-debit 0 with a real repository fixture.
- [x] Compile Evolution, Runtime and Editor sources and run scoped diff validation.
- [ ] Run the focused fixture and actual recalibration PlayMode in Unity when the Unity MCP server is exposed.
- [ ] Approve catalyst mass and economic values only after the all-item/all-recipe mass audit.
- Next implementation slice: replace `FacilityModificationOrder` multi-material count consumption with one exact atomic batch Transfer-to-WIP outbox.

## 2026-08-25 facility modification batch Transfer closure

- [x] Persist source-level item/stack/quantity, request fingerprint, input grams, operation/commit and outcome phase in Facility Evolution V6.
- [x] Commit binding and optional catalyst as one exact atomic Transfer-to-WIP and remove all untyped FacilityEvolution buffer consumption.
- [x] Add bidirectional restore joins, acknowledgement-fault JSON recovery, terminal replay 0 and missing-input partial-debit 0 focused evidence.
- [x] Repair relocation/recalibration acknowledgement retry liveness and compile Evolution/Runtime/Editor successfully.
- [x] Make production physical disposition/candidate dependencies mandatory `[Inject]` constructor inputs while retaining isolated legacy fixture constructors.
- [ ] Run focused and actual facility-evolution PlayMode/whole-save evidence when Unity MCP is available.
- [ ] Regenerate final facility-evolution kg/WU/EWU/price/ROI after all item semantics are authored.
- Next implementation slice: audit the remaining repository-wide untyped physical removal callsites and select the next reachable high-risk owner.

## Active continuation — physical mass / hauling

- [x] Convert equipment reforge and reattunement materials to exact atomic Transfer-to-WIP ownership.
- [x] Persist and validate current-format material provenance and incoming Physical receipt joins.
- [x] Add acknowledgement-fault, restore replay, equipment-source exclusion, and missing-input focused fixtures.
- [x] Pass Evolution, runtime, and Editor static Roslyn compile gates.
- [ ] Run the registered fixture and focused gameplay path in Unity after MCP approval is restored.
- [ ] Continue with the remaining untyped material debit domains; equipment maintenance is next.

## Equipment maintenance Transfer continuation

- [x] Replace repair-material count deletion with exact split-lot Transfer-to-WIP.
- [x] Persist durability before/after, acknowledgement, and output-release phases in current-format V3.
- [x] Reject WIP cancellation and join pending owners to incoming Physical receipts bidirectionally.
- [x] Add acknowledgement-fault/JSON replay, equipment-source exclusion, missing-input atomicity, and restore mismatch fixtures.
- [x] Pass runtime and Editor Roslyn compile gates and close checkpoint 80.
- [ ] Run Unity focused/PlayMode evidence after MCP approval is restored.
- [ ] Continue with combat equipment crafting material WIP and probabilistic/multi-output completion.

## Combat equipment crafting transaction continuation

- [x] Replace per-item count deletion with one attempt-scoped exact multi-lot Transfer-to-WIP.
- [x] Persist the material receipt, fixed quality/Mythic result and physical output identity in Combat Equipment V7.
- [x] Move generic/unique output publication into the crafting runtime and remove the building handler's post-order spawn gap.
- [x] Add incoming receipt joins, acknowledgement-fault/JSON replay, missing-input atomicity and second-output-0 focused evidence.
- [x] Pass Combat model, Runtime and Editor Roslyn compile gates and close checkpoint 81.
- [ ] Run Unity focused/PlayMode evidence after MCP approval is restored.
- [x] Convert rejected-equipment auto-dismantle to a typed Transfer-to-WIP Transform contract and commit-tagged recovery Source outbox.
- [x] Convert the three DefenseFacility maintenance/supply item count-debit paths and category fallback to exact pending physical transactions.
- [x] Persist Defense Facility V2 physical ownership and validate incoming receipts bidirectionally.
- [x] Add split-source, missing-input, maintenance Sink, acknowledgement-fault and JSON restore focused evidence.
- [x] Pass Defense model, Runtime and Editor Roslyn compile gates and close checkpoint 82.
- [ ] Run the registered defense fixture and actual reload/jam/save PlayMode path after Unity MCP approval is restored.
- [ ] Continue with the remaining four production count-debit callsites; CropEcology is the next bounded owner.
- [x] Replace CropPlot and CertifiedSeed immediate crop-input deletion with exact pending Transfer-to-WIP ownership.
- [x] Persist Crop Plot V4 ecology envelope and Certified Seed V1 fixed output identity.
- [x] Add incoming receipt/output joins, acknowledgement-fault fixture, source ratchets and Runtime/Editor compile evidence.
- [ ] Run the crop fixtures and live P23/P24/greenhouse path in Unity after MCP approval is restored.
- [ ] Close crop facility-destruction WIP disposition and final agricultural grams/buffers/haul/WU/EWU/6-adult loop.
- [ ] Continue with the remaining three production count-debit callsites; ProductionItemGateway is the next bounded owner.
- [x] Replace GrandProject completion count deletion with one exact multi-material pending Sink and durable before/after outcome envelope.
- [x] Upgrade Grand Project save data to current V2 and join the physical owner to incoming Sink receipts bidirectionally.
- [x] Add acknowledgement-fault/JSON replay evidence, source ratchets and Economy/Runtime/Editor compile evidence for checkpoint 84.
- [ ] Execute the GrandProject fixture and live office delivery/completion/save path after Unity MCP approval is restored.
- [ ] Convert the ProductionStockSensor legacy `ConsumeDelivered` path next; then close WorkAmountSystem and FluidNetwork count debits.
- [x] Convert ProductionStockSensor installation to exact pending Sink ownership and persist it in Production V14.
- [x] Remove the now-unused production `ConsumeDelivered` API and its last `ProductionItemGateway.TryConsumeFacilityItemBuffer` implementation.
- [x] Add stock-sensor incoming receipt joins, acknowledgement-fault evidence, source ratchets and Production/Economy/Runtime/Editor compile evidence.
- [x] Convert both FluidNetwork manual/container-feed count/category debits to exact pending Transfer ownership with Fluid V6 restore joins.
- [x] Convert the final remaining semantic count debit in `WorkAmountSystem.EnsureMaterialsReady`.
- [x] Add deterministic physical output ownership for stock-sensor removal.

## 2026-08-25 fluid manual/container-feed exact Transfer checkpoint 86

- [x] Replace manual-water item count deletion and automatic Water-category deletion with exact clean-water FacilityBuffer Transfer transactions.
- [x] Persist immediate/feed operation sequence, fingerprint, commit, source, input grams and outcome phase in current Fluid V6 state.
- [x] Make reserve/network outcome publication precede acknowledgement and recover acknowledgement-only without a second debit or outcome.
- [x] Add bidirectional incoming Physical receipt joins, strict local candidate validation and missing/orphan/mismatch rejection.
- [x] Add real-repository acknowledgement-fault evidence and source ratchets for both legacy Fluid debit APIs.
- [x] Pass Infrastructure, current-source Runtime and Editor Roslyn compilation plus scoped diff validation.
- [ ] Run fresh Unity focused/whole-save fixtures and Console 0/0 after Editor MCP approval is restored.
- [ ] Close empty-container/tare lifecycle and regenerate final water grams, buffer/haul, WU/EWU/price and six-adult water-loop evidence.
- [ ] Convert the final `WorkAmountSystem` semantic count debit next.

Errors encountered and resolved:

- The first Infrastructure compile exposed an invalid Infrastructure→save-DTO dependency for pending container feed; the runtime state was separated from the save DTO and mapped only at the serialization boundary.
- The first Runtime compile used a stale Infrastructure assembly and then exposed one outdated `FluidNetworkRuntime` construction in `FluidNetworkBatchDebugContract`; Infrastructure was rebuilt and the focused constructor was updated.
- A helper-name typo in save validation and two stale Windows search paths were corrected; no source mutation resulted from the rejected searches.

## 2026-08-25 construction material Transfer-to-WIP checkpoint 87

- [x] Audit WorkAmount construction delivery, cancellation, placement failure and debug completion ownership boundaries.
- [x] Replace the final semantic count debit with one exact multi-BOM pending Transfer and persist Work Order V6 source/fingerprint/commit/gram provenance.
- [x] Publish delivered custody before acknowledgement and recover acknowledgement-only without a second material debit.
- [x] Add bidirectional incoming Physical receipt joins and strict authored-BOM/source validation.
- [x] Replace untyped cancellation deletion with deterministic exact-mass restitution and retain the order while output space is unavailable.
- [x] Gate ConstructionSite cancellation and work-order completion so failed restitution or placement cannot orphan material custody.
- [x] Add acknowledgement-fault, V6 JSON, missing/orphan join, single-commit and exact restitution focused fixture source plus runtime ratchets.
- [x] Pass current-source Runtime and Editor Roslyn compilation and confirm WorkAmount production semantic count-debit callers are 0.
- [ ] Execute focused Work Amount/Physical Item fixtures and actual AI construction/cancellation whole-save PlayMode after Unity MCP approval is restored.
- [x] Add partial multi-output restitution incoming-stack preflight and atomic restore evidence.
- [ ] Regenerate construction item grams, site FacilityBuffer capacity, 25kg haul, WU/EWU/price/teardown/space and six-adult growth evidence.

Errors encountered and resolved:

- The first Editor compile of the focused fixture could not access the internal restitution operation prefix. An Editor-only public debug contract now exposes only the deterministic operation ID and receipt constructor required by fixtures.
- One Windows `rg --glob` invocation was parsed as paths and failed without source mutation; subsequent scoped searches use explicit roots and PowerShell filtering.

- Plan SHA-256: `0621ACEB37E574FCE15C1079A53A1877A1D4D13C8A99C2DDD9DFA89ECFBB6FA6`.
- Baseline SHA-256: `B8D5D1A6BEBC851FE6D1AAE24532F1B91AA8ABEA684C749F42452BB78B6F2196`.
- Checklist: `463/571` checked, `108` open, `81.1%` row completion.

## 2026-08-25 stock-sensor removal Source output checkpoint 88

- [x] Persist installed stock-sensor input operation/commit/source/embedded grams and upgrade Production to current V15.
- [x] Add Prepared/OutputPublished removal ownership that retains installed state across output-space failure.
- [x] Publish one deterministic Loose Source through the physical source service and require output grams to equal installed input grams.
- [x] Key removal operation by the installation source stack so repeated install/remove cycles cannot reuse a past output commit.
- [x] Add detached committed-output candidate indexing and bind it in production composition.
- [x] Preflight pending stock-sensor removal and partial construction restitution outputs against incoming physical stacks.
- [x] Add V15 cross-owner validation, output fault/missing-output fixtures and source ratchets.
- [x] Pass Items, Production, Economy, Runtime and Editor Roslyn compilation plus scoped diff and GUID validation.
- [ ] Run fresh Production/Physical Item/Work Amount scenarios and actual repeated install/remove haul PlayMode after Unity MCP approval is restored.
- [ ] Approve final sensor grams, buffers, 25kg haul, WU/EWU/price/repair/ROI and six-adult growth evidence.

Errors encountered and resolved:

- Runtime compilation initially referenced stale Production/Economy asmdef DLLs; the owned asmdefs were rebuilt first and the current Economy DLL was copied to the existing Codex static-compile reference path before rebuilding Runtime/Editor.
- The first Editor compile exposed six target-typed `ProductionBillsSaveSection` fixtures and a `BuildableObject.Position` typo; all constructors now provide the output candidate query and the fixture uses the captured facility handle position.
- Work-order output preflight initially used nonexistent `positionX/positionY` fields; it now uses the authoritative `gridX/gridY` save coordinates.

- Plan SHA-256: `AB06045E89BB5ABE7B770404A4EF522CB5021CB8921A570560093FFE5DDF29C0`.
- Baseline SHA-256: `202502CB0FA2A22B2F729A6DB36C23DE41F965613A8E79ADFB38600970B9FCDC`.
- Checklist: `480/589` checked, `109` open, `81.5%` row completion.

## 2026-08-25 crop-plot destruction WIP terminal-loss checkpoint 89

- [x] Audit the destroyed-plot branch that retained committed sow WIP without a terminal disposition.
- [x] Define already-transferred seed/water/compost/fuel as typed `DestroyedWithPlotLoss` rather than teleporting it to source storage.
- [x] Upgrade Crop Plot current format to V5 with exact terminal quantity/grams and last-known grid coordinates.
- [x] Persist the loss owner before acknowledging the original pending Transfer and resume acknowledgement-only after fault/restore.
- [x] Remove the crop ecology owner and plot state only after the receipt reaches its terminal state.
- [x] Validate empty/input/outcome/destroyed phases separately and reject terminal operation/reason/quantity/mass drift.
- [x] Extend the real-repository crop fixture with acknowledgement failure, serialized loss replay, incoming join and tampered-mass rejection.
- [x] Strengthen runtime authority ratchets for V5, destroyed loss, ecology cleanup and focused evidence.
- [x] Pass current-source Economy, Runtime and Editor Roslyn compilation plus scoped diff validation.
- [ ] Execute the focused fixture and real destruction/save-restore PlayMode after Unity MCP approval is restored.
- [ ] Recalculate final crop-input grams, buffer/haul, WU/EWU/price and six-adult agricultural loss budget.

Errors encountered and resolved:

- The first ecology patch used a one-line fungicide body that no longer matched current source; the patch was re-anchored to the current block implementation without mutating unrelated code.
- Destroyed states originally had no durable position. V5 now captures `lastKnownGridX/Y`, preventing destination release at `(0,0)` after restore.

- Plan SHA-256: `EFC41C402E3DE7CA03A78B5131F6CFCC700FF58AE271B3CC847F4876A61B9096`.
- Baseline SHA-256: `86A2D90D6A3B1A3C96B05BD2346289220305E686A2A6C9738EA08D2B36B6A8DC`.
- Checklist: `496/607` checked, `111` open, `81.7%` row completion.
# Crop treatment vertical-slice structure contract (active)

| Gate | Contract |
|---|---|
| Content authority | `ResourceItemDefinitionSO` plus `CropTreatmentItemFeature` is the immutable treatment definition. The feature owns kind, per-application quantity, direct WU, ecology reduction and cooldown; the builder authors all three real definitions. |
| Runtime authority | The owning `CropPlotState` is the sole mutable treatment-order authority. Ecology remains solely owned by `ICropEcologyService`; item custody remains solely owned by the physical item repository. |
| Command | `ICropPlotRuntime.TryScheduleTreatment` and `TryCancelTreatment` are the only player scheduling mutations. AI only executes a persisted order through the existing Treat work type. |
| Query/observation | `CropPlotSnapshot` exposes the active treatment, delivery/progress/pressure/cooldown state. The existing crop building panel schedules/cancels and displays it; the work policy reads `TryGetWork(Treat)`. |
| Identity | Plot ID remains `BuildingInstanceId`; treatment operation is `crop-treatment:{plotId}:{sequence:D8}` and destination is `{plot sow destination}:treatment`. Item IDs come from canonical catalog definitions. |
| Save | Crop Plot current format advances V5->V7. V6 added the order phase, fixed authored policy snapshot, exact receipt/mass/source/tare summary, ecology before/after fingerprints, cooldown days and sequence. V7 adds explicit `hasSeedLot` because Unity `JsonUtility` serializes a null nested seed-lot as an empty object. Snapshot/query caches are recomputed. No old-save migration is added. |
| Dependencies | `CropPlotRuntime` composes `IPhysicalFacilityItemSinkGateway` and `IPackagedLotTareDispositionService`; `SurvivalWorkExecutionHandler` delegates crop-plot Treat targets to `ICropPlotRuntime`. No Items->Economy reverse reference is introduced. |
| Failure policy | Missing/duplicate definitions, noncanonical IDs, receipt mismatch, ecology fingerprint conflict, missing restore counterpart and impossible package output fail loudly. Pre-commit cancel releases the treatment destination physically; post-commit cancellation is rejected. |
| Transition | The prior callerless direct-treatment service remains deleted. Four crop facility assets/builders gain the existing Treat bit; no new serialized flag bit or prefab field is introduced. |
| Verification | Exact Sink/tare/outcome/ack fault, JSON restore, missing/orphan/mismatch joins, cancel/release, cooldown and no-double-effect fixtures; Economy/Runtime/Editor compile; Unity fixture/Console when available. |

Provisional structural authoring (balance status: basis assigned, not approved): pest lure `1 unit / 3 WU / -15 pest / 1 day`; botanical pesticide `1 / 5 WU / -35 pest / 2 days`; fungicide `1 / 5 WU / -25 disease / 1 day`. Final grams, package lifecycle, WU/EWU/price and six-adult/multi-seed evidence remain open.

## Crop treatment vertical-slice checkpoint 90

- [x] Implement authored treatment policy, UI schedule/cancel, exact treatment destination and persistent Treat WU.
- [x] Implement exact physical Sink, package-tare call, ecology before/after, cooldown and acknowledgement-only recovery.
- [x] Add incoming receipt reverse join, mismatch rejection and destroyed-plot terminal loss.
- [x] Add real-repository focused evidence for second Sink 0, acknowledgement fault, JSON replay, orphan/mismatch and tamper rejection.
- [x] Add Crop Plot V7 `hasSeedLot` discriminator after the live self-save round-trip exposed Unity null nested-object materialization.
- [x] Pass Economy, Runtime and Editor static compilation, Unity physical fixture repetition, Crop Plot PlayMode report `valid=true` and Console `0/0`.
- [ ] Finalize three treatment item grams, package/container/residue lifecycle and production recipe conservation.
- [ ] Regenerate agricultural WU/EWU/price, buffer/haul, six-adult loop and multi-seed treatment outcomes.

Next bounded work: use the restored Unity MCP connection to execute the backlog of already-authored focused fixtures, starting with checkpoint 74/75 small fixtures, while separately keeping the large 51-item semantic and 355-recipe mass audit open.

- Plan SHA-256 after checkpoint 90: `CEB7A7B30BD6A575911A359B3E75CF1D9124A82DA4335FE73D6CE5954DBA3E73`.
- Baseline SHA-256 after checkpoint 90: `D5B8BBD55E9581EF240DB3D306D16BE4EA296E68F1B2BE2BF58A3B5F38ACFA1C`.
- Checklist after checkpoint 90: `499/607` checked, `108` open, `82.2%` row completion.

## Unity focused backlog checkpoint 91

- [x] Execute accord signal exact Sink/restore/acknowledgement fault fixture in Unity.
- [x] Execute organ-preservation canister exact Sink/restore/acknowledgement fault fixture in Unity.
- [x] Keep the wider combat/storage PlayMode and final authored kg/economy rows open rather than overclaiming focused evidence.
- Plan SHA-256: `DA87E90EFDF476CF6B5B624899A40FA51E83DE8E46BDBAFF859479B42FD61FFA`.
- Baseline SHA-256: `D5B8BBD55E9581EF240DB3D306D16BE4EA296E68F1B2BE2BF58A3B5F38ACFA1C`.
- Checklist: `501/607` checked, `106` open, `82.5%` row completion.

## Unity facility-evolution backlog checkpoint 92

- [x] Run relocation, recalibration and atomic modification physical fixtures in Unity.
- [x] Run the full Facility Evolution scenario suite and keep Console Warning/Error at `0/0`.
- [x] Close only the narrowly proven relocation fixture row; preserve live delivery/work/whole-save rows as open.
- Plan SHA-256: `ABE954DDB190F976BE80F7EDD46041E14741ED854733AE886A2A19F278124A91`.
- Baseline SHA-256: `D5B8BBD55E9581EF240DB3D306D16BE4EA296E68F1B2BE2BF58A3B5F38ACFA1C`.
- Checklist: `502/607` checked, `105` open, `82.7%` row completion.

## Unity strict combat/defense checkpoint 93

- [x] Execute Strict Progression Combat Save in Unity and isolate its failing subfixture.
- [x] Repair the production generic-output prepared-owner contract found by the fixture.
- [x] Recompile Runtime/Editor and pass strict combat plus P1 Defense suites with Console `0/0`.
- [x] Close only checkpoints 79/80 focused execution rows; keep live workbench/defense hauling rows open.
- Plan SHA-256: `69D4CF844944952ECDE7F098E7255F732CCDC9EA581A693607D40CB7F71B0633`.
- Baseline SHA-256: `9F739ABAF8EEEAC6F2F95528964550D1133CC419C8EE1A9A46887B999E8F4194`.
- Checklist: `504/607` checked, `103` open, `83.0%` row completion.

## Unity production/items/work backlog checkpoint 94

- [x] Re-run `ProductionEconomyDebugScenarios.RunAll()` after making `GrandProjectPhysicalInputReceipt.IsCommitted` safe for the default readonly-struct value.
- [x] Repair and re-run the full `PhysicalItemDebugScenarios.RunAll()` suite. The first fresh run failed in exactly two strict-authority fixtures: a stale non-`ItemInstanceId` module fixture ID and an appraisal negative-test host left in `MaintenanceBuffer` without a physical stack reference.
- [x] Run `WorkAmountDebugScenarios.RunAll(true)` after Physical Item is green.
- First Work Amount run reached the strict restore validator and failed because the fixture's `FakeWorldItemStackRuntime.CatalogProvider` was null; therefore even the real authored `material:lumber` could not be proven to exist. The fixture must compose the authored Editor catalog rather than weakening restore validation.
- [x] Record focused Unity/Console evidence and close only the matching V27 plan rows.

Resolved evidence:

- Physical Item now uses a canonical `item-instance:*` module fixture ID and gives the maintenance-buffer codec-negative host its real FacilityBuffer stack; the strict production validators were not relaxed.
- Work Amount's fake world now composes the authored Editor item catalog, so current-format restore verifies `material:lumber` through the production definition authority.
- Production Economy, Physical Item and Work Amount full focused suites PASS after fresh Unity import; Console Warning/Error is `0/0`.
- Plan checkpoints 84, 85, 87 and 88 each close exactly one Unity-focused row. Live AI/PlayMode and final authored mass/economy rows remain open.
- Plan SHA-256: `01E7E8218E29677114371F09F20F945FDEE44DF8779978D65EC016C5437BE773`.
- Baseline SHA-256: `B7EEB640951813E28597787D37A587580B57CD08DD3F10287D36B845F785EE3B`.
- Checklist: `508/607` checked, `99` open, `83.7%` row completion.

## Unity industrial process-fluid authoring checkpoint 95

- [x] Run the Industrial Infrastructure suite after the Physical Item suite became green.
- [x] Isolate the pre-fluid-contract failure: Surgery builder `ReplaceAbilities` removed the Industrial builder's clean-water/wastewater/process-fluid/plumbing overlay from medical facilities.
- [x] Extract the existing Industrial process-fluid overlay as a reusable authoring method and invoke it from the Surgery builder after its authoritative ability replacement.
- [x] Rebuild only authored medical facility assets, rerun Industrial Infrastructure, verify no-op second rebuild and Console `0/0`.
- [x] Close checkpoint 86's focused Unity row and update baseline evidence only after all gates pass.

Errors encountered:

- The first Industrial suite stopped at `응급 처치대 is missing process fluid settings`; this was an actual cross-builder asset drift, not a reason to weaken the verifier.
- A dynamic read-only audit command could not compile because the command assembly lacked the Sirenix serialization reference required by `BuildingSO`; source/asset/builder inspection was used instead and the failed command made no project mutation.
- A first attempt to invoke the service-room regression used the nonexistent `RunAll` entrypoint. Source inspection found the authoritative `Run()` entrypoint; that command then compiled and passed.

Resolved evidence:

- Surgery authoring now replaces only medical-owned ability types and preserves cross-domain instances; existing assets also retain external `unlocked` authority.
- Industrial process-fluid and Service Room medical hub overlays update existing abilities in place, so managed-reference identities and ordering do not churn.
- M01–M13 were rebuilt twice; all 13 SHA-256 hashes were byte-identical between the first and second build.
- `IndustrialInfrastructureDebugScenarios.RunAll()` and `ServiceRoomDebugScenarios.Run()` PASS; Console Warning/Error is `0/0`.
- M01 retains `unlocked=1`, its Direct medical service hub, CleanWater|Wastewater channels and the surgery `0.2/0.2` process-fluid contract simultaneously.
- Plan checkpoint 86's focused Unity row is closed. Live AI water hauling, package tare/container lifecycle and final water kg/WU/EWU/price/six-adult rows remain open.
- Plan SHA-256: `4708F0201D165196DDFBF600F01C0DDF8859704DD46E5F02C094F5CB1D4FF8F4`.
- Baseline SHA-256: `81376571EF06DD30FA636A115C104B81F387E69A888B6E3A26B30C948571E149`.
- Checklist: `509/607` checked, `98` open, `83.9%` row completion.

Next bounded work: use the connected Unity editor to recapture the authored mass-semantic and recipe inventories, establish the exact current missing rows, and close only deterministic inventory gates before changing final item kg values.

## Unity packaged consumable fail-closed checkpoint 97

- [x] Update the baseline records that still described MCP as unavailable with the exact focused Unity evidence now obtained for disease response, expedition supply, urgent mitigation, vaccination and character medical supplies.
- [x] Add an injectable catalog boundary to the physical-item cross-domain fixture without changing the production catalog or mass query.
- [x] Exercise a packaged substance with `IPackagedLotTareDispositionService` intentionally absent and require the effects-published plan plus physical pending receipt to survive repeated retries.
- [x] Restore the same current-format payload with the production tare service/gateway, publish exactly one reusable container, acknowledge the Sink and prove replay creates no second container or gameplay result.
- [x] Run the full Survival and Physical Item focused suites in Unity and require Console Warning/Error `0/0` plus scoped diff check success.
- Plan SHA-256: `5A117524A17162627D5574583F2F5E96A9FD5AC0C0F225053C8290B26517EE73`.
- Baseline SHA-256: `11D739F8EF1373B23936B37CE345ED866660FDF880CD0A31C4132FD74796D74D`.
- Checklist: `535/607` checked, `72` open, `88.1%` row completion.

Next bounded work: proceed to the first still-open runtime-consumer/WIP checkpoint whose focused transaction exists but live PlayMode evidence is missing; do not use the focused tare fixture as proof of unrelated final kg, recipe conservation, EWU or six-adult balance.

## Unity physical-mass inventory recapture checkpoint 96

- [x] Run current authority inventory and reject unknown physical-mass writers.
- [x] Classify the two newly detected focused fixtures as Editor-only test writers without broadening production authority.
- [x] Double-capture explicit semantics, recipe mass inventory and remaining packaging review from live Unity catalogs.
- [x] Repeat the complete artifact sequence and require byte-identical SHA-256 plus Console `0/0`.
- [x] Close only the deterministic recapture checklist rows; keep all item-level package, recipe Critical, final kg and economy rows open.

Errors encountered:

- The first fresh authority capture correctly failed on `PhysicalVaccinationOutboxDebugScenarios.cs` and `CharacterMedicalSupplyOutboxDebugScenarios.cs` as unknown mass writers. Both only use fixed-gram fake queries inside Editor fixtures, so they were registered as `editor-test-writer`; no asset or production writer authority was added.

Resolved evidence:

- Authority inventory: ledger items `414`, serialized sites `1,074`, recipes `355`, equipment `61`, unknown writers `0`, asset mutations `0`.
- Explicit semantics: `363/414`, missing `51`, profiles `51`, reviewed transforms `38`, duplicate/out-of-ledger/haul-class failures `0`.
- Recipe inventory: source/transform/sink `23/328/4`, reviewed `38`, missing disposition `159`, mass-creation Critical `84`, candidates `126`, missing-semantic recipes `47`, role-shape mismatch `0`.
- Packaging review: exact remaining `51`, authored package feature `0`, runtime consumer rows/links `28/31`, execution orphan `0`.
- The complete 12-artifact sequence was run twice; every SHA-256 remained identical and Console Warning/Error was `0/0`.
- Plan SHA-256: `1E0059ABB1C814736985D12AA92B4842EA5862655C2B3FB55D9A0972470C7786`.
- Baseline SHA-256: `A3661FF610F5CB1009E85677186F557A47F0749F31E740A0E31A749F6A7A7784`.
- Checklist: `525/607` checked, `82` open, `86.5%` row completion.

Next bounded work: execute the already-authored packaging/tare and runtime-consumer focused fixtures that were left pending only because Unity MCP was unavailable, then use their evidence to close narrowly matching rows before changing package semantics or final kg.

## Unity generator fuel outbox checkpoint 98

- [x] Keep the internal industrial topology private by placing an Editor-only contract probe in the runtime assembly instead of widening production API visibility.
- [x] Build a real fuelled `BuildableObject`, exact `power:{nodeId}` FacilityBuffer coal stack and production physical Sink gateway.
- [x] Inject the first acknowledgement failure, retain `OutcomePublished` plus the pending physical receipt, restore the current-format power owner and finish acknowledgement without a second debit.
- [x] Advance simulation time after recovery and prove `FuelSeconds` decreases from `120` to `110` while the source remains debited exactly once.
- [x] Run the focused and full Industrial Infrastructure suites after fresh Unity import and require Console Warning/Error `0/0` plus scoped diff-check success.
- Checklist after closure: `538/607` checked, `69` open (`88.6%`).
- Plan SHA-256: `E379E5D268E75C757168B3C7068DF3C932065F4E7D8915C496820E42F7FBB3C0`.
- Baseline SHA-256: `CE5200E6ADBCD6CCAE7EE671527C5C456B87947728D8246E1FB5FAFD5F98A0EF`.

Next bounded work: select the next open runtime consumer/WIP checkpoint with the smallest missing production boundary, add only the evidence that its exact checklist text requires, and keep final authored fuel kg/economy and six-adult closure open.

## Whole-save physical candidate transaction checkpoint 99

- [x] Reproduce the real Surgery PlayMode whole-save failure where candidate-dependent sections ran during DTO preflight before Items staging.
- [x] Split DTO-local preflight from candidate-dependent staging for production bills, grand projects, Circus, Surgery and RunMilestones without weakening their exact physical joins.
- [x] Extend the Physical Items candidate index through transaction-participant publication and clear it only on complete, rollback, discard or pre-commit stage discard.
- [x] Reset crop, defense and combat material guard active state during rollback so a failed restore does not poison the next restore attempt.
- [x] Update the candidate-lifetime focused fixture and runtime authority source gate to the transaction-scoped contract.
- [x] Run Physical Items, regional supply, production economy, Circus, V20 campaign, Surgery, temporal-stasis and strategic save-registry suites.
- [x] Re-run the actual Surgery PlayMode verifier and require mid-procedure current-format whole-save restore, exact AI resume, physical medicine/process-water conservation, packaged vial return/reuse, `RESULT=PASS` and Console Warning/Error `0/0`.
- Checklist after evidence closure: `540/607` checked, `67` open (`89.0%`).
- Plan SHA-256: `D24A049A6ECDEF8275CA12C3227D919B2DB1C55CA96FC973BC088732E9C92CD3`.
- Baseline SHA-256: `A70FCF6042A2DD905BF73B733E95F3F53A9900C0C84097784DD0A0FD7165DD9B`.

Next bounded work: build an actual pending-regional-delivery whole-save PlayMode fixture or move to the next open live WIP route; do not treat the generic Surgery whole-save as proof of a pending regional owner/receipt join.

## Pending regional delivery whole-save checkpoint 100

- [x] Add a narrow cross-domain Physical Items fixture overload that injects one shared `DungeonRuntimeAggregateRootStore` without exposing production repository internals.
- [x] Build a real `DungeonSaveSectionRegistry` payload containing both a Regional Supply pending owner and its exact Physical Items Transfer receipt.
- [x] Restore the valid payload and preserve the pending owner, source remainder `1` and transaction-scoped candidate lifetime.
- [x] Reject missing receipt, owner mass mismatch and orphan incoming receipt before live aggregate publication, with candidate-index leak `0` after every path.
- [x] Run the same matrix while `EditorApplication.isPlaying=true`, then run Physical Items, Production Economy and Dungeon Save Section regressions.
- [x] Require Unity compile success, `PLAYMODE=1` PASS, Console Warning/Error `0/0` and scoped diff-check error `0`.
- Checklist after evidence closure: `541/607` checked, `66` open (`89.1%`).
- Plan SHA-256: `66756929F2AEA770E0C94FD03E40940669B6AC00207E023A02CDB0F29571F490`.
- Baseline SHA-256: `E9215ED4F782C9F8C22A4E6DE591EBB1174A4F75711C48A78449B426BDA7332C`.

Next bounded work: proceed to the next open live runtime/WIP route; keep final authored contract kg, haul count, reward, EWU and six-adult economy closure open.

## Dog-food exact mass vertical slice checkpoint 101

- [x] Audit the two dog-food recipes and confirm both produced `+50g` per cycle at the prior `550g` unit authority.
- [x] Set the count-preserving unit authority to `525g` and add builder topology/no-clobber guards.
- [x] Register both recipes as reviewed exact `1,050g → 1,050g` transforms with no package tare or process loss.
- [x] Apply the targeted item asset, compile Unity, and run semantic/recipe audits twice with byte-identical artifacts.
- [x] Run the Captivity/Circus exact feed Sink regression and require Console Warning/Error `0/0`.
- [x] Record the completed slice and its still-open downstream gates in the V27 plan and whole-game baseline.

Errors encountered:

- One Windows `rg` command used a wildcard as a literal path and failed before reading files; the corrected command used explicit files. No project file changed.
- One combined `apply_patch` missed a report anchor and failed atomically; the same changes were applied in smaller verified patches. No partial edit remained from the failed patch.
- The first Unity dynamic command referenced a Sirenix-backed item type unavailable to the dynamic compilation context. It failed compilation before mutation; the replacement used `SerializedObject` against the exact asset path.

Resolved evidence:

- Dog-food unit mass is `525g`; both animal-rot and fresh-meat recipes are exact `1,050g` input/output transforms.
- Reviewed transforms are `41`; mass-creation Critical rows are `81`; missing disposition remains `159` because these two rows moved from mass-creation, not disposition-missing.
- Six generated semantic/transform/recipe artifacts are SHA-256 identical across consecutive runs.
- Captivity/Circus typed feed Sink regression PASS; Unity Console Warning/Error `0/0`.
- Checklist is now `554/622` checked, `68` open (`89.1%` surface completion). Weighted implementation effort remains approximately `35–45%` because the open rows contain warehouse gram admission, WIP/restore, final kg-aware economy and multi-seed gates.
- Plan SHA-256: `F4DA97CADB39D16CBDA57E7CC9991771A55FF5409E925F32B2743F185496EA4D`.
- Baseline SHA-256: `8F02C6EDEFF7E3338D4868135ED8173A5E6ED6A9A70AB108F9774C13B1E9DF5B`.

Next bounded work: bedding triage found incompatible input masses and requires a broader authored-yield decision, so do not invent a large loss. Implement the independent inoculated-log `1,400g → 2×700g` slice first, then return to bedding with both recipe pathways modeled together. Defer the expensive final 3-seed coverage run until the current source batch is stable.

Inoculated-log implementation error log:

- The first multi-file patch failed atomically because the report-chain anchor included indentation that did not match the current file. No file changed. Continue with small file-local patches and exact anchors; do not repeat the combined patch.
- The full builder no-clobber gate reached `research-overhaul` and failed because that existing builder changed RF03 and RF93. The command partially mutated exactly those two authored assets. Inspect their exact diff and restore only the builder-induced changes before retrying with a corrected cause; do not treat this as an inoculated-log mass failure or repeat the same command unchanged.
- One cross-directory `rg` command included the nonexistent path `Assets/Scripts/Services/Economy/Crops` and reported a path error while still returning matches from the valid directories. No file changed; subsequent searches use the actual runtime path under `Assets/Scripts/Services/Economy`.

Inoculated-log resolved evidence:

- [x] Applied `supply:inoculated-log 1,800g → 700g` and preserved the existing two-output recipe.
- [x] Added exact `1,400g → 2×700g` no-loss transform, semantic correction, builder no-clobber override and topology guard.
- [x] Repeated six mass artifacts with byte-identical SHA-256; current reviewed/mass-creation counts are `42/80`.
- [x] Passed the common crop physical transaction/restore fixture.
- [x] Extended the real crop PlayMode verifier to RF13 and proved `700g×1` physical consumption followed by `Growing`.
- [x] Resolved the adjacent RF03/RF93 `Treat` source/asset drift without RID churn; full five-builder/7,219-file no-clobber PASS.
- [x] Recorded plan checkpoint 92 and baseline record `balance:v27:inoculated-log-section-mass-conservation-v1`.
- Checklist after closure: `564/632` checked, `68` open (`89.2%`).
- Plan SHA-256: `3FAFD18685DC6B7A4819BA86AC1970DA9F7204D6D9F2407D2BD23510B4917EA7`.
- Baseline SHA-256: `67C8C2A65B876FA3BB9CF582A73A3FB1C90ABA02852D09146C0EC0A858A3753F`.

Next bounded work: do not force bedding without a consumer/yield decision. Implement the already-audited L02 positive gram-capacity warehouse slice, use inoculated-log as one representative ordinary item, and keep FacilityBuffer gram admission as the immediately following distinct slice.

L02 implementation error log:

- The first wrapper insertion patch targeted `private static T Resolve<T>(...)` without the current `where T : class` suffix, so `apply_patch` rejected it atomically and no file changed. Reapply against the exact current signature; do not repeat the stale anchor.
- The first full physical-logistics PlayMode run proved every new L02 gate, but the pre-existing broad scenario ended `RESULT=FAIL; failures=18` in craft-output hauling, expedition packing and official restore of a transient construction order. Captured Console errors/warnings were still `0/0`. Do not claim the broad suite is green or rerun it unchanged; add an L02-focused request/report using the same runtime, and retain the 18 unrelated failures as open regression work.
- The first documentation-log patch contained an empty `progress.md` update hunk, so `apply_patch` rejected the whole patch atomically and no file changed. Split task/progress updates into valid file-local hunks; do not repeat the empty hunk.
- The first PowerShell census for the 19-storage rollout piped directly from a completed `foreach` statement and hit `ParserError: An empty pipe element is not allowed`; it was read-only and changed no file. The corrected census assigns the loop output to an array before formatting; do not repeat the invalid pipeline form.
- The first fresh five-builder no-clobber run after the 19-storage rollout reached the `surgery` step and changed exactly `M08_장기보관함.asset`, then failed. This is a real regeneration drift and the builder partially mutated M08; do not rerun unchanged. Inspect the exact M08 diff, separate the intended 12,500g scalar from pre-existing RID/ability-order churn, align source/asset once, and only then recapture evidence.
- The first dynamic M08 diagnostic could not compile because the Unity Editor was still exposing the pre-change `Assembly-CSharp-Editor` (`SurgeryContentAssetBuilder` had no new constant), and the dynamic compiler also lacks the Sirenix reference needed for direct `BuildingSO` access. This proves the earlier refresh command returned before the project assembly/domain reload completed; those focused calls executed stale scenario code and cannot be counted. Do not repeat direct Sirenix asset access in a dynamic command. Wait for a fresh project assembly timestamp/domain reload, then invoke only public scenario entry points.
- The follow-up diagnostic attempted `System.Reflection` to avoid the Sirenix type, but Unity RunCommand rejected that namespace before compilation as unauthorized. It changed no project state. Do not repeat reflection through RunCommand; use filesystem assembly/source timestamps plus public project scenario entry points instead.
- The first explicit compilation request used an imported `UnityEditor.Compilation` namespace, but the dynamic wrapper resolved `CompilationPipeline` incorrectly as `Unity.CompilationPipeline` and failed before execution. No project file changed. Retry with the fully qualified `UnityEditor.Compilation.CompilationPipeline` API rather than the ambiguous imported symbol.
- The first real forced project compilation failed at `PhysicalStockQueryV18DebugScenarios.cs(176,65)` because replacing the two local L01/L02 ratchets with the 21-row helper also removed the `l02` local still consumed by the existing exact admission fixture. The stale DLL therefore remained unchanged and the Unity console MCP did not surface the Bee error; `Editor.log` did. Restore only the L02 local after the table verification, then force-import/recompile and confirm the DLL timestamp advances.
- The first exact-scope diff check after fresh PASS found one Surgery-writer YAML trailing space at M08 `minimumSkillId:`; all code and the other 20 storage assets were clean. Remove only that whitespace, then regenerate the byte-digest/no-clobber artifact because the aggregate asset hash becomes stale.

L02 resolved evidence:

- [x] Authored L02 as `12,500g` from the reviewed L01 `25,000g/2-cell` density, while preserving General/restricted policy and legacy count metadata `16`.
- [x] Proved legacy count does not clamp positive-gram admission: exact `700g` inoculated logs admit 17 units (`11,900g`) and reject the next unit with `600g` remaining.
- [x] Proved admission token/reserved grams/physical publication/commit receipt and current-format restore preserve exact stack identity, quantity and mass.
- [x] Proved real `Facility.Initialization` projects the asset authority into `WarehouseInventory`.
- [x] Added focused PlayMode production ingress and isolated real-planner rejection-before-pickup; report `RESULT=PASS`, captured Console Error/Warning `0/0`.
- [x] Re-ran all five content builders across 7,219 files; `changes=0`.
- [x] Added plan checkpoint 93 and baseline record `balance:v27:l02-mass-authoritative-general-warehouse-v1`.
- Checklist after closure: `576/647` checked, `71` open (`89.0%` surface). Open count rose by three because the new checkpoint explicitly records the 19-facility rollout, FacilityBuffer/OutputBuffer and broad-regression debt instead of hiding them.
- Plan SHA-256: `75ECE55A45B97269ED95A372E2546B3E0449ED05B1E012A54FC8BC34182AF5A9`.
- Baseline SHA-256: `F33D633F54B4DE64BEFE3A43972BFBB4EAA98F34FE1249AC3CF7DBFAD4BE1676`.
- Focused L02 artifact SHA-256: `5FA0DFDAA9E98DEB25EADB11B7E127554CBBE5F5D406A515D63F96A9191D1D9A`.

Remaining 19-storage rollout resolved evidence:

- [x] Applied durable positive gram writer authority and serialized values to every remaining Modular, M08 and P1 storage definition; exact global result is `21/21` positive-count and positive-gram.
- [x] Preserved existing footprint, count, category, all-category policy, managed-reference ownership and save schema. Q03 archive/category semantics remain an explicit separate gate.
- [x] Added exact 21-row source-writer/asset census, all-Modular runtime projection, exact M08 and P1 policy assertions.
- [x] Forced a real current-source Unity compile; `Assembly-CSharp-Editor.dll` advanced to `2026-08-25T04:25:15Z / 8,096,256 bytes` before evidence execution.
- [x] Generated `v27-storage-mass-authority.txt` twice with byte-identical SHA-256 `1D77C4EA3D8011561EDE0010EBB77E80432A87ED6E256CC5141CDA4A7461E214`.
- [x] Fresh Physical Stock, Modular runtime, Surgery and P1 Defense suites PASS; Console Warning/Error `0/0`.
- [x] Fresh five-builder/7,219-file no-clobber PASS with `changes=0`; report SHA-256 `913AC6D7D8BC9D47005C5A8BAFB585C2BCFB881426F269615ACCAC25B35EC88F`.
- [x] Added plan checkpoint 94 and baseline record `balance:v27:positive-gram-storage-census-v1`.
- Checklist after closure: `587/660` checked, `73` open (`88.9%` surface). The three new open rows make the heavy-carcass, Q03 archive and FacilityBuffer debt explicit rather than hiding them.
- Plan SHA-256: `93D48A6AFAC80529EFB3175506682101C80F6330B1E487D55009B50A1E902704`.
- Baseline SHA-256: `46A74A8417E8C0D452CB5D876A2180387017A55D9ABCF350291DCEF9E9EB7882`.

Next bounded work: implement the separate FacilityBuffer gram-capacity/reservation contract. Keep 22/28kg carcass/corpse dedicated transport and D03 local-buffer delivery as an explicit Critical; do not weaken ordinary warehouse admission or claim the 21-storage deployment solves it.

Error log: the first combined FacilityBuffer source search emitted too many matching contexts and the tool output was truncated. It was read-only and changed no project file. Continue with file-local bounded reads for the destination model, claim registry, transaction, production gateway and save codec; do not repeat the broad combined context dump.

Error log: two bounded reads initially used stale guessed paths for `WorldItemModels.cs` and `ProductionItemGateway.cs` and returned file-not-found without changing files. The authoritative paths are `Assets/Scripts/Services/Items/WorldItemModels.cs` and `Assets/Scripts/Services/Economy/ProductionItemGateway.cs`; subsequent reads use the discovered paths.

Error log: one combined planning-file patch failed atomically because its `findings.md` checkpoint anchor was not present in the current tail. No file changed. Apply the task-plan and findings updates separately with current anchors.

Error log: one PowerShell `rg` call passed wildcard path operands on Windows and returned an invalid-path error without changing files. Use the concrete Production Core directory plus `--glob '*.cs'`; do not repeat wildcard path operands.

Error log: the first FacilityBuffer compile request repeated the known dynamic-command namespace ambiguity and resolved `CompilationPipeline` as `Unity.CompilationPipeline`, failing CS0234 before execution. No project file changed. Use the fully qualified `global::UnityEditor.Compilation.CompilationPipeline` symbol for the retry and do not repeat the ambiguous form.

Error log: one read used the stale guessed path `Assets/Scripts/Services/Infrastructure/Save/ProductionBillsSaveSection.cs`; the authoritative file is `Assets/Scripts/Services/Economy/ProductionBillsSaveSection.cs`. It was read-only and changed no file. Do not repeat the guessed path.

Error log: the first bounded wait command printed the entire production-input PlayMode report and exceeded the available model-context output. The report itself was only 2,367 bytes and valid; subsequent inspection used exact `RESULT`/`PASS`/`FAIL` markers and the last 30 matching lines. Do not dump whole broad reports while polling.

Error log: one follow-up `rg` command again passed Windows wildcard path operands for `ProductionRuntime*.cs` and `*Assembly*.cs`. PowerShell reported invalid paths while explicit-file operands still returned matches. It was read-only and changed no file. Use a concrete directory with `--glob '*.cs'`; do not repeat wildcard path operands.

Error log: two JavaScript orchestration snippets failed before tool execution with `SyntaxError: Invalid or unexpected token`. Both were read-only and changed no file. The retry used a JavaScript template literal and succeeded; keep multiline command strings in template literals.

Error log: the first exact production-destination claim compile failed with CS1061 because `ProductionBillRestoreCandidate.State` is internal across the asmdef boundary. The fix exposes only a public read-only `Bills` projection and the claim runtime now consumes that projection. The next fresh compile passed; do not widen the internal mutable state.

Error log: the first actual-AI production input pickup regression selected the two unbound one-unit fixture leftovers for ordinary warehouse hauling before the claimed production request, so no production intent committed and the focused report failed only `PRODUCTION_INPUT_BUFFER_PICKUP_MASS_IDENTITY`. The atomic cancellation then released all six requested units without loss. This is test-fixture competition, not an ownership implementation failure. Forbid the two intentionally unbound overflow-control stacks before starting AIHaul and require a non-null committed intent in the recovery assertion; do not repeat the competing fixture unchanged.

Error log: after forbidding the competing leftovers, the second actual-AI fixture exposed that its synthetic `LiveFacility` claim named a building that did not exist in the real world registry, so the destination authority correctly rejected both routed stacks as non-haulable. The production runtime's real-LiveFacility claim is already covered by focused contracts; this transport-only fixture must use the registry's explicit `ReservedTarget` anchor with no invented building identity. Do not weaken live-facility validation or repeat the nonexistent-owner claim.

Production input FacilityBuffer resolved evidence:

- [x] Added exact committed carried-haul gram query and bounded production gateway admission.
- [x] Production prefetch derives exact `2~3` cycle capacity and no longer calls the unbounded request API.
- [x] Added a source ratchet requiring exactly one bounded caller and zero unbounded callers in `ProductionInputLogisticsService`.
- [x] Fresh focused PlayMode admits inoculated-log inputs at exactly `4,200g`, rejects the next `250g` before pickup without mutation, restores `4,200g`, releases six items to `0g`, and reports `RESULT=PASS`.
- [x] Fresh Production Economy contracts PASS after a clean compile; Console Warning/Error is `0/0`.
- [x] Replaced source-debit/spawner delivery with a preflighted exact physical split/retarget transaction; obsolete partial `RequestLoose`/`RequestStored` helpers are removed.
- [x] Added exact `production:{billId}` claims and made all four claim-revocation paths close active haul/carry ownership before revoking the claim.
- [x] Actual AIHaul pickup preserves exact `4,200g`; mid-pickup cancel physically recovers the committed lot at the actor cell, releases the remaining three units, leaves quantity 8/8, and survives immediate save/restore with zero orphan intent.
- [x] Fresh focused PlayMode `RESULT=PASS`, Production Economy contracts PASS, Console Warning/Error `0/0`.
- [x] WIP consume removes exact `1,400g/2 units`, restores the pending receipt, acknowledges it once, and preserves fixture quantity 8/8.
- [ ] The full FacilityBuffer owner manifest remains open.
- Current literal checklist: `595/669` checked, `74` open (`88.94%`). Weighted remaining effort remains approximately `30~40%` because generic buffer/output, dedicated heavy transport, mass semantic/disposition and final economy/6-adult gates dominate the open work.
- Plan SHA-256: `D56C1D62865B1739F15CE5D3E4289931D2D8956FA931229087BCD05857D080EB`.
- Baseline SHA-256: `D1D5AD893039C6C105A78E2BE86E98CF078A676C580E5A5BA8D003BD3E7497CB`.
- Focused PlayMode SHA-256: `4E9E39F94A306F60090A057DA2C31787FF0CA7AD8F25407692E744F0FD0C846B`.

Next bounded work: apply positive gram authority to the remaining 19 storage definitions through their actual builders, keeping mass capacity independent from unresolved Q03 category semantics. Then move to FacilityBuffer as a separate token/capacity contract.
# 2026-08-25 V27 labor/facility fixed-point rebase (complete; downstream audits pending)

- [x] Replace the one-pass authored rebase with a bounded exact fixed-point coordinator.
- [x] Preserve per-iteration approval custody and dynamically capture every touched asset's original bytes under one rollback unit.
- [x] Align direct refresh with the approval/no-op scope, including 203 already-approved item-market rows.
- [x] Compile the current source and execute the real Unity transaction to `noOpDiff=0`.
- [ ] Run the current-source 256-seed economy simulation, whole-game coverage/labor/facility audit, and second deterministic artifact/asset gate.

# 2026-08-25 FacilityBuffer owner manifest checkpoint (complete; migration pending)

- [x] Enumerate and classify all current FacilityBuffer/FacilityOutputBuffer owner families and generic delivery callsites.
- [x] Add a deterministic CSV/TXT generator with current-source drift, exact-claim, bypass and orphan ratchets.
- [x] Integrate classification coverage into Production Economy contracts without requiring unfinished migrations to pass.
- [x] Compile current source, recapture twice with no byte/mtime change, and confirm Console Warning/Error `0/0`.
- [x] Close plan checkpoint 95's final manifest row and append baseline record `balance:v27:facility-buffer-owner-classification-manifest-v1`.

Fresh classification: input owners `39` (`migrated=1`, `remaining=38`), output owners `5` (`remaining=5`), direct bypass `5`, orphan API `1`, generic delivery invocations `59` across `39` files, unclassified `0`. This closes only classification; `RequireFullyMigrated` deliberately remains red until all remaining/bypass/orphan rows are retired.

FacilityBuffer owner-manifest error log:

- The first combined owner/source search included the entire historical planning corpus and broad runtime state matches, so its output was truncated and cannot serve as exhaustive manifest evidence. It was read-only and changed no project file. Continue with bounded source-root inventories and exact destination/claim symbols; do not repeat the broad cross-repository dump.
- One follow-up `rg` command again passed Windows wildcard path operands while looking for serializer callsites and returned invalid-path errors without changing files. The corrected command uses a concrete directory with `--glob 'V27*.cs'`; do not repeat wildcard operands.
- The first compiled manifest capture rejected its own exact-claim census because the predicate required the description to start with `exact`, while the blueprint row intentionally reads `archive exact ...`. No artifact was written. Match the canonical token anywhere in the claim-authority field, then recompile and rerun; do not weaken the required count of five.
- The first combined post-manifest read of `ProductionItemGateway`, `ProductionAssemblyBridge`, `ProductionAssemblyBridgeAdapter`, and `ProductionBillRuntime` exceeded the output/context budget and was truncated. It was read-only and changed no project file. Continue with file-local 100–180-line windows; do not repeat the combined dump.
- A follow-up status/search command included the full historical planning corpus and again exceeded the output budget, while one bounded read guessed `ProductionAssemblyBridge.cs` under `Services/Economy` instead of its authoritative `Models/Economy/Content` path. Both operations were read-only and changed no project file. Use exact source paths and bounded planning tails only.
- A later cross-service `Bounded|MassAdmission|Capacity` census returned the useful authority symbols but exceeded the direct output budget because warehouse tests and historical compatibility paths were included. It was read-only and changed no project file. Continue from the exact `ProductionItemGateway`, `ProductionInputLogisticsService`, `WorldItemWarehouseService`, and mass-admission symbols with bounded file windows; do not repeat the broad alternation search.

Fresh Unity result: `iterations=1; rebasePatches=4; directRefreshPatches=370; changedAssets=232; rollbackAssets=230; approvals=1936; noOpDiff=0`. This closes only the authored rebase/apply checkpoint; final mass, kg-aware EWU/price and play-balance gates remain open.

## 2026-08-25 common FacilityBuffer admission / power-fuel + production-input cutover (two owners complete; migration ongoing)

- [x] Add the common positive-gram `FacilityBufferCapacityProfile`, exact-lot admission token, repository-derived physical/carried occupancy query and bounded terminal receipt lifecycle.
- [x] Add reversible owner-scoped claim/profile publication and register the common services through the production composition root.
- [x] Publish `power:{nodeId}` as an exact live-building claim with a four-fuel-unit positive gram profile; managed power destinations fail loud when the profile is absent.
- [x] Route generic delivery through exact lot reservation before repository mutation, with an undo journal for split/retarget rollback and a routed receipt.
- [x] Fresh Unity compilation PASS; FacilityBuffer Mass Admission, Physical Stock, Industrial Infrastructure and Production Economy focused suites PASS; Console Warning/Error `0/0`.
- [x] Close power delivery terminal ownership, stabilize restore profile revision, reject raw routing, roll admission/retarget back on downstream failure, and prove actual AIHaul carried `350g` full save/restore→consume→power with no remaining intent.
- [x] Production input migrated from its legacy bounded precheck/max argument to the common profile/token authority, including actual LiveFacility/AIHaul, cancel recovery, restore and WIP evidence.
- [ ] The other 37 input owners, all 5 output owners, 5 direct bypasses and 1 orphan API remain open.

This closes the representative common-service, power and production-input owners, not full FacilityBuffer migration. After expanding the audited production-output work into explicit implementation gates, the authoritative plan is now `607/692` checked with `85` open (`87.72%` surface); weighted remaining effort remains approximately `30~40%`.

Fresh power closure evidence:

- [x] Current-source `Assembly-CSharp.dll` and `Assembly-CSharp-Editor.dll` advanced before execution.
- [x] FacilityBuffer Mass Admission, Physical Stock, Industrial Infrastructure and Production Economy static suites PASS; Console Warning/Error `0/0`.
- [x] Focused PlayMode report records raw-route rejection, exact-stack admission, actual AIHaul, carried `350g` restore, power `6.4/10`, zero remaining intent and `result=PASS`; normalized artifact SHA-256 `41CD376E62EA60D556DEF18382F38E1482FDF65B3FFF9F639593F997479CE0CC`, second-run byte/mtime change `0`.
- [x] Production input now publishes its `2~3` batch positive-gram profile together with the exact claim, uses common admission through the ordinary exact delivery route, and has zero live callers of the legacy caller-authored max API.
- [x] Isolated actual-LiveFacility/AIHaul PlayMode PASS: 4,200g admission, overflow pre-pickup rejection, carried pickup/cancel actor-cell recovery, current-format restore and exact 1,400g WIP Transfer; report SHA-256 `A3902852796480CA6F6F253CF64E415E0904915913D3AFE22148B450B993466A`, Console Warning/Error `0/0`.
- [x] Power-fuel normalized artifact was regenerated in two isolated PlayMode runs with SHA-256 `41CD376E62EA60D556DEF18382F38E1482FDF65B3FFF9F639593F997479CE0CC`, byte/mtime change `0`, Console Warning/Error `0/0`.
- [x] Owner manifest recaptured twice with input `migrated=2/remaining=37`, output `remaining=5`, bypass `5`, orphan `1`, unclassified `0`; CSV SHA-256 `DF455D3CA1BD9D7C07939FA0758C210743CDCEC03BF4F0753B3271E52DC6E5A4`, TXT SHA-256 `377388A7B6187E83031EA0DB9F1B8DEA7A170FB94F49967C510B069667A5FADC`, byte/mtime change `0`.

FacilityBuffer common-admission error log:

- The first focused dynamic scenario call could not resolve the newly added Editor type because the project assemblies were stale; no project file changed. A forced synchronous import exposed the actual Bee compiler diagnostics in `Editor.log`.
- The first import command repeated the dynamic namespace ambiguity and resolved `CompilationPipeline` as `Unity.CompilationPipeline`, failing CS0234 before execution. The retry used `global::UnityEditor.Compilation.CompilationPipeline`.
- The first real project compile exposed four source issues: two missing probe constructor arguments, one incompatible null-coalescing pair for topology values, and one short-circuit `out` local that was not definitely assigned. All four were fixed locally and the assembly timestamps advanced on the next compile.
- The first post-compile focused command treated the void `RunAll()` as returning a string and failed dynamic-command compilation before execution. The corrected command invoked it directly and passed.
- The first focused PlayMode launch guessed a nonexistent `DungeonStory.Infrastructure.Industrial.Editor` namespace and failed dynamic-command compilation before execution. The verifier is global; the corrected `global::IndustrialInfrastructurePlayModeVerifier.RunPowerFuelOnly()` command passed.
- The first carried-restore run reached successful fuel delivery and power but continued into unrelated clean-water assertions because `PowerFuelOnly` was checked outside `VerifyPowerAndFluids`. The verifier now exits immediately after the focused power evidence; the production route was not weakened or bypassed.

## 2026-08-25 equipment-repair common FacilityBuffer migration (complete; global migration ongoing)

- [x] Publish every active `equipment-repair:{equipmentInstanceId}` claim/profile as one deterministic owner-wide lifecycle image on create, restore, complete and cancel.
- [x] Calculate one repair-job capacity from unique equipment dynamic mass, installed modules, loaded ammunition and exact required material mass; representative focused profile is `6,500g`, revision `1`.
- [x] Require the exact claim/profile immediately before delivery and route the existing equipment/material exact stacks through common admission tokens.
- [x] Extend the material outbox fixture with profile publication, exact token commit, restore, WIP receipt join and terminal profile-zero assertions.
- [x] Add and run an isolated equipment-repair PlayMode mode using a real facility and actual AIHaul; two pickups, no duplicate request, repair completion, salvage conservation and terminal claim/profile zero PASS.
- [x] Fix the one fresh compile error (`capacityFailure` short-circuit definite assignment) without weakening authority checks; final current-source DLL timestamps advanced to `09:04:44Z` / `09:10:59Z`.
- [x] Strict Progression Combat Save, FacilityBuffer Mass Admission, Physical Stock, Production Economy and Industrial Infrastructure regressions PASS with Console Warning/Error `0/0`.
- [x] Regenerate manifest twice: input `migrated=3/remaining=36`, output `5`, bypass `5`, orphan `1`, unclassified `0`; CSV/TXT SHA-256 `4578FAA4E4D1310484E2CB966E4FCD7BCECC17A99E1BB322E765DA74421B55EE` / `CAAC0A58031E9C50926161A0A6F5858BAFBB018C63D14374E26006B7CBB56A31`, byte/mtime change `0`.
- [ ] The other 36 input owners, 5 output owners, 5 bypasses, 1 orphan and broad Physical Logistics failures remain open.

The authoritative plan is now `616/701` checked with `85` open (`87.87%` surface). Revised plan SHA-256 is `7A183703CD748272035655B48A1F9E0EF71393E4128E454F57FDFEF5126916FF`; weighted remaining effort is still approximately `30~40%` because output/WIP and cross-domain rollout dominate.

Equipment-repair migration error log:

- The first clean current-source compile exposed CS0165 for a short-circuit `out capacityFailure`. Equipment lookup and capacity calculation were separated into two explicit fail-loud branches; the second clean build succeeded and DLL timestamps advanced.
- The broad Physical Item Logistics run passed every new `MATERIAL_REPAIR_*` assertion but still ended with 11 unrelated failures in craft-output materialization and a stale construction work-order restore fixture. A dedicated equipment-repair mode was added and passed with captured/Console Warning/Error `0/0`; the broad failures remain visible and are not claimed as fixed.

## 2026-08-25 planned-output gram admission foundation (in progress)

- [x] Add separate planned-output request/token/publication receipt/final receipt contracts without fake repository stack IDs or caller-authored total grams.
- [x] Make source-lot and planned-output reservations compete in the same destination reserved-gram ledger.
- [x] Recompute planned line and batch mass through `IPhysicalItemMassQuery`, fingerprint the immutable subject and fail on capacity/profile/mass-authority drift.
- [x] Add focused static scenarios for shared capacity, exact 1g overflow, release, tampered receipt, exact replay, conflicting replay and stale revisions.
- [x] Complete the concurrent canonical output-line schema edit, then obtain a fresh current-source Unity compile and focused scenario PASS before closing the V27 checklist row.

Planned-output foundation error log:

- The first clean Unity build overlapped the bounded canonical output-line schema edit and correctly failed with CS7036 at `ProductionWorkshopContentAssetBuilder.cs:1313` and `ResourceEconomyAssetBuilder.cs:1151`, where the new constructor arguments had not yet been applied. This is an invalid intermediate source state, not completion evidence. Finish that bounded edit and rerun the clean build; do not reuse the failed DLL or Console `0` as proof.
- The direct dynamic schema probe could not reference `ProductionRecipeSO` because the MCP dynamic compiler lacks the Sirenix assembly. It failed before execution. A project-owned `ValidateCanonicalProductionOutputLines` regression replaced that probe and passed from the freshly built Editor assembly.

Resolved evidence: current-source DLLs advanced to `09:37:47Z/09:41:09Z`; planned-output admission and Production Economy/canonical-line scenarios PASS; Console Warning/Error `0/0`. Only the common token checklist row is closed. Asset backfill and live WIP producer remain open. Authoritative V27 checklist is now `617/701`, `84` open (`88.02%`).

## 2026-08-25 prepared-output exact provenance/checkpoint integration

- [x] Add split-aware exact custody and V13 current-delivery overlay while keeping the original physical receipt/target immutable.
- [x] Add Items route outbox replay/rollback/acknowledgement and Economy durable routing/terminal-retirement ownership.
- [x] Add exact Economy↔Items restore join and checkpoint-safe two-participant GC after durable atomic save replacement.
- [x] Fail-close direct carry consumption/removal, contextless drop, retail transfer, theft/relocation/compaction/spawn and FacilityBuffer aggregation bypasses covered by focused guards.
- [x] Obtain fresh Unity runtime/editor compilation and execute all focused routing/restore/GC/guard/ProductionEconomy suites with Console Warning/Error `0/0`.
- [ ] Implement live delivery-reroute prepare/publish/rollback across custody, haul intent/lease/AbilityHaul and gram admission.
- [ ] Prove actual AIHaul partial pickup/deposit/cancel/Downed/mid-haul restore and feedbench output in focused PlayMode.
- [ ] Ratchet every remaining legacy production bypass to zero and run two-pass artifact identity.

Current V27 checklist: `626/712` checked, `86` open (`87.92%`). Weighted remaining effort is approximately `20~30%`; the open work is dominated by live delivery reroute, actual PlayMode logistics, standard output profile/application, authored mass semantics, EWU/price and six-adult closure.

Document SHA-256: plan `E9440C697002EA2B20173F8880C1CB7C0319645332BCE0362963AC19DC4A848E`; whole-game baseline `2E8E14F96B5C2BF97BAD448BFB4D534195AE9C7D83258A330ACFB2FE53905584`.

## 2026-08-26 active V27 mass/hauling execution checkpoint

- [x] Bind prepared-output buffer capacity to deterministic source digest and schema-v3 save/restore contract.
- [x] Enforce production-owner digest/minimum through admission fingerprint and isolated publication.
- [x] Add focused P03 sawmill exact profile, lumber codec and deterministic `14,400g` capacity projection.
- [x] Add real-adapter sawmill execution/schema-v3 round-trip/capacity restore/publication/routing exact-once focused fixture.
- [ ] Rebuild the full Production/Physical/Routing save graph and keep sawmill live closure open until normal-boot AIHaul/fault evidence exists.
- [ ] Continue Batch A recipe-family expansion with whole-workstation families only, then proceed to Batch B lifecycle/revoke fence without overlapping Unity/common save edits.
- [x] Migrate the complete charcoal-kiln, mill (including reachable malt), steelworks and treated-lumber definition-only workstation families with exact 4-cycle asset authority and focused regressions.
- [x] Use the public save-section registry to prove completed-unrouted sawmill aggregate rehydrate and replay idempotency.
- [x] Implement the common Batch B production-output lifecycle query, mutation epoch, direct demolition empty-only reversible fence, and no-authority fail-closed gates for relocation/synthesis/evolution.
- [x] Route drained structural/cover lethal damage through typed strict-empty destructive loss with grid/authority rollback, zero-HP cover restore protection, and focused invasion/static evidence.
- [x] Retire old modular-world objects without gameplay destruction events during same-ID current-format aggregate replacement and prove deterministic round-trip output.
- [ ] Implement contributor-by-contributor durable destructive release for active production authority, then add actual invasion PlayMode and save/restore lifecycle-fingerprint evidence.
- [ ] For `capacity-routing`, prefer completing the proven route → Items commit → Economy/Items acknowledgement → durable checkpoint-GC lifecycle over introducing an arbitrary batch-retire API; add only the typed actor-current-cell quiesce and owner-removal destination policy needed to make that lifecycle complete.
  - [x] Add an immutable exact routing-batch query with line, route-operation and physical-slice receipt projections; register it and prove initial/applied/drained/checkpoint-tombstone states without exposing save DTOs to gameplay.
  - [ ] Add atomic actor-current-cell quiesce for exact-route carried cargo, including legal partial-pickup lease closure and mixed-owner zero mutation.
  - [ ] Add the owner-removal loose-target/defer policy and drive all OriginBuffered remainder through the normal route/ack lifecycle.
  - [ ] Add durable capacity-routing progress/outbox only for cross-aggregate replay evidence, then wait for the normal durable checkpoint GC before owner acknowledgement.
    - [x] Add Physical V15 producer DTO/outbox, canonical capture/detached restore, raw missing-array/V14 rejection, local replay/tamper validation, journal producer/orphan joins, and focused Unity evidence.
    - [ ] Complete exact frozen-slice/actor-carry reverse joins and bind the live capacity participant before closing the parent row.

Error log: a combined read of `ItemTransferService`, repository mutators, and routing helpers exceeded the output cap while preparing atomic actor quiescence. Do not repeat that broad dump; continue with bounded, file-local reads and persist findings after each two views.
Error log: the first actor-quiesce outbox fixture patch used an incorrect bottom-of-method anchor near `3_000L` and did not apply. Re-read only the exact fixture tail and patch the call sites/helper separately; do not repeat the failed broad hunk.
Error log: a follow-up search for every actor-carry construction used an over-broad context pattern and truncated. The relevant production type has only the capacity outbox fixture constructor today; use exact type-name/file-local searches from here.
Error log: a PowerShell/rg search passed a wildcard path containing `*` as a literal Windows filename and emitted OS error 123. The custody type is in `FacilityOutputExactRouteCustodyCodec.cs`; use that exact path.
Error log: the first Unity compile of atomic actor quiescence failed with CS1628 because repository validation captured the method `out receipt` parameter inside `Any(...)`. Assign the factory result to a local value, validate that local, then publish it to the out parameter; do not capture an out parameter in LINQ.
Error log: the second Unity compile failed because the Editor fixture cannot call repository-internal capacity drain accessors across the Editor assembly boundary. Add one `UNITY_EDITOR`-only outbox fixture helper that publishes a cloned receipt; do not widen runtime repository mutation APIs.
Error log: immediately after the long haul-freeze domain reload, Unity MCP revoked the first Console query connection. The preceding refresh command reported successful compilation; retry MCP after the editor reconnects and do not treat the revoked query as Console evidence.

Error log: the first immutable routing-batch query compile failed because its constructor was `internal` in the Production model assembly while the Economy service implementation is in `Assembly-CSharp`. The snapshot is read-only and never accepted by a mutation API, so expose only its constructor publicly; do not widen any command surface or repeat the inaccessible constructor.
- [ ] Return to the remaining Batch A custom/stateful output families after the destructive-loss P0 is closed; do not widen Unity/common save edits concurrently.
- [x] Add a replay-safe destructive-drain journal skeleton, carried-cargo failure atomicity, idempotent input revoke and attempt-scoped apparel material operation identity without registering the unfinished save section.
- [x] Project generic/combat/apparel, complete prepared-routing/exact-route outbox and durable physical/carry/recovery provenance from current-format save semantics; prove nested shuffle invariance and focused fresh-DLL regressions.
- [x] Complete pure capacity save re-projection and compose all five contributor hashes with one explicit lifecycle schema token.
- [x] Publish normalized world/character/physical/production/routing/combat/environment DTO slots only from the real detached commit transaction, validate the complete deterministic bundle at drain commit, and clear it on complete/rollback/discard.
- [x] Lock the exact five destructive-drain participants to one versioned DAG/fingerprint and reject journal replay/advance plan drift or non-monotonic owner state.
- [x] Insert an ordered durable-save commit pipeline after atomic replacement and preserve prepared-output checkpoint GC as the order-100 compatibility participant.
- [x] Bind the drain header and exact five participant current fingerprints to the normalized save-only contributor projections before publication.
- [x] Project typed Prepared owner IDs from all five source domains and require an exact forward/reverse journal bijection.
- [x] Collapse physical stack owners to one atomic destination owner matching the real release boundary.
- [x] Add Physical V14 producer-side custody-drain outbox with deterministic actor/intent/destination progress, exact receipt replay and save validation.
- [x] Join the physical producer outbox to the destructive journal in both directions and reject phase/receipt/orphan drift before publication.
- [ ] Implement the live physical custody drain port, including mixed-destination Pick-and-Haul scope protection, then bind the actual physical participant.
- [ ] Add world/physical/production/routing/combat/environment/journal forward and reverse joins to aggregate preflight and section-stage restore before DI/save registration.

### Current destructive-drain boundary rewrite evidence

- [x] Add a shared exact-route lifecycle that cannot report success before physical route, both acknowledgements, final destination authority, and gram admission are durable.
- [x] Add and statically verify the capacity-routing participant contract fixture; keep live physical-route completion open until Unity integration evidence exists.
- [x] Fail-close capacity terminal results with missing commit/receipt and reject journal/producer receipt drift during recovery.
- [x] Add a cycle-free root-store-only open-operation query and compose it with the transient mutation epoch in production DI.
- [x] Freeze generic production bill mutations/automatic retirement and new FacilityBuffer gram admission for every open journal phase.
- [x] Compile Production, Economy, Assembly-CSharp, and Editor assemblies in dependency order with zero diagnostics after the durable gate changes.
- [ ] Execute the capacity and durable-open-gate scenarios inside Unity and obtain fresh Console Warning/Error `0/0`.
- [x] Add and statically verify the actual physical participant adapter over the existing Items custody-drain port, including zero-owner empty authority, exact-one authoritative owner, 1g drift, terminal guard, and recovery receipt mismatch.
- [x] Add and integrate-compile generic/combat/apparel producer outboxes and all five participant implementations before registering the exact-five registry; Unity-loaded fixture execution remains separately open.
- [x] Add the Items-owned live input-destination custody drain service and require immediate recovery before any AI/movement/expiry tick.
- [x] Add the Production-owned generic terminal producer outbox with child receipt, WIP terminal receipt, claim revocation, exact bill removal, owner acknowledgement and child-first GC.
- [x] Register the live input service and generic producer query/command; derived current-source runtime compilation passes.
- [x] Complete and compile the generic destructive-drain participant and focused live-service/outbox fixtures.
- [x] Add current-format generic producer save projection and exact cross-aggregate restore join before exact-five gameplay registration.
- [x] Expand the existing combat contributor/source projection from craft-only to craft plus active equipment-repair orders without adding a sixth participant.
- [x] Add an unregistered exact apparel lease-authority capture/release port that remains valid after restore when the legacy wrapper cache is empty.
- [x] Implement combat and apparel producer outboxes/participants plus unregistered current-format producer projections and upper forward/reverse joins.
- [x] Re-audit the live combat/apparel authorities before further integration and reject a whole-system rewrite: retain the proven shared drain journal/participant/save topology, but replace the unsafe terminal mutation boundary with owner-aggregate durable effect/source receipts.
- [ ] Rewrite only the combat terminal boundary around craft/repair owner aggregates: freeze pending custody once, persist the four-phase terminal effect row beside each owner save authority, remove pending quantity/grams from the post-child source fingerprint, and prove exact source/evidence joins without a second roll or debit.
  - [x] Split Items custody capture/build around one immutable source snapshot, hold the reservation capture barrier across the whole closure, reject stale revision/ownership/row drift, and preserve the legacy wrapper as a deterministic composition.
  - [x] Move combat prepare onto `CombatEquipmentTerminalPreparedSource`, exclude volatile pending quantity/grams from source identity, bind the full child request at producer prepare, and exact-join child claim/input/released grams by phase; Runtime and Editor current-source Roslyn compile pass.
  - [ ] Implement the live craft owner terminal authority and its current-format effect/source receipt rows; focused Unity execution remains open.
  - [ ] Implement the equivalent repair authority and destination-buffer closure; focused Unity execution remains open.
- [ ] Rewrite only the apparel terminal boundary inside `ApparelWorkOrderRuntime`: persist effect/source receipts with the work orders, keep repair physical evidence and rejected-output transform evidence durable, and remove the order only in the same aggregate publication that creates the source-terminal receipt.
- [ ] Implement synchronous pre-gameplay recovery and rollback-capable reverse-DAG checkpoint GC before exact-five registration; then connect real combat/apparel lower terminal receipt authorities and execute Unity-loaded fixtures.

Continuation error log:

- The static Assembly-CSharp compile invoked immediately after the detached capacity floor correction returned tool output after context compaction, so its exit status was not preserved as admissible evidence. Re-run a bounded current-source Roslyn compile before closing any row that depends on that correction.
- A combined read of `FacilityBufferDestinationReleaseService` plus the repository-wide destination-release call graph exceeded the output cap. The complete 159-line service body was retained, but the broad call graph was truncated; continue with exact call sites and file-local searches.
- The first derived Assembly-CSharp preflight compile command failed in PowerShell parsing before compilation because the inline quoted source-path append was malformed. No source or Bee response file changed; keep the derived response output/reference replacements and pass the one new source path as a separate compiler argument.
- The corrected current-source Assembly-CSharp compile reached Roslyn but the stale Bee source list again omitted `CapacityRoutingAuthorityReleaseContracts.cs`, producing three `ExactAuthorityReleaseStatus` CS0246 diagnostics in existing Items services. This is the known response-file freshness boundary; append that existing current source together with the new preflight service and rerun.
- The next runtime compile exposed eight more existing current sources omitted from the stale Bee list (`ProductionCapacityRoutingPhysicalSourceQuery`, haul-plan fence, operation-release coordinator, open-operation query, mutation gate, exact-route lifecycle, drain coordinator and actor physical fingerprint). No new preflight diagnostic was emitted. Append the exact missing files only; do not broaden to a whole-tree duplicate source list.
- The first Editor compile against the fresh temporary runtime stopped at an existing capacity fixture because the stale runtime response omitted `FacilityOutputExactRouteEditorTestFactory.cs`. Rebuild the temporary runtime with that `UNITY_EDITOR` test-only helper, then retry the Editor response; this is unrelated to the new preflight contract.
- A PowerShell reflection execution attempt installed a script-block `AssemblyResolve` handler; PowerShell recursively requested satellite/resource assemblies inside that handler and terminated with stack overflow before the fixture ran. Do not repeat that resolver approach. The fixture is compiled but remains execution-pending until a bounded compiled runner or Unity MCP is available.
- The first `DungeonStory.Items` compile of the new input-destination contract exposed target-typed `new(capacity).Append(...)` without a target type (`CS8754`). Both builders now use explicit `new StringBuilder(capacity)` and the focused Items compile passes.
- The first current runtime compile after the new outbox again used the stale Bee source list and omitted `CapacityRoutingAuthorityReleaseContracts.cs`, producing the three known `ExactAuthorityReleaseStatus` errors. The bounded derived response now appends only the previously audited missing sources and replaces Items/Production/Economy references with current temporary refs.
- The next runtime compile correctly exposed stale Production/Economy refs for the new prepared-output preflight contracts. Recompiling Production and Economy first, then replacing those refs in the runtime response, produced a zero-diagnostic current-source runtime build.
- Save-authority audit found that `ItemQuantityLease.leaseId` is runtime-transient: restore reconstructs leases from canonical claim hints and allocates new sequence IDs. Persisting raw lease IDs in the new drain would create false restore conflicts. The new row now persists stable lease-authority fingerprints instead; live release still recaptures current leases by operation owner.
- A bounded PowerShell range reader constructed nested array ranges incorrectly and failed before reading or changing a file. The retry used independent fixed ranges; do not repeat the nested subtraction expression.
- The first derived Roslyn response-file command used C-style escaped quotes inside PowerShell and failed at parse time before compilation or source changes. The retry used a single quoted source token and passed.
- One read-only `rg` command passed Windows wildcard path operands and returned invalid-path errors. The corrected command targets the concrete registration directory with `--glob '*.cs'`; do not repeat wildcard operands.
- The first root-side Editor compile of the participant fixture raced the subagent's final edit and saw the removed `plan.DependsOn` assertion, producing one `CS1061`. No gameplay source changed from that failed compile. After the fixture stopped mutating, the same current-source runtime and Editor response files compiled with zero diagnostics; do not reuse the raced diagnostic as current evidence.
- The first combat projection compile used Bee's missing current `DungeonStory.Items.ref.dll` and stopped with `CS0006`. The derived response now references the already current Items ref from `GenericDrainRestoreJoin`; the combat assembly then compiled successfully.
- Rebuilding Production for the combat projection initially omitted the untracked generic terminal contract source, causing downstream generic type `CS0246` diagnostics. The bounded Production response now appends that audited source, recompiles Production, and the runtime build advances normally.
- The first integrated runtime/editor compile caught two focused-fixture callsite mistakes from the combat projection edit: `OwnerCount` instead of `ActiveRecordCount`, and one missing maintenance payload argument in the absent-lifecycle helper. Both fixture-only mismatches were corrected; current Production, Combat, runtime and Editor derived assemblies now compile with zero diagnostics.

- The first generic producer-contract lookup guessed `Models/Economy/Content/ProductionBillModels.cs`; the authoritative DTO file is `Models/Production/Core/ProductionBillModels.cs`. No mutation resulted; use the Production-core path for bounded save-contract reads.
- The first generic codec lookup guessed `Models/Production/Core/ProductionBillStateCodec.cs`; the codec implementation is in `Models/Economy/Content/ProductionBillStateCodec.cs`. No mutation resulted; keep DTO/state reads in Production core and codec/runtime reads in Economy content.
- The first prepared-output execution-port lookup guessed `Models/Production/Core`; the interface is authored in `Models/Economy/Content/ProductionPreparedOutputExecutionPort.cs`. No mutation resulted; use that path and keep the adapter in `Services/Economy` distinct.

- The first ordered compile after extracting the shared exact-route lifecycle failed in the existing Economy diagnostic because it still references the removed internal `ProductionPreparedOutputDeliveryDispatch` probe (`CS0103`, six call sites). Keep a diagnostic-only compatibility probe or migrate the fixture before repeating; the runtime distribution path must remain on the new composite lifecycle.
- The second ordered compile passed Production/Economy but the stale Assembly-CSharp Bee response omitted the existing untracked `CapacityRoutingAuthorityReleaseContracts.cs`, producing three `ExactAuthorityReleaseStatus` `CS0246` diagnostics. Repeat the known static fallback with all current untracked common contract files explicitly appended; this is response-file staleness, not a new source-contract defect.
- The first static compile of the capacity drain haul-plan fence used the guessed type name `ItemQuantityLeaseSlice`; the current contract type is `ItemLeaseSlice`, producing `CS0246`/`CS0019`. Replace only that local enumeration type and rerun the ordered compile.
- The first combined `AbilityHaul` capacity-drain gate patch used a repeated dependency-check context that did not match both current methods and was rejected without mutation. Apply the field/injection, `CanStartHauling`, `StartHauling`, and helper as separate bounded patches.
- A combined bounded read of the destructive-drain contracts, journal, task-plan tail and scoped worktree status still exceeded the aggregate tool output cap. No mutation resulted. Continue with one file/range per read and keep each output below 10k tokens.
- The first combined inspection of the new capacity participant fixture, task-plan tail, and scoped dirty-worktree status exceeded the aggregate output cap and truncated the fixture body. No mutation resulted. Inspect that fixture in bounded ranges and compile it independently before treating its scenarios as evidence.

- The first assembly-boundary probe guessed `Assets/Scripts/Models/Items/DungeonStory.Items.asmdef` and used a PowerShell-incompatible `**` glob. Both lookups failed before any mutation. The corrected bounded lookup found the actual files under `Models/Items/Core`, `Models/Production/Core`, `Models/Economy/Content`, and `Services/Foundation`; do not repeat the guessed paths.
- Broad combined authority/worktree reads exceeded the output cap twice. Current inspection is file-local and range-bounded; no code or asset mutation resulted from either truncated read.
- The first V15 focused-test compile rebuilt runtime code but failed the Editor assembly because the test assembly could not see the new `internal` raw-shape verifier (`CS0117` at the two fixture calls). The verifier is pure validation and never accepted by a mutation path, so only that method was made public; no gameplay command surface was widened.
- The first visibility-fix import returned success and Console `0/0`, but the inspected `Assembly-CSharp*.dll` timestamps were older than the corrected source. It is not accepted as fresh compile evidence; issue a new full synchronous refresh/compile and verify timestamp ordering before running the fixture.
- A PowerShell reflection probe tried to resolve global runtime type `PhysicalItemsSaveSection` from `Assembly-CSharp.dll` and hit a null type handle, although the Editor fixture type and `RunAll` were present. The probe is not used as proof. Bee response files place the save section in `Assembly-CSharp` and the fixture in `Assembly-CSharp-Editor`; the authoritative proof is the Unity dynamic invocation of the newly compiled fixture plus Console/Editor-log checks.
- The first full-persistence regression invocation guessed a nonexistent `RunAll` method and failed dynamic-command compilation before execution. The actual public entry point is `VerifyFullCurrentFormatRoundTrip`; use that method and do not repeat the guessed call.
- The first fresh Unity compile request after adding the capacity-routing actor authority-release service returned `Connection revoked. Go to Unity Editor > Project Settings > AI > Unity MCP to change approval.` No editor/scene fallback was used; do not repeat the MCP call until approval changes.
- The first fallback `dotnet build Assembly-CSharp.csproj --no-restore` could not start because the system `dotnet` installation has no SDK. Do not repeat it. The Unity 6000.3.8f1 bundled Roslyn compiler plus the existing Bee response file is the accepted static fallback until MCP reconnects.
- The first combined durable actor-authority DTO/fingerprint/interface patch used a receipt-body anchor that did not match the current file order and was rejected without mutation. It was split into bounded DTO, fingerprint, serializer, interface and clone patches; do not repeat the broad combined hunk.
- The first static compile of the new actor-transition fixture failed because its local hauling-settings provider omitted `MaxCarryMultiplier` and `Restore`. Implement the complete provider contract before repeating the ordered compile.
- A fresh Unity MCP state retry after the actor-transition fixture compile still returned `Connection revoked`. Do not repeat Unity MCP calls until the Editor approval state changes; continue static/P0 save-contract work without touching the dirty scene.
- The first compile of the detached actor-physical save gate exposed that the Editor-only public fingerprint entry point was hosted on an internal class (`CS0122`). Expose the stateless fingerprint host while keeping both runtime overloads internal; do not widen record access or mutation APIs.
- A bounded read initially guessed the capacity cross-aggregate validator under `Services/Infrastructure/Save`; the actual file is `Services/Economy/ProductionFacilityDestructiveDrainCrossAggregateSaveValidation.cs`. No mutation occurred from the failed path; use the exact Economy path.

## 2026-08-26 exact-five live registration prerequisites (in progress)

- [x] Re-audit the latest five participants, producer projections, save DAG, DI registrations and live destructive-loss entry point.
- [ ] Add maintenance-aggregate-owned repair terminal effect/save/restore state and a craft/repair authority router.
- [ ] Add apparel-aggregate-owned terminal effect/source receipt state and replace rejected auto-dismantle raw deletion with a durable physical Transform.
- [ ] Register the exact five participants, producer sections, journal and coordinator only after both owner authorities compile and pass focused evidence.
- [ ] Run synchronous restore-before-gameplay recovery and rollback-capable reverse-DAG checkpoint GC before opening the live destructive path.

- The first source-range command for `CombatEquipmentCraftTerminalEffectSaveData` calculated a negative `Select-Object -Skip` value because the declaration was near the file start. It failed before mutation; the retry reads the bounded file head directly.
# 2026-08-26 exact-five combat upper authority integration (in progress)

- [x] Repair terminal authority compiles with same-aggregate maintenance terminal rows.
- [x] Concrete craft/repair source-authority router added and runtime static compile passed.
- [x] Focused repair/router Editor scenarios compile; actual Unity menu execution remains pending.
- [ ] Combat authority/outbox/participant DI remains closed until apparel, recovery, and rollback-GC prerequisites are green.

# 2026-08-26 exact-five apparel upper authority integration (in progress)

- [x] Orders and terminal receipts share one pointer-swapped runtime authority.
- [x] Live effect/source terminal ports and CharacterEnvironment V7 persistence compile.
- [ ] Focused apparel terminal save/restore scenarios pass.
- [ ] Rejected auto-dismantle uses a durable exact-mass Transform instead of raw delete/spawn.
- [ ] Apparel outbox/participant/save producer registration remains closed until the above and recovery/GC gates are green.
# 2026-08-26 bounded exact-five continuation contract

Status: **in_progress**

- [x] Reconfirm that a whole hauling/warehouse rewrite is slower than preserving the green gram, admission, custody, routing and same-aggregate terminal authorities.
- [x] Close combat craft/repair source dispatch statically with the strict owner-prefix router and focused repair/router scenarios.
- [x] Implement apparel work-order plus terminal-effect/source receipts as one pointer-swapped aggregate; add strict CharacterEnvironment V8 capture/restore joins and focused static scenarios.
- [ ] Replace apparel rejected-output `DeleteStack -> SpawnItemAt` with a durable exact-once Transform boundary.
  - [x] Persist exact rejected source identity, pending input Transfer receipt, recovery outcome/mass, publication and acknowledgement owner fields.
  - [x] Reject premature acknowledgement inference and published-output respawn when exact physical evidence is missing.
  - [ ] Reserve the complete recovery gram batch in the existing FacilityBuffer planned-output authority before input debit.
  - [ ] Publish and acknowledge recovery through the existing atomic full-batch publisher; no direct output spawn remains.
  - [ ] Publish the initially crafted unique garment and Apparel component through the same admitted atomic boundary; no raw spawn/component/delete rollback remains.
  - [x] Prove in current-source Runtime/Editor compilation that reserved `Transfer` pending consumes the exact lease/source atomically, rolls back source/lease/pending state together on publication failure, and replays without the consumed lease.
  - [x] Execute the reserved `Transfer` focused scenario in a Unity-loaded domain before using it as final runtime evidence for the acknowledged-garment handoff. (`PhysicalStockQueryV18DebugScenarios.RunAll`, Console 0/0)
  - [ ] Prove output-full source preservation, physical-ahead join, exact capacity commit, missing-output conflict, unique component/mass and attempt isolation in focused Unity execution.
    - [x] Craft output-full source preservation, exact unique component/mass, terminal acknowledgements, replay idempotence and rejected-sale physical routing pass in `ApparelPhysicalTransactionDebugScenarios.RunAll`; physical-ahead/missing-output/attempt-isolation and rejected-dismantle remain open.
- [ ] Add rollback-capable reverse-DAG checkpoint GC for `physical -> capacity -> generic -> combat -> apparel -> journal`.
- [ ] Register the exact-five participants/coordinator/save sections only after all live owner/receipt joins are green.
- [ ] Run synchronous post-restore/pre-gameplay recovery before AI, movement, lease expiry or ordinary restore hooks.
- [ ] Obtain fresh Unity compile/focused execution and only then close the matching V27 implementation checklist rows.

Apparel rejected-dismantle authority contract:

| Contract | Authority |
|---|---|
| immutable definition | `ApparelDefinitionSO` and `TextileMaterialDefinitionSO` catalogs |
| mutable gameplay state | one `ApparelWorkOrderRuntime.AuthorityState` pointer containing orders and terminal rows |
| physical input | Items-owned pending `Transfer` receipt for the exact rejected unique stack |
| physical recovery | Items-owned planned-output reservation plus atomic full-batch publication in the exact facility buffer; output mass must not exceed frozen input mass |
| capacity | `IProductionOutputDestinationAuthorityRuntime` owns the facility claim/profile; `IFacilityBufferMassAdmissionService` projects and reserves grams from immutable mass subjects |
| command | one Apparel physical transaction facade composes destination authority, input disposition, planned-output admission and publication; `ResolveCraft`/`ResolveRejectedApparelDismantle` do not mutate physical stacks directly |
| restore | strict current-format environment payload plus detached Physical pending/output candidate joins; crash-ahead is accepted only for an exact deterministic commit |
| failure | missing/drifted source, pending receipt, output commit, grams or instance ID fails loudly without clearing owner state |
| completion | capacity commit and physical publication acknowledgement precede input acknowledgement; order fields clear only after every receipt is terminal |
