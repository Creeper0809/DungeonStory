using System;

public readonly struct CharacterConditionChangedEvent
{
    private readonly CharacterEventContext context;
    public CharacterConditionChangedEvent(CharacterId character, CharacterCondition condition,
        float before, float after, int day = 0, CharacterEventContext context = null)
    {
        if (!character.IsValid || !Enum.IsDefined(typeof(CharacterCondition), condition)
            || !float.IsFinite(before) || !float.IsFinite(after) || day < 0)
            throw new ArgumentException("character-condition-event-invalid");
        context?.Validate();
        Character = character; Condition = condition; Before = before; After = after; Day = day;
        this.context = context?.Clone();
    }
    public CharacterId Character { get; }
    public CharacterCondition Condition { get; }
    public float Before { get; }
    public float After { get; }
    public int Day { get; }
    public CharacterEventContext Context => context?.Clone();
}

public readonly struct CharacterMoodFactorAppliedEvent
{
    private readonly CharacterEventContext context;
    public CharacterMoodFactorAppliedEvent(CharacterId character, string factorId, float value,
        int day = 0, CharacterEventContext context = null)
    {
        if (!character.IsValid || string.IsNullOrWhiteSpace(factorId)
            || !float.IsFinite(value) || day < 0)
            throw new ArgumentException("character-mood-event-invalid");
        context?.Validate();
        Character = character; FactorId = factorId; Value = value; Day = day;
        this.context = context?.Clone();
    }
    public CharacterId Character { get; }
    public string FactorId { get; }
    public float Value { get; }
    public int Day { get; }
    public CharacterEventContext Context => context?.Clone();
}
