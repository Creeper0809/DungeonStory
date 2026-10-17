using System;
using System.Globalization;
using System.Text;

public static class WildlifeCarcassProcessedFactMapping
{
    public static MigratedDomainFactCapture Capture(
        WildlifeCarcassProcessedFact fact)
    {
        WildlifeCarcassProcessingData data = (fact
            ?? throw new ArgumentNullException(nameof(fact))).Snapshot();
        data.Validate();

        MigratedProducerOutcomeKind kind = data.taboo
            ? MigratedProducerOutcomeKind.CharacterTabooIncidentEvent
            : MigratedProducerOutcomeKind.AnimalHusbandryWorkResult;
        MigratedProducerOutcomeSubject subject = data.actorId.Length > 0
            ? new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.CharacterKind,
                data.actorId,
                data.actorDisplayName,
                MigratedProducerOutcomeIds.ActorRole)
            : new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.OperationKind,
                data.operationId,
                data.speciesDisplayName + " 사체 도축",
                MigratedProducerOutcomeIds.OperationRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            CreateOutcomeSummary(data),
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "carcass-processing-context-sha256",
                    ComputeContextSha256(data)),
                new GameplayOutcomeProvenanceReference(
                    "domain-commit",
                    data.operationId)
            });
        return new MigratedDomainFactCapture(
            kind,
            data.operationId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static string ComputeContextSha256(
        WildlifeCarcassProcessingData source)
    {
        WildlifeCarcassProcessingData data = (source
            ?? throw new ArgumentNullException(nameof(source))).Clone();
        data.Validate();

        var canonical = new StringBuilder();
        Append(canonical, "wildlife-carcass-processing-context-v1");
        Append(canonical, data.operationId);
        Append(canonical, data.transformOperationId);
        Append(canonical, data.absoluteDay);
        Append(canonical, data.taboo);
        Append(canonical, data.actorId);
        Append(canonical, data.actorDisplayName);
        Append(canonical, data.speciesId);
        Append(canonical, data.speciesDisplayName);
        Append(canonical, data.carcassStackId);
        Append(canonical, data.facilityId);
        Append(canonical, data.inputQuantity);
        Append(canonical, data.outputQuantity);
        Append(canonical, data.sourceCharacterId);
        Append(canonical, data.sourceDisplayName);
        Append(canonical, data.sameSpecies);
        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
    }

    private static string CreateOutcomeSummary(
        WildlifeCarcassProcessingData data)
    {
        if (data.taboo)
        {
            return $"sourceCharacter={data.sourceCharacterId}; "
                + $"sourceName={data.sourceDisplayName}; "
                + $"sameSpecies={data.sameSpecies}; "
                + $"produced={data.outputQuantity}";
        }

        return $"event=carcass-butchered;speciesId={data.speciesId};"
            + $"speciesName={data.speciesDisplayName};"
            + $"carcassStackId={data.carcassStackId};"
            + $"facilityId={(data.facilityId.Length > 0 ? data.facilityId : "none")};"
            + $"inputQuantity={data.inputQuantity};"
            + $"outputQuantity={data.outputQuantity}";
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

    private static void Append(StringBuilder target, int value) =>
        Append(target, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder target, bool value) =>
        Append(target, value ? "1" : "0");
}
