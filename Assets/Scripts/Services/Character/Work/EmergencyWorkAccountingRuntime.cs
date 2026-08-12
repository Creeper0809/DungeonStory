using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class EmergencyWorkAccountingRuntime :
    IEmergencyWorkAccountingService,
    IEmergencyWorkAccountingReconciler,
    IDungeonSaveCaptureGuard,
    IDungeonSaveRestoreCompletedHook,
    IStartable,
    IDisposable
{
    private const int MaximumRememberedCompletionTokens = 4096;
    private readonly IGameEventBus events;
    private readonly Dictionary<string, EmergencyWorkLedgerEntry> entriesByOperation =
        new Dictionary<string, EmergencyWorkLedgerEntry>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> operationByWorker =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> completionTokens =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Queue<string> completionTokenOrder = new Queue<string>();

    private IDisposable dayEndedSubscription;
    private AccountingTotals totals;
    private long accountingRevision;
    private int correctionCount;
    private bool healthy = true;

    public EmergencyWorkAccountingRuntime(IGameEventBus events)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        dayEndedSubscription ??= events.Subscribe<OperatingDayEndedEvent>(OnDayEnded);
    }

    public void Dispose()
    {
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }

    public EmergencyAccountingResult Register(EmergencyWorkLedgerEntry entry)
    {
        EmergencyAccountingResult validation = ValidateEntry(entry);
        if (!validation.Success)
        {
            healthy = false;
            return validation;
        }

        if (entriesByOperation.TryGetValue(entry.OperationId, out EmergencyWorkLedgerEntry existing))
        {
            return existing.MutationSequence == entry.MutationSequence
                && EntriesEqual(existing, entry)
                ? EmergencyAccountingResult.Ok("duplicate-register-ignored")
                : EmergencyAccountingResult.Fail(
                    "EmergencyWorkOperationDuplicate",
                    $"Operation '{entry.OperationId}' is already registered with a different revision.");
        }

        if (operationByWorker.TryGetValue(entry.WorkerId, out string activeOperation))
        {
            healthy = false;
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkWorkerAlreadyActive",
                $"Worker '{entry.WorkerId}' already owns active operation '{activeOperation}'.");
        }

        entriesByOperation.Add(entry.OperationId, entry);
        operationByWorker.Add(entry.WorkerId, entry.OperationId);
        AddContribution(ref totals, entry);
        accountingRevision = checked(accountingRevision + 1L);
        return EmergencyAccountingResult.Ok("registered");
    }

    public EmergencyAccountingResult ApplyProgress(EmergencyWorkProgress progress)
    {
        if (!entriesByOperation.TryGetValue(progress.OperationId, out EmergencyWorkLedgerEntry existing))
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkOperationMissing",
                $"Operation '{progress.OperationId}' does not exist.");
        }

        if (progress.EventSequence <= existing.MutationSequence)
        {
            return EmergencyAccountingResult.Ok("duplicate-progress-ignored");
        }

        if (progress.ApprovedMilliWu < 0L || progress.RemainingMilliWu < 0L)
        {
            healthy = false;
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkProgressInvalid",
                $"Operation '{progress.OperationId}' has negative progress or remaining WU.");
        }

        long reserve = CalculateReserveContribution(existing.Flags, progress.RemainingMilliWu);
        EmergencyWorkLedgerEntry updated = new EmergencyWorkLedgerEntry(
            existing.OperationId,
            existing.WorkerId,
            existing.WorkTypeId,
            existing.Flags,
            progress.RemainingMilliWu,
            reserve,
            existing.ClassificationRevision,
            progress.EventSequence);
        ReplaceEntry(existing, updated);
        return EmergencyAccountingResult.Ok("progress-applied");
    }

    public EmergencyAccountingResult Reclassify(EmergencyWorkReclassification change)
    {
        if (!entriesByOperation.TryGetValue(change.OperationId, out EmergencyWorkLedgerEntry existing))
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkOperationMissing",
                $"Operation '{change.OperationId}' does not exist.");
        }

        if (change.EventSequence <= existing.MutationSequence)
        {
            return EmergencyAccountingResult.Ok("duplicate-reclassification-ignored");
        }

        if (change.ClassificationRevision <= existing.ClassificationRevision)
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkClassificationRevisionInvalid",
                $"Operation '{change.OperationId}' classification revision did not advance.");
        }

        EmergencyWorkLedgerEntry updated = new EmergencyWorkLedgerEntry(
            existing.OperationId,
            existing.WorkerId,
            existing.WorkTypeId,
            change.Flags,
            change.RemainingMilliWu,
            change.ReserveContributionMilliWu,
            change.ClassificationRevision,
            change.EventSequence);
        EmergencyAccountingResult validation = ValidateEntry(updated);
        if (!validation.Success)
        {
            healthy = false;
            return validation;
        }

        ReplaceEntry(existing, updated);
        return EmergencyAccountingResult.Ok("reclassified");
    }

    public EmergencyAccountingResult Remove(EmergencyWorkCompletion completion)
    {
        if (string.IsNullOrWhiteSpace(completion.CompletionToken))
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkCompletionTokenMissing",
                "Completion token is required.");
        }

        if (completionTokens.Contains(completion.CompletionToken))
        {
            return EmergencyAccountingResult.Ok("duplicate-completion-ignored");
        }

        if (!entriesByOperation.TryGetValue(completion.OperationId, out EmergencyWorkLedgerEntry existing))
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkOperationMissing",
                $"Operation '{completion.OperationId}' does not exist.");
        }

        if (completion.EventSequence <= existing.MutationSequence)
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkCompletionSequenceInvalid",
                $"Operation '{completion.OperationId}' completion sequence did not advance.");
        }

        RemoveContribution(ref totals, existing);
        entriesByOperation.Remove(existing.OperationId);
        operationByWorker.Remove(existing.WorkerId);
        RememberCompletionToken(completion.CompletionToken);
        accountingRevision = checked(accountingRevision + 1L);
        return EmergencyAccountingResult.Ok("removed");
    }

    public EmergencyReserveSnapshot CaptureSnapshot() =>
        CreateSnapshot(totals, ComputeStableHash(), healthy);

    public EmergencyAccountingReconciliationResult Reconcile(
        EmergencyAccountingReconciliationTrigger trigger)
    {
        if (!TryBuildGroundTruth(out AccountingTotals recomputed, out ulong hash, out string error))
        {
            healthy = false;
            return new EmergencyAccountingReconciliationResult(
                false,
                false,
                false,
                error,
                CreateSnapshot(totals, ComputeStableHash(), false));
        }

        bool drift = !totals.Equals(recomputed);
        if (drift)
        {
            Debug.LogWarning(
                $"[EmergencyWorkAccounting] Drift detected during {trigger}; "
                + $"cached={totals}; groundTruth={recomputed}. The ground truth replaced the cache.");
            totals = recomputed;
            correctionCount = checked(correctionCount + 1);
            accountingRevision = checked(accountingRevision + 1L);
        }

        healthy = true;
        EmergencyReserveSnapshot snapshot = CreateSnapshot(totals, hash, true);
        return new EmergencyAccountingReconciliationResult(
            true,
            drift,
            drift,
            drift ? "EmergencyWorkAccountingDriftCorrected" : "EmergencyWorkAccountingMatched",
            snapshot);
    }

    public void ValidateBeforeCapture()
    {
        EmergencyAccountingReconciliationResult result = Reconcile(
            EmergencyAccountingReconciliationTrigger.BeforeSaveCapture);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Diagnostic);
        }
    }

    public void OnRestoreCompleted()
    {
        entriesByOperation.Clear();
        operationByWorker.Clear();
        completionTokens.Clear();
        completionTokenOrder.Clear();
        totals = default;
        healthy = true;
        accountingRevision = checked(accountingRevision + 1L);
        EmergencyAccountingReconciliationResult result = Reconcile(
            EmergencyAccountingReconciliationTrigger.AfterRestore);
        if (!result.Success)
        {
            throw new InvalidOperationException(result.Diagnostic);
        }
    }

    private void OnDayEnded(OperatingDayEndedEvent _) =>
        Reconcile(EmergencyAccountingReconciliationTrigger.OperatingDayEnded);

    private void RememberCompletionToken(string token)
    {
        completionTokens.Add(token);
        completionTokenOrder.Enqueue(token);
        while (completionTokenOrder.Count > MaximumRememberedCompletionTokens)
        {
            completionTokens.Remove(completionTokenOrder.Dequeue());
        }
    }

    private void ReplaceEntry(
        EmergencyWorkLedgerEntry previous,
        EmergencyWorkLedgerEntry updated)
    {
        RemoveContribution(ref totals, previous);
        entriesByOperation[updated.OperationId] = updated;
        AddContribution(ref totals, updated);
        accountingRevision = checked(accountingRevision + 1L);
    }

    private EmergencyAccountingResult ValidateEntry(EmergencyWorkLedgerEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.OperationId)
            || string.IsNullOrWhiteSpace(entry.WorkerId)
            || !entry.WorkTypeId.IsValid)
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkIdentityInvalid",
                "Operation, worker and work type IDs are required.");
        }

        if (!WorkTypeCatalog.TryGet(entry.WorkTypeId, out _))
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkTypeUnknown",
                $"Work type '{entry.WorkTypeId.Value}' is not registered.");
        }

        if (!IsValidFlagCombination(entry.Flags))
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkFlagsInvalid",
                $"Operation '{entry.OperationId}' has invalid flags '{entry.Flags}'.");
        }

        if (entry.RemainingMilliWu < 0L
            || entry.ReserveContributionMilliWu < 0L
            || entry.ReserveContributionMilliWu > entry.RemainingMilliWu
            || entry.ReserveContributionMilliWu > EmergencyWuUnits.MaximumReserveWindowMilliWu
            || entry.ClassificationRevision < 0
            || entry.MutationSequence < 0L)
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkAmountsInvalid",
                $"Operation '{entry.OperationId}' has invalid WU or revision values.");
        }

        if ((entry.Flags & EmergencyWorkFlags.ReserveEligible) == 0
            && entry.ReserveContributionMilliWu != 0L)
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyWorkReserveContributionInvalid",
                $"Non-reserve operation '{entry.OperationId}' contributes reserve WU.");
        }

        return EmergencyAccountingResult.Ok();
    }

    private bool TryBuildGroundTruth(
        out AccountingTotals recomputed,
        out ulong hash,
        out string error)
    {
        recomputed = default;
        hash = FnvOffset;
        error = string.Empty;
        List<string> operationIds = new List<string>(entriesByOperation.Keys);
        operationIds.Sort(StringComparer.Ordinal);
        HashSet<string> workers = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < operationIds.Count; index++)
        {
            EmergencyWorkLedgerEntry entry = entriesByOperation[operationIds[index]];
            EmergencyAccountingResult validation = ValidateEntry(entry);
            if (!validation.Success)
            {
                error = validation.Code + ": " + validation.Message;
                return false;
            }

            if (!workers.Add(entry.WorkerId))
            {
                error = $"EmergencyWorkWorkerDuplicate: worker '{entry.WorkerId}' owns multiple active operations.";
                return false;
            }

            AddContribution(ref recomputed, entry);
            hash = Hash(hash, entry.OperationId);
            hash = Hash(hash, entry.WorkerId);
            hash = Hash(hash, entry.WorkTypeId.Value);
            hash = Hash(hash, (long)entry.Flags);
            hash = Hash(hash, entry.RemainingMilliWu);
            hash = Hash(hash, entry.ReserveContributionMilliWu);
            hash = Hash(hash, entry.ClassificationRevision);
            hash = Hash(hash, entry.MutationSequence);
        }

        return true;
    }

    private ulong ComputeStableHash()
    {
        return TryBuildGroundTruth(out _, out ulong hash, out _)
            ? hash
            : 0UL;
    }

    private EmergencyReserveSnapshot CreateSnapshot(
        AccountingTotals source,
        ulong hash,
        bool isHealthy) =>
        new EmergencyReserveSnapshot(
            source.ReserveEligibleMilliWu,
            source.InterruptImmediatelyMilliWu,
            source.InterruptAtCheckpointMilliWu,
            source.CriticalNonInterruptibleMilliWu,
            source.EmergencyResponseMilliWu,
            source.ReserveEligibleWorkers,
            source.ProtectedRecoveryWorkers,
            entriesByOperation.Count,
            accountingRevision,
            hash,
            isHealthy,
            correctionCount);

    private static long CalculateReserveContribution(
        EmergencyWorkFlags flags,
        long remainingMilliWu) =>
        (flags & EmergencyWorkFlags.ReserveEligible) != 0
            ? Math.Min(remainingMilliWu, EmergencyWuUnits.MaximumReserveWindowMilliWu)
            : 0L;

    private static bool IsValidFlagCombination(EmergencyWorkFlags flags)
    {
        int interruptKinds = ((flags & EmergencyWorkFlags.InterruptImmediately) != 0 ? 1 : 0)
            + ((flags & EmergencyWorkFlags.InterruptAtCheckpoint) != 0 ? 1 : 0);
        bool reserve = (flags & EmergencyWorkFlags.ReserveEligible) != 0;
        int exclusiveKinds = ((flags & EmergencyWorkFlags.CriticalNonInterruptible) != 0 ? 1 : 0)
            + ((flags & EmergencyWorkFlags.EmergencyResponse) != 0 ? 1 : 0)
            + ((flags & EmergencyWorkFlags.ProtectedRecovery) != 0 ? 1 : 0);
        return reserve
            ? interruptKinds == 1 && exclusiveKinds == 0
            : interruptKinds == 0 && exclusiveKinds == 1;
    }

    private static bool EntriesEqual(
        EmergencyWorkLedgerEntry left,
        EmergencyWorkLedgerEntry right) =>
        string.Equals(left.OperationId, right.OperationId, StringComparison.Ordinal)
        && string.Equals(left.WorkerId, right.WorkerId, StringComparison.Ordinal)
        && left.WorkTypeId.Equals(right.WorkTypeId)
        && left.Flags == right.Flags
        && left.RemainingMilliWu == right.RemainingMilliWu
        && left.ReserveContributionMilliWu == right.ReserveContributionMilliWu
        && left.ClassificationRevision == right.ClassificationRevision
        && left.MutationSequence == right.MutationSequence;

    private static void AddContribution(
        ref AccountingTotals destination,
        EmergencyWorkLedgerEntry entry)
    {
        destination.ActiveOperations = checked(destination.ActiveOperations + 1);
        if ((entry.Flags & EmergencyWorkFlags.ReserveEligible) != 0)
        {
            destination.ReserveEligibleMilliWu = checked(
                destination.ReserveEligibleMilliWu + entry.ReserveContributionMilliWu);
            destination.ReserveEligibleWorkers = checked(destination.ReserveEligibleWorkers + 1);
        }
        if ((entry.Flags & EmergencyWorkFlags.InterruptImmediately) != 0)
            destination.InterruptImmediatelyMilliWu = checked(destination.InterruptImmediatelyMilliWu + entry.ReserveContributionMilliWu);
        if ((entry.Flags & EmergencyWorkFlags.InterruptAtCheckpoint) != 0)
            destination.InterruptAtCheckpointMilliWu = checked(destination.InterruptAtCheckpointMilliWu + entry.ReserveContributionMilliWu);
        if ((entry.Flags & EmergencyWorkFlags.CriticalNonInterruptible) != 0)
            destination.CriticalNonInterruptibleMilliWu = checked(destination.CriticalNonInterruptibleMilliWu + entry.RemainingMilliWu);
        if ((entry.Flags & EmergencyWorkFlags.EmergencyResponse) != 0)
            destination.EmergencyResponseMilliWu = checked(destination.EmergencyResponseMilliWu + entry.RemainingMilliWu);
        if ((entry.Flags & EmergencyWorkFlags.ProtectedRecovery) != 0)
            destination.ProtectedRecoveryWorkers = checked(destination.ProtectedRecoveryWorkers + 1);
    }

    private static void RemoveContribution(
        ref AccountingTotals destination,
        EmergencyWorkLedgerEntry entry)
    {
        destination.ActiveOperations = checked(destination.ActiveOperations - 1);
        if ((entry.Flags & EmergencyWorkFlags.ReserveEligible) != 0)
        {
            destination.ReserveEligibleMilliWu = checked(destination.ReserveEligibleMilliWu - entry.ReserveContributionMilliWu);
            destination.ReserveEligibleWorkers = checked(destination.ReserveEligibleWorkers - 1);
        }
        if ((entry.Flags & EmergencyWorkFlags.InterruptImmediately) != 0)
            destination.InterruptImmediatelyMilliWu = checked(destination.InterruptImmediatelyMilliWu - entry.ReserveContributionMilliWu);
        if ((entry.Flags & EmergencyWorkFlags.InterruptAtCheckpoint) != 0)
            destination.InterruptAtCheckpointMilliWu = checked(destination.InterruptAtCheckpointMilliWu - entry.ReserveContributionMilliWu);
        if ((entry.Flags & EmergencyWorkFlags.CriticalNonInterruptible) != 0)
            destination.CriticalNonInterruptibleMilliWu = checked(destination.CriticalNonInterruptibleMilliWu - entry.RemainingMilliWu);
        if ((entry.Flags & EmergencyWorkFlags.EmergencyResponse) != 0)
            destination.EmergencyResponseMilliWu = checked(destination.EmergencyResponseMilliWu - entry.RemainingMilliWu);
        if ((entry.Flags & EmergencyWorkFlags.ProtectedRecovery) != 0)
            destination.ProtectedRecoveryWorkers = checked(destination.ProtectedRecoveryWorkers - 1);
    }

    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static ulong Hash(ulong current, string value)
    {
        string normalized = value ?? string.Empty;
        for (int index = 0; index < normalized.Length; index++)
        {
            current ^= normalized[index];
            current *= FnvPrime;
        }
        return current;
    }

    private static ulong Hash(ulong current, long value)
    {
        unchecked
        {
            for (int shift = 0; shift < 64; shift += 8)
            {
                current ^= (byte)(value >> shift);
                current *= FnvPrime;
            }
            return current;
        }
    }

    private struct AccountingTotals : IEquatable<AccountingTotals>
    {
        public long ReserveEligibleMilliWu;
        public long InterruptImmediatelyMilliWu;
        public long InterruptAtCheckpointMilliWu;
        public long CriticalNonInterruptibleMilliWu;
        public long EmergencyResponseMilliWu;
        public int ReserveEligibleWorkers;
        public int ProtectedRecoveryWorkers;
        public int ActiveOperations;

        public bool Equals(AccountingTotals other) =>
            ReserveEligibleMilliWu == other.ReserveEligibleMilliWu
            && InterruptImmediatelyMilliWu == other.InterruptImmediatelyMilliWu
            && InterruptAtCheckpointMilliWu == other.InterruptAtCheckpointMilliWu
            && CriticalNonInterruptibleMilliWu == other.CriticalNonInterruptibleMilliWu
            && EmergencyResponseMilliWu == other.EmergencyResponseMilliWu
            && ReserveEligibleWorkers == other.ReserveEligibleWorkers
            && ProtectedRecoveryWorkers == other.ProtectedRecoveryWorkers
            && ActiveOperations == other.ActiveOperations;

        public override string ToString() =>
            $"reserve={ReserveEligibleMilliWu}, immediate={InterruptImmediatelyMilliWu}, "
            + $"checkpoint={InterruptAtCheckpointMilliWu}, critical={CriticalNonInterruptibleMilliWu}, "
            + $"response={EmergencyResponseMilliWu}, workers={ReserveEligibleWorkers}, "
            + $"recovery={ProtectedRecoveryWorkers}, operations={ActiveOperations}";
    }
}
