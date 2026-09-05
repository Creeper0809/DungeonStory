#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class V27PhysicalMassAuditRequestRunner
{
    public const string RequestPath = "Temp/v27-physical-mass-audits.request";
    public const string ReportPath = "Artifacts/QA/v27-physical-mass-audits-request.txt";

    static V27PhysicalMassAuditRequestRunner()
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
        List<string> lines = new()
        {
            "V27_PHYSICAL_MASS_AUDIT_REQUEST_V1",
            $"generatedUtc={DateTime.UtcNow:O}"
        };
        bool success = true;
        success &= RunStep(
            "explicit-unit-semantics",
            V27PhysicalMassExplicitSemanticDebugScenarios.RunFromMenu,
            lines);
        success &= RunStep(
            "recipe-mass-inventory",
            V27PhysicalMassRecipeInventoryDebugScenarios.RunFromMenu,
            lines);

        lines.Add($"RESULT={(success ? "PASS" : "FAIL")}");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Artifacts/QA");
        File.WriteAllText(ReportPath, string.Join("\n", lines));
        AssetDatabase.Refresh();
        if (success)
        {
            Debug.Log($"V27 physical-mass audits passed: {ReportPath}");
        }
        else
        {
            Debug.LogError($"V27 physical-mass audits failed: {ReportPath}");
        }
    }

    private static bool RunStep(string name, Action action, ICollection<string> lines)
    {
        try
        {
            action();
            lines.Add($"PASS {name}");
            return true;
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL {name}: {exception.GetType().Name}: {exception.Message}");
            Debug.LogException(exception);
            return false;
        }
    }
}
#endif
