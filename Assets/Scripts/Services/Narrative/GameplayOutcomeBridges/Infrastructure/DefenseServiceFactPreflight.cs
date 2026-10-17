using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;

public sealed class DefenseServiceFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(row => row?.sectionId == DefenseServicePublicationSaveSection.Id);
            if (section == null || section.sectionVersion != DefenseServicePublicationSaveData.CurrentVersion)
                throw new InvalidOperationException("defense-service-section-missing");
            DefenseServicePublicationSaveSection.ValidateJson(section.payloadJson);
            if (!DungeonSaveSectionPayload.TryRead(save, GameplayOutcomeLedgerSaveSection.Id, out GameplayOutcomeLedgerSaveData ledger)
                || !DungeonSaveSectionPayload.TryRead(save, DefenseFacilitySaveSection.Id, out DefenseFacilitySaveData facilities))
                throw new InvalidOperationException("defense-service-owner-section-missing");
            var owner = save.sections.Single(row => row.sectionId == DefenseFacilitySaveSection.Id);
            var raw = JObject.Parse(owner.payloadJson);
            if (raw["facilities"] is not JArray rows) throw new InvalidOperationException("defense-service-owners-missing");
            foreach (var row in rows)
                if (row?["serviceRevision"]?.Type != JTokenType.Integer
                    || row["nextServiceOperationSequence"]?.Type != JTokenType.Integer || row["pendingService"] is not JObject)
                    throw new InvalidOperationException("defense-service-owner-fields-missing");
            var journal = DungeonSaveJson.Deserialize<DefenseServicePublicationSaveData>(section.payloadJson);
            ValidateOwners(journal, facilities);
            ValidateJournal(journal, ledger);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or OverflowException or Newtonsoft.Json.JsonException)
        { report.AddError("defense-service-save-join:" + error.Message); }
    }

    public static void ValidateOwners(DefenseServicePublicationSaveData source, DefenseFacilitySaveData facilities)
    {
        var journal = DefenseServicePublicationRuntime.ValidateAndClone(source);
        var failures = DefenseFacilitySaveRules.Validate(facilities);
        if (failures.Count != 0) throw new InvalidOperationException("defense-service-owner-invalid:" + string.Join("|", failures));
        var owners = facilities.facilities.ToDictionary(row => row.facilityPersistentId, StringComparer.Ordinal);
        if (journal.observations.Any(row => !owners.ContainsKey(row.fact.facilityId)))
            throw new InvalidOperationException("defense-service-history-owner-missing");
        foreach (var owner in owners.Values)
        {
            var history = journal.observations.Where(row => row.fact.facilityId == owner.facilityPersistentId)
                .OrderBy(row => row.fact.revision).ToArray();
            var pending = owner.pendingService;
            int offset = pending.phase == DefenseFacilityServicePendingPhase.None ? 0
                : pending.phase == DefenseFacilityServicePendingPhase.Requested ? 1 : 2;
            long expected = checked((long)owner.nextServiceOperationSequence * 2 + offset);
            if (owner.serviceRevision != expected || history.LongLength != expected)
                throw new InvalidOperationException("defense-service-owner-history-incomplete");
            for (int i = 0; i < history.Length; i++)
            {
                var fact = history[i].fact;
                if (fact.revision != i + 1L || fact.operationSequence != i / 2
                    || (i % 2 == 0) != (fact.phase == DefenseFacilityServiceOutcomePhase.Requested))
                    throw new InvalidOperationException("defense-service-owner-history-regressed");
            }
            if (offset == 0) continue;
            var request = history[owner.nextServiceOperationSequence * 2];
            if (request.fact.revision != pending.requestRevision || request.fact.absoluteDay != pending.absoluteDay
                || request.fact.kind != pending.kind || request.fact.beforeState != pending.beforeState
                || request.fact.afterState != pending.requestedState || request.fact.beforeSupply != pending.beforeSupply
                || request.fact.afterSupply != pending.requestedSupply || pending.requestAcknowledged && request.pending)
                throw new InvalidOperationException("defense-service-active-request-conflict");
            if (offset != 2) continue;
            if (!pending.requestAcknowledged || request.pending)
                throw new InvalidOperationException("defense-service-terminal-request-not-acknowledged");
            var terminal = history[history.Length - 1].fact;
            if (terminal.revision != pending.terminalRevision || terminal.phase != pending.terminalPhase
                || terminal.afterState != pending.terminalState || terminal.afterSupply != pending.terminalSupply
                || terminal.physicalOperationId != pending.physicalOperationId || terminal.physicalOwnerRevision != pending.physicalOwnerRevision
                || terminal.itemId != pending.itemId || terminal.inputQuantity != pending.inputQuantity || terminal.inputMassGrams != pending.inputMassGrams)
                throw new InvalidOperationException("defense-service-active-terminal-conflict");
        }
    }

    public static void ValidateJournal(DefenseServicePublicationSaveData source, GameplayOutcomeLedgerSaveData incoming)
    {
        var journal = DefenseServicePublicationRuntime.ValidateAndClone(source);
        if (incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
            throw new InvalidOperationException("defense-service-ledger-invalid");
        var canonical = new Dictionary<string, GameplayOutcomeTombstoneSnapshot>(StringComparer.Ordinal);
        foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes)) Add(RetiredCopy(row));
        foreach (var row in incoming.tombstones) Add(row);
        var expected = new HashSet<string>(journal.observations.Select(row => row.publicationKey), StringComparer.Ordinal);
        foreach (var pair in canonical)
            if (pair.Value.producerId == DefenseFacilityServiceOutcomeIds.ProducerId && !expected.Contains(pair.Key))
                throw new InvalidOperationException("defense-service-canonical-owner-missing");
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { new DefenseFacilityServiceOutcomeDescriptor() },
            new IGameplayOutcomeAdapterRegistration[] { new DefenseFacilityServiceOutcomeAdapter() });
        var comparison = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4, retainedSmallPageCount: 1, retainedLargePageCount: 1));
        var recorder = new GameplayOutcomeRecorder(comparison, registry, new GameEventBus());
        foreach (var saved in journal.observations)
        {
            var fact = new DefenseServiceChangedFact(saved.fact);
            var key = DefenseServiceFactMapping.ResultKey(saved.fact);
            if (key.ToString() != saved.publicationKey) throw new InvalidOperationException("defense-service-key-conflict");
            canonical.TryGetValue(saved.publicationKey, out var row);
            if (row == null && !saved.pending) throw new InvalidOperationException("defense-service-acknowledged-result-missing");
            if (row != null && (row.runId != incoming.runId || row.sequence <= 0 || row.sequence >= incoming.nextSequence
                || row.ownerRevision != saved.fact.revision || row.absoluteDay != saved.fact.absoluteDay
                || !saved.pending && (saved.publicationId != new GameplayOutcomeId(new GameplayOutcomeRunId(row.runId), row.sequence).ToString()
                    || saved.fingerprint != row.canonicalHash)))
                throw new InvalidOperationException("defense-service-proof-conflict");
            var subset = new GameplayOutcomeLedgerSaveData
            { runId = incoming.runId, worldEpoch = incoming.worldEpoch, revision = incoming.revision, nextSequence = incoming.nextSequence };
            if (row != null) subset.tombstones.Add(row);
            GameplayOutcomeId parentId = default;
            if (saved.fact.phase == DefenseFacilityServiceOutcomePhase.Completed)
            {
                string parentKey = DefenseServiceFactMapping.PhysicalChildKey(saved.fact).ToString();
                if (!canonical.TryGetValue(parentKey, out var parent) || parent.runId != incoming.runId
                    || parent.sequence <= 0 || parent.sequence >= incoming.nextSequence || parent.ownerRevision != saved.fact.physicalOwnerRevision)
                    throw new InvalidOperationException("defense-service-physical-parent-missing");
                parentId = new GameplayOutcomeId(new GameplayOutcomeRunId(parent.runId), parent.sequence);
                subset.tombstones.Add(parent);
            }
            subset.knownResultKeyHighWater = subset.tombstones.Count;
            var candidate = comparison.PrepareGameplayOutcomeRestore(subset);
            comparison.BeginRestoreCandidate(); comparison.PublishGameplayOutcomeRestore(candidate);
            comparison.PublishRestoreCandidate(); comparison.CompleteRestoreCandidate();
            var result = recorder.TryPrepare(DefenseServiceFactMapping.CaptureWithPhysicalIdentity(fact, parentId), out var token);
            recorder.CancelPrepared(token);
            if (row == null ? !result.Success : result.Code != OutcomePrepareCode.AlreadyTerminal)
                throw new InvalidOperationException("defense-service-canonical-fact-conflict:" + result.DetailCode);
        }
        void Add(GameplayOutcomeTombstoneSnapshot row)
        {
            if (row == null) throw new InvalidOperationException("defense-service-canonical-null");
            var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId), row.commitRevision, row.localResultIndex);
            if (!canonical.TryAdd(key.ToString(), row)) throw new InvalidOperationException("defense-service-canonical-duplicate");
        }
    }

    private static GameplayOutcomeTombstoneSnapshot RetiredCopy(GameplayOutcomeSnapshot row)
    {
        if (row == null) throw new InvalidOperationException("defense-service-canonical-null");
        return new GameplayOutcomeTombstoneSnapshot
        {
            runId = row.runId, sequence = row.sequence, producerId = row.producerId, operationId = row.operationId,
            commitRevision = row.commitRevision, localResultIndex = row.localResultIndex, ownerRevision = row.ownerRevision,
            temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion, absoluteDay = row.absoluteDay,
            parentRunId = row.parentRunId, parentSequence = row.parentSequence, deliveryMetadataVersion = row.deliveryMetadataVersion,
            deliveryClockId = row.deliveryClockId, recordedAbsoluteHour = row.recordedAbsoluteHour,
            publishedRevision = row.publishedRevision, deliveryFaultCount = row.deliveryFaultCount,
            deliveryDelayReason = row.deliveryDelayReason, canonicalHash = row.immutablePayloadHash,
            terminalTier = NarrativeMemoryTier.Forgotten
        };
    }
}
