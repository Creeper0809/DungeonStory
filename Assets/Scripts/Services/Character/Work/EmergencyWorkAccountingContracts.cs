using System;

public static class EmergencyWuUnits
{
    public const long UnitsPerWu = 1000L;
    public const long MaximumReserveWindowMilliWu = 30L * UnitsPerWu;

    public static long FromWu(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "WU must be finite and non-negative.");
        }

        return checked((long)Math.Round(
            value * UnitsPerWu,
            MidpointRounding.AwayFromZero));
    }

    public static float ToWu(long value)
    {
        if (value < 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "milli-WU must be non-negative.");
        }

        return value / (float)UnitsPerWu;
    }
}

public readonly struct EmergencyAccountingResult
{
    private EmergencyAccountingResult(bool success, string code, string message)
    {
        Success = success;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public bool Success { get; }
    public string Code { get; }
    public string Message { get; }

    public static EmergencyAccountingResult Ok(string code = "ok") =>
        new EmergencyAccountingResult(true, code, string.Empty);

    public static EmergencyAccountingResult Fail(string code, string message) =>
        new EmergencyAccountingResult(false, code, message);
}

public readonly struct EmergencyWorkLedgerEntry
{
    public EmergencyWorkLedgerEntry(
        string operationId,
        string workerId,
        WorkTypeId workTypeId,
        EmergencyWorkFlags flags,
        long remainingMilliWu,
        long reserveContributionMilliWu,
        int classificationRevision,
        long mutationSequence)
    {
        OperationId = operationId?.Trim() ?? string.Empty;
        WorkerId = workerId?.Trim() ?? string.Empty;
        WorkTypeId = workTypeId;
        Flags = flags;
        RemainingMilliWu = remainingMilliWu;
        ReserveContributionMilliWu = reserveContributionMilliWu;
        ClassificationRevision = classificationRevision;
        MutationSequence = mutationSequence;
    }

    public string OperationId { get; }
    public string WorkerId { get; }
    public WorkTypeId WorkTypeId { get; }
    public EmergencyWorkFlags Flags { get; }
    public long RemainingMilliWu { get; }
    public long ReserveContributionMilliWu { get; }
    public int ClassificationRevision { get; }
    public long MutationSequence { get; }
}

public readonly struct EmergencyWorkProgress
{
    public EmergencyWorkProgress(
        string operationId,
        long approvedMilliWu,
        long remainingMilliWu,
        long eventSequence)
    {
        OperationId = operationId?.Trim() ?? string.Empty;
        ApprovedMilliWu = approvedMilliWu;
        RemainingMilliWu = remainingMilliWu;
        EventSequence = eventSequence;
    }

    public string OperationId { get; }
    public long ApprovedMilliWu { get; }
    public long RemainingMilliWu { get; }
    public long EventSequence { get; }
}

public readonly struct EmergencyWorkReclassification
{
    public EmergencyWorkReclassification(
        string operationId,
        EmergencyWorkFlags flags,
        long remainingMilliWu,
        long reserveContributionMilliWu,
        int classificationRevision,
        long eventSequence)
    {
        OperationId = operationId?.Trim() ?? string.Empty;
        Flags = flags;
        RemainingMilliWu = remainingMilliWu;
        ReserveContributionMilliWu = reserveContributionMilliWu;
        ClassificationRevision = classificationRevision;
        EventSequence = eventSequence;
    }

    public string OperationId { get; }
    public EmergencyWorkFlags Flags { get; }
    public long RemainingMilliWu { get; }
    public long ReserveContributionMilliWu { get; }
    public int ClassificationRevision { get; }
    public long EventSequence { get; }
}

public readonly struct EmergencyWorkCompletion
{
    public EmergencyWorkCompletion(
        string operationId,
        string completionToken,
        long eventSequence)
    {
        OperationId = operationId?.Trim() ?? string.Empty;
        CompletionToken = completionToken?.Trim() ?? string.Empty;
        EventSequence = eventSequence;
    }

    public string OperationId { get; }
    public string CompletionToken { get; }
    public long EventSequence { get; }
}

public readonly struct EmergencyReserveSnapshot
{
    public EmergencyReserveSnapshot(
        long reserveEligibleMilliWu,
        long interruptImmediatelyMilliWu,
        long interruptAtCheckpointMilliWu,
        long criticalNonInterruptibleMilliWu,
        long emergencyResponseMilliWu,
        int reserveEligibleWorkers,
        int protectedRecoveryWorkers,
        int activeOperationCount,
        long accountingRevision,
        ulong groundTruthHash,
        bool healthy,
        int correctionCount)
    {
        ReserveEligibleMilliWu = reserveEligibleMilliWu;
        InterruptImmediatelyMilliWu = interruptImmediatelyMilliWu;
        InterruptAtCheckpointMilliWu = interruptAtCheckpointMilliWu;
        CriticalNonInterruptibleMilliWu = criticalNonInterruptibleMilliWu;
        EmergencyResponseMilliWu = emergencyResponseMilliWu;
        ReserveEligibleWorkers = reserveEligibleWorkers;
        ProtectedRecoveryWorkers = protectedRecoveryWorkers;
        ActiveOperationCount = activeOperationCount;
        AccountingRevision = accountingRevision;
        GroundTruthHash = groundTruthHash;
        Healthy = healthy;
        CorrectionCount = correctionCount;
    }

    public long ReserveEligibleMilliWu { get; }
    public long InterruptImmediatelyMilliWu { get; }
    public long InterruptAtCheckpointMilliWu { get; }
    public long CriticalNonInterruptibleMilliWu { get; }
    public long EmergencyResponseMilliWu { get; }
    public int ReserveEligibleWorkers { get; }
    public int ProtectedRecoveryWorkers { get; }
    public int ActiveOperationCount { get; }
    public long AccountingRevision { get; }
    public ulong GroundTruthHash { get; }
    public bool Healthy { get; }
    public int CorrectionCount { get; }
}

public enum EmergencyAccountingReconciliationTrigger
{
    OperatingDayEnded = 0,
    BeforeSaveCapture = 1,
    AfterRestore = 2,
    BeforeQualifiedRedEscalation = 3,
    DeveloperAudit = 4
}

public readonly struct EmergencyAccountingReconciliationResult
{
    public EmergencyAccountingReconciliationResult(
        bool success,
        bool driftDetected,
        bool corrected,
        string diagnostic,
        EmergencyReserveSnapshot snapshot)
    {
        Success = success;
        DriftDetected = driftDetected;
        Corrected = corrected;
        Diagnostic = diagnostic ?? string.Empty;
        Snapshot = snapshot;
    }

    public bool Success { get; }
    public bool DriftDetected { get; }
    public bool Corrected { get; }
    public string Diagnostic { get; }
    public EmergencyReserveSnapshot Snapshot { get; }
}

public interface IEmergencyWorkAccountingService
{
    EmergencyAccountingResult Register(EmergencyWorkLedgerEntry entry);
    EmergencyAccountingResult ApplyProgress(EmergencyWorkProgress progress);
    EmergencyAccountingResult Reclassify(EmergencyWorkReclassification change);
    EmergencyAccountingResult Remove(EmergencyWorkCompletion completion);
    EmergencyReserveSnapshot CaptureSnapshot();
}

public interface IEmergencyWorkAccountingReconciler
{
    EmergencyAccountingReconciliationResult Reconcile(
        EmergencyAccountingReconciliationTrigger trigger);
}
