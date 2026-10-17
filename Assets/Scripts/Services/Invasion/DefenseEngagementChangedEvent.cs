using System;
using DungeonStory.Foundation.Facts;

public enum DefenseEngagementTransition
{
    Dispatched,
    Started,
    Completed
}

/// <summary>
/// A frozen notification of a defense engagement lifecycle transition.
/// Combat state, guard control, and persistence remain with the producer.
/// </summary>
public readonly struct DefenseEngagementChangedEvent
{
    public DefenseEngagementChangedEvent(
        DefenseEngagementTransition transition,
        CharacterId subject,
        string targetId = "",
        string targetName = "",
        string intruderDisplayLabel = "",
        string dispatchDetail = "",
        int exchangeCount = 0)
    {
        Transition = transition;
        Subject = subject;
        TargetId = targetId ?? string.Empty;
        TargetName = targetName ?? string.Empty;
        IntruderDisplayLabel = intruderDisplayLabel ?? string.Empty;
        DispatchDetail = dispatchDetail ?? string.Empty;
        ExchangeCount = exchangeCount;
        Validate();
    }

    public DefenseEngagementTransition Transition { get; }
    public CharacterId Subject { get; }
    public string TargetId { get; }
    public string TargetName { get; }
    public string IntruderDisplayLabel { get; }
    public string DispatchDetail { get; }
    public int ExchangeCount { get; }

    internal void Validate()
    {
        if (!Enum.IsDefined(typeof(DefenseEngagementTransition), Transition)
            || !Subject.IsValid
            || !Text(TargetId)
            || !Text(TargetName)
            || !Text(IntruderDisplayLabel)
            || !Text(DispatchDetail)
            || ExchangeCount < 0)
        {
            throw Invalid();
        }

        switch (Transition)
        {
            case DefenseEngagementTransition.Dispatched:
                if (string.IsNullOrWhiteSpace(DispatchDetail))
                    throw Invalid();
                break;
            case DefenseEngagementTransition.Started:
            case DefenseEngagementTransition.Completed:
                if (string.IsNullOrWhiteSpace(TargetName)
                    || string.IsNullOrWhiteSpace(IntruderDisplayLabel))
                {
                    throw Invalid();
                }
                break;
            default:
                throw Invalid();
        }
    }

    private static bool Text(string value) =>
        DomainFactValueSyntax.IsText(value, 16384, allowEmpty: true);

    private static ArgumentException Invalid() =>
        new("defense-engagement-event-invalid");
}
