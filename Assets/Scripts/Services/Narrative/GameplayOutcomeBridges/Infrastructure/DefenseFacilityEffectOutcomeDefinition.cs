using System;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class DefenseFacilityEffectOutcomeIds
{
    public const string ProducerId = "defense.facility-effect";
    public static readonly GameplayOutcomeTypeId Result =
        new("defense.facility-effect-resolved");
    public static readonly GameplayEntityKindId Facility = new("facility");
    public static readonly GameplayEntityKindId Character = new("character");
    public static readonly GameplayRoleId SourceFacility =
        new("source-facility");
    public static readonly GameplayRoleId Target = new("target");
    public static readonly GameplayOutcomeTagId DefenseEffect =
        new("defense-effect");
    public static readonly GameplayMetricUnitId Points = new("points");
    public static readonly GameplayMetricUnitId Count = new("count");
    public static readonly GameplayMetricUnitId Seconds = new("seconds");
    public static readonly GameplayMetricId Damage = new("defense-effect.damage");
    public static readonly GameplayMetricId HealthBefore =
        new("defense-effect.health-before");
    public static readonly GameplayMetricId HealthAfter =
        new("defense-effect.health-after");
    public static readonly GameplayMetricId StatusCountBefore =
        new("defense-effect.status-count-before");
    public static readonly GameplayMetricId StatusCountAfter =
        new("defense-effect.status-count-after");
    public static readonly GameplayMetricId SupplyBefore =
        new("defense-effect.supply-before");
    public static readonly GameplayMetricId SupplyAfter =
        new("defense-effect.supply-after");
    public static readonly GameplayMetricId ConditionBefore =
        new("defense-effect.condition-before");
    public static readonly GameplayMetricId ConditionAfter =
        new("defense-effect.condition-after");
    public static readonly GameplayMetricId MovementDelay =
        new("defense-effect.movement-delay");
    public static readonly GameplayMetricId IgnitionIntensity =
        new("defense-effect.ignition-intensity");
    public static readonly GameplayOutcomeFactId Timing =
        new("defense-effect.timing");
    public static readonly GameplayOutcomeFactId Concept =
        new("defense-effect.concept");
    public static readonly GameplayOutcomeFactId StatusBefore =
        new("defense-effect.status-before");
    public static readonly GameplayOutcomeFactId StatusAfter =
        new("defense-effect.status-after");
    public static readonly GameplayOutcomeFactId EffectTags =
        new("defense-effect.tags");
    public static readonly GameplayOutcomeFactId Failure =
        new("defense-effect.failure");
    public static readonly GameplayOutcomeFactId Jammed =
        new("defense-effect.jammed");
    public static readonly GameplayOutcomeFactId Misfired =
        new("defense-effect.misfired");

    public static GameplayResultKey ResultKey(
        string facilityId,
        int activationCount) => new(
        ProducerId,
        new GameplayOperationId(
            "defense-effect:" + facilityId + ":" + activationCount),
        Math.Max(1, activationCount),
        0);
}

public readonly struct DefenseFacilityEffectOutcomeReceipt
{
    public DefenseFacilityEffectOutcomeReceipt(
        GameplayResultKey resultKey,
        int absoluteDay,
        GameplayOutcomeStatus status,
        string facilityId,
        string facilityName,
        string targetId,
        string targetName,
        DefenseTriggerTiming timing,
        DefenseAttackConcept concept,
        float damage,
        float healthBefore,
        float healthAfter,
        int statusCountBefore,
        int statusCountAfter,
        int supplyBefore,
        int supplyAfter,
        float conditionBefore,
        float conditionAfter,
        float movementDelaySeconds,
        float ignitionIntensity,
        string statusesBefore,
        string statusesAfter,
        string effectTags,
        string failure,
        bool jammed,
        bool misfired,
        GameplayOutcomeCausation causation)
    {
        ResultKey = resultKey;
        AbsoluteDay = absoluteDay;
        Status = status;
        FacilityId = facilityId?.Trim() ?? string.Empty;
        FacilityName = facilityName?.Trim() ?? string.Empty;
        TargetId = targetId?.Trim() ?? string.Empty;
        TargetName = targetName?.Trim() ?? string.Empty;
        Timing = timing;
        Concept = concept;
        Damage = damage;
        HealthBefore = healthBefore;
        HealthAfter = healthAfter;
        StatusCountBefore = statusCountBefore;
        StatusCountAfter = statusCountAfter;
        SupplyBefore = supplyBefore;
        SupplyAfter = supplyAfter;
        ConditionBefore = conditionBefore;
        ConditionAfter = conditionAfter;
        MovementDelaySeconds = movementDelaySeconds;
        IgnitionIntensity = ignitionIntensity;
        StatusesBefore = statusesBefore?.Trim() ?? string.Empty;
        StatusesAfter = statusesAfter?.Trim() ?? string.Empty;
        EffectTags = effectTags?.Trim() ?? string.Empty;
        Failure = failure?.Trim() ?? string.Empty;
        Jammed = jammed;
        Misfired = misfired;
        Causation = causation;
    }

    public GameplayResultKey ResultKey { get; }
    public int AbsoluteDay { get; }
    public GameplayOutcomeStatus Status { get; }
    public string FacilityId { get; }
    public string FacilityName { get; }
    public string TargetId { get; }
    public string TargetName { get; }
    public DefenseTriggerTiming Timing { get; }
    public DefenseAttackConcept Concept { get; }
    public float Damage { get; }
    public float HealthBefore { get; }
    public float HealthAfter { get; }
    public int StatusCountBefore { get; }
    public int StatusCountAfter { get; }
    public int SupplyBefore { get; }
    public int SupplyAfter { get; }
    public float ConditionBefore { get; }
    public float ConditionAfter { get; }
    public float MovementDelaySeconds { get; }
    public float IgnitionIntensity { get; }
    public string StatusesBefore { get; }
    public string StatusesAfter { get; }
    public string EffectTags { get; }
    public string Failure { get; }
    public bool Jammed { get; }
    public bool Misfired { get; }
    public GameplayOutcomeCausation Causation { get; }
}

public sealed class DefenseFacilityEffectOutcomeAdapter :
    GameplayOutcomeAdapter<DefenseFacilityEffectOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        DefenseFacilityEffectOutcomeIds.Result;

    public override OutcomePrepareResult TryGetRequirements(
        in DefenseFacilityEffectOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid
            || receipt.AbsoluteDay < 0
            || receipt.Status is < GameplayOutcomeStatus.Succeeded
                or > GameplayOutcomeStatus.Cancelled
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.FacilityId)
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.TargetId)
            || string.IsNullOrWhiteSpace(receipt.FacilityName)
            || string.IsNullOrWhiteSpace(receipt.TargetName)
            || receipt.Timing == DefenseTriggerTiming.None
            || receipt.Concept == DefenseAttackConcept.None
            || !FiniteNonNegative(receipt.Damage)
            || !FiniteNonNegative(receipt.HealthBefore)
            || !FiniteNonNegative(receipt.HealthAfter)
            || receipt.StatusCountBefore < 0
            || receipt.StatusCountAfter < 0
            || receipt.SupplyBefore < 0
            || receipt.SupplyAfter < 0
            || !FiniteNonNegative(receipt.ConditionBefore)
            || !FiniteNonNegative(receipt.ConditionAfter)
            || !FiniteNonNegative(receipt.MovementDelaySeconds)
            || !FiniteNonNegative(receipt.IgnitionIntensity)
            || receipt.StatusesBefore.Length == 0
            || receipt.StatusesAfter.Length == 0
            || receipt.EffectTags.Length == 0
            || receipt.Failure.Length == 0
            || !receipt.Causation.HasParent
            || !receipt.Causation.RootOperationId.IsValid)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "defense-facility-effect-receipt-invalid");
        }

        requirements = Requirements(
            receipt.ResultKey,
            receipt.AbsoluteDay,
            receipt.Status,
            currentWorldEpoch);
        return OutcomePrepareResult.Prepared();
    }

    public static OutcomeWriteRequirements Requirements(
        GameplayResultKey key,
        int day,
        GameplayOutcomeStatus status,
        long epoch) => new(
        key,
        DefenseFacilityEffectOutcomeIds.Result,
        day,
        status,
        epoch,
        key.CommitRevision,
        participantCount: 2,
        metricCount: 11,
        subjectCount: 2,
        tagCount: 1,
        anchorCount: 0,
        provenanceCount: 1,
        factCount: 8);

    public override OutcomePrepareResult TryWrite(
        in DefenseFacilityEffectOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId facility = new(
            DefenseFacilityEffectOutcomeIds.Facility,
            receipt.FacilityId);
        GameplayEntityId target = new(
            DefenseFacilityEffectOutcomeIds.Character,
            receipt.TargetId);
        bool valid = builder.AddParticipant(new GameplayOutcomeParticipant(
                facility,
                DefenseFacilityEffectOutcomeIds.SourceFacility,
                GameplayParticipationKind.Direct,
                true,
                MigratedProducerOutcomeSnapshots.Name(
                    receipt.FacilityId,
                    receipt.FacilityName)))
            && builder.AddParticipant(new GameplayOutcomeParticipant(
                target,
                DefenseFacilityEffectOutcomeIds.Target,
                GameplayParticipationKind.Direct,
                true,
                MigratedProducerOutcomeSnapshots.Name(
                    receipt.TargetId,
                    receipt.TargetName)))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility,
                0.68f,
                NarrativeMemoryTier.Episodic,
                false,
                false,
                0))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                target,
                0.72f,
                NarrativeMemoryTier.Episodic,
                false,
                false,
                0))
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.Damage,
                receipt.Damage, DefenseFacilityEffectOutcomeIds.Points, target)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.HealthBefore,
                receipt.HealthBefore, DefenseFacilityEffectOutcomeIds.Points, target)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.HealthAfter,
                receipt.HealthAfter, DefenseFacilityEffectOutcomeIds.Points, target)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.StatusCountBefore,
                receipt.StatusCountBefore, DefenseFacilityEffectOutcomeIds.Count, target)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.StatusCountAfter,
                receipt.StatusCountAfter, DefenseFacilityEffectOutcomeIds.Count, target)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.SupplyBefore,
                receipt.SupplyBefore, DefenseFacilityEffectOutcomeIds.Count, facility)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.SupplyAfter,
                receipt.SupplyAfter, DefenseFacilityEffectOutcomeIds.Count, facility)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.ConditionBefore,
                receipt.ConditionBefore, DefenseFacilityEffectOutcomeIds.Points, facility)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.ConditionAfter,
                receipt.ConditionAfter, DefenseFacilityEffectOutcomeIds.Points, facility)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.MovementDelay,
                receipt.MovementDelaySeconds,
                DefenseFacilityEffectOutcomeIds.Seconds,
                target)
            && Metric(ref builder, DefenseFacilityEffectOutcomeIds.IgnitionIntensity,
                receipt.IgnitionIntensity,
                DefenseFacilityEffectOutcomeIds.Points,
                target)
            && builder.AddTag(DefenseFacilityEffectOutcomeIds.DefenseEffect)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "defense-facility-effect-v1"))
            && builder.AddFact(new GameplayOutcomeFact(
                DefenseFacilityEffectOutcomeIds.Timing,
                receipt.Timing.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                DefenseFacilityEffectOutcomeIds.Concept,
                receipt.Concept.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                DefenseFacilityEffectOutcomeIds.StatusBefore,
                receipt.StatusesBefore))
            && builder.AddFact(new GameplayOutcomeFact(
                DefenseFacilityEffectOutcomeIds.StatusAfter,
                receipt.StatusesAfter))
            && builder.AddFact(new GameplayOutcomeFact(
                DefenseFacilityEffectOutcomeIds.EffectTags,
                receipt.EffectTags))
            && builder.AddFact(new GameplayOutcomeFact(
                DefenseFacilityEffectOutcomeIds.Failure,
                receipt.Failure))
            && builder.AddFact(new GameplayOutcomeFact(
                DefenseFacilityEffectOutcomeIds.Jammed,
                receipt.Jammed ? "true" : "false"))
            && builder.AddFact(new GameplayOutcomeFact(
                DefenseFacilityEffectOutcomeIds.Misfired,
                receipt.Misfired ? "true" : "false"))
            && builder.SetCausation(receipt.Causation);
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "defense-facility-effect-receipt-write-failed");
    }

    private static bool FiniteNonNegative(float value) =>
        float.IsFinite(value) && value >= 0f;

    private static bool Metric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId entity) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, entity));
}

public sealed class DefenseFacilityEffectOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly IOutcomeMemoryPolicy Memory =
        new MigratedProducerOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new MigratedProducerOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new MigratedProducerOutcomeConsolidator();
    private readonly INarrativePerspectiveProjector projector;

    public DefenseFacilityEffectOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new DefenseFacilityEffectOutcomeProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        DefenseFacilityEffectOutcomeIds.Result;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId role) =>
        role.Equals(DefenseFacilityEffectOutcomeIds.SourceFacility)
        || role.Equals(DefenseFacilityEffectOutcomeIds.Target);
    public bool IsKnownMetric(GameplayMetricId id, GameplayMetricUnitId unit) =>
        id.Equals(DefenseFacilityEffectOutcomeIds.Damage)
        || id.Equals(DefenseFacilityEffectOutcomeIds.HealthBefore)
        || id.Equals(DefenseFacilityEffectOutcomeIds.HealthAfter)
        || id.Equals(DefenseFacilityEffectOutcomeIds.StatusCountBefore)
        || id.Equals(DefenseFacilityEffectOutcomeIds.StatusCountAfter)
        || id.Equals(DefenseFacilityEffectOutcomeIds.SupplyBefore)
        || id.Equals(DefenseFacilityEffectOutcomeIds.SupplyAfter)
        || id.Equals(DefenseFacilityEffectOutcomeIds.ConditionBefore)
        || id.Equals(DefenseFacilityEffectOutcomeIds.ConditionAfter)
        || id.Equals(DefenseFacilityEffectOutcomeIds.MovementDelay)
        || id.Equals(DefenseFacilityEffectOutcomeIds.IgnitionIntensity);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 2
        && outcome.MetricCount == 11
        && outcome.SubjectCount == 2
        && outcome.TagCount == 1
        && outcome.ProvenanceCount == 1
        && outcome.FactCount == 8
        && outcome.Causation.HasParent
        && outcome.Causation.RootOperationId.IsValid
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "defense-facility-effect-outcome-shape-invalid");
}

internal sealed class DefenseFacilityEffectOutcomeProjector :
    INarrativePerspectiveProjector
{
    private readonly IKoreanJosaFormatter josa;

    public DefenseFacilityEffectOutcomeProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant target = outcome.GetParticipant(1);
        KoreanJosaFormatResult subject = josa.Format(new KoreanJosaRequest(
            target.DisplayName,
            KoreanJosaKind.Subject));
        bool neutral = subject.RequiresNeutralFrame;
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            neutral
                ? $"{target.DisplayName.DisplayText}: 방어 시설 효과를 받았다."
                : $"{subject.Text} 방어 시설 효과를 받았다.",
            "defense-facility-effect-v1",
            neutral);
    }
}

public static class DefenseFacilityEffectOutcomeRegistration
{
    public static void RegisterDefenseFacilityEffectGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.RegisterGameplayActionLogs(nameof(DefenseFacilityEffectOutcomeAdapter));
        builder.Register<DefenseActivationFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.Register<DefenseActivationFactObserver>(Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register(resolver => DefenseEffectFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
    }
}
