using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class DungeonCombatEquipmentSaveData
{
    public List<CombatEquipmentInstance> instances = new List<CombatEquipmentInstance>();
    public List<CharacterCombatLoadoutState> loadouts = new List<CharacterCombatLoadoutState>();
    public List<CombatEquipmentCraftOrderSaveData> craftOrders =
        new List<CombatEquipmentCraftOrderSaveData>();
}

[Serializable]
public sealed class CombatEquipmentCraftOrderSaveData
{
    public string orderId = string.Empty;
    public string definitionId = string.Empty;
    public float requiredWork;
    public float completedWork;
    public bool materialsReady;
    public string materialDestinationId = string.Empty;
    public int destinationX;
    public int destinationY;

    public float RemainingWork => Mathf.Max(0f, requiredWork - completedWork);

    public CombatEquipmentCraftOrderSaveData Clone()
    {
        return new CombatEquipmentCraftOrderSaveData
        {
            orderId = orderId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            requiredWork = Mathf.Max(0.1f, requiredWork),
            completedWork = Mathf.Clamp(completedWork, 0f, Mathf.Max(0.1f, requiredWork)),
            materialsReady = materialsReady,
            materialDestinationId = materialDestinationId ?? string.Empty,
            destinationX = destinationX,
            destinationY = destinationY
        };
    }
}

public interface ICombatEquipmentRuntime
{
    IReadOnlyList<CombatEquipmentDefinitionSO> Definitions { get; }
    IReadOnlyCollection<CombatEquipmentInstance> Instances { get; }
    IReadOnlyList<CombatEquipmentCraftOrderSaveData> CraftQueue { get; }
    bool TryGetDefinition(string definitionId, out CombatEquipmentDefinitionSO definition);
    int GetAvailableCount(string definitionId);
    bool TryQueueCraft(
        string definitionId,
        BuildableObject craftingFacility,
        out string failureReason);
    bool HasPendingCraftWork(IEnumerable<string> craftableDefinitionIds);
    int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId);
    CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState = CombatEquipmentWorldState.Stored);
    bool TryGetInstance(string instanceId, out CombatEquipmentInstance instance);
    bool TryGetInstanceBySourceStack(string sourceStackId, out CombatEquipmentInstance instance);
    bool TryLinkToWorldStack(
        string instanceId,
        string sourceStackId,
        CombatEquipmentWorldState worldState);
    bool TrySetWorldStateBySourceStack(string sourceStackId, CombatEquipmentWorldState worldState);
    bool TryMarkLost(string instanceId);
    bool TryAssignToCharacter(string characterId, string instanceId, out string failureReason);
    bool TryUnassignSlot(
        string characterId,
        CombatEquipmentLoadoutSlot slot,
        out string failureReason);
    bool TrySetActiveWeapon(string characterId, string instanceId, out string failureReason);
    bool TrySetActiveProfile(string characterId, string profileId);
    bool TrySetFireMode(string characterId, CombatFireMode fireMode, out string failureReason);
    bool TrySetHoldFire(string characterId, bool holdFire);
    CharacterCombatLoadoutState GetOrCreateLoadout(string characterId);
    CharacterCombatLoadoutProfile GetActiveProfileSnapshot(string characterId);
    bool TryGetActiveWeapon(string characterId, out CombatWeaponSnapshot weapon);
    IReadOnlyList<CombatArmorSnapshot> GetArmor(string characterId);
    CombatShieldSnapshot GetShield(string characterId, float incomingAngleDegrees = 0f);
    bool TryReload(string instanceId, int availableAmmo, out int consumedAmmo);
    bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out int consumedAmmo);
    bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out int consumedAmmo);
    bool TryConsumeLoadedAmmo(string instanceId);
    bool TryApplyDurabilityDamage(string instanceId, float damage);
    bool TryDetachForMaintenance(
        string instanceId,
        out CombatEquipmentInstance detached);
    IReadOnlyList<CombatEquipmentInstance> ConfiscateAllFromCharacter(
        string characterId);
    void HandleCharacterDeath(string characterId);
    bool TryRestoreDurability(string instanceId, float durabilityRatio);
    float GetCarriedWeight(string characterId);
    DungeonCombatEquipmentSaveData Capture();
    void Restore(DungeonCombatEquipmentSaveData saveData);
}

public interface ICombatLoadoutRuntime
{
    CharacterCombatLoadoutState GetOrCreateLoadout(string characterId);
    bool TrySetActiveProfile(string characterId, string profileId);
    bool TrySetActiveWeapon(string characterId, string instanceId, out string failureReason);
    bool TrySetFireMode(string characterId, CombatFireMode fireMode, out string failureReason);
    bool TrySetHoldFire(string characterId, bool holdFire);
}

public sealed class CombatEquipmentRuntime : ICombatEquipmentRuntime, ICombatLoadoutRuntime
{
    private readonly ICombatEquipmentCatalog catalog;
    private IWorldItemStackRuntime itemStackRuntime;
    private readonly Dictionary<string, CombatEquipmentInstance> instances =
        new Dictionary<string, CombatEquipmentInstance>(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterCombatLoadoutState> loadouts =
        new Dictionary<string, CharacterCombatLoadoutState>(StringComparer.Ordinal);
    private readonly List<CombatEquipmentCraftOrderSaveData> craftOrders =
        new List<CombatEquipmentCraftOrderSaveData>();
    private IReadOnlyList<CombatEquipmentCraftOrderSaveData> craftQueueView;

    public CombatEquipmentRuntime(ICombatEquipmentCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<CombatEquipmentDefinitionSO> Definitions => catalog.All;
    public IReadOnlyCollection<CombatEquipmentInstance> Instances => instances.Values;
    public IReadOnlyList<CombatEquipmentCraftOrderSaveData> CraftQueue =>
        craftQueueView ??= craftOrders.AsReadOnly();

    public void BindItemStackRuntime(IWorldItemStackRuntime runtime)
    {
        if (runtime == null)
        {
            throw new ArgumentNullException(nameof(runtime));
        }

        if (itemStackRuntime != null && !ReferenceEquals(itemStackRuntime, runtime))
        {
            throw new InvalidOperationException(
                "Combat equipment is already bound to another item runtime.");
        }

        itemStackRuntime = runtime;
    }

    public bool TryGetDefinition(
        string definitionId,
        out CombatEquipmentDefinitionSO definition)
    {
        return catalog.TryGet(definitionId, out definition);
    }

    public int GetAvailableCount(string definitionId)
    {
        string normalizedId = definitionId?.Trim() ?? string.Empty;
        return instances.Values.Count(instance =>
            instance != null
            && string.Equals(instance.definitionId, normalizedId, StringComparison.Ordinal)
            && instance.worldState == CombatEquipmentWorldState.Stored);
    }

    public bool TryQueueCraft(
        string definitionId,
        BuildableObject craftingFacility,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedId = definitionId?.Trim() ?? string.Empty;
        bool isAmmunitionRecipe = IsAmmunitionRecipe(normalizedId);
        CombatEquipmentDefinitionSO definition = null;
        if (!isAmmunitionRecipe && !catalog.TryGet(normalizedId, out definition))
        {
            failureReason = "제작할 장비를 찾을 수 없습니다.";
            return false;
        }

        if (craftingFacility == null || itemStackRuntime == null)
        {
            failureReason = "제작 시설이나 물리 아이템 시스템이 준비되지 않았습니다.";
            return false;
        }

        string orderId = $"combat-craft:{Guid.NewGuid():N}";
        string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + orderId;
        IReadOnlyDictionary<StockCategory, int> materials =
            BuildCraftMaterials(definition, normalizedId);
        foreach (KeyValuePair<StockCategory, int> material in materials)
        {
            if (!itemStackRuntime.TryRequestFacilityDelivery(
                    material.Key,
                    material.Value,
                    craftingFacility.centerPos,
                    destinationId,
                    out int requested,
                    out string requestFailure)
                || requested < material.Value)
            {
                itemStackRuntime.ReleaseStacksByDestination(
                    destinationId,
                    craftingFacility.centerPos);
                failureReason = string.IsNullOrWhiteSpace(requestFailure)
                    ? "제작 재료가 부족합니다."
                    : requestFailure;
                return false;
            }
        }

        craftOrders.Add(new CombatEquipmentCraftOrderSaveData
        {
            orderId = orderId,
            definitionId = normalizedId,
            requiredWork = isAmmunitionRecipe
                ? 4f
                : definition.RequiredCraftWork,
            completedWork = 0f,
            materialsReady = materials.Count == 0,
            materialDestinationId = destinationId,
            destinationX = craftingFacility.centerPos.x,
            destinationY = craftingFacility.centerPos.y
        });
        return true;
    }

    public bool HasPendingCraftWork(IEnumerable<string> craftableDefinitionIds)
    {
        return craftOrders.Any(order =>
            order != null
            && order.RemainingWork > 0f
            && IsCraftable(order.definitionId, craftableDefinitionIds)
            && EnsureCraftMaterialsReady(order));
    }

    public int ApplyCraftWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId)
    {
        completedDefinitionId = string.Empty;
        float safeWork = Mathf.Max(0f, workUnits);
        if (safeWork <= 0f)
        {
            return 0;
        }

        for (int index = 0; index < craftOrders.Count; index++)
        {
            CombatEquipmentCraftOrderSaveData order = craftOrders[index];
            if (order == null
                || !IsCraftable(order.definitionId, craftableDefinitionIds)
                || !EnsureCraftMaterialsReady(order))
            {
                continue;
            }

            order.completedWork = Mathf.Min(
                Mathf.Max(0.1f, order.requiredWork),
                order.completedWork + safeWork);
            if (order.RemainingWork > 0.001f)
            {
                return 0;
            }

            completedDefinitionId = order.definitionId;
            craftOrders.RemoveAt(index);
            return 1;
        }

        return 0;
    }

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState = CombatEquipmentWorldState.Stored)
    {
        if (!catalog.TryGet(definitionId, out CombatEquipmentDefinitionSO definition))
        {
            throw new KeyNotFoundException($"Unknown combat equipment definition '{definitionId}'.");
        }

        CombatEquipmentInstance instance = new CombatEquipmentInstance
        {
            instanceId = $"combat-item:{Guid.NewGuid():N}",
            definitionId = definition.EquipmentId,
            quality = quality,
            durabilityRatio = 1f,
            loadedAmmo = 0,
            worldState = worldState
        };
        instances.Add(instance.instanceId, instance);
        return instance.Clone();
    }

    public bool TryGetInstance(string instanceId, out CombatEquipmentInstance instance)
    {
        if (instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance stored))
        {
            instance = stored.Clone();
            return true;
        }

        instance = null;
        return false;
    }

    public bool TryGetInstanceBySourceStack(
        string sourceStackId,
        out CombatEquipmentInstance instance)
    {
        CombatEquipmentInstance stored = instances.Values.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sourceStackId,
                sourceStackId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        if (stored != null)
        {
            instance = stored.Clone();
            return true;
        }

        instance = null;
        return false;
    }

    public bool TryLinkToWorldStack(
        string instanceId,
        string sourceStackId,
        CombatEquipmentWorldState worldState)
    {
        if (!instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || string.IsNullOrWhiteSpace(sourceStackId))
        {
            return false;
        }

        instance.sourceStackId = sourceStackId.Trim();
        instance.worldState = worldState;
        if (worldState is CombatEquipmentWorldState.Stored
            or CombatEquipmentWorldState.Loose
            or CombatEquipmentWorldState.Carried
            or CombatEquipmentWorldState.MaintenanceBuffer)
        {
            RemoveFromAllLoadouts(instance.instanceId);
            instance.ownerCharacterId = string.Empty;
        }

        return true;
    }

    public bool TrySetWorldStateBySourceStack(
        string sourceStackId,
        CombatEquipmentWorldState worldState)
    {
        CombatEquipmentInstance instance = instances.Values.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.sourceStackId,
                sourceStackId?.Trim() ?? string.Empty,
                StringComparison.Ordinal));
        if (instance == null)
        {
            return false;
        }

        instance.worldState = worldState;
        return true;
    }

    public bool TryMarkLost(string instanceId)
    {
        if (!instances.TryGetValue(
            instanceId?.Trim() ?? string.Empty,
            out CombatEquipmentInstance instance))
        {
            return false;
        }

        RemoveFromAllLoadouts(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Lost;
        return true;
    }

    public bool TryAssignToCharacter(string characterId, string instanceId, out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedCharacterId = characterId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(characterId)
            || !instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "장비 또는 캐릭터가 유효하지 않습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(instance.ownerCharacterId)
            && !string.Equals(
                instance.ownerCharacterId,
                normalizedCharacterId,
                StringComparison.Ordinal))
        {
            failureReason = "다른 캐릭터가 이미 장착한 장비입니다.";
            return false;
        }

        if (instance.worldState is CombatEquipmentWorldState.Lost
            or CombatEquipmentWorldState.ExpeditionPacked
            or CombatEquipmentWorldState.MaintenanceBuffer)
        {
            failureReason = "현재 장착할 수 없는 상태의 장비입니다.";
            return false;
        }

        CharacterCombatLoadoutProfile profile = GetActiveProfile(
            GetOrCreateLoadout(normalizedCharacterId));
        if (!ValidateLayerConflict(profile, definition, out failureReason))
        {
            return false;
        }

        if (!ValidateHandOccupancyForAssignment(profile, definition, out failureReason))
        {
            return false;
        }

        RemoveFromAllLoadouts(instance.instanceId);
        instance.ownerCharacterId = normalizedCharacterId;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Equipped;
        switch (definition.Kind)
        {
            case CombatEquipmentKind.Armor:
                profile.armorInstanceIds.Add(instance.instanceId);
                break;
            case CombatEquipmentKind.Shield:
                MarkReplacedShieldCarried(profile, characterId);
                profile.shieldInstanceId = instance.instanceId;
                break;
            default:
                profile.weaponInstanceIds.Add(instance.instanceId);
                if (string.IsNullOrWhiteSpace(profile.activeWeaponInstanceId))
                {
                    profile.activeWeaponInstanceId = instance.instanceId;
                }
                break;
        }

        return true;
    }

    public bool TryUnassignSlot(
        string characterId,
        CombatEquipmentLoadoutSlot slot,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            failureReason = "캐릭터를 찾을 수 없습니다.";
            return false;
        }

        CharacterCombatLoadoutProfile profile = GetActiveProfile(
            GetOrCreateLoadout(characterId));
        List<string> instanceIds = slot == CombatEquipmentLoadoutSlot.Weapon
            ? profile.weaponInstanceIds.ToList()
            : profile.armorInstanceIds
                .Concat(string.IsNullOrWhiteSpace(profile.shieldInstanceId)
                    ? Array.Empty<string>()
                    : new[] { profile.shieldInstanceId })
                .ToList();
        if (instanceIds.Count == 0)
        {
            failureReason = "해제할 장비가 없습니다.";
            return false;
        }

        foreach (string instanceId in instanceIds)
        {
            RemoveFromAllLoadouts(instanceId);
            if (instances.TryGetValue(instanceId, out CombatEquipmentInstance instance))
            {
                instance.ownerCharacterId = string.Empty;
                instance.worldState = CombatEquipmentWorldState.Stored;
            }
        }

        return true;
    }

    public bool TrySetActiveWeapon(string characterId, string instanceId, out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            failureReason = "캐릭터 ID가 없습니다.";
            return false;
        }

        CharacterCombatLoadoutProfile profile = GetActiveProfile(GetOrCreateLoadout(characterId));
        if (!profile.weaponInstanceIds.Contains(instanceId, StringComparer.Ordinal)
            || !instances.TryGetValue(instanceId ?? string.Empty, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon)
        {
            failureReason = "현재 로드아웃에 없는 무기입니다.";
            return false;
        }

        if (!ValidateHandOccupancy(profile, weapon, out failureReason))
        {
            return false;
        }

        profile.activeWeaponInstanceId = instanceId;
        return true;
    }

    public bool TrySetActiveProfile(string characterId, string profileId)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || string.IsNullOrWhiteSpace(profileId))
        {
            return false;
        }

        CharacterCombatLoadoutState state = GetOrCreateLoadout(characterId);
        if (!state.profiles.Any(profile => string.Equals(profile.profileId, profileId, StringComparison.Ordinal)))
        {
            return false;
        }

        CharacterCombatLoadoutProfile targetProfile = state.profiles.First(profile =>
            string.Equals(profile.profileId, profileId, StringComparison.Ordinal));
        if (!ValidateProfileHandOccupancy(targetProfile))
        {
            return false;
        }

        state.activeProfileId = profileId;
        return true;
    }

    public bool TrySetFireMode(
        string characterId,
        CombatFireMode fireMode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryGetActiveWeapon(characterId, out CombatWeaponSnapshot weapon)
            || weapon == null
            || !weapon.IsRanged
            || string.IsNullOrWhiteSpace(weapon.InstanceId))
        {
            failureReason = "활성 원거리 무기가 없습니다.";
            return false;
        }

        bool supported = fireMode switch
        {
            CombatFireMode.Aimed => weapon.SupportsAimed,
            CombatFireMode.Rapid => weapon.SupportsRapid,
            CombatFireMode.Suppressive => weapon.SupportsSuppressive,
            _ => false
        };
        if (!supported)
        {
            failureReason = "이 무기는 선택한 사격 모드를 지원하지 않습니다.";
            return false;
        }

        GetActiveProfile(GetOrCreateLoadout(characterId)).fireMode = fireMode;
        return true;
    }

    public bool TrySetHoldFire(string characterId, bool holdFire)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        GetActiveProfile(GetOrCreateLoadout(characterId)).holdFire = holdFire;
        return true;
    }

    public CharacterCombatLoadoutState GetOrCreateLoadout(string characterId)
    {
        string normalizedId = characterId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            throw new ArgumentException("Character ID is required.", nameof(characterId));
        }

        if (loadouts.TryGetValue(normalizedId, out CharacterCombatLoadoutState existing))
        {
            return existing;
        }

        CharacterCombatLoadoutState created = new CharacterCombatLoadoutState
        {
            characterId = normalizedId,
            activeProfileId = CombatLoadoutPresetIds.Peace,
            profiles = new List<CharacterCombatLoadoutProfile>
            {
                new CharacterCombatLoadoutProfile
                {
                    profileId = CombatLoadoutPresetIds.Peace,
                    displayName = "평시"
                },
                new CharacterCombatLoadoutProfile
                {
                    profileId = CombatLoadoutPresetIds.Combat,
                    displayName = "전투",
                    desiredWeaponDefinitionIds = new List<string> { "weapon:longsword" },
                    desiredArmorDefinitionIds = new List<string> { "armor:gambeson" },
                    desiredShieldDefinitionId = "shield:wood",
                    desiredAmmo = 20
                },
                new CharacterCombatLoadoutProfile
                {
                    profileId = CombatLoadoutPresetIds.Melee,
                    displayName = "근접병",
                    desiredWeaponDefinitionIds = new List<string> { "weapon:longsword" },
                    desiredArmorDefinitionIds = new List<string> { "armor:gambeson" },
                    desiredShieldDefinitionId = "shield:wood",
                    desiredAmmo = 0
                },
                new CharacterCombatLoadoutProfile
                {
                    profileId = CombatLoadoutPresetIds.Archer,
                    displayName = "궁수",
                    desiredWeaponDefinitionIds = new List<string>
                    {
                        "weapon:shortbow",
                        "weapon:dagger"
                    },
                    desiredArmorDefinitionIds = new List<string> { "armor:leather" },
                    desiredAmmo = 30
                },
                new CharacterCombatLoadoutProfile
                {
                    profileId = CombatLoadoutPresetIds.Crossbow,
                    displayName = "석궁수",
                    desiredWeaponDefinitionIds = new List<string>
                    {
                        "weapon:crossbow",
                        "weapon:dagger"
                    },
                    desiredArmorDefinitionIds = new List<string> { "armor:gambeson" },
                    desiredAmmo = 18
                },
                new CharacterCombatLoadoutProfile
                {
                    profileId = CombatLoadoutPresetIds.Skirmisher,
                    displayName = "척후병",
                    desiredWeaponDefinitionIds = new List<string>
                    {
                        "weapon:javelin",
                        "weapon:throwing-axe"
                    },
                    desiredArmorDefinitionIds = new List<string> { "armor:leather" },
                    desiredAmmo = 6
                }
            }
        };
        loadouts.Add(normalizedId, created);
        return created;
    }

    public CharacterCombatLoadoutProfile GetActiveProfileSnapshot(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return null;
        }

        return GetActiveProfile(GetOrCreateLoadout(characterId)).Clone();
    }

    public bool TryGetActiveWeapon(string characterId, out CombatWeaponSnapshot weapon)
    {
        weapon = CombatWeaponSnapshot.CreateUnarmed();
        if (string.IsNullOrWhiteSpace(characterId)
            || !loadouts.TryGetValue(characterId, out CharacterCombatLoadoutState state))
        {
            return true;
        }

        CharacterCombatLoadoutProfile profile = GetActiveProfile(state);
        if (string.IsNullOrWhiteSpace(profile.activeWeaponInstanceId)
            || !instances.TryGetValue(profile.activeWeaponInstanceId, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weaponDefinition)
        {
            return true;
        }

        weapon = weaponDefinition.CreateSnapshot(instance);
        return true;
    }

    public IReadOnlyList<CombatArmorSnapshot> GetArmor(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || !loadouts.TryGetValue(characterId, out CharacterCombatLoadoutState state))
        {
            return Array.Empty<CombatArmorSnapshot>();
        }

        List<CombatArmorSnapshot> result = new List<CombatArmorSnapshot>();
        CharacterCombatLoadoutProfile profile = GetActiveProfile(state);
        foreach (string instanceId in profile.armorInstanceIds)
        {
            if (!instances.TryGetValue(instanceId, out CombatEquipmentInstance instance)
                || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
                || definition is not CombatArmorSO armorDefinition)
            {
                continue;
            }

            foreach (CombatArmorPartValue value in armorDefinition.BodyPartDefense)
            {
                if (value == null)
                {
                    continue;
                }

                result.Add(new CombatArmorSnapshot(
                    instance.instanceId,
                    value.bodyPart,
                    armorDefinition.Layer,
                    instance.quality,
                    instance.durabilityRatio,
                    value.slashDefense,
                    value.pierceDefense,
                    value.bluntDefense));
            }
        }

        return result;
    }

    public CombatShieldSnapshot GetShield(string characterId, float incomingAngleDegrees = 0f)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || !loadouts.TryGetValue(characterId, out CharacterCombatLoadoutState state))
        {
            return default;
        }

        CharacterCombatLoadoutProfile profile = GetActiveProfile(state);
        if (string.IsNullOrWhiteSpace(profile.shieldInstanceId)
            || !instances.TryGetValue(profile.shieldInstanceId, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition is not CombatShieldSO shield)
        {
            return default;
        }

        return new CombatShieldSnapshot(
            instance.instanceId,
            instance.quality,
            instance.durabilityRatio,
            shield.FrontalBlockChance,
            incomingAngleDegrees,
            shield.SlashDefense,
            shield.PierceDefense,
            shield.BluntDefense);
    }

    public bool TryReload(string instanceId, int availableAmmo, out int consumedAmmo)
    {
        consumedAmmo = 0;
        if (!instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon
            || weapon.MagazineCapacity <= 0)
        {
            return false;
        }

        int needed = Mathf.Max(0, weapon.MagazineCapacity - instance.loadedAmmo);
        consumedAmmo = Mathf.Min(needed, Mathf.Max(0, availableAmmo));
        instance.loadedAmmo += consumedAmmo;
        return consumedAmmo > 0;
    }

    public bool TryReloadFromInventory(
        string instanceId,
        CharacterCarryInventory inventory,
        out int consumedAmmo)
    {
        consumedAmmo = 0;
        if (inventory == null
            || !instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon
            || weapon.MagazineCapacity <= 0
            || string.IsNullOrWhiteSpace(weapon.AmmunitionItemId))
        {
            return false;
        }

        int needed = Mathf.Max(0, weapon.MagazineCapacity - instance.loadedAmmo);
        int available = inventory.CountItem(weapon.AmmunitionItemId);
        consumedAmmo = Mathf.Min(needed, available);
        if (consumedAmmo <= 0
            || !inventory.TryConsumeItem(weapon.AmmunitionItemId, consumedAmmo))
        {
            consumedAmmo = 0;
            return false;
        }

        instance.loadedAmmo += consumedAmmo;
        return true;
    }

    public bool TryReloadFromCharacterInventory(
        string characterId,
        string instanceId,
        out int consumedAmmo)
    {
        return TryReloadFromInventory(
            instanceId,
            CharacterCarryInventory.FindByCharacterId(characterId),
            out consumedAmmo);
    }

    public bool TryConsumeLoadedAmmo(string instanceId)
    {
        if (!instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || instance.loadedAmmo <= 0)
        {
            return false;
        }

        instance.loadedAmmo--;
        return true;
    }

    public bool TryApplyDurabilityDamage(string instanceId, float damage)
    {
        if (damage <= 0f
            || !instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is CombatEquipmentKind.MeleeWeapon
                or CombatEquipmentKind.RangedWeapon
                or CombatEquipmentKind.RecoverableThrowingWeapon)
        {
            return false;
        }

        instance.durabilityRatio = Mathf.Clamp01(
            instance.durabilityRatio - damage / Mathf.Max(1f, definition.MaxDurability));
        return true;
    }

    public bool TryDetachForMaintenance(
        string instanceId,
        out CombatEquipmentInstance detached)
    {
        detached = null;
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is not CombatEquipmentKind.Armor
                and not CombatEquipmentKind.Shield)
        {
            return false;
        }

        RemoveFromAllLoadouts(instance.instanceId);
        instance.ownerCharacterId = string.Empty;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Loose;
        detached = instance.Clone();
        return true;
    }

    public IReadOnlyList<CombatEquipmentInstance> ConfiscateAllFromCharacter(
        string characterId)
    {
        string normalized = characterId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return Array.Empty<CombatEquipmentInstance>();
        }

        List<CombatEquipmentInstance> confiscated = instances.Values
            .Where(instance => instance != null
                && string.Equals(
                    instance.ownerCharacterId,
                    normalized,
                    StringComparison.Ordinal))
            .ToList();
        foreach (CombatEquipmentInstance instance in confiscated)
        {
            RemoveFromAllLoadouts(instance.instanceId);
            instance.ownerCharacterId = string.Empty;
            instance.sourceStackId = string.Empty;
            instance.worldState = CombatEquipmentWorldState.Loose;
        }

        return confiscated.Select(instance => instance.Clone()).ToArray();
    }

    public void HandleCharacterDeath(string characterId)
    {
        string normalized = characterId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return;
        }

        string[] lostInstanceIds = instances.Values
            .Where(instance => instance != null
                && string.Equals(
                    instance.ownerCharacterId,
                    normalized,
                    StringComparison.Ordinal))
            .Select(instance => instance.instanceId)
            .ToArray();
        foreach (string instanceId in lostInstanceIds)
        {
            TryMarkLost(instanceId);
        }

        loadouts.Remove(normalized);
    }

    public bool TryRestoreDurability(string instanceId, float durabilityRatio)
    {
        if (!instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is not CombatEquipmentKind.Armor
                and not CombatEquipmentKind.Shield)
        {
            return false;
        }

        instance.durabilityRatio = Mathf.Clamp01(
            Mathf.Max(instance.durabilityRatio, durabilityRatio));
        return true;
    }

    public float GetCarriedWeight(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return 0f;
        }

        float total = 0f;
        foreach (CombatEquipmentInstance instance in instances.Values)
        {
            if (string.Equals(instance.ownerCharacterId, characterId, StringComparison.Ordinal)
                && catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition))
            {
                total += definition.Weight;
            }
        }

        return total;
    }

    public DungeonCombatEquipmentSaveData Capture()
    {
        return new DungeonCombatEquipmentSaveData
        {
            instances = instances.Values.Select(item => item.Clone()).ToList(),
            loadouts = loadouts.Values.Select(CloneLoadout).ToList(),
            craftOrders = craftOrders
                .Where(order => order != null && order.RemainingWork > 0f)
                .Select(order => order.Clone())
                .ToList()
        };
    }

    public void Restore(DungeonCombatEquipmentSaveData saveData)
    {
        instances.Clear();
        loadouts.Clear();
        craftOrders.Clear();
        foreach (CombatEquipmentInstance instance in saveData?.instances ?? new List<CombatEquipmentInstance>())
        {
            if (instance == null
                || string.IsNullOrWhiteSpace(instance.instanceId)
                || string.IsNullOrWhiteSpace(instance.definitionId)
                || !catalog.TryGet(instance.definitionId, out _)
                || instances.ContainsKey(instance.instanceId))
            {
                continue;
            }

            instance.durabilityRatio = Mathf.Clamp01(instance.durabilityRatio);
            instances.Add(instance.instanceId, instance.Clone());
        }

        foreach (CharacterCombatLoadoutState loadout in saveData?.loadouts ?? new List<CharacterCombatLoadoutState>())
        {
            if (loadout == null
                || string.IsNullOrWhiteSpace(loadout.characterId)
                || loadouts.ContainsKey(loadout.characterId))
            {
                continue;
            }

            CharacterCombatLoadoutState restored = CloneLoadout(loadout);
            SanitizeLoadout(restored);
            loadouts.Add(loadout.characterId, restored);
        }

        HashSet<string> orderIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CombatEquipmentCraftOrderSaveData source in saveData?.craftOrders
            ?? new List<CombatEquipmentCraftOrderSaveData>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.orderId)
                || !orderIds.Add(source.orderId)
                || source.RemainingWork <= 0f
                || (!IsAmmunitionRecipe(source.definitionId)
                    && !catalog.TryGet(source.definitionId, out _)))
            {
                continue;
            }

            craftOrders.Add(source.Clone());
        }
    }

    private bool EnsureCraftMaterialsReady(CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null)
        {
            return false;
        }

        if (order.materialsReady)
        {
            return true;
        }

        CombatEquipmentDefinitionSO definition = null;
        if (!IsAmmunitionRecipe(order.definitionId)
            && !catalog.TryGet(order.definitionId, out definition))
        {
            return false;
        }

        IReadOnlyDictionary<StockCategory, int> materials =
            BuildCraftMaterials(definition, order.definitionId);
        if (materials.Count == 0)
        {
            order.materialsReady = true;
            return true;
        }

        if (itemStackRuntime == null
            || string.IsNullOrWhiteSpace(order.materialDestinationId)
            || !itemStackRuntime.TryConsumeFacilityBuffer(
                order.materialDestinationId,
                materials,
                out _))
        {
            return false;
        }

        order.materialsReady = true;
        return true;
    }

    private static IReadOnlyDictionary<StockCategory, int> BuildCraftMaterials(
        CombatEquipmentDefinitionSO definition,
        string definitionId)
    {
        Dictionary<StockCategory, int> materials =
            new Dictionary<StockCategory, int>();
        if (IsAmmunitionRecipe(definitionId))
        {
            materials[StockCategory.General] = 1;
            return materials;
        }

        foreach (CombatEquipmentCraftMaterial material in definition?.CraftMaterials
            ?? Array.Empty<CombatEquipmentCraftMaterial>())
        {
            if (material == null || material.amount <= 0)
            {
                continue;
            }

            materials.TryGetValue(material.category, out int current);
            materials[material.category] = current + material.amount;
        }

        if (materials.Count == 0)
        {
            materials[StockCategory.General] = 1;
        }

        return materials;
    }

    private static bool IsCraftable(
        string definitionId,
        IEnumerable<string> craftableDefinitionIds)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return false;
        }

        string[] allowed = craftableDefinitionIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        return allowed.Length == 0
            || allowed.Contains(definitionId, StringComparer.Ordinal);
    }

    private static bool IsAmmunitionRecipe(string definitionId)
    {
        return string.Equals(
                definitionId,
                CombatItemDefinitions.ArrowBundleRecipeId,
                StringComparison.Ordinal)
            || string.Equals(
                definitionId,
                CombatItemDefinitions.BoltBundleRecipeId,
                StringComparison.Ordinal);
    }

    private bool ValidateHandOccupancyForAssignment(
        CharacterCombatLoadoutProfile profile,
        CombatEquipmentDefinitionSO candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (candidate is CombatShieldSO)
        {
            CombatWeaponSO activeWeapon = ResolveActiveWeaponDefinition(profile);
            return ValidateHandOccupancy(profile, activeWeapon, candidate, out failureReason);
        }

        if (candidate is CombatWeaponSO weapon
            && string.IsNullOrWhiteSpace(profile.activeWeaponInstanceId))
        {
            return ValidateHandOccupancy(profile, weapon, out failureReason);
        }

        return true;
    }

    private bool ValidateHandOccupancy(
        CharacterCombatLoadoutProfile profile,
        CombatWeaponSO activeWeapon,
        out string failureReason)
    {
        CombatEquipmentDefinitionSO shield = ResolveShieldDefinition(profile);
        return ValidateHandOccupancy(profile, activeWeapon, shield, out failureReason);
    }

    private static bool ValidateHandOccupancy(
        CharacterCombatLoadoutProfile profile,
        CombatEquipmentDefinitionSO activeWeapon,
        CombatEquipmentDefinitionSO shield,
        out string failureReason)
    {
        int occupiedHands = (activeWeapon?.OccupiedHands ?? 0) + (shield?.OccupiedHands ?? 0);
        if (occupiedHands <= 2)
        {
            failureReason = string.Empty;
            return true;
        }

        string weaponName = activeWeapon?.DisplayName ?? "활성 무기";
        string shieldName = shield?.DisplayName ?? "방패";
        failureReason = $"{weaponName}과 {shieldName}은 함께 사용할 손이 부족합니다.";
        return false;
    }

    private bool ValidateProfileHandOccupancy(CharacterCombatLoadoutProfile profile)
    {
        return ValidateHandOccupancy(
            profile,
            ResolveActiveWeaponDefinition(profile),
            ResolveShieldDefinition(profile),
            out _);
    }

    private CombatWeaponSO ResolveActiveWeaponDefinition(CharacterCombatLoadoutProfile profile)
    {
        if (profile == null
            || string.IsNullOrWhiteSpace(profile.activeWeaponInstanceId)
            || !instances.TryGetValue(profile.activeWeaponInstanceId, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition))
        {
            return null;
        }

        return definition as CombatWeaponSO;
    }

    private CombatEquipmentDefinitionSO ResolveShieldDefinition(CharacterCombatLoadoutProfile profile)
    {
        if (profile == null
            || string.IsNullOrWhiteSpace(profile.shieldInstanceId)
            || !instances.TryGetValue(profile.shieldInstanceId, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition))
        {
            return null;
        }

        return definition is CombatShieldSO ? definition : null;
    }

    private void MarkReplacedShieldCarried(
        CharacterCombatLoadoutProfile profile,
        string characterId)
    {
        if (profile == null
            || string.IsNullOrWhiteSpace(profile.shieldInstanceId)
            || !instances.TryGetValue(profile.shieldInstanceId, out CombatEquipmentInstance previous))
        {
            return;
        }

        previous.ownerCharacterId = characterId?.Trim() ?? string.Empty;
        previous.worldState = CombatEquipmentWorldState.Carried;
    }

    private void SanitizeLoadout(CharacterCombatLoadoutState state)
    {
        foreach (CharacterCombatLoadoutProfile profile in state?.profiles
            ?? new List<CharacterCombatLoadoutProfile>())
        {
            profile.weaponInstanceIds ??= new List<string>();
            profile.armorInstanceIds ??= new List<string>();
            profile.desiredWeaponDefinitionIds ??= new List<string>();
            profile.desiredArmorDefinitionIds ??= new List<string>();

            if (ValidateProfileHandOccupancy(profile))
            {
                continue;
            }

            profile.shieldInstanceId = string.Empty;
        }
    }

    private bool ValidateLayerConflict(
        CharacterCombatLoadoutProfile profile,
        CombatEquipmentDefinitionSO candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (candidate is not CombatArmorSO candidateArmor)
        {
            return true;
        }

        foreach (string instanceId in profile.armorInstanceIds)
        {
            if (!instances.TryGetValue(instanceId, out CombatEquipmentInstance instance)
                || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
                || definition is not CombatArmorSO equippedArmor
                || equippedArmor.Layer != candidateArmor.Layer)
            {
                continue;
            }

            bool overlaps = equippedArmor.BodyPartDefense.Any(left => left != null
                && candidateArmor.BodyPartDefense.Any(right => right != null && right.bodyPart == left.bodyPart));
            if (overlaps)
            {
                failureReason = "같은 부위와 레이어를 차지하는 방어구가 이미 장착되어 있습니다.";
                return false;
            }
        }

        return true;
    }

    private void RemoveFromAllLoadouts(string instanceId)
    {
        foreach (CharacterCombatLoadoutState state in loadouts.Values)
        {
            foreach (CharacterCombatLoadoutProfile profile in state.profiles)
            {
                profile.weaponInstanceIds.RemoveAll(id => string.Equals(id, instanceId, StringComparison.Ordinal));
                profile.armorInstanceIds.RemoveAll(id => string.Equals(id, instanceId, StringComparison.Ordinal));
                if (string.Equals(profile.shieldInstanceId, instanceId, StringComparison.Ordinal))
                {
                    profile.shieldInstanceId = string.Empty;
                }

                if (string.Equals(profile.activeWeaponInstanceId, instanceId, StringComparison.Ordinal))
                {
                    profile.activeWeaponInstanceId = profile.weaponInstanceIds.FirstOrDefault() ?? string.Empty;
                }
            }
        }
    }

    private static CharacterCombatLoadoutProfile GetActiveProfile(CharacterCombatLoadoutState state)
    {
        CharacterCombatLoadoutProfile profile = state.profiles.FirstOrDefault(item =>
            string.Equals(item.profileId, state.activeProfileId, StringComparison.Ordinal));
        if (profile != null)
        {
            return profile;
        }

        profile = state.profiles.FirstOrDefault();
        if (profile == null)
        {
            profile = new CharacterCombatLoadoutProfile
            {
                profileId = CombatLoadoutPresetIds.Peace,
                displayName = "평시"
            };
            state.profiles.Add(profile);
        }

        state.activeProfileId = profile.profileId;
        return profile;
    }

    private static CharacterCombatLoadoutState CloneLoadout(CharacterCombatLoadoutState source)
    {
        return new CharacterCombatLoadoutState
        {
            characterId = source.characterId ?? string.Empty,
            activeProfileId = source.activeProfileId ?? CombatLoadoutPresetIds.Peace,
            profiles = source.profiles?.Select(profile => profile?.Clone())
                .Where(profile => profile != null)
                .ToList() ?? new List<CharacterCombatLoadoutProfile>()
        };
    }
}
