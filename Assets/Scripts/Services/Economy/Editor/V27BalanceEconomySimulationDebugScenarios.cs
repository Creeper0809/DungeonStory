#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

public static class V27BalanceEconomySimulationDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-economy-256-seed.txt";
    private const int SeedCount = 256;

    [MenuItem("DungeonStory/V27/Run 256-Seed Economy Simulation")]
    public static void RunFromMenu()
    {
        string report;
        Exception failure = null;
        try
        {
            report = RunAll();
        }
        catch (Exception exception)
        {
            failure = exception;
            report = "RESULT=FAIL; seeds=256; reason=" + exception.Message + "\n";
        }

        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
        {
            using StreamWriter writer = new(
                stream,
                new UTF8Encoding(false, true),
                4096,
                leaveOpen: true);
            writer.Write(report);
            writer.Flush();
        });
        AssetDatabase.Refresh();
        if (failure == null)
            Debug.Log(report);
        else
            Debug.LogError(report + failure);
    }

    public static string RunAll()
    {
        SimulationContentSource source = SimulationContentSource.Load();
        ProductionRecipeSO[] recipes = source.GetAll<ProductionRecipeSO>().ToArray();
        CropDefinitionSO[] crops = source.GetAll<CropDefinitionSO>().ToArray();
        ItemDefinitionSO[] items = source.GetAll<ItemDefinitionSO>().ToArray();
        CombatEquipmentDefinitionSO[] equipment =
            source.GetAll<CombatEquipmentDefinitionSO>().ToArray();
        CraftMaterialDefinitionSO[] materials =
            source.GetAll<CraftMaterialDefinitionSO>().ToArray();
        Require(recipes.Length == 354,
            "Expected 354 recipes, found " + recipes.Length + ".");

        ResourceMaterialEconomicProfileCatalog profiles = new(source);
        V23BalanceWorkCalculator work = new(profiles);
        EmbeddedWorkValueSnapshot before = new V23EmbeddedWorkValueCalculator(
            recipes,
            items,
            equipment,
            materials,
            work).Calculate();
        Require(before.UnresolvedItemIds.Count == 0
            && before.NonConvergentRecipeIds.Count == 0,
            "V23 authority is incomplete before 256-seed simulation.");

        V27EmbeddedWorkValueSnapshot canonical = Calculate(
            recipes,
            crops,
            items,
            equipment,
            materials,
            before,
            work,
            profiles);
        Require(canonical.IsComplete,
            "Canonical V27 snapshot is incomplete.");
        string expectedHash = HashSnapshot(canonical);
        long minimumMargin = long.MaxValue;
        int verifiedTransforms = 0;
        int verifiedPartitions = 0;

        for (int seed = 0; seed < SeedCount; seed++)
        {
            V27EmbeddedWorkValueSnapshot shuffled = Calculate(
                Shuffle(recipes, seed, 11),
                Shuffle(crops, seed, 23),
                Shuffle(items, seed, 37),
                Shuffle(equipment, seed, 53),
                Shuffle(materials, seed, 71),
                before,
                work,
                profiles);
            Require(shuffled.IsComplete,
                "Seed " + seed + " produced an incomplete V27 snapshot.");
            string actualHash = HashSnapshot(shuffled);
            Require(string.Equals(expectedHash, actualHash, StringComparison.Ordinal),
                "Seed " + seed + " changed the semantic snapshot hash.");

            foreach (V27RecipeValueBreakdown recipe in shuffled.Recipes.Values)
            {
                Require(recipe.TransformMarginMilliEwu <= -1L,
                    "Seed " + seed + " found non-lossy recipe "
                    + recipe.RecipeId + " margin="
                    + recipe.TransformMarginMilliEwu + ".");
                minimumMargin = Math.Min(
                    minimumMargin,
                    recipe.TransformMarginMilliEwu);
                verifiedTransforms++;
            }
            foreach (V27CropValueBreakdown crop in shuffled.Crops.Values)
            {
                Require(crop.TransformMarginMilliEwu <= -1L,
                    "Seed " + seed + " found non-lossy crop "
                    + crop.CropId + " margin="
                    + crop.TransformMarginMilliEwu + ".");
                minimumMargin = Math.Min(
                    minimumMargin,
                    crop.TransformMarginMilliEwu);
                verifiedTransforms++;
            }

            VerifyRandomPartitionMonotonicity(seed);
            verifiedPartitions += 64;
        }

        V27BalanceAuditOutput audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.RegenerateArtifacts);
        Require(audit.IntegrityFailures.Count == 0,
            "Whole-game audit integrity failed: "
            + string.Join(" | ", audit.IntegrityFailures));
        Require(audit.CriticalCount == 0,
            "Whole-game audit has unresolved Criticals: "
            + audit.CriticalCount + ".");

        StringBuilder report = new(1024);
        report.AppendLine("RESULT=PASS; seeds=256; failures=0");
        report.AppendLine("PASS V27_ECONOMY_256_SEED_SEMANTIC_IDENTITY hash="
            + expectedHash);
        report.AppendLine("PASS V27_ECONOMY_256_SEED_ALL_AUTHORITY_COMPLETE items="
            + canonical.Items.Count.ToString(CultureInfo.InvariantCulture)
            + "; recipes="
            + canonical.Recipes.Count.ToString(CultureInfo.InvariantCulture)
            + "; crops="
            + canonical.Crops.Count.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("PASS V27_ECONOMY_256_SEED_TRANSFORMS_STRICTLY_LOSSY checks="
            + verifiedTransforms.ToString(CultureInfo.InvariantCulture)
            + "; minimumMargin="
            + minimumMargin.ToString(CultureInfo.InvariantCulture)
            + "mEWU");
        report.AppendLine("PASS V27_ECONOMY_256_SEED_PARTITION_MONOTONICITY checks="
            + verifiedPartitions.ToString(CultureInfo.InvariantCulture));
        report.AppendLine("PASS V27_ECONOMY_256_SEED_AUDIT_INTEGRITY rows="
            + audit.Ledger.Count.ToString(CultureInfo.InvariantCulture)
            + "; critical=0; scc="
            + audit.SccCount.ToString(CultureInfo.InvariantCulture));
        return report.ToString();
    }

    private static V27EmbeddedWorkValueSnapshot Calculate(
        IEnumerable<ProductionRecipeSO> recipes,
        IEnumerable<CropDefinitionSO> crops,
        IEnumerable<ItemDefinitionSO> items,
        IEnumerable<CombatEquipmentDefinitionSO> equipment,
        IEnumerable<CraftMaterialDefinitionSO> materials,
        EmbeddedWorkValueSnapshot before,
        IBalanceWorkCalculator work,
        IMaterialEconomicProfileCatalog profiles) =>
        new V27EmbeddedWorkValueCalculator(
            recipes,
            crops,
            items,
            equipment,
            materials,
            before,
            work,
            profiles,
            V27EmbeddedWorkValueCalculator.DefaultDurationPreservingScale)
        .Calculate();

    private static void VerifyRandomPartitionMonotonicity(int seed)
    {
        DeterministicRandom random = new(unchecked((uint)(seed + 1)));
        for (int sample = 0; sample < 64; sample++)
        {
            int parts = 2 + random.Next(7);
            long[] numerators = new long[parts];
            long totalNumerator = 0L;
            for (int part = 0; part < parts; part++)
            {
                numerators[part] = 1L + random.Next(1_000_000);
                totalNumerator = checked(totalNumerator + numerators[part]);
            }

            decimal whole = totalNumerator / 10000m;
            long wholeDebit = V27EwuQuantizer
                .QuantizeInputDebit(whole).MilliEwu;
            long splitDebit = 0L;
            long wholeCredit = V27EwuQuantizer
                .QuantizeOutputCredit(whole).MilliEwu;
            long splitCredit = 0L;
            foreach (long numerator in numerators)
            {
                decimal value = numerator / 10000m;
                splitDebit = checked(splitDebit
                    + V27EwuQuantizer.QuantizeInputDebit(value).MilliEwu);
                splitCredit = checked(splitCredit
                    + V27EwuQuantizer.QuantizeOutputCredit(value).MilliEwu);
            }
            Require(splitDebit >= wholeDebit,
                "Seed " + seed + " reduced debit by partitioning.");
            Require(splitCredit <= wholeCredit,
                "Seed " + seed + " increased credit by partitioning.");
        }
    }

    private static T[] Shuffle<T>(T[] source, int seed, int salt)
    {
        T[] values = source.ToArray();
        DeterministicRandom random = new(unchecked(
            (uint)((seed + 1) * 16777619 ^ salt * 486187739)));
        for (int index = values.Length - 1; index > 0; index--)
        {
            int swap = random.Next(index + 1);
            (values[index], values[swap]) = (values[swap], values[index]);
        }
        return values;
    }

    private static string HashSnapshot(V27EmbeddedWorkValueSnapshot snapshot)
    {
        StringBuilder canonical = new(128 * 1024);
        foreach (KeyValuePair<string, V27ItemValue> pair in snapshot.Items
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            canonical.Append("I|").Append(pair.Key).Append('|')
                .Append(pair.Value.AcquisitionCost.MilliEwu).Append('|')
                .Append(pair.Value.RecoverableValue.MilliEwu).Append('|')
                .Append(pair.Value.SelectedSourceId).Append('\n');
        }
        foreach (KeyValuePair<string, V27RecipeValueBreakdown> pair in
                 snapshot.Recipes.OrderBy(value => value.Key,
                     StringComparer.Ordinal))
        {
            V27RecipeValueBreakdown value = pair.Value;
            canonical.Append("R|").Append(pair.Key).Append('|')
                .Append(value.InputDebit.MilliEwu).Append('|')
                .Append(value.DirectWorkDebit.MilliEwu).Append('|')
                .Append(value.LogisticsDebit.MilliEwu).Append('|')
                .Append(value.InfrastructureDebit.MilliEwu).Append('|')
                .Append(value.ExpectedLossDebit.MilliEwu).Append('|')
                .Append(value.PerUnitAcquisition.MilliEwu).Append('|')
                .Append(value.TotalOutputCredit.MilliEwu).Append('|')
                .Append(value.TransformMarginMilliEwu).Append('\n');
        }
        foreach (KeyValuePair<string, V27CropValueBreakdown> pair in
                 snapshot.Crops.OrderBy(value => value.Key,
                     StringComparer.Ordinal))
        {
            V27CropValueBreakdown value = pair.Value;
            canonical.Append("C|").Append(pair.Key).Append('|')
                .Append(value.InputDebit.MilliEwu).Append('|')
                .Append(value.DirectWorkDebit.MilliEwu).Append('|')
                .Append(value.LogisticsDebit.MilliEwu).Append('|')
                .Append(value.InfrastructureDebit.MilliEwu).Append('|')
                .Append(value.ExpectedLossDebit.MilliEwu).Append('|')
                .Append(value.PerUnitAcquisition.MilliEwu).Append('|')
                .Append(value.TotalOutputCredit.MilliEwu).Append('|')
                .Append(value.TransformMarginMilliEwu).Append('\n');
        }
        byte[] bytes = Encoding.UTF8.GetBytes(canonical.ToString());
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private struct DeterministicRandom
    {
        private uint state;

        public DeterministicRandom(uint seed)
        {
            state = seed == 0 ? 0x9E3779B9u : seed;
        }

        public int Next(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            uint value = NextUInt();
            return (int)(value % (uint)exclusiveMaximum);
        }

        private uint NextUInt()
        {
            uint value = state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            state = value;
            return value;
        }
    }

    private sealed class SimulationContentSource : IGameContentDefinitionSource
    {
        private readonly ScriptableObject[] definitions;

        private SimulationContentSource(IEnumerable<ScriptableObject> definitions)
        {
            this.definitions = definitions
                .Where(value => value != null)
                .Distinct()
                .ToArray();
        }

        public static SimulationContentSource Load()
        {
            GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
                ?? throw new InvalidOperationException(
                    "Root content catalog is missing.");
            GameDomainContentCatalogSO domain = root.DomainCatalogs
                .OfType<GameDomainContentCatalogSO>()
                .Single();
            ItemDefinitionCatalogSO items = root
                .GetItemDefinitions<ItemDefinitionCatalogSO>()
                ?? throw new InvalidOperationException(
                    "Item definition catalog is missing.");
            return new SimulationContentSource(domain.Definitions
                .Concat(items.Definitions.Cast<ScriptableObject>()));
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject => definitions
            .OfType<T>()
            .Distinct()
            .ToArray();

        public T RequireSingle<T>() where T : ScriptableObject =>
            GetAll<T>().Single();
    }
}
#endif
