using System;
using DungeonStory.Foundation.Facts;

/// <summary>Owns knowledge rewards and tasks within the physical debit boundary.
/// Observers alone record facts. Uncertain publication preserves applied effects
/// and the frozen fact; recovery never consumes input or grants rewards.</summary>
public sealed class KnowledgeResidueCompletionTransaction
{
    private readonly IDomainFactStagingPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;

    public KnowledgeResidueCompletionTransaction(IDomainFactStagingPublisher facts,
        IDomainFactRecoveryPublisher recovery)
    {
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }

    public Participant Prepare(DungeonRuntimeAggregateRootStore root, KnowledgeResidueTaskSaveData task,
        CodexRuntime codex, IOffenseRegionRuntime regions, int day, string facilityName)
        => new(this, root, task, codex, regions, day, facilityName);

    public bool TryDeliver(KnowledgeResidueTaskSaveData task,
        in PhysicalItemBatchDispositionReceipt input, out string reason)
    {
        reason = string.Empty;
        try
        {
            KnowledgeResidueCompletedFact fact = RestoreFact(task);
            if (!KnowledgeResidueProcessingRuntime.ReceiptMatches(task, input)
                || input.OwnerRevision != task.completionOwnerRevision)
            { reason = "knowledge-completion-physical-mismatch"; return false; }
            var results = new DomainFactDelivery[1];
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(task.completionPublicationKey, fact) },
                    results, out reason)) return false;
            DomainFactDelivery delivery = results[0];
            if (!delivery.Completed || delivery.OperationKey != task.completionPublicationKey
                || string.IsNullOrEmpty(delivery.PublicationId) || string.IsNullOrEmpty(delivery.Fingerprint))
            { reason = "knowledge-completion-delivery-pending:" + delivery.Detail; return false; }
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { reason = "knowledge-completion-recovery:" + exception.Message; return false; }
    }

    // Domain-only join. Canonical recording proof belongs to the observer preflight.
    internal static KnowledgeResidueCompletedFact RestoreFact(KnowledgeResidueTaskSaveData task)
    {
        if (task == null || task.dispositionPhase != KnowledgeResidueDispositionPhase.RewardsApplied
            || task.completionOwnerRevision <= 0 || string.IsNullOrWhiteSpace(task.completionPublicationKey)
            || task.completionPublicationKey != task.completionPublicationKey.Trim()
            || task.completedWork < task.requiredWork)
            throw new InvalidOperationException("knowledge-completion-owner-fact-missing");
        KnowledgeResidueCompletedFact fact = KnowledgeResidueCompletedFact.FromSave(task.completionFact);
        var experience = fact.Experience;
        if (experience.taskId != task.taskId || experience.facilityId != task.facilityInstanceId
            || experience.use != task.use || experience.regionId != task.regionId
            || experience.codexClue != task.codexCluePayload
            || fact.Physical.Receipt.OwnerRevision != task.completionOwnerRevision
            || !KnowledgeResidueProcessingRuntime.ReceiptMatches(task, fact.Physical.Receipt)
            || task.appliedReconnaissanceAmount != (task.use == KnowledgeResidueUse.RegionReconnaissance
                ? experience.rewardAfter - experience.rewardBefore : 0))
            throw new InvalidOperationException("knowledge-completion-owner-fact-mismatch");
        return fact;
    }

    public sealed class Participant : IPhysicalItemDispositionEffect, IDomainFactStage
    {
        private readonly KnowledgeResidueCompletionTransaction owner;
        private readonly DungeonRuntimeAggregateRootStore root;
        private readonly KnowledgeResidueTaskSaveData task;
        private readonly CodexRuntime codex;
        private readonly IOffenseRegionRuntime regions;
        private readonly int day;
        private readonly string facilityName;
        private KnowledgeResidueAggregateState before, after;
        private CodexRuntime.MemoryResidueCluePreparation clue;
        private OffenseRegionRuntime.ReconnaissancePreparation recon;
        private IDomainFactStage observation;
        private bool published, commitAttempted, notificationAttempted, closed;

        internal Participant(KnowledgeResidueCompletionTransaction owner, DungeonRuntimeAggregateRootStore root,
            KnowledgeResidueTaskSaveData task, CodexRuntime codex, IOffenseRegionRuntime regions, int day, string facilityName)
        { this.owner = owner; this.root = root; this.task = task; this.codex = codex;
          this.regions = regions; this.day = day; this.facilityName = facilityName; }

        public string OperationKey => observation?.OperationKey ?? string.Empty;
        public long OwnerRevision => observation?.OwnerRevision ?? 0;
        public bool IsReplay => false;
        public bool IsActive => !closed && observation?.IsActive == true;

        public bool TryStage(in PhysicalItemBatchDispositionReceipt input, long expectedOwnerRevision,
            out IDomainFactStage stage, out string reason)
        {
            stage = null;
            reason = string.Empty;
            before = root.GetOrCreate(() => new KnowledgeResidueAggregateState());
            if (observation != null || closed || !ReferenceEquals(before.FirstTask, task)
                || task.dispositionPhase != KnowledgeResidueDispositionPhase.AwaitingInput
                || task.completedWork < task.requiredWork || input.OperationId != task.sinkOperationId
                || input.OwnerRevision != expectedOwnerRevision)
            { reason = "knowledge-completion-preparation-stale"; return false; }
            float rewardBefore, rewardAfter;
            string regionName = string.Empty;
            if (task.use == KnowledgeResidueUse.CodexAnalysis)
            {
                if (!codex.TryPrepareMemoryResidueClue(task.codexCluePayload, out clue, out reason)) return false;
                rewardBefore = clue.Before;
                rewardAfter = clue.After;
            }
            else if (task.use == KnowledgeResidueUse.RegionReconnaissance
                && regions.TryPrepareReconnaissance(task.regionId, 10, out recon))
            {
                rewardBefore = recon.Before;
                rewardAfter = recon.After;
                regionName = recon.RegionName;
            }
            else { reason = "knowledge-reward-preparation-rejected"; return false; }

            var fact = new KnowledgeResidueCompletedFact(input, new KnowledgeCompletionExperienceData
            {
                taskId = task.taskId, facilityId = task.facilityInstanceId, facilityName = facilityName,
                use = task.use, regionId = task.regionId, regionName = regionName,
                codexClue = task.codexCluePayload, absoluteDay = day,
                rewardBefore = rewardBefore, rewardAfter = rewardAfter
            });
            if (!owner.facts.TryStage(fact, out observation, out reason)) return false;
            if (observation.IsReplay || observation.OwnerRevision != expectedOwnerRevision
                || string.IsNullOrEmpty(observation.OperationKey))
            { Cancel(); reason = "knowledge-completion-observation-stale"; return false; }
            try
            {
                after = before.DeepClone();
                KnowledgeResidueProcessingRuntime.StoreCommittedReceipt(after.FirstTask, input);
                after.FirstTask.dispositionPhase = KnowledgeResidueDispositionPhase.RewardsApplied;
                after.FirstTask.completionOwnerRevision = expectedOwnerRevision;
                after.FirstTask.appliedReconnaissanceAmount = recon == null ? 0 : rewardAfter - rewardBefore;
                after.FirstTask.completionPublicationKey = observation.OperationKey;
                after.FirstTask.completionFact = fact.Snapshot();
            }
            catch { Cancel(); throw; }
            stage = this;
            return true;
        }

        public DomainFactDelivery Commit()
        {
            if (commitAttempted)
                return DomainFactDelivery.Uncertain("knowledge-completion-commit-already-attempted");
            if (!IsActive || published || !ReferenceEquals(root.GetOrCreate(() => new KnowledgeResidueAggregateState()), before))
            { Cancel(); return DomainFactDelivery.Reject("knowledge-completion-commit-stale"); }
            try
            {
                if (!(clue != null ? clue.TryPublish() : recon.TryPublish()))
                { Cancel(); return DomainFactDelivery.Reject("knowledge-reward-preparation-stale"); }
                published = true;
                root.Replace(after);
            }
            catch (Exception exception) when (Recoverable(exception))
            {
                Cancel();
                return DomainFactDelivery.Reject("knowledge-reward-commit:" + exception.GetType().Name);
            }

            commitAttempted = true;
            DomainFactDelivery delivery;
            try { delivery = observation.Commit(); }
            catch (Exception exception) when (Recoverable(exception))
            {
                observation.Cancel();
                closed = true;
                return DomainFactDelivery.Uncertain("knowledge-completion-commit:" + exception.GetType().Name);
            }
            if (!delivery.MayBeCommitted)
            {
                commitAttempted = false;
                Cancel();
            }
            else closed = true;
            return delivery;
        }

        public void Cancel()
        {
            if (!commitAttempted && published)
            {
                var current = root.GetOrCreate(() => new KnowledgeResidueAggregateState());
                if (!ReferenceEquals(current, after) && !ReferenceEquals(current, before))
                    throw new InvalidOperationException("knowledge-completion-rollback-conflict");
                clue?.Rollback();
                recon?.Rollback();
                root.Replace(before);
                published = false;
            }
            observation?.Cancel();
            closed = true;
        }

        public void NotifyCommitted()
        {
            if (!published || notificationAttempted) return;
            notificationAttempted = true;
            clue?.NotifyCommitted();
        }
    }

    public static IPhysicalItemDispositionAcknowledgementParticipant PrepareCleanup(
        DungeonRuntimeAggregateRootStore root, KnowledgeResidueTaskSaveData task)
        => new Cleanup(root, task);

    private sealed class Cleanup : IPhysicalItemDispositionAcknowledgementParticipant
    {
        private readonly DungeonRuntimeAggregateRootStore root;
        private readonly KnowledgeResidueAggregateState before, after;
        private readonly KnowledgeResidueTaskSaveData task;
        private bool published;
        public Cleanup(DungeonRuntimeAggregateRootStore root, KnowledgeResidueTaskSaveData task)
        {
            this.root = root; this.task = task;
            before = root.GetOrCreate(() => new KnowledgeResidueAggregateState());
            after = before.DeepClone();
            after.RemoveFirstTask();
            after.ClearReadySignal();
        }
        public bool TryCommit(out string reason)
        {
            reason = string.Empty;
            if (published || !ReferenceEquals(root.GetOrCreate(() => new KnowledgeResidueAggregateState()), before)
                || !ReferenceEquals(before.FirstTask, task) || task.completionOwnerRevision <= 0
                || task.dispositionPhase != KnowledgeResidueDispositionPhase.RewardsApplied)
            { reason = "knowledge-cleanup-owner-stale"; return false; }
            root.Replace(after);
            published = true;
            return true;
        }
        public void Rollback()
        {
            if (!published) return;
            if (!ReferenceEquals(root.GetOrCreate(() => new KnowledgeResidueAggregateState()), after))
                throw new InvalidOperationException("knowledge-cleanup-rollback-conflict");
            root.Replace(before);
            published = false;
        }
    }
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        and not StackOverflowException and not AccessViolationException;
}
