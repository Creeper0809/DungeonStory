using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CharacterPotentialGrade
{
    Ordinary,
    Promising,
    Excellent,
    Exceptional,
    Genius
}

public enum CharacterSkillRarity
{
    Common,
    Advanced,
    Rare,
    Heroic,
    Legendary
}

public enum CharacterSkillKind
{
    SpeciesActive,
    OwnerFixed,
    Active,
    Passive,
    Ultimate
}

public enum CharacterUltimateDomain
{
    None,
    Offense,
    Defense,
    Management
}

public enum CharacterSkillTrigger
{
    ManualCombat,
    BattleStarted,
    DamageTaken,
    EnemyDefeated,
    BattleCompleted,
    InvasionStarted,
    WorkStarted,
    WorkCompleted,
    NeedChanged,
    MoodChanged,
    RelationshipChanged,
    OperatingDayStarted
}

public enum CharacterSkillTarget
{
    Self,
    Ally,
    Enemy,
    AllAllies,
    AllEnemies,
    Facility,
    Dungeon
}

public enum CharacterNarrativeDomain
{
    Work,
    FacilityUse,
    Need,
    Mood,
    Relationship,
    Injury,
    Survival,
    Invasion,
    Expedition,
    Combat
}

[Serializable]
public sealed class CharacterSkillModuleSelection
{
    public string moduleId = string.Empty;
    public string variantId = string.Empty;

    public CharacterSkillModuleSelection Clone()
    {
        return new CharacterSkillModuleSelection
        {
            moduleId = moduleId,
            variantId = variantId
        };
    }
}

[Serializable]
public sealed class CharacterSkillInstance
{
    public string id = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [TextArea] public string narrativeReason = string.Empty;
    public CharacterSkillKind kind;
    public CharacterSkillRarity rarity;
    public CharacterSkillTrigger trigger;
    public CharacterSkillTarget target;
    public CharacterUltimateDomain ultimateDomain;
    [Min(0)] public int cooldownTurns;
    public OffenseFormationMask usableFrom = OffenseFormationMask.Any;
    public OffenseFormationMask targetPositions = OffenseFormationMask.Any;
    public List<CharacterSkillModuleSelection> modules = new List<CharacterSkillModuleSelection>();
    public string requestKey = string.Empty;

    public bool IsReady => !string.IsNullOrWhiteSpace(id)
        && !string.IsNullOrWhiteSpace(displayName)
        && modules != null
        && modules.Count > 0;

    public CharacterSkillInstance Clone()
    {
        return new CharacterSkillInstance
        {
            id = id,
            displayName = displayName,
            description = description,
            narrativeReason = narrativeReason,
            kind = kind,
            rarity = rarity,
            trigger = trigger,
            target = target,
            ultimateDomain = ultimateDomain,
            cooldownTurns = cooldownTurns,
            usableFrom = usableFrom == OffenseFormationMask.None
                ? OffenseFormationMask.Any
                : usableFrom,
            targetPositions = targetPositions == OffenseFormationMask.None
                ? OffenseFormationMask.Any
                : targetPositions,
            modules = modules?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillModuleSelection>(),
            requestKey = requestKey
        };
    }
}

[Serializable]
public sealed class CharacterSkillCandidateRule
{
    public CharacterSkillRarity rarity;
    [Min(1)] public int budget = 1;
    public CharacterSkillTrigger trigger;
    public CharacterSkillTarget target;
    public OffenseFormationMask usableFrom = OffenseFormationMask.Any;
    public OffenseFormationMask targetPositions = OffenseFormationMask.Any;
    public List<string> allowedModuleIds = new List<string>();
    public List<string> allowedVariantIds = new List<string>();

    public CharacterSkillCandidateRule Clone()
    {
        return new CharacterSkillCandidateRule
        {
            rarity = rarity,
            budget = budget,
            trigger = trigger,
            target = target,
            usableFrom = usableFrom == OffenseFormationMask.None
                ? OffenseFormationMask.Any
                : usableFrom,
            targetPositions = targetPositions == OffenseFormationMask.None
                ? OffenseFormationMask.Any
                : targetPositions,
            allowedModuleIds = allowedModuleIds?.ToList() ?? new List<string>(),
            allowedVariantIds = allowedVariantIds?.ToList() ?? new List<string>()
        };
    }
}

public static class CharacterSkillFormationRules
{
    public static void Resolve(
        CharacterSkillTarget target,
        IEnumerable<CharacterSkillModuleSelection> modules,
        out OffenseFormationMask usableFrom,
        out OffenseFormationMask targetPositions)
    {
        string[] moduleIds = (modules ?? Enumerable.Empty<CharacterSkillModuleSelection>())
            .Where(module => module != null)
            .Select(module => module.moduleId ?? string.Empty)
            .ToArray();
        bool friendlyTarget = target is CharacterSkillTarget.Self
            or CharacterSkillTarget.Ally
            or CharacterSkillTarget.AllAllies
            or CharacterSkillTarget.Facility
            or CharacterSkillTarget.Dungeon;
        bool hasDirectAttack = moduleIds.Any(id => string.Equals(id, "damage", StringComparison.Ordinal));
        bool hasControl = moduleIds.Any(IsControlModule);

        if (friendlyTarget)
        {
            usableFrom = OffenseFormationMask.Middle | OffenseFormationMask.Rear;
            targetPositions = OffenseFormationMask.Any;
            return;
        }

        if (hasControl && !hasDirectAttack)
        {
            usableFrom = OffenseFormationMask.Middle | OffenseFormationMask.Rear;
            targetPositions = OffenseFormationMask.Any;
            return;
        }

        usableFrom = OffenseFormationMask.Front | OffenseFormationMask.Middle;
        targetPositions = OffenseFormationMask.Front | OffenseFormationMask.Middle;
    }

    public static string Format(OffenseFormationMask mask)
    {
        OffenseFormationMask normalized = mask == OffenseFormationMask.None
            ? OffenseFormationMask.Any
            : mask;
        if (normalized == OffenseFormationMask.Any)
        {
            return "Any";
        }

        List<string> names = new List<string>();
        if ((normalized & OffenseFormationMask.Front) != 0) names.Add("Front");
        if ((normalized & OffenseFormationMask.Middle) != 0) names.Add("Middle");
        if ((normalized & OffenseFormationMask.Rear) != 0) names.Add("Rear");
        return string.Join("|", names);
    }

    private static bool IsControlModule(string moduleId)
    {
        return string.Equals(moduleId, "delay", StringComparison.Ordinal)
            || string.Equals(moduleId, "debuff", StringComparison.Ordinal)
            || string.Equals(moduleId, "vulnerability", StringComparison.Ordinal)
            || string.Equals(moduleId, "dot", StringComparison.Ordinal)
            || string.Equals(moduleId, "reposition", StringComparison.Ordinal)
            || string.Equals(moduleId, "multi_target", StringComparison.Ordinal)
            || string.Equals(moduleId, "conditional_amplify", StringComparison.Ordinal)
            || string.Equals(moduleId, "cooldown_adjust", StringComparison.Ordinal);
    }
}

[Serializable]
public sealed class CharacterSkillDraft
{
    public int unlockLevel;
    public CharacterSkillKind kind;
    public CharacterUltimateDomain requestedUltimateDomain;
    public string requestKey = string.Empty;
    public bool requestSubmitted;
    public bool isReady;
    public bool permanentlyChosen;
    public int chosenIndex = -1;
    public bool grantsUpperRarityPity;
    public List<CharacterSkillCandidateRule> rules = new List<CharacterSkillCandidateRule>();
    public List<CharacterSkillInstance> candidates = new List<CharacterSkillInstance>();

    public CharacterSkillInstance ChosenSkill => permanentlyChosen
        && chosenIndex >= 0
        && candidates != null
        && chosenIndex < candidates.Count
            ? candidates[chosenIndex]
            : null;

    public CharacterSkillDraft Clone()
    {
        return new CharacterSkillDraft
        {
            unlockLevel = unlockLevel,
            kind = kind,
            requestedUltimateDomain = requestedUltimateDomain,
            requestKey = requestKey,
            requestSubmitted = requestSubmitted,
            isReady = isReady,
            permanentlyChosen = permanentlyChosen,
            chosenIndex = chosenIndex,
            grantsUpperRarityPity = grantsUpperRarityPity,
            rules = rules?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillCandidateRule>(),
            candidates = candidates?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillInstance>()
        };
    }
}

[Serializable]
public sealed class CharacterSkillUseLimitState
{
    public int offenseBattleSerial = -1;
    public int defenseInvasionSerial = -1;
    public int managementOperatingDay = -1;

    public bool CanUse(CharacterUltimateDomain domain, int serial)
    {
        return domain switch
        {
            CharacterUltimateDomain.Offense => offenseBattleSerial != serial,
            CharacterUltimateDomain.Defense => defenseInvasionSerial != serial,
            CharacterUltimateDomain.Management => managementOperatingDay != serial,
            _ => false
        };
    }

    public void MarkUsed(CharacterUltimateDomain domain, int serial)
    {
        switch (domain)
        {
            case CharacterUltimateDomain.Offense:
                offenseBattleSerial = serial;
                break;
            case CharacterUltimateDomain.Defense:
                defenseInvasionSerial = serial;
                break;
            case CharacterUltimateDomain.Management:
                managementOperatingDay = serial;
                break;
        }
    }

    public CharacterSkillUseLimitState Clone()
    {
        return new CharacterSkillUseLimitState
        {
            offenseBattleSerial = offenseBattleSerial,
            defenseInvasionSerial = defenseInvasionSerial,
            managementOperatingDay = managementOperatingDay
        };
    }
}

[Serializable]
public sealed class CharacterGrowthAllocationRecord
{
    public int level;
    public CharacterStatType statType;
    public string reason = string.Empty;

    public CharacterGrowthAllocationRecord Clone()
    {
        return new CharacterGrowthAllocationRecord
        {
            level = level,
            statType = statType,
            reason = reason ?? string.Empty
        };
    }
}

[Serializable]
public sealed class CharacterGrowthState
{
    public bool initialized;
    public bool autoChooseDrafts;
    public CharacterPotentialGrade potentialGrade;
    public int generationSeed;
    public string origin = string.Empty;
    public string displayName = string.Empty;
    public CharacterStatBlock initialBaseStats = CharacterStatBlock.CreateDefault();
    public CharacterStatBlock levelGrowthStats = new CharacterStatBlock();
    public List<int> traitIds = new List<int>();
    public List<CharacterSkillInstance> activeSkills = new List<CharacterSkillInstance>();
    public List<CharacterSkillInstance> passiveSkills = new List<CharacterSkillInstance>();
    public CharacterSkillInstance ultimate;
    public List<CharacterSkillDraft> drafts = new List<CharacterSkillDraft>();
    public List<string> pendingRequestKeys = new List<string>();
    public List<CharacterGrowthAllocationRecord> allocationRecords =
        new List<CharacterGrowthAllocationRecord>();
    public CharacterSkillUseLimitState useLimits = new CharacterSkillUseLimitState();
    public int allocatedGrowthPoints;
    public bool nextActiveDraftHasPity;
    public int skillGenerationRevision;

    public void EnsureCollections()
    {
        initialBaseStats ??= CharacterStatBlock.CreateDefault();
        levelGrowthStats ??= new CharacterStatBlock();
        traitIds ??= new List<int>();
        activeSkills ??= new List<CharacterSkillInstance>();
        passiveSkills ??= new List<CharacterSkillInstance>();
        drafts ??= new List<CharacterSkillDraft>();
        pendingRequestKeys ??= new List<string>();
        allocationRecords ??= new List<CharacterGrowthAllocationRecord>();
        useLimits ??= new CharacterSkillUseLimitState();
    }

    public CharacterGrowthState Clone()
    {
        EnsureCollections();
        return new CharacterGrowthState
        {
            initialized = initialized,
            autoChooseDrafts = autoChooseDrafts,
            potentialGrade = potentialGrade,
            generationSeed = generationSeed,
            origin = origin,
            displayName = displayName,
            initialBaseStats = CharacterSkillModelUtility.CopyStats(initialBaseStats),
            levelGrowthStats = CharacterSkillModelUtility.CopyStats(levelGrowthStats),
            traitIds = traitIds.ToList(),
            activeSkills = activeSkills.Where(item => item != null).Select(item => item.Clone()).ToList(),
            passiveSkills = passiveSkills.Where(item => item != null).Select(item => item.Clone()).ToList(),
            ultimate = ultimate?.Clone(),
            drafts = drafts.Where(item => item != null).Select(item => item.Clone()).ToList(),
            pendingRequestKeys = pendingRequestKeys.ToList(),
            allocationRecords = allocationRecords
                .Where(item => item != null)
                .Select(item => item.Clone())
                .ToList(),
            useLimits = useLimits.Clone(),
            allocatedGrowthPoints = allocatedGrowthPoints,
            nextActiveDraftHasPity = nextActiveDraftHasPity,
            skillGenerationRevision = skillGenerationRevision
        };
    }
}

[Serializable]
public sealed class CharacterNarrativeFact
{
    public CharacterNarrativeDomain domain;
    public string factId = string.Empty;
    public string subjectId = string.Empty;
    public string outcome = string.Empty;
    public int count;
    public float totalValue;
    public int lastDay;
    public int milestoneCount;

    public CharacterNarrativeFact Clone()
    {
        return (CharacterNarrativeFact)MemberwiseClone();
    }
}

[Serializable]
public sealed class CharacterNarrativeLedger
{
    private static readonly int[] Milestones = { 1, 3, 8, 20, 50 };

    public List<CharacterNarrativeFact> facts = new List<CharacterNarrativeFact>();

    public IReadOnlyList<CharacterNarrativeFact> Facts => facts ??= new List<CharacterNarrativeFact>();
    public int MeaningfulRecordCount => Facts.Sum(item => item?.milestoneCount ?? 0);
    public int MeaningfulDomainCount => Facts
        .Where(item => item != null && item.milestoneCount > 0)
        .Select(item => item.domain)
        .Distinct()
        .Count();

    public void Record(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value = 0f,
        int day = 0)
    {
        if (string.IsNullOrWhiteSpace(factId))
        {
            return;
        }

        facts ??= new List<CharacterNarrativeFact>();
        string normalizedFactId = factId.Trim();
        string normalizedSubject = subjectId?.Trim() ?? string.Empty;
        CharacterNarrativeFact fact = facts.Find(item => item != null
            && item.domain == domain
            && string.Equals(item.factId, normalizedFactId, StringComparison.Ordinal)
            && string.Equals(item.subjectId, normalizedSubject, StringComparison.Ordinal));
        if (fact == null)
        {
            fact = new CharacterNarrativeFact
            {
                domain = domain,
                factId = normalizedFactId,
                subjectId = normalizedSubject
            };
            facts.Add(fact);
        }

        fact.count++;
        fact.totalValue += value;
        fact.lastDay = Mathf.Max(fact.lastDay, day);
        fact.outcome = outcome?.Trim() ?? string.Empty;
        fact.milestoneCount = Milestones.Count(threshold => fact.count >= threshold);
    }

    public CharacterNarrativeLedger Clone()
    {
        return new CharacterNarrativeLedger
        {
            facts = Facts.Where(item => item != null).Select(item => item.Clone()).ToList()
        };
    }
}

[Serializable]
public sealed class WorldCharacterProfile
{
    public string persistentId = string.Empty;
    public int characterDataId = -1;
    public string displayName = string.Empty;
    public string origin = string.Empty;
    public bool isOwner;
    public bool isStaff;
    public bool isAlive = true;
    public bool isVisiting;
    public int visitCount;
    public CharacterSocialMemorySnapshot socialMemory = new CharacterSocialMemorySnapshot();
    public int level = 1;
    public int currentExperience;
    public CharacterGrowthState growth = new CharacterGrowthState();
    public CharacterNarrativeLedger narrative = new CharacterNarrativeLedger();

    public bool IsReady => growth != null
        && growth.activeSkills != null
        && growth.activeSkills.Count > 0
        && growth.passiveSkills != null
        && growth.passiveSkills.Count > 0;

    public WorldCharacterProfile Clone()
    {
        return new WorldCharacterProfile
        {
            persistentId = persistentId,
            characterDataId = characterDataId,
            displayName = displayName,
            origin = origin,
            isOwner = isOwner,
            isStaff = isStaff,
            isAlive = isAlive,
            isVisiting = isVisiting,
            visitCount = visitCount,
            socialMemory = socialMemory?.Clone() ?? new CharacterSocialMemorySnapshot(),
            level = level,
            currentExperience = currentExperience,
            growth = growth?.Clone() ?? new CharacterGrowthState(),
            narrative = narrative?.Clone() ?? new CharacterNarrativeLedger()
        };
    }
}

public static class CharacterSkillDisplay
{
    public static string Potential(CharacterPotentialGrade grade)
    {
        return grade switch
        {
            CharacterPotentialGrade.Promising => "유망",
            CharacterPotentialGrade.Excellent => "우수",
            CharacterPotentialGrade.Exceptional => "탁월",
            CharacterPotentialGrade.Genius => "천재",
            _ => "평범"
        };
    }

    public static string Rarity(CharacterSkillRarity rarity)
    {
        return rarity switch
        {
            CharacterSkillRarity.Advanced => "고급",
            CharacterSkillRarity.Rare => "희귀",
            CharacterSkillRarity.Heroic => "영웅",
            CharacterSkillRarity.Legendary => "전설",
            _ => "일반"
        };
    }
}

public static class CharacterSkillModelUtility
{
    public static CharacterStatBlock CopyStats(CharacterStatBlock source)
    {
        CharacterStatBlock copy = new CharacterStatBlock();
        if (source != null)
        {
            copy.Add(source);
        }

        return copy;
    }
}
