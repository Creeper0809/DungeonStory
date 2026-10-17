using System;

/// <summary>
/// Defers an owner's pointer swaps until restore publication and keeps its exact
/// previous references for rollback. This owns only transaction bookkeeping;
/// the runtime owner supplies both publication and rollback operations.
/// </summary>
public sealed class DungeonRestorePublication
{
    private bool active;
    private bool attempted;
    private Action publication;
    private Func<Action> captureRollback;
    private Action rollback;

    public void Begin()
    {
        if (active)
            throw new InvalidOperationException("Restore publication is already active.");
        active = true;
    }

    public bool TryStage(Action publish, Func<Action> capturePrevious)
    {
        if (!active) return false;
        if (publication != null || attempted)
            throw new InvalidOperationException("Restore publication was already staged or attempted.");
        if (publish == null) throw new ArgumentNullException(nameof(publish));
        if (capturePrevious == null) throw new ArgumentNullException(nameof(capturePrevious));
        publication = publish;
        captureRollback = capturePrevious;
        return true;
    }

    public void Publish()
    {
        if (!active || attempted)
            throw new InvalidOperationException("Restore publication is inactive or already attempted.");
        // A participant may be registered without a section in a partial restore.
        if (publication == null) return;
        rollback = captureRollback()
            ?? throw new InvalidOperationException("Restore publication requires a rollback operation.");
        attempted = true;
        // Install rollback before calling the owner, including for partial throws.
        publication();
    }

    public void Discard()
    {
        try { rollback?.Invoke(); }
        finally { Complete(); }
    }

    public void Complete()
    {
        active = attempted = false;
        publication = rollback = null;
        captureRollback = null;
    }
}
