using System;

public static class SurgicalOrganPreservationOutbox
{
    public const string ReasonCode = "organ-preservation-canister-consumed";
    public static string FormatOperationId(string partId) => $"surgical-organ-preservation:{partId}";
    public static bool HasPending(SurgicalPartInstance part) => part != null && part.preservationOperationId.Length > 0;

    public static void Record(SurgicalPartInstance part, PhysicalItemBatchDispositionReceipt receipt)
    {
        if (part == null || !receipt.IsCommitted || receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(receipt.OperationId, FormatOperationId(part.partInstanceId), StringComparison.Ordinal)
            || !string.Equals(receipt.ReasonCode, ReasonCode, StringComparison.Ordinal)
            || receipt.SourceStackIds.Count != 1)
            throw new InvalidOperationException("Organ preservation receipt is not canonical.");
        part.preservationOperationId=receipt.OperationId; part.preservationCommitId=receipt.CommitId;
        part.preservationSourceStackId=receipt.SourceStackIds[0]; part.preservationInputMassGrams=receipt.InputMassGrams;
    }

    public static bool TryFinalize(SurgicalPartInstance part, IPhysicalItemBatchDispositionService dispositions, out string failure)
    {
        failure=string.Empty;
        if (!HasPending(part) || dispositions==null) { failure="organ-preservation-outbox-invalid"; return false; }
        bool pending=dispositions.TryGetPending(part.preservationOperationId,out PhysicalItemBatchDispositionReceipt receipt);
        if (pending && !string.Equals(receipt.CommitId,part.preservationCommitId,StringComparison.Ordinal)) { failure="organ-preservation-receipt-mismatch"; return false; }
        if (!part.preservationOutcomePublished)
        {
            if(!pending){failure="organ-preservation-receipt-missing";return false;}
            part.preservationCanisterApplied=true; part.preservationOutcomePublished=true;
        }
        if(!part.preservationCanisterApplied){failure="organ-preservation-terminal-mismatch";return false;}
        if(pending && !dispositions.Acknowledge(part.preservationCommitId,out failure)) return false;
        part.preservationOperationId=part.preservationCommitId=part.preservationSourceStackId=string.Empty;
        part.preservationInputMassGrams=0; part.preservationOutcomePublished=false;
        return true;
    }
}
