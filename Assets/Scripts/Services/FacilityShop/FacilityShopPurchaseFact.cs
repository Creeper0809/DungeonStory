using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class FacilityShopPurchaseFactData
{
    public string operationId = string.Empty, shopSource = string.Empty, offerTypeId = string.Empty;
    public string offerName = string.Empty, itemDefinitionId = string.Empty;
    public string paymentTransactionId = string.Empty, paymentSourceId = string.Empty;
    public int sequence, absoluteDay, offerDataId, quotedCost, paidCost, balanceBefore, balanceAfter;
    public FacilityShopPurchaseFactData Clone() => (FacilityShopPurchaseFactData)MemberwiseClone();
    public void Validate(bool proposal = false)
    {
        if (!Text(operationId) || !Text(shopSource, true) || !DomainFactValueSyntax.IsIdentity(offerTypeId)
            || !DomainFactValueSyntax.IsText(offerName, DomainFactValueSyntax.MaximumNameCharacters, false)
            || offerName != offerName.Trim() || !DomainFactValueSyntax.IsIdentity(itemDefinitionId)
            || !Text(paymentTransactionId, true) || !Text(paymentSourceId) || paymentSourceId != operationId
            || sequence <= 0 || absoluteDay < 0 || offerDataId < 0 || quotedCost < 0 || balanceBefore < 0
            || paidCost < 0 || paidCost > quotedCost || balanceAfter < 0
            || (proposal ? paidCost != 0 || balanceAfter != balanceBefore || paymentTransactionId.Length != 0
                : balanceBefore - balanceAfter != paidCost || (paidCost > 0) != (paymentTransactionId.Length > 0)))
            throw new ArgumentException("facility-shop-purchase-fact-invalid");
    }
    private static bool Text(string value, bool empty = false) =>
        DomainFactValueSyntax.IsText(value, 2048, empty) && value == value.Trim();
}

public readonly struct FacilityShopPurchaseFact
{
    private readonly FacilityShopPurchaseFactData data;
    public FacilityShopPurchaseFact(FacilityShopPurchaseFactData source, bool proposal = false)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        source.Validate(proposal); data = source.Clone(); IsProposal = proposal;
    }
    public bool IsProposal { get; }
    public FacilityShopPurchaseFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("facility-shop-purchase-fact-empty");
    public bool MatchesProposal(FacilityShopPurchaseFact proposed)
    {
        var other = proposed.Snapshot();
        return !IsProposal && proposed.IsProposal && data.operationId == other.operationId
            && data.sequence == other.sequence && data.absoluteDay == other.absoluteDay
            && data.shopSource == other.shopSource && data.offerTypeId == other.offerTypeId
            && data.offerDataId == other.offerDataId && data.offerName == other.offerName
            && data.itemDefinitionId == other.itemDefinitionId && data.quotedCost == other.quotedCost
            && data.balanceBefore == other.balanceBefore && data.paymentSourceId == other.paymentSourceId;
    }
}

// Required domain receiver acknowledges durable enqueue, not physical delivery or presentation.
public interface IFacilityShopPurchaseDelivery
{
    bool TryAcceptPurchase(FacilityShopPurchaseFact fact, string publicationKey, out string failure);
}
