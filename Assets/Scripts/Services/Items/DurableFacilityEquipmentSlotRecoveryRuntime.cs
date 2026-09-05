using System;
using System.Collections.Generic;
using System.Linq;
using VContainer.Unity;

/// <summary>
/// Owner-neutral forward-retry driver for restored durable equipment slots.
/// Policies provide content; this runner only advances the shared lifecycle.
/// </summary>
public sealed class DurableFacilityEquipmentSlotRecoveryRuntime :
    IStartable,
    ITickable,
    IDungeonSaveRestoreCompletedHook,
    IDungeonSaveCaptureGuard
{
    private readonly IDurableFacilityEquipmentSlotCommand command;
    private readonly IDurableFacilityEquipmentSlotQuery query;
    private bool resumeRequested;
    private string unresolvedConflict = string.Empty;

    public DurableFacilityEquipmentSlotRecoveryRuntime(
        IDurableFacilityEquipmentSlotCommand command,
        IDurableFacilityEquipmentSlotQuery query)
    {
        this.command = command ?? throw new ArgumentNullException(nameof(command));
        this.query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public void Start() => resumeRequested = true;

    public void OnRestoreCompleted()
    {
        DrivePendingToCompletion();
        resumeRequested = false;
    }

    public void Tick()
    {
        if (!resumeRequested)
            return;
        resumeRequested = false;
        unresolvedConflict = string.Empty;
        try
        {
            DrivePendingToCompletion();
        }
        catch (Exception exception)
        {
            unresolvedConflict = exception.Message;
        }
    }

    public void ValidateBeforeCapture()
    {
        if (unresolvedConflict.Length != 0)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment recovery has an unresolved conflict: "
                + unresolvedConflict);
        }
    }

    private DurableFacilityEquipmentSlotSnapshot[] Pending() => query
        .CaptureAll()
        .Where(value => value.LifecyclePhase is
            DurableFacilityEquipmentSlotLifecyclePhase.CloseRequested
            or DurableFacilityEquipmentSlotLifecyclePhase.Draining)
        .OrderBy(value => value.AssignmentSequence)
        .ToArray();

    private void DrivePendingToCompletion()
    {
        unresolvedConflict = string.Empty;
        HashSet<string> visited = new(StringComparer.Ordinal);
        while (true)
        {
            DurableFacilityEquipmentSlotSnapshot[] before = Pending();
            if (before.Length == 0)
                return;
            string beforeDigest = Digest(before);
            if (!visited.Add(beforeDigest))
            {
                throw new InvalidOperationException(
                    "durable-equipment-slot-recovery-made-no-monotonic-progress");
            }
            DurableFacilityEquipmentSlotResult[] results = command
                .TryAdvancePending()
                .ToArray();
            DurableFacilityEquipmentSlotResult conflict = results
                .FirstOrDefault(value =>
                    value.Status == DurableFacilityEquipmentSlotStatus.Conflict);
            if (conflict.Status == DurableFacilityEquipmentSlotStatus.Conflict)
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(conflict.FailureReason)
                        ? "durable-equipment-slot-recovery-conflict"
                        : conflict.FailureReason);
            }
            DurableFacilityEquipmentSlotSnapshot[] after = Pending();
            Dictionary<long, DurableFacilityEquipmentSlotSnapshot> afterBySequence =
                after.ToDictionary(
                    value => value.AssignmentSequence,
                    value => value);
            foreach (DurableFacilityEquipmentSlotSnapshot previous in before)
            {
                if (!afterBySequence.TryGetValue(
                        previous.AssignmentSequence,
                        out DurableFacilityEquipmentSlotSnapshot current))
                {
                    continue;
                }
                if (CompareProgress(current, previous) >= 0)
                {
                    throw new InvalidOperationException(
                        "durable-equipment-slot-recovery-rank-stalled:"
                        + previous.AssignmentSequence);
                }
            }
        }
    }

    private static int CompareProgress(
        DurableFacilityEquipmentSlotSnapshot left,
        DurableFacilityEquipmentSlotSnapshot right)
    {
        ProgressMarker leftMarker = ProgressMarker.From(left);
        ProgressMarker rightMarker = ProgressMarker.From(right);
        int stage = leftMarker.Stage.CompareTo(rightMarker.Stage);
        if (stage != 0)
            return stage;
        int actors = leftMarker.RemainingActors.CompareTo(
            rightMarker.RemainingActors);
        if (actors != 0)
            return actors;
        return leftMarker.RemainingOperations.CompareTo(
            rightMarker.RemainingOperations);
    }

    private static string Digest(
        DurableFacilityEquipmentSlotSnapshot[] values) => string.Join(
        "|",
        values.Select(value => string.Join(
            ":",
            value.AssignmentSequence,
            (int)value.LifecyclePhase,
            value.AuthoritiesRevoked ? 1 : 0,
            value.Drain == null ? -1 : (int)value.Drain.Phase,
            value.Drain == null
                ? 0
                : value.Drain.SourceActorCount
                    - value.Drain.CompletedActorCount,
            value.Drain == null
                ? 0
                : value.Drain.SourceOperationCount
                    - value.Drain.ReleasedOperationCount,
            value.Drain?.RequestFingerprint ?? string.Empty,
            value.Drain?.ReceiptFingerprint ?? string.Empty)));

    private readonly struct ProgressMarker
    {
        private ProgressMarker(
            int stage,
            int remainingActors,
            int remainingOperations)
        {
            Stage = stage;
            RemainingActors = remainingActors;
            RemainingOperations = remainingOperations;
        }

        internal int Stage { get; }
        internal int RemainingActors { get; }
        internal int RemainingOperations { get; }

        internal static ProgressMarker From(
            DurableFacilityEquipmentSlotSnapshot value)
        {
            if (value.LifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase.CloseRequested)
            {
                return new ProgressMarker(6, 0, 0);
            }
            FacilityBufferDestinationCustodyDrainSnapshot child = value.Drain
                ?? throw new InvalidOperationException(
                    "Draining durable equipment slot has no child.");
            return new ProgressMarker(
                5 - (int)child.Phase,
                Math.Max(0,
                    child.SourceActorCount - child.CompletedActorCount),
                Math.Max(0,
                    child.SourceOperationCount - child.ReleasedOperationCount));
        }
    }
}
