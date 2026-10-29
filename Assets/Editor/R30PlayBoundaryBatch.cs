using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VContainer;

public static class R30PlayBoundaryBatch
{
    private const string Key = "DungeonStory.R30.PlayBoundary";
    public static void RequestPause() => Request("pause");
    public static void RequestStaff() => Request("staff");
    public static void RequestDoor() => Request("door");
    public static void RequestFluid() => Request("fluid");
    public static void RequestCombatCommands() => Request("commands");
    public static void RequestExterior() => Request("exterior");

    private static string Output => Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
    private static string Mode => SessionState.GetString(Key + ".mode", "");
    private static string NativeReport => Mode switch
    {
        "pause" => Path.Combine(Output, "research-pause-native.txt"),
        "staff" => P1P2FeatureSurfacePlayModeVerifier.ReportPath,
        "door" => DoorAccessPlayModeVerifier.ReportPath,
        "fluid" => IndustrialInfrastructurePlayModeVerifier.ReportPath,
        "commands" => "Artifacts/QA/combat-v14-playmode-report.txt",
        "exterior" => Path.Combine(Output, "exterior-native.txt"),
        _ => throw new InvalidOperationException("Unknown R30 Play mode.")
    };

    private static void Request(string mode)
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
            && !EditorApplication.isPlayingOrWillChangePlaymode && Directory.Exists(Output),
            "R30 Play requires a fresh isolated batch and its explicit evidence directory.");
        Require(string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
            CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable)),
            "A prior narrative capture context must not be reused.");
        // These UI and infrastructure probes do not test narrative inference.
        // Reuse the isolated capture gate without supplying any model response.
        Environment.SetEnvironmentVariable(
            CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable, "1");
        SessionState.SetString(Key + ".mode", mode);
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".started", false);
        SessionState.SetBool(Key + ".leaving", false);
        SessionState.SetInt(Key + ".issues", 0);
        SessionState.SetString(Key + ".source", V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest());
        SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 180f);
        PreservePrevious(NativeReport);
        if (mode == "staff") PreservePrevious("Temp/p1-ui-staff.png");
        if (mode == "door")
        {
            PreservePrevious(DoorAccessPlayModeVerifier.DesktopCapturePath);
            PreservePrevious(DoorAccessPlayModeVerifier.MobileCapturePath);
        }
        Resume();
        if (mode == "staff")
        {
            Require(!File.Exists(P1P2FeatureSurfacePlayModeVerifier.RequestPath)
                && !File.Exists(P1P2FeatureSurfacePlayModeVerifier.StaffOnlyRequestPath),
                "A prior UI verification request still exists.");
            P1P2FeatureSurfacePlayModeVerifier.RequestStaffOnlyRunFromMenu();
            return;
        }
        EditorSceneManager.OpenScene("Assets/Scenes/GameplayScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.update -= Pump;
        EditorApplication.update += Pump;
        Application.logMessageReceived -= Observe;
        Application.logMessageReceived += Observe;
    }

    private static void Observe(string message, string stack, LogType type)
    {
        if (type is LogType.Warning or LogType.Error or LogType.Assert or LogType.Exception)
            SessionState.SetInt(Key + ".issues", SessionState.GetInt(Key + ".issues", 0) + 1);
    }

    private static void Pump()
    {
        try
        {
            Require(SessionState.GetInt(Key + ".issues", 0) == 0, "R30 Play emitted a console issue.");
            Require(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + ".deadline", 0),
                "R30 focused Play exceeded its bounded execution window.");
            if (!SessionState.GetBool(Key + ".started", false))
            {
                if (!EditorApplication.isPlaying) return;
                var scope = UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
                var owner = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
                if (scope?.Container == null || owner == null) return;
                var narrative = scope.Container.Resolve<ILocalLlmRuntimeProvider>()
                    .GetRequiredRuntime() as LocalLlmRequestQueue;
                Require(narrative != null && narrative.RunningCount == 0
                    && !narrative.IsBundledModelStarting && !narrative.IsBundledModelRunning
                    && !narrative.HasConfiguredEndpoint
                    && narrative.CaptureReadiness().State == LocalLlmRuntimeReadinessState.Starting,
                    "Non-target narrative startup was not safely deferred.");
                if (owner.CurrentOwnerActor == null)
                {
                    string prepared = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
                    Require(prepared.StartsWith("committed=True;", StringComparison.Ordinal)
                        && owner.CurrentOwnerActor != null, "Official starting-party command failed: " + prepared);
                }
                if (Mode != "staff")
                {
                    IGameSpeedController speed = scope.Container.Resolve<IGameSpeedController>();
                    speed.SetSpeed(1);
                    speed.SetPaused(false);
                    if (Mode == "pause")
                    {
                        Require(!File.Exists(ResearchTreePlayModeVerifier.RequestPath), "A prior research request exists.");
                        var runner = new GameObject("R30 pause verification").AddComponent<ResearchTreeVerificationRunner>();
                        runner.PauseBoundaryOnly = true;
                        runner.ReportPathOverride = NativeReport;
                        runner.ProgressPathOverride = Path.Combine(Output, "research-pause-progress.txt");
                    }
                    else if (Mode == "door") DoorAccessPlayModeVerifier.RunFromMenu();
                    else if (Mode == "fluid") IndustrialInfrastructurePlayModeVerifier.RunFluidSnapshotOnly();
                    else if (Mode == "commands") CombatV14PlayModeVerifier.StartCombatCommandProbe();
                    else if (Mode == "exterior")
                    {
                        Require(ExteriorActivityDebugScenarios.RunPlayModeZoneMembershipOnly(true),
                            "Exterior query membership or restore failed.");
                        WriteNew(NativeReport, "result=PASS\n");
                    }
                }
                SessionState.SetBool(Key + ".started", true);
                return;
            }
            if (!File.Exists(NativeReport)) return;
            string report = File.ReadAllText(NativeReport);
            bool passed;
            if (Mode == "staff")
            {
                Match result = Regex.Match(report, @"(?m)^RESULT rowsPassed=(\d+)/(\d+); rowsFailed=(\d+)\r?$");
                passed = result.Success && int.Parse(result.Groups[1].Value) > 0
                    && result.Groups[1].Value == result.Groups[2].Value && result.Groups[3].Value == "0"
                    && report.Contains("STALE_STAFF_COMMAND_REJECTION");
            }
            else passed = Regex.Matches(report, @"(?im)^RESULT=PASS(?:;[^\r\n]*)?\r?$").Count == 1;
            Require(passed, "Focused " + Mode + " report did not pass: " + report);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!SessionState.GetBool(Key + ".leaving", false))
                {
                    SessionState.SetBool(Key + ".leaving", true);
                    EditorApplication.ExitPlaymode();
                }
                return;
            }
            Require(SessionState.GetString(Key + ".source", "")
                == V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(), "R30 Play source changed.");
            string copy = Path.Combine(Output, Mode + "-native-report.txt");
            if (Path.GetFullPath(copy) != Path.GetFullPath(NativeReport))
                WriteNew(copy, report);
            WriteNew(Path.Combine(Output, "r30-" + Mode + "-play.txt"), "result=PASS\nsource="
                + SessionState.GetString(Key + ".source", "") + "\nconsoleIssues=0\nreportSha256="
                + Sha(File.ReadAllBytes(NativeReport))
                + "\nbackgroundNarrative=deferred-without-responses;not-tested\n");
            Finish(0);
        }
        catch (Exception error)
        {
            string path = Path.Combine(Output, "r30-play-failure.txt");
            if (!File.Exists(path)) WriteNew(path, error.ToString());
            Debug.LogException(error);
            Finish(1);
        }
    }

    private static void PreservePrevious(string path)
    {
        string absolute = Path.GetFullPath(path);
        string root = Path.GetDirectoryName(Application.dataPath) + Path.DirectorySeparatorChar;
        Require(absolute.StartsWith(root, StringComparison.Ordinal), "QA evidence must stay in this project.");
        if (!File.Exists(absolute)) return;
        byte[] previous = File.ReadAllBytes(absolute);
        string copy = Path.Combine(Output, "prior-" + Mode + "-" + Path.GetFileName(absolute));
        using (var stream = new FileStream(copy, FileMode.CreateNew, FileAccess.Write))
            stream.Write(previous, 0, previous.Length);
        Require(Sha(File.ReadAllBytes(copy)) == Sha(previous)
            && Sha(File.ReadAllBytes(absolute)) == Sha(previous), "Prior QA evidence changed while archiving.");
        File.Delete(absolute);
    }

    private static string Sha(byte[] data)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
    }
    private static void WriteNew(string path, string value)
    {
        using var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew));
        writer.Write(value);
    }
    private static void Finish(int code)
    {
        SessionState.SetBool(Key, false);
        EditorApplication.update -= Pump;
        Application.logMessageReceived -= Observe;
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            Environment.SetEnvironmentVariable(
                CaptivityWildlifeLifecyclePlayModeVerifier.LiveNarrativeCaptureVariable, null);
        EditorApplication.Exit(code);
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
}
