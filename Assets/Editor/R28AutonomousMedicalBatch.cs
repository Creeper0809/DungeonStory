using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VContainer;

public static class R28AutonomousMedicalBatch
{
    private const string MedicalPending = "DungeonStory.R28.MedicalPending";

    public static void RequestForBatch()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
            && !EditorApplication.isPlayingOrWillChangePlaymode,
            "Autonomous medical verification requires fresh isolated PlayMode.");
        Require(!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT")), "Missing current evidence directory.");
        Require(!File.Exists(CharacterAiAutonomousMedicalPlayModeVerifier.ReportPath), "Medical evidence already exists.");
        SessionState.SetBool(MedicalPending, true);
        SessionState.SetBool(MedicalPending + ".started", false);
        SessionState.SetString(MedicalPending + ".source", V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest());
        SessionState.SetBool(MedicalPending + ".errors", false);
        SessionState.SetFloat(MedicalPending + ".deadline", (float)EditorApplication.timeSinceStartup + 180f);
        ResumeMedicalVerification();
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeMedicalVerification()
    {
        if (!SessionState.GetBool(MedicalPending, false)) return;
        EditorApplication.update -= PumpMedical;
        EditorApplication.update += PumpMedical;
        Application.logMessageReceived -= ObserveMedical;
        Application.logMessageReceived += ObserveMedical;
    }

    private static void ObserveMedical(string text, string stack, LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
            SessionState.SetBool(MedicalPending + ".errors", true);
    }

    private static void PumpMedical()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(MedicalPending + ".deadline", 0),
                "Autonomous medical verification exceeded its bounded execution window.");
            if (!EditorApplication.isPlaying && !SessionState.GetBool(MedicalPending + ".started", false)) return;
            if (!SessionState.GetBool(MedicalPending + ".started", false))
            {
                var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
                var owner = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
                if (scope?.Container == null || owner == null) return;
                if (owner.CurrentOwnerActor == null)
                {
                    string preparation = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
                    Require(preparation.StartsWith("committed=True;", StringComparison.Ordinal)
                        && owner.CurrentOwnerActor != null, "Official run preparation failed: " + preparation);
                    Time.timeScale = 1f;
                    Debug.Log("R28_MEDICAL_PREPARATION=PASS; controlled official starting-party command");
                }
                var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
                Require(game != null, "Medical game manager missing.");
                game.isPause = false;
                scope.Container.Resolve<IGameTimeScaleController>().Scale = 1;
                Time.timeScale = 1f;
                CharacterAiAutonomousMedicalPlayModeVerifier.RequestRun();
                SessionState.SetBool(MedicalPending + ".started", true);
                return;
            }
            string report = File.Exists(CharacterAiAutonomousMedicalPlayModeVerifier.ReportPath)
                ? File.ReadAllText(CharacterAiAutonomousMedicalPlayModeVerifier.ReportPath) : string.Empty;
            if (!report.Contains("RESULT=PASS") && !report.Contains("RESULT=FAIL")) return;
            Require(report.Split('\n').Any(line => line.Trim().StartsWith("RESULT=PASS;", StringComparison.Ordinal))
                && !SessionState.GetBool(MedicalPending + ".errors", true), "Autonomous medical failed: " + report);
            Require(SessionState.GetString(MedicalPending + ".source", "")
                == V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(), "Medical source changed.");
            Debug.Log("R28_AUTONOMOUS_MEDICAL=PASS; actual AI stabilization/carry/treatment; original assertions retained");
            FinishMedical(0);
        }
        catch (Exception error) { Debug.LogException(error); FinishMedical(1); }
    }

    private static void FinishMedical(int code)
    {
        SessionState.SetBool(MedicalPending, false);
        EditorApplication.update -= PumpMedical;
        Application.logMessageReceived -= ObserveMedical;
        EditorApplication.Exit(code);
    }

    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }
}
