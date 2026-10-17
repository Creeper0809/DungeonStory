using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayOutcomeLedgerPhase80BatchRunner
{
    private const string DefaultReportRelativePath =
        "Artifacts/QA/GameplayOutcomeLedgerPhase80Focused-20260920-r83/"
        + "focused-runtime-report.json";

    [MenuItem("DungeonStory/QA/Run Phase80 Gameplay Outcome Ledger")]
    public static void RunFromMenu() => RunBatch();

    public static void RunBatch() => RunBatch(false);

    public static void RunResearchBatch() => RunBatch(true);

    public static void RunFixtureCorrectionsBatch() => RunBatch(false, true);

    // Final-source row binding reuses the original scenario entry points, not
    // the unrelated full integration suite or weaker copies of assertions.
    public static void RunFinalSourceBoundLedgerSubsetBatch() => RunBatch(false, false, true);

    // Explicit process partitions preserve all full-suite actions and assertions.
    // A single partition is never reported as a completed full integration run.
    public static void RunIntegrationShardOneBatch() => RunBatch(false, integrationShard: 1);
    public static void RunIntegrationShardTwoBatch() => RunBatch(false, integrationShard: 2);
    public static void RunIntegrationShardThreeBatch() => RunBatch(false, integrationShard: 3);
    public static void RunIntegrationShardFourBatch() => RunBatch(false, integrationShard: 4);

    private static void RunBatch(bool researchOnly, bool fixtureCorrectionsOnly = false,
        bool sourceBindingOnly = false, int integrationShard = 0)
    {
        string reportRelativePath = ResolveReportRelativePath();
        string absolute = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            reportRelativePath));
        if (File.Exists(absolute))
        {
            throw new InvalidOperationException(
                "Phase80 focused evidence is immutable and already exists: "
                + reportRelativePath);
        }

        if (Application.isBatchMode
            && string.IsNullOrWhiteSpace(SceneManager.GetActiveScene().path))
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/TitleScene.unity",
                OpenSceneMode.Single);
        }

        List<Step> steps = integrationShard == 0
            ? new List<Step>()
            : new IntegrationShardSteps(integrationShard);
        if (sourceBindingOnly)
        {
            Run("migrated-producer-production-entry-72", () => Require(
                MigratedProducerProductionEntry57E2EDebugScenarios.RunAll(throwOnFailure: true)), steps);
            Run("infrastructure-autonomous-outcomes-e12", () => RequirePass(
                IndustrialInfrastructureDebugScenarios.RunE12AutonomousOutcomeFocused()), steps);
            Run("character-ambition-transition-outcome", () =>
                CharacterAmbitionTransitionOutcomeDebugScenarios.RunOrThrow(), steps);
            Run("policy-skill-equipment-terminal-outcomes", () => Require(
                LedgerB1NewOutcomeDebugScenarios.RunAll(false)), steps);
            Run("v20-content-child-commit-recovery-g08", () =>
                V20ContentResolutionRecoveryDebugScenarios.RunIsolatedOrThrow(), steps);
        }
        else
        {
        if (fixtureCorrectionsOnly)
        {
            Run("migrated-producer-production-entry-72", () => Require(
                MigratedProducerProductionEntry57E2EDebugScenarios.RunAll(throwOnFailure: true)), steps);
            Run("policy-skill-equipment-terminal-outcomes", () => Require(
                LedgerB1NewOutcomeDebugScenarios.RunAll(false)), steps);
            Run("causal-species-incident-owner", () => Require(
                EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                    .RunSpeciesCausalOwnerFamily(false)), steps);
            Run("seasonal-wildlife-arrival-terminal-outcome", () => Require(
                SeasonalWildlifeArrivalOutcomeDebugScenarios.RunAll(throwOnFailure: true)), steps);
        }
        if (!fixtureCorrectionsOnly)
        {
        if (researchOnly)
            Run("runtime-composition", () => Require(
                DungeonRuntimeCompositionDebugScenarios.RunAll(false)), steps);
        if (!researchOnly)
        {
        Run("ledger-core", () => RequirePass(
            GameplayOutcomeLedgerDebugScenarios.RunAll()), steps);
        Run("migrated-producer-all-72", () => Require(
            MigratedProducerOutcomeDebugScenarios.RunAll(throwOnFailure: true)), steps);
        Run("migrated-producer-production-entry-72", () => Require(
            MigratedProducerProductionEntry57E2EDebugScenarios.RunAll(throwOnFailure: true)), steps);
        Run("survival-rest-proficiency-details-d12", () => Require(
            MigratedProducerCharacterSurvivalEntryE2EDebugScenarios
                .VerifyD12SurvivalDetails(false)), steps);
        Run("korean-josa", () => RequirePass(
            KoreanJosaFormatterDebugScenarios.RunAll()), steps);
        Run("runtime-composition", () => Require(
            DungeonRuntimeCompositionDebugScenarios.RunAll(false)), steps);
        Run("offense-truth-reveal-owner", () => Require(
            OffenseWorldMapDebugScenarios.RunAll(false)), steps);
        Run("offense-truth-reveal-causal-owner", () => Require(
            OffenseTruthRevealCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("trade-inventory-owner", () => RequirePass(
            TradeInventoryOutcomeDebugScenarios.RunAll()), steps);
        Run("trade-inventory-production-entry", () => Require(
            TradeInventoryProductionEntryExecutionDebugScenarios.RunAll(false)), steps);
        Run("facility-shop-purchase-t01", () =>
            FacilityShopPurchaseT01DebugScenarios.RunOrThrow(), steps);
        Run("retail-purchase-t03", () =>
            RetailPurchaseT03DebugScenarios.RunOrThrow(), steps);
        Run("facility-fulfillment-procurement-t02-t06", () =>
            FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                .RunOrThrow(), steps);
        Run("regional-supply-contract-t07", () =>
            RegionalSupplyContractTransferOutboxDebugScenarios
                .VerifyGameplayOutcomeParent(), steps);
        Run("retail-purchase-recovery", () =>
            RetailPurchaseRecoveryB0T04DebugScenarios.RunOrThrow(), steps);
        Run("warehouse-delivery-status", () =>
            WarehouseFeatureCommandServiceT05DebugScenarios.RunOrThrow(), steps);
        Run("social-life-production-entry", () => Require(
            SocialLifeProductionEntryExecutionDebugScenarios.RunAll(false)), steps);
        Run("environment-fire-physical-production-entry", () => Require(
            EnvironmentFirePhysicalProductionEntryDebugScenarios.RunAll(false)), steps);
        Run("environment-population-room-species-production-entry", () => Require(
            EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios.RunAll(false)), steps);
        Run("environment-crop-production-entry", () => Require(
            EnvironmentCropProductionEntryDebugScenarios.RunAll(false)), steps);
        Run("environment-control-exposure-production-entry", () => Require(
            EnvironmentControlExposureOutcomeDebugScenarios.RunAll(false)), steps);
        Run("wildlife-food-raid-production-entry", () =>
            WildlifeFoodRaidOutboxDebugScenarios
                .VerifyPendingDispositionOutbox(), steps);
        Run("wildlife-command-action-outcome-recovery", () => Require(
            WildlifeGameplayOutcomeDebugScenarios.RunRegression()), steps);
        Run("wildlife-general-butchery-physical-join", () => Require(
            Wim030CrystalBeetleCarapaceDebugScenarios.Verify()
                .Contains(
                    "parent=animal-husbandry-work-result; event=wildlife-general-butchery; exact=1; physicalChildJoined=true",
                    StringComparison.Ordinal)), steps);
        Run("service-session-terminal-production-entry", () => Require(
            ServiceSessionOutcomeDebugScenarios.RunAll(log: true)), steps);
        Run("crop-failed-cycle-terminal-outcome", () =>
            EnvironmentCropProductionEntryDebugScenarios.RunOrThrow(), steps);
        Run("passive-production-status-projection", () =>
            ProductionCompletedOutcomeAdapterEec04DebugScenarios.Run(), steps);
        Run("causal-owner-family-cross-episode", () => Require(
            ProductionCausalPlanBatchExporter
                .RunOwnerFamilyCheckpointDebugScenarios(false)), steps);
        Run("causal-acquired-trait-memory-owner", () => Require(
            AcquiredTraitMemoryCausalProductionEntryDebugScenarios.RunAll(false)), steps);
        Run("causal-captivity-owner", () => Require(
            CaptivityCausalProductionEntryDebugScenarios.RunAll(false)), steps);
        Run("causal-room-environment-owner", () => Require(
            RoomEnvironmentCausalProductionEntryDebugScenarios.RunAll(false)), steps);
        Run("causal-equipment-apparel-owner", () => Require(
            EquipmentApparelCausalProductionEntryDebugScenarios.RunAll(false)), steps);
        Run("apparel-change-production-entry", () => Require(
            ApparelChangeProductionEntryDebugScenarios.RunAll(false)), steps);
        Run("facility-evolution-owner", () => Require(
            FacilityEvolutionDebugScenarios.RunAll(false)), steps);
        Run("character-skill-trait-owner", () => Require(
            CharacterProgressionDebugScenarios.RunAll(false)), steps);
        Run("trait-analysis-recovery-owner", () => Require(
            TraitAnalysisRecoveryDebugScenarios.RunAll(false)), steps);
        Run("character-ambition-transition-outcome", () =>
            CharacterAmbitionTransitionOutcomeDebugScenarios.RunOrThrow(),
            steps);
        Run("family-planning-policy-reachability", () =>
            FamilyPlanningPolicyReachabilityDebugScenarios.RunOrThrow(),
            steps);
        Run("reproduction-process-transition-and-recovery", () =>
            ReproductionProcessTransitionDebugScenarios.RunOrThrow(),
            steps);
        Run("resident-evacuation-terminal-outcomes", () => RequirePass(
            ResidentEvacuationOutcomeDebugScenarios.RunAll(false)), steps);
        Run("defense-facility-service-outcomes", () =>
            DefenseFacilityServiceOutcomeDebugScenarios.RunOrThrow(), steps);
        Run("door-held-open-policy-outcome", () =>
            DoorHeldOpenOutcomeDebugScenarios.RunOrThrow(), steps);
        Run("policy-skill-equipment-terminal-outcomes", () => Require(
            LedgerB1NewOutcomeDebugScenarios.RunAll(false)), steps);
        Run("staff-discontent-daily-transition-and-save", () =>
            StaffDiscontentDebugScenarios.RunBatch(), steps);
        Run("staff-rebellion-response-outcome", () => Require(
            StaffRebellionResponseDebugScenarios.RunAll(false)), steps);
        Run("causal-staff-rebellion-response-owner", () => Require(
            StaffRebellionResponseDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-species-incident-owner", () => Require(
            EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios
                .RunSpeciesCausalOwnerFamily(false)), steps);
        Run("causal-certified-seed-owner", () => Require(
            EnvironmentCropProductionEntryDebugScenarios
                .RunCertifiedSeedCausalOwnerFamily(false)), steps);
        Run("causal-emergency-work-suspension-owner", () => Require(
            EmergencyWorkSuspensionCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-memory-erasure-boss-award-owner", () => Require(
            MemoryErasureSealBossAwardCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-trade-inventory-three-producer-owner", () => Require(
            TradeInventoryCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-disease-route-exposure-owner", () => Require(
            DiseaseRouteExposureCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("captive-performer-milestone-outcome", () => Require(
            CaptivePerformerMilestoneOutcomeDebugScenarios.RunAll(false)), steps);
        Run("captive-escape-outcome", () => Require(
            CaptiveEscapeOutcomeDebugScenarios.RunAll(false)), steps);
        Run("captive-ransom-outcome", () => Require(
            CaptiveRansomOutcomeDebugScenarios.RunAll(false)), steps);
        Run("captivity-interaction-outcome", () => Require(
            CaptivityInteractionOutcomeDebugScenarios.RunAll(false)), steps);
        Run("emergency-work-suspension-outcome", () => Require(
            EmergencyWorkSuspensionOutcomeDebugScenarios.RunAll(false)), steps);
        Run("facility-synthesis-outcome", () => Require(
            FacilitySynthesisOutcomeDebugScenarios.RunAll(false)
            && FacilitySynthesisDebugScenarios.RunAll(false)), steps);
        Run("causal-facility-synthesis-owner", () => Require(
            FacilitySynthesisDebugScenarios
                .RunCausalSynthesisOwnerFamily(false)), steps);
        Run("dungeon-space-expansion-outcome", () => Require(
            DungeonSpaceExpansionOutcomeDebugScenarios.RunAll(false)
            && DungeonSpaceExpansionDebugScenarios
                .RunOutcomeIntegrationScenarios(false)), steps);
        Run("dungeon-space-causal-trigger-outcome", () =>
            DungeonSpaceExpansionDebugScenarios.RunOrThrow(), steps);
        Run("building-demolition-outcome", () => Require(
            BuildingDemolitionOutcomeDebugScenarios.RunAll(false)), steps);
        Run("production-command-outcome", () => Require(
            ProductionCommandOutcomeDebugScenarios.RunAll(false)), steps);
        Run("production-command-owner-transaction", () =>
            ProductionPreparedOutputFullPersistenceDebugScenarios
                .VerifyProductionCommandOutcomeTransactions(), steps);
        Run("production-completed-owner", () =>
            ProductionPreparedOutputFullPersistenceDebugScenarios
                .VerifyProductionCompletedOutcomeFromRuntime(), steps);
        Run("causal-production-bill-owner", () => Require(
            ProductionPreparedOutputFullPersistenceDebugScenarios
                .RunCausalProductionOwnerFamily(false)), steps);
        Run("causal-facility-evolution-owner", () => Require(
            FacilityEvolutionDebugScenarios
                .RunCausalEvolutionOwnerFamily(false)), steps);
        Run("infrastructure-command-outcome", () => Require(
            InfrastructureCommandOutcomeDebugScenarios.RunAll(false)), steps);
        Run("infrastructure-autonomous-outcomes-e12", () => RequirePass(
            IndustrialInfrastructureDebugScenarios
                .RunE12AutonomousOutcomeFocused()), steps);
        Run("shared-combat-transaction-d02", () => RequirePass(
            MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .RunD02SharedCombatTransactionFocused()), steps);
        Run("social-causation-and-minion-parent-d08", () =>
            MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .RunD08MinionParentFocused(), steps);
        Run("captivity-release-rehabilitation-d09", () => Require(
            CaptivityTransitionOutcomeDebugScenarios.RunFocused()), steps);
        Run("defense-facility-effect-d06", () => RequirePass(
            MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .RunD06DefenseFacilityEffectFocused()), steps);
        Run("causal-infrastructure-power-owner", () => Require(
            InfrastructureCommandCausalProductionEntryDebugScenarios
                .RunOwnerFamily(false)), steps);
        Run("causal-dungeon-space-owner", () => Require(
            DungeonSpaceExpansionCausalProductionEntryDebugScenarios
                .RunOwnerFamily(false)), steps);
        Run("work-completion-identity-outcome", () => Require(
            WorkCompletionIdentityOutcomeDebugScenarios.RunAll(false)), steps);
        Run("work-terminal-identity-ownership", () =>
            WorkTerminalIdentityOwnershipDebugScenarios.RunOrThrow(), steps);
        Run("causal-work-completion-identity-owner", () => Require(
            WorkCompletionIdentityCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-run-operation-variable-owner", () => Require(
            RunVariableCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-owner-run-completion-owner", () => Require(
            OwnerRunCompletionCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-meta-run-progression-owner", () => Require(
            MetaProgressionCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-regular-customer-progression-owner", () => Require(
            RegularCustomerCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("seasonal-wildlife-arrival-terminal-outcome", () => Require(
            SeasonalWildlifeArrivalOutcomeDebugScenarios.RunAll(throwOnFailure: true)), steps);
        Run("seasonal-drift-cargo-terminal-outcome", () => Require(
            SeasonalDriftCargoOutcomeDebugScenarios.RunAll(false)), steps);
        Run("apparel-work-order-terminal-outcome", () => Require(
            ApparelWorkOrderOutcomeDebugScenarios.RunAll(false)), steps);
        Run("combat-equipment-craft-completion-outcome", () => Require(
            CombatEquipmentCraftCompletionOutcomeDebugScenarios.RunAll(false)), steps);
        Run("repair-work-terminal-outcome", () => Require(
            RepairWorkOutcomeDebugScenarios.RunAll(false)), steps);
        Run("causal-character-life-progression-owner", () => Require(
            CharacterLifeCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("visitor-exit-outcome-a03", () => Require(
            VisitorExitOutcomeDebugScenarios.RunAll(false)), steps);
        Run("facility-vandalism-outcome-a04", () => Require(
            FacilityVandalismOutcomeDebugScenarios.RunAll(false)), steps);
        Run("character-skill-activation-outcome-a05", () => Require(
            CharacterSkillActivationOutcomeDebugScenarios.RunAll(false)),
            steps);
        Run("age-condition-anatomy-child-recovery", () => Require(
            AgeConditionAnatomyProjectionDebugScenarios.RunAll(false)), steps);
        Run("disease-anatomy-burden-recovery", () => Require(
            DiseaseAnatomyBurdenProjectionDebugScenarios.RunAll(false)), steps);
        Run("causal-faction-route-progression-owner", () => Require(
            FactionRouteCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("faction-world-memory-d10", () => Require(
            MigratedProducerRunOffenseEntryE2EDebugScenarios
                .RunFactionWorldMemoryD10(false)), steps);
        Run("offense-expedition-v20-d11", () => Require(
            MigratedProducerRunOffenseEntryE2EDebugScenarios
                .RunOffenseV20D11(false)), steps);
        Run("causal-character-consumables-progression-owner", () => Require(
            ConsumablesProgressionCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-survival-meal-shortage-owner", () => Require(
            MealShortageCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-primitive-survival-rest-owner", () => Require(
            PrimitiveSurvivalCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("primitive-survival-atomic-rollback", () =>
            PrimitiveSurvivalCausalProductionEntryDebugScenarios
                .RunB0RollbackBatch(), steps);
        Run("causal-service-hub-day-owner", () => Require(
            ServiceHubDayCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-invasion-lifecycle-owner", () => Require(
            MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .RunInvasionLifecycleCausalOwnerFamily(false)), steps);
        Run("causal-combat-life-cycle-owner", () => Require(
            MigratedProducerCombatDefenseEntryE2EDebugScenarios
                .RunCombatLifeCycleCausalOwnerFamily(false)), steps);
        Run("causal-stock-supply-transform-owner", () => Require(
            StockSupplyTransformCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-observed-career-arc-owner", () => Require(
            ObservedCareerArcCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-facility-wishlist-procurement-owner", () => Require(
            FacilityWishlistProcurementCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-offense-node-resolution-owner", () => Require(
            OffenseNodeResolutionCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("causal-observed-meal-incident-resolution-owner", () => Require(
            ObservedMealIncidentResolutionCausalProductionEntryDebugScenarios
                .RunCausalOwnerFamily(false)), steps);
        Run("apparel-physical-transaction", () =>
            ApparelPhysicalTransactionDebugScenarios.RunAll(), steps);
        Run("product-quality-outcome", () => Require(
            ProductQualityOutcomeDebugScenarios.RunAll(false)), steps);
        Run("combat-product-quality-gate", () => Require(
            CombatEquipmentCraftTransactionFixture.Run()), steps);
        Run("apparel-rejected-dismantle-transaction", () =>
            ApparelRejectedDismantlePhysicalTransactionDebugScenarios.RunAll(), steps);
        Run("memory-erasure-boss-award", () => Require(
            OffenseRewardDebugScenarios.RunAll(false)), steps);
        Run("equipment-evolution-owner", () =>
            EquipmentEvolutionInputOwnerDebugScenarios.RunAll(), steps);
        Run("room-environment-owner", () => Require(
            RoomEnvironmentDebugScenarios.RunAll(false)), steps);
        Run("crop-irrigation-joint-transaction", () =>
        {
            if (!Wim016IrrigationContractDebugScenarios.Run(out string report))
                throw new InvalidOperationException(report);
        }, steps);
        Run("plumbing-work-command-results", () =>
            PlumbingWorkExecutionHandlerDebugScenarios.RunOrThrow(), steps);
        Run("destructive-drain-registry-and-v3-migration", () =>
            ProductionFacilityDestructiveDrainParticipantRegistryDebugScenarios
                .RunAll(), steps);
        Run("destructive-drain-journal", () =>
            ProductionFacilityDestructiveDrainJournalDebugScenarios.RunAll(),
            steps);
        Run("destructive-drain-recovery-runtime", () =>
            ProductionFacilityDestructiveDrainRecoveryRuntimeDebugScenarios
                .RunAll(), steps);
        Run("environmental-fire-outcomes", () =>
            EnvironmentalFireFocusedDebugScenarios.RunAll(), steps);
        Run("defense-combat-joint-transaction", () =>
            DefenseCombatOutcomeTransactionDebugScenarios.RunAll(), steps);
        Run("intruder-defeat-body-authority", () =>
            InvasionIntruderDebugScenarios.VerifyDefeatAuthority(), steps);
        Run("medical-surface-healing-authority", () =>
            CharacterAnatomyMedicalIntegrationDebugScenarios.VerifySurfaceHealingAuthority(), steps);
        Run("character-medical-terminal-outcome", () =>
            CharacterMedicalOutcomeProductionEntryDebugScenarios.RunOrThrow(), steps);
        Run("surgery-terminal-outcome", () =>
            SurgeryOutcomeProductionEntryDebugScenarios.RunOrThrow(), steps);
        Run("consumables-effect-publication-recovery", () =>
            CharacterConsumablesEffectPublicationDebugScenarios.RunAll(), steps);
        Run("content-resolution-partial-recovery", () =>
            V20ContentResolutionRecoveryDebugScenarios.RunOrThrow(), steps);
        Run("construction-support-rollback", () =>
            ConstructionSupportRollbackB0DebugScenarios.RunOrThrow(), steps);
        }
        Run("research-joint-transaction", () =>
            ResearchOutcomeTransactionDebugScenarios.RunAll(), steps);
        Run("research-runtime-work-path", () => Require(
            BlueprintResearchDebugScenarios.RunAll(false)), steps);
        Run("durable-equipment-post-commit", () =>
            DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll(), steps);
        Run("research-equipment-work-path", () =>
            ResearchArcaneIndexEquipmentDebugScenarios.RunAll(), steps);
        Run("research-equipment-receipt", () =>
            Debug.Log(ResearchEquipmentOutcomeDebugScenarios.RunAll()), steps);
        Run("knowledge-research-work-and-recovery", () => RequirePass(
            KnowledgeResearchOutcomeDebugScenarios.RunAll()), steps);
        Run("causal-research-knowledge-owner", () => Require(
            KnowledgeResearchOutcomeDebugScenarios
                .RunCausalResearchOwnerFamily(false)), steps);
        Run("knowledge-reward-and-ack-commit-gaps", () => RequirePass(
            KnowledgeResearchOutcomeDebugScenarios.RunCommitGapFaults()), steps);
        Run("knowledge-reward-owner-preparation", () => RequirePass(
            KnowledgeRewardPreparationDebugScenarios.RunAll()), steps);
        Run("physical-facility-outcome-gateway", () => RequirePass(
            PhysicalFacilityItemSinkGatewayOutcomeDebugScenarios.RunAll()), steps);
        Run("knowledge-physical-restore-identity", () =>
            Debug.Log(KnowledgeResiduePhysicalRestoreJoinDebugScenarios.RunAll()), steps);
        Run("save-payload-integrity", () => RequirePass(
            DungeonSavePayloadIntegrityDebugScenarios.RunAll()), steps);
        Run("durable-save-checkpoint-failures", () =>
            PreparedOutputCheckpointGcDebugScenarios.RunAll(), steps);
        }

        }

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)
            ?? throw new InvalidOperationException(
                "Phase80 report directory is missing."));
        if (steps is IntegrationShardSteps partition)
            partition.RequireCompleteSelection();
        string scope = integrationShard != 0 ? "full-integration-shard"
            : sourceBindingOnly ? "final-source-bound-ledger-subset"
            : fixtureCorrectionsOnly ? "fixture-corrections"
            : researchOnly ? "research" : "full-integration";
        File.WriteAllText(absolute, BuildReport(steps, scope), new UTF8Encoding(false));

        Step[] failures = steps.Where(step => !step.Success).ToArray();
        if (failures.Length > 0)
        {
            throw new InvalidOperationException(
                "Phase80 focused regressions failed: "
                + string.Join(", ", failures.Select(step => step.Name)));
        }

        Debug.Log("PHASE80_GAMEPLAY_OUTCOME_LEDGER_FOCUSED=PASS");
    }

    private static void Run(string name, Action action, ICollection<Step> steps)
    {
        if (steps is IntegrationShardSteps partition && !partition.RegisterAndSelect(name))
            return;
        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();
        Step completed;
        try
        {
            using (GameplayOutcomePublishedEvidenceCapture.PushContext(name))
                action();
            stopwatch.Stop();
            completed = new Step(name, true, string.Empty, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            completed = new Step(
                name,
                false,
                exception.GetType().Name + ": " + exception.Message,
                stopwatch.ElapsedMilliseconds);
        }
        steps.Add(completed);
        // Preserve the actual result even if the isolated job stops before its final report.
        Debug.Log("PHASE80_STEP {\"name\":\"" + Escape(completed.Name)
            + "\",\"status\":\"" + (completed.Success ? "PASS" : "FAIL")
            + "\",\"durationMs\":" + completed.DurationMs
            + ",\"detail\":\"" + Escape(completed.Detail) + "\"}");

        // Fixture scopes have ended. Release unreachable managed test data between
        // scenarios; this does not dispose live domain state or weaken the OS guard.
        long managedBefore = GC.GetTotalMemory(false);
        System.Diagnostics.Stopwatch collection =
            System.Diagnostics.Stopwatch.StartNew();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        collection.Stop();
        using (System.Diagnostics.Process process =
            System.Diagnostics.Process.GetCurrentProcess())
        {
            Debug.Log("PHASE80_MEMORY {\"name\":\"" + Escape(completed.Name)
                + "\",\"managedBytesBeforeCollection\":" + managedBefore
                + ",\"managedBytesAfterCollection\":" + GC.GetTotalMemory(false)
                + ",\"privateBytes\":" + process.PrivateMemorySize64
                + ",\"workingSetBytes\":" + process.WorkingSet64
                + ",\"collectionDurationMs\":" + collection.ElapsedMilliseconds + "}");
        }
    }

    private static void Require(bool value)
    {
        if (!value)
        {
            throw new InvalidOperationException("Scenario returned false.");
        }
    }

    private static string ResolveReportRelativePath()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_PHASE80_FOCUSED_REPORT_PATH");
        string value = string.IsNullOrWhiteSpace(configured)
            ? DefaultReportRelativePath
            : configured.Trim().Replace('\\', '/');
        if (Path.IsPathRooted(value)
            || value.Contains("../", StringComparison.Ordinal)
            || value.Contains("/..", StringComparison.Ordinal)
            || !value.StartsWith("Artifacts/QA/", StringComparison.Ordinal)
            || !value.EndsWith(".json", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Phase80 focused report override must be a JSON path under Artifacts/QA.");
        }
        return value;
    }

    private static void RequirePass(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !value.StartsWith("PASS", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Scenario did not return a PASS result: " + (value ?? "<null>"));
        }
    }

    private static string BuildReport(IReadOnlyList<Step> steps, string scope)
    {
        bool success = steps.All(step => step.Success);
        StringBuilder builder = new StringBuilder();
        builder.Append("{\n")
            .Append("  \"schema\": \"gameplay-outcome-ledger-phase80-focused@1\",\n")
            .Append("  \"scope\": \"").Append(Escape(scope)).Append("\",\n")
            .Append("  \"status\": \"").Append(success ? "PASS" : "FAIL")
            .Append("\",\n")
            .Append("  \"unityMcpUsed\": false,\n")
            .Append("  \"dungeonPlayerMcpUsed\": false,\n");
        if (steps is IntegrationShardSteps partition)
        {
            builder.Append("  \"integrationPartition\": {\"index\": ")
                .Append(partition.Index)
                .Append(", \"count\": 4, \"expectedTotalSteps\": 142, \"firstStepIndex\": ")
                .Append(partition.FirstStepIndex)
                .Append(", \"selectedStepCount\": ").Append(steps.Count)
                .Append(", \"completeSuiteExecutedInThisProcess\": false, \"declaredSteps\": [")
                .Append(string.Join(", ", partition.DeclaredNames.Select(
                    name => "\"" + Escape(name) + "\"")))
                .Append("]},\n");
        }
        builder.Append("  \"steps\": [\n");
        for (int index = 0; index < steps.Count; index++)
        {
            Step step = steps[index];
            builder.Append("    {\"name\": \"").Append(Escape(step.Name))
                .Append("\", \"status\": \"")
                .Append(step.Success ? "PASS" : "FAIL")
                .Append("\", \"durationMs\": ").Append(step.DurationMs)
                .Append(", \"detail\": \"").Append(Escape(step.Detail)).Append("\"}")
                .Append(index + 1 == steps.Count ? "\n" : ",\n");
        }

        return builder.Append("  ]\n}\n").ToString();
    }

    private static string Escape(string value) => (value ?? string.Empty)
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\r", "\\r")
        .Replace("\n", "\\n");

    private sealed class IntegrationShardSteps : List<Step>
    {
        private const int ExpectedTotalSteps = 142;
        private const int PartitionWidth = 36;
        private readonly List<string> declaredNames = new List<string>();
        private readonly HashSet<string> uniqueNames =
            new HashSet<string>(StringComparer.Ordinal);

        public IntegrationShardSteps(int index)
        {
            if (index < 1 || index > 4)
                throw new ArgumentOutOfRangeException(nameof(index));
            Index = index;
        }

        public int Index { get; }
        public int FirstStepIndex => (Index - 1) * PartitionWidth;
        public IReadOnlyList<string> DeclaredNames => declaredNames;

        public bool RegisterAndSelect(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || !uniqueNames.Add(name))
                throw new InvalidOperationException("Phase80 partition step names must be unique.");
            int ordinal = declaredNames.Count;
            declaredNames.Add(name);
            return ordinal >= FirstStepIndex
                && ordinal < FirstStepIndex + PartitionWidth;
        }

        public void RequireCompleteSelection()
        {
            int expectedSelected = Math.Min(
                PartitionWidth, ExpectedTotalSteps - FirstStepIndex);
            if (declaredNames.Count != ExpectedTotalSteps || Count != expectedSelected
                || !this.Select(step => step.Name).SequenceEqual(
                    declaredNames.Skip(FirstStepIndex).Take(expectedSelected)))
            {
                throw new InvalidOperationException(
                    "Phase80 partition did not enumerate the complete142-step suite or execute its exact slice.");
            }
        }
    }

    private sealed class Step
    {
        public Step(string name, bool success, string detail, long durationMs)
        {
            Name = name;
            Success = success;
            Detail = detail;
            DurationMs = durationMs;
        }

        public string Name { get; }
        public bool Success { get; }
        public string Detail { get; }
        public long DurationMs { get; }
    }
}
