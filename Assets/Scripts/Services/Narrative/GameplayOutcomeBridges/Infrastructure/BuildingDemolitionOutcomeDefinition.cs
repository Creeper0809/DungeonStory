using System;
using System.Collections.Generic;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class BuildingDemolitionOutcomeIds
{
    public const string ProducerId = "infrastructure.building-demolition";

    public static readonly GameplayOutcomeTypeId Demolished =
        new("infrastructure.building-demolished");
    public static readonly GameplayEntityKindId FacilityKind = new("facility");
    public static readonly GameplayRoleId DemolishedFacilityRole =
        new("demolished-facility");
    public static readonly GameplayMetricId OwnerRevisionMetric =
        new("building-demolition.owner-revision");
    public static readonly GameplayMetricUnitId RevisionUnit = new("revision");
    public static readonly GameplayOutcomeTagId InfrastructureTag =
        new("domain:infrastructure");
    public static readonly GameplayOutcomeTagId DemolitionTag =
        new("building-demolition");
    public static readonly GameplayOutcomeFactId DefinitionIdFact =
        new("building-definition-id");
    public static readonly GameplayOutcomeFactId DrainOperationFact =
        new("destructive-drain-operation-id");

    public static GameplayResultKey ResultKey(
        ProductionFacilityDestructiveDrainOperationId operationId,
        long ownerRevision) => new(
        ProducerId,
        new GameplayOperationId(operationId.Value),
        ownerRevision,
        0);
}

public static class BuildingDemolitionOutcomeNames
{
    public static KoreanNameSnapshot Snapshot(
        string facilityId,
        string displayText)
    {
        string identity = GameplayOutcomeStableIdSyntax.Require(
            facilityId,
            nameof(facilityId));
        string display = displayText?.Trim() ?? string.Empty;
        if (display.Length == 0)
        {
            throw new ArgumentException(
                "A building-demolition display snapshot is required.",
                nameof(displayText));
        }
        string revision = "building-demolition-name-v1:"
            + NarrativeInferenceHash.ComputeSha256Utf8(
                identity + "|" + display);
        return new KoreanNameSnapshot(
            display,
            revision,
            KoreanPronunciationHint.AutoHangulDisplay(revision),
            "ko-KR");
    }
}

public readonly struct BuildingDemolitionOutcomeReceipt
{
    public BuildingDemolitionOutcomeReceipt(
        ProductionFacilityDestructiveDrainOperationId operationId,
        BuildingInstanceId facilityId,
        string buildingDefinitionId,
        KoreanNameSnapshot facilityDisplayName,
        int positionX,
        int positionY,
        int absoluteDay,
        long ownerRevision)
    {
        if (!operationId.IsValid
            || !facilityId.IsValid
            || !string.Equals(
                operationId.Value,
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    facilityId).Value,
                StringComparison.Ordinal)
            || !ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                buildingDefinitionId)
            || string.IsNullOrWhiteSpace(facilityDisplayName.DisplayText)
            || string.IsNullOrWhiteSpace(
                facilityDisplayName.DisplaySnapshotRevision)
            || string.IsNullOrWhiteSpace(
                facilityDisplayName.PronunciationHint.Revision)
            || absoluteDay < 1
            || ownerRevision <= 0L)
        {
            throw new ArgumentException(
                "A canonical building-demolition receipt is required.");
        }
        OperationId = operationId;
        FacilityId = facilityId;
        BuildingDefinitionId = buildingDefinitionId;
        FacilityDisplayName = facilityDisplayName;
        PositionX = positionX;
        PositionY = positionY;
        AbsoluteDay = absoluteDay;
        OwnerRevision = ownerRevision;
    }

    public ProductionFacilityDestructiveDrainOperationId OperationId { get; }
    public BuildingInstanceId FacilityId { get; }
    public string BuildingDefinitionId { get; }
    public KoreanNameSnapshot FacilityDisplayName { get; }
    public int PositionX { get; }
    public int PositionY { get; }
    public int AbsoluteDay { get; }
    public long OwnerRevision { get; }
    public GameplayResultKey ResultKey =>
        BuildingDemolitionOutcomeIds.ResultKey(OperationId, OwnerRevision);
}

public sealed class BuildingDemolitionOutcomeAdapter :
    GameplayOutcomeAdapter<BuildingDemolitionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        BuildingDemolitionOutcomeIds.Demolished;

    public override OutcomePrepareResult TryGetRequirements(
        in BuildingDemolitionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = default;
        if (!receipt.ResultKey.IsValid || receipt.OwnerRevision <= 0L)
        {
            return new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "building-demolition-receipt-invalid");
        }
        requirements = new OutcomeWriteRequirements(
            receipt.ResultKey,
            OutcomeTypeId,
            receipt.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            currentWorldEpoch,
            receipt.OwnerRevision,
            participantCount: 1,
            metricCount: 1,
            subjectCount: 1,
            tagCount: 2,
            anchorCount: 0,
            provenanceCount: 2,
            factCount: 2);
        return OutcomePrepareResult.Prepared();
    }

    public override OutcomePrepareResult TryWrite(
        in BuildingDemolitionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId facility = new(
            BuildingDemolitionOutcomeIds.FacilityKind,
            receipt.FacilityId.Value);
        bool written = builder.AddParticipant(
                new GameplayOutcomeParticipant(
                    facility,
                    BuildingDemolitionOutcomeIds.DemolishedFacilityRole,
                    GameplayParticipationKind.Direct,
                    true,
                    receipt.FacilityDisplayName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                facility,
                0.68f,
                NarrativeMemoryTier.Recent,
                false,
                false,
                0))
            && builder.AddMetric(new GameplayOutcomeMetric(
                BuildingDemolitionOutcomeIds.OwnerRevisionMetric,
                receipt.OwnerRevision,
                BuildingDemolitionOutcomeIds.RevisionUnit,
                facility))
            && builder.AddTag(
                BuildingDemolitionOutcomeIds.InfrastructureTag)
            && builder.AddTag(BuildingDemolitionOutcomeIds.DemolitionTag)
            && builder.AddProvenance(
                new GameplayOutcomeProvenanceReference(
                    "runtime-receipt",
                    "building-demolition-v1"))
            && builder.AddProvenance(
                new GameplayOutcomeProvenanceReference(
                    "domain-commit",
                    receipt.OperationId.Value))
            && builder.AddFact(new GameplayOutcomeFact(
                BuildingDemolitionOutcomeIds.DefinitionIdFact,
                receipt.BuildingDefinitionId))
            && builder.AddFact(new GameplayOutcomeFact(
                BuildingDemolitionOutcomeIds.DrainOperationFact,
                receipt.OperationId.Value))
            && builder.SetLocation(new GameplayLocationReference(
                "dungeon",
                string.Empty,
                receipt.PositionX,
                receipt.PositionY));
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "building-demolition-write-failed");
    }
}

public readonly struct PreparedBuildingDemolitionOutcome
{
    internal PreparedBuildingDemolitionOutcome(
        PreparedOwnerOutcome prepared,
        GameplayResultKey resultKey,
        long ownerRevision,
        bool isReplay)
    {
        Prepared = prepared;
        ResultKey = resultKey;
        OwnerRevision = ownerRevision;
        IsReplay = isReplay;
    }

    internal PreparedOwnerOutcome Prepared { get; }
    public GameplayResultKey ResultKey { get; }
    public long OwnerRevision { get; }
    public bool IsReplay { get; }
}

public interface IBuildingDemolitionOutcomeCommitter
{
    bool TryPrepare(
        in BuildingDemolitionOutcomeReceipt receipt,
        out PreparedBuildingDemolitionOutcome prepared,
        out string failureReason);
    OwnerOutcomeCommitResult Commit(
        in PreparedBuildingDemolitionOutcome prepared);
    OwnerOutcomeCommitResult Reconcile(GameplayResultKey resultKey);
    void Cancel(in PreparedBuildingDemolitionOutcome prepared);
}

public sealed class BuildingDemolitionGameplayOutcomeBridge :
    IBuildingDemolitionOutcomeCommitter
{
    private readonly PreparedOutcomeOwnerTransaction transactions;

    public BuildingDemolitionGameplayOutcomeBridge(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        transactions = new PreparedOutcomeOwnerTransaction(
            recorder ?? throw new ArgumentNullException(nameof(recorder)),
            diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)));
    }

    public bool TryPrepare(
        in BuildingDemolitionOutcomeReceipt receipt,
        out PreparedBuildingDemolitionOutcome prepared,
        out string failureReason)
    {
        prepared = default;
        if (!transactions.TryPrepare(
                receipt,
                out PreparedOwnerOutcome token,
                out OwnerOutcomeCommitResult replay,
                out _,
                out failureReason))
        {
            return false;
        }
        prepared = new PreparedBuildingDemolitionOutcome(
            token,
            receipt.ResultKey,
            receipt.OwnerRevision,
            replay.DurablyCommitted);
        return true;
    }

    public OwnerOutcomeCommitResult Commit(
        in PreparedBuildingDemolitionOutcome prepared) =>
        prepared.IsReplay
            ? transactions.Reconcile(prepared.ResultKey)
            : transactions.Commit(prepared.Prepared, prepared.OwnerRevision);

    public OwnerOutcomeCommitResult Reconcile(GameplayResultKey resultKey) =>
        transactions.Reconcile(resultKey);

    public void Cancel(in PreparedBuildingDemolitionOutcome prepared)
    {
        if (!prepared.IsReplay)
            transactions.Cancel(prepared.Prepared);
    }
}

internal sealed class BuildingDemolitionMemoryPolicy : IOutcomeMemoryPolicy
{
    public int PolicyVersion => 1;
    public GameplayMemorySignature GetSignature(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => new(outcome.OutcomeTypeId.Value);

    public OutcomeMemoryEvaluation Evaluate(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId,
        int priorMatchingCount,
        int evaluationDay)
    {
        int age = Math.Max(0, evaluationDay - outcome.AbsoluteDay);
        float salience = Math.Clamp(
            0.68f - Math.Min(0.18f, age * 0.006f),
            0f,
            1f);
        return new OutcomeMemoryEvaluation(
            salience,
            NarrativeMemoryTier.Recent,
            Math.Max(evaluationDay + 3, outcome.AbsoluteDay + 3));
    }
}

internal sealed class BuildingDemolitionPerceptionPolicy :
    IOutcomePerceptionPolicy
{
    public int MaximumOptionalWitnessLinks => 0;
    public bool ShouldCreateOptionalWitnessLink(
        in GameplayOutcomeReadView outcome,
        in GameplayOutcomeSubjectLink candidate) => false;
}

internal sealed class BuildingDemolitionConsolidator :
    IOutcomeMemoryConsolidator
{
    public bool CanCompact(
        in GameplayOutcomeReadView outcome,
        GameplayEntityId subjectId) => false;
    public bool IsAdditiveMetric(GameplayMetricId metricId) => false;
}

internal sealed class BuildingDemolitionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    private const string RendererVersion = "building-demolition-v1";
    private readonly IKoreanJosaFormatter josa;

    public BuildingDemolitionPerspectiveProjector(IKoreanJosaFormatter josa) =>
        this.josa = josa ?? throw new ArgumentNullException(nameof(josa));

    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        GameplayOutcomeParticipant facility = outcome.GetParticipant(0);
        KoreanJosaFormatResult topic = josa.Format(new KoreanJosaRequest(
            facility.DisplayName,
            KoreanJosaKind.Topic));
        string text = topic.RequiresNeutralFrame
            ? "시설 철거 완료: " + facility.DisplayName.DisplayText
                + "의 해체가 끝나 터가 비었다."
            : topic.Text + " 해체되어 터가 비었다.";
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            text,
            RendererVersion,
            topic.RequiresNeutralFrame);
    }
}

public sealed class BuildingDemolitionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private readonly INarrativePerspectiveProjector projector;
    private static readonly IOutcomeMemoryPolicy Memory =
        new BuildingDemolitionMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new BuildingDemolitionPerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new BuildingDemolitionConsolidator();

    public BuildingDemolitionOutcomeDescriptor(IKoreanJosaFormatter josa) =>
        projector = new BuildingDemolitionPerspectiveProjector(josa);

    public GameplayOutcomeTypeId OutcomeTypeId =>
        BuildingDemolitionOutcomeIds.Demolished;
    public INarrativePerspectiveProjector PerspectiveProjector => projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;

    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(
            BuildingDemolitionOutcomeIds.DemolishedFacilityRole);

    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        metricId.Equals(BuildingDemolitionOutcomeIds.OwnerRevisionMetric)
        && unitId.Equals(BuildingDemolitionOutcomeIds.RevisionUnit);

    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome)
    {
        bool shape = outcome.OutcomeTypeId == OutcomeTypeId
            && outcome.ParticipantCount == 1
            && outcome.SubjectCount == 1
            && outcome.MetricCount == 1
            && outcome.TagCount == 2
            && outcome.ProvenanceCount == 2
            && outcome.FactCount == 2
            && outcome.GetParticipant(0).RoleId.Equals(
                BuildingDemolitionOutcomeIds.DemolishedFacilityRole);
        return shape
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "building-demolition-shape-invalid");
    }
}

public static class BuildingDemolitionOutcomeRegistration
{
    public static void RegisterBuildingDemolitionGameplayOutcomes(
        this IContainerBuilder builder)
    {
        builder.Register<BuildingDemolitionGameplayOutcomeBridge>(
                Lifetime.Singleton)
            .As<IBuildingDemolitionOutcomeCommitter>();
        builder.Register<BuildingDemolitionOutcomeAdapter>(Lifetime.Singleton)
            .As<IGameplayOutcomeAdapterRegistration>();
        builder.Register<BuildingDemolitionOutcomeDescriptor>(
                Lifetime.Singleton)
            .As<IGameplayOutcomeDescriptor>();
    }
}

public static class BuildingDemolitionOutcomeLifecycle
{
    public static string ProjectContribution(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "A valid facility ID is required.",
                nameof(facilityId));
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("building-demolition-outcome-lifecycle@1");
        digest.Append(facilityId.Value);
        return digest.ComputeSha256();
    }
}

public sealed class BuildingDemolitionOutcomeLifecycleContributor :
    IProductionOutputDestinationLifecycleContributor
{
    public string ContributorId =>
        ProductionFacilityDestructiveDrainParticipantIds
            .BuildingDemolitionOutcome;

    public ProductionOutputDestinationLifecycleContribution Capture(
        BuildingInstanceId facilityId,
        ProductionOutputDestinationId destinationId)
    {
        string fingerprint = BuildingDemolitionOutcomeLifecycle
            .ProjectContribution(facilityId);
        return new ProductionOutputDestinationLifecycleContribution(
            ContributorId,
            hasAuthority: false,
            authorityRevision: 0,
            activeRecordCount: 0,
            ownedMassGrams: 0L,
            Array.Empty<ProductionOutputLifecycleBlock>(),
            fingerprint,
            fingerprint);
    }
}

public sealed class BuildingDemolitionDestructiveDrainParticipant :
    IProductionFacilityDestructiveDrainParticipant,
    IProductionFacilityDestructiveDrainDurablePrepareParticipant
{
    public const int CurrentContractVersion = 1;

    private static readonly IReadOnlyList<string> Dependencies =
        Array.AsReadOnly(new[]
        {
            ProductionFacilityDestructiveDrainParticipantIds
                .EnvironmentalFireDamageOutcome
        });

    private readonly IBuildingDemolitionOutcomeCommitter outcomes;
    private readonly Dictionary<string, PreparedBuildingDemolitionOutcome>
        preparedByOperation = new(StringComparer.Ordinal);

    public BuildingDemolitionDestructiveDrainParticipant(
        IBuildingDemolitionOutcomeCommitter outcomes)
    {
        this.outcomes = outcomes
            ?? throw new ArgumentNullException(nameof(outcomes));
    }

    public string ParticipantId =>
        ProductionFacilityDestructiveDrainParticipantIds
            .BuildingDemolitionOutcome;
    public int ContractVersion => CurrentContractVersion;
    public IReadOnlyList<string> DependsOnParticipantIds => Dependencies;

    public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
        ProductionFacilityDestructiveDrainPrepareContext context)
    {
        string contribution = BuildingDemolitionOutcomeLifecycle
            .ProjectContribution(context.FacilityId);
        ProductionFacilityDestructiveDrainOwnerPlan[] owners =
            context.Cause ==
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition
                ? new[]
                {
                    new ProductionFacilityDestructiveDrainOwnerPlan(
                        ProductionFacilityDestructiveDrainOwnerStableIds
                            .BuildingDemolition(context.OperationId.Value),
                        ProductionFacilityDestructiveDrainDisposition.Terminalize,
                        string.Empty,
                        context.OutcomeSnapshot.ComputeFingerprint(
                            context.FacilityId))
                }
                : Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>();
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("building-demolition-destructive-plan@1");
        digest.Append(context.FacilityId.Value);
        digest.Append(contribution);
        digest.Append(owners.Length);
        foreach (ProductionFacilityDestructiveDrainOwnerPlan owner in owners)
        {
            digest.Append(owner.OwnerStableId);
            digest.Append(owner.RequestFingerprint);
        }
        return new ProductionFacilityDestructiveDrainParticipantPlan(
            ParticipantId,
            CurrentContractVersion,
            contribution,
            digest.ComputeSha256(),
            owners);
    }

    public bool TryPrepareDurable(
        ProductionFacilityDestructiveDrainStepContext context,
        out string failureReason)
    {
        if (!TryCreateReceipt(context, out BuildingDemolitionOutcomeReceipt receipt,
                out failureReason))
        {
            return false;
        }
        if (preparedByOperation.TryGetValue(
                context.OperationId.Value,
                out PreparedBuildingDemolitionOutcome existing))
        {
            if (existing.ResultKey == receipt.ResultKey)
            {
                failureReason = string.Empty;
                return true;
            }
            failureReason = "building-demolition-prepared-operation-conflict";
            return false;
        }
        if (!outcomes.TryPrepare(receipt, out var prepared, out failureReason))
            return false;
        preparedByOperation.Add(context.OperationId.Value, prepared);
        failureReason = string.Empty;
        return true;
    }

    public ProductionFacilityDestructiveDrainStepResult TryCommit(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string contribution = BuildingDemolitionOutcomeLifecycle
            .ProjectContribution(context.FacilityId);
        if (!TryCreateReceipt(context, out BuildingDemolitionOutcomeReceipt receipt,
                out _)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase.Planned)
        {
            return Conflict(contribution);
        }
        if (!preparedByOperation.TryGetValue(
                context.OperationId.Value,
                out PreparedBuildingDemolitionOutcome prepared)
            && !outcomes.TryPrepare(receipt, out prepared, out _))
        {
            return Deferred(contribution);
        }
        OwnerOutcomeCommitResult committed = outcomes.Commit(prepared);
        if (!committed.DurablyCommitted)
        {
            outcomes.Cancel(prepared);
            preparedByOperation.Remove(context.OperationId.Value);
            return Conflict(contribution);
        }
        preparedByOperation.Remove(context.OperationId.Value);
        return Applied(context, contribution,
            ProductionFacilityDestructiveDrainStepStatus.Applied);
    }

    public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string contribution = BuildingDemolitionOutcomeLifecycle
            .ProjectContribution(context.FacilityId);
        if (!TryCreateReceipt(context, out BuildingDemolitionOutcomeReceipt receipt,
                out _)
            || context.Owner.phase !=
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck
            || !MatchesJournalReceipt(context))
        {
            return Conflict(contribution);
        }
        OwnerOutcomeCommitResult reconciled = outcomes.Reconcile(
            receipt.ResultKey);
        if (!reconciled.DurablyCommitted)
            return Conflict(contribution);
        return reconciled.Acknowledged
            ? Applied(context, contribution,
                ProductionFacilityDestructiveDrainStepStatus.Applied)
            : Deferred(contribution);
    }

    public ProductionFacilityDestructiveDrainRecoveryResult Recover(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        string contribution = BuildingDemolitionOutcomeLifecycle
            .ProjectContribution(context.FacilityId);
        if (!TryCreateReceipt(context, out BuildingDemolitionOutcomeReceipt receipt,
                out _))
        {
            return new ProductionFacilityDestructiveDrainRecoveryResult(
                ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
                Conflict(contribution));
        }
        if (context.Owner.phase ==
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged)
        {
            return new ProductionFacilityDestructiveDrainRecoveryResult(
                ProductionFacilityDestructiveDrainRecoveryAction
                    .AlreadyAcknowledged,
                Applied(context, contribution,
                    ProductionFacilityDestructiveDrainStepStatus.Replay));
        }
        if (context.Owner.phase ==
            ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck)
        {
            return new ProductionFacilityDestructiveDrainRecoveryResult(
                ProductionFacilityDestructiveDrainRecoveryAction
                    .ResumeAcknowledge,
                Deferred(contribution));
        }
        OwnerOutcomeCommitResult replay = outcomes.Reconcile(receipt.ResultKey);
        return new ProductionFacilityDestructiveDrainRecoveryResult(
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit,
            replay.DurablyCommitted
                ? Applied(context, contribution,
                    ProductionFacilityDestructiveDrainStepStatus.Replay)
                : Deferred(contribution));
    }

    private bool TryCreateReceipt(
        ProductionFacilityDestructiveDrainStepContext context,
        out BuildingDemolitionOutcomeReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        ProductionFacilityDestructiveDrainOutcomeSnapshot snapshot =
            context.OutcomeSnapshot;
        string expectedOwner = snapshot.IsValid
            ? ProductionFacilityDestructiveDrainOwnerStableIds
                .BuildingDemolition(context.OperationId.Value)
            : string.Empty;
        string expectedStep = expectedOwner.Length > 0
            ? ProductionFacilityDestructiveDrainCanonical.BuildStepOperationId(
                context.OperationId,
                ParticipantId,
                expectedOwner)
            : string.Empty;
        string expectedContribution = BuildingDemolitionOutcomeLifecycle
            .ProjectContribution(context.FacilityId);
        if (!string.Equals(context.ParticipantId, ParticipantId,
                StringComparison.Ordinal)
            || !snapshot.IsValid
            || !string.Equals(context.Owner.ownerStableId, expectedOwner,
                StringComparison.Ordinal)
            || !string.Equals(context.Owner.stepOperationId, expectedStep,
                StringComparison.Ordinal)
            || !string.Equals(
                context.Owner.requestFingerprint,
                snapshot.ComputeFingerprint(context.FacilityId),
                StringComparison.Ordinal)
            || !string.Equals(
                context.ExpectedDurableContributionFingerprint,
                expectedContribution,
                StringComparison.Ordinal))
        {
            failureReason = "building-demolition-destructive-owner-conflict";
            return false;
        }
        try
        {
            receipt = new BuildingDemolitionOutcomeReceipt(
                context.OperationId,
                context.FacilityId,
                snapshot.BuildingDefinitionId,
                BuildingDemolitionOutcomeNames.Snapshot(
                    context.FacilityId.Value,
                    snapshot.BuildingDisplayName),
                snapshot.PositionX,
                snapshot.PositionY,
                snapshot.AbsoluteDay,
                snapshot.OwnerRevision);
            failureReason = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException)
        {
            failureReason = "building-demolition-receipt-invalid:"
                + exception.Message;
            return false;
        }
    }

    private static ProductionFacilityDestructiveDrainStepResult Applied(
        ProductionFacilityDestructiveDrainStepContext context,
        string contribution,
        ProductionFacilityDestructiveDrainStepStatus status)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("building-demolition-destructive-receipt@1");
        digest.Append(context.Owner.requestFingerprint);
        digest.Append(context.Owner.stepOperationId);
        string fingerprint = digest.ComputeSha256();
        return new ProductionFacilityDestructiveDrainStepResult(
            status,
            "building-demolition-outcome:" + fingerprint.Substring(0, 24),
            fingerprint,
            contribution);
    }

    private static bool MatchesJournalReceipt(
        ProductionFacilityDestructiveDrainStepContext context)
    {
        ProductionFacilityDestructiveDrainStepResult expected = Applied(
            context,
            context.ExpectedDurableContributionFingerprint,
            ProductionFacilityDestructiveDrainStepStatus.Applied);
        return string.Equals(
                context.Owner.commitId,
                expected.CommitId,
                StringComparison.Ordinal)
            && string.Equals(
                context.Owner.receiptFingerprint,
                expected.ReceiptFingerprint,
                StringComparison.Ordinal);
    }

    private static ProductionFacilityDestructiveDrainStepResult Deferred(
        string contribution) => new(
        ProductionFacilityDestructiveDrainStepStatus.Deferred,
        string.Empty,
        string.Empty,
        contribution);

    private static ProductionFacilityDestructiveDrainStepResult Conflict(
        string contribution) => new(
        ProductionFacilityDestructiveDrainStepStatus.Conflict,
        string.Empty,
        string.Empty,
        contribution);
}
