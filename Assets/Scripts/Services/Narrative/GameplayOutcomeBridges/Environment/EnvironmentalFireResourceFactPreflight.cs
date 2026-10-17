using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Observer-side canonical proof join. The fire and item owners do not interpret ledger keys.</summary>
public sealed class EnvironmentalFireResourceFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            if (!DungeonSaveSectionPayload.TryRead(save, EnvironmentalFireSaveSection.Id, out DungeonEnvironmentalFireSaveData fire)
                || !DungeonSaveSectionPayload.TryRead(save, PhysicalItemsSaveSection.Id, out DungeonPhysicalItemSaveData physical)
                || !DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger))
                throw new InvalidOperationException("fire-resource-required-section-missing");
            Validate(fire, physical, ledger, report);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Newtonsoft.Json.JsonException)
        { report.AddError("fire-resource-save-join:" + exception.Message); }
    }

    internal static void Validate(DungeonEnvironmentalFireSaveData fire, DungeonPhysicalItemSaveData physical,
        GameplayOutcomeLedgerSaveData incoming, DungeonGameRestoreReport report)
    {
        try
        {
            if (fire == null || fire.version != DungeonEnvironmentalFireSaveData.CurrentVersion
                || fire.activeFires == null || fire.history == null || fire.suppressionOperations == null
                || fire.activeFires.Any(value => value == null) || fire.history.Any(value => value?.fire == null)
                || fire.suppressionOperations.Any(value => value == null))
                throw new InvalidOperationException("fire-resource-owner-image-invalid");
            EnvironmentalFireResourceTransaction.Validate(fire?.resourceOperations);
            if (physical?.pendingBatchDispositions == null) throw new InvalidOperationException("fire-resource-physical-missing");
            var pending = physical.pendingBatchDispositions.Where(value => value != null
                && (value.reasonCode == EnvironmentalFireWorldAdapter.FuelLossSinkReasonCode
                    || value.reasonCode == EnvironmentalFireWorldAdapter.WaterSinkReasonCode))
                .ToDictionary(value => value.operationId, StringComparer.Ordinal);
            if (pending.Count != fire.resourceOperations.Count)
                throw new InvalidOperationException("fire-resource-debit-journal-count-conflict");
            if (pending.Count == 0) return;
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("fire-resource-ledger-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            var undelivered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null || !Relevant(row.producerId)) continue;
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
                Add(key, GameplayOutcomeSnapshotCodec.CaptureTombstoneIdentity(row));
                if (incoming.outbox.Contains(row)) undelivered.Add(key);
            }
            foreach (var row in incoming.tombstones)
                if (row != null && Relevant(row.producerId))
                    Add(new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString(), row);

            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
                { new FireFuelOutcomeDescriptor(new KoreanJosaFormatter()), new FireWaterOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new EnvironmentalFireFuelOutcomeAdapter(), new EnvironmentalFireWaterOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var group = new GameplayDomainFactCommitGroup(recorder, ledger);
            var fuelObserver = new GameplayDomainFactObserver<EnvironmentalFireFuelLostFact, EnvironmentalFireFuelOutcomeReceipt>(
                EnvironmentalFireResourceFactMapping.Capture, registry, ledger, recorder, group);
            var waterObserver = new GameplayDomainFactObserver<EnvironmentalFireWaterConsumedFact, EnvironmentalFireWaterOutcomeReceipt>(
                EnvironmentalFireResourceFactMapping.Capture, registry, ledger, recorder, group);
            foreach (var value in fire.resourceOperations)
            {
                if (!pending.TryGetValue(value.physical.operationId, out var debit) || !debit.publicationExpected
                    || debit.defaultObservationDay != 0 || debit.publicationKey != value.publicationKey
                    || debit.commitId != value.physical.commitId
                    || !EnvironmentalFireResourceTransaction.MatchesPhysical(value, PhysicalItemDisposedFact.FromSave(
                        WithFactDay(debit, value.absoluteDay)).Receipt))
                    throw new InvalidOperationException("fire-resource-physical-fact-conflict");
                ValidateFireOwner(value, fire);
                string expected = value.use == EnvironmentalFireResourceUse.FuelLoss
                    ? EnvironmentalFireResourceFactMapping.Capture(new EnvironmentalFireFuelLostFact(value)).Payload.ResultKey.ToString()
                    : EnvironmentalFireResourceFactMapping.Capture(new EnvironmentalFireWaterConsumedFact(value)).Payload.ResultKey.ToString();
                if (value.publicationKey != expected) throw new InvalidOperationException("fire-resource-publication-key-conflict");
                bool exists = rows.TryGetValue(expected, out var canonical);
                if ((!exists && (!string.IsNullOrEmpty(debit.publicationId) || !string.IsNullOrEmpty(debit.publicationFingerprint) || debit.publicationDelivered))
                    || (exists && (canonical.ownerRevision != debit.ownerRevision
                        || !string.IsNullOrEmpty(debit.publicationId) && debit.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(canonical.runId), canonical.sequence).ToString()
                        || !string.IsNullOrEmpty(debit.publicationFingerprint) && debit.publicationFingerprint != canonical.canonicalHash
                        || debit.publicationDelivered && undelivered.Contains(expected))))
                    throw new InvalidOperationException("fire-resource-publication-proof-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch,
                    revision = incoming.revision, nextSequence = incoming.nextSequence };
                if (exists) subset.tombstones.Add(canonical);
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var restored = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(restored);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                DungeonStory.Foundation.Facts.IDomainFactPreparedPublication prepared;
                string failure;
                bool accepted = value.use == EnvironmentalFireResourceUse.FuelLoss
                    ? fuelObserver.TryPrepareReplay(expected, new EnvironmentalFireFuelLostFact(value), out prepared, out failure)
                    : waterObserver.TryPrepareReplay(expected, new EnvironmentalFireWaterConsumedFact(value), out prepared, out failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("fire-resource-canonical-payload-conflict:" + failure);
            }
            void Add(string key, GameplayOutcomeTombstoneSnapshot row)
            { if (!rows.TryAdd(key, row)) throw new InvalidOperationException("fire-resource-canonical-identity-duplicate"); }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("fire-resource-save-join:" + exception.Message); }
    }

    private static bool Relevant(string producer) => producer == EnvironmentOutcomeIds.FireFuelProducer || producer == EnvironmentOutcomeIds.FireWaterProducer;
    private static PhysicalItemBatchDispositionSaveData WithFactDay(PhysicalItemBatchDispositionSaveData value, int day)
    {
        var copy = new EnvironmentalFireResourceRecord { physical = value }.Clone().physical;
        copy.defaultObservationDay = day;
        return copy;
    }
    private static void ValidateFireOwner(EnvironmentalFireResourceRecord value, DungeonEnvironmentalFireSaveData save)
    {
        var fires = save.activeFires.Concat(save.history.Select(entry => entry.fire));
        if (value.use == EnvironmentalFireResourceUse.FuelLoss)
        {
            var owner = fires.SingleOrDefault(row => row.fuelLossOperationId == value.physical.operationId);
            if (owner == null || owner.fuelLossAcknowledged || owner.fuelLossTargetKind != (int)EnvironmentalFireTargetKind.ItemStack
                || owner.fuelLossTargetId != value.targetId
                || owner.fuelLossQuantity != value.physical.quantity
                || owner.fuelLossCommitId != value.physical.commitId || owner.fuelLossMassGrams != value.physical.inputMassGrams)
                throw new InvalidOperationException("fire-resource-fuel-owner-conflict");
        }
        else
        {
            var owner = save.suppressionOperations.SingleOrDefault(row => row.operationId == value.physical.operationId);
            var fire = fires.SingleOrDefault(row => row.fireId == value.fireId);
            if (owner == null || fire == null || owner.waterAcknowledged || owner.mode != (int)EnvironmentalFireSuppressionMode.Water
                || owner.fireId != value.fireId || owner.workerId != value.workerId || owner.waterLeaseId != value.leaseId
                || owner.waterCommittedQuantity != value.physical.quantity || owner.waterCommitId != value.physical.commitId
                || fire.positionX != value.positionX || fire.positionY != value.positionY)
                throw new InvalidOperationException("fire-resource-water-owner-conflict");
        }
    }
}
