# DungeonStory Current Task Plan

## Active SOURCE_EDIT sub-batch: production-entry atomicity correction — 2026-09-20

- [ ] Fix the five source-review defects in offense finalization/retry, V20 effect ordering, primitive survival, and breakdown drinking without adding silent fallback behavior.
- [ ] Add branch-specific regressions that invoke the real outer production entry paths and prove rollback or durable retry at each demonstrated boundary.
- [ ] Strengthen the 57-kind evidence so acknowledgement and actually executed probes are reported rather than inferred only from static kind lists.
- [ ] Freeze source, compile in a new immutable revision, run focused and dedicated production-entry suites, then run the full-world save regression.
- [ ] Update review evidence and handoff only from the new current-source results; preserve r18/r81/r40/r164 as historical evidence.

Window: `SOURCE_EDIT / EDIT_READY`. Unity and `dungeon-player` MCP remain unused. The five P1 findings in the immutable review artifact are the acceptance scope.

Errors encountered:

- The first three-file planning patch assumed generic `# Findings` / `# Progress` headings, but these files use DungeonStory-specific headings. `apply_patch` rejected the whole patch and changed no files; the corrected patch targets the current headings.
- The first survival source lookup guessed `Assets/Scripts/Characters`; the files actually live under `Services/Survival` and `Models/Survival/Core`. The failed read changed no files; exact paths were resolved with `rg --files`.
- A Windows `rg` call passed wildcard file arguments directly and failed with an invalid path syntax error. It changed no files; subsequent reward searches use `-g` include patterns over real directories.

## Active SOURCE_EDIT sub-batch: ledger economy producer transactions — 2026-09-20

- [x] Trace the live owner/mutation boundary for `AutoProcurementResult`, `BuildingRetailPurchaseCommitResult`, `FacilityCrimeEvent`, `OperatingDayReportEvent`, and `StockSupplyResult`.
- [x] Reuse `MigratedProducerOutcomeKind` / `IMigratedProducerOutcomeTransaction` at each owner boundary with stable operation identity and current day.
- [x] Preserve exact rollback on definite rejection, or establish durable pending before irreversible child work; isolate post-commit observers.
- [x] Run source-only `git diff --check` and production non-Editor symbol coverage. Do not run Unity, compile, tests, MCP, inventory, manifest, or runner updates.

Window: `SOURCE_EDIT / READY/FROZEN`. Ownership remained restricted to the five producer owner files plus strictly dedicated sub-contract/adapters; no Editor fixture callsite was changed.

Errors encountered:

- The batched knowledge-base query exceeded the 30-second command window after two completed symbol checks and while starting `FacilityCrimeEvent`. Both completed checks reported the index stale with 1,026 freshness failures. No files changed; current C# and narrow `rg` searches are used instead of repeating the timed-out batch or rebuilding generated indexes.
- The first combined retail/crime `apply_patch` payload was rejected by the JavaScript wrapper with a string syntax error before `apply_patch` ran. It changed no files; the same scoped edit is split into smaller raw patch payloads instead of repeating the oversized wrapper input.
- A later three-file planning update raced another writer and failed to write `task_plan.md`; the source edits were unaffected. Findings/progress were updated in a separate patch, then this plan-only update was retried against the current file.
- The first fail-loud correction patch repeated the same file in two update blocks, which `apply_patch` rejected before editing. The corrected patch combines those hunks under one file update and leaves the other owner edits separate.

## Active goal: close all 57 remaining gameplay-outcome producers — 2026-09-20

- [x] Set one persistent goal for the remaining `transaction-integration-pending` producers: implement all 57 continuously, then run the consolidated gates.
- [x] Freeze the exact 57-row inventory and group rows by shared gameplay-state owner and transaction boundary.
- [ ] Complete one uninterrupted `SOURCE_EDIT` pass across every owner group. Reuse the common prepared-outcome/outbox/save contracts; do not stop for per-row manifest promotion or per-row full-world verification.
- [ ] Run only source/static checks and compile-blocker correction while owner seams are still being connected.
- [ ] After all 57 rows are implemented, freeze source and run the consolidated compile, focused normal/fault/replay suite, full-world save roundtrip and inventory regeneration.
- [ ] Accept completion only when the current-source inventory reports `transaction-integration-pending=0`, all required runtime/save gates pass, and every promoted row names its real replacement authority and evidence.

Window: `SOURCE_EDIT / EDIT_READY`. The main agent owns transaction design, shared contracts, save boundaries and final integration. There is no per-result approval or full verification cycle inside this goal. Unity MCP and `dungeon-player` MCP remain unused.

Errors encountered:

- The first PowerShell inventory listing attempted to pipe directly from a `foreach` statement and failed with `An empty pipe element is not allowed`. It changed no files. The next attempt materializes an array before formatting instead of repeating the invalid form.
- The first multi-file planning patch used stale headings for `findings.md` and `progress.md`, so `apply_patch` rejected the entire patch without changing any file. The corrected patch uses the current headings.
- A registration search guessed `DungeonNarrativeRegistration.cs`, which does not exist. It changed no files; registration composition is resolved from the actual `DungeonFoundationRegistration.cs` and related files before editing.
- A save-pattern read guessed `GameplayOutcomeBridges/Infrastructure/InfrastructureCommandOutcomePersistence.cs`, which does not exist. It changed no files; the exact `InfrastructureCommandOutcomeSaveSection` definition is located by symbol before reuse.
- The first r140 launch check queried its log before Unity had created it because the Windows Editor process detached from the shell. The Editor PID was then awaited directly; it exited successfully and the completed log contains zero C# errors and no fatal compile marker.

## Current implementation batch: Phase80 research work transaction — 2026-09-19

- [x] r36 current-source compile has zero C# errors and 13/13 focused groups PASS. Actual physical/reconnaissance/task/outbox joint commit, canonical-exception recovery and joined cleanup pass the r33 failure boundaries. Main reviewed isolated agent gateway/descriptor/fixture work; 66 source/meta hashes are recorded. This does not prove full Codex/input integration or whole-game save/UI/performance.
- [x] Persist task allocation high-water through empty queues and JSON; unknown legacy history stays zero, emits a restore warning and blocks new allocation instead of reusing IDs. Pending modern tasks require matching physical completion revision/producer/operation on restore.
- [ ] Complete explicit legacy allocation-history/split-commit reconciliation and actual Codex/material/outbox save roundtrip. Restore warnings and safe blocking are not completion of compatibility. Verify whole-domain semantic joins before promoting the research producer.
- [x] Revalidate r32: all 48 source hashes match, Unity absent. Previous goal turn made verified implementation progress. Main reopens EDIT_READY for knowledge reward/ack fault closure, using the already-read Unity C# and planning guidance.
- [x] r33 runs the two new commit-after-effect probes against unchanged production: C# errors 0, existing 9 groups PASS; the new group FAIL reports reward-after => reward=2/pressure=20 and ack-after => task remains 1. Retained failure evidence and 48 source hashes; only the test helper/runner differ from r32.
- [x] Implement real owner preparation/rollback and the physical outcome-participant boundary; r36 verifies actual region/physical owners and both r33 fault classes without missing-receipt success. Full save/restore is tracked separately above.
- [x] Revalidate r31 (40 hashes match, Unity absent); previous turn made verified implementation progress. Main EDIT_READY for cross-section snapshot integrity, no MCP.
- [x] Bind newly captured section payloads and the entire manifest with versioned SHA-256; r32 rejects same-sequence payload tampering/mixed snapshots before restore. Preserve unsealed V24 as an explicitly warned legacy path, never automatically reseal load input. This does not replace producer semantic joins.
- [x] r32 verifies the real save service's roundtrip and ten integrity groups using an explicit recording registry, plus the existing atomic file-write failure regression. Current C# errors 0, all 9 focused groups PASS; semantic/physical/outbox integration and real whole-game restore remain separate pending gates.
- [x] Reproduce reward-applied-then-throw and acknowledgement-then-throw in r33; close and verify the actual completion/cleanup runtime boundary in r36. Do not substitute this focused runtime proof for full semantic save closure.
- [x] Resume after the AGENTS-only reply (no ledger progress): Unity process count is zero. Main reopens SOURCE_EDIT for unfinished equipment/knowledge changes; the previous r29 PASS does not cover them.
- [x] Verify exact wear and knowledge progress: r30 current-source compilation and 6/6 groups PASS (9 research transaction, 5 equipment receipt, 8 knowledge runtime subgroups plus existing suites). Main owns multi-owner semantics; Terra supplied the reviewed independent equipment receipt regressions.
- [x] Add research/ledger sequence cross-section preflight on capture and restore; r31 passes both interfaces, missing/stale/duplicate/canonical-key rejection and retired/pending identity accounting. Current compilation and all 7 focused groups PASS, including runtime composition; full owner-state/durability digest join remains open.
- [x] Revalidate r29: all 19 source hashes still match, Unity is absent. The previous goal turn made verified implementation progress. Main opens SOURCE_EDIT for exact equipment context; a read-only agent maps the knowledge-residue routing branch.
- [x] Propagate authority-captured equipment identity/durability through both work-service branches; r30 passes typed receipt/query and focused outbox JSON checks. Knowledge progress is distinct from final reward completion and resumes finalization without repaying work.
- [x] Resume after AGENTS-only maintenance (no ledger progress in that turn): r26's 12 hashes still match and Unity process count is zero. Main owns SOURCE_EDIT and Unity verification; the existing research agent maps the leap owner read-only.
- [x] Prepare special research results without consuming trait state; jointly commit research/shop/trait/outbox. r29 passes failure/capacity/determinism, signed/zero progress, trait and pending-outbox JSON, immediate completion and post-commit equipment faults.
- [x] Revalidate no Unity process; root AGENTS and the research owner/caller/save boundaries inspected. Knowledge-base is stale (565 failures); source is authoritative.
- [x] Connect normal project and legacy blueprint work through detached research/shop calculation, capacity reservation and rollback-safe domain/outbox commit; notify only afterward.
- [x] Persist the research outcome sequence; focused tests prove progress/completion, capacity/commit failures, pending delivery and JSON continuation (not full game save roundtrip).
- [x] Current source compiles; r25 proves 19 existing groups plus research transaction, and r26 proves both research groups. Twelve changed source hashes are recorded in r26/research-source-evidence.json.
- [x] Remove the direct leap/completion bypasses and isolate durable-equipment finalization faults after successful effects. Main reviewed Terra's five post-commit fault regressions; r29 also passes the existing research equipment adapter suite.
- [ ] Prove combined research/equipment/outbox save-generation restore join and actual runtime leap/UI paths before promoting the whole research producer. Knowledge reward/physical input/outbox atomicity and post-commit identity-event effects remain open. Refresh the final source manifest only after the relevant source freeze.

Window: SOURCE_EDIT / EDIT_READY. r36 PID 33864 exited 0; Unity processes 0, C# errors 0, focused groups 13/13 PASS, with 66 source/meta hashes in r36/knowledge-source-evidence.json. r34 compile failure and r35 missing direct-subject-link failure are retained with their source hashes. Full Phase80 remains incomplete; r10 is a historical classification baseline, not a new frozen-source closure claim.

## Previous implementation batch: Phase80 defense live closure — 2026-09-19

- [x] Re-read the approved 826-line ledger contract; no Unity process or active source-writing agent found.
- [x] Confirm r46 compile evidence and the r1 party-readiness failure; current verifier includes the deterministic fixture and room save-phase correction.
- [x] Run create-only defense PlayMode r2: failed before combat on premature empty-evidence skill generation during actor initialization.
- [x] Inspect focused r20: 16/17 groups pass; new composition test selected its lifecycle mode after injection and failed before exercising the fix.
- [x] Verify corrected fixture ordering in focused r21: 17/17 groups PASS, Unity process exit 0.
- [x] Defense PlayMode r3: 3 real exchanges, 2 acknowledged damage outcomes, victory PASS; whole run FAIL on medical SupplyUnavailable.
- [x] Fix the demonstrated medicine fixture's obsolete destination; validate the runtime-owned one-item claim before seeding.
- [x] Defense r4 verifies actual medicine consumption and treatment; recovery still fails because the fixture stocked only one low-potency dose.
- [x] r5 confirms combat again; a real downed guard exposed a verifier bug that still required two healthy workers to inject another injury.
- [x] Inspect terminal r6: combat/damage ledger verified; medical recovery FAIL. Independent capture fails on a living Active intruder without an invasion/captivity save owner.
- [x] Enforce actual defeated state before defense rewards/terminal; r22 focused and r7 live prove refusal, event-driven terminal and captivity/save ownership. Full save roundtrip/terminal transaction coverage remains open.
- [x] Reproduce medical surface-alias healing loss before the fix; verify canonical treatment/general-heal budgets with Human/Slime regressions in r23.
- [ ] Complete live medical recovery. r8 reaches 53.554/58.08 before the old shared deadline; r9 fails earlier because the front collapses after two exchanges. Preserve failures; isolate medical evidence from already-proven combat or establish reproducible authored combat conditions before a changed run.
- [ ] Fix any demonstrated failure, or promote the exact producer disposition only when covered by evidence; then continue remaining owners.

Window: SOURCE_EDIT / EDIT_READY; r9 exited 1, no Unity process remains. Current production source passes focused r23; latest live test does not prove medical completion. Next work must address its fixture/phase boundary or another pending producer, not repeat the unchanged scenario to seek a lucky PASS.

## Current documentation task: 2026-09-19

- [x] Confirm Unity AGENTS scope and inspect existing guidance.
- [x] Reduce AGENTS to 119 lines; retain fallback, authority, exclusive Unity windows and model delegation.
- [x] Move conditional audit/architecture/CLI procedures to three docs/dev references; link only verified tools.
- [x] Verify 82 local documentation links/anchors and whitespace diff; no Unity or gameplay checks for this docs-only change.

This maintenance task does not change the approved Phase80 gameplay scope below.

## Active implementation batch: production command outcomes — 2026-09-19

- [x] Add a persisted monotonic production-command outcome sequence and pending outbox; bump the production-bill save DTO to v25 and accept legacy zero as sequence 1 on restore.
- [x] Add the typed production command receipt, adapter, descriptor, perspective projector and prepared owner transaction bridge.
- [x] Connect every state-changing `ProductionBillCommandResult` producer. Reversible policy commands roll back on definite rejection; bill add/remove and stock-sensor install/acknowledgement/removal persist a frozen outbox row and retry without rerunning gameplay.
- [x] Add command outcome save/fault/replay/no-op regressions, invalid frozen-row rejection, Add/Remove coverage and exact three-transition stock-sensor coverage.
- [x] Compile r108 and run focused r62: 41/41 PASS, including both production-command steps. The r60 failure was an invalid locked-research fixture; r61 recorded the exact gate failure and r62 verifies the corrected already-unlocked setup.
- [x] Run the full save suite: r25 passes 79/79 on unchanged production runtime/save code. The report passed before a separate shutdown autosave warning, which remains a different checkpoint-topology concern.
- [x] Promote only `ProductionBillCommandResult` and regenerate r27: reviewed 323/323, connected 32, pending 63, authority 0, unreviewed 0, stable source digest and 10/10 matching artifact hashes.

Window: SOURCE_EDIT / EDIT_READY. Unity and dungeon-player MCP remain unused. Human/training gates are unchanged.

## Active implementation batch: industrial infrastructure command outcomes — 2026-09-19

- [x] Inventory the 13 shared command mutations across power, fluid, conveyor and automation. The generated knowledge base is stale with 798 freshness failures, so current C# is authoritative.
- [x] Add one C#-owned industrial command sequence/pending-outbox save section and a typed `infrastructure.command-applied` bridge with frozen facility context.
- [x] Route reversible commands through prepare → mutation → durable commit with aggregate rollback on definite rejection; successful no-ops must not consume a sequence or emit an outcome.
- [x] Persist conveyor overflow approval on the payload. Queue its outcome before physical discharge finalization so delivery/physical retry never reruns the player command or loses approval on save.
- [x] Validate malformed/duplicate/stale/null outbox rows, save/restore replay and composition registration. Focused r65 passes 42/42; real-DI PlayMode r4 proves all 13 changes plus 13 repeated no-ops, owner revision 1→14, real persistence isolation and canonical world restoration.
- [x] Compile r117 with zero C# errors, pass the affected full save roundtrip r27 at 80/80, and promote only `InfrastructureCommandResult` in immutable inventory r29. The stable inventory remains fully reviewed at 323/323 with 33 connected, 62 pending, 0 authority fixes and 0 unreviewed rows; all 10 artifact hashes match.

Errors encountered:

- A broad `rg` alternation for save-section declarations had an unclosed quoted group. It changed no files; subsequent discovery uses literal/single-pattern searches instead of repeating that expression.
- A production-pattern search named a nonexistent `Assets/Scripts/Services/Production` directory. It changed no files; the corrected repository-wide search found no separate production transaction interface, so the industrial shared owner remains its own explicit abstraction.
- A guessed `ConveyorPersistenceAdapter.cs` path did not exist. It changed no files; exact symbol discovery is used before the next persistence read.
- A guessed industrial-folder path for `AutomationStateSession.cs` did not exist. It changed no files; symbol discovery located the session in `Assets/Scripts/Models/Automation/Core/AutomationCoreModels.cs`.
- Compile r109 failed with eight unique current-source errors: the automation model assembly's internal aggregate/mutation methods were inaccessible from the runtime assembly, and two `InfrastructureStatus ==` comparisons were invalid. Preserve r109; replace the leaked aggregate with a public opaque mutation token and use value `.Equals` before r110.
- Compile r110 removed every r109 production error and failed only in the new editor scenario because it omitted `using DungeonStory.Foundation` for `GameEventBus`. Preserve r110 and add the missing import before r111.
- Live r1/r2/r3 and compile r112/r115 are preserved diagnostic evidence: r1 exposed missing report-directory setup, r2 exposed the occupied-grid assumption and restore-epoch comparison, r3 exposed whole-struct `InfrastructureStatus` no-op comparison, r112 exposed a definite-assignment error, and r115 exposed one missing parenthesis. Each retry used a new immutable revision.

Authority: ScriptableObject abilities remain immutable definitions; the four industrial runtimes retain gameplay state; the new owner stores only the shared command sequence and frozen pending outcome rows. Missing outcome composition fails the command explicitly. No Unity or dungeon-player MCP.

Window: `SOURCE_EDIT / EDIT_READY`. Compile r117 has zero C# errors, focused r65 passes 42/42, real-DI industrial r4 passes 13 changes plus 13 no-ops, full-world r27 passes 80/80, and immutable inventory r29 promotes only `InfrastructureCommandResult`. Unity and dungeon-player MCP remain unused; human/training gates are unchanged.

## Active implementation batch: work-completion identity outcomes — 2026-09-20

- [x] Classify the exact authority boundary for `WorkCompletedIdentityEvent` across direct producers and the existing durable autonomous-harvest delivery path.
- [x] Add the smallest C#-owned receipt/transaction integration that preserves identity-state rollback, replay identity and explicit failure without claiming the underlying production, equipment or surgery mutation as its authority.
- [x] Cover direct and durable delivery, replay/no-op, malformed restore, save roundtrip and fault boundaries with focused regressions.
- [x] Compile and run focused/full-save evidence on current source, then promote only the reviewed inventory row after every required gate passes.

Compile r120 passes with zero C# errors. Focused r67 passes 43/43, full-world r28 passes 80/80 with canonical baseline restoration, and stable inventory r30 promotes only `WorkCompletedIdentityEvent`: 323/323 reviewed, 34 connected, 61 pending, 0 authority fixes, 0 unreviewed, 84 Phase80 infrastructure rows and 10/10 artifact hashes. Compile r118 is retained as the fixture-constructor failure that led to adding the real outcome dependency; r119 first passed before the definition file was moved under the narrative bridge boundary.

Window: `SOURCE_EDIT / EDIT_READY`. This batch treats the character identity reaction as its own result boundary; it does not fabricate atomicity for the upstream work-domain mutation. Unity and dungeon-player MCP remain unused; human/training gates are unchanged.

## Active implementation batch: captive ransom outcome — 2026-09-20

- [x] Replace the post-state-only `CaptiveRansomedEvent` authority with one typed `CaptiveRansomOutcomeReceipt`, registered adapter/descriptor, sparse subject memory and Korean perspective projection.
- [x] Prepare before captivity mutation, persist a recoverable `Ransom` pending state, restore it on definite commit rejection, and retain delivery-pending as durably committed.
- [x] Finalize treasury credit through the existing idempotent economy ledger, then retry terminal physical custody cleanup and release without duplicate payment or observer publication.
- [x] Persist and validate ransom revision, accepted amount, credit and observer state; retain compatible legacy released/ransom saves without inventing missing outcome facts.
- [x] Verify current source: compile r132 has zero C# errors, focused r75 passes 46/46, and full-world r34 passes 80/80 with canonical baseline restoration.
- [x] Regenerate immutable inventory r36 from all seven reviewed disposition groups and confirm only `CaptiveRansomedEvent` moves from pending to connected with matching artifact hashes.

Window: `SOURCE_EDIT / EDIT_READY`. Inventory r36 is stable and hash-valid at 323/323 reviewed, 37 connected, 58 pending, 0 authority fixes and 0 unreviewed; all 10 artifact hashes match. The r34 report completed before Unity stalled during final process teardown; only the verified run PID and its exact children were stopped after `Cleanup mono`. Unity MCP and dungeon-player MCP remain unused.

## Active implementation batch: captivity interaction outcome — 2026-09-20

- [x] Revalidate the live owner boundary: interaction materials already use a durable Sink receipt, while captive deltas, optional loose output and aggregate body damage currently occur after work completion without one gameplay-outcome transaction.
- [x] Give every started interaction a stable saved attempt identity and freeze the exact terminal result, warden, housing facility, applied social deltas, physical output and body-health projection.
- [x] Prepare the typed outcome before captive/body mutation; restore both owners on definite rejection and retain a committed pending terminal on ambiguous/delivery-pending results.
- [x] Publish optional extraction output through the existing idempotent physical-source service, then complete body/lifecycle observers and interrogation publication without rerunning the handler or consuming materials twice.
- [x] Validate pending/torn/legacy save shapes and cover all ten authored interaction handlers, rejection, delivery pending, output retry, lethal damage, interrogation composition, replay/drift and save roundtrip.
- [x] Compile, run focused and full-world save evidence, then promote only `CaptivityInteractionResult` and regenerate the reviewed inventory. Compile r139, focused r77 (47/47), full-world r35 (80/80) and immutable inventory r38 all pass. The reviewed delta from r36 is exactly this one row; 10/10 artifact hashes match.

Window: `SOURCE_EDIT / EDIT_READY`. This batch is closed at 323/323 reviewed, 38 connected, 57 pending, 0 authority fixes and 0 unreviewed. r37 is preserved as the scanner-classification failure that exposed `CharacterPreparedAggregateDamageReceipt`; r38 classifies that transaction token as Phase80 infrastructure and is source-stable with 10/10 matching hashes. The next reviewed producer is `CharacterBodyHealthDownedEvent`. Unity MCP and dungeon-player MCP remain unused; human/training gates are unchanged.

## Active implementation batch: body-health lifecycle transition outcomes — 2026-09-20

- [x] Recheck the reviewed row and live source. The knowledge base is stale with 915 failures, so current C# is authoritative. `SyncLifecycle` is the sole publisher; six systems consume the actor-only event after body state mutation.
- [ ] Add a saved per-character monotonic transition identity and frozen pending outbox for the canonical downed/recovered state change. Reserve capacity before mutation, join pending state to the body owner, and preserve the exact receipt across delivery retry.
- [ ] Integrate direct body mutations and the existing combat, work-accident, environmental-fire and captivity outer transactions so rollback cancels the child transition while successful parent commit completes it before legacy observers.
- [ ] Register typed adapters/descriptors, sparse memory and Korean perspective projection; validate strict current/legacy save shapes and deterministic replay/drift.
- [ ] Add focused normal/fault/save tests for downed and recovered transitions, consumer notification ordering, nested-parent rejection and pending delivery, then compile/full-save and promote only evidence-proven manifest rows.

Window: `SOURCE_EDIT / EDIT_READY`. Main owns the transaction and test design. No Unity process is running. A post-state `Record` call or observer-only bridge is explicitly insufficient; the saved body/outbox boundary must survive rollback, restore and idempotent redelivery.

Updated: 2026-09-20

This file contains only the current cross-repository implementation state. The complete
pre-cleanup planning history is recoverable from Git commit
`6e77756f8909bcb23cc91f2698e5129e90eec123`.

## Active authority

- Implementation contract: `tools/Documentation/gameplay-outcome-narrative-ledger-plan.md`
- Narrative-AI handoff: `../DungeonStoryNarrativeAI/HANDOFF.md`
- Documentation map: `DOCUMENTATION.md`
- Unity MCP and `dungeon-player` MCP remain disabled and must not be used.

## Current goal: Phase80 gameplay-outcome narrative ledger

Record every meaningful committed gameplay result under C# authority, retain or compact
it deterministically, and project the same fact by viewer perspective without a central
result-type switch.

## Completed evidence

- [x] Common ledger, save and memory core implemented with bounded retention and a flat due index.
- [x] Separate influence-use transaction/revision and atomic multi-evidence completion implemented.
- [x] Shared Korean particle adapter and focused static/runtime checks added.
- [x] Unity 6000.3.8f1 current-source compile has zero C# errors in r33. The existing research/equipment/knowledge/save-integrity 9 groups still pass; the newly added commit-gap group fails as documented above. The 19 base groups passed historically in r25, not a new all-group run against r33.
- [x] Narrative-AI gameplay-outcome verifier passes 17/17.

## Remaining completion gates

- [x] Correct focused runtime fixtures/composition; verify 19 base groups in r25 and 2 research groups in r26. Real gameplay and full producer closure remain separate gates.
- [ ] Freeze the source and reduce producer/transaction manifest unresolved or pending rows to zero.
- [ ] Connect all four mechanic-evidence consumers without adding model-owned mechanics or values.
- [ ] Register and verify global, entity and role-aware queries/UI for all five perspectives.
- [ ] Verify save/restore, fault injection, deterministic consolidation and no immediate duplicate delivery.
- [ ] Measure Player warm-path allocation, sustained load, capacity and lease behavior.
- [ ] Generate the final hash-bound manifest/export and rebuild the knowledge base once after source freeze.
- [ ] Only after Phase80 closes, resume Phase79 facility rows, C# replay and the complete 100-row editorial review.

## Invariants

- Existing QA, Review and Export evidence is immutable; new evidence uses a new versioned directory.
- Game rules, legal candidates, effects, numeric values and persistent state remain owned by C#.
- `humanApprovalClaimed=false`, `trainingEligible=false`, and `REJECT_BEFORE_DPO` remain in force.
- No reservoir/full generation, SFT, DPO, GGUF or release promotion occurs before the required gates.
- `SOURCE_EDIT` and `UNITY_VERIFY` never overlap.

## Closed verification gate: migrated producer production-entry 57/57 — 2026-09-20

- [x] Map all 57 `MigratedProducerOutcomeKind` values to domain-grouped production entry scenarios and reject duplicate, missing or unexpected coverage.
- [x] Exercise each real entry/owner under successful commit, definite commit rejection with rollback, and durable commit followed by delivery failure/retry.
- [x] Pass the dedicated r18 runner at exact 57/57 and add it to the focused batch.
- [x] Pass focused r81 at 49/49 and live `GameplayScene` full-world r40 at 81/81 sections with canonical baseline restoration.
- [x] Pass Unity 6000.3.8f1 compile r164 with zero C# errors.
- [x] Refresh the final-frozen inventory/closure after the production fixes: clean source commit `a88a0482d`, inventory r46 and closure r6.

This closes automated production-entry E2E, not a claim that a person manually played 57 UI paths. Unity MCP and `dungeon-player` MCP were not used.

## Re-review — 2026-09-20

- [x] Review production failure boundaries independently of the prior PASS reports.
- [x] Identify concrete missed branches and compare the tests' observed state with all affected domain owners.
- [x] Preserve findings under `Artifacts/QA/MigratedProducerProductionEntry57-Review-20260920-r1/review.md`.

Verdict: CHANGES_REQUIRED. Five P1 production defects reopen the producer atomicity gate; the previous 57-kind suite completion must not be read as complete branch coverage. This review did not change production code or rerun Unity.

## Closed correction gate: migrated producer branch atomicity — 2026-09-20

- [x] Roll back expedition history, rewards, meta, campaign and world state together; keep return sealing inside the same rollback boundary.
- [x] Restore participant recovery/progression and always clear `ReturnFinalizing` when final result commit fails.
- [x] Restore V20 campaign and external effect owners when resolved-event outcome commit fails.
- [x] Join primitive field-meal outcomes and cover floor-rest, latrine and bucket-wash failure boundaries.
- [x] Restore water, needs, deprivation and body state for rejected breakdown world-source drinking.
- [x] Add branch-specific success/reject/delivery-retry scenarios without changing the 57-kind catalog denominator.
- [x] Pass compile r171, dedicated r25 (57/57), focused r83 (49/49) and live full-world r42 (81/81).

The earlier `CHANGES_REQUIRED` remains historical review evidence; this correction gate is now closed. Final inventory/closure hashes still require a later source-freeze refresh and human/training flags remain unchanged.

## Active finalization batch: source freeze, evidence refresh and push — 2026-09-20

- [x] Rebuild the current-source final-frozen inventory and final closure into new immutable evidence directories.
- [x] Verify hashes, closure policy, scoped whitespace and the already-completed compile/runtime evidence on the exact frozen source.
- [x] Curate Git contents: include source, tests, contracts, planning/docs and necessary compact QA evidence; exclude Player builds, transient process files and redundant intermediate run output.
- [x] Commit the frozen source as `a88a0482d` on `codex/wim-implementation`.
- [ ] Commit final evidence/completion records and push the branch to `origin`.

Window: `SOURCE_EDIT / EDIT_READY`. Unity is not running, no further gameplay source edit is planned, and Unity MCP plus `dungeon-player` MCP remain unused. Human/training flags remain unchanged.
