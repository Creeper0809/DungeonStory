using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>Only observation infrastructure interprets opaque skill publication identities.</summary>
public sealed class CharacterSkillActivationFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var journal = DungeonSaveSectionPayload.ReadOrNew<CharacterSkillActivationPublicationSaveData>(save, CharacterSkillActivationPublicationSaveSection.Id);
            var battle = DungeonSaveSectionPayload.ReadOrNew<DungeonOffenseAggregateSaveData>(save, OffenseAggregateSaveSection.Id).battleCommandPublications;
            ValidateCanonical(journal, DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), battle, report);
            if (report.Success) ValidateDomain(journal, battle,
                DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterWorldSaveData>(save, CharacterWorldSaveSection.Id));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("skill-publication-domain-join:" + exception.Message); }
    }

    internal static void ValidateCanonical(CharacterSkillActivationPublicationSaveData source, GameplayOutcomeLedgerSaveData incoming,
        OffenseBattleCommandPublicationSaveData battle, DungeonGameRestoreReport report)
    {
        try
        {
            var saved = CharacterSkillActivationPublicationRuntime.ValidateAndClone(source);
            var joint = OffenseBattleCommandPublicationRuntime.ValidateAndClone(battle);
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("skill-publication-canonical-missing");
            var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var value in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (value == null) throw new InvalidOperationException("skill-publication-canonical-null");
                if (value.producerId != CharacterSkillActivationOutcomeIds.ProducerId) continue;
                Add(new GameplayOutcomeTombstoneSnapshot { runId = value.runId, sequence = value.sequence, producerId = value.producerId,
                    operationId = value.operationId, commitRevision = value.commitRevision, localResultIndex = value.localResultIndex,
                    ownerRevision = value.ownerRevision, canonicalHash = value.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten });
            }
            foreach (var value in incoming.tombstones)
            {
                if (value == null) throw new InvalidOperationException("skill-publication-canonical-null");
                if (value.producerId == CharacterSkillActivationOutcomeIds.ProducerId) Add(value);
            }
            void Add(GameplayOutcomeTombstoneSnapshot value)
            {
                var key = new GameplayResultKey(value.producerId, new GameplayOperationId(value.operationId), value.commitRevision, value.localResultIndex);
                if (value.commitRevision < 1 || value.ownerRevision != value.commitRevision || value.localResultIndex != 0
                    || value.runId != incoming.runId || value.sequence >= incoming.nextSequence || !canonical.TryAdd(key.ToString(), value))
                    throw new InvalidOperationException("skill-publication-canonical-identity-conflict");
            }
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[] { new CharacterSkillActivationOutcomeDescriptor() },
                new IGameplayOutcomeAdapterRegistration[] { new CharacterSkillActivationOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(smallPageCount: 1, largePageCount: 1,
                knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<CharacterSkillActivationFact, CharacterSkillActivationOutcomeReceipt>(
                CharacterSkillActivationFactMapping.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger));
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in saved.observations)
            {
                var fact = new CharacterSkillActivationFact(row.fact);
                var receipt = CharacterSkillActivationFactMapping.Capture(fact);
                if (receipt.ResultKey.ToString() != row.publicationKey || !keys.Add(row.publicationKey))
                    throw new InvalidOperationException("skill-publication-key-conflict");
                bool exists = canonical.TryGetValue(row.publicationKey, out var evidence);
                if (!exists && !row.pending) throw new InvalidOperationException("skill-publication-delivered-canonical-missing");
                if (exists && !row.pending && (row.fingerprint != evidence.canonicalHash
                    || row.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(evidence.runId), evidence.sequence).ToString()))
                    throw new InvalidOperationException("skill-publication-proof-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (exists) subset.tombstones.Add(evidence);
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate); ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(row.publicationKey, fact, out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("skill-publication-canonical-payload-conflict:" + failure);
            }
            foreach (var row in joint.observations.Where(row => row.hasSkill))
                if (!keys.Add(row.skillPublicationKey)) throw new InvalidOperationException("skill-publication-multiple-owners");
            if (canonical.Keys.Any(key => !keys.Contains(key))) throw new InvalidOperationException("skill-publication-canonical-orphan");
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("skill-publication-canonical-join:" + exception.Message); }
    }

    internal static void ValidateDomain(CharacterSkillActivationPublicationSaveData source,
        OffenseBattleCommandPublicationSaveData battle, DungeonCharacterWorldSaveData characters)
    {
        var saved = CharacterSkillActivationPublicationRuntime.ValidateAndClone(source);
        var joint = OffenseBattleCommandPublicationRuntime.ValidateAndClone(battle);
        if (characters?.actors == null || characters.actors.Any(value => value == null))
            throw new InvalidOperationException("skill-publication-characters-missing");
        var all = saved.observations.Select(row => (fact: new CharacterSkillActivationFact(row.fact), key: row.publicationKey))
            .Concat(joint.observations.Where(row => row.hasSkill).Select(row => (fact: new CharacterSkillActivationFact(row.skill), key: row.skillPublicationKey)))
            .ToArray();
        if (all.Select(value => (value.fact.SourceCharacterId, value.fact.OwnerRevision)).Distinct().Count() != all.Length)
            throw new InvalidOperationException("skill-publication-owner-revision-duplicate");
        foreach (var row in saved.observations)
        {
            var fact = new CharacterSkillActivationFact(row.fact);
            var actor = characters.actors.SingleOrDefault(value => value.persistentId == fact.SourceCharacterId);
            var limits = actor?.growth?.useLimits;
            if (!row.ownerFinalized && (limits == null || limits.activationSequence != fact.OwnerRevision))
                throw new InvalidOperationException("skill-publication-unfinished-owner-missing");
            if (limits?.activationSequence != fact.OwnerRevision) continue;
            var last = limits.lastSkillActivation;
            if (!fact.MatchesUseLimit(last, row.publicationKey)
                || (last.observationPending ? row.ownerFinalized || last.canonicalPayloadHash != string.Empty
                    : row.pending || last.canonicalPayloadHash != row.fingerprint))
                throw new InvalidOperationException("skill-publication-latest-mirror-conflict");
        }
        foreach (var actor in characters.actors)
        {
            var limits = actor?.growth?.useLimits;
            if (limits == null) continue;
            if (limits.automaticSkillActivations == null || limits.manualSkillCooldowns == null
                || limits.automaticSkillActivations.Any(value => value == null)
                || limits.manualSkillCooldowns.Any(value => value == null))
                throw new InvalidOperationException("skill-publication-use-history-missing");
            var history = all.Where(value => value.fact.SourceCharacterId == actor.persistentId)
                .OrderBy(value => value.fact.OwnerRevision).ToArray();
            if (history.LongLength != limits.activationSequence
                || history.Where((value, index) => value.fact.OwnerRevision != index + 1L).Any())
                throw new InvalidOperationException("skill-publication-owner-history-gap");
            foreach (var automatic in limits.automaticSkillActivations)
            {
                var matching = history.SingleOrDefault(value => value.fact.OwnerRevision == automatic.ownerRevision).fact;
                if (matching == null || matching.TriggerEventKeySha256 != automatic.eventKeySha256
                    || matching.TriggerEventId != automatic.eventId || matching.SkillId != automatic.skillId
                    || matching.ActivationKind != automatic.activationKind || matching.UseCountAfter != automatic.useCount)
                    throw new InvalidOperationException("skill-publication-automatic-history-conflict");
            }
            if (history.Count(value => value.fact.ActivationKind is CharacterSkillActivationKind.AutomaticPassive
                or CharacterSkillActivationKind.AutomaticUltimate or CharacterSkillActivationKind.ManualCombatUltimate)
                != limits.automaticSkillActivations.Count)
                throw new InvalidOperationException("skill-publication-automatic-history-missing");
            foreach (var cooldown in limits.manualSkillCooldowns)
            {
                var latest = history.LastOrDefault(value => value.fact.SkillId == cooldown.skillId
                    && value.fact.ActivationKind is CharacterSkillActivationKind.ManualDungeon or CharacterSkillActivationKind.ManualExpedition).fact;
                if (latest == null ? cooldown.useCount != 0 : latest.UseCountAfter != cooldown.useCount
                    || latest.ReadyAbsoluteHourAfter != cooldown.readyAbsoluteHour)
                    throw new InvalidOperationException("skill-publication-manual-cooldown-conflict");
            }
            foreach (var manual in history.Where(value => value.fact.ActivationKind is CharacterSkillActivationKind.ManualDungeon
                or CharacterSkillActivationKind.ManualExpedition))
                if (!limits.manualSkillCooldowns.Any(value => value.skillId == manual.fact.SkillId))
                    throw new InvalidOperationException("skill-publication-manual-cooldown-missing");
        }
    }
}
