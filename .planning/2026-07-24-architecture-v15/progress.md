# Progress

## 2026-07-24

- Created the V15 architecture migration workspace.
- Confirmed a clean Unity Console baseline before edits.
- Selected a compile-green, foundation-first migration order.
- Added the `DungeonStory.Foundation` assembly with scoped clock, random stream,
  event bus, and runtime registry abstractions.
- Replaced the static path broker with a scene-scoped `IGridPathSearchBroker`
  whose budget is owned by the scheduler.
- Replaced the static AI world registry with an injected
  `ICharacterAiWorldRegistry`; characters, buildings, wildlife, warehouses,
  and the grid now register explicitly.
- Migrated runtime path/world lookup call sites and editor fixtures to explicit
  dependencies.
- Verified a clean Unity compile and Console `Error 0 / Warning 0` after the
  foundation and registry checkpoint.
- Removed every production call to `WorldItemStackRuntime.Active` and routed
  physical item dependencies through actors, buildings, and runtime services.
- Added stable `WorkTypeId` values plus registered execution, candidate, and
  urgency provider contracts.
- Moved repair, research, crafting, butchering, survival work, and cleaning out
  of `WorkTaskExecutor` into VContainer-registered handlers.
- Replaced the matching open-ended work availability and repair urgency
  branches in `WorkTargetSelector` with the same policy registry.
- Removed runtime work-order and game-time global access from
  `WorkTaskExecutor`.
- Verified compilation and gameplay-scene startup with Unity Console
  `Error 0 / Warning 0` after both work-registry checkpoints.
- Reduced `WorkExecutionRegistry.cs` from 696 lines to a 271-line contract and
  duplicate-validating registry, with concrete handlers owned by Combat,
  Research, Buildings, Wildlife, and Survival folders.
- Replaced the open-ended work-stat `if` chain with registered
  `IWorkStatPolicy` implementations and an injected `IWorkAmountCalculator`.
- Split work stat policy indexing from execution handler indexing to avoid a
  dependency cycle; verified the VContainer graph by starting the gameplay
  scene with Console `Error 0 / Warning 0`.
- Added a registered building ability runtime dispatcher and moved water,
  cooking, medical, fuel, and butchering completion logic out of serialized
  ability configuration classes.
- Moved survival and butchering ability definitions and runtime handlers into
  their owning domain folders without changing their serialized type identity.
- Re-ran `ModularFacilityDebugScenarios.RunAll()` after the split with no
  Console errors or warnings.
- Moved equipment crafting configuration and execution into Combat-owned
  ability files and removed crafting-time access to combat runtime globals.
- Replaced the item-to-expedition inventory callback with an
  `EquipmentStoredEvent`, avoiding a `WorldItemStackRuntime` /
  `ExpeditionEquipmentRuntime` constructor cycle.
- Re-ran physical item and haul/construction safety contracts with no Console
  errors or warnings.
- Removed scoped path, world registry, combat equipment, body health, and
  deprivation fallbacks from wildlife and hunting runtime paths.
- Fixed the wildlife natural-motion fixture to construct the scoped path
  broker explicitly; the complete wildlife contract suite now passes.
- Reworked rescue and medical execution to use injected medical services,
  game time, world registries, survival stock, and defense interfaces.
- Added body-health downed/recovered and infection-burden events to break
  Combat/Survival/Exterior constructor cycles.
- Removed the `CharacterMedicalRuntime.Active`,
  `CharacterBodyHealthRuntime.Active`, and
  `CharacterPhysicalCapacityQuery.Active` facades.
- Routed character move/work capacity and the character combat UI through
  injected interfaces.
- Re-ran combat contracts and clean-start gameplay for 15 seconds with Unity
  Console `Error 0 / Warning 0`.
- Removed `CharacterDeprivationRuntime.Active` and runtime-created breakdown
  ScriptableObjects; deprivation actions now use registered runtime routines.
- Updated the shared Behavior Designer graph and its characterization contract
  for the `Deprivation Breakdown` branch, including a non-overlapping layout.
- Removed `DefenseEngagementRuntime.Active` from production and editor code.
  Intruders receive the scene-scoped defense service from their factory, while
  movement, commands, work, and character UI depend on
  `IDefenseEngagementRuntime`.
- Re-ran intruder, defense engagement, combat, and character AI contracts; all
  passed. A clean gameplay-scene run for 15 seconds reported Unity Console
  `Error 0 / Warning 0`.
- Removed `WorkOrderRuntime.Active` from production and verification code.
  Building placement now receives the scene-scoped work runtime, construction
  sites retain that explicit dependency, and UI/summary queries use the same
  service.
- Replaced `BuildingSummaryFormatter`'s filth global access with the scoped
  `IWorldFilthQuery`.
- Re-ran the work amount contracts and a clean 15-second gameplay-scene start;
  both passed with Unity Console `Error 0 / Warning 0`.
- Removed `SurvivalFoodRuntime.Active` and moved food/water shortage pressure
  into the cached `CharacterAiWorldSignalSnapshot`, keeping isolated AI tests
  neutral when no survival service is present.
- Removed `WorldFilthRuntime.Active`; room evaluation receives
  `IWorldFilthQuery`, while spawned filth work targets retain a narrow creator
  contract for cleaning and lifecycle callbacks.
- Split wildlife UI dependencies into `IWildlifeQuery` and
  `IWildlifeHuntCommandService`, then removed both `WildlifeRuntime.Active` and
  `WildlifeEcosystemRuntime.Active`.
- Re-ran AI naturalness, room environment, wildlife contracts, the wildlife
  PlayMode snapshot, and a clean 15-second gameplay run. All passed with Unity
  Console `Error 0 / Warning 0`.
- Removed the final `Active` facades from world items, expedition equipment,
  world water, and debug mode. Product code now has zero static `Active`
  runtime accessors; the remaining debug-rule bridge is tracked separately as
  mutable static debt.
- Added `ItemMarkerPresenter` and moved item marker creation, refresh, removal,
  camera lookup, and font resolution behind `IItemMarkerPresenter`.
  `WorldItemStackRuntime` now only exposes pile data through
  `IWorldItemMarkerDataSource` and can run headless with a null presenter.
- Re-ran physical item, multi-haul/construction safety, and the full item-pile
  pointer PlayMode verifier. List/detail/back, character priority, Alt item
  selection, captures, and Console checks all passed with zero warnings/errors.
- Added the V15 save-section envelope, dependency validation, restore phases,
  and a topologically sorted section registry.
- Moved physical items, work orders, wildlife population, survival resources,
  and deprivation state behind domain-owned `IDungeonSaveSection`
  implementations. `DungeonGameSaveService` no longer directly depends on
  those five runtime services.
- Raised new saves to V15 and verified a live gameplay capture containing the
  five registered section IDs with Unity Console `Error 0 / Warning 0`.
- Split carcass freshness, rot conversion, butchery, taboo consequences, and
  butcher work queries out of `WildlifeRuntime` into
  `IWildlifeCarcassService`.
- Narrowed butcher work and building-ability handlers to the carcass service,
  fixed the PlayMode hunt fixture to place an unarmed hunter in a legal
  adjacent cell, and passed the hunt -> carcass -> butcher loop.
- Moved the 1,295-line `SurvivalFoodRuntime` class from
  `WildlifeRuntime.cs` into the Survival domain without behavioral changes.
  Survival and wildlife contracts pass, and the latest Unity reload has zero
  compiler errors.
- Converted the save root to strict V15 metadata plus
  `DungeonSaveSectionEnvelope` entries. `DungeonGameSaveService` now depends
  only on `IDungeonSaveSectionRegistry`; world, characters, items, work,
  survival, wildlife, combat, exterior, offense, invasion, operation, run,
  debug, research, shop, recruitment, staff, codex, and meta state own their
  save sections.
- Migrated editor/debug save contracts away from removed V14 root fields and
  verified product, Editor, and architecture-test assemblies with Unity's
  Roslyn compiler.
- Added NUnit architecture ratchets for static `Active`, global access counts,
  God-object size, registered work dispatch, envelope-only save roots,
  composition-root delegation, and defense combat execution boundaries.
- Added `IDefenseCombatExecutor` and moved attack resolution, body damage,
  ammunition consumption, recoverable weapon drops, armor durability, and
  combat presentation out of `DefenseEngagementRuntime`. The coordinator fell
  from 2,634 to 2,313 lines.
- Replaced every direct `Time` access in `DefenseEngagementRuntime` with the
  injected scene-scoped `IGameClock`.
- Split VContainer registration into Foundation, Work, Combat/Invasion,
  World Simulation, and Save modules. `DungeonRuntimeLifetimeScope` fell from
  696 lines / 303 direct registrations to 511 lines / 217 registrations.
- Moved the AI naturalness QA observer into the Editor assembly and replaced
  start-party, character-summary, and recruitment lifetime-scope searches with
  injected scene queries/services. Product scene searches fell from 16 to 6.
- Current static baseline: `Active=0`, scene searches `6`, direct `Time=259`,
  direct `Random=50`, mutable statics `45`; all current ratchets compile.
- Unity MCP remained unavailable on the final live verification attempt and
  timed out after 300 seconds. Roslyn product/Editor/test compilation is green;
  live VContainer resolution, Test Runner execution, Console, and captures
  remain pending until the bridge responds.
- Extracted the remaining core, facility, character, AI/room,
  progression/offense, and presentation registrations from
  `DungeonRuntimeLifetimeScope`. The composition root is now about 100 lines
  and its direct-registration ratchet is fixed at 13.
- Added a real shop vertical slice with `IShopFeatureQueryService`,
  `IShopFeatureCommandService`, and `ShopFeatureSurfacePresenter`.
  Purchase idempotency and retail shop/product selection no longer live in the
  monolithic feature panel.
- Removed the shop presenter legacy downcast, `BuildFacilityShop`,
  `BuildShopOperationsDetail`, and the panel's daily-shop dependency.
- Recompiled product, Editor, and architecture-test assemblies successfully
  after the registration and shop checkpoints.
- Extracted multi-stack route selection, opportunistic pickup grouping,
  warehouse choice, detour limits, and partial reservation into
  `WorldItemHaulPlanningService`; the item runtime now delegates haul policy.
- Added the real research feature vertical slice with domain-owned query,
  command, and presenter services. The feature panel no longer knows the
  research runtime, blueprint catalog, queue mutations, or reward formatting.
- Fixed the research editor fixture to use the registered candidate policy.
  `BlueprintResearchDebugScenarios` now passes again.
- Added `DefenseEngagementStore` for active engagements, collision-safe ID
  allocation, and retreat history. Restored IDs advance the sequence before
  new engagements are created.
- Re-ran defense engagement and V14 combat regression scenarios; both passed
  with Unity Console `Error 0 / Warning 0`.
- Expanded the architecture NUnit suite to 15 tests. The synchronous Unity Test
  Runner result is `15 passed / 0 failed / 0 skipped`.
- Fixed gameplay composition startup by registering `RandomStreamProvider`
  through an explicit seeded factory instead of allowing VContainer to select
  its primitive constructor.
- Made the P1/P2 pointer verifier reopen `GameplayScene` in single-scene mode,
  fixed TMP outline setup before font material assignment, and updated the
  offense interaction to the current attack-confirmation flow.
- The full P1/P2 pointer verifier now passes `18/18` with captured
  `Error 0 / Warning 0`; its report and screenshot are under `Temp/`.
- Added `IItemTransferService` and moved reserved pickup, warehouse deposit,
  facility-buffer deposit/consumption, and equipment world-state transfer out
  of `WorldItemStackRuntime`. The compatibility runtime fell to about 1,530
  lines.
- Physical item and haul/construction safety contracts pass after the transfer
  split with Unity Console `Error 0 / Warning 0`.
- Expanded the official architecture NUnit suite to `19 passed / 0 failed /
  0 skipped`, including the item transfer and seeded-random composition
  ratchets.
- Added read-only scoped world queries for characters, wildlife, buildings,
  warehouses, and retail facilities. AI director context, facility lookup,
  workforce summaries, building management summaries, and invasion threat
  sampling no longer scan the scene hierarchy.
- Extended the same character registry query to AI scheduler startup,
  workforce replanning, combat loadout preparation, automatic skill triggers,
  and offense member selection.
- Fixed the Editor repair-candidate fixture uncovered by the registry
  migration. Work-priority and invasion-threat regressions both pass with
  Unity Console `Error 0 / Warning 0`.
- Product, Editor, and architecture-test assemblies compile with the V15
  Roslyn response files, and `git diff --check` reports no whitespace errors.
- A final synchronous architecture Test Runner invocation after the latest
  hot-path registry extension stalled inside Unity/MCP and did not produce a
  result. The latest source compiles, but that final Unity NUnit rerun and a
  post-extension gameplay boot remain pending until the editor command
  finishes or the bridge is restarted.
- Unity MCP recovered without restarting the editor. Added a real building
  feature slice with `IBuildingFeatureQueryService`,
  `IBuildingFeatureCommandService`, and `BuildingFeatureSurfacePresenter`;
  the registered building presenter no longer downcasts to the legacy panel.
- Removed scene-scan fallbacks from `WorldItemStackRuntime`,
  `ItemTransferService`, and `WorldItemHaulPlanningService`. Product and editor
  fixtures now register warehouses explicitly, matching runtime composition.
- Replaced the last defense character lookup scan with
  `ICharacterWorldQuery`.
- Migrated shopping choice randomness to a character-scoped
  `IRandomStreamProvider` stream and mood/condition timing in
  `CharacterStats` to `IGameClock`.
- Focused Unity regressions pass for physical items, multi-haul,
  construction safety, work priority, invasion, V15 save sections, and tab
  architecture. Architecture ratchets pass `20/20` when executed in the Unity
  domain.
- Booted the actual dirty `GameplayScene` twice for 8 seconds after the latest
  constructor and registry changes. Both runs ended with Console
  `Error 0 / Warning 0`; the scene was not saved or otherwise rewritten.
- Replaced all eight forwarding feature presenters with domain-owned
  query/command/presenter slices: buildings, shop, warehouse, operations,
  defense, expedition, research, and codex.
- Reduced `P0FeatureSurfacePanel` to a layout/view shell with only the Korean
  font service and presenter registry injected. Deleted the obsolete
  `P1P2FeatureSurfacePanel` partial and 665 lines of disabled legacy builders;
  the shell is now 553 lines.
- Migrated warehouse, shop, research, and codex feature queries from scene
  hierarchy searches to scoped world registries or cached runtime providers.
- Architecture ratchets now pass `24/24` in the Unity domain. Actual pointer
  events opened all eight feature tabs, followed by a five-second gameplay
  stability window with Console `Error 0 / Warning 0`.
- Added the first real product assembly boundary,
  `DungeonStory.World.asmdef`, and moved shared grid/area/terrain/movement
  primitives out of the legacy Grid and Survival files. Architecture
  ratchets pass `25/25`.
- Reduced `DungeonGameSaveService.cs` from 647 to 385 lines by moving
  character, research, shop, run, meta, recruitment, discontent, and codex
  DTOs into their owning domains. V15 live capture and round-trip preserved
  all 28 section IDs, physical item payloads, and item sequence values.
- Migrated codex, research, offense, invasion summaries and offense reward
  context creation from direct scene queries to cached providers and scoped
  building/warehouse registries. The offense reward regression suite passes.
- Replaced scene hierarchy queries in the item, wildlife, and room world-view
  toggles with `IDungeonUiCanvasProvider`; room hover throttling now uses the
  scoped `IUiClock`.
- Product scene-query tokens fell from 62 to 56 and direct `Time` accesses
  fell from 118 to 116. The architecture ratchet now locks both changes.
- A PowerShell metrics command timed out after recursively enumerating all of
  `Assets`; the retry used `rg` over product sources. A follow-up constructor
  search initially exceeded the Windows command-line length and was rerun
  against the directory directly.
- Isolated compile 34 caught that `IUiClock` exposes `Time`, not `Now`, in the
  migrated room inspection runtime. The two call sites were corrected to the
  actual scoped clock contract before rerunning verification.
- Isolated compile 35 passed and the complete EditMode suite passed
  `59/59`. The world-view toggle checkpoint is compile-green and protected by
  architecture tests.
- Added `IPlayerCombatCommandSource` as the narrow query/command port between
  character input and combat presentation. The command bar and tactical
  overlay no longer search the scene or depend directly on
  `OwnerCommandController`.
- Combat command UI refresh throttling now uses `IUiClock`; product
  scene-query tokens fell from 56 to 52 and direct `Time` accesses from 116
  to 114.
- A combined combat structure/label patch was rejected because terminal
  mojibake did not match the UTF-8 source. The structural change was reapplied
  using ASCII contexts; an explicit UTF-8 read confirmed the Korean labels
  were already correct. A metrics command returned exit code 1 only because
  its final no-match `rg` assertion found no forbidden tokens.
- Isolated compile 36 found a C# null-coalescing type inference mismatch
  between `OwnerCommandController` and the unavailable command-source object.
  The captured scene component is now explicitly viewed through
  `IPlayerCombatCommandSource` at the composition boundary.
- Isolated compile 37 passed and the complete EditMode suite passed `60/60`.
  Combat presentation is now protected against scene-query and direct-clock
  regressions.
- Added `IRunResultPanelRegistry`. Scene-authored run-result UI is registered
  once at composition time and factory-created panels register themselves,
  removing the result service's hierarchy lookup.
- `RunResultPanel` now pauses and restores gameplay through
  `IGameTimeScaleController`. Product scene-query tokens fell from 52 to 50
  and direct `Time` accesses from 114 to 111.
- The post-change search command returned exit code 1 because no explicit
  constructor call sites existed; the reported architecture metrics were
  still valid.
- Isolated compile 38 passed and the complete EditMode suite passed `61/61`.
  The run-result panel registry and time-scale boundary are compile-green.
- Character floating-icon feedback now consumes the composition-captured
  `GameManager` instead of keeping its own hierarchy search and cache.
- Settings UI now receives the captured `GameManager` and
  `IGameTimeScaleController`; opening and closing options no longer searches
  the hierarchy or writes `Time.timeScale` directly.
- The floating-feedback and settings checkpoints reduced product scene-query
  tokens from 50 to 46 and direct `Time` accesses from 111 to 108. The direct
  time ratchet was lowered accordingly.
- An exploratory no-match search for explicit settings-controller
  construction returned exit code 1; VContainer remains its only creation
  path.
- Isolated compile 39 passed and the complete EditMode suite passed `63/63`.
  Floating feedback and settings UI remain behaviorally covered.
- Added a distinct `ICharacterLifetimeQuery` alongside the active character
  registry. Character bridges register for their full scene lifetime and only
  leave that registry on destruction, while AI membership still follows
  enable/disable.
- `CharacterWorldSaveService` now uses the lifetime view for IDs and inactive
  restore data, and the active view for interruption/reuse. It no longer
  searches the scene hierarchy.
- A registry inspection command ended with a missing optional file path after
  printing the requested sources; the correct Foundation registry path was
  found and inspected in the follow-up.
- Isolated compile 40 passed and the complete EditMode suite passed `64/64`.
  Active and lifetime character registry behavior is compile-green and the
  save-service architecture contract is locked.
- Migrated invasion entry/owner lookup, facility-evolution warehouse
  inventory lookup, recruitment actor lookup, and SampleScene ration
  processing to scoped providers/registries. Sample rations now use
  `IGameClock` rather than `Time.time`.
- Repaired editor fixtures that initialized `CharacterActor` before injecting
  its new clock dependency. Invasion, recruitment, facility evolution,
  SampleScene ration, and offense reward regressions now pass.
- Architecture ratchets pass `27/27`. Gameplay pointer events still open all
  eight feature tabs with no product exceptions.
- Added the second product assembly boundary,
  `DungeonStory.Characters.asmdef`, and moved the shared character role,
  stat, condition, facing, decision, and lifecycle primitives into it while
  preserving the original script GUID.
- Replaced direct scene traversal in staff-discontent processing with
  `ICharacterWorldQuery`; daily evaluation and automatic rebellion response
  now consume the scene-scoped character registry.
- Injected `IGameClock` into meta run progress and result construction, so
  elapsed time and save restoration no longer read `Time.time` directly.
- Removed `SocialReputationRuntime.Current`. Social reputation now consumes
  character/building registries, `IGameClock`, and a deterministic
  `IRandomStream`; character save capture/restore reaches it through
  `ISocialReputationRuntimeProvider`.
- Because the live Unity MCP relay stopped returning responses, copied the
  project source/settings to an isolated temporary verification project.
  Unity 6000.3.8f1 compiled all product and editor assemblies there and the
  architecture suite passed `30/30`, including the newest character assembly,
  meta clock, and social reputation dependency ratchets.
- Added `DungeonStory.Work.asmdef` and moved stable `WorkTypeId`/
  `BuiltInWorkTypeIds` contracts out of the execution registry. The isolated
  Unity architecture suite passed `31/31` with the new assembly boundary.
- Removed every direct product `Object.Find*` call and lowered the architecture
  scene-search allowance to zero. Removed unused mutable static skill/start
  preparation diagnostics and lowered that ratchet to the measured 40.
- Injected `IGameClock` into invasion gathering, advance, facility-damage
  cooldown, and persistence timing. The isolated Unity suite passed `32/32`.
- Migrated wildlife ecosystem decisions to a seeded `wildlife-ecosystem`
  random stream and moved habitat marker discovery into a one-time
  Infrastructure registry. Split the wildlife HUD toggle into its own UI
  source file so the ecosystem domain no longer knows scene/UI services.
- Migrated invasion candidate timing to `IGameClock` plus an
  `invasion-threat` random stream. Static `InvasionThreatSettings` now
  computes ranges from a caller-provided stream instead of executing global
  randomness.
- Removed runtime random rolls from `CharacterSO`. Shopping and character
  spawning now supply scoped random streams for visit count, carrying money,
  and respawn cadence. Architecture limits are now direct `Time <= 176` and
  direct `Random <= 15`.
- Added V15 random-stream state capture/restore plus real Foundation NUnit
  coverage. The isolated Unity EditMode run passed `39/39`, including
  deterministic sequence, next-draw restore, and duplicate stream rejection.
- Started the run-seed integration: `RunVariableRuntime` now consumes the
  scoped `run-variables` stream, existing issued stream references are reseeded
  in place, and the random-stream save section restores after run variables.
- Migrated combat, invasion paths, shops, exterior incidents, character
  deprivation, grid idle movement, and character AI micro-actions to
  scene-scoped deterministic random streams. The product architecture ratchet
  now allows zero direct `UnityEngine.Random` access.
- The fifteenth isolated Unity EditMode run passed `44/44` and exited cleanly.
  Its NUnit XML and log were written inside the temporary verification project
  rather than the repository root.
- Confirmed there are no direct product `UnityEngine.Random` calls left and
  launched isolated Unity EditMode run 16 with the AI, Grid movement, and
  deprivation random-stream migrations included.
- Isolated run 16 imported and compiled all nine changed scripts without
  compiler errors, then Unity exited cleanly before the test runner produced
  XML. This is treated as the mirror's compile/import warm-up; a second run on
  the now-current Library is required rather than repeating the initial-copy
  diagnostic.
- Launched isolated run 17 without an explicit `-quit` flag so the Unity Test
  Runner owns process termination after the import warm-up.
- Replaced direct game-time access in combat commands, the AI director, and
  work-duty decisions with the scoped `IGameClock`.
- Source inspection confirms those three paths now contain no direct
  `UnityEngine.Time` access, and the work-duty clock guard sits at the helper
  boundary rather than falling back to global time.
- Isolated run 17 compiled and executed `47` tests: `46 passed / 1 failed`.
  The sole failure is the new deprivation clock ratchet finding one remaining
  direct `Time.*` token; the AI random-stream ratchet passed.
- Replaced the final deprivation `Time.time` use and lowered the product
  direct-time ratchet from `169` to the newly measured `149`.
- Isolated compile warm-up 18 completed with zero C# compilation errors after
  the combat, AI director, work-duty, and deprivation clock changes.
- Isolated Unity EditMode run 18 passed `47/47`. The checkpoint now enforces
  product direct `Random.* = 0` and direct `Time.* <= 149`.
- Moved V15 save envelopes, restore phases, section contracts, section
  registry, and restore report into the Foundation assembly. Infrastructure
  now records restore counts through explicit report methods while retaining
  root DTOs, orchestration, and disk I/O.
- Replaced `OperatingDaySettlementRuntime`'s generic scene query and duplicate
  constructors with one canonical constructor over scoped character/building
  queries. Its editor fixture now supplies explicit actor/building lists.
- Isolated compile 19 passed the Foundation save-contract relocation with zero
  C# errors. Compile warm-up 20 is running for the settlement query change.
- Compile 20 found two stale editor construction sites for
  `OperatingDaySettlementRuntime`: the shared AI fixture omitted
  `IGameDataProvider`, and the shop scenario still used the removed generic
  scene-query signature. No product compilation error was reported.
- Updated both stale editor construction sites to the canonical settlement
  signature using explicit world queries and game-data providers.
- Migrated `CharacterAiScheduler` timing and frame budgets to the scoped
  `IGameClock`; the scheduler no longer reads `Time.time` or
  `Time.frameCount` directly.
- Isolated compile 21 passed the settlement constructor and fixture migration.
  Product direct `Time.*` is now `143`, and generic scene-query tokens fell to
  `101`; the time ratchet was lowered accordingly.
- Isolated compile 22 passed the scheduler clock migration with zero C#
  errors.
- Migrated `LocalLlmRequestQueue` queue age, request timeout, and diagnostics
  to the scoped `IUiClock`, preserving timeout progress while gameplay is
  paused. Dynamic QA queue creation now injects `UnityUiClock` explicitly.
- Lowered the product direct-time ratchet from `143` to `136` and added an
  architecture contract preventing the LLM queue from returning to direct
  Unity time access.
- Isolated compile 23 passed the scoped LLM UI-clock migration with zero C#
  errors.
- Isolated EditMode run 23 executed `51` tests: `50 passed / 1 failed`.
  The failure was isolated to the new settlement architecture test using the
  product-only source locator for an Editor fixture; all product and LLM-clock
  tests passed.
- Removed Unity time access from `SocialRumor` and
  `SocialRumorSnapshot`. Expiry, capture, and restore now receive a sampled
  timestamp from `CharacterSocialMemory` or `SocialReputationRuntime`, both of
  which own a scoped `IGameClock`.
- Lowered the product direct-time ratchet from `136` to `131` and added an
  architecture contract for passive rumor models.
- Replaced lazy generic scene queries in the main-camera, grid-controller,
  grid-texture, game-data, and floating-number providers with one
  composition-time `DungeonSceneRuntimeReferences` snapshot. Runtime
  replacement now uses explicit registration methods instead of hierarchy
  rescans.
- Generic product scene-query tokens fell from `101` to `91`; the captured
  runtime provider boundary is protected by a new architecture test.
- Isolated compile 25 passed the captured scene-reference provider migration
  with zero C# errors.
- Migrated `BuildableObject` visit and worker reservation expiry to a scoped
  `IGameClock`. Dynamically generated filth work targets receive the same clock
  from `WorldFilthRuntime`, and the shared Editor fixture now mirrors that
  injection.
- Lowered the product direct-time ratchet from `131` to `125`.
- Isolated compile 26 passed the building reservation clock and dynamic filth
  target injection with zero C# errors.
- Added the Foundation `IGameTimeScaleController` port and migrated the save UI
  to `IUiClock`, the time-scale port, and captured `GameManager` references.
  The save UI no longer queries the scene or touches Unity time directly.
- Product direct-time access fell from `125` to `118`; generic scene-query
  tokens fell from `91` to `89`.
- Isolated compile 27 found one stale `sceneQuery` use in the save UI's
  post-load owner-selection refresh. Added the QA fallback panel to the
  composition-time scene references and removed that final save-UI query.
- Isolated compile 28 passed the corrected save UI and captured owner-selection
  reference with zero C# errors.
- Added the fifth product assembly boundary, `DungeonStory.Buildings`, owning
  stable building category, facility role, legacy work mask, and stock
  category primitives. `BuildingSO` and `SaleItem` now consume those contracts
  instead of declaring them.
- Isolated compile 29 completed the new Buildings assembly import with zero C#
  errors.
- Isolated EditMode run 29 passed `56/56`, covering all current Foundation,
  architecture, save-section, work, item, AI, and newly added clock/reference/
  Buildings-boundary regressions.
- Unity MCP is responsive again. The live editor reports Play Mode off,
  compilation idle, and Console `Error 0 / Warning 0` before the gameplay boot
  check.
- Entered the live `GameplayScene` through Unity MCP. Play Mode started without
  a product exception or compile error; the known five transient
  `The referenced script (Unknown) on this Behaviour is missing!` warnings
  recurred during play-entry deserialization.
- Exited Play Mode through Unity MCP and confirmed the editor is stopped,
  unpaused, and not compiling. The dirty gameplay scene was not saved.
- Recounted the remaining generic scene-query boundary after the live boot.
  Product source contains `89` `IDungeonSceneComponentQuery` /
  `CachedSceneRuntimeProvider` tokens, concentrated in Infrastructure runtime
  providers; direct product `Time.*` remains at the ratcheted `118`.
- Added domain-scoped composition-time reference groups for offense, invasion,
  and facility feature runtimes. Their providers no longer inherit the generic
  cached scene provider or query the hierarchy; dynamically generated offense
  panels explicitly register themselves with the offense reference group.
- Updated the gameplay composition root and domain registration methods to
  pass only their owning reference group, and added an NUnit architecture
  contract that prevents those providers from regressing to generic scene
  lookup.
- Isolated compile 30 passed the offense, invasion, and facility reference
  group migration with zero C# errors.

## Errors Encountered

- Parsing the run-15 XML and log together through PowerShell's XML DOM exceeded
  the 10-second command limit. Reading the small result file directly and
  extracting the root attributes completed immediately and confirmed the
  `44/44` pass.
- A PowerShell source-segment inspection failed because `$path:$start` was
  parsed as an invalid scoped variable. Formatting the label with `-f` avoided
  the ambiguous colon and completed on the next attempt.
- A compile-status query used `-and Test-Path` without wrapping the command
  call as an expression, causing a PowerShell parser error. Computing
  `$logExists` first made the condition unambiguous on the next attempt.
- `apply_patch` rejected a content-free move hunk for the save contract source.
  The `.cs` and `.meta` were then moved by exact, workspace-validated paths.
- The first combined save-boundary patch used an outdated invasion restore
  call shape and was rejected atomically. A follow-up progress patch also
  targeted text that had never landed with the rejected patch; the current
  file tail was inspected before recording both errors here.
- The first combined LLM clock patch used two update hunks against the same
  property block; the second hunk expected context created by the first, so
  `apply_patch` rejected the patch atomically. Consolidating the source changes
  into one hunk applied cleanly.
- The first settlement architecture NUnit used `SourceBySuffix`, which
  intentionally excludes Editor directories, to locate an Editor debug
  fixture. Added an explicit Editor-inclusive locator instead of weakening the
  product-source definition used by every ratchet.
- The first captured-runtime-reference patch also included a test insertion
  against a stale neighboring method context, so `apply_patch` rejected the
  combined patch atomically. Source changes and the test insertion were split
  into independent, current-context patches.
- The first building-clock patch matched the complete using block too
  strictly and was rejected without changing files. Replacing the individual
  import and reservation expressions in a narrower patch applied cleanly.
- Compile 27 failed on the save UI's single stale `sceneQuery` identifier after
  the constructor dependency was removed. The owner-selection fallback is now
  captured explicitly with the other scene UI references.
- The first building-primitives patch included a test insertion against a
  guessed neighboring method name and was rejected atomically. The assembly
  source move and architecture test were reapplied as separate patches using
  the current file context.
- A parallel source inspection included guessed
  `DungeonOffenseRegistration.cs` and `DungeonInvasionRegistration.cs` paths
  that do not exist, so the composed read aborted without returning the other
  files. Registration filenames are now discovered with `rg --files` before
  the next targeted inspection.
- A parallel constructor search treated `rg`'s normal no-match exit code as a
  composed tool failure and returned no grouped results. The follow-up uses
  one PowerShell `Select-String` pass, which can report a partially populated
  constructor set without failing on absent patterns.
- The first EditMode run after compile 30 was rejected because the preceding
  batch compile process still held the isolated project's Unity lock after its
  launcher returned. The retry waits for the isolated batch process to exit
  before starting the test runner.
- The explicit `Wait-Process` check raced with the isolated compiler's normal
  shutdown and reported that its PID no longer existed. No process was
  terminated; the project lock was gone, so validation continued normally.
- A fixture source read contained an accidental terminal escape sequence in
  the command's working directory, so PowerShell rejected the path before
  reading any file. The command is retried with the canonical workspace path.
- A constructor search passed two paths as positional `Get-ChildItem`
  arguments, which PowerShell rejected. The corrected search uses the explicit
  `-Path` array form before changing the captured-reference constructor.
- EditMode run 30 executed `57` tests with `56 passed / 1 failed`. The sole
  failure was the existing composition-root text contract still expecting
  parameterless domain registration methods; it is updated to assert the new
  explicit offense, invasion, and facility reference arguments.
- Isolated EditMode run 31 passed `57/57`, including the new domain runtime
  reference contract and the updated composition-root assertions.
- Removed the generic `CachedSceneRuntimeProvider` abstraction after migrating
  its final users. Character, social, LLM, staff, progression, research, shop,
  meta, settlement, alert, and run-variable providers now read explicit
  composition-time references; the dynamically created regular-customer
  runtime is injected as a VContainer component.
- Captured `GridSystemManager`, `CharacterSpawner`, `CharacterAiScheduler`,
  and `OwnerRunManager` once at composition and migrated their providers away
  from hierarchy lookup. Product generic scene-query tokens fell from `93` at
  the measured pre-slice state to `62`; direct product time remains `118`.
- Isolated compile 32 passed the removal of `CachedSceneRuntimeProvider`, all
  new character/progression reference groups, and the updated VContainer
  registration graph with zero C# errors.
- The combined post-test validation was marked failed because repository-wide
  `git diff --check` found extensive pre-existing trailing whitespace in the
  dirty `GameplayScene.unity`. The scene is user-owned and remains untouched;
  test XML and changed-source whitespace are checked separately.
- An assembly inspection guessed that the Work asmdef lived under
  `Character/Work`; the file has already been moved to a different contract
  folder, so that single read failed while the other assembly/item dependency
  reports completed. Subsequent assembly paths are discovered before reading.
- The first canvas-provider patch also tried to replace a mojibake button label
  using the terminal-rendered text, which did not match the file's underlying
  bytes, so the whole patch was rejected atomically. Structural dependency
  edits are reapplied separately; label cleanup is deferred to an exact
  source-aware pass.
- Isolated EditMode run 32 passed `57/57`. Scoped `git diff --check` found no
  whitespace errors in the touched source/test scope; only the repository's
  existing LF-to-CRLF notices were emitted.
- Added the sixth product assembly boundary, `DungeonStory.Items`, containing
  item-stack state, haul-state enums, physical-item/carry save DTOs, hauling
  settings snapshot, and the stored-equipment event. All moved serializable
  types carry `MovedFrom` metadata for their former `Assembly-CSharp`
  identity; runtime behavior remains in the existing Items implementation
  assembly until its Character/Building/UI edges are further separated.
- Isolated compile 33 and EditMode run 33 passed the Items assembly migration;
  `58/58` tests are green and the touched source scope has no whitespace
  errors.
- Split start-party preparation from gameplay commit/application. The
  preparation service now only owns candidate generation, rerolls, readiness,
  and snapshot creation; `PreparedStartPartyCommitService` is the sole bridge
  into the gameplay applier.
- Removed the preparation scene's gameplay-only owner, spawner, grid,
  run-variable, and spawn-factory provider registrations. Its LLM queue now
  uses a preparation-scene provider and an explicit UI clock.
- Added an architecture contract preventing start-party preparation from
  reacquiring gameplay runtime dependencies or commit responsibility.
- The first combined start-party scope and architecture-test patch used a
  stale test method context and was rejected atomically. The source and test
  changes were reapplied separately against the inspected current context.
- Isolated compile 41 found that the preparation scope's new UI clock
  registration omitted the `DungeonStory.Foundation` namespace import. Added
  the missing import before rerunning the checkpoint.
- The compile-43 launcher was terminated before Unity created its log because
  the shell startup timeout was too short. This produced no compiler evidence;
  the retry uses a longer launcher window and a fresh log number.
- Compile 44 exposed six hidden `sceneQuery` uses in the staff-management
  partial after its shared field was removed, plus two incorrect time-scale
  property names. Replaced the hidden lookups with explicit staff command,
  character/building registry, discontent, and AI mood-diagnostic ports, and
  corrected the time-scale port to its `Scale` property.
- Isolated compile 45 passed and EditMode run 45 passed `66/66`. Staff
  priority, staff management, character summary, and owner-selection UI now
  use scoped clocks, registries, command ports, and diagnostic queries.
- Replaced repeated user-settings scene traversal with composition-time camera
  and theme targets. Title and preparation canvases now receive an EventSystem
  reference holder, so neither dedicated scene exposes the generic scene query
  through VContainer.
- Product generic scene-query tokens fell from `40` before the start-party
  slice to `27`; direct product `Time.*` fell from `108` to `102`.
- Isolated compile 46 passed and EditMode run 46 passed `67/67`, including the
  dedicated-scene canvas and user-settings architecture contract.
- Added `WorldSimulationSceneReferences` and captured initial exterior zones
  and wildlife habitat markers once in the gameplay composition root.
  `ExteriorActivityRuntime` and `WildlifeHabitatMarkerRegistry` now seed from
  those references and retain their existing dynamic registration behavior.
- Migrated gameplay launch owner-panel refresh to
  `DungeonSceneRuntimeReferences`, and migrated character/defense debug
  commands to `ICharacterWorldQuery`.
- Added an architecture contract covering world-simulation references, scene
  navigation, and debug commands. Isolated compile 47 passed and EditMode run
  47 passed `68/68`.
- Added `SceneValidationReferences`; the scene leak validator now validates
  captured roots, buildables, and LLM queues rather than traversing through a
  generic service.
- Migrated the player automation bridge to the registered gameplay Canvas and
  captured camera settings target. Runtime-created controls remain visible to
  automation without a scene-wide component query.
- Removed `IDungeonSceneComponentQuery`, its VContainer registration, and stale
  Editor fixture implementations. `DungeonSceneComponentQuery` remains a
  concrete composition/QA utility only. Repository script references to the
  removed interface are `0`.
- Isolated compile 49 passed and EditMode run 49 passed `68/68`.
- Added the seventh product assembly boundary,
  `DungeonStory.Rooms`, owning the stable facility-role definition and
  catalog. The broader Rooms runtime remains in the gameplay assembly until
  its grid/building/presentation dependencies are inverted.
- Compile 50 reported duplicate `FacilityRoleCatalog` types because the
  isolated copy retained the moved source at its former path. The source
  worktree contained only the new file; removing the two exact stale mirror
  files resolved the verification artifact.
- Isolated compile 51 passed and EditMode run 51 passed `69/69`, including the
  new Rooms assembly ownership contract.
- Added the eighth product assembly boundary, `DungeonStory.Survival`, owning
  survival save DTOs, deprivation/filth/water primitives, overview snapshots,
  and the filth/water query contracts. All assembly-moved public types carry
  `MovedFrom` metadata.
- Removed survival model declarations from `WildlifeModels.cs` and
  `DarkSurvivalModels.cs`; wildlife now owns its hunt/ecosystem contracts while
  dark-survival runtime execution retains only actor-dependent behavior.
- Isolated compile 52 passed and EditMode run 52 passed `70/70`, including the
  new Survival assembly ownership contract.
- A post-checkpoint PowerShell inventory used the invalid regex `\Editor\`
  and emitted one parse error per file; the accompanying `rg` also used
  Windows-incompatible directory globs. No source was changed. The follow-up
  uses path-segment comparison and directory arguments.
- Added `DungeonStory.Combat` as a ninth product assembly. It owns the combat
  resolution model and polymorphic attack-verb/weapon-snapshot contracts;
  the SO definitions and Resources-backed catalog remain outside the kernel.
- Isolated compile 53 passed and EditMode run 53 passed `71/71`, including the
  new Combat assembly ownership contract.
- The first Invasion extraction patch was rejected atomically because its
  calculator-removal hunk matched terminal-rendered mojibake rather than the
  file's exact text. No source changed. The retry leaves the existing
  formatter/calculator in place and extracts only policy, threat, and save
  primitives using stable ASCII boundaries.
- Added `DungeonStory.Invasion` as the tenth product assembly, owning defense
  policy data, engagement save DTOs, owner-evacuation save state, and invasion
  threat tuning/snapshots. Active engagement execution remains in the
  gameplay assembly.
- EditMode run 54 executed `72` tests with `71 passed / 1 failed`. The existing
  threat-random architecture test still inspected the former settings source;
  it now targets `Invasion/Core/InvasionPrimitives.cs`.
- EditMode run 55 passed `72/72`; the Invasion assembly migration and updated
  random-stream ownership assertion are green.
- The first Offense extraction patch was rejected atomically because the
  top-range hunk included mojibake formation labels that did not match the
  file's exact byte-decoded text. No source changed. The retry leaves the
  display formatter in place and moves enums and model classes through
  smaller ASCII-only hunks.
- Added `DungeonStory.Offense` as the eleventh product assembly, owning route
  graphs, formation/supply IDs, supply loadouts, preparation snapshots,
  node-result models, and stable reward/strategy IDs.
- Isolated compile 56 passed and EditMode run 56 passed `73/73`, including the
  new Offense assembly ownership contract.
- Added `DungeonStory.AI` as the twelfth product assembly, owning stable BT
  branch, interrupt, macro-goal, mood-impulse, utility-factor, failure, and
  action-tag identifiers. Actor-dependent contexts, runners, and decision
  execution remain in the gameplay assembly.
- All assembly-moved serializable AI models carry `MovedFrom` metadata, and
  the original source files now consume the new leaf contracts without
  duplicate declarations.
- Isolated compile 57 passed and EditMode run 57 passed `74/74`, including the
  new AI assembly ownership contract.
- Added `DungeonStory.Presentation` as the thirteenth product assembly. It owns
  top-level tab identity/catalog data and the registered feature-surface
  presenter contracts/registry, while MonoBehaviour views and UI factories
  remain in the gameplay assembly.
- Replaced the mojibake top-level tab labels with canonical Korean labels in
  the single presentation catalog. Isolated compile 58 passed; after updating
  eight architecture tests from the deleted legacy presenter file to the new
  owner, EditMode run 59 passed `75/75`.
- Added `DungeonStory.Infrastructure` as the fourteenth product assembly,
  owning V15 save-root data, save service contracts, and immutable save-slot
  metadata. File-system, JsonUtility, and scene-aware implementations remain
  in the Infrastructure implementation folder.
- Isolated compile 60 passed. Two tests still inspected the former save-root
  location; after updating them, EditMode run 61 passed `76/76`.
- Added `DungeonStory.Wildlife` as the fifteenth product assembly, owning
  wildlife state/intent/habitat identifiers, save DTOs, butcher-yield data,
  and ecosystem overview values. Catalog loading and live ecosystem execution
  remain outside the leaf boundary.
- Isolated compile 62 passed and EditMode run 62 passed `77/77`.
- Added an assembly-graph architecture test covering the exact fifteen V15
  product assemblies, unique names, root namespaces, dependency-layer
  direction, and cycle detection. EditMode run 63 passed `78/78`.
- Marked the product-assembly-boundary checkpoint complete. Full namespace
  migration and removal of legacy facades remain in the final compatibility
  checkpoint.
- Re-ran all implemented debug suites after the assembly extraction and fixed
  stale Editor fixtures so they exercise the registered V15 paths. The
  isolated implemented-scenario run passed `30/30`; EditMode run 71 passed
  `78/78`.
- A Save UI PlayMode run exposed a real runtime-spawn lifecycle defect:
  `CharacterStats.Awake` calculated mood before VContainer could inject
  `IGameClock`. `Awake` now binds local components only, while the injected
  `ConstructCharacterStats` call performs the first time-dependent
  calculation. The graphics-enabled Save UI regression passed with real
  pointer input, save/load/delete, `Error 0 / Warning 0`.
- The unified UI PlayMode regression passed, including start-party rerolls,
  character status/growth/mood/records tabs, notices, and pointer routing.
  The older ProductShell verifier still contains title-flow assumptions that
  predate the dedicated preparation scene and is tracked as a stale QA
  contract rather than a product architecture failure.
- Removed `IBuildingWorkCompletedRuntimeAbility` and the direct execution
  fallback from serialized building abilities. Production, cleaning, security,
  reception, patrol, outdoor rest, and exterior maintenance now execute only
  through separately registered handlers. Work-completion abilities carry a
  data-only marker, and the dispatcher throws on a missing registration.
- Updated the extensibility fixture to register its own handler instead of
  putting execution code in its test ability. Added an architecture ratchet
  forbidding executable work methods in `BuildingAbility` data and requiring
  the seven core handler registrations.
- The post-cleanup implemented-scenario run passed `30/30`, EditMode run 84
  passed `79/79`, and the final Save UI PlayMode run passed with
  `Error 0 / Warning 0`.
- Replaced the complete `CharacterDeathEvent` static path with scoped
  `IGameEventBus` publication and lifecycle-owned subscriptions in character
  stats, medical care, deprivation, defense engagements, and owner-run
  management. Editor fixtures now share an explicit test bus. Isolated compile
  run 86 passed with exit code 0.
- Replaced `InvasionDungeonBreachedEvent` and `CodexUpdatedEvent` with scoped
  publication/subscriptions, and added an architecture ratchet capping product
  `EventObserver` references at 61. Implemented scenarios run 89 passed
  `30/30`.
- EditMode run 90 caught `DefenseEngagementRuntime` at 2,171 lines after adding
  event subscription fields, two lines above the existing 2,169-line
  God-object ceiling. The subscriptions were compacted without raising the
  ceiling; EditMode run 91 then passed `79/79`.
- Removed four unused global offense world-map notification types and replaced
  target selection/change notifications with events owned by the
  `OffenseWorldMapRuntime` instance. Replaced the global facility-synthesis
  selection notification with a lifecycle-bound event owned by
  `FacilitySynthesisRuntime`.
- Tightened the product `EventObserver` architecture ratchet from 61 to 56
  references. Isolated compile run 94 passed, implemented-scenario run 95
  passed `30/30`, and EditMode run 96 passed `79/79`.
- Replaced facility-shop refresh, regular-customer lifecycle, and invasion
  combat-feedback notifications with events owned by their runtime instances.
  Removed unused staff-discontent, research queued/progress, and dungeon
  economy global notifications instead of introducing replacement plumbing.
- Tightened the `EventObserver` ratchet from 56 to 44 references. Isolated
  compile runs 97 and 98 passed; implemented-scenario run 99 passed `30/30`
  and EditMode run 100 passed `79/79`.
- Migrated run-result/profile persistence, owner-run completion, offense truth
  reveal, and offense reward delivery to scoped `IGameEventBus` contracts.
  Removed unused offense departure/completion notifications. Each startable
  service and enabled MonoBehaviour now owns and disposes its subscription.
- Migrated facility-evolution completion to a dual boundary: the bound panel
  consumes an instance event, while cross-domain codex recording consumes the
  scoped bus. The first scenario run exposed a manually configured runtime
  without a bus; the fixture now supplies one explicitly. Compile run 110,
  implemented-scenario run 111 (`30/30`), and EditMode run 112 (`79/79`)
  passed. The `EventObserver` ratchet is now 36 references.
- Replaced room-experience publication with the direct
  `IRoomEnvironmentExperienceService` command boundary used by facilities,
  shops, and work execution. Compile run 122 passed, and the subsequent full
  scenario/EditMode checkpoints remained green.
- Migrated character growth-tab requests to `IGameEventBus`. The progression
  alert still opens the selected actor through the existing info-feed command,
  then publishes the tab-specific request; `CharacterSummeryInfo` owns and
  disposes the scoped subscription. Compile run 123, implemented-scenario run
  124 (`30/30`), and EditMode run 125 (`79/79`) passed.
- Migrated completed invasion combat reports to the scoped bus. The report
  runtime is now the publisher and Codex is a lifecycle-owned subscriber;
  editor fixtures use an explicit per-world bus. Compile run 126,
  implemented-scenario run 127 (`30/30`), and EditMode run 128 (`79/79`)
  passed.
- Migrated defense-facility activation from the static observer network to the
  scoped bus across `DefenseFacility`, combat reporting, Codex, facility
  evolution recording, and the natural-run verifier. Runtime-created editor
  facilities now receive the same explicit bus dependency as production
  facilities.
- Compile run 129 found one stale parameterless test listener and was fixed in
  run 130. Implemented-scenario run 131 then exposed a second fixture-only
  construction path that bypassed VContainer; after explicit injection,
  scenario run 132 passed `30/30` and EditMode run 133 passed `79/79`.
- Tightened the product `EventObserver` architecture ratchet to 24 references.
  The rejected broad patch that matched a BOM-prefixed first line changed no
  files; subsequent edits used stable class/method boundaries.
- A diagnostic PowerShell inventory attempted
  `[System.IO.Path]::GetRelativePath`, which is unavailable in the installed
  PowerShell/.NET host and emitted repeated non-mutating errors. Future
  inventories use resolved-path substring handling or `rg` paths directly.
- Migrated boss-invasion startup to the scoped event bus. The intruder system
  now publishes, while audio, run flow, and the natural-run verifier own
  lifecycle-bound subscriptions. Compile run 134, implemented-scenario run
  135 (`30/30`), and EditMode run 136 (`79/79`) passed.
- Migrated invasion-threat warnings to the scoped event bus. Threat generation
  now publishes through its injected world bus; combat loadout preparation and
  meta progression own disposable subscriptions, and the characterization
  fixture uses an explicit per-world bus.
- Tightened the product `EventObserver` ratchet from 23 to 22 references.
  Compile run 137, implemented-scenario run 138 (`30/30`), and EditMode run
  139 (`79/79`) passed.
- Migrated invasion-candidate and invasion-started notifications to the scoped
  bus across the threat runtime, director, run variables, meta progression,
  combat reporting, audio, and QA publishers. Compile runs 140 and 143 passed;
  implemented-scenario runs 141 and 144 passed `30/30`; EditMode runs 142 and
  145 passed `79/79`. The product `EventObserver` ratchet reached 20.
- Migrated `InvasionSpawnedEvent` to the scoped event bus across the invasion
  director, combat report, codex, owner evacuation, automatic skill triggers,
  and editor publishers. The event type is now a passive message contract with
  no static publisher.
- The isolated verification project had retained an intentionally reduced
  `Packages/manifest.json`; Unity therefore removed restored Test Framework,
  UGUI, TMP, and other package-cache contents on the next run. Mirroring the
  current source manifest, lock file, and same-hash package cache fixed the
  verifier without modifying the main project or its dirty gameplay scene.
- Compile run 153 passed, implemented-scenario run 154 passed `30/30`, and
  EditMode run 155 passed `79/79`. The product `EventObserver` ratchet is now
  verified at 19 references.
- Migrated facility-damage and final-combat-start notifications to the scoped
  bus across intrusion, codex, facility evolution, combat reporting, audio,
  and QA fixtures. Manually composed intrusion scenarios now inject and
  subscribe to an explicit per-world bus instead of relying on process-global
  listeners.
- Compile runs 156 and 159 passed; implemented-scenario runs 157 and 160
  passed `30/30`; EditMode runs 158 and 161 passed `79/79`. The verified
  product `EventObserver` ratchet is now 17 references.
- Completed the invasion event-family migration by moving
  `InvasionResolvedEvent` to the scoped bus. Threat, director, defense,
  combat reporting, loadout preparation, run variables, run flow, meta
  progression, and QA publishers now share the world-scoped contract.
- Compile run 162 passed, implemented-scenario run 163 passed `30/30`, and
  EditMode run 164 passed `79/79`. No invasion runtime now implements a
  process-global event listener.
- Migrated `OperatingDayReportEvent` from the global observer to the scoped
  bus. Settlement publishes the report; autosave, report alerts, and meta
  progression own lifecycle-bound subscriptions.
- Compile run 165 passed, implemented-scenario run 166 passed `30/30`, and
  EditMode run 167 passed `79/79`. The verified product `EventObserver`
  ratchet is now 15 references.
- The next bounded event slice is the facility activity family. Its consumers
  already have the scoped bus, but `BuildableObject` and `Shop` need an
  explicit bus port plus updates to manually composed editor worlds before
  removing the five static publication helpers.
- Migrated the complete facility activity family (`visit`, `revenue`, stock
  consumption, crime, and restock) to the scoped game-event bus.
  `BuildableObject` now owns an injected publication port; settlement,
  facility evolution, codex, regular customers, and meta progression own
  lifecycle-bound subscriptions. Product and PlayMode QA publishers use the
  active gameplay scope instead of static triggers.
- Scenario run 170 correctly exposed a manually composed `Shop` missing the
  new bus port. The facility and modular-facility fixture composition paths
  were updated, after which compile run 169, scenario run 171 (`30/30`), and
  EditMode run 172 (`79/79`) passed. The product `EventObserver` ratchet is
  now 10 references.
- Migrated the operating-day start/end lifecycle to `IGameEventBus`.
  `GameManager` and the force-settlement debug command publish the messages;
  skills, survival, staff discontent, facility evolution/shop, invasion
  threat, run flow/variables, meta progression, and settlement own scoped
  subscriptions. PlayMode probes publish through their gameplay scope.
- Compile run 173, implemented-scenario run 174 (`30/30`), and EditMode run
  175 (`79/79`) passed. No `OperatingDayStartedEvent` or
  `OperatingDayEndedEvent` static trigger/listener remains, and the product
  `EventObserver` ratchet is now 8 references.
- Migrated event alerts, info-panel requests, and notice-feed messages to the
  scoped `IGameEventBus`. Runtime listeners own disposable subscriptions and
  direct editor fixtures resolve or receive the same per-world bus.
- Removed the legacy `EventObserver`, `UtilEventListener`, and listener-wrapper
  implementation completely. The unrelated read-only collection and event
  payload snapshot helpers that had been hidden in the observer file now live
  in a dedicated Foundation collections file.
- Compile run 181 correctly exposed the hidden utility coupling and one static
  combat-command presenter using an instance bus. Compile run 182 then exposed
  a manually reconstructed grid controller missing its new bus dependency.
  After both fixture boundaries were corrected, compile run 183 passed with
  exit code 0. The product `EventObserver` ratchet is now 0.
- Post-removal verification passed: implemented-scenario run 184 completed
  `30/30`, and EditMode run 185 completed `79/79`.
- Migrated `GameManager`, `DungeonTitleUiController`, and LLM JSON runtime
  conversion helpers off direct Unity time. The title exit helper initially
  remained `static` while using the injected time-scale controller; isolated
  compile run 199 caught `CS0120`, and changing it to an instance method fixed
  the boundary.
- Locked the direct product `Time.*` architecture ratchet from `35` to `25`.
  Isolated compile run 200 passed, implemented-scenario run 201 passed
  `30/30`, and EditMode run 202 passed `79/79`.
- Migrated `DungeonUiThemeRuntime` refresh timing to `IUiClock`, threaded that
  clock through combat command, save, settings, title, start-preparation,
  run-result, and tab UI creation paths, and registered Foundation services in
  the title/preparation scene scopes. The direct `Time.*` ratchet is now `23`.
  Isolated compile run 203 passed, implemented-scenario run 204 passed
  `30/30`, and EditMode run 205 passed `79/79`.
- Migrated `DungeonAudioController` and scene transition fade/time-scale
  handling to `IUiClock` and `IGameTimeScaleController`. `DungeonSceneNavigator`
  keeps a parameterless editor-compatibility constructor that composes Unity
  clock adapters, while production scopes resolve the injected constructor.
  The direct `Time.*` ratchet is now `17`, with only Foundation clock adapters
  and the player automation bridge still matching. Isolated compile run 206
  passed, implemented-scenario run 207 passed `30/30`, and EditMode run 208
  passed `79/79`.
- Migrated title/icon and audio library/mixer resource access behind `IResourcesAssetLoader`, while keeping the direct Unity `Resources` call confined to the loader boundary. The product `Resources.Load` ratchet is now `13`.
- Isolated compile run 209 passed, implemented-scenario run 210 passed `30/30`, and EditMode run 211 passed `79/79`. Unity batch logs still contain external licensing handshake noise, but no scenario or test failures.
- Migrated world character nameplates and defense combat presentation text away from direct TMP font `Resources.Load`. `CharacterActorPresentationBridge` now carries the scoped `ITmpKoreanFontService` into runtime-created presentation components, with manual/editor fallback left non-fatal.
- The product `Resources.Load` ratchet is now `11`. Isolated compile run 212 passed, implemented-scenario run 213 passed `30/30`, EditMode run 214 correctly exposed the stale architecture expectation, and EditMode run 215 passed `79/79` after updating the bridge expectation.
- Migrated dungeon/interior door visual material loading away from direct `Resources.Load`. Doors now receive `IResourcesAssetLoader` through injection and fall back to a shader-created sprite material for manually composed editor cases.
- The product `Resources.Load` ratchet is now `10`. Isolated compile run 216 passed, implemented-scenario run 217 passed `30/30`, and EditMode run 218 passed `79/79`.
- Migrated wildlife habitat decoration palette loading behind the ecosystem-owned `IResourcesAssetLoader` path. Manual decoration fixtures can still pass an explicit palette, while production no longer lets the decoration runtime call `Resources.Load` directly.
- The product `Resources.Load` ratchet is now `9`. Isolated compile run 219 passed, implemented-scenario run 220 passed `30/30`, and EditMode run 221 passed `79/79`.
- Migrated item hauling settings, dungeon item catalog, combat equipment catalog, and expedition equipment catalog loading behind `IResourcesAssetLoader`. The expedition catalog now receives combat definitions from `ICombatEquipmentCatalog` instead of scanning resources inside the SO.
- The product `Resources.Load` ratchet is now `4`. Isolated compile run 222 passed, implemented-scenario run 223 passed `30/30`, and EditMode run 224 passed `79/79`.
- Completed the product direct resource-load cleanup. Character skill settings, skill runtime effects, character trait resolution, item/hauling/combat/expedition catalogs, wildlife decorations, door material, title branding, and audio assets now route through `IResourcesAssetLoader` or scoped providers.
- The product `Resources.Load` ratchet is now `1`, leaving only the `UnityResourcesAssetLoader` infrastructure boundary. Isolated compile run 225 passed, implemented-scenario run 226 passed `30/30`, and EditMode run 227 passed `79/79`.
- Removed the remaining product `Resources.FindObjectsOfTypeAll` scene scans from
  UI/audio runtime paths. `DungeonAudioController` now scans only loaded scene
  roots for buttons, and `OwnerSelectionPanel` checks the generated save modal
  through loaded-scene hierarchy traversal instead of the editor-wide resources
  table.
- Added an architecture ratchet forbidding `Resources.FindObjectsOfTypeAll` in
  product code. Isolated compile run 228 passed, implemented-scenario run 229
  passed `30/30`, and EditMode run 230 passed `79/79`.
- Removed the final product `GameObject.Find` fallback from
  `DungeonRuntimeHierarchy`; runtime category roots are now resolved only by
  the target loaded scene's root objects or created explicitly in that scene.
- Expanded the scene-search ratchet to cover `GameObject.Find` as well as
  `Object.Find*ByType`. Isolated compile run 231 passed, implemented-scenario
  run 232 passed `30/30`, and EditMode run 233 passed `79/79`.
- Migrated the player automation bridge away from direct Unity `Time.*` access.
  The bridge now receives `IGameClock`, `IUiClock`, and
  `IGameTimeScaleController`, while editor PlayMode verifiers pass explicit
  Unity clock adapters into the static automation input buffer.
- Compile run 234 correctly exposed six stale verifier calls to
  `DungeonAutomationInputState.Enable()`. After updating those callers and
  moving the bridge's injected clocks behind a static readonly holder so the
  mutable-static ratchet did not grow, compile run 237 passed, EditMode run
  238 passed `79/79`, and implemented-scenario run 239 passed `30/30`.
- The product direct `Time.*` ratchet is now `8`, leaving only the Foundation
  Unity clock/time-scale adapter boundary.
- Moved the `DungeonDebugRuntimeRules` service pointer behind a static readonly
  state holder and tightened the mutable-static architecture ratchet from
  `40` to `38`. This does not remove the remaining debug compatibility facade
  yet, but it prevents the final checkpoint from regressing static runtime
  state while later DI call-site migrations continue.
- Compile run 240 passed, EditMode run 241 passed `79/79`, and
  implemented-scenario run 242 passed `30/30` after the debug-rule holder
  cleanup.
- Moved the work catalog built-in registrations onto stable `BuiltInWorkTypeIds`
  and gave `WorkTypeDefinition` a typed `WorkTypeId` property while keeping the
  legacy string `Id` facade for save and editor compatibility. Work execution,
  building fallback dispatch, stat policies, and survival work resolution now
  reuse the catalog's typed id instead of reconstructing `new WorkTypeId` from
  strings.
- Added a ratchet that forbids built-in work registration from raw `"work:*"`
  literals in `WorkTypeCatalog` and rejects product call sites that rebuild a
  typed id from `definition.Id`. Isolated compile run 243 passed,
  implemented-scenario run 244 passed `30/30`, and EditMode run 245 passed
  `79/79`.
- Opened typed `WorkTypeId` entry points on the work execution and stat-policy
  registries. Legacy `FacilityWorkType` calls now resolve through
  `WorkTypeCatalog` once and delegate to the typed path instead of keeping
  enum-only registry APIs as the only option.
- Compile run 246 passed, implemented-scenario run 247 passed `30/30`, and
  EditMode run 248 passed `79/79` after the typed registry API slice.
- Added `WorkTypeId` to `WorkExecutionContext` and changed the main
  `WorkTaskExecutor` handler path to resolve the catalog definition once,
  then pass the typed id into both the handler lookup and execution context.
  The survival handler now reads `context.WorkTypeId` directly instead of
  resolving the legacy enum again.
- Compile run 249 passed, implemented-scenario run 250 passed `30/30`, and
  EditMode run 251 passed `79/79` after the execution-context typed-id slice.
- Opened typed priority APIs on `WorkPriorityProfile` and `AbilityWork`, while
  preserving serialized string work IDs and the legacy enum wrappers for
  existing UI and actor state. Character save restore now applies saved work
  priorities through `definition.WorkTypeId` instead of re-entering through the
  enum definition type.
- Compile run 252 passed, implemented-scenario run 253 passed `30/30`, and
  EditMode run 254 passed `79/79` after the work-priority typed-id slice.
- Migrated AI routine-priority reads for duty aggregation, hauling, hunting,
  and rescue from hard-coded `FacilityWorkType` enum lookups to
  `BuiltInWorkTypeIds`. The enum remains in execution state and candidate
  selection, but AI priority scoring now consumes the stable work-id surface.
- Compile run 255 passed, implemented-scenario run 256 passed `30/30`, and
  EditMode run 257 passed `79/79` after the AI priority typed-id slice.
- Migrated fixed guard/research priority reads in combat loadout prep,
  rebellion and deprivation suppression, defense/research feature presenters,
  and defense engagement dispatch from `FacilityWorkType` enum calls to
  `BuiltInWorkTypeIds`. Added architecture ratchets to keep those product
  call sites on the stable typed-id API.
- Compile run 258 passed, implemented-scenario run 259 passed `30/30`, and
  EditMode run 260 passed `79/79` after the guard/research priority typed-id
  slice.
- Migrated the staff work-priority panel and its refresh hash from
  `WorkTaskCatalog.TaskTypes` enum iteration to `WorkTaskCatalog.Definitions`
  and `WorkTypeId` reads/writes. The UI object name still keeps the legacy enum
  suffix for existing pointer verifiers, but the priority command path now goes
  through the typed work-id API.
- Compile run 261 passed, implemented-scenario run 262 passed `30/30`, and
  EditMode run 263 passed `79/79` after the staff priority UI typed-id slice.
- Migrated dynamic work-priority reads in `WorkforceReplanService` and
  `WorkDutyController` by resolving the legacy `FacilityWorkType` through
  `WorkTypeCatalog` once, then reading priority with the resulting
  `WorkTypeId`. Product runtime code no longer passes raw `FacilityWorkType`
  constants or variables directly into `WorkPriorities` priority lookups.
- Compile run 264 passed, implemented-scenario run 265 passed `30/30`, and
  EditMode run 266 passed `79/79` after the dynamic priority typed-id slice.
- Added a `WorkTypeId` overload to `IWorkforceReplanService` and made the
  legacy `FacilityWorkType` overload a catalog-resolving wrapper. Blueprint
  research now requests research reprioritization through
  `BuiltInWorkTypeIds.Research`; editor no-op implementations were extended for
  the typed facade.
- Compile run 267 passed, implemented-scenario run 268 passed `30/30`, and
  EditMode run 269 passed `79/79` after the workforce replan typed-facade
  slice.
- Added typed current-work surfaces to `AbilityWork` and `WorkCommandHandler`
  with `AssignedWorkTypeId`, `PriorityWorkTypeId`, and an assigned definition
  helper. `WorkforceReplanService`, `WorkDutyController`, the research feature
  query, and work debug context now use stable work IDs for priority/display
  decisions while preserving the legacy enum for actual candidate evaluation
  and execution.
- Compile run 270 failed because `TryGetAssignedWorkDefinition` did not assign
  its `out` parameter when the current work type was `None`; assigning `null`
  before the catalog lookup fixed it.
- Compile run 271 passed, implemented-scenario run 272 passed `30/30`, and
  EditMode run 273 passed `79/79` after the assigned-work typed-id slice.
- Added `AbilityWork.IsAssignedWork(WorkTypeId)` and
  `IsPriorityWork(WorkTypeId)` helper methods, then migrated the research
  feature assignment count and routine operate/guard shift check to those
  helpers. The remaining `AssignedWorkType`/`PriorityWorkType` comparisons are
  now concentrated in work target selection and task execution, where the
  legacy enum still defines the candidate/execution bridge.
- Compile run 274 passed, implemented-scenario run 275 passed `30/30`, and
  EditMode run 276 passed `79/79` after the assigned-work helper slice.
- Removed the unused `FacilityWorkType` overload from
  `IWorkforceReplanService` and its concrete/editor no-op implementations.
  The workforce replan service now exposes only the stable `WorkTypeId` request
  surface, with `BlueprintResearchSystem` remaining on
  `BuiltInWorkTypeIds.Research`.
- Compile run 277 passed, implemented-scenario run 278 passed `30/30`, and
  EditMode run 279 passed `79/79` after removing the workforce replan enum
  facade.
- Added typed work metadata to `WorkTargetCandidate` (`WorkTypeId` and
  `DisplayName`) and changed `WorkTargetSelector` to enumerate
  `WorkTypeDefinition` values for candidate scoring. Routine target priority
  selection now reads `WorkPriorityProfile` through `WorkTypeId`, while the
  legacy enum remains only as the execution/serialization bridge.
- The first scenario run attempt after compile found a stale verification
  Unity process still holding the clone; after waiting for the clone process
  to exit, implemented-scenario run 281 passed `30/30`.
- Compile run 280 passed with no C# errors or warnings, and EditMode run 282
  passed `79/79` after the work-target candidate typed-id slice.
- Added typed `WorkTypeId` entry points to `IWorkPolicyRegistry` and routed
  `WorkTargetSelector` candidate availability/urgency checks through them.
  Candidate providers still receive the legacy enum at the final adapter edge,
  but selector and policy lookup now stay on stable work IDs.
- Compile run 283 passed, implemented-scenario run 284 passed `30/30`, and
  EditMode run 285 exposed a ratchet-only newline mismatch in
  `WorkAssemblyOwnsStableWorkIds`. Adjusting the test to source-loader `\n`
  newlines fixed it, and EditMode run 286 passed `79/79`.
- Removed the now-unused `FacilityWorkType` overloads from
  `IWorkExecutionHandlerRegistry` and `IWorkPolicyRegistry`. The policy
  registry surface is now typed-id only; the registered candidate/urgency
  providers remain the final legacy-enum adapter edge.
- Compile run 287 passed, implemented-scenario run 288 passed `30/30`, and
  EditMode run 289 exposed that the first ratchet was banning the provider
  adapter instead of only the registry surface. Narrowing the assertion to the
  `IWorkPolicyRegistry` body fixed it, and EditMode run 290 passed `79/79`.
- Removed the unused `FacilityWorkType` overload from
  `IWorkStatPolicyRegistry`. Work amount calculation already resolves the
  definition once and calls the typed stat-policy API, so the stat policy
  registry now exposes only `WorkTypeId`.
- Compile run 291 passed, implemented-scenario run 292 passed `30/30`, and
  EditMode run 293 passed `79/79` after the stat-policy facade removal.
- Added a typed `WorkTypeId` overload to `IWorkAmountCalculator` and routed the
  main `WorkTaskExecutor` work-speed calculation plus the combat equipment
  repair handler through it. The legacy overload remains as an adapter for
  older call sites, but the verified product execution paths now enter through
  stable work IDs.
- Compile run 294 passed, implemented-scenario run 295 passed `30/30`, and
  EditMode run 296 passed `79/79` after the work-amount typed-entry slice.
- Removed the legacy `FacilityWorkType` overload from `IWorkAmountCalculator`
  after routing the verified product execution paths through `WorkTypeId`.
  Unknown legacy work types now fall back to the executor's local speed
  calculation instead of re-entering the calculator facade.
- Compile run 297 passed, implemented-scenario run 298 passed `30/30`, and
  EditMode run 299 passed `79/79` after removing the work-amount enum facade.
- Removed the enum-only `WorkExecutionContext` constructor and its fallback
  `new WorkTypeId($"work:{(int)legacyWorkType}")` path. All context creation
  now has to provide a valid registered `WorkTypeId`; the defense repair
  fixture was updated to pass `BuiltInWorkTypeIds.Repair` explicitly.
- Compile run 300 passed, implemented-scenario run 301 passed `30/30`, and
  EditMode run 302 passed `79/79` after the execution-context facade removal.
- Migrated runtime facility/shop assignment checks and work-priority fallback
  loops from `WorkTaskCatalog.GetSingleTypes` to `WorkTypeCatalog.Enumerate`,
  so those paths keep registered `WorkTypeDefinition` metadata instead of
  re-expanding open-ended enum flags. Added architecture ratchets forbidding
  product `WorkTaskCatalog.GetSingleTypes` and `WorkTaskCatalog.TaskTypes`
  call sites.
- Compile run 303 passed with no C# compiler errors or warnings,
  implemented-scenario run 304 passed `30/30`, and EditMode run 305 passed
  `79/79` after the work-definition enumeration slice.
- Added typed `WorkTypeId` overloads for work environment duration,
  routine-work throttling, and routine cooldowns on `AbilityWork`, then routed
  restock work, generic work-order execution, timed work, repair execution,
  and work-duty shifts through those stable IDs. The legacy enum overloads
  remain only as compatibility wrappers around `WorkTypeCatalog`.
- Compile run 306 passed with no C# compiler errors or warnings,
  implemented-scenario run 307 passed `30/30`, and EditMode run 308 passed
  `79/79` after the work-environment typed-id slice.
- Added typed work-speed and work-preference entry points to `CharacterActor`,
  `CharacterStats`, and `CharacterModelData`, then routed equipment crafting,
  rescue/treatment work, and blueprint research through `BuiltInWorkTypeIds`
  instead of fixed `FacilityWorkType` constants. The old stat APIs remain as
  compatibility wrappers for legacy mask-based callers.
- Compile run 309 passed with no C# compiler errors or warnings,
  implemented-scenario run 310 passed `30/30`, and EditMode run 311 passed
  `79/79` after the character work-speed typed-id slice.
- Removed the now-unused `WorkTaskCatalog.GetSingleTypes` compatibility
  helper. Runtime code already enumerates `WorkTypeDefinition` values, and
  remaining editor-only task enumeration still uses `TaskTypes` until those
  fixtures are migrated.
- Compile run 312 passed with no C# compiler errors or warnings,
  implemented-scenario run 313 passed `30/30`, and EditMode run 314 passed
  `79/79` after deleting the single-type helper facade.
- Migrated the last editor scenario callers from `WorkTaskCatalog.TaskTypes`
  to registered `WorkTypeDefinition` lists or `WorkTypeCatalog` lookup, then
  removed the `TaskTypes` compatibility property from `WorkTaskCatalog`.
- Compile run 315 passed with no C# compiler errors or warnings,
  implemented-scenario run 316 passed `30/30`, and EditMode run 317 passed
  `79/79` after deleting the task-types helper facade.
- Added typed repeated-work and target-work fatigue entry points to
  `CharacterAiMemoryRuntime`, then routed work target scoring through
  `WorkTypeId` for actor work preference, work speed, repeated-work fatigue,
  and target fatigue. The work amount fallback now derives one `WorkTypeId`
  and uses it for both the registered calculator and the no-calculator speed
  fallback.
- Compile run 318 passed with no C# compiler errors or warnings and
  implemented-scenario run 319 passed `30/30`. EditMode run 320 exposed a
  ratchet-only expectation still looking for `definition.WorkTypeId` in the
  calculator call; updating it to the new local `workTypeId` variable fixed
  the assertion, and EditMode run 321 passed `79/79`.
- Removed the `WorkTargetCandidate` legacy `FacilityWorkType` constructor and
  its private definition resolver. Target candidates now have a single
  definition-based constructor; invalid candidates pass a null definition and
  remain invalid through the existing `WorkTypeId.IsValid` check.
- Compile run 322 passed with no C# compiler errors or warnings,
  implemented-scenario run 323 passed `30/30`, and EditMode run 324 passed
  `79/79` after the work-target candidate constructor cleanup.
- Added typed `WorkTypeId` overloads to `IWorkOrderRuntime`/`WorkOrderRuntime`
  for order lookup and work application, then routed the main timed work-order
  executor through those stable IDs. Work orders still persist the legacy enum
  for snapshot compatibility, but the hot execution path now resolves a single
  `WorkTypeDefinition` and no longer re-enters the enum order API.
- Removed the now-unused private `FindOrder(BuildableObject, FacilityWorkType)`
  helper and added ratchets to keep work-order runtime execution on typed IDs.
- Compile run 325 passed with no C# compiler errors or warnings,
  implemented-scenario run 326 passed `30/30`, and EditMode run 327 passed
  `79/79` after the work-order typed-entry slice.
- Routed construction-site status, building summary/details UI, and the
  developer work-order commands through `BuiltInWorkTypeIds.Construct` or the
  registered `WorkTypeCatalog.All` list instead of directly probing
  `FacilityWorkType` values. The debug command provider no longer enumerates
  `Enum.GetValues(typeof(FacilityWorkType))` for work-order lookup/cancel paths.
- Compile run 328 passed with no C# compiler errors or warnings,
  implemented-scenario run 329 passed `30/30`, and EditMode run 330 passed
  `79/79` after the construction work-order caller cleanup.
- Migrated the remaining editor verifier/debug scenario work-order calls to
  `BuiltInWorkTypeIds.Construct`, then removed the legacy
  `IWorkOrderRuntime.TryGetOrderFor(... FacilityWorkType ...)` and
  `IWorkOrderRuntime.ApplyWork(... FacilityWorkType ...)` overloads entirely.
  `IWorkOrderRuntime` is now typed-id only at its public boundary.
- Compile run 331 passed with no C# compiler errors or warnings,
  implemented-scenario run 332 passed `30/30`, and EditMode run 333 passed
  `79/79` after deleting the work-order enum facade.
- Added typed `WorkTypeId` utility/candidate/urgent-work overloads to
  `WorkTargetSelector` and `AbilityWork`, then routed `AIWork` and
  `ConsiderationWorkNeed` through those stable IDs for specific work actions.
  Existing serialized `FacilityWorkType` action fields remain only as asset
  configuration and the `None` sentinel still represents any work.
- Compile run 334 passed with no C# compiler errors or warnings,
  implemented-scenario run 335 passed `30/30`, and EditMode run 336 passed
  `79/79` after the AI work scoring typed-call slice.
- Removed the unused legacy `CharacterAiMemoryRuntime` overloads for
  `GetRepeatedWorkFatigue(FacilityWorkType)` and
  `GetRecentTargetWorkFatigue(BuildableObject, FacilityWorkType)`. The only
  remaining work-fatigue query surface is now `WorkTypeId` based, and the
  ratchets forbid reintroducing the enum overloads.
- Compile run 337 passed with no C# compiler errors or warnings,
  implemented-scenario run 338 passed `30/30`, and EditMode run 339 passed
  `79/79` after the AI memory fatigue facade removal.
- Routed the work amount calculator through
  `actor.GetWorkSpeedMultiplier(definition.WorkTypeId)`, migrated editor
  character model and owner checks to `BuiltInWorkTypeIds`, and removed the
  `CharacterActor` enum speed/preference facades. CharacterActor now exposes
  only typed work speed/preference queries.
- Compile run 340 passed with no C# compiler errors or warnings,
  implemented-scenario run 341 passed `30/30`, and EditMode run 342 passed
  `79/79` after the CharacterActor work-stat facade removal.
- Removed the remaining public enum work-stat facades from `CharacterStats`
  and `CharacterRuntimeProfile`. Their `WorkTypeId` APIs now resolve one
  `WorkTypeDefinition` and calculate directly instead of delegating back to
  `FacilityWorkType` overloads, while internal mask checks stay private for
  serialized trait/species compatibility.
- Compile run 343 passed with no C# compiler errors or warnings,
  implemented-scenario run 344 passed `30/30`, and EditMode run 345 passed
  `79/79` after the character stats/profile work-stat facade cleanup.
- Converted building work-amount APIs from `FacilityWorkType` to
  `WorkTypeId`: `IBuildingWorkAmountRuntimeAbility`,
  `BuildingWorkAmountAbility`, and `BuildingSO.GetRequiredWork` now expose
  only typed-id entry points. Construction, repair, clean, research, and
  editor validation callers were moved to `BuiltInWorkTypeIds`, with legacy
  enum switches retained only as private fallback calculations.
- Compile run 346 passed with no C# compiler errors or warnings,
  implemented-scenario run 347 passed `30/30`, and EditMode run 348 passed
  `79/79` after the building required-work typed-id cleanup.
- Split the "any work" AI path away from the `FacilityWorkType.None` sentinel.
  `WorkTargetSelector` and `AbilityWork` now expose explicit any-work
  utility/candidate/start helpers, while `AIWork`, `AIWait`,
  `ConsiderationWorkNeed`, workforce replanning, and QA probes use the
  typed/any APIs instead of passing `None` through the specific-work facade.
- Compile run 349 passed with no C# compiler errors or warnings,
  implemented-scenario run 350 passed `30/30`, and EditMode run 351 passed
  `79/79` after the any-work API separation.
- Added `AbilityWork.TryAssignWork(WorkTypeId)` and migrated the remaining
  specific-work debug/verifier calls to `BuiltInWorkTypeIds`. Removed the
  public `AbilityWork` enum facades for assign, utility score, and start
  checks; serialized enum requests now pass through a private bridge only.
- Compile run 352 passed with no C# compiler errors or warnings,
  implemented-scenario run 353 passed `30/30`, and EditMode run 354 passed
  `79/79` after the AbilityWork specific-work public facade cleanup.
- Lowered `WorkTargetSelector`'s enum work-selection methods to private legacy
  adapters. Its public assign, urgency, best-candidate, and utility APIs now
  expose only `WorkTypeId` or explicit any-work methods, and the remaining
  editor/verifier calls were migrated to typed or any-work entry points.
- Compile run 355 passed with no C# compiler errors or warnings,
  implemented-scenario run 356 passed `30/30`, and EditMode run 357 passed
  `79/79` after the WorkTargetSelector public-facade cleanup.
- Migrated `AbilityWork.SetWorkPriority` editor/verifier callers to
  `BuiltInWorkTypeIds`, removed its public enum setter overloads, and deleted
  the unused enum routine-throttle/cooldown wrappers. Routine throttling now
  uses `WorkTypeId` from `WorkTargetSelector` and `WorkDutyController`.
- Compile run 358 passed with no C# compiler errors or warnings,
  implemented-scenario run 359 passed `30/30`, and EditMode run 360 passed
  `79/79` after the AbilityWork priority/cooldown facade cleanup.
- Removed the public `WorkPriorityProfile` enum priority facades. Single-work
  priority reads/writes and enabled checks now use `WorkTypeId`, combined
  priority checks use an explicit typed-id list, and owner serialized work masks
  are interpreted only inside `AbilityWork` before applying typed preferences.
- After syncing the verification clone to the current workspace, compile run
  363 passed with no C# compiler errors or warnings, implemented-scenario run
  364 passed `30/30`, and EditMode run 365 passed `79/79` after the
  WorkPriorityProfile typed-id cleanup.
- Migrated the physical-capacity work multiplier from `FacilityWorkType` to
  `WorkTypeId` and removed the unused `AbilityWork` environment-duration enum
  wrapper. Character stat calculations now feed body-capacity penalties with
  the resolved typed work id.
- Compile run 366 passed with no C# compiler errors or warnings,
  implemented-scenario run 367 passed `30/30`, and EditMode run 368 passed
  `79/79` after the physical-capacity work multiplier cleanup.
- Renamed the enum-based work display-name formatter to the internal
  `WorkTaskCatalog.GetLegacyDisplayName` bridge. `WorkTaskCatalog` now exposes
  only typed-id display-name lookup publicly, while legacy work-state labels
  are explicitly marked as internal compatibility paths.
- Compile run 369 passed with no C# compiler errors or warnings,
  implemented-scenario run 370 passed `30/30`, and EditMode run 371 passed
  `79/79` after the work display formatter facade cleanup.
- Added architecture ratchets that forbid reintroducing the public
  `WorkPriorityProfile` enum priority/preference facades and the public
  enum-based `WorkTaskCatalog.GetDisplayName` overload. The remaining enum
  display bridge is explicitly internal.
- Compile run 372 passed with no C# compiler errors or warnings,
  implemented-scenario run 373 passed `30/30`, and EditMode run 374 passed
  `79/79` after the priority/formatter ratchet update.
- Added public typed `SupportsWork(WorkTypeId)` overloads to `FacilityData`
  and `BuildableObject`, moved their enum overloads to internal legacy bridges,
  and migrated research, defense, UI, shop, modular facility, and QA callers to
  `BuiltInWorkTypeIds`.
- Compile run 375 passed with no C# compiler errors or warnings,
  implemented-scenario run 376 passed `30/30`, and EditMode run 377 passed
  `79/79` after the `SupportsWork` typed-overload migration.
- Added public typed `CanAssignWork(WorkTypeId, ...)` and
  `GetWorkAssignmentStatus(WorkTypeId)` entry points to `BuildableObject`,
  moved the enum overloads to internal legacy bridges, and migrated selector,
  duty, facility, shop, command resolution, and editor callers to typed ids.
- Compile run 379 passed with no C# compiler errors or warnings,
  implemented-scenario run 380 passed `30/30`, and EditMode run 381 passed
  `79/79` after the work-assignment typed-overload migration.
- Reworked building work urgency to expose `GetWorkUrgency(WorkTypeId)` publicly
  and moved the enum virtual path to internal `GetLegacyWorkUrgency`. The
  selector, construction site, shop, exterior marker, filth target, survival
  verifier, and offense debug checks now use typed urgency queries.
- Compile run 382 passed with no C# compiler errors or warnings,
  implemented-scenario run 383 passed `30/30`, and EditMode run 384 passed
  `79/79` after the work-urgency typed-overload migration.
- Added ratchets for the typed building work surfaces: `SupportsWork`,
  `CanAssignWork`, `GetWorkAssignmentStatus`, and `GetWorkUrgency` must remain
  public `WorkTypeId` APIs, while enum-based variants stay non-public legacy
  bridges.
- Compile run 385 passed with no C# compiler errors or warnings,
  implemented-scenario run 386 passed `30/30`, and EditMode run 387 passed
  `79/79` after the building work-surface ratchet update.
- Migrated `IBuildingExteriorWorkRuntimeAbility` and the reception, patrol,
  outdoor-rest, and exterior-maintenance ability implementations from
  `FacilityWorkType` parameters to `WorkTypeId`. Work execution, work target
  scoring, exterior completion handlers, and exterior debug checks now call the
  typed exterior-work APIs.
- Compile run 392 passed with no C# compiler errors or warnings,
  implemented-scenario run 393 passed `30/30`, and EditMode run 394 passed
  `79/79` after the exterior-work ability typed-api migration.
- Added a public typed `CodexTextFormatter.FormatWorkTypes(IEnumerable<WorkTypeId>)`
  and moved the enum formatter to internal `FormatLegacyWorkTypes`. Added
  ratchets that keep exterior work ability APIs and the Codex work formatter on
  typed ids.
- Compile run 395 passed with no C# compiler errors or warnings,
  implemented-scenario run 396 passed `30/30`, and EditMode run 397 passed
  `79/79` after the exterior/Codex formatter ratchet update.
- Migrated `IRoomEnvironmentQuery.GetWorkDurationMultiplier` from
  `FacilityWorkType` to `WorkTypeId`, kept the enum path as an internal legacy
  bridge, and added an architecture ratchet so the room environment work
  multiplier stays on typed work ids.
- Compile run 398 passed with no C# compiler errors or warnings,
  implemented-scenario run 399 passed `30/30`, and EditMode run 400 passed
  `79/79` after the room-environment work multiplier cleanup.
- Moved `BuildingAbilityWorkContext.WorkType` behind an internal legacy bridge
  and updated combat crafting, survival work, butcher, and editor
  characterization handlers to consume `WorkTypeId` from the public context.
  The interrupted EditMode run was rerun as EditMode 405 and passed `79/79`.
- Migrated `ISurvivalFoodRuntime` and `SurvivalFoodRuntime` public survival-work
  APIs (`TryApplySurvivalWork`, `HasSurvivalWorkAvailable`,
  `GetSurvivalWorkUrgency`) from `FacilityWorkType` to `WorkTypeId`, updated
  deprivation, survival work execution, and survival building handlers, and
  added ratchets to prevent the enum API from returning.
- Compile run 406 passed with no C# compiler errors or warnings,
  implemented-scenario run 407 passed `30/30`, and EditMode run 408 passed
  `79/79` after the survival-work API cleanup.
- Migrated `RoomEnvironmentExperienceEvent` so public construction and access
  use `WorkTypeId` for work experiences. Facility/shopping events keep the
  three-argument constructor, while the legacy enum is internal for label
  compatibility only.
- Compile run 409 passed with no C# compiler errors or warnings,
  implemented-scenario run 410 passed `30/30`, and EditMode run 411 passed
  `79/79` after the room-experience work id cleanup.
- Migrated `CharacterSkillRuntimeEffects` work-start/work-completed public
  events and `CharacterSkillExecutionContext` from `FacilityWorkType` to
  stable `WorkTypeId`. Work execution and progression debug scenarios now pass
  typed ids into skill triggers, and a ratchet prevents the public enum work
  event surface from returning.
- Compile run 412 passed with no C# compiler errors or warnings,
  implemented-scenario run 413 passed `30/30`, and EditMode run 414 passed
  `80/80` after the skill work-event id cleanup. Remaining public
  `FacilityWorkType` surfaces: `27`.
- Migrated `AIWork` and `ConsiderationWorkNeed` public work-type access from
  `FacilityWorkType` to `WorkTypeId` while keeping their serialized enum fields
  for existing AI asset compatibility. Updated the stable-work-id ratchet so AI
  action and consideration APIs cannot re-expose enum work types.
- Compile run 418 passed with no C# compiler errors or warnings,
  implemented-scenario run 419 passed `30/30`, and EditMode run 420 passed
  `80/80` after the AI work getter cleanup. Remaining public
  `FacilityWorkType` surfaces: `25`.
- Moved `AbilityWork.AssignedWorkType`, `AbilityWork.PriorityWorkType`, and
  `WorkCommandHandler.PriorityWorkType` from public API to internal legacy
  compatibility, migrated editor/QA checks to `AssignedWorkTypeId` and
  `PriorityWorkTypeId`, and added ratchets to keep those enum properties
  non-public.
- Compile run 421 passed with no C# compiler errors or warnings,
  implemented-scenario run 422 passed `30/30`, and EditMode run 423 passed
  `80/80` after the ability work public-enum cleanup. Remaining public
  `FacilityWorkType` surfaces: `22`.
- Migrated `CharacterAiMemoryRuntime.RecordWork` and
  `CharacterAiMemoryEntry` work storage from public `FacilityWorkType` to
  typed/stable work ids. Work execution now records AI work memory with
  `WorkTypeId`, and architecture ratchets keep AI memory entries and recording
  APIs off enum work types.
- Compile run 426 passed with no C# compiler errors or warnings,
  implemented-scenario run 427 passed `30/30`, and EditMode run 428 passed
  `80/80` after the AI memory work-id cleanup. Remaining public
  `FacilityWorkType` surfaces: `21`.
- Moved `WorkExecutionContext.LegacyWorkType` from public API to an internal
  compatibility property and added a ratchet to prevent the legacy enum
  execution context from becoming public again.
- Compile run 429 passed with no C# compiler errors or warnings,
  implemented-scenario run 430 passed `30/30`, and EditMode run 431 passed
  `80/80` after the work execution context legacy-surface cleanup. Remaining
  public `FacilityWorkType` surfaces: `20`.
- Moved `WorkTargetCandidate.WorkType` from public API to an internal legacy
  bridge. Added typed overloads for priority/manual work targeting so editor QA
  probes and debug scenarios consume `WorkTypeId` from candidates, and added a
  ratchet to keep candidate work-type output stable-id-only.
- Compile run 432 passed with no C# compiler errors or warnings,
  implemented-scenario run 433 passed `30/30`, and EditMode run 434 passed
  `80/80` after the work target candidate public-enum cleanup. Remaining
  public `FacilityWorkType` surfaces: `19`.
- Migrated work-order save/progress DTOs from public `FacilityWorkType` fields
  to stable work ids. `WorkOrderSaveData` now persists `workTypeId`,
  `WorkOrderProgressState` exposes only `WorkTypeId`, and invalid restored work
  ids are reported and discarded instead of being silently merged.
- Compile run 435 passed with no C# compiler errors or warnings,
  implemented-scenario run 436 passed `30/30`, and EditMode run 437 passed
  `80/80` after the work-order save/progress id cleanup. Remaining public
  `FacilityWorkType` surfaces: `16`.
- Moved `WorkTypeDefinition.Type` and the legacy enum `WorkTypeCatalog`
  lookup/enumeration APIs behind internal compatibility. Editor diagnostics now
  resolve work definitions through `BuiltInWorkTypeIds` or custom stable ids
  instead of enum flags, and ratchets keep the catalog surface stable-id first.
- Compile run 439 passed with no C# compiler errors or warnings,
  implemented-scenario run 440 passed `30/30`, and EditMode run 441 passed
  `80/80` after the work-type catalog public-enum cleanup. Remaining public
  `FacilityWorkType` surfaces: `12`.
- Moved survival, wildlife, and equipment-maintenance fallback work helpers
  behind internal compatibility and changed editor builders/debug scenarios to
  compute or verify work support through local ability checks and stable ids.
  Editor-only asset-builder data members that carried `FacilityWorkType` are no
  longer public.
- Compile run 445 passed with no C# compiler errors or warnings,
  implemented-scenario run 446 passed `30/30`, and EditMode run 447 passed
  `80/80` after the fallback utility/editor-builder public-enum cleanup.
  Remaining public `FacilityWorkType` surfaces: `5`.
- Moved the serialized facility/owner/preference work masks off public fields
  while preserving their Unity field names with `[SerializeField] internal`.
  Added typed accessors for building supported work, owner preferred work, and
  character model preferred/disliked work IDs; UI, codex, editor builders, and
  natural-run diagnostics now consume stable `WorkTypeId` lists instead of
  reading serialized masks directly.
- Compile runs 448-450 exposed the expected fallout from hiding those
  serialized masks: `AbilityWork` needed `System.Collections.Generic`, editor
  fixture object initializers still assigned `supportedWorkTypes`, and one
  combat-cover builder used the wrong local name after conversion. Each was
  migrated to `SetSupportedWorkTypeIds(...)`.
- Compile run 451 passed with no C# compiler errors or warnings. Implemented
  scenario run 452 passed `30/30`, and the architecture EditMode run 453
  passed `76/76` with the new stable-work-id ratchets.
- A wider multiline public-surface scan showed the earlier one-line
  `Remaining public FacilityWorkType surfaces` count was too optimistic. The
  remaining compatibility layer still includes public or effectively public
  enum paths in work candidate/urgency provider signatures,
  `AbilityWork`/`WorkCommandHandler` legacy command methods,
  `ModularFacilityRuntimeEffects`, and the legacy enum definition itself.
  These are the next cleanup targets before the final dependency-direction
  lock can be marked complete.
- Migrated the remaining public work-provider and command surfaces to stable
  `WorkTypeId`: `IWorkCandidateProvider`, `IWorkUrgencyProvider`,
  `WorkExecutionContext`, `AbilityWork`, `WorkCommandHandler`,
  `WorkCommandResolver`, and `WorkTargetSelector` no longer expose legacy
  `FacilityWorkType` through public APIs. `AIWork`, owner commands, QA probes,
  and editor scenarios now route manual/priority work through stable ids.
- Moved building ability work completion and modular facility runtime effects
  to stable work ids as well. `BuildingAbilityWorkContext`,
  `IBuildingAbilityRuntimeDispatcher`, `ModularFacilityRuntimeEffects`, craft,
  research, repair, survival, and debug call sites now use
  `BuiltInWorkTypeIds`/`WorkTypeId` instead of public enum dispatch.
- Closed the last catalog construction escape hatch: `WorkTypeDefinition`
  enum-based constructors and raw definition registration are internal, while
  custom work extension uses the public stable-id overload
  `WorkTypeCatalog.Register(WorkTypeId, ...)`.
- Changed the public `CharacterActivityEvent.Work` factory to accept
  `WorkTypeId`; the legacy enum bridge remains internal for in-assembly
  compatibility while editor/QA callers were migrated to stable ids.
- Added architecture ratchets covering the new typed work provider signatures,
  command APIs, ability work APIs, catalog registration, activity event work
  factory, and the absence of public enum work completion dispatch.
- Compile run 455 passed with no C# compiler errors or warnings, implemented
  scenario run 456 passed `30/30`, and architecture EditMode run 457 passed
  `76/76`. A multiline public-surface scan now shows only the serialized
  compatibility enum definition itself:
  `Assets/Scripts/Buildings/Core/BuildingPrimitives.cs:33`.
- Lowered `FacilityWorkType` itself from `public` to `internal` while keeping
  the enum name and values intact for Unity serialized asset compatibility.
  Added `BuildingAssemblyInfo.cs` with `InternalsVisibleTo("Assembly-CSharp")`
  and `InternalsVisibleTo("Assembly-CSharp-Editor")`, then updated the
  architecture ratchet to require the enum to stay internal and friend-scoped.
- Compile run 458 passed with return code `0` after the internal enum
  transition. Implemented scenario run 459 passed `30/30`, and architecture
  EditMode run 460 passed `76/76`.
- Final public-surface scans now report `public FacilityWorkType surface: 0`
  and `EventObserver/static Active scan: 0`. The remaining direct
  `Find*`/`Time.timeScale` usages are Editor/QA automation utilities, not
  product runtime architecture paths; product architecture ratchets already
  enforce zero scene search and bounded direct time/random access for product
  sources.
