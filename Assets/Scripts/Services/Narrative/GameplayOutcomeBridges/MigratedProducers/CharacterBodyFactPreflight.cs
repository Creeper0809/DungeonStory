using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

public sealed class CharacterBodyFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterBodyHealthSaveData>(save, CharacterBodyHealthSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(DungeonCharacterBodyHealthSaveData body, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            if (body?.characters == null || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("body-observation-save-shape-invalid");
            CharacterHealingFact.ValidatePending(body.pendingHealingFacts);
            CharacterFatalityFact.ValidatePending(body.pendingFatalityFacts);
            if (body.pendingHealingFacts.Count + body.pendingFatalityFacts.Count == 0) return;
            var owners = new HashSet<string>(body.pendingHealingFacts.Select(value => value.fact.characterId), StringComparer.Ordinal);
            foreach (var pending in body.pendingFatalityFacts)
            {
                if (!owners.Add(pending.fact.characterId))
                    throw new InvalidOperationException("body-observation-pending-owner-conflict");
                var state = body.characters.SingleOrDefault(value => value?.characterId == pending.fact.characterId);
                if (state == null || state.currentHealth != 0f)
                    throw new InvalidOperationException("fatality-observation-owner-missing-or-alive");
            }
            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                    (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 3, largePageCount: 3, knownResultKeyCapacity: 6,
                retainedSmallPageCount: 3, retainedLargePageCount: 3));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var group = new GameplayDomainFactCommitGroup(recorder, ledger);
            var critical = new MigratedDomainFactObserver<CharacterCriticalHealthTransitionFact>(
                CharacterHealingFactMapping.CaptureCritical, migrated, group);
            var recovered = new MigratedDomainFactObserver<CharacterMobilityRecoveredFact>(
                CharacterHealingFactMapping.CaptureRecovered, migrated, group);
            var death = new MigratedDomainFactObserver<CharacterFatalityFact>(
                CharacterFatalityFactMapping.CaptureDeath, migrated, group);
            var fatalCritical = new MigratedDomainFactObserver<CharacterFatalCriticalTransitionFact>(
                CharacterFatalityFactMapping.CaptureCritical, migrated, group);
            var downed = new MigratedDomainFactObserver<CharacterFatalDownedFact>(
                CharacterFatalityFactMapping.CaptureDowned, migrated, group);
            var cohorts = new List<List<Member>>();
            foreach (var pending in body.pendingHealingFacts)
            {
                var fact = new CharacterHealingFact(pending.fact);
                var cohort = new List<Member>();
                if (pending.criticalPublicationKey.Length > 0)
                    AddMember(cohort, pending.criticalPublicationKey, new CharacterCriticalHealthTransitionFact(fact), critical);
                if (pending.recoveryPublicationKey.Length > 0)
                    AddMember(cohort, pending.recoveryPublicationKey, new CharacterMobilityRecoveredFact(fact), recovered);
                cohorts.Add(cohort);
            }
            foreach (var pending in body.pendingFatalityFacts)
            {
                var fact = new CharacterFatalityFact(pending.fact);
                var cohort = new List<Member>();
                AddMember(cohort, pending.deathPublicationKey, fact, death);
                if (pending.criticalPublicationKey.Length > 0)
                    AddMember(cohort, pending.criticalPublicationKey, new CharacterFatalCriticalTransitionFact(fact), fatalCritical);
                if (pending.downedPublicationKey.Length > 0)
                    AddMember(cohort, pending.downedPublicationKey, new CharacterFatalDownedFact(fact), downed);
                cohorts.Add(cohort);
            }
            string[] keys = cohorts.SelectMany(value => value.Select(member => member.Key)).ToArray();
            var needed = new HashSet<string>(keys, StringComparer.Ordinal);
            if (needed.Count != keys.Length)
                throw new InvalidOperationException("body-observation-pending-key-duplicate");
            var identities = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
                if (row != null) Add(new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId,
                    operationId = row.operationId, commitRevision = row.commitRevision,
                    localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
            foreach (var row in incoming.tombstones) if (row != null) Add(row);
            foreach (var cohort in cohorts)
            {
                int present = cohort.Count(member => identities.ContainsKey(member.Key));
                if (present != 0 && present != cohort.Count)
                    throw new InvalidOperationException("body-canonical-cohort-torn");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                foreach (var member in cohort) if (identities.TryGetValue(member.Key, out var row)) subset.tombstones.Add(row);
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                var prepared = new List<IDomainFactPreparedPublication>();
                try
                {
                    foreach (var member in cohort)
                    {
                        bool accepted = member.Prepare(out var publication, out string failure);
                        if (publication != null) prepared.Add(publication);
                        if (!accepted || publication == null)
                            throw new InvalidOperationException("body-observation-canonical-conflict:" + failure);
                    }
                }
                finally { foreach (var publication in prepared) publication.Cancel(); }
            }

            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
                    row.commitRevision, row.localResultIndex).ToString();
                if (needed.Contains(key) && !identities.TryAdd(key, row))
                    throw new InvalidOperationException("body-canonical-identity-duplicate");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("body-observation-save-join:" + exception.Message); }
    }

    private static void AddMember<T>(List<Member> cohort, string key, T fact, IDurableDomainFactRecoveryObserver<T> observer) =>
        cohort.Add(new Member(key, (out IDomainFactPreparedPublication publication, out string failure) =>
            observer.TryPrepareReplay(key, fact, out publication, out failure)));

    private delegate bool Prepare(out IDomainFactPreparedPublication publication, out string failure);
    private sealed class Member
    {
        internal Member(string key, Prepare prepare) { Key = key; Prepare = prepare; }
        internal string Key { get; }
        internal Prepare Prepare { get; }
    }
}
