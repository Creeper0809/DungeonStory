#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CharacterSkillObserverDebugScenarios
{
    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("Skill observer smoke requires a new isolated evidence directory.");
        _ = DungeonPersistencePaths.ValidateVerificationRoot(Path.Combine(output, "persistent-data"),
            output, Path.GetDirectoryName(Application.dataPath), Application.isBatchMode);
        Directory.CreateDirectory(output);
        var report = new StringBuilder("B58 controlled production skill observer smoke\n");
        int exit = 0;
        try
        {
            Debug.Log("B58: standalone skill effects, observer, rejection and recovery");
            CharacterSkillActivationOutcomeDebugScenarios.VerifyStandaloneObserverFocused();
            report.AppendLine("PASS standalone manual/automatic production skills, observer failure and saved pending recovery");
            Debug.Log("B58: shared combat ultimate regression");
            CharacterSkillActivationOutcomeDebugScenarios.VerifyAppliedUltimateFocused();
            if (!OffenseRewardDebugScenarios.VerifyOffenseUltimateAtomicPublicEntry())
                throw new InvalidOperationException("Public ultimate command returned false.");
            report.AppendLine("PASS shared applied and actual player/planned combat ultimate");
            report.AppendLine("RESULT=PASS");
        }
        catch (Exception exception)
        { exit = 1; report.AppendLine("RESULT=FAIL").AppendLine(exception.ToString()); Debug.LogException(exception); }
        using (var stream = new FileStream(Path.Combine(output, "skill-observers.txt"), FileMode.CreateNew))
        using (var writer = new StreamWriter(stream)) writer.Write(report.ToString());
        Debug.Log(report.ToString());
        EditorApplication.Exit(exit);
    }
}
#endif
