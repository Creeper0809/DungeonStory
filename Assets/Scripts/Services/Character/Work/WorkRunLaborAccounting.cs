using System;

/// <summary>
/// Owns cumulative labor fractions and the active emergency-ledger operation.
/// Coroutine, actor and movement state remain with the work executor.
/// </summary>
internal sealed class WorkRunLaborAccounting
{
    private readonly IEmergencyWorkAccountingService emergencyWorkAccounting;
    private readonly ISettlementLaborAccountingService settlementLaborAccounting;
    private string activeEmergencyOperationId = string.Empty;
    private long emergencyAccountingSequence;
    private double actualLaborMilliWuCarry;
    private double projectOutputMilliWuCarry;
    private long latestApprovedLaborMilliWu;

    internal WorkRunLaborAccounting(IEmergencyWorkAccountingService emergencyWorkAccounting,
        ISettlementLaborAccountingService settlementLaborAccounting)
    {
        this.emergencyWorkAccounting = emergencyWorkAccounting;
        this.settlementLaborAccounting = settlementLaborAccounting;
    }

    internal double ActualLaborMilliWuCarry => actualLaborMilliWuCarry;

    internal void BeginRun()
    {
        Complete("superseded-before-work-run");
        activeEmergencyOperationId = string.Empty;
        emergencyAccountingSequence = 0L;
        // Keep cumulative fractions across routine-need interruption.
        latestApprovedLaborMilliWu = 0L;
    }

    internal void RecordApproved(
        CharacterId characterId,
        float approvedWork,
        float remainingWork,
        WorkTypeId workTypeId,
        int currentRunId)
    {
        if (emergencyWorkAccounting == null)
        {
            return;
        }

        if (!workTypeId.IsValid
            || !WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            || !characterId.IsValid)
        {
            throw new InvalidOperationException(
                "Emergency work accounting requires a registered work type and persistent worker identity: "
                + $"approvedWorkType={workTypeId.Value}; "
                + $"actor={characterId.Value}; "
                + $"run={currentRunId}.");
        }

        float knownRemaining = remainingWork >= 0f
            ? remainingWork
            : EmergencyWuUnits.ToWu(EmergencyWuUnits.MaximumReserveWindowMilliWu);
        long remainingMilliWu = EmergencyWuUnits.FromWu(knownRemaining);
        long approvedMilliWu = ConvertApprovedWorkToMilliWu(approvedWork);
        latestApprovedLaborMilliWu = approvedMilliWu;
        if (string.IsNullOrWhiteSpace(activeEmergencyOperationId))
        {
            string operationId =
                $"work:{characterId.Value}:{currentRunId}:{workTypeId.Value}";
            long initialRemaining = checked(
                remainingMilliWu + approvedMilliWu);
            long reserve = (definition.EmergencyFlags & EmergencyWorkFlags.ReserveEligible) != 0
                ? Math.Min(initialRemaining, EmergencyWuUnits.MaximumReserveWindowMilliWu)
                : 0L;
            EmergencyAccountingResult registration = emergencyWorkAccounting.Register(
                new EmergencyWorkLedgerEntry(
                    operationId,
                    characterId.Value,
                    workTypeId,
                    definition.EmergencyFlags,
                    initialRemaining,
                    reserve,
                    classificationRevision: 0,
                    mutationSequence: emergencyAccountingSequence));
            RequireEmergencyAccountingSuccess(registration);
            // Registration is the ownership commit point. Do not retain a
            // candidate operation ID when Register fails, otherwise cleanup
            // later attempts to remove an operation the ledger never owned and
            // hides the original lifecycle fault behind OperationMissing noise.
            activeEmergencyOperationId = operationId;
        }

        emergencyAccountingSequence = checked(emergencyAccountingSequence + 1L);
        EmergencyAccountingResult progress = emergencyWorkAccounting.ApplyProgress(
            new EmergencyWorkProgress(
                activeEmergencyOperationId,
                approvedMilliWu,
                remainingMilliWu,
                emergencyAccountingSequence));
        RequireEmergencyAccountingSuccess(progress);

        if (settlementLaborAccounting != null)
        {
            EmergencyAccountingResult labor = settlementLaborAccounting.Record(
                new SettlementLaborContribution(
                    activeEmergencyOperationId,
                    emergencyAccountingSequence,
                    SettlementLaborContributionChannel.ActualLabor,
                    approvedMilliWu,
                    workTypeId.Value));
            RequireEmergencyAccountingSuccess(labor);
            if (SettlementLaborBalanceRules.TryGetMaintenanceChannel(
                    workTypeId,
                    out SettlementLaborContributionChannel maintenanceChannel))
            {
                EmergencyAccountingResult maintenance =
                    settlementLaborAccounting.Record(
                        new SettlementLaborContribution(
                            activeEmergencyOperationId,
                            emergencyAccountingSequence,
                            maintenanceChannel,
                            approvedMilliWu,
                            workTypeId.Value));
                RequireEmergencyAccountingSuccess(maintenance);
            }
        }
    }

    private long ConvertApprovedWorkToMilliWu(float approvedWork)
    {
        if (float.IsNaN(approvedWork)
            || float.IsInfinity(approvedWork)
            || approvedWork < 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(approvedWork),
                approvedWork,
                "Approved work must be finite and non-negative.");
        }

        // Work progresses in small floating-point deltas. Rounding every tick
        // independently accumulated measurable drift against the authoritative
        // physical project progress. Carry the sub-milli remainder within the
        // active operation so the integer ledger tracks the rounded cumulative
        // total instead of the sum of rounded fragments.
        double exactMilliWu = approvedWork * EmergencyWuUnits.UnitsPerWu
            + actualLaborMilliWuCarry;
        long wholeMilliWu = checked((long)Math.Floor(exactMilliWu + 1e-9d));
        actualLaborMilliWuCarry = exactMilliWu - wholeMilliWu;
        return wholeMilliWu;
    }

    internal void Complete(string reason)
    {
        if (emergencyWorkAccounting == null
            || string.IsNullOrWhiteSpace(activeEmergencyOperationId))
        {
            return;
        }

        emergencyAccountingSequence = checked(emergencyAccountingSequence + 1L);
        string operationId = activeEmergencyOperationId;
        EmergencyAccountingResult result = emergencyWorkAccounting.Remove(
            new EmergencyWorkCompletion(
                operationId,
                $"{operationId}:{reason}",
                emergencyAccountingSequence));
        RequireEmergencyAccountingSuccess(result);
        // Retain the operation identity until the authority acknowledges removal.
        // A rejected completion must still be cancellable with the same owner.
        activeEmergencyOperationId = string.Empty;
        latestApprovedLaborMilliWu = 0L;
    }

    internal void RecordProjectOutputAdjustment(
        float outputEquivalentWork,
        WorkTypeId workTypeId)
    {
        if (settlementLaborAccounting == null
            || string.IsNullOrWhiteSpace(activeEmergencyOperationId)
            || !workTypeId.IsValid)
        {
            return;
        }

        double exactOutputMilliWu = outputEquivalentWork
            * EmergencyWuUnits.UnitsPerWu
            + projectOutputMilliWuCarry;
        long outputMilliWu = checked((long)Math.Floor(exactOutputMilliWu + 1e-9d));
        projectOutputMilliWuCarry = exactOutputMilliWu - outputMilliWu;
        long laborMilliWu = latestApprovedLaborMilliWu;
        long difference = checked(outputMilliWu - laborMilliWu);
        if (difference == 0L)
        {
            return;
        }

        SettlementLaborContributionChannel channel = difference > 0L
            ? SettlementLaborContributionChannel.ConvertedProcessOutput
            : SettlementLaborContributionChannel.FuelMaintenanceAccidentSpoilageLoss;
        EmergencyAccountingResult result = settlementLaborAccounting.Record(
            new SettlementLaborContribution(
                activeEmergencyOperationId,
                emergencyAccountingSequence,
                channel,
                Math.Abs(difference),
                workTypeId.Value));
        RequireEmergencyAccountingSuccess(result);
    }

    private static void RequireEmergencyAccountingSuccess(
        EmergencyAccountingResult result)
    {
        if (!result.Success)
        {
            throw new InvalidOperationException(
                $"{result.Code}: {result.Message}");
        }
    }

}
