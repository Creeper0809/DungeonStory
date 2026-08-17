#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

public static class V27BalanceWholeGameCoverageDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-whole-game-coverage.txt";

    private static readonly string[] RequiredDomains =
    {
        "agriculture",
        "combat",
        "content",
        "defense",
        "economy",
        "facilities",
        "items",
        "labor",
        "medical",
        "offense",
        "production",
        "research"
    };

    private static readonly int[] RequiredDailyRoutineSeeds =
    {
        157181,
        157182,
        157183
    };

    [MenuItem("DungeonStory/V27/Verify Whole-Game Ledger Coverage")]
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
        string laborMatrix = V27LaborAuthorityMatrixDebugScenarios.RunAll();
        string[] requiredLaborMatrixMarkers =
        {
            "RESULT=PASS; cells=360",
            "PASS V27_LABOR_MATRIX_360_CELLS",
            "PASS V27_LABOR_MATRIX_ACTUAL_EFFECTIVE_RATIO",
            "PASS V27_LABOR_MATRIX_TECH_MONOTONIC",
            "PASS V27_LABOR_MATRIX_SURVIVAL_MONOTONIC",
            "PASS V27_LABOR_MATRIX_GROWTH_CUT_FIRST",
            "PASS V27_LABOR_MATRIX_SHORTAGE_CRISIS_EXPOSED"
        };
        string[] missingLaborMatrixMarkers = requiredLaborMatrixMarkers
            .Where(marker => !laborMatrix.Contains(marker, StringComparison.Ordinal))
            .ToArray();
        if (missingLaborMatrixMarkers.Length > 0)
        {
            throw new InvalidOperationException(
                "V27 labor authority matrix is incomplete: "
                + string.Join(",", missingLaborMatrixMarkers));
        }

        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (audit.IntegrityFailures.Count != 0 || audit.CriticalCount != 0)
        {
            throw new InvalidOperationException(
                $"V27 coverage requires a clean audit: integrity="
                + $"{audit.IntegrityFailures.Count}; critical={audit.CriticalCount}.");
        }

        (double actualMean, double effectiveMean) =
            RequireFreshDailyRoutineEvidence();

        IReadOnlyList<CanonicalBalanceMetricRecord> records = audit.Ledger.Records;
        string[] missingDomains = RequiredDomains
            .Where(domain => records.All(record =>
                !string.Equals(record.Domain, domain, StringComparison.Ordinal)))
            .ToArray();
        if (missingDomains.Length > 0)
        {
            throw new InvalidOperationException(
                "V27 ledger is missing domains: " + string.Join(",", missingDomains));
        }

        IReadOnlyList<string> networkFailures =
            BranchedProductionNetworkDebugScenarios.Validate();
        ProductionNetworkCoverageSnapshot network =
            BranchedProductionNetworkDebugScenarios.LastCoverage;
        if (networkFailures.Count != 0
            || network.ProducerOrphanCount != 0
            || network.ConsumerOrphanCount != 0)
        {
            throw new InvalidOperationException(
                "V27 production network coverage failed: "
                + string.Join(" | ", networkFailures));
        }

        int itemDefinitions = records.Count(record =>
            string.Equals(record.DefinitionKind, "item", StringComparison.Ordinal)
            && string.Equals(record.Metric, "acquisition-cost", StringComparison.Ordinal));
        int recipeDefinitions = AssetDatabase.FindAssets("t:ProductionRecipeSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>)
            .Where(value => value != null)
            .Select(value => value.RecipeId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        int activeBuildingDefinitions = records.Count(record =>
            string.Equals(record.Domain, "facilities", StringComparison.Ordinal)
            && string.Equals(
                record.Metric,
                "construction-authored-wu:period-preserving",
                StringComparison.Ordinal));
        int serializedDefinitionCount = records
            .Where(record => string.Equals(
                record.DefinitionKind,
                "serialized-property",
                StringComparison.Ordinal))
            .Select(record => record.StableId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        int approvedButUnapplied = records.Count(record =>
            record.ApprovalKey.Length > 0
            && !string.Equals(record.AssetApplied, "true", StringComparison.Ordinal));
        if (itemDefinitions != 413
            || network.DefinitionCount != 363
            || recipeDefinitions != 354
            || activeBuildingDefinitions != 356
            || approvedButUnapplied != 0)
        {
            throw new InvalidOperationException(
                $"V27 coverage count drift: items={itemDefinitions}; "
                + $"resourceItems={network.DefinitionCount}; recipes={recipeDefinitions}; "
                + $"buildings={activeBuildingDefinitions}; unapplied={approvedButUnapplied}.");
        }

        StringBuilder report = new();
        report.Append("RESULT=PASS; rows=")
            .Append(records.Count.ToString(CultureInfo.InvariantCulture))
            .Append("; domains=")
            .Append(RequiredDomains.Length.ToString(CultureInfo.InvariantCulture))
            .Append("; producerOrphans=0; consumerOrphans=0; approvedUnapplied=0\n");
        report.Append("PASS V27_WHOLE_GAME_SERIALIZED_AUTHORITY definitions=")
            .Append(serializedDefinitionCount.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_WHOLE_GAME_ITEM_DEFINITIONS total=")
            .Append(itemDefinitions.ToString(CultureInfo.InvariantCulture))
            .Append("; resource=")
            .Append(network.DefinitionCount.ToString(CultureInfo.InvariantCulture))
            .Append("; nonResource=")
            .Append((itemDefinitions - network.DefinitionCount).ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_WHOLE_GAME_RECIPE_DEFINITIONS total=")
            .Append(recipeDefinitions.ToString(CultureInfo.InvariantCulture))
            .Append("; maxDepth=")
            .Append(network.MaximumRecipeDepth.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_WHOLE_GAME_ACTIVE_BUILDINGS total=")
            .Append(activeBuildingDefinitions.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_WHOLE_GAME_PRODUCER_LINKS links=")
            .Append(network.ProducerLinkCount.ToString(CultureInfo.InvariantCulture))
            .Append("; orphans=0\n");
        report.Append("PASS V27_WHOLE_GAME_CONSUMER_LINKS links=")
            .Append(network.ConsumerLinkCount.ToString(CultureInfo.InvariantCulture))
            .Append("; orphans=0\n");
        report.Append("PASS V27_WHOLE_GAME_EXACT_APPROVAL_APPLICATION unapplied=0\n");
        report.Append("PASS V27_WHOLE_GAME_LABOR_AUTHORITY_MATRIX cells=360\n");
        report.Append("PASS V27_WHOLE_GAME_DAILY_ROUTINE_3_SEEDS actualMean=")
            .Append(actualMean.ToString("0.000000", CultureInfo.InvariantCulture))
            .Append("; effectiveMean=")
            .Append(effectiveMean.ToString("0.000000", CultureInfo.InvariantCulture))
            .Append("\n");
        foreach (string domain in RequiredDomains)
        {
            int count = records.Count(record =>
                string.Equals(record.Domain, domain, StringComparison.Ordinal));
            report.Append("PASS V27_DOMAIN_ROWS domain=")
                .Append(domain)
                .Append("; rows=")
                .Append(count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }
        return report.ToString();
    }

    private static (double ActualMean, double EffectiveMean)
        RequireFreshDailyRoutineEvidence()
    {
        string[] sourcePaths =
        {
            "Assets/Scripts/Services/Character/Work/Editor/DailyRoutineWuPlayModeVerifier.cs",
            "Assets/Scripts/Models/Work/SettlementLaborAuthority.cs",
            "Assets/Scripts/Services/Character/AI/CharacterAiDecisionPipeline.cs",
            "Assets/Scripts/Services/Character/Ability/AbilityWork.cs",
            "Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs"
        };
        DateTime latestSource = sourcePaths
            .Select(Path.GetFullPath)
            .Select(path => File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : throw new FileNotFoundException(
                    "Daily-routine freshness source is missing.",
                    path))
            .Max();
        double actualTotal = 0d;
        double effectiveTotal = 0d;
        foreach (int seed in RequiredDailyRoutineSeeds)
        {
            string path = Path.GetFullPath(
                $"Artifacts/QA/phase157-daily-routine-wu-seed-{seed}.txt");
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < latestSource)
            {
                throw new InvalidOperationException(
                    $"Daily-routine evidence is missing or stale: seed={seed}.");
            }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            RequireExactLine(lines, "observedDays=5", seed);
            RequireExactLine(lines, $"runSeed={seed}", seed);
            RequireExactLine(
                lines,
                "runtimeDiagnosticsGate=ai-runtime-gate-v3",
                seed);
            if (!lines.Any(line => line.StartsWith(
                    "RESULT=PASS; failures=0;",
                    StringComparison.Ordinal)
                && line.EndsWith("capturedIssues=0", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Daily-routine result contract failed: seed={seed}.");
            }
            double days = ParseEvidenceNumber(lines, "observedDays=", seed);
            double actors = ParseEvidenceNumber(lines, "actors=", seed);
            double divisor = checked(days * actors);
            double actual = ParseEvidenceNumber(lines, "actualLaborWU=", seed) / divisor;
            double effective = ParseEvidenceNumber(
                lines,
                "outputEquivalentWU=",
                seed) / divisor;
            if (actual < SettlementLaborAuthority.ActualWuPerAdultDay
                || effective < SettlementLaborAuthority.EffectiveOutputWuPerAdultDay)
            {
                throw new InvalidOperationException(
                    $"Daily-routine labor authority failed: seed={seed}; "
                    + $"actual={actual:R}; effective={effective:R}.");
            }
            actualTotal += actual;
            effectiveTotal += effective;
        }
        return (
            actualTotal / RequiredDailyRoutineSeeds.Length,
            effectiveTotal / RequiredDailyRoutineSeeds.Length);
    }

    private static void RequireExactLine(
        IReadOnlyList<string> lines,
        string expected,
        int seed)
    {
        if (!lines.Contains(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Daily-routine marker is missing: seed={seed}; marker={expected}.");
        }
    }

    private static double ParseEvidenceNumber(
        IReadOnlyList<string> lines,
        string prefix,
        int seed)
    {
        string line = lines.SingleOrDefault(value => value.StartsWith(
            prefix,
            StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Daily-routine numeric marker is missing: seed={seed}; prefix={prefix}.");
        return double.Parse(
            line.Substring(prefix.Length),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
    }
}
#endif
