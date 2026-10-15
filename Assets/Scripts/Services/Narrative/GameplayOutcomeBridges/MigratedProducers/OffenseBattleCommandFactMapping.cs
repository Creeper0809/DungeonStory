using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class OffenseBattleCommandFactMapping
{
    private const string DigestSchema = "offense-battle-command-fact-v1";
    private const string ReservationSchema =
        "offense-battle-command-reservation-v1";

    public static MigratedDomainFactCapture Capture(
        OffenseBattleCommandFact fact)
    {
        OffenseBattleCommandFactData data = Snapshot(fact);
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.OffenseBattleCommandResult,
            OwnerIdentity(data),
            data.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            CreateDetails(data, Digest(data)),
            reservationOnly: !data.resolved);
    }

    public static string Digest(OffenseBattleCommandFact fact) =>
        Digest(Snapshot(fact));

    internal static string ReservationDigest(
        OffenseBattleCommandFact fact) =>
        ReservationDigest(Snapshot(fact));

    internal static string OwnerIdentity(
        OffenseBattleCommandFactData data) =>
        "offense-battle:" + data.battleId;

    private static MigratedProducerOutcomeDetails CreateDetails(
        OffenseBattleCommandFactData data,
        string digest)
    {
        var subjects = new List<MigratedProducerOutcomeSubject>();
        AddSubject(
            subjects,
            MigratedProducerOutcomeIds.CharacterKind,
            data.actorId,
            data.actorDisplayName,
            MigratedProducerOutcomeIds.ActorRole);
        if (!string.IsNullOrWhiteSpace(data.targetId))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.CharacterKind,
                data.targetId,
                data.targetDisplayName,
                MigratedProducerOutcomeIds.TargetRole);
        }
        if (!string.IsNullOrWhiteSpace(data.abilityId))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.AbilityKind,
                data.abilityId,
                data.abilityId,
                MigratedProducerOutcomeIds.AbilityRole);
        }
        if (!string.IsNullOrWhiteSpace(data.weaponInstanceId)
            || !string.IsNullOrWhiteSpace(data.weaponDefinitionId))
        {
            bool hasInstance = !string.IsNullOrWhiteSpace(
                data.weaponInstanceId);
            AddSubject(
                subjects,
                hasInstance
                    ? MigratedProducerOutcomeIds.ItemKind
                    : MigratedProducerOutcomeIds.ItemDefinitionKind,
                hasInstance
                    ? data.weaponInstanceId
                    : data.weaponDefinitionId,
                data.weaponDefinitionId,
                MigratedProducerOutcomeIds.EquipmentRole);
        }

        GameplayEntityId metricSubject = subjects
            .FirstOrDefault(value => value.Role.Equals(
                MigratedProducerOutcomeIds.TargetRole)).EntityId;
        if (!metricSubject.IsValid)
            metricSubject = subjects[0].EntityId;
        GameplayOutcomeMetric[] metrics =
        {
            Metric(
                MigratedProducerOutcomeIds.HealthBeforeMetric,
                data.targetHealthBefore,
                MigratedProducerOutcomeIds.PointUnit,
                metricSubject),
            Metric(
                MigratedProducerOutcomeIds.HealthAfterMetric,
                data.targetHealthAfter,
                MigratedProducerOutcomeIds.PointUnit,
                metricSubject),
            Metric(
                MigratedProducerOutcomeIds.AmountMetric,
                data.amount,
                MigratedProducerOutcomeIds.PointUnit,
                metricSubject),
            Metric(
                MigratedProducerOutcomeIds.DamageMetric,
                Math.Max(0d, (double)data.targetHealthBefore - data.targetHealthAfter),
                MigratedProducerOutcomeIds.PointUnit,
                metricSubject),
            Metric(
                MigratedProducerOutcomeIds.StatusMetric,
                data.hit ? 1d : 0d,
                MigratedProducerOutcomeIds.EnumUnit,
                metricSubject)
        };
        GameplayOutcomeFact[] facts =
        {
            new(
                MigratedProducerOutcomeIds.ActionFact,
                data.actionType.ToString()),
            new(
                MigratedProducerOutcomeIds.OperationFact,
                data.battleId + ":command:" + data.commandId),
            new(
                MigratedProducerOutcomeIds.TargetFact,
                string.IsNullOrWhiteSpace(data.targetId)
                    ? "none"
                    : data.targetId),
            new(
                MigratedProducerOutcomeIds.DetailFact,
                "hit=" + data.hit
                + "; shieldBlocked=" + data.shieldBlocked
                + "; coverBlocked=" + data.coverBlocked
                + "; ability=" + (string.IsNullOrWhiteSpace(data.abilityId)
                    ? "none"
                    : data.abilityId))
        };
        MigratedProducerOutcomeSubject[] subjectArray = subjects.ToArray();
        if (subjectArray.Length is < 1 or > 16
            || metrics.Length > 16
            || facts.Length > 14)
        {
            throw new InvalidOperationException(
                "Offense detailed outcome exceeds the fixed ledger capacity: "
                + "subjects=" + subjectArray.Length
                + "; metrics=" + metrics.Length
                + "; facts=" + facts.Length + ".");
        }

        return new MigratedProducerOutcomeDetails(
            subjectArray,
            metrics,
            facts,
            "원정 전투 명령: battle=" + data.battleId
            + "; actor=" + data.actorId
            + "; target=" + data.targetId
            + "; action=" + data.actionType
            + "; amount=" + data.amount.ToString(
                "0.###",
                CultureInfo.InvariantCulture),
            new GameplayOutcomeCausation(
                default,
                new GameplayOperationId(data.expeditionId),
                "expedition-battle-command"),
            provenance: new[]
            {
                new GameplayOutcomeProvenanceReference(
                    "domain-fact",
                    digest)
            });
    }

    private static string Digest(OffenseBattleCommandFactData data)
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append(DigestSchema);
        AppendStableContext(digest, data);
        digest.Append(data.weaponInstanceId);
        digest.Append(data.weaponDefinitionId);
        digest.Append(data.resolved);
        digest.Append(data.targetIsDead);
        digest.AppendFloat(data.targetHealthAfter);
        digest.AppendFloat(data.amount);
        digest.Append(data.hit);
        digest.Append(data.shieldBlocked);
        digest.Append(data.coverBlocked);
        return digest.ComputeSha256();
    }

    private static string ReservationDigest(
        OffenseBattleCommandFactData data)
    {
        var digest = new CanonicalSemanticDigestBuilder();
        digest.Append(ReservationSchema);
        AppendStableContext(digest, data);
        return digest.ComputeSha256();
    }

    private static void AppendStableContext(
        CanonicalSemanticDigestBuilder digest,
        OffenseBattleCommandFactData data)
    {
        digest.Append(data.battleId);
        digest.Append(data.expeditionId);
        digest.Append(data.actorId);
        digest.Append(data.actorDisplayName);
        digest.Append(data.targetId);
        digest.Append(data.targetDisplayName);
        digest.Append(data.abilityId);
        digest.Append(data.commandId);
        digest.Append(data.absoluteDay);
        digest.Append(data.absoluteHour);
        digest.AppendEnum(data.actionType);
        digest.Append(data.targetWasDead);
        digest.AppendFloat(data.targetHealthBefore);
    }

    private static OffenseBattleCommandFactData Snapshot(
        OffenseBattleCommandFact fact) =>
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
}
