#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Intake for an actual controlled campaign. It never calls the source-v8
/// fixture loops and never adds a model response. The campaign runner must have
/// already captured official full-world checkpoints and exact transport bytes.
/// </summary>
internal static class GameplayModuleSelectionContinuousSourceV9Exporter
{
    private const string InventoryEnvironment = "DUNGEONSTORY_SOURCE_V9_INVENTORY";
    private const string MechanicsManifestEnvironment = "DUNGEONSTORY_SOURCE_V9_MECHANICS_MANIFEST";
    private const string PackageIdEnvironment = "DUNGEONSTORY_SOURCE_V9_PACKAGE_ID";
    private const string FinalDirectoryName = "source-v9";
    private const string StagingDirectoryName = "source-v9-staging";
    private const string RepeatDirectoryName = "source-v9-repeat";
    private const string DeterminismReportName = "source-v9-determinism.json";

    private static readonly string[] CodeIdentityFiles =
    {
        "Assets/Scripts/Services/Infrastructure/Save/GameWorldLineageSaveSection.cs",
        "Assets/Scripts/Services/Infrastructure/Registration/DungeonSaveRegistration.cs",
        "Assets/Scripts/Models/Work/DungeonStaffDiscontentSaveData.cs",
        "Assets/Scripts/Services/Character/Work/StaffDiscontentFacts.cs",
        "Assets/Scripts/Services/Character/Work/StaffDiscontentSaveSection.cs",
        "Assets/Scripts/Services/Character/Work/StaffDiscontentSystem.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/SocialLife/StaffDiscontentFactPreflight.cs",
        "Assets/Scripts/Models/Evolution/Core/GameplayOutcomeEvidenceBindingSnapshot.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/GameplayOutcomeEvidenceBindingAuthority.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/GameplayOutcomeEvidenceFormulaProjection.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/GameplayOutcomeNarrativeEvidenceQuery.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/GameplayOutcomeEvidencePublicMaterialComposer.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/GameplayExperienceObservation.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/GameplayExperienceObserverRegistration.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/GameplayExperienceRule.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayActionLogModule.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayActionLogCatalog.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayOutcomeRegistry.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Life/CaptivityExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Life/CharacterLifecycleExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Life/CraftingExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Life/EvolutionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Life/LifeExperiencePolicyFacts.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Life/SkillExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Life/SocialExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Life/VisitorExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Migrated/MigratedCharacterSurvivalExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Migrated/MigratedCombatExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Migrated/MigratedCommerceServiceExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Migrated/MigratedExperiencePolicyFacts.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Migrated/MigratedFactionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Migrated/MigratedInvasionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Migrated/MigratedOffenseRunExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/Migrated/MigratedWorldWorkExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/World/ClinicalActionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/World/CommerceResearchActionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/World/CropActionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/World/EnvironmentActionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/World/FireActionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/World/InfrastructureActionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/World/ProductionActionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/World/WildlifeActionExperiencePolicies.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/ActionPolicies/World/WorldExperiencePolicyFacts.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentalExperienceObserver.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionCanonicalJson.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleDispatchEligibilityCapture.cs",
        "Assets/Scripts/Services/Infrastructure/Editor/LiveNarrativeCausalPlan.cs",
        "Assets/Scripts/Services/Narrative/CausalExecution/CausalGameplayPlanExecution.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/Editor/ProductionCausalPlanBatchExporter.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionContinuousSourceV9Exporter.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionReplayProofV2Contract.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionSourceV9ReplayInput.cs",
        "Assets/Scripts/Services/Character/AI/Editor/GameplayModuleSelectionActualModelAuthoringBatchRunner.cs",
        "Assets/Scripts/Models/AI/Core/DungeonStoryHostStructuredChatBackend.cs",
        "Assets/Scripts/Models/AI/Core/DungeonStoryLlmHostProcess.cs",
        "Assets/Scripts/Models/AI/Core/CharacterSkillCompactModelPrompt.cs",
        "Assets/Scripts/Models/AI/Core/NarrativeStructuredOutputCore.cs",
        "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitEffectSource.cs",
        "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitInferenceService.cs",
        "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitManifestationRuntime.cs",
        "Assets/Scripts/Services/Character/Core/CharacterAcquiredTraitState.cs",
        "Assets/Scripts/Services/Character/Core/CharacterProgression.cs",
        "Assets/Scripts/Services/Character/Core/CharacterSkillFormulaGeneration.cs",
        "Assets/Scripts/Services/Character/Core/CharacterSkillGenerationService.cs",
        "Assets/Scripts/Services/Character/Core/CharacterSkillModels.cs",
        "Assets/Scripts/Services/Character/Core/CharacterPopulationService.cs",
        "Assets/Scripts/Services/Character/Core/CharacterPopulationServiceFacade.cs",
        "Assets/Scripts/Services/Character/CharacterSpawner.cs",
        "Assets/Scripts/Models/Characters/CharacterSpawnRules.cs",
        "Assets/Scripts/Services/Recruitment/RegularCustomerRuntime.cs",
        "Assets/Scripts/Services/Recruitment/RegularCustomerSystem.cs",
        "Assets/Scripts/Services/Recruitment/SettlementPopulationCapacityRuntime.cs",
        "Assets/Scripts/Services/Character/Work/SettlementEmergencyReservePlanner.cs",
        "Assets/Scripts/Services/Character/Work/EmergencyWorkAccountingContracts.cs",
        "Assets/Scripts/Services/Character/Work/EmergencyWorkAccountingRuntime.cs",
        "Assets/Scripts/Services/Character/Work/SettlementAlertRuntime.cs",
        "Assets/Scripts/Services/Character/Work/CharacterWorkRoleUtility.cs",
        "Assets/Scripts/Services/Recruitment/RecruitedCharacterActivationService.cs",
        "Assets/Scripts/Models/Recruitment/Core/DungeonRegularCustomerSaveData.cs",
        "Assets/Scripts/Views/UI/OperationsFeatureCommandService.cs",
        "Assets/Scripts/Views/UI/Core/OperationsFeatureSurfacePresenter.cs",
        "Assets/Scripts/Views/UI/OperationsFeatureQueryService.cs",
        "Assets/Scripts/Views/UI/WarehouseFeatureSurfacePresenter.cs",
        "Assets/Scripts/Views/UI/WarehouseFeatureCommandService.cs",
        "Assets/Scripts/Services/Infrastructure/RunVariableCatalogServices.cs",
        "Assets/Scripts/Services/Infrastructure/Editor/ShopContinuationSkillResponseFixture.cs",
        "Assets/Scripts/Services/Items/WorldItemStackRuntime.cs",
        "Assets/Scripts/Services/Buildings/SO/StockInfo.cs",
        "Assets/Scripts/Services/Buildings/Abilities/BuildingAbility.cs",
        "Assets/Scripts/Models/Production/Core/ProductionPrimitives.cs",
        "Assets/Scripts/Services/Character/AI/AIBrain.cs",
        "Assets/Scripts/Services/Character/AI/AIBrainCandidateCommitter.cs",
        "Assets/Scripts/Services/Character/Core/CharacterActor.cs",
        "Assets/Scripts/Services/Character/AI/AIActionRuntime.cs",
        "Assets/Scripts/Services/Infrastructure/AI/Actions/AIShoppingAdapter.cs",
        "Assets/Scripts/Services/Character/Ability/AbilityShopping.cs",
        "Assets/Scripts/Services/Character/Ability/CharacterVisitPolicy.cs",
        "Assets/Scripts/Services/Character/Core/CharacterBuildingVisitorPort.cs",
        "Assets/Scripts/Services/Buildings/Facility.cs",
        "Assets/Scripts/Services/Buildings/BuildableObject.cs",
        "Assets/Scripts/Services/Buildings/FacilityUseExecutionState.cs",
        "Assets/Scripts/Services/Buildings/FacilityOperationalData.cs",
        "Assets/Scripts/Services/Buildings/BuildingStateModule.cs",
        "Assets/Scripts/Services/Character/AI/Action/AIWork.cs",
        "Assets/Scripts/Services/Character/Ability/AbilityWork.cs",
        "Assets/Scripts/Services/Character/Work/WorkCommandHandler.cs",
        "Assets/Scripts/Services/Character/Work/WorkDutyController.cs",
        "Assets/Scripts/Services/Character/Work/WorkTargetSelector.cs",
        "Assets/Scripts/Services/Combat/Work/RepairWorkExecutionHandler.cs",
        "Assets/Scripts/Services/Character/Work/WorkControlChangedEvent.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/WorkControlActivityObserver.cs",
        "Assets/Scripts/Services/Character/Work/RepairWorkOutcomeRuntime.cs",
        "Assets/Scripts/Services/Character/Work/ConstructionWorkEndedFact.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/ConstructionWorkEndedFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/ConstructionWorkEndedFactPreflight.cs",
        "Assets/Scripts/Services/Character/Work/RepairWorkCompletedFact.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/RepairWorkCompletedFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/RepairWorkCompletionObservation.cs",
        "Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs",
        "Assets/Scripts/Services/Character/Work/WorkExecutionChangedEvent.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/WorkExecutionActivityObserver.cs",
        "Assets/Scripts/Services/Character/Work/WorkAccidentFact.cs",
        "Assets/Scripts/Services/Character/Work/WorkAccidentObservationSaveData.cs",
        "Assets/Scripts/Services/Character/Work/WorkAccidentObservationRuntime.cs",
        "Assets/Scripts/Services/Character/Work/WorkOrderContracts.cs",
        "Assets/Scripts/Services/Character/Work/WorkOrderAggregateState.cs",
        "Assets/Scripts/Services/Character/Work/WorkAmountSystem.cs",
        "Assets/Scripts/Services/Character/Work/WorkOrderSaveValidation.cs",
        "Assets/Scripts/Services/Character/Work/WorkOrdersSaveSection.cs",
        "Assets/Scripts/Services/Character/Identity/Runtime/CharacterIdentityRuntime.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/WorkAccidentFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/WorkAccidentFactPreflight.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/WorkAccidentActivityObserver.cs",
        "Assets/Scripts/Services/Character/Work/WorkExecutionRules.cs",
        "Assets/Scripts/Services/Character/Work/WorkExecutionRegistry.cs",
        "Assets/Scripts/Services/Character/Work/CharacterWorkPerformanceContextResolver.cs",
        "Assets/Scripts/Services/Character/Core/CharacterStats.cs",
        "Assets/Scripts/Services/Character/Core/CharacterStatsVitalsService.cs",
        "Assets/Scripts/Services/Character/Core/CharacterVitalsChangedEvent.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/CharacterVitalsActivityObserver.cs",
        "Assets/Scripts/Models/Characters/CharacterRestAppliedFact.cs",
        "Assets/Scripts/Services/Survival/PrimitiveSurvivalCompletedFact.cs",
        "Assets/Scripts/Services/Infrastructure/Environment/ApparelWorkCompletedFact.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/ApparelWorkCompletedFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/ApparelWorkCompletedFactPreflight.cs",
        "Assets/Scripts/Services/Survival/PrimitiveSurvivalChangedEvent.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/PrimitiveSurvivalCompletedFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/PrimitiveSurvivalActivityObserver.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/PrimitiveSurvivalFactPreflight.cs",
        "Assets/Scripts/Services/Character/Core/CharacterStateChangedEvents.cs",
        "Assets/Scripts/Services/Foundation/Events/CharacterEventContext.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CharacterRestFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CharacterRestFactPreflight.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/CharacterStateNarrativeObserver.cs",
        "Assets/Scripts/Services/Character/Core/CharacterPerformanceQuery.cs",
        "Assets/Scripts/Services/Foundation/CharacterPerformanceExecutionTrace.cs",
        "Assets/Scripts/Services/Infrastructure/Rooms/RoomEnvironmentAdapter.cs",
        "Assets/Scripts/Services/Infrastructure/Rooms/RoomLayoutCacheAdapter.cs",
        "Assets/Scripts/Services/Infrastructure/Rooms/RoomLayoutAdapter.cs",
        "Assets/Scripts/Services/Infrastructure/Rooms/RoomDetectorAdapter.cs",
        "Assets/Scripts/Services/Infrastructure/Rooms/RoomInstanceAdapter.cs",
        "Assets/Scripts/Services/Infrastructure/Rooms/RoomFacilityPolicyAdapter.cs",
        "Assets/Scripts/Services/FacilityEvolution/RoomProfile.cs",
        "Assets/Scripts/Controllers/Grid/DungeonStory/Building/DungeonStoryGridBuildingController.cs",
        "Assets/Scripts/Services/Grid/Building/GridBuildingRuntime.cs",
        "Assets/Scripts/Services/Grid/Placement/GridPlacementValidator.cs",
        "Assets/Scripts/Services/Survival/Work/CleanWorkExecutionHandler.cs",
        "Assets/Scripts/Services/Survival/WorldFilthRuntime.cs",
        "Assets/Scripts/Services/Combat/CharacterCombatCommandRuntime.cs",
        "Assets/Scripts/Services/Combat/CombatEquipmentLoadoutRuntime.cs",
        "Assets/Scripts/Services/Combat/CombatEquipmentRuntime.cs",
        "Assets/Scripts/Services/Combat/CombatEquipmentStatProjector.cs",
        "Assets/Scripts/Services/Combat/CombatResolutionService.cs",
        "Assets/Scripts/Services/Combat/CombatCommandResultApplier.cs",
        "Assets/Scripts/Services/Combat/CharacterBodyHealthRuntime.cs",
        "Assets/Scripts/Services/Combat/CharacterBodyHealthStateRules.cs",
        "Assets/Scripts/Models/Combat/Core/CombatWeaponPrimitives.cs",
        "Assets/Scripts/Models/Economy/Content/CombatWeaponSO.cs",
        "Assets/Scripts/Services/Evolution/EvolutionHistoryNarrativeRuntime.cs",
        "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRuntime.cs",
        "Assets/Scripts/Services/FacilityEvolution/FacilityFormulaEvolutionPresentationRuntime.cs",
        "Assets/Scripts/Services/FacilityEvolution/FacilityUsagePublicProjection.cs",
        "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionStateComponent.cs",
        "Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecipeSO.cs",
        "Assets/Scripts/Services/Infrastructure/Editor/DungeonLiveNarrativeCampaignRunner.cs",
        "Assets/Scripts/Services/Infrastructure/Editor/LiveFacilityUsageEvidence.cs",
        "Assets/Scripts/Services/Infrastructure/DungeonGameSaveService.cs",
        "Assets/Scripts/Services/Infrastructure/Save/DungeonAggregateReferencePreflight.cs",
        "Assets/Scripts/Services/Infrastructure/V18TypedCharacterReferenceRestoreNormalizer.cs",
        "Assets/Scripts/Services/Effects/Runtime/CharacterDerivedStatsSnapshot.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Presentation/GameplayOutcomeEvidenceUseTransaction.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CharacterSkillAcquiredOutcomeDefinition.cs",
        "Assets/Scripts/Services/Offense/OffenseExpeditionAccessRules.cs",
        "Assets/Scripts/Services/Infrastructure/BlueprintResearchRuntime.cs",
        "Assets/Scripts/Services/Infrastructure/BlueprintResearchProjectCoordinator.cs",
        "Assets/Scripts/Models/Research/Core/ResearchProjectContracts.cs",
        "Assets/Scripts/Models/Research/Core/ResearchQueueSystem.cs",
        "Assets/Scripts/Services/Infrastructure/ResearchWorkExecutionAdapter.cs",
        "Assets/Scripts/Models/Work/ResearchWorkExecutionHandler.cs",
        "Assets/Scripts/Services/Infrastructure/ResearchBlueprintArchiveAdapter.cs",
        "Assets/Scripts/Services/FacilityShop/DailyFacilityShopRuntime.cs",
        "Assets/Scripts/Services/FacilityShop/FacilityShopSystem.cs",
        "Assets/Scripts/Views/UI/ShopFeatureSurfacePresenter.cs",
        "Assets/Scripts/Services/Infrastructure/GameSessionRuntimeServices.cs",
        "Assets/Scripts/Services/Offense/OffenseExpeditionRuntime.cs",
        "Assets/Scripts/Services/Offense/OffenseExpeditionLaunchService.cs",
        "Assets/Scripts/Services/Offense/OffenseExpeditionModel.cs",
        "Assets/Scripts/Services/Offense/OffenseJourneyModel.cs",
        "Assets/Scripts/Services/Offense/OffenseExpeditionBattleCompletionHandler.cs",
        "Assets/Scripts/Services/Offense/OffenseBattleRuntime.cs",
        "Assets/Scripts/Services/Offense/OffenseEncounterCatalog.cs",
        "Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntime.cs",
        "Assets/Scripts/Services/Offense/OffenseReturnArrivalRuntimeServices.cs",
        "Packages/jp.hadashikick.vcontainer/Runtime/Internal/TypeAnalyzer.cs",
        "Assets/Scripts/Services/Offense/OffenseBattleModel.cs",
        "Assets/Scripts/Services/Offense/OffenseAggregateSaveValidation.cs",
        "Assets/Scripts/Services/Offense/EnemyEncounterFactory.cs",
        "Assets/Scripts/Services/Offense/EnemyIndividualRuntime.cs",
        "Assets/Scripts/Models/Offense/Core/OffenseBattleContracts.cs",
        "Assets/Scripts/Services/Offense/OffenseCombatAbilities.cs",
        "Assets/Scripts/Services/Offense/OffenseCombatEffectRuntime.cs",
        "Assets/Scripts/Services/Offense/OffenseBattleCommandFact.cs",
        "Assets/Scripts/Services/Offense/OffenseBattleCommandPublicationRuntime.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseBattleCommandFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseBattleCommandFactObserver.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/MigratedProducers/OffenseBattleCommandFactPreflight.cs",
        "Assets/Scripts/Services/Character/Core/CharacterSkillRuntimeEffects.cs",
        "Assets/Scripts/Services/Character/Core/CharacterSkillActivationFact.cs",
        "Assets/Scripts/Services/Character/Core/CharacterSkillActivationOutcomeTransaction.cs",
        "Assets/Scripts/Services/Character/Core/CharacterSkillActivationPublicationRuntime.cs",
        "Assets/Scripts/Services/Character/Core/CharacterSkillActivationPublicationSaveSection.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CharacterSkillActivationOutcomeDefinition.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CharacterSkillActivationFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CharacterSkillActivationFactPreflight.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ExternalFaction/InvasionExposureExperienceObserver.cs",
        "Assets/Scripts/Models/Species/Core/DiseaseAnatomyBurdenAppliedFact.cs",
        "Assets/Scripts/Models/Species/Core/CharacterLifeDomain.cs",
        "Assets/Scripts/Models/Species/Core/CharacterLifeTransitionFact.cs",
        "Assets/Scripts/Models/Species/Core/CharacterLifeObservationState.cs",
        "Assets/Scripts/Models/Species/Core/AgeConditionAnatomyAppliedFact.cs",
        "Assets/Scripts/Models/Species/Core/CharacterSpeciesRuntimeContracts.cs",
        "Assets/Scripts/Models/Species/Core/SpeciesActionFactData.cs",
        "Assets/Scripts/Services/Character/SpeciesRuntime.cs",
        "Assets/Scripts/Services/Character/SpeciesRuntimeSaveSection.cs",
        "Assets/Scripts/Services/Character/SpeciesActionFacts.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/SpeciesActionFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/SpeciesActionFactPreflight.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/SpeciesIncidentActivityObserver.cs",
        "Assets/Scripts/Services/Character/CharacterLifeApplicationAdapter.cs",
        "Assets/Scripts/Services/Character/AgeConditionAnatomyProjectionRuntime.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CharacterLifeTransitionFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/AgeConditionAnatomyFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Evolution/CharacterLifeFactPreflight.cs",
        "Assets/Scripts/Models/Operation/Core/EventAlertSaveContracts.cs",
        "Assets/Scripts/Models/Operation/Core/EmergencyWorkSuspensionFactData.cs",
        "Assets/Scripts/Services/Character/Work/SettlementAlertContracts.cs",
        "Assets/Scripts/Services/Character/Work/EmergencyWorkSuspensionFact.cs",
        "Assets/Scripts/Services/Character/Work/EmergencyWorkSuspensionObservationState.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/EmergencyWorkSuspensionFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/EmergencyWorkSuspensionFactPreflight.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/EmergencyWorkSuspensionOutcomeDefinition.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/DiseaseAnatomyBurdenFactMapping.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/DiseaseAnatomyBurdenFactPreflight.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/ProductionCombat/WorkCompletionIdentityOutcomeDefinition.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/Environment/EnvironmentOutcomeDescriptors.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomeBridges/KnowledgeCompletionOutcomeDefinition.cs",
        "Assets/Scripts/Services/Narrative/GameplayOutcomes/GameplayOutcomePersistence.cs",
        "Assets/Scripts/Models/AI/Core/LocalLlmRequestQueue.cs",
        "Tools/QA/run_isolated_unity_batch.ps1",
        "Tools/QA/IsolatedUnityLauncher.cs"
    };

    internal static JObject Export(string campaignOutput, int registeredSections)
        => ExportPair(campaignOutput, registeredSections, ExportSkillTraitOnce);

    private static JObject ExportSkillTraitOnce(
        string campaignOutput, int registeredSections, string directoryName)
    {
        if (registeredSections <= 0)
            throw new ArgumentOutOfRangeException(nameof(registeredSections));
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string campaignRoot = RequireDirectoryInside(
            projectRoot,
            campaignOutput,
            Path.Combine(projectRoot, "Artifacts", "QA"),
            "campaign output");
        string inventoryPath = RequireFileInside(
            projectRoot,
            RequireEnvironment(InventoryEnvironment),
            "source inventory");
        string mechanicsManifestPath = RequireFileInside(
            projectRoot,
            RequireEnvironment(MechanicsManifestEnvironment),
            "B1 mechanics manifest");
        string packageId = RequirePackageId(RequireEnvironment(PackageIdEnvironment));
        string staging = Path.Combine(campaignRoot, directoryName + "-staging");
        string final = Path.Combine(campaignRoot, directoryName);
        if (Directory.Exists(staging) || File.Exists(staging)
            || Directory.Exists(final) || File.Exists(final))
        {
            throw new InvalidOperationException(
                "Source-v9 intake output is create-only.");
        }

        Directory.CreateDirectory(staging);
        JObject inventory = ReadObject(inventoryPath);
        RequireString(inventory, "gameCommit");
        RequireSha(inventory, "dirtySourceSha256");
        RequireSha(inventory, "contentSha256");
        if (inventory["files"] is not JArray inventoryFiles || inventoryFiles.Count == 0)
            throw new InvalidOperationException("Source inventory has no files.");
        JObject b1Manifest = ReadObject(mechanicsManifestPath);
        if ((bool?)b1Manifest["trainingEligible"] != false
            || (bool?)b1Manifest["humanApprovalClaimed"] != false)
        {
            throw new InvalidOperationException(
                "B1 mechanics input must remain review-only.");
        }

        JObject inventoryDescriptor = CopyInput(
            inventoryPath,
            staging,
            "inputs/source-inventory.json");
        JObject b1Descriptor = CopyInput(
            mechanicsManifestPath,
            staging,
            "inputs/b1-evidence-manifest.json");
        JObject[] codeIdentity = CodeIdentityFiles
            .Select(path => ProjectFileDescriptor(projectRoot, path))
            .ToArray();
        JObject mechanics = new()
        {
            ["schema"] = "dungeonstory-mechanics-source-v9@1",
            ["sourcePackageId"] = packageId,
            ["gameCommit"] = (string)inventory["gameCommit"],
            ["dirtySourceSha256"] = (string)inventory["dirtySourceSha256"],
            ["contentSha256"] = (string)inventory["contentSha256"],
            ["sourceInventory"] = inventoryDescriptor,
            ["b1EvidenceManifest"] = b1Descriptor,
            ["codeIdentity"] = new JArray(codeIdentity),
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };

        string[] commandNames = Directory.GetFiles(campaignRoot, "command-*.json")
            .Select(Path.GetFileName)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (commandNames.Length < 4)
            throw new InvalidOperationException(
                "A continuous source requires multiple actual campaign commands.");
        bool resumedTrait = File.Exists(Path.Combine(campaignRoot, "resumed-trait-pending.json"));
        var campaignFiles = new List<JObject>();
        var commandIntents = new JArray();
        var rootOperations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in commandNames)
        {
            JObject command = ReadObject(Path.Combine(campaignRoot, name));
            if (command["publishedOutcomes"] is not JArray outcomes || outcomes.Count == 0)
                throw new InvalidOperationException("Campaign command has no outcomes: " + name);
            commandIntents.Add(new JObject
            {
                ["command"] = RequireString(command, "command"),
                ["actorId"] = RequireString(command, "actorId"),
                ["facilityId"] = RequireString(command, "facilityId")
            });
            foreach (JObject outcome in outcomes.OfType<JObject>())
            {
                string runId = RequireString(outcome, "runId");
                int sequence = (int?)outcome["sequence"]
                    ?? throw new InvalidOperationException("Outcome sequence is missing.");
                rootOperations.Add(runId + "/" + sequence);
            }
            campaignFiles.Add(CopyCampaign(campaignRoot, staging, name,
                "campaign/" + name));
            if ((bool?)command["activeUseObserved"] == true)
            {
                string checkpoint = RequireString(command, "activeUseCheckpointFile");
                string expected = "training-active-world-"
                    + name.Substring("command-".Length, 3) + ".json";
                if ((bool?)command["activeUseOfficialSaveCaptured"] != true || checkpoint != expected)
                    throw new InvalidOperationException("Actual training in-use checkpoint is missing: " + name);
                campaignFiles.Add(CopyCampaign(campaignRoot, staging, checkpoint,
                    "campaign/" + checkpoint));
            }
        }
        foreach (string name in new[]
        {
            "initial-world.json",
            "post-dispatch-world.json"
        })
        {
            campaignFiles.Add(CopyCampaign(campaignRoot, staging, name,
                "campaign/" + name));
        }
        if (resumedTrait)
        {
            foreach (string name in new[] { "resumed-trait-pending.json", "resumed-trait-evidence.json",
                "skill-intake-continuation-witness.json" })
                campaignFiles.Add(CopyCampaign(campaignRoot, staging, name, "campaign/" + name));
        }
        bool explicitPilotSeed = File.Exists(Path.Combine(campaignRoot, "pilot-initial-seed.json"));
        if (explicitPilotSeed)
            campaignFiles.Add(CopyCampaign(campaignRoot, staging, "pilot-initial-seed.json", "campaign/pilot-initial-seed.json"));
        JObject campaign = new()
        {
            ["schema"] = "dungeonstory-executed-campaign-v9@1",
            ["sourcePackageId"] = packageId,
            ["sourceTier"] = "controlled-campaign",
            ["lineage"] = new JObject
            {
                ["campaignInstanceId"] = packageId + ":campaign",
                ["runId"] = packageId + ":run:skill-trait",
                ["branchId"] = resumedTrait
                    ? "restored-trait-request-and-actual-training-to-new-skill"
                    : "actual-training-to-skill-and-trait-pending",
                ["planTemplateFamily"] = "controlled-facility-training-sequence",
                ["planHash"] = GameplayModuleSelectionCanonicalJson.Hash(commandIntents),
                ["rootOperations"] = new JArray(rootOperations)
            },
            ["commandIntentsNonAuthoritative"] = commandIntents,
            ["restoredExistingTraitRequest"] = resumedTrait,
            ["explicitTestOnlyInitialSeed"] = explicitPilotSeed,
            ["files"] = new JArray(campaignFiles),
            ["fabricatedOutcome"] = false,
            ["naturalPlayClaimed"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };

        JObject skill = BuildSelection(
            campaignRoot,
            staging,
            registeredSections,
            packageId,
            "skill",
            "dispatch-witness.json",
            "dispatch-world.json",
            "dispatch-wire.json",
            "replayed-wire.json",
            "restored-pending.json");
        JObject trait = BuildSelection(
            campaignRoot,
            staging,
            registeredSections,
            packageId,
            "trait",
            "trait-dispatch-witness.json",
            "trait-dispatch-world.json",
            "trait-dispatch-wire.json",
            "trait-replayed-wire.json",
            "trait-restored-pending.json");
        CopyLivePlan(campaignRoot, staging, campaign);
        JObject selection = new()
        {
            ["schema"] = "dungeonstory-selection-source-v9@1",
            ["sourcePackageId"] = packageId,
            ["sourceTier"] = "controlled-campaign",
            ["selections"] = new JArray(skill, trait),
            ["responseDataIncluded"] = false,
            ["networkSent"] = false,
            ["responseApplied"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };

        JObject mechanicsDescriptor = WriteManifest(
            staging,
            "mechanics.json",
            mechanics);
        JObject campaignDescriptor = WriteManifest(
            staging,
            "executed-campaign.json",
            campaign);
        JObject selectionDescriptor = WriteManifest(
            staging,
            "selection-source.json",
            selection);
        JObject manifest = new()
        {
            ["schema"] = "dungeonstory-gameplay-module-selection-source@9",
            ["sourcePackageId"] = packageId,
            ["sourceTier"] = "controlled-campaign",
            ["mechanics"] = mechanicsDescriptor,
            ["executedCampaign"] = campaignDescriptor,
            ["selectionSource"] = selectionDescriptor,
            ["selectionCount"] = 2,
            ["integrationFixture"] = false,
            ["naturalPlayClaimed"] = false,
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["dpoPerformed"] = false,
            ["releaseReady"] = false
        };
        JObject manifestDescriptor = WriteManifest(staging, "manifest.json", manifest);
        MoveDirectoryWithRetry(staging, final);
        return new JObject
        {
            ["outputDirectory"] = final.Replace('\\', '/'),
            ["selectionCount"] = 2,
            ["manifestSha256"] = (string)manifestDescriptor["sha256"],
            ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false
        };
    }

    internal static JObject ExportEquipment(
        string campaignOutput,
        int registeredSections)
        => ExportPair(campaignOutput, registeredSections, ExportEquipmentOnce);

    private static JObject ExportEquipmentOnce(
        string campaignOutput, int registeredSections, string directoryName)
    {
        if (registeredSections <= 0)
            throw new ArgumentOutOfRangeException(nameof(registeredSections));
        string projectRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            ".."));
        string campaignRoot = RequireDirectoryInside(
            projectRoot,
            campaignOutput,
            Path.Combine(projectRoot, "Artifacts", "QA"),
            "campaign output");
        string inventoryPath = RequireFileInside(
            projectRoot,
            RequireEnvironment(InventoryEnvironment),
            "source inventory");
        string mechanicsManifestPath = RequireFileInside(
            projectRoot,
            RequireEnvironment(MechanicsManifestEnvironment),
            "B1 mechanics manifest");
        string packageId = RequirePackageId(
            RequireEnvironment(PackageIdEnvironment));
        string staging = Path.Combine(campaignRoot, directoryName + "-staging");
        string final = Path.Combine(campaignRoot, directoryName);
        if (Directory.Exists(staging)
            || File.Exists(staging)
            || Directory.Exists(final)
            || File.Exists(final))
        {
            throw new InvalidOperationException(
                "Source-v9 intake output is create-only.");
        }

        Directory.CreateDirectory(staging);
        JObject inventory = ReadObject(inventoryPath);
        RequireString(inventory, "gameCommit");
        RequireSha(inventory, "dirtySourceSha256");
        RequireSha(inventory, "contentSha256");
        if (inventory["files"] is not JArray inventoryFiles
            || inventoryFiles.Count == 0)
        {
            throw new InvalidOperationException("Source inventory has no files.");
        }
        JObject b1Manifest = ReadObject(mechanicsManifestPath);
        if ((bool?)b1Manifest["trainingEligible"] != false
            || (bool?)b1Manifest["humanApprovalClaimed"] != false)
        {
            throw new InvalidOperationException(
                "B1 mechanics input must remain review-only.");
        }

        JObject inventoryDescriptor = CopyInput(
            inventoryPath,
            staging,
            "inputs/source-inventory.json");
        JObject b1Descriptor = CopyInput(
            mechanicsManifestPath,
            staging,
            "inputs/b1-evidence-manifest.json");
        JObject mechanics = new()
        {
            ["schema"] = "dungeonstory-mechanics-source-v9@1",
            ["sourcePackageId"] = packageId,
            ["gameCommit"] = (string)inventory["gameCommit"],
            ["dirtySourceSha256"] = (string)inventory["dirtySourceSha256"],
            ["contentSha256"] = (string)inventory["contentSha256"],
            ["sourceInventory"] = inventoryDescriptor,
            ["b1EvidenceManifest"] = b1Descriptor,
            ["codeIdentity"] = new JArray(CodeIdentityFiles.Select(path =>
                ProjectFileDescriptor(projectRoot, path))),
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };

        JObject command = ReadObject(Path.Combine(
            campaignRoot,
            "equipment-command-witness.json"));
        if ((bool?)command["publicCommandIssued"] != true
            || (bool?)command["fabricatedExperience"] != false
            || (bool?)command["fabricatedOutcome"] != false
            || (bool?)command["naturalPlayClaimed"] != false
            || (int?)command["committedAttackOutcomes"] < 30
            || (int?)command["affinityAfter"] < 30
            || command["outcomes"] is not JArray outcomes
            || outcomes.Count < 30)
        {
            throw new InvalidOperationException(
                "Equipment command witness is not an actual threshold trigger.");
        }
        var rootOperations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (JObject outcome in outcomes.OfType<JObject>())
        {
            rootOperations.Add(
                RequireString(outcome, "runId")
                + "/"
                + ((int?)outcome["sequence"]
                    ?? throw new InvalidOperationException(
                        "Equipment outcome sequence is missing.")));
        }
        string[] campaignNames =
        {
            "equipment-continuation-witness.json",
            "equipment-continuation-restored-world.json",
            "equipment-pre-dispatch-world.json",
            "equipment-command-witness.json",
            "equipment-dispatch-world.json",
            "equipment-restored-world.json"
        };
        var campaignFiles = new JArray(campaignNames.Select(name =>
            CopyCampaign(
                campaignRoot,
                staging,
                name,
                "campaign/" + name)));
        JObject campaign = new()
        {
            ["schema"] = "dungeonstory-executed-campaign-v9@1",
            ["sourcePackageId"] = packageId,
            ["sourceTier"] = "controlled-campaign",
            ["lineage"] = new JObject
            {
                ["campaignInstanceId"] = packageId + ":campaign",
                ["runId"] = packageId + ":run:equipment",
                ["branchId"] = "actual-force-fire-to-equipment-pending",
                ["planTemplateFamily"] = "controlled-public-combat-sequence",
                ["planHash"] = GameplayModuleSelectionCanonicalJson.Hash(
                    new JObject
                    {
                        ["command"] = RequireString(command, "command"),
                        ["actorId"] = RequireString(command, "actorId"),
                        ["targetId"] = RequireString(command, "targetId"),
                        ["equipmentInstanceId"] = RequireString(
                            command,
                            "equipmentInstanceId")
                    }),
                ["rootOperations"] = new JArray(rootOperations)
            },
            ["controlledSetup"] = new JObject
            {
                ["authoredEquipmentFixture"] = (bool?)command[
                    "controlledEquipmentFixture"] == true,
                ["durableTargetFixture"] = (bool?)command[
                    "controlledDurableTargetFixture"] == true,
                ["externalAmmunitionLoads"] = (int?)command[
                    "externalAmmunitionLoads"] ?? 0
            },
            ["files"] = campaignFiles,
            ["fabricatedOutcome"] = false,
            ["naturalPlayClaimed"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };

        JObject equipment = BuildEquipmentSelection(
            campaignRoot,
            staging,
            registeredSections,
            packageId);
        CopyLivePlan(campaignRoot, staging, campaign);
        JObject selection = new()
        {
            ["schema"] = "dungeonstory-selection-source-v9@1",
            ["sourcePackageId"] = packageId,
            ["sourceTier"] = "controlled-campaign",
            ["selections"] = new JArray(equipment),
            ["responseDataIncluded"] = false,
            ["networkSent"] = false,
            ["responseApplied"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };
        JObject mechanicsDescriptor = WriteManifest(
            staging,
            "mechanics.json",
            mechanics);
        JObject campaignDescriptor = WriteManifest(
            staging,
            "executed-campaign.json",
            campaign);
        JObject selectionDescriptor = WriteManifest(
            staging,
            "selection-source.json",
            selection);
        JObject manifest = new()
        {
            ["schema"] = "dungeonstory-gameplay-module-selection-source@9",
            ["sourcePackageId"] = packageId,
            ["sourceTier"] = "controlled-campaign",
            ["mechanics"] = mechanicsDescriptor,
            ["executedCampaign"] = campaignDescriptor,
            ["selectionSource"] = selectionDescriptor,
            ["selectionCount"] = 1,
            ["integrationFixture"] = false,
            ["naturalPlayClaimed"] = false,
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["dpoPerformed"] = false,
            ["releaseReady"] = false
        };
        JObject manifestDescriptor = WriteManifest(
            staging,
            "manifest.json",
            manifest);
        MoveDirectoryWithRetry(staging, final);
        return new JObject
        {
            ["outputDirectory"] = final.Replace('\\', '/'),
            ["selectionCount"] = 1,
            ["manifestSha256"] = (string)manifestDescriptor["sha256"],
            ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false
        };
    }

    internal static JObject ExportFacility(
        string campaignOutput,
        int registeredSections)
        => ExportPair(campaignOutput, registeredSections, ExportFacilityOnce);

    private static JObject ExportFacilityOnce(
        string campaignOutput, int registeredSections, string directoryName)
    {
        if (registeredSections <= 0)
            throw new ArgumentOutOfRangeException(nameof(registeredSections));
        string projectRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            ".."));
        string campaignRoot = RequireDirectoryInside(
            projectRoot,
            campaignOutput,
            Path.Combine(projectRoot, "Artifacts", "QA"),
            "campaign output");
        string inventoryPath = RequireFileInside(
            projectRoot,
            RequireEnvironment(InventoryEnvironment),
            "source inventory");
        string mechanicsManifestPath = RequireFileInside(
            projectRoot,
            RequireEnvironment(MechanicsManifestEnvironment),
            "B1 mechanics manifest");
        string packageId = RequirePackageId(
            RequireEnvironment(PackageIdEnvironment));
        string staging = Path.Combine(campaignRoot, directoryName + "-staging");
        string final = Path.Combine(campaignRoot, directoryName);
        if (Directory.Exists(staging)
            || File.Exists(staging)
            || Directory.Exists(final)
            || File.Exists(final))
        {
            throw new InvalidOperationException(
                "Source-v9 intake output is create-only.");
        }

        Directory.CreateDirectory(staging);
        JObject inventory = ReadObject(inventoryPath);
        RequireString(inventory, "gameCommit");
        RequireSha(inventory, "dirtySourceSha256");
        RequireSha(inventory, "contentSha256");
        if (inventory["files"] is not JArray inventoryFiles
            || inventoryFiles.Count == 0)
        {
            throw new InvalidOperationException("Source inventory has no files.");
        }
        JObject b1Manifest = ReadObject(mechanicsManifestPath);
        if ((bool?)b1Manifest["trainingEligible"] != false
            || (bool?)b1Manifest["humanApprovalClaimed"] != false)
        {
            throw new InvalidOperationException(
                "B1 mechanics input must remain review-only.");
        }

        JObject inventoryDescriptor = CopyInput(
            inventoryPath,
            staging,
            "inputs/source-inventory.json");
        JObject b1Descriptor = CopyInput(
            mechanicsManifestPath,
            staging,
            "inputs/b1-evidence-manifest.json");
        JObject mechanics = new()
        {
            ["schema"] = "dungeonstory-mechanics-source-v9@1",
            ["sourcePackageId"] = packageId,
            ["gameCommit"] = (string)inventory["gameCommit"],
            ["dirtySourceSha256"] = (string)inventory["dirtySourceSha256"],
            ["contentSha256"] = (string)inventory["contentSha256"],
            ["sourceInventory"] = inventoryDescriptor,
            ["b1EvidenceManifest"] = b1Descriptor,
            ["codeIdentity"] = new JArray(CodeIdentityFiles.Select(path =>
                ProjectFileDescriptor(projectRoot, path))),
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };

        string[] commandNames = Directory.GetFiles(campaignRoot, "command-*.json")
            .Select(Path.GetFileName)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (commandNames.Length == 0)
            throw new InvalidOperationException(
                "Facility source requires an actual gameplay command.");
        var commandIntents = new JArray();
        var rootOperations = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string name in commandNames)
        {
            JObject command = ReadObject(Path.Combine(campaignRoot, name));
            if (command["publishedOutcomes"] is not JArray outcomes
                || outcomes.Count == 0)
            {
                throw new InvalidOperationException(
                    "Facility command has no outcomes: " + name);
            }
            commandIntents.Add(new JObject
            {
                ["command"] = RequireString(command, "command"),
                ["actorId"] = RequireString(command, "actorId"),
                ["facilityId"] = RequireString(command, "facilityId")
            });
            foreach (JObject outcome in outcomes.OfType<JObject>())
            {
                rootOperations.Add(
                    RequireString(outcome, "runId")
                    + "/"
                    + ((int?)outcome["sequence"]
                        ?? throw new InvalidOperationException(
                            "Facility outcome sequence is missing.")));
            }
        }
        JObject witness = ReadObject(Path.Combine(
            campaignRoot,
            "facility-dispatch-witness.json"));
        if (witness["evidence"] is not JArray witnessEvidence)
        {
            throw new InvalidOperationException(
                "Facility dispatch outcome evidence must be an array.");
        }
        var evidenceOperations = witnessEvidence.OfType<JObject>()
            .Select(value => RequireString(value, "runId")
                + "/"
                + ((int?)value["sequence"]
                    ?? throw new InvalidOperationException(
                        "Facility evidence sequence is missing.")))
            .ToHashSet(StringComparer.Ordinal);
        var campaignNames = new List<string>
        {
            "facility-continuation-witness.json",
            "facility-continuation-restored-world.json",
            "facility-candidates.json",
            "facility-pre-command-world.json",
            "facility-dispatch-world.json",
            "facility-restored-world.json"
        };
        campaignNames.AddRange(commandNames);
        foreach (string optional in new[]
        {
            "facility-candidate-after-construction.json",
            "facility-construction-command.json",
            "facility-construction-result.json",
            "facility-construction-after-world.json",
            "facility-trade-command.json",
            "facility-trade-result.json",
            "facility-trade-after-world.json",
            "facility-visitor-world.json"
        })
        {
            if (File.Exists(Path.Combine(campaignRoot, optional)))
                campaignNames.Add(optional);
        }
        var campaignFiles = new JArray(campaignNames
            .Distinct(StringComparer.Ordinal)
            .Select(name => CopyCampaign(
                campaignRoot,
                staging,
                name,
                "campaign/" + name)));
        JObject campaign = new()
        {
            ["schema"] = "dungeonstory-executed-campaign-v9@1",
            ["sourcePackageId"] = packageId,
            ["sourceTier"] = "controlled-campaign",
            ["lineage"] = new JObject
            {
                ["campaignInstanceId"] = packageId + ":campaign",
                ["runId"] = packageId + ":run:facility",
                ["branchId"] = "actual-facility-use-to-facility-pending",
                ["planTemplateFamily"] = "controlled-public-facility-sequence",
                ["planHash"] = GameplayModuleSelectionCanonicalJson.Hash(
                    commandIntents),
                ["rootOperations"] = new JArray(rootOperations)
            },
            ["commandIntentsNonAuthoritative"] = commandIntents,
            ["files"] = campaignFiles,
            ["fabricatedOutcome"] = false,
            ["naturalPlayClaimed"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };

        JObject facility = BuildFacilitySelection(
            campaignRoot,
            staging,
            registeredSections,
            packageId);
        // BuildFacilitySelection independently validates every usage link
        // against the copied command and official world. A nonempty sidecar
        // alone cannot bypass the exact-outcome route's overlap check.
        if (!evidenceOperations.Overlaps(rootOperations)
            && (facility["facilityUsageSources"] as JArray)?.Count is not > 0)
            throw new InvalidOperationException(
                "Facility request has no selected origin in the actual commands.");
        CopyLivePlan(campaignRoot, staging, campaign);
        JObject selection = new()
        {
            ["schema"] = "dungeonstory-selection-source-v9@1",
            ["sourcePackageId"] = packageId,
            ["sourceTier"] = "controlled-campaign",
            ["selections"] = new JArray(facility),
            ["responseDataIncluded"] = false,
            ["networkSent"] = false,
            ["responseApplied"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };
        JObject mechanicsDescriptor = WriteManifest(
            staging,
            "mechanics.json",
            mechanics);
        JObject campaignDescriptor = WriteManifest(
            staging,
            "executed-campaign.json",
            campaign);
        JObject selectionDescriptor = WriteManifest(
            staging,
            "selection-source.json",
            selection);
        JObject manifest = new()
        {
            ["schema"] = "dungeonstory-gameplay-module-selection-source@9",
            ["sourcePackageId"] = packageId,
            ["sourceTier"] = "controlled-campaign",
            ["mechanics"] = mechanicsDescriptor,
            ["executedCampaign"] = campaignDescriptor,
            ["selectionSource"] = selectionDescriptor,
            ["selectionCount"] = 1,
            ["integrationFixture"] = false,
            ["naturalPlayClaimed"] = false,
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false,
            ["dpoPerformed"] = false,
            ["releaseReady"] = false
        };
        JObject manifestDescriptor = WriteManifest(
            staging,
            "manifest.json",
            manifest);
        MoveDirectoryWithRetry(staging, final);
        return new JObject
        {
            ["outputDirectory"] = final.Replace('\\', '/'),
            ["selectionCount"] = 1,
            ["manifestSha256"] = (string)manifestDescriptor["sha256"],
            ["trainingEligible"] = false,
            ["humanApprovalClaimed"] = false
        };
    }

    private static JObject ExportPair(
        string campaignOutput,
        int registeredSections,
        Func<string, int, string, JObject> exportOnce)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string campaignRoot = RequireDirectoryInside(projectRoot, campaignOutput,
            Path.Combine(projectRoot, "Artifacts", "QA"), "campaign output");
        string[] externalInputs =
        {
            RequireFileInside(projectRoot, RequireEnvironment(InventoryEnvironment), "source inventory"),
            RequireFileInside(projectRoot, RequireEnvironment(MechanicsManifestEnvironment), "B1 mechanics manifest")
        };
        return ExportPairCore(campaignRoot,
            name => exportOnce(campaignRoot, registeredSections, name),
            () => new JArray(Directory.GetFiles(campaignRoot, "*", SearchOption.TopDirectoryOnly)
                .Concat(externalInputs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => Descriptor(Path.GetRelativePath(projectRoot, path), File.ReadAllBytes(path)))));
    }

    private static JObject ExportPairCore(
        string campaignRoot,
        Func<string, JObject> exportOnce,
        Func<JArray> captureInputs)
    {
        foreach (string name in new[] { FinalDirectoryName, StagingDirectoryName,
                     RepeatDirectoryName, RepeatDirectoryName + "-staging", DeterminismReportName })
        {
            string path = Path.Combine(campaignRoot, name);
            if (File.Exists(path) || Directory.Exists(path))
                throw new InvalidOperationException("Source-v9 pair output is create-only: " + name);
        }

        JArray inputs = captureInputs();
        JObject first = exportOnce(FinalDirectoryName);
        exportOnce(RepeatDirectoryName);
        if (!JToken.DeepEquals(inputs, captureInputs()))
            throw new InvalidOperationException("Captured inputs changed during source-v9 export.");
        JArray files = ExportGraph(Path.Combine(campaignRoot, FinalDirectoryName));
        JArray repeat = ExportGraph(Path.Combine(campaignRoot, RepeatDirectoryName));
        if (files.Count == 0 || !JToken.DeepEquals(files, repeat))
            throw new InvalidOperationException("Source-v9 repeated export file graph differs.");

        // This external proof is not part of either deterministic package.
        // A failure leaves both outputs intact, and the campaign caller reports failure.
        first["determinismProof"] = WriteManifest(campaignRoot, DeterminismReportName,
            new JObject
            {
                ["schema"] = "dungeonstory-source-v9-determinism@1",
                ["passed"] = true,
                ["sameCapturedInput"] = true,
                ["exportInvocations"] = 2,
                ["primaryDirectory"] = FinalDirectoryName,
                ["repeatDirectory"] = RepeatDirectoryName,
                ["inputs"] = inputs,
                ["files"] = files,
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            });
        return first;
    }

    private static JArray ExportGraph(string directory) => new(
        Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
            .Select(path => Descriptor(Path.GetRelativePath(directory, path), File.ReadAllBytes(path)))
            .OrderBy(value => (string)value["path"], StringComparer.Ordinal));

    public static void RunDeterminismContractBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Source-v9 contract requires isolated batch mode.");
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string configuredOutput = Path.GetFullPath(
            RequireEnvironment("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT"));
        string outputParent = RequireDirectoryInside(projectRoot,
            Path.GetDirectoryName(configuredOutput),
            Path.Combine(projectRoot, "Artifacts", "QA"), "contract output parent");
        if (!string.Equals(configuredOutput, Path.Combine(outputParent, "unity-evidence"),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Contract requires the isolated launcher's evidence directory.");
        Directory.CreateDirectory(configuredOutput);
        string output = RequireDirectoryInside(projectRoot, configuredOutput,
            Path.Combine(projectRoot, "Artifacts", "QA"), "contract output");
        string tests = Path.Combine(output, "source-v9-determinism-contract");
        if (Directory.Exists(tests) || File.Exists(tests))
            throw new InvalidOperationException("Source-v9 contract output is create-only.");

        int successfulInvocations = 0;
        foreach (string mode in new[] { "same", "changed-output", "missing-output", "changed-input" })
        {
            string directory = Path.Combine(tests, mode);
            string input = Path.Combine(directory, "input.json");
            WriteNew(input, Encoding.UTF8.GetBytes("{\"value\":1}\n"));
            int invocations = 0;
            Func<JArray> capture = () => new JArray(Descriptor("input.json", File.ReadAllBytes(input)));
            Func<string, JObject> write = name =>
            {
                ++invocations;
                string destination = Path.Combine(directory, name);
                Directory.CreateDirectory(destination);
                if (mode != "missing-output" || invocations == 1)
                    WriteManifest(destination, "manifest.json", new JObject
                    {
                        ["fixtureValue"] = mode == "changed-output" ? invocations : 1,
                        ["integrationFixture"] = true
                    });
                if (mode == "changed-input" && invocations == 2)
                    WriteNew(Path.Combine(directory, "new-input.json"), new byte[] { 1 });
                return new JObject { ["outputDirectory"] = destination };
            };
            if (mode == "changed-input")
                capture = () => new JArray(Directory.GetFiles(directory, "*.json")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => Descriptor(Path.GetFileName(path), File.ReadAllBytes(path))));
            if (mode == "same")
            {
                ExportPairCore(directory, write, capture);
                if (invocations != 2)
                    throw new InvalidOperationException("Contract did not invoke the exporter twice.");
                ExpectExportFailure(() => ExportPairCore(directory, write, capture), "create-only");
                if (invocations != 2)
                    throw new InvalidOperationException("Duplicate export changed existing outputs.");
                successfulInvocations = invocations;
            }
            else
                ExpectExportFailure(() => ExportPairCore(directory, write, capture),
                    mode == "changed-input" ? "Captured inputs changed" : "file graph differs");
        }
        WriteManifest(output, "source-v9-determinism-contract-report.json", new JObject
        {
            ["passed"] = true,
            ["successfulExportInvocations"] = successfulInvocations,
            ["changedBytesRejected"] = true,
            ["missingFileRejected"] = true,
            ["changedInputRejected"] = true,
            ["duplicateOutputRejectedBeforeWriting"] = true,
            ["integrationFixture"] = true,
            ["actualProfilePackagesExecuted"] = false,
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        });
        Debug.Log("SOURCE_V9_DETERMINISM_CONTRACT=PASS");
    }

    private static void ExpectExportFailure(Action action, string expected)
    {
        try { action(); }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains(expected, StringComparison.Ordinal)) { return; }
        throw new InvalidOperationException("Expected export failure was not observed: " + expected);
    }

    private static JObject BuildEquipmentSelection(
        string campaignRoot,
        string staging,
        int registeredSections,
        string packageId)
    {
        const string label = "equipment";
        JObject witness = ReadObject(Path.Combine(
            campaignRoot,
            "equipment-dispatch-witness.json"));
        JObject pending = witness["pending"] as JObject
            ?? throw new InvalidOperationException(
                "Equipment witness pending is missing.");
        JObject node = witness["node"] as JObject
            ?? throw new InvalidOperationException(
                "Equipment witness node is missing.");
        JObject restoredPending = ReadObject(Path.Combine(
            campaignRoot,
            "equipment-restored-pending.json"));
        if (!JToken.DeepEquals(pending, restoredPending))
            throw new InvalidOperationException(
                "Equipment restored pending differs.");
        byte[] worldBytes = File.ReadAllBytes(Path.Combine(
            campaignRoot,
            "equipment-dispatch-world.json"));
        byte[] wireBytes = File.ReadAllBytes(Path.Combine(
            campaignRoot,
            "equipment-dispatch-wire.json"));
        byte[] replayWireBytes = File.ReadAllBytes(Path.Combine(
            campaignRoot,
            "equipment-replayed-wire.json"));
        if (!wireBytes.SequenceEqual(replayWireBytes))
            throw new InvalidOperationException(
                "Equipment replay wire differs.");
        RequireUnprefixedHash(witness, "checkpointSha256", worldBytes);
        RequireUnprefixedHash(witness, "wireSha256", wireBytes);
        JObject world = ParseObject(worldBytes, "equipment world");
        if (world["sections"] is not JArray sections
            || sections.Count != registeredSections
            || world["manifest"] is not JObject manifest
            || manifest["sections"] is not JArray manifestSections
            || manifestSections.Count != registeredSections)
        {
            throw new InvalidOperationException(
                "Equipment checkpoint is not the complete registered world.");
        }
        if ((bool?)witness["captureOnly"] != true
            || (bool?)witness["networkSent"] != false
            || (bool?)witness["responseApplied"] != false
            || witness["evidence"] is not JArray evidence
            || evidence.Count == 0)
        {
            throw new InvalidOperationException(
                "Equipment witness flags or evidence differ.");
        }
        JArray offers = node["moduleSelectionOffers"] as JArray
            ?? throw new InvalidOperationException(
                "Equipment module offers are missing.");
        string selectionId = RequireString(pending, "presentationId");
        if (offers.Count < 2
            || !string.Equals(
                selectionId,
                RequireString(node, "moduleSelectionId"),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Equipment has no actual candidate choice.");
        }

        string destination = "selection/" + label + "/";
        JObject witnessFile = CopyCampaign(
            campaignRoot,
            staging,
            "equipment-dispatch-witness.json",
            destination + "witness.json");
        JObject worldFile = CopyCampaign(
            campaignRoot,
            staging,
            "equipment-dispatch-world.json",
            destination + "world.json");
        JObject wireFile = CopyCampaign(
            campaignRoot,
            staging,
            "equipment-dispatch-wire.json",
            destination + "wire.json");
        JObject replayWireFile = CopyCampaign(
            campaignRoot,
            staging,
            "equipment-replayed-wire.json",
            destination + "replayed-wire.json");
        JObject pendingFile = CopyCampaign(
            campaignRoot,
            staging,
            "equipment-restored-pending.json",
            destination + "pending.json");
        string equipmentInstanceId = RequireString(
            witness,
            "equipmentInstanceId");
        string ownerId = RequireString(witness, "ownerId");
        return new JObject
        {
            ["selectionSourceId"] = packageId + ":selection:equipment",
            ["worldLineage"] = ExtractWorldLineage(world),
            ["profileId"] = RequireString(witness, "profileId"),
            ["targetId"] = equipmentInstanceId,
            ["ownerId"] = ownerId,
            ["dispatchEligibility"] = GameplayModuleDispatchEligibilityCapture.RequireForExport(
                witness, ownerId, equipmentInstanceId, selectionId),
            ["ownerBinding"] = new JObject
            {
                ["equipmentInstanceId"] = equipmentInstanceId,
                ["attunementOwnerPersistentId"] = RequireString(
                    pending,
                    "attunementOwnerPersistentId"),
                ["nodeId"] = RequireString(pending, "nodeId"),
                ["presentationId"] = selectionId
            },
            ["triggerId"] = selectionId,
            ["correlationId"] = RequireString(witness, "correlationId"),
            ["absoluteHour"] = (long?)witness["absoluteHour"]
                ?? throw new InvalidOperationException(
                    "Equipment clock is missing."),
            ["schemaVersion"] = (int?)witness["schemaVersion"]
                ?? throw new InvalidOperationException(
                    "Equipment schema version is missing."),
            ["schemaHash"] = RequireString(witness, "schemaHash"),
            ["candidateCount"] = offers.Count,
            ["selectionIds"] = new JArray(offers.OfType<JObject>()
                .Select(value => RequireString(value, "moduleId"))),
            ["capture"] = new JObject
            {
                ["officialWorldCheckpoint"] = worldFile,
                ["pending"] = pendingFile,
                ["witness"] = witnessFile
            },
            ["wire"] = new JObject
            {
                ["original"] = wireFile,
                ["restoredReplay"] = replayWireFile,
                ["byteExactReplay"] = true
            },
            ["evidenceSidecar"] = witnessFile,
            ["responseDataIncluded"] = false
        };
    }

    private static JObject BuildFacilitySelection(
        string campaignRoot,
        string staging,
        int registeredSections,
        string packageId)
    {
        JObject witness = ReadObject(Path.Combine(
            campaignRoot,
            "facility-dispatch-witness.json"));
        JObject pending = witness["pending"] as JObject
            ?? throw new InvalidOperationException(
                "Facility witness pending is missing.");
        JObject node = pending["node"] as JObject
            ?? throw new InvalidOperationException(
                "Facility witness node is missing.");
        JObject restoredPending = ReadObject(Path.Combine(
            campaignRoot,
            "facility-restored-pending.json"));
        if (!JToken.DeepEquals(pending, restoredPending))
            throw new InvalidOperationException(
                "Facility restored pending differs.");
        byte[] worldBytes = File.ReadAllBytes(Path.Combine(
            campaignRoot,
            "facility-dispatch-world.json"));
        byte[] wireBytes = File.ReadAllBytes(Path.Combine(
            campaignRoot,
            "facility-dispatch-wire.json"));
        byte[] replayWireBytes = File.ReadAllBytes(Path.Combine(
            campaignRoot,
            "facility-replayed-wire.json"));
        if (!wireBytes.SequenceEqual(replayWireBytes))
            throw new InvalidOperationException(
                "Facility replay wire differs.");
        RequireUnprefixedHash(witness, "checkpointSha256", worldBytes);
        RequireUnprefixedHash(witness, "wireSha256", wireBytes);
        JObject world = ParseObject(worldBytes, "facility world");
        if (world["sections"] is not JArray sections
            || sections.Count != registeredSections
            || world["manifest"] is not JObject manifest
            || manifest["sections"] is not JArray manifestSections
            || manifestSections.Count != registeredSections)
        {
            throw new InvalidOperationException(
                "Facility checkpoint is not the complete registered world.");
        }
        JArray facilityUsageSources = ValidateFacilityUsageEvidence(campaignRoot, staging, witness, pending, world);
        if ((bool?)witness["captureOnly"] != true
            || (bool?)witness["networkSent"] != false
            || (bool?)witness["responseApplied"] != false
            || witness["evidence"] is not JArray evidence
            || (evidence.Count == 0 && facilityUsageSources.Count == 0))
        {
            throw new InvalidOperationException(
                "Facility witness flags or evidence differ.");
        }
        JArray offers = node["moduleSelectionOffers"] as JArray
            ?? throw new InvalidOperationException(
                "Facility module offers are missing.");
        string selectionId = RequireString(pending, "presentationId");
        if (offers.Count == 0
            || !string.Equals(
                selectionId,
                RequireString(node, "moduleSelectionId"),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Facility has no actual legal candidate or its selection identity differs.");
        }

        const string destination = "selection/facility/";
        JObject witnessFile = CopyCampaign(
            campaignRoot,
            staging,
            "facility-dispatch-witness.json",
            destination + "witness.json");
        JObject worldFile = CopyCampaign(
            campaignRoot,
            staging,
            "facility-dispatch-world.json",
            destination + "world.json");
        JObject wireFile = CopyCampaign(
            campaignRoot,
            staging,
            "facility-dispatch-wire.json",
            destination + "wire.json");
        JObject replayWireFile = CopyCampaign(
            campaignRoot,
            staging,
            "facility-replayed-wire.json",
            destination + "replayed-wire.json");
        JObject pendingFile = CopyCampaign(
            campaignRoot,
            staging,
            "facility-restored-pending.json",
            destination + "pending.json");
        string facilityId = RequireString(witness, "facilityId");
        var result = new JObject
        {
            ["selectionSourceId"] = packageId + ":selection:facility",
            ["worldLineage"] = ExtractWorldLineage(world),
            ["profileId"] = RequireString(witness, "profileId"),
            ["targetId"] = facilityId,
            ["ownerId"] = facilityId,
            ["dispatchEligibility"] = GameplayModuleDispatchEligibilityCapture.RequireForExport(
                witness, facilityId, facilityId, selectionId),
            ["ownerBinding"] = new JObject
            {
                ["sourceFacilityPersistentId"] = RequireString(
                    pending,
                    "sourceFacilityPersistentId"),
                ["recipeId"] = RequireString(pending, "recipeId"),
                ["nodeId"] = RequireString(node, "nodeId"),
                ["presentationId"] = selectionId
            },
            ["triggerId"] = selectionId,
            ["correlationId"] = RequireString(witness, "correlationId"),
            ["absoluteHour"] = (long?)witness["absoluteHour"]
                ?? throw new InvalidOperationException(
                    "Facility clock is missing."),
            ["schemaVersion"] = (int?)witness["schemaVersion"]
                ?? throw new InvalidOperationException(
                    "Facility schema version is missing."),
            ["schemaHash"] = RequireString(witness, "schemaHash"),
            ["candidateCount"] = offers.Count,
            ["selectionIds"] = new JArray(offers.OfType<JObject>()
                .Select(value => RequireString(value, "moduleId"))),
            ["capture"] = new JObject
            {
                ["officialWorldCheckpoint"] = worldFile,
                ["pending"] = pendingFile,
                ["witness"] = witnessFile
            },
            ["wire"] = new JObject
            {
                ["original"] = wireFile,
                ["restoredReplay"] = replayWireFile,
                ["byteExactReplay"] = true
            },
            ["evidenceSidecar"] = witnessFile,
            ["responseDataIncluded"] = false
        };
        result["hasAlternativeModules"] = offers.Count > 1;
        if (facilityUsageSources.Count > 0) result["facilityUsageSources"] = facilityUsageSources;
        return result;
    }

    private static JObject BuildSelection(
        string campaignRoot,
        string staging,
        int registeredSections,
        string packageId,
        string label,
        string witnessName,
        string worldName,
        string wireName,
        string replayWireName,
        string restoredPendingName)
    {
        JObject witness = ReadObject(Path.Combine(campaignRoot, witnessName));
        JObject pending = witness["pending"] as JObject
            ?? throw new InvalidOperationException(label + " witness pending is missing.");
        JObject restoredPending = ReadObject(Path.Combine(
            campaignRoot,
            restoredPendingName));
        if (!JToken.DeepEquals(pending, restoredPending))
            throw new InvalidOperationException(label + " restored pending differs.");
        byte[] worldBytes = File.ReadAllBytes(Path.Combine(campaignRoot, worldName));
        byte[] wireBytes = File.ReadAllBytes(Path.Combine(campaignRoot, wireName));
        byte[] replayWireBytes = File.ReadAllBytes(Path.Combine(campaignRoot, replayWireName));
        if (!wireBytes.SequenceEqual(replayWireBytes))
            throw new InvalidOperationException(label + " replay wire differs.");
        RequireUnprefixedHash(witness, "checkpointSha256", worldBytes);
        RequireUnprefixedHash(witness, "wireSha256", wireBytes);
        JObject world = ParseObject(worldBytes, label + " world");
        if (world["sections"] is not JArray sections
            || sections.Count != registeredSections
            || world["manifest"] is not JObject manifest
            || manifest["sections"] is not JArray manifestSections
            || manifestSections.Count != registeredSections)
        {
            throw new InvalidOperationException(
                label + " checkpoint is not the complete registered world.");
        }
        JArray aggregateSources = ValidateTraitAggregateEvidence(campaignRoot, staging, label, witness, pending, world);
        if ((bool?)witness["captureOnly"] != true
            || (bool?)witness["networkSent"] != false
            || (bool?)witness["responseApplied"] != false
            || witness["evidence"] is not JArray evidence
            || (evidence.Count == 0 && aggregateSources.Count == 0))
        {
            throw new InvalidOperationException(label + " witness flags/evidence differ.");
        }

        string destination = "selection/" + label + "/";
        JObject witnessFile = CopyCampaign(campaignRoot, staging, witnessName,
            destination + "witness.json");
        JObject worldFile = CopyCampaign(campaignRoot, staging, worldName,
            destination + "world.json");
        JObject wireFile = CopyCampaign(campaignRoot, staging, wireName,
            destination + "wire.json");
        JObject replayWireFile = CopyCampaign(campaignRoot, staging, replayWireName,
            destination + "replayed-wire.json");
        JObject pendingFile = CopyCampaign(campaignRoot, staging, restoredPendingName,
            destination + "pending.json");

        JArray selectionIds;
        int candidateCount;
        string triggerId;
        if (label == "skill")
        {
            triggerId = RequireString(pending, "requestKey");
            JArray offers = pending["moduleSelectionOffers"] as JArray
                ?? throw new InvalidOperationException("Skill offers are missing.");
            selectionIds = new JArray(offers.OfType<JObject>()
                .Select(value => RequireString(value, "selectionId")));
            candidateCount = offers.Count;
        }
        else
        {
            triggerId = RequireString(pending, "requestId");
            string selectionId = RequireString(pending, "moduleSelectionId");
            JArray offers = pending["moduleOffers"] as JArray
                ?? throw new InvalidOperationException("Trait offers are missing.");
            selectionIds = new JArray(selectionId);
            candidateCount = offers.Count;
        }
        if (candidateCount < 2 || selectionIds.Count == 0)
            throw new InvalidOperationException(label + " has no actual candidate choice.");

        JObject result = new JObject
        {
            ["selectionSourceId"] = packageId + ":selection:" + label,
            ["worldLineage"] = ExtractWorldLineage(world),
            ["triggerOrigin"] = label == "trait"
                && File.Exists(Path.Combine(campaignRoot, "resumed-trait-pending.json"))
                ? "restored-existing-authoritative-request" : "actual-production-command",
            ["profileId"] = RequireString(witness, "profileId"),
            ["targetId"] = RequireString(witness, "ownerId"),
            ["ownerId"] = RequireString(witness, "ownerId"),
            ["dispatchEligibility"] = GameplayModuleDispatchEligibilityCapture.RequireForExport(
                witness, RequireString(witness, "ownerId"), RequireString(witness, "ownerId"), triggerId),
            ["ownerBinding"] = pending["ownerBinding"]?.DeepClone()
                ?? throw new InvalidOperationException(label + " owner binding is missing."),
            ["triggerId"] = triggerId,
            ["correlationId"] = RequireString(witness, "correlationId"),
            ["absoluteHour"] = (long?)witness["absoluteHour"]
                ?? throw new InvalidOperationException(label + " clock is missing."),
            ["schemaVersion"] = (int?)witness["schemaVersion"]
                ?? throw new InvalidOperationException(label + " schema version is missing."),
            ["schemaHash"] = RequireString(witness, "schemaHash"),
            ["candidateCount"] = candidateCount,
            ["selectionIds"] = selectionIds,
            ["capture"] = new JObject
            {
                ["officialWorldCheckpoint"] = worldFile,
                ["pending"] = pendingFile,
                ["witness"] = witnessFile
            },
            ["wire"] = new JObject
            {
                ["original"] = wireFile,
                ["restoredReplay"] = replayWireFile,
                ["byteExactReplay"] = true
            },
            ["evidenceSidecar"] = witnessFile,
            ["responseDataIncluded"] = false
        };
        if (label == "skill")
        {
            result["capture"]["originalPrompt"] = CopyCampaign(campaignRoot, staging,
                "dispatch-prompt.txt", destination + "original-prompt.txt");
            result["triggerWitness"] = CopyCampaign(campaignRoot, staging,
                "skill-trigger-witness.json", destination + "trigger-witness.json");
        }
        else if (label == "trait")
        {
            if (aggregateSources.Count > 0) result["aggregateEvidenceSources"] = aggregateSources;
            result["capture"]["originalPrompt"] = CopyCampaign(campaignRoot, staging,
                "trait-dispatch-prompt.txt", destination + "original-prompt.txt");
        }
        return result;
    }

    private static JArray ValidateFacilityUsageEvidence(
        string campaignRoot, string staging, JObject witness, JObject pending, JObject world)
    {
        var sources = new JArray();
        if (witness["facilityUsageEvidence"] == null) return sources;
        if (witness["facilityUsageEvidence"] is not JArray bindings)
            throw new InvalidOperationException("Facility usage evidence must be an array.");
        if (bindings.Count == 0) return sources;
        var commands = new Dictionary<string, JObject>(StringComparer.Ordinal);
        foreach (JToken binding in bindings)
        {
            if (binding is not JObject row) throw new InvalidOperationException("Malformed facility usage binding.");
            string name = RequireString(row, "commandFile");
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"\Acommand-[0-9]{3}\.json\z"))
                throw new InvalidOperationException("Invalid facility usage command path.");
            if (!commands.ContainsKey(name)) commands.Add(name, ReadObject(Path.Combine(campaignRoot, name)));
        }
        LiveFacilityUsageEvidence.Validate(RequireString(witness, "facilityId"),
            (long?)witness["absoluteHour"] ?? throw new InvalidOperationException("Facility usage clock is missing."),
            pending, world, commands, bindings);
        foreach (string name in commands.Keys.OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(campaignRoot, name));
            string relative = "campaign/" + name;
            if (!bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(staging, relative))))
                throw new InvalidOperationException("Facility usage command changed during export.");
            sources.Add(Descriptor(relative, bytes));
        }
        return sources;
    }

    private static JArray ValidateTraitAggregateEvidence(
        string campaignRoot, string staging, string label, JObject witness, JObject pending, JObject world)
    {
        var sources = new JArray();
        if (witness["aggregateEvidence"] is not JArray bindings || bindings.Count == 0) return sources;
        if (label != "trait") throw new InvalidOperationException("Cumulative witness is only supported for traits.");
        var typedPending = JsonUtility.FromJson<CharacterAcquiredTraitPendingRequestState>(pending.ToString());
        var context = typedPending.RestorePublicContext()?.CaptureSnapshot()
            ?? throw new InvalidOperationException("Cumulative trait requires its frozen public context.");
        string ownerId = RequireString(witness, "ownerId");
        JObject actorSection = ((JArray)world["sections"]).OfType<JObject>()
            .Single(value => (string)value["sectionId"] == "characters.world");
        JObject actor = ((JArray)JObject.Parse((string)actorSection["payloadJson"])["actors"])
            .OfType<JObject>().Single(value => (string)value["persistentId"] == ownerId);
        var savedFacts = ((JArray)actor["narrative"]["facts"]).Select(value =>
            JsonUtility.FromJson<CharacterNarrativeFact>(value.ToString())).ToArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var sourceNames = new SortedSet<string>(StringComparer.Ordinal);
        foreach (JObject binding in bindings.OfType<JObject>())
        {
            string name = RequireString(binding, "commandFile");
            string factId = RequireString(binding, "publicFactId");
            string kind = RequireString(binding, "evidenceKind");
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"\Acommand-[0-9]{3}\.json\z")
                || !seen.Add(name + "/" + factId))
                throw new InvalidOperationException("Cumulative command identity is invalid/duplicated.");
            JObject command = ReadObject(Path.Combine(campaignRoot, name));
            var observation = JsonUtility.FromJson<LiveNarrativeActivityWitness>(
                command["observedActivityLedger"].ToString());
            NarrativePublicFactSnapshot fact = context.publicFacts.Single(value => value.factId == factId);
            bool matched = kind == "typed-state-cumulative@1"
                ? LiveNarrativeActivityEvidence.TryMatchState(observation, fact, typedPending.evidenceFactIds, out _)
                : kind == "typed-activity-cumulative@1"
                    && LiveNarrativeActivityEvidence.TryMatch(observation, fact, typedPending.evidenceFactIds, out _);
            if (!matched || observation.actorId != ownerId || (string)command["actorId"] != ownerId)
                throw new InvalidOperationException("Cumulative command does not prove the selected owner/fact.");
            bool SameTuple(CharacterNarrativeFact row) => row.domain.ToString() == fact.domain
                && row.factId == fact.originalFactId && row.subjectId == fact.sourceSubjectId && row.outcome == fact.outcome;
            CharacterNarrativeFact after = observation.factsAfter.Single(SameTuple);
            CharacterNarrativeFact saved = savedFacts.Single(SameTuple);
            if (saved.count != after.count || saved.totalValue != after.totalValue || saved.lastDay != after.lastDay)
                throw new InvalidOperationException("Cumulative observation differs from the official saved actor ledger.");
            sourceNames.Add(name);
        }
        if (seen.Count != bindings.Count) throw new InvalidOperationException("Invalid cumulative evidence binding.");
        foreach (string name in sourceNames)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(campaignRoot, name));
            string relative = "campaign/" + name;
            if (!bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(staging, relative))))
                throw new InvalidOperationException("Cumulative campaign copy changed during export.");
            sources.Add(Descriptor(relative, bytes));
        }
        return sources;
    }

    private static void CopyLivePlan(string campaignRoot, string staging, JObject campaign)
    {
        if (!LiveNarrativeCausalPlan.Files.Any(name => File.Exists(Path.Combine(campaignRoot, name)))) return;
        if (!LiveNarrativeCausalPlan.Files.All(name => File.Exists(Path.Combine(campaignRoot, name))))
            throw new InvalidOperationException("Live causal plan was supplied but did not complete; cannot publish a partial recipe.");
        LiveNarrativeCausalPlan.VerifyCapturedRequests(campaignRoot);
        JObject receipt = ReadObject(Path.Combine(campaignRoot, LiveNarrativeCausalPlan.ExecutionFile));
        var files = (JArray)campaign["files"];
        string[] names = LiveNarrativeCausalPlan.Files.Concat(((JArray)receipt["commands"])
            .OfType<JObject>().Select(value => (string)value["path"])).Distinct(StringComparer.Ordinal).ToArray();
        foreach (string name in names)
        {
            if (string.IsNullOrWhiteSpace(name) || Path.GetFileName(name) != name)
                throw new InvalidOperationException("Invalid live plan command filename.");
            string relative = "campaign/" + name;
            JObject descriptor = files.OfType<JObject>().SingleOrDefault(value => (string)value["path"] == relative);
            if (descriptor == null)
            {
                descriptor = CopyCampaign(campaignRoot, staging, name, relative);
                files.Add(descriptor);
            }
            if (name == LiveNarrativeCausalPlan.ExecutionFile)
                campaign["liveCausalPlan"] = descriptor.DeepClone();
        }
    }

    private static JObject CopyCampaign(
        string campaignRoot,
        string staging,
        string sourceName,
        string relativeDestination)
    {
        string source = Path.GetFullPath(Path.Combine(campaignRoot, sourceName));
        string prefix = campaignRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(source))
            throw new FileNotFoundException("Campaign source is missing.", source);
        return CopyInput(source, staging, relativeDestination);
    }

    private static JObject CopyInput(
        string source,
        string staging,
        string relativeDestination)
    {
        byte[] bytes = File.ReadAllBytes(source);
        string destination = Path.Combine(staging,
            relativeDestination.Replace('/', Path.DirectorySeparatorChar));
        WriteNew(destination, bytes);
        return Descriptor(relativeDestination, bytes);
    }

    private static JObject WriteManifest(string staging, string name, JObject value)
    {
        byte[] canonical = GameplayModuleSelectionCanonicalJson.ToUtf8(value);
        byte[] bytes = new byte[canonical.Length + 1];
        Buffer.BlockCopy(canonical, 0, bytes, 0, canonical.Length);
        bytes[bytes.Length - 1] = (byte)'\n';
        WriteNew(Path.Combine(staging, name), bytes);
        return Descriptor(name, bytes);
    }

    private static JObject Descriptor(string path, byte[] bytes) => new()
    {
        ["path"] = path.Replace('\\', '/'),
        ["bytes"] = bytes.Length,
        ["sha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes)
    };

    // This identity comes only from the sealed official world, never from package labels.
    internal static JObject ExtractWorldLineage(JObject world)
    {
        var save = JsonUtility.FromJson<DungeonGameSaveData>(world.ToString());
        if (save.version != DungeonGameSaveData.CurrentVersion || save.manifest == null
            || save.manifest.payloadHashVersion != DungeonSavePayloadIntegrity.CurrentVersion)
            throw new InvalidOperationException("Source9 lineage requires a current sealed world.");
        if (!DungeonSaveManifest.TryValidate(save.manifest, save.sections, out string sealError))
            throw new InvalidOperationException("Source9 world seal invalid: " + sealError);
        var report = new DungeonGameRestoreReport();
        new GameWorldLineageSaveValidation().Validate(save, report);
        if (!report.Success)
            throw new InvalidOperationException("Source9 world lineage invalid: " + string.Join(" | ", report.Errors));
        DungeonSaveSectionEnvelope section = save.sections.Single(s => s.sectionId == GameWorldLineageSaveSection.Id);
        if (section.sectionVersion != GameWorldLineageSaveData.CurrentVersion || section.optional
            || section.restorePhase != DungeonSaveRestorePhase.Foundation)
            throw new InvalidOperationException("Source9 world lineage envelope invalid.");
        return JObject.FromObject(GameWorldLineageSaveSection.ReadSnapshot(section.payloadJson).Capture());
    }

    private static JObject ProjectFileDescriptor(string root, string relative)
    {
        string full = RequireFileInside(root, Path.Combine(root,
            relative.Replace('/', Path.DirectorySeparatorChar)), "code identity");
        byte[] bytes = File.ReadAllBytes(full);
        return new JObject
        {
            ["path"] = relative,
            ["bytes"] = bytes.Length,
            ["sha256"] = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes)
        };
    }

    private static JObject ReadObject(string path) => ParseObject(
        File.ReadAllBytes(path),
        path);

    private static JObject ParseObject(byte[] bytes, string label)
    {
        try
        {
            return JObject.Parse(new UTF8Encoding(false, true).GetString(bytes));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Invalid JSON: " + label, exception);
        }
    }

    private static string RequireEnvironment(string name)
    {
        string value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Missing environment variable: " + name);
        return value;
    }

    private static string RequirePackageId(string value)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (!string.Equals(canonical, value, StringComparison.Ordinal)
            || canonical.Length < 2 || canonical.Length > 128
            || !canonical.All(character => char.IsLetterOrDigit(character)
                || character is '.' or '_' or ':' or '-'))
            throw new InvalidOperationException("Invalid source-v9 package ID.");
        return canonical;
    }

    private static string RequireDirectoryInside(
        string root,
        string supplied,
        string requiredParent,
        string label)
    {
        string full = Path.GetFullPath(supplied
            ?? throw new ArgumentNullException(nameof(supplied)));
        string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string parentPrefix = Path.GetFullPath(requiredParent)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
            || !full.StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(full))
            throw new InvalidOperationException(label + " must be inside project QA artifacts.");
        return full;
    }

    private static string RequireFileInside(string root, string supplied, string label)
    {
        string full = Path.GetFullPath(supplied
            ?? throw new ArgumentNullException(nameof(supplied)));
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(full))
            throw new FileNotFoundException(label + " is missing or outside the project.", full);
        return full;
    }

    private static string RequireString(JObject value, string key)
    {
        string result = (string)value[key];
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException("Required string is missing: " + key);
        return result;
    }

    private static void RequireSha(JObject value, string key)
    {
        string sha = RequireString(value, key);
        if (!sha.StartsWith("sha256:", StringComparison.Ordinal)
            || sha.Length != 71 || sha.Skip(7).Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException("Invalid SHA-256: " + key);
    }

    private static void RequireUnprefixedHash(JObject value, string key, byte[] bytes)
    {
        string expected = RequireString(value, key);
        string actual = GameplayModuleSelectionCanonicalJson.Sha256Prefixed(bytes).Substring(7);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Byte hash differs: " + key);
    }

    private static void MoveDirectoryWithRetry(string staging, string final)
    {
        const int maxAttempts = 10;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                Directory.Move(staging, final);
                return;
            }
            catch (Exception exception) when (
                (exception is IOException
                    || exception is UnauthorizedAccessException)
                && attempt < maxAttempts - 1)
            {
                System.Threading.Thread.Sleep(50 * (attempt + 1));
            }
        }
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Output parent is missing."));
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        stream.Write(bytes, 0, bytes.Length);
    }
}
#endif
