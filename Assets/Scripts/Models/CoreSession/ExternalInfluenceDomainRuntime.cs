using System;
using System.Collections.Generic;
using DungeonStory.Foundation;

namespace DungeonStory.CoreSession
{
    public sealed class ExternalInfluenceAggregateStateStore
    {
        private readonly DungeonRuntimeAggregateRootStore rootStore;

        public ExternalInfluenceAggregateStateStore(
            DungeonRuntimeAggregateRootStore rootStore)
        {
            this.rootStore = rootStore
                ?? throw new ArgumentNullException(nameof(rootStore));
        }

        public ExternalInfluenceAggregateState Current =>
            rootStore.GetOrCreate(() => new ExternalInfluenceAggregateState());

        public void Replace(ExternalInfluenceAggregateState restored)
        {
            rootStore.Replace(
                restored ?? throw new ArgumentNullException(nameof(restored)));
        }
    }

    public readonly struct ExternalInfluencePressureResult
    {
        public ExternalInfluencePressureResult(
            float weatherPressure,
            float exposedFoodPressure)
        {
            WeatherPressure = weatherPressure;
            ExposedFoodPressure = exposedFoodPressure;
        }

        public float WeatherPressure { get; }
        public float ExposedFoodPressure { get; }
        public float Total => WeatherPressure + ExposedFoodPressure;
    }

    public static class ExternalInfluenceDomainRules
    {
        private const float MaximumRenown = 999f;
        private const float MaximumDread = 999f;
        private const float MaximumRumor = 100f;
        private const float MaximumEcologyPressure = 100f;
        private const float MaximumScoutingLabor = 999f;
        private const float EcologyWarningThreshold = 60f;
        private const float EcologyRaidThreshold = 80f;
        private const float EcologyPressureAfterRaid = 35f;
        private const float EcologyPressureRelief = 45f;

        public static float AddRenown(
            DungeonExternalInfluenceSaveData state,
            float amount)
        {
            RequireState(state);
            float before = state.renown;
            state.renown = Clamp(
                state.renown + Math.Max(0f, amount),
                0f,
                MaximumRenown);
            return state.renown - before;
        }

        public static void AddDread(
            DungeonExternalInfluenceSaveData state,
            float amount)
        {
            RequireState(state);
            state.dread = Clamp(
                state.dread + Math.Max(0f, amount),
                0f,
                MaximumDread);
        }

        public static void AddHostileRumor(
            DungeonExternalInfluenceSaveData state,
            float amount)
        {
            RequireState(state);
            state.hostileRumor = Clamp(
                state.hostileRumor + Math.Max(0f, amount),
                0f,
                MaximumRumor);
        }

        public static void AddEcologyPressure(
            DungeonExternalInfluenceSaveData state,
            float amount)
        {
            RequireState(state);
            state.ecologyPressure = Clamp(
                state.ecologyPressure + amount,
                0f,
                MaximumEcologyPressure);
        }

        public static void AddScoutingLabor(
            DungeonExternalInfluenceSaveData state,
            float amount)
        {
            RequireState(state);
            state.scoutingLabor = Clamp(
                state.scoutingLabor + Math.Max(0f, amount),
                0f,
                MaximumScoutingLabor);
        }

        public static void ApplyOperatingDayReport(
            DungeonExternalInfluenceSaveData state,
            int totalVisits,
            float averageSatisfaction)
        {
            RequireState(state);
            if (totalVisits <= 0)
            {
                return;
            }

            float amount = Math.Max(1f, totalVisits / 5f);
            if (averageSatisfaction >= 70f)
            {
                float generatedRenown = AddRenown(state, amount);
                state.hostileRumor = Math.Max(
                    0f,
                    state.hostileRumor - generatedRenown);
            }
            else if (averageSatisfaction < 40f)
            {
                AddHostileRumor(state, amount);
            }
        }

        public static ExternalInfluencePressureResult BeginOperatingDay(
            DungeonExternalInfluenceSaveData state,
            int day,
            bool coldSnap,
            int exposedFoodCount)
        {
            RequireState(state);
            state.currentOperatingDay = day;
            ExternalInfluencePressureResult pressure = new(
                coldSnap ? 25f : 4f,
                Math.Min(20f, Math.Max(0, exposedFoodCount) * 1.5f));
            state.lastWeatherPressure = pressure.WeatherPressure;
            state.lastExposedFoodPressure = pressure.ExposedFoodPressure;
            AddEcologyPressure(state, pressure.Total);
            return pressure;
        }

        public static bool TryPrepareRumorMitigation(
            DungeonExternalInfluenceSaveData state,
            HostileRumorMitigationMethod method,
            float maximumMitigation,
            int maximumRenownCost,
            int maximumGoldCost,
            out float reducedAmount,
            out int cost,
            out DomainFailure failure)
        {
            RequireState(state);
            reducedAmount = Math.Min(
                maximumMitigation,
                Math.Max(0f, state.hostileRumor));
            cost = 0;
            if (reducedAmount <= 0f)
            {
                failure = new DomainFailure(FailureCode.HostileRumorUnavailable);
                return false;
            }
            if (state.currentOperatingDay < 0)
            {
                failure = new DomainFailure(FailureCode.OperatingDayNotStarted);
                return false;
            }
            if (state.lastRumorMitigationDay == state.currentOperatingDay)
            {
                failure = new DomainFailure(
                    FailureCode.RumorMitigationAlreadyUsed,
                    state.currentOperatingDay.ToString());
                return false;
            }

            float ratio = reducedAmount / maximumMitigation;
            cost = method switch
            {
                HostileRumorMitigationMethod.Renown =>
                    (int)Math.Ceiling(maximumRenownCost * ratio),
                HostileRumorMitigationMethod.Gold =>
                    (int)Math.Ceiling(maximumGoldCost * ratio),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(method),
                    method,
                    null)
            };
            if (method == HostileRumorMitigationMethod.Renown
                && state.renown < cost)
            {
                failure = new DomainFailure(
                    FailureCode.InsufficientRenown,
                    state.renown.ToString("0.#"),
                    cost.ToString());
                return false;
            }

            failure = DomainFailure.None;
            return true;
        }

        public static void CommitRumorMitigation(
            DungeonExternalInfluenceSaveData state,
            HostileRumorMitigationMethod method,
            float reducedAmount,
            int cost)
        {
            RequireState(state);
            if (method == HostileRumorMitigationMethod.Renown)
            {
                state.renown -= cost;
            }
            state.hostileRumor = Math.Max(
                0f,
                state.hostileRumor - reducedAmount);
            state.lastRumorMitigationDay = state.currentOperatingDay;
        }

        public static bool TryArmDreadDefense(
            DungeonExternalInfluenceSaveData state,
            float dreadCost,
            out DomainFailure failure)
        {
            RequireState(state);
            if (state.dreadDefenseArmed)
            {
                failure = new DomainFailure(FailureCode.DreadDefenseAlreadyArmed);
                return false;
            }
            if (state.dread < dreadCost)
            {
                failure = new DomainFailure(
                    FailureCode.InsufficientDread,
                    state.dread.ToString("0.#"),
                    dreadCost.ToString("0"));
                return false;
            }

            state.dread -= dreadCost;
            state.dreadDefenseArmed = true;
            failure = DomainFailure.None;
            return true;
        }

        public static bool BeginInvasionDread(
            ExternalInfluenceAggregateState aggregate,
            bool boss)
        {
            RequireAggregate(aggregate);
            DungeonExternalInfluenceSaveData state = aggregate.Data;
            if (state.dreadDefenseActive)
            {
                return true;
            }
            if (!state.dreadDefenseArmed)
            {
                return false;
            }

            state.dreadDefenseArmed = false;
            state.dreadDefenseActive = true;
            state.dreadDefenseBoss = boss;
            aggregate.DreadAffectedIntruders.Clear();
            return true;
        }

        public static void EndInvasionDread(
            ExternalInfluenceAggregateState aggregate)
        {
            RequireAggregate(aggregate);
            aggregate.Data.dreadDefenseActive = false;
            aggregate.Data.dreadDefenseBoss = false;
            aggregate.DreadAffectedIntruders.Clear();
        }

        public static void MarkDreadAffectedIntruder(
            ExternalInfluenceAggregateState aggregate,
            string characterId)
        {
            RequireAggregate(aggregate);
            string normalized = NormalizeId(characterId);
            if (normalized.Length > 0)
            {
                aggregate.DreadAffectedIntruders.Add(normalized);
            }
        }

        public static float GetDreadSpeedMultiplier(
            ExternalInfluenceAggregateState aggregate,
            string characterId)
        {
            RequireAggregate(aggregate);
            DungeonExternalInfluenceSaveData state = aggregate.Data;
            return state.dreadDefenseActive
                && aggregate.DreadAffectedIntruders.Contains(
                    NormalizeId(characterId))
                        ? state.dreadDefenseBoss ? 0.95f : 0.9f
                        : 1f;
        }

        public static bool IsIntelUnlocked(
            ExternalInfluenceAggregateState aggregate,
            string siteId)
        {
            RequireAggregate(aggregate);
            return aggregate.IntelUnlocked.Contains(NormalizeId(siteId));
        }

        public static void UnlockIntel(
            ExternalInfluenceAggregateState aggregate,
            string siteId)
        {
            RequireAggregate(aggregate);
            aggregate.IntelUnlocked.Add(NormalizeId(siteId));
        }

        public static bool TrySpendRenownForIntel(
            DungeonExternalInfluenceSaveData state,
            float cost,
            out DomainFailure failure)
        {
            RequireState(state);
            if (state.renown < cost)
            {
                failure = new DomainFailure(
                    FailureCode.InsufficientRenown,
                    state.renown.ToString("0.#"),
                    cost.ToString("0"));
                return false;
            }

            state.renown -= cost;
            failure = DomainFailure.None;
            return true;
        }

        public static bool TrySpendScoutingForIntel(
            DungeonExternalInfluenceSaveData state,
            float cost,
            out DomainFailure failure)
        {
            RequireState(state);
            if (state.scoutingLabor < cost)
            {
                failure = new DomainFailure(
                    FailureCode.InsufficientScoutingLabor,
                    state.scoutingLabor.ToString("0.#"),
                    cost.ToString("0"));
                return false;
            }

            state.scoutingLabor -= cost;
            failure = DomainFailure.None;
            return true;
        }

        public static float AdvanceEcologyRaidCountdown(
            float remainingSeconds,
            float gameDeltaTime,
            bool paused)
        {
            return paused
                ? Math.Max(0f, remainingSeconds)
                : Math.Max(
                    0f,
                    remainingSeconds - Math.Max(0f, gameDeltaTime));
        }

        public static bool IsIntelSiteActive(
            bool fixedBoss,
            int expiresDay,
            int currentDay)
        {
            return fixedBoss
                || expiresDay > 0 && currentDay < expiresDay;
        }

        public static bool TryIssueEcologyWarning(
            DungeonExternalInfluenceSaveData state)
        {
            RequireState(state);
            if (state.ecologyPressure < EcologyWarningThreshold
                || state.ecologyWarningIssued)
            {
                return false;
            }

            state.ecologyWarningIssued = true;
            return true;
        }

        public static bool TryScheduleEcologyRaid(
            DungeonExternalInfluenceSaveData state,
            float countdownSeconds)
        {
            RequireState(state);
            if (state.ecologyPressure < EcologyRaidThreshold
                || state.ecologyRaidScheduled
                || state.ecologyRaidInProgress)
            {
                return false;
            }

            state.ecologyRaidScheduled = true;
            state.ecologyRaidRemainingSeconds = countdownSeconds;
            state.ecologyRaidSequence++;
            state.ecologyResolutionReported = false;
            return true;
        }

        public static string BeginScheduledEcologyRaid(
            DungeonExternalInfluenceSaveData state)
        {
            RequireState(state);
            state.ecologyRaidScheduled = false;
            state.ecologyRaidRemainingSeconds = 0f;
            return $"ecology:{state.ecologyRaidSequence}";
        }

        public static void RecordEcologyRaidStartFailure(
            DungeonExternalInfluenceSaveData state)
        {
            RequireState(state);
            state.ecologyResolutionReported = true;
            RelieveEcologyPressure(state);
        }

        public static void RecordEcologyRaidStarted(
            DungeonExternalInfluenceSaveData state)
        {
            RequireState(state);
            state.ecologyRaidInProgress = true;
            state.ecologyResolutionReported = false;
        }

        public static void RecordEcologyRaidResolved(
            DungeonExternalInfluenceSaveData state)
        {
            RequireState(state);
            state.ecologyRaidInProgress = false;
            state.ecologyResolutionReported = true;
            RelieveEcologyPressure(state);
        }

        public static string NormalizeId(string value) =>
            value?.Trim() ?? string.Empty;

        private static void RelieveEcologyPressure(
            DungeonExternalInfluenceSaveData state)
        {
            state.ecologyPressure = Math.Max(
                EcologyPressureAfterRaid,
                state.ecologyPressure - EcologyPressureRelief);
            state.ecologyWarningIssued = false;
        }

        private static void RequireAggregate(
            ExternalInfluenceAggregateState aggregate)
        {
            if (aggregate == null)
            {
                throw new ArgumentNullException(nameof(aggregate));
            }
        }

        private static void RequireState(
            DungeonExternalInfluenceSaveData state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
        }

        private static float Clamp(float value, float minimum, float maximum) =>
            Math.Min(maximum, Math.Max(minimum, value));
    }
}
