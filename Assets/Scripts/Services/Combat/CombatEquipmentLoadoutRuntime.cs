using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Owns character equipment references and loadout policy. Equipment payloads
/// remain authoritative in IItemInstanceRepository.
/// </summary>
public sealed class CombatEquipmentLoadoutRuntime
{
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IItemInstanceRepository itemInstances;
    private readonly CombatEquipmentLoadoutStore store;
    private readonly CombatEquipmentStatProjector statProjector;
    private readonly CombatEquipmentCraftingRuntime crafting;

    private IDictionary<string, CombatEquipmentInstance> Instances =>
        itemInstances.EquipmentInstances;
    private IDictionary<string, EquipmentModuleInstance> Modules =>
        itemInstances.EquipmentModules;
    private IDictionary<string, CharacterCombatLoadoutState> States => store.States;

    public CombatEquipmentLoadoutRuntime(
        ICombatEquipmentCatalog catalog,
        IItemInstanceRepository itemInstances,
        CombatEquipmentLoadoutStore store,
        CombatEquipmentStatProjector statProjector,
        CombatEquipmentCraftingRuntime crafting)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.statProjector = statProjector
            ?? throw new ArgumentNullException(nameof(statProjector));
        this.crafting = crafting
            ?? throw new ArgumentNullException(nameof(crafting));
    }

    public bool TryAssign(
        string characterId,
        string instanceId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedCharacterId = characterId?.Trim() ?? string.Empty;
        if (normalizedCharacterId.Length == 0
            || !Instances.TryGetValue(
                instanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "equipment.assign.invalid_target";
            return false;
        }
        if (!string.IsNullOrWhiteSpace(instance.ownerCharacterId)
            && !string.Equals(
                instance.ownerCharacterId,
                normalizedCharacterId,
                StringComparison.Ordinal))
        {
            failureReason = "equipment.assign.owned_by_other_character";
            return false;
        }
        if (instance.worldState is CombatEquipmentWorldState.Lost
            or CombatEquipmentWorldState.ExpeditionPacked
            or CombatEquipmentWorldState.MaintenanceBuffer)
        {
            failureReason = "equipment.assign.invalid_world_state";
            return false;
        }

        CharacterCombatLoadoutProfile profile = GetActiveProfile(
            GetOrCreate(normalizedCharacterId));
        if (!ValidateLayerConflict(profile, definition, out failureReason)
            || !ValidateHandOccupancyForAssignment(
                profile,
                definition,
                out failureReason))
        {
            return false;
        }

        store.RemoveEquipment(instance.instanceId);
        instance.ownerCharacterId = normalizedCharacterId;
        instance.sourceStackId = string.Empty;
        instance.worldState = CombatEquipmentWorldState.Equipped;
        switch (definition.Kind)
        {
            case CombatEquipmentKind.Armor:
                profile.armorInstanceIds.Add(instance.instanceId);
                break;
            case CombatEquipmentKind.Shield:
                MarkReplacedShieldCarried(profile, normalizedCharacterId);
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

    public bool TryUnassign(
        string characterId,
        CombatEquipmentLoadoutSlot slot,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            failureReason = "equipment.loadout.character_required";
            return false;
        }
        CharacterCombatLoadoutProfile profile = GetActiveProfile(GetOrCreate(characterId));
        List<string> instanceIds = slot == CombatEquipmentLoadoutSlot.Weapon
            ? profile.weaponInstanceIds.ToList()
            : profile.armorInstanceIds
                .Concat(string.IsNullOrWhiteSpace(profile.shieldInstanceId)
                    ? Array.Empty<string>()
                    : new[] { profile.shieldInstanceId })
                .ToList();
        if (instanceIds.Count == 0)
        {
            failureReason = "equipment.loadout.slot_empty";
            return false;
        }
        foreach (string id in instanceIds)
        {
            store.RemoveEquipment(id);
            if (Instances.TryGetValue(id, out CombatEquipmentInstance instance))
            {
                instance.ownerCharacterId = string.Empty;
                instance.worldState = CombatEquipmentWorldState.Stored;
            }
        }
        return true;
    }

    public bool TrySetActiveWeapon(
        string characterId,
        string instanceId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            failureReason = "equipment.loadout.character_required";
            return false;
        }
        CharacterCombatLoadoutProfile profile = GetActiveProfile(GetOrCreate(characterId));
        if (!profile.weaponInstanceIds.Contains(instanceId, StringComparer.Ordinal)
            || !Instances.TryGetValue(
                instanceId ?? string.Empty,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weapon)
        {
            failureReason = "equipment.loadout.weapon_not_assigned";
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
        CharacterCombatLoadoutState state = GetOrCreate(characterId);
        CharacterCombatLoadoutProfile target = state.profiles.FirstOrDefault(profile =>
            string.Equals(profile.profileId, profileId, StringComparison.Ordinal));
        if (target == null || !ValidateProfileHandOccupancy(target))
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
            failureReason = "equipment.fire_mode.no_active_ranged_weapon";
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
            failureReason = "equipment.fire_mode.unsupported";
            return false;
        }
        GetActiveProfile(GetOrCreate(characterId)).fireMode = fireMode;
        return true;
    }

    public bool TrySetHoldFire(string characterId, bool holdFire)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }
        GetActiveProfile(GetOrCreate(characterId)).holdFire = holdFire;
        return true;
    }

    public CharacterCombatLoadoutState GetOrCreate(string characterId)
    {
        string normalizedId = characterId?.Trim() ?? string.Empty;
        if (normalizedId.Length == 0)
        {
            throw new ArgumentException("Character ID is required.", nameof(characterId));
        }
        if (States.TryGetValue(normalizedId, out CharacterCombatLoadoutState existing))
        {
            return existing;
        }

        CharacterCombatLoadoutState created = new CharacterCombatLoadoutState
        {
            characterId = normalizedId,
            activeProfileId = CombatLoadoutPresetIds.Peace,
            profiles = new List<CharacterCombatLoadoutProfile>
            {
                Profile(CombatLoadoutPresetIds.Peace, "평시"),
                Profile(
                    CombatLoadoutPresetIds.Combat,
                    "전투",
                    new[] { "weapon:longsword" },
                    new[] { "armor:gambeson" },
                    "shield:wood",
                    20),
                Profile(
                    CombatLoadoutPresetIds.Melee,
                    "근접병",
                    new[] { "weapon:longsword" },
                    new[] { "armor:gambeson" },
                    "shield:wood",
                    0),
                Profile(
                    CombatLoadoutPresetIds.Archer,
                    "궁수",
                    new[] { "weapon:shortbow", "weapon:dagger" },
                    new[] { "armor:leather" },
                    string.Empty,
                    30),
                Profile(
                    CombatLoadoutPresetIds.Crossbow,
                    "석궁수",
                    new[] { "weapon:crossbow", "weapon:dagger" },
                    new[] { "armor:gambeson" },
                    string.Empty,
                    18),
                Profile(
                    CombatLoadoutPresetIds.Skirmisher,
                    "척후병",
                    new[] { "weapon:javelin", "weapon:throwing-axe" },
                    new[] { "armor:leather" },
                    string.Empty,
                    6)
            }
        };
        States.Add(normalizedId, created);
        return created;
    }

    public CharacterCombatLoadoutProfile GetActiveProfileSnapshot(string characterId)
    {
        return string.IsNullOrWhiteSpace(characterId)
            ? null
            : GetActiveProfile(GetOrCreate(characterId)).Clone();
    }

    public bool TryGetActiveWeapon(string characterId, out CombatWeaponSnapshot weapon)
    {
        weapon = CombatWeaponSnapshot.CreateUnarmed();
        if (string.IsNullOrWhiteSpace(characterId)
            || !States.TryGetValue(characterId, out CharacterCombatLoadoutState state))
        {
            return true;
        }
        CharacterCombatLoadoutProfile profile = GetActiveProfile(state);
        if (string.IsNullOrWhiteSpace(profile.activeWeaponInstanceId)
            || !Instances.TryGetValue(
                profile.activeWeaponInstanceId,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || definition is not CombatWeaponSO weaponDefinition)
        {
            return true;
        }
        weapon = weaponDefinition.CreateSnapshot(
            instance,
            material: crafting.ResolveInstanceMaterial(instance, weaponDefinition),
            evolutionDamageMultiplier: statProjector.GetEvolutionMultiplier(instance, "combat.damage")
                * statProjector.GetInstalledModuleMultiplier(instance, true),
            evolutionPenetrationMultiplier: statProjector.GetEvolutionMultiplier(instance, "combat.penetration"),
            evolutionAccuracyMultiplier: statProjector.GetEvolutionMultiplier(instance, "combat.accuracy")
                * statProjector.GetInstalledModuleMultiplier(instance, false),
            evolutionReloadMultiplier: statProjector.GetEvolutionMultiplier(instance, "combat.reload"));
        return true;
    }

    public IReadOnlyList<CombatArmorSnapshot> GetArmor(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || !States.TryGetValue(characterId, out CharacterCombatLoadoutState state))
        {
            return Array.Empty<CombatArmorSnapshot>();
        }
        List<CombatArmorSnapshot> result = new();
        foreach (string instanceId in GetActiveProfile(state).armorInstanceIds)
        {
            if (!Instances.TryGetValue(instanceId, out CombatEquipmentInstance instance)
                || !catalog.TryGet(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition)
                || definition is not CombatArmorSO armor)
            {
                continue;
            }
            CraftMaterialDefinitionSO material =
                crafting.ResolveInstanceMaterial(instance, armor);
            float powerMultiplier = CombatEquipmentRoleRules
                .GetPowerPerformanceMultiplier(
                    armor.EquipmentId,
                    instance.powerCharge);
            foreach (CombatArmorPartValue value in armor.BodyPartDefense)
            {
                if (value == null)
                {
                    continue;
                }
                result.Add(new CombatArmorSnapshot(
                    instance.instanceId,
                    value.bodyPart,
                    armor.Layer,
                    instance.quality,
                    instance.durabilityRatio,
                    value.slashDefense,
                    value.pierceDefense,
                    value.bluntDefense,
                    (material?.PenetrationDefenseMultiplier ?? 1f)
                        * armor.BaseStatMultiplier
                        * statProjector.GetEvolutionMultiplier(instance, "combat.defense")
                        * statProjector.GetInstalledModuleMultiplier(instance, true)
                        * powerMultiplier,
                    armor.EquipmentId,
                    CombatEquipmentRoleRules.ForPowerState(
                        armor.EquipmentId,
                        instance.powerCharge > 0f)));
            }
        }
        return result;
    }

    public CombatShieldSnapshot GetShield(
        string characterId,
        float incomingAngleDegrees)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || !States.TryGetValue(characterId, out CharacterCombatLoadoutState state))
        {
            return default;
        }
        CharacterCombatLoadoutProfile profile = GetActiveProfile(state);
        if (string.IsNullOrWhiteSpace(profile.shieldInstanceId)
            || !Instances.TryGetValue(
                profile.shieldInstanceId,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
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
            shield.BluntDefense,
            crafting.ResolveInstanceMaterial(instance, shield)
                ?.PenetrationDefenseMultiplier
                * shield.BaseStatMultiplier
                * statProjector.GetEvolutionMultiplier(instance, "combat.defense")
                * statProjector.GetInstalledModuleMultiplier(instance, true)
                * CombatEquipmentRoleRules.GetPowerPerformanceMultiplier(
                    shield.EquipmentId,
                    instance.powerCharge)
                ?? statProjector.GetEvolutionMultiplier(instance, "combat.defense"),
            shield.EquipmentId,
            CombatEquipmentRoleRules.ForPowerState(
                shield.EquipmentId,
                instance.powerCharge > 0f));
    }

    public IReadOnlyList<CombatEquipmentInstance> ConfiscateAll(string characterId)
    {
        string normalized = characterId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return Array.Empty<CombatEquipmentInstance>();
        }
        List<CombatEquipmentInstance> confiscated = Instances.Values
            .Where(instance => instance != null
                && string.Equals(
                    instance.ownerCharacterId,
                    normalized,
                    StringComparison.Ordinal))
            .ToList();
        foreach (CombatEquipmentInstance instance in confiscated)
        {
            store.RemoveEquipment(instance.instanceId);
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
        foreach (CombatEquipmentInstance instance in Instances.Values
                     .Where(instance => instance != null
                         && string.Equals(
                             instance.ownerCharacterId,
                             normalized,
                             StringComparison.Ordinal)))
        {
            store.RemoveEquipment(instance.instanceId);
            instance.ownerCharacterId = string.Empty;
            instance.sourceStackId = string.Empty;
            instance.worldState = CombatEquipmentWorldState.Lost;
            foreach (EquipmentModuleSlotState slot in instance.moduleSlots
                         ?? new List<EquipmentModuleSlotState>())
            {
                if (slot != null
                    && Modules.TryGetValue(
                        slot.moduleInstanceId,
                        out EquipmentModuleInstance module))
                {
                    module.attachedEquipmentInstanceId = string.Empty;
                    module.state = EquipmentModuleProcessState.Lost;
                    module.condition = 0f;
                }
            }
        }
        States.Remove(normalized);
    }

    public float GetCarriedWeight(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return 0f;
        }
        return Instances.Values
            .Where(instance => string.Equals(
                instance.ownerCharacterId,
                characterId,
                StringComparison.Ordinal))
            .Sum(instance => catalog.TryGet(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition)
                ? statProjector.Build(
                    definition,
                    crafting.ResolveInstanceMaterial(instance, definition),
                    instance).Weight
                : 0f);
    }

    public IReadOnlyList<CharacterCombatLoadoutState> Capture()
    {
        return States.Values.Select(CloneLoadout).ToArray();
    }

    internal void PopulateRestoreState(
        CombatEquipmentRuntimeState target,
        IEnumerable<CharacterCombatLoadoutState> savedLoadouts)
    {
        CombatEquipmentRuntimeState requiredTarget = target
            ?? throw new ArgumentNullException(nameof(target));
        foreach (CharacterCombatLoadoutState source in savedLoadouts
                     ?? Array.Empty<CharacterCombatLoadoutState>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.characterId)
                || requiredTarget.Loadouts.ContainsKey(source.characterId))
            {
                continue;
            }
            CharacterCombatLoadoutState restored = CloneLoadout(source);
            Sanitize(restored);
            requiredTarget.Loadouts.Add(restored.characterId, restored);
        }
    }

    public void RemoveEquipment(string instanceId) => store.RemoveEquipment(instanceId);

    private static CharacterCombatLoadoutProfile Profile(
        string id,
        string displayName,
        IEnumerable<string> weapons = null,
        IEnumerable<string> armor = null,
        string shield = "",
        int ammo = 0)
    {
        return new CharacterCombatLoadoutProfile
        {
            profileId = id,
            displayName = displayName,
            desiredWeaponDefinitionIds = weapons?.ToList() ?? new List<string>(),
            desiredArmorDefinitionIds = armor?.ToList() ?? new List<string>(),
            desiredShieldDefinitionId = shield,
            desiredAmmo = ammo
        };
    }

    private bool ValidateHandOccupancyForAssignment(
        CharacterCombatLoadoutProfile profile,
        CombatEquipmentDefinitionSO candidate,
        out string failureReason)
    {
        if (candidate is CombatShieldSO)
        {
            return ValidateHandOccupancy(
                profile,
                ResolveActiveWeaponDefinition(profile),
                candidate,
                out failureReason);
        }
        if (candidate is CombatWeaponSO weapon
            && string.IsNullOrWhiteSpace(profile.activeWeaponInstanceId))
        {
            return ValidateHandOccupancy(profile, weapon, out failureReason);
        }
        failureReason = string.Empty;
        return true;
    }

    private bool ValidateHandOccupancy(
        CharacterCombatLoadoutProfile profile,
        CombatWeaponSO activeWeapon,
        out string failureReason)
    {
        return ValidateHandOccupancy(
            profile,
            activeWeapon,
            ResolveShieldDefinition(profile),
            out failureReason);
    }

    private static bool ValidateHandOccupancy(
        CharacterCombatLoadoutProfile profile,
        CombatEquipmentDefinitionSO activeWeapon,
        CombatEquipmentDefinitionSO shield,
        out string failureReason)
    {
        if ((activeWeapon?.OccupiedHands ?? 0) + (shield?.OccupiedHands ?? 0) <= 2)
        {
            failureReason = string.Empty;
            return true;
        }
        failureReason = "equipment.loadout.insufficient_hands";
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

    private CombatWeaponSO ResolveActiveWeaponDefinition(
        CharacterCombatLoadoutProfile profile)
    {
        if (profile == null
            || string.IsNullOrWhiteSpace(profile.activeWeaponInstanceId)
            || !Instances.TryGetValue(
                profile.activeWeaponInstanceId,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            return null;
        }
        return definition as CombatWeaponSO;
    }

    private CombatEquipmentDefinitionSO ResolveShieldDefinition(
        CharacterCombatLoadoutProfile profile)
    {
        if (profile == null
            || string.IsNullOrWhiteSpace(profile.shieldInstanceId)
            || !Instances.TryGetValue(
                profile.shieldInstanceId,
                out CombatEquipmentInstance instance)
            || !catalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
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
            || !Instances.TryGetValue(
                profile.shieldInstanceId,
                out CombatEquipmentInstance previous))
        {
            return;
        }
        previous.ownerCharacterId = characterId?.Trim() ?? string.Empty;
        previous.worldState = CombatEquipmentWorldState.Carried;
    }

    private void Sanitize(CharacterCombatLoadoutState state)
    {
        foreach (CharacterCombatLoadoutProfile profile in state?.profiles
                     ?? new List<CharacterCombatLoadoutProfile>())
        {
            profile.weaponInstanceIds ??= new List<string>();
            profile.armorInstanceIds ??= new List<string>();
            profile.desiredWeaponDefinitionIds ??= new List<string>();
            profile.desiredArmorDefinitionIds ??= new List<string>();
            if (!ValidateProfileHandOccupancy(profile))
            {
                profile.shieldInstanceId = string.Empty;
            }
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
            if (!Instances.TryGetValue(instanceId, out CombatEquipmentInstance instance)
                || !catalog.TryGet(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition)
                || definition is not CombatArmorSO equippedArmor
                || equippedArmor.Layer != candidateArmor.Layer)
            {
                continue;
            }
            bool overlaps = equippedArmor.BodyPartDefense.Any(left => left != null
                && candidateArmor.BodyPartDefense.Any(right =>
                    right != null && right.bodyPart == left.bodyPart));
            if (overlaps)
            {
                failureReason = "equipment.loadout.armor_layer_conflict";
                return false;
            }
        }
        return true;
    }

    private static CharacterCombatLoadoutProfile GetActiveProfile(
        CharacterCombatLoadoutState state)
    {
        CharacterCombatLoadoutProfile profile = state.profiles.FirstOrDefault(item =>
            string.Equals(item.profileId, state.activeProfileId, StringComparison.Ordinal))
            ?? state.profiles.FirstOrDefault();
        if (profile == null)
        {
            profile = Profile(CombatLoadoutPresetIds.Peace, "평시");
            state.profiles.Add(profile);
        }
        state.activeProfileId = profile.profileId;
        return profile;
    }

    private static CharacterCombatLoadoutState CloneLoadout(
        CharacterCombatLoadoutState source)
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
