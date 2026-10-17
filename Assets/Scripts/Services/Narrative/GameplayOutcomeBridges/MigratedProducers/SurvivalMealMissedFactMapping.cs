using System;
using System.Globalization;
using System.Text;

public static class SurvivalMealMissedFactMapping
{
    public static MigratedDomainFactCapture Capture(
        SurvivalMealMissedFact fact)
    {
        SurvivalMealMissedData data = (fact
            ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        data.Validate();

        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            data.characterId,
            data.characterId,
            MigratedProducerOutcomeIds.ActorRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            CreateSummary(data),
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "meal-missed-context-sha256",
                    ComputeContextSha256(data))
            });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.MealMissedEvent,
            CreateOwnerIdentity(data),
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static string ComputeContextSha256(
        SurvivalMealMissedData source)
    {
        SurvivalMealMissedData data = (source
            ?? throw new ArgumentNullException(nameof(source))).Clone();
        data.Validate();
        var canonical = new StringBuilder();
        Append(canonical, "survival-meal-missed-context-v1");
        Append(canonical, data.characterId);
        Append(canonical, data.absoluteDay);
        Append(canonical, data.completedDay);
        Append(canonical, data.consecutiveMisses);
        Append(canonical, data.cohortIndex);
        Append(canonical, data.cohortSize);
        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
    }

    private static string CreateOwnerIdentity(
        SurvivalMealMissedData data) => string.Join("|", new[]
    {
        "meal-missed@1",
        data.absoluteDay.ToString(CultureInfo.InvariantCulture),
        data.completedDay.ToString(CultureInfo.InvariantCulture),
        data.characterId,
        data.consecutiveMisses.ToString(CultureInfo.InvariantCulture)
    });

    private static string CreateSummary(
        SurvivalMealMissedData data) => string.Join("|", new[]
    {
        "meal-missed@1",
        data.characterId,
        data.completedDay.ToString(CultureInfo.InvariantCulture),
        data.consecutiveMisses.ToString(CultureInfo.InvariantCulture)
    });

    private static void Append(StringBuilder target, string value)
    {
        string text = value ?? string.Empty;
        target.Append(Encoding.UTF8.GetByteCount(text).ToString(
                CultureInfo.InvariantCulture))
            .Append(':')
            .Append(text)
            .Append('|');
    }

    private static void Append(StringBuilder target, int value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));
}
