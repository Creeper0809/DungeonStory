using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class UiPlayModeRegressionBatchRunner
{
    private const string BatchRequestPath = "Temp/ui-playmode-regression-batch.request";
    private const string StatePath = "Temp/ui-playmode-regression-batch.state";
    private const string BatchReportPath = "Library/ui-playmode-regression-batch-report.txt";
    private const string BatchProgressPath = "Library/ui-playmode-regression-batch-progress.txt";
    private const string BuildPlacementRequestPath = "Temp/build-placement-ux.request";
    private const string CharacterClickRequestPath = "Temp/character-click-priority.request";
    private const string RoomInspectionRequestPath = "Temp/room-inspection-playmode.request";
    private const string ExpeditionEquipmentRequestPath = "Temp/expedition-equipment-playmode.request";
    private const string PhysicalItemPileRequestPath = "Temp/physical-item-pile-playmode.request";
    private const string PhysicalItemLogisticsRequestPath = "Temp/physical-item-logistics-playmode.request";
    private const string StartPartyRequestPath = "Temp/start-party-playmode.request";
    private const string SkillRuntimeRequestPath = "Temp/character-skill-runtime-playmode.request";
    private const string SkillRuntimeReportPath = "Temp/character-skill-runtime-playmode-report.txt";
    private const string GameplayVerificationScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string TitleVerificationScenePath = "Assets/Scenes/TitleScene.unity";
    private const double TargetTimeoutSeconds = 420d;
    private const double MissingReportGraceSeconds = 12d;

    private static readonly Dictionary<string, VerificationTarget> Targets =
        new Dictionary<string, VerificationTarget>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "ProductShell",
                new VerificationTarget(
                    "ProductShell",
                    DungeonProductShellPlayModeVerifier.RequestPath,
                    DungeonProductShellPlayModeVerifier.ReportPath,
                    DungeonProductShellPlayModeVerifier.RequestRunFromMenu,
                    text => text.StartsWith("PRODUCT_SHELL PASS", StringComparison.Ordinal))
            },
            {
                "SaveUi",
                new VerificationTarget(
                    "SaveUi",
                    DungeonSaveUiPlayModeVerifier.RequestPath,
                    DungeonSaveUiPlayModeVerifier.ReportPath,
                    DungeonSaveUiPlayModeVerifier.RequestRunFromMenu,
                    text => text.StartsWith("SAVE_UI PASS", StringComparison.Ordinal))
            },
            {
                "UnifiedUi",
                new VerificationTarget(
                    "UnifiedUi",
                    UnifiedUiPlayModeVerifier.RequestPath,
                    UnifiedUiPlayModeVerifier.ReportPath,
                    RequestUnifiedUiVerification,
                    text => text.Contains("RESULT=PASS"))
            },
            {
                "BuildPlacement",
                new VerificationTarget(
                    "BuildPlacement",
                    BuildPlacementRequestPath,
                    BuildPlacementUxPlayModeVerifier.ReportPath,
                    () => RequestMarker(BuildPlacementRequestPath),
                    text => text.Contains("RESULT=PASS"))
            },
            {
                "CharacterClick",
                new VerificationTarget(
                    "CharacterClick",
                    CharacterClickRequestPath,
                    CharacterClickPriorityPlayModeVerifier.ReportPath,
                    () => RequestMarker(CharacterClickRequestPath),
                    text => text.Contains("RESULT=PASS"))
            },
            {
                "RoomInspection",
                new VerificationTarget(
                    "RoomInspection",
                    RoomInspectionRequestPath,
                    RoomInspectionPlayModeVerifier.ReportPath,
                    () => RequestMarker(RoomInspectionRequestPath),
                    text => text.Contains("RESULT=PASS"))
            },
            {
                "ExpeditionEquipment",
                new VerificationTarget(
                    "ExpeditionEquipment",
                    ExpeditionEquipmentRequestPath,
                    ExpeditionEquipmentPlayModeVerifier.ReportPath,
                    () => RequestMarker(ExpeditionEquipmentRequestPath),
                    text => text.Contains("RESULT=PASS"))
            },
            {
                "PhysicalItemPile",
                new VerificationTarget(
                    "PhysicalItemPile",
                    PhysicalItemPileRequestPath,
                    PhysicalItemPilePlayModeVerifier.ReportPath,
                    () => RequestMarker(PhysicalItemPileRequestPath),
                    text => text.Contains("RESULT=PASS"))
            },
            {
                "PhysicalItemLogistics",
                new VerificationTarget(
                    "PhysicalItemLogistics",
                    PhysicalItemLogisticsRequestPath,
                    PhysicalItemLogisticsPlayModeVerifier.ReportPath,
                    () => RequestMarker(PhysicalItemLogisticsRequestPath),
                    text => text.Contains("RESULT=PASS"))
            },
            {
                "StartParty",
                new VerificationTarget(
                    "StartParty",
                    StartPartyRequestPath,
                    StartPartyPreparationPlayModeVerifier.ReportPath,
                    () => RequestMarker(StartPartyRequestPath),
                    text => text.Contains("errors=0; warnings=0; failures=0"))
            },
            {
                "SkillRuntime",
                new VerificationTarget(
                    "SkillRuntime",
                    SkillRuntimeRequestPath,
                    SkillRuntimeReportPath,
                    () => RequestMarker(SkillRuntimeRequestPath),
                    text => text.StartsWith("CHARACTER_SKILL_RUNTIME_PLAYMODE_PASS", StringComparison.Ordinal))
            }
        };

    static UiPlayModeRegressionBatchRunner()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
    }

    [MenuItem("DungeonStory/Debug/QA/Run Changed UI Regression Batch")]
    public static void RunChangedUiRegressionForBatchMode()
    {
        StartRun("Changed UI regression", new[] { "ProductShell", "SaveUi", "UnifiedUi" });
    }

    public static void RunProductShellForBatchMode()
    {
        StartRun("ProductShell regression", new[] { "ProductShell" });
    }

    public static void RunSaveUiForBatchMode()
    {
        StartRun("Save UI regression", new[] { "SaveUi" });
    }

    public static void RunUnifiedUiForBatchMode()
    {
        StartRun("Unified UI regression", new[] { "UnifiedUi" });
    }

    [InitializeOnEnterPlayMode]
    private static void EnsureRunnerOnEnterPlayMode(EnterPlayModeOptions options)
    {
        EditorApplication.delayCall += EnsureCurrentPlayModeRunnerFromState;
    }

    private static void StartRun(string runName, IReadOnlyList<string> targetNames)
    {
        Directory.CreateDirectory("Temp");
        File.Delete(StatePath);
        File.Delete(BatchReportPath);
        File.Delete(BatchProgressPath);

        foreach (string targetName in targetNames)
        {
            VerificationTarget target = GetTarget(targetName);
            File.Delete(target.RequestPath);
            File.Delete(target.ReportPath);
        }

        BatchState state = new BatchState(runName, targetNames.ToArray(), 0, DateTime.UtcNow.Ticks);
        WriteState(state);
        StartCurrentTarget(state);
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(StatePath) && File.Exists(BatchRequestPath))
        {
            string[] targetNames = File.ReadAllLines(BatchRequestPath)
                .SelectMany(line => line.Split(new[] { ',', '|', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToArray();
            File.Delete(BatchRequestPath);
            if (targetNames.Length == 0)
            {
                targetNames = new[]
                {
                    "BuildPlacement",
                    "CharacterClick",
                    "RoomInspection",
                    "ExpeditionEquipment",
                    "PhysicalItemPile",
                    "PhysicalItemLogistics",
                    "SkillRuntime"
                };
            }

            StartRun("Requested UI feature regression", targetNames);
            return;
        }

        if (!File.Exists(StatePath))
        {
            return;
        }

        BatchState state;
        try
        {
            state = ReadState();
        }
        catch (Exception ex)
        {
            Finish(false, "UI regression batch state could not be read.\n" + ex);
            return;
        }

        VerificationTarget target = GetTarget(state.CurrentTargetName);
        if (EditorApplication.isPlaying)
        {
            EnsurePlayModeRunner(target);
        }

        if (File.Exists(target.ReportPath))
        {
            string reportText = File.ReadAllText(target.ReportPath);
            bool passed = target.IsPass(reportText);
            if (!passed)
            {
                AppendProgress($"[FAIL] {target.Name} {FirstReportLine(reportText)}");
                Finish(false, BuildBatchSummary(state, target, reportText, "Target report failed."));
                return;
            }

            AppendProgress($"[PASS] {target.Name} {FirstReportLine(reportText)}");
            if (state.CurrentIndex + 1 >= state.TargetNames.Length)
            {
                Finish(true, BuildBatchSummary(state, target, reportText, "All targets passed."));
                return;
            }

            state = state.WithIndex(state.CurrentIndex + 1);
            WriteState(state);
            StartCurrentTarget(state);
            return;
        }

        double elapsed = new TimeSpan(DateTime.UtcNow.Ticks - state.TargetStartedUtcTicks).TotalSeconds;
        if (elapsed > TargetTimeoutSeconds)
        {
            Finish(false, BuildBatchSummary(state, target, string.Empty, $"Timed out after {elapsed:0.0}s."));
            return;
        }

        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(target.RequestPath))
        {
            EditorApplication.EnterPlaymode();
            return;
        }

        if (!EditorApplication.isPlayingOrWillChangePlaymode
            && !File.Exists(target.RequestPath)
            && elapsed > MissingReportGraceSeconds)
        {
            Finish(false, BuildBatchSummary(
                state,
                target,
                string.Empty,
                "Request disappeared while no PlayMode runner was active, and no report was written."));
        }
    }

    private static void EnsureCurrentPlayModeRunnerFromState()
    {
        if (!File.Exists(StatePath) || !EditorApplication.isPlaying)
        {
            return;
        }

        BatchState state = ReadState();
        EnsurePlayModeRunner(GetTarget(state.CurrentTargetName));
    }

    private static void EnsurePlayModeRunner(VerificationTarget target)
    {
        if (target.Name.Equals("ProductShell", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<DungeonProductShellVerificationRunner>() == null)
            {
                Debug.Log("UI regression batch creating ProductShell PlayMode runner.");
                GameObject runner = new GameObject("Product Shell Verification Runner");
                UnityEngine.Object.DontDestroyOnLoad(runner);
                runner.AddComponent<DungeonProductShellVerificationRunner>();
            }

            return;
        }

        if (target.Name.Equals("SaveUi", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<DungeonSaveUiVerificationRunner>() == null)
            {
                Debug.Log("UI regression batch creating Save UI PlayMode runner.");
                new GameObject("Save UI Verification Runner").AddComponent<DungeonSaveUiVerificationRunner>();
            }

            return;
        }

        if (target.Name.Equals("BuildPlacement", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<BuildPlacementUxPlayModeVerificationRunner>() == null)
            {
                new GameObject("Build Placement UX Verification Runner")
                    .AddComponent<BuildPlacementUxPlayModeVerificationRunner>();
            }

            return;
        }

        if (target.Name.Equals("CharacterClick", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<CharacterClickPriorityVerificationRunner>() == null)
            {
                new GameObject("Character Click Priority Verification Runner")
                    .AddComponent<CharacterClickPriorityVerificationRunner>();
            }

            return;
        }

        if (target.Name.Equals("RoomInspection", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<RoomInspectionPlayModeVerificationRunner>() == null)
            {
                new GameObject("Room Inspection PlayMode Verification Runner")
                    .AddComponent<RoomInspectionPlayModeVerificationRunner>();
            }

            return;
        }

        if (target.Name.Equals("ExpeditionEquipment", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<ExpeditionEquipmentPlayModeVerificationRunner>() == null)
            {
                new GameObject("Expedition Equipment PlayMode Verification Runner")
                    .AddComponent<ExpeditionEquipmentPlayModeVerificationRunner>();
            }

            return;
        }

        if (target.Name.Equals("PhysicalItemLogistics", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<PhysicalItemLogisticsPlayModeVerificationRunner>() == null)
            {
                new GameObject("Physical Item Logistics PlayMode Verification Runner")
                    .AddComponent<PhysicalItemLogisticsPlayModeVerificationRunner>();
            }

            return;
        }

        if (target.Name.Equals("PhysicalItemPile", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<PhysicalItemPilePlayModeVerificationRunner>() == null)
            {
                new GameObject("Physical Item Pile PlayMode Verification Runner")
                    .AddComponent<PhysicalItemPilePlayModeVerificationRunner>();
            }

            return;
        }

        if (target.Name.Equals("StartParty", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<StartPartyPreparationPlayModeRunner>() == null)
            {
                new GameObject("Start Party PlayMode Verification Runner")
                    .AddComponent<StartPartyPreparationPlayModeRunner>();
            }

            return;
        }

        if (target.Name.Equals("SkillRuntime", StringComparison.OrdinalIgnoreCase))
        {
            if (UnityEngine.Object.FindFirstObjectByType<CharacterSkillRuntimeProbeBatchRunner>() == null)
            {
                new GameObject("Character Skill Runtime Probe Batch Runner")
                    .AddComponent<CharacterSkillRuntimeProbeBatchRunner>();
            }

            return;
        }

        // UnifiedUiPlayModeVerifier owns its runner through InitializeOnEnterPlayMode.
        // Creating a second runner here is harmless but emits a warning, which makes
        // batch logs look dirtier than the verifier report.
    }

    private static void StartCurrentTarget(BatchState state)
    {
        VerificationTarget target = GetTarget(state.CurrentTargetName);
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        OpenVerificationScene(target);
        File.Delete(target.ReportPath);
        File.Delete(target.RequestPath);

        state = state.WithTargetStarted(DateTime.UtcNow.Ticks);
        WriteState(state);

        target.Request();
        Debug.Log($"UI regression batch requested {target.Name}: {target.ReportPath}");
        EditorApplication.delayCall += EnsurePlayModeRequested;
    }

    private static void EnsurePlayModeRequested()
    {
        if (!File.Exists(StatePath) || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        BatchState state = ReadState();
        VerificationTarget target = GetTarget(state.CurrentTargetName);
        if (File.Exists(target.RequestPath))
        {
            EditorApplication.EnterPlaymode();
        }
    }

    private static void RequestUnifiedUiVerification()
    {
        RequestMarker(UnifiedUiPlayModeVerifier.RequestPath);
    }

    private static void RequestMarker(string requestPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(requestPath) ?? "Temp");
        File.WriteAllText(requestPath, DateTime.UtcNow.ToString("O"));
    }

    private static void OpenVerificationScene(VerificationTarget target)
    {
        string verificationScenePath = target.Name.Equals("ProductShell", StringComparison.OrdinalIgnoreCase)
            ? TitleVerificationScenePath
            : GameplayVerificationScenePath;
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        string fullPath = Path.Combine(projectRoot, verificationScenePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("UI regression verification scene was not found.", fullPath);
        }

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!string.Equals(activeScene.path, verificationScenePath, StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(verificationScenePath, OpenSceneMode.Single);
        }
    }

    private static VerificationTarget GetTarget(string name)
    {
        if (!Targets.TryGetValue(name, out VerificationTarget target))
        {
            throw new InvalidOperationException("Unknown UI regression target: " + name);
        }

        return target;
    }

    private static void Finish(bool passed, string summary)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BatchReportPath) ?? "Library");
        File.WriteAllText(BatchReportPath, summary);
        File.Delete(StatePath);

        if (passed)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }

        if (ShouldExitEditorProcess())
        {
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    private static string BuildBatchSummary(
        BatchState state,
        VerificationTarget target,
        string targetReport,
        string resultDetail)
    {
        List<string> lines = new List<string>
        {
            resultDetail.StartsWith("All targets", StringComparison.Ordinal)
                ? "UI_REGRESSION_BATCH PASS"
                : "UI_REGRESSION_BATCH FAIL",
            $"run={state.RunName}",
            $"currentTarget={target.Name}",
            $"targetIndex={state.CurrentIndex + 1}/{state.TargetNames.Length}",
            $"targetReport={target.ReportPath}",
            $"detail={resultDetail}"
        };

        if (File.Exists(BatchProgressPath))
        {
            lines.Add("completedTargets:");
            lines.AddRange(File.ReadAllLines(BatchProgressPath));
        }

        if (!string.IsNullOrWhiteSpace(targetReport))
        {
            lines.Add("targetReportPreview:");
            lines.AddRange(targetReport
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                .Take(80));
        }

        return string.Join("\n", lines);
    }

    private static void AppendProgress(string line)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BatchProgressPath) ?? "Library");
        File.AppendAllText(BatchProgressPath, line + "\n");
    }

    private static string FirstReportLine(string reportText)
    {
        if (string.IsNullOrWhiteSpace(reportText))
        {
            return string.Empty;
        }

        return reportText
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? string.Empty;
    }

    private static bool ShouldExitEditorProcess()
    {
        return Application.isBatchMode
            || Environment.GetCommandLineArgs().Any(arg =>
                arg.Equals("-uiRegressionExit", StringComparison.OrdinalIgnoreCase));
    }

    private static BatchState ReadState()
    {
        string[] lines = File.ReadAllLines(StatePath);
        if (lines.Length < 4)
        {
            throw new InvalidOperationException("State file is incomplete.");
        }

        return new BatchState(
            lines[0],
            lines[3].Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries),
            int.Parse(lines[1]),
            long.Parse(lines[2]));
    }

    private static void WriteState(BatchState state)
    {
        Directory.CreateDirectory("Temp");
        File.WriteAllLines(
            StatePath,
            new[]
            {
                state.RunName,
                state.CurrentIndex.ToString(),
                state.TargetStartedUtcTicks.ToString(),
                string.Join("|", state.TargetNames)
            });
    }

    private readonly struct VerificationTarget
    {
        public VerificationTarget(
            string name,
            string requestPath,
            string reportPath,
            Action request,
            Func<string, bool> isPass)
        {
            Name = name;
            RequestPath = requestPath;
            ReportPath = reportPath;
            Request = request;
            IsPass = isPass;
        }

        public string Name { get; }
        public string RequestPath { get; }
        public string ReportPath { get; }
        public Action Request { get; }
        public Func<string, bool> IsPass { get; }
    }

    private readonly struct BatchState
    {
        public BatchState(string runName, string[] targetNames, int currentIndex, long targetStartedUtcTicks)
        {
            RunName = runName;
            TargetNames = targetNames;
            CurrentIndex = currentIndex;
            TargetStartedUtcTicks = targetStartedUtcTicks;
        }

        public string RunName { get; }
        public string[] TargetNames { get; }
        public int CurrentIndex { get; }
        public long TargetStartedUtcTicks { get; }
        public string CurrentTargetName => TargetNames[CurrentIndex];

        public BatchState WithIndex(int currentIndex)
        {
            return new BatchState(RunName, TargetNames, currentIndex, DateTime.UtcNow.Ticks);
        }

        public BatchState WithTargetStarted(long ticks)
        {
            return new BatchState(RunName, TargetNames, CurrentIndex, ticks);
        }
    }
}

public sealed class CharacterSkillRuntimeProbeBatchRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;
        string fastCommit = string.Empty;
        Exception failure = null;

        try
        {
            fastCommit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        yield return null;

        if (failure == null)
        {
            try
            {
                string result = CharacterSkillRuntimePlayModeVerifier.RunProbe();
                File.WriteAllText("Temp/character-skill-runtime-playmode-report.txt",
                    result + "\nFAST_PARTY_COMMIT " + fastCommit);
                Debug.Log(result);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        }

        if (failure != null)
        {
            string result = "CHARACTER_SKILL_RUNTIME_PLAYMODE_FAIL\n" + failure;
            File.WriteAllText("Temp/character-skill-runtime-playmode-report.txt", result);
            Debug.LogError(result);
        }

        EditorApplication.ExitPlaymode();
    }
}
