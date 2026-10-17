using System;
using System.Globalization;

public static class ProficiencyAwardFactMapping
{
    public static MigratedDomainFactCapture Capture(CharacterProficiencyAwardCommitReceipt fact)
    {
        string summary = string.Join("|", new[]
        {
            "proficiency-award@1",
            ((int)fact.AwardKind).ToString(CultureInfo.InvariantCulture),
            fact.CharacterId.Value,
            fact.ProficiencyId.Value,
            fact.BeforeCurrentMilliExperience.ToString(CultureInfo.InvariantCulture),
            fact.AfterCurrentMilliExperience.ToString(CultureInfo.InvariantCulture),
            fact.BeforeLifetimeMilliExperience.ToString(CultureInfo.InvariantCulture),
            fact.AfterLifetimeMilliExperience.ToString(CultureInfo.InvariantCulture),
            ((int)fact.BeforeRank).ToString(CultureInfo.InvariantCulture),
            ((int)fact.AfterRank).ToString(CultureInfo.InvariantCulture),
            fact.AbsoluteHour.ToString(CultureInfo.InvariantCulture)
        });
        int day = checked((int)Math.Min(int.MaxValue,
            Math.Max(0L, fact.AbsoluteHour) / GameCalendarRules.HoursPerDay + 1L));
        return new MigratedDomainFactCapture(MigratedProducerOutcomeKind.CharacterProficiencyAwardCommitReceipt,
            fact.SourceOperationId, day, GameplayOutcomeStatus.Succeeded,
            CharacterSurvivalOutcomeDetails.Proficiency(fact, summary));
    }
}
