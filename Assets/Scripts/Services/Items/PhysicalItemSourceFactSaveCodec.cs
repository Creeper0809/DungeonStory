using UnityEngine;

internal static class PhysicalItemSourceFactSaveCodec
{
    internal static PhysicalItemDispositionSourceFactSaveData ToSave(in PhysicalItemDispositionSourceFact fact) => new()
    {
        stackId = fact.StackId, itemDefinitionId = fact.ItemDefinitionId, itemInstanceId = fact.ItemInstanceId,
        quantity = fact.Quantity, massGrams = fact.MassGrams, sourceX = fact.SourcePosition.x,
        sourceY = fact.SourcePosition.y, displayText = fact.DisplayName
    };
    internal static PhysicalItemDispositionSourceFact FromSave(PhysicalItemDispositionSourceFactSaveData fact) =>
        fact == null ? default : new PhysicalItemDispositionSourceFact(fact.stackId, fact.itemDefinitionId,
            fact.itemInstanceId, fact.quantity, fact.massGrams, new Vector2Int(fact.sourceX, fact.sourceY), fact.displayText);
}
