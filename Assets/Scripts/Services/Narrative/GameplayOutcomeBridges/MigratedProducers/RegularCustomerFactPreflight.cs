using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// Pending regular-customer observations retain only their opaque publication
// key. This adapter reconstructs the observer-owned native identity and payload
// from the frozen domain fact before a save can be restored.
public sealed class RegularCustomerFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator
{
    private static readonly HashSet<MigratedProducerOutcomeKind> AllowedKinds =
        new()
        {
            MigratedProducerOutcomeKind.RegularCustomerVisitResult,
            MigratedProducerOutcomeKind.RegularCustomerRecruitResult,
            MigratedProducerOutcomeKind.RegularCustomerRecruitDeliveryResult
        };

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
                    RegularCustomerSaveSection.Id,
                    out DungeonSaveSectionEnvelope section)
                || section == null
                || section.sectionVersion
                    != DungeonRegularCustomerSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "regular-customer-observations-section-missing");
            }

            RegularCustomerSaveSection.ValidatePendingFactJson(
                section.payloadJson);
            DungeonRegularCustomerSaveData owners =
                DungeonSaveJson.Deserialize<DungeonRegularCustomerSaveData>(
                    section.payloadJson);
            ValidatePending(
                owners,
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
                "regular-customer-observations-save-join:"
                + error.Message);
        }
    }

    public static void ValidatePending(
        DungeonRegularCustomerSaveData owners,
        GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters)
    {
        if (owners?.version != DungeonRegularCustomerSaveData.CurrentVersion
            || owners.pendingFacts == null
            || incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null
            || counters?.owners == null)
        {
            throw new InvalidOperationException(
                "regular-customer-observations-state-missing");
        }

        foreach (RegularCustomerPendingFactData row in owners.pendingFacts)
        {
            if (row == null)
            {
                throw new InvalidOperationException(
                    "regular-customer-observations-row-missing");
            }
            row.Validate();
        }
        if (owners.pendingFacts.Count == 0)
        {
            return;
        }

        HashSet<string> producers = AllowedKinds
            .Select(kind => MigratedProducerOutcomeCatalog.Get(kind).ProducerId)
            .ToHashSet(StringComparer.Ordinal);
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
        var publicationKeys = new HashSet<string>(StringComparer.Ordinal);
        var ownerKeys = new HashSet<string>(StringComparer.Ordinal);
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
                knownResultKeyCapacity: Math.Max(4, owners.pendingFacts.Count),
                retainedSmallPageCount: 1,
                retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(
            ledger,
            registry,
            new GameEventBus());
        var migrated = new MigratedProducerOutcomeRuntime(
            new DungeonRuntimeAggregateRootStore(),
            ledger,
            recorder,
            ledger);
        var observer = new MigratedDomainFactObserver<RegularCustomerFact>(
            RegularCustomerFactMapping.Capture,
            migrated,
            new GameplayDomainFactCommitGroup(recorder, ledger));

        foreach (RegularCustomerPendingFactData row in owners.pendingFacts)
        {
            var fact = new RegularCustomerFact(row.fact);
            MigratedDomainFactCapture capture =
                RegularCustomerFactMapping.Capture(fact);
            if (!AllowedKinds.Contains(capture.Kind))
            {
                throw new InvalidOperationException(
                    "regular-customer-observations-kind-conflict");
            }

            string producer =
                MigratedProducerOutcomeCatalog.Get(capture.Kind).ProducerId;
            string ownerKey = producer + "|" + capture.OwnerIdentity;
            string expectedKey = MigratedProducerOutcomeCatalog.ResultKey(
                    capture.Kind,
                    capture.OwnerIdentity,
                    1L)
                .ToString();
            if (!publicationKeys.Add(row.publicationKey)
                || !ownerKeys.Add(ownerKey)
                || row.publicationKey != expectedKey)
            {
                throw new InvalidOperationException(
                    "regular-customer-observations-key-conflict");
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
                    "regular-customer-observations-counter-conflict");
            }
            if (actual != null
                && (actual.absoluteDay != capture.AbsoluteDay
                    || actual.ownerRevision != 1L
                    || actual.commitRevision != 1L))
            {
                throw new InvalidOperationException(
                    "regular-customer-observations-proof-conflict");
            }

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
            migrated.Restore(migrated.PrepareRestore(counters));

            bool accepted = observer.TryPrepareReplay(
                expectedKey,
                fact,
                out var prepared,
                out string failure);
            prepared?.Cancel();
            if (!accepted)
            {
                throw new InvalidOperationException(
                    "regular-customer-observations-canonical-fact-conflict:"
                    + failure);
            }
        }

        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null)
            {
                throw new InvalidOperationException(
                    "regular-customer-observations-canonical-null");
            }
            if (!producers.Contains(row.producerId))
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
                    "regular-customer-observations-canonical-identity-conflict");
            }
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
                "regular-customer-observations-section-missing:" + id);
        }
        return DungeonSaveJson.Deserialize<T>(section.payloadJson);
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(
        GameplayOutcomeSnapshot row)
    {
        if (row == null)
        {
            throw new InvalidOperationException(
                "regular-customer-observations-canonical-null");
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
