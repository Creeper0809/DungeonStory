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
    public static readonly GameplayRoleId OperationRole = new("operation");

    public static readonly GameplayMetricId AmountMetric = new("amount");
    public static readonly GameplayMetricId PreviousMetric = new("previous");
    public static readonly GameplayMetricId CurrentMetric = new("current");
    public static readonly GameplayMetricId DeltaMetric = new("delta");
    public static readonly GameplayMetricId CountMetric = new("count");
    public static readonly GameplayMetricId QuantityMetric = new("quantity");
    public static readonly GameplayMetricId MassMetric = new("mass");
    public static readonly GameplayMetricId DamageMetric = new("damage");
    public static readonly GameplayMetricId RatioMetric = new("ratio");
    public static readonly GameplayMetricId DurationMetric = new("duration");
    public static readonly GameplayMetricId DayMetric = new("day");
    public static readonly GameplayMetricId RevisionMetric = new("revision");
    public static readonly GameplayMetricId StatusMetric = new("status");
    public static readonly GameplayMetricId ValueMetric = new("value");

    public static readonly GameplayMetricUnitId CountUnit = new("count");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId RatioUnit = new("ratio");
    public static readonly GameplayMetricUnitId CurrencyUnit = new("currency");
    public static readonly GameplayMetricUnitId DayUnit = new("day");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricUnitId GramUnit = new("gram");
    public static readonly GameplayMetricUnitId SecondUnit = new("second");
    public static readonly GameplayMetricUnitId EnumUnit = new("enum");

    public static readonly GameplayOutcomeFactId SourceTypeFact = new("source-type");
    public static readonly GameplayOutcomeFactId SummaryFact = new("summary");
    public static readonly GameplayOutcomeFactId ReasonFact = new("reason-code");
    public static readonly GameplayOutcomeFactId DetailFact = new("detail");
    public static readonly GameplayOutcomeFactId IdentityFact = new("identity");
    public static readonly GameplayOutcomeFactId BeforeFact = new("state-before");
    public static readonly GameplayOutcomeFactId AfterFact = new("state-after");
    public static readonly GameplayOutcomeFactId OperationFact = new("operation-id");

    public static readonly GameplayOutcomeProvenanceReference ProducerMigrationProvenance =
        new("capture-contract", "migrated-producer-v1");

    private static readonly GameplayRoleId[] KnownRolesInternal =
    {
        ActorRole, TargetRole, FacilityRole, ItemRole, FactionRole,
        ExpeditionRole, RouteRole, OwnerRole, CustomerRole, SourceRole,
        DestinationRole, WitnessRole, OperationRole
    };

    private static readonly GameplayMetricId[] KnownMetricsInternal =
    {
        AmountMetric, PreviousMetric, CurrentMetric, DeltaMetric, CountMetric,
        QuantityMetric, MassMetric, DamageMetric, RatioMetric, DurationMetric,
        DayMetric, RevisionMetric, StatusMetric, ValueMetric
    };

    private static readonly GameplayMetricUnitId[] KnownUnitsInternal =
    {
        CountUnit, PointUnit, RatioUnit, CurrencyUnit, DayUnit, RevisionUnit,
        GramUnit, SecondUnit, EnumUnit
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
        KoreanJosaFormatResult formatted = josa.Format(new KoreanJosaRequest(
            subject.DisplayName,
            KoreanJosaKind.Topic));
        bool neutral = formatted.RequiresNeutralFrame;
        string text = neutral
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
            || outcome.AnchorCount != 0
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
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<MigratedProducerOutcomeDescriptorCatalog>(Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptorCatalog>();
    }
}
