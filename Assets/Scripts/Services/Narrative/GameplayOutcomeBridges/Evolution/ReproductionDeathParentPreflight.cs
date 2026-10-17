using System;
using System.Linq;

/// <summary>The observer, not reproduction state, joins its saved opaque death reference.</summary>
public sealed class ReproductionDeathParentPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<ReproductionWorldSaveData>(save, ReproductionSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report,
        DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterBodyHealthSaveData>(save, CharacterBodyHealthSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<DungeonMigratedProducerOutcomeSaveData>(save, MigratedProducerOutcomeSaveSection.Id));

    internal static void Validate(ReproductionWorldSaveData reproduction, GameplayOutcomeLedgerSaveData ledger,
        DungeonGameRestoreReport report, DungeonCharacterBodyHealthSaveData body = null,
        DungeonMigratedProducerOutcomeSaveData counters = null)
    {
        try
        {
            if (reproduction?.pendingCarrierDeaths == null || ledger?.outbox == null
                || ledger.exactOutcomes == null || ledger.tombstones == null)
                throw new InvalidOperationException("reproduction-death-parent-save-shape-invalid");
            var definition = MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.CharacterDeathEvent);
            foreach (var notification in reproduction.pendingCarrierDeaths)
            {
                if (notification?.parentPublicationKey == null)
                    throw new InvalidOperationException("reproduction-death-parent-reference-missing");
                // Some existing death authorities have no observed parent. Never invent one.
                if (notification.parentPublicationKey.Length == 0) continue;
                if (!GameplayDomainFactDeliveryQuery.TryReadKey(notification.parentPublicationKey, out var key)
                    || key.ProducerId != definition.ProducerId)
                    throw new InvalidOperationException("reproduction-death-parent-reference-invalid");
                var rows = ledger.outbox.Concat(ledger.exactOutcomes).Where(row => row != null
                    && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value
                    && row.commitRevision == key.CommitRevision && row.localResultIndex == key.LocalResultIndex).ToArray();
                var retired = ledger.tombstones.Where(row => row != null
                    && row.producerId == key.ProducerId && row.operationId == key.OperationId.Value
                    && row.commitRevision == key.CommitRevision && row.localResultIndex == key.LocalResultIndex).ToArray();
                if (rows.Length + retired.Length == 0)
                {
                    // An uncertain death is not a fabricated parent: its exact frozen producer
                    // fact and whole atomic cohort must still be owned by the saved body state.
                    var pending = body?.pendingFatalityFacts?.Where(value => value != null
                        && value.deathPublicationKey == notification.parentPublicationKey).ToArray();
                    if (pending?.Length != 1 || pending[0].fact == null
                        || pending[0].fact.characterId != notification.carrierId
                        || pending[0].fact.absoluteDay != notification.absoluteDay
                        || pending[0].fact.cause != notification.cause)
                        throw new InvalidOperationException("reproduction-death-parent-source-missing-or-conflicting");
                    var sourceReport = new DungeonGameRestoreReport();
                    CharacterBodyFactPreflight.Validate(body, ledger, counters, sourceReport);
                    if (!sourceReport.Success)
                        throw new InvalidOperationException("reproduction-death-parent-source-invalid:" + string.Join(";", sourceReport.Errors));
                    continue;
                }
                if (rows.Length + retired.Length != 1)
                    throw new InvalidOperationException("reproduction-death-parent-canonical-missing-or-duplicate");
                string run = rows.Length == 1 ? rows[0].runId : retired[0].runId;
                long sequence = rows.Length == 1 ? rows[0].sequence : retired[0].sequence;
                string hash = rows.Length == 1 ? rows[0].immutablePayloadHash : retired[0].canonicalHash;
                if (run != ledger.runId || !new GameplayOutcomeId(new GameplayOutcomeRunId(run), sequence).IsValid
                    || hash == null || hash.Length != 64
                    || rows.Length == 1 && (rows[0].outcomeTypeId != definition.OutcomeTypeId.Value
                        || rows[0].lifecycle < GameplayOutcomeRecordLifecycle.Committed
                        || rows[0].participants == null || !rows[0].participants.Any(
                            value => value != null && value.entityId == notification.carrierId)))
                    throw new InvalidOperationException("reproduction-death-parent-canonical-conflict");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("reproduction-death-parent-save-join:" + exception.Message); }
    }
}
