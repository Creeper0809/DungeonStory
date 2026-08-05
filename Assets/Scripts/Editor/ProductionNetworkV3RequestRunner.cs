#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ProductionNetworkV3RequestRunner
{
    public const string RequestPath = "Temp/production-network-v3.request";
    public const string ReportPath = "Temp/production-network-v3-report.txt";

    static ProductionNetworkV3RequestRunner()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    private static void OnEditorUpdate()
    {
        if (EditorApplication.isCompiling
            || EditorApplication.isPlayingOrWillChangePlaymode
            || !File.Exists(RequestPath))
        {
            return;
        }

        File.Delete(RequestPath);
        List<string> report = new() { $"Generated: {DateTime.UtcNow:O}" };
        RunStep("resource economy", ResourceEconomyAssetBuilder.Rebuild, report);
        RunStep("combat equipment", CombatEquipmentAssetBuilder.BuildAll, report);
        RunStep("research/content", ResearchProjectAssetBuilder.Rebuild, report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        IReadOnlyList<string> graphFailures =
            BranchedProductionNetworkDebugScenarios.Validate();
        report.Add(graphFailures.Count == 0
            ? "PRODUCTION GRAPH PASS"
            : $"PRODUCTION GRAPH FAIL ({graphFailures.Count})\n"
                + string.Join("\n", graphFailures));
        RunStep("production runtime contracts", ProductionEconomyDebugScenarios.RunAll, report);
        IReadOnlyList<string> researchFailures =
            ResearchEquipmentOverhaulDebugScenarios.ValidateAll(
                out string pacingReport);
        report.Add(researchFailures.Count == 0
            ? "RESEARCH/EQUIPMENT PASS"
            : $"RESEARCH/EQUIPMENT FAIL ({researchFailures.Count})\n"
                + string.Join("\n", researchFailures));
        report.Add(pacingReport);
        Directory.CreateDirectory("Temp");
        File.WriteAllText(ReportPath, string.Join("\n", report));
        Debug.Log($"Production network V3 report written: {ReportPath}");
    }

    private static void RunStep(
        string label,
        Action action,
        ICollection<string> report)
    {
        try
        {
            action();
            report.Add($"{label}: PASS");
        }
        catch (Exception exception)
        {
            report.Add($"{label}: FAIL - {exception.Message}");
            Debug.LogException(exception);
        }
    }
}
#endif
