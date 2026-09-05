#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityOutputCensusDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-production-facility-output-census.txt";
    public const string CsvPath =
        "Artifacts/QA/v27-production-facility-output-census.csv";

    [MenuItem("DungeonStory/V27/Physical Mass/Capture Facility Output Census")]
    public static void CaptureFromMenu()
    {
        ProductionFacilityOutputCensusSnapshot snapshot = CaptureCurrent(false);
        ProductionFacilityOutputCensusSnapshot shuffled = CaptureCurrent(true);
        Require(string.Equals(
                snapshot.SourceDigest,
                shuffled.SourceDigest,
                StringComparison.Ordinal),
            "Production facility output census depends on definition, recipe, or contributor order.");
        WriteArtifacts(snapshot);
        Debug.Log(
            "V27_FACILITY_OUTPUT_CENSUS_CAPTURED definitions="
            + snapshot.DefinitionCount
            + "; facilities=" + snapshot.FacilityCount
            + "; producers=" + snapshot.AutomaticProducerCount
            + "; nonproducers=" + snapshot.NonProducerCount
            + "; unclassified=" + snapshot.UnclassifiedCount
            + "; orphans=" + snapshot.ExecutionOrphanCount);
    }

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Facility Output Census")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_FACILITY_OUTPUT_CENSUS_PASS");
    }

    public static void RunAll()
    {
        ProductionFacilityOutputCensusSnapshot snapshot = CaptureCurrent(false);
        ProductionFacilityOutputCensusSnapshot shuffled = CaptureCurrent(true);
        Require(string.Equals(
                snapshot.SourceDigest,
                shuffled.SourceDigest,
                StringComparison.Ordinal),
            "Production facility output census is not insertion-order deterministic.");
        Require(snapshot.RawDefinitionCount == 419
            && snapshot.DefinitionCount == 377
            && snapshot.ActiveDefinitionCount == 356
            && snapshot.DeprecatedDefinitionCount == 21
            && snapshot.FacilityCount == 146
            && snapshot.AutomaticProducerCount == 92
            && snapshot.NonProducerCount == 54
            && snapshot.RecipeOnlyProducerCount == 85
            && snapshot.SpecialProducerCount == 7
            && snapshot.RecipeAndSpecialProducerCount == 4,
            "Production facility output census drifted: rawDefinitions="
            + snapshot.RawDefinitionCount
            + "; authoredDefinitions=" + snapshot.DefinitionCount
            + "; activeDefinitions=" + snapshot.ActiveDefinitionCount
            + "; deprecatedDefinitions=" + snapshot.DeprecatedDefinitionCount
            + "; facilities=" + snapshot.FacilityCount
            + "; producers=" + snapshot.AutomaticProducerCount
            + "; nonproducers=" + snapshot.NonProducerCount
            + "; recipeOnly=" + snapshot.RecipeOnlyProducerCount
            + "; special=" + snapshot.SpecialProducerCount
            + "; overlap=" + snapshot.RecipeAndSpecialProducerCount + ".");
        Require(snapshot.UnclassifiedCount == 0,
            "Production facility output census has unclassified facilities: "
            + JoinIds(snapshot.Rows.Where(value => value.IsUnclassified)));
        Require(snapshot.ExecutionOrphanCount == 0,
            "Production facility output census has execution orphans: "
            + JoinIds(snapshot.Rows.Where(value => value.HasExecutionOrphan)));
        Require(snapshot.Rows.Count(value => value.LanePolicy
                    == ProductionWorkstationLanePolicy
                        .ManualWithDetachedBatchProcessors) == 123
                && snapshot.Rows.Count(value => value.LanePolicy
                    == ProductionWorkstationLanePolicy
                        .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors)
                    == 23
                && snapshot.Rows.All(value =>
                    value.ManualWorkLaneCount == 1
                    && value.AutomaticWorkLaneCount
                        == (value.LanePolicy == ProductionWorkstationLanePolicy
                            .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors
                                ? 1
                                : 0)),
            "Production facility output census lane authority drifted.");
        Require(snapshot.SpecialFacilityCount == 7
            && snapshot.SpecialBranchCount == 904
            && snapshot.SpecialCandidateCount == 0
            && snapshot.SpecialGapCount == 904
            && snapshot.SpecialUnregisteredGapCount == 0
            && snapshot.SpecialAuthoredCycleGapCount == 60
            && snapshot.SpecialExecutionUnsupportedGapCount == 844,
            "Production special-throughput coverage drifted: facilities="
            + snapshot.SpecialFacilityCount
            + "; branches=" + snapshot.SpecialBranchCount
            + "; candidates=" + snapshot.SpecialCandidateCount
            + "; gaps=" + snapshot.SpecialGapCount
            + "; unregistered=" + snapshot.SpecialUnregisteredGapCount
            + "; authoredCycle=" + snapshot.SpecialAuthoredCycleGapCount
            + "; executionUnsupported="
            + snapshot.SpecialExecutionUnsupportedGapCount + ".");
        WriteArtifacts(snapshot);
    }

    public static ProductionFacilityOutputCensusSnapshot CaptureCurrent(
        bool reverse)
    {
        IGameContentCatalog gameContent = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        IGameContentDefinitionSource definitions = gameContent;
        BuildingSO[] buildings = definitions.GetAll<BuildingSO>()
            .OrderBy(value => AssetDatabase.GetAssetPath(value),
                StringComparer.Ordinal)
            .ToArray();
        ResourceEconomyContentCatalog economy = new(gameContent);
        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        GameplayEffectResultBoundsCatalog bounds = new(definitions);
        CharacterFunctionalCapacityDefinitionBoundsQuery capacityBounds = new(
            bounds);
        CharacterPerformanceDefinitionMaximumQuery performance = new(
            new CharacterPerformanceFormulaCatalog(definitions),
            bounds,
            capacityBounds);

        ResourceApparelDefinitionCatalog apparel = new(definitions);
        ResourceTextileMaterialCatalog textiles = new(definitions);
        ResourceCombatEquipmentCatalog equipment = new(definitions);
        CombatCraftDefinitionCatalog crafts = new(equipment);
        CombatCraftFacilityEligibilityQuery combatEligibility = new(
            definitions,
            crafts);
        CombatRejectedRecoveryProjector recovery = new(
            equipment,
            economy,
            new V23MaterialSalvageCalculator(
                new ResourceMaterialEconomicProfileCatalog(definitions)),
            mass,
            bounds);

        IProductionFacilityOutputCapacityContributor[] capacity =
        {
            new CertifiedSeedFacilityOutputCapacityContributor(economy),
            new ApparelFacilityOutputCapacityContributor(
                apparel,
                textiles,
                mass,
                bounds),
            new CombatCraftFacilityOutputCapacityContributor(
                combatEligibility,
                recovery),
            new CropHarvestFacilityOutputCapacityContributor(
                economy,
                definitions,
                new ICropHarvestReachableMaximumWitnessContributor[]
                {
                    new NaturalGoldenHarvestReachableMaximumWitnessContributor(
                        definitions,
                        new CharacterPerformanceFormulaCatalog(definitions))
                })
        };
        IProductionSpecialThroughputContributor[] specialThroughput =
        {
            new CertifiedSeedSpecialThroughputGapContributor(),
            new CropHarvestSpecialThroughputGapContributor(),
            new ApparelSpecialThroughputGapContributor(),
            new CombatCraftSpecialThroughputGapContributor()
        };
        IProductionFacilityOutputDispositionContributor[] dispositions =
        {
            new AuthoredFacilityOutputDispositionContributor(),
            new CoreAbilityFacilityOutputDispositionContributor(),
            new EquipmentProgressionFacilityOutputDispositionContributor(),
            new ResearchCommandFacilityOutputDispositionContributor(
                new CurrentCommandExecutionConnectionQuery())
        };
        IEnumerable<BuildingSO> orderedBuildings = reverse
            ? buildings.Reverse()
            : buildings;
        IEnumerable<ProductionRecipeSO> orderedRecipes = reverse
            ? economy.Recipes.Reverse()
            : economy.Recipes;
        IEnumerable<IProductionFacilityOutputCapacityContributor> orderedCapacity =
            reverse ? capacity.Reverse() : capacity;
        IEnumerable<IProductionSpecialThroughputContributor>
            orderedSpecialThroughput = reverse
                ? specialThroughput.Reverse()
                : specialThroughput;
        IEnumerable<IProductionFacilityOutputDispositionContributor>
            orderedDispositions = reverse
                ? dispositions.Reverse()
                : dispositions;

        ProductionPreparedOutputComponentCodec codec = new();
        ResourceItemDefinitionCatalog itemCatalog = new(
            definitions.GetAll<ItemDefinitionSO>());
        ProductionOutputMaximumMassRegistry maximumMass = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(economy, codec),
                new CertifiedSeedOutputCapability(economy),
                new CropHarvestSeedLotOutputCapability(economy),
                new ApparelWorkOrderOutputCapability(apparel),
                new CombatEquipmentCraftOutputCapability(equipment),
                new CombatAmmunitionCraftOutputCapability(itemCatalog),
                new PerishableFoodOutputCapability(itemCatalog),
                new EnvironmentalWorkwearProductionOutputMaximumMassCapability(
                    apparel),
                new SurgicalPartProductionOutputMaximumMassCapability()
            },
            mass);
        ProductionFacilityOutputCapacityContributorRegistry capacityRegistry = new(
            orderedCapacity,
            maximumMass);
        ProductionFacilityOutputCensus census = new(
            orderedRecipes,
            capacityRegistry,
            new ProductionSpecialThroughputContributorRegistry(
                orderedSpecialThroughput),
            new ProductionFacilityOutputDispositionRegistry(
                orderedDispositions));
        return census.Capture(orderedBuildings.ToArray());
    }

    private static void WriteArtifacts(
        ProductionFacilityOutputCensusSnapshot snapshot)
    {
        string report = "schema=production-facility-output-census@3\n"
            + "rawDefinitions=" + snapshot.RawDefinitionCount + "\n"
            + "authoredDefinitions=" + snapshot.DefinitionCount + "\n"
            + "activeDefinitions=" + snapshot.ActiveDefinitionCount + "\n"
            + "deprecatedDefinitions=" + snapshot.DeprecatedDefinitionCount + "\n"
            + "facilities=" + snapshot.FacilityCount + "\n"
            + "automaticProducers=" + snapshot.AutomaticProducerCount + "\n"
            + "nonProducers=" + snapshot.NonProducerCount + "\n"
            + "recipeOnlyProducers=" + snapshot.RecipeOnlyProducerCount + "\n"
            + "specialProducers=" + snapshot.SpecialProducerCount + "\n"
            + "recipeAndSpecialProducers="
            + snapshot.RecipeAndSpecialProducerCount + "\n"
            + "manualOnlyLaneProfiles=" + snapshot.Rows.Count(value =>
                value.LanePolicy == ProductionWorkstationLanePolicy
                    .ManualWithDetachedBatchProcessors) + "\n"
            + "modeExclusiveManualOrAutomaticLaneProfiles="
            + snapshot.Rows.Count(value => value.LanePolicy
                == ProductionWorkstationLanePolicy
                    .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors)
            + "\n"
            + "unclassified=" + snapshot.UnclassifiedCount + "\n"
            + "executionOrphans=" + snapshot.ExecutionOrphanCount + "\n"
            + "contentGaps=" + snapshot.ContentGapCount + "\n"
            + "specialFacilities=" + snapshot.SpecialFacilityCount + "\n"
            + "specialBranches=" + snapshot.SpecialBranchCount + "\n"
            + "specialCandidates=" + snapshot.SpecialCandidateCount + "\n"
            + "specialGaps=" + snapshot.SpecialGapCount + "\n"
            + "specialUnregisteredGaps="
            + snapshot.SpecialUnregisteredGapCount + "\n"
            + "specialAuthoredCycleGaps="
            + snapshot.SpecialAuthoredCycleGapCount + "\n"
            + "specialExecutionUnsupportedGaps="
            + snapshot.SpecialExecutionUnsupportedGapCount + "\n"
            + "sourceDigest=" + snapshot.SourceDigest + "\n"
            + "unclassifiedIds="
            + JoinIds(snapshot.Rows.Where(value => value.IsUnclassified)) + "\n"
            + "executionOrphanIds="
            + JoinIds(snapshot.Rows.Where(value => value.HasExecutionOrphan)) + "\n"
            + "result="
            + (snapshot.UnclassifiedCount == 0
                && snapshot.ExecutionOrphanCount == 0
                    ? "PASS"
                    : "FAIL")
            + "\n";
        WriteUtf8(ReportPath, report);
        V27BalanceArtifactWriter.WriteIfDifferent(CsvPath, stream =>
        {
            using StreamWriter writer = new(
                stream,
                new UTF8Encoding(false, true),
                16384,
                leaveOpen: true);
            writer.NewLine = "\r\n";
            writer.WriteLine(
                "definitionId,workstationTag,lanePolicy,manualWorkLaneCount,automaticWorkLaneCount,workstationLaneSourceDigest,automaticProducer,recipeIds,capacityContributorIds,specialThroughputContributorIds,specialCandidateCount,specialGapCount,specialGapReasons,specialSourceDigest,dispositionClaims,unclassified,executionOrphan,contentGap,sourceDigest");
            foreach (ProductionFacilityOutputCensusRow row in snapshot.Rows)
            {
                WriteField(writer, row.DefinitionId);
                writer.Write(',');
                WriteField(writer, row.WorkstationTag);
                writer.Write(',');
                writer.Write((int)row.LanePolicy);
                writer.Write(',');
                writer.Write(row.ManualWorkLaneCount);
                writer.Write(',');
                writer.Write(row.AutomaticWorkLaneCount);
                writer.Write(',');
                writer.Write(row.WorkstationLaneSourceDigest);
                writer.Write(',');
                writer.Write(row.IsAutomaticProducer ? "true" : "false");
                writer.Write(',');
                WriteField(writer, string.Join("|", row.RecipeIds));
                writer.Write(',');
                WriteField(writer, string.Join("|", row.CapacityContributorIds));
                writer.Write(',');
                WriteField(writer, string.Join(
                    "|",
                    row.SpecialThroughputContributorIds));
                writer.Write(',');
                writer.Write(row.SpecialThroughputCandidates.Count);
                writer.Write(',');
                writer.Write(row.SpecialThroughputGaps.Count);
                writer.Write(',');
                WriteField(writer, string.Join("|", row.SpecialThroughputGaps
                    .Select(value => value.ProducerId + ":" + value.BranchId
                        + ":" + value.Reason)));
                writer.Write(',');
                writer.Write(row.SpecialThroughputSourceDigest);
                writer.Write(',');
                WriteField(writer, string.Join("|", row.DispositionClaims.Select(
                    value => value.CapabilityId
                        + ":" + value.EffectKind
                        + ":" + value.RouteKind
                        + ":" + (value.ExecutionConnected
                            ? "connected"
                            : "orphan")
                        + ":" + value.ReasonCode)));
                writer.Write(',');
                writer.Write(row.IsUnclassified ? "true" : "false");
                writer.Write(',');
                writer.Write(row.HasExecutionOrphan ? "true" : "false");
                writer.Write(',');
                writer.Write(row.HasContentGap ? "true" : "false");
                writer.Write(',');
                writer.Write(row.SourceDigest);
                writer.Write('\r');
                writer.Write('\n');
            }
            writer.Flush();
        });
    }

    private static void WriteField(StreamWriter writer, string value) =>
        V27BalanceCsvSerializer.WriteEscapedField(
            writer,
            (value ?? string.Empty).AsSpan());

    private static void WriteUtf8(string path, string value)
    {
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(value);
        V27BalanceArtifactWriter.WriteIfDifferent(path, stream =>
            stream.Write(bytes, 0, bytes.Length));
    }

    private static string JoinIds(
        IEnumerable<ProductionFacilityOutputCensusRow> rows) =>
        string.Join(",", rows
            .Select(value => value.DefinitionId)
            .OrderBy(value => value, StringComparer.Ordinal));

    private static T[] LoadAll<T>(string root) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .OrderBy(value => AssetDatabase.GetAssetPath(value),
                StringComparer.Ordinal)
            .ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class CurrentCommandExecutionConnectionQuery :
        IResearchFacilityCommandExecutionConnectionQuery
    {
        public bool IsConnected(ResearchFacilityCommandKind command) =>
            ResearchFacilityCommandConsumerRegistry.HasExecutionContract(command);
    }

}
#endif
