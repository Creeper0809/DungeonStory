using System;

/// <summary>
/// Canonical mass disposition for a ruined passive production batch. The
/// available mass is conserved across recoverable waste, process wastewater,
/// and an explicit irrecoverable loss. No remainder may disappear implicitly.
/// </summary>
public readonly struct ProductionRuinedBatchDispositionPlan
{
    public const string RecoverableWasteOutputLineId =
        ProductionRuinedOutputProtocol.RecoverableWasteOutputLineId;
    public const string DeclaredLossOutputLineId =
        ProductionRuinedOutputProtocol.DeclaredLossOutputLineId;

    private ProductionRuinedBatchDispositionPlan(
        string spoilageItemId,
        int recoverableWasteQuantity,
        long spoilageUnitMassGrams,
        long availableMassGrams,
        long processWastewaterMassGrams,
        long recoverableWasteMassGrams,
        long declaredLossMassGrams)
    {
        SpoilageItemId = spoilageItemId;
        RecoverableWasteQuantity = recoverableWasteQuantity;
        SpoilageUnitMassGrams = spoilageUnitMassGrams;
        AvailableMassGrams = availableMassGrams;
        ProcessWastewaterMassGrams = processWastewaterMassGrams;
        RecoverableWasteMassGrams = recoverableWasteMassGrams;
        DeclaredLossMassGrams = declaredLossMassGrams;
    }

    public string SpoilageItemId { get; }
    public int RecoverableWasteQuantity { get; }
    public long SpoilageUnitMassGrams { get; }
    public long AvailableMassGrams { get; }
    public long ProcessWastewaterMassGrams { get; }
    public long RecoverableWasteMassGrams { get; }
    public long DeclaredLossMassGrams { get; }

    public static ProductionRuinedBatchDispositionPlan Create(
        long wipInputMassGrams,
        long processCleanWaterMassGrams,
        long processWastewaterMassGrams,
        string spoilageItemId,
        long spoilageUnitMassGrams)
    {
        if (wipInputMassGrams <= 0L
            || processCleanWaterMassGrams < 0L
            || processWastewaterMassGrams < 0L
            || spoilageUnitMassGrams <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(wipInputMassGrams),
                "Ruined-batch masses must be positive input and nonnegative process masses.");
        }
        if (!IsCanonicalStableId(spoilageItemId))
        {
            throw new ArgumentException(
                "A ruined batch requires a canonical spoilage item ID.",
                nameof(spoilageItemId));
        }

        long availableMassGrams = checked(
            wipInputMassGrams + processCleanWaterMassGrams);
        if (processWastewaterMassGrams > availableMassGrams)
        {
            throw new InvalidOperationException(
                "Ruined-batch wastewater exceeds the available WIP mass.");
        }

        long recoverableMassBudget = checked(
            availableMassGrams - processWastewaterMassGrams);
        long quantity = recoverableMassBudget / spoilageUnitMassGrams;
        if (quantity <= 0L || quantity > int.MaxValue)
        {
            throw new InvalidOperationException(
                "Ruined-batch mass cannot produce a positive Int32 recoverable-waste quantity.");
        }

        long recoverableWasteMassGrams = checked(
            quantity * spoilageUnitMassGrams);
        long declaredLossMassGrams = checked(
            recoverableMassBudget - recoverableWasteMassGrams);
        if (checked(
                recoverableWasteMassGrams
                + processWastewaterMassGrams
                + declaredLossMassGrams)
            != availableMassGrams)
        {
            throw new InvalidOperationException(
                "Ruined-batch mass disposition is not conservative.");
        }

        return new ProductionRuinedBatchDispositionPlan(
            spoilageItemId,
            checked((int)quantity),
            spoilageUnitMassGrams,
            availableMassGrams,
            processWastewaterMassGrams,
            recoverableWasteMassGrams,
            declaredLossMassGrams);
    }

    private static bool IsCanonicalStableId(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            bool allowed = character >= 'a' && character <= 'z'
                || character >= '0' && character <= '9'
                || character == ':' || character == '/'
                || character == '.' || character == '_'
                || character == '-';
            if (!allowed)
            {
                return false;
            }
        }
        return true;
    }
}

public readonly struct ProductionRuinedBatchExecutionResult
{
    private ProductionRuinedBatchExecutionResult(
        bool isValid,
        bool batchDispositionCompleted,
        ProductionPreparedOutputPhase phase,
        ProductionRuinedBatchDispositionPlan disposition,
        DomainFailure failure)
    {
        IsValid = isValid;
        BatchDispositionCompleted = batchDispositionCompleted;
        Phase = phase;
        Disposition = disposition;
        Failure = failure;
    }

    public bool IsValid { get; }
    public bool BatchDispositionCompleted { get; }
    public ProductionPreparedOutputPhase Phase { get; }
    public ProductionRuinedBatchDispositionPlan Disposition { get; }
    public DomainFailure Failure { get; }

    public static ProductionRuinedBatchExecutionResult Completed(
        ProductionRuinedBatchDispositionPlan disposition)
    {
        if (disposition.AvailableMassGrams <= 0L
            || disposition.RecoverableWasteQuantity <= 0
            || disposition.RecoverableWasteMassGrams <= 0L
            || checked(
                disposition.RecoverableWasteMassGrams
                + disposition.ProcessWastewaterMassGrams
                + disposition.DeclaredLossMassGrams)
                != disposition.AvailableMassGrams)
        {
            throw new ArgumentException(
                "A completed ruined-batch result requires a conservative disposition.",
                nameof(disposition));
        }
        return new ProductionRuinedBatchExecutionResult(
            true,
            true,
            ProductionPreparedOutputPhase.Completed,
            disposition,
            DomainFailure.None);
    }

    public static ProductionRuinedBatchExecutionResult Blocked(
        ProductionPreparedOutputPhase phase,
        DomainFailure failure)
    {
        if (!Enum.IsDefined(typeof(ProductionPreparedOutputPhase), phase)
            || phase == ProductionPreparedOutputPhase.Completed)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        if (!failure.IsFailure)
        {
            throw new ArgumentException(
                "A blocked ruined-batch result requires a failure.",
                nameof(failure));
        }
        return new ProductionRuinedBatchExecutionResult(
            true,
            false,
            phase,
            default,
            failure);
    }
}

/// <summary>
/// Economy-owned ruined-batch boundary. Implementations must resolve the
/// outcome once, retain a ResolvedWaitingForOutputSpace prepared owner while
/// blocked, reserve exact FacilityOutputBuffer grams, and publish the physical
/// batch idempotently. Authored Main/Byproduct lines must be represented as
/// failed zero-mass lines; the recoverable waste and declared loss use the
/// canonical line IDs from <see cref="ProductionRuinedBatchDispositionPlan"/>.
/// A physical commit may never be rolled back into an implicit Loose spawn.
/// </summary>
public interface IProductionRuinedBatchExecutionPort
{
    ProductionRuinedBatchExecutionResult ExecuteRuinedBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);
}
