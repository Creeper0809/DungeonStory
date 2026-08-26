using System;

/// <summary>
/// Composes the transient synchronous epoch with the durable destructive-drain
/// journal gate. Public production mutations remain frozen after a crash or
/// save/restore until journal-last checkpoint GC closes the operation.
/// </summary>
public sealed class ProductionFacilityMutationAuthorityGate :
    IProductionFacilityMutationEpochAuthority
{
    private readonly ProductionFacilityMutationEpochRuntime transient;
    private readonly IProductionFacilityDestructiveDrainOpenOperationQuery open;

    public ProductionFacilityMutationAuthorityGate(
        ProductionFacilityMutationEpochRuntime transient,
        IProductionFacilityDestructiveDrainOpenOperationQuery open)
    {
        this.transient = transient
            ?? throw new ArgumentNullException(nameof(transient));
        this.open = open ?? throw new ArgumentNullException(nameof(open));
    }

    public long Revision => unchecked(
        transient.Revision * 486187739L + open.Revision);

    public bool IsFrozen(BuildingInstanceId facilityId) =>
        transient.IsFrozen(facilityId) || open.IsOpen(facilityId);

    public bool TryBegin(
        BuildingInstanceId facilityId,
        string ownerOperationId,
        out long epoch,
        out string failureReason)
    {
        epoch = 0L;
        if (open.TryCapture(
                facilityId,
                out ProductionFacilityDestructiveDrainOpenOperationSnapshot
                    pending))
        {
            failureReason =
                "production-facility-mutation-durable-drain-open:"
                + pending.OperationId.Value;
            return false;
        }
        return transient.TryBegin(
            facilityId,
            ownerOperationId,
            out epoch,
            out failureReason);
    }

    public bool IsCurrent(
        BuildingInstanceId facilityId,
        string ownerOperationId,
        long epoch) => transient.IsCurrent(
        facilityId,
        ownerOperationId,
        epoch);

    public bool TryEnd(
        BuildingInstanceId facilityId,
        string ownerOperationId,
        long epoch,
        out string failureReason) => transient.TryEnd(
        facilityId,
        ownerOperationId,
        epoch,
        out failureReason);
}
