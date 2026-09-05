using System;

/// <summary>
/// Shared fail-loud policy for production owners whose facility topology is
/// frozen by an open relocation/evolution/synthesis or destructive drain.
/// Terminal publication/acknowledgement remains the responsibility of each
/// owner and must be allowed before calling this policy.
/// </summary>
public static class ProductionFacilityMutationWorkPolicy
{
    public static bool TryRequireMutable(
        IProductionFacilityMutationEpochQuery mutations,
        BuildingInstanceId facilityId,
        out DomainFailure failure)
    {
        if (mutations == null)
            throw new ArgumentNullException(nameof(mutations));
        if (!facilityId.IsValid)
        {
            failure = new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                string.Empty,
                "production-facility-id-invalid");
            return false;
        }
        if (!mutations.TryCaptureOpen(facilityId, out var open))
        {
            failure = DomainFailure.None;
            return true;
        }

        failure = new DomainFailure(
            FailureCode.ProductionBillUnavailable,
            facilityId.Value,
            "production-facility-mutation-open:"
            + KindToken(open.Kind)
            + ":" + open.OperationId
            + ":" + open.OperationRevision);
        return false;
    }

    public static bool IsMutable(
        IProductionFacilityMutationEpochQuery mutations,
        BuildingInstanceId facilityId) => TryRequireMutable(
        mutations,
        facilityId,
        out _);

    private static string KindToken(ProductionFacilityMutationFenceKind kind) =>
        kind switch
        {
            ProductionFacilityMutationFenceKind.TransientTopology =>
                "transient-topology",
            ProductionFacilityMutationFenceKind.DurableDestructiveDrain =>
                "durable-destructive-drain",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
