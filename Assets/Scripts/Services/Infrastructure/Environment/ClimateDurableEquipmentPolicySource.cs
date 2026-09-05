using System;
using System.Collections.Generic;

/// <summary>
/// Climate-owned registration data for the weather tower's two durable tools.
/// The common Items runtime owns exact delivery, capacity, persistence and wear.
/// </summary>
public sealed class ClimateDurableEquipmentPolicySource :
    IDurableFacilityEquipmentPolicySource
{
    public const string PolicyId = "policy:infrastructure.climate-observation";
    public const string AlmanacRequirementId = "seasonal-almanac";
    public const string ObservationKitRequirementId = "weather-observation-kit";
    public const string LogicalOwnerDomain = "infrastructure.climate";
    public const string StableSourceId = "infrastructure.climate-observation-equipment";

    private static readonly IReadOnlyList<DurableFacilityEquipmentPolicy>
        Policies = Array.AsReadOnly(new[]
        {
            new DurableFacilityEquipmentPolicy(
                PolicyId,
                revision: 1L,
                LogicalOwnerDomain,
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
                new[]
                {
                    new DurableFacilityEquipmentRequirement(
                        AlmanacRequirementId,
                        (ItemDefinitionId)DurableToolItemRules.SeasonalAlmanac,
                        requiredQuantity: 1),
                    new DurableFacilityEquipmentRequirement(
                        ObservationKitRequirementId,
                        (ItemDefinitionId)DurableToolItemRules.WeatherObservationKit,
                        requiredQuantity: 1)
                })
        });

    public string SourceId => StableSourceId;
    public long Revision => 1L;

    public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
        Policies;
}
