#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class AgeLifeObserverDebugScenarios
{
    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("Age and life observer smoke requires a new isolated evidence directory.");
        _ = DungeonPersistencePaths.ValidateVerificationRoot(Path.Combine(output, "persistent-data"),
            output, Path.GetDirectoryName(Application.dataPath), Application.isBatchMode);
        Directory.CreateDirectory(output);
        var report = new StringBuilder("B60 controlled age and life observer smoke\n");
        int exit = 0;
        try
        {
            Debug.Log("B60: daily aging/body effects and saved observation replay");
            AgeConditionAnatomyProjectionDebugScenarios.VerifyObserverFocused();
            report.AppendLine("PASS daily age/life transitions, actual nonlethal/lethal body effects, rejection rollback, saved pending replay and duplicate prevention");
            report.AppendLine("RESULT=PASS");
        }
        catch (Exception exception)
        { exit = 1; report.AppendLine("RESULT=FAIL").AppendLine(exception.ToString()); Debug.LogException(exception); }
        using (var stream = new FileStream(Path.Combine(output, "age-life-observers.txt"), FileMode.CreateNew))
        using (var writer = new StreamWriter(stream)) writer.Write(report.ToString());
        Debug.Log(report.ToString());
        EditorApplication.Exit(exit);
    }
}
#endif
