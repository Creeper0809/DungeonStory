using System;
using System.Collections.Generic;
using System.Linq;

[Flags]
public enum EmergencyWorkFlags
{
    None = 0,
    ReserveEligible = 1 << 0,
    InterruptImmediately = 1 << 1,
    InterruptAtCheckpoint = 1 << 2,
    CriticalNonInterruptible = 1 << 3,
    EmergencyResponse = 1 << 4,
    ProtectedRecovery = 1 << 5
}

public sealed class WorkTypeDefinition
{
    internal WorkTypeDefinition(
        string id,
        string displayName,
        int sortOrder,
        WorkPriorityLevel defaultPriority,
        string capabilityId,
        EmergencyWorkFlags emergencyFlags)
        : this(new WorkTypeId(id), displayName, sortOrder, defaultPriority, capabilityId, emergencyFlags)
    {
    }

    internal WorkTypeDefinition(
        WorkTypeId id,
        string displayName,
        int sortOrder,
        WorkPriorityLevel defaultPriority,
        string capabilityId,
        EmergencyWorkFlags emergencyFlags)
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
        EmergencyFlags = ValidateEmergencyFlags(id, emergencyFlags);
    }

    public WorkTypeId WorkTypeId { get; }
    public string Id => WorkTypeId.Value;
    public string DisplayName { get; }
    public int SortOrder { get; }
    public WorkPriorityLevel DefaultPriority { get; }
    public string CapabilityId { get; }
    public EmergencyWorkFlags EmergencyFlags { get; }

    private static EmergencyWorkFlags ValidateEmergencyFlags(
        WorkTypeId workTypeId,
        EmergencyWorkFlags flags)
    {
        int interruptKinds = ((flags & EmergencyWorkFlags.InterruptImmediately) != 0 ? 1 : 0)
            + ((flags & EmergencyWorkFlags.InterruptAtCheckpoint) != 0 ? 1 : 0);
        bool reserveEligible = (flags & EmergencyWorkFlags.ReserveEligible) != 0;
        bool exclusive = (flags & (EmergencyWorkFlags.CriticalNonInterruptible
            | EmergencyWorkFlags.EmergencyResponse
            | EmergencyWorkFlags.ProtectedRecovery)) != 0;
        int exclusiveKinds = ((flags & EmergencyWorkFlags.CriticalNonInterruptible) != 0 ? 1 : 0)
            + ((flags & EmergencyWorkFlags.EmergencyResponse) != 0 ? 1 : 0)
            + ((flags & EmergencyWorkFlags.ProtectedRecovery) != 0 ? 1 : 0);

        if ((reserveEligible && interruptKinds != 1)
            || (!reserveEligible && interruptKinds != 0)
            || (reserveEligible && exclusive)
            || exclusiveKinds > 1
            || (!reserveEligible && exclusiveKinds != 1))
        {
            throw new ArgumentException(
                $"Work type '{workTypeId.Value}' has invalid emergency flags '{flags}'.",
                nameof(flags));
        }

        return flags;
    }
}

/// <summary>
/// Immutable mapping for the stable authored work ID protocol.
/// Mod-added work content must use an authored capability contract, not mutate this global table.
/// </summary>
public static class WorkTypeCatalog
{
    private const EmergencyWorkFlags Immediate =
        EmergencyWorkFlags.ReserveEligible | EmergencyWorkFlags.InterruptImmediately;
    private const EmergencyWorkFlags Checkpoint =
        EmergencyWorkFlags.ReserveEligible | EmergencyWorkFlags.InterruptAtCheckpoint;
    private const EmergencyWorkFlags NonInterruptible =
        EmergencyWorkFlags.CriticalNonInterruptible;
    private const EmergencyWorkFlags Response = EmergencyWorkFlags.EmergencyResponse;
    private const EmergencyWorkFlags Recovery = EmergencyWorkFlags.ProtectedRecovery;

    private static readonly WorkTypeDefinition[] Definitions =
    {
        Definition(BuiltInWorkTypeIds.Operate, "운영", 10, WorkPriorityLevel.Priority1, "building:use", Checkpoint),
        Definition(BuiltInWorkTypeIds.Restock, "보급", 20, WorkPriorityLevel.Priority2, "building:stock", Immediate),
        Definition(BuiltInWorkTypeIds.Construct, "건설", 25, WorkPriorityLevel.Priority2, "building:construct", Checkpoint),
        Definition(BuiltInWorkTypeIds.Repair, "수리", 30, WorkPriorityLevel.Priority2, "building:durability", Checkpoint),
        Definition(BuiltInWorkTypeIds.Clean, "청소", 40, WorkPriorityLevel.Priority3, "building:cleaning", Immediate),
        Definition(BuiltInWorkTypeIds.Research, "연구", 50, WorkPriorityLevel.Priority2, "building:research", Immediate),
        Definition(BuiltInWorkTypeIds.Guard, "경비", 60, WorkPriorityLevel.Priority3, "building:security", Response),
        Definition(BuiltInWorkTypeIds.Reception, "접객", 65, WorkPriorityLevel.Priority2, "exterior:reception", Immediate),
        Definition(BuiltInWorkTypeIds.Rescue, "구조", 70, WorkPriorityLevel.Priority2, "character:rescue", Response),
        Definition(BuiltInWorkTypeIds.Rest, "휴식", 80, WorkPriorityLevel.Priority3, "character:rest", Recovery),
        Definition(BuiltInWorkTypeIds.Craft, "제작", 90, WorkPriorityLevel.Priority2, "building:craft", Checkpoint),
        Definition(BuiltInWorkTypeIds.Haul, "운반", 95, WorkPriorityLevel.Priority2, "item:haul", Immediate),
        Definition(BuiltInWorkTypeIds.Hunt, "사냥", 96, WorkPriorityLevel.Priority2, "wildlife:hunt", Checkpoint),
        Definition(BuiltInWorkTypeIds.Butcher, "도축", 97, WorkPriorityLevel.Priority2, "wildlife:butcher", Checkpoint),
        Definition(BuiltInWorkTypeIds.DrawWater, "급수", 98, WorkPriorityLevel.Priority2, "survival:water", Immediate),
        Definition(BuiltInWorkTypeIds.Cook, "조리", 99, WorkPriorityLevel.Priority2, "survival:cook", Checkpoint),
        Definition(BuiltInWorkTypeIds.Treat, "치료", 100, WorkPriorityLevel.Priority2, "survival:treat", Response),
        Definition(BuiltInWorkTypeIds.Surgery, "수술", 101, WorkPriorityLevel.Priority1, "medical:surgery", NonInterruptible),
        Definition(BuiltInWorkTypeIds.Refuel, "연료 보급", 102, WorkPriorityLevel.Priority2, "survival:fuel", Immediate),
        Definition(BuiltInWorkTypeIds.Warden, "관리", 103, WorkPriorityLevel.Priority2, "captivity:warden", Checkpoint),
        Definition(BuiltInWorkTypeIds.Perform, "공연", 104, WorkPriorityLevel.Priority2, "circus:perform", Immediate),
        Definition(BuiltInWorkTypeIds.Gather, "채집", 110, WorkPriorityLevel.Priority2, "resource:gather", Immediate),
        Definition(BuiltInWorkTypeIds.Sow, "파종", 111, WorkPriorityLevel.Priority2, "crop:sow", Immediate),
        Definition(BuiltInWorkTypeIds.Harvest, "수확", 112, WorkPriorityLevel.Priority2, "crop:harvest", Immediate),
        Definition(BuiltInWorkTypeIds.Logging, "벌목", 113, WorkPriorityLevel.Priority2, "resource:logging", Checkpoint),
        Definition(BuiltInWorkTypeIds.Quarry, "채석", 114, WorkPriorityLevel.Priority2, "resource:quarry", Checkpoint),
        Definition(BuiltInWorkTypeIds.AnimalCare, "동물 돌봄", 115, WorkPriorityLevel.Priority2, "husbandry:care", Checkpoint),
        Definition(BuiltInWorkTypeIds.GrandProject, "대형 사업", 116, WorkPriorityLevel.Priority2, "economy:grand-project", Checkpoint),
        Definition(BuiltInWorkTypeIds.ThreatMitigation, "위협 완화", 117, WorkPriorityLevel.Priority1, "offense:threat-mitigation", Response),
        Definition(BuiltInWorkTypeIds.Plumbing, "배관", 118, WorkPriorityLevel.Priority2, "infrastructure:plumbing", Checkpoint),
        Definition(BuiltInWorkTypeIds.Dismantle, "시설 해체", 119, WorkPriorityLevel.Priority2, "building:dismantle", Checkpoint)
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
        string capabilityId,
        EmergencyWorkFlags emergencyFlags)
    {
        return new WorkTypeDefinition(
            id,
            displayName,
            sortOrder,
            defaultPriority,
            capabilityId,
            emergencyFlags);
    }
}
