using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

public enum WorkPriorityLevel
{
    Off = 0,
    Priority1 = 1,
    Priority2 = 2,
    Priority3 = 3
}

public static class WorkPriorityLevelExtensions
{
    public static WorkPriorityLevel Next(this WorkPriorityLevel priority)
    {
        return priority switch
        {
            WorkPriorityLevel.Priority1 => WorkPriorityLevel.Priority2,
            WorkPriorityLevel.Priority2 => WorkPriorityLevel.Priority3,
            WorkPriorityLevel.Priority3 => WorkPriorityLevel.Off,
            _ => WorkPriorityLevel.Priority1
        };
    }

    public static float GetBaseScore(this WorkPriorityLevel priority)
    {
        return priority switch
        {
            WorkPriorityLevel.Priority1 => 300f,
            WorkPriorityLevel.Priority2 => 200f,
            WorkPriorityLevel.Priority3 => 100f,
            _ => float.NegativeInfinity
        };
    }

    public static string ToDisplayText(this WorkPriorityLevel priority)
    {
        return priority switch
        {
            WorkPriorityLevel.Priority1 => "1",
            WorkPriorityLevel.Priority2 => "2",
            WorkPriorityLevel.Priority3 => "3",
            _ => "꺼짐"
        };
    }
}

public static class WorkTaskCatalog
{
    public static IReadOnlyList<WorkTypeDefinition> Definitions => WorkTypeCatalog.All;

    internal static string GetLegacyDisplayName(FacilityWorkType workType)
    {
        return WorkTypeCatalog.TryGet(workType, out WorkTypeDefinition definition)
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

public static class WorkCommandResolver
{
    public static bool TryResolveFacilityCommand(
        CharacterActor actor,
        BuildableObject target,
        out WorkTypeId workTypeId,
        out string failureReason)
    {
        workTypeId = default;
        if (!TryResolveLegacyFacilityCommand(
                actor,
                target,
                out FacilityWorkType workType,
                out failureReason))
        {
            return false;
        }

        if (!WorkTypeCatalog.TryGet(workType, out WorkTypeDefinition definition))
        {
            failureReason = "명령할 작업 종류를 해석할 수 없습니다.";
            return false;
        }

        workTypeId = definition.WorkTypeId;
        return true;
    }

    internal static bool TryResolveLegacyFacilityCommand(
        CharacterActor actor,
        BuildableObject target,
        out FacilityWorkType workType,
        out string failureReason)
    {
        workType = FacilityWorkType.None;
        failureReason = string.Empty;

        if (actor == null)
        {
            failureReason = "선택된 캐릭터가 없습니다";
            return false;
        }

        if (target == null)
        {
            failureReason = "우선 지정할 대상이 없습니다";
            return false;
        }

        if (target is ConstructionSite constructionSite)
        {
            if (!constructionSite.CanAssignWorker(actor, out failureReason))
            {
                return false;
            }

            workType = FacilityWorkType.Construct;
            return true;
        }

        if (target.isDestroy || target.Facility == null)
        {
            failureReason = "작업 가능한 시설이 아닙니다";
            return false;
        }

        FacilityWorkType supportedTypes = WildlifeButcherFacilityUtility.AddFallbackWorkTypes(
            target,
            target.Facility.supportedWorkTypes);
        supportedTypes = SurvivalFacilityUtility.AddFallbackWorkTypes(target, supportedTypes);
        if (supportedTypes == FacilityWorkType.None)
        {
            failureReason = "지원하는 작업이 없습니다";
            return false;
        }

        if (target.IsDamaged && TryUse(target, supportedTypes, FacilityWorkType.Repair, out workType))
        {
            return true;
        }

        if (NeedsRestock(target) && TryUse(target, supportedTypes, FacilityWorkType.Restock, out workType))
        {
            return true;
        }

        if (TryUse(target, supportedTypes, FacilityWorkType.Research, out workType))
        {
            return true;
        }

        List<FacilityWorkType> assignableTypes = new List<FacilityWorkType>();
        foreach (WorkTypeDefinition definition in WorkTypeCatalog.Enumerate(supportedTypes))
        {
            if (TryUse(target, supportedTypes, definition.Type, out workType))
            {
                assignableTypes.Add(workType);
            }
        }

        if (assignableTypes.Count == 1)
        {
            workType = assignableTypes[0];
            return true;
        }

        if (assignableTypes.Count > 1)
        {
            workType = FacilityWorkType.None;
            failureReason = "작업 종류를 명시해야 합니다";
            return false;
        }

        failureReason = "수행 가능한 작업이 없습니다";
        return false;
    }

    public static bool IsSuppressTarget(CharacterActor target, Predicate<CharacterActor> isRebellionTarget)
    {
        CharacterIdentity identity = target != null ? target.Identity : null;
        return target != null
            && ((identity != null && identity.CharacterType == CharacterType.Intruder)
                || (isRebellionTarget != null && isRebellionTarget(target))
                || (target.DeprivationRuntime?.IsSuppressible(target) ?? false));
    }

    public static bool TryResolveSuppressCommand(
        CharacterActor actor,
        CharacterActor target,
        Predicate<CharacterActor> isRebellionTarget,
        out string failureReason)
    {
        failureReason = string.Empty;

        if (actor == null)
        {
            failureReason = "선택된 캐릭터가 없습니다";
            return false;
        }

        if (target == null)
        {
            failureReason = "제압할 대상이 없습니다";
            return false;
        }

        if (actor == target)
        {
            failureReason = "자기 자신은 제압할 수 없습니다";
            return false;
        }

        if (actor.IsDead)
        {
            failureReason = "선택된 캐릭터가 행동할 수 없습니다";
            return false;
        }

        if (target.IsDead)
        {
            failureReason = "이미 제압된 대상입니다";
            return false;
        }

        if (!IsSuppressTarget(target, isRebellionTarget))
        {
            failureReason = "제압 대상이 아닙니다";
            return false;
        }

        return true;
    }

    private static bool TryUse(
        BuildableObject target,
        FacilityWorkType supportedTypes,
        FacilityWorkType candidate,
        out FacilityWorkType result)
    {
        result = FacilityWorkType.None;
        if ((supportedTypes & candidate) == 0)
        {
            return false;
        }

        if (!WorkTypeCatalog.TryGet(candidate, out WorkTypeDefinition definition)
            || !target.CanAssignWork(definition.WorkTypeId, out _))
        {
            return false;
        }

        result = candidate;
        return true;
    }

    private static bool NeedsRestock(BuildableObject target)
    {
        if (target == null || target.Facility == null || target is not IStockedFacility stocked)
        {
            return false;
        }

        if (target.BuildingData.RequiresStockForUse() && !stocked.HasAvailableStock)
        {
            return true;
        }

        return target.GetInternalStockCapacity() > 0
            && stocked.CurrentStock <= target.GetRestockRequestThreshold();
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
                    GetLegacyPriority(definition.Type, definition.DefaultPriority)));
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
                        GetLegacyPriority(definition.Type, definition.DefaultPriority)));
                }
            }
        }

        schemaVersion = CurrentSchemaVersion;
    }

    private WorkPriorityEntry FindEntry(WorkTypeId workTypeId)
    {
        return priorities.FirstOrDefault((entry) => entry != null
            && string.Equals(
                entry.WorkTypeId,
                workTypeId.Value,
                StringComparison.Ordinal));
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
