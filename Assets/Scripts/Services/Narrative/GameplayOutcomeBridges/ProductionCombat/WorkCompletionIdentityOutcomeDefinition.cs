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
    public static readonly GameplayRoleId WorkerRole = new("worker");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("work-identity.owner-revision");
    public static readonly GameplayMetricUnitId RevisionUnit =
        new("revision");
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
            metricCount: 1,
            subjectCount: 1,
            tagCount: 1,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 5);
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
            || !builder.AddMetric(new GameplayOutcomeMetric(
                WorkCompletionIdentityOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                WorkCompletionIdentityOutcomeIds.RevisionUnit,
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
                receipt.Request.ProducerStreamId)))
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
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public WorkCompletionIdentityGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics,
        IGameplayOutcomeDisplayNameQuery displayNames)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
        this.displayNames = displayNames
            ?? throw new ArgumentNullException(nameof(displayNames));
    }

    public bool TryPrepare(
        WorkCompletionIdentityDeliveryRequest request,
        out PreparedWorkCompletionIdentityOutcome prepared,
        out bool capacityDeferred,
        out string failureReason)
    {
        prepared = default;
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
            && request.PayloadFingerprint?.Length == 64
            && !string.IsNullOrWhiteSpace(
                receipt.CharacterDisplayName.DisplayText)
            && !string.IsNullOrWhiteSpace(
                receipt.CharacterDisplayName.DisplaySnapshotRevision);
    }

    public static bool IsValidOutcome(in GameplayOutcomeReadView outcome)
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
            || outcome.SubjectCount != 1
            || outcome.MetricCount != 1
            || outcome.TagCount != 1
            || outcome.AnchorCount != 0
            || outcome.ProvenanceCount != 2
            || outcome.FactCount != 5)
        {
            return false;
        }

        GameplayOutcomeParticipant participant = outcome.GetParticipant(0);
        GameplayOutcomeSubjectLink subject = outcome.GetSubject(0);
        GameplayOutcomeMetric metric = outcome.GetMetric(0);
        return participant.RoleId.Equals(
                WorkCompletionIdentityOutcomeIds.WorkerRole)
            && participant.EntityId.Kind.Equals(
                WorkCompletionIdentityOutcomeIds.CharacterKind)
            && subject.SubjectId.Equals(participant.EntityId)
            && metric.MetricId.Equals(
                WorkCompletionIdentityOutcomeIds.OwnerRevisionMetric)
            && metric.UnitId.Equals(
                WorkCompletionIdentityOutcomeIds.RevisionUnit)
            && metric.DefinitionOrInstanceId.Equals(participant.EntityId)
            && Math.Abs(metric.Value - outcome.OwnerRevision) < 0.00001d
            && outcome.GetTag(0).Equals(
                WorkCompletionIdentityOutcomeIds.WorkIdentityTag)
            && HasFact(outcome, WorkCompletionIdentityOutcomeIds.WorkIdFact)
            && HasFact(outcome, WorkCompletionIdentityOutcomeIds.ProductIdFact)
            && HasFact(outcome, WorkCompletionIdentityOutcomeIds.OriginFact)
            && HasFact(outcome, WorkCompletionIdentityOutcomeIds.ReactionFact)
            && HasFact(
                outcome,
                WorkCompletionIdentityOutcomeIds.ProducerStreamFact);
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
        GameplayOutcomeSubjectLink subject = outcome.GetSubject(0);
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
        metricId.Equals(
            WorkCompletionIdentityOutcomeIds.OwnerRevisionMetric)
        && unitId.Equals(WorkCompletionIdentityOutcomeIds.RevisionUnit);

    public OutcomeValidationResult Validate(
        in GameplayOutcomeReadView outcome) =>
        WorkCompletionIdentityOutcomeValidation.IsValidOutcome(outcome)
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "work-identity-shape-invalid");
}

public static class WorkCompletionIdentityOutcomeRegistration
{
    public static void RegisterWorkCompletionIdentityGameplayOutcomes(
        this IContainerBuilder builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));
        builder.Register<WorkCompletionIdentityGameplayOutcomeBridge>(
            Lifetime.Singleton);
        builder.Register<WorkCompletionIdentityOutcomeAdapter>(
                Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<WorkCompletionIdentityOutcomeDescriptor>(
                Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}
