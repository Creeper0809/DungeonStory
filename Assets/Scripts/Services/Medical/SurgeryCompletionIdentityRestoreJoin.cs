using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Detached current-save joins; historical doctors need not be live actors.</summary>
public static class SurgeryCompletionIdentityRestoreJoin
{
    public static void Validate(IEnumerable<SurgeryOrder> orders,
        IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> deliveries,
        GameplayOutcomeLedgerSaveData ledger)
    {
        if (orders == null || ledger?.outbox == null || ledger.exactOutcomes == null
            || ledger.tombstones == null)
            throw new InvalidOperationException("Surgery completion join requires its saved owners and ledger.");
        SurgeryResolvedFactRestoreJoin.Validate(orders, ledger);
        var cursors = WorkCompletionIdentityDeliveryLedger.ValidateAndClone(deliveries)
            .ToDictionary(value => value.producerStreamId, StringComparer.Ordinal);
        var exact = ledger.outbox.Concat(ledger.exactOutcomes).Where(value => value != null)
            .ToLookup(value => (value.producerId, value.operationId, value.commitRevision, value.localResultIndex));
        var tombstones = ledger.tombstones.Where(value => value != null)
            .ToLookup(value => (value.producerId, value.operationId, value.commitRevision, value.localResultIndex));

        foreach (SurgeryOrder order in orders)
        {
            if (order == null) throw new InvalidOperationException("Surgery completion join contains a null order.");
            if (!order.resolutionFactAccepted) continue;
            WorkCompletionIdentityDeliveryRequest request = SurgeryWorkCompletionFactMapping.Capture(
                new SurgeryWorkCompletedFact(order.resolvedFact));
            GameplayOutcomeSnapshot child = RequireCommitted(SurgeryOutcomeIds.ResultKey(order.orderId),
                order.completionAbsoluteDay);
            if (child != null && (child.outcomeTypeId != SurgeryOutcomeIds.Resolved.Value
                || child.status != (order.resultSucceeded ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.Failed)
                || !Fact(child, SurgeryOutcomeIds.Order.Value, order.orderId)
                || !Fact(child, SurgeryOutcomeIds.Result.Value, order.resultOutcomeId)
                || !child.facts.Any(value => value.factId == SurgeryOutcomeIds.Procedure.Value
                    && value.value.StartsWith(order.procedureId + "|", StringComparison.Ordinal)
                    && value.value.Split('|').Contains("doctor=" + order.completionDoctorId))))
                throw new InvalidOperationException("Surgery frozen completion differs from its clinical child: " + order.orderId);

            GameplayResultKey identityKey = WorkCompletionIdentityOutcomeIds.ResultKey(request);
            var identityTuple = (identityKey.ProducerId, identityKey.OperationId.Value,
                identityKey.CommitRevision, identityKey.LocalResultIndex);
            bool identityKnown = exact[identityTuple].Any() || tombstones[identityTuple].Any();
            bool started = order.completionPublicationKey.Length != 0;
            if (started && order.completionPublicationKey != identityKey.ToString())
                throw new InvalidOperationException("Surgery completion publication key differs: " + order.orderId);
            if (!cursors.TryGetValue(request.ProducerStreamId, out var cursor))
            {
                if (order.completionFactDelivered || identityKnown)
                    throw new InvalidOperationException("Surgery completion ACK/outcome has no delivery cursor: " + order.orderId);
                continue; // Legitimate clinical commit before the identity command starts.
            }
            if (!started || cursor.operationSequence != request.OperationSequence || cursor.deliveryId != request.DeliveryId
                || cursor.payloadFingerprint != request.PayloadFingerprint)
                throw new InvalidOperationException("Surgery completion owner and delivery cursor differ: " + order.orderId);
            if (order.completionFactDelivered && (!cursor.notificationAttempted
                || order.completionPublicationId != request.DeliveryId
                || order.completionFingerprint != request.PayloadFingerprint))
                throw new InvalidOperationException("Surgery completion proof and observer cursor differ: " + order.orderId);
            if (cursor.disposition == WorkCompletionIdentityDeliveryDisposition.TerminalRecipientUnavailable)
            {
                if (identityKnown)
                    throw new InvalidOperationException("Surgery unavailable recipient also has an applied identity outcome: " + order.orderId);
                continue;
            }
            bool awaitingObservation = cursor.disposition == WorkCompletionIdentityDeliveryDisposition.EffectsAppliedPendingObservation;
            if (awaitingObservation)
            {
                if (order.completionFactDelivered)
                    throw new InvalidOperationException("Surgery completion ACK precedes its identity observation: " + order.orderId);
                if (!identityKnown) continue;
            }
            if (!awaitingObservation && cursor.disposition != WorkCompletionIdentityDeliveryDisposition.EffectsAppliedWithOutcome)
                throw new InvalidOperationException("Surgery completion has an unsupported delivery disposition: " + order.orderId);
            GameplayOutcomeSnapshot identity = RequireCommitted(identityKey, order.completionAbsoluteDay);
            if (identity != null && (identity.outcomeTypeId != WorkCompletionIdentityOutcomeIds.Applied.Value
                || !identity.participants.Any(value => value.roleId == WorkCompletionIdentityOutcomeIds.WorkerRole.Value
                    && value.entityId == order.completionDoctorId)
                || !identity.provenance.Any(value => value.kindId == "identity-payload-fingerprint"
                    && value.value == request.PayloadFingerprint)
                || !Fact(identity, WorkCompletionIdentityOutcomeIds.WorkIdFact.Value, request.WorkId)
                || !Fact(identity, WorkCompletionIdentityOutcomeIds.ProductIdFact.Value, request.ProductId)
                || !Fact(identity, WorkCompletionIdentityOutcomeIds.OriginFact.Value,
                    WorkCompletionIdentityOutcomeIds.StableOrigin(request.Origin))
                || !Fact(identity, WorkCompletionIdentityOutcomeIds.ProducerStreamFact.Value, request.ProducerStreamId)
                || !Fact(identity, WorkCompletionIdentityOutcomeIds.OperationIdFact.Value, request.OperationId)
                || !Fact(identity, WorkCompletionIdentityOutcomeIds.TerminalOwnerFact.Value, request.TerminalOwnerId)
                || !Fact(identity, WorkCompletionIdentityOutcomeIds.ChildResultKeyFact.Value, request.ChildResultKey)
                || !identity.subjects.Any(value => value.entityKindId == WorkCompletionIdentityOutcomeIds.BuildingKind.Value
                    && value.entityId == request.TargetEntityId)
                || !identity.metrics.Any(value => value.metricId == WorkCompletionIdentityOutcomeIds.ContributionMetric.Value
                    && Math.Abs(value.value - request.ContributionWorkUnits) < 0.0001d)))
                throw new InvalidOperationException("Surgery completion owner and applied identity outcome differ: " + order.orderId);
        }

        GameplayOutcomeSnapshot RequireCommitted(GameplayResultKey key, int day)
        {
            var tuple = (key.ProducerId, key.OperationId.Value, key.CommitRevision, key.LocalResultIndex);
            GameplayOutcomeSnapshot[] rows = exact[tuple].ToArray();
            GameplayOutcomeTombstoneSnapshot[] retired = tombstones[tuple].ToArray();
            if (rows.Length + retired.Length != 1
                || rows.Length == 1 && rows[0].absoluteDay != day
                || retired.Length == 1 && retired[0].absoluteDay != day)
                throw new InvalidOperationException("Surgery completion has no unique committed outcome with its frozen date: " + key);
            // Preserve the ledger's existing compacted/forgotten replay contract.
            // Tombstones have identity/date, not recoverable doctor/fact payloads.
            return rows.Length == 1 ? rows[0] : null;
        }
    }

    private static bool Fact(GameplayOutcomeSnapshot row, string id, string expected) =>
        row.facts.Any(value => value.factId == id && value.value == expected);
}
