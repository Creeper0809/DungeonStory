using System;
using System.Globalization;
using System.Text;

public static class PrimitiveSurvivalCompletedFactMapping
{
    public static MigratedDomainFactCapture Capture(
        PrimitiveSurvivalCompletedFact fact)
    {
        PrimitiveSurvivalCompletedFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        PrimitiveSurvivalPendingSaveData data = value.Snapshot();
        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            data.characterId,
            data.characterName,
            MigratedProducerOutcomeIds.ActorRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            $"action={data.actionId}; recovery={data.recovery:0.###}; physicalItemCount={data.physicalItemCount}",
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "primitive-survival-context-sha256",
                    ComputeContextSha256(data)),
                // The gameplay occurrence is independent of the ledger operation.
                // Preserve its explicit alias for need/mood evidence deduplication.
                new GameplayOutcomeProvenanceReference("domain-commit", data.operationId)
            });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind
                .CharacterPrimitiveSurvivalCompletedEvent,
            "primitive-survival:" + data.characterId,
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details,
            reservationOnly: value.ReservationOnly);
    }

    public static string ComputeContextSha256(
        PrimitiveSurvivalPendingSaveData source)
    {
        PrimitiveSurvivalPendingSaveData data = (source
            ?? throw new ArgumentNullException(nameof(source))).Clone();
        var canonical = new StringBuilder();
        Append(canonical, "primitive-survival-completed-context-v1");
        Append(canonical, data.actionId);
        Append(canonical, data.operationId);
        Append(canonical, data.characterId);
        Append(canonical, data.characterName);
        Append(canonical, data.restPublicationKey);
        Append(canonical, data.absoluteDay);
        Append(canonical, data.recovery);
        Append(canonical, data.physicalItemCount);
        Append(canonical, data.sleepAfter);
        Append(canonical, data.hygieneAfter);
        Append(canonical, data.excretionAfter);
        Append(canonical, data.targetGridX);
        Append(canonical, data.targetGridY);
        Append(canonical, data.wasteFilthId);
        Append(canonical, data.wasteAmountAfter);
        Append(canonical, data.stainFilthId);
        Append(canonical, data.stainAmountAfter);
        Append(canonical, data.waterStackId);
        return NarrativeInferenceHash.ComputeSha256Utf8(
            canonical.ToString());
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

    private static void Append(StringBuilder target, float value) =>
        Append(target, value.ToString("R", CultureInfo.InvariantCulture));
}
