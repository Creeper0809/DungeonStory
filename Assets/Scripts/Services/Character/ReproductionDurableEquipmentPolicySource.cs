using System;
using System.Collections.Generic;

/// <summary>
/// Reproduction-owned registration data for the breeding ledger. Exact
/// delivery, positive gram capacity, persistence and terminal recovery stay in
/// the common durable facility-equipment runtime.
/// </summary>
public sealed class ReproductionDurableEquipmentPolicySource :
    IDurableFacilityEquipmentPolicySource
{
    public const string PolicyId = "policy:character.reproduction-ledger";
    public const string RequirementId = "breeding-ledger";
    public const string LogicalOwnerDomain = "character.reproduction";
    public const string StableSourceId =
        "character.reproduction-ledger-equipment";

    private static readonly IReadOnlyList<DurableFacilityEquipmentPolicy>
        Policies = Array.AsReadOnly(new[]
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
                        (ItemDefinitionId)DurableToolItemRules.BreedingLedger,
                        requiredQuantity: 1)
                })
        });

    public string SourceId => StableSourceId;
    public long Revision => 1L;

    public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
        Policies;
}
