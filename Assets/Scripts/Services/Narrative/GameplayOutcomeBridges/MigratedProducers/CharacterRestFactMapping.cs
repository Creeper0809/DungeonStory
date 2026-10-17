using System;
using System.Globalization;
using System.Text;

public static class CharacterRestFactMapping
{
    private static readonly GameplayOutcomeFactId ContextSha256Fact =
        new("character-event-context-sha256");

    public static MigratedDomainFactCapture Capture(
        CharacterRestAppliedFact fact)
    {
        CharacterRestAppliedFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        CharacterRestAppliedFactData data = value.Snapshot();
        var characterId = new CharacterId(data.characterId);
        string summary = "수면 수치가 "
            + data.before.ToString("0.##", CultureInfo.InvariantCulture)
            + "에서 "
            + data.after.ToString("0.##", CultureInfo.InvariantCulture)
            + "로 회복됐다.";
        MigratedProducerOutcomeDetails legacy =
            CharacterSurvivalOutcomeDetails.Rest(
                characterId,
                data.displayName,
                data.before,
                data.after,
                data.activeConditions,
                summary);
        var facts = new GameplayOutcomeFact[legacy.Facts.Length + 1];
        Array.Copy(legacy.Facts, facts, legacy.Facts.Length);
        facts[facts.Length - 1] = new GameplayOutcomeFact(
            ContextSha256Fact,
            ComputeContextSha256(data.context));
        var details = new MigratedProducerOutcomeDetails(
            legacy.Subjects,
            legacy.Metrics,
            facts,
            legacy.Summary,
            legacy.Causation,
            legacy.SummaryFirst,
            legacy.Provenance);
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.RestOutcomeIdentityEvent,
            "rest-outcome:" + characterId.Value,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static string ComputeContextSha256(CharacterEventContext context)
    {
        if (context == null)
        {
            return NarrativeInferenceHash.ComputeSha256Utf8(
                "character-event-context-v1|null");
        }

        context.Validate();
        var canonical = new StringBuilder("character-event-context-v1|value|");
        Append(canonical, context.eventInstanceId);
        Append(canonical, context.chainId);
        Append(canonical, context.locationId);
        Append(canonical, context.locationDisplayName);
        Append(canonical, context.actorId);
        Append(canonical, context.actorDisplayName);
        Append(canonical, context.counterpartyId);
        Append(canonical, context.counterpartyDisplayName);
        Append(canonical, context.usedObjectId);
        Append(canonical, context.usedObjectDisplayName);
        Append(canonical, context.resultDetail);
        canonical.Append(context.occurredDay.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(context.sequence.ToString(CultureInfo.InvariantCulture));
        return NarrativeInferenceHash.ComputeSha256Utf8(canonical.ToString());
    }

    private static void Append(StringBuilder target, string value)
    {
        string text = value ?? string.Empty;
        target.Append(Encoding.UTF8.GetByteCount(text).ToString(
                CultureInfo.InvariantCulture))
            .Append(':')
            .Append(text)
            .Append('|');
    }
}
