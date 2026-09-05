using System;
using System.Collections.Generic;
using System.Linq;

public static class CropHarvestOutputMaximumAuthority
{
    public static string HarvestOutputLineId(string cropId) =>
        "output:crop-harvest/" + RequireCanonicalCropId(cropId) + "/harvest";

    public static string SeedOutputLineId(string cropId) =>
        "output:crop-harvest/" + RequireCanonicalCropId(cropId)
        + "/returned-seed";

    public static int ResolveMaximumHarvestQuantity(
        CropDefinitionSO crop,
        bool indoor,
        ICharacterPerformanceDefinitionMaximumQuery performance)
    {
        if (crop == null)
            throw new ArgumentNullException(nameof(crop));
        CharacterPerformanceDefinitionMaximumSnapshot worker =
            (performance ?? throw new ArgumentNullException(nameof(performance)))
            .Capture(CropHarvestOutputRules.PerformanceFormulaId);
        ProductionOutputFactor fixedMaximum = indoor
            ? ProductionOutputFactorAuthority.ResolveMaximumGrandProject(
                    "crop-indoor")
            : ProductionOutputFactor.One;
        fixedMaximum = fixedMaximum
            .Multiply(CropHarvestOutputRules.EcologyYieldMaximum)
            .Multiply(CropHarvestOutputRules.SoilDiagnosticsMaximum);
        double maximum = crop.Yield
            * worker.MaximumValue
            * fixedMaximum.Numerator
            / fixedMaximum.Denominator;
        if (double.IsNaN(maximum)
            || double.IsInfinity(maximum)
            || maximum <= 0d
            || maximum > int.MaxValue)
        {
            throw new InvalidOperationException(
                "Crop harvest maximum quantity is invalid: " + crop.CropId);
        }
        return checked((int)Math.Ceiling(
            maximum + Math.Max(1d, maximum) * 1e-12d));
    }

    public static int ResolveMaximumReturnedSeedQuantity(
        IGameplayEffectResultBoundsQuery effectBounds)
    {
        double seedFactor = Math.Max(
            1d,
            (effectBounds ?? throw new ArgumentNullException(nameof(effectBounds)))
            .RequireFiniteMaximum(
                CropHarvestOutputRules.SeedYieldEffectTargetId));
        double maximum = CropHarvestOutputRules.MaximumReturnedSeedCount
            * seedFactor
            + CropHarvestOutputRules.SeedSelectionBonus;
        if (double.IsNaN(maximum)
            || double.IsInfinity(maximum)
            || maximum <= 0d
            || maximum > int.MaxValue)
        {
            throw new InvalidOperationException(
                "Crop returned-seed maximum quantity is invalid.");
        }
        return checked((int)Math.Ceiling(maximum));
    }

    private static string RequireCanonicalCropId(string cropId)
    {
        if (string.IsNullOrWhiteSpace(cropId)
            || !string.Equals(cropId, cropId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical crop ID is required.",
                nameof(cropId));
        }
        return cropId;
    }
}

public static class CropHarvestFacilityOutputBranchIdentity
{
    public static string ForCrop(string cropId)
    {
        if (string.IsNullOrWhiteSpace(cropId)
            || !string.Equals(cropId, cropId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical crop ID is required.",
                nameof(cropId));
        }
        return "crop-harvest:" + cropId;
    }

    public static string ForReachableWitness(
        string cropId,
        string witnessId)
    {
        string cropBranch = ForCrop(cropId);
        if (string.IsNullOrWhiteSpace(witnessId)
            || !string.Equals(
                witnessId,
                witnessId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical crop witness ID is required.",
                nameof(witnessId));
        }
        return cropBranch + ":witness:" + witnessId;
    }
}

public sealed class CropHarvestFacilityOutputCapacityContributor :
    IProductionFacilityOutputCapacityContributor
{
    public const string Id =
        "production-facility-output-capacity:crop-harvest";
    public const int Version = 2;

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IReadOnlyList<
        CropHarvestReachableMaximumWitnessSnapshot> witnesses;
    private readonly CropGenomeReachableMaximumWitnessCatalog genomeWitnesses;
    private readonly IReadOnlyDictionary<string, bool> indoorByDefinitionId;

    public CropHarvestFacilityOutputCapacityContributor(
        IResourceEconomyContentCatalog catalog,
        IGameContentDefinitionSource content,
        IEnumerable<ICropHarvestReachableMaximumWitnessContributor>
            witnessContributors)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        genomeWitnesses = new CropGenomeReachableMaximumWitnessCatalog(
            content ?? throw new ArgumentNullException(nameof(content)));
        ICropHarvestReachableMaximumWitnessContributor[] contributors =
            (witnessContributors
                ?? throw new ArgumentNullException(nameof(witnessContributors)))
            .Where(value => value != null)
            .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
            .ToArray();
        if (contributors.Length == 0
            || contributors.Select(value => value.ContributorId)
                .Distinct(StringComparer.Ordinal).Count() != contributors.Length)
        {
            throw new InvalidOperationException(
                "Crop harvest capacity requires unique reachable witnesses.");
        }
        CropHarvestReachableMaximumWitnessSnapshot[] capturedWitnesses =
            contributors.Select(value => value.Capture())
                .OrderBy(value => value.WitnessId, StringComparer.Ordinal)
                .ToArray();
        if (capturedWitnesses.Select(value => value.WitnessId)
                .Distinct(StringComparer.Ordinal).Count()
            != capturedWitnesses.Length)
        {
            throw new InvalidOperationException(
                "Crop harvest reachable witness IDs are duplicated.");
        }
        witnesses = Array.AsReadOnly(capturedWitnesses);
        BuildingSO[] plots = content
            .GetAll<BuildingSO>()
            .Where(value => value != null
                && value.GetAbility<BuildingCropPlotAbility>() != null)
            .OrderBy(
                ProductionFacilityDefinitionIdentity.Resolve,
                StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, bool> captured = new(StringComparer.Ordinal);
        foreach (BuildingSO plot in plots)
        {
            string definitionId =
                ProductionFacilityDefinitionIdentity.Resolve(plot);
            BuildingProductionBufferAbility buffer =
                plot.GetProductionBufferAbility();
            BuildingProductionWorkstationAbility workstation =
                plot.GetProductionWorkstationAbility();
            if (buffer == null
                || workstation == null
                || buffer.physicalOutputBufferCycleCapacity is < 2 or > 4
                || string.IsNullOrWhiteSpace(workstation.WorkstationTag))
            {
                throw new InvalidOperationException(
                    "Crop plot facility lacks a canonical output-buffer authority: "
                    + definitionId);
            }
            if (!captured.TryAdd(
                    definitionId,
                    plot.GetAbility<BuildingCropPlotAbility>().Indoor))
            {
                throw new InvalidOperationException(
                    "Duplicate crop plot facility definition ID: "
                    + definitionId);
            }
        }
        indoorByDefinitionId = captured;
    }

    public string ContributorId => Id;
    public int ContractVersion => Version;

    public ProductionFacilityOutputCapacityContribution Capture(
        ProductionFacilityCapacitySubject subject)
    {
        if (!indoorByDefinitionId.TryGetValue(
                subject.DefinitionId,
                out bool indoor))
        {
            return new ProductionFacilityOutputCapacityContribution(
                Id,
                Version,
                false,
                Array.Empty<ProductionFacilityOutputCapacityBranch>());
        }

        List<ProductionFacilityOutputCapacityBranch> branches = new();
        foreach (CropDefinitionSO crop in (catalog.Crops
                     ?? Array.Empty<CropDefinitionSO>())
                 .Where(value => value != null
                     && (!indoor || value.IndoorAllowed))
                 .OrderBy(value => value.CropId, StringComparer.Ordinal))
        {
            CropGenomeReachableMaximumWitnessSnapshot genomeWitness =
                genomeWitnesses.Capture(crop.CropId);
            foreach (CropHarvestReachableMaximumWitnessSnapshot witness
                     in witnesses)
            {
                ProductionOutputFactor fixedOutput = indoor
                    ? ProductionOutputFactorAuthority
                        .ResolveMaximumGrandProject("crop-indoor")
                    : ProductionOutputFactor.One;
                int maximumHarvest = CropHarvestOutputRules
                    .ResolveHarvestQuantity(
                        crop.Yield,
                        fixedOutput.Numerator / (float)fixedOutput.Denominator,
                        witness.WorkerYieldMultiplier,
                        1f,
                        genomeWitness.YieldMultiplier,
                        hasSoilDiagnostics: true);
                int maximumReturnedSeeds = CropHarvestOutputRules
                    .ResolveReturnedSeedQuantity(
                        CropHarvestOutputRules.MaximumReturnedSeedCount,
                        witness.ReturnedSeedMultiplier,
                        hasSeedSelection: true);
                CanonicalSemanticDigestBuilder branchDigest = new();
                branchDigest.Append(
                    "crop-harvest-capacity-reachable-branch@1");
                branchDigest.Append(witness.SourceDigest);
                branchDigest.Append(genomeWitness.SourceDigest);
                branches.Add(new ProductionFacilityOutputCapacityBranch(
                    CropHarvestFacilityOutputBranchIdentity
                        .ForReachableWitness(crop.CropId, witness.WitnessId),
                    new[]
                    {
                        new ProductionFacilityOutputMaximumMassRequest(
                            CropHarvestOutputMaximumAuthority.HarvestOutputLineId(
                                crop.CropId),
                            crop.HarvestItemId,
                            ProductionOutputCapabilityIds.StandardDefinition,
                            maximumHarvest),
                        new ProductionFacilityOutputMaximumMassRequest(
                            CropHarvestOutputMaximumAuthority.SeedOutputLineId(
                                crop.CropId),
                            crop.SeedItemId,
                            ProductionOutputCapabilityIds.CropHarvestSeedLot,
                            maximumReturnedSeeds)
                    },
                    branchDigest.ComputeSha256()));
            }
        }

        return new ProductionFacilityOutputCapacityContribution(
            Id,
            Version,
            true,
            branches);
    }
}
