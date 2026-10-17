using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

// The publication journal is durable observation state. Equipment progression
// remains authoritative in the physical combat-equipment component.
public sealed class EquipmentModuleSelectedFactPreflight :
    IDungeonSavePreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report)
    {
        try
        {
            var section = save?.sections?.SingleOrDefault(value =>
                value?.sectionId
                    == EquipmentModuleSelectionPublicationSaveSection.Id);
            if (section == null
                || section.sectionVersion
                    != EquipmentModuleSelectionPublicationSaveData.CurrentVersion)
            {
                throw new InvalidOperationException(
                    "equipment-module-selection-publication-section-missing-or-version-unsupported");
            }

            EquipmentModuleSelectionPublicationSaveSection.ValidateJson(
                section.payloadJson);
            EquipmentModuleSelectionPublicationSaveData journal =
                EquipmentModuleSelectionPublicationRuntime.ValidateAndClone(
                    DungeonSaveJson.Deserialize<
                        EquipmentModuleSelectionPublicationSaveData>(
                            section.payloadJson));

            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    GameplayOutcomeLedgerSaveSection.Id,
                    out GameplayOutcomeLedgerSaveData ledger))
            {
                throw new InvalidOperationException(
                    "equipment-module-selection-publication-ledger-section-missing");
            }
            foreach (EquipmentModuleSelectionPublicationData observation
                in journal.observations)
            {
                ValidateObservation(observation, ledger);
            }

            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    PhysicalItemsSaveSection.Id,
                    out DungeonPhysicalItemSaveData physical)
                || physical == null
                || physical.version != DungeonPhysicalItemSaveData.CurrentVersion
                || physical.uniqueItems == null)
            {
                throw new InvalidOperationException(
                    "equipment-module-selection-physical-section-missing");
            }
            ValidatePhysicalOwners(journal, physical);
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException
            or OverflowException
            or Newtonsoft.Json.JsonException)
        {
            report.AddError(
                "equipment-module-selection-save-join:" + error.Message);
        }
    }

    public static void ValidateObservation(
        EquipmentModuleSelectionPublicationData observation,
        GameplayOutcomeLedgerSaveData incoming)
    {
        if (observation?.fact == null
            || incoming?.outbox == null
            || incoming.exactOutcomes == null
            || incoming.tombstones == null)
        {
            throw new InvalidOperationException(
                "equipment-module-selection-observation-save-missing");
        }

        observation.fact.Validate();
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
                "equipment-module-selection-publication-proof-invalid");
        }
        EquipmentModuleSelectionPublicationData saved = observation.Clone();
        GameplayResultKey key = EquipmentModuleSelectedFactMapping.ResultKey(
            saved.fact);
        if (saved.publicationKey != key.ToString())
        {
            throw new InvalidOperationException(
                "equipment-module-selection-publication-key-conflict");
        }

        GameplayOutcomeSnapshot[] liveRows = incoming.outbox
            .Concat(incoming.exactOutcomes)
            .Where(row => row != null
                && row.producerId == key.ProducerId
                && row.operationId == key.OperationId.Value)
            .ToArray();
        GameplayOutcomeTombstoneSnapshot[] retiredRows = incoming.tombstones
            .Where(row => row != null
                && row.producerId == key.ProducerId
                && row.operationId == key.OperationId.Value)
            .ToArray();
        if (liveRows.Length + retiredRows.Length > 1)
        {
            throw new InvalidOperationException(
                "equipment-module-selection-canonical-duplicate");
        }

        GameplayOutcomeSnapshot live = liveRows.SingleOrDefault();
        GameplayOutcomeTombstoneSnapshot canonical = live == null
            ? retiredRows.SingleOrDefault()
            : RetiredCopy(live);
        if (canonical == null && !saved.pending)
        {
            throw new InvalidOperationException(
                "equipment-module-selection-acknowledged-canonical-missing");
        }
        if (canonical != null
            && (canonical.runId != incoming.runId
                || canonical.sequence < 0
                || canonical.sequence >= incoming.nextSequence
                || canonical.commitRevision != key.CommitRevision
                || canonical.localResultIndex != key.LocalResultIndex
                || canonical.ownerRevision != key.CommitRevision
                || canonical.absoluteDay != saved.fact.absoluteDay
                || !saved.pending
                    && (saved.publicationId
                            != new GameplayOutcomeId(
                                new GameplayOutcomeRunId(canonical.runId),
                                canonical.sequence).ToString()
                        || saved.fingerprint != canonical.canonicalHash)))
        {
            throw new InvalidOperationException(
                "equipment-module-selection-canonical-proof-conflict");
        }

        EquipmentModuleSelectionOutcomeReceipt receipt =
            EquipmentModuleSelectedFactMapping.Capture(
                new EquipmentModuleSelectedFact(saved.fact));
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new EquipmentModuleSelectionOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new EquipmentModuleSelectionOutcomeAdapter()
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
        OutcomePrepareResult result = recorder.TryPrepare(receipt, out var token);
        recorder.CancelPrepared(token);
        if (canonical == null
            ? !result.Success
            : result.Code != OutcomePrepareCode.AlreadyTerminal)
        {
            throw new InvalidOperationException(
                "equipment-module-selection-canonical-fact-conflict:"
                + result.DetailCode);
        }
    }

    private static void ValidatePhysicalOwners(
        EquipmentModuleSelectionPublicationSaveData journal,
        DungeonPhysicalItemSaveData physical)
    {
        Dictionary<string, long> latest = journal.observations
            .GroupBy(value => value.fact.equipmentInstanceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Max(value => value.fact.afterRevision),
                StringComparer.Ordinal);
        Dictionary<string, string> definitions = journal.observations
            .GroupBy(value => value.fact.equipmentInstanceId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.fact.equipmentDefinitionId)
                    .Distinct(StringComparer.Ordinal).Single(),
                StringComparer.Ordinal);

        foreach (UniqueItemInstanceSaveData unique in physical.uniqueItems)
        {
            if (unique == null)
                throw new InvalidOperationException(
                    "equipment-module-selection-physical-item-null");
            ItemInstanceComponentSaveData[] components =
                (unique.components ?? new List<ItemInstanceComponentSaveData>())
                .Where(value => value != null
                    && value.componentTypeId == ItemInstanceComponentIds.Equipment)
                .ToArray();
            if (components.Length == 0)
            {
                if (latest.ContainsKey(unique.itemInstanceId))
                {
                    throw new InvalidOperationException(
                        "equipment-module-selection-physical-type-conflict");
                }
                continue;
            }
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    "equipment-module-selection-physical-equipment-duplicate");
            }
            if (!EquipmentItemStateCodec.TryDecode(
                    components[0],
                    out CombatEquipmentInstance equipment,
                    out string decodeError))
            {
                throw new InvalidOperationException(
                    "equipment-module-selection-physical-equipment-invalid:"
                    + decodeError);
            }
            if (equipment.instanceId != unique.itemInstanceId
                || PhysicalItemIds.ForEquipment(equipment.definitionId)
                    != unique.definitionId)
            {
                throw new InvalidOperationException(
                    "equipment-module-selection-physical-identity-conflict");
            }

            long physicalRevision = equipment.evolution?.moduleSelectionRevision
                ?? 0L;
            long journalRevision = latest.TryGetValue(
                    equipment.instanceId,
                    out long foundRevision)
                ? foundRevision
                : 0L;
            if (physicalRevision != journalRevision)
            {
                throw new InvalidOperationException(
                    "equipment-module-selection-physical-revision-conflict");
            }
            if (journalRevision > 0L
                && definitions[equipment.instanceId] != equipment.definitionId)
            {
                throw new InvalidOperationException(
                    "equipment-module-selection-physical-definition-conflict");
            }
        }
        // Journal rows whose physical item no longer exists remain valid history.
    }

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
