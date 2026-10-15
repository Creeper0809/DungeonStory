using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Narrative.Korean;

public static class ReproductionProcessTransitionOutcomeIds
{
    public const string ProducerId = "character.reproduction-process";
    public static readonly GameplayOutcomeTypeId Transitioned =
        new("character.reproduction-process-transitioned");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayRoleId GeneticParentRole =
        new("genetic-parent");
    public static readonly GameplayRoleId CarrierRole = new("reproduction-carrier");
    public static readonly GameplayRoleId MedicalResponderRole =
        new("medical-responder");
    public static readonly GameplayRoleId OffspringRole = new("offspring");
    public static readonly GameplayRoleId FacilityRole =
        new("support-facility");
    public static readonly GameplayOutcomeTagId ReproductionTag =
        new("reproduction-process");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricUnitId PhaseUnit = new("phase-index");
    public static readonly GameplayMetricUnitId DayUnit = new("day");
    public static readonly GameplayMetricId RevisionBefore =
        new("reproduction.revision-before");
    public static readonly GameplayMetricId RevisionAfter =
        new("reproduction.revision-after");
    public static readonly GameplayMetricId PhaseBefore =
        new("reproduction.phase-before");
    public static readonly GameplayMetricId PhaseAfter =
        new("reproduction.phase-after");
    public static readonly GameplayMetricId PhaseElapsedBefore =
        new("reproduction.phase-elapsed-before");
    public static readonly GameplayMetricId PhaseElapsedAfter =
        new("reproduction.phase-elapsed-after");
    public static readonly GameplayMetricId ProgressBefore =
        new("reproduction.progress-before");
    public static readonly GameplayMetricId ProgressAfter =
        new("reproduction.progress-after");
    public static readonly GameplayMetricId EmergencyDeadlineDay =
        new("reproduction.emergency-deadline-day");
    public static readonly GameplayOutcomeFactId ProcessId =
        new("reproduction.process-id");
    public static readonly GameplayOutcomeFactId TransitionKind =
        new("reproduction.transition-kind");
    public static readonly GameplayOutcomeFactId Mode =
        new("reproduction.mode");
    public static readonly GameplayOutcomeFactId Phenotype =
        new("reproduction.phenotype-species-id");
    public static readonly GameplayOutcomeFactId StatusBefore =
        new("reproduction.status-before");
    public static readonly GameplayOutcomeFactId StatusAfter =
        new("reproduction.status-after");
    public static readonly GameplayOutcomeFactId FailureBefore =
        new("reproduction.failure-before");
    public static readonly GameplayOutcomeFactId FailureAfter =
        new("reproduction.failure-after");
    public static readonly GameplayOutcomeFactId Cause =
        new("reproduction.cause");
    public static readonly GameplayOutcomeFactId FacilityId =
        new("reproduction.facility-id");
    public static readonly GameplayOutcomeFactId Costs =
        new("reproduction.costs");
    public static readonly GameplayOutcomeFactId PreviousOutcome =
        new("reproduction.previous-outcome-id");

    public static readonly GameplayMetricId[] OrderedMetrics =
    {
        RevisionBefore,
        RevisionAfter,
        PhaseBefore,
        PhaseAfter,
        PhaseElapsedBefore,
        PhaseElapsedAfter,
        ProgressBefore,
        ProgressAfter,
        EmergencyDeadlineDay
    };

    public static readonly GameplayOutcomeFactId[] OrderedFacts =
    {
        ProcessId,
        TransitionKind,
        Mode,
        Phenotype,
        StatusBefore,
        StatusAfter,
        FailureBefore,
        FailureAfter,
        Cause,
        FacilityId,
        Costs,
        PreviousOutcome
    };

    public static string OperationId(
        string processId,
        string transitionKind,
        long afterRevision)
    {
        string digest = NarrativeInferenceHash.ComputeSha256Utf8(string.Concat(
            processId,
            "\n",
            transitionKind,
            "\n",
            afterRevision.ToString(CultureInfo.InvariantCulture)));
        return "reproduction-transition:" + digest;
    }

    public static GameplayOperationId RootOperation(string processId) => new(
        "reproduction-process:"
        + NarrativeInferenceHash.ComputeSha256Utf8(processId));
}

public readonly struct ReproductionOutcomeParticipant
{
    public ReproductionOutcomeParticipant(
        GameplayEntityId entityId,
        GameplayRoleId roleId,
        KoreanNameSnapshot displayName,
        float salience,
        NarrativeMemoryTier tier)
    {
        if (!entityId.IsValid
            || !roleId.IsValid
            || string.IsNullOrWhiteSpace(displayName.DisplayText)
            || salience is < 0f or > 1f)
            throw new ArgumentException("A valid reproduction participant is required.");
        EntityId = entityId;
        RoleId = roleId;
        DisplayName = displayName;
        Salience = salience;
        Tier = tier;
    }

    public GameplayEntityId EntityId { get; }
    public GameplayRoleId RoleId { get; }
    public KoreanNameSnapshot DisplayName { get; }
    public float Salience { get; }
    public NarrativeMemoryTier Tier { get; }
}

public readonly struct ReproductionProcessTransitionOutcomeReceipt
{
    public ReproductionProcessTransitionOutcomeReceipt(
        string processId,
        string transitionKind,
        ReproductionMode mode,
        string phenotypeSpeciesId,
        string statusBefore,
        string statusAfter,
        string failureBefore,
        string failureAfter,
        string cause,
        string facilityId,
        string costs,
        int absoluteDay,
        long revisionBefore,
        long revisionAfter,
        int phaseBefore,
        int phaseAfter,
        int phaseElapsedBefore,
        int phaseElapsedAfter,
        int progressBefore,
        int progressAfter,
        int emergencyDeadlineDay,
        IEnumerable<ReproductionOutcomeParticipant> participants,
        GameplayLocationReference location,
        GameplayOutcomeId parentOutcomeId,
        GameplayOutcomeId previousProcessOutcomeId)
    {
        ProcessId = GameplayOutcomeStableIdSyntax.Require(processId, nameof(processId));
        TransitionKind = GameplayOutcomeStableIdSyntax.Require(
            transitionKind,
            nameof(transitionKind));
        if (!Enum.IsDefined(typeof(ReproductionMode), mode)
            || !GameplayOutcomeStableIdSyntax.IsValid(phenotypeSpeciesId)
            || !GameplayOutcomeStableIdSyntax.IsValid(statusBefore)
            || !GameplayOutcomeStableIdSyntax.IsValid(statusAfter)
            || !GameplayOutcomeStableIdSyntax.IsValid(failureBefore)
            || !GameplayOutcomeStableIdSyntax.IsValid(failureAfter)
            || !GameplayOutcomeStableIdSyntax.IsValid(cause)
            || !GameplayOutcomeStableIdSyntax.IsValid(facilityId)
            || absoluteDay < 0
            || revisionBefore < 0L
            || revisionAfter != revisionBefore + 1L
            || phaseBefore < 0 || phaseAfter < 0
            || phaseElapsedBefore < 0 || phaseElapsedAfter < 0
            || progressBefore < 0 || progressAfter < 0
            || emergencyDeadlineDay < 0
            || !GameplayOutcomeLedger.IsValidBoundedUtf16(
                costs,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false))
            throw new ArgumentException("A valid reproduction transition is required.");
        ReproductionOutcomeParticipant[] copied = (participants
                ?? Array.Empty<ReproductionOutcomeParticipant>())
            .ToArray();
        if (copied.Length is < 2 or > 8
            || copied.Select(value => (value.EntityId, value.RoleId))
                .Distinct().Count() != copied.Length)
            throw new ArgumentException("Reproduction participants are incomplete.");

        Mode = mode;
        PhenotypeSpeciesId = phenotypeSpeciesId;
        StatusBefore = statusBefore;
        StatusAfter = statusAfter;
        FailureBefore = failureBefore;
        FailureAfter = failureAfter;
        Cause = cause;
        FacilityId = facilityId;
        Costs = costs.Trim();
        AbsoluteDay = absoluteDay;
        RevisionBefore = revisionBefore;
        RevisionAfter = revisionAfter;
        PhaseBefore = phaseBefore;
        PhaseAfter = phaseAfter;
        PhaseElapsedBefore = phaseElapsedBefore;
        PhaseElapsedAfter = phaseElapsedAfter;
        ProgressBefore = progressBefore;
        ProgressAfter = progressAfter;
        EmergencyDeadlineDay = emergencyDeadlineDay;
        Participants = copied;
        Location = location;
        ParentOutcomeId = parentOutcomeId;
        PreviousProcessOutcomeId = previousProcessOutcomeId;
        OperationId = new GameplayOperationId(
            ReproductionProcessTransitionOutcomeIds.OperationId(
                ProcessId,
                TransitionKind,
                RevisionAfter));
    }

    public string ProcessId { get; }
    public string TransitionKind { get; }
    public ReproductionMode Mode { get; }
    public string PhenotypeSpeciesId { get; }
    public string StatusBefore { get; }
    public string StatusAfter { get; }
    public string FailureBefore { get; }
    public string FailureAfter { get; }
    public string Cause { get; }
    public string FacilityId { get; }
    public string Costs { get; }
    public int AbsoluteDay { get; }
    public long RevisionBefore { get; }
    public long RevisionAfter { get; }
    public int PhaseBefore { get; }
    public int PhaseAfter { get; }
    public int PhaseElapsedBefore { get; }
    public int PhaseElapsedAfter { get; }
    public int ProgressBefore { get; }
    public int ProgressAfter { get; }
    public int EmergencyDeadlineDay { get; }
    public IReadOnlyList<ReproductionOutcomeParticipant> Participants { get; }
    public GameplayLocationReference Location { get; }
    public GameplayOutcomeId ParentOutcomeId { get; }
    public GameplayOutcomeId PreviousProcessOutcomeId { get; }
    public GameplayOperationId OperationId { get; }
    public GameplayResultKey ResultKey => new(
        ReproductionProcessTransitionOutcomeIds.ProducerId,
        OperationId,
        RevisionAfter,
        0);
    public GameplayOutcomeStatus OutcomeStatus =>
        StatusAfter == "failed"
            ? GameplayOutcomeStatus.Failed
            : StatusAfter.StartsWith("waiting-", StringComparison.Ordinal)
                ? GameplayOutcomeStatus.PartiallySucceeded
                : GameplayOutcomeStatus.Succeeded;
}

public sealed class ReproductionProcessTransitionOutcomeAdapter :
    GameplayOutcomeAdapter<ReproductionProcessTransitionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        ReproductionProcessTransitionOutcomeIds.Transitioned;

    public override OutcomePrepareResult TryGetRequirements(
        in ReproductionProcessTransitionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        int subjectCount = receipt.Participants
            .Select(value => value.EntityId)
            .Distinct()
            .Count();
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            receipt.OutcomeStatus,
            currentWorldEpoch,
            receipt.RevisionAfter,
            receipt.Participants.Count,
            9,
            subjectCount,
            1,
            0,
            2,
            12);
        return receipt.OperationId.IsValid
            && receipt.RevisionAfter == receipt.RevisionBefore + 1L
            && receipt.Participants.Count is >= 2 and <= 8
                ? OutcomePrepareResult.Prepared()
                : new OutcomePrepareResult(
                    OutcomePrepareCode.InvalidReceipt,
                    "reproduction-transition-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in ReproductionProcessTransitionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        HashSet<GameplayEntityId> subjects = new();
        for (int index = 0; index < receipt.Participants.Count; index++)
        {
            ReproductionOutcomeParticipant value = receipt.Participants[index];
            if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                    value.EntityId,
                    value.RoleId,
                    GameplayParticipationKind.Direct,
                    true,
                    value.DisplayName)))
                return Failed();
            if (subjects.Add(value.EntityId)
                && !builder.AddSubject(new GameplayOutcomeSubjectLink(
                    value.EntityId,
                    value.Salience,
                    value.Tier,
                    value.Tier == NarrativeMemoryTier.Core,
                    false,
                    0)))
                return Failed();
        }

        GameplayEntityId owner = receipt.Participants[0].EntityId;
        double[] values =
        {
            receipt.RevisionBefore,
            receipt.RevisionAfter,
            receipt.PhaseBefore,
            receipt.PhaseAfter,
            receipt.PhaseElapsedBefore,
            receipt.PhaseElapsedAfter,
            receipt.ProgressBefore,
            receipt.ProgressAfter,
            receipt.EmergencyDeadlineDay
        };
        for (int index = 0; index < values.Length; index++)
        {
            GameplayMetricUnitId unit = index < 2
                ? ReproductionProcessTransitionOutcomeIds.RevisionUnit
                : index < 4
                    ? ReproductionProcessTransitionOutcomeIds.PhaseUnit
                    : ReproductionProcessTransitionOutcomeIds.DayUnit;
            if (!builder.AddMetric(new GameplayOutcomeMetric(
                    ReproductionProcessTransitionOutcomeIds.OrderedMetrics[index],
                    values[index],
                    unit,
                    owner)))
                return Failed();
        }

        string previous = receipt.PreviousProcessOutcomeId.IsValid
            ? receipt.PreviousProcessOutcomeId.ToString()
            : "none";
        string[] facts =
        {
            receipt.ProcessId,
            receipt.TransitionKind,
            receipt.Mode.ToString(),
            receipt.PhenotypeSpeciesId,
            receipt.StatusBefore,
            receipt.StatusAfter,
            receipt.FailureBefore,
            receipt.FailureAfter,
            receipt.Cause,
            receipt.FacilityId,
            receipt.Costs,
            previous
        };
        for (int index = 0; index < facts.Length; index++)
            if (!builder.AddFact(new GameplayOutcomeFact(
                    ReproductionProcessTransitionOutcomeIds.OrderedFacts[index],
                    facts[index])))
                return Failed();

        if (!builder.AddTag(
                ReproductionProcessTransitionOutcomeIds.ReproductionTag)
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "reproduction-process-transition-v1"))
            || !builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.OperationId.Value))
            || !builder.SetLocation(receipt.Location))
            return Failed();

        GameplayOutcomeId parent = receipt.ParentOutcomeId.IsValid
            ? receipt.ParentOutcomeId
            : receipt.PreviousProcessOutcomeId;
        if (parent.IsValid)
        {
            builder.SetCausation(new GameplayOutcomeCausation(
                parent,
                ReproductionProcessTransitionOutcomeIds.RootOperation(
                    receipt.ProcessId),
                receipt.ParentOutcomeId.IsValid
                    ? "direct-effect"
                    : "process-transition"));
        }
        return OutcomePrepareResult.Prepared();
    }

    private static OutcomePrepareResult Failed() => new(
        OutcomePrepareCode.AdapterWriteFailed,
        "reproduction-transition-write-failed");
}

public sealed class ReproductionProcessTransitionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new ReproductionTransitionPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(false);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        ReproductionProcessTransitionOutcomeIds.Transitioned;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(ReproductionProcessTransitionOutcomeIds.GeneticParentRole)
        || roleId.Equals(ReproductionProcessTransitionOutcomeIds.CarrierRole)
        || roleId.Equals(
            ReproductionProcessTransitionOutcomeIds.MedicalResponderRole)
        || roleId.Equals(ReproductionProcessTransitionOutcomeIds.OffspringRole)
        || roleId.Equals(ReproductionProcessTransitionOutcomeIds.FacilityRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId)
    {
        int index = Array.IndexOf(
            ReproductionProcessTransitionOutcomeIds.OrderedMetrics,
            metricId);
        return index >= 0
            && (index < 2
                ? unitId.Equals(
                    ReproductionProcessTransitionOutcomeIds.RevisionUnit)
                : index < 4
                    ? unitId.Equals(
                        ReproductionProcessTransitionOutcomeIds.PhaseUnit)
                    : unitId.Equals(
                        ReproductionProcessTransitionOutcomeIds.DayUnit));
    }

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.ParticipantCount is < 2 or > 8
            || outcome.SubjectCount is < 2 or > 7
            || outcome.MetricCount != 9
            || outcome.TagCount != 1
            || outcome.ProvenanceCount != 2
            || outcome.FactCount != 12
            || !outcome.GetTag(0).Equals(
                ReproductionProcessTransitionOutcomeIds.ReproductionTag))
            return OutcomeValidationResult.Reject(
                "reproduction-transition-shape-invalid");
        for (int index = 0; index < outcome.ParticipantCount; index++)
            if (!IsKnownRole(outcome.GetParticipant(index).RoleId))
                return OutcomeValidationResult.Reject(
                    "reproduction-transition-role-invalid");
        for (int index = 0; index < 9; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (!metric.MetricId.Equals(
                    ReproductionProcessTransitionOutcomeIds.OrderedMetrics[index])
                || !IsKnownMetric(metric.MetricId, metric.UnitId)
                || metric.Value < 0d
                || metric.Value != Math.Truncate(metric.Value))
                return OutcomeValidationResult.Reject(
                    "reproduction-transition-metric-invalid");
        }
        for (int index = 0; index < 12; index++)
            if (!outcome.GetFact(index).FactId.Equals(
                    ReproductionProcessTransitionOutcomeIds.OrderedFacts[index]))
                return OutcomeValidationResult.Reject(
                    "reproduction-transition-fact-invalid");
        if (outcome.GetMetric(1).Value != outcome.GetMetric(0).Value + 1d
            || !GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(0).Value)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(1).Value)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(3).Value)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(8).Value)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(9).Value)
            || string.IsNullOrWhiteSpace(outcome.GetFact(10).Value))
            return OutcomeValidationResult.Reject(
                "reproduction-transition-semantics-invalid");
        return OutcomeValidationResult.Accepted;
    }
}

internal sealed class ReproductionTransitionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective) => new(
        outcome.OutcomeId,
        perspective.Kind,
        "번식 과정의 상태가 변경되었다.",
        "reproduction-process-transition-v1",
        false);
}

