using System;
using DungeonStory.Foundation.Facts;
using DungeonStory.Characters;

public readonly struct CharacterTraitReactionFact
{
    private readonly CharacterTraitReactionFactData data;
    public CharacterTraitReactionFact(CharacterTraitReactionFactData data)
    {
        (data ?? throw new ArgumentNullException(nameof(data))).Validate();
        this.data = data.Clone();
    }
    public CharacterTraitReactionFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("trait-reaction-fact-uninitialized")).Clone();
}

[Serializable]
public sealed class CharacterTraitReactionFactData
{
    public string characterId = string.Empty;
    public string characterDisplayName = string.Empty;
    public string traitInstanceId = string.Empty;
    public string reactionId = string.Empty;
    public int beforeRevision, afterRevision, absoluteDay;
    public CharacterAcquiredTraitReactionAction action;
    public float requestedValue, appliedValue, moodBefore, moodAfter;
    public int beforeLevel, beforeExperience, afterLevel, afterExperience, durationDays;
    public int beforeTriggerDay, beforeDailyCount, beforeTotalCount, afterDailyCount, afterTotalCount;

    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(characterId) || !DomainFactValueSyntax.IsIdentity(traitInstanceId)
            || !DomainFactValueSyntax.IsIdentity(reactionId)
            || !DomainFactValueSyntax.IsText(characterDisplayName, 4096, false)
            || beforeRevision < 0 || beforeRevision == int.MaxValue || afterRevision != beforeRevision + 1
            || absoluteDay < 0 || beforeTriggerDay < -1 || beforeTriggerDay > absoluteDay
            || beforeDailyCount < 0 || beforeTotalCount < beforeDailyCount || beforeTotalCount == int.MaxValue
            || afterTotalCount != beforeTotalCount + 1
            || afterDailyCount != (beforeTriggerDay == absoluteDay ? (long)beforeDailyCount + 1 : 1)
            || beforeLevel < 1 || beforeLevel > CharacterProgressionRules.MaxLevel || afterLevel < beforeLevel
            || afterLevel > CharacterProgressionRules.MaxLevel || beforeExperience < 0 || afterExperience < 0
            || (beforeLevel == CharacterProgressionRules.MaxLevel ? beforeExperience != 0
                : beforeExperience >= CharacterProgressionRules.GetExperienceRequired(beforeLevel))
            || (afterLevel == CharacterProgressionRules.MaxLevel ? afterExperience != 0
                : afterExperience >= CharacterProgressionRules.GetExperienceRequired(afterLevel))
            || beforeTriggerDay == -1 && (beforeDailyCount != 0 || beforeTotalCount != 0)
            || durationDays < 0 || !float.IsFinite(requestedValue) || !float.IsFinite(appliedValue)
            || !float.IsFinite(moodBefore) || !float.IsFinite(moodAfter)
            || moodBefore < 0 || moodBefore > 100 || moodAfter < 0 || moodAfter > 100
            || action is not (CharacterAcquiredTraitReactionAction.GrantExperience or CharacterAcquiredTraitReactionAction.ApplyMoodImpulse))
            throw new ArgumentException("trait-reaction-fact-invalid");
        if (action == CharacterAcquiredTraitReactionAction.GrantExperience)
        {
            if (requestedValue < 1 || requestedValue >= int.MaxValue || requestedValue != Math.Floor(requestedValue)
                || requestedValue > int.MaxValue - beforeExperience
                || appliedValue < 1 || durationDays != 0 || moodBefore != moodAfter)
                throw new ArgumentException("trait-reaction-experience-invalid");
            var expected = DungeonStory.Characters.CharacterProgressionRules.AddExperience(beforeLevel, beforeExperience, (int)requestedValue);
            int applied = expected.CurrentExperience - beforeExperience;
            for (int level = beforeLevel; level < expected.Level; level++)
                applied = checked(applied + DungeonStory.Characters.CharacterProgressionRules.GetExperienceRequired(level));
            if (afterLevel != expected.Level || afterExperience != expected.CurrentExperience || appliedValue != applied)
                throw new ArgumentException("trait-reaction-experience-transition-conflict");
        }
        else if (durationDays <= 0 || beforeLevel != afterLevel || beforeExperience != afterExperience)
            throw new ArgumentException("trait-reaction-mood-transition-conflict");
    }

    public CharacterTraitReactionFactData Clone()
    { Validate(); return (CharacterTraitReactionFactData)MemberwiseClone(); }
}
