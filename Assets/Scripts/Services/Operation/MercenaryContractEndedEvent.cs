using System;

public readonly struct MercenaryContractEndedEvent
{
    public MercenaryContractEndedEvent(string characterId, string characterDisplayName,
        int settlementDay, string financeTransactionId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || settlementDay < 1
            || string.IsNullOrWhiteSpace(financeTransactionId))
            throw new ArgumentException("A committed contract termination requires character, day and finance identity.");
        CharacterId = characterId;
        CharacterDisplayName = characterDisplayName ?? characterId;
        SettlementDay = settlementDay;
        FinanceTransactionId = financeTransactionId;
    }

    public string CharacterId { get; }
    public string CharacterDisplayName { get; }
    public int SettlementDay { get; }
    public string FinanceTransactionId { get; }
}
