using System;
using System.Collections.Generic;
using System.Linq;

public enum CombatCraftOutputKind
{
    UniqueEquipment = 0,
    GenericAmmunition = 1
}

public static class CombatCraftAllowlist
{
    public static IReadOnlyList<string> Capture(IEnumerable<string> authored)
    {
        string[] source = (authored
                ?? throw new ArgumentNullException(nameof(authored)))
            .ToArray();
        if (source.Length == 0
            || source.Any(value => string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            || source.Distinct(StringComparer.Ordinal).Count() != source.Length)
        {
            throw new InvalidOperationException(
                "Combat craft allowlist must be canonical, nonempty and unique.");
        }
        return Array.AsReadOnly(source
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
    }
}

public sealed class CombatCraftDefinitionSnapshot
{
    public CombatCraftDefinitionSnapshot(
        string craftDefinitionId,
        CombatCraftOutputKind kind,
        string outputLineId,
        ItemDefinitionId outputItemId,
        int outputQuantity,
        string outputCapabilityId,
        IReadOnlyDictionary<string, int> fixedInputs)
    {
        if (!Canonical(craftDefinitionId)
            || !Enum.IsDefined(typeof(CombatCraftOutputKind), kind)
            || !ProductionOutputDefinition.IsCanonicalOutputLineId(outputLineId)
            || !outputItemId.IsValid
            || outputQuantity <= 0
            || !Canonical(outputCapabilityId))
        {
            throw new ArgumentException("Combat craft definition is invalid.");
        }
        KeyValuePair<string, int>[] inputs = (fixedInputs
                ?? new Dictionary<string, int>(StringComparer.Ordinal))
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToArray();
        if (inputs.Any(value => !Canonical(value.Key) || value.Value <= 0)
            || inputs.Select(value => value.Key)
                .Distinct(StringComparer.Ordinal).Count() != inputs.Length)
        {
            throw new InvalidOperationException(
                "Combat craft fixed inputs are invalid.");
        }
        CraftDefinitionId = craftDefinitionId;
        Kind = kind;
        OutputLineId = outputLineId;
        OutputItemId = outputItemId;
        OutputQuantity = outputQuantity;
        OutputCapabilityId = outputCapabilityId;
        FixedInputs = new System.Collections.ObjectModel
            .ReadOnlyDictionary<string, int>(inputs.ToDictionary(
                value => value.Key,
                value => value.Value,
                StringComparer.Ordinal));

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-craft-definition@1");
        digest.Append(CraftDefinitionId);
        digest.Append((int)Kind);
        digest.Append(OutputLineId);
        digest.Append(OutputItemId.Value);
        digest.Append(OutputQuantity);
        digest.Append(OutputCapabilityId);
        digest.Append(inputs.Length);
        foreach (KeyValuePair<string, int> input in inputs)
        {
            digest.Append(input.Key);
            digest.Append(input.Value);
        }
        DefinitionFingerprint = digest.ComputeSha256();
    }

    public string CraftDefinitionId { get; }
    public CombatCraftOutputKind Kind { get; }
    public string OutputLineId { get; }
    public ItemDefinitionId OutputItemId { get; }
    public int OutputQuantity { get; }
    public string OutputCapabilityId { get; }
    public IReadOnlyDictionary<string, int> FixedInputs { get; }
    public string DefinitionFingerprint { get; }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public static class CombatAmmunitionCraftDefinitions
{
    private static readonly CombatCraftDefinitionSnapshot[] Definitions =
    {
        new(
            CombatItemDefinitions.ArrowBundleRecipeId,
            CombatCraftOutputKind.GenericAmmunition,
            CombatAmmunitionCraftOutputCapability.OutputLineId,
            (ItemDefinitionId)CombatItemDefinitions.ArrowItemId,
            20,
            ProductionOutputCapabilityIds.CombatAmmunitionCraft,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["material:lumber"] = 1,
                ["resource:feather"] = 1
            }),
        new(
            CombatItemDefinitions.BoltBundleRecipeId,
            CombatCraftOutputKind.GenericAmmunition,
            CombatAmmunitionCraftOutputCapability.OutputLineId,
            (ItemDefinitionId)CombatItemDefinitions.BoltItemId,
            12,
            ProductionOutputCapabilityIds.CombatAmmunitionCraft,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["material:iron-ingot"] = 1,
                ["material:lumber"] = 1
            })
    };
    private static readonly IReadOnlyDictionary<string, CombatCraftDefinitionSnapshot>
        ById = Definitions.ToDictionary(
            value => value.CraftDefinitionId,
            StringComparer.Ordinal);

    public static IReadOnlyList<CombatCraftDefinitionSnapshot> All => Definitions;

    public static bool TryGetExact(
        string craftDefinitionId,
        out CombatCraftDefinitionSnapshot definition) =>
        ById.TryGetValue(craftDefinitionId ?? string.Empty, out definition);
}

public interface ICombatCraftDefinitionCatalog
{
    IReadOnlyList<CombatCraftDefinitionSnapshot> All { get; }
    bool TryGetExact(
        string craftDefinitionId,
        out CombatCraftDefinitionSnapshot definition);
}

public sealed class CombatCraftDefinitionCatalog : ICombatCraftDefinitionCatalog
{
    private readonly CombatCraftDefinitionSnapshot[] all;
    private readonly IReadOnlyDictionary<string, CombatCraftDefinitionSnapshot> byId;

    public CombatCraftDefinitionCatalog(ICombatEquipmentCatalog equipment)
    {
        if (equipment == null)
            throw new ArgumentNullException(nameof(equipment));
        all = equipment.All
            .Where(value => value != null)
            .Select(value => new CombatCraftDefinitionSnapshot(
                value.EquipmentId,
                CombatCraftOutputKind.UniqueEquipment,
                CombatEquipmentCraftOutputCapability.OutputLineId,
                (ItemDefinitionId)PhysicalItemIds.ForEquipment(value.EquipmentId),
                1,
                ProductionOutputCapabilityIds.CombatEquipmentCraft,
                new Dictionary<string, int>(StringComparer.Ordinal)))
            .Concat(CombatAmmunitionCraftDefinitions.All)
            .OrderBy(value => value.CraftDefinitionId, StringComparer.Ordinal)
            .ToArray();
        if (all.Length == 0
            || all.Select(value => value.CraftDefinitionId)
                .Distinct(StringComparer.Ordinal).Count() != all.Length)
        {
            throw new InvalidOperationException(
                "Combat craft catalog requires unique definitions.");
        }
        byId = all.ToDictionary(
            value => value.CraftDefinitionId,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<CombatCraftDefinitionSnapshot> All => all;

    public bool TryGetExact(
        string craftDefinitionId,
        out CombatCraftDefinitionSnapshot definition)
    {
        if (string.IsNullOrWhiteSpace(craftDefinitionId)
            || !string.Equals(
                craftDefinitionId,
                craftDefinitionId.Trim(),
                StringComparison.Ordinal))
        {
            definition = null;
            return false;
        }
        return byId.TryGetValue(craftDefinitionId, out definition);
    }
}

public sealed class CombatCraftFacilityEligibilitySnapshot
{
    public CombatCraftFacilityEligibilitySnapshot(
        string facilityDefinitionId,
        string workstationTag,
        int outputBufferCycleCapacity,
        IReadOnlyList<CombatCraftDefinitionSnapshot> craftDefinitions)
    {
        if (string.IsNullOrWhiteSpace(facilityDefinitionId)
            || !string.Equals(
                facilityDefinitionId,
                facilityDefinitionId.Trim(),
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(workstationTag)
            || !string.Equals(
                workstationTag,
                workstationTag.Trim(),
                StringComparison.Ordinal)
            || outputBufferCycleCapacity is < 2 or > 4)
        {
            throw new ArgumentException(
                "Combat craft facility eligibility metadata is invalid.");
        }
        CombatCraftDefinitionSnapshot[] ordered = (craftDefinitions
                ?? throw new ArgumentNullException(nameof(craftDefinitions)))
            .OrderBy(value => value?.CraftDefinitionId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null)
            || ordered.Select(value => value.CraftDefinitionId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Combat craft facility requires a nonempty unique allowlist.");
        }
        FacilityDefinitionId = facilityDefinitionId;
        WorkstationTag = workstationTag;
        OutputBufferCycleCapacity = outputBufferCycleCapacity;
        CraftDefinitions = Array.AsReadOnly(ordered);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("combat-craft-facility-eligibility@1");
        digest.Append(FacilityDefinitionId);
        digest.Append(WorkstationTag);
        digest.Append(OutputBufferCycleCapacity);
        digest.Append(ordered.Length);
        foreach (CombatCraftDefinitionSnapshot definition in ordered)
        {
            digest.Append(definition.CraftDefinitionId);
            digest.Append(definition.DefinitionFingerprint);
        }
        SourceDigest = digest.ComputeSha256();
    }

    public string FacilityDefinitionId { get; }
    public string WorkstationTag { get; }
    public int OutputBufferCycleCapacity { get; }
    public IReadOnlyList<CombatCraftDefinitionSnapshot> CraftDefinitions { get; }
    public string SourceDigest { get; }

    public bool Contains(string craftDefinitionId) =>
        CraftDefinitions.Any(value => string.Equals(
            value.CraftDefinitionId,
            craftDefinitionId,
            StringComparison.Ordinal));
}

public static class CombatCraftFacilityEligibility
{
    public static CombatCraftFacilityEligibilitySnapshot Capture(
        BuildingSO definition,
        ICombatCraftDefinitionCatalog catalog)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));
        BuildingEquipmentCraftingAbility ability =
            definition.GetAbility<BuildingEquipmentCraftingAbility>();
        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        if (ability == null || workstation == null || buffer == null)
        {
            throw new InvalidOperationException(
                "Combat craft facility is missing its ability/workstation/buffer.");
        }
        IReadOnlyList<string> authored = CombatCraftAllowlist.Capture(
            ability.CraftableEquipmentIds);
        CombatCraftDefinitionSnapshot[] definitions = authored
            .Select(value => catalog.TryGetExact(value, out var resolved)
                ? resolved
                : throw new InvalidOperationException(
                    "Combat craft facility references an unknown definition: "
                    + value))
            .ToArray();
        return new CombatCraftFacilityEligibilitySnapshot(
            ProductionFacilityDefinitionIdentity.Resolve(definition),
            workstation.WorkstationTag,
            buffer.physicalOutputBufferCycleCapacity,
            definitions);
    }
}

public interface ICombatCraftFacilityEligibilityQuery
{
    bool TryCapture(
        ProductionFacilityCapacitySubject subject,
        out CombatCraftFacilityEligibilitySnapshot snapshot);
}

public sealed class CombatCraftFacilityEligibilityQuery :
    ICombatCraftFacilityEligibilityQuery
{
    private readonly IReadOnlyDictionary<string, CombatCraftFacilityEligibilitySnapshot>
        byDefinitionId;

    public CombatCraftFacilityEligibilityQuery(
        IGameContentDefinitionSource content,
        ICombatCraftDefinitionCatalog catalog)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (catalog == null)
            throw new ArgumentNullException(nameof(catalog));
        CombatCraftFacilityEligibilitySnapshot[] snapshots = content
            .GetAll<BuildingSO>()
            .Where(value => value != null
                && value.GetAbility<BuildingEquipmentCraftingAbility>() != null)
            .Select(value => CombatCraftFacilityEligibility.Capture(value, catalog))
            .OrderBy(value => value.FacilityDefinitionId, StringComparer.Ordinal)
            .ToArray();
        if (snapshots.Select(value => value.FacilityDefinitionId)
            .Distinct(StringComparer.Ordinal).Count() != snapshots.Length)
        {
            throw new InvalidOperationException(
                "Combat craft facility definitions are not unique.");
        }
        byDefinitionId = snapshots.ToDictionary(
            value => value.FacilityDefinitionId,
            StringComparer.Ordinal);
    }

    public bool TryCapture(
        ProductionFacilityCapacitySubject subject,
        out CombatCraftFacilityEligibilitySnapshot snapshot)
    {
        if (!byDefinitionId.TryGetValue(subject.DefinitionId, out snapshot))
            return false;
        if (!string.Equals(
                snapshot.WorkstationTag,
                subject.WorkstationTag,
                StringComparison.Ordinal)
            || snapshot.OutputBufferCycleCapacity
                != subject.OutputBufferCycleCapacity)
        {
            throw new InvalidOperationException(
                "Combat craft facility subject drifted from authored eligibility.");
        }
        return true;
    }
}

public sealed class CombatCraftFacilityOutputCapacityContributor :
    IProductionFacilityOutputCapacityContributor
{
    public const string Id =
        "production-facility-output-capacity:combat-craft";
    public const int Version = 2;
    private readonly ICombatCraftFacilityEligibilityQuery eligibility;
    private readonly ICombatRejectedRecoveryProjector rejectedRecovery;

    public CombatCraftFacilityOutputCapacityContributor(
        ICombatCraftFacilityEligibilityQuery eligibility,
        ICombatRejectedRecoveryProjector rejectedRecovery)
    {
        this.eligibility = eligibility
            ?? throw new ArgumentNullException(nameof(eligibility));
        this.rejectedRecovery = rejectedRecovery
            ?? throw new ArgumentNullException(nameof(rejectedRecovery));
    }

    public string ContributorId => Id;
    public int ContractVersion => Version;

    public ProductionFacilityOutputCapacityContribution Capture(
        ProductionFacilityCapacitySubject subject)
    {
        if (!eligibility.TryCapture(subject, out var facility))
        {
            return new ProductionFacilityOutputCapacityContribution(
                Id,
                Version,
                false,
                Array.Empty<ProductionFacilityOutputCapacityBranch>());
        }
        List<ProductionFacilityOutputCapacityBranch> branches = facility
            .CraftDefinitions
            .Select(value => new ProductionFacilityOutputCapacityBranch(
                CombatCraftFacilityOutputBranchIdentity.Primary(
                    value.CraftDefinitionId),
                new[]
                {
                    new ProductionFacilityOutputMaximumMassRequest(
                        value.OutputLineId,
                        value.OutputItemId.Value,
                        value.OutputCapabilityId,
                        value.OutputQuantity)
                }))
            .ToList();
        foreach (CombatCraftDefinitionSnapshot craft in facility.CraftDefinitions
                     .Where(value => value.Kind
                         == CombatCraftOutputKind.UniqueEquipment))
        {
            foreach (CombatRejectedRecoveryProjection projection in
                     rejectedRecovery.CaptureDefinitionMaximums(
                         craft.CraftDefinitionId))
            {
                if (projection.Outputs.Count == 0)
                    continue;
                branches.Add(new ProductionFacilityOutputCapacityBranch(
                    CombatCraftFacilityOutputBranchIdentity.Recovery(
                        craft.CraftDefinitionId,
                        projection.MaterialId),
                    projection.Outputs.Select(output =>
                        new ProductionFacilityOutputMaximumMassRequest(
                            output.OutputLineId,
                            output.ItemId,
                            ProductionOutputCapabilityIds.StandardDefinition,
                            output.Quantity))
                        .ToArray()));
            }
        }
        return new ProductionFacilityOutputCapacityContribution(
            Id,
            Version,
            true,
            branches.OrderBy(value => value.BranchId, StringComparer.Ordinal)
                .ToArray());
    }
}
