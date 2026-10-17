using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class GameplayOutcomeLedgerPhase80PlayerBuildRunner
{
    private const string EvidenceDirectory =
        "Artifacts/QA/GameplayOutcomeLedgerPhase80PlayerBuild-20260919-r3";
    private const string PlayerPath = EvidenceDirectory + "/Player/DungeonStory.exe";
    private const string ReportPath = EvidenceDirectory + "/player-build-report.txt";

    private static readonly string[] Scenes =
    {
        "Assets/Scenes/TitleScene.unity",
        "Assets/Scenes/StartPreparationScene.unity",
        "Assets/Scenes/GameplayScene.unity"
    };

    public static void RunBatch()
    {
        string evidencePath = Path.GetFullPath(EvidenceDirectory);
        if (Directory.Exists(evidencePath))
        {
            Debug.LogError("Phase80 Player evidence is immutable and already exists: " + evidencePath);
            EditorApplication.Exit(1);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PlayerPath))
            ?? throw new InvalidOperationException("Player output directory is missing."));

        try
        {
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.StrictMode
            };

            BuildReport build = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = build.summary;
            bool passed = summary.result == BuildResult.Succeeded && summary.totalErrors == 0;
            string report = string.Join(
                Environment.NewLine,
                passed ? "PHASE80_PLAYER_BUILD=PASS" : "PHASE80_PLAYER_BUILD=FAIL",
                "unityVersion=" + Application.unityVersion,
                "target=" + summary.platform,
                "result=" + summary.result,
                "errors=" + summary.totalErrors,
                "warnings=" + summary.totalWarnings,
                "sizeBytes=" + summary.totalSize,
                "duration=" + summary.totalTime,
                "output=" + Path.GetFullPath(PlayerPath),
                "unityMcpUsed=false",
                "dungeonPlayerMcpUsed=false");
            File.WriteAllText(ReportPath, report + Environment.NewLine);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                ReportPath,
                "PHASE80_PLAYER_BUILD=FAIL" + Environment.NewLine
                + exception.GetType().FullName + ": " + exception.Message + Environment.NewLine
                + "unityMcpUsed=false" + Environment.NewLine
                + "dungeonPlayerMcpUsed=false" + Environment.NewLine);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
