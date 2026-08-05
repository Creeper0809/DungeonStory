using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RuntimeWorkCapabilityMarker : MonoBehaviour
{
    private readonly Dictionary<string, HashSet<WorkTypeId>> workTypesBySource =
        new Dictionary<string, HashSet<WorkTypeId>>(StringComparer.Ordinal);

    public bool HasAny => workTypesBySource.Count > 0;

    public void Add(string sourceId, WorkTypeId workTypeId)
    {
        string source = NormalizeSource(sourceId);
        if (!workTypeId.IsValid)
        {
            throw new ArgumentException(
                "A runtime work capability requires a valid work type.",
                nameof(workTypeId));
        }

        if (!workTypesBySource.TryGetValue(source, out HashSet<WorkTypeId> workTypes))
        {
            workTypes = new HashSet<WorkTypeId>();
            workTypesBySource.Add(source, workTypes);
        }

        workTypes.Add(workTypeId);
    }

    public void RemoveSource(string sourceId)
    {
        workTypesBySource.Remove(NormalizeSource(sourceId));
    }

    public bool Supports(WorkTypeId workTypeId)
    {
        if (!workTypeId.IsValid)
        {
            return false;
        }

        foreach (HashSet<WorkTypeId> workTypes in workTypesBySource.Values)
        {
            if (workTypes.Contains(workTypeId))
            {
                return true;
            }
        }

        return false;
    }

    public FacilityWorkType AddSupportedTypes(FacilityWorkType current)
    {
        foreach (HashSet<WorkTypeId> workTypes in workTypesBySource.Values)
        {
            foreach (WorkTypeId workTypeId in workTypes)
            {
                if (WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition))
                {
                    current |= FacilityWorkTypeMap.GetRequired(definition);
                }
            }
        }

        return current;
    }

    private static string NormalizeSource(string sourceId)
    {
        return string.IsNullOrWhiteSpace(sourceId)
            ? throw new ArgumentException(
                "A runtime work capability requires a source id.",
                nameof(sourceId))
            : sourceId.Trim();
    }
}

public static class RuntimeWorkCapabilityUtility
{
    public static FacilityWorkType AddFallbackWorkTypes(
        Component building,
        FacilityWorkType current)
    {
        RuntimeWorkCapabilityMarker marker = building != null
            ? building.GetComponent<RuntimeWorkCapabilityMarker>()
            : null;
        return marker != null ? marker.AddSupportedTypes(current) : current;
    }

    public static bool Supports(Component building, WorkTypeId workTypeId)
    {
        RuntimeWorkCapabilityMarker marker = building != null
            ? building.GetComponent<RuntimeWorkCapabilityMarker>()
            : null;
        return marker != null && marker.Supports(workTypeId);
    }
}
