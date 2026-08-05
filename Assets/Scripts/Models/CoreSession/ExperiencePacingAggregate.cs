using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ExperiencePacingAggregate
{
    private readonly Func<ExperiencePacingAggregateState> getState;
    private readonly Action<ExperiencePacingAggregateState> replaceState;

    public ExperiencePacingAggregate(
        Func<ExperiencePacingAggregateState> getState,
        Action<ExperiencePacingAggregateState> replaceState)
    {
        this.getState = getState
            ?? throw new ArgumentNullException(nameof(getState));
        this.replaceState = replaceState
            ?? throw new ArgumentNullException(nameof(replaceState));
    }

    private ExperiencePacingAggregateState State =>
        getState()
        ?? throw new InvalidOperationException(
            "Experience-pacing state authority returned null.");

    public int CurrentDay => State.CurrentDay;
    public int ScheduledRehearsalMask => State.ScheduledRehearsalMask;
    public int CompletedRehearsalMask => State.CompletedRehearsalMask;
    public int ActiveRehearsalDay => State.ActiveRehearsalDay;
    public bool IsRehearsalActive => ActiveRehearsalDay > 0;

    public IReadOnlyList<ExperienceEventConcept> IntroducedConcepts =>
        State.IntroducedConcepts
            .OrderBy(value => value)
            .ToArray();

    public bool AdvanceToDay(int day)
    {
        int normalized = Math.Max(1, day);
        if (normalized <= State.CurrentDay)
        {
            return false;
        }

        State.CurrentDay = normalized;
        return true;
    }

    public bool TryBeginRehearsal(int day, int rehearsalBit)
    {
        ExperiencePacingAggregateState state = State;
        if (day < 1
            || day > state.CurrentDay
            || !IsSingleBit(rehearsalBit)
            || (state.ScheduledRehearsalMask & rehearsalBit) != 0
            || (state.CompletedRehearsalMask & rehearsalBit) != 0
            || state.ActiveRehearsalDay > 0)
        {
            return false;
        }

        state.ScheduledRehearsalMask |= rehearsalBit;
        state.ActiveRehearsalDay = day;
        state.IntroducedConcepts.Add(ExperienceEventConcept.Defense);
        return true;
    }

    public bool ResolveRehearsal(int rehearsalBit)
    {
        ExperiencePacingAggregateState state = State;
        if (state.ActiveRehearsalDay <= 0
            || !IsSingleBit(rehearsalBit)
            || (state.ScheduledRehearsalMask & rehearsalBit) == 0
            || (state.CompletedRehearsalMask & rehearsalBit) != 0)
        {
            return false;
        }

        state.CompletedRehearsalMask |= rehearsalBit;
        state.ActiveRehearsalDay = 0;
        return true;
    }

    public bool MarkExteriorIncidentStarted(ExteriorIncidentKind kind)
    {
        ExperiencePacingAggregateState state = State;
        int before = state.IntroducedConcepts.Count;
        switch (kind)
        {
            case ExteriorIncidentKind.MerchantCart:
            case ExteriorIncidentKind.Informant:
                state.IntroducedConcepts.Add(
                    ExperienceEventConcept.GuestService);
                break;
            case ExteriorIncidentKind.CargoDamage:
                state.IntroducedConcepts.Add(
                    ExperienceEventConcept.StockConsumption);
                state.IntroducedConcepts.Add(
                    ExperienceEventConcept.Cleanliness);
                break;
            case ExteriorIncidentKind.InjuredReturnee:
                state.IntroducedConcepts.Add(
                    ExperienceEventConcept.MinorInjury);
                break;
            case ExteriorIncidentKind.Thief:
                state.IntroducedConcepts.Add(
                    ExperienceEventConcept.Sabotage);
                break;
            case ExteriorIncidentKind.PredatorApproach:
                state.IntroducedConcepts.Add(
                    ExperienceEventConcept.Defense);
                break;
        }

        return state.IntroducedConcepts.Count != before;
    }

    public ExperiencePacingAggregateState PrepareRestoreCandidate(
        int currentDay,
        int scheduledRehearsalMask,
        int completedRehearsalMask,
        int activeRehearsalDay,
        IEnumerable<ExperienceEventConcept> introducedConcepts,
        IReadOnlyDictionary<int, int> rehearsalBitsByDay)
    {
        if (rehearsalBitsByDay == null)
        {
            throw new ArgumentNullException(nameof(rehearsalBitsByDay));
        }

        ExperienceEventConcept[] conceptHistory =
            (introducedConcepts
                ?? throw new ArgumentNullException(nameof(introducedConcepts)))
            .ToArray();
        HashSet<ExperienceEventConcept> concepts = new(conceptHistory);
        HashSet<int> distinctBits = new();
        bool rehearsalMapValid = rehearsalBitsByDay.All(pair =>
            pair.Key >= 1
            && IsSingleBit(pair.Value)
            && distinctBits.Add(pair.Value));
        int allowedMask = distinctBits.Aggregate(
            0,
            (mask, bit) => mask | bit);
        bool masksValid = currentDay >= 1
            && (scheduledRehearsalMask & ~allowedMask) == 0
            && (completedRehearsalMask & ~allowedMask) == 0
            && (completedRehearsalMask & ~scheduledRehearsalMask) == 0;
        bool scheduledDaysValid = rehearsalBitsByDay.All(pair =>
            (scheduledRehearsalMask & pair.Value) == 0
            || currentDay >= pair.Key);
        int activeBit = rehearsalBitsByDay.TryGetValue(
            activeRehearsalDay,
            out int resolvedActiveBit)
            ? resolvedActiveBit
            : 0;
        bool activeValid = activeRehearsalDay == 0
            || activeBit != 0
            && currentDay >= activeRehearsalDay
            && (scheduledRehearsalMask & activeBit) != 0
            && (completedRehearsalMask & activeBit) == 0;
        bool conceptsValid = concepts.Count == conceptHistory.Length
            && concepts.All(concept =>
                Enum.IsDefined(typeof(ExperienceEventConcept), concept))
            && (scheduledRehearsalMask == 0
                || concepts.Contains(ExperienceEventConcept.Defense));
        if (!rehearsalMapValid
            || !masksValid
            || !scheduledDaysValid
            || !activeValid
            || !conceptsValid)
        {
            throw new InvalidOperationException(
                "Experience-pacing candidate violates day, mask, active rehearsal, or concept invariants.");
        }

        ExperiencePacingAggregateState restored = new()
        {
            CurrentDay = currentDay,
            ScheduledRehearsalMask = scheduledRehearsalMask,
            CompletedRehearsalMask = completedRehearsalMask,
            ActiveRehearsalDay = activeRehearsalDay
        };
        restored.IntroducedConcepts.UnionWith(concepts);
        return restored;
    }

    public void PublishRestoreCandidate(
        ExperiencePacingAggregateState candidate)
    {
        replaceState(candidate
            ?? throw new ArgumentNullException(nameof(candidate)));
    }

    private static bool IsSingleBit(int value) =>
        value > 0 && (value & (value - 1)) == 0;
}
