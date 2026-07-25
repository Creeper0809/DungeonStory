using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public enum ExpeditionEquipmentSlot
{
    Weapon,
    Armor
}

[Serializable]
public sealed class ExpeditionEquipmentStatBlock
{
    public int maxHealth;
    public int attack;
    public int strength;
    public int toughness;
    public int dexterity;
    public int moveSpeed;

    public static ExpeditionEquipmentStatBlock Empty => new ExpeditionEquipmentStatBlock();

    public ExpeditionEquipmentStatBlock Clone()
    {
        return new ExpeditionEquipmentStatBlock
        {
            maxHealth = maxHealth,
            attack = attack,
            strength = strength,
            toughness = toughness,
            dexterity = dexterity,
            moveSpeed = moveSpeed
        };
    }

    public void Add(ExpeditionEquipmentStatBlock other)
    {
        if (other == null)
        {
            return;
        }

        maxHealth += other.maxHealth;
        attack += other.attack;
        strength += other.strength;
        toughness += other.toughness;
        dexterity += other.dexterity;
        moveSpeed += other.moveSpeed;
    }
}

[Serializable]
public sealed class ExpeditionEquipmentCraftCost
{
    public StockCategory category = StockCategory.Weapon;
    [Min(0)] public int amount;
}

[Serializable]
public sealed class ExpeditionEquipmentDefinition
{
    public string id = string.Empty;
    public string displayName = string.Empty;
    public ExpeditionEquipmentSlot slot;
    public ExpeditionEquipmentStatBlock stats = new ExpeditionEquipmentStatBlock();
    public List<ExpeditionEquipmentCraftCost> craftCosts = new List<ExpeditionEquipmentCraftCost>();
    [Min(0.1f)] public float craftSeconds = 6f;

    public bool IsValid => !string.IsNullOrWhiteSpace(id)
        && !string.IsNullOrWhiteSpace(displayName);
}

public interface IExpeditionEquipmentCatalogProvider
{
    ExpeditionEquipmentCatalogSO Catalog { get; }
}

public sealed class ResourceExpeditionEquipmentCatalogProvider : IExpeditionEquipmentCatalogProvider
{
    public const string ResourcePath = "Config/ExpeditionEquipmentCatalog";

    private readonly IResourcesAssetLoader resourcesAssetLoader;
    private readonly ICombatEquipmentCatalog combatEquipmentCatalog;
    private ExpeditionEquipmentCatalogSO catalog;

    public ResourceExpeditionEquipmentCatalogProvider(
        IResourcesAssetLoader resourcesAssetLoader = null,
        ICombatEquipmentCatalog combatEquipmentCatalog = null)
    {
        this.resourcesAssetLoader = resourcesAssetLoader ?? new UnityResourcesAssetLoader();
        this.combatEquipmentCatalog = combatEquipmentCatalog ?? new ResourceCombatEquipmentCatalog(
            this.resourcesAssetLoader);
    }

    public ExpeditionEquipmentCatalogSO Catalog
    {
        get
        {
            if (catalog == null)
            {
                catalog = resourcesAssetLoader.LoadOptional<ExpeditionEquipmentCatalogSO>(ResourcePath)
                    ?? ExpeditionEquipmentCatalogSO.CreateRuntimeDefaults();
                catalog.EnsureCombatCompatibilityDefinitions(combatEquipmentCatalog);
            }

            return catalog;
        }
    }
}

[Serializable]
public sealed class ExpeditionEquipmentInventoryEntry
{
    public string equipmentId = string.Empty;
    public int quantity;
}

[Serializable]
public sealed class ExpeditionEquipmentLoadoutSaveData
{
    public string characterId = string.Empty;
    public string weaponId = string.Empty;
    public string armorId = string.Empty;
}

[Serializable]
public sealed class ExpeditionEquipmentCraftOrderSaveData
{
    public string orderId = string.Empty;
    public string equipmentId = string.Empty;
    public float remainingSeconds;
    public bool materialsReady = true;
    public string materialDestinationId = string.Empty;
    public int materialDestinationX;
    public int materialDestinationY;

    public ExpeditionEquipmentCraftOrderSaveData Clone()
    {
        return new ExpeditionEquipmentCraftOrderSaveData
        {
            orderId = orderId ?? string.Empty,
            equipmentId = equipmentId ?? string.Empty,
            remainingSeconds = Mathf.Max(0f, remainingSeconds),
            materialsReady = materialsReady,
            materialDestinationId = materialDestinationId ?? string.Empty,
            materialDestinationX = materialDestinationX,
            materialDestinationY = materialDestinationY
        };
    }
}

[Serializable]
public sealed class ExpeditionEquipmentSaveData
{
    public List<ExpeditionEquipmentInventoryEntry> inventory = new List<ExpeditionEquipmentInventoryEntry>();
    public List<ExpeditionEquipmentLoadoutSaveData> loadouts = new List<ExpeditionEquipmentLoadoutSaveData>();
    public List<ExpeditionEquipmentCraftOrderSaveData> craftQueue =
        new List<ExpeditionEquipmentCraftOrderSaveData>();
}

public interface IExpeditionEquipmentRuntime
{
    IReadOnlyList<ExpeditionEquipmentDefinition> Definitions { get; }
    IReadOnlyDictionary<string, int> Inventory { get; }
    IReadOnlyList<ExpeditionEquipmentCraftOrderSaveData> CraftQueue { get; }
    bool TryGetDefinition(string equipmentId, out ExpeditionEquipmentDefinition definition);
    bool TryGetEquipped(string characterId, ExpeditionEquipmentSlot slot, out string equipmentId);
    ExpeditionEquipmentStatBlock GetCombatBonuses(string characterId);
    bool TryEquip(string characterId, string equipmentId, out string message);
    void Unequip(string characterId, ExpeditionEquipmentSlot slot);
    void AddInventory(string equipmentId, int quantity);
    int GetAvailableCount(string equipmentId);
    void HandleCharacterDeath(string characterId);
    bool TryQueueCraft(string equipmentId, out string message);
    bool TryQueueCraft(string equipmentId, BuildableObject craftingFacility, out string message);
    bool HasPendingCraftWork(IEnumerable<string> craftableEquipmentIds);
    int ApplyCraftWork(
        IEnumerable<string> craftableEquipmentIds,
        float workSeconds,
        out string completedEquipmentId,
        bool addCompletedToInventory = true);
    ExpeditionEquipmentSaveData Capture();
    void Restore(ExpeditionEquipmentSaveData saveData);
}

public sealed class ExpeditionEquipmentRuntime : IExpeditionEquipmentRuntime, IDisposable
{
    private sealed class RuntimeLoadout
    {
        public string WeaponId = string.Empty;
        public string ArmorId = string.Empty;
    }

    private readonly IExpeditionEquipmentCatalogProvider catalogProvider;
    private readonly IFacilityEvolutionWarehouseInventoryQuery inventoryQuery;
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IDisposable equipmentStoredSubscription;
    private readonly Dictionary<string, int> inventory = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, RuntimeLoadout> loadoutsByCharacter =
        new Dictionary<string, RuntimeLoadout>(StringComparer.Ordinal);
    private readonly List<ExpeditionEquipmentCraftOrderSaveData> craftQueue =
        new List<ExpeditionEquipmentCraftOrderSaveData>();
    private IReadOnlyList<ExpeditionEquipmentCraftOrderSaveData> craftQueueView;

    public ExpeditionEquipmentRuntime(
        IExpeditionEquipmentCatalogProvider catalogProvider,
        IFacilityEvolutionWarehouseInventoryQuery inventoryQuery = null,
        IWorldItemStackRuntime itemStackRuntime = null,
        IGameEventBus gameEventBus = null)
    {
        this.catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
        this.inventoryQuery = inventoryQuery;
        this.itemStackRuntime = itemStackRuntime;
        equipmentStoredSubscription = gameEventBus?.Subscribe<EquipmentStoredEvent>(
            OnEquipmentStored);
    }

    public IReadOnlyDictionary<string, int> Inventory => inventory;
    public IReadOnlyList<ExpeditionEquipmentDefinition> Definitions => catalogProvider.Catalog.Equipment;
    public IReadOnlyList<ExpeditionEquipmentCraftOrderSaveData> CraftQueue =>
        craftQueueView ??= craftQueue.AsReadOnly();

    public void Dispose()
    {
        equipmentStoredSubscription?.Dispose();
    }

    private void OnEquipmentStored(EquipmentStoredEvent stored)
    {
        if (!string.IsNullOrWhiteSpace(stored.EquipmentId)
            && stored.Quantity > 0)
        {
            AddInventory(stored.EquipmentId, stored.Quantity);
        }
    }

    public bool TryGetDefinition(string equipmentId, out ExpeditionEquipmentDefinition definition)
    {
        return catalogProvider.Catalog.TryGet(equipmentId, out definition);
    }

    public bool TryGetEquipped(string characterId, ExpeditionEquipmentSlot slot, out string equipmentId)
    {
        equipmentId = string.Empty;
        if (string.IsNullOrWhiteSpace(characterId)
            || !loadoutsByCharacter.TryGetValue(characterId, out RuntimeLoadout loadout))
        {
            return false;
        }

        equipmentId = GetSlot(loadout, slot);
        return !string.IsNullOrWhiteSpace(equipmentId);
    }

    public ExpeditionEquipmentStatBlock GetCombatBonuses(string characterId)
    {
        ExpeditionEquipmentStatBlock result = new ExpeditionEquipmentStatBlock();
        if (string.IsNullOrWhiteSpace(characterId)
            || !loadoutsByCharacter.TryGetValue(characterId, out RuntimeLoadout loadout))
        {
            return result;
        }

        AddStats(loadout.WeaponId, result);
        AddStats(loadout.ArmorId, result);
        return result;
    }

    public bool TryEquip(string characterId, string equipmentId, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            message = "character-id-missing";
            return false;
        }

        if (!catalogProvider.Catalog.TryGet(equipmentId, out ExpeditionEquipmentDefinition definition))
        {
            message = "equipment-not-found";
            return false;
        }

        RuntimeLoadout loadout = GetOrCreateLoadout(characterId);
        string previous = GetSlot(loadout, definition.slot);
        if (!string.Equals(previous, equipmentId, StringComparison.Ordinal)
            && GetAvailableCount(equipmentId) <= 0)
        {
            message = "equipment-not-available";
            return false;
        }

        SetSlot(loadout, definition.slot, equipmentId);
        return true;
    }

    public void Unequip(string characterId, ExpeditionEquipmentSlot slot)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || !loadoutsByCharacter.TryGetValue(characterId, out RuntimeLoadout loadout))
        {
            return;
        }

        SetSlot(loadout, slot, string.Empty);
    }

    public void AddInventory(string equipmentId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(equipmentId) || quantity == 0)
        {
            return;
        }

        inventory.TryGetValue(equipmentId, out int current);
        int next = Mathf.Max(0, current + quantity);
        if (next == 0)
        {
            inventory.Remove(equipmentId);
        }
        else
        {
            inventory[equipmentId] = next;
        }
    }

    public int GetAvailableCount(string equipmentId)
    {
        if (string.IsNullOrWhiteSpace(equipmentId))
        {
            return 0;
        }

        inventory.TryGetValue(equipmentId, out int total);
        int reserved = loadoutsByCharacter.Values.Count(loadout =>
            string.Equals(loadout.WeaponId, equipmentId, StringComparison.Ordinal)
            || string.Equals(loadout.ArmorId, equipmentId, StringComparison.Ordinal));
        return Mathf.Max(0, total - reserved);
    }

    public void HandleCharacterDeath(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || !loadoutsByCharacter.TryGetValue(characterId, out RuntimeLoadout loadout))
        {
            return;
        }

        ConsumeEquipped(loadout.WeaponId);
        ConsumeEquipped(loadout.ArmorId);
        loadoutsByCharacter.Remove(characterId);
    }

    public bool TryQueueCraft(string equipmentId, out string message)
    {
        return TryQueueCraft(equipmentId, null, out message);
    }

    public bool TryQueueCraft(string equipmentId, BuildableObject craftingFacility, out string message)
    {
        message = string.Empty;
        if (!catalogProvider.Catalog.TryGet(equipmentId, out ExpeditionEquipmentDefinition definition))
        {
            message = "equipment-not-found";
            return false;
        }

        string orderId = Guid.NewGuid().ToString("N");
        bool usePhysicalMaterials = itemStackRuntime != null
            && craftingFacility != null
            && HasCraftCosts(definition);
        if (usePhysicalMaterials)
        {
            string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + orderId;
            if (!TryRequestCraftMaterialDeliveries(
                    definition,
                    craftingFacility.centerPos,
                    destinationId,
                    out message))
            {
                return false;
            }

            craftQueue.Add(new ExpeditionEquipmentCraftOrderSaveData
            {
                orderId = orderId,
                equipmentId = definition.id,
                remainingSeconds = Mathf.Max(0.1f, definition.craftSeconds),
                materialsReady = false,
                materialDestinationId = destinationId,
                materialDestinationX = craftingFacility.centerPos.x,
                materialDestinationY = craftingFacility.centerPos.y
            });
            return true;
        }

        if (!TryWithdrawCraftCosts(definition, out message))
        {
            return false;
        }

        craftQueue.Add(new ExpeditionEquipmentCraftOrderSaveData
        {
            orderId = orderId,
            equipmentId = definition.id,
            remainingSeconds = Mathf.Max(0.1f, definition.craftSeconds),
            materialsReady = true
        });
        return true;
    }

    public bool HasPendingCraftWork(IEnumerable<string> craftableEquipmentIds)
    {
        return craftQueue.Any(order => order != null
            && order.remainingSeconds > 0f
            && IsCraftable(order.equipmentId, craftableEquipmentIds)
            && TryEnsureCraftMaterialsReady(order));
    }

    public int ApplyCraftWork(
        IEnumerable<string> craftableEquipmentIds,
        float workSeconds,
        out string completedEquipmentId,
        bool addCompletedToInventory = true)
    {
        completedEquipmentId = string.Empty;
        float safeWorkSeconds = Mathf.Max(0f, workSeconds);
        if (safeWorkSeconds <= 0f)
        {
            return 0;
        }

        for (int i = 0; i < craftQueue.Count; i++)
        {
            ExpeditionEquipmentCraftOrderSaveData order = craftQueue[i];
            if (order == null || !IsCraftable(order.equipmentId, craftableEquipmentIds))
            {
                continue;
            }

            if (!TryEnsureCraftMaterialsReady(order))
            {
                continue;
            }

            order.remainingSeconds = Mathf.Max(0f, order.remainingSeconds - safeWorkSeconds);
            if (order.remainingSeconds > 0f)
            {
                return 0;
            }

            completedEquipmentId = order.equipmentId;
            if (addCompletedToInventory)
            {
                AddInventory(order.equipmentId, 1);
            }

            craftQueue.RemoveAt(i);
            return 1;
        }

        return 0;
    }

    public ExpeditionEquipmentSaveData Capture()
    {
        return new ExpeditionEquipmentSaveData
        {
            inventory = inventory
                .Where(pair => pair.Value > 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ExpeditionEquipmentInventoryEntry
                {
                    equipmentId = pair.Key,
                    quantity = pair.Value
                })
                .ToList(),
            loadouts = loadoutsByCharacter
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.WeaponId)
                    || !string.IsNullOrWhiteSpace(pair.Value.ArmorId))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new ExpeditionEquipmentLoadoutSaveData
                {
                    characterId = pair.Key,
                    weaponId = pair.Value.WeaponId,
                    armorId = pair.Value.ArmorId
                })
                .ToList(),
            craftQueue = craftQueue
                .Where(order => order != null
                    && !string.IsNullOrWhiteSpace(order.equipmentId)
                    && order.remainingSeconds > 0f)
                .Select(order => order.Clone())
                .ToList()
        };
    }

    public void Restore(ExpeditionEquipmentSaveData saveData)
    {
        inventory.Clear();
        loadoutsByCharacter.Clear();
        craftQueue.Clear();
        foreach (ExpeditionEquipmentInventoryEntry entry in saveData?.inventory
            ?? new List<ExpeditionEquipmentInventoryEntry>())
        {
            if (!string.IsNullOrWhiteSpace(entry?.equipmentId) && entry.quantity > 0)
            {
                inventory[entry.equipmentId] = entry.quantity;
            }
        }

        HashSet<string> seenCharacters = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExpeditionEquipmentLoadoutSaveData loadout in saveData?.loadouts
            ?? new List<ExpeditionEquipmentLoadoutSaveData>())
        {
            if (loadout == null || string.IsNullOrWhiteSpace(loadout.characterId))
            {
                continue;
            }

            if (!seenCharacters.Add(loadout.characterId))
            {
                throw new InvalidOperationException($"Duplicate equipment loadout for '{loadout.characterId}'.");
            }

            RuntimeLoadout runtimeLoadout = GetOrCreateLoadout(loadout.characterId);
            runtimeLoadout.WeaponId = catalogProvider.Catalog.TryGet(loadout.weaponId, out _)
                ? loadout.weaponId
                : string.Empty;
            runtimeLoadout.ArmorId = catalogProvider.Catalog.TryGet(loadout.armorId, out _)
                ? loadout.armorId
                : string.Empty;
        }

        foreach (string equipmentId in inventory.Keys.ToArray())
        {
            int reserved = loadoutsByCharacter.Values.Count(loadout =>
                string.Equals(loadout.WeaponId, equipmentId, StringComparison.Ordinal)
                || string.Equals(loadout.ArmorId, equipmentId, StringComparison.Ordinal));
            if (reserved > inventory[equipmentId])
            {
                throw new InvalidOperationException(
                    $"Equipment '{equipmentId}' has {reserved} reserved but only {inventory[equipmentId]} owned.");
            }
        }

        HashSet<string> seenOrders = new HashSet<string>(StringComparer.Ordinal);
        foreach (ExpeditionEquipmentCraftOrderSaveData order in saveData?.craftQueue
            ?? new List<ExpeditionEquipmentCraftOrderSaveData>())
        {
            if (order == null
                || string.IsNullOrWhiteSpace(order.equipmentId)
                || order.remainingSeconds <= 0f
                || !catalogProvider.Catalog.TryGet(order.equipmentId, out _))
            {
                continue;
            }

            string orderId = string.IsNullOrWhiteSpace(order.orderId)
                ? Guid.NewGuid().ToString("N")
                : order.orderId;
            if (!seenOrders.Add(orderId))
            {
                throw new InvalidOperationException($"Duplicate equipment craft order '{orderId}'.");
            }

            craftQueue.Add(new ExpeditionEquipmentCraftOrderSaveData
            {
                orderId = orderId,
                equipmentId = order.equipmentId,
                remainingSeconds = Mathf.Max(0.1f, order.remainingSeconds),
                materialsReady = order.materialsReady,
                materialDestinationId = order.materialDestinationId ?? string.Empty,
                materialDestinationX = order.materialDestinationX,
                materialDestinationY = order.materialDestinationY
            });
        }
    }

    private RuntimeLoadout GetOrCreateLoadout(string characterId)
    {
        if (!loadoutsByCharacter.TryGetValue(characterId, out RuntimeLoadout loadout))
        {
            loadout = new RuntimeLoadout();
            loadoutsByCharacter[characterId] = loadout;
        }

        return loadout;
    }

    private void AddStats(string equipmentId, ExpeditionEquipmentStatBlock result)
    {
        if (!string.IsNullOrWhiteSpace(equipmentId)
            && catalogProvider.Catalog.TryGet(equipmentId, out ExpeditionEquipmentDefinition definition))
        {
            result.Add(definition.stats);
        }
    }

    private void ConsumeEquipped(string equipmentId)
    {
        if (string.IsNullOrWhiteSpace(equipmentId)
            || !inventory.TryGetValue(equipmentId, out int quantity))
        {
            return;
        }

        if (quantity <= 1)
        {
            inventory.Remove(equipmentId);
        }
        else
        {
            inventory[equipmentId] = quantity - 1;
        }
    }

    private bool TryRequestCraftMaterialDeliveries(
        ExpeditionEquipmentDefinition definition,
        Vector2Int destinationPosition,
        string destinationId,
        out string message)
    {
        message = string.Empty;
        Dictionary<StockCategory, int> costs = BuildCraftCostMap(definition);
        if (costs.Count == 0)
        {
            return true;
        }

        if (!HasAvailableCraftCosts(costs))
        {
            message = "craft-cost-not-available";
            return false;
        }

        if (itemStackRuntime == null)
        {
            message = "physical-item-runtime-unavailable";
            return false;
        }

        foreach (KeyValuePair<StockCategory, int> pair in costs)
        {
            if (!itemStackRuntime.TryRequestFacilityDelivery(
                    pair.Key,
                    pair.Value,
                    destinationPosition,
                    destinationId,
                    out int requested,
                    out string reason)
                || requested < pair.Value)
            {
                message = string.IsNullOrWhiteSpace(reason)
                    ? "craft-cost-delivery-failed"
                    : reason;
                return false;
            }
        }

        return true;
    }

    private bool TryEnsureCraftMaterialsReady(ExpeditionEquipmentCraftOrderSaveData order)
    {
        if (order == null)
        {
            return false;
        }

        if (order.materialsReady)
        {
            return true;
        }

        if (!catalogProvider.Catalog.TryGet(order.equipmentId, out ExpeditionEquipmentDefinition definition))
        {
            return false;
        }

        Dictionary<StockCategory, int> costs = BuildCraftCostMap(definition);
        if (costs.Count == 0)
        {
            order.materialsReady = true;
            return true;
        }

        if (itemStackRuntime == null
            || string.IsNullOrWhiteSpace(order.materialDestinationId))
        {
            return false;
        }

        if (!itemStackRuntime.TryConsumeFacilityBuffer(
                order.materialDestinationId,
                costs,
                out _))
        {
            return false;
        }

        order.materialsReady = true;
        return true;
    }

    private bool HasCraftCosts(ExpeditionEquipmentDefinition definition)
    {
        return BuildCraftCostMap(definition).Count > 0;
    }

    private bool HasAvailableCraftCosts(IReadOnlyDictionary<StockCategory, int> costs)
    {
        if (costs == null || costs.Count == 0 || inventoryQuery == null)
        {
            return true;
        }

        WarehouseInventory[] inventories = inventoryQuery.GetInventories()
            .Where(inventory => inventory != null)
            .ToArray();
        foreach (KeyValuePair<StockCategory, int> cost in costs)
        {
            int available = inventories.Sum(inventory => inventory.GetStock(cost.Key));
            if (available < cost.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<StockCategory, int> BuildCraftCostMap(ExpeditionEquipmentDefinition definition)
    {
        Dictionary<StockCategory, int> result = new Dictionary<StockCategory, int>();
        foreach (ExpeditionEquipmentCraftCost cost in definition?.craftCosts
            ?? new List<ExpeditionEquipmentCraftCost>())
        {
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            result.TryGetValue(cost.category, out int current);
            result[cost.category] = current + Mathf.Max(0, cost.amount);
        }

        return result;
    }

    private bool TryWithdrawCraftCosts(
        ExpeditionEquipmentDefinition definition,
        out string message)
    {
        message = string.Empty;
        List<ExpeditionEquipmentCraftCost> costs = definition?.craftCosts?
            .Where(cost => cost != null && cost.amount > 0)
            .ToList() ?? new List<ExpeditionEquipmentCraftCost>();
        if (costs.Count == 0 || inventoryQuery == null)
        {
            return true;
        }

        WarehouseInventory[] inventories = inventoryQuery.GetInventories()
            .Where(inventory => inventory != null)
            .ToArray();
        foreach (ExpeditionEquipmentCraftCost cost in costs)
        {
            int available = inventories.Sum(inventory => inventory.GetStock(cost.category));
            if (available < cost.amount)
            {
                message = "craft-cost-not-available";
                return false;
            }
        }

        List<Withdrawal> withdrawals = new List<Withdrawal>();
        foreach (ExpeditionEquipmentCraftCost cost in costs)
        {
            int remaining = cost.amount;
            foreach (WarehouseInventory inventory in inventories)
            {
                int amount = inventory.Withdraw(cost.category, remaining);
                if (amount > 0)
                {
                    withdrawals.Add(new Withdrawal(inventory, cost.category, amount));
                    remaining -= amount;
                }

                if (remaining <= 0)
                {
                    break;
                }
            }

            if (remaining <= 0)
            {
                continue;
            }

            foreach (Withdrawal withdrawal in withdrawals)
            {
                withdrawal.Inventory.AddStock(withdrawal.Category, withdrawal.Amount);
            }

            message = "craft-cost-withdraw-failed";
            return false;
        }

        return true;
    }

    private static bool IsCraftable(string equipmentId, IEnumerable<string> craftableEquipmentIds)
    {
        if (string.IsNullOrWhiteSpace(equipmentId))
        {
            return false;
        }

        string[] allowed = craftableEquipmentIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        return allowed.Length == 0
            || allowed.Contains(equipmentId, StringComparer.Ordinal);
    }

    private readonly struct Withdrawal
    {
        public Withdrawal(WarehouseInventory inventory, StockCategory category, int amount)
        {
            Inventory = inventory;
            Category = category;
            Amount = amount;
        }

        public WarehouseInventory Inventory { get; }
        public StockCategory Category { get; }
        public int Amount { get; }
    }

    private static string GetSlot(RuntimeLoadout loadout, ExpeditionEquipmentSlot slot)
    {
        return slot == ExpeditionEquipmentSlot.Weapon ? loadout.WeaponId : loadout.ArmorId;
    }

    private static void SetSlot(RuntimeLoadout loadout, ExpeditionEquipmentSlot slot, string equipmentId)
    {
        if (slot == ExpeditionEquipmentSlot.Weapon)
        {
            loadout.WeaponId = equipmentId ?? string.Empty;
        }
        else
        {
            loadout.ArmorId = equipmentId ?? string.Empty;
        }
    }
}
