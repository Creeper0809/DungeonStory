using System;
using System.Linq;
using DungeonStory.Factions;

public static class FactionRestitutionOutbox
{
    public const string TransferReason =
        "faction-restitution-goods-transfer";

    public static string FormatOperationId(
        string factionId,
        int betrayalScars) =>
        $"faction-restitution:{factionId}:scar:{betrayalScars:D8}";

    public static bool HasProvenance(DungeonFactionState faction) =>
        faction != null
        && !string.IsNullOrEmpty(faction.restitutionTransferOperationId);

    public static void RecordPending(
        DungeonFactionState faction,
        PhysicalItemBatchDispositionReceipt receipt,
        int transferredPhysicalValue,
        int campaignGrievanceTarget)
    {
        if (faction == null)
        {
            throw new ArgumentNullException(nameof(faction));
        }
        if (!receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(
                receipt.OperationId,
                FormatOperationId(faction.factionId, faction.betrayalScars),
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                TransferReason,
                StringComparison.Ordinal)
            || transferredPhysicalValue <= 0
            || campaignGrievanceTarget is < 0 or > 100)
        {
            throw new InvalidOperationException(
                "Faction restitution transfer receipt is not canonical.");
        }

        faction.restitutionTransferOperationId = receipt.OperationId;
        faction.restitutionTransferCommitId = receipt.CommitId;
        faction.restitutionTransferSourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        faction.restitutionTransferQuantity = receipt.Quantity;
        faction.restitutionTransferMassGrams = receipt.InputMassGrams;
        faction.restitutionTransferredPhysicalValue =
            transferredPhysicalValue;
        faction.restitutionCampaignGrievanceTarget =
            campaignGrievanceTarget;
        faction.restitutionTransferCompleted = false;
    }

    public static bool TryFinalizePending(
        DungeonFactionState faction,
        IPhysicalItemBatchDispositionService batchDispositions,
        IFactionCampaignQuery campaignQuery,
        IFactionCampaignCommand campaignCommand,
        Action<DungeonFactionState> acceptRestitution,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (faction == null
            || batchDispositions == null
            || campaignQuery == null
            || campaignCommand == null
            || acceptRestitution == null
            || !HasProvenance(faction)
            || faction.betrayalScars <= 0
            || !string.Equals(
                faction.restitutionTransferOperationId,
                FormatOperationId(faction.factionId, faction.betrayalScars),
                StringComparison.Ordinal)
            || faction.restitutionTransferSourceStackIds == null
            || faction.restitutionTransferSourceStackIds.Count == 0
            || faction.restitutionTransferQuantity <= 0
            || faction.restitutionTransferMassGrams <= 0L
            || faction.restitutionTransferredPhysicalValue <= 0
            || faction.restitutionCampaignGrievanceTarget is < 0 or > 100)
        {
            failureReason = "faction-restitution-outbox-invalid";
            return false;
        }

        bool pending = batchDispositions.TryGetPending(
            faction.restitutionTransferOperationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (pending && !ReceiptMatches(faction, receipt))
        {
            failureReason = "faction-restitution-outbox-receipt-mismatch";
            return false;
        }
        if (faction.restitutionTransferCompleted)
        {
            if (!faction.restitutionPaid)
            {
                failureReason =
                    "faction-restitution-outbox-terminal-domain-missing";
                return false;
            }
            return !pending || batchDispositions.Acknowledge(
                faction.restitutionTransferCommitId,
                out failureReason);
        }
        if (!pending)
        {
            failureReason = "faction-restitution-outbox-receipt-missing";
            return false;
        }

        if (!faction.restitutionPaid)
        {
            acceptRestitution(faction);
        }
        if (!faction.restitutionPaid)
        {
            failureReason = "faction-restitution-outbox-domain-not-applied";
            return false;
        }

        if (!campaignQuery.TryGetFaction(
                faction.factionId,
                out FactionCampaignStateSaveData campaignState)
            || campaignState == null)
        {
            failureReason = "faction-restitution-outbox-campaign-missing";
            return false;
        }
        int target = faction.restitutionCampaignGrievanceTarget;
        if (campaignState.grievance > target)
        {
            campaignCommand.ApplyFactionChange(
                faction.factionId,
                rapportDelta: 0,
                grievanceDelta: target - campaignState.grievance,
                obligationDelta: 0);
        }
        if (!campaignQuery.TryGetFaction(
                faction.factionId,
                out campaignState)
            || campaignState == null
            || campaignState.grievance != target)
        {
            failureReason = "faction-restitution-outbox-campaign-target-mismatch";
            return false;
        }

        faction.restitutionTransferCompleted = true;
        if (pending && !batchDispositions.Acknowledge(
                faction.restitutionTransferCommitId,
                out failureReason))
        {
            faction.restitutionTransferCompleted = false;
            return false;
        }
        return true;
    }

    private static bool ReceiptMatches(
        DungeonFactionState faction,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(
            receipt.OperationId,
            faction.restitutionTransferOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            TransferReason,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            faction.restitutionTransferCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == faction.restitutionTransferQuantity
        && receipt.InputMassGrams == faction.restitutionTransferMassGrams
        && receipt.SourceStackIds.OrderBy(value => value, StringComparer.Ordinal)
            .SequenceEqual(
                faction.restitutionTransferSourceStackIds,
                StringComparer.Ordinal);
}
