using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;

public sealed class FactionGoodwillTransferredFact
{
    public FactionGoodwillTransferredFact(DungeonFactionState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.factionId) || !state.goodwillTransferCompleted
            || string.IsNullOrWhiteSpace(state.goodwillTransferOperationId) || string.IsNullOrWhiteSpace(state.goodwillTransferCommitId)
            || string.IsNullOrWhiteSpace(state.goodwillFactionDisplayName) || state.goodwillObservationDay < 1
            || state.goodwillTransferRevision <= 0 || state.goodwillTransferQuantity <= 0 || state.goodwillTransferMassGrams <= 0
            || state.goodwillTransferSourceStackIds == null || state.goodwillTransferSourceStackIds.Count == 0
            || state.goodwillTransferSourceStackIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("faction-goodwill-fact-invalid");
        FactionId = state.factionId; FactionDisplayName = state.goodwillFactionDisplayName;
        AbsoluteDay = state.goodwillObservationDay; OperationId = state.goodwillTransferOperationId;
        TransferCommitId = state.goodwillTransferCommitId; TransferRevision = state.goodwillTransferRevision;
        PreviousTrust = state.goodwillCampaignRapportBefore; CurrentTrust = state.goodwillCampaignRapportTarget;
        Quantity = state.goodwillTransferQuantity; MassGrams = state.goodwillTransferMassGrams;
        PhysicalValue = state.goodwillTransferredPhysicalValue;
        SourceStackIds = Array.AsReadOnly(state.goodwillTransferSourceStackIds.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }
    public string FactionId { get; }
    public string FactionDisplayName { get; }
    public string OperationId { get; }
    public string TransferCommitId { get; }
    public int AbsoluteDay { get; }
    public int PreviousTrust { get; }
    public int CurrentTrust { get; }
    public int Quantity { get; }
    public int PhysicalValue { get; }
    public long MassGrams { get; }
    public long TransferRevision { get; }
    public IReadOnlyList<string> SourceStackIds { get; }
}
