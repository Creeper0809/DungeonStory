using System;
using System.Collections.Generic;

/// <summary>
/// Career-owned registration data for the mentorship ledger. Registration is
/// intentionally supplied through the common policy-source capability; the
/// Items runtime contains no career or item-ID branch.
/// </summary>
public sealed class CareerDurableEquipmentPolicySource :
    IDurableFacilityEquipmentPolicySource
{
    public const string PolicyId = "policy:character.career-ledger";
    public const string RequirementId = "career-ledger";
    public const string LogicalOwnerDomain = "character.career";
    public const string StableSourceId = "character.career-ledger-equipment";

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
                        (ItemDefinitionId)DurableToolItemRules.CareerLedger,
                        requiredQuantity: 1)
                })
        });

    public string SourceId => StableSourceId;
    public long Revision => 1L;

    public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
        Policies;
}
