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

        return "[PASS] knowledge residue physical restore join: "
            + "torn-capture hydration, exact replay, mismatch rejection, "
            + "and orphan rejection";
    }

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
