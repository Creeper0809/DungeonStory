using System;
using System.Collections.Generic;

namespace DungeonStory.Factions
{
    // Frozen gameplay observations. Publication keys are opaque transport receipts.
    [Serializable]
    public sealed class FactionRouteFactData
    {
        public string routeId = string.Empty, factionId = string.Empty, factionDisplayName = string.Empty;
        public FactionRouteKind kind;
        public FactionRouteStatus status;
        public int strength, sourceQ, sourceR, destinationQ, destinationR;
        public float delaySeconds;
        public string settlementPublicationKey = string.Empty;
        public List<string> cargoItemIds = new();

        public bool IsEmpty => routeId == string.Empty && factionId == string.Empty && factionDisplayName == string.Empty
            && kind == 0 && status == 0 && strength == 0 && sourceQ == 0 && sourceR == 0 && destinationQ == 0
            && destinationR == 0 && delaySeconds == 0 && settlementPublicationKey == string.Empty && cargoItemIds?.Count == 0;
    }

    [Serializable]
    public sealed class FactionRouteObservationData
    {
        public int absoluteDay;
        public string operationId = string.Empty;
        // Unity inline serialization materializes empty nested objects; absence is an
        // explicit empty value, not nullable object identity.
        public FactionRouteFactData before = new(), after = new();
        public FactionRouteSettlementReceipt settlement = new();

        public bool IsEmpty => absoluteDay == 0 && operationId == string.Empty
            && before?.IsEmpty == true && after?.IsEmpty == true && HasEmptySettlement;

        public bool HasEmptySettlement => settlement != null && settlement.state == 0
            && settlement.capabilityId == string.Empty && settlement.capabilityVersion == 0 && settlement.operationSequence == 0
            && settlement.cargoAuthoredGold == 0 && settlement.paymentGold == 0 && settlement.quoteLines?.Count == 0
            && settlement.sourceDigest == string.Empty && settlement.quoteDigest == string.Empty
            && settlement.transactionId == string.Empty && settlement.transactionSourceId == string.Empty
            && settlement.transactionTargetId == string.Empty && settlement.balanceBefore == 0 && settlement.balanceAfter == 0
            && settlement.allianceBenefitAuthorityDigest == string.Empty && settlement.allianceBenefitReservationId == string.Empty
            && settlement.allianceBenefitDebitMilliEwu == 0 && settlement.allianceBenefitBalanceBeforeMilliEwu == 0
            && settlement.allianceBenefitBalanceAfterMilliEwu == 0;
    }
}
