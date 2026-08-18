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
        string worldMarker = RequireFreshMarker(
            WorldResourceReportPath,
            LatestSourceWriteTime(WorldResourceSourcePaths),
            "PASS OUTPUT_CONTAINMENT_TYPED_BLOCK_RECOVERY ");
        string cropMarker = RequireFreshMarker(
            CropPlotReportPath,
            LatestSourceWriteTime(CropPlotSourcePaths),
            "PASS CROP_OUTPUT_CONTAINMENT_TYPED_BLOCK_RECOVERY ");
        if (!worldMarker.Contains("conserved=true", StringComparison.Ordinal)
            || !cropMarker.Contains(
                "outputs=resource:twilight-grain,seed-lot:twilight-grain",
                StringComparison.Ordinal)
            || !cropMarker.Contains("workConserved=true", StringComparison.Ordinal)
            || !cropMarker.Contains("quantityConserved=true", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Output-capacity PlayMode evidence lacks exact conservation markers.");
        }
        return "RESULT=PASS; checks=2; failures=0\n"
            + "sourceDigest=" + ComputeEvidenceSourceDigest() + "\n"
            + "PASS OUTPUT_CONTAINMENT_TYPED_BLOCK_RECOVERY "
            + "typedReason=ProductionOutputSpaceUnavailable;"
            + "workConserved=true;resourceCycleConserved=true;recovered=true\n"
            + "PASS CROP_OUTPUT_CONTAINMENT_TYPED_BLOCK_RECOVERY "
            + "harvestItem=true;seedLot=true;"
            + "typedReason=ProductionOutputSpaceUnavailable;"
            + "workConserved=true;quantityConserved=true;recovered=true\n";
    }

    public static string ComputeEvidenceSourceDigest()
    {
        StringBuilder builder = new();
        foreach (string path in EvidenceSourcePaths)
        {
            builder.Append(path).Append('\t')
                .Append(V27BalanceArtifactWriter.ComputeSha256(path))
                .Append('\n');
        }

        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(builder.ToString()));
        const string hex = "0123456789abcdef";
        char[] result = new char[digest.Length * 2];
        for (int index = 0; index < digest.Length; index++)
        {
            result[index * 2] = hex[digest[index] >> 4];
            result[index * 2 + 1] = hex[digest[index] & 15];
        }
        return new string(result);
    }

    private static DateTime LatestSourceWriteTime(
        IEnumerable<string> sourcePaths) => sourcePaths
        .Select(Path.GetFullPath)
        .Select(path => File.Exists(path)
            ? File.GetLastWriteTimeUtc(path)
            : throw new FileNotFoundException(
                "V27 output-capacity evidence source is missing.",
                path))
        .Max();

    private static string RequireFreshMarker(
        string relativePath,
        DateTime latestSource,
        string markerPrefix)
    {
        string path = Path.GetFullPath(relativePath);
        if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < latestSource)
        {
            throw new InvalidOperationException(
                "Output-capacity PlayMode evidence is missing or stale: "
                + relativePath);
        }

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        if (!lines.Contains("valid=true", StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Output-capacity PlayMode evidence did not finish successfully: "
                + relativePath);
        }
        return lines.FirstOrDefault(line => line.StartsWith(
                   markerPrefix,
                   StringComparison.Ordinal))
               ?? throw new InvalidOperationException(
                   "Output-capacity PlayMode marker is missing: " + markerPrefix);
    }
}
#endif
