#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

public static class ArchitectureRefactorVerificationBatch
{
    public static void RunExtremeTraits() => RunConsumableBatch(
        "extreme-trait-boundary.txt",
        "controlled native actor/capability owners and Golden crop physical JSON restore; existing exact bodies; A07 command recovery runs separately; not natural UI/AI or whole-world capability restore",
        new Func<string>[] { RunExtremeTraitContracts },
        "ARCHITECTURE_EXTREME_TRAITS_BATCH");

    private static string RunExtremeTraitContracts()
    {
        var errors = new List<string>();
        void Observe(string condition, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(condition);
        }
        Application.logMessageReceived += Observe;
        try
        {
            string capabilities = V26FounderTraitAuditScenario.RunExtremeTraitsForArchitectureBatch();
            string golden = EnvironmentCropProductionEntryDebugScenarios.RunGoldenHarvestForArchitectureBatch();
            if (errors.Count != 0)
                throw new InvalidOperationException("Extreme trait execution emitted engine errors: " + string.Join(" | ", errors));
            return capabilities + "\n" + golden + "\nconsoleErrors=0";
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    public static void RunCaptivityLabor() => RunConsumableBatch(
        "captivity-labor-boundary.txt",
        "controlled native actors/physical repository/spawner; delivery port failure, real assignment/permission/wear/return, section JSON state retry; not natural hauling or official labor pending whole-world restore",
        new Func<string>[]
        {
            CaptivityCircusDebugScenarios.RunLaborContractsForArchitectureBatch,
            CaptivityTransitionOutcomeDebugScenarios.RunLaborForArchitectureBatch
        },
        "ARCHITECTURE_CAPTIVITY_LABOR_BATCH");

    public static void RunTreatmentExecution() => RunConsumableBatch(
        "treatment-execution-boundary.txt",
        "controlled native actor/work/physical/service; internal and paid patients; official physical/service/survival/consumables section restores; not natural hauling/AI or whole-world treatment restore",
        new Func<string>[] { RunTreatmentOwnerContracts },
        "ARCHITECTURE_TREATMENT_EXECUTION_BATCH");

    private static string RunTreatmentOwnerContracts()
    {
        var errors = new List<string>();
        void Observe(string condition, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(condition);
        }
        Application.logMessageReceived += Observe;
        try
        {
            string treatment = SurvivalDebugScenarios.RunTreatmentBridgeForServiceOwnerVerification();
            string fuel = SurvivalDebugScenarios.RunFacilityFuelDeliveryFocused();
            if (errors.Count != 0)
                throw new InvalidOperationException("Treatment execution emitted engine errors: " + string.Join(" | ", errors));
            return treatment + "\n" + fuel + "\nconsoleErrors=0";
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    public static void RunConsumablePolicies() => RunConsumableBatch(
        "consumable-policy-boundary.txt",
        "controlled runtime commands; actual policy fact ledger; JSON parent restore; no natural UI/AI or cold whole-world claim",
        new Func<string>[]
        {
            SurvivalDebugScenarios.RunConsumablesPolicyOutcomeAtomicityFocused,
            SurvivalDebugScenarios.RunPersistentActorCaptureClosureFocused
        },
        "ARCHITECTURE_CONSUMABLE_POLICIES_BATCH");

    public static void RunConsumableExecution() => RunConsumableBatch(
        "consumable-execution-boundary.txt",
        "controlled runtime and physical ports; meal/substance/detox/expiry; JSON parent/root restore; no natural UI/AI or cold whole-world claim",
        new Func<string>[]
        {
            SurvivalDebugScenarios.RunConsumablesPolicyOutcomeAtomicityFocused,
            SurvivalDebugScenarios.RunPersistentActorCaptureClosureFocused,
            SurvivalDebugScenarios.RunMealV7PendingOutboxFocused,
            SurvivalDebugScenarios.RunSubstanceV8PendingOutboxFocused,
            SurvivalDebugScenarios.RunPackagedConsumableTareRecoveryFocused,
            SurvivalDebugScenarios.RunSubstanceEffectsForArchitectureBatch,
            SurvivalDebugScenarios.RunOverdosePolicyForArchitectureBatch,
            SurvivalDebugScenarios.VerifyDetoxDomainFactFocused,
            CharacterConsumablesEffectPublicationDebugScenarios.RunSharedOperationOwnershipForArchitectureBatch,
            RunConsumableEffectRecovery
        },
        "ARCHITECTURE_CONSUMABLE_EXECUTION_BATCH");

    private static string RunConsumableEffectRecovery()
    {
        CharacterConsumablesEffectPublicationDebugScenarios.RunAll();
        return "effect-recovery=PASS; contracts=9; meal/substance rejection; durable time; failure history; JSON restore/retry";
    }

    private static void RunConsumableBatch(string filename, string scope, Func<string>[] checks, string marker)
    {
        if (!Application.isBatchMode || Application.isPlaying)
            throw new InvalidOperationException("Consumable verification requires isolated edit batch execution.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output) || !System.IO.Path.IsPathRooted(output))
            throw new InvalidOperationException("Consumable verification requires the launcher's evidence directory.");
        string previous = Environment.GetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable);
        Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable,
            System.IO.Path.Combine(output, "persistent-data"));
        try
        {
            if (!DungeonPersistencePaths.IsIsolatedVerification)
                throw new InvalidOperationException("Isolated consumable persistence was not established.");
            System.IO.Directory.CreateDirectory(output);
            using var report = new System.IO.StreamWriter(new System.IO.FileStream(
                System.IO.Path.Combine(output, filename),
                System.IO.FileMode.CreateNew, System.IO.FileAccess.Write, System.IO.FileShare.Read));
            report.AutoFlush = true;
            report.WriteLine("scope=" + scope);
            try
            {
                foreach (Func<string> check in checks)
                {
                    report.WriteLine("start=" + check.Method.Name);
                    report.WriteLine(check());
                }
                report.WriteLine("result=PASS");
            }
            catch (Exception exception)
            {
                report.WriteLine("result=FAIL; " + exception);
                throw;
            }
            Debug.Log(marker + "=PASS; groups=" + checks.Length);
        }
        finally { Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable, previous); }
    }

    public static void RunEquipmentEvolution()
    {
        EquipmentEvolutionLifecycleFixture.Run();
        Debug.Log("ARCHITECTURE_EQUIPMENT_EVOLUTION_BATCH=PASS; queue/cancel; parent JSON restore; material ACK; publication rollback/retry");
    }

    public static void RunCore()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Architecture verification requires isolated batch execution.");
        string auditOutput = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (!string.IsNullOrWhiteSpace(auditOutput))
            System.IO.Directory.CreateDirectory(auditOutput);
        Debug.Log(GameplayOutcomeLedgerDebugScenarios.RunLedgerStorageContractsForBatch());
        NarrativeFormulaCoreDebugScenarios.RunAll();
        RunSaveJoins();
        RunCampaign();
        PhysicalItemDebugScenarios.RunSaveValidationForBatch();
        PhysicalItemDebugScenarios.RunQuantityTransfersForBatch();
        StoredCargoReturnDebugScenarios.RunForBatch();
        Debug.Log(OrganPreservationRestoreJoinFixture.Run());
        CombatEquipmentCustodyA06DebugScenarios.RunSupplyForBatch();
        OffenseBattleDebugScenarios.RunProjectionRulesForBatch();
        WorldItemDestinationAdmissionDebugScenarios.RunForBatch();
        ExactWarehouseHaulAdmissionJoinDebugScenarios.RunAdmissionServiceForBatch();
        Debug.Log(EnvironmentalFireFocusedDebugScenarios.RunTemporalJoinForBatch());
        AnimalHusbandryOutcomeDebugScenarios.RunWorkDomainFactFocused();
        ProductionCapacityRoutingActorTransitionDebugScenarios.RunAll();
        Debug.Log(PhysicalItemDebugScenarios.VerifyUniqueRetailTransferForEditor());
        PhysicalStockQueryV18DebugScenarios.RunTransferTransitForBatch();
        PreparedOutputCustodyCarryBoundaryDebugScenarios.RunAll();
        ApparelPhysicalTransactionDebugScenarios.RunAll();
        ApparelRejectedDismantlePhysicalTransactionDebugScenarios.RunAll();
        FacilityBufferMassAdmissionDebugScenarios.RunAll();
        FacilityBufferPlannedOutputPublicationDebugScenarios.RunAll();
        Debug.Log("ARCHITECTURE_BUFFER_ADMISSION=PASS; exact capacity; rollback; publication replay; restore candidate atomicity");
        ProductionEconomyDebugScenarios.RunPreparedOutputResolutionForBatch();
        PreparedOutputLegacyBypassStaticDiagnostics.RunAll();
        PreparedOutputHaulPlannerGateDebugScenarios.RunAll();
        FacilityOutputExactRouteDebugScenarios.RunAll();
        ProductionPhysicalCustodyDrainOutboxDebugScenarios.RunAll();
        ProductionInputDestinationCustodyDrainOutboxDebugScenarios.RunAll();
        ProductionCapacityRoutingDrainOutboxDebugScenarios.RunAll();
        _ = PhysicalItemRelocationFactRecoveryDebugScenarios.Run();
        _ = PhysicalItemDispositionFactRecoveryDebugScenarios.Run();
        Debug.Log("ARCHITECTURE_CORE_BATCH=PASS; campaign; physical state/journals/routes/custody/capacity; failure/restore/retry");
    }

    public static void RunCampaign()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Campaign verification requires isolated batch execution.");
        V20CampaignDebugScenarios.RunStateOwnershipForBatch();
        ObservedServiceIncidentFactDebugScenarios.RunBatch();
        Debug.Log(Wim040IncidentCompletionDebugScenarios.RunForArchitectureBatch());
        SeasonalTerminalObserverRecoveryDebugScenarios.RunForBatch();
        V20ContentResolutionRecoveryDebugScenarios.RunReceiptContractBatch();
        Debug.Log("ARCHITECTURE_CAMPAIGN_BATCH=PASS; daily scope; current root; publication fence; response/life/guest receipts; restore/retry");
    }

    public static void RunProductionBills()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Production bill verification requires isolated batch execution.");
        ProductionPreparedOutputFullPersistenceDebugScenarios.RunCommandObserverRecoveryForBatch();
        ProductionPreparedOutputFullPersistenceDebugScenarios.VerifyAllCurrentFormatRoundTrips();
        ProductionPreparedOutputFullPersistenceDebugScenarios.VerifyBillOwnerCheckpointGcForBatch();
        ProductionEconomyDebugScenarios.RunPreparedOutputResolutionForBatch();
        Debug.Log("ARCHITECTURE_PRODUCTION_BILLS_BATCH=PASS; command fence; ACK before/after; fresh-owner restore; exact WIP GC; prepared output; retry");
    }

    public static void RunBuildings()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Building verification requires isolated batch execution.");
        var errors = new List<string>();
        void Observe(string condition, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(condition);
        }
        Application.logMessageReceived += Observe;
        try
        {
            Debug.Log(ScriptableObjectModularityDebugScenarios.RunForBatch());
            Debug.Log(CharacterAiPriorityCornerCaseDebugScenarios.RunOwnerActionCompositionForBatch());
            Debug.Log(ClimateDurableEquipmentDebugScenarios.RunSoObservationCompositionForBatch());
            Debug.Log(CombatSystemDebugScenarios.RunSoRoleCompositionForBatch());
            if (errors.Count != 0)
                throw new InvalidOperationException("SO execution emitted engine errors: " + string.Join(" | ", errors));
            Debug.Log("SO_MODULARITY_CONSOLE_ERRORS=0");
        }
        finally { Application.logMessageReceived -= Observe; }
        BuildingWorldRegistryReplacementDebugScenarios.VerifyDestructionLifetime();
        Debug.Log(CharacterAiPriorityCornerCaseDebugScenarios.RunWorkerAllocationLifetimeForArchitectureBatch());
        Debug.Log("BUILDING_DESTRUCTION_LIFETIME=PASS; EditMode native destruction; observer reentry/failure; contract-release retry; physical destructive drain=not-covered");
        BuildingAbilityRuntimeDebugScenarios.RunForBatch();
        ModularFacilityDebugScenarios.RunDefinitionRuntimeForBatch();
        Debug.Log(BuildingStatePersistenceDebugScenarios.RunForArchitectureBatch());
        FacilityCrimeRiskDebugScenarios.RunAll();
        if (!ExteriorActivityDebugScenarios.RunAll(true))
            throw new InvalidOperationException("Building exterior runtime contracts failed.");
        RunProductionBills();
        Debug.Log("ARCHITECTURE_BUILDINGS_BATCH=PASS; immutable definitions; registered runtime; authored assets; state module restore; crime; exterior; production");
    }

    public static void RunDefense()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Defense verification requires isolated batch execution.");
        MigratedProducerCombatDefenseEntryE2EDebugScenarios.RunDefenseActivationObserversForBatchMode();
        DefenseEffectCommandBoundaryDebugScenarios.RunForBatch();
        Debug.Log("ARCHITECTURE_DEFENSE_BATCH=PASS; command boundary; per-effect and publication rollback; ACK loss; fresh-owner JSON restore; retry");
    }

    public static void RunBuildingVisual()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Building visual verification requires isolated batch execution.");
        BuildingDefinitionVisualDebugScenarios.RunForBatch();
        ModularFacilityDebugScenarios.RunDefinitionRuntimeForBatch();
        Debug.Log(BuildingStatePersistenceDebugScenarios.RunForArchitectureBatch());
        Debug.Log("ARCHITECTURE_BUILDING_VISUAL_BATCH=PASS; definition/model separation; legacy Odin/scene compatibility; actual rendering; component creation; building state restore");
    }

    public static void RunCharacterDefinitions()
    {
        CharacterDefinitionBoundaryDebugScenarios.RunForBatch();
        Debug.Log("ARCHITECTURE_CHARACTER_DEFINITIONS_BATCH=PASS; authored definitions; runtime/editor profile; JSON; fixed skills; personality; work data/commands");
    }

    public static void RunCropPersistence()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Crop persistence verification requires isolated batch execution.");
        EnvironmentCropProductionEntryDebugScenarios.VerifyCropPersistenceBoundaryForBatch();
        EnvironmentCropProductionEntryDebugScenarios.VerifyCropQueryCacheBoundaryForBatch();
        CropPlotInputOwnerDebugScenarios.RunAll();
        if (!EnvironmentCropProductionEntryDebugScenarios.RunAll(log: true))
            throw new InvalidOperationException("Crop production restore/retry regression failed.");
        EnvironmentCropProductionEntryDebugScenarios.RunCropCommandObserversFromBatch();
        Debug.Log("ARCHITECTURE_CROP_PERSISTENCE_BATCH=PASS; root replacement; malformed candidate atomicity; sow; irrigation; harvest; water/treatment recovery; golden harvest; input ownership");
    }

    public static void RunSaveJoins()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Architecture verification requires isolated batch execution.");
        PersistentIdentityDebugScenarios.RunAll();
        OffenseAggregateSaveV18DebugScenarios.Run();
        Wim009ArcaneSeasonalDebugScenarios.Run();
        BuildingDemolitionOutcomeDebugScenarios.RunOrThrow();
        ProductionFacilityTerminalProducerJoinDebugScenarios.RunAll();
        ProductionStockSensorDestructiveDrainParticipantDebugScenarios.RunAll();
        ProductionPreparedOutputRoutingAuthorityDebugScenarios.RunAll();
        ProductionPreparedOutputRoutingRestoreJoinDebugScenarios.RunFromMenu();
        ProductionPreparedOutputFullPersistenceDebugScenarios.VerifyFullCurrentFormatRoundTrip();
        Debug.Log("ARCHITECTURE_SAVE_JOINS=PASS; exact identity; arrival; seasonal offer; terminal producer; stock sensor; full persistence");
    }

    public static void RunDisposition()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Architecture verification requires isolated batch execution.");
        Debug.Log(PhysicalItemDispositionFactRecoveryDebugScenarios.Run());
    }

    public static void RunEnvironment()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Architecture verification requires isolated batch execution.");
        if (!EnvironmentCropProductionEntryDebugScenarios.RunAll(log: true))
            throw new InvalidOperationException("Separated crop receipt production regression failed.");
        if (!EnvironmentFirePhysicalProductionEntryDebugScenarios.RunAll(log: true))
            throw new InvalidOperationException("Separated fire receipt production regression failed.");
        if (!EnvironmentControlExposureOutcomeDebugScenarios.RunAll(log: true, writeReport: false))
            throw new InvalidOperationException("Separated environment condition receipt regression failed.");
        EnvironmentalFireFocusedDebugScenarios.RunAll();
        if (!EnvironmentPopulationRoomSpeciesProductionEntryDebugScenarios.RunAll(log: true))
            throw new InvalidOperationException("Separated population/room/species receipt production regression failed.");
        WildlifeFoodRaidOutboxDebugScenarios.VerifyPendingDispositionOutbox();
        WildlifeGameplayOutcomeDebugScenarios.RunObserverFocusedForBatchMode();
        Debug.Log("ARCHITECTURE_ENVIRONMENT_BATCH=PASS; crop; physical fire; control/exposure; wildlife; typed facts; restore/retry");
    }
}
#endif
