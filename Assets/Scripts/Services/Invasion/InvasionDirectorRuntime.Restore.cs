using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class InvasionDirectorRestoreCoordinator
{
    private readonly List<InvasionIntruderRuntime> restoreCandidates = new();
    private bool restoreCandidatePrepared;
    private bool restorePublicationPending;

    public IReadOnlyList<InvasionIntruderPersistenceState> Capture(
        IEnumerable<InvasionIntruderRuntime> activeIntruders,
        Grid grid)
    {
        if (grid == null)
        {
            return Array.Empty<InvasionIntruderPersistenceState>();
        }

        return (activeIntruders ?? Array.Empty<InvasionIntruderRuntime>())
            .Where(runtime => runtime != null
                && runtime.State != InvasionIntruderState.Finished
                && runtime.IntruderActor != null
                && !runtime.IntruderActor.IsDead)
            .Select(runtime => runtime.CapturePersistentState(grid))
            .ToArray();
    }

    public int Prepare(
        IEnumerable<InvasionIntruderPersistenceState> restoredIntruders,
        DungeonGameRestoreReport report,
        Func<CharacterSO> resolveIntruderData,
        Func<InvasionIntruderPersistenceState, InvasionIntruderRuntime> createDetached,
        Action<InvasionIntruderRuntime> initialize,
        Action<InvasionIntruderRuntime> destroyDetached)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (restoreCandidatePrepared || restoreCandidates.Count > 0)
        {
            report.AddError(
                "An invasion intruder restore candidate is already prepared.");
            return 0;
        }

        CharacterSO data = (resolveIntruderData
            ?? throw new ArgumentNullException(nameof(resolveIntruderData)))();
        if (data == null)
        {
            report.AddError(
                "Invasion restore requires authored intruder character data.");
            return 0;
        }

        try
        {
            foreach (InvasionIntruderPersistenceState source in
                     restoredIntruders ?? Array.Empty<InvasionIntruderPersistenceState>())
            {
                if (source == null || source.DataId != data.id)
                {
                    report.AddError(
                        $"Invasion intruder data '{source?.DataId ?? -1}' is invalid.");
                    break;
                }

                InvasionIntruderRuntime runtime =
                    (createDetached
                        ?? throw new ArgumentNullException(nameof(createDetached)))(source);
                (initialize
                    ?? throw new ArgumentNullException(nameof(initialize)))(runtime);
                if (!runtime.TryPrepareRestore(
                        data,
                        source,
                        finalDefenseTarget: null,
                        out string error))
                {
                    (destroyDetached
                        ?? throw new ArgumentNullException(nameof(destroyDetached)))(runtime);
                    report.AddError(
                        string.IsNullOrWhiteSpace(error)
                            ? "Invasion intruder restore candidate is invalid."
                            : error);
                    break;
                }
                restoreCandidates.Add(runtime);
            }
        }
        catch (Exception exception)
        {
            report.AddError(
                $"Invasion intruder candidate preparation failed: {exception.Message}");
        }

        if (!report.Success)
        {
            Discard(destroyDetached);
            return 0;
        }

        restoreCandidatePrepared = true;
        return restoreCandidates.Count;
    }

    public void Publish()
    {
        if (!restoreCandidatePrepared || restorePublicationPending)
        {
            throw new InvalidOperationException(
                "No invasion intruder restore candidate is ready to publish.");
        }

        restorePublicationPending = true;
    }

    public void Rollback(Action<InvasionIntruderRuntime> destroyDetached)
    {
        DiscardPrepared(destroyDetached);
        restorePublicationPending = false;
    }

    public void Complete(
        Action clearActiveIntruders,
        Action<InvasionIntruderRuntime> publishCandidate)
    {
        if (!restorePublicationPending)
        {
            return;
        }

        (clearActiveIntruders
            ?? throw new ArgumentNullException(nameof(clearActiveIntruders)))();
        foreach (InvasionIntruderRuntime runtime in restoreCandidates)
        {
            (publishCandidate
                ?? throw new ArgumentNullException(nameof(publishCandidate)))(runtime);
        }
        restoreCandidates.Clear();
        restoreCandidatePrepared = false;
        restorePublicationPending = false;
    }

    public void Discard(Action<InvasionIntruderRuntime> destroyDetached)
    {
        if (restorePublicationPending)
        {
            Rollback(destroyDetached);
            return;
        }

        DiscardPrepared(destroyDetached);
    }

    private void DiscardPrepared(Action<InvasionIntruderRuntime> destroyDetached)
    {
        foreach (InvasionIntruderRuntime runtime in restoreCandidates)
        {
            (destroyDetached
                ?? throw new ArgumentNullException(nameof(destroyDetached)))(runtime);
        }
        restoreCandidates.Clear();
        restoreCandidatePrepared = false;
    }

    public bool TryGet(
        string persistentId,
        out InvasionIntruderRuntime candidate)
    {
        candidate = restoreCandidates.FirstOrDefault(runtime =>
            runtime != null
            && string.Equals(
                runtime.IntruderActor?.Identity?.PersistentId,
                persistentId,
                StringComparison.Ordinal));
        return candidate != null;
    }

}
