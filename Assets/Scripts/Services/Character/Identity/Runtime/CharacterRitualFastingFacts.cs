using System;
using System.Linq;

public enum CharacterRitualFastTransition { Started, Completed, Interrupted }
public enum CharacterRitualFastCause { ManualOrder, FestivalEve, DirectMeal }

// Frozen gameplay evidence. Publication identities are opaque acknowledgements,
// never parsed or constructed by the fasting authority.
[Serializable]
public sealed class CharacterRitualFastFactData
{
    public string characterId = string.Empty;
    public string characterName = string.Empty;
    public string traitId = string.Empty;
    public long sequence;
    public CharacterRitualFastTransition transition;
    public CharacterRitualFastCause cause;
    public CharacterRitualFastPhase before;
    public CharacterRitualFastPhase after;
    public int day;
    public long hour;
    public int startBefore = -1;
    public int startAfter = -1;
    public float moodBefore;
    public float moodAfter;
    public float resolvedMood;
    public string moodEvent = string.Empty;
    public string cycleStartPublicationKey = string.Empty;
    public string mealOperation = string.Empty;
    public string mealPublicationKey = string.Empty;
    public CharacterMealConsumedFactData mealEvidence = new();
    public CharacterRitualFastFactData Clone()
    {
        var copy = (CharacterRitualFastFactData)MemberwiseClone();
        copy.mealEvidence = mealEvidence?.Clone();
        return copy;
    }
}

public sealed class CharacterRitualFastTransitionFact
{
    private readonly CharacterRitualFastFactData data;
    public CharacterRitualFastTransitionFact(CharacterRitualFastFactData source)
    {
        if (source == null || !Canonical(source.characterId) || !Canonical(source.traitId)
            || string.IsNullOrWhiteSpace(source.characterName) || source.sequence <= 0 || source.day < 0 || source.hour < 0
            || !Enum.IsDefined(typeof(CharacterRitualFastTransition), source.transition)
            || !Enum.IsDefined(typeof(CharacterRitualFastCause), source.cause)
            || !Enum.IsDefined(typeof(CharacterRitualFastPhase), source.before)
            || !Enum.IsDefined(typeof(CharacterRitualFastPhase), source.after)
            || !float.IsFinite(source.moodBefore) || !float.IsFinite(source.moodAfter)
            || !float.IsFinite(source.resolvedMood) || source.moodEvent == null
            || source.cycleStartPublicationKey == null || source.mealOperation == null || source.mealPublicationKey == null)
            throw new ArgumentException("Ritual fasting fact has incomplete frozen evidence.");
        bool meal = source.cause == CharacterRitualFastCause.DirectMeal;
        if (source.mealEvidence == null || meal != !string.IsNullOrEmpty(source.mealEvidence.operationId))
            throw new ArgumentException("Ritual fasting meal evidence is missing or unexpected.");
        if (meal)
        {
            var consumed = source.mealEvidence.ToFact();
            if (consumed.CharacterId.Value != source.characterId || consumed.OperationId.Value != source.mealOperation
                || !consumed.RecipientActiveAtCompletion || consumed.Evidence.EffectsSkippedReason.Length > 0)
                throw new ArgumentException("Ritual fasting consequence belongs to a different meal recipient.");
        }
        if (meal != (source.mealOperation.Length > 0) || meal != (source.mealPublicationKey.Length > 0)
            || source.transition != CharacterRitualFastTransition.Started && source.cycleStartPublicationKey.Length == 0
            || !(source.transition switch
            {
                CharacterRitualFastTransition.Started => source.before == CharacterRitualFastPhase.Inactive
                    && source.after == CharacterRitualFastPhase.Fasting && source.startBefore == -1
                    && source.startAfter == source.day && !meal,
                CharacterRitualFastTransition.Completed => source.before == CharacterRitualFastPhase.Fasting
                    && source.after == CharacterRitualFastPhase.AwaitingPostFastMeal && source.startBefore >= 0
                    && source.startBefore < source.day && source.startAfter == source.startBefore
                    && source.cause == CharacterRitualFastCause.ManualOrder,
                CharacterRitualFastTransition.Interrupted => source.before == CharacterRitualFastPhase.Fasting
                    && source.after == CharacterRitualFastPhase.Inactive && source.startBefore >= 0
                    && source.startBefore <= source.day && source.startAfter == -1
                    && source.cause != CharacterRitualFastCause.FestivalEve,
                _ => false
            }))
            throw new ArgumentException("Ritual fasting fact has an invalid state transition.");
        data = source.Clone();
    }
    public CharacterRitualFastFactData Snapshot() => data.Clone();
    private static bool Canonical(string value) => !string.IsNullOrWhiteSpace(value)
        && value == value.Trim() && !value.Any(char.IsControl);
}

[Serializable]
public sealed class CharacterRitualFastPendingFact
{
    public bool active;
    public CharacterRitualFastFactData fact = new();
    public string publicationKey = string.Empty;
    public string publicationId = string.Empty;
    public string fingerprint = string.Empty;
    public bool retainedForDeath;
}

public readonly struct CharacterRitualFastMealObservedFact
{
    public CharacterRitualFastMealObservedFact(string characterId, CharacterMealConsumedFact meal, string publicationKey)
    { CharacterId = characterId; Meal = meal; PublicationKey = publicationKey; }
    public string CharacterId { get; }
    public CharacterMealConsumedFact Meal { get; }
    public string OperationId => Meal?.OperationId.Value;
    public string PublicationKey { get; }
}

// A post-commit gameplay notification, not a second log or another mood effect.
public readonly struct CharacterRitualFastMoodCommittedFact
{
    public CharacterRitualFastMoodCommittedFact(CharacterActor actor, CharacterRitualFastTransitionFact transition,
        string publicationKey, string cycleStartPublicationKey)
    { Actor = actor; Transition = transition; PublicationKey = publicationKey; CycleStartPublicationKey = cycleStartPublicationKey; }
    public CharacterActor Actor { get; }
    public CharacterRitualFastTransitionFact Transition { get; }
    public string PublicationKey { get; }
    public string CycleStartPublicationKey { get; }
}
