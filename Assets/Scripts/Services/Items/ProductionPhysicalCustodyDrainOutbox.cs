using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Items-owned durable progress authority for a production destination drain.
/// It does not mutate stacks itself; the physical drain port records each
/// completed sub-effect here and may then publish one immutable receipt.
/// </summary>
public sealed class ProductionPhysicalCustodyDrainOutbox :
    IProductionPhysicalCustodyDrainOutbox,
    IProductionPhysicalCustodyDrainCheckpointGcPort
{
    private const string CommitPrefix =
        "production-physical-custody-drain-commit:";
    private readonly WorldItemRepository repository;
    private CheckpointGcCandidate activeCheckpointGcCandidate;

    public ProductionPhysicalCustodyDrainOutbox(WorldItemRepository repository)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
    }

    public ProductionPhysicalCustodyDrainResult TryPrepare(
        ProductionPhysicalCustodyDrainRequest request)
    {
        if (!IsValid(request, out string failure))
            return Conflict(failure);
        if (repository.TryGetPendingProductionCustodyDrain(
                request.StepOperationId,
                out ProductionPhysicalCustodyDrainSaveData existing))
        {
            return Matches(existing, request)
                ? Current(existing, ProductionPhysicalCustodyDrainStatus.Replay)
                : Conflict("production-physical-custody-drain-request-conflict");
        }

        ProductionPhysicalCustodyDrainSaveData prepared = new()
        {
            stepOperationId = request.StepOperationId,
            ownerStableId = request.OwnerStableId,
            sourceDestinationId = request.SourceDestinationId,
            ownerGridX = request.OwnerGridX,
            ownerGridY = request.OwnerGridY,
            requestFingerprint = request.RequestFingerprint,
            sourceOwnershipFingerprint = request.SourceOwnershipFingerprint,
            phase = ProductionPhysicalCustodyDrainPhase.Prepared,
            sourceStackIds = request.SourceStackIds.ToList(),
            sourceActorIds = request.SourceActorIds.ToList(),
            sourceHaulIntentOperationIds =
                request.SourceHaulIntentOperationIds.ToList(),
            inputQuantity = request.InputQuantity,
            inputMassGrams = request.InputMassGrams
        };
        repository.SetPendingProductionCustodyDrain(prepared);
        return Current(prepared, ProductionPhysicalCustodyDrainStatus.Applied);
    }

    public ProductionPhysicalCustodyDrainResult TryBeginDraining(
        string stepOperationId,
        string requestFingerprint)
    {
        if (!TryGet(stepOperationId, out ProductionPhysicalCustodyDrainSaveData value))
            return Conflict("production-physical-custody-drain-missing");
        if (!string.Equals(
                value.requestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("production-physical-custody-drain-request-conflict");
        }
        if (value.phase != ProductionPhysicalCustodyDrainPhase.Prepared)
            return Current(value, ProductionPhysicalCustodyDrainStatus.Replay);
        value.phase = ProductionPhysicalCustodyDrainPhase.ReleasingActors;
        repository.SetPendingProductionCustodyDrain(value);
        return Current(value, ProductionPhysicalCustodyDrainStatus.Applied);
    }

    public ProductionPhysicalCustodyDrainResult TryRecordActorCompleted(
        string stepOperationId,
        string actorId) => RecordProgress(
            stepOperationId,
            actorId,
            value => value.completedActorIds,
            value => value.sourceActorIds,
            ProductionPhysicalCustodyDrainPhase.ReleasingActors,
            "actor");

    public ProductionPhysicalCustodyDrainResult TryBeginReleasingIntents(
        string stepOperationId) => AdvancePhase(
            stepOperationId,
            ProductionPhysicalCustodyDrainPhase.ReleasingActors,
            ProductionPhysicalCustodyDrainPhase.ReleasingIntents,
            value => value.completedActorIds.SequenceEqual(
                value.sourceActorIds,
                StringComparer.Ordinal),
            "production-physical-custody-drain-actors-incomplete");

    public ProductionPhysicalCustodyDrainResult TryRecordHaulIntentReleased(
        string stepOperationId,
        string haulIntentOperationId) => RecordProgress(
            stepOperationId,
            haulIntentOperationId,
            value => value.releasedHaulIntentOperationIds,
            value => value.sourceHaulIntentOperationIds,
            ProductionPhysicalCustodyDrainPhase.ReleasingIntents,
            "haul-intent");

    public ProductionPhysicalCustodyDrainResult TryBeginReleasingDestination(
        string stepOperationId) => AdvancePhase(
            stepOperationId,
            ProductionPhysicalCustodyDrainPhase.ReleasingIntents,
            ProductionPhysicalCustodyDrainPhase.ReleasingDestination,
            value => value.releasedHaulIntentOperationIds.SequenceEqual(
                value.sourceHaulIntentOperationIds,
                StringComparer.Ordinal),
            "production-physical-custody-drain-intents-incomplete");

    public ProductionPhysicalCustodyDrainResult TryCommitEffect(
        string stepOperationId,
        IEnumerable<string> releasedStackIds,
        int releasedQuantity,
        long releasedMassGrams,
        string resultFingerprint)
    {
        if (!TryGet(stepOperationId, out ProductionPhysicalCustodyDrainSaveData value))
            return Conflict("production-physical-custody-drain-missing");
        if (value.phase is ProductionPhysicalCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or ProductionPhysicalCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
        {
            return value.releasedQuantity == releasedQuantity
                    && value.releasedMassGrams == releasedMassGrams
                    && string.Equals(
                        value.resultFingerprint,
                        resultFingerprint,
                        StringComparison.Ordinal)
                ? Current(value, ProductionPhysicalCustodyDrainStatus.Replay)
                : Conflict("production-physical-custody-drain-result-conflict");
        }
        if (value.phase != ProductionPhysicalCustodyDrainPhase
                .ReleasingDestination)
        {
            return Deferred(
                "production-physical-custody-drain-destination-not-releasing");
        }
        string[] released = (releasedStackIds ?? Array.Empty<string>())
            .OrderBy(identity => identity, StringComparer.Ordinal)
            .ToArray();
        if (releasedQuantity != value.inputQuantity
            || releasedMassGrams != value.inputMassGrams
            || !IsDigest(resultFingerprint)
            || !released.SequenceEqual(
                value.sourceStackIds,
                StringComparer.Ordinal)
            || !IsCanonicalUnique(released, requireNonEmpty: true))
        {
            return Deferred(
                "production-physical-custody-drain-effect-incomplete");
        }

        value.releasedStackIds = released.ToList();
        value.releasedQuantity = releasedQuantity;
        value.releasedMassGrams = releasedMassGrams;
        value.resultFingerprint = resultFingerprint;
        value.commitId = BuildCommitId(value);
        value.receiptFingerprint = BuildReceiptFingerprint(value);
        value.phase = ProductionPhysicalCustodyDrainPhase
            .EffectCommittedAwaitingOwnerAck;
        repository.SetPendingProductionCustodyDrain(value);
        return Current(value, ProductionPhysicalCustodyDrainStatus.Applied);
    }

    public ProductionPhysicalCustodyDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (!TryGet(stepOperationId, out ProductionPhysicalCustodyDrainSaveData value))
            return Conflict("production-physical-custody-drain-missing");
        if (!string.Equals(
                value.receiptFingerprint,
                receiptFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("production-physical-custody-drain-receipt-conflict");
        }
        if (value.phase == ProductionPhysicalCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
            return Current(value, ProductionPhysicalCustodyDrainStatus.Replay);
        if (value.phase != ProductionPhysicalCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck)
        {
            return Deferred(
                "production-physical-custody-drain-effect-not-committed");
        }
        value.phase = ProductionPhysicalCustodyDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
        repository.SetPendingProductionCustodyDrain(value);
        return Current(value, ProductionPhysicalCustodyDrainStatus.Applied);
    }

    public ProductionPhysicalCustodyDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (activeCheckpointGcCandidate != null)
        {
            return Deferred(
                "production-physical-custody-checkpoint-gc-transaction-active");
        }
        if (!TryGet(stepOperationId, out ProductionPhysicalCustodyDrainSaveData value))
            return new ProductionPhysicalCustodyDrainResult(
                ProductionPhysicalCustodyDrainStatus.Replay,
                string.Empty,
                receiptFingerprint,
                string.Empty);
        if (value.phase != ProductionPhysicalCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
            return Deferred("production-physical-custody-drain-not-acknowledged");
        if (!string.Equals(
                value.receiptFingerprint,
                receiptFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("production-physical-custody-drain-receipt-conflict");
        }
        repository.RemovePendingProductionCustodyDrain(stepOperationId);
        return Current(value, ProductionPhysicalCustodyDrainStatus.Applied);
    }

    public bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionPhysicalCustodyDrainSaveData> records,
        out IProductionPhysicalCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (activeCheckpointGcCandidate != null)
        {
            failureReason =
                "production-physical-custody-checkpoint-gc-already-prepared";
            return false;
        }
        ProductionPhysicalCustodyDrainSaveData[] expected = (records
                ?? Array.Empty<ProductionPhysicalCustodyDrainSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .ToArray();
        if (expected.Any(value => value == null)
            || expected.Select(value => value.stepOperationId)
                .Distinct(StringComparer.Ordinal).Count() != expected.Length)
        {
            failureReason =
                "production-physical-custody-checkpoint-gc-records-invalid";
            return false;
        }
        foreach (ProductionPhysicalCustodyDrainSaveData row in expected)
        {
            if (row.phase != ProductionPhysicalCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                || !IsDigest(row.receiptFingerprint)
                || !TryGet(row.stepOperationId,
                    out ProductionPhysicalCustodyDrainSaveData current)
                || !RowsEqual(current, row))
            {
                failureReason =
                    "production-physical-custody-checkpoint-gc-row-conflict";
                return false;
            }
        }
        activeCheckpointGcCandidate = new CheckpointGcCandidate(expected);
        candidate = activeCheckpointGcCandidate;
        return true;
    }

    public bool TryPublishCheckpointGarbageCollection(
        IProductionPhysicalCustodyDrainCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.Published)
            return true;
        foreach (ProductionPhysicalCustodyDrainSaveData row in exact.ExpectedRows)
        {
            if (!repository
                    .TryRemoveExactPendingProductionCustodyDrainForCheckpointGc(
                        row,
                        out ProductionPhysicalCustodyDrainSaveData removed))
            {
                if (exact.RemovedRows.Any(value => !repository
                        .CanRestoreExactPendingProductionCustodyDrainForCheckpointGc(
                            value)))
                {
                    throw new InvalidOperationException(
                        "production-physical-custody-checkpoint-gc-publish-rollback-conflict");
                }
                for (int index = exact.RemovedRows.Count - 1; index >= 0; index--)
                {
                    if (!repository
                            .TryRestoreExactPendingProductionCustodyDrainForCheckpointGc(
                                exact.RemovedRows[index]))
                    {
                        throw new InvalidOperationException(
                            "production-physical-custody-checkpoint-gc-publish-rollback-conflict");
                    }
                }
                exact.RemovedRows.Clear();
                failureReason =
                    "production-physical-custody-checkpoint-gc-publish-conflict";
                return false;
            }
            exact.RemovedRows.Add(removed);
        }
        exact.Published = true;
        return true;
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionPhysicalCustodyDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (exact.RemovedRows.Any(value => !repository
                .CanRestoreExactPendingProductionCustodyDrainForCheckpointGc(
                    value)))
        {
            throw new InvalidOperationException(
                "production-physical-custody-checkpoint-gc-rollback-conflict");
        }
        for (int index = exact.RemovedRows.Count - 1; index >= 0; index--)
        {
            if (!repository
                    .TryRestoreExactPendingProductionCustodyDrainForCheckpointGc(
                        exact.RemovedRows[index]))
            {
                throw new InvalidOperationException(
                    "production-physical-custody-checkpoint-gc-rollback-conflict");
            }
        }
        exact.RemovedRows.Clear();
        exact.Published = false;
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionPhysicalCustodyDrainCheckpointGcCandidate candidate)
    {
        CheckpointGcCandidate exact = RequireCheckpointGcCandidate(candidate);
        if (!exact.Published && exact.RemovedRows.Count != 0)
        {
            throw new InvalidOperationException(
                "production-physical-custody-checkpoint-gc-not-rolled-back");
        }
        exact.Completed = true;
        exact.RemovedRows.Clear();
        activeCheckpointGcCandidate = null;
    }

    public bool TryCapture(
        string stepOperationId,
        out ProductionPhysicalCustodyDrainSaveData record)
    {
        record = null;
        if (!TryGet(stepOperationId, out ProductionPhysicalCustodyDrainSaveData value))
            return false;
        record = value.Clone();
        return true;
    }

    private ProductionPhysicalCustodyDrainResult RecordProgress(
        string stepOperationId,
        string identity,
        Func<ProductionPhysicalCustodyDrainSaveData, List<string>> target,
        Func<ProductionPhysicalCustodyDrainSaveData, List<string>> allowed,
        ProductionPhysicalCustodyDrainPhase requiredPhase,
        string kind)
    {
        if (!IsToken(identity))
            return Conflict("production-physical-custody-drain-" + kind + "-invalid");
        if (!TryGet(stepOperationId, out ProductionPhysicalCustodyDrainSaveData value))
            return Conflict("production-physical-custody-drain-missing");
        if (value.phase != requiredPhase)
            return Deferred("production-physical-custody-drain-phase-mismatch");
        List<string> completed = target(value);
        int index = completed.BinarySearch(identity, StringComparer.Ordinal);
        if (index >= 0)
            return Current(value, ProductionPhysicalCustodyDrainStatus.Replay);
        if (allowed != null)
        {
            List<string> planned = allowed(value);
            if (completed.Count >= planned.Count
                || !string.Equals(
                    planned[completed.Count],
                    identity,
                    StringComparison.Ordinal))
            {
                return Conflict(
                    "production-physical-custody-drain-" + kind
                    + "-out-of-order-or-not-planned");
            }
        }
        completed.Add(identity);
        repository.SetPendingProductionCustodyDrain(value);
        return Current(value, ProductionPhysicalCustodyDrainStatus.Applied);
    }

    private ProductionPhysicalCustodyDrainResult AdvancePhase(
        string stepOperationId,
        ProductionPhysicalCustodyDrainPhase expected,
        ProductionPhysicalCustodyDrainPhase next,
        Func<ProductionPhysicalCustodyDrainSaveData, bool> canAdvance,
        string incompleteReason)
    {
        if (!TryGet(stepOperationId, out ProductionPhysicalCustodyDrainSaveData value))
            return Conflict("production-physical-custody-drain-missing");
        if (value.phase == next || value.phase > next)
            return Current(value, ProductionPhysicalCustodyDrainStatus.Replay);
        if (value.phase != expected)
            return Deferred("production-physical-custody-drain-phase-mismatch");
        if (!canAdvance(value))
            return Deferred(incompleteReason);
        value.phase = next;
        repository.SetPendingProductionCustodyDrain(value);
        return Current(value, ProductionPhysicalCustodyDrainStatus.Applied);
    }

    private bool TryGet(
        string stepOperationId,
        out ProductionPhysicalCustodyDrainSaveData value)
    {
        value = null;
        if (!IsToken(stepOperationId)
            || !repository.TryGetPendingProductionCustodyDrain(
                stepOperationId,
                out ProductionPhysicalCustodyDrainSaveData stored))
        {
            return false;
        }
        value = stored.Clone();
        return true;
    }

    private static bool IsValid(
        ProductionPhysicalCustodyDrainRequest request,
        out string failure)
    {
        failure = string.Empty;
        if (request == null
            || !IsToken(request.StepOperationId)
            || !IsToken(request.SourceDestinationId)
            || !string.Equals(
                request.OwnerStableId,
                "physical-destination:" + request.SourceDestinationId,
                StringComparison.Ordinal)
            || !IsDigest(request.RequestFingerprint)
            || !IsDigest(request.SourceOwnershipFingerprint)
            || !string.Equals(
                request.RequestFingerprint,
                ProductionPhysicalCustodyDrainFingerprint.CreateRequest(
                    request.StepOperationId,
                    request.OwnerStableId,
                    request.SourceDestinationId,
                    request.OwnerGridX,
                    request.OwnerGridY,
                    request.SourceOwnershipFingerprint,
                    request.SourceStackIds,
                    request.SourceActorIds,
                    request.SourceHaulIntentOperationIds,
                    request.InputQuantity,
                    request.InputMassGrams),
                StringComparison.Ordinal)
            || request.InputQuantity <= 0
            || request.InputMassGrams <= 0L
            || !IsCanonicalUnique(request.SourceStackIds, requireNonEmpty: true)
            || !IsCanonicalUnique(request.SourceActorIds, requireNonEmpty: false)
            || !IsCanonicalUnique(
                request.SourceHaulIntentOperationIds,
                requireNonEmpty: false))
        {
            failure = "production-physical-custody-drain-request-invalid";
            return false;
        }
        return true;
    }

    private CheckpointGcCandidate RequireCheckpointGcCandidate(
        IProductionPhysicalCustodyDrainCheckpointGcCandidate candidate)
    {
        if (candidate is not CheckpointGcCandidate exact
            || exact.Completed
            || !ReferenceEquals(exact, activeCheckpointGcCandidate))
        {
            throw new InvalidOperationException(
                "production-physical-custody-checkpoint-gc-candidate-conflict");
        }
        return exact;
    }

    private static bool RowsEqual(
        ProductionPhysicalCustodyDrainSaveData left,
        ProductionPhysicalCustodyDrainSaveData right) => left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private sealed class CheckpointGcCandidate :
        IProductionPhysicalCustodyDrainCheckpointGcCandidate
    {
        internal CheckpointGcCandidate(
            IReadOnlyList<ProductionPhysicalCustodyDrainSaveData> expectedRows)
        {
            ExpectedRows = expectedRows
                ?? throw new ArgumentNullException(nameof(expectedRows));
        }

        internal IReadOnlyList<ProductionPhysicalCustodyDrainSaveData>
            ExpectedRows { get; }
        internal List<ProductionPhysicalCustodyDrainSaveData> RemovedRows { get; } =
            new();
        internal bool Published { get; set; }
        internal bool Completed { get; set; }
    }

    private static bool Matches(
        ProductionPhysicalCustodyDrainSaveData value,
        ProductionPhysicalCustodyDrainRequest request) =>
        string.Equals(value.ownerStableId, request.OwnerStableId, StringComparison.Ordinal)
        && string.Equals(
            value.sourceDestinationId,
            request.SourceDestinationId,
            StringComparison.Ordinal)
        && string.Equals(
            value.requestFingerprint,
            request.RequestFingerprint,
            StringComparison.Ordinal)
        && value.ownerGridX == request.OwnerGridX
        && value.ownerGridY == request.OwnerGridY
        && string.Equals(
            value.sourceOwnershipFingerprint,
            request.SourceOwnershipFingerprint,
            StringComparison.Ordinal)
        && value.inputQuantity == request.InputQuantity
        && value.inputMassGrams == request.InputMassGrams
        && value.sourceStackIds.SequenceEqual(
            request.SourceStackIds,
            StringComparer.Ordinal)
        && value.sourceActorIds.SequenceEqual(
            request.SourceActorIds,
            StringComparer.Ordinal)
        && value.sourceHaulIntentOperationIds.SequenceEqual(
            request.SourceHaulIntentOperationIds,
            StringComparer.Ordinal);

    private static string BuildCommitId(
        ProductionPhysicalCustodyDrainSaveData value) =>
        CommitPrefix + Hash(value.stepOperationId + "|" + value.requestFingerprint)
            .Substring(0, 24);

    private static string BuildReceiptFingerprint(
        ProductionPhysicalCustodyDrainSaveData value)
    {
        StringBuilder canonical = new StringBuilder(256)
            .Append("production-physical-custody-drain-receipt@1|")
            .Append(value.stepOperationId).Append('|')
            .Append(value.ownerStableId).Append('|')
            .Append(value.sourceDestinationId).Append('|')
            .Append(value.ownerGridX.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(value.ownerGridY.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(value.requestFingerprint).Append('|')
            .Append(value.sourceOwnershipFingerprint).Append('|')
            .Append(value.inputQuantity.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(value.inputMassGrams.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(value.releasedQuantity.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(value.releasedMassGrams.ToString(CultureInfo.InvariantCulture))
            .Append('|').Append(value.resultFingerprint).Append('|')
            .Append(value.commitId).Append('|');
        Append(canonical, value.sourceStackIds);
        Append(canonical, value.sourceActorIds);
        Append(canonical, value.sourceHaulIntentOperationIds);
        Append(canonical, value.completedActorIds);
        Append(canonical, value.releasedHaulIntentOperationIds);
        Append(canonical, value.releasedStackIds);
        return Hash(canonical.ToString());
    }

    private static void Append(StringBuilder target, IEnumerable<string> values)
    {
        foreach (string value in values ?? Array.Empty<string>())
            target.Append(value.Length).Append(':').Append(value).Append(';');
        target.Append('|');
    }

    private static string Hash(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte current in digest)
            result.Append(current.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }

    private static bool IsCanonicalUnique(
        IReadOnlyList<string> values,
        bool requireNonEmpty)
    {
        if (values == null || requireNonEmpty && values.Count == 0)
            return false;
        for (int index = 0; index < values.Count; index++)
        {
            if (!IsToken(values[index])
                || index > 0
                    && string.CompareOrdinal(values[index - 1], values[index]) >= 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsToken(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsDigest(string value) => value?.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static ProductionPhysicalCustodyDrainResult Current(
        ProductionPhysicalCustodyDrainSaveData value,
        ProductionPhysicalCustodyDrainStatus status) => new(
            status,
            value.commitId,
            value.receiptFingerprint,
            string.Empty);

    private static ProductionPhysicalCustodyDrainResult Deferred(string reason) =>
        new(ProductionPhysicalCustodyDrainStatus.Deferred, string.Empty,
            string.Empty, reason);

    private static ProductionPhysicalCustodyDrainResult Conflict(string reason) =>
        new(ProductionPhysicalCustodyDrainStatus.Conflict, string.Empty,
            string.Empty, reason);
}

#if UNITY_EDITOR
public static class ProductionPhysicalCustodyDrainSaveValidationProbe
{
    public static DungeonGameRestoreReport Validate(
        DungeonPhysicalItemSaveData snapshot,
        IDungeonItemCatalogProvider catalog)
    {
        DungeonGameRestoreReport report = new();
        PhysicalItemSaveValidation.Validate(snapshot, report, catalog);
        return report;
    }
}
#endif
