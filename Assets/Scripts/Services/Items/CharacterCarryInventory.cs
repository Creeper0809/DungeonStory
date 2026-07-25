using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterCarryInventory : MonoBehaviour
{
    private static readonly HashSet<CharacterCarryInventory> ActiveInventories =
        new HashSet<CharacterCarryInventory>();
    [SerializeField] private List<CharacterCarriedItemSaveData> carriedItems =
        new List<CharacterCarriedItemSaveData>();

    private CharacterActor actor;
    private IDungeonItemCatalogProvider catalogProvider;
    private IItemHaulingSettingsProvider haulingSettingsProvider;

    public IReadOnlyList<CharacterCarriedItemSaveData> Items => carriedItems;
    public bool HasItems => carriedItems.Any(item => item != null && item.quantity > 0);

    private void Awake()
    {
        actor = GetComponent<CharacterActor>();
    }

    private void OnEnable()
    {
        ActiveInventories.Add(this);
    }

    private void OnDisable()
    {
        ActiveInventories.Remove(this);
    }

    public static CharacterCarryInventory FindByCharacterId(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        return ActiveInventories.FirstOrDefault(inventory =>
        {
            if (inventory == null)
            {
                return false;
            }

            return string.Equals(
                inventory.actor?.Identity?.PersistentId,
                characterId,
                StringComparison.Ordinal);
        });
    }

    public static CharacterCarryInventory Ensure(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        CharacterCarryInventory inventory = actor.GetComponent<CharacterCarryInventory>();
        if (inventory == null && Application.isPlaying)
        {
            inventory = actor.gameObject.AddComponent<CharacterCarryInventory>();
        }

        return inventory;
    }

    public void Configure(
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider haulingSettingsProvider)
    {
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.haulingSettingsProvider = haulingSettingsProvider
            ?? throw new ArgumentNullException(nameof(haulingSettingsProvider));
    }

    public float GetBaseCarryLimit()
    {
        CharacterStats stats = actor != null ? actor.Stats : null;
        int strength = stats != null ? stats.GetCharacterStat(CharacterStatType.Strength) : 5;
        int endurance = stats != null ? stats.GetCharacterStat(CharacterStatType.Endurance) : 5;
        return 8f + (strength * 1.5f) + (endurance * 0.75f);
    }

    public float GetMaxAllowedWeight(IItemHaulingSettingsProvider settingsProvider = null)
    {
        IItemHaulingSettingsProvider resolvedSettings = settingsProvider ?? haulingSettingsProvider;
        float multiplier = resolvedSettings != null
            ? resolvedSettings.MaxCarryMultiplier
            : Mathf.Clamp(DungeonUserSettingsRuntime.Current.maxCarryMultiplier, 1f, 2.5f);
        return GetBaseCarryLimit() * Mathf.Clamp(multiplier, 1f, 2.5f);
    }

    public float GetCurrentWeight(IDungeonItemCatalogProvider catalogProvider = null)
    {
        float total = 0f;
        foreach (CharacterCarriedItemSaveData item in carriedItems)
        {
            if (item == null || item.quantity <= 0)
            {
                continue;
            }

            DungeonItemDefinition definition = ResolveDefinition(
                item.itemId,
                catalogProvider ?? this.catalogProvider);
            total += definition.UnitWeight * Mathf.Max(0, item.quantity);
        }

        return total;
    }

    public float GetMoveSpeedMultiplier(
        IDungeonItemCatalogProvider catalogProvider = null,
        IItemHaulingSettingsProvider settingsProvider = null)
    {
        float baseLimit = Mathf.Max(0.01f, GetBaseCarryLimit());
        float maxAllowed = Mathf.Max(baseLimit, GetMaxAllowedWeight(settingsProvider));
        float current = GetCurrentWeight(catalogProvider);
        if (current <= baseLimit)
        {
            return 1f;
        }

        float t = Mathf.InverseLerp(baseLimit, maxAllowed, current);
        return Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(t));
    }

    public float GetLoadRatio(
        IDungeonItemCatalogProvider catalogProvider = null,
        IItemHaulingSettingsProvider settingsProvider = null)
    {
        float maxAllowed = Mathf.Max(0.01f, GetMaxAllowedWeight(settingsProvider));
        return Mathf.Clamp01(GetCurrentWeight(catalogProvider) / maxAllowed);
    }

    public int GetMaxAcceptableQuantity(
        string itemId,
        int requestedQuantity,
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider settingsProvider)
    {
        int safeQuantity = Mathf.Max(0, requestedQuantity);
        if (safeQuantity == 0)
        {
            return 0;
        }

        DungeonItemDefinition definition = ResolveDefinition(
            itemId,
            catalogProvider ?? this.catalogProvider);
        float unitWeight = Mathf.Max(0.01f, definition.UnitWeight);
        float remainingWeight = Mathf.Max(0f, GetMaxAllowedWeight(settingsProvider) - GetCurrentWeight(catalogProvider));
        return Mathf.Clamp(Mathf.FloorToInt(remainingWeight / unitWeight), 0, safeQuantity);
    }

    public bool TryAdd(
        string sourceStackId,
        string itemId,
        int quantity,
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider settingsProvider,
        out string failureReason)
    {
        return TryAddPartialStack(
            sourceStackId,
            itemId,
            quantity,
            catalogProvider,
            settingsProvider,
            out _,
            out failureReason)
            && string.IsNullOrWhiteSpace(failureReason);
    }

    public bool TryAddPartialStack(
        string sourceStackId,
        string itemId,
        int quantity,
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider settingsProvider,
        out int acceptedQuantity,
        out string failureReason)
    {
        failureReason = string.Empty;
        acceptedQuantity = GetMaxAcceptableQuantity(itemId, quantity, catalogProvider, settingsProvider);
        if (acceptedQuantity <= 0)
        {
            failureReason = "carry limit";
            return false;
        }

        CharacterCarriedItemSaveData existing = carriedItems.FirstOrDefault(item => item != null
            && string.Equals(item.itemId, itemId, StringComparison.Ordinal)
            && string.Equals(item.sourceStackId, sourceStackId, StringComparison.Ordinal));
        if (existing == null)
        {
            carriedItems.Add(new CharacterCarriedItemSaveData
            {
                sourceStackId = sourceStackId ?? string.Empty,
                itemId = itemId ?? string.Empty,
                quantity = acceptedQuantity
            });
        }
        else
        {
            existing.quantity += acceptedQuantity;
        }

        if (acceptedQuantity < quantity)
        {
            failureReason = "carry limit";
        }

        return acceptedQuantity > 0;
    }

    public int CountItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        return carriedItems
            .Where(item => item != null
                && item.quantity > 0
                && string.Equals(item.itemId, itemId, StringComparison.Ordinal))
            .Sum(item => item.quantity);
    }

    public bool TryConsumeItem(string itemId, int quantity)
    {
        int remaining = Mathf.Max(0, quantity);
        if (remaining <= 0 || CountItem(itemId) < remaining)
        {
            return false;
        }

        for (int index = carriedItems.Count - 1; index >= 0 && remaining > 0; index--)
        {
            CharacterCarriedItemSaveData item = carriedItems[index];
            if (item == null
                || item.quantity <= 0
                || !string.Equals(item.itemId, itemId, StringComparison.Ordinal))
            {
                continue;
            }

            int consumed = Mathf.Min(remaining, item.quantity);
            item.quantity -= consumed;
            remaining -= consumed;
            if (item.quantity <= 0)
            {
                carriedItems.RemoveAt(index);
            }
        }

        return remaining == 0;
    }

    public bool TryConsumeSourceStack(string sourceStackId, string itemId, int quantity = 1)
    {
        int remaining = Mathf.Max(0, quantity);
        if (remaining <= 0 || string.IsNullOrWhiteSpace(sourceStackId))
        {
            return false;
        }

        for (int index = carriedItems.Count - 1; index >= 0 && remaining > 0; index--)
        {
            CharacterCarriedItemSaveData item = carriedItems[index];
            if (item == null
                || item.quantity <= 0
                || !string.Equals(item.sourceStackId, sourceStackId, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(itemId)
                    && !string.Equals(item.itemId, itemId, StringComparison.Ordinal)))
            {
                continue;
            }

            int consumed = Mathf.Min(remaining, item.quantity);
            item.quantity -= consumed;
            remaining -= consumed;
            if (item.quantity <= 0)
            {
                carriedItems.RemoveAt(index);
            }
        }

        return remaining == 0;
    }

    public List<CharacterCarriedItemSaveData> RemoveAllItems()
    {
        List<CharacterCarriedItemSaveData> result = carriedItems
            .Where(item => item != null && item.quantity > 0)
            .Select(item => new CharacterCarriedItemSaveData
            {
                sourceStackId = item.sourceStackId,
                itemId = item.itemId,
                quantity = item.quantity
            })
            .ToList();
        carriedItems.Clear();
        return result;
    }

    public CharacterCarryInventorySaveData Capture()
    {
        return new CharacterCarryInventorySaveData
        {
            items = carriedItems
                .Where(item => item != null && item.quantity > 0)
                .Select(item => new CharacterCarriedItemSaveData
                {
                    sourceStackId = item.sourceStackId,
                    itemId = item.itemId,
                    quantity = Mathf.Max(0, item.quantity)
                })
                .ToList()
        };
    }

    public void Restore(CharacterCarryInventorySaveData snapshot)
    {
        carriedItems.Clear();
        foreach (CharacterCarriedItemSaveData item in snapshot?.items ?? new List<CharacterCarriedItemSaveData>())
        {
            if (item == null || item.quantity <= 0 || string.IsNullOrWhiteSpace(item.itemId))
            {
                continue;
            }

            carriedItems.Add(new CharacterCarriedItemSaveData
            {
                sourceStackId = item.sourceStackId ?? string.Empty,
                itemId = item.itemId.Trim(),
                quantity = Mathf.Max(0, item.quantity)
            });
        }
    }

    private static DungeonItemDefinition ResolveDefinition(
        string itemId,
        IDungeonItemCatalogProvider catalogProvider)
    {
        if (catalogProvider != null)
        {
            return catalogProvider.GetDefinition(itemId);
        }

        return new DungeonItemDefinition(
            itemId,
            itemId,
            string.Empty,
            StockCategory.General,
            0,
            null,
            1f,
            1);
    }
}
