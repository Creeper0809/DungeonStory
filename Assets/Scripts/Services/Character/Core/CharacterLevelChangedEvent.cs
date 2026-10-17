using System;
using DungeonStory.Characters;

public enum CharacterLevelChangeCause
{
    Experience,
    MinimumLevelRequirement,
    CampaignRecruitmentTraining,
    DebugAdjustment
}

public readonly struct CharacterLevelChangedEvent
{
    public CharacterLevelChangedEvent(
        CharacterId character,
        int previousLevel,
        int level,
        CharacterLevelChangeCause cause)
    {
        if (!character.IsValid)
            throw new ArgumentException(
                "A valid character identity is required.",
                nameof(character));
        if (previousLevel < 1
            || previousLevel >= level
            || level > CharacterProgressionRules.MaxLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(level));
        }
        if (!Enum.IsDefined(typeof(CharacterLevelChangeCause), cause))
            throw new ArgumentOutOfRangeException(nameof(cause));

        Character = character;
        PreviousLevel = previousLevel;
        Level = level;
        Cause = cause;
    }

    public CharacterId Character { get; }
    public int PreviousLevel { get; }
    public int Level { get; }
    public CharacterLevelChangeCause Cause { get; }
}
