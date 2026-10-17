using System;
using System.Globalization;
using System.Linq;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class CharacterRitualFastingOutcomeIds
{
    public const string Producer = "character.ritual-fasting";
    public const string Started = "started";
    public const string Completed = "completed";
    public const string Interrupted = "interrupted";
    public const string Manual = "manual-order";
    public const string Festival = "festival-eve";
    public const string Meal = "committed-direct-meal";
    public static readonly GameplayOutcomeTypeId Transitioned = new("character.ritual-fasting-transitioned");
    public static readonly GameplayEntityKindId Character = new("character");
    public static readonly GameplayRoleId Faster = new("ritual-faster");
    public static readonly GameplayOutcomeTagId Tag = new("ritual-fasting");
    public static readonly GameplayMetricUnitId Day = new("day");
    public static readonly GameplayMetricUnitId Mood = new("mood");
    public static readonly GameplayMetricId[] Metrics =
    {
        new("ritual-fast.start-day-before"), new("ritual-fast.start-day-after"),
        new("ritual-fast.mood-before"), new("ritual-fast.mood-after")
    };
    public static readonly GameplayOutcomeFactId[] Facts =
    {
        new("ritual-fast.transition"), new("ritual-fast.reason"),
        new("ritual-fast.trait"), new("ritual-fast.phase-before"),
        new("ritual-fast.phase-after"), new("ritual-fast.absolute-hour"),
        new("ritual-fast.meal-operation"), new("ritual-fast.meal-result")
    };

    public static GameplayResultKey ResultKey(string characterId, string traitId, long sequence) =>
        new(Producer, new GameplayOperationId("ritual-fast:"
            + NarrativeInferenceHash.ComputeSha256Utf8(characterId + "\n" + traitId)
            + ":" + sequence.ToString(CultureInfo.InvariantCulture)), sequence, 0);

    internal static bool ValidTransition(string kind, string reason,
        CharacterRitualFastPhase before, CharacterRitualFastPhase after,
        int day, int startBefore, int startAfter, bool hasMeal) =>
        kind == Started && before == CharacterRitualFastPhase.Inactive
            && after == CharacterRitualFastPhase.Fasting && startBefore == -1
            && startAfter == day && (reason == Manual || reason == Festival) && !hasMeal
        || kind == Completed && before == CharacterRitualFastPhase.Fasting
            && after == CharacterRitualFastPhase.AwaitingPostFastMeal
            && startBefore >= 0 && startBefore < day && startAfter == startBefore
            && reason == Manual && !hasMeal
        || kind == Interrupted && before == CharacterRitualFastPhase.Fasting
            && after == CharacterRitualFastPhase.Inactive
            && startBefore >= 0 && startBefore <= day && startAfter == -1
            && (reason == Manual && !hasMeal || reason == Meal && hasMeal);
}

public readonly struct CharacterRitualFastingOutcomeReceipt
{
    public CharacterRitualFastingOutcomeReceipt(string characterId, string displayName,
        string traitId, long sequence, string kind, string reason,
        CharacterRitualFastPhase before, CharacterRitualFastPhase after,
        int day, long hour, int startBefore, int startAfter, float moodBefore, float moodAfter,
        GameplayOutcomeId parent, string mealOperation = "", string mealResult = "")
    {
        if (sequence <= 0 || day < 0 || hour < 0
            || !GameplayOutcomeStableIdSyntax.IsValid(characterId)
            || !GameplayOutcomeStableIdSyntax.IsValid(traitId)
            || float.IsNaN(moodBefore) || float.IsInfinity(moodBefore)
            || float.IsNaN(moodAfter) || float.IsInfinity(moodAfter)
            || !CharacterRitualFastingOutcomeIds.ValidTransition(kind, reason, before, after,
                day, startBefore, startAfter, !string.IsNullOrEmpty(mealOperation))
            || (!string.IsNullOrEmpty(mealOperation) && (string.IsNullOrEmpty(mealResult) || !parent.IsValid))
            || (kind != CharacterRitualFastingOutcomeIds.Started && !parent.IsValid))
            throw new ArgumentException("Invalid ritual fasting transition receipt.");
        CharacterId = characterId;
        TraitId = traitId;
        Name = EvolutionOutcomeNames.Snapshot(characterId, displayName);
        Sequence = sequence;
        Kind = kind;
        Reason = reason;
        Before = before;
        After = after;
        Day = day;
        Hour = hour;
        StartBefore = startBefore;
        StartAfter = startAfter;
        MoodBefore = moodBefore;
        MoodAfter = moodAfter;
        Parent = parent;
        MealOperation = mealOperation;
        MealResult = mealResult;
    }
    public string CharacterId { get; }
    public string TraitId { get; }
    public KoreanNameSnapshot Name { get; }
    public long Sequence { get; }
    public string Kind { get; }
    public string Reason { get; }
    public CharacterRitualFastPhase Before { get; }
    public CharacterRitualFastPhase After { get; }
    public int Day { get; }
    public long Hour { get; }
    public int StartBefore { get; }
    public int StartAfter { get; }
    public float MoodBefore { get; }
    public float MoodAfter { get; }
    public GameplayOutcomeId Parent { get; }
    public string MealOperation { get; }
    public string MealResult { get; }
    public GameplayResultKey ResultKey => CharacterRitualFastingOutcomeIds.ResultKey(CharacterId, TraitId, Sequence);
}

public sealed class CharacterRitualFastingOutcomeAdapter : GameplayOutcomeAdapter<CharacterRitualFastingOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => CharacterRitualFastingOutcomeIds.Transitioned;
    public override OutcomePrepareResult TryGetRequirements(in CharacterRitualFastingOutcomeReceipt receipt,
        long currentWorldEpoch, out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(receipt.ResultKey, OutcomeTypeId, receipt.Day,
            GameplayOutcomeStatus.Succeeded, currentWorldEpoch, receipt.Sequence,
            participantCount: 1, metricCount: 4, subjectCount: 1, tagCount: 1,
            anchorCount: 0, provenanceCount: 2, factCount: 8);
        return receipt.Sequence > 0 && receipt.ResultKey.IsValid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(OutcomePrepareCode.InvalidReceipt, "ritual-fast-receipt-invalid");
    }
    public override OutcomePrepareResult TryWrite(in CharacterRitualFastingOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(CharacterRitualFastingOutcomeIds.Character, receipt.CharacterId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(character,
                CharacterRitualFastingOutcomeIds.Faster, GameplayParticipationKind.Direct, true, receipt.Name))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(character, .62f, NarrativeMemoryTier.Episodic, false, false, 0))
            && builder.AddTag(CharacterRitualFastingOutcomeIds.Tag)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference("runtime-receipt", "ritual-fast-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference("domain-commit", receipt.ResultKey.OperationId.Value));
        double[] metrics = { receipt.StartBefore, receipt.StartAfter, receipt.MoodBefore, receipt.MoodAfter };
        for (int index = 0; written && index < metrics.Length; index++)
            written = builder.AddMetric(new GameplayOutcomeMetric(CharacterRitualFastingOutcomeIds.Metrics[index],
                metrics[index], index < 2 ? CharacterRitualFastingOutcomeIds.Day : CharacterRitualFastingOutcomeIds.Mood, character));
        string[] facts = { receipt.Kind, receipt.Reason, receipt.TraitId, receipt.Before.ToString(),
            receipt.After.ToString(), receipt.Hour.ToString(CultureInfo.InvariantCulture),
            string.IsNullOrEmpty(receipt.MealOperation) ? "none" : receipt.MealOperation,
            string.IsNullOrEmpty(receipt.MealResult) ? "none" : receipt.MealResult };
        for (int index = 0; written && index < facts.Length; index++)
            written = builder.AddFact(new GameplayOutcomeFact(CharacterRitualFastingOutcomeIds.Facts[index], facts[index]));
        if (receipt.Parent.IsValid)
            written = written && builder.SetCausation(new GameplayOutcomeCausation(receipt.Parent, receipt.ResultKey.OperationId,
                receipt.Reason == CharacterRitualFastingOutcomeIds.Meal ? "direct-effect" : "process-transition"));
        return written ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(OutcomePrepareCode.AdapterWriteFailed, "ritual-fast-write-failed");
    }
}

public sealed class CharacterRitualFastingOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    public GameplayOutcomeTypeId OutcomeTypeId => CharacterRitualFastingOutcomeIds.Transitioned;
    public INarrativePerspectiveProjector PerspectiveProjector { get; } = new CharacterRitualFastingPerspectiveProjector();
    public IOutcomeMemoryPolicy MemoryPolicy { get; } = new EvolutionOutcomeMemoryPolicy();
    public IOutcomePerceptionPolicy PerceptionPolicy { get; } = new EvolutionOutcomePerceptionPolicy();
    public IOutcomeMemoryConsolidator MemoryConsolidator { get; } = new EvolutionOutcomeConsolidator(false);
    public bool IsKnownRole(GameplayRoleId roleId) => roleId.Equals(CharacterRitualFastingOutcomeIds.Faster);
    public bool IsKnownMetric(GameplayMetricId metricId, GameplayMetricUnitId unitId) =>
        CharacterRitualFastingOutcomeIds.Metrics.Take(2).Contains(metricId) && unitId.Equals(CharacterRitualFastingOutcomeIds.Day)
        || CharacterRitualFastingOutcomeIds.Metrics.Skip(2).Contains(metricId) && unitId.Equals(CharacterRitualFastingOutcomeIds.Mood);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId || outcome.Status != GameplayOutcomeStatus.Succeeded
            || outcome.OwnerRevision <= 0 || outcome.ParticipantCount != 1 || outcome.SubjectCount != 1
            || outcome.MetricCount != 4 || outcome.FactCount != 8 || outcome.TagCount != 1
            || outcome.AnchorCount != 0 || outcome.ProvenanceCount != 2)
            return OutcomeValidationResult.Reject("ritual-fast-shape-invalid");
        GameplayOutcomeParticipant actor = outcome.GetParticipant(0);
        if (!actor.EntityId.Kind.Equals(CharacterRitualFastingOutcomeIds.Character)
            || !IsKnownRole(actor.RoleId) || outcome.GetSubject(0).SubjectId != actor.EntityId
            || !outcome.GetTag(0).Equals(CharacterRitualFastingOutcomeIds.Tag))
            return OutcomeValidationResult.Reject("ritual-fast-actor-invalid");
        for (int index = 0; index < 4; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (!metric.MetricId.Equals(CharacterRitualFastingOutcomeIds.Metrics[index])
                || !IsKnownMetric(metric.MetricId, metric.UnitId) || metric.DefinitionOrInstanceId != actor.EntityId
                || double.IsNaN(metric.Value) || double.IsInfinity(metric.Value)
                || index < 2 && (metric.Value < -1 || metric.Value != Math.Truncate(metric.Value)))
                return OutcomeValidationResult.Reject("ritual-fast-metric-invalid");
        }
        for (int index = 0; index < 8; index++)
            if (!outcome.GetFact(index).FactId.Equals(CharacterRitualFastingOutcomeIds.Facts[index]))
                return OutcomeValidationResult.Reject("ritual-fast-fact-order-invalid");
        bool meal = outcome.GetFact(6).Value != "none";
        if (!Enum.TryParse(outcome.GetFact(3).Value, out CharacterRitualFastPhase before)
            || !Enum.TryParse(outcome.GetFact(4).Value, out CharacterRitualFastPhase after)
            || !GameplayOutcomeStableIdSyntax.IsValid(outcome.GetFact(2).Value)
            || !long.TryParse(outcome.GetFact(5).Value, NumberStyles.None, CultureInfo.InvariantCulture, out long hour) || hour < 0
            || !CharacterRitualFastingOutcomeIds.ValidTransition(outcome.GetFact(0).Value, outcome.GetFact(1).Value,
                before, after, outcome.AbsoluteDay, (int)outcome.GetMetric(0).Value, (int)outcome.GetMetric(1).Value, meal)
            || meal != (outcome.GetFact(7).Value != "none")
            || outcome.ResultKey != CharacterRitualFastingOutcomeIds.ResultKey(actor.EntityId.Value,
                outcome.GetFact(2).Value, outcome.OwnerRevision)
            || outcome.GetFact(0).Value != CharacterRitualFastingOutcomeIds.Started && !outcome.Causation.HasParent)
            return OutcomeValidationResult.Reject("ritual-fast-semantics-invalid");
        return OutcomeValidationResult.Accepted;
    }
}

internal sealed class CharacterRitualFastingPerspectiveProjector : INarrativePerspectiveProjector
{
    public NarrativeView Project(in GameplayOutcomeReadView outcome, NarrativePerspectiveContext perspective)
    {
        string verb = outcome.GetFact(0).Value switch
        {
            CharacterRitualFastingOutcomeIds.Started => "의식 단식을 시작했다.",
            CharacterRitualFastingOutcomeIds.Completed => "의식 단식을 완수했다.",
            _ => outcome.GetFact(1).Value == CharacterRitualFastingOutcomeIds.Meal
                ? "직접 지시된 식사를 먹고 의식 단식을 중단했다." : "의식 단식을 중단했다."
        };
        return new NarrativeView(outcome.OutcomeId, perspective.Kind,
            outcome.GetParticipant(0).DisplayName.DisplayText + ": " + verb, "ritual-fast-v1", true);
    }
}

public static class CharacterRitualFastingOutcomeRegistration
{
    public static void RegisterCharacterRitualFastingGameplayOutcomes(this IContainerBuilder builder)
    {
        builder.RegisterGameplayActionLogs(nameof(CharacterRitualFastingOutcomeAdapter));
        builder.Register<CharacterRitualFastingFactMapping>(Lifetime.Singleton);
        builder.Register(resolver => new GameplayDomainFactObserverBinding<CharacterRitualFastTransitionFact,
            CharacterRitualFastingOutcomeReceipt>("log:character.ritual-fasting",
            resolver.Resolve<CharacterRitualFastingFactMapping>().Capture,
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.RegisterDeferredDomainFactBinding<CharacterRitualFastMealObservedFact>((resolver, dispatcher) =>
            dispatcher.Subscribe("observe:ritual-fasting.meal",
                CharacterRitualFastingAuxiliaryObservation.Meals(resolver.Resolve<CharacterRitualFastingFactMapping>())));
        builder.RegisterDeferredDomainFactBinding<CharacterRitualFastMoodCommittedFact>((_, dispatcher) =>
            dispatcher.Subscribe("notify:ritual-fasting.mood", CharacterRitualFastingAuxiliaryObservation.Moods()));
    }
}
