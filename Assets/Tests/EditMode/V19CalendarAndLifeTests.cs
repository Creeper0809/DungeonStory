using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DungeonStory.Tests.Architecture
{
    public sealed class V19CalendarAndLifeTests
    {
        [TestCase(1, 1, 1, Season.Spring, 1)]
        [TestCase(30, 1, 30, Season.Spring, 30)]
        [TestCase(31, 1, 31, Season.Summer, 1)]
        [TestCase(61, 1, 61, Season.Autumn, 1)]
        [TestCase(91, 1, 91, Season.Winter, 1)]
        [TestCase(120, 1, 120, Season.Winter, 30)]
        [TestCase(121, 2, 1, Season.Spring, 1)]
        public void CalendarProjectsTheFixedOneHundredTwentyDayYear(
            int day,
            int year,
            int dayOfYear,
            Season season,
            int dayOfSeason)
        {
            CalendarDateTime value = GameCalendarRules.Project(day, 0);
            Assert.That(value.Year, Is.EqualTo(year));
            Assert.That(value.DayOfYear, Is.EqualTo(dayOfYear));
            Assert.That(value.Season, Is.EqualTo(season));
            Assert.That(value.DayOfSeason, Is.EqualTo(dayOfSeason));
        }

        [Test]
        public void RegionalTimeUsesOneAbsoluteCalendarAndBoundedHourOffset()
        {
            CalendarDateTime previous = GameCalendarRules.ProjectRegional(31, 2, -6);
            CalendarDateTime next = GameCalendarRules.ProjectRegional(30, 22, 6);
            Assert.That(previous.AbsoluteDay, Is.EqualTo(30));
            Assert.That(previous.Hour, Is.EqualTo(20));
            Assert.That(next.AbsoluteDay, Is.EqualTo(31));
            Assert.That(next.Hour, Is.EqualTo(4));
        }

        [Test]
        public void MinorsAgeFourUnitsAndAdultsAgeSixUnitsPerDay()
        {
            SpeciesLifeHistoryDefinition history = Life("Orc", 2, 11, 14, 45);
            CharacterLifeRecord minor = new(
                new CharacterId("character:test-minor"),
                new CharacterSpeciesId("Orc"),
                0,
                history.AdultAgeDayUnits - 4d,
                1,
                history);

            minor.AdvanceOneChronologicalDay(
                history,
                Array.Empty<AgeConditionDefinition>(),
                () => 0.99d);
            Assert.That(minor.BiologicalAgeDayUnits, Is.EqualTo(history.AdultAgeDayUnits));
            Assert.That(minor.LifeStage, Is.EqualTo(CharacterLifeStage.Adult));

            minor.AdvanceOneChronologicalDay(
                history,
                Array.Empty<AgeConditionDefinition>(),
                () => 0.99d);
            Assert.That(
                minor.BiologicalAgeDayUnits,
                Is.EqualTo(history.AdultAgeDayUnits + 6d));
        }

        [Test]
        public void ElderBirthdayCreatesConditionButNeverPerformsAgeDeath()
        {
            SpeciesLifeHistoryDefinition history = Life("Orc", 2, 11, 14, 45);
            CharacterLifeRecord elder = new(
                new CharacterId("character:test-elder"),
                new CharacterSpeciesId("Orc"),
                500,
                history.ElderAgeDayUnits - 1d,
                1,
                history);
            AgeConditionDefinition condition = new(
                "condition:test-aging",
                constructCondition: false,
                new[] { "heart" });

            IReadOnlyList<AgeConditionChange> changes = elder.AdvanceOneChronologicalDay(
                history,
                new[] { condition },
                () => 0d);

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].NewlyDiagnosed, Is.True);
            Assert.That(changes[0].CausesOrganFunctionLoss, Is.False);
            Assert.That(elder.AgeConditions.Count, Is.EqualTo(1));
        }

        [Test]
        public void SenescenceHazardMatchesTheApprovedCurveAndCap()
        {
            Assert.That(
                CharacterLifeRecord.CalculateAgeConditionProbability(45d, 45),
                Is.EqualTo(0.005d).Within(0.0000001d));
            Assert.That(
                CharacterLifeRecord.CalculateAgeConditionProbability(53d, 45),
                Is.EqualTo(0.01d).Within(0.0000001d));
            Assert.That(
                CharacterLifeRecord.CalculateAgeConditionProbability(200d, 45),
                Is.EqualTo(0.65d).Within(0.0000001d));
        }

        [Test]
        public void TwoHundredThousandUntreatedLivesMatchApprovedExpectancies()
        {
            const int population = 200_000;
            const int severityProgressionYears = 4;
            Random random = new(19019);
            int[] yearsAfterElder = new int[population];
            double total = 0d;
            for (int subject = 0; subject < population; subject++)
            {
                for (int offset = 0; ; offset++)
                {
                    double probability =
                        CharacterLifeRecord.CalculateAgeConditionProbability(
                            offset,
                            0);
                    if (random.NextDouble() >= probability)
                        continue;

                    int years = offset + severityProgressionYears;
                    yearsAfterElder[subject] = years;
                    total += years;
                    break;
                }
            }

            Array.Sort(yearsAfterElder);
            double meanAfterElder = total / population;
            Assert.That(meanAfterElder, Is.EqualTo(32.17d).Within(0.15d));
            Assert.That(yearsAfterElder[population / 2], Is.EqualTo(33));

            (int ElderAge, double ExpectedLife)[] species =
            {
                (42, 74.2d),
                (45, 77.2d),
                (130, 162.2d),
                (48, 80.2d),
                (85, 117.2d),
                (38, 70.2d),
                (30, 62.2d),
                (52, 84.2d),
                (90, 122.2d)
            };
            foreach ((int elderAge, double expectedLife) in species)
            {
                Assert.That(
                    elderAge + meanAfterElder,
                    Is.EqualTo(expectedLife).Within(1d));
            }
        }

        [Test]
        public void WholeBodyRegenerationReducesAgeAndOnlyApprovedConditionStages()
        {
            SpeciesLifeHistoryDefinition history = Life("Orc", 2, 11, 14, 45);
            CharacterLifeRecord record = CharacterLifeRecord.Restore(
                new CharacterLifeRecordSaveData
                {
                    characterId = "character:regeneration-test",
                    phenotypeSpeciesId = "Orc",
                    chronologicalAgeDays = 1_000,
                    biologicalAgeDayUnits = (14 + 40) * GameCalendarRules.DaysPerYear,
                    birthdayDayOfYear = 1,
                    lifeStage = CharacterLifeStage.Elder,
                    ageConditions = new List<CharacterAgeConditionSaveData>
                    {
                        Condition("condition:mild", AgeConditionSeverity.Mild),
                        Condition("condition:moderate", AgeConditionSeverity.Moderate),
                        Condition("condition:severe", AgeConditionSeverity.Severe),
                        Condition("condition:critical", AgeConditionSeverity.Critical),
                        Condition("condition:loss", AgeConditionSeverity.OrganFunctionLoss)
                    }
                },
                history);

            IReadOnlyList<AgeConditionChange> changes =
                record.ApplyWholeBodyRegeneration(history);

            Assert.That(
                record.BiologicalAgeDayUnits,
                Is.EqualTo((14 + 10) * GameCalendarRules.DaysPerYear));
            Assert.That(
                changes.Count(value => value.Resolved),
                Is.EqualTo(2));
            Assert.That(
                record.AgeConditions.Select(value => value.ConditionId),
                Is.EquivalentTo(new[]
                {
                    "condition:severe",
                    "condition:critical",
                    "condition:loss"
                }));
            Assert.That(
                record.AgeConditions.Single(
                    value => value.ConditionId == "condition:severe").Severity,
                Is.EqualTo(AgeConditionSeverity.Mild));
            Assert.That(
                record.AgeConditions.Single(
                    value => value.ConditionId == "condition:critical").Severity,
                Is.EqualTo(AgeConditionSeverity.Critical));
        }

        [Test]
        public void BloodRejuvenationUsesAdultPlusFiveFloorAndOneYearCooldown()
        {
            SpeciesLifeHistoryDefinition history = Life("Orc", 2, 11, 14, 45);
            CharacterLifeRecord record = new(
                new CharacterId("character:blood-rejuvenation-test"),
                new CharacterSpeciesId("Orc"),
                1_000,
                30 * GameCalendarRules.DaysPerYear,
                1,
                history);

            Assert.That(
                record.TryApplyBloodRejuvenation(history, 200, out _),
                Is.True);
            Assert.That(
                record.BiologicalAgeDayUnits,
                Is.EqualTo(20 * GameCalendarRules.DaysPerYear));
            Assert.That(
                record.TryApplyBloodRejuvenation(
                    history,
                    319,
                    out DomainFailure cooldownFailure),
                Is.False);
            Assert.That(
                cooldownFailure.Code,
                Is.EqualTo(FailureCode.AgeTreatmentCooldownActive));
            Assert.That(
                record.TryApplyBloodRejuvenation(history, 320, out _),
                Is.True);
            Assert.That(
                record.BiologicalAgeDayUnits,
                Is.EqualTo(19 * GameCalendarRules.DaysPerYear));
            Assert.That(
                record.TryApplyBloodRejuvenation(
                    history,
                    440,
                    out DomainFailure floorFailure),
                Is.False);
            Assert.That(
                floorFailure.Code,
                Is.EqualTo(FailureCode.AgeTreatmentTooYoung));
        }

        [Test]
        public void AgingCareModesApplyQuarterSpeedAndSupplyGatedStasis()
        {
            SpeciesLifeHistoryDefinition history = Life("Orc", 2, 11, 14, 45);
            CharacterLifeRecord record = new(
                new CharacterId("character:aging-care-test"),
                new CharacterSpeciesId("Orc"),
                1_000,
                20 * GameCalendarRules.DaysPerYear,
                1,
                history);
            double initial = record.BiologicalAgeDayUnits;

            record.ConfigureLongTermCare(
                geriatricMedicineActive: false,
                chronicCareActive: false,
                AgingCareMode.RuneHibernation);
            record.AdvanceOneChronologicalDayWithCare(
                history,
                Array.Empty<AgeConditionDefinition>(),
                () => 0.99d);
            Assert.That(
                record.BiologicalAgeDayUnits,
                Is.EqualTo(initial + 1.5d));

            record.ConfigureTemporalStasis(
                "building:temporal-stasis-test",
                operational: true,
                nextMaintenanceAbsoluteDay: 31);
            record.AdvanceOneChronologicalDayWithCare(
                history,
                Array.Empty<AgeConditionDefinition>(),
                () => 0.99d);
            Assert.That(
                record.BiologicalAgeDayUnits,
                Is.EqualTo(initial + 1.5d));

            record.ConfigureTemporalStasis(
                "building:temporal-stasis-test",
                operational: false,
                nextMaintenanceAbsoluteDay: 31);
            record.AdvanceOneChronologicalDayWithCare(
                history,
                Array.Empty<AgeConditionDefinition>(),
                () => 0.99d);
            Assert.That(
                record.BiologicalAgeDayUnits,
                Is.EqualTo(initial + 7.5d));
        }

        [Test]
        public void ChildSafetyBlocksCombatSupplyAtTheDirectTraversalRule()
        {
            bool allowed = ChildSafetyTraversalRules.CanTraverse(
                CharacterLifeStage.Child,
                GridMovementIntent.CombatSupply,
                apprenticeshipAuthorizationValid: false,
                WorldHazardLevel.Safe,
                WorldHazardLevel.Safe,
                out FailureCode failure);
            Assert.That(allowed, Is.False);
            Assert.That(failure, Is.EqualTo(FailureCode.ChildSafetyCombatForbidden));
        }

        [Test]
        public void RestrictedTraversalRequiresAdolescentApprenticeshipAuthorization()
        {
            Assert.That(
                ChildSafetyTraversalRules.CanTraverse(
                    CharacterLifeStage.Child,
                    GridMovementIntent.Apprenticeship,
                    true,
                    WorldHazardLevel.Safe,
                    WorldHazardLevel.Restricted,
                    out _),
                Is.False);
            Assert.That(
                ChildSafetyTraversalRules.CanTraverse(
                    CharacterLifeStage.Adolescent,
                    GridMovementIntent.Apprenticeship,
                    false,
                    WorldHazardLevel.Safe,
                    WorldHazardLevel.Restricted,
                    out _),
                Is.False);
            Assert.That(
                ChildSafetyTraversalRules.CanTraverse(
                    CharacterLifeStage.Adolescent,
                    GridMovementIntent.Apprenticeship,
                    true,
                    WorldHazardLevel.Safe,
                    WorldHazardLevel.Restricted,
                    out _),
                Is.True);
        }

        [Test]
        public void HazardEscapeOnlyAllowsStrictlyLowerRisk()
        {
            Assert.That(
                ChildSafetyTraversalRules.CanTraverse(
                    CharacterLifeStage.Child,
                    GridMovementIntent.EscapeHazard,
                    false,
                    WorldHazardLevel.Forbidden,
                    WorldHazardLevel.Restricted,
                    out _),
                Is.True);
            Assert.That(
                ChildSafetyTraversalRules.CanTraverse(
                    CharacterLifeStage.Child,
                    GridMovementIntent.EscapeHazard,
                    false,
                    WorldHazardLevel.Restricted,
                    WorldHazardLevel.Restricted,
                    out FailureCode failure),
                Is.False);
            Assert.That(
                failure,
                Is.EqualTo(FailureCode.ChildSafetyHazardEscapeDirectionInvalid));
        }

        [Test]
        public void KinshipColdArchiveKeepsThreeGenerationAncestryAndCompressesUnrelatedDeaths()
        {
            CharacterKinshipAggregate kinship = new();
            CharacterId child = new("character:living-child");
            CharacterId parent = new("character:dead-parent");
            CharacterId grandparent = new("character:dead-grandparent");
            CharacterId greatGrandparent = new("character:dead-great-grandparent");
            CharacterId unrelated = new("character:dead-unrelated");
            HouseholdId household = new("household:test-lineage");

            kinship.AddParent(child, parent, adoptive: false);
            kinship.AddParent(parent, grandparent, adoptive: false);
            kinship.AddParent(grandparent, greatGrandparent, adoptive: false);
            kinship.ArchiveDeath(parent, new CharacterSpeciesId("Orc"), -200, 100,
                false, household, 1);
            kinship.ArchiveDeath(grandparent, new CharacterSpeciesId("Orc"), -500, 90,
                false, household, 0);
            kinship.ArchiveDeath(greatGrandparent, new CharacterSpeciesId("Orc"), -800, 80,
                false, household, 0);
            kinship.ArchiveDeath(unrelated, new CharacterSpeciesId("Orc"), -100, 110,
                false, household, 2);

            kinship.ArchiveColdData(400, new[] { child });

            KinshipWorldSaveData captured = kinship.Capture();
            Assert.That(
                captured.tombstones.Select(value => value.characterId),
                Is.EquivalentTo(new[]
                {
                    parent.Value,
                    grandparent.Value,
                    greatGrandparent.Value
                }));
            Assert.That(captured.links, Has.Count.EqualTo(3));
            Assert.That(captured.lineageSummaries, Has.Count.EqualTo(1));
            Assert.That(captured.lineageSummaries[0].archivedCharacterCount, Is.EqualTo(1));
            Assert.That(captured.lineageSummaries[0].generation, Is.EqualTo(2));
            Assert.That(
                kinship.IsAncestor(greatGrandparent, child, 3),
                Is.True);
        }

        [Test]
        public void CompletedReproductionPublishesExactlyOnePersistentResultCharacter()
        {
            ReproductionDefinition definition = new(
                new CharacterSpeciesId("Orc"),
                ReproductionMode.Pregnancy,
                0.35f,
                -10f,
                40f,
                new[]
                {
                    new ReproductionPhaseDefinition
                    {
                        phase = ReproductionPhaseKind.Delivery,
                        durationDays = 1
                    }
                });
            ReproductionProcess process = new(
                "reproduction:test-result",
                new CharacterId("character:parent-a"),
                new CharacterId("character:parent-b"),
                new CharacterId("character:parent-a"),
                new CharacterSpeciesId("Orc"),
                definition,
                1,
                crossLineageIncubatorUsed: false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<InnateAptitudeSaveData>());

            process.AdvanceDay(
                new ReproductionDailyContext(2, 100f, 100f, 20f),
                miscarriageRandom: 0.99d);
            Assert.That(process.Status, Is.EqualTo(ReproductionProcessStatus.Completed));

            CharacterId child = new("character:published-child");
            process.MarkResultPublished(child);
            ReproductionProcessSaveData captured = process.Capture();
            Assert.That(captured.resultPublished, Is.True);
            Assert.That(captured.resultCharacterId, Is.EqualTo(child.Value));
            Assert.Throws<InvalidOperationException>(() =>
                process.MarkResultPublished(new CharacterId("character:duplicate-child")));
        }

        [Test]
        public void ReproductionAttemptUsesHealthNutritionAndFertilityAdjustedChance()
        {
            ReproductionDefinition definition = new(
                new CharacterSpeciesId("Orc"),
                ReproductionMode.Pregnancy,
                0.35f,
                -10f,
                40f,
                new[]
                {
                    new ReproductionPhaseDefinition
                    {
                        phase = ReproductionPhaseKind.Attempt,
                        durationDays = 1
                    }
                });
            ReproductionProcess success = ReproductionProcessForChance(
                "reproduction:chance-success",
                definition);
            success.AdvanceDay(
                new ReproductionDailyContext(2, 100f, 100f, 20f, 1f),
                miscarriageRandom: 0.34d);
            Assert.That(success.Status, Is.EqualTo(ReproductionProcessStatus.Completed));

            ReproductionProcess failure = ReproductionProcessForChance(
                "reproduction:chance-failure",
                definition);
            failure.AdvanceDay(
                new ReproductionDailyContext(2, 100f, 100f, 20f, 1f),
                miscarriageRandom: 0.35d);
            Assert.That(failure.Status, Is.EqualTo(ReproductionProcessStatus.Failed));
            Assert.That(failure.Failure, Is.EqualTo(ReproductionFailureCode.ConceptionFailed));

            Assert.That(
                ReproductionRules.CalculateSuccessChance(0.35f, 50f, 50f, 0.5f),
                Is.EqualTo(0.0875f).Within(0.000001f));
        }

        [Test]
        public void TemperateClimateUsesApprovedSeasonalCurveAndBoundedNoise()
        {
            TestClimateCatalog catalog = new();
            ClimateAggregateState state = ClimateAggregateState.Create(
                1,
                "climate:test",
                catalog,
                () => 0.5d);
            state.AdvanceToDay(30, catalog, () => 0.5d);
            Assert.That(state.GetOutdoorTemperature(catalog), Is.EqualTo(14f).Within(0.0001f));
            ClimateWorldSaveData captured = state.Capture();
            Assert.That(captured.dailyNoiseC, Is.InRange(-2f, 2f));
            Assert.That(
                ClimateAggregateState.Restore(captured, catalog)
                    .GetOutdoorTemperature(catalog),
                Is.EqualTo(state.GetOutdoorTemperature(catalog)).Within(0.0001f));
        }

        [Test]
        public void PopulationHealthUsesBoundedExposureProbabilityAndDeterministicOutbreaks()
        {
            Assert.That(
                PopulationHealthAggregateState.CalculateInfectionProbability(
                    0.18f, 24f, 0f, 1f, 1f),
                Is.EqualTo(0.18d).Within(0.000001d));
            Assert.That(
                PopulationHealthAggregateState.CalculateInfectionProbability(
                    1f, 24f, 0f, 3f, 2f),
                Is.EqualTo(0.80d).Within(0.000001d));

            TestDiseaseCatalog catalog = new();
            PopulationHealthAggregateState state = new();
            PopulationExposureTarget[] targets =
            {
                new(new CharacterId("character:patient-a"), 1f),
                new(new CharacterId("character:patient-b"), 1f),
                new(new CharacterId("character:patient-c"), 1f)
            };
            state.RecordExposure("disease:cave-flu", targets, 24f, 1f, catalog);
            state.AdvanceToDay(2, catalog, () => 0d);
            state.AdvanceToDay(4, catalog, () => 0.99d);
            Assert.That(state.IsEpidemicDeclared("disease:cave-flu"), Is.True);
            state.AdvanceToDay(10, catalog, () => 0.99d);
            Assert.That(
                state.GetImmunity(new CharacterId("character:patient-a"), "disease:cave-flu"),
                Is.EqualTo(80f).Within(0.001f));
              PopulationHealthAggregateState restored =
                  PopulationHealthAggregateState.Restore(state.Capture(), catalog);
              Assert.That(
                  restored.GetImmunity(new CharacterId("character:patient-a"), "disease:cave-flu"),
                  Is.EqualTo(80f).Within(0.001f));

              CharacterId golem = new("character:golem-a");
              PopulationHealthChange corrosion = state.ApplyEnvironmentalCondition(
                  golem,
                  "condition:core-corrosion",
                  catalog);
              Assert.That(corrosion.Kind, Is.EqualTo(PopulationHealthChangeKind.Diagnosed));
              Assert.That(
                  state.Capture().characters.Single(value => value.characterId == golem.Value)
                      .activeDiseases.Single().recoveryDay,
                  Is.EqualTo(int.MaxValue));
              state.RemoveEnvironmentalCondition(golem, "condition:core-corrosion", catalog);
              Assert.That(
                  state.Capture().characters.Single(value => value.characterId == golem.Value)
                      .activeDiseases,
                  Is.Empty);
        }

        [Test]
        public void JointFuneralAndLongNightMemorialConvertBoundedGriefOnce()
        {
            CharacterId survivor = new("character:grief-survivor");
            CharacterGriefAggregate grief = new(survivor);
            CharacterId[] deceased =
            {
                new("character:grief-deceased-a"),
                new("character:grief-deceased-b"),
                new("character:grief-deceased-c")
            };
            for (int index = 0; index < deceased.Length; index++)
            {
                grief.RecordDeath(
                    new CharacterLifeDeathRecord(
                        deceased[index],
                        CharacterDeathCauseCode.AgeConditionOrganFailure,
                        114 + index,
                        new CoreGridCell(0, 0),
                        Array.Empty<CharacterId>()),
                    GriefRelationshipKind.Colleague);
            }

            float beforeFuneral = grief.GetProjectedGriefMood(120);
            grief.CompleteJointMemorial(
                deceased,
                120,
                matchingSpeciesRitual: true);
            float afterFuneral = grief.GetProjectedGriefMood(120);
            Assert.That(afterFuneral, Is.GreaterThan(beforeFuneral));
            Assert.That(grief.GetProjectedMemorialResolve(120), Is.EqualTo(8f));

            grief.RecordFestivalAttendance(
                "festival:long-night-memorial",
                1);
            grief.ApplyLongNightMemorial(120);
            Assert.That(
                grief.GetProjectedGriefMood(120),
                Is.EqualTo(afterFuneral * 0.75f).Within(0.0001f));
            Assert.That(
                () => grief.ApplyLongNightMemorial(120),
                Throws.InvalidOperationException);

            CharacterGriefAggregate restored = CharacterGriefAggregate.Restore(
                grief.Capture());
            Assert.That(
                restored.HasAttendedFestival(
                    "festival:long-night-memorial",
                    1),
                Is.True);
            Assert.That(restored.LastLongNightMemorialYear, Is.EqualTo(1));
        }

        [Test]
        public void RetireesOnlyPerformSafeWorkForFourHoursPerDay()
        {
            CharacterId retiree = new("character:retiree-test");
            CharacterCareerAggregate careers = new();
            careers.Retire(retiree, 20);

            Assert.That(
                careers.CanPerformRetiredWork(
                    retiree,
                    20,
                    safeWork: false,
                    out string unsafeReason),
                Is.False);
            Assert.That(unsafeReason, Is.EqualTo("career:retiree-unsafe-work"));
            careers.RecordRetiredWork(
                retiree,
                20,
                CareerRules.RetireeMaximumSafeWorkSeconds);
            Assert.That(
                careers.CanPerformRetiredWork(
                    retiree,
                    20,
                    safeWork: true,
                    out string limitReason),
                Is.False);
            Assert.That(limitReason, Is.EqualTo("career:retiree-daily-limit"));
            Assert.That(
                careers.CanPerformRetiredWork(
                    retiree,
                    21,
                    safeWork: true,
                    out _),
                Is.True);

            CharacterCareerAggregate restored = CharacterCareerAggregate.Restore(
                careers.CaptureWorld());
            Assert.That(restored.TryGet(retiree, out CharacterCareerSnapshot state), Is.True);
            Assert.That(
                state.RetiredWorkSeconds,
                Is.EqualTo(CareerRules.RetireeMaximumSafeWorkSeconds));
        }

        [Test]
        public void MentoringAwardsAtMostOncePerStudentPerDayAndRoundTrips()
        {
            CharacterId mentor = new("character:mentor-test");
            CharacterId student = new("character:student-test");
            BuildingInstanceId academy = new("building:mentor-academy-test");
            CharacterCareerAggregate careers = new();
            careers.AssignPosition(
                mentor,
                CareerPositionKind.Mentor,
                academy.Value,
                40);
            careers.AssignMentorship(
                mentor,
                student,
                academy,
                BuiltInCharacterProficiencyIds.Crafting);

            Assert.That(careers.TryMarkMentoringAwarded(student, 41), Is.True);
            Assert.That(careers.TryMarkMentoringAwarded(student, 41), Is.False);
            Assert.That(careers.TryMarkMentoringAwarded(student, 42), Is.True);
            Assert.That(CareerRules.ResolveMentoringXp(99), Is.EqualTo(10));

            CharacterCareerAggregate restored = CharacterCareerAggregate.Restore(
                careers.CaptureWorld());
            Assert.That(restored.Mentorships.Count, Is.EqualTo(1));
            Assert.That(
                restored.Mentorships[0].ProficiencyId,
                Is.EqualTo(BuiltInCharacterProficiencyIds.Crafting));
            Assert.That(
                restored.Mentorships[0].LastAwardAbsoluteDay,
                Is.EqualTo(42));
        }

        private static SpeciesLifeHistoryDefinition Life(
            string species,
            int infant,
            int adolescent,
            int adult,
            int elder) => new(
            new CharacterSpeciesId(species),
            infant,
            adolescent,
            adult,
            elder,
            elder + 32.2f,
            construct: false);

        private static CharacterAgeConditionSaveData Condition(
            string conditionId,
            AgeConditionSeverity severity) => new()
        {
            conditionId = conditionId,
            severity = severity,
            onsetBiologicalAgeDayUnits = 1,
            nextProgressBiologicalAgeDayUnits = 10_000
        };

        private static ReproductionProcess ReproductionProcessForChance(
            string processId,
            ReproductionDefinition definition) => new(
            processId,
            new CharacterId("character:chance-parent-a"),
            new CharacterId("character:chance-parent-b"),
            new CharacterId("character:chance-parent-a"),
            new CharacterSpeciesId("Orc"),
            definition,
            1,
            crossLineageIncubatorUsed: false,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<InnateAptitudeSaveData>());

        private sealed class TestClimateCatalog : IClimateDefinitionCatalog
        {
            private readonly WeatherFrontDefinition front = new(
                "weather:test-clear",
                WeatherFrontKind.Clear,
                1,
                3,
                0f,
                new[] { 1f, 1f, 1f, 1f });

            public IReadOnlyList<WeatherFrontDefinition> Fronts => new[] { front };
            public ClimateZoneDefinition RequireZone(string id) =>
                id == "climate:test"
                    ? new ClimateZoneDefinition(id, 14f, 14f, 0)
                    : throw new KeyNotFoundException(id);
            public WeatherFrontDefinition RequireFront(string id) =>
                id == front.Id ? front : throw new KeyNotFoundException(id);
        }

          private sealed class TestDiseaseCatalog : IDiseaseDefinitionCatalog
          {
              private readonly IReadOnlyDictionary<string, DiseaseDefinition> definitions =
                  new[]
                  {
                      new DiseaseDefinition(
                          "disease:cave-flu",
                          "동굴 독감",
                          DiseaseTransmissionRoute.Air,
                          2,
                          6,
                          0.18f,
                          25f,
                          DiseaseTargetSystem.Breathing,
                          vaccineAllowed: true),
                      new DiseaseDefinition(
                          "condition:core-corrosion",
                          "핵 부식",
                          DiseaseTransmissionRoute.Environment,
                          0,
                          0,
                          0f,
                          60f,
                          DiseaseTargetSystem.Core,
                          vaccineAllowed: false,
                          chronic: true)
                  }.ToDictionary(value => value.Id, StringComparer.Ordinal);
              public IReadOnlyList<DiseaseDefinition> Definitions => definitions.Values.ToArray();
              public DiseaseDefinition Require(string diseaseId) =>
                  definitions.TryGetValue(diseaseId, out DiseaseDefinition value)
                      ? value
                      : throw new KeyNotFoundException(diseaseId);
          }
    }
}
