using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class CombatEquipmentDefinitionSO : ScriptableObject
{
    [SerializeField] private string equipmentId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private string itemId = string.Empty;
    [Min(0f), SerializeField] private float weight = 1f;
    [Range(0, 2), SerializeField] private int occupiedHands = 1;
    [Min(1f), SerializeField] private float maxDurability = 100f;
    [Min(0.1f), SerializeField] private float requiredCraftWork = 6f;
    [SerializeField] private string defaultMaterialId = string.Empty;
    [Min(1), SerializeField] private int primaryMaterialAmount = 1;
    [SerializeField] private List<CombatMaterialFamily> allowedMaterialFamilies =
        new List<CombatMaterialFamily>();
    [SerializeField] private List<CombatEquipmentCraftMaterial> craftMaterials =
        new List<CombatEquipmentCraftMaterial>();
    [SerializeField] private List<ItemAmountDefinition> requiredComponentInputs =
        new List<ItemAmountDefinition>();
    [SerializeField] private string requiredResearchId = string.Empty;
    [SerializeField] private EquipmentEra era = EquipmentEra.Starting;
    [Min(0), SerializeField] private int tier;
    [SerializeField] private EquipmentSlotProfile slotProfile;
    [SerializeField] private EquipmentLineageKind lineageKind;
    [SerializeField] private bool growthEquipment;
    [Range(0.5f, 1f), SerializeField] private float growthBaseStatMultiplier = 0.88f;

    public string EquipmentId => equipmentId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EquipmentId : displayName.Trim();
    public string Description => description ?? string.Empty;
    public string ItemId => itemId?.Trim() ?? string.Empty;
    public float Weight => Mathf.Max(0f, weight);
    public int OccupiedHands => Mathf.Clamp(occupiedHands, 0, 2);
    public float MaxDurability => Mathf.Max(1f, maxDurability);
    public float RequiredCraftWork => Mathf.Max(0.1f, requiredCraftWork);
    public string DefaultMaterialId => defaultMaterialId?.Trim() ?? string.Empty;
    public int PrimaryMaterialAmount => Mathf.Max(1, primaryMaterialAmount);
    public IReadOnlyList<CombatMaterialFamily> AllowedMaterialFamilies =>
        allowedMaterialFamilies ??= new List<CombatMaterialFamily>();
    public IReadOnlyList<CombatEquipmentCraftMaterial> CraftMaterials =>
        craftMaterials ??= new List<CombatEquipmentCraftMaterial>();
    public IReadOnlyList<ItemAmountDefinition> RequiredComponentInputs =>
        requiredComponentInputs ??= new List<ItemAmountDefinition>();
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;
    public EquipmentEra Era => era;
    public int Tier => Mathf.Max(0, tier);
    public EquipmentSlotProfile SlotProfile => slotProfile;
    public int ModuleSlotCount => (int)slotProfile;
    public EquipmentLineageKind LineageKind => lineageKind;
    public bool GrowthEquipment => growthEquipment;
    public float BaseStatMultiplier => growthEquipment
        ? Mathf.Clamp(growthBaseStatMultiplier, 0.5f, 1f)
        : 1f;
    public abstract CombatEquipmentKind Kind { get; }

    public bool AllowsMaterial(CraftMaterialDefinitionSO material)
    {
        return material != null
            && (AllowedMaterialFamilies.Count == 0
                || AllowedMaterialFamilies.Contains(material.Family));
    }

#if UNITY_EDITOR
    public void ConfigureRequiredComponentInputs(
        IEnumerable<ItemAmountDefinition> components)
    {
        requiredComponentInputs = (components ?? Array.Empty<ItemAmountDefinition>())
            .Where(component => component != null
                && !string.IsNullOrWhiteSpace(component.ItemId)
                && component.Amount > 0)
            .Select(component =>
                new ItemAmountDefinition(component.ItemId, component.Amount))
            .ToList();
    }
#endif
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatEquipmentCraftMaterial
{
    public StockCategory category = StockCategory.General;
    [Min(1)] public int amount = 1;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICombatEquipmentCatalog
{
    IReadOnlyList<CombatEquipmentDefinitionSO> All { get; }
    bool TryGet(string definitionId, out CombatEquipmentDefinitionSO definition);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceCombatEquipmentCatalog : ICombatEquipmentCatalog
{
    public const string ResourcePath = "SO/Combat/Equipment";
    private readonly IReadOnlyList<CombatEquipmentDefinitionSO> all;
    private readonly Dictionary<string, CombatEquipmentDefinitionSO> byId;

    public ResourceCombatEquipmentCatalog(IGameContentDefinitionSource content)
    {
        CombatEquipmentDefinitionSO[] definitions = (content
                ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<CombatEquipmentDefinitionSO>()
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.EquipmentId))
            .ToArray();
        IGrouping<string, CombatEquipmentDefinitionSO> duplicate = definitions
            .GroupBy(item => item.EquipmentId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException(
                $"Duplicate combat equipment definition id '{duplicate.Key}'.");
        }

        byId = definitions.ToDictionary(item => item.EquipmentId, StringComparer.Ordinal);
        all = byId.Values
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<CombatEquipmentDefinitionSO> All => all;

    public bool TryGet(string definitionId, out CombatEquipmentDefinitionSO definition)
    {
        return byId.TryGetValue(definitionId?.Trim() ?? string.Empty, out definition);
    }
}
