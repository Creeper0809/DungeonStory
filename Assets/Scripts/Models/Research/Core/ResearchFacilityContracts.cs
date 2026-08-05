using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ResearchFacilityCapabilityId
{
    Basic = 0,
    Archive = 1,
    Specimen = 2,
    Design = 3,
    Reagent = 4,
    Arcane = 5,
    Advanced = 6
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public struct ResearchFacilityRequirement
{
    public ResearchFacilityCapabilityId capability;
    [Min(1)] public int requiredCount;

    public ResearchFacilityRequirement(
        ResearchFacilityCapabilityId capability,
        int requiredCount)
    {
        this.capability = capability;
        this.requiredCount = Mathf.Max(1, requiredCount);
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public struct ResearchFacilityContribution
{
    public ResearchFacilityCapabilityId capability;
    [Min(1)] public int capacity;

    public ResearchFacilityContribution(
        ResearchFacilityCapabilityId capability,
        int capacity)
    {
        this.capability = capability;
        this.capacity = Mathf.Max(1, capacity);
    }
}

public static class ResearchFacilityCapacityRules
{
    public static bool MeetsRequirements(
        System.Collections.Generic.IReadOnlyList<ResearchFacilityRequirement> requirements,
        Func<ResearchFacilityCapabilityId, int> getAvailable,
        out ResearchFacilityRequirement[] missing)
    {
        if (getAvailable == null)
        {
            throw new ArgumentNullException(nameof(getAvailable));
        }

        missing = (requirements ?? Array.Empty<ResearchFacilityRequirement>())
            .Where(requirement =>
                getAvailable(requirement.capability)
                < Mathf.Max(1, requirement.requiredCount))
            .OrderBy(requirement => requirement.capability)
            .ToArray();
        return missing.Length == 0;
    }
}
