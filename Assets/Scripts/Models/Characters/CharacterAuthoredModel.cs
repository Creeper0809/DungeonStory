using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class CharacterModelModifiers
{
    public FacilityRole preferredFacilityRoles;
    public FacilityRole dislikedFacilityRoles;
    [SerializeField] internal FacilityWorkType preferredWorkTypes;
    [SerializeField] internal FacilityWorkType dislikedWorkTypes;

    public IEnumerable<WorkTypeId> PreferredWorkTypeIds =>
        EnumerateWorkTypeIds(preferredWorkTypes);
    public IEnumerable<WorkTypeId> DislikedWorkTypeIds =>
        EnumerateWorkTypeIds(dislikedWorkTypes);
    public FacilityWorkType PreferredLegacyWorkTypes => preferredWorkTypes;
    public FacilityWorkType DislikedLegacyWorkTypes => dislikedWorkTypes;

    public void SetWorkPreferences(
        FacilityWorkType preferred,
        FacilityWorkType disliked)
    {
        preferredWorkTypes = preferred;
        dislikedWorkTypes = disliked;
    }

    public void Multiply(CharacterModelModifiers other)
    {
        if (other == null)
        {
            return;
        }

        preferredFacilityRoles |= other.preferredFacilityRoles;
        dislikedFacilityRoles |= other.dislikedFacilityRoles;
        preferredWorkTypes |= other.preferredWorkTypes;
        dislikedWorkTypes |= other.dislikedWorkTypes;
    }

    private static IEnumerable<WorkTypeId> EnumerateWorkTypeIds(
        FacilityWorkType workTypes)
    {
        foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
        {
            if (TryGetLegacyType(
                    definition.WorkTypeId,
                    out FacilityWorkType legacyType)
                && (workTypes & legacyType) != 0)
            {
                yield return definition.WorkTypeId;
            }
        }
    }

    private static bool TryGetLegacyType(
        WorkTypeId workTypeId,
        out FacilityWorkType legacyType)
    {
        if (workTypeId == BuiltInWorkTypeIds.Operate) legacyType = FacilityWorkType.Operate;
        else if (workTypeId == BuiltInWorkTypeIds.Restock) legacyType = FacilityWorkType.Restock;
        else if (workTypeId == BuiltInWorkTypeIds.Construct) legacyType = FacilityWorkType.Construct;
        else if (workTypeId == BuiltInWorkTypeIds.Repair) legacyType = FacilityWorkType.Repair;
        else if (workTypeId == BuiltInWorkTypeIds.Clean) legacyType = FacilityWorkType.Clean;
        else if (workTypeId == BuiltInWorkTypeIds.Research) legacyType = FacilityWorkType.Research;
        else if (workTypeId == BuiltInWorkTypeIds.Guard) legacyType = FacilityWorkType.Guard;
        else if (workTypeId == BuiltInWorkTypeIds.Reception) legacyType = FacilityWorkType.Reception;
        else if (workTypeId == BuiltInWorkTypeIds.Rescue) legacyType = FacilityWorkType.Rescue;
        else if (workTypeId == BuiltInWorkTypeIds.Rest) legacyType = FacilityWorkType.Rest;
        else if (workTypeId == BuiltInWorkTypeIds.Craft) legacyType = FacilityWorkType.Craft;
        else if (workTypeId == BuiltInWorkTypeIds.Haul) legacyType = FacilityWorkType.Haul;
        else if (workTypeId == BuiltInWorkTypeIds.Hunt) legacyType = FacilityWorkType.Hunt;
        else if (workTypeId == BuiltInWorkTypeIds.Butcher) legacyType = FacilityWorkType.Butcher;
        else if (workTypeId == BuiltInWorkTypeIds.DrawWater) legacyType = FacilityWorkType.DrawWater;
        else if (workTypeId == BuiltInWorkTypeIds.Cook) legacyType = FacilityWorkType.Cook;
        else if (workTypeId == BuiltInWorkTypeIds.Treat) legacyType = FacilityWorkType.Treat;
        else if (workTypeId == BuiltInWorkTypeIds.Surgery) legacyType = FacilityWorkType.Surgery;
        else if (workTypeId == BuiltInWorkTypeIds.Refuel) legacyType = FacilityWorkType.Refuel;
        else if (workTypeId == BuiltInWorkTypeIds.Warden) legacyType = FacilityWorkType.Warden;
        else if (workTypeId == BuiltInWorkTypeIds.Perform) legacyType = FacilityWorkType.Perform;
        else if (workTypeId == BuiltInWorkTypeIds.Gather) legacyType = FacilityWorkType.Gather;
        else if (workTypeId == BuiltInWorkTypeIds.Sow) legacyType = FacilityWorkType.Sow;
        else if (workTypeId == BuiltInWorkTypeIds.Harvest) legacyType = FacilityWorkType.Harvest;
        else if (workTypeId == BuiltInWorkTypeIds.Logging) legacyType = FacilityWorkType.Logging;
        else if (workTypeId == BuiltInWorkTypeIds.Quarry) legacyType = FacilityWorkType.Quarry;
        else if (workTypeId == BuiltInWorkTypeIds.AnimalCare) legacyType = FacilityWorkType.AnimalCare;
        else if (workTypeId == BuiltInWorkTypeIds.GrandProject) legacyType = FacilityWorkType.GrandProject;
        else if (workTypeId == BuiltInWorkTypeIds.ThreatMitigation) legacyType = FacilityWorkType.ThreatMitigation;
        else if (workTypeId == BuiltInWorkTypeIds.Plumbing) legacyType = FacilityWorkType.Plumbing;
        else
        {
            legacyType = FacilityWorkType.None;
            return false;
        }

        return true;
    }
}
