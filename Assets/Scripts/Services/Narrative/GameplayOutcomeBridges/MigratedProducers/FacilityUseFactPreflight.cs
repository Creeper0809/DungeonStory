using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

// Only the observer knows how an opaque publication joins the native ledger.
public sealed class FacilityUseFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<ModularFacilityWorldSaveData>(save, ModularFacilityWorldSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id), report);

    internal static void Validate(ModularFacilityWorldSaveData world, GameplayOutcomeLedgerSaveData incoming,
        DungeonMigratedProducerOutcomeSaveData counters, DungeonGameRestoreReport report)
    {
        try
        {
            if (world?.buildings == null || incoming?.outbox == null || incoming.exactOutcomes == null
                || incoming.tombstones == null || counters?.owners == null)
                throw new InvalidOperationException("facility-use-observation-sections-missing");
            string producer = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.FacilityVisitEvent).ProducerId;
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("facility-use-canonical-null");
                if (row.producerId == producer) Add(RetiredCopy(row));
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("facility-use-canonical-null");
                if (row.producerId == producer) Add(row);
            }
            void Add(GameplayOutcomeTombstoneSnapshot row)
            {
                if (row.commitRevision != 1 || row.ownerRevision != 1 || row.localResultIndex != 0
                    || !rows.TryAdd(new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString(), row))
                    throw new InvalidOperationException("facility-use-canonical-identity-invalid");
            }
            var owners = counters.owners.Where(value => value?.ownerKey?.StartsWith(producer + "|", StringComparison.Ordinal) == true)
                .ToDictionary(value => value.ownerKey, StringComparer.Ordinal);
            foreach (var owner in owners.Values)
            {
                string identity = owner.ownerKey.Substring(producer.Length + 1);
                string key = MigratedProducerOutcomeCatalog.ResultKey(MigratedProducerOutcomeKind.FacilityVisitEvent, identity, 1).ToString();
                if (owner.revision != 1 || !rows.ContainsKey(key)) throw new InvalidOperationException("facility-use-native-owner-or-proof-invalid");
            }
            if (rows.Count != owners.Count) throw new InvalidOperationException("facility-use-native-owner-missing");

            var registry = new GameplayOutcomeRegistry(MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                    (IGameplayOutcomeDescriptor)new MigratedProducerOutcomeDescriptor(value, new KoreanJosaFormatter())),
                new IGameplayOutcomeAdapterRegistration[] { new MigratedProducerOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var runtime = new MigratedProducerOutcomeRuntime(new DungeonRuntimeAggregateRootStore(), ledger, recorder, ledger);
            var observer = new MigratedDomainFactObserver<FacilityUseCompletedFact>(FacilityUseFactMapping.Capture,
                runtime, new GameplayDomainFactCommitGroup(recorder, ledger));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var building in world.buildings)
            {
                if (building?.stateModules == null) throw new InvalidOperationException("facility-use-building-modules-missing");
                foreach (var module in building.stateModules.Where(value => value?.moduleId == BuildingStateModuleIds.FacilityOperation))
                {
                    if (!seen.Add(building.persistentInstanceId) || module.version != FacilityRuntimeStateModule.Version)
                        throw new InvalidOperationException("facility-use-building-version-or-identity-invalid");
                    FacilityUseSaveValidation.ValidateJson(module.payload);
                    var state = DungeonSaveJson.Deserialize<FacilityRuntimeState>(module.payload);
                    if (state == null || !state.IsValid(out _)) throw new InvalidOperationException("facility-use-building-state-invalid");
                    string prefix = producer + "|facility-visit:facility=" + building.persistentInstanceId + ":use=";
                    long acknowledgedCount = 0;
                    foreach (var owner in owners.Values.Where(value => value.ownerKey.StartsWith(prefix, StringComparison.Ordinal)))
                    {
                        string suffix = owner.ownerKey.Substring(prefix.Length);
                        if (!long.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out long sequence)
                            || suffix != sequence.ToString(CultureInfo.InvariantCulture) || sequence < 1 || sequence >= state.nextUseOperationSequence)
                            throw new InvalidOperationException("facility-use-cursor-rewound");
                        if (state.IsUseAcknowledged(sequence)) acknowledgedCount++;
                        else if (!state.pendingUses.Any(value => value.operationSequence == sequence))
                            throw new InvalidOperationException("facility-use-completion-authority-missing");
                    }
                    long expectedCount = 0;
                    foreach (var range in state.acknowledgedUseRanges) expectedCount = checked(expectedCount + range.last - range.first + 1);
                    if (acknowledgedCount != expectedCount) throw new InvalidOperationException("facility-use-acknowledgement-proof-missing");
                    foreach (var pending in state.pendingUses)
                    {
                        if (pending.operationId != FacilityUseCompletedFact.OperationIdentity(building.persistentInstanceId, pending.operationSequence))
                            throw new InvalidOperationException("facility-use-pending-owner-conflict");
                        string expected = MigratedProducerOutcomeCatalog.ResultKey(MigratedProducerOutcomeKind.FacilityVisitEvent, pending.operationId, 1).ToString();
                        bool exists = rows.TryGetValue(expected, out var canonical);
                        if (pending.publicationKey.Length == 0)
                        {
                            if (exists) throw new InvalidOperationException("facility-use-pending-publication-missing");
                            continue;
                        }
                        if (pending.publicationKey != expected) throw new InvalidOperationException("facility-use-publication-key-conflict");
                        var subset = new GameplayOutcomeLedgerSaveData
                        { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
                        if (exists) { subset.tombstones.Add(canonical); subset.knownResultKeyHighWater = 1; }
                        var candidate = ledger.PrepareGameplayOutcomeRestore(subset);
                        ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
                        ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                        runtime.Restore(runtime.PrepareRestore(counters));
                        var fact = new FacilityUseCompletedFact(building.persistentInstanceId, pending.facilityDisplayName, pending);
                        bool accepted = observer.TryPrepareReplay(expected, fact, out var prepared, out string failure);
                        prepared?.Cancel();
                        if (!accepted) throw new InvalidOperationException("facility-use-canonical-payload-conflict:" + failure);
                    }
                }
            }
            // Demolished facilities may leave historical native rows; no live owner is fabricated for them.
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("facility-use-save-join:" + exception.Message); }
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row) => new()
    {
        runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
        commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
        temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
        parentRunId = row.parentRunId, parentSequence = row.parentSequence, deliveryMetadataVersion = row.deliveryMetadataVersion,
        deliveryClockId = row.deliveryClockId, recordedAbsoluteHour = row.recordedAbsoluteHour, publishedRevision = row.publishedRevision,
        deliveryFaultCount = row.deliveryFaultCount, deliveryDelayReason = row.deliveryDelayReason,
        canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
    };
}
