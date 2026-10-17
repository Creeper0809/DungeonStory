using System;
using System.Collections.Generic;
using System.Linq;

public enum CommittedRunChoiceKind
{
    None = 0,
    SocietyEventChoice = 1,
    FactionChapterChoice = 2
}

public readonly struct CommittedRunChoiceSnapshot
{
    public CommittedRunChoiceSnapshot(
        CommittedRunChoiceKind kind,
        string ownerId,
        string definitionId,
        string instanceId,
        string choiceId,
        string operationId,
        long ordinal)
    {
        Kind = kind;
        OwnerId = ownerId ?? string.Empty;
        DefinitionId = definitionId ?? string.Empty;
        InstanceId = instanceId ?? string.Empty;
        ChoiceId = choiceId ?? string.Empty;
        OperationId = operationId ?? string.Empty;
        Ordinal = ordinal;
    }

    public CommittedRunChoiceKind Kind { get; }
    public string OwnerId { get; }
    public string DefinitionId { get; }
    public string InstanceId { get; }
    public string ChoiceId { get; }
    public string OperationId { get; }
    public long Ordinal { get; }
}

public readonly struct CommittedRunResultSnapshot
{
    public CommittedRunResultSnapshot(
        IEnumerable<string> completedMilestoneIds,
        IEnumerable<CommittedRunChoiceSnapshot> committedChoices)
    {
        CompletedMilestoneIds = Array.AsReadOnly(
            (completedMilestoneIds ?? Array.Empty<string>()).ToArray());
        CommittedChoices = Array.AsReadOnly(
            (committedChoices ?? Array.Empty<CommittedRunChoiceSnapshot>())
            .ToArray());
    }

    public IReadOnlyList<string> CompletedMilestoneIds { get; }
    public IReadOnlyList<CommittedRunChoiceSnapshot> CommittedChoices { get; }
}

public interface ICommittedRunResultQuery
{
    CommittedRunResultSnapshot CaptureCommittedRunResult();
}
