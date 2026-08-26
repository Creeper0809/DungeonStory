using System;
using System.Linq;
using DungeonStory.CoreSession;

public static class ExternalInfluenceTrailCharmOutbox
{
    public const string ReasonCode =
        ExternalInfluenceTrailCharmSaveContract.ReasonCode;

    public static string FormatOperationId(string siteId) =>
        ExternalInfluenceTrailCharmSaveContract.FormatOperationId(siteId);

    public static bool HasPending(DungeonExternalInfluenceSaveData state) =>
        state != null
        && state.trailCharmCommitPhase
            != ExternalInfluenceTrailCharmCommitPhase.None;

    public static void RecordPending(
        DungeonExternalInfluenceSaveData state,
        string siteId,
        string itemId,
        PhysicalItemBatchDispositionReceipt receipt)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
        string canonicalSiteId = siteId ?? string.Empty;
        string canonicalItemId = itemId ?? string.Empty;
        if (!IsCanonicalRequired(canonicalSiteId)
            || !IsCanonicalRequired(canonicalItemId)
            || !receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(
                receipt.OperationId,
                FormatOperationId(canonicalSiteId),
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                ReasonCode,
                StringComparison.Ordinal)
            || receipt.Quantity != 1)
        {
            throw new InvalidOperationException(
                "External-influence trail-charm receipt is not canonical.");
        }

        state.trailCharmCommitPhase =
            ExternalInfluenceTrailCharmCommitPhase.ItemCommitted;
        state.pendingTrailCharmSiteId = canonicalSiteId;
        state.pendingTrailCharmOperationId = receipt.OperationId;
        state.pendingTrailCharmReasonCode = receipt.ReasonCode;
        state.pendingTrailCharmCommitId = receipt.CommitId;
        state.pendingTrailCharmSourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        state.pendingTrailCharmQuantity = receipt.Quantity;
        state.pendingTrailCharmMassGrams = receipt.InputMassGrams;
        state.pendingTrailCharmItemId = canonicalItemId;
    }

    public static bool TryFinalizePending(
        ExternalInfluenceAggregateState aggregate,
        IPhysicalItemBatchDispositionService dispositions,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (aggregate?.Data == null || dispositions == null
            || !IsStructurallyValid(aggregate.Data))
        {
            failureReason =
                "external-influence-trail-charm-outbox-invalid";
            return false;
        }

        DungeonExternalInfluenceSaveData state = aggregate.Data;
        bool pending = dispositions.TryGetPending(
            state.pendingTrailCharmOperationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (pending && !ReceiptMatches(state, receipt))
        {
            failureReason =
                "external-influence-trail-charm-receipt-mismatch";
            return false;
        }
        if (!pending
            && state.trailCharmCommitPhase
                == ExternalInfluenceTrailCharmCommitPhase.ItemCommitted)
        {
            failureReason =
                "external-influence-trail-charm-receipt-missing";
            return false;
        }

        if (state.trailCharmCommitPhase
            == ExternalInfluenceTrailCharmCommitPhase.ItemCommitted)
        {
            ExternalInfluenceDomainRules.UnlockIntel(
                aggregate,
                state.pendingTrailCharmSiteId);
            state.trailCharmCommitPhase =
                ExternalInfluenceTrailCharmCommitPhase.IntelPublished;
        }
        if (!ExternalInfluenceDomainRules.IsIntelUnlocked(
                aggregate,
                state.pendingTrailCharmSiteId))
        {
            failureReason =
                "external-influence-trail-charm-terminal-mismatch";
            return false;
        }
        if (pending && !dispositions.Acknowledge(
                state.pendingTrailCharmCommitId,
                out failureReason))
        {
            return false;
        }

        ClearPending(state);
        return true;
    }

    public static bool IsStructurallyValid(
        DungeonExternalInfluenceSaveData state) =>
        ExternalInfluenceTrailCharmSaveContract.IsStructurallyValid(state);

    public static bool HasEmptyProvenance(
        DungeonExternalInfluenceSaveData state) =>
        ExternalInfluenceTrailCharmSaveContract.HasEmptyProvenance(state);

    private static bool ReceiptMatches(
        DungeonExternalInfluenceSaveData state,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            state.pendingTrailCharmOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            state.pendingTrailCharmReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            state.pendingTrailCharmCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == state.pendingTrailCharmQuantity
        && receipt.InputMassGrams == state.pendingTrailCharmMassGrams
        && receipt.SourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal)
            .SequenceEqual(
                state.pendingTrailCharmSourceStackIds,
                StringComparer.Ordinal);

    private static void ClearPending(
        DungeonExternalInfluenceSaveData state)
    {
        state.trailCharmCommitPhase =
            ExternalInfluenceTrailCharmCommitPhase.None;
        state.pendingTrailCharmSiteId = string.Empty;
        state.pendingTrailCharmOperationId = string.Empty;
        state.pendingTrailCharmReasonCode = string.Empty;
        state.pendingTrailCharmCommitId = string.Empty;
        state.pendingTrailCharmSourceStackIds.Clear();
        state.pendingTrailCharmQuantity = 0;
        state.pendingTrailCharmMassGrams = 0L;
        state.pendingTrailCharmItemId = string.Empty;
    }

    private static bool IsCanonicalRequired(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
