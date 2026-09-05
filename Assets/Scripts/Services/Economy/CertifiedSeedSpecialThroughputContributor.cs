using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CertifiedSeedFacilityOutputBranchIdentity
{
    public static string ForCrop(string cropId)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            cropId,
            nameof(cropId));
        return "certified-seed:" + cropId;
    }
}

/// <summary>
/// Shared physical transform authority used by both the live certified-seed
/// runtime and output-clearance execution descriptors. The selected seed lot
/// keeps its exact cultivar/generation component; only pathogen load changes.
/// </summary>
public static class CertifiedSeedPhysicalTransformAuthority
{
    public const string CertificationKitItemId =
        "supply:certified-seed-kit";
    public const int SeedInputQuantity = 1;
    public const int CertificationKitInputQuantity = 1;
    public const int OutputQuantity = 1;
    public const float PathogenLoadReduction = 30f;
    public const string TransformContractId =
        "certified-seed-transform:preserve-lot-reduce-pathogen";

    public static SeedLotState Project(SeedLotState source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        SeedLotItemStateCodec.Encode(source);
        SeedLotState result = source.Clone();
        result.pathogenLoad = Mathf.Clamp(
            result.pathogenLoad - PathogenLoadReduction,
            0f,
            100f);
        SeedLotItemStateCodec.Encode(result);
        return result;
    }
}

public sealed class CertifiedSeedLotWitnessSnapshot
{
    public CertifiedSeedLotWitnessSnapshot(SeedLotState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        ItemInstanceComponentSaveData component =
            SeedLotItemStateCodec.Encode(state);
        CropId = state.cropId;
        CultivarGenomeId = state.cultivarGenomeId;
        Generation = state.generation;
        PathogenLoad = state.pathogenLoad;
        CanonicalComponent = component.ToCanonicalString();
        ComponentFingerprint =
            ProductionDomainOutputPublicationService
                .CaptureComponentFingerprint(new[] { component });
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("certified-seed-lot-witness@1");
        digest.Append(CropId);
        digest.Append(CultivarGenomeId);
        digest.Append(Generation);
        digest.AppendFloat(PathogenLoad);
        digest.Append(CanonicalComponent);
        digest.Append(ComponentFingerprint);
        SourceDigest = digest.ComputeSha256();
    }

    public string CropId { get; }
    public string CultivarGenomeId { get; }
    public int Generation { get; }
    public float PathogenLoad { get; }
    public string CanonicalComponent { get; }
    public string ComponentFingerprint { get; }
    public string SourceDigest { get; }

    public SeedLotState CreateState() => new()
    {
        cropId = CropId,
        cultivarGenomeId = CultivarGenomeId,
        generation = Generation,
        pathogenLoad = PathogenLoad
    };
}

public sealed class CertifiedSeedCycleSnapshot
{
    internal CertifiedSeedCycleSnapshot(
        string branchId,
        string cropId,
        string seedItemId,
        CertifiedSeedLotWitnessSnapshot inputSeedLot,
        int operatingHoursPerCycle,
        string sourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            cropId,
            nameof(cropId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            seedItemId,
            nameof(seedItemId));
        if (inputSeedLot == null
            || !string.Equals(
                inputSeedLot.CropId,
                cropId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Certified-seed cycle requires an exact authored seed-lot witness.");
        }
        if (operatingHoursPerCycle <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(operatingHoursPerCycle));
        BranchId = branchId;
        CropId = cropId;
        SeedItemId = seedItemId;
        InputSeedLot = inputSeedLot;
        OutputSeedLot = new CertifiedSeedLotWitnessSnapshot(
            CertifiedSeedPhysicalTransformAuthority.Project(
                inputSeedLot.CreateState()));
        OperatingHoursPerCycle = operatingHoursPerCycle;
        SourceDigest = sourceDigest;
    }

    public string BranchId { get; }
    public string CropId { get; }
    public string SeedItemId { get; }
    public CertifiedSeedLotWitnessSnapshot InputSeedLot { get; }
    public CertifiedSeedLotWitnessSnapshot OutputSeedLot { get; }
    public string CertificationKitItemId =>
        CertifiedSeedPhysicalTransformAuthority.CertificationKitItemId;
    public int SeedInputQuantity =>
        CertifiedSeedPhysicalTransformAuthority.SeedInputQuantity;
    public int CertificationKitInputQuantity =>
        CertifiedSeedPhysicalTransformAuthority.CertificationKitInputQuantity;
    public int OutputQuantity =>
        CertifiedSeedPhysicalTransformAuthority.OutputQuantity;
    public float PathogenLoadReduction =>
        CertifiedSeedPhysicalTransformAuthority.PathogenLoadReduction;
    public string TransformContractId =>
        CertifiedSeedPhysicalTransformAuthority.TransformContractId;
    public int OperatingHoursPerCycle { get; }
    public string SourceDigest { get; }
}

public interface ICertifiedSeedCycleMaximumQuery
{
    CertifiedSeedCycleSnapshot Capture(string branchId);
}

/// <summary>
/// Mirrors the persisted monotonic OperatingDay gate in CertifiedSeedRuntime.
/// Each crop branch has at most one reachable completion opportunity per day;
/// duplicate or stale day events cannot increase throughput.
/// </summary>
public sealed class CertifiedSeedCycleMaximumAuthority :
    ICertifiedSeedCycleMaximumQuery
{
    public const string Schema = "certified-seed-cycle-maximum@2";
    public const string ExecutionPath = "execution:operating-day-gate";

    private readonly IReadOnlyDictionary<string, CertifiedSeedCycleSnapshot>
        byBranch;

    public CertifiedSeedCycleMaximumAuthority(
        IResourceEconomyContentCatalog catalog)
    {
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        Dictionary<string, CertifiedSeedCycleSnapshot> captured = new(
            StringComparer.Ordinal);
        foreach (CropDefinitionSO crop in (catalog.Crops
                     ?? Array.Empty<CropDefinitionSO>())
                 .Where(value => value != null)
                 .OrderBy(value => value.CropId, StringComparer.Ordinal))
        {
            string branchId = CertifiedSeedFacilityOutputBranchIdentity.ForCrop(
                crop.CropId);
            if (crop.BaseGenome == null
                || !string.Equals(
                    crop.BaseGenome.CropId,
                    crop.CropId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Certified-seed cycle requires the crop's authored base genome: "
                    + crop.CropId);
            }
            CertifiedSeedLotWitnessSnapshot inputSeedLot = new(new SeedLotState
            {
                cropId = crop.CropId,
                cultivarGenomeId = crop.BaseGenome.GenomeId,
                generation = 0,
                pathogenLoad = 0f
            });
            CertifiedSeedLotWitnessSnapshot outputSeedLot = new(
                CertifiedSeedPhysicalTransformAuthority.Project(
                    inputSeedLot.CreateState()));
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append(Schema);
            digest.Append(branchId);
            digest.Append(ExecutionPath);
            digest.Append(crop.CropId);
            digest.Append(crop.SeedItemId);
            digest.Append(inputSeedLot.SourceDigest);
            digest.Append(outputSeedLot.SourceDigest);
            digest.Append(
                CertifiedSeedPhysicalTransformAuthority.CertificationKitItemId);
            digest.Append(CertifiedSeedPhysicalTransformAuthority
                .SeedInputQuantity);
            digest.Append(CertifiedSeedPhysicalTransformAuthority
                .CertificationKitInputQuantity);
            digest.Append(CertifiedSeedPhysicalTransformAuthority.OutputQuantity);
            digest.AppendFloat(CertifiedSeedPhysicalTransformAuthority
                .PathogenLoadReduction);
            digest.Append(CertifiedSeedPhysicalTransformAuthority
                .TransformContractId);
            digest.Append(GameSimulationTimeRules.HoursPerDay);
            if (!captured.TryAdd(branchId, new CertifiedSeedCycleSnapshot(
                    branchId,
                    crop.CropId,
                    crop.SeedItemId,
                    inputSeedLot,
                    GameSimulationTimeRules.HoursPerDay,
                    digest.ComputeSha256())))
            {
                throw new InvalidOperationException(
                    "Duplicate certified-seed cycle branch: " + branchId);
            }
        }
        if (captured.Count == 0)
            throw new InvalidOperationException(
                "Certified-seed cycle authority requires authored crops.");
        byBranch = captured;
    }

    public CertifiedSeedCycleSnapshot Capture(string branchId)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        if (!byBranch.TryGetValue(branchId, out CertifiedSeedCycleSnapshot value))
            throw new InvalidOperationException(
                "Certified-seed branch has no operating-day cycle: "
                + branchId);
        return value;
    }
}

public sealed class CertifiedSeedSpecialThroughputContributor :
    IProductionSpecialThroughputContributor
{
    public const string Id = "special-throughput:certified-seed";
    public const int Version = 3;
    private const string Schema = "certified-seed-special-throughput@3";

    private readonly ICertifiedSeedCycleMaximumQuery cycles;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery masses;
    private readonly ProductionThroughputTimeScaleSnapshot timeScale;
    private readonly string contributorDigest;

    public CertifiedSeedSpecialThroughputContributor(
        ICertifiedSeedCycleMaximumQuery cycles,
        IProductionFacilityOutputCapacityBranchMassQuery masses)
    {
        this.cycles = cycles ?? throw new ArgumentNullException(nameof(cycles));
        this.masses = masses ?? throw new ArgumentNullException(nameof(masses));
        timeScale = ProductionThroughputTimeScaleAuthority.Capture();
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(Id);
        digest.Append(Version);
        digest.Append(CertifiedSeedFacilityOutputCapacityContributor.Id);
        digest.Append(CertifiedSeedCycleMaximumAuthority.ExecutionPath);
        digest.Append(timeScale.SourceDigest);
        contributorDigest = digest.ComputeSha256();
    }

    public string ContributorId => Id;
    public int ContractVersion => Version;
    public string CapacityContributorId =>
        CertifiedSeedFacilityOutputCapacityContributor.Id;

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

        List<ProductionSpecialThroughputCandidateSnapshot> candidates = new();
        foreach (ProductionFacilityOutputCapacityBranch branch in
                 capacityContribution.Branches.OrderBy(
                     value => value.BranchId,
                     StringComparer.Ordinal))
        {
            CertifiedSeedCycleSnapshot cycle = cycles.Capture(branch.BranchId);
            ProductionFacilityOutputCapacityBranchMassSnapshot mass =
                masses.Capture(branch);
            long peak = checked((long)decimal.Ceiling(
                mass.MaximumMassGrams
                / (decimal)cycle.OperatingHoursPerCycle));
            if (peak <= 0L)
                throw new InvalidOperationException(
                    "Certified-seed throughput projected no positive mass.");
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append(Schema);
            digest.Append(contributorDigest);
            digest.Append(facility.SourceDigest);
            digest.Append(capacityContribution.SourceDigest);
            digest.Append(branch.BranchId);
            digest.Append(cycle.SourceDigest);
            digest.Append(cycle.CropId);
            digest.Append(cycle.SeedItemId);
            digest.Append(cycle.InputSeedLot.SourceDigest);
            digest.Append(cycle.OutputSeedLot.SourceDigest);
            digest.Append(cycle.CertificationKitItemId);
            digest.Append(cycle.SeedInputQuantity);
            digest.Append(cycle.CertificationKitInputQuantity);
            digest.Append(cycle.OutputQuantity);
            digest.AppendFloat(cycle.PathogenLoadReduction);
            digest.Append(cycle.TransformContractId);
            digest.Append(cycle.OperatingHoursPerCycle);
            digest.Append(mass.SourceDigest);
            digest.Append(mass.MaximumMassGrams);
            digest.Append(peak);
            candidates.Add(new ProductionSpecialThroughputCandidateSnapshot(
                facility.DefinitionId,
                facility.WorkstationTag,
                CapacityContributorId,
                branch.BranchId,
                peak,
                digest.ComputeSha256()));
        }

        return new ProductionSpecialThroughputContributorResult(
            Id,
            Version,
            CapacityContributorId,
            true,
            candidates,
            Array.Empty<ProductionThroughputCoverageGap>(),
            contributorDigest);
    }

    private void Validate(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution contribution)
    {
        if (facility == null) throw new ArgumentNullException(nameof(facility));
        if (contribution == null)
            throw new ArgumentNullException(nameof(contribution));
        if (!string.Equals(
                contribution.ContributorId,
                CapacityContributorId,
                StringComparison.Ordinal)
            || !facility.CapacityContributions.Any(value => string.Equals(
                value.ContributorId,
                CapacityContributorId,
                StringComparison.Ordinal)
                && string.Equals(
                    value.SourceDigest,
                    contribution.SourceDigest,
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Certified-seed throughput received foreign capacity authority.");
        }
    }
}
