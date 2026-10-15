using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class OffenseArrivalSettlementFactMapping
{
    public static MigratedDomainFactCapture Capture(
        OffenseArrivalSettledFact fact)
    {
        OffenseArrivalSettledFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        OffenseArrivalSettlementFactData data = value.Snapshot();
        OffenseExpeditionArrivalReceipt receipt = value.Receipt;

        var subjects = new List<MigratedProducerOutcomeSubject>();
        AddSubject(
            subjects,
            MigratedProducerOutcomeIds.ExpeditionKind,
            data.expeditionId,
            data.expeditionId,
            MigratedProducerOutcomeIds.ExpeditionRole);
        AddSubject(
            subjects,
            MigratedProducerOutcomeIds.WorldLocationKind,
            data.targetId,
            data.targetId,
            MigratedProducerOutcomeIds.TargetRole);
        foreach (string materializedId in data.materializedIds
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.Ordinal))
        {
            AddSubject(
                subjects,
                data.kind == OffenseReturnArrivalKind.Prisoner
                    ? MigratedProducerOutcomeIds.CharacterKind
                    : MigratedProducerOutcomeIds.WildlifeKind,
                materializedId,
                materializedId,
                MigratedProducerOutcomeIds.ArrivalRole);
        }

        GameplayEntityId expeditionEntity = subjects[0].EntityId;
        MigratedProducerOutcomeSubject[] subjectArray = subjects.ToArray();
        GameplayOutcomeMetric[] metrics =
        {
            Metric(
                MigratedProducerOutcomeIds.RequestedCountMetric,
                receipt.requestedAmount,
                expeditionEntity),
            Metric(
                MigratedProducerOutcomeIds.MaterializedCountMetric,
                receipt.materializedAmount,
                expeditionEntity),
            Metric(
                MigratedProducerOutcomeIds.SecuredCountMetric,
                receipt.securedAmount,
                expeditionEntity),
            Metric(
                MigratedProducerOutcomeIds.EscapedCountMetric,
                receipt.escapedAmount,
                expeditionEntity)
        };
        GameplayOutcomeFact[] facts =
        {
            new(
                MigratedProducerOutcomeIds.OperationFact,
                receipt.arrivalId),
            new(
                MigratedProducerOutcomeIds.DetailFact,
                "kind=" + receipt.kind
                + "; resolution=" + receipt.resolution)
        };
        if (subjectArray.Length is < 1 or > 16
            || metrics.Length > 16
            || facts.Length > 14)
        {
            throw new InvalidOperationException(
                "Offense detailed outcome exceeds the fixed ledger capacity: "
                + "subjects=" + Invariant(subjectArray.Length)
                + "; metrics=" + Invariant(metrics.Length)
                + "; facts=" + Invariant(facts.Length) + ".");
        }

        var details = new MigratedProducerOutcomeDetails(
            subjectArray,
            metrics,
            facts,
            "원정대 귀환 도착: arrival=" + receipt.arrivalId
            + "; expedition=" + data.expeditionId
            + "; target=" + data.targetId
            + "; kind=" + receipt.kind
            + "; secured=" + Invariant(receipt.securedAmount)
            + "; escaped=" + Invariant(receipt.escapedAmount),
            new GameplayOutcomeCausation(
                default,
                new GameplayOperationId(data.expeditionId),
                "expedition-arrival-resolved"),
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "domain-fact",
                    Digest(data))
            });
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.OffenseExpeditionArrivalReceipt,
            data.arrivalId,
            data.absoluteDay,
            Status(data),
            details);
    }

    public static string Digest(OffenseArrivalSettledFact fact) =>
        Digest((fact ?? throw new ArgumentNullException(nameof(fact))).Snapshot());

    private static GameplayOutcomeStatus Status(
        OffenseArrivalSettlementFactData data) => data.escapedAmount == 0
        ? GameplayOutcomeStatus.Succeeded
        : data.securedAmount > 0
            ? GameplayOutcomeStatus.PartiallySucceeded
            : GameplayOutcomeStatus.Failed;

    private static string Digest(OffenseArrivalSettlementFactData data)
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append("offense-arrival-settlement-fact-v1");
        digest.Append(data.arrivalId);
        digest.Append(data.expeditionId);
        digest.Append(data.targetId);
        digest.AppendEnum(data.kind);
        digest.Append(data.absoluteDay);
        digest.Append(data.requestedAmount);
        digest.Append(data.materializedAmount);
        digest.Append(data.securedAmount);
        digest.Append(data.escapedAmount);
        digest.Append(data.materializedIds.Count);
        foreach (string value in data.materializedIds)
            digest.Append(value);
        digest.Append(data.escapedIds.Count);
        foreach (string value in data.escapedIds)
            digest.Append(value);
        return digest.ComputeSha256();
    }

    private static void AddSubject(
        ICollection<MigratedProducerOutcomeSubject> subjects,
        GameplayEntityKindId kind,
        string stableId,
        string displayName,
        GameplayRoleId role)
    {
        string normalized = stableId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
            return;
        var candidate = new MigratedProducerOutcomeSubject(
            kind,
            normalized,
            string.IsNullOrWhiteSpace(displayName)
                ? normalized
                : displayName,
            role);
        if (subjects.Any(value => value.EntityId == candidate.EntityId))
            return;
        subjects.Add(candidate);
    }

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId id,
        double value,
        GameplayEntityId reference) => new(
        id,
        value,
        MigratedProducerOutcomeIds.CountUnit,
        reference);

    private static string Invariant(int value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
