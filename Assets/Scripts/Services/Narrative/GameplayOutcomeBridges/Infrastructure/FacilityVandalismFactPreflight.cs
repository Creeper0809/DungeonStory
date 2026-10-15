using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Canonical identities and payload comparisons belong to the observer, not the damage owner.</summary>
public sealed class FacilityVandalismFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var envelope = save?.sections?.SingleOrDefault(value => value?.sectionId == FacilityVandalismPendingSaveSection.Id);
            if (envelope == null) throw new InvalidOperationException("facility-vandalism-pending-section-missing");
            FacilityVandalismPendingSaveSection.ValidatePendingJson(envelope.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, ModularFacilityWorldSaveSection.Id, out ModularFacilityWorldSaveData world)
                || !DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger))
                throw new InvalidOperationException("facility-vandalism-owner-or-ledger-section-missing");
            Validate(DungeonSaveJson.Deserialize<FacilityVandalismPendingSaveData>(envelope.payloadJson), world, ledger, report);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Newtonsoft.Json.JsonException)
        { report.AddError("facility-vandalism-observation-save-join:" + exception.Message); }
    }

    internal static void Validate(FacilityVandalismPendingSaveData source, ModularFacilityWorldSaveData world,
        GameplayOutcomeLedgerSaveData incoming, DungeonGameRestoreReport report)
    {
        try
        {
            var pending = FacilityVandalismPendingState.Restore(source).Capture().pending
                .ToDictionary(value => value.publicationKey, StringComparer.Ordinal);
            if (world?.buildings == null) throw new InvalidOperationException("facility-vandalism-world-missing");
            var owners = new Dictionary<string, FacilityRuntimeState>(StringComparer.Ordinal);
            foreach (var building in world.buildings)
            {
                if (building?.stateModules == null) throw new InvalidOperationException("facility-vandalism-owner-modules-missing");
                foreach (var module in building.stateModules.Where(value => value?.moduleId == BuildingStateModuleIds.FacilityOperation))
                {
                    if (module.version != FacilityRuntimeStateModule.Version)
                        throw new InvalidOperationException("facility-vandalism-owner-version-unsupported");
                    FacilityVandalismPendingSaveSection.ValidateFacilityJson(module.payload);
                    var owner = DungeonSaveJson.Deserialize<FacilityRuntimeState>(module.payload);
                    if (owner == null || !owner.IsValid(out _)
                        || !owner.lastVandalism.IsEmpty && owner.lastVandalism.facilityId != building.persistentInstanceId
                        || !owners.TryAdd(building.persistentInstanceId, owner))
                        throw new InvalidOperationException("facility-vandalism-owner-state-conflict");
                }
            }
            if (pending.Count == 0 && owners.Values.All(value => value.lastVandalism.IsEmpty)) return;
            if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("facility-vandalism-canonical-save-missing");
            var rows = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
            var undelivered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
            {
                if (row == null) throw new InvalidOperationException("facility-vandalism-canonical-row-null");
                string key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString();
                Add(key, new GameplayOutcomeTombstoneSnapshot
                {
                    runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
                    commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
                    canonicalHash = row.immutablePayloadHash, terminalTier = NarrativeMemoryTier.Forgotten
                });
                if (incoming.outbox.Contains(row)) undelivered.Add(key);
            }
            foreach (var row in incoming.tombstones)
            {
                if (row == null) throw new InvalidOperationException("facility-vandalism-canonical-tombstone-null");
                Add(new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex).ToString(), row);
            }
            var registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
                { new FacilityVandalismOutcomeDescriptor(new KoreanJosaFormatter()) },
                new IGameplayOutcomeAdapterRegistration[] { new FacilityVandalismOutcomeAdapter() });
            var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
            var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
            var observer = new GameplayDomainFactObserver<FacilityVandalismFact, FacilityVandalismOutcomeReceipt>(
                FacilityVandalismFactMapping.Capture, registry, ledger, recorder, new GameplayDomainFactCommitGroup(recorder, ledger));
            foreach (var entry in pending.Values)
            {
                if (owners.TryGetValue(entry.facilityId, out var owner))
                {
                    if (entry.operationSequence >= owner.nextVandalismOperationSequence)
                        throw new InvalidOperationException("facility-vandalism-pending-owner-sequence-conflict");
                    if (entry.operationSequence == owner.lastVandalism.operationSequence
                        && !SameFact(entry, owner.lastVandalism))
                        throw new InvalidOperationException("facility-vandalism-pending-owner-payload-conflict");
                }
                // An absent building is legitimate: demolition must not erase an undelivered fact.
                CompareCanonical(entry, required: false);
            }
            foreach (var owner in owners.Values.Where(value => !value.lastVandalism.IsEmpty))
            {
                var last = owner.lastVandalism;
                bool awaiting = pending.ContainsKey(last.publicationKey);
                if (!awaiting && undelivered.Contains(last.publicationKey))
                    throw new InvalidOperationException("facility-vandalism-pending-proof-missing");
                CompareCanonical(last, required: !awaiting);
            }

            void Add(string key, GameplayOutcomeTombstoneSnapshot row)
            {
                if (!rows.TryAdd(key, row)) throw new InvalidOperationException("facility-vandalism-canonical-identity-duplicate");
            }
            void CompareCanonical(FacilityVandalismCommitState entry, bool required)
            {
                string expected = FacilityVandalismOutcomeIds.ResultKey(new BuildingInstanceId(entry.facilityId), entry.operationSequence).ToString();
                if (entry.publicationKey != expected) throw new InvalidOperationException("facility-vandalism-publication-key-conflict");
                var subset = new GameplayOutcomeLedgerSaveData
                    { runId = incoming.runId, worldEpoch = incoming.worldEpoch, nextSequence = incoming.nextSequence };
                if (rows.TryGetValue(expected, out var canonical))
                {
                    if (canonical.ownerRevision != entry.operationSequence || string.IsNullOrWhiteSpace(canonical.canonicalHash))
                        throw new InvalidOperationException("facility-vandalism-canonical-proof-conflict");
                    subset.tombstones.Add(canonical);
                }
                else if (required) throw new InvalidOperationException("facility-vandalism-canonical-proof-missing");
                subset.knownResultKeyHighWater = subset.tombstones.Count;
                var restored = ledger.PrepareGameplayOutcomeRestore(subset);
                ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(restored);
                ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
                bool accepted = observer.TryPrepareReplay(expected, new FacilityVandalismFact(entry), out var prepared, out string failure);
                prepared?.Cancel();
                if (!accepted) throw new InvalidOperationException("facility-vandalism-canonical-payload-conflict:" + failure);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("facility-vandalism-observation-save-join:" + exception.Message); }
    }

    private static bool SameFact(FacilityVandalismCommitState left, FacilityVandalismCommitState right) =>
        UnityEngine.JsonUtility.ToJson(left) == UnityEngine.JsonUtility.ToJson(right);
}
