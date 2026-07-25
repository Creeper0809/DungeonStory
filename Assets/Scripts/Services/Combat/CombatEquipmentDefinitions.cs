using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class CombatEquipmentDefinitionSO : ScriptableObject
{
    [SerializeField] private string equipmentId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private string itemId = string.Empty;
    [Min(0f), SerializeField] private float weight = 1f;
    [Range(0, 2), SerializeField] private int occupiedHands = 1;
    [Min(1f), SerializeField] private float maxDurability = 100f;

    public string EquipmentId => equipmentId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? EquipmentId : displayName.Trim();
    public string Description => description ?? string.Empty;
    public string ItemId => itemId?.Trim() ?? string.Empty;
    public float Weight => Mathf.Max(0f, weight);
    public int OccupiedHands => Mathf.Clamp(occupiedHands, 0, 2);
    public float MaxDurability => Mathf.Max(1f, maxDurability);
    public abstract CombatEquipmentKind Kind { get; }
}

public interface ICombatEquipmentCatalog
{
    IReadOnlyList<CombatEquipmentDefinitionSO> All { get; }
    bool TryGet(string definitionId, out CombatEquipmentDefinitionSO definition);
}

public sealed class ResourceCombatEquipmentCatalog : ICombatEquipmentCatalog
{
    public const string ResourcePath = "SO/Combat/Equipment";
    private readonly IResourcesAssetLoader resourcesAssetLoader;
    private IReadOnlyList<CombatEquipmentDefinitionSO> all;
    private Dictionary<string, CombatEquipmentDefinitionSO> byId;

    public ResourceCombatEquipmentCatalog(IResourcesAssetLoader resourcesAssetLoader = null)
    {
        this.resourcesAssetLoader = resourcesAssetLoader ?? new UnityResourcesAssetLoader();
    }

    public IReadOnlyList<CombatEquipmentDefinitionSO> All
    {
        get
        {
            EnsureLoaded();
            return all;
        }
    }

    public bool TryGet(string definitionId, out CombatEquipmentDefinitionSO definition)
    {
        EnsureLoaded();
        return byId.TryGetValue(definitionId?.Trim() ?? string.Empty, out definition);
    }

    private void EnsureLoaded()
    {
        if (all != null)
        {
            return;
        }

        IReadOnlyCollection<CombatEquipmentDefinitionSO> loaded =
            resourcesAssetLoader.LoadAllOptional<CombatEquipmentDefinitionSO>(ResourcePath);
        byId = loaded
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.EquipmentId))
            .GroupBy(item => item.EquipmentId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        all = byId.Values
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }
}
