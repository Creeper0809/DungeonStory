using System;
using System.Globalization;
using DungeonStory.Narrative.Korean;

public static class CharacterAmbitionTransitionOutcomeIds
{
    public const string ProducerId = "character.ambition-transition";
    public const string ActivationReason = "activation-requirements-met";
    public const string FailureReason = "failure-conditions-met";
    public const string DeadlineReason = "deadline-reached";
    public static readonly GameplayOutcomeTypeId Transitioned =
        new("character.ambition-transitioned");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayRoleId OwnerRole = new("ambition-owner");
    public static readonly GameplayOutcomeTagId TransitionTag =
        new("ambition-transition");
    public static readonly GameplayMetricUnitId ProgressUnit = new("progress");
    public static readonly GameplayMetricUnitId DayUnit = new("day");
    public static readonly GameplayMetricId ProgressBefore =
        new("ambition.progress-before");
    public static readonly GameplayMetricId ProgressAfter =
        new("ambition.progress-after");
    public static readonly GameplayMetricId StartDayBefore =
        new("ambition.start-day-before");
    public static readonly GameplayMetricId StartDayAfter =
        new("ambition.start-day-after");
    public static readonly GameplayMetricId DueDayBefore =
        new("ambition.due-day-before");
    public static readonly GameplayMetricId DueDayAfter =
        new("ambition.due-day-after");
    public static readonly GameplayMetricId NextAllowedDayBefore =
        new("ambition.next-allowed-day-before");
    public static readonly GameplayMetricId NextAllowedDayAfter =
        new("ambition.next-allowed-day-after");
    public static readonly GameplayOutcomeFactId AmbitionId =
        new("ambition.id");
    public static readonly GameplayOutcomeFactId AmbitionDisplayName =
        new("ambition.display-name");
    public static readonly GameplayOutcomeFactId BeforeStatus =
        new("ambition.status-before");
    public static readonly GameplayOutcomeFactId AfterStatus =
        new("ambition.status-after");
    public static readonly GameplayOutcomeFactId Reason =
        new("ambition.transition-reason");
    public static readonly GameplayOutcomeFactId DefinitionRevision =
        new("ambition.definition-revision");
    public const string AmbitionDefinitionAnchor = "ambition-definition-sha256";
    public const string TransitionReasonAnchor = "ambition-transition-reason-sha256";

    public static readonly GameplayMetricId[] OrderedMetrics =
    {
        ProgressBefore,
        ProgressAfter,
        StartDayBefore,
        StartDayAfter,
        DueDayBefore,
        DueDayAfter,
        NextAllowedDayBefore,
        NextAllowedDayAfter
    };

    public static readonly GameplayOutcomeFactId[] OrderedFacts =
    {
        AmbitionId,
        AmbitionDisplayName,
        BeforeStatus,
        AfterStatus,
        Reason,
        DefinitionRevision
    };

    public static string CreateOperationId(
        string characterId,
        string ambitionId,
        int transitionDay,
        CharacterAmbitionStatus afterStatus)
    {
        string canonical = string.Concat(
            characterId,
            "\n",
            ambitionId,
            "\n",
            transitionDay.ToString(CultureInfo.InvariantCulture),
            "\n",
            afterStatus.ToString());
        return "character-ambition-transition:"
            + NarrativeInferenceHash.ComputeSha256Utf8(canonical);
    }
}

public readonly struct CharacterAmbitionTransitionOutcomeReceipt
{
    public CharacterAmbitionTransitionOutcomeReceipt(
        string operationId,
        string characterPersistentId,
        string characterDisplayName,
        string ambitionId,
        string ambitionDisplayName,
        int definitionRevision,
        CharacterAmbitionStatus beforeStatus,
        CharacterAmbitionStatus afterStatus,
        int transitionDay,
        int startDayBefore,
        int startDayAfter,
        int dueDayBefore,
        int dueDayAfter,
        int progressBefore,
        int progressAfter,
        int nextAllowedDayBefore,
        int nextAllowedDayAfter,
        string reasonCode)
    {
        OperationId = new GameplayOperationId(operationId);
        CharacterPersistentId = GameplayOutcomeStableIdSyntax.Require(
            characterPersistentId,
            nameof(characterPersistentId));
        CharacterDisplayName = EvolutionOutcomeNames.Snapshot(
            CharacterPersistentId,
            characterDisplayName);
        AmbitionId = GameplayOutcomeStableIdSyntax.Require(
            ambitionId,
            nameof(ambitionId));
        if (!GameplayOutcomeLedger.IsValidBoundedUtf16(
                ambitionDisplayName,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false))
        {
            throw new ArgumentException(
                "A bounded ambition display name is required.",
                nameof(ambitionDisplayName));
        }
        if (definitionRevision <= 0 || transitionDay < 0
            || startDayBefore < 0 || startDayAfter < 0
            || dueDayBefore < 0 || dueDayAfter < 0
            || progressBefore < 0 || progressAfter < 0
            || nextAllowedDayBefore < 0 || nextAllowedDayAfter < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(transitionDay),
                "Ambition transition counters cannot be negative.");
        }

        bool starting = afterStatus == CharacterAmbitionStatus.Active
            && beforeStatus != CharacterAmbitionStatus.Active
            && reasonCode == CharacterAmbitionTransitionOutcomeIds.ActivationReason
            && startDayAfter == transitionDay
            && dueDayAfter > transitionDay
            && progressAfter == 0
            && nextAllowedDayAfter == nextAllowedDayBefore;
        bool failing = beforeStatus == CharacterAmbitionStatus.Active
            && afterStatus == CharacterAmbitionStatus.Failed
            && (reasonCode == CharacterAmbitionTransitionOutcomeIds.FailureReason
                || reasonCode
                    == CharacterAmbitionTransitionOutcomeIds.DeadlineReason)
            && startDayBefore <= transitionDay
            && startDayAfter == 0
            && (reasonCode
                    != CharacterAmbitionTransitionOutcomeIds.DeadlineReason
                || dueDayBefore > 0 && dueDayBefore <= transitionDay)
            && dueDayAfter == 0
            && progressAfter == 0
            && nextAllowedDayAfter == checked(transitionDay + 10);
        if (!starting && !failing)
            throw new ArgumentException("The ambition transition is invalid.");

        AmbitionDisplayName = ambitionDisplayName.Trim();
        DefinitionRevision = definitionRevision;
        BeforeStatus = beforeStatus;
        AfterStatus = afterStatus;
        TransitionDay = transitionDay;
        StartDayBefore = startDayBefore;
        StartDayAfter = startDayAfter;
        DueDayBefore = dueDayBefore;
        DueDayAfter = dueDayAfter;
        ProgressBefore = progressBefore;
        ProgressAfter = progressAfter;
        NextAllowedDayBefore = nextAllowedDayBefore;
        NextAllowedDayAfter = nextAllowedDayAfter;
        ReasonCode = GameplayOutcomeStableIdSyntax.Require(
            reasonCode,
            nameof(reasonCode));
    }

    public GameplayOperationId OperationId { get; }
    public string CharacterPersistentId { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public string AmbitionId { get; }
    public string AmbitionDisplayName { get; }
    public int DefinitionRevision { get; }
    public CharacterAmbitionStatus BeforeStatus { get; }
    public CharacterAmbitionStatus AfterStatus { get; }
    public int TransitionDay { get; }
    public int StartDayBefore { get; }
    public int StartDayAfter { get; }
    public int DueDayBefore { get; }
    public int DueDayAfter { get; }
    public int ProgressBefore { get; }
    public int ProgressAfter { get; }
    public int NextAllowedDayBefore { get; }
    public int NextAllowedDayAfter { get; }
    public string ReasonCode { get; }
    public GameplayResultKey ResultKey => new(
        CharacterAmbitionTransitionOutcomeIds.ProducerId,
        OperationId,
        1L,
        0);
}

public sealed class CharacterAmbitionTransitionOutcomeAdapter :
    GameplayOutcomeAdapter<CharacterAmbitionTransitionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        CharacterAmbitionTransitionOutcomeIds.Transitioned;

    public override OutcomePrepareResult TryGetRequirements(
        in CharacterAmbitionTransitionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.TransitionDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            ownerRevision: 1L,
            participantCount: 1,
            metricCount: 8,
            subjectCount: 1,
            tagCount: 1,
            anchorCount: 2,
            provenanceCount: 2,
            factCount: 6);
        bool valid = receipt.OperationId.IsValid
            && GameplayOutcomeStableIdSyntax.IsValid(
                receipt.CharacterPersistentId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.AmbitionId)
            && receipt.DefinitionRevision > 0
            && receipt.TransitionDay >= 0
            && receipt.StartDayBefore >= 0
            && receipt.StartDayAfter >= 0
            && receipt.DueDayBefore >= 0
            && receipt.DueDayAfter >= 0
            && receipt.ProgressBefore >= 0
            && receipt.ProgressAfter >= 0
            && receipt.NextAllowedDayBefore >= 0
            && receipt.NextAllowedDayAfter >= 0
            && (receipt.ReasonCode
                    == CharacterAmbitionTransitionOutcomeIds.ActivationReason
                || receipt.ReasonCode
                    == CharacterAmbitionTransitionOutcomeIds.FailureReason
                || receipt.ReasonCode
                    == CharacterAmbitionTransitionOutcomeIds.DeadlineReason);
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "character-ambition-transition-receipt-invalid");
    }

    public override OutcomePrepareResult TryWrite(
        in CharacterAmbitionTransitionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId character = new(
            CharacterAmbitionTransitionOutcomeIds.CharacterKind,
            receipt.CharacterPersistentId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                character,
                CharacterAmbitionTransitionOutcomeIds.OwnerRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CharacterDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                character,
                0.72f,
                NarrativeMemoryTier.Core,
                true,
                false,
                0))
            && AddMetric(
                ref builder,
                CharacterAmbitionTransitionOutcomeIds.ProgressBefore,
                receipt.ProgressBefore,
                CharacterAmbitionTransitionOutcomeIds.ProgressUnit,
                character)
            && AddMetric(
                ref builder,
                CharacterAmbitionTransitionOutcomeIds.ProgressAfter,
                receipt.ProgressAfter,
                CharacterAmbitionTransitionOutcomeIds.ProgressUnit,
                character)
            && AddMetric(
                ref builder,
                CharacterAmbitionTransitionOutcomeIds.StartDayBefore,
                receipt.StartDayBefore,
                CharacterAmbitionTransitionOutcomeIds.DayUnit,
                character)
            && AddMetric(
                ref builder,
                CharacterAmbitionTransitionOutcomeIds.StartDayAfter,
                receipt.StartDayAfter,
                CharacterAmbitionTransitionOutcomeIds.DayUnit,
                character)
            && AddMetric(
                ref builder,
                CharacterAmbitionTransitionOutcomeIds.DueDayBefore,
                receipt.DueDayBefore,
                CharacterAmbitionTransitionOutcomeIds.DayUnit,
                character)
            && AddMetric(
                ref builder,
                CharacterAmbitionTransitionOutcomeIds.DueDayAfter,
                receipt.DueDayAfter,
                CharacterAmbitionTransitionOutcomeIds.DayUnit,
                character)
            && AddMetric(
                ref builder,
                CharacterAmbitionTransitionOutcomeIds.NextAllowedDayBefore,
                receipt.NextAllowedDayBefore,
                CharacterAmbitionTransitionOutcomeIds.DayUnit,
                character)
            && AddMetric(
                ref builder,
                CharacterAmbitionTransitionOutcomeIds.NextAllowedDayAfter,
                receipt.NextAllowedDayAfter,
                CharacterAmbitionTransitionOutcomeIds.DayUnit,
                character)
            && builder.AddTag(
                CharacterAmbitionTransitionOutcomeIds.TransitionTag)
            && builder.AddAnchor(
                character,
                Hash(
                    CharacterAmbitionTransitionOutcomeIds
                        .AmbitionDefinitionAnchor,
                    receipt.AmbitionId + ":"
                        + receipt.DefinitionRevision.ToString(
                            CultureInfo.InvariantCulture)))
            && builder.AddAnchor(
                character,
                Hash(
                    CharacterAmbitionTransitionOutcomeIds
                        .TransitionReasonAnchor,
                    receipt.ReasonCode))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt",
                "character-ambition-transition-v1"))
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "domain-commit",
                receipt.OperationId.Value))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterAmbitionTransitionOutcomeIds.AmbitionId,
                receipt.AmbitionId))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterAmbitionTransitionOutcomeIds.AmbitionDisplayName,
                receipt.AmbitionDisplayName))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterAmbitionTransitionOutcomeIds.BeforeStatus,
                receipt.BeforeStatus.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterAmbitionTransitionOutcomeIds.AfterStatus,
                receipt.AfterStatus.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterAmbitionTransitionOutcomeIds.Reason,
                receipt.ReasonCode))
            && builder.AddFact(new GameplayOutcomeFact(
                CharacterAmbitionTransitionOutcomeIds.DefinitionRevision,
                receipt.DefinitionRevision.ToString(
                    CultureInfo.InvariantCulture)));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "character-ambition-transition-write-failed");
    }

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId character) => builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, character));

    private static NarrativeEvidenceReference Hash(string type, string value) =>
        new(type, NarrativeInferenceHash.ComputeSha256Utf8(value));
}

public sealed class CharacterAmbitionTransitionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new CharacterAmbitionTransitionPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new EvolutionOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new EvolutionOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new EvolutionOutcomeConsolidator(false);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        CharacterAmbitionTransitionOutcomeIds.Transitioned;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(CharacterAmbitionTransitionOutcomeIds.OwnerRole);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        unitId.Equals(CharacterAmbitionTransitionOutcomeIds.ProgressUnit)
            && (metricId.Equals(
                    CharacterAmbitionTransitionOutcomeIds.ProgressBefore)
                || metricId.Equals(
                    CharacterAmbitionTransitionOutcomeIds.ProgressAfter))
        || unitId.Equals(CharacterAmbitionTransitionOutcomeIds.DayUnit)
            && (metricId.Equals(
                    CharacterAmbitionTransitionOutcomeIds.StartDayBefore)
                || metricId.Equals(
                    CharacterAmbitionTransitionOutcomeIds.StartDayAfter)
                || metricId.Equals(
                    CharacterAmbitionTransitionOutcomeIds.DueDayBefore)
                || metricId.Equals(
                    CharacterAmbitionTransitionOutcomeIds.DueDayAfter)
                || metricId.Equals(
                    CharacterAmbitionTransitionOutcomeIds
                        .NextAllowedDayBefore)
                || metricId.Equals(
                    CharacterAmbitionTransitionOutcomeIds
                        .NextAllowedDayAfter));

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome)
    {
        if (outcome.OutcomeTypeId != OutcomeTypeId
            || outcome.ParticipantCount != 1
            || outcome.SubjectCount != 1
            || outcome.MetricCount != 8
            || outcome.TagCount != 1
            || outcome.AnchorCount != 2
            || outcome.ProvenanceCount != 2
            || outcome.FactCount != 6
            || outcome.OwnerRevision != 1L)
        {
            return OutcomeValidationResult.Reject(
                "character-ambition-transition-shape-invalid");
        }
        GameplayOutcomeParticipant participant = outcome.GetParticipant(0);
        if (!participant.RoleId.Equals(
                CharacterAmbitionTransitionOutcomeIds.OwnerRole)
            || !participant.EntityId.Kind.Equals(
                CharacterAmbitionTransitionOutcomeIds.CharacterKind)
            || outcome.GetSubject(0).SubjectId != participant.EntityId
            || !outcome.GetTag(0).Equals(
                CharacterAmbitionTransitionOutcomeIds.TransitionTag))
        {
            return OutcomeValidationResult.Reject(
                "character-ambition-transition-owner-invalid");
        }
        for (int index = 0;
             index < CharacterAmbitionTransitionOutcomeIds.OrderedMetrics.Length;
             index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            GameplayMetricUnitId expectedUnit = index < 2
                ? CharacterAmbitionTransitionOutcomeIds.ProgressUnit
                : CharacterAmbitionTransitionOutcomeIds.DayUnit;
            if (!metric.MetricId.Equals(
                    CharacterAmbitionTransitionOutcomeIds
                        .OrderedMetrics[index])
                || !metric.UnitId.Equals(expectedUnit)
                || metric.DefinitionOrInstanceId != participant.EntityId
                || metric.Value < 0d
                || metric.Value != Math.Truncate(metric.Value))
            {
                return OutcomeValidationResult.Reject(
                    "character-ambition-transition-metrics-invalid");
            }
        }
        for (int index = 0;
             index < CharacterAmbitionTransitionOutcomeIds.OrderedFacts.Length;
             index++)
        {
            if (!outcome.GetFact(index).FactId.Equals(
                    CharacterAmbitionTransitionOutcomeIds.OrderedFacts[index]))
            {
                return OutcomeValidationResult.Reject(
                    "character-ambition-transition-facts-invalid");
            }
        }

        string before = outcome.GetFact(2).Value;
        string after = outcome.GetFact(3).Value;
        string reason = outcome.GetFact(4).Value;
        double progressAfter = outcome.GetMetric(1).Value;
        double startBefore = outcome.GetMetric(2).Value;
        double startAfter = outcome.GetMetric(3).Value;
        double dueBefore = outcome.GetMetric(4).Value;
        double dueAfter = outcome.GetMetric(5).Value;
        double nextBefore = outcome.GetMetric(6).Value;
        double nextAfter = outcome.GetMetric(7).Value;
        bool starting = after == nameof(CharacterAmbitionStatus.Active)
            && before != nameof(CharacterAmbitionStatus.Active)
            && reason
                == CharacterAmbitionTransitionOutcomeIds.ActivationReason
            && startAfter == outcome.AbsoluteDay
            && dueAfter > outcome.AbsoluteDay
            && progressAfter == 0d
            && nextAfter == nextBefore;
        bool failing = before == nameof(CharacterAmbitionStatus.Active)
            && after == nameof(CharacterAmbitionStatus.Failed)
            && (reason == CharacterAmbitionTransitionOutcomeIds.FailureReason
                || reason
                    == CharacterAmbitionTransitionOutcomeIds.DeadlineReason)
            && startBefore <= outcome.AbsoluteDay
            && startAfter == 0d
            && (reason != CharacterAmbitionTransitionOutcomeIds.DeadlineReason
                || dueBefore > 0d && dueBefore <= outcome.AbsoluteDay)
            && dueAfter == 0d
            && progressAfter == 0d
            && nextAfter == outcome.AbsoluteDay + 10d;
        if ((!starting && !failing)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                outcome.GetFact(0).Value)
            || string.IsNullOrWhiteSpace(outcome.GetFact(1).Value)
            || !int.TryParse(
                outcome.GetFact(5).Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int revision)
            || revision <= 0
            || !HasHash(
                outcome.GetAnchor(0),
                CharacterAmbitionTransitionOutcomeIds
                    .AmbitionDefinitionAnchor,
                outcome.GetFact(0).Value + ":" + outcome.GetFact(5).Value)
            || !HasHash(
                outcome.GetAnchor(1),
                CharacterAmbitionTransitionOutcomeIds
                    .TransitionReasonAnchor,
                reason))
        {
            return OutcomeValidationResult.Reject(
                "character-ambition-transition-semantics-invalid");
        }
        return OutcomeValidationResult.Accepted;
    }

    private static bool HasHash(
        NarrativeEvidenceReference value,
        string type,
        string fact) => value.AnchorTypeId == type
            && value.AnchorId == NarrativeInferenceHash.ComputeSha256Utf8(fact);
}

internal sealed class CharacterAmbitionTransitionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        bool started = outcome.GetFact(3).Value
            == nameof(CharacterAmbitionStatus.Active);
        string text = outcome.GetParticipant(0).DisplayName.DisplayText
            + ": 야망 「" + outcome.GetFact(1).Value + "」"
            + (started ? "을 시작했다." : "이 실패로 끝났다.");
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            "character-ambition-transition-v1",
            true);
    }
}
