using System;
using System.Linq;
using UnityEngine;

public interface ICapturedWildlifeFeedOutcomeTarget
{
    string WildlifeId { get; }
    float Hunger { get; }
    int CurrentHealth { get; }
    string LastCaptiveFeedCommitId { get; }

    bool TryApplyCaptiveFeedOutcome(
        string commitId,
        float hungerTarget,
        int healthTarget,
        out bool applied);
}

public static class CapturedWildlifeFeedOutbox
{
    public const string ReasonCode =
        "captivity-wildlife-feed-consumed";

    public static string FormatOperationId(
        string wildlifeId,
        int sequence) =>
        $"captivity-wildlife-feed:{wildlifeId}:{sequence:D8}";

    public static bool HasPending(CapturedWildlifeState state) =>
        state != null
        && state.pendingFeedPhase != CapturedWildlifeFeedCommitPhase.None;

    public static void RecordPending(
        CapturedWildlifeState state,
        int sequence,
        PhysicalItemBatchDispositionReceipt receipt,
        string itemId,
        float nutrition,
        float diseaseChance,
        bool diseaseTriggered,
        ICapturedWildlifeFeedOutcomeTarget target)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }
        string canonicalItemId = itemId ?? string.Empty;
        float effectiveNutrition = Mathf.Clamp01(nutrition);
        float canonicalDiseaseChance = Mathf.Clamp01(diseaseChance);
        if (sequence <= 0
            || sequence != state.nextFeedOperationSequence
            || !string.Equals(
                state.wildlifeId,
                target.WildlifeId,
                StringComparison.Ordinal)
            || !receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(
                receipt.OperationId,
                FormatOperationId(state.wildlifeId, sequence),
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                ReasonCode,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(canonicalItemId)
            || effectiveNutrition <= 0f
            || diseaseTriggered && canonicalDiseaseChance <= 0f)
        {
            throw new InvalidOperationException(
                "Captured wildlife feed receipt is not canonical.");
        }

        state.pendingFeedOperationSequence = sequence;
        state.pendingFeedPhase =
            CapturedWildlifeFeedCommitPhase.ItemCommitted;
        state.pendingFeedOperationId = receipt.OperationId;
        state.pendingFeedReasonCode = receipt.ReasonCode;
        state.pendingFeedCommitId = receipt.CommitId;
        state.pendingFeedSourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        state.pendingFeedQuantity = receipt.Quantity;
        state.pendingFeedMassGrams = receipt.InputMassGrams;
        state.pendingFeedItemId = canonicalItemId;
        state.pendingFeedNutrition = effectiveNutrition;
        state.pendingFeedDiseaseChance = canonicalDiseaseChance;
        state.pendingFeedDiseaseTriggered = diseaseTriggered;
        state.pendingFeedHungerTarget = Mathf.Clamp01(
            target.Hunger - effectiveNutrition);
        state.pendingFeedHealthTarget = Math.Max(
            0,
            target.CurrentHealth - (diseaseTriggered ? 1 : 0));
        state.pendingFeedSicknessTarget = Mathf.Clamp(
            state.feedSicknessSeverity + (diseaseTriggered ? 25f : 0f),
            0f,
            100f);
    }

    public static bool TryFinalizePending(
        CapturedWildlifeState state,
        ICapturedWildlifeFeedOutcomeTarget target,
        IPhysicalItemBatchDispositionService batchDispositions,
        out bool careAppliedNow,
        out string failureReason)
    {
        careAppliedNow = false;
        failureReason = string.Empty;
        if (!IsStructurallyValid(state)
            || target == null
            || batchDispositions == null
            || !string.Equals(
                state.wildlifeId,
                target.WildlifeId,
                StringComparison.Ordinal))
        {
            failureReason = "captured-wildlife-feed-outbox-invalid";
            return false;
        }

        bool pending = batchDispositions.TryGetPending(
            state.pendingFeedOperationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (pending && !ReceiptMatches(state, receipt))
        {
            failureReason =
                "captured-wildlife-feed-outbox-receipt-mismatch";
            return false;
        }
        if (state.pendingFeedPhase
                == CapturedWildlifeFeedCommitPhase.ItemCommitted
            && !pending)
        {
            failureReason =
                "captured-wildlife-feed-outbox-receipt-missing";
            return false;
        }

        if (state.pendingFeedPhase
            == CapturedWildlifeFeedCommitPhase.ItemCommitted)
        {
            if (!target.TryApplyCaptiveFeedOutcome(
                    state.pendingFeedCommitId,
                    state.pendingFeedHungerTarget,
                    state.pendingFeedHealthTarget,
                    out careAppliedNow))
            {
                failureReason =
                    "captured-wildlife-feed-outbox-target-rejected";
                return false;
            }
            state.feedSicknessSeverity =
                state.pendingFeedSicknessTarget;
            state.lastFeedItemId = state.pendingFeedItemId;
            state.lastFeedDiseaseChance =
                state.pendingFeedDiseaseChance;
            state.lastCareStatus =
                WasteFeedOutcomeCode.FeedConsumed.ToString();
            state.pendingFeedPhase =
                CapturedWildlifeFeedCommitPhase.CarePublished;
        }

        if (!TerminalMatches(state, target))
        {
            failureReason =
                "captured-wildlife-feed-outbox-terminal-mismatch";
            return false;
        }
        if (pending && !batchDispositions.Acknowledge(
                state.pendingFeedCommitId,
                out failureReason))
        {
            return false;
        }

        state.foodDeliveryPending = false;
        ClearPending(state);
        return true;
    }

    public static bool IsStructurallyValid(CapturedWildlifeState state)
    {
        if (state == null
            || state.pendingFeedPhase is not (
                CapturedWildlifeFeedCommitPhase.ItemCommitted
                or CapturedWildlifeFeedCommitPhase.CarePublished)
            || state.pendingFeedOperationSequence <= 0
            || state.pendingFeedOperationSequence
                != state.nextFeedOperationSequence
            || !string.Equals(
                state.pendingFeedOperationId,
                FormatOperationId(
                    state.wildlifeId,
                    state.pendingFeedOperationSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                state.pendingFeedReasonCode,
                ReasonCode,
                StringComparison.Ordinal)
            || state.pendingFeedSourceStackIds == null
            || state.pendingFeedSourceStackIds.Count == 0
            || state.pendingFeedSourceStackIds.Any(
                value => string.IsNullOrWhiteSpace(value)
                    || !string.Equals(
                        value,
                        value.Trim(),
                        StringComparison.Ordinal))
            || state.pendingFeedSourceStackIds
                .Distinct(StringComparer.Ordinal).Count()
                != state.pendingFeedSourceStackIds.Count
            || !state.pendingFeedSourceStackIds.SequenceEqual(
                state.pendingFeedSourceStackIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal)
            || state.pendingFeedQuantity != 1
            || state.pendingFeedMassGrams <= 0L
            || string.IsNullOrWhiteSpace(state.pendingFeedItemId)
            || !IsFiniteRange(state.pendingFeedNutrition, 0f, 1f)
            || state.pendingFeedNutrition <= 0f
            || !IsFiniteRange(state.pendingFeedDiseaseChance, 0f, 1f)
            || state.pendingFeedDiseaseTriggered
                && state.pendingFeedDiseaseChance <= 0f
            || !IsFiniteRange(state.pendingFeedHungerTarget, 0f, 1f)
            || state.pendingFeedHealthTarget < 0
            || !IsFiniteRange(state.pendingFeedSicknessTarget, 0f, 100f))
        {
            return false;
        }
        string expectedCommit =
            $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Sink}:"
            + $"{state.pendingFeedOperationId}:"
            + $"{state.pendingFeedQuantity}:"
            + state.pendingFeedMassGrams;
        return string.Equals(
            state.pendingFeedCommitId,
            expectedCommit,
            StringComparison.Ordinal);
    }

    public static bool HasEmptyProvenance(CapturedWildlifeState state) =>
        state != null
        && state.pendingFeedPhase == CapturedWildlifeFeedCommitPhase.None
        && state.pendingFeedOperationSequence == 0
        && string.IsNullOrEmpty(state.pendingFeedOperationId)
        && string.IsNullOrEmpty(state.pendingFeedReasonCode)
        && string.IsNullOrEmpty(state.pendingFeedCommitId)
        && state.pendingFeedSourceStackIds != null
        && state.pendingFeedSourceStackIds.Count == 0
        && state.pendingFeedQuantity == 0
        && state.pendingFeedMassGrams == 0L
        && string.IsNullOrEmpty(state.pendingFeedItemId)
        && state.pendingFeedNutrition == 0f
        && state.pendingFeedDiseaseChance == 0f
        && !state.pendingFeedDiseaseTriggered
        && state.pendingFeedHungerTarget == 0f
        && state.pendingFeedHealthTarget == 0
        && state.pendingFeedSicknessTarget == 0f;

    private static bool TerminalMatches(
        CapturedWildlifeState state,
        ICapturedWildlifeFeedOutcomeTarget target) =>
        string.Equals(
            target.LastCaptiveFeedCommitId,
            state.pendingFeedCommitId,
            StringComparison.Ordinal)
        && string.Equals(
            state.lastFeedItemId,
            state.pendingFeedItemId,
            StringComparison.Ordinal)
        && Mathf.Approximately(
            state.lastFeedDiseaseChance,
            state.pendingFeedDiseaseChance)
        && Mathf.Approximately(
            state.feedSicknessSeverity,
            state.pendingFeedSicknessTarget)
        && string.Equals(
            state.lastCareStatus,
            WasteFeedOutcomeCode.FeedConsumed.ToString(),
            StringComparison.Ordinal);

    private static bool ReceiptMatches(
        CapturedWildlifeState state,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            state.pendingFeedOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            state.pendingFeedReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            state.pendingFeedCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == state.pendingFeedQuantity
        && receipt.InputMassGrams == state.pendingFeedMassGrams
        && receipt.SourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal)
            .SequenceEqual(
                state.pendingFeedSourceStackIds,
                StringComparer.Ordinal);

    private static void ClearPending(CapturedWildlifeState state)
    {
        state.pendingFeedOperationSequence = 0;
        state.pendingFeedPhase = CapturedWildlifeFeedCommitPhase.None;
        state.pendingFeedOperationId = string.Empty;
        state.pendingFeedReasonCode = string.Empty;
        state.pendingFeedCommitId = string.Empty;
        state.pendingFeedSourceStackIds.Clear();
        state.pendingFeedQuantity = 0;
        state.pendingFeedMassGrams = 0L;
        state.pendingFeedItemId = string.Empty;
        state.pendingFeedNutrition = 0f;
        state.pendingFeedDiseaseChance = 0f;
        state.pendingFeedDiseaseTriggered = false;
        state.pendingFeedHungerTarget = 0f;
        state.pendingFeedHealthTarget = 0;
        state.pendingFeedSicknessTarget = 0f;
    }

    private static bool IsFiniteRange(
        float value,
        float minimum,
        float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;
}
