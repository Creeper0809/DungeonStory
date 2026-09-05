using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// Research-owned registration data for the existing Arcane Index socket.
/// The common Items runtime consumes only the policy contract and contains no
/// Research or item-definition branch.
/// </summary>
public sealed class ResearchArcaneIndexEquipmentPolicySource :
    IDurableFacilityEquipmentPolicySource,
    IResearchDurableEquipmentWorkPolicySource
{
    public const string PolicyId = "policy:research.arcane-index";
    public const string RequirementId = "arcane-index";
    public const string LogicalOwnerDomain = "research.arcane-index";
    public const string StableSourceId = "research.arcane-index-equipment";

    private static readonly IReadOnlyList<DurableFacilityEquipmentPolicy>
        Policies = System.Array.AsReadOnly(new[]
        {
            new DurableFacilityEquipmentPolicy(
                PolicyId,
                revision: 1L,
                LogicalOwnerDomain,
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                DurableFacilityEquipmentPolicyKinds
                    .PositiveDurabilityComponent,
                new[]
                {
                    new DurableFacilityEquipmentRequirement(
                        RequirementId,
                        (ItemDefinitionId)DurableToolItemRules.ArcaneIndex,
                        requiredQuantity: 1)
                })
        });

    public string SourceId => StableSourceId;
    public long Revision => 1L;
    public IReadOnlyList<string> EquipmentPolicyIds { get; } =
        Array.AsReadOnly(new[] { PolicyId });

    public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
        Policies;

    public bool TryResolve(
        BuildableObject facility,
        out ResearchDurableEquipmentWorkPolicy policy)
    {
        if (facility == null
            || !facility.SupportsWork(BuiltInWorkTypeIds.Research))
        {
            policy = null;
            return false;
        }
        policy = new ResearchDurableEquipmentWorkPolicy(
            PolicyId,
            RequirementId,
            DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
            "research-approved-work-multiplier",
            effectMultiplier: 1.1d,
            wearPerApprovedWorkUnit: 0.01d);
        return true;
    }
}

public sealed class ResearchDurableEquipmentWorkPolicy
{
    public ResearchDurableEquipmentWorkPolicy(
        string equipmentPolicyId,
        string requirementId,
        string wearPolicyKind,
        string effectKind,
        double effectMultiplier,
        double wearPerApprovedWorkUnit)
    {
        if (!Canonical(equipmentPolicyId)
            || !Canonical(requirementId)
            || !Canonical(wearPolicyKind)
            || !Canonical(effectKind)
            || double.IsNaN(effectMultiplier)
            || double.IsInfinity(effectMultiplier)
            || effectMultiplier < 1d
            || double.IsNaN(wearPerApprovedWorkUnit)
            || double.IsInfinity(wearPerApprovedWorkUnit)
            || wearPerApprovedWorkUnit <= 0d)
        {
            throw new ArgumentException(
                "Research durable-equipment work policy is invalid.");
        }
        EquipmentPolicyId = equipmentPolicyId;
        RequirementId = requirementId;
        WearPolicyKind = wearPolicyKind;
        EffectKind = effectKind;
        EffectMultiplier = effectMultiplier;
        WearPerApprovedWorkUnit = wearPerApprovedWorkUnit;
    }

    public string EquipmentPolicyId { get; }
    public string RequirementId { get; }
    public string WearPolicyKind { get; }
    public string EffectKind { get; }
    public double EffectMultiplier { get; }
    public double WearPerApprovedWorkUnit { get; }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

public interface IResearchDurableEquipmentWorkPolicySource
{
    string SourceId { get; }
    long Revision { get; }
    IReadOnlyList<string> EquipmentPolicyIds { get; }

    bool TryResolve(
        BuildableObject facility,
        out ResearchDurableEquipmentWorkPolicy policy);
}

public interface IResearchDurableEquipmentWorkPolicyQuery
{
    bool TryResolve(
        BuildableObject facility,
        out ResearchDurableEquipmentWorkPolicy policy,
        out string failureReason);

    bool IsRegisteredEquipmentPolicy(string policyId);
}

public sealed class ResearchDurableEquipmentWorkPolicyRegistry :
    IResearchDurableEquipmentWorkPolicyQuery
{
    private readonly IReadOnlyList<IResearchDurableEquipmentWorkPolicySource>
        sources;
    private readonly HashSet<string> equipmentPolicyIds;

    public ResearchDurableEquipmentWorkPolicyRegistry(
        IEnumerable<IResearchDurableEquipmentWorkPolicySource> sources)
    {
        IResearchDurableEquipmentWorkPolicySource[] ordered = (sources
                ?? throw new ArgumentNullException(nameof(sources)))
            .OrderBy(value => value?.SourceId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null
                || !Canonical(value.SourceId)
                || value.Revision <= 0L
                || value.EquipmentPolicyIds == null
                || value.EquipmentPolicyIds.Count == 0
                || value.EquipmentPolicyIds.Any(id => !Canonical(id)))
            || ordered.Select(value => value.SourceId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Research durable-equipment work policy sources are missing, duplicate, or invalid.");
        }
        this.sources = Array.AsReadOnly(ordered);
        string[] policyIds = ordered
            .SelectMany(value => value.EquipmentPolicyIds)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (policyIds.Distinct(StringComparer.Ordinal).Count()
            != policyIds.Length)
        {
            throw new InvalidOperationException(
                "Research durable-equipment policy ID is owned by more than one source.");
        }
        equipmentPolicyIds = new HashSet<string>(
            policyIds,
            StringComparer.Ordinal);
    }

    public bool TryResolve(
        BuildableObject facility,
        out ResearchDurableEquipmentWorkPolicy policy,
        out string failureReason)
    {
        policy = null;
        failureReason = string.Empty;
        if (facility == null)
        {
            failureReason = "research-durable-equipment-facility-missing";
            return false;
        }
        List<ResearchDurableEquipmentWorkPolicy> matches = new();
        foreach (IResearchDurableEquipmentWorkPolicySource source in sources)
        {
            if (!source.TryResolve(facility, out ResearchDurableEquipmentWorkPolicy value))
                continue;
            if (value == null)
            {
                failureReason =
                    "research-durable-equipment-source-returned-null:"
                    + source.SourceId;
                return false;
            }
            matches.Add(value);
        }
        if (matches.Count == 0)
        {
            failureReason = "research-durable-equipment-policy-not-applicable";
            return false;
        }
        if (matches.Count != 1)
        {
            failureReason = "research-durable-equipment-policy-ambiguous";
            return false;
        }
        policy = matches[0];
        return true;
    }

    public bool IsRegisteredEquipmentPolicy(string policyId) =>
        equipmentPolicyIds.Contains(policyId ?? string.Empty);

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
