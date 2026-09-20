#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class KnowledgeResiduePhysicalRestoreJoinDebugScenarios
{
    private const string OperationId =
        "research-knowledge-residue-sink:knowledge-00001";
    private const string ReasonCode = "memory-residue-research-consumed";

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
        BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            new[] { torn },
            new CandidateQuery(receipt));
        Require(
            torn.dispositionPhase ==
                KnowledgeResidueDispositionPhase.InputCommitted
            && string.Equals(
                torn.sinkRequestFingerprint,
                receipt.RequestFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                torn.sinkCommitId,
                receipt.CommitId,
                StringComparison.Ordinal)
            && torn.sinkInputMassGrams == 200L
            && torn.sinkSourceStackIds.SequenceEqual(
                receipt.SourceStackIds,
                StringComparer.Ordinal),
            "torn research capture was not hydrated from the physical receipt");

        BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            new[] { torn },
            new CandidateQuery(receipt));

        KnowledgeResidueTaskSaveData waiting = CreateTask();
        BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            new[] { waiting },
            new CandidateQuery());
        Require(
            waiting.dispositionPhase ==
                KnowledgeResidueDispositionPhase.AwaitingInput
            && string.IsNullOrEmpty(waiting.sinkCommitId),
            "uncommitted task changed without a physical receipt");

        KnowledgeResidueTaskSaveData mismatch = CreateTask();
        RequireThrows(() =>
            BlueprintResearchSaveSection
                .ReconcileKnowledgeResiduePhysicalCandidate(
                    new[] { mismatch },
                    new CandidateQuery(new
                        PhysicalItemRestoreCandidateDispositionSnapshot(
                            PhysicalItemDispositionKind.Sink,
                            OperationId,
                            ReasonCode,
                            "request-mismatch",
                            new[] { "stack-residue-0001" },
                            1,
                            199L,
                            "commit-mismatch"))));

        RequireThrows(() =>
            BlueprintResearchSaveSection
                .ReconcileKnowledgeResiduePhysicalCandidate(
                    Array.Empty<KnowledgeResidueTaskSaveData>(),
                    new CandidateQuery(receipt)));

        VerifyJointCompletionIdentity(torn, receipt);
        VerifyDurableSaveJoin(receipt);
        return "[PASS] knowledge residue physical restore join: "
            + "torn-capture hydration, exact replay, mismatch rejection, "
            + "orphan rejection, joint completion identity, durable research/physical/ledger join";
    }

    private static void VerifyJointCompletionIdentity(KnowledgeResidueTaskSaveData task,
        PhysicalItemRestoreCandidateDispositionSnapshot legacyReceipt)
    {
        task.dispositionPhase = KnowledgeResidueDispositionPhase.OutcomePublished;
        task.completionOwnerRevision = 41;
        PhysicalItemBatchDispositionSaveData pending =
            CreateJointPending(legacyReceipt);
        void Check() => BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            new[] { task }, new CandidateQuery(new PhysicalItemRestoreCandidateDispositionSnapshot(pending)));
        Check();
        pending.expectedOutcomeCommitRevision = 42;
        RequireThrows(Check);
        pending.expectedOutcomeCommitRevision = 41;
        pending.expectedOutcomeProducerId = "items.disposition";
        RequireThrows(Check);
        pending.expectedOutcomeProducerId = KnowledgeCompletionOutcomeIds.ProducerId;
        pending.expectedOutcomeOperationId = "other-operation";
        RequireThrows(Check);
        pending.expectedOutcomeOperationId = OperationId;
        task.completionOwnerRevision = 0;
        Check();
        Require(task.completionOwnerRevision == 41
            && task.dispositionPhase ==
                KnowledgeResidueDispositionPhase.OutcomePublished,
            "torn research completion owner was not hydrated from its exact physical attachment");
        task.dispositionPhase = KnowledgeResidueDispositionPhase.AwaitingInput;
        task.completionOwnerRevision = 0;
        Check();
        Require(task.completionOwnerRevision == 41
            && task.dispositionPhase ==
                KnowledgeResidueDispositionPhase.OutcomePublished,
            "awaiting research capture was not advanced from its committed joint attachment");
        RequireThrows(() => BlueprintResearchSaveSection.ReconcileKnowledgeResiduePhysicalCandidate(
            new[] { task }, new CandidateQuery()));
        Check();
    }

    private static void VerifyDurableSaveJoin(
        PhysicalItemRestoreCandidateDispositionSnapshot receipt)
    {
        KnowledgeResidueTaskSaveData task = CreateTask();
        task.sinkRequestFingerprint = receipt.RequestFingerprint;
        task.sinkSourceStackIds = receipt.SourceStackIds.ToList();
        task.sinkInputMassGrams = receipt.InputMassGrams;
        task.sinkCommitId = receipt.CommitId;
        task.dispositionPhase =
            KnowledgeResidueDispositionPhase.OutcomePublished;
        task.completionOwnerRevision = 41;
        DungeonResearchSaveData research = new()
        {
            nextKnowledgeTaskSequence = 2,
            knowledgeTaskIdentityGeneration =
                KnowledgeResidueTaskIdentity.OriginalGeneration,
            knowledgeTasks = new List<KnowledgeResidueTaskSaveData> { task }
        };
        DungeonPhysicalItemSaveData physical = new();
        physical.pendingBatchDispositions.Add(CreateJointPending(receipt));
        GameplayOutcomeLedgerSaveData ledger = new();
        ledger.exactOutcomes.Add(new GameplayOutcomeSnapshot
        {
            runId = "run:knowledge-restore-join",
            sequence = 1,
            producerId = KnowledgeCompletionOutcomeIds.ProducerId,
            operationId = OperationId,
            commitRevision = 41,
            localResultIndex = 0,
            outcomeTypeId = KnowledgeCompletionOutcomeIds.Completed.Value,
            ownerRevision = 41
        });

        DungeonGameRestoreReport accepted = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            ledger,
            accepted);
        Require(accepted.Success,
            "matching durable knowledge completion join was rejected");

        task.completionOwnerRevision = 0;
        DungeonGameRestoreReport tornAccepted = new();
        ResearchOutcomeSavePreflight.ValidateKnowledgeCompletionJoin(
            research,
            physical,
            ledger,
            tornAccepted);
        Require(tornAccepted.Success,
            "recoverable torn research owner was rejected before hydration");
        task.completionOwnerRevision = 41;

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
        outcomeOwnerRevision = 41,
        gameplayOutcomeExpected = true,
        expectedOutcomeProducerId = KnowledgeCompletionOutcomeIds.ProducerId,
        expectedOutcomeOperationId = OperationId,
        expectedOutcomeCommitRevision = 41,
        gameplayOutcome = new PhysicalGameplayOutcomeAttachmentSaveData
        {
            producerId = KnowledgeCompletionOutcomeIds.ProducerId,
            operationId = OperationId,
            commitRevision = 41,
            localResultIndex = 0,
            outcomeRunId = "run:knowledge-restore-join",
            outcomeSequence = 1,
            replayState = (int)GameplayOutcomeReplayState.Committed,
            canonicalPayloadHash = new string('a', 64)
        }
    };

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

    private static PhysicalItemRestoreCandidateDispositionSnapshot
        CreateReceipt() => new(
        PhysicalItemDispositionKind.Sink,
        OperationId,
        ReasonCode,
        "request-fingerprint",
        new[] { "stack-residue-0001" },
        1,
        200L,
        "commit-fingerprint");

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
