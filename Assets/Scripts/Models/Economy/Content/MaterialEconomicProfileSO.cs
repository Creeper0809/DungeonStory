using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(
    fileName = "MaterialEconomicProfile",
    menuName = "DungeonStory/Economy/Material Economic Profile")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class MaterialEconomicProfileSO : DataScriptableObject
{
    [SerializeField] private string physicalItemId = string.Empty;
    [Min(0f), SerializeField] private float intrinsicValue = 1f;
    [Min(0f), SerializeField] private float handlingDifficulty = 0.5f;
    [Range(0f, 1f), SerializeField] private float salvageRetention = 0.6f;
    [SerializeField] private bool consumableDuringCraft;

    public string PhysicalItemId => physicalItemId?.Trim() ?? string.Empty;
    public float IntrinsicValue => Mathf.Max(0f, intrinsicValue);
    public float HandlingDifficulty => Mathf.Max(0f, handlingDifficulty);
    public float SalvageRetention => Mathf.Clamp01(salvageRetention);
    public bool ConsumableDuringCraft => consumableDuringCraft;
    public float WorkFactor => Mathf.Clamp(
        0.70f + IntrinsicValue * 0.20f + HandlingDifficulty * 0.15f,
        0.80f,
        2.20f);

#if UNITY_EDITOR
    public void Configure(
        string itemId,
        float value,
        float difficulty,
        float salvage,
        bool consumable)
    {
        physicalItemId = itemId?.Trim() ?? string.Empty;
        intrinsicValue = Mathf.Max(0f, value);
        handlingDifficulty = Mathf.Max(0f, difficulty);
        salvageRetention = Mathf.Clamp01(salvage);
        consumableDuringCraft = consumable;
    }
#endif
}

public interface IMaterialEconomicProfileCatalog
{
    bool TryGet(string physicalItemId, out MaterialEconomicProfileSO profile);
    float GetWorkFactor(string physicalItemId);
    float GetSalvageRetention(string physicalItemId);
    bool IsConsumableDuringCraft(string physicalItemId);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ResourceMaterialEconomicProfileCatalog :
    IMaterialEconomicProfileCatalog
{
    private readonly Dictionary<string, MaterialEconomicProfileSO> byItemId;
    private readonly Dictionary<string, DerivedMaterialProfile> derivedByItemId;

    public ResourceMaterialEconomicProfileCatalog(IGameContentDefinitionSource source)
    {
        MaterialEconomicProfileSO[] definitions =
            (source ?? throw new ArgumentNullException(nameof(source)))
            .GetAll<MaterialEconomicProfileSO>()
            .Where(value => value != null)
            .OrderBy(value => value.PhysicalItemId, StringComparer.Ordinal)
            .ToArray();
        if (definitions.Any(value => string.IsNullOrWhiteSpace(value.PhysicalItemId))
            || definitions.Select(value => value.PhysicalItemId)
                .Distinct(StringComparer.Ordinal).Count() != definitions.Length)
        {
            throw new InvalidOperationException(
                "V23 material economic profiles require unique physical item IDs.");
        }
        byItemId = definitions.ToDictionary(
            value => value.PhysicalItemId,
            StringComparer.Ordinal);
        derivedByItemId = source.GetAll<ItemDefinitionSO>()
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ItemId))
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => Derive(group.First()),
                StringComparer.Ordinal);
    }

    public bool TryGet(
        string physicalItemId,
        out MaterialEconomicProfileSO profile) => byItemId.TryGetValue(
        physicalItemId?.Trim() ?? string.Empty,
        out profile);

    public float GetWorkFactor(string physicalItemId) =>
        TryGet(physicalItemId, out MaterialEconomicProfileSO profile)
            ? profile.WorkFactor
            : derivedByItemId.TryGetValue(
                physicalItemId?.Trim() ?? string.Empty,
                out DerivedMaterialProfile derived)
                ? derived.WorkFactor
                : 1f;

    public float GetSalvageRetention(string physicalItemId) =>
        TryGet(physicalItemId, out MaterialEconomicProfileSO profile)
            ? profile.SalvageRetention
            : derivedByItemId.TryGetValue(
                physicalItemId?.Trim() ?? string.Empty,
                out DerivedMaterialProfile derived)
                ? derived.SalvageRetention
                : 0.6f;

    public bool IsConsumableDuringCraft(string physicalItemId) =>
        TryGet(physicalItemId, out MaterialEconomicProfileSO profile)
            ? profile.ConsumableDuringCraft
            : derivedByItemId.TryGetValue(
                physicalItemId?.Trim() ?? string.Empty,
                out DerivedMaterialProfile derived)
                && derived.Consumable;

    private static DerivedMaterialProfile Derive(ItemDefinitionSO item)
    {
        float intrinsic = Mathf.Clamp(
            Mathf.Log(Mathf.Max(1f, item.UnitPrice + 1f), 2f) / 3f,
            0f,
            5f);
        float handling = Mathf.Clamp(
            0.25f + Mathf.Sqrt(item.UnitWeight) * 0.22f
            + (item.MaxStack == 1 ? 0.35f : 0f),
            0f,
            5f);
        bool consumable = item.StockCategory is StockCategory.Food
            or StockCategory.Medicine
            or StockCategory.Fuel;
        float salvage = consumable
            ? 0f
            : Mathf.Clamp(0.72f - handling * 0.08f, 0.35f, 0.75f);
        return new DerivedMaterialProfile(
            Mathf.Clamp(0.70f + intrinsic * 0.20f + handling * 0.15f,
                0.80f,
                2.20f),
            salvage,
            consumable);
    }

    private readonly struct DerivedMaterialProfile
    {
        public DerivedMaterialProfile(
            float workFactor,
            float salvageRetention,
            bool consumable)
        {
            WorkFactor = workFactor;
            SalvageRetention = salvageRetention;
            Consumable = consumable;
        }

        public float WorkFactor { get; }
        public float SalvageRetention { get; }
        public bool Consumable { get; }
    }
}
