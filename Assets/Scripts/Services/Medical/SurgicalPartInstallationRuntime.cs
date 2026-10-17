using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Owns exact physical reservation and the durable installation Transfer.
/// Queries the current aggregate state instead of retaining part-list snapshots.
/// </summary>
internal sealed class SurgicalPartInstallationRuntime
{
    private readonly IWorldItemStackRuntime items;
    private readonly SurgeryAggregateStateStore stateStore;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private List<SurgicalPartInstance> parts => stateStore.State.Parts;

    internal SurgicalPartInstallationRuntime(
        IWorldItemStackRuntime items,
        SurgeryAggregateStateStore stateStore,
        IPhysicalItemBatchDispositionService batchDispositions)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        this.batchDispositions = batchDispositions
            ?? throw new ArgumentNullException(nameof(batchDispositions));
    }

    public bool TryGet(string partInstanceId, out SurgicalPartInstance part)
    {
        part = parts.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.partInstanceId,
                partInstanceId?.Trim(),
                StringComparison.Ordinal));
        return part != null;
    }

    public bool TryReserveForOrder(
        string partInstanceId,
        string orderId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGet(partInstanceId, out SurgicalPartInstance part)
            || part.installed)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(part.reservedOrderId)
            && !string.Equals(part.reservedOrderId, orderId, StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                part.reservedOrderId);
            return false;
        }

        if (part.kind == SurgicalPartKind.NaturalOrgan
            && part.freshnessSeconds <= 0f)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryCorpseStale,
                partInstanceId ?? string.Empty);
            return false;
        }

        part.reservedOrderId = orderId ?? string.Empty;
        return true;
    }

    public void ReleaseReservation(string partInstanceId, string orderId)
    {
        if (TryGet(partInstanceId, out SurgicalPartInstance part)
            && string.Equals(part.reservedOrderId, orderId, StringComparison.Ordinal))
        {
            part.reservedOrderId = string.Empty;
        }
    }

    public bool TryValidateReservationForOrder(
        string partInstanceId,
        string orderId,
        out DomainFailure failure) =>
        TryValidateReservationForOrder(
            partInstanceId,
            orderId,
            requireFreshness: true,
            out failure);

    private bool TryValidateReservationForOrder(
        string partInstanceId,
        string orderId,
        bool requireFreshness,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGet(partInstanceId, out SurgicalPartInstance part)
            || part.installed
            || string.IsNullOrWhiteSpace(part.worldStackId)
            || !string.Equals(
                part.reservedOrderId,
                orderId,
                StringComparison.Ordinal)
            || requireFreshness
                && part.kind == SurgicalPartKind.NaturalOrgan
                && !(part.freshnessSeconds > 0f))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                orderId ?? string.Empty);
            return false;
        }

        WorldItemStackSnapshot[] matching = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.StackId,
                    part.worldStackId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matching.Length != 1
            || matching[0].Quantity != 1
            || !string.Equals(
                matching[0].ItemId,
                part.itemDefinitionId,
                StringComparison.Ordinal)
            || !string.Equals(
                matching[0].ItemInstanceId,
                part.physicalItemInstanceId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                "surgical-part-physical-identity-changed");
            return false;
        }
        return true;
    }

    public bool TryConsumeForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        if (!TryPrepareForInstallation(
                partInstanceId,
                orderId,
                subjectId,
                allowExpiredCommittedPart: false,
                out part,
                out failure))
        {
            return false;
        }
        if (!SurgicalPartInstallationOutbox.TryFinalizePending(
                part,
                batchDispositions,
                out string finalizeFailure))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                finalizeFailure);
            return false;
        }
        return true;
    }

    internal bool TryPrepareForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
        bool allowExpiredCommittedPart,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        part = null;
        failure = DomainFailure.None;
        if (string.IsNullOrEmpty(partInstanceId)
            || string.IsNullOrEmpty(orderId)
            || string.IsNullOrEmpty(subjectId)
            || !string.Equals(
                partInstanceId,
                partInstanceId.Trim(),
                StringComparison.Ordinal)
            || !string.Equals(orderId, orderId.Trim(), StringComparison.Ordinal)
            || !string.Equals(subjectId, subjectId.Trim(), StringComparison.Ordinal)
            || !TryGet(partInstanceId, out part))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        string operationId = SurgicalPartInstallationIdentity.FormatOperationId(
            orderId,
            partInstanceId);
        if (part.installed)
        {
            if (!string.Equals(
                    part.installationOrderId,
                    orderId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    part.installationOperationId,
                    operationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    part.installationSubjectId,
                    subjectId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    part.installedSubjectId,
                    subjectId,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryPartUnavailable,
                    partInstanceId ?? string.Empty,
                    "installation-replay-conflict");
                return false;
            }
            return true;
        }
        bool createdIntent = string.IsNullOrEmpty(part.installationOperationId);
        if (createdIntent)
        {
            if (!string.Equals(
                    part.reservedOrderId,
                    orderId,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryPartUnavailable,
                    partInstanceId ?? string.Empty);
                return false;
            }
            if (!allowExpiredCommittedPart
                && part.kind == SurgicalPartKind.NaturalOrgan
                && !(part.freshnessSeconds > 0f))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryCorpseStale,
                    partInstanceId);
                return false;
            }
            if (!TryValidateReservationForOrder(
                    partInstanceId,
                    orderId,
                    requireFreshness: !allowExpiredCommittedPart,
                    out failure))
            {
                return false;
            }
            part.installationOrderId = orderId ?? string.Empty;
            part.installationOperationId = operationId;
            part.installationSourceStackId = part.worldStackId ?? string.Empty;
            part.installationSubjectId = subjectId ?? string.Empty;
        }
        if (!string.Equals(part.installationOrderId, orderId, StringComparison.Ordinal)
            || !string.Equals(
                part.installationOperationId,
                operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                part.installationSourceStackId,
                part.worldStackId,
                StringComparison.Ordinal)
            || !string.Equals(
                part.installationSubjectId,
                subjectId,
                StringComparison.Ordinal)
            || string.IsNullOrEmpty(part.installationSourceStackId))
        {
            if (createdIntent)
            {
                ClearInstallationIntent(part);
            }
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                "installation-intent-conflict");
            return false;
        }
        if (!batchDispositions.TryCommitPending(
                new[]
                {
                    new PhysicalItemTransformInput(
                        part.installationSourceStackId,
                        1)
                },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                SurgicalPartInstallationOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt disposition,
                out string dispositionFailure))
        {
            if (createdIntent)
            {
                ClearInstallationIntent(part);
            }
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty,
                dispositionFailure ?? string.Empty);
            return false;
        }

        part.installationCommitId = disposition.CommitId;
        return true;
    }

    internal bool TryValidateForInstallation(
        string partInstanceId,
        string orderId,
        string subjectId,
        bool allowExpiredCommittedPart,
        out SurgicalPartInstance part,
        out DomainFailure failure)
    {
        part = null;
        failure = DomainFailure.None;
        if (string.IsNullOrEmpty(partInstanceId)
            || string.IsNullOrEmpty(orderId)
            || string.IsNullOrEmpty(subjectId)
            || !string.Equals(
                partInstanceId,
                partInstanceId.Trim(),
                StringComparison.Ordinal)
            || !string.Equals(orderId, orderId.Trim(), StringComparison.Ordinal)
            || !string.Equals(subjectId, subjectId.Trim(), StringComparison.Ordinal)
            || !TryGet(partInstanceId, out part))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId ?? string.Empty);
            return false;
        }

        string operationId = SurgicalPartInstallationIdentity.FormatOperationId(
            orderId,
            partInstanceId);
        if (part.installed)
        {
            if (!string.Equals(part.installationOrderId, orderId,
                    StringComparison.Ordinal)
                || !string.Equals(part.installationOperationId, operationId,
                    StringComparison.Ordinal)
                || !string.Equals(part.installationSubjectId, subjectId,
                    StringComparison.Ordinal)
                || !string.Equals(part.installedSubjectId, subjectId,
                    StringComparison.Ordinal))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryPartUnavailable,
                    partInstanceId,
                    "installation-replay-conflict");
                return false;
            }
            return true;
        }

        if (string.IsNullOrEmpty(part.installationOperationId))
        {
            return TryValidateReservationForOrder(
                partInstanceId,
                orderId,
                requireFreshness: !allowExpiredCommittedPart,
                out failure);
        }

        if (!string.Equals(part.installationOrderId, orderId,
                StringComparison.Ordinal)
            || !string.Equals(part.installationOperationId, operationId,
                StringComparison.Ordinal)
            || !string.Equals(part.installationSubjectId, subjectId,
                StringComparison.Ordinal)
            || !string.Equals(part.installationSourceStackId,
                part.worldStackId, StringComparison.Ordinal)
            || string.IsNullOrEmpty(part.installationSourceStackId))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId,
                "installation-intent-conflict");
            return false;
        }

        if (string.IsNullOrEmpty(part.installationCommitId))
        {
            return TryValidateReservationForOrder(
                partInstanceId,
                orderId,
                requireFreshness: !allowExpiredCommittedPart,
                out failure);
        }
        return true;
    }

    internal static void ClearInstallationIntent(SurgicalPartInstance part)
    {
        part.installationOrderId = string.Empty;
        part.installationOperationId = string.Empty;
        part.installationCommitId = string.Empty;
        part.installationSourceStackId = string.Empty;
        part.installationSubjectId = string.Empty;
    }

}
