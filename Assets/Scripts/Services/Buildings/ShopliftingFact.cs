using System;
using DungeonStory.Foundation.Facts;

[Serializable]
public sealed class ShopliftingFactData
{
    public string unitOperationId = string.Empty, facilityId = string.Empty, facilityName = string.Empty;
    public string actorId = string.Empty, actorName = string.Empty, itemName = string.Empty;
    public int absoluteDay, lossValue, stockCategory, sourceQuantityBefore;
    public float chance;
    public RetailStockLotSnapshot lot;
    public ShopliftingFactData Clone()
    {
        var clone = (ShopliftingFactData)MemberwiseClone(); clone.lot = lot?.Clone(); return clone;
    }
    public void Validate()
    {
        if (!Key(unitOperationId) || !Key(facilityId) || !Key(actorId)
            || !Text(facilityName) || !Text(actorName) || !Text(itemName)
            || absoluteDay < 0 || lossValue < 0 || !Enum.IsDefined(typeof(StockCategory), stockCategory)
            || !float.IsFinite(chance) || chance < 0f || chance > 1f || sourceQuantityBefore < 1
            || lot == null || lot.quantity != 1 || lot.saleItemId < 0 || lot.unitMassGrams <= 0
            || !Key(lot.itemDefinitionId) || !Key(lot.sourceOperationId) || !OptionalKey(lot.itemInstanceId)
            || !OptionalKey(lot.sourceStackId) || lot.componentFingerprint == null
            || lot.components == null || lot.components.Exists(value => value == null)
            || lot.itemInstanceId.Length > 0 && string.IsNullOrWhiteSpace(lot.componentFingerprint)
            || lot.itemInstanceId.Length > 0 && sourceQuantityBefore != 1
            || unitOperationId != $"retail-unit:{lot.sourceOperationId}:ordinal:{sourceQuantityBefore:D10}")
            throw new ArgumentException("shoplifting-fact-invalid");
        foreach (var component in lot.components)
        {
            if (!Key(component.componentTypeId) || component.schemaVersion < 1 || component.values == null)
                throw new ArgumentException("shoplifting-fact-component-invalid");
            foreach (var value in component.values)
                if (value == null || !Key(value.key) || !Enum.IsDefined(typeof(ItemStateValueKind), value.kind)
                    || value.stringValue == null || !double.IsFinite(value.decimalValue))
                    throw new ArgumentException("shoplifting-fact-component-value-invalid");
        }
    }
    private static bool Key(string value) => DomainFactValueSyntax.IsText(value, 2048, false) && value == value.Trim();
    private static bool OptionalKey(string value) => value == string.Empty || Key(value);
    private static bool Text(string value) => DomainFactValueSyntax.IsText(value, 16384, true);
}

// Frozen evidence of one taken unit, not a native log payload or its presentation.
public readonly struct ShopliftingFact
{
    private readonly ShopliftingFactData data;
    public ShopliftingFact(ShopliftingFactData source)
    { source = source ?? throw new ArgumentNullException(nameof(source)); source.Validate(); data = source.Clone(); }
    public ShopliftingFactData Snapshot() => data?.Clone() ?? throw new InvalidOperationException("shoplifting-fact-empty");
}

// Synchronous, non-durable notification for existing facility/character presentation consumers.
public readonly struct ShopliftingCompletedEvent
{
    public ShopliftingCompletedEvent(Shop shop, IBuildingVisitorPort actor, ShopliftingFact fact)
    { Shop = shop ?? throw new ArgumentNullException(nameof(shop)); Actor = actor; Fact = fact; }
    public Shop Shop { get; }
    public IBuildingVisitorPort Actor { get; }
    public ShopliftingFact Fact { get; }
}
