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
    [ContentReference(ContentReferenceKind.WorkType)]
    [SerializeField] private string workTypeId;
    [SerializeField] private WorkPriorityLevel priority;

    public WorkPriorityEntry(string workTypeId, WorkPriorityLevel priority)
    {
        WorkTypeDefinition definition = WorkTypeCatalog.RequireRegistered(
            workTypeId,
            nameof(WorkPriorityEntry));
        this.workTypeId = definition.Id;
        this.priority = priority;
    }

    public WorkPriorityEntry(WorkTypeId workTypeId, WorkPriorityLevel priority)
    {
        WorkTypeDefinition definition = WorkTypeCatalog.RequireRegistered(
            workTypeId,
            nameof(WorkPriorityEntry));
        this.workTypeId = definition.Id;
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
    [NonSerialized] private List<WorkPriorityEntry> validatedPriorities;

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
        WorkTypeDefinition definition = WorkTypeCatalog.RequireRegistered(
            workTypeId,
            "Work priority query");
        EnsureMigrated();
        WorkPriorityEntry entry = FindEntry(definition.WorkTypeId);
        return entry != null ? entry.Priority : definition.DefaultPriority;
    }

    public void SetPriority(WorkTypeId workTypeId, WorkPriorityLevel priority)
    {
        WorkTypeDefinition definition = WorkTypeCatalog.RequireRegistered(
            workTypeId,
            "Work priority update");
        EnsureMigrated();
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
        InvalidateEntryValidation();
        EnsureMigrated();
    }

    public void OnAfterDeserialize()
    {
        priorities ??= new List<WorkPriorityEntry>();
        prioritiesView = null;
        InvalidateEntryValidation();
    }

    private void EnsureMigrated()
    {
        List<WorkPriorityEntry> source = priorities
            ?? new List<WorkPriorityEntry>();
        ValidateCurrentEntriesOrThrow(source);
        if (schemaVersion >= CurrentSchemaVersion)
        {
            priorities = source;
            return;
        }

        try
        {
            List<WorkPriorityEntry> migrated = source
                .Select(entry => new WorkPriorityEntry(
                    entry.TypedWorkTypeId,
                    entry.Priority))
                .ToList();
            var authoredIds = new HashSet<string>(
                migrated.Select(entry => entry.WorkTypeId),
                StringComparer.Ordinal);
            foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
            {
                if (authoredIds.Add(definition.Id))
                {
                    migrated.Add(new WorkPriorityEntry(
                        definition.WorkTypeId,
                        GetLegacyPriority(
                            FacilityWorkTypeMap.GetRequired(definition),
                            definition.DefaultPriority)));
                }
            }

            ValidateEntriesOrThrow(migrated);
            priorities = migrated;
            schemaVersion = CurrentSchemaVersion;
            prioritiesView = null;
            validatedPriorities = migrated;
        }
        catch
        {
            InvalidateEntryValidation();
            throw;
        }
    }

    private void ValidateCurrentEntriesOrThrow(
        List<WorkPriorityEntry> entries)
    {
        if (ReferenceEquals(validatedPriorities, entries))
        {
            return;
        }

        try
        {
            ValidateEntriesOrThrow(entries);
            validatedPriorities = entries;
        }
        catch
        {
            InvalidateEntryValidation();
            throw;
        }
    }

    private void InvalidateEntryValidation() => validatedPriorities = null;

    private static void ValidateEntriesOrThrow(
        IReadOnlyList<WorkPriorityEntry> entries)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < entries.Count; index++)
        {
            WorkPriorityEntry entry = entries[index];
            if (entry == null)
            {
                throw new InvalidOperationException(
                    $"Work priority profile contains a null row at index {index}.");
            }

            WorkTypeDefinition definition = WorkTypeCatalog.RequireRegistered(
                entry.WorkTypeId,
                $"Work priority row {index}");
            if (!ids.Add(definition.Id))
            {
                throw new InvalidOperationException(
                    $"Work priority profile contains duplicate work type ID "
                    + $"'{definition.Id}'.");
            }
        }
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
