using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ImplementedScenarioDebugRunner
{
    private const string ReportRelativePath = "Temp/DungeonStoryImplementedScenarioReport.txt";
    private const string JsonReportRelativePath = "Temp/DungeonStoryImplementedScenarioReport.json";

    [MenuItem("DungeonStory/Debug/Run All Implemented Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Implemented scenario suite failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/Open Last Implemented Scenario Report")]
    public static void OpenLastReportFromMenu()
    {
        string reportPath = GetReportPath();
        if (!File.Exists(reportPath))
        {
            Debug.LogWarning($"Implemented scenario report does not exist yet: {reportPath}");
            return;
        }

        UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(reportPath, 1);
    }

    public static void RunForBatchMode()
    {
        int exitCode = 1;
        try
        {
            bool success = RunAll(true);
            exitCode = success ? 0 : 1;
            if (!success)
            {
                Debug.LogError("Implemented scenario suite failed.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            WriteTextReport(
                GetReportPath(),
                string.Join(
                    "\n",
                    "Implemented scenarios crashed.",
                    $"Generated: {generatedAt}",
                    $"Exception: {ex.GetType().Name}",
                    ex.Message,
                    ex.StackTrace));
            WriteJsonReport(
                GetJsonReportPath(),
                BuildCrashJson(generatedAt, ex));
        }

        if (Application.isBatchMode)
        {
            EditorApplication.Exit(exitCode);
            return;
        }

        Debug.LogWarning($"RunForBatchMode finished outside batchmode. Exit code would be {exitCode}.");
    }

    public static bool RunAll(bool logSummary)
    {
        EnsureGeneratedAssets();

        List<ScenarioSuiteResult> results = new List<ScenarioSuiteResult>();

        Run("P0 Grid foundation", GridFoundationDebugScenarios.RunAll, results);
        Run("P1 Facilities and logistics", FacilityDebugScenarios.RunAll, results);
        Run("P1 Plan character AI", RunPlanCharacterAiScenarios, results);
        Run("P1 Customer AI", CustomerAiDebugScenarios.RunAll, results);
        Run("P1 Character model", CharacterModelDebugScenarios.RunAll, results);
        Run("P1 Owner character", OwnerDebugScenarios.RunAll, results);
        Run("P1 Work priority", WorkPriorityDebugScenarios.RunAll, results);
        Run("P1 Work amount", WorkAmountDebugScenarios.RunAll, results);
        Run("P1 Captivity, circus, and door access", CaptivityCircusDebugScenarios.RunAll, results);
        Run("P1 Priority command", PriorityCommandDebugScenarios.RunAll, results);
        Run("P1 Staff duty", StaffDutyDebugScenarios.RunAll, results);
        Run("P1 Operating day", OperatingDaySettlementDebugScenarios.RunAll, results);
        Run("P1 Gameplay flow diagnostics", GameplayFlowDiagnosticsDebugScenarios.RunAll, results);
        Run("P1 Character feedback", CharacterFeedbackDebugScenarios.RunAll, results);
        Run("P1 Event alerts", EventAlertDebugScenarios.RunAll, results);
        Run("P1 Invasion threat", InvasionThreatDebugScenarios.RunAll, results);
        Run("P1 Intruder", InvasionIntruderDebugScenarios.RunAll, results);
        Run("P1 Defense facilities", DefenseFacilityDebugScenarios.RunAll, results);
        Run("P1 Combat report", InvasionCombatReportDebugScenarios.RunAll, results);
        Run("P1 Facility shop", FacilityShopDebugScenarios.RunAll, results);
        Run("P1 Blueprint research", BlueprintResearchDebugScenarios.RunAll, results);
        Run("P1 Facility synthesis", FacilitySynthesisDebugScenarios.RunAll, results);
        Run("P1 Facility evolution", FacilityEvolutionDebugScenarios.RunAll, results);
        Run("P1 Codex", CodexDebugScenarios.RunAll, results);
        Run("P2 Regular customer", RegularCustomerDebugScenarios.RunAll, results);
        Run("P2 Staff discontent", StaffDiscontentDebugScenarios.RunAll, results);
        Run("P2 Staff rebellion response", StaffRebellionResponseDebugScenarios.RunAll, results);
        Run("P2 Run variables", RunVariableDebugScenarios.RunAll, results);
        Run("P2 Meta progression", MetaProgressionDebugScenarios.RunAll, results);
        Run("P3 Offense world map", OffenseWorldMapDebugScenarios.RunAll, results);
        Run("P3 Offense turn battle", OffenseBattleDebugScenarios.RunAll, results);
        Run("P3 Offense rewards", OffenseRewardDebugScenarios.RunAll, results);

        bool success = results.All((result) => result.Success);
        if (logSummary)
        {
            LogSummary(results, success);
        }

        return success;
    }

    private static void EnsureGeneratedAssets()
    {
        P1DefenseFacilityAssetBuilder.EnsureP1DefenseAssets();
        P1FacilityShopAssetBuilder.EnsureP1FacilityShopAssets();
        P1FacilitySynthesisAssetBuilder.EnsureP1SynthesisAssets();
        P1FacilityEvolutionAssetBuilder.EnsureP1EvolutionAssets();
        CaptivityFacilityAssetBuilder.BuildAll();
    }

    private static void Run(
        string name,
        Func<bool, bool> scenario,
        List<ScenarioSuiteResult> results)
    {
        System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            bool success = scenario(false);
            stopwatch.Stop();
            string detail = success ? string.Empty : "Returned false";
            results.Add(new ScenarioSuiteResult(name, success, detail, stopwatch.ElapsedMilliseconds));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Debug.LogException(ex);
            string detail = $"{ex.GetType().Name}: {ex.Message}";
            results.Add(new ScenarioSuiteResult(name, false, detail, stopwatch.ElapsedMilliseconds));
        }
    }

    private static bool RunPlanCharacterAiScenarios(bool log)
    {
        CharacterAiPlanDebugScenarios.RunAll();
        return true;
    }

    private static void LogSummary(IReadOnlyList<ScenarioSuiteResult> results, bool success)
    {
        string reportPath = GetReportPath();
        string jsonReportPath = GetJsonReportPath();
        string generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        List<string> lines = new List<string>
        {
            success ? "Implemented scenarios passed." : "Implemented scenarios failed.",
            $"Generated: {generatedAt}",
            $"Report: {reportPath}",
            $"JsonReport: {jsonReportPath}",
            $"Suites: {results.Count}",
            $"Passed: {results.Count((result) => result.Success)}",
            $"Failed: {results.Count((result) => !result.Success)}"
        };

        foreach (ScenarioSuiteResult result in results)
        {
            string suffix = string.IsNullOrWhiteSpace(result.Detail) ? string.Empty : $" / {result.Detail}";
            lines.Add($"{(result.Success ? "[PASS]" : "[FAIL]")} {result.Name} ({result.DurationMs}ms){suffix}");
        }

        string summary = string.Join("\n", lines);
        WriteTextReport(reportPath, summary);
        WriteJsonReport(jsonReportPath, BuildJsonSummary(results, success, generatedAt, reportPath, jsonReportPath));

        if (success)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }
    }

    private static string GetReportPath()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        return Path.Combine(projectRoot ?? Application.dataPath, ReportRelativePath);
    }

    private static string GetJsonReportPath()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        return Path.Combine(projectRoot ?? Application.dataPath, JsonReportRelativePath);
    }

    private static void WriteTextReport(string reportPath, string summary)
    {
        try
        {
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(reportPath, summary);
            Debug.Log($"Implemented scenario report saved: {reportPath}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private static void WriteJsonReport(string reportPath, string json)
    {
        try
        {
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(reportPath, json);
            Debug.Log($"Implemented scenario JSON report saved: {reportPath}");
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private static string BuildJsonSummary(
        IReadOnlyList<ScenarioSuiteResult> results,
        bool success,
        string generatedAt,
        string reportPath,
        string jsonReportPath)
    {
        List<string> lines = new List<string>
        {
            "{",
            $"  \"success\": {ToJsonBool(success)},",
            $"  \"generatedAt\": \"{EscapeJson(generatedAt)}\",",
            $"  \"reportPath\": \"{EscapeJson(reportPath)}\",",
            $"  \"jsonReportPath\": \"{EscapeJson(jsonReportPath)}\",",
            $"  \"suites\": {results.Count},",
            $"  \"passed\": {results.Count((result) => result.Success)},",
            $"  \"failed\": {results.Count((result) => !result.Success)},",
            "  \"results\": ["
        };

        for (int i = 0; i < results.Count; i++)
        {
            ScenarioSuiteResult result = results[i];
            string suffix = i == results.Count - 1 ? string.Empty : ",";
            lines.Add("    {");
            lines.Add($"      \"name\": \"{EscapeJson(result.Name)}\",");
            lines.Add($"      \"success\": {ToJsonBool(result.Success)},");
            lines.Add($"      \"detail\": \"{EscapeJson(result.Detail)}\",");
            lines.Add($"      \"durationMs\": {result.DurationMs}");
            lines.Add($"    }}{suffix}");
        }

        lines.Add("  ]");
        lines.Add("}");
        return string.Join("\n", lines);
    }

    private static string BuildCrashJson(string generatedAt, Exception ex)
    {
        return string.Join(
            "\n",
            "{",
            "  \"success\": false,",
            "  \"crashed\": true,",
            $"  \"generatedAt\": \"{EscapeJson(generatedAt)}\",",
            $"  \"exceptionType\": \"{EscapeJson(ex.GetType().Name)}\",",
            $"  \"message\": \"{EscapeJson(ex.Message)}\",",
            $"  \"stackTrace\": \"{EscapeJson(ex.StackTrace)}\"",
            "}");
    }

    private static string ToJsonBool(bool value)
    {
        return value ? "true" : "false";
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }

    private readonly struct ScenarioSuiteResult
    {
        public ScenarioSuiteResult(string name, bool success, string detail, long durationMs)
        {
            Name = name;
            Success = success;
            Detail = detail ?? string.Empty;
            DurationMs = durationMs;
        }

        public string Name { get; }
        public bool Success { get; }
        public string Detail { get; }
        public long DurationMs { get; }
    }
}
