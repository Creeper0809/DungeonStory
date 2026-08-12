using System;
using System.Collections.Generic;
using System.Linq;

public static class CharacterProficiencyDebugHarness
{
    public static string Run()
    {
        CharacterNarrativeRecord record = CharacterNarrativeRecord.Create(
            new CharacterId("character:proficiency-debug"),
            new CharacterSpeciesId("species:orc"),
            new CharacterBackgroundId("background:proficiency-debug"),
            new SpeciesCultureId("culture:orc"),
            Array.Empty<string>(),
            Array.Empty<string>());

        record.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            0L,
            out _);
        long first = record.AddDirectExperience(
            BuiltInCharacterProficiencyIds.Crafting,
            0.4f,
            absoluteHour: 20L);
        Require(first == 400L, "The first fractional practice award was lost.");
        record.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            20L,
            out CharacterProficiencySnapshot partial);
        Require(
            partial.LastPracticeAbsoluteHour == 0L,
            "Less than one accumulated XP reset the decay clock.");

        record.AddDirectExperience(
            BuiltInCharacterProficiencyIds.Crafting,
            0.6f,
            absoluteHour: 21L);
        record.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            21L,
            out CharacterProficiencySnapshot practiced);
        Require(
            practiced.LastPracticeAbsoluteHour == 21L,
            "One accumulated XP did not reset the decay clock.");

        record.TryGetProficiency(
            BuiltInCharacterProficiencyIds.MeleeCombat,
            0L,
            out CharacterProficiencySnapshot initialMelee);
        for (int index = 0; index < 20; index++)
        {
            record.AddCombatExperience(
                BuiltInCharacterProficiencyIds.MeleeCombat,
                0.5f,
                training: false,
                stableAwardKey: $"battle:test:{index}",
                absoluteHour: 40L);
        }
        record.TryGetProficiency(
            BuiltInCharacterProficiencyIds.MeleeCombat,
            40L,
            out CharacterProficiencySnapshot combat);
        Require(
            combat.CurrentMilliExperience
                == initialMelee.CurrentMilliExperience + 8000L,
            "Combat daily cap must be exactly 8 XP.");
        long duplicate = record.AddCombatExperience(
            BuiltInCharacterProficiencyIds.MeleeCombat,
            0.5f,
            training: false,
            stableAwardKey: "battle:test:0",
            absoluteHour: 40L);
        Require(duplicate == 0L, "A stable combat event awarded XP twice.");

        record.TryGetProficiency(
            BuiltInCharacterProficiencyIds.RangedCombat,
            0L,
            out CharacterProficiencySnapshot initialRanged);
        for (int index = 0; index < 10; index++)
        {
            record.AddCombatExperience(
                BuiltInCharacterProficiencyIds.RangedCombat,
                0.5f,
                training: true,
                stableAwardKey: string.Empty,
                absoluteHour: 40L);
        }
        record.TryGetProficiency(
            BuiltInCharacterProficiencyIds.RangedCombat,
            40L,
            out CharacterProficiencySnapshot training);
        Require(
            training.CurrentMilliExperience
                == initialRanged.CurrentMilliExperience + 2000L,
            "Safe training daily cap must be exactly 2 XP.");

        CharacterNarrativeSaveData saved = record.Capture();
        NarrativeSkillExperienceSaveData melee = saved.skillExperience.Single(
            value => string.Equals(
                value.proficiencyId,
                BuiltInCharacterProficiencyIds.MeleeCombat.Value,
                StringComparison.Ordinal));
        Require(
            melee.combatAwardMilliToday == 8000L
                && melee.recentCombatAwardKeys.Count == 20,
            "Combat cap or idempotency state was not captured.");

        VerifyDecayRules();
        VerifyStartingProficiencyAuthority();
        VerifySpecializationLearning();
        VerifyDetailedPerformanceProjection();
        VerifyCompositeRules();
        VerifyWorkTypeAuthority();
        VerifyMentorshipLedgerRoundTrip();

        return "V26 proficiency aggregate PASS: deterministic starts, derived "
            + "performance, maintenance threshold, combat caps, composites, "
            + "x1.50/x1.20 specialization learning, expert/master decay, "
            + "and physical mentorship round-trip.";
    }

    private static void VerifySpecializationLearning()
    {
        List<CharacterStartingProficiencyExperience> starts =
            CharacterStartingProficiencyRules.Create(260810)
                .Select(value => value.Clone())
                .ToList();
        foreach (CharacterStartingProficiencyExperience value in starts)
        {
            value.learningMultiplier =
                CharacterProficiencySpecializationRules.Resolve(
                    BuiltInCharacterProficiencyIds.Crafting.Value,
                    BuiltInCharacterProficiencyIds.MeleeCombat.Value,
                    new CharacterProficiencyId(value.proficiencyId));
        }
        CharacterNarrativeRecord record = CharacterNarrativeRecord.Create(
            new CharacterId("character:proficiency-specialization-debug"),
            new CharacterSpeciesId("species:orc"),
            new CharacterBackgroundId("background:proficiency-debug"),
            new SpeciesCultureId("culture:orc"),
            Array.Empty<string>(),
            Array.Empty<string>(),
            startingProficiencies: starts);

        long primaryDirect = record.AddDirectExperience(
            BuiltInCharacterProficiencyIds.Crafting,
            1f,
            absoluteHour: 1L);
        long primaryWork = record.AddApprovedWork(
            new ProficiencyWorkProfile(BuiltInCharacterProficiencyIds.Crafting),
            approvedWork: 10f,
            difficultyMultiplier: 1f,
            outcome: ProficiencyWorkOutcome.Success,
            learningMultiplier: 1f,
            repetitionMultiplier: 1f,
            absoluteHour: 2L);
        long secondaryCombat = record.AddCombatExperience(
            BuiltInCharacterProficiencyIds.MeleeCombat,
            1f,
            training: false,
            stableAwardKey: "battle:specialization-debug",
            absoluteHour: 3L);
        long neutralDirect = record.AddDirectExperience(
            BuiltInCharacterProficiencyIds.FoodProduction,
            1f,
            absoluteHour: 4L);
        long unmultipliedFloor = record.AddDirectExperience(
            BuiltInCharacterProficiencyIds.Crafting,
            1f,
            absoluteHour: 5L,
            applyLearningMultiplier: false);
        Require(
            primaryDirect == 1500L
            && primaryWork == 1200L
            && secondaryCombat == 1200L
            && neutralDirect == 1000L
            && unmultipliedFloor == 1000L,
            "Specialization learning factors did not apply to all intended paths.");

        record.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            5L,
            out CharacterProficiencySnapshot primary);
        CharacterNarrativeSaveData saved = record.Capture();
        Require(
            Math.Abs(
                primary.LearningMultiplier
                - CharacterProficiencySpecializationRules
                    .PrimaryLearningMultiplier) <= 0.0001f
            && Math.Abs(saved.skillExperience.Single(value =>
                    value.proficiencyId
                    == BuiltInCharacterProficiencyIds.MeleeCombat.Value)
                .learningMultiplier
                - CharacterProficiencySpecializationRules
                    .SecondaryLearningMultiplier) <= 0.0001f,
            "Specialization learning factors were not projected or captured.");
    }

    public static string RunLongHorizonAndQualitySamples()
    {
        const int Samples = 100000;
        DeterministicCraftQualityResolver resolver = new();
        double apprenticeTotal = 0d;
        double masterTotal = 0d;
        bool apprenticeVaries = false;
        bool masterVaries = false;
        CraftsmanshipQualityTier firstApprentice = default;
        CraftsmanshipQualityTier firstMaster = default;
        float apprenticeSkill = ProficiencyProgressionRules.ResolveEffects(0L)
            .QualityScore;
        float masterSkill = ProficiencyProgressionRules.ResolveEffects(
            ProficiencyProgressionRules.MasterThreshold).QualityScore;
        for (int index = 0; index < Samples; index++)
        {
            CraftQualityRollSaveData roll = resolver.Roll(
                0xD0570F1UL,
                "pipeline:v25-proficiency-sample",
                "equipment:sample",
                index);
            CraftQualityResolution apprentice = resolver.Resolve(
                roll, apprenticeSkill, 0f, 0f, 0f);
            CraftQualityResolution master = resolver.Resolve(
                roll, masterSkill, 0f, 0f, 0f);
            apprenticeTotal += apprentice.Score;
            masterTotal += master.Score;
            if (index == 0)
            {
                firstApprentice = apprentice.Tier;
                firstMaster = master.Tier;
            }
            else
            {
                apprenticeVaries |= apprentice.Tier != firstApprentice;
                masterVaries |= master.Tier != firstMaster;
            }
        }
        Require(
            masterTotal > apprenticeTotal
                && apprenticeVaries
                && masterVaries,
            "100,000 quality samples did not improve with proficiency or became deterministic quality.");

        CharacterNarrativeRecord uninterrupted = CreateRecord("960-uninterrupted");
        uninterrupted.AddDirectExperience(
            BuiltInCharacterProficiencyIds.Crafting,
            3060f,
            absoluteHour: 0L);
        long half = 480L * GameCalendarRules.HoursPerDay;
        uninterrupted.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            half,
            out _);
        CharacterNarrativeSaveData checkpoint = uninterrupted.Capture();
        long end = 960L * GameCalendarRules.HoursPerDay;
        uninterrupted.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            end,
            out CharacterProficiencySnapshot direct);
        NarrativeSkillExperienceSaveData checkpointSkill =
            checkpoint.skillExperience.Single(value => string.Equals(
                value.proficiencyId,
                BuiltInCharacterProficiencyIds.Crafting.Value,
                StringComparison.Ordinal));
        Require(
            checkpointSkill.currentMilliExperience
                    == direct.CurrentMilliExperience
                && checkpointSkill.lifetimeMilliExperience
                    == direct.LifetimeMilliExperience
                && direct.Rank == CharacterProficiencyRank.Technician,
            "The 960-day lazy decay result or lifetime ledger is invalid.");

        return "V26 proficiency balance PASS: 100,000 quality samples and "
            + "960-day deterministic lazy decay/lifetime ledger.";
    }

    public static string RunDecayPerformanceProbe()
    {
        const int Population = 2000;
        CharacterNarrativeRecord[] records = new CharacterNarrativeRecord[Population];
        for (int index = 0; index < Population; index++)
        {
            records[index] = CreateRecord("decay-perf-" + index);
            RaiseToExactExperience(
                records[index],
                BuiltInCharacterProficiencyIds.Crafting,
                1200,
                0L);
        }

        CharacterNarrativeRecord warmup = CreateRecord("decay-perf-warmup");
        RaiseToExactExperience(
            warmup,
            BuiltInCharacterProficiencyIds.Crafting,
            1200,
            0L);
        warmup.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            361L,
            out _);

        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics.Stopwatch.StartNew();
        int demoted = 0;
        for (int index = 0; index < records.Length; index++)
        {
            records[index].TryGetProficiency(
                BuiltInCharacterProficiencyIds.Crafting,
                361L,
                out CharacterProficiencySnapshot snapshot);
            if (snapshot.Rank == CharacterProficiencyRank.Technician)
            {
                demoted++;
            }
        }
        stopwatch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread()
            - allocationBefore;

        Require(demoted == Population,
            "The 2,000-character lazy decay settlement produced an invalid rank.");
        Require(allocated == 0L,
            $"The 2,000-character lazy decay settlement allocated {allocated} bytes.");
        return $"V26 proficiency decay performance PASS: residents={Population}, "
            + $"lazySettlementMs={stopwatch.Elapsed.TotalMilliseconds:0.###}, "
            + "allocatedBytes=0, hourlyGlobalScan=absent.";
    }

    private static void VerifyDecayRules()
    {
        CharacterNarrativeRecord expert = CreateRecord("expert");
        RaiseToExactExperience(
            expert,
            BuiltInCharacterProficiencyIds.Crafting,
            1200,
            0L);
        expert.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            15L * GameCalendarRules.HoursPerDay,
            out CharacterProficiencySnapshot expertAtGrace);
        Require(
            expertAtGrace.Rank == CharacterProficiencyRank.Expert,
            "Expert decay began before the 15-day grace elapsed.");
        expert.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            15L * GameCalendarRules.HoursPerDay + 1L,
            out CharacterProficiencySnapshot demotedExpert);
        Require(
            demotedExpert.Rank == CharacterProficiencyRank.Technician,
            "Expert did not demote immediately after falling below 1,200 XP.");

        CharacterNarrativeRecord master = CreateRecord("master");
        RaiseToExactExperience(
            master,
            BuiltInCharacterProficiencyIds.Crafting,
            3060,
            0L);
        master.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            0L,
            out CharacterProficiencySnapshot cappedMaster);
        Require(
            cappedMaster.CurrentMilliExperience
                == ProficiencyProgressionRules.MasterCurrentCap,
            "Master current XP cap is not 3,060 XP.");
        master.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            5L * GameCalendarRules.HoursPerDay + 1L,
            out CharacterProficiencySnapshot decayingMaster);
        Require(
            decayingMaster.Rank == CharacterProficiencyRank.Master
                && decayingMaster.CurrentMilliExperience
                    == ProficiencyProgressionRules.MasterCurrentCap - 100L,
            "Master did not begin 0.10 XP/hour decay after the five-day grace.");
        master.TryGetProficiency(
            BuiltInCharacterProficiencyIds.Crafting,
            5L * GameCalendarRules.HoursPerDay + 601L,
            out CharacterProficiencySnapshot demotedMaster);
        Require(
            demotedMaster.Rank == CharacterProficiencyRank.Expert,
            "Master did not demote after its protected 60 XP buffer decayed.");
    }

    private static void VerifyStartingProficiencyAuthority()
    {
        IReadOnlyList<CharacterStartingProficiencyExperience> first =
            CharacterStartingProficiencyRules.Create(7731);
        IReadOnlyList<CharacterStartingProficiencyExperience> repeated =
            CharacterStartingProficiencyRules.Create(7731);
        IReadOnlyList<CharacterStartingProficiencyExperience> different =
            CharacterStartingProficiencyRules.Create(7732);
        CharacterStartingProficiencyRules.Validate(first);
        Require(
            first.Count == 9
                && first.All(value =>
                    value.experience
                        >= CharacterStartingProficiencyRules.MinimumStartingExperience
                    && value.experience
                        <= CharacterStartingProficiencyRules.MaximumStartingExperience),
            "Starting proficiency must contain nine bounded apprentice values.");
        Require(
            first.Select(value => $"{value.proficiencyId}:{value.experience}")
                .SequenceEqual(repeated.Select(value =>
                    $"{value.proficiencyId}:{value.experience}")),
            "The same starting proficiency seed was not deterministic.");
        Require(
            first.Where((value, index) =>
                    value.experience != different[index].experience)
                .Any(),
            "Different starting proficiency seeds produced an identical packet.");
    }

    private static void VerifyDetailedPerformanceProjection()
    {
        CharacterProficiencyEffectSnapshot apprentice =
            ProficiencyProgressionRules.ResolveEffects(
                20L * ProficiencyProgressionRules.MilliPerExperience);
        CharacterProficiencyEffectSnapshot expert =
            ProficiencyProgressionRules.ResolveEffects(
                ProficiencyProgressionRules.ExpertThreshold);
        Require(
            expert.WorkSpeedMultiplier > apprentice.WorkSpeedMultiplier
                && expert.QualityScore > apprentice.QualityScore
                && expert.AccidentMultiplier < apprentice.AccidentMultiplier,
            "The proficiency authority did not improve its independent result channels.");
    }

    private static void VerifyCompositeRules()
    {
        Require(
            WorkTypeProficiencyRules.ResolveDefenseExperience(450, 700) == 700f,
            "Defense must use the higher combat proficiency.");
        Require(
            Math.Abs(WorkTypeProficiencyRules.ResolvePrisonerManagementExperience(
                600, 300, 500) - 570f) < 0.001f,
            "Prisoner management composite is not social 70/combat 30.");
        Require(
            Math.Abs(WorkTypeProficiencyRules.ResolveHuntingExperience(500, 250)
                - 400f) < 0.001f,
            "Hunting composite is not food 60/weapon 40.");
        Require(
            Math.Abs(WorkTypeProficiencyRules.ResolveRuneCraftExperience(800, 400)
                - 680f) < 0.001f,
            "Rune crafting composite is not crafting 70/scholarship 30.");
    }

    private static void VerifyWorkTypeAuthority()
    {
        Require(
            BuiltInWorkTypeIds.All.Count == 31
                && BuiltInWorkTypeIds.All.Select(value => value.Value)
                    .Distinct(StringComparer.Ordinal).Count() == 31,
            "The canonical work-type catalog must contain 31 unique ids.");
        foreach (WorkTypeId workTypeId in BuiltInWorkTypeIds.All)
        {
            bool intentionallyUnskilled =
                workTypeId == BuiltInWorkTypeIds.Clean
                || workTypeId == BuiltInWorkTypeIds.Rest
                || workTypeId == BuiltInWorkTypeIds.Guard
                || workTypeId == BuiltInWorkTypeIds.ThreatMitigation
                || workTypeId == BuiltInWorkTypeIds.Operate;
            bool mapped = WorkTypeProficiencyRules.TryResolve(
                workTypeId,
                out ProficiencyWorkProfile profile);
            Require(
                mapped != intentionallyUnskilled && (!mapped || profile.IsValid),
                $"Work type '{workTypeId.Value}' has an invalid proficiency authority.");
        }
    }

    private static void VerifyMentorshipLedgerRoundTrip()
    {
        CharacterId mentor = new("character:mentor-debug");
        CharacterId student = new("character:student-debug");
        CharacterProficiencyId proficiency =
            BuiltInCharacterProficiencyIds.Crafting;
        CharacterCareerAggregate careers = new();
        careers.AssignMentorship(
            mentor,
            student,
            new BuildingInstanceId("building:mentor-academy-debug"),
            proficiency);
        careers.RecordMentorshipWork(
            student,
            absoluteDay: 12,
            mentorContribution: true,
            approvedWork: 30f);
        careers.RecordMentorshipWork(
            student,
            absoluteDay: 12,
            mentorContribution: false,
            approvedWork: 29f);

        CharacterCareerAggregate restored = CharacterCareerAggregate.Restore(
            careers.CaptureWorld());
        CareerMentorshipSnapshot beforeCompletion = restored.Mentorships.Single();
        Require(
            !beforeCompletion.HasCompletedPhysicalLesson,
            "Mentoring XP became eligible before both participants completed 30 work.");
        CareerMentorshipSnapshot completed = restored.RecordMentorshipWork(
            student,
            absoluteDay: 12,
            mentorContribution: false,
            approvedWork: 1f);
        Require(
            completed.HasCompletedPhysicalLesson,
            "The persisted 30+30 physical lesson did not complete.");
        Require(
            restored.TryMarkMentoringAwarded(student, 12)
                && !restored.TryMarkMentoringAwarded(student, 12),
            "Mentoring daily award idempotency failed.");
    }

    private static CharacterNarrativeRecord CreateRecord(string suffix) =>
        CharacterNarrativeRecord.Create(
            new CharacterId($"character:proficiency-{suffix}"),
            new CharacterSpeciesId("species:orc"),
            new CharacterBackgroundId("background:proficiency-debug"),
            new SpeciesCultureId("culture:orc"),
            Array.Empty<string>(),
            Array.Empty<string>());

    private static void RaiseToExactExperience(
        CharacterNarrativeRecord record,
        CharacterProficiencyId proficiencyId,
        int targetExperience,
        long absoluteHour)
    {
        record.TryGetProficiency(
            proficiencyId,
            absoluteHour,
            out CharacterProficiencySnapshot current);
        int missing = targetExperience - current.CurrentExperience;
        Require(missing >= 0, "A debug proficiency target was below its start.");
        record.AddDirectExperience(proficiencyId, missing, absoluteHour);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
