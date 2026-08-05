using System;
using System.Collections.Generic;

namespace DungeonStory.Content.CoreSession
{
    public enum DungeonRunFlowEventKind
    {
        DayStarted = 0,
        RehearsalSchedulingResolved = 1,
        BossInvasionStarted = 2,
        InvasionResolved = 3,
        TruthRevealed = 4,
        OwnerRunEnded = 5
    }

    public readonly struct DungeonRunFlowEvent
    {
        private DungeonRunFlowEvent(
            DungeonRunFlowEventKind kind,
            int day,
            int bossCycle,
            bool flag,
            int rehearsalDay,
            DungeonRunOutcome outcome)
        {
            Kind = kind;
            Day = day;
            BossCycle = bossCycle;
            Flag = flag;
            RehearsalDay = rehearsalDay;
            Outcome = outcome;
        }

        public DungeonRunFlowEventKind Kind { get; }
        public int Day { get; }
        public int BossCycle { get; }
        public bool Flag { get; }
        public int RehearsalDay { get; }
        public DungeonRunOutcome Outcome { get; }

        public static DungeonRunFlowEvent DayStarted(int day) =>
            new(DungeonRunFlowEventKind.DayStarted, day, 0, false, 0, default);

        public static DungeonRunFlowEvent RehearsalSchedulingResolved(
            int day,
            bool scheduled,
            int dueBossCycle) =>
            new(
                DungeonRunFlowEventKind.RehearsalSchedulingResolved,
                day,
                dueBossCycle,
                scheduled,
                0,
                default);

        public static DungeonRunFlowEvent BossInvasionStarted() =>
            new(
                DungeonRunFlowEventKind.BossInvasionStarted,
                0,
                0,
                false,
                0,
                default);

        public static DungeonRunFlowEvent InvasionResolved(
            bool defended,
            int rehearsalDay) =>
            new(
                DungeonRunFlowEventKind.InvasionResolved,
                0,
                0,
                defended,
                rehearsalDay,
                default);

        public static DungeonRunFlowEvent TruthRevealed() =>
            new(
                DungeonRunFlowEventKind.TruthRevealed,
                0,
                0,
                false,
                0,
                default);

        public static DungeonRunFlowEvent OwnerRunEnded(
            DungeonRunOutcome outcome) =>
            new(
                DungeonRunFlowEventKind.OwnerRunEnded,
                0,
                0,
                false,
                0,
                outcome);
    }

    public enum DungeonRunFlowEffectKind
    {
        AdvancePacingDay = 0,
        RaisePhaseAlert = 1,
        EvaluateRehearsal = 2,
        ScheduleBossInvasion = 3,
        ResolveRehearsal = 4,
        RaiseRehearsalResolvedAlert = 5,
        RaiseDefenseFailedAlert = 6,
        RaiseBossDefendedAlert = 7,
        ForceArmedInvasion = 8,
        CompleteRun = 9
    }

    public readonly struct DungeonRunFlowEffect
    {
        internal DungeonRunFlowEffect(
            DungeonRunFlowEffectKind kind,
            int day = 0,
            int bossCycle = 0,
            DungeonRunPhase phase = default,
            DungeonRunOutcome outcome = default,
            bool defended = false)
        {
            Kind = kind;
            Day = day;
            BossCycle = bossCycle;
            Phase = phase;
            Outcome = outcome;
            Defended = defended;
        }

        public DungeonRunFlowEffectKind Kind { get; }
        public int Day { get; }
        public int BossCycle { get; }
        public DungeonRunPhase Phase { get; }
        public DungeonRunOutcome Outcome { get; }
        public bool Defended { get; }
    }

    public sealed class DungeonRunFlowTransition
    {
        internal DungeonRunFlowTransition(
            DungeonRunFlowAggregateState state,
            IReadOnlyList<DungeonRunFlowEffect> effects,
            bool stateChanged)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Effects = effects ?? throw new ArgumentNullException(nameof(effects));
            StateChanged = stateChanged;
        }

        public DungeonRunFlowAggregateState State { get; }
        public IReadOnlyList<DungeonRunFlowEffect> Effects { get; }
        public bool StateChanged { get; }
    }

    public static class DungeonRunFlowReducer
    {
        public static DungeonRunFlowTransition Reduce(
            DungeonRunFlowAggregateState current,
            DungeonRunFlowEvent eventType,
            CoreSessionRulesDefinition rules)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return eventType.Kind switch
            {
                DungeonRunFlowEventKind.DayStarted =>
                    ReduceDayStarted(current, eventType.Day, rules),
                DungeonRunFlowEventKind.RehearsalSchedulingResolved =>
                    ReduceRehearsalScheduling(current, eventType),
                DungeonRunFlowEventKind.BossInvasionStarted =>
                    ReduceBossStarted(current),
                DungeonRunFlowEventKind.InvasionResolved =>
                    ReduceInvasionResolved(current, eventType),
                DungeonRunFlowEventKind.TruthRevealed =>
                    ReduceTruthRevealed(current),
                DungeonRunFlowEventKind.OwnerRunEnded =>
                    ReduceOwnerRunEnded(current, eventType.Outcome),
                _ => Unchanged(current)
            };
        }

        public static float ResolveBossHealthMultiplier(int cycle) =>
            1f + 0.35f * Math.Max(0, cycle);

        public static float ResolveBossDamageMultiplier(int cycle) =>
            1f + 0.35f * Math.Max(0, cycle);

        public static float ResolveThreatRiseMultiplier(int cycle) =>
            1f + 0.2f * Math.Max(0, cycle);

        private static DungeonRunFlowTransition ReduceDayStarted(
            DungeonRunFlowAggregateState current,
            int day,
            CoreSessionRulesDefinition rules)
        {
            int normalizedDay = Math.Max(1, day);
            if (current.Outcome != DungeonRunOutcome.None
                || normalizedDay <= current.CurrentDay)
            {
                return Unchanged(current);
            }

            DungeonRunFlowAggregateState next = Clone(current);
            next.CurrentDay = normalizedDay;
            List<DungeonRunFlowEffect> effects = new()
            {
                new DungeonRunFlowEffect(
                    DungeonRunFlowEffectKind.AdvancePacingDay,
                    day: normalizedDay)
            };
            DungeonRunPhase phase = DungeonRunFlowRules.ResolvePhaseForDay(
                normalizedDay,
                rules);
            if (phase != next.Phase)
            {
                next.Phase = phase;
                effects.Add(new DungeonRunFlowEffect(
                    DungeonRunFlowEffectKind.RaisePhaseAlert,
                    phase: phase));
            }
            effects.Add(new DungeonRunFlowEffect(
                DungeonRunFlowEffectKind.EvaluateRehearsal,
                day: normalizedDay,
                bossCycle: DungeonRunFlowRules.ResolveBossCycleForDay(
                    normalizedDay,
                    rules)));
            return Changed(next, effects);
        }

        private static DungeonRunFlowTransition ReduceRehearsalScheduling(
            DungeonRunFlowAggregateState current,
            DungeonRunFlowEvent eventType)
        {
            if (current.Outcome != DungeonRunOutcome.None
                || eventType.Day != current.CurrentDay
                || eventType.Flag
                || eventType.BossCycle <= current.BossCycle
                || current.BossArmed
                || current.BossActive)
            {
                return Unchanged(current);
            }

            DungeonRunFlowAggregateState next = Clone(current);
            next.BossCycle = Math.Max(1, eventType.BossCycle);
            next.BossArmed = true;
            return Changed(
                next,
                new DungeonRunFlowEffect(
                    DungeonRunFlowEffectKind.ScheduleBossInvasion,
                    bossCycle: next.BossCycle));
        }

        private static DungeonRunFlowTransition ReduceBossStarted(
            DungeonRunFlowAggregateState current)
        {
            if (current.Outcome != DungeonRunOutcome.None
                || current.BossActive)
            {
                return Unchanged(current);
            }

            DungeonRunFlowAggregateState next = Clone(current);
            next.BossArmed = false;
            next.BossActive = true;
            next.Phase = DungeonRunPhase.EndlessDefense;
            return Changed(next);
        }

        private static DungeonRunFlowTransition ReduceInvasionResolved(
            DungeonRunFlowAggregateState current,
            DungeonRunFlowEvent eventType)
        {
            if (current.Outcome != DungeonRunOutcome.None)
            {
                return Unchanged(current);
            }
            if (eventType.RehearsalDay > 0)
            {
                return EffectsOnly(
                    current,
                    new DungeonRunFlowEffect(
                        DungeonRunFlowEffectKind.ResolveRehearsal,
                        day: eventType.RehearsalDay),
                    new DungeonRunFlowEffect(
                        DungeonRunFlowEffectKind.RaiseRehearsalResolvedAlert,
                        day: eventType.RehearsalDay,
                        defended: eventType.Flag));
            }
            if (!eventType.Flag)
            {
                DungeonRunFlowAggregateState next = Clone(current);
                next.Outcome = DungeonRunOutcome.Defeat;
                next.Phase = DungeonRunPhase.Finished;
                next.BossArmed = false;
                next.BossActive = false;
                return Changed(
                    next,
                    new DungeonRunFlowEffect(
                        DungeonRunFlowEffectKind.RaiseDefenseFailedAlert),
                    new DungeonRunFlowEffect(
                        DungeonRunFlowEffectKind.CompleteRun,
                        outcome: DungeonRunOutcome.Defeat));
            }
            if (current.BossActive)
            {
                DungeonRunFlowAggregateState next = Clone(current);
                next.BossActive = false;
                return Changed(
                    next,
                    new DungeonRunFlowEffect(
                        DungeonRunFlowEffectKind.RaiseBossDefendedAlert,
                        bossCycle: next.BossCycle));
            }
            return current.BossArmed
                ? EffectsOnly(
                    current,
                    new DungeonRunFlowEffect(
                        DungeonRunFlowEffectKind.ForceArmedInvasion))
                : Unchanged(current);
        }

        private static DungeonRunFlowTransition ReduceTruthRevealed(
            DungeonRunFlowAggregateState current)
        {
            if (current.Outcome != DungeonRunOutcome.None)
            {
                return Unchanged(current);
            }

            DungeonRunFlowAggregateState next = Clone(current);
            next.Outcome = DungeonRunOutcome.Victory;
            next.Phase = DungeonRunPhase.Finished;
            next.BossArmed = false;
            next.BossActive = false;
            return Changed(
                next,
                new DungeonRunFlowEffect(
                    DungeonRunFlowEffectKind.CompleteRun,
                    outcome: DungeonRunOutcome.Victory));
        }

        private static DungeonRunFlowTransition ReduceOwnerRunEnded(
            DungeonRunFlowAggregateState current,
            DungeonRunOutcome outcome)
        {
            DungeonRunOutcome normalized = outcome == DungeonRunOutcome.None
                ? DungeonRunOutcome.Defeat
                : outcome;
            if (current.Outcome == normalized
                && current.Phase == DungeonRunPhase.Finished
                && !current.BossArmed
                && !current.BossActive)
            {
                return Unchanged(current);
            }

            DungeonRunFlowAggregateState next = Clone(current);
            next.Outcome = normalized;
            next.Phase = DungeonRunPhase.Finished;
            next.BossArmed = false;
            next.BossActive = false;
            return Changed(next);
        }

        private static DungeonRunFlowAggregateState Clone(
            DungeonRunFlowAggregateState source)
        {
            return new DungeonRunFlowAggregateState
            {
                Phase = source.Phase,
                Outcome = source.Outcome,
                CurrentDay = source.CurrentDay,
                BossCycle = source.BossCycle,
                BossArmed = source.BossArmed,
                BossActive = source.BossActive
            };
        }

        private static DungeonRunFlowTransition Changed(
            DungeonRunFlowAggregateState state,
            params DungeonRunFlowEffect[] effects)
        {
            return new DungeonRunFlowTransition(
                state,
                Array.AsReadOnly(effects ?? Array.Empty<DungeonRunFlowEffect>()),
                true);
        }

        private static DungeonRunFlowTransition Changed(
            DungeonRunFlowAggregateState state,
            IReadOnlyList<DungeonRunFlowEffect> effects)
        {
            return new DungeonRunFlowTransition(state, effects, true);
        }

        private static DungeonRunFlowTransition EffectsOnly(
            DungeonRunFlowAggregateState state,
            params DungeonRunFlowEffect[] effects)
        {
            return new DungeonRunFlowTransition(
                state,
                Array.AsReadOnly(effects ?? Array.Empty<DungeonRunFlowEffect>()),
                false);
        }

        private static DungeonRunFlowTransition Unchanged(
            DungeonRunFlowAggregateState state)
        {
            return new DungeonRunFlowTransition(
                state,
                Array.Empty<DungeonRunFlowEffect>(),
                false);
        }
    }
}
