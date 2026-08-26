#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class FactionRestitutionOutboxDebugScenarios
{
    private const string FactionId = "faction:qa-restitution";
    private const string ItemId = "material:lumber";

    [MenuItem("DungeonStory/Debug/Factions/Run Restitution Outbox Contracts")]
    public static void RunAll()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(catalog);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService batch = new(
            repository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance);
        FakeCampaign campaign = new(FactionId, grievance: 80);

        string sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            2,
            WorldItemStackState.Loose,
            position: new Vector2Int(7, 3));
        DungeonFactionState state = CreateFaction(betrayalScars: 2);
        string operationId = FactionRestitutionOutbox.FormatOperationId(
            FactionId,
            state.betrayalScars);
        Require(batch.TryCommitPending(
                new[] { new PhysicalItemTransformInput(sourceStackId, 1) },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                FactionRestitutionOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure),
            "Could not stage restitution transfer: " + commitFailure);
        FactionRestitutionOutbox.RecordPending(
            state,
            receipt,
            transferredPhysicalValue: 150,
            campaignGrievanceTarget: 50);

        DungeonFactionState roundTrip = JsonUtility.FromJson<DungeonFactionState>(
            JsonUtility.ToJson(state));
        Require(roundTrip != null
            && string.Equals(
                roundTrip.restitutionTransferOperationId,
                operationId,
                StringComparison.Ordinal)
            && roundTrip.restitutionTransferSourceStackIds.SequenceEqual(
                new[] { sourceStackId },
                StringComparer.Ordinal),
            "V3 faction payload did not preserve restitution provenance.");

        DungeonFactionState tampered = JsonUtility.FromJson<DungeonFactionState>(
            JsonUtility.ToJson(roundTrip));
        tampered.restitutionTransferCommitId += ":tampered";
        Require(!FactionRestitutionOutbox.TryFinalizePending(
                tampered,
                batch,
                campaign,
                campaign,
                Accept,
                out _)
            && !tampered.restitutionPaid
            && campaign.Grievance == 80
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Tampered restitution provenance mutated physical or campaign authority.");

        Require(FactionRestitutionOutbox.TryFinalizePending(
                roundTrip,
                batch,
                campaign,
                campaign,
                Accept,
                out string finalizeFailure),
            "Restitution outbox did not finalize: " + finalizeFailure);
        Require(roundTrip.restitutionPaid
            && roundTrip.restitutionTransferCompleted
            && campaign.Grievance == 50
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && repository.GetEditorTestQuantity(sourceStackId) == 1,
            "Restitution finalization did not preserve exact quantity and terminal state.");

        Require(FactionRestitutionOutbox.TryFinalizePending(
                roundTrip,
                batch,
                campaign,
                campaign,
                Accept,
                out string replayFailure)
            && campaign.ApplyCount == 1,
            "Terminal restitution replay was not idempotent: " + replayFailure);

        VerifyCampaignAlreadyAppliedRecovery(
            repository,
            batch,
            campaign);
        VerifyRecurringGoodwillOutbox(repository, batch);

        Debug.Log(
            "Faction disposition outbox contracts passed: "
            + "scar-unique restitution, sequence-unique recurring goodwill, "
            + "V3 provenance, receipt tamper rejection, exact physical "
            + "conservation, campaign target idempotency, terminal replay.");
    }

    private static void VerifyRecurringGoodwillOutbox(
        WorldItemRepository repository,
        PhysicalItemBatchDispositionService batch)
    {
        FakeCampaign campaign = new(FactionId, grievance: 0, rapport: 0);
        DungeonFactionState first = CreateFaction(betrayalScars: 0);
        string sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            3,
            WorldItemStackState.Loose,
            position: new Vector2Int(9, 3));

        string firstOperation =
            FactionGoodwillOutbox.FormatOperationId(FactionId, 1);
        Require(batch.TryCommitPending(
                new[] { new PhysicalItemTransformInput(sourceStackId, 1) },
                PhysicalItemDispositionKind.Transfer,
                firstOperation,
                FactionGoodwillOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt firstReceipt,
                out string firstCommitFailure),
            "Could not stage first goodwill transfer: " + firstCommitFailure);
        FactionGoodwillOutbox.RecordPending(
            first,
            sequence: 1,
            firstReceipt,
            transferredPhysicalValue: 50,
            campaignRapportTarget: 5);

        DungeonFactionState roundTrip =
            JsonUtility.FromJson<DungeonFactionState>(JsonUtility.ToJson(first));
        Require(roundTrip != null
            && roundTrip.goodwillTransferSequence == 1
            && string.Equals(
                roundTrip.goodwillTransferOperationId,
                firstOperation,
                StringComparison.Ordinal),
            "V3 faction payload did not preserve goodwill provenance.");
        DungeonFactionState tampered =
            JsonUtility.FromJson<DungeonFactionState>(JsonUtility.ToJson(roundTrip));
        tampered.goodwillTransferCommitId += ":tampered";
        Require(!FactionGoodwillOutbox.TryFinalizePending(
                tampered,
                batch,
                campaign,
                campaign,
                AcceptGoodwill,
                out _,
                out _)
            && !tampered.discovered
            && campaign.Rapport == 0
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Tampered goodwill provenance mutated physical or campaign authority.");

        Require(FactionGoodwillOutbox.TryFinalizePending(
                roundTrip,
                batch,
                campaign,
                campaign,
                AcceptGoodwill,
                out bool firstApplied,
                out string firstFinalizeFailure)
            && firstApplied
            && roundTrip.discovered
            && campaign.Rapport == 5
            && repository.GetEditorTestQuantity(sourceStackId) == 2
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "First goodwill outbox did not finalize exactly: "
                + firstFinalizeFailure);
        FactionGoodwillOutbox.ClearCompleted(roundTrip);

        string secondOperation =
            FactionGoodwillOutbox.FormatOperationId(FactionId, 2);
        Require(!string.Equals(
                firstOperation,
                secondOperation,
                StringComparison.Ordinal),
            "Recurring same-day goodwill reused its operation ID.");
        Require(batch.TryCommitPending(
                new[] { new PhysicalItemTransformInput(sourceStackId, 1) },
                PhysicalItemDispositionKind.Transfer,
                secondOperation,
                FactionGoodwillOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt secondReceipt,
                out string secondCommitFailure),
            "Recurring same-day goodwill did not receive a unique operation: "
                + secondCommitFailure);
        FactionGoodwillOutbox.RecordPending(
            roundTrip,
            sequence: 2,
            secondReceipt,
            transferredPhysicalValue: 50,
            campaignRapportTarget: 10);
        int applyCountBefore = campaign.ApplyCount;
        campaign.ApplyFactionChange(
            FactionId,
            rapportDelta: 5,
            grievanceDelta: 0,
            obligationDelta: 0);
        Require(FactionGoodwillOutbox.TryFinalizePending(
                roundTrip,
                batch,
                campaign,
                campaign,
                AcceptGoodwill,
                out bool secondApplied,
                out string secondFinalizeFailure)
            && !secondApplied
            && campaign.Rapport == 10
            && campaign.ApplyCount == applyCountBefore + 1
            && repository.GetEditorTestQuantity(sourceStackId) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "Already-applied recurring goodwill was applied twice: "
                + secondFinalizeFailure);
        FactionGoodwillOutbox.ClearCompleted(roundTrip);
    }

    private static void VerifyCampaignAlreadyAppliedRecovery(
        WorldItemRepository repository,
        PhysicalItemBatchDispositionService batch,
        FakeCampaign campaign)
    {
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            1,
            WorldItemStackState.Loose,
            position: new Vector2Int(8, 3));
        DungeonFactionState state = CreateFaction(betrayalScars: 3);
        string operationId = FactionRestitutionOutbox.FormatOperationId(
            FactionId,
            state.betrayalScars);
        Require(batch.TryCommitPending(
                new[] { new PhysicalItemTransformInput(stackId, 1) },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                FactionRestitutionOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure),
            "Could not stage already-applied recovery: " + commitFailure);
        FactionRestitutionOutbox.RecordPending(
            state,
            receipt,
            transferredPhysicalValue: 150,
            campaignGrievanceTarget: 50);
        state.restitutionPaid = true;
        int applyCountBefore = campaign.ApplyCount;

        Require(FactionRestitutionOutbox.TryFinalizePending(
                state,
                batch,
                campaign,
                campaign,
                Accept,
                out string finalizeFailure)
            && state.restitutionTransferCompleted
            && campaign.ApplyCount == applyCountBefore
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "Already-applied campaign target was applied twice: "
                + finalizeFailure);
    }

    private static DungeonFactionState CreateFaction(int betrayalScars) => new()
    {
        factionId = FactionId,
        betrayalScars = betrayalScars,
        restitutionPaid = false,
        restitutionRequiredValue = 150
    };

    private static void Accept(DungeonFactionState state) =>
        state.restitutionPaid = true;

    private static void AcceptGoodwill(DungeonFactionState state) =>
        state.discovered = true;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakeCampaign :
        IFactionCampaignQuery,
        IFactionCampaignCommand
    {
        private readonly FactionCampaignStateSaveData state;

        internal FakeCampaign(
            string factionId,
            int grievance,
            int rapport = 0)
        {
            state = new FactionCampaignStateSaveData
            {
                factionId = factionId,
                grievance = grievance,
                rapport = rapport
            };
        }

        internal int Grievance => state.grievance;
        internal int Rapport => state.rapport;
        internal int ApplyCount { get; private set; }
        public System.Collections.Generic.IReadOnlyList<
            FactionCampaignStateSaveData> Factions => new[] { state };

        public bool TryGetFaction(
            string factionId,
            out FactionCampaignStateSaveData result)
        {
            result = string.Equals(
                factionId,
                state.factionId,
                StringComparison.Ordinal)
                ? state
                : null;
            return result != null;
        }

        public void ApplyFactionChange(
            string factionId,
            int rapportDelta,
            int grievanceDelta,
            int obligationDelta)
        {
            if (!string.Equals(
                    factionId,
                    state.factionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Unexpected faction campaign mutation.");
            }
            ApplyCount++;
            state.rapport = Math.Clamp(
                state.rapport + rapportDelta,
                -100,
                100);
            state.grievance = Math.Clamp(
                state.grievance + grievanceDelta,
                0,
                100);
            state.obligationTokens = Math.Clamp(
                state.obligationTokens + obligationDelta,
                0,
                5);
        }

        public bool TryResolveChapter(
            string factionId,
            string choiceId,
            RunMilestoneEvaluationSnapshot requirements,
            out V20ResolvedEventResult result,
            out string failure)
        {
            result = default;
            failure = "unsupported";
            return false;
        }

        public bool TryAcceptContract(
            string factionId,
            string contractId,
            int absoluteDay,
            out string failure)
        {
            failure = "unsupported";
            return false;
        }

        public bool TryResolveContract(
            string factionId,
            bool success,
            RunMilestoneEvaluationSnapshot requirements,
            out V20ResolvedEventResult result,
            out string failure)
        {
            result = default;
            failure = "unsupported";
            return false;
        }
    }
}
#endif
