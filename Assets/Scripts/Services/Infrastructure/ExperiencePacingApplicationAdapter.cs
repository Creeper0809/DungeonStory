using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;

/// <summary>
/// Composes authored pacing rules, the Foundation event bus, and the shared
/// aggregate-root store around the named CoreSession aggregate.
/// </summary>
public sealed class ExperiencePacingApplicationAdapter
{
    private readonly IGameEventBus gameEventBus;
    private readonly CoreSessionRulesDefinition rules;
    private readonly IReadOnlyDictionary<int, int> rehearsalBitsByDay;

    public ExperiencePacingApplicationAdapter(
        IGameEventBus gameEventBus,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICoreSessionRulesProvider rulesProvider)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        DungeonRuntimeAggregateRootStore rootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        rules = (rulesProvider
                ?? throw new ArgumentNullException(nameof(rulesProvider)))
            .CoreSessionRules
            ?? throw new InvalidOperationException(
                "Core-session rules are not authored.");
        rehearsalBitsByDay = BuildRehearsalBitsByDay(rules);
        Aggregate = new ExperiencePacingAggregate(
            () => rootStore.GetOrCreate(
                () => new ExperiencePacingAggregateState()),
            rootStore.Replace);
    }

    public ExperiencePacingAggregate Aggregate { get; }
    public int RandomInvasionStartDay => rules.RandomInvasionStartDay;

    public IDisposable SubscribeToOperatingDayStarted(Action<int> handler)
    {
        if (handler == null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return gameEventBus.Subscribe<OperatingDayStartedEvent>(
            eventData => handler(eventData.day));
    }

    public int ResolveMaximumConcurrentExternalProblems(int day) =>
        ResolveExternalProblemBand(day).MaximumConcurrentProblems;

    public bool IsExteriorIncidentAllowed(
        int day,
        ExteriorIncidentKind kind) =>
        ResolveExternalProblemBand(day)
            .AllowedIncidentKinds.Contains((int)kind);

    public bool TryResolveRehearsal(
        int day,
        out int rehearsalBit,
        out RehearsalInvasionProfile profile)
    {
        rehearsalBit = ResolveRehearsalBit(day);
        profile = ResolveRehearsalProfile(day, rules);
        return rehearsalBit != 0;
    }

    public int ResolveRehearsalBit(int day) =>
        rehearsalBitsByDay.TryGetValue(day, out int bit) ? bit : 0;

    public ExperiencePacingAggregateState PrepareRestoreCandidate(
        DungeonExperiencePacingSaveData data)
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }
        if (data.introducedConcepts == null)
        {
            throw new InvalidOperationException(
                "Experience-pacing concept history is missing.");
        }

        return Aggregate.PrepareRestoreCandidate(
            data.currentDay,
            data.scheduledRehearsalMask,
            data.completedRehearsalMask,
            data.activeRehearsalDay,
            data.introducedConcepts.Select(
                raw => (ExperienceEventConcept)raw),
            rehearsalBitsByDay);
    }

    public void PublishRestoreCandidate(
        ExperiencePacingAggregateState candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        ExperiencePacingAggregateState validated =
            Aggregate.PrepareRestoreCandidate(
                candidate.CurrentDay,
                candidate.ScheduledRehearsalMask,
                candidate.CompletedRehearsalMask,
                candidate.ActiveRehearsalDay,
                candidate.IntroducedConcepts,
                rehearsalBitsByDay);
        Aggregate.PublishRestoreCandidate(validated);
    }

    public static RehearsalInvasionProfile ResolveRehearsalProfile(
        int day,
        CoreSessionRulesDefinition rules)
    {
        if (rules == null
            || !rules.TryGetRehearsal(day, out CoreRehearsalRule rule))
        {
            return default;
        }

        return new RehearsalInvasionProfile(
            rule.Day,
            rule.PowerMultiplier,
            rule.OwnerDamageMultiplier,
            rule.RetreatHealthRatio);
    }

    private static IReadOnlyDictionary<int, int> BuildRehearsalBitsByDay(
        CoreSessionRulesDefinition rules)
    {
        Dictionary<int, int> bitsByDay = new();
        if (rules.Rehearsals.Count > 30)
        {
            throw new InvalidOperationException(
                "Experience pacing supports at most 30 rehearsal milestones.");
        }

        for (int index = 0; index < rules.Rehearsals.Count; index++)
        {
            CoreRehearsalRule rule = rules.Rehearsals[index]
                ?? throw new InvalidOperationException(
                    $"Rehearsal rule {index} is null.");
            if (rule.Day < 1
                || !bitsByDay.TryAdd(rule.Day, 1 << index))
            {
                throw new InvalidOperationException(
                    $"Rehearsal day {rule.Day} is invalid or duplicated.");
            }
        }

        return bitsByDay;
    }

    private CoreExternalProblemBand ResolveExternalProblemBand(int day) =>
        rules.ExternalProblemBands.FirstOrDefault(band =>
            band != null && day <= band.LastDayInclusive)
        ?? throw new InvalidOperationException(
            $"No authored external-problem band covers day {day}.");
}
