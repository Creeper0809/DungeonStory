using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CropHarvestSpecialThroughputDebugScenarios
{
    [MenuItem("DungeonStory/V27/Production/Validate Crop Harvest Throughput")]
    public static void Validate()
    {
        AssetContentSource content = new();
        ResourceEconomyContentCatalog economy = new(
            content.GetAll<ResourceItemDefinitionSO>(),
            content.GetAll<ProductionRecipeSO>(),
            content.GetAll<CropDefinitionSO>(),
            content.GetAll<CraftMaterialDefinitionSO>());
        Require(economy.Crops.Count == 12,
            "Crop throughput requires the current 12-crop catalog.");
        CropGenomeReachableMaximumWitnessCatalog genomeWitnesses = new(
            content);
        VerifyGenomeWitnesses(economy, genomeWitnesses);

        CropHarvestFacilityOutputCapacityContributor capacity = new(
            economy,
            content,
            new ICropHarvestReachableMaximumWitnessContributor[]
            {
                new NaturalGoldenHarvestReachableMaximumWitnessContributor(
                    content,
                    new CharacterPerformanceFormulaCatalog(content))
            });
        IPhysicalItemMassQuery itemMass = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        ProductionPreparedOutputComponentCodec codec = new();
        ProductionOutputMaximumMassRegistry maximumMass = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(economy, codec),
                new CropHarvestSeedLotOutputCapability(economy)
            },
            itemMass);
        ProductionFacilityOutputCapacityBranchMassAuthority branchMass = new(
            maximumMass);
        CropHarvestCycleMaximumAuthority cycles = new(
            economy,
            content,
            new ICropHarvestReachableMaximumWitnessContributor[]
            {
                new NaturalGoldenHarvestReachableMaximumWitnessContributor(
                    content,
                    new CharacterPerformanceFormulaCatalog(content))
            });
        CropHarvestSpecialThroughputContributor contributor = new(
            cycles,
            new FixedWorkRateQuery(),
            branchMass);

        BuildingSO[] plots = content.GetAll<BuildingSO>()
            .Where(value => value != null
                && value.GetAbility<BuildingCropPlotAbility>() != null)
            .OrderBy(
                ProductionFacilityDefinitionIdentity.Resolve,
                StringComparer.Ordinal)
            .ToArray();
        Require(plots.Length == 4,
            "Crop throughput requires the current four crop facilities.");

        int branches = 0;
        int candidates = 0;
        int gaps = 0;
        List<string> firstDigests = new();
        foreach (BuildingSO plot in plots)
        {
            ProductionFacilityCapacitySubject subject = Subject(plot);
            ProductionFacilityOutputCapacityContribution contribution =
                capacity.Capture(subject);
            ProductionSpecialThroughputContributorResult result =
                contributor.Capture(
                    new ProductionSpecialThroughputFacilityContext(
                        subject,
                        new[] { contribution }),
                    contribution);
            branches += contribution.Branches.Count;
            candidates += result.Candidates.Count;
            gaps += result.Gaps.Count;
            firstDigests.Add(result.SourceDigest);
            Require(result.Candidates.Select(value => value.BranchId)
                    .SequenceEqual(
                        contribution.Branches.Select(value => value.BranchId),
                        StringComparer.Ordinal),
                "Crop candidate coverage drifted from capacity branches.");
        }
        Require(branches == 48 && candidates == 48 && gaps == 0,
            "Crop current-source census is not 48 candidates / 0 gaps: "
            + $"branches={branches};candidates={candidates};gaps={gaps}.");

        VerifyCalendarIntegration(plots, economy, cycles, genomeWitnesses);

        foreach (BuildingSO plot in plots.Reverse())
        {
            ProductionFacilityCapacitySubject subject = Subject(plot);
            ProductionFacilityOutputCapacityContribution contribution =
                capacity.Capture(subject);
            ProductionSpecialThroughputContributorResult repeat =
                contributor.Capture(
                    new ProductionSpecialThroughputFacilityContext(
                        subject,
                        new[] { contribution }),
                    contribution);
            Require(firstDigests.Contains(repeat.SourceDigest),
                "Crop throughput digest depends on facility enumeration order.");
        }
        Debug.Log(
            "CROP_HARVEST_SPECIAL_THROUGHPUT_PASS "
            + "facilities=4;branches=48;candidates=48;gaps=0");
    }

    private static void VerifyCalendarIntegration(
        IReadOnlyList<BuildingSO> plots,
        IResourceEconomyContentCatalog economy,
        ICropHarvestCycleMaximumQuery cycles,
        CropGenomeReachableMaximumWitnessCatalog genomeWitnesses)
    {
        BuildingSO outdoor = plots.Single(value =>
            !value.GetAbility<BuildingCropPlotAbility>().Indoor);
        BuildingSO hydroponics = plots.Single(value => string.Equals(
            value.GetProductionWorkstationAbility().WorkstationTag,
            "workstation:hydroponics",
            StringComparison.Ordinal));
        CropDefinitionSO crop = economy.Crops.First();
        CropGenomeReachableMaximumWitnessSnapshot genome =
            genomeWitnesses.Capture(crop.CropId);
        string branch = CropHarvestFacilityOutputBranchIdentity
            .ForReachableWitness(
                crop.CropId,
                NaturalGoldenHarvestReachableMaximumWitnessContributor
                    .WitnessId);
        CropHarvestCycleMaximumSnapshot outdoorCycle = cycles.Capture(
            ProductionFacilityDefinitionIdentity.Resolve(outdoor),
            branch);
        CropHarvestCycleMaximumSnapshot indoorCycle = cycles.Capture(
            ProductionFacilityDefinitionIdentity.Resolve(hydroponics),
            branch);

        decimal outdoorExpected = Exact(
                outdoor.GetAbility<BuildingCropPlotAbility>().GrowthMultiplier)
            * 1.10m * 1.05m * Exact(genome.GrowthMultiplier)
            * (115m + 65m * 0.55m) / 180m;
        decimal indoorExpected = Exact(
                hydroponics.GetAbility<BuildingCropPlotAbility>()
                    .GrowthMultiplier)
            * 1.08m * 1.05m * Exact(genome.GrowthMultiplier);
        Require(outdoorCycle.MaximumSustainableGrowthRate == outdoorExpected,
            "Outdoor crop cycle did not integrate the 65-second night window.");
        Require(indoorCycle.MaximumSustainableGrowthRate == indoorExpected,
            "Indoor crop cycle incorrectly inherited outdoor night slowdown.");
        Require(CropGrowthCycleAuthority.ResolveOutdoorTimeOfDayMultiplier(
                TimeOfDay.Night) == 0.55f
            && CropGrowthCycleAuthority.ResolveOutdoorTimeOfDayMultiplier(
                TimeOfDay.Noon) == 1f,
            "Crop runtime day/night multiplier drifted.");
    }

    private static void VerifyGenomeWitnesses(
        IResourceEconomyContentCatalog economy,
        CropGenomeReachableMaximumWitnessCatalog witnesses)
    {
        foreach (CropDefinitionSO crop in economy.Crops)
        {
            CropGenomeReachableMaximumWitnessSnapshot witness =
                witnesses.Capture(crop.CropId);
            SeedLotState seed = witness.CreatePhysicalSeedLot();
            Require(string.Equals(seed.cropId, crop.CropId,
                        StringComparison.Ordinal)
                    && string.Equals(seed.cultivarGenomeId,
                        witness.GenomeId,
                        StringComparison.Ordinal)
                    && seed.generation == 0
                    && seed.pathogenLoad == 0f,
                "Crop maximum witness did not produce an exact physical seed lot: "
                + crop.CropId);
            Require(ReferenceEquals(
                    crop.BaseGenome,
                    witness.Definition)
                    || witness.YieldMultiplier
                    >= CropGenomePhenotypeAuthority.Create(
                            crop.BaseGenome.CreateRuntimeDefinition())
                        .YieldMultiplier,
                "Crop maximum witness regressed below its base cultivar: "
                + crop.CropId);
        }

        CropGenomeReachableMaximumWitnessSnapshot ordinary =
            witnesses.Capture("crop:cave-mushroom");
        Require(string.Equals(
                    ordinary.GenomeId,
                    "genome:cave-mushroom:base",
                    StringComparison.Ordinal)
                && ordinary.YieldMultiplier == 1f
                && ordinary.GrowthMultiplier == 1f,
            "Ordinary crop must use its real registered base seed, not a fabricated +2 genome.");
        CropGenomeReachableMaximumWitnessSnapshot textile =
            witnesses.Capture("crop:ember-cotton");
        Require(string.Equals(
                    textile.GenomeId,
                    "genome:ember-cotton:bulk",
                    StringComparison.Ordinal)
                && textile.YieldMultiplier == 1.10f
                && textile.GrowthMultiplier == 1.16f,
            "Textile crop must select its authored high-yield physical cultivar.");
    }

    private static ProductionFacilityCapacitySubject Subject(
        BuildingSO definition)
    {
        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        string definitionId =
            ProductionFacilityDefinitionIdentity.Resolve(definition);
        return new ProductionFacilityCapacitySubject(
            (BuildingInstanceId)("building:qa:crop-throughput:"
                + definitionId.Replace(':', '-')),
            Vector2Int.zero,
            definitionId,
            workstation.WorkstationTag,
            buffer.physicalOutputBufferCycleCapacity,
            ProductionFacilityCapacitySubjectAdapter
                .CaptureWorkstationLaneProfile(definition),
            ProductionFacilityCapacitySubjectAdapter.CaptureProcessFluidProfile(
                definition));
    }

    private static decimal Exact(float value) => decimal.Parse(
        value.ToString("R", CultureInfo.InvariantCulture),
        NumberStyles.Float,
        CultureInfo.InvariantCulture);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FixedWorkRateQuery : IProductionWorkRateMaximumQuery
    {
        public ProductionRecipeWorkRateMaximumQueryResult Capture(
            ProductionWorkRateMaximumSubject subject)
        {
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append("crop-throughput-fixed-work-rate@1");
            digest.Append(subject.FacilityDefinitionId);
            digest.Append(subject.WorkstationTag);
            digest.Append(subject.WorkTypeId.Value);
            digest.Append(subject.OperationDefinitionId);
            digest.Append(subject.OperationSourceDigest);
            return ProductionRecipeWorkRateMaximumQueryResult.Complete(
                new ProductionRecipeWorkRateMaximumSnapshot(
                    1_000L,
                    0L,
                    digest.ComputeSha256()));
        }
    }

    private sealed class AssetContentSource : IGameContentDefinitionSource
    {
        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name,
                    new[] { "Assets/Resources/SO" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(value => value != null)
                .OrderBy(
                    AssetDatabase.GetAssetPath,
                    StringComparer.Ordinal)
                .ToArray();

        public T RequireSingle<T>() where T : ScriptableObject =>
            GetAll<T>().Single();
    }
}
