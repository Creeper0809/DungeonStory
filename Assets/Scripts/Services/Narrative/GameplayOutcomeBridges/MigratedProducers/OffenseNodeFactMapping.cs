using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class OffenseNodeFactMapping
{
    private const string DigestSchema = "offense-node-resolution-fact-v1";
    private const string ReservationSchema =
        "offense-node-resolution-reservation-v1";

    public static MigratedDomainFactCapture Capture(
        OffenseNodeResolutionFact fact)
    {
        OffenseNodeResolutionFactData data = Snapshot(fact);
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.OffenseExpeditionNodeResult,
            OwnerIdentity(data),
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            CreateDetails(data, Digest(data)),
            reservationOnly: !data.resolved);
    }

    public static string Digest(OffenseNodeResolutionFact fact) =>
        Digest(Snapshot(fact));

    internal static string ReservationDigest(
        OffenseNodeResolutionFact fact) =>
        ReservationDigest(Snapshot(fact));

    internal static string OwnerIdentity(
        OffenseNodeResolutionFactData data) =>
        "offense-expedition-node:" + data.expeditionId;

    private static MigratedProducerOutcomeDetails CreateDetails(
        OffenseNodeResolutionFactData data,
        string digest)
    {
        var subjects = new List<MigratedProducerOutcomeSubject>();
        AddSubject(
            subjects,
            MigratedProducerOutcomeIds.ExpeditionKind,
            data.expeditionId,
            data.targetTitle,
            MigratedProducerOutcomeIds.ExpeditionRole);
        AddSubject(
            subjects,
            MigratedProducerOutcomeIds.WorldLocationKind,
            data.targetId,
            data.targetTitle,
            MigratedProducerOutcomeIds.TargetRole);
        if (!string.IsNullOrWhiteSpace(data.regionId))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.WorldLocationKind,
                data.regionId,
                data.regionDisplayName,
                MigratedProducerOutcomeIds.DestinationRole);
        }
        if (!string.IsNullOrWhiteSpace(data.factionId))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.FactionKind,
                data.factionId,
                data.factionId,
                MigratedProducerOutcomeIds.FactionRole);
        }
        foreach (OffenseNodeParticipantData participant in data.participants)
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.CharacterKind,
                participant.characterId,
                participant.displayName,
                MigratedProducerOutcomeIds.MemberRole);
        }
        AddSubject(
            subjects,
            MigratedProducerOutcomeIds.WorldLocationKind,
            data.nodeId,
            data.nodeTitle,
            MigratedProducerOutcomeIds.TargetRole);

        GameplayEntityId expeditionEntity = subjects[0].EntityId;
        MigratedProducerOutcomeSubject[] subjectArray = subjects.ToArray();
        GameplayOutcomeMetric[] metrics =
        {
            Metric(
                MigratedProducerOutcomeIds.StatusMetric,
                data.usedSupply ? 1d : 0d,
                MigratedProducerOutcomeIds.EnumUnit,
                expeditionEntity),
            Metric(
                MigratedProducerOutcomeIds.OutputQuantityMetric,
                data.gainedLoot ? 1d : 0d,
                MigratedProducerOutcomeIds.CountUnit,
                expeditionEntity)
        };
        GameplayOutcomeFact[] facts =
        {
            new(
                MigratedProducerOutcomeIds.NodeFact,
                data.nodeId),
            new(
                MigratedProducerOutcomeIds.DetailFact,
                "kind=" + data.nodeKind
                + "; usedSupply=" + data.usedSupply
                + "; gainedLoot=" + data.gainedLoot)
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

        return new MigratedProducerOutcomeDetails(
            subjectArray,
            metrics,
            facts,
            "원정 지점 해결: expedition=" + data.expeditionId
            + "; node=" + data.nodeId
            + "; kind=" + data.nodeKind,
            new GameplayOutcomeCausation(
                default,
                new GameplayOperationId(data.expeditionId),
                "expedition-node-resolved"),
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "domain-fact",
                    digest)
            });
    }

    private static string Digest(OffenseNodeResolutionFactData data)
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append(DigestSchema);
        AppendStableContext(digest, data);
        digest.Append(data.resolved);
        digest.Append(data.usedSupply);
        digest.Append(data.gainedLoot);
        digest.Append(data.message);
        return digest.ComputeSha256();
    }

    private static string ReservationDigest(
        OffenseNodeResolutionFactData data)
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append(ReservationSchema);
        AppendStableContext(digest, data);
        return digest.ComputeSha256();
    }

    private static void AppendStableContext(
        CanonicalSemanticDigestBuilder digest,
        OffenseNodeResolutionFactData data)
    {
        digest.Append(data.expeditionId);
        digest.Append(data.targetId);
        digest.Append(data.targetTitle);
        digest.Append(data.regionId);
        digest.Append(data.regionDisplayName);
        digest.Append(data.factionId);
        digest.Append(data.nodeId);
        digest.Append(data.nodeTitle);
        digest.Append(data.nodeDescription);
        digest.AppendEnum(data.nodeKind);
        digest.Append(data.absoluteDay);
        digest.Append(data.sequence);
        digest.Append(data.useSupply);
        digest.Append(data.experienceReward);
        digest.Append(data.participants.Count);
        foreach (OffenseNodeParticipantData participant in data.participants)
        {
            digest.Append(participant.characterId);
            digest.Append(participant.displayName);
            digest.Append(participant.experienceEligible);
            digest.Append(participant.experienceDisplayName);
        }
    }

    private static OffenseNodeResolutionFactData Snapshot(
        OffenseNodeResolutionFact fact) =>
        (fact ?? throw new ArgumentNullException(nameof(fact))).Snapshot();

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
        GameplayMetricUnitId unit,
        GameplayEntityId reference) => new(id, value, unit, reference);

    private static string Invariant(int value) =>
        value.ToString(CultureInfo.InvariantCulture);
}
