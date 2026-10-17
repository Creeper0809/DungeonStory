# Gameplay Outcome Ledger Phase80 connection manifest

Status: Batch 0 / SOURCE_EDIT / implementation in progress  
Unity branch: `codex/wim-implementation`  
Unity commit at start: `c03d2e0a1c68067de48840f3851b388483966a6e`  
Plan: `Tools/Documentation/gameplay-outcome-narrative-ledger-plan.md`  
Unity MCP / dungeon-player: not connected or used

## Nodes and current seams

| Node | Current authority | Phase80 bind point | Identity | Current status |
|---|---|---|---|---|
| Generic production completion | `ProductionBillRuntime` plus prepared/exact physical-output authorities | `IProductionRecipeExecutionReceiptAuthority.TryPublishCompleted` / `TryFinalizeExactCompleted`, before completed state is cleared | bill ID + cycle sequence + batch/commit IDs | Existing receipt is opt-in diagnostics and not persistently delivered; integration required |
| Character combat damage | `CombatResolutionService` and `CharacterBodyHealthRuntime` | Pure health transition receipt after resolve and before `CombatCommandResultApplier` mutates health | attack operation ID + attacker/defender persistent IDs | Current exact damage is observed after mutation and transient events only; integration required |
| Outcome storage | none | `GameplayOutcomeLedger` | run ID + monotonic sequence, stable result key | First core slice frozen and reviewed; reserve/write/commit/deliver/ack exists, but display snapshot, transactional evidence anchoring, large-record slicing and compacted read defects require a revised core freeze before Unity verification |
| Save | `DungeonStrictJsonSaveSection` registry | `world.gameplay-outcome-ledger` payload v1 | save generation + sequence/outbox/ack/watermark | Detached strict v1 restore root exists; revised compacted/display/anchor contracts, fault tests and Unity verification remain pending |
| Entity log UI | `CharacterLog` 80-row display cache | `CharacterSummaryInfo.RefreshLogText` via outcome query/projector | persistent character ID + outcome ID | Query migration required; legacy free text retained as non-mechanical data |
| Narrative mechanic evidence | `CharacterNarrativeLedger` / `NarrativeRequestContext` | eligibility query mapped from outcome role/metric/causation | outcome ID + subject role | Explicit bridge required; display migration alone is insufficient |
| Korean particles | no shared runtime formatter found | `IKoreanJosaFormatter` adapter | renderer/cache version + display/pronunciation text | BSD-3-Clause commit-pinned minimal port and adapter source reviewed; isolated regression PASS, Unity/UI registration pending |

## Provisional producer coverage

The current static inventory is intentionally non-final because source writers are active. Its latest stable classification pass captured all 194 lexical `Publish` calls with zero unresolved event types and zero unknown receivers, yielding 127 `Record` candidates, 39 justified `NonResult` candidates, one Phase80 infrastructure type, one orphan authority-fix (`FestivalOutcomeEvent`) and 126 transaction-integration-pending records. These counts are routing evidence only; completion remains withheld until every writer freezes and the inventory is regenerated with matching start/end source digests.

## Required connection edges

1. Domain computes immutable result receipt.
2. Outcome recorder validates descriptor/adapter and reserves bounded storage before mutation.
3. Domain mutation and pending outcome token become one recoverable command state.
4. Idempotent delivery appends once, acknowledges pending delivery, then notifies observers.
5. Save captures domain generation plus exact pending/delivered outcome state.
6. Query returns immutable read views; UI and narrative consumers cannot mutate the ledger.
7. Consolidation publishes only complete deterministic deltas and retains exact unprocessed inputs across save/load.

## Verification ownership

- Main agent: architecture, important integration/fault/save/performance tests, Unity compile/test operator.
- Core writer: new `Services/Narrative/GameplayOutcomes` production core and save/DI registration only.
- Korean writer: new `Services/Narrative/Korean` and dedicated third-party source/license paths only.
- Producer inventory writer: deterministic source inventory under its separate QA artifact only.

No prefab, scene, ScriptableObject, Inspector binding, gameplay value, probability, reward, cooldown, AI utility, reviewer decision, human approval, or training state is changed by this manifest.
