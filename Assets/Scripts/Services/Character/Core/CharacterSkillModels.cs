using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
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

public enum CharacterSkillMechanicalPolicySource
{
    AuthoredRule,
    RequestedUltimateDomain,
    LegacyDeterministicDefault
}

public enum CharacterSkillPresentationState
{
    None = 0,
    PresentationPending = 1,
    Ready = 2,
    AwaitingNarrativeRetry = 3
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
    OperatingDayStarted,
    ManualWork
}

public enum CharacterSkillTargetingMode
{
    Self = 0,
    PlayerSelected = 1,
    DeterministicRandom = 2,
    AllEligible = 3
}

public enum CharacterSkillEffectArea
{
    Single = 0,
    Room = 1,
    Square = 2,
    Dungeon = 3
}

public static class CharacterSkillAreaRules
{
    public static void RequireValid(CharacterSkillTargetingMode targetingMode,
        CharacterSkillEffectArea effectArea, int areaSize)
    {
        if (!Enum.IsDefined(typeof(CharacterSkillTargetingMode), targetingMode)
            || !Enum.IsDefined(typeof(CharacterSkillEffectArea), effectArea))
            throw new InvalidOperationException("Unknown CharacterSkill targeting metadata.");
        if (effectArea == CharacterSkillEffectArea.Square)
        {
            if (areaSize < 3 || areaSize > 7 || areaSize % 2 == 0)
                throw new InvalidOperationException(
                    "CharacterSkill square areas require an odd size from 3 to 7.");
        }
        else if (areaSize != 1)
        {
            throw new InvalidOperationException(
                "Only square CharacterSkill areas may author areaSize above one.");
        }
        if (targetingMode == CharacterSkillTargetingMode.AllEligible
            && effectArea != CharacterSkillEffectArea.Dungeon)
            throw new InvalidOperationException(
                "AllEligible CharacterSkill targeting requires Dungeon area.");
        if (effectArea == CharacterSkillEffectArea.Dungeon
            && targetingMode != CharacterSkillTargetingMode.AllEligible)
            throw new InvalidOperationException(
                "Dungeon CharacterSkill area requires AllEligible targeting.");
        if (targetingMode == CharacterSkillTargetingMode.Self
            && effectArea != CharacterSkillEffectArea.Single)
            throw new InvalidOperationException(
                "Self CharacterSkill targeting requires Single area.");
    }

    public static int ResolveCostTargetCount(CharacterSkillEffectArea effectArea,
        int areaSize, int authoredTargetCount)
    {
        RequireValid(effectArea == CharacterSkillEffectArea.Dungeon
                ? CharacterSkillTargetingMode.AllEligible
                : effectArea == CharacterSkillEffectArea.Single
                    ? CharacterSkillTargetingMode.Self
                    : CharacterSkillTargetingMode.PlayerSelected,
            effectArea,
            areaSize);
        int estimate = effectArea switch
        {
            CharacterSkillEffectArea.Single => 1,
            CharacterSkillEffectArea.Room => 3,
            CharacterSkillEffectArea.Square => areaSize,
            CharacterSkillEffectArea.Dungeon => 8,
            _ => 1
        };
        return Math.Max(authoredTargetCount, estimate);
    }
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
public sealed class CharacterSkillDrawbackEffectEnvelope
{
    public string drawbackModuleId = string.Empty;
    public string effectId = string.Empty;
    public string targetId = string.Empty;
    public GameplayEffectOperation operation;
    public float value = 1f;
    [Min(1)] public int severityUnits = 1;
    public string displayName = string.Empty;

    /// <summary>
    /// Unity JsonUtility materializes an absent nested envelope with these exact
    /// field defaults. Only that complete sentinel is equivalent to a null stat
    /// effect; any partially populated or numerically changed envelope is data.
    /// </summary>
    public static bool IsExactSerializedAbsence(
        CharacterSkillDrawbackEffectEnvelope effect)
    {
        return effect != null
            && string.IsNullOrEmpty(effect.drawbackModuleId)
            && string.IsNullOrEmpty(effect.effectId)
            && string.IsNullOrEmpty(effect.targetId)
            && effect.operation == GameplayEffectOperation.AddFlat
            && effect.value == 1f
            && effect.severityUnits == 1
            && string.IsNullOrEmpty(effect.displayName);
    }

    public CharacterSkillDrawbackEffectEnvelope Clone() =>
        new CharacterSkillDrawbackEffectEnvelope
        {
            drawbackModuleId = drawbackModuleId,
            effectId = effectId,
            targetId = targetId,
            operation = operation,
            value = value,
            severityUnits = severityUnits,
            displayName = displayName
        };
}

[Serializable]
public sealed class CharacterSkillInstance
{
    public string id = string.Empty;
    public string ruleId = string.Empty;
    public string combinationId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    [TextArea] public string narrativeReason = string.Empty;
    public CharacterSkillKind kind;
    public CharacterSkillRarity rarity;
    public CharacterSkillTrigger trigger;
    public CharacterSkillTarget target;
    public CharacterSkillTargetingMode targetingMode;
    public CharacterSkillEffectArea effectArea;
    [Min(1)] public int areaSize = 1;
    public CharacterUltimateDomain ultimateDomain;
    [Min(0)] public int cooldownTurns;
    [Min(0)] public int manualDurationHours;
    [Min(0)] public int manualCooldownDays;
    public OffenseFormationMask usableFrom = OffenseFormationMask.Any;
    public OffenseFormationMask targetPositions = OffenseFormationMask.Any;
    public List<CharacterSkillModuleSelection> modules = new List<CharacterSkillModuleSelection>();
    public string requestKey = string.Empty;
    public NarrativeGenerationTrace narrativeTrace;
    [Tooltip("0 preserves legacy authored variant values. Positive versions require generated formula envelopes.")]
    public int formulaVersion;
    public string formulaCatalogSha256 = string.Empty;
    [Min(0)] public int calculatedCost;
    [Min(0)] public int positiveCost;
    [Min(0)] public int drawbackCredit;
    public string drawbackId = string.Empty;
    public CharacterSkillDrawbackEffectEnvelope drawbackEffect;
    [Min(0)] public int narrativeBudget;
    public bool drawbackEvidenceQualified;
    public List<string> evidenceIds = new List<string>();
    public List<GameplayOutcomeEvidenceBindingSnapshot> evidenceBindings = new();
    public List<CharacterSkillFormulaCapabilityEnvelope> formulaCapabilities =
        new List<CharacterSkillFormulaCapabilityEnvelope>();
    public string presentationId = string.Empty;
    [TextArea] public string mechanicalDescription = string.Empty;
    [TextArea] public string narrativeFlavor = string.Empty;

    public bool IsReady => formulaVersion == 0
        ? !string.IsNullOrWhiteSpace(id)
            && !string.IsNullOrWhiteSpace(displayName)
            && modules != null
            && modules.Count > 0
        : formulaVersion > 0
            && !string.IsNullOrWhiteSpace(id)
            && !string.IsNullOrWhiteSpace(displayName)
            && !string.IsNullOrWhiteSpace(presentationId)
            && !string.IsNullOrWhiteSpace(mechanicalDescription)
            && !string.IsNullOrWhiteSpace(narrativeFlavor)
            && formulaCapabilities != null
            && formulaCapabilities.Count > 0;

    public CharacterSkillInstance Clone()
    {
        return new CharacterSkillInstance
        {
            id = id,
            ruleId = ruleId,
            combinationId = combinationId,
            displayName = displayName,
            description = description,
            narrativeReason = narrativeReason,
            kind = kind,
            rarity = rarity,
            trigger = trigger,
            target = target,
            targetingMode = targetingMode,
            effectArea = effectArea,
            areaSize = areaSize,
            ultimateDomain = ultimateDomain,
            cooldownTurns = cooldownTurns,
            manualDurationHours = manualDurationHours,
            manualCooldownDays = manualCooldownDays,
            usableFrom = usableFrom == OffenseFormationMask.None
                ? OffenseFormationMask.Any
                : usableFrom,
            targetPositions = targetPositions == OffenseFormationMask.None
                ? OffenseFormationMask.Any
                : targetPositions,
            modules = modules?.Where(item => item != null).Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillModuleSelection>(),
            requestKey = requestKey,
            narrativeTrace = narrativeTrace,
            formulaVersion = formulaVersion,
            formulaCatalogSha256 = formulaCatalogSha256,
            calculatedCost = calculatedCost,
            positiveCost = positiveCost,
            drawbackCredit = drawbackCredit,
            drawbackId = drawbackId,
            drawbackEffect = drawbackEffect?.Clone(),
            narrativeBudget = narrativeBudget,
            drawbackEvidenceQualified = drawbackEvidenceQualified,
            evidenceIds = evidenceIds?.ToList() ?? new List<string>(),
            evidenceBindings = evidenceBindings?
                .Where(value => value != null)
                .Select(value => value.Clone()).ToList()
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>(),
            formulaCapabilities = formulaCapabilities?.Where(item => item != null)
                .Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillFormulaCapabilityEnvelope>(),
            presentationId = presentationId,
            mechanicalDescription = mechanicalDescription,
            narrativeFlavor = narrativeFlavor
        };
    }
}

[Serializable]
public sealed class CharacterSkillCandidateRule
{
    public string ruleId = string.Empty;
    public CharacterSkillRarity rarity;
    [Min(1)] public int budget = 1;
    public CharacterSkillTrigger trigger;
    public CharacterSkillTarget target;
    public CharacterSkillTargetingMode targetingMode;
    public CharacterSkillEffectArea effectArea;
    [Min(1)] public int areaSize = 1;
    public CharacterUltimateDomain ultimateDomain;
    [Min(0)] public int cooldownTurns;
    [Min(0)] public int manualDurationHours;
    [Min(0)] public int manualCooldownDays;
    public CharacterSkillMechanicalPolicySource mechanicalPolicySource;
    public OffenseFormationMask usableFrom = OffenseFormationMask.Any;
    public OffenseFormationMask targetPositions = OffenseFormationMask.Any;
    public List<string> allowedModuleIds = new List<string>();
    public List<string> allowedVariantIds = new List<string>();

    public CharacterSkillCandidateRule Clone()
    {
        return new CharacterSkillCandidateRule
        {
            ruleId = ruleId,
            rarity = rarity,
            budget = budget,
            trigger = trigger,
            target = target,
            targetingMode = targetingMode,
            effectArea = effectArea,
            areaSize = areaSize,
            ultimateDomain = ultimateDomain,
            cooldownTurns = cooldownTurns,
            manualDurationHours = manualDurationHours,
            manualCooldownDays = manualCooldownDays,
            mechanicalPolicySource = mechanicalPolicySource,
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
    public int formulaVersion;
    public string formulaCatalogSha256 = string.Empty;
    [Min(0)] public int formulaBudget;
    public CharacterSkillPresentationState presentationState;
    [Min(0)] public int presentationFailureCount;
    [Min(0)] public int nextPresentationIndex;
    public List<CharacterSkillInstance> frozenMechanics =
        new List<CharacterSkillInstance>();
    public List<CharacterSkillModuleOfferState> moduleSelectionOffers =
        new List<CharacterSkillModuleOfferState>();
    public List<GameplayOutcomeEvidenceBindingSnapshot> outcomeEvidenceBindings = new();

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
                ?? new List<CharacterSkillInstance>(),
            formulaVersion = formulaVersion,
            formulaCatalogSha256 = formulaCatalogSha256,
            formulaBudget = formulaBudget,
            presentationState = presentationState,
            presentationFailureCount = presentationFailureCount,
            nextPresentationIndex = nextPresentationIndex,
            frozenMechanics = frozenMechanics?.Where(item => item != null)
                .Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillInstance>(),
            moduleSelectionOffers = moduleSelectionOffers?.Where(item => item != null)
                .Select(item => item.Clone()).ToList()
                ?? new List<CharacterSkillModuleOfferState>(),
            outcomeEvidenceBindings = outcomeEvidenceBindings?
                .Where(value => value != null)
                .Select(value => value.Clone()).ToList()
                ?? new List<GameplayOutcomeEvidenceBindingSnapshot>()
        };
    }
}

[Serializable]
public sealed class CharacterSkillModuleOfferState
{
    public string selectionId = string.Empty;
    public string ruleId = string.Empty;
    public List<string> positiveModuleIds = new List<string>();
    public List<string> drawbackModuleIds = new List<string>();
    public List<string> evidenceFactIds = new List<string>();
    public List<string> qualifiedNegativeEvidenceFactIds = new List<string>();
    [Min(1)] public int maximumPositiveModules = 3;
    [Min(0)] public int maximumDrawbackModules;

    public CharacterSkillModuleOfferState Clone() => new CharacterSkillModuleOfferState
    {
        selectionId = selectionId,
        ruleId = ruleId,
        positiveModuleIds = positiveModuleIds?.ToList() ?? new List<string>(),
        drawbackModuleIds = drawbackModuleIds?.ToList() ?? new List<string>(),
        evidenceFactIds = evidenceFactIds?.ToList() ?? new List<string>(),
        qualifiedNegativeEvidenceFactIds = qualifiedNegativeEvidenceFactIds?.ToList()
            ?? new List<string>(),
        maximumPositiveModules = maximumPositiveModules,
        maximumDrawbackModules = maximumDrawbackModules
    };
}

[Serializable]
public sealed class CharacterManualSkillCooldownState
{
    public string skillId = string.Empty;
    public long readyAbsoluteHour;
    public int useCount;

    public CharacterManualSkillCooldownState Clone() => new CharacterManualSkillCooldownState
    {
        skillId = skillId,
        readyAbsoluteHour = readyAbsoluteHour,
        useCount = useCount
    };
}

[Serializable]
public sealed class CharacterManualSkillBuffState
{
    public string sourceCharacterId = string.Empty;
    public string skillId = string.Empty;
    public long expiresAbsoluteHour;
    public CharacterSkillInstance frozenSkill;

    public CharacterManualSkillBuffState Clone() => new CharacterManualSkillBuffState
    {
        sourceCharacterId = sourceCharacterId,
        skillId = skillId,
        expiresAbsoluteHour = expiresAbsoluteHour,
        frozenSkill = frozenSkill?.Clone()
    };
}

[Serializable]
public sealed class CharacterSkillUseLimitState
{
    public int offenseBattleSerial = -1;
    public int defenseInvasionSerial = -1;
    public int managementOperatingDay = -1;
    public List<CharacterManualSkillCooldownState> manualSkillCooldowns = new List<CharacterManualSkillCooldownState>();
    public List<CharacterManualSkillBuffState> manualSkillBuffs = new List<CharacterManualSkillBuffState>();

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
            managementOperatingDay = managementOperatingDay,
            manualSkillCooldowns = (manualSkillCooldowns
                ?? new List<CharacterManualSkillCooldownState>())
                .Where(value => value != null).Select(value => value.Clone()).ToList(),
            manualSkillBuffs = (manualSkillBuffs
                ?? new List<CharacterManualSkillBuffState>())
                .Where(value => value != null).Select(value => value.Clone()).ToList()
        };
    }
}

public enum CharacterTraitSelectionAuthorityOrigin
{
    None = 0,
    PreparedSelection = 1,
    PopulationGeneration = 2,
    LegacyCharacterDefinitionBootstrap = 3
}

[Serializable]
public sealed class CharacterGrowthState
{
    public const int CurrentTraitSelectionAuthorityVersion = 1;

    public bool initialized;
    public bool autoChooseDrafts;
    public CharacterPotentialGrade potentialGrade;
    public int generationSeed;
    public string origin = string.Empty;
    public string displayName = string.Empty;
    public CharacterStartingProfileState startingProfile =
        new CharacterStartingProfileState();
    public List<CharacterStartingProficiencyExperience> startingProficiencies =
        new List<CharacterStartingProficiencyExperience>();
    public int traitSelectionAuthorityVersion;
    public CharacterTraitSelectionAuthorityOrigin traitSelectionAuthorityOrigin;
    public List<int> traitIds = new List<int>();
    public List<CharacterSkillInstance> activeSkills = new List<CharacterSkillInstance>();
    public List<CharacterSkillInstance> passiveSkills = new List<CharacterSkillInstance>();
    public CharacterSkillInstance ultimate;
    public List<CharacterSkillDraft> drafts = new List<CharacterSkillDraft>();
    public List<string> pendingRequestKeys = new List<string>();
    public CharacterSkillUseLimitState useLimits = new CharacterSkillUseLimitState();
    public bool nextActiveDraftHasPity;
    public int skillGenerationRevision;

    public void EnsureCollections()
    {
        startingProficiencies ??=
            new List<CharacterStartingProficiencyExperience>();
        startingProfile ??= new CharacterStartingProfileState();
        startingProfile.EnsureCollections();
        foreach (CharacterStartingProficiencyExperience value in
                 startingProficiencies.Where(value => value != null))
        {
            CharacterProficiencyId proficiencyId = new(value.proficiencyId);
            value.learningMultiplier = startingProfile.prepared
                ? CharacterProficiencySpecializationRules.Resolve(
                    startingProfile,
                    proficiencyId)
                : CharacterProficiencySpecializationRules
                    .NormalizeSerializedMultiplier(value.learningMultiplier);
        }
        traitIds ??= new List<int>();
        activeSkills ??= new List<CharacterSkillInstance>();
        passiveSkills ??= new List<CharacterSkillInstance>();
        drafts ??= new List<CharacterSkillDraft>();
        pendingRequestKeys ??= new List<string>();
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
            startingProfile = startingProfile.Clone(),
            startingProficiencies = startingProficiencies
                .Where(item => item != null)
                .Select(item => item.Clone())
                .ToList(),
            traitSelectionAuthorityVersion = traitSelectionAuthorityVersion,
            traitSelectionAuthorityOrigin = traitSelectionAuthorityOrigin,
            traitIds = traitIds.ToList(),
            activeSkills = activeSkills.Where(item => item != null).Select(item => item.Clone()).ToList(),
            passiveSkills = passiveSkills.Where(item => item != null).Select(item => item.Clone()).ToList(),
            ultimate = ultimate?.Clone(),
            drafts = drafts.Where(item => item != null).Select(item => item.Clone()).ToList(),
            pendingRequestKeys = pendingRequestKeys.ToList(),
            useLimits = useLimits.Clone(),
            nextActiveDraftHasPity = nextActiveDraftHasPity,
            skillGenerationRevision = skillGenerationRevision
        };
    }
}

[Serializable]
public sealed class CharacterNarrativeEvidenceMetadata
{
    public const float OrdinaryImportance = 1f;

    public CharacterNarrativeEvidenceMetadata(
        string eventGroupKey,
        string actionKey,
        string relationshipKey,
        float importancePoints)
    {
        this.eventGroupKey = RequireCanonical(eventGroupKey, nameof(eventGroupKey));
        this.actionKey = RequireCanonical(actionKey, nameof(actionKey));
        this.relationshipKey = NormalizeOptional(relationshipKey, nameof(relationshipKey));
        if (float.IsNaN(importancePoints) || float.IsInfinity(importancePoints) || importancePoints < 0f)
            throw new ArgumentOutOfRangeException(nameof(importancePoints));
        this.importancePoints = importancePoints;
    }

    public string eventGroupKey { get; }
    public string actionKey { get; }
    public string relationshipKey { get; }
    public float importancePoints { get; }

    public static CharacterNarrativeEvidenceMetadata Ordinary(
        CharacterNarrativeDomain domain,
        string factId)
    {
        string canonicalFact = factId?.Trim() ?? string.Empty;
        return new CharacterNarrativeEvidenceMetadata(
            "ordinary:" + domain,
            "ordinary:" + canonicalFact,
            string.Empty,
            OrdinaryImportance);
    }

    private static string RequireCanonical(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0 || !string.Equals(canonical, value, StringComparison.Ordinal))
            throw new ArgumentException("A canonical non-empty narrative evidence key is required.", name);
        return canonical;
    }

    private static string NormalizeOptional(string value, string name)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (!string.Equals(canonical, value ?? string.Empty, StringComparison.Ordinal))
            throw new ArgumentException("Narrative evidence keys must already be canonical.", name);
        return canonical;
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
    [Min(0)] public int influenceUseCount;
    public string eventGroupKey = string.Empty;
    public string actionKey = string.Empty;
    public string relationshipKey = string.Empty;
    [Min(0f)] public float importancePoints = 1f;
    public GameplayNarrativeEventContext lastEventContext = new GameplayNarrativeEventContext();

    public CharacterNarrativeFact Clone()
    {
        CharacterNarrativeFact clone = (CharacterNarrativeFact)MemberwiseClone();
        clone.lastEventContext = lastEventContext?.Clone() ?? new GameplayNarrativeEventContext();
        return clone;
    }
}

[Serializable]
public sealed class CharacterNarrativeLedger
{
    private static readonly int[] Milestones = { 1, 3, 8, 20, 50 };

    public List<CharacterNarrativeFact> facts = new List<CharacterNarrativeFact>();
    public long nextEventSequence = 1L;

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
        if (string.IsNullOrWhiteSpace(factId)) return;
        Record(domain, factId, subjectId, outcome, value, day,
            CharacterNarrativeEvidenceMetadata.Ordinary(domain, factId));
    }

    public void Record(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day,
        CharacterNarrativeEvidenceMetadata metadata)
    {
        Record(domain, factId, subjectId, outcome, value, day, metadata, null);
    }

    public void Record(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day,
        CharacterNarrativeEvidenceMetadata metadata,
        GameplayNarrativeEventContext eventContext)
    {
        if (string.IsNullOrWhiteSpace(factId))
        {
            return;
        }

        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
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
                subjectId = normalizedSubject,
                eventGroupKey = metadata.eventGroupKey,
                actionKey = metadata.actionKey,
                relationshipKey = metadata.relationshipKey,
                importancePoints = metadata.importancePoints
            };
            facts.Add(fact);
        }
        else if (!string.Equals(fact.eventGroupKey, metadata.eventGroupKey, StringComparison.Ordinal)
            || !string.Equals(fact.actionKey, metadata.actionKey, StringComparison.Ordinal)
            || !string.Equals(fact.relationshipKey, metadata.relationshipKey, StringComparison.Ordinal)
            || fact.importancePoints != metadata.importancePoints)
        {
            throw new InvalidOperationException(
                $"Narrative fact '{normalizedFactId}' cannot change its formula evidence metadata.");
        }

        fact.count++;
        fact.totalValue += value;
        fact.lastDay = Mathf.Max(fact.lastDay, day);
        fact.outcome = outcome?.Trim() ?? string.Empty;
        fact.milestoneCount = Milestones.Count(threshold => fact.count >= threshold);
        if (eventContext != null)
        {
            fact.lastEventContext = eventContext.Clone();
            if (fact.lastEventContext.sequence <= 0L)
                fact.lastEventContext.sequence = Math.Max(1L, nextEventSequence);
            nextEventSequence = Math.Max(nextEventSequence, fact.lastEventContext.sequence + 1L);
        }
    }

    public void Record(
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day,
        string eventGroupKey,
        string actionKey,
        string relationshipKey,
        float importancePoints)
    {
        Record(domain, factId, subjectId, outcome, value, day,
            new CharacterNarrativeEvidenceMetadata(
                eventGroupKey,
                actionKey,
                relationshipKey,
                importancePoints));
    }

    public CharacterNarrativeLedger Clone()
    {
        return new CharacterNarrativeLedger
        {
            facts = Facts.Where(item => item != null).Select(item => item.Clone()).ToList(),
            nextEventSequence = Math.Max(1L, nextEventSequence)
        };
    }
}

[Serializable]
public sealed class WorldCharacterProfile : ICharacterPopulationProfileState
{
    public string persistentId = string.Empty;
    public int characterDataId = -1;
    public string displayName = string.Empty;
    public string origin = string.Empty;
    public bool isOwner;
    public bool isStaff;
    public bool isAlive = true;
    public bool isVisiting;
    public CharacterSettlementStanding settlementStanding;
    public int visitCount;
    public CharacterSocialMemorySnapshot socialMemory = new CharacterSocialMemorySnapshot();
    public int level = 1;
    public int currentExperience;
    public CharacterGrowthState growth = new CharacterGrowthState();
    public CharacterNarrativeLedger narrative = new CharacterNarrativeLedger();
    public CharacterAcquiredTraitAggregateState acquiredTraits =
        new CharacterAcquiredTraitAggregateState();

    public bool IsReady => growth != null
        && growth.activeSkills != null
        && growth.activeSkills.Count > 0
        && growth.passiveSkills != null
        && growth.passiveSkills.Count > 0;

    string ICharacterPopulationProfileState.PersistentId => persistentId;
    int ICharacterPopulationProfileState.CharacterDataId => characterDataId;
    bool ICharacterPopulationProfileState.IsAlive
    {
        get => isAlive;
        set => isAlive = value;
    }
    bool ICharacterPopulationProfileState.IsStaff
    {
        get => isStaff;
        set
        {
            isStaff = value;
            if (value && settlementStanding is
                CharacterSettlementStanding.Unknown or
                CharacterSettlementStanding.PreparedCandidate or
                CharacterSettlementStanding.Visitor)
            {
                settlementStanding = CharacterSettlementStanding.Resident;
            }
            else if (!value && CharacterSettlementStandingRules
                         .IsSettlementResident(settlementStanding))
            {
                settlementStanding = isVisiting
                    ? CharacterSettlementStanding.Visitor
                    : CharacterSettlementStanding.PreparedCandidate;
            }
        }
    }
    bool ICharacterPopulationProfileState.IsVisiting
    {
        get => isVisiting;
        set
        {
            isVisiting = value;
            if (value)
            {
                settlementStanding = CharacterSettlementStanding.Visitor;
                isStaff = false;
            }
            else if (settlementStanding == CharacterSettlementStanding.Visitor)
            {
                settlementStanding = CharacterSettlementStanding.PreparedCandidate;
            }
        }
    }
    CharacterSettlementStanding ICharacterPopulationProfileState.SettlementStanding
    {
        get => settlementStanding;
        set => settlementStanding = value;
    }
    int ICharacterPopulationProfileState.VisitCount
    {
        get => visitCount;
        set => visitCount = value;
    }

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
            settlementStanding = settlementStanding,
            visitCount = visitCount,
            socialMemory = socialMemory?.Clone() ?? new CharacterSocialMemorySnapshot(),
            level = level,
            currentExperience = currentExperience,
            growth = growth?.Clone() ?? new CharacterGrowthState(),
            narrative = narrative?.Clone() ?? new CharacterNarrativeLedger(),
            acquiredTraits = (acquiredTraits
                ?? throw new InvalidOperationException(
                    $"World profile '{persistentId}' has no current acquired-trait state."))
                .Clone()
        };
    }
}

[Serializable]
public sealed class CharacterSkillFormulaParameter
{
    public string parameterId = string.Empty;
    public long units;

    public CharacterSkillFormulaParameter Clone()
    {
        return new CharacterSkillFormulaParameter
        {
            parameterId = parameterId,
            units = units
        };
    }
}

[Serializable]
public sealed class CharacterSkillFormulaCapabilityEnvelope
{
    public string capabilityId = string.Empty;
    public string formatterId = string.Empty;
    public string applicatorId = string.Empty;
    public List<CharacterSkillFormulaParameter> parameters =
        new List<CharacterSkillFormulaParameter>();

    public CharacterSkillFormulaCapabilityEnvelope Clone()
    {
        return new CharacterSkillFormulaCapabilityEnvelope
        {
            capabilityId = capabilityId,
            formatterId = formatterId,
            applicatorId = applicatorId,
            parameters = parameters?.Where(value => value != null)
                .Select(value => value.Clone()).ToList()
                ?? new List<CharacterSkillFormulaParameter>()
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
