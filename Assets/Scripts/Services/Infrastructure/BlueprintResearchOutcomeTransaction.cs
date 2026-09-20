using System;
using System.Globalization;
using System.Linq;

/// <summary>
/// Single-writer, no-callback research/shop/outbox boundary. Calculation and
/// serialization are detached; publication is rollback-capable until the exact
/// prepared receipt commits. Delivery/observers never rerun gameplay.
/// </summary>
public sealed class BlueprintResearchOutcomeTransaction
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;

    public BlueprintResearchOutcomeTransaction(
        IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    {
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public bool TryApply(
        BlueprintResearchState state,
        FacilityShopUnlockState shop,
        IFacilityShopCatalog catalog,
        ResearchProjectSO project,
        FacilityBlueprintSO blueprint,
        float work,
        int absoluteDay,
        string researcherId,
        string researcherName,
        string facilityId,
        string facilityName,
        out BlueprintResearchWorkResult result,
        out BlueprintResearchUnlockResult unlocks,
        ResearchEquipmentOutcomeEvidence equipment = null)
        => TryApplyCore(state, shop, catalog, project, blueprint, work, absoluteDay,
            researcherId, researcherName, facilityId, facilityName, null, false, out result, out unlocks, equipment);

    public bool TryApplyForbiddenLeap(
        BlueprintResearchState state, FacilityShopUnlockState shop, IFacilityShopCatalog catalog,
        ResearchProjectSO project, ExtremeTraitRuntime traits, CharacterActor researcher,
        ulong runSeed, float elapsedSeconds, int absoluteDay,
        out ExtremeRiskResolution resolution, out BlueprintResearchWorkResult result,
        out BlueprintResearchUnlockResult unlocks)
    {
        resolution = default;
        unlocks = default;
        if (project == null || traits == null || researcher == null
            || !traits.TryPrepareForbiddenResearchLeap(researcher, project.ProjectId.Value,
                runSeed, elapsedSeconds, out ForbiddenResearchLeapPreparation leap))
        {
            result = Failure(project, null, "research-leap-unavailable");
            return false;
        }
        if (!TryApplyCore(state, shop, catalog, project, null,
                leap.Resolution.ProgressDelta * project.RequiredWork, absoluteDay,
                leap.CharacterId, researcher.Identity.DisplayName, string.Empty, string.Empty,
                leap, false, out result, out unlocks))
            return false;
        resolution = leap.Resolution;
        return true;
    }

    public bool TryCompleteImmediately(
        BlueprintResearchState state, FacilityShopUnlockState shop, IFacilityShopCatalog catalog,
        ResearchProjectSO project, FacilityBlueprintSO blueprint, int absoluteDay,
        out BlueprintResearchWorkResult result, out BlueprintResearchUnlockResult unlocks)
        => TryApplyCore(state, shop, catalog, project, blueprint, 0, absoluteDay,
            string.Empty, string.Empty, string.Empty, string.Empty, null, true, out result, out unlocks);

    private bool TryApplyCore(
        BlueprintResearchState state, FacilityShopUnlockState shop, IFacilityShopCatalog catalog,
        ResearchProjectSO project, FacilityBlueprintSO blueprint, float work, int absoluteDay,
        string researcherId, string researcherName, string facilityId, string facilityName,
        ForbiddenResearchLeapPreparation leap, bool immediate,
        out BlueprintResearchWorkResult result, out BlueprintResearchUnlockResult unlocks,
        ResearchEquipmentOutcomeEvidence equipment = null)
    {
        if (state == null || shop == null || catalog == null)
            throw new ArgumentNullException("Research transaction owner dependencies are required.");
        result = default;
        unlocks = default;
        if (!float.IsFinite(work) || !immediate && leap == null && work <= 0f
            || project == null && blueprint == null)
        {
            result = Failure(project, blueprint, "research-work-invalid");
            return false;
        }
        if (project != null ? state.Projects.IsCompleted(project.ProjectId) : state.IsCompleted(blueprint))
        {
            result = Failure(project, blueprint, "research-already-completed");
            return false;
        }

        BlueprintResearchAggregateState beforeState = state.CaptureAggregateClone();
        FacilityShopRestoreCandidate beforeShop = shop.PrepareRestore(shop.Capture());
        BlueprintResearchState candidate = new();
        candidate.ReplaceAggregate(beforeState.DeepClone());
        FacilityShopUnlockState candidateShop = new();
        candidateShop.Restore(shop.Capture());
        float before, after, required;
        string projectId, projectName;
        if (project != null)
        {
            ResearchProjectProgressState progress = candidate.Projects.GetProgress(project.ProjectId);
            before = progress.Progress;
            if (immediate)
                progress.Restore(project.RequiredWork, project);
            else if (leap != null)
                progress.Restore(Math.Max(0f, before + work), project);
            else
                progress.Add(work, project);
            after = progress.Progress;
            required = project.RequiredWork;
            projectId = project.ProjectId.Value;
            projectName = project.DisplayName;
        }
        else
        {
            BlueprintResearchTask task = candidate.Tasks.FirstOrDefault(value =>
                value?.Blueprint != null && value.Blueprint.id == blueprint.id);
            if (task == null && !immediate)
            {
                result = Failure(null, blueprint, "research-task-missing");
                return false;
            }
            before = task?.Progress ?? 0f;
            required = ResearchProgressRules.ClampRequiredWork(blueprint.researchWorkRequired);
            if (immediate)
                after = required;
            else
            {
                task.AddProgress(work);
                after = task.Progress;
            }
            projectId = "blueprint:" + blueprint.id.ToString(CultureInfo.InvariantCulture);
            projectName = blueprint.DisplayName;
        }
        if (!immediate && leap == null && after <= before)
        {
            result = Failure(project, blueprint, "research-no-progress");
            return false;
        }
        bool completed = after >= required;
        BlueprintResearchUnlockResult candidateUnlocks = completed
            ? project != null
                ? BlueprintResearchService.ApplyCompletion(project, candidate, candidateShop, catalog)
                : BlueprintResearchService.ApplyCompletion(blueprint, candidate, candidateShop, catalog)
            : new BlueprintResearchUnlockResult(blueprint, Array.Empty<BlueprintUnlockRecord>());
        long sequence = checked(state.OutcomeSequence + 1);
        candidate.RestoreOutcomeSequence(sequence);
        GameplayResultKey key = new(
            "research.work",
            new GameplayOperationId("research-work:" + sequence.ToString(CultureInfo.InvariantCulture)),
            sequence, 0);
        ResearchWorkOutcomeReceipt receipt = new(
            key, absoluteDay, projectId, projectName, researcherId, researcherName,
            facilityId, facilityName, before, after, required, completed, candidateUnlocks.Unlocks,
            immediate ? ResearchWorkCause.ImmediateCompletion : leap != null
                ? ResearchWorkCause.ForbiddenLeap : ResearchWorkCause.Work,
            leap?.Resolution ?? default, leap?.AftermathUntilSeconds ?? 0f, equipment);
        // Allocate every owner replacement before reserving the bounded ledger page.
        BlueprintResearchAggregateState preparedState = candidate.CaptureAggregateClone();
        FacilityShopRestoreCandidate preparedShop = shop.PrepareRestore(candidateShop.Capture());
        OutcomePrepareResult preparation = recorder.TryPrepare(receipt, out PreparedOutcomeToken token);
        if (!preparation.Success)
        {
            result = Failure(project, blueprint,
                "research-outcome-prepare-" + preparation.Code + ":" + preparation.DetailCode,
                preparation.Code == OutcomePrepareCode.CapacityDeferred);
            return false;
        }

        if (leap != null && !leap.IsCurrent)
        {
            recorder.CancelPrepared(token);
            result = Failure(project, blueprint, "research-leap-preparation-stale");
            return false;
        }

        CommittedOutcomeToken committed = default;
        bool durable = false;
        string detail = string.Empty;
        try
        {
            // No events, external effects, await or user callbacks in this boundary.
            state.ReplaceAggregate(preparedState);
            shop.PublishRestore(preparedShop);
            if (leap != null && !leap.TryPublish())
                throw new InvalidOperationException("research-leap-preparation-stale");
            OutcomeCommitResult commit = recorder.CommitPrepared(token, sequence, out committed);
            durable = commit.Success;
            if (!durable)
                detail = "research-outcome-commit-" + commit.Code + ":" + commit.DetailCode;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            // A dependency can throw after committing. Only canonical identity,
            // never the mere fact of an exception, proves that gameplay must stay.
            durable = diagnostics.TryGetResultIdentity(key, out GameplayOutcomeReplayIdentity identity)
                && identity.State >= GameplayOutcomeReplayState.Committed;
            detail = "research-outcome-commit-exception:" + exception.GetType().Name;
            if (!durable)
            {
                state.ReplaceAggregate(beforeState);
                shop.PublishRestore(beforeShop);
                leap?.Rollback();
                recorder.CancelPrepared(token);
                throw;
            }
        }
        if (!durable)
        {
            state.ReplaceAggregate(beforeState);
            shop.PublishRestore(beforeShop);
            leap?.Rollback();
            recorder.CancelPrepared(token);
            result = Failure(project, blueprint, detail);
            return false;
        }

        // Post-commit faults retain the canonical payload and report pending status.
        detail = Deliver(committed, detail);
        unlocks = candidateUnlocks;
        string message = completed ? "연구 완료" : "연구 진행";
        if (detail.Length > 0) message += " (" + detail + ")";
        result = project != null
            ? BlueprintResearchWorkResult.ForProject(true, project, after - before, after, required, completed, message)
            : new BlueprintResearchWorkResult(true, blueprint, after - before, after, required, completed, message);
        return true;
    }

    // Knowledge work is committed separately from its later physical-input/reward
    // finalization. A full progress bar must not claim that those rewards committed.
    public bool TryApplyKnowledgeProgress(
        DungeonRuntimeAggregateRootStore root, float work, int absoluteDay,
        string taskLabel, string researcherId, string researcherName,
        string facilityId, string facilityName, ResearchEquipmentOutcomeEvidence equipment,
        out BlueprintResearchWorkResult result)
    {
        if (root == null) throw new ArgumentNullException(nameof(root));
        result = default;
        KnowledgeResidueAggregateState current = root.GetOrCreate(() => new KnowledgeResidueAggregateState());
        var task = current.FirstTask;
        if (!float.IsFinite(work) || work <= 0 || task == null
            || task.dispositionPhase != KnowledgeResidueDispositionPhase.AwaitingInput
            || !string.Equals(task.facilityInstanceId, facilityId, StringComparison.Ordinal)
            || task.completedWork + .001f >= task.requiredWork)
        {
            result = Failure(null, null, "knowledge-work-unavailable");
            return false;
        }
        BlueprintResearchState sequenceOwner = new(root);
        BlueprintResearchAggregateState beforeResearch = sequenceOwner.CaptureAggregateClone();
        BlueprintResearchAggregateState preparedResearch = beforeResearch.DeepClone();
        KnowledgeResidueAggregateState beforeKnowledge = current.DeepClone();
        KnowledgeResidueAggregateState preparedKnowledge = current.DeepClone();
        float before = task.completedWork;
        float after = Math.Clamp(before + work, 0, Math.Max(1, task.requiredWork));
        if (after <= before)
        {
            result = Failure(null, null, "knowledge-no-progress");
            return false;
        }
        preparedKnowledge.FirstTask.completedWork = after;
        long sequence = checked(beforeResearch.OutcomeSequence + 1);
        preparedResearch.OutcomeSequence = sequence;
        GameplayResultKey key = new("research.work",
            new GameplayOperationId("research-work:" + sequence.ToString(CultureInfo.InvariantCulture)), sequence, 0);
        ResearchWorkOutcomeReceipt receipt = new(key, absoluteDay, task.taskId, taskLabel,
            researcherId, researcherName, facilityId, facilityName, before, after, task.requiredWork,
            false, Array.Empty<BlueprintUnlockRecord>(), ResearchWorkCause.KnowledgeProgress,
            equipment: equipment);
        OutcomePrepareResult preparation = recorder.TryPrepare(receipt, out PreparedOutcomeToken token);
        if (!preparation.Success)
        {
            result = Failure(null, null, "knowledge-outcome-prepare-" + preparation.Code + ":" + preparation.DetailCode,
                preparation.Code == OutcomePrepareCode.CapacityDeferred);
            return false;
        }
        CommittedOutcomeToken committed = default;
        bool durable = false;
        string detail = string.Empty;
        try
        {
            root.PublishPreparedReplacements(preparedResearch, preparedKnowledge);
            OutcomeCommitResult commit = recorder.CommitPrepared(token, sequence, out committed);
            durable = commit.Success;
            if (!durable) detail = "knowledge-outcome-commit-" + commit.Code + ":" + commit.DetailCode;
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            durable = diagnostics.TryGetResultIdentity(key, out GameplayOutcomeReplayIdentity identity)
                && identity.State >= GameplayOutcomeReplayState.Committed;
            detail = "knowledge-outcome-commit-exception:" + exception.GetType().Name;
            if (!durable)
            {
                root.PublishPreparedReplacements(beforeResearch, beforeKnowledge);
                recorder.CancelPrepared(token);
                throw;
            }
        }
        if (!durable)
        {
            root.PublishPreparedReplacements(beforeResearch, beforeKnowledge);
            recorder.CancelPrepared(token);
            result = Failure(null, null, detail);
            return false;
        }
        detail = Deliver(committed, detail);
        result = new BlueprintResearchWorkResult(true, null, after - before, after, task.requiredWork,
            false, taskLabel + (detail.Length == 0 ? string.Empty : " (" + detail + ")"));
        return true;
    }

    private string Deliver(CommittedOutcomeToken committed, string detail)
    {
        if (committed.IsValid)
        {
            try
            {
                OutcomeDeliveryResult delivery = recorder.TryDeliver(committed);
                if (!delivery.Published)
                    detail = "research-outcome-delivery-pending:" + delivery.DetailCode;
                else if (!recorder.Acknowledge(committed).Success)
                    detail = "research-outcome-acknowledgement-pending";
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                detail = "research-outcome-delivery-pending:" + exception.GetType().Name;
            }
        }
        return detail;
    }

    private static BlueprintResearchWorkResult Failure(
        ResearchProjectSO project, FacilityBlueprintSO blueprint, string reason, bool capacityDeferred = false) => project != null
        ? BlueprintResearchWorkResult.ForProject(false, project, 0, 0, project.RequiredWork, false, reason, capacityDeferred)
        : new BlueprintResearchWorkResult(false, blueprint, 0, 0, 1, false, reason, capacityDeferred);

    private static bool IsRecoverable(Exception exception) => exception is not
        OutOfMemoryException and not StackOverflowException and not AccessViolationException;
}
