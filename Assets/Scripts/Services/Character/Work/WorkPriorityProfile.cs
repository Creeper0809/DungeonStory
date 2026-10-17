using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public static class WorkTaskCatalog
{
    public static IReadOnlyList<WorkTypeDefinition> Definitions => WorkTypeCatalog.All;

    internal static string GetLegacyDisplayName(FacilityWorkType workType)
    {
        return FacilityWorkTypeMap.TryGet(workType, out WorkTypeDefinition definition)
            ? definition.DisplayName
            : workType.ToString();
    }

    public static string GetDisplayName(WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? definition.DisplayName
            : workTypeId.ToString();
    }

}

[Serializable]
public sealed class WorkPriorityEntry
{
    [SerializeField] private string workTypeId;
    [SerializeField] private WorkPriorityLevel priority;

    public WorkPriorityEntry(string workTypeId, WorkPriorityLevel priority)
        : this(new WorkTypeId(workTypeId), priority)
    {
    }

    public WorkPriorityEntry(WorkTypeId workTypeId, WorkPriorityLevel priority)
    {
        if (!workTypeId.IsValid)
        {
            throw new ArgumentException(
                "Work priority entry requires a valid work type id.",
                nameof(workTypeId));
        }

        this.workTypeId = workTypeId.Value;
        this.priority = priority;
    }

    public string WorkTypeId => workTypeId;
    public WorkTypeId TypedWorkTypeId => new WorkTypeId(workTypeId);
    public WorkPriorityLevel Priority => priority;

    internal void SetPriority(WorkPriorityLevel value)
    {
        priority = value;
    }
}

[Serializable]
public class WorkPriorityProfile : ISerializationCallbackReceiver
{
    private const int CurrentSchemaVersion = 2;

    [SerializeField] private int schemaVersion;
    [SerializeField] private List<WorkPriorityEntry> priorities = new List<WorkPriorityEntry>();
    [NonSerialized] private IReadOnlyList<WorkPriorityEntry> prioritiesView;

    [SerializeField, HideInInspector, FormerlySerializedAs("operate")]
    private WorkPriorityLevel legacyOperate = WorkPriorityLevel.Priority1;
    [SerializeField, HideInInspector, FormerlySerializedAs("restock")]
    private WorkPriorityLevel legacyRestock = WorkPriorityLevel.Priority2;
    [SerializeField, HideInInspector, FormerlySerializedAs("repair")]
    private WorkPriorityLevel legacyRepair = WorkPriorityLevel.Priority2;
    [SerializeField, HideInInspector, FormerlySerializedAs("clean")]
    private WorkPriorityLevel legacyClean = WorkPriorityLevel.Priority3;
    [SerializeField, HideInInspector, FormerlySerializedAs("research")]
    private WorkPriorityLevel legacyResearch = WorkPriorityLevel.Priority2;
    [SerializeField, HideInInspector, FormerlySerializedAs("guard")]
    private WorkPriorityLevel legacyGuard = WorkPriorityLevel.Priority3;
    [SerializeField, HideInInspector, FormerlySerializedAs("rescue")]
    private WorkPriorityLevel legacyRescue = WorkPriorityLevel.Priority2;
    [SerializeField, HideInInspector, FormerlySerializedAs("rest")]
    private WorkPriorityLevel legacyRest = WorkPriorityLevel.Priority3;

    public IReadOnlyList<WorkPriorityEntry> Entries
    {
        get
        {
            EnsureMigrated();
            return prioritiesView ??= ReadOnlyView.List(priorities);
        }
    }

    public static WorkPriorityProfile CreateDefault()
    {
        WorkPriorityProfile profile = new WorkPriorityProfile();
        profile.EnsureMigrated();
        return profile;
    }

    public WorkPriorityProfile Clone()
    {
        EnsureMigrated();
        return new WorkPriorityProfile
        {
            schemaVersion = CurrentSchemaVersion,
            priorities = priorities
                .Where((entry) => entry != null)
                .Select((entry) => new WorkPriorityEntry(
                    entry.TypedWorkTypeId,
                    entry.Priority))
                .ToList()
        };
    }

    public WorkPriorityLevel GetPriority(WorkTypeId workTypeId)
    {
        EnsureMigrated();
        if (WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition))
        {
            WorkPriorityEntry entry = FindEntry(definition.WorkTypeId);
            return entry != null ? entry.Priority : definition.DefaultPriority;
        }

        return WorkPriorityLevel.Off;
    }

    public void SetPriority(WorkTypeId workTypeId, WorkPriorityLevel priority)
    {
        EnsureMigrated();
        WorkTypeDefinition definition = WorkTypeCatalog.TryGet(
                workTypeId,
                out WorkTypeDefinition resolved)
            ? resolved
            : throw new KeyNotFoundException(
                $"No work type definition is registered for '{workTypeId}'.");
        WorkPriorityEntry entry = FindEntry(definition.WorkTypeId);
        if (entry != null)
        {
            entry.SetPriority(priority);
            return;
        }

        priorities.Add(new WorkPriorityEntry(definition.WorkTypeId, priority));
    }

    public bool IsEnabled(WorkTypeId workTypeId)
    {
        return GetPriority(workTypeId) != WorkPriorityLevel.Off;
    }

    public WorkPriorityLevel GetBestPriority(params WorkTypeId[] workTypeIds)
    {
        EnsureMigrated();
        if (workTypeIds == null || workTypeIds.Length == 0)
        {
            return WorkPriorityLevel.Off;
        }

        WorkPriorityLevel best = WorkPriorityLevel.Off;
        foreach (WorkTypeId workTypeId in workTypeIds)
        {
            WorkPriorityLevel current = GetPriority(workTypeId);
            if (current == WorkPriorityLevel.Off)
            {
                continue;
            }

            if (best == WorkPriorityLevel.Off || current < best)
            {
                best = current;
            }
        }

        return best;
    }

    public void ApplyPreferredTypes(params WorkTypeId[] preferredTypes)
    {
        if (preferredTypes == null)
        {
            return;
        }

        foreach (WorkTypeId workTypeId in preferredTypes)
        {
            WorkPriorityLevel current = GetPriority(workTypeId);
            if (current == WorkPriorityLevel.Off || current > WorkPriorityLevel.Priority1)
            {
                SetPriority(workTypeId, WorkPriorityLevel.Priority1);
            }
        }
    }

    public void OnBeforeSerialize()
    {
        EnsureMigrated();
    }

    public void OnAfterDeserialize()
    {
        priorities ??= new List<WorkPriorityEntry>();
        prioritiesView = null;
    }

    private void EnsureMigrated()
    {
        priorities ??= new List<WorkPriorityEntry>();
        if (schemaVersion >= CurrentSchemaVersion)
        {
            return;
        }

        if (priorities.Count == 0)
        {
            foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
            {
                priorities.Add(new WorkPriorityEntry(
                    definition.WorkTypeId,
                    GetLegacyPriority(
                        FacilityWorkTypeMap.GetRequired(definition),
                        definition.DefaultPriority)));
            }
        }
        else
        {
            foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
            {
                if (FindEntry(definition.WorkTypeId) == null)
                {
                    priorities.Add(new WorkPriorityEntry(
                        definition.WorkTypeId,
                        GetLegacyPriority(
                            FacilityWorkTypeMap.GetRequired(definition),
                            definition.DefaultPriority)));
                }
            }
        }

        schemaVersion = CurrentSchemaVersion;
    }

    private WorkPriorityEntry FindEntry(WorkTypeId workTypeId)
    {
        string requestedId = workTypeId.Value;
        for (int index = 0; index < priorities.Count; index++)
        {
            WorkPriorityEntry entry = priorities[index];
            if (entry != null
                && string.Equals(
                    entry.WorkTypeId,
                    requestedId,
                    StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    private WorkPriorityLevel GetLegacyPriority(
        FacilityWorkType workType,
        WorkPriorityLevel fallback)
    {
        return workType switch
        {
            FacilityWorkType.Operate => legacyOperate,
            FacilityWorkType.Restock => legacyRestock,
            FacilityWorkType.Repair => legacyRepair,
            FacilityWorkType.Clean => legacyClean,
            FacilityWorkType.Research => legacyResearch,
            FacilityWorkType.Guard => legacyGuard,
            FacilityWorkType.Rescue => legacyRescue,
            FacilityWorkType.Rest => legacyRest,
            _ => fallback
        };
    }
}
