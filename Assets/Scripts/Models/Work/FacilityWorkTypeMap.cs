using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Explicit compatibility boundary between the authored work ID protocol and the
/// legacy facility bit mask. New domain code should use WorkTypeId directly.
/// </summary>
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class FacilityWorkTypeMap
{
    private readonly struct Entry
    {
        public Entry(FacilityWorkType legacyType, WorkTypeId workTypeId)
        {
            LegacyType = legacyType;
            WorkTypeId = workTypeId;
        }

        public FacilityWorkType LegacyType { get; }
        public WorkTypeId WorkTypeId { get; }
    }

    private static readonly Entry[] Entries =
    {
        Map(FacilityWorkType.Operate, BuiltInWorkTypeIds.Operate),
        Map(FacilityWorkType.Restock, BuiltInWorkTypeIds.Restock),
        Map(FacilityWorkType.Construct, BuiltInWorkTypeIds.Construct),
        Map(FacilityWorkType.Repair, BuiltInWorkTypeIds.Repair),
        Map(FacilityWorkType.Clean, BuiltInWorkTypeIds.Clean),
        Map(FacilityWorkType.Research, BuiltInWorkTypeIds.Research),
        Map(FacilityWorkType.Guard, BuiltInWorkTypeIds.Guard),
        Map(FacilityWorkType.Reception, BuiltInWorkTypeIds.Reception),
        Map(FacilityWorkType.Rescue, BuiltInWorkTypeIds.Rescue),
        Map(FacilityWorkType.Rest, BuiltInWorkTypeIds.Rest),
        Map(FacilityWorkType.Craft, BuiltInWorkTypeIds.Craft),
        Map(FacilityWorkType.Haul, BuiltInWorkTypeIds.Haul),
        Map(FacilityWorkType.Hunt, BuiltInWorkTypeIds.Hunt),
        Map(FacilityWorkType.Butcher, BuiltInWorkTypeIds.Butcher),
        Map(FacilityWorkType.DrawWater, BuiltInWorkTypeIds.DrawWater),
        Map(FacilityWorkType.Cook, BuiltInWorkTypeIds.Cook),
        Map(FacilityWorkType.Treat, BuiltInWorkTypeIds.Treat),
        Map(FacilityWorkType.Surgery, BuiltInWorkTypeIds.Surgery),
        Map(FacilityWorkType.Refuel, BuiltInWorkTypeIds.Refuel),
        Map(FacilityWorkType.Warden, BuiltInWorkTypeIds.Warden),
        Map(FacilityWorkType.Perform, BuiltInWorkTypeIds.Perform),
        Map(FacilityWorkType.Gather, BuiltInWorkTypeIds.Gather),
        Map(FacilityWorkType.Sow, BuiltInWorkTypeIds.Sow),
        Map(FacilityWorkType.Harvest, BuiltInWorkTypeIds.Harvest),
        Map(FacilityWorkType.Logging, BuiltInWorkTypeIds.Logging),
        Map(FacilityWorkType.Quarry, BuiltInWorkTypeIds.Quarry),
        Map(FacilityWorkType.AnimalCare, BuiltInWorkTypeIds.AnimalCare),
        Map(FacilityWorkType.GrandProject, BuiltInWorkTypeIds.GrandProject),
        Map(FacilityWorkType.ThreatMitigation, BuiltInWorkTypeIds.ThreatMitigation),
        Map(FacilityWorkType.Plumbing, BuiltInWorkTypeIds.Plumbing)
    };

    public static bool TryGet(
        FacilityWorkType legacyType,
        out WorkTypeDefinition definition)
    {
        if (TryGetWorkTypeId(legacyType, out WorkTypeId workTypeId))
        {
            return WorkTypeCatalog.TryGet(workTypeId, out definition);
        }

        definition = null;
        return false;
    }

    public static bool TryGetWorkTypeId(
        FacilityWorkType legacyType,
        out WorkTypeId workTypeId)
    {
        for (int index = 0; index < Entries.Length; index++)
        {
            if (Entries[index].LegacyType == legacyType)
            {
                workTypeId = Entries[index].WorkTypeId;
                return true;
            }
        }

        workTypeId = default;
        return false;
    }

    public static bool TryGetLegacyType(
        WorkTypeId workTypeId,
        out FacilityWorkType legacyType)
    {
        if (workTypeId.IsValid)
        {
            for (int index = 0; index < Entries.Length; index++)
            {
                if (Entries[index].WorkTypeId == workTypeId)
                {
                    legacyType = Entries[index].LegacyType;
                    return true;
                }
            }
        }

        legacyType = FacilityWorkType.None;
        return false;
    }

    public static FacilityWorkType GetRequired(WorkTypeDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        return GetRequired(definition.WorkTypeId);
    }

    public static FacilityWorkType GetRequired(WorkTypeId workTypeId)
    {
        if (TryGetLegacyType(workTypeId, out FacilityWorkType legacyType))
        {
            return legacyType;
        }

        throw new KeyNotFoundException(
            $"No legacy facility work type maps to '{workTypeId}'.");
    }

    public static IEnumerable<WorkTypeDefinition> Enumerate(
        FacilityWorkType workTypes)
    {
        for (int index = 0; index < Entries.Length; index++)
        {
            Entry entry = Entries[index];
            if ((workTypes & entry.LegacyType) != 0
                && WorkTypeCatalog.TryGet(
                    entry.WorkTypeId,
                    out WorkTypeDefinition definition))
            {
                yield return definition;
            }
        }
    }

    private static Entry Map(
        FacilityWorkType legacyType,
        WorkTypeId workTypeId)
    {
        return new Entry(legacyType, workTypeId);
    }
}
