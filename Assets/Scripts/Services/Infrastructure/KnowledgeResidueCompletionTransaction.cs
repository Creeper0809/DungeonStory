using System;
using System.Linq;

/// <summary>Reward/task/outbox publication inside the physical owner's rollback boundary.</summary>
public sealed class KnowledgeResidueCompletionTransaction
{
    private readonly IGameplayOutcomeRecorder recorder;
    private readonly IGameplayOutcomeDiagnosticsQuery diagnostics;
    public KnowledgeResidueCompletionTransaction(IGameplayOutcomeRecorder recorder,
        IGameplayOutcomeDiagnosticsQuery diagnostics)
    { this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
      this.diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics)); }

    public Participant Prepare(DungeonRuntimeAggregateRootStore root, KnowledgeResidueTaskSaveData task,
        CodexRuntime codex, IOffenseRegionRuntime regions, int day, string facilityName)
        => new(this, root, task, codex, regions, day, facilityName);

    public bool IsCommitted(KnowledgeResidueTaskSaveData task, in PhysicalItemBatchDispositionReceipt input)
    {
        if (task.completionOwnerRevision <= 0 || input.OwnerRevision != task.completionOwnerRevision) return false;
        var key = new GameplayResultKey(KnowledgeCompletionOutcomeIds.ProducerId,
            new GameplayOperationId(task.sinkOperationId), task.completionOwnerRevision, 0);
        return diagnostics.TryGetResultIdentity(key, out var identity)
            && identity.State >= GameplayOutcomeReplayState.Committed;
    }

    public sealed class Participant : IPhysicalItemBatchDispositionOutcomeParticipant, IPreparedPhysicalItemGameplayOutcome
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
        private PreparedOutcomeToken token;
        private bool prepared, published, canonical;
        internal Participant(KnowledgeResidueCompletionTransaction owner, DungeonRuntimeAggregateRootStore root,
            KnowledgeResidueTaskSaveData task, CodexRuntime codex, IOffenseRegionRuntime regions, int day, string facilityName)
        { this.owner = owner; this.root = root; this.task = task; this.codex = codex;
          this.regions = regions; this.day = day; this.facilityName = facilityName; }
        public GameplayResultKey ResultKey { get; private set; }
        public bool IsCanonicalReplay => canonical;

        public bool TryPrepare(in PhysicalItemBatchDispositionReceipt input, long expectedOwnerRevision,
            out IPreparedPhysicalItemGameplayOutcome result, out string reason)
        {
            result = null;
            reason = string.Empty;
            before = root.GetOrCreate(() => new KnowledgeResidueAggregateState());
            if (prepared || !ReferenceEquals(before.FirstTask, task)
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

            after = before.DeepClone();
            KnowledgeResidueProcessingRuntime.StoreCommittedReceipt(after.FirstTask, input);
            after.FirstTask.dispositionPhase = KnowledgeResidueDispositionPhase.OutcomePublished;
            after.FirstTask.completionOwnerRevision = expectedOwnerRevision;
            after.FirstTask.appliedReconnaissanceAmount = recon == null ? 0 : rewardAfter - rewardBefore;
            ResultKey = new GameplayResultKey(KnowledgeCompletionOutcomeIds.ProducerId,
                new GameplayOperationId(task.sinkOperationId), expectedOwnerRevision, 0);
            var receipt = new KnowledgeCompletionOutcomeReceipt(ResultKey, day, task.taskId,
                task.use == KnowledgeResidueUse.CodexAnalysis ? "도감 단서 분석" : "지역 기억 정찰",
                task.facilityInstanceId, facilityName, task.use, task.regionId, regionName,
                task.codexCluePayload, rewardBefore, rewardAfter, input);
            var preparation = owner.recorder.TryPrepare(receipt, out token);
            if (!preparation.Success)
            { reason = "knowledge-completion-prepare-" + preparation.Code + ":" + preparation.DetailCode; return false; }
            prepared = true;
            result = this;
            return true;
        }

        public bool TryCommit(long expectedOwnerRevision, out PhysicalGameplayOutcomeAttachment attachment,
            out bool canonicalCommitted, out string reason)
        {
            attachment = default;
            canonicalCommitted = false;
            reason = string.Empty;
            if (!prepared || published || expectedOwnerRevision != ResultKey.CommitRevision
                || !ReferenceEquals(root.GetOrCreate(() => new KnowledgeResidueAggregateState()), before))
            { reason = "knowledge-completion-commit-stale"; return false; }
            try
            {
                if (!(clue != null ? clue.TryPublish() : recon.TryPublish()))
                { reason = "knowledge-reward-preparation-stale"; Cancel(); return false; }
                published = true;
                root.Replace(after);
                var commit = owner.recorder.CommitPrepared(token, expectedOwnerRevision, out _);
                canonical = commit.Success;
                if (!canonical) reason = "knowledge-completion-commit-" + commit.Code;
            }
            catch (Exception exception) when (Recoverable(exception))
            {
                canonical = owner.diagnostics.TryGetResultIdentity(ResultKey, out var identity)
                    && identity.State >= GameplayOutcomeReplayState.Committed;
                reason = "knowledge-completion-commit-exception:" + exception.GetType().Name;
            }
            canonicalCommitted = canonical;
            if (!canonical) { Cancel(); return false; }
            if (!owner.diagnostics.TryGetResultIdentity(ResultKey, out var committedIdentity))
            { reason = "knowledge-completion-identity-pending"; return false; }
            attachment = new PhysicalGameplayOutcomeAttachment(ResultKey, committedIdentity.OutcomeId,
                committedIdentity.State, committedIdentity.CanonicalPayloadHash);
            return attachment.IsValid;
        }

        public void Cancel()
        {
            if (canonical) return;
            if (published)
            {
                root.Replace(before);
                clue?.Rollback();
                recon?.Rollback();
                published = false;
            }
            if (prepared) owner.recorder.CancelPrepared(token);
            prepared = false;
        }

        public void NotifyCommitted()
        {
            if (canonical) clue?.NotifyCommitted();
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
                || task.dispositionPhase != KnowledgeResidueDispositionPhase.OutcomePublished)
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
