using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

/// <summary>Cross-section validation of a joint command/skill publication; no gameplay effects.</summary>
public sealed class OffenseBattleCommandFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var aggregate = DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(save, OffenseAggregateSaveSection.Id);
            if (aggregate.version != DungeonOffenseAggregateSaveData.CurrentVersion)
                throw new InvalidOperationException("battle-command-aggregate-version-invalid");
            Validate(aggregate.battleCommandPublications,
                DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
                DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);
            if (!report.Success) return;
            ValidateDomain(aggregate, DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(save, CharacterWorldSaveSection.Id));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("battle-command-domain-join:" + exception.Message); }
    }

    internal static void ValidateDomain(DungeonOffenseAggregateSaveData aggregate, DungeonCharacterWorldSaveData characters)
    {
        var journal = OffenseBattleCommandPublicationRuntime.ValidateAndClone(aggregate.battleCommandPublications);
        if (characters?.actors == null || aggregate.expedition?.activeExpeditions == null || aggregate.resultPublications?.observations == null)
            throw new InvalidOperationException("battle-command-domain-save-missing");
        foreach (var row in journal.observations)
        {
            var data = row.fact;
            bool unfinished = row.pending || row.followupStage != OffenseBattleCommandPublicationRuntime.FollowupsComplete;
            if (unfinished)
            {
                var battle = aggregate.expedition.activeBattle;
                if (!aggregate.expedition.hasActiveBattle || battle == null || battle.battleId != data.battleId
                    || battle.expeditionId != data.expeditionId || battle.lastProcessedCommandId != data.commandId
                    || battle.combatants == null || !battle.combatants.Any(value => value.persistentId == data.actorId)
                    || data.targetId != string.Empty && !battle.combatants.Any(value => value.persistentId == data.targetId))
                    throw new InvalidOperationException("battle-command-pending-battle-conflict");
                // After followup 3 a passive may legitimately change health again.
                if (row.followupStage < 3 && data.targetId != string.Empty
                    && battle.combatants.Single(value => value.persistentId == data.targetId).currentHealth != data.targetHealthAfter)
                    throw new InvalidOperationException("battle-command-pending-health-conflict");
            }
            if (!row.hasSkill) continue;
            var fact = new CharacterSkillActivationFact(row.skill);
            var actor = characters.actors.SingleOrDefault(value => value.persistentId == fact.SourceCharacterId);
            var limits = actor?.growth?.useLimits;
            if (unfinished && row.followupStage == 0)
            {
                if (limits == null || limits.activationSequence != fact.OwnerRevision
                    || limits.offenseBattleSerial != fact.TriggerSerial || !fact.MatchesUseLimit(limits.lastSkillActivation, row.skillPublicationKey)
                    || (limits.lastSkillActivation.observationPending
                        ? limits.lastSkillActivation.canonicalPayloadHash != string.Empty
                        : row.pending || limits.lastSkillActivation.canonicalPayloadHash != row.skillFingerprint))
                    throw new InvalidOperationException("battle-command-pending-skill-owner-conflict");
            }
            if (limits?.lastSkillActivation?.ownerRevision == fact.OwnerRevision
                && (!fact.MatchesUseLimit(limits.lastSkillActivation, row.skillPublicationKey)
                    || row.followupStage > 0 && (limits.lastSkillActivation.observationPending
                        || limits.lastSkillActivation.canonicalPayloadHash != row.skillFingerprint)))
                throw new InvalidOperationException("battle-command-skill-mirror-conflict");
        }
        foreach (var actor in characters.actors)
        {
            var last = actor?.growth?.useLimits?.lastSkillActivation;
            if (last == null || last.activationKind != CharacterSkillActivationKind.ManualCombatUltimate) continue;
            if (journal.observations.Count(row => row.hasSkill && (!last.observationPending || row.followupStage == 0)
                && row.skill.sourceCharacterId == actor.persistentId && row.skill.ownerRevision == last.ownerRevision
                && row.skillPublicationKey == last.resultKey) != 1)
                throw new InvalidOperationException("battle-command-skill-owner-orphan");
        }
    }

    internal static void Validate(OffenseBattleCommandPublicationSaveData source, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            var saved = OffenseBattleCommandPublicationRuntime.ValidateAndClone(source);
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null || counters?.owners == null)
                throw new InvalidOperationException("battle-command-canonical-save-missing");
            string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.OffenseBattleCommandResult).ProducerId;
            string skillProducer = CharacterSkillActivationPersistenceContract.ProducerId;
            var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var value in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (value == null) throw new InvalidOperationException("battle-command-canonical-null");
                if (value.producerId != producer && value.producerId != skillProducer) continue;
                Add(new GameplayOutcomeTombstoneSnapshot { runId = value.runId, sequence = value.sequence, producerId = value.producerId,
                    operationId = value.operationId, commitRevision = value.commitRevision, localResultIndex = value.localResultIndex,
                    ownerRevision = value.ownerRevision, canonicalHash = value.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten });
            }
            foreach (var value in incoming.tombstones)
            { if (value == null) throw new InvalidOperationException("battle-command-canonical-null"); if (value.producerId == producer || value.producerId == skillProducer) Add(value); }
            void Add(GameplayOutcomeTombstoneSnapshot value)
            {
                var key = new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex);
                if (value.commitRevision < 1 || value.ownerRevision != value.commitRevision || value.localResultIndex != 0
                    || value.runId != incoming.runId || value.sequence >= incoming.nextSequence || !canonical.TryAdd(key.ToString(), value))
                    throw new InvalidOperationException("battle-command-canonical-identity-conflict");
            }
            var descriptors = MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())).ToList();
            descriptors.Add(new CharacterSkillActivationOutcomeDescriptor());
            var registry = new GameplayOutcomeRegistry(descriptors,
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter(), new CharacterSkillActivationOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(smallPageCount: 1, largePageCount: 1,
                knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var migrated = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var commits = new GameplayDomainFactCommitGroup(recorder, ledger);
            var observer = new OffenseBattleCommandFactObserver(migrated, commits);
            var skillObserver = new GameplayDomainFactObserver<CharacterSkillActivationFact, CharacterSkillActivationOutcomeReceipt>(
                CharacterSkillActivationFactMapping.Capture, registry, ledger, recorder, commits);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var owners = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var row in saved.observations)
            {
                if (!GameplayDomainFactDeliveryQuery.TryReadKey(row.publicationKey, out var key)
                    || key.ProducerId != producer || !keys.Add(row.publicationKey))
                    throw new InvalidOperationException("battle-command-publication-key-conflict");
                string owner = producer + "|offense-battle:" + row.fact.battleId;
                long previous = owners.TryGetValue(owner, out long observed) ? observed : 0L;
                if (key.CommitRevision != previous + 1) throw new InvalidOperationException("battle-command-publication-revision-gap");
                owners[owner] = key.CommitRevision;
                bool exists = canonical.TryGetValue(row.publicationKey, out var commandCanonical);
                if (!exists && !row.pending) throw new InvalidOperationException("battle-command-delivered-canonical-missing");
                var subset = new GameplayOutcomeLedgerSaveData { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (exists) subset.tombstones.Add(commandCanonical);
                if (row.hasSkill)
                {
                    bool skillExists = canonical.TryGetValue(row.skillPublicationKey, out var skillCanonical);
                    if (exists != skillExists) throw new InvalidOperationException("battle-command-partial-cohort");
                    if (skillExists)
                    {
                        subset.tombstones.Add(skillCanonical);
                        if (!row.pending && (row.skillFingerprint != skillCanonical.canonicalHash
                            || row.skillPublicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(skillCanonical.runId), skillCanonical.sequence).ToString()))
                            throw new InvalidOperationException("battle-command-skill-proof-conflict");
                    }
                }
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate); ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                migrated.Restore(migrated.PrepareRestore(counters));
                bool accepted = observer.TryPrepareReplay(row.publicationKey, new OffenseBattleCommandFact(row.fact), out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("battle-command-canonical-payload-conflict:" + failure);
                if (row.hasSkill)
                {
                    accepted = skillObserver.TryPrepareReplay(row.skillPublicationKey, new CharacterSkillActivationFact(row.skill), out prepared, out failure);
                    prepared?.Cancel();
                    if (!accepted) throw new InvalidOperationException("battle-command-skill-payload-conflict:" + failure);
                }
            }
            if (canonical.Any(pair => pair.Value.producerId == producer && !keys.Contains(pair.Key))
                || counters.owners.Any(value => value == null || value.ownerKey == null
                    || value.ownerKey.StartsWith(producer + "|", StringComparison.Ordinal)
                        && (!owners.TryGetValue(value.ownerKey, out long expected) || value.revision < expected - 1L || value.revision > expected)))
                throw new InvalidOperationException("battle-command-canonical-owner-conflict");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("battle-command-save-join:" + exception.Message); }
    }
}
