using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation.Facts;

// Coordinates a production owner's frozen fact with the generic publication boundary.
// Domain effects and saved facts remain on ProductionBillRecord; native mapping is not here.
public sealed class ProductionCompletionObservationRuntime
{
    private readonly IDomainFactStagingPublisher staging;
    private readonly IDomainFactRecoveryPublisher recovery;
    public ProductionCompletionObservationRuntime(IDomainFactStagingPublisher staging, IDomainFactRecoveryPublisher recovery)
    { this.staging = staging ?? throw new ArgumentNullException(nameof(staging)); this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery)); }

    public bool TryPrepare(ProductionBillRecord record, Func<ProductionCycleCompletedFact> capture,
        out IDomainFactStage stage, out string failure)
    {
        stage = null;
        var saved = record.CompletionObservation;
        try
        {
            var fact = saved == null ? capture() : new ProductionCycleCompletedFact(saved.fact);
            if (!staging.TryStage(fact, out stage, out failure) || stage == null) return false;
            if (stage.OwnerRevision != record.cycleSequence || !DomainFactValueSyntax.IsText(stage.OperationKey, 4096, false)
                || stage.OperationKey != stage.OperationKey.Trim() || saved != null && saved.publicationKey != stage.OperationKey)
            { stage.Cancel(); stage = null; failure = "production-completion-stage-conflict"; return false; }
            record.SetCompletionObservation(saved ?? new ProductionCompletionObservationSaveData
            { fact = fact.Snapshot(), publicationKey = stage.OperationKey });
            return true;
        }
        catch (Exception error) when (Nonfatal(error))
        { stage?.Cancel(); stage = null; failure = "production-completion-prepare:" + error.Message; return false; }
    }

    public DomainFactDelivery Commit(ProductionBillRecord record, IDomainFactStage stage)
    {
        DomainFactDelivery result;
        try { result = stage.Commit(); }
        catch (Exception error) when (Nonfatal(error)) { result = DomainFactDelivery.Uncertain("production-completion-commit:" + error.Message); }
        return Accept(record, result);
    }

    public bool TryComplete(ProductionBillRecord record, out string failure)
    {
        var saved = record.CompletionObservation;
        if (saved == null) { failure = "production-completion-fact-missing"; return false; }
        var results = new DomainFactDelivery[1];
        try
        {
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(saved.publicationKey, new ProductionCycleCompletedFact(saved.fact)) },
                    results, out failure)) return false;
            var delivery = Accept(record, results[0]);
            failure = delivery.Completed ? "" : "production-completion-pending:" + delivery.Detail;
            return delivery.Completed;
        }
        catch (Exception error) when (Nonfatal(error))
        { failure = "production-completion-recovery:" + error.Message; return false; }
    }

    private static DomainFactDelivery Accept(ProductionBillRecord record, DomainFactDelivery delivery)
    {
        if (delivery.Phase is DomainFactDeliveryPhase.Rejected or DomainFactDeliveryPhase.InDoubt) return delivery;
        var saved = record.CompletionObservation;
        if (saved == null || saved.publicationKey != delivery.OperationKey
            || !DomainFactValueSyntax.IsText(delivery.PublicationId, 4096, false)
            || delivery.PublicationId != delivery.PublicationId.Trim() || !ProductionCycleCompletedFact.Hash(delivery.Fingerprint)
            || saved.publicationId.Length > 0 && (saved.publicationId != delivery.PublicationId || saved.publicationFingerprint != delivery.Fingerprint))
            return DomainFactDelivery.Uncertain("production-completion-proof-conflict");
        saved.publicationId = delivery.PublicationId; saved.publicationFingerprint = delivery.Fingerprint;
        record.SetCompletionObservation(saved);
        return delivery;
    }

    internal static bool Nonfatal(Exception error) => error is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
}

public static class ProductionCycleFactCapture
{
    // Physical owners retain their full compound keys. Only the observation
    // projection is bounded; existing short keys retain their published identity.
    public static string CommitIdentity(string physicalCommitId)
    {
        if (DomainFactValueSyntax.IsIdentity(physicalCommitId)) return physicalCommitId;
        if (string.IsNullOrEmpty(physicalCommitId))
            throw new InvalidOperationException("production-completion-commit-identity-invalid");
        for (int offset = 0; offset < physicalCommitId.Length; offset += 192)
            if (!DomainFactValueSyntax.IsIdentity(physicalCommitId.Substring(offset, Math.Min(192, physicalCommitId.Length - offset))))
                throw new InvalidOperationException("production-completion-commit-identity-invalid");
        using SHA256 sha = SHA256.Create();
        return "production-commit-digest:" + string.Concat(sha.ComputeHash(new UTF8Encoding(false, true).GetBytes(physicalCommitId))
            .Select(value => value.ToString("x2")));
    }

    public static string PreparedCommitIdentity(string batchCommitId, string outputLineId, string physicalCommitId)
    {
        if (physicalCommitId != ProductionPreparedOutputIdentity.BuildLineCommitId(batchCommitId, outputLineId))
            throw new InvalidOperationException("production-completion-physical-commit-conflict");
        return CommitIdentity(physicalCommitId);
    }

    public static ProductionCycleCompletedFact Prepared(ProductionBillRecord record, ProductionFacilityHandle facility,
        ProductionWorkerHandle worker, FacilityBufferPlannedOutputPublicationReceipt publication, int absoluteDay)
    {
        var batch = record.preparedOutput;
        if (batch.batchCommitId != publication.BatchCommitId || batch.outcomeFingerprint != publication.OutcomeFingerprint
            || batch.admissionFingerprint != publication.PlannedOutputFingerprint || batch.destinationId != publication.DestinationId)
            throw new InvalidOperationException("production-completion-physical-preview-conflict");
        string workerId = worker?.AuthorityKind == ProductionWorkerAuthorityKind.Actor ? worker.PersistentId : "";
        return new ProductionCycleCompletedFact(new ProductionCycleCompletedFactData
        {
            billId = record.billId.Value, cycleSequence = record.cycleSequence, recipeId = record.recipeId,
            facilityId = record.buildingInstanceId.Value, facilityDisplayName = facility.DisplayName,
            workerId = workerId, workerDisplayName = workerId.Length == 0 ? "" : worker.DisplayName, absoluteDay = absoluteDay,
            batchCommitId = batch.batchCommitId, outcomeFingerprint = batch.outcomeFingerprint,
            plannedOutputFingerprint = publication.PlannedOutputFingerprint, destinationId = batch.destinationId,
            wipInputCommitId = record.wipInputCommitId, wipInputQuantity = record.wipInputQuantity, wipInputMassGrams = record.wipInputMassGrams,
            processCleanWaterMassGrams = record.processCleanWaterMassGrams, processWastewaterMassGrams = record.processWastewaterMassGrams,
            declaredLossMassGrams = batch.totalDeclaredLossMassGrams, declaredExternalInputMassGrams = batch.totalDeclaredExternalInputMassGrams,
            lines = batch.lines.Where(value => value != null && ProductionOutputRoleRules.IsPhysical(value.role)
                && value.rollSucceeded && value.quantity > 0 && value.exactMassGrams > 0).Select(value => new ProductionCompletedLineData
                { outputLineId = value.outputLineId,
                    commitIds = new() { PreparedCommitIdentity(batch.batchCommitId, value.outputLineId, value.lineCommitId) }, itemId = value.itemId,
                    quantity = value.quantity, massGrams = value.exactMassGrams, capabilityFingerprint = value.outputCapabilityFingerprint }).ToList(),
            stacks = publication.Stacks.Select(value => new ProductionCompletedStackData
                { stackId = value.StackId, outputLineId = value.OutputLineId, itemId = value.ItemDefinitionId.Value,
                    quantity = value.Quantity, massGrams = value.MassGrams, itemInstanceId = value.ItemInstanceId ?? "" }).ToList()
        });
    }

    public static ProductionCycleCompletedFact Exact(ProductionRecipeExecutionReceipt receipt, string facilityName,
        string workerId, string workerName, int absoluteDay, long cleanWaterMassGrams, long wastewaterMassGrams)
    {
        var correlation = receipt.Correlation;
        return new ProductionCycleCompletedFact(new ProductionCycleCompletedFactData
        {
            billId = correlation.BillId.Value, cycleSequence = correlation.CycleSequence, recipeId = correlation.RecipeId,
            facilityId = correlation.FacilityId.Value, facilityDisplayName = facilityName, workerId = workerId,
            workerDisplayName = workerName, absoluteDay = absoluteDay,
            batchCommitId = receipt.BatchCommitId, outcomeFingerprint = receipt.OutcomeFingerprint, plannedOutputFingerprint = receipt.PlannedOutputFingerprint,
            destinationId = ProductionOutputDestinationId.FromFacility(correlation.FacilityId).Value,
            wipInputCommitId = receipt.WipInputCommitId, wipInputQuantity = receipt.WipInputQuantity, wipInputMassGrams = receipt.WipInputMassGrams,
            processCleanWaterMassGrams = cleanWaterMassGrams, processWastewaterMassGrams = wastewaterMassGrams,
            lines = receipt.Outputs.Select(value => new ProductionCompletedLineData
                { outputLineId = value.OutputLineId, commitIds = value.CommitIds.Select(CommitIdentity).ToList(), itemId = value.ItemId,
                    quantity = value.Quantity, massGrams = value.MassGrams, capabilityFingerprint = value.CapabilityFingerprint }).ToList(),
            stacks = receipt.PhysicalSlices.Select(value => new ProductionCompletedStackData
                { stackId = value.StackId, outputLineId = value.OutputLineId, itemId = value.ItemId, quantity = value.Quantity,
                    massGrams = value.MassGrams, itemInstanceId = value.ItemInstanceId ?? "",
                    sourceCommitId = string.IsNullOrEmpty(value.CommitId) ? "" : CommitIdentity(value.CommitId),
                    componentSignature = value.ComponentSignature ?? "" }).ToList()
        });
    }
}

