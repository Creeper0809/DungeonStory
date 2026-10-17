using System;
using System.Linq;

/// <summary>Exact physical wear observed inside the durable-use rollback boundary.</summary>
public sealed class ResearchEquipmentUseSnapshot
{
    public ResearchEquipmentUseSnapshot(DurableFacilityEquipmentUseContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));
        StackId = context.Before.StackId;
        ItemId = context.Before.ItemId.Value;
        FacilityId = context.Slot.OwnerFacilityId.Value;
        AssignmentSequence = context.Slot.AssignmentSequence;
        RevisionBefore = context.Before.ContentRevision;
        RevisionAfter = context.After.ContentRevision;
        Before = Read(context.Before, "current");
        After = Read(context.After, "current");
        RequestedWear = context.WearAmount;
        Maximum = Read(context.Before, "maximum");
        if (!IsValidStableId(StackId)
            || !IsValidStableId(ItemId)
            || !IsValidStableId(FacilityId)
            || !string.Equals(context.Requirement.ItemId.Value, ItemId, StringComparison.Ordinal)
            || context.Before.Quantity != context.After.Quantity
            || AssignmentSequence <= 0 || RevisionBefore < 0 || RevisionAfter <= RevisionBefore
            || !double.IsFinite(Maximum) || Maximum <= 0 || Maximum != Read(context.After, "maximum")
            || !double.IsFinite(Before) || !double.IsFinite(After) || !double.IsFinite(RequestedWear)
            || Before <= 0 || Before > Maximum || After < 0 || After > Before || RequestedWear <= 0
            || Math.Abs(After - Math.Max(0, Before - RequestedWear)) > 0.0000001d)
            throw new ArgumentException("research-equipment-wear-context-invalid", nameof(context));
    }

    public string StackId { get; }
    public string ItemId { get; }
    public string FacilityId { get; }
    public long AssignmentSequence { get; }
    public long RevisionBefore { get; }
    public long RevisionAfter { get; }
    public double Before { get; }
    public double After { get; }
    public double RequestedWear { get; }
    public double Maximum { get; }
    public double Spent => Before - After;

    public ResearchEquipmentUseSnapshot(ResearchEquipmentUseData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        StackId = data.stackId; ItemId = data.itemId; FacilityId = data.facilityId;
        AssignmentSequence = data.assignmentSequence;
        RevisionBefore = data.revisionBefore; RevisionAfter = data.revisionAfter;
        Before = data.before; After = data.after; RequestedWear = data.requestedWear; Maximum = data.maximum;
        if (!IsValidStableId(StackId) || !IsValidStableId(ItemId) || !IsValidStableId(FacilityId)
            || AssignmentSequence <= 0 || RevisionBefore < 0 || RevisionAfter <= RevisionBefore
            || !double.IsFinite(Maximum) || Maximum <= 0 || !double.IsFinite(Before)
            || !double.IsFinite(After) || !double.IsFinite(RequestedWear)
            || Before <= 0 || Before > Maximum || After < 0 || After > Before || RequestedWear <= 0
            || Math.Abs(After - Math.Max(0, Before - RequestedWear)) > .0000001d)
            throw new ArgumentException("research-equipment-wear-snapshot-invalid", nameof(data));
    }

    public ResearchEquipmentUseData Capture() => new()
    {
        stackId = StackId, itemId = ItemId, facilityId = FacilityId, assignmentSequence = AssignmentSequence,
        revisionBefore = RevisionBefore, revisionAfter = RevisionAfter, maximum = Maximum,
        before = Before, after = After, requestedWear = RequestedWear
    };

    private static double Read(DurableFacilityEquipmentUseSubject subject, string key)
    {
        var component = subject.Components.SingleOrDefault(value =>
            value.ComponentTypeId == ItemInstanceComponentIds.Durability)
            ?? throw new ArgumentException("research-equipment-durability-missing");
        var values = component.Values.Where(value => value.Key == key).ToArray();
        if (values.Length != 1 || values[0].Kind != ItemStateValueKind.Decimal)
            throw new ArgumentException("research-equipment-durability-invalid:" + key);
        return values[0].DecimalValue;
    }

    private static bool IsValidStableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 192)
            return false;

        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            bool valid = current >= 'a' && current <= 'z'
                || current >= 'A' && current <= 'Z'
                || current >= '0' && current <= '9'
                || current == '.'
                || current == ':'
                || current == '_'
                || current == '-'
                || current == '/';
            if (!valid)
                return false;
        }

        return true;
    }
}
