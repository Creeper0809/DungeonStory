using System;

/// <summary>The exact manual door transition retained by the door's own outbox.</summary>
public sealed class DoorHeldOpenFact
{
    private readonly DoorHeldOpenPendingSaveData state;
    public DoorHeldOpenFact(DoorHeldOpenPendingSaveData source)
    {
        if (!DoorHeldOpenPendingSaveData.IsValid(source))
            throw new ArgumentException("An exact manual door transition is required.", nameof(source));
        state = source.Clone();
    }
    public DoorHeldOpenPendingSaveData Snapshot() => state.Clone();
}
