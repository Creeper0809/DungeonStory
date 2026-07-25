using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class CharacterWeightedRarity
{
    public CharacterSkillRarity rarity;
    [Min(0.001f)] public float weight = 1f;

    public CharacterWeightedRarity()
    {
    }

    public CharacterWeightedRarity(CharacterSkillRarity rarity, float weight)
    {
        this.rarity = rarity;
        this.weight = Mathf.Max(0.001f, weight);
    }
}

[Serializable]
public sealed class CharacterPotentialRarityProfile
{
    public CharacterPotentialGrade potential;
    public List<CharacterWeightedRarity> rarityWeights = new List<CharacterWeightedRarity>();
}

[Serializable]
public sealed class CharacterRarityBudget
{
    public CharacterSkillRarity rarity;
    [Min(1)] public int budget = 1;
}

[Serializable]
public sealed class CharacterSkillNumericVariant
{
    public string id = string.Empty;
    public string displayName = string.Empty;
    public float primaryValue;
    public float secondaryValue;
    [Min(0)] public int duration;
    [Min(1)] public int count = 1;
    [Min(1)] public int cost = 1;

    public CharacterSkillNumericVariant()
    {
    }

    public CharacterSkillNumericVariant(
        string id,
        string displayName,
        float primaryValue,
        float secondaryValue,
        int duration,
        int count,
        int cost)
    {
        this.id = id;
        this.displayName = displayName;
        this.primaryValue = primaryValue;
        this.secondaryValue = secondaryValue;
        this.duration = Mathf.Max(0, duration);
        this.count = Mathf.Max(1, count);
        this.cost = Mathf.Max(1, cost);
    }
}

[Serializable]
public abstract class CharacterSkillModuleRule
{
    public string id = string.Empty;
    public string displayName = string.Empty;
    public List<CharacterSkillKind> allowedKinds = new List<CharacterSkillKind>();
    public List<CharacterSkillTrigger> allowedTriggers = new List<CharacterSkillTrigger>();
    public List<CharacterSkillTarget> allowedTargets = new List<CharacterSkillTarget>();
    public List<CharacterSkillNumericVariant> variants = new List<CharacterSkillNumericVariant>();

    public bool Allows(CharacterSkillKind kind, CharacterSkillTrigger trigger, CharacterSkillTarget target)
    {
        return !string.IsNullOrWhiteSpace(id)
            && (allowedKinds == null || allowedKinds.Count == 0 || allowedKinds.Contains(kind))
            && (allowedTriggers == null || allowedTriggers.Count == 0 || allowedTriggers.Contains(trigger))
            && (allowedTargets == null || allowedTargets.Count == 0 || allowedTargets.Contains(target));
    }

    public CharacterSkillNumericVariant FindVariant(string variantId)
    {
        return variants?.FirstOrDefault(item => item != null
            && string.Equals(item.id, variantId, StringComparison.Ordinal));
    }
}

[Serializable]
public sealed class CharacterCombatSkillModuleRule : CharacterSkillModuleRule
{
}

[Serializable]
public sealed class CharacterManagementSkillModuleRule : CharacterSkillModuleRule
{
}

[Serializable]
public sealed class CharacterTraitConflictRule
{
    public int firstTraitId;
    public int secondTraitId;
}

[CreateAssetMenu(menuName = "DungeonStory/Character/Skill System Settings", order = 20)]
public sealed class CharacterSkillSystemSettingsSO : ScriptableObject
{
    [Header("Growth")]
    [Min(1)] public int maxLevel = 50;
    [Min(1)] public int initialStatTotal = 55;
    [Min(1)] public int initialStatMin = 1;
    [Min(1)] public int initialStatMax = 10;
    [Min(1)] public int levelGrowthStatCap = 30;
    [Range(0f, 1f)] public float identityGrowthWeight = 0.6f;
    [Min(1)] public int secondPassiveMinimumLevel = 25;
    [Min(1)] public int secondPassiveMinimumRecords = 8;
    [Min(1)] public int secondPassiveMinimumDomains = 3;
    public int[] activeUnlockLevels = { 1, 5, 30 };

    [Header("Potential")]
    public float[] potentialPopulationWeights = { 45f, 30f, 15f, 8f, 2f };
    public List<CharacterPotentialRarityProfile> potentialRarityProfiles =
        new List<CharacterPotentialRarityProfile>();
    [Min(1f)] public float missedUpperRarityMultiplier = 1.5f;

    [Header("Generation")]
    [Min(0.25f)] public float initialRetrySeconds = 1f;
    [Min(1f)] public float maximumRetrySeconds = 30f;
    [Min(1)] public int guestReadyTarget = 8;
    [Min(0)] public int guestReadyLowWatermark = 4;
    [Min(1)] public int maximumAliveNonStaffGuests = 24;
    public List<CharacterRarityBudget> rarityBudgets = new List<CharacterRarityBudget>();
    public List<CharacterTraitConflictRule> traitConflicts = new List<CharacterTraitConflictRule>();

    [SerializeReference]
    private List<CharacterSkillModuleRule> modules = new List<CharacterSkillModuleRule>();

    public IReadOnlyList<CharacterSkillModuleRule> Modules => modules;

    private void OnEnable()
    {
        EnsureDefaults();
    }

    private void OnValidate()
    {
        EnsureDefaults();
    }

    public int GetBudget(CharacterSkillRarity rarity)
    {
        CharacterRarityBudget configured = rarityBudgets?.FirstOrDefault(item => item != null && item.rarity == rarity);
        return Mathf.Max(1, configured?.budget ?? 1);
    }

    public IReadOnlyList<CharacterWeightedRarity> GetRarityWeights(CharacterPotentialGrade potential)
    {
        CharacterPotentialRarityProfile profile = potentialRarityProfiles?
            .FirstOrDefault(item => item != null && item.potential == potential);
        return profile != null
            ? profile.rarityWeights
            : (IReadOnlyList<CharacterWeightedRarity>)Array.Empty<CharacterWeightedRarity>();
    }

    public CharacterSkillModuleRule FindModule(string moduleId)
    {
        return modules?.FirstOrDefault(item => item != null
            && string.Equals(item.id, moduleId, StringComparison.Ordinal));
    }

    public void EnsureDefaults()
    {
        activeUnlockLevels = activeUnlockLevels == null || activeUnlockLevels.Length == 0
            ? new[] { 1, 5, 30 }
            : activeUnlockLevels.Distinct().OrderBy(value => value).ToArray();
        if (potentialPopulationWeights == null || potentialPopulationWeights.Length != 5)
        {
            potentialPopulationWeights = new[] { 45f, 30f, 15f, 8f, 2f };
        }

        if (rarityBudgets == null || rarityBudgets.Count == 0)
        {
            rarityBudgets = new List<CharacterRarityBudget>
            {
                new CharacterRarityBudget { rarity = CharacterSkillRarity.Common, budget = 3 },
                new CharacterRarityBudget { rarity = CharacterSkillRarity.Advanced, budget = 5 },
                new CharacterRarityBudget { rarity = CharacterSkillRarity.Rare, budget = 8 },
                new CharacterRarityBudget { rarity = CharacterSkillRarity.Heroic, budget = 12 },
                new CharacterRarityBudget { rarity = CharacterSkillRarity.Legendary, budget = 18 }
            };
        }

        if (potentialRarityProfiles == null || potentialRarityProfiles.Count == 0)
        {
            potentialRarityProfiles = CreateDefaultRarityProfiles();
        }

        if (traitConflicts == null || traitConflicts.Count == 0)
        {
            traitConflicts = new List<CharacterTraitConflictRule>
            {
                new CharacterTraitConflictRule { firstTraitId = 101, secondTraitId = 102 },
                new CharacterTraitConflictRule { firstTraitId = 105, secondTraitId = 106 },
                new CharacterTraitConflictRule { firstTraitId = 102, secondTraitId = 103 }
            };
        }

        modules ??= new List<CharacterSkillModuleRule>();
        if (modules.Count == 0)
        {
            modules = CreateDefaultModules();
        }

        foreach (CharacterManagementSkillModuleRule managementModule in modules
            .OfType<CharacterManagementSkillModuleRule>())
        {
            managementModule.allowedTriggers ??= new List<CharacterSkillTrigger>();
            if (!managementModule.allowedTriggers.Contains(CharacterSkillTrigger.WorkStarted))
            {
                managementModule.allowedTriggers.Add(CharacterSkillTrigger.WorkStarted);
            }
        }
    }

    public static CharacterSkillSystemSettingsSO CreateRuntimeDefaults()
    {
        CharacterSkillSystemSettingsSO settings = CreateInstance<CharacterSkillSystemSettingsSO>();
        settings.hideFlags = HideFlags.HideAndDontSave;
        settings.EnsureDefaults();
        return settings;
    }

    private static List<CharacterPotentialRarityProfile> CreateDefaultRarityProfiles()
    {
        return new List<CharacterPotentialRarityProfile>
        {
            Profile(CharacterPotentialGrade.Ordinary, 60f, 27f, 10f, 2.7f, 0.3f),
            Profile(CharacterPotentialGrade.Promising, 48f, 31f, 15f, 5f, 1f),
            Profile(CharacterPotentialGrade.Excellent, 35f, 32f, 22f, 9f, 2f),
            Profile(CharacterPotentialGrade.Exceptional, 25f, 30f, 27f, 14f, 4f),
            Profile(CharacterPotentialGrade.Genius, 15f, 25f, 30f, 21f, 9f)
        };
    }

    private static CharacterPotentialRarityProfile Profile(
        CharacterPotentialGrade potential,
        params float[] weights)
    {
        return new CharacterPotentialRarityProfile
        {
            potential = potential,
            rarityWeights = Enum.GetValues(typeof(CharacterSkillRarity))
                .Cast<CharacterSkillRarity>()
                .Select((rarity, index) => new CharacterWeightedRarity(rarity, weights[index]))
                .ToList()
        };
    }

    private static List<CharacterSkillModuleRule> CreateDefaultModules()
    {
        List<CharacterSkillKind> combatKinds = new List<CharacterSkillKind>
        {
            CharacterSkillKind.Active,
            CharacterSkillKind.Passive,
            CharacterSkillKind.Ultimate
        };
        List<CharacterSkillTarget> combatTargets = new List<CharacterSkillTarget>
        {
            CharacterSkillTarget.Self,
            CharacterSkillTarget.Ally,
            CharacterSkillTarget.Enemy,
            CharacterSkillTarget.AllAllies,
            CharacterSkillTarget.AllEnemies
        };
        List<CharacterSkillTrigger> combatTriggers = new List<CharacterSkillTrigger>
        {
            CharacterSkillTrigger.ManualCombat,
            CharacterSkillTrigger.BattleStarted,
            CharacterSkillTrigger.DamageTaken,
            CharacterSkillTrigger.EnemyDefeated,
            CharacterSkillTrigger.BattleCompleted,
            CharacterSkillTrigger.InvasionStarted
        };
        List<CharacterSkillTrigger> managementTriggers = new List<CharacterSkillTrigger>
        {
            CharacterSkillTrigger.WorkStarted,
            CharacterSkillTrigger.WorkCompleted,
            CharacterSkillTrigger.NeedChanged,
            CharacterSkillTrigger.MoodChanged,
            CharacterSkillTrigger.RelationshipChanged,
            CharacterSkillTrigger.OperatingDayStarted
        };
        List<CharacterSkillTarget> managementTargets = new List<CharacterSkillTarget>
        {
            CharacterSkillTarget.Self,
            CharacterSkillTarget.Ally,
            CharacterSkillTarget.Facility,
            CharacterSkillTarget.Dungeon
        };

        return new List<CharacterSkillModuleRule>
        {
            Combat("damage", "피해", combatKinds, combatTriggers, combatTargets, V("light", 0.75f, 0f, 0, 1, 2), V("standard", 1.1f, 0f, 0, 1, 3), V("heavy", 1.6f, 2f, 0, 1, 6)),
            Combat("heal", "회복", combatKinds, combatTriggers, combatTargets, V("minor", 8f, 0f, 0, 1, 2), V("standard", 18f, 0f, 0, 1, 4), V("drain", 4f, 0.35f, 0, 1, 6)),
            Combat("guard", "방어", combatKinds, combatTriggers, combatTargets, V("brief", 0.25f, 0f, 1, 1, 2), V("strong", 0.45f, 0f, 2, 1, 5)),
            Combat("dot", "지속 피해", combatKinds, combatTriggers, combatTargets, V("bleed", 4f, 0f, 2, 1, 3), V("severe", 7f, 0f, 3, 1, 6)),
            Combat("vulnerability", "취약", combatKinds, combatTriggers, combatTargets, V("brief", 0.2f, 0f, 1, 1, 3), V("deep", 0.4f, 0f, 2, 1, 6)),
            Combat("delay", "지연", combatKinds, combatTriggers, combatTargets, V("short", 2f, 0f, 0, 1, 2), V("long", 5f, 0f, 0, 1, 5)),
            Combat("buff", "강화", combatKinds, combatTriggers, combatTargets, V("attack", 0.2f, 0f, 2, 1, 4), V("speed", 0.25f, 1f, 2, 1, 4)),
            Combat("debuff", "약화", combatKinds, combatTriggers, combatTargets, V("attack", 0.2f, 0f, 2, 1, 4), V("slow", 0.25f, 1f, 2, 1, 4)),
            Combat("cleanse", "정화", combatKinds, combatTriggers, combatTargets, V("one", 1f, 0f, 0, 1, 3), V("all", 1f, 0f, 0, 8, 7)),
            Combat("protect", "보호", combatKinds, combatTriggers, combatTargets, V("brief", 0.3f, 0f, 1, 1, 4), V("long", 0.35f, 0f, 3, 1, 7)),
            Combat("reposition", "위치 이동", combatKinds, combatTriggers, combatTargets, V("one", 1f, 0f, 0, 1, 3), V("two", 2f, 0f, 0, 1, 6)),
            Combat("multi_target", "다중 대상", combatKinds, combatTriggers, combatTargets, V("two", 2f, 0f, 0, 2, 3), V("all", 1f, 0f, 0, 8, 7)),
            Combat("conditional_amplify", "조건부 증폭", combatKinds, combatTriggers, combatTargets, V("wounded", 0.35f, 0.5f, 0, 1, 4), V("critical", 0.7f, 0.25f, 0, 1, 7)),
            Combat("cooldown_adjust", "재사용 조작", combatKinds, combatTriggers, combatTargets, V("one", -1f, 0f, 0, 1, 4), V("reset", -99f, 0f, 0, 1, 9)),
            Management("work_speed", "작업 속도", managementTriggers, managementTargets, V("small", 0.1f, 0f, 1, 1, 2), V("large", 0.25f, 0f, 1, 1, 5)),
            Management("output", "생산량", managementTriggers, managementTargets, V("small", 0.1f, 0f, 1, 1, 2), V("large", 0.3f, 0f, 1, 1, 6)),
            Management("cleaning", "청결", managementTriggers, managementTargets, V("small", 5f, 0f, 0, 1, 2), V("large", 15f, 0f, 0, 1, 5)),
            Management("repair", "수리", managementTriggers, managementTargets, V("small", 8f, 0f, 0, 1, 2), V("large", 25f, 0f, 0, 1, 6)),
            Management("stock", "재고", managementTriggers, managementTargets, V("small", 1f, 0f, 0, 1, 3), V("large", 3f, 0f, 0, 1, 7)),
            Management("research", "연구", managementTriggers, managementTargets, V("small", 5f, 0f, 0, 1, 2), V("large", 18f, 0f, 0, 1, 6)),
            Management("needs", "욕구", managementTriggers, managementTargets, V("small", 5f, 0f, 0, 1, 2), V("large", 15f, 0f, 0, 1, 5)),
            Management("mood", "기분", managementTriggers, managementTargets, V("small", 3f, 0f, 180, 1, 2), V("large", 8f, 0f, 180, 1, 6)),
            Management("relationship", "관계", managementTriggers, managementTargets, V("small", 2f, 0f, 0, 1, 2), V("large", 7f, 0f, 0, 1, 5)),
            Management("revenue", "수익", managementTriggers, managementTargets, V("small", 0.05f, 0f, 0, 1, 3), V("large", 0.2f, 0f, 0, 1, 7))
        };
    }

    private static CharacterCombatSkillModuleRule Combat(
        string id,
        string name,
        List<CharacterSkillKind> kinds,
        List<CharacterSkillTrigger> triggers,
        List<CharacterSkillTarget> targets,
        params CharacterSkillNumericVariant[] variants)
    {
        return new CharacterCombatSkillModuleRule
        {
            id = id,
            displayName = name,
            allowedKinds = kinds.ToList(),
            allowedTriggers = triggers.ToList(),
            allowedTargets = targets.ToList(),
            variants = variants.ToList()
        };
    }

    private static CharacterManagementSkillModuleRule Management(
        string id,
        string name,
        List<CharacterSkillTrigger> triggers,
        List<CharacterSkillTarget> targets,
        params CharacterSkillNumericVariant[] variants)
    {
        return new CharacterManagementSkillModuleRule
        {
            id = id,
            displayName = name,
            allowedKinds = new List<CharacterSkillKind> { CharacterSkillKind.Passive, CharacterSkillKind.Ultimate },
            allowedTriggers = triggers.ToList(),
            allowedTargets = targets.ToList(),
            variants = variants.ToList()
        };
    }

    private static CharacterSkillNumericVariant V(
        string id,
        float primary,
        float secondary,
        int duration,
        int count,
        int cost)
    {
        return new CharacterSkillNumericVariant(id, id, primary, secondary, duration, count, cost);
    }
}

public interface ICharacterSkillSystemSettingsProvider
{
    CharacterSkillSystemSettingsSO Settings { get; }
}

public sealed class ResourceCharacterSkillSystemSettingsProvider : ICharacterSkillSystemSettingsProvider
{
    private const string SettingsResourcePath = "SO/Character/CharacterSkillSystemSettings";

    private readonly IResourcesAssetLoader resourcesAssetLoader;
    private CharacterSkillSystemSettingsSO settings;

    public ResourceCharacterSkillSystemSettingsProvider(IResourcesAssetLoader resourcesAssetLoader = null)
    {
        this.resourcesAssetLoader = resourcesAssetLoader ?? new UnityResourcesAssetLoader();
    }

    public CharacterSkillSystemSettingsSO Settings
    {
        get
        {
            if (settings == null)
            {
                settings = resourcesAssetLoader.LoadOptional<CharacterSkillSystemSettingsSO>(
                        SettingsResourcePath)
                    ?? CharacterSkillSystemSettingsSO.CreateRuntimeDefaults();
                settings.EnsureDefaults();
            }

            return settings;
        }
    }
}
