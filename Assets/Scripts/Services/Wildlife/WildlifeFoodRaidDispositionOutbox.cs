using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

public sealed class WildlifeFoodRaidDispositionOutbox
{
    private const string ReasonCode = "wildlife-food-raid-consumed";
    private readonly IPhysicalItemBatchDispositionService dispositions;
    private readonly IDomainFactStagingPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;

    public WildlifeFoodRaidDispositionOutbox(IPhysicalItemBatchDispositionService dispositions,
        IDomainFactStagingPublisher facts, IDomainFactRecoveryPublisher recovery)
    {
        this.dispositions = dispositions ?? throw new ArgumentNullException(nameof(dispositions));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }

    public bool TryCommit(WildlifeFoodRaidOrderSaveData order,
        WorldItemStackSnapshot target, out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || order.outcomeSchemaVersion != WildlifeFoodRaidOrderSaveData.CurrentOutcomeSchemaVersion)
        { failureReason = "wildlife-food-raid-outcome-schema-invalid"; return false; }
        if (order.commitPhase != WildlifeFoodRaidCommitPhase.None)
            return TryResume(order, out failureReason);
        if (!HasValidShape(order) || order.state != WildlifeFoodRaidOrderState.Approaching
            || target == null || target.AvailableQuantity < 1 || !Text(target.StackId)
            || !Text(target.ItemId) || !Text(target.DisplayName)
            || !float.IsFinite(order.completedGameTime) || order.completedGameTime < order.startedGameTime
            || order.completedAbsoluteDay < order.startedAbsoluteDay)
        { failureReason = "wildlife-food-raid-target-unavailable"; return false; }

        order.targetStackId = target.StackId;
        order.targetItemId = target.ItemId;
        order.targetItemDisplayName = target.DisplayName;
        string operationId = OperationId(order);
        if (!dispositions.TryCommitPending(new[] { new PhysicalItemTransformInput(target.StackId, 1) },
            PhysicalItemDispositionKind.Sink, operationId, ReasonCode,
            out PhysicalItemBatchDispositionReceipt receipt, out failureReason)) return false;

        order.commitPhase = WildlifeFoodRaidCommitPhase.ItemCommitted;
        order.state = WildlifeFoodRaidOrderState.WaitingForDispositionFinalization;
        order.dispositionOperationId = operationId;
        order.dispositionReasonCode = ReasonCode;
        order.dispositionCommitId = receipt.CommitId;
        order.dispositionSourceStackIds = receipt.SourceStackIds.ToList();
        order.dispositionQuantity = receipt.Quantity;
        order.dispositionInputMassGrams = receipt.InputMassGrams;
        order.dispositionOwnerRevision = receipt.OwnerRevision;
        order.dispositionItemId = target.ItemId;
        order.stolenQuantity = receipt.Quantity;
        order.outcomeReason = "늑대가 식량에 도달해 1개를 훔쳤습니다.";
        // Freeze before publication; later UI errors must not change the applied fact.
        order.fact = CaptureFact(order);
        return TryResume(order, out failureReason);
    }

    public bool TryResume(WildlifeFoodRaidOrderSaveData order, out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || !HasValidShape(order))
        { failureReason = "wildlife-food-raid-pending-shape-invalid"; return false; }
        if (order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged) return true;
        if (!TryValidatePending(order, out _, out failureReason)) return false;
        var fact = new WildlifeFoodRaidFact(order.fact);
        DomainFactDelivery delivery;
        if (order.publicationKey.Length == 0)
        {
            IDomainFactStage stage = null;
            try
            {
                if (!facts.TryStage(fact, out stage, out failureReason)) return false;
                if (stage == null || !Text(stage.OperationKey) || stage.OwnerRevision != order.dispositionOwnerRevision)
                { failureReason = "wildlife-food-raid-publication-reservation-invalid"; return false; }
                order.publicationKey = stage.OperationKey;
                delivery = stage.Commit();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            { failureReason = "wildlife-food-raid-publication-failed:" + exception.Message; return false; }
            finally { if (stage?.IsActive == true) stage.Cancel(); }
        }
        else
        {
            var results = new DomainFactDelivery[1];
            try
            {
                if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(order.publicationKey, fact) },
                    results, out failureReason)) return false;
                delivery = results[0];
            }
            catch (Exception exception) when (exception is not OutOfMemoryException and not StackOverflowException)
            { failureReason = "wildlife-food-raid-replay-failed:" + exception.Message; return false; }
        }
        if (!delivery.MayBeCommitted)
        { failureReason = delivery.Detail; return false; }
        order.commitPhase = WildlifeFoodRaidCommitPhase.RaidPublished;
        if ((!string.IsNullOrEmpty(delivery.OperationKey) && delivery.OperationKey != order.publicationKey)
            || (!string.IsNullOrEmpty(order.publicationId) && !string.IsNullOrEmpty(delivery.PublicationId)
                && order.publicationId != delivery.PublicationId)
            || (!string.IsNullOrEmpty(order.publicationFingerprint) && !string.IsNullOrEmpty(delivery.Fingerprint)
                && order.publicationFingerprint != delivery.Fingerprint))
        { failureReason = "wildlife-food-raid-publication-proof-conflict"; return false; }
        if (delivery.Phase == DomainFactDeliveryPhase.InDoubt)
        { failureReason = delivery.Detail; return false; }
        if (delivery.OperationKey != order.publicationKey || !Text(delivery.PublicationId) || !Text(delivery.Fingerprint))
        { failureReason = "wildlife-food-raid-publication-proof-missing"; return false; }
        order.publicationId = delivery.PublicationId;
        order.publicationFingerprint = delivery.Fingerprint;
        if (!delivery.Completed)
        { failureReason = delivery.Detail; return false; }
        if (!dispositions.Acknowledge(order.dispositionCommitId, out failureReason)) return false;
        order.commitPhase = WildlifeFoodRaidCommitPhase.Acknowledged;
        order.state = WildlifeFoodRaidOrderState.Leaving;
        order.outcomeReason = order.fact.outcomeReason;
        return true;
    }

    public bool TryValidatePending(WildlifeFoodRaidOrderSaveData order,
        out PhysicalItemBatchDispositionReceipt receipt, out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        if (!IsPending(order) || !HasValidShape(order))
        { failureReason = "wildlife-food-raid-pending-shape-invalid"; return false; }
        if (!dispositions.TryGetPending(order.dispositionOperationId, out receipt)
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || receipt.OperationId != order.dispositionOperationId || receipt.ReasonCode != order.dispositionReasonCode
            || receipt.CommitId != order.dispositionCommitId || receipt.Quantity != order.dispositionQuantity
            || receipt.InputMassGrams != order.dispositionInputMassGrams || receipt.OwnerRevision != order.dispositionOwnerRevision
            || !receipt.SourceStackIds.SequenceEqual(order.dispositionSourceStackIds, StringComparer.Ordinal))
        { failureReason = "wildlife-food-raid-pending-receipt-mismatch"; return false; }
        return true;
    }

    public static bool HasValidShape(WildlifeFoodRaidOrderSaveData order)
    {
        if (order == null || order.outcomeSchemaVersion != WildlifeFoodRaidOrderSaveData.CurrentOutcomeSchemaVersion
            || !Text(order.raidId) || !Text(order.wildlifeId) || !Text(order.wildlifeSpeciesId)
            || !Text(order.wildlifeDisplayName) || order.requestedQuantity != 1
            || !float.IsFinite(order.startedGameTime) || order.startedGameTime < 0 || order.startedAbsoluteDay < 0
            || !Optional(order.publicationKey) || !Optional(order.publicationId) || !Optional(order.publicationFingerprint))
            return false;
        if (order.commitPhase == WildlifeFoodRaidCommitPhase.None)
            return order.state is WildlifeFoodRaidOrderState.Approaching or WildlifeFoodRaidOrderState.Cancelled or WildlifeFoodRaidOrderState.Failed
                && order.fact?.IsEmpty == true && order.stolenQuantity == 0 && order.publicationKey.Length == 0
                && order.publicationId.Length == 0 && order.publicationFingerprint.Length == 0
                && string.IsNullOrEmpty(order.dispositionOperationId) && string.IsNullOrEmpty(order.dispositionReasonCode)
                && string.IsNullOrEmpty(order.dispositionCommitId) && order.dispositionSourceStackIds?.Count == 0
                && order.dispositionQuantity == 0 && order.dispositionInputMassGrams == 0
                && order.dispositionOwnerRevision == 0 && string.IsNullOrEmpty(order.dispositionItemId);
        if (!MatchesFact(order)) return false;
        if ((order.publicationId.Length > 0 || order.publicationFingerprint.Length > 0) && order.publicationKey.Length == 0)
            return false;
        if (IsPending(order))
            return order.state == WildlifeFoodRaidOrderState.WaitingForDispositionFinalization
                && (order.commitPhase != WildlifeFoodRaidCommitPhase.RaidPublished || order.publicationKey.Length > 0);
        return order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged
            && order.state is WildlifeFoodRaidOrderState.Leaving or WildlifeFoodRaidOrderState.Stolen
            && Text(order.publicationKey) && Text(order.publicationId) && Text(order.publicationFingerprint);
    }

    public static bool IsPending(WildlifeFoodRaidOrderSaveData order) => order != null
        && order.commitPhase is WildlifeFoodRaidCommitPhase.ItemCommitted or WildlifeFoodRaidCommitPhase.RaidPublished;
    public static bool IsDurablyStolen(WildlifeFoodRaidOrderSaveData order) => order != null
        && order.outcomeSchemaVersion == WildlifeFoodRaidOrderSaveData.CurrentOutcomeSchemaVersion
        && order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged && order.stolenQuantity > 0;

    private static WildlifeFoodRaidFactData CaptureFact(WildlifeFoodRaidOrderSaveData order) => new()
    {
            raidId = order.raidId,
            wildlifeId = order.wildlifeId,
            wildlifeSpeciesId = order.wildlifeSpeciesId,
            wildlifeDisplayName = order.wildlifeDisplayName,
            targetStackId = order.targetStackId,
            targetItemId = order.targetItemId,
            targetItemDisplayName = order.targetItemDisplayName,
            dispositionOperationId = order.dispositionOperationId,
            dispositionReasonCode = order.dispositionReasonCode,
            dispositionCommitId = order.dispositionCommitId,
            dispositionItemId = order.dispositionItemId,
            outcomeReason = order.outcomeReason,
            requestedQuantity = order.requestedQuantity,
            startX = order.startX,
            startY = order.startY,
            startedAbsoluteDay = order.startedAbsoluteDay,
            endX = order.endX,
            endY = order.endY,
            completedAbsoluteDay = order.completedAbsoluteDay,
            stolenQuantity = order.stolenQuantity,
            dispositionQuantity = order.dispositionQuantity,
            startedGameTime = order.startedGameTime,
            completedGameTime = order.completedGameTime,
            dispositionInputMassGrams = order.dispositionInputMassGrams,
            dispositionOwnerRevision = order.dispositionOwnerRevision,
            dispositionSourceStackIds = order.dispositionSourceStackIds.ToList()
    };

    private static bool MatchesFact(WildlifeFoodRaidOrderSaveData order)
    {
        var value = order.fact;
        if (value == null) return false;
        try { value.Validate(); }
        catch (ArgumentException) { return false; }
        return order.dispositionSourceStackIds != null
            && value.raidId == order.raidId
            && value.wildlifeId == order.wildlifeId
            && value.wildlifeSpeciesId == order.wildlifeSpeciesId
            && value.wildlifeDisplayName == order.wildlifeDisplayName
            && value.targetStackId == order.targetStackId
            && value.targetItemId == order.targetItemId
            && value.targetItemDisplayName == order.targetItemDisplayName
            && value.dispositionOperationId == order.dispositionOperationId
            && value.dispositionReasonCode == order.dispositionReasonCode
            && value.dispositionCommitId == order.dispositionCommitId
            && value.dispositionItemId == order.dispositionItemId
            && value.requestedQuantity == order.requestedQuantity
            && value.startX == order.startX
            && value.startY == order.startY
            && value.startedAbsoluteDay == order.startedAbsoluteDay
            && value.endX == order.endX
            && value.endY == order.endY
            && value.completedAbsoluteDay == order.completedAbsoluteDay
            && value.stolenQuantity == order.stolenQuantity
            && value.dispositionQuantity == order.dispositionQuantity
            && value.startedGameTime == order.startedGameTime
            && value.completedGameTime == order.completedGameTime
            && value.dispositionInputMassGrams == order.dispositionInputMassGrams
            && value.dispositionOwnerRevision == order.dispositionOwnerRevision
            && value.dispositionSourceStackIds.SequenceEqual(order.dispositionSourceStackIds, StringComparer.Ordinal);
    }

    private static string OperationId(WildlifeFoodRaidOrderSaveData order) => $"wildlife-food-raid:{order.raidId}:{order.wildlifeId}";
    private static bool Text(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Length <= 4096 && value == value.Trim();
    private static bool Optional(string value) => value != null && (value.Length == 0 || Text(value));
}
