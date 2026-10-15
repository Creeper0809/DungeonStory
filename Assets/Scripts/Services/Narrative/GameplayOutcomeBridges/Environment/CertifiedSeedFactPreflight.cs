using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Only this observer boundary joins frozen production facts to the canonical ledger.</summary>
public sealed class CertifiedSeedFactPreflight :
    IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<CertifiedSeedWorldSaveData>(save, CertifiedSeedSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(CertifiedSeedWorldSaveData seeds, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            if (seeds?.orders == null || seeds.orders.Any(order => order == null)
                || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("certified-seed-observation-save-shape-invalid");
            foreach (var order in seeds.orders)
            {
                var owner = order.outputPublication;
                bool applied = order.phase is CertifiedSeedOrderPhase.OutputPublished
                    or CertifiedSeedOrderPhase.OutputRestoredAwaitingInputAcknowledgement;
                if (applied != (owner?.observationExpected == true))
                    throw new InvalidOperationException("certified-seed-observation-phase-conflict");
                if (!applied)
                {
                    if (order.completionFact?.IsEmpty != true)
                        throw new InvalidOperationException("certified-seed-unapplied-order-has-fact");
                    continue;
                }
                if (!ProductionDomainOutputPublicationService.TryValidateCommittedOwner(owner, out string failure))
                    throw new InvalidOperationException("certified-seed-observation-owner-invalid:" + failure);
                var fact = CertifiedSeedRuntime.RestoreCompletionFact(order);
                ValidateCanonical(fact, owner.observationKey, owner, incoming);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("certified-seed-completion-save-join:" + exception.Message); }
    }

    private static void ValidateCanonical(CertifiedSeedCompletedFact fact, string publicationKey,
        ProductionDomainOutputPublicationSaveData owner, GameplayOutcomeLedgerSaveData incoming)
    {
        var receipt = CertifiedSeedFactObserverMapping.Capture(fact);
        GameplayResultKey resultKey = receipt.Payload.ResultKey;
        if (resultKey.ToString() != publicationKey)
            throw new InvalidOperationException("certified-seed-observer-key-conflict");
        if (string.IsNullOrEmpty(owner.observationId) != string.IsNullOrEmpty(owner.observationFingerprint)
            || owner.observationDelivered && string.IsNullOrEmpty(owner.observationId))
            throw new InvalidOperationException("certified-seed-publication-proof-incomplete");
        var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
            && value.producerId == resultKey.ProducerId
            && value.operationId == resultKey.OperationId.Value
            && value.commitRevision == resultKey.CommitRevision && value.localResultIndex == 0).ToArray();
        var retired = incoming.tombstones.Where(value => value != null
            && value.producerId == resultKey.ProducerId
            && value.operationId == resultKey.OperationId.Value
            && value.commitRevision == resultKey.CommitRevision && value.localResultIndex == 0).ToArray();
        if (rows.Length + retired.Length > 1)
            throw new InvalidOperationException("certified-seed-canonical-duplicate");
        bool exists = rows.Length + retired.Length == 1;
        if (!exists && (!string.IsNullOrEmpty(owner.observationId)
                || !string.IsNullOrEmpty(owner.observationFingerprint) || owner.observationDelivered))
            throw new InvalidOperationException("certified-seed-publication-proof-without-ledger");
        // Detached comparison against canonical payloads, never a live commit or gameplay replay.
        var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new CertifiedSeedCompletionOutcomeDescriptor(new KoreanJosaFormatter()) },
            new IGameplayOutcomeAdapterRegistration[] { new CertifiedSeedCompletionOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 1, retainedLargePageCount: 1));
        if (exists)
        {
            string run = rows.Length == 1 ? rows[0].runId : retired[0].runId;
            long sequence = rows.Length == 1 ? rows[0].sequence : retired[0].sequence;
            long revision = rows.Length == 1 ? rows[0].ownerRevision : retired[0].ownerRevision;
            string hash = rows.Length == 1 ? rows[0].immutablePayloadHash : retired[0].canonicalHash;
            string id = new GameplayOutcomeId(new GameplayOutcomeRunId(run), sequence).ToString();
            bool delivered = retired.Length == 1 || incoming.exactOutcomes.Contains(rows[0]);
            if (run != incoming.runId || sequence >= incoming.nextSequence || revision != resultKey.CommitRevision
                || rows.Length == 1 && rows[0].outcomeTypeId != EnvironmentOutcomeIds.CertifiedSeedCompleted.Value
                || !string.IsNullOrEmpty(owner.observationId) && owner.observationId != id
                || !string.IsNullOrEmpty(owner.observationFingerprint) && owner.observationFingerprint != hash
                || owner.observationDelivered && !delivered)
                throw new InvalidOperationException("certified-seed-publication-proof-conflict");
            var comparison = new GameplayOutcomeLedgerSaveData
            {
                runId = run, worldEpoch = incoming.worldEpoch, nextSequence = checked(sequence + 1),
                knownResultKeyHighWater = 1,
                tombstones = new() { new GameplayOutcomeTombstoneSnapshot
                {
                    runId = run, sequence = sequence, producerId = resultKey.ProducerId,
                    operationId = resultKey.OperationId.Value,
                    commitRevision = resultKey.CommitRevision, localResultIndex = 0, ownerRevision = revision,
                    terminalTier = NarrativeMemoryTier.Forgotten, canonicalHash = hash
                } }
            };
            var candidate = ledger.PrepareGameplayOutcomeRestore(comparison);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
        }
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var result = recorder.TryPrepare(receipt, out var token);
        recorder.CancelPrepared(token);
        if (exists ? result.Code != OutcomePrepareCode.AlreadyTerminal : !result.Success)
            throw new InvalidOperationException("certified-seed-canonical-payload-conflict:" + result.Code + ":" + result.DetailCode);
    }
}
