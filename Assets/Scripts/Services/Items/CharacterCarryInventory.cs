using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public interface ICharacterCarryInventoryRegistry
{
    void Register(CharacterCarryInventory inventory);
    void Unregister(CharacterCarryInventory inventory);
    CharacterCarryInventory Find(CharacterId characterId);
}

public sealed class CharacterCarryInventoryRegistry :
    ICharacterRuntimeTransientStateRegistry,
    IDisposable
{
    private sealed class CharacterSkillState
    {
        public readonly HashSet<string> ExecutingKeys =
            new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> ExecutedEventKeys =
            new HashSet<string>(StringComparer.Ordinal);
        public WorkTypeId ActiveWorkTypeId;
        public float WorkSpeedMultiplier = 1f;
    }

    private readonly HashSet<CharacterCarryInventory> activeInventories =
        new HashSet<CharacterCarryInventory>();
    private readonly Dictionary<CharacterId, CharacterSkillState> skillStates =
        new Dictionary<CharacterId, CharacterSkillState>();
    private bool disposed;

    public void Register(CharacterCarryInventory inventory)
    {
        if (!disposed && inventory != null) activeInventories.Add(inventory);
    }

    public void Unregister(CharacterCarryInventory inventory)
    {
        if (!disposed && inventory != null) activeInventories.Remove(inventory);
    }

    public CharacterCarryInventory Find(CharacterId characterId)
    {
        if (disposed || !characterId.IsValid) return null;
        activeInventories.RemoveWhere(inventory => inventory == null);
        return activeInventories.FirstOrDefault(inventory =>
            inventory.CharacterId.Equals(characterId));
    }

    public bool TryEnter(CharacterId characterId, string key)
    {
        ThrowIfDisposed();
        RequireCharacterId(characterId);
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        CharacterSkillState state = GetOrCreateSkillState(characterId);
        if (state.ExecutingKeys.Contains(key)
            || state.ExecutedEventKeys.Contains(key))
        {
            return false;
        }

        state.ExecutingKeys.Add(key);
        state.ExecutedEventKeys.Add(key);
        return true;
    }

    public void Exit(CharacterId characterId, string key)
    {
        ThrowIfDisposed();
        if (!characterId.IsValid || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        if (skillStates.TryGetValue(characterId, out CharacterSkillState state))
        {
            state.ExecutingKeys.Remove(key);
        }
    }

    public void BeginWork(
        CharacterId characterId,
        WorkTypeId workTypeId,
        float speedMultiplier)
    {
        ThrowIfDisposed();
        RequireCharacterId(characterId);
        CharacterSkillState state = GetOrCreateSkillState(characterId);
        state.ActiveWorkTypeId = workTypeId;
        state.WorkSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
    }

    public void EndWork(CharacterId characterId)
    {
        ThrowIfDisposed();
        if (!characterId.IsValid
            || !skillStates.TryGetValue(characterId, out CharacterSkillState state))
        {
            return;
        }

        state.ActiveWorkTypeId = default;
        state.WorkSpeedMultiplier = 1f;
    }

    public float GetWorkSpeedMultiplier(CharacterId characterId)
    {
        ThrowIfDisposed();
        return characterId.IsValid
            && skillStates.TryGetValue(characterId, out CharacterSkillState state)
            && state.ActiveWorkTypeId.IsValid
                ? Mathf.Max(0.1f, state.WorkSpeedMultiplier)
                : 1f;
    }

    public void Reset(CharacterId characterId)
    {
        ThrowIfDisposed();
        if (characterId.IsValid)
        {
            skillStates.Remove(characterId);
        }
    }

    public void ResetAll()
    {
        ThrowIfDisposed();
        skillStates.Clear();
    }

    public void Dispose()
    {
        if (disposed) return;
        activeInventories.Clear();
        skillStates.Clear();
        disposed = true;
    }

    private CharacterSkillState GetOrCreateSkillState(CharacterId characterId)
    {
        if (!skillStates.TryGetValue(characterId, out CharacterSkillState state))
        {
            state = new CharacterSkillState();
            skillStates.Add(characterId, state);
        }

        return state;
    }

    private static void RequireCharacterId(CharacterId characterId)
    {
        if (!characterId.IsValid)
        {
            throw new InvalidOperationException(
                "Character skill execution state requires a persistent character ID.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(CharacterCarryInventoryRegistry));
        }
    }
}

[DisallowMultipleComponent]
public sealed class CharacterCarryInventory : MonoBehaviour, ICombatAmmunitionInventory
{
    [SerializeField] private List<CharacterCarriedItemSaveData> carriedItems =
        new List<CharacterCarriedItemSaveData>();

    private CharacterActor actor;
    private IDungeonItemCatalogProvider catalogProvider;
    private IItemHaulingSettingsProvider haulingSettingsProvider;
    private ICharacterCarryInventoryRegistry registry;

    public IReadOnlyList<CharacterCarriedItemSaveData> Items => carriedItems;
    public bool HasItems => carriedItems.Any(item => item != null && item.quantity > 0);
    internal CharacterId CharacterId => actor?.Identity?.TypedPersistentId ?? default;
    public event Action Changed;

    private void Awake()
    {
        actor = GetComponent<CharacterActor>();
    }

    private void OnEnable()
    {
        registry?.Register(this);
    }

    private void OnDisable()
    {
        registry?.Unregister(this);
    }

    [Inject]
    public void Construct(ICharacterCarryInventoryRegistry registry)
    {
        this.registry?.Unregister(this);
        this.registry = registry
            ?? throw new ArgumentNullException(nameof(registry));
        if (isActiveAndEnabled) registry.Register(this);
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
        IItemHaulingSettingsProvider haulingSettingsProvider,
        ICharacterCarryInventoryRegistry registry)
    {
        this.catalogProvider = catalogProvider
            ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.haulingSettingsProvider = haulingSettingsProvider
            ?? throw new ArgumentNullException(nameof(haulingSettingsProvider));
        Construct(registry);
    }

    public float GetBaseCarryLimit()
    {
        CharacterStats stats = actor != null ? actor.Stats : null;
        int strength = stats != null ? stats.GetCharacterStat(CharacterStatType.Strength) : 5;
        int endurance = stats != null ? stats.GetCharacterStat(CharacterStatType.Endurance) : 5;
        return 8f + (strength * 1.5f) + (endurance * 0.75f);
    }

    public float GetMaxAllowedWeight(IItemHaulingSettingsProvider settingsProvider)
    {
        float multiplier = (settingsProvider
            ?? throw new ArgumentNullException(nameof(settingsProvider)))
            .MaxCarryMultiplier;
        return GetBaseCarryLimit() * Mathf.Clamp(multiplier, 1f, 2.5f);
    }

    public float GetMaxAllowedWeight() =>
        GetMaxAllowedWeight(RequireHaulingSettings());

    public float GetCurrentWeight(IDungeonItemCatalogProvider catalogProvider)
    {
        if (catalogProvider == null)
        {
            throw new ArgumentNullException(nameof(catalogProvider));
        }

        float total = 0f;
        foreach (CharacterCarriedItemSaveData item in carriedItems)
        {
            if (item == null || item.quantity <= 0)
            {
                continue;
            }

            DungeonItemDefinition definition = ResolveDefinition(
                item.itemId,
                catalogProvider);
            total += definition.UnitWeight * Mathf.Max(0, item.quantity);
        }

        return total;
    }

    public float GetCurrentWeight() => GetCurrentWeight(RequireCatalog());

    public float GetMoveSpeedMultiplier(
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider settingsProvider)
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

    public float GetMoveSpeedMultiplier() =>
        GetMoveSpeedMultiplier(RequireCatalog(), RequireHaulingSettings());

    public float GetLoadRatio(
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider settingsProvider)
    {
        float maxAllowed = Mathf.Max(0.01f, GetMaxAllowedWeight(settingsProvider));
        return Mathf.Clamp01(GetCurrentWeight(catalogProvider) / maxAllowed);
    }

    public float GetLoadRatio() =>
        GetLoadRatio(RequireCatalog(), RequireHaulingSettings());

    private IDungeonItemCatalogProvider RequireCatalog() => catalogProvider
        ?? throw new InvalidOperationException(
            $"{nameof(CharacterCarryInventory)} requires an item catalog.");

    private IItemHaulingSettingsProvider RequireHaulingSettings() =>
        haulingSettingsProvider
        ?? throw new InvalidOperationException(
            $"{nameof(CharacterCarryInventory)} requires hauling settings.");

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
        return TryAddPartialStack(
            sourceStackId,
            itemId,
            quantity,
            catalogProvider,
            settingsProvider,
            WasteOriginKind.Unknown,
            0f,
            out acceptedQuantity,
            out failureReason);
    }

    public bool TryAddPartialStack(
        string sourceStackId,
        string itemId,
        int quantity,
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider settingsProvider,
        WasteOriginKind wasteOrigin,
        float contamination,
        out int acceptedQuantity,
        out string failureReason)
    {
        return TryAddPartialStack(
            sourceStackId,
            string.Empty,
            itemId,
            quantity,
            catalogProvider,
            settingsProvider,
            wasteOrigin,
            contamination,
            null,
            out acceptedQuantity,
            out failureReason);
    }

    public bool TryAddPartialStack(
        string sourceStackId,
        string itemInstanceId,
        string itemId,
        int quantity,
        IDungeonItemCatalogProvider catalogProvider,
        IItemHaulingSettingsProvider settingsProvider,
        WasteOriginKind wasteOrigin,
        float contamination,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int acceptedQuantity,
        out string failureReason)
    {
        failureReason = string.Empty;
        ItemInstanceId typedInstanceId = (ItemInstanceId)itemInstanceId;
        DungeonItemDefinition definition = ResolveDefinition(
            itemId,
            catalogProvider ?? this.catalogProvider);
        if (!string.IsNullOrWhiteSpace(itemInstanceId)
            && (!typedInstanceId.IsValid || definition.MaxStack != 1 || quantity != 1))
        {
            acceptedQuantity = 0;
            failureReason = "invalid unique item identity";
            return false;
        }
        acceptedQuantity = GetMaxAcceptableQuantity(itemId, quantity, catalogProvider, settingsProvider);
        if (acceptedQuantity <= 0)
        {
            failureReason = "carry limit";
            return false;
        }

        string incomingSignature = typedInstanceId.IsValid
            ? $"{ItemStackSignature.Create(itemId, components)}#instance={typedInstanceId.Value}"
            : ItemStackSignature.Create(itemId, components);
        CharacterCarriedItemSaveData existing = carriedItems.FirstOrDefault(item => item != null
            && string.Equals(item.itemId, itemId, StringComparison.Ordinal)
            && string.Equals(item.sourceStackId, sourceStackId, StringComparison.Ordinal)
            && string.Equals(item.itemInstanceId, typedInstanceId.Value, StringComparison.Ordinal)
            && item.wasteOrigin == wasteOrigin
            && Mathf.Abs(item.contamination - contamination) < 0.01f
            && string.Equals(
                item.GetStackSignature(),
                incomingSignature,
                StringComparison.Ordinal));
        if (existing == null)
        {
            carriedItems.Add(new CharacterCarriedItemSaveData
            {
                sourceStackId = sourceStackId ?? string.Empty,
                itemInstanceId = typedInstanceId.IsValid ? typedInstanceId.Value : string.Empty,
                itemId = itemId ?? string.Empty,
                quantity = acceptedQuantity,
                wasteOrigin = wasteOrigin,
                contamination = Mathf.Clamp(contamination, 0f, 100f),
                components = (components ?? Array.Empty<ItemInstanceComponentSaveData>())
                    .Where(component => component != null)
                    .Select(component => component.Clone())
                    .ToList()
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

        Changed?.Invoke();
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

        bool consumedAll = remaining == 0;
        if (consumedAll)
        {
            Changed?.Invoke();
        }

        return consumedAll;
    }

    public bool TryConsumeSourceStack(string sourceStackId, string itemId, int quantity = 1)
    {
        int remaining = Mathf.Max(0, quantity);
        int requested = remaining;
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

        bool consumedAll = remaining == 0;
        if (remaining < requested)
        {
            Changed?.Invoke();
        }

        return consumedAll;
    }

    public List<CharacterCarriedItemSaveData> RemoveAllItems()
    {
        List<CharacterCarriedItemSaveData> result = carriedItems
            .Where(item => item != null && item.quantity > 0)
            .Select(item => new CharacterCarriedItemSaveData
            {
                sourceStackId = item.sourceStackId,
                itemInstanceId = item.itemInstanceId,
                itemId = item.itemId,
                quantity = item.quantity,
                wasteOrigin = item.wasteOrigin,
                contamination = item.contamination,
                components = (item.components ?? new List<ItemInstanceComponentSaveData>())
                    .Where(component => component != null)
                    .Select(component => component.Clone())
                    .ToList()
            })
            .ToList();
        if (carriedItems.Count > 0)
        {
            carriedItems.Clear();
            Changed?.Invoke();
        }
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
                    itemInstanceId = item.itemInstanceId,
                    itemId = item.itemId,
                    quantity = Mathf.Max(0, item.quantity),
                    wasteOrigin = item.wasteOrigin,
                    contamination = item.contamination,
                    components = (item.components ?? new List<ItemInstanceComponentSaveData>())
                        .Where(component => component != null)
                        .Select(component => component.Clone())
                        .ToList()
                })
                .ToList()
        };
    }

    public void Restore(CharacterCarryInventorySaveData snapshot)
    {
        carriedItems.Clear();
        HashSet<string> restoredInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterCarriedItemSaveData item in snapshot?.items ?? new List<CharacterCarriedItemSaveData>())
        {
            if (item == null || item.quantity <= 0 || string.IsNullOrWhiteSpace(item.itemId))
            {
                continue;
            }

            ItemInstanceId itemInstanceId = (ItemInstanceId)item.itemInstanceId;
            if (!string.IsNullOrWhiteSpace(item.itemInstanceId)
                && (!itemInstanceId.IsValid
                    || item.quantity != 1
                    || !restoredInstanceIds.Add(itemInstanceId.Value)))
            {
                throw new InvalidOperationException(
                    $"Invalid or duplicate carried item-instance ID '{item.itemInstanceId}'.");
            }

            carriedItems.Add(new CharacterCarriedItemSaveData
            {
                sourceStackId = item.sourceStackId ?? string.Empty,
                itemInstanceId = itemInstanceId.IsValid
                    ? itemInstanceId.Value
                    : string.Empty,
                itemId = item.itemId.Trim(),
                quantity = Mathf.Max(0, item.quantity),
                wasteOrigin = item.wasteOrigin,
                contamination = Mathf.Clamp(item.contamination, 0f, 100f),
                components = (item.components ?? new List<ItemInstanceComponentSaveData>())
                    .Where(component => component != null)
                    .Select(component => component.Clone())
                    .ToList()
            });
        }

        Changed?.Invoke();
    }

    private static DungeonItemDefinition ResolveDefinition(
        string itemId,
        IDungeonItemCatalogProvider catalogProvider)
    {
        return (catalogProvider
                ?? throw new InvalidOperationException(
                    "Character carry inventory requires an item catalog."))
            .GetDefinition(itemId);
    }
}
