using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

public sealed class VisitorExitFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value =>
                value?.sectionId == VisitorExitPublicationSaveSection.Id);
            if (section == null
                || section.sectionVersion
                    != VisitorExitPublicationSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "visitor-exit-publication-section-missing-or-version-unsupported");
            }

            VisitorExitPublicationSaveSection.ValidateJson(section.payloadJson);
            VisitorExitPublicationSaveData journal =
                DungeonSaveJson.Deserialize<VisitorExitPublicationSaveData>(
                    section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    GameplayOutcomeLedgerSaveSection.Id,
                    out GameplayOutcomeLedgerSaveData ledger))
            {
                throw new InvalidOperationException(
                    "visitor-exit-publication-ledger-section-missing");
            }
            ValidateJournal(journal, ledger);
            if (!DungeonSaveSectionPayload.TryRead(save, CharacterWorldSaveSection.Id,
                    out DungeonCharacterWorldSaveData characters))
                throw new InvalidOperationException("visitor-exit-character-owner-section-missing");
            ValidateOwners(journal, characters.populationProfiles);
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException
            or Newtonsoft.Json.JsonException)
        {
            report.AddError("visitor-exit-publication-save-join:"
                + error.Message);
        }
    }

    public static void ValidateOwners(VisitorExitPublicationSaveData source, IEnumerable<WorldCharacterProfile> profiles)
    {
        var journal = VisitorExitPublicationRuntime.ValidateAndClone(source);
        if (profiles == null) throw new InvalidOperationException("visitor-exit-profiles-missing");
        var owners = profiles.ToArray();
        if (owners.Any(row => row == null)
            || owners.Select(row => row.persistentId).Distinct(StringComparer.Ordinal).Count() != owners.Length)
            throw new InvalidOperationException("visitor-exit-profile-owner-duplicate-or-null");
        var report = new DungeonGameRestoreReport();
        foreach (var profile in owners)
            CharacterWorldSaveValidation.ValidateVisitorExit(profile, "Population profile '" + profile.persistentId + "'", report);
        if (!report.Success) throw new InvalidOperationException("visitor-exit-profile-state-invalid:" + string.Join(";", report.Errors));
        foreach (var row in journal.observations)
        {
            string id = row.theft?.characterId ?? row.departure.characterId;
            long revision = row.theft?.ownerRevision ?? row.departure.ownerRevision;
            var profile = owners.SingleOrDefault(value => value.persistentId == id);
            if (profile?.visitorExitOperation == null || profile.nextVisitorExitSequence <= revision
                || profile.visitorExitOperation.ownerRevision < revision)
                throw new InvalidOperationException("visitor-exit-profile-history-regressed");
        }
        foreach (var profile in owners)
        {
            var operation = profile.visitorExitOperation;
            if (operation == null || operation.operationId.Length == 0) continue;
            var theft = journal.observations.SingleOrDefault(row =>
                row.theft?.parentOperationId == operation.operationId);
            var departure = journal.observations.SingleOrDefault(row =>
                row.departure?.operationId == operation.operationId);
            if (operation.theftPublicationKey != (theft?.publicationKey ?? string.Empty)
                || operation.departurePublicationKey != (departure?.publicationKey ?? string.Empty)
                || theft != null && (theft.theft.characterId != profile.persistentId
                    || theft.theft.ownerRevision != operation.ownerRevision
                    || theft.theft.absoluteDay != operation.absoluteDay))
                throw new InvalidOperationException("visitor-exit-profile-publication-conflict");
            if (departure == null) continue;
            var fact = departure.departure;
            if (fact.characterId != profile.persistentId || fact.ownerRevision != operation.ownerRevision
                || fact.absoluteDay != operation.absoluteDay || fact.sourceMacro != operation.sourceMacro
                || fact.lifecycleBefore != operation.lifecycleBefore || fact.visitingBefore != operation.visitingBefore
                || fact.visitCountBefore != operation.visitCountBefore || fact.equipmentCountBefore != operation.equipmentCountBefore
                || fact.terminalKind.ToString() != operation.terminalKind || fact.failureReason != operation.failureReason
                || fact.theftPublicationKey != operation.theftPublicationKey)
                throw new InvalidOperationException("visitor-exit-profile-fact-conflict");
        }
    }

    public static void ValidateJournal(
        VisitorExitPublicationSaveData source,
        GameplayOutcomeLedgerSaveData incoming)
    {
        VisitorExitPublicationSaveData journal =
            VisitorExitPublicationRuntime.ValidateAndClone(source);
        RequireLedger(incoming);

        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (VisitorExitPublicationData observation in journal.observations)
        {
            GameplayResultKey key = ResultKey(observation);
            if (!expected.Add(key.ToString()))
            {
                throw new InvalidOperationException(
                    "visitor-exit-publication-key-duplicate");
            }
            ValidateObservation(observation, incoming);
        }
        ValidateTheftChildren(journal, incoming);

        var canonical = new HashSet<string>(StringComparer.Ordinal);
        foreach (GameplayOutcomeSnapshot row in incoming.outbox
            .Concat(incoming.exactOutcomes)
            .Where(value => value != null && IsOwned(value.producerId)))
        {
            RequireOwnedCanonical(
                row.producerId,
                row.operationId,
                row.commitRevision,
                row.localResultIndex,
                expected,
                canonical);
        }
        foreach (GameplayOutcomeTombstoneSnapshot row in incoming.tombstones
            .Where(value => value != null && IsOwned(value.producerId)))
        {
            RequireOwnedCanonical(
                row.producerId,
                row.operationId,
                row.commitRevision,
                row.localResultIndex,
                expected,
                canonical);
        }
    }

    public static void ValidateObservation(
        VisitorExitPublicationData observation,
        GameplayOutcomeLedgerSaveData incoming)
    {
        RequireLedger(incoming);
        ValidateObservationShape(observation);

        VisitorExitPublicationData saved = observation.Clone();
        GameplayResultKey key = ResultKey(saved);
        if (saved.publicationKey != key.ToString())
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-key-conflict");
        }

        GameplayOutcomeSnapshot[] liveRows = incoming.outbox
            .Concat(incoming.exactOutcomes)
            .Where(row => MatchesExact(row, key))
            .ToArray();
        GameplayOutcomeTombstoneSnapshot[] retiredRows = incoming.tombstones
            .Where(row => MatchesExact(row, key))
            .ToArray();
        if (liveRows.Length + retiredRows.Length > 1)
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-canonical-duplicate");
        }

        GameplayOutcomeSnapshot live = liveRows.SingleOrDefault();
        GameplayOutcomeTombstoneSnapshot canonical = live == null
            ? retiredRows.SingleOrDefault()
            : RetiredCopy(live);
        int absoluteDay = AbsoluteDay(saved);
        if (canonical == null && !saved.pending)
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-acknowledged-canonical-missing");
        }
        if (canonical != null
            && (canonical.runId != incoming.runId
                || canonical.sequence < 0
                || canonical.sequence >= incoming.nextSequence
                || canonical.commitRevision != key.CommitRevision
                || canonical.ownerRevision != key.CommitRevision
                || canonical.localResultIndex != key.LocalResultIndex
                || canonical.absoluteDay != absoluteDay
                || !saved.pending
                    && (saved.publicationId
                            != new GameplayOutcomeId(
                                new GameplayOutcomeRunId(canonical.runId),
                                canonical.sequence).ToString()
                        || saved.fingerprint != canonical.canonicalHash)))
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-canonical-proof-conflict");
        }

        ValidateCanonicalPayload(saved, incoming, canonical);
    }

    private static void ValidateObservationShape(
        VisitorExitPublicationData observation)
    {
        if (observation == null
            || (observation.theft == null) == (observation.departure == null))
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-observation-fact-invalid");
        }
        if (observation.theft != null)
            observation.theft.Validate();
        else
            observation.departure.Validate();
        if (!DomainFactValueSyntax.IsText(
                observation.publicationKey,
                1024,
                false)
            || (observation.pending
                ? observation.publicationId != string.Empty
                    || observation.fingerprint != string.Empty
                : !DomainFactValueSyntax.IsText(
                        observation.publicationId,
                        1024,
                        false)
                    || !IsHash(observation.fingerprint)))
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-proof-invalid");
        }
    }

    private static void ValidateTheftChildren(
        VisitorExitPublicationSaveData journal,
        GameplayOutcomeLedgerSaveData incoming)
    {
        foreach (VisitorExitPublicationData parent in journal.observations
            .Where(value => value.departure != null))
        {
            VisitorDepartureFactData departure = parent.departure;
            if (string.IsNullOrWhiteSpace(departure.theftPublicationKey)
                || departure.theftPublicationKey == "none")
            {
                continue;
            }

            string theftOperation = departure.operationId + ":floor-theft:1";
            VisitorExitPublicationData child = journal.observations
                .SingleOrDefault(value => value.theft != null
                    && value.theft.operationId == theftOperation);
            if (child?.theft == null
                || child.theft.parentOperationId != departure.operationId
                || child.theft.characterId != departure.characterId
                || child.theft.ownerRevision != departure.ownerRevision
                || child.theft.absoluteDay != departure.absoluteDay)
            {
                throw new InvalidOperationException(
                    "visitor-exit-publication-theft-child-owner-conflict");
            }

            GameplayResultKey childKey =
                FloorItemStolenFactMapping.ResultKey(child.theft);
            if (departure.theftPublicationKey != childKey.ToString())
            {
                throw new InvalidOperationException(
                    "visitor-exit-publication-theft-child-key-conflict");
            }

            GameplayResultKey parentKey =
                VisitorDepartureFactMapping.ResultKey(departure);
            if (HasCanonical(incoming, parentKey)
                && !HasCanonical(incoming, childKey))
            {
                throw new InvalidOperationException(
                    "visitor-exit-publication-theft-child-canonical-missing");
            }
        }
    }

    private static void ValidateCanonicalPayload(
        VisitorExitPublicationData saved,
        GameplayOutcomeLedgerSaveData incoming,
        GameplayOutcomeTombstoneSnapshot canonical)
    {
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new FloorTheftOutcomeDescriptor(),
                new VisitorDepartureOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new FloorTheftOutcomeAdapter(),
                new VisitorDepartureOutcomeAdapter()
            });
        var comparison = new GameplayOutcomeLedger(
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
            nextSequence = incoming.nextSequence,
            knownResultKeyHighWater = canonical == null ? 0 : 1
        };
        if (canonical != null)
            subset.tombstones.Add(canonical);

        GameplayOutcomeLedgerRestoreCandidate candidate =
            comparison.PrepareGameplayOutcomeRestore(subset);
        comparison.BeginRestoreCandidate();
        comparison.PublishGameplayOutcomeRestore(candidate);
        comparison.PublishRestoreCandidate();
        comparison.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(
            comparison,
            registry,
            new GameEventBus());

        OutcomePrepareResult result;
        PreparedOutcomeToken token;
        if (saved.theft != null)
        {
            FloorTheftOutcomeReceipt receipt =
                FloorItemStolenFactMapping.Capture(
                    new FloorItemStolenFact(saved.theft));
            result = recorder.TryPrepare(receipt, out token);
        }
        else
        {
            VisitorDepartureOutcomeReceipt receipt =
                VisitorDepartureFactMapping.Capture(
                    new VisitorDepartureFact(saved.departure));
            result = recorder.TryPrepare(receipt, out token);
        }
        recorder.CancelPrepared(token);
        if (canonical == null
            ? !result.Success
            : result.Code != OutcomePrepareCode.AlreadyTerminal)
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-canonical-fact-conflict:"
                + result.DetailCode);
        }
    }

    private static GameplayResultKey ResultKey(
        VisitorExitPublicationData observation) => observation.theft != null
        ? FloorItemStolenFactMapping.ResultKey(observation.theft)
        : VisitorDepartureFactMapping.ResultKey(observation.departure);

    private static int AbsoluteDay(
        VisitorExitPublicationData observation) => observation.theft != null
        ? observation.theft.absoluteDay
        : observation.departure.absoluteDay;

    private static bool HasCanonical(
        GameplayOutcomeLedgerSaveData incoming,
        GameplayResultKey key) => incoming.outbox
            .Concat(incoming.exactOutcomes)
            .Any(row => MatchesExact(row, key))
        || incoming.tombstones.Any(row => MatchesExact(row, key));

    private static bool IsOwned(string producerId) =>
        producerId == VisitorExitOutcomeIds.TheftProducerId
        || producerId == VisitorExitOutcomeIds.DepartureProducerId;

    private static void RequireOwnedCanonical(
        string producerId,
        string operationId,
        long commitRevision,
        int localResultIndex,
        ISet<string> expected,
        ISet<string> canonical)
    {
        var key = new GameplayResultKey(
            producerId,
            new GameplayOperationId(operationId),
            commitRevision,
            localResultIndex);
        string opaque = key.ToString();
        if (!expected.Contains(opaque))
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-canonical-history-missing");
        }
        if (!canonical.Add(opaque))
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-canonical-duplicate");
        }
    }

    private static void RequireLedger(GameplayOutcomeLedgerSaveData incoming)
    {
        if (incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "visitor-exit-publication-ledger-save-missing");
        }
    }

    private static bool MatchesExact(
        GameplayOutcomeSnapshot row,
        GameplayResultKey key) => row != null
        && row.producerId == key.ProducerId
        && row.operationId == key.OperationId.Value
        && row.commitRevision == key.CommitRevision
        && row.localResultIndex == key.LocalResultIndex;

    private static bool MatchesExact(
        GameplayOutcomeTombstoneSnapshot row,
        GameplayResultKey key) => row != null
        && row.producerId == key.ProducerId
        && row.operationId == key.OperationId.Value
        && row.commitRevision == key.CommitRevision
        && row.localResultIndex == key.LocalResultIndex;

    private static bool IsHash(string value) => value?.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f'
            or >= 'A' and <= 'F');

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(
        GameplayOutcomeSnapshot row) => new()
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
