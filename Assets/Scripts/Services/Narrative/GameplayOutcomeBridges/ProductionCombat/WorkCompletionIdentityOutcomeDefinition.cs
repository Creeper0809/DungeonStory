using System;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class WorkCompletionIdentityOutcomeIds
{
    public const string ProducerId = "character.work-identity";

    public static readonly GameplayOutcomeTypeId Applied =
        new("character.work-identity-applied");
    public static readonly GameplayEntityKindId CharacterKind =
        new("character");
    public static readonly GameplayEntityKindId BuildingKind =
        new("building");
    public static readonly GameplayRoleId WorkerRole = new("worker");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("work-identity.owner-revision");
    public static readonly GameplayMetricId ContributionMetric =
        new("work-identity.contribution-work-units");
    public static readonly GameplayMetricUnitId RevisionUnit =
        new("revision");
    public static readonly GameplayMetricUnitId WorkUnit =
        new("work-unit");
    public static readonly GameplayOutcomeTagId WorkIdentityTag =
        new("work-identity");
    public static readonly GameplayOutcomeFactId WorkIdFact =
        new("work-identity.work-id");
    public static readonly GameplayOutcomeFactId ProductIdFact =
        new("work-identity.product-id");
    public static readonly GameplayOutcomeFactId OriginFact =
        new("work-identity.origin");
    public static readonly GameplayOutcomeFactId ReactionFact =
        new("work-identity.reaction");
    public static readonly GameplayOutcomeFactId ProducerStreamFact =
        new("work-identity.producer-stream");
    public static readonly GameplayOutcomeFactId OperationIdFact =
        new("work-identity.operation-id");
    public static readonly GameplayOutcomeFactId TerminalOwnerFact =
        new("work-identity.terminal-owner");
    public static readonly GameplayOutcomeFactId ChildResultKeyFact =
        new("work-identity.child-result-key");
    public static readonly GameplayOutcomeFactId SkillModifierProvenanceFact =
        new("work-identity.skill-modifier-provenance");

    public static GameplayResultKey ResultKey(
        WorkCompletionIdentityDeliveryRequest request) => new(
        ProducerId,
        new GameplayOperationId(request.DeliveryId),
        checked((long)request.OperationSequence + 1L),
        0);

    public static string StableOrigin(CharacterCommandOrigin origin) => origin switch
    {
        CharacterCommandOrigin.Autonomous => "autonomous",
        CharacterCommandOrigin.DirectPlayerOrder => "direct-player-order",
        CharacterCommandOrigin.ScriptedForced => "scripted-forced",
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
    };
}

public readonly struct WorkCompletionIdentityOutcomeReceipt
{
    public WorkCompletionIdentityOutcomeReceipt(
        WorkCompletionIdentityDeliveryRequest request,
        KoreanNameSnapshot characterDisplayName)
    {
        GameplayResultKey resultKey =
            WorkCompletionIdentityOutcomeIds.ResultKey(request);
        if (!resultKey.IsValid
            || !request.Character.IsValid
            || !GameplayOutcomeStableIdSyntax.IsValid(request.WorkId)
            || !GameplayOutcomeStableIdSyntax.IsValid(request.DeliveryId)
            || !GameplayOutcomeStableIdSyntax.IsValid(request.ProducerStreamId)
            || request.OperationSequence < 0
            || request.AbsoluteDay < 0
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), request.Origin)
            || !GameplayOutcomeStableIdSyntax.IsValid(request.OperationId)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                request.TerminalOwnerId)
            || string.IsNullOrWhiteSpace(request.ChildResultKey)
            || !GameplayOutcomeStableIdSyntax.IsValid(
                request.TargetEntityId)
            || !GameplayOutcomeLedger.IsValidBoundedUtf16(
                request.SkillModifierProvenance,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false)
            || !float.IsFinite(request.ContributionWorkUnits)
            || request.ContributionWorkUnits < 0f
            || request.PayloadFingerprint?.Length != 64
            || string.IsNullOrWhiteSpace(characterDisplayName.DisplayText)
            || string.IsNullOrWhiteSpace(
                characterDisplayName.DisplaySnapshotRevision))
        {
            throw new ArgumentException(
                "A canonical work-completion identity receipt is required.",
                nameof(request));
        }

        Request = request;
        CharacterDisplayName = characterDisplayName;
        ResultKey = resultKey;
    }

    public WorkCompletionIdentityDeliveryRequest Request { get; }
    public KoreanNameSnapshot CharacterDisplayName { get; }
    public GameplayResultKey ResultKey { get; }
    public long OwnerRevision => ResultKey.CommitRevision;
    public bool IsFailedWork => Request.ProductId.StartsWith(
        "outcome:",
        StringComparison.Ordinal);
}

public sealed class WorkCompletionIdentityOutcomeAdapter :
    GameplayOutcomeAdapter<WorkCompletionIdentityOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        WorkCompletionIdentityOutcomeIds.Applied;

    public override OutcomePrepareResult TryGetRequirements(
        in WorkCompletionIdentityOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!WorkCompletionIdentityOutcomeValidation.IsValidReceipt(receipt))
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "work-identity-receipt-invalid");
        }

        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.Request.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 1,
            metricCount: 2,
            subjectCount: receipt.Request.TargetEntityId ==
                WorkCompletionIdentityReferenceDefaults.None ? 1 : 2,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 9);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in WorkCompletionIdentityOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        if (!WorkCompletionIdentityOutcomeValidation.IsValidReceipt(receipt))
            return Failed("work-identity-receipt-invalid");

        GameplayEntityId character = new(
            WorkCompletionIdentityOutcomeIds.CharacterKind,
            receipt.Request.Character.Value);
        bool hasTarget = !string.Equals(
            receipt.Request.TargetEntityId,
            WorkCompletionIdentityReferenceDefaults.None,
            StringComparison.Ordinal);
        GameplayEntityId target = hasTarget
            ? new GameplayEntityId(
                WorkCompletionIdentityOutcomeIds.BuildingKind,
                receipt.Request.TargetEntityId)
            : default;
        if (!builder.AddParticipant(new GameplayOutcomeParticipant(
                character,
                WorkCompletionIdentityOutcomeIds.WorkerRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.CharacterDisplayName))
            || !builder.AddSubject(new GameplayOutcomeSubjectLink(
                character,
                receipt.IsFailedWork ? 0.55f : 0.38f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            || (hasTarget && !builder.AddSubject(
                new GameplayOutcomeSubjectLink(
                    target,
                    receipt.IsFailedWork ? 0.35f : 0.28f,
                    NarrativeMemoryTier.Recent,
                    false,
                    false,
                    0)))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                WorkCompletionIdentityOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                WorkCompletionIdentityOutcomeIds.RevisionUnit,
                character))
            || !builder.AddMetric(new GameplayOutcomeMetric(
                WorkCompletionIdentityOutcomeIds.ContributionMetric,
                receipt.Request.ContributionWorkUnits,
                WorkCompletionIdentityOutcomeIds.WorkUnit,
                character))
            || !builder.AddTag(
                WorkCompletionIdentityOutcomeIds.WorkIdentityTag)
            || !builder.AddProvenance(
                new GameplayOutcomeProvenanceReference(
                    "identity-delivery",
                    receipt.Request.DeliveryId))
            || !builder.AddProvenance(
                new GameplayOutcomeProvenanceReference(
                    "identity-payload-fingerprint",
                    receipt.Request.PayloadFingerprint))
            || !builder.AddFact(new GameplayOutcomeFact(
                WorkCompletionIdentityOutcomeIds.WorkIdFact,
                receipt.Request.WorkId))
            || !builder.AddFact(new GameplayOutcomeFact(
                WorkCompletionIdentityOutcomeIds.ProductIdFact,
                string.IsNullOrEmpty(receipt.Request.ProductId)
                    ? "none"
                    : receipt.Request.ProductId))
            || !builder.AddFact(new GameplayOutcomeFact(
                WorkCompletionIdentityOutcomeIds.OriginFact,
                WorkCompletionIdentityOutcomeIds.StableOrigin(
                    receipt.Request.Origin)))
            || !builder.AddFact(new GameplayOutcomeFact(
                WorkCompletionIdentityOutcomeIds.ReactionFact,
                receipt.IsFailedWork ? "failed-work" : "completed-work"))
            || !builder.AddFact(new GameplayOutcomeFact(
                WorkCompletionIdentityOutcomeIds.ProducerStreamFact,
                receipt.Request.ProducerStreamId))
            || !builder.AddFact(new GameplayOutcomeFact(
                WorkCompletionIdentityOutcomeIds.OperationIdFact,
                receipt.Request.OperationId))
            || !builder.AddFact(new GameplayOutcomeFact(
                WorkCompletionIdentityOutcomeIds.TerminalOwnerFact,
                receipt.Request.TerminalOwnerId))
            || !builder.AddFact(new GameplayOutcomeFact(
                WorkCompletionIdentityOutcomeIds.ChildResultKeyFact,
                receipt.Request.ChildResultKey))
            || !builder.AddFact(new GameplayOutcomeFact(
                WorkCompletionIdentityOutcomeIds.SkillModifierProvenanceFact,
                receipt.Request.SkillModifierProvenance)))
        {
            return Failed("work-identity-write-failed");
        }
        return OutcomePrepareResult.Prepared();
    }

    private static OutcomePrepareResult Failed(string detail) => new(
        OutcomePrepareCode.AdapterWriteFailed,
        detail);
}

public readonly struct PreparedWorkCompletionIdentityOutcome
{
    internal PreparedWorkCompletionIdentityOutcome(
        PreparedOwnerOutcome prepared,
        GameplayResultKey resultKey,
        long ownerRevision,
        bool replay)
    {
        Prepared = prepared;
        ResultKey = resultKey;
        OwnerRevision = ownerRevision;
        IsReplay = replay;
    }

    internal PreparedOwnerOutcome Prepared { get; }
    public GameplayResultKey ResultKey { get; }
    public long OwnerRevision { get; }
    public bool IsReplay { get; }
    public bool IsValid => IsReplay ? ResultKey.IsValid : Prepared.IsValid;
}

public sealed class WorkCompletionIdentityGameplayOutcomeBridge
{
    private readonly IGameplayOutcomeDisplayNameQuery displayNames;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    private readonly IGameplayOutcomeQuery query;
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public WorkCompletionIdentityGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameplayOutcomeDisplayNameQuery displayNames,
        IGameplayOutcomeQuery query)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
        this.displayNames = displayNames
            ?? throw new ArgumentNullException(nameof(displayNames));
        this.diagnostics = diagnostics;
        this.query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public bool TryPrepare(
        WorkCompletionIdentityDeliveryRequest request,
        out PreparedWorkCompletionIdentityOutcome prepared,
        out bool capacityDeferred,
        out string failureReason)
    {
        prepared = default;
        if (request.ChildResultKey != WorkCompletionIdentityReferenceDefaults.None
            && !TryValidateCommittedChild(request, out failureReason))
        {
            capacityDeferred = false;
            return false;
        }
        GameplayEntityId character = new(
            WorkCompletionIdentityOutcomeIds.CharacterKind,
            request.Character.Value);
        if (!displayNames.TryGetCurrentName(
                character,
                out KoreanNameSnapshot characterName))
        {
            capacityDeferred = false;
            failureReason = "work-identity-character-name-missing";
            return false;
        }

        WorkCompletionIdentityOutcomeReceipt receipt;
        try
        {
            receipt = new WorkCompletionIdentityOutcomeReceipt(
                request,
                characterName);
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            capacityDeferred = false;
            failureReason = "work-identity-receipt-invalid:"
                + exception.Message;
            return false;
        }

        if (!transactions.TryPrepare(
                receipt,
                out PreparedOwnerOutcome token,
                out OwnerOutcomeCommitResult replay,
                out capacityDeferred,
                out failureReason))
        {
            return false;
        }
        prepared = new PreparedWorkCompletionIdentityOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public bool TryValidateCommittedChild(
        WorkCompletionIdentityDeliveryRequest request, out string failureReason)
    {
        GameplayResultKey child;
        try
        {
            child = request.WorkId == BuiltInWorkTypeIds.Repair.Value
                ? RepairWorkCompletionObservation.RequireCompletionChild(request)
                : request.WorkId == BuiltInWorkTypeIds.ThreatMitigation.Value
                    ? EnvironmentalFireCompletionIdentity.RequireCompletionChild(request)
                : WorkCompletionChildReference.Require(
                    request.OperationId, request.TerminalOwnerId, request.ChildResultKey);
        }
        catch (ArgumentException error)
        {
            failureReason = "work-identity-child-invalid:" + error.Message;
            return false;
        }
        if (!diagnostics.TryGetResultIdentity(child, out GameplayOutcomeReplayIdentity identity)
            || identity.State is not (GameplayOutcomeReplayState.Committed
                or GameplayOutcomeReplayState.DeliveryFaultPending
                or GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement
                or GameplayOutcomeReplayState.PublishedAcknowledged
                or GameplayOutcomeReplayState.Compacted
                or GameplayOutcomeReplayState.Forgotten))
        {
            failureReason = "work-identity-child-not-committed:" + child;
            return false;
        }
        if (request.WorkId == BuiltInWorkTypeIds.Repair.Value
            && query.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot exact)
            && !RepairWorkCompletionObservation.MatchesCompletionChild(request, exact))
        {
            failureReason = "work-identity-repair-child-payload-conflict:" + child;
            return false;
        }
        if (request.WorkId == BuiltInWorkTypeIds.ThreatMitigation.Value
            && query.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot fireChild)
            && !EnvironmentalFireCompletionIdentity.MatchesCompletionChild(request, fireChild))
        {
            failureReason = "work-identity-fire-child-payload-conflict:" + child;
            return false;
        }
        if (request.WorkId == BuiltInWorkTypeIds.AnimalCare.Value)
        {
            if (request.TerminalOwnerId != MigratedProducerOutcomeCatalog.Get(
                    MigratedProducerOutcomeKind.AnimalHusbandryWorkResult).ProducerId
                || child.ProducerId != request.TerminalOwnerId)
            {
                failureReason = "work-identity-husbandry-child-owner-conflict:" + child;
                return false;
            }
            if (query.TryGetExact(identity.OutcomeId, out GameplayOutcomeSnapshot husbandryChild)
                && !AnimalHusbandryCompletionObservation.MatchesCompletionChild(request, husbandryChild))
            {
                failureReason = "work-identity-husbandry-child-payload-conflict:" + child;
                return false;
            }
        }
        failureReason = string.Empty;
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedWorkCompletionIdentityOutcome prepared) =>
        prepared.IsReplay
            ? transactions.Reconcile(prepared.ResultKey)
            : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public OwnerOutcomeCommitResult Reconcile(
        WorkCompletionIdentityDeliveryRequest request) =>
        transactions.Reconcile(
            WorkCompletionIdentityOutcomeIds.ResultKey(request));

    public void Cancel(in PreparedWorkCompletionIdentityOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal static class WorkCompletionIdentityOutcomeValidation
{
    public static bool IsValidReceipt(
        in WorkCompletionIdentityOutcomeReceipt receipt)
    {
        WorkCompletionIdentityDeliveryRequest request = receipt.Request;
        return receipt.ResultKey.IsValid
            && string.Equals(
                receipt.ResultKey.ProducerId,
                WorkCompletionIdentityOutcomeIds.ProducerId,
                StringComparison.Ordinal)
            && receipt.OwnerRevision == checked(
                (long)request.OperationSequence + 1L)
            && receipt.ResultKey.LocalResultIndex == 0
            && string.Equals(
                receipt.ResultKey.OperationId.Value,
                request.DeliveryId,
                StringComparison.Ordinal)
            && request.Character.IsValid
            && GameplayOutcomeStableIdSyntax.IsValid(request.WorkId)
            && GameplayOutcomeStableIdSyntax.IsValid(request.DeliveryId)
            && GameplayOutcomeStableIdSyntax.IsValid(request.ProducerStreamId)
            && request.OperationSequence >= 0
            && request.AbsoluteDay >= 0
            && GameplayOutcomeStableIdSyntax.IsValid(request.OperationId)
            && GameplayOutcomeStableIdSyntax.IsValid(
                request.TerminalOwnerId)
            && !string.IsNullOrWhiteSpace(request.ChildResultKey)
            && GameplayOutcomeStableIdSyntax.IsValid(
                request.TargetEntityId)
            && GameplayOutcomeLedger.IsValidBoundedUtf16(
                request.SkillModifierProvenance,
                GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length,
                allowEmpty: false)
            && float.IsFinite(request.ContributionWorkUnits)
            && request.ContributionWorkUnits >= 0f
            && request.PayloadFingerprint?.Length == 64
            && !string.IsNullOrWhiteSpace(
                receipt.CharacterDisplayName.DisplayText)
            && !string.IsNullOrWhiteSpace(
                receipt.CharacterDisplayName.DisplaySnapshotRevision);
    }

    public static bool IsValidOutcome(in GameplayOutcomeReadView outcome)
        => TryValidateOutcome(outcome, out _);

    public static bool TryValidateOutcome(
        in GameplayOutcomeReadView outcome,
        out string failureReason)
    {
        if (outcome.OutcomeTypeId
                != WorkCompletionIdentityOutcomeIds.Applied
            || outcome.Status != GameplayOutcomeStatus.Succeeded
            || !outcome.ResultKey.IsValid
            || !string.Equals(
                outcome.ResultKey.ProducerId,
                WorkCompletionIdentityOutcomeIds.ProducerId,
                StringComparison.Ordinal)
            || outcome.OwnerRevision != outcome.ResultKey.CommitRevision
            || outcome.ResultKey.LocalResultIndex != 0
            || outcome.ParticipantCount != 1
            || outcome.SubjectCount is < 1 or > 2
            || outcome.MetricCount != 2
            || outcome.TagCount != 1
            // The producer has no initial anchors. Later evidence-use anchors
            // are ledger-owned mutable retention metadata, validated by the
            // ledger independently of this immutable work receipt.
            || outcome.InitialAnchorCount != 0
            || outcome.ProvenanceCount != 2
            || outcome.FactCount != 9)
        {
            failureReason = "work-identity-header-shape-invalid";
            return false;
        }

        GameplayOutcomeParticipant participant = outcome.GetParticipant(0);
        GameplayOutcomeMetric metric = outcome.GetMetric(0);
        GameplayOutcomeMetric contribution = outcome.GetMetric(1);
        bool hasTarget = outcome.SubjectCount == 2;
        bool hasWorkerSubject = TryFindSubject(
            outcome,
            participant.EntityId,
            out _);
        bool hasBuildingSubject = TryFindSubjectByKind(
            outcome,
            WorkCompletionIdentityOutcomeIds.BuildingKind,
            out GameplayOutcomeSubjectLink targetSubject);
        if (!participant.RoleId.Equals(
                WorkCompletionIdentityOutcomeIds.WorkerRole)
            || !participant.EntityId.Kind.Equals(
                WorkCompletionIdentityOutcomeIds.CharacterKind)
            || !hasWorkerSubject)
        {
            failureReason = "work-identity-worker-shape-invalid";
            return false;
        }
        if (!metric.MetricId.Equals(
                WorkCompletionIdentityOutcomeIds.OwnerRevisionMetric)
            || !metric.UnitId.Equals(
                WorkCompletionIdentityOutcomeIds.RevisionUnit)
            || !metric.DefinitionOrInstanceId.Equals(participant.EntityId)
            || Math.Abs(metric.Value - outcome.OwnerRevision) >= 0.00001d)
        {
            failureReason = "work-identity-owner-revision-metric-invalid";
            return false;
        }
        if (!contribution.MetricId.Equals(
                WorkCompletionIdentityOutcomeIds.ContributionMetric)
            || !contribution.UnitId.Equals(
                WorkCompletionIdentityOutcomeIds.WorkUnit)
            || !contribution.DefinitionOrInstanceId.Equals(
                participant.EntityId)
            || contribution.Value < 0d)
        {
            failureReason = "work-identity-contribution-metric-invalid";
            return false;
        }
        if (outcome.SubjectCount != (hasTarget ? 2 : 1)
            || hasTarget != hasBuildingSubject
            || hasTarget
            && !targetSubject.SubjectId.Kind.Equals(
                WorkCompletionIdentityOutcomeIds.BuildingKind))
        {
            failureReason = "work-identity-target-subject-invalid";
            return false;
        }
        if (!outcome.GetTag(0).Equals(
                WorkCompletionIdentityOutcomeIds.WorkIdentityTag)
            || !HasFact(outcome, WorkCompletionIdentityOutcomeIds.WorkIdFact)
            || !HasFact(
                outcome,
                WorkCompletionIdentityOutcomeIds.ProductIdFact)
            || !HasFact(outcome, WorkCompletionIdentityOutcomeIds.OriginFact)
            || !HasFact(
                outcome,
                WorkCompletionIdentityOutcomeIds.ReactionFact)
            || !HasFact(
                outcome,
                WorkCompletionIdentityOutcomeIds.ProducerStreamFact)
            || !HasFact(
                outcome,
                WorkCompletionIdentityOutcomeIds.OperationIdFact)
            || !HasFact(
                outcome,
                WorkCompletionIdentityOutcomeIds.TerminalOwnerFact)
            || !HasFact(
                outcome,
                WorkCompletionIdentityOutcomeIds.ChildResultKeyFact)
            || !HasFact(
                outcome,
                WorkCompletionIdentityOutcomeIds.SkillModifierProvenanceFact))
        {
            failureReason = "work-identity-tag-or-fact-invalid";
            return false;
        }
        failureReason = string.Empty;
        return true;
    }

    public static bool TryFindSubject(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        out GameplayOutcomeSubjectLink subject)
    {
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink candidate = outcome.GetSubject(index);
            if (candidate.SubjectId.Equals(subjectId))
            {
                subject = candidate;
                return true;
            }
        }
        subject = default;
        return false;
    }

    private static bool TryFindSubjectByKind(
        in GameplayOutcomeReadView outcome,
        GameplayEntityKindId kind,
        out GameplayOutcomeSubjectLink subject)
    {
        for (int index = 0; index < outcome.SubjectCount; index++)
        {
            GameplayOutcomeSubjectLink candidate = outcome.GetSubject(index);
            if (candidate.SubjectId.Kind.Equals(kind))
            {
                subject = candidate;
                return true;
            }
        }
        subject = default;
        return false;
    }

    public static string Fact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId id)
    {
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(id)) return fact.Value;
        }
        return string.Empty;
    }

    private static bool HasFact(
        in GameplayOutcomeReadView outcome,
        GameplayOutcomeFactId id) =>
        !string.IsNullOrWhiteSpace(Fact(outcome, id));
}

internal sealed class WorkCompletionIdentityMemoryPolicy :
    IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;

    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(
        outcome.OutcomeTypeId.Value + ":"
        + WorkCompletionIdentityOutcomeValidation.Fact(
            outcome,
            WorkCompletionIdentityOutcomeIds.WorkIdFact)
        + ":"
        + WorkCompletionIdentityOutcomeValidation.Fact(
            outcome,
            WorkCompletionIdentityOutcomeIds.ReactionFact));

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        GameplayOutcomeSubjectLink subject =
            WorkCompletionIdentityOutcomeValidation.TryFindSubject(
                outcome,
                subjectId,
                out GameplayOutcomeSubjectLink matched)
                ? matched
                : outcome.GetSubject(0);
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float salience = Math.Clamp(
            subject.Salience
            - Math.Min(0.2f, priorMatchingCount * 0.04f)
            - Math.Min(0.25f, age * 0.015f),
            0f,
            1f);
        return new OutcomeMemoryEvaluation(
            salience,
            NarrativeMemoryTier.Recent,
            Math.Max(evaluationDay + 2, outcome.AbsoluteDay + 2));
    }
}

internal sealed class WorkCompletionIdentityPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;

    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class WorkCompletionIdentityConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;

    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class WorkCompletionIdentityPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "work-identity-v1";
    private readonly IKoreanJosaFormatter josa;

    public WorkCompletionIdentityPerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant worker = outcome.GetParticipant(0);
        bool failed = string.Equals(
            WorkCompletionIdentityOutcomeValidation.Fact(
                outcome,
                WorkCompletionIdentityOutcomeIds.ReactionFact),
            "failed-work",
            StringComparison.Ordinal);
        string reaction = failed ? "작업 실패" : "작업 완료";
        KoreanJosaFormatResult topic = josa.Format(new KoreanJosaRequest(
            worker.DisplayName,
            KoreanJosaKind.Topic));
        bool neutral = topic.RequiresNeutralFrame;
        string text = neutral
            ? $"인물 {worker.DisplayName.DisplayText}: {reaction}에 따른 반응이 반영됐다."
            : $"{topic.Text} {reaction}에 따른 반응을 겪었다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            neutral);
    }
}

public sealed class WorkCompletionIdentityOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly IOutcomeMemoryPolicy Memory =
        new WorkCompletionIdentityMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new WorkCompletionIdentityPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new WorkCompletionIdentityConsolidator();
    private readonly INarrativePerspectiveProjector projector;

    public WorkCompletionIdentityOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new WorkCompletionIdentityPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        WorkCompletionIdentityOutcomeIds.Applied;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(WorkCompletionIdentityOutcomeIds.WorkerRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        (metricId.Equals(
                WorkCompletionIdentityOutcomeIds.OwnerRevisionMetric)
            && unitId.Equals(
                WorkCompletionIdentityOutcomeIds.RevisionUnit))
        || (metricId.Equals(
                WorkCompletionIdentityOutcomeIds.ContributionMetric)
            && unitId.Equals(
                WorkCompletionIdentityOutcomeIds.WorkUnit));

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome) =>
        WorkCompletionIdentityOutcomeValidation.TryValidateOutcome(
            outcome,
            out string failureReason)
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(failureReason);
}

public static class WorkCompletionIdentityOutcomeRegistration
{
    public static void RegisterWorkCompletionIdentityGameplayOutcomes(
        this IContainerBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        builder.Register<WorkIdentityAppliedFactMapping>(Lifetime.Singleton);
        builder.Register(resolver => new GameplayDomainFactObserverBinding<WorkIdentityAppliedFact, WorkCompletionIdentityOutcomeReceipt>(
            WorkIdentityAppliedFactMapping.ObserverId, resolver.Resolve<WorkIdentityAppliedFactMapping>().Capture,
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>()), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<WorkIdentityObservationPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>()
            .As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayActionLogs(nameof(WorkCompletionIdentityOutcomeAdapter));
    }
}
