using System;
using System.Globalization;
using DungeonStory.Foundation.Facts;

public sealed class CharacterRitualFastingFactMapping
{
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly MigratedProducerOutcomeRuntime migrated;
    private readonly GameplayDomainFactCommitGroup commits;
    public CharacterRitualFastingFactMapping(IGameplayOutcomeDiagnosticsQuery diagnostics,
        MigratedProducerOutcomeRuntime migrated, GameplayDomainFactCommitGroup commits)
    {
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        this.migrated = migrated ?? throw new ArgumentNullException(nameof(migrated));
        this.commits = commits ?? throw new ArgumentNullException(nameof(commits));
    }

    public CharacterRitualFastingOutcomeReceipt Capture(CharacterRitualFastTransitionFact fact)
    {
        var data = fact.Snapshot();
        GameplayOutcomeId parent = default;
        if (data.transition != CharacterRitualFastTransition.Started)
        {
            // Both the saved cycle and, when present, the physical meal must be
            // committed. The producer supplies references, never ledger queries.
            var start = RequireCommitted(data.cycleStartPublicationKey);
            if (start.ResultKey != CharacterRitualFastingOutcomeIds.ResultKey(data.characterId, data.traitId,
                    start.ResultKey.CommitRevision) || start.ResultKey.CommitRevision != data.sequence - 1)
                throw new InvalidOperationException("ritual-fast-start-outcome-missing");
            parent = start.OutcomeId;
        }
        if (data.cause == CharacterRitualFastCause.DirectMeal)
            parent = RequireMeal(new(data.characterId, data.mealEvidence.ToFact(), data.mealPublicationKey)).OutcomeId;
        return new CharacterRitualFastingOutcomeReceipt(data.characterId, data.characterName, data.traitId,
            data.sequence, Kind(data.transition), Cause(data.cause), data.before, data.after, data.day, data.hour,
            data.startBefore, data.startAfter, data.moodBefore, data.moodAfter, parent,
            data.mealOperation, data.mealPublicationKey);
    }

    public GameplayOutcomeReplayIdentity RequireMeal(CharacterRitualFastMealObservedFact fact)
    {
        if (string.IsNullOrWhiteSpace(fact.CharacterId) || string.IsNullOrWhiteSpace(fact.OperationId)
            || fact.Meal.CharacterId.Value != fact.CharacterId || !fact.Meal.RecipientActiveAtCompletion
            || fact.Meal.Evidence.EffectsSkippedReason.Length > 0)
            throw new InvalidOperationException("ritual-fast-committed-meal-identity-missing");
        var identity = RequireCommitted(fact.PublicationKey);
        if (identity.ResultKey != MigratedProducerOutcomeCatalog.ResultKey(
                MigratedProducerOutcomeKind.CharacterConsumablesMealResult,
                "character-consumables-meal@1|" + fact.OperationId, identity.ResultKey.CommitRevision))
            throw new InvalidOperationException("ritual-fast-committed-meal-identity-missing");
        // Compare the complete frozen gameplay evidence, including recipient,
        // against the canonical hash. This also works for compacted/tombstoned
        // meals and never creates or commits another meal result.
        GameplayDomainFactPublication comparison = null;
        try
        {
            if (!migrated.TryPrepareObservedRecovery(CharacterConsumptionFactMapping.Capture(fact.Meal),
                    fact.PublicationKey, commits, out comparison, out string failure) || !comparison.IsReplay)
                throw new InvalidOperationException("ritual-fast-meal-evidence-mismatch:" + failure);
        }
        finally { comparison?.Cancel(); }
        return identity;
    }

    private GameplayOutcomeReplayIdentity RequireCommitted(string serialized)
    {
        string[] parts = (serialized ?? string.Empty).Split('|');
        if (parts.Length != 4 || !GameplayOutcomeStableIdSyntax.IsValid(parts[0])
            || !GameplayOutcomeStableIdSyntax.IsValid(parts[1])
            || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long revision)
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out int index))
            throw new InvalidOperationException("ritual-fast-publication-reference-invalid");
        var key = new GameplayResultKey(parts[0], new GameplayOperationId(parts[1]), revision, index);
        if (key.ToString() != serialized || !diagnostics.TryGetResultIdentity(key, out var identity)
            || identity.ResultKey != key || !identity.OutcomeId.IsValid || identity.State < GameplayOutcomeReplayState.Committed)
            throw new InvalidOperationException("ritual-fast-publication-not-committed");
        return identity;
    }

    internal static string Kind(CharacterRitualFastTransition value) => value switch
    {
        CharacterRitualFastTransition.Started => CharacterRitualFastingOutcomeIds.Started,
        CharacterRitualFastTransition.Completed => CharacterRitualFastingOutcomeIds.Completed,
        CharacterRitualFastTransition.Interrupted => CharacterRitualFastingOutcomeIds.Interrupted,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    internal static string Cause(CharacterRitualFastCause value) => value switch
    {
        CharacterRitualFastCause.ManualOrder => CharacterRitualFastingOutcomeIds.Manual,
        CharacterRitualFastCause.FestivalEve => CharacterRitualFastingOutcomeIds.Festival,
        CharacterRitualFastCause.DirectMeal => CharacterRitualFastingOutcomeIds.Meal,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}

public static class CharacterRitualFastingAuxiliaryObservation
{
    // These observations add no ledger rows. The owner closes pending delivery
    // before notification, so callback re-entry cannot repeat the notification.
    public static GameplayPostCommitFactObserver<CharacterRitualFastMealObservedFact> Meals(
        CharacterRitualFastingFactMapping mapping) => new(fact =>
        {
            mapping.RequireMeal(fact);
            return "ritual-fast-meal-observed:" + NarrativeInferenceHash.ComputeSha256Utf8(
                fact.CharacterId + "\n" + fact.OperationId + "\n" + fact.PublicationKey);
        }, null);

    public static GameplayPostCommitFactObserver<CharacterRitualFastMoodCommittedFact> Moods() => new(fact =>
        {
            var data = fact.Transition?.Snapshot() ?? throw new ArgumentException("Missing fasting mood transition.");
            string expected = CharacterRitualFastingOutcomeIds.ResultKey(data.characterId, data.traitId, data.sequence).ToString();
            string start = data.transition == CharacterRitualFastTransition.Started ? expected : data.cycleStartPublicationKey;
            if (fact.Actor?.Identity?.PersistentId != data.characterId || fact.PublicationKey != expected
                || fact.CycleStartPublicationKey != start)
                throw new ArgumentException("Fasting mood notification has no matching owner/publication.");
            return "ritual-fast-mood:" + NarrativeInferenceHash.ComputeSha256Utf8(fact.PublicationKey + "\n"
                + fact.CycleStartPublicationKey + "\n" + UnityEngine.JsonUtility.ToJson(data));
        }, fact =>
        {
            var data = fact.Transition.Snapshot();
            fact.Actor.Stats.PublishCommittedMoodFactor("identity:" + data.moodEvent, data.resolvedMood, data.day,
                new CharacterEventContext { eventInstanceId = fact.PublicationKey,
                    chainId = fact.CycleStartPublicationKey, actorId = data.characterId,
                    actorDisplayName = data.characterName,
                    resultDetail = CharacterRitualFastingFactMapping.Kind(data.transition) + ":"
                        + CharacterRitualFastingFactMapping.Cause(data.cause), occurredDay = data.day, sequence = data.sequence });
        });
}
