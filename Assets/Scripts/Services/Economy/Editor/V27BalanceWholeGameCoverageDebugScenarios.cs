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
        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (audit.IntegrityFailures.Count != 0 || audit.CriticalCount != 0)
        {
            throw new InvalidOperationException(
                $"V27 coverage requires a clean audit: integrity="
                + $"{audit.IntegrityFailures.Count}; critical={audit.CriticalCount}.");
        }

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
}
#endif
