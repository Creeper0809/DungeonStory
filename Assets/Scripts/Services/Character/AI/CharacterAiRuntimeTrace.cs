using System;
using System.Text;

public enum CharacterAiRuntimeTraceKind : byte
{
    None = 0,
    ActionStarted = 1,
    ActionTerminal = 2,
    PhaseChanged = 3,
    ExecutionFailure = 4,
    DeferredRetry = 5,
    LifecycleCleanup = 6,
    OrphanRecovery = 7
}
/// <summary>
/// Allocation-free, fixed-width evidence captured on AI lifecycle transitions.
/// Human-readable strings are produced only when a diagnostic snapshot is requested.
/// </summary>
public readonly struct CharacterAiRuntimeTraceEvent
{
    public CharacterAiRuntimeTraceEvent(
        long sequence,
        long actionEpoch,
        float gameTime,
        CharacterAiRuntimeTraceKind kind,
        CharacterAiBranch branch,
        AIActionFailureKind failureKind,
        int actionDefinitionInstanceId,
        int destinationInstanceId,
        int phaseCode)
    {
        Sequence = sequence;
        ActionEpoch = actionEpoch;
        GameTime = gameTime;
        Kind = kind;
        Branch = branch;
        FailureKind = failureKind;
        ActionDefinitionInstanceId = actionDefinitionInstanceId;
        DestinationInstanceId = destinationInstanceId;
        PhaseCode = phaseCode;
    }

    public long Sequence { get; }
    public long ActionEpoch { get; }
    public float GameTime { get; }
    public CharacterAiRuntimeTraceKind Kind { get; }
    public CharacterAiBranch Branch { get; }
    public AIActionFailureKind FailureKind { get; }
    public int ActionDefinitionInstanceId { get; }
    public int DestinationInstanceId { get; }
    public int PhaseCode { get; }

    public void AppendTo(StringBuilder builder)
    {
        builder.Append('#').Append(Sequence)
            .Append("@t=").Append(GameTime.ToString("0.###"))
            .Append(" epoch=").Append(ActionEpoch)
            .Append(" kind=").Append(Kind)
            .Append(" branch=").Append(Branch)
            .Append(" action=").Append(ActionDefinitionInstanceId)
            .Append(" destination=").Append(DestinationInstanceId)
            .Append(" phase=").Append(PhaseCode);
        if (FailureKind != AIActionFailureKind.None)
        {
            builder.Append(" failure=").Append(FailureKind);
        }
    }
}
