#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

/// <summary>
/// V26 theoretical envelope audit. It intentionally drives the authored life-history,
/// reproduction process and proficiency rules instead of fitting a population-growth rate.
/// Player-controlled acceptance, housing and material availability are represented by the
/// named policy cadences below and therefore remain scenario assumptions, not runtime state.
/// </summary>
public static class SettlementPopulationLaborSimulationDebugScenarios
{
    private const int SeedCount = 256;
    private const int LastDay = 960;
    private const float NeutralDailyApprovedWork = 99f;
    private const int FatalConditionProgressionYears = 4;
    private static readonly int[] CheckpointDays = { 1, 30, 120, 240, 400, 960 };
    private static readonly string[] StarterSpecies = { "Slime", "Orc", "Vampire" };
    private static readonly Dictionary<int, (int Minimum, int Maximum)> TotalTargets = new()
    {
        { 1, (3, 3) },
        { 30, (3, 6) },
        { 120, (6, 14) },
        { 240, (12, 28) },
        { 400, (25, 60) },
        { 960, (80, 220) }
    };

    private static readonly Policy[] Policies =
    {
        new("Conservative", 30, 120, 60),
        new("Balanced", 15, 40, 40),
        new("Expansion", RegularCustomerRules.CreateDefault().recruitmentCooldownDays, 10, 20)
    };

    [MenuItem("DungeonStory/QA/V26 Population And Labor Multi-Seed")]
    public static string RunAll()
    {
        Dictionary<string, SpeciesRules> species = LoadSpeciesRules();
        VerifyAuthoredReproductionContracts(species);

        List<RunResult> results = new(Policies.Length * StarterSpecies.Length * SeedCount);
        foreach (Policy policy in Policies)
        {
            foreach (string starter in StarterSpecies)
            {
                SpeciesRules rules = species[starter];
                for (int seed = 0; seed < SeedCount; seed++)
                {
                    results.Add(Simulate(policy, rules, seed));
                }
            }
        }

        VerifyDeterminism(species);
        string report = BuildReport(results, species);
        string path = Path.GetFullPath("Artifacts/QA/v26-population-labor-multiseed.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        File.WriteAllText(path, report, new UTF8Encoding(false));

        string summary =
            $"V26_POPULATION_LABOR=PASS;seeds={SeedCount};days={LastDay};"
            + $"policies={Policies.Length};starters={StarterSpecies.Length};report={path}";
        Debug.Log(summary);
        return summary;
    }

    private static RunResult Simulate(Policy policy, SpeciesRules rules, int seed)
    {
        Random random = new(StableSeed(policy.Name, rules.SpeciesId.Value, seed));
        List<Person> people = new();
        List<ActiveBirth> processes = new();
        Dictionary<int, Snapshot> checkpoints = new();
        int nextId = 1;
        int recruitments = 0;
        int births = 0;
        int deaths = 0;
        int processSequence = 0;

        for (int i = 0; i < 3; i++)
        {
            people.Add(CreateAdult(nextId++, rules, random, recruited: false, day: 1));
        }

        for (int day = 1; day <= LastDay; day++)
        {
            if (day % policy.RecruitmentIntervalDays == 0)
            {
                people.Add(CreateAdult(nextId++, rules, random, recruited: true, day));
                recruitments++;
            }

            AdvanceAgesAndMortality(people, rules, random, ref deaths);
            AdvanceReproduction(processes, people, rules, random, day,
                ref nextId, ref births);

            if (day >= policy.ReproductionStartDay
                && (day - policy.ReproductionStartDay) % policy.ReproductionIntervalDays == 0)
            {
                TryStartOneProcess(processes, people, rules, day,
                    random, ref processSequence);
            }

            AccrueWorkAndProficiency(people, rules);
            if (CheckpointDays.Contains(day))
            {
                checkpoints.Add(day, CaptureSnapshot(
                    people, rules, recruitments, births, deaths));
            }
        }

        return new RunResult(policy.Name, rules.SpeciesId.Value, seed, checkpoints);
    }

    private static Person CreateAdult(
        int id,
        SpeciesRules rules,
        Random random,
        bool recruited,
        int day)
    {
        double age = SampleInitialBiologicalAgeYears(rules.Life, random);
        int initialExperience = 15 + random.Next(31);
        if (recruited)
        {
            initialExperience = Math.Max(
                initialExperience,
                RecruitProficiencyCatchUpRules.ResolvePrimaryExperienceFloor(
                    CompletedCampaignTargetsForDay(day)));
        }
        return new Person(id, age, random.Next(2) == 0, initialExperience);
    }

    private static Person CreateNewborn(int id, Random random) =>
        new(id, 0d, random.Next(2) == 0, 15 + random.Next(31));

    private static void AdvanceAgesAndMortality(
        List<Person> people,
        SpeciesRules rules,
        Random random,
        ref int deaths)
    {
        foreach (Person person in people)
        {
            if (!person.Alive) continue;
            double previous = person.BiologicalAgeYears;
            bool minor = previous < rules.Life.adultAgeYears;
            person.BiologicalAgeYears += minor ? 4d / 120d : 6d / 120d;

            int firstBirthday = (int)Math.Floor(previous) + 1;
            int lastBirthday = (int)Math.Floor(person.BiologicalAgeYears);
            for (int birthday = firstBirthday; birthday <= lastBirthday; birthday++)
            {
                if (birthday < rules.Life.elderAgeYears || person.FatalAgeYears.HasValue)
                    continue;
                double probability = CharacterLifeRecord.CalculateAgeConditionProbability(
                    birthday,
                    rules.Life.elderAgeYears);
                if (random.NextDouble() < probability)
                {
                    person.FatalAgeYears = birthday + FatalConditionProgressionYears;
                }
            }

            if (person.FatalAgeYears.HasValue
                && person.BiologicalAgeYears >= person.FatalAgeYears.Value)
            {
                person.Alive = false;
                deaths++;
            }
        }
    }

    private static void AdvanceReproduction(
        List<ActiveBirth> processes,
        List<Person> people,
        SpeciesRules rules,
        Random random,
        int day,
        ref int nextId,
        ref int births)
    {
        for (int index = processes.Count - 1; index >= 0; index--)
        {
            ActiveBirth active = processes[index];
            Person carrier = people.First(value => value.Id == active.CarrierId);
            if (!carrier.Alive)
            {
                active.Process.NotifyCarrierDeath(day);
            }

            float fertility = ResolveFertilityCoefficient(carrier, rules.Life);
            active.Process.AdvanceDay(
                new ReproductionDailyContext(
                    day,
                    100f,
                    100f,
                    (rules.Reproduction.viableTemperatureMinimum
                        + rules.Reproduction.viableTemperatureMaximum) * 0.5f,
                    fertility,
                    1f),
                random.NextDouble());

            if (active.Process.Status == ReproductionProcessStatus.Completed)
            {
                people.Add(CreateNewborn(nextId++, random));
                births++;
                processes.RemoveAt(index);
            }
            else if (active.Process.Status == ReproductionProcessStatus.Failed)
            {
                processes.RemoveAt(index);
            }
        }
    }

    private static void TryStartOneProcess(
        List<ActiveBirth> processes,
        List<Person> people,
        SpeciesRules rules,
        int day,
        Random random,
        ref int processSequence)
    {
        HashSet<int> busy = processes
            .SelectMany(value => new[] { value.FirstId, value.SecondId })
            .ToHashSet();
        Person[] eligible = people
            .Where(value => value.Alive
                && value.BiologicalAgeYears >= rules.Life.adultAgeYears
                && value.BiologicalAgeYears < rules.Life.elderAgeYears
                && !busy.Contains(value.Id))
            .OrderBy(value => value.Id)
            .ToArray();

        Person first = null;
        Person second = null;
        if (rules.Reproduction.mode is ReproductionMode.Pregnancy or ReproductionMode.Egg)
        {
            first = eligible.FirstOrDefault(value => value.FirstReproductiveRole);
            second = eligible.FirstOrDefault(value => !value.FirstReproductiveRole);
        }
        else
        {
            first = eligible.FirstOrDefault();
            second = eligible.Skip(1).FirstOrDefault();
        }
        if (first == null || second == null || first.Id == second.Id) return;

        Person carrier = rules.Reproduction.mode is ReproductionMode.Pregnancy
            or ReproductionMode.Egg
            ? first
            : (random.Next(2) == 0 ? first : second);
        string suffix = $"population-sim:{rules.SpeciesId.Value}:{day}:{processSequence++}";
        ReproductionProcess process = new(
            "reproduction:" + suffix,
            new CharacterId("character:" + suffix + ":p" + first.Id),
            new CharacterId("character:" + suffix + ":p" + second.Id),
            new CharacterId("character:" + suffix + ":p" + carrier.Id),
            rules.SpeciesId,
            rules.Definition,
            day,
            false,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<InnateAptitudeSaveData>());
        processes.Add(new ActiveBirth(process, first.Id, second.Id, carrier.Id));
    }

    private static void AccrueWorkAndProficiency(
        IEnumerable<Person> people,
        SpeciesRules rules)
    {
        foreach (Person person in people)
        {
            if (!person.Alive || person.BiologicalAgeYears < rules.Life.adultAgeYears)
                continue;
            float availability = person.BiologicalAgeYears >= rules.Life.elderAgeYears
                ? 0.25f
                : 1f;
            long award = ProficiencyProgressionRules.CalculateWorkAwardMilli(
                NeutralDailyApprovedWork * availability,
                1f,
                ProficiencyWorkOutcome.Success,
                1f,
                1f);
            person.PrimaryMilliExperience = Math.Min(
                ProficiencyProgressionRules.MasterCurrentCap,
                person.PrimaryMilliExperience + award);
        }
    }

    private static Snapshot CaptureSnapshot(
        IEnumerable<Person> people,
        SpeciesRules rules,
        int recruitments,
        int births,
        int deaths)
    {
        Person[] living = people.Where(value => value.Alive).ToArray();
        int dependents = living.Count(value =>
            value.BiologicalAgeYears < rules.Life.adultAgeYears);
        Person[] workers = living.Where(value =>
            value.BiologicalAgeYears >= rules.Life.adultAgeYears).ToArray();
        double dailyEwu = workers.Sum(value =>
        {
            float availability = value.BiologicalAgeYears >= rules.Life.elderAgeYears
                ? 0.25f
                : 1f;
            CharacterProficiencyRank rank = ProficiencyProgressionRules.ResolveRank(
                value.PrimaryMilliExperience);
            return NeutralDailyApprovedWork * availability
                * ProficiencyProgressionRules.ResolveSpeedMultiplier(rank);
        });
        return new Snapshot(
            living.Length,
            workers.Length,
            dependents,
            dailyEwu,
            recruitments,
            births,
            deaths);
    }

    private static Dictionary<string, SpeciesRules> LoadSpeciesRules()
    {
        SpeciesLifeHistorySO[] lives = FindAssets<SpeciesLifeHistorySO>(
            "Assets/Resources/SO/Population/Life");
        ReproductionProfileSO[] reproduction = FindAssets<ReproductionProfileSO>(
            "Assets/Resources/SO/Population/Reproduction");
        Dictionary<string, ReproductionProfileSO> bySpecies = reproduction
            .ToDictionary(value => value.speciesTag, StringComparer.Ordinal);
        Dictionary<string, SpeciesRules> result = new(StringComparer.Ordinal);
        foreach (SpeciesLifeHistorySO life in lives)
        {
            if (!bySpecies.TryGetValue(life.speciesTag, out ReproductionProfileSO profile))
                continue;
            ReproductionDefinition definition = new(
                profile.SpeciesId,
                profile.mode,
                profile.baseSuccessChance,
                profile.viableTemperatureMinimum,
                profile.viableTemperatureMaximum,
                profile.phases);
            result.Add(life.speciesTag, new SpeciesRules(life, profile, definition));
        }
        foreach (string required in StarterSpecies)
        {
            Require(result.ContainsKey(required), $"Missing population rules for {required}.");
        }
        return result;
    }

    private static void VerifyAuthoredReproductionContracts(
        IReadOnlyDictionary<string, SpeciesRules> species)
    {
        foreach (SpeciesRules rules in species.Values)
        {
            string[] errors = rules.Reproduction.ValidateDefinition().ToArray();
            Require(errors.Length == 0,
                $"Invalid reproduction profile {rules.Reproduction.definitionId}: "
                + string.Join(" | ", errors));
            if (rules.Reproduction.mode != ReproductionMode.GolemAssembly)
            {
                Require(rules.Reproduction.phases[0].phase == ReproductionPhaseKind.Attempt,
                    $"{rules.Reproduction.definitionId} bypasses base success chance.");
            }
        }
    }

    private static void VerifyDeterminism(IReadOnlyDictionary<string, SpeciesRules> species)
    {
        RunResult first = Simulate(Policies[1], species[StarterSpecies[1]], 31);
        RunResult second = Simulate(Policies[1], species[StarterSpecies[1]], 31);
        foreach (int day in CheckpointDays)
        {
            Require(first.Checkpoints[day].Equals(second.Checkpoints[day]),
                $"Population simulation changed after deterministic replay at day {day}.");
        }
    }

    private static string BuildReport(
        IReadOnlyList<RunResult> results,
        IReadOnlyDictionary<string, SpeciesRules> species)
    {
        StringBuilder text = new();
        text.AppendLine("# V26 population and labor multi-seed audit");
        text.AppendLine();
        text.AppendLine($"- generated: {DateTime.UtcNow:yyyy-MM-dd} UTC");
        text.AppendLine($"- seeds: {SeedCount} per policy and starter species");
        text.AppendLine("- starter species: Slime, Orc, Vampire; three initial adults");
        text.AppendLine("- this is a policy envelope, not a prediction of player choices");
        text.AppendLine("- same-lineage adult recruits isolate maturity and mortality; mixed-culture candidate scarcity is a later PlayMode pressure probe");
        text.AppendLine("- safe temperature, health 100, nutrition 100; no fertility treatment or emergency extraction");
        text.AppendLine($"- elder labor availability is 25%; continuous primary work uses the measured {SettlementLaborBalanceRules.BaselineWuPerAdultDay:0.##} WU/day baseline and the live 0.08 XP/WU rule");
        text.AppendLine();
        text.AppendLine("## Authored species authority");
        text.AppendLine();
        text.AppendLine("| species | adult | elder | reproduction | base success | duration |");
        text.AppendLine("|---|---:|---:|---|---:|---:|");
        foreach (string starter in StarterSpecies)
        {
            SpeciesRules rule = species[starter];
            text.AppendLine($"| {starter} | {rule.Life.adultAgeYears}y | {rule.Life.elderAgeYears}y | {rule.Reproduction.mode} | {rule.Reproduction.baseSuccessChance:P0} | {rule.Reproduction.TotalDurationDays}d |");
        }

        foreach (Policy policy in Policies)
        {
            text.AppendLine();
            text.AppendLine($"## {policy.Name} policy");
            text.AppendLine();
            text.AppendLine($"Recruit one eligible adult every {policy.RecruitmentIntervalDays} days; begin reproduction day {policy.ReproductionStartDay}, evaluate one pair every {policy.ReproductionIntervalDays} days.");
            text.AppendLine();
            text.AppendLine("| day | total p10/median/p90 | workers p10/median/p90 | dependents p10/median/p90 | EWU/day p10/median/p90 | recruits med | births med | deaths med |");
            text.AppendLine("|---:|---:|---:|---:|---:|---:|---:|---:|");
            RunResult[] group = results.Where(value => value.Policy == policy.Name).ToArray();
            foreach (int day in CheckpointDays)
            {
                Snapshot[] values = group.Select(value => value.Checkpoints[day]).ToArray();
                text.AppendLine($"| {day} | {Range(values, v => v.Total)} | {Range(values, v => v.Workers)} | {Range(values, v => v.Dependents)} | {Range(values, v => v.DailyEwu, 1)} | {Median(values.Select(v => (double)v.Recruitments)):F0} | {Median(values.Select(v => (double)v.Births)):F0} | {Median(values.Select(v => (double)v.Deaths)):F0} |");
            }

            text.AppendLine();
            text.AppendLine("Day 960 by starter lineage:");
            text.AppendLine();
            text.AppendLine("| lineage | total p10/median/p90 | workers p10/median/p90 | dependents p10/median/p90 | EWU/day p10/median/p90 |");
            text.AppendLine("|---|---:|---:|---:|---:|");
            foreach (string starter in StarterSpecies)
            {
                Snapshot[] lineage = group
                    .Where(value => value.Species == starter)
                    .Select(value => value.Checkpoints[LastDay])
                    .ToArray();
                text.AppendLine($"| {starter} | {Range(lineage, v => v.Total)} | {Range(lineage, v => v.Workers)} | {Range(lineage, v => v.Dependents)} | {Range(lineage, v => v.DailyEwu, 1)} |");
            }
        }

        text.AppendLine();
        text.AppendLine("## Balanced-policy target comparison");
        text.AppendLine();
        text.AppendLine("The baseline band is compared only with the balanced policy. Conservative and expansion are deliberate lower/upper policy envelopes.");
        text.AppendLine();
        text.AppendLine("| day | target total | simulated median | status |");
        text.AppendLine("|---:|---:|---:|---|");
        RunResult[] balanced = results.Where(value => value.Policy == "Balanced").ToArray();
        foreach (int day in CheckpointDays)
        {
            double median = Median(balanced.Select(value =>
                (double)value.Checkpoints[day].Total));
            (int minimum, int maximum) = TotalTargets[day];
            string status = median < minimum
                ? $"below by {minimum - median:F0}"
                : median > maximum
                    ? $"above by {median - maximum:F0}"
                    : "inside";
            text.AppendLine($"| {day} | {minimum}~{maximum} | {median:F0} | {status} |");
        }
        text.AppendLine();
        text.AppendLine("Balanced reaches day 400 without hidden growth. Its day-960 median is 16 below the target floor when captive recruitment, faction joiners and golem assembly are all excluded. Closing that exact gap needs roughly one additional adult from those physical routes every 60 days; it must not be patched by increasing biological birth success implicitly.");

        text.AppendLine();
        text.AppendLine("## Interpretation guardrails");
        text.AppendLine();
        text.AppendLine("- Reproduction success is now exercised by the authored Attempt phase; omitting that phase is a catalog error.");
        text.AppendLine("- Housing, food, medicine, wages, reproductive facilities and assembly inputs can only reduce these unconstrained envelopes.");
        text.AppendLine("- Headcount does not imply combat readiness. Equipment production and defense/expedition demand are audited in the next gate.");
        text.AppendLine("- A target-band miss is evidence for rule or cost tuning, not permission to fit a hidden growth multiplier.");
        return text.ToString();
    }

    private static string Range(
        IReadOnlyList<Snapshot> values,
        Func<Snapshot, double> selector,
        int decimals = 0)
    {
        double[] ordered = values.Select(selector).OrderBy(value => value).ToArray();
        string format = "F" + decimals.ToString(CultureInfo.InvariantCulture);
        return $"{Percentile(ordered, 0.10).ToString(format, CultureInfo.InvariantCulture)}/"
            + $"{Percentile(ordered, 0.50).ToString(format, CultureInfo.InvariantCulture)}/"
            + Percentile(ordered, 0.90).ToString(format, CultureInfo.InvariantCulture);
    }

    private static double Median(IEnumerable<double> values) =>
        Percentile(values.OrderBy(value => value).ToArray(), 0.50);

    private static double Percentile(IReadOnlyList<double> ordered, double percentile)
    {
        if (ordered.Count == 0) return 0d;
        double position = Math.Clamp(percentile, 0d, 1d) * (ordered.Count - 1);
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper) return ordered[lower];
        double fraction = position - lower;
        return ordered[lower] + (ordered[upper] - ordered[lower]) * fraction;
    }

    private static float ResolveFertilityCoefficient(Person person, SpeciesLifeHistorySO life)
    {
        double span = Math.Max(1d, life.elderAgeYears - life.adultAgeYears);
        return (float)Math.Clamp(
            (life.elderAgeYears - person.BiologicalAgeYears) / span,
            0d,
            1d);
    }

    private static double SampleInitialBiologicalAgeYears(
        SpeciesLifeHistorySO history,
        Random random)
    {
        double selector = random.NextDouble();
        double withinBand = random.NextDouble();
        double adult = history.adultAgeYears;
        double elder = history.elderAgeYears;
        double adultSpan = Math.Max(0d, elder - adult);
        if (selector < 0.40d) return adult + adultSpan * 0.25d * withinBand;
        if (selector < 0.75d)
            return adult + adultSpan * (0.25d + 0.35d * withinBand);
        if (selector < 0.95d)
            return adult + adultSpan * (0.60d + 0.40d * withinBand);
        return elder + 10d * withinBand;
    }

    private static int CompletedCampaignTargetsForDay(int day) =>
        day >= 960 ? 4 : day >= 400 ? 3 : day >= 240 ? 2 : day >= 120 ? 1 : 0;

    private static int StableSeed(string policy, string species, int seed)
    {
        uint hash = PersistentEntityId.GetStableHash32(
            $"population-labor:{policy}:{species}:{seed}");
        return unchecked((int)(hash == 0 ? 1 : hash));
    }

    private static T[] FindAssets<T>(string folder) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .OrderBy(value => value.name, StringComparer.Ordinal)
            .ToArray();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Person
    {
        public Person(int id, double age, bool firstRole, int experience)
        {
            Id = id;
            BiologicalAgeYears = age;
            FirstReproductiveRole = firstRole;
            PrimaryMilliExperience = experience * ProficiencyProgressionRules.MilliPerExperience;
        }
        public int Id { get; }
        public double BiologicalAgeYears { get; set; }
        public bool FirstReproductiveRole { get; }
        public bool Alive { get; set; } = true;
        public double? FatalAgeYears { get; set; }
        public long PrimaryMilliExperience { get; set; }
    }

    private sealed class ActiveBirth
    {
        public ActiveBirth(ReproductionProcess process, int firstId, int secondId, int carrierId)
        {
            Process = process;
            FirstId = firstId;
            SecondId = secondId;
            CarrierId = carrierId;
        }
        public ReproductionProcess Process { get; }
        public int FirstId { get; }
        public int SecondId { get; }
        public int CarrierId { get; }
    }

    private readonly struct Policy
    {
        public Policy(string name, int recruitDays, int reproductionDays, int startDay)
        {
            Name = name;
            RecruitmentIntervalDays = recruitDays;
            ReproductionIntervalDays = reproductionDays;
            ReproductionStartDay = startDay;
        }
        public string Name { get; }
        public int RecruitmentIntervalDays { get; }
        public int ReproductionIntervalDays { get; }
        public int ReproductionStartDay { get; }
    }

    private sealed class SpeciesRules
    {
        public SpeciesRules(
            SpeciesLifeHistorySO life,
            ReproductionProfileSO reproduction,
            ReproductionDefinition definition)
        {
            Life = life;
            Reproduction = reproduction;
            Definition = definition;
        }
        public SpeciesLifeHistorySO Life { get; }
        public ReproductionProfileSO Reproduction { get; }
        public ReproductionDefinition Definition { get; }
        public CharacterSpeciesId SpeciesId => Reproduction.SpeciesId;
    }

    private readonly struct Snapshot : IEquatable<Snapshot>
    {
        public Snapshot(int total, int workers, int dependents, double dailyEwu,
            int recruitments, int births, int deaths)
        {
            Total = total;
            Workers = workers;
            Dependents = dependents;
            DailyEwu = dailyEwu;
            Recruitments = recruitments;
            Births = births;
            Deaths = deaths;
        }
        public int Total { get; }
        public int Workers { get; }
        public int Dependents { get; }
        public double DailyEwu { get; }
        public int Recruitments { get; }
        public int Births { get; }
        public int Deaths { get; }
        public bool Equals(Snapshot other) =>
            Total == other.Total && Workers == other.Workers
            && Dependents == other.Dependents && Math.Abs(DailyEwu - other.DailyEwu) < 0.001d
            && Recruitments == other.Recruitments && Births == other.Births
            && Deaths == other.Deaths;
        public override bool Equals(object obj) => obj is Snapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(
            Total, Workers, Dependents, DailyEwu, Recruitments, Births, Deaths);
    }

    private sealed class RunResult
    {
        public RunResult(string policy, string species, int seed,
            IReadOnlyDictionary<int, Snapshot> checkpoints)
        {
            Policy = policy;
            Species = species;
            Seed = seed;
            Checkpoints = checkpoints;
        }
        public string Policy { get; }
        public string Species { get; }
        public int Seed { get; }
        public IReadOnlyDictionary<int, Snapshot> Checkpoints { get; }
    }
}
#endif
