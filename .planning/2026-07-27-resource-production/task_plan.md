# Resource Production Completion

## Goal

Implement the accepted resource production, material crafting, diet, husbandry, waste-loop,
and late-game economy plan as connected physical gameplay rather than disconnected catalogs.

## Phases

| Phase | Scope | Status |
|---|---|---|
| 1 | Audit existing research, items, work, combat equipment, food, wildlife, save, and UI contracts | Complete |
| 2 | Add valid 72-node research and resource/recipe/material/substance authoring assets with usage validation | Complete |
| 3 | Implement physical production bills, exact material delivery, work-unit execution, and output stacks | Complete |
| 4 | Implement gathering, crops, logging, quarrying, processing stations, and resource depletion | Complete |
| 5 | Add equipment primary material instances, stat derivation, repair, salvage, and crafting selection | Complete |
| 6 | Add ingredient-aware meals, diet policies, medicines, drugs, tolerance, addiction, and withdrawal | Complete |
| 7 | Add tame livestock, pens, compatibility, feeding, breeding, products, and auto-slaughter | Complete |
| 8 | Add origin-aware rot, waste policies, compost/feed/fuel/toxin loops | Complete |
| 9 | Add stock targets, contracts, grand projects, forecasting, save sections, and player UI | Complete |
| 10 | Run EditMode/PlayMode regressions, performance tests, captures, and clear Console | Complete |

## Decisions

- ScriptableObjects contain static definitions only.
- Runtime progress, reservations, inventories, health, breeding, and policy state remain in scoped services/save DTOs.
- All non-gold resources are physical item stacks and are moved by hauling.
- New open-ended production and interaction types use registered handlers, not central switches.
- Research remains V16 section-versioned and expands from 24 to 72 deterministic nodes.

## Completion Gate

- Every raw resource has a producer and at least two meaningful consumers.
- Every intermediate reaches at least one final item or persistent sink.
- Production cannot complete before exact physical inputs are delivered and work units are performed.
- Equipment material, meals/diets, substances, livestock, and waste loops round-trip through save data.
- Related regressions pass and Unity Console reports Error 0 / Warning 0.

## Errors

| Error | Attempt | Resolution |
|---|---|---|
| Generated economy assets contain `m_Script: {fileID: 0}` | 1 | Split each ScriptableObject into its own matching `.cs` file, recreated assets, and verified valid MonoScript bindings. |
| Unity MCP dynamic command resolved `CompilationPipeline` under its wrapper namespace | 1 | Use the fully-qualified `UnityEditor.Compilation.CompilationPipeline` name. This does not affect project compilation. |
| Husbandry model inspection used an obsolete service-folder path | 1 | Located the model at `Assets/Scripts/Models/Economy/Core/AnimalHusbandryModels.cs` and used the assembly-owned path. |
| One read-only inspection contained a malformed working-directory escape | 1 | Corrected the literal workspace path; no source change was attempted by the failed command. |
| Registration files were queried under an obsolete Infrastructure path | 1 | Located the V15 files under `Services/Infrastructure/Registration` and used those paths. |
| Crop verifier used an unavailable `InjectGameObject` extension | 1 | Injected the newly added `Facility` component directly through `IObjectResolver.Inject`. |
| Unique repair equipment duplicated while waiting for facility delivery | 1 | Counted `Carried` and `MaintenanceBuffer` equipment as already in transit before issuing another delivery request. |
| Repair haul picked up equipment but stalled before delivery | 1 | Removed a nonfunctional scaled-time pickup delay after the atomic inventory transfer. |
| Repair material duplicated while the first request was carried | 1 | Persisted per-order delivery-issued flags and made equipment/material requests idempotent. |
| Delivered repair equipment was hauled back to storage | 1 | Preserved destination metadata on facility-buffer stacks and treated all routed stored stacks as outbound stock. |
| A shell inspection used a malformed working-directory string | 1 | Corrected the literal workspace path and reran the read-only inspection. |
| Substance AI used an obsolete mood field | 1 | Read `CharacterMoodSnapshot.Value`, the current authoritative mood value. |
| Substance action duplicated a local UI helper | 1 | Removed the duplicate `SetButtonLabel` implementation and reused the existing helper. |
| Husbandry verifier used an obsolete actor constructor call | 1 | Updated the scenario to the current positional `CharacterActor` construction contract. |
| Gameplay compile saw stale Work/Buildings assemblies | 1 | Recompiled the changed asmdefs before compiling `Assembly-CSharp`. |
| Manual gameplay compile omitted new husbandry/waste sources from the stale response file | 1 | Passed every new phase 7-9 source explicitly; Unity Roslyn then compiled with 0 errors. |
| Planning session catch-up script exceeded its time budget | 1 | Recovered from the active task, findings, progress files and current git status instead of retrying the same scan. |
| Production save-section lookup used an obsolete subfolder | 1 | Located the section directly under `Services/Economy` and reused its phase/dependency pattern. |
| Editor Roslyn compile referenced a stale gameplay DLL | 1 | Rebuilt the Bee `Assembly-CSharp` output with all newly imported economy sources, then the editor assembly compiled cleanly. |
| Bee gameplay response omitted new consumables model/runtime files | 1 | Added the omitted sources explicitly to the Bee compile checkpoint; gameplay and editor assemblies then compiled with 0 errors. |
| A broad `rg` query included a nonexistent legacy work directory and a malformed PowerShell pattern | 1 | Narrowed subsequent queries to discovered files and literal patterns. |
| Temporary Unity contract run expected 25 production stations but the authored modular catalog contains P01-P24 | 1 | Replaced the stale magic number with the production-station count derived from `ModularFacilityAssetBuilder.GetCatalogCodes()`. |
| A second temporary Unity contract run was started while the first import was still active | 1 | Let the original process finish and inspect its log before starting any new run. |
| A read-only shell command used an invalid temporary working directory | 1 | Corrected it to the shared workspace before retrying. |
| The final temporary contract process exceeded the shell wait timeout | 1 | Inspected the already-running process and its log instead of launching another copy. |
| The current Recipes asset folder contained 119 files and was briefly mistaken for the authoritative recipe count | 1 | Rechecked the builder source: it defines 126 recipes, so kept 126 as the contract and identified seven missing generated waste-loop assets. |
| Warehouse control-card compile could not resolve a query-service-private disposition formatter | 1 | Added a presenter-local pure formatter for the control label. |
| GameplayScene PlayMode scope failed with a VContainer circular dependency | 1 | Traced the exact path in `Editor.log` (`Deprivation -> Filth -> Exterior -> Medical -> Deprivation`) and replaced the medical-to-deprivation call with a game event. |
| Seven newly restored waste recipe assets reused numeric IDs from copied assets | 1 | Ran the authoritative economy asset builder so all 126 recipes received deterministic unique IDs; duplicate count is now zero. |
| Parallel read command used an unterminated PowerShell string while searching interpolation patterns | 1 | Removed the problematic quoted interpolation pattern and reran the searches with literal single-quoted expressions. |
| A parallel source inspection treated an expected `rg` no-match/obsolete path result as a failed batch | 1 | Narrowed the inspection to known files and read the relevant source ranges directly. |
| Stored physical water could not be consumed through the warehouse aggregate mirror | 1 | Resolve the stable item category, withdraw the exact aggregate amount first, and only then decrement the stored physical stack. |
| Safe drinking never ran while a normal continuous action was active | 1 | Added an explicit survival-emergency interrupt contract: work, hauling, and hunting may yield after minimum persistence while rescue, substance use, and breakdown actions remain protected. |
| Unity target profile does not expose `Queue<T>.EnsureCapacity` | 1 | Removed the unsupported pre-sizing call; newly created pooled path queues still receive the requested constructor capacity and reused queues retain their existing storage. |
| `AIBrain.externalReplanPending` generated CS0414 | 1 | Removed the redundant write-only flag; external actions already request a replan unconditionally when they end, while the meaningful clear-failure request remains preserved. |
| Runtime purchases and offense rewards mutated shared `BuildingSO.unlocked` state | 1 | Moved building unlock authority into `BlueprintResearchState`, migrated legacy shop save IDs through the research save dependency, and added PlayMode contamination regressions. |
| Research-tree construction-gate verifier clicked an offscreen locked node | 1 | Added a public project-centering API and changed it to use actual `RectTransform` coordinate conversion, so pointer verification and the player-facing `선택 이동` action work at any zoom. |
| Final dynamic aggregate command omitted `ResearchTreeDebugScenarios.RunAll(bool)` argument | 1 | Corrected the temporary command invocation and reran the complete aggregate successfully; product assemblies were unaffected. |

## Deferred Optimization Debt

- The accepted feature scope is complete and the production-like `100 staff + 100 livestock + x5`
  profile remains within frame and scheduler budgets.
- Baseline-adjusted incremental GC remains about `143KB/frame`, above the aspirational
  `64KB/frame` allocation target. This is isolated as a follow-up optimization pass rather than
  blocking the completed gameplay loops.
