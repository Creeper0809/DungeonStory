using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// The run-flow owner retains only its frozen terminal fact and opaque
// publication key. This adapter reconstructs and compares the observer-owned
// native identity before a current checkpoint can be restored.
public sealed class OwnerRunCompletionFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator
{
    public void Validate(
        DungeonGameSaveData save,
        DungeonGameRestoreReport report) =>
        Validate(
            save.sections.ToDictionary(
                row => row.sectionId,
                StringComparer.Ordinal),
            report);

    public void Validate(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections,
        DungeonGameRestoreReport report)
    {
        try
        {
            if (!sections.TryGetValue(
                    RunFlowSaveSection.Id,
                    out DungeonSaveSectionEnvelope section)
                || section == null
                || section.sectionVersion
                    != DungeonRunFlowSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "owner-run-completion-section-missing");
            }

            RunFlowSaveSection.ValidatePendingCompletionJson(
                section.payloadJson);
            DungeonRunFlowSaveData flow =
                DungeonSaveJson.Deserialize<DungeonRunFlowSaveData>(
                    section.payloadJson);
            if (flow?.version != DungeonRunFlowSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "owner-run-completion-flow-version-conflict");
            }
            var meta = Read<DungeonMetaProgressionSaveData>(sections,
                MetaProgressionSaveSection.Id, DungeonMetaProgressionSaveData.CurrentVersion);
            MetaProgressionSaveSection.ValidatePendingCompletionJson(
                sections[MetaProgressionSaveSection.Id].payloadJson);
            ValidateSettlementJoin(flow, meta);
            if (flow.pendingCompletion == null && !meta.hasPendingCompletion)
            {
                return;
            }

            DungeonCharacterWorldSaveData characters =
                Read<DungeonCharacterWorldSaveData>(
                    sections,
                    CharacterWorldSaveSection.Id,
                    CharacterWorldSaveSection.CurrentVersion);
            DungeonRunVariableSaveData runVariables =
                Read<DungeonRunVariableSaveData>(
                    sections,
                    RunVariableSaveSection.Id,
                    DungeonRunVariableSaveData.CurrentVersion);
            var completion = flow.pendingCompletion?.fact ?? meta.pendingCompletion.fact;
            ValidateOwnerJoin(completion, characters, runVariables);
            ValidatePending(
                flow,
                Read<GameplayOutcomeLedgerSaveData>(
                    sections,
                    GameplayOutcomeLedgerSaveSection.Id,
                    GameplayOutcomeLedgerSaveData.CurrentVersion),
                Read<DungeonMigratedProducerOutcomeSaveData>(
                    sections,
                    MigratedProducerOutcomeSaveSection.Id,
                    DungeonMigratedProducerOutcomeSaveData.CurrentVersion));
        }
        catch (Exception error) when (
            error is ArgumentException
                or InvalidOperationException
                or OverflowException
                or Newtonsoft.Json.JsonException)
        {
            report.AddError(
                "owner-run-completion-save-join:"
                + error.Message);
        }
    }

    public static void ValidateSettlementJoin(DungeonRunFlowSaveData flow, DungeonMetaProgressionSaveData meta)
    {
        if (flow == null || meta == null)
            throw new InvalidOperationException("owner-run-meta-state-missing");
        if (flow.pendingCompletion != null && !meta.hasPendingCompletion)
            throw new InvalidOperationException("owner-run-meta-frozen-settlement-missing");
        if (!meta.hasPendingCompletion) return;
        var saved = meta.pendingCompletion;
        if (saved?.fact == null || meta.ended || meta.hasLatestResult
            || saved.runSequence != checked(meta.completedRunCount + 1))
            throw new InvalidOperationException("owner-run-meta-pending-conflict");
        saved.fact.Validate();
        if (flow.phase != DungeonRunPhase.Finished || flow.outcome != saved.fact.outcome
            || flow.currentDay != saved.fact.absoluteDay)
            throw new InvalidOperationException("owner-run-meta-terminal-conflict");
        var raw = flow.pendingCompletion?.fact;
        if (raw != null && (raw.runSeed != saved.fact.runSeed || raw.ownerId != saved.fact.ownerId
            || raw.displayName != saved.fact.displayName || raw.reason != saved.fact.reason
            || raw.outcome != saved.fact.outcome || raw.absoluteDay != saved.fact.absoluteDay))
            throw new InvalidOperationException("owner-run-meta-fact-conflict");
    }

    public static void ValidatePending(
        DungeonRunFlowSaveData flow,
        GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (flow?.version != DungeonRunFlowSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                "owner-run-completion-flow-state-missing");
        }
        OwnerRunCompletionPendingData pending = flow.pendingCompletion;
        if (pending == null)
        {
            return;
        }
        pending.Validate(flow.outcome, flow.currentDay);
        if (incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null
            || counters?.owners == null)
        {
            throw new InvalidOperationException(
                "owner-run-completion-observation-state-missing");
        }

        const MigratedProducerOutcomeKind kind =
            MigratedProducerOutcomeKind.OwnerRunEndedEvent;
        string producer =
            MigratedProducerOutcomeCatalog.Get(kind).ProducerId;
        var canonical =
            new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(
                StringComparer.Ordinal);
        foreach (GameplayOutcomeSnapshot row in
                 incoming.outbox.Concat(incoming.exactOutcomes))
        {
            Add(RetiredCopy(row));
        }
        foreach (GameplayOutcomeTombstoneSnapshot row in incoming.tombstones)
        {
            Add(row);
        }

        Dictionary<string, long> savedCounters = counters.owners.ToDictionary(
            row => row.ownerKey,
            row => row.revision,
            StringComparer.Ordinal);
        var fact = new OwnerRunCompletionFact(pending.fact);
        MigratedDomainFactCapture capture =
            OwnerRunCompletionFactMapping.Capture(fact);
        if (capture.Kind != kind)
        {
            throw new InvalidOperationException(
                "owner-run-completion-kind-conflict");
        }

        string ownerKey = producer + "|" + capture.OwnerIdentity;
        string expectedKey = MigratedProducerOutcomeCatalog.ResultKey(
                kind,
                capture.OwnerIdentity,
                1L)
            .ToString();
        if (pending.publicationKey != expectedKey)
        {
            throw new InvalidOperationException(
                "owner-run-completion-key-conflict");
        }

        canonical.TryGetValue(expectedKey, out var actual);
        long revision = savedCounters.TryGetValue(
            ownerKey,
            out long savedRevision)
            ? savedRevision
            : 0L;
        if (actual != null
                ? revision != 1L
                : revision != 0L && revision != 1L)
        {
            throw new InvalidOperationException(
                "owner-run-completion-counter-conflict");
        }
        if (actual != null
            && (actual.absoluteDay != capture.AbsoluteDay
                || actual.ownerRevision != 1L
                || actual.commitRevision != 1L))
        {
            throw new InvalidOperationException(
                "owner-run-completion-proof-conflict");
        }

        var registry = new GameplayOutcomeRegistry(
            MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                (IGameplayOutcomeDescriptor)
                    new MigratedProducerOutcomeDescriptor(
                        value,
                        new KoreanJosaFormatter())),
            new IGameplayOutcomeAdapterRegistration[]
            {
                new MigratedProducerOutcomeAdapter()
            });
        var ledger = new GameplayOutcomeLedger(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 1,
                largePageCount: 1,
                knownResultKeyCapacity: 4,
                retainedSmallPageCount: 1,
                retainedLargePageCount: 1));
        var subset = new GameplayOutcomeLedgerSaveData
        {
            runId = incoming.runId,
            worldEpoch = incoming.worldEpoch,
            revision = incoming.revision,
            nextSequence = incoming.nextSequence
        };
        if (actual != null)
        {
            subset.tombstones.Add(actual);
        }
        subset.knownResultKeyHighWater = subset.tombstones.Count;
        var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
        ledger.BeginRestoreCandidate();
        ledger.PublishGameplayOutcomeRestore(candidate);
        ledger.PublishRestoreCandidate();
        ledger.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(
            ledger,
            registry,
            new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(
            new DungeonRuntimeAggregateRootStore(),
            ledger,
            recorder,
            ledger);
        migrated.Restore(migrated.PrepareRestore(counters));
        var observer = new MigratedDomainFactObserver<OwnerRunCompletionFact>(
            OwnerRunCompletionFactMapping.Capture,
            migrated,
            new GameplayDomainFactCommitGroup(recorder, ledger));
        bool accepted = observer.TryPrepareReplay(
            expectedKey,
            fact,
            out var prepared,
            out string failure);
        prepared?.Cancel();
        if (!accepted)
        {
            throw new InvalidOperationException(
                "owner-run-completion-canonical-fact-conflict:"
                + failure);
        }

        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null)
            {
                throw new InvalidOperationException(
                    "owner-run-completion-canonical-null");
            }
            if (row.producerId != producer)
            {
                return;
            }
            string key = new GameplayResultKey(
                    row.producerId,
                    new GameplayOperationId(row.operationId),
                    row.commitRevision,
                    row.localResultIndex)
                .ToString();
            if (row.runId != incoming.runId
                || row.sequence <= 0L
                || row.sequence >= incoming.nextSequence
                || !canonical.TryAdd(key, row))
            {
                throw new InvalidOperationException(
                    "owner-run-completion-canonical-identity-conflict");
            }
        }
    }

    private static void ValidateOwnerJoin(
        OwnerRunCompletionFactData fact,
        DungeonCharacterWorldSaveData characters,
        DungeonRunVariableSaveData runVariables)
    {
        fact.Validate();
        if (characters?.actors == null
            || runVariables?.version
                != DungeonRunVariableSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                "owner-run-completion-owner-state-missing");
        }

        DungeonCharacterSaveData[] owners = characters.actors
            .Where(actor => actor?.isOwner == true)
            .ToArray();
        if (owners.Length != 1
            || !string.Equals(
                owners[0].persistentId,
                fact.ownerId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "owner-run-completion-character-owner-conflict");
        }
        if (runVariables.runSeed != fact.runSeed)
        {
            throw new InvalidOperationException(
                "owner-run-completion-run-seed-conflict");
        }
    }

    private static T Read<T>(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> sections,
        string id,
        int version)
        where T : class
    {
        if (!sections.TryGetValue(id, out DungeonSaveSectionEnvelope section)
            || section == null
            || section.sectionVersion != version)
        {
            throw new InvalidOperationException(
                "owner-run-completion-section-missing:" + id);
        }
        return DungeonSaveJson.Deserialize<T>(section.payloadJson);
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(
        GameplayOutcomeSnapshot row)
    {
        if (row == null)
        {
            throw new InvalidOperationException(
                "owner-run-completion-canonical-null");
        }
        return new GameplayOutcomeTombstoneSnapshot
        {
            runId = row.runId,
            sequence = row.sequence,
            producerId = row.producerId,
            operationId = row.operationId,
            commitRevision = row.commitRevision,
            localResultIndex = row.localResultIndex,
            ownerRevision = row.ownerRevision,
            temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
            absoluteDay = row.absoluteDay,
            parentRunId = row.parentRunId,
            parentSequence = row.parentSequence,
            deliveryMetadataVersion = row.deliveryMetadataVersion,
            deliveryClockId = row.deliveryClockId,
            recordedAbsoluteHour = row.recordedAbsoluteHour,
            publishedRevision = row.publishedRevision,
            deliveryFaultCount = row.deliveryFaultCount,
            deliveryDelayReason = row.deliveryDelayReason,
            canonicalHash = row.immutablePayloadHash,
            terminalTier = NarrativeMemoryTier.Forgotten
        };
    }
}
