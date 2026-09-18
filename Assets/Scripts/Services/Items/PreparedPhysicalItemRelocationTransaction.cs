using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public readonly struct PreparedPhysicalItemRelocationPreview
{
    internal PreparedPhysicalItemRelocationPreview(
        PhysicalItemRelocationReceipt receipt,
        string itemInstanceId,
        KoreanNameSnapshot displayName,
        long ownerRevision)
    {
        Receipt = receipt;
        ItemInstanceId = itemInstanceId ?? string.Empty;
        DisplayName = displayName;
        OwnerRevision = ownerRevision;
    }

    public PhysicalItemRelocationReceipt Receipt { get; }
    public string ItemInstanceId { get; }
    public KoreanNameSnapshot DisplayName { get; }
    public long OwnerRevision { get; }
    public bool IsValid => Receipt.IsCommitted
        && OwnerRevision > 0L
        && GameplayOutcomeLedger.IsValidDisplayNameSnapshot(DisplayName);
}

public interface IPreparedPhysicalItemRelocation
{
    PreparedPhysicalItemRelocationPreview Preview { get; }
    bool TryApply(
        out IReversiblePhysicalItemRelocation transaction,
        out string failureReason);
    void Cancel();
}

public interface IReversiblePhysicalItemRelocation
{
    PhysicalItemRelocationReceipt Receipt { get; }
    bool TryRollback(out string failureReason);
    bool TryAcknowledge(out string failureReason);
}

public interface IPreparedPhysicalItemRelocationService
{
    bool TryPrepare(
        string sourceStackId,
        int quantity,
        Vector2Int destinationPosition,
        WorldItemStackState destinationState,
        string destinationId,
        string operationId,
        string reasonCode,
        out IPreparedPhysicalItemRelocation prepared,
        out string failureReason);
}

/// <summary>
/// Domain-neutral bridge invoked after an exact relocation preview has been
/// calculated and before its physical mutation is applied. Implementations
/// own the typed narrative receipt; the item service never dispatches on a
/// result kind.
/// </summary>
public interface IPhysicalItemRelocationOutcomeParticipant
{
    bool TryPrepare(
        in PreparedPhysicalItemRelocationPreview preview,
        out IPreparedPhysicalItemGameplayOutcome prepared,
        out string failureReason);
}

/// <summary>
/// Two-phase relocation boundary for cross-aggregate owners. It previews the
/// exact destination stack identity before mutation, then returns a short-lived
/// rollback handle. The owning transaction must prepare its durable outbox from
/// Preview before TryApply and synchronously acknowledge or roll back the handle.
/// </summary>
public sealed class PreparedPhysicalItemRelocationService :
    IPreparedPhysicalItemRelocationService
{
    private readonly WorldItemRepository repository;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IItemMarkerPresenter markers;

    public PreparedPhysicalItemRelocationService(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        IDungeonItemCatalogProvider catalog,
        IItemMarkerPresenter markers)
    {
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.markers = markers
            ?? throw new ArgumentNullException(nameof(markers));
    }

    public bool TryPrepare(
        string sourceStackId,
        int quantity,
        Vector2Int destinationPosition,
        WorldItemStackState destinationState,
        string destinationId,
        string operationId,
        string reasonCode,
        out IPreparedPhysicalItemRelocation prepared,
        out string failureReason)
    {
        prepared = null;
        failureReason = string.Empty;
        string sourceId = sourceStackId?.Trim() ?? string.Empty;
        string targetId = destinationId?.Trim() ?? string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        string reason = reasonCode?.Trim() ?? string.Empty;
        if (!IsCanonicalRequired(sourceStackId, sourceId)
            || !IsCanonicalRequired(operationId, operation)
            || !IsCanonicalRequired(reasonCode, reason)
            || !IsCanonicalOptional(destinationId, targetId)
            || quantity <= 0
            || destinationState is WorldItemStackState.Carried
                or WorldItemStackState.InTransit
            || !TryGetAvailableSource(sourceId, quantity, out WorldItemStackRecord source))
        {
            failureReason = "physical-relocation-prepare-invalid";
            return false;
        }
        if (quantity < source.quantity
            && (!string.IsNullOrEmpty(source.itemInstanceId)
                || source.components?.Count > 0))
        {
            failureReason = "physical-relocation-unique-partial-forbidden";
            return false;
        }
        if (!catalog.TryGetDefinition(
                source.itemId,
                out DungeonItemDefinition definition)
            || definition == null)
        {
            failureReason =
                "physical-relocation-item-definition-unavailable:"
                + source.itemId;
            return false;
        }

        PhysicalItemMassSubject massSubject =
            PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                (ItemDefinitionId)source.itemId,
                source.itemInstanceId,
                source.components);
        long massGrams = massQuery.GetQuantityMass(
            (ItemDefinitionId)source.itemId,
            massSubject,
            quantity).Value;
        string destinationStackId = source.stackId;
        bool wholeStack = quantity == source.quantity;
        if (!wholeStack)
        {
            WorldItemStackRecord merge = FindExactMergeTarget(
                source,
                destinationPosition,
                destinationState,
                targetId,
                definition.MaxStack,
                quantity);
            destinationStackId = merge?.stackId
                ?? repository.AllocateStackId();
        }
        PhysicalItemRelocationReceipt receipt = new(
            operation,
            reason,
            source.stackId,
            destinationStackId,
            source.itemId,
            quantity,
            massGrams,
            source.position,
            destinationPosition);
        KoreanNameSnapshot display = new(
            definition.DisplayName,
            "item-definition:" + source.itemId + ":v1",
            KoreanPronunciationHint.AutoHangulDisplay(
                "item-definition-pronunciation-v1"),
            "ko-KR");
        PreparedPhysicalItemRelocationPreview preview = new(
            receipt,
            source.itemInstanceId,
            display,
            checked((long)repository.ItemStackVersion
                + (wholeStack ? 1L : 2L)));
        if (!preview.IsValid)
        {
            failureReason = "physical-relocation-preview-invalid";
            return false;
        }
        prepared = new Prepared(
            this,
            preview,
            destinationState,
            targetId,
            source.quantity,
            definition.MaxStack);
        return true;
    }

    private bool TryApply(
        PreparedPhysicalItemRelocationPreview preview,
        WorldItemStackState destinationState,
        string destinationId,
        int expectedSourceQuantity,
        int maxStack,
        out IReversiblePhysicalItemRelocation transaction,
        out string failureReason)
    {
        transaction = null;
        failureReason = string.Empty;
        PhysicalItemRelocationReceipt expected = preview.Receipt;
        long expectedDelta = expected.Quantity == expectedSourceQuantity
            ? 1L
            : 2L;
        if (preview.OwnerRevision
                != checked((long)repository.ItemStackVersion + expectedDelta)
            || !TryGetAvailableSource(
                expected.SourceStackId,
                expected.Quantity,
                out WorldItemStackRecord source)
            || source.quantity != expectedSourceQuantity
            || !string.Equals(source.itemId, expected.ItemId, StringComparison.Ordinal)
            || !string.Equals(
                source.itemInstanceId ?? string.Empty,
                preview.ItemInstanceId,
                StringComparison.Ordinal)
            || source.position != expected.SourcePosition)
        {
            failureReason = "physical-relocation-prepared-source-changed";
            return false;
        }

        RelocationRollbackState rollback = new(
            repository,
            markers,
            source,
            expected,
            destinationState,
            destinationId);
        try
        {
            if (expected.Quantity == source.quantity)
            {
                rollback.CaptureWholeSource();
                repository.Relocate(source, expected.DestinationPosition);
                source.state = destinationState;
                source.destinationId = destinationId;
                source.sourceStorageDestinationId = string.Empty;
                source.hasDestinationPosition = destinationId.Length > 0;
                source.destinationPosition = expected.DestinationPosition;
                repository.MarkChanged();
            }
            else
            {
                WorldItemStackRecord destination = repository.RecordsById
                    .TryGetValue(
                        expected.DestinationStackId,
                        out WorldItemStackRecord existing)
                    ? existing
                    : null;
                if (destination != null)
                {
                    if (!IsExactMergeTarget(
                            destination,
                            source,
                            expected.DestinationPosition,
                            destinationState,
                            destinationId,
                            maxStack,
                            expected.Quantity))
                    {
                        failureReason =
                            "physical-relocation-prepared-destination-changed";
                        return false;
                    }
                    rollback.CaptureMergeDestination(destination);
                    destination.quantity = checked(
                        destination.quantity + expected.Quantity);
                    repository.MarkChanged();
                }
                else
                {
                    rollback.CaptureNewDestination();
                    repository.Add(new WorldItemStackRecord
                    {
                        stackId = expected.DestinationStackId,
                        itemId = source.itemId,
                        itemInstanceId = string.Empty,
                        quantity = expected.Quantity,
                        state = destinationState,
                        position = expected.DestinationPosition,
                        destinationId = destinationId,
                        sourceStorageDestinationId = string.Empty,
                        hasDestinationPosition = destinationId.Length > 0,
                        destinationPosition = expected.DestinationPosition,
                        components = new List<ItemInstanceComponentSaveData>()
                    });
                }
                source.quantity = checked(source.quantity - expected.Quantity);
                repository.MarkChanged();
            }
        }
        catch (Exception exception)
        {
            rollback.TryRollback(out _);
            failureReason = "physical-relocation-prepared-apply-rollback:"
                + exception.Message;
            return false;
        }

        if (repository.ItemStackVersion != preview.OwnerRevision)
        {
            rollback.TryRollback(out _);
            failureReason =
                "physical-relocation-owner-revision-mismatch";
            return false;
        }

        Refresh(expected.SourcePosition);
        Refresh(expected.DestinationPosition);
        transaction = rollback;
        return true;
    }

    private bool TryGetAvailableSource(
        string sourceId,
        int quantity,
        out WorldItemStackRecord source)
    {
        if (!repository.RecordsById.TryGetValue(sourceId, out source)
            || source == null
            || source.quantity < quantity
            || source.quantity - source.reservedQuantity < quantity
            || source.reservedQuantity > 0
            || !string.IsNullOrEmpty(source.reservedByPersistentId)
            || source.state is WorldItemStackState.Carried
                or WorldItemStackState.InTransit
            || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                source.components))
        {
            source = null;
            return false;
        }
        return true;
    }

    private WorldItemStackRecord FindExactMergeTarget(
        WorldItemStackRecord source,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        int maxStack,
        int quantity)
    {
        if (!repository.RecordsByPosition.TryGetValue(
                position,
                out List<WorldItemStackRecord> candidates))
            return null;
        return candidates.FirstOrDefault(candidate => IsExactMergeTarget(
            candidate,
            source,
            position,
            state,
            destinationId,
            maxStack,
            quantity));
    }

    private static bool IsExactMergeTarget(
        WorldItemStackRecord candidate,
        WorldItemStackRecord source,
        Vector2Int position,
        WorldItemStackState state,
        string destinationId,
        int maxStack,
        int quantity) => candidate != null
        && candidate.position == position
        && candidate.quantity > 0
        && candidate.quantity + quantity <= maxStack
        && candidate.dropDisposition == WorldItemDropDisposition.None
        && candidate.state == state
        && candidate.reservedQuantity <= 0
        && string.Equals(candidate.itemId, source.itemId, StringComparison.Ordinal)
        && string.Equals(
            candidate.destinationId ?? string.Empty,
            destinationId,
            StringComparison.Ordinal)
        && string.IsNullOrEmpty(candidate.sourceStorageDestinationId)
        && candidate.hasDestinationPosition == (destinationId.Length > 0)
        && (destinationId.Length == 0
            || candidate.destinationPosition == position)
        && string.IsNullOrEmpty(candidate.sourceCharacterId)
        && string.IsNullOrEmpty(candidate.sourceDisplayName)
        && string.IsNullOrEmpty(candidate.sourceSpeciesTag)
        && string.IsNullOrEmpty(candidate.sourceDeathReason)
        && !candidate.emergencyButcheryAllowed
        && candidate.wasteOrigin == WasteOriginKind.Unknown
        && Mathf.Abs(candidate.contamination) < 0.01f
        && string.Equals(
            ItemStackSignature.Create(candidate.itemId, candidate.components),
            ItemStackSignature.Create(source.itemId, source.components),
            StringComparison.Ordinal);

    private void Refresh(Vector2Int position)
    {
        try
        {
            markers.RefreshAt(position);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Prepared relocation marker refresh failed after authority commit: "
                + exception);
        }
    }

    private static bool IsCanonicalRequired(string original, string trimmed) =>
        trimmed.Length > 0
        && string.Equals(original, trimmed, StringComparison.Ordinal);

    private static bool IsCanonicalOptional(string original, string trimmed) =>
        string.Equals(original ?? string.Empty, trimmed, StringComparison.Ordinal);

    private sealed class Prepared : IPreparedPhysicalItemRelocation
    {
        private readonly PreparedPhysicalItemRelocationService owner;
        private readonly WorldItemStackState destinationState;
        private readonly string destinationId;
        private readonly int expectedSourceQuantity;
        private readonly int maxStack;
        private bool terminal;

        internal Prepared(
            PreparedPhysicalItemRelocationService owner,
            PreparedPhysicalItemRelocationPreview preview,
            WorldItemStackState destinationState,
            string destinationId,
            int expectedSourceQuantity,
            int maxStack)
        {
            this.owner = owner;
            Preview = preview;
            this.destinationState = destinationState;
            this.destinationId = destinationId;
            this.expectedSourceQuantity = expectedSourceQuantity;
            this.maxStack = maxStack;
        }

        public PreparedPhysicalItemRelocationPreview Preview { get; }

        public bool TryApply(
            out IReversiblePhysicalItemRelocation transaction,
            out string failureReason)
        {
            if (terminal)
            {
                transaction = null;
                failureReason = "physical-relocation-prepared-terminal";
                return false;
            }
            terminal = true;
            return owner.TryApply(
                Preview,
                destinationState,
                destinationId,
                expectedSourceQuantity,
                maxStack,
                out transaction,
                out failureReason);
        }

        public void Cancel() => terminal = true;
    }

    private sealed class RelocationRollbackState :
        IReversiblePhysicalItemRelocation
    {
        private readonly WorldItemRepository repository;
        private readonly IItemMarkerPresenter markers;
        private readonly WorldItemStackRecord source;
        private readonly WorldItemStackState oldState;
        private readonly string oldDestinationId;
        private readonly string oldStorageDestinationId;
        private readonly bool oldHasDestinationPosition;
        private readonly Vector2Int oldDestinationPosition;
        private readonly int oldSourceQuantity;
        private WorldItemStackRecord mergeDestination;
        private int oldMergeQuantity;
        private bool newDestination;
        private bool wholeSource;
        private bool terminal;

        internal RelocationRollbackState(
            WorldItemRepository repository,
            IItemMarkerPresenter markers,
            WorldItemStackRecord source,
            PhysicalItemRelocationReceipt receipt,
            WorldItemStackState destinationState,
            string destinationId)
        {
            this.repository = repository;
            this.markers = markers;
            this.source = source;
            Receipt = receipt;
            oldState = source.state;
            oldDestinationId = source.destinationId;
            oldStorageDestinationId = source.sourceStorageDestinationId;
            oldHasDestinationPosition = source.hasDestinationPosition;
            oldDestinationPosition = source.destinationPosition;
            oldSourceQuantity = source.quantity;
        }

        public PhysicalItemRelocationReceipt Receipt { get; }

        internal void CaptureWholeSource() => wholeSource = true;

        internal void CaptureMergeDestination(WorldItemStackRecord destination)
        {
            mergeDestination = destination;
            oldMergeQuantity = destination.quantity;
        }

        internal void CaptureNewDestination() => newDestination = true;

        public bool TryRollback(out string failureReason)
        {
            failureReason = string.Empty;
            if (terminal)
            {
                failureReason = "physical-relocation-transaction-terminal";
                return false;
            }
            try
            {
                if (wholeSource)
                {
                    repository.Relocate(source, Receipt.SourcePosition);
                    source.state = oldState;
                    source.destinationId = oldDestinationId;
                    source.sourceStorageDestinationId = oldStorageDestinationId;
                    source.hasDestinationPosition = oldHasDestinationPosition;
                    source.destinationPosition = oldDestinationPosition;
                }
                else
                {
                    source.quantity = oldSourceQuantity;
                    if (newDestination
                        && repository.RecordsById.TryGetValue(
                            Receipt.DestinationStackId,
                            out WorldItemStackRecord created))
                    {
                        repository.Remove(created);
                    }
                    else if (mergeDestination != null)
                    {
                        mergeDestination.quantity = oldMergeQuantity;
                    }
                }
                repository.MarkChanged();
                terminal = true;
            }
            catch (Exception exception)
            {
                failureReason = "physical-relocation-rollback-failed:"
                    + exception.Message;
                return false;
            }
            Refresh(Receipt.SourcePosition);
            Refresh(Receipt.DestinationPosition);
            return true;
        }

        public bool TryAcknowledge(out string failureReason)
        {
            if (terminal)
            {
                failureReason = "physical-relocation-transaction-terminal";
                return false;
            }
            terminal = true;
            failureReason = string.Empty;
            return true;
        }

        private void Refresh(Vector2Int position)
        {
            try
            {
                markers.RefreshAt(position);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "Prepared relocation marker refresh failed after rollback: "
                    + exception);
            }
        }
    }
}
