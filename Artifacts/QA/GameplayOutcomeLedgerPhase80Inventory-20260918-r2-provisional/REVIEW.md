# Gameplay Outcome Ledger Phase80 — Batch 0 producer inventory (provisional)

## Verdict

This package is a **provisional static inventory**, not final coverage evidence. It does not run Unity, the Unity CLI, Unity MCP, or `dungeon-player`, and it makes no production C# changes. The source was stable for the latest provisional interval, but production writers have not been globally declared frozen, so the final double-digest scan is deliberately deferred.

- Source branch: `codex/wim-implementation`
- Source commit observed: `c03d2e0a1c68067de48840f3851b388483966a6e`
- Latest provisional start digest: `5e5a10f07aac9d7a9833a5f21c09b845bd166bc0a1d1931952bf6069d30b5ef7`
- Latest provisional end digest: `5e5a10f07aac9d7a9833a5f21c09b845bd166bc0a1d1931952bf6069d30b5ef7`
- Snapshot stable during latest scan: **yes**
- Completeness claim: `withheld-production-writers-not-frozen`

`metadata.json` and `artifact-sha256.json` are the machine-readable authority for the provisional run. A later `--final-frozen` scan must replace these observations before a completeness claim is allowed.

## Inventory result

The scanner inspected every `.cs` file below `Assets/Scripts` except paths with an `Editor` segment. The latest provisional input contained 2,022 such files.

| Measure | Provisional count |
|---|---:|
| Definitions whose type name ends in `Receipt` or `Event` | 167 |
| Raw lexical `.Publish(...)` call sites | 194 |
| Captured `.Publish(...)` call sites | 194 |
| Unresolved Publish event/method type | 0 |
| Unknown Publish receiver kind | 0 |
| Unclassified domain definitions | 0 |
| Semantic `Record` candidates | 127 |
| Semantic `NonResult` definitions | 39 |
| New Phase80 implementation-infrastructure definitions | 1 |
| Record candidates missing owner transaction integration | 126 |
| Record candidates with no producer authority | 1 |
| Connected pre-existing Record producers | 0 |

The 127 Record candidates are exported in `record-integration-input.csv`, one deterministic row per type, with exact definition and producer locations, current transaction boundary, save-owner candidates, participants, metrics, and consumers.

### Record candidates by plan section 12 domain

| Group | Domain | Count |
|---:|---|---:|
| 1 | 작업·생산·품질·연구·건설·수리 | 21 |
| 2 | 전투·침입·의료·생포·사망 | 21 |
| 3 | 원정·외부 활동·세력·사건·축제 | 23 |
| 4 | 관계·욕구·기분·생애·가족 | 23 |
| 5 | 거래·재고·재정·서비스·손님 | 22 |
| 6 | 장비·특성·기술·시설 진화 | 8 |
| 7 | 환경·농업·야생동물·질병·재난 | 9 |

## Classification contract

The inventory uses two independent axes so that factual semantics are not confused with current integration quality.

1. `coverage_disposition`
   - `Record`: a result-bearing receipt or a completed/resolved gameplay state transition that belongs in the outcome ledger.
   - `NonResult`: request, candidate, preview, warning, UI projection, diagnostic, start/progress/lifecycle notification, interface, or embedded detail whose parent/source result is the authority.
   - `N/A`: Phase80's own newly added infrastructure, which is explicitly not counted as a pre-existing producer.
2. `integration_status`
   - `transaction-integration-pending`: semantic Record with a producer, but no owner-specific `TryPrepare`/joint outcome outbox/save/replay proof.
   - `needs-authority-fix`: semantic Record without a live authoritative producer.
   - `excluded`: NonResult.
   - `new-phase80-infrastructure`: implementation infrastructure kept outside the pre-existing producer count.

The legacy `classification` column remains a conservative combined view: until transaction integration exists, semantic Record candidates appear as `needs-authority-fix`. Consumers planning later batches should use `coverage_disposition` plus `integration_status`, not the combined column alone.

Specific excluded alternatives are recorded per row. Examples include:

- `ResearchProgressEvent`: progress signal; use completed research plus compacted progress metrics.
- `BossInvasionStartedEvent`, `InvasionStartedEvent`, `InvasionSpawnedEvent`, `InvasionFinalCombatStartedEvent`: lifecycle start notifications; use later breach, damage, death, and resolution authorities.
- `OperatingDayReportEvent`, `RunResultReadyEvent`, `InvasionCombatReportReadyEvent`: derived report-ready wrappers; use their committed component outcomes.
- embedded `*SliceReceipt` / line receipts: use their parent aggregate receipt.
- `CharacterActivityEvent`: legacy character-log projection, explicitly not gameplay truth.

## Orphan that blocks authority completion

`FestivalOutcomeEvent` is a semantic Record candidate at `Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityRuntime.cs` but has no lexically proven constructor/factory/publish producer. It is only consumed by `FestivalIdentityEventAdapter.Start`, which subscribes and applies a mood consequence. The later integration batch must either:

1. connect the actual festival settlement authority to produce it at a committed boundary, or
2. remove/replace the dead contract with the authoritative festival result type.

It must not be marked connected merely because a subscriber exists.

## Publish resolution

The repository uses the method name `Publish` for both game events and unrelated operations. The raw-call artifact therefore retains all 194 lexical calls and classifies each receiver.

- Generic `IGameEventBus` wrappers are recorded as `<generic:TEvent>`.
- the internal `EventChannel<TEvent>.Publish` dispatch is recorded as internal dispatch, not a producer.
- character object factories, restore coordinators, projections, and diagnostics fixtures are recorded as `<non-game-event-method>`.
- the chained death event factory in `CharacterBodyHealthStateRules` is resolved to `CharacterDeathEvent`.

No raw call was discarded to reach unresolved zero. `publish-calls.csv` and `publish-calls.json` preserve the evidence line and resolution method.

## Save ownership

The scan found 78 concrete save-section declarations in the provisional snapshot, including the concurrently added Phase80 `GameplayOutcomeLedgerSaveSection` with ID `world.gameplay-outcome-ledger`. This new save section is implementation infrastructure; it does not prove that any of the 127 pre-existing producers is jointly committed to it.

For each pre-existing candidate, `outcome_save_owner` therefore remains `none-found` until an actual adapter/outbox path is proven. `domain_state_save_owner_candidates` lists the existing state owners that a later integration must audit; it is not evidence that the receipt/event itself is persisted.

## Highest-priority integration evidence

### Production completion

Primary files and symbols:

- `Assets/Scripts/Models/Economy/Content/ProductionBillRuntime.cs` — `ProductionBillRuntime.ExecuteWork`
- `Assets/Scripts/Models/Production/Core/ProductionRecipeExecutionReceiptContracts.cs` — `ProductionRecipeExecutionReceipt`
- `Assets/Scripts/Services/Economy/ProductionRecipeExecutionReceiptAuthority.cs` — `TryPublishCompletedCore`
- `Assets/Scripts/Services/Economy/ProductionBillsSaveSection.cs` — production bill save owner (`economy.production-bills`)

The production runtime physically commits output slices, acknowledges pending output, clears work-in-progress and advances the cycle before/around diagnostic receipt publication. `ProductionRecipeExecutionReceiptAuthority` is explicitly opt-in diagnostics and keeps receipts in memory. It is rich enough to seed the first vertical slice—operation/batch/route commit IDs, WIP input, exact output lines and physical slices—but it is not a durable joint outcome outbox. The subsequent bounded change should stage a production aggregate outcome before clearing/advancing, commit it with the bill/output state, and deliver it idempotently.

### Combat damage

Primary files and symbols:

- `Assets/Scripts/Services/Combat/CombatCommandResultApplier.cs` — `Apply`, `PublishCommittedCharacterDamage`
- `Assets/Scripts/Services/Combat/CharacterBodyHealthContracts.cs` — health result contracts
- `Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs` — `ApplyCombatResult`
- `Assets/Scripts/Models/Combat/Core/CharacterCombatCommandModels.cs` — command termination result
- `Assets/Scripts/Services/Combat/CombatSaveSections.cs` — `combat.body-health`, `combat.commands`

`CombatCommandResultApplier.Apply` reads health-before, calls the in-place void mutation `CharacterBodyHealthRuntime.ApplyCombatResult`, reads health-after, then publishes injury/damage/death observers. The current mutation has neither a detached receipt nor rollback token, so publication is post-state and cannot prove joint commit. The bounded fix must compute or stage a damage receipt before mutation, or introduce a durable body-health outbox committed with state, while preserving actual-damage calculation and death/downed transitions.

## Bounded subsequent production file set

Core infrastructure is owned by the Phase80 core writer and is not part of this inventory edit:

- `Assets/Scripts/Services/Narrative/GameplayOutcomes/**`
- `Assets/Scripts/Services/Infrastructure/Registration/DungeonWorldSimulationRegistration.cs`
- `Assets/Scripts/Services/Infrastructure/Registration/DungeonSaveRegistration.cs`

The first producer-integration batch should remain bounded to:

- production: `ProductionBillRuntime.cs`, `ProductionBillModels.cs`, `ProductionRecipeExecutionReceiptContracts.cs`, `ProductionRecipeExecutionReceiptAuthority.cs`, `ProductionBillsSaveSection.cs`
- combat: `CombatCommandResultApplier.cs`, `CharacterBodyHealthContracts.cs`, `CharacterBodyHealthRuntime.cs`, `CharacterCombatCommandRuntime.cs`, `CharacterCombatCommandModels.cs`, `CombatSaveSections.cs`

Registration and core-ledger files should be changed only by their current owner or after an explicit ownership handoff.

## Knowledge-base and scan history

Knowledge-base queries were executed before source inspection.

- observation query `GameplayOutcome Receipt Publish`: fresh, 0 hits
- persistence query `DungeonStrictJsonSaveSection save registration`: fresh, 0 hits
- code query `CharacterActivityEvent`: fresh, 1 hit at `docs_final/knowledge-base/code/systems/services-character.csv:192`
- content digest: `7be6a15e848d0380aacab78d9d4d4aad76f0fbc6f7bd08f9b3063b7a37d11f69`
- system digest: `e5817ec1064a78bb6ff1806707ed9a8aa7a4b11788460fc5485ad118d52bb876`

The first grouped KB invocation timed out after partial output and was rerun as bounded individual queries. A broad early `rg` of activity/log consumers produced truncated output; later evidence was collected with narrow type/path queries.

Initial source probing found no `GameplayOutcome` implementation symbols and 10,804 dirty entries. During inventory work, another writer created `Assets/Scripts/Services/Narrative/GameplayOutcomes/**`; dirty entries rose through 10,810/10,811 and non-Editor C# count rose from 2,021 to 2,022. The new types are therefore labeled `phase80-implementation-infrastructure` and excluded from the pre-existing producer count. Multiple later scans observed different source digests, including an unstable `89a131...` → `5e5a10...` interval. The latest provisional interval was stable at `5e5a10...`, but this is not a substitute for the orchestrator-declared frozen final scan.

## Limitations and final rescan gate

- This is lexical source analysis, not C# semantic compilation. Reflection, aliases, delegate forwarding, and runtime-only reachability can require owner confirmation.
- Object construction proves creation, not commit. Subscriber presence proves consumption, not authoritative production.
- Participant/metric fields are candidates extracted from names; adapters must verify semantics and role direction.
- Save-owner associations are audit leads only.
- No result-size or runtime burst measurement is claimed because Unity execution was prohibited for this batch.
- No balance behavior changed; this package is inventory/QA only.

Final acceptance requires all production writers to be declared READY/FROZEN, followed by `build_inventory.py --final-frozen`. The run must have equal start/end source SHA-256, raw Publish count parity, unresolved receiver/type 0, unclassified domain 0, and an independent second lexical count. Until then, all counts in this document remain provisional.
