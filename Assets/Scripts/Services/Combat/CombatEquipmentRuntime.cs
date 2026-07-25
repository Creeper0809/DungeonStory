using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class DungeonCombatEquipmentSaveData
{
    public List<CombatEquipmentInstance> instances = new List<CombatEquipmentInstance>();
    public List<CharacterCombatLoadoutState> loadouts = new List<CharacterCombatLoadoutState>();
}

public interface ICombatEquipmentRuntime
{
    IReadOnlyCollection<CombatEquipmentInstance> Instances { get; }
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
    private readonly Dictionary<string, CombatEquipmentInstance> instances =
        new Dictionary<string, CombatEquipmentInstance>(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterCombatLoadoutState> loadouts =
        new Dictionary<string, CharacterCombatLoadoutState>(StringComparer.Ordinal);

    public CombatEquipmentRuntime(ICombatEquipmentCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyCollection<CombatEquipmentInstance> Instances => instances.Values;

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
        if (string.IsNullOrWhiteSpace(characterId)
            || !instances.TryGetValue(instanceId?.Trim() ?? string.Empty, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "장비 또는 캐릭터가 유효하지 않습니다.";
            return false;
        }

        CharacterCombatLoadoutProfile profile = GetActiveProfile(GetOrCreateLoadout(characterId));
        if (!ValidateLayerConflict(profile, definition, out failureReason))
        {
            return false;
        }

        if (!ValidateHandOccupancyForAssignment(profile, definition, out failureReason))
        {
            return false;
        }

        RemoveFromAllLoadouts(instance.instanceId);
        instance.ownerCharacterId = characterId.Trim();
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
            loadouts = loadouts.Values.Select(CloneLoadout).ToList()
        };
    }

    public void Restore(DungeonCombatEquipmentSaveData saveData)
    {
        instances.Clear();
        loadouts.Clear();
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
