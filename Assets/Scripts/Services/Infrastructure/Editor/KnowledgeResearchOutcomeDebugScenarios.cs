using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Real knowledge-completion transaction coverage. Fault ports only bracket
/// the external boundary; repository, physical batch, outcome ledger, and
/// reconnaissance authority remain the production implementations.
/// </summary>
public static class KnowledgeResearchOutcomeDebugScenarios
{
    private const string CompletionOutcomeType = "research.knowledge-completed";
    private const string ProgressOutcomeType = "research.work-applied";

    public static string RunAll()
    {
        VerifySinkBoundaries();
        VerifyCanonicalCommitThenThrow();
        VerifyRewardPreparationRejectsBeforePhysicalMutation();
        VerifyRevokeRejectRecovery();
        VerifyAcknowledgementRejectRecoveryAndCleanupParticipant();
        VerifyActualPhysicalCompletionEvidence();
        VerifyPostCommitReplanObserver();
        VerifyInvalidRewardReturnsWithoutCompletion();
        VerifyWholeSaveRoundTrip();
        return "PASS knowledge research runtime (9 groups): actual physical completion, reward rollback, acknowledgement recovery, whole-save continuation, and evidence";
    }

    /// <summary>
    /// Fault reproductions deliberately span a real physical transaction. They
    /// aggregate both failures so a regression never hides the second gap.
    /// </summary>
    public static string RunCommitGapFaults()
    {
        List<string> failures = new();
        foreach (string fault in new[] { "reward-after", "ack-after" })
        {
            try
            {
                if (fault == "reward-after")
                    VerifyRewardPublishedBeforeCanonicalCommitRollsBack();
                else
                    VerifyJointAcknowledgementThenThrowKeepsCleanup();
            }
            catch (Exception exception)
            {
                failures.Add(fault + ": " + exception.Message);
            }
        }

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "Knowledge research commit-gap reproductions failed:\n"
                + string.Join("\n", failures));
        }

        return "PASS knowledge research commit-gap faults (2 groups)";
    }

    private static void VerifySinkBoundaries()
    {
        foreach (string fault in new[] { "sink-reject", "sink-throw", "sink-after-commit" })
        {
            using Fixture fixture = new();
            fixture.Gateway.Fault = fault;
            BlueprintResearchWorkResult first = fixture.Runtime.ApplyApprovedWork(
                null, fixture.Facility, 24);

            Require(
                first.Success && !first.Completed && first.TotalProgress == 24f
                && fixture.Task != null && fixture.Task.completedWork == 24f
                && fixture.Sequence == 1,
                fault + " must retain the paid work as pending finalization");
            if (fault == "sink-after-commit")
            {
                Require(
                    !fixture.HasSource && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1
                    && fixture.Region.intelligenceDamage == 10f
                    && fixture.CanonicalOutcomeCount == 2,
                    "after-commit sink fault must retain the actual physical and canonical completion receipt");
            }
            else
            {
                Require(
                    fixture.HasSource && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
                    && fixture.Region.intelligenceDamage == 0f
                    && fixture.CanonicalOutcomeCount == 1,
                    fault + " must not mutate physical input or reconnaissance before its gateway succeeds");
            }

            fixture.Gateway.Fault = string.Empty;
            fixture.ClockTime = 1f;
            fixture.Runtime.Tick();
            AssertFinalizedOnce(fixture, fault + " recovery");
            fixture.ClockTime = 2f;
            fixture.Runtime.Tick();
            AssertFinalizedOnce(fixture, fault + " inert retry");
        }
    }

    private static void VerifyCanonicalCommitThenThrow()
    {
        using Fixture fixture = new();
        fixture.Faults.ThrowAfterCommit = true;
        BlueprintResearchWorkResult result = fixture.Runtime.ApplyApprovedWork(
            null, fixture.Facility, 24);

        Require(
            result.Success && result.Completed && fixture.Sequence == 1
            && fixture.CanonicalOutcomeCount == 2 && fixture.Region.intelligenceDamage == 10f,
            "A completion recorder throw after canonical commit must retain the actual committed result.");
        AssertFinalizedOnce(fixture, "canonical completion commit then throw");
    }

    private static void VerifyRewardPreparationRejectsBeforePhysicalMutation()
    {
        using Fixture fixture = new();
        fixture.Gateway.Fault = "reward-prepare-reject";
        BlueprintResearchWorkResult first = fixture.Runtime.ApplyApprovedWork(
            null, fixture.Facility, 24);

        Require(
            first.Success && !first.Completed && fixture.Task != null
            && fixture.Task.completedWork == 24f && fixture.HasSource
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && fixture.Region.intelligenceDamage == 100f && fixture.CanonicalOutcomeCount == 1,
            "Actual reconnaissance preparation rejection must leave its exact source and ledger completion unmutated.");

        fixture.Region.intelligenceDamage = 0f;
        fixture.Gateway.Fault = string.Empty;
        fixture.ClockTime = 1f;
        fixture.Runtime.Tick();
        AssertFinalizedOnce(fixture, "reward preparation rejection recovery");
        Require(
            fixture.Gateway.SuccessfulCommits == 1 && fixture.Gateway.TypedCommitCalls == 2,
            "Only the retry may terminally consume the prepared physical source.");
    }

    private static void VerifyRevokeRejectRecovery()
    {
        using Fixture fixture = new();
        fixture.Fault = "revoke-reject";
        BlueprintResearchWorkResult first = fixture.Runtime.ApplyApprovedWork(
            null, fixture.Facility, 24);

        Require(
            first.Success && !first.Completed && fixture.Task != null
            && !fixture.HasSource && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1
            && fixture.CanonicalOutcomeCount == 2 && fixture.Region.intelligenceDamage == 10f,
            "Destination revoke rejection must retain one actual physical/completion receipt for retry.");

        fixture.Fault = string.Empty;
        fixture.ClockTime = 1f;
        fixture.Runtime.Tick();
        AssertFinalizedOnce(fixture, "destination revoke recovery");
    }

    private static void VerifyAcknowledgementRejectRecoveryAndCleanupParticipant()
    {
        using (Fixture fixture = new())
        {
            fixture.Gateway.Fault = "ack-reject";
            BlueprintResearchWorkResult first = fixture.Runtime.ApplyApprovedWork(
                null, fixture.Facility, 24);
            Require(
                first.Success && !first.Completed && fixture.Task != null
                && !fixture.HasSource && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1
                && fixture.CanonicalOutcomeCount == 2 && fixture.Region.intelligenceDamage == 10f,
                "Acknowledgement rejection must not revive or duplicate actual physical/reward mutation.");

            fixture.Gateway.Fault = string.Empty;
            fixture.ClockTime = 1f;
            fixture.Runtime.Tick();
            AssertFinalizedOnce(fixture, "acknowledgement recovery");
        }

        using (Fixture fixture = new())
        {
            IPhysicalItemDispositionAcknowledgementParticipant stale =
                KnowledgeResidueCompletionTransaction.PrepareCleanup(
                    fixture.Root, fixture.Task);
            Require(
                !stale.TryCommit(out string staleReason)
                && staleReason == "knowledge-cleanup-owner-stale"
                && fixture.Task != null,
                "Cleanup must reject an awaiting task without removing it.");

            fixture.Task.dispositionPhase = KnowledgeResidueDispositionPhase.OutcomePublished;
            fixture.Task.completionOwnerRevision = 1L;
            IPhysicalItemDispositionAcknowledgementParticipant cleanup =
                KnowledgeResidueCompletionTransaction.PrepareCleanup(
                    fixture.Root, fixture.Task);
            Require(cleanup.TryCommit(out string commitReason) && commitReason.Length == 0
                && fixture.Task == null,
                "Joint cleanup participant did not remove its exact outcome-published task.");
            cleanup.Rollback();
            Require(fixture.Task != null
                && fixture.Task.dispositionPhase == KnowledgeResidueDispositionPhase.OutcomePublished
                && fixture.Task.completionOwnerRevision == 1L,
                "Joint cleanup rollback did not restore its exact owner snapshot.");
        }
    }

    private static void VerifyActualPhysicalCompletionEvidence()
    {
        using Fixture fixture = new();
        BlueprintResearchWorkResult result = fixture.Runtime.ApplyApprovedWork(
            null, fixture.Facility, 24);
        Require(result.Success && result.Completed, "Actual completion fixture did not finish.");
        AssertFinalizedOnce(fixture, "actual completion evidence");

        GameplayOutcomeSnapshot completion = fixture.Ledger.CaptureGameplayOutcomes()
            .exactOutcomes.Single(value => value.outcomeTypeId == CompletionOutcomeType);
        Require(
            completion.participants.Any(value => value.roleId == "consumed-material"
                && value.entityKindId == "item-stack"
                && value.entityId == fixture.SourceStackId)
            && completion.metrics.Any(value => value.metricId == "physical.input-quantity"
                && value.value == 1d && value.unitId == "count")
            && completion.metrics.Any(value => value.metricId == "physical.input-mass-grams"
                && value.value == 1_000d && value.unitId == "gram")
            && completion.metrics.Any(value => value.metricId == "physical.source-x"
                && value.value == fixture.Facility.centerPos.x && value.unitId == "cell")
            && completion.metrics.Any(value => value.metricId == "physical.source-y"
                && value.value == fixture.Facility.centerPos.y && value.unitId == "cell")
            && completion.facts.Any(value => value.factId == "physical.source-stack-id"
                && value.value == fixture.SourceStackId)
            && completion.facts.Any(value => value.factId == "physical.source-definition-id"
                && value.value == KnowledgeResidueDestinationAuthority.MemoryResidueItemId)
            && !completion.facts.Any(value =>
                value.factId == "physical.source-instance-id"),
            "Completion outcome lost actual source identity, mass, or facility position evidence.");
    }

    private static void VerifyPostCommitReplanObserver()
    {
        using Fixture fixture = new();
        fixture.ThrowReplan = true;
        BlueprintResearchWorkResult result = fixture.Runtime.ApplyApprovedWork(
            null, fixture.Facility, 24);
        Require(result.Success && result.Completed,
            "Post-commit observer failure cannot reject the completed knowledge work.");
        AssertFinalizedOnce(fixture, "post-commit replan observer");
    }

    private static void VerifyInvalidRewardReturnsWithoutCompletion()
    {
        using Fixture fixture = new();
        fixture.Region.intelligenceDamage = 100f;
        BlueprintResearchWorkResult result = fixture.Runtime.ApplyApprovedWork(
            null, fixture.Facility, 24);
        Require(
            result.Success && !result.Completed && fixture.Task == null
            && fixture.Releases == 1 && fixture.Gateway.SuccessfulCommits == 0
            && fixture.Region.intelligenceDamage == 100f && fixture.CanonicalOutcomeCount == 1
            && fixture.HasSource,
            "An invalid actual reward target must not invent a physical or completion outcome.");
    }

    private static void VerifyRewardPublishedBeforeCanonicalCommitRollsBack()
    {
        using Fixture fixture = new();
        fixture.Faults.BeforeCommitFaultObservation = () =>
            fixture.Region.intelligenceDamage == 10f
            && !fixture.HasSource
            && fixture.Task != null
            && fixture.Task.dispositionPhase == KnowledgeResidueDispositionPhase.OutcomePublished
            && fixture.Task.completionOwnerRevision > 0L;
        fixture.Faults.ThrowBeforeCommit = true;
        BlueprintResearchWorkResult first = fixture.Runtime.ApplyApprovedWork(
            null, fixture.Facility, 24);
        Require(
            first.Success && !first.Completed && fixture.Task != null
            && fixture.Task.completedWork == 24f && fixture.Sequence == 1
            && fixture.Region.intelligenceDamage == 0f && fixture.HasSource
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && fixture.CanonicalOutcomeCount == 1
            && fixture.Faults.BeforeCommitFaultObservations == 1
            && fixture.Faults.BeforeCommitFaultObservationPassed,
            "Reward publication before canonical commit must roll back actual reward and physical input.");

        fixture.Faults.ThrowBeforeCommit = false;
        fixture.ClockTime = 1f;
        fixture.Runtime.Tick();
        AssertFinalizedOnce(fixture, "reward publish-before-canonical retry");
    }

    private static void VerifyJointAcknowledgementThenThrowKeepsCleanup()
    {
        using Fixture fixture = new();
        fixture.Gateway.Fault = "ack-after";
        BlueprintResearchWorkResult first = fixture.Runtime.ApplyApprovedWork(
            null, fixture.Facility, 24);
        Require(
            first.Success && !first.Completed && fixture.Task == null
            && !fixture.HasSource && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && fixture.Gateway.SuccessfulCommits == 1
            && fixture.Gateway.SuccessfulAcknowledgements == 1
            && fixture.Sequence == 1 && fixture.CanonicalOutcomeCount == 2
            && fixture.Region.intelligenceDamage == 10f,
            "An outer acknowledgement throw after joint acknowledgement must retain task cleanup.");

        fixture.Gateway.Fault = string.Empty;
        fixture.ClockTime = 1f;
        fixture.Runtime.Tick();
        AssertFinalizedOnce(fixture, "acknowledgement-after retry");
    }

    private static void VerifyWholeSaveRoundTrip()
    {
        using Fixture fixture = new(KnowledgeResidueUse.CodexAnalysis);
        fixture.Faults.FailDelivery = true;
        fixture.Gateway.Fault = "ack-reject";
        BlueprintResearchWorkResult first = fixture.Runtime.ApplyApprovedWork(
            null,
            fixture.Facility,
            24);
        int setupPending = fixture.Repository
            .GetEditorPendingBatchDispositionCount();
        GameplayOutcomeLedgerSaveData setupLedger =
            fixture.Ledger.CaptureGameplayOutcomes();
        Require(
            first.Success && !first.Completed && fixture.Task != null
            && fixture.Task.dispositionPhase ==
                KnowledgeResidueDispositionPhase.OutcomePublished
            && fixture.Task.completionOwnerRevision > 0
            && setupPending == 1
            && setupLedger.outbox.Count == 1
            && setupLedger.exactOutcomes.Count == 1
            && fixture.CodexHasReward,
            "Whole-save setup did not retain one committed Codex reward, material receipt, task owner, and pending outbox: "
            + "success=" + first.Success
            + ", completed=" + first.Completed
            + ", task=" + (fixture.Task != null)
            + ", phase=" + (fixture.Task?.dispositionPhase.ToString() ?? "missing")
            + ", ownerRevision=" + (fixture.Task?.completionOwnerRevision ?? 0)
            + ", pending=" + setupPending
            + ", outbox=" + setupLedger.outbox.Count
            + ", exact=" + setupLedger.exactOutcomes.Count
            + ", codexLines=" + fixture.CodexRewardLineCount
            + ", clue=" + fixture.CodexReward);

        DungeonResearchSaveData research = JsonUtility.FromJson<
            DungeonResearchSaveData>(JsonUtility.ToJson(
            new DungeonResearchSaveData
            {
                outcomeSequence = fixture.Sequence,
                nextKnowledgeTaskSequence = fixture.Runtime.NextTaskSequence,
                knowledgeTaskIdentityGeneration =
                    fixture.Runtime.TaskIdentityGeneration,
                knowledgeTasks = fixture.Runtime.Capture().ToList()
            }));
        DungeonPhysicalItemSaveData physical = JsonUtility.FromJson<
            DungeonPhysicalItemSaveData>(JsonUtility.ToJson(
            fixture.Items.Capture()));
        GameplayOutcomeLedgerSaveData ledger = JsonUtility.FromJson<
            GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(
            fixture.Ledger.CaptureGameplayOutcomes()));
        CodexSaveApplicationAdapter codexSave =
            fixture.CreateCodexSaveAdapter();
        DungeonCodexSaveData codex = JsonUtility.FromJson<
            DungeonCodexSaveData>(JsonUtility.ToJson(codexSave.Capture()));

        DungeonGameRestoreReport joined = new();
        ResearchOutcomeSavePreflight.ValidateSequence(
            research,
            ledger,
            joined);
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            ledger,
            joined);
        Require(joined.Success,
            "Whole-save detached research/material/outbox join failed: "
            + string.Join(" | ", joined.Errors));

        fixture.Root.Replace(new KnowledgeResidueAggregateState());
        new BlueprintResearchState(fixture.Root).RestoreOutcomeSequence(0);
        fixture.Codex.ReplaceWithEmptyStateForDebug();
        fixture.Items.Restore(physical);
        GameplayOutcomeLedgerRestoreCandidate ledgerCandidate =
            fixture.Ledger.PrepareGameplayOutcomeRestore(ledger);
        fixture.Ledger.BeginRestoreCandidate();
        fixture.Ledger.PublishGameplayOutcomeRestore(ledgerCandidate);
        fixture.Ledger.PublishRestoreCandidate();
        fixture.Ledger.CompleteRestoreCandidate();
        codexSave.Restore(codex);
        BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            research.knowledgeTasks,
            new SavedPhysicalCandidateQuery(physical));
        fixture.Runtime.Restore(fixture.Runtime.PrepareRestore(
            research.knowledgeTasks,
            research.nextKnowledgeTaskSequence,
            research.knowledgeTaskIdentityGeneration));
        new BlueprintResearchState(fixture.Root)
            .RestoreOutcomeSequence(research.outcomeSequence);

        Require(
            fixture.Task != null
            && fixture.Task.completionOwnerRevision > 0
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1
            && fixture.Ledger.CaptureGameplayOutcomes().outbox.Count == 1
            && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1
            && fixture.CodexHasReward,
            "Whole-save restore lost the exact task/material/outbox/Codex state.");

        fixture.Faults.FailDelivery = false;
        Require(
            fixture.Faults.RetryPendingDeliveries(8) == 1
            && fixture.Faults.RetryPendingDeliveries(8) == 0
            && fixture.Ledger.CaptureGameplayOutcomes().outbox.Count == 0
            && fixture.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 2,
            "Restored outbox did not deliver exactly once.");
        fixture.Gateway.Fault = string.Empty;
        fixture.ClockTime = 1f;
        fixture.Runtime.Tick();
        Require(
            fixture.Task == null
            && fixture.Runtime.Tasks.Count == 0
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && fixture.Gateway.SuccessfulCommits == 1
            && fixture.Gateway.SuccessfulAcknowledgements == 1
            && fixture.CodexRewardLineCount == 1
            && fixture.Runtime.NextTaskSequence == 2
            && fixture.Runtime.TaskIdentityGeneration
                == KnowledgeResidueTaskIdentity.OriginalGeneration,
            "Restored completion did not acknowledge once while preserving one Codex reward and the task high-water mark.");
    }

    private static void AssertFinalizedOnce(Fixture fixture, string boundary)
    {
        Require(
            fixture.Runtime.Tasks.Count == 0 && fixture.Task == null
            && !fixture.HasSource && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0
            && fixture.Gateway.SuccessfulCommits == 1
            && fixture.Gateway.SuccessfulAcknowledgements == 1
            && fixture.Region.intelligenceDamage == 10f && fixture.Sequence == 1
            && fixture.CanonicalOutcomeCount == 2 && fixture.HasProgressAndCompletion,
            boundary + " must finish exactly one physical sink, reward, cleanup, and two outcome records; tasks="
            + fixture.Runtime.Tasks.Count + ", source=" + fixture.HasSource + ", pending="
            + fixture.Repository.GetEditorPendingBatchDispositionCount() + ", commits=" + fixture.Gateway.SuccessfulCommits
            + ", ack=" + fixture.Gateway.SuccessfulAcknowledgements + ", pressure=" + fixture.Region.intelligenceDamage
            + ", records=" + fixture.CanonicalOutcomeCount + ", sequence=" + fixture.Sequence);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Knowledge research outcome: " + message);
    }

    private sealed class Fixture : IDisposable
    {
        internal const string TaskId = "knowledge-00001";
        private readonly GameObject facilityHost = new("Knowledge research outcome facility");
        private readonly GameObject codexHost = new("Knowledge research outcome codex");
        internal readonly DungeonRuntimeAggregateRootStore Root = new();
        internal readonly BuildableObject Facility;
        internal readonly KnowledgeResidueProcessingRuntime Runtime;
        internal readonly GameplayOutcomeLedger Ledger;
        internal readonly WorldItemStackRuntime Items;
        internal readonly WorldItemRepository Repository;
        internal readonly FaultRecorder Faults;
        internal readonly FaultingGateway Gateway;
        internal readonly OffenseRegionRuntime Regions = new();
        internal readonly OffenseRegionState Region;
        internal readonly CodexRuntime Codex;
        internal readonly string CodexReward;
        internal readonly string SourceStackId;
        internal string Fault = string.Empty;
        internal bool ThrowReplan;
        internal float ClockTime;
        internal int Releases;
        internal long Sequence => new BlueprintResearchState(Root).OutcomeSequence;
        // Includes both delivered exact history and the durable committed outbox;
        // a post-commit fault may intentionally leave the latter undelivered.
        internal int CanonicalOutcomeCount
        {
            get
            {
                GameplayOutcomeLedgerSaveData saved = Ledger.CaptureGameplayOutcomes();
                return saved.exactOutcomes.Count + saved.outbox.Count;
            }
        }
        internal KnowledgeResidueTaskSaveData Task => Root
            .GetOrCreate(() => new KnowledgeResidueAggregateState()).FirstTask;
        internal bool HasSource => Items.GetAllStacks().Any(stack => stack != null
            && stack.StackId == SourceStackId
            && stack.ItemId == KnowledgeResidueDestinationAuthority.MemoryResidueItemId
            && stack.Quantity == 1 && stack.State == WorldItemStackState.FacilityBuffer);
        internal bool HasProgressAndCompletion => Ledger.CaptureGameplayOutcomes().exactOutcomes
            .Concat(Ledger.CaptureGameplayOutcomes().outbox)
            .Select(value => value.outcomeTypeId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(new[] { CompletionOutcomeType, ProgressOutcomeType }
                .OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);
        internal int CodexRewardLineCount => Codex.State
            .GetSnapshot(CodexEntryCategory.Invasion, "memory-residue")
            ?.lines.Count(line => string.Equals(
                line.Text,
                CodexReward,
                StringComparison.Ordinal)) ?? 0;
        internal bool CodexHasReward => CodexRewardLineCount == 1;

        internal Fixture(
            KnowledgeResidueUse use = KnowledgeResidueUse.RegionReconnaissance)
        {
            Facility = facilityHost.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(Facility);
            BuildingSO definition = AssetDatabase.FindAssets("t:BuildingSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
                .Where(value => value != null)
                .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal)
                .First(value => value.Facility?.SupportsWork(BuiltInWorkTypeIds.Research) == true);
            Facility.Initialization(definition, new Vector2Int(8, 6));
            codexHost.SetActive(false);
            Codex = codexHost.AddComponent<CodexRuntime>();
            CodexReward = use == KnowledgeResidueUse.CodexAnalysis
                && Codex.TryGetNextMemoryResidueClue(out string clue)
                    ? clue
                    : string.Empty;

            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new ResearchWorkOutcomeDescriptor(),
                    new KnowledgeCompletionOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new ResearchWorkOutcomeAdapter(),
                    new KnowledgeCompletionOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(registry, GameplayOutcomeBufferLimits.Default);
            IGameEventBus events = new GameEventBus();
            GameplayOutcomeRecorder recorder = new(Ledger, registry, events);
            Faults = new FaultRecorder(recorder);

            FixtureCatalog catalog = new();
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                catalog, Root, out WorldItemRepository repository, out _,
                out ItemQuantityReservationService reservations, out _, out _, out _);
            Repository = repository;
            Region = Regions.Regions.Single(value => value.regionId
                == OffenseRegionRuntime.BorderTradeRegionId);
            Region.intelligenceDamage = 0f;

            KnowledgeResidueAggregateState state = new();
            state.AddRestoredTask(new KnowledgeResidueTaskSaveData
            {
                taskId = TaskId,
                use = use,
                regionId = use == KnowledgeResidueUse.RegionReconnaissance
                    ? Region.regionId
                    : string.Empty,
                requiredWork = 24f,
                facilityId = Facility.id,
                facilityX = Facility.centerPos.x,
                facilityY = Facility.centerPos.y,
                assignmentSequence = 1,
                facilityInstanceId = Facility.RequirePersistentInstanceId().Value,
                destinationId = KnowledgeResidueDestinationAuthority.FormatDestinationId(TaskId, 1),
                inputCapacityGrams = 1_000L,
                massAuthorityRevision = Items.MassQuery.AuthorityRevision,
                inputCapacityFingerprint = "qa-knowledge-residue-mass-v1",
                sinkOperationId = KnowledgeResidueDestinationAuthority.FormatSinkOperationId(TaskId),
                sinkReasonCode = KnowledgeResidueDestinationAuthority.SinkReasonCode,
                codexCluePayload = CodexReward
            });
            state.RestoreAllocationIdentity(
                2,
                KnowledgeResidueTaskIdentity.OriginalGeneration);
            Root.Replace(state);
            SourceStackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                1,
                WorldItemStackState.FacilityBuffer,
                Task.destinationId,
                position: Facility.centerPos,
                itemInstanceId: string.Empty);

            IPhysicalItemBatchDispositionService dispositions =
                new PhysicalItemBatchDispositionService(
                    Repository,
                    Items.MassQuery,
                    EditorNullItemMarkerPresenter.Instance,
                    reservations,
                    catalog,
                    null,
                    Ledger,
                    Faults);
            IOutcomeAwarePhysicalFacilityItemSinkGateway actualGateway =
                new PhysicalFacilityItemSinkGateway(
                    new PhysicalStockQuery(Repository, catalog, Items.MassQuery),
                    dispositions);
            Gateway = new FaultingGateway(this, actualGateway);
            IBuildingWorldQuery buildings = StrictPort.Create<IBuildingWorldQuery>((method, _) =>
                method.Name == "get_Buildings" ? new[] { Facility } : throw Unexpected(method));
            IKnowledgeResidueDestinationRuntime destinations =
                StrictPort.Create<IKnowledgeResidueDestinationRuntime>((method, args) =>
                {
                    if (method.Name != "TryRevoke") throw Unexpected(method);
                    args[1] = Fault == "revoke-reject" ? "injected-revoke" : string.Empty;
                    return Fault != "revoke-reject";
                });
            IFacilityBufferDestinationReleaseService releases =
                StrictPort.Create<IFacilityBufferDestinationReleaseService>((method, args) =>
                {
                    if (method.Name != "TryReleaseAtOwnerPosition") throw Unexpected(method);
                    Releases++;
                    Type element = method.GetParameters()[3].ParameterType.GetElementType();
                    args[3] = element.IsValueType ? Activator.CreateInstance(element) : null;
                    args[4] = string.Empty;
                    return true;
                });
            IWorkforceReplanService workforce = StrictPort.Create<IWorkforceReplanService>((method, _) =>
            {
                if (method.Name != "RequestIdleWorkersToReplan") throw Unexpected(method);
                if (ThrowReplan) throw new InvalidOperationException("injected-replan-observer");
                return null;
            });
            IGameClock clock = StrictPort.Create<IGameClock>((method, _) => method.Name switch
            {
                "get_IsPaused" => false,
                "get_Time" => ClockTime,
                _ => throw Unexpected(method)
            });
            IDungeonDebugRuleQuery debug = StrictPort.Create<IDungeonDebugRuleQuery>((method, _) =>
                method.Name == "IsEnabled" ? false : throw Unexpected(method));

            Runtime = new KnowledgeResidueProcessingRuntime(
                Items,
                Gateway,
                destinations,
                releases,
                buildings,
                new FacilityFeatureSceneRuntimeReferences(null, null, Codex),
                Regions,
                new KnowledgeResidueExecutionServices(workforce, events, clock, debug),
                Root);
            Runtime.ConstructGameplayOutcomes(
                new BlueprintResearchOutcomeTransaction(recorder, Ledger));
            Runtime.ConstructCompletionOutcomes(
                new KnowledgeResidueCompletionTransaction(Faults, Ledger));
        }

        internal CodexSaveApplicationAdapter CreateCodexSaveAdapter() =>
            new(new FacilityFeatureSceneRuntimeReferences(null, null, Codex));

        public void Dispose()
        {
            Items.Dispose();
            UnityEngine.Object.DestroyImmediate(codexHost);
            UnityEngine.Object.DestroyImmediate(facilityHost);
        }
    }

    private sealed class FaultingGateway :
        IPhysicalFacilityItemSinkGateway,
        IOutcomeAwarePhysicalFacilityItemSinkGateway
    {
        private readonly Fixture fixture;
        private readonly IPhysicalFacilityItemSinkGateway sink;
        private readonly IOutcomeAwarePhysicalFacilityItemSinkGateway outcomes;

        internal FaultingGateway(
            Fixture fixture,
            IOutcomeAwarePhysicalFacilityItemSinkGateway actual)
        {
            this.fixture = fixture;
            outcomes = actual ?? throw new ArgumentNullException(nameof(actual));
            sink = (IPhysicalFacilityItemSinkGateway)actual;
        }

        internal string Fault { get; set; } = string.Empty;
        internal int TypedCommitCalls { get; private set; }
        internal int SuccessfulCommits { get; private set; }
        internal int AcknowledgementCalls { get; private set; }
        internal int SuccessfulAcknowledgements { get; private set; }

        public bool TryCommitSinkPending(
            string destinationId,
            string itemId,
            int quantity,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => sink.TryCommitSinkPending(
                destinationId, itemId, quantity, operationId, reasonCode,
                out receipt, out failureReason);

        public bool TryCommitSinkPending(
            string destinationId,
            string itemId,
            int quantity,
            string operationId,
            string reasonCode,
            IPhysicalItemBatchDispositionOutcomeParticipant participant,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = string.Empty;
            if (Fault == "sink-throw")
                throw new InvalidOperationException("injected-sink-before-commit");
            if (Fault == "sink-reject")
            {
                failureReason = "injected-sink-reject";
                return false;
            }
            Require(
                destinationId == fixture.Task.destinationId
                && itemId == KnowledgeResidueDestinationAuthority.MemoryResidueItemId
                && quantity == 1,
                "Actual gateway received an unexpected knowledge input command.");
            if (Fault == "reward-prepare-reject")
                fixture.Region.intelligenceDamage = 100f;

            TypedCommitCalls++;
            bool committed = outcomes.TryCommitSinkPending(
                destinationId, itemId, quantity, operationId, reasonCode, participant,
                out receipt, out failureReason);
            if (committed) SuccessfulCommits++;
            if (Fault == "sink-after-commit" && committed)
                throw new InvalidOperationException("injected-sink-after-commit");
            return committed;
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            sink.TryGetPending(operationId, out receipt);

        public bool Acknowledge(string commitId, out string failureReason) =>
            sink.Acknowledge(commitId, out failureReason);

        public bool Acknowledge(
            string commitId,
            IPhysicalItemDispositionAcknowledgementParticipant participant,
            out string failureReason)
        {
            if (Fault == "ack-reject")
            {
                failureReason = "injected-ack-reject";
                return false;
            }
            AcknowledgementCalls++;
            bool acknowledged = outcomes.Acknowledge(
                commitId, participant, out failureReason);
            if (acknowledged) SuccessfulAcknowledgements++;
            if (Fault == "ack-after" && acknowledged)
                throw new InvalidOperationException("injected-ack-after-joint-commit");
            return acknowledged;
        }
    }

    private sealed class FaultRecorder : IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;
        internal bool ThrowBeforeCommit;
        internal bool ThrowAfterCommit;
        internal bool FailDelivery;
        internal Func<bool> BeforeCommitFaultObservation;
        internal int BeforeCommitFaultObservations { get; private set; }
        internal bool BeforeCommitFaultObservationPassed { get; private set; }

        internal FaultRecorder(IGameplayOutcomeRecorder inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public OutcomePrepareResult TryPrepare<T>(in T receipt, out PreparedOutcomeToken token) =>
            inner.TryPrepare(receipt, out token);

        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation) =>
            inner.TryReserve(requirements, out reservation);

        public OutcomePrepareResult TryWriteReserved<T>(
            in T receipt,
            in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken token) =>
            inner.TryWriteReserved(receipt, reservation, out token);

        public void CancelReservation(in PreparedOutcomeReservation reservation) =>
            inner.CancelReservation(reservation);

        public void CancelPrepared(in PreparedOutcomeToken token) =>
            inner.CancelPrepared(token);

        public OutcomeCommitResult CommitPrepared(
            in PreparedOutcomeToken token,
            long revision,
            out CommittedOutcomeToken committed)
        {
            committed = default;
            if (ThrowBeforeCommit)
            {
                BeforeCommitFaultObservations++;
                BeforeCommitFaultObservationPassed =
                    BeforeCommitFaultObservation?.Invoke() ?? true;
                throw new InvalidOperationException("injected-before-canonical-commit");
            }
            OutcomeCommitResult result = inner.CommitPrepared(token, revision, out committed);
            if (ThrowAfterCommit)
            {
                committed = default;
                throw new InvalidOperationException("injected-after-canonical-commit");
            }
            return result;
        }

        public OutcomeCommitResult CommitPreparedBatch(
            PreparedOutcomeToken[] prepared,
            long[] revisions,
            CommittedOutcomeToken[] committed)
        {
            if (ThrowBeforeCommit)
            {
                BeforeCommitFaultObservations++;
                BeforeCommitFaultObservationPassed =
                    BeforeCommitFaultObservation?.Invoke() ?? true;
                throw new InvalidOperationException(
                    "injected-before-canonical-commit");
            }
            OutcomeCommitResult result = inner.CommitPreparedBatch(
                prepared,
                revisions,
                committed);
            if (ThrowAfterCommit)
            {
                Array.Clear(committed, 0, committed.Length);
                throw new InvalidOperationException(
                    "injected-after-canonical-commit");
            }
            return result;
        }

        public OutcomeDeliveryResult TryDeliver(in CommittedOutcomeToken committed) =>
            FailDelivery
                ? new OutcomeDeliveryResult(
                    OutcomeDeliveryCode.PendingDeliveryFault,
                    committed.OutcomeId,
                    "injected")
                : inner.TryDeliver(committed);

        public OutcomeAcknowledgeResult Acknowledge(in CommittedOutcomeToken committed) =>
            inner.Acknowledge(committed);

        public int RetryPendingDeliveries(int maximumCount) =>
            inner.RetryPendingDeliveries(maximumCount);
    }

    private sealed class SavedPhysicalCandidateQuery :
        IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> pending;

        internal SavedPhysicalCandidateQuery(DungeonPhysicalItemSaveData source)
        {
            pending = (source?.pendingBatchDispositions
                    ?? new List<PhysicalItemBatchDispositionSaveData>())
                .Where(value => value != null)
                .Select(value =>
                    new PhysicalItemRestoreCandidateDispositionSnapshot(value))
                .OrderBy(value => value.OperationId, StringComparer.Ordinal)
                .ToArray();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => pending;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = pending.SingleOrDefault(value => string.Equals(
                value.OperationId,
                operationId,
                StringComparison.Ordinal));
            return disposition != null;
        }
    }

    private sealed class FixtureCatalog : IDungeonItemCatalogProvider
    {
        private readonly IReadOnlyList<DungeonItemDefinition> all =
            new[]
            {
                new DungeonItemDefinition(
                    KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                    "기억 잔재",
                    "Knowledge residue test input",
                    StockCategory.General,
                    1,
                    null,
                    1f,
                    1)
            };

        public IReadOnlyList<DungeonItemDefinition> All => all;

        public DungeonItemDefinition GetDefinition(string itemId) =>
            TryGetDefinition(itemId, out DungeonItemDefinition definition)
                ? definition
                : throw new KeyNotFoundException(itemId ?? string.Empty);

        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition definition)
        {
            definition = all.FirstOrDefault(value => value.ItemId == itemId);
            return definition != null;
        }
    }

    private static Exception Unexpected(MethodInfo method) =>
        new InvalidOperationException(
            "Unexpected knowledge fixture port: "
            + method.DeclaringType?.Name + "." + method.Name);

    public class StrictPort : DispatchProxy
    {
        private Func<MethodInfo, object[], object> invoke;

        internal static T Create<T>(Func<MethodInfo, object[], object> handler)
            where T : class
        {
            T contract = Create<T, StrictPort>();
            ((StrictPort)(object)contract).invoke = handler;
            return contract;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            invoke(targetMethod, args);
    }
}
