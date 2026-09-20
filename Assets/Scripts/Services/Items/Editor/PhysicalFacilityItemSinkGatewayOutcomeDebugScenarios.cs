using System;
using System.Linq;
using UnityEngine;

/// <summary>Focused real physical-batch coverage for the outcome-aware facility Sink bridge.</summary>
public static class PhysicalFacilityItemSinkGatewayOutcomeDebugScenarios
{
    private const string DestinationId = "facility-input:qa:outcome-aware-sink";

    public static string RunAll()
    {
        VerifyRejectedPreparationPreservesFacilityBuffer();
        VerifySuccessfulParticipantCommitsExactlyOnce();
        return "PASS PhysicalFacilityItemSinkGatewayOutcomeDebugScenarios (2 groups)";
    }

    private static void VerifyRejectedPreparationPreservesFacilityBuffer()
    {
        using Fixture fixture = new();
        string stackId = fixture.SeedFacilityBuffer();
        RejectingParticipant participant = new(fixture.Repository, stackId);

        bool committed = fixture.Gateway.TryCommitSinkPending(
            DestinationId,
            "item:buffer",
            1,
            "qa:facility-outcome-sink:reject",
            "qa-facility-outcome-sink",
            participant,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason);

        Require(
            !committed
            && !receipt.IsCommitted
            && failureReason == "qa-outcome-preparation-rejected"
            && participant.PrepareCalls == 1
            && participant.SawUnchangedSource
            && fixture.HasExactFacilityBuffer(stackId)
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "Rejected outcome preparation consumed or staged a facility-buffer source.");
    }

    private static void VerifySuccessfulParticipantCommitsExactlyOnce()
    {
        using Fixture fixture = new();
        string stackId = fixture.SeedFacilityBuffer();
        SuccessfulParticipant participant = new(fixture.Repository, stackId);

        Require(
            fixture.Gateway.TryCommitSinkPending(
                DestinationId,
                "item:buffer",
                1,
                "qa:facility-outcome-sink:success",
                "qa-facility-outcome-sink",
                participant,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string failureReason)
            && receipt.IsCommitted
            && receipt.InputMassGrams == 1_000L
            && receipt.SourceStackIds.SequenceEqual(new[] { stackId })
            && participant.PrepareCalls == 1
            && participant.SawUnchangedSource
            && participant.Prepared.CommitCalls == 1
            && !fixture.HasStack(stackId)
            && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1,
            "Outcome-aware facility Sink did not commit its exact physical source once: "
            + failureReason);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(
                "Physical facility outcome Sink: " + message);
    }

    private sealed class Fixture : IDisposable
    {
        internal Fixture()
        {
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService quantityReservations,
                out _,
                out _,
                out _);
            Repository = repository;
            IPhysicalItemBatchDispositionService dispositions =
                new PhysicalItemBatchDispositionService(
                    repository,
                    Items.MassQuery,
                    EditorNullItemMarkerPresenter.Instance,
                    quantityReservations,
                    Items.CatalogProvider,
                    null,
                    null,
                    null);
            Gateway = new PhysicalFacilityItemSinkGateway(
                new PhysicalStockQuery(repository, Items.CatalogProvider, Items.MassQuery),
                dispositions);
        }

        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal IOutcomeAwarePhysicalFacilityItemSinkGateway Gateway { get; }

        internal string SeedFacilityBuffer() =>
            WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                "item:buffer",
                1,
                WorldItemStackState.FacilityBuffer,
                DestinationId,
                position: new Vector2Int(6, 4));

        internal bool HasExactFacilityBuffer(string stackId) =>
            Items.GetAllStacks().Any(stack =>
                stack.StackId == stackId
                && stack.ItemId == "item:buffer"
                && stack.Quantity == 1
                && stack.State == WorldItemStackState.FacilityBuffer
                && stack.DestinationId == DestinationId);

        internal bool HasStack(string stackId) => Items.GetAllStacks()
            .Any(stack => stack.StackId == stackId);

        public void Dispose() => Items.Dispose();
    }

    private sealed class RejectingParticipant :
        IPhysicalItemBatchDispositionOutcomeParticipant
    {
        private readonly WorldItemRepository repository;
        private readonly string stackId;

        internal RejectingParticipant(
            WorldItemRepository repository,
            string stackId)
        {
            this.repository = repository;
            this.stackId = stackId;
        }

        internal int PrepareCalls { get; private set; }
        internal bool SawUnchangedSource { get; private set; }

        public bool TryPrepare(
            in PhysicalItemBatchDispositionReceipt exactReceipt,
            long expectedOwnerRevision,
            out IPreparedPhysicalItemGameplayOutcome prepared,
            out string failureReason)
        {
            PrepareCalls++;
            SawUnchangedSource = exactReceipt.IsCommitted
                && exactReceipt.InputMassGrams == 1_000L
                && exactReceipt.SourceStackIds.SequenceEqual(new[] { stackId })
                && repository.GetEditorTestQuantity(stackId) == 1
                && repository.GetEditorPendingBatchDispositionCount() == 0;
            prepared = null;
            failureReason = "qa-outcome-preparation-rejected";
            return false;
        }
    }

    private sealed class SuccessfulParticipant :
        IPhysicalItemBatchDispositionOutcomeParticipant
    {
        private readonly WorldItemRepository repository;
        private readonly string stackId;

        internal SuccessfulParticipant(
            WorldItemRepository repository,
            string stackId)
        {
            this.repository = repository;
            this.stackId = stackId;
        }

        internal int PrepareCalls { get; private set; }
        internal bool SawUnchangedSource { get; private set; }
        internal PreparedOutcome Prepared { get; private set; }

        public bool TryPrepare(
            in PhysicalItemBatchDispositionReceipt exactReceipt,
            long expectedOwnerRevision,
            out IPreparedPhysicalItemGameplayOutcome prepared,
            out string failureReason)
        {
            PrepareCalls++;
            SawUnchangedSource = exactReceipt.IsCommitted
                && exactReceipt.InputMassGrams == 1_000L
                && exactReceipt.SourceStackIds.SequenceEqual(new[] { stackId })
                && repository.GetEditorTestQuantity(stackId) == 1
                && repository.GetEditorPendingBatchDispositionCount() == 0;
            Prepared = new PreparedOutcome(
                new GameplayResultKey(
                    "qa.physical-facility-outcome-sink",
                    new GameplayOperationId(exactReceipt.OperationId),
                    expectedOwnerRevision,
                    0));
            prepared = Prepared;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class PreparedOutcome : IPreparedPhysicalItemGameplayOutcome
    {
        internal PreparedOutcome(GameplayResultKey resultKey) => ResultKey = resultKey;

        public GameplayResultKey ResultKey { get; }
        public bool IsCanonicalReplay => false;
        internal int CommitCalls { get; private set; }

        public bool TryCommit(
            long expectedOwnerRevision,
            out PhysicalGameplayOutcomeAttachment attachment,
            out bool canonicalCommitted,
            out string failureReason)
        {
            CommitCalls++;
            attachment = new PhysicalGameplayOutcomeAttachment(
                ResultKey,
                new GameplayOutcomeId(
                    new GameplayOutcomeRunId("run:qa-physical-facility-sink"),
                    1L),
                GameplayOutcomeReplayState.PublishedAcknowledged,
                "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
            canonicalCommitted = true;
            failureReason = string.Empty;
            return true;
        }

        public void Cancel()
        {
        }
    }
}
