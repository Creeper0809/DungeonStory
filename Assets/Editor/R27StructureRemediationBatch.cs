using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class R27StructureRemediationBatch
{
    private const string DeliveryPending = "DungeonStory.R27.DeliveryPending";

    public static void RequestFactionDeliveryForBatch()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
            && !EditorApplication.isPlayingOrWillChangePlaymode,
            "Faction delivery verification requires fresh isolated PlayMode.");
        Require(!File.Exists(WimWinterContractLiveRunner.ReportPath), "Delivery evidence already exists.");
        SessionState.SetBool(DeliveryPending, true);
        SessionState.SetBool(DeliveryPending + ".started", false);
        SessionState.SetString(DeliveryPending + ".source", V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest());
        SessionState.SetBool(DeliveryPending + ".errors", false);
        SessionState.SetFloat(DeliveryPending + ".deadline", (float)EditorApplication.timeSinceStartup + 600f);
        ResumeDeliveryVerification();
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void ResumeDeliveryVerification()
    {
        if (!SessionState.GetBool(DeliveryPending, false)) return;
        EditorApplication.update -= PumpDelivery;
        EditorApplication.update += PumpDelivery;
        Application.logMessageReceived -= ObserveDelivery;
        Application.logMessageReceived += ObserveDelivery;
    }

    private static void ObserveDelivery(string text, string stack, LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert)
            SessionState.SetBool(DeliveryPending + ".errors", true);
    }

    private static void PumpDelivery()
    {
        try
        {
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(DeliveryPending + ".deadline", 0),
                "Faction delivery verification exceeded its bounded execution window.");
            if (!EditorApplication.isPlaying) return;
            if (!SessionState.GetBool(DeliveryPending + ".started", false))
            {
                var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
                var owner = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
                if (scope?.Container == null || owner == null) return;
                if (owner.CurrentOwnerActor == null)
                {
                    string preparation = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
                    Require(preparation.StartsWith("committed=True;", StringComparison.Ordinal)
                        && owner.CurrentOwnerActor != null, "Official run preparation failed: " + preparation);
                    // Fast preparation intentionally freezes Unity time at commit.
                    // The delivery runner controls the game clock independently.
                    Time.timeScale = 1f;
                    Debug.Log("R27_DELIVERY_PREPARATION=PASS; official controlled starting-party command; party UI is outside this delivery test");
                }
                _ = WimWinterContractLiveRunner.StartFocused();
                SessionState.SetBool(DeliveryPending + ".started", true);
                return;
            }
            string report = File.Exists(WimWinterContractLiveRunner.ReportPath)
                ? File.ReadAllText(WimWinterContractLiveRunner.ReportPath) : string.Empty;
            if (!report.Contains("result=PASS") && !report.Contains("result=FAIL")) return;
            Require(report.Split('\n').Any(line => line.Trim() == "result=PASS")
                && !SessionState.GetBool(DeliveryPending + ".errors", true), "Faction delivery failed: " + report);
            Require(SessionState.GetString(DeliveryPending + ".source", "")
                == V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(), "Delivery source changed.");
            FinishDelivery(0);
        }
        catch (Exception error) { Debug.LogException(error); FinishDelivery(1); }
    }

    private static void FinishDelivery(int code)
    {
        SessionState.SetBool(DeliveryPending, false);
        EditorApplication.update -= PumpDelivery;
        Application.logMessageReceived -= ObserveDelivery;
        EditorApplication.Exit(code);
    }

    public static void RunFocusedContracts()
    {
        RunAuthoringAndEquipment();
        RunShopOwnership();
        RunRestoreAndEffects();
        RunFacilityConsumption();
    }

    public static void RunFacilityConsumption() => Execute("facility-consumption", () =>
        MigratedProducerEconomyServiceEntryE2EDebugScenarios.RunFacilityUseObserverBatch());

    public static void RunPhysicalDispositionIdentity() => Execute("physical-operation-identity", () =>
    {
        Debug.Log(PhysicalItemDispositionFactRecoveryDebugScenarios.Run());
        Debug.Log(PhysicalItemDispositionFactRecoveryDebugScenarios.RunEncodedIdentityContracts());
    });

    public static void RunAuthoringAndEquipment() => Execute("authoring-equipment", () =>
    {
        ContentReferenceSelectionDebugScenarios.RunAll();
        R26ReferenceOwnerGuardDebugScenarios.RunAll();
        BuildingAbilityRuntimeDebugScenarios.RunForBatch();
        var failures = ResearchEquipmentOverhaulDebugScenarios.ValidateModuleLifecycleForAutomation();
        Require(failures.Count == 0, string.Join(" | ", failures));
    });

    public static void RunShopOwnership() => Execute("shop-owner", () =>
        MigratedProducerEconomyEntryExtendedE2EDebugScenarios.RunDailyOfferOwnershipForBatch());

    public static void RunRestoreAndEffects() => Execute("restore-effects", () =>
    {
        Require(CharacterMedicalRestoreFaultScenarios.Run(), "Medical AI rollback failed.");
        Require(SurgeryRestoreFaultScenarios.Run(), "Surgery AI rollback failed.");
        Require(FacilityUseExecutionStateDebugScenarios.RunAll(true), "Facility use state roundtrip failed.");
        V20ContentResolutionRecoveryDebugScenarios.RunObserverBatch();
    });

    private static void Execute(string name, Action action)
    {
        Require(Application.isBatchMode && !Application.isPlaying
            && DungeonPersistencePaths.IsIsolatedVerification, "R27 requires isolated edit-mode persistence.");
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
            Debug.Log("R27_" + name + "=PASS");
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
