using System;
using System.Collections.Generic;
using System.Linq;

public sealed class DefenseFacilityPhysicalRestoreGuard :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "216.world.defense-facility-physical-transactions";
    private readonly DefenseFacilityRuntime runtime;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private bool active;
    private bool published;

    public DefenseFacilityPhysicalRestoreGuard(
        DefenseFacilityRuntime runtime,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
        {
            throw new InvalidOperationException(
                "Defense physical restore validation is already active.");
        }
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
        {
            throw new InvalidOperationException(
                "Defense physical restore validation is not ready to publish.");
        }
        ValidateOwnerSet(runtime.States, physicalCandidates);
        published = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public void CompleteRestoreCandidate()
    {
        if (!active || !published)
        {
            throw new InvalidOperationException(
                "Defense physical restore validation cannot complete.");
        }
        active = false;
        published = false;
    }

    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public static void ValidateOwnerSet(
        IReadOnlyCollection<DefenseFacilityState> states,
        IPhysicalItemRestoreCandidateQuery query)
    {
        Dictionary<string, DefenseFacilityPhysicalCommitSaveData> pending =
            new(StringComparer.Ordinal);
        foreach (DefenseFacilityState state in
                 states ?? Array.Empty<DefenseFacilityState>())
        {
            if (state == null)
            {
                continue;
            }
            ValidateOwner(
                state,
                state.pendingMaintenance,
                DefenseFacilityPhysicalCommitKind.MaintenanceSink,
                state.nextMaintenanceOperationSequence,
                BuildMaintenanceDestinationId(state.facilityPersistentId),
                DefenseFacilityPhysicalTransactionOutbox.MaintenanceItemId,
                pending);
            DefenseFacilityPhysicalCommitSaveData supply = state.pendingSupply;
            ValidateOwner(
                state,
                supply,
                DefenseFacilityPhysicalCommitKind.SupplyTransfer,
                state.nextSupplyOperationSequence,
                BuildSupplyDestinationId(state.facilityPersistentId),
                supply?.itemId ?? string.Empty,
                pending);
        }

        if (query == null || !query.IsCandidateAvailable)
        {
            if (pending.Count == 0)
            {
                return;
            }
            throw new InvalidOperationException(
                "Defense physical restore requires the incoming item candidate.");
        }

        foreach (KeyValuePair<string, DefenseFacilityPhysicalCommitSaveData> pair
                 in pending)
        {
            DefenseFacilityPhysicalCommitSaveData owner = pair.Value;
            PhysicalItemDispositionKind kind =
                owner.kind == DefenseFacilityPhysicalCommitKind.MaintenanceSink
                    ? PhysicalItemDispositionKind.Sink
                    : PhysicalItemDispositionKind.Transfer;
            string[] sourceIds = owner.inputs
                .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
                .Select(value => value.sourceStackId)
                .ToArray();
            if (!query.TryGetPendingBatchDisposition(
                    owner.operationId,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || receipt.Kind != kind
                || !string.Equals(
                    receipt.ReasonCode,
                    owner.reasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.CommitId,
                    owner.commitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.RequestFingerprint,
                    owner.requestFingerprint,
                    StringComparison.Ordinal)
                || receipt.Quantity != owner.inputQuantity
                || receipt.InputMassGrams != owner.inputMassGrams
                || !receipt.SourceStackIds.SequenceEqual(
                    sourceIds,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Defense physical owner has no exact incoming receipt: "
                    + owner.operationId);
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt?.OperationId == null
                || (!receipt.OperationId.StartsWith(
                        DefenseFacilityPhysicalTransactionOutbox
                            .MaintenanceOperationPrefix,
                        StringComparison.Ordinal)
                    && !receipt.OperationId.StartsWith(
                        DefenseFacilityPhysicalTransactionOutbox
                            .SupplyOperationPrefix,
                        StringComparison.Ordinal)))
            {
                continue;
            }
            if (!pending.ContainsKey(receipt.OperationId))
            {
                throw new InvalidOperationException(
                    "Incoming defense physical receipt has no facility owner: "
                    + receipt.OperationId);
            }
        }
    }

    private static void ValidateOwner(
        DefenseFacilityState state,
        DefenseFacilityPhysicalCommitSaveData owner,
        DefenseFacilityPhysicalCommitKind kind,
        int sequence,
        string destinationId,
        string itemId,
        IDictionary<string, DefenseFacilityPhysicalCommitSaveData> pending)
    {
        if (owner == null
            || owner.phase == DefenseFacilityPhysicalCommitPhase.None)
        {
            return;
        }
        if (!DefenseFacilityPhysicalTransactionOutbox.ValidateProvenance(
                owner,
                kind,
                state.facilityPersistentId,
                sequence,
                destinationId,
                itemId,
                owner.inputQuantity,
                owner.supplyBefore,
                owner.supplyUnitsGranted,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Invalid defense physical owner: " + failureReason);
        }
        if (!pending.TryAdd(owner.operationId, owner))
        {
            throw new InvalidOperationException(
                "Duplicate pending defense physical operation: "
                + owner.operationId);
        }
    }

    private static string BuildSupplyDestinationId(string facilityId) =>
        WorldItemStackRuntime.FacilityInputDestinationPrefix
        + "defense:"
        + facilityId;

    private static string BuildMaintenanceDestinationId(string facilityId) =>
        WorldItemStackRuntime.FacilityInputDestinationPrefix
        + "defense-maintenance:"
        + facilityId;
}
