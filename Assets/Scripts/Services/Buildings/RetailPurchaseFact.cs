using System;
using DungeonStory.Foundation.Facts;

// Exact game facts after the physical unit has left retail custody. Native log
// keys, scoring, participants and presentation are deliberately absent.
public sealed class RetailPurchaseFactData
{
    public string operationId, buyerId, facilityId, revenueWorkerId, revenueSkillModifierProvenance;
    public int absoluteDay, purchaseCost, buyerMoneyBefore, buyerMoneyAfter;
    public float revenuePriceMultiplier;
    public RetailStockLotSnapshot lot;

    public RetailPurchaseFactData Clone()
    {
        var copy = (RetailPurchaseFactData)MemberwiseClone();
        copy.lot = lot?.Clone();
        return copy;
    }

    public void Validate()
    {
        if (!Key(operationId) || !Key(buyerId) || !Key(facilityId)
            || !OptionalKey(revenueWorkerId) || !Key(revenueSkillModifierProvenance)
            || absoluteDay < 0 || purchaseCost < 0 || buyerMoneyAfter < 0
            || buyerMoneyBefore < buyerMoneyAfter || buyerMoneyBefore - buyerMoneyAfter != purchaseCost
            || !float.IsFinite(revenuePriceMultiplier) || revenuePriceMultiplier < 1f
            || revenuePriceMultiplier > GoldEconomyBalanceRules.MaximumWorkerRevenuePremium
            || lot == null || lot.saleItemId < 0 || lot.quantity != 1 || lot.unitMassGrams <= 0L
            || !Key(lot.itemDefinitionId) || !Key(lot.sourceOperationId)
            || !OptionalKey(lot.itemInstanceId) || !OptionalKey(lot.sourceStackId)
            || lot.components == null || lot.components.Exists(value => value == null)
            || lot.componentFingerprint == null
            || lot.components.Count > 0 && !Key(lot.componentFingerprint)
            || !operationId.StartsWith("retail-unit:" + lot.sourceOperationId + ":ordinal:", StringComparison.Ordinal))
            throw new ArgumentException("retail-purchase-fact-invalid");
        foreach (var component in lot.components)
        {
            if (!Key(component.componentTypeId) || component.schemaVersion < 1 || component.values == null)
                throw new ArgumentException("retail-purchase-component-invalid");
            foreach (var value in component.values)
                if (value == null || !Key(value.key) || value.stringValue == null || !double.IsFinite(value.decimalValue))
                    throw new ArgumentException("retail-purchase-component-value-invalid");
        }
    }

    private static bool Key(string value) => DomainFactValueSyntax.IsText(value, 2048, false) && value == value.Trim();
    private static bool OptionalKey(string value) => value == string.Empty || Key(value);
}

public readonly struct RetailPurchaseFact
{
    private readonly RetailPurchaseFactData data;
    public RetailPurchaseFact(RetailPurchaseRecoverySaveData source) : this(FromOwner(source)) { }
    public RetailPurchaseFact(RetailPurchaseFactData source)
    {
        source = source ?? throw new ArgumentNullException(nameof(source));
        source.Validate();
        data = source.Clone();
    }
    public RetailPurchaseFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("retail-purchase-fact-empty");

    private static RetailPurchaseFactData FromOwner(RetailPurchaseRecoverySaveData source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        return new RetailPurchaseFactData
        {
            operationId = source.operationId, buyerId = source.buyerId, facilityId = source.facilityId,
            absoluteDay = source.absoluteDay, purchaseCost = source.purchaseCost,
            buyerMoneyBefore = source.buyerMoneyBefore, buyerMoneyAfter = source.buyerMoneyAfter,
            revenueWorkerId = source.revenueWorkerId, revenuePriceMultiplier = source.revenuePriceMultiplier,
            revenueSkillModifierProvenance = source.revenueSkillModifierProvenance, lot = source.lot
        };
    }
}
