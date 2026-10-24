using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
using DungeonStory.Foundation.Facts;
using UnityEngine;

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
    AwaitingNarrativeRetry = 3,
    InvalidatedOwner = 4,
    InvalidatedEvidence = 5
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

[Serializable]
public sealed class CharacterSkillModuleSelection
{
    [ContentReference(ContentReferenceKind.SkillModule)]
    public string moduleId = string.Empty;
    [ContentReference(ContentReferenceKind.SkillVariant)]
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
    public int prominencePolicyVersion;
    public string primaryCapabilityId = string.Empty;
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
            prominencePolicyVersion = prominencePolicyVersion,
            primaryCapabilityId = primaryCapabilityId,
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
