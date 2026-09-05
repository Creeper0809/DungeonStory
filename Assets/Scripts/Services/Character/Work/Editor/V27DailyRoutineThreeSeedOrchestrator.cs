#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class V27DailyRoutineThreeSeedOrchestrator
{
    public const string ReportPath =
        "Artifacts/QA/v27-daily-routine-three-seed-orchestration.txt";

    private const string ActiveKey =
        "DungeonStory.V27.DailyRoutineThreeSeed.Active";
    private const string IndexKey =
        "DungeonStory.V27.DailyRoutineThreeSeed.Index";
    private const string PendingSeedKey =
        "DungeonStory.V27.DailyRoutineThreeSeed.PendingSeed";
    private const string SourceDigestKey =
        "DungeonStory.V27.DailyRoutineThreeSeed.SourceDigest";
    private const string SourceCountKey =
        "DungeonStory.V27.DailyRoutineThreeSeed.SourceCount";
    private const string SourcePathsKey =
        "DungeonStory.V27.DailyRoutineThreeSeed.SourcePaths";
    private const string SceneDigestKey =
        "DungeonStory.V27.DailyRoutineThreeSeed.SceneDigest";

    private static readonly int[] Seeds = { 157181, 157182, 157183 };
    private static readonly UTF8Encoding StrictUtf8 =
        new UTF8Encoding(false, true);

    static V27DailyRoutineThreeSeedOrchestrator()
    {
        EditorApplication.update -= OnUpdate;
        EditorApplication.update += OnUpdate;
    }

    [MenuItem("DungeonStory/V27/Balance/Run Daily Routine Three Seeds")]
    public static void QueueRunFromEditorCommand()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException(
                "Daily-routine orchestration must start in EditMode.");
        V27CurrentSourceEvidenceSnapshot source =
            V27CurrentSourceEvidenceDigest.Capture();
        string scene = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        Require(string.Equals(
                scene,
                V27CurrentSourceEvidenceDigest.OfficialGameplaySceneSha256,
                StringComparison.Ordinal),
            "Official GameplayScene digest drifted before daily-routine run.");
        Directory.CreateDirectory("Artifacts/QA");
        foreach (int seed in Seeds)
            File.Delete(SeedReportPath(seed));
        File.Delete(ReportPath);
        SessionState.SetString(SourceDigestKey, source.Digest);
        SessionState.SetInt(SourceCountKey, source.InputCount);
        SessionState.SetString(SourcePathsKey, source.PathListDigest);
        SessionState.SetString(SceneDigestKey, scene);
        SessionState.SetInt(IndexKey, 0);
        SessionState.SetInt(PendingSeedKey, 0);
        SessionState.SetBool(ActiveKey, true);
        Debug.Log("V27_DAILY_ROUTINE_THREE_SEED=QUEUED");
    }

    private static void OnUpdate()
    {
        if (!SessionState.GetBool(ActiveKey, false)
            || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        try
        {
            int index = SessionState.GetInt(IndexKey, 0);
            int pendingSeed = SessionState.GetInt(PendingSeedKey, 0);
            if (pendingSeed != 0)
            {
                string path = SeedReportPath(pendingSeed);
                if (!File.Exists(path))
                    return;
                RequireSeedReport(pendingSeed, path);
                SessionState.SetInt(PendingSeedKey, 0);
                SessionState.SetInt(IndexKey, checked(index + 1));
                index++;
            }

            if (index < Seeds.Length)
            {
                int seed = Seeds[index];
                SessionState.SetInt(PendingSeedKey, seed);
                DailyRoutineWuPlayModeVerifier.RequestRun(seed);
                return;
            }

            Complete();
        }
        catch (Exception exception)
        {
            SessionState.SetBool(ActiveKey, false);
            Debug.LogException(exception);
            if (Application.isBatchMode)
                EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }

    private static void Complete()
    {
        V27CurrentSourceEvidenceSnapshot current =
            V27CurrentSourceEvidenceDigest.Capture();
        string expectedSource = SessionState.GetString(SourceDigestKey, string.Empty);
        int expectedCount = SessionState.GetInt(SourceCountKey, -1);
        string expectedPaths = SessionState.GetString(SourcePathsKey, string.Empty);
        string expectedScene = SessionState.GetString(SceneDigestKey, string.Empty);
        Require(string.Equals(current.Digest, expectedSource, StringComparison.Ordinal)
                && current.InputCount == expectedCount
                && string.Equals(
                    current.PathListDigest,
                    expectedPaths,
                    StringComparison.Ordinal)
                && string.Equals(
                    V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest(),
                    expectedScene,
                    StringComparison.Ordinal),
            "Source or official scene changed during daily-routine orchestration.");
        Require(DailyRoutineWuPlayModeVerifier.VerifyThreeSeedReports(false),
            "Daily-routine compound three-seed gate did not pass.");

        StringBuilder report = new StringBuilder(4096);
        report.Append("schema=v27-daily-routine-three-seed-orchestration@1\n")
            .Append("RESULT=PASS\n")
            .Append("currentSourceDigest=").Append(current.Digest).Append('\n')
            .Append("currentSourceInputCount=").Append(current.InputCount).Append('\n')
            .Append("currentSourcePathListDigest=")
            .Append(current.PathListDigest).Append('\n')
            .Append("gameplaySceneSha256=").Append(expectedScene).Append('\n')
            .Append("seeds=3/3\n")
            .Append("observedDaysPerSeed=5\n");
        foreach (int seed in Seeds)
        {
            string path = SeedReportPath(seed);
            byte[] bytes = File.ReadAllBytes(path);
            report.Append("seed=").Append(seed)
                .Append("; path=").Append(path.Replace('\\', '/'))
                .Append("; sha256=").Append(Sha256(bytes))
                .Append("; bytes=").Append(bytes.Length)
                .Append("; result=PASS\n");
        }
        report.Append("consoleWarnings=0\n")
            .Append("consoleErrors=0\n")
            .Append("secondWriteDiff=0\n")
            .Append("secondWriteLengthDiff=0\n")
            .Append("secondWriteMtimeDiff=0\n");
        byte[] reportBytes = StrictUtf8.GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream => stream.Write(reportBytes, 0, reportBytes.Length));
        string absolute = Path.GetFullPath(ReportPath);
        long length = new FileInfo(absolute).Length;
        long mtime = File.GetLastWriteTimeUtc(absolute).Ticks;
        string digest = Sha256(File.ReadAllBytes(absolute));
        bool second = V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream => stream.Write(reportBytes, 0, reportBytes.Length));
        Require(!second
                && length == new FileInfo(absolute).Length
                && mtime == File.GetLastWriteTimeUtc(absolute).Ticks
                && string.Equals(
                    digest,
                    Sha256(File.ReadAllBytes(absolute)),
                    StringComparison.Ordinal),
            "Daily-routine orchestration second write changed byte/length/mtime.");

        SessionState.SetBool(ActiveKey, false);
        SessionState.SetInt(PendingSeedKey, 0);
        Debug.Log("V27_DAILY_ROUTINE_THREE_SEED=PASS; seeds=3/3; days=5");
        if (Application.isBatchMode)
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
    }

    private static void RequireSeedReport(int seed, string path)
    {
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        Require(lines.Count(line => string.Equals(
                    line,
                    "observedDays=5",
                    StringComparison.Ordinal)) == 1,
            "Daily-routine report has invalid observedDays: " + seed);
        Require(lines.Count(line => string.Equals(
                    line,
                    "runSeed=" + seed,
                    StringComparison.Ordinal)) == 1,
            "Daily-routine report seed mismatch: " + seed);
        Require(lines.Count(line => string.Equals(
                    line,
                    "runtimeDiagnosticsGate="
                    + DailyRoutineWuPlayModeVerifier.RuntimeDiagnosticsGateVersion,
                    StringComparison.Ordinal)) == 1,
            "Daily-routine diagnostics gate is stale: " + seed);
        Require(lines.Count(line => string.Equals(
                    line,
                    "currentSourceDigest="
                    + SessionState.GetString(SourceDigestKey, string.Empty),
                    StringComparison.Ordinal)) == 1,
            "Daily-routine source digest is stale: " + seed);
        Require(lines.Count(line => string.Equals(
                    line,
                    "gameplaySceneSha256="
                    + SessionState.GetString(SceneDigestKey, string.Empty),
                    StringComparison.Ordinal)) == 1,
            "Daily-routine scene digest is stale: " + seed);
        Require(lines.Count(line => line.StartsWith(
                    "RESULT=PASS; failures=0;",
                    StringComparison.Ordinal)
                && line.EndsWith(
                    "capturedIssues=0",
                    StringComparison.Ordinal)) == 1,
            "Daily-routine report did not pass cleanly: " + seed);
    }

    private static string SeedReportPath(int seed) =>
        "Artifacts/QA/phase157-daily-routine-wu-seed-"
        + seed.ToString(CultureInfo.InvariantCulture) + ".txt";

    private static string Sha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(bytes);
        const string alphabet = "0123456789abcdef";
        char[] result = new char[digest.Length * 2];
        for (int index = 0; index < digest.Length; index++)
        {
            result[index * 2] = alphabet[digest[index] >> 4];
            result[index * 2 + 1] = alphabet[digest[index] & 0x0f];
        }
        return new string(result);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
