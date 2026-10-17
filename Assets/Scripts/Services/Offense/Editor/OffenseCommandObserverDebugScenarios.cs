#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class OffenseCommandObserverDebugScenarios
{
    public static void RunBatch()
    {
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (!Application.isBatchMode || string.IsNullOrWhiteSpace(output) || Directory.Exists(output))
            throw new InvalidOperationException("Battle observer smoke requires a new isolated evidence directory.");
        _ = DungeonPersistencePaths.ValidateVerificationRoot(Path.Combine(output, "persistent-data"),
            output, Path.GetDirectoryName(Application.dataPath), Application.isBatchMode);
        Directory.CreateDirectory(output);
        var report = new StringBuilder("B57 controlled production command/skill smoke\n");
        int exit = 0;
        try
        {
            Debug.Log("B57 step 1/6: actual command observer and equipment reference policy");
            MigratedProducerRunOffenseEntryE2EDebugScenarios.VerifyBattleCommandObserverFocused();
            report.AppendLine("PASS actual command observer success/rejection and detailed payload");
            Debug.Log("B57 step 2/6: applied ultimate observer joint contract");
            CharacterSkillActivationOutcomeDebugScenarios.VerifyAppliedUltimateFocused();
            report.AppendLine("PASS applied ultimate/observer joint contract");
            Debug.Log("B57 step 3/6: player/planned ultimate production entry");
            if (!OffenseRewardDebugScenarios.VerifyOffenseUltimateAtomicPublicEntry())
                throw new InvalidOperationException("Public ultimate command returned false.");
            report.AppendLine("PASS actual player + planned battle command/ultimate damage, exact child and use limit");
            Debug.Log("B57 step 4/6: planned command equipment/ability");
            if (!OffenseRewardDebugScenarios.VerifyPlannedCommandPayloadForwarding())
                throw new InvalidOperationException("Planned command payload forwarding returned false.");
            report.AppendLine("PASS actual planned command equipment/ability payload");
            Debug.Log("B57 step 5/6: battle snapshot restore");
            OffenseRewardDebugScenarios.RunPersistentBattleSnapshotRestoreRegression();
            report.AppendLine("PASS current battle snapshot/restore");
            Debug.Log("B57 step 6/6: restore publication isolation");
            OffenseRewardDebugScenarios.VerifyRuntimeOwnerRestoreIsolation();
            report.AppendLine("PASS battle/expedition restore publication isolation");
            report.AppendLine("RESULT=PASS");
        }
        catch (Exception exception)
        { exit = 1; report.AppendLine("RESULT=FAIL").AppendLine(exception.ToString()); Debug.LogException(exception); }
        using (var stream = new FileStream(Path.Combine(output, "battle-command-observers.txt"), FileMode.CreateNew))
        using (var writer = new StreamWriter(stream)) writer.Write(report.ToString());
        Debug.Log(report.ToString());
        EditorApplication.Exit(exit);
    }
}
#endif
