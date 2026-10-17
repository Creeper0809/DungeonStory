using System;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>
/// Single-writer research/shop boundary. The owner publishes immutable domain
/// facts; observers alone own recording, identities and delivery. An ambiguous
/// publication retains applied state and its frozen fact, never reruns gameplay.
/// </summary>
public sealed class BlueprintResearchOutcomeTransaction
{
    private readonly IDomainFactStagingPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;

    public BlueprintResearchOutcomeTransaction(
        IDomainFactStagingPublisher facts,
        IDomainFactRecoveryPublisher recovery)
    {
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
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
        ResearchEquipmentUseSnapshot equipment = null)
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
        ResearchEquipmentUseSnapshot equipment = null)
    {
        if (state == null || shop == null || catalog == null)
            throw new ArgumentNullException("Research transaction owner dependencies are required.");
        result = default;
        unlocks = default;
        if (state.HasPendingPublication)
        {
            result = Failure(project, blueprint, "research-fact-publication-pending");
            return false;
        }
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
        ResearchWorkAppliedFact fact = CaptureFact(
            sequence, absoluteDay, project != null, blueprint?.id ?? project?.Blueprint?.id ?? -1,
            projectId, projectName, researcherId, researcherName,
            facilityId, facilityName, before, after, required, completed, candidateUnlocks.Unlocks,
            immediate ? ResearchWorkCause.ImmediateCompletion : leap != null
                ? ResearchWorkCause.ForbiddenLeap : ResearchWorkCause.Work,
            leap?.Resolution ?? default, leap?.AftermathUntilSeconds ?? 0f, equipment, Math.Abs(work));
        FacilityShopRestoreCandidate preparedShop = shop.PrepareRestore(candidateShop.Capture());
        if (!facts.TryStage(fact, out IDomainFactStage stage, out string preparationFailure))
        {
            result = Failure(project, blueprint, preparationFailure,
                preparationFailure.Contains("CapacityDeferred", StringComparison.Ordinal));
            return false;
        }
        if (stage.IsReplay || stage.OwnerRevision != sequence || leap != null && !leap.IsCurrent)
        {
            stage.Cancel();
            result = Failure(project, blueprint, "research-fact-preparation-stale");
            return false;
        }
        string key = stage.OperationKey;
        BlueprintResearchAggregateState preparedState;
        try
        {
            if (completed && project != null) candidate.RecordCompletedProjectResultKey(projectId, key);
            candidate.RestorePendingPublication(new ResearchWorkPendingPublication
            { active = true, publicationKey = key, fact = fact.Capture() });
            preparedState = candidate.CaptureAggregateClone();
        }
        catch { stage.Cancel(); throw; }
        // No callbacks inside owner publication. Definite owner failure remains reversible.
        try
        {
            state.ReplaceAggregate(preparedState);
            shop.PublishRestore(preparedShop);
            if (leap != null && !leap.TryPublish())
                throw new InvalidOperationException("research-leap-preparation-stale");
        }
        catch
        {
            state.ReplaceAggregate(beforeState);
            shop.PublishRestore(beforeShop);
            leap?.Rollback();
            stage.Cancel();
            throw;
        }
        DomainFactDelivery delivery = Commit(stage);
        if (!delivery.MayBeCommitted)
        {
            state.ReplaceAggregate(beforeState);
            shop.PublishRestore(beforeShop);
            leap?.Rollback();
            stage.Cancel();
            result = Failure(project, blueprint, delivery.Detail);
            return false;
        }
        bool pending = !IsConfirmed(delivery, key);
        if (!pending) state.RestorePendingPublication(null);
        unlocks = candidateUnlocks;
        string message = completed ? "연구 완료" : "연구 진행";
        string detail = DeliveryDetail(delivery);
        if (detail.Length > 0) message += " (" + detail + ")";
        result = project != null
            ? BlueprintResearchWorkResult.ForProject(true, project, after - before, after, required, completed, message,
                committedResultKey: key, publicationPending: pending)
            : new BlueprintResearchWorkResult(true, blueprint, after - before, after, required, completed, message,
                committedResultKey: key, publicationPending: pending);
        return true;
    }

    // Knowledge work is committed separately from its later physical-input/reward
    // finalization. A full progress bar must not claim that those rewards committed.
    public bool TryApplyKnowledgeProgress(
        DungeonRuntimeAggregateRootStore root, float work, int absoluteDay,
        string taskLabel, string researcherId, string researcherName,
        string facilityId, string facilityName, ResearchEquipmentUseSnapshot equipment,
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
        if (sequenceOwner.HasPendingPublication)
        {
            result = Failure(null, null, "research-fact-publication-pending");
            return false;
        }
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
        ResearchWorkAppliedFact fact = CaptureFact(sequence, absoluteDay, false, -1, task.taskId, taskLabel,
            researcherId, researcherName, facilityId, facilityName, before, after, task.requiredWork,
            false, Array.Empty<BlueprintUnlockRecord>(), ResearchWorkCause.KnowledgeProgress,
            default, 0, equipment, work);
        if (!facts.TryStage(fact, out IDomainFactStage stage, out string preparationFailure))
        {
            result = Failure(null, null, preparationFailure,
                preparationFailure.Contains("CapacityDeferred", StringComparison.Ordinal));
            return false;
        }
        if (stage.IsReplay || stage.OwnerRevision != sequence)
        {
            stage.Cancel();
            result = Failure(null, null, "research-fact-preparation-stale");
            return false;
        }
        string key = stage.OperationKey;
        preparedResearch.PendingPublication = new ResearchWorkPendingPublication
        { active = true, publicationKey = key, fact = fact.Capture() };
        try
        {
            root.PublishPreparedReplacements(preparedResearch, preparedKnowledge);
        }
        catch
        {
            root.PublishPreparedReplacements(beforeResearch, beforeKnowledge);
            stage.Cancel();
            throw;
        }
        DomainFactDelivery delivery = Commit(stage);
        if (!delivery.MayBeCommitted)
        {
            root.PublishPreparedReplacements(beforeResearch, beforeKnowledge);
            stage.Cancel();
            result = Failure(null, null, delivery.Detail);
            return false;
        }
        bool pending = !IsConfirmed(delivery, key);
        if (!pending) sequenceOwner.RestorePendingPublication(null);
        string detail = DeliveryDetail(delivery);
        result = new BlueprintResearchWorkResult(true, null, after - before, after, task.requiredWork,
            false, taskLabel + (detail.Length == 0 ? string.Empty : " (" + detail + ")"),
            committedResultKey: key, publicationPending: pending);
        return true;
    }

    public bool TryRecoverPending(BlueprintResearchState state, out ResearchWorkAppliedFact recovered,
        out string publicationKey, out string failure)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        recovered = null;
        publicationKey = string.Empty;
        failure = string.Empty;
        ResearchWorkPendingPublication pending = state.PendingPublication;
        if (pending == null) return true;
        ResearchWorkAppliedFact fact = new(pending.fact);
        if (fact.Revision != state.OutcomeSequence)
        {
            failure = "research-pending-revision-mismatch";
            return false;
        }
        var deliveries = new DomainFactDelivery[1];
        try
        {
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey, fact) },
                    deliveries, out failure) || !IsConfirmed(deliveries[0], pending.publicationKey))
            {
                if (string.IsNullOrEmpty(failure)) failure = deliveries[0].Detail;
                return false;
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        { failure = "research-fact-recovery-uncertain:" + exception.GetType().Name; return false; }
        // Caller emits post-commit gameplay notifications once; never recomputes work/unlocks/wear.
        state.RestorePendingPublication(null);
        recovered = fact;
        publicationKey = pending.publicationKey;
        return true;
    }

    private static DomainFactDelivery Commit(IDomainFactStage stage)
    {
        try { return stage.Commit(); }
        catch (Exception exception) when (IsRecoverable(exception))
        { return DomainFactDelivery.Uncertain("research-fact-commit-uncertain:" + exception.GetType().Name); }
    }

    private static bool IsConfirmed(DomainFactDelivery delivery, string key) =>
        delivery.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery
        && string.Equals(delivery.OperationKey, key, StringComparison.Ordinal);

    private static string DeliveryDetail(DomainFactDelivery delivery) => delivery.Phase switch
    {
        DomainFactDeliveryPhase.InDoubt => "research-fact-publication-pending:" + delivery.Detail,
        DomainFactDeliveryPhase.AcceptedPendingDelivery => "research-fact-delivery-pending:" + delivery.Detail,
        _ => delivery.Detail
    };

    private static ResearchWorkAppliedFact CaptureFact(long revision, int day, bool isProject, int blueprintId,
        string subjectId, string subjectName, string researcherId, string researcherName,
        string facilityId, string facilityName, float before, float after, float required, bool completed,
        System.Collections.Generic.IReadOnlyList<BlueprintUnlockRecord> unlocks, ResearchWorkCause cause,
        ExtremeRiskResolution leap, float aftermathUntil, ResearchEquipmentUseSnapshot equipment, float approvedWork) => new(new()
        {
            revision = revision, absoluteDay = day, isProject = isProject, blueprintId = blueprintId,
            subjectId = subjectId, subjectName = subjectName, researcherId = researcherId ?? string.Empty,
            researcherName = researcherName ?? string.Empty, facilityId = facilityId ?? string.Empty,
            facilityName = facilityName ?? string.Empty, before = before, after = after, required = required,
            completed = completed, cause = cause, leapOutcome = (int)leap.Outcome,
            leapPrimary = leap.PrimaryMultiplier, leapSecondary = leap.SecondaryMultiplier,
            leapDelta = leap.ProgressDelta, leapHash = leap.FixedRollHash, aftermathUntil = aftermathUntil,
            approvedWork = approvedWork, hasEquipment = equipment != null, equipment = equipment?.Capture(),
            unlocks = unlocks.Select(value => new ResearchUnlockFactData
            {
                typeId = value.UnlockTypeId, categoryLabel = value.CategoryLabel, valueId = value.ValueId,
                displayName = value.DisplayName, codexDetail = value.CodexDetail
            }).ToArray()
        });

    private static BlueprintResearchWorkResult Failure(
        ResearchProjectSO project, FacilityBlueprintSO blueprint, string reason, bool capacityDeferred = false) => project != null
        ? BlueprintResearchWorkResult.ForProject(false, project, 0, 0, project.RequiredWork, false, reason, capacityDeferred)
        : new BlueprintResearchWorkResult(false, blueprint, 0, 0, 1, false, reason, capacityDeferred);

    private static bool IsRecoverable(Exception exception) => exception is not
        OutOfMemoryException and not StackOverflowException and not AccessViolationException;
}
