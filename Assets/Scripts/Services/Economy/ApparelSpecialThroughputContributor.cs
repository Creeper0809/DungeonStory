using System;
using System.Collections.Generic;
using System.Linq;

public static class ApparelFacilityOutputBranchIdentity
{
    public static string Craft(string apparelId)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            apparelId,
            nameof(apparelId));
        return "apparel-craft:" + apparelId;
    }

    public static string RejectedRecovery(
        string apparelId,
        string materialId)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            apparelId,
            nameof(apparelId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            materialId,
            nameof(materialId));
        return "apparel-rejected-recovery:" + apparelId + ":" + materialId;
    }
}

public sealed class ApparelCraftCycleSnapshot
{
    private const string SelectedWitnessSchema =
        "apparel-craft-cycle-selected-witness@1";

    internal ApparelCraftCycleSnapshot(
        string apparelId,
        string branchId,
        float requiredWork,
        string executionPath,
        string selectedMaterialId,
        string selectedPhysicalItemId,
        ApparelSizeClass selectedSize,
        ApparelModificationKind selectedModifications,
        int exactMaterialQuantity,
        string sourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            apparelId,
            nameof(apparelId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            executionPath,
            nameof(executionPath));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            selectedMaterialId,
            nameof(selectedMaterialId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            selectedPhysicalItemId,
            nameof(selectedPhysicalItemId));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        if (!float.IsFinite(requiredWork) || requiredWork <= 0f)
            throw new ArgumentOutOfRangeException(nameof(requiredWork));
        if (!Enum.IsDefined(typeof(ApparelSizeClass), selectedSize))
            throw new ArgumentOutOfRangeException(nameof(selectedSize));
        const ApparelModificationKind canonicalModificationMask =
            ApparelModificationKind.TailOpening
            | ApparelModificationKind.WingSlits
            | ApparelModificationKind.HornClearance;
        if ((selectedModifications & ~canonicalModificationMask) != 0)
            throw new ArgumentOutOfRangeException(nameof(selectedModifications));
        if (exactMaterialQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(exactMaterialQuantity));
        ApparelId = apparelId;
        BranchId = branchId;
        RequiredWork = requiredWork;
        ExecutionPath = executionPath;
        SelectedMaterialId = selectedMaterialId;
        SelectedPhysicalItemId = selectedPhysicalItemId;
        SelectedSize = selectedSize;
        SelectedModifications = selectedModifications;
        ExactMaterialQuantity = exactMaterialQuantity;
        SourceDigest = sourceDigest;

        CanonicalSemanticDigestBuilder witnessDigest = new();
        witnessDigest.Append(SelectedWitnessSchema);
        witnessDigest.Append(ApparelId);
        witnessDigest.Append(BranchId);
        witnessDigest.AppendFloat(RequiredWork);
        witnessDigest.Append(ExecutionPath);
        witnessDigest.Append(SelectedMaterialId);
        witnessDigest.Append(SelectedPhysicalItemId);
        witnessDigest.Append((int)SelectedSize);
        witnessDigest.Append((int)SelectedModifications);
        witnessDigest.Append(ExactMaterialQuantity);
        witnessDigest.Append(SourceDigest);
        SelectedWitnessSourceDigest = witnessDigest.ComputeSha256();
    }

    public string ApparelId { get; }
    public string BranchId { get; }
    public float RequiredWork { get; }
    public string ExecutionPath { get; }
    public string SelectedMaterialId { get; }
    public string SelectedPhysicalItemId { get; }
    public ApparelSizeClass SelectedSize { get; }
    public ApparelModificationKind SelectedModifications { get; }
    public int ExactMaterialQuantity { get; }
    public string SourceDigest { get; }
    public string SelectedWitnessSourceDigest { get; }
}

public interface IApparelCraftCycleMaximumQuery
{
    ApparelCraftCycleSnapshot Capture(string branchId);
}

/// <summary>
/// Shares the live apparel work calculator and rejected-backlog rule with the
/// throughput ledger. No expected quality probability is used: recovery is an
/// independently reachable backlog execution path.
/// </summary>
public sealed class ApparelCraftCycleMaximumAuthority :
    IApparelCraftCycleMaximumQuery
{
    public const string Schema = "apparel-craft-cycle-maximum@2";
    public const float RejectedRecoveryWorkMultiplier = 0.20f;
    public const float MinimumRequiredWork = 0.10f;

    private readonly IReadOnlyDictionary<string, ApparelCraftCycleSnapshot>
        byBranch;

    public ApparelCraftCycleMaximumAuthority(
        IApparelDefinitionCatalog apparel,
        ITextileMaterialCatalog materials,
        IBalanceWorkCalculator work)
    {
        if (apparel == null) throw new ArgumentNullException(nameof(apparel));
        if (materials == null) throw new ArgumentNullException(nameof(materials));
        if (work == null) throw new ArgumentNullException(nameof(work));

        Dictionary<string, ApparelCraftCycleSnapshot> captured = new(
            StringComparer.Ordinal);
        TextileMaterialDefinitionSO[] orderedMaterials = materials.Definitions
            .Where(value => value != null)
            .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
            .ToArray();
        foreach (ApparelDefinitionSO definition in apparel.Definitions
                     .Where(value => value != null)
                     .OrderBy(value => value.ApparelId, StringComparer.Ordinal))
        {
            TextileMaterialDefinitionSO[] allowed = orderedMaterials
                .Where(value => (value.Tags & definition.AllowedMaterialTags) != 0)
                .ToArray();
            if (allowed.Length == 0)
                throw new InvalidOperationException(
                    "Apparel definition has no legal physical material: "
                    + definition.ApparelId);

            List<Variant> primaryVariants = new();
            foreach (TextileMaterialDefinitionSO material in allowed)
                primaryVariants.AddRange(CaptureVariants(
                    definition,
                    material,
                    work));
            string primaryBranch = ApparelFacilityOutputBranchIdentity.Craft(
                definition.ApparelId);
            Add(captured, CreateSnapshot(
                primaryBranch,
                "execution:manual-primary",
                definition,
                primaryVariants));

            foreach (TextileMaterialDefinitionSO material in allowed)
            {
                List<Variant> variants = CaptureVariants(
                    definition,
                    material,
                    work);
                string recoveryBranch =
                    ApparelFacilityOutputBranchIdentity.RejectedRecovery(
                        definition.ApparelId,
                        material.MaterialId);
                Variant minimum = SelectMinimum(variants);
                float recoveryOnlyWork = ResolveRejectedRecoveryWork(
                    minimum.RequiredWork);
                CanonicalSemanticDigestBuilder digest = new();
                digest.Append(Schema);
                digest.Append(recoveryBranch);
                digest.Append("execution:backlog-recovery");
                AppendDefinition(digest, definition);
                AppendVariant(digest, minimum);
                digest.AppendFloat(RejectedRecoveryWorkMultiplier);
                digest.AppendFloat(MinimumRequiredWork);
                digest.AppendFloat(recoveryOnlyWork);
                Add(captured, new ApparelCraftCycleSnapshot(
                    definition.ApparelId,
                    recoveryBranch,
                    recoveryOnlyWork,
                    "execution:backlog-recovery",
                    minimum.MaterialId,
                    minimum.PhysicalItemId,
                    minimum.Size,
                    minimum.Modifications,
                    minimum.ExactMaterialQuantity,
                    digest.ComputeSha256()));
            }
        }
        byBranch = captured;
    }

    public ApparelCraftCycleSnapshot Capture(string branchId)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        if (!byBranch.TryGetValue(branchId, out ApparelCraftCycleSnapshot value))
            throw new InvalidOperationException(
                "Apparel throughput branch has no shared cycle authority: "
                + branchId);
        return value;
    }

    public static float ResolveRejectedRecoveryWork(float craftWork)
    {
        if (!float.IsFinite(craftWork) || craftWork <= 0f)
            throw new ArgumentOutOfRangeException(nameof(craftWork));
        return UnityEngine.Mathf.Max(
            MinimumRequiredWork,
            craftWork * RejectedRecoveryWorkMultiplier);
    }

    private static ApparelCraftCycleSnapshot CreateSnapshot(
        string branchId,
        string executionPath,
        ApparelDefinitionSO definition,
        IReadOnlyList<Variant> variants)
    {
        Variant minimum = SelectMinimum(variants);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(branchId);
        digest.Append(executionPath);
        AppendDefinition(digest, definition);
        digest.Append(variants.Count);
        foreach (Variant variant in variants
                     .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
                     .ThenBy(value => (int)value.Size)
                     .ThenBy(value => (int)value.Modifications))
            AppendVariant(digest, variant);
        digest.Append("minimum");
        AppendVariant(digest, minimum);
        return new ApparelCraftCycleSnapshot(
            definition.ApparelId,
            branchId,
            minimum.RequiredWork,
            executionPath,
            minimum.MaterialId,
            minimum.PhysicalItemId,
            minimum.Size,
            minimum.Modifications,
            minimum.ExactMaterialQuantity,
            digest.ComputeSha256());
    }

    private static List<Variant> CaptureVariants(
        ApparelDefinitionSO definition,
        TextileMaterialDefinitionSO material,
        IBalanceWorkCalculator work)
    {
        List<Variant> variants = new();
        int exactMaterialQuantity = UnityEngine.Mathf.Max(
            1,
            UnityEngine.Mathf.CeilToInt(2f * definition.TailoringCoefficient));
        foreach (ApparelSizeClass size in Enum.GetValues(
                     typeof(ApparelSizeClass)).Cast<ApparelSizeClass>())
        {
            for (int mask = 0; mask <= (int)(
                     ApparelModificationKind.TailOpening
                     | ApparelModificationKind.WingSlits
                     | ApparelModificationKind.HornClearance); mask++)
            {
                ApparelModificationKind modifications =
                    (ApparelModificationKind)mask;
                if ((modifications & ~definition.SupportedModifications) != 0)
                    continue;
                float requiredWork = work.CalculateApparel(
                    definition,
                    material,
                    size,
                    modifications);
                if (!float.IsFinite(requiredWork) || requiredWork <= 0f)
                    throw new InvalidOperationException(
                        "Apparel work calculator returned invalid cycle work.");
                variants.Add(new Variant(
                    material.MaterialId,
                    material.PhysicalItemId,
                    size,
                    modifications,
                    exactMaterialQuantity,
                    requiredWork));
            }
        }
        return variants;
    }

    private static Variant SelectMinimum(IReadOnlyList<Variant> variants) =>
        (variants ?? throw new ArgumentNullException(nameof(variants)))
        .OrderBy(value => value.RequiredWork)
        .ThenBy(value => value.MaterialId, StringComparer.Ordinal)
        .ThenBy(value => (int)value.Size)
        .ThenBy(value => (int)value.Modifications)
        .FirstOrDefault() ?? throw new InvalidOperationException(
            "Apparel cycle authority has no legal variant.");

    private static void Add(
        IDictionary<string, ApparelCraftCycleSnapshot> target,
        ApparelCraftCycleSnapshot value)
    {
        if (!target.TryAdd(value.BranchId, value))
            throw new InvalidOperationException(
                "Duplicate apparel cycle branch: " + value.BranchId);
    }

    private static void AppendDefinition(
        CanonicalSemanticDigestBuilder digest,
        ApparelDefinitionSO definition)
    {
        digest.Append(definition.ApparelId);
        digest.Append(definition.PhysicalItemId);
        digest.Append((int)definition.AllowedMaterialTags);
        digest.Append((int)definition.SupportedModifications);
        digest.AppendFloat(definition.TailoringCoefficient);
    }

    private static void AppendVariant(
        CanonicalSemanticDigestBuilder digest,
        Variant variant)
    {
        digest.Append(variant.MaterialId);
        digest.Append(variant.PhysicalItemId);
        digest.Append((int)variant.Size);
        digest.Append((int)variant.Modifications);
        digest.Append(variant.ExactMaterialQuantity);
        digest.AppendFloat(variant.RequiredWork);
    }

    private sealed class Variant
    {
        internal Variant(
            string materialId,
            string physicalItemId,
            ApparelSizeClass size,
            ApparelModificationKind modifications,
            int exactMaterialQuantity,
            float requiredWork)
        {
            MaterialId = materialId;
            PhysicalItemId = physicalItemId;
            Size = size;
            Modifications = modifications;
            ExactMaterialQuantity = exactMaterialQuantity;
            RequiredWork = requiredWork;
        }

        internal string MaterialId { get; }
        internal string PhysicalItemId { get; }
        internal ApparelSizeClass Size { get; }
        internal ApparelModificationKind Modifications { get; }
        internal int ExactMaterialQuantity { get; }
        internal float RequiredWork { get; }
    }
}

public sealed class ApparelSpecialThroughputContributor :
    IProductionSpecialThroughputContributor
{
    public const string Id = "special-throughput:apparel";
    public const int Version = 3;
    private const string Schema = "apparel-special-throughput@3";

    private readonly IApparelCraftCycleMaximumQuery cycles;
    private readonly IProductionWorkRateMaximumQuery workRates;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery masses;
    private readonly ProductionThroughputTimeScaleSnapshot timeScale;
    private readonly string contributorDigest;

    public ApparelSpecialThroughputContributor(
        IApparelCraftCycleMaximumQuery cycles,
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
        digest.Append(ApparelFacilityOutputCapacityContributor.Id);
        digest.Append(timeScale.SourceDigest);
        contributorDigest = digest.ComputeSha256();
    }

    public string ContributorId => Id;
    public int ContractVersion => Version;
    public string CapacityContributorId =>
        ApparelFacilityOutputCapacityContributor.Id;

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
            ApparelCraftCycleSnapshot cycle = cycles.Capture(branch.BranchId);
            ProductionWorkRateMaximumSubject rateSubject = new(
                subject.DefinitionId,
                subject.WorkstationTag,
                subject.WorkstationLaneProfile,
                BuiltInWorkTypeIds.Craft,
                branch.BranchId,
                cycle.SourceDigest);
            ProductionRecipeWorkRateMaximumQueryResult rate =
                workRates.Capture(rateSubject);
            if (!rate.HasSnapshot)
            {
                CanonicalSemanticDigestBuilder gapDigest = BeginDigest(
                    facility,
                    capacityContribution,
                    branch,
                    cycle);
                gapDigest.Append("gap");
                gapDigest.Append((int)rate.MissingReason);
                gapDigest.Append(rate.Detail);
                gapDigest.Append(rate.SourceDigest);
                gaps.Add(new ProductionThroughputCoverageGap(
                    facility.DefinitionId,
                    facility.WorkstationTag,
                    ProductionThroughputProducerKind.CapacityContributor,
                    CapacityContributorId,
                    branch.BranchId,
                    rate.MissingReason,
                    rate.Detail,
                    gapDigest.ComputeSha256()));
                continue;
            }

            ProductionFacilityOutputCapacityBranchMassSnapshot mass =
                masses.Capture(branch);
            ProductionWorkCycleThroughputSnapshot workCycle =
                ProductionWorkCycleThroughputAuthority.Capture(
                    subject.WorkstationLaneProfile,
                    rate.Snapshot,
                    ProductionOutputFactor.One,
                    (decimal)cycle.RequiredWork,
                    timeScale);
            decimal cyclesPerGameHour = workCycle.CyclesPerGameHour;
            long peak = checked((long)decimal.Ceiling(
                mass.MaximumMassGrams * cyclesPerGameHour));
            if (peak <= 0L)
                throw new InvalidOperationException(
                    "Apparel special throughput projected no positive mass.");
            CanonicalSemanticDigestBuilder candidateDigest = BeginDigest(
                facility,
                capacityContribution,
                branch,
                cycle);
            candidateDigest.Append(rate.Snapshot.SourceDigest);
            candidateDigest.Append(workCycle.SourceDigest);
            candidateDigest.Append((int)workCycle.Path);
            candidateDigest.Append(mass.SourceDigest);
            candidateDigest.Append(mass.MaximumMassGrams);
            candidateDigest.Append(cyclesPerGameHour.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
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

    private CanonicalSemanticDigestBuilder BeginDigest(
        ProductionSpecialThroughputFacilityContext facility,
        ProductionFacilityOutputCapacityContribution contribution,
        ProductionFacilityOutputCapacityBranch branch,
        ApparelCraftCycleSnapshot cycle)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(contributorDigest);
        digest.Append(facility.SourceDigest);
        digest.Append(contribution.SourceDigest);
        digest.Append(branch.BranchId);
        digest.Append(cycle.ApparelId);
        digest.Append(cycle.ExecutionPath);
        digest.AppendFloat(cycle.RequiredWork);
        digest.Append(cycle.SourceDigest);
        digest.Append(cycle.SelectedWitnessSourceDigest);
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
                "Apparel special throughput received foreign capacity authority.");
        }
    }
}
