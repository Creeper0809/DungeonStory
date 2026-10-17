using System;
using DungeonStory.Foundation.Facts;

/// <summary>Frozen physical wear values; contains no narrative formatting or ledger identity.</summary>
[Serializable]
public sealed class DurableEquipmentUseFactData
{
    public string stackId = string.Empty, itemId = string.Empty, facilityId = string.Empty, requirementId = string.Empty;
    public long assignmentSequence, revisionBefore, revisionAfter;
    public int quantityBefore, quantityAfter;
    public double durabilityBefore, durabilityAfter, maximumDurability, requestedWear;
    public DurableEquipmentUseFactData Clone() => (DurableEquipmentUseFactData)MemberwiseClone();
    public static DurableEquipmentUseFactData Capture(DurableFacilityEquipmentUseContext context)
    {
        var value = new DurableFacilityEquipmentUseEvidence(context);
        var result = new DurableEquipmentUseFactData
        { stackId = value.StackId, itemId = value.ItemId, facilityId = value.FacilityId, requirementId = value.RequirementId,
            assignmentSequence = value.AssignmentSequence, revisionBefore = value.RevisionBefore, revisionAfter = value.RevisionAfter,
            quantityBefore = value.QuantityBefore, quantityAfter = value.QuantityAfter, durabilityBefore = value.DurabilityBefore,
            durabilityAfter = value.DurabilityAfter, maximumDurability = value.MaximumDurability, requestedWear = value.RequestedWear };
        result.Validate(); return result;
    }
    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(stackId) || !DomainFactValueSyntax.IsIdentity(itemId)
            || !DomainFactValueSyntax.IsIdentity(facilityId) || !DomainFactValueSyntax.IsIdentity(requirementId)
            || assignmentSequence <= 0 || revisionBefore < 0 || revisionAfter <= revisionBefore
            || quantityBefore <= 0 || quantityAfter != quantityBefore || !double.IsFinite(durabilityBefore)
            || !double.IsFinite(durabilityAfter) || !double.IsFinite(maximumDurability) || !double.IsFinite(requestedWear)
            || durabilityBefore <= 0 || maximumDurability < durabilityBefore || durabilityAfter < 0 || requestedWear <= 0
            || Math.Abs(durabilityAfter - Math.Max(0, durabilityBefore - requestedWear)) > 0.0000001)
            throw new ArgumentException("durable-equipment-use-fact-invalid");
    }
}
