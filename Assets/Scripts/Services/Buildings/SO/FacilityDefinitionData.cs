using System;
using System.Collections.Generic;
using UnityEngine;


public static class FacilityAnchorPurposeIds
{
    public const string Use = "facility.use";
    public const string Work = "facility.work";
    public const string Checkout = "facility.checkout";
    public const string Exit = "facility.exit";
}

[Serializable]
public sealed class FacilityAnchorSlot
{
    [Tooltip("이 슬롯을 사용하는 시스템의 안정적인 목적 ID")]
    [ContentReference(ContentReferenceKind.FacilityAnchorPurpose)]
    public string purposeId = FacilityAnchorPurposeIds.Use;
    [Tooltip("시설 중심 칸에서 더할 그리드 좌표 오프셋")]
    public Vector2 offset;

    public bool IsValid => !string.IsNullOrWhiteSpace(purposeId);
}

[Serializable]
public sealed class FacilityAnchorData
{
    [SerializeField] private List<FacilityAnchorSlot> slots = new List<FacilityAnchorSlot>();
    [NonSerialized] private IReadOnlyList<FacilityAnchorSlot> slotsView;

    public IReadOnlyList<FacilityAnchorSlot> Slots
    {
        get
        {
            slots ??= new List<FacilityAnchorSlot>();
            return slotsView ??= ReadOnlyView.List(slots);
        }
    }

    public void Add(string purposeId, Vector2 offset)
    {
        if (string.IsNullOrWhiteSpace(purposeId))
        {
            return;
        }

        slots ??= new List<FacilityAnchorSlot>();
        slots.Add(new FacilityAnchorSlot { purposeId = purposeId, offset = offset });
    }

    public IEnumerable<FacilityAnchorSlot> Enumerate(string purposeId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(purposeId))
        {
            yield break;
        }

        foreach (FacilityAnchorSlot slot in slots)
        {
            if (slot != null && slot.IsValid && string.Equals(slot.purposeId, purposeId, StringComparison.Ordinal))
            {
                yield return slot;
            }
        }
    }

    public int RemoveInvalidSlots()
    {
        return slots?.RemoveAll(slot => slot == null || !slot.IsValid) ?? 0;
    }
}

[Serializable]
public class FacilityData
{
    public FacilityRole roles;
    [Min(0)] public int capacity = 1;
    [Min(0f)] public float useDuration = 1f;
    [Min(0)] public int requiredWorkers;
    [SerializeField] internal FacilityWorkType supportedWorkTypes;
    public bool disabledWhenDamaged = true;

    public bool IsVisitorFacility => roles != FacilityRole.None && capacity > 0;
    public bool HasSupportedWorkTypes => (int)supportedWorkTypes != 0;
    public IEnumerable<WorkTypeId> SupportedWorkTypeIds
    {
        get
        {
            foreach (WorkTypeDefinition definition in FacilityWorkTypeMap.Enumerate(supportedWorkTypes))
            {
                yield return definition.WorkTypeId;
            }
        }
    }

    public void SetSupportedWorkTypeIds(IEnumerable<WorkTypeId> workTypeIds)
    {
        supportedWorkTypes = FacilityWorkType.None;
        AddSupportedWorkTypeIds(workTypeIds);
    }

    public void AddSupportedWorkTypeIds(IEnumerable<WorkTypeId> workTypeIds)
    {
        if (workTypeIds == null)
        {
            return;
        }

        foreach (WorkTypeId workTypeId in workTypeIds)
        {
            AddSupportedWorkTypeId(workTypeId);
        }
    }

    public void AddSupportedWorkTypeId(WorkTypeId workTypeId)
    {
        if (WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition))
        {
            supportedWorkTypes |= FacilityWorkTypeMap.GetRequired(definition);
        }
    }

    public bool SupportsRole(FacilityRole role)
    {
        return role != FacilityRole.None && (roles & role) != 0;
    }

    public bool SupportsWork(WorkTypeId workTypeId)
    {
        return workTypeId.IsValid
            && WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            && SupportsWork(FacilityWorkTypeMap.GetRequired(definition));
    }

    internal bool SupportsWork(FacilityWorkType workType)
    {
        return workType != FacilityWorkType.None && (supportedWorkTypes & workType) != 0;
    }
}
