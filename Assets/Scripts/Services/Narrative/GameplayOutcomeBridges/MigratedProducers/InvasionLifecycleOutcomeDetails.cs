using System;

internal static class InvasionLifecycleOutcomeDetails
{
    private static readonly GameplayOutcomeFactId StageFact =
        new("invasion-stage");

    internal static MigratedProducerOutcomeDetails Create(
        string runtimeId,
        string intruderId,
        string intruderName,
        string stage,
        int damageIndexBefore,
        int damageIndexAfter,
        InvasionIntruderState stateBefore,
        InvasionIntruderState stateAfter,
        float riskBefore,
        float riskAfter,
        string summary,
        GameplayOutcomeCausation causation,
        MigratedProducerOutcomeSubject additionalSubject = default,
        string reason = "",
        DurableFacilityEquipmentUseEvidence durableEquipment = null,
        bool reserveDurableEquipmentShape = false)
    {
        string requiredRuntimeId = GameplayOutcomeStableIdSyntax.Require(
            runtimeId,
            nameof(runtimeId));
        string requiredIntruderId = GameplayOutcomeStableIdSyntax.Require(
            intruderId,
            nameof(intruderId));
        string requiredStage = GameplayOutcomeStableIdSyntax.Require(
            stage,
            nameof(stage));
        if (!float.IsFinite(riskBefore) || riskBefore < 0f)
            throw new ArgumentOutOfRangeException(nameof(riskBefore));
        if (!float.IsFinite(riskAfter) || riskAfter < 0f)
            throw new ArgumentOutOfRangeException(nameof(riskAfter));
        if (damageIndexBefore < 0)
            throw new ArgumentOutOfRangeException(nameof(damageIndexBefore));
        if (damageIndexAfter < 0)
            throw new ArgumentOutOfRangeException(nameof(damageIndexAfter));

        var operation = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.OperationKind,
            requiredRuntimeId,
            requiredRuntimeId,
            MigratedProducerOutcomeIds.OperationRole);
        var intruder = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            requiredIntruderId,
            intruderName,
            MigratedProducerOutcomeIds.IntruderRole);
        MigratedProducerOutcomeSubject[] subjects =
            additionalSubject.EntityId.IsValid
                ? new[] { operation, intruder, additionalSubject }
                : new[] { operation, intruder };
        GameplayEntityId operationEntity = operation.EntityId;
        MigratedProducerOutcomeDetails details = new(
            subjects,
            new[]
            {
                Metric(
                    MigratedProducerOutcomeIds.DamageIndexBeforeMetric,
                    damageIndexBefore,
                    MigratedProducerOutcomeIds.CountUnit,
                    operationEntity),
                Metric(
                    MigratedProducerOutcomeIds.DamageIndexAfterMetric,
                    damageIndexAfter,
                    MigratedProducerOutcomeIds.CountUnit,
                    operationEntity),
                Metric(
                    MigratedProducerOutcomeIds.StateBeforeMetric,
                    (int)stateBefore,
                    MigratedProducerOutcomeIds.EnumUnit,
                    operationEntity),
                Metric(
                    MigratedProducerOutcomeIds.StateAfterMetric,
                    (int)stateAfter,
                    MigratedProducerOutcomeIds.EnumUnit,
                    operationEntity),
                Metric(
                    MigratedProducerOutcomeIds.RiskBeforeMetric,
                    riskBefore,
                    MigratedProducerOutcomeIds.PointUnit,
                    operationEntity),
                Metric(
                    MigratedProducerOutcomeIds.RiskAfterMetric,
                    riskAfter,
                    MigratedProducerOutcomeIds.PointUnit,
                    operationEntity)
            },
            string.IsNullOrWhiteSpace(reason)
                ? new[]
                {
                    new GameplayOutcomeFact(StageFact, requiredStage),
                    new GameplayOutcomeFact(
                        MigratedProducerOutcomeIds.BeforeFact,
                        stateBefore.ToString()),
                    new GameplayOutcomeFact(
                        MigratedProducerOutcomeIds.AfterFact,
                        stateAfter.ToString())
                }
                : new[]
                {
                    new GameplayOutcomeFact(StageFact, requiredStage),
                    new GameplayOutcomeFact(
                        MigratedProducerOutcomeIds.BeforeFact,
                        stateBefore.ToString()),
                    new GameplayOutcomeFact(
                        MigratedProducerOutcomeIds.AfterFact,
                        stateAfter.ToString()),
                    new GameplayOutcomeFact(
                        MigratedProducerOutcomeIds.ReasonFact,
                        reason.Trim())
                },
            summary,
            causation);
        if (durableEquipment != null)
        {
            return DurableFacilityEquipmentOutcomeEncoding.Append(
                details,
                durableEquipment);
        }
        return reserveDurableEquipmentShape
            ? DurableFacilityEquipmentOutcomeEncoding
                .AppendReservationShape(details)
            : details;
    }

    internal static MigratedProducerOutcomeSubject Facility(
        string facilityId,
        string displayName,
        bool defenseFacility = false) =>
        new(
            MigratedProducerOutcomeIds.FacilityKind,
            facilityId,
            displayName,
            defenseFacility
                ? MigratedProducerOutcomeIds.DefenseFacilityRole
                : MigratedProducerOutcomeIds.AttackedFacilityRole);

    internal static MigratedProducerOutcomeSubject Defender(
        CharacterActor actor)
    {
        CharacterId id = CharacterPersistentIdentity.Require(actor);
        return new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.CharacterKind,
            id.Value,
            actor.Identity?.DisplayName ?? actor.name,
            MigratedProducerOutcomeIds.DefenderRole);
    }

    private static GameplayOutcomeMetric Metric(
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId entity) => new(id, value, unit, entity);
}
