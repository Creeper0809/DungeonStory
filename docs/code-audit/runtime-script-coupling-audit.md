# Runtime Script Coupling Audit

> Generated from current workspace. Excludes `Editor` directory scripts. Flags are heuristics for prioritizing manual review, not final judgments.

## Summary

- Generated: 2026-07-12 facility-evolution-record-event-recorder refresh
- Runtime script files: 296
- Extracted declarations: 3716
- Inventory: [runtime-scripts-function-inventory.md](runtime-scripts-function-inventory.md)

## Flag Counts

| Flag | Files |
| --- | ---: |
| `GlobalObjectLookup` | 0 |
| `SingletonAccess` | 0 |
| `ResourcesAccess` | 1 |
| `Reflection` | 4 |
| `EventBus` | 38 |
| `RuntimeObjectCreation` | 33 |
| `SceneMutation` | 79 |
| `DependencyInjection` | 79 |

## Runtime-Only Notes

- Runtime asset `Resources.Load/LoadAll` is intentionally centralized in `Assets/Scripts/Infrastructure/ResourcesAssetLoader.cs`.
- `Assets/Scripts/Infrastructure/DungeonSceneComponentQuery.cs` now traverses loaded scene roots via `SceneManager` and no longer uses `Resources.FindObjectsOfTypeAll`.
- Generated tab panel creation moved out of `UITabManager` into injected `UITabGeneratedPanelFactory`; top-button prefab cloning and single-row layout component creation moved into injected `UITabTopButtonFactory`.
- Grid construction button/panel prefab instantiation moved out of `GridConstructTab` into injected `GridConstructButtonFactory`; `GridConstructTab` now orchestrates category state and delegates dynamic UI creation.
- Hidden hallway creation is now constrained to passage overlays (`Door`/movement/hallway-layer occupants). Room-like initial buildings reserve their footprint so gameplay hallway occupants are not silently placed under visible facilities.
- Room-role facilities that intentionally behave as a complete prefab room use `FacilityData.selfContainedRoom`; `RoomDetector` turns those facilities into usable `RoomInstance`s instead of relying on a wall/door shell that does not exist in the current scene layout.
- Hallway visual synchronization keeps hallway tiles visible by default for PlayMode debugging; the previous hide-under-buildings pass remains opt-in via `hideHallwayUnderBuildings` and still uses the passage-overlay policy when enabled.
- `GridTexture` sprite tile transforms now include the target tilemap local y offset, so lowered building tilemaps still keep generated facility sprites inside the intended cell band instead of protruding below the floor.
- Staff work priority table object/component creation moved out of `StaffWorkPriorityPanel` into injected `StaffWorkPriorityPanelUiFactory`; the panel now owns event/state/table orchestration while the factory owns row/cell/text/button creation and release.
- Owner selection option prefab creation/release moved out of `OwnerSelectionPanel` into injected `OwnerSelectionOptionButtonFactory`; the panel now owns owner-list orchestration and selection-state updates only.
- Notice feed pool ownership, tween release, and pooled item presentation moved out of `NoticeFeed` into injected `NoticeFeedPresenter`; `NoticeFeed` now only listens for events and delegates display.
- `DungeonSceneBackdropFitter` already delegates sprite duplication to `IDungeonBackdropSpriteTilingFactory`; its previous runtime-creation flag was stale after the backdrop factory extraction.
- Scene/object creation flags are acceptable inside explicit factory/service boundaries, but remain review candidates when they appear on domain components or large UI surfaces.
- Event alert alert/choice button object creation now routes through injected `IEventAlertButtonFactory`; `EventAlertViewPresenter` keeps UI orchestration and delegates generated button lifetime to the factory boundary.
- Staff work priority worker discovery, display-name lookup, and change-detection hash moved out of `StaffWorkPriorityPanel` into injected `IStaffWorkPriorityPanelModelBuilder`.
- `GridPlacementGhostPresenter` now resolves its required `GridGhostObject` through injected `IGridGhostObjectResolver` or an attached component and never creates the component as a fallback.
- Runtime inventory coverage was rechecked after the grid ghost resolver update: 286 runtime script paths, 286 inventory headings, 0 missing.
- `FacilityEvolutionRecordComponentService` no longer attaches `FacilityEvolutionRecordComponent` directly. `FacilityEvolutionRecordComponentFactory` owns record component creation while the service remains the record access/replace policy boundary.
- `BuildingSummaryInfo` no longer reads `DataManager.Instance`/`UIManager.Instance` directly. It receives popup/font services plus `IBuildingSummaryFormatter`, while stock/name formatting lives in the flag-free `BuildingSummaryFormatter`.
- `DefenseStatusRuntimeService` no longer attaches `DefenseStatusRuntime` components directly. `DefenseStatusRuntimeFactory` is the explicit component-creation boundary and is registered through VContainer before the service.
- Runtime inventory coverage was rechecked after the defense status factory update: 288 runtime script paths, 288 inventory headings, 0 missing.
- `CharacterSocialMemoryService` and `CharacterFeedbackBubbleService` no longer attach or inject components directly. `CharacterSocialMemoryFactory` and `CharacterFeedbackBubbleFactory` own the runtime component creation/injection boundaries, and the services delegate to those factories.
- Runtime inventory coverage was rechecked after the character runtime component factory update: 290 runtime script paths, 290 inventory headings, 0 missing.
- `FacilityEvolutionStateService` no longer attaches `FacilityEvolutionStateComponent` directly. `FacilityEvolutionStateComponentFactory` owns state component creation/initialization, while the service remains the state access policy boundary.
- Runtime inventory coverage was rechecked after the facility evolution state factory update: 291 runtime script paths, 291 inventory headings, 0 missing.
- Runtime inventory coverage was rechecked after the facility evolution record factory update: 292 runtime script paths, 292 inventory headings, 0 missing.
- Run result panel Canvas/Text object creation moved out of `MetaProgressionRunResultServices.cs` into `RunResultPanelFactory.cs`; the former now only builds result snapshots and coordinates existing/factory panel presentation.
- Runtime inventory coverage was rechecked after the run result panel factory split: 293 runtime script paths, 293 inventory headings, 0 missing.
- Offense world-map/expedition panel Canvas creation moved out of `OffenseRuntimeServices.cs` into `OffensePanelFactory.cs`; the former now keeps provider/query/catalog/panel-service policy only.
- Runtime inventory coverage was rechecked after the offense panel factory split: 294 runtime script paths, 294 inventory headings, 0 missing.
- Facility evolution replacement GridBuildingFactory/VContainer injection assembly moved out of `FacilityEvolutionRuntime.cs` into `GridFacilityEvolutionBuildingReplacerFactory`; the runtime now depends on `IFacilityEvolutionBuildingReplacerFactory` instead of `IObjectResolver`, `IGridTextureProvider`, and `IGridBuildingObjectFactory`.
- Runtime inventory coverage was rechecked after the facility evolution replacer factory split: 295 runtime script paths, 295 inventory headings, 0 missing.
- Facility evolution event recording math moved out of `FacilityEvolutionRecordRuntime.cs` into `FacilityEvolutionRecordEventRecorder`; the MonoBehaviour now only subscribes/unsubscribes from EventBus and delegates typed events through an injected `IFacilityEvolutionRecordEventRecorder`.
- Runtime inventory coverage was rechecked after the facility evolution record event recorder split: 296 runtime script paths, 296 inventory headings, 0 missing.

## High-Risk Runtime Candidates

- `Assets\Scripts\Character\AI\SocialReputationRuntime.cs`: 1001 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\AI\LocalLlmRequestQueue.cs`: 588 lines, flags=[SceneMutation]
- `Assets\Scripts\Buildings\Shop.cs`: 959 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\Core\CharacterActor.cs`: 730 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\Ability\AbilityMove.cs`: 564 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\AI\CharacterAiScheduler.cs`: 561 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\Buildings\BuildableObject.cs`: 560 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\UITabManager.cs`: 557 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\DungeonSceneBackdropFitter.cs`: 255 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\RuntimePanelFactories.cs`: 230 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Meta\RunResultPanelFactory.cs`: 67 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Offense\OffensePanelFactory.cs`: 117 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\UITabGeneratedPanelFactory.cs`: 157 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\UITabTopButtonFactory.cs`: 124 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\UI\StaffWorkPriorityPanelUiFactory.cs`: 163 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Grid\DungeonStory\UI\GridConstructButtonFactory.cs`: 114 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Grid\DungeonStory\UI\GridConstructTab.cs`: 230 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\UI\OwnerSelectionPanel.cs`: 140 lines, flags=[SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\UI\OwnerSelectionOptionButtonFactory.cs`: 70 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\Core\OwnerCharacterFactory.cs`: 126 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\NoticeFeedItemFactory.cs`: 94 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\NoticeFeedPresenter.cs`: 80 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\UI\CharacterFeedbackBubbleViewFactory.cs`: 70 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\CharacterSummaryRuntimeLogFactory.cs`: 70 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\CharacterSpawnObjectFactory.cs`: 64 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\AI\CharacterDialogueBubbleFactory.cs`: 46 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\AI\CharacterSocialMemoryFactory.cs`: 36 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\UI\CharacterFeedbackBubbleFactory.cs`: 35 lines, flags=[RuntimeObjectCreation, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\AI\AIBrain.cs`: 1294 lines, flags=[Reflection, DependencyInjection]
- `Assets\Scripts\Character\AI\CharacterAiDecisionPipeline.cs`: 748 lines, flags=[Reflection]
- `Assets\Scripts\Character\AI\CharacterBlackboard.cs`: 748 lines, flags=[Reflection]
- `Assets\Scripts\Invasion\InvasionIntruderSystem.cs`: 425 lines, flags=[EventBus, SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\CharacterSummeryInfo.cs`: 228 lines, flags=[EventBus, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\Core\OwnerRunManager.cs`: 194 lines, flags=[EventBus, SceneMutation, DependencyInjection]
- `Assets\Scripts\UI\BuildingSummaryInfo.cs`: 143 lines, flags=[EventBus, SceneMutation, DependencyInjection]
- `Assets\Scripts\Operation\EventAlertSystem.cs`: 129 lines, flags=[EventBus, SceneMutation, DependencyInjection]
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRuntime.cs`: 764 lines, flags=[EventBus, SceneMutation, DependencyInjection]
- `Assets\Scripts\Character\UI\StaffWorkPriorityPanel.cs`: 528 lines, flags=[EventBus, SceneMutation, DependencyInjection]
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordEventRecorder.cs`: 421 lines, flags=[DependencyInjection]
- `Assets\Scripts\Character\UI\StaffWorkPriorityPanelModel.cs`: 94 lines, flags=[DependencyInjection]
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordRuntime.cs`: 98 lines, flags=[EventBus, DependencyInjection]
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordComponentFactory.cs`: 23 lines, flags=[RuntimeObjectCreation, SceneMutation]
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionBuildingReplacerFactory.cs`: 44 lines, flags=[RuntimeObjectCreation, DependencyInjection]
- `Assets\Scripts\Character\Work\StaffDiscontentSystem.cs`: 758 lines, flags=[EventBus, DependencyInjection]
- `Assets\Scripts\FacilityShop\FacilityShopSystem.cs`: 727 lines, flags=[EventBus, DependencyInjection]
- `Assets\Scripts\Operation\OperatingDaySettlement.cs`: 662 lines, flags=[EventBus, DependencyInjection]
- `Assets\Scripts\Recruitment\RegularCustomerSystem.cs`: 521 lines, flags=[EventBus]
- `Assets\Scripts\Character\Ability\AbilityWork.cs`: 666 lines, flags=[DependencyInjection]
- `Assets\Scripts\Character\AI\AiDirectorRuntime.cs`: 610 lines, flags=[DependencyInjection]
- `Assets\Scripts\Grid\Core\Grid.cs`: 955 lines, flags=[]
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionService.cs`: 731 lines, flags=[]
- `Assets\Scripts\Character\AI\CharacterAiBehaviorTasks.cs`: 708 lines, flags=[]
- `Assets\Scripts\Character\AI\LlmJsonResponseParser.cs`: 654 lines, flags=[]
- `Assets\Scripts\Character\AI\CharacterAiJobGiver.cs`: 538 lines, flags=[]
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionLlmProposalProvider.cs`: 520 lines, flags=[]

## GlobalObjectLookup

- None after replacing scene component global scans with loaded-scene root traversal.

## SingletonAccess

- None after removing runtime public `Instance` facades from `LocalLlmRequestQueue` and `SocialReputationRuntime`.
- Previous `RoomProfile` hit was a false positive from `RoomInstance`/private static helper naming, not singleton access.

## ResourcesAccess

- `Assets\Scripts\Infrastructure\ResourcesAssetLoader.cs`: 53 lines

## Reflection

- `Assets\Scripts\Character\AI\AIBrain.cs`: 1294 lines
- `Assets\Scripts\Character\AI\CharacterAiDecisionPipeline.cs`: 748 lines
- `Assets\Scripts\Character\AI\CharacterBlackboard.cs`: 748 lines
- `Assets\Scripts\Character\AI\Idle\IdleBehavior.cs`: 135 lines

## EventBus

- `Assets\Scripts\Buildings\SO\StockInfo.cs`: 402 lines
- `Assets\Scripts\Character\Core\CharacterDeathEvent.cs`: 20 lines
- `Assets\Scripts\Character\Core\OwnerRunManager.cs`: 194 lines
- `Assets\Scripts\Character\Input\OwnerCommandController.cs`: 148 lines
- `Assets\Scripts\Character\UI\StaffWorkPriorityPanel.cs`: 528 lines
- `Assets\Scripts\Character\Work\StaffDiscontentSystem.cs`: 758 lines
- `Assets\Scripts\Codex\CodexSystem.cs`: 462 lines
- `Assets\Scripts\Codex\UI\CodexPanel.cs`: 120 lines
- `Assets\Scripts\Defense\DefenseFacilitySystem.cs`: 481 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordRuntime.cs`: 98 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRuntime.cs`: 764 lines
- `Assets\Scripts\FacilityEvolution\UI\FacilityEvolutionPanel.cs`: 281 lines
- `Assets\Scripts\FacilityShop\FacilityShopSystem.cs`: 727 lines
- `Assets\Scripts\Invasion\InvasionCombatReportRuntime.cs`: 119 lines
- `Assets\Scripts\Invasion\InvasionCombatReportSystem.cs`: 435 lines
- `Assets\Scripts\Invasion\InvasionIntruderEvents.cs`: 62 lines
- `Assets\Scripts\Invasion\InvasionIntruderSystem.cs`: 425 lines
- `Assets\Scripts\Invasion\InvasionThreatRuntime.cs`: 281 lines
- `Assets\Scripts\Invasion\InvasionThreatSystem.cs`: 236 lines
- `Assets\Scripts\Meta\MetaProgressionEvents.cs`: 17 lines
- `Assets\Scripts\Meta\MetaProgressionSystem.cs`: 247 lines
- `Assets\Scripts\Offense\OffenseExpeditionEvents.cs`: 35 lines
- `Assets\Scripts\Offense\OffenseRewardEvents.cs`: 27 lines
- `Assets\Scripts\Offense\OffenseWorldMapEvents.cs`: 69 lines
- `Assets\Scripts\Operation\EventAlertEvents.cs`: 35 lines
- `Assets\Scripts\Operation\EventAlertSystem.cs`: 129 lines
- `Assets\Scripts\Operation\OperatingDayReportAlertBridge.cs`: 36 lines
- `Assets\Scripts\Operation\OperatingDaySettlement.cs`: 662 lines
- `Assets\Scripts\Recruitment\RegularCustomerSystem.cs`: 521 lines
- `Assets\Scripts\Research\BlueprintResearchSystem.cs`: 448 lines
- `Assets\Scripts\Run\RunVariableEvents.cs`: 71 lines
- `Assets\Scripts\Run\RunVariableSystem.cs`: 272 lines
- `Assets\Scripts\Synthesis\FacilitySynthesisSystem.cs`: 466 lines
- `Assets\Scripts\Synthesis\UI\FacilitySynthesisPanel.cs`: 126 lines
- `Assets\Scripts\UI\BuildingSummaryInfo.cs`: 143 lines
- `Assets\Scripts\UI\CharacterSummeryInfo.cs`: 228 lines
- `Assets\Scripts\UI\NoticeFeed.cs`: 59 lines
- `Assets\Scripts\Utils\EventObserver.cs`: 152 lines

## RuntimeObjectCreation

- `Assets\Scripts\Character\AI\CharacterBehaviorTreeRuntimeConfigurator.cs`: 46 lines
- `Assets\Scripts\Character\AI\CharacterDialogueBubbleFactory.cs`: 46 lines
- `Assets\Scripts\Character\AI\CharacterSocialMemoryFactory.cs`: 36 lines
- `Assets\Scripts\Character\CharacterSpawnObjectFactory.cs`: 64 lines
- `Assets\Scripts\Character\Core\CharacterVisual.cs`: 441 lines
- `Assets\Scripts\Character\Core\CharacterVisualRootFactory.cs`: 35 lines
- `Assets\Scripts\Character\Core\OwnerCharacterFactory.cs`: 126 lines
- `Assets\Scripts\Character\UI\CharacterFeedbackBubbleFactory.cs`: 35 lines
- `Assets\Scripts\Character\UI\CharacterFeedbackBubbleViewFactory.cs`: 70 lines
- `Assets\Scripts\Character\UI\OwnerSelectionOptionButtonFactory.cs`: 70 lines
- `Assets\Scripts\Character\UI\StaffWorkPriorityPanel.cs`: 528 lines
- `Assets\Scripts\Defense\DefenseStatusRuntimeFactory.cs`: 32 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordComponentFactory.cs`: 23 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionBuildingReplacerFactory.cs`: 44 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionStateComponentFactory.cs`: 24 lines
- `Assets\Scripts\Grid\Building\GridBuildingObjectFactory.cs`: 48 lines
- `Assets\Scripts\Grid\DungeonStory\UI\GridConstructButtonFactory.cs`: 114 lines
- `Assets\Scripts\Grid\Rendering\GridTexture.cs`: 491 lines
- `Assets\Scripts\Grid\UI\GridGhostObject.cs`: 262 lines
- `Assets\Scripts\Grid\UI\GridPlacementGhostPresenter.cs`: 134 lines
- `Assets\Scripts\Invasion\InvasionIntruderFactory.cs`: 67 lines
- `Assets\Scripts\Meta\RunResultPanelFactory.cs`: 67 lines
- `Assets\Scripts\Offense\OffensePanelUiFactory.cs`: 161 lines
- `Assets\Scripts\Offense\OffensePanelFactory.cs`: 117 lines
- `Assets\Scripts\Operation\EventAlertCanvasProvider.cs`: 39 lines
- `Assets\Scripts\Operation\EventAlertUiFactory.cs`: 361 lines
- `Assets\Scripts\UI\DungeonBackdropSpriteTilingFactory.cs`: 38 lines
- `Assets\Scripts\UI\CharacterSummaryRuntimeLogFactory.cs`: 70 lines
- `Assets\Scripts\UI\DungeonSceneBackdropFitter.cs`: 258 lines
- `Assets\Scripts\UI\NoticeFeedItemFactory.cs`: 94 lines
- `Assets\Scripts\UI\NoticeFeedPresenter.cs`: 80 lines
- `Assets\Scripts\UI\RuntimePanelFactories.cs`: 230 lines
- `Assets\Scripts\UI\UITabGeneratedPanelFactory.cs`: 157 lines
- `Assets\Scripts\UI\UITabTopButtonFactory.cs`: 124 lines

## SceneMutation

- `Assets\Scripts\Buildings\BuildableObject.cs`: 560 lines
- `Assets\Scripts\Buildings\Facility.cs`: 266 lines
- `Assets\Scripts\Buildings\Shop.cs`: 959 lines
- `Assets\Scripts\Buildings\Stair.cs`: 117 lines
- `Assets\Scripts\Buildings\UI\UIBuildingInfo.cs`: 129 lines
- `Assets\Scripts\Buildings\UI\UIBuildingSelectButton.cs`: 108 lines
- `Assets\Scripts\Character\Ability\AbilityMove.cs`: 564 lines
- `Assets\Scripts\Character\Ability\AbilityShopping.cs`: 380 lines
- `Assets\Scripts\Character\AI\CharacterAiScheduler.cs`: 561 lines
- `Assets\Scripts\Character\AI\CharacterBehaviorTreeRuntimeConfigurator.cs`: 46 lines
- `Assets\Scripts\Character\AI\CharacterDialogueBubbleFactory.cs`: 46 lines
- `Assets\Scripts\Character\AI\CharacterDialogueRuntime.cs`: 283 lines
- `Assets\Scripts\Character\AI\CharacterSocialMemoryFactory.cs`: 36 lines
- `Assets\Scripts\Character\AI\LocalLlmRequestQueue.cs`: 588 lines
- `Assets\Scripts\Character\AI\SocialReputationRuntime.cs`: 1001 lines
- `Assets\Scripts\Character\CharacterSpawner.cs`: 338 lines
- `Assets\Scripts\Character\CharacterSpawnObjectFactory.cs`: 64 lines
- `Assets\Scripts\Character\Core\CharacterAbilityCache.cs`: 68 lines
- `Assets\Scripts\Character\Core\CharacterActor.cs`: 730 lines
- `Assets\Scripts\Character\Core\CharacterLifecycle.cs`: 182 lines
- `Assets\Scripts\Character\Core\CharacterVisual.cs`: 441 lines
- `Assets\Scripts\Character\Core\CharacterVisualRootFactory.cs`: 35 lines
- `Assets\Scripts\Character\Core\OwnerCharacterFactory.cs`: 126 lines
- `Assets\Scripts\Character\Core\OwnerRunManager.cs`: 194 lines
- `Assets\Scripts\Character\UI\CharacterFeedbackBubble.cs`: 341 lines
- `Assets\Scripts\Character\UI\CharacterFeedbackBubbleFactory.cs`: 35 lines
- `Assets\Scripts\Character\UI\CharacterFeedbackBubbleViewFactory.cs`: 70 lines
- `Assets\Scripts\Character\UI\CharacterFloatingIcon.cs`: 92 lines
- `Assets\Scripts\Character\UI\OwnerSelectionPanel.cs`: 140 lines
- `Assets\Scripts\Character\UI\OwnerSelectionOptionButtonFactory.cs`: 70 lines
- `Assets\Scripts\Character\UI\StaffWorkPriorityPanel.cs`: 528 lines
- `Assets\Scripts\Character\Work\WorkGridUtility.cs`: 58 lines
- `Assets\Scripts\Character\Work\WorkTaskExecutor.cs`: 490 lines
- `Assets\Scripts\Defense\DefenseStatusRuntimeFactory.cs`: 32 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordComponentFactory.cs`: 23 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRuntime.cs`: 764 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionStateComponentFactory.cs`: 24 lines
- `Assets\Scripts\Grid\Building\GridBuildingObjectFactory.cs`: 48 lines
- `Assets\Scripts\Grid\Building\GridBuildingRuntime.cs`: 531 lines
- `Assets\Scripts\Grid\DungeonStory\Building\DungeonStoryGridBuildingController.cs`: 463 lines
- `Assets\Scripts\Grid\DungeonStory\UI\GridConstructButtonFactory.cs`: 114 lines
- `Assets\Scripts\Grid\DungeonStory\UI\GridConstructTab.cs`: 230 lines
- `Assets\Scripts\Grid\DungeonStory\UI\GridUIManager.cs`: 118 lines
- `Assets\Scripts\Grid\Rendering\GridTexture.cs`: 491 lines
- `Assets\Scripts\Grid\UI\GridGhostObject.cs`: 262 lines
- `Assets\Scripts\Grid\UI\GridPlacementGhostPresenter.cs`: 134 lines
- `Assets\Scripts\Grid\UI\UIGridTab.cs`: 39 lines
- `Assets\Scripts\Infrastructure\DungeonBackdropReferenceProvider.cs`: 65 lines
- `Assets\Scripts\Infrastructure\DungeonGridBuildingRuntimeProviders.cs`: 108 lines
- `Assets\Scripts\Infrastructure\DungeonRuntimeLifetimeScope.cs`: 543 lines
- `Assets\Scripts\Infrastructure\SceneBuildableLeakValidator.cs`: 136 lines
- `Assets\Scripts\Invasion\InvasionIntruderFactory.cs`: 67 lines
- `Assets\Scripts\Invasion\InvasionIntruderPlanner.cs`: 105 lines
- `Assets\Scripts\Invasion\InvasionIntruderSystem.cs`: 425 lines
- `Assets\Scripts\Meta\RunResultPanelFactory.cs`: 67 lines
- `Assets\Scripts\Meta\RunResultPanel.cs`: 29 lines
- `Assets\Scripts\Offense\OffenseExpeditionSystem.cs`: 380 lines
- `Assets\Scripts\Offense\OffensePanelFactory.cs`: 117 lines
- `Assets\Scripts\Offense\OffensePanelUiFactory.cs`: 161 lines
- `Assets\Scripts\Offense\OffenseWorldMapSystem.cs`: 279 lines
- `Assets\Scripts\Operation\EventAlertChoicePresenter.cs`: 53 lines
- `Assets\Scripts\Operation\EventAlertSystem.cs`: 129 lines
- `Assets\Scripts\Operation\EventAlertUiFactory.cs`: 361 lines
- `Assets\Scripts\Operation\EventAlertViewPresenter.cs`: 252 lines
- `Assets\Scripts\UI\BuildingSummaryInfo.cs`: 143 lines
- `Assets\Scripts\UI\CameraManager.cs`: 184 lines
- `Assets\Scripts\UI\CharacterSummaryRuntimeLogFactory.cs`: 70 lines
- `Assets\Scripts\UI\CharacterSummeryInfo.cs`: 228 lines
- `Assets\Scripts\UI\DungeonSceneBackdropFitter.cs`: 258 lines
- `Assets\Scripts\UI\NoticeFeedItemFactory.cs`: 94 lines
- `Assets\Scripts\UI\NoticeFeedPresenter.cs`: 80 lines
- `Assets\Scripts\UI\RuntimePanelFactories.cs`: 230 lines
- `Assets\Scripts\UI\UITab.cs`: 63 lines
- `Assets\Scripts\UI\UITabGeneratedPanelFactory.cs`: 157 lines
- `Assets\Scripts\UI\UITabManager.cs`: 557 lines
- `Assets\Scripts\UI\UITabTopButtonFactory.cs`: 124 lines
- `Assets\Scripts\UI\WorldInfoClickSelector.cs`: 145 lines

## DependencyInjection

- `Assets\Scripts\Buildings\BuildableObject.cs`: 560 lines
- `Assets\Scripts\Buildings\Shop.cs`: 959 lines
- `Assets\Scripts\Buildings\UI\UIBuildingInfo.cs`: 129 lines
- `Assets\Scripts\Buildings\UI\UIBuildingSelectButton.cs`: 108 lines
- `Assets\Scripts\Character\Ability\AbilityMove.cs`: 564 lines
- `Assets\Scripts\Character\Ability\AbilityShopping.cs`: 380 lines
- `Assets\Scripts\Character\Ability\AbilityWork.cs`: 666 lines
- `Assets\Scripts\Character\Ability\CharacterAbility.cs`: 84 lines
- `Assets\Scripts\Character\AI\AIBrain.cs`: 1294 lines
- `Assets\Scripts\Character\AI\AiDirectorRuntime.cs`: 610 lines
- `Assets\Scripts\Character\AI\CharacterAiScheduler.cs`: 561 lines
- `Assets\Scripts\Character\AI\CharacterDialogueBubbleFactory.cs`: 46 lines
- `Assets\Scripts\Character\AI\CharacterDialogueRuntime.cs`: 283 lines
- `Assets\Scripts\Character\AI\CharacterSocialMemoryFactory.cs`: 36 lines
- `Assets\Scripts\Character\AI\CustomerPersonaRuntime.cs`: 323 lines
- `Assets\Scripts\Character\AI\SocialReputationRuntime.cs`: 1001 lines
- `Assets\Scripts\Character\CharacterSpawner.cs`: 338 lines
- `Assets\Scripts\Character\CharacterSpawnObjectFactory.cs`: 64 lines
- `Assets\Scripts\Character\Core\CharacterActor.cs`: 730 lines
- `Assets\Scripts\Character\Core\CharacterLifecycle.cs`: 182 lines
- `Assets\Scripts\Character\Core\CharacterStats.cs`: 301 lines
- `Assets\Scripts\Character\Core\OwnerCharacterFactory.cs`: 126 lines
- `Assets\Scripts\Character\Core\OwnerRunManager.cs`: 194 lines
- `Assets\Scripts\Character\Input\OwnerCommandController.cs`: 148 lines
- `Assets\Scripts\Character\UI\CharacterFeedbackBubble.cs`: 341 lines
- `Assets\Scripts\Character\UI\CharacterFeedbackBubbleFactory.cs`: 35 lines
- `Assets\Scripts\Character\UI\CharacterFeedbackBubbleViewFactory.cs`: 70 lines
- `Assets\Scripts\Character\UI\OwnerSelectionPanel.cs`: 140 lines
- `Assets\Scripts\Character\UI\OwnerSelectionOptionButtonFactory.cs`: 70 lines
- `Assets\Scripts\Character\UI\StaffWorkPriorityPanel.cs`: 528 lines
- `Assets\Scripts\Character\UI\StaffWorkPriorityPanelModel.cs`: 94 lines
- `Assets\Scripts\Character\Work\StaffDiscontentSystem.cs`: 758 lines
- `Assets\Scripts\Codex\CodexSystem.cs`: 462 lines
- `Assets\Scripts\Codex\UI\CodexPanel.cs`: 120 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordEventRecorder.cs`: 421 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRecordRuntime.cs`: 98 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionBuildingReplacerFactory.cs`: 44 lines
- `Assets\Scripts\FacilityEvolution\FacilityEvolutionRuntime.cs`: 764 lines
- `Assets\Scripts\FacilityEvolution\UI\FacilityEvolutionPanel.cs`: 281 lines
- `Assets\Scripts\FacilityShop\FacilityShopSystem.cs`: 727 lines
- `Assets\Scripts\Grid\DungeonStory\Building\DungeonStoryGridBuildingController.cs`: 463 lines
- `Assets\Scripts\Grid\DungeonStory\UI\DungeonStoryGridGhostPresenter.cs`: 149 lines
- `Assets\Scripts\Grid\DungeonStory\UI\GridConstructButtonFactory.cs`: 114 lines
- `Assets\Scripts\Grid\DungeonStory\UI\GridConstructTab.cs`: 230 lines
- `Assets\Scripts\Grid\DungeonStory\UI\GridUIManager.cs`: 118 lines
- `Assets\Scripts\Infrastructure\DungeonRuntimeLifetimeScope.cs`: 543 lines
- `Assets\Scripts\Infrastructure\SceneBuildableLeakValidator.cs`: 136 lines
- `Assets\Scripts\Invasion\InvasionIntruderSystem.cs`: 425 lines
- `Assets\Scripts\Invasion\InvasionThreatRuntime.cs`: 281 lines
- `Assets\Scripts\Meta\RunResultPanelFactory.cs`: 67 lines
- `Assets\Scripts\Meta\MetaProgressionSystem.cs`: 247 lines
- `Assets\Scripts\Offense\OffenseExpeditionSystem.cs`: 380 lines
- `Assets\Scripts\Offense\OffenseRewardSystem.cs`: 294 lines
- `Assets\Scripts\Offense\OffensePanelFactory.cs`: 117 lines
- `Assets\Scripts\Offense\OffenseWorldMapSystem.cs`: 279 lines
- `Assets\Scripts\Operation\EventAlertSystem.cs`: 129 lines
- `Assets\Scripts\Operation\EventAlertChoicePresenter.cs`: 53 lines
- `Assets\Scripts\Operation\EventAlertUiFactory.cs`: 361 lines
- `Assets\Scripts\Operation\EventAlertViewPresenter.cs`: 252 lines
- `Assets\Scripts\Operation\OperatingDaySettlement.cs`: 662 lines
- `Assets\Scripts\Research\BlueprintResearchSystem.cs`: 448 lines
- `Assets\Scripts\Run\RunVariableSystem.cs`: 272 lines
- `Assets\Scripts\Synthesis\FacilitySynthesisSystem.cs`: 466 lines
- `Assets\Scripts\Synthesis\UI\FacilitySynthesisPanel.cs`: 126 lines
- `Assets\Scripts\UI\BuildingSummaryInfo.cs`: 143 lines
- `Assets\Scripts\UI\CameraManager.cs`: 184 lines
- `Assets\Scripts\UI\CharacterSummaryRuntimeLogFactory.cs`: 70 lines
- `Assets\Scripts\UI\CharacterSummeryInfo.cs`: 228 lines
- `Assets\Scripts\UI\DungeonSceneBackdropFitter.cs`: 258 lines
- `Assets\Scripts\UI\NoticeFeed.cs`: 59 lines
- `Assets\Scripts\UI\NoticeFeedItemFactory.cs`: 94 lines
- `Assets\Scripts\UI\NoticeFeedPresenter.cs`: 80 lines
- `Assets\Scripts\UI\RuntimePanelFactories.cs`: 230 lines
- `Assets\Scripts\UI\UITab.cs`: 63 lines
- `Assets\Scripts\UI\UITabGeneratedPanelFactory.cs`: 157 lines
- `Assets\Scripts\UI\UITabManager.cs`: 557 lines
- `Assets\Scripts\UI\UITabTopButtonFactory.cs`: 124 lines

