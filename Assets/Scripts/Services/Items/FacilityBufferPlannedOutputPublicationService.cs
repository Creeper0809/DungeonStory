using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using VContainer;

public enum FacilityBufferPlannedOutputPublicationFailureCode
{
    None = 0,
    InvalidToken = 1,
    UnsupportedPreparedSubject = 2,
    CatalogMismatch = 3,
    ExistingPublicationConflict = 4,
    PhysicalMassMismatch = 5,
    RepositoryTransactionFailed = 6
}

public interface IFacilityBufferPlannedOutputPublicationFaultInjector
{
    bool FailBeforeRepositoryAdd(int zeroBasedStackIndex);
}

public interface IFacilityBufferPlannedOutputPublicationService
{
    bool TryPublishFullBatch(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryRollbackPublishedBatch(
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryAcknowledgePublishedBatch(
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryRollbackRestoreCandidate(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryAcknowledgeRestoreCandidate(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
    bool TryCapturePendingBatch(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason);
}

#if UNITY_EDITOR
public readonly struct FacilityBufferPlannedOutputPublicationEditorStackSnapshot
{
    public FacilityBufferPlannedOutputPublicationEditorStackSnapshot(
        string stackId,
        int quantity,
        WorldItemStackState state,
        string destinationId,
        Vector2Int position,
        int markerCount,
        bool markerAffectsStacking)
    {
        StackId = stackId;
        Quantity = quantity;
        State = state;
        DestinationId = destinationId;
        Position = position;
        MarkerCount = markerCount;
        MarkerAffectsStacking = markerAffectsStacking;
    }

    public string StackId { get; }
    public int Quantity { get; }
    public WorldItemStackState State { get; }
    public string DestinationId { get; }
    public Vector2Int Position { get; }
    public int MarkerCount { get; }
    public bool MarkerAffectsStacking { get; }
}

public readonly struct FacilityBufferPlannedOutputPublicationEditorSnapshot
{
    public FacilityBufferPlannedOutputPublicationEditorSnapshot(
        int itemStackVersion,
        IReadOnlyList<FacilityBufferPlannedOutputPublicationEditorStackSnapshot>
            stacks)
    {
        ItemStackVersion = itemStackVersion;
        Stacks = stacks ?? Array.Empty<
            FacilityBufferPlannedOutputPublicationEditorStackSnapshot>();
    }

    public int ItemStackVersion { get; }
    public IReadOnlyList<FacilityBufferPlannedOutputPublicationEditorStackSnapshot>
        Stacks { get; }
}
#endif

/// <summary>
/// Atomically materializes a resolved, admitted output plan. The admission token
/// describes objects which do not exist yet; repository stack IDs are allocated
/// only while preparing the complete batch and are never used as reservation IDs.
/// </summary>
public sealed class FacilityBufferPlannedOutputPublicationService :
    IFacilityBufferPlannedOutputPublicationService
{
    private readonly WorldItemRepository repository;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferMassAdmissionService admission;
    private readonly IFacilityBufferPlannedOutputPublicationFaultInjector faultInjector;

    [Inject]
    public FacilityBufferPlannedOutputPublicationService(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferMassAdmissionService admission)
        : this(repository, catalog, massQuery, admission, null)
    {
    }

    public FacilityBufferPlannedOutputPublicationService(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferMassAdmissionService admission,
        IFacilityBufferPlannedOutputPublicationFaultInjector faultInjector = null)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.admission = admission ?? throw new ArgumentNullException(nameof(admission));
        this.faultInjector = faultInjector;
    }

#if UNITY_EDITOR
    public FacilityBufferPlannedOutputPublicationEditorSnapshot
        CaptureEditorTestSnapshot()
    {
        FacilityBufferPlannedOutputPublicationEditorStackSnapshot[] stacks =
            repository.Records
                .OrderBy(value => value.stackId, StringComparer.Ordinal)
                .Select(value =>
                {
                    ItemInstanceComponentSaveData[] markers = value.components
                        .Where(PlannedOutputPublicationComponentCodec.IsAnyMarker)
                        .ToArray();
                    return new
                        FacilityBufferPlannedOutputPublicationEditorStackSnapshot(
                            value.stackId,
                            value.quantity,
                            value.state,
                            value.destinationId,
                            value.position,
                            markers.Length,
                            markers.Length == 1 && markers[0].affectsStacking);
                })
                .ToArray();
        return new FacilityBufferPlannedOutputPublicationEditorSnapshot(
            repository.ItemStackVersion,
            stacks);
    }

    public IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
        CapturePendingRestoreBatchesForEditorTest() =>
        FacilityBufferPlannedOutputRestoreCandidateFactory.CapturePendingBatches(
            repository.Records,
            massQuery);

    public void DecrementFirstStackQuantityForEditorTest()
    {
        WorldItemStackRecord first = repository.Records
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (first == null || first.quantity <= 1)
            throw new InvalidOperationException(
                "Planned-output Editor fixture has no decrementable stack.");
        first.quantity--;
    }

    public void RemoveFirstStackForEditorTest()
    {
        WorldItemStackRecord first = repository.Records
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (first == null)
            throw new InvalidOperationException(
                "Planned-output Editor fixture has no removable stack.");
        repository.Remove(first);
    }
#endif

    public bool TryPublishFullBatch(
        FacilityBufferPlannedOutputToken token,
        out FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        receipt = default;
        failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
        failureReason = string.Empty;
        if (!admission.TryValidatePlannedOutputPublicationToken(
                token,
                out bool admissionCommitted,
                out _,
                out string tokenFailure))
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.InvalidToken,
                tokenFailure,
                out failureCode,
                out failureReason);
        }
        if (massQuery.AuthorityRevision != token.MassAuthorityRevision)
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.InvalidToken,
                "Planned-output mass authority changed before publication.",
                out failureCode,
                out failureReason);
        }

        if (!TryBuildExpectedStacks(
                token,
                allocateIdentities: false,
                out List<PreparedStack> expected,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        WorldItemStackRecord[] existing = repository.Records
            .Where(record => PlannedOutputPublicationComponentCodec.HasBatchCommitId(
                record?.components,
                token.Request.BatchCommitId))
            .OrderBy(record => record.stackId, StringComparer.Ordinal)
            .ToArray();
        if (existing.Length > 0)
        {
            return TryReplayExisting(
                token,
                expected,
                existing,
                out receipt,
                out failureCode,
                out failureReason);
        }
        if (admissionCommitted)
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict,
                $"Committed batch '{token.Request.BatchCommitId}' has no physical publication.",
                out failureCode,
                out failureReason);
        }

        if (!TryBuildExpectedStacks(
                token,
                allocateIdentities: true,
                out List<PreparedStack> publish,
                out failureCode,
                out failureReason))
        {
            return false;
        }

        WorldItemStackRecord[] records = publish
            .Select(value => value.Record)
            .ToArray();
        if (!repository.TryAddBatchAtomically(
                records,
                index => faultInjector?.FailBeforeRepositoryAdd(index) == true,
                out string repositoryFailure))
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.RepositoryTransactionFailed,
                repositoryFailure,
                out failureCode,
                out failureReason);
        }

        receipt = CreateReceipt(token, publish);
        return true;
    }

    public bool TryRollbackPublishedBatch(
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
        failureReason = string.Empty;
        if (admission.TryGetPlannedOutputReceipt(
                receipt.AdmissionTokenId,
                out _))
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.InvalidToken,
                $"Committed batch '{receipt.BatchCommitId}' cannot be rolled back physically.",
                out failureCode,
                out failureReason);
        }
        if (!TryResolveReceiptBatch(
                receipt,
                allowAcknowledged: false,
                out WorldItemStackRecord[] records,
                out _,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (!repository.TryRemoveBatchAtomically(records, out string repositoryFailure))
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.RepositoryTransactionFailed,
                repositoryFailure,
                out failureCode,
                out failureReason);
        }
        return true;
    }

    public bool TryAcknowledgePublishedBatch(
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
        failureReason = string.Empty;
        if (!admission.TryGetPlannedOutputReceipt(
                receipt.AdmissionTokenId,
                out FacilityBufferPlannedOutputReceipt committed)
            || !string.Equals(
                committed.BatchCommitId,
                receipt.BatchCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                committed.PlannedOutputFingerprint,
                receipt.PlannedOutputFingerprint,
                StringComparison.Ordinal))
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.InvalidToken,
                $"Batch '{receipt.BatchCommitId}' cannot be acknowledged before admission commit.",
                out failureCode,
                out failureReason);
        }
        if (!TryResolveReceiptBatch(
                receipt,
                allowAcknowledged: true,
                out WorldItemStackRecord[] records,
                out bool acknowledged,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (acknowledged)
            return true;

        return TryConvertPendingMarkersToProvenance(
            records,
            receipt.BatchCommitId,
            out failureCode,
            out failureReason);
    }

    public bool TryRollbackRestoreCandidate(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        if (!TryResolveRestoreCandidate(
                candidate,
                allowAcknowledged: false,
                out WorldItemStackRecord[] records,
                out _,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (!repository.TryRemoveBatchAtomically(records, out string repositoryFailure))
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.RepositoryTransactionFailed,
                repositoryFailure,
                out failureCode,
                out failureReason);
        }
        return true;
    }

    public bool TryAcknowledgeRestoreCandidate(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        if (!TryResolveRestoreCandidate(
                candidate,
                allowAcknowledged: true,
                out WorldItemStackRecord[] records,
                out bool acknowledged,
                out failureCode,
                out failureReason))
        {
            return false;
        }
        if (acknowledged)
            return true;

        return TryConvertPendingMarkersToProvenance(
            records,
            candidate.BatchCommitId,
            out failureCode,
            out failureReason);
    }

    public bool TryCapturePendingBatch(
        string batchCommitId,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
        failureReason = string.Empty;
        if (!FacilityBufferPlannedOutputRestoreCandidateFactory.TryCaptureBatch(
                repository.Records,
                batchCommitId,
                massQuery,
                allowAcknowledged: false,
                out candidate,
                out _,
                out _,
                out failureReason))
        {
            failureCode =
                FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict;
            return false;
        }
        return true;
    }

    private bool TryConvertPendingMarkersToProvenance(
        IReadOnlyList<WorldItemStackRecord> records,
        string batchCommitId,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
        failureReason = string.Empty;

        Dictionary<string, IReadOnlyList<ItemInstanceComponentSaveData>>
            replacements = new(StringComparer.Ordinal);
        foreach (WorldItemStackRecord record in records)
        {
            if (!PlannedOutputPublicationComponentCodec.TryRead(
                    record.components,
                    out PlannedOutputPublicationMetadata metadata)
                || metadata.Acknowledged)
            {
                return Fail(
                    FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict,
                    $"Batch '{batchCommitId}' acknowledgement marker changed.",
                    out failureCode,
                    out failureReason);
            }
            List<ItemInstanceComponentSaveData> next = record.components
                .Where(component => component != null
                    && !PlannedOutputPublicationComponentCodec.IsAnyMarker(component))
                .Select(component => component.Clone())
                .ToList();
            next.Add(PlannedOutputPublicationComponentCodec.CreateProvenance(metadata));
            replacements.Add(record.stackId, next);
        }
        if (!repository.TryReplaceBatchComponentsAtomically(
                replacements,
                out string repositoryFailure))
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.RepositoryTransactionFailed,
                repositoryFailure,
                out failureCode,
                out failureReason);
        }
        return true;
    }

    private bool TryResolveRestoreCandidate(
        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
        bool allowAcknowledged,
        out WorldItemStackRecord[] records,
        out bool acknowledged,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        records = Array.Empty<WorldItemStackRecord>();
        acknowledged = false;
        failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
        failureReason = string.Empty;
        if (candidate == null
            || candidate.Stacks.Count == 0
            || !FacilityBufferPlannedOutputRestoreCandidateFactory.TryCaptureBatch(
                repository.Records,
                candidate.BatchCommitId,
                massQuery,
                allowAcknowledged,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot live,
                out records,
                out acknowledged,
                out failureReason))
        {
            failureCode =
                FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict;
            if (string.IsNullOrEmpty(failureReason))
                failureReason = "planned-output-restore-candidate-invalid";
            return false;
        }

        FacilityBufferPlannedOutputRestoreStackSnapshot[] expected = candidate.Stacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackOrdinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferPlannedOutputRestoreStackSnapshot[] actual = live.Stacks
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.StackOrdinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (!string.Equals(candidate.BatchCommitId, live.BatchCommitId, StringComparison.Ordinal)
            || !string.Equals(candidate.OutcomeFingerprint, live.OutcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(candidate.PlannedOutputFingerprint, live.PlannedOutputFingerprint, StringComparison.Ordinal)
            || candidate.TotalQuantity != live.TotalQuantity
            || candidate.TotalMassGrams != live.TotalMassGrams
            || expected.Length != actual.Length)
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict,
                $"Batch '{candidate.BatchCommitId}' restore candidate header mismatched.",
                out failureCode,
                out failureReason);
        }
        for (int index = 0; index < expected.Length; index++)
        {
            FacilityBufferPlannedOutputRestoreStackSnapshot left = expected[index];
            FacilityBufferPlannedOutputRestoreStackSnapshot right = actual[index];
            if (!string.Equals(left.BatchCommitId, right.BatchCommitId, StringComparison.Ordinal)
                || !string.Equals(left.OutcomeFingerprint, right.OutcomeFingerprint, StringComparison.Ordinal)
                || !string.Equals(left.PlannedOutputFingerprint, right.PlannedOutputFingerprint, StringComparison.Ordinal)
                || !string.Equals(left.OutputLineId, right.OutputLineId, StringComparison.Ordinal)
                || left.StackOrdinal != right.StackOrdinal
                || !string.Equals(left.StackId, right.StackId, StringComparison.Ordinal)
                || !string.Equals(left.ItemId, right.ItemId, StringComparison.Ordinal)
                || left.Quantity != right.Quantity
                || left.MassGrams != right.MassGrams
                || !string.Equals(left.ComponentSignature, right.ComponentSignature, StringComparison.Ordinal)
                || left.State != right.State
                || left.Position != right.Position
                || !string.Equals(left.DestinationId, right.DestinationId, StringComparison.Ordinal))
            {
                return Fail(
                    FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict,
                    $"Batch '{candidate.BatchCommitId}' restore candidate stack set mismatched.",
                    out failureCode,
                    out failureReason);
            }
        }
        return true;
    }

    private bool TryResolveReceiptBatch(
        FacilityBufferPlannedOutputPublicationReceipt receipt,
        bool allowAcknowledged,
        out WorldItemStackRecord[] records,
        out bool acknowledged,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        records = Array.Empty<WorldItemStackRecord>();
        acknowledged = false;
        failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
        failureReason = string.Empty;
        if (!FacilityBufferPlannedOutputRestoreCandidateFactory.TryCaptureBatch(
                repository.Records,
                receipt.BatchCommitId,
                massQuery,
                allowAcknowledged,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                out records,
                out acknowledged,
                out failureReason))
        {
            failureCode =
                FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict;
            return false;
        }
        FacilityBufferPublishedOutputStackReceipt[] expected = receipt.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferPlannedOutputRestoreStackSnapshot[] actual = batch.Stacks
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (!string.Equals(batch.BatchCommitId, receipt.BatchCommitId, StringComparison.Ordinal)
            || !string.Equals(batch.OutcomeFingerprint, receipt.OutcomeFingerprint, StringComparison.Ordinal)
            || !string.Equals(
                batch.PlannedOutputFingerprint,
                receipt.PlannedOutputFingerprint,
                StringComparison.Ordinal)
            || expected.Length == 0
            || expected.Length != actual.Length
            || records.Any(record => record.position != receipt.DropPosition
                || !string.Equals(record.destinationId, receipt.DestinationId, StringComparison.Ordinal)))
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict,
                $"Batch '{receipt.BatchCommitId}' receipt ownership or cardinality mismatched.",
                out failureCode,
                out failureReason);
        }
        for (int index = 0; index < expected.Length; index++)
        {
            FacilityBufferPublishedOutputStackReceipt left = expected[index];
            FacilityBufferPlannedOutputRestoreStackSnapshot right = actual[index];
            if (!string.Equals(left.StackId, right.StackId, StringComparison.Ordinal)
                || !string.Equals(left.OutputLineId, right.OutputLineId, StringComparison.Ordinal)
                || !left.ItemDefinitionId.Equals((ItemDefinitionId)right.ItemId)
                || left.Quantity != right.Quantity
                || left.MassGrams != right.MassGrams)
            {
                return Fail(
                    FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict,
                    $"Batch '{receipt.BatchCommitId}' receipt stack set mismatched.",
                    out failureCode,
                    out failureReason);
            }
        }
        return true;
    }

    private bool TryBuildExpectedStacks(
        FacilityBufferPlannedOutputToken token,
        bool allocateIdentities,
        out List<PreparedStack> prepared,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        prepared = new List<PreparedStack>();
        failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
        failureReason = string.Empty;
        try
        {
            foreach (FacilityBufferPlannedOutputSliceSnapshot line in
                     token.PlannedOutput.Slices
                         .OrderBy(value => value.OutputLineId, StringComparer.Ordinal))
            {
                DungeonItemDefinition definition = catalog.GetDefinition(
                    line.ItemDefinitionId.Value);
                if (definition == null || definition.MaxStack <= 0)
                {
                    return Fail(
                        FacilityBufferPlannedOutputPublicationFailureCode.CatalogMismatch,
                        $"Planned-output definition '{line.ItemDefinitionId.Value}' is missing.",
                        out failureCode,
                        out failureReason);
                }
                if (PhysicalItemIds.TryGetEquipmentDefinitionId(
                        line.ItemDefinitionId.Value,
                        out _)
                    || PhysicalItemIds.IsEquipmentModule(
                        line.ItemDefinitionId.Value))
                {
                    return Fail(
                        FacilityBufferPlannedOutputPublicationFailureCode.UnsupportedPreparedSubject,
                        $"Planned-output definition '{line.ItemDefinitionId.Value}' requires the equipment aggregate publisher.",
                        out failureCode,
                        out failureReason);
                }

                PhysicalItemMassSubject reconstructed =
                    PhysicalItemMassSubjectAdapter.Create(
                        massQuery,
                        line.ItemDefinitionId,
                        line.Source.Subject.ItemInstanceId,
                        line.Source.MaterializeRuntimeComponents());
                if (!SubjectsMatch(line.Source.Subject, reconstructed)
                    || (line.Source.Subject.ItemInstanceId.Length > 0
                        && line.Quantity != 1))
                {
                    return Fail(
                        FacilityBufferPlannedOutputPublicationFailureCode.UnsupportedPreparedSubject,
                        $"Planned-output line '{line.OutputLineId}' requires runtime components not present in the immutable token.",
                        out failureCode,
                        out failureReason);
                }

                int remaining = line.Quantity;
                int ordinal = 0;
                long lineMass = 0L;
                while (remaining > 0)
                {
                    int quantity = Math.Min(remaining, definition.MaxStack);
                    PhysicalMassGrams mass = massQuery.GetQuantityMass(
                        line.ItemDefinitionId,
                        reconstructed,
                        quantity);
                    lineMass = checked(lineMass + mass.Value);
                    string stackId = allocateIdentities
                        ? repository.AllocateStackId()
                        : string.Empty;
                    string instanceId = line.Source.Subject.ItemInstanceId;
                    if (allocateIdentities
                        && instanceId.Length == 0
                        && definition.MaxStack == 1)
                    {
                        instanceId = repository.AllocateItemInstanceId();
                    }
                    List<ItemInstanceComponentSaveData> components =
                        line.Source.MaterializeRuntimeComponents()
                            .Select(component => component.Clone())
                            .ToList();
                    if (components.Any(
                            PlannedOutputPublicationComponentCodec.IsAnyMarker))
                    {
                        return Fail(
                            FacilityBufferPlannedOutputPublicationFailureCode.UnsupportedPreparedSubject,
                            $"Planned-output line '{line.OutputLineId}' contains reserved publication metadata.",
                            out failureCode,
                            out failureReason);
                    }
                    WorldItemStackRecord record = new()
                    {
                        stackId = stackId,
                        itemInstanceId = instanceId,
                        itemId = line.ItemDefinitionId.Value,
                        quantity = quantity,
                        state = WorldItemStackState.FacilityOutputBuffer,
                        position = token.Request.DropPosition,
                        destinationId = token.Request.DestinationId,
                        components = components
                    };
                    prepared.Add(new PreparedStack(
                        line.OutputLineId,
                        ordinal,
                        mass,
                        record,
                        line.Source.PreparedComponentFingerprint));
                    remaining -= quantity;
                    ordinal++;
                }
                if (lineMass != line.ExactMassGrams)
                {
                    return Fail(
                        FacilityBufferPlannedOutputPublicationFailureCode.PhysicalMassMismatch,
                        $"Planned-output line '{line.OutputLineId}' split mass changed.",
                        out failureCode,
                        out failureReason);
                }
            }
            AttachPublicationComponents(token, prepared);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.CatalogMismatch,
                exception.Message,
                out failureCode,
                out failureReason);
        }
    }

    private static void AttachPublicationComponents(
        FacilityBufferPlannedOutputToken token,
        IReadOnlyList<PreparedStack> prepared)
    {
        int batchStackCount = prepared.Count;
        int batchQuantity = prepared.Sum(value => value.Record.quantity);
        long batchMassGrams = prepared.Sum(value => value.Mass.Value);
        foreach (IGrouping<string, PreparedStack> line in prepared.GroupBy(
                     value => value.OutputLineId,
                     StringComparer.Ordinal))
        {
            PreparedStack[] lineStacks = line.OrderBy(value => value.Ordinal).ToArray();
            int lineQuantity = lineStacks.Sum(value => value.Record.quantity);
            long lineMassGrams = lineStacks.Sum(value => value.Mass.Value);
            foreach (PreparedStack stack in lineStacks)
            {
                string componentSignature = CreateRuntimeComponentSignature(
                    stack.Record.components);
                stack.Record.components.Add(
                    PlannedOutputPublicationComponentCodec.CreatePublication(
                        token.Request.BatchCommitId,
                        token.Request.OutcomeFingerprint,
                        token.PlannedOutput.Fingerprint,
                        stack.OutputLineId,
                        stack.Ordinal,
                        batchStackCount,
                        batchQuantity,
                        batchMassGrams,
                        lineStacks.Length,
                        lineQuantity,
                        lineMassGrams,
                        stack.Record.itemId,
                        stack.Record.quantity,
                        stack.Mass.Value,
                        componentSignature,
                        stack.PreparedComponentFingerprint));
            }
        }
    }

    private bool TryReplayExisting(
        FacilityBufferPlannedOutputToken token,
        IReadOnlyList<PreparedStack> expected,
        IReadOnlyList<WorldItemStackRecord> existing,
        out FacilityBufferPlannedOutputPublicationReceipt receipt,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        receipt = default;
        failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
        failureReason = string.Empty;
        if (!FacilityBufferPlannedOutputRestoreCandidateFactory.TryCaptureBatch(
                existing,
                token.Request.BatchCommitId,
                massQuery,
                allowAcknowledged: true,
                out _,
                out _,
                out _,
                out string candidateFailure))
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict,
                candidateFailure,
                out failureCode,
                out failureReason);
        }
        Dictionary<string, PreparedStack> expectedByKey = expected.ToDictionary(
            value => CreateLineOrdinalKey(value.OutputLineId, value.Ordinal),
            StringComparer.Ordinal);
        List<PreparedStack> replay = new();
        foreach (WorldItemStackRecord record in existing)
        {
            if (!PlannedOutputPublicationComponentCodec.TryRead(
                    record.components,
                    out PlannedOutputPublicationMetadata metadata)
                || !string.Equals(metadata.BatchCommitId, token.Request.BatchCommitId, StringComparison.Ordinal)
                || !string.Equals(metadata.OutcomeFingerprint, token.Request.OutcomeFingerprint, StringComparison.Ordinal)
                || !string.Equals(metadata.PlannedOutputFingerprint, token.PlannedOutput.Fingerprint, StringComparison.Ordinal)
                || !expectedByKey.TryGetValue(
                    CreateLineOrdinalKey(metadata.OutputLineId, metadata.StackOrdinal),
                    out PreparedStack expectedStack)
                || !string.Equals(
                    metadata.PreparedComponentFingerprint,
                    expectedStack.PreparedComponentFingerprint,
                    StringComparison.Ordinal)
                || record.state != WorldItemStackState.FacilityOutputBuffer
                || record.position != token.Request.DropPosition
                || !string.Equals(record.destinationId, token.Request.DestinationId, StringComparison.Ordinal)
                || !string.Equals(record.itemId, expectedStack.Record.itemId, StringComparison.Ordinal)
                || record.quantity != expectedStack.Record.quantity
                || !RuntimeComponentsMatch(
                    expectedStack.Record.components,
                    record.components))
            {
                return Fail(
                    FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict,
                    $"Batch commit '{token.Request.BatchCommitId}' has a conflicting or partial publication.",
                    out failureCode,
                    out failureReason);
            }
            PhysicalItemMassSubject actualSubject = PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                (ItemDefinitionId)record.itemId,
                record.itemInstanceId,
                record.components);
            PhysicalMassGrams actualMass = massQuery.GetQuantityMass(
                (ItemDefinitionId)record.itemId,
                actualSubject,
                record.quantity);
            if (actualMass.Value != expectedStack.Mass.Value)
            {
                return Fail(
                    FacilityBufferPlannedOutputPublicationFailureCode.PhysicalMassMismatch,
                    $"Batch commit '{token.Request.BatchCommitId}' replay mass mismatched.",
                    out failureCode,
                    out failureReason);
            }
            replay.Add(new PreparedStack(
                metadata.OutputLineId,
                metadata.StackOrdinal,
                actualMass,
                record,
                metadata.PreparedComponentFingerprint));
            expectedByKey.Remove(CreateLineOrdinalKey(
                metadata.OutputLineId,
                metadata.StackOrdinal));
        }
        if (expectedByKey.Count != 0 || replay.Count != expected.Count)
        {
            return Fail(
                FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict,
                $"Batch commit '{token.Request.BatchCommitId}' is only partially published.",
                out failureCode,
                out failureReason);
        }
        receipt = CreateReceipt(token, replay);
        return true;
    }

    private static FacilityBufferPlannedOutputPublicationReceipt CreateReceipt(
        FacilityBufferPlannedOutputToken token,
        IEnumerable<PreparedStack> prepared) => new(
        token.TokenId,
        token.Request.BatchCommitId,
        token.Request.OutcomeFingerprint,
        token.Request.DestinationId,
        token.Request.DropPosition,
        token.Request.ExpectedOwnerDomain,
        token.Request.ExpectedOwnerOperationId,
        token.Request.ExpectedOwnerFacilityId,
        token.Request.ExpectedCapacityRevision,
        token.PlannedOutput.Fingerprint,
        prepared
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.Ordinal)
            .Select(value => new FacilityBufferPublishedOutputStackReceipt(
                value.Record.stackId,
                value.OutputLineId,
                (ItemDefinitionId)value.Record.itemId,
                value.Record.quantity,
                value.Mass))
            .ToArray());

    private static bool SubjectsMatch(
        PhysicalItemMassSubject expected,
        PhysicalItemMassSubject actual) =>
        expected != null
        && actual != null
        && expected.ItemId.Equals(actual.ItemId)
        && string.Equals(expected.ItemInstanceId, actual.ItemInstanceId, StringComparison.Ordinal)
        && expected.Kind == actual.Kind
        && string.Equals(
            expected.ComponentFingerprint,
            actual.ComponentFingerprint,
            StringComparison.Ordinal);

    private static bool RuntimeComponentsMatch(
        IEnumerable<ItemInstanceComponentSaveData> expected,
        IEnumerable<ItemInstanceComponentSaveData> actual)
    {
        string[] left = (expected ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null
                && !PlannedOutputPublicationComponentCodec.IsAnyMarker(component))
            .Select(CreateExactComponentSignature)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] right = (actual ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null
                && !PlannedOutputPublicationComponentCodec.IsAnyMarker(component))
            .Select(CreateExactComponentSignature)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return left.SequenceEqual(right, StringComparer.Ordinal);
    }

    private static string CreateExactComponentSignature(
        ItemInstanceComponentSaveData component) =>
        $"{(component.affectsStacking ? 1 : 0)}:{component.ToCanonicalString()}";

    internal static string CreateRuntimeComponentSignature(
        IEnumerable<ItemInstanceComponentSaveData> components) => string.Join(
        "|",
        (components ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Where(component => component != null
            && !PlannedOutputPublicationComponentCodec.IsAnyMarker(component))
        .Select(CreateExactComponentSignature)
        .OrderBy(value => value, StringComparer.Ordinal));

    private static string CreateLineOrdinalKey(string lineId, int ordinal) =>
        $"{lineId?.Length ?? 0}:{lineId}:{ordinal.ToString(CultureInfo.InvariantCulture)}";

    private static bool Fail(
        FacilityBufferPlannedOutputPublicationFailureCode code,
        string reason,
        out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
        out string failureReason)
    {
        failureCode = code;
        failureReason = reason ?? string.Empty;
        return false;
    }

    private readonly struct PreparedStack
    {
        internal PreparedStack(
            string outputLineId,
            int ordinal,
            PhysicalMassGrams mass,
            WorldItemStackRecord record,
            string preparedComponentFingerprint)
        {
            OutputLineId = outputLineId;
            Ordinal = ordinal;
            Mass = mass;
            Record = record;
            PreparedComponentFingerprint = preparedComponentFingerprint
                ?? string.Empty;
        }

        internal string OutputLineId { get; }
        internal int Ordinal { get; }
        internal PhysicalMassGrams Mass { get; }
        internal WorldItemStackRecord Record { get; }
        internal string PreparedComponentFingerprint { get; }
    }
}

internal static class FacilityBufferPlannedOutputRestoreCandidateFactory
{
    internal static IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
        CapturePendingBatches(
            IEnumerable<WorldItemStackRecord> records,
            IPhysicalItemMassQuery massQuery)
    {
        WorldItemStackRecord[] all = (records ?? Array.Empty<WorldItemStackRecord>())
            .Where(record => record != null)
            .ToArray();
        foreach (WorldItemStackRecord malformed in all.Where(record =>
                     (record.components ?? new List<ItemInstanceComponentSaveData>())
                     .Any(PlannedOutputPublicationComponentCodec.IsAnyMarker)
                     && !PlannedOutputPublicationComponentCodec.TryRead(
                         record.components,
                         out _)))
        {
            throw new InvalidOperationException(
                $"Malformed planned-output publication marker on '{malformed.stackId}'.");
        }

        string[] batchIds = all
            .SelectMany(record => (record.components
                    ?? new List<ItemInstanceComponentSaveData>())
                .Where(component => component != null
                    && string.Equals(
                        component.componentTypeId,
                        PlannedOutputPublicationComponentCodec
                            .PublicationComponentTypeId,
                        StringComparison.Ordinal))
                .SelectMany(component => component.values
                    ?? new List<ItemStateValueSaveData>())
                .Where(value => value != null
                    && value.kind == ItemStateValueKind.String
                    && string.Equals(value.key, "batch-commit-id", StringComparison.Ordinal))
                .Select(value => value.stringValue))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        List<FacilityBufferPlannedOutputRestoreBatchSnapshot> result = new();
        foreach (string batchId in batchIds)
        {
            if (!TryCaptureBatch(
                    all,
                    batchId,
                    massQuery,
                    allowAcknowledged: false,
                    out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
                    out _,
                    out _,
                    out string failureReason))
            {
                throw new InvalidOperationException(failureReason);
            }
            result.Add(batch);
        }
        return Array.AsReadOnly(result.ToArray());
    }

    internal static bool TryCaptureBatch(
        IEnumerable<WorldItemStackRecord> source,
        string batchCommitId,
        IPhysicalItemMassQuery massQuery,
        bool allowAcknowledged,
        out FacilityBufferPlannedOutputRestoreBatchSnapshot batch,
        out WorldItemStackRecord[] records,
        out bool acknowledged,
        out string failureReason)
    {
        batch = null;
        records = Array.Empty<WorldItemStackRecord>();
        acknowledged = false;
        failureReason = string.Empty;
        if (massQuery == null || string.IsNullOrWhiteSpace(batchCommitId))
        {
            failureReason = "planned-output-restore-query-invalid";
            return false;
        }
        records = (source ?? Array.Empty<WorldItemStackRecord>())
            .Where(record => record != null
                && PlannedOutputPublicationComponentCodec.HasBatchCommitId(
                    record.components,
                    batchCommitId))
            .OrderBy(record => record.stackId, StringComparer.Ordinal)
            .ToArray();
        if (records.Length == 0)
        {
            failureReason = $"planned-output-batch-missing:{batchCommitId}";
            return false;
        }

        List<(WorldItemStackRecord Record, PlannedOutputPublicationMetadata Metadata,
            long ActualMass, string ActualSignature)> parsed = new();
        foreach (WorldItemStackRecord record in records)
        {
            if (!PlannedOutputPublicationComponentCodec.TryRead(
                    record.components,
                    out PlannedOutputPublicationMetadata metadata)
                || !string.Equals(metadata.BatchCommitId, batchCommitId, StringComparison.Ordinal))
            {
                failureReason = $"planned-output-marker-invalid:{batchCommitId}:{record.stackId}";
                return false;
            }
            try
            {
                PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                    massQuery,
                    (ItemDefinitionId)record.itemId,
                    record.itemInstanceId,
                    record.components);
                long actualMass = massQuery.GetQuantityMass(
                    (ItemDefinitionId)record.itemId,
                    subject,
                    record.quantity).Value;
                string signature =
                    FacilityBufferPlannedOutputPublicationService
                    .CreateRuntimeComponentSignature(record.components);
                if (!string.Equals(metadata.ItemId, record.itemId, StringComparison.Ordinal)
                    || metadata.Quantity != record.quantity
                    || metadata.MassGrams != actualMass
                    || !string.Equals(
                        metadata.ComponentSignature,
                        signature,
                        StringComparison.Ordinal)
                    || !metadata.Acknowledged
                        && record.state != WorldItemStackState.FacilityOutputBuffer)
                {
                    failureReason = $"planned-output-physical-mismatch:{batchCommitId}:{record.stackId}";
                    return false;
                }
                parsed.Add((record, metadata, actualMass, signature));
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or OverflowException)
            {
                failureReason = $"planned-output-mass-invalid:{batchCommitId}:{record.stackId}:{exception.Message}";
                return false;
            }
        }

        bool batchAcknowledged = parsed[0].Metadata.Acknowledged;
        acknowledged = batchAcknowledged;
        PlannedOutputPublicationMetadata header = parsed[0].Metadata;
        if (parsed.Any(value =>
                value.Metadata.Acknowledged != batchAcknowledged)
            || batchAcknowledged && !allowAcknowledged
            || parsed.Any(value =>
                !string.Equals(value.Metadata.OutcomeFingerprint, header.OutcomeFingerprint, StringComparison.Ordinal)
                || !string.Equals(value.Metadata.PlannedOutputFingerprint, header.PlannedOutputFingerprint, StringComparison.Ordinal)
                || value.Metadata.BatchStackCount != header.BatchStackCount
                || value.Metadata.BatchQuantity != header.BatchQuantity
                || value.Metadata.BatchMassGrams != header.BatchMassGrams)
            || records.Length != header.BatchStackCount
            || records.Sum(record => record.quantity) != header.BatchQuantity
            || parsed.Sum(value => value.ActualMass) != header.BatchMassGrams)
        {
            failureReason = $"planned-output-batch-partial-or-conflicting:{batchCommitId}";
            return false;
        }

        foreach (IGrouping<string, (WorldItemStackRecord Record,
                     PlannedOutputPublicationMetadata Metadata,
                     long ActualMass,
                     string ActualSignature)> line in parsed.GroupBy(
                     value => value.Metadata.OutputLineId,
                     StringComparer.Ordinal))
        {
            var ordered = line.OrderBy(value => value.Metadata.StackOrdinal).ToArray();
            PlannedOutputPublicationMetadata lineHeader = ordered[0].Metadata;
            if (ordered.Length != lineHeader.LineStackCount
                || ordered.Select(value => value.Metadata.StackOrdinal)
                    .Where((ordinal, index) => ordinal != index).Any()
                || ordered.Sum(value => value.Record.quantity) != lineHeader.LineQuantity
                || ordered.Sum(value => value.ActualMass) != lineHeader.LineMassGrams
                || ordered.Any(value =>
                    value.Metadata.LineStackCount != lineHeader.LineStackCount
                    || value.Metadata.LineQuantity != lineHeader.LineQuantity
                    || value.Metadata.LineMassGrams != lineHeader.LineMassGrams))
            {
                failureReason = $"planned-output-line-partial-or-conflicting:{batchCommitId}:{line.Key}";
                return false;
            }
        }

        FacilityBufferPlannedOutputRestoreStackSnapshot[] snapshots = parsed
            .OrderBy(value => value.Metadata.OutputLineId, StringComparer.Ordinal)
            .ThenBy(value => value.Metadata.StackOrdinal)
            .Select(value => new FacilityBufferPlannedOutputRestoreStackSnapshot(
                value.Metadata.BatchCommitId,
                value.Metadata.OutcomeFingerprint,
                value.Metadata.PlannedOutputFingerprint,
                value.Metadata.OutputLineId,
                value.Metadata.StackOrdinal,
                value.Record.stackId,
                value.Record.itemId,
                value.Record.quantity,
                value.ActualMass,
                value.Metadata.ComponentSignature,
                value.Record.state,
                value.Record.position,
                value.Record.destinationId))
            .ToArray();
        batch = new FacilityBufferPlannedOutputRestoreBatchSnapshot(
            header.BatchCommitId,
            header.OutcomeFingerprint,
            header.PlannedOutputFingerprint,
            header.BatchQuantity,
            header.BatchMassGrams,
            snapshots);
        return true;
    }
}

internal readonly struct PlannedOutputPublicationMetadata
{
    internal PlannedOutputPublicationMetadata(
        string batchCommitId,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        string outputLineId,
        int stackOrdinal,
        int batchStackCount,
        int batchQuantity,
        long batchMassGrams,
        int lineStackCount,
        int lineQuantity,
        long lineMassGrams,
        string itemId,
        int quantity,
        long massGrams,
        string componentSignature,
        string preparedComponentFingerprint,
        bool acknowledged)
    {
        BatchCommitId = batchCommitId;
        OutcomeFingerprint = outcomeFingerprint;
        PlannedOutputFingerprint = plannedOutputFingerprint;
        OutputLineId = outputLineId;
        StackOrdinal = stackOrdinal;
        BatchStackCount = batchStackCount;
        BatchQuantity = batchQuantity;
        BatchMassGrams = batchMassGrams;
        LineStackCount = lineStackCount;
        LineQuantity = lineQuantity;
        LineMassGrams = lineMassGrams;
        ItemId = itemId;
        Quantity = quantity;
        MassGrams = massGrams;
        ComponentSignature = componentSignature;
        PreparedComponentFingerprint = preparedComponentFingerprint;
        Acknowledged = acknowledged;
    }

    internal string BatchCommitId { get; }
    internal string OutcomeFingerprint { get; }
    internal string PlannedOutputFingerprint { get; }
    internal string OutputLineId { get; }
    internal int StackOrdinal { get; }
    internal int BatchStackCount { get; }
    internal int BatchQuantity { get; }
    internal long BatchMassGrams { get; }
    internal int LineStackCount { get; }
    internal int LineQuantity { get; }
    internal long LineMassGrams { get; }
    internal string ItemId { get; }
    internal int Quantity { get; }
    internal long MassGrams { get; }
    internal string ComponentSignature { get; }
    internal string PreparedComponentFingerprint { get; }
    internal bool Acknowledged { get; }
}

internal static class PlannedOutputPublicationComponentCodec
{
    internal const string PublicationComponentTypeId =
        "item-state:facility-buffer-planned-output-publication";
    internal const string ProvenanceComponentTypeId =
        "item-state:facility-buffer-planned-output-provenance";
    private const string BatchCommitIdKey = "batch-commit-id";
    private const string OutcomeFingerprintKey = "outcome-fingerprint";
    private const string PlannedOutputFingerprintKey = "planned-output-fingerprint";
    private const string OutputLineIdKey = "output-line-id";
    private const string StackOrdinalKey = "stack-ordinal";
    private const string BatchStackCountKey = "batch-stack-count";
    private const string BatchQuantityKey = "batch-quantity";
    private const string BatchMassGramsKey = "batch-mass-grams";
    private const string LineStackCountKey = "line-stack-count";
    private const string LineQuantityKey = "line-quantity";
    private const string LineMassGramsKey = "line-mass-grams";
    private const string ItemIdKey = "item-id";
    private const string QuantityKey = "quantity";
    private const string MassGramsKey = "mass-grams";
    private const string ComponentSignatureKey = "component-signature";
    private const string PreparedComponentFingerprintKey =
        "prepared-component-fingerprint";

    internal static ItemInstanceComponentSaveData CreatePublication(
        string batchCommitId,
        string outcomeFingerprint,
        string plannedOutputFingerprint,
        string outputLineId,
        int stackOrdinal,
        int batchStackCount,
        int batchQuantity,
        long batchMassGrams,
        int lineStackCount,
        int lineQuantity,
        long lineMassGrams,
        string itemId,
        int quantity,
        long massGrams,
        string componentSignature,
        string preparedComponentFingerprint) => Create(
        new PlannedOutputPublicationMetadata(
            batchCommitId,
            outcomeFingerprint,
            plannedOutputFingerprint,
            outputLineId,
            stackOrdinal,
            batchStackCount,
            batchQuantity,
            batchMassGrams,
            lineStackCount,
            lineQuantity,
            lineMassGrams,
            itemId,
            quantity,
            massGrams,
            componentSignature,
            preparedComponentFingerprint,
            acknowledged: false));

    internal static ItemInstanceComponentSaveData CreateProvenance(
        PlannedOutputPublicationMetadata metadata) => Create(
        new PlannedOutputPublicationMetadata(
            metadata.BatchCommitId,
            metadata.OutcomeFingerprint,
            metadata.PlannedOutputFingerprint,
            metadata.OutputLineId,
            metadata.StackOrdinal,
            metadata.BatchStackCount,
            metadata.BatchQuantity,
            metadata.BatchMassGrams,
            metadata.LineStackCount,
            metadata.LineQuantity,
            metadata.LineMassGrams,
            metadata.ItemId,
            metadata.Quantity,
            metadata.MassGrams,
            metadata.ComponentSignature,
            metadata.PreparedComponentFingerprint,
            acknowledged: true));

    private static ItemInstanceComponentSaveData Create(
        PlannedOutputPublicationMetadata metadata) => new()
    {
        componentTypeId = metadata.Acknowledged
            ? ProvenanceComponentTypeId
            : PublicationComponentTypeId,
        schemaVersion = 2,
        affectsStacking = !metadata.Acknowledged,
        values = new List<ItemStateValueSaveData>
        {
            StringField(BatchCommitIdKey, metadata.BatchCommitId),
            StringField(OutcomeFingerprintKey, metadata.OutcomeFingerprint),
            StringField(PlannedOutputFingerprintKey, metadata.PlannedOutputFingerprint),
            StringField(OutputLineIdKey, metadata.OutputLineId),
            IntegerField(StackOrdinalKey, metadata.StackOrdinal),
            IntegerField(BatchStackCountKey, metadata.BatchStackCount),
            IntegerField(BatchQuantityKey, metadata.BatchQuantity),
            IntegerField(BatchMassGramsKey, metadata.BatchMassGrams),
            IntegerField(LineStackCountKey, metadata.LineStackCount),
            IntegerField(LineQuantityKey, metadata.LineQuantity),
            IntegerField(LineMassGramsKey, metadata.LineMassGrams),
            StringField(ItemIdKey, metadata.ItemId),
            IntegerField(QuantityKey, metadata.Quantity),
            IntegerField(MassGramsKey, metadata.MassGrams),
            StringField(ComponentSignatureKey, metadata.ComponentSignature),
            StringField(
                PreparedComponentFingerprintKey,
                metadata.PreparedComponentFingerprint)
        }
    };

    internal static bool IsAnyMarker(ItemInstanceComponentSaveData component) =>
        component != null
        && (string.Equals(
                component.componentTypeId,
                PublicationComponentTypeId,
                StringComparison.Ordinal)
            || string.Equals(
                component.componentTypeId,
                ProvenanceComponentTypeId,
                StringComparison.Ordinal));

    internal static bool HasBatchCommitId(
        IEnumerable<ItemInstanceComponentSaveData> components,
        string batchCommitId) => (components
            ?? Array.Empty<ItemInstanceComponentSaveData>())
        .Where(IsAnyMarker)
        .SelectMany(component => component.values ?? new List<ItemStateValueSaveData>())
        .Any(value => value != null
            && value.kind == ItemStateValueKind.String
            && string.Equals(value.key, BatchCommitIdKey, StringComparison.Ordinal)
            && string.Equals(value.stringValue, batchCommitId, StringComparison.Ordinal));

    internal static bool TryRead(
        IEnumerable<ItemInstanceComponentSaveData> components,
        out PlannedOutputPublicationMetadata metadata)
    {
        metadata = default;
        ItemInstanceComponentSaveData[] matches = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(IsAnyMarker)
            .ToArray();
        bool acknowledged = matches.Length == 1
            && string.Equals(
                matches[0].componentTypeId,
                ProvenanceComponentTypeId,
                StringComparison.Ordinal);
        if (matches.Length != 1
            || matches[0].schemaVersion != 2
            || matches[0].affectsStacking == acknowledged)
            return false;
        List<ItemStateValueSaveData> values = matches[0].values
            ?? new List<ItemStateValueSaveData>();
        if (values.Count != 16)
            return false;
        if (!TryString(values, BatchCommitIdKey, out string batchCommitId)
            || !TryString(values, OutcomeFingerprintKey, out string outcomeFingerprint)
            || !TryString(values, PlannedOutputFingerprintKey, out string plannedFingerprint)
            || !TryString(values, OutputLineIdKey, out string lineId)
            || !TryString(values, ItemIdKey, out string itemId)
            || !TryStringAllowEmpty(
                values,
                ComponentSignatureKey,
                out string componentSignature)
            || !TryStringAllowEmpty(
                values,
                PreparedComponentFingerprintKey,
                out string preparedComponentFingerprint)
            || !TryNonNegativeInt(values, StackOrdinalKey, out int ordinal)
            || !TryPositiveInt(values, BatchStackCountKey, out int batchStackCount)
            || !TryPositiveInt(values, BatchQuantityKey, out int batchQuantity)
            || !TryPositiveLong(values, BatchMassGramsKey, out long batchMassGrams)
            || !TryPositiveInt(values, LineStackCountKey, out int lineStackCount)
            || !TryPositiveInt(values, LineQuantityKey, out int lineQuantity)
            || !TryPositiveLong(values, LineMassGramsKey, out long lineMassGrams)
            || !TryPositiveInt(values, QuantityKey, out int quantity)
            || !TryPositiveLong(values, MassGramsKey, out long massGrams))
            return false;
        metadata = new PlannedOutputPublicationMetadata(
            batchCommitId,
            outcomeFingerprint,
            plannedFingerprint,
            lineId,
            ordinal,
            batchStackCount,
            batchQuantity,
            batchMassGrams,
            lineStackCount,
            lineQuantity,
            lineMassGrams,
            itemId,
            quantity,
            massGrams,
            componentSignature,
            preparedComponentFingerprint,
            acknowledged);
        return true;
    }

    private static bool TryString(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out string result)
    {
        ItemStateValueSaveData[] matches = values.Where(value => value != null
                && value.kind == ItemStateValueKind.String
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = matches.Length == 1 ? matches[0].stringValue ?? string.Empty : string.Empty;
        return matches.Length == 1
            && result.Length > 0
            && string.Equals(result, result.Trim(), StringComparison.Ordinal);
    }

    private static bool TryStringAllowEmpty(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out string result)
    {
        ItemStateValueSaveData[] matches = values.Where(value => value != null
                && value.kind == ItemStateValueKind.String
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = matches.Length == 1 ? matches[0].stringValue ?? string.Empty : string.Empty;
        return matches.Length == 1
            && string.Equals(result, result.Trim(), StringComparison.Ordinal);
    }

    private static bool TryNonNegativeInt(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out int result)
    {
        result = 0;
        if (!TryInteger(values, key, out long raw)
            || raw < 0L
            || raw > int.MaxValue)
            return false;
        result = (int)raw;
        return true;
    }

    private static bool TryPositiveInt(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out int result) => TryNonNegativeInt(values, key, out result)
        && result > 0;

    private static bool TryPositiveLong(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out long result) => TryInteger(values, key, out result)
        && result > 0L;

    private static bool TryInteger(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out long result)
    {
        ItemStateValueSaveData[] matches = values.Where(value => value != null
                && value.kind == ItemStateValueKind.Integer
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        result = matches.Length == 1 ? matches[0].integerValue : 0L;
        return matches.Length == 1;
    }

    private static ItemStateValueSaveData StringField(string key, string value) => new()
    {
        key = key,
        kind = ItemStateValueKind.String,
        stringValue = value
    };

    private static ItemStateValueSaveData IntegerField(string key, long value) => new()
    {
        key = key,
        kind = ItemStateValueKind.Integer,
        integerValue = value
    };
}
