using System;

public enum CharacterMacroActionTransition
{
    ComplaintExpressed = 1,
    DepartureSelected = 2,
    FacilityVandalized = 3
}

public readonly struct CharacterMacroActionTransitionEvent
{
    public CharacterMacroActionTransitionEvent(CharacterActor actor,
        CharacterMacroActionTransition transition, string reason, BuildableObject facility = null)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        if (!Enum.IsDefined(typeof(CharacterMacroActionTransition), transition))
            throw new ArgumentOutOfRangeException(nameof(transition));
        Transition = transition;
        Reason = reason ?? string.Empty;
        Facility = facility;
    }
    public CharacterActor Actor { get; }
    public CharacterMacroActionTransition Transition { get; }
    public string Reason { get; }
    public BuildableObject Facility { get; }
}

public readonly struct CharacterExpeditionPresenceChangedEvent
{
    public CharacterExpeditionPresenceChangedEvent(CharacterActor actor, bool onExpedition)
    { Actor = actor ?? throw new ArgumentNullException(nameof(actor)); OnExpedition = onExpedition; }
    public CharacterActor Actor { get; }
    public bool OnExpedition { get; }
}

public readonly struct CharacterInterruptedWorkAbandonedEvent
{
    public CharacterInterruptedWorkAbandonedEvent(CharacterActor actor, WorkTypeId workType,
        BuildableObject target, string reasonCode, string restoreFailure)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        WorkType = workType;
        Target = target;
        ReasonCode = reasonCode ?? string.Empty;
        RestoreFailure = restoreFailure ?? string.Empty;
    }
    public CharacterActor Actor { get; }
    public WorkTypeId WorkType { get; }
    public BuildableObject Target { get; }
    public string ReasonCode { get; }
    public string RestoreFailure { get; }
}

public readonly struct CareerMentorshipCompletedEvent
{
    public CareerMentorshipCompletedEvent(
        CharacterActor actor,
        string counterpartName,
        string proficiencyId,
        bool isMentor)
    {
        Actor = actor ?? throw new ArgumentNullException(nameof(actor));
        CounterpartName = counterpartName ?? string.Empty;
        ProficiencyId = proficiencyId ?? string.Empty;
        IsMentor = isMentor;
    }

    public CharacterActor Actor { get; }
    public string CounterpartName { get; }
    public string ProficiencyId { get; }
    public bool IsMentor { get; }
}
