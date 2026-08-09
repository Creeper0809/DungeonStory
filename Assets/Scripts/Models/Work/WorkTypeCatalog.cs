using System;
using System.Collections.Generic;
using System.Linq;

public sealed class WorkTypeDefinition
{
    internal WorkTypeDefinition(
        string id,
        string displayName,
        int sortOrder,
        WorkPriorityLevel defaultPriority,
        string capabilityId)
        : this(new WorkTypeId(id), displayName, sortOrder, defaultPriority, capabilityId)
    {
    }

    internal WorkTypeDefinition(
        WorkTypeId id,
        string displayName,
        int sortOrder,
        WorkPriorityLevel defaultPriority,
        string capabilityId)
    {
        if (!id.IsValid)
        {
            throw new ArgumentException("Work type id is required.", nameof(id));
        }

        WorkTypeId = id;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        SortOrder = sortOrder;
        DefaultPriority = defaultPriority;
        CapabilityId = capabilityId?.Trim() ?? string.Empty;
    }

    public WorkTypeId WorkTypeId { get; }
    public string Id => WorkTypeId.Value;
    public string DisplayName { get; }
    public int SortOrder { get; }
    public WorkPriorityLevel DefaultPriority { get; }
    public string CapabilityId { get; }

}

/// <summary>
/// Immutable mapping for the stable authored work ID protocol.
/// Mod-added work content must use an authored capability contract, not mutate this global table.
/// </summary>
public static class WorkTypeCatalog
{
    private static readonly WorkTypeDefinition[] Definitions =
    {
        Definition(BuiltInWorkTypeIds.Operate, "운영", 10, WorkPriorityLevel.Priority1, "building:use"),
        Definition(BuiltInWorkTypeIds.Restock, "보급", 20, WorkPriorityLevel.Priority2, "building:stock"),
        Definition(BuiltInWorkTypeIds.Construct, "건설", 25, WorkPriorityLevel.Priority2, "building:construct"),
        Definition(BuiltInWorkTypeIds.Repair, "수리", 30, WorkPriorityLevel.Priority2, "building:durability"),
        Definition(BuiltInWorkTypeIds.Clean, "청소", 40, WorkPriorityLevel.Priority3, "building:cleaning"),
        Definition(BuiltInWorkTypeIds.Research, "연구", 50, WorkPriorityLevel.Priority2, "building:research"),
        Definition(BuiltInWorkTypeIds.Guard, "경비", 60, WorkPriorityLevel.Priority3, "building:security"),
        Definition(BuiltInWorkTypeIds.Reception, "접객", 65, WorkPriorityLevel.Priority2, "exterior:reception"),
        Definition(BuiltInWorkTypeIds.Rescue, "구조", 70, WorkPriorityLevel.Priority2, "character:rescue"),
        Definition(BuiltInWorkTypeIds.Rest, "휴식", 80, WorkPriorityLevel.Priority3, "character:rest"),
        Definition(BuiltInWorkTypeIds.Craft, "제작", 90, WorkPriorityLevel.Priority2, "building:craft"),
        Definition(BuiltInWorkTypeIds.Haul, "운반", 95, WorkPriorityLevel.Priority2, "item:haul"),
        Definition(BuiltInWorkTypeIds.Hunt, "사냥", 96, WorkPriorityLevel.Priority2, "wildlife:hunt"),
        Definition(BuiltInWorkTypeIds.Butcher, "도축", 97, WorkPriorityLevel.Priority2, "wildlife:butcher"),
        Definition(BuiltInWorkTypeIds.DrawWater, "급수", 98, WorkPriorityLevel.Priority2, "survival:water"),
        Definition(BuiltInWorkTypeIds.Cook, "조리", 99, WorkPriorityLevel.Priority2, "survival:cook"),
        Definition(BuiltInWorkTypeIds.Treat, "치료", 100, WorkPriorityLevel.Priority2, "survival:treat"),
        Definition(BuiltInWorkTypeIds.Surgery, "수술", 101, WorkPriorityLevel.Priority1, "medical:surgery"),
        Definition(BuiltInWorkTypeIds.Refuel, "연료 보급", 102, WorkPriorityLevel.Priority2, "survival:fuel"),
        Definition(BuiltInWorkTypeIds.Warden, "관리", 103, WorkPriorityLevel.Priority2, "captivity:warden"),
        Definition(BuiltInWorkTypeIds.Perform, "공연", 104, WorkPriorityLevel.Priority2, "circus:perform"),
        Definition(BuiltInWorkTypeIds.Gather, "채집", 110, WorkPriorityLevel.Priority2, "resource:gather"),
        Definition(BuiltInWorkTypeIds.Sow, "파종", 111, WorkPriorityLevel.Priority2, "crop:sow"),
        Definition(BuiltInWorkTypeIds.Harvest, "수확", 112, WorkPriorityLevel.Priority2, "crop:harvest"),
        Definition(BuiltInWorkTypeIds.Logging, "벌목", 113, WorkPriorityLevel.Priority2, "resource:logging"),
        Definition(BuiltInWorkTypeIds.Quarry, "채석", 114, WorkPriorityLevel.Priority2, "resource:quarry"),
        Definition(BuiltInWorkTypeIds.AnimalCare, "동물 돌봄", 115, WorkPriorityLevel.Priority2, "husbandry:care"),
        Definition(BuiltInWorkTypeIds.GrandProject, "대형 사업", 116, WorkPriorityLevel.Priority2, "economy:grand-project"),
        Definition(BuiltInWorkTypeIds.ThreatMitigation, "위협 완화", 117, WorkPriorityLevel.Priority1, "offense:threat-mitigation"),
        Definition(BuiltInWorkTypeIds.Plumbing, "배관", 118, WorkPriorityLevel.Priority2, "infrastructure:plumbing"),
        Definition(BuiltInWorkTypeIds.Dismantle, "시설 해체", 119, WorkPriorityLevel.Priority2, "building:dismantle")
    };

    public static IReadOnlyList<WorkTypeDefinition> All => Definitions;

    public static bool TryGet(string id, out WorkTypeDefinition definition)
    {
        string normalized = id?.Trim() ?? string.Empty;
        definition = Definitions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, normalized, StringComparison.Ordinal));
        return definition != null;
    }

    public static bool TryGet(WorkTypeId id, out WorkTypeDefinition definition)
    {
        return TryGet(id.Value, out definition);
    }

    private static WorkTypeDefinition Definition(
        WorkTypeId id,
        string displayName,
        int sortOrder,
        WorkPriorityLevel defaultPriority,
        string capabilityId)
    {
        return new WorkTypeDefinition(
            id,
            displayName,
            sortOrder,
            defaultPriority,
            capabilityId);
    }
}
