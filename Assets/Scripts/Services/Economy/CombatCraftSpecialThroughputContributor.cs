using System;
using System.Collections.Generic;
using System.Linq;

public static class CombatCraftFacilityOutputBranchIdentity
{
    public static string Primary(string craftDefinitionId)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            craftDefinitionId,
            nameof(craftDefinitionId));
        return "combat-craft-primary:" + craftDefinitionId;
    }

    public static string Recovery(string craftDefinitionId, string materialId)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            craftDefinitionId,
            nameof(craftDefinitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            materialId,
            nameof(materialId));
        return "combat-craft-recovery:" + craftDefinitionId + ":" + materialId;
    }
}

public sealed class CombatCraftCycleSnapshot
{
    internal CombatCraftCycleSnapshot(
        string craftDefinitionId,
        string branchId,
        string selectedMaterialId,
        string selectedMaterialItemId,
        IReadOnlyDictionary<string, int> physicalInputs,
        float requiredWork,
        string executionPath,
        string sourceDigest)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            craftDefinitionId,
            nameof(craftDefinitionId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            executionPath,
            nameof(executionPath));
        ProductionAuthoredThroughputContractRules.RequireDigest(
            sourceDigest,
            nameof(sourceDigest));
        bool hasMaterialId = !string.IsNullOrEmpty(selectedMaterialId);
        bool hasMaterialItemId = !string.IsNullOrEmpty(selectedMaterialItemId);
        if (hasMaterialId != hasMaterialItemId)
            throw new ArgumentException(
                "Combat cycle selected material identity is incomplete.");
        if (hasMaterialId)
        {
            ProductionAuthoredThroughputContractRules.RequireCanonical(
                selectedMaterialId,
                nameof(selectedMaterialId));
            ProductionAuthoredThroughputContractRules.RequireCanonical(
                selectedMaterialItemId,
                nameof(selectedMaterialItemId));
        }
        KeyValuePair<string, int>[] orderedInputs = (physicalInputs
                ?? throw new ArgumentNullException(nameof(physicalInputs)))
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        if (orderedInputs.Any(value => value.Value <= 0
                || string.IsNullOrWhiteSpace(value.Key)
                || !string.Equals(value.Key, value.Key.Trim(),
                    StringComparison.Ordinal))
            || orderedInputs.Select(value => value.Key)
                .Distinct(StringComparer.Ordinal).Count() != orderedInputs.Length)
        {
            throw new InvalidOperationException(
                "Combat cycle physical input vector is invalid.");
        }
        if (!float.IsFinite(requiredWork) || requiredWork <= 0f)
            throw new ArgumentOutOfRangeException(nameof(requiredWork));
        CraftDefinitionId = craftDefinitionId;
        BranchId = branchId;
        SelectedMaterialId = selectedMaterialId ?? string.Empty;
        SelectedMaterialItemId = selectedMaterialItemId ?? string.Empty;
        PhysicalInputs = new System.Collections.ObjectModel
            .ReadOnlyDictionary<string, int>(orderedInputs.ToDictionary(
                value => value.Key,
                value => value.Value,
                StringComparer.Ordinal));
        RequiredWork = requiredWork;
        ExecutionPath = executionPath;
        SourceDigest = sourceDigest;
    }

    public string CraftDefinitionId { get; }
    public string BranchId { get; }
    public string SelectedMaterialId { get; }
    public string SelectedMaterialItemId { get; }
    public IReadOnlyDictionary<string, int> PhysicalInputs { get; }
    public float RequiredWork { get; }
    public string ExecutionPath { get; }
    public string SourceDigest { get; }
}

public interface ICombatCraftCycleMaximumQuery
{
    CombatCraftCycleSnapshot Capture(string branchId);
}

/// <summary>
/// Shares the live combat craft calculator, ammunition work, and rejected
/// backlog rule with the producer-wide throughput ledger. Branch IDs are
/// captured from catalogs and never parsed.
/// </summary>
public sealed class CombatCraftCycleMaximumAuthority :
    ICombatCraftCycleMaximumQuery
{
    public const string Schema = "combat-craft-cycle-maximum@2";
    public const float AmmunitionPrimaryWork = 4f;
    public const float RejectedRecoveryWorkMultiplier = 0.25f;
    public const float MinimumRequiredWork = 0.10f;

    private readonly IReadOnlyDictionary<string, CombatCraftCycleSnapshot>
        byBranch;

    public CombatCraftCycleMaximumAuthority(
        ICombatCraftDefinitionCatalog crafts,
        ICombatEquipmentCatalog equipment,
        IResourceEconomyContentCatalog economy,
        IBalanceWorkCalculator work,
        ICombatRejectedRecoveryProjector recovery)
    {
        if (crafts == null) throw new ArgumentNullException(nameof(crafts));
        if (equipment == null) throw new ArgumentNullException(nameof(equipment));
        if (economy == null) throw new ArgumentNullException(nameof(economy));
        if (work == null) throw new ArgumentNullException(nameof(work));
        if (recovery == null) throw new ArgumentNullException(nameof(recovery));

        Dictionary<string, CombatCraftCycleSnapshot> captured = new(
            StringComparer.Ordinal);
        foreach (CombatCraftDefinitionSnapshot craft in crafts.All
                     .Where(value => value != null)
                     .OrderBy(value => value.CraftDefinitionId,
                         StringComparer.Ordinal))
        {
            if (craft.Kind == CombatCraftOutputKind.GenericAmmunition)
            {
                Add(captured, CreateAmmunition(craft));
                continue;
            }

            if (!equipment.TryGet(
                    craft.CraftDefinitionId,
                    out CombatEquipmentDefinitionSO definition)
                || definition == null
                || !string.Equals(
                    definition.EquipmentId,
                    craft.CraftDefinitionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat cycle authority cannot join equipment definition: "
                    + craft.CraftDefinitionId);
            }

            Variant[] legal = economy.Materials
                .Where(value => value != null && definition.AllowsMaterial(value))
                .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
                .Select(value => CaptureVariant(definition, value, work))
                .ToArray();
            if (legal.Length == 0)
                throw new InvalidOperationException(
                    "Combat equipment has no legal craft material: "
                    + craft.CraftDefinitionId);
            Add(captured, CreatePrimary(craft, definition, legal));

            IReadOnlyList<CombatRejectedRecoveryProjection> projections =
                recovery.CaptureDefinitionMaximums(craft.CraftDefinitionId);
            foreach (CombatRejectedRecoveryProjection projection in projections
                         .Where(value => value != null
                             && value.Outputs.Count > 0)
                         .OrderBy(value => value.MaterialId,
                             StringComparer.Ordinal))
            {
                Variant variant = legal.SingleOrDefault(value => string.Equals(
                    value.MaterialId,
                    projection.MaterialId,
                    StringComparison.Ordinal));
                if (variant == null)
                    throw new InvalidOperationException(
                        "Combat recovery material is not a legal craft variant: "
                        + projection.MaterialId);
                float recoveryWork = ResolveRejectedRecoveryWork(
                    variant.RequiredWork);
                string branchId = CombatCraftFacilityOutputBranchIdentity
                    .Recovery(craft.CraftDefinitionId, projection.MaterialId);
                CanonicalSemanticDigestBuilder digest = new();
                digest.Append(Schema);
                digest.Append(branchId);
                digest.Append("execution:backlog-recovery");
                digest.Append(craft.DefinitionFingerprint);
                AppendDefinition(digest, definition);
                AppendVariant(digest, variant);
                digest.Append(projection.SourceDigest);
                digest.AppendFloat(RejectedRecoveryWorkMultiplier);
                digest.AppendFloat(MinimumRequiredWork);
                digest.AppendFloat(recoveryWork);
                Add(captured, new CombatCraftCycleSnapshot(
                    craft.CraftDefinitionId,
                    branchId,
                    variant.MaterialId,
                    variant.ItemId,
                    EmptyInputs,
                    recoveryWork,
                    "execution:backlog-recovery",
                    digest.ComputeSha256()));
            }
        }
        byBranch = captured;
    }

    public CombatCraftCycleSnapshot Capture(string branchId)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            branchId,
            nameof(branchId));
        if (!byBranch.TryGetValue(branchId, out CombatCraftCycleSnapshot value))
            throw new InvalidOperationException(
                "Combat throughput branch has no shared cycle authority: "
                + branchId);
        return value;
    }

    public static float ResolveAmmunitionPrimaryWork() =>
        AmmunitionPrimaryWork;

    public static float ResolveRejectedRecoveryWork(float craftWork)
    {
        if (!float.IsFinite(craftWork) || craftWork <= 0f)
            throw new ArgumentOutOfRangeException(nameof(craftWork));
        return UnityEngine.Mathf.Max(
            MinimumRequiredWork,
            craftWork * RejectedRecoveryWorkMultiplier);
    }

    private static CombatCraftCycleSnapshot CreateAmmunition(
        CombatCraftDefinitionSnapshot craft)
    {
        string branchId = CombatCraftFacilityOutputBranchIdentity.Primary(
            craft.CraftDefinitionId);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(branchId);
        digest.Append("execution:manual-primary");
        digest.Append(craft.DefinitionFingerprint);
        digest.AppendFloat(AmmunitionPrimaryWork);
        AppendInputs(digest, craft.FixedInputs);
        return new CombatCraftCycleSnapshot(
            craft.CraftDefinitionId,
            branchId,
            string.Empty,
            string.Empty,
            craft.FixedInputs,
            AmmunitionPrimaryWork,
            "execution:manual-primary",
            digest.ComputeSha256());
    }

    private static CombatCraftCycleSnapshot CreatePrimary(
        CombatCraftDefinitionSnapshot craft,
        CombatEquipmentDefinitionSO definition,
        IReadOnlyList<Variant> variants)
    {
        Variant minimum = variants
            .OrderBy(value => value.RequiredWork)
            .ThenBy(value => value.MaterialId, StringComparer.Ordinal)
            .First();
        string branchId = CombatCraftFacilityOutputBranchIdentity.Primary(
            craft.CraftDefinitionId);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(branchId);
        digest.Append("execution:manual-primary");
        digest.Append(craft.DefinitionFingerprint);
        AppendDefinition(digest, definition);
        digest.Append(variants.Count);
        foreach (Variant variant in variants
                     .OrderBy(value => value.MaterialId,
                         StringComparer.Ordinal))
            AppendVariant(digest, variant);
        digest.Append("minimum");
        AppendVariant(digest, minimum);
        IReadOnlyDictionary<string, int> physicalInputs = CapturePrimaryInputs(
            definition,
            minimum.ItemId);
        AppendInputs(digest, physicalInputs);
        return new CombatCraftCycleSnapshot(
            craft.CraftDefinitionId,
            branchId,
            minimum.MaterialId,
            minimum.ItemId,
            physicalInputs,
            minimum.RequiredWork,
            "execution:manual-primary",
            digest.ComputeSha256());
    }

    private static Variant CaptureVariant(
        CombatEquipmentDefinitionSO definition,
        CraftMaterialDefinitionSO material,
        IBalanceWorkCalculator work)
    {
        float requiredWork = work.CalculateEquipment(
            definition,
            material.ItemId);
        if (!float.IsFinite(requiredWork) || requiredWork <= 0f)
            throw new InvalidOperationException(
                "Combat work calculator returned invalid cycle work.");
        return new Variant(material.MaterialId, material.ItemId, requiredWork);
    }

    private static void Add(
        IDictionary<string, CombatCraftCycleSnapshot> target,
        CombatCraftCycleSnapshot value)
    {
        if (!target.TryAdd(value.BranchId, value))
            throw new InvalidOperationException(
                "Duplicate combat cycle branch: " + value.BranchId);
    }

    private static void AppendDefinition(
        CanonicalSemanticDigestBuilder digest,
        CombatEquipmentDefinitionSO definition)
    {
        digest.Append(definition.EquipmentId);
        digest.Append(definition.ItemId);
        digest.Append(definition.PrimaryMaterialAmount);
        digest.Append((int)definition.Era);
        digest.Append(definition.Tier);
        digest.Append(definition.RequiredComponentInputs.Count);
        foreach (ItemAmountDefinition component in definition
                     .RequiredComponentInputs
                     .OrderBy(value => value.ItemId, StringComparer.Ordinal))
        {
            digest.Append(component.ItemId);
            digest.Append(component.Amount);
        }
    }

    private static void AppendVariant(
        CanonicalSemanticDigestBuilder digest,
        Variant variant)
    {
        digest.Append(variant.MaterialId);
        digest.Append(variant.ItemId);
        digest.AppendFloat(variant.RequiredWork);
    }

    private static IReadOnlyDictionary<string, int> CapturePrimaryInputs(
        CombatEquipmentDefinitionSO definition,
        string materialItemId)
    {
        Dictionary<string, int> inputs = new(StringComparer.Ordinal);
        AddInput(inputs, materialItemId, definition.PrimaryMaterialAmount);
        foreach (ItemAmountDefinition component in definition
                     .RequiredComponentInputs)
            AddInput(inputs, component.ItemId, component.Amount);
        return new System.Collections.ObjectModel
            .ReadOnlyDictionary<string, int>(inputs);
    }

    private static void AddInput(
        IDictionary<string, int> inputs,
        string itemId,
        int quantity)
    {
        ProductionAuthoredThroughputContractRules.RequireCanonical(
            itemId,
            nameof(itemId));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        inputs[itemId] = checked(inputs.TryGetValue(itemId, out int existing)
            ? existing + quantity
            : quantity);
    }

    private static void AppendInputs(
        CanonicalSemanticDigestBuilder digest,
        IReadOnlyDictionary<string, int> inputs)
    {
        KeyValuePair<string, int>[] ordered = inputs
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        digest.Append(ordered.Length);
        foreach (KeyValuePair<string, int> input in ordered)
        {
            digest.Append(input.Key);
            digest.Append(input.Value);
        }
    }

    private static readonly IReadOnlyDictionary<string, int> EmptyInputs =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(StringComparer.Ordinal));

    private sealed class Variant
    {
        internal Variant(
            string materialId,
            string itemId,
            float requiredWork)
        {
            MaterialId = materialId;
            ItemId = itemId;
            RequiredWork = requiredWork;
        }

        internal string MaterialId { get; }
        internal string ItemId { get; }
        internal float RequiredWork { get; }
    }
}

public sealed class CombatCraftSpecialThroughputContributor :
    IProductionSpecialThroughputContributor
{
    public const string Id = "special-throughput:combat-craft";
    public const int Version = 3;
    private const string Schema = "combat-craft-special-throughput@3";

    private readonly ICombatCraftCycleMaximumQuery cycles;
    private readonly IProductionWorkRateMaximumQuery workRates;
    private readonly IProductionFacilityOutputCapacityBranchMassQuery masses;
    private readonly ProductionThroughputTimeScaleSnapshot timeScale;
    private readonly string contributorDigest;

    public CombatCraftSpecialThroughputContributor(
        ICombatCraftCycleMaximumQuery cycles,
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
        digest.Append(CombatCraftFacilityOutputCapacityContributor.Id);
        digest.Append(timeScale.SourceDigest);
        contributorDigest = digest.ComputeSha256();
    }

    public string ContributorId => Id;
    public int ContractVersion => Version;
    public string CapacityContributorId =>
        CombatCraftFacilityOutputCapacityContributor.Id;

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
            CombatCraftCycleSnapshot cycle = cycles.Capture(branch.BranchId);
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
                ProductionWorkCycleThroughputAuthority.CaptureManualOnly(
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
                    "Combat special throughput projected no positive mass.");
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
            candidateDigest.Append(
                ProductionAuthoredThroughputContractRules.DecimalToken(
                    cyclesPerGameHour));
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
        CombatCraftCycleSnapshot cycle)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(contributorDigest);
        digest.Append(facility.SourceDigest);
        digest.Append(contribution.SourceDigest);
        digest.Append(branch.BranchId);
        digest.Append(cycle.CraftDefinitionId);
        digest.Append(cycle.SelectedMaterialId);
        digest.Append(cycle.SelectedMaterialItemId);
        digest.Append(cycle.PhysicalInputs.Count);
        foreach (KeyValuePair<string, int> input in cycle.PhysicalInputs
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            digest.Append(input.Key);
            digest.Append(input.Value);
        }
        digest.Append(cycle.ExecutionPath);
        digest.AppendFloat(cycle.RequiredWork);
        digest.Append(cycle.SourceDigest);
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
                "Combat special throughput received foreign capacity authority.");
        }
    }
}
