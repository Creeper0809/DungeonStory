using System;
using DungeonStory.Foundation.Facts;

public enum CharacterVitalTransition
{
    Damaged,
    Healed,
    InjurySeverityChanged,
    Died
}

/// <summary>An applied vital change, without activity or narrative policy.</summary>
public readonly struct CharacterVitalsChangedEvent
{
    public CharacterVitalsChangedEvent(
        CharacterId character,
        CharacterVitalTransition transition,
        float amount,
        string reason = "",
        bool wasFatal = false,
        CharacterDeathCauseCode deathCause = CharacterDeathCauseCode.Unknown)
    {
        reason ??= string.Empty;
        if (!character.IsValid || !Enum.IsDefined(typeof(CharacterVitalTransition), transition)
            || !Enum.IsDefined(typeof(CharacterDeathCauseCode), deathCause)
            || !float.IsFinite(amount) || amount < 0f
            || transition == CharacterVitalTransition.InjurySeverityChanged && amount > 1f
            || !DomainFactValueSyntax.IsText(reason, 16384, true))
            throw new ArgumentException("character-vital-change-invalid");
        Character = character;
        Transition = transition;
        Amount = amount;
        Reason = reason;
        WasFatal = wasFatal;
        DeathCause = deathCause;
    }

    public CharacterId Character { get; }
    public CharacterVitalTransition Transition { get; }
    public float Amount { get; }
    public string Reason { get; }
    public bool WasFatal { get; }
    public CharacterDeathCauseCode DeathCause { get; }
}
