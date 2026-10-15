#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ResearchWorkObserverDebugScenarios
{
    public static void RunFromBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string persistence = DungeonPersistencePaths.ValidateVerificationRoot(
            Path.Combine(output ?? string.Empty, "persistent-data"), output,
            Path.GetDirectoryName(Application.dataPath), Application.isBatchMode);
        string prior = Environment.GetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable);
        var diagnostics = new List<string>();
        int errors = 0;
        bool serviceTerminalFaultWindow = false;
        void Capture(string message, string trace, LogType type)
        {
            if (type is LogType.Warning or LogType.Error or LogType.Exception or LogType.Assert)
                diagnostics.Add(type + ": " + message + "\n" + trace);
            bool expectedServicePending = serviceTerminalFaultWindow && type == LogType.Error
                && (message.StartsWith("service-session-terminal-pending:", StringComparison.Ordinal)
                    || message.StartsWith("service-session-cancellation-pending:", StringComparison.Ordinal)
                    || message.StartsWith("service-session-terminal-retry-pending:", StringComparison.Ordinal));
            if ((type is LogType.Error or LogType.Exception or LogType.Assert) && !expectedServicePending) errors++;
        }
        Directory.CreateDirectory(output);
        using var file = new FileStream(Path.Combine(output, "research-work-observers.txt"),
            FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var writer = new StreamWriter(file) { AutoFlush = true };
        Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable, persistence);
        Application.logMessageReceived += Capture;
        try
        {
            writer.WriteLine(DomainFactDispatcherSmokeScenarios.Run());
            GameplayDomainFactPairProofDebugScenarios.Run();
            writer.WriteLine("atomic-observation-pair-proof=PASS");
            writer.WriteLine(PhysicalItemFactDebugScenarios.Run());
            writer.WriteLine(PhysicalItemRelocationFactRecoveryDebugScenarios.Run());
            writer.WriteLine(PhysicalItemDispositionFactRecoveryDebugScenarios.Run());
            writer.WriteLine(CharacterCorpseConsumptionFactDebugScenarios.Run());
            writer.WriteLine(CharacterDeprivationExperienceFactDebugScenarios.Run());
            writer.WriteLine(CharacterWaterFactDebugScenarios.Run());
            writer.WriteLine(MigratedProducerCharacterSurvivalEntryE2EDebugScenarios.VerifyWaterFactFocused());
            writer.WriteLine(TradeInventoryOutcomeDebugScenarios.RunAll());
            ResearchOutcomeTransactionDebugScenarios.RunAll(writer.WriteLine);
            VerifyRecoveredUnlockPresentation();
            writer.WriteLine("research-work/progress-completion-unlocks/rejection-rollback/capacity/leap/immediate/knowledge-progress/saved-recovery=PASS");
            writer.WriteLine(ResearchEquipmentOutcomeDebugScenarios.RunAll());
            DungeonSpaceExpansionDebugScenarios.RunOrThrow();
            writer.WriteLine("research-completion/space-parent/event-and-restore-reconciliation=PASS");
            writer.WriteLine(DungeonSpaceFactRecoveryDebugScenarios.Run());
            KnowledgeResearchOutcomeDebugScenarios.RunAnchoredEvidenceRestore();
            writer.WriteLine(KnowledgeCompletionFactDebugScenarios.Run());
            writer.WriteLine(KnowledgeResearchOutcomeDebugScenarios.RunCommitGapFaults());
            writer.WriteLine(KnowledgeResearchOutcomeDebugScenarios.RunCompletionSaveRecovery());
            writer.WriteLine(KnowledgeResiduePhysicalRestoreJoinDebugScenarios.RunAll());
            writer.WriteLine("knowledge-progress/physical-completion/evidence-restore-regression=PASS");
            EnvironmentCropProductionEntryDebugScenarios.VerifyCertifiedSeedObserverFocused();
            writer.WriteLine(EnvironmentCropProductionEntryDebugScenarios.VerifyCertifiedSeedFactRecovery());
            CharacterMedicalOutcomeProductionEntryDebugScenarios.RunOrThrow();
            writer.WriteLine("medical-procedure/facts/rejection/unknown-reply/saved-replay/canonical-join/dedup=PASS");
            writer.WriteLine(MigratedProducerCombatDefenseEntryE2EDebugScenarios.VerifyHealingFactRecoveryFocused());
            writer.WriteLine(MigratedProducerCombatDefenseEntryE2EDebugScenarios.VerifyFatalityFactRecoveryFocused());
            writer.WriteLine(ReproductionProcessTransitionDebugScenarios.RunObserverFocused());
            writer.WriteLine(FacilityVandalismOutcomeDebugScenarios.RunObserverFocused());
            writer.WriteLine(EnvironmentalFireFocusedDebugScenarios.RunDamageObserverFocused());
            writer.WriteLine(EnvironmentalFireFocusedDebugScenarios.RunActionObserverFocused());
            writer.WriteLine(EnvironmentalFireResourceObserverDebugScenarios.Run());
            writer.WriteLine(MigratedProducerEconomyServiceEntryE2EDebugScenarios.VerifyServiceModeFactRecoveryFocused());
            serviceTerminalFaultWindow = true;
            try { writer.WriteLine(ServiceSessionOutcomeDebugScenarios.RunObserverFocused()); }
            finally { serviceTerminalFaultWindow = false; }
            if (errors != 0) throw new InvalidOperationException("Research observer tests emitted " + errors + " Unity errors.");
            writer.WriteLine("production-commands-and-ledger=YES;controlled-fixtures=YES;natural-ai-and-whole-world=NOT-RUN");
            writer.WriteLine("result=PASS");
        }
        catch (Exception exception)
        {
            writer.WriteLine("result=FAIL\n" + exception);
            throw;
        }
        finally
        {
            writer.WriteLine("diagnostics=" + diagnostics.Count + ";errors=" + errors);
            foreach (string diagnostic in diagnostics) writer.WriteLine(diagnostic);
            Application.logMessageReceived -= Capture;
            Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable, prior);
        }
    }

    private static void VerifyRecoveredUnlockPresentation()
    {
        var building = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            var frozen = new[] { new BlueprintUnlockRecord(BlueprintUnlockTypeIds.Building,
                "시설 해금", "731", "frozen-display", codexDetail: "frozen-detail") };
            var restored = BlueprintResearchRuntime.RestoreCompletionPresentation(null, frozen,
                id => id == 731 ? building : null);
            if (restored.Unlocks.Count != 1 || restored.Unlocks[0].Facility != building
                || restored.Unlocks[0].DisplayName != "frozen-display" || frozen[0].Facility != null)
                throw new InvalidOperationException("Recovered unlock presentation lost its definition or mutated evidence.");
            bool rejected = false;
            try { BlueprintResearchRuntime.RestoreCompletionPresentation(null, frozen, _ => null); }
            catch (InvalidOperationException) { rejected = true; }
            if (!rejected) throw new InvalidOperationException("Missing recovered unlock definition was silently dropped.");
        }
        finally { UnityEngine.Object.DestroyImmediate(building); }
    }
}
#endif
