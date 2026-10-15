using System;
using System.Linq;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class SurgeryOutcomeIds
{
    public static readonly GameplayOutcomeTypeId Resolved =
        new("medical.surgery-resolved");
    public static readonly GameplayEntityKindId Subject =
        new("surgical-subject");
    public static readonly GameplayRoleId Patient = new("patient");
    public static readonly GameplayOutcomeTagId Surgery = new("surgery");
    public static readonly GameplayOutcomeFactId Order = new("surgery.order-id");
    public static readonly GameplayOutcomeFactId Procedure = new("surgery.procedure");
    public static readonly GameplayOutcomeFactId Result = new("surgery.result");
    public static readonly GameplayOutcomeFactId Severity = new("surgery.severity");
    public static readonly GameplayOutcomeFactId Materials = new("surgery.materials");
    public static readonly GameplayOutcomeFactId AnatomyBefore = new("surgery.anatomy-before");
    public static readonly GameplayOutcomeFactId AnatomyAfter = new("surgery.anatomy-after");
    public static readonly GameplayOutcomeFactId ChildReferences = new("surgery.child-references");
    public static readonly GameplayOutcomeFactId ContextSha256 =
        new("surgery.context-sha256");

    public static GameplayResultKey ResultKey(string orderId) => new(
        "medical.surgery",
        new GameplayOperationId("surgery-terminal:" + orderId),
        1,
        0);
}

public readonly struct SurgeryOutcomeReceipt
{
    public SurgeryOutcomeReceipt(
        GameplayResultKey resultKey,
        int absoluteDay,
        string orderId,
        string subjectId,
        string subjectName,
        SurgicalSubjectKind subjectKind,
        string procedureId,
        SurgicalProcedureKind procedureKind,
        string doctorId,
        string targetNodeId,
        string resultId,
        SurgeryFailureSeverity severity,
        bool succeeded,
        string materials,
        string anatomyBefore,
        string anatomyAfter,
        string childReferences,
        string contextSha256)
    {
        ResultKey = resultKey;
        AbsoluteDay = absoluteDay;
        OrderId = orderId ?? string.Empty;
        SubjectId = subjectId ?? string.Empty;
        SubjectName = subjectName ?? string.Empty;
        SubjectKind = subjectKind;
        ProcedureId = procedureId ?? string.Empty;
        ProcedureKind = procedureKind;
        DoctorId = doctorId ?? string.Empty;
        TargetNodeId = targetNodeId ?? string.Empty;
        ResultId = resultId ?? string.Empty;
        Severity = severity;
        Succeeded = succeeded;
        Materials = materials ?? string.Empty;
        AnatomyBefore = anatomyBefore ?? string.Empty;
        AnatomyAfter = anatomyAfter ?? string.Empty;
        ChildReferences = childReferences ?? string.Empty;
        ContextSha256 = contextSha256 ?? string.Empty;
    }

    public GameplayResultKey ResultKey { get; }
    public int AbsoluteDay { get; }
    public string OrderId { get; }
    public string SubjectId { get; }
    public string SubjectName { get; }
    public SurgicalSubjectKind SubjectKind { get; }
    public string ProcedureId { get; }
    public SurgicalProcedureKind ProcedureKind { get; }
    public string DoctorId { get; }
    public string TargetNodeId { get; }
    public string ResultId { get; }
    public SurgeryFailureSeverity Severity { get; }
    public bool Succeeded { get; }
    public string Materials { get; }
    public string AnatomyBefore { get; }
    public string AnatomyAfter { get; }
    public string ChildReferences { get; }
    public string ContextSha256 { get; }
}

public sealed class SurgeryOutcomeAdapter :
    GameplayOutcomeAdapter<SurgeryOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId => SurgeryOutcomeIds.Resolved;

    public override OutcomePrepareResult TryGetRequirements(
        in SurgeryOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid
            || receipt.AbsoluteDay <= 0
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.OrderId)
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.SubjectId)
            || !GameplayOutcomeStableIdSyntax.IsValid(receipt.ProcedureId)
            || string.IsNullOrWhiteSpace(receipt.SubjectName)
            || !Enum.IsDefined(typeof(SurgicalSubjectKind), receipt.SubjectKind)
            || !Enum.IsDefined(typeof(SurgicalProcedureKind), receipt.ProcedureKind)
            || !Enum.IsDefined(typeof(SurgeryFailureSeverity), receipt.Severity)
            || string.IsNullOrWhiteSpace(receipt.ResultId)
            || string.IsNullOrWhiteSpace(receipt.AnatomyBefore)
            || string.IsNullOrWhiteSpace(receipt.AnatomyAfter)
            || receipt.Materials.Length > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length
            || receipt.AnatomyBefore.Length > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length
            || receipt.AnatomyAfter.Length > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length
            || receipt.ChildReferences.Length > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length
            || !IsContextSha256(receipt.ContextSha256))
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "surgery-receipt-invalid");
        requirements = Requirements(
            receipt.ResultKey,
            receipt.AbsoluteDay,
            currentWorldEpoch,
            receipt.Succeeded);
        return OutcomePrepareResult.Prepared();
    }

    public static OutcomeWriteRequirements Requirements(
        GameplayResultKey key,
        int day,
        long epoch,
        bool succeeded) => new(
        key,
        SurgeryOutcomeIds.Resolved,
        day,
        succeeded ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.Failed,
        epoch,
        key.CommitRevision,
        participantCount: 1,
        metricCount: 0,
        subjectCount: 1,
        tagCount: 1,
        anchorCount: 0,
        provenanceCount: 1,
        factCount: 9);

    public override OutcomePrepareResult TryWrite(
        in SurgeryOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId patient = new(
            SurgeryOutcomeIds.Subject,
            receipt.SubjectId);
        bool valid = builder.AddParticipant(new GameplayOutcomeParticipant(
                patient,
                SurgeryOutcomeIds.Patient,
                GameplayParticipationKind.Direct,
                true,
                MigratedProducerOutcomeSnapshots.Name(
                    receipt.SubjectId,
                    receipt.SubjectName)))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                patient, receipt.Succeeded ? 0.65f : 0.85f,
                NarrativeMemoryTier.Recent, false, false, 0))
            && builder.AddTag(SurgeryOutcomeIds.Surgery)
            && builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "runtime-receipt", "surgery-v1"))
            && builder.AddFact(new GameplayOutcomeFact(
                SurgeryOutcomeIds.Order, receipt.OrderId))
            && builder.AddFact(new GameplayOutcomeFact(
                SurgeryOutcomeIds.Procedure,
                receipt.ProcedureId + "|" + receipt.ProcedureKind
                + "|subject-kind=" + receipt.SubjectKind
                + "|doctor=" + Value(receipt.DoctorId)
                + "|target=" + Value(receipt.TargetNodeId)))
            && builder.AddFact(new GameplayOutcomeFact(
                SurgeryOutcomeIds.Result, receipt.ResultId))
            && builder.AddFact(new GameplayOutcomeFact(
                SurgeryOutcomeIds.Severity, receipt.Severity.ToString()))
            && builder.AddFact(new GameplayOutcomeFact(
                SurgeryOutcomeIds.Materials, Value(receipt.Materials)))
            && builder.AddFact(new GameplayOutcomeFact(
                SurgeryOutcomeIds.AnatomyBefore, receipt.AnatomyBefore))
            && builder.AddFact(new GameplayOutcomeFact(
                SurgeryOutcomeIds.AnatomyAfter, receipt.AnatomyAfter))
            && builder.AddFact(new GameplayOutcomeFact(
                SurgeryOutcomeIds.ChildReferences,
                Value(receipt.ChildReferences)))
            && builder.AddFact(new GameplayOutcomeFact(
                SurgeryOutcomeIds.ContextSha256,
                receipt.ContextSha256));
        return valid
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "surgery-receipt-write-failed");
    }

    private static string Value(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value;

    private static bool IsContextSha256(string value) =>
        value != null
        && value.Length == 71
        && value.StartsWith("sha256:", StringComparison.Ordinal)
        && value.Substring("sha256:".Length)
            .All(character => Uri.IsHexDigit(character));
}

public sealed class SurgeryOutcomeDescriptor : IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new MigratedProducerOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new MigratedProducerOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new MigratedProducerOutcomeConsolidator();

    public SurgeryOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new SurgeryOutcomeProjector(josa);
    public GameplayOutcomeTypeId OutcomeTypeId => SurgeryOutcomeIds.Resolved;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId role) =>
        role.Equals(SurgeryOutcomeIds.Patient);
    public bool IsKnownMetric(GameplayMetricId id, GameplayMetricUnitId unit) => false;
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 1
        && outcome.MetricCount == 0
        && outcome.SubjectCount == 1
        && outcome.TagCount == 1
        && outcome.ProvenanceCount == 1
        && outcome.FactCount == 9
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject("surgery-outcome-shape-invalid");
}

internal sealed class SurgeryOutcomeProjector : INarrativePerspectiveProjector
{
    private readonly IKoreanJosaFormatter josa;
    public SurgeryOutcomeProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant patient = outcome.GetParticipant(0);
        KoreanJosaFormatResult subject = josa.Format(new KoreanJosaRequest(
            patient.DisplayName, KoreanJosaKind.Topic));
        bool succeeded = outcome.Status == GameplayOutcomeStatus.Succeeded;
        bool fatal = false;
        for (int index = 0; index < outcome.FactCount; index++)
        {
            GameplayOutcomeFact fact = outcome.GetFact(index);
            if (fact.FactId.Equals(SurgeryOutcomeIds.Severity))
            {
                fatal = fact.Value == SurgeryFailureSeverity.Fatal.ToString();
                break;
            }
        }
        string action = fatal
            ? "수술 중 사망했다."
            : succeeded ? "수술을 마쳤다." : "수술 중 합병증을 겪었다.";
        bool neutral = subject.RequiresNeutralFrame;
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            neutral
                ? patient.DisplayName.DisplayText + ": " + action
                : subject.Text + " " + action,
            "surgery-v1",
            neutral);
    }
}

public static class SurgeryOutcomeRegistration
{
    public static void RegisterSurgeryGameplayOutcomes(this IContainerBuilder builder)
    {
        builder.RegisterGameplayActionLogs(nameof(SurgeryOutcomeAdapter));
        builder.Register(resolver => SurgeryResolvedFactMapping.CreateBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.RegisterWorkCompletionDomainFact<SurgeryWorkCompletedFact>(
            SurgeryWorkCompletionFactMapping.ObserverId,
            SurgeryWorkCompletionFactMapping.Capture,
            WorkCompletionNotificationMode.ReferencedEvent);
    }
}
