#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class V27BalanceBuilderNoClobberDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-builder-no-clobber.txt";

    private static readonly string[] CapturedRoots =
    {
        "Assets/Resources/SO",
        "Assets/Images/MedicalFacilities"
    };

    private static readonly string[] EvidenceSourcePaths =
    {
        "Assets/Scripts/Services/Economy/Editor/V27BalanceBuilderNoClobberDebugScenarios.cs",
        "Assets/Scripts/Services/Economy/Editor/V23RecipeProcessClassAuthoring.cs",
        "Assets/Scripts/Services/Economy/Editor/ResourceEconomyAssetBuilder.cs",
        "Assets/Scripts/Services/Economy/Editor/ProductionWorkshopContentAssetBuilder.cs",
        "Assets/Scripts/Services/Economy/Editor/V22ApparelContentAssetBuilder.cs",
        "Assets/Scripts/Services/Economy/Editor/V19CropEcologyContentAssetBuilder.cs",
        "Assets/Scripts/Services/Research/Editor/ResearchOverhaulContentAssetBuilder.cs",
        "Assets/Scripts/Services/Medical/Editor/SurgeryContentAssetBuilder.cs"
    };

    private static readonly BuilderStep[] Steps =
    {
        new("resource-economy-and-workshops", ResourceEconomyAssetBuilder.Rebuild),
        new("v22-apparel", V22ApparelContentAssetBuilder.EnsureAssets),
        new("v19-crop-ecology", V19CropEcologyContentAssetBuilder.Build),
        new("research-overhaul", ResearchOverhaulContentAssetBuilder.EnsureAssets),
        new("surgery", SurgeryContentAssetBuilder.EnsureAssets)
    };

    [MenuItem("DungeonStory/V27/Verify Content Builders Do Not Clobber Approved Balance")]
    public static void RunFromMenu()
    {
        SortedDictionary<string, string> baseline = CaptureFileHashes();
        StringBuilder details = new();
        foreach (BuilderStep step in Steps)
        {
            step.Execute();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SortedDictionary<string, string> current = CaptureFileHashes();
            string[] changed = FindChanges(baseline, current);
            if (changed.Length != 0)
            {
                string failure = BuildReport(
                    passed: false,
                    current,
                    details,
                    step.Name,
                    changed);
                WriteReport(failure);
                throw new InvalidOperationException(
                    $"V27 balance builder '{step.Name}' changed {changed.Length} "
                    + "already-authored files: "
                    + string.Join(",", changed.Take(20)));
            }

            details.Append("PASS\tBUILDER_NO_CLOBBER\tbuilder=")
                .Append(step.Name)
                .Append(";files=")
                .Append(current.Count)
                .Append(";changes=0\n");
            baseline = current;
        }

        string report = BuildReport(
            passed: true,
            baseline,
            details,
            string.Empty,
            Array.Empty<string>());
        WriteReport(report);
        Debug.Log(report);
    }

    public static void RequireFreshEvidence()
    {
        string path = Path.GetFullPath(ReportPath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "V27 builder no-clobber evidence is missing.");
        }

        string first = File.ReadLines(path, new UTF8Encoding(false, true))
            .FirstOrDefault() ?? string.Empty;
        if (!first.StartsWith(
                $"RESULT=PASS; builders={Steps.Length}; files=",
                StringComparison.Ordinal)
            || !first.Contains("; changes=0;", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "V27 builder no-clobber evidence did not pass: " + first);
        }

        string expectedSourceDigest = ComputeEvidenceSourceDigest();
        string expectedAssetDigest = ComputeAggregateDigest(CaptureFileHashes());
        if (!string.Equals(
                ParseToken(first, "sourceDigest="),
                expectedSourceDigest,
                StringComparison.Ordinal)
            || !string.Equals(
                ParseToken(first, "assetDigest="),
                expectedAssetDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "V27 builder no-clobber evidence is stale.");
        }
    }

    public static string ComputeEvidenceSourceDigest()
    {
        StringBuilder source = new();
        foreach (string path in EvidenceSourcePaths)
        {
            source.Append(path)
                .Append('\t')
                .Append(V27BalanceArtifactWriter.ComputeSha256(path))
                .Append('\n');
        }
        return HashText(source.ToString());
    }

    private static string BuildReport(
        bool passed,
        IReadOnlyDictionary<string, string> snapshot,
        StringBuilder details,
        string failedBuilder,
        IReadOnlyList<string> changes)
    {
        StringBuilder report = new();
        report.Append("RESULT=")
            .Append(passed ? "PASS" : "FAIL")
            .Append("; builders=")
            .Append(Steps.Length)
            .Append("; files=")
            .Append(snapshot.Count)
            .Append("; changes=")
            .Append(changes.Count)
            .Append("; sourceDigest=")
            .Append(ComputeEvidenceSourceDigest())
            .Append("; assetDigest=")
            .Append(ComputeAggregateDigest(snapshot))
            .Append(";\n")
            .Append(details);
        if (!passed)
        {
            report.Append("FAIL\tBUILDER_CLOBBER\tbuilder=")
                .Append(failedBuilder)
                .Append(";changes=")
                .Append(changes.Count)
                .Append('\n');
            foreach (string path in changes)
            {
                report.Append("CHANGED\t")
                    .Append(path)
                    .Append('\n');
            }
        }
        return report.ToString();
    }

    private static SortedDictionary<string, string> CaptureFileHashes()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        SortedDictionary<string, string> result =
            new(StringComparer.Ordinal);
        foreach (string relativeRoot in CapturedRoots)
        {
            string fullRoot = Path.Combine(
                projectRoot,
                relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(fullRoot))
            {
                throw new InvalidOperationException(
                    $"Builder snapshot root is missing: {relativeRoot}.");
            }

            foreach (string fullPath in Directory.EnumerateFiles(
                         fullRoot,
                         "*",
                         SearchOption.AllDirectories))
            {
                string relativePath = fullPath
                    .Substring(projectRoot.Length + 1)
                    .Replace('\\', '/');
                result.Add(
                    relativePath,
                    V27BalanceArtifactWriter.ComputeSha256(relativePath));
            }
        }
        return result;
    }

    private static string[] FindChanges(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after) =>
        before.Keys
            .Concat(after.Keys)
            .Distinct(StringComparer.Ordinal)
            .Where(path => !before.TryGetValue(path, out string left)
                || !after.TryGetValue(path, out string right)
                || !string.Equals(left, right, StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string ComputeAggregateDigest(
        IReadOnlyDictionary<string, string> snapshot)
    {
        StringBuilder canonical = new();
        foreach (KeyValuePair<string, string> pair in snapshot
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            canonical.Append(pair.Key)
                .Append('\t')
                .Append(pair.Value)
                .Append('\n');
        }
        return HashText(canonical.ToString());
    }

    private static string HashText(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            new UTF8Encoding(false, true).GetBytes(value ?? string.Empty));
        return string.Concat(digest.Select(valueByte => valueByte.ToString("x2")));
    }

    private static string ParseToken(string line, string token)
    {
        int start = line.IndexOf(token, StringComparison.Ordinal);
        if (start < 0)
            return string.Empty;
        start += token.Length;
        int end = line.IndexOf(';', start);
        return (end < 0 ? line.Substring(start) : line.Substring(start, end - start))
            .Trim();
    }

    private static void WriteReport(string report)
    {
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report);
        V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream => stream.Write(bytes, 0, bytes.Length));
    }

    private readonly struct BuilderStep
    {
        public BuilderStep(string name, Action execute)
        {
            Name = name;
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public string Name { get; }
        public Action Execute { get; }
    }
}
#endif
