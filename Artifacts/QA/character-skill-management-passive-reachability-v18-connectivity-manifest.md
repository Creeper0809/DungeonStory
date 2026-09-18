# CharacterSkill management-passive reachability v18

## Scope and status

- Batch: `MPCC-18`
- Status: `COMPLETE / VERIFIED`
- Goal: generated management-passive combinations must contain only modules with at least one reachable current runtime effect for the authored trigger.
- Explicit non-goals: no new effects, values, triggers, scenario-ID exceptions, semantic relabeling, fallback chain, content asset edit, save schema change, stored-skill migration, model inference, training, DPO, GGUF, or release.
- Workspace authority: `F:/01_Programming/01_Project/02_Unity/DungeonStory` at commit `c03d2e0a1c68067de48840f3851b388483966a6e`; the dirty worktree is preserved.
- Read-only comparison workspace: `F:/01_Programming/01_Project/02_Unity/DungeonStoryNarrativeAI`.
- Tool boundary: official local Unity CLI only. Unity MCP and dungeon-player MCP are not connected, registered, or used.

## Current-source reproduction

The six primary v17 source/input files below have byte-identical SHA-256 values in the current worktree and the immutable v17 manifest:

- `CharacterSkillGenerationService.cs`
- `CharacterSkillRuntimeEffects.cs`
- `CharacterSkillCombinationSemantics.cs`
- `NarrativeMechanicScenarioCharacterSkillSource.cs`
- `NarrativeMechanicCatalogExporter.cs`
- `CharacterSkillSystemSettings.asset`

`MPCC-BASELINE-01` compiled and executed `.planning/2026-09-15-management-passive-candidate-consistency/BaselineCapture.cs` in memory against the current editor source. It captured 20 CharacterSkill positive scenarios and 3 CharacterSkill-local negative scenarios.

| Scenario | Legal candidates | With a permanently inactive module | Entirely inactive |
|---|---:|---:|---:|
| `character-skill-authority-09-passive-l01-work-start` | 12 | 12 | 6 |
| `character-skill-authority-11-passive-l01-need` | 12 | 12 | 8 |
| `character-skill-authority-12-passive-l01-mood` | 12 | 12 | 8 |
| `character-skill-authority-13-passive-l25-relationship` | 12 | 12 | 4 |

The matching immutable v17 review independently records the same four tuples and is not treated as human approval.

## Frozen structure contract

| Contract | Authority and rule |
|---|---|
| Content definition | `CharacterSkillSystemSettingsSO` and `Assets/Resources/SO/Character/CharacterSkillSystemSettings.asset` remain the immutable authored module/variant/trigger catalog. No asset value changes. |
| Runtime state | `CharacterGrowthState` remains the sole owner of installed active/passive/ultimate skill instances and drafts. The fix does not rewrite those lists. |
| Command | `CharacterSkillGenerationService.CreateDraft` creates rules; `CharacterSkillCombinationCatalog.Build` is the single legal-combination authority; `TryValidateResponse` accepts only the exact selected legal ID. |
| Query | One side-effect-free management reachability predicate reports whether a module has a current immediate-event or management-query path for a kind/trigger. Catalog generation, draft rule authoring, response defense, and catalog-semantics coverage use it. Runtime effect calculation remains unchanged. |
| Identifiers | Definition IDs, rule IDs, and `skill-combination:sha256(ruleId|mechanicalIdentity)` stay unchanged. A combination that remains valid must retain its exact ID. |
| Save | `CharacterSkillInstance.combinationId` plus exact module selections remain saved source state. No DTO/schema/version change. Existing installed skills are neither deleted nor regenerated; current runtime continues reading their stored modules. |
| Dependencies | Runtime reachability truth lives with CharacterSkill runtime/core code and is consumed by generation and semantics in the same gameplay assembly. Editor scenarios/exporters consume the production catalog and validator; they do not clone the policy. |
| Failure | A rule with zero reachable legal combinations fails with a clear kind/rule/trigger reason before a model request is emitted. A stale/illegal returned combination ID is rejected; no substitution or module deletion occurs. |
| Transition | Only new draft enumeration and outstanding-response validation use the corrected legality. An already installed legacy no-op skill stays byte/field exact. No silent migration is authorized. |
| Verification | Current C# compile; four before/after rows; module event/query matrix; WorkCompleted; cross-trigger non-effects; budgets/domains/duplicates; exact valid ID; stale ID; installed-skill preservation; candidate starvation; 100 positive; 14 prior negative plus new negative; scenario/request/export parity; independent immutable export parity. |

## Runtime reachability matrix

`event` means the existing matching passive dispatch reaches `ApplyOutsideCombat`; `query` means an existing management stat query reads that passive trigger. `self-excluded` remains the existing recursion guard and is not part of the new predicate.

| Module | Immediate event | Passive query trigger(s) | Legal generation triggers after self-exclusion |
|---|---|---|---|
| `work_speed` | none | WorkStarted, WorkCompleted | WorkStarted, WorkCompleted |
| `output` | none | WorkCompleted | WorkCompleted |
| `cleaning` | matching passive trigger | WorkCompleted | WorkStarted, WorkCompleted, NeedChanged, MoodChanged, RelationshipChanged, OperatingDayStarted |
| `repair` | none | WorkCompleted | WorkCompleted |
| `stock` | none | WorkCompleted | WorkCompleted |
| `research` | none | WorkCompleted | WorkCompleted |
| `needs` | matching passive trigger | none | WorkStarted, WorkCompleted, MoodChanged, RelationshipChanged, OperatingDayStarted; NeedChanged self-excluded |
| `mood` | matching passive trigger | none | WorkStarted, WorkCompleted, NeedChanged, RelationshipChanged, OperatingDayStarted; MoodChanged self-excluded |
| `relationship` | none | RelationshipChanged | RelationshipChanged self-excluded, therefore no passive generation trigger |
| `revenue` | none | WorkCompleted | WorkCompleted |

Management ultimates keep all currently authored management modules because their established ultimate event/query routes are independent of passive trigger matching. Non-management active/passive/ultimate behavior is unchanged.

## Connectivity manifest

| symbol-or-id | definition | live-producer | authority | live-consumer | save-or-recompute | player/ai-observation | deterministic-test | status | evidence |
|---|---|---|---|---|---|---|---|---|---|
| management reachability predicate | `CharacterSkillRuntimeEffects.IsManagementModuleReachable` | catalog/draft rule construction | existing runtime branch/query matrix | catalog build, candidate rule, response defense, packet boundary, semantics coverage | recompute; never save | candidate packet and exported semantics | `RunManagementPassiveReachabilityScenario` | connected | all six passive triggers and all 10 management modules passed catalog/semantics/runtime matrix checks |
| legal combination ID | `CharacterSkillAllowedCombination.Id` | `CharacterSkillCombinationCatalog.Build` | rule ID + mechanical identity | prompt, model validator, installed skill | exact ID saved for installed skill | prompt/audit/export | frozen-valid-ID plus stale-ID regressions | connected | v17 WorkCompleted rule/combination IDs remained exact; stale v17 NeedChanged combination was rejected |
| stored legacy management passive | `CharacterSkillInstance` in `CharacterGrowthState` | prior accepted generation/save restore | saved exact instance | existing runtime event/query consumers | exact capture/restore | skill UI/runtime observations | legacy instance preservation regression | connected | full canonical `JsonUtility` payload matched before restore and after recapture; no migration or invented output |
| WorkStarted passive request | current scenario 09 | production `CreateDraft` | production catalog | scenario/export/model packet | draft state | full legal candidates + semantics | before/after reproduction | connected | `12/12/6 -> 12/0/0` for legal/with-inactive/entirely-inactive |
| NeedChanged passive request | current scenario 11 | production `CreateDraft` | production catalog | scenario/export/model packet | draft state | full legal candidates + semantics | before/after reproduction plus stale-ID negative | connected | `12/12/8 -> 4/0/0`; exact v17 stale combination rejected |
| MoodChanged passive request | current scenario 12 | production `CreateDraft` | production catalog | scenario/export/model packet | draft state | full legal candidates + semantics | before/after reproduction | connected | `12/12/8 -> 5/0/0` |
| RelationshipChanged passive request | current scenario 13 | production `CreateDraft` | production catalog | scenario/export/model packet | draft state | full legal candidates + semantics | before/after reproduction | connected | `12/12/4 -> 12/0/0` |
| candidate-starvation failure | synthetic focused fixture | production `RequestDraft` then `Tick` | `RequireLegalCombinations` | caller exception/diagnostic | N/A: rejected before save | explicit rule/kind/trigger error | focused v18 regression | connected | exception included rule/kind/trigger; runtime calls `0`; pending requests `0` |

Final counters: definitions `10` management modules; predicate consumers `4` (catalog, rule drafting, response defense, semantics coverage), with `3` fail-loud request boundaries through the catalog; runtime effect consumers `1` immediate dispatcher plus `7` management query families; direct execution evidence `1` focused matrix/runtime suite plus all `20` production CharacterSkill scenarios inside the `100`-positive bundle and all `15` negative validations; orphans `0` in the changed reachability contract.

## Balance record — `밸런스 영향 없음`

| Required field | Record |
|---|---|
| 1. Change | Remove permanently unreachable management-passive module/trigger pairs from new candidate enumeration. |
| 2. Era | All eras that use CharacterSkill generation. |
| 3. Role | Candidate legality correction; no effect-strength change. |
| 4. Before | Authored `Allows` could expose a module although neither immediate execution nor a stat query could observe it under that trigger. |
| 5. After | A generated management-passive module needs at least one existing reachable event or query effect. |
| 6. Existing alternatives | Existing active, passive, ultimate, and management modules/variants remain authored and numerically unchanged. |
| 7. Advantage | New choices no longer spend budget on permanently inert modules. |
| 8. Disadvantage/tradeoff | Some trigger contexts expose fewer legal combinations; no replacement effect is invented. |
| 9. Physical BOM | Unchanged. |
| 10. Direct/embedded WU | Unchanged. |
| 11. Calendar/space/infrastructure | Unchanged. |
| 12. kg/price/economy | Unchanged. |
| 13. Power/water/waste | Unchanged. |
| 14. Reversible/irreversible risk | New invalid responses are rejected; installed legacy skills remain exact. No save migration. |
| 15. Attention cost | Fewer inert choices; request cardinality remains the authored one-rule passive contract. |
| 16. Exploit loops | No value, cost, sale, dismantle, reroll, reward, or probability path changes. |
| 17. Verification | Catalog matrix, runtime/event/query regressions, budgets/duplicates, save preservation, 100+14+new negatives, export parity. |

Checked baseline sections: 4.14 peaceful production trait bands and section 8 mandatory change record. No magnitude, probability, trigger selection, cooldown, cost, rarity budget, progression threshold, BOM, WU/EWU, resource, price, time, space, power, water, waste, reward, or runtime formula changes are authorized.

## Final export and provenance

- Immutable create-new directory: `Artifacts/Exports/NarrativeMechanicCatalog/20260915-v26-management-passive-reachability-consistency`; 12 files, no staging directory, and the v17 directory remains present and unchanged.
- Current commit: `c03d2e0a1c68067de48840f3851b388483966a6e`.
- Catalog hash: `sha256:38706406b7a4192ff8408d34a44c507a4b94ae308da9a82cceb3feb42545bd25`.
- Full source+commit input digest: `sha256:64cd089f854d8bf0e24991d14c3fe9bc20a9a253dcb7b02df881d0f3ee347cfb`.
- Dirty declared-source hash: `sha256:b8264e50c928f420a6108a7144e219875f1a5945f213fd5c66b379d2a6dd9d53`.
- Independent external audit recomputed `11/11` delivery hashes, `290/290` declared source hashes, catalog hash, input digest, dirty-source hash, and current Git commit: PASS.
- Manifest schemas are delivery `2`, manifest `2`, catalog `1`, raw-test-results `1`. Raw result gates are `7 PASS / 4 NOT_RUN`; `humanApprovalClaimed=false`, `trainingEligible=false`, and test evidence remains `not-supplied` by design.

## Knowledge-base evidence

- Final freshness: `fresh`; content source digest `806a1cbd313eb267626f65607ea3330b88f850756f6b258c538211581a6818fa`; system source digest `0537c768ba71f8570b130c07dc58673bd3076acdc82207b9dbcc137c0529731e`.
- Query/areas: `CharacterSkillGenerationService` with `code, authority, observation, implementation, persistence`, limit 12, Markdown format.
- Generated rows checked: `docs_final/knowledge-base/code/systems/services-character.csv:112`, `:203`; `docs_final/knowledge-base/code/observation.csv:340`.
- Direct originals opened: `CharacterSkillGenerationService.cs`, `CharacterSkillRuntimeEffects.cs`, `CharacterSkillCombinationSemantics.cs`, `NarrativeMechanicScenarioCharacterSkillSource.cs`, `NarrativeMechanicCatalogExporter.cs`, `NarrativeMechanicCatalogContracts.cs`, `CharacterProgressionDebugScenarios.cs`, and `V25NarrativeInferenceDebugScenarios.cs`.
- Known unrelated quality exceptions remain visible: 25 unresolved content references and 45 manual-review contents. No new mismatch or unresolved reachability row remains in this batch.

## Evidence levels and NOT_RUN ledger

| Evidence | Status |
|---|---|
| Static/current-source ownership and v17 byte identity | PASS |
| Current editor C# baseline capture | PASS |
| Focused post-fix C# regression | PASS |
| Current-source Unity compile and console | PASS; Tundra build success, 0 errors after cursor 38794 through 38799; one pre-existing obsolete-constructor warning |
| Model-request/export candidate ID and canonical semantics equivalence | PASS |
| Full 100 positive / 14 prior negative / 1 new negative | PASS |
| Independent export byte parity with exact raw-results payload | PASS; 4,636,538 bytes |
| External package/source/provenance hash audit | PASS |
| Actual UI/AI PlayMode | NOT_RUN |
| Real model inference | NOT_RUN |
| Human approval | NOT_RUN; `humanApprovalClaimed=false` required |
| SFT/DPO/GGUF/training/release | NOT_RUN / not authorized |

## Extension-closure status

- Change type: correction inside an existing management capability invariant; no `ParameterContent`, `ComposedContent`, or new capability is added.
- Core content-ID-specific branches: `0` new branches; dispatch is by existing module capability IDs already used by runtime consumers.
- Unregistered capabilities: `0`.
- Synthetic future-content canary: `NOT_RUN` (Hardening P2, not required for this focused Ship P0 consistency repair).
- Future-content extension-closure completion is not claimed by this batch.
