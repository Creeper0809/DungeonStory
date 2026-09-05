using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

/// <summary>
/// Shared live and audit authority for crop growth speed. Crop throughput is a
/// serial sow -> calendar growth -> harvest cycle; growth is not workstation
/// work and must not be scaled by actor work rate.
/// </summary>
public static class CropGrowthCycleAuthority
{
    public const string Schema = "crop-growth-cycle-authority@1";
    public const float ClimateControlMultiplier = 1.08f;
    public const float CropCalendarMultiplier = 1.05f;
    public const float OutdoorNightMultiplier = 0.55f;
    public const float OutdoorRainMultiplier = 1.10f;
    public const float OutdoorFogMultiplier = 0.85f;
    public const float OutdoorStormMultiplier = 0.55f;
    public const float OutdoorHeatWaveMultiplier = 0.90f;
    public const float OutdoorColdSnapMultiplier = 0.90f;

    // GameCalendarRuntime currently classifies [0,40) and [155,180) as
    // Night. The sustainable peak integrates both intervals instead of
    // pretending that a multi-day crop can remain in daylight forever.
    public const int OutdoorNightRealSecondsPerDay = 65;
    public const int OutdoorNonNightRealSecondsPerDay = 115;

    public static float ResolveIndoorRuntimeMultiplier(
        BuildingCropPlotAbility ability,
        bool climateControlOperational,
        bool cropCalendarOperational,
        CropGenomePhenotype phenotype)
    {
        if (ability == null)
            throw new ArgumentNullException(nameof(ability));
        return ability.GrowthMultiplier
            * (climateControlOperational ? ClimateControlMultiplier : 1f)
            * (cropCalendarOperational ? CropCalendarMultiplier : 1f)
            * phenotype.GrowthMultiplier;
    }

    public static float ResolveOutdoorRuntimeMultiplier(
        BuildingCropPlotAbility ability,
        CropDefinitionSO crop,
        CropGenomePhenotype phenotype,
        SurvivalEnvironmentSnapshot environment,
        TimeOfDay? timeOfDay,
        bool cropCalendarOperational,
        out string blockedReason)
    {
        if (ability == null)
            throw new ArgumentNullException(nameof(ability));
        if (crop == null)
            throw new ArgumentNullException(nameof(crop));
        blockedReason = string.Empty;
        Vector2 authoredRange = crop.TemperatureRange;
        Vector2 range = new(
            authoredRange.x - phenotype.ColdToleranceDegrees,
            authoredRange.y + phenotype.HeatToleranceDegrees);
        if (environment.OutdoorTemperature < range.x)
        {
            blockedReason =
                $"기온이 너무 낮음 ({environment.OutdoorTemperature:0.#}도)";
            return 0f;
        }
        if (environment.OutdoorTemperature > range.y)
        {
            blockedReason =
                $"기온이 너무 높음 ({environment.OutdoorTemperature:0.#}도)";
            return 0f;
        }

        return ability.GrowthMultiplier
            * ResolveOutdoorWeatherMultiplier(environment.Weather)
            * ResolveOutdoorTimeOfDayMultiplier(timeOfDay)
            * (cropCalendarOperational ? CropCalendarMultiplier : 1f)
            * phenotype.GrowthMultiplier;
    }

    public static float ResolveOutdoorWeatherMultiplier(
        SurvivalWeatherType weather) => weather switch
    {
        SurvivalWeatherType.Rain => OutdoorRainMultiplier,
        SurvivalWeatherType.Fog => OutdoorFogMultiplier,
        SurvivalWeatherType.Storm => OutdoorStormMultiplier,
        SurvivalWeatherType.HeatWave => OutdoorHeatWaveMultiplier,
        SurvivalWeatherType.ColdSnap => OutdoorColdSnapMultiplier,
        _ => 1f
    };

    public static float ResolveOutdoorTimeOfDayMultiplier(
        TimeOfDay? timeOfDay) =>
        timeOfDay == TimeOfDay.Night ? OutdoorNightMultiplier : 1f;

    public static decimal ResolveMaximumSustainableGrowthRate(
        BuildingCropPlotAbility ability,
        float phenotypeGrowthMultiplier)
    {
        if (ability == null)
            throw new ArgumentNullException(nameof(ability));
        if (!float.IsFinite(phenotypeGrowthMultiplier)
            || phenotypeGrowthMultiplier <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phenotypeGrowthMultiplier));
        }
        decimal baseMultiplier = Exact(ability.GrowthMultiplier);
        decimal phenotype = Exact(phenotypeGrowthMultiplier);
        decimal calendar = Exact(CropCalendarMultiplier);
        if (ability.Indoor)
        {
            return checked(baseMultiplier
                * Exact(ClimateControlMultiplier)
                * calendar
                * phenotype);
        }

        decimal dayIntegrated = checked(
            (OutdoorNonNightRealSecondsPerDay
                + OutdoorNightRealSecondsPerDay
                    * Exact(OutdoorNightMultiplier))
            / Exact(GameSimulationTimeRules.SecondsPerDay));
        return checked(baseMultiplier
            * Exact(OutdoorRainMultiplier)
            * calendar
            * phenotype
            * dayIntegrated);
    }

    public static string CaptureSourceDigest()
    {
        if (OutdoorNightRealSecondsPerDay
                + OutdoorNonNightRealSecondsPerDay
                != (int)GameSimulationTimeRules.SecondsPerDay)
        {
            throw new InvalidOperationException(
                "Crop day/night integration drifted from the game-day clock.");
        }
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.AppendFloat(ClimateControlMultiplier);
        digest.AppendFloat(CropCalendarMultiplier);
        digest.AppendFloat(OutdoorNightMultiplier);
        digest.AppendFloat(OutdoorRainMultiplier);
        digest.AppendFloat(OutdoorFogMultiplier);
        digest.AppendFloat(OutdoorStormMultiplier);
        digest.AppendFloat(OutdoorHeatWaveMultiplier);
        digest.AppendFloat(OutdoorColdSnapMultiplier);
        digest.Append(OutdoorNightRealSecondsPerDay);
        digest.Append(OutdoorNonNightRealSecondsPerDay);
        digest.AppendFloat(GameSimulationTimeRules.SecondsPerDay);
        return digest.ComputeSha256();
    }

    private static decimal Exact(float value) => decimal.Parse(
        value.ToString("R", CultureInfo.InvariantCulture),
        NumberStyles.Float,
        CultureInfo.InvariantCulture);
}

public sealed class CropHarvestCycleMaximumSnapshot
{
    internal CropHarvestCycleMaximumSnapshot(
        string facilityDefinitionId,
        string branchId,
        string cropId,
        bool indoor,
        float sowWork,
        float harvestWork,
        decimal growthHours,
        decimal maximumSustainableGrowthRate,
        string sourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            facilityDefinitionId,
            nameof(facilityDefinitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            cropId,
            nameof(cropId));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        if (!float.IsFinite(sowWork) || sowWork <= 0f
            || !float.IsFinite(harvestWork) || harvestWork <= 0f
            || growthHours <= 0m
            || maximumSustainableGrowthRate <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(growthHours));
        }
        FacilityDefinitionId = facilityDefinitionId;
        BranchId = branchId;
        CropId = cropId;
        Indoor = indoor;
        SowWork = sowWork;
        HarvestWork = harvestWork;
        GrowthHours = growthHours;
        MaximumSustainableGrowthRate = maximumSustainableGrowthRate;
        SourceDigest = sourceDigest;
    }

    public string FacilityDefinitionId { get; }
    public string BranchId { get; }
    public string CropId { get; }
    public bool Indoor { get; }
    public float SowWork { get; }
    public float HarvestWork { get; }
    public decimal GrowthHours { get; }
    public decimal MaximumSustainableGrowthRate { get; }
    public decimal EffectiveGrowthHours => checked(
        GrowthHours / MaximumSustainableGrowthRate);
    public string SourceDigest { get; }
}

public interface ICropHarvestCycleMaximumQuery
{
    CropHarvestCycleMaximumSnapshot Capture(
        string facilityDefinitionId,
        string branchId);
}

public sealed class CropHarvestCycleMaximumAuthority :
    ICropHarvestCycleMaximumQuery
{
    public const string Schema = "crop-harvest-cycle-maximum@2";
    private readonly IReadOnlyDictionary<string, CropHarvestCycleMaximumSnapshot>
        byFacilityBranch;

    public CropHarvestCycleMaximumAuthority(
        IResourceEconomyContentCatalog catalog,
        IGameContentDefinitionSource content,
        IEnumerable<ICropHarvestReachableMaximumWitnessContributor>
            witnessContributors)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (content == null) throw new ArgumentNullException(nameof(content));
        CropGenomeReachableMaximumWitnessCatalog genomeWitnesses = new(
            content);
        CropHarvestReachableMaximumWitnessSnapshot[] witnesses =
            (witnessContributors
                ?? throw new ArgumentNullException(nameof(witnessContributors)))
            .Where(value => value != null)
            .OrderBy(value => value.ContributorId, StringComparer.Ordinal)
            .Select(value => value.Capture())
            .OrderBy(value => value.WitnessId, StringComparer.Ordinal)
            .ToArray();
        if (witnesses.Length == 0
            || witnesses.Select(value => value.WitnessId)
                .Distinct(StringComparer.Ordinal).Count() != witnesses.Length)
        {
            throw new InvalidOperationException(
                "Crop cycle authority requires unique reachable witnesses.");
        }
        string growthDigest = CropGrowthCycleAuthority.CaptureSourceDigest();
        Dictionary<string, CropHarvestCycleMaximumSnapshot> captured = new(
            StringComparer.Ordinal);
        foreach (BuildingSO definition in content.GetAll<BuildingSO>()
                     .Where(value => value != null
                         && value.GetAbility<BuildingCropPlotAbility>() != null)
                     .OrderBy(
                         ProductionFacilityDefinitionIdentity.Resolve,
                         StringComparer.Ordinal))
        {
            string definitionId =
                ProductionFacilityDefinitionIdentity.Resolve(definition);
            BuildingCropPlotAbility ability =
                definition.GetAbility<BuildingCropPlotAbility>();
            foreach (CropDefinitionSO crop in catalog.Crops
                         .Where(value => value != null
                             && (!ability.Indoor || value.IndoorAllowed))
                         .OrderBy(value => value.CropId, StringComparer.Ordinal))
            {
                CropGenomeReachableMaximumWitnessSnapshot genomeWitness =
                    genomeWitnesses.Capture(crop.CropId);
                decimal maximumGrowthRate = CropGrowthCycleAuthority
                    .ResolveMaximumSustainableGrowthRate(
                        ability,
                        genomeWitness.GrowthMultiplier);
                foreach (CropHarvestReachableMaximumWitnessSnapshot witness
                         in witnesses)
                {
                    string branchId = CropHarvestFacilityOutputBranchIdentity
                        .ForReachableWitness(crop.CropId, witness.WitnessId);
                    CanonicalSemanticDigestBuilder digest = new();
                    digest.Append(Schema);
                    digest.Append(growthDigest);
                    digest.Append(witness.SourceDigest);
                    digest.Append(genomeWitness.SourceDigest);
                    digest.Append(definitionId);
                    digest.Append(branchId);
                    digest.Append(ability.Indoor);
                    digest.AppendFloat(ability.GrowthMultiplier);
                    digest.Append(crop.CropId);
                    digest.AppendFloat(crop.SowWork);
                    digest.AppendFloat(crop.HarvestWork);
                    digest.AppendFloat(crop.GrowthHours);
                    digest.Append(maximumGrowthRate.ToString(
                        CultureInfo.InvariantCulture));
                    CropHarvestCycleMaximumSnapshot value = new(
                        definitionId,
                        branchId,
                        crop.CropId,
                        ability.Indoor,
                        crop.SowWork,
                        crop.HarvestWork,
                        Exact(crop.GrowthHours),
                        maximumGrowthRate,
                        digest.ComputeSha256());
                    if (!captured.TryAdd(Key(definitionId, branchId), value))
                    {
                        throw new InvalidOperationException(
                            "Duplicate crop facility/cycle branch: "
                            + definitionId + "/" + branchId);
                    }
                }
            }
        }
        byFacilityBranch = captured;
    }

    public CropHarvestCycleMaximumSnapshot Capture(
        string facilityDefinitionId,
        string branchId)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            facilityDefinitionId,
            nameof(facilityDefinitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        if (!byFacilityBranch.TryGetValue(
                Key(facilityDefinitionId, branchId),
                out CropHarvestCycleMaximumSnapshot value))
        {
            throw new InvalidOperationException(
                "Crop throughput branch has no shared cycle authority: "
                + facilityDefinitionId + "/" + branchId);
        }
        return value;
    }

    private static string Key(string definitionId, string branchId) =>
        definitionId + "\n" + branchId;

    private static decimal Exact(float value) => decimal.Parse(
        value.ToString("R", CultureInfo.InvariantCulture),
        NumberStyles.Float,
        CultureInfo.InvariantCulture);
}

public sealed class CropHarvestSpecialThroughputContributor :
    IProductionSpecialThroughputContributor
{
    public const string Id = "special-throughput:crop-harvest";
    public const int Version = 2;
    private const string Schema = "crop-harvest-special-throughput@2";

    private readonly ICropHarvestCycleMaximumQuery cycles;
    private readonly IProductionWorkRateMaximumQuery workRates;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery masses;
    private readonly ProductionThroughputTimeScaleSnapshot timeScale;
    private readonly string contributorDigest;

    public CropHarvestSpecialThroughputContributor(
        ICropHarvestCycleMaximumQuery cycles,
        IProductionWorkRateMaximumQuery workRates,
        IProductionFacilityOutputCapacityBranchMassQuery masses)
    {
        this.cycles = cycles ?? throw new ArgumentNullException(nameof(cycles));
        this.workRates = workRates ?? throw new ArgumentNullException(nameof(workRates));
        this.masses = masses ?? throw new ArgumentNullException(nameof(masses));
        timeScale = ProductionThroughputTimeScaleAuthority.Capture();
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(Id);
        digest.Append(Version);
        digest.Append(CropHarvestFacilityOutputCapacityContributor.Id);
        digest.Append(CropGrowthCycleAuthority.CaptureSourceDigest());
        digest.Append(timeScale.SourceDigest);
        contributorDigest = digest.ComputeSha256();
    }

    public string ContributorId => Id;
    public int ContractVersion => Version;
    public string CapacityContributorId =>
        CropHarvestFacilityOutputCapacityContributor.Id;

    public ProductionSpecialThroughputContributorResult Capture(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution capacityContribution)
    {
        Validate(facility, capacityContribution);
        if (!capacityContribution.AppliesToFacility)
        {
            return new ProductionSpecialThroughputContributorResult(
                Id,
                Version,
                CapacityContributorId,
                false,
                Array.Empty<ProductionSpecialThroughputCandidateSnapshot>(),
                Array.Empty<ProductionThroughputCoverageGap>(),
                contributorDigest);
        }

        ProductionFacilityCapacitySubject subject =
            facility.RequireFacilitySubject();
        List<ProductionSpecialThroughputCandidateSnapshot> candidates = new();
        List<ProductionThroughputCoverageGap> gaps = new();
        foreach (ProductionFacilityOutputCapacityBranch branch in
                 capacityContribution.Branches.OrderBy(
                     value => value.BranchId,
                     StringComparer.Ordinal))
        {
            CropHarvestCycleMaximumSnapshot cycle = cycles.Capture(
                facility.DefinitionId,
                branch.BranchId);
            ProductionRecipeWorkRateMaximumQueryResult sowRate = CaptureRate(
                subject,
                branch.BranchId + ":sow",
                BuiltInWorkTypeIds.Sow,
                cycle.SourceDigest);
            ProductionRecipeWorkRateMaximumQueryResult harvestRate = CaptureRate(
                subject,
                branch.BranchId + ":harvest",
                BuiltInWorkTypeIds.Harvest,
                cycle.SourceDigest);
            if (!sowRate.HasSnapshot || !harvestRate.HasSnapshot)
            {
                ProductionRecipeWorkRateMaximumQueryResult missing =
                    !sowRate.HasSnapshot ? sowRate : harvestRate;
                CanonicalSemanticDigestBuilder gapDigest = BeginDigest(
                    facility,
                    capacityContribution,
                    branch,
                    cycle);
                gapDigest.Append("gap");
                gapDigest.Append((int)missing.MissingReason);
                gapDigest.Append(missing.Detail);
                gapDigest.Append(sowRate.SourceDigest);
                gapDigest.Append(harvestRate.SourceDigest);
                gaps.Add(new ProductionThroughputCoverageGap(
                    facility.DefinitionId,
                    facility.WorkstationTag,
                    ProductionThroughputProducerKind.CapacityContributor,
                    CapacityContributorId,
                    branch.BranchId,
                    missing.MissingReason,
                    missing.Detail,
                    gapDigest.ComputeSha256()));
                continue;
            }

            ProductionWorkCycleThroughputSnapshot sow =
                ProductionWorkCycleThroughputAuthority.Capture(
                    subject.WorkstationLaneProfile,
                    sowRate.Snapshot,
                    ProductionOutputFactor.One,
                    Exact(cycle.SowWork),
                    timeScale);
            ProductionWorkCycleThroughputSnapshot harvest =
                ProductionWorkCycleThroughputAuthority.Capture(
                    subject.WorkstationLaneProfile,
                    harvestRate.Snapshot,
                    ProductionOutputFactor.One,
                    Exact(cycle.HarvestWork),
                    timeScale);
            decimal cycleHours = checked(
                1m / sow.CyclesPerGameHour
                + cycle.EffectiveGrowthHours
                + 1m / harvest.CyclesPerGameHour);
            decimal cyclesPerGameHour = checked(1m / cycleHours);
            ProductionFacilityOutputCapacityBranchMassSnapshot mass =
                masses.Capture(branch);
            long peak = checked((long)decimal.Ceiling(
                mass.MaximumMassGrams * cyclesPerGameHour));
            if (peak <= 0L)
                throw new InvalidOperationException(
                    "Crop special throughput projected no positive mass.");

            CanonicalSemanticDigestBuilder candidateDigest = BeginDigest(
                facility,
                capacityContribution,
                branch,
                cycle);
            candidateDigest.Append(sowRate.Snapshot.SourceDigest);
            candidateDigest.Append(harvestRate.Snapshot.SourceDigest);
            candidateDigest.Append(sow.SourceDigest);
            candidateDigest.Append(harvest.SourceDigest);
            candidateDigest.Append(mass.SourceDigest);
            candidateDigest.Append(mass.MaximumMassGrams);
            candidateDigest.Append(cycleHours.ToString(CultureInfo.InvariantCulture));
            candidateDigest.Append(cyclesPerGameHour.ToString(
                CultureInfo.InvariantCulture));
            candidateDigest.Append(peak);
            candidates.Add(new ProductionSpecialThroughputCandidateSnapshot(
                facility.DefinitionId,
                facility.WorkstationTag,
                CapacityContributorId,
                branch.BranchId,
                peak,
                candidateDigest.ComputeSha256()));
        }

        return new ProductionSpecialThroughputContributorResult(
            Id,
            Version,
            CapacityContributorId,
            true,
            candidates,
            gaps,
            contributorDigest);
    }

    private ProductionRecipeWorkRateMaximumQueryResult CaptureRate(
        ProductionFacilityCapacitySubject subject,
        string operationId,
        WorkTypeId workTypeId,
        string cycleDigest) => workRates.Capture(
        new ProductionWorkRateMaximumSubject(
            subject.DefinitionId,
            subject.WorkstationTag,
            subject.WorkstationLaneProfile,
            workTypeId,
            operationId,
            cycleDigest));

    private CanonicalSemanticDigestBuilder BeginDigest(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution contribution,
        ProductionFacilityOutputCapacityBranch branch,
        CropHarvestCycleMaximumSnapshot cycle)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(contributorDigest);
        digest.Append(facility.SourceDigest);
        digest.Append(contribution.SourceDigest);
        digest.Append(branch.BranchId);
        digest.Append(cycle.SourceDigest);
        digest.Append(cycle.Indoor);
        digest.AppendFloat(cycle.SowWork);
        digest.AppendFloat(cycle.HarvestWork);
        digest.Append(cycle.GrowthHours.ToString(CultureInfo.InvariantCulture));
        digest.Append(cycle.MaximumSustainableGrowthRate.ToString(
            CultureInfo.InvariantCulture));
        digest.Append(timeScale.SourceDigest);
        return digest;
    }

    private void Validate(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution contribution)
    {
        if (facility == null) throw new ArgumentNullException(nameof(facility));
        if (contribution == null)
            throw new ArgumentNullException(nameof(contribution));
        if (!string.Equals(contribution.ContributorId, CapacityContributorId,
                StringComparison.Ordinal)
            || !facility.CapacityContributions.Any(value => string.Equals(
                value.ContributorId,
                CapacityContributorId,
                StringComparison.Ordinal)
                && string.Equals(value.SourceDigest, contribution.SourceDigest,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Crop special throughput received foreign capacity authority.");
        }
    }

    private static decimal Exact(float value) => decimal.Parse(
        value.ToString("R", CultureInfo.InvariantCulture),
        NumberStyles.Float,
        CultureInfo.InvariantCulture);
}
