using System.Collections.Generic;

/// <summary>Retires recovery evidence only with an atomic, rollback-capable parent completion.</summary>
public interface IPhysicalEffectReceiptAcknowledgement
{
    bool AcknowledgePhysicalEffectReceipts(IReadOnlyList<string> commitIds,
        IPhysicalItemDispositionAcknowledgementParticipant parent, out string failureReason);
}

