# Script Coupling Audit

> Auto-generated audit document. Flags are heuristics for finding refactor candidates; inspect files directly before judging responsibility violations.

## Summary

- Script files: 328
- Extracted declarations: 6339
- Misplaced Runtime Editor API Candidates: None
- Runtime scope check: 277 non-Editor/non-debug-scenario scripts, 0 missing from `all-scripts-function-inventory.md` (verified 2026-07-05).

> Runtime-only companion: [runtime-script-coupling-audit.md](runtime-script-coupling-audit.md) excludes scripts under `Editor` directories and is the goal-aligned coupling audit for runtime refactoring work.

> Targeted refresh note: `GameManager`, VContainer scene composition, invasion runtime split, `OperatingDaySettlementRuntime`, `WorkforceReplanService`, `StaffDiscontentRuntime`, staff-discontent work command path, blueprint research work service path, offense reward selector/grant services, codex reference importer service, CharacterStats meta-progression reader, AI scene-query entries, GameData/earning feedback access, facility shop/offense reward catalogs, run-start/owner candidate catalogs, facility synthesis/evolution recipe query services, codex reference/import recipe services, facility shop explicit catalog/run/meta service inputs, stock delivery run-variable reader inputs, shop stock/game data/feedback explicit DI, building condition context, centralized Resources asset loader DI, AI action asset catalog, invasion intruder data provider/factory, TMP Korean font provider/service, DataScriptableObject source provider, unused scene singleton inheritance, OwnerRunManager singleton inheritance, CharacterAi scheduling service, run-variable catalog services, RunVariableRuntimeAccess removal, run-start variable selector service, meta-run progress tracker extraction, event-alert selection/choice presenter extraction, main camera provider extraction, facility/room cache service extraction, TMP Korean font service extraction, invasion intruder factory extraction, room facility policy service extraction, facility evolution state service extraction, facility-evolution room-profile cache DI, facility-evolution state/cache invalidation DI, facility-evolution record cache dirty DI, facility-evolution service state DI, buildable object cache dirty DI, facility-candidate scorer cache DI, work ability cache dirty DI, shop inherited cache dirty DI, character AI facility lookup brain DI, character AI job giver catalog DI, room facility policy explicit DI, facility/room static cache facade lifecycle removal, facility-evolution state static lifecycle removal, TMP Korean font UI component DI, TMP Korean font runtime factory DI, TMP Korean font runtime static facade removal, character AI decision pipeline service DI, runtime static facade definition removal, invasion intruder creation factory ownership, defense status runtime service DI, owner character factory ownership, character behavior tree runtime configurator DI, character feedback bubble service DI, character feedback bubble view factory DI, character social memory service DI, run result panel factory DI, offense panel factory DI, runtime generated panel factory DI, character dialogue bubble factory DI, notice feed item factory DI, and character summary runtime log factory DI were manually refreshed on 2026-07-05 after the explicit-runtime refactor.

> Visual render refresh note: `GridTexture` now separates gameplay hallway occupancy from visible hallway tiles, but keeps hallway tiles visible by default during PlayMode debugging. The old hide-under-buildings synchronization remains opt-in via `hideHallwayUnderBuildings`.

> Passage overlay visual refresh note: `GridTexture` hallway synchronization now treats `Door` the same way as movement buildings, matching `GridBuildingPlacementService` passage-overlay placement policy.

> Spawn factory refresh note: `CharacterSpawner` now receives `ICharacterSpawnObjectFactory` and no longer owns spawned prefab instantiation, component injection loops, or pool object destruction directly.

> Visual root factory refresh note: `OwnerCharacterFactory` and `InvasionIntruderRuntimeFactory` now receive `ICharacterVisualRootFactory`; shared Visual child/SpriteRenderer creation is centralized in `CharacterVisualRootFactory`.

> Grid building object factory refresh note: runtime building GameObject/collider/Rigidbody/component creation is centralized in `GridBuildingObjectFactory` and injected into placement, synthesis, and evolution building creation paths.

> Grid construct button factory refresh note: building-category panel/button and building-select button prefab instantiation moved out of `GridConstructTab` into VContainer-registered `GridConstructButtonFactory`; the tab now orchestrates category state while the factory owns dynamic UI creation and component injection.

> Hallway/room validation refresh note: hidden hallway occupants are no longer auto-created under every placed building. `GridBuildingPlacementService` now reserves room-like footprints and only shares hallway cells for passage overlays, while `FacilityData.selfContainedRoom` lets room-role facilities become usable room instances when they are designed as prefab rooms rather than wall/door shells.

> Staff work priority UI factory refresh note: `StaffWorkPriorityPanel` no longer owns `new GameObject`, `AddComponent`, `Instantiate`, `Destroy`, or `DestroyImmediate` paths. Row/cell/text/button creation and release moved to VContainer-registered `StaffWorkPriorityPanelUiFactory`.

> Owner selection option factory refresh note: `OwnerSelectionPanel` no longer owns option button prefab instantiation or destruction. Option creation/release moved to VContainer-registered `OwnerSelectionOptionButtonFactory`.

## Flag Counts

| Flag | Files | Meaning |
| --- | ---: | --- |
| `GlobalObjectLookup` | 11 | Scene-wide object lookup. Prefer domain provider/cache/service boundaries. |
| `SingletonAccess` | 18 | Direct global singleton access. Raises scene/test coupling. |
| `ResourcesAccess` | 7 | Runtime Resources dependency. Prefer injected catalog/provider for shared systems. |
| `EditorAssetAccess` | 43 | Editor-only API. Must stay in Editor folders or #if UNITY_EDITOR isolated tooling. |
| `Reflection` | 50 | Reflection dependency. Confirm it is debug/tooling or properly encapsulated. |
| `EventBus` | 56 | Global event bus boundary. Should remain at runtime integration edges. |
| `RuntimeObjectCreation` | 80 | Runtime scene object creation. Verify ownership/lifetime boundary. |
| `SceneMutation` | 105 | Scene hierarchy/state mutation. Verify it belongs to UI/factory/runtime owner. |
| `DependencyInjection` | 149 | VContainer registration/injection boundary. Prefer moving singleton/scene lookup behind these seams. |

## High-Risk/Large File Candidates

- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`: 400 lines, 27 declarations, `DependencyInjection, EventBus, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`: 3468 lines, 108 declarations, `GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs`: 239 lines, 14 declarations, `GlobalObjectLookup, SingletonAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanTestScenePreparer.cs`: 171 lines, 8 declarations, `EditorAssetAccess, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`: 1214 lines, 132 declarations, `ResourcesAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/CustomerAiDebugScenarios.cs`: 912 lines, 78 declarations, `SingletonAccess, ResourcesAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Offense/OffenseWorldMapSystem.cs`: 279 lines, 21 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`: 380 lines, 23 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Meta/MetaProgressionSystem.cs`: 247 lines, 31 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/Meta/RunResultPanel.cs`: 29 lines, 4 declarations, `SceneMutation`
- `Assets/Scripts/Character/AI/Editor/WorkPriorityDebugScenarios.cs`: 533 lines, 50 declarations, `GlobalObjectLookup, SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`: 451 lines, 47 declarations, `SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Research/Editor/BlueprintResearchDebugScenarios.cs`: 350 lines, 29 declarations, `SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs`: 310 lines, 31 declarations, `SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/PriorityCommandDebugScenarios.cs`: 286 lines, 32 declarations, `SingletonAccess, EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/Core/OwnerRunManager.cs`: 194 lines, 18 declarations, `DependencyInjection, Reflection, EventBus, SceneMutation`
- `Assets/DataManager.cs`: 64 lines, 4 declarations, `DependencyInjection`
- `Assets/Scripts/Invasion/Editor/InvasionThreatDebugScenarios.cs`: 248 lines, 33 declarations, `GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Operation/Editor/EventAlertDebugScenarios.cs`: 167 lines, 22 declarations, `GlobalObjectLookup, ResourcesAccess, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/StaffDutyDebugScenarios.cs`: 966 lines, 59 declarations, `SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/CharacterAiStressDebugScenarios.cs`: 963 lines, 70 declarations, `SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Operation/EventAlertUiFactory.cs`: 293 lines, 11 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Operation/EventAlertViewPresenter.cs`: 234 lines, 18 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Operation/EventAlertChoicePresenter.cs`: 61 lines, 5 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Operation/EventAlertCanvasProvider.cs`: 39 lines, 4 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/CharacterAiPriorityCornerCaseDebugScenarios.cs`: 703 lines, 64 declarations, `SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`: 783 lines, 48 declarations, `DependencyInjection, Reflection, EventBus, RuntimeObjectCreation`
- `Assets/Scripts/Offense/OffenseRewardSystem.cs`: 294 lines, 22 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs`: 485 lines, 43 declarations, `SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`: 411 lines, 43 declarations, `SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs`: 463 lines, 42 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Offense/Editor/OffenseExpeditionDebugScenarios.cs`: 337 lines, 30 declarations, `GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Meta/Editor/MetaProgressionDebugScenarios.cs`: 327 lines, 30 declarations, `GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Invasion/Editor/InvasionCombatReportDebugScenarios.cs`: 319 lines, 31 declarations, `EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs`: 281 lines, 18 declarations, `DependencyInjection, Reflection, EventBus, SceneMutation`
- `Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs`: 341 lines, 27 declarations, `EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/FacilityShop/Editor/FacilityShopDebugScenarios.cs`: 290 lines, 30 declarations, `EditorAssetAccess, Reflection, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/StaffRebellionResponseDebugScenarios.cs`: 253 lines, 20 declarations, `SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs`: 244 lines, 26 declarations, `SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/StaffDiscontentDebugScenarios.cs`: 224 lines, 27 declarations, `SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/Editor/OwnerDebugScenarios.cs`: 207 lines, 21 declarations, `SingletonAccess, EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/UI/CharacterSummeryInfo.cs`: 228 lines, 18 declarations, `DependencyInjection, Reflection, EventBus, SceneMutation`
- `Assets/Scripts/Offense/Editor/OffenseWorldMapDebugScenarios.cs`: 182 lines, 21 declarations, `GlobalObjectLookup, EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`: 126 lines, 12 declarations, `DependencyInjection, Reflection, EventBus, SceneMutation`
- `Assets/Scripts/Codex/UI/CodexPanel.cs`: 120 lines, 12 declarations, `DependencyInjection, Reflection, EventBus, SceneMutation`
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`: 1001 lines, 71 declarations, `DependencyInjection, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Codex/CodexSystem.cs`: 462 lines, 57 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/Character/AI/Editor/CharacterAiBehaviorDesignerGraphBuilder.cs`: 717 lines, 58 declarations, `GlobalObjectLookup, SingletonAccess, EditorAssetAccess, RuntimeObjectCreation`
- `Assets/Scripts/Character/AI/LocalLlmRequestQueue.cs`: 588 lines, 44 declarations, `Reflection, RuntimeObjectCreation`
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`: 584 lines, 33 declarations, `DependencyInjection, EventBus, SceneMutation`
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanelUiFactory.cs`: 163 lines, 17 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/UI/UITabManager.cs`: 557 lines, 36 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/UI/UITabTopButtonFactory.cs`: 124 lines, 10 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/AI/CharacterAiScheduler.cs`: 561 lines, 63 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`: 455 lines, 34 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/Rooms/Editor/RoomSystemDebugScenarios.cs`: 317 lines, 31 declarations, `EditorAssetAccess, Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Recruitment/Editor/RegularCustomerDebugScenarios.cs`: 300 lines, 34 declarations, `EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Run/Editor/RunVariableDebugScenarios.cs`: 284 lines, 26 declarations, `EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/UI/DungeonSceneBackdropFitter.cs`: 255 lines, 20 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Operation/Editor/OperatingDaySettlementDebugScenarios.cs`: 195 lines, 13 declarations, `EditorAssetAccess, EventBus, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Grid/DungeonStory/UI/GridUIManager.cs`: 118 lines, 15 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Character/Core/CharacterActor.cs`: 730 lines, 121 declarations, `DependencyInjection, Reflection, RuntimeObjectCreation`
- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`: 727 lines, 62 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/Invasion/InvasionThreatRuntime.cs`: 281 lines, 21 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/Character/AI/AiDirectorRuntime.cs`: 527 lines, 44 declarations, `DependencyInjection, Reflection`
- `Assets/Scripts/Grid/Building/GridBuildingRuntime.cs`: 531 lines, 55 declarations, `Reflection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordRuntime.cs`: 451 lines, 58 declarations, `DependencyInjection, EventBus, RuntimeObjectCreation`
- `Assets/Scripts/Character/UI/CharacterFeedbackBubble.cs`: 341 lines, 32 declarations, `DependencyInjection, Reflection, SceneMutation`
- `Assets/Scripts/Offense/Editor/OffenseRewardDebugScenarios.cs`: 347 lines, 43 declarations, `EditorAssetAccess, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Character/CharacterSpawner.cs`: 338 lines, 28 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Character/AI/CharacterDialogueRuntime.cs`: 283 lines, 18 declarations, `DependencyInjection, Reflection, SceneMutation`
- `Assets/Scripts/Grid/System/Editor/GridFoundationDebugScenarios.cs`: 233 lines, 39 declarations, `EditorAssetAccess, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Grid/System/GridSystemManager.cs`: 188 lines, 18 declarations, `SceneMutation`
- `Assets/Scripts/Character/AI/Editor/CharacterFeedbackDebugScenarios.cs`: 131 lines, 11 declarations, `EditorAssetAccess, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/UI/NoticeFeed.cs`: 59 lines, 10 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/UI/NoticeFeedPresenter.cs`: 80 lines, 7 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/UI/CharacterSummaryRuntimeLogFactory.cs`: 70 lines, 8 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructButtonFactory.cs`: 114 lines, 10 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructTab.cs`: 230 lines, 20 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Character/UI/OwnerSelectionPanel.cs`: 140 lines, 10 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Character/UI/OwnerSelectionOptionButtonFactory.cs`: 70 lines, 7 declarations, `DependencyInjection, RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/UI/BuildingSummaryInfo.cs`: 139 lines, 17 declarations, `DependencyInjection, EventBus, SceneMutation`
- `Assets/Scripts/Character/AI/AIBrain.cs`: 1294 lines, 63 declarations, `DependencyInjection`
- `Assets/Scripts/Buildings/Shop.cs`: 959 lines, 78 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Synthesis/Editor/P1FacilitySynthesisAssetBuilder.cs`: 752 lines, 46 declarations, `EditorAssetAccess, Reflection`
- `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs`: 758 lines, 59 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/Operation/OperatingDaySettlement.cs`: 662 lines, 59 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/Character/Ability/AbilityMove.cs`: 564 lines, 52 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Recruitment/RegularCustomerSystem.cs`: 521 lines, 49 declarations, `EventBus`
- `Assets/Scripts/Buildings/BuildableObject.cs`: 560 lines, 45 declarations, `DependencyInjection, Reflection, SceneMutation`
- `Assets/Scripts/Character/Work/WorkTaskExecutor.cs`: 490 lines, 28 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Defense/DefenseFacilitySystem.cs`: 481 lines, 35 declarations, `EventBus`
- `Assets/Scripts/Character/Core/CharacterVisual.cs`: 441 lines, 58 declarations, `RuntimeObjectCreation, SceneMutation`
- `Assets/Scripts/Research/BlueprintResearchSystem.cs`: 448 lines, 51 declarations, `DependencyInjection, EventBus`
- `Assets/Scripts/Buildings/SO/StockInfo.cs`: 402 lines, 34 declarations, `EventBus`
- `Assets/Scripts/Character/Ability/AbilityShopping.cs`: 332 lines, 27 declarations, `DependencyInjection, SceneMutation`
- `Assets/Scripts/Defense/Editor/P1DefenseFacilityAssetBuilder.cs`: 308 lines, 25 declarations, `EditorAssetAccess, Reflection`
- `Assets/Scripts/FacilityEvolution/Editor/P1FacilityEvolutionAssetBuilder.cs`: 708 lines, 107 declarations, `EditorAssetAccess`
- `Assets/Scripts/Character/AI/CharacterAiDecisionPipeline.cs`: 748 lines, 58 declarations, `DependencyInjection`

## Misplaced Runtime Editor API Candidates

- None

## Visual Render Responsibility Notes

- `Assets/Scripts/Buildings/SO/BuildingSO.cs`: `GridBuildingPlacement.IsStructuralWall` separates gameplay/category semantics from structural wall rendering. This prevents hallway-layer data from being treated as wall visuals merely because its category is `Wall`.
- `Assets/Scripts/Grid/Rendering/GridTexture.cs`: `DrawBuilding(BuildingSO, Vector2Int)` and `DeleteBuilding(BuildingSO, Vector2Int)` route only `IsStructuralWall` data to `wallTilemap`; `DrawWall(Grid)` keeps hallway visuals visible by default for PlayMode debugging, with the older hide-under-buildings synchronization retained as an opt-in path. `Door` and movement buildings share that opt-in visual hallway policy.
- `Assets/Scripts/Grid/UI/GridGhostObject.cs`: build previews cap renderer alpha through `maxPreviewAlpha` so placement ghosts do not hide existing rooms, corridors, or buildings during PlayMode debugging.
- PlayMode evidence on `CharacterAiTestScene` after a clean script-refresh/restart: grid `32x3`, grid Hallway layer had 81 occupied cells, visible `Hallway` tilemap had 41 filled cells, `Wall` tilemap had 102 filled cells, building tilemaps were `BuildingBack=5`, `BuildingBackWidth=8`, `BuildingFront=3`, `BuildingFrontWidth=1`, selected rest-room ghost alpha was `0.350`, 2D SceneView captures showed the corridor/room structure without the opaque overlap, and Unity console warnings/errors were 0.

## DependencyInjection

- `Assets/Scripts/Buildings/BuildingManagementSummaryQuery.cs`: 169 lines, 19 declarations, building/shop/warehouse summary service plus pure summary calculation helpers; static service-locator capture facade removed.
- `Assets/Scripts/Buildings/BuildableObject.cs`: 560 lines, 45 declarations, receives blueprint research work, world-info click selection, facility candidate cache dirty, and room facility policy services directly through VContainer.
- `Assets/Scripts/Buildings/Shop.cs`: 959 lines, 78 declarations, receives shop stock catalog, game data provider, floating-number feedback, and workforce replan services directly through VContainer; stock initialization, revenue settlement, checkout replan, sale item category lookup, and facility candidate cache dirty signaling no longer use static access facades.
- `Assets/Scripts/Buildings/SO/Conditions/ConditionNeedMoney.cs`: 39 lines, 3 declarations, reads GameData from explicit building-condition context instead of resolving global runtime state.
- `Assets/Scripts/Character/Ability/AbilityShopping.cs`: 332 lines, 27 declarations, receives shop stock catalog and floating-icon feedback services through VContainer for purchase feedback item lookup.
- `Assets/Scripts/Character/Ability/AbilityWork.cs`: 666 lines, 89 declarations, receives blueprint research work, staff discontent, floating-icon feedback, work-grid resolver, and facility candidate cache services for work target selection, execution, blocking, suppression resolution, restock pickup feedback, grid resolution, and cache dirty signaling.
- `Assets/Scripts/Character/AI/FacilityCandidateScorer.cs`: 499 lines, 20 declarations, takes explicit actor-brain cache and `FacilityScoringContext` inputs instead of resolving facility candidates, social reputation, or room role policy through static access facades.
- `Assets/Scripts/Character/AI/FacilityScoringContext.cs`: 86 lines, 10 declarations, explicit facility scoring dependency context backed by `ISocialReputationBiasService` and `IRoomFacilityPolicy`, with isolated editor-test reputation opt-out only when a room policy is passed deliberately.
- `Assets/Scripts/Buildings/UI/UIBuildingInfo.cs`: 129 lines, 10 declarations, receives building definition lookup and UI touch guard through VContainer.
- `Assets/Scripts/Character/UI/CharacterFeedbackBubble.cs`: 341 lines, 32 declarations, receives AI scheduling and feedback bubble view factory services through VContainer so visibility throttling and feedback-state rendering avoid direct static runtime helpers.
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleService.cs`: 35 lines, 5 declarations, owns `CharacterFeedbackBubble` component creation and immediate VContainer injection for actor feedback UI.
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleViewFactory.cs`: 70 lines, 8 declarations, owns pooled world-space feedback bubble text object creation, TMP Korean font application, and release/reset behavior.
- `Assets/Scripts/Character/UI/CharacterFloatingIcon.cs`: 78 lines, 8 declarations, injected feedback-number service plus icon feedback defaults; static service-locator facade removed.
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`: 584 lines, 33 declarations, receives staff workforce query and staff-priority UI factory through VContainer; row/cell/text/button object creation moved to `StaffWorkPriorityPanelUiFactory`.
- `Assets/Scripts/Character/Core/CharacterVisualRootFactory.cs`: 35 lines, 4 declarations, centralizes shared Visual child/SpriteRenderer creation behind `ICharacterVisualRootFactory` for owner and invasion runtime factories.
- `Assets/Scripts/Character/Core/OwnerCharacterFactory.cs`: 126 lines, 8 declarations, owns owner prefab/prefab-less creation, required component composition, spawn-position resolution, and component injection through VContainer; shared Visual child/SpriteRenderer composition is delegated to `ICharacterVisualRootFactory`.
- `Assets/Scripts/Character/Core/OwnerRunManager.cs`: 194 lines, 18 declarations, receives owner candidate catalog and owner character factory so owner selection/run lifecycle no longer owns spawned object composition.
- `Assets/Scripts/Character/Core/CharacterStats.cs`: 301 lines, 43 declarations, receives staff discontent, owner-run lifecycle, and meta-progression reader services through VContainer.
- `Assets/Scripts/Character/Input/OwnerCommandController.cs`: 148 lines, 11 declarations, receives staff discontent service and main camera provider through VContainer so suppression command target checks and right-click raycasts do not resolve static runtime/camera access.
- `Assets/Scripts/Character/Work/WorkCommandHandler.cs`: 228 lines, 10 declarations, uses staff discontent and work-grid resolver services supplied by AbilityWork for suppression resolution.
- `Assets/Scripts/Character/Work/WorkDutyController.cs`: 462 lines, 33 declarations, uses staff discontent and facility cache dirty signaling supplied by AbilityWork for work blocking and duty-state changes.
- `Assets/Scripts/Character/Work/WorkPriorityProfile.cs`: 373 lines, 20 declarations, WorkCommandResolver accepts a rebellion-target predicate instead of resolving staff discontent runtime itself.
- `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs`: 758 lines, 59 declarations, staff discontent runtime receives scene component query for daily processing and auto-suppress candidate scans.
- `Assets/Scripts/Character/UI/OwnerSelectionPanel.cs`: 140 lines, 10 declarations, receives owner-run manager provider, TMP Korean font service, and option-button factory through VContainer instead of reading OwnerRunManager.Instance, calling the font static facade directly, or owning option prefab creation/release.
- `Assets/Scripts/Character/UI/OwnerSelectionOptionButtonFactory.cs`: 70 lines, 7 declarations, owns owner option prefab instantiation, TMP label setup, click listener binding, and release/destruction behind an injected factory boundary.
- `Assets/Scripts/Character/Work/WorkTaskExecutor.cs`: 490 lines, 28 declarations, applies research work, restock pickup feedback, and facility cache dirty signaling through services supplied by AbilityWork.
- `Assets/Scripts/Character/Work/StaffWorkforceQueryService.cs`: 51 lines, 9 declarations, injected staff workforce lookup service with the static compatibility facade removed.
- `Assets/Scripts/Character/Work/WorkforceReplanService.cs`: 38 lines, 5 declarations, idle worker replan requests behind an injected scene-query service with the static compatibility facade removed.
- `Assets/Scripts/Character/Work/WorkGridUtility.cs`: 48 lines, 5 declarations, injected work grid resolver service; static compatibility facade removed.
- `Assets/Scripts/Character/AI/AiDirectorContextSceneQuery.cs`: 37 lines, 9 declarations, captures AI director actor/facility snapshots through injected scene query.
- `Assets/Scripts/Character/AI/AiDirectorRuntime.cs`: 527 lines, 44 declarations, receives local LLM, AI director context scene-query, AI scheduling, and facility lookup services through VContainer.
- `Assets/Scripts/Character/AI/CharacterAiDecisionPipeline.cs`: 748 lines, 58 declarations, behavior-task decision pipeline is registered as `ICharacterAiDecisionPipeline`; BT tasks call it through the actor brain's injected service while pure facility matching remains static.
- `Assets/Scripts/Character/AI/CharacterBehaviorTreeRuntimeConfigurator.cs`: 46 lines, 4 declarations, owns scheduled character `BehaviorTree` component creation and runtime external-behavior setup behind an injected service.
- `Assets/Scripts/Character/AI/CharacterAiScheduler.cs`: 561 lines, 63 declarations, receives scene component query, main camera provider, and behavior-tree runtime configurator for initial character registration/visibility checks, scheduled BT setup, and instance scheduling operations consumed through the injected scheduling service.
- `Assets/Scripts/Character/AI/CharacterDialogueBubbleFactory.cs`: 46 lines, 5 declarations, owns world-space dialogue bubble text object creation and TMP Korean font application for character dialogue runtime.
- `Assets/Scripts/Character/AI/CharacterDialogueRuntime.cs`: 283 lines, 18 declarations, receives local LLM runtime, AI scheduling, and dialogue bubble factory services for bubble-line requests gated by scheduler visibility policy.
- `Assets/Scripts/Character/AI/CharacterSocialMemoryService.cs`: 36 lines, 5 declarations, owns `CharacterSocialMemory` component creation, binding, and immediate VContainer injection for actor social memory.
- `Assets/Scripts/Character/AI/CustomerPersonaRuntime.cs`: 323 lines, 19 declarations, receives the local LLM runtime provider for persona requests.
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`: 1005 lines, 71 declarations, receives local LLM, scene component query, and character social memory services for rumor generation, actor registration, memory creation, and facility matching.
- `Assets/Scripts/Codex/CodexReferenceImporter.cs`: 43 lines, 4 declarations, imports codex reference data and visible synthesis recipe snapshots through injected catalog/query services.
- `Assets/Scripts/Codex/CodexSystem.cs`: 462 lines, 57 declarations, receives blueprint research state, codex reference importer, synthesis recipe query, and facility shop catalog services through VContainer; static runtime access compatibility was removed.
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordRuntime.cs`: 451 lines, 58 declarations, receives facility candidate cache through VContainer; facility-evolution record events no longer mark candidate cache dirty through the static `FacilityCandidateCache` facade.
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`: 783 lines, 48 declarations, receives recipe query, record-token consumer, blueprint research state, grid texture, room layout cache, facility evolution state service, facility candidate cache, object resolver, and local LLM runtime providers through VContainer; evolved buildings are injected after creation and cache invalidation no longer goes through `FacilityCandidateCache`/`RoomRegistry` static facades inside the engine.
- `Assets/Scripts/FacilityEvolution/RoomProfile.cs`: 394 lines, 31 declarations, `RoomProfileBuilder` receives evolution records and room layout cache through constructor DI instead of reading the `RoomRegistry` static facade.
- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`: 727 lines, 62 declarations, daily shop runtime receives facility shop catalog, run-variable reader, and meta-progression reader through VContainer; offer generation takes catalog/readers/delegates explicitly instead of using static catalog/run/meta access.
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`: 455 lines, 34 declarations, receives blueprint research state, grid texture, object resolver, and synthesis recipe query through VContainer, and injects synthesized buildings after creation.
- `Assets/Scripts/Codex/CodexRecordSummaryQuery.cs`: 68 lines, 8 declarations, codex record summary service and injected scene source boundary; static service-locator capture facade removed.
- `Assets/DataManager.cs`: 64 lines, 4 declarations, receives a DataScriptableObject source instead of loading Resources directly.
- `Assets/Scripts/Infrastructure/DataCatalogService.cs`: 51 lines, 7 declarations, DataManager catalog and building definition lookup boundary.
- `Assets/Scripts/Infrastructure/ResourcesAssetLoader.cs`: 53 lines, 7 declarations, single runtime `Resources.Load/LoadAll` asset access boundary registered through VContainer.
- `Assets/Scripts/Infrastructure/DataScriptableObjectSource.cs`: 25 lines, 5 declarations, injected DataScriptableObject source backed by the shared Resources asset loader.
- `Assets/Scripts/Infrastructure/GameRuntimeServices.cs`: 65 lines, 11 declarations, injected GameManager-backed game data and floating-number feedback services; static DI compatibility accessors were removed.
- `Assets/Scripts/Infrastructure/BlueprintResearchRuntimeProvider.cs`: 97 lines, 13 declarations, injected blueprint research runtime provider plus state/work services; work and state static access facades were removed.
- `Assets/Scripts/Infrastructure/FacilityShopRuntimeProvider.cs`: 93 lines, 14 declarations, injected daily facility shop runtime provider, facility shop catalog, and unlock-state service; facility shop catalog static access was removed.
- `Assets/Scripts/Infrastructure/ShopStockCatalogService.cs`: 41 lines, 9 declarations, injected shop stock and sale item catalog service backed by IDataCatalog; static catalog access facade was removed.
- `Assets/Scripts/Infrastructure/StaffDiscontentRuntimeProvider.cs`: 74 lines, 11 declarations, injected staff discontent runtime provider and service; the unused static compatibility facade was removed.
- `Assets/Scripts/Infrastructure/LocalLlmRuntimeProvider.cs`: 35 lines, 5 declarations, injected local LLM queue lookup boundary.
- `Assets/Scripts/Infrastructure/SocialReputationRuntimeProvider.cs`: 54 lines, 8 declarations, injected social reputation runtime provider and facility bias service; the static social reputation bias facade was removed.
- `Assets/Scripts/Infrastructure/DungeonRuntimeLifetimeScope.cs`: 516 lines, 6 declarations, explicit-scene VContainer composition root and scene component injection bridge, including the shared Resources asset loader, OwnerCommandController, BuildableObject, StaffWorkPriorityPanel, NoticeFeed, notice feed presenter, main camera provider, daily facility shop runtime, offense reward, codex importer, facility-evolution record runtime injection, character AI decision pipeline/job giver catalog, room facility policy service registration, staff work priority UI factory registration, and synthesis/evolution recipe query registrations; no longer configures facility/room cache, facility-evolution state, or TMP Korean font static facades.
- `Assets/Scripts/Infrastructure/TmpKoreanFontProvider.cs`: 29 lines, 3 declarations, injected TMP Korean font provider boundary backed by the shared Resources asset loader.
- `Assets/Scripts/Infrastructure/CodexReferenceCatalogServices.cs`: 34 lines, 7 declarations, injected codex species/facility reference catalog backed by IDataCatalog; static access facade was removed.
- `Assets/Scripts/Infrastructure/CharacterAiActionAssetCatalog.cs`: 40 lines, 7 declarations, injected AI action asset catalog and required-action validation boundary backed by the shared Resources asset loader.
- `Assets/Scripts/Infrastructure/CharacterAiSchedulingService.cs`: 79 lines, 21 declarations, injected AI scheduler service boundary for registration, immediate replans, path-search budget, feedback visibility, movement frame stride, and PlayMode-exit-safe unregister.
- `Assets/Scripts/Invasion/InvasionIntruderDataResolver.cs`: 4 lines, 1 declaration, injected invasion intruder data provider contract.
- `Assets/Scripts/Infrastructure/InvasionIntruderDataProvider.cs`: 24 lines, 3 declarations, injected default intruder CharacterSO provider boundary backed by the shared Resources asset loader.
- `Assets/Scripts/Infrastructure/FacilityRecipeCatalogServices.cs`: 195 lines, 25 declarations, injected synthesis/evolution recipe catalogs plus recipe query services and evolution record-token definition provider backed by IDataCatalog/meta-progression reader; evolution source candidate queries receive the facility-evolution state service instead of reading the state static facade.
- `Assets/Scripts/Infrastructure/DungeonBackdropReferenceProvider.cs`: 65 lines, 5 declarations, injected backdrop scene-reference provider for background and ground tilemap lookup.
- `Assets/Scripts/Infrastructure/DungeonGridBuildingRuntimeProviders.cs`: 108 lines, 14 declarations, injected grid building controller, grid texture, main camera, and camera-backed world pointer provider boundaries.
- `Assets/Scripts/Infrastructure/DungeonSceneComponentQuery.cs`: 51 lines, 5 declarations, shared loaded-scene component lookup boundary for injected services.
- `Assets/Scripts/Infrastructure/DungeonSceneRuntimeReferences.cs`: 24 lines, 2 declarations, immutable scene runtime reference bundle registered by the scope.
- `Assets/Scripts/Infrastructure/GridSystemProvider.cs`: 72 lines, 5 declarations, injected GridSystemManager/Grid access boundary with required and optional accessors.
- `Assets/Scripts/Infrastructure/CharacterSpawnerProvider.cs`: 25 lines, 4 declarations, injected character spawner lookup boundary for dynamically spawned character abilities.
- `Assets/Scripts/Infrastructure/InvasionThreatRuntimeProvider.cs`: 23 lines, 4 declarations, injected invasion threat runtime lookup boundary.
- `Assets/Scripts/Invasion/InvasionThreatWorldSampler.cs`: 135 lines, 7 declarations, injected invasion threat world factor sampler.
- `Assets/Scripts/Infrastructure/RegularCustomerRuntimeProvider.cs`: 25 lines, 4 declarations, injected regular customer runtime lookup boundary.
- `Assets/Scripts/Infrastructure/MetaProgressionRuntimeProvider.cs`: 86 lines, 13 declarations, injected meta progression runtime lookup and reader boundary; the static access facade was removed.
- `Assets/Scripts/Infrastructure/RunVariableRuntimeProvider.cs`: 171 lines, 37 declarations, injected run-variable runtime lookup/reader, owner run data, and owner lifecycle service boundaries; RunVariableRuntimeAccess and OwnerRunLifecycleAccess static facades were removed.
- `Assets/Scripts/Infrastructure/RunVariableCatalogServices.cs`: 91 lines, 19 declarations, injected run character catalog, owner candidate catalog, and run-start variable catalog boundaries; character catalog asset loading goes through the shared Resources asset loader.
- `Assets/Scripts/Infrastructure/RuntimePanelProviders.cs`: 79 lines, 9 declarations, injected runtime providers for evolution, synthesis, and codex panels; static runtime access facades were removed.
- `Assets/Scripts/Invasion/InvasionDefenseSummaryQuery.cs`: 111 lines, 9 declarations, invasion defense summary service and injected scene source boundary; static service-locator capture facade removed.
- `Assets/Scripts/Invasion/InvasionIntruderContext.cs`: 82 lines, 9 declarations, injected invasion intruder grid, owner, entry, and run-variable context.
- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`: 400 lines, 27 declarations, receives invasion intruder context, data provider, and intruder factory through VContainer; intruder object creation and component composition are delegated to the injected factory.
- `Assets/Scripts/Invasion/InvasionThreatRuntime.cs`: 281 lines, 21 declarations, receives threat world sampler plus run/meta readers through VContainer.
- `Assets/Scripts/Character/CharacterSpawner.cs`: 338 lines, 28 declarations, receives regular-customer, grid, run-variable reader, and character spawn object factory through VContainer; spawned prefab instantiation, component injection, and pool object destruction are delegated to the injected factory.
- `Assets/Scripts/Character/CharacterSpawnObjectFactory.cs`: 64 lines, 9 declarations, owns spawned customer prefab instantiation, child component injection, and pool object destruction behind `ICharacterSpawnObjectFactory`.
- `Assets/Scripts/Character/Ability/AbilityMove.cs`: 564 lines, 52 declarations, receives character spawner provider and AI scheduling service through VContainer.
- `Assets/Scripts/Character/Ability/CharacterAbility.cs`: 84 lines, 12 declarations, receives grid provider through VContainer.
- `Assets/Scripts/Character/AI/AIBrain.cs`: 1294 lines, 63 declarations, uses the injected grid provider, action asset catalog, AI scheduling service, facility candidate cache, facility lookup, job giver catalog, decision pipeline, social reputation bias service, and room facility policy for path searches, immediate replans, BT task decisions, AI destination planning, required action composition, cached facility lookup, macro target lookup, job giver selection, and facility scoring context creation.
- `Assets/Scripts/Character/Core/CharacterActor.cs`: 730 lines, 121 declarations, receives grid provider, AI scheduling, world-info click selection, social memory, and feedback bubble services through VContainer.
- `Assets/Scripts/Character/Core/CharacterLifecycle.cs`: 182 lines, 15 declarations, receives grid provider through VContainer.
- `Assets/Scripts/Grid/Building/GridBuildingObjectFactory.cs`: 48 lines, 4 declarations, owns runtime building GameObject/collider/Rigidbody/component creation behind `IGridBuildingObjectFactory`.
- `Assets/Scripts/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs`: 463 lines, 42 declarations, receives grid, data catalog, pointer position, grid texture, object resolver, game data provider, and grid building object factory through VContainer; placed buildings are injected immediately and build conditions receive explicit context.
- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`: 294 lines, 33 declarations, injected offense runtime providers, expedition member query, reward catalog, reward selector/grant service contracts, panel presentation service, generated offense panel factory, and panel button factory wiring.
- `Assets/Scripts/Offense/OffenseRewardSelector.cs`: 117 lines, 7 declarations, instance offense reward selector receives the reward catalog directly for facility and blueprint pools.
- `Assets/Scripts/Offense/OffenseRewardContextResolver.cs`: 78 lines, 7 declarations, offense reward context builder backed by injected scene query.
- `Assets/Scripts/Offense/OffenseWorldMapSystem.cs`: 279 lines, 21 declarations, receives offense panel service through VContainer and receives `IOffensePanelButtonFactory` through panel binding for generated target buttons; default panel creation moved to `IOffensePanelFactory`.
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`: 380 lines, 23 declarations, receives offense runtime providers, member query, meta provider, panel service, and panel button factory through VContainer/service binding; default panel shell creation stays in `IOffensePanelFactory`, dynamic buttons now go through `IOffensePanelButtonFactory`.
- `Assets/Scripts/Offense/OffenseRewardSystem.cs`: 294 lines, 22 declarations, receives offense reward context builder and reward grant service through VContainer.
- `Assets/Scripts/Offense/OffenseTabSummaryQuery.cs`: 80 lines, 8 declarations, offense tab summary service and injected scene source boundary; static service-locator capture facade removed.
- `Assets/Scripts/Operation/OperationTabSummaryQuery.cs`: 90 lines, 10 declarations, operation summary service and injected runtime source boundary; static service-locator capture facade removed.
- `Assets/Scripts/Research/ResearchCraftingSummaryQuery.cs`: 70 lines, 8 declarations, research/crafting summary service and injected scene source boundary; static service-locator capture facade removed.
- `Assets/Scripts/UI/BuildingSummaryInfo.cs`: 139 lines, 17 declarations, receives popup, building definition, and TMP Korean font services through VContainer method injection.
- `Assets/Scripts/Buildings/UI/UIBuildingSelectButton.cs`: 108 lines, 13 declarations, receives grid building controller provider through VContainer or dynamic initialization.
- `Assets/Scripts/UI/CameraManager.cs`: 184 lines, 18 declarations, receives grid provider and main camera provider through VContainer method injection.
- `Assets/Scripts/UI/CharacterSummeryInfo.cs`: 228 lines, 18 declarations, receives popup service and character-summary runtime log factory through VContainer method injection.
- `Assets/Scripts/UI/CharacterSummaryRuntimeLogFactory.cs`: 70 lines, 8 declarations, owns runtime character log text creation, TMP Korean font application, and text style setup behind injected factory.
- `Assets/Scripts/UI/DungeonSceneBackdropFitter.cs`: 255 lines, 20 declarations, receives grid, backdrop reference, and sprite tiling factory providers through VContainer method injection.
- `Assets/Scripts/UI/NoticeFeed.cs`: 59 lines, 10 declarations, receives notice feed presenter through VContainer so event handling no longer owns object pool construction, tween release, or item presentation.
- `Assets/Scripts/UI/NoticeFeedItemFactory.cs`: 94 lines, 14 declarations, owns notice prefab instantiation, TMP Korean font application, notice text/color setup, and pool item activation/destruction behind injected factory.
- `Assets/Scripts/UI/NoticeFeedPresenter.cs`: 80 lines, 7 declarations, owns notice feed object pool lookup/creation and DOTween release timing behind an injected presenter boundary.
- `Assets/Scripts/UI/TMPKoreanFont.cs`: 68 lines, 14 declarations, TMP font application now lives only in injected `ITmpKoreanFontService`; the runtime static helper definition was removed.
- `Assets/Scripts/UI/UITabContentTextProvider.cs`: 277 lines, 13 declarations, injected tab text provider.
- `Assets/Scripts/UI/UITab.cs`: 63 lines, 7 declarations, receives popup service through VContainer method injection.
- `Assets/Scripts/UI/UITabManager.cs`: 557 lines, 36 declarations, receives tab content, popup, TMP Korean font, generated-panel, staff-priority panel, and top-button factories through VContainer method injection; it orchestrates tab state while generated panel and top-button creation live behind factories.
- `Assets/Scripts/UI/UITabTopButtonFactory.cs`: 124 lines, 10 declarations, owns top-tab button prefab cloning, TMP label setup, row child normalization, and horizontal layout component creation behind an injected factory boundary.
- `Assets/Scripts/UI/UiPopupService.cs`: 71 lines, 13 declarations, UIManager popup and touch guard operations behind injected services.
- `Assets/Scripts/UI/WorldInfoClickSelector.cs`: 145 lines, 10 declarations, injected world-info click selector service with main camera provider; static compatibility facade and direct `Camera.main` access were removed.
- `Assets/Scripts/Grid/DungeonStory/UI/DungeonStoryGridGhostPresenter.cs`: 149 lines, 27 declarations, receives grid, grid-building controller, and world pointer providers through VContainer method injection.
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructButtonFactory.cs`: 114 lines, 10 declarations, owns grid construction category panel/button and building-select button prefab instantiation behind an injected factory.
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructTab.cs`: 230 lines, 20 declarations, receives data catalog, popup service, grid-building controller provider, TMP Korean font service, and grid construct button factory through VContainer method injection.
- `Assets/Scripts/Grid/DungeonStory/UI/GridUIManager.cs`: 118 lines, 15 declarations, receives grid and grid-building controller providers through VContainer method injection.
- `Assets/Scripts/UI/RuntimePanelFactories.cs`: 230 lines, 23 declarations, owns generated codex/synthesis/evolution panel shell creation behind injected factories and immediately injects generated panel components when an `IObjectResolver` is available.
- `Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs`: 281 lines, 18 declarations, receives runtime provider through VContainer or explicit dynamic binding; generated default panel creation moved to `IFacilityEvolutionPanelFactory`.
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`: 126 lines, 12 declarations, receives runtime provider through VContainer or explicit dynamic binding; generated default panel creation moved to `IFacilitySynthesisPanelFactory`.
- `Assets/Scripts/Codex/UI/CodexPanel.cs`: 120 lines, 12 declarations, receives runtime provider through VContainer or explicit dynamic binding; generated default panel creation moved to `ICodexPanelFactory`.
- `Assets/Scripts/Meta/MetaProgressionSystem.cs`: 247 lines, 31 declarations, receives run-result builder and panel service through VContainer; run progress collection is delegated to `MetaRunProgressTracker`.
- `Assets/Scripts/Meta/MetaProgressionRunResultServices.cs`: 184 lines, 13 declarations, run-result builder plus injected panel service/factory; generated result panel creation and TMP Korean font application no longer live on the MonoBehaviour panel type.
- `Assets/Scripts/Run/RunVariableSystem.cs`: 272 lines, 28 declarations, receives owner run data, invasion threat, and run-start selector services through VContainer.
- `Assets/Scripts/Run/RunStartVariableSelector.cs`: 102 lines, 5 declarations, injected run-start variable selector service using run-start catalog and meta progression reader.
- `Assets/Scripts/Operation/EventAlertSystem.cs`: 129 lines, 14 declarations, receives event alert view presenter factory through VContainer and delegates selected-record state to `EventAlertSelectionState`.
- `Assets/Scripts/Operation/EventAlertCanvasProvider.cs`: 39 lines, 4 declarations, injected event alert Canvas provider that owns runtime Canvas creation.
- `Assets/Scripts/Operation/EventAlertViewPresenter.cs`: 234 lines, 18 declarations, injected event alert UI presenter boundary and Canvas/font provider consumer; choice-button lifetime is delegated to `EventAlertChoicePresenter`.
- `Assets/Scripts/Operation/OperatingDaySettlement.cs`: 662 lines, 59 declarations, receives scene component query, facility shop catalog, and run-variable reader through VContainer for daily report snapshots and next-day shop preview generation.
- `Assets/Scripts/Research/BlueprintResearchSystem.cs`: 448 lines, 51 declarations, receives facility shop unlock state and facility shop catalog through VContainer; research-work checks live in the injected service provider and blueprint unlock building lookup no longer resolves static catalog access.

## GlobalObjectLookup

- `Assets/Scripts/Character/AI/Editor/CharacterAiBehaviorDesignerGraphBuilder.cs`: 717 lines, 58 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`: 3468 lines, 108 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs`: 239 lines, 14 declarations
- `Assets/Scripts/Character/AI/Editor/WorkPriorityDebugScenarios.cs`: 533 lines, 50 declarations
- `Assets/Scripts/Invasion/Editor/InvasionThreatDebugScenarios.cs`: 248 lines, 33 declarations
- `Assets/Scripts/Infrastructure/DungeonSceneComponentQuery.cs`: 51 lines, 5 declarations
- `Assets/Scripts/Meta/Editor/MetaProgressionDebugScenarios.cs`: 327 lines, 30 declarations
- `Assets/Scripts/Offense/Editor/OffenseExpeditionDebugScenarios.cs`: 337 lines, 30 declarations
- `Assets/Scripts/Offense/Editor/OffenseWorldMapDebugScenarios.cs`: 182 lines, 21 declarations
- `Assets/Scripts/Operation/Editor/EventAlertDebugScenarios.cs`: 167 lines, 22 declarations

## SingletonAccess

- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`: 411 lines, 43 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiBehaviorDesignerGraphBuilder.cs`: 717 lines, 58 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiBehaviorDesignerRuntimeDebugger.cs`: 98 lines, 8 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`: 3468 lines, 108 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs`: 239 lines, 14 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPriorityCornerCaseDebugScenarios.cs`: 703 lines, 64 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiStressDebugScenarios.cs`: 963 lines, 70 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs`: 244 lines, 26 declarations
- `Assets/Scripts/Character/AI/Editor/CustomerAiDebugScenarios.cs`: 912 lines, 78 declarations
- `Assets/Scripts/Character/AI/Editor/OwnerDebugScenarios.cs`: 207 lines, 21 declarations
- `Assets/Scripts/Character/AI/Editor/PriorityCommandDebugScenarios.cs`: 286 lines, 32 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDiscontentDebugScenarios.cs`: 224 lines, 27 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDutyDebugScenarios.cs`: 966 lines, 59 declarations
- `Assets/Scripts/Character/AI/Editor/StaffRebellionResponseDebugScenarios.cs`: 253 lines, 20 declarations
- `Assets/Scripts/Character/AI/Editor/WorkPriorityDebugScenarios.cs`: 533 lines, 50 declarations
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`: 451 lines, 47 declarations
- `Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs`: 310 lines, 31 declarations
- `Assets/Scripts/Research/Editor/BlueprintResearchDebugScenarios.cs`: 350 lines, 29 declarations
- `Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs`: 485 lines, 43 declarations

## ResourcesAccess

- `Assets/Scripts/Infrastructure/ResourcesAssetLoader.cs`: 53 lines, 7 declarations, the only runtime `Resources.Load/LoadAll` asset access boundary.
- `Assets/Scripts/Character/AI/Editor/AiDebugScenarioActionFactory.cs`: 43 lines, 4 declarations
- `Assets/Scripts/Character/AI/Editor/CustomerAiDebugScenarios.cs`: 912 lines, 78 declarations
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`: 1214 lines, 132 declarations
- `Assets/Scripts/Invasion/Editor/InvasionThreatDebugScenarios.cs`: 248 lines, 33 declarations
- `Assets/Scripts/Infrastructure/DungeonSceneComponentQuery.cs`: 51 lines, 5 declarations, loaded-scene component lookup uses `Resources.FindObjectsOfTypeAll`, not asset loading.
- `Assets/Scripts/Operation/Editor/EventAlertDebugScenarios.cs`: 167 lines, 22 declarations

## EditorAssetAccess

- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`: 411 lines, 43 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiBehaviorDesignerGraphBuilder.cs`: 717 lines, 58 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiBehaviorDesignerRuntimeDebugger.cs`: 98 lines, 8 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`: 3468 lines, 108 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanTestScenePreparer.cs`: 171 lines, 8 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPriorityCornerCaseDebugScenarios.cs`: 703 lines, 64 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiStressDebugScenarios.cs`: 963 lines, 70 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterFeedbackDebugScenarios.cs`: 131 lines, 11 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs`: 244 lines, 26 declarations
- `Assets/Scripts/Character/AI/Editor/CustomerAiDebugScenarios.cs`: 912 lines, 78 declarations
- `Assets/Scripts/Character/AI/Editor/OwnerDebugScenarios.cs`: 207 lines, 21 declarations
- `Assets/Scripts/Character/AI/Editor/PriorityCommandDebugScenarios.cs`: 286 lines, 32 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDiscontentDebugScenarios.cs`: 224 lines, 27 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDutyDebugScenarios.cs`: 966 lines, 59 declarations
- `Assets/Scripts/Character/AI/Editor/StaffRebellionResponseDebugScenarios.cs`: 253 lines, 20 declarations
- `Assets/Scripts/Character/AI/Editor/WorkPriorityDebugScenarios.cs`: 533 lines, 50 declarations
- `Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs`: 341 lines, 27 declarations
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`: 451 lines, 47 declarations
- `Assets/Scripts/Defense/Editor/P1DefenseFacilityAssetBuilder.cs`: 308 lines, 25 declarations
- `Assets/Scripts/Editor/ImplementedScenarioDebugRunner.cs`: 325 lines, 53 declarations
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`: 1214 lines, 132 declarations
- `Assets/Scripts/FacilityEvolution/Editor/P1FacilityEvolutionAssetBuilder.cs`: 708 lines, 107 declarations
- `Assets/Scripts/FacilityShop/Editor/FacilityShopDebugScenarios.cs`: 290 lines, 30 declarations
- `Assets/Scripts/FacilityShop/Editor/P1FacilityShopAssetBuilder.cs`: 190 lines, 8 declarations
- `Assets/Scripts/Grid/System/Editor/GridFoundationDebugScenarios.cs`: 233 lines, 39 declarations
- `Assets/Scripts/Grid/System/Editor/GridSystemManagerEditor.cs`: 11 lines, 3 declarations
- `Assets/Scripts/Invasion/Editor/InvasionCombatReportDebugScenarios.cs`: 319 lines, 31 declarations
- `Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs`: 310 lines, 31 declarations
- `Assets/Scripts/Invasion/Editor/InvasionThreatDebugScenarios.cs`: 248 lines, 33 declarations
- `Assets/Scripts/Meta/Editor/MetaProgressionDebugScenarios.cs`: 327 lines, 30 declarations
- `Assets/Scripts/Offense/Editor/OffenseExpeditionDebugScenarios.cs`: 337 lines, 30 declarations
- `Assets/Scripts/Offense/Editor/OffenseRewardDebugScenarios.cs`: 347 lines, 43 declarations
- `Assets/Scripts/Offense/Editor/OffenseWorldMapDebugScenarios.cs`: 182 lines, 21 declarations
- `Assets/Scripts/Operation/Editor/EventAlertDebugScenarios.cs`: 167 lines, 22 declarations
- `Assets/Scripts/Operation/Editor/OperatingDaySettlementDebugScenarios.cs`: 195 lines, 13 declarations
- `Assets/Scripts/Recruitment/Editor/RegularCustomerDebugScenarios.cs`: 300 lines, 34 declarations
- `Assets/Scripts/Research/Editor/BlueprintResearchDebugScenarios.cs`: 350 lines, 29 declarations
- `Assets/Scripts/Rooms/Editor/RoomSystemDebugScenarios.cs`: 317 lines, 31 declarations
- `Assets/Scripts/Run/Editor/RunVariableDebugScenarios.cs`: 284 lines, 26 declarations
- `Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs`: 485 lines, 43 declarations
- `Assets/Scripts/Synthesis/Editor/P1FacilitySynthesisAssetBuilder.cs`: 752 lines, 46 declarations
- `Assets/Scripts/UI/Editor/TMPKoreanFontEditorResolver.cs`: 13 lines, 2 declarations

## Reflection

- `Assets/Scripts/Buildings/BuildableObject.cs`: 560 lines, 45 declarations
- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`: 411 lines, 43 declarations
- `Assets/Scripts/Character/AI/AiDirectorRuntime.cs`: 527 lines, 44 declarations
- `Assets/Scripts/Character/AI/CharacterDialogueRuntime.cs`: 283 lines, 18 declarations
- `Assets/Scripts/Character/AI/CharacterSocialMemory.cs`: 452 lines, 47 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`: 3468 lines, 108 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs`: 239 lines, 14 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPriorityCornerCaseDebugScenarios.cs`: 703 lines, 64 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiStressDebugScenarios.cs`: 963 lines, 70 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs`: 244 lines, 26 declarations
- `Assets/Scripts/Character/AI/Editor/CustomerAiDebugScenarios.cs`: 912 lines, 78 declarations
- `Assets/Scripts/Character/AI/Editor/OwnerDebugScenarios.cs`: 207 lines, 21 declarations
- `Assets/Scripts/Character/AI/Editor/PriorityCommandDebugScenarios.cs`: 286 lines, 32 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDiscontentDebugScenarios.cs`: 224 lines, 27 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDutyDebugScenarios.cs`: 966 lines, 59 declarations
- `Assets/Scripts/Character/AI/Editor/StaffRebellionResponseDebugScenarios.cs`: 253 lines, 20 declarations
- `Assets/Scripts/Character/AI/Editor/WorkPriorityDebugScenarios.cs`: 533 lines, 50 declarations
- `Assets/Scripts/Character/AI/Editor/FacilityCandidateCacheEditorFacade.cs`: 21 lines, 4 declarations, Editor scenario compatibility wrapper around `FacilityCandidateCacheStore`; no runtime static facade remains.
- `Assets/Scripts/Character/AI/FacilityCandidateCache.cs`: 125 lines, 14 declarations, candidate cache state lives in injected `IFacilityCandidateCache`; runtime static facade definition was removed.
- `Assets/Scripts/Rooms/Editor/RoomEditorFacades.cs`: 40 lines, 8 declarations, Editor scenario compatibility wrappers for room layout and facility policy services.
- `Assets/Scripts/Rooms/RoomRegistry.cs`: 59 lines, 9 declarations, room layout cache state lives in injected `IRoomLayoutCache`; runtime static facade definition was removed.
- `Assets/Scripts/Rooms/RoomFacilityPolicy.cs`: 81 lines, 7 declarations, room-role availability and room utility scoring live in injected `IRoomFacilityPolicy`; runtime static facade definition was removed.
- `Assets/Scripts/Character/AI/LlmJsonResponseParser.cs`: 654 lines, 31 declarations
- `Assets/Scripts/Character/AI/LocalLlmRequestQueue.cs`: 588 lines, 44 declarations
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`: 1001 lines, 71 declarations
- `Assets/Scripts/Character/Core/CharacterAbilityCache.cs`: 68 lines, 9 declarations
- `Assets/Scripts/Character/Core/CharacterActor.cs`: 730 lines, 121 declarations
- `Assets/Scripts/Character/Core/CharacterLog.cs`: 176 lines, 15 declarations
- `Assets/Scripts/Character/Core/Customer.cs`: 15 lines, 1 declarations
- `Assets/Scripts/Character/Core/OwnerRunManager.cs`: 194 lines, 18 declarations
- `Assets/Scripts/Character/Core/Shopkeeper.cs`: 15 lines, 1 declarations
- `Assets/Scripts/Character/UI/CharacterFeedbackBubble.cs`: 341 lines, 32 declarations
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`: 584 lines, 33 declarations
- `Assets/Scripts/Codex/CodexTextFormatter.cs`: 158 lines, 11 declarations
- `Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs`: 341 lines, 27 declarations
- `Assets/Scripts/Codex/UI/CodexPanel.cs`: 120 lines, 12 declarations
- `Assets/Scripts/Data.cs`: 50 lines, 5 declarations
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`: 451 lines, 47 declarations
- `Assets/Scripts/Defense/Editor/P1DefenseFacilityAssetBuilder.cs`: 308 lines, 25 declarations
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`: 1214 lines, 132 declarations
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionLlmProposalProvider.cs`: 520 lines, 34 declarations
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`: 783 lines, 48 declarations
- `Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs`: 281 lines, 18 declarations
- `Assets/Scripts/FacilityShop/Editor/FacilityShopDebugScenarios.cs`: 290 lines, 30 declarations
- `Assets/Scripts/Grid/Building/GridBuildingRuntime.cs`: 531 lines, 55 declarations
- `Assets/Scripts/Grid/System/Editor/GridSystemManagerEditor.cs`: 11 lines, 3 declarations
- `Assets/Scripts/Invasion/Editor/InvasionCombatReportDebugScenarios.cs`: 319 lines, 31 declarations
- `Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs`: 310 lines, 31 declarations
- `Assets/Scripts/Research/Editor/BlueprintResearchDebugScenarios.cs`: 350 lines, 29 declarations
- `Assets/Scripts/Rooms/Editor/RoomSystemDebugScenarios.cs`: 317 lines, 31 declarations
- `Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs`: 485 lines, 43 declarations
- `Assets/Scripts/Synthesis/Editor/P1FacilitySynthesisAssetBuilder.cs`: 752 lines, 46 declarations
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`: 126 lines, 12 declarations
- `Assets/Scripts/UI/CharacterSummeryInfo.cs`: 228 lines, 18 declarations
- `Assets/Scripts/UI/UITabTopButtonFactory.cs`: 124 lines, 10 declarations, top-tab button prefab cloning and HorizontalLayoutGroup creation are owned by injected `IUITabTopButtonFactory`.
- `Assets/Scripts/Utils/EventObserver.cs`: 152 lines, 18 declarations

## EventBus

- `Assets/Scripts/Buildings/SO/StockInfo.cs`: 373 lines, 33 declarations
- `Assets/Scripts/Character/AI/Editor/PriorityCommandDebugScenarios.cs`: 286 lines, 32 declarations
- `Assets/Scripts/Character/Core/CharacterDeathEvent.cs`: 20 lines, 3 declarations
- `Assets/Scripts/Character/Core/OwnerRunManager.cs`: 194 lines, 18 declarations
- `Assets/Scripts/Character/Input/OwnerCommandController.cs`: 148 lines, 11 declarations
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`: 584 lines, 33 declarations
- `Assets/Scripts/Character/Work/StaffDiscontentSystem.cs`: 758 lines, 59 declarations
- `Assets/Scripts/Codex/CodexSystem.cs`: 462 lines, 57 declarations
- `Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs`: 341 lines, 27 declarations
- `Assets/Scripts/Codex/UI/CodexPanel.cs`: 120 lines, 12 declarations
- `Assets/Scripts/Defense/DefenseFacilitySystem.cs`: 481 lines, 35 declarations
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`: 451 lines, 47 declarations
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`: 1214 lines, 132 declarations
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordRuntime.cs`: 451 lines, 58 declarations
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`: 783 lines, 48 declarations
- `Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs`: 281 lines, 18 declarations
- `Assets/Scripts/FacilityShop/Editor/FacilityShopDebugScenarios.cs`: 290 lines, 30 declarations
- `Assets/Scripts/FacilityShop/FacilityShopSystem.cs`: 608 lines, 46 declarations
- `Assets/Scripts/Invasion/Editor/InvasionCombatReportDebugScenarios.cs`: 319 lines, 31 declarations
- `Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs`: 310 lines, 31 declarations
- `Assets/Scripts/Invasion/Editor/InvasionThreatDebugScenarios.cs`: 248 lines, 33 declarations
- `Assets/Scripts/Invasion/InvasionCombatReportRuntime.cs`: 119 lines, 11 declarations
- `Assets/Scripts/Invasion/InvasionCombatReportSystem.cs`: 435 lines, 34 declarations
- `Assets/Scripts/Invasion/InvasionIntruderEvents.cs`: 62 lines, 9 declarations
- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`: 400 lines, 27 declarations
- `Assets/Scripts/Invasion/InvasionThreatRuntime.cs`: 281 lines, 21 declarations
- `Assets/Scripts/Invasion/InvasionThreatSystem.cs`: 236 lines, 23 declarations
- `Assets/Scripts/Meta/Editor/MetaProgressionDebugScenarios.cs`: 327 lines, 30 declarations
- `Assets/Scripts/Meta/MetaProgressionSystem.cs`: 247 lines, 31 declarations
- `Assets/Scripts/Meta/MetaProgressionEvents.cs`: 17 lines, 3 declarations
- `Assets/Scripts/Offense/Editor/OffenseExpeditionDebugScenarios.cs`: 337 lines, 30 declarations
- `Assets/Scripts/Offense/Editor/OffenseWorldMapDebugScenarios.cs`: 182 lines, 21 declarations
- `Assets/Scripts/Offense/OffenseExpeditionEvents.cs`: 35 lines, 6 declarations
- `Assets/Scripts/Offense/OffenseRewardEvents.cs`: 27 lines, 3 declarations
- `Assets/Scripts/Offense/OffenseWorldMapEvents.cs`: 69 lines, 9 declarations
- `Assets/Scripts/Operation/Editor/EventAlertDebugScenarios.cs`: 167 lines, 22 declarations
- `Assets/Scripts/Operation/Editor/OperatingDaySettlementDebugScenarios.cs`: 195 lines, 13 declarations
- `Assets/Scripts/Operation/EventAlertEvents.cs`: 35 lines, 6 declarations
- `Assets/Scripts/Operation/EventAlertService.cs`: 29 lines, 5 declarations
- `Assets/Scripts/Operation/EventAlertSystem.cs`: 129 lines, 14 declarations
- `Assets/Scripts/Operation/OperatingDayReportAlertBridge.cs`: 36 lines, 4 declarations
- `Assets/Scripts/Operation/OperatingDaySettlement.cs`: 630 lines, 75 declarations
- `Assets/Scripts/Recruitment/Editor/RegularCustomerDebugScenarios.cs`: 300 lines, 34 declarations
- `Assets/Scripts/Recruitment/RegularCustomerSystem.cs`: 521 lines, 49 declarations
- `Assets/Scripts/Research/BlueprintResearchSystem.cs`: 430 lines, 40 declarations
- `Assets/Scripts/Research/Editor/BlueprintResearchDebugScenarios.cs`: 350 lines, 29 declarations
- `Assets/Scripts/Run/Editor/RunVariableDebugScenarios.cs`: 284 lines, 26 declarations
- `Assets/Scripts/Run/RunVariableEvents.cs`: 71 lines, 12 declarations
- `Assets/Scripts/Run/RunVariableSystem.cs`: 272 lines, 28 declarations
- `Assets/Scripts/Synthesis/FacilitySynthesisSystem.cs`: 455 lines, 34 declarations
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`: 126 lines, 12 declarations
- `Assets/Scripts/UI/BuildingSummaryInfo.cs`: 128 lines, 16 declarations
- `Assets/Scripts/UI/CharacterSummeryInfo.cs`: 228 lines, 18 declarations
- `Assets/Scripts/UI/NoticeFeed.cs`: 59 lines, 10 declarations
- `Assets/Scripts/Utils/EventObserver.cs`: 152 lines, 18 declarations

## RuntimeObjectCreation

- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`: 411 lines, 43 declarations
- `Assets/Scripts/Character/AI/CharacterAiScheduler.cs`: 561 lines, 63 declarations
- `Assets/Scripts/Character/AI/CharacterBehaviorTreeRuntimeConfigurator.cs`: 46 lines, 4 declarations, scheduled character `BehaviorTree` component creation is owned by injected `ICharacterBehaviorTreeRuntimeConfigurator`.
- `Assets/Scripts/Character/AI/CharacterDialogueBubbleFactory.cs`: 46 lines, 5 declarations, dialogue bubble text object creation is owned by injected `ICharacterDialogueBubbleFactory`.
- `Assets/Scripts/Character/AI/Editor/CharacterAiBehaviorDesignerGraphBuilder.cs`: 717 lines, 58 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`: 3468 lines, 108 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanTestScenePreparer.cs`: 171 lines, 8 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs`: 239 lines, 14 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPriorityCornerCaseDebugScenarios.cs`: 703 lines, 64 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiStressDebugScenarios.cs`: 963 lines, 70 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterFeedbackDebugScenarios.cs`: 131 lines, 11 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs`: 244 lines, 26 declarations
- `Assets/Scripts/Character/AI/Editor/CustomerAiDebugScenarios.cs`: 912 lines, 78 declarations
- `Assets/Scripts/Character/AI/Editor/OwnerDebugScenarios.cs`: 207 lines, 21 declarations
- `Assets/Scripts/Character/AI/Editor/PriorityCommandDebugScenarios.cs`: 286 lines, 32 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDiscontentDebugScenarios.cs`: 224 lines, 27 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDutyDebugScenarios.cs`: 966 lines, 59 declarations
- `Assets/Scripts/Character/AI/Editor/StaffRebellionResponseDebugScenarios.cs`: 253 lines, 20 declarations
- `Assets/Scripts/Character/AI/Editor/WorkPriorityDebugScenarios.cs`: 533 lines, 50 declarations
- `Assets/Scripts/Character/AI/LocalLlmRequestQueue.cs`: 588 lines, 44 declarations
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`: 1001 lines, 71 declarations
- `Assets/Scripts/Character/AI/CharacterSocialMemoryService.cs`: 36 lines, 5 declarations, social memory component creation is owned by injected `ICharacterSocialMemoryService`.
- `Assets/Scripts/Character/CharacterSpawnObjectFactory.cs`: 64 lines, 9 declarations, spawned customer prefab instantiation, child component injection, and pool object destruction are owned by injected `ICharacterSpawnObjectFactory`.
- `Assets/Scripts/Character/Core/CharacterActor.cs`: 730 lines, 121 declarations
- `Assets/Scripts/Character/Core/CharacterVisual.cs`: 441 lines, 58 declarations
- `Assets/Scripts/Character/Core/CharacterVisualRootFactory.cs`: 35 lines, 4 declarations, shared Visual child/SpriteRenderer composition is owned by injected `ICharacterVisualRootFactory`.
- `Assets/Scripts/Character/Core/OwnerCharacterFactory.cs`: 126 lines, 8 declarations, owner prefab/prefab-less creation and component composition are owned by injected `IOwnerCharacterFactory`; Visual root composition is delegated to `ICharacterVisualRootFactory`.
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleService.cs`: 35 lines, 5 declarations, feedback bubble component creation is owned by injected `ICharacterFeedbackBubbleService`.
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleViewFactory.cs`: 70 lines, 8 declarations, feedback bubble text view creation and pooling are owned by injected `ICharacterFeedbackBubbleViewFactory`.
- `Assets/Scripts/Character/UI/OwnerSelectionOptionButtonFactory.cs`: 70 lines, 7 declarations, owner option button instantiation and release are owned by injected `IOwnerSelectionOptionButtonFactory`.
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`: 584 lines, 33 declarations, row/cell/text/button construction is owned by injected `IStaffWorkPriorityPanelUiFactory`.
- `Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs`: 341 lines, 27 declarations
- `Assets/Scripts/Defense/DefenseStatusRuntimeService.cs`: 46 lines, 8 declarations, status component creation is owned by injected `IDefenseStatusRuntimeService`.
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`: 451 lines, 47 declarations
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`: 1214 lines, 132 declarations
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRecordRuntime.cs`: 451 lines, 58 declarations
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionRuntime.cs`: 783 lines, 48 declarations
- `Assets/Scripts/FacilityEvolution/FacilityEvolutionState.cs`: 247 lines, 14 declarations, state component creation is owned by injected `IFacilityEvolutionStateService`; runtime static utility definition was removed.
- `Assets/Scripts/FacilityShop/Editor/FacilityShopDebugScenarios.cs`: 290 lines, 30 declarations
- `Assets/Scripts/Grid/Building/GridBuildingObjectFactory.cs`: 48 lines, 4 declarations, runtime building object/collider/Rigidbody/component creation is owned by injected `IGridBuildingObjectFactory`.
- `Assets/Scripts/Grid/Building/GridBuildingRuntime.cs`: 531 lines, 55 declarations
- `Assets/Scripts/Grid/Rendering/GridTexture.cs`: 456 lines, 37 declarations
- `Assets/Scripts/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs`: 463 lines, 42 declarations
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructButtonFactory.cs`: 114 lines, 10 declarations, building-category and building-select prefab instantiation is owned by injected `IGridConstructButtonFactory`.
- `Assets/Scripts/Grid/System/Editor/GridFoundationDebugScenarios.cs`: 233 lines, 39 declarations
- `Assets/Scripts/Grid/UI/GridGhostObject.cs`: 228 lines, 30 declarations
- `Assets/Scripts/Grid/UI/GridPlacementGhostPresenter.cs`: 134 lines, 23 declarations
- `Assets/Scripts/Invasion/Editor/InvasionCombatReportDebugScenarios.cs`: 319 lines, 31 declarations
- `Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs`: 310 lines, 31 declarations
- `Assets/Scripts/Invasion/Editor/InvasionThreatDebugScenarios.cs`: 248 lines, 33 declarations
- `Assets/Scripts/Invasion/InvasionIntruderFactory.cs`: 67 lines, 7 declarations, prefab/prefab-less intruder object creation and component composition are owned by injected `IInvasionIntruderFactory`; Visual root composition is delegated to `ICharacterVisualRootFactory`.
- `Assets/Scripts/Meta/Editor/MetaProgressionDebugScenarios.cs`: 327 lines, 30 declarations
- `Assets/Scripts/Meta/MetaProgressionRunResultServices.cs`: 184 lines, 13 declarations, run-result panel creation is owned by injected `IRunResultPanelFactory`.
- `Assets/Scripts/UI/RuntimePanelFactories.cs`: 230 lines, 23 declarations, generated codex/synthesis/evolution panel creation is owned by injected panel factories.
- `Assets/Scripts/Offense/Editor/OffenseExpeditionDebugScenarios.cs`: 337 lines, 30 declarations
- `Assets/Scripts/Offense/Editor/OffenseRewardDebugScenarios.cs`: 347 lines, 43 declarations
- `Assets/Scripts/Offense/Editor/OffenseWorldMapDebugScenarios.cs`: 182 lines, 21 declarations
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`: 380 lines, 23 declarations, generated panel shell creation moved to injected `IOffensePanelFactory`; generated member/start buttons are owned by injected `IOffensePanelButtonFactory`.
- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`: 294 lines, 33 declarations, offense world-map/expedition panel creation is owned by injected `IOffensePanelFactory` and button lifetime by injected `IOffensePanelButtonFactory`.
- `Assets/Scripts/Offense/OffensePanelUiFactory.cs`: 161 lines, 8 declarations
- `Assets/Scripts/Offense/OffenseWorldMapSystem.cs`: 279 lines, 21 declarations, generated panel shell creation moved to injected `IOffensePanelFactory`; generated target buttons are owned by injected `IOffensePanelButtonFactory`.
- `Assets/Scripts/Operation/Editor/EventAlertDebugScenarios.cs`: 167 lines, 22 declarations
- `Assets/Scripts/Operation/Editor/OperatingDaySettlementDebugScenarios.cs`: 195 lines, 13 declarations
- `Assets/Scripts/Operation/EventAlertCanvasProvider.cs`: 39 lines, 4 declarations
- `Assets/Scripts/Operation/EventAlertChoicePresenter.cs`: 61 lines, 5 declarations
- `Assets/Scripts/Operation/EventAlertUiFactory.cs`: 293 lines, 11 declarations
- `Assets/Scripts/Operation/EventAlertViewPresenter.cs`: 234 lines, 18 declarations
- `Assets/Scripts/Recruitment/Editor/RegularCustomerDebugScenarios.cs`: 300 lines, 34 declarations
- `Assets/Scripts/Research/Editor/BlueprintResearchDebugScenarios.cs`: 350 lines, 29 declarations
- `Assets/Scripts/Rooms/Editor/RoomSystemDebugScenarios.cs`: 317 lines, 31 declarations
- `Assets/Scripts/Run/Editor/RunVariableDebugScenarios.cs`: 284 lines, 26 declarations
- `Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs`: 485 lines, 43 declarations
- `Assets/Scripts/UI/CharacterSummaryRuntimeLogFactory.cs`: 70 lines, 8 declarations, character summary runtime log text view creation and style setup are owned by injected `ICharacterSummaryRuntimeLogFactory`.
- `Assets/Scripts/UI/DungeonSceneBackdropFitter.cs`: 255 lines, 20 declarations
- `Assets/Scripts/UI/NoticeFeedItemFactory.cs`: 94 lines, 14 declarations, notice feed prefab instantiation and destruction are owned by injected `INoticeFeedItemFactory`.
- `Assets/Scripts/UI/NoticeFeedPresenter.cs`: 80 lines, 7 declarations, notice feed object pool creation and release sequencing are owned by injected `INoticeFeedPresenter`.
- `Assets/Scripts/UI/UITabManager.cs`: 557 lines, 36 declarations
- `Assets/Scripts/UI/UITabTopButtonFactory.cs`: 124 lines, 10 declarations

## SceneMutation

- `Assets/Scripts/Buildings/BuildableObject.cs`: 560 lines, 45 declarations
- `Assets/Scripts/Buildings/Editor/FacilityDebugScenarios.cs`: 411 lines, 43 declarations
- `Assets/Scripts/Buildings/Facility.cs`: 266 lines, 18 declarations
- `Assets/Scripts/Buildings/Shop.cs`: 959 lines, 78 declarations
- `Assets/Scripts/Buildings/Stair.cs`: 117 lines, 5 declarations
- `Assets/Scripts/Buildings/UI/UIBuildingInfo.cs`: 129 lines, 10 declarations
- `Assets/Scripts/Buildings/UI/UIBuildingSelectButton.cs`: 108 lines, 13 declarations
- `Assets/Scripts/Character/Ability/AbilityMove.cs`: 564 lines, 52 declarations
- `Assets/Scripts/Character/Ability/AbilityShopping.cs`: 332 lines, 27 declarations
- `Assets/Scripts/Character/AI/CharacterAiScheduler.cs`: 561 lines, 63 declarations
- `Assets/Scripts/Character/AI/CharacterBehaviorTreeRuntimeConfigurator.cs`: 46 lines, 4 declarations
- `Assets/Scripts/Character/AI/CharacterDialogueBubbleFactory.cs`: 46 lines, 5 declarations
- `Assets/Scripts/Character/AI/CharacterDialogueRuntime.cs`: 283 lines, 18 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugScenarios.cs`: 3468 lines, 108 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanTestScenePreparer.cs`: 171 lines, 8 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPlanDebugFixtures.cs`: 239 lines, 14 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiPriorityCornerCaseDebugScenarios.cs`: 703 lines, 64 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterAiStressDebugScenarios.cs`: 963 lines, 70 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterFeedbackDebugScenarios.cs`: 131 lines, 11 declarations
- `Assets/Scripts/Character/AI/Editor/CharacterModelDebugScenarios.cs`: 244 lines, 26 declarations
- `Assets/Scripts/Character/AI/Editor/CustomerAiDebugScenarios.cs`: 912 lines, 78 declarations
- `Assets/Scripts/Character/AI/Editor/OwnerDebugScenarios.cs`: 207 lines, 21 declarations
- `Assets/Scripts/Character/AI/Editor/PriorityCommandDebugScenarios.cs`: 286 lines, 32 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDiscontentDebugScenarios.cs`: 224 lines, 27 declarations
- `Assets/Scripts/Character/AI/Editor/StaffDutyDebugScenarios.cs`: 966 lines, 59 declarations
- `Assets/Scripts/Character/AI/Editor/StaffRebellionResponseDebugScenarios.cs`: 253 lines, 20 declarations
- `Assets/Scripts/Character/AI/Editor/WorkPriorityDebugScenarios.cs`: 533 lines, 50 declarations
- `Assets/Scripts/Character/AI/SocialReputationRuntime.cs`: 1001 lines, 71 declarations
- `Assets/Scripts/Character/AI/CharacterSocialMemoryService.cs`: 36 lines, 5 declarations
- `Assets/Scripts/Character/CharacterSpawner.cs`: 338 lines, 28 declarations, spawn timing, respawn state, entry-position resolution, and pool release policy remain here; spawned object creation/destruction moved to `ICharacterSpawnObjectFactory`.
- `Assets/Scripts/Character/CharacterSpawnObjectFactory.cs`: 64 lines, 9 declarations, spawned customer object creation/destruction mutates scene objects behind the injected factory boundary.
- `Assets/Scripts/Character/Core/CharacterLifecycle.cs`: 182 lines, 15 declarations
- `Assets/Scripts/Character/Core/CharacterVisual.cs`: 441 lines, 58 declarations
- `Assets/Scripts/Character/Core/CharacterVisualRootFactory.cs`: 35 lines, 4 declarations, shared Visual child/SpriteRenderer creation mutates scene objects behind the injected factory boundary.
- `Assets/Scripts/Character/Core/OwnerCharacterFactory.cs`: 126 lines, 8 declarations
- `Assets/Scripts/Character/Core/OwnerRunManager.cs`: 194 lines, 18 declarations
- `Assets/Scripts/Character/UI/CharacterFeedbackBubble.cs`: 341 lines, 32 declarations
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleService.cs`: 35 lines, 5 declarations
- `Assets/Scripts/Character/UI/CharacterFeedbackBubbleViewFactory.cs`: 70 lines, 8 declarations
- `Assets/Scripts/Character/UI/CharacterFloatingIcon.cs`: 78 lines, 8 declarations
- `Assets/Scripts/Character/UI/StaffWorkPriorityPanel.cs`: 584 lines, 33 declarations
- `Assets/Scripts/Character/Work/WorkGridUtility.cs`: 48 lines, 5 declarations
- `Assets/Scripts/Character/Work/WorkTaskExecutor.cs`: 490 lines, 28 declarations
- `Assets/Scripts/Codex/Editor/CodexDebugScenarios.cs`: 341 lines, 27 declarations
- `Assets/Scripts/Codex/UI/CodexPanel.cs`: 120 lines, 12 declarations
- `Assets/Scripts/Defense/Editor/DefenseFacilityDebugScenarios.cs`: 451 lines, 47 declarations
- `Assets/Scripts/FacilityEvolution/Editor/FacilityEvolutionDebugScenarios.cs`: 1214 lines, 132 declarations
- `Assets/Scripts/FacilityEvolution/UI/FacilityEvolutionPanel.cs`: 281 lines, 18 declarations
- `Assets/Scripts/FacilityShop/Editor/FacilityShopDebugScenarios.cs`: 290 lines, 30 declarations
- `Assets/Scripts/Grid/Building/GridBuildingObjectFactory.cs`: 48 lines, 4 declarations, runtime building object/collider/Rigidbody/component creation mutates scene objects behind the injected factory boundary.
- `Assets/Scripts/Grid/Building/GridBuildingRuntime.cs`: 531 lines, 55 declarations
- `Assets/Scripts/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs`: 463 lines, 42 declarations
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructButtonFactory.cs`: 114 lines, 10 declarations, dynamic construction UI objects are created and injected behind the factory boundary.
- `Assets/Scripts/Grid/DungeonStory/UI/GridConstructTab.cs`: 230 lines, 20 declarations
- `Assets/Scripts/Grid/DungeonStory/UI/GridUIManager.cs`: 118 lines, 15 declarations
- `Assets/Scripts/Grid/Rendering/GridTexture.cs`: 456 lines, 37 declarations
- `Assets/Scripts/Grid/System/Editor/GridFoundationDebugScenarios.cs`: 233 lines, 39 declarations
- `Assets/Scripts/Grid/System/GridSystemManager.cs`: 188 lines, 18 declarations
- `Assets/Scripts/Grid/UI/GridGhostObject.cs`: 228 lines, 30 declarations
- `Assets/Scripts/Grid/UI/GridPlacementGhostPresenter.cs`: 134 lines, 23 declarations
- `Assets/Scripts/Grid/UI/UIGridTab.cs`: 39 lines, 9 declarations
- `Assets/Scripts/Invasion/Editor/InvasionCombatReportDebugScenarios.cs`: 319 lines, 31 declarations
- `Assets/Scripts/Invasion/Editor/InvasionIntruderDebugScenarios.cs`: 310 lines, 31 declarations
- `Assets/Scripts/Invasion/Editor/InvasionThreatDebugScenarios.cs`: 248 lines, 33 declarations
- `Assets/Scripts/Invasion/InvasionIntruderFactory.cs`: 67 lines, 7 declarations, prefab/prefab-less intruder object creation and component composition are owned by injected `IInvasionIntruderFactory`.
- `Assets/Scripts/Invasion/InvasionIntruderSystem.cs`: 400 lines, 27 declarations
- `Assets/Scripts/Meta/Editor/MetaProgressionDebugScenarios.cs`: 327 lines, 30 declarations
- `Assets/Scripts/Meta/MetaProgressionRunResultServices.cs`: 184 lines, 13 declarations
- `Assets/Scripts/Meta/RunResultPanel.cs`: 29 lines, 4 declarations
- `Assets/Scripts/Offense/Editor/OffenseExpeditionDebugScenarios.cs`: 337 lines, 30 declarations
- `Assets/Scripts/Offense/Editor/OffenseRewardDebugScenarios.cs`: 347 lines, 43 declarations
- `Assets/Scripts/Offense/Editor/OffenseWorldMapDebugScenarios.cs`: 182 lines, 21 declarations
- `Assets/Scripts/Offense/OffenseExpeditionSystem.cs`: 380 lines, 23 declarations
- `Assets/Scripts/Offense/OffenseRuntimeServices.cs`: 294 lines, 33 declarations
- `Assets/Scripts/Offense/OffensePanelUiFactory.cs`: 161 lines, 8 declarations
- `Assets/Scripts/Offense/OffenseWorldMapSystem.cs`: 279 lines, 21 declarations
- `Assets/Scripts/Operation/Editor/EventAlertDebugScenarios.cs`: 167 lines, 22 declarations
- `Assets/Scripts/Operation/Editor/OperatingDaySettlementDebugScenarios.cs`: 195 lines, 13 declarations
- `Assets/Scripts/Operation/EventAlertCanvasProvider.cs`: 39 lines, 4 declarations
- `Assets/Scripts/Operation/EventAlertChoicePresenter.cs`: 61 lines, 5 declarations
- `Assets/Scripts/Operation/EventAlertUiFactory.cs`: 293 lines, 11 declarations
- `Assets/Scripts/Operation/EventAlertViewPresenter.cs`: 234 lines, 18 declarations
- `Assets/Scripts/Operation/EventAlertLayout.cs`: 144 lines, 8 declarations
- `Assets/Scripts/Recruitment/Editor/RegularCustomerDebugScenarios.cs`: 300 lines, 34 declarations
- `Assets/Scripts/Research/Editor/BlueprintResearchDebugScenarios.cs`: 350 lines, 29 declarations
- `Assets/Scripts/Rooms/Editor/RoomSystemDebugScenarios.cs`: 317 lines, 31 declarations
- `Assets/Scripts/Run/Editor/RunVariableDebugScenarios.cs`: 284 lines, 26 declarations
- `Assets/Scripts/Synthesis/Editor/FacilitySynthesisDebugScenarios.cs`: 485 lines, 43 declarations
- `Assets/Scripts/Synthesis/UI/FacilitySynthesisPanel.cs`: 126 lines, 12 declarations
- `Assets/Scripts/UI/BuildingSummaryInfo.cs`: 128 lines, 16 declarations
- `Assets/Scripts/UI/CameraManager.cs`: 184 lines, 18 declarations
- `Assets/Scripts/UI/CharacterSummeryInfo.cs`: 228 lines, 18 declarations
- `Assets/Scripts/UI/CharacterSummaryRuntimeLogFactory.cs`: 70 lines, 8 declarations, runtime character log text parent, rect, font, and visual style mutation are owned by injected `ICharacterSummaryRuntimeLogFactory`.
- `Assets/Scripts/UI/DungeonSceneBackdropFitter.cs`: 255 lines, 20 declarations
- `Assets/Scripts/UI/NoticeFeedItemFactory.cs`: 94 lines, 14 declarations, notice item parent, local scale, active state, text content, and color mutation are owned by injected `INoticeFeedItemFactory`.
- `Assets/Scripts/UI/NoticeFeedPresenter.cs`: 80 lines, 7 declarations, notice item presentation and DOTween release sequencing mutate UI state behind the injected presenter boundary.
- `Assets/Scripts/UI/RuntimePanelFactories.cs`: 230 lines, 23 declarations, generated codex/synthesis/evolution panel shell creation mutates scene hierarchy behind injected factories.
- `Assets/Scripts/UI/UITab.cs`: 63 lines, 7 declarations
- `Assets/Scripts/UI/UITabManager.cs`: 557 lines, 36 declarations
- `Assets/Scripts/UI/UITabTopButtonFactory.cs`: 124 lines, 10 declarations
- `Assets/Scripts/UI/WorldInfoClickSelector.cs`: 145 lines, 10 declarations







