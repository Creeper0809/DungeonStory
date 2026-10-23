using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal static class ContentResolutionFactMapping
{
    internal static MigratedDomainFactCapture Capture(
        ContentResolutionFact fact)
    {
        ContentResolutionFactData data = fact.Snapshot();
        V20ResolvedEventResult resolution =
            ContentResolutionFact.ToResult(data.resolution);
        string operationId = data.operationId.Trim();
        string index = data.resolutionIndex.ToString("D4");
        GameplayOperationId rootOperationId = new(
            "content-resolution:"
            + NarrativeInferenceHash.ComputeSha256Utf8(operationId));
        string childIdentity = rootOperationId.Value + ":resolution:" + index;
        MigratedProducerOutcomeDetails details = CreateDetails(
            operationId,
            rootOperationId,
            childIdentity,
            resolution,
            data,
            includeDurableEquipment: data.resolutionIndex == 0);

        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.V20ResolvedEventResult,
            operationId + ":resolution:" + index,
            Math.Max(1, data.absoluteDay),
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    private static MigratedProducerOutcomeDetails CreateDetails(
        string operationId,
        GameplayOperationId rootOperationId,
        string childIdentity,
        V20ResolvedEventResult resolution,
        ContentResolutionFactData data,
        bool includeDurableEquipment)
    {
        List<MigratedProducerOutcomeSubject> subjects = new();
        foreach (string characterId in resolution.ParticipantCharacterIds
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.Ordinal))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.CharacterKind,
                characterId,
                characterId,
                MigratedProducerOutcomeIds.ActorRole);
        }
        if (!string.IsNullOrWhiteSpace(resolution.ContextFactionId))
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.FactionKind,
                resolution.ContextFactionId,
                resolution.ContextFactionId,
                MigratedProducerOutcomeIds.FactionRole);
        }
        for (int index = 0; index < resolution.Effects.Count; index++)
        {
            V20ContentEffect effect = resolution.Effects[index];
            if (effect == null || !effect.IsValid) continue;
            V20EffectApplicationEvidence[] projections = resolution.Applications
                .Where(value => value.effectIndex == index && value.projectionVersion == 1).ToArray();
            if (projections.Length > 0)
            {
                foreach (V20EffectApplicationEvidence projection in projections)
                    AddSubject(subjects, new GameplayEntityKindId(projection.targetKindId),
                        projection.targetId, projection.targetId, new GameplayRoleId(projection.targetRoleId));
                continue;
            }
            // Explicit compatibility for facts written before projectionVersion 1.
            // New receipts carry their executing handler's frozen identity and unit.
            if (string.IsNullOrWhiteSpace(effect.targetId))
                continue;
            GameplayEntityKindId kind = effect.kind switch
            {
                V20ContentEffectKind.ItemGrant
                    or V20ContentEffectKind.ItemConsume =>
                    MigratedProducerOutcomeIds.ItemDefinitionKind,
                V20ContentEffectKind.FactionRapport
                    or V20ContentEffectKind.FactionGrievance
                    or V20ContentEffectKind.FactionObligation =>
                    MigratedProducerOutcomeIds.FactionKind,
                _ => MigratedProducerOutcomeIds.OperationKind
            };
            GameplayRoleId role = effect.kind switch
            {
                V20ContentEffectKind.ItemGrant =>
                    MigratedProducerOutcomeIds.RewardRole,
                V20ContentEffectKind.ItemConsume =>
                    MigratedProducerOutcomeIds.SourceRole,
                V20ContentEffectKind.FactionRapport
                    or V20ContentEffectKind.FactionGrievance
                    or V20ContentEffectKind.FactionObligation =>
                    MigratedProducerOutcomeIds.FactionRole,
                _ => MigratedProducerOutcomeIds.TargetRole
            };
            AddSubject(
                subjects,
                kind,
                effect.targetId,
                effect.targetId,
                role);
        }
        if (subjects.Count == 0)
        {
            AddSubject(
                subjects,
                MigratedProducerOutcomeIds.OperationKind,
                childIdentity,
                string.IsNullOrWhiteSpace(resolution.DisplayName)
                    ? resolution.DefinitionId
                    : resolution.DisplayName,
                MigratedProducerOutcomeIds.OperationRole);
        }

        List<GameplayOutcomeMetric> metrics = new();
        List<GameplayOutcomeFact> facts = new()
        {
            new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.OperationFact,
                operationId.Trim()),
            new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.IdentityFact,
                resolution.DefinitionId),
            new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.ActionFact,
                resolution.ResolutionId),
            new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.StateBeforeFingerprintFact,
                string.IsNullOrWhiteSpace(data.stateBeforeFingerprint)
                    ? "unavailable"
                    : data.stateBeforeFingerprint),
            new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.StateAfterFingerprintFact,
                string.IsNullOrWhiteSpace(data.stateAfterFingerprint)
                    ? "unavailable"
                    : data.stateAfterFingerprint)
        };
        if (!string.IsNullOrWhiteSpace(resolution.OccurrenceInstanceId))
        {
            facts.Add(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.OccurrenceFact,
                resolution.OccurrenceInstanceId));
        }

        int effectIndex = 0;
        foreach (V20ContentEffect effect in resolution.Effects
                     .Where(value => value != null && value.IsValid))
        {
            string childCommitId = ChildCommitId(data, effect.kind);
            if ((effect.kind is V20ContentEffectKind.Money
                    or V20ContentEffectKind.ItemGrant
                    or V20ContentEffectKind.ItemConsume)
                && string.IsNullOrWhiteSpace(childCommitId))
            {
                throw new InvalidOperationException(
                    "V20 transactional effect is missing its exact child commit identity: "
                    + effect.kind + ".");
            }
            V20EffectApplicationEvidence projection = resolution.Applications.FirstOrDefault(
                value => value.effectIndex == effectIndex && value.projectionVersion == 1);
            GameplayEntityId reference = projection != null
                ? new GameplayEntityId(new GameplayEntityKindId(projection.targetKindId), projection.targetId)
                : ResolveEffectReference(subjects, resolution, effect);
            metrics.Add(new GameplayOutcomeMetric(
                MigratedProducerOutcomeIds.AmountMetric,
                projection?.appliedAmount ?? effect.amount,
                projection != null ? new GameplayMetricUnitId(projection.metricUnitId) : EffectUnit(effect.kind),
                reference));
            facts.Add(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.EffectFact,
                "index=" + effectIndex
                + "; kind=" + effect.kind
                + "; target=" + (string.IsNullOrWhiteSpace(effect.targetId)
                    ? "participants"
                    : effect.targetId)
                + "; amount=" + effect.amount.ToString(
                    "R",
                    CultureInfo.InvariantCulture)
                + "; durationDays=" + effect.durationDays
                + "; childOutcomeId=" + (childCommitId.Length == 0
                    ? "none"
                    : childCommitId)));
            effectIndex++;
        }

        string[] effectDetails = facts
            .Where(value => value.FactId.Equals(
                MigratedProducerOutcomeIds.EffectFact))
            .Select(value => value.Value)
            .ToArray();
        facts.RemoveAll(value => value.FactId.Equals(
            MigratedProducerOutcomeIds.EffectFact));
        if (effectDetails.Length > 0)
        {
            string aggregate = string.Join(" | ", effectDetails);
            if (aggregate.Length
                > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length)
            {
                throw new InvalidOperationException(
                    "V20 effect evidence exceeds the fixed ledger fact-value capacity: utf16="
                    + aggregate.Length + ".");
            }
            facts.Add(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.EffectFact,
                aggregate));
        }

        string[] applicationDetails = resolution.Applications
            .Select(application =>
                "effectIndex=" + application.effectIndex
                + "; target=" + application.targetId
                + "; status=" + application.status.ToString()
                    .ToLowerInvariant()
                + "; requested=" + FormatFloat(application.requestedAmount)
                + "; applied=" + FormatFloat(application.appliedAmount)
                + "; before=" + application.beforeValue
                + "; after=" + application.afterValue
                + "; semantic=" + application.semantic
                + "; childOutcomeId="
                + (string.IsNullOrWhiteSpace(application.childCommitId)
                    ? "none"
                    : application.childCommitId))
            .ToArray();
        if (applicationDetails.Length > 0)
        {
            string aggregate = string.Join(" | ", applicationDetails);
            if (aggregate.Length
                > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length)
            {
                throw new InvalidOperationException(
                    "V20 application evidence exceeds the fixed ledger fact-value capacity: utf16="
                    + aggregate.Length + ".");
            }
            facts.Add(new GameplayOutcomeFact(
                MigratedProducerOutcomeIds.EffectApplicationFact,
                aggregate));
        }
        if (subjects.Count > 16 || metrics.Count > 16 || facts.Count > 14)
        {
            throw new InvalidOperationException(
                "V20 resolved-event evidence exceeds fixed ledger capacity: "
                + "subjects=" + subjects.Count
                + "; metrics=" + metrics.Count
                + "; facts=" + facts.Count + ".");
        }

        MigratedProducerOutcomeDetails details = new(
            subjects.ToArray(),
            metrics.ToArray(),
            facts.ToArray(),
            "캠페인 사건 해결: definition=" + resolution.DefinitionId
            + "; resolution=" + resolution.ResolutionId
            + "; participants=" + resolution.ParticipantCharacterIds.Count
            + "; effects=" + effectIndex
            + "; faction=" + (string.IsNullOrWhiteSpace(
                resolution.ContextFactionId)
                ? "none"
                : resolution.ContextFactionId),
            new GameplayOutcomeCausation(
                default,
                rootOperationId,
                "campaign-event-effect-resolved"));
        return DurableFacilityEquipmentOutcomeEncoding.Append(
            details,
            includeDurableEquipment
                ? data.durableEquipmentStackId
                : string.Empty,
            includeDurableEquipment
                ? data.durableEquipmentItemId
                : string.Empty,
            includeDurableEquipment
                ? data.durableEquipmentContext
                : string.Empty);
    }

    private static string ChildCommitId(
        ContentResolutionFactData data,
        V20ContentEffectKind kind) => kind switch
    {
        V20ContentEffectKind.Money => data.moneyTransactionId,
        V20ContentEffectKind.ItemConsume => data.itemConsumptionOperationId,
        V20ContentEffectKind.ItemGrant => data.itemGrantBatchCommitId,
        _ => string.Empty
    };

    private static GameplayEntityId ResolveEffectReference(
        IReadOnlyList<MigratedProducerOutcomeSubject> subjects,
        V20ResolvedEventResult resolution,
        V20ContentEffect effect)
    {
        if (RequiresCharacter(effect.kind))
        {
            string participant = resolution.ParticipantCharacterIds
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (!string.IsNullOrWhiteSpace(participant))
            {
                return new GameplayEntityId(
                    MigratedProducerOutcomeIds.CharacterKind,
                    participant);
            }
        }
        if (!string.IsNullOrWhiteSpace(effect.targetId))
        {
            MigratedProducerOutcomeSubject target = subjects.FirstOrDefault(
                value => string.Equals(
                    value.EntityId.Value,
                    effect.targetId,
                    StringComparison.Ordinal));
            if (target.EntityId.IsValid)
                return target.EntityId;
        }
        return subjects[0].EntityId;
    }

    private static GameplayMetricUnitId EffectUnit(
        V20ContentEffectKind kind) => V21ContentEffectExecutionRegistry.Default.Require(kind).MetricUnit;

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
        MigratedProducerOutcomeSubject candidate = new(
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

    private static bool RequiresCharacter(V20ContentEffectKind kind) =>
        V21ContentEffectExecutionRegistry.Default.Require(kind).MinimumParticipants > 0;

    private static string FormatFloat(float value) => value.ToString(
        "R",
        CultureInfo.InvariantCulture);
}
