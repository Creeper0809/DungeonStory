using System;

public enum ProductionFacilityMutationKind
{
    Demolition = 0,
    Relocation = 1,
    Synthesis = 2,
    Evolution = 3,
    DestructiveLoss = 4
}

public sealed class ProductionFacilityEmptyMutationCandidate
{
    internal ProductionFacilityEmptyMutationCandidate(
        ProductionFacilityMutationKind kind,
        string operationId,
        long epoch,
        ProductionFacilityHandle facility,
        string preparedFingerprint,
        bool hadOutputAuthority,
        long priorCapacityMassGrams,
        bool hadStockSensorAuthority,
        long priorStockSensorCapacityMassGrams)
    {
        Kind = kind;
        OperationId = operationId;
        Epoch = epoch;
        Facility = facility;
        PreparedFingerprint = preparedFingerprint;
        HadOutputAuthority = hadOutputAuthority;
        PriorCapacityMassGrams = priorCapacityMassGrams;
        HadStockSensorAuthority = hadStockSensorAuthority;
        PriorStockSensorCapacityMassGrams = priorStockSensorCapacityMassGrams;
    }

    public ProductionFacilityMutationKind Kind { get; }
    public string OperationId { get; }
    public long Epoch { get; }
    public ProductionFacilityHandle Facility { get; }
    public BuildingInstanceId FacilityId => Facility.InstanceId;
    public string PreparedFingerprint { get; }
    public bool HadOutputAuthority { get; }
    public long PriorCapacityMassGrams { get; }
    public bool HadStockSensorAuthority { get; }
    public long PriorStockSensorCapacityMassGrams { get; }
    public bool AuthorityRevoked { get; internal set; }
    public bool StockSensorAuthorityRevoked { get; internal set; }
    public bool IsClosed { get; internal set; }
}

public interface IProductionFacilityMutationFence
{
    bool TryPrepareEmpty(
        BuildableObject facility,
        ProductionFacilityMutationKind kind,
        string operationId,
        out ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason);

    bool TryCommitAuthorityRevoke(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason);

    bool TryAbort(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason);

    bool TryComplete(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason);

    bool TryRequireNoAuthority(
        BuildableObject facility,
        ProductionFacilityMutationKind kind,
        out string failureReason);
}

/// <summary>
/// Synchronous topology fence for gameplay mutations. The mutation epoch is
/// transient, but every decision is made from live runtime authorities and an
/// exact semantic fingerprint; save DTOs are never queried.
/// </summary>
public sealed class ProductionFacilityMutationFence :
    IProductionFacilityMutationFence
{
    private readonly IProductionFacilityHandleQuery facilities;
    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly IProductionOutputDestinationAuthorityRuntime outputAuthority;
    private readonly IProductionStockSensorDestinationAuthorityRuntime
        stockSensorAuthority;
    private readonly IProductionStockSensorRuntime stockSensors;
    private readonly IProductionFacilityMutationEpochAuthority epochs;

    public ProductionFacilityMutationFence(
        IProductionFacilityHandleQuery facilities,
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IProductionOutputDestinationAuthorityRuntime outputAuthority,
        IProductionStockSensorDestinationAuthorityRuntime stockSensorAuthority,
        IProductionStockSensorRuntime stockSensors,
        IProductionFacilityMutationEpochAuthority epochs)
    {
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.outputAuthority = outputAuthority
            ?? throw new ArgumentNullException(nameof(outputAuthority));
        this.stockSensorAuthority = stockSensorAuthority
            ?? throw new ArgumentNullException(nameof(stockSensorAuthority));
        this.stockSensors = stockSensors
            ?? throw new ArgumentNullException(nameof(stockSensors));
        this.epochs = epochs ?? throw new ArgumentNullException(nameof(epochs));
    }

    public bool TryPrepareEmpty(
        BuildableObject facility,
        ProductionFacilityMutationKind kind,
        string operationId,
        out ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (!TryCaptureFacility(facility, out ProductionFacilityHandle handle, out failureReason)
            || !Enum.IsDefined(typeof(ProductionFacilityMutationKind), kind)
            || !IsCanonicalOperationId(operationId))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-facility-mutation-request-invalid"
                : failureReason;
            return false;
        }
        if (!epochs.TryBegin(
                handle.InstanceId,
                operationId,
                out long epoch,
                out failureReason))
        {
            return false;
        }

        try
        {
            if (stockSensors.HasOwnedPhysicalState(handle))
            {
                failureReason =
                    "production-facility-mutation-stock-sensor-state-active";
                EndPreparedEpoch(
                    handle.InstanceId,
                    operationId,
                    epoch,
                    ref failureReason);
                return false;
            }
            ProductionOutputDestinationLifecycleSnapshot snapshot =
                lifecycle.Capture(handle.InstanceId);
            if (!snapshot.CanRevokeEmpty)
            {
                failureReason = FormatBlocked(kind, snapshot);
                EndPreparedEpoch(handle.InstanceId, operationId, epoch, ref failureReason);
                return false;
            }

            long priorCapacity = 0L;
            if (snapshot.HasAnyAuthority)
            {
                if (!outputAuthority.TryValidate(
                        handle,
                        out FacilityBufferCapacityProfile profile,
                        out string authorityFailure)
                    || profile == null
                    || profile.MaxMassGrams <= 0L)
                {
                    failureReason = "production-facility-mutation-authority-invalid:"
                        + authorityFailure;
                    EndPreparedEpoch(handle.InstanceId, operationId, epoch, ref failureReason);
                    return false;
                }
                priorCapacity = profile.MaxMassGrams;
            }

            bool hasStockSensorAuthority =
                !string.IsNullOrEmpty(handle.StockSensorInstallationItemId);
            long priorStockSensorCapacity = 0L;
            if (hasStockSensorAuthority)
            {
                if (!stockSensorAuthority.TryEnsure(
                        handle,
                        out priorStockSensorCapacity,
                        out string sensorEnsureFailure)
                    || priorStockSensorCapacity <= 0L)
                {
                    failureReason =
                        "production-facility-mutation-stock-sensor-authority-invalid:"
                        + sensorEnsureFailure;
                    EndPreparedEpoch(
                        handle.InstanceId,
                        operationId,
                        epoch,
                        ref failureReason);
                    return false;
                }
                if (!stockSensorAuthority.TryRequireEmpty(
                        handle,
                        out string sensorEmptyFailure))
                {
                    failureReason =
                        "production-facility-mutation-stock-sensor-authority-invalid:"
                        + sensorEmptyFailure;
                    EndPreparedEpoch(
                        handle.InstanceId,
                        operationId,
                        epoch,
                        ref failureReason);
                    return false;
                }
            }

            candidate = new ProductionFacilityEmptyMutationCandidate(
                kind,
                operationId,
                epoch,
                handle,
                snapshot.SemanticFingerprint,
                snapshot.HasAnyAuthority,
                priorCapacity,
                hasStockSensorAuthority,
                priorStockSensorCapacity);
            return true;
        }
        catch (Exception exception)
        {
            failureReason = "production-facility-mutation-capture-failed:"
                + exception.GetType().Name + ":" + exception.Message;
            EndPreparedEpoch(handle.InstanceId, operationId, epoch, ref failureReason);
            return false;
        }
    }

    public bool TryCommitAuthorityRevoke(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryRequireOpenCurrent(candidate, out failureReason)
            || candidate.AuthorityRevoked
            || candidate.StockSensorAuthorityRevoked)
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-facility-mutation-already-committed"
                : failureReason;
            return false;
        }

        ProductionOutputDestinationLifecycleSnapshot current =
            lifecycle.Capture(candidate.FacilityId);
        if (!current.CanRevokeEmpty
            || !string.Equals(
                current.SemanticFingerprint,
                candidate.PreparedFingerprint,
                StringComparison.Ordinal))
        {
            failureReason = "production-facility-mutation-candidate-stale:"
                + current.SemanticFingerprint;
            return false;
        }

        if (candidate.HadStockSensorAuthority)
        {
            if (!stockSensorAuthority.TryRevoke(
                    candidate.FacilityId,
                    out failureReason))
            {
                return false;
            }
            candidate.StockSensorAuthorityRevoked = true;
        }

        if (!candidate.HadOutputAuthority)
            return true;
        if (!outputAuthority.TryRevoke(candidate.FacilityId, out failureReason))
        {
            if (!TryRestoreStockSensorAuthority(
                    candidate,
                    out string sensorRestoreFailure))
            {
                failureReason += ":stock-sensor-rollback-failed:"
                    + sensorRestoreFailure;
            }
            return false;
        }

        ProductionOutputDestinationLifecycleSnapshot revoked =
            lifecycle.Capture(candidate.FacilityId);
        if (revoked.HasAnyAuthority || !revoked.CanRevokeEmpty)
        {
            candidate.AuthorityRevoked = true;
            if (!TryRestoreAuthorities(candidate, out string restoreFailure))
            {
                failureReason = "production-facility-mutation-revoke-postcondition-failed:"
                    + restoreFailure;
                return false;
            }
            failureReason = "production-facility-mutation-revoke-postcondition-failed";
            return false;
        }
        candidate.AuthorityRevoked = true;
        return true;
    }

    public bool TryAbort(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryRequireOpenCurrent(candidate, out failureReason))
            return false;
        if ((candidate.AuthorityRevoked
                || candidate.StockSensorAuthorityRevoked)
            && !TryRestoreAuthorities(candidate, out failureReason))
        {
            return false;
        }
        if (!epochs.TryEnd(
                candidate.FacilityId,
                candidate.OperationId,
                candidate.Epoch,
                out failureReason))
        {
            return false;
        }
        candidate.IsClosed = true;
        return true;
    }

    public bool TryComplete(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryRequireOpenCurrent(candidate, out failureReason))
            return false;
        if (candidate.HadOutputAuthority && !candidate.AuthorityRevoked)
        {
            failureReason = "production-facility-mutation-authority-not-revoked";
            return false;
        }
        if (candidate.HadStockSensorAuthority
            && !candidate.StockSensorAuthorityRevoked)
        {
            failureReason =
                "production-facility-mutation-stock-sensor-authority-not-revoked";
            return false;
        }
        if (!epochs.TryEnd(
                candidate.FacilityId,
                candidate.OperationId,
                candidate.Epoch,
                out failureReason))
        {
            return false;
        }
        candidate.IsClosed = true;
        return true;
    }

    public bool TryRequireNoAuthority(
        BuildableObject facility,
        ProductionFacilityMutationKind kind,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryCaptureFacility(facility, out ProductionFacilityHandle handle, out failureReason))
            return false;
        if (stockSensors.HasOwnedPhysicalState(handle))
        {
            failureReason =
                "production-facility-mutation-stock-sensor-state-active";
            return false;
        }
        if (!TryRequireStockSensorAuthorityEmpty(handle, out failureReason))
            return false;
        ProductionOutputDestinationLifecycleSnapshot snapshot =
            lifecycle.Capture(handle.InstanceId);
        if (!snapshot.HasAnyAuthority && snapshot.CanRevokeEmpty)
            return true;
        failureReason = "production-facility-" + kind.ToString().ToLowerInvariant()
            + "-requires-no-output-authority:" + snapshot.SemanticFingerprint;
        return false;
    }

    private bool TryRestoreAuthorities(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (candidate.AuthorityRevoked
            && (!outputAuthority.TryEnsure(
                candidate.Facility,
                candidate.PriorCapacityMassGrams,
                out FacilityBufferCapacityProfile restored,
                out failureReason)
            || restored == null
            || restored.MaxMassGrams != candidate.PriorCapacityMassGrams))
        {
            failureReason = "production-facility-mutation-authority-restore-failed:"
                + failureReason;
            return false;
        }
        if (candidate.AuthorityRevoked)
        {
            ProductionOutputDestinationLifecycleSnapshot snapshot =
                lifecycle.Capture(candidate.FacilityId);
            if (!snapshot.HasAnyAuthority || !snapshot.CanRevokeEmpty)
            {
                failureReason =
                    "production-facility-mutation-authority-restore-postcondition-failed";
                return false;
            }
            candidate.AuthorityRevoked = false;
        }
        return TryRestoreStockSensorAuthority(candidate, out failureReason);
    }

    private bool TryRestoreStockSensorAuthority(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!candidate.StockSensorAuthorityRevoked)
            return true;
        if (!stockSensorAuthority.TryEnsure(
                candidate.Facility,
                out long restoredMassGrams,
                out failureReason)
            || restoredMassGrams != candidate.PriorStockSensorCapacityMassGrams
            || !stockSensorAuthority.TryRequireEmpty(
                candidate.Facility,
                out failureReason))
        {
            failureReason =
                "production-facility-mutation-stock-sensor-restore-failed:"
                + failureReason;
            return false;
        }
        candidate.StockSensorAuthorityRevoked = false;
        return true;
    }

    private bool TryRequireStockSensorAuthorityEmpty(
        ProductionFacilityHandle handle,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrEmpty(handle.StockSensorInstallationItemId))
            return true;
        return stockSensorAuthority.TryEnsure(handle, out _, out failureReason)
            && stockSensorAuthority.TryRequireEmpty(handle, out failureReason);
    }

    private bool TryCaptureFacility(
        BuildableObject facility,
        out ProductionFacilityHandle handle,
        out string failureReason)
    {
        handle = null;
        failureReason = string.Empty;
        if (facility == null
            || facility.isDestroy
            || !facility.PersistentInstanceId.IsValid)
        {
            failureReason = "production-facility-mutation-facility-invalid";
            return false;
        }
        try
        {
            handle = facilities.CaptureFacility(facility);
        }
        catch (Exception exception)
        {
            failureReason = "production-facility-mutation-facility-capture-failed:"
                + exception.GetType().Name;
            return false;
        }
        if (handle == null || handle.IsDestroyed || !handle.InstanceId.IsValid)
        {
            failureReason = "production-facility-mutation-facility-invalid";
            return false;
        }
        return true;
    }

    private bool TryRequireOpenCurrent(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (candidate == null || candidate.IsClosed)
        {
            failureReason = "production-facility-mutation-candidate-closed";
            return false;
        }
        if (!epochs.IsCurrent(
                candidate.FacilityId,
                candidate.OperationId,
                candidate.Epoch))
        {
            failureReason = "production-facility-mutation-epoch-stale";
            return false;
        }
        return true;
    }

    private static bool IsCanonicalOperationId(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static string FormatBlocked(
        ProductionFacilityMutationKind kind,
        ProductionOutputDestinationLifecycleSnapshot snapshot)
    {
        System.Text.StringBuilder result = new System.Text.StringBuilder(128)
            .Append("production-facility-")
            .Append(kind.ToString().ToLowerInvariant())
            .Append("-blocked:")
            .Append(snapshot.SemanticFingerprint).Append(':');
        for (int i = 0; i < snapshot.Blocks.Count; i++)
        {
            ProductionOutputLifecycleBlock block = snapshot.Blocks[i];
            if (i > 0)
                result.Append(',');
            result.Append(block.Code).Append('=')
                .Append(block.Count).Append('/').Append(block.MassGrams).Append('g');
        }
        return result.ToString();
    }

    private void EndPreparedEpoch(
        BuildingInstanceId facilityId,
        string operationId,
        long epoch,
        ref string failureReason)
    {
        if (!epochs.TryEnd(facilityId, operationId, epoch, out string endFailure))
            failureReason += ":epoch-end-failed:" + endFailure;
    }
}
