using System;
using System.Linq;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class ServiceSessionOutcomeIds
{
    public static readonly GameplayOutcomeTypeId Terminal =
        new("service.session-terminal");
    public static readonly GameplayEntityKindId SessionKind =
        new("service-session");
    public static readonly GameplayEntityKindId CustomerKind =
        new("service-customer");
    public static readonly GameplayEntityKindId ProcessKind =
        new("service-process");
    public static readonly GameplayRoleId SessionRole = new("session");
    public static readonly GameplayRoleId FacilityRole = new("facility");
    public static readonly GameplayRoleId ActorRole = new("actor");
    public static readonly GameplayRoleId ProcessRole = new("process");
    public static readonly GameplayOutcomeTagId ServiceTag =
        new("domain:service");
    public static readonly GameplayMetricUnitId EnumUnit = new("enum");
    public static readonly GameplayMetricUnitId SecondUnit = new("second");
    public static readonly GameplayMetricUnitId GoldUnit = new("gold");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayMetricId ModeMetric = new("operation-mode");
    public static readonly GameplayMetricId StartedTimeMetric =
        new("started-game-time");
    public static readonly GameplayMetricId CompletedTimeMetric =
        new("completed-game-time");
    public static readonly GameplayMetricId DurationMetric = new("duration");
    public static readonly GameplayMetricId ContractedServiceSecondsMetric =
        new("contracted-service-seconds");
    public static readonly GameplayMetricId ProvidedServiceSecondsMetric =
        new("provided-service-seconds");
    public static readonly GameplayMetricId PriceMetric = new("price");
    public static readonly GameplayMetricId SatisfactionMetric =
        new("satisfaction");
    public static readonly GameplayMetricId PaymentRequiredMetric =
        new("payment-required");
    public static readonly GameplayMetricId PaymentCommittedMetric =
        new("payment-committed");
    public static readonly GameplayMetricId InternalActorMetric =
        new("internal-actor");
    public static readonly GameplayMetricId AdvertisedDemandMetric =
        new("advertised-demand");
    public static readonly GameplayMetricId PaymentAmountMetric =
        new("payment-amount");
    public static readonly GameplayMetricId BalanceBeforeMetric =
        new("balance-before");
    public static readonly GameplayMetricId BalanceAfterMetric =
        new("balance-after");
    public static readonly GameplayMetricId TerminalRevisionMetric =
        new("terminal-revision");
    public static readonly GameplayOutcomeFactId ReceiptKindFact =
        new("receipt-kind");
    public static readonly GameplayOutcomeFactId SessionIdFact =
        new("session-id");
    public static readonly GameplayOutcomeFactId ProcessIdFact =
        new("process-id");
    public static readonly GameplayOutcomeFactId CategoryFact =
        new("service-category");
    public static readonly GameplayOutcomeFactId SupportIdsFact =
        new("support-ids");
    public static readonly GameplayOutcomeFactId TransactionIdFact =
        new("economy-transaction-id");
    public static readonly GameplayOutcomeFactId TransactionSourceFact =
        new("economy-transaction-source-id");
    public static readonly GameplayOutcomeFactId CancellationReasonFact =
        new("cancellation-reason");
    public static readonly GameplayOutcomeFactId PreviousStageFact =
        new("previous-stage-name");
    public static readonly GameplayOutcomeFactId TerminalStageFact =
        new("terminal-stage-name");
    public static readonly GameplayOutcomeFactId ModeFact =
        new("operation-mode-name");
    public static readonly GameplayOutcomeFactId PaymentStateFact =
        new("payment-state");
    public static readonly GameplayOutcomeFactId SummaryFact = new("summary");
}

public static class ServiceSessionOutcomeKey
{
    public const string ProducerId = "service-room.service-session-terminal";
    public static string ResultKeyText(string sessionId) => Create(sessionId).ToString();
    public static GameplayResultKey Create(string sessionId) => new(
        ServiceSessionOutcomeKey.ProducerId,
        new GameplayOperationId(sessionId),
        ServiceSessionTerminalIdentity.OwnerRevision,
        0);
}

public readonly struct ServiceSessionOutcomeReceipt : IEnvironmentOutcomeReceipt
{
    public ServiceSessionOutcomeReceipt(EnvironmentOutcomePayload payload) =>
        Payload = payload;

    public EnvironmentOutcomePayload Payload { get; }
}

public static class ServiceSessionOutcomeReceiptFactory
{
    // Only retained cancelled cleanup owners need this detached ACK join. The
    // ledger still validates its own canonical digest and delivery state.
    public static bool MatchesRetainedCancellation(ServiceSessionSnapshot session, GameplayOutcomeSnapshot outcome)
    {
        if (session == null || session.Stage != ServiceSessionStage.Cancelled
            || session.TerminalPhase != ServiceSessionTerminalPhase.Acknowledged || outcome == null
            || outcome.producerId != ServiceSessionOutcomeKey.ProducerId
            || outcome.operationId != session.SessionId || outcome.commitRevision != session.TerminalOwnerRevision
            || outcome.localResultIndex != 0 || outcome.ownerRevision != session.TerminalOwnerRevision
            || outcome.outcomeTypeId != ServiceSessionOutcomeIds.Terminal.Value
            || outcome.status != GameplayOutcomeStatus.Cancelled || outcome.absoluteDay != session.TerminalAbsoluteDay
            || outcome.immutablePayloadHash != session.TerminalPublicationFingerprint
            || new GameplayOutcomeId(new GameplayOutcomeRunId(outcome.runId), outcome.sequence).ToString() != session.TerminalPublicationId
            || session.TerminalPublicationKey != ServiceSessionOutcomeKey.Create(session.SessionId).ToString()
            || outcome.participants == null || outcome.metrics == null || outcome.facts == null)
            return false;
        EnvironmentOutcomePayload expected = Create(session, session.TerminalHubName).Payload;
        if (outcome.participants.Count != expected.Participants.Count
            || outcome.metrics.Count != expected.Metrics.Count || outcome.facts.Count != expected.Facts.Count)
            return false;
        for (int index = 0; index < expected.Participants.Count; index++)
        {
            GameplayOutcomeParticipant participant = expected.Participants.Get(index);
            if (outcome.participants.Count(value => value != null
                && value.entityKindId == participant.EntityId.Kind.Value && value.entityId == participant.EntityId.Value
                && value.roleId == participant.RoleId.Value && value.participationKind == participant.ParticipationKind
                && value.hasPerceptionEvidence == participant.HasPerceptionEvidence) != 1) return false;
        }
        for (int index = 0; index < expected.Metrics.Count; index++)
        {
            GameplayOutcomeMetric metric = expected.Metrics.Get(index);
            if (outcome.metrics.Count(value => value != null && value.metricId == metric.MetricId.Value
                && value.value == metric.Value && value.unitId == metric.UnitId.Value
                && value.referenceKindId == metric.DefinitionOrInstanceId.Kind.Value
                && value.referenceId == metric.DefinitionOrInstanceId.Value) != 1) return false;
        }
        for (int index = 0; index < expected.Facts.Count; index++)
        {
            GameplayOutcomeFact fact = expected.Facts.Get(index);
            if (outcome.facts.Count(value => value != null && value.factId == fact.FactId.Value && value.value == fact.Value) != 1)
                return false;
        }
        return true;
    }

    public const int ParticipantCount = 4;
    public const int MetricCount = 16;
    public const int SubjectCount = 4;
    public const int TagCount = 1;
    public const int ProvenanceCount = 1;
    public const int FactCount = 13;

    public static EnvironmentOutcomeReservationSpec CreateReservationSpec(
        ServiceSessionSnapshot source,
        ServiceSessionStage terminalStage,
        int absoluteDay)
    {
        RequireSource(source, terminalStage, requireTerminal: false);
        return new EnvironmentOutcomeReservationSpec(
            ServiceSessionOutcomeKey.Create(source.SessionId),
            ServiceSessionOutcomeIds.Terminal,
            absoluteDay,
            terminalStage == ServiceSessionStage.Completed
                ? GameplayOutcomeStatus.Succeeded
                : GameplayOutcomeStatus.Cancelled,
            ServiceSessionTerminalIdentity.OwnerRevision,
            ParticipantCount,
            MetricCount,
            SubjectCount,
            TagCount,
            ProvenanceCount,
            FactCount);
    }

    public static ServiceSessionOutcomeReceipt Create(
        ServiceSessionSnapshot source,
        string hubDisplayName)
    {
        RequireSource(source, source?.Stage ?? default, requireTerminal: true);
        string hubName = string.IsNullOrWhiteSpace(hubDisplayName)
            ? source.HubId
            : hubDisplayName.Trim();
        string actorId = string.IsNullOrEmpty(source.ActorId)
            ? "anonymous:" + source.SessionId
            : source.ActorId;
        string actorName = string.IsNullOrEmpty(source.ActorId)
            ? "익명 손님"
            : source.ActorId;
        GameplayOutcomeStatus status = source.Stage
            == ServiceSessionStage.Completed
                ? GameplayOutcomeStatus.Succeeded
                : GameplayOutcomeStatus.Cancelled;
        var session = new GameplayEntityId(
            ServiceSessionOutcomeIds.SessionKind,
            source.SessionId);
        var facility = new GameplayEntityId(
            EnvironmentOutcomeIds.FacilityKind,
            source.HubId);
        var actor = new GameplayEntityId(
            string.IsNullOrEmpty(source.ActorId)
                ? ServiceSessionOutcomeIds.CustomerKind
                : EnvironmentOutcomeIds.CharacterKind,
            actorId);
        var process = new GameplayEntityId(
            ServiceSessionOutcomeIds.ProcessKind,
            source.ProcessId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            ServiceSessionOutcomeKey.Create(source.SessionId),
            ServiceSessionOutcomeIds.Terminal,
            source.TerminalAbsoluteDay,
            status,
            source.TerminalOwnerRevision);
        AddParticipant(ref builder, session,
            ServiceSessionOutcomeIds.SessionRole, source.SessionId,
            source.ProcessId + " 세션");
        AddParticipant(ref builder, facility,
            ServiceSessionOutcomeIds.FacilityRole, source.HubId, hubName);
        AddParticipant(ref builder, actor,
            ServiceSessionOutcomeIds.ActorRole, actorId, actorName);
        AddParticipant(ref builder, process,
            ServiceSessionOutcomeIds.ProcessRole, source.ProcessId,
            source.ProcessId);
        AddSubject(ref builder, session, 0.72f, NarrativeMemoryTier.Recent);
        AddSubject(ref builder, facility, 0.58f, NarrativeMemoryTier.Recent);
        AddSubject(ref builder, actor,
            source.Stage == ServiceSessionStage.Completed ? 0.7f : 0.76f,
            NarrativeMemoryTier.Episodic);
        AddSubject(ref builder, process, 0.42f, NarrativeMemoryTier.Recent);
        AddMetric(ref builder, ServiceSessionOutcomeIds.ModeMetric,
            (int)source.Contract.mode, ServiceSessionOutcomeIds.EnumUnit,
            facility);
        AddMetric(ref builder, ServiceSessionOutcomeIds.StartedTimeMetric,
            source.StartedAt, ServiceSessionOutcomeIds.SecondUnit, session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.CompletedTimeMetric,
            source.StageStartedAt, ServiceSessionOutcomeIds.SecondUnit, session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.DurationMetric,
            source.StageStartedAt - source.StartedAt,
            ServiceSessionOutcomeIds.SecondUnit, session);
        AddMetric(ref builder,
            ServiceSessionOutcomeIds.ContractedServiceSecondsMetric,
            source.Contract.serviceSeconds,
            ServiceSessionOutcomeIds.SecondUnit,
            session);
        AddMetric(ref builder,
            ServiceSessionOutcomeIds.ProvidedServiceSecondsMetric,
            source.ProvidedServiceSeconds,
            ServiceSessionOutcomeIds.SecondUnit,
            session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.PriceMetric,
            source.Contract.price, ServiceSessionOutcomeIds.GoldUnit, session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.SatisfactionMetric,
            source.Contract.satisfaction, ServiceSessionOutcomeIds.PointUnit,
            actor);
        AddMetric(ref builder, ServiceSessionOutcomeIds.PaymentRequiredMetric,
            source.Contract.paymentRequired ? 1d : 0d,
            ServiceSessionOutcomeIds.EnumUnit, session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.PaymentCommittedMetric,
            source.PaymentCommitted ? 1d : 0d,
            ServiceSessionOutcomeIds.EnumUnit, session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.InternalActorMetric,
            source.Contract.internalActor ? 1d : 0d,
            ServiceSessionOutcomeIds.EnumUnit, actor);
        AddMetric(ref builder, ServiceSessionOutcomeIds.AdvertisedDemandMetric,
            source.AdvertisedDemand ? 1d : 0d,
            ServiceSessionOutcomeIds.EnumUnit, session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.PaymentAmountMetric,
            source.PaymentAmount, ServiceSessionOutcomeIds.GoldUnit, session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.BalanceBeforeMetric,
            source.BalanceBefore, ServiceSessionOutcomeIds.GoldUnit, session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.BalanceAfterMetric,
            source.BalanceAfter, ServiceSessionOutcomeIds.GoldUnit, session);
        AddMetric(ref builder, ServiceSessionOutcomeIds.TerminalRevisionMetric,
            source.TerminalOwnerRevision,
            ServiceSessionOutcomeIds.RevisionUnit, session);
        Require(builder.AddTag(ServiceSessionOutcomeIds.ServiceTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            ServiceSessionOutcomeKey.ProducerId,
            source.SessionId)));
        AddFact(ref builder, ServiceSessionOutcomeIds.ReceiptKindFact,
            nameof(ServiceSessionOutcomeReceipt));
        AddFact(ref builder, ServiceSessionOutcomeIds.SessionIdFact,
            source.SessionId);
        AddFact(ref builder, ServiceSessionOutcomeIds.ProcessIdFact,
            source.ProcessId);
        AddFact(ref builder, ServiceSessionOutcomeIds.CategoryFact,
            source.Category.ToString());
        AddFact(ref builder, ServiceSessionOutcomeIds.SupportIdsFact,
            source.Contract.supportIds.Length == 0
                ? "none"
                : string.Join(",", source.Contract.supportIds
                    .OrderBy(value => value, StringComparer.Ordinal)));
        AddFact(ref builder, ServiceSessionOutcomeIds.TransactionIdFact,
            EmptyAsNone(source.EconomyTransactionId));
        AddFact(ref builder, ServiceSessionOutcomeIds.TransactionSourceFact,
            EmptyAsNone(source.EconomyTransactionSourceId));
        AddFact(ref builder, ServiceSessionOutcomeIds.CancellationReasonFact,
            EmptyAsNone(source.CancellationReason));
        AddFact(ref builder, ServiceSessionOutcomeIds.PreviousStageFact,
            source.PreviousStage.ToString());
        AddFact(ref builder, ServiceSessionOutcomeIds.TerminalStageFact,
            source.Stage.ToString());
        AddFact(ref builder, ServiceSessionOutcomeIds.ModeFact,
            source.Contract.mode.ToString());
        AddFact(ref builder, ServiceSessionOutcomeIds.PaymentStateFact,
            source.PaymentCommitted ? "committed" : "not-required");
        AddFact(ref builder, ServiceSessionOutcomeIds.SummaryFact,
            source.Stage == ServiceSessionStage.Completed
                ? actorName + "의 " + source.ProcessId + " 서비스가 완료됐다."
                : actorName + "의 " + source.ProcessId
                    + " 서비스가 취소됐다: " + source.CancellationReason);
        return new ServiceSessionOutcomeReceipt(builder.Build());
    }

    private static void RequireSource(
        ServiceSessionSnapshot source,
        ServiceSessionStage terminalStage,
        bool requireTerminal)
    {
        if (source == null
            || string.IsNullOrWhiteSpace(source.SessionId)
            || string.IsNullOrWhiteSpace(source.HubId)
            || string.IsNullOrWhiteSpace(source.ProcessId)
            || source.Contract == null
            || source.Contract.supportIds == null
            || !float.IsFinite(source.StartedAt)
            || source.StartedAt < 0f
            || requireTerminal
            && (!float.IsFinite(source.ProvidedServiceSeconds)
                || source.ProvidedServiceSeconds < 0f
                || source.ProvidedServiceSeconds
                    > source.Contract.serviceSeconds + 0.001f)
            || terminalStage is not (ServiceSessionStage.Completed
                or ServiceSessionStage.Cancelled)
            || requireTerminal
            && (source.Stage != terminalStage
                || !float.IsFinite(source.StageStartedAt)
                || source.StageStartedAt < source.StartedAt
                || source.TerminalAbsoluteDay < 0
                || source.TerminalOwnerRevision
                    != ServiceSessionTerminalIdentity.OwnerRevision))
        {
            throw new ArgumentException(
                "A canonical terminal service-session snapshot is required.",
                nameof(source));
        }
    }

    private static void AddParticipant(
        ref EnvironmentOutcomePayloadBuilder builder,
        GameplayEntityId entity,
        GameplayRoleId role,
        string stableId,
        string displayName) => Require(builder.AddParticipant(
        new GameplayOutcomeParticipant(
            entity,
            role,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(stableId, displayName))));

    private static void AddSubject(
        ref EnvironmentOutcomePayloadBuilder builder,
        GameplayEntityId entity,
        float salience,
        NarrativeMemoryTier tier) => Require(builder.AddSubject(
        new GameplayOutcomeSubjectLink(
            entity,
            salience,
            tier,
            false,
            false,
            0)));

    private static void AddMetric(
        ref EnvironmentOutcomePayloadBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId reference) => Require(builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, reference)));

    private static void AddFact(
        ref EnvironmentOutcomePayloadBuilder builder,
        GameplayOutcomeFactId id,
        string value) => Require(builder.AddFact(
        new GameplayOutcomeFact(id, value)));

    private static string EmptyAsNone(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();

    private static void Require(bool accepted)
    {
        if (!accepted)
        {
            throw new InvalidOperationException(
                "Service-session outcome fixed buffer capacity was exceeded.");
        }
    }
}

public sealed class ServiceSessionOutcomeAdapter :
    EnvironmentOutcomeAdapter<ServiceSessionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        ServiceSessionOutcomeIds.Terminal;

    protected override string FailurePrefix => "service-session-terminal";
}

public sealed class ServiceSessionOutcomeDescriptor : EnvironmentOutcomeDescriptor
{
    public ServiceSessionOutcomeDescriptor(IKoreanJosaFormatter josa) : base(
        josa,
        "서비스 세션",
        ServiceSessionOutcomeIds.ActorRole,
        KoreanJosaKind.Topic,
        " 서비스를 마쳤다.",
        "서비스를 마쳤다.",
        GameplayOutcomeStatus.Succeeded,
        GameplayOutcomeStatus.Cancelled,
        ServiceSessionOutcomeIds.ServiceTag,
        new[]
        {
            ServiceSessionOutcomeIds.SessionRole,
            ServiceSessionOutcomeIds.FacilityRole,
            ServiceSessionOutcomeIds.ActorRole,
            ServiceSessionOutcomeIds.ProcessRole
        },
        new[]
        {
            Metric(ServiceSessionOutcomeIds.ModeMetric,
                ServiceSessionOutcomeIds.EnumUnit, 0d, 2d, true),
            Metric(ServiceSessionOutcomeIds.StartedTimeMetric,
                ServiceSessionOutcomeIds.SecondUnit, 0d),
            Metric(ServiceSessionOutcomeIds.CompletedTimeMetric,
                ServiceSessionOutcomeIds.SecondUnit, 0d),
            Metric(ServiceSessionOutcomeIds.DurationMetric,
                ServiceSessionOutcomeIds.SecondUnit, 0d),
            Metric(ServiceSessionOutcomeIds.ContractedServiceSecondsMetric,
                ServiceSessionOutcomeIds.SecondUnit, 0d),
            Metric(ServiceSessionOutcomeIds.ProvidedServiceSecondsMetric,
                ServiceSessionOutcomeIds.SecondUnit, 0d),
            Metric(ServiceSessionOutcomeIds.PriceMetric,
                ServiceSessionOutcomeIds.GoldUnit, 0d, int.MaxValue, true),
            Metric(ServiceSessionOutcomeIds.SatisfactionMetric,
                ServiceSessionOutcomeIds.PointUnit),
            Metric(ServiceSessionOutcomeIds.PaymentRequiredMetric,
                ServiceSessionOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(ServiceSessionOutcomeIds.PaymentCommittedMetric,
                ServiceSessionOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(ServiceSessionOutcomeIds.InternalActorMetric,
                ServiceSessionOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(ServiceSessionOutcomeIds.AdvertisedDemandMetric,
                ServiceSessionOutcomeIds.EnumUnit, 0d, 1d, true),
            Metric(ServiceSessionOutcomeIds.PaymentAmountMetric,
                ServiceSessionOutcomeIds.GoldUnit, 0d, int.MaxValue, true),
            Metric(ServiceSessionOutcomeIds.BalanceBeforeMetric,
                ServiceSessionOutcomeIds.GoldUnit, 0d, int.MaxValue, true),
            Metric(ServiceSessionOutcomeIds.BalanceAfterMetric,
                ServiceSessionOutcomeIds.GoldUnit, 0d, int.MaxValue, true),
            Metric(ServiceSessionOutcomeIds.TerminalRevisionMetric,
                ServiceSessionOutcomeIds.RevisionUnit, 1d, 1d, true)
        },
        new[]
        {
            ServiceSessionOutcomeIds.ReceiptKindFact,
            ServiceSessionOutcomeIds.SessionIdFact,
            ServiceSessionOutcomeIds.ProcessIdFact,
            ServiceSessionOutcomeIds.CategoryFact,
            ServiceSessionOutcomeIds.SupportIdsFact,
            ServiceSessionOutcomeIds.TransactionIdFact,
            ServiceSessionOutcomeIds.TransactionSourceFact,
            ServiceSessionOutcomeIds.CancellationReasonFact,
            ServiceSessionOutcomeIds.PreviousStageFact,
            ServiceSessionOutcomeIds.TerminalStageFact,
            ServiceSessionOutcomeIds.ModeFact,
            ServiceSessionOutcomeIds.PaymentStateFact,
            ServiceSessionOutcomeIds.SummaryFact
        })
    {
    }

    public override GameplayOutcomeTypeId OutcomeTypeId =>
        ServiceSessionOutcomeIds.Terminal;

    protected override OutcomeValidationResult ValidateAdditional(
        in GameplayOutcomeReadView outcome)
    {
        double started = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.StartedTimeMetric);
        double completed = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.CompletedTimeMetric);
        double duration = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.DurationMetric);
        double contractedServiceSeconds = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.ContractedServiceSecondsMetric);
        double providedServiceSeconds = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.ProvidedServiceSecondsMetric);
        double price = FindMetric(outcome, ServiceSessionOutcomeIds.PriceMetric);
        double required = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.PaymentRequiredMetric);
        double committed = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.PaymentCommittedMetric);
        double amount = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.PaymentAmountMetric);
        double before = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.BalanceBeforeMetric);
        double after = FindMetric(
            outcome,
            ServiceSessionOutcomeIds.BalanceAfterMetric);
        bool timeMatches = completed >= started
            && Math.Abs(duration - (completed - started)) <= 0.001d;
        bool provisionMatches = contractedServiceSeconds > 0d
            && providedServiceSeconds >= 0d
            && providedServiceSeconds <= contractedServiceSeconds + 0.001d
            && (outcome.Status != GameplayOutcomeStatus.Succeeded
                || Math.Abs(
                    providedServiceSeconds - contractedServiceSeconds)
                    <= 0.001d);
        bool paymentMatches = committed == 1d
            ? required == 1d
                && price > 0d
                && amount == price
                && after == before + amount
            : amount == 0d && before == 0d && after == 0d;
        return timeMatches && provisionMatches
            && paymentMatches
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                OutcomeTypeId.Value + "-terminal-semantics-invalid");
    }
}

public static class ServiceSessionOutcomeRegistration
{
    public static void RegisterServiceSessionGameplayOutcomeBridge(
        this IContainerBuilder builder)
    {
        builder.Register(resolver => new GameplayDomainFactObserverBinding<ServiceSessionTerminalFact, ServiceSessionOutcomeReceipt>(
            "log:service.session-terminal", ServiceSessionTerminalFactMapping.Capture,
            resolver.Resolve<IGameplayOutcomeRegistry>(), resolver.Resolve<GameplayOutcomeLedger>(),
            resolver.Resolve<IGameplayOutcomeRecorder>(), resolver.Resolve<GameplayDomainFactCommitGroup>(),
            ServiceSessionTerminalFactMapping.Reserve), Lifetime.Singleton)
            .As<DungeonStory.Foundation.Facts.IDomainFactObserverBinding>();
        builder.Register<ServiceSessionTerminalFactPreflight>(Lifetime.Singleton)
            .As<IDungeonSavePreflightValidator>().As<IDungeonCapturedSavePreflightValidator>();
        builder.RegisterGameplayActionLogs(nameof(ServiceSessionOutcomeAdapter));
    }
}
