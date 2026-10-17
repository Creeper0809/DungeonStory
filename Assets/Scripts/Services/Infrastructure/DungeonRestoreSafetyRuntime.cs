using System;
using DungeonStory.Foundation;

/// <summary>
/// Owns the current container's admission block after a published restore
/// cannot finish. A fresh scene/container is the recovery boundary.
/// </summary>
public sealed class DungeonRestoreSafetyRuntime : IDungeonRestoreSafetyQuery
{
    private readonly IGameSessionPauseAuthority pauseAuthority;
    private readonly IGameTimeScaleController timeScale;

    public DungeonRestoreSafetyRuntime(
        IGameSessionPauseAuthority pauseAuthority,
        IGameTimeScaleController timeScale)
    {
        this.pauseAuthority = pauseAuthority
            ?? throw new ArgumentNullException(nameof(pauseAuthority));
        this.timeScale = timeScale ?? throw new ArgumentNullException(nameof(timeScale));
    }

    public bool RequiresRecovery { get; private set; }
    public string RecoveryMessage { get; private set; } = string.Empty;

    internal void ObserveRestoreResult(DungeonGameRestoreReport report)
    {
        if (!report.RequiresRecovery) return;
        RequiresRecovery = true;
        RecoveryMessage = report.FailureDescription;
        try { pauseAuthority.SetPaused(true); }
        catch (Exception exception)
        {
            report.AddError("Published restore could not pause the session: " + exception.Message);
        }
        try { timeScale.Scale = 0f; }
        catch (Exception exception)
        {
            report.AddError("Published restore could not stop game time: " + exception.Message);
        }
    }

    internal void RequireSafeState(string operation)
    {
        if (RequiresRecovery)
            throw new InvalidOperationException(operation + ": " + RecoveryMessage);
    }
}
