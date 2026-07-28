# Progress

## 2026-07-27

- Extended research fields and the research window filter/labels.
- Expanded the authored research graph from 24 to 72 projects with required cross-links.
- Added static item, recipe, crop, craft-material, and substance definition types.
- Added an economy content catalog and resource usage index.
- Added an editor builder for 101 items, 119 recipes, 8 crops, 12 materials, and 7 substances.
- Registered the catalog/index with the gameplay composition root.
- Split ScriptableObject classes into matching source files after Unity generated missing-script assets.
- Recreated and verified 101 items, 119 recipes, 8 crops, 12 materials, and 7 substances.
- Added exact item-level facility-buffer consumption so recipes cannot substitute another item in the same stock category.
- Added persistent production bills with repeats, target-stock mode, physical material requests,
  partial work progress, cancellation, and loose output stacks.
- Added a narrow production item gateway around the world-item runtime.
- Integrated production work into Craft and Cook execution while retaining legacy fallbacks.
- Added 21 modular production facilities and research unlock mappings.
- Added production economy contract scenarios for exact delivery, wrong-item rejection, partial
  progress restore, physical output, and repeated material requests.
- Fixed the actual runtime compile failure caused by duplicate `applied` locals in the Craft handler.
- Rebuilt the modular content catalog to 94 assets, including P01-P21, and rebuilt research unlocks.
- Production economy contracts passed in Unity Editor.
- Added the `economy.production-bills` V16 save section after physical items and modular facilities.
- Added a dedicated production building panel presenter with matching recipes, exact material/output
  summaries, queue progress, blocked reasons, pause/resume, cancellation, and one-cycle ordering.
- Registered the presenter and delegated rendering from the existing building information window.
- Production save-section JSON round-trip passed in the Unity contract run.
- Console after the latest compile and contract run: Error 0 / Warning 0.
- Phase 3 complete.
- Next: implement gatherable resource nodes, crops, logging, quarry depletion, and their work/save loops.
- Audited wildlife habitat decoration and the registered work execution pipeline. Existing flower
  visuals already react to habitat depletion; phase 4 will connect physical resource nodes without
  disguising them as buildings.
- Logged one noisy `rg` attempt that exceeded the useful output window; subsequent audits use
  narrowly scoped files and patterns.
- Added non-blocking natural resource work nodes backed by existing tree, rock, grass, and flower
  decorations. Logging, gathering, and surface quarry work now deplete the visible source and spawn
  physical loose outputs.
- Added P22 deep quarry and verified the repeatable quarry recipe/work connection.
- Fixed runtime catalog injection so all 119 recipes and all 72 research projects load through
  their asset-loader constructors.
- World resource Play Mode verification passed: 7 nodes, physical logs, depletion, and save capture.
- Added `BuildingCropPlotAbility`, P23 outdoor crop plot, and P24 indoor grow bed.
- Added `CropPlotRuntime`, crop plot save section, physical sowing-material requests, accumulated
  sow/harvest work, weather/temperature/night outdoor growth, and indoor growth tuning.
- Connected Sow and Harvest to the registered resource work handler.
- Rebuilt 97 modular facility assets and the research unlocks for P23/P24.
- Crop Play Mode verification passed: Water delivery, sow work, restored growth, harvest work,
  and 6 physical twilight-grain output.
- Production economy regression contracts still pass after expanding production facilities to 24.
- Console after crop and production verification: Error 0 / Warning 0.
- Completed equipment primary-material integration for all 19 combat equipment definitions.
- Added material-aware damage, penetration/defense, durability, weight, value, repair input, salvage,
  crafting filters, rarity defaults, predicted stats, and character/building UI.
- Added physical blacksteel repair and salvage coverage to the item logistics PlayMode verifier.
- Fixed duplicate repair-equipment delivery requests and added explicit facility destination positions
  to unique physical item stacks.
- Material definition contracts and equipment derivation scenarios pass; the final repair haul
  regression is being rerun after removing the post-pickup scaled-time stall.
- Made repair equipment and exact repair-material requests idempotent across pickup transit and save.
- Preserved facility destination metadata when carried stacks are deposited, preventing repair inputs
  from being immediately hauled back to storage.
- Generalized stored-stock withdrawal to every valid routed destination instead of hardcoded prefixes.
- Added `MATERIAL_REPAIR_NO_DUPLICATE_REQUEST` to the PlayMode verifier.
- Physical item logistics PlayMode verification now passes completely, including blacksteel delivery,
  unique-instance repair, original-material salvage, and warehouse accounting.
- Console after the final physical logistics run: Error 0 / Warning 0.
- Diagnosed the GameplayScene PlayMode VContainer failure from the full Editor log and removed the
  `CharacterMedicalRuntime -> ICharacterDeprivationRuntime` constructor edge.
- Added an infection-reduction game event; the deprivation runtime owns its application and medical
  treatment now publishes the completed result.
- GameplayScene entered PlayMode after the DI fix. This exposed seven duplicated legacy numeric
  recipe IDs, which were corrected by rebuilding all 126 recipes from the authoritative builder.
- Phase 5 complete.
- Next: ingredient-aware meals, diet policy enforcement, substances, tolerance, addiction, withdrawal,
  and their physical-use/save/UI contracts.
- Phase 6 audit confirmed that all 13 meal recipes and all 13 medicine/drug definitions already
  produce exact physical item IDs, but facility use still consumes an abstract Food category and
  grants generic hunger recovery. The next change makes the exact consumed menu authoritative.
- Added meal quality, nutrition, mood, shelf life, preservation, and ingredient-derived diet class
  to all 13 authored meals.
- Added persistent character diet and substance policy/state models plus their V16 save section.
- Added exact physical meal selection, facility delivery requests, emergency diet violations,
  contamination consequences, physical consumption, and enriched meal ledger events.
- Routed both ordinary meal facilities and meal shops through the physical meal service, eliminating
  their generic free hunger-recovery path.
- Added physical substance consumption with policy checks, tolerance, addiction, withdrawal,
  overdose, deterministic random rolls, and work/combat effect queries.
- The changed Survival, gameplay, and Editor assemblies compile with Unity's bundled Roslyn compiler.
- Connected active substance effects to the authoritative character work and combat multipliers.
- Added data-driven medicine treatment potency, infection reduction, detox, and pain relief metadata.
- Medical orders now reserve, haul, save, and consume an exact medicine item; extracted blood remains
  a lower-efficiency physical fallback when no valid medicine can be supplied.
- Connected authored meal shelf life and preservation metadata to spoilage status and freshness ratios.
- Recompiled the gameplay and Editor assemblies successfully with Unity's bundled NetCore Roslyn.
- Logged and corrected one malformed local shell working-directory invocation; no source operation ran.
- Added automatic substance-use AI that selects an allowed physical item, retrieves the exact stored
  stack, consumes it under the character policy, and never exposes generation or fallback text.
- Added persistent livestock sex, age, growth stage, taming, pregnancy/incubation, product progress,
  manure progress, pen policy, compatibility risk, and auto-slaughter models.
- Extended captured wildlife feeding to use exact diet-compatible physical feed and added deterministic
  pen-born wildlife spawning.
- Added the registered `work:animal-care` handler for taming, product collection, manure collection,
  and slaughter designation.
- Added husbandry save data, pen policy UI, animal detail UI, breeding/product outputs, and common
  carcass-flow slaughter.
- Recompiled the Work, Buildings, gameplay, and Editor assemblies successfully with Unity's bundled
  NetCore Roslyn compiler.
- Completed livestock pens, diet-aware physical feeding, compatibility, breeding/incubation,
  products, manure, auto-slaughter, save state, work registration, and UI.
- Added origin-aware waste metadata, direct diet-compatible waste feeding, waste routing policies,
  compost/dog-food/fuel/toxin/incineration recipes, save state, and operations UI.
- Began phase 9 with physical stock surplus policies and three-day regional supply contracts.
- Regional contracts generate independently of current warehouse quantity and require actual hauling
  to the delivery destination before payout.
- Corrected resident counting to use the authoritative owner/staff predicate.
- Phase 7 and phase 8 are complete; phase 9 is in progress.
- Added six persistent grand projects with physical material delivery, registered work-unit
  execution, research/office gating, cancellation, and production/contract/defense/expedition benefits.
- Connected grand-project output benefits to production bills, deep quarry output, and indoor crops.
- Added three-day economy forecasting across physical stock, reservations, bills, crops, livestock,
  resident food/water demand, regional contracts, and the active grand project.
- Registered stock policies, regional contracts, grand projects, and the forecast service in the
  gameplay composition root.
- Added independent V16 save sections for stock policies, regional contracts, and grand projects.
- Extended the warehouse feature surface with shortages/surpluses, stock-policy commands, physical
  regional contract acceptance/status, and grand-project start/cancel controls.
- Added deterministic regional contract sizing and economy-planning contract coverage.
- Bee gameplay and editor assemblies compile successfully after rebuilding stale response outputs.
- The first clean temporary-editor phase 9 contract run compiled successfully but exposed that P25
  폐기 소각로 had code and recipes but no generated BuildingSO or research unlock asset.
- Generated P25 through the official modular builder, added its sprite and BuildingSO, and rebuilt
  the 기초 위생 research unlock to point to building 1097.
- Replaced station and economy-content count literals with builder-owned contracts so future
  content additions cannot silently desynchronize the verifier.
- The authoritative builder defines 126 recipes; the asset folder still contained 119. The seven
  missing assets are the mixed compost, plant/animal low fuel, and four incineration recipes.
- Generated the seven missing physical waste-loop recipe assets; the main catalog now contains all
  126 recipes.
- Added reusable feature-surface control cards, direct minimum/target/maximum stock steppers,
  independent stock-policy toggles/disposition controls, and visible contract accept/decline actions.
- Production economy contracts pass in a clean temporary Unity Editor after importing the complete
  101-item, 126-recipe, 25-station, 72-project content set.
- Gameplay and Editor assemblies compile with Unity Roslyn after the warehouse control UI changes.
- Phase 9 complete.
- Phase 10 started: run gameplay regressions, pointer-driven UI verification, performance, captures,
  and final Console inspection.
- Re-ran the actual world-resource and crop-plot PlayMode contracts. Resource gathering/logging/quarry
  produced physical stacks from seven visible nodes, and the crop contract delivered water/compost/fuel,
  grew, and harvested six twilight-grain items.
- Fixed the warehouse control-card layout so exact pointer clicks no longer fall through to later
  section headers, and made section backgrounds ignore raycasts.
- Extended stock policies to legacy physical category item IDs and kept enabled policies visible in
  the forecast list.
- The pointer-driven warehouse regression now clicks restock, delivery, all three stock steppers,
  policy enable/disposition, contract accept, and contract decline. Every exact target and domain
  state transition passed with captured Error 0 / Warning 0.
- Visually inspected the warehouse and action-state captures; Korean labels, deliveries, contracts,
  and grand-project rows are visible without the previous black frame.
- Recompiled Assembly-CSharp and Assembly-CSharp-Editor with Unity Roslyn: 0 errors.
- Extended the normal-new-run `DungeonGameplayPerformanceProbe` with real livestock population setup.
  The mixed profile now creates a 256x8 gameplay grid, 128 real facilities, 100 pooled staff actors,
  one real high-capacity pen, and 100 real wildlife actors registered as tamed captive livestock.
- Added wildlife-capture and husbandry profiler markers, livestock counts, first/last-quarter managed
  memory comparison, and explicit mixed-population gates: frame p95 16.67ms, scheduler p95 4ms,
  average GC 64KB/frame, sustained managed-memory growth 16MB.
- Added the editor entry point `StartEditorEconomyMixedPopulationProfile` for the accepted
  `100 staff + 100 livestock + x5` scenario.
- The changed gameplay assembly compiles with Unity Roslyn: 0 errors.
- Ran the first actual mixed-population profile in the Unity Editor at x5. It created all requested
  actors, livestock, grid cells, and facilities with Console Error 0 / Warning 0.
- First mixed profile metrics: frame p95 14.326ms, AI scheduler p95 1.258ms, wildlife p95 0.384ms,
  sustained managed-memory delta -11.76MB. The run correctly failed the final gate because average
  GC allocation was 2.38MB/frame with a 163.7MB spike.
- Next: capture slow frames with Unity Raw Profiler, remove the allocation source, and rerun the
  exact profile until the mixed-population gate passes.
- Added real normal-operation profile supplies: five days of physical Food and Water are spawned at
  the drop zone and remain subject to ordinary pickup, hauling, and consumption.
- Removed several hot-path allocation sources: repeated animal enumeration in husbandry, ecosystem
  work for captured livestock, cloned downed-character snapshots, workforce LINQ/list creation,
  expanding AI-memory lists, stat snapshot copies for presentation, delayed nameplate component
  creation, and emergency full-stack item snapshots.
- Latest official mixed profile: frame p95 `12.981ms`, scheduler p95 `1.970ms`, retained managed
  growth `9,031,680B`, Console Error 0 / Warning 0. The only failed gate is baseline-adjusted GC at
  `215,297B/frame`.
- Latest raw supplied trace reduced the former emergency lookup and nameplate allocation spikes.
  Its largest remaining sampled path is `CharacterAiScheduler.ProcessAiBudget` direct allocation
  (`251,590B`), so phase 10 continues with finer decision-pipeline GC instrumentation and removal.
- Reused each `AIBrain`'s action-scoring continuation instead of allocating one for every decision,
  changed need urgency enumeration to indexed loops, and routed emergency thirst reads through the
  nonalloc condition query.
- Added emergency-decision profiler submarkers and found that detailed candidate diagnostics were
  being collected for every visible actor. Restricted full diagnostics to the selected actor
  (owner-only fallback when no selection source exists).
- Added habitat-decoration structure versioning and changed world-resource synchronization to rebuild
  only when the grid or decoration topology changes, removing the per-frame sorted signature.
- Re-ran focused production, physical-item, AI-naturalness, priority-corner-case, and work-amount
  regressions; all passed after these changes.
- Current official mixed-population profile with presentation enabled: frame p95 `10.2ms`,
  scheduler p95 `1.192ms`, average FPS `113.2`, 1% low `69.0`, retained growth `2,265,088B`,
  Console Error 0 / Warning 0. Baseline-adjusted GC remains above target at `110,392B/frame`.
- Started the final allocation pass on crop-plot presentation, character nameplates/status text,
  movement, hauling, and work coroutines.
- Replaced the profile's single drop-zone ration pile with physical stock distributed across nine
  real warehouses and diversified the 128-facility stress dungeon to include storage, water,
  crops, production, meal recovery, and butchery capabilities.
- Added nonalloc consumable-stock candidate copying, saturated-source fallback, earlier safe
  drinking, per-frame relief-start throttling, and correct stored-stock/warehouse aggregate
  consumption.
- Physical item, production economy, dark survival, AI naturalness, and AI priority regressions
  passed after the changes; Unity Console remained Error 0 / Warning 0.
- Distributed-supply mixed profile: frame p95 `15.02ms`, scheduler p95 `1.475ms`, retained growth
  `9,916,416B`, Console Error 0 / Warning 0. Baseline-adjusted GC is still `204,554B/frame`, so
  phase 10 remains in progress with a fresh raw allocation trace.
- Added the stored-warehouse consumption contract and fixed exact aggregate/physical mirror
  withdrawal. `Physical item contracts`, `Dark survival scenarios`, and `Character AI
  naturalness scenarios` pass.
- Added `SurvivalEmergency` as a first-class AI interruption reason and per-action opt-in policy.
  Normal work, hauling, and hunting now yield to safe drinking after minimum persistence; rescue,
  substance use, and deprivation breakdown actions remain protected.
- Added an editor characterization scenario proving the interruptible-work and hard-lock branches.
- Next: rerun the official `100 staff + 100 livestock + x5` mixed profile and verify physical water
  decreases, thirst recovers, breakdown load collapses, and allocation throughput moves toward the
  `64KB/frame` gate.
- Added per-stage deprivation diagnostics and path-aware approach filtering for safe and desperate
  drinking. A successful desperate drink now ends its active thirst breakdown immediately.
- Reset transient debug cheats before profiling, added warehouse/loose supply provenance fields,
  and cleared profile-created warehouse legacy seed stock before physical supply placement.
- Compile, `Character AI naturalness`, `Dark survival`, and `Physical item` contracts passed with
  Console Error 0 / Warning 0.
- Official rerun: frame p95 `12.649ms`, scheduler p95 `1.700ms`, retained growth `10,461,184B`,
  Console Error 0 / Warning 0. Movement failures fell to `8`, but only `74` Food fit into nine
  pre-existing warehouses and all `500` Water remained at the drop zone; the deprivation storm
  therefore persisted and incremental GC remained `171,080B/frame`.
- Expanded the dense profile facility predicate to include `GridLayer.Building`, allowing real
  storage, water, crop, production, meal, and butchery facilities to enter the 128-facility mix.
- Diagnosed 354 safe-drink movement failures as synthetic profile stair links without movement
  handlers. Added a real-time performance stair occupant and generalized `AbilityMove` to the
  movement-handler interface; compile and focused regressions remain clean.
- Official rerun after the stair fix: frame p95 `12.880ms`, scheduler p95 below target, retained
  memory within target, and Console Error 0 / Warning 0. Blocked movement failures are now `0`,
  but facilities were concentrated on floors 0-1, leaving upper-floor residents on long water
  routes and incremental GC at `170,757B/frame`.
- Changed dense facility placement to distribute each floor's quota across all horizontal room
  segments. The next official profile will verify steady-state thirst and GC under the corrected
  large-dungeon topology.
- Balanced-facility rerun: frame p95 `12.257ms`, scheduler p95 `1.359ms`, incremental GC
  `107,112B/frame`, memory target passed, Console Error 0 / Warning 0. Five active breakdowns
  remained because hydration ranked an adjacent-floor stack as one cell away despite the long
  stair detour.
- Added a floor-transition-aware safe-relief heuristic and raised the proactive hydration threshold
  from 45 to 65 so normal same-floor travel can finish before critical thirst.
- Fixed the current Unity compile failure caused by an unsupported `Queue<GridMoveStep>.EnsureCapacity`
  call in the path scratch pool. New queues are still sized at construction and pooled queues retain
  their allocated backing storage without relying on a newer framework API.
- Removed the redundant write-only `AIBrain.externalReplanPending` field. External-driven actions
  already trigger their replan on completion, so behavior is unchanged and the CS0414 warning is gone.
- Restored production-equivalent building registration in `CharacterAiEditorTestDependencies`.
- Added per-grid occupancy-version invalidation to the facility fallback index.
- Changed nearest-facility scoring to consume the queried grid and role index instead of scanning
  every registered building across loaded scenes and editor fixtures.
- The recovery-facility regression now reaches both valid facilities; the remaining focused failures
  are the final action-slice retry and idle-wander/stair destination selection.
- Limited nearest-facility scans to the queried grid's role index, eliminating false deferred
  decisions caused by unrelated registered buildings.
- Added a bounded reachable-cell fallback for idle wandering. Edge starts and valid stair links now
  produce supported paths without replacing the fast randomized same-floor attempt.
- `CharacterAiPlanDebugScenarios` and the complete `StaffDutyDebugScenarios` focused suites pass.
- Removed the remaining shared-asset unlock mutation from facility purchases and offense rewards.
  Runtime unlocks now flow through `BlueprintResearchState`; the facility-shop save section depends
  on the research section and restores legacy unlock IDs without touching `BuildingSO`.
- Added regressions proving facility purchases and rare-facility rewards preserve authored assets
  while unlocking the runtime research state.
- Added `ResearchTreeWindow.CenterProject` and corrected its coordinate handling with measured
  viewport-local transforms. The actual pointer verifier now reaches the locked `실내 재배조`
  project and proves its construction action remains blocked by missing prerequisites.
- Final aggregate editor regressions passed: implemented scenarios, production economy, V16
  integration, research tree, facility shop, and offense rewards.
- Final actual P0 pointer report passed with 72 research nodes, natural work progress, locked
  construction gating, nonblank capture, Error 0 / Warning 0.
- Final actual save UI report passed save, metadata, load restore, delete confirmation/execution,
  pause/resume, and input blocking with Error 0 / Warning 0.
- Final resolution matrix passed title, settings, and gameplay HUD at 1280x720, 1600x900,
  1920x1080, 2560x1440, and 900x1600 with Error 0 / Warning 0.
- Unity Console is Error 0 / Warning 0 after the final PlayMode runs and post-run static-content
  contamination check. The changed source files also pass targeted `git diff --check`.
- Functional implementation is complete. The production-like x5 mixed profile still carries a
  separate incremental-GC optimization debt of roughly 143KB/frame versus the 64KB/frame target;
  frame and AI scheduler budgets pass.
