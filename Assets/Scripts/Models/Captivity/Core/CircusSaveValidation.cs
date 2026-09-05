using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CircusSaveValidation
{
    public const int MaximumOrders = 256;
    public const int MaximumCapturedWildlife = 512;
    public const int MaximumParticipantsPerGroup = 256;
    private const string OrderPrefix = "circus:";
    private const string FeedOperationPrefix = "captivity-wildlife-feed:";
    private const string FeedReasonCode = "captivity-wildlife-feed-consumed";
    private const string FeedConsumedStatus = "FeedConsumed";

    public static void Validate(
        CircusSaveData payload,
        CircusProgramRegistry programs,
        DungeonGameRestoreReport report)
    {
        if (programs == null)
        {
            throw new ArgumentNullException(nameof(programs));
        }
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (payload == null)
        {
            report.AddError("Circus payload is null.");
            return;
        }
        if (payload.version != CircusSaveData.CurrentVersion)
        {
            report.AddError($"Circus payload version {payload.version} is invalid.");
        }
        if (payload.nextOrderSequence < 0)
        {
            report.AddError("Circus order sequence cannot be negative.");
        }
        if (payload.orders == null || payload.capturedWildlife == null)
        {
            report.AddError("Circus payload is missing a required list.");
            return;
        }
        if (payload.orders.Count > MaximumOrders)
        {
            report.AddError($"Circus payload exceeds {MaximumOrders} orders.");
        }
        if (payload.capturedWildlife.Count > MaximumCapturedWildlife)
        {
            report.AddError(
                $"Circus payload exceeds {MaximumCapturedWildlife} captured wildlife.");
        }

        HashSet<string> orderIds = new(StringComparer.Ordinal);
        int highestOrderSequence = 0;
        foreach (CircusShowOrder order in payload.orders)
        {
            ValidateOrder(
                order,
                programs,
                orderIds,
                ref highestOrderSequence,
                report);
        }
        if (payload.nextOrderSequence < highestOrderSequence)
        {
            report.AddError(
                $"Circus order sequence {payload.nextOrderSequence} is below saved order sequence {highestOrderSequence}.");
        }

        HashSet<string> wildlifeIds = new(StringComparer.Ordinal);
        foreach (CapturedWildlifeState wildlife in payload.capturedWildlife)
        {
            ValidateCapturedWildlife(wildlife, orderIds, wildlifeIds, report);
        }

        foreach (CircusShowOrder order in payload.orders.Where(
                     order => order != null && !order.IsTerminal))
        {
            foreach (string wildlifeId in order.wildlifeIds)
            {
                if (!wildlifeIds.Contains(wildlifeId))
                {
                    report.AddError(
                        $"Circus order '{order.orderId}' references uncaptured wildlife '{wildlifeId}'.");
                }
            }
        }
    }

    internal static CircusAggregateState CreateCircusState(CircusSaveData payload)
    {
        CircusAggregateState state = new()
        {
            NextOrderSequence = payload.nextOrderSequence
        };
        foreach (CircusShowOrder source in payload.orders)
        {
            state.Orders.Add(CreateRestoredOrder(source));
        }
        return state;
    }

    internal static CapturedWildlifeAggregateState CreateCapturedWildlifeState(
        CircusSaveData payload)
    {
        CapturedWildlifeAggregateState state = new();
        foreach (CapturedWildlifeState source in payload.capturedWildlife)
        {
            CapturedWildlifeState restored = CreateRestoredCapturedWildlife(source);
            state.Captured.Add(restored.wildlifeId, restored);
        }
        return state;
    }

    public static CircusShowOrder CreateRestoredOrder(CircusShowOrder source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        CircusShowOrder restored = source.Clone();
        if (!restored.IsTerminal)
        {
            restored.state = CircusShowState.Composition;
            restored.preparationWorkCompleted = Math.Min(
                restored.preparationWorkCompleted,
                restored.preparationWorkRequired);
            restored.statusMessage = "circus.preparation-resumed";
        }
        return restored;
    }

    public static CapturedWildlifeState CreateRestoredCapturedWildlife(
        CapturedWildlifeState source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }
        CapturedWildlifeState restored = source.Clone();
        if (restored.transportState is CapturedWildlifeTransportState.AwaitingTransport
            or CapturedWildlifeTransportState.Transporting
            or CapturedWildlifeTransportState.MovingToShow
            or CapturedWildlifeTransportState.Performing
            or CapturedWildlifeTransportState.ReturningToPen)
        {
            restored.reservedCarrierId = string.Empty;
            restored.assignedShowOrderId = string.Empty;
            restored.transportState = CapturedWildlifeTransportState.Penned;
            restored.lastCareStatus = "circus.transport-reset-to-pen";
        }
        return restored;
    }

    internal static bool IsDoorCaptured(CapturedWildlifeState state)
    {
        return state != null
            && !state.escaped
            && state.transportState != CapturedWildlifeTransportState.Released;
    }

    private static void ValidateOrder(
        CircusShowOrder order,
        CircusProgramRegistry programs,
        ISet<string> orderIds,
        ref int highestOrderSequence,
        DungeonGameRestoreReport report)
    {
        string orderId = order?.orderId ?? string.Empty;
        if (order == null
            || !TryParseOrderId(orderId, out int sequence)
            || !orderIds.Add(orderId))
        {
            report.AddError($"Circus payload contains invalid order '{orderId}'.");
            return;
        }
        highestOrderSequence = Math.Max(highestOrderSequence, sequence);

        if (!IsCanonical(order.stageId)
            || !new BuildingInstanceId(order.stageId).IsValid
            || order.roomId < 0
            || !IsCanonical(order.programId)
            || !programs.TryGet(order.programId, out _)
            || !Enum.IsDefined(typeof(CircusLethalityPolicy), order.lethality)
            || !Enum.IsDefined(typeof(CircusShowState), order.state)
            || order.statusMessage == null)
        {
            report.AddError($"Circus order '{orderId}' has invalid identity or enum data.");
        }

        ValidateParticipantGroup(orderId, "performer", order.performerIds,
            order.performerPositions, report);
        ValidateParticipantGroup(orderId, "wildlife", order.wildlifeIds,
            order.wildlifePositions, report);
        ValidateParticipantGroup(orderId, "audience", order.audienceIds,
            order.audiencePositions, report);

        if (order.performerIds != null
            && order.audienceIds != null
            && order.performerIds.Intersect(
                order.audienceIds,
                StringComparer.Ordinal).Any())
        {
            report.AddError(
                $"Circus order '{orderId}' assigns a character as both performer and audience.");
        }

        if (!IsFiniteNonNegative(order.preparationWorkRequired)
            || order.preparationWorkRequired < 1f
            || !IsFiniteNonNegative(order.preparationWorkCompleted)
            || order.preparationWorkCompleted > order.preparationWorkRequired
            || !IsFiniteNonNegative(order.elapsedShowSeconds)
            || !IsFiniteNonNegative(order.showDurationSeconds)
            || order.showDurationSeconds < 5f
            || !IsFiniteNonNegative(order.nextCombatExchangeAt)
            || !IsFiniteNonNegative(order.phaseElapsedSeconds)
            || order.ticketPrice < 1
            || order.revenue < 0
            || !IsFiniteRange(order.satisfaction, 0f, 100f)
            || !IsFinite(order.venueSatisfactionBonus)
            || !IsFinite(order.venueAccidentRiskBonus)
            || !IsFiniteNonNegative(order.venueAccidentDamageMultiplier)
            || !IsFiniteNonNegative(order.venueFilthMultiplier)
            || !IsFinite(order.venueWitnessMoodPenalty)
            || !IsFinite(order.venueGamblingVariance))
        {
            report.AddError($"Circus order '{orderId}' has invalid numeric state.");
        }
        if (order.venueFlatRevenuePerAudience < 0)
        {
            report.AddError(
                $"Circus order '{orderId}' has negative venue revenue.");
        }
        ValidateSupplyState(order, orderId, report);
    }

    private static void ValidateSupplyState(
        CircusShowOrder order,
        string orderId,
        DungeonGameRestoreReport report)
    {
        bool empty = order.pendingSupplyPhase == CircusShowSupplyCommitPhase.None;
        if (order.nextSupplyOperationSequence <= 0
            || !Enum.IsDefined(typeof(CircusShowSupplyCommitPhase), order.pendingSupplyPhase)
            || empty && (order.pendingSupplyOperationSequence != 0
                || !string.IsNullOrEmpty(order.pendingSupplyOperationId)
                || !string.IsNullOrEmpty(order.pendingSupplyCommitId)
                || order.pendingSupplySourceStackIds == null
                || order.pendingSupplySourceStackIds.Count != 0
                || order.pendingSupplyQuantity != 0
                || order.pendingSupplyMassGrams != 0)
            || !empty && (order.pendingSupplyOperationSequence != order.nextSupplyOperationSequence
                || order.pendingSupplyQuantity != 1
                || order.pendingSupplyMassGrams !=
                    CircusPerformanceSupplyContracts
                        .PerformancePropBoxMassGrams
                || order.pendingSupplySourceStackIds == null
                || order.pendingSupplySourceStackIds.Count == 0
                || string.IsNullOrWhiteSpace(order.pendingSupplyOperationId)
                || string.IsNullOrWhiteSpace(order.pendingSupplyCommitId)
                || string.IsNullOrWhiteSpace(order.pendingSupplyCartStackId)
                || order.pendingSupplyCartDurabilityBefore <= order.pendingSupplyCartDurabilityAfter
                || Math.Abs(
                    order.pendingSupplyCartDurabilityBefore
                    - order.pendingSupplyCartDurabilityAfter
                    - CircusPerformanceSupplyContracts.BanquetCartWearPerShow)
                    >= 0.001d
                || order.pendingSupplyCartDurabilityAfter < 0f)
            || order.preparationSuppliesCommitted != !string.IsNullOrEmpty(order.preparationSupplyCommitId))
        {
            report.AddError($"Circus order '{orderId}' has invalid supply outbox state.");
        }
    }

    private static void ValidateParticipantGroup(
        string orderId,
        string group,
        List<string> ids,
        List<UnityEngine.Vector2Int> positions,
        DungeonGameRestoreReport report)
    {
        if (ids == null
            || positions == null
            || ids.Count > MaximumParticipantsPerGroup
            || positions.Count != ids.Count)
        {
            report.AddError(
                $"Circus order '{orderId}' has invalid {group} collection sizes.");
            return;
        }
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string id in ids)
        {
            if (!IsCanonical(id) || !unique.Add(id))
            {
                report.AddError(
                    $"Circus order '{orderId}' has invalid {group} '{id}'.");
            }
        }
    }

    private static void ValidateCapturedWildlife(
        CapturedWildlifeState wildlife,
        ISet<string> orderIds,
        ISet<string> wildlifeIds,
        DungeonGameRestoreReport report)
    {
        string wildlifeId = wildlife?.wildlifeId ?? string.Empty;
        if (wildlife == null
            || !IsCanonical(wildlifeId)
            || !wildlifeIds.Add(wildlifeId)
            || !IsCanonical(wildlife.speciesId)
            || !IsCanonical(wildlife.penId)
            || !new BuildingInstanceId(wildlife.penId).IsValid
            || wildlife.reservedCarrierId == null
            || wildlife.assignedShowOrderId == null
            || wildlife.lastFeedItemId == null
            || wildlife.lastCareStatus == null
            || !Enum.IsDefined(
                typeof(CapturedWildlifeTransportState),
                wildlife.transportState))
        {
            report.AddError(
                $"Circus payload contains invalid captured wildlife '{wildlifeId}'.");
            return;
        }

        bool carrierRequired = wildlife.transportState is
            CapturedWildlifeTransportState.AwaitingTransport
            or CapturedWildlifeTransportState.Transporting;
        bool showRequired = wildlife.transportState is
            CapturedWildlifeTransportState.MovingToShow
            or CapturedWildlifeTransportState.Performing
            or CapturedWildlifeTransportState.ReturningToPen;
        bool escaped = wildlife.transportState == CapturedWildlifeTransportState.Escaped;
        if (wildlife.transportState == CapturedWildlifeTransportState.Released
            || carrierRequired != IsCanonical(wildlife.reservedCarrierId)
            || showRequired != IsCanonical(wildlife.assignedShowOrderId)
            || showRequired && !orderIds.Contains(wildlife.assignedShowOrderId)
            || escaped != wildlife.escaped
            || carrierRequired && IsCanonical(wildlife.assignedShowOrderId)
            || showRequired && IsCanonical(wildlife.reservedCarrierId))
        {
            report.AddError(
                $"Captured wildlife '{wildlifeId}' has incoherent transport state.");
        }

        if (!IsFiniteNonNegative(wildlife.nextCareAt)
            || !IsFiniteRange(wildlife.escapeRisk, 0f, 100f)
            || !IsFiniteRange(wildlife.feedSicknessSeverity, 0f, 100f)
            || !IsFiniteRange(wildlife.lastFeedDiseaseChance, 0f, 1f))
        {
            report.AddError(
                $"Captured wildlife '{wildlifeId}' has invalid numeric state.");
        }
        ValidateCapturedWildlifeFeed(wildlife, report);
    }

    private static void ValidateCapturedWildlifeFeed(
        CapturedWildlifeState wildlife,
        DungeonGameRestoreReport report)
    {
        if (wildlife.nextFeedOperationSequence < 0
            || !Enum.IsDefined(
                typeof(CapturedWildlifeFeedCommitPhase),
                wildlife.pendingFeedPhase)
            || wildlife.pendingFeedSourceStackIds == null)
        {
            report.AddError(
                $"Captured wildlife '{wildlife.wildlifeId}' has invalid feed sequence or collection state.");
            return;
        }

        if (wildlife.pendingFeedPhase == CapturedWildlifeFeedCommitPhase.None)
        {
            if (wildlife.pendingFeedOperationSequence != 0
                || !string.IsNullOrEmpty(wildlife.pendingFeedOperationId)
                || !string.IsNullOrEmpty(wildlife.pendingFeedReasonCode)
                || !string.IsNullOrEmpty(wildlife.pendingFeedCommitId)
                || wildlife.pendingFeedSourceStackIds.Count != 0
                || wildlife.pendingFeedQuantity != 0
                || wildlife.pendingFeedMassGrams != 0L
                || !string.IsNullOrEmpty(wildlife.pendingFeedItemId)
                || wildlife.pendingFeedNutrition != 0f
                || wildlife.pendingFeedDiseaseChance != 0f
                || wildlife.pendingFeedDiseaseTriggered
                || wildlife.pendingFeedHungerTarget != 0f
                || wildlife.pendingFeedHealthTarget != 0
                || wildlife.pendingFeedSicknessTarget != 0f)
            {
                report.AddError(
                    $"Captured wildlife '{wildlife.wildlifeId}' has orphan feed provenance without a pending phase.");
            }
            return;
        }

        string expectedOperation = FeedOperationPrefix
            + wildlife.wildlifeId
            + ":"
            + wildlife.pendingFeedOperationSequence.ToString("D8");
        string expectedCommit = "physical-batch-disposition:3:"
            + expectedOperation
            + ":1:"
            + wildlife.pendingFeedMassGrams;
        bool sourcesCanonical = wildlife.pendingFeedSourceStackIds.Count > 0
            && wildlife.pendingFeedSourceStackIds.All(IsCanonical)
            && wildlife.pendingFeedSourceStackIds
                .Distinct(StringComparer.Ordinal).Count()
                == wildlife.pendingFeedSourceStackIds.Count
            && wildlife.pendingFeedSourceStackIds.SequenceEqual(
                wildlife.pendingFeedSourceStackIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal);
        bool structurallyValid =
            wildlife.pendingFeedOperationSequence > 0
            && wildlife.pendingFeedOperationSequence
                == wildlife.nextFeedOperationSequence
            && string.Equals(
                wildlife.pendingFeedOperationId,
                expectedOperation,
                StringComparison.Ordinal)
            && string.Equals(
                wildlife.pendingFeedReasonCode,
                FeedReasonCode,
                StringComparison.Ordinal)
            && string.Equals(
                wildlife.pendingFeedCommitId,
                expectedCommit,
                StringComparison.Ordinal)
            && sourcesCanonical
            && wildlife.pendingFeedQuantity == 1
            && wildlife.pendingFeedMassGrams > 0L
            && IsCanonical(wildlife.pendingFeedItemId)
            && IsFiniteRange(wildlife.pendingFeedNutrition, 0f, 1f)
            && wildlife.pendingFeedNutrition > 0f
            && IsFiniteRange(wildlife.pendingFeedDiseaseChance, 0f, 1f)
            && (!wildlife.pendingFeedDiseaseTriggered
                || wildlife.pendingFeedDiseaseChance > 0f)
            && IsFiniteRange(wildlife.pendingFeedHungerTarget, 0f, 1f)
            && wildlife.pendingFeedHealthTarget >= 0
            && IsFiniteRange(wildlife.pendingFeedSicknessTarget, 0f, 100f);
        if (!structurallyValid)
        {
            report.AddError(
                $"Captured wildlife '{wildlife.wildlifeId}' has invalid pending feed provenance.");
            return;
        }

        if (wildlife.pendingFeedPhase
                == CapturedWildlifeFeedCommitPhase.CarePublished
            && (!string.Equals(
                    wildlife.lastFeedItemId,
                    wildlife.pendingFeedItemId,
                    StringComparison.Ordinal)
                || !Mathf.Approximately(
                    wildlife.lastFeedDiseaseChance,
                    wildlife.pendingFeedDiseaseChance)
                || !Mathf.Approximately(
                    wildlife.feedSicknessSeverity,
                    wildlife.pendingFeedSicknessTarget)
                || !string.Equals(
                    wildlife.lastCareStatus,
                    FeedConsumedStatus,
                    StringComparison.Ordinal)))
        {
            report.AddError(
                $"Captured wildlife '{wildlife.wildlifeId}' has a feed publication phase without its terminal state.");
        }
    }

    private static bool TryParseOrderId(string value, out int sequence)
    {
        sequence = 0;
        return IsCanonical(value)
            && value.StartsWith(OrderPrefix, StringComparison.Ordinal)
            && int.TryParse(value.Substring(OrderPrefix.Length), out sequence)
            && sequence > 0
            && string.Equals(value, OrderPrefix + sequence, StringComparison.Ordinal);
    }

    private static bool IsCanonical(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsFiniteNonNegative(float value) =>
        IsFinite(value) && value >= 0f;

    private static bool IsFiniteRange(float value, float minimum, float maximum) =>
        IsFinite(value) && value >= minimum && value <= maximum;
}
