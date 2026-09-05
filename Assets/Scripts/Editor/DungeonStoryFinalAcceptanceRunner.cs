using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DungeonStoryFinalAcceptanceRunner
{
    public const int ExpectedAcceptanceStepCount = 33;
    public const string ReportRelativePath = "Artifacts/QA/final-acceptance-report.txt";

    [MenuItem("DungeonStory/QA/Run Final Acceptance")]
    public static void RunFromMenu()
    {
        RunAll(true);
    }

    public static bool RunAll(bool logSummary)
    {
        List<AcceptanceStep> steps = new List<AcceptanceStep>();

        Run("Localization assets", ValidateLocalizationAssets, steps);
        Run("V19 runtime authority", () => RuntimeAuthorityV18Validator.ValidateOrThrow(), steps);
        Run("Batch A architecture metrics", () => BatchAArchitectureMetricsValidator.ValidateOrThrow(), steps);
        Run("Runtime composition", () => Require(DungeonRuntimeCompositionDebugScenarios.RunAll(false)), steps);
        Run("74 strict save sections", () => Require(DungeonSaveSectionDebugScenarios.RunAll(false)), steps);
        Run("Batch A content authority", () => Require(BatchAContentAuthorityDebugScenarios.RunAll(false)), steps);
        Run("Batch A core-session save", () => Require(BatchACoreSessionSaveDebugScenarios.RunAll(false)), steps);
        Run("Batch B character/survival authority", BatchBCharacterSurvivalAuthorityDebugScenarios.RunAll, steps);
        Run("Batch C production/infrastructure authority", BatchCProductionInfrastructureAuthorityDebugScenarios.RunAll, steps);
        Run("Persistent identities", PersistentIdentityDebugScenarios.RunAll, steps);
        Run("Physical item contracts", PhysicalItemDebugScenarios.RunAll, steps);
        Run("Physical stock query", PhysicalStockQueryV18DebugScenarios.RunAll, steps);
        Run("V19 equipment item state", EquipmentItemStateV18DebugScenarios.RunAll, steps);
        Run("Combat equipment materials", CombatEquipmentMaterialDebugScenarios.RunAll, steps);
        Run("Research tree", () => Require(ResearchTreeDebugScenarios.RunAll(false)), steps);
        Run("180 research/equipment overhaul", ValidateResearchEquipmentOverhaul, steps);
        Run("Branched production network", ValidateBranchedProduction, steps);
        Run("Production economy", ProductionEconomyDebugScenarios.RunAll, steps);
        Run("Industrial infrastructure", IndustrialInfrastructureDebugScenarios.RunAll, steps);
        Run("Exterior incident authority", () => Require(ExteriorActivityDebugScenarios.RunAll(false)), steps);
        Run("Operating-day settlement authority", () => Require(OperatingDaySettlementDebugScenarios.RunAll(false)), steps);
        Run("Experience pacing authority", ExperiencePacingDebugScenarios.Run, steps);
        Run("Service-room session authority", () => RequireNoErrors(ServiceRoomDebugScenarios.Validate()), steps);
        Run("Combat system", () => RequireNoErrors(CombatSystemDebugScenarios.ValidateAll()), steps);
        Run("Strict combat save", () => Require(StrictProgressionCombatSaveDebugScenarios.RunAll(false)), steps);
        Run("Surgery", () => Require(SurgeryDebugScenarios.RunAll(false)), steps);
        Run("Character anatomy/medical integration", () => RequireNoErrors(CharacterAnatomyMedicalIntegrationDebugScenarios.RunAll()), steps);
        Run("Survival", () => RequireNoErrors(SurvivalDebugScenarios.RunAll()), steps);
        Run("Offense strategic physical expedition", () => _ = OffenseStrategicDebugScenarios.RunAll(), steps);
        Run("Offense expedition journey", () => Require(OffenseExpeditionDebugScenarios.RunAll(false)), steps);
        Run("Offense expedition architecture", () => Require(OffenseExpeditionArchitectureDebugScenarios.RunAll(false)), steps);
        Run("Offense aggregate V19", OffenseAggregateSaveV18DebugScenarios.Run, steps);
        // Always emit the nested suite report. A single false here can represent
        // several independent gameplay regressions, and the final acceptance
        // report must not collapse those failures into an opaque one-line result.
        Run("Implemented gameplay scenarios", () => Require(ImplementedScenarioDebugRunner.RunAll(true)), steps);

        bool exactStepCount = steps.Count == ExpectedAcceptanceStepCount;
        bool success = exactStepCount && steps.All(step => step.Success);
        WriteReport(steps, success);

        if (logSummary)
        {
            string message = BuildSummary(steps, success);
            if (success)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }
        }

        return success;
    }

    private static void ValidateLocalizationAssets()
    {
        ProductionUiLocalizationAssetBuilder.Validate();
        DefenseUiLocalizationAssetBuilder.Validate();
        CharacterCombatUiLocalizationAssetBuilder.Validate();
        WildlifeUiLocalizationAssetBuilder.Validate();
        CharacterNarrativeLocalizationAssetBuilder.Validate();
        CharacterAiDiagnosticsLocalizationAssetBuilder.Validate();
        BuildingSummaryUiLocalizationAssetBuilder.Validate();
        CharacterSummaryUiLocalizationAssetBuilder.Validate();
    }

    private static void ValidateResearchEquipmentOverhaul()
    {
        IReadOnlyList<string> errors = ResearchEquipmentOverhaulDebugScenarios.ValidateAll(out string pacingReport);
        RequireNoErrors(errors);
        if (!string.IsNullOrWhiteSpace(pacingReport))
        {
            Debug.Log(pacingReport);
        }
    }

    private static void ValidateBranchedProduction()
    {
        RequireNoErrors(BranchedProductionNetworkDebugScenarios.Validate());
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Scenario returned false.");
        }
    }

    private static void RequireNoErrors(IEnumerable<string> errors)
    {
        string[] materialized = errors?.Where(error => !string.IsNullOrWhiteSpace(error)).ToArray()
            ?? Array.Empty<string>();
        if (materialized.Length > 0)
        {
            throw new InvalidOperationException(string.Join(" | ", materialized));
        }
    }

    private static void Run(string name, Action action, ICollection<AcceptanceStep> steps)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            action();
            stopwatch.Stop();
            steps.Add(new AcceptanceStep(name, true, string.Empty, stopwatch.ElapsedMilliseconds));
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            steps.Add(new AcceptanceStep(
                name,
                false,
                $"{exception.GetType().Name}: {exception.Message}",
                stopwatch.ElapsedMilliseconds));
        }
    }

    private static void WriteReport(IReadOnlyList<AcceptanceStep> steps, bool success)
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        string reportPath = Path.Combine(projectRoot, ReportRelativePath);
        string directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(reportPath, BuildSummary(steps, success));
    }

    private static string BuildSummary(IReadOnlyList<AcceptanceStep> steps, bool success)
    {
        List<string> lines = new List<string>
        {
            success ? "DungeonStory final acceptance passed." : "DungeonStory final acceptance failed.",
            "Scope: synchronous Editor regressions and source/content contracts.",
            "Deferred external gate: Unity MCP PlayMode UI at 1600x900 and 900x1600, captures, and final Console Error 0 / Warning 0.",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"ExpectedSteps: {ExpectedAcceptanceStepCount}",
            $"ActualSteps: {steps.Count}",
            $"Passed: {steps.Count(step => step.Success)}",
            $"Failed: {steps.Count(step => !step.Success)}"
        };

        foreach (AcceptanceStep step in steps)
        {
            string detail = string.IsNullOrWhiteSpace(step.Detail) ? string.Empty : $" / {step.Detail}";
            lines.Add($"{(step.Success ? "[PASS]" : "[FAIL]")} {step.Name} ({step.DurationMs}ms){detail}");
        }

        return string.Join("\n", lines);
    }

    private sealed class AcceptanceStep
    {
        public AcceptanceStep(string name, bool success, string detail, long durationMs)
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
