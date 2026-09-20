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
    private const string ReportRelativePath =
        "Artifacts/QA/GameplayOutcomeLedgerPhase80Focused-20260920-r83/"
        + "focused-runtime-report.json";

    [MenuItem("DungeonStory/QA/Run Phase80 Gameplay Outcome Ledger")]
    public static void RunFromMenu() => RunBatch();

    public static void RunBatch() => RunBatch(false);

    public static void RunResearchBatch() => RunBatch(true);

    private static void RunBatch(bool researchOnly)
    {
        string absolute = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            ReportRelativePath));
        if (File.Exists(absolute))
        {
            throw new InvalidOperationException(
                "Phase80 focused evidence is immutable and already exists: "
                + ReportRelativePath);
        }

        if (Application.isBatchMode
            && string.IsNullOrWhiteSpace(SceneManager.GetActiveScene().path))
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/TitleScene.unity",
                OpenSceneMode.Single);
        }

        List<Step> steps = new List<Step>();
        if (researchOnly)
            Run("runtime-composition", () => Require(
                DungeonRuntimeCompositionDebugScenarios.RunAll(false)), steps);
        if (!researchOnly)
        {
        Run("ledger-core", () => RequirePass(
            GameplayOutcomeLedgerDebugScenarios.RunAll()), steps);
        Run("migrated-producer-all-57", () => Require(
            MigratedProducerOutcomeDebugScenarios.RunAll(false)), steps);
        Run("migrated-producer-production-entry-57", () => Require(
            MigratedProducerProductionEntry57E2EDebugScenarios.RunAll(false)), steps);
        Run("korean-josa", () => RequirePass(
            KoreanJosaFormatterDebugScenarios.RunAll()), steps);
        Run("runtime-composition", () => Require(
            DungeonRuntimeCompositionDebugScenarios.RunAll(false)), steps);
        Run("trade-inventory-owner", () => RequirePass(
            TradeInventoryOutcomeDebugScenarios.RunAll()), steps);
        Run("facility-evolution-owner", () => Require(
            FacilityEvolutionDebugScenarios.RunAll(false)), steps);
        Run("character-skill-trait-owner", () => Require(
            CharacterProgressionDebugScenarios.RunAll(false)), steps);
        Run("staff-discontent-save", () => Require(
            StaffDiscontentDebugScenarios.RunAll(false)), steps);
        Run("staff-rebellion-response-outcome", () => Require(
            StaffRebellionResponseDebugScenarios.RunAll(false)), steps);
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
        Run("dungeon-space-expansion-outcome", () => Require(
            DungeonSpaceExpansionOutcomeDebugScenarios.RunAll(false)
            && DungeonSpaceExpansionDebugScenarios
                .RunOutcomeIntegrationScenarios(false)), steps);
        Run("building-demolition-outcome", () => Require(
            BuildingDemolitionOutcomeDebugScenarios.RunAll(false)), steps);
        Run("production-command-outcome", () => Require(
            ProductionCommandOutcomeDebugScenarios.RunAll(false)), steps);
        Run("production-command-owner-transaction", () =>
            ProductionPreparedOutputFullPersistenceDebugScenarios
                .VerifyProductionCommandOutcomeTransactions(), steps);
        Run("infrastructure-command-outcome", () => Require(
            InfrastructureCommandOutcomeDebugScenarios.RunAll(false)), steps);
        Run("work-completion-identity-outcome", () => Require(
            WorkCompletionIdentityOutcomeDebugScenarios.RunAll(false)), steps);
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

        Directory.CreateDirectory(Path.GetDirectoryName(absolute)
            ?? throw new InvalidOperationException(
                "Phase80 report directory is missing."));
        File.WriteAllText(absolute, BuildReport(steps), new UTF8Encoding(false));

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
        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();
        try
        {
            action();
            stopwatch.Stop();
            steps.Add(new Step(name, true, string.Empty, stopwatch.ElapsedMilliseconds));
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            steps.Add(new Step(
                name,
                false,
                exception.GetType().Name + ": " + exception.Message,
                stopwatch.ElapsedMilliseconds));
        }
    }

    private static void Require(bool value)
    {
        if (!value)
        {
            throw new InvalidOperationException("Scenario returned false.");
        }
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

    private static string BuildReport(IReadOnlyList<Step> steps)
    {
        bool success = steps.All(step => step.Success);
        StringBuilder builder = new StringBuilder();
        builder.Append("{\n")
            .Append("  \"schema\": \"gameplay-outcome-ledger-phase80-focused@1\",\n")
            .Append("  \"status\": \"").Append(success ? "PASS" : "FAIL")
            .Append("\",\n")
            .Append("  \"unityMcpUsed\": false,\n")
            .Append("  \"dungeonPlayerMcpUsed\": false,\n")
            .Append("  \"steps\": [\n");
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
