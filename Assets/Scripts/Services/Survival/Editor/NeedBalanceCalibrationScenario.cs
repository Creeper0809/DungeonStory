using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NeedBalanceCalibrationScenario
{
    private const int SeedCount = 30;
    private const int SimulationDays = 5;
    private const float DaySeconds = 180f;
    private const string ReportDirectory =
        "Artifacts/QA/NeedBalanceCalibration";
    private const string ReportPath =
        ReportDirectory + "/need-balance-latest.json";

    private static readonly int[] PopulationSizes = { 3, 10, 50 };
    private static readonly SpeciesProbe[] Species =
    {
        new SpeciesProbe("Slime", 0.92f, 1.05f, true),
        new SpeciesProbe("Orc", 1.12f, 1.08f, true),
        new SpeciesProbe("Vampire", 0.88f, 0.90f, true),
        new SpeciesProbe("Beastkin", 1.05f, 1.04f, true),
        new SpeciesProbe("Demon", 0.96f, 1.00f, true),
        new SpeciesProbe("Kobold", 1.08f, 1.06f, true),
        new SpeciesProbe("Myconid", 0.82f, 0.88f, true),
        new SpeciesProbe("Harpy", 1.02f, 1.09f, true),
        new SpeciesProbe("Golem", 0f, 0f, false)
    };

    [MenuItem(
        "DungeonStory/Debug/Survival/Run Need Balance Calibration")]
    public static void RunFromMenu()
    {
        NeedBalanceCalibrationReport report = RunCalibration();
        Directory.CreateDirectory(ReportDirectory);
        File.WriteAllText(
            ReportPath,
            JsonUtility.ToJson(report, true));
        AssetDatabase.Refresh();

        if (report.errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Need balance calibration failed:\n"
                + string.Join("\n", report.errors));
        }

        Debug.Log(
            $"Need balance calibration passed. "
            + $"life={report.standardSufficient.lifeRatioP95:P1}; "
            + $"work={report.standardSufficient.workRatioMean:P1}; "
            + $"report={ReportPath}");
    }

    public static NeedBalanceCalibrationReport RunCalibration()
    {
        NeedBalanceCalibrationReport report =
            new NeedBalanceCalibrationReport();
        ValidateStaticContracts(report.errors);

        NeedBalanceCandidate initial = NeedBalanceCandidate.Standard;
        NeedBalanceCandidate selected = Optimize(initial);
        report.initial = initial;
        report.selected = selected;

        foreach (DungeonSurvivalPressure pressure in new[]
                 {
                     DungeonSurvivalPressure.Relaxed,
                     DungeonSurvivalPressure.Standard,
                     DungeonSurvivalPressure.Harsh
                 })
        {
            foreach (NeedSupplyScenario supply in
                     Enum.GetValues(typeof(NeedSupplyScenario)))
            {
                NeedBalanceAggregate aggregate = RunAggregate(
                    selected,
                    pressure,
                    supply);
                report.aggregates.Add(aggregate);
                if (pressure == DungeonSurvivalPressure.Standard
                    && supply == NeedSupplyScenario.Sufficient)
                {
                    report.standardSufficient = aggregate;
                }
            }
        }

        ValidateProductGate(report.standardSufficient, report.errors);
        ValidateTimeScaleDeterminism(selected, report.errors);
        ValidateGolemExclusion(report.errors);
        return report;
    }

    private static NeedBalanceCandidate Optimize(
        NeedBalanceCandidate initial)
    {
        NeedBalanceCandidate best = initial;
        float bestScore = Score(RunOptimizationAggregate(
            best,
            DungeonSurvivalPressure.Standard,
            NeedSupplyScenario.Sufficient));

        for (int pass = 0; pass < 2; pass++)
        {
            for (int dimension = 0; dimension < 7; dimension++)
            {
                foreach (float value in EnumerateDimension(dimension))
                {
                    NeedBalanceCandidate candidate =
                        best.WithDimension(dimension, value);
                    NeedBalanceAggregate aggregate =
                        RunOptimizationAggregate(
                        candidate,
                        DungeonSurvivalPressure.Standard,
                        NeedSupplyScenario.Sufficient);
                    float score = Score(aggregate);
                    if (score < bestScore - 0.0001f
                        || Mathf.Approximately(score, bestScore)
                        && IsSaferTieBreak(aggregate, candidate, best))
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }
            }
        }

        return best;
    }

    private static NeedBalanceAggregate RunOptimizationAggregate(
        NeedBalanceCandidate candidate,
        DungeonSurvivalPressure pressure,
        NeedSupplyScenario supply)
    {
        List<NeedBalanceAgentMetrics> agents =
            new List<NeedBalanceAgentMetrics>(36);
        SpeciesProbe[] probes =
        {
            Species[0],
            Species[1],
            Species[6]
        };
        for (int seed = 0; seed < 4; seed++)
        {
            foreach (SpeciesProbe species in probes)
            {
                for (int actorIndex = 0; actorIndex < 3; actorIndex++)
                {
                    agents.Add(SimulateAgent(
                        candidate,
                        pressure,
                        supply,
                        species,
                        10,
                        seed,
                        actorIndex,
                        1f));
                }
            }
        }

        return NeedBalanceAggregate.From(pressure, supply, agents);
    }

    private static IEnumerable<float> EnumerateDimension(int dimension)
    {
        float minimum;
        float maximum;
        float step;
        switch (dimension)
        {
            case 0:
                minimum = 45f; maximum = 55f; step = 2.5f; break;
            case 1:
                minimum = 50f; maximum = 65f; step = 3f; break;
            case 2:
                minimum = 20f; maximum = 30f; step = 1.2f; break;
            case 3:
                minimum = 15f; maximum = 24f; step = 0.9f; break;
            case 4:
                minimum = 0.30f; maximum = 0.45f; step = 0.0175f; break;
            case 5:
                minimum = 0.03f; maximum = 0.07f; step = 0.0025f; break;
            default:
                minimum = 0.04f; maximum = 0.08f; step = 0.003f; break;
        }

        for (float value = minimum;
             value <= maximum + step * 0.25f;
             value += step)
        {
            yield return Mathf.Min(value, maximum);
        }
    }

    private static float Score(NeedBalanceAggregate metrics)
    {
        float lifeError =
            Mathf.Abs(metrics.lifeRatioMean - 0.225f) / 0.225f;
        float useError =
            RangeError(metrics.mealsPerDay, 1f, 1.5f)
            + RangeError(metrics.drinksPerDay, 1f, 1.5f)
            + RangeError(metrics.restsPerDay, 0.7f, 1.2f)
            + RangeError(metrics.toiletsPerDay, 0.6f, 1.0f)
            + RangeError(metrics.hygienePerDay, 0.6f, 1.0f);
        useError /= 5f;
        float workLoss = Mathf.Max(
            0f,
            0.55f - metrics.workRatioMean) / 0.55f;
        float safety = Mathf.Clamp01(
            (35f - metrics.minimumNeedP10) / 35f)
            + Mathf.Min(1f, metrics.deprivationDamageCount)
            + Mathf.Min(1f, metrics.breakdownCount);
        float churn = Mathf.Min(
            1f,
            metrics.interruptionsPerDay / 2f
            + metrics.blockedRatioMean / 0.05f);
        return lifeError * 0.30f
            + useError * 0.20f
            + workLoss * 0.15f
            + safety * 0.15f
            + churn * 0.10f
            + metrics.speciesOutcomeSpread * 0.10f;
    }

    private static float RangeError(
        float value,
        float minimum,
        float maximum)
    {
        if (value < minimum)
        {
            return (minimum - value) / Mathf.Max(0.01f, minimum);
        }

        if (value > maximum)
        {
            return (value - maximum) / Mathf.Max(0.01f, maximum);
        }

        return 0f;
    }

    private static bool IsSaferTieBreak(
        NeedBalanceAggregate metrics,
        NeedBalanceCandidate candidate,
        NeedBalanceCandidate current)
    {
        if (metrics.deprivationDamageCount == 0
            && metrics.breakdownCount == 0)
        {
            return candidate.TotalDepletion < current.TotalDepletion;
        }

        return false;
    }

    private static NeedBalanceAggregate RunAggregate(
        NeedBalanceCandidate candidate,
        DungeonSurvivalPressure pressure,
        NeedSupplyScenario supply)
    {
        List<NeedBalanceAgentMetrics> agents =
            new List<NeedBalanceAgentMetrics>(
                SeedCount * PopulationSizes.Sum() * Species.Length);
        foreach (int population in PopulationSizes)
        {
            for (int seed = 0; seed < SeedCount; seed++)
            {
                for (int speciesIndex = 0;
                     speciesIndex < Species.Length;
                     speciesIndex++)
                {
                    SpeciesProbe species = Species[speciesIndex];
                    int count = population == 3
                        ? 3
                        : Mathf.Max(1, population / Species.Length);
                    for (int actorIndex = 0; actorIndex < count; actorIndex++)
                    {
                        agents.Add(SimulateAgent(
                            candidate,
                            pressure,
                            supply,
                            species,
                            population,
                            seed,
                            actorIndex,
                            1f));
                    }
                }
            }
        }

        return NeedBalanceAggregate.From(
            pressure,
            supply,
            agents);
    }

    private static NeedBalanceAgentMetrics SimulateAgent(
        NeedBalanceCandidate candidate,
        DungeonSurvivalPressure pressure,
        NeedSupplyScenario supply,
        SpeciesProbe species,
        int population,
        int seed,
        int actorIndex,
        float deltaSeconds)
    {
        SurvivalPressureBalanceProfile profile =
            SurvivalBalanceSettingsSO.GetDefaultPressure(pressure);
        NeedBalanceAgentState state = new NeedBalanceAgentState();
        System.Random random = new System.Random(
            seed * 7919
            + actorIndex * 131
            + StableHash(species.id));
        float duration = DaySeconds * SimulationDays;
        // 프레임/배속이 아니라 게임 시계의 고정 1초 샘플을 사용한다.
        // deltaSeconds는 호출자가 비교하려는 X1/X5 프레임 간격이며,
        // 동일한 게임 시간에는 같은 시뮬레이션 틱으로 정규화된다.
        _ = deltaSeconds;
        const float simulationStep = 1f;
        int tickCount = Mathf.CeilToInt(duration / simulationStep);
        for (int tick = 0; tick < tickCount; tick++)
        {
            float dt = Mathf.Min(
                simulationStep,
                duration - tick * simulationStep);
            if (dt <= 0f)
            {
                break;
            }

            bool inLifeAction = state.actionRemaining > 0f;
            if (inLifeAction)
            {
                state.actionRemaining =
                    Mathf.Max(0f, state.actionRemaining - dt);
                state.lifeSeconds += dt;
                if (state.actionRemaining <= 0f)
                {
                    CompleteNeedAction(
                        state,
                        profile.recoveryMultiplier);
                }
            }
            else
            {
                CharacterCondition? next = SelectRoutineNeed(
                    state,
                    species);
                if (next.HasValue)
                {
                    CharacterCondition selectedNeed = next.Value;
                    bool unavailable = IsUnavailable(
                        supply,
                        selectedNeed);
                    float queue = CalculateQueueSeconds(
                        supply,
                        population,
                        actorIndex,
                        random);
                    if (unavailable)
                    {
                        state.blockedSeconds += dt;
                    }
                    else
                    {
                        state.activeNeed = selectedNeed;
                        state.actionRemaining =
                            GetActionSeconds(selectedNeed) + queue;
                        state.queueSeconds += queue;
                        if (GetNeed(state, selectedNeed)
                            <= GetResponse(selectedNeed).emergencyStart)
                        {
                            state.interruptions++;
                        }
                    }
                }
                else
                {
                    state.workSeconds += dt;
                }
            }

            ApplyDepletion(
                state,
                candidate,
                profile.depletionMultiplier,
                species,
                dt,
                !inLifeAction);
            SampleNeeds(state, species);
        }

        return state.ToMetrics(
            species.id,
            species.biological,
            duration);
    }

    private static CharacterCondition? SelectRoutineNeed(
        NeedBalanceAgentState state,
        SpeciesProbe species)
    {
        CharacterCondition? selected = null;
        float selectedRatio = float.MaxValue;
        foreach (CharacterCondition condition in BalancedConditions)
        {
            if (!species.biological
                && (condition == CharacterCondition.HUNGER
                    || condition == CharacterCondition.THIRST))
            {
                continue;
            }

            CharacterNeedResponseProfile response =
                GetResponse(condition);
            float current = GetNeed(state, condition);
            if (current > response.routineStart)
            {
                continue;
            }

            float ratio = current / Mathf.Max(1f, response.routineStart);
            if (ratio < selectedRatio)
            {
                selected = condition;
                selectedRatio = ratio;
            }
        }

        return selected;
    }

    private static void ApplyDepletion(
        NeedBalanceAgentState state,
        NeedBalanceCandidate candidate,
        float pressureMultiplier,
        SpeciesProbe species,
        float dt,
        bool working)
    {
        float hungerSpecies = species.biological ? species.hunger : 0f;
        float thirstSpecies = species.biological ? species.thirst : 0f;
        state.hunger -= candidate.hungerPerDay
            * pressureMultiplier * hungerSpecies * dt / DaySeconds;
        state.thirst -= candidate.thirstPerDay
            * pressureMultiplier * thirstSpecies * dt / DaySeconds;
        state.excretion -= candidate.excretionPerDay
            * pressureMultiplier
            * Mathf.Max(hungerSpecies, thirstSpecies)
            * dt / DaySeconds;
        state.hygiene -= candidate.hygienePerDay
            * pressureMultiplier * dt / DaySeconds;
        if (working)
        {
            state.sleep -= candidate.workSleepPerSecond
                * pressureMultiplier * dt;
            state.excretion -= candidate.workExcretionPerSecond
                * pressureMultiplier * dt;
            state.hygiene -= candidate.workHygienePerSecond
                * pressureMultiplier * dt;
        }

        state.ClampNeeds();
        float minimum = MinimumNeed(state, species);
        if (minimum <= 15f)
        {
            state.damageTicks += dt / 10f;
        }
        if (minimum <= 0f)
        {
            state.breakdownSeconds += dt;
        }
    }

    private static void CompleteNeedAction(
        NeedBalanceAgentState state,
        float recoveryMultiplier)
    {
        switch (state.activeNeed)
        {
            case CharacterCondition.HUNGER:
                state.hunger += 42f * recoveryMultiplier;
                state.meals++;
                break;
            case CharacterCondition.THIRST:
                state.thirst += 65f * recoveryMultiplier;
                state.drinks++;
                break;
            case CharacterCondition.SLEEP:
                state.sleep += 42f * recoveryMultiplier;
                state.rests++;
                break;
            case CharacterCondition.EXCRETION:
                state.excretion += 50f * recoveryMultiplier;
                state.toilets++;
                break;
            case CharacterCondition.HYGIENE:
                state.hygiene += 45f * recoveryMultiplier;
                state.hygieneUses++;
                break;
        }

        state.activeNeed = null;
        state.ClampNeeds();
    }

    private static bool IsUnavailable(
        NeedSupplyScenario scenario,
        CharacterCondition condition)
    {
        return scenario == NeedSupplyScenario.FoodCut
                && condition == CharacterCondition.HUNGER
            || scenario == NeedSupplyScenario.WaterCut
                && condition == CharacterCondition.THIRST
            || scenario == NeedSupplyScenario.RestDestroyed
                && condition == CharacterCondition.SLEEP;
    }

    private static float CalculateQueueSeconds(
        NeedSupplyScenario scenario,
        int population,
        int actorIndex,
        System.Random random)
    {
        if (scenario != NeedSupplyScenario.Crowded)
        {
            return 0.5f + (float)random.NextDouble();
        }

        int queueBand = Mathf.Max(1, population / 10);
        return 2f + actorIndex % queueBand * 1.5f
            + (float)random.NextDouble();
    }

    private static float GetActionSeconds(CharacterCondition condition)
    {
        return condition switch
        {
            CharacterCondition.HUNGER => 8f,
            CharacterCondition.THIRST => 5f,
            CharacterCondition.SLEEP => 18f,
            CharacterCondition.EXCRETION => 7f,
            CharacterCondition.HYGIENE => 10f,
            _ => 0f
        };
    }

    private static CharacterNeedResponseProfile GetResponse(
        CharacterCondition condition)
    {
        return SurvivalBalanceSettingsSO.TryGetDefaultNeed(
            condition,
            out CharacterNeedBalanceEntry entry)
                ? entry.response
                : default;
    }

    private static float GetNeed(
        NeedBalanceAgentState state,
        CharacterCondition condition)
    {
        return condition switch
        {
            CharacterCondition.HUNGER => state.hunger,
            CharacterCondition.THIRST => state.thirst,
            CharacterCondition.SLEEP => state.sleep,
            CharacterCondition.EXCRETION => state.excretion,
            CharacterCondition.HYGIENE => state.hygiene,
            _ => 100f
        };
    }

    private static float MinimumNeed(
        NeedBalanceAgentState state,
        SpeciesProbe species)
    {
        float minimum = Mathf.Min(
            state.sleep,
            state.excretion,
            state.hygiene);
        return species.biological
            ? Mathf.Min(minimum, state.hunger, state.thirst)
            : minimum;
    }

    private static void SampleNeeds(
        NeedBalanceAgentState state,
        SpeciesProbe species)
    {
        state.minimumNeed = Mathf.Min(
            state.minimumNeed,
            MinimumNeed(state, species));
    }

    private static void ValidateStaticContracts(List<string> errors)
    {
        Require(
            SurvivalBalanceSettingsSO.DefaultDayLengthSeconds == 180f,
            "하루 길이가 180초가 아닙니다.",
            errors);
        NeedBalanceCandidate standard = NeedBalanceCandidate.Standard;
        Require(
            standard.hungerPerDay == 50f
            && standard.thirstPerDay == 60f
            && standard.excretionPerDay == 24f
            && standard.hygienePerDay == 18f,
            "표준 하루 감소량 계약이 다릅니다.",
            errors);
        Require(
            standard.workSleepPerSecond == 0.35f
            && standard.workExcretionPerSecond == 0.05f
            && standard.workHygienePerSecond == 0.06f,
            "표준 작업 피로 계약이 다릅니다.",
            errors);

        foreach (CharacterCondition condition in BalancedConditions)
        {
            CharacterNeedResponseProfile response = GetResponse(condition);
            float worstRate = GetWorstStandardRatePerSecond(
                standard,
                condition);
            float grace = (response.routineStart
                    - response.emergencyStart)
                / Mathf.Max(0.0001f, worstRate);
            Require(
                grace >= 60f,
                $"{condition} 일상→긴급 여유가 {grace:0.0}초입니다.",
                errors);
        }
    }

    private static float GetWorstStandardRatePerSecond(
        NeedBalanceCandidate candidate,
        CharacterCondition condition)
    {
        return condition switch
        {
            CharacterCondition.HUNGER =>
                candidate.hungerPerDay / DaySeconds,
            CharacterCondition.THIRST =>
                candidate.thirstPerDay / DaySeconds,
            CharacterCondition.SLEEP =>
                candidate.workSleepPerSecond,
            CharacterCondition.EXCRETION =>
                candidate.excretionPerDay / DaySeconds
                + candidate.workExcretionPerSecond,
            CharacterCondition.HYGIENE =>
                candidate.hygienePerDay / DaySeconds
                + candidate.workHygienePerSecond,
            _ => 0f
        };
    }

    private static void ValidateProductGate(
        NeedBalanceAggregate metrics,
        List<string> errors)
    {
        Require(metrics != null, "표준 공급 결과가 없습니다.", errors);
        if (metrics == null)
        {
            return;
        }

        Require(
            metrics.deprivationDamageCount == 0,
            $"정상 공급 결핍 피해 {metrics.deprivationDamageCount:0.##}회",
            errors);
        Require(
            metrics.breakdownCount == 0,
            $"정상 공급 붕괴 {metrics.breakdownCount:0.##}회",
            errors);
        Require(
            metrics.lifeRatioP95 <= 0.30f,
            $"생활 시간 p95 {metrics.lifeRatioP95:P1}",
            errors);
        Require(
            metrics.workRatioMean >= 0.55f,
            $"작업 시간 평균 {metrics.workRatioMean:P1}",
            errors);
        Require(
            metrics.blockedRatioMean <= 0.05f,
            $"차단 시간 평균 {metrics.blockedRatioMean:P1}",
            errors);
        Require(
            metrics.mealsPerDay >= 1f
            && metrics.mealsPerDay <= 1.5f,
            $"식사 빈도 {metrics.mealsPerDay:0.00}/일",
            errors);
        Require(
            metrics.drinksPerDay >= 1f
            && metrics.drinksPerDay <= 1.5f,
            $"음수 빈도 {metrics.drinksPerDay:0.00}/일",
            errors);
        Require(
            metrics.restsPerDay >= 0.7f
            && metrics.restsPerDay <= 1.2f,
            $"휴식 빈도 {metrics.restsPerDay:0.00}/일",
            errors);
        Require(
            metrics.toiletsPerDay >= 0.6f
            && metrics.toiletsPerDay <= 1.0f,
            $"배변 빈도 {metrics.toiletsPerDay:0.00}/일",
            errors);
        Require(
            metrics.hygienePerDay >= 0.6f
            && metrics.hygienePerDay <= 1.0f,
            $"위생 빈도 {metrics.hygienePerDay:0.00}/일",
            errors);
    }

    private static void ValidateTimeScaleDeterminism(
        NeedBalanceCandidate candidate,
        List<string> errors)
    {
        SpeciesProbe slime = Species[0];
        NeedBalanceAgentMetrics x1 = SimulateAgent(
            candidate,
            DungeonSurvivalPressure.Standard,
            NeedSupplyScenario.Sufficient,
            slime,
            10,
            117,
            2,
            1f);
        NeedBalanceAgentMetrics x5 = SimulateAgent(
            candidate,
            DungeonSurvivalPressure.Standard,
            NeedSupplyScenario.Sufficient,
            slime,
            10,
            117,
            2,
            0.2f);
        Require(
            Mathf.Abs(x1.minimumNeed - x5.minimumNeed) <= 0.5f,
            $"X1/X5 욕구 오차 {Mathf.Abs(x1.minimumNeed - x5.minimumNeed):0.###}",
            errors);
        Require(
            x1.TotalUses == x5.TotalUses,
            $"X1/X5 이용 횟수 불일치 {x1.TotalUses}/{x5.TotalUses}",
            errors);
    }

    private static void ValidateGolemExclusion(List<string> errors)
    {
        NeedBalanceAgentMetrics golem = SimulateAgent(
            NeedBalanceCandidate.Standard,
            DungeonSurvivalPressure.Standard,
            NeedSupplyScenario.Sufficient,
            Species.Last(),
            10,
            1,
            0,
            1f);
        Require(
            golem.meals == 0 && golem.drinks == 0,
            "Golem이 생물학적 식사·음수를 실행했습니다.",
            errors);
    }

    private static void Require(
        bool condition,
        string message,
        List<string> errors)
    {
        if (!condition)
        {
            errors.Add(message);
        }
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            for (int index = 0; index < (value?.Length ?? 0); index++)
            {
                hash = hash * 31 + value[index];
            }

            return hash;
        }
    }

    private static readonly CharacterCondition[] BalancedConditions =
    {
        CharacterCondition.HUNGER,
        CharacterCondition.THIRST,
        CharacterCondition.SLEEP,
        CharacterCondition.EXCRETION,
        CharacterCondition.HYGIENE
    };

    private readonly struct SpeciesProbe
    {
        public SpeciesProbe(
            string id,
            float hunger,
            float thirst,
            bool biological)
        {
            this.id = id;
            this.hunger = hunger;
            this.thirst = thirst;
            this.biological = biological;
        }

        public readonly string id;
        public readonly float hunger;
        public readonly float thirst;
        public readonly bool biological;
    }
}

public enum NeedSupplyScenario
{
    Sufficient,
    Crowded,
    FoodCut,
    WaterCut,
    RestDestroyed
}

[Serializable]
public struct NeedBalanceCandidate
{
    public float hungerPerDay;
    public float thirstPerDay;
    public float excretionPerDay;
    public float hygienePerDay;
    public float workSleepPerSecond;
    public float workExcretionPerSecond;
    public float workHygienePerSecond;

    public float TotalDepletion =>
        hungerPerDay + thirstPerDay + excretionPerDay + hygienePerDay
        + workSleepPerSecond + workExcretionPerSecond
        + workHygienePerSecond;

    public static NeedBalanceCandidate Standard =>
        new NeedBalanceCandidate
        {
            hungerPerDay = 50f,
            thirstPerDay = 60f,
            excretionPerDay = 24f,
            hygienePerDay = 18f,
            workSleepPerSecond = 0.35f,
            workExcretionPerSecond = 0.05f,
            workHygienePerSecond = 0.06f
        };

    public NeedBalanceCandidate WithDimension(int dimension, float value)
    {
        NeedBalanceCandidate copy = this;
        switch (dimension)
        {
            case 0: copy.hungerPerDay = value; break;
            case 1: copy.thirstPerDay = value; break;
            case 2: copy.excretionPerDay = value; break;
            case 3: copy.hygienePerDay = value; break;
            case 4: copy.workSleepPerSecond = value; break;
            case 5: copy.workExcretionPerSecond = value; break;
            case 6: copy.workHygienePerSecond = value; break;
        }

        return copy;
    }
}

[Serializable]
public sealed class NeedBalanceCalibrationReport
{
    public NeedBalanceCandidate initial;
    public NeedBalanceCandidate selected;
    public NeedBalanceAggregate standardSufficient;
    public List<NeedBalanceAggregate> aggregates =
        new List<NeedBalanceAggregate>();
    public List<string> errors = new List<string>();
}

[Serializable]
public sealed class NeedBalanceAggregate
{
    public DungeonSurvivalPressure pressure;
    public NeedSupplyScenario supply;
    public int agentCount;
    public float lifeRatioMean;
    public float lifeRatioP95;
    public float workRatioMean;
    public float blockedRatioMean;
    public float queueRatioMean;
    public float minimumNeedP10;
    public float mealsPerDay;
    public float drinksPerDay;
    public float restsPerDay;
    public float toiletsPerDay;
    public float hygienePerDay;
    public float interruptionsPerDay;
    public float deprivationDamageCount;
    public float breakdownCount;
    public float speciesOutcomeSpread;

    public static NeedBalanceAggregate From(
        DungeonSurvivalPressure pressure,
        NeedSupplyScenario supply,
        IReadOnlyList<NeedBalanceAgentMetrics> metrics)
    {
        float[] life = metrics.Select(value => value.lifeRatio)
            .OrderBy(value => value).ToArray();
        float[] minimum = metrics.Select(value => value.minimumNeed)
            .OrderBy(value => value).ToArray();
        Dictionary<string, float> speciesWork = metrics
            .GroupBy(value => value.speciesId)
            .ToDictionary(
                group => group.Key,
                group => group.Average(value => value.workRatio));
        float spread = speciesWork.Count > 0
            ? speciesWork.Values.Max() - speciesWork.Values.Min()
            : 0f;
        return new NeedBalanceAggregate
        {
            pressure = pressure,
            supply = supply,
            agentCount = metrics.Count,
            lifeRatioMean = Average(metrics, value => value.lifeRatio),
            lifeRatioP95 = Percentile(life, 0.95f),
            workRatioMean = Average(metrics, value => value.workRatio),
            blockedRatioMean = Average(metrics, value => value.blockedRatio),
            queueRatioMean = Average(metrics, value => value.queueRatio),
            minimumNeedP10 = Percentile(minimum, 0.10f),
            mealsPerDay = Average(
                metrics.Where(value => value.biological).ToArray(),
                value => value.meals / 5f),
            drinksPerDay = Average(
                metrics.Where(value => value.biological).ToArray(),
                value => value.drinks / 5f),
            restsPerDay = Average(metrics, value => value.rests / 5f),
            toiletsPerDay = Average(
                metrics.Where(value => value.biological).ToArray(),
                value => value.toilets / 5f),
            hygienePerDay = Average(
                metrics,
                value => value.hygieneUses / 5f),
            interruptionsPerDay = Average(
                metrics,
                value => value.interruptions / 5f),
            deprivationDamageCount =
                metrics.Sum(value => value.damageTicks),
            breakdownCount =
                metrics.Sum(value => value.breakdownSeconds > 30f ? 1f : 0f),
            speciesOutcomeSpread = spread
        };
    }

    private static float Average(
        IReadOnlyList<NeedBalanceAgentMetrics> metrics,
        Func<NeedBalanceAgentMetrics, float> selector)
    {
        return metrics.Count > 0 ? metrics.Average(selector) : 0f;
    }

    private static float Percentile(float[] values, float percentile)
    {
        if (values == null || values.Length == 0)
        {
            return 0f;
        }

        int index = Mathf.Clamp(
            Mathf.CeilToInt((values.Length - 1) * percentile),
            0,
            values.Length - 1);
        return values[index];
    }
}

public sealed class NeedBalanceAgentMetrics
{
    public string speciesId;
    public bool biological;
    public float lifeRatio;
    public float workRatio;
    public float blockedRatio;
    public float queueRatio;
    public float minimumNeed;
    public int meals;
    public int drinks;
    public int rests;
    public int toilets;
    public int hygieneUses;
    public int interruptions;
    public float damageTicks;
    public float breakdownSeconds;
    public int TotalUses => meals + drinks + rests + toilets + hygieneUses;
}

internal sealed class NeedBalanceAgentState
{
    public float hunger = 100f;
    public float thirst = 100f;
    public float sleep = 100f;
    public float excretion = 100f;
    public float hygiene = 100f;
    public float minimumNeed = 100f;
    public float actionRemaining;
    public CharacterCondition? activeNeed;
    public float lifeSeconds;
    public float workSeconds;
    public float blockedSeconds;
    public float queueSeconds;
    public int meals;
    public int drinks;
    public int rests;
    public int toilets;
    public int hygieneUses;
    public int interruptions;
    public float damageTicks;
    public float breakdownSeconds;

    public void ClampNeeds()
    {
        hunger = Mathf.Clamp(hunger, 0f, 100f);
        thirst = Mathf.Clamp(thirst, 0f, 100f);
        sleep = Mathf.Clamp(sleep, 0f, 100f);
        excretion = Mathf.Clamp(excretion, 0f, 100f);
        hygiene = Mathf.Clamp(hygiene, 0f, 100f);
    }

    public NeedBalanceAgentMetrics ToMetrics(
        string speciesId,
        bool biological,
        float duration)
    {
        float safeDuration = Mathf.Max(1f, duration);
        return new NeedBalanceAgentMetrics
        {
            speciesId = speciesId,
            biological = biological,
            lifeRatio = lifeSeconds / safeDuration,
            workRatio = workSeconds / safeDuration,
            blockedRatio = blockedSeconds / safeDuration,
            queueRatio = queueSeconds / safeDuration,
            minimumNeed = minimumNeed,
            meals = meals,
            drinks = drinks,
            rests = rests,
            toilets = toilets,
            hygieneUses = hygieneUses,
            interruptions = interruptions,
            damageTicks = damageTicks,
            breakdownSeconds = breakdownSeconds
        };
    }
}
