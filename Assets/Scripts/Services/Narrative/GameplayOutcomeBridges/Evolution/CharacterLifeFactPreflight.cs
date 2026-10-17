using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Only observation code joins opaque daily/child facts to ledger identities.</summary>
public sealed class CharacterLifeFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<CharacterLifeWorldSaveData>(save, CharacterLifeSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(CharacterLifeWorldSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            if (source?.version != CharacterLifeWorldSaveData.CurrentVersion || source.pendingAgeConditionAnatomyProjections == null
                || source.characters == null || source.lastAdvancedAbsoluteDay < 0 || incoming?.outbox == null
                || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
                throw new InvalidOperationException("life-observation-save-shape-invalid");
            CharacterLifeObservationState.Validate(source.pendingDailyObservation);
            var pending = source.pendingDailyObservation;
            if (pending.entries.Count > 0 && pending.absoluteDay != source.lastAdvancedAbsoluteDay)
                throw new InvalidOperationException("life-observation-day-conflict");
            string ageProducer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent).ProducerId;
            string stageProducer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent).ProducerId;
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("life-canonical-null");
                if (Relevant(row.producerId)) Add(new GameplayOutcomeTombstoneSnapshot
                { runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
                    commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten });
            }
            foreach (var row in incoming.tombstones)
            { if (row == null) throw new InvalidOperationException("life-canonical-null"); if (Relevant(row.producerId)) Add(row); }
            bool Relevant(string producer) => producer == ageProducer || producer == stageProducer || producer == AgeConditionAnatomyOutcomeIds.ProducerId;
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex);
                if (row.runId != incoming.runId || row.sequence >= incoming.nextSequence || row.ownerRevision != row.commitRevision
                    || row.commitRevision <= 0 || row.localResultIndex != 0 || !rows.TryAdd(key.ToString(), row))
                    throw new InvalidOperationException("life-canonical-identity-conflict");
            }
            var descriptors = MigratedProducerOutcomeCatalog.Definitions.Select(definition =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(definition, new KoreanJosaFormatter()))
                .Append(new AgeConditionAnatomyOutcomeDescriptor());
            var registry = new GameplayOutcomeRegistry(descriptors,
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter(), new AgeConditionAnatomyOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(smallPageCount: 1, largePageCount: 1,
                knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var parentObserver = new MigratedDomainFactObserver<CharacterLifeTransitionFact>(CharacterLifeTransitionFactMapping.Capture,
                migrated, new GameplayDomainFactCommitGroup(recorder, ledger));
            void RestoreRow(GameplayOutcomeTombstoneSnapshot row)
            {
                var subset = new GameplayOutcomeLedgerSaveData { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (row != null) { subset.tombstones.Add(row); subset.knownResultKeyHighWater = 1; }
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate); ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
            }
            bool CheckParent(CharacterLifeTransitionFact fact, string key, string requiredId)
            {
                rows.TryGetValue(key, out var canonical);
                if (requiredId != null && (canonical == null || requiredId != new GameplayOutcomeId(new GameplayOutcomeRunId(canonical.runId), canonical.sequence).ToString()))
                    throw new InvalidOperationException("age-anatomy-durable-parent-missing");
                RestoreRow(canonical);
                migrated.Restore(migrated.PrepareRestore(counters));
                bool accepted = parentObserver.TryPrepareReplay(key, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("life-parent-payload-conflict:" + failure);
                return canonical != null;
            }
            int committed = 0;
            foreach (var row in pending.entries)
                if (CheckParent(new CharacterLifeTransitionFact(row.fact), row.publicationKey, null)) committed++;
            if (committed != 0 && committed != pending.entries.Count)
                throw new InvalidOperationException("life-daily-cohort-partial-commit");
            var domainIds = new HashSet<string>(pending.entries.Select(row => new CharacterLifeTransitionFact(row.fact).DomainId), StringComparer.Ordinal);
            var childIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var projection in source.pendingAgeConditionAnatomyProjections)
            {
                if (projection == null || !childIds.Add(projection.operationId) || projection.absoluteDay > source.lastAdvancedAbsoluteDay)
                    throw new InvalidOperationException("age-anatomy-projection-identity-invalid");
                var parent = AgeConditionAnatomyAppliedFact.Parent(projection);
                if (projection.operationId != parent.AnatomyOperationId || domainIds.Contains(parent.DomainId))
                    throw new InvalidOperationException("age-anatomy-parent-not-finalized");
                CheckParent(parent, projection.parentPublicationKey, projection.parentPublicationId);
                var key = new GameplayResultKey(AgeConditionAnatomyOutcomeIds.ProducerId, new GameplayOperationId(projection.operationId), 1, 0);
                rows.TryGetValue(key.ToString(), out var canonical);
                if (projection.phase == (int)AgeConditionAnatomyProjectionPhase.Pending)
                {
                    if (canonical != null || projection.publicationKey != string.Empty || projection.characterDisplayName != string.Empty)
                        throw new InvalidOperationException("age-anatomy-unapplied-canonical-conflict");
                    continue;
                }
                var fact = new AgeConditionAnatomyAppliedFact(projection);
                if (projection.publicationKey == null || projection.publicationKey.Length > 0 && projection.publicationKey != key.ToString()
                    || canonical != null && projection.publicationKey.Length == 0)
                    throw new InvalidOperationException("age-anatomy-observation-key-conflict");
                RestoreRow(canonical);
                var preparedResult = recorder.TryPrepare(AgeConditionAnatomyFactMapping.Capture(fact), out var token);
                recorder.CancelPrepared(token);
                if (canonical == null ? !preparedResult.Success : preparedResult.Code != OutcomePrepareCode.AlreadyTerminal)
                    throw new InvalidOperationException("age-anatomy-canonical-payload-conflict:" + preparedResult.DetailCode);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("life-observation-save-join:" + exception.Message); }
    }
}
