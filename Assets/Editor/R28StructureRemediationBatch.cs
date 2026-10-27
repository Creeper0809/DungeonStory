using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class R28StructureRemediationBatch
{
    public static void RunQueryContracts() => Execute("query-contracts", () =>
    {
        Require(GridFoundationDebugScenarios.VerifyBrainSessionUsesCurrentPolicy(), "Brain path policy invalidation failed.");
        R28AccessAndEcologySnapshotDebugScenarios.RunOrThrow();
        R28OwnedQuerySnapshotDebugScenarios.RunOrThrow();
        R28RemainingQuerySnapshotDebugScenarios.RunOrThrow();
        Require(CharacterMedicalRestoreFaultScenarios.Run(), "Medical restore rollback failed.");
        Require(SurgeryRestoreFaultScenarios.Run(), "Surgery restore rollback failed.");
        Require(FacilityUseExecutionStateDebugScenarios.RunAll(true), "Facility restore failed.");
    });
    public static void RunProgressionContracts() => Execute("progression-contracts", () =>
    {
        Require(CharacterSkillActivationOutcomeDebugScenarios.RunAll(true), "Skill activation contracts failed.");
        Require(CharacterProgressionDebugScenarios.RunGenerationTimeoutScenario(), "Generation retry failed.");
        Require(CharacterProgressionDebugScenarios.RunSkillAcquiredOutcomeAtomicityScenario(), "Skill acquisition atomicity failed.");
        Require(CharacterProgressionDebugScenarios.RunPreparationSkillAcquisitionScenario(), "Preparation acquisition failed.");
    });
    public static void RunAuthoringContracts() => Execute("authoring-contracts", () =>
        ContentReferenceSelectionDebugScenarios.RunWithUndoIsolation(() =>
    {
        R18MedicalPolicyReferenceDebugScenarios.RunR28ForAutomation();
        BranchedProductionNetworkDebugScenarios.RunR28ForAutomation();
        DefenseFacilityServiceOutcomeDebugScenarios.RunR28ForAutomation();
    }));

    private static void Execute(string name, Action action)
    {
        Require(Application.isBatchMode && !Application.isPlaying
            && DungeonPersistencePaths.IsIsolatedVerification, "R28 requires isolated edit-mode persistence.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output), "Missing evidence directory.");
        Directory.CreateDirectory(output);
        string before = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        var errors = new List<string>();
        void Observe(string text, string stack, LogType type)
        { if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(text); }
        Application.logMessageReceived += Observe;
        try
        {
            action();
            Require(errors.Count == 0, "Console errors: " + string.Join(" | ", errors));
            string after = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
            Require(before == after, "Source changed during verification.");
            Write(name + ".txt", "result=PASS\nsourceBefore=" + before + "\nsourceAfter=" + after
                + "\nconsoleErrors=0\nboundary=focused contracts; actual gameplay is recorded separately\n");
            Debug.Log("R28_" + name + "=PASS");
        }
        catch (Exception error)
        {
            Write(name + "-failure.txt", "result=FAIL\nsource=" + before + "\n" + error);
            throw;
        }
        finally { Application.logMessageReceived -= Observe; }
        void Write(string file, string text)
        {
            using var writer = new StreamWriter(new FileStream(Path.Combine(output, file), FileMode.CreateNew));
            writer.Write(text);
        }
    }
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }
}
