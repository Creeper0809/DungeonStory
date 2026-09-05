using System;

public enum ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
{
    Applied = 0,
    AlreadyApplied = 1,
    Deferred = 2,
    Conflict = 3
}

public readonly struct ProductionFacilityDestructiveDrainAuthorityConvergenceResult
{
    public ProductionFacilityDestructiveDrainAuthorityConvergenceResult(
        ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
            disposition,
        string failureReason)
    {
        Disposition = disposition;
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
        Disposition { get; }
    public string FailureReason { get; }
    public bool Succeeded => Disposition is
        ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition.Applied
        or ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
            .AlreadyApplied;
}

public interface IProductionFacilityDestructiveDrainAuthorityRevoker
{
    ProductionFacilityDestructiveDrainAuthorityConvergenceResult TryConverge(
        BuildableObject facility,
        ProductionFacilityDestructiveDrainCause cause,
        ProductionFacilityDestructiveDrainOperationId operationId,
        long expectedRevision);
}

/// <summary>
/// Converges only the empty destination authorities owned by an exact open
/// destructive-drain journal entry. Each owner domain commits atomically and
/// cross-domain failures retry forward without recreating an already retired
/// authority.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainAuthorityRevoker :
    IProductionFacilityDestructiveDrainAuthorityRevoker
{
    private readonly IProductionFacilityHandleQuery facilities;
    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly IProductionOutputDestinationAuthorityRuntime output;
    private readonly IProductionStockSensorDestinationAuthorityRuntime sensor;
    private readonly IProductionStockSensorRuntime sensorState;
    private readonly IProductionFacilityDestructiveDrainAuthorityStateQuery
        authorityState;
    private readonly IProductionFacilityDestructiveDrainJournalQuery journal;

    public ProductionFacilityDestructiveDrainAuthorityRevoker(
        IProductionFacilityHandleQuery facilities,
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IProductionOutputDestinationAuthorityRuntime output,
        IProductionStockSensorDestinationAuthorityRuntime sensor,
        IProductionStockSensorRuntime sensorState,
        IProductionFacilityDestructiveDrainAuthorityStateQuery authorityState,
        IProductionFacilityDestructiveDrainJournalQuery journal)
    {
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.output = output ?? throw new ArgumentNullException(nameof(output));
        this.sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
        this.sensorState = sensorState ?? throw new ArgumentNullException(nameof(sensorState));
        this.authorityState = authorityState
            ?? throw new ArgumentNullException(nameof(authorityState));
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
    }

    public ProductionFacilityDestructiveDrainAuthorityConvergenceResult
        TryConverge(
            BuildableObject facility,
            ProductionFacilityDestructiveDrainCause cause,
            ProductionFacilityDestructiveDrainOperationId operationId,
            long expectedRevision)
    {
        if (!TryRequireExactOpen(
                facility,
                cause,
                operationId,
                expectedRevision,
                out ProductionFacilityHandle handle,
                out string tokenFailure))
        {
            return Conflict(tokenFailure);
        }

        ProductionFacilityDestructiveDrainAuthoritySnapshot before =
            authorityState.Capture(handle.InstanceId);
        if (before.HasInvalidPair)
            return Conflict(before.FailureReason);
        if (sensorState.HasOwnedPhysicalState(handle))
        {
            return Deferred(
                "production-destructive-drain-sensor-physical-state-active");
        }

        ProductionOutputDestinationLifecycleSnapshot beforeLifecycle;
        try
        {
            beforeLifecycle = lifecycle.Capture(handle.InstanceId);
        }
        catch (Exception exception)
        {
            return Conflict(
                "production-destructive-drain-lifecycle-capture-failed:"
                + exception.GetType().Name + ":" + exception.Message);
        }
        if (!beforeLifecycle.CanRevokeEmpty)
        {
            return Deferred(
                "production-destructive-drain-lifecycle-not-empty:"
                + beforeLifecycle.SemanticFingerprint);
        }
        if (before.AllAbsent)
        {
            return beforeLifecycle.HasAnyAuthority
                ? Conflict(
                    "production-destructive-drain-untracked-authority-present:"
                    + beforeLifecycle.SemanticFingerprint)
                : AlreadyApplied();
        }

        bool changed = false;
        if (before.Sensor.IsExact)
        {
            if (!sensor.TryValidate(handle, out _, out string sensorFailure))
            {
                return Conflict(
                    "production-destructive-drain-sensor-validate-failed:"
                    + sensorFailure);
            }
            if (!sensor.TryRequireEmpty(handle, out sensorFailure))
            {
                return Deferred(
                    "production-destructive-drain-sensor-not-empty:"
                    + sensorFailure);
            }
            if (!TryRequireExactOpen(
                    facility,
                    cause,
                    operationId,
                    expectedRevision,
                    out _,
                    out tokenFailure))
            {
                return Conflict(tokenFailure);
            }
            if (!sensor.TryRevoke(handle.InstanceId, out sensorFailure))
            {
                if (!TryObserveCommittedDomainRevoke(
                        handle.InstanceId,
                        sensorDomain: true,
                        "production-destructive-drain-sensor-revoke-failed:"
                        + sensorFailure,
                        out ProductionFacilityDestructiveDrainAuthorityConvergenceResult
                            sensorFailureResult))
                {
                    return sensorFailureResult;
                }
            }
            changed = true;
        }

        ProductionFacilityDestructiveDrainAuthoritySnapshot current =
            authorityState.Capture(handle.InstanceId);
        if (current.HasInvalidPair)
            return Conflict(current.FailureReason);
        if (current.Output.IsExact)
        {
            ProductionOutputDestinationLifecycleSnapshot currentLifecycle;
            try
            {
                currentLifecycle = lifecycle.Capture(handle.InstanceId);
            }
            catch (Exception exception)
            {
                return Conflict(
                    "production-destructive-drain-lifecycle-capture-failed:"
                    + exception.GetType().Name + ":" + exception.Message);
            }
            if (!currentLifecycle.CanRevokeEmpty)
            {
                return Deferred(
                    "production-destructive-drain-output-not-empty:"
                    + currentLifecycle.SemanticFingerprint);
            }
            if (!output.TryValidate(handle, out _, out string outputFailure))
            {
                return Conflict(
                    "production-destructive-drain-output-validate-failed:"
                    + outputFailure);
            }
            if (!TryRequireExactOpen(
                    facility,
                    cause,
                    operationId,
                    expectedRevision,
                    out _,
                    out tokenFailure))
            {
                return Conflict(tokenFailure);
            }
            if (!output.TryRevoke(handle.InstanceId, out outputFailure))
            {
                if (!TryObserveCommittedDomainRevoke(
                        handle.InstanceId,
                        sensorDomain: false,
                        "production-destructive-drain-output-revoke-failed:"
                        + outputFailure,
                        out ProductionFacilityDestructiveDrainAuthorityConvergenceResult
                            outputFailureResult))
                {
                    return outputFailureResult;
                }
            }
            changed = true;
        }

        if (!TryRequireExactOpen(
                facility,
                cause,
                operationId,
                expectedRevision,
                out _,
                out tokenFailure))
        {
            return Conflict(tokenFailure);
        }

        ProductionFacilityDestructiveDrainAuthoritySnapshot closed =
            authorityState.Capture(handle.InstanceId);
        if (closed.HasInvalidPair)
            return Conflict(closed.FailureReason);
        if (!closed.AllAbsent)
        {
            return Conflict(
                "production-destructive-drain-authority-revoke-postcondition");
        }

        ProductionOutputDestinationLifecycleSnapshot closedLifecycle;
        try
        {
            closedLifecycle = lifecycle.Capture(handle.InstanceId);
        }
        catch (Exception exception)
        {
            return Conflict(
                "production-destructive-drain-lifecycle-capture-failed:"
                + exception.GetType().Name + ":" + exception.Message);
        }
        if (closedLifecycle.HasAnyAuthority || !closedLifecycle.CanRevokeEmpty)
        {
            return Conflict(
                "production-destructive-drain-authority-revoke-postcondition:"
                + closedLifecycle.SemanticFingerprint);
        }
        return changed ? Applied() : AlreadyApplied();
    }

    private bool TryObserveCommittedDomainRevoke(
            BuildingInstanceId facilityId,
            bool sensorDomain,
            string failureReason,
            out ProductionFacilityDestructiveDrainAuthorityConvergenceResult
                terminalResult)
    {
        ProductionFacilityDestructiveDrainAuthoritySnapshot state =
            authorityState.Capture(facilityId);
        if (state.HasInvalidPair)
        {
            terminalResult = Conflict(state.FailureReason);
            return false;
        }
        ProductionFacilityDestructiveDrainAuthorityPairSnapshot target =
            sensorDomain ? state.Sensor : state.Output;
        if (target.IsAbsent)
        {
            terminalResult = default;
            return true;
        }

        terminalResult = target.IsExact
            ? Deferred(failureReason)
            : Conflict(failureReason);
        return false;
    }

    private bool TryRequireExactOpen(
        BuildableObject facility,
        ProductionFacilityDestructiveDrainCause cause,
        ProductionFacilityDestructiveDrainOperationId operationId,
        long expectedRevision,
        out ProductionFacilityHandle handle,
        out string failureReason)
    {
        handle = null;
        failureReason = string.Empty;
        if (facility == null
            || facility.isDestroy
            || !facility.PersistentInstanceId.IsValid
            || cause == ProductionFacilityDestructiveDrainCause.None
            || !Enum.IsDefined(typeof(ProductionFacilityDestructiveDrainCause), cause)
            || !operationId.IsValid
            || expectedRevision <= 0L
            || !operationId.Equals(
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    facility.PersistentInstanceId))
            || !journal.TryGet(
                operationId,
                out ProductionFacilityDestructiveDrainEntrySaveData entry)
            || entry == null
            || entry.revision != expectedRevision
            || entry.phase !=
                ProductionFacilityDestructiveDrainPhase.AwaitingAuthorityRevoke
            || entry.cause != cause
            || !string.Equals(
                entry.facilityId,
                facility.PersistentInstanceId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                entry.initiatingMutationOperationId,
                ProductionFacilityDestructiveDrainCanonical
                    .BuildInitiatingMutationOperationId(
                        cause,
                        facility.PersistentInstanceId),
                StringComparison.Ordinal))
        {
            failureReason =
                "production-destructive-drain-authority-token-invalid";
            return false;
        }

        try
        {
            handle = facilities.CaptureFacility(facility);
        }
        catch (Exception exception)
        {
            failureReason =
                "production-destructive-drain-facility-capture-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (handle == null
            || handle.IsDestroyed
            || !handle.InstanceId.Equals(facility.PersistentInstanceId))
        {
            failureReason =
                "production-destructive-drain-facility-handle-invalid";
            return false;
        }
        return true;
    }

    private static ProductionFacilityDestructiveDrainAuthorityConvergenceResult
        Applied() => new(
        ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition.Applied,
        string.Empty);

    private static ProductionFacilityDestructiveDrainAuthorityConvergenceResult
        AlreadyApplied() => new(
        ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
            .AlreadyApplied,
        string.Empty);

    private static ProductionFacilityDestructiveDrainAuthorityConvergenceResult
        Deferred(string reason) => new(
        ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition.Deferred,
        reason);

    private static ProductionFacilityDestructiveDrainAuthorityConvergenceResult
        Conflict(string reason) => new(
        ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition.Conflict,
        reason);
}
