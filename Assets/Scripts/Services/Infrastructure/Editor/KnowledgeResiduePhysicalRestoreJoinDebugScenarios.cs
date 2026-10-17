#if UNITY_EDITOR
using System;
using DungeonStory.Foundation;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class KnowledgeResiduePhysicalRestoreJoinDebugScenarios
{
    private const string OperationId =
        "research-knowledge-residue-sink:knowledge-00001";
    private const string ReasonCode = "memory-residue-research-consumed";
    private const string SourceStackId = "stack-residue-0001";

    [MenuItem(
        "DungeonStory/QA/V27/Run Knowledge Residue Physical Restore Join")]
    public static void RunFromMenu()
    {
        string details = RunAll();
        Debug.Log(details);
    }

    public static string RunAll()
    {
        PhysicalItemRestoreCandidateDispositionSnapshot receipt = CreateReceipt();

        KnowledgeResidueTaskSaveData torn = CreateTask();
        RequireThrows(() =>
            BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
                new[] { torn },
                new CandidateQuery(receipt)));
        Require(
            torn.dispositionPhase ==
                KnowledgeResidueDispositionPhase.AwaitingInput
            && string.IsNullOrEmpty(torn.sinkCommitId)
            && torn.completionOwnerRevision == 0L
            && string.IsNullOrEmpty(torn.completionPublicationKey)
            && torn.completionFact.IsEmpty,
            "torn research capture was mutated while rejecting a pending debit");

        KnowledgeResidueTaskSaveData waiting = CreateTask();
        BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            new[] { waiting },
            new CandidateQuery());
        Require(
            waiting.dispositionPhase ==
                KnowledgeResidueDispositionPhase.AwaitingInput
            && string.IsNullOrEmpty(waiting.sinkCommitId)
            && waiting.completionOwnerRevision == 0L
            && string.IsNullOrEmpty(waiting.completionPublicationKey)
            && waiting.completionFact.IsEmpty,
            "uncommitted task changed without a physical receipt");

        KnowledgeResidueTaskSaveData mismatch = CreateAppliedTask(receipt);
        PhysicalItemBatchDispositionSaveData mismatchedPending =
            CreateJointPending(receipt);
        mismatchedPending.requestFingerprint = "request-mismatch";
        RequireThrows(() =>
            BlueprintResearchSaveSection
                .ReconcileKnowledgeResiduePhysicalCandidate(
                    new[] { mismatch },
                    new CandidateQuery(
                        new PhysicalItemRestoreCandidateDispositionSnapshot(
                            mismatchedPending))));

        RequireThrows(() =>
            BlueprintResearchSaveSection
                .ReconcileKnowledgeResiduePhysicalCandidate(
                    Array.Empty<KnowledgeResidueTaskSaveData>(),
                    new CandidateQuery(receipt)));

        VerifyJointCompletionIdentity(receipt);
        VerifyDurableSaveJoin(receipt);
        return "[PASS] knowledge residue physical restore join: "
            + "torn-state rejection, exact replay, mismatch rejection, "
            + "orphan rejection, joint completion identity, durable research/physical/ledger join";
    }

    private static void VerifyJointCompletionIdentity(
        PhysicalItemRestoreCandidateDispositionSnapshot receipt)
    {
        KnowledgeResidueTaskSaveData task = CreateAppliedTask(receipt);
        PhysicalItemBatchDispositionSaveData pending =
            CreateJointPending(receipt);
        void Check() => BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            new[] { task }, new CandidateQuery(new PhysicalItemRestoreCandidateDispositionSnapshot(pending)));
        Check();
        pending.publicationKey = CompletionKey(42);
        RequireThrows(Check);
        pending.publicationKey = new GameplayResultKey(
            "items.disposition",
            new GameplayOperationId(OperationId),
            41,
            0).ToString();
        RequireThrows(Check);
        pending.publicationKey = new GameplayResultKey(
            KnowledgeCompletionOutcomeIds.ProducerId,
            new GameplayOperationId("other-operation"),
            41,
            0).ToString();
        RequireThrows(Check);
        pending.publicationKey = CompletionKey(41);
        task.completionOwnerRevision = 0;
        RequireThrows(Check);
        task.completionOwnerRevision = 41;
        KnowledgeCompletionFactData completionFact = task.completionFact;
        task.completionFact = new KnowledgeCompletionFactData();
        RequireInvalidPayload(Check);
        task.completionFact = completionFact;

        KnowledgeResidueTaskSaveData awaiting = CreateTask();
        RequireThrows(() => BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            new[] { awaiting },
            new CandidateQuery(
                new PhysicalItemRestoreCandidateDispositionSnapshot(pending))));
        RequireThrows(() => BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            new[] { task }, new CandidateQuery()));
        Check();
    }

    private static void VerifyDurableSaveJoin(
        PhysicalItemRestoreCandidateDispositionSnapshot receipt)
    {
        KnowledgeResidueTaskSaveData task = CreateAppliedTask(receipt);
        DungeonResearchSaveData research = new()
        {
            nextKnowledgeTaskSequence = 2,
            knowledgeTaskIdentityGeneration =
                KnowledgeResidueTaskIdentity.OriginalGeneration,
            knowledgeTasks = new List<KnowledgeResidueTaskSaveData> { task }
        };
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new KnowledgeCompletionOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new KnowledgeCompletionOutcomeAdapter()
            });
        var outcomeLedger = new GameplayOutcomeLedger(
            registry,
            GameplayOutcomeBufferLimits.Default,
            new GameplayOutcomeRunId("run:knowledge-restore-join"),
            1L);
        var recorder = new GameplayOutcomeRecorder(
            outcomeLedger,
            registry,
            new GameEventBus());
        KnowledgeCompletionOutcomeReceipt outcome =
            KnowledgeCompletionFactObservation.Capture(
                KnowledgeResidueCompletionTransaction.RestoreFact(task));
        Require(recorder.TryPrepare(
                outcome,
                out PreparedOutcomeToken prepared).Success,
            "knowledge completion canonical fixture did not prepare");
        Require(recorder.CommitPrepared(
                prepared,
                41L,
                out CommittedOutcomeToken committed).Success,
            "knowledge completion canonical fixture did not commit");
        Require(recorder.TryDeliver(committed).Published
                && recorder.Acknowledge(committed).Success,
            "knowledge completion canonical fixture did not deliver");
        GameplayOutcomeLedgerSaveData ledger =
            outcomeLedger.CaptureGameplayOutcomes();
        GameplayOutcomeSnapshot delivered = ledger.exactOutcomes.Single(value =>
            value != null
            && value.producerId == outcome.ResultKey.ProducerId
            && value.operationId == outcome.ResultKey.OperationId.Value
            && value.commitRevision == outcome.ResultKey.CommitRevision
            && value.localResultIndex == outcome.ResultKey.LocalResultIndex);

        DungeonPhysicalItemSaveData physical = new();
        PhysicalItemBatchDispositionSaveData pending =
            CreateJointPending(receipt);
        pending.publicationId = committed.OutcomeId.ToString();
        pending.publicationFingerprint = delivered.immutablePayloadHash;
        pending.publicationDelivered = true;
        physical.pendingBatchDispositions.Add(pending);

        DungeonGameRestoreReport accepted = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            ledger,
            accepted);
        Require(accepted.Success,
            "matching durable knowledge completion join was rejected");

        string publicationId = pending.publicationId;
        pending.publicationId = new GameplayOutcomeId(
            new GameplayOutcomeRunId("run:knowledge-restore-join"),
            2).ToString();
        DungeonGameRestoreReport mismatchedId = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            ledger,
            mismatchedId);
        Require(!mismatchedId.Success,
            "mismatched durable knowledge publication ID was accepted");
        pending.publicationId = publicationId;

        string publicationFingerprint = pending.publicationFingerprint;
        pending.publicationFingerprint = new string('b', 64);
        DungeonGameRestoreReport mismatchedFingerprint = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            ledger,
            mismatchedFingerprint);
        Require(!mismatchedFingerprint.Success,
            "mismatched durable knowledge publication fingerprint was accepted");
        pending.publicationFingerprint = publicationFingerprint;

        ledger.exactOutcomes.Clear();
        ledger.outbox.Add(delivered);
        DungeonGameRestoreReport falseDelivery = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            ledger,
            falseDelivery);
        Require(!falseDelivery.Success,
            "delivered durable knowledge proof was accepted from the pending outbox");
        pending.publicationDelivered = false;
        DungeonGameRestoreReport pendingDelivery = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            ledger,
            pendingDelivery);
        Require(pendingDelivery.Success,
            "pending durable knowledge proof was rejected from the pending outbox");
        ledger.outbox.Clear();
        ledger.exactOutcomes.Add(delivered);
        pending.publicationDelivered = true;

        task.completionOwnerRevision = 0;
        DungeonGameRestoreReport tornAccepted = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            ledger,
            tornAccepted);
        Require(!tornAccepted.Success,
            "torn research owner was accepted for implicit hydration");
        task.completionOwnerRevision = 41;
        pending.publicationId = string.Empty;
        pending.publicationFingerprint = string.Empty;
        pending.publicationDelivered = false;
        DungeonGameRestoreReport neutralPending = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            new GameplayOutcomeLedgerSaveData(),
            neutralPending);
        Require(neutralPending.Success,
            "unresolved knowledge publication without claimed proof was rejected");
        pending.publicationId = publicationId;
        pending.publicationFingerprint = publicationFingerprint;
        pending.publicationDelivered = true;

        DungeonGameRestoreReport missingLedger = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            new GameplayOutcomeLedgerSaveData(),
            missingLedger);
        Require(!missingLedger.Success,
            "missing canonical knowledge completion was accepted");

        DungeonGameRestoreReport orphan = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            new DungeonResearchSaveData(),
            physical,
            ledger,
            orphan);
        Require(!orphan.Success,
            "physical completion without its research owner was accepted");
    }

    private static PhysicalItemBatchDispositionSaveData CreateJointPending(
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) => new()
    {
        kind = (int)PhysicalItemDispositionKind.Sink,
        operationId = OperationId,
        reasonCode = ReasonCode,
        requestFingerprint = receipt.RequestFingerprint,
        sourceStackIds = receipt.SourceStackIds.ToList(),
        quantity = 1,
        inputMassGrams = 200,
        commitId = receipt.CommitId,
        ownerRevision = 41,
        publicationExpected = true,
        publicationKey = CompletionKey(41),
        publicationId = string.Empty,
        publicationFingerprint = string.Empty,
        publicationDelivered = false,
        defaultObservationDay = 0,
        sourceFacts = new List<PhysicalItemDispositionSourceFactSaveData>
        {
            new()
            {
                stackId = SourceStackId,
                itemDefinitionId =
                    KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                itemInstanceId = string.Empty,
                quantity = 1,
                massGrams = 200L,
                sourceX = 4,
                sourceY = 7,
                displayText = "Memory residue"
            }
        }
    };

    private static string CompletionKey(long revision) =>
        new GameplayResultKey(
            KnowledgeCompletionOutcomeIds.ProducerId,
            new GameplayOperationId(OperationId),
            revision,
            0).ToString();

    private static KnowledgeResidueTaskSaveData CreateTask() => new()
    {
        taskId = "knowledge-00001",
        use = KnowledgeResidueUse.CodexAnalysis,
        requiredWork = 24f,
        completedWork = 24f,
        facilityId = 101,
        facilityX = 4,
        facilityY = 7,
        assignmentSequence = 1,
        destinationId =
            "facility-input:exact:research.knowledge-residue:"
            + "knowledge-00001:00000001",
        facilityInstanceId = "facility-research-0001",
        inputCapacityGrams = 200L,
        massAuthorityRevision = 1L,
        inputCapacityFingerprint = "capacity-fingerprint",
        dispositionPhase = KnowledgeResidueDispositionPhase.AwaitingInput,
        sinkOperationId = OperationId,
        sinkReasonCode = ReasonCode,
        codexCluePayload = "deterministic-clue"
    };

    private static KnowledgeResidueTaskSaveData CreateAppliedTask(
        PhysicalItemRestoreCandidateDispositionSnapshot receipt)
    {
        KnowledgeResidueTaskSaveData task = CreateTask();
        task.sinkRequestFingerprint = receipt.RequestFingerprint;
        task.sinkSourceStackIds = receipt.SourceStackIds.ToList();
        task.sinkInputMassGrams = receipt.InputMassGrams;
        task.sinkCommitId = receipt.CommitId;
        task.dispositionPhase = KnowledgeResidueDispositionPhase.RewardsApplied;
        task.completionOwnerRevision = 41L;
        task.completionPublicationKey = CompletionKey(41L);
        task.completionFact = new KnowledgeResidueCompletedFact(
            CreatePhysicalReceipt(),
            new KnowledgeCompletionExperienceData
            {
                taskId = task.taskId,
                facilityId = task.facilityInstanceId,
                facilityName = "Research facility",
                regionId = string.Empty,
                regionName = string.Empty,
                codexClue = task.codexCluePayload,
                use = task.use,
                absoluteDay = 9,
                rewardBefore = 0f,
                rewardAfter = 1f
            }).Snapshot();
        return task;
    }

    private static PhysicalItemBatchDispositionReceipt CreatePhysicalReceipt()
    {
        var source = new PhysicalItemDispositionSourceFact(
            SourceStackId,
            KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
            string.Empty,
            1,
            200L,
            new Vector2Int(4, 7),
            "Memory residue");
        return new PhysicalItemBatchDispositionReceipt(
            PhysicalItemDispositionKind.Sink,
            OperationId,
            ReasonCode,
            $"{(int)PhysicalItemDispositionKind.Sink}:{ReasonCode}:"
                + SourceStackId + "=1",
            new[] { SourceStackId },
            1,
            200L,
            41L,
            new[] { source });
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot
        CreateReceipt()
    {
        PhysicalItemBatchDispositionReceipt receipt = CreatePhysicalReceipt();
        return new PhysicalItemRestoreCandidateDispositionSnapshot(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            receipt.RequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected the restore join to reject an invalid candidate.");
    }

    private static void RequireInvalidPayload(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected the restore join to reject an invalid saved payload.");
    }

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> receipts;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
        {
            this.receipts = (receipts ?? Array.Empty<
                    PhysicalItemRestoreCandidateDispositionSnapshot>())
                .Where(value => value != null)
                .OrderBy(value => value.OperationId, StringComparer.Ordinal)
                .ToArray();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => receipts;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = receipts.SingleOrDefault(value => string.Equals(
                value.OperationId,
                operationId,
                StringComparison.Ordinal));
            return disposition != null;
        }
    }
}
#endif
