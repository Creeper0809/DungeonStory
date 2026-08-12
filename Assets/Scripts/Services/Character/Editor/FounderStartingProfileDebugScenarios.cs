using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class FounderStartingProfileDebugScenarios
{
    private const double ConditionSelectionPenalty = 0.05d;

    private static readonly CharacterProficiencyId[] SettlementEssentials =
    {
        BuiltInCharacterProficiencyIds.Fieldwork,
        BuiltInCharacterProficiencyIds.FoodProduction,
        BuiltInCharacterProficiencyIds.ConstructionEngineering,
        BuiltInCharacterProficiencyIds.Crafting
    };

    public static string Run()
    {
        VerifySubgradeBoundaries();
        VerifyAgeCaps();

        CharacterStartingOriginSO[] origins = LoadAll<CharacterStartingOriginSO>();
        CharacterStartingHistorySO[] histories = LoadAll<CharacterStartingHistorySO>();
        SpeciesLifeHistorySO life = LoadAll<SpeciesLifeHistorySO>()
            .Single(value => string.Equals(
                value.speciesTag,
                "Adventurer",
                StringComparison.Ordinal));
        AgeConditionDefinitionSO[] conditions = LoadAll<AgeConditionDefinitionSO>();
        if (origins.Length != 6 || histories.Length != 9)
        {
            throw new InvalidOperationException(
                $"Expected 6 origins and 9 histories, got {origins.Length}/{histories.Length}.");
        }
        foreach (CharacterStartingOriginSO origin in origins)
        {
            RequireValid(origin.ValidateDefinition(), origin.name);
        }
        foreach (CharacterStartingHistorySO history in histories)
        {
            RequireValid(history.ValidateDefinition(), history.name);
        }

        string[] primaryCoverage = histories
            .Select(value => value.primaryProficiencyId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] requiredCoverage = BuiltInCharacterProficiencyIds.All
            .Select(value => value.Value)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!primaryCoverage.SequenceEqual(requiredCoverage))
        {
            throw new InvalidOperationException(
                "Authored histories do not cover each built-in proficiency exactly once as primary.");
        }

        CharacterStartingLifeHistory startingLife = new(
            life.adultAgeYears,
            life.elderAgeYears,
            life.untreatedExpectedLifeYears,
            life.construct);
        CharacterStartingAgeCondition[] startingConditions = conditions
            .Select(value => new CharacterStartingAgeCondition(
                value.conditionId,
                value.constructCondition))
            .ToArray();
        long primaryTotal = 0L;
        long secondaryTotal = 0L;
        long unrelatedTotal = 0L;
        long unrelatedCount = 0L;
        double primarySpeedTotal = 0d;
        double secondarySpeedTotal = 0d;
        double unrelatedSpeedTotal = 0d;
        int[] ageBandCounts = new int[4];
        long[] primaryByAgeBand = new long[4];
        long[] secondaryByAgeBand = new long[4];
        int initialConditionProfiles = 0;
        int multipleConditionProfiles = 0;
        int samples = 0;

        foreach (CharacterStartingHistorySO history in histories)
        {
            for (int seed = 1; seed <= 2000; seed++)
            {
                int combinedSeed = CharacterGrowthRules.StableHash(
                    $"founder-audit:{history.historyId}:{seed}");
                CharacterStartingProfileRoll first =
                    CharacterStartingProfileRules.Create(
                        combinedSeed,
                        startingLife,
                        origins[seed % origins.Length],
                        history,
                        startingConditions);
                CharacterStartingProfileRoll second =
                    CharacterStartingProfileRules.Create(
                        combinedSeed,
                        startingLife,
                        origins[seed % origins.Length],
                        history,
                        startingConditions);
                VerifyDeterministic(first, second);
                VerifyProfile(first);
                ageBandCounts[(int)first.Profile.ageBand]++;
                int ageBandIndex = (int)first.Profile.ageBand;
                if (first.Profile.initialAgeConditionIds.Count > 0)
                    initialConditionProfiles++;
                if (first.Profile.initialAgeConditionIds.Count > 1)
                    multipleConditionProfiles++;

                foreach (CharacterStartingProficiencyExperience value in
                             first.Proficiencies)
                {
                    if (value.proficiencyId == history.primaryProficiencyId)
                    {
                        primaryTotal += value.experience;
                        primaryByAgeBand[ageBandIndex] += value.experience;
                        primarySpeedTotal += ProficiencyProgressionRules
                            .ResolveEffects(value.experience
                                * ProficiencyProgressionRules.MilliPerExperience)
                            .WorkSpeedMultiplier;
                    }
                    else if (value.proficiencyId == history.secondaryProficiencyId)
                    {
                        secondaryTotal += value.experience;
                        secondaryByAgeBand[ageBandIndex] += value.experience;
                        secondarySpeedTotal += ProficiencyProgressionRules
                            .ResolveEffects(value.experience
                                * ProficiencyProgressionRules.MilliPerExperience)
                            .WorkSpeedMultiplier;
                    }
                    else
                    {
                        unrelatedTotal += value.experience;
                        unrelatedSpeedTotal += ProficiencyProgressionRules
                            .ResolveEffects(value.experience
                                * ProficiencyProgressionRules.MilliPerExperience)
                            .WorkSpeedMultiplier;
                        unrelatedCount++;
                    }
                }
                samples++;
            }
        }

        double primaryMean = primaryTotal / (double)samples;
        double secondaryMean = secondaryTotal / (double)samples;
        double unrelatedMean = unrelatedTotal / (double)unrelatedCount;
        double primarySpeedMean = primarySpeedTotal / samples;
        double secondarySpeedMean = secondarySpeedTotal / samples;
        double unrelatedSpeedMean = unrelatedSpeedTotal / unrelatedCount;
        if (!(primaryMean > secondaryMean && secondaryMean > unrelatedMean))
        {
            throw new InvalidOperationException(
                $"Expected primary > secondary > unrelated mean, got "
                + $"{primaryMean:0.00}/{secondaryMean:0.00}/{unrelatedMean:0.00}.");
        }
        if (ageBandCounts.Any(value => value <= 0))
        {
            throw new InvalidOperationException("All four starting-age bands must appear.");
        }
        int elderCount = ageBandCounts[(int)CharacterStartingAgeBand.Elder];
        double elderConditionRate = initialConditionProfiles / (double)elderCount;
        double elderMultipleRate = multipleConditionProfiles / (double)elderCount;
        if (elderConditionRate < 0.65d || elderConditionRate > 0.80d
            || elderMultipleRate < 0.25d || elderMultipleRate > 0.45d)
        {
            throw new InvalidOperationException(
                $"Elder condition rates are outside the target bands: "
                + $"any={elderConditionRate:P1}, multiple={elderMultipleRate:P1}.");
        }

        VerifyInitialLifeConditionPublication(conditions, life);
        VerifyCharacterSaveRoundTrip(
            CharacterStartingProfileRules.Create(
                260810,
                startingLife,
                origins[0],
                histories[0],
                startingConditions));

        StringBuilder report = new();
        report.Append("founder-profile passed; samples=").Append(samples)
            .Append("; mean primary/secondary/unrelated=")
            .Append(primaryMean.ToString("0.00")).Append('/')
            .Append(secondaryMean.ToString("0.00")).Append('/')
            .Append(unrelatedMean.ToString("0.00"))
            .Append("; mean speed=")
            .Append(primarySpeedMean.ToString("0.000")).Append('/')
            .Append(secondarySpeedMean.ToString("0.000")).Append('/')
            .Append(unrelatedSpeedMean.ToString("0.000"))
            .Append("; age bands=")
            .Append(string.Join("/", ageBandCounts))
            .Append("; primary by age=")
            .Append(string.Join("/", primaryByAgeBand.Select(
                (value, index) => (value / (double)ageBandCounts[index])
                    .ToString("0.00"))))
            .Append("; secondary by age=")
            .Append(string.Join("/", secondaryByAgeBand.Select(
                (value, index) => (value / (double)ageBandCounts[index])
                    .ToString("0.00"))))
            .Append("; condition profiles=").Append(initialConditionProfiles)
            .Append("; multiple conditions=").Append(multipleConditionProfiles)
            .Append("; elder condition rates=")
            .Append(elderConditionRate.ToString("P1")).Append('/')
            .Append(elderMultipleRate.ToString("P1"));
        return report.ToString();
    }

    public static string RunRosterCoverage(int rosterSamples = 20000)
    {
        if (rosterSamples <= 0)
            throw new ArgumentOutOfRangeException(nameof(rosterSamples));
        CharacterStartingOriginSO[] origins = LoadAll<CharacterStartingOriginSO>();
        CharacterStartingHistorySO[] histories = LoadAll<CharacterStartingHistorySO>();
        SpeciesLifeHistorySO[] lifeHistories = LoadAll<SpeciesLifeHistorySO>();
        CharacterStartingAgeCondition[] conditions =
            LoadAll<AgeConditionDefinitionSO>()
                .Select(value => new CharacterStartingAgeCondition(
                    value.conditionId,
                    value.constructCondition))
                .ToArray();
        if (origins.Length != 6 || histories.Length != 9
            || lifeHistories.Length == 0)
        {
            throw new InvalidOperationException(
                "Founder roster coverage requires the complete authored profile catalog.");
        }

        double[] selectedBestSpeed = new double[SettlementEssentials.Length];
        double[] randomBestSpeed = new double[SettlementEssentials.Length];
        int selectedAllSpecialized = 0;
        int randomAllSpecialized = 0;
        double selectedSurvivalAssignment = 0d;
        double randomSurvivalAssignment = 0d;
        double selectedManufacturingAssignment = 0d;
        double randomManufacturingAssignment = 0d;
        double selectedPrimaryExperience = 0d;
        double randomPrimaryExperience = 0d;
        int selectedElders = 0;
        int randomElders = 0;
        int selectedConditions = 0;
        int randomConditions = 0;

        for (int sample = 0; sample < rosterSamples; sample++)
        {
            SpeciesLifeHistorySO life = lifeHistories[sample % lifeHistories.Length];
            CharacterStartingLifeHistory startingLife = new(
                life.adultAgeYears,
                life.elderAgeYears,
                life.untreatedExpectedLifeYears,
                life.construct);
            FounderCandidate[] roster = new FounderCandidate[7];
            for (int index = 0; index < roster.Length; index++)
            {
                int seed = CharacterGrowthRules.StableHash(
                    $"founder-roster:{sample}:{life.speciesTag}:{index}");
                int historyIndex = PositiveModulo(
                    CharacterGrowthRules.StableHash($"{seed}:history"),
                    histories.Length);
                int originIndex = PositiveModulo(
                    CharacterGrowthRules.StableHash($"{seed}:origin"),
                    origins.Length);
                roster[index] = new FounderCandidate(
                    CharacterStartingProfileRules.Create(
                        seed,
                        startingLife,
                        origins[originIndex],
                        histories[historyIndex],
                        conditions));
            }

            FounderCandidate[] randomParty =
                { roster[0], roster[1], roster[2] };
            FounderCandidate[] selectedParty = SelectBalancedParty(roster);
            AccumulateParty(
                randomParty,
                randomBestSpeed,
                ref randomAllSpecialized,
                ref randomSurvivalAssignment,
                ref randomManufacturingAssignment,
                ref randomPrimaryExperience,
                ref randomElders,
                ref randomConditions);
            AccumulateParty(
                selectedParty,
                selectedBestSpeed,
                ref selectedAllSpecialized,
                ref selectedSurvivalAssignment,
                ref selectedManufacturingAssignment,
                ref selectedPrimaryExperience,
                ref selectedElders,
                ref selectedConditions);
        }

        StringBuilder report = new();
        report.Append("founder-roster passed; samples=").Append(rosterSamples)
            .Append("; random best speeds=")
            .Append(FormatMeans(randomBestSpeed, rosterSamples))
            .Append("; selected best speeds=")
            .Append(FormatMeans(selectedBestSpeed, rosterSamples))
            .Append("; all-four specialized random/selected=")
            .Append((randomAllSpecialized / (double)rosterSamples).ToString("P1"))
            .Append('/')
            .Append((selectedAllSpecialized / (double)rosterSamples).ToString("P1"))
            .Append("; survival assignment random/selected=")
            .Append((randomSurvivalAssignment / rosterSamples).ToString("0.000"))
            .Append('/')
            .Append((selectedSurvivalAssignment / rosterSamples).ToString("0.000"))
            .Append("; manufacturing assignment random/selected=")
            .Append((randomManufacturingAssignment / rosterSamples).ToString("0.000"))
            .Append('/')
            .Append((selectedManufacturingAssignment / rosterSamples).ToString("0.000"))
            .Append("; mean selected primary XP random/selected=")
            .Append((randomPrimaryExperience / (rosterSamples * 3d)).ToString("0.00"))
            .Append('/')
            .Append((selectedPrimaryExperience / (rosterSamples * 3d)).ToString("0.00"))
            .Append("; elder share random/selected=")
            .Append((randomElders / (rosterSamples * 3d)).ToString("P1"))
            .Append('/')
            .Append((selectedElders / (rosterSamples * 3d)).ToString("P1"))
            .Append("; condition count random/selected=")
            .Append(randomConditions).Append('/').Append(selectedConditions);
        return report.ToString();
    }

    private static FounderCandidate[] SelectBalancedParty(
        IReadOnlyList<FounderCandidate> roster)
    {
        FounderCandidate[] best = null;
        double bestScore = double.MinValue;
        for (int first = 1; first < roster.Count - 1; first++)
        {
            for (int second = first + 1; second < roster.Count; second++)
            {
                FounderCandidate[] party =
                    { roster[0], roster[first], roster[second] };
                double[] speeds = SettlementEssentials
                    .Select(id => (double)party.Max(value => value.Speed(id)))
                    .ToArray();
                int specializationCoverage = SettlementEssentials.Count(id =>
                    party.Any(value => value.IsSpecialized(id)));
                int conditionCount = party.Sum(value =>
                    value.Roll.Profile.initialAgeConditionIds.Count);
                double score = speeds.Sum()
                    + 2d * speeds.Min()
                    + 0.10d * specializationCoverage
                    - ConditionSelectionPenalty * conditionCount;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = party;
                }
            }
        }
        return best ?? throw new InvalidOperationException(
            "Could not select a balanced founder party.");
    }

    private static void AccumulateParty(
        IReadOnlyList<FounderCandidate> party,
        double[] bestSpeedTotals,
        ref int allSpecialized,
        ref double survivalAssignment,
        ref double manufacturingAssignment,
        ref double primaryExperience,
        ref int elders,
        ref int conditions)
    {
        for (int index = 0; index < SettlementEssentials.Length; index++)
        {
            bestSpeedTotals[index] += party.Max(value =>
                value.Speed(SettlementEssentials[index]));
        }
        if (SettlementEssentials.All(id =>
                party.Any(value => value.IsSpecialized(id))))
        {
            allSpecialized++;
        }
        survivalAssignment += ResolveBestDistinctAssignment(
            party,
            BuiltInCharacterProficiencyIds.Fieldwork,
            BuiltInCharacterProficiencyIds.FoodProduction,
            BuiltInCharacterProficiencyIds.ConstructionEngineering);
        manufacturingAssignment += ResolveBestDistinctAssignment(
            party,
            BuiltInCharacterProficiencyIds.Fieldwork,
            BuiltInCharacterProficiencyIds.FoodProduction,
            BuiltInCharacterProficiencyIds.Crafting);
        foreach (FounderCandidate candidate in party)
        {
            primaryExperience += candidate.Experience(
                new CharacterProficiencyId(
                    candidate.Roll.Profile.primaryProficiencyId));
            if (candidate.Roll.Profile.ageBand == CharacterStartingAgeBand.Elder)
                elders++;
            conditions += candidate.Roll.Profile.initialAgeConditionIds.Count;
        }
    }

    private static double ResolveBestDistinctAssignment(
        IReadOnlyList<FounderCandidate> party,
        CharacterProficiencyId first,
        CharacterProficiencyId second,
        CharacterProficiencyId third)
    {
        CharacterProficiencyId[] jobs = { first, second, third };
        double best = 0d;
        for (int a = 0; a < 3; a++)
        for (int b = 0; b < 3; b++)
        for (int c = 0; c < 3; c++)
        {
            if (a == b || a == c || b == c) continue;
            double total = party[a].Speed(jobs[0])
                + party[b].Speed(jobs[1])
                + party[c].Speed(jobs[2]);
            best = Math.Max(best, total);
        }
        return best;
    }

    private static int PositiveModulo(int value, int count) =>
        (int)((uint)value % (uint)count);

    private static string FormatMeans(
        IReadOnlyList<double> totals,
        int samples) => string.Join("/", totals.Select(value =>
        (value / samples).ToString("0.000")));

    private sealed class FounderCandidate
    {
        public FounderCandidate(CharacterStartingProfileRoll roll) => Roll = roll;

        public CharacterStartingProfileRoll Roll { get; }

        public bool IsSpecialized(CharacterProficiencyId id) =>
            string.Equals(
                Roll.Profile.primaryProficiencyId,
                id.Value,
                StringComparison.Ordinal)
            || string.Equals(
                Roll.Profile.secondaryProficiencyId,
                id.Value,
                StringComparison.Ordinal);

        public int Experience(CharacterProficiencyId id) =>
            Roll.Proficiencies.First(value =>
                string.Equals(
                    value.proficiencyId,
                    id.Value,
                    StringComparison.Ordinal)).experience;

        public float Speed(CharacterProficiencyId id) =>
            ProficiencyProgressionRules.ResolveEffects(
                Experience(id) * ProficiencyProgressionRules.MilliPerExperience)
            .WorkSpeedMultiplier;
    }

    private static void VerifySubgradeBoundaries()
    {
        VerifyBand(0, CharacterProficiencyRank.Apprentice, CharacterProficiencySubgrade.Fourth);
        VerifyBand(25, CharacterProficiencyRank.Apprentice, CharacterProficiencySubgrade.Third);
        VerifyBand(50, CharacterProficiencyRank.Apprentice, CharacterProficiencySubgrade.Second);
        VerifyBand(75, CharacterProficiencyRank.Apprentice, CharacterProficiencySubgrade.First);
        VerifyBand(100, CharacterProficiencyRank.Skilled, CharacterProficiencySubgrade.Fourth);
        VerifyBand(175, CharacterProficiencyRank.Skilled, CharacterProficiencySubgrade.Third);
        VerifyBand(250, CharacterProficiencyRank.Skilled, CharacterProficiencySubgrade.Second);
        VerifyBand(325, CharacterProficiencyRank.Skilled, CharacterProficiencySubgrade.First);
        VerifyBand(400, CharacterProficiencyRank.Technician, CharacterProficiencySubgrade.Fourth);
        VerifyBand(600, CharacterProficiencyRank.Technician, CharacterProficiencySubgrade.Third);
        VerifyBand(800, CharacterProficiencyRank.Technician, CharacterProficiencySubgrade.Second);
        VerifyBand(1000, CharacterProficiencyRank.Technician, CharacterProficiencySubgrade.First);
        VerifyBand(1200, CharacterProficiencyRank.Expert, CharacterProficiencySubgrade.Fourth);
        VerifyBand(1650, CharacterProficiencyRank.Expert, CharacterProficiencySubgrade.Third);
        VerifyBand(2100, CharacterProficiencyRank.Expert, CharacterProficiencySubgrade.Second);
        VerifyBand(2550, CharacterProficiencyRank.Expert, CharacterProficiencySubgrade.First);
        VerifyBand(3000, CharacterProficiencyRank.Master, CharacterProficiencySubgrade.Fourth);
        VerifyBand(3015, CharacterProficiencyRank.Master, CharacterProficiencySubgrade.Third);
        VerifyBand(3030, CharacterProficiencyRank.Master, CharacterProficiencySubgrade.Second);
        VerifyBand(3045, CharacterProficiencyRank.Master, CharacterProficiencySubgrade.First);

        float previousSpeed = 0f;
        float previousAccident = float.MaxValue;
        for (int xp = 0; xp <= 3060; xp++)
        {
            CharacterProficiencyEffectSnapshot effect =
                ProficiencyProgressionRules.ResolveEffects(
                    xp * ProficiencyProgressionRules.MilliPerExperience);
            if (effect.WorkSpeedMultiplier + 0.000001f < previousSpeed
                || effect.AccidentMultiplier - 0.000001f > previousAccident)
            {
                throw new InvalidOperationException(
                    $"Continuous proficiency effects are not monotonic at {xp} XP.");
            }
            previousSpeed = effect.WorkSpeedMultiplier;
            previousAccident = effect.AccidentMultiplier;
        }
    }

    private static void VerifyBand(
        int experience,
        CharacterProficiencyRank rank,
        CharacterProficiencySubgrade subgrade)
    {
        CharacterProficiencyBandSnapshot actual =
            ProficiencyProgressionRules.ResolveBand(
                experience * ProficiencyProgressionRules.MilliPerExperience);
        if (actual.Rank != rank || actual.Subgrade != subgrade)
        {
            throw new InvalidOperationException(
                $"Unexpected band at {experience} XP: {actual.Rank}/{actual.Subgrade}.");
        }
    }

    private static void VerifyAgeCaps()
    {
        int[] caps = Enum.GetValues(typeof(CharacterStartingAgeBand))
            .Cast<CharacterStartingAgeBand>()
            .Select(CharacterStartingProfileRules.ResolveAgeCap)
            .ToArray();
        if (!caps.SequenceEqual(new[] { 99, 174, 249, 399 })
            || caps.Zip(caps.Skip(1), (left, right) => left < right).Any(value => !value))
        {
            throw new InvalidOperationException("Starting-age caps are not canonical and monotonic.");
        }
    }

    private static void VerifyProfile(CharacterStartingProfileRoll roll)
    {
        CharacterStartingProficiencyRules.Validate(roll.Proficiencies);
        CharacterStartingProficiencyExperience primary = roll.Proficiencies
            .Single(value => value.proficiencyId
                == roll.Profile.primaryProficiencyId);
        CharacterStartingProficiencyExperience secondary = roll.Proficiencies
            .Single(value => value.proficiencyId
                == roll.Profile.secondaryProficiencyId);
        if (!roll.Profile.prepared
            || roll.Proficiencies.Count != 9
            || roll.Proficiencies.Any(value => value.experience > roll.Profile.proficiencyCap)
            || roll.Profile.proficiencyCap >= 400
            || Math.Abs(
                primary.learningMultiplier
                - CharacterProficiencySpecializationRules
                    .PrimaryLearningMultiplier) > 0.0001f
            || Math.Abs(
                secondary.learningMultiplier
                - CharacterProficiencySpecializationRules
                    .SecondaryLearningMultiplier) > 0.0001f
            || roll.Proficiencies.Any(value =>
                value != primary
                && value != secondary
                && Math.Abs(
                    value.learningMultiplier
                    - CharacterProficiencySpecializationRules
                        .NeutralLearningMultiplier) > 0.0001f)
            || (roll.Profile.ageBand != CharacterStartingAgeBand.Elder
                && roll.Profile.initialAgeConditionIds.Count > 0))
        {
            throw new InvalidOperationException("Generated starting profile violates its age cap or health boundary.");
        }
    }

    private static void VerifyDeterministic(
        CharacterStartingProfileRoll first,
        CharacterStartingProfileRoll second)
    {
        if (JsonUtility.ToJson(first.Profile) != JsonUtility.ToJson(second.Profile)
            || !first.Proficiencies.Select(value => value.experience)
                .SequenceEqual(second.Proficiencies.Select(value => value.experience)))
        {
            throw new InvalidOperationException("Starting profile generation is not deterministic.");
        }
    }

    private static void VerifyInitialLifeConditionPublication(
        IReadOnlyList<AgeConditionDefinitionSO> conditions,
        SpeciesLifeHistorySO life)
    {
        AgeConditionDefinitionSO condition = conditions.First(value =>
            value != null && !value.constructCondition);
        CharacterId characterId = new("character:founder-profile-audit");
        SpeciesLifeHistoryDefinition definition = new(
            new CharacterSpeciesId(life.speciesTag),
            life.infantEndAgeYears,
            life.adolescentStartAgeYears,
            life.adultAgeYears,
            life.elderAgeYears,
            life.untreatedExpectedLifeYears,
            life.construct);
        CharacterLifeRecord record = new(
            characterId,
            definition.SpeciesId,
            100,
            life.elderAgeYears * GameCalendarRules.DaysPerYear,
            1,
            definition);
        IReadOnlyList<AgeConditionChange> changes = record.AddInitialAgeConditions(
            new[] { condition.conditionId },
            new[]
            {
                new AgeConditionDefinition(
                    condition.conditionId,
                    condition.constructCondition,
                    condition.affectedAnatomyNodeIds)
            });
        CharacterLifeRecordSaveData saved = record.Capture();
        if (changes.Count != 1
            || !changes[0].NewlyDiagnosed
            || saved.ageConditions.Count != 1
            || saved.ageConditions[0].conditionId != condition.conditionId)
        {
            throw new InvalidOperationException(
                "Initial age condition did not enter the existing life save authority.");
        }
    }

    private static void VerifyCharacterSaveRoundTrip(
        CharacterStartingProfileRoll roll)
    {
        CharacterGrowthState growth = new()
        {
            initialized = true,
            traitSelectionAuthorityVersion =
                CharacterGrowthState.CurrentTraitSelectionAuthorityVersion,
            traitSelectionAuthorityOrigin =
                CharacterTraitSelectionAuthorityOrigin.PreparedSelection,
            displayName = "Founder Audit",
            origin = roll.Profile.originDisplayName,
            startingProfile = roll.Profile.Clone(),
            startingProficiencies = roll.Proficiencies
                .Select(value => value.Clone())
                .ToList()
        };
        growth.EnsureCollections();
        DungeonCharacterSaveData source = new()
        {
            persistentId = "character:founder-profile-save-audit",
            displayName = "Founder Audit",
            level = 1,
            growth = growth
        };
        DungeonCharacterSaveData restored = JsonUtility.FromJson<DungeonCharacterSaveData>(
            JsonUtility.ToJson(source));
        DungeonGameRestoreReport report = new();
        CharacterWorldSaveValidation.ValidateActor(
            restored,
            restored.persistentId,
            report);
        if (!report.Success
            || restored.growth.startingProfile.historyId != roll.Profile.historyId
            || restored.growth.startingProfile.biologicalAgeYears
                != roll.Profile.biologicalAgeYears
            || !restored.growth.startingProficiencies
                .Select(value => value.experience)
                .SequenceEqual(roll.Proficiencies.Select(value => value.experience))
            || !restored.growth.startingProficiencies
                .Select(value => value.learningMultiplier)
                .SequenceEqual(roll.Proficiencies.Select(
                    value => value.learningMultiplier)))
        {
            throw new InvalidOperationException(
                "Prepared founder profile failed strict character-save round trip: "
                + string.Join("; ", report.Errors));
        }
    }

    private static T[] LoadAll<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .OrderBy(value => value.name, StringComparer.Ordinal)
            .ToArray();

    private static void RequireValid(
        IReadOnlyList<string> errors,
        string name)
    {
        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Invalid authored founder profile asset '{name}': {string.Join("; ", errors)}");
    }
}
