public sealed class ActiveRunVariableSnapshot
{
    public ActiveRunVariableSnapshot(ActiveRunVariable source)
    {
        Definition = source?.Definition;
        StartDay = source?.StartDay ?? 0;
        RemainingDays = source?.RemainingDays ?? 0;
        InstanceSequence = source?.InstanceSequence ?? 0L;
        IsExpired = source == null || source.IsExpired;
    }

    public RunVariableDefinition Definition { get; }
    public int StartDay { get; }
    public int RemainingDays { get; }
    public long InstanceSequence { get; }
    public bool IsExpired { get; }
}

public readonly struct RunStartVariablesSelectedEvent
{
    public RunStartVariablesSelectedEvent(RunStartVariableSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }

    public RunStartVariableSnapshot snapshot { get; }
}

public readonly struct RunVariableActivatedEvent
{
    public RunVariableActivatedEvent(ActiveRunVariable activeVariable, bool announce = true)
    {
        this.activeVariable = new ActiveRunVariableSnapshot(activeVariable);
        Announce = announce;
    }

    public ActiveRunVariableSnapshot activeVariable { get; }
    public bool Announce { get; }
}

public readonly struct RunVariableExpiredEvent
{
    public RunVariableExpiredEvent(RunVariableDefinition definition)
    {
        this.definition = definition;
    }

    public RunVariableDefinition definition { get; }
}

public readonly struct InvasionVariableSelectedEvent
{
    public InvasionVariableSelectedEvent(RunVariableDefinition definition, bool announce = true)
    {
        this.definition = definition;
        Announce = announce;
    }

    public RunVariableDefinition definition { get; }
    public bool Announce { get; }
}
