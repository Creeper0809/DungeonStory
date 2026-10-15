using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>A frozen mechanical selection, independent of any log format or scoring policy.</summary>
public readonly struct EquipmentModuleSelectedFact
{
    private readonly EquipmentModuleSelectedFactData data;
    public EquipmentModuleSelectedFact(EquipmentModuleSelectedFactData data) =>
        this.data = (data ?? throw new ArgumentNullException(nameof(data))).Clone();
    public EquipmentModuleSelectedFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("equipment-selection-fact-uninitialized")).Clone();
}

[Serializable]
public sealed class EquipmentModuleSelectedFactData
{
    public string operationId = string.Empty, selectionId = string.Empty, nodeId = string.Empty,
        nodeDisplayName = string.Empty, equipmentInstanceId = string.Empty, equipmentDefinitionId = string.Empty,
        currentOwnerId = string.Empty, attunementOwnerId = string.Empty, reforgeOrderId = string.Empty;
    public long beforeRevision, afterRevision;
    public bool historical;
    public int generationBefore, generationAfter, absoluteDay;
    public float masteryBefore, masteryAfter;
    public string[] positiveModuleIds = Array.Empty<string>(), drawbackModuleIds = Array.Empty<string>(),
        selectedModuleIds = Array.Empty<string>(), capabilityIds = Array.Empty<string>(),
        effectIds = Array.Empty<string>(), evidenceFactIds = Array.Empty<string>();

    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(operationId) || !DomainFactValueSyntax.IsIdentity(selectionId)
            || !DomainFactValueSyntax.IsIdentity(nodeId) || !DomainFactValueSyntax.IsText(nodeDisplayName, 4096, false)
            || !DomainFactValueSyntax.IsIdentity(equipmentInstanceId) || !DomainFactValueSyntax.IsIdentity(equipmentDefinitionId)
            || !OptionalId(currentOwnerId) || !OptionalId(attunementOwnerId) || !OptionalId(reforgeOrderId)
            || beforeRevision < 0 || beforeRevision == long.MaxValue || afterRevision != beforeRevision + 1
            || operationId != "equipment-module:" + equipmentInstanceId + ":" + afterRevision.ToString(System.Globalization.CultureInfo.InvariantCulture)
            || generationBefore < 0 || generationAfter < 0 || absoluteDay < 0
            || !Finite(masteryBefore) || !Finite(masteryAfter)
            || !Ids(positiveModuleIds, true) || !Ids(drawbackModuleIds, false) || !Ids(selectedModuleIds, true)
            || !Ids(capabilityIds, true) || !Ids(effectIds, false) || !Ids(evidenceFactIds, true)
            || !positiveModuleIds.Concat(drawbackModuleIds).OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(selectedModuleIds.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
            throw new ArgumentException("equipment-module-selection-fact-invalid");
    }
    public EquipmentModuleSelectedFactData Clone()
    {
        Validate();
        var copy = (EquipmentModuleSelectedFactData)MemberwiseClone();
        copy.positiveModuleIds = positiveModuleIds.ToArray(); copy.drawbackModuleIds = drawbackModuleIds.ToArray();
        copy.selectedModuleIds = selectedModuleIds.ToArray(); copy.capabilityIds = capabilityIds.ToArray();
        copy.effectIds = effectIds.ToArray(); copy.evidenceFactIds = evidenceFactIds.ToArray();
        return copy;
    }
    private static bool OptionalId(string value) => value == string.Empty || DomainFactValueSyntax.IsIdentity(value);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
    private static bool Ids(string[] values, bool required) => values != null && (!required || values.Length > 0)
        && values.All(DomainFactValueSyntax.IsIdentity) && values.Distinct(StringComparer.Ordinal).Count() == values.Length;
}
