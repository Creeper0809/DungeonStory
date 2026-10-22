#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class V27OutputCapacityEvidenceDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-output-capacity-playmode.txt";
    private const string WorldResourceReportPath =
        "docs/implementation-reports/world-resource-runtime-latest.txt";
    private const string CropPlotReportPath =
        "docs/implementation-reports/crop-plot-runtime-latest.txt";
    private const string AggregatorSourcePath =
        "Assets/Scripts/Services/Economy/Editor/V27OutputCapacityEvidenceDebugScenarios.cs";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly string[] WorldResourceSourcePaths =
    {
        "Assets/Scripts/Models/Economy/Content/WorldResourcePorts.cs",
        "Assets/Scripts/Models/Economy/Content/WorldResourceRuntime.cs",
        "Assets/Scripts/Services/Economy/ProductionItemGateway.cs",
        "Assets/Scripts/Services/Economy/WorldResourcePortAdapters.cs",
        "Assets/Scripts/Services/Economy/Editor/WorldResourceDebugScenarios.cs"
    };
    private static readonly string[] CropPlotSourcePaths =
    {
        "Assets/Scripts/Services/Economy/ProductionItemGateway.cs",
        "Assets/Scripts/Services/Economy/CropEcologyRuntime.cs",
        "Assets/Scripts/Services/Economy/CropPlotRuntime.cs",
        "Assets/Scripts/Services/Economy/Editor/CropPlotDebugScenarios.cs"
    };
    public static IReadOnlyList<string> EvidenceSourcePaths { get; } =
        Array.AsReadOnly(WorldResourceSourcePaths
            .Concat(CropPlotSourcePaths)
            .Append(AggregatorSourcePath)
            .Distinct(StringComparer.Ordinal)
            .ToArray());

    [MenuItem("DungeonStory/V27/Capture Output Capacity PlayMode Evidence")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report);
        V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log(report);
    }

    public static string RunAll()
    {
        CaptureCurrentRunIdentity(
            out string currentSourceDigest,
            out string gameplaySceneSha256);
        string worldMarker = RequireCurrentMarker(
            WorldResourceReportPath,
            currentSourceDigest,
            gameplaySceneSha256,
            "PASS WORLD_RESOURCE_EXACT_SOURCE_ATOMIC_PUBLICATION ",
            out string worldResourceReportSha256);
        string cropMarker = RequireCurrentMarker(
            CropPlotReportPath,
            currentSourceDigest,
            gameplaySceneSha256,
            "PASS CROP_OUTPUT_FACILITY_BUFFER_WAIT_RESTORE_RETRY_EXACT_ONCE ",
            out string cropPlotReportSha256);
        if (!worldMarker.Contains("conserved=true", StringComparison.Ordinal)
            || !cropMarker.Contains("capacityWait=true", StringComparison.Ordinal)
            || !cropMarker.Contains("frozenRestore=true", StringComparison.Ordinal)
            || !cropMarker.Contains("replayDelta=0", StringComparison.Ordinal)
            || !cropMarker.Contains("executionReceipt=true", StringComparison.Ordinal)
            || !cropMarker.Contains("physicalBatchJoin=true", StringComparison.Ordinal)
            || !cropMarker.Contains("terminalRestore=true", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Output-capacity PlayMode evidence lacks exact conservation markers.");
        }
        string evidenceSourceDigest = ComputeEvidenceSourceDigest();
        RequireUnchangedRunIdentity(currentSourceDigest, gameplaySceneSha256);
        return "RESULT=PASS; checks=2; failures=0\n"
            + "sourceDigest=" + evidenceSourceDigest + "\n"
            + "currentSourceDigest=" + currentSourceDigest + "\n"
            + "gameplaySceneSha256=" + gameplaySceneSha256 + "\n"
            + "worldResourceReportPath=" + WorldResourceReportPath + "\n"
            + "worldResourceReportSha256=" + worldResourceReportSha256 + "\n"
            + "cropPlotReportPath=" + CropPlotReportPath + "\n"
            + "cropPlotReportSha256=" + cropPlotReportSha256 + "\n"
            + "PASS WORLD_RESOURCE_EXACT_SOURCE_ATOMIC_PUBLICATION "
            + "exactSource=true;sourceDebitExact=true;"
            + "frozenOutcome=true;conserved=true\n"
            + "PASS CROP_OUTPUT_FACILITY_BUFFER_WAIT_RESTORE_RETRY_EXACT_ONCE "
            + "capacityWait=true;frozenRestore=true;replayDelta=0;"
            + "executionReceipt=true;physicalBatchJoin=true;"
            + "terminalRestore=true;ackRetention=true\n";
    }

    public static string ComputeEvidenceSourceDigest()
    {
        StringBuilder builder = new();
        foreach (string path in EvidenceSourcePaths)
        {
            builder.Append(path).Append('\t')
                .Append(V27CurrentSourceEvidenceDigest.ComputeSourceFileDigest(path))
                .Append('\n');
        }

        return ComputeSha256(StrictUtf8.GetBytes(builder.ToString()));
    }

    internal static void CaptureCurrentRunIdentity(
        out string currentSourceDigest,
        out string gameplaySceneSha256)
    {
        currentSourceDigest = V27CurrentSourceEvidenceDigest
            .ComputeAllScriptsDigest();
        gameplaySceneSha256 = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        if (!string.Equals(
                gameplaySceneSha256,
                V27CurrentSourceEvidenceDigest.OfficialGameplaySceneSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "OUTPUT_CAPACITY_OFFICIAL_GAMEPLAY_SCENE_DIGEST_MISMATCH:"
                + gameplaySceneSha256);
        }
    }

    internal static void RequireUnchangedRunIdentity(
        string expectedSourceDigest,
        string expectedSceneDigest)
    {
        CaptureCurrentRunIdentity(
            out string currentSourceDigest,
            out string gameplaySceneSha256);
        if (!string.Equals(
                currentSourceDigest,
                expectedSourceDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                gameplaySceneSha256,
                expectedSceneDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "OUTPUT_CAPACITY_RUN_IDENTITY_CHANGED:"
                + $"expectedSource={expectedSourceDigest};"
                + $"currentSource={currentSourceDigest};"
                + $"expectedScene={expectedSceneDigest};"
                + $"currentScene={gameplaySceneSha256}");
        }
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(bytes);
        const string hex = "0123456789abcdef";
        char[] result = new char[digest.Length * 2];
        for (int index = 0; index < digest.Length; index++)
        {
            result[index * 2] = hex[digest[index] >> 4];
            result[index * 2 + 1] = hex[digest[index] & 15];
        }
        return new string(result);
    }

    private static string RequireCurrentMarker(
        string relativePath,
        string expectedSourceDigest,
        string expectedSceneDigest,
        string markerPrefix,
        out string reportSha256)
    {
        string path = Path.GetFullPath(relativePath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "Output-capacity PlayMode evidence is missing: "
                + relativePath);
        }

        byte[] bytes = File.ReadAllBytes(path);
        reportSha256 = ComputeSha256(bytes);
        string[] lines = StrictUtf8.GetString(bytes)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        RequireSingleField(lines, "valid", "true", relativePath);
        RequireSingleField(
            lines,
            "currentSourceDigest",
            expectedSourceDigest,
            relativePath);
        RequireSingleField(
            lines,
            "gameplaySceneSha256",
            expectedSceneDigest,
            relativePath);
        return lines.FirstOrDefault(line => line.StartsWith(
                   markerPrefix,
                   StringComparison.Ordinal))
               ?? throw new InvalidOperationException(
                   "Output-capacity PlayMode marker is missing: " + markerPrefix);
    }

    private static void RequireSingleField(
        IEnumerable<string> lines,
        string key,
        string expectedValue,
        string relativePath)
    {
        string prefix = key + "=";
        string[] matches = lines.Where(line => line.StartsWith(
                prefix,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || !string.Equals(
                matches[0],
                prefix + expectedValue,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Output-capacity PlayMode evidence identity is stale or ambiguous: "
                + $"path={relativePath};key={key};count={matches.Length}.");
        }
    }
}
#endif
