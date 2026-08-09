# DungeonStory Current Findings

## 2026-08-09 current base-model mount

- The gameplay composition was already wired to `LocalLlmRequestQueue`, but the repository had neither a GGUF nor an executable host, so the previous V25 state could only fail closed to deterministic prose.
- The official `ggml-org/Qwen3-1.7B-GGUF` Q4_K_M artifact is `1,282,439,264` bytes and fits the 1.5 GB model contract. Its mounted SHA-256 is `d2387ca2dbfee2ffabce7120d3770dadca0b293052bc2f0e138fdc940d9bc7b5`.
- llama.cpp `b10331` supports CPU inference, OpenAI-compatible chat completions, per-request JSON Schema constraints, prompt caching, API-key authentication, and explicit `--reasoning off`; this permits a fully offline base mount without Ollama or CUDA.
- A listening llama.cpp port is not a readiness signal. During model load `/health` returns HTTP 503, so the Unity launcher must wait for authenticated HTTP 200 before exposing the endpoint. The initial runtime smoke caught this and the corrected smoke passed.
- The current mount is deliberately labeled `base-untrained` and `releaseCertified=false`. It proves runtime integration and fallback behavior, not the creative quality or release eligibility of the future DungeonStory fine-tune.
- Windows child lifetime is protected by the existing kill-on-close Job Object. The real Unity smoke ended with zero remaining `DungeonStoryLlmHost` processes. The stock CPU-server development mount is Windows-only; Linux/Steam Deck still requires the dedicated native host path before certification.

## 2026-08-09 V25 narrative corpus research and generation

- The official `Qwen/Qwen3-1.7B` model card identifies the model as Apache-2.0, multilingual, and capable of a hard non-thinking switch through `enable_thinking=False`. It recommends current Transformers support and separate non-thinking sampling guidance. Source: https://huggingface.co/Qwen/Qwen3-1.7B
- The Qwen license permits derivative works subject to Apache-2.0 notice and attribution obligations; the release pipeline must preserve the upstream license and modification notices. Source: https://huggingface.co/Qwen/Qwen3-1.7B/blob/main/LICENSE
- National Institute of Korean Language search results confirm that dictionary/open-API and corpus assets have source-specific copyright rules. The generator will therefore use NIKL materials only to define lexical categories and validate words; it will not ingest or redistribute example sentences until a specific dataset license is verified. Source index: https://www.korean.go.kr/
- Phase 135 copyright boundary: no modern fantasy/martial-arts novel passages, fan wikis, or franchise proper nouns are copied. All training prose is newly composed from DungeonStory stable facts, controlled motif lexicons, and deterministic templates.
- `우리말샘` exposes an official copyright-policy and Open API entry, so it is suitable as a later word-validation authority but not as an assumed free sentence corpus. Source: https://opendict.korean.go.kr/
- The Academy of Korean Studies encyclopedia organizes folklore around reusable structural categories such as imitation tales, bargains, reversals, oath/ritual, place creation, lineage, and communal memory. Phase 135 uses only those high-level narrative categories; it does not copy encyclopedia prose or named-story plots into examples. Sources: https://encykorea.aks.ac.kr/Article/E0063887 and https://encykorea.aks.ac.kr/Article/E0015531
- Current Hugging Face TRL documentation accepts standard or conversational prompt/completion data and can compute loss only on completions. The generator will retain the rich audit envelope while also exporting a training projection with `prompt` and `completion` fields. Source: https://huggingface.co/docs/trl/en/dataset_formats
- No sufficiently precise data.go.kr result was found for bulk NIKL text reuse in this search round. Absence of a verified license is treated as denial: no dictionary example sentence or corpus sentence enters the generated dataset.

## 2026-08-09 V25 dedicated narrative inference

- The former release queue was coupled to an Ollama endpoint even though gameplay rules already had deterministic owners. The player path now launches only a hash-verified `DungeonStoryLlmHost`; the Ollama adapter is Editor-only.
- Equipment history had two authority leaks: string-fragment evidence classification and `playerVisible` gating mechanical effects. Typed evidence now ranks legal effects, while `mechanicallyUnlocked`, `narrativeReady`, and `uiVisible` are independent.
- Character skills, customer persona multipliers, facility proposal IDs, AI goals/impulses, and social reputation previously accepted model-authored mechanical values. V25 preserves their rule values and consumes only prose/trace; skills and equipment have deterministic offline fallbacks.
- Prefix affinity must include the knowledge and culture versions, not just EventId. Initial 2-4 perspectives now share one static-schema request but remain bound to unique persistent CharacterIds and knowledge snapshots.
- A deployable native host and fine-tuned GGUF are not present in the repository. Runtime and release gates therefore fail closed and this is intentionally not recorded as a completed release-model integration.

## 2026-08-08 V21 actionable alert persistence and dispatch

- `EventAlertChoice` previously persisted only label/description, so every callback-backed choice became inert after save restore. Choices now own a stable `ActionId`; alert records also own a stable `SourceId` so an active authored event projects to one alert instead of one alert per operating day.
- `V21ContentAlertChoiceActionDispatcher` decodes society-event, faction-chapter, faction-contract acceptance, and faction-contract outcome actions, rebuilds the current milestone/world snapshot, and calls the atomic `IContentResolutionService`. Failed dispatch leaves the alert open and does not dismiss the source event.
- Active society events and current faction chapters are now projected through the existing alert UI with persisted action IDs. Successful action choices publish their resolved typed-effect event and dismiss only that actionable alert.
- The alert choice cap is four, matching authored life events and service incidents; the old three-choice truncation silently removed a valid authored choice.
- The event-alert save section schema changed, so its exact section version is now 2. Offline Operation, Presentation, main, and Editor Roslyn compiles pass. Unity live-console verification remains blocked by the pre-existing four unanswered bridge requests.
- The same dispatcher now routes planned reproduction start, due festival resolution, recent funeral handling, counseling, and five age-treatment choices. Age treatment creates the existing persistent surgery order; reproduction starts the existing persistent process.
- Legacy festival attendance had ignored authored facility, item, participant, and outcome fields. `IFestivalCommand.Schedule/Resolve` now grades preparation, reserves exact stacks, applies the result to a detached psychosocial state, atomically consumes supplies, then publishes attendance/mood/grief/faction effects.
- Legacy funeral and counseling calls now require their authored operational facility plus `supply:funeral-preparation-kit` or `medical:trauma-care-kit`; both prepare psychosocial state before atomic consumption and publish only after success.

## 2026-08-08 reproduction hereditary authority correction

- Reproduction had mixed two independent ID domains: parent general `CharacterTraitSO` IDs were written into heritable-trait fields, while child general trait construction attempted to resolve inherited heritable IDs as ordinary traits. This made inherited physiology invalid or inert.
- Parent inheritance now reads expressed/latent IDs from `CharacterNarrativeSnapshot`; child general traits continue to come from its archetype, and the child narrative is registered separately with the inherited hereditary IDs.
- New narrative records with no authored hereditary list receive a deterministic, compatibility-filtered 2 expressed + 1 latent set from the exact 24-definition catalog, so hereditary runtime calculations have real inputs in ordinary runs.

## 2026-08-04 Phase 117 risk-classifier precision

- The first conservative classifier treated any mutable scalar or collection on a presentation `MonoBehaviour` as domain authority, producing false `ReviewRequired` results for ordinary view state.
- Presentation and device-edge paths now suppress mutable-field and local enum/delegate authority evidence, while explicit authority names, SO/content definitions, domain models, and runtime/service/policy roles still force named ownership or review.
- This precision change removes 43 false unapproved findings (`776 -> 733`) without approving a mixed owner by manifest explanation and preserves the plan's rule that genuine `ReviewRequired` sources must be split.
- Remaining presentation reviews are now concentrated in plausible mixed files such as feature query/command services, relocation targeting, detailed stats runtime, popup services, and view files that declare rule/service types.
- The host-owned Unity MCP relay is closed and its direct tool binding returns `Transport closed`, but the Unity package bridge itself is healthy. A project-scoped `relay_win.exe --mcp` session completed MCP initialization, discovered the live Editor named pipe, executed `Unity_ReadConsole`, and reported zero Error/Warning entries.
- The reusable project script terminates only the exact relay child it creates; it never restarts the Editor or synthesizes operating-system mouse/keyboard input.

## 2026-08-04 leaf named-assembly migration checkpoint

- The repository has a very large shared dirty worktree (`1913` changed paths in the current diff summary), so migration selection must be planner-driven and must avoid every concurrently active ownership area named by the root agent.
- This worker may move at most 15 source files, must preserve each original Unity `.meta` GUID, and must stop at semantic-planner/source/diff evidence without opening Unity or touching scenes.
- The local `dotnet` host contains only the runtime and no SDK, so `dotnet build` cannot be used for worker compilation evidence; the root agent owns the fresh Unity compile.
- The preceding strict-save checkpoint converted GrandProject, ResourceStockPolicy, RegionalSupplyContract, Faction, DungeonDebug, and RandomStream to current-version detached candidates with invalid-no-mutation and late-discard fixture coverage.
- `tools/AssemblyMigrationPlanner` uses Unity's bundled Roslyn compiler/runtime, so it remains runnable even though the machine-wide `dotnet` SDK is absent. Its deterministic report orders leaf/sink SCCs first and supports a project-source fallback when no current Bee response file is usable.
- The planner semantic self-test passes. Its input loader explicitly falls back from a stale Bee response to nearest-asmdef project scanning when source moves invalidate the response, which is the required clean/project-scan behavior for this dirty worktree.
- Fresh planner report: `885` Assembly-CSharp candidates, `8079` semantic file edges, `330` SCCs, `19` cyclic SCCs, and only `4` leaf SCCs; graph hash `4f09c016ce001adfb0638c90435c79b0bbf627353c9c12c92f9ee7c03e0a0b53`.
- The four leaf SCCs are single files: `GameDomainContentCatalogSO.cs`, `CharacterActorBridges.cs`, `DungeonFactionDefinitionSO.cs`, and `MetaProgressionRuntime.cs`. The active-area exclusion does not immediately rule out the content-catalog or faction-definition leaves, but semantic boundary inspection is required before selection.
- Migration batch order confirms these four isolated leaves precede one enormous cyclic SCC, so the safe checkpoint should select among the four leaves rather than attempting to split or move the active mega-SCC.
- `DungeonFactionDefinitionSO.cs` is the strongest semantic leaf: one serialized SO type, one Assembly-CSharp consumer (`FactionRuntime`), and egress only to `DungeonStory.Factions`, UnityEngine, and netstandard. Its original script GUID is `2141cf61d65c4574b72b89276d3dd67f`.
- The existing `DungeonStory.Factions` asmdef is currently pure (`noEngineReferences: true`), so putting the SO directly into that core folder would force the pure model assembly to take an engine dependency. Before moving, inspect the existing `DungeonStory.Content` direction or another established SO-domain pattern to avoid degrading the core boundary.
- `GameDomainContentCatalogSO` fans out to seven named domains and risks cycles; `MetaProgressionRuntime` is a VContainer MonoBehaviour with active Offense consumers; `CharacterActorBridges.cs` is empty. None is safer than the faction-definition leaf.
- Existing model-domain precedent supports Unity-aware domain asmdefs (`Economy`, `Species`, and `Wildlife` all use `noEngineReferences: false`). Therefore the faction SO can move into the existing `DungeonStory.Factions` assembly by changing that flag only; it requires no new assembly reference and cannot create an asmdef cycle.
- Repository-wide path search found no hard-coded validator/source-contract path for `Services/Factions/DungeonFactionDefinitionSO.cs`; the V18 validator mentions only the type name in the global runtime-SO synthesis regex. No validator path rewrite is needed for this leaf.
- The source and `.meta` moved together into `Models/Factions/Core`; old paths are absent and the preserved GUID is still `2141cf61d65c4574b72b89276d3dd67f`. The serialized SO now carries `[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]`.
- `DungeonStory.Factions.asmdef` now allows engine references but retains an empty named-assembly reference list, so the migration adds no Assembly-CSharp or cross-domain dependency.
- The post-move planner deliberately rejected the now-stale Bee source list and used `project-fallback`: `1120` candidates, `10646` semantic edges, `566` SCCs, `19` cyclic SCCs, and `106` leaf candidates; graph hash `cfdc4d413f40545208a56960aef58c6d964437f16047d7ab3ad1f2a53041d636`.
- Neither the old service path nor the new named-assembly path appears in the post-move Assembly-CSharp candidate list. A repository path scan excluding generated planner/baseline data also found no live hard-coded old source path, so no validator source-path patch is necessary.
- Targeted `git diff --check` passes. Fresh Unity compilation is intentionally deferred to the root agent as required by the assigned checkpoint boundary.

## 2026-08-03 Batch B survival and medical boundary findings

- A recovered need value is not proof that the exterior-water path ran: safe inventory and facility supplies can satisfy thirst first. The PlayMode fixture must exhaust safe stacks, disable facility supply, place an authored unsafe world source, and assert both source consumption and its health consequence.
- Breakdown execution needs a generation-aware handoff. Accepting a new Aggregate generation while an old coroutine still owns the per-character execution slot can strand the new state unless dispatch happens after slot release.
- Strict persistent building IDs make fixture cleanup part of correctness. A temporary facility created before being registered for teardown can leak after initialization failure and contaminate later scenarios.
- The first safe Medical assembly cut is anatomy content, not the whole surgery model. `SurgeryModels.cs` still mixes immutable definitions with `CharacterActor`, `WildlifeActor`, `BuildableObject`, and code-owned stat IDs, so DTOs and runtime ports must be separated before moving it.
- Splitting a default-assembly MonoBehaviour into partial files can change Unity's MonoScript ownership counts even when runtime behavior is unchanged. Architecture baselines must be reviewed by exact type/path delta rather than updated from counts alone.
- `EnvironmentalFieldRuntime.Tick` originally returned while the pre-run clock was paused before initializing its grid arrays. Owner selection starts the calendar and publishes day-one spoilage immediately, so food lookup could throw before the first unpaused tick. Initializing from the available grid before the pause check removes the startup race without adding a fallback cell value.
- CharacterSummary's generated close button called `OnClose()` directly instead of removing the popup through `IUiPopupService`. The stale stack entry was closed again on the next open after the new actor had been assigned, clearing the binding and leaving visible health controls as no-ops. The button now requests a stack-aware close, and opening closes previous popups before assigning the new actor.
- The reviewed Medical ownership delta is two moved default MonoScripts plus one new Unity adapter source: Unity default MonoScripts decrease `1032 -> 1031`, Roslyn default sources decrease `1051 -> 1050`, while mutable statics and large-constructor counts remain unchanged.

## 2026-08-03 Batch A concrete-runtime assembly audit

- All six concrete implementations currently depend on Unity lifecycle APIs or default-assembly concrete event/domain types. Moving any file wholesale into `DungeonStory.CoreSession` would require a named assembly to reference `Assembly-CSharp`, which Unity cannot support and which reverses the intended dependency direction.
- The valid atomic boundary for Batch A is therefore the six engine-independent Aggregate states, contracts, exact save participants, and duplicate-authority removal. Concrete Unity adapters remain at the edge until the corresponding event, item, wildlife, invasion, building, character, research, and economy ports are promoted during cross-domain closure.
- `ExperiencePacingRuntime` is the closest to portable but still uses `Mathf`, VContainer lifecycle, and a default event DTO. ExternalInfluence, RunFlow, RunVariable, DungeonDebug, and ServiceRooms have stronger concrete dependencies, so a blind asmdef move would be architecture regression rather than progress.

## 2026-08-03 Batch A synchronized cutover dependency shape

- The six components are not equally portable implementations. ExperiencePacing and DungeonDebug are mostly plain Aggregate services; RunVariable is a `MonoBehaviour`; ExternalInfluence and ServiceRooms directly depend on world items, wildlife/survival, power, research, buildings, and characters; RunFlow directly depends on invasion implementations. Moving the six files wholesale into the contract-only `DungeonStory.CoreSession` assembly would create reverse domain dependencies and would not be a valid atomic cutover.
- The shared migration seam therefore has to separate domain state/commands from Unity and cross-domain adapters for all six at once. The `CoreSession` assembly owns immutable component state, commands/queries/results, and transaction participant contracts; Infrastructure/Composition owns MonoBehaviour lifecycle and cross-domain adapter wiring. This is one synchronized shape applied across the set, not a sequence of six independent migrations.
- The current named assembly proves only contract ownership. Runtime implementations remain in default `Assembly-CSharp`, so no component is accepted as migrated despite the six-owner save fixture and clean player build.
- `DungeonStory.CoreSession` currently references only `DungeonStory.World` and forbids UnityEngine, while the existing runtime implementations consume many default-assembly concrete types. `DungeonStory.Infrastructure` references only Foundation and cannot legally host those implementations either. The atomic cut therefore needs explicit ports in the named assembly and composition adapters at the edge; merely adding asmdef references would invert the intended dependency graph.
- The current validator only ratchets three contract types (`IExperiencePacingRuntime`, `IDungeonRunFlowRuntime`, `IDungeonDebugModeService`) into CoreSession. ExternalInfluence, RunVariable, and ServiceRooms contracts are still default-assembly declarations, so the executable cutover matrix must extend the assembly check to all six components and distinguish domain implementation from Unity adapter ownership.
- ExternalInfluence's save DTO is primitive-only, but its query snapshot leaks `Vector2Int`; ServiceRooms mixes pure session records with `BuildableObject`/`CharacterActor`; RunVariable mixes state, Unity `Mathf`, localized presentation text, authored definitions, and effect interfaces in one model file. These types must be separated by role before their contracts can move without pulling Unity/presentation and concrete world entities into CoreSession.
- The existing `DungeonStory.World` primitive file itself imports UnityEngine and exposes `Vector2Int`, so using it as the CoreSession boundary does not make all state engine-independent. Batch A should introduce a primitive, serializable cell value owned by CoreSession (or Foundation) for external-raid state rather than widening the no-engine assembly to Unity types.
- Five of the six components currently retain the authored `CoreSessionRulesSO` asset directly. RunVariable already consumes a root-catalog-derived definition catalog. A shared immutable `CoreSessionRulesDefinition` projection created once by `ResourceGameContentCatalog` is therefore the first legitimate six-component content seam: five consumers stop retaining an SO, while RunVariable remains on the same root-derived definition pattern.
- The SO is already validated before `ResourceGameContentCatalog` becomes usable. Copying rehearsal bands, incident kinds, service research requirements, thresholds, costs, and limits into immutable read-only collections at catalog construction preserves authored authority while preventing runtime asset mutation and direct SO coupling.
- The architecture analyzer correctly rejected a single 18-parameter rules constructor. Matching the SO's authoring sections with three immutable runtime value objects keeps constructor limits intact and makes each rule family's ownership explicit without changing consumer-facing semantics.
- After the split, the root catalog/content proof, six-component save/integration fixture, V18 authority validator, Roslyn metrics, and Unity Console all pass together. This proves the shared content seam, but does not prove the remaining runtime/command/save/composition/presentation/legacy-removal matrix cells.
- ExternalInfluence's `Restore(..., DungeonGameRestoreReport)` parameter is unused; validation already belongs to its save section. Removing that save-framework parameter from the runtime port lets its enums, DTO, command/query contract, and a primitive cell snapshot move to CoreSession with only a Foundation dependency for `DomainFailure`.
- RunVariable save capture is currently duplicated inside `RunVariableSaveSection`, while restoration reconstructs runtime objects there and the section locates the MonoBehaviour through `DungeonSceneRuntimeReferences`. A CoreSession `IRunVariableRuntime` capture/restore port can make the save section depend on a real runtime boundary and remove this scene-reference bridge.
- Service domain enums (`ServiceCategory`, operation modes, stage mask) are pure but live beside Unity building abilities/SOs. Moving those enums and pure session records to CoreSession while leaving `BuildableObject`/`CharacterActor` request/view adapters at the Unity edge is the corresponding synchronized contract cut.
- CoreSession can reference Foundation without importing Unity APIs into its own source. That enables External/Service command results to reuse the single `DomainFailure` protocol while the asmdef retains `noEngineReferences: true`.
- Replacing RunVariableSaveSection's `DungeonSceneRuntimeReferences` dependency with `IRunVariableRuntime` removes a real composition-time locator. Capture/restore conversion now belongs to the runtime state boundary; the save section keeps only canonical payload validation and authored-reference validation.
- The synchronized contract move compiles with no Unity errors or warnings: External enums/DTO/query/runtime state, RunVariable difficulty/survival/category/save DTO/port, and Service enums/session/save/query contracts now load from `DungeonStory.CoreSession`; Unity building/character adapters remain at the default edge.

## 2026-08-03 Batch A integrated transaction boundary

- A meaningful vertical batch needs more than six green unit checks. The new fixture constructs the six production runtimes on one event bus and one `DungeonRuntimeAggregateRootStore`, performs cross-owner day progression plus External, RunVariable, Debug, and Service mutations, captures all six sections, and exercises presentation localization in the same call.
- Preflight rejection and late commit failure prove different invariants. Invalid ServiceRooms JSON must stop before any owner restore; a valid six-owner candidate followed by a failing final section must reach the final commit and then leave every live snapshot plus the published root revision unchanged. Both are now explicit production-registry scenarios.
- Test fakes that store state in private fields cannot prove rollback-free behavior because their fields sit outside the candidate Aggregate. The transaction fixture's owner fakes write their DTO types through the shared root, while RunVariable uses its actual Aggregate; candidate discard is therefore observable rather than inferred.
- `CoreSessionRulesSO` must cover every future day because ExperiencePacing intentionally throws when no band matches. Requiring the last band to end at `int.MaxValue` turns that runtime assumption into an authored-content validation rule. Concurrent incident count is independent of the number of incident kinds, so no artificial cross-field cap is valid.
- `DispatchProxy` is used only inside the Editor integration fixture to supply unused dependency surfaces. Production still receives explicit concrete capabilities through composition; the proxy cannot become a runtime Null Object or content fallback.

## 2026-08-03 Batch A command and presentation authority

- ExternalInfluence and ServiceRooms were still dual-purpose APIs: a domain mutation returned a localized Korean sentence, then UI, activity logs, and save-adjacent state copied that sentence as if it were a stable reason identifier. `DomainFailure` is now the only failure authority at those command boundaries; localization is presentation-only.
- Service availability had the same duplication in query form through `BlockedReason`, while mode changes embedded a success/failure `Message`. A query snapshot needs structured blockage state and a command result needs only success plus a failure code; success copy belongs to the presenter.
- Service-room link ordering still synthesized a key from legacy facility number and coordinates when a persistent ID was absent. That made service topology an exception to the V18 identifier contract. Requiring the typed building instance ID is safe because industrial topology and service hub IDs already use the same required identity path.
- Grouping font and failure-localization presentation dependencies as a top-level class created one additional default-assembly MonoScript even though no new file was added. Nesting the dependency value under the existing panel retains constructor grouping without expanding Unity's default MonoScript surface.
- The executable localization validator enumerates every `FailureCode`; adding a domain code without both Shared Table and Korean table entries now fails V18 immediately. The new command boundary therefore cannot silently regress to an untranslated code.

## 2026-08-03 Batch A scoped debug-rule ownership

- `DungeonDebugRuntimeRules` was a hidden global authority despite its `static readonly` wrapper: it retained a mutable mode-service reference and thread-static command depth, and 31 gameplay call sites could read it without declaring the dependency.
- The correct boundary is one scoped `DungeonDebugRuleRuntime`, with `IDungeonDebugRuleQuery` for gameplay reads and `IDungeonDebugRuleRuntime` only for command-depth mutation. ScriptableObject building conditions receive this capability through `BuildingConditionContext` rather than retaining runtime services.
- Explicit dependency routing exposed an existing eight-dependency `WorkOrderRuntime`; grouping workforce, clocks, and debug rules into `WorkOrderExecutionServices` reduced the large-constructor violation set instead of adding a ninth dependency.

## 2026-08-03 Batch A production-count ratchet

- The V18 validator enumerates top-level public production save sections but previously only checked staged-interface presence; it did not enforce the known total or rollback-free conversion count. Batch A can now ratchet `54 total / 32 rollback-free / 22 remaining`, making the plan's `28 → 22` exit counter executable.

## 2026-08-03 Batch A fixture construction strategy

- RunVariableRuntime is a MonoBehaviour whose section constructor only requires the component reference; strict preflight does not touch its uninjected Aggregate when the canonical DTO represents an unstarted run. A temporary GameObject plus empty authored catalogs can therefore test exact-version validation and invalid no-commit behavior without constructing the entire gameplay graph.
- The other five sections accept interface runtimes and can use counting fakes. One central callable Editor suite can prove canonical restore, invalid report/no restore call, required/preflight/rollback-free marker contracts, and the absence of optional-section interfaces for the entire Batch A set.

## 2026-08-03 Batch A V18 ratchet location

- `RuntimeAuthorityV18Validator.ValidateOrThrow()` keeps source-contract ratchets inline near the StaffDiscontent block. Add all six Batch A typed/marker/version/fallback prohibitions there, plus one callable Batch A strict-boundary fixture requirement, so future regressions cannot silently restore optional/migrating paths.

## 2026-08-03 Batch A fixture version impact

- PlayerFairness hardcodes ExternalInfluence V2 and constructs a scheduled raid without warning/sequence/current-day state. Update it to canonical V3 so its DTO round-trip remains representative of a payload accepted by the strict boundary.
- ServiceRoom's success log is the only hardcoded `service.rooms V1` text; the mapper scenario remains valid after switching to exact field copying, but the message and explicit payload-version assertion must reflect V2.

## 2026-08-03 Batch A post-edit call-site findings

- `IServiceProcessCatalog` intentionally exposes only `TryGet`; ServiceRooms validation must not assume a throwing `Get`. Production constructs `ServiceSessionRuntime` and all six sections through composition, so the added required dependencies are resolved by existing registrations rather than manual factories.
- `FirstRunObjectivePlayModeVerifier` is the only direct RunFlow reset call and must now pass explicit `bossCycle: 0`, matching the new non-legacy exact restore contract.

## 2026-08-03 Batch A staged cross-reference confirmation

- The generic section base validates JSON against the live world first, then commits staged sections in dependency order. Facility commit publishes a detached facility candidate to `RestoreWorldCandidateIndex`; Character commit consumes that Grid and publishes detached characters; ServiceRooms runs afterward and can validate saved hub/actor IDs against both candidate lists before replacing its own Aggregate root.
- Commit-time candidate validation remains rollback-free because all earlier world changes are detached transaction candidates and final live publication occurs only after every section commit succeeds. ServiceRooms must add errors and return before its root swap when candidate references are missing, never fall back to live registries or skip individual sessions.

## 2026-08-03 Batch A ServiceRooms candidate-reference strategy

- Save preflight runs before detached character/facility candidate creation, so validating session hub/actor IDs against the current live `IBuildingWorldQuery`/`ICharacterLifetimeQuery` would reject valid loads into a fresh world or validate the wrong world. Cross-aggregate references must use the existing restore-candidate index during staged preparation or the global aggregate reference preflight, not live runtime queries.
- `RestoreWorldCandidateIndex` already publishes detached facility and character candidate views and is used by AI lookups. ServiceRooms can structurally/authored-validate during ordinary preflight, then resolve hub/actor references against `IRestoreWorldCandidateQuery` during its stage after its declared world dependencies have prepared candidates.
- `ServiceSessionAggregateState` is a small replaceable root containing mode/session dictionaries, advertised categories, and a revision. Exact restore should construct this root without trimming/skipping; post-publication hub subscription is already revision-driven and staging-aware.

## 2026-08-03 Batch A call-site and fixture impact

- Batch A sections are composition-injected rather than manually instantiated in production, so dropping ExperiencePacing's obsolete RunFlow constructor dependency is low risk. Public DTOs and runtime methods are used by Editor fixtures, which must be updated in the same source batch.
- `IsFinalInvasionDefended` survives only in `NaturalRunPlayModeVerifier` and the RunFlow save path. The verifier already has authoritative evidence (`bossFightObserved`, defense trigger count, `!IsBossActive`), so the dead property can be removed without replacing it with another saved flag.
- `ServiceRoomDebugScenarios` hardcodes `service.rooms V1` and currently tests the lossy `ToSnapshot` mapper with a synthetic process ID. The fixture must move to the new exact payload version and add validator/no-mutation coverage using authored process/hub/actor references or a dedicated validator dependency seam.
- `DungeonDebugModeDebugScenarios` already exercises the 50-entry cap and transient reset, but it calls runtime restore directly. Add section-level exact-version, invalid-history, and staging-event suppression proof rather than treating this existing gameplay test as sufficient save preflight evidence.

## 2026-08-03 Batch A Experience/External final audit

- ExperiencePacing has a clean plain Aggregate but both runtime and section currently repair missing data, clamp masks/day, skip unknown concepts, and synthesize a missing section from RunFlow. V18 can remove the RunFlow dependency from the constructor, make the section required typed V1, validate mask/subset/active-day/concept order invariants, and perform one exact root replacement.
- ExternalInfluence clones its DTO, clamps scalars/days, trims/deduplicates both ID collections, supports V1 migration, and resets when missing. The section can become required typed and rollback-free once exact current-version validation rejects every repair case and restore only copies validated values.
- `ExternalInfluenceAggregateState.EcologyResolutionReported` is mutable gameplay state used to distinguish Resolved from Inactive, but it is not included in `DungeonExternalInfluenceSaveData`; every save/load currently loses this state. Move it into the versioned DTO (or otherwise persist it) and bump the section payload version instead of falsely claiming an exact V2 round trip.
- External ID lists are already captured sorted. Strict validation should require nonblank trimmed unique ascending IDs. Dread boss/affected IDs require active defense; armed and active are exclusive. Ecology scheduled/in-progress/resolved states are mutually exclusive, scheduled requires positive remaining time, inactive/non-scheduled states require zero remaining time, and active raid state requires positive sequence plus a warning.

## 2026-08-03 Batch A strict-section implementation pattern

- The existing `DungeonJsonSaveSection<T>` already rejects blank/invalid JSON, exact-matches section versions by default, runs typed validation before staging, and captures one immutable payload reference for commit. Batch A manual sections should inherit it instead of duplicating deserialization/stage plumbing.
- The proven `StaffDiscontentSaveSection` pattern is: embedded DTO `CurrentVersion`, constructor-required runtime, deterministic capture ordering, exhaustive typed validator, lossless snapshot mapping, plain Aggregate replacement, and `IDungeonRollbackFreeSaveSection`. Batch A should reuse this shape without adding compatibility migrations in V18.

## 2026-08-03 Batch A RunFlow invariants

- RunFlow canonical phase is fully derived: unfinished runs use Preparation for days 1–3, Growth 4–9, Escalation 10–29, and EndlessDefense from day 30; Victory/Defeat always use Finished. Restore currently ignores the saved phase and recomputes it, so exact preflight must verify this equality and then assign the validated phase directly.
- `finalInvasionDefended` is a dead legacy projection: runtime always reports false and the Aggregate has no field, while restore uses it only to promote `bossCycle`. It should be removed in a new section schema together with the optional legacy restore parameter and interface property, rather than retained as a second representation.
- Boss armed/active are transient mutually exclusive unfinished-run states. `bossCycle` is nonnegative, cannot exceed `ResolveBossCycleForDay(currentDay)`, and armed/active require a positive cycle; finished runs require both flags false. These conditions prevent current restore clamps/filtering from changing validated payloads.
- RunFlow already replaces a detached Aggregate and suppresses threat/director/owner projection during staging. After strict typed preflight and exact field assignment it qualifies as rollback-free; projection remains a post-publication responsibility.

## 2026-08-03 Batch A RunVariable model invariants

- `RunStartVariableSnapshot` normalizes difficulty/survival enums, trims species/layout/doctrine strings, clamps threat multiplier to at least `0.05`, and copies candidate lists. Strict preflight must require values already canonical (`value == Trim()`), defined enums, finite threat `>=0.05`, and non-null candidate lists so construction is lossless.
- `RunVariableAggregateState` clamps zero seed and day below 1; `ActiveRunVariable` clamps start/remaining values; `RunVariableState.Restore` filters null, non-Operation, and expired entries and narrows invasion definitions to the Invasion category. Validation therefore must require nonzero run seed, current/start day `>=1`, active remaining days `>=1`, unique known Operation definitions, and an empty or known Invasion definition before root replacement.
- Operation activation replaces an existing variable with the same definition ID and appends the new entry, so a valid runtime capture has unique operation IDs but not necessarily lexical order. Preserve list order exactly rather than imposing an unsupported sort; canonicality here means unique, non-null, authored entries in runtime order.

## 2026-08-03 Batch A RunVariable initial invariants

- `RunVariableSaveSection` is nominally V2 but the DTO has no embedded version and still accepts/mutates V1. Capture contains a dead `runtime == null` fallback despite constructor-enforced runtime presence; restore also contains an impossible runtime-null warning branch. Both must be removed under required dependency construction.
- Restore currently synthesizes missing start/list data, resolves a missing doctrine from species, skips unknown operation definitions, defaults a zero seed to 1, drops nonpositive random maxima, then reseeds and advances the shared random stream. This is lossy and makes `run.variables` a second RNG authority beside the dedicated random-stream save section.
- The clean V18 boundary should require one exact current DTO, validate every authored variable/doctrine reference and all nested lists before staging, replace the plain `RunVariableAggregateState` once, and remove reseed/replay side effects. The obsolete `randomDrawMaxima` replay field and unused legacy `difficulty` field are candidates for a section-schema bump/removal after fixture/call-site audit.

## 2026-08-03 Batch A DungeonDebug/ServiceRooms invariants

- `CreateContract` guarantees defined hub mode, known mask from the authored mode contract, finite nonnegative reception/waiting/payment/cleanup durations, strictly positive finite service duration, nonnegative price, payment/internal flags, and nonblank support IDs. Satisfaction is not clamped and therefore should only require finiteness. Support IDs are emitted in hub-link order, so strict validation must either prove that query order is canonical or canonicalize capture before requiring sorted order.
- ServiceRooms capture persists only active sessions ordered by `StartedAt` then `SessionId`; Completed/Cancelled payload entries are invalid. Its restored snapshot should preserve every field byte-for-byte after preflight, with no trimming/defaulting, and the runtime command boundary should reject stages absent from the active-stage mask so future captures remain valid.
- DungeonDebug save state contains only `debugModified` and the most recent command history. Capture preserves list order and caps it at 50; exact V1 validation can require a non-null list of at most 50 non-null entries with non-null strings. Runtime-generated text is not constrained enough to justify trimming, parsing, or arbitrary length repair.
- DungeonDebug owns a detached `DungeonDebugModeState`; restore can be rollback-free after replacing its candidate root exactly and suppressing `StateChanged` while staging. Overlay/cheat transient state is intentionally not part of the payload and remains reset by the new root.

## 2026-08-03 Batch A ServiceRooms contract follow-up

- Service process masks contain only Reception/Waiting/Service/Payment/Cleanup, and authored contracts expose nonnegative stage durations/base price, satisfaction, and required feature tags. Hub modes/categories/payment policies are closed enums. Strict save validation can reject unknown mask bits, non-finite or negative durations, negative prices, undefined enums, and noncanonical support IDs without reproducing gameplay calculations.
- The current save captures only `ActiveSessions`, so persisted Completed/Cancelled sessions are noncanonical even though the DTO enum can represent them. Active session stage should match the contract mask, but runtime currently allows arbitrary non-Completed `TrySetStage`; that runtime command boundary must be tightened or the save contract cannot guarantee stage/mask coherence.

## 2026-08-03 Batch A strict-save audit

- Service session creation requires an operational hub, a catalogued process supported by that hub with matching category/owner tag, capacity, one active session per actor, and a contract for the hub mode. A saved session therefore must preserve a nonblank unique `service:*` ID, matching hub/process/category, defined stage/mode/mask values, finite timestamps with `stageStartedAt >= startedAt`, and a non-null contract; these checks belong in preflight rather than restore-time dropping.
- Runtime completion is legal only from Service/Payment/Cleanup, commits payment at most once, and cancellation supplies a nonblank reason. `TrySetStage` currently permits arbitrary non-Completed stages, including Cancelled, so the save validator must enforce only invariants actually guaranteed by runtime rather than inventing a stricter transition graph. Contract numeric bounds and active-stage membership still require confirmation from `CreateContract` and the authored process definitions.
- ServiceRooms capture already canonicalizes hub IDs, advertised categories, and active sessions, while restore currently trims identifiers, defaults contracts, and silently drops inactive hubs, missing actors/processes, invalid sessions, and duplicate session IDs. Strict preflight must validate the exact authored hub/actor/process references and canonical hub/session ordering before the existing detached `ServiceSessionAggregateState` swap; no record may be repaired or skipped during restore.
- `ServiceRoomsSaveData` persists hub mode/category lists plus full session identity, actor/process/category/stage/timing/advertising/payment/cancellation/contract state. The remaining audit must derive contract and stage invariants from `ServiceSessionModels` and transition code before assigning the rollback-free marker.

- ExperiencePacing transition details confirm completed rehearsal bits remain scheduled; therefore `completed ⊆ scheduled`. An active rehearsal bit must be scheduled and not completed, and any active rehearsal introduces the Defense concept.
- RunFlow DTO persists `finalInvasionDefended`, but the Aggregate has no such field; the runtime derives it from boss-cycle state. V18 payload validation must require this legacy projection to equal its canonical derived value or remove it with a new exact DTO version; it cannot be allowed to raise `bossCycle` during restore.
- ExternalInfluence flag hierarchy from production transitions: armed and active are mutually exclusive; boss and affected-intruder IDs require active defense. Ecology scheduled/in-progress are mutually exclusive; scheduled requires positive remaining time, both active raid states require positive sequence and warning-issued, and inactive state requires zero remaining time. Current/mitigation days start at -1 and mitigation cannot be later than current day.
- ExternalInfluence scalar bounds are renown/dread/scouting `[0,999]`, hostile rumor/ecology `[0,100]`, last exposed-food pressure `[0,20]`, and finite nonnegative last weather pressure (generated values currently 4/8/12). These exact runtime-generated ranges can replace restore clamps.
- Existing regression surfaces are uneven: RunVariable and DungeonDebug expose `RunAll(bool)`, while ExperiencePacing and ServiceRooms expose menu-only `Run()`, and ExternalInfluence has no dedicated fixture. Batch A should normalize these to callable boolean suites or add one batch coordinator that invokes focused strict-boundary helpers without duplicating gameplay setup.
- ExperiencePacing's only rehearsal bits are days 10/20/30 (`mask 0b111`), and introduced concepts are captured in numeric order. Strict payload should require exact V1, current day ≥1, masks within 0–7, completed subset of scheduled, active day in {0,10,20,30} with coherent scheduled/completed membership, and sorted unique defined concepts.
- RunFlow restore already suppresses projection while staging, so root replacement can be rollback-free. Its public restore currently ignores the persisted phase and obsolete `finalInvasionDefended`; the V18 DTO should remove or require the legacy field false, validate phase as the exact day/outcome-derived value, and restore the validated fields without clamps.
- ServiceRooms already replaces a plain `ServiceSessionAggregateState` and suppresses hub subscription while the aggregate store is staging. Its restore remains lossy (null/default lists, trimmed/skipped hubs and sessions), so strict validation can make the existing root swap rollback-free; authored hub/actor/process references must be checked through its existing world/catalog dependencies.
- DungeonDebug also owns a replaceable plain `DungeonDebugModeState`, but restore invokes `StateChanged` after replacing the candidate root. During staging that event can leak presentation side effects; rollback-free conversion must suppress the event while `aggregateRootStore.IsRestoreStaging` and validate/copy the bounded command history exactly.
- RunVariable restore replaces a plain `RunVariableAggregateState`, but then reseeds the shared `IRandomStreamProvider`; that is an external side effect and conflicts with rollback-free publication because random-stream persistence has its own save authority. Batch A must remove RunVariable's restore-time RNG mutation and let the random-stream section own stream state.
- RunVariable capture/restore still contains impossible runtime-null skips despite constructor enforcement, V1 migration, doctrine fallback resolution, missing-list defaults, and unknown-definition warnings/skips. Exact V2 preflight must validate authored doctrine/variable references and canonical collections so valid restore constructs the root without fallback.
- ExternalInfluence payload contains bounded continuous pressures, raid/defense state flags, days/sequences, and two canonical ID sets. Current restore clamps every range and trims/deduplicates both sets; preflight must validate those exact bounds, flag hierarchy, finite values, nonnegative timers/sequences, canonical sorted unique IDs, and then copy without normalization.
- RunFlow already stores a replaceable `DungeonRunFlowAggregateState`, but `RestoreState` recomputes phase from day/outcome and clamps boss cycle rather than honoring the DTO. Strict validation should require the serialized phase/flags/cycle to match the runtime state machine, after which restore can replace the root without lossy derivation.
- ExternalInfluence already owns a replaceable plain `ExternalInfluenceAggregateState`; capture canonicalizes its two ID sets. Restore still accepts null/default clones and reports version errors after entry, so strict section preflight can make publication a single safe root swap.
- `DungeonRunVariableSaveData` currently has no embedded DTO version despite section version 2, while RunFlow also hardcodes section version 1 without a DTO version field. Batch A should add exact payload versions so same-section balance/schema changes cannot silently accept defaulted JSON.
- `RunVariableSaveSection` already uses the typed base but still accepts V1 migration, fills missing nested lists/start data, warns/skips unknown definitions, and has no rollback-free marker. V18 can require exact V2, authored catalog references, canonical order, and lossless detached Aggregate replacement.
- `DungeonDebugSaveSection` is a required presentation-phase section but manually stages default DTOs and mutates the debug-mode service directly. Its payload is small; strict conversion depends on confirming whether the debug service owns a replaceable plain root or still projects live state.
- `ServiceRoomsSaveSection` uses the typed base but has no strict validator/marker/version field. `ServiceSessionSaveData.ToSnapshot` trims IDs, drops invalid sessions, and defaults contracts; these lossy transforms must move behind exact preflight, with canonical hub/session ordering and valid process/actor/contract state.
- `ExperiencePacingSaveSection` is optional and manually staged. It accepts blank/malformed JSON as defaults, supports missing-section synthesis from RunFlow, and runtime restore clamps days/masks plus skips unknown concepts. It must become required exact-version typed preflight and publish only a validated plain Aggregate root.
- `ExternalInfluenceSaveSection` is optional, accepts V1-to-current migration, fabricates default DTOs, and resets state when missing. Batch A must reject legacy/missing payloads in V18 and validate the current DTO before a detached root replacement.
- `RunFlowSaveSection` is required but manually staged; it fabricates an empty DTO and clamps day/cycle during commit. It is a plain-state candidate for the shared typed JSON boundary once phase/outcome/flag/day/cycle invariants are preflighted.

## 2026-08-03 throughput-plan correction

- Phase 112 now distinguishes completed foundations from remaining work and is the sole active ledger. Non-save work is batched independently across atomic publication, executable architecture metrics, three asmdef waves, authored static/session closure, three responsibility-decomposition waves, UI boundaries, localization, content/duplicate authority audit, integrated save proof, and final gameplay/UI verification.
- Historical unchecked tasks in Phases 89–107 duplicated these same scopes. They are retired as planning entries and point to their authoritative Phase 112 batch, preventing completion counts and future agents from double-counting stale work.
- `CharacterSummeryInfo` has already been renamed to `CharacterSummaryInfo`, and the project already has `FailureCode`, `DomainFailure`, combat equipment/module adoption, String Table assets, and a V18 coverage validator. The remaining localization batch is an adoption/closure pass across other domain APIs, not foundation creation.
- There are 74 production C# files over 800 physical lines. This is an upper bound, not 74 proven violations: the final Roslyn gate must aggregate partial class declarations and apply 800 only to MonoBehaviour/Presenter and 1,200 to other runtime classes.
- Production `Bind*Runtime(...)` call sites are already `0`; late binding is a completed ratchet, not remaining work.
- No production C# file currently exceeds the 1,200-line runtime hard limit, but many files remain between roughly 1,035 and 1,099 lines. File length alone cannot prove the stricter 800-line MonoBehaviour/Presenter limit because partials and non-MonoBehaviour owners differ; the architecture-test batch must add a Roslyn class-kind/aggregate-line metric before defining the exact decomposition queue.
- Current large-file leaders include `WorkTaskExecutor`, `SurvivalFoodRuntime`, `CircusRuntime`, `FacilityInstanceEvolutionRuntime`, `Shop`, `DungeonGameplayPerformanceProbe`, `EquipmentEvolutionRuntime`, `SurgeryRuntime`, `AIBrain`, `ConveyorRuntime`, `AnimalHusbandryRuntime`, `ProductionBillRuntime`, `WildlifeActor`, and `Grid`. Several match the original priority list, while earlier Phase 110 already closed the global 1,200-line gate.
- Live measurement shows optional required-interface dependencies are already `0`, so this is a maintained ratchet rather than remaining implementation work. It must not be presented as an unfinished batch.
- The current reflection static-field query returns `3,110`, but it includes compiler-generated mutable caches in gameplay assemblies and is therefore not an actionable source violation count. The non-save plan must first replace this noisy reflection metric with the requested Roslyn/source allowlist rule, then batch only real authored mutable runtime state.
- Unity reports `1,039` top-level MonoScript gameplay types still loaded from `Assembly-CSharp`. Existing domain asmdefs cover Foundation, Infrastructure, Presentation, AI, Buildings, Characters, Combat, Invasion, Items, Offense, Rooms, Survival, Wildlife, Work, World, and Evolution, but the default-assembly migration is still a large concrete track.
- The user's follow-up is correct: save owners were detailed, while all non-save work remained compressed into four broad lines. The plan also contains older duplicated unchecked items across Phases 89–107, so Phase 112 must explicitly supersede them and provide one authoritative remaining-work ledger.
- `RuntimeAuthorityV18Validator` already exposes exact live queries for optional required-interface dependencies and mutable runtime static fields. These should become numerical batch gates instead of vague DI/static cleanup statements.
- `Assets/Architecture/runtime-architecture-baseline.json` currently has zero approved oversized-file violations. Remaining decomposition must therefore be driven by the named priority classes and hard line/dependency limits, not by carrying a nonzero waiver baseline.
- The original save-batch partition was a migration plan, not the current counter. The source ratchet now requires all 54 production sections to be strict rollback-free with an empty remaining set; loaded Unity acceptance of the complete 54-section graph is still pending.
- The previous one-owner loop repeated audit, compilation, Unity reload, V18 validation, Console inspection, and planning-file writes at the smallest unit. The revised plan preserves strict per-owner acceptance but amortizes tooling and documentation at the batch boundary.
- The save-owner batches do not redefine completion: each owner still needs exact versioning, required typed preflight, canonical lossless restore, rollback-free publication, invalid no-mutation proof, and no lossy restore fallback.

## 2026-08-03 StaffDiscontent strict invariant audit

- 전환 완료 후 Unity AppDomain에서 `StaffDiscontentSaveSection`이 rollback-free marker를 실제 구현하며 운영 비-marker section 수가 29에서 28로 감소했다. strict fixture와 V18 authority가 모두 PASS했고 Console은 0/0이다.
- RegularCustomer 선례처럼 이 규모의 검증은 별도 validator 타입 없이 section의 `ValidatePayload`에 둘 수 있다. V18 ratchet은 section의 marker/version/canonical ID/status hierarchy와 runtime의 clamp/default 제거를 고정하면 된다.
- `RegularCustomerDebugScenarios.VerifyStrictSaveBoundary`가 요구하는 검증 형태를 확인했다: source capture → target restore → exact recapture, preflight/rollback-free/required interface 검사, 변형 invalid JSON restore 실패 후 exact state 보존이다. Staff fixture도 동일 형식을 적용한다.
- V18 source ratchets는 실제 `Assets/Scripts/Services/Items/Editor/RuntimeAuthorityV18Validator.cs`의 최근 strict section 묶음 뒤에 이어 추가해야 한다.
- DTO에는 아직 버전 필드가 없고 section은 공용 typed JSON base를 쓰지만 validator/rollback-free marker가 없다. exact V1 필드와 `ValidatePayload`를 추가할 수 있는 단순 경계다.
- Capture는 이미 staff ID를 ordinal 정렬한다. Restore의 null-list 기본화, null/blank skip, trim, 중복의 restore-time report는 모두 preflight로 이동하고 valid 경로는 목록을 그대로 snapshot으로 변환해야 한다.
- 저장된 `outcome`은 capture 경로가 항상 `None`으로 내보내며 runtime record에는 보관되지 않는다. strict payload는 `None`만 허용해야 손실 없는 왕복이 된다.
- `LocalRebellion` 진입은 `permanentLoss=true`, `localRebellion=true`, `LocalRebellionDays=1`을 만든다. `Departure`는 `permanentLoss=true`, `departed=true`를 만든다.
- 격리는 반란 상태에서만 성공하고 `isolated=true`, `ownerThreat=false`가 된다. 진압은 반란 상태에서만 성공하고 `suppressed=true`, `localRebellion=false`, `ownerThreat=false`, `permanentLoss=true`가 된다.
- 현재 restore는 display name 기본화, mood/day clamp를 수행하므로 preflight가 canonical text, enum, finite mood `[0,100]`, nonnegative days, status hierarchy를 먼저 보장해야 한다.
- 실제 fixture용 `CharacterSceneRuntimeReferences` 생성 경로는 공용 Editor 의존성 helper와 scene reference 생성자에 이미 존재한다. 다음 조회에서 생성자 계약과 기존 Staff fixture wiring만 좁게 확인한다.
- `CharacterSceneRuntimeReferences` 생성자는 8개 런타임 참조를 받지만 Staff section은 `StaffDiscontent`만 요구한다. fixture에서는 실제 Staff runtime을 넣고 나머지를 null로 둔 경량 reference를 안전하게 만들 수 있다.
- Staff runtime은 `RestoreSnapshots`에서 새 `StaffDiscontentState`를 만들고 Aggregate root를 교체하므로 Unity scene/presentation 객체를 mutate하지 않는다. validator가 normalization을 차단하면 rollback-free publication 조건에 맞는다.
- Snapshot/restore call-site 검색 결과 production 생성은 record capture와 save section뿐이고, 외부 debug 호출은 empty restore 하나뿐이다. constructor/record/state restore를 strict argument 계약으로 바꿔도 정상 호출을 깨지 않는다.
- 프로젝트에는 이미 `CharacterId` 값 타입이 있으며 Staff ID는 `CharacterIdentity`의 persistent ID에서 나온다. 현재 저장 DTO는 문자열이므로 이번 section은 최소 canonical nonblank/ordinal uniqueness를 강제하고, 후속 전역 typed-ID 단계에서 DTO 필드 자체를 값 타입 직렬화 계약으로 교체해야 한다.
- `CharacterId.IsValid` 자체도 현재 nonblank만 검사하며 persistent ID normalization은 trim뿐이다. 따라서 section validator의 `new CharacterId(id).Value == id && IsValid` 검사는 현재 typed-ID 계약과 정확히 일치한다; 임의 접두사 강제는 기존 fixture/콘텐츠를 잘못 배제한다.
- `StaffDiscontentSnapshot`과 `StaffDiscontentRecord.FromSnapshot`이 ID trim, blank-name fallback, mood/day clamp를 중복 수행하고 `StaffDiscontentState.Restore`도 null enumerable/default/skip을 허용한다. section preflight뿐 아니라 runtime restore 경로도 valid-only 계약으로 축소해야 우회 호출에서 손실을 숨기지 않는다.
- `MarkIsolated`/`MarkSuppressed`는 departed, already-suppressed, non-rebellion 상태에서 실패한다. 따라서 isolated는 active rebellion에만 허용하고 suppressed는 LocalRebellion stage의 영구손실이지만 active rebellion/owner threat는 false여야 한다. 격리 후 진압은 가능하므로 suppressed+isolated 조합은 허용해야 한다.
- 기존 Staff fixture의 `ScenarioRuntime`은 실제 `StaffDiscontentRuntime` MonoBehaviour와 공용 Editor DI helper를 이미 사용하고 cleanup도 갖춘다. `RunAll`에 저장 경계 시나리오를 추가하고 동일 runtime으로 canonical round-trip과 invalid no-mutation을 증명할 수 있다.
- Staff fixture의 save scenario 삽입 위치는 기존 7개 gameplay scenario 다음이며, helper는 `VerifyOwnerThreatEscalation` 뒤/`CreateStaff` 앞에 둘 수 있다. 기존 cleanup은 이름이 `StaffDiscontentRuntime_Test`인 모든 runtime을 제거한다.
- 최근 strict fixture들은 section의 `Capture()`와 `Restore(json, version, report)`를 직접 사용해 canonical round-trip과 invalid preflight를 증명한다. Staff도 이 경량 패턴을 쓰고, interface 검사는 sealed 타입의 불가능 pattern 오류를 피하도록 `object sectionContract`를 거친다.
- Capture는 record의 outcome을 기본 인자 `None`으로 생성한다. 상태 Aggregate가 저장할 수 있는 값은 record의 11개 필드이며 outcome은 이벤트 결과이지 저장 상태가 아니다. V1 DTO를 유지한다면 validator가 `None`을 고정해 손실 없는 직렬화 계약을 명시한다.
- 첫 기록 patch는 문서 제목을 `# DungeonStory Findings`로 잘못 추정해 실패했다. 실제 제목 `# DungeonStory Current Findings`를 확인해 수정했다.

## 2026-08-02 detached restore findings

- Payload preflight alone was insufficient because the registry deserialized each payload again during live commit. The prepare-all phase now materializes typed payloads before any live mutation.
- Physical items already had the correct internal seam (`StageRestore` plus `Commit`) but the save section bypassed it through the broad runtime `Restore` method. Exposing that seam as a dedicated staging capability made the item repository the first genuinely detached Aggregate restore.
- A detached DTO is not enough if commit clears a live dictionary and repopulates it entry by entry. `WorldItemRepositoryState` now owns stacks, indexes, caches, equipment, and modules as one replaceable root, so the authoritative item state changes in a single assignment.
- Strict physical-equipment validation correctly rejects a stored/loose equipment instance without a stack. Tests and Editor tools must create and link the unique stack rather than weakening that invariant.
- A staged delegate around an old runtime `Restore` is useful migration infrastructure, but it is not proof of an atomic world swap. Final completion still requires replaceable Aggregate state roots and removal of the legacy adapter/rollback path.
- Mandatory staging can be enforced without guessing from source text: the Registry rejects non-staged sections during composition and the Editor validator reflects every public gameplay SaveSection. Current coverage is 54/54, with staged missing-data behavior for every optional section.
- Broad regressions are catching architecture migration omissions: the combat suite exposed an Editor fixture that still passed `null` for a dependency that production now correctly requires.
- Source-wide counts must distinguish production code from Editor fixtures and validator literals. The apparent default-service and late-bind hits were confined to fixtures or the validator's forbidden-token list; the production code count is zero for all five active authority guards.
- Scene transitions need persistent state, but not static state: the existing `DontDestroyOnLoad` mailbox is the correct scoped owner for pending requests, messages, and the temporary transition host.
- A SaveSection can stage its JSON and still expose partial state if its runtime calls `Clear()` across several collections. A replaceable root must include the related sequence/version/view fields as well as the primary dictionary.
- Production orders and their stock-sensor installation state are one Aggregate because one saved production payload owns both. Sharing one state store prevents a restored order list from being observed with the previous sensor set.
- `combat.equipment` previously rewrote physical equipment after `items.physical` restored it. Removing that pass is both an atomicity fix and an authority fix: combat restore now owns only references and work queues.
- Restore-time defaults must be built without publishing side effects. Faction defaults are now computed from the strategic map in a detached state and only synchronize faction home sites after the final state assignment.
- Character exposure and protective workwear cannot be independent restore commits because one save section captures both. A shared environment state store keeps equipment protection queries from observing a new exposure set with old workwear data.
- A version check must happen before any reset. External influence previously reset the current run before rejecting an unsupported payload; detached construction now leaves live influence state untouched on validation failure.

## 2026-08-02 authored gameplay catalog findings

- Meta upgrades, run variables, owner doctrines, and invasion patterns were not fixed protocols: their costs, text, weights, target preferences, and effect parameters are editable game content. Freezing their dictionaries would remove mutation but would still leave code as a second content authority.
- The existing `GameDomainContentCatalogSO` is a safer migration root than creating temporary runtime SOs or manually fabricating new asset GUIDs. Inline serialized records let the authored root become authoritative before legacy writers are removed.
- Effect interfaces remain useful runtime behavior boundaries, but their instances are now projections of serialized effect descriptors. The SO owns values; the plain runtime catalog owns validated immutable behavior objects; neither owns run state.
- `MetaProgressionState` and `RunVariableState` previously reached hidden static catalogs. Requiring catalog contracts at construction makes their rules deterministic per scope and removes cross-test/global reset coupling.
- The remaining taxonomy registries need separate treatment: enum/ID mappings may become immutable protocol tables, while display text and balance values such as stock delivery weights belong in authored SO content.

## 2026-08-02 runtime registry findings

- A provider that only returns one property from a scoped registry is not a policy boundary; it hides composition failures and encourages call-site defaults.
- Required scene runtimes are now resolved once from typed domain registries. Missing runtime state is a composition error, not an empty save, zero seed, unavailable UI, or permissive unlock result.
- Research and equipment unlock checks now fail closed against the same `BlueprintResearchRuntime`; provider absence can no longer bypass locks.
- The local LLM provider remains justified because it selects between two environment-specific queue implementations. It is the only remaining `I*RuntimeProvider` interface.

## 2026-08-01 V18 Phase 90 findings

- The character-summary defect was responsibility coupling, not only file length: combat commands, health/captivity operations, AI diagnostics, progression confirmation, stock projection, popup lifecycle, and detailed-stat rendering all lived in one MonoBehaviour.
- The replacement keeps Unity button entry points on `CharacterSummaryInfo` but delegates rules and projections to narrowly injected presenters. This preserves generated-view bindings while preventing the view from owning combat, surgery, captivity, or stock state.
- `CharacterSummaryInfo` is now 729 lines with eight injected dependencies. Shell/status/growth/AI/health/captivity/combat presenters range from 147 to 516 lines.
- Seed-addressed content rolls were using `System.Random` directly in character growth, start variables, shops, evolution, and procedural audio. These were deterministic calculations rather than saved run streams, so they now use the explicit `DeterministicRandomSequence` contract; saved stochastic gameplay continues to use `IRandomStreamProvider`.
- The V18 validator passes with 772 authored items and 168 catalyst SOs after adding the new presentation boundaries and direct-RNG prohibition.

## 2026-08-01 Branched production network V3 audit

- The live economy contains 174 authored production recipes. Sixty-two produced item IDs are reused as inputs; 20 of those currently have exactly one recipe consumer, so the defect is catalog-wide rather than limited to the generated research-overhaul content.
- All 24 generated `ResearchOverhaul` recipes currently consume the placeholder `stock-item:1`, and most generated products have no real downstream consumer. The V3 builder must replace these inputs and index consumers outside recipe assets as well.
- `ProductionOrderMode` currently has only `RepeatCount` and `MaintainStock`; `ProductionBillStatus` has no output-space or stock-sensor state, and production outputs spawn directly instead of reserving a dedicated local output buffer.
- Existing conveyor code already owns overflow policies and cyclic-deadlock detection. V3 should extend the production boundary with local buffer backpressure instead of creating a second conveyor authority.
- Fuel support currently stores one exact `fuelItemId`. Resource items already expose ingredient tags and nutrition, but need authored fuel value and facility supply eligibility for deterministic multi-item fuel/feed selection.
- The worktree contains extensive user/previous-agent changes from the completed 168-research, equipment, medical, defense, and survival work. V3 edits must preserve and build on those changes without cleanup or rollback.
- `ResourceUsageIndex` already reverse-indexes recipes, crops, craft materials, and built-in sinks. It currently treats `sink:equipment-material:*`, generic meals, trade, fuel, and other synthetic sinks as consumers, so V3 should evolve this authority into the production dependency catalog instead of adding a parallel graph service.
- `ResearchOverhaulContentAssetBuilder` owns all 40 generated facilities and 24 generated item/recipe pairs. It currently assigns every facility `FacilityRole.Research`, generic `research-overhaul`/`rf##` tags, and every recipe a single `stock-item:1` input, making it the primary content rewrite boundary.
- Combat equipment definitions expose material families and stock-category amounts rather than concrete resource item inputs. The dependency catalog needs a deterministic material-family-to-resource mapping or explicit authored dependency inputs to count real equipment consumers.
- The first broad multi-search returned exit code 1 because one `rg` branch had no match, although other branches returned useful output. Follow-up inspection uses independent targeted reads so a no-match cannot mask successful results.


## 2026-08-01 168-node research and equipment overhaul

- The implementation starts from a heavily dirty 442-file worktree whose active unrelated work is character anatomy/medical expansion. Research/equipment changes must remain narrowly scoped and preserve every pre-existing edit.
- The live catalog currently contains 141 research assets. The approved target is 168: the three previously planned nodes plus research IDs 7224–7247.
- Current research prerequisites are bare `ResearchProjectSO` references, research save data is V3, and the tree has no shared reverse reward catalog or timing projection.
- Current combat equipment has 19 authored definitions, no research/tier/slot/lineage fields, and forge recipes expose every definition. Runtime crafting therefore has no authoritative research lock yet.
- The overhaul is intentionally new-run only for research/equipment V1–V3 data; no silent migration or default substitution is permitted.
- The final approved breadth queue measures 32.2 medieval days, 80.4 early-industrial days, 234.3 mature-industrial days, and 372.0 rune/abyssal days at 99 effective work per day.
- Reusing the serialized project unlock collection caused stale and multiply-owned facility rewards. The builder now reconstructs unlock collections and applies one canonical research owner per building ID.
- Existing equipment tests must explicitly inject completed research. Product runtime remains fail-closed when no research provider is available, preventing direct-call and restore bypasses.
- The save-slot catalog now checks research/equipment section versions before enabling Load, rather than waiting for restore to throw after scene launch.
- The research pointer verifier previously hard-coded a small catalog and searched only the current viewport for queue candidates; it now expects 168 and centers an available node before pointer interaction.


## 2026-07-26 V16 traversal-cache and wildlife timing findings

- `Grid.version` was serving two incompatible purposes: any content mutation invalidated structural path/room/facility caches. Moving wildlife and changing items therefore discarded otherwise valid routes.
- `Grid.StructuralVersion`/`TraversalVersion` now changes only for area, building, hallway, movement-blocking, or connection mutations. Full `version` still tracks every content change for consumers that need it.
- Wildlife hunt reachability previously depended on cached visitable-occupant positions. With dynamic occupancy excluded from traversal invalidation, the hunt query now checks the target actor's current Grid coordinate against the reachable-cell result.
- Wildlife arrival dwell mixed the caller-provided current time with a null-clock fallback of zero. Giving every actor a Unity game-clock fallback makes route start, route completion, threat interruption, and dwell expiry use one scaled time base.
- Focused Grid, Wildlife, and AI naturalness regressions pass after the changes.
- The 100-NPC EditMode stress scenario improved from roughly 353 seconds to 50.6 seconds. Broker path searches fell from 1,440 to 51 and budget deferrals from 16,461 to 50. Scheduler p95 is 0.73ms; the large max values are cold-path/test instrumentation spikes and require PlayMode profiling before final acceptance.

## 2026-07-21 Physical item and hauling implementation findings

- Current Editor Console baseline is clean through Unity MCP (`Error 0 / Warning 0`), but batchmode compilation cannot run while the interactive Unity Editor owns the project. MCP/Editor Console is the active compile source for this pass.
- `WarehouseInventory` currently lives inside `Buildings/SO/StockInfo.cs` and is consumed directly by stock delivery, shop restock, crafting, and expedition preparation. Physical items must be introduced as a runtime layer without mutating shared `BuildingSO` data.
- World info click selection currently checks characters before buildings and does not know item piles. Adding `GridLayer.Item` must not break `GridCell.GetBuilding()` callers that expect buildings even when another non-blocking occupant is on the same cell.
- Existing `AIActionSet.RequiresDestination=false` actions such as wait/look-around provide the right pattern for hauling: `AbilityHaul` should own pickup/dropoff pathing instead of forcing the generic AI destination contract to represent two legs.

- Final character-growth acceptance is green: combined EditMode regression, real-pointer P1/P2 (`18/18`), exclusive character/building selection, actual skill alert navigation, start-party generation, V3 save restore, and all three ultimate domains passed with Unity Console `Error 0 / Warning 0`.
- MCP `Camera_Capture` cannot render the live URP `Main Camera` directly in this editor/package combination, but a plain runtime camera copied from it renders the same transform and projection successfully. That capture is nonblank and provides independent world-only evidence alongside HUD `ScreenCapture` artifacts.
- Skill runtime audit found that `research`, `output`, `repair`, `stock`, `relationship`, and `revenue` modules only granted a generic mood factor. Their authored numeric variants now feed their actual subsystem paths, while management ultimates only join those contextual modifiers after their operating-day use limit has been marked.
- Defense automatic ultimates previously listened to the pre-target `InvasionStartedEvent`, so enemy-targeted effects had no intruder to affect. They now listen to `InvasionSpawnedEvent`, canonicalize actors by GameObject, and apply validated damage modules to the spawned intruder.
- Two inspection commands failed before code changes: one referenced the nonexistent `Assets/Scripts/FacilityShop/Shop.cs` instead of `Assets/Scripts/Buildings/Shop.cs`, and one PowerShell interpolation used `$i:` without braces. The follow-up reads used the resolved path and format operator.
- The actual skill-alert capture shows the prepared owner name as `유나 사장으로 시작`, confirming the duplicate-role notice fix. It also shows the event detail and `성장 탭 열기` command clearly above the world, while the character panel remains readable and no surfaces overlap incoherently.

## 2026-07-20 Confirmed character growth design

- The runtime character prefab contained both the empty legacy `Customer : CharacterActor` component and the canonical `CharacterActor`, so scene queries counted every spawned character twice. Start-party cleanup then destroyed the shared GameObject while trying to remove the apparent duplicate. Both character prefabs now keep only the canonical component, and start-party/save queries normalize actors by GameObject as a compatibility guard.
- The start-party pointer flow passed end to end with real LLM output, but the supposed mobile capture remained 1920x1080 because `Screen.SetResolution` does not resize this Editor Game View. Mobile bounds/capture evidence must use an Editor Game View size change or an equivalent actual render target before it can count as verified.
- Selecting fixed Game View sizes through `GameViewResolutionController` produces real 1600x900 and 900x1600 render targets. The portrait layout keeps all three member cards and the final action row in-bounds; no card or text overlaps were visible in the corrected capture.
- Growth-tab capture exposed a separate visible copy defect: authored owner names such as `슬라임 사장` were rendered as `슬라임 사장 사장으로 시작`. Owner selection now accepts the prepared identity name and avoids appending a duplicate role suffix.

- The legacy `CharacterProgression` model (`MaxLevel=20`, three equipped IDs, fixed unlock track, global `PowerMultiplier`) must be replaced rather than extended in parallel.
- Shared authored configuration belongs in one `CharacterSkillSystemSettingsSO` with managed-reference module rules. Character-specific skills, drafts, growth, ledgers, request state, and use limits belong to serializable runtime/save records.
- Normal active unlocks are at levels 1, 5, and 30; passives are automatic at level 1 and after level 25 plus narrative breadth; the narrative-derived ultimate arrives at level 50.
- Potential uses five display grades with 45/30/15/8/2 population weights and only modifies normal-active rarity rolls. A missed Rare-or-higher draft grants the next unlock a 1.5x upper-rarity weight modifier.
- Character preparation is a three-person roster (owner plus two same-species staff) with identity, aptitude, and skill reroll groups. World visitors require persistent individual profiles rather than respawning shared `CharacterSO` definitions.
- Save compatibility is deliberately broken for legacy progression data; save/load must preserve already-rolled rarity and candidates so reloading cannot reroll outcomes.
- `CharacterActor` already requires `CharacterProgression`, so the new per-character growth state can replace that component's legacy lists without adding another required prefab component.
- Combat applies the legacy level multiplier both in `CharacterActor.GetCombatPowerMultiplier` and `OffenseBattleFactory.CreatePlayerCombatant`; both applications must be removed so level growth comes only from allocated stat points.
- `CharacterStats.GetCharacterStat` is the narrow final-stat query used by battle and expedition power. It can compose identity profile stats with character-specific base-stat replacement, level growth, and conditional passive bonuses.
- Existing combat abilities are constructor-driven modules, so generated combat selections can be validated as string IDs and converted into `CharacterCombatAbilityDefinition` instances without storing polymorphic runtime effects in save data.
- The local LLM queue already supports prioritized request profiles and JSON mode. A dedicated skill profile can use the same queue while persistent retry keys and backoff live in the generation service.
- `OwnerSelectionPanel` is runtime-configured and pauses the simulation, making it the correct replacement surface for three-character preparation rather than introducing a disconnected scene-only mockup.
- Start preparation can remain instance-safe by generating skills on hidden `CharacterProgression` preview objects, then restoring their snapshots onto the real owner/staff actors only when the player confirms the party.
- Skill rerolls need request cancellation plus a per-growth revision because LLM callbacks can finish after identity or aptitude has changed. The generation service now ignores canceled requests and request keys include the revision.
- Existing customer `CharacterSO` assets can provide same-species staff visuals and authored species data without mutation: the spawned actor is converted to runtime `CharacterType.NPC`, receives `AbilityWork`, and owns its prepared growth snapshot.
- The first preparation PlayMode pass exposed bottom buttons parented directly under the modal surface; rebuilding member cards therefore left duplicate Start/Back controls. A dedicated preparation-action root fixes their lifecycle.
- Replacing base stats alone was insufficient: modifier queries still read `CharacterIdentity.Profile`, which is built from authored SO traits. The effective runtime profile must be rebuilt from the character's selected trait IDs and used by all modifier queries.
- Recruitment previously changed the live actor to NPC but never marked its persistent world profile as staff. Without promotion, a saved hired guest remained eligible for later visitor acquisition.

- Character progression must not be mutable state on `CharacterSO`; those assets are shared by every character using the same definition. Per-character level, XP, learned skills, and equipped slots need runtime/save ownership.

- The offense loop now has the missing expedition layer: preparation in the dungeon, route pressure, attrition, tactical formation combat, retreat, return, and reinvestment are separate decisions rather than one launch button followed by one boss battle.
- Ordinary battle victory no longer finalizes a target or heals the party. It returns to route choice with damage and stress intact; only the boss resolves the expedition and advances the campaign.
- The dungeon link is capability-based. Formal usable rooms and modular expedition-support abilities contribute preparation values, while supplies are withdrawn from and returned to the real warehouse inventory.
- A complete UI-event campaign passed all targets in order: `food_farm`, `merchant_road`, `old_armory`, `mana_ruins`, `rival_dungeon`, `truth_core`; final state was `truth=True` with six result records.
- Product-shell coverage separately proves pointer-driven owner selection, customer recruitment, map/composition, journey entry, first battle, and exact active-battle save/load without captured errors or warnings.
- Global button-text lookup is unsafe once event alerts repeat prior action labels. Offense verification now scopes clicks to the active map, expedition, or battle panel, matching the visible interaction surface.
- Immediate `CaptureScreenshotAsTexture` after a synchronous full campaign can capture before the next rendered frame. Scheduling `ScreenCapture.CaptureScreenshot` for the following frame produced valid visual evidence.

- The old offense lifecycle was the main reason it felt flat: launch opened one full encounter, every battle completion deleted the expedition, and victory fully healed survivors.
- Formation previously had no tactical effect. Source and target position constraints plus forward compaction are required before party order becomes a real decision.
- Dungeon/offense coupling now has an existing-compatible path: explicit `BuildingExpeditionSupportAbility` values override or extend role-based fallback contributions, so old content works before every asset is migrated.
- Warehouse inventory can safely back expedition supplies because aggregate availability, deterministic withdrawal, rollback, and return can be implemented without introducing a second resource ledger.

- The requested target is now a dungeon-linked multi-node expedition, not continued balancing of the existing one-target/one-battle flow.
- The existing offense lacks route decisions, supplies, formation constraints, persistent expedition attrition, camping, and room/facility recovery. These are product gaps, not presentation polish.
- The recently added campaign-order `+50%` stat multiplier closes a numerical test but works against the requested design; it must be removed as the real growth and preparation loop is introduced.
- The worktree is heavily dirty across scenes, prefabs, data assets, and gameplay scripts. Offense changes must remain tightly scoped and must not revert unrelated user changes.
- Stock is not a standalone subsystem folder. Runtime inventory is `WarehouseInventory` on warehouse buildables, with `SceneFacilityEvolutionWarehouseInventoryQuery` already providing aggregate query/withdraw/rollback patterns suitable for expedition supplies.
- Building functionality is already modular through `BuildingAbilityCollection`; expedition preparation/recovery should be added as ability modules and queried through capability interfaces instead of adding more fixed fields to `BuildingSO`.
- `OffenseExpeditionRuntime` currently owns the right lifecycle boundary but removes the active run on every `BattleCompleted`, fully heals victory survivors, grants target rewards immediately, and advances the world map. This method must become node-aware: ordinary victories return to the route, only boss victory finalizes the target, and survivors retain attrition.
- `OffenseSaveService` already pairs one active expedition with an exact battle snapshot. Its run payload can be extended in place with route, formation, stress, supplies, loot, and current-node fields while old saves default to a legacy boss-battle run.
- Offense panels are created through injected factories and `IOffensePanelService`; adding a dedicated route panel preserves the existing ownership pattern and avoids scene-authored UI dependencies.
- `OffenseExpeditionRun` can absorb the new journey state without changing its identity, target, or actor ownership. This lets battle, reward, campaign, and save systems migrate incrementally instead of creating a parallel offense runtime.

- Six campaign targets and truth-reveal victory already exist.
- `OffenseBattleSession`, inline combat abilities, six fixed encounters, direct command runtime, and a dedicated battle panel now exist.
- Product expedition start now creates a turn battle. Product UI, debug completion, reward probes, and PlayMode verification no longer resolve expeditions by timer or combat-power comparison.
- Staff identities now persist from run seed plus creation sequence, with owner fixed to `owner`.
- Save V2 captures the active battle and V1 active expeditions migrate to a first-turn battle.
- Title new-game now selects `DungeonDifficulty`; start/result persistence is explicit and still needs an end-to-end runtime audit.
- Runtime defense uses recurring `EndlessDefense` cycles, but two PlayMode verifiers still expected obsolete `FinalChallenge/TruthHunt`; those expectations were replaced.
- Scene runtimes are found through cached providers, so the turn engine can be a DI singleton without adding another scene-authored component.
- The standalone `OffenseBattlePanelFactory.cs` was visible to `AssetDatabase` but absent from Unity Bee's source list. Merging its factory/controller types into the already imported `OffenseBattlePanel.cs` resolved the actual Unity compile failure.
- Unity MCP is connected. The Editor is idle and the current Console count is `Error 0 / Warning 0`.
- `DungeonProductShellPlayModeVerifier` now drives difficulty selection, expedition target/start, guard, attack/target selection, dungeon switching, manual save/load, and exact battle-state comparison through pointer input.
- First current-build product-shell run reached PlayMode with no Console errors/warnings, but every synthetic Input System button click failed to invoke its callback. The report proves targets were active/interactable while Settings, Difficulty, and Owner state did not change. The later gameplay transition came from the verifier's direct duplicate-transition assertion, so this run is not product-path evidence.
- Captures confirm the failure boundary: the title capture is valid, while the alleged battle capture still shows the owner-selection modal.
- The queue-plus-`InputState.Change` fallback fixed current Editor pointer delivery. The next run passed title Settings, audio/accessibility tabs, Hard difficulty, owner selection, save/settings/title return, Continue, and load-failure handoff with `capturedErrors=0; capturedWarnings=0`.
- The remaining product-shell failure moved to offense navigation: `P1Action_OffenseTarget_0` had a screen center at y=0.71 and was active/interactable but clipped/covered by the bottom HUD. The verifier skipped the visible `월드맵 열기` and party-composition workflow, so its click did not select a target and no Start button appeared.
- The visible map flow now selects `food_farm` and opens composition correctly. The current capture shows `선택 인원 0/3` and `필요 인력 부족 0/1`: a clean new run has no eligible employee, so the verifier cannot start combat until it exercises or prepares the recruitment path.
- Recruitment currently marks only `RegularCustomerRecord.IsRecruited`. No listener or conversion changes the live actor from `CharacterType.Customer` to `CharacterType.NPC` or grants `AbilityWork`; therefore a normally recruited customer still fails offense eligibility (`NPC` + active `AbilityWork`). This is a real gameplay-loop gap, not just a verifier fixture issue.
- Recruitment also had no runtime component in `SampleScene`, and the scene spawner referenced only `TestCharacter` (`NPC`). The real customer asset (`Resources/SO/Character/New Character SO.asset`) was never included in the spawn list, so visits and recruitment could not occur naturally. The lifetime scope now owns the runtime, and the spawner merges customer catalog entries once after DI.
- Customer data IDs identify a customer definition, not a unique live visitor. Keeping only the data ID caused recruitment to convert an arbitrary matching actor. `RegularCustomerRecord.ActiveActor` now preserves the exact last visitor in memory, while restored records safely retain the scene-query fallback.
- Current product-shell report is `PRODUCT_SHELL PASS`. It proves recruitment, offense input, independent dungeon time, view switching, direct guard/attack targeting, and exact active-battle restore with `capturedErrors=0; capturedWarnings=0`.
- The room MCP capture originally lost its overlay between tool calls because normal hover polling cleared it on the next frame. Preparing the room and pausing PlayMode in the same editor command preserves the real renderer state; the capture then shows 4 active fill cells and 10 active outline segments on the intended sorting layers.
- `DungeonRunFlowPlayModeVerifier` now opens `SampleScene` before PlayMode, so it remains valid after introducing the separate title scene. It proves recurring day-10/day-20 bosses never grant Victory and only stage 6 `truth_core` does.
- The final independent domain pass reports `RoomSystem`, `RoomEnvironment`, `OffenseBattle`, `OffenseWorldMap`, and `OffenseReward` all successful with no console errors or warnings.
- The complete regression set is green: product shell, run flow, save UI, Unified UI, P1/P2 surfaces, character click priority, room inspection, and 29 implemented debug suites.
- A clean player-style run proved natural visits stopped at 15 because a visitor with no remaining visits was still forced through an optional look-around before exit. `AbilityShopping` now exits immediately when the visit cycle is complete, and its focused AI regression passes.
- The recruited prefab carries both legacy `Customer` and canonical `CharacterActor` components. Reference-only distinctness therefore rendered one employee twice and made the second lifecycle transition fail. Expedition discovery and launch now canonicalize actors by GameObject; the clean composition shows one row for one employee.
- World map and expedition composition canvases could stay open together. Because composition sorts above the map, a visible stage button could actually hit composition's Close button. `OffensePanelService` now hides the opposite panel before binding either surface.
- Stage 1 left the only employee at `13/120`, while no building ability or player action restores combat health. The campaign exposed a hard lock rather than attrition. Successful surviving members now receive full return treatment; retreat/failure health and permanent death remain consequential.
- The resource catalog contained only one customer definition even though campaign stages require two and then three non-owner members. Added natural Orc and Vampire customer definitions, bringing the recruitable pool to IDs `1,2,3` without bypassing visits or recruitment UI.
- Focused customer, staff, world-map, battle, and reward suites pass after these direct-play fixes.
- `SampleScene` had a serialized `RegularCustomerRuntime` while the lifetime scope also created one. Both listened to every visit, but UI recruitment and generic runtime lookup could observe different states. DI now reuses the scene runtime and only creates a fallback when a scene has none; a clean run reports exactly one instance.
- A second clean seed ran at X5 for almost two real minutes and settled at average satisfaction `62.0 / 65.1 / 71.7`, below the old recruit threshold `75`. Since the unmodified starter dungeon offers no clear way to correct that early random average, recruitment and therefore the campaign were seed-locked. The default/product candidate threshold is now `65`: three visits establish a regular and the fourth qualifying visit unlocks recruitment.
- The direct run won stages 1 and 2, then lost both selected employees at stage 3. Training facilities only add mood factors; they never improve combat stats, while the fixed encounter roster escalates far beyond the unchanged recruit stats.
- Required power was informational and the original encounter curve had no corresponding growth path. Campaign stages now grant a deterministic preparation multiplier derived from prior victories (`+50%` combat stats per completed stage, health unchanged). The map and composition UI expose the bonus and effective party power.
- A deterministic battle regression now runs the starter Orc/Vampire/Slime roster through all six encounters on Easy, Normal, and Hard and requires victory with every member alive. This is regression coverage only; final acceptance still requires the clean pointer-driven playthrough.

## 2026-07-20 Weak-link audit

- P0: World profiles use unique string IDs, but regular-customer, rumor, staff-discontent, and some evolution records still key characters by shared `CharacterSO.id`; distinct people using the same template can share visits, recruitment, trust, and complaints.
- P0: The six-target winning campaign previously ended with the party at levels 6/5/3, while the new curve requires 63,700 XP for level 50. Level-25/30/50 content is outside the current run loop.
- P0: Skill generation has no executable fallback. Start-party confirmation and the ready guest pool both wait on validated LLM results, so an unavailable local LLM can stop both new-game start and the customer economy.
- P0: Passive validation permits combat modules for DamageTaken/InvasionStarted/BattleCompleted, but the outside-combat executor implements only a subset. Valid generated passives can therefore have no mechanical effect.
- P1: Room quality has two definitions. UI/mood use `RoomEnvironmentSnapshot`, while AI utility uses the older area/door/furniture `RoomInstance.GetQualityScore`; a room can look excellent in the overlay without becoming equally desirable to AI.
- P1: Offense preparation checks usable rooms, then adds fixed bonuses per facility role/ability. It does not consume room environment scores, staffing quality, recent operation, or character mastery, so duplicate fixtures can replace good operation until caps are reached.
- P1: Generated actives are converted with source and target formation masks set to `Any`; authored species skills respect positions, but personalized skills bypass the Darkest-Dungeon-style formation layer.
- P1: World profiles persist growth and narrative, but actor binding/release does not persist social memory, mood history, or the profile's relationship score.
- P2: Unlimited full reroll also rerolls potential and restores all partial reroll charges, making rare potential primarily a patience check instead of a durable roster tradeoff.
- P2: Growth UI shows only total allocated points and skill descriptions. Per-stat growth causes and stored narrative reasons disappear after the acquisition alert, weakening player understanding of history-shaped growth.

## 2026-07-20 Closed-loop integration findings

- Building info craft buttons were visible but ignored when clicked in the first fade-in frame because the parent `CanvasGroup.interactable` stayed false until the 0.1s tween completed. The panel now enables interaction immediately, leaving the tween as visual-only feedback.
- JsonUtility can deserialize a null nested class field as a blank object; `DungeonOffenseSaveData.activeBattle` therefore came back non-null with empty IDs. Offense restore now treats blank battle IDs as no saved battle, and capture only persists active battles that match an active `InBattle` expedition.
- Equipment crafting PlayMode now proves the full non-scenario UI path: queried warehouse material is visible to the runtime, the building craft button creates a queue order, materials are withdrawn, `Craft` work completes the order, and the expedition loadout reserves the crafted item.
- The character growth tab previously displayed only total growth, so players could not tell whether a stat came from base rolls, species/traits, level growth, equipment, or a conditional passive. `CharacterProgression` now exposes the breakdown directly and the UI consumes that source.
- Runtime-generated UI can survive script changes inside the dirty scene. The first stat-breakdown test passed by text value while the screenshot still showed the old 30px summary region; `CharacterSummaryRuntimeLogFactory` now rebuilds the generated view when the expected `GrowthList` structure is missing.
- The first no-injection direct-play verifier did not fail because offense launch was too strict; it failed because the real AI never consumed recovery facilities after a stage-1 victory. Prior coverage only checked `AIRest.CanStart` and `FacilityCandidateScorer.GetNeedScore`, so `AIAction.CalculateScore` could still return 0 and prevent selection.
- The concrete recovery scoring gap was that `Rest.asset` still referenced the old `Sleep` stat consideration instead of `NeedRest`, and `ConsiderationFacilityNeed` rejected staff self-care when `AbilityShopping.visitCount` was 0. On-duty Hygiene also rejected workers despite stress recovery being part of the hygiene need score.
- Unity can keep running the previous assembly when a compile error exists elsewhere. The missing `restResolved` diagnostics were not an MCP truncation problem; `NaturalRunRuntimeDebugProbe` still called the removed `IGameDataProvider.GetGameData()` API, so StaffDuty was executing stale code.
- `CharacterAiActionCandidate.Action.destination` is intentionally null during scoring. Destination proof belongs at `AIActionSet.TryResolveDestinationWithFailure` or after `AIBrain.TryCommitActionCandidate`; tests that read candidate destination before commit can produce false failures even though the AI can select and use the facility.
- The next no-injection direct-play failure was within the expedition itself: a two-person stage-1 party could reach the boss after an elite node but lacked enough remaining health to finish it. The verifier now behaves like an actually cautious player by filling the three-person party, carrying available supplies, using medicine before routing, and visiting camps when attrition is visible.
- Debug and focused scenarios can leave event-listener MonoBehaviours alive without DI-owned UI/recorder dependencies. Throwing from those listeners pollutes the Console even when the domain scenario itself passes. Event-alert and facility-evolution listeners now keep runtime state but skip only their dependent side effects until injection exists.
- The direct run proved the old default recruitment cadence was too slow for the current 3-person stage-1 offense requirement: by Day 10 a customer had 3 visits and high satisfaction, but no 4th visit arrived during the recruitment wait. Default recruitment now promotes a satisfied 3-visit regular to a recruit candidate; custom rule tests still cover the stricter 4-visit path.
- Recruitment is now explicitly template-safe rather than template-consuming. `CharacterSO` remains a shared species/source template, while `RegularCustomerState` and the promoted world profile own the persistent recruited person. Tests should assert the persistent ID is recruited, not that the shared template can never spawn another person.
- Unity scene serialization can override constructor/default rule changes. `SampleScene` carried the old `RegularCustomerRuntime.rules.recruitCandidateVisitThreshold=4`, so the direct run still behaved like the old cadence until the scene value was changed to 3.

## 2026-07-21 Feature verification findings

- Unity can keep QA request files alive while compilation is broken, so a shell-side "waiting for report" loop is not evidence that a verifier is actually running. Compile errors must be cleared before accepting any PlayMode result.
- ProductShell and CharacterClick both exposed stale Input System mouse devices after scene/UI transitions. The reliable verifier pattern is to use a dedicated verification mouse, make it current, apply `InputState.Change`, queue the state event, and recreate the device if the position still does not move.
- RoomInspection was still closing only the legacy owner-selection flow. The current start-party preparation panel must be completed as part of any gameplay-scene verifier before testing top-right HUD toggles.
- Expedition equipment UI searches must be scoped to the active expedition panel. A global "contains Iron Edge" button search can hit the building crafting button instead of the expedition equip button after crafting leaves the building info surface open.
- Feature batch verification now runs without MCP by writing request markers and letting the Editor open `SampleScene`, enter PlayMode, attach each runner, and aggregate the report. MCP Camera_Capture evidence is still separate and cannot be substituted by this batch.
## 2026-07-20 Character progression audit

- `CharacterSO` and `CharacterRuntimeProfile` currently describe shared authored identity, species, traits, base stats, and derived multipliers. They do not own mutable per-character growth.
- `CharacterActor` requires split runtime components (`CharacterIdentity`, `CharacterStats`, lifecycle, log, etc.) but has no progression component yet.
- Per-character level, XP, learned skill IDs, and equipped skill IDs should therefore live in a new actor-owned runtime component and be serialized by the character world save service.
- Existing offense ability definitions should be reused as the shared skill catalog so progression does not create a second combat-skill system.
- `CharacterCombatAbilityCatalog.GetAbilities` currently grants every species/trait ability directly to the battle combatant. This is the single integration point to filter by a per-character equipped loadout.
- Deterministic XP award points are `BuildingTrainingAbility.ApplyUseCompleted`, successful completion in `WorkTaskExecutor`, and `OffenseExpeditionSystem.OnBattleCompleted`; each fires once per completed activity/result.
- The generated character summary already owns Status, Mood, and Records tabs. A fourth Growth tab can expose level/XP and learned/equipped skills without adding another popup.
- Existing species assets do not currently serialize authored combat ability collections, so the catalog fallback abilities are the effective species skills. Shared unlockable techniques are needed for meaningful level milestones beyond the single innate skill.
- `OffenseExpeditionService.CalculateMemberPower` originally read the base stats multiplier directly; it now goes through `CharacterActor.GetCombatPowerMultiplier` so composition power and actual battle scaling agree on level growth.
- The full-game save debug scenario had retained a legacy single-battle expectation. It now verifies the current route-choice state, and orphan battle snapshots no longer discard an otherwise valid saved journey; both focused progression and full-game round trips pass.

## 2026-07-21 Physical item and hauling findings

- `RegisterComponentOnNewGameObject<ItemPileInfoPanel>` only created the panel when something resolved it, so item pile click events had no listener until the lifetime-scope build callback explicitly resolved the singleton.
- The item marker fallback sprite used a tiny white texture with too high a pixels-per-unit value, making default stock markers effectively invisible in SceneView capture. Lowering fallback PPU gives a visible one-cell marker.
- Delivery/reward physicalization can be introduced safely before removing legacy warehouse deposits by making `WorldItemStackRuntime.TrySpawnStockDelivery` the first path and preserving direct deposit only when the physical runtime is unavailable.
- Shop restock already used a character movement route, while the older instant `RestockFrom` API remains for legacy/debug callers. Gameplay work execution should continue using the physical route.
- Purchase and shoplifting previously only changed money/events and shop stock. Adding carried items at purchase/theft time creates the downstream hook for exit, confiscation, recovery, and theft consequences.
- Restored hauling multipliers must not permanently override runtime option changes; the settings provider now reads the current user setting first.
- The first actual `AIHaul` PlayMode pass found that `AbilityHaul.StartHauling()` reserved a stack, then immediately called `StopHauling("restart")`, releasing and clearing that same job. The worker walked toward the default `(0,0)` route instead of the selected item; reserving after stopping fixes the runtime, not just the verifier.
- A second logistics pass showed loose items could be carried toward a far existing warehouse because warehouse selection used scene-query order. `WorldItemStackRuntime.TryFindWarehouseForStack` now chooses the nearest reachable delivery cell, which also prevents long, confusing haul routes in normal play.
- The physical logistics verifier now proves the full no-injection movement path: loose stack to warehouse, warehouse stock to facility buffer, craft input buffer consumption, crafted equipment output stack to equipment inventory, expedition packed stacks, and carried-weight UI all pass with Console `Error 0 / Warning 0`.
- The item-pile PlayMode verifier still carried a legacy owner-option click path, so the current start-party preparation UI could leave owner selection active and make pile UX verification fail before item interaction. Reusing `StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug()` fixes the verifier entry without changing gameplay item logic.
- File-request PlayMode runners are useful when MCP loses approval during PlayMode/domain reloads. The pile verifier now mirrors the logistics verifier's request-file pattern, which lets the Editor run the test from EditMode even when direct MCP command execution is flaky.
- The remaining warehouse link was conceptual: stock could be deposited into the aggregate `WarehouseInventory` while the player could not inspect a corresponding stored physical stack. Stored warehouse stacks now mirror aggregate stock and are hidden unless the `물품` view toggle is on, keeping ordinary play uncluttered while preserving physical inspection.
- Stored physical stacks must be the restore authority when present. V5 restore now makes warehouse aggregate stock follow stored stack totals, so save/load cannot silently resurrect an old direct-inventory value after items have entered the physical hauling system.
- `CharacterWorldSaveService` already captured and restored `CharacterCarryInventory`; the contract test now verifies that carried items also survive the full `DungeonGameSaveData` JSON boundary.
- Direct-play recruitment exposed a profile/actor authority gap: recruited visitors kept a customer `CharacterSO` template while their world profile had become staff. If later code reset only `CharacterIdentity.CharacterType`, the actor could be released like a visitor and disappear from expedition candidates. `CharacterPopulationService` now treats `WorldCharacterProfile.isStaff` as authoritative and `CharacterSpawner.Interact` refuses to return staff profiles to the visitor pool.
- Offense reward regressions had a stale stock expectation after physicalization. Rewards may now appear as loose dropoff stacks instead of immediate warehouse stock, so tests measure warehouse delta plus physical-stack delta. The recruit-candidate reward handler also intentionally grants at least two candidates.
- `CharacterCarryInventory` could be duplicated in editor/runtime fixtures, splitting carried items between two components and making theft/hauling assertions read the empty one. The carry inventory is now `DisallowMultipleComponent`, and item tests resolve it through `CharacterCarryInventory.Ensure`.

## 2026-07-22 Direct-play completion findings

- The final `truth_core` failure was not a hidden compile or MCP issue; it was a bad natural-play decision path. The verifier accepted "three legally deployable members" even when the final party was not the trained Lv50 core.
- Camp routing had an off-by-one supply assumption. Entering any node consumes one ration, so choosing a camp with exactly two rations left means there is no longer enough food to actually rest at the camp.
- Generated/offense skill verification must treat control and setup modules as real combat actions. Looking only for `OffenseDamageEffect` made valid vulnerability, delay, multi-target, and conditional-amplify skills invisible to the direct-play battle driver.
- Late-stage direct-play proof needs a stronger gate than `CanJoinExpedition`. The product rule correctly blocks only below 25% health or 80 stress, but a player-style final boss run should wait for a healthy trained party and spend camp/medicine before engaging.
- The previous 900x1600 HUD width gap is fixed. Upper-right controls are clamped to the canvas width, top/bottom tab buttons no longer retain template widths, and `Temp/resolution-matrix-report.txt` now verifies 900x1600 gameplay bounds with `RESULT=PASS; failures=0`.

## 2026-07-22 Work amount and construction findings

- GameplayScene PlayMode verifiers must first complete the current start-party fallback before testing world input. Otherwise the owner-selection overlay can remain visible while tests interact with UI behind it, producing false world-click failures.
- `Grid.CountBuilding(BuildingSO)` counts any occupant with the same `GridId`, including a construction site using the target building ID. Tests that need to prove "not instantly built" should inspect the target building layer at the footprint, not the aggregate count.
- Work target scoring needs an explicit facility-role fit term. Without it, a generally supported work type such as Guard can compete on unrelated facilities and flatten species/work specialization. Guard now favors Training/Security facilities, while Research favors Research facilities.
## 2026-07-23 Nameplate, wildlife motion, and zoom findings

- `WorldCharacterNameplate` inherited the actor sprite sorting layer and added only `+36` order. Dungeon walls, floors, and furniture can use later sorting layers or much higher orders, so the text can be visibly covered even while the character sprite itself remains readable.
- `CameraManager` already reads unscaled wheel and keyboard zoom input, but `blockWheelZoomOverUi` calls the broad `IUiPointerBlocker.IsPointerOverUi()`. Full-screen HUD graphics count as UI hits, which suppresses wheel zoom over most or all of the visible world.
- Wildlife ecology produces meaningful intents, but `ChooseReachablePosition` samples only `origin.x +/- distance`, picks an almost deterministic best score, and immediately becomes eligible for another decision after a route finishes. On a side-view exterior surface this presents as repeated left/right pacing.
- Natural wildlife motion needs to preserve the one-dimensional walkable surface while varying cadence and intent: weighted target choice, direction momentum, arrival dwell, sprite facing, and eased/bobbed locomotion are the appropriate fixes; allowing arbitrary vertical cells would reintroduce air/wall traversal bugs.
- The gameplay camera uses the URP `UnityEngine.Rendering.Universal.PixelPerfectCamera`, not the legacy `UnityEngine.U2D` component. Zoom must enter the URP component's Cinemachine-compatible mode before assigning a continuous orthographic size, or `OnPreCull` restores the baseline every frame.
- Runtime sampling after the wildlife changes showed intent-driven cadence rather than synchronized pacing: most animals remained at forage/drink/rest targets while only one moved at a time, and individual positions changed by only zero to two cells over the next sample window.

## 2026-07-23 Customer checkout patience audit

- Staffed shops already keep customers in `WaitForServingWorker` and raise `Operate` urgency, but the wait has no timeout or personality stage. A shop without a worker therefore holds every customer forever.
- `CharacterAiPersonality.patience` and trait/species `waitPatienceMultiplier` exist, but no runtime checkout path consumes the latter.
- `AbilityShopping` always counts a finished interaction as a completed visit. An abandoned checkout needs a separate outcome so the customer avoids that shop without consuming the remaining visit, allowing Utility AI to choose an alternative.
- The action-phase/nameplate path already exposes current AI phases. Checkout feedback can remain lightweight by updating that phase and using one-shot event alerts instead of adding a new always-visible panel.
- A queue reaction must not consume the customer's remaining visit. Marking only the abandoned facility as visited lets the existing shopping Utility AI select another reachable shop and naturally fall back to looking around or exiting when none remain.
- Runtime verification showed the staged path is deterministic at accelerated game speed: an impatient customer reached abandonment, released the queue, lost mood, retained a negative personal facility memory, emitted both service and abandonment alerts, and exposed `구매 포기` as the visible action phase.
# 2026-07-23 Paused stair and low-needs AI audit

- `CharacterVisual.HideForTraversal` uses `Time.realtimeSinceStartup`, `WaitForSecondsRealtime`, and an unscaled expiry check. Therefore the stair coroutine correctly pauses on scaled waits, but its visibility fail-safe restores the actor while the game remains paused.
- `CharacterAiDecisionContext.Capture` chooses its strongest need from every registered need. `FUN` can therefore drive `EmergencyScore`, while `BuildEmergencyJobGivers` has no fun response and falls through to work/wait.
- Emergency job construction currently adds only the single strongest need action. If that facility/action is unavailable, another simultaneously critical survival need is not tried before wait.
- `AIBrain.UseOwnerWorkActions` omits Eat, Rest, Toilet, and Hygiene. Owners with depleted needs have no self-care action to select.
- `AIEat.CanStart` rejects every on-duty worker. Hunger is not part of `WorkDutyController.ShouldTakeOffDuty`, so a starving on-duty worker can remain unable to eat indefinitely.
- Stair traversal itself already uses scaled `WaitForSeconds` and scaled movement. The only pause leak was `CharacterVisual`'s realtime fail-safe, so the correct fix is to scale the fail-safe rather than alter stair timing or DOTween globally.
- Low hunger should interrupt current work without switching the worker off duty. Sending hunger through the off-duty state breaks return-to-work semantics and makes ordinary meals look like schedule changes.
- Emergency selection must retain every urgent survival candidate in weighted order. Choosing only one need makes a missing facility turn a solvable hunger/rest/hygiene combination into a generic wait.
- The focused naturalness regression suite passes after excluding leisure from emergency selection, adding survival fallbacks, and exposing owner self-care actions. Two broader `StaffDutyDebugScenarios` cases around emergency-priority and expedition-return wake-up remain separate pre-existing failures and were not counted as proof for this fix.

# 2026-07-23 Stationary AI fallback audit

- The reported actor is not movement-locked. Its debug panel shows `Emergency -> WaitJobGiver`, no target, and every self-care action rejected by `CanStart`, so the pipeline intentionally commits a stationary wait.
- A wait action is currently allowed to be the terminal fallback even when the actor is healthy enough to move. Repeating that fallback makes a living actor look frozen despite the BT and scheduler continuing to tick.
- Low mood already has a mood-impulse model, but several impulse types map back to `Wait`; without a guaranteed locomotion/micro-action fallback, bad mood can still present as standing in place.
- `AIWait` does request a moving idle behavior, but high recent movement pressure selects `InspectFacilityIdleBehavior`, whose implementation is only `StartWait`. The unresolved emergency then selects the same branch again.
- Queue waiting is the only stationary wait that should remain intentionally static. Ambient inspection, weather shelter, complaint, and unresolved-need fallbacks need a bounded pause followed by movement or a fresh decision.
- Routine/job-giver mood bias alone was insufficient because the final routine-group multiplier could make ordinary work win again. Low-mood autonomy therefore has to be enforced once more at the final cross-group candidate comparison.
- The fixed idle path keeps deliberate queue/chat waits bounded, converts inspection and generic wait into reachable roaming, and ends a failed no-path wait quickly so the next decision can retry instead of holding an infinite action.
- A real GameplayScene probe with no LLM mood impulse held the actor at mood 17-20, selected `RoutineUtility -> Wait -> 기분 내키는 대로 배회`, and visited six distinct grid cells during the observation window.

# 2026-07-23 Dark survival V11 findings

- Runtime-only filth work targets cannot be resolved through the static `BuildingSO` catalog. Their information panel must format the runtime `WorldFilthWorkTarget` directly.
- Runtime work targets created outside prefab injection must receive `ConstructBuildableObject(...)` before initialization so grid and work services are available deterministically.
- Water Tilemap world X is mirrored from logical grid X in this project; verification must query the runtime grid-to-tile conversion rather than assume identical coordinates.
- `Tilemap.GetUsedTilesCount()` reports distinct tile assets, not occupied cells. Visual verification therefore counts source-cell tiles explicitly.
- Filth priority is most reliable as target-owned runtime state: it raises Clean urgency and wakes eligible workers without mutating shared SO data.
- Desperate drinking needs two distinct contracts: stored/clean facility water wins first, while disabling those sources must expose the external unsafe-water fallback and its infection cost.
- A permanent social-memory entry uses `validUntil = 0`; restore must retain zero-duration snapshots because zero means indefinite, not already expired.
- Mood modifies breakdown probability, while `selfCare` and `patience` provide a bounded personality adjustment. Even the most stable personality cannot suppress a forced 100-burden breakdown.

# 2026-07-23 Exterior habitat decoration findings

- The ecosystem already consumed `Grass` and `Brush` resources, but had no world visual bound to those values; the missing link was presentation, not another food simulation.
- The authored flower PNGs are imported as six full cluster sprites. Layering several clusters at stable offsets produces a readable dense patch and lets resource thresholds remove whole clusters without modifying source art.
- Trees and rocks must remain nonblocking visual decoration. They use `OutsideObject`, while wildlife remains on `Default`, so actors pass in front without changing pathfinding or grid occupancy.
- `Grass` and `Brush` intent filtering is required when habitat radii overlap; a foraging animal now consumes only forage patches and a drinking animal only water patches.
- Pure EditMode ecosystem contracts must not create scene decoration roots. Automatic decoration creation is PlayMode-only, while the focused visual contract explicitly creates and disposes its runtime.

# 2026-07-23 Exterior pond visibility findings

- The original default water selector admitted DropZone cells and picked the lowest X cells. That put two tiny sources at Grid `(0,0)` and `(1,0)`, visually buried beside the entrance instead of reading as an exterior water feature.
- A runtime Tile uses `TileFlags.LockColor` by default, so `Tilemap.SetColor` did not tint the generated white/gray sprite. Setting `TileFlags.None` is required for clean/unsafe/foul source colors to appear.
- The logical grid world position is the floor of a three-world-unit cell, while a Tilemap sprite is centered in that cell. A half-height water sprite therefore needs a `0.25 - CellWorldHeight/2` local Y offset to sit on the ground.
- The longest exterior surface run is Grid X `31..59`. Placing shallow water at `56..58` and deep water only at the outer boundary `59` keeps the pond reachable without partitioning the exterior route.

# 2026-07-23 Zoom sky and centered dungeon findings

- The solid sky previously followed only the physical world width. At maximum zoom-out the camera viewed Y `-6..15`, while the sky covered only about `-0.29..14.29`, exposing the camera clear color at the frame edges.
- Background coverage must be the union of padded physical-world bounds and the current orthographic viewport. Recomputing on camera position, size, or aspect changes keeps every zoom level covered without stretching decorative foreground sprites.
- The 27-column dungeon interior was authored at Grid X `4..30` inside a 60-column world. A centered start is X `17`, so both area tags and all 93 authored placements require the same `+13` translation.
- After centering, the entrance resolves to `(17,0)`, the drop zone to X `14..16`, and the interior to X `17..43`. The camera world X is `-29.5`, exactly matching the dungeon interior center.
- Runtime wall-tile inspection confirms outer-wall tiles at both centered boundaries X `17` and X `43`; the maximum zoom-out camera capture shows both edges in frame.

# 2026-07-23 Entrance outer-wall gap finding

- The entrance door correctly occupied Grid X `14..16`, but three invisible `ExteriorZoneMarker` instances shared X `13` through fixture/overlay layers.
- `GridWallTileCalculator` used `GridCell.HasOccupant()` for automatic side walls, so those nonstructural markers made X `13` look like a building and pushed the visible wall outward to X `12`.
- Automatic side-wall topology now reads only `Building` and `Hallway` structural content. Dynamic actors, items, wildlife, filth, construction overlays, and exterior markers no longer move outer walls.
- Fresh PlayMode verification reports rendered wall `X12=false`, `X13=true`; the entrance arch and wall are visually adjacent.

# 2026-07-23 Exact world click finding

- `WorldInfoClickSelectionService` first used exact `Physics2D.OverlapPointAll` hits, but then fell back to `GridCell.GetBuilding()` whenever no collider was hit.
- `GridCell.GetBuilding()` intentionally searches every occupant layer and ranks hallway last, so a bare floor click still returned the cell's `Hallway` object and opened corridor information.
- Ordinary facilities already own runtime colliders. Their selection now requires the pointer world point to overlap that collider; sharing a grid cell or being nearby is not sufficient.
- Structural wall and interior-door visuals are tile-based and do not always own colliders, so they retain a strictly same-cell `GridLayer.Building` fallback. Hallways, dungeon doors, and normal facilities are excluded from that fallback.
- The actual pointer regression clicked a facility collider and opened only that facility, clicked a collider-free hallway cell `(28, 0)` and opened nothing, then verified character-over-building priority. The report finished with zero failures, errors, or warnings.

# 2026-07-23 Consecutive wildlife click finding

- `WildlifeInfoPanel.OnTriggerEvent` assigned `current = wildlife` before calling `popupService.CloseAll()`.
- On the first click the panel was not yet in the popup stack, so the assignment survived. On a consecutive click, `CloseAll()` closed the already-open wildlife panel and `OnClose()` reset `current` to null after the new assignment.
- Clicking a building between wildlife clicks removed the wildlife panel from the popup stack, which explains why the next wildlife click appeared to work again.
- The panel now closes the prior popup stack first and assigns the clicked wildlife afterward. Repeated clicks therefore refresh and retain the same target instead of clearing it.
- The Input System regression performs two consecutive clicks at the same wildlife collider and verifies `CurrentWildlife` and the visible panel after both clicks.

# 2026-07-23 Wildlife horizontal-facing finding

- Wildlife facing used `step.To.x - step.From.x`, but this project's `Grid.GetWorldPos` maps logical X with `origin.x - gridX`; increasing Grid X therefore moves left on screen.
- The source animal sheets face right, so rightward world movement must keep `flipX=false` and leftward world movement must set `flipX=true`.
- Facing now uses the actual world-space X delta between movement-step endpoints, which remains correct if the Grid origin or coordinate mapping changes again.
- The focused natural-motion contract and live GameplayScene checks pass in both directions for every currently spawned species.

# 2026-07-23 Defense interception audit

- Invasion intruders currently run an independent movement coroutine and never enter an engaged state. Defense facilities can delay them, but guards do not stop that coroutine.
- `SuppressPriorityTarget` moves the guard onto the intruder's exact cell and applies one-way damage every `0.55s`; the intruder neither retaliates nor stops advancing.
- Guard work currently behaves like ordinary facility work. `InvasionSpawnedEvent` has no runtime listener that assigns on-duty Guard workers to an intruder.
- Character zero health immediately triggers death and despawn, so retreat and replacement policy directly controls guard survival.
- The existing boss-only owner rally chooses a shared hallway target rather than an Administration room. It must be replaced by evacuation for every invasion.
- The defense feature panel already owns threat, intruder, facility, and report sections, making it the correct home for policy editing and live engagement status. Several strings in that section are mojibake and need replacement while it is changed.
- The current top-level save version is V11 and invasion state already has a dedicated snapshot, so V12 can extend that boundary without mixing policy state into character or shared SO assets.

# 2026-07-23 Defense interception completion findings

- The live intruder coroutine must consult the engagement runtime before every Grid step. Merely pausing facility damage is insufficient because a previously started movement path can otherwise cross the frontline.
- Combat presentation must animate only the actor's visual child. Moving the actor root for a lunge corrupts logical Grid occupancy and can let the intruder or guard appear to cross the line.
- Policy switching keeps the same engagement and intruder reservation while swapping lead and reserve positions. Verification must follow the intruder runtime rather than assume a replacement engagement ID.
- Owner final defense can be planned while the intruder is still several cells away. `InterceptPlanned` is expected until the intruder reaches the reserved stop cell; only then does reciprocal combat begin.
- A real PlayMode run held the intruder at `(1,0)` against a lead at `(2,0)` for at least three exchanges, with reciprocal damage and no additional facility damage. Policy switching changed the lead without moving the intruder.
- The owner reached fallback evacuation cell `(41,2)`. After the non-owner frontline collapsed, final combat held the intruder at `(40,2)` against the owner at `(41,2)` for 20 exchanges with no reserve.
- Unity 6000.3.8 emits one editor-startup `The referenced script (Unknown) on this Behaviour is missing!` warning despite project-wide loaded-scene, prefab, ScriptableObject, animator, renderer-feature, and volume-profile scans finding no missing project scripts. Unity issue UUM-133323 lists the fix in 6000.3.12f1. After clearing that startup-only engine warning, the complete defense probe produced `Error 0 / Warning 0`.

# 2026-07-23 Developer mode findings

- Commands remain maintainable when each provider declares category, exact target contract, mutation status, and execution; the registry validates 112 unique IDs.
- Exact targeting uses pointer colliders for actors/items/facilities and only the resolved cursor cell for GridCell commands. There is no nearest-target fallback.
- Pure overlays do not mark a run modified. Stateful commands do, while palette visibility, targeting, cheats, and overlays reset when developer mode is disabled or a save is loaded.
- The palette remains non-modal at `1600x900` and becomes a bounded scrollable bottom sheet at `900x1600`.
- Camera Capture comparison confirmed the Grid overlay appears only while enabled and leaves no lines, labels, or pooled renderer residue after disable.

# 2026-07-23 Construction material delivery audit

- `WorkOrderRuntime.TryCreateConstructionOrder` immediately requests every missing material through `WorldItemStackRuntime.TryRequestFacilityDelivery`.
- Construction readiness correctly consumes only `FacilityBuffer` stock at the construction destination, so the expected final step is a worker deposit.
- The suspicious path is the delivery-request implementation: it may be representing demand by creating a visible `Loose` stack at the construction cell instead of reserving physical stock at its warehouse/source location.
- Correct behavior is source-preserving: order creation creates demand/reservations, pickup removes quantity from the warehouse stack, and only worker deposit creates the construction-site buffer.
- `TryRequestFacilityDelivery` delegates part of its work to a dedicated `RequestLooseStockDelivery` method. The construction bug is therefore localized around request-time stock conversion and the haul-plan candidate rules.
- Root cause confirmed: `TryRequestFacilityDelivery` calls `warehouse.Inventory.Withdraw(...)` immediately, removes the physical `Stored` quantity, then respawns it at the warehouse cell as a destination-tagged visible `Loose` stack.
- The stack is not teleported to the construction cell, but it is incorrectly dropped onto the warehouse floor before any worker pickup. That is the yellow pile visible immediately after placement.
- The fix must let destination-tagged warehouse stock remain `Stored` and hidden, make it haulable only as outbound reserved stock, and withdraw aggregate warehouse inventory only when the worker actually picks it up.
- Both pickup APIs currently only decrement the selected world stack and add it to `CharacterCarryInventory`; they do not touch `WarehouseInventory`. This confirms the aggregate withdrawal was intentionally front-loaded and must move into pickup for outbound stored stacks.
- Facility deposit already has the correct endpoint: carried items become `FacilityBuffer` only after the worker reaches the destination.
- The haul planner already understands destination-tagged stacks and routes them to `FacilityBuffer`, including multi-pickup plans and partial carry quantities.
- A focused extension is sufficient: allow only destination-tagged `Stored` stacks as outbound haul candidates, then perform the matching warehouse aggregate withdrawal atomically during pickup.
- Stored-stack save restoration currently rebuilds each warehouse aggregate from `destinationId` values prefixed with `warehouse:`. Overwriting that field with a construction destination would lose warehouse ownership on reload.
- Outbound stock therefore needs separate source-storage metadata. Cancellation must clear the delivery destination and merge the reserved quantity back into normal stored stock rather than deleting it.
- `DungeonPhysicalItemSaveData` currently enforces nested version 1 exactly. Source-storage ownership can be added as an optional serialized field while retaining version 1 compatibility; older V12 saves deserialize the new field as empty.
- `WarehouseInventory` exposes bounded `Withdraw` and `Deposit` operations but no reservation model. Reservation should remain physical-stack metadata, with pickup performing `Withdraw` and rolling back via `Deposit` if carry insertion fails.
- Existing item regressions explicitly expect warehouse stock to drop at request time, so they currently preserve the defect. They must assert stock remains unchanged and no visible loose stack appears until pickup, then assert the aggregate drops at pickup.
- `BuildPlacementUxPlayModeVerifier` bypasses hauling by spawning a `FacilityBuffer` directly at the site. It cannot be final proof for this fix and needs either a real-haul path or a separate focused pointer/play verifier.
- The first live haul exposed a second root cause: warehouse storage IDs use `BuildableObject.GridId`, which is the shared building-definition ID. Two warehouses of the same type both became `warehouse:1050`.
- The worker reached the reserved stack's physical warehouse, but pickup resolved the other same-type warehouse by the colliding ID and could not withdraw its stock. Warehouse ownership must use a per-building persistent/runtime instance key.
- Warehouse keys now use `building definition ID + center grid position`, which is stable across saves and unique for same-type warehouses. Legacy two-part IDs are normalized by matching the saved stack position during restore.

## 2026-07-23 Medieval dark fantasy combat V13

- The defense and offense loops previously owned separate damage assumptions. Both now route attacks through `ICombatResolutionService`, with adapters supplying real Grid distance/LOS or formation distance/cover.
- The active equipment instance, not a character template, is the authoritative source for range profiles, attack verb, quality, loaded ammunition, fire modes, recoverable throws, armor layers, shield state, and durability.
- Wildlife hunting still used a bespoke random hit roll after defense and offense had moved to the shared core. It now uses the same line-of-sight, friendly-fire, cover, range, evasion, body-part, and presentation rules.
- Wildlife uses a deliberately smaller body profile: head, torso, and combined limbs. Limb damage lowers mobility/evasion; vital-part destruction kills; the profile is persisted in wildlife save data.
- Ranged hunters now seek a valid firing cell instead of always pathing adjacent, refuse unsafe friendly-fire lines, reload from their physical carry inventory over scaled game time, and stop cleanly when ammo or a firing position is unavailable.
- A PlayMode command probe exposed a manual-move lock leak: owner evacuation could cancel the movement coroutine without clearing `AIBrain.manualCommandActive`. `AbilityMove.CancelActiveMovement` now completes cancelled manual commands and releases the lock.
- Live defense verification retained the intended phase order: 12-second external rally with guards waiting, dispatch only after breach, then four held reciprocal exchanges on adjacent cells.
- Unity Console finished at `Error 0 / Warning 0`. The MCP camera preview renderer failed twice for the live camera, while Unity's direct Game View screenshot succeeded.
## 2026-07-23 Construction material delivery

- The yellow pile was not a harmless preview. `TryRequestFacilityDelivery` withdrew aggregate warehouse stock and respawned it as a visible `Loose` stack at the warehouse cell as soon as the construction order was created.
- Construction readiness already consumed only `FacilityBuffer` stock, so the defect was isolated to the request/pickup boundary.
- Warehouse building-definition IDs are shared by every instance. Using only `GridId` as storage identity caused two same-type warehouses to collide; storage IDs now include the warehouse grid position.
- The correct three-stage ownership model is now explicit: ordinary hidden `Stored` stock, destination-reserved hidden `Stored` stock, then carried stock and destination `FacilityBuffer`.
- A delivery request does not alter aggregate warehouse inventory. Pickup atomically withdraws it, and failed carry insertion deposits it back.
- The pointer-driven build verifier previously spawned `FacilityBuffer` stacks directly. That shortcut was removed so it cannot hide a future request-time drop regression.
- The work-amount save contract had a stale `save.version == 9` assertion despite the product using V12; the assertion now follows `DungeonGameSaveData.CurrentVersion`.

## 2026-07-26 V16 integration audit

- `GameplayScene` contained both the production owner command controller and a priority-command duplicate, plus production and `_Test` regular-customer runtimes. Exact-one composition validation is required because first-match lookup silently accepts this corruption.
- `ExpeditionEquipmentRuntime` and `ICombatEquipmentRuntime` both authored inventory, loadouts, crafting, offense modifiers, and save data. Offense applied both bonus paths, so consolidation must remove the legacy stat block rather than adapt both indefinitely.
- The common combat runtime already owns persistent equipment instance IDs, quality, durability, ammunition, and active loadouts, making it the correct authority. Its missing piece was work-unit crafting and physical material/output integration.
- Offense weakening, prisoner rewards, special-monster rewards, and recruit rewards are counters only. They must become regional pressure or pending physical/persistent arrivals before the old reward-state fields can be removed.
- `SurvivalFoodRuntime` both withdraws food at daily settlement and allows real meal completion to consume food. Daily withdrawal must become forecast/reporting only to avoid double consumption.
- Exterior incidents currently advance through text and timers without persistent actors, inventories, theft stacks, rescue patients, or handler-owned stages.
- Circus fame and injury history are recorded but do not gate treatment, contracts, release, or performer availability.
- Blood and memory extraction currently collapse into generic Mana-style stock. They need Biological and Knowledge categories plus physical, work-based consumers.
- `CharacterAiPerfSettingsSO` and report types exist without a runtime recorder, so the current performance surface cannot provide trustworthy rolling avg/p95/max/GC/path-cache evidence.
- Offense targets currently carry no region or faction identity. The human/rival reward handlers only increment `OffenseRewardState` counters, and the terminal truth target still grants a meaningless rival weakening reward.
- `ExteriorActivityRuntime` already owns visible departure/return movement, a physical entry point, body-health checks, and medical-order creation. V16 return rewards should attach to this completed return boundary rather than create a parallel arrival animation service.
- Exterior incident persistence currently stores only kind, zone, text, and remaining seconds on a zone marker. There is no handler-owned actor, inventory, stage, or outcome state.
- V15 offense persistence serializes all abstract reward counters inside `DungeonOffenseRewardSaveData`; regional pressure and pending arrivals should be independent domain sections so the offense service no longer owns their restore order.
- Save restoration is already dependency-sorted by section and phase, so V16 can express the required equipment/items → characters/wildlife → captivity → arrivals → incidents → regions order without central orchestration.
- `InvasionIntruderRuntimeFactory` is the single constructor boundary for runtime intruder actors and is a suitable point for applying a captured regional pressure snapshot once per spawn.
- Offense enemy templates are materialized in `OffenseEncounterCatalog.CreateEnemies`; applying regional armament/manpower factors there avoids a second post-construction stat mutation path.
- Invasion intruder health and attack configuration is finalized in `InvasionIntruderRuntime.Initialize` inside `InvasionIntruderSystem.cs`, so regional modifiers should be supplied with the spawn settings before actor health is scaled.
- `CharacterPopulationService` already owns deterministic persistent IDs and full generated growth profiles, but exposes no API for adding a reward candidate. Extending that boundary avoids a second profile generator.
- Recruitment activation can bind an actor back to an existing population profile by matching `CharacterIdentity.PersistentId`; a reward candidate therefore needs a population profile and a matching `RegularCustomerRecord`, not a counter.
- Wildlife spawning is private to `WildlifeRuntime`. A narrow `TrySpawnArrival` method on `IWildlifeRuntime` can reuse the catalog, grid validation, hierarchy, actor initialization, and registry path without exposing general mutation internals.

## 2026-07-26 AI profile boundary and allocation findings

- `CharacterNeedCatalog.All` rebuilt a sorted array on every access. Survival scoring calls it
  for each AI candidate, making the catalog a measurable allocation hotspot at population scale.
- Offscreen/nonselected actors do not need full utility strings or breakdown objects. Retaining
  compact numeric scoring while collecting details only for selected diagnostics preserves
  decisions and substantially lowers garbage.
- `WorldCharacterNameplate` previously captured a complete deprivation snapshot only to display
  the highest burden and breakdown state. A narrow display-state query avoids grouping,
  dictionaries, and arrays on every visible nameplate update.
- The first PlayMode profile implementation sampled immediately after forced GC and reused the
  last warmup scheduler timing. That made a 2.6-second sample window report impossible 17-second
  scheduler frames. Discarding two transition frames produces coherent wall, frame, and
  scheduler timing.
- Unity 6000.3.8's Mono runtime returns zero from
  `GC.GetAllocatedBytesForCurrentThread()` even around a known 4KB allocation. Scheduler-only
  allocation is therefore explicitly reported as unsupported; `GC Allocated In Frame` remains
  the authoritative Editor-wide counter.
- The stabilized 100-character result is frame `2.77ms average / 3.42ms p95`, scheduler
  `0.370ms average / 0.497ms p95 / 0.632ms max`, all 100 trees ticked, and zero decision/path
  budget overflow.

## 2026-07-26 Weighted navigation and 500-character profile

- Unweighted BFS could not represent shallow-water speed, door policy, traversal penalties,
  or cost-aware target choice. Fixed destinations now use A*, while multi-target candidate
  scoring retains a weighted Dijkstra field.
- The current 60x3 gameplay and 96x3 stress grids are small enough that one Job per route
  would add more scheduling overhead than search work. The optimized weighted A* benchmark is
  about 11.3 microseconds per query.
- The largest apparent 500-character hotspot was an Editor test provider repeatedly calling
  `FindFirstObjectByType<GridSystemManager>`. Caching the fixture manager reduced the
  diagnostic average from 13.61 ms to 2.60 ms.
- The final staged 500-character profile passed: frame average/p95/max
  `3.39/4.37/15.40ms`, scheduler `1.228/1.809/2.580ms`, and no sampled frame exceeded
  16.67 ms.
- Broad multithreading is therefore deferred. Only immutable, batched offscreen scoring or
  route requests are safe future candidates; Unity objects, door access, reservations, and
  route commit remain main-thread responsibilities.

### V18 identity and physical-state follow-up

- Warehouse storage destinations were still generated from `GridId:centerX:centerY`, with `GetHashCode()`
  for non-building implementations. V18 now requires the warehouse's typed `BuildingInstanceId` at the interface.
- A physical-stack-derived stock query is safe as a non-owning index during cutover, but remaining
  `WarehouseInventory.Deposit/Withdraw` callers must move to the transfer service before its quantity dictionary
  and snapshot fields can be deleted.
- Warehouse snapshot V3 now proves that aggregate stock is not a save authority: only capacity and acceptance
  policy serialize, while the derived dictionary is cleared on config restore and rebuilt from physical stacks.
- The old equipment item component stored only identity, definition, material, quality, and durability.
  Ammunition, owner/world state, evolution, slots, and module condition were absent. Schema V2 carries the full
  equipment and attached-module payload as the prerequisite for removing separate equipment-instance persistence.

### V18 Phase 85 single-authority findings

- Removing only the combat save lists was insufficient: carried items retained `sourceStackId` and components but
  dropped `ItemInstanceId`, so equipment could fork when deposited into a new physical stack. The carried DTO and
  transfer API now preserve the typed instance identity explicitly.
- `SpawnUnique` always allocated a fresh instance ID. Crafting output and loadout drop therefore needed a separate
  `SpawnExistingUnique` path that materializes repository-owned unique state without minting another identity.
- The former deposit path searched equipment by its old source stack and synthesized a normal-quality replacement
  when lookup failed. This silently discarded durability, material, modules, and lineage; it now fails loudly.
- Combat material policies still keyed facilities as `definitionId:x:y`. Changing that key to the required
  `BuildingInstanceId` removed another coordinate-based persistence fallback exposed by the Phase 85 regressions.
- Equipment modules must be restored after equipment shells but before slot-reference sanitization, and tests must
  use a slot-bearing definition. The failed dagger fixture exposed that a valid zero-slot item correctly discards
  an impossible installed-module reference.
- Warehouse tests can use Editor-only physical-stock fixtures, but the production API no longer exposes aggregate
  writers. The old `Deposit/Withdraw/AddStock` names were removed so new tests cannot normalize a second authority.

## 2026-08-01 Branched production network V3

- The production dependency catalog now indexes recipe, equipment, construction, facility-supply,
  medical-procedure, and defense-ammunition consumers instead of treating recipes as the whole graph.
- Concrete recipe inputs contain no `stock-item:*`; flexible fuel/feed selection remains available only
  through value-bearing facility supply profiles and persists the selected concrete item ID.
- Shared intermediates require at least two real direct consumers, strategic intermediates require three,
  fake `sink:*` consumers are rejected, and post-acquisition conversion depth is capped at four.
- All production facilities own separate persistent input/output buffers. Output space is reserved before
  work starts, so a full output pauses only that bill and does not corrupt an upstream conveyor or worker.
- The old wort-only chain was removed. Malt, fermented liquor, grape juice, curd, dough, filling,
  salted meat, ration mixture, washed vegetables, and brined vegetables now branch into real products.
- Production order persistence is V4; research/equipment compatibility is V5 and rejects preceding V4 runs.
- Medical procedures are first-class research rewards. Dedicated construct-core engineering and dining
  operations facilities close the final direct-reward gaps without adding dummy recipes.
- Final Unity MCP regression report passes resource generation, equipment, the production graph,
  production runtime contracts, research/equipment validation, and pacing at 32.2/80.4/234.3/372.0 days.
- Unity MCP captured the active Main Camera at 1920x1080 and the final Console audit returned
  `Error 0 / Warning 0`.

## 2026-08-01 Item architecture V6 audit

- `ResourceItemDefinitionSO` mixes identity, economy, production classification, research,
  food, medicine, fuel, feed, and intermediate policy in one flat asset.
- `DungeonItemCatalogSO` is present but its serialized list is empty; runtime lookup falls through
  several hardcoded static definition classes and synthetic `stock-item:` / `equipment-item:` IDs.
- `ResourceItemDefinitionSO.ToDungeonItemDefinition` discards every optional behavior field, so
  consumers must query both the physical-item and resource-economy catalogs.
- `ResourceDungeonItemCatalogProvider.TryGetDefinition` currently fabricates a default definition
  for unknown IDs and therefore reports success for missing content.
- Mutable equipment and food state live in separate systems keyed back to a world stack ID, while
  corpse and contamination fields are embedded directly in the generic stack save DTO.
- The target must keep SOs immutable and consolidate authoring, without moving per-instance state
  into shared assets.
- `DungeonStory.Items` is a low-level assembly with no gameplay-model references, so canonical
  authoring belongs in the economy/model assembly while generic instance persistence stays in Items.
- A strict Resources index can load `ItemDefinitionSO` across `SO/**`; resource economy then becomes
  a typed projection over the same authored definitions instead of a second item authority.
- Stack compatibility must use definition ID plus stack-affecting component state, preventing
  fresh/spoiled, damaged/pristine, or provenance-bearing instances from merging by ID alone.
- The base resource builder intentionally precedes research-overhaul reward generation. A unified
  rebuild that stops after the base builder erases V3's extended item-consumer graph, so the item
  pipeline must run resource -> combat -> research/overhaul before indexing final definitions.
- `PhysicalItemDebugScenarios` still contains a legacy `save_v10_contract` assertion while the
  current global save contract is V17; that isolated failure is not an Item V6 schema regression.
- Final unified generation contains 296 canonical SO definitions. The dedicated generated folder
  contains 110 survival/wildlife/medical/special/equipment assets and all 110 reference the concrete
  `GenericItemDefinitionSO` script GUID; missing-script references are zero.
- Item V6, production V3, research/equipment, signature isolation, and pacing all pass together at
  32.2/80.4/234.3/372.0 days. Unity MCP captured Main Camera at 1920x1080 and Console ended 0/0.

## 2026-08-01 V18 authority-normalization audit

- Item V6 did not finish the single-authority cutover: `DungeonItemCatalogSO`, hardcoded `*ItemDefinitions`, installation/blueprint synthesis, `stock-item:*` conversion, and fabricated unknown definitions remain reachable at runtime.
- Warehouse aggregate counts and physical stacks are both mutable and separately persisted; equipment instances are also stored beside physical item components. These are P0 duplicate-authority defects.
- Persistent ownership still falls back to actor names, `GetInstanceID()`, positions, and definition IDs in multiple character, building, combat, wildlife, and reservation paths.
- `GameData` is a mutable ScriptableObject for money/calendar/speed state, and direct money access bypasses the transaction runtime in many callers.
- Both legacy offense and V17 offense runtimes/save sections are registered. The final system must retain V17 behavior and remove the legacy bridge and duplicate save ownership.
- Save restore validates section order but applies live sections sequentially, so a late failure can leave a partially restored world.
- The project has 784 runtime files and roughly 288K lines in default `Assembly-CSharp`, about 501 runtime interfaces, roughly 401 optional-interface parameter occurrences, and 12 product files above the existing 2,169-line ratchet.
- Architecture tests are stale: they still assert older save/version and resource-loading expectations and rely heavily on source substring/regex checks.
- The planning session-catchup helper failed after detecting 73 unsynced messages because the Windows CP949 console could not encode U+2013. Context was recovered from the planning files and `git diff --stat` instead.
- `DungeonItemCatalogSO.cs` still contains all forbidden Item V6 escape hatches in one place: `FromStockCategory`, equipment synthesis, `GetDefinitionOrDefault`, blueprint synthesis, installation-kit synthesis, and a fabricated generic definition for every unknown ID.
- `ResourceDungeonItemCatalogProvider` also has four optional constructor dependencies and constructs its own Resources loader/catalog, so strictness depends on how it was instantiated.
- The canonical `ResourceItemDefinitionCatalog.GetRequired` already has the desired fail-loud behavior; the old provider can become a thin projection over that catalog without inventing a second definition type.
- The root save version is still 17 and save-slot incompatibility strings are mojibake. Several debug scenarios still pin V16 or V17 explicitly, confirming the existing architecture ratchet is stale.
- `DungeonSaveSectionRegistry.RestoreAll` validates duplicate/missing envelopes but invokes live `Restore` methods in sequence immediately; it has no preflight/staging contract and labels current failures as V16.
- No authored `ItemDefinitionSO` has a `stock-item:*` ID. Those IDs remain synthetic runtime-only identifiers even though Item V6 reported a strict 296-definition catalog.
- Equipment IDs are authored (`equipment-item:*` has 86 YAML occurrences), but stock-category calls in surgery, survival, offense, shops, wildlife, grand projects, fluids, and tests would fail immediately under a strict catalog.
- Therefore Phase 83 must eliminate stock-category item creation at each call site before making the provider fail-loud. Recipe/material inputs become concrete item IDs; flexible facility fuel/feed remains tag/value selection rather than a fabricated item.
- The canonical catalog has no Water or Blueprint-category item entries. `resource:clean-water` exists but is incorrectly authored as General; seven facility-blueprint assets exist but have no corresponding item-definition SOs.
- A deterministic concrete default can replace remaining category-to-item spawn requests during the cutover: preserved ration, lumber, dagger, mana crystal, clean water, standard medicine, low fuel, arrow, blood, memory residue, and a real blueprint definition. This is a concrete-ID compatibility mapping, not a synthetic definition.
- `UnifiedItemDefinitionAssetBuilder` is currently still a second content source because it calls hardcoded `*ItemDefinitions` to generate canonical assets. After the existing assets are made complete, those branches must become explicit one-time migration code and runtime hardcoded definition methods must be removed.
- The resource loader already supports `LoadRequired<T>`, so one required `GameContentCatalogSO` bootstrap can replace item `Resources.LoadAll` without adding Addressables.
- `DefenseCombatPresentation` was another hidden authority path: it constructed static combat and item catalogs inside a MonoBehaviour. Its weapon sprite lookup can use the actor's already-injected `IWorldItemStackRuntime.CatalogProvider` and the authored equipment item ID instead.
- The explicit catalog cutover requires 604 definitions today: 296 pre-existing physical items, 301 building installation kits, and 7 research blueprints. This closes the two runtime synthesis categories without using dummy fallback definitions.

## 2026-08-01 V18 Phase 86 findings

- Mutable `GameData` was not isolated to one manager: UI pause flows, debug commands, settlement, shops, construction, and save restore all wrote its reactive fields. The cutover required named authorities plus updated Editor fixtures, not a type rename alone.
- Static run leakage existed in four independent forms: a user-settings `Current`, active carry inventories, cover durability by source ID, and skill execution/work snapshots. The correct scopes differ: a run service for cross-entity lookup and an actor component for actor-local transient state.
- Presentation and character-skill settings were still synthesized or resource-loaded outside the root catalog. Explicit root references now turn missing assets into boot/validation failures.
- Mandatory typed IDs correctly caused older test factories to fail at initialization. Tests now construct identities before domain initialization instead of weakening the runtime invariant.
- Phase 86 regression failures exposed stale V12 compatibility expectations and fixtures without physical stock views; both were corrected to the V18 boundary and derived-stock architecture.
# V18 Phases 87-88 findings

- The offense duplication was not limited to naming: four separately registered save sections captured overlapping expedition state. They are now one aggregate and only `offense.aggregate` is registered.
- Direct scene-runtime providers had leaked into recruitment, first-run objectives, codex, and the expedition feature UI. The new query/application boundary removes those cross-domain MonoBehaviour dependencies while keeping providers internal to offense persistence/composition.
- The former registry validated section presence only while mutating each section immediately. V18 restore now validates the manifest, envelopes, typed payloads, IDs, content references, and aggregate references before commit.
- Unity world replacement cannot be made by swapping a plain object graph because many current aggregates own scene objects. The implemented transaction therefore stages all serialized data first and captures a complete live rollback image before commit; an injected last-stage failure verifies no observable live state remains changed.
- Live PlayMode capture contains 54 sections and round-trips through the new manifest/preflight/transaction path successfully.
# V18 runtime authority findings — 2026-08-01

- The dominant defect was not raw class size but parallel ownership: SO/code fallbacks, physical/aggregate stock, physical/equipment instances, and multiple offense saves.
- A strict root catalog exposed real missing registrations immediately (`FacilityCrimeSettingsSO`, then `CharacterAiNaturalnessSettingsSO`); widening the editor collection scope to all authored Resources assets fixed the catalog rather than reintroducing fallbacks.
- Presentation assets were also bypassing the root through string paths. `GameMediaCatalogSO` now explicitly references audio, TMP font settings, title icon, and door material.
- Unity MCP import can report success before all dependent editor assemblies rebuild. DLL timestamps and `Editor.log` are the reliable freshness checks; clean compilation is required after cross-assembly signature changes.
- Large block rewrites must preserve UTF-8 explicitly. PowerShell's default `Get-Content` decoding corrupted Korean literals once; the affected files were reconstructed from Git UTF-8 sources before continuing.
- Character progression test fixtures were overwriting their immediate generator through a later generic dependency injection call. Reordering injection restored deterministic passive/active/ultimate tests.
- Strict persistent IDs correctly exposed a population promotion fixture that initialized an actor before assigning its profile ID; the fixture now assigns the persistent ID first.
- Current top remaining sizes are roughly 3.1k lines for deprivation, 3.0k performance probe, 2.6k equipment, 2.5k AI brain/offense, and 2.4k surgery. These require responsibility extraction, not region-only partial files.

## 2026-08-01 Phase 90 decomposition findings

- A line-count limit becomes useful only when it is a ratchet. Recording each existing exception with its current maximum allows legacy debt to compile while making every new violation and every regression fail immediately.
- `CombatEquipmentRuntime` mixed five ownership concerns: physical item state, character loadout references, crafting queues, module processing, and lineage-transfer orders. Extracting modules and lineage as state-transition Aggregates preserved the physical item repository as the only equipment/module authority.
- Persisting an equipment component was duplicated between the facade and module operations. `CombatEquipmentPhysicalStateWriter` now encodes repository-owned equipment plus attached modules through one path.
- Test-only absence is a capability, not `null`. Explicit empty catalogs and unavailable research capability objects preserve isolated fixtures without permitting production constructors to invent fallback rules.
- Deterministic seeded calculations and session randomness are different contracts. The former now uses a small deterministic sequence; the latter remains injected through `IRandomStreamProvider`.
- The project had no Unity Localization package or String Table assets, so merely introducing an error enum would still leave sentence ownership in code. Localization 1.5.9 and an active Korean `DomainFailures` table now provide a real presentation boundary.
- A String Table asset alone is not loadable at runtime: the active `LocalizationSettings`, registered locale, project locale, and Addressables localization groups must all exist. The validator now checks the authored settings/table, while the MCP regression proves runtime resolution.

## 2026-08-01 full-goal continuation audit

- The worktree contains 1,666 changed/untracked entries because the content migration authored hundreds of SO assets; unrelated user edits must continue to be preserved.
- The current architecture baseline still contains 53 oversized source exceptions. The largest are deprivation 3,410, performance diagnostics 3,245, AI brain 2,858, offense expedition 2,786, grid 2,567, surgery 2,565, and wildlife 2,513 lines.
- A broad source audit finds 738 production-code `out string failureReason/errorMessage/reason` declarations or call sites. The 21-code equipment slice is therefore only the first domain-error migration, not evidence that localization is globally complete.
- Fifteen production files still define or reference `*RuntimeProvider` types. Each must be classified as a real scene/capability boundary or removed as a policy-free wrapper.
- The runtime source scan still finds one direct `Resources.Load/LoadAll` occurrence and three `CreateInstance` occurrences; validator allowlists must be checked against the actual central loader/editor-only intent rather than assuming zero from a raw count.
- There are 850 non-Editor C# files under `Assets/Scripts`; an asmdef inventory must use filesystem enumeration because the first `rg --files` pipeline returned no entries despite the known Foundation assembly.
- `CharacterDeprivationRuntime` owned two clearly separable state groups before any pathfinding split: persistent deprivation state keyed by character and non-persistent safe-relief diagnostics. Moving both first reduces coupling for the later safe-drink planner extraction and prevents pathfinding code from becoming another save authority.

## 2026-08-01 deprivation decomposition findings

- 안전 음용의 “대상 선택·접근 예약”과 “코루틴 실행·재시도 제한”은 수명이 다르다. 전자는 `CharacterSafeDrinkPlanner`, 후자는 `CharacterSafeReliefRunner`가 소유해야 죽음·취소 시 예약과 실행 상태를 각각 명확히 해제할 수 있다.
- 붕괴 행동은 영속 결핍 상태를 소유하지 않는다. `CharacterBreakdownActionRunner`는 실행 중 actor ID와 코루틴 디스패치만 소유하고, 영속 상태는 계속 `CharacterDeprivationStateStore`에 남긴다.
- 이동 경로 재시도는 식수와 모든 붕괴 행동이 함께 사용하는 실제 정책이었다. `CharacterEmergencyMovement`로 추출해 두 행동 런너가 동일한 긴급 경로 실패 의미를 사용하게 했다.
- 감염, 금기 기억, 목격자 기분, 붕괴 종료는 행동 종류와 무관한 후속 효과다. `CharacterDeprivationConsequences`로 모아 행동 런너가 메인 런타임을 콜백 호스트로 참조하지 않도록 했다.
- 메인 런타임은 이제 tick/부담 계산/공개 질의/저장 조정에 집중하며 1,123줄이다. 이는 단순 partial 분할이 아니라 상태 권위와 실행 책임을 분리한 결과다.

## 2026-08-01 authoritative Phase 90 inventory

- 현재 기준선 예외는 52개다. 결핍 런타임 예외 1개가 실제 제한 충족으로 제거됐다.
- 프로젝트 asmdef는 플러그인 제외 18개이며 Foundation/Infrastructure/Presentation과 모델 계약 어셈블리는 존재한다. 그러나 대부분의 서비스 구현은 여전히 기본 `Assembly-CSharp`에 남아 있어 “asmdef가 없다”가 아니라 “구현 이동이 미완료”인 상태다.
- 비 Editor 직접 `Resources.Load` 1건은 루트 카탈로그만 읽는 승인된 `ResourcesAssetLoader`다. 비 Editor `CreateInstance` 3건은 콘텐츠 정의가 아니라 런타임 Tile 표현 생성이다. 현 validator가 금지하는 Definition/Settings/SO 합성과 구분된다.
- 실제 `*RuntimeProvider` 정의는 클래스 22개, 인터페이스 19개다. 이전 15개는 파일 수 기반 값이어서 최종 제거 조건의 정확한 분모로 쓰기에 부족했다.
- 다음 대형 예외는 성능 Probe 3,245줄, AI Brain 2,858줄, 원정 2,786줄, Grid 2,567줄, 수술 2,565줄 순이다.
- 성능 Probe의 3,245줄 중 570줄은 직렬화 모델·옵션, 약 1,100줄은 측정 월드 생성/밀집 시설 배치/스트레스 개체 생성, 약 370줄은 월드 상태 요약이었다. 이를 별도 소유자로 옮기자 MonoBehaviour는 실행 수명·ProfilerRecorder·파일 출력만 담당하게 됐다.
- 런타임 생성된 스트레스 테스트용 SO 복제본의 수명은 월드 구성기가 소유하며 `IDisposable`에서 파기한다. 콘텐츠 권위 SO 합성이 아니라 프로파일 전용 임시 복제라는 경계도 명확해졌다.
- `AIBrain.cs`는 뇌만 큰 것이 아니라 별도 런타임 객체인 `AIActionPlan`과 `AIAction` 548줄을 같은 파일에 담고 있었다. 이를 독립시킨 뒤에도 뇌 본체가 2,319줄이므로 행동 선택/제어 상태 분해가 계속 필요하다.
- Character stat 회귀는 코드 컴파일보다 강한 에셋 검사를 수행하며, 현행 `Customer_Orc.asset`에 `stat:shooting`이 없음을 드러냈다. 구조 리팩터링 회귀와 콘텐츠 완전성 실패를 분리 기록해야 한다.
- WorkAmount와 Combat 회귀의 실패는 새 코드 경로가 아니라 오래된 fixture가 필수 persistent building ID와 root anatomy catalog를 구성하지 않는 데서 발생한다. 최종 회귀는 엄격한 운영 계약을 약화시키지 않고 fixture를 갱신해야 한다.
- `WildlifeActor.Initialize`와 `NextRange`는 필수 난수 주입이 없을 때 `new RandomStreamProvider(1)`을 생성해 운영 규칙을 바꾸고 있었다. 이제 구성 전 초기화는 명시적으로 실패하고, 시각용 Sprite/Material만 재구축 가능한 별도 캐시에 남는다.
- AI 스케줄러의 힙 항목은 파생 인덱스이며 저장 권위가 아니다. actor 등록 집합과 due-time 사전을 참조하는 `CharacterAiDecisionSchedule`로 분리해 Clear/Remove/Schedule/Take가 한곳에서 버전 무효화를 수행하도록 했다.
- `CharacterEnvironmentRuntime.cs`에는 노출 상태 런타임과 770줄짜리 작업 배정 정책이 함께 있었고 두 객체 사이에 상태 권위 공유가 없었다. 파일 분리만으로도 실제 클래스 경계와 소스 책임이 일치했다.

## 2026-08-01 fluid-network findings

- 첫 유체망 추출 뒤 본체에 수질 변경과 스냅샷 조립 구현이 중복으로 남아 있었다. 최종 구조는 수질 변경을 `FluidNodeWaterRules`, 읽기 모델 조립을 `FluidNetworkSnapshotBuilder`, 실시간 조정을 `FluidNetworkRuntime`이 각각 소유한다.
- Unity MCP 동적 명령의 컴파일 성공은 변경된 프로젝트 어셈블리의 재컴파일을 뜻하지 않는다. `CompilationPipeline.RequestScriptCompilation` 완료와 Console 확인을 이후 모든 회귀 증명의 선행 조건으로 둔다.
- 강제 전체 빌드가 이전 집중 테스트에서 가려진 추출 오류를 드러냈다. 향후 분해 단계마다 전체 컴파일을 먼저 통과시켜야 한다.
- 산업 디버그 시나리오는 과거 141개 연구/32개 시설을 고정하고 있었다. 권위 에셋은 연구 168개, 산업 분야 45개, 상하수 분야 9개, 산업 시설 36개다.
- `ExteriorZoneMarker`는 단순 DTO가 아니라 시설 작업, 사건 상태, 저장 캡처, 그리드 등록/해제를 모두 소유하는 독립 런타임 객체였다. 같은 파일에 둘 이유가 없었고, 분리 후 외부 활동 조정기는 구역 생성·사건·원정 이동만 담당한다.
- Wildlife 오버레이는 생태 상태가 아니라 언제든 재구축 가능한 표현 캐시다. 별도 `IDisposable` 객체가 Sprite/Texture/Renderer 수명을 소유하게 해 생태 저장 권위와 분리했다.
- 기본 난수 폴백 제거 후 실패한 Wildlife 회귀는 운영 결함이 아니라 fixture DI 누락이었다. 테스트도 실제 `ConfigureRuntimeServices` 계약을 사용하도록 수정해야 엄격한 생성 경로가 유지된다.
- 축산의 자동 도축 후보 목록은 영속 상태가 아니라 정책 재평가용 재사용 버퍼다. 별도 평가기가 소유하게 하자 본체의 `animals`/`policies` 사전만 저장 권위로 남고 후보 그룹은 언제든 재구축 가능해졌다.
- 서커스 프로그램 예측은 공연 주문을 변경하지 않는 읽기 모델이다. `CircusProgramForecastService`로 분리하자 공연 주문 상태 전이와 UI용 예상 수익/위험 계산의 권위가 명확히 갈렸다.
- 산업 기능 표면과 탭 요약은 같은 도메인 데이터를 읽지만 다른 화면 계약이다. 별도 Presenter로 두면 한쪽 레이아웃 변경이 다른 쪽의 800줄 제한을 다시 침범하지 않는다.
- 캐릭터 요약 팩토리의 패널 경계/RectTransform 생성은 데이터 바인딩과 무관한 공용 View 구성 규칙이므로 `CharacterSummaryRuntimeLayout`이 소유한다.
- 해상도 후보 탐색은 설정 모달의 상태가 아니라 플랫폼에서 다시 계산할 수 있는 카탈로그다. 입력 단축키 MonoBehaviour도 모달 조정기와 별도 Unity 수명을 가진다.
- 사장 선택의 저장 모달 탐색과 라벨/레이아웃 생성은 선택 상태 전이와 무관한 View 규칙이며 별도 정적 도우미로 분리할 수 있다.
- 시설 정보 화면의 작업 버튼/진행도 렌더링은 시설 선택 상태를 소유하지 않는 순수 View 생성 책임이었다. `BuildingInfoActionViewFactory`로 옮기자 MonoBehaviour는 대상 추적과 갱신 수명만 담당한다.
- 구형 시설 납품 회귀는 물리 아이템 생성 실패 뒤 집계형 창고 입고로 내려가 결제 후 예외가 날 수 있는 실운영 결함을 드러냈다. 집계 쓰기 경로를 제거하고 물리 런타임 누락/생성 실패를 무변경 실패로 고정했다.
- 타이틀 캔버스와 EventSystem 생성은 화면 흐름 상태가 아니라 장면 UI 인프라 수명이다. 난이도/생존압 표시와 저장 슬롯 메타데이터 포맷도 입력 명령과 무관한 읽기 표현이므로 별도 소유자로 분리했다.
- 창고 기능 표면은 Query가 물리 재고/전망을 읽고 Command가 납품·정책·계약을 변경하는 명확한 경계를 이미 인터페이스로 갖고 있었지만 구현이 한 파일에 섞여 있었다. 구현 파일도 분리해 변경 방향과 소스 소유권을 일치시켰다.
- Unity MCP Console 조회는 실패한 프로젝트 컴파일을 한동안 0건으로 반환할 수 있었다. `Library/ScriptAssemblies` DLL 갱신 시각과 `Editor.log`의 `error CS`를 함께 확인해야 오래된 어셈블리로 회귀를 실행하는 오류를 막을 수 있다.
- 생산 작업대 연결선은 저장 상태가 아니라 선택 시 재구축되는 월드 표현이다. 패널 Presenter 밖의 전용 렌더러가 GameObject/Material 캐시를 소유하고, UI 행·버튼·진행도는 상태를 갖지 않는 View 팩토리로 분리하는 것이 맞다.
- 방어 기능 표면도 Query/Command 계약은 이미 분리돼 있었지만 두 구현과 Presenter가 한 소스에 묶여 있었다. 파일 경계를 계약 경계와 맞추자 화면 선택 상태, 방어 읽기 모델, 정책/시설 명령의 변경 이유가 분리됐다.
- 수술 창 파일에는 694줄 응용 서비스와 450줄 MonoBehaviour가 나란히 있었고 서로의 가변 상태를 공유하지 않았다. 파일 분리만으로 수술 규칙 조정과 반응형 UI 변경의 컴파일/탐색 경계가 명확해졌다.
- 연구 카탈로그를 168개로 확장한 뒤에도 수술·생산 작업대·종족 방어·서비스룸·경험 페이싱 픽스처가 141개를 고정하고 있었다. 한 회귀만 고치는 대신 모든 명시적 구형 연구 수 계약을 제거해야 전수 검증이 일관된다.
- 수술 회귀가 `RebuildAll()`을 호출해 작성된 콘텐츠를 검증 전에 덮어쓰고 있었다. 회귀는 `ValidateBuiltContent()`만 호출해야 하며, 연구 회귀도 빌더 실행 없이 현재 카탈로그를 검사해야 SO 최종 권위가 유지된다.
- 루트 도메인 카탈로그의 376번째 참조는 존재하는 AI 설정 에셋이었지만 YAML의 `m_Script`가 0으로 끊겨 있었다. GUID 복구 후 `ResourceGameContentCatalog` 전체 검증이 다시 통과했다.
- 연구 트리의 큰 줄 수는 단일 알고리즘 때문이 아니라 네 수명(데이터 표현, UI 요소 생성, 그래프 pan/zoom/center, 창 열림 중 일시정지)이 MonoBehaviour에 합쳐진 결과였다. 각 협력 객체는 저장 권위를 갖지 않고 입력과 파생 표현만 소유하므로 창 본체에는 선택·큐 명령·반응형 조정만 남겼다.
- 연구 저장 회귀는 실제 구현이 이미 섹션 V5 미만을 거부하는데도 과거 V3/V2 이관 이름과 더 짧은 구형 오류 문구를 기대했다. 테스트 이름과 판정을 현재 명시적 비호환 계약에 맞추되 운영 구현의 엄격한 거부 동작은 완화하지 않았다.
- 인스턴스 진화 화면의 장비 선택·안정제·정밀 재단조 선택은 시설 진화 상태가 아니라 화면 세션 상태다. 이를 `InstanceEquipmentEvolutionSection`이 소유하게 하고, 시설 Presenter는 시설 세대·후보·이전·촉매 선택만 조정하도록 경계를 맞췄다.
- 진화 효과 ID/촉매/상태의 표시명과 동적 GameObject 생성은 도메인 변경과 무관한 표현 책임이었다. 별도 Presentation/View로 옮긴 뒤에도 기존 시설·장비 진화 회귀가 동일하게 통과했다.
- 시작 파티 상세 탭의 선택 상태와 특성 툴팁 GameObject 수명은 준비 Aggregate가 아니라 화면 세션에 속한다. `StartPartyMemberDetailRenderer`가 이를 소유하고 Controller에는 사장 선택, 준비 시작/취소, 리롤/교체 명령, 런 시작 조정만 남겼다.
- 시작 준비 UI의 표시 규칙과 GameObject 생성 규칙을 별도 객체로 옮기자 Controller 의존성은 그대로 명시적으로 유지하면서도 표현 변경이 팀 구성 흐름을 다시 비대화하지 않게 됐다.
- 장비 진화의 방향 추론, 촉매 계열 배율, 재료 요구량 조립, 귀속 역사 노드 생성은 런타임 주문 목록을 소유하지 않는 결정적 규칙이다. `EquipmentEvolutionRules`로 이동해 주문 상태 전이/물리 재료 소비와 순수 계산 경계를 분리했다.
- `EquipmentEvolutionSaveData`, 런타임 인터페이스, 촉매 ID 파서는 가변 주문 구현과 독립된 계약이다. 별도 계약 소스로 이동하되 기존 `EquipmentEvolutionRuntime.GetCatalystFamilyPotencyScale` 호출자는 깨지지 않도록 전달 API를 남겼다.
- `AbilityMove`의 유휴 배회 후보 탐색과 경로 지원 형태 판정은 코루틴 상태와 독립된 질의다. 이동 중 재검증은 `AbilityMoveTraversalGuard`, 시각 방향/속도는 `CharacterMovementKinematics`, 막힘 후 AI 반응은 `GridMoveBlockedResponder`가 맡아 이동 요청 권위는 원본에 남겼다.
- Unity MCP 동적 명령 및 Console 0건만으로는 프로젝트 DLL 갱신을 증명할 수 없다. 이번에는 소스가 01:50 이후인데 DLL이 01:24/01:26에 머문 상태로 오래된 회귀가 실행됐다. 이후에는 명시적 `AssetDatabase.ImportAsset(...ImportRecursive...)`, 컴파일 완료, `Library/ScriptAssemblies` 시각 갱신을 모두 확인해야 한다.
- 긴 소스를 셸 출력에서 동적으로 추출할 때 도구 출력 축약 문자열이 실제 결과에 섞일 수 있다. `…`, `tokens truncated` 전수 검색과 실제 Unity 컴파일을 구조 추출 직후 필수 단계로 둔다.
- Grid placement 운영 경로는 VContainer 생성 콜백이 `BuildableObject.ConstructPersistentIdentity`를 호출하지만, Grid 픽스처의 콜백은 도메인 의존성만 구성하고 ID를 누락했다. 테스트 콜백도 운영 생성 계약과 동일하게 ID를 발급하도록 수정했다.
- 작성된 해부학 SO 12개는 이미 루트 도메인 카탈로그에 등록돼 있었다. 회귀 실패의 원인은 전투 fixture가 `Array.Empty<AnatomyProfileSO>()`로 빈 카탈로그를 명시 생성한 것이었다.
- 창고 fixture에 `IStockQuery`만 주입하면 초기화는 되지만 물리 저장소가 비어 있으므로 보충 작업이 선택되지 않는 것이 정상이다. 우선순위 회귀는 Editor 전용 물리 재고 질의를 통해 식량을 시드해야 하며 집계형 `Deposit` 경로를 되살려서는 안 된다.
- 거시 목표 실행은 AI 분기 순서와 다른 응용 책임이다. 목표 소비, 시설 회피, 불만·퇴장·파손 부작용, JobGiver 커밋을 `CharacterAiMacroDecisionRunner`가 소유하고 파이프라인은 분기 오케스트레이션만 유지한다.
- 컨베이어 필터는 경로 탐색 자체가 아니라 화물이 특정 노드에 입장할 수 있는지 판정하는 정책이다. 품목/카테고리뿐 아니라 금지품, 장비 재질·품질, 음식 신선도·오염을 한 정책 소유자가 판정해야 라우팅과 실제 이동이 같은 결과를 사용한다.
- 컨베이어 네트워크 상태는 저장 권위가 아니라 런타임 상태에서 재구축되는 Query 투영이다. 교착·무전력·의도 정지와 대표 막힘 원인은 별도 `ConveyorSnapshotProjector`에서 계산하도록 분리했다.
- 컨베이어 저장 변환은 노드/화물 Aggregate를 직접 운행하지 않는 순수 경계다. 복원 결과를 먼저 완성한 뒤 런타임 사전에 교체하게 해 부분 파싱 상태가 런타임에 노출되지 않는다.
- 작업 대상 적격성 평가는 후보 스캔 캐시와 별개인 정책 경계다. `WorkTargetEvaluator`가 작업 가능성·보충 공급·포로 노동·환경 판정을 담당하고, 선택기는 캐시와 점수 비교만 소유한다.
- Editor 회귀가 `FindObjectsByType<CharacterActor>`로 씬 전체를 훑으면 테스트가 만들지 않은 미주입 캐릭터 상태에 오염된다. fixture가 만든 명시적 참가자만 검증해야 작성된 SO 의존성 계약을 정확히 시험한다.
- 영속 ID 폴백 제거 후 공사 fixture도 운영 생성 경로처럼 배치 전에 `BuildingInstanceId`를 가져야 한다. 이름 기반 키를 되살리는 대신 고유 테스트 ID를 발급하니 작업 주문 생성·취소·고아 복구가 모두 동일 계약으로 통과한다.
- 신체 건강 런타임의 해부학 스냅샷·행동축·구형 표면 변환은 상태 소유권이 아니라 결정적 투영/정규화 규칙이다. 이를 별도 객체로 옮기면 가변 상태 사전과 생명주기 이벤트는 런타임에 남기면서 전투·수술이 같은 계산 결과를 계속 공유한다.
- LINQ의 `Select(ClonePart)`처럼 메서드 그룹으로 전달된 호출은 괄호 기반 호출부 검색에서 누락된다. 책임 이동 후에는 일반 호출뿐 아니라 메서드 그룹 식별자도 컴파일로 검증해야 한다.
- 한 소스에 두 개의 독립 MonoBehaviour가 함께 있으면 파일 줄 수뿐 아니라 탐색·생성 책임도 섞인다. 침공 감독자와 개별 침입자를 먼저 파일 단위로 분리하니 실제 초과 책임이 침입자 전술 루프라는 점이 명확해졌다.
- 침입자 경로 선택은 이동 코루틴의 상태 변경이 아니라 위험 인지도·작전 패턴·경로 고정 시간을 입력으로 받는 결정적 계획이다. 경로와 함께 인지도 버전/고정 만료를 결과 객체로 반환해야 계획 계산이 런타임 필드를 직접 소유하지 않는다.
- 생존 식량 런타임의 저장 복제·재고 인덱스·부패 컴포넌트 동기화·식사 원장은 하루 생존 상태 전이와 다른 변경 이유를 가진다. 각각을 별도 객체로 옮기면 `SurvivalFoodRuntime`에는 날씨/위험/시설 작업 조정만 남고 물리 아이템 권위도 명시된다.
- 물리 제작 회귀가 `EmptyResourceEconomyContentCatalog`로 실행되면 장비 재질 정책을 전혀 검증하지 못한다. 픽스처도 루트 `IGameContentCatalog`에서 장비·모듈·재질을 함께 구성하고 시설에 `BuildingInstanceId`를 발급해야 운영 경로와 같은 계약을 시험한다.

## 2026-08-02 shop/runtime authority findings

- 상점의 상품 재고는 물리 창고 재고와 동일한 개념이 아니다. 상품 진열/가격/재입고 주문 상태는 `ShopInventoryRuntime`이 소유하고, 시설 창고 수량은 계속 물리 아이템에서 파생되어야 한다.
- 특수 환경 시설 ID가 1500 이상에 존재하므로 에셋을 숫자 ID로 정렬한 위치와 카탈로그 코드 배열 위치를 비교하는 회귀는 잘못됐다. 코드 포함 여부를 ID/코드 키로 검증해야 한다.
- 창고는 용량만 설정된 채 비어 시작하는 것이 정상이다. `TotalStock == capacity`를 기대하던 회귀는 삭제된 집계 재고 시드 경로를 암묵적으로 요구하므로 `MaxCapacity == configured`와 `TotalStock == 0`을 별도로 검증해야 한다.
- `[RequireComponent]`가 테스트 GameObject에 컴포넌트를 추가해도 `CharacterActor`의 캐시된 `Identity`가 EditMode 생성 시점에 아직 연결되지 않을 수 있다. 영속 ID 픽스처는 `GetComponent<CharacterIdentity>()`로 실제 소유 컴포넌트에 직접 설정한 뒤 런타임을 사용해야 한다.
- 작성된 종족 SO 값과 Editor 빌더 입력이 다르면 어느 쪽을 고쳐도 다음 명시적 마이그레이션에서 되돌아간다. 최종 SO와 빌더 사양을 같은 변경에서 맞추되 회귀 실행 중 빌더를 자동 호출하지 않아야 한다.

## 2026-08-02 captivity authority findings

- 포로 정책 목록과 사용자 정책 시퀀스는 포로 상태 목록과 다른 변경 이유를 갖는다. `CaptivityPolicyRuntime`이 정책 복원·중복 검사·노역 재적용을 함께 소유해야 저장 시퀀스가 본체와 갈라지지 않는다.
- 공연 명성/특혜, 관리 상호작용 재료 예약, 호송 중 부모 Transform, 탈출 경로는 각각 다른 수명이다. 이를 별도 객체로 분리하면 포로 Aggregate의 영속 상태만 `CaptivityStateRuntime`에 남고 일시 호송 부모와 경로 실행은 저장되지 않는다.
- 감방을 `building.id + centerPos`로 식별하면 이동·동형 시설 배치에서 충돌한다. 포로 상태의 `housingBuildingId`도 다른 시설 참조와 동일하게 `BuildingInstanceId`만 저장해야 한다.
- Unity 응답 파일을 이용한 별도 Roslyn 컴파일은 MCP 장애 중 문법/타입 오류를 찾는 보조 수단으로 유효하지만, 새 소스를 명시적으로 추가해야 한다. 응답 파일은 마지막 Unity 빌드 당시 파일 목록만 포함하므로 최종 Unity 컴파일 증거를 대체하지 않는다.

## 2026-08-02 battle/grid ownership findings

- `OffenseBattleModel.cs`의 큰 크기는 하나의 세션 알고리즘만의 문제가 아니라 전투원 DTO, 저장 DTO, 세션, 조우 카탈로그 16개 타입이 한 파일에 있던 결과였다. 세션만 가변 전투 상태를 소유하고 나머지는 계약·순수 규칙·콘텐츠 조회로 분리할 수 있다.
- Grid의 수직 포털 목록과 최소 수직 이동 비용은 저장 상태가 아니라 현재 traversal link에서 완전히 재구축 가능한 휴리스틱 인덱스다. 별도 객체가 소유하면 Grid Aggregate는 셀/점유/경로 명령을 유지하면서 파생 캐시 저장 권위를 갖지 않는다.
- 인터페이스의 `new`는 실제 상속 멤버를 숨기는 위치에만 있어야 한다. 기반 gateway에 붙은 `new`와 파생 runtime에서 빠진 `new`가 동시에 12개 경고를 만들었고, 상속 방향에 맞춰 교정하니 동일 Roslyn 설정에서 Warning 0이 됐다.
- `OffenseExpeditionSystem.cs`는 원정 Aggregate와 689줄 UI MonoBehaviour를 함께 담고 있었다. 패널은 선택 멤버·버튼 GameObject·렌더링 수명만 소유하므로 별도 파일로 이동해도 원정 상태 권위와 공유할 가변 필드가 없다.
- 원정 런타임의 실제 결합점은 UI가 아니라 `이동 이벤트 → 전투 → 귀환 애니메이션 → 보상/캠페인 확정` 체인이었다. 이를 Target/Travel/Battle/Return/Finalizer 서비스로 나누자 Aggregate는 활성 원정 목록과 상태 전이만 유지하면서 1,200줄 제한 안으로 들어왔다.
- 생산 주문은 하나의 클래스 안에서 명령 처리뿐 아니라 출력 예약, 설비 유틸리티, 입력 선행 운반, 센서 설치 상태, 저장 매핑, UI 상태 투영까지 소유하고 있었다. 특히 출력 목적지와 센서 키가 `시설 숫자 ID + 좌표` 폴백을 공유해 시설 이동/복원에 취약했으며, 두 경로 모두 필수 `BuildingInstanceId`로 교체했다.

## 2026-08-02 equipment aggregate ownership findings

- 장비 런타임이 물리 아이템 저장소를 사용하더라도 생성자 안에서 통계 투영기·장착 저장소·부품·계보 구현을 직접 만들면 생성 경로별 규칙 권위가 다시 갈라진다. 이 객체들은 Composition Root가 동일한 싱글턴 그래프로 조립해야 한다.
- 장비 제작 큐와 캐릭터 장착 프로필은 장비 payload 자체와 수명이 다르다. 제작 Aggregate는 주문/재료 정책을, 캐릭터 장착 Aggregate는 장비 인스턴스 ID 참조만 소유하고, 내구도·품질·부품·계보는 계속 `IItemInstanceRepository` 한 곳에 남겨야 한다.
- 장비 SO의 구형 `CombatEquipmentCraftMaterial(StockCategory)` 필드는 작성 에셋에서는 이미 비어 있었지만 런타임 변환 코드가 살아 있어 추상 재료를 재도입할 수 있었다. 구형 필드가 채워진 콘텐츠는 구체 입력으로 추측 변환하지 말고 검증에서 거부해야 한다.
- 수리 주문 복원에서 재질 정의를 찾지 못했을 때 일반 재고로 대체하면 손상된 저장이나 누락 콘텐츠가 정상 수리처럼 진행된다. 구체 재질 아이템 ID를 복구하지 못한 주문은 명시적 실패로 폐기해야 한다.

## 2026-08-02 physical item aggregate findings

- 창고 집계 수량을 읽어 누락된 물리 스택을 복원하는 코드는 읽기 캐시가 아니라 두 번째 쓰기 권위다. 집계와 실물이 어긋났을 때 실물을 합성하면 오류를 은폐하고 저장 후 아이템이 증식하므로, 배송 가능 수량은 물리 저장소에서만 계산해야 한다.
- 원자 복원은 JSON 파싱만 선행해서는 부족하다. 저장 상태에 포함된 창고 목적지 키, 고유 장비와 스택의 상호 참조, 부품 소유 관계까지 스테이징 단계에서 검증해야 `repository.Clear()` 이후 예외가 발생하지 않는다.
- 물리 아이템 Aggregate의 읽기, 변경, 창고 물류, 절도는 같은 저장소를 사용하지만 서로 다른 책임이다. 불변 facet으로 노출하면 호출자는 필요한 capability만 의존하고 본체 생성자도 8개 의존성 제한을 지킬 수 있다.

## 2026-08-02 V18 ratchet and identity findings

- `AIBrain`은 하나의 불가분 알고리즘이 아니라 액션 콘텐츠 구성, 후보 평가 캐시, 재개형 스케줄링, 경로 검색 상태, 중단 정책, 명령 상태, 디버그 문구가 합쳐진 구조였다.
- 분리된 협력 객체가 각자의 캐시와 continuation을 소유한다. `AIBrain`은 캐릭터 대상 명령 경계로 남으며 점수 계산·경로 검색 continuation을 중복 저장하지 않는다.
- 후보 평가는 계속 구조화된 `AIActionFailure`를 반환한다. 새 협력 객체의 진단 문구는 전체 String Table 이전 전까지 안정적인 영문 메시지로 정규화했다.
- 방어 교전의 원거리 위치 탐색, 원거리 이동/사격, 저장 해석, 경비 AI pause 복원, 교전 승패 처리는 서로 다른 상태 수명주기다. 이들을 한 tick owner에 둘 이유가 없었으며 각각의 전용 객체로 이동했다.
- 방어 저장 복원은 저장 DTO 해석과 월드 명령 콜백을 분리한다. 저장 해석기는 경비·침입자 참조와 전선 셀을 검증한 뒤에만 교전 객체를 등록한다.

- 전역 파일 줄 수 2,169 같은 숫자 상한은 이미 비대해진 파일을 정상으로 고정한다. 1,200/800 목표와 경로별 현재 최대치를 가진 하나의 기준선 문서를 검증기와 테스트가 함께 읽어야 예외가 줄어들 때 즉시 삭제할 수 있다.
- 정적 가변 필드 총량 상한도 같은 결함이 있다. 필드 이름까지 고정한 재구축 캐시·표현 자산·프로파일러 승인 목록으로 바꾸면 새로운 런 상태가 기존 총량 아래에서 숨어 들어오지 못한다.
- 서식지 마커의 Unity 인스턴스 ID와 산업시설의 숫자 ID+좌표는 저장 왕복 시 동일성을 보장하지 않는다. 새 개체는 중앙 발급기의 타입 ID를 받고, 기존 시설 참조는 `RequirePersistentInstanceId()` 실패를 전파해야 한다.
- 씬 전환 요청은 씬보다 오래 살지만 정적 필드일 필요는 없다. `DontDestroyOnLoad` mailbox가 요청 상태를 소유하고 정적 참조는 재확보 가능한 Unity 객체 캐시로만 남기는 편이 수명과 권위를 분리한다.

- 수술의 환경 대기·복구 요청은 임상 진행률과 다른 수명이다. 필수 환경 capability를 묶음에서 강제하면 누락 시 임의 위험 계산으로 내려가는 이중 규칙을 제거할 수 있다.
- 수술 환자 입실과 재료 운반은 동일한 시설 목적지 ID를 공유하지만 임상 결과 권위는 갖지 않는다. 전용 물류 객체가 사체·환자·재료 버퍼를 함께 조정해야 부분 준비 상태가 일관된다.
- 전략 원정 화면은 화면 조정, 준비/세력 명령, 전투 카드, 동적 뷰 생성, 읽기 상세가 서로 독립적인 변경 이유를 가진다. partial 소스 경계를 이 책임 경계와 맞추면 각 Presenter 파일이 800줄 아래로 유지된다.
- 야생동물의 사냥 전투와 생태 행동은 같은 개체 목록을 보지만 권위가 다르다. 목록은 본체가 소유하고 전용 런타임은 공유 참조를 통해 전투 또는 행동만 변경하므로 별도 개체 저장을 만들지 않는다.
- 사냥 예약 키에서 캐릭터 이름으로 내리는 폴백은 동명이인과 이름 변경에 취약하다. 예약 시작도 `CharacterPersistentIdentity.Require`를 사용해야 저장·복원 식별자 계약과 일치한다.
# 2026-08-02 Authority Audit Corrections

- The former water/filth/GridTexture fallback was content synthesis disguised as presentation convenience. Shared authored colorable Tiles and ordinary SpriteRenderer views remove that hidden SO authority without duplicating hundreds of Tile assets.
- Building behavior had only eight actual runtime shells across 343 definitions. Storing `System.Type` in every BuildingSO made those assets code-serialization dependent; a fixed archetype protocol plus existing ability modules represents the same variation without reflection-driven construction.
- `WarehouseInventory` no longer owns quantities despite its legacy name: capacity/category policy is serialized, while totals are calculated through `IStockQuery` over physical item records. The remaining V1 snapshot migration was still misleading and has been removed.
- The plan previously overstated restore atomicity. `DungeonSaveSectionRegistry` preflights all payloads and rolls back on commit failure, but it still mutates the live world before rollback. Detached Aggregate staging and a single world swap remain required.
- Domain asmdefs exist for Foundation and a small set of core model contracts, but most gameplay code still compiles in `Assembly-CSharp`; the dependency-graph phase is not complete.

## 2026-08-02 treasury aggregate findings

- `economy.treasury` serialized one section but formerly restored six independent live owners in sequence. A failure in a later owner could expose a partially restored ledger, wage, procurement, overclock, or defense state.
- The six owners now project onto one replaceable `TreasuryEconomyAggregateStateStore`. Section staging creates a detached root, normalizes every subtree, and captures a commit that performs only one reference replacement.
- Individual runtime `Restore` entry points remain available for focused tests, but they copy the current aggregate and replace it after their subtree is complete; they no longer clear a live collection before validation finishes.

## 2026-08-02 composition-wide aggregate root findings

- Per-domain replaceable stores are insufficient by themselves: sequentially replacing several correct domain roots can still expose a mixed world if a later commit fails. They must project through one composition-owned root whose live reference is published once.
- A shallow candidate root is safe only when every migrated restore replaces its complete slot rather than mutating the shared old slot. Operational mutation still targets the live slot outside restore; restore paths construct detached dictionaries/DTOs and call `Replace`.
- Rebuildable presentation caches may still be invalidated during a failed restore, but authoritative persisted state remains unchanged. Such caches are not stored and recompute from the published root.
- Unity-object-heavy owners such as crop plots and world-resource nodes still combine authoritative save state with scene bindings and pending-restore fields. They require a DTO state slot plus a post-publish scene-binding projection, rather than moving their current Unity-reference dictionaries wholesale into the root.

## 2026-08-02 survival projection findings

- The `survival.deprivation` section already serialized deprivation, water, and filth together, but only deprivation previously used the detached root. Water and filth mutated live collections and Unity scene projections during commit, so a later-section failure could leave terrain, tilemaps, and cleaning targets inconsistent with the rolled-back DTO state.
- Persisted world state and Unity presentation must not share the same restore operation. Water/filth now replace detached DTO slots first; terrain, tilemaps, and work targets are reconstructed only when the runtime observes the newly published slot reference.
- Character consumables had the same clear-then-fill defect across diet and substance dictionaries. Treating its delivery and item-availability maps as state-owned transient data allows the complete runtime slot to be replaced without leaking old-run cache entries.

## 2026-08-02 captivity projection findings

- A replaceable root cannot work when helper runtimes retain a constructor-time `List<T>` reference. Captivity actor access and policy evaluation now resolve their lists through `CaptivityAggregateStateStore`, so a published root swap is visible to every collaborator.
- Door subject registries, carried Transform parents, wildlife capture flags, and actor warps are scene projections rather than persisted authority. Running them inside section commit breaks atomicity even when the DTO dictionary itself is detached.
- Restore now normalizes captive and captured-wildlife DTOs in candidate slots. Projection owners compare state references and perform external cleanup/rebinding only after publication; a discarded candidate leaves the live scene untouched.
- The architecture validator correctly rejected aggregate code added inside already bounded runtimes. Extracting state codecs, query views, projection owners, and performance sampling math restored the 1,200-line invariant without adding baseline exceptions.

## 2026-08-02 authored taxonomy findings

- 욕구·재고·시설 카테고리는 enum 자체는 저장 프로토콜이지만 표시명, 정렬, 초기값, 기분 곡선, 납품량·단가, 상점 가격 가중치는 변경 가능한 콘텐츠다. 이를 같은 정적 클래스에 두면 프로토콜과 밸런스 권위가 섞인다.
- SO 레코드를 불변 런타임 정의로 투영하고 `CharacterStats`, 생산/상점 서비스, Presenter에 카탈로그를 주입하면 런타임 전역 등록·리셋 없이 동일 데이터를 공유할 수 있다.
- 저장의 안정 ID 변환은 authored 표시 데이터 조회와 분리해야 한다. V18에서는 알려진 enum 값만 명시적 ID로 변환하고 알 수 없는 숫자·enum 이름 폴백은 손상된 저장을 은폐하지 않고 실패시킨다.

## 2026-08-02 composition-cycle findings

- `RegisterEntryPoint<T>` exposes implemented interfaces automatically. Removing an explicit `.As<IExteriorZoneQuery>()` was insufficient while `IExteriorActivityRuntime` still inherited the query contract; the contracts themselves had to be separated.
- A query that refreshes persisted state is a command in disguise. `FacilityEvolutionModifierQuery` re-entered room, filth, and building-ability services while production work was already dispatching an ability, creating a closed construction graph. Modifier evaluation now consumes the last committed component snapshot only.
- Wildlife butchery and deprivation share an event, not an ownership boundary. Publishing a typed taboo incident preserves synchronous consequences while avoiding a carcass-service → deprivation → filth → building-handler → carcass-service cycle.
- Scene-authored characters can be injected before entry points initialize, but presentation construction still requires identity. Persistent ID assignment therefore belongs before runtime/presentation bridge activation, not in save capture or a later registry pass.
- A valid `MonoScript` asset can still be the wrong script reference for a serialized component after class extraction. The scene retained `Assembly-CSharp::InvasionDirectorRuntime` data but pointed at `InvasionIntruderSystem.cs`; runtime missing-script checks exposed the stale GUID even though ordinary component-removal tooling could not repair it.
- Numeric `DataScriptableObject.id` collisions and stable string recipe-ID collisions are separate invariants. Both must fail editor validation, and the boot catalog must throw rather than keep whichever asset happened to load first.

## 2026-08-02 restore publication and session ownership findings

- Replacing an Aggregate slot is not sufficient when the same restore callback also updates a user setting, subscribes to Unity events, rebuilds markers, registers strategic sites, or warps actors. Those operations are projections and must observe publication, not candidate preparation.
- A staging-time `dirty` boolean is itself live mutation. Comparing the last projected Aggregate object or the shared root's published revision provides a stricter boundary: failed restores neither publish a new reference nor advance the revision.
- Physical hauling settings were incorrectly restored through `IDungeonUserSettingsService`, coupling a run save to persistent player preferences. They now occupy a replaceable runtime-state slot beside physical item state.
- `GameData` was already settings-only, but `GameManager` still created and owned the mutable `GameSessionState`. A scoped store now owns that state; the scene component forwards lifecycle and input only.
- The remaining hard atomicity boundary is the modular facility and character world reconstruction. It still clears and rebuilds live Unity objects during commit, so the rollback image cannot be removed until that work is prepared in a detached world representation and swapped after all sections succeed.

## 2026-08-02 detached facility-world findings

- `IGridSystemProvider` previously exposed only `GridSystemManager.grid`; there was no candidate or publication boundary. A narrow publisher now performs the checked Grid reference swap, while presentation notification is delayed until restored facilities are registered.
- Facility definition lookup, footprint collision checks, component injection, persistent-ID restore, and state-module decoding are all fallible. Performing them on inactive objects registered to an occupant-free layout copy prevents these failures from clearing the live facility world.
- Inactive candidates must also suppress external ownership. `BuildableObject` now withholds world-registry and paid-contract registration until publication, and contract removal happens synchronously when a live facility is destroyed so a delayed Unity `OnDestroy` cannot delete the replacement facility's contract by the same persistent ID.
- DTO aggregate publication alone cannot atomically publish Unity objects. Facilities and characters now participate in the same final publication boundary: facility staging exposes its detached Grid to character restore, while stable participant IDs publish quiescence, facilities, and characters in `050 -> 100 -> 200` order.
- A disabled character prefab is not automatically detached: dependency injection previously registered it in the lifetime registry and `CharacterLifecycle` subscribed to the live Grid manager even before activation. Candidate mode therefore has to begin before injection and propagate through runtime, presentation, carry, and lifecycle bridges.
- Reusing existing staff made character restore impossible to stage, because identity, abilities, health, inventory, progression, social memory, and transforms were overwritten one actor at a time. Creating complete inactive replacements makes those fallible mutations discardable and leaves old staff untouched until publication.
- The world save section must not quiesce live actors during its commit. That side effect is now a dedicated transaction participant, so a failed candidate build discards inactive objects without cancelling live work or movement.
- A detached Grid alone is not a sufficient restore view. Downstream sections also resolve facilities, warehouses, retail facilities, and characters while committing, so `RestoreWorldCandidateIndex` now redirects the ordinary world query interfaces without publishing candidates into live scene registries.
- Random streams require stable handles because most gameplay runtimes cache `IRandomStream` once in their constructors. Moving only the provider dictionary into the Aggregate root would leave those consumers attached to obsolete stream objects; provider-backed handles now resolve and advance the active root state on every call.
- A shallow candidate root is safe for `Replace`-style restore code but not for ordinary mutation: untouched slots initially point at the same object as the live root. `GetOrCreateWritable` now clones each slot on its first candidate-stage write and records candidate ownership for later writes.
- `RunVariableRuntime` formerly kept run seed, current day, replay maxima, and variable state on a scene MonoBehaviour. These values now occupy one root slot; restore builds the complete replacement before replaying the candidate random stream.
- Meta progression has two lifetimes: the external profile is intentionally merged, while run progress and the latest run result are exact save state. The profile slot now merges through copy-on-write, and the per-run tracker/lifecycle use complete replaceable slots so a rejected run cannot contaminate either lifetime.

## 2026-08-02 research restore authority audit

- The Unity Editor process and its restarted named-pipe bridge are alive, but the current Codex MCP client remains attached to the disposed pre-reload transport and returns `Transport closed`; Unity-native proof is deferred without using operating-system input.
- `BlueprintResearchSaveSection` is the next unclosed restore authority: its staged commit still clears and incrementally repopulates the live `BlueprintResearchState`, refreshes the queue, and restores knowledge-processing state. A later section failure can therefore leak research mutations unless research state is rebuilt off-world and published through the Aggregate root.
- `BlueprintResearchState` and `ResearchProjectRuntimeState` currently own readonly mutable collections, while `BlueprintResearchRuntime` owns one readonly state instance. That shape prevents root publication and forces in-place restore; the state needs an explicit deep-clone/build contract and runtime access through the active Aggregate slot.
- `KnowledgeResidueProcessingRuntime` has the same issue in a separate list plus sequence/transient fields. Its restore clears the live task list before validation, so it must either join the research Aggregate or receive its own replaceable root before the research save section is truly detached.
- Research gameplay mutations are not isolated to restore: queue ordering, progress, unlocks, and knowledge task delivery mutate the same objects during normal play. Root ownership therefore needs copy-on-write accessors for ordinary commands, not merely a `ReplaceState` used by loading.
- `RefreshProjectQueueAfterRestore` currently combines authoritative queue normalization (`TryResolveActiveProject`) with workforce/event notification. Queue normalization belongs in the detached decoded state; notification must observe a successfully published state reference so a failed restore cannot replan live workers.
- `DungeonRuntimeAggregateRootStore` deliberately shallow-copies untouched slots and requires `GetOrCreateWritable(factory, clone)` before any ordinary mutation during staging. Research facades must therefore return candidate-owned deep-cloned task/project objects before exposing mutable references such as `BlueprintResearchTask` or `ResearchProjectProgressState`.
- Existing root-backed runtimes reconcile external Unity/service projections by comparing `PublishedRestoreRevision`. Research queue/workforce notification should follow that pattern; a save-section commit must only replace root data.
- `TryResolveActiveProject` is itself authoritative normalization because it rewrites suspension reasons and the active project. It runs every second already, so the save section does not need to invoke it during commit; the first post-publication update can normalize the published queue and then emit one availability notification.
- The public research state leaks mutable task/progress objects. A root-aware `BlueprintResearchState` facade can preserve the existing API while routing all potentially mutable access (`Projects`, active task resolution, command methods) through deep copy-on-write Aggregate data.
- Editor scenarios frequently construct `BlueprintResearchRuntime` manually, and some deliberately use an uninjected component only as a standalone state container. The migration must preserve a local-state constructor path for tests while production `Construct` replaces the facade with one backed by the scoped Aggregate root.
- `KnowledgeResidueProcessingRuntime` is created only through composition in production, so adding the shared root store keeps its constructor at eight dependencies and can make its task/sequence state transactional without a second service locator.
- The first Foundation/runtime/Editor auxiliary compilation after the research and knowledge Aggregate cutover passes with Error 0 / Warning 0, including all known manual `BlueprintResearchRuntime.Construct` fixtures.
- Existing research scenarios verify direct V5 round-trip and V3 rejection but do not inject a later-section failure through `DungeonSaveSectionRegistry`; a new scenario must prove the published live research root remains unchanged when the candidate is discarded.
- Research now has a focused candidate-discard scenario: it stages different progress/queue data through the real V5 section, observes the candidate, discards the root, and requires the original live state plus publication revision to remain unchanged.
- The registry still captures and reapplies a rollback image after any commit failure. Aggregate-backed research no longer needs that repair path, but global rollback removal remains blocked on the remaining non-root Unity/runtime owners and participant publication failure semantics.
- The public-registry failure scenario compiles after replacing internal root calls with a one-shot late failure plus a discard observer. It proves the observer sees the original 7-work live state immediately after candidate discard, before rollback publication.
- Focused source review finds zero remaining research-save calls to live `ClearForRestore`, restore-time queue refresh, or legacy blueprint item materialization, and zero knowledge-task list fields outside the Aggregate state.

## 2026-08-02 remaining restore-owner audit

- A fresh scan of save-section commit paths identifies `CodexSaveSection` as the clearest next live-mutation owner: it calls `runtime.State.ClearForRestore()` and then recreates entries/lines one at a time during commit.
- Other candidates requiring follow-up include regular customers, facility shop, species, defense, exterior/waste/resource policies, and infrastructure runtimes. Their `Restore` methods must be inspected individually because several already replace root-backed state despite the generic method name.
- Codex has the same mutable-object leak pattern research had: a readonly `CodexState` owns a dictionary of mutable `CodexEntryRecord` objects, and `GetOrCreate` returns those records directly. A root-aware facade must deep-clone both the dictionary and every entry/line set before returning writable records during staging.
- `HasMemoryResidueClueAvailable` currently calls `GetOrCreate`, so a nominal query creates a discovered blank codex entry. The Aggregate migration should make this a pure snapshot lookup while preserving clue availability behavior.
- `CodexSaveSection` now preflights missing IDs, invalid enum values, and duplicate category/ID keys, builds a standalone state, then replaces the root slot. The only manual runtime construction site has been updated; no `ClearForRestore` reference remains in Codex or the full-save scenario.
- `RegularCustomerSaveSection` already constructs detached `RegularCustomerRecord` objects, but its final `runtime.State.Restore(records)` still targets a non-root state owner; there are no Aggregate-root references anywhere in the recruitment domain.
- Regular-customer state contained a second `recruitedCharacters` list that could diverge from each record's `IsRecruited` flag. The list is now eliminated; the public result view is deterministically derived from the authoritative record dictionary.
- Mutable records retain an `ActiveActor` runtime link that is not save data but must survive copy-on-write during normal gameplay. Deep cloning preserves that link while save decoding still constructs records with no actor and lets world activation reconnect them later.
- The production recruitment runtime remains at the eight-dependency boundary by combining activation and population into a typed character-lifecycle capability bundle before adding Aggregate-root ownership.
- Facility-shop saving currently duplicates research authority by capturing `research.State.UnlockedBuildingIds` into `DungeonFacilityShopSaveData` and restoring those IDs back into research after the research section. That field and dependency should be removed; research unlocks belong only to the research Aggregate.
- `DailyFacilityShopRuntime.RestoreState` clears unlock sets and calls `Refresh`, which emits `Refreshed` and runs auto-procurement during save commit. Offer lists are deterministic projections of day/catalog/run variables, so restore should replace day/unlock data and rebuild offers only after publication, without purchasing or alerts.
- Facility-shop offer day, basic-purchase unlocks, and acquired-blueprint IDs are one runtime aggregate. Giving the runtime a second direct root accessor would split the uninjected editor path from the unlock façade, so both date and unlock access now route through the same local/root-aware façade.
- Research-unlocked building IDs were duplicated in `DungeonFacilityShopSaveData` and written back after the research section. Removing that field, the research dependency, and the research runtime reference leaves `BlueprintResearchAggregateState` as the sole research-unlock authority.
- Daily offers are deterministic projections, not save authority. Candidate commit now replaces only the facility-shop aggregate; post-publication observation rebuilds offers without auto-procurement or alerts, while ordinary day refresh still performs those gameplay side effects.
- The facility-shop payload now validates offer day plus every saved building/blueprint ID against the authored catalog before commit. Duplicate, negative, or missing IDs fail preflight instead of being filtered into plausible state.
- A fresh post-facility scan found the four industrial infrastructure runtimes are the next genuine live owners: electrical and fluid clear node dictionaries, conveyor clears node/payload dictionaries, and automation clears both facility state and its power-demand registry during staged commit.
- Service-room code appeared suspicious by method name but already uses a `ServiceSessionAggregateState`; generic `Restore(...)` searches must be verified against ownership rather than treated as proof of live mutation.
- Industrial network summaries, topology versions, route caches, snapshot lists, and automation demand are projections. Persisted node/payload/facility values should swap through root slots, while topology/snapshot/demand rebuilding must observe the published revision rather than execute during candidate commit.
- `AutomationPowerDemandRegistry` did not need its own saved dictionary. Reading `AutomationAggregateState.Facilities[facilityId].Mode` directly makes power demand consistent with the candidate/live root and removes publication-order sensitivity between the automation and electrical tickables.
- All four industrial runtimes can preserve their existing interfaces while routing dictionary access through `GetOrCreateWritable`. During restore staging the first access deep-clones the shallow candidate slot; restore then replaces the complete slot and leaves topology, route, snapshot, warning timer, and payload-count caches untouched until publication.
- Industrial payloads previously normalized invalid values and overwrote duplicate IDs during restore. The new preflight rejects payload-version mismatch, blank/duplicate IDs, invalid enums, non-finite values, out-of-range percentages/freshness, and malformed conveyor payload stacks before any Aggregate replacement.
- `WorkOrderRuntime` is a remaining Unity-object-bound authority, not a simple dictionary conversion: restore destroys live `ConstructionSite` GameObjects, clears order/site maps, rebuilds orders, and creates construction sites while decoding. It needs a detached order DTO slot plus a transaction participant that prepares site objects against the candidate Grid and publishes them only after all sections commit.

## 2026-08-02 event-alert authority audit

- `EventAlertRuntime` still owned saved history, dismissal IDs, and its next numeric ID directly in MonoBehaviour collections. `RestoreHistory` destroyed/recreated Unity UI while the save registry's candidate root was active, so a later-section failure could visibly mutate the live world before publication.
- `EventAlertAggregateState` now owns those three persistent concerns and deep-clones mutable records, including runtime-only choice callbacks during copy-on-write. Selection, buttons, and detail visibility remain transient presentation state.
- The event-alert save section and direct save service now share `EventAlertSaveValidation`; invalid/null/duplicate IDs, unknown importance values, invalid counts/text, and more than three choices are rejected instead of filtered or normalized away.
- A public `DungeonSaveSectionRegistry` regression stages a dismissed candidate alert, injects one late failure, and observes the target immediately after candidate discard. It requires only the original live alert and zero presenter create/destroy calls before rollback publication.
- The source contract validator now requires the Aggregate slot, published-root revision observation, detached replacement, and the generic preflighted JSON save boundary.

## 2026-08-02 operating-day settlement authority audit

- The settlement persistence DTO was already an immutable snapshot, but the runtime restored it by `ResetLedger`, repopulating seven live collections, clearing report history, and rewriting debt/scalar fields during staged commit.
- `OperatingDaySettlementAggregateState` now owns all ledger dictionaries/lists, current counters, debt state, and report history. `LatestReport` is derived from history instead of being a second writable reference.
- `OperatingDaySettlementSaveValidation` rejects invalid root and nested report data before conversion: missing lists, duplicate keys/categories/days, negative amounts, non-finite mood values, invalid enums, malformed warehouse/supply/offer records, and history beyond 20 reports.
- Direct save-service restoration invokes the same validation as `OperatingDaySettlementSaveSection`; the section now uses the common `DungeonJsonSaveSection<T>` staged/preflight boundary.
- The public-registry regression replaces a candidate day/revenue/visit ledger, injects a one-shot later failure, and requires the observer to see the original live Aggregate immediately after discard.
- The settlement's existing eleven service dependencies remain a separate Phase 89/90 decomposition item. This change adds the Aggregate root explicitly and does not introduce another state owner or late-bind path.
## Work-order / construction-site restore authority audit (Phase 88 continuation)

- `WorkOrderRuntime.Restore` currently destroys every live `ConstructionSite`, clears both order dictionaries, and bumps the live candidate version before it has validated the complete snapshot.
- The same restore loop converts each DTO into mutable runtime state and immediately creates a Unity `GameObject`/`ConstructionSite`; a later duplicate, missing building definition, invalid grid position, or site creation failure therefore leaves a partially replaced live world.
- `ordersById` plus `nextOrderSequence` are the persisted work-order authority. `orderIdBySite` is a Unity-object projection and must be rebuilt only from a successfully published restore candidate.
- `WorkOrdersSaveSection.StageRestore` currently only deserializes JSON and defers all semantic validation and world mutation to `Commit`; it needs a shared strict validator and detached aggregate candidate before any live publication.
- The construction-site projection needs transaction-participant semantics: prepare against the restore candidate grid, publish only after aggregate publication, and destroy candidate objects on discard without touching the live sites.

## 2026-08-02 work-order detached Aggregate result

- `WorkOrderAggregateState` is now the sole owner of order records, next-ID sequence, and candidate version. `orderIdBySite` remains only a rebuildable Unity-object projection.
- `WorkOrderSaveValidation` rejects null/version-mismatched payloads, invalid or duplicate canonical IDs, sequence reuse, unknown work/building/item definitions, non-finite progress, terminal statuses, malformed material lists, abstract `stock-item:*` item inputs, duplicate construction targets, and mismatched construction destinations.
- `WorkOrderRuntime.Restore` no longer clears live orders or sites. It builds a complete replacement Aggregate and inactive `ConstructionSite` candidates on the shared facility candidate Grid; any footprint/build/injection/registration failure discards those candidates.
- The runtime is restore participant `150.world.construction-sites`, so publication follows facilities (`100`) and precedes characters (`200`). Failure discard leaves live site mappings untouched; successful publication removes old sites and makes the prepared sites visible.
- Work-order persistence now uses `DungeonJsonSaveSection<DungeonWorkOrderSaveData>` and explicitly depends on both the facility-world and physical-item sections.
- The work-order runtime source was kept within the 1,200-line boundary by separating save contracts, strict validation, and Aggregate state into focused source owners; `WorkAmountSystem.cs` is 1,185 lines after the cutover.
- Auxiliary Foundation, runtime, and Editor Roslyn builds pass. Unity MCP still returns `Transport closed`, so Unity-native menu execution and Console/capture proof remain pending.

## 2026-08-03 work-order rollback-free verification result

- `WorkOrdersSaveSection` now declares rollback-free staging, and capture persists active work as canonical resumable `Ready` state with no worker reservation. Validation requires canonical ascending `work:{sequence:D6}` IDs, exact work/material/destination state, no terminal or transient status, and authored references.
- construction sites are fully prepared inactive against the candidate Grid before live retirement. Successful publication uses synchronous world-replacement retirement; failed/partial candidates use synchronous discard and cannot leave detached GameObjects.
- Unity actual execution passed normal publication, invalid preflight preservation, one-commit late failure, root/candidate/live-site preservation, and the full WorkAmount suite. The later physical-item cutover did not regress this suite.

## 2026-08-02 wildlife restore authority audit

- `WildlifeRuntime.Restore` currently calls `ClearWildlife` before validating the full payload, destroys every live `WildlifeActor`, creates replacement `GameObject`s one at a time, and silently skips unknown species or blocked positions. A later failure therefore leaves a partial population.
- Wildlife persistence is split across four mutable owners: Actor MonoBehaviours, `WildlifeEcosystemRuntime` fields, `WildlifeCarcassService.freshnessByStackId`, and `WildlifeRuntime.foodRaidOrders`/sequence fields. All four are mutated during one save-section commit.
- `WildlifeSaveSection` still accepts and migrates V2 to V3 even though the V18 root explicitly rejects pre-V18 runs. The wildlife section should require its current version and fail invalid data rather than fabricate empty raid state.
- `WildlifeActor.Initialize` registers immediately with both the Grid and live world registry. Candidate construction needs an explicit detached flag: register against the candidate Grid, suppress live registry publication, then publish or discard in one transaction participant.
- Wildlife publication should sort after facilities, construction sites, and characters (`100 -> 150 -> 200 -> 250`) so restored animals see the final Grid/world before AI resumes.
- Food-raid entries that reference missing animals, duplicate wildlife IDs, unknown species, non-finite body/need values, malformed habitat patches/respawn records, or invalid carcass records must be rejected during shared preflight instead of normalized or skipped during commit.

## 2026-08-02 wildlife detached restore result

- `WildlifePopulationState` now owns the live actor list, behavior schedule, raid orders, sequence, initial-spawn flag, and carcass tick schedule. `WildlifeRuntime` accesses these through one replaceable population reference and rebuilds its hunt/behavior collaborators after publication.
- `WildlifeSaveValidation` rejects invalid current-version payloads, canonical-ID/sequence errors, unknown species, invalid health/need/enums, malformed carcass and ecosystem records, duplicate active raid ownership, and invalid typed habitat IDs. Terminal raid history may outlive its actor; nonterminal raid state may not.
- Restore participant `250.world.wildlife` builds every Actor as an inactive detached candidate on `RestoreWorldCandidateIndex`'s facility Grid. Candidate actors register only on that Grid; live world-registry publication is suppressed until the final participant boundary.
- Candidate commit does not clear or mutate the live population, ecosystem, carcass freshness, or raid-order state. Failure discards only candidate actors; publication replaces those projections after facilities (`100`), sites (`150`), and characters (`200`) are already published.
- `WildlifeSaveSection` now uses `DungeonJsonSaveSection<DungeonWildlifeSaveData>` and accepts only its current V3 section contract. The obsolete V2-to-V3 empty-raid migration was removed because the V18 root rejects old runs.
- World-reference validation runs after physical-item and facility candidates are staged. Carcass freshness must reference a matching physical carcass stack, while every saved habitat patch must cover a usable restored exterior cell.
- Normal habitat creation now emits typed `wildlife-habitat:*` IDs for authored, default, and water-source patches. The constructor no longer fabricates a GUID fallback, so a missing/legacy ID is an explicit contract failure.
- Carcass capture filters freshness records through the physical item authority, preventing deleted or mismatched stacks from becoming unloadable V18 saves.
- The main and restore partials total 1,198 lines, preserving the 1,200-line wildlife runtime boundary. Runtime and Editor auxiliary Roslyn compilation pass with Error 0 / Warning 0.
- PlayMode regressions now cover invalid preflight preserving actor identity, successful round-trip publishing replacement actors, and a one-shot later failure that discards the first candidate before rollback. Execution remains pending because Unity MCP still reports `Transport closed`.

## 2026-08-02 exterior-zone and return-arrival authority result

- `ExteriorZoneMarker` was previously captured by both the modular facility snapshot and `exterior.activities`, because it inherits `Facility`. Facility capture/clear now explicitly excludes it, leaving the exterior activity section as its only save owner.
- `ExteriorActivitySaveSection` now requires strict V3 preflight and delegates restore to participant `300.world.exterior-zones`. The coordinator creates inactive markers on the shared candidate Grid, restores typed building identity, indexes them for downstream queries, and does not replace live zone objects until publication.
- Exterior incident persistence no longer stores both summary incidents and detailed runtime incidents. Only detailed `incidentStates` remain; terminal history can outlive world references, while active incidents require restored characters, wildlife, and physical stacks.
- `OffenseReturnArrivalRuntime.Restore` previously cleared live queues and called `MaterializeReadyArrivals`, which could spawn prisoner or wildlife GameObjects during staged commit. Return queues, barriers, sequence, and retry time now live in `OffenseReturnArrivalAggregateState`.
- Return-arrival payloads now reject version/list errors, duplicate or noncanonical IDs, invalid enums/counts/risk, inconsistent escaped/materialized sets, and sequence reuse before commit. Restore swaps one detached Aggregate slot and schedules physical materialization for a later normal tick.
- Return-arrival dependencies are grouped into two explicit capability bundles, reducing the runtime constructor from fifteen direct dependencies to two.

## 2026-08-03 character-medical detached restore result

- The previous medical restore cleared live orders, treatment reservations, carry-parent state, and downed Grid occupants before validating all saved references. Invalid patients, rescuers, facilities, or occupied cells could therefore leave a partially replaced runtime.
- `CharacterMedicalAggregateState` is now the sole owner of medical orders and their canonical sequence. Capture and ordinary mutation use the active Aggregate root; restore creates a deep-cloned replacement state.
- `CharacterMedicalSaveValidation` rejects null/oversized lists, malformed or duplicate `medical:N` IDs, sequence reuse, invalid typed character/building IDs, invalid enums, non-finite work/treatment values, impossible carry/supply flags, duplicate active patients, and unknown authored treatment items.
- `CharacterMedicalRestoreCoordinator` validates candidate-world patients, rescuers, and treatment facilities, then registers downed occupants only on the detached facility Grid. Participant `350.world.medical` swaps the projection after facilities, sites, characters, wildlife, and exterior zones publish.
- Failed preparation removes only candidate Grid registrations. Successful publication removes old registrations using their recorded original Grid and position, preventing a world-swap lookup from detaching the wrong Grid.
- `CharacterMedicalSaveSection` now uses `DungeonJsonSaveSection<DungeonCharacterMedicalSaveData>` and the old warning-based restore call is gone. The composition root registers the medical runtime as a restore participant.
- Restore orchestration is a focused coordinator rather than a partial-class size escape; `CharacterMedicalRuntime.cs` is 1,199 lines and retains exactly eight required constructor dependencies.

## 2026-08-03 character combat-command detached restore result

- The old persistence helper released every live combat stance, unpaused actors, cleared commands, and then silently skipped missing actors or malformed reservations. A failure after that point could not preserve the live command projection.
- The old DTO omitted both `commandSequence` and `commandRevisions`, so loading an active run could reuse `combat-command:N` IDs and lower an actor's revision watermark.
- `CharacterCombatCommandAggregateState` now owns commands, stance membership, actor revision watermarks, and sequence. The V2 DTO captures all four and strict validation rejects malformed IDs, duplicates, terminal commands, invalid enums/timers, missing target contracts, revision mismatch, and sequence reuse.
- Candidate-world validation requires active restored stance actors, valid target cells, restored character/wildlife targets, and existing physical weapon instances. Restore only replaces the detached Aggregate root.
- Participant `400.world.combat-command-stances` applies AI pause and existing stance presentation after the detached character, wildlife, exterior, and medical participants have published. Failure discard never touches the live actor projection.
- `CharacterAiWorldRegistry.Wildlife` and its version now follow `RestoreWorldCandidateIndex`, closing a downstream query hole that otherwise exposed the retired live wildlife population during staged combat validation.
- `CharacterCombatCommandRuntime` remains exactly 1,200 lines while its constructor falls from fourteen direct dependencies to four parameters: combat capabilities, world capabilities, focused collaborators, and the Aggregate root.

## 2026-08-03 defense-tactical detached Aggregate result

- The former restore cleared `byActor`, fabricated missing reservation IDs, and silently dropped missing actors, duplicate cells, and invalid Grid positions. Its `sequence` was not saved, so IDs could be reused after load.
- `DefenseTacticalAggregateState` now owns reservations and sequence. Strict V2 validation rejects malformed/duplicate canonical IDs, actor/cell duplicates, invalid enums or scores, sequence reuse, unavailable candidate actors/targets, and blocked candidate cells.
- Restore performs no live clear and no fallback generation; it replaces one complete Aggregate slot only after structural and detached-world validation succeeds.
- The public PlayMode save path now includes an invalid-sequence regression that requires both the reservation view reference and serialized live state to remain unchanged.

## 2026-08-03 medical lifecycle and physical-supply findings

- `TryRequestTreatment` previously routed any injured actor through `NotifyCharacterDowned`, even when the body Aggregate reported the actor ambulatory. This created the observed `Completed -> Downed -> new medical:N` loop. Emergency rescue orders now require `CharacterBodyHealthSnapshot.Downed`.
- `CharacterMedicalRuntime.AdvanceTreatment` duplicated body-health recovery by calling `NotifyCharacterRecovered` after `ApplyTreatment` had already published the authoritative event. Removing that second writer and guarding both notification handlers against the body snapshot restores one lifecycle authority.
- The old verifier seeded only a `WarehouseInventory` category query fixture. It increased `Medicine` counts but created no `ItemInstanceId` or physical stack, so exact authored medicine delivery could never finish under the V18 item authority.
- Exact medicine already present in an order's facility buffer is now consumed before a haul request is created. This is valid for restored/prepositioned supplies and enabled the verifier to use real authored medicine SOs and physical stacks.
- Apparent rescue-command and transform failures were caused by autonomous owner AI reserving the same order and by facilities adjacent to the patient. Pausing all other rescuers and enforcing a minimum facility distance produced deterministic pointer-owned transport evidence.
- Live actor identity audit found no persistent-ID collision: `owner` and both `staff:*` identities were unique. The differing rescuer was a legitimate autonomous owner, not duplicate state authority.
- `CharacterCombatCommandRuntime` now completes rescue commands from `CharacterRecoveredEvent`; this is event-driven and does not depend on positive game-clock delta after the verifier pauses time.
- Runtime size validation is enforced after behavior changes. Medical supply and combat-command lifecycle responsibilities are separate partial sources, leaving every affected runtime file below 1,200 lines.

## 2026-08-03 rollback-boundary resumption audit

- `DungeonSaveSectionRegistry.RestoreAll` still captures `CaptureAll()` as a full rollback image before committing staged sections. A failed commit discards candidate roots/participants, then preflights, stages, commits, and publishes the captured live image again.
- Removing that image now would be unsafe: the Registry still acknowledges legacy sections whose runtime state is not a replaceable Aggregate or detached Unity-world candidate. The next work must identify and convert those owners, not delete the guard prematurely.
- The worktree is extensively dirty from the continuing V18 program. A normal `git diff --stat` invoked Git LFS clean filtering and failed because `.git/lfs/tmp` is read-only in the managed workspace; `git -c filter.lfs.process= -c filter.lfs.clean= -c filter.lfs.required=false diff --stat` is the safe read-only audit form here.
- The previous broad baseline output is too large for a single tool response. Subsequent audits must be domain-scoped and line-bounded so evidence is not lost to truncation.

## 2026-08-03 captivity/circus restore audit — initial evidence

- Captivity is only partially Aggregate-backed. `CaptivitySaveSection.StageRestore` still captures a DTO and calls `runtime.Restore` during commit; `CaptivityStateRuntime.Restore` clears/restores policy and captive collections while treating invalid or missing references as warnings.
- `CaptivityPolicyRuntime.Restore` clears the live policy list and skips invalid or duplicate policies. That is a live mutation plus permissive normalization, not detached candidate validation.
- Circus similarly builds a `CircusAggregateState`, but its save section invokes live `CircusRuntime.Restore`, and captured-wildlife restoration remains a separate mutable path with warning-based skipping and transient carry-parent clearing.
- These two sections are strong next candidates because their plain Aggregate types already exist, while their save boundaries still lack strict preflight, detached slot replacement, and candidate-world reference validation.

- The captivity save section has no semantic preflight: `JsonUtility.FromJson(...) ?? new CaptivitySaveData()` silently fabricates an empty payload and defers every rule to commit. `Restore` also duplicates this permissive path outside the staged boundary.
- `CaptivityStateRuntime.Restore` does replace `CaptivityAggregateState` before adding records, so it may already target the registry's candidate root; however it clamps negative sequences, skips duplicate/malformed captives, marks missing actors dead, and rewrites in-progress escort state instead of rejecting an invalid V18 snapshot. Strict validation is still required even if the root swap itself is detached.
- `CaptivityAggregateStateStore.Replace` delegates to `DungeonRuntimeAggregateRootStore`, confirming captivity DTO state is staged in the candidate Aggregate root rather than necessarily mutating the published root.
- The external door-access/carry projection is updated lazily by `CaptivityDoorAccessProjection.EnsureCurrent`; it removes previous door subjects, clears escort transient parents, then applies the published state. It is not itself a transaction participant, so its call sites and publication timing must be audited before declaring captivity rollback-independent.
- Captivity actor validation resolves through injected `ICharacterAiWorldRegistry.AllCharacters`, not through a cached actor list. Because the character world registry was previously made candidate-aware, this is the correct abstraction if its current implementation delegates to `RestoreWorldCandidateIndex`; that delegation must be verified directly.
- During staged restore, `doorAccessProjection.RestoreCompleted()` deliberately does nothing and normal tick later notices the published Aggregate reference. This avoids live door mutation before publication, but it leaves restoration consistency dependent on the next tick and does not provide an explicit participant ordering boundary.
- `CharacterAiWorldRegistry.AllCharacters` is candidate-aware: while restore candidates exist it returns `IRestoreWorldCandidateQuery.TryGetCharacters`, otherwise the live lifetime registry. Captivity can therefore validate character references against the detached character population without a new world lookup abstraction.
- Audit command note: two filename-pipeline searches for medical save sources returned exit code 1 because the relevant types are grouped in `CombatSaveSections.cs` and `CharacterMedicalRestoreRuntime.cs`, not files matching the assumed filename pattern. Symbol search (`rg -l "class CharacterMedical..."`) is the reliable lookup and located all three sources.
- The established medical pattern separates three responsibilities: strict DTO validation plus `CreateState`, runtime candidate-world validation/projection preparation, and an ordered `IDungeonRestoreTransactionParticipant` that publishes only after Aggregate-root publication.
- Captivity does not need a new inactive actor projection because characters already belong to the character-world participant. It does need the first two layers and a small participant for door/carry projection publication so success does not depend on a later tick.
- `CharacterMedicalSaveSection` demonstrates the intended concise boundary: inherit `DungeonJsonSaveSection<T>`, validate in `ValidatePayload`, and call a runtime restore that requires an active transaction. Captivity's hand-written `Restore`/`StageRestore` should be replaced with this form.
- Captivity V2 persists canonical sequences, policies, and a broad captive state including character/building/stack/item references, interaction progress, performer progression, and timed security state. Validation must cover all of these rather than only duplicate IDs.
- `CaptivityPolicyRuntime.Restore` currently masks corrupted V18 data by clamping the policy sequence, dropping malformed/duplicate policies, and fabricating built-in policies if the result is empty. It also may call `ApplyLabor`, which changes candidate character type/lifecycle while decoding state. The replacement builder should clone validated policies/captives directly and defer actor projection to the participant.
- Captive numeric invariants are explicit in the model: bounded 0–100 traits/health/pressure, nonnegative performer/injury/privilege/security counters, finite timed fields, defined status/milestone enums, and labor flags limited to `CaptiveLaborPermission.All`.
- `captureSequence` is only a monotonic event counter and is not encoded into `captiveId`; it therefore needs nonnegative validation but cannot be compared to captive IDs. `policySequence` can and should be compared to `captivity:custom:N` IDs.
- Active escort and interaction state has cross-reference contracts: carrier/warden must be candidate characters, housing must be a candidate captivity-capable building, restraint stack/item/quantity must agree with physical item state, and interaction IDs/destinations/work fields must form a coherent all-or-none set. Current restore rewrites escort state and ignores these contracts instead of validating them.
- `ICaptivityRuntime` still exposes the old warning-based `Restore` only; it needs `ValidateRestore(payload, report)` and report-based transaction restore like the medical runtime.
- `CaptivityRuntime` is registered as an entry point and multiple captivity interfaces, but not yet as `IDungeonRestoreTransactionParticipant`. Adding that service exposure is required for explicit post-root door/carry projection publication.
- Audit command note: PowerShell/Windows does not accept a wildcard embedded in the `rg` path argument (`CharacterMedicalRuntime*.cs` produced OS error 123). Use a directory path plus `-g 'CharacterMedicalRuntime*.cs'`.
- Medical runtime exposes the participant lifecycle as thin delegates to its restore coordinator. Captivity can use the same shape without growing its already size-constrained main runtime.
- `IWorldItemStackRuntime.GetAllStacks()` is available through the injected physical-item authority and can validate saved restraint stack/item/quantity against the detached physical-item candidate. `CaptivityInteractionRegistry.TryGet` can validate current interaction IDs against the authored runtime handler set.
- `WorldItemStackSnapshot` exposes the needed authority fields (`StackId`, `ItemId`, `Quantity`, `ReservedByPersistentId`, `DestinationId`). No captivity-specific editor test currently calls the warning-based restore API directly, so changing the public interface should have limited fixture fallout.
- Audit command note: a parallel search initially returned exit 1 because one assumed filename/type match was absent; broad parallel `rg` calls should be wrapped or issued independently when a no-match exit is acceptable.
- Typed `ItemDefinitionId` and `ItemStackId` value types already exist, and captivity housing capability is explicitly discoverable through `BuildingSO.GetCaptiveHousingAbility()`. Strict validation can avoid raw-string format guesses for these references.
- `CharacterId` currently validates any nonempty canonical string, while `BuildingInstanceId` requires `building:*` and `ItemStackId` requires `stack:*`. Captivity validation should use these actual contracts rather than impose a new character prefix.
- Valid captivity housing must have `BuildingCaptiveHousingAbility.IsValid` (`capacity > 0` and humanoid acceptance); a mere surviving building ID is insufficient for active confined/interaction states.
- `IDoorAccessSubjectRegistry` only exposes per-ID `SetCaptive`/`SetCapturedWildlife`; it has no replace-all/pointer-swap contract. A captivity participant cannot honestly promise a single non-failing door projection swap until this registry gains an Aggregate-backed subject set or replace operation.
- `DoorAccessService` owns mutable `HashSet<string>` memberships and every per-ID change calls `NotifyDoorPolicyChanged`, increments a version, clears path caches, and requests AI replans. Replaying a full captive list at publication is both non-atomic and unnecessarily noisy.
- The concrete `DoorAccessService` is already a singleton registered behind query/command/subject interfaces. A narrow `ReplaceCaptiveSubjects(IEnumerable<string>)` command on `IDoorAccessSubjectRegistry` can build a detached set first, swap membership in one method, and emit one policy-change notification.
- Door path-search caching already keys on `IDoorAccessQuery.DoorAccessVersion`. If door subject membership becomes an Aggregate-root slot, adding `DungeonRuntimeAggregateRootStore.PublishedRestoreRevision` to that version invalidates all cached routes immediately after one root publication without replaying per-captive notifications.
- Captivity restraint reservations can legitimately outlive their source world stack ID after the carrier picks the restraint into inventory; current code does not clear the saved stack fields on pickup/consume. Strict validation may require typed/coherent fields but cannot require the original world stack to still exist without first unifying carry inventory with physical items.
- In-flight escort parent transforms are transient. The existing restore intentionally resets `Escorting` to a safe non-carry state; a strict builder may retain that explicit canonicalization, but it must validate the source state first and clear all related transient reservation fields consistently.
- `CaptivityEscortRuntime.ClearTransientState()` is a non-failing dictionary clear; it is suitable for the participant's publication step. Door membership itself can now be staged in the new Aggregate-root door subject state, so the old lazy `CaptivityDoorAccessProjection` replay should be removed.
- Performer skill/fame are clamped to 0–100 and privilege tier is derived as 0–2. Captivity statuses used by runtime cover every enum value except `None`; strict save validation can reject `None` and out-of-range performer/milestone fields without rejecting authored flows.
- Fresh-source audit confirms the old `CaptivityDoorAccessProjection` and warning-based captivity restore signatures are gone. Remaining warning restores reported by the scoped scan belong to circus, invasion, and another combat section, which remain later conversion targets.
- The current runtime response file writes directly to Unity's Bee `Assembly-CSharp.dll`; newly added sources are not yet listed there, so the auxiliary runtime compile must append the four new `.cs` paths explicitly. Editor compilation must then reference the rebuilt runtime DLL without appending those partial/runtime sources again.
- Auxiliary runtime and Editor Roslyn compilation both pass after the captivity/door Aggregate edits. No existing captivity-specific invalid-preflight regression was found, so focused coverage must be added rather than inferred from other domains.
- The V18 validator still expects the removed `CaptivityDoorAccessProjection.EnsureCurrent` source contract. It must be ratcheted to require `DoorAccessSubjectAggregateState`, `ReplaceCaptiveSubjects`, strict `CaptivitySaveValidation`, the typed JSON section, and participant ID `450.world.captivity` instead.
- `CaptivityCircusDebugScenarios` is a 328-line pure contract suite and is the appropriate place for deterministic DTO validation/clone checks. A full live-world preflight-preservation check should be added to the existing PlayMode save verifier path separately.
- Editor code compiles in a separate assembly and cannot exercise internal runtime validators. The stable boundary is a public payload validator plus a public pure `CaptiveState -> CaptiveState` restore normalization function; the internal Aggregate builder remains hidden.
- `CombatV14PlayModeVerifier` already runs invalid medical, combat-command, defense-tactical, and equipment-maintenance payloads through the public `IDungeonGameSaveService` before tactical gameplay. Captivity should join this exact sequence and assert unchanged serialized captivity state, Aggregate published revision, and door-access version.
- The verifier can be started entirely through Unity MCP via `StartFromMenu()` and polled with `GetReport()`; it writes the authoritative report to `Artifacts/QA/combat-v14-playmode-report.txt` and uses Unity's virtual Input System/capture paths.
- The first live run exposed a real validator defect: authored built-in policies `captivity:forced-labor`, `captivity:performer`, and `captivity:corruption` are valid but were rejected because the new validator allowed only `captivity:standard` and `captivity:custom:N`. Every unrelated invalid-payload report therefore also contained captivity errors, meaning a normal save would have been unloadable. Built-in policy IDs must be explicitly recognized.
- `CAPTIVITY_PREFLIGHT_ATOMIC` itself passed with unchanged revision/door version/state. The overall run failed later at `POINTER_RELOAD` in a reused PlayMode/InputSystem session; this is separate from save preflight and will be rerun after a clean PlayMode restart.
- The clean rerun proves the built-in policy fix: unrelated medical/command/defense/maintenance preflight errors no longer contain captivity errors, and `CAPTIVITY_PREFLIGHT_ATOMIC=PASS` reports only the injected negative sequence.
- The clean run reached medical QA but failed rescue initiation because the verifier explicitly paused the rescuer, then used the combat-stance button before rescue without ensuring that stance activation left the actor eligible for `AbilityRescue`. This is verifier isolation/setup behavior, not a captivity restore mutation: Aggregate revision and door version stayed unchanged and all preflight checks passed.
- `TryIssueRescue` itself accepts a paused rescuer in combat stance; once a command exists, `TickRescue` briefly unpauses to start the ability and pauses again. The failure occurs before command creation, so the retry must target the UI selection/mode/right-click sequence rather than changing rescue runtime rules.
- Audit command note: a mixed `rg` command again used wildcard path arguments and returned Windows error 123 after useful partial output. Subsequent searches use directory roots with `-g` only.
- `OwnerCommandController` clears rescue input mode after one pointer attempt. The verifier did not confirm that its second single-selection click actually selected only the rescuer before activating stance/mode, and it made only one target attempt. A bounded three-attempt selection→stance→rescue-mode→right-click loop is the correct deterministic QA fix.
- `OwnerCommandController` exposes a public `SelectedActors` read-only view that prunes stale selections. The verifier can therefore assert a canonical one-actor selection before arming rescue mode instead of inferring selection from button state.
- The verifier source lives at `Assets/Scripts/Services/Combat/Editor/CombatV14PlayModeVerifier.cs`, not under `Assets/Tests/PlayMode`; future reads should locate it by symbol or exact tracked path.
- The bounded retry removed selection ambiguity but did not start rescue: the clean MCP run reports `attempts=3; selected=Sion; mode=None; stance=True`. Since rescue mode is one-shot and resets to `None`, the pointer command handler is receiving each click but declining/resolving the target before `TryIssueRescue` creates a command. Target raycast and patient commandability are now the narrowed fault boundary.
- `OwnerCommandController.TryIssuePriorityWorkCommand` reads a single `Physics2D.Raycast` hit at `IPlayerInputReader.MousePosition` and enters combat dispatch whenever the selected rescuer is in stance. Rescue mode resets only after a non-null downed `CharacterActor` reaches the rescue issue branch; the observed `mode=None` therefore strongly indicates the hit resolves to some collider, but the exact failure message/hit identity is not yet exposed by the verifier.
- Medical setup reuses the deterministic pointer layout, places rescuer and patient 4–8 clear horizontal cells apart, and keeps both actors paused. Geometry overlap between the two actors is therefore not the explanation.
- `TryIssueRescue` has only two rejection conditions after selection filtering: rescuer must be in combat stance and the raycast-resolved target instance must currently be `Downed`. It otherwise writes the command immediately. Because the final diagnostic samples after two frames, a third possibility remains: the pointer command may be created and then removed by `TickRescue` before the verifier observes it. The next check must capture issue/cancel behavior or sample on the first frame rather than assuming the command never existed.
- `TickRescue` does not normally remove a valid command: it resolves the patient, starts `AbilityRescue`, leaves the command in `Executing`, and only completes if the target vanished/recovered. With the patient still downed, an ephemeral successful command is unlikely. Capturing the controller's published `NoticeFeedEvent` will reveal the exact rejection text without adding production debug state.
- `IGameEventBus.Subscribe<TEvent>` is available from the existing runtime scope. The verifier currently resolves no event bus, so it can add a temporary `NoticeFeedEvent` subscription scoped to the rescue attempts and dispose it immediately afterward.
- `PublishCombatCommandResult` grades successful commands as `NoticeFeedEvent.Grade.NONE`, while only failures are warnings. Filtering the diagnostic subscription to non-`NONE` cannot distinguish a success that was immediately completed/cancelled; the last notice after each right click must be recorded regardless of grade.
- Continuing physical carry/treatment assertions after `POINTER_RESCUE_COMMAND` fails adds a 60-second wait and three derivative failures without new evidence. The verifier should yield-break immediately after the root pointer failure.
- The corrected notice capture is decisive: the controller publishes `1명 구조`, proving raycast resolution, downed target, selection, stance, and `TryIssueRescue` all succeed. The command is then removed before `RightClickActor` returns. The bug is in the command tick/participant lookup lifecycle, not pointer input.
- `CombatCommandParticipantQuery.FindCharacter` searches `ICharacterAiWorldRegistry.Characters`, while `CharacterAiWorldRegistry` separately exposes `AllCharacters` from its lifetime registry. The verifier discovers canonical active staff directly from the scene, so a scene actor missing from the active-AI registry can accept a UI command but be treated as nonexistent on the next combat tick. This split is the leading explanation for immediate cancellation.
- The active-vs-lifetime hypothesis is not yet proven: `CharacterMedicalRuntime` also resolves patients through `worldRegistry.Characters`, and its order remains alive for the downed Dion. Therefore the same active registry likely still contains the patient. The exact removal path must be found before changing participant lookup semantics.
- A scoped `rg "AllCharacters"` returned exit 1 because the combat directory has no such use; this was a no-match diagnostic, not a build failure.
- `AbilityRescue.StartRescue(patient)` reserves the medical order and starts a coroutine synchronously. `TickRescue` then immediately calls `actor.SetAiPaused(true)` after starting it; if pausing the brain invokes `AIRescue.OnStop`, that can stop the just-started coroutine and release the reservation. This explains the empty medical rescuer field, but by itself does not explain removal of the combat command, which should remain and retry.
- `AbilityRescue.RescueRoutine` contains the same `medicalRuntime.TryGetPatient(order, out patient)` condition twice. It is redundant and should be removed once the lifecycle defect is fixed, though it is not the source of command deletion.
- `CharacterLifecycle.SetAiPaused` only flips the pause flag and requests a replan when unpausing; it does not stop the rescue coroutine or remove the command. The pause-sequence hypothesis is therefore disproven.
- `CharacterCombatCommandRuntime.commands` resolves through `aggregateRootStore.GetOrCreateWritable(...)` on every access. If writable-root semantics clone or replace outside an active transaction, a command could be written to one root and read from another; the Aggregate store implementation is the next high-value inspection target.
- Aggregate-store inspection disproved that hypothesis: outside restore staging, `GetOrCreateWritable` returns the same live root state and does not clone or replace it.
- The terminal event proves the exact removal path is `TickRescue -> CompleteCommand("구조 대상 회복")`. Because the verifier's canonical patient remains downed, `CombatCommandParticipantQuery.FindCharacter` is returning a noncanonical same-ID actor whose lifecycle is active. Combat participant queries currently do not canonicalize `worldRegistry.Characters`, while the verifier and other mature callers explicitly use `CharacterActorCollection.GetCanonical`.
- Unity MCP dynamic command compilation cannot directly reference VContainer/Sirenix-backed project types in this environment; the attempted registry dump failed at compile time and made no state change. Diagnostics that need those references must live in the already-compiled Editor assembly or use source-level contracts.
- `CharacterActorCollection.GetCanonical` selects the base `CharacterActor` component on the same GameObject over derived compatibility components. Registry registration was bypassing this boundary, so it could store a derived actor with a separate lifecycle field while scene/test code used the canonical actor. Canonicalizing both register and unregister calls fixes the ownership boundary for all consumers, not only combat rescue.
- A previous wide source excerpt appeared to show a duplicated `TryGetPatient` condition in `AbilityRescue`, but the line-bounded UTF-8 reread proves the file contains only one condition. No cleanup is needed there; the failed combined patch changed nothing.
- Canonicalizing normal active/lifetime registration did not change the live failure. `CharacterAiWorldRegistry.Characters` can bypass those registries whenever `RestoreWorldCandidateIndex.TryGetCharacters` returns a candidate list, so a stale or noncanonical candidate projection is now the primary suspect. The diagnostic must report `aggregateRootStore.IsRestoreStaging` plus active/lifetime registry matches and component types.
- Compiled diagnostics disprove candidate leakage and duplicate registry identity: after all invalid preflights, `candidate=False`, `aggregateStaging=False`, and both active/lifetime views contain exactly the canonical downed Dion instance. The `구조 대상 회복` completion must therefore come from a transient `CharacterRecoveredEvent` or lifecycle transition rather than `CombatCommandParticipantQuery` returning another actor.
- `OnCharacterRecovered` and `TickRescue` currently use the identical terminal status text, so the new terminal event cannot distinguish an event-driven completion from the tick's state check. Recovery-event observation or a typed terminal cause is required next.
- Medical recovery handling already treats `ICharacterBodyHealthRuntime.GetSnapshot(actor).Downed` as the sole authority and ignores inconsistent recovery events. Combat command lifecycle lacked this guard, allowing the exact observed `Completed:구조 대상 회복` while the canonical patient remained downed. Applying the same guard is an authority correction, not a verifier workaround.
- One handler search again included a Windows wildcard path and emitted error 123 after useful `-g` results. The actual reads used explicit files/directories; no edit depended on the failed path segment.
- The body-authority guard resolves the regression completely: the same verifier now observes stabilization, physical parenting/carry, treatment, and recovered lifecycle, with no leaked restore candidate or Aggregate staging root. This proves the fix addresses the runtime lifecycle rather than merely relaxing the test.

## 2026-08-03 circus/captured-wildlife restore audit — initial evidence

- `CircusSaveSection` is still a hand-written warning-based staged boundary. It fabricates `new CircusSaveData()` for malformed/null JSON and calls live `runtime.Restore` during commit without semantic preflight.
- `CircusStateCodec.Restore` clamps sequence state, skips malformed/duplicate orders, cancels missing programs, and rewrites every nonterminal order to `Composition`. Those are lossy migrations inside current-version V18 restore, not strict validation.
- `WildlifeCaptureRuntime.Restore` similarly skips invalid states, rewrites five in-flight transport states to `Penned`, tolerates missing actors, replaces the captured-wildlife Aggregate, and performs live projection outside staging. `CircusRestoreProjection` then lazily releases orders/transient state on a later `EnsureCurrent` call.
- Both `CircusAggregateState` and `CapturedWildlifeAggregateState` already exist, so the correct direction mirrors captivity: strict DTO validation, candidate-world reference validation, Aggregate replacement during staging, and one ordered participant for all Unity/door/transient projection at publication.
- Circus V2 persists one monotonic `nextOrderSequence`, orders with stage/room/program/participant IDs plus phase/economy/risk/timing fields, and captured wildlife with pen/carrier/show references plus care/feed state. Strict validation must cover finite/nonnegative numbers, enum values, list/null/duplicate coherence, canonical `circus:<n>` IDs, authored programs, and cross-links between orders and captured wildlife.
- Current interfaces still expose warning-based `Restore(..., IList<string>)` on both circus and wildlife capture. They need report-based validation/restore contracts or a single circus coordinator API so UI and save sections cannot invoke permissive normalization directly.
- `CircusRuntime` already has lazy `CircusRestoreProjection` called from `Start`/`Tick`, and `WildlifeCaptureRuntime` has its own lazy actor/door projection. A transaction participant can make both explicit at publication, but door membership should be staged with `ReplaceCapturedWildlifeSubjects` rather than replayed per ID after publication.
- The combined circus participant should publish after captivity (`450`) and before any later dependent projection; `500.world.circus` is the natural ordering key. It must own both show-order Aggregate replacement and captured-wildlife Aggregate replacement so their cross-references cannot publish independently.
- To avoid adding another saved authority, terminal/transient state remains runtime-only: save validation builds fresh Aggregate roots, while publication clears access passes, return routes, and carry-parent projections. The save DTO remains the only serialization boundary.
- Runtime state semantics are now explicit: new captures are `AwaitingTransport` with a required carrier; `Transporting` keeps that carrier; pen-born/finished transport is `Penned` with no carrier; show assignment is `MovingToShow` with `assignedShowOrderId`; and escape is `Escaped` with `escaped=true`. The strict validator can enforce these combinations before any safe transient normalization.
- For current V2 snapshots, in-flight carrier/show transforms are not independently persisted. After validating their source references, the pure restore builder may canonicalize transport/show transient states back to `Penned` and nonterminal shows to `Composition`, but it must do so explicitly and deterministically rather than warning/skip during commit.
- The only non-save caller of `IWildlifeCaptureRuntime.Restore` is `GameplayPerformanceWorldConfigurator`, which abuses restore as a benchmark seeding mutation. It should call the existing explicit `TryRegisterPenBorn` command per spawned animal; then strict restore can require an active V18 transaction without retaining a compatibility escape hatch.
- Existing circus clone tests use intentionally informal IDs but never validate/restore them. New strict validation tests must construct canonical `circus:<n>`, real program IDs, and coherent stage/pen/participant fields rather than weakening production validation for clone-only fixtures.
- `BuildingInstanceId` already enforces the `building:*` protocol and should validate both stage and pen IDs. `IResourceEconomyContentCatalog.TryGetItem` can validate an optional `lastFeedItemId`, while `IWildlifeSpeciesCatalogProvider.TryGetSpecies` validates captured species IDs.
- Two path assumptions in the latest search were wrong (`Assets/Scripts/CharacterController.cs` and a guessed save-folder location for `DungeonRuntimeAggregateRootStore.cs`). Both exited 1 after useful output; the store will be located with `rg --files` before reading.
- Another exploratory search included a nonexistent `Assets/Scripts/Character` root and exited 1 after returning useful matches. The correct ability source is `Assets/Scripts/Services/Combat/AbilityRescue.cs`.
- Audit tooling repeated the known Windows error 123 by passing `CharacterCombatCommand*.cs` as a path to `rg`. Subsequent searches use the containing directory plus `-g` only.

## 2026-08-03 circus/captured-wildlife restore conversion — verified result

- Circus orders and captured wildlife now share one transactional publication boundary, `500.world.circus`. Both Aggregate slots and captured-wildlife door membership are written only to the detached candidate root; actor/carry/access-pass projection happens after root publication.
- Current-version corruption is no longer hidden. Duplicate/malformed IDs, sequence reuse, missing programs or cross-links, invalid enums/numbers, incoherent carrier/show/escape states, and invalid candidate-world stage/room/pen/actor/species/feed references fail the entire restore.
- Deterministic normalization is deliberately narrower than validation: a valid nonterminal show restarts at `Composition`, and valid in-flight wildlife returns to `Penned` with transient carrier/show references cleared. No invalid record is skipped, cancelled, clamped, or synthesized.
- Terminal show history does not require a still-existing stage or captured-wildlife record; only active orders require live world references. This avoids making ordinary post-show dismantling or later animal release invalidate otherwise valid saves.
- Restore publication must not invoke normal cancellation/release commands against the new Aggregate. The dedicated circus projection cleanup only disposes old access passes, clears transient routes, and releases actor pause/projection state.
- Benchmark/setup code was the sole non-save caller abusing wildlife restore. Replacing it with `TryRegisterPenBorn` allowed the permissive restore API to be deleted instead of retained as a compatibility backdoor.
- Evidence is direct: strict contract row PASS, V18 validator PASS, public save preflight rejection with unchanged JSON/revision/door version PASS, candidate cleanup PASS, full PlayMode `RESULT=PASS`, and final Unity Console Error 0 / Warning 0.

## 2026-08-03 invasion restore audit — next Phase 112 owner

- After the circus cutover, warning-based restore signatures remain in three invasion owners (response policy, defense engagement, owner evacuation) and one surgery owner. Invasion is the higher-risk next boundary because one save section restores all three plus threat, campaign, and active intruder GameObjects.
- `InvasionSaveSection` accepts section versions 1–3, creates empty DTOs for missing/malformed data, and commits directly into `InvasionSaveService.Restore`. The service mutates campaign and threat first, resets policies, destroys active intruders, restarts evacuation, and only then rebuilds engagements; a late failure cannot preserve the live run.
- Current restore is lossy: policy duplicates are skipped and missing assignments fall back to standard; campaign values are clamped/overwritten; intruder settings synthesize defaults; invalid intruders and engagements are skipped; invalid evacuation targets are silently recalculated. These behaviors must become either explicit version migration before V18 validation or hard current-version errors.
- No invasion Aggregate state exists yet. Pure authority should be grouped into an invasion Aggregate (threat, campaign, response policies and stable engagement DTO state), while candidate-only Unity state must be owned by an ordered restore participant.
- Active intruders cannot be restored by clearing the live director first. The director needs a detached candidate collection of inactive GameObjects, with validated runtime IDs/data/patterns/grid state and no live subscriptions or presentation until publication. Discard must destroy only candidate objects; publish swaps the collection, releases old objects, activates candidates, and attaches callbacks.
- Defense engagements must be built against candidate intruders plus candidate-world characters without starting movement/coroutines during staging. Guard preparation, reservations, combat presentation, and movement begin only after the invasion Aggregate and intruder collection are published.
- Owner evacuation requires the same split: validate the exact saved target rather than recalculating it, stage owner/target/status as data, then start movement only at publication. A bad target must reject the save and leave the existing evacuation untouched.
- A combined participant ordered after circus/candidate world state (provisionally `550.world.invasion`) is preferable to separate intruder/engagement/evacuation participants because their references form one consistency boundary.

## 2026-08-03 surgery restore audit — next Phase 112 owner

- `SurgeryPersistence.Restore` is a live mutation pipeline, not detached restoration. It cancels active transport, releases admitted patients, clears the live order list, then restores parts, storage, corpse freshness, extraction records, wildlife anatomy, and policies before it validates surgery orders.
- Corrupt current data is normalized or skipped instead of rejected: duplicate/blank order IDs are excluded with warnings, transient doctor/transport state is silently erased, missing procedures or facilities turn active work into cancelled history, and negative sequence/environment values are clamped.
- `DungeonSurgerySaveData` has no explicit payload version and combines at least seven mutable owners plus live Unity-world projections. Strict validation and one staged Aggregate/publication boundary are required before the rollback image can be removed.
- `SurgerySaveSection` still accepts section V2–V4, deserializes null/malformed payloads to an empty object, mutates old DTOs in place, and returns a delegate whose commit calls the warning-based live restore. Under the V18 incompatibility boundary it should accept one exact current section/payload version and use the typed JSON section/transaction participant pattern.
- The nested owners (`SurgicalPartRuntime`, storage state, corpse freshness, policy, extraction ledger, wildlife anatomy) each clear their own live dictionaries before filtering records. Because orders cross-reference parts, subjects, corpses, wildlife, physical stacks, and facilities, validating each list independently is insufficient; one candidate graph must validate all cross-links before any owner is replaced.
- Surgical parts, organ-storage fuel, and per-subject policy are ordinary runtime state held in independent mutable collections. They can share a replaceable surgery Aggregate slot while the existing runtimes remain command/query facades; item spawning/deletion and facility fuel delivery must stay runtime effects and must not run while a candidate snapshot is being validated.
- Corpse freshness restore immediately rebuilds against the live physical-item index, so it both filters saved state and synthesizes default freshness entries during commit. Candidate construction must instead validate exact saved corpse stack IDs against the staged physical-item world, then publish the validated map and only resume incremental indexing afterward.
- Wildlife anatomy restore silently resolves unknown profiles to a quadruped/humanoid fallback and fills missing nodes. In V18 current data, profile/species/node identity must be validated against authored anatomy and the staged wildlife world; deterministic node completion is acceptable only when the payload format explicitly omits derived nodes, not as repair for malformed saved node data.
- The existing invasion/circus cutovers establish the reusable pattern: a small domain-specific state store delegates copy-on-write and replacement to `DungeonRuntimeAggregateRootStore`, while a transaction participant stages Unity/world projections and publishes them only after the root swap. Surgery should follow this pattern rather than adding another bespoke rollback buffer.
- Circus shows that the transaction participant need not be the main gameplay runtime. A dedicated `SurgeryRestoreCoordinator` can validate the complete DTO/world graph, replace one candidate Aggregate, stage patient transport/AI projection, and publish it in order while `SurgeryRuntime` and its six supporting services remain focused command/query facades.
- `SurgeryRuntime` already receives four explicit capability bundles, so the Aggregate state store can be a fifth required constructor dependency without reviving the former 28-parameter constructor. The restore coordinator can consume the same four bundles plus root/state stores and stay below the eight-dependency composition limit.
- Surgery payload validation must treat every enum and float as untrusted JSON data: subject kind, order state, failure severity, environment resume stage, material quantities, stage work totals, risk probabilities/contributions, positions, timestamps, freshness, fuel, contamination, quality, and anatomy-node burdens all require finite/range/coherence checks before cloning into the Aggregate.
- The warning-based restore APIs on parts/storage, corpse freshness, policy, and wildlife anatomy are called only by `SurgeryPersistence`. One extraction-ledger fixture also calls its permissive restore directly; that fixture must construct/replace a surgery Aggregate snapshot instead of preserving a general mutation backdoor.
- Generated surgery order IDs use canonical `surgery:N` numbering and part IDs use the existing surgical-part sequence. Strict validation must require each stored sequence to be at least the largest canonical numeric suffix so a restored run cannot issue a colliding ID.
- Patient transport stores live carrier ability/coroutine state outside the DTO. A valid in-flight save cannot resume that Unity execution object exactly; candidate normalization should preserve the order/admission intent but clear `admissionMoveRequested`, `patientTransporterId`, and `patientTransportInProgress`, then let the published runtime deterministically request transport again. This is explicit transient normalization, not corruption repair.
- Publication must first cancel transports and clear AI/action projection from the previous orders using the captured old-order list, then project admitted patients and pending wildlife returns from the newly published Aggregate. It must not release saved physical materials or run ordinary surgery cancellation commands during staging.
- The authored `SurgicalProcedureSO.RequiredFacilityTags` and `ISurgicalFacilityQuery.Evaluate` provide the exact facility-capability check for each active order. World validation should require the saved facility ID to resolve to one live candidate building whose evaluated snapshot is available for those tags, not merely any building with a matching string ID.
- The existing `DungeonJsonSaveSection<TPayload>`/`InvasionSaveSection` pattern already supplies typed preflight and staged commit plumbing. Surgery should replace its hand-written V2–V4 delegate section with that exact base rather than duplicating deserialization and warning propagation.
- `SurgeryPlayModeVerifier` captured item and surgery sections separately and restored both runtimes directly during cleanup. That bypasses the very cross-section transaction being tested; cleanup should capture and restore one `DungeonGameSaveData` through `IDungeonGameSaveService` so candidate-world and Aggregate publication semantics are exercised.
- Focused post-cutover scans find no remaining surgery V2–V4 version branch, warning-based restore API, or direct `ISurgeryRuntime.Restore` caller. The remaining generic `runtime.Restore` hits belong to environment/medical sections and are not surgery bypasses.
### 침공 저장 감사 중 도구 출력 절단

- `InvasionPrimitives.cs`, `DefenseResponsePolicyRuntime.cs`, `InvasionDirectorRuntime.cs` 묶음 읽기가 도구 출력 한도로 절단되었다. 저장소 소스에는 절단 마커가 없으며, 근거 수집은 80줄 이하의 경계 읽기로 다시 수행한다.

## 2026-08-03 invasion restore conversion — verified result

- `DungeonInvasionSaveData`와 `InvasionSaveSection`은 정확한 V4만 허용하며, 위협·침입자·정책·교전·대피·5개 캠페인 분기의 null/ID/enum/수치/내부 참조를 라이브 변경 전에 검증한다. V1~V3와 빈 기본 DTO 복원은 더 이상 허용하지 않는다.
- 위협, 캠페인, 방어 정책은 한 `InvasionAggregateState`에 저장된다. 복원 중에는 `DungeonRuntimeAggregateRootStore`의 detached 후보만 바뀌고 캠페인 월드 거점 투영은 루트 게시 이후로 연기된다.
- active intruder는 기존 목록을 지우기 전에 비활성 RestoreCandidates 루트에서 준비된다. authored prefab과 정상 prefabless 구성 모두 detached 캐릭터 계약을 따르며, 상태·입구·격자·콘텐츠 검증 전에는 코루틴, raid-awareness 투영, `OnFinished` 구독이 시작되지 않는다.
- owner evacuation과 defense engagement도 후보 참조를 먼저 구축한다. 무효 대피 칸을 다시 계산하거나 사라진 경비/침입자를 건너뛰지 않으며, AI 정지·이동·전투 표현은 `550.world.invasion` 게시 시점에만 시작한다.
- 구형 `Restore(..., IList<string>)`, 침입자 경고/skip 복원, `RestoreFromLegacyPressure`, 설정 기본값 합성 경로를 삭제했다. 정책과 캠페인은 검증된 snapshot을 정확히 대체한다.
- Unity MCP 증거: EditMode threat/intruder/engagement 회귀 PASS, fresh/corrupt V4 validation PASS, 정상/후행 실패/V3 거부 원자 계약 PASS, active prefabless intruder 왕복 PASS, detached 후보 잔존 0, V18 authority PASS. 런타임 본체는 partial 분리 후 1,193줄과 1,093줄로 1,200줄 제한을 만족한다.

## 2026-08-03 surgery restore conversion — validator placement

- V18 validator의 정확한 구조 구간은 `RuntimeAuthorityV18Validator.cs` 780–872줄이다. 포획(450) → 서커스(500) → 침공(550) 순서로 Aggregate, strict validator, typed JSON section, ordered participant를 각각 강제하고 legacy warning/migration 토큰을 금지한다.
- 수술은 참여자 순서 `525.world.surgery`이므로 서커스 뒤·침공 앞에 동일한 네 가지 요구와 구형 Restore 금지를 배치하는 것이 기존 검증 체계와 일치한다.
- `SurgeryDebugScenarios.RunAll`은 9개의 순수/에셋 계약을 TSV로 기록하는 486줄 Editor 진입점이다. 여기에 strict V5 payload 계약을 추가하면 Unity MCP 동적 명령은 이 public 진입점만 호출해도 된다.
- 기존 `VerifyUniquePartSaveData`는 `DungeonSurgerySaveData`의 JSON 왕복만 확인하며 반환 문자열이 아직 “V16 section data”다. V5 `version`과 모든 필수 컬렉션/시퀀스를 채운 strict validator 검증으로 보완하고 문구를 V5로 고쳐야 한다.
- `SurgerySaveValidation.Validate`는 public static이며 필요한 의존성은 `ISurgicalProcedureCatalog`, `IAnatomyProfileCatalog`, `DungeonGameRestoreReport`뿐이다. 완전한 빈 V5 payload도 유효하므로, Editor 계약은 실제 authored catalogs로 빈 정상 DTO 통과·V4 거부·null collection 거부를 먼저 고정하고 최소 한 주문/부품 fixture로 ID/sequence/NaN/중복 교차 검증을 추가할 수 있다.
- `CreateState`는 검증 뒤 주문의 운반 실행 플래그만 명시적으로 초기화하고 나머지 Aggregate를 deep clone한다. 따라서 테스트는 이 transient normalization이 source DTO를 변경하지 않으면서 candidate만 정규화하는지도 직접 확인해야 한다.
- `SurgeryDebugScenarios`가 이미 같은 authored 폴더에서 42개 `SurgicalProcedureSO`와 12개 `AnatomyProfileSO`를 로드해 각각 `ResourceSurgicalProcedureCatalog`/`ResourceAnatomyProfileCatalog`를 생성한다. strict V5 검증 테스트는 새 대역이나 런타임 SO 합성 없이 이 에셋 카탈로그를 그대로 재사용할 수 있다.
- `DungeonSurgerySaveData`는 `version = 5`와 일곱 필수 컬렉션을 모두 빈 리스트로 초기화하고 두 시퀀스는 0이다. 따라서 새 DTO 자체가 canonical empty V5 fixture이며, JSON clone으로 V4/null collection/negative sequence 같은 독립 오염을 만들 수 있다.
- `SurgerySubjectPolicyState`는 subject ID와 자동 응급수술 플래그만 갖는다. 같은 canonical subject ID를 두 번 넣는 fixture가 다른 콘텐츠나 Unity 월드 없이 중복 상태 거부를 증명하는 가장 작은 strictness 사례다.
- 침공 원자 계약의 재사용 가능한 패턴은 live `DungeonRuntimeLifetimeScope`에서 runtime/root/participant를 resolve하고, 실제 capture/validate/restore를 위임하는 격리 typed section과 commit 시 한 번 실패하는 후행 section을 `DungeonSaveSectionRegistry`에 조립하는 방식이다. 정상 왕복, payload 변경 후 후행 실패 시 JSON 불변, 구형 section version 거부를 한 진입점에서 증명한다.
- 수술도 동일한 registry 경계를 쓰면 `IDungeonGameSaveService` 전체 매니페스트를 수동 수정할 필요 없이 실제 `SurgeryRestoreCoordinator`의 staging/rollback 참여를 검증할 수 있다. 격리 section의 순서는 수술 participant `525`가 게시된 뒤 후행 실패 section이 commit되는 형태여야 한다.
- 실제 `SurgerySaveSection`은 `ISurgeryRuntime.Capture`와 `SurgeryRestoreCoordinator.ValidateRestore/Restore`만 연결하며 section version은 정확한 V5다. 따라서 원자 계약용 격리 section도 이 세 호출을 그대로 위임하면 프로덕션 복원 경계를 우회하지 않는다.
- coordinator는 active V18 registry staging이 없으면 Restore를 거부하고, validation/world-reference 성공 뒤에만 후보 Aggregate를 교체한다. 현재 라이브 DTO의 `orderSequence`만 1 증가시키면 새 Unity 참조 없이도 유효한 서로 다른 후보를 만들 수 있어 후행 실패 rollback 불변 비교에 적합하다.
- `DungeonRuntimeAggregateRootStore.PublishedRestoreRevision`은 성공적인 후보 root 게시 때만 1 증가하고 discard에는 변하지 않는다. 수술 후행 실패 계약은 JSON 불변뿐 아니라 revision 불변과 `IsRestoreStaging == false`까지 함께 검사할 수 있다.
- 첫 원자 테스트 실패 원인은 소스상 명확하다. `DungeonSaveSectionRegistry.RestoreAll`은 commit 실패 시 후보 root를 discard한 뒤에도 모든 section의 `rollbackImage`를 다시 stage/commit하고 root를 publish한다. 따라서 수술 JSON은 보존돼도 `PublishedRestoreRevision`이 1 증가한다. 이는 Phase 112에 남아 있는 rollback-image 의존성 그 자체다.
- 현재 registry는 모든 section이 `IDungeonStagedSaveSection`인지 확인하지만 “commit이 detached candidate만 변경한다”는 더 강한 계약은 구분하지 않는다. rollback-free 경로를 안전하게 열려면 변환 완료 section을 명시하는 marker가 필요하고, registry에 포함된 전 section이 그 marker를 가질 때만 commit 실패 후 재적용을 생략해야 한다.
- 기존 `IDungeonRestoreTransactionParticipant` 문서는 Begin/Discard가 후보만 할당·해제하고 Publish는 실패하지 않는 pointer/visibility swap이어야 한다고 이미 명시한다. 따라서 추가 marker는 participant가 아니라 section commit의 live-mutation 부재만 선언하면 된다.
- generic `DungeonJsonSaveSection<T>` 자체는 여러 미전환 도메인도 사용하므로 base에 atomic marker를 붙이면 위험하다. `SurgerySaveSection`과 격리 원자 테스트 section처럼 변환 완료 owner가 개별 opt-in해야 한다.

## 2026-08-03 surgery restore conversion — verified result

- 수술의 주문·고유 부품·장기 보관·사체 신선도·대상 정책·적출 원장·동물 해부 상태와 두 시퀀스는 하나의 replaceable `SurgeryAggregateState`에 있다. 저장은 exact V5만 허용하고 invalid record를 skip/clamp/default하지 않는다.
- `525.world.surgery`는 후보 캐릭터·야생동물·시설·물리 스택·authored 절차/해부 프로필을 검증한 뒤 detached root만 교체한다. 운반 실행 상태만 명시적으로 재요청 가능한 transient 상태로 정규화하며 AI/운반/귀환 투영은 root 공개 이후다.
- `IDungeonRollbackFreeSaveSection`을 도입했다. registry의 모든 section이 candidate-only commit을 선언한 경우 commit 실패는 후보 root/Unity 후보를 discard하고 종료하며 rollback image를 재적용하지 않는다. 미전환 section이 포함된 registry는 기존 안전망을 계속 사용한다.
- Unity MCP 증거: V18 authority PASS(`save V18`, authored item 772, catalyst SO 168, legacy item authority 0), strict V5 contracts PASS, 정상/후행 실패/V4 거부 원자 계약 PASS, 실패 전후 수술 JSON과 published revision 불변, staging 잔존 없음, 최종 Console Error 0 / Warning 0.

## 2026-08-03 next rollback-image owner audit — initial scan

- 수술 전환 뒤 `Save|Restore|Persistence` 이름의 운영 파일에서 warning list를 직접 쓰는 곳은 `ModularFacilityWorldSaveService.cs` 하나만 남았다(`DungeonGameRestoreReport` 자체 제외). 시설은 inactive Unity candidate를 이미 사용하므로, warning/skip semantics와 section commit 소유권을 strict candidate-only로 닫으면 rollback-free 전환 효과가 크다.
- direct `IDungeonSaveSection` 파일은 여전히 방어·환경·종족·경제·생산·생존·물리 아이템·원정 등 여러 도메인에 남아 있다. 일부 파일은 테스트 nested section이나 typed section과 함께 있어 파일 단위 검색은 후보 목록일 뿐 완료 증거가 아니다. 다음 감사는 프로덕션 `ModularFacilityWorldSaveService`와 `ModularFacilityWorldSaveSection`의 정확한 복원 경로부터 시작한다.
- 시설 save section은 이미 typed JSON과 exact current-version validation을 사용하지만 rollback-free marker가 없고, `TryRestoreSnapshot`은 candidate factory/resolver/grid publisher가 하나라도 null이면 `ClearExistingBuildings` → session 적용 → live `RestoreBuilding`으로 즉시 되돌아가는 구형 직접 mutation fallback을 유지한다.
- section은 validation warnings를 버리고 restore 시 `worldReport.warnings`를 UI report로 옮긴다. migration warning과 state-module warning이 current V18 payload에서 어떤 의미인지 분리해야 하며, 구형 fallback을 삭제하려면 세 candidate 의존성을 생성자에서 필수화하고 모든 테스트/조립 경로를 갱신해야 한다.
- 운영 생성자는 이미 object factory/resolver/texture/relocation/session/grid publisher/candidate publisher를 모두 null 불가로 강제한다. 직접 live restore는 `#if UNITY_EDITOR` 2인자 생성자만 가능하게 만드는 테스트 전용 backdoor다.
- 이 2인자 생성자는 네 Editor 시나리오에서만 사용된다(진화 1, 시설 save/load 1, 건물 상태 persistence 2). 운영 fallback을 보존할 이유는 없으며, fixture를 candidate-capable 테스트 조립으로 바꾸거나 pure serialization/validation 목적에 맞는 좁은 테스트 대역으로 분리해야 한다.
- `ModularFacilitySaveLoadDebugScenarios`는 실제 source/target Grid와 stale buildings를 만들고 direct `TryRestoreSnapshot` 후 stale 파괴·게임데이터·건물 상태·레이어·JSON 왕복을 검사한다. 이 테스트는 삭제할 것이 아니라 transaction Begin → detached candidate 준비 → Publish 흐름으로 전환해야 한다.
- `BuildingStatePersistenceDebugScenarios`의 두 생성은 world V1 JSON 거부와 writer schema만 검사하며 Grid/Unity 복원을 전혀 사용하지 않는다. 이 둘은 시설 복원 서비스를 가짜 2인자 생성자로 만들지 말고 dependency-free strict facility save codec API를 호출하는 편이 책임에 맞다.
- 진화 시나리오의 네 번째 생성도 V2 JSON 거부만 확인한다. 세 schema/version fixture를 static strict codec으로 옮기면 2인자 서비스 생성자를 쓰는 곳은 실제 시설 save/load 회귀 하나만 남는다.
- 현재 `ToJson`은 null snapshot을 새 기본 DTO로 합성하지만 `FromJson`은 exact V4만 허용한다. strict codec은 serialize에서도 null을 거부해야 current-version 빈 저장을 누락 콘텐츠의 대체물로 만들지 않는다.
- `ResolveBuildingFactory`는 이미 주입된 `buildingFactory`가 있으면 그것을 사용한다. 따라서 Editor 전용 생성자를 candidate-capable 의존성(미리 조립된 factory + relocation/session/grid/candidate publishers)으로 바꾸면 테스트에서 prefab/object resolver를 재현하지 않고도 detached restore만 실행할 수 있다.
- 시설 save/load 시나리오에는 이미 DI가 적용된 `IGridBuildingFactory`와 source/target `GameSessionState`가 있다. 필요한 publisher/store 계약에는 운영 concrete `GridSystemProvider`, `RestoreWorldCandidateIndex`, `GameSessionStateStore`, `FacilityRelocationWorldService`가 존재하므로 생성자 의존성 수와 실제 조립 가능성을 확인한 뒤 fixture 전용 candidate-capable 생성자로 연결할 수 있다.
- 운영 `GridSystemProvider`와 `ScopedGameSessionStateStore`는 씬 manager/settings에 묶여 EditMode fixture에 직접 쓰기 어렵고, `FacilityRelocationWorldService`도 object factory/resolver가 필요하다. save/load fixture에는 세 좁은 대역(`TestGridSystemPublisher`, `TestSessionStateStore`, relocation no-op)과 실제 `RestoreWorldCandidateIndex`를 두는 편이 더 작고 명확하다.
- candidate publish는 live Grid 객체를 교체하므로 fixture는 publish 뒤 `TestGridSystemPublisher.CurrentGrid`를 target grid로 사용해야 한다. 기존 local target Grid에서 restored occupants를 찾는 방식은 direct-mutation 권위에 묶인 테스트다.
- `TryRestoreDetached`도 transaction 비활성 시 후보를 즉시 publish하는 두 번째 backdoor를 갖고 있다. `TryRestoreSnapshot`의 direct clear/restore 분기와 함께 삭제하고, 호출은 반드시 `BeginRestoreCandidate`가 선행된 registry transaction 안에서만 허용해야 한다.
- detached building creation은 state-module restore warning을 그대로 허용한다. 구조 검증은 null/빈 ID/중복/비양수 version/빈 payload만 확인하므로, warning 조건이 “저장에만 있거나 런타임에만 있는 module” 같은 lossy skip/default인지 확인해 current V4 strictness를 결정해야 한다.
- 시설 participant의 Publish는 후보 활성화 → 기존 시설 clear → Grid publish → session restore → detached publish 순서로 여러 fallible 호출을 포함한다. section commit을 rollback-free로 표시할 수는 있지만, 최종 one-shot world replacement 완료 전에는 publication 단계도 검증/비실패 swap으로 더 분리해야 한다.
- `BuildingStateModulePersistence.Restore`에서 유일한 warning은 현재 건물에 존재하지만 저장 payload에 없는 module을 기본값으로 유지하는 경우다. Capture가 모든 module을 저장하고 unknown/invalid/duplicate는 이미 error이므로, current-version payload에서 missing module도 콘텐츠 누락/스키마 불일치로 간주해 error로 바꾸는 것이 strict V18 원칙과 일치한다.
- 기존 진단 테스트는 missing module의 성공+warning을 의도적으로 기대한다. 이를 명시적 실패+module ID error로 고치고 warning collection/전달을 제거하면 시설 restore의 마지막 warning/default 경로를 닫을 수 있다.
- 시설 restore report의 다른 warning은 authored `BuildingSO` 배치 layer가 저장 layer와 달라졌을 때다. 저장된 runtime layer와 별개로 authored placement layer 불일치는 콘텐츠 계약 변경이므로 경고 후 계속하기보다 preflight error로 거부해야 한다.
- `migrationWarnings`와 `migratedFromVersion`은 `[NonSerialized]`이고 exact V4 `FromJson`은 migration을 수행하지 않는다. 이 비직렬 필드와 report warning API는 현행 복원에서 구형 의미만 남아 있으므로 삭제 가능하다.
- candidate-capable Editor 생성자는 미리 조립된 `IGridBuildingFactory`를 받을 수 있지만 publication은 `IGridTextureProvider.Texture.DrawBuilding`을 직접 호출한다. 기존 다른 Editor fixture는 null texture provider를 사용하므로, 시설 save/load 테스트에는 실제 `GridTexture` 컴포넌트를 만들거나 시각 publication을 별도 capability로 분리해야 한다. 운영 provider는 texture null을 허용하지 않는다.
- `GridTexture.DrawBuilding`은 missing building/tile/sprite/tilemap을 안전하게 no-op하므로, fixture GameObject에 실제 빈 `GridTexture` 컴포넌트를 붙인 provider를 사용할 수 있다. 운영 null 계약을 완화할 필요가 없다.
- `RestoreBuilding`은 transaction 비활성 fallback에서만 호출되는 live 생성/등록/state restore 구현이다. fallback 제거 후 메서드 전체를 삭제할 수 있으며, candidate 생성은 `TryCreateDetachedBuilding` 하나로 단일화된다.
- warning 필드 제거 후 남는 사용처는 시설 candidate의 state result 전달과 두 건의 BuildingState persistence 진단 문자열/기대뿐이다. legacy core-module migration 테스트는 실제 warning에 의존하지 않고 결과 문자열만 count를 출력하므로 strict error 전환과 충돌하지 않는다.
- 기존 V18 validator는 시설에 대해 `TryRestoreDetached` 존재만 요구하고 character restore가 candidate Grid를 읽는지만 검사한다. transaction-only 호출, rollback-free section, strict codec, direct `RestoreBuilding`/warning 부재는 아직 강제하지 않으므로 이번 전환에서 ratchet을 보강해야 한다.
- 첫 통합 회귀의 시설 NRE는 `TryCreateDetachedBuilding`이 test constructor에서 null인 `objectFactory`를 직접 호출한 정확한 조립 오류다. candidate 생성 abstraction은 `IGridBuildingObjectFactory`, 기존 fixture factory는 `IGridBuildingFactory`이므로 Editor 생성자도 detached object factory를 명시적으로 받아야 한다.
- `generic_stock_categories` fixture는 존재하지 않는 enum 777을 `StockCategoryPersistenceId.ToId`로 변환해 성공을 기대하는 낡은 테스트다. authored/fixed protocol cutover 이후 올바른 계약은 물리 수량이 snapshot에 없고 unknown category protocol은 거부된다는 것이므로 custom-ID 성공 문구를 제거해야 한다.
- detached 시설 생성은 `IGridBuildingObjectFactory.CreateDetached` 뒤 `IObjectResolver.Inject`를 별도로 호출한다. Editor constructor는 이미 주입된 ordinary factory만 받으면 안 되고, detached object factory와 명시적 building injector를 받아야 한다.
- 서비스 내부에 `Action<BuildableObject>` injector를 두면 운영 생성자는 `objectResolver.Inject`를, Editor fixture는 `CharacterAiEditorTestDependencies.Inject`(+Shop 보강)를 제공할 수 있다. 같은 object factory로 ordinary `GridBuildingFactory`도 조립할 수 있어 test-only resolver 구현이 필요 없다.
- `FacilityRuntimeStateModule`이 module version 1을 받아 `LegacyFacilityOperationalStateV1` 하나를 core/production/security로 분해하는 것이 legacy split 테스트의 근거였다. 그러나 현재 writer는 version 2만 쓰고 V17 이하는 복원하지 않는 확정 계획이므로 이 migration을 보존하는 것보다 version 1을 명시적으로 거부하는 편이 최종 계약에 맞다.
- `RestoreLegacyFacilityStateV1` 사용처는 legacy test와 save/load fixture 초기화뿐이다. fixture는 현행 `RestoreFacilityState`와 production module setter로 같은 상태를 만들 수 있으므로 legacy 모델·분기·테스트를 모두 제거할 수 있다. 별도 migration coverage 인터페이스는 필요 없다.
- 진화 determinism 실패는 시설 저장 변경과 무관하게 `EditorRuntimeReferenceFixtures.DungeonWithRunVariables`가 aggregate-root 주입이 필요한 `RunVariableRuntime`을 직접 노출하는 오래된 fixture 경로에서 발생한다. scenario가 필요한 것은 결정론 seed/run-variable query뿐이므로 fixture의 생성 경계를 canonical Aggregate store와 맞춰야 한다.
- `EditorRuntimeReferenceFixtures.DungeonWithRunVariables`는 disabled MonoBehaviour만 만들고 `RunVariableRuntime.Construct`의 8개 필수 의존성을 전혀 주입하지 않는다. `RunSeed` getter가 Aggregate root를 즉시 요구하므로 이 fixture는 구조 개편 이후 본질적으로 무효다.
- 운영 runtime에 Editor용 late-binding hook을 추가하는 것은 선택형 DI 제거 목표와 반대다. 진화 runtime이 결정론 seed만 필요하다면 scene-bound `RunVariableRuntime` 대신 좁은 seed/query 계약을 주입하도록 분리하거나, 해당 시나리오가 이미 제공하는 명시적 seed 경로를 사용해야 한다.
- `FacilityInstanceEvolutionRuntime`은 실제로 `DungeonSceneRuntimeReferences`에서 concrete `RunVariableRuntime`을 꺼내 `RunSeed` 하나만 읽는다. 이는 scene adapter/concrete 결합이며 테스트 fixture를 무효화한 직접 원인이다.
- 저장소에 공용 run-seed query 계약은 없다. `IRunSeedProvider { int RunSeed; }`를 도입해 `RunVariableRuntime`이 구현하고 시설 진화가 그 계약을 직접 받도록 바꾸면 운영 조립은 더 좁아지고 Editor determinism fixture는 plain fixed provider를 사용할 수 있다.

## 2026-08-03 modular facility restore conversion — verified result

- 시설 JSON은 dependency-free `ModularFacilityWorldSaveCodec`에서 exact current version만 역직렬화하며 serialize null 합성도 금지한다. schema/version fixture는 더 이상 restore service를 가짜 생성하지 않는다.
- `TryRestoreSnapshot`은 active V18 transaction이 없으면 실패한다. direct live clear/rebuild, transaction 없는 즉시 candidate publish, `RestoreBuilding`, Editor 2인자 backdoor를 삭제했고 section은 rollback-free commit을 선언한다.
- authored placement layer 불일치, missing/unknown/duplicate state module, legacy facility module v1은 모두 error다. current module default 유지 warning, migration warning DTO, facility restore warning report를 삭제했다.
- 전체 Editor 왕복은 11개 시설을 inactive replacement Grid에 구축한 뒤 stale 시설 2개를 제거하고 session/building state/layer/JSON을 정확히 보존했다. invalid overlap preflight는 live target을 건드리지 않고 거부됐다.
- 시설 진화가 concrete scene runtime 대신 `IRunSeedProvider`를 직접 받도록 좁혀 invalid Editor scene fixture 의존을 제거했다. Unity MCP 증거는 state persistence 7/7 PASS, facility round-trip 9/9 PASS, instance evolution PASS, V18 authority PASS, Console Error 0 / Warning 0이다.

## 2026-08-03 character world restore audit — initial scan

- 캐릭터 월드는 이미 detached candidate participant를 갖고 replacement facility Grid를 소비하지만 warning/default 경로가 다수 남아 있다: 빈 persistent state면 live actors 보존, owner snapshot이 없으면 current owner 유지, invalid position이면 nearest cell 이동, missing owner manager/profile/actor 참조 경고 등이 있다.
- section은 아직 rollback-free marker가 없고 `CharacterWorldSaveService.Restore`가 candidate 준비와 preserve-live 정책을 함께 결정한다. exact V18 복원에서는 저장 DTO가 빈 경우 “현재 캐릭터 유지”가 아니라 정확한 빈 세계 또는 필수 owner 누락 오류여야 하며, 좌표 보정도 실패로 바뀌어야 한다.
- `ValidateRestore`는 `actors`와 `populationProfiles`가 `null`이면 빈 목록으로 합성하고 owner를 `0..1`명으로 허용한다. V18 canonical payload라면 두 컬렉션의 존재와 정확히 한 명의 owner를 강제해야 빈 저장이 라이브 owner 보존으로 변질되지 않는다.
- 캐릭터 정의 검증이 authored `characterCatalog`뿐 아니라 현재 라이브 actor의 `Identity.Data` ID도 신뢰한다. 라이브에만 남은 정의가 저장 참조를 합법화할 수 있으므로 복원 사전 검증은 불변 콘텐츠 카탈로그만 권위로 사용해야 한다.
- null 항목을 오류로 기록한 뒤 `.Where(... != null)`로 계속 처리하는 구조는 진단에는 무해하지만, 실제 후보 준비에서도 같은 필터가 사용되면 손실 복원이 된다. 검증 실패 payload는 후보 준비 자체를 시작하지 않고 exact collection을 사용해야 한다.
- `ApplyActorState`는 저장 좌표가 유효하지 않으면 nearest walkable cell 또는 현재 transform 위치로 바꾸고 warning만 남긴다. exact V18 복원에서는 좌표의 격자 유효성·점유 가능성을 preflight에서 검증하고 후보 적용은 저장 좌표를 그대로 사용해야 한다.
- actor 하위 컬렉션도 `null → empty`, null 항목 제거, condition 중복은 `Last()` 선택, 알 수 없는 work type은 무시하는 식으로 보정된다. strict validator가 필수 컬렉션·항목·고유 키·작성된 work type을 모두 보장하고 적용 단계에서는 필터/기본값 없이 읽어야 한다.
- 저장된 lifecycle이 `Active`/`OnExpedition` 이외이면 복원 시 `Active`로 정규화된다. 저장 DTO가 표현 가능한 lifecycle을 exact 복원할 수 없다면 해당 상태를 캡처 대상에서 제외하거나 transient 필드로 명시해야 하며, 현재처럼 영속 필드로 저장한 뒤 warning으로 바꾸는 것은 허용할 수 없다.
- `DungeonCharacterWorldSaveData` 자체에는 버전/계약 표지가 없고 모든 하위 컬렉션과 snapshot이 필드 initializer로 기본 생성된다. JSON 필드 누락도 정상 empty/default처럼 보일 수 있으므로 section 버전만으로는 canonical payload를 판별할 수 없다. strict validation에서 필수 참조형 필드의 null을 전부 거부해야 한다.
- 서비스는 후보 참여자이지만 공개 계약에 `PrepareForWorldRestore()`와 `Restore(...)`가 남아 있어 section/coordinator 밖에서도 순서를 어겨 호출할 수 있다. 최소한 `Restore`가 active transaction을 강제하고, 장기적으로는 외부 계약을 capture/query와 transaction-owned restore 경계로 분리해야 한다.
- `Capture`는 persistent actor를 열거할 뿐 owner 존재를 보장하지 않는다. strict section이 정확히 한 owner를 요구하도록 바꾸면 캡처도 동일 invariant를 즉시 검사해야 자기 자신이 복원할 수 없는 V18 payload를 만들지 않는다.
- `BeginRestoreCandidate`는 `restoredActorsById`만 얕게 백업하고, `PrepareForWorldRestore`는 라이브 actor의 작업·이동·AI를 즉시 변경하는 별도 공개 단계다. 이 호출이 preflight/후보 준비 전에 실행되면 실패 복원이 라이브 행동 상태를 이미 건드릴 수 있으므로 coordinator 순서와 호출처를 확인하고 해당 변경을 commit 시점으로 옮겨야 한다.
- 현재 quiescence participant는 후보 준비가 끝난 뒤라도 publish 순서 `050`에서 라이브 캐릭터의 작업·이동·AI를 먼저 취소한다. 후행 facility/character participant publish가 실패하면 rollback-free 경계에서는 이 변경을 되돌릴 이미지가 없으므로, quiescence를 별도 선행 participant로 두면 캐릭터 section을 안전하게 rollback-free로 선언할 수 없다.
- `DungeonCharacterWorldSaveSection.ResolveRestoreGrid`는 replacement Grid가 없으면 라이브 Grid로 폴백한다. 시설 section이 필수 dependency인 V18 restore에서는 후보 Grid 누락을 오류로 처리해야 캐릭터 후보가 라이브 월드와 결합되지 않는다.
- `Restore`는 active transaction이 아니어도 즉시 publish하고, empty actor payload는 live actor 보존 candidate와 warning으로 성공시킨다. 또한 candidate 정의 사전에 현재 라이브 actor의 SO를 추가한다. 세 경로 모두 authored payload/catalog/candidate world만 신뢰하는 strict V18 계약과 충돌한다.
- owner 누락은 warning으로 현재 owner를 후보 view에 합성하고, 후보 생성자에도 `preserveLiveActors` 플래그가 전파된다. exact payload가 정확히 한 owner를 강제하면 이 두 분기와 `BuildCandidateCharacterView`의 live-owner 합성을 전부 삭제할 수 있다.
- `PublishCharacterCandidate`는 profile/reputation 복원, detached GameObject 활성화, owner publish, 기존 staff 파괴를 순차 수행한다. 모두 라이브 변경이며 중간 예외 가능성이 있으므로 rollback-free 선언 전에 입력·의존성 사전 검증을 강화하고 publish 경로가 no-fail인지 확인하거나 한 번에 교체 가능한 world root 뒤로 감싸야 한다.
- 후보 staging 시 `restoredActorsById`를 실제 공개 전에 후보 actor로 교체한다. 다른 후행 section이 candidate query를 통해 참조해야 한다면 별도 candidate index가 권위가 되어야 하고, live query인 `TryGetRestoredActor`가 준비 중 후보를 노출하는 것은 제거해야 한다.
- 현재 컬렉션 validator는 condition/work-priority 중복 일부만 검사하며 `workTypeId`의 실제 등록 여부, priority enum, mood/growth/narrative/social/carry 하위 참조의 non-null·범위 계약을 충분히 보지 않는다. 적용 코드가 모르는 work type을 조용히 건너뛰므로 strict preflight 범위를 확장해야 한다.
- `DetachedCharacterWorldCandidate` 생성자도 population profile null 항목을 제거하고 null reputation을 새 기본 snapshot으로 합성한다. candidate는 검증된 DTO를 그대로 deep-clone해야 하며, 생성자에서 데이터 손실/기본값을 만드는 정책을 제거해야 한다.
- `AddCandidateIdentity`는 null/empty ID를 조용히 return한다. preflight가 보장하더라도 commit-boundary 내부 방어는 실패해야 하며, silent omission은 `ActorsById`와 실제 후보 목록의 불일치를 숨긴다.
- `ApplyActorState`는 growth/narrative/log을 다시 `null → default/empty`로 합성하고 carry/social restore를 null 허용 호출한다. DTO initializer와 별개로 역직렬화된 explicit null을 strict validator에서 거부한 뒤 적용 경로의 `?? new`를 제거해야 한다.
- 캡처는 interaction mood factor와 최대 30개 로그만 영속화하는 명시적 축약 정책이다. 이 축약 자체는 정상이나, 복원 시 lifecycle/좌표/하위 객체를 보정하는 것과 구분해 계약·테스트에서 canonical capture → exact restore 범위를 고정해야 한다.
- `CharacterWorldSaveSection`은 typed JSON section이지만 아직 `IDungeonRollbackFreeSaveSection` marker가 없고 replacement Grid query를 선택적 폴백처럼 사용한다. 시설 후보가 필수인 V18 dependency임을 section 수준에서 강제한 뒤 marker를 붙여야 한다.
- 전용 캐릭터 월드 strict/atomic Editor 시나리오는 보이지 않고 progression/game-save의 간접 왕복만 있다. 빈 actor·owner 누락·invalid 좌표·null 필드·transaction 밖 restore·후행 commit 실패 불변성을 직접 고정하는 테스트가 필요하다.
- candidate index는 이미 Grid/시설/캐릭터를 transaction 동안 별도 read-only view로 노출한다. 따라서 live `restoredActorsById`를 staging 때 후보로 바꿀 필요가 없고, 후행 복원은 `IRestoreWorldCandidateQuery.TryGetCharacters`를 사용하도록 유지하면 된다.
- quiescence participant는 composition root에 별도 등록돼 있다. 캐릭터 publish 안에서 교체 직전 기존 actor를 정리하도록 합치면 이 선행 live mutation participant를 제거할 수 있고, transaction participant 수와 실패 표면도 줄어든다.
- `TryGetRestoredActor`는 Offense 복원이 transaction 안에서 캐릭터 후보 ID를 해석할 때 사용한다. live 인덱스를 staging에서 교체하는 대신, active transaction 동안 `stagedCandidate.ActorsById`를 우선 조회하고 publish 성공 후에만 live 인덱스를 교체하면 동일 기능을 권위 혼합 없이 유지할 수 있다.
- participant 순서는 facility `100`, construction `150`, character `200`이고 quiescence만 `050`이다. quiescence 제거 후 캐릭터 교체는 facility/Grid 및 construction candidate가 준비된 뒤 일어나며, 후행 도메인들은 candidate query/ID lookup으로 새 actor를 참조할 수 있다.
- 저장 위치 검증은 범위 검사인 `Grid.IsValidGridPos`뿐 아니라 replacement Grid의 `IsWalkable`도 요구해야 한다. 기존 nearest-cell 보정이 두 조건을 함께 대신했으므로 하나만 검사하면 벽/비통행 시설 위 좌표가 exact restore에서 살아남는다.
- `WorldCharacterProfile.Clone`와 `GlobalFacilityReputationSnapshot.Clone`도 null 하위 상태/항목을 기본값·필터로 보정한다. 복원 후보 생성에 이 Clone을 그대로 쓰려면 strict validator가 profile의 social/growth/narrative와 reputation의 rumors/reputation 컬렉션 및 항목을 먼저 완전하게 검사해야 한다.
- `CharacterGrowthState.Clone()`은 먼저 `EnsureCollections()`를 호출해 source DTO 자체의 null 필드를 채우고, null skill/draft/allocation 항목을 필터링한다. 복원 전 source 불변성까지 보장하려면 strict validation 후에만 호출하고 모든 컬렉션/항목을 non-null로 강제해야 한다.
- narrative facts도 `Facts` getter가 null 컬렉션을 생성하고 Clone이 null fact를 제거한다. strict validator는 `facts` 존재, null 항목 0, enum/유한 수치/비음수 카운트와 고유 `(domain,factId,subjectId)` 키를 검사해야 한다.
- social memory/reputation은 rumor와 `SocialMemoryFloat` 목록을 가진다. strict 검증은 모든 목록/항목 non-null, enum 정의, rumor 확률·잔여시간·수치 유한성, memory key 비어있지 않음과 중복 키 0을 요구해야 silent Clone 필터가 작동하지 않는다.
- carry inventory는 stack/item/definition ID, quantity, contamination, item component DTO를 포함한다. 아이템 Aggregate가 별도 권위이므로 캐릭터 carry snapshot이 실물 아이템 소유권을 중복 저장하는지 후속 감사가 필요하지만, 이번 cutover에서는 최소한 null/ID/수량/오염/중복 instance 계약을 strict하게 검증해야 한다.
- `CharacterCarryInventory.Restore` 자체도 null snapshot/목록/항목을 비우거나 건너뛰고 quantity·contamination을 보정한다. 캐릭터 preflight가 이 메서드 호출 전에 canonical carry DTO를 보장해야 하며, 이후 아이템 단일 권위 단계에서 이 별도 carried-item 상태를 `ItemInstanceId` 참조로 축소해야 한다.
- persistent actor 캡처는 `Despawned`와 dead만 제외하므로 `SpawningOutside`, `EnteringDungeon`, `ExitingDungeon`, 원정 준비/출발/귀환, `Downed`도 실제 저장될 수 있다. 복원에서 Active로 바꾸는 대신 각 상태를 정확히 재구성하거나 transient 상태를 영속 DTO에서 제거하는 명시적 계약이 필요하다.
- `CharacterLifecycle.SetLifecycleState`는 모든 enum 상태를 직접 설정하고 비-Active에서 AI 실행 상태를 정리하므로 DTO lifecycle을 그대로 적용할 기술 경로가 있다. 기존 Active/OnExpedition 특례와 warning normalization을 제거하고 exact 상태를 설정할 수 있다.
- `ApplyActorState`의 나머지 default/filter 경로는 strict preflight 후 제거 가능하다. work type은 validator에서 `WorkTypeCatalog.TryGet`을 요구하고 적용에서는 누락 시 예외, growth/narrative/carry/social/log은 non-null canonical 필드를 그대로 사용한다.
- `CharacterIdentity`의 role은 authored `CharacterSO`가 권위이고 런타임 setter가 없으며, character type만 명시적으로 변경 가능하다. 저장된 role이 definition role과 다르면 오류로 거부하고, character type은 저장값을 exact 적용해야 한다.
- `displayName`은 identity의 독립 상태가 아니라 initialized growth displayName → definition name → GameObject name의 파생값이다. DTO의 별도 `displayName`은 중복 권위이므로 이번 복원에서는 성장 상태와 일치하도록 검증하고, 후속 DTO 정리에서 제거하는 편이 맞다.
- `SocialMemoryFloat`는 단순 `(key,value)` DTO이므로 목록별 empty/duplicate key, non-finite value 검증으로 strict clone 계약을 고정할 수 있다.
- `CharacterExpeditionRecoveryState.CopyFrom/Clone`은 null을 0으로, stress를 0..100으로 clamp한다. preflight에서 snapshot non-null·finite·범위를 강제하면 복원 중 보정 없이 동일 값을 유지할 수 있다.
- `DungeonGameRestoreReport`는 error 목록을 공개하므로 캡처 직후 동일 `ValidateRestore`를 실행해 “캡처는 성공했지만 자기 자신이 복원 불가”한 payload를 즉시 예외로 바꿀 수 있다.
- 모든 lifecycle에 walkable cell을 강제하면 dungeon 밖에 있는 출입/원정 actor의 정상 캡처를 거부한다. spatially active한 `Active`/`Downed`만 replacement Grid walkability를 요구하고, 나머지는 저장된 grid 좌표를 상태와 함께 그대로 투영하는 계약으로 분리한다.
- V18 validator에는 아직 `050.world.characters.quiescence` 존재를 요구하는 이전 ratchet이 남아 있어 새 no-live-mutation staging 구조와 정면 충돌한다. 이를 active transaction, strict validator, rollback-free section, detached Grid 강제 요구로 교체하고 preserve/warning/nearest/direct-publish 패턴을 금지해야 한다.
- 캐릭터 월드 cutover 완료: 정확히 한 owner, authored character catalog, nested actor/profile/reputation/carry validator, lifecycle exact 적용, Active/Downed cell exact 검증, transaction-only staging, facility candidate Grid 필수, staged ID lookup과 live index 지연 교체를 적용했다. preserve-live actor, live SO 보충, nearest-cell 이동, warning/default, 직접 publish, 선행 quiescence participant는 제거됐다.
- `CharacterWorldSaveSection`은 rollback-free로 전환됐다. 실제 facility/character section과 participant를 격리 registry에 조립한 후행 고의 실패에서 owner·live Grid·Aggregate revision이 불변이고 candidate index/staging/detached actor가 모두 정리됨을 PlayMode에서 확인했다.
- 전체 V18 왕복은 owner progression Lv.4/XP19, active/passive skill, growth/narrative를 정확히 보존했고 direct restore, ownerless payload, invalid active cell, V17 root를 모두 명시적으로 거부했으며 restore warning은 0이었다.

## 2026-08-03 remaining Unity-object owner audit — wildlife first

- 남은 실제 Unity 후보 participant는 construction sites `150`, wildlife `250`, exterior zones `300`(상수 위치), medical/combat/captivity/circus/surgery/invasion이다. 현재 production rollback-free marker는 facility, character, surgery만 있으므로 registry 전체는 아직 rollback image를 유지한다.
- wildlife는 이미 exact payload version validator, replacement Grid/building/character candidate query, detached actor preparation, candidate index와 publish/discard 경계를 갖는다. section에 marker가 없고 direct transaction 외 Restore 차단·warning/default 부재·동기 candidate cleanup 증거가 충분한지 확인하면 다음 cutover로 가장 적합하다.
- exterior는 zone/character/wildlife 후보를 모두 결합해 wildlife 다음 순서가 자연스럽고, runtime의 일반 gameplay 위치 해석에는 nearest fallback이 있으므로 save restore 전용 coordinator와 혼동하지 않고 분리 감사해야 한다.
- construction sites도 detached 후보를 갖지만 1,200줄에 가까운 `WorkAmountSystem` 안에 work Aggregate와 Unity site publication이 결합돼 있어, wildlife보다 분해 판단이 크다. 우선 wildlife를 strict rollback-free로 완결한 뒤 construction/exterior를 이어간다.
- `WildlifeSaveSection`은 exact `DungeonWildlifeSaveData.CurrentVersion`과 runtime validator를 사용하지만 rollback-free marker가 없다. `WildlifeRuntime.Restore`는 active transaction, single stage, detached facility Grid를 이미 강제하며 transaction 밖 live publish 경로는 보이지 않는다.
- wildlife publish는 ecosystem/carcass 상태 복원 → 기존 actor 파괴 → population reference 교체 → 새 actor 활성화 순서다. rollback-free 선언 전 각 호출이 prevalidated/no-fail인지, discard가 detached actor를 동기 제거하는지, candidate factory가 null/default/filter를 만들지 않는지 확인해야 한다.
- actor 후보 준비는 authored species와 exact candidate Grid cell 점유를 검사하고 detached actor가 실제 Wildlife layer에 등록됐는지까지 확인한다. 실패하면 report error와 candidate discard로 종료하며 nearest-cell/default spawn 보정은 restore 경로에 없다.
- discard는 candidate population의 각 detached actor에 `DiscardDetachedRestore`를 호출한 뒤 모든 후보 목록/예약을 비운다. 실제 GameObject 제거 방식과 candidate DTO clone의 손실 여부를 다음 소스에서 확인한다.
- `WildlifeRestoreCandidate.Create`는 validator가 보장한 non-null food raid/ecosystem/carcass 컬렉션을 필터/default 없이 deep-copy한다. `NextCarcassTickAt`은 현재 clock에서 재생성되는 transient scheduler 값이고 `InitialSpawnCompleted=true`는 복원된 population이 초기 자동 spawn을 반복하지 않도록 하는 운영 상태다.
- 후보 population은 Unity actor 목록과 순수 예약/sequence 상태를 한 객체에 함께 보유한다. 이번 cutover에서는 detached publication 경계가 안전한지 증명하고, 장기적으로는 actor ID Aggregate와 Unity projection을 분리할 후보로 기록한다.
- `WildlifeActor.DiscardDetachedRestore`는 PlayMode에서 `Destroy`를 사용해 제거를 프레임 끝까지 지연한다. unpublished 후보는 외부 참조가 없어 동기 `DestroyImmediate`가 안전하며 rollback-free failure 직후 candidate leak 0을 증명하려면 캐릭터와 동일하게 즉시 제거해야 한다.
- `WildlifeCarcassService.RestoreFreshness`는 null enumerable을 empty로, null/empty entry를 skip하는 warning 없는 보정 경로다. strict save validator 뒤에서는 입력이 canonical이지만 공개 API가 다른 호출자에게 fallback을 제공하므로 호출처를 조사해 strict replacement 메서드 또는 사전조건 예외로 바꿔야 한다.
- carcass/ecosystem Restore 호출자는 wildlife transaction publication 한 곳뿐이다. 따라서 두 API를 “validated candidate replacement” 의미로 좁히고 null/invalid 입력을 예외로 거부해도 운영 호환 경로가 필요 없다.
- `WildlifeSaveValidation`은 payload/version/sequence/필수 목록, authored species, animal ID/health/state, physical carcass stack 교차참조, habitat patch의 replacement Grid 가용 cell까지 preflight한다. candidate/publish가 이 validator 계약을 다시 보정하지 않고 소비하도록 만들 수 있다.
- `WildlifeEcosystemRuntime.Restore`는 null→default, 압력/시간 clamp, null respawn skip을 수행하고 source DTO를 `pendingSaveData`로 보관한다. 유일 호출자가 validated candidate publication이므로 이 보정을 제거하고 null/duplicate를 예외로 거부한 exact replacement로 바꿔야 한다.
- ecosystem publication은 overlay/decoration을 즉시 clear하고 initialization을 reset한다. 후속 `RebuildPopulationRuntimes`가 pending patches를 replacement Grid에 적용하는지 확인해야 publish 중간 실패 표면과 live projection 교체 순서를 판단할 수 있다.
- `RebuildPopulationRuntimes`는 ecosystem을 초기화하지 않고 hunt/behavior facade만 새 population 컬렉션에 다시 결합한다. ecosystem patch 적용은 다음 Tick의 `EnsureInitialized`에서 live Grid provider를 조회하므로 facility participant가 먼저 replacement Grid를 publish한 뒤 적용된다.
- `EnsureInitialized`는 saved patch를 null/usable 필터로 제거하고, 결과가 0이면 scene/default patch 생성, water patch 교체, forage patch 보강을 수행한다. validator가 saved patch 가용성을 이미 증명해도 저장에 없던 patch를 추가·교체할 수 있어 exact round trip과 충돌한다. restore candidate publication 시 exact saved patch 집합을 적용하는 별도 strict 경로가 필요하다.
- `ApplyPendingRespawns`는 respawn 상태를 적용하지 않고 legacy version mismatch를 current로 덮어쓰기만 한다. version은 이미 exact validator가 보장하므로 이 mutation은 삭제 가능하며, 메서드명과 실제 동작 불일치도 함께 제거해야 한다.
- `IWildlifeEcosystemRuntime.Restore`가 공개 mutating API로 노출돼 strict candidate 경계를 표현하지 못한다. `PrepareRestoreCandidate(save, replacementGrid)`와 `PublishRestoreCandidate(candidate)`로 분리하면 patch 변환/가용성 검증은 stage에서, live pointer/list 교체는 participant publish에서 수행할 수 있다.
- carcass clone도 `remainingFreshnessSeconds`를 clamp한다. validator가 nonnegative finite를 보장하므로 candidate clone과 live replacement 모두 값을 그대로 복사하고 invalid direct 호출은 예외로 실패해야 exact round trip이 된다.
- `WildlifeHabitatPatch` constructor는 radius 0..12, capacity 최소 0.1, current/capacity, danger 0..1, tag trim/distinct를 보정한다. strict validator는 현재 radius 최소 1·capacity/danger 최소 0만 확인하므로 radius≤12, capacity≥0.1, danger≤1, canonical tag/ID 문자열을 추가 검증해야 candidate 변환이 값을 바꾸지 않는다.
- food-raid candidate clone도 null→default, text null→empty, stolen quantity clamp를 수행하며 유일 호출자는 validated restore candidate 생성이다. 이를 null 예외와 exact field copy로 바꿔 validator가 단일 canonicalization 권위가 되게 한다.
- 기존 wildlife PlayMode suite가 invalid preflight live actor 보존, 정상 detached actor publish, 후행 실패 candidate discard를 이미 실행한다. 이를 새 rollback-free marker/root revision/candidate index/detached object 0과 ecosystem JSON exact round trip까지 강화하면 별도 fixture를 만들 필요가 없다.
## 2026-08-03 V18 validator location correction

- `RuntimeAuthorityV18Validator`는 공용 `Editor/Validation` 폴더가 아니라 `Assets/Scripts/Services/Items/Editor/RuntimeAuthorityV18Validator.cs`에 있다.
- 이후 wildlife ratchet은 이 실제 파일을 작은 범위로 읽고 수정한다. 파일명 검색이 아니라 타입 심볼 검색이 현재 저장소 구조에서 더 신뢰할 수 있다.
- 기존 wildlife ratchet은 detached actor 준비와 publication 지연까지만 확인하며, `IDungeonRollbackFreeSaveSection`, 생태계 후보 publication, carcass exact replacement, 구형 손실 복원 제거는 아직 검사하지 않는다.
- validator 자체와 wildlife 분해 파일 다수가 현재 untracked 상태이므로 넓은 Git 작업은 피하고 해당 텍스트 경로만 명시적으로 검증해야 한다.
- V18 validator는 이미 `RequireSourceContract`/`ForbidSourceContract` 방식으로 시설·캐릭터·수술 rollback-free 계약을 고정한다. wildlife도 같은 수준의 문자열 ratchet을 추가하는 것이 기존 검증 체계와 일치한다.
- 현재 wildlife 소스에는 필요한 새 심볼(`PrepareRestoreCandidate`, `PublishRestoreCandidate`, `ReplaceFreshnessValidated`, section marker)이 존재하며, 구형 `pendingSaveData`, `ApplyPendingRespawns`, `RestoreFreshness` 심볼은 검색 결과에서 사라졌다.
- `WildlifeSaveSection`의 실제 선언은 정확히 rollback-free marker를 포함하고, `WildlifeRestoreRuntime.Restore`는 활성 V18 transaction boundary가 없으면 즉시 실패한다.
- habitat save 복원은 `WildlifeHabitatPatch.FromSave`를 통하므로 exactness 판단은 해당 구현과 validator 범위를 함께 확인해야 한다. 일반 species definition 생성자의 clamp는 저장 후보 patch 복원과는 별개다.
- `WildlifeHabitatPatch` 생성자는 값을 clamp/trim하지만 strict save validation이 그 허용 범위와 canonical 문자열·중복 규칙을 먼저 강제하므로 정상 후보에서는 값 변환이 일어나지 않는다.
- patch validation은 복원 대상 Grid에서 실제 usable exterior cell 존재까지 확인한다. 후보 생성 시 같은 Grid 조건을 재확인하므로 publication 전에 세계 참조 불일치가 차단된다.
- wildlife normal roundtrip fixture가 `TryResolveSaveScenario`의 scope 출력을 discard하면서 아래에서 candidate index를 조회해 Editor 독립 컴파일이 실패했다. 세 번째 출력을 실제 `DungeonRuntimeLifetimeScope scope`로 받으면 의도한 검사가 성립한다.
- 같은 파일의 다른 `scope` 사용 지점은 모두 지역 선언 또는 out 변수와 연결되어 있어 이번 누락은 해당 메서드 한 곳으로 한정된다.
- scope 수정 후 `Assembly-CSharp-Editor.rsp` 독립 컴파일이 진단 0으로 통과했다. Unity import도 완료되어 새 ecosystem partial과 candidate에 정식 GUID가 부여됐다.
- 검증 진입점은 `RuntimeAuthorityV18Validator.ValidateOrThrow()`, wildlife EditMode 계약은 `WildlifeDebugScenarios.RunAll(true)`, PlayMode 계약은 `RunPlayModeSnapshot(true)`로 직접 호출할 수 있다.
- PlayMode 차단 ILPP 오류의 대상은 `BuildingStatePersistenceDebugScenarios` 안의 private nested `UnlistedWorkAbility`와 이를 generic base에 넣은 nested handler 한 쌍이다.
- 운영 handler들은 동일 generic base를 top-level concrete ability와 함께 정상 사용한다. 따라서 ILPP 취약점은 generic base 자체보다 Editor fixture의 private nested generic argument 형태에 국한될 가능성이 높다.
- fixture의 unlisted ability/handler/state module은 해당 시나리오 한 곳에서만 사용되며 파일은 이미 다른 top-level test module을 둔다. 세 테스트 타입을 top-level `internal`로 이동하면 행위·가시 범위를 유지하면서 Cecil nested generic 해석 경로를 제거할 수 있다.
- generic dispatcher 계약은 그대로 유지해야 하므로 handler를 비-generic으로 우회하지 않고 `BuildingAbilityWorkCompletedHandler<UnlistedWorkAbility>` 상속 자체는 보존한다.
- top-level 이동 후에도 ILPP가 같은 closed generic을 해석하지 못해 원인은 Editor-only ability를 runtime generic base에 닫는 형태 자체로 좁혀졌다. 이전의 “generic 상속 보존” 판단은 실제 Unity 컴파일 증거와 모순되어 폐기한다.
- dispatcher가 요구하는 실제 계약은 `AbilityTypes`와 `Apply(BuildingAbility, context)`뿐이다. fixture handler가 인터페이스를 직접 구현해도 미등록 concrete ability의 정확 타입 dispatch와 상태 모듈 persistence 검증 범위는 그대로 유지된다.
- unlisted generic 제거 뒤 Play 진입은 그 다음 Editor-only generic closure인 `DungeonJsonSaveSection<CharacterProgressionSavePlayModeFacade/MarkerPayload>`에서 멈췄다. Unity Jobs ILPP가 Editor assembly의 타입 인수로 runtime generic을 닫는 형태 전반에 취약하다.
- 실제 Editor fixture에서 자체 payload를 generic 인수로 쓰는 곳은 progression marker 2개, wildlife marker, surgery fail marker, invasion fail marker다. 실제 runtime DTO를 인수로 쓰는 Editor section은 이 문제 패턴이 아니므로 유지할 수 있다.
- `DungeonJsonSaveSection<T>`가 제공하는 marker fixture용 동작은 `{}` 캡처, exact section version 확인, no-op 또는 고의 실패 stage 생성뿐이다. 이 용도는 비-generic `IDungeonSaveSection`/`IDungeonStagedSaveSection` 구현으로 동일하게 표현 가능하다.
- wildlife의 fail-after-candidate section은 이미 비-generic staged section으로 구현되어 ILPP 안전한 참고 구현이다. progression과 wildlife marker부터 같은 패턴으로 전환하고 나머지 Editor-only payload closure도 일괄 제거해야 연쇄 Play 차단을 피할 수 있다.
- 공용 interface에는 staged section과 rollback-free marker가 분리되어 있다. 공용 Editor test base는 staged/preflight만 구현하고, 각 파생 fixture가 기존 의미대로 `IDungeonRollbackFreeSaveSection`을 선택적으로 선언해야 한다.
- `IDungeonSaveSectionPreflight`는 payload 문자열/버전 검증만 요구한다. marker fixture는 자체 생성한 작은 JSON만 다루므로 공용 비-generic base에서 exact version·non-empty object JSON을 검증하고 동일 `DungeonDelegateSaveRestoreStage`를 만들 수 있다.
- 공용 `DungeonDebugStagedSaveSection`을 추가하고 progression/wildlife/surgery/invasion marker·failure sections를 비-generic staged 구현으로 전환했다. rollback-free marker는 기존에 선언하던 세 fixture에만 유지했다.
- Editor 경로에 남은 `DungeonJsonSaveSection<T>` 두 건은 모두 runtime assembly DTO(`DungeonInvasionSaveData`, `DungeonSurgerySaveData`)를 사용한다. Editor-only payload type 및 unlisted generic handler 검색 결과는 0건이다.
- 실제 Play compile은 남은 `DungeonJsonSaveSection<DungeonInvasionSaveData>`에서도 ILPP 해석 실패를 재현했다. 따라서 문제 경계는 Editor assembly에서 runtime generic base를 상속하는 모든 closed type이며 DTO 출처로 구분할 수 없다.
- 남은 두 isolated typed section은 각 service/coordinator의 `Capture`, `ValidateRestore`, `Restore`만 위임한다. JSON deserialize와 staged delegate를 interface 직접 구현으로 옮기면 의미를 보존하면서 Editor generic base 상속을 0건으로 만들 수 있다.
- invasion/surgery isolated typed sections를 `IDungeonSaveSection`·preflight·staged 직접 구현으로 바꾸고 기존 typed DTO 검증과 동일 payload instance의 staged commit을 보존했다.
- Editor 경로의 runtime generic base 상속과 unlisted generic handler 검색이 모두 0건이 됐다. 다음 Unity import/ILPP 결과가 이 원인 분석의 결정적 검증이다.
- 그 다음 ILPP 대상은 `RunVariableDebugScenarios`의 private nested `TestGuestDemandEffect : IRunVariableMultiplierEffect<string>` 한 건이다. 운영 assembly의 동일 interface 구현들은 문제가 없고 Editor 구현만 실패한다.
- fixture는 runtime의 `RunGuestDemandEffect`와 중복되는 문자열 컨텍스트 multiplier를 테스트할 가능성이 높다. 사용 지점을 확인해 운영 구현 재사용 또는 비-generic test seam으로 치환할 수 있다.
- `TestGuestDemandEffect`는 두 곳에서 `TestSpecies`에 2.25배, 그 외 1배를 반환한다. `RunGuestDemandEffect("TestSpecies", 2.25f)`가 대소문자 비교까지 동일하게 구현하므로 fixture 의미 손실 없이 교체 가능하다.
- 운영 concrete effect를 재사용하면 중복 테스트 구현을 제거하고 실제 배포 코드까지 함께 검증하므로 테스트 신뢰도도 높아진다.
- test effect를 운영 `RunGuestDemandEffect`로 교체한 뒤 runtime generic 타입 14종의 Editor 사용을 교차 감사했다. 남은 22개 hit는 `SceneRuntimeRegistry<T>` 객체 생성과 validator 문자열뿐이며 Editor type 선언의 runtime generic 상속/구현은 0건이다.
- 따라서 현재 ILPP 연쇄의 알려진 source 원인은 모두 제거됐고 `RunVariableDebugScenarios.cs` import 후 전체 Editor assembly postprocess를 다시 실행하면 된다.
- 실제 다음 ILPP 실패는 `Data<int>`였지만 Editor 소스에는 closed `Data<int>` 사용이 없고 `RuntimeAuthorityV18Validator`의 `typeof(Data<>)` reflection 검사만 있다. runtime의 `GameSessionState`와 money services가 `Data<int>`를 소유한다.
- 이 결과는 ILPP 취약점이 상속/구현에만 국한되지 않고 Editor assembly의 runtime generic 타입 메타데이터 참조 전반까지 포함함을 보여준다. validator의 open generic reflection을 비-generic 판정(`FieldType.Name`/generic definition full name 문자열)으로 바꾸는 것이 다음 최소 수정이다.
- validator의 `typeof(Data<>)`를 generic definition FullName 문자열 비교로 바꿔 Editor metadata에서 Data generic 참조를 제거했다. Error message의 `Data<T>` 텍스트만 남는다.
- runtime generic cross-audit에서 실제 코드 사용으로 남은 것은 `CharacterAiEditorTestDependencies`의 `SceneRuntimeRegistry<T>` 다섯 생성뿐이다. 다음 ILPP 실행에서 문제가 되면 runtime composition helper로 묶거나 non-generic registry factory를 사용해야 한다.
- `Data.cs`는 현재 `Assembly-CSharp.rsp` 입력에 포함되고, Bee runtime DLL과 `Library/ScriptAssemblies` DLL의 SHA-256 및 길이가 정확히 일치한다. 단순 runtime DLL 복사 불일치는 아니다.
- 그러나 runtime DLL 최종 수정 시각은 wildlife 새 파일 import 시점(00:50)으로 고정돼 있고, 이후 변경은 모두 Editor 소스였다. ILPP가 runtime generic definition 자체를 못 찾는 이유를 좁히려면 Editor rsp의 참조 방식과 DLL metadata를 직접 확인해야 한다.
- Editor rsp는 runtime 구현 DLL이 아니라 `Library/Bee/.../Assembly-CSharp.ref.dll`을 참조한다. 보조 runtime csc 명령은 `-out`만 Temp로 덮고 rsp 안의 `-refout`을 덮지 않아 Bee reference assembly를 수동 컴파일 결과로 재작성했다.
- 수동 출력 파일명이 assembly identity가 되므로 Bee의 `Assembly-CSharp.ref.dll` 내부 이름이 `CodexWildlifeRuntimeImportedCheck`로 오염됐을 가능성이 매우 높다. 이것이면 Editor IL과 ILPP resolver의 Assembly-CSharp identity 불일치 및 generic resolve 연쇄를 모두 설명한다.
- binary 검사로 오염 identity를 직접 확인했고 Unity MCP runtime reimport가 정식 ref DLL을 재생성하자 ILPP 연쇄가 종료되고 PlayMode 진입이 복구됐다. 이후 보조 csc에는 반드시 Temp `-refout`도 함께 지정해야 한다.
- wildlife strict restore는 ecosystem patch JSON, actor instance replacement, candidate index cleanup, warning 0을 정상 왕복에서 확인했고, 고의 후반 실패에서는 live Grid/actors/root revision/detached candidate count를 모두 보존했다.
## 2026-08-03 exterior activity strict restore audit

- `ExteriorActivitySaveSection` is staged through `DungeonJsonSaveSection` and runtime transaction participation, but it does not declare `IDungeonRollbackFreeSaveSection`, so the registry still needs a rollback image when this section is present.
- `ExteriorActivityRuntime` delegates candidate preparation/publication to `ExteriorActivityRestoreCoordinator`; source search shows the coordinator clears and repopulates live zone/incident lists during publication.
- `ExteriorZoneMarker.RestoreState` clamps five saved fields. Whether this is lossy depends on preflight ranges; validation and candidate construction must be audited together before marking rollback-free.
- coordinator already requires an active V18 transaction, the detached facility Grid, candidate-aware building/character/wildlife references, and inactive zone objects. It publishes at ordered participant `300.world.exterior-zones`.
- publication retires every live zone before clearing/repopulating lists, then activates and registers each candidate one-by-one. This is not yet a demonstrably non-failing pointer/visibility swap and needs stronger prevalidation/publication semantics before adding the rollback-free marker.
- candidate creation catch still uses delayed `Destroy` when injection fails before the marker enters detached mode; this can leak an unpublished GameObject across a failed transaction frame and must be made synchronous.
- preflight validates all clamped zone fields against the exact same ranges and nonnegative integer bounds, so `ApplySaveData` does not change any valid saved value. Incident durations/progress and typed/canonical IDs are also validated before cloning.
- validation trims zone/incident/reference IDs for checks but does not always require the stored string itself to equal its trimmed form. Because clone/candidate state can retain whitespace even when references are looked up with trimmed strings, canonical equality must be added for exact round-trip guarantees.
- exterior zone detached lifecycle is inherited from `BuildableObject`; its publish/discard/retire implementation must be checked for synchronous cleanup and possible throwing operations before section marker promotion.
- `BuildableObject.DiscardDetachedRestore` and `RetireForWorldReplacement` both use delayed `Destroy` in PlayMode. That violates the same failed-candidate cleanup invariant already fixed for characters/wildlife and explains why exterior tests do not currently count detached leftovers.
- current exterior late-failure fixture appends a non-rollback-free fail section to the full live registry and expects `CommitCount == 2`, explicitly proving rollback-image replay rather than rollback-free publication. It must be replaced with an isolated all-marker registry and `CommitCount == 1`.
- normal roundtrip fixture compares zone IDs and instance replacement only; it should also compare exact zone/incident JSON and candidate index cleanup/warnings to prove that canonical save state survives unchanged.
- incident `Clone()` is field-for-field and only replaces null lists, which preflight already rejects. No numeric normalization occurs in incident candidate creation.
- coordinator currently sorts zones by type/ID while capture preserves live list order; this can change serialized order on a valid roundtrip. Preserving validated payload order in candidate construction is the simplest exact-state contract.
- `BuildableObject.PublishDetachedRestore` performs the same world-registry/contract publication already accepted by rollback-free facility restore. Exterior can use that boundary once candidate cleanup is synchronous and its fixture proves late-failure discard without rollback replay.
- existing V18 exterior ratchet checks transaction staging and detached publication but not the rollback-free section marker, synchronous destruction, canonical IDs, payload order, or one-commit late-failure proof.
- the production section/coordinator changes now satisfy those missing contracts; validator must make them non-regressible before Unity execution evidence is accepted.
- 첫 fixture patch가 동일한 호출 모양 때문에 invalid-preflight 메서드에 scope 변수를 넣고 normal-roundtrip에는 discard를 남겼다. compiler line evidence로 범위를 특정했으며 두 호출을 각각 원래 목적에 맞게 교정한다.
- 접객 작업 후보는 `ReceptionPoint` 하나에 한정되지 않는다. `ExteriorZoneMarker.CanRunReceptionWork`와 authored archetype 모두 `IncidentPoint`도 합법 접객 시설로 정의하므로 fixture가 첫 reception marker와 reference-equality를 요구한 것은 실제 규칙보다 좁은 잘못된 기대였다.
- 외부 사건 페이싱은 1~3일차 모든 자연 사건을 차단하고 `Thief`는 31일차부터 허용한다. PlayMode fixture는 기준 V18 저장을 캡처하고 테스트 안에서만 31일차로 전진한 뒤 사건/section 캡처를 확인하고 기준 저장을 원자 복원해야 날짜 잠금과 테스트 격리를 동시에 보존한다.
- 수정된 전체 외부 활동 PlayMode suite는 합법 접객 후보, 사건 생성/저장, invalid preflight live 보존, exact JSON 왕복, rollback-free 후행 실패 후보 정리를 모두 통과했다. 이어진 V18 authority는 772 authored items, 168 catalyst SOs, legacy authority 0으로 통과했고 Console은 Error 0 / Warning 0이다.

## 2026-08-03 physical-item reservation round-trip audit

- 전체 V18 왕복에서 달라진 것은 시작 자원 4스택의 `reservedByPersistentId="owner"`가 복원 뒤 빈 값이 되는 현상이다. 저장 DTO에는 예약 필드가 있으나 `WorldItemPersistenceService`는 restore candidate 생성 시 예약을 명시적으로 비우는 경로가 존재한다.
- 시작 자원은 `PreparedStartPartyGameplayApplier`가 `IWorldItemStackRuntime.SpawnStockAtDropoff`로 생성한다. 다음 감사는 이 호출이 넘기는 persistent owner ID와 `SpawnStockAtDropoff`의 예약 의미를 확인해, 운반 예약을 영속 상태로 둘지 capture에서 제거할지 결정해야 한다.
- 시작 보급품 호출은 예약자나 목적지 ID를 넘기지 않는 4인자 기본 overload다. 따라서 `owner` 값은 시작 파티 코드가 직접 저장한 것이 아니라 `SpawnStockAtDropoff`의 기본 목적지/드롭오프 구현 또는 후속 AI 예약에서 생긴다.
- `SpawnStockAtDropoff` 기본 overload는 loose 상태와 빈 destination으로만 스택을 만든다. 실제 `owner` 값은 생성 후 AI가 잡은 운반 예약이다.
- `WorldItemPersistenceService`는 capture 시 `reservedByPersistentId`를 그대로 DTO에 쓰지만 restore candidate에는 모든 일반 예약을 빈 값으로 만들고, combat-loadout direct pickup 예약만 source storage/loose 상태로 되돌린다. 즉 현재 구현 자체가 예약을 transient로 취급하면서도 비정규 payload를 캡처하는 단일 원인 불일치다.
- `PhysicalItemsSaveSection`은 아직 rollback-free marker가 없고 별도 preflight 인터페이스도 구현하지 않는다. section version은 exact로 검사하지만 내부 DTO는 `WorldItemPersistenceService.StageRestore`에서 null을 빈 월드로 합성하고 V1~current를 허용하며 invalid item entry를 skip한다.
- 따라서 예약 한 필드만 고치기보다 물리 아이템 owner를 current-version strict detached Aggregate로 닫는 것이 Phase 112 방향과 맞다. 최소 계약은 capture에서 transient 예약 제거, 저장 payload의 예약자 필드 금지, null/구버전/invalid entry 거부, rollback-free section marker, full exact round trip이다.
- live `WorldItemStackRecord.reservedByPersistentId`는 운반/직접 pickup 동작을 위한 런타임 필드이며 공개 저장 인터페이스의 필수 권위가 아니다. 현재 save DTO 선언은 Items 폴더 밖의 Foundation 계약에 있을 가능성이 있어 정의 위치와 버전 정책을 별도로 확인해야 한다.
- 물리 DTO는 `Assets/Scripts/Models/Items/Core/ItemPrimitives.cs`의 V6이며 예약 필드를 포함한다. 필드를 즉시 삭제하면 JSON/테스트 계약 파급이 커지므로 V18에서는 필드는 남기되 canonical 값은 빈 문자열로 고정하고 non-empty payload를 preflight에서 거부하는 방향이 안전하면서도 단일 권위 원칙에 맞다.
- StageRestore의 lossy 경로는 예약 외에도 null snapshot→빈 월드, V1~V5 migration, null stack list→empty, invalid entry skip, enum fallback, contamination clamp, legacy waste/component 합성, null hauling settings→default, component/value null filtering·trim·schema clamp까지 포함한다. strict V6 validator가 이를 선행해 정상 후보 변환이 필드 값을 바꾸지 않도록 해야 한다.
- commit은 hauling settings restore 후 repository state pointer replacement 두 단계다. rollback-free 선언 전 `ItemHaulingSettings` 복원이 검증 후 비실패인지, repository replacement가 단순 swap인지 확인하고 capture/restore preflight를 공용 validator로 묶어야 한다.
- `ResourceItemHaulingSettingsProvider.Restore`는 shared Aggregate root store에 새 runtime component를 replace하고, repository도 같은 root store에 detached state를 replace한다. V18 transaction staging 중에는 live root가 아니라 candidate root를 바꾸므로 section은 strict preflight 이후 rollback-free participant가 될 수 있다.
- hauling multiplier는 provider가 1..2.5 범위·0.05 단위로 반올림한다. save validator가 finite/range/step canonicality를 먼저 요구하면 restore의 `Normalize()`가 유효 payload를 바꾸지 않는다. null settings/default fallback은 거부해야 한다.
- 기존 `PhysicalItemDebugScenarios.VerifyRestoreReleasesTransientReservations`는 noncanonical payload에 예약자를 직접 넣고 restore가 조용히 비우기를 성공 조건으로 삼는다. strict V18에서는 이 테스트를 “live 예약은 capture에서 제외됨”과 “non-empty saved reservation은 preflight 거부·live 보존” 두 계약으로 교체해야 한다.
- 공용 `CreatePileSnapshot`에도 facility buffer 예약자가 박혀 있어 strict 전환 시 다른 pile/selection/roundtrip fixture를 함께 canonical payload로 바꿔야 한다.
- `ItemStackId`/`ItemInstanceId` 생성자는 trim 정규화하므로 strict validator는 `saved == typed.Value` equality도 검사해야 whitespace ID가 lookup 중 조용히 정규화되지 않는다. 기존 fixture의 `stack:*` 형식은 타입 계약상 유효하다.
- combat-loadout 예약은 단순 예약자 필드 제거만으로 부족하다. capture 시 저장된 source storage로 복귀한 durable 상태(state/destination/source/destination-position)를 출력해야 restore가 추가 정규화 없이 exact 후보를 만들 수 있다. 일반 운반 예약은 destination을 유지하고 예약자만 제외한다.
- Physical fixture는 `IWorldItemStackRuntime.TryReserveStoredItemForDirectPickup`을 공개 API로 이미 노출하므로, canonical snapshot을 restore한 뒤 실제 예약을 생성하고 capture 결과가 durable source 상태인지 검증할 수 있다. 반면 invalid payload 보존 검사는 section/preflight 수준에서 별도로 두는 편이 맞다.
- 최근 strict owner들은 typed `DungeonJsonSaveSection<T>`의 `ValidatePayload`를 공용 runtime validator에 연결하고 `IDungeonRollbackFreeSaveSection`을 선언한다. 물리 section은 custom staged 경계를 유지하더라도 동일한 explicit preflight+marker 계약을 구현해야 registry의 all-marker 원자 경로에 참여한다.
- registry 계약상 typed DTO section은 `IDungeonSaveSectionPreflight`를 구현해야 하며, staged commit은 모든 preflight/staging이 끝난 뒤 실행된다. 물리 section은 현재 이 preflight가 누락돼 있으므로 JSON deserialize→strict validator를 공용 helper로 만들고 StageRestore에서도 같은 helper를 재사용해야 한다.
- 프로젝트의 strict validator 관례는 DTO를 직접 변경하지 않고 `DungeonGameRestoreReport`에 모든 오류를 누적한다. direct runtime restore에는 같은 검증을 실행한 뒤 실패를 예외로 승격하는 thin wrapper가 필요하다.
- `ItemInstanceComponentSaveData.Clone`은 null을 제거하고 component ID/key를 trim하며 schema를 최소 1로 올린다. strict validator가 null/빈·비정규 ID/key, invalid kind, non-finite decimal, schema<1을 거부하면 candidate clone은 유효 입력을 바꾸지 않는다.
- physical capture는 stacks만 y/x/item ID로 정렬하고 tie-breaker stack ID가 없으며 unique item dictionary는 정렬하지 않는다. exact deterministic JSON을 위해 stack ID와 unique instance ID 정렬을 추가하고 validator도 그 canonical order를 확인해야 한다.
- `IDungeonItemCatalogProvider.TryGetDefinition`이 있으므로 strict validator는 unknown item을 예외 의존 없이 report에 누적하고, MaxStack/unique item 규칙까지 실제 authored/test 카탈로그 기준으로 검사할 수 있다.
- Physical fixture에는 simple `CharacterActor`와 test character-ID registry 조립이 이미 존재한다. 일반 운반 예약은 실제 runtime API로 생성해 capture omission을 검증할 수 있고, direct-pickup durable 복귀는 별도 warehouse-backed fixture가 필요할 수 있다.
- `TryReserveBestHaulJob` requires a real downstream destination, so a pile-only fixture may not produce a reservation. The lower-level `ItemReservationService` or an existing registered test warehouse should be used rather than fabricating DTO reservation state.
- `ItemReservationService.TryReserve` is the actual runtime mutation boundary and only needs the repository plus a null marker presenter. Physical fixture can reserve an existing canonical stack through this production service, prove live state is reserved, then prove `Capture()` emits an empty reservation without DTO fabrication.
- Editor fixture already provides singleton `EditorNullItemMarkerPresenter.Instance`, and `WorldItemStackRuntime` itself implements `IPhysicalItemRestoreStaging`, so strict section/preflight tests need no new mock surface.
- 구현 후 Physical item 전체 Editor 계약이 Unity 실제 컴파일/실행에서 통과했다. 따라서 transient reservation omission과 invalid payload 무변경 거부는 단위 수준에서 증명됐고, 남은 결정적 증거는 실제 start-party live world의 전체 V18 save→restore signature 일치다.
- 실제 start-party PlayMode 전체 V18 왕복도 54개 section, physical stacks 6→6, item signature diff 0으로 통과했다. 이로써 예약은 런타임 운반 capability의 transient 상태이고 물리 Aggregate의 durable 저장 권위에는 포함되지 않는다는 계약이 live 증거로 확정됐다.
- 기존 `RuntimeAuthorityV18Validator`에는 physical section/persistence strictness ratchet이 없고 장비 runtime이 repository를 직접 clear하지 않는지만 검사한다. 새 validator/rollback-free marker/capture omission/legacy migration 부재를 source contract로 고정해야 회귀 방지가 된다.

## 2026-08-03 remaining rollback-image owner inventory

- Unity runtime reflection 기준 public production save section 47개가 아직 `IDungeonRollbackFreeSaveSection`을 선언하지 않는다. 이미 strict detached Aggregate로 전환된 captivity/circus/invasion 등도 marker만 누락된 경우가 포함되어, “미전환 47개”가 모두 같은 작업량을 뜻하지는 않는다.
- 남은 목록은 전투 7, 산업 인프라 4, 경제/운영 다수, run/foundation, 생존/환경, 연구/메타/디버그 등이다. 다음 단계는 section별 staged commit이 candidate aggregate만 쓰는지와 publication side effect가 있는지를 분류해 안전한 marker-only 군과 추가 구현 필요 군을 분리하는 것이다.
- Captivity/Circus/Invasion section은 이미 typed strict validation과 detached runtime restore를 호출하지만 marker 선언이 실제로 빠져 있다. 이전 late-failure/invalid-preflight 증거가 있는 이 세 개는 우선 marker-only 전환 후보이며 validator ratchet도 현재 strict 호출만 검사하고 marker를 요구하지 않는지 확인해야 한다.
- V18 validator는 세 section에 대해 typed boundary와 coordinator participant만 요구하고 rollback-free marker는 요구하지 않는다(수술만 요구). 세 marker와 ratchet을 추가한 뒤 `CaptivityCircusDebugScenarios`, invasion threat/intruder/combat/defense suites로 회귀하면 이미 증명된 candidate publication 의미를 코드 계약에 반영할 수 있다.
- 세 marker와 ratchet 추가 후 모든 captivity/circus/invasion/defense 관련 suite 및 V18 authority가 통과했다. reflection 기준 rollback-image 의존 section은 47개에서 44개로 줄었으며, 이 목록을 진행률의 권위 있는 수치로 사용한다.

## 2026-08-03 combat save-owner audit

- combat의 남은 7개 중 equipment evolution과 body health는 custom section에서 V1/V2 migration, warning, null→default를 수행하므로 marker-only 전환 대상이 아니다. CombatEquipment도 null JSON→default를 허용하고 explicit preflight가 없다.
- CharacterMedical, DefenseTactical, EquipmentMaintenance, CharacterCombatCommand는 공용 typed preflight를 사용한다. 다음 감사는 각 runtime `Restore`가 candidate Aggregate root만 교체하고 projection을 published revision 뒤로 미루는지 확인해 marker-only 가능 여부를 결정한다.
- DefenseTactical와 EquipmentMaintenance restore는 strict validator 뒤 shared Aggregate root slot만 replace한다. DefenseTactical의 추가 변경은 rebuildable read-view cache invalidation뿐이며 durable live state를 쓰지 않는다.
- CharacterMedical과 CharacterCombatCommand는 각각 restore transaction participant를 갖고 active V18 transaction과 detached world references를 검증한 뒤 Aggregate candidate를 준비한다. CombatCommand publication은 actor AI pause/presentation projection을 적용하므로 prevalidation/non-failing publication 증거를 기존 tactical PlayMode 회귀와 함께 확인해야 한다.
- CharacterMedical candidate preparation의 lifecycle/order 정규화는 detached candidate character와 새 Aggregate state에 적용되고, downed Grid occupants도 candidate Grid에만 등록된다. publication은 검증된 registration/reservation projection 교체이며 이전 tactical/medical PlayMode suite가 late-failure 보존을 이미 확인했다.
- combat Editor의 공용 `CombatSystemDebugScenarios.RunAll(true)`가 네 marker 후보의 broad regression 진입점이다. 세 legacy/lossy owners(equipment/evolution/body-health)는 이번 marker 묶음에서 제외한다.
- 네 combat marker 추가 후 full CombatSystem contracts와 V18 authority가 통과했고 reflection count는 44→40이다. CharacterMedical/DefenseTactical/EquipmentMaintenance/CharacterCombatCommand는 이제 rollback-image 의존 목록에서 제거됐다.

## 2026-08-03 economy owner triage

- AnimalHusbandry, CropPlot, GrandProject save sections는 exact section version만 검사하고 empty/null JSON을 새 default DTO로 합성하며 공용 preflight도 없다. runtime이 Aggregate를 쓰더라도 현재 section 경계는 lossy이므로 marker-only 전환하면 안 된다.
- 남은 owner 중 공용 `DungeonJsonSaveSection<T>` 기반인 FacilityShop/RegularCustomer/StaffDiscontent/OperatingDay/EventAlert/Codex/ServiceRooms/Meta/RunVariable/Research/RandomStream부터 runtime publication 안전성을 감사하는 편이 marker-only 후보를 더 정확히 고를 수 있다.
- FacilityShop/RegularCustomer/StaffDiscontent는 typed base를 쓰지만 null list를 empty로 허용하거나 restore에서 invalid record를 skip/trim하며, StaffDiscontent는 별도 ValidatePayload조차 없다. 이 세 개도 marker-only 전환 대상이 아니다.
- OperatingDaySettlement와 EventAlert는 별도 strict validation을 공용 typed preflight에 연결한다. OperatingDay는 이전 detached Aggregate late-failure proof가 있어 우선 marker 후보이고, EventAlert save service의 restore state ownership을 추가 확인해야 한다.
- 두 operation save service 구현은 Operation 폴더가 아니라 Infrastructure의 `OperatingDaySettlementSaveService.cs`와 `EventAlertSaveService.cs`에 있다. section만 보고 소유권을 판단하지 않고 해당 Restore 구현을 기준으로 marker를 결정한다.
- OperatingDay/EventAlert service Restore는 validator 뒤 runtime state replace를 호출하지만 service 내부에는 source null→default fallback이 남아 있다. Registry preflight에서는 section validator가 null을 거부할 수 있으나 direct service 경계는 아직 strict하지 않다.
- 더 근본적으로 공용 `DungeonJsonSaveSection<T>.StageRestore`가 empty/null JSON과 null migration 결과를 새 DTO로 합성하고, `Capture()`도 null payload를 default로 바꾼다. `ValidatePayload`만 strict라 registry 정상 경로는 보호되지만 direct section.Restore와 fixture 경로가 우회한다. marker 확대 전에 이 공용 typed boundary를 strict deserialize/capture로 고치는 것이 20여 section을 동시에 정상화한다.
- strict base 변경 뒤 save registry suite의 유일한 실패는 aggregate-candidate commit failure 계약이다. 다른 dependency/staging/rollback/participant 계약은 통과했으므로 JSON strictness 전반이 아니라 해당 fixture section의 payload/marker 구성과 registry branch 선택을 조사해야 한다.
- 실패 fixture는 typed base를 사용하지 않는 두 직접 test section(`AggregateTransactionFakeSection`, `TransactionFakeSection`)으로 구성된다. 따라서 strict JSON base 변경이 직접 원인일 가능성은 낮고, 최근 production marker 증가가 TypeCache/registry 전역 판단에 간접 영향을 줬는지 또는 fixture rollback image 캡처가 strict owner 상태를 읽다가 실패했는지 registry report를 상세화해야 한다.
- 상세 진단 결과 durable root/last 값은 10/30으로 정확히 복원됐지만 `PublishedRestoreRevision`만 1이었다. 두 fake section이 rollback-free marker가 아니므로 registry가 의도대로 rollback image를 재공개한 결과이며, fixture의 “candidate discard/revision 0” 기대와 구성 자체가 모순된다.
- 이 계약은 aggregate fake와 “live 값을 건드리기 전에 실패하는 rollback-free fail section” 두 개로 구성해야 all-marker branch를 실제로 타고 revision 0/candidate discard를 증명한다. 기존 non-marker TransactionFake는 별도의 rollback-image 회귀에 유지한다.
- fixture를 실제 all-marker 구성으로 교정하자 strict typed base와 save registry 전체 suite가 통과했다. 공용 typed section은 이제 capture null, empty/invalid JSON, deserialize null, migration null을 default DTO로 숨기지 않는다.
- EventAlert runtime restore constructs a fresh `EventAlertAggregateState` and validates record identity again before replacement, so its durable state path is candidate-root friendly; remaining publication/UI cache mutations must be checked later in the method.
- OperatingDay runtime rebuilds a fresh Aggregate but uses clamps and conditional skips. Its section validator is intended to make these no-ops for valid payloads; exact range/list validation and final replacement path must be checked before adding the marker.
- EventAlert defers presentation rebuilding when `aggregateRootStore.IsRestoreStaging` and follows `PublishedRestoreRevision` in Update, so staged commit changes only the candidate root. Its validator requires the record list.
- OperatingDay validator requires mood/history and all nested lists/ranges; previous atomic fixture proved candidate-root late-failure preservation. The runtime's clamp/filter steps are no-ops for validated payloads, and final state writes only `RequireAggregateRoot().Replace(restored)`. Both operation sections are marker candidates once direct service null→default fallback is removed.

## 2026-08-03 foundation/run typed owner audit

- RunVariable still accepts V1, mutates payload during migration, warns/skips missing runtime/definitions, and defaults nested start/list state. It requires a real strict-current rewrite before marker promotion.
- RandomStream is a small candidate-root owner with existing failed-restore/live-handle registry tests, but validator currently treats null streams as empty, trims IDs, accepts noncanonical numeric strings, and capture does not explicitly sort. It can be fully strictified locally, then marked rollback-free and covered by the existing save-section suite.
- Codex, MetaProgression, ServiceRooms remain lossy: Codex permits null lists and skips invalid entries; Meta has no validator and merges/defaults nested state rather than replacing an exact Aggregate; ServiceRooms has no validator and defaults null payload/contract state.
- ExperiencePacing and ExternalInfluence are optional sections with missing-data fallback, version migration, warnings, and default DTO/reset behavior. They cannot be marked rollback-free until V18 makes them required current-version sections or models missing state as an explicit canonical payload.
- Codex runtime state already lives in `CodexAggregateState`; `ReplaceStateFromRestore` deep-clones into the shared candidate root when present and has no live projection side effect. Codex can be promoted after tightening its DTO lists/order/text/source validation and removing restore skips/defaults.
- Codex domain regression failures came from `CodexScenarioWorld.CreateFacility` constructing BuildableObjects without the now-mandatory `BuildingInstanceId`. The correct fixture fix is `RestorePersistentIdentity` with a typed `building:*` ID before `Initialization`, not reintroducing name-based runtime fallback.
- typed facility ID fixture fix 후 Codex 전체 domain contracts와 V18 ratchet이 통과했다. Codex는 strict canonical DTO→fresh CodexState→candidate Aggregate deep clone 경계로 rollback-image 목록에서 제거할 수 있다.
## 2026-08-03 V18 continuation resynchronization

- Phase 112의 현재 미완료 계약은 남은 Unity-object 저장 소유자를 strict detached/rollback-free 경계로 바꾸고 Registry rollback image 의존성을 제거하는 것이다.
- 작업 주문·건설 현장과 물리 아이템 V6 strict 전환 및 54-section live V18 왕복은 완료 증거가 있으며, 다음 판단은 기억상의 36개가 아니라 Unity TypeCache가 산출하는 실제 non-marker production section 수를 기준으로 한다.
- 계획/발견/진행 파일과 대규모 diff 통계를 한 호출에 합치면 출력이 절단되므로, 이후 owner 감사는 파일별 소범위 조회와 2-view 기록 규칙을 유지한다.

## 2026-08-03 remaining rollback-image owner recount and first candidates

- Unity `TypeCache`를 null namespace와 Editor assembly를 명시 처리해 다시 계산한 결과 non-rollback-free public production save section은 정확히 36개다.
- `DefenseFacilitySaveSection`과 `FactionSaveSection`은 둘 다 optional staged section이며, null/empty JSON을 기본 DTO로 합성하고 missing section도 기본/무변경 상태로 허용한다. 현재 상태로는 strict V18 marker 후보가 아니다.
- 두 runtime은 이미 각각 `DefenseFacilityAggregateState`/`FactionAggregateState` 형태의 plain-state 소유자를 사용하지만 `Restore`가 clamp/default/skip을 수행하는지와 실제 root publication이 단순 교체인지 추가 확인해야 한다. section strictification만으로 충분하다고 가정하지 않는다.
- Defense restore는 valid state dictionary를 새 `DefenseFacilityAggregateState`에 담아 `aggregateRootStore.Replace`만 수행한다. 현재 lossy 지점은 null/blank entry skip, duplicate last-write, condition clamp이며 strict validator가 이를 모두 사전 거부하면 publication 자체는 candidate-root 교체다.
- Faction restore도 새 `FactionAggregateState`를 교체하지만 authored catalog/world에서 default state를 먼저 생성한 뒤 저장된 faction만 덮어쓰고 day/sequence clamp, null skip을 수행한다. exact V18 계약으로 승격하려면 authored faction 전체성·route canonicality까지 검증하고 valid payload에서는 restore가 값과 순서를 바꾸지 않는다는 증거가 필요하다.
- 두 section의 missing/default 경로는 V17 이하 비호환 및 V18 required-section 원칙과 충돌하므로, rollback-free marker만 붙이는 대신 optional 인터페이스/StageMissing 경로 제거 여부를 기존 strict section 패턴과 대조한다.
- 기존 strict `CodexSaveSection`은 공용 `DungeonJsonSaveSection<T>`를 사용해 exact current DTO version, non-null payload, `ValidatePayload`, staged restore를 공유하고 marker만 추가한다. Defense도 같은 경계로 옮기는 것이 중복 custom section 로직을 유지하는 것보다 현재 아키텍처와 일치한다.
- Defense DTO는 version, 정렬된 facility state 목록, typed enum/수치/접근그룹/허용 ID/growth/blocked reason을 저장한다. strict validator는 DTO version, canonical facility ID와 order/uniqueness, building/cell reference, enum/finite/range/nonnegative fields, flags, canonical allowed IDs/growth/text를 검증해야 한다.
- 기존 Defense debug suite에는 save section 자체의 strict round-trip/invalid preservation 케이스가 보이지 않는다. section 변환과 함께 공용 save registry 또는 해당 suite에 public invalid-preflight live-state preservation 증거를 추가해야 marker가 단순 선언에 그치지 않는다.
- `BuildingInstanceId`는 입력을 trim한 뒤 `building:` prefix만 확인하므로 validator는 `saved == typed.Value`까지 비교해야 공백 ID를 조용히 정규화하지 않는다.
- Defense growth는 여섯 개의 nonnegative integer level이고 state에는 runtime-only처럼 보이는 blocked text도 저장된다. 모든 valid payload를 그대로 보존하려면 growth null/default 합성과 blocked text trimming을 금지하고 null/비정규 값을 reject해야 한다.
- 기존 defense fixture는 실제 시설·물리 아이템·전력·이벤트를 조립하는 `DefenseScenarioWorld`를 이미 가진다. 새 저장 검증은 이 world의 실제 runtime/root를 재사용해 capture → strict section restore와 invalid preflight 무변경을 증명하는 방향이 적합하다.
- 실제 fixture에는 Defense runtime 생성자가 없고 composition에서만 VContainer가 조립한다. section 단위 검증은 작은 `IDefenseFacilityRuntime` fake로 strict preflight 호출 여부와 restore 무변경을 증명하고, 실제 runtime의 root replacement는 기존 gameplay suite + 별도 capture/restore 검증으로 보강한다.
- runtime `Clone`은 null allowed-ID list와 null growth/text를 각각 empty/default로 합성한다. validator가 valid payload에서 이 null을 전부 거부해야 clone이 값 손실 없이 작동한다.
- `DoorAccessGroup.All`은 bit 0..6의 유일한 허용 마스크다. `allowedGroups & ~All == 0` 검증이 필요하며 개별 allowed ID는 `CharacterId`가 prefix를 강제하지 않으므로 nonblank/canonical/sorted/unique 계약으로 제한한다.
- 공용 typed base는 section version만 exact 검사하므로 Defense validator가 DTO 내부 `version == CurrentVersion`도 별도로 강제해야 한다.
- `SetAllowed(persistentId)`는 trim/중복 제거 후 list 끝에 추가하므로 live 허용-ID 순서는 결정적이지 않다. Capture/Clone에서 ordinal sort하고 validator는 strict ascending order를 요구해야 exact JSON과 deterministic save가 함께 성립한다.
- Defense `Clone`은 capture와 restore 두 곳에서만 사용된다. allowed-ID 정렬을 clone에 넣으면 capture는 canonical해지고, validator가 이미 정렬된 payload만 허용하므로 valid restore 값은 변하지 않는다.

## 2026-08-03 defense-facility strict cutover result

- `defense.facilities`는 required typed section으로 전환되어 empty/null/legacy/default/missing payload를 더 이상 합성하지 않으며, strict validator를 통과한 DTO만 candidate Aggregate root에 교체한다.
- capture는 facility ID와 allowed-character ID를 ordinal canonical order로 기록한다. invalid condition, unordered IDs, unknown flags, null growth/list/text, malformed typed IDs는 commit 전에 실패한다.
- 기존 Defense fixture도 production의 필수 building identity 계약에 맞춰 deterministic `building:defense-fixture:*` ID를 initialization 전에 받는다.
- Unity MCP에서 Defense 전체 suite, 신규 canonical round-trip/invalid no-mutation fixture, V18 authority가 통과했고 Console Error 0 / Warning 0이다. non-rollback-free production section count는 36에서 35로 감소했다.
## 2026-08-03 faction strict restore audit

- Authored faction catalog is already sorted by stable ID and runtime `Factions` capture is sorted by faction ID. A strict payload can therefore require the faction list to exactly match authored definitions in count and order.
- Routes are created as `faction-route:{++routeSequence}` and appended in sequence order. Capture currently preserves this order, so validator can require canonical route IDs, strict ascending sequence, uniqueness, and `max(route sequence) <= routeSequence`.
- Current restore clamps day/sequence, seeds authored defaults, skips null/unknown factions, accepts null routes, and JSON-clone defaults malformed nested state. These are all lossy paths that must be rejected before restore; valid payload may then reuse the existing root replacement.
- Route payload includes faction/type/status/path/index/progress/delay/strength/days/flags/reinforcement actor IDs/cargo. Strict validation must cover canonical faction references, enums, nonempty path and index bounds, finite progress/delay, strength/range/day relations, sorted unique actor IDs, concrete authored item IDs and positive cargo amounts.
- `IFactionRuntime` already exposes authored `Definitions`, so validator can check exact faction coverage without adding a new runtime method. `FactionSaveSection` can inject the already-registered `IDungeonItemCatalogProvider` to reject unknown cargo item IDs.
- Save section은 production 외 직접 생성되는 곳이 없어 constructor에 item catalog를 추가해도 composition 외 호출 파손은 없다. Faction/Species/Defense expansion validator가 기존 authored faction asset 검증 entry이므로 strict save fixture를 그 suite에 추가하는 것이 적합하다.
- Runtime invariants show trust is bounded `[-100,100]`; betrayal/restitution/unpaid/death/equipment counters and embargo days are nonnegative. Home coordinates are legitimate signed hex coordinates.
- Route strength is `[0,100]` because ambush can reduce it to zero and mark the route Lost. Segment progress is finite `[0,1)`, delay is finite/nonnegative, path index must address a non-null nonempty path, created/ETA days are positive with ETA not earlier than creation.
- Reinforcement actor IDs are deterministically `{routeId}:ally:{index}` in insertion order. Validator can require canonical exact prefix, contiguous positive suffix order and uniqueness; cargo item lookup can use `EditorItemCatalogFactory` in the fixture and the injected production item catalog at runtime.

## 2026-08-03 faction strict cutover result

- `world.factions`는 required typed/rollback-free section이 되었고 authored faction 정의 전체를 exact order로 저장하지 않거나 route/day/sequence/nested state가 비정규이면 commit 전에 실패한다.
- route cargo는 injected `IDungeonItemCatalogProvider`의 concrete authored item만 허용하고, capture는 numeric route sequence로 결정 정렬된다. valid restore는 기존 candidate `FactionAggregateState` root 교체만 수행한다.
- Expansion fixture가 canonical round-trip과 reversed faction/unknown cargo invalid no-mutation을 검증한다.
- Unity MCP에서 species/faction/defense expansion 전체, 168 research 검사, V18 authority가 통과했고 Console Error 0 / Warning 0이다. non-rollback-free production section count는 35에서 34로 감소했다.
## 2026-08-03 world-resource restore audit

- `economy.world-resources` is a custom staged section that accepts blank/null JSON as a default DTO and has no preflight/marker.
- Runtime capture already sorts nodes by node ID and sources by work-type ID. Restore clones the Aggregate root but accepts null payload/list, skips unknown/null node/source/recipe references, and clamps completed work/remaining cycles.
- If resource nodes are not initialized, restore stores a cloned pending DTO in the candidate root; after initialization the same permissive `ApplyRestore` path projects it. Strict validation therefore needs both syntax/catalog checks and exact correspondence to the current or deterministically rebuildable node/source set before marker promotion.
- Aggregate clone retains Unity `WorldResourceNode`, Grid and wildlife patch references. Publication is still root replacement, but it shares existing scene objects rather than preparing new ones; marking rollback-free is safe only if commit cannot call scene mutation and later projection consumes a fully validated pending DTO.
- Public runtime exposes only node objects plus capture. Validating against `runtime.Capture()` would reject a legitimate incoming node set if restore occurs before deterministic world-resource initialization, while accepting unknown topology would preserve skip/clamp loss. This owner needs an explicit topology catalog/candidate-aware validator and is not the next low-risk marker conversion.
## 2026-08-03 crop-plot restore audit

- Crop plots have the same topology timing problem as world resources: custom section fabricates defaults, runtime clones an Aggregate containing live `BuildableObject` references, synchronizes against building world during restore, stores pending DTO, then skips/clamps nested values.
- Existing crop fixture even mutates `growthHours=999` and relies on runtime normalization/ticking, so strict conversion would require separating gameplay mutation tests from save-contract tests plus candidate building topology validation. It is not a marker-only candidate and is deferred behind simpler plain-state owners.
## 2026-08-03 grand-project restore audit

- `economy.grand-projects` custom section fabricates empty DTOs for blank/null JSON, but runtime state is a plain `GrandProjectAggregateState` and publication ultimately replaces one Aggregate root.
- Runtime restore silently drops unknown active/completed project IDs, clamps work, trims/defaults destination, normalizes no-active state, deduplicates completed IDs, and defaults null status/list/state. These can all be moved to a strict preflight.
- Runtime already exposes its six project definitions and Capture emits one state DTO. A validator can require exact DTO version, non-null state/list/text, canonical known project IDs, sorted unique completed IDs, active/completed exclusivity, canonical destination formula, finite work within the active definition's required work, and zero/empty inactive state.
- No Unity object needs to be created or mutated during restore; next-evaluation/version live in the candidate Aggregate and state replacement is plain. GrandProject is a suitable next rollback-free conversion.
- Active destination is canonically `grand-project:{activeProjectId}` (the project ID itself already begins with `grand-project:`); inactive state requires empty destination and zero work. Completion clears active state and appends a known project ID.
- Existing production-economy fixture constructs `GrandProjectSaveSection` directly and already proves a normal round-trip. It can be extended in place with marker/preflight/required checks and an invalid duplicate/negative-work payload no-mutation assertion without new composition dependencies.
- Completed project insertion order reflects gameplay completion order, so Capture must sort it ordinal before validator requires strict order; completed membership semantics are order-independent.
- Unity `Library/ScriptAssemblies` and Bee runtime DLLs have identical SHA and contain the new `GrandProjectSaveValidation` symbol, while the live AppDomain still reports the old GrandProject section base/interfaces. The remaining issue is assembly reload timing/locking, not source compilation.
## 2026-08-03 grand-project strict cutover result

- `economy.grand-projects` now uses the shared typed JSON preflight boundary and publishes only a validated plain Aggregate root. It is required, rollback-free, and not optional.
- Unity's loaded AppDomain confirms the new generic base and interfaces; the prior mismatch was caused by Editor fixture CS8121 errors, not by the runtime DLL.
- `ProductionEconomyDebugScenarios.RunAll()` and V18 authority validation pass. The non-rollback-free production section count is now 33, and Unity Console reports Error 0 / Warning 0.
## 2026-08-03 stock-policy / regional-contract restore audit start

- `economy.stock-policies` and `economy.regional-contracts` are separate custom staged sections over plain runtime contracts, both still outside rollback-free publication.
- Their DTOs live together in `ResourceEconomyPlanningModels.cs` at schema version 1. They are promising paired candidates because they have no obvious Unity-object ownership at the save-section boundary; exact runtime normalization and catalog-reference behavior still needs inspection before conversion.
- Both current sections synthesize empty/default DTOs for blank, null, or malformed payloads. Both runtimes replace a plain Aggregate root, but restore currently performs lossy normalization.
- Stock-policy restore skips null/unknown items, normalizes thresholds/status, overwrites duplicate item IDs, and then fabricates defaults for every catalog item. A strict contract should instead require exact authored item coverage in canonical order, unique concrete IDs, valid enum, nonnegative ordered thresholds, and non-null status.
- Regional-contract restore clamps day/sequence, skips malformed entries, then calls `EnsureOffers`, which can mutate the restored candidate by expiring or generating offers. Strict restore must validate a canonical complete snapshot and publish it without generating new content during restore.
- Stock-policy runtime state is entirely plain data. Capture emits `policyView`, whose refresh path is the natural place to confirm deterministic ordering; restore can safely replace only the Aggregate root once preflight guarantees exact catalog coverage and canonical values.
- The existing shared `DungeonJsonSaveSection<T>` plus a small domain validator matches the proven GrandProject cutover pattern and avoids duplicating JSON/default/version logic.
- `ResourceStockPolicyRuntime.RefreshView` currently sorts by localized display name, which is unsuitable for canonical persistence. The persisted view should be ordered by immutable item definition ID; presentation can apply its own localized sort.
- `IsKnownPolicyItem` still accepts `PhysicalItemIds.TryGetStockCategory`, retaining an abstract stock-category backdoor. Strict SO authority requires concrete catalog item IDs only.
- There are no direct constructor call sites for `ResourceStockPolicySaveSection`; production composition can inject the content catalog when the strict section gains that required dependency. The production-economy Editor fixture is the right place for an explicit fake-runtime boundary test.
## 2026-08-03 stock-policy strict cutover result

- `economy.stock-policies` is now a required typed/preflight/rollback-free section. Its payload must contain every authored resource item exactly once in immutable item-ID order.
- Restore no longer skips unknown/null entries, normalizes thresholds, fills missing definitions, or accepts abstract stock-category IDs. It replaces only the validated plain Aggregate root.
- The production-economy Unity fixture proves canonical full-catalog round trip and invalid-threshold no-mutation. V18 authority passes with 772 authored items and the non-rollback-free section count is 32; Console Error 0 / Warning 0.
## 2026-08-03 regional-contract strict design

- Contract IDs are generated as `contract:{offeredDay}:{sequence}`; accepted deliveries use `regional-contract:{contractId}`. These formulas provide a canonical validator without depending on display strings.
- Runtime history is capped at 24 and newly generated contracts are append-only by offered day/sequence. Capture should explicitly sort by parsed numeric ID parts, while restore should clone the validated list verbatim and must not call `EnsureOffers`.
- Valid snapshots require positive days/sequence/reward/requirements, `nextOfferDay >= currentDay`, unique canonical contract IDs, `nextSequence` greater than every saved sequence, concrete catalog item requirements, valid status, canonical text, and status-appropriate destination identity.
- `Offered` and `Declined` contracts have no destination; `Accepted`, `Delivering`, `Completed`, and `Failed` contracts may carry the canonical delivery destination because completion/failure does not clear that field.
## 2026-08-03 regional-contract strict cutover result

- `economy.regional-contracts` is now required, typed/preflight, and rollback-free. Restore publishes only the validated plain contract Aggregate and never generates/expires offers as a side effect.
- Contract snapshots are canonical by offered day and numeric sequence; IDs, schedule, reward, status-specific destination, text, history bound, next sequence, and one/two concrete authored item requirements are validated before publication.
- The production-economy Unity fixture proves canonical nested round trip and invalid-destination no-mutation. V18 authority passes and the non-rollback-free section count is 31; Console Error 0 / Warning 0.
## 2026-08-03 next rollback-free candidate triage

- Remaining nearby economy/recruitment owners include RegularCustomer (1,057-line runtime), FacilityShop (1,086-line runtime), and TreasuryEconomy. The first two likely couple to authored/customer or facility presentation state and need broader audits.
- TreasuryEconomy has a small dedicated save boundary and is the next low-risk candidate to inspect before taking on the larger customer/shop owners.
- TreasuryEconomy is not actually a small semantic owner: one section aggregates ledger, employment, procurement, paid facilities, overclock, and treasury defense through six runtimes. Each `PopulateRestoreState` currently has its own normalization/reference rules, so it needs a composite validator rather than a marker-only conversion.
## 2026-08-03 regular-customer strict audit

- The section already uses the shared typed JSON base and runtime state replacement is a plain Aggregate; it is missing rollback-free marking and strict canonical validation.
- Restore constructs `RegularCustomerRecord`, whose constructor clamps satisfaction/visit count, fills blank display/species text, promotes boolean status hierarchy, and converts `RecruitCapability.None` to `All`. Preflight must reject every payload that would trigger those normalizations.
- Capture ordering currently uses default string comparison and must be changed to `StringComparer.Ordinal`. Payload IDs must be canonical, unique, and strictly ordered.
- Existing recruitment fixture has no save-boundary coverage, but it already creates/destroys a real `RegularCustomerRuntime` safely. A small fake `IRunCharacterCatalog` can reuse authored/test `CharacterSO` references without adding production dependencies.
- `RegularCustomerSaveSection` has no direct constructor call sites outside composition, so adding a marker and DTO version does not break manual production construction.
## 2026-08-03 regular-customer strict cutover result

- `recruitment.regular-customers` now carries an exact V1 DTO version and is typed/preflight/rollback-free. IDs are ordinal-canonical and source definitions, statistics, display data, recruitment hierarchy, and capabilities are validated before Aggregate replacement.
- Restore no longer skips missing records or relies on the record constructor to clamp/default/promote state. CharacterSO references remain immutable authored definitions; no scene actor is created or mutated during publication.
- Full RegularCustomer gameplay scenarios plus real-runtime canonical/invalid save coverage pass in Unity. V18 authority passes, non-rollback-free count is 30, and Console Error 0 / Warning 0.
## 2026-08-03 facility-shop strict audit

- FacilityShop already owns one plain Aggregate of offer day plus two ID sets. Capture sorts both sets and the V18 validator already prevents duplicating research unlock authority.
- Current payload validation accepts null lists and unordered IDs, while `RestoreState` clamps the day, drops negative IDs, and defaults null collections. Strict preflight can make those normalization branches unreachable.
- During transactional restore, `RestoreState` detects `aggregateRootStore.IsRestoreStaging` and suppresses offer projection. Projection is rebuilt only after `PublishedRestoreRevision` changes in `Update`, so the section can be rollback-free once payload validation is strict.
- The existing fixture already proves a discarded late candidate leaves the live shop untouched. Marking both the shop section and its throw-before-mutation failure fixture rollback-free turns this into direct all-marker discard-path evidence.
- The all-marker late-failure path correctly leaves `PublishedRestoreRevision` at 0. The prior non-marker rollback-image fixture expected revision 1 because rollback replay itself published a replacement; changing the expectation to 0 proves the live root was never replaced.
## 2026-08-03 facility-shop strict cutover result

- `facility-shop.state` now has exact V1 data and a required typed/preflight/rollback-free boundary. Unlock lists must be non-null, authored, unique, and strictly ascending.
- Restore no longer clamps the offer day or filters/defaults unlock IDs. During staging it replaces only the detached Aggregate; daily offer presentation is projected after a published revision.
- Full FacilityShop gameplay tests, invalid no-mutation, and all-marker late failure pass. The failed candidate leaves live state and published revision untouched at revision 0. V18 authority passes, non-rollback-free count is 29, and Console Error 0 / Warning 0.
## 2026-08-03 remaining-owner triage after facility shop

- Unity TypeCache confirms 29 production save sections still lack `IDungeonRollbackFreeSaveSection`. The list spans plain progression/pacing state, character substate, infrastructure networks, production, combat/offense, and world-topology owners.
- The next audit should prioritize ExperiencePacing, StaffDiscontent, ExternalInfluence, and DungeonDebug as likely plain-state boundaries, while deferring topology-heavy CropPlot/WorldResource and composite Treasury until their validators are explicit.
- ExperiencePacing and ExternalInfluence are still optional, version-migrating sections with missing-state reset semantics; they require explicit V18 compatibility decisions and are not marker-only candidates.
- StaffDiscontent already captures deterministic records through the typed JSON base and restores snapshots, but validation is embedded in restore and still trims/skips/defaults records. It is the strongest next strict Aggregate candidate.
- DungeonDebug synthesizes default payloads and directly restores presentation/debug service state. It is small but should be handled after deciding whether debug state is a required production save section at all.
## 2026-08-03 staff-discontent strict audit

- `StaffDiscontentRuntime.RestoreSnapshots` constructs a fresh plain `StaffDiscontentState` and replaces the Aggregate root; it does not mutate CharacterActor or presentation during restore.
- Current section has no `ValidatePayload`. Restore skips null/blank records, trims IDs, detects duplicates only during restore, and constructs snapshots that clamp mood/low-mood days. `FromSnapshot` additionally fills blank names and normalizes values.
- Strict preflight must make all of those transformations unreachable before adding rollback-free publication.
## 2026-08-03 Batch B remaining aggregate audit

- `ICharacterEnvironmentRuntime`, `ICharacterSpeciesRuntime`, and `IAnimalHusbandryRuntime` are wrapper aggregates over already-existing query/command/persistence contracts. Their remaining work is consumer migration plus deletion of the wrapper exposure, not a new state model.
- `ICharacterConsumablesRuntime` still combines diet policy, meal consumption, substance use, and persistence. Its result DTOs retain duplicate sentence-bearing `FailureReason` fields despite having failure codes; this boundary needs typed parameters and presentation-only localization before aggregate removal.
- `ISurvivalFoodRuntime` is the broadest remaining Batch B contract: persistence, UI queries, work commands, stock consumption, and debug mutation are all on one interface. It requires a deliberate query/command/persistence/debug split rather than a mechanical rename.
- Current concrete consumers confirm the split boundaries: save sections require persistence only; character/UI/operations panels require queries; work/building/AI flows require commands or targeted queries; debug command providers alone need debug mutation.
- `MealConsumptionResult` and `SubstanceUseResult` already carry `CharacterConsumablesFailureCode`, but duplicate localized/domain sentences through `FailureReason`. Facility, Shop, and `AbilityUseSubstance` read that sentence directly. The safe cutover is to replace it with typed parameters, return stable code names to non-UI diagnostics where unavoidable, and localize only at presentation boundaries.
- `CharacterSubstanceUseRequest.Reason` is command context rather than a failure result; it should be audited separately and not mechanically removed with result failures.
- A repository search included a nonexistent `Assets/Scripts/Services/Shop` path and returned exit 1; the actual shop consumer is `Assets/Scripts/Services/Buildings/Shop.cs` and was still found through the other roots.
- Batch B now has all six required named contract assemblies present (`DungeonStory.Characters`, `.Work`, `.Survival`, `.Medical`, `.Species`, `.Combat`). The engine-independent authored/runtime DTO seams therefore exist without forcing the Unity adapters themselves into a reverse dependency.
- Each of the seven Batch B owner sections is current-version, preflighted/staged through either the shared typed JSON section or an explicit candidate, and implements `IDungeonRollbackFreeSaveSection`: AnimalHusbandry, CharacterBodyHealth, CharacterConsumables, CharacterEnvironment, SpeciesRuntime, SurvivalResources, and DarkSurvival.

## 2026-08-03 Batch B broad-authority closeout audit

- Four broad runtime authorities remain: EnvironmentalField, EnvironmentalWorkwear, Surgery, and CharacterMedical. Surgery and CharacterMedical are assigned to independent workers; EnvironmentalField is explicitly Batch C-owned, while the main thread owns EnvironmentalWorkwear completion.
- `IEnvironmentalWorkwearRuntime` mixes equipped/stock queries, equip/auto-equip/unequip commands, and Capture/Restore/Reset persistence. The concrete runtime also permits a null research service, and WorkTaskExecutor/AbilityWork consume the broad port. The cutover must expose Query/Command/Persistence separately, make research mandatory, and preserve the shared CharacterEnvironment aggregate state.
- Workwear persistence is nested in the CharacterEnvironment aggregate rather than owning an independent save section. Its narrow persistence facet therefore prepares and validates an equipped-item map; CharacterEnvironment adds that map to the detached aggregate candidate and publishes once. This avoids a second direct workwear restore authority.
- WorkTaskExecutor does not need a query facet: it can issue `TryUnequip` and treat the typed `EnvironmentWorkwearNotEquipped` result as an idempotent no-op. This keeps its dependency count unchanged while maintaining strict Query/Command separation.
- The authoritative Batch B integration entry point is `BatchBCharacterSurvivalAuthorityDebugScenarios.RunAll()`. It executes survival/deprivation/dark-survival/species-husbandry/environment/wildlife/combat/save fixtures, verifies all seven strict save boundaries, enforces 39/54 rollback-free, checks removed wrappers and narrow facets, then runs V18 plus architecture validators.
- The required UI acceptance entry point is `CharacterSummaryMedicalUiMatrixPlayModeVerifier.RequestRunFromMenu()`, which owns the two-resolution EventSystem workflow and writes `Artifacts/QA/CharacterSummaryMedical/ui-matrix-report.txt`. `DarkSurvivalPlayModeVerifier.RequestRunFromMenu()` remains the separate dark-survival world/health pointer capture gate.

## 2026-08-03 Batch C seven-owner audit

- The exact seven remaining Batch C save sections are Power V1, Fluid V3, Conveyor V2, Automation V1, ProductionBills V4, WasteProcessing V1, and EnvironmentalField V1. Converting all seven from their current optional/staged or staged-only forms to strict required detached rollback-free boundaries moves the production total from 39/54 to exactly 46/54, leaving Batch D's eight.
- Broad authorities remain in `IElectricalNetworkRuntime`, `IWaterNetworkRuntime`, `IConveyorRuntime`/inherited command service, `IAutomationRuntime`, `IProductionBillRuntime`, `IWasteProcessingRuntime`, and `IEnvironmentalFieldRuntime`. Their results also retain raw messages/status sentences and several semantic null fallbacks.
- Conveyor currently removes a physical stack from the repository and copies the complete `WorldItemStackSaveData` into its own payload. The required cutover keeps the stack in the physical repository, adds an in-transit state, stores only `ItemStackId` plus segment/progress in the conveyor aggregate, and routes movement through `IItemTransferService`.
- Production bills currently key facilities with definition numeric ID plus coordinates, and EnvironmentalField thermostats use coordinates as owner keys. Batch C must use `BuildingInstanceId`; field cells may remain coordinate-indexed because the cell itself is spatial state.
- EnvironmentalField restore mutates live initialization state before validating the payload. It therefore needs a real detached `EnvironmentalFieldAggregateState` candidate and one final replacement, not merely a rollback-free marker or interface rename.
- Content-graph validation currently counts code-generated generic consumers for food, finished goods, fuel, and feed as real branches. Actual authored building supply profiles, recipes, equipment, surgery, construction, and gameplay item features must provide the reverse index; generic virtual consumer aliases cannot satisfy branch counts.
- All seven Batch C runtime/save/presenter surfaces remain in default Assembly-CSharp. Current largest sources are ProductionBillRuntime 1,180, ConveyorRuntime 1,178, FluidNetworkRuntime 1,126, and EnvironmentalFieldRuntime 870, so responsibility extraction must accompany the authority split instead of staying just below the limit.
- EnvironmentalField cannot claim rollback-free restore by adding a marker alone: its cells, topology, caches, and thermostat configuration must first form a detached Aggregate candidate and publish by one root replacement.
- Survival food cannot constructor-inject the field query directly because the field already depends on `ISurvivalEnvironmentQuery`; the startup bridge is the explicit cycle-breaking adapter. Its pre-start state can be an explicit `NoEnvironmentalFieldQuery`, eliminating semantic null DI while preserving weather fallback until the physical field initializes.
- Topology rebuild must delete thermostat overrides whose `BuildingInstanceId` no longer owns a configurable thermal emitter; otherwise a destroyed facility creates a save payload that strict restore correctly rejects. Pruning the derived stale owner at rebuild keeps capture self-consistent.
- The legacy `BuildingTemperatureAbility -> new BuildingThermalEmitterAbility` runtime conversion was another hidden content authority. Removing it makes authored `BuildingThermalEmitterAbility` modules the only physical-field source while Editor builders remain the explicit authoring path.

## 2026-08-04 authored production-consumer indexing

- `ResourceEconomyContentCatalog` was still manufacturing generic `service:*`, `commerce:*`, and generic kiln/boiler/incinerator/animal-pen links solely from item kind or tags. Those links were not backed by a recipe, equipment definition, substance definition, medical procedure, or authored facility supply profile, so they could falsely satisfy the two-branch intermediate-material rule.
- The reverse index now derives substance consumers from `SubstanceDefinitionSO`, ammunition consumers from each `CombatWeaponSO.AmmunitionItemId`, and fuel/feed consumers from each authored `BuildingFacilitySupplyAbility` profile that actually accepts the item. The generic built-in consumer generator has been removed.
- Food and finished-good consumption still require explicit authored consumer definitions before the full content graph can become a mandatory bootstrap gate; the honest index is expected to expose those missing authorities rather than silently treating aliases as gameplay branches.
- A second authority remains for substances: `SubstanceDefinitionSO` duplicates the same item ID, classification, addiction/overdose/tolerance/withdrawal/effect/duration data already authored in `SubstanceItemFeature`. `CharacterConsumablesRuntime` correctly reads the item feature, while `IResourceEconomyCatalog`, CharacterSummary, and ItemPile UI still read the parallel SO. The final cutover must project substance views from `ItemDefinitionSO` and delete the parallel definition/catalog path.
- Construction is not yet part of the concrete item dependency graph. `BuildingWorkAmountAbility` still authors one `StockCategory` plus an amount and can synthesize a General-category cost from money; `GetConstructionMaterials()` returns `Dictionary<StockCategory,int>`. Work execution and construction UI consume that abstract category. This must become an authored `IReadOnlyList<ItemAmountDefinition>` per building ability, be indexed as `ConstructionMaterial`, and reject missing concrete requirements rather than falling back to General stock.
- Terminal item use can be indexed without virtual service aliases when the capability itself is authored on the item SO: `FoodItemFeature` yields a character-consumption link, injury-capable `MedicineItemFeature` yields a treatment link, `InstallationItemFeature` points to its concrete building definition, and `BlueprintItemFeature` points to its concrete research target. Marketability was deliberately not counted as a production branch because commerce is not one of the approved direct-consumer edge kinds.

## 2026-08-04 concrete construction-material authority

- The construction worker migrated all 293 `BuildingSO` assets to explicit `ItemAmountDefinition` requirements: 101 legacy abilities were converted and 192 missing abilities were authored. Category/money-cost fallbacks, abstract save fields, and category reservations/consumption are gone; WorkOrder V3 and the construction UI use concrete item IDs.
- The dependency graph now indexes each `construction:{buildingId}` as an actual `ConstructionMaterial` consumer. Graph validation rejects a building with no work-amount authority, no material, a non-positive/blank/abstract/unknown material, or duplicate material IDs.
- The first honest Unity graph run after adding construction links failed with 70 findings: 9 loaded building definitions still lacked material authority and 61 produced items had no indexed real consumer. This is not a validator false positive: it exposes both a construction migration loading-scope gap and missing reverse-index/content links for ammunition variants, facility installation components, medical procedure parts, workwear, husbandry supplies, defense consumables, lineage/offense rewards, food/medicine, and other finished goods.
- After indexing only existing real systems (workwear definitions, explicit market-sale features, effective medicine use, and lineage transfer), the graph fell from 70 to 36 findings. Fixing all 343 cataloged buildings, authoring twilight beer/night spirit as actual `SubstanceItemFeature` consumables, and using fermented vinegar in two preserved-food recipes reduced it again to 32; the remaining list is exactly two defense ammunition items plus 30 research-overhaul facility/equipment/medical/agriculture components now assigned to parallel content work.

## 2026-08-04 production consumer closure

- The final 32 gaps required eight distinct authoritative paths rather than more recipes: equipment component inputs, concrete building construction materials, surgical procedure materials, crop-cycle supplies, stock-sensor installation, equipment-maintenance supplies, physical defense supplies, and expedition tools. These are now read directly by the systems that deliver and consume the physical item.
- Ammunition reverse indexing must enumerate `CombatWeaponSO.CompatibleAmmunitionItemIds`; indexing only the preferred legacy `AmmunitionItemId` hides steel/arcane arrows and bolt variants. The graph and runtime catalog now enumerate the complete typed list.
- Stock sensor panels, maintenance kits, fertilizer/substrate, toxic trap coating, and expedition field tools are not ordinary recipe inputs. Counting their real consumers therefore requires indexing their authored capability fields, while their runtime owners must independently prove delivery and consumption.
- The honest branched-production validator now passes after closing the 32-item backlog. No `sink:*` recipe or item-kind-derived generic service alias was added.
- `ItemTransferService` no longer exceeds the 1,200-line runtime limit. Facility-buffer transaction validation and mutation moved to a focused helper, and destination-priority ownership moved to the warehouse service without introducing a second inventory authority.
- The environmental solver's apparent 28ms p95 was not merely a noisy threshold: each access to `temperature`, `air`, `light`, barrier, door, duct, and exterior properties re-entered the scoped Aggregate root. Caching those array references once per fixed tick brought the loaded measurement to 17.648ms and makes the performance contract substantially less sensitive to preceding integration tests.
- Work-order Editor scenarios must not use `UnityGameClock` when their contract is unrelated to pause. A prior production scenario can leave `Time.timeScale == 0`, preventing orphan cleanup from ticking and producing an order-dependent false failure. Fixed game/UI clocks make the construction recovery proof isolated and deterministic.
## 2026-08-04 Batch B character/medical checklist reconciliation

- The Phase 112 wording was stale for three medical items: `ICharacterBodyHealthRuntime` has zero source references, `CharacterBodyHealthRuntime` implements separate Query/Command/Persistence facets, and its save section consumes only `ICharacterBodyHealthPersistence`.
- Surgery and character-medical orders no longer persist presentation sentences. `SurgeryOrder.statusData` is `SurgeryStatusData`; `CharacterMedicalOrder.statusCode` is `CharacterMedicalStatusCode`; no legacy `status`, `FailureReason`, or `out string failureReason` field remains in those contracts.
- The only literal `out string` left in `CharacterSurgeryWindow.cs` was a carcass-species parser result, not a domain failure. A value-returning parser now removes that UI boundary leak while preserving the existing `Try...` compatibility API for non-UI callers.
- Static String Table reconciliation found 280 distinct required keys across `FailureCode`, consumables failures, surgery statuses, surgery risk summaries, and character-medical statuses; shared data, Korean, and English tables each have zero missing keys.
- The genuine remaining Batch B structural item is concrete Unity-adapter assembly ownership. Pure Character/Work/Survival/Medical contracts and SO models are in named assemblies, but service adapters still depend on default-assembly types such as `CharacterActor`, `BuildableObject`, Grid/world-item ports, and VContainer composition. Moving those adapters safely is a broader default-assembly cutover, not a missing medical facet.

## 2026-08-04 production UI evidence and remaining routing gap

- A functional pointer PASS was not sufficient visual evidence: the first captures rendered only two of three route rows, overlaid the route panel on the building body, clipped portrait chrome, and wrote mojibake to the report. The acceptance gate now checks the building root, context panel, fixed controls, ScrollRect-visible controls, all route rows, and the third route row against screen bounds before capture.
- The accepted layout shows all three route policies at both required resolutions and hides the legacy building tabs/demolition control while the context action is active. Report writing uses BOM-bearing UTF-8 and re-reads Korean sentinel phrases before declaring PASS.
- `ProductionConsumerRoutePolicy` is authored, editable, and saved, but the current runtime search shows `ProductionRouteDistribution.SelectNext` is exercised only by the debug scenario. `ProductionConsumerRouteState.currentDemand/reservedQuantity/blockedReason` is not yet projected by a live routing query, and the UI rows currently show policy values rather than the requested live demand/reservation/block reason. Batch C therefore still needs an actual downstream route-state/query/dispatch implementation even though the UI pointer matrix itself is accepted.

## 2026-08-04 save-root and architecture-ratchet findings

- Seven of the final eight Batch D save sections are now strict detached/rollback-free in source. Raising the global validator from `46/8` to `54/0` before OffenseAggregate finishes would be false evidence, so the ratchet remains unchanged until the final section and the complete loaded type graph pass together.
- `DungeonSaveSectionRegistry.RestoreAll` previously called `CaptureAll()` unconditionally after every successful stage build, even for an all-marker registry. This defeated the intended rollback-free cost model. The capture is now conditional on a legacy section being present, and the failed aggregate-candidate regression asserts that neither rollback-free section is recaptured.
- The original `largeConstructor` metric treated DTO/value constructors as dependency injection and reported 90 violations, including requests, snapshots, and result values. Applying the requirement to operational owners only produces 32 actionable DI constructors; these remain real Batch E work and are not waived.
- The current Roslyn baseline verification is expected to fail while concurrent source migrations change the exact oversized/default-assembly sets. No baseline write is permitted until the active Offense, production-routing, and CoreSession-asmdef slices finish and their exact diffs are reviewed.

## 2026-08-04 all-marker save and live production source closure

- `OffenseAggregateSaveSection` is now the eighth and final Batch D strict boundary. It prepares expedition, world, region, battle, mitigation, preparation, field-medical, travel, decision, and return-arrival candidates before publication; its constructor is reduced from 13 dependencies to 5. Canonical, invalid-no-mutation, and late-discard Editor scenarios exist, but Unity-loaded execution is still required.
- The V18 source ratchet now requires all 54 production sections to implement `IDungeonRollbackFreeSaveSection`; the approved remaining set is empty. `DungeonSaveSectionRegistry` does not call `CaptureAll()` on an all-marker restore path. This remains source evidence until Unity TypeCache and the full restore fixture pass.
- The previously missing production seam is implemented in source. Four live demand providers feed route state, the physical output-buffer path invokes `SelectNext`, and fallback ordering plus fairness policies are connected. The merged Unity scenario remains the acceptance gate.
- Current Roslyn metrics are `1126 files / 3499 types / 0 mutable statics / 13 oversized types / 28 large constructors / 1058 default-assembly sources / 0 content escapes / 0 direct session mutations / 6504 raw Korean literals / 4 root catalog references`. This is not an accepted baseline; the 13/28/1058/6504 residuals remain open work.
- The top-level `DataManager` and `IDataScriptableObjectSource` were a second in-memory content authority even though their input already came from `IGameContentCatalog`. They are removed. The remaining numeric compatibility API is a read-only, rebuildable `GameContentDataCatalog` projection over the root SO catalog; a V18 source ratchet prevents the deleted cache/source types from returning. Typed stable-ID domain catalogs are still the target for retiring the numeric compatibility surface itself.

## 2026-08-04 merged authority audit findings

- Unity's first merged run used a DLL compiled just before the final save-ratchet source edits, so it reported the obsolete 46/8 constants even though the current validator source and TypeCache contract are 54/0. A fresh compile after all active file edits is required before treating those messages as implementation evidence.
- The audit still found a genuine atomicity violation: `OffenseAggregateSaveSection.CommitRestore` called `returnArrivals.Restore`, mutating the live return-arrival root before `PublishRestoreCandidate`. The return-arrival runtime now exposes `PrepareRestore` and non-failing `PublishRestore`, and the offense candidate owns that fourth detached candidate alongside expedition, region, and world.
- `SurvivalDebugScenarios` encoded an obsolete exact count of seven substances. The SO catalog now intentionally contains nine after twilight beer and night spirit became real physical substance items; the test now requires at least nine unique stable substance IDs and retains class-specific addiction invariants.
- Runtime class size must be measured per Roslyn type, not by total lines in a source file that can contain several small context classes. The duplicate file-length baseline gate contradicted the explicit 1,200/800 type contract and has been retired in favor of `BatchAArchitectureMetricsValidator`.

## 2026-08-04 responsibility and serialization findings

- Meaningful state/behavior collaborators can close the type-size gate without partial-class shuffling: the owner keeps serialized aggregate authority while per-entity occupancy, assignment, presentation, natural-condition, launch, decision, and execution policies move to plain C# collaborators. Current oversized and large-constructor sets are both empty.
- Unity Console compiler diagnostics can be returned as entries whose MCP `Type` is `Log`; filtering only `Error`/`Warning` can therefore produce a false clean result. Consolidated verification must inspect all Console entries for compiler `error CS` and `warning CS` messages after each assembly reload.
- Unity `JsonUtility` does not preserve null for an optional nested serializable class in the offense save DTO. An explicit presence bit is required; relying on `null` or accepting an empty object would either reject valid no-battle saves or permit hidden malformed state.
- New collaborator source files increased the default-assembly file counter while improving type responsibility. The architecture baseline must not be raised to bless that regression; actual named-assembly migration is required before the V18 authority gate can pass.
- Exact set hashes are useful only while a ratchet count is unchanged. Requiring an obsolete hash after a strict count reduction makes successful cleanup fail; both Roslyn and Unity architecture gates now accept lower counts and retain identity review for equal-count churn.
- Catalyst IDs had conflated 21 progression steps with a potency feature whose authored contract is 1-5. Treating the suffix as potency invalidated 128 authored SOs and leaked progression into effect/save fields. The corrected model keeps progression in identity/unlock rules and stores only derived grade in effect/save state.
- A source-path assertion in the V18 validator became stale when `DoorAccessSubjectAggregateState` moved to `DungeonStory.Buildings`. Named-assembly moves must update source-contract paths in the same change; otherwise a correct runtime type graph can still fail static authority validation.
## 2026-08-04 Unity MCP command-vs-project compilation boundary

- `Unity_RunCommand` reporting `isCompilationSuccessful=true` proves only that the dynamic command compiled against the currently loaded assemblies. It does not prove that the project source refresh which follows the command produced a new `Assembly-CSharp.dll`.
- A Captivity/Circus command completed all ten scenarios while the Console simultaneously contained fresh project compiler diagnostics from the active FacilityShop/Automation migrations. Therefore every acceptance run must additionally wait for `IsCompiling=false` and inspect all Console entry types for `error CS` and `warning CS`; command success alone is not acceptance evidence.
- The scenario result remains useful behavioral evidence for the loaded assembly, but it must be rerun after the source revision reaches Console Error 0 / Warning 0 before the batch is accepted.

## 2026-08-04 fresh-assembly recovery and V18 gap audit

- `OperatingDaySettlementRuntime.cs` contained a literal tool-output truncation marker. The source was rebuilt from the last known-good compiled type, malformed strings were replaced with ASCII Unicode escapes, ambiguous decompiler `Object` references were explicitly aliased to `UnityEngine.Object`, and duplicate declarations remain zero.
- An apparent zero-error checkpoint was rejected because 28 source files were newer than the loaded DLLs. The authoritative retry waited for domain reload and proved both `Assembly-CSharp` DLLs newer than every relevant source; source-newer counts are now zero.
- The fresh focused run passes Blueprint Research, Research Tree, the 168-node Research/Equipment overhaul, Branched Production, Facility Evolution, and Survival. Approved pacing remains `32.2/80.4/234.3/372.0` days.
- Architecture metrics were regenerated and pass at `1296 files / 4042 types / 0 mutable statics / 0 oversized types / 0 large constructors / 885 default-assembly files / 0 content escapes / 0 direct session mutations / 6505 raw Korean strings / 3 root catalogs`.
- Save-boundary audit found one formal Offense aggregate section but several public direct restore bypasses. `OffenseWorldStateSaveCodec.Restore`, `IOffenseSaveService.Restore`, and public subsystem restores sit outside the detached prepare/publish transaction and must be removed or internalized.
- Thirty-two sections still inherit the legacy one-parameter `DungeonJsonSaveSection<T>`, whose fallible candidate construction can occur at commit time. Operating Day is the first strict-candidate conversion; the other 31 require individual proof rather than marker-only upgrades.
- Runtime `ScriptableObject.CreateInstance`, `Resources.LoadAll`, `GetDefinitionOrDefault`, and `FromStockCategory` content fallbacks are zero. A representative-item authority remains in `CanonicalStockItemIds` and `DungeonItemCatalog.GetDefinition(StockCategory)` with eight production caller groups; that mapping is now a dedicated removal batch.
- The leaked `Character Model Scenario Character` fixture was confirmed and removed through Unity MCP, then the scene was saved and verified clean with zero matching objects.
- The saved GameplayScene currently contains 16 embedded `MonoScript` blocks, not the earlier estimate of 10: FacilityEvolutionStateComponent, CharacterSkillTransientState x2, FacilitySynthesisRuntime, OffenseRewardRuntime, CharacterActorPresentationBridge x3, StaffDiscontentRuntime, OperatingDaySettlementRuntime x3, DailyFacilityShopRuntime, BlueprintResearchRuntime, CodexRuntime, and RegularCustomerRuntime. These require Unity-side script rebinding/removal; text deletion is not accepted evidence.
- Unity's authored-script lookup resolves exactly one project MonoScript asset for eight embedded component types. `CharacterSkillTransientState` and `FacilityEvolutionStateComponent` resolve zero because their MonoBehaviour declarations still live in files named `CharacterSkillRuntimeEffects.cs` and `FacilityEvolutionState.cs`; they need class-name-matching script assets before all 16 embedded MonoScript objects can be rebound safely.
- After the authored Codex component was added, `DungeonRuntimeSystems` still exposes two Unity fake-null component slots at indices 12 and 21 even though `GameObjectUtility.GetMonoBehavioursWithMissingScriptCount` returns 0. Slot 21 corresponds to the old embedded Codex component; slot 12 is a second invalid component that must be identified before removing only the broken serialized entries.
- Fake-null slot 12 is `OffenseExpeditionRuntime`. Its script GUID points to `OffenseExpeditionSystem.cs`, while the MonoBehaviour class is named `OffenseExpeditionRuntime`; this also explains the earlier `ExtensionOfNativeClass` regression. The file identity must be renamed while preserving GUID `d577ed6425ec47ed8e60f245ce07336a` before removing/replacing serialized null slots.
- A menu-style regression that only logs failures can appear as PASS to an outer command unless its Console side effect is inspected. `CombatEquipmentMaterialDebugScenarios` exposed this: the wrapper returned normally while two cases logged an error. The fixture now uses the same explicit `BuildableObject` dependency composition as runtime, and the scenario's own success log is part of the accepted evidence.
- The current semantic asmdef graph has no leaf candidates; useful progress requires cutting a small cyclic boundary rather than waiting for a naturally movable file. Moving presentation ports/policy IDs and pure restore validation rules reduced cyclic SCCs 18 -> 16 without creating named-to-default backreferences.
- A generic Foundation extraction can reveal the stricter 800-line MonoBehaviour gate even when the overall runtime limit is 1,200. `CharacterStats` briefly measured 809 lines; extracting condition-penalty projection restored oversized type findings to zero instead of raising the baseline.
- Small cyclic SCCs were mostly concrete reverse references or partial-class ownership leaks, not reasons to move whole feature trees at once. Narrow command/query ports, callback bundles, pure restore policies, and cohesive context objects reduced 18 cyclic SCCs to one without adding named-to-default dependencies.
- Partial files are not automatically harmless organization. `DefenseEngagementRuntime.Restore.cs` formed a two-file semantic cycle despite containing only three public methods. Folding its public restore facade into the single runtime declaration and moving cell-reservation calculation to the existing intercept planner kept the runtime under 1,200 lines and removed the cycle.
- UI gestures should depend on an interaction sink, not their concrete window coordinator. `ResearchTreePanSurface` and `ResearchQueueRowDrag` now target `IResearchTreeInteractionSink`, which makes the interaction, viewport, and window files acyclic without changing pointer behavior.
- Integration commands must not run while a worker is between a source move and its `.meta` move. Unity correctly warned about `InvasionIntruderContentBinding.cs.meta` during that transient state; the original GUID was restored, but the checkpoint was rejected and scheduled for a stable rerun.
## 2026-08-04 Phase 116 integration findings

- A clean Editor.log tail is not sufficient when the loaded Assembly-CSharp DLL predates the latest source. The current relay disconnect therefore blocks acceptance only for the newest batch, not further source-level SCC decomposition.
- `SurvivalEnvironmentalFieldBridge` can live in Infrastructure when Survival owns only a narrow environmental sink contract; this removes the bridge from the default assembly without making Survival depend on Infrastructure.
- `WorkTargetCandidate` can move to the Work assembly when its persisted/domain-facing building reference is `IBuildingWorldEntryPort`; the sole `BuildableObject` cast belongs in the default runtime adapter rather than the candidate value itself.
- The latest authoritative source checkpoint is default assembly 856 and giant SCC 508 with all hard architecture violation counters at zero. Bee response files and generated csproj data are stale until Unity completes a fresh import.

## 2026-08-04 cohesive cluster findings

- The new settled checkpoint is default runtime 852 and giant SCC 504. Further progress requires moving the contract and its concrete adapter together; keeping a one-file cap would preserve the wrong architecture.
- Source-path ratchets can silently stop checking a moved implementation when they use optional `File.Exists` reads. The Blueprint research save check did exactly this until its path was updated to Infrastructure.
- `GameplayArchitectureRatchetTests.SourceBySuffix` had 16 unresolved calls across moved or split sources. A mechanical path audit is now part of each migration checkpoint; all 280 calls currently resolve uniquely.
- The Unity relay server listens on the configured ports but the editor has no live WebSocket connection after domain reload. Relay-only restart did not help, while `Temp/__Backupscenes/0.backup` is 59 minutes newer than the saved GameplayScene; editor restart is not an acceptable recovery action without first preserving that state.
- Survival deprivation diagnostics and their snapshot form one cohesive named-domain cluster. Moving the diagnostics class alone is invalid because a named assembly cannot reference the snapshot while it remains default-owned.

## 2026-08-04 first cluster barrier findings

- Cohesive clusters reduce the semantic cycle faster than file-count alone: default runtime ownership moved 852 -> 839 while the giant SCC moved 504 -> 490 in one barrier.
- Generic named contracts are an effective compile-time boundary when a default implementation must remain closed over `CharacterSO` or `CharacterActor`; the closed alias stays at composition/runtime edges without reflection, `object`, or service location.
- Moving immutable DTOs and snapshots alongside their aggregate preserves restore semantics while allowing staging stores and scene application adapters to remain outside the named domain.
- A named/global namespace move must audit type namespace imports separately from asmdef references. The warehouse cluster had a valid assembly edge but still needed an explicit namespace import for `ShopSaleItemDefinition`.
- The central planner barrier must be scheduled after all source lanes stop editing; otherwise changing input hashes prevent meaningful determinism evidence even when every individual change is valid.

## 2026-08-04 second cohesive-cluster findings

- Several apparent Character-to-default dependencies were broader than the rule required. `ResourceCharacterSpeciesCatalog` only needs `IGameContentDefinitionSource`; path-search and idle-wander rules only need grid position plus `GridTraversalContext`; movement-facing policy only needs a two-method character capability. Reusing these existing/narrow contracts removes edges without introducing a second catalog, state store, or fallback.
- Unity can regenerate Bee response files while the project-scoped MCP transport remains disconnected. Response-file freshness is useful compile input evidence, but it is not proof that the Editor accepted the assemblies or that serialized SO references survived import.
- For focused Unity Roslyn verification on Windows, passing the full response-file argument set directly exceeds the command-line limit. Generate a derived response file under `Library` and invoke `csc.dll` through Unity's bundled `NetCoreRuntime`; never overwrite Bee's real output DLLs.
- PowerShell's automatic `$args` variable must not be repurposed as a mutable compiler-argument list. Use a task-specific name such as `$compilerArgs`.

## 2026-08-04 third cohesive-cluster findings

- A leaf adapter can still hide a default-assembly dependency behind extension-method syntax. `BuildingConnectivityQueryAdapter` appeared to depend only on named Grid and Buildings contracts, but `Grid.IsConnected` resolved to `GridBuildingExtensions` in Assembly-CSharp. Focused compilation exposed the boundary; moving the pure occupant-path query onto `Grid` made the ownership explicit without duplicating state.
- MonoBehaviour source moves must preserve the `.meta` GUID even when the new filename is improved to match the class. `EventAlertRuntime` and `NoticeFeed` can reside entirely in Presentation because their Aggregate/event contracts are already named and Presentation owns TMP/UI/VContainer dependencies.
- Infrastructure runtime registries that wrap named Aggregate sessions can move as cohesive adapters when their concrete consumers remain at the Unity edge. Their type must be public across the assembly boundary, but mutation still remains encapsulated in the Aggregate session.
- Precompiled-plugin references do not imply that source extension modules are visible to a named assembly. DOTween core was referenced by Presentation, but `CanvasGroup.DOFade` lived in the default-assembly `DOTweenModuleUI.cs`; using the core `DOTween.To` API removes that hidden edge while preserving the same alpha interpolation.
- Broad scene-reference containers should not be injected into small UI constructors when the UI needs one object. Extracting the EventSystem-only bootstrap reference lets the title canvas live in Presentation without turning the full scene Aggregate into a Presentation dependency.
- Session clock/speed interfaces and user-settings DTO/contracts are shared protocols, while their Unity effects and persistence are Infrastructure. Moving only the protocols to CoreSession/Foundation enables presentation policy migration without moving mutable implementation state into an SO or introducing a second settings owner.
- A settings-aware MonoBehaviour is not a pure palette merely because its first type is static. `DungeonUiThemeRuntime` still styles default-owned room and building views, so only the stateless palette facade can move now; the component remains at the Unity adapter edge with its original serialized identity.
- Path-based architecture ratchets must be rerun after every source move even when semantic planner and focused compilation pass. The Invasion intruder planner move preserved its API and GUID but left one stale test suffix; a full 281-call uniqueness audit caught it before the Unity test boundary.

## 2026-08-04 localization and encoding authority audit

- The current ArchitectureMetrics Korean-literal rule covers `6,441` ordinary string literals across `401` non-Editor runtime files, but omits another `2,122` Korean interpolated-text segments across `229` files. The proven display/content debt is therefore at least `8,563` tokens, and the metric must include `InterpolatedStringTextSyntax` before it can be used as a closure gate.
- Of the `6,441` counted literals, `6,423` are valid Korean, `18` are confirmed mojibake, and U+FFFD replacement characters are `0`. The 18 damaged literals are confined to five runtime files: `DefenseFacilityRuntime.cs` (14), three CharacterCombatCommand runtime/contract files (one each), and `WildlifeRuntime.cs` (one).
- Only one String Table collection exists (`DomainFailures`): `296` shared keys with complete Korean and English entries. Exact raw-literal coverage is only `2/6,441`; UI/domain candidates total `3,482`, so localization is not close to completion.
- A UI-first role partition yields `2,089` UI display literals, `1,387` non-UI domain-error literals, `883` authored narrative/content literals, and `2,082` other literals. Recommended non-overlapping vertical cuts are Production presentation, Defense runtime/presentation, then Character narrative content.
- `ProductionRoutePanelPresenter` itself has no encoded mojibake. It contains four valid Korean UI tokens plus three English status templates not covered by existing tables; use dedicated `ProductionUI` keys for header, priority/weight/reserve controls, demand/reserved, blocked, and inactive-consumer text.

## 2026-08-04 risk-based assembly closure decision

- `Assembly-CSharp` file count is a poor completion oracle. A scene-bound adapter and a mutable gameplay Aggregate both count as one file even though only the latter threatens authority, determinism, and test isolation.
- The enforceable replacement is a reviewed role classification: `NamedRequired`, `DefaultAllowed`, or `ReviewRequired`. A mixed Unity/domain owner cannot be approved as an adapter; it must be split until gameplay state and rules are named-owned.
- The cycle gate must also be semantic rather than numeric: named asmdef cycles remain forbidden, no cyclic source SCC may contain a `NamedRequired` owner, and remaining default-edge code may not bypass command/query/capability/DTO boundaries between gameplay domains.
- This rescope is expected to remove roughly 70-85% of the former file-migration workload, but the estimate is scheduling guidance only. Completion is proven by empty reviewed violation sets, not by an estimated percentage or a chosen residual file count.

## 2026-08-04 Phase 117 ownership-classifier evidence

- The first syntax-based audit classifies every one of the `811` current default-runtime sources: `35 DefaultAllowed`, `441 NamedRequired`, and `335 ReviewRequired`. The honest `UnapprovedDefaultDomainAuthorityCount` is therefore `776`, not zero.
- `defaultAssemblyFiles` remains in the report as trend information but is no longer part of the baseline ratchet. The new unapproved-authority metric is emitted separately, and the current baseline intentionally does not approve this newly measured debt.
- Classification records exact syntax/type/source-role evidence. Mutable state, Aggregate/state/store/rules/policy/calculator/content/SO/persistence/command-query and deterministic service roles raise risk; a mixed Unity edge and domain owner remains `ReviewRequired` and cannot be downgraded to `DefaultAllowed` by a manifest explanation.
- The exact-path override manifest rejects wildcards, parent traversal, missing fields, duplicate normalized paths, deleted paths, named-assembly paths, and other stale entries. The separate Library report currently lists `22` cross-domain-cycle candidates with their referenced domain set.

## 2026-08-04 Environment work-policy boundary audit

- `EnvironmentWorkPolicy` mixed pure cooldown/failure/speed decisions with `CharacterActor`, `Grid`, workwear commands, and Unity coordinates. The named `DungeonStory.Environment` assembly already owned the core exposure math, so creating another assembly or moving Unity-facing types into the domain would have been the wrong boundary.
- Cold-work cooldown hysteresis, safety-exception classification, blocking-failure selection, and legacy speed selection now live beside `EnvironmentWorkRules` in the named Environment domain. The default edge only converts scene actors/cells and maps the typed decision to `DomainFailure` presentation parameters.
- The default type is now `EnvironmentWorkPolicyUnityAdapter`; classifier evidence changes from `NamedRequired` to `DefaultAllowed`. It remains visible in the cross-domain candidate report because it deliberately bridges Environment and Foundation contracts, but no gameplay decision is owned by that bridge.
- The source/meta rename preserves GUID `286444572b7d9f24db60fc3a64916ba7` exactly once. The old source/meta paths and old concrete type reference are absent; the interface contract and singleton composition registration remain unchanged.

## 2026-08-04 Character environment runtime boundary audit

- `CharacterEnvironmentRuntime` mixed deterministic exposure accumulation, band transitions, and movement/accuracy policy with `CharacterActor`, world queries, persistence projection, and Unity-side damage/effect dispatch. The pure transition now lives in the named `DungeonStory.Environment` domain as `CharacterEnvironmentRules.StepExposure`; the default edge is explicitly named `CharacterEnvironmentUnityAdapter`.
- The adapter projects the named result back into the unchanged `CharacterEnvironmentExposure` save DTO and retains the existing side effects, timers, capture/restore ordering, and aggregate publication. No V18 manifest, save version, DTO shape, or Character AI narrative source was changed.
- The classifier target changed from `NamedRequired` to `DefaultAllowed` with `unity-edge-suffix` evidence. The current report is `811 default / 79 allowed / 439 named / 293 review / 732 unapproved / 22 cross-domain candidates`; only the target transition and the one-count unapproved reduction are attributed to this cluster because other lanes are active.
- The runtime source/meta path remains unchanged to preserve serialized identity. GUID `1e8e23e7affbbc645a4ef3b83b17163f` occurs exactly once, and all old concrete-type references are absent.

## 2026-08-04 Character progression boundary audit in progress

- `CharacterProgression.cs` is an 877-line scene component that currently owns both deterministic experience/level transitions and Unity-facing `CharacterActor`, alerts, draft generation, triggered passives, and persistence projection. Moving the whole type would create the wrong boundary; the narrow cut is to extract only the pure progression transition into the existing named Characters domain.
- Save safety requires the existing `CharacterProgressionSnapshot`, capture/restore order, draft generation, profile warming, notifications, and actor vital recalculation to remain on the current adapter path. The first candidate for extraction is therefore the experience curve plus add/target-level transition result, not the persisted DTO or Character AI narrative behavior.

## 2026-08-04 Character progression boundary audit complete

- `CharacterProgressionRules` and immutable `CharacterProgressionTransition` now live in `DungeonStory.Characters`. They own the experience curve, experience addition, minimum-level advancement, reached-level sequence, ratio projection, and restore normalization without referencing `CharacterActor`, Unity scene objects, save services, or authored skill/narrative content.
- The existing `CharacterProgression` MonoBehaviour remains the compatibility and side-effect edge. It applies reached levels in their original order, so deterministic stat allocation, vital recalculation, logs, draft unlocks, and `Changed` publication preserve their former order and meaning. `CharacterProgressionSnapshot` shape and the V18 capture/restore call sites are unchanged.
- The target remains honestly `ReviewRequired` because it still serializes progression/growth state and contains the save snapshot beside a MonoBehaviour. This lane did not add an override or move that persisted authority merely to improve a metric. The useful boundary delta is that `CharacterProgression.cs` is absent from the current cross-domain candidate set while the new rules source is owned by the named Characters assembly.
- The original MonoScript GUID `badabbf33eed2ae46b77a5f13883bc2d` remains exactly once. The new named rules source has unique GUID `4b5a3cf2ed6845d8a76c50e0909a09c2`. No Character AI narrative, facility naturalness/utility, Defense Codex, save-version, save-service, DTO-shape, or restore-order source was edited by this lane.

## 2026-08-04 Environmental field boundary audit in progress

- `EnvironmentalFieldRuntime.cs` owns three distinct layers: strict save projection/restore mapping, a mutable array Aggregate and root store, and Unity Grid/building/power/clock projection. The save path already delegates detached candidate validation to named `EnvironmentalFieldRestoreRules`; no save defect was found, so the save section and DTO must remain untouched.
- The safe boundary is to move the array Aggregate plus diffusion, exterior exchange, air recovery/contamination, light relaxation, source-cell transitions, swaps, and version touch into the existing named Environment assembly. The default adapter should retain only Grid topology discovery, line-of-effect, authored building projection, power checks, and fixed-clock scheduling.
- The runtime can preserve exact Grid replacement semantics without default-owned gameplay state by holding a readonly `WeakReference<Grid>` projection cache. Source descriptors can also remain at the edge as immutable records; target overrides and simulation arrays remain named-owned.
- The earlier Character progression candidate-removal report disagreed with the root fresh analyzer and is withdrawn. Before this lane is handed off, CharacterProgression will be rerun through the same fresh analyzer and resolved without an exact-path override.

## 2026-08-04 Environmental field and Character progression boundary closure

- `EnvironmentalFieldAggregateState`, its Aggregate-root store, and all deterministic diffusion/source/buffer/version rules now live in named `DungeonStory.Environment`. `EnvironmentalFieldRuntimeApplicationAdapter` retains only fixed-clock scheduling, Grid topology and line-of-effect discovery, authored building/power projection, and the unchanged strict save mapping.
- A standalone legacy-equivalence probe compared 240 deterministic randomized grids and 16,234 assertions across temperature, air, light, barriers, doors, ducts, exterior exchange, source helpers, swaps, and version increments; all passed. Focused `DungeonStory.Environment` compilation also passes with zero diagnostics.
- The earlier Character progression candidate result was stale, not an analyzer defect. The actual remaining cross-domain edges were `IGameEventBus`/Operation presentation and Foundation deterministic-random construction. They now live in `CharacterProgressionNotificationApplicationAdapter` and `CharacterProgressionGrowthApplicationAdapter`; the state owner references only `DungeonStory.Characters`.
- Fresh ArchitectureMetrics reports both target paths absent from `crossDomainCycleCandidates`. The environment adapter and notification adapter are `DefaultAllowed`; `CharacterProgression` remains honestly `ReviewRequired` because it still owns serialized per-character state and its snapshot, but it is no longer cross-domain. No exact-path override, manifest approval, or classifier weakening was used.
- No environmental save section, DTO, payload version, restore order, or V18 compatibility rule changed. The original environment and Character progression GUIDs plus all three new source GUIDs are unique; the 48-asmdef graph has zero cycles.
- Unity comprehensive script validation passes for all changed runtime/rule sources. The merged Editor currently has one unrelated compile error in `GridFoundationDebugScenarios.cs` for missing `DungeonEntranceGridResolver`; this lane did not edit that source, so final Console-zero acceptance remains with the merged integration owner.

## 2026-08-04 External influence CoreSession/application boundary

- `ExternalInfluenceRuntime.cs` mixed root-Aggregate access and deterministic reputation, dread, hostile-rumor, scouting, ecology, raid, intel-payment, and invasion-defense state transitions with authored CoreSession content, Unity clock, money ledger, physical items, wildlife world state, and event presentation.
- Added named `DungeonStory.CoreSession.ExternalInfluenceAggregateStateStore` and `ExternalInfluenceDomainRules`. They now own all direct state changes, identifier normalization, threshold decisions, countdowns, daily pressure/report transitions, payment-state commits, raid lifecycle, and dread multipliers. The default `ExternalInfluenceRuntimeApplicationAdapter` retains only external capability checks/transactions, world snapshots, event subscriptions/alerts, JSON projection, and strict candidate publication.
- The existing V3 DTO, restore candidate, save section, validation order, participant order, V18 compatibility meaning, and transaction ledger owner/reason strings are unchanged. No save or contract source was edited; the adapter publishes through the named store into the same Aggregate root slot.
- A deterministic current-source probe passed 6,506 comparisons covering all extracted numeric clamps and transitions. Focused `DungeonStory.CoreSession` Roslyn compilation has zero diagnostics. Unity current-source fairness and content-authority scenarios pass, and direct strict validation accepts the canonical V3 payload while rejecting out-of-range renown.
- The combined Batch A suite reaches its unrelated presentation check and then fails in `DomainFailureLocalizer` because the current `InsufficientRenown` String Table format expects a different argument count. This is outside the external-influence boundary and was not hidden by changing the localizer or save fixture; focused external-influence validation passes and the final Console is Error 0 / Warning 0 after the diagnostic run is cleared.
- Fresh ArchitectureMetrics classifies the preserved target `DefaultAllowed` with application-adapter evidence and reports target candidate count 0 without an override. The observed shared-tree checkpoint is `1,368 files / 4,244 types / 817 default / 123 allowed / 408 named / 286 review / 694 unapproved / 11 cross-domain candidates`; only the target transition is attributed to this lane.
- The original MonoScript GUID `115d5aeafd549764a9fbff9b92d35017` and new named source GUID `c7ea3bfe8eec4f909347a5e0f48bf0e4` each occur exactly once. The 48-asmdef graph has zero cycles, old concrete construction/static-policy references are zero, and scoped `git diff --check` passes.

## 2026-08-04 World-simulation composition registration classifier

- `DungeonWorldSimulationRegistration.cs` contains one static extension method and no fields, properties, constructors, nested types, local state, assignments, loops, switches, policies, or calculations. Its only runtime branch is the existing scene-capability registration guard; all other calls are VContainer registration/exposure wiring.
- The ownership analyzer now recognizes composition registration only when the file is under `Services/Infrastructure/Registration`, the type is static and ends in `Registration`, every member is a static `void Register*` method whose first parameter is `IContainerBuilder`, and every invocation is registration/exposure wiring, `nameof`, or the approved scene-capability check. Local declarations, assignments, local functions, loops, and switches reject the shape.
- A separate three-source analyzer probe proves the boundary: pure registration is `DefaultAllowed`, while an otherwise identical registration with mutable static state and another with a local policy calculation both remain `ReviewRequired`. Existing `MetaRuntimeApplicationAdapter`, `OperatingDaySettlementRuntime`, and `ConstructionSite` also remain `ReviewRequired` with their mutable-state evidence.
- Fresh ArchitectureMetrics classifies `DungeonWorldSimulationRegistration` as `DefaultAllowed` with `composition-registration` evidence and target candidate count 0 without an exact-path override or baseline change. The observed shared-tree checkpoint is `1,368 files / 4,244 types / 816 default / 131 allowed / 408 named / 277 review / 685 unapproved / 9 cross-domain candidates`; only the target transition is attributed here.
- The registration source itself required no change in this lane. Its GUID `9296e3e24fa840b45a164c196fa08423` remains unique, the 48-asmdef graph has zero cycles, analyzer compilation and Unity comprehensive script validation pass, scoped diff whitespace is clean, and loaded Console remains Error 0 / Warning 0. Save/V18 sources and the ownership override manifest were not edited.

## 2026-08-04 Blueprint research runtime boundary

- The named Research assembly already contained the authoritative `ResearchProjectRuntimeState`, progress ratio/add/restore rules, queue entry mutations, and prerequisite/dependency ordering. The actual cross-domain defect was the scene component directly owning Foundation root-store projection, event publication, debug-cheat lookup, and the final node-state branch matrix.
- `BlueprintResearchApplicationAdapter` now contains only those Foundation-facing connections, while `ResearchProjectCoordinatorRules.EvaluateNodeState` owns the deterministic state precedence: missing, completed, active, queued/suspended, archived shortcut, prerequisite lock, required-blueprint transit/lock, facility lock, then available.
- The pre-injection fallback in the preserved MonoBehaviour is retained for Unity serialization/debug compatibility, but every constructed runtime delegates fact projection to `BlueprintResearchProjectCoordinator` and then uses the named decision path. Moving that projection out restored the runtime from 839 to 741 lines and returned `oversizedTypes` to 0. No research save source changed, and the loaded V5 `ResearchSaveValidation.RestoreProgressRatio(196, 560, 720)` result remains exactly `252`.
- Fresh ownership evidence for the target is `ReviewRequired` with only `mutable-domain-state-shape`, `runtime-service-role`, and `MonoBehaviour-scene-edge`; referenced domains are Research only and target cross-candidate count is 0. The Foundation adapter is `DefaultAllowed` with application-adapter evidence.

## 2026-08-04 Exterior incident authority defect

- The live incident had two clocks: `ExteriorActivityRuntime.TickIncidentStates` decreased `ExteriorIncidentRuntimeState.remainingSeconds`, then every `ExteriorZoneMarker.TickIncident` decreased a second private timer. Reception/patrol work could also clear only the marker while the saved runtime state remained active, and `ActiveIncidents` was sourced from marker copies while `Capture` used runtime states.
- The named generic Aggregate now owns the collection, countdown normalization, handler mutation boundary, active count, and history trim. Marker projection never advances or resolves time and cannot produce save data. Every handler tick/action returns a transition that updates or clears the matching marker after the authoritative state has settled.
- Restore still uses the frozen detached-zone candidate and exact V18 section contract. Publication replaces the Aggregate from candidate incident states and then rebuilds all marker projections; capture and active queries read the same Aggregate collection.
- Fresh ownership reports `ExteriorActivityRuntime` as `ReviewRequired`, referencing Exterior only, with target candidate count 0. `ExteriorActivityApplicationAdapter` is `DefaultAllowed` and contains the Foundation clock/random plus Environment hazard projection. Source gates pass with global candidates 6, oversized 0, `48/0` asmdefs, unique GUIDs, no save-source diff, and no marker incident timer/save source.

## 2026-08-04 Operating-day settlement authority defect

- The previous MonoBehaviour directly mutated every ledger collection and debt field while also scanning Unity buildings/characters, invoking employment and paid-facility settlement, raising alerts, and publishing reports. Repeated `OperatingDayEndedEvent` calls therefore had no domain idempotence barrier before irreversible economy ports.
- The named generic Aggregate avoids a reverse reference to default-owned report and stock-supply presentation types while still owning their history/list transitions. Primitive category IDs cross the boundary and are converted back to `StockCategory` only by the default persistence/report adapter.
- Settlement is a tokenized two-phase domain transition: begin freezes an immutable ledger request, external ports produce an immutable economy application, named rules calculate debt/shortfall effects, alert side effects are reflected into a refreshed immutable snapshot, and completion/history publication is followed by an explicit ledger finish. `LastSettledDay` rejects duplicate settlement before any port call.
- `LastSettledDay` is reconstructed from the newest existing report during the unchanged restore preparation, so the new idempotence state requires no save DTO/version change. Pending tokens are deliberately transient and are never serialized.
- A compatibility facade is necessary because authored scenes serialize the original MonoScript GUID and type name. Keeping logic in the `ApplicationAdapter` lets the analyzer recognize the actual Unity edge; the exact-path facade allowance documents that it owns no field or rule rather than hiding mixed ownership.

## 2026-08-04 Experience pacing authority findings

- The concrete defect was not the pacing DTO. `ExperiencePacingRuntime` directly owned the Aggregate-root lookup plus every day/mask/concept mutation while also resolving authored Content rules and subscribing to a Foundation event. That made one default file both state authority and cross-domain adapter.
- The named Aggregate now makes invalid or duplicate transitions unrepresentable through its command surface. It also validates detached candidates independently of the strict save section, so direct runtime publication cannot bypass the same invariants used by save restore.
- Keeping a legacy three-argument constructor as a partial runtime declaration inside the application adapter initially left that file `ReviewRequired` and in the cross-domain candidate set. Removing the compatibility surface and updating Editor callers made the adapter a recognized `DefaultAllowed` application edge without an override; global candidates fell from `3` to `2`.
- The frozen save wire contract remains payload version `1` within root V18. Capture emits ordered unique concepts; prepare validates before publication; publication revalidates and clones the candidate before replacing the live Aggregate state.
- Focused Roslyn and standalone probes are green. Unity MCP currently reports revoked approval, so current-loaded execution is an integration checkpoint rather than a source-lane failure.

## 2026-08-04 final-acceptance runner coverage findings

| Completion contract | Synchronous runner evidence |
|---|---|
| V18 manifest, authority, 54 strict sections | `RuntimeAuthorityV18Validator`, `DungeonSaveSectionDebugScenarios`, Batch A/B/C, Offense aggregate V18 |
| Authored content authority | localization synchronization/validation plus `BatchAContentAuthorityDebugScenarios` |
| Physical item, stock, equipment state | persistent identity, physical item/stock, equipment component/material, combat and research-equipment suites |
| Branched production and facility fuel/feed | Batch C, branched-network graph/value/distribution/save validation, production economy, industrial infrastructure |
| 168 research and equipment growth | research tree plus `ResearchEquipmentOverhaulDebugScenarios`, including 43 equipment, 20 modules, locks, module process/save, drops, and pacing |
| Exterior, OperatingDay, Experience, Service, DungeonRun | direct Exterior/OperatingDay/Experience/Service entries plus Batch A integrated CoreSession RunFlow/save fixture |
| Combat, medical, survival | combat and strict combat save, surgery/anatomy integration, Batch B and survival suites |
| Implemented game loops | broad implemented-scenario runner plus direct strategic physical expedition, journey, architecture, and Offense aggregate validation |

- The runner previously invoked OperatingDay only as one nested item inside `ImplementedScenarioDebugRunner`; the new direct entry makes the recent idempotence/ledger authority result independently visible in the final report. The new composition entry similarly makes the VContainer/Unity edge contract explicit.
- The runner is intentionally not a PlayMode or visual harness. Its report now names the deferred Unity MCP resolution/capture/Console gate, preventing a synchronous pass from being presented as complete UI acceptance.
- No callable synchronous scenario currently executes equipment history transfer end-to-end or proves expedition-death co-loss of an item and its installed modules. The research-equipment fixture covers module appraisal/restoration/install/remove damage and V6 save, but not lineage transfer.
- Combat production code implements reload, smoke exposure, and durability-based misfire, but the callable combat suite does not assert smoke/misfire or bow/crossbow/gun non-dominance scenarios. Live 54-section world round-trip and repeated scene/run static isolation also require the loaded PlayMode integration path. These gaps were not replaced with source-token assertions in the final runner.

## 2026-08-04 Dungeon run-flow authority findings

- `DungeonRunFlowRuntime` mixed Aggregate-root writes with authored pacing rules, Experience rehearsal coordination, invasion scene mutation, owner-run completion, alerts, and restore projection. The original type/GUID is serialized compatibility surface, so moving or renaming it would be unsafe.
- `DungeonRunFlowReducer` now receives one event and immutable authored rules, returns a replacement state plus an ordered effect list, and never invokes Unity or external runtimes. Monotonic day handling rejects duplicate and out-of-order days before phase, rehearsal, or boss effects can repeat.
- Rehearsal ownership remains singular in the existing Experience pacing Aggregate. RunFlow owns only the deterministic decision to evaluate that rehearsal and the feedback transition that either suppresses or arms the due recurring boss; it does not add a second persisted rehearsal clock or mask.
- Boss cycle and armed flags are committed before the adapter executes the invasion effect. Repeated scheduling feedback therefore produces no second arm/force effect, while boss start, boss defense, truth completion, owner completion, and post-finish days are also idempotent.
- The frozen save seam remains root V18 with `run.flow` payload V2, `LateRuntimeState`, Offense/Invasion dependencies, detached `BuildRestoreCandidate`, and single `PublishRestoreCandidate`. No new persisted reducer-only field or migration was introduced.
- The adapter is automatically `DefaultAllowed`; the fieldless runtime facade has a reviewed exact-path allowance rather than hidden domain state. Both targets are absent from the fresh cross-domain candidate set.

## 2026-08-05 final offline integration audit findings

- The final runner still has exactly 33 named steps. No extra top-level step was necessary: `PhysicalItemDebugScenarios.RunAll` now executes real queued lineage transfer work and verifies source equipment/seal consumption plus target physical properties/modules; `CombatSystemDebugScenarios.RunAll(false)` executes durability misfire, smoke exposure, ammunition, penetration, reload, cadence, and bow/crossbow/gun role assertions; `OffenseExpeditionDebugScenarios.RunAll(false)` delegates to `OffenseJourneyDebugScenarios`, whose death scenario calls the actual `OffenseExpeditionReturnPort.HandleMemberDeath` path and verifies equipment/module co-loss.
- The first focused compile exposed two real integration errors in the new regressions: a nonexistent `EquipmentEvolutionDirection.Defensive` value and a missing `DungeonStory.Foundation` import for `GameEventBus`. The owning random-stream lane corrected both to `Protection` and the proper import; the scoped source diff then passed.
- Fresh ArchitectureMetrics after those corrections reports `1,380 files / 4,275 types / 822 default / 141 allowed / 401 named / 280 review / 681 unapproved / 0 cross-domain candidates`. Mutable statics, oversized types, large constructors, content escapes, and direct session mutations are all `0`.
- The 49-asmdef graph has zero cycles. Four unresolved GUID-form asmdef references belong to the external `DamageNumbersPro` package and are outside the Assets-only name map; they are not graph cycles. All C# source metas exist and all 6,817 parsed asset GUIDs are unique.
- The final runner passes focused Assembly-CSharp-Editor compilation. A broader offline Editor compile cannot substitute for the root loaded gate because shared Bee reference artifacts are stale/overwritten (`Assembly-CSharp-exterior-check` and `ExperiencePacingAggregateProbe`) and do not expose the current environment interface; Unity reload must regenerate them.
- Global `git diff --check` is not clean: it reports 1,502 trailing-whitespace lines across 32 pre-existing/shared Unity-generated files, dominated by `GameplayScene.unity` (1,406) and `DungeonStory.slnx` (43). The audited runner, three new regression sources, architecture manifest, and planning documents pass scoped diff checks. These unrelated serialized files were deliberately not rewritten during the concurrent audit.

## 2026-08-04 final evidence-gap closure findings

- The lineage authority was already production-ready but unproven end to end. The new physical-item regression uses the actual seal stack, queue/work APIs, repository-backed equipment instances, evolution state, and module runtime instead of a definition or source-token check.
- The expedition return port is the authoritative death bridge. Exercising that port exposed no product defect: the equipment loadout runtime already marks both the unique equipment item and each installed module Lost as one death consequence.
- Gunpowder smoke did contain a product defect. `CombatResolutionService` previously placed `SmokeExposure` into target `Suppression` only for a misfire and emitted no smoke on normal hits or misses. The result contract now has a separate immutable smoke field, centralized result normalization attaches smoke to every executed gunpowder outcome, and `CombatResolutionService.Record` applies the exposure exactly once. Applying it from `CombatCommandResultApplier` was incorrect because Defense, Wildlife, Offense, and Circus share the resolver but do not all share that later applier.
- Offense ally attacker IDs are persistent character IDs and therefore resolve through `ICharacterWorldQuery`; generated enemy IDs do not. `CharacterEnvironmentRuntime.AddAirborneExposure` now rejects an ID with no live actor, preventing a smoke result from creating a phantom saved character-environment entry.
- The full-world facade's earlier `baselineRestored=true` was only a scenario-return assumption. It now compares canonical 54-section captures before and after execution, treats mismatch as a test failure, and performs a separately verified restore only for cleanup. Its Console capture also starts at PlayMode transition before gameplay scene composition and excludes stale EditMode history.
- The save round-trip fixture still expected a section-version-1 owner-doctrine fallback even though the V18 run-variable payload contract is strictly current V3. The fixture now injects an explicit V2 payload under the current section envelope, requires version rejection, and proves the failed restore leaves the canonical live state unchanged.
- `ICharacterEnvironmentExposureCommand` is the minimal mutation capability for this boundary. Its implementation clamps airborne exposure, refreshes the physiological band immediately, remains part of the existing environment Aggregate state, and adds no save field or V18 contract change.
- The synchronous final runner remains 33 steps because all three regressions are reached through existing composite entries. The 54-section live-world requirement is intentionally a distinct PlayMode facade so an offline Editor pass cannot impersonate live scene/container restoration evidence.
- The only unresolved verification is environmental, not a known source defect: Unity must regenerate stale Bee references before loaded compile/run. The isolated facade and smoke-focused compiles pass, while the stale whole-default response fails first on concurrently moved Operation/Exterior/Service/Run sources.

## 2026-08-05 final PlayMode facade static follow-up

- At this checkpoint the final coordinator covered Resolution, Research, Production, Service Room, Character Summary/Medical, and Full World, but did not yet include direct equipment/expedition evidence. This checkpoint was superseded later the same day by the current seven-target/30-capture matrix.
- `CharacterProgressionSavePlayModeFacade.Run` had no caller, so its ownerless/invalid-cell/rollback-free-late-failure and staff work-state round-trip contracts were not part of final acceptance. The existing Full World target now invokes it before the broad 54-section scenario and requires its result; the later equipment/expedition expansion owns the current target/capture contract.
- Resolution explicitly waits for an owner and a closed owner-selection surface before HUD checks. Research and Production use the 45-second party driver. Full World and Character Summary use synchronous fast commit followed by frame settling; their clearest remaining runtime risk is an owner/preparation composition failure, which will surface as an explicit target failure rather than a false pass.
- No Unity or MCP process was used. Local command-line compilation remains unavailable because neither `dotnet` nor the Visual Studio MSBuild installation can resolve `Microsoft.NET.Sdk`; loaded Unity compilation remains the required next gate.

## 2026-08-05 equipment/expedition final UI evidence closure

- The earlier final coordinator had no direct equipment or expedition evidence at the two required resolutions. Existing offense verifiers either lacked pointer input, ran the pointer flow outside their responsive matrix, or emitted a report marker incompatible with the final coordinator.
- Added a seventh `EquipmentExpeditionUiMatrix` target with four required fresh captures: equipment and expedition at both `1600x900` and `900x1600`. The final coordinator contract is now seven targets and 30 captures.
- The equipment matrix uses the authored `EquipmentProgressionCommandPanel` stable object names and Unity `EventSystem` pointer events to execute appraisal, restoration, rune tuning, installation, removal, lineage source/target/seal selection, and lineage confirmation. It asserts the lineage order was actually queued.
- Equipment instances and the lineage seal are materialized as physical items. Physical-item and equipment runtime snapshots are restored between resolution rows and during cleanup; the outer final coordinator remains the persistent-state snapshot authority around the PlayMode target.
- The verifier also captures the canonical `research.blueprints` and `offense.aggregate` save sections before seeding. Both sections are restored and compared byte-for-byte with their original captures before every resolution row and during final cleanup, so a standalone run cannot leave completed research or expedition/campaign state behind.
- Each resolution row explicitly clears the transient expedition and battle runtimes after restoring the same offense baseline. Both rows therefore start from an empty verifier-owned expedition session instead of inheriting the previous row's route/battle progress.
- The expedition matrix uses the live expedition panel and pointer-clicks a non-close journey action, requires the expedition phase/node to change, checks panel bounds, and captures the resulting surface at both resolutions.
- No Unity, MCP, helper, or operating-system mouse automation was used in this source-only lane. Direct Roslyn compilation passed for the current default runtime RSP plus the new progression panel and for the Editor RSP plus the new matrix verifier against that fresh runtime DLL. Loaded Unity PlayMode execution remains the root gate.

## 2026-08-05 final coordinator dirty-scene safety

- The only final-coordinator scene switch is `EditorSceneManager.OpenScene(..., OpenSceneMode.Single)`. It previously ran after persistence capture and evidence cleanup, so a dirty loaded scene could trigger Unity's blocking save/discard modal during unattended acceptance.
- Before the request is admitted, the coordinator now validates every distinct scene path required by the full suite against the currently loaded scenes. A dirty active Title scene therefore fails immediately because a later Gameplay target requires a switch; it cannot run Resolution first and fail late. This preflight occurs before state creation or persistence capture.
- `StartCurrentTarget` also validates each actual transition before any target-side mutation. A switch is rejected when any loaded scene is dirty, including an untitled scene whose path is reported as `<unsaved>`; no scene is saved, discarded, unloaded, or overwritten.
- `OpenScene` repeats the same validation immediately before the Unity API call as a defensive boundary. When the requested scene is already active, the existing no-open path is preserved. With clean loaded scenes, the existing seven-target/30-capture sequence is unchanged.
- A preflight rejection declares persistence restoration not required and does not touch a previous snapshot. Mid-run failures retain the existing captured-snapshot restore path.
- No Unity, MCP, helper, or operating-system input was used. Direct Roslyn compilation of the current Runtime and Editor response sets passed with zero errors.

## 2026-08-05 authored equipment-progression facility evidence

- The equipment matrix no longer renders every progression command on one arbitrary forge. It instantiates the authored RF42 appraisal table, RF43 restoration bench, RF44 precision fitting bench, I17 rune tuning room, and I18 lineage archive, plus S08 as a negative control.
- One grade-4 module is a real `item:equipment-module` unique physical item. The verifier routes the same stack through each facility-local `FacilityBuffer`, requires the destination to equal that facility's persistent ID, and pointer-clicks appraisal, restoration, rune tuning, installation, and removal only after delivery.
- Every facility render proves its allowed command prefixes are present and all other progression command prefixes are absent. The S08 forge must expose none of them.
- Precision installation requires both module and target equipment in RF44's local buffer. Installation must absorb the standalone module stack without marking the module lost; removal must recreate a standalone stack with the same module instance ID in RF44's buffer and apply the replacement/removal condition loss.
- Lineage source equipment, target equipment, and the regional seal are all routed to I18's local buffer before pointer selection and confirmation. Work is applied through I18, then source/seal consumption and target history inheritance are checked.
- The verifier emits `FACILITY_FLOW=RF42,RF43,RF44,I17,I18`; the final coordinator now requires that marker, so an older shallow equipment report cannot satisfy final acceptance. The suite remains seven targets and 30 captures.
- `item:equipment-module` is now an authored max-stack-one item registered exactly once in `ItemDefinitionCatalogSO`. An unattached module is saved as its own unique physical item with a typed `ItemInstanceId`, `sourceStackId`, and strict module component payload; installed modules remain embedded only in their equipment payload, and validation rejects detached/attached duplication or broken stack links.
- Destructive stack deletion and full consumption move a detached module to `Lost`, while the dedicated installation absorption path deliberately avoids the loss transition. Removal and occupied-slot replacement rematerialize the same module instance in the precision-fitting facility buffer with the required condition loss.
- The three former facility-less debug callers now use real facilities and physical buffers. Fresh Foundation, Items, Combat, default Runtime, and full Editor Roslyn compilation all pass with zero compiler output. Fresh ArchitectureMetrics also passes at `1,384 files / 4,314 types` with mutable statics, oversized types, large constructors, cross-domain cycle candidates, content escapes, and direct session mutations all `0`.
# 2026-08-05 post-Copilot acceptance findings

- The 18:50 final acceptance artifact is not current completion evidence: it reports 29/33 and predates a fresh Unity import that found source compiler errors.
- `DungeonGameRestoreReport.Success` is derived from its private error list and is intentionally read-only. Validation failures must flow through `AddError`; test diagnostics must not mutate the result flag.
- The temporary `Run V14 Combat Scenarios` menu had been narrowed to only the V18 body-health test, making the menu label and executed coverage disagree. It must always invoke the full combat suite; focused diagnostics may use separately named commands.
- Combat failures were previously flattened twice (`Combat/body-health fixture failed` and `Scenario returned false`). Returning a concrete failure collection from the fixture preserves exact scenario evidence without static mutable diagnostic state.
- `item:equipment-module` must remain non-craftable expedition loot. Its dependency graph needs authored external reward producers and real appraisal/restoration/fitting/tuning consumers; a fake recipe, sink, or item-ID skip would violate the production-graph contract.
## 2026-08-06 Phase 120 CharacterId integration audit

- Fresh loaded gates passed before repair (`Architecture 131/131`, transactional restore `33/33`, synchronous final acceptance `33/33`), but they did not cover operational actor creation followed by save capture and restore.
- Faction reinforcements are now created as `character:faction-route:*`, while `FactionPayloadValidation` still requires the former raw `faction-route:*` form. Any run with materialized reinforcements captures a payload that its own restore validator rejects.
- Offense return prisoners are now created as `character:return:*:prisoner:*`, while `OffenseReturnArrivalSaveValidation` still requires the former raw `return:*:prisoner:*` form. A materialized-prisoner run is likewise self-unrestorable.
- The early-V18 compatibility resolver only accepts raw `world:*` and `staff:*` IDs, although the same V18 generation previously assigned raw invasion, faction, return, and exterior incident IDs to `CharacterActor` instances.
- The global reflection normalizer guesses character references by field name and misses concrete save fields including `actorId`, `actorIds`, `preferredDoctorId`, and `doctorId`; it also cannot safely distinguish every generic `targetId`/`persistentId` from non-character IDs. Section/type-scoped normalization is required.
- `FinalAcceptanceReportPolicy.IsFreshPass` accepts any `RESULT=PASS` line and does not reject conflicting or duplicate result declarations, so a failed composite report can be misclassified.
- Content migration safety improved through owned-output saves and dirty preflight, but the provenance input hash omits code dependencies used to generate evolution catalyst definitions, and the root catalog's type-erased fields are only null-checked.
- Final PlayMode evidence remains missing. The active `Assets/Scenes/TitleScene.unity` is dirty in memory, so the project-scoped coordinator cannot safely switch scenes until the user explicitly saves or reverts it.

## 2026-08-06 final PlayMode composition and transition findings

- The first fresh Full World run proved the remaining mass injection failures were cascading from one composition-root cycle, not dozens of missing registrations. `OffenseWorldMapRuntime` eagerly required `IOffensePanelService`, whose constructor required the query/command interfaces implemented by that same runtime.
- The durable dependency direction is Presentation/Application -> campaign query/commands. World-map UI opening was removed from `IOffenseCampaignCommands` and moved to `OffenseApplication -> IOffensePanelService`; the save authority remains `OffenseCampaignRuntime`, so no V18 DTO, section, or restore contract changed.
- Unity PlayMode entry must not be requested in the same `EditorApplication.update` that opens the target scene. The standalone Full World retry demonstrated that this can freeze the Editor at `Entering Playmode`; the facade now returns after opening the scene and lets the next update request PlayMode.
- The editor later recovered and completed the request, so no restart is required. The fresh report narrows remaining work to four contract families: strict `CharacterId` ingress, authored faction/offense region canonicalization, body-health injury projection restoration, and early-V18 regular-customer ID normalization.

## 2026-08-06 Phase 122 closure findings

- Operational early-V18 IDs were a real compatibility surface, not arbitrary runtime strings. Invasion, faction-route, return-prisoner, and incident actors restore under canonical `character:` IDs, so every typed character reference to them must canonicalize before aggregate cross-reference preflight.
- A union field cannot preserve an ID merely because it is not `staff:` or `world:`. The correct discriminator is the exact `CharacterId.TryCanonicalizeV18Restore` grammar; only unrecognized wildlife/building/transaction/runtime identifiers remain untouched.
- Numeric `int.TryParse` alone is not a persistence grammar. It accepts `+1` and `01`, and equipment repair additionally authors a minimum-width `D6` suffix. Validators now reconstruct the exact invariant string emitted by each generator.
- Sequence watermark validation and generation must agree at the maximum value. Restoring `int.MaxValue` or `long.MaxValue` is safe only when the next command fails before state, physical items, reservations, or world actors are mutated.
- Consumables previously shared one prefix between external idempotency keys and automatic IDs. The `auto:v1` namespace removes that ambiguity while legacy exact D16 values remain reserved for V18 watermark compatibility.
- The former equipment/expedition verifier could click a covered or clipped Button by directly invoking pointer handlers. It now proves full ScrollRect visibility, actual top EventSystem raycast ownership, and successful dispatch before accepting the flow.
- The former research verifier proved layout and captures but not the actual detail contract. It now selects `research:equipment:powered-armor` and compares visible detail text against runtime progress, an independent deduplicated prerequisite DFS, work/day estimates, the reward catalog, and the exact lock blocker.
- Fresh non-PlayMode evidence is complete: Unity compile clean, architecture `131/131`, transactional restore `33/33`, synchronous final acceptance `33/33`, and ArchitectureMetrics hard gates all pass. The only missing completion evidence is the fresh seven-target/30-capture/54-section PlayMode matrix and Console `0/0`, which cannot safely start while the loaded Title scene remains dirty.

## 2026-08-06 Full World 54-section restoration narrowing

- The second standalone run preserved `registeredSections=54`, `capturedSections=54`, and `postRoundTripSections=54`; remaining failures are now concrete authored-data and fixture contracts rather than composition or section-registration defects.
- The root content catalog registered two SOs for every dungeon faction StableId: six obsolete shallow definitions and six richer authored definitions. The obsolete registrations were removed, and the runtime adapter now fails explicitly if a duplicate StableId returns.
- Human support sites persisted `region:human-campaign`, but that region does not exist in the strategic world. An empty authored region is the correct contract because strategic-site registration resolves it from the actual tile.
- Empty settlement state is still a valid state, but its persistence contract requires explicit empty collections. Passing null from the QA baseline fixture was invalid test setup rather than a reason to weaken the runtime contract.
- Invalid CharacterId acceptance tests should prove rejection atomicity and identify the exact offending serialized value; coupling the test to a particular validation layer's English phrase produced a false failure when an earlier aggregate validator rejected the same payload.
- Unity YAML list indentation is part of the serialized contract. Four retained faction references were briefly written at column zero, which truncated effective deserialization before `coreSessionRules`; the cascading missing-injection exceptions were downstream symptoms of that single malformed asset edit.
- `DungeonGameSaveDebugScenarios` was rewriting `research.blueprints` with hardcoded section version 3 even though `BlueprintResearchSaveSection` now registers version 5. QA mutation helpers must source version and restore phase from `IDungeonSaveSectionRegistry`, just like production manifests.
- Aggregate rejection was already atomic for `Named Hero`; the remaining contract failure was diagnostic. Including the exact raw ID in preflight errors makes the failing serialized value observable without weakening canonical-ID validation.
- Strict V18 validation correctly rejects QA-only identifiers and split state. A fake recipe ID is not authored content, an operation-variable start day cannot exceed the captured current day, and `facilityDamageCount` must equal the set of canonical damaged `BuildingInstanceId` values.
- Aggregate cross-reference preflight can reject a normalized legacy/canonical collision before the character section's own validator runs. The acceptance contract should assert the exact duplicate canonical ID and atomicity, not require one downstream layer's wording.
- V18's explicit no-migration boundary means a full-game round-trip fixture must not replace an authored current research reward with the removed `recipe_battlefield_dining_2` alias. Testing that alias inside a V18 manifest contradicts the compatibility policy.
- Removing the owner actor invalidates aggregate references before character-section owner-count validation. Either layer is a valid fail-closed boundary; the contract must identify the missing owner and prove the live/staged world stayed unchanged.

## 2026-08-06 physical projection and restore-report findings

- A stored equipment instance is not valid without a max-stack-one physical stack and `sourceStackId`. QA setup must use the same physical-item materialization/link path as production instead of inserting only into the equipment dictionary.
- `DungeonPhysicalItemSaveData.stacks` and `uniqueItems` are complementary projections of one item identity: the former owns location/quantity and the latter owns versioned unique components. Matching `ItemInstanceId` values across those collections are required, not duplicates; uniqueness is enforced within each collection.
- `DungeonCandidateSaveRestoreStage` records counts only when its candidate implements `IDungeonRestoreReportContributor`. Offense and invasion candidates previously published correct state but left `RestoredExpeditionCount` and `RestoredIntruderCount` at zero, producing false-negative round-trip reports.
- Authored NPC staff content is a real boot/runtime dependency. Creating a temporary `CharacterSO` in Editor QA concealed the catalog gap and violated the SO single-authority rule; the root catalog now contains an explicit staff definition.

## 2026-08-06 final integration findings

- Localized failure contracts cannot be validated by searching rendered English words. Production verification now checks locale-neutral structure and parameters, while domain code supplies only the parameters declared by `ProductionMaterialsMissing`.
- Market sale is a real dependency-graph consumer, but its demand query must be independent from the mutable stock-policy runtime. Inheriting the query interface from the runtime made VContainer select the runtime during query resolution and created a composition cycle. A read-only projection plus `RoutingOwnedExternally` exposes demand without duplicating hauling or settlement commands.
- Restore publication must not run normal-session population maintenance. Immediate replenishment after publishing a restored Character World changed the state before the canonical recapture and made an otherwise valid transactional restore appear non-deterministic.
- Non-interactive TMP labels created over buttons still receive raycasts unless explicitly disabled. Setting generated static labels to `raycastTarget=false` restored actual EventSystem top-hit ownership instead of weakening the verifier.
- A pointer verifier must wait after rebuilding a dynamic UI surface. Same-frame clicks observed stale layout/raycast geometry; a capture-ready frame boundary made the test match player-visible state.
- TMP's default Liberation Sans fallback was not valid Korean evidence and emitted thousands of glyph warnings. Verification must resolve the same Korean font service used by production UI.
- Unity layout groups can dirty serialized RectTransforms during verification even when gameplay state is restored. Cleanup is safe only for exact diagnosed residues and only after a save-as-copy byte comparison proves the scene matches the on-disk asset; the final cleanup met that condition.
- The final integrated evidence is authoritative: seven of seven targets, 30 fresh captures, Full World `54/54/54`, canonical persistence restoration, and Console warnings/errors/exceptions/asserts `0/0/0/0`.
# 2026-08-06 V19 life-simulation implementation baseline

- The worktree is clean before V19 work. The accepted V18 baseline has 54 staged rollback-free save sections, seven final PlayMode targets, 30 captures, and Console Error/Warning 0/0.
- `IGameCalendar` and `GameCalendarRuntime` currently expose day/hour/time-of-day only and hardcode 180 seconds per day. They have no year, season, climate front, or regional time projection.
- `CharacterRuntimeProfile` currently retains `CharacterSO Source`, `CharacterSpeciesSO`, and trait SO references; character saves contain no age, kinship, household, reproduction, disease-immunity, grief, trauma, or career state.
- Existing mood memory caps each factor independently, so one grief factor per deceased would bypass the approved aggregate -20 cap. V19 must project one aggregate grief factor.
- Existing A* already has `IGridTraversalCostPolicy` and `GridTraversalContext`, while environmental cells expose temperature/air/light plus a version. Child safety belongs in a typed actor-aware traversal policy and cache key, not in job filtering alone.
- Crop plots currently persist growth/water/yield only. Seed lots, fertility, rotation, pests, disease, and cultivar genomes require a new Aggregate while physical seeds remain under the item repository authority.
- Existing body-health owns infection and anatomy damage. Named pathogens, immunity, and outbreaks must project into that authority rather than introducing a second health pool.
- Current research assets are exactly 168 nodes ending at 7247. The approved 48-node manifest reaches 216 nodes; the 7271 prerequisite closure is 108 nodes / 95,448 work / 964.1 effective days.
- V19 is a deliberate new-game-only generation. V18 and below must be rejected before any live-state mutation with the approved incompatibility message.
- The current domain boundary supports an incremental V19 cut: CoreSession is engine-free, Characters owns typed `CharacterId`, Species owns authored species SOs, and Grid owns traversal-policy contracts.
- `CharacterSpeciesDefinitionSO` currently requires needs/environment/anatomy/incident content but has no required life-history or reproduction content. Those definitions belong in the same fail-closed catalog validation path.
- `GridTraversalContext` still stores a Unity `Object` and hashes it with `GetInstanceID()`. V19 must replace that cache/policy identity with `CharacterId`, movement intent, and a safety authorization token.
- Unity requires each serialized `ScriptableObject` type used as an asset to have a matching source filename/MonoScript. Grouping V19 SOs in one file compiled but created `m_Script: {fileID: 0}` assets, so the content contract now enforces one SO type per source file and repairs only its own newly generated population assets.
- Typed traversal contexts now carry `CharacterId`, movement intent, safety authorization, and environment/combat/life/policy revisions; Unity `Object` and `GetInstanceID()` are no longer part of the path key. Supervised routes deliberately bypass path-result caching so a moved supervisor cannot leave a reusable stale route.
- The existing default-assembly `CharacterDeathEvent` still carries `CharacterActor` for legacy consumers. The V19 serializable ID-only payload is therefore `CharacterLifeDeathRecord`; a later application adapter must translate the legacy Unity event once instead of allowing two same-named authorities.
- The environmental simulation has no authored fire authority yet. V19 hazard routing exposes an explicit overlay command, projects active combat and severe filth now, and leaves fire publication to the future fire Aggregate rather than inventing hidden state.
# V19 climate and population-health findings (2026-08-06)

- The approved seasonal curve can live under the existing absolute calendar without creating a second time authority. Five climate-zone SOs and six weather-front SOs now project deterministic daily weather from the calendar and random-stream state.
- Disease exposure must remain room/cell aggregated. The runtime therefore records one weighted exposure batch per disease and room instead of comparing every character pair.
- `condition:core-corrosion` cannot be represented honestly as a zero-duration contagious disease. It is now an explicitly chronic, non-contagious environmental condition with separate apply and maintenance-removal commands; it never creates vaccine or epidemic state.
- Existing body-health anatomy nodes remain the only physical-health authority. Population health emits daily burdens and the application adapter projects them into the matching breathing, digestion, filtration, consciousness, or core anatomy node.

# 2026-08-06 V19 character profile and aging-treatment findings

- `CharacterRuntimeProfile` can be value-only without moving authored stat calculation into runtime state: the factory resolves the root-catalog SOs once, copies immutable gameplay values, and returns a profile containing IDs and values only.
- The root catalog had 14 character archetypes but one legacy Adventurer archetype had no species definition. Treating that as a default player species would silently corrupt life rules, so Adventurer is now explicit enemy-only authored content while the nine player species remain unchanged.
- Long-term aging care must alter the daily life transition, not rewrite captured age afterward. Rune hibernation therefore applies a 0.25 biological-aging multiplier, chronic care freezes condition progression, and temporal stasis blocks both aging and new age-condition rolls only while its facility and power contract are currently valid.
- Temporal stasis maintenance runs before the daily life tick. A supply or power failure changes the effective mode to normal for that day and never creates retroactive catch-up aging.
- Whole-body regeneration follows the approved severity contract exactly: mild/moderate conditions resolve, severe conditions drop two stages, and critical/organ-loss states are preserved for the body-health/surgery authority.
- `CharacterDeathEvent` is now an ID/day/location/witness value payload. Actor lookup exists only in application adapters, so death persistence and social simulation no longer retain Unity objects.
- Age-condition severity changes now damage authored anatomy nodes through the existing body-health Aggregate. Fatal age-condition organ failure carries an explicit cause; the owner exception clamps the same authoritative health state to one instead of creating a second vitality authority.
- Kinship cold archival must prune links before tombstones. Otherwise a removed tombstone leaves an invalid saved relationship reference. The implemented order preserves parent edges reachable within depth three from living characters, retains recent deaths for 120 days, and only then aggregates unrelated old deaths by household/generation.
- Reproduction completion previously stopped at `Completed` and had no world publication consumer. A daily application adapter now advances processes, publishes one value-profile character, registers newborn/golem life state, writes parent or guardian links, and stores `resultCharacterId`; this ID is preflight-validated and prevents duplicate births after restore.
- Existing authored characters were being created with `ReproductiveRole.None`. Publication now deterministically derives the applicable role from the persistent CharacterId and authored reproduction mode, then rebuilds the immutable runtime profile before life registration.
- Whole-body regeneration used to change only biological-age and condition state. It now prevalidates all authored anatomy targets before consuming the physical treatment item and repairs the exact accumulated mild/moderate or severe-to-mild health fraction afterward.

## 2026-08-06 V19 funeral, career, and physical disease-route findings

- Funeral and festival rules cannot be represented as mood-only calls. The application service now requires the deceased's authored funeral culture, a live tombstone, living participants, and a built memorial facility with the exact semantic ritual tag before grief is converted.
- Career retirement needs enforcement at both assignment and continuation boundaries. Checking only the work picker leaves direct orders and already-running unsafe work as bypasses, so the same policy is applied by the handler registry and ongoing duty controller.
- Mentoring reuses the existing character progression XP authority and persists only assignment/idempotency state. It never stores a second skill ledger or copies active skills.
- Population health already owned probability, immunity, outbreaks, and anatomy burden, but only ambient air/droplet exposure reached it. Contaminated meals and successful world-water consumption now publish physical exposure, while the water aggregate persists a concrete disease ID and rejects non-water disease definitions.
- Slime contamination is now a real species incident handler. It creates physical filth and deterministically contaminates the nearest real water source within four cells with `disease:slime-blight`; no synthetic global infection scan is used.
- 2026-08-07 V19 cohesion review: line count is now a review signal rather than a decomposition command. `CharacterActor` 819, `CharacterBodyHealthRuntime` 1,291, and `DungeonAggregateReferencePreflight` 1,623 are cohesive Unity facade, health application/Aggregate boundary, and atomic cross-Aggregate preflight respectively. `PhysicalAgeTreatmentRuntime` needs ten explicit authorities to keep item consumption, life mutation, anatomy repair, facility/power validation, and calendar maintenance visible and atomic; a dependency bag or split command would weaken the design.
- Fresh ArchitectureMetrics passes at `1,431 files / 4,532 types / mutable statics 0 / review types 3 / hard oversized 0 / review constructors 1 / hard large constructors 0 / content escapes 0 / direct session mutations 0`.
- V19 short definition files are Unity-authored SO boundaries, the save sections are already co-located, and the event/application adapters perform real cross-domain projection rather than forwarding. No new V19 merge candidate was found.
- The current synchronous final acceptance is `33/33 PASS`. The final project-local Unity MCP PlayMode request was safely rejected before state capture because dirty `Assets/Scenes/GameplayScene.unity` would be unloaded by the required switch to Title. The rejection report proves `consoleWarnings=0`, `consoleErrors=0`, `consoleExceptions=0`, and no persistence snapshot was required; it is not final 32-capture evidence.
- The first standalone V19 UI retry exposed four stale root objects named `RegularCustomerRuntime_Test`/`RegularCustomerRewardPromotion_Test`. Those QA objects made the strict composition query see five `RegularCustomerRuntime` instances and disabled the LifetimeScope; all other injection exceptions were cascades. The four exact QA roots were removed with Unity Undo, `VerifyRuntimeEvents` now uses `try/finally`, and two consecutive scenario runs leave `runtimeCount=1; debris=0`.
- After cleanup, the standalone character-summary/medical matrix passes at both target resolutions with six fresh Unity captures. Population text includes life, kinship, disease/immunity, career, and child-safety sections; all tab/button/modal flows use EventSystem hit tests and dispatch. The verifier captured `errors=0`, `warnings=0`, and `RESULT=PASS`.
- Stale Console history from the rejected composition attempt was cleared with `Unity_ReadConsole(Action=Clear)` through the project-local MCP bridge. A fresh Error/Warning query returned zero entries; this is the current stopped-Editor baseline, while the separate seven-target/32-capture coordinator remains pending the dirty-scene gate.

## 2026-08-07 V19 final integration findings

- Asset count is not catalog authority. The project had 216 `ResearchProjectSO` assets while `GameDomainContentCatalogSO` exposed only 168, so runtime/UI validation correctly failed. Research rebuild now refreshes only the research slice of the authoritative catalog and preserves every other curated definition.
- A broad `Resources` reindex is unsafe when legacy shadow assets still exist. Six shallow dungeon-faction assets duplicated the six canonical authored StableIds and caused downstream composition failures; full rebuild filters those exact legacy shadows while runtime duplicate validation remains fail-closed.
- Responsive route editors need density policy based on actual branch count. The presenter selects compact rows for more than six consumers, and the factory remains the sole owner of row geometry. This fixes 11-route portrait visibility without merging policy and Unity construction responsibilities.
- Final-target timeout begins before Unity enters PlayMode, so it includes blocking scene activation time. A 900-second limit was invalid once warm-editor scene integration reached 915 seconds. The 1,800-second limit is not a weaker gameplay test; it prevents infrastructure time from preempting the verifier.
- Timeout resume is safe only when it revalidates the failed report, exact sequential PASS progress, fresh verifier reports and PNG dimensions, required report markers, persistence restoration, and a zero-warning/error/exception/assert console record. Any functional failure or stale/missing evidence rejects resume.
- Final authoritative evidence is now complete: Architecture `154/154`, Transactional Restore `33/33`, synchronous acceptance `33/33`, Full World `63/63/63`, research 216, final targets `7/7`, captures `32`, persistence restored, and Console `0/0/0/0`.
- The final cohesion scan remains `1,431 files / 4,532 types / mutable statics 0 / review types 3 / hard oversized 0 / review constructors 1 / hard large constructors 0`. Current review sizes are `CharacterActor 819`, `CharacterBodyHealthRuntime 1,291`, and `DungeonAggregateReferencePreflight 1,623`; no new over-separation merge candidate was introduced.
# 2026-08-07 V20 content-density implementation baseline

- The approved V20 scope keeps the existing 216 research nodes and adds exactly 450 hand-authored definitions; bulk template-generated content and research exclusivity are explicitly out of scope.
- V19 is already functionally complete with the final synchronous and Unity MCP gates recorded as passing. V20 is a new compatibility generation, not a continuation of unfinished V19 feature work.
- The authority boundary is fixed: ScriptableObjects own immutable authored content; plain C# Aggregates own mutable run state; save DTOs contain IDs and values only; root catalog registration is mandatory; missing content fails loudly.
- The requested runtime additions are character narrative, society events, faction campaigns, SO-authored enemies and encounters, ecology/health/cultivar expansions, nine non-terminal milestones, and deterministic EndlessAge composition.
- The existing worktree contains extensive user-owned changes and generated QA evidence. V20 implementation must use narrow patches and must not reset, normalize, or overwrite unrelated files.
- The persistent planning catch-up detected the approved V20 plan and implementation request as unsynchronized context. Phase 124 was added to the root plan rather than replacing the completed V19 history.
- Existing V19 catalogs currently hard-fail on exactly eight disease definitions and exactly four festivals. V20 must deliberately version those validators to 16 diseases and 16 festivals rather than merely adding assets.
- The project already has named Content, Offense, Factions, Wildlife, Run, Characters, Items, Economy, CoreSession, Infrastructure, and Foundation assemblies. V20 should extend these existing domain boundaries and avoid introducing a new assembly for every content subtype.
- Existing save sections implement strict preflight, detached staging, and rollback-free publication contracts. The five V20 sections must follow this established pattern and dependency topology instead of creating an alternate persistence framework.
- `GameDomainContentCatalogSO` stores heterogeneous immutable definitions and already exposes typed `GetAll<T>()`; V20 can register new SO types without adding parallel root catalogs.
- `CharacterTraitSO` currently exposes only stats, model modifiers, combat abilities, and thermal protection. V20 requires authored behavior/mood/event-weight consequences to satisfy the approved content contract.
- `OffenseEncounterSO` currently stores only strength range, elite/boss flags, and enemy count entries. Objectives, battlefield modifiers, deadlines, and reward/counter tags require a schema extension.
- Recoverable inspection error: initial guessed locations for `HeritableTraitDefinitionSO`, `FestivalDefinitionSO`, and wildlife definitions were wrong. The real files are under Species/Core and Wildlife/Core; the failed read changed nothing and those guesses will not be repeated.
- `HeritableTraitDefinitionSO` currently contains only ID, name, aptitude modifier, and compatible species tags. V20 must add category, incompatibility group, gameplay stat/mood/behavior consequences, and validation while retaining the existing fields for migration.
- `FestivalDefinitionSO` currently contains only ID/name/date and a grief-conversion flag. V20 must add physical input, facility, participation, and outcome records; the existing four assets need explicit migrated values.
- Wildlife definitions are currently converted to immutable runtime definitions with diet, habitats, needs, predation, husbandry, and yields. V20 ecology can extend this existing contract with nesting, seasonal activity, migration, disease-vector, and prey links rather than introducing a second wildlife state authority.
- The current `OffenseEncounterCatalog` is confirmed as a hard-coded enemy/ability content authority. Its ally projection is reusable, but enemy templates and abilities must be moved to root-catalog SO definitions and the static enemy factory removed after the runtime adapter is connected.
- Root save version is `DungeonGameSaveData.CurrentVersion = 19`; the approved V20 change point is `InfrastructureSavePrimitives.cs`. Existing QA and full-world constants still assert 63 sections and must move to 68 with the new manifest.
- Recoverable inspection issue: a broad registration search returned a non-zero pipeline status because of output limiting, while still producing valid matches. Future registration reads will target concrete files instead of broad pipelines.
- `IGameContentDefinitionSource` is the correct read-only authored-definition port and `ResourceGameContentCatalog` already projects `GameDomainContentCatalogSO` through it. New content catalogs should depend on this port rather than on Resources or the root implementation.
- Character life domain state is consolidated in `Models/Species/Core/CharacterLifeDomain.cs`, while the V19 section adapters are consolidated in `Services/Infrastructure/Save/V19SimulationSaveSections.cs`; guessed one-class-per-file locations were incorrect.
- Recoverable inspection errors: two guessed Character Life file paths did not exist, and one filename-pattern search returned no matches. A class-declaration search resolved the actual consolidated files; no files were changed by the failed reads.
- V19 save sections use `DungeonStrictJsonSaveSection<TPayload, TCandidate>`: raw JSON shape is checked before deserialization, a detached candidate is built during staging, and commit only publishes that validated candidate. V20 sections will use the same base and implement required-array checks.
- `CharacterLifeRuntime` uses `DungeonRuntimeAggregateRootStore` with read/current and clone-on-write access. Character narrative should use the same root-store publication model so full-world restore can swap one staged aggregate root atomically.
- Recoverable inspection error: the guessed standalone `DungeonRuntimeAggregateRootStore.cs` path does not exist. Its declaration will be located by type search before use; no mutation occurred.
- `DungeonRuntimeAggregateRootStore` is defined with the save contracts in Foundation, and VContainer already registers it once for all aggregate domains. Narrative and campaign state should be added to this shared root, not to new singleton dictionaries.
- `CharacterLifeApplicationAdapter` establishes the approved daily event pattern: subscribe through `IGameEventBus`, mutate the Aggregate once at day end, and publish value events. The narrative scheduler can follow it without per-frame evaluation.
- `DungeonCharacterRegistration` is the correct composition point for narrative catalog/runtime/adapters; save section registration remains centralized in `DungeonSaveRegistration`.
- Unity MonoScript stability requires each new concrete SO type to remain in a matching source file. Shared requirements, effects, choices, and metadata are consolidated in one contracts file to avoid artificial one-record files.
- The active project version is Unity `6000.3.8f1`. The main Editor process is responsive, but its Editor log and Offense assembly predate the V20 edits, so no compile success can be claimed until an explicit asset refresh occurs.
- The project-local player bridge is not a substitute for Editor commands and was unavailable during the first refresh check. No OS input or additional Unity process will be used to force import while the project lock is held.
- Project policy intentionally disables automatic Unity MCP registration and provides `tools/unity-mcp/Invoke-ProjectUnityMcp.ps1`, which resolves only the live Editor for this exact project and serializes an explicit tool call. This is the approved root-only Editor path.
- `Invoke-ProjectRefresh.ps1` performs a forced synchronous `AssetDatabase.Refresh` followed by a clean script compilation request through Unity MCP. It does not use OS input or start another Editor, so it is the correct compile gate.
- Unity's current MCP package exposes `Unity.RunCommand` and `Unity.GetConsoleLogs`; there is no separate refresh tool. A RunCommand-triggered domain reload necessarily disconnects the relay before its JSON-RPC response is flushed, so compiler success must be read from the actual Editor/Tundra output.
- Unity republishes its project-scoped connection at `C:\Users\vulpo\.unity\mcp\connections\bridge-ca5ada59-34544.json` after reload. Discovery retries should key off this exact file/Editor log rather than recursively scanning temporary directories.
- The ten current life-history species IDs are the exact case-sensitive tags `Adventurer`, `Beastkin`, `Demon`, `Golem`, `Harpy`, `Kobold`, `Myconid`, `Orc`, `Slime`, and `Vampire`. `CharacterSpeciesId` stores these tags directly, so default culture authoring must use them rather than invented `species:*` identifiers.
- Phase 124's first authored asset batch can safely live under a dedicated `Assets/Resources/SO/V20/Narrative` subtree. `GameDomainContentCatalogSO.SetDefinitions` sorts and de-duplicates references, so the builder can replace only the five V20 narrative types while preserving every unrelated user-owned catalog entry.
- The five narrative definition contracts already enforce the key authoring invariants: backgrounds require a memory, ambitions require a positive target and reward, major life events require 2-4 mechanical choices, automatic events require effects, cultures require exact 120-day assimilation and etiquette, and practices require a parent culture and success effect.
- The narrative authoring manifest now has an explicit count guard for 12/18/32/10/20 and separately asserts the 20 major plus 12 automatic event split. Its catalog update filters only the five owned V20 types, preserving unrelated definitions in the dirty root catalog.
- Unity's editor command completed the 92-definition asset transaction without a domain disconnect. This confirms the dedicated V20 subtree and type-scoped catalog replacement work with the current root catalog.
- The current save registry has 63 sections and central registration in `DungeonSaveRegistration.RegisterSections`. V20 can reach the required 68 without replacing existing section IDs by adding exactly five strict sections and changing the root compatibility generation from 19 to 20.
- `DungeonRuntimeAggregateRootStore` already supports clone-on-write detached restore candidates by aggregate state type. The five V20 aggregates should use `GetOrCreateWritable` and `Replace`, so a late restore failure remains rollback-free and does not mutate the live root.
- The root catalog currently contains exactly nine legacy `CharacterTraitSO` assets under `Character/Traits`; V20 should preserve their GUIDs and add 47 new assets for a total of 56. The extended trait contract now rejects stat-only definitions unless they also author a behavior preference, mood reaction, or event weight.
- No authored `HeritableTraitDefinitionSO` assets were found in the current resource tree. The planned 24 hereditary traits can therefore establish the sole asset authority without migrating a competing legacy asset set.
- Existing general trait numeric IDs are 101-109. V20 additions can use a non-overlapping 200-246 range while preserving the nine legacy GUIDs and identifiers.
- The trait builder enforces the exact hereditary category split (Anatomy 6, Metabolism 6, Arcane 4, Reproduction 4, Immunity/Longevity 4), validates all consequence records, and replaces only V20-owned ID ranges/types in the root catalog.
- The V19 festival assets do not satisfy the extended V20 physical-input/outcome contract until upgraded. The V20 world builder therefore owns and rewrites all 16 festival definitions while preserving the four established stable IDs.
- Seasonal event definitions already enforce at least two affected domains and a real mechanical effect. The authored batch fixes seven events per `Spring`, `Summer`, `Autumn`, and `Winter` for 28 total.
- The completed first authored block is exactly 203 net-new V20 definitions. The festival catalog contains 16 total because the 12 new festivals replace/extend the four preserved V19 stable IDs rather than counting the originals as new content.
- The six canonical factions for V20 long arcs are the existing dungeon factions: `faction:dungeon:beastkin`, `demon`, `golem`, `harpy`, `kobold`, and `myconid`. The parallel `Factions/Dungeons` assets share those IDs and must not be treated as additional factions.
- Faction arc/chapter/contract, guest request, and service incident contracts are already typed and require mechanical outcomes; chapters and incidents reject single-outcome narrative text.
- Non-craftable faction relics should be authored as `GenericItemDefinitionSO` assets in a dedicated V20 item subtree, registered in `ItemDefinitionCatalogSO`, and additionally referenced by each faction arc. They do not need a production feature or recipe; max stack one makes their physical identity explicit.
- The item catalog exposes an editor-only `SetDefinitions` API, so the faction builder can replace only the eighteen `relic:faction:*` IDs without invoking the legacy all-content rebuild.
- The faction/service batch contributes 100 net-new definitions to the 450 manifest: 82 domain definitions plus 18 physical relic items. Each arc owns exactly six chapters, three contracts, and three relic IDs, and chapter three names a real cross-faction dependency.
- The six existing encounter stable IDs are `encounter:01` through `encounter:06`. The V20 combat builder should preserve and rewrite those assets, then add 30 new encounter assets, while enemy archetypes/abilities/modifiers use dedicated new SO authority.
- `OffenseEncounterCatalog` still contains the forbidden code-built enemy templates and ability factories. Ally projection is independent and can remain; `CreateEnemies`, `GetEnemySummary`, the template class, and hardcoded enemy ability helpers must be replaced by injected SO catalogs.
- The combat assets now pass cross-reference validation from every enemy to its 1-3 abilities and from every encounter to registered enemy IDs. Each role also has a nonempty formation and at least one counter tag, preventing stat-only archetype duplication.
- `WildlifeSpeciesSO` has useful diet/habitat/husbandry data but lacks explicit predator/prey links, nest behavior, breeding season, migration, disease-vector, and seasonal-activity metadata. V20 needs to extend this existing authority rather than introduce a second wildlife definition type.
- Crop cultivars are already represented by `CropGenomeDefinitionSO` over six diploid loci. The 12 V20 cultivars can be additional genome assets referencing the eight canonical crop IDs, with tradeoffs expressed directly in allele values.
- Recoverable inspection errors: one guessed Population/Species asset directory did not exist and a PowerShell-incompatible wildcard was passed to `rg`. Direct `Get-ChildItem` plus YAML field matching resolved the exact species tags; neither failure changed files.
- User clarification locks the human-enemy model: the 25 human entries are offense/defense tactical archetypes, not 25 fixed recruit templates. Every spawned enemy must be a normal persistent character instance with deterministic variation in age, background, culture, general/hereditary traits, skills, ambition, injuries, and loyalty. Capture and recruitment preserve that state and CharacterId; the former military archetype becomes origin/training history only.
- Combat readability remains authored at the archetype layer (equipment family, core abilities, formation, target priorities, counter tags). Individual variation may change proficiency and personality but must not erase the role's intended counters.
- After adding the Wildlife -> CoreSession dependency, the live project-scoped Unity Console reports zero errors and zero warnings. The ecology contracts now compile and the asset builder can be executed.
- `V20EcologyContentAssetBuilder.Build()` completed inside Unity with both compilation and execution success. The 33-definition ecology contribution is now authored and root-registered, bringing the net-new V20 manifest to 432; only 9 milestones and 9 physical landmarks remain for the exact 450.
- The current offense path proves the user's concern: `OffenseEncounterCatalog.CreateEnemies` still constructs transient combatants from hard-coded `EnemyTemplate` values and generated string IDs, so no normal CharacterRuntimeProfile exists to preserve when captured.
- The character profile factory already provides the correct authored/value-only boundary, but `CharacterSpawnRequest` currently carries only archetype/species/visual/reproductive role/traits/aptitudes. V20 enemy individuality therefore needs a separate persisted origin/narrative record rather than polluting the immutable combat archetype or recreating state at recruitment.
- Captivity commands already operate on the real `CharacterActor` and `captiveId`. The safest cut is to ensure invasion/offense enemy publication creates a real persistent actor first, then let `TryRecruit` remove only captive control state while leaving identity/profile/body/narrative state untouched.
- `CaptivityRuntime.TryRecruit` already preserves the actor object and identity: it changes captive status, character type, AI pause, lifecycle, and door-access registration only. It does not replace profile, stats, injuries, or ID. The missing guarantee is entirely upstream: abstract offense combatants have no corresponding persistent character/profile before capture.
- Offense battle save currently persists combat numbers/statuses but omits display/species/archetype/origin/narrative identity. A V20 enemy-instance record must be authoritative outside the transient battle projection, while the battle section references its CharacterId.
- Offense prisoner rewards currently queue only an integer amount. On return, `TrySpawnPrisoner` deterministically creates a generic intruder from `IInvasionIntruderDataProvider`, initializes it only at materialization time, and assigns a new ID derived from arrival sequence. That path destroys encounter-specific identity by design and must carry saved enemy individual payloads/IDs instead.
- `CharacterActor.Initialize(CharacterSO, CharacterSpawnRequest)` already accepts a fully chosen profile while retaining the authored prefab/archetype asset. This enables enemy generation without runtime SO synthesis: choose IDs/aptitudes deterministically, call the normal profile factory through actor initialization, then persist origin/narrative state by CharacterId.
- Fresh life registration currently filters to owner or NPC workers. Captured intruders therefore need explicit life/narrative registration at enemy publication (not delayed until recruitment), otherwise their age and narrative could be regenerated or missing after recruitment.
- The current narrative Aggregate already supports deterministic background/default culture selection and 4/2 hereditary traits; extending its record with immutable enemy-origin fields and a bounded loyalty value avoids a parallel prisoner-personality store.
- The combat builder authors 36 roles over Human, Beastkin, Demon, Golem, Harpy, Myconid, Construct, and Truth tags. Enemy individual generation must map non-life tags such as `Construct`/`Truth` to an authored phenotype (Golem/Adventurer) while preserving the displayed tactical species tag separately.
- The narrative runtime/catalog code exists but `DungeonCharacterRegistration` does not register it yet. This explains why authored narrative content is not part of live character creation despite compiling; registration must be completed before enemy publication depends on it.
- The actual offense asmdef is `Assets/Scripts/Models/Offense/Core/DungeonStory.Offense.asmdef`; the prior miss was only a wrong directory level.
- The return-arrival Aggregate is the correct persistence owner for not-yet-materialized prisoners. Its state already survives expedition return barriers and contains deterministic arrival IDs; adding validated per-prisoner blueprints there closes retry/save identity loss without adding a sixth V20 section.
- Existing V18 return-arrival ID validation hardcodes `return:{n}:prisoner:{n}` suffixes. V20 may retain those CharacterIds while attaching individual blueprints, preserving stable cross-section references and minimizing migration surface within the new-game-only generation.
- The structural integration is in the intended location: `PopulatePrisonerIndividuals` runs immediately before the arrival enters aggregate state; restore validation runs before candidate creation; materialization reads the blueprint by the next unmaterialized index and registers life/narrative before publishing the downed actor.
- Enemy individual generation is deterministic from CharacterId + context + archetype ID, so save/load and retry do not consume mutable global RNG or generate a different recruit. The blueprint stores all chosen IDs/values; restoration validates authored references before actor creation.
- The canonical intruder CharacterSO stable ID is `character-archetype:2001`. Enemy profiles can use that visual/prefab archetype with a separately selected authored phenotype species; no runtime archetype SO is needed.
- The live content catalog is now safe to construct `EnemyCombatContentCatalog`: all 36 enemy assets have nonempty `training:*` IDs and valid generation bounds after the rebuild.
- VContainer registrations confirmed the narrative runtime was genuinely absent, not hidden in another module. It is now registered once with query/command/persistence facets, and the shared enemy/encounter catalog plus individual factory are registered in the existing offense composition boundary.
## Phase 124 enemy individuality findings

- The defense invasion path still initializes every intruder from one `CharacterSO` and assigns its persistent ID only inside `InvasionIntruderRuntime.PrepareBegin`; it does not currently use the new enemy individual blueprint.
- Captivity recruitment itself preserves an existing actor and CharacterId. The remaining defense defect is therefore generation and invasion persistence, not recruitment mutation.
- `CharacterLifePublicationService` samples initial age and birthday from a mutable shared random stream. Enemy blueprints must persist explicit chronological age, biological age, and birthday, then register through `ICharacterLifeCommand`, so queued/restored enemies do not change with spawn order.
- `DungeonStory.Invasion` already has a one-way reference to `DungeonStory.Characters`, so the shared serializable enemy blueprint belongs in the Characters model assembly. This lets invasion DTOs persist identity without making model code reference the default runtime assembly.
- The combat catalog contains 27 archetypes with `speciesTag=Human`, but exactly 25 belong to the five human enemy factions; the other two are neutral/shared human templates. Tests must classify by faction ID rather than treating species tag as faction membership.
- Stable FNV-style hash low bits were not sufficiently distributed for direct modulo selection over power-of-two content pools. A deterministic avalanche step is required before range selection; after it, the 100-instance probe exercises all twelve background definitions while preserving exact repeatability.
- The original V20 encounter assets authored objectives and battlefield modifiers, but the old battle session still treated every encounter as enemy extermination and ignored modifier values. Objective state now belongs to `OffenseBattleEncounterRules`, is persisted via reconstructible encounter content, and is evaluated at every battle boundary.
- Registering every transient enemy in Character Life/Narrative at spawn creates dangling cross-aggregate records after death or retreat. Keep the enemy blueprint in its owning combat/invasion aggregate and publish character domains only when a physical actor survives as a capture candidate.
- Faction effects need either one of the six canonical campaign faction IDs or an explicit contextual token. Silent semantic strings such as `faction:merchant-league` cannot be applied to the six-faction Aggregate and must fail catalog construction rather than fail years into a run.
- Society event caps need distinct ordinary and emergency counters. Counting total active events as ordinary capacity lets candidate ordering admit multiple emergencies and makes saved states impossible to validate consistently.
- The current V20 source layout is not excessively fragmented. The eleven sub-80-line files are Unity SO contracts or compact DTOs with independent asset/serialization identities; merging them would couple unrelated asset types. `V20CampaignRuntime.cs` is approximately 1,300 lines, but the size is a review signal rather than a failure and its catalog/contracts/aggregate rules remain one highly coupled feature boundary for now.

## Phase 125 design-document findings

- The previous 1,970-line overview was rich in subsystem detail but its top-level authority was still V17: it described 168 research nodes, a V17 save boundary, `truth_core` as the final run-ending victory, and pre-V19/V20 content totals. Adding isolated update notes would have left contradictory player expectations in one file.
- The consolidated document now treats the intended player experience as the organizing authority: physical place and logistics, memorable persistent people, progress with maintenance costs, multiple valid preparation paths, and history that survives across generations.
- Exact contracts and elastic catalog sizes are deliberately separated. Research 216, V20 net-new definitions 450, milestones/landmarks 9/9, and V20 save sections 68 are fixed; facilities and general items are described by approximate design scale plus catalog-authority language because those collections continue to grow.
- The 450-definition table contains 24 net-new category rows and sums exactly to 450. Preserved-and-rewritten definitions such as the original six encounters and four festivals are described in final totals but are not double-counted as net-new.
- The human-enemy explanation now distinguishes 25 faction combat archetypes from persistent individual characters. It explicitly preserves CharacterId, age, narrative, injury, loyalty, captivity, and recruitment continuity across offense and defense.
- V20 save architecture is documented as implemented while the unrun full 68/68/68 world round trip remains visibly deferred. Focused content, campaign, enemy-individual, encounter-objective, and modifier evidence is listed separately from final integration evidence.
- The rewritten Markdown is 1,009 lines and 55,786 bytes with 21 H2 sections and 66 H3 sections. It has zero stale V17/168/141/192 authority matches, zero Unicode replacement characters, twelve balanced code fences, and zero trailing-whitespace findings.
- The repository `.gitignore` ignores `docs/`, so the updated design document exists in the workspace but does not appear in `git diff` or `git status`. This is repository policy rather than a failed write; the file was read back and verified directly.

## Phase 126 exhaustive content-intent findings

- The facility asset subtree currently contains 349 `.asset` files, while the full V20 subtree contains 454. Raw file counts are not canonical content counts because buildings include structural pieces, duplicated/legacy paths, builder-owned partials, and potentially fixtures; canonical documentation must deduplicate by stable content identity and inspect ability modules.
- `BuildingSO` exposes presentation, authored `contentDefinitionId`, revision/source note, placement category/archetype, build conditions, unlock state, and polymorphic ability modules. An accurate facility intent entry must therefore use the stable ID plus actual ability/BOM/research data, not infer intent from filename alone.
- General traits directly author three player-facing consequence channels: behavior utility deltas, mood reactions with duration, and event-category weights. Every trait is validated to contain at least one of those consequences.
- Heritable traits directly author category, incompatibility, aptitude, species compatibility, and typed consequences for aptitude, environment, disease resistance, fertility, aging, anatomy capacity, or mana affinity. Combined hereditary modification is capped to ±25%.
- Life events, faction chapters, and service incidents validate two-to-four genuinely separate choices; automatic life events require effects. Seasonal events require at least two affected domains and a real mechanical effect. These typed fields are the authority for individual intent documentation.
- The first parallel discovery batch aborted because one optional `rg` pattern returned exit code 1 inside `Promise.all`. No files changed. Subsequent discovery uses sequential calls with optional-no-match exit normalization.
- Exactly 400 `BuildingSO` assets exist. Forty-two live under `Assets/Resources/SO/Buildings/RuntimeArchetypes` and are internal runtime archetype assets, not separately placeable design content. The exhaustive facility catalog scope is therefore 358: 349 placeable assets under `SO/Building` plus nine V20 milestone landmarks.
- The 358 facilities have unique numeric IDs and nonempty display names. Only the nine V20 landmarks currently carry the newer `contentDefinitionId`; older facilities remain canonically keyed by their stable numeric `DataScriptableObject.id`.
- Facility distribution is: root 9, captivity 10, combat 3, industrial 36, medical 13, modular 104, P1 34, production support 28, research-overhaul 96, service rooms 16, and landmarks 9. This is the documentation grouping, while individual entries remain mandatory.
- The V20 root contains the expected event-like concrete assets: 32 life events, 16 festivals, 28 seasonal world events, 36 faction chapters, 18 contracts, 14 guest requests, eight service incidents, 30 net-new encounter assets plus six preserved encounter assets elsewhere, and nine milestones/endings.
- The trait authority contains exactly 56 general trait assets (nine preserved plus 47 V20) and 24 hereditary trait assets.
- One attempted facility-list helper embedded PowerShell backtick-tab syntax inside a JavaScript template literal and failed at JavaScript parsing before any tool call or mutation. It was replaced with `[string]::Join([char]9, ...)`.
- Festival definitions are individually actionable rather than calendar flavor: each requires a concrete facility, physical item amounts, minimum participants, and separate success/partial/failure outcomes; some explicitly convert active grief.
- Encounter assets do not carry prose descriptions. Their individual intent must be documented from the authored objective, round/target conditions, battlefield modifiers, counter tags, reward items, and enemy compositions rather than invented narrative text.
- All 32 life-event names, descriptions, and authored choices were extracted from the generated SOs. Twenty are explicit two-way dilemmas and twelve are automatic history moments; the latter intentionally reward continuity without interrupting the player with a choice dialog.
- All 16 festivals carry distinct cultural or seasonal meanings. Their individual intent entries will connect the authored physical preparation to grief, mood, cohesion, or faction outcomes rather than merely list calendar dates.
- The guessed seasonal-event folder `V20/Society/SeasonalEvents` does not exist. The failed read was non-mutating; locate the class assets recursively instead of assuming the builder folder name.
- Located all 28 seasonal-event assets under `Assets/Resources/SO/V20/World/SeasonalEvents` and extracted their exact two-domain couplings. Each season contains seven authored pressures that connect farming, wildlife, health, logistics, expeditions, factions, guests, or facility operation.
- Extracted all 36 faction chapters. Their narrative conflicts are faction-specific, but every chapter currently reuses the same three mechanical stances (`support`, `bargain`, `refuse`) and the same rapport/obligation/grievance effect pattern. The design document must state this honestly rather than imply 36 mechanically unique choice sets.
- The generated faction-contract descriptions use generic templated copy and sometimes malformed Korean particles. Documentation should capture each contract's actual item, amount, deadline, and strategic purpose; the source copy remains a later content-polish defect.
- The exhaustive intent appendix now contains all 358 canonical player-facing facilities and all 32 life events as individual rows.
- The completed event intent catalog covers every authored player-facing event authority: 32 life events, 16 festivals, 28 seasonal events, 20 cultural practices, 36 faction chapters, 18 faction contracts, 14 guest requests, eight service incidents, 36 combat encounters, and nine milestones.
- The encounter builder is not fully bespoke authoring: it cycles six objectives and twelve battlefield modifiers over the ordered enemy array. Preserved encounter display names 03-06 do not match their current primary enemy faction/role. The document records the actual mechanics and flags these assets for rewrite instead of inventing nonexistent uniqueness.
- The completed trait intent catalog covers all 56 general traits and all 24 hereditary traits. The nine legacy general traits remain scalar-only and need behavior/mood/event channels for parity with V20 traits.
- Exact source-to-document ID comparison passes with zero missing and zero extra entries for 358 facilities and every event/trait category. The final Markdown has no replacement characters or trailing whitespace and retains balanced code fences.

## Phase 127 V21 research-expansion findings

- The user explicitly removed save-file migration from scope. V21 keeps only a clear V20-and-earlier incompatibility result; implementation must not add old-ID remapping, legacy DTO conversion, or partial restoration paths. Editor-time content rewrites used to author the V21 assets are not save migration.

- The live authored research catalog contains 216 project assets. The approved consolidation removes 36 stable IDs and keeps 180 survivor projects while preserving 138,824 total work.
- The existing reward index covers facilities, resource items, production recipes, combat equipment, and surgical procedures, but omits eight research-gated crops, twelve craft materials, and three environmental workwear definitions.
- Current project assets and research-gated definition assets are highly uneven: many projects expose one direct reward while fermentation, livestock cuisine, compost, vaccination, and other families expose large flat lists. V21 needs authored grouping plus a broader reverse index, not a second completion authority.
- Existing combat equipment authoring provides six starting definitions and research-gated weapon/armor/shield families. Sparse gates include ballistics, dark foundry, steel, tailoring, tanning, and powered armor; the V21 additions must fill distinct tactical roles rather than scalar duplicates.
- The repository working tree already contains user-owned planning-file changes and a deleted `COPILOT_HANDOFF.md`; Phase 127 must preserve that deletion and all unrelated content.
- The final authored V21 asset audit reports exactly 180 research projects, 180 unlock bundles, 61 combat equipment definitions, and 138,824 total research work. All 36 absorbed IDs have zero references in non-Editor runtime C#.
- `V21ResearchConsolidation` is compiled only under `UNITY_EDITOR` and documents itself as an asset-authoring map. Runtime compatibility exposes only the V21 equality check and the exact V20-or-earlier rejection reason, so it cannot become an implicit save migration table.
- The ten V21 ammunition IDs are all present as physical item definitions. `supply:defense-mixed-ammo-box` is intentionally a `FinishedGood`, preserving the requested ten ammunition kinds while still giving defense-supply research a physical consumable.
- A stale architecture ratchet still expected save root V20 and one research scenario still compared the V5 incompatibility copy. Both were updated to the shared V21 compatibility constant; no restoration behavior changed.
- After the final test-only edits Unity compiled all 2,172 evaluated items successfully. The project-scoped MCP relay subsequently timed out on dynamic validation responses despite a responsive Editor; no validation-failure marker was emitted, so a fresh live Console 0/0 capture remains deferred rather than claimed.
# 2026-08-08 Phase 128 actual-gameplay connection audit

- Registration and count validation are not sufficient evidence for V21. The accepted completion path is command/AI entry -> authoritative requirements -> physical reservation/durability -> domain effect -> atomic publish -> V21 restore.
- `V20CampaignRuntime` currently resolves society events and applies internal campaign effects before `V20CampaignApplicationAdapter` applies item/money effects. A later item failure therefore leaves the event resolved and campaign state mutated; this is the first blocking defect.
- `V20ContentEffectKind` declares mood, trauma, skill XP, health, relationship, work delay, disease exposure, and ambition progress effects that have no production executor. Requirement evaluation also substitutes total character/building counts for life-stage, trait, health, operational-state, and capability checks.
- V21 `GuestSupplies` attaches unrelated medical and operational goods to guest requests. These are fabricated sinks and must be removed before intended procedure/work consumers are counted.
- Loaded weapons retain only an ammunition count, so a selected physical ammunition ID is lost after reload/loading and special-ammunition behavior cannot be authoritative.
- Crop yield/seed-yield loci execute, but cold tolerance, heat tolerance, growth speed, and disease resistance do not participate in the authoritative crop calculations.
- The current worktree contains the broad user-owned V21 asset regeneration and prior phase changes. Phase 128 must use narrow patches and may not reset or normalize those changes.
- The aggregate-root store exposes detached staging only to the save registry. Ordinary gameplay commands mutate the live root directly, so content resolution needs its own prepared campaign candidate (or a carefully bounded public transaction) rather than abusing save restore.
- The item repository has stack reservations, but the current event adapter ignores them and merely recounts global stock before consuming stacks. Money also has no reservation token. On the single Unity main thread, a fully prepared batch can make commit operations non-failing, but the contract must reserve exact stack IDs and publish campaign/domain state last.
- Existing domain entry points can support typed effect handlers without a second state authority: actors expose mood and progression operations, `IGriefTraumaService` owns trauma/counseling, `ICharacterNarrativeCommand` owns ambition progress, and body/population-health commands own health/disease effects. The event result must carry participant IDs and contextual faction ID so those handlers do not infer targets from strings.
- Content resolution now prepares a detached campaign candidate, validates typed requirements and exact physical stack reservations, applies typed character/health/relationship/faction effects, commits non-failing item consumption, and publishes the campaign candidate last. A late material/effect failure no longer resolves the event or partially changes live Aggregate state.
- `GuestSupplies` was confirmed to be a fabricated-consumer table. Its builder now removes those links instead of creating them, and all 30 unrelated guest-request item requirements were removed while the 14 authored request requirements remain.
- Reproduction now has persisted planned/start transitions and a real command path. Cross-lineage and golem processes require the exact operational facility and physical inputs, validate a detached candidate, consume atomically, and publish last; Allowed policy evaluates proposals only every ten days.
- All five age treatments now enter through the existing surgery Aggregate. Their authored procedures require exact 8868-8872 facilities, clinician/patient work, physical materials, surgery environment, typed effects, and the existing surgery save section rather than direct age mutation.
- The 101 research-reward facilities no longer all advertise generic Research/Logistics roles. Builder profiles now assign administration, production, living, medical, industrial, rune-biomedical, greenhouse, or observation roles; age-treatment facilities expose the typed surgery capability.
- The V21 equipment assets existed outside the root catalog. Eighteen role-equipment definitions, five age procedures, and facilities 8897-8901 were added to the root catalog explicitly; the repeating-crossbow scenario's prior unknown-definition failure identified this real registration gap.
- Loaded ammunition now persists both ammunition definition ID and remaining count. A nonempty magazine accepts only the same ammunition type; changing type requires an empty magazine, and firing the last round clears the loaded type.
- Crop ecology previously consumed only Yield and SeedYield. `CropGenomePhenotype` now maps all six loci: cold/heat tolerance alter the live temperature band, growth speed alters Tick progress, disease resistance alters daily disease probability/progression, and yield/seed yield remain in harvest output. The phenotype is derived from the saved cultivar genome, so restore preserves every effect.
- Eleven authored choices/events used `WorkDelayDays`, but the atomic resolver explicitly rejected that effect. Society save V3 now owns scoped delay end-days; flood affects agriculture/haul work, road/whiteout affect expedition logistics, and unscoped service/life-event delays affect global work. `CharacterStatsProjectionService` consumes the persisted query, so the effect survives restore and changes completed work rather than only a debug snapshot.
- General-trait behavior preferences were validated but unread. Runtime profiles now compile them into capped Utility AI multipliers, while trait event weights and active-ambition related-event weights affect deterministic society-event and participant selection. Participant assignment is no longer biased toward the first stable CharacterId.
- Expressed hereditary definitions now have one runtime query authority backed by the narrative Aggregate. Slow aging modifies the daily biological-age increment, broad/toxin resistance modifies infection susceptibility, and success-rate/gestation/offspring stability modify conception and miscarriage calculations. Latent traits remain non-expressed and therefore do not contribute.
- Faction chapter `consume=true` requirements were previously checked but not converted into commit effects, so a chapter could advance without spending its promised items. `TryResolveChapter` now returns the exact consumed requirements, allowing `IContentResolutionService` to reserve and atomically consume them before publishing the staged chapter/faction state.
- The former 36-way faction duplication is removed in current assets and the rebuild path: 72 consumable support/bargain requirements, 72 operational facility requirements, 36 refusal pressures, six counterpart-faction mutations, and 36 unique mechanical choice signatures are present.
- Culture environment and etiquette fields were previously presentation-only free text. Ten typed room profiles now feed real facility scoring, while forbidden-food, etiquette, and inter-culture attitude data feed the society incident selector. The descriptive `environmentalPreferences` strings remain presentation text and no runtime rule parses them.
- Cultural-practice success was connected, but authored `neglectedEffects` had no command path and the saved participation record did not distinguish observance from neglect. The alert dispatcher now exposes a stable neglect action, applies only typed neglect effects, advances no assimilation, persists the outcome/cooldown, and round-trips it in `characters.narrative`.
- Inter-culture attitude weights previously influenced incident selection only. A newcomer now creates one bidirectional direct relationship memory against already initialized residents during its one-time background initialization, using each culture's independently authored attitude.
- `supply:greenhouse-nutrient` and `supply:inoculated-log` were still output-only goods after fake guest sinks were removed. The greenhouse and fungal shelf now own real `BuildingCropPlotAbility` cycles that request, haul, and consume those supplies together with the seed lot before growth starts; crop-plot persistence owns the resulting phase and genome state.
- The seven hereditary costs described in prose were absent from the consequence assets. They now use appended, serialization-safe consequence kinds and feed survival need growth, active-reproduction hunger, movement, mana-disease exposure, and the existing biological-aging projection.
- The nine legacy traits had placeholder `legacy-trait:*` behavior tags and no mood/event data. They now share the V20 three-part contract, and a typed reaction runtime translates concrete meal, research, invasion, festival, room-environment, and checkout-wait events into mood reactions instead of parsing display text.
- `tool:reinforced-restraint` and `tool:prisoner-work-kit` were still stackable output-only goods while captivity hardcoded and consumed `captivity:restraints`. They now have unique persistent item IDs and durability state. Captivity owns them only while actually equipped; otherwise the same physical instance remains in carry/world inventory. Other audited V21 output-only tools and records remain open and must not be counted as connected.
- Fertility treatment had no reproduction reference even though conception and miscarriage calculations were already centralized. It is now a saved optional process choice, paid at `TryStart`, and modifies those existing calculation inputs instead of creating a second fertility authority.
- The first fertility implementation exposed a request flag but the generated approval alert still had only one generic start action. This would have left treatment unreachable in normal play. Biological reproduction alerts now offer both paths; golem assembly continues to show only ordinary assembly.
- Latent hereditary traits were always exposed on the general snapshot while `medical:trait-analysis-kit` had no consumer. The narrative Aggregate now owns an analyzed flag and a separate visible-latent projection. Internal genetics still reads the latent authority, so discovery state cannot alter inheritance.
- Facility `8879` still carried the generic industrial-lab role after gaining a medical analysis command. Its authored profile and current asset now advertise Medical/Research and Treat/Research/Operate, allowing capability evaluation and the generated analysis alert to use the real facility role.
- The complete 8801-8901 facility set divides cleanly into 63 exact workstation-recipe executors and 38 typed command executors. The earlier no-recipe set shrank after correcting treated lumber to the 8816 workstation; zero facilities now rely on a generic role tag alone.
- Facility 8882 had a circular BOM: it produced the room-partition kit while also requiring that kit for its own construction. The kit belongs to 8883 family partitions and the authored BOM is corrected accordingly.
- Semantic tags were insufficient for the facility audit because unrelated buildings could satisfy them. Mentorship, pathogen diagnosis, weapon-pattern access, resonance tuning, secure trade, remote defense, crop/husbandry support, flow metering, and expedition planning now query exact typed facility commands.

## 2026-08-08 Phase 128 final integration findings

- The initial isolated full-world PlayMode gate found a real scene-composition defect before save capture: moving the shared world-map source file had left the authored scene GUID attached to `OffenseWorldMapPanel.cs`, so Unity could no longer materialize `OffenseWorldMapRuntime`. The runtime and panel now live in filename-matching source files, and the original GUID remains with `OffenseWorldMapRuntime.cs`.
- Enemy background faction reactions previously stopped at narrative metadata. Canonical faction-ID mapping now modifies persisted enemy loyalty, and captivity derives compliance and escape risk from that same narrative state. The focused enemy continuity scenario ratchets the deserter/legion mapping.
- Landmark visibility and placement previously trusted ordinary research/unlock state. Both construction UI and placement validation now query the milestone authority; all nine landmarks are locked before their matching milestone and become constructible after completion.
- The isolated five-route functional alert PlayMode facade passed reproduction, festival scheduling/resolution, funeral, counseling, and age-treatment dispatch with five persisted/dismissed actions.
- The focused V21 vertical gate passes after the scene-GUID repair, including 68-section atomic staging scenarios, 10,000 general-trait selections, 10,000 hereditary combinations, 2,000-by-three-generation narrative compression, all six crop loci, ecology/disease vectors, combat, campaign, faction authority, and all 101 research facilities.
- The current-code full-world gate now passes `68/68/68`: all registered sections were captured, restored, and recaptured; the canonical baseline matched; the live baseline was restored; and the integrated Console result was Error 0 / Warning 0.
- The first successful capture exposed that bulk `StockCategory.General` spawning selected a max-stack-one workwear definition by lexical ID and created 40 unauthoritative item instances. Bulk stock spawning now selects stackable definitions only; unique equipment remains owned by the equipment runtime.
- Active invasion enemies intentionally live in the invasion section rather than the resident character-world section. Cross-aggregate preflight now indexes their canonical CharacterIds so their saved life and combat-loadout state can reference the same persistent individual without spawning a duplicate resident.
- VContainer 1.19's circular-dependency scan revisited the same shared registration DAG from every root and made the production scope appear hung. The package is now embedded in the project with a memoized, cycle-safe traversal; a clean isolated run resolved the embedded package path and passed the full-world gate.
# Phase 129 research-node catalog findings

- The design authority already summarizes the 180-node/138,824-work V21 research contract, pacing, major branches, and unlock principles, but it does not yet contain an exhaustive node-by-node dictionary comparable to the facility, event, and trait appendices.
- The new catalog must follow current authored research assets and the reverse reward authority; presentation-only unlock bundles must not be mistaken for a second gameplay lock authority.
- `ResearchProjectAssetBuilder.CreateSpecs()` is the canonical authored source for stable ID, numeric ID, Korean name, description, field, work, and direct prerequisites. Rebuilt `ResearchProjectSO` assets persist the resolved references and causal prerequisite links.
- The builder captures merged unlocks, appends production/service/overhaul unlocks, rewrites absorbed research requirements, and then builds presentation bundles. Therefore documentation needs a reverse-indexed reward pass in addition to the project spec list.
- Current generated authority is exactly 180 `ResearchProjectSO` assets and 180 matching unlock-bundle assets.
- Content-owned reward declarations currently appear on 275 resource items, 265 production recipes, 47 surgical procedures, 12 craft materials, eight crops, four environmental workwear definitions, and 55 combat equipment definitions (28 weapons, 19 armors, eight shields). Generic item mirrors are not a separate research reward kind and must be deduplicated against their equipment/resource authority.
- Common YAML keys are stable enough for a read-only extractor: `projectId`, `displayName`, `description`, `field`, `requiredWork`, `prerequisiteId`, the polymorphic `BlueprintBuildingUnlock`/`BlueprintRecipeUnlock`, and content-specific identity plus `requiredResearchId`.
- The comprehensive document currently contains no stable `research:*` IDs, confirming the exhaustive node appendix is wholly missing rather than partially duplicated.
- Canonical `BuildingSO` reward names serialize as `objectName` and numeric `id`; production recipes use `recipeId`/`displayName`. This is sufficient to resolve project-owned building and recipe unlock entries without relying on filenames.
- The reproducible extractor passes the authority baseline: 180 projects, 138,824 total work, zero duplicate IDs, zero unresolved direct prerequisites, zero rewardless projects, and 919 deduplicated direct reward entries.
- Field distribution is 9/8/13/12/4/11/10/6/6/7/8/7/10/6/27/31/5 across the 17 `ResearchField` values; the largest appendices will be industry/automation (31) and surgery/transplant (27).
- The Markdown formatter now emits readable prerequisite names together with exact stable IDs and keeps recipes/results as distinct rewards even when their display names match.
- A review of the initial four fields caught blank duplicate rewards from unrecognized mirror assets and raw labels for project-owned recipes. These are extractor-only defects, not content defects; recognized `ResearchRewardCatalog` families and a global recipe identity map are now enforced.
- Removing unrecognized mirrors reduces the truthful reverse reward set from the prototype's 919 entries to 899 while retaining zero rewardless projects. Eleven rows in the first four field tables require regeneration.
- Three raw recipe IDs are authored `FacilitySynthesisRecipeSO` upgrades rather than production recipes: 잠금진열장 개조, 전투깃발 제작, 의식초점석 조율. They remain direct project unlocks and now receive their authored display names through the global recipe map.
- Fields 0-8 now render 79 unique node rows with nine field headings. The reviewed section contains no blank reward labels, raw `recipe_*` labels, or leaked PowerShell interpolation syntax.
- Fields 9-13 add 38 nodes for husbandry, metallurgy, textiles, cuisine, and pharmacology. The running document total is 117 nodes across 14 fields with zero formatter artifacts.
- All 17 fields are now present. Exact block comparison against regenerated Markdown passes, with 180 distinct stable IDs, 180 distinct numeric IDs, work sum 138,824, zero missing/extra nodes, zero malformed six-column rows, and zero formatter artifacts.
- The design document remains ignored by the repository-wide `docs/` rule, while the new verifier lives in the existing `Tools` tree. A small `.cmd` entry point handles Windows PowerShell 5's UTF-8-without-BOM and execution-policy behavior before invoking the source-derived `.ps1` implementation.
- Final validation passes after the navigation/tooling updates: 180 rows, 180 unique stable IDs, 180 unique numeric IDs, 138,824 documented work, 17 field headings, exact regenerated table match, zero malformed rows, zero formatter artifacts, zero replacement characters, balanced code fences, and zero trailing whitespace.

# Phase 130 V22 apparel/textile implementation findings

- The approved scope is a new runtime vertical, not a documentation-only expansion. Completion requires authored content, executable work orders, physical inventory effects, aggregate persistence, functional UI, and focused/full validation.
- Existing environmental workwear owns a species-oriented equipped map and loose-item fallback; V22 must move mutable slot authority to `CharacterApparelAggregate` and leave that runtime as a compatibility adapter.
- Existing physical stack signatures preserve exact state payloads, so V22 fiber components must expose a canonical signature containing only material item, four-tier quality, and three-band condition. Production day, exact quality, pathogen detail, and lot identity cannot participate.
- Existing item reservations have no time-bounded lease. Apparel workflows therefore need a scoped lease layer with invalidation and retry semantics rather than changing unrelated reservation behavior implicitly.
- The existing crop genome already has the required six loci and its positive bounds match V22's +16% growth/+10% yield normalization. Fiber quality should consume those loci through the approved penalties rather than adding a seventh locus.
- Facility IDs 9301-9314 and the six referenced research IDs are available. The reverse reward count must rise from 101 to 115 facilities without adding research nodes or work.
- Current content exposes exactly four environmental workwear identities, which will be reused as four entries of the 56-definition apparel catalog instead of duplicated.
- The V22 authored slice now resolves to 56 apparel definitions, 12 material definitions (10 woven plus leather/rune-leather), 4 crops, 12 six-locus genomes, 3 husbandry fiber outputs, 14 facilities, and 89 recipes. The focused Unity gate passes all counts and the 81-point yield/growth tradeoff grid.
- An invalid apparel lease originally retried only the same saved stack IDs, which could permanently strand a craft or medium repair after fire, contamination, compaction, or quantity loss. Revalidation now preserves valid delivered stacks first, but after invalidation performs a bounded policy-aware rebuild for substitutable inputs without ever replacing a persistent target garment with a different item.
- Focused asset-count checks were insufficient to catch zero-valued inherited `DataScriptableObject.id` fields. The production container's exact-type compatibility index exposed the collision; V22 focused validation now explicitly checks positive unique numeric IDs for apparel and textile materials.
- Expanding authored crops/genomes did not automatically update `CropEcologyRuntime` or `TryClaimInitialSeedGrant`; both retained exact V20 count assertions. The V22 authority is now consistently 12 crops, 32 genomes, and 12 base seed lots across assets, construction, and bootstrap.
- MaxStack-one does not imply combat equipment. Apparel is a persistent unique physical stack whose item-instance ID and `ItemInstanceComponentIds.Apparel` state live inline on the stack, while combat equipment and modules continue to require their dedicated authoritative unique-item registry entry.
- Existing cloth and dreamweave physical items had stack caps below the V22 fabric contract. The V22 builder now preserves their features and identity while normalizing every woven material to MaxStack 100; raw fiber and yarn remain MaxStack 200.
- Short wardrobe alteration previously overwrote the authored cut-opening mask, making “close/reopen” semantically incorrect. Short operations now mutate only `closedOpenings`; full tailoring remains the only path that changes size or cuts a new opening.
- The open Unity production report still carries 24 pre-existing V21 fake-consumer failures outside the V22 apparel slice. V22’s own focused gate is PASS, so the old failures must not be reported as V22 regressions or silently described as globally clean.
# V23 implementation findings - 2026-08-08

- The current worktree already contains broad V21/V22 user changes and generated assets; V23 changes must be additive and must not clean or reset unrelated files.
- `WorkOrderSaveData` currently persists one `reservedWorkerPersistentId`; there is no reusable rule-based worker policy or contribution ledger.
- Work eligibility already passes through `IWorkPolicyRegistry` and typed `IWorkStatPolicy` implementations, providing the correct integration boundary for V23 eligibility without bypassing safety/career rules.
- Existing V22 apparel runtime still uses `TextileQualityTier`, minimum material quality filters, quality-bearing stack codecs, and quality projection; these are direct grade-free migration targets.
- Production bills already support repeated/target-stock execution, but do not own craftsmanship-target pipelines or common worker-selection policies.
- V22 textile quality is embedded in the shared apparel definition file (`TextileQualityTier`, quality projection, provenance, and instance state), the apparel work-order runtime, item codecs, availability index, and crop/certified-seed paths; removal must be coordinated rather than deleting one enum in isolation.
- Relevant definition paths differ from their logical namespaces: buildings live under `Services/Buildings/SO`, production recipes under `Models/Economy/Content`, and combat save/runtime contracts under `Models/Combat/Core` plus `Services/Combat`.
- Building construction work is owned by `BuildingWorkAbility.constructionWorkRequired`; construction material requirements are exposed through the building ability accessors rather than fields directly on `BuildingSO`.
- Combat equipment craft orders already capture required work and material ID but lack worker policy, contributor ledger, fixed quality roll, and quality-target repetition state.
- Production recipe definitions expose explicit process kind, direct/preparation/finishing work, inputs, and outputs; they are suitable inputs to one central V23 calculator while preserving authored overrides.
- `WorkOrderRuntime.ApplyWork` is the atomic construction contribution boundary: it currently accepts any caller, writes a transient reserved worker ID, and deletes the order immediately before `ConstructionSite.CompleteConstruction`; V23 eligibility/contribution/quality completion must be inserted before that publication sequence.
- Work-order capture deliberately clears the reserved worker and restores in-progress orders as ready. This matches the V23 rule that leases are not saved, while policy, contribution, and fixed roll must be durable.
- `BuildingSO.Abilities` exposes the authored ability count, so the construction capability factor can be computed without reflection or string-tag guessing.
- VContainer registration for work orders and apparel is centralized in `DungeonWorldSimulationRegistration`; combat is registered separately in `DungeonCombatRegistration`.
- `CombatEquipmentDefinitionSO` provides kind, weight, primary material amount, components, era, and tier, enough to derive non-placeholder equipment work without changing the 61 definitions' public identity.
- `BuildingWorkAmountAbility` is the authoritative authored BOM/work component and already rejects empty, abstract, duplicate, or missing construction material definitions.
- Production input/output primitives expose canonical item IDs, integer amounts, and output probabilities; V23 recipe work can account for expected output quantity without reaching into serialized fields.
- Combat equipment form classification can use concrete weapon/armor/shield types, weapon gunpowder/range data, armor layer, occupied hands, tier, and era; it does not need display-name inference.
- Every `BuildableObject.Initialization` rebuilds its state-module set, so a craftsmanship module registered there will participate automatically in the existing modular-facility save section without adding a 69th section.
- `WorkOrderRuntime` already receives `IObjectResolver`; optional V23 services can be resolved through its `TryResolve` boundary without breaking the many direct editor-test constructors.
- The authoritative project currently contains 368 player-building assets, not the approximate 369 in the proposal. All 368 serialize a construction BOM; runtime representation archetypes remain excluded.
- The 24 production items previously labelled as fake-consumer failures already have real typed command consumers. The production validator omitted those domains, so V23 adds an explicit item-to-command-owner catalog instead of inventing recipes or sinks.
- Automatic rejected-output dismantling must consume the rejected unique item before publishing recovery. Persisting a `rejectedOutputConsumed` recovery obligation closes the output-full/save-restore duplication window for both apparel and combat equipment.
- Character stats are authored on a 0..10 scale while craftsmanship skill is 0..100. Equipment quality therefore projects the mean Dexterity/Research scale by multiplying their sum by five; passing the raw values made almost every result poor.
- Facility quality now affects completed-facility work throughput through the shared 0.70..1.40 projection multiplier, so construction craftsmanship is mechanically observable rather than display-only.
- Apparel and equipment now mirror construction's pre-consumption quality reachability rule. No eligible worker releases reservations into `WaitingForEligibleWorker`; an impossible target releases them into `TargetCurrentlyUnreachable` and reuses the saved attempt roll after conditions improve.
- Construction completion delegates return only success, not the created building. The completed facility can instead be resolved from the authoritative grid at the saved order position after publication.
## 2026-08-08 - Phase 132 gameplay UI/debug separation discovery

- User settings already persist `DungeonUserSettingsData.developerMode`; it defaults to `false`, is cloned with the rest of the settings object, and does not consume a world-save section.
- `DungeonUserSettingsService` already exposes `Changed` and applies presentation preferences, so Phase 132 should reuse this authority and present it to players as `Debug Mode` instead of adding a second setting.
- The settings runtime UI already has a fourth development page and a `developerModeToggle`, but its Korean literals are visibly mojibake in source/output and need player-facing copy cleanup.
- `GameplayScene.unity` has an active top-level `__Debug` root with two children. It must be classified before gating; an unrelated empty child named `Debug` also exists under another transform and should not be hidden by name alone.
- Debug-mode changes must remain presentation-only: overlays, raw identifiers, diagnostic counters, validation launchers, and direct debug controls may be hidden/revealed, while actionable failure reasons stay visible.

## 2026-08-08 - Phase 132 implementation and verification findings

- Runtime surfaces were classified as player-facing (construction, production, equipment, apparel, character, medical, research, faction, expedition, event/notice), advanced player policy (worker/material/quality/repeat settings), or debug-only (palette, raw IDs/enums, AI utility diagnostics, overlays and mutation commands).
- `DungeonUserSettingsData.developerMode` remains the single persisted authority. No world-save section or duplicate flag was added.
- `DungeonDebugSceneVisibilityController` only gates the active scene's top-level `__Debug` root and `__Runtime/Debug`; unrelated transforms named `Debug` are not affected.
- The character AI tab contains BT branches, utility candidates, timing, path budgets and raw memory diagnostics, so the tab is now hidden unless Debug Mode is enabled. Disabling Debug Mode while it is open returns the panel to the normal status tab.
- Player surfaces no longer expose the surgery order ID, doctor persistent ID, combat equipment definition ID, husbandry status enum, husbandry failure enum or V23 craftsmanship enum in the audited paths.
- Construction, production and apparel use player-facing stage/failure copy; worker/material/quality/repeat choices are placed behind progressive disclosure. Technical order/definition/state text is appended only while Debug Mode is enabled.
- Settings and building detail panels use short unscaled-time fade/scale choreography and respect Reduced Motion; gameplay authority and command timing are unchanged.
- First fresh PlayMode attempt revealed that the debug verifier called the full-world save without a prepared owner. The verifier now validates its owned `DungeonDebugRunSaveData` directly; the separate 68-section integration gate remains authoritative for full saves.
- Final MCP verification passed every pointer, visibility, command, targeting, overlay, metadata and reset assertion with Console Error 0 / Warning 0.
- Visual review: the 1600×900 palette is centered and legible; the 900×1600 sheet remains fully on-screen with scrollable command rows and reachable close/action controls.
## 2026-08-08 - V24 static structured narrative kickoff

- Current `LocalLlmRequestQueue` uses Ollama's OpenAI-compatible `/v1/chat/completions` endpoint with prompt-only JSON instructions and `response_format:{type:"json_object"}`; it has no per-profile schema authority.
- Local Ollama reports version 0.32.5 with `llama3.1:latest`; a read-only `/api/chat` probe accepted a JSON Schema in `format` and returned schema-shaped content.
- The repository already exposes nine request profiles and domain DTO validators. Character narrative state includes background, culture, ambition, expressed traits, revealed latent traits, recent events, origin faction/archetype, skills, age/life-stage records, and career/progression data.
- The worktree contains extensive user-owned V21-V23/UI changes. V24 edits must remain scoped to LLM/narrative files and preserve those changes.
- All nine profiles already route through one queue, but prompt ownership is distributed across character skills, AI director goals/impulses, persona, social rumor, character log, dialogue, facility evolution, and equipment/facility evolution history. The implementation needs a queue-level static schema authority plus shared context/quality utilities that prompt builders can adopt incrementally.
- Existing DTOs are split across `LlmJsonResponseParser`, `CharacterRecordJsonDto`, `CharacterSkillGenerationService`, `FacilityEvolutionLlmProposalProvider`, and `EvolutionHistoryNarrativeRuntime`; schema definitions must match these concrete wire shapes rather than introducing an unrelated envelope.
- The shared queue can switch transport without changing `ILocalLlmRuntime` or the many fake runtimes. It currently owns request construction and HTTP response extraction, making it the narrow integration point for static schema selection, native Ollama request/response wire shapes, capability status, schema diagnostics, and pre-callback reference validation.
- Existing request-bound identifiers (skill combinations, facility proposals, evolution evidence) are already validated in their domain handlers. V24 schemas should keep those fields as ordinary strings/arrays and leave request-local membership checks in C#, consistent with the static-schema rule.
# 2026-08-08 — V24 컨텍스트 연결 점검

- `NarrativeRequestContext.cs`의 최초 작성본은 터미널/패치 경로에서 한국어가 mojibake로 변형되어 따옴표까지 손상됐다. 프롬프트용 한국어는 Unicode escape로, 코드 식별용 문구는 ASCII로 유지해야 한다.
- 현재 중앙 큐의 기본 컨텍스트만으로는 `Persona`, `CharacterSkill`, `CharacterRecord`가 실제 인물 사실을 의무적으로 참조하지 않는다. 각 프롬프트 생성 지점에서 actor/progression 기반 컨텍스트를 명시적으로 붙여야 한다.
- 나이·배경·문화·야망은 `ICharacterLifeQuery` 및 `ICharacterNarrativeQuery`에 이미 권위가 있으므로, 공개 사실 투영기를 통해 LLM 컨텍스트에 넣는 것이 기존 Aggregate 권위를 보존하는 경로다.
- 9개 프로필은 요청 데이터와 무관한 고정 JSON Schema 문자열·UTF-8 바이트·SHA-256 해시를 소유한다. 요청별 Fxx/Mxx는 일반 문자열 배열이며 membership과 공개 범위는 C# 품질 게이트가 검증한다.
- `NarrativeRequestContextBuilder`는 표현 특성, 출신, 문화, 야망, 실제·생물학적 나이, 생애 단계, 경력, 부상, 최근 사건과 공개된 잠재 유전 특성을 최대 24개 사실로 결정론적으로 투영한다. 미공개 잠재 형질은 컨텍스트·응답 trace에 들어가지 않는다.
- 영속 프로필은 Hard Reject에서만 한 차례 교정 요청하며, 유효 참조 하나를 사용하고 금지 위반이 없는 투박한 문구는 Soft Pass로 채택된다. 문체 약함만으로 재요청하지 않는다.
- V24 집중 시나리오는 6/6 통과했다. 10,000개 서로 다른 참조 컨텍스트에서도 프로필별 schema hash/reference가 변하지 않았고, 잘못된 F99/M99는 Hard Reject, 유효 F01/M01의 투박한 응답은 Soft Pass, 충분히 근거 있는 응답은 Strong Pass가 됐다.
- 첫 실제 모델 smoke는 정적 검사가 놓친 CharacterSkill JSON 닫힘 오류와 모델 입력의 stable-id 노출을 발견했다. 둘을 고친 두 번째 smoke에서 9개 schema 모두 Ollama structured generation을 통과했고 8/9가 즉시 품질 게이트를 통과했다. 남은 BubbleLine의 F/M 배열 혼입은 정적 `^Fdd$`/`^Mdd$` 패턴으로 문법 단계에서 차단한다.
- 20/profile 1차 실측은 파싱 실패 0이었으나 167/180이었다. `BubbleLine` 12건이 선택 사항인 reference 배열을 과도하게 채워 실패했으므로, 말풍선의 정적 schema는 `line`만 허용하도록 좁혔다. 이는 사실 grounding이 선택 사항이라는 계약을 따르며, 허구 F 참조를 Soft Pass로 완화하지 않는다.
- 진화 계보의 참여자 CharacterId는 내부 fact stable ID로만 유지하고 모델이 보는 label에서는 제거했다. 모델은 “기록된 소유자·제작자·사용자의 기여”만 보며 영속 ID를 이름처럼 문구에 복사할 수 없다.
- 20/profile 2차 실측은 179/180 accepted, fallback 1/180이었으나 SocialRumor 한 건이 256-token 상한에서 닫는 괄호 전에 잘렸다. 이 프로필만 384-token 상한으로 높이고, 스킬·계보는 강한 판타지/무협, 인물 기록은 중간 강도, 말풍선은 자연 구어체라는 중앙 문체 지침을 최종 model prompt에 넣는다.
- 3차 실측에서 SocialRumor와 BubbleLine은 각각 20/20으로 수정이 확인됐다. 남은 한 건은 FacilityEvolution의 복합 배열이 기본 256-token 상한에 걸린 미완성 JSON이므로, 복잡도에 맞춰 768로 조정한다. 이는 생성 길이를 강제하는 값이 아니라 schema 완료 전 최대 허용량이다.
- 최종 20/profile 실측은 179/180 accepted(99.4%), parse failure 0, fallback 1/180(0.6%)로 PASS다. 유일 폴백은 SocialRumor가 F01을 targetCharacterId로 사용한 응답이며 C# quality gate가 `Unknown inline reference`로 정확히 Hard Reject했다. CharacterSkill, Persona, FacilityEvolution, EvolutionHistory, CharacterRecord와 BubbleLine은 모두 20/20이다.
- 최종 TTFT 중앙값은 profile별 697.1~700.1 ms, p95는 698.8~897.1 ms였다. 스키마 구조가 큰 영속 프로필도 정적 schema 재사용 상태에서 1초 미만 p95를 유지했다.
- Final clean Unity validation passed after clearing the Console: V24 focused scenarios 6/6, Error 0 / Warning 0.

## 2026-08-09 - Phase 135 official source boundaries

- The Korea Heritage Service intangible-heritage overview classifies transmitted culture into traditional craft, oral expression, ritual, lifestyle, play/festivals, and martial arts. V25 uses these categories only as scenario and motif taxonomies; it does not copy catalogue prose.
- The official archery heritage record and Heritage Channel material on `Muyedobotongji` support treating martial practice as trained technique, ritual, record, and community memory rather than as generic combat vocabulary. Only that high-level framing is encoded in generation rules.
- Our Korean Dictionary and the Encyclopedia of Korean Culture are reference boundaries for terminology and folklore categories. Their example sentences, entry prose, and modern creative text are not copied into the dataset.
- The generated corpus uses 12 backgrounds, 18 ambitions, 32 life events, 20 practices, 56 general traits, 24 heritable traits, 61 equipment definitions, and 368 building definitions read from authoritative Unity YAML assets.
- Pair-aware filtering produces exactly 40,000 records from 50,000 raw scenarios. The 38,000 SFT candidate set includes the 6,000 preference-review rows; 2,000 evaluation rows are isolated by whole scenario family with zero family leakage.
- Player-facing prose audit covers 78,998 fields. Korean coverage is 100%, selected generic fallback phrases are 0, vocabulary entropy is 9.045316 bits, and exact duplicates among fields at least 40 characters long are 505/50,835 (0.9934%). Short names and fixed labels are reported separately rather than disguised as unique prose.
- Same-seed regeneration produced 19/19 byte-identical files with zero missing paths and zero SHA-256 mismatches. Gzip headers use a fixed timestamp so compressed artifacts are reproducible.

## 2026-08-09 - Phase 136 local reviewer findings

- The merge contract accepts one combined CSV through `--review-csv`, so the safest UI boundary is immutable eight-chunk input, separate atomic JSON autosave, and explicit combined CSV export.
- Exact automatic review warnings can cover malformed JSON, unknown F/M references, generic cliches, repeated-particle patterns, duplicate candidates, and A/B mechanical-field divergence. Semantic voice quality remains a human decision.
- The original rejected generator used one fixed sentence (`전설의 운명이 깨어나 모든 것을 바꾸었다`) across most review pairs. It was absent from SFT chosen completions but made preference review trivial and would teach DPO to avoid one phrase rather than improve narrative quality.
- The rebuilt review package contains zero copies of that fixed fallback and zero manufactured cliche warnings. It contains 2,347 records with an invalid fact/motif reference in one blinded fact-distortion candidate; these remain useful hard-reject tests, not automatic verdicts.
- Hard negatives now follow three deterministic contrast classes: generic-but-safe prose, invented-fact contradiction, and awkward motif listing. All retain the chosen payload's fixed mechanical fields.
- Official TRL documentation confirms that conversational prompt-completion datasets compute loss on completions only; the SFT projection therefore trains on the grounded assistant completion and excludes rejected candidates. Official bitsandbytes documentation supports NF4 on Windows/NVIDIA for this GPU class.
- A deterministic 16-bit text SimHash grouped all 8,000 records into 256 coarse similarity buckets. The UI exposes these as navigation/batch scopes while keeping the current visible-page bulk limit at 20 records.
- Browser runtime discovery returned no available browser bindings, so the local UI cannot receive pointer/screenshot evidence in this session. The implementation must retain HTTP/API, static DOM, CSP, keyboard-contract and responsive-CSS tests as nonvisual evidence and report the visual limitation honestly.
- Reviewer state lives under `Artifacts/Review/V25`, outside the generated `Artifacts/Training/V25` tree. Regenerating the deterministic corpus therefore cannot erase human review progress.
- The correct training boundary is now explicit: grounded chosen completions train the initial SFT adapter first; the 6,000 preference rows are human-reviewed afterward and only those explicit A/B/rewrite/drop decisions may feed DPO. Synthetic `systemPreferred` values remain navigation metadata, not human labels.
- The repeated fixed rejection phrase was a dataset-construction shortcut, not a model-generated alternative or an SFT target. It has been removed from all rebuilt review artifacts and retained only as a fail-closed leakage assertion in build/train code.
- Sustained local QLoRA is not currently safe on this configuration. The strongest causal evidence is the NVIDIA kernel event sequence (`UVMLiteProcess` error, then recovery state `Node Reboot Required`) during full CUDA load; the final `0x1E` dump still requires elevated WinDbg access for exact stack attribution. The project and training output also sit on external USB Disk 2, which logged an I/O retry and left the step-20 optimizer checkpoint corrupt.
# Phase 139 Git publish findings - 2026-08-09

- Local `main` is clean and one commit ahead of `origin/main`; commit `a525783` was not accepted remotely.
- VS Code's Git log records `GH001` after a 354-second push. Two `adapter_model.safetensors` files are 133.05 MiB each and were stored as ordinary Git blobs because `.safetensors` has no LFS attribute.
- Two optimizer checkpoints are 67.98 MiB and 62.16 MiB; GitHub warned about them even though they are below the 100 MiB hard blob limit.
- The mounted `DungeonStory-Qwen3-1.7B-Q4_K_M.gguf` is correctly tracked by Git LFS and must remain a release artifact.
- Two broken loose refs under `refs/codex/turn-diffs/checkpoints` independently cause `git pull` and automatic `git gc`/repack to fail. They are ephemeral Codex checkpoint refs rather than gameplay branches.
- The training-model directory contains 41 generated files totaling 574.78 MiB, all added only by the unpushed commit. Nine Python bytecode-cache files were also accidentally committed.
- The only non-LFS blobs at or above 50 MiB are the two 133.05 MiB adapters and the 67.98/62.16 MiB optimizer checkpoints, all within the generated training-model directory.
- The two broken refs contain valid object IDs, but their full paths are 270 characters. Git for Windows fails to stat those paths and reports a synthetic zero invalid pointer. Moving them out of the active `refs` namespace fixes enumeration without affecting `main` or any gameplay branch.
