using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class WorkTypeDefinition
{
    internal WorkTypeDefinition(
        string id,
        FacilityWorkType type,
        string displayName,
        int sortOrder,
        WorkPriorityLevel defaultPriority,
        string capabilityId)
        : this(
            new WorkTypeId(id),
            type,
            displayName,
            sortOrder,
            defaultPriority,
            capabilityId)
    {
    }

    internal WorkTypeDefinition(
        WorkTypeId id,
        FacilityWorkType type,
        string displayName,
        int sortOrder,
        WorkPriorityLevel defaultPriority,
        string capabilityId)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException("Work type id is required.", nameof(id));
        }

        if (type == FacilityWorkType.None || !IsSingleBit(type))
        {
            throw new ArgumentException("A work type must use one non-zero flag bit.", nameof(type));
        }

        WorkTypeId = id;
        Type = type;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        SortOrder = sortOrder;
        DefaultPriority = defaultPriority;
        CapabilityId = capabilityId?.Trim() ?? string.Empty;
    }

    public WorkTypeId WorkTypeId { get; }
    public string Id => WorkTypeId.Value;
    internal FacilityWorkType Type { get; }
    public string DisplayName { get; }
    public int SortOrder { get; }
    public WorkPriorityLevel DefaultPriority { get; }
    public string CapabilityId { get; }

    private static bool IsSingleBit(FacilityWorkType type)
    {
        int value = (int)type;
        return (value & (value - 1)) == 0;
    }
}

public static class WorkTypeCatalog
{
    private static readonly Dictionary<string, WorkTypeDefinition> ById =
        new Dictionary<string, WorkTypeDefinition>(StringComparer.Ordinal);
    private static readonly Dictionary<FacilityWorkType, WorkTypeDefinition> ByType =
        new Dictionary<FacilityWorkType, WorkTypeDefinition>();
    private static bool initialized;

    public static IReadOnlyList<WorkTypeDefinition> All
    {
        get
        {
            EnsureInitialized();
            return ById.Values
                .OrderBy(definition => definition.SortOrder)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();
        }
    }

    public static WorkTypeDefinition Register(
        WorkTypeId id,
        string displayName,
        int sortOrder,
        WorkPriorityLevel defaultPriority,
        string capabilityId)
    {
        EnsureInitialized();
        WorkTypeDefinition definition = new WorkTypeDefinition(
            id,
            AllocateCustomType(),
            displayName,
            sortOrder,
            defaultPriority,
            capabilityId);
        Register(definition);
        return definition;
    }

    internal static void Register(WorkTypeDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        EnsureInitialized();
        if (ById.TryGetValue(definition.Id, out WorkTypeDefinition existingById)
            && existingById.Type != definition.Type)
        {
            throw new InvalidOperationException(
                $"Work type id '{definition.Id}' is already assigned to {existingById.Type}.");
        }

        if (ByType.TryGetValue(definition.Type, out WorkTypeDefinition existingByType)
            && !string.Equals(existingByType.Id, definition.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Work type flag '{definition.Type}' is already assigned to '{existingByType.Id}'.");
        }

        ById[definition.Id] = definition;
        ByType[definition.Type] = definition;
    }

    private static FacilityWorkType AllocateCustomType()
    {
        for (int bit = 20; bit < 30; bit++)
        {
            FacilityWorkType candidate = (FacilityWorkType)(1 << bit);
            if (!ByType.ContainsKey(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No custom work type flag is available.");
    }

    public static bool TryGet(string id, out WorkTypeDefinition definition)
    {
        EnsureInitialized();
        return ById.TryGetValue(id?.Trim() ?? string.Empty, out definition);
    }

    public static bool TryGet(WorkTypeId id, out WorkTypeDefinition definition)
    {
        EnsureInitialized();
        return ById.TryGetValue(id.Value ?? string.Empty, out definition);
    }

    internal static bool TryGet(FacilityWorkType type, out WorkTypeDefinition definition)
    {
        EnsureInitialized();
        return ByType.TryGetValue(type, out definition);
    }

    internal static WorkTypeDefinition GetRequired(FacilityWorkType type)
    {
        if (TryGet(type, out WorkTypeDefinition definition))
        {
            return definition;
        }

        throw new KeyNotFoundException($"No work type definition is registered for '{type}' ({(int)type}).");
    }

    internal static IEnumerable<WorkTypeDefinition> Enumerate(FacilityWorkType workTypes)
    {
        return All.Where(definition => (workTypes & definition.Type) != 0);
    }

    public static void ResetToBuiltIns()
    {
        ById.Clear();
        ByType.Clear();
        initialized = true;
        RegisterBuiltIns();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeState()
    {
        ById.Clear();
        ByType.Clear();
        initialized = false;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        RegisterBuiltIns();
    }

    private static void RegisterBuiltIns()
    {
        RegisterBuiltIn(BuiltInWorkTypeIds.Operate, FacilityWorkType.Operate, "운영", 10, WorkPriorityLevel.Priority1, "building:use");
        RegisterBuiltIn(BuiltInWorkTypeIds.Restock, FacilityWorkType.Restock, "보충", 20, WorkPriorityLevel.Priority2, "building:stock");
        RegisterBuiltIn(BuiltInWorkTypeIds.Construct, FacilityWorkType.Construct, "건설", 25, WorkPriorityLevel.Priority2, "building:construct");
        RegisterBuiltIn(BuiltInWorkTypeIds.Repair, FacilityWorkType.Repair, "수리", 30, WorkPriorityLevel.Priority2, "building:durability");
        RegisterBuiltIn(BuiltInWorkTypeIds.Clean, FacilityWorkType.Clean, "청소", 40, WorkPriorityLevel.Priority3, "building:cleaning");
        RegisterBuiltIn(BuiltInWorkTypeIds.Research, FacilityWorkType.Research, "연구", 50, WorkPriorityLevel.Priority2, "building:research");
        RegisterBuiltIn(BuiltInWorkTypeIds.Guard, FacilityWorkType.Guard, "경비", 60, WorkPriorityLevel.Priority3, "building:security");
        RegisterBuiltIn(BuiltInWorkTypeIds.Reception, FacilityWorkType.Reception, "응대", 65, WorkPriorityLevel.Priority2, "exterior:reception");
        RegisterBuiltIn(BuiltInWorkTypeIds.Rescue, FacilityWorkType.Rescue, "구조", 70, WorkPriorityLevel.Priority2, "character:rescue");
        RegisterBuiltIn(BuiltInWorkTypeIds.Rest, FacilityWorkType.Rest, "휴식", 80, WorkPriorityLevel.Priority3, "character:rest");
        RegisterBuiltIn(BuiltInWorkTypeIds.Craft, FacilityWorkType.Craft, "제작", 90, WorkPriorityLevel.Priority2, "building:craft");
        RegisterBuiltIn(BuiltInWorkTypeIds.Haul, FacilityWorkType.Haul, "운반", 95, WorkPriorityLevel.Priority2, "item:haul");
        RegisterBuiltIn(BuiltInWorkTypeIds.Hunt, FacilityWorkType.Hunt, "사냥", 96, WorkPriorityLevel.Priority2, "wildlife:hunt");
        RegisterBuiltIn(BuiltInWorkTypeIds.Butcher, FacilityWorkType.Butcher, "도축", 97, WorkPriorityLevel.Priority2, "wildlife:butcher");
        RegisterBuiltIn(BuiltInWorkTypeIds.DrawWater, FacilityWorkType.DrawWater, "급수", 98, WorkPriorityLevel.Priority2, "survival:water");
        RegisterBuiltIn(BuiltInWorkTypeIds.Cook, FacilityWorkType.Cook, "조리", 99, WorkPriorityLevel.Priority2, "survival:cook");
        RegisterBuiltIn(BuiltInWorkTypeIds.Treat, FacilityWorkType.Treat, "치료", 100, WorkPriorityLevel.Priority2, "survival:treat");
        RegisterBuiltIn(BuiltInWorkTypeIds.Refuel, FacilityWorkType.Refuel, "연료 보충", 101, WorkPriorityLevel.Priority2, "survival:fuel");
        RegisterBuiltIn(BuiltInWorkTypeIds.Warden, FacilityWorkType.Warden, "관리", 102, WorkPriorityLevel.Priority2, "captivity:warden");
        RegisterBuiltIn(BuiltInWorkTypeIds.Perform, FacilityWorkType.Perform, "공연", 103, WorkPriorityLevel.Priority2, "circus:perform");
    }

    private static void RegisterBuiltIn(
        WorkTypeId id,
        FacilityWorkType type,
        string displayName,
        int sortOrder,
        WorkPriorityLevel defaultPriority,
        string capabilityId)
    {
        WorkTypeDefinition definition = new WorkTypeDefinition(
            id,
            type,
            displayName,
            sortOrder,
            defaultPriority,
            capabilityId);
        ById.Add(definition.Id, definition);
        ByType.Add(definition.Type, definition);
    }
}
