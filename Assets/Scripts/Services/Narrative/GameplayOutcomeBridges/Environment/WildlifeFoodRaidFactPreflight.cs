using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// Native child/parent identity and payload validation belong to observation, never the raid owner.
public sealed class WildlifeFoodRaidFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            Validate(DungeonSaveSectionPayload.ReadOrNew<DungeonWildlifeSaveData>(save, WildlifeSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(save, PhysicalItemsSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("wildlife-food-raid-save-join:" + exception.Message); }
    }

    public static void Validate(DungeonWildlifeSaveData wildlife, DungeonPhysicalItemSaveData physical,
        GameplayOutcomeLedgerSaveData incoming)
    {
        if (wildlife?.foodRaidOrders == null || physical?.pendingBatchDispositions == null
            || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("food-raid-save-state-missing");
        var pending = physical.pendingBatchDispositions.Where(row => row != null
            && (row.reasonCode == "wildlife-food-raid-consumed"
                || row.operationId?.StartsWith("wildlife-food-raid:", StringComparison.Ordinal) == true)).ToArray();
        if (wildlife.foodRaidOrders.Any(row => !WildlifeFoodRaidDispositionOutbox.HasValidShape(row)))
            throw new InvalidOperationException("food-raid-owner-shape-invalid");
        if (wildlife.foodRaidOrders.All(row => row.commitPhase == WildlifeFoodRaidCommitPhase.None))
        {
            if (pending.Length != 0) throw new InvalidOperationException("food-raid-orphan-physical-pending");
            return;
        }
        var joined = new HashSet<string>(StringComparer.Ordinal);
        var publications = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var josa = new KoreanJosaFormatter();
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { new WildlifeFoodRaidOutcomeDescriptor(josa) },
            new IGameplayOutcomeAdapterRegistration[] { new WildlifeFoodRaidOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        foreach (var order in wildlife.foodRaidOrders)
        {
            if (!WildlifeFoodRaidDispositionOutbox.HasValidShape(order))
                throw new InvalidOperationException("food-raid-owner-shape-invalid");
            if (order.commitPhase == WildlifeFoodRaidCommitPhase.None) continue;
            if (!operations.Add(order.dispositionOperationId)
                || (order.publicationKey.Length > 0 && !publications.Add(order.publicationKey)))
                throw new InvalidOperationException("food-raid-owner-identity-duplicate");
            var fact = new WildlifeFoodRaidFact(order.fact);
            var childKey = WildlifeFoodRaidFactMapping.PhysicalChildKey(order.fact);
            var parentKey = new GameplayResultKey(EnvironmentOutcomeIds.WildlifeFoodRaidProducer,
                new GameplayOperationId(order.dispositionOperationId), order.dispositionOwnerRevision, 0);
            if (order.publicationKey.Length > 0 && order.publicationKey != parentKey.ToString())
                throw new InvalidOperationException("food-raid-parent-key-conflict");
            var matches = pending.Where(row => row.operationId == order.dispositionOperationId).ToArray();
            if (WildlifeFoodRaidDispositionOutbox.IsPending(order))
            {
                if (matches.Length != 1) throw new InvalidOperationException("food-raid-physical-pending-missing-or-duplicate");
                var value = matches[0];
                if (value.kind != (int)PhysicalItemDispositionKind.Sink
                    || value.reasonCode != order.dispositionReasonCode || value.commitId != order.dispositionCommitId
                    || value.quantity != order.dispositionQuantity || value.inputMassGrams != order.dispositionInputMassGrams
                    || value.ownerRevision != order.dispositionOwnerRevision || value.sourceStackIds == null
                    || !value.sourceStackIds.SequenceEqual(order.dispositionSourceStackIds, StringComparer.Ordinal)
                    || !value.publicationExpected || value.publicationKey != childKey.ToString())
                    throw new InvalidOperationException("food-raid-physical-pending-conflict");
                joined.Add(value.operationId);
            }
            else if (matches.Length != 0)
                throw new InvalidOperationException("food-raid-acknowledged-physical-pending");

            var child = Find(childKey, out _);
            var parent = Find(parentKey, out bool delivered);
            if (child == null)
            {
                // The physical debit itself may have an unknown ACK before its observer commits.
                if (parent != null || order.publicationKey.Length > 0
                    || order.commitPhase != WildlifeFoodRaidCommitPhase.ItemCommitted)
                    throw new InvalidOperationException("food-raid-physical-child-missing");
                continue;
            }
            if (parent != null && order.publicationKey.Length == 0)
                throw new InvalidOperationException("food-raid-parent-owner-key-missing");
            if (parent == null && (order.publicationId.Length > 0 || order.publicationFingerprint.Length > 0
                || order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged))
                throw new InvalidOperationException("food-raid-parent-proof-missing");
            if (parent != null)
            {
                string id = new GameplayOutcomeId(new GameplayOutcomeRunId(parent.runId), parent.sequence).ToString();
                if ((order.publicationId.Length > 0 && order.publicationId != id)
                    || (order.publicationFingerprint.Length > 0 && order.publicationFingerprint != parent.canonicalHash)
                    || (order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged && !delivered)
                    || parent.sequence <= child.sequence)
                    throw new InvalidOperationException("food-raid-parent-proof-conflict");
            }
            var receipt = WildlifeFoodRaidFactMapping.CaptureWithChild(fact,
                new GameplayOutcomeId(new GameplayOutcomeRunId(child.runId), child.sequence));
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            subset.tombstones.Add(child);
            if (parent != null) subset.tombstones.Add(parent);
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            var prepared = recorder.TryPrepare(receipt, out var token);
            recorder.CancelPrepared(token);
            if (parent == null ? !prepared.Success : prepared.Code != OutcomePrepareCode.AlreadyTerminal)
                throw new InvalidOperationException("food-raid-canonical-fact-conflict:" + prepared.DetailCode);
        }
        if (pending.Any(row => !joined.Contains(row.operationId)))
            throw new InvalidOperationException("food-raid-orphan-physical-pending");

        GameplayOutcomeTombstoneSnapshot Find(GameplayResultKey key, out bool delivered)
        {
            var exact = incoming.exactOutcomes.Where(Matches).ToArray();
            var outbox = incoming.outbox.Where(Matches).ToArray();
            var retired = incoming.tombstones.Where(row => row != null
                && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value).ToArray();
            if (exact.Length + outbox.Length + retired.Length > 1)
                throw new InvalidOperationException("food-raid-canonical-duplicate");
            delivered = exact.Length == 1 || retired.Length == 1;
            GameplayOutcomeTombstoneSnapshot value = retired.FirstOrDefault();
            var live = exact.Concat(outbox).FirstOrDefault();
            if (live != null)
                value = new GameplayOutcomeTombstoneSnapshot
                {
                    runId = live.runId, sequence = live.sequence, producerId = live.producerId,
                    operationId = live.operationId, commitRevision = live.commitRevision, localResultIndex = live.localResultIndex,
                    ownerRevision = live.ownerRevision, temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
                    absoluteDay = live.absoluteDay, parentRunId = live.parentRunId, parentSequence = live.parentSequence,
                    deliveryMetadataVersion = live.deliveryMetadataVersion, deliveryClockId = live.deliveryClockId,
                    recordedAbsoluteHour = live.recordedAbsoluteHour, publishedRevision = live.publishedRevision,
                    deliveryFaultCount = live.deliveryFaultCount, deliveryDelayReason = live.deliveryDelayReason,
                    canonicalHash = live.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                };
            if (value != null && (value.runId != incoming.runId || value.sequence < 0 || value.sequence >= incoming.nextSequence
                || value.commitRevision != key.CommitRevision || value.ownerRevision != key.CommitRevision
                || value.localResultIndex != 0 || string.IsNullOrEmpty(value.canonicalHash)))
                throw new InvalidOperationException("food-raid-canonical-identity-conflict");
            // The comparison ledger normalizes detached copies, never the caller's save graph.
            return value == null ? null : new GameplayOutcomeTombstoneSnapshot
            {
                runId = value.runId, sequence = value.sequence, producerId = value.producerId,
                operationId = value.operationId, commitRevision = value.commitRevision, localResultIndex = value.localResultIndex,
                ownerRevision = value.ownerRevision, temporalMetadataVersion = value.temporalMetadataVersion,
                absoluteDay = value.absoluteDay, parentRunId = value.parentRunId, parentSequence = value.parentSequence,
                deliveryMetadataVersion = value.deliveryMetadataVersion, deliveryClockId = value.deliveryClockId,
                recordedAbsoluteHour = value.recordedAbsoluteHour, publishedRevision = value.publishedRevision,
                deliveryFaultCount = value.deliveryFaultCount, deliveryDelayReason = value.deliveryDelayReason,
                canonicalHash = value.canonicalHash, terminalTier = value.terminalTier
            };
            bool Matches(GameplayOutcomeSnapshot row) => row != null
                && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value;
        }
    }
}
