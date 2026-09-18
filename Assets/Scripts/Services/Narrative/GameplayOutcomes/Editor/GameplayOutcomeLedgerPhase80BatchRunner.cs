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
        "Artifacts/QA/GameplayOutcomeLedgerPhase80Focused-20260918-r1/"
        + "focused-runtime-report.json";

    [MenuItem("DungeonStory/QA/Run Phase80 Gameplay Outcome Ledger")]
    public static void RunFromMenu() => RunBatch();

    public static void RunBatch()
    {
        if (Application.isBatchMode
            && string.IsNullOrWhiteSpace(SceneManager.GetActiveScene().path))
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/TitleScene.unity",
                OpenSceneMode.Single);
        }

        List<Step> steps = new List<Step>();
        Run("ledger-core", () => RequirePass(
            GameplayOutcomeLedgerDebugScenarios.RunAll()), steps);
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
        Run("equipment-evolution-owner", () =>
            EquipmentEvolutionInputOwnerDebugScenarios.RunAll(), steps);
        Run("room-environment-owner", () => Require(
            RoomEnvironmentDebugScenarios.RunAll(false)), steps);

        string absolute = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            ReportRelativePath));
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
