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
        long priorCapacityMassGrams)
    {
        Kind = kind;
        OperationId = operationId;
        Epoch = epoch;
        Facility = facility;
        PreparedFingerprint = preparedFingerprint;
        HadOutputAuthority = hadOutputAuthority;
        PriorCapacityMassGrams = priorCapacityMassGrams;
    }

    public ProductionFacilityMutationKind Kind { get; }
    public string OperationId { get; }
    public long Epoch { get; }
    public ProductionFacilityHandle Facility { get; }
    public BuildingInstanceId FacilityId => Facility.InstanceId;
    public string PreparedFingerprint { get; }
    public bool HadOutputAuthority { get; }
    public long PriorCapacityMassGrams { get; }
    public bool AuthorityRevoked { get; internal set; }
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
    private readonly IProductionFacilityMutationEpochAuthority epochs;

    public ProductionFacilityMutationFence(
        IProductionFacilityHandleQuery facilities,
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IProductionOutputDestinationAuthorityRuntime outputAuthority,
        IProductionFacilityMutationEpochAuthority epochs)
    {
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.outputAuthority = outputAuthority
            ?? throw new ArgumentNullException(nameof(outputAuthority));
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

            candidate = new ProductionFacilityEmptyMutationCandidate(
                kind,
                operationId,
                epoch,
                handle,
                snapshot.SemanticFingerprint,
                snapshot.HasAnyAuthority,
                priorCapacity);
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
            || candidate.AuthorityRevoked)
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

        if (!candidate.HadOutputAuthority)
            return true;
        if (!outputAuthority.TryRevoke(candidate.FacilityId, out failureReason))
            return false;

        ProductionOutputDestinationLifecycleSnapshot revoked =
            lifecycle.Capture(candidate.FacilityId);
        if (revoked.HasAnyAuthority || !revoked.CanRevokeEmpty)
        {
            if (!TryRestoreAuthority(candidate, out string restoreFailure))
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
        if (candidate.AuthorityRevoked
            && !TryRestoreAuthority(candidate, out failureReason))
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
        ProductionOutputDestinationLifecycleSnapshot snapshot =
            lifecycle.Capture(handle.InstanceId);
        if (!snapshot.HasAnyAuthority && snapshot.CanRevokeEmpty)
            return true;
        failureReason = "production-facility-" + kind.ToString().ToLowerInvariant()
            + "-requires-no-output-authority:" + snapshot.SemanticFingerprint;
        return false;
    }

    private bool TryRestoreAuthority(
        ProductionFacilityEmptyMutationCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!candidate.HadOutputAuthority)
            return true;
        if (!outputAuthority.TryEnsure(
                candidate.Facility,
                candidate.PriorCapacityMassGrams,
                out FacilityBufferCapacityProfile restored,
                out failureReason)
            || restored == null
            || restored.MaxMassGrams != candidate.PriorCapacityMassGrams)
        {
            failureReason = "production-facility-mutation-authority-restore-failed:"
                + failureReason;
            return false;
        }
        ProductionOutputDestinationLifecycleSnapshot snapshot =
            lifecycle.Capture(candidate.FacilityId);
        if (!snapshot.HasAnyAuthority || !snapshot.CanRevokeEmpty)
        {
            failureReason = "production-facility-mutation-authority-restore-postcondition-failed";
            return false;
        }
        candidate.AuthorityRevoked = false;
        return true;
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
