using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class MigratedProducerOutcomeIds
{
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayEntityKindId ItemKind = new("item-instance");
    public static readonly GameplayEntityKindId FactionKind = new("faction");
    public static readonly GameplayEntityKindId ExpeditionKind = new("expedition");
    public static readonly GameplayEntityKindId RouteKind = new("route");
    public static readonly GameplayEntityKindId RunKind = new("run");
    public static readonly GameplayEntityKindId OperationKind = new("operation");
    public static readonly GameplayEntityKindId ItemDefinitionKind =
        new("item-definition");
    public static readonly GameplayEntityKindId WorldLocationKind =
        new("world-location");
    public static readonly GameplayEntityKindId AbilityKind =
        new("ability");
    public static readonly GameplayEntityKindId WildlifeKind =
        new("wildlife");
    public static readonly GameplayEntityKindId ItemStackKind =
        new("item-stack");
    public static readonly GameplayEntityKindId WaterSourceKind =
        new("water-source");
    public static readonly GameplayEntityKindId ProficiencyKind =
        new("proficiency");

    public static readonly GameplayRoleId ActorRole = new("actor");
    public static readonly GameplayRoleId TargetRole = new("target");
    public static readonly GameplayRoleId FacilityRole = new("facility");
    public static readonly GameplayRoleId ItemRole = new("item");
    public static readonly GameplayRoleId FactionRole = new("faction");
    public static readonly GameplayRoleId ExpeditionRole = new("expedition");
    public static readonly GameplayRoleId RouteRole = new("route");
    public static readonly GameplayRoleId OwnerRole = new("owner");
    public static readonly GameplayRoleId CustomerRole = new("customer");
    public static readonly GameplayRoleId SourceRole = new("source");
    public static readonly GameplayRoleId DestinationRole = new("destination");
    public static readonly GameplayRoleId WitnessRole = new("witness");
    public static readonly GameplayRoleId BetrayerRole = new("betrayer");
    public static readonly GameplayRoleId OperationRole = new("operation");
    public static readonly GameplayRoleId IntruderRole = new("intruder");
    public static readonly GameplayRoleId AttackedFacilityRole =
        new("attacked-facility");
    public static readonly GameplayRoleId DefenderRole = new("defender");
    public static readonly GameplayRoleId DefenseFacilityRole =
        new("defense-facility");
    public static readonly GameplayRoleId MemberRole = new("member");
    public static readonly GameplayRoleId AbilityRole = new("ability");
    public static readonly GameplayRoleId EquipmentRole = new("equipment");
    public static readonly GameplayRoleId RewardRole = new("reward");
    public static readonly GameplayRoleId ArrivalRole = new("arrival");

    public static readonly GameplayMetricId AmountMetric = new("amount");
    public static readonly GameplayMetricId PreviousMetric = new("previous");
    public static readonly GameplayMetricId CurrentMetric = new("current");
    public static readonly GameplayMetricId DeltaMetric = new("delta");
    public static readonly GameplayMetricId CountMetric = new("count");
    public static readonly GameplayMetricId QuantityMetric = new("quantity");
    public static readonly GameplayMetricId SecuredCountMetric =
        new("secured-count");
    public static readonly GameplayMetricId ExpiredCountMetric =
        new("expired-count");
    public static readonly GameplayMetricId MassMetric = new("mass");
    public static readonly GameplayMetricId DamageMetric = new("damage");
    public static readonly GameplayMetricId RatioMetric = new("ratio");
    public static readonly GameplayMetricId DurationMetric = new("duration");
    public static readonly GameplayMetricId DayMetric = new("day");
    public static readonly GameplayMetricId RevisionMetric = new("revision");
    public static readonly GameplayMetricId StatusMetric = new("status");
    public static readonly GameplayMetricId ValueMetric = new("value");
    public static readonly GameplayMetricId CurrentExperienceBeforeMetric =
        new("current-experience-before");
    public static readonly GameplayMetricId CurrentExperienceAfterMetric =
        new("current-experience-after");
    public static readonly GameplayMetricId LifetimeExperienceBeforeMetric =
        new("lifetime-experience-before");
    public static readonly GameplayMetricId LifetimeExperienceAfterMetric =
        new("lifetime-experience-after");
    public static readonly GameplayMetricId RankBeforeMetric =
        new("rank-before");
    public static readonly GameplayMetricId RankAfterMetric =
        new("rank-after");
    public static readonly GameplayMetricId AbsoluteHourMetric =
        new("absolute-hour");
    public static readonly GameplayMetricId RequiredWorkMetric =
        new("required-work-milli-units");
    public static readonly GameplayMetricId CompletedWorkMetric =
        new("completed-work-milli-units");
    public static readonly GameplayMetricId InputQuantityMetric =
        new("input-quantity");
    public static readonly GameplayMetricId OutputQuantityMetric =
        new("output-quantity");
    public static readonly GameplayMetricId InputMassMetric =
        new("input-mass");
    public static readonly GameplayMetricId OutputMassMetric =
        new("output-mass");
    public static readonly GameplayMetricId DamageIndexBeforeMetric =
        new("damage-index-before");
    public static readonly GameplayMetricId DamageIndexAfterMetric =
        new("damage-index-after");
    public static readonly GameplayMetricId StateBeforeMetric =
        new("state-before");
    public static readonly GameplayMetricId StateAfterMetric =
        new("state-after");
    public static readonly GameplayMetricId RiskBeforeMetric =
        new("risk-before");
    public static readonly GameplayMetricId RiskAfterMetric =
        new("risk-after");
    public static readonly GameplayMetricId TrustBeforeMetric =
        new("trust-before");
    public static readonly GameplayMetricId TrustAfterMetric =
        new("trust-after");
    public static readonly GameplayMetricId StrengthBeforeMetric =
        new("strength-before");
    public static readonly GameplayMetricId StrengthAfterMetric =
        new("strength-after");
    public static readonly GameplayMetricId DelayBeforeMetric =
        new("delay-before");
    public static readonly GameplayMetricId DelayAfterMetric =
        new("delay-after");
    public static readonly GameplayMetricId HealthBeforeMetric =
        new("health-before");
    public static readonly GameplayMetricId HealthAfterMetric =
        new("health-after");
    public static readonly GameplayMetricId PowerMetric = new("power");
    public static readonly GameplayMetricId RequiredPowerMetric =
        new("required-power");
    public static readonly GameplayMetricId DangerMetric = new("danger");
    public static readonly GameplayMetricId RequestedCountMetric =
        new("requested-count");
    public static readonly GameplayMetricId MaterializedCountMetric =
        new("materialized-count");
    public static readonly GameplayMetricId EscapedCountMetric =
        new("escaped-count");
    public static readonly GameplayMetricId HungerBeforeMetric =
        new("hunger-before");
    public static readonly GameplayMetricId HungerAfterMetric =
        new("hunger-after");
    public static readonly GameplayMetricId ThirstBeforeMetric =
        new("thirst-before");
    public static readonly GameplayMetricId ThirstAfterMetric =
        new("thirst-after");
    public static readonly GameplayMetricId HygieneBeforeMetric =
        new("hygiene-before");
    public static readonly GameplayMetricId HygieneAfterMetric =
        new("hygiene-after");
    public static readonly GameplayMetricId InfectionBeforeMetric =
        new("infection-before");
    public static readonly GameplayMetricId InfectionAfterMetric =
        new("infection-after");
    public static readonly GameplayMetricId MoodBeforeMetric =
        new("mood-before");
    public static readonly GameplayMetricId MoodAfterMetric =
        new("mood-after");
    public static readonly GameplayMetricId SleepBeforeMetric =
        new("sleep-before");
    public static readonly GameplayMetricId SleepAfterMetric =
        new("sleep-after");
    public static readonly GameplayMetricId FunBeforeMetric =
        new("fun-before");
    public static readonly GameplayMetricId FunAfterMetric =
        new("fun-after");
    public static readonly GameplayMetricId ExcretionBeforeMetric =
        new("excretion-before");
    public static readonly GameplayMetricId ExcretionAfterMetric =
        new("excretion-after");
    public static readonly GameplayMetricId InjuryBeforeMetric =
        new("injury-before");
    public static readonly GameplayMetricId InjuryAfterMetric =
        new("injury-after");
    public static readonly GameplayMetricId StressBeforeMetric =
        new("stress-before");
    public static readonly GameplayMetricId StressAfterMetric =
        new("stress-after");

    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId RatioUnit = new("ratio");
    public static readonly GameplayMetricUnitId CurrencyUnit = new("currency");
    public static readonly GameplayMetricUnitId DayUnit = new("day");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricUnitId GramUnit = new("gram");
    public static readonly GameplayMetricUnitId SecondUnit = new("second");
    public static readonly GameplayMetricUnitId EnumUnit = new("enum");
    public static readonly GameplayMetricUnitId HourUnit = new("hour");
    public static readonly GameplayMetricUnitId MilliExperienceUnit =
        new("milli-experience");
    public static readonly GameplayMetricUnitId MilliWorkUnit =
        new("milli-work-unit");

    public static readonly GameplayOutcomeFactId SourceTypeFact = new("source-type");
    public static readonly GameplayOutcomeFactId SummaryFact = new("summary");
    public static readonly GameplayOutcomeFactId ReasonFact = new("reason-code");
    public static readonly GameplayOutcomeFactId DetailFact = new("detail");
    public static readonly GameplayOutcomeFactId IdentityFact = new("identity");
    public static readonly GameplayOutcomeFactId BeforeFact = new("state-before");
    public static readonly GameplayOutcomeFactId AfterFact = new("state-after");
    public static readonly GameplayOutcomeFactId OperationFact = new("operation-id");
    public static readonly GameplayOutcomeFactId FacilityFact = new("facility-id");
    public static readonly GameplayOutcomeFactId HouseholdFact = new("household-id");
    public static readonly GameplayOutcomeFactId CharacterFact = new("character-id");
    public static readonly GameplayOutcomeFactId SpeciesFact = new("species-id");
    public static readonly GameplayOutcomeFactId OccurrenceFact =
        new("occurrence-id");
    public static readonly GameplayOutcomeFactId RecipeFact = new("recipe-id");
    public static readonly GameplayOutcomeFactId BillFact = new("bill-id");
    public static readonly GameplayOutcomeFactId BatchFact = new("batch-commit-id");
    public static readonly GameplayOutcomeFactId FingerprintFact = new("outcome-fingerprint");
    public static readonly GameplayOutcomeFactId ProficiencyFact = new("proficiency-id");
    public static readonly GameplayOutcomeFactId RetirementEventFact = new("retirement-event-id");
    public static readonly GameplayOutcomeFactId RetirementChoiceFact = new("retirement-choice-id");
    public static readonly GameplayOutcomeFactId EquipmentDefinitionFact = new("equipment-definition-id");
    public static readonly GameplayOutcomeFactId AwardKindFact =
        new("award-kind");
    public static readonly GameplayOutcomeFactId WorkOrderFact =
        new("work-order-id");
    public static readonly GameplayOutcomeFactId TerminalKindFact =
        new("terminal-kind");
    public static readonly GameplayOutcomeFactId ConstructionSiteFact =
        new("construction-site-id");
    public static readonly GameplayOutcomeFactId BuildingDefinitionFact =
        new("building-definition-id");
    public static readonly GameplayOutcomeFactId FinalFacilityFact =
        new("final-facility-id");
    public static readonly GameplayOutcomeFactId ReplacedFacilityFact =
        new("replaced-facility-id");
    public static readonly GameplayOutcomeFactId SupportFacilitiesFact =
        new("support-facility-ids");
    public static readonly GameplayOutcomeFactId ContributorsFact =
        new("contributors-and-work");
    public static readonly GameplayOutcomeFactId RequiredMaterialsFact =
        new("required-materials");
    public static readonly GameplayOutcomeFactId MaterialInputOperationFact =
        new("material-input-operation-id");
    public static readonly GameplayOutcomeFactId RestitutionOperationFact =
        new("restitution-operation-id");
    public static readonly GameplayOutcomeFactId PlacementOperationFact =
        new("placement-operation-id");
    public static readonly GameplayOutcomeFactId QualityFact =
        new("quality-tier");
    public static readonly GameplayOutcomeFactId ActionFact =
        new("action-id");
    public static readonly GameplayOutcomeFactId TargetFact =
        new("target-id");
    public static readonly GameplayOutcomeFactId NodeFact =
        new("node-id");
    public static readonly GameplayOutcomeFactId EffectFact =
        new("effect-detail");
    public static readonly GameplayOutcomeFactId EffectApplicationFact =
        new("effect-application");
    public static readonly GameplayOutcomeFactId RewardFact =
        new("reward-detail");
    public static readonly GameplayOutcomeFactId CampaignFact =
        new("campaign-transition");
    public static readonly GameplayOutcomeFactId StateBeforeFingerprintFact =
        new("state-before-fingerprint");
    public static readonly GameplayOutcomeFactId StateAfterFingerprintFact =
        new("state-after-fingerprint");
    public static readonly GameplayOutcomeFactId ItemDefinitionFact =
        new("item-definition-id");
    public static readonly GameplayOutcomeFactId SourceStackFact =
        new("source-stack-ids");
    public static readonly GameplayOutcomeFactId PhysicalCommitFact =
        new("physical-commit-id");
    public static readonly GameplayOutcomeFactId WaterQualityFact =
        new("quality");
    public static readonly GameplayOutcomeFactId PathogenFact =
        new("pathogen-id");
    public static readonly GameplayOutcomeFactId ActiveConditionsFact =
        new("active-condition-ids");
    public static readonly GameplayOutcomeFactId SourceOperationFact =
        new("source-operation-id");
    public static readonly GameplayOutcomeFactId ServiceSessionFact =
        new("service-session-id");
    public static readonly GameplayOutcomeFactId FacilityRoleFact =
        new("facility-role");
    public static readonly GameplayOutcomeFactId OutcomeStatusFact =
        new("outcome-status");
    public static readonly GameplayOutcomeFactId ChildResultKeysFact =
        new("child-result-keys");
    public static readonly GameplayOutcomeFactId InjuryBeforeFact =
        new("injury-before");
    public static readonly GameplayOutcomeFactId InjuryAfterFact =
        new("injury-after");
    public static readonly GameplayOutcomeFactId MaximumHealthFact =
        new("maximum-health-before-after");

    public static readonly GameplayOutcomeProvenanceReference ProducerMigrationProvenance =
        new("capture-contract", "migrated-producer-v1");

    private static readonly GameplayRoleId[] KnownRolesInternal =
    {
        ActorRole, TargetRole, FacilityRole, ItemRole, FactionRole,
        ExpeditionRole, RouteRole, OwnerRole, CustomerRole, SourceRole,
        DestinationRole, WitnessRole, BetrayerRole, OperationRole
        , IntruderRole, AttackedFacilityRole, DefenderRole,
        DefenseFacilityRole, MemberRole, AbilityRole, EquipmentRole,
        RewardRole, ArrivalRole
    };

    private static readonly GameplayMetricId[] KnownMetricsInternal =
    {
        AmountMetric, PreviousMetric, CurrentMetric, DeltaMetric, CountMetric,
        QuantityMetric, SecuredCountMetric, ExpiredCountMetric, MassMetric,
        DamageMetric, RatioMetric, DurationMetric,
        DayMetric, RevisionMetric, StatusMetric, ValueMetric,
        CurrentExperienceBeforeMetric, CurrentExperienceAfterMetric,
        LifetimeExperienceBeforeMetric, LifetimeExperienceAfterMetric,
        RankBeforeMetric, RankAfterMetric, AbsoluteHourMetric,
        RequiredWorkMetric, CompletedWorkMetric,
        InputQuantityMetric, OutputQuantityMetric, InputMassMetric,
        OutputMassMetric, DamageIndexBeforeMetric, DamageIndexAfterMetric,
        StateBeforeMetric, StateAfterMetric, RiskBeforeMetric,
        RiskAfterMetric, TrustBeforeMetric, TrustAfterMetric,
        StrengthBeforeMetric, StrengthAfterMetric,
        DelayBeforeMetric, DelayAfterMetric,
        HealthBeforeMetric, HealthAfterMetric, PowerMetric,
        RequiredPowerMetric, DangerMetric, RequestedCountMetric,
        MaterializedCountMetric, EscapedCountMetric,
        HungerBeforeMetric, HungerAfterMetric,
        ThirstBeforeMetric, ThirstAfterMetric,
        HygieneBeforeMetric, HygieneAfterMetric,
        InfectionBeforeMetric, InfectionAfterMetric,
        MoodBeforeMetric, MoodAfterMetric,
        SleepBeforeMetric, SleepAfterMetric,
        FunBeforeMetric, FunAfterMetric,
        ExcretionBeforeMetric, ExcretionAfterMetric,
        InjuryBeforeMetric, InjuryAfterMetric,
        StressBeforeMetric, StressAfterMetric
    };

    private static readonly GameplayMetricUnitId[] KnownUnitsInternal =
    {
        CountUnit, PointUnit, RatioUnit, CurrencyUnit, DayUnit, RevisionUnit,
        GramUnit, SecondUnit, EnumUnit, HourUnit, MilliExperienceUnit,
        MilliWorkUnit
    };

    public static bool IsKnownRole(GameplayRoleId roleId) =>
        Contains(KnownRolesInternal, roleId);

    public static bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        Contains(KnownMetricsInternal, metricId)
        && Contains(KnownUnitsInternal, unitId);

    private static bool Contains<T>(T[] values, T value) where T : struct, IEquatable<T>
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (values[index].Equals(value))
                return true;
        }
        return false;
    }
}

public static class MigratedProducerOutcomeSnapshots
{
    public static KoreanNameSnapshot Name(string stableId, string displayText)
    {
        string identity = GameplayOutcomeStableIdSyntax.Require(
            stableId,
            nameof(stableId));
        string display = displayText?.Trim() ?? string.Empty;
        if (display.Length == 0 || display.Length > 256)
            throw new ArgumentException("A bounded immutable display name is required.", nameof(displayText));
        string revision = "migrated-producer-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(identity + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }
}

public sealed class MigratedProducerOutcomeAdapter :
    GameplayOutcomeAdapter<MigratedProducerOutcomeReceipt>,
    IGameplayOutcomeDynamicAdapterRegistration
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        MigratedProducerOutcomeCatalog.Definitions[0].OutcomeTypeId;

    public IReadOnlyList<GameplayOutcomeTypeId> SupportedOutcomeTypes =>
        MigratedProducerOutcomeCatalog.SupportedOutcomeTypes;

    public bool SupportsOutcomeType(GameplayOutcomeTypeId outcomeTypeId) =>
        MigratedProducerOutcomeCatalog.TryGet(outcomeTypeId, out _);

    public override OutcomePrepareResult TryGetRequirements(
        in MigratedProducerOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        MigratedProducerOutcomePayload payload = receipt.Payload;
        requirements = new OutcomeWriteRequirements(
            payload.ResultKey,
            payload.OutcomeTypeId,
            payload.AbsoluteDay,
            payload.Status,
            currentWorldEpoch,
            payload.OwnerRevision,
            payload.Participants.Count,
            payload.Metrics.Count,
            payload.Subjects.Count,
            payload.Tags.Count,
            anchorCount: 0,
            provenanceCount: payload.Provenance.Count,
            factCount: payload.Facts.Count);
        return Validate(payload, out string failure)
            ? OutcomePrepareResult.Prepared()
            : Invalid(failure);
    }

    public override OutcomePrepareResult TryWrite(
        in MigratedProducerOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        MigratedProducerOutcomePayload payload = receipt.Payload;
        if (!Validate(payload, out string failure))
            return Invalid(failure);
        for (int index = 0; index < payload.Participants.Count; index++)
        {
            if (!builder.AddParticipant(payload.Participants.Get(index)))
                return WriteFailed("participant");
        }
        for (int index = 0; index < payload.Metrics.Count; index++)
        {
            if (!builder.AddMetric(payload.Metrics.Get(index)))
                return WriteFailed("metric");
        }
        for (int index = 0; index < payload.Subjects.Count; index++)
        {
            if (!builder.AddSubject(payload.Subjects.Get(index)))
                return WriteFailed("subject");
        }
        for (int index = 0; index < payload.Tags.Count; index++)
        {
            if (!builder.AddTag(payload.Tags.Get(index)))
                return WriteFailed("tag");
        }
        for (int index = 0; index < payload.Provenance.Count; index++)
        {
            if (!builder.AddProvenance(payload.Provenance.Get(index)))
                return WriteFailed("provenance");
        }
        for (int index = 0; index < payload.Facts.Count; index++)
        {
            if (!builder.AddFact(payload.Facts.Get(index)))
                return WriteFailed("fact");
        }
        if (payload.Location.HasLocation && !builder.SetLocation(payload.Location))
            return WriteFailed("location");
        if ((payload.Causation.HasParent
                || payload.Causation.RootOperationId.IsValid
                || !string.IsNullOrEmpty(payload.Causation.RelationId))
            && !builder.SetCausation(payload.Causation))
        {
            return WriteFailed("causation");
        }
        return OutcomePrepareResult.Prepared();
    }

    private static bool Validate(
        MigratedProducerOutcomePayload payload,
        out string failure)
    {
        failure = string.Empty;
        if (!MigratedProducerOutcomeCatalog.TryGet(
                payload.OutcomeTypeId,
                out MigratedProducerOutcomeDefinition definition)
            || !payload.ResultKey.IsValid
            || !string.Equals(
                payload.ResultKey.ProducerId,
                definition.ProducerId,
                StringComparison.Ordinal)
            || payload.ResultKey.CommitRevision != payload.OwnerRevision)
        {
            return Fail("identity", out failure);
        }
        if (payload.AbsoluteDay < 0
            || payload.OwnerRevision <= 0L
            || !Enum.IsDefined(typeof(GameplayOutcomeStatus), payload.Status)
            || payload.Participants.Count == 0
            || payload.Subjects.Count == 0
            || payload.Tags.Count != 1
            || !payload.Tags.Get(0).Equals(definition.DomainTag)
            || payload.Provenance.Count == 0
            || payload.Facts.Count < 2)
        {
            return Fail("shape", out failure);
        }
        for (int index = 0; index < payload.Participants.Count; index++)
        {
            GameplayOutcomeParticipant participant = payload.Participants.Get(index);
            if (!participant.EntityId.IsValid
                || !MigratedProducerOutcomeIds.IsKnownRole(participant.RoleId)
                || participant.ParticipationKind != GameplayParticipationKind.Direct
                || !participant.HasPerceptionEvidence
                || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(participant.DisplayName))
            {
                return Fail("participant", out failure);
            }
        }
        for (int index = 0; index < payload.Metrics.Count; index++)
        {
            GameplayOutcomeMetric metric = payload.Metrics.Get(index);
            if (!MigratedProducerOutcomeIds.IsKnownMetric(metric.MetricId, metric.UnitId)
                || !metric.DefinitionOrInstanceId.IsValid
                || !double.IsFinite(metric.Value))
            {
                return Fail("metric", out failure);
            }
        }
        for (int index = 0; index < payload.Subjects.Count; index++)
        {
            GameplayOutcomeSubjectLink subject = payload.Subjects.Get(index);
            if (!subject.SubjectId.IsValid
                || subject.IsOptionalWitness
                || !float.IsFinite(subject.Salience)
                || subject.Salience < 0f
                || subject.Salience > 1f
                || !ContainsParticipant(payload.Participants, subject.SubjectId))
            {
                return Fail("subject", out failure);
            }
        }
        bool sourceType = false;
        bool summary = false;
        for (int index = 0; index < payload.Facts.Count; index++)
        {
            GameplayOutcomeFact fact = payload.Facts.Get(index);
            if (!fact.FactId.IsValid
                || !GameplayOutcomeLedger.IsValidBoundedUtf16(
                    fact.Value,
                    GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                    allowEmpty: false))
            {
                return Fail("fact", out failure);
            }
            if (fact.FactId.Equals(MigratedProducerOutcomeIds.SourceTypeFact))
                sourceType = string.Equals(fact.Value, definition.SourceTypeName, StringComparison.Ordinal);
            if (fact.FactId.Equals(MigratedProducerOutcomeIds.SummaryFact))
                summary = true;
        }
        if (!sourceType || !summary)
            return Fail("required-fact", out failure);
        for (int index = 0; index < payload.Provenance.Count; index++)
        {
            if (!payload.Provenance.Get(index).IsValid)
                return Fail("provenance", out failure);
        }
        return true;
    }

    private static bool ContainsParticipant(
        MigratedOutcomeFixedBuffer16<GameplayOutcomeParticipant> participants,
        GameplayEntityId subjectId)
    {
        for (int index = 0; index < participants.Count; index++)
        {
            if (participants.Get(index).EntityId == subjectId)
                return true;
        }
        return false;
    }

    private static bool Fail(string reason, out string failure)
    {
        failure = reason;
        return false;
    }

    private static OutcomePrepareResult Invalid(string reason) => new(
        OutcomePrepareCode.InvalidReceipt,
        "migrated-producer-" + reason + "-invalid");

    private static OutcomePrepareResult WriteFailed(string reason) => new(
        OutcomePrepareCode.AdapterWriteFailed,
        "migrated-producer-" + reason + "-write-failed");
}

internal sealed class MigratedProducerOutcomeMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":" + subjectId.Kind.Value);

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        float baseSalience = 0f;
        NarrativeMemoryTier tier = NarrativeMemoryTier.Recent;
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink subject = outcome.GetSubject(index);
            if (subject.SubjectId == subjectId)
            {
                baseSalience = subject.Salience;
                tier = subject.Tier;
                break;
            }
        }
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float salience = Math.Clamp(
            baseSalience - Math.Min(0.2f, priorMatchingCount * 0.025f)
                - Math.Min(0.2f, age * 0.01f),
            0f,
            1f);
        return new OutcomeMemoryEvaluation(
            salience,
            tier,
            Math.Max(evaluationDay + 3, outcome.AbsoluteDay + 3));
    }
}

internal sealed class MigratedProducerOutcomePerceptionPolicy : IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class MigratedProducerOutcomeConsolidator : IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class MigratedProducerOutcomeProjector : INarrativePerspectiveProjector
{
    private const string RendererVersion = "migrated-producer-v1";
    private static readonly string[] AttachedNameParticles =
    {
        "으로부터", "에게서", "한테서", "으로", "에게", "한테", "께서",
        "은", "는", "이", "가", "을", "를", "과", "와", "의", "로",
        "에", "도", "만", "부터", "까지"
    };
    private readonly IKoreanJosaFormatter josa;
    private readonly MigratedProducerOutcomeDefinition definition;

    public MigratedProducerOutcomeProjector(
        IKoreanJosaFormatter josa,
        MigratedProducerOutcomeDefinition definition)
    {
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant subject = outcome.GetParticipant(0);
        string summary = FindFact(outcome, MigratedProducerOutcomeIds.SummaryFact);
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            summary = NormalizeSubjectParticlePlaceholder(
                summary,
                outcome.GetParticipant(index).DisplayName);
        }
        KoreanJosaFormatResult formatted = josa.Format(new KoreanJosaRequest(
            subject.DisplayName,
            KoreanJosaKind.Topic));
        bool neutral = formatted.RequiresNeutralFrame;
        bool summaryAlreadyNamesSubject = SummaryNamesSubject(
            summary,
            subject.DisplayName);
        string text = summaryAlreadyNamesSubject
            ? summary
            : neutral
                ? definition.KoreanLabel + " · " + subject.DisplayName.DisplayText
                    + ": " + summary
                : formatted.Text + " " + summary;
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }

    private string NormalizeSubjectParticlePlaceholder(
        string summary,
        KoreanNameSnapshot subjectName)
    {
        string displayText = subjectName.DisplayText;
        string placeholder = displayText + "이(가)";
        if (!ContainsStandaloneToken(summary, placeholder))
            return summary;

        KoreanJosaFormatResult subject = josa.Format(new KoreanJosaRequest(
            subjectName,
            KoreanJosaKind.Subject));
        string replacement = subject.RequiresNeutralFrame
            ? displayText
            : subject.Text;
        return ReplaceStandaloneToken(summary, placeholder, replacement);
    }

    private bool SummaryNamesSubject(
        string summary,
        KoreanNameSnapshot subjectName)
    {
        string displayText = subjectName.DisplayText;
        if (ContainsNameMention(summary, displayText))
            return true;

        KoreanJosaFormatResult subject = josa.Format(new KoreanJosaRequest(
            subjectName,
            KoreanJosaKind.Subject));
        return !subject.RequiresNeutralFrame
            && ContainsStandaloneToken(summary, subject.Text);
    }

    private static bool ContainsNameMention(string summary, string displayText)
    {
        if (ContainsStandaloneToken(summary, displayText))
            return true;

        for (int index = 0; index < AttachedNameParticles.Length; index++)
        {
            if (ContainsStandaloneToken(
                    summary,
                    displayText + AttachedNameParticles[index]))
            {
                return true;
            }
        }
        return false;
    }

    private static bool ContainsStandaloneToken(string text, string token)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
            return false;

        int searchIndex = 0;
        while (searchIndex <= text.Length - token.Length)
        {
            int matchIndex = text.IndexOf(
                token,
                searchIndex,
                StringComparison.Ordinal);
            if (matchIndex < 0)
                return false;

            int endIndex = matchIndex + token.Length;
            if (IsTokenBoundaryBefore(text, matchIndex)
                && IsTokenBoundaryAfter(text, endIndex))
            {
                return true;
            }
            searchIndex = endIndex;
        }
        return false;
    }

    private static string ReplaceStandaloneToken(
        string text,
        string token,
        string replacement)
    {
        int searchIndex = 0;
        while (searchIndex <= text.Length - token.Length)
        {
            int matchIndex = text.IndexOf(
                token,
                searchIndex,
                StringComparison.Ordinal);
            if (matchIndex < 0)
                break;

            int endIndex = matchIndex + token.Length;
            if (IsTokenBoundaryBefore(text, matchIndex)
                && IsTokenBoundaryAfter(text, endIndex))
            {
                text = text.Remove(matchIndex, token.Length)
                    .Insert(matchIndex, replacement);
                searchIndex = matchIndex + replacement.Length;
            }
            else
            {
                searchIndex = endIndex;
            }
        }
        return text;
    }

    private static bool IsTokenBoundaryBefore(string text, int index) =>
        index == 0 || !IsTokenCharacter(text[index - 1]);

    private static bool IsTokenBoundaryAfter(string text, int index) =>
        index >= text.Length || !IsTokenCharacter(text[index]);

    private static bool IsTokenCharacter(char value) =>
        char.IsLetterOrDigit(value) || value == '_';

    private static string FindFact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId factId)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(factId))
                return fact.Value;
        }
        return "결과가 확정됐다.";
    }
}

public sealed class MigratedProducerOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private static readonly IOutcomeMemoryPolicy Memory =
        new MigratedProducerOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new MigratedProducerOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new MigratedProducerOutcomeConsolidator();
    private readonly MigratedProducerOutcomeDefinition definition;
    private readonly INarrativePerspectiveProjector projector;

    public MigratedProducerOutcomeDescriptor(
        MigratedProducerOutcomeDefinition definition,
        IKoreanJosaFormatter josa)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        projector = new MigratedProducerOutcomeProjector(josa, definition);
    }

    public GameplayOutcomeTypeId OutcomeTypeId => definition.OutcomeTypeId;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        MigratedProducerOutcomeIds.IsKnownRole(roleId);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        MigratedProducerOutcomeIds.IsKnownMetric(metricId, unitId);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != definition.OutcomeTypeId
            || outcome.ParticipantCount == 0
            || outcome.SubjectCount == 0
            || outcome.TagCount != 1
            || !outcome.GetTag(0).Equals(definition.DomainTag)
            || outcome.ProvenanceCount == 0
            || outcome.FactCount < 2
            || !string.Equals(
                FindFact(outcome, MigratedProducerOutcomeIds.SourceTypeFact),
                definition.SourceTypeName,
                StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(
                FindFact(outcome, MigratedProducerOutcomeIds.SummaryFact)))
        {
            return OutcomeValidationResult.Reject(
                definition.OutcomeTypeId.Value + "-shape-invalid");
        }
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            if (!IsKnownRole(outcome.GetParticipant(index).RoleId))
            {
                return OutcomeValidationResult.Reject(
                    definition.OutcomeTypeId.Value + "-role-invalid");
            }
        }
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (!IsKnownMetric(metric.MetricId, metric.UnitId))
            {
                return OutcomeValidationResult.Reject(
                    definition.OutcomeTypeId.Value + "-metric-invalid");
            }
        }
        return OutcomeValidationResult.Accepted;
    }

    private static string FindFact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId factId)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(factId))
                return fact.Value;
        }
        return string.Empty;
    }
}

public sealed class MigratedProducerOutcomeDescriptorCatalog :
    IGameplayOutcomeDescriptorCatalog
{
    private readonly IGameplayOutcomeDescriptor[] descriptors;

    public MigratedProducerOutcomeDescriptorCatalog(IKoreanJosaFormatter josa)
    {
        IReadOnlyList<MigratedProducerOutcomeDefinition> definitions =
            MigratedProducerOutcomeCatalog.Definitions;
        descriptors = new IGameplayOutcomeDescriptor[definitions.Count];
        for (int index = 0; index < definitions.Count; index++)
            descriptors[index] = new MigratedProducerOutcomeDescriptor(definitions[index], josa);
    }

    public IReadOnlyList<IGameplayOutcomeDescriptor> Descriptors => descriptors;
}

public static class MigratedProducerOutcomeRegistration
{
    public static void RegisterMigratedProducerGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<MigratedProducerOutcomeAdapter>(Lifetime.Singleton)
            .AsSelf();
        builder.RegisterGameplayActionLogs("migrated");
    }
}
