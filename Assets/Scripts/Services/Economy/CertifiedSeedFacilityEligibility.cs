using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Single authored eligibility authority shared by certified-seed commands,
/// presentation, and output-capacity contributors. Numeric building IDs are
/// deliberately excluded so future facilities opt in through capabilities.
/// </summary>
public static class CertifiedSeedFacilityEligibility
{
    public const string WorkstationTag =
        "workstation:v19:cultivar-breeding";

    public static bool IsEligible(BuildingSO definition)
    {
        if (definition == null)
            return false;
        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        return workstation != null
            && string.Equals(
                workstation.WorkstationTag,
                WorkstationTag,
                StringComparison.Ordinal)
            && buffer != null
            && buffer.physicalOutputBufferCycleCapacity is >= 2 and <= 4;
    }

    public static bool IsEligible(BuildableObject facility) =>
        facility != null
        && !facility.IsBuildingDestroyed
        && IsEligible(facility.BuildingData);

    public static bool IsEligible(ProductionFacilityCapacitySubject subject) =>
        string.Equals(
            subject.WorkstationTag,
            WorkstationTag,
            StringComparison.Ordinal)
        && subject.OutputBufferCycleCapacity is >= 2 and <= 4;

    public static IReadOnlyList<BuildableObject> FindOperational(
        IFacilityCapabilityQuery facilities)
    {
        if (facilities == null)
            throw new ArgumentNullException(nameof(facilities));
        return facilities.FindOperational(FacilityCapabilityKind.None)
            .Where(IsEligible)
            .OrderBy(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
    }
}
