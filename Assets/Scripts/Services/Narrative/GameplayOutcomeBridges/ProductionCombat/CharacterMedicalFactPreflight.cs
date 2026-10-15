using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

/// <summary>Observer-owned canonical join, without a dependency from medical state to its record.</summary>
public sealed class CharacterMedicalFactPreflight : IDungeonSavePreflightValidator, IDungeonCapturedSavePreflightValidator
{
    public void Validate(DungeonGameSaveData save, DungeonGameRestoreReport report) => Validate(
        DungeonSaveSectionPayload.ReadOrNew<DungeonCharacterMedicalSaveData>(save, CharacterMedicalSaveSection.Id),
        DungeonSaveSectionPayload.ReadOrNew<GameplayOutcomeLedgerSaveData>(save, GameplayOutcomeLedgerSaveSection.Id), report);

    internal static void Validate(DungeonCharacterMedicalSaveData medical, GameplayOutcomeLedgerSaveData incoming,
        DungeonGameRestoreReport report)
    {
        try
        {
            if (medical?.orders == null || medical.pendingProcedureFacts == null
                || incoming?.outbox == null || incoming.exactOutcomes == null || incoming.tombstones == null)
                throw new InvalidOperationException("medical-procedure-save-shape-invalid");
            CharacterMedicalSaveValidation.ValidatePendingProcedureFacts(medical, report);
            foreach (var row in incoming.outbox.Concat(incoming.exactOutcomes))
                if (row?.producerId == "character.medical")
                    ValidateSequenceAuthority(medical, row.operationId, row.commitRevision, row.localResultIndex);
            foreach (var row in incoming.tombstones)
                if (row?.producerId == "character.medical")
                    ValidateSequenceAuthority(medical, row.operationId, row.commitRevision, row.localResultIndex);
            foreach (var pending in medical.pendingProcedureFacts)
            {
                if (pending?.fact == null) throw new InvalidOperationException("medical-procedure-pending-fact-missing");
                var receipt = CharacterMedicalFactObservation.Capture(new CharacterMedicalProcedureFact(pending.fact));
                ValidateCanonical(receipt, pending.publicationKey, incoming);
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        { report.AddError("medical-procedure-save-join:" + exception.Message); }
    }

    private static void ValidateSequenceAuthority(DungeonCharacterMedicalSaveData medical,
        string operation, long revision, int localIndex)
    {
        const string prefix = "medical-terminal:";
        int split = operation?.LastIndexOf(':') ?? -1;
        if (operation == null || !operation.StartsWith(prefix, StringComparison.Ordinal) || split <= prefix.Length
            || !CharacterMedicalSaveValidation.TryParseOrderId(operation.Substring(prefix.Length, split - prefix.Length), out int ordinal)
            || !int.TryParse(operation.Substring(split + 1), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out int sequence)
            || sequence <= 0 || operation.Substring(split + 1) != sequence.ToString(System.Globalization.CultureInfo.InvariantCulture)
            || revision != sequence || localIndex != 0 || ordinal > medical.orderSequence)
            throw new InvalidOperationException("medical-procedure-canonical-sequence-invalid");
        string orderId = operation.Substring(prefix.Length, split - prefix.Length);
        if (medical.orders.Any(order => order != null && order.orderId == orderId
                && order.nextTerminalOutcomeSequence <= sequence))
            throw new InvalidOperationException("medical-procedure-canonical-sequence-reuse");
    }

    private static void ValidateCanonical(CharacterMedicalOutcomeReceipt receipt, string publicationKey,
        GameplayOutcomeLedgerSaveData incoming)
    {
        var key = receipt.ResultKey;
        if (key.ToString() != publicationKey) throw new InvalidOperationException("medical-procedure-observer-key-conflict");
        var rows = incoming.outbox.Concat(incoming.exactOutcomes).Where(value => value != null
            && value.producerId == key.ProducerId && value.operationId == key.OperationId.Value
            && value.commitRevision == key.CommitRevision && value.localResultIndex == 0).ToArray();
        var retired = incoming.tombstones.Where(value => value != null
            && value.producerId == key.ProducerId && value.operationId == key.OperationId.Value
            && value.commitRevision == key.CommitRevision && value.localResultIndex == 0).ToArray();
        if (rows.Length + retired.Length > 1) throw new InvalidOperationException("medical-procedure-canonical-duplicate");
        bool exists = rows.Length + retired.Length == 1;
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[] { new CharacterMedicalOutcomeDescriptor(new KoreanJosaFormatter()) },
            new IGameplayOutcomeAdapterRegistration[] { new CharacterMedicalOutcomeAdapter() });
        var ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
            smallPageCount: 1, largePageCount: 1, knownResultKeyCapacity: 4,
            retainedSmallPageCount: 1, retainedLargePageCount: 1));
        if (exists)
        {
            string run = rows.Length == 1 ? rows[0].runId : retired[0].runId;
            long sequence = rows.Length == 1 ? rows[0].sequence : retired[0].sequence;
            long revision = rows.Length == 1 ? rows[0].ownerRevision : retired[0].ownerRevision;
            string hash = rows.Length == 1 ? rows[0].immutablePayloadHash : retired[0].canonicalHash;
            if (run != incoming.runId || sequence >= incoming.nextSequence || revision != key.CommitRevision
                || rows.Length == 1 && rows[0].outcomeTypeId != CharacterMedicalOutcomeIds.Completed.Value)
                throw new InvalidOperationException("medical-procedure-canonical-identity-conflict");
            var comparison = new GameplayOutcomeLedgerSaveData
            {
                runId = run, worldEpoch = incoming.worldEpoch, nextSequence = checked(sequence + 1), knownResultKeyHighWater = 1,
                tombstones = new() { new GameplayOutcomeTombstoneSnapshot
                {
                    runId = run, sequence = sequence, producerId = key.ProducerId, operationId = key.OperationId.Value,
                    commitRevision = key.CommitRevision, localResultIndex = 0, ownerRevision = revision,
                    terminalTier = NarrativeMemoryTier.Forgotten, canonicalHash = hash
                } }
            };
            var candidate = ledger.PrepareGameplayOutcomeRestore(comparison);
            ledger.BeginRestoreCandidate(); ledger.PublishGameplayOutcomeRestore(candidate);
            ledger.PublishRestoreCandidate(); ledger.CompleteRestoreCandidate();
        }
        var recorder = new GameplayOutcomeRecorder(ledger, registry, new GameEventBus());
        var result = recorder.TryPrepare(receipt, out var token);
        recorder.CancelPrepared(token);
        // Missing canonical data is legitimate only as an unresolved publication;
        // even then the full fact must be recordable without replaying treatment.
        if (exists ? result.Code != OutcomePrepareCode.AlreadyTerminal : !result.Success)
            throw new InvalidOperationException("medical-procedure-canonical-payload-conflict:" + result.Code + ":" + result.DetailCode);
    }
}
