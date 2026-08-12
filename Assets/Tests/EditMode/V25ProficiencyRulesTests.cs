using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DungeonStory.Tests.Architecture
{
    public sealed class V25ProficiencyRulesTests
    {
        [Test]
        public void CanonicalCatalogContainsExactlyNineUniqueIds()
        {
            Assert.That(BuiltInCharacterProficiencyIds.All.Count, Is.EqualTo(9));
            Assert.That(
                BuiltInCharacterProficiencyIds.All
                    .Select(value => value.Value)
                    .Distinct(System.StringComparer.Ordinal)
                    .Count(),
                Is.EqualTo(9));
            Assert.That(
                BuiltInCharacterProficiencyIds.All.All(value => value.IsValid),
                Is.True);
        }

        [Test]
        public void AllThirtyOneWorkTypesHaveOneExplicitXpDisposition()
        {
            HashSet<WorkTypeId> noRoutineExperience = new()
            {
                BuiltInWorkTypeIds.Operate,
                BuiltInWorkTypeIds.Guard,
                BuiltInWorkTypeIds.Rest
            };
            Assert.That(WorkTypeCatalog.All.Count, Is.EqualTo(31));
            foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
            {
                bool mapped = WorkTypeProficiencyRules.TryResolve(
                    definition.WorkTypeId,
                    out ProficiencyWorkProfile profile);
                Assert.That(
                    mapped ^ noRoutineExperience.Contains(definition.WorkTypeId),
                    Is.True,
                    definition.Id);
                if (mapped)
                {
                    Assert.That(profile.Primary.IsValid, Is.True, definition.Id);
                }
            }
        }

        [Test]
        public void ApprovedWorkMatchesTargetRankCadence()
        {
            long daily = ProficiencyProgressionRules.CalculateWorkAwardMilli(
                99f,
                1f,
                ProficiencyWorkOutcome.Success,
                1f,
                1f);
            Assert.That(daily, Is.EqualTo(7920L));
            Assert.That(
                (ProficiencyProgressionRules.SkilledThreshold + daily - 1L) / daily,
                Is.EqualTo(13L));
            Assert.That(
                (ProficiencyProgressionRules.TechnicianThreshold + daily - 1L) / daily,
                Is.EqualTo(51L));
            Assert.That(
                (ProficiencyProgressionRules.ExpertThreshold + daily - 1L) / daily,
                Is.EqualTo(152L));
            Assert.That(
                (ProficiencyProgressionRules.MasterThreshold + daily - 1L) / daily,
                Is.EqualTo(379L));
        }

        [Test]
        public void WaitingAndCancelledWorkAwardNothing()
        {
            Assert.That(
                ProficiencyProgressionRules.CalculateWorkAwardMilli(
                    0f,
                    1f,
                    ProficiencyWorkOutcome.Success,
                    1f,
                    1f),
                Is.Zero);
            Assert.That(
                ProficiencyProgressionRules.CalculateWorkAwardMilli(
                    100f,
                    1f,
                    ProficiencyWorkOutcome.NoApprovedWork,
                    1f,
                    1f),
                Is.Zero);
        }

        [Test]
        public void ExpertAndMasterDecayAreLazyAndDemoteAtTheBoundary()
        {
            long masterDemotionHour =
                5L * GameCalendarRules.HoursPerDay + 610L;
            Assert.That(
                ProficiencyProgressionRules.SettleDecay(
                    ProficiencyProgressionRules.MasterCurrentCap,
                    0L,
                    0L,
                    masterDemotionHour),
                Is.EqualTo(ProficiencyProgressionRules.MasterThreshold - 1L));

            long expertDemotionHour =
                15L * GameCalendarRules.HoursPerDay + 1L;
            Assert.That(
                ProficiencyProgressionRules.SettleDecay(
                    ProficiencyProgressionRules.ExpertThreshold,
                    0L,
                    0L,
                    expertDemotionHour),
                Is.EqualTo(ProficiencyProgressionRules.ExpertThreshold - 1L));
        }

        [Test]
        public void CraftsmanshipUsesOnlyAuthoritativeProficiencyExperience()
        {
            Assert.That(
                ProficiencyProgressionRules.ResolveEffects(0L).QualityScore,
                Is.EqualTo(25f).Within(0.001f));
            Assert.That(
                ProficiencyProgressionRules.ResolveEffects(
                    ProficiencyProgressionRules.MasterThreshold).QualityScore,
                Is.EqualTo(95f).Within(0.001f));
            Assert.That(
                ProficiencyProgressionRules.ResolveEffects(
                    50L * ProficiencyProgressionRules.MilliPerExperience)
                    .QualityScore,
                Is.EqualTo(32.5f).Within(0.001f));
        }
    }
}
