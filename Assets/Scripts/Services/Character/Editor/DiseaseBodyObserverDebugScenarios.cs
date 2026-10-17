#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DiseaseBodyObserverDebugScenarios
{
    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("Disease body observer smoke requires a new isolated evidence directory.");
        _ = DungeonPersistencePaths.ValidateVerificationRoot(Path.Combine(output, "persistent-data"),
            output, Path.GetDirectoryName(Application.dataPath), Application.isBatchMode);
        Directory.CreateDirectory(output);
        var report = new StringBuilder("B59 controlled disease body observer smoke\n");
        int exit = 0;
        try
        {
            Debug.Log("B59: disease progression/body effects and saved observation replay");
            DiseaseAnatomyBurdenProjectionDebugScenarios.VerifyObserverFocused();
            report.AppendLine("PASS actual disease/body effects, known rejection rollback, saved pending replay and duplicate prevention");
            report.AppendLine("RESULT=PASS");
        }
        catch (Exception exception)
        { exit = 1; report.AppendLine("RESULT=FAIL").AppendLine(exception.ToString()); Debug.LogException(exception); }
        using (var stream = new FileStream(Path.Combine(output, "disease-body-observers.txt"), FileMode.CreateNew))
        using (var writer = new StreamWriter(stream)) writer.Write(report.ToString());
        Debug.Log(report.ToString());
        EditorApplication.Exit(exit);
    }
}
#endif
