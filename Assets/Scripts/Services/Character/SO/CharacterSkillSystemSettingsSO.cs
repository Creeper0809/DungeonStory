using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

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
public sealed class CharacterSkillFormulaAxisDefinition
{
    public string parameterId = string.Empty;
    [Min(0)] public long minimumUnits;
    [Min(0)] public long maximumUnits;
    [Min(1)] public long quantumUnits = 1;
    [Range(0, 6)] public int decimalPlaces;
    [Min(1)] public int costPerQuantum = 1;

    public NarrativeFormulaQuantizedRange ToRuntime()
    {
        return new NarrativeFormulaQuantizedRange(
            parameterId,
            minimumUnits,
            maximumUnits,
            quantumUnits,
            decimalPlaces,
            costPerQuantum);
    }
}

[Serializable]
public sealed class CharacterSkillPairSynergyCostDefinition
{
    public string otherCapabilityId = string.Empty;
    [Min(0)] public int cost;
}

[Serializable]
public sealed class CharacterSkillCapabilityFormulaDefinition
{
    [Tooltip("Formula axes consumed by the registered runtime applicator. Unlisted axes must be fixed ranges.")]
    public List<string> appliedParameterIds = new List<string>();
    [Min(0)] public int baseCost;
    [FormerlySerializedAs("triggerFrequencyCost")]
    [Min(0)] public int triggerFrequencyCostPerUnit;
    [FormerlySerializedAs("certaintyCost")]
    [Min(0)] public int guaranteedProcCost;
    [FormerlySerializedAs("areaCost")]
    [Min(0)] public int areaCostPerExtraTarget;
    [FormerlySerializedAs("multiEffectCost")]
    [Min(0)] public int multiEffectCostPerExtraEffect;
    public List<CharacterSkillPairSynergyCostDefinition> pairSynergyCosts =
        new List<CharacterSkillPairSynergyCostDefinition>();
    [Min(0f)] public float narrativeAffinity = 1f;
    public List<string> affinityKeys = new List<string>();
    public List<string> conflictGroups = new List<string>();
    public List<string> forbiddenSynergies = new List<string>();
    public string formatterId = string.Empty;
    public string applicatorId = string.Empty;
    public List<CharacterSkillFormulaAxisDefinition> parameterRanges =
        new List<CharacterSkillFormulaAxisDefinition>();

    public NarrativeFormulaCapabilityDescriptor ToRuntime(string capabilityId)
    {
        if (parameterRanges == null || parameterRanges.Any(value => value == null))
        {
            throw new InvalidOperationException(
                $"CharacterSkill capability '{capabilityId ?? string.Empty}' has a null formula range.");
        }
        return new NarrativeFormulaCapabilityDescriptor(
            capabilityId,
            parameterRanges.Select(value => value.ToRuntime()),
            appliedParameterIds,
            baseCost,
            triggerFrequencyCostPerUnit,
            guaranteedProcCost,
            areaCostPerExtraTarget,
            multiEffectCostPerExtraEffect,
            (pairSynergyCosts ?? throw new InvalidOperationException(
                $"CharacterSkill capability '{capabilityId}' has no pair-synergy declaration."))
                .Select(value => value == null
                    ? throw new InvalidOperationException(
                        $"CharacterSkill capability '{capabilityId}' has a null pair-synergy cost.")
                    : new NarrativeFormulaPairSynergyCost(value.otherCapabilityId, value.cost)),
            narrativeAffinity,
            affinityKeys,
            conflictGroups,
            forbiddenSynergies,
            formatterId,
            applicatorId);
    }
}

[Serializable]
public sealed class CharacterSkillTriggerFormulaCostDefinition
{
    public CharacterSkillTrigger trigger;
    [Min(0)] public int frequencyUnits;
    public bool guaranteedProc;
}

[Serializable]
public sealed class CharacterSkillTargetFormulaCostDefinition
{
    public CharacterSkillTarget target;
    [Min(1)] public int targetCount = 1;
}

[Serializable]
public sealed class CharacterSkillFormulaPolicyDefinition
{
    [Min(1)] public int formulaVersion = 1;
    [Min(0)] public int baseBudget = 3;
    [Min(0)] public int powerScale = 15;
    [Min(0.0001f)] public float softCapK = 8f;
    [Min(0f)] public float minimumImportance;
    [Min(0f)] public float maximumImportance = 4f;
    public List<float> milestoneWeights = new List<float> { 1f, 1.5f, 2f, 3f, 5f };
    public NarrativeFormulaDrawbackCreditPolicyDefinition drawbackCredit = new();
    [Tooltip("Authored deterministic cost context for every CharacterSkill trigger.")]
    public List<CharacterSkillTriggerFormulaCostDefinition> triggerCosts =
        new List<CharacterSkillTriggerFormulaCostDefinition>();
    [Tooltip("Authored deterministic target count charged for every CharacterSkill target policy.")]
    public List<CharacterSkillTargetFormulaCostDefinition> targetCosts =
        new List<CharacterSkillTargetFormulaCostDefinition>();
    [Tooltip("Canonical SHA-256 of the formula-capability catalog used by deterministic winner selection.")]
    public string catalogSha256 = string.Empty;

    public NarrativeFormulaStrengthPolicy ToRuntime()
    {
        return new NarrativeFormulaStrengthPolicy(
            formulaVersion,
            baseBudget,
            powerScale,
            softCapK,
            minimumImportance,
            maximumImportance,
            (milestoneWeights ?? new List<float>()).Select(value => (double)value));
    }

    public NarrativeFormulaDrawbackCreditPolicy RequireDrawbackPolicy() =>
        (drawbackCredit ?? throw new InvalidOperationException(
            "CharacterSkill drawback-credit policy is missing.")).ToRuntime();

    public string RequireCatalogSha256()
    {
        string canonical = catalogSha256?.Trim() ?? string.Empty;
        if (canonical.Length != 64
            || !string.Equals(canonical, catalogSha256, StringComparison.Ordinal)
            || canonical.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException(
                "CharacterSkill formula policy requires a canonical 64-character catalog SHA-256.");
        }
        return canonical.ToLowerInvariant();
    }

    public NarrativeFormulaGenerationCostContext RequireGenerationCostContext(
        CharacterSkillTrigger trigger,
        CharacterSkillTarget target)
    {
        CharacterSkillTriggerFormulaCostDefinition[] triggerMatches = (triggerCosts
                ?? throw new InvalidOperationException("CharacterSkill formula trigger-cost policy is missing."))
            .Where(value => value != null && value.trigger == trigger).ToArray();
        CharacterSkillTargetFormulaCostDefinition[] targetMatches = (targetCosts
                ?? throw new InvalidOperationException("CharacterSkill formula target-cost policy is missing."))
            .Where(value => value != null && value.target == target).ToArray();
        if (triggerMatches.Length != 1 || targetMatches.Length != 1)
            throw new InvalidOperationException(
                $"CharacterSkill formula policy requires exactly one cost context for '{trigger}/{target}'.");
        return new NarrativeFormulaGenerationCostContext(
            triggerMatches[0].frequencyUnits,
            triggerMatches[0].guaranteedProc,
            targetMatches[0].targetCount);
    }

    public void RequireCompleteGenerationContexts()
    {
        CharacterSkillTriggerFormulaCostDefinition[] triggers = (triggerCosts
                ?? throw new InvalidOperationException("CharacterSkill formula trigger-cost policy is missing."))
            .ToArray();
        CharacterSkillTargetFormulaCostDefinition[] targets = (targetCosts
                ?? throw new InvalidOperationException("CharacterSkill formula target-cost policy is missing."))
            .ToArray();
        CharacterSkillTrigger[] expectedTriggers = Enum.GetValues(typeof(CharacterSkillTrigger))
            .Cast<CharacterSkillTrigger>().ToArray();
        CharacterSkillTarget[] expectedTargets = Enum.GetValues(typeof(CharacterSkillTarget))
            .Cast<CharacterSkillTarget>().ToArray();
        if (triggers.Any(value => value == null || value.frequencyUnits < 0)
            || triggers.Select(value => value.trigger).Distinct().Count() != expectedTriggers.Length
            || expectedTriggers.Any(trigger => triggers.Count(value => value.trigger == trigger) != 1))
            throw new InvalidOperationException("CharacterSkill formula trigger-cost policy must cover every trigger exactly once.");
        if (targets.Any(value => value == null || value.targetCount < 1)
            || targets.Select(value => value.target).Distinct().Count() != expectedTargets.Length
            || expectedTargets.Any(target => targets.Count(value => value.target == target) != 1))
            throw new InvalidOperationException("CharacterSkill formula target-cost policy must cover every target exactly once.");
    }
}

public enum CharacterSkillDrawbackApplicationKind
{
    CombatCooldownTurns,
    WorkCooldownDays,
    EquippedStat
}

[Serializable]
public sealed class CharacterSkillDrawbackCapabilityDefinition
{
    public string drawbackId = string.Empty;
    public string capabilityId = string.Empty;
    public string displayName = string.Empty;
    [TextArea] public string description = string.Empty;
    public CharacterSkillDrawbackApplicationKind applicationKind;
    [Tooltip("Required only for EquippedStat. The runtime stores the resolved value, while this asset remains the semantic authority.")]
    public GameplayEffectDefinitionSO effectDefinition;
    [Min(0.001f)] public float statDeltaPerCredit = 0.03f;
    public List<CharacterSkillKind> allowedKinds = new List<CharacterSkillKind>();
    public List<CharacterSkillTrigger> allowedTriggers = new List<CharacterSkillTrigger>();
    public List<CharacterNarrativeDomain> domainAffinities = new List<CharacterNarrativeDomain>();
    [Min(1)] public int maximumCredit = 1;
    public CharacterSkillCapabilityFormulaDefinition formula;

    public string DrawbackId => drawbackId?.Trim() ?? string.Empty;
    public string CapabilityId => capabilityId?.Trim() ?? string.Empty;
    public string DisplayName => displayName?.Trim() ?? string.Empty;
    public string Description => description?.Trim() ?? string.Empty;
    public int MaximumCredit => maximumCredit;

    public bool Allows(
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger,
        IEnumerable<CharacterNarrativeFact> negativeFacts)
    {
        return AppliesTo(kind, trigger)
            && (negativeFacts ?? Array.Empty<CharacterNarrativeFact>()).Any(fact =>
                fact != null && (domainAffinities?.Contains(fact.domain) ?? false));
    }

    public bool AppliesTo(CharacterSkillKind kind, CharacterSkillTrigger trigger) =>
        (allowedKinds?.Contains(kind) ?? false)
        && (allowedTriggers?.Contains(trigger) ?? false);

    public NarrativeFormulaCapabilityDescriptor RequireFormulaDescriptor()
    {
        if (DrawbackId.Length == 0
            || CapabilityId.Length == 0
            || formula == null)
        {
            throw new InvalidOperationException(
                $"CharacterSkill drawback '{DrawbackId}' has no complete formula descriptor.");
        }
        NarrativeFormulaCapabilityDescriptor descriptor = formula.ToRuntime(CapabilityId);
        if (!string.Equals(descriptor.FormatterId, CapabilityId, StringComparison.Ordinal)
            || !string.Equals(descriptor.ApplicatorId, CapabilityId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"CharacterSkill drawback '{DrawbackId}' formatter/applicator must equal its capability ID.");
        }
        return descriptor;
    }

    public string BuildResolvedDrawbackId(int amount)
    {
        if (amount < 1 || amount > MaximumCredit)
            throw new ArgumentOutOfRangeException(nameof(amount));
        return applicationKind switch
        {
            CharacterSkillDrawbackApplicationKind.WorkCooldownDays =>
                "character-skill:work-cooldown:+" + amount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) + "d",
            CharacterSkillDrawbackApplicationKind.CombatCooldownTurns =>
                "character-skill:cooldown:+" + amount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            CharacterSkillDrawbackApplicationKind.EquippedStat =>
                DrawbackId + ":+" + amount.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException(
                $"CharacterSkill drawback '{DrawbackId}' has an unknown application kind.")
        };
    }

    public float BuildResolvedStatValue(int amount)
    {
        if (applicationKind != CharacterSkillDrawbackApplicationKind.EquippedStat
            || effectDefinition == null)
            throw new InvalidOperationException(
                $"CharacterSkill drawback '{DrawbackId}' is not a stat burden.");
        if (amount < 1 || amount > MaximumCredit)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if (!AcquiredTraitBurdenTargetCatalog.TryGet(
                effectDefinition.TargetId,
                out AcquiredTraitBurdenTargetDefinition target))
            throw new InvalidOperationException(
                $"CharacterSkill drawback '{DrawbackId}' uses an unregistered runtime stat.");
        float delta = statDeltaPerCredit * amount;
        return effectDefinition.Operation switch
        {
            GameplayEffectOperation.Multiply => target.HigherIsHarmful
                ? 1f + delta : 1f - delta,
            GameplayEffectOperation.AddFlat or GameplayEffectOperation.AddPercent =>
                target.HigherIsHarmful ? delta : -delta,
            _ => throw new InvalidOperationException(
                $"CharacterSkill drawback '{DrawbackId}' uses an unsupported stat operation.")
        };
    }

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new List<string>();
        if (DrawbackId.Length == 0
            || !string.Equals(drawbackId, DrawbackId, StringComparison.Ordinal))
            errors.Add("CharacterSkill drawback requires a stable canonical ID.");
        if (CapabilityId.Length == 0
            || !string.Equals(capabilityId, CapabilityId, StringComparison.Ordinal))
            errors.Add($"CharacterSkill drawback '{DrawbackId}' requires a canonical capability ID.");
        if (DisplayName.Length == 0 || Description.Length == 0)
            errors.Add($"CharacterSkill drawback '{DrawbackId}' requires display text.");
        if (maximumCredit is < 1 or > 2)
            errors.Add($"CharacterSkill drawback '{DrawbackId}' credit must be between one and two units.");
        if (allowedKinds == null || allowedKinds.Count == 0
            || allowedKinds.Distinct().Count() != allowedKinds.Count)
            errors.Add($"CharacterSkill drawback '{DrawbackId}' has invalid allowed kinds.");
        if (allowedTriggers == null || allowedTriggers.Count == 0
            || allowedTriggers.Distinct().Count() != allowedTriggers.Count)
            errors.Add($"CharacterSkill drawback '{DrawbackId}' has invalid allowed triggers.");
        if (domainAffinities == null || domainAffinities.Count == 0
            || domainAffinities.Distinct().Count() != domainAffinities.Count)
            errors.Add($"CharacterSkill drawback '{DrawbackId}' has invalid narrative domains.");
        if (applicationKind == CharacterSkillDrawbackApplicationKind.EquippedStat)
        {
            if (effectDefinition == null)
                errors.Add($"CharacterSkill drawback '{DrawbackId}' requires an effect definition.");
            else
            {
                if ((effectDefinition.AllowedSources & GameplayEffectSourceKind.Status) == 0)
                    errors.Add($"CharacterSkill drawback '{DrawbackId}' effect does not allow Status sources.");
                if (!AcquiredTraitBurdenTargetCatalog.IsHarmful(
                        effectDefinition,
                        BuildResolvedStatValue(MaximumCredit),
                        out string reason))
                    errors.Add($"CharacterSkill drawback '{DrawbackId}' is not harmful: {reason}.");
            }
            if (float.IsNaN(statDeltaPerCredit)
                || float.IsInfinity(statDeltaPerCredit)
                || statDeltaPerCredit <= 0f
                || statDeltaPerCredit * maximumCredit >= 0.5f)
                errors.Add($"CharacterSkill drawback '{DrawbackId}' has an invalid stat delta.");
        }
        else if (effectDefinition != null)
            errors.Add($"Cooldown drawback '{DrawbackId}' cannot carry a stat effect definition.");
        try
        {
            NarrativeFormulaCapabilityDescriptor descriptor = RequireFormulaDescriptor();
            NarrativeFormulaQuantizedRange magnitude = descriptor.RequireRange(
                NarrativeFormulaParameterIds.Magnitude);
            if (magnitude.MinimumUnits != 1
                || magnitude.MaximumUnits != maximumCredit
                || magnitude.QuantumUnits != 1
                || descriptor.BaseCost != 1)
            {
                errors.Add(
                    $"CharacterSkill drawback '{DrawbackId}' must allocate one-to-{maximumCredit} burden units at one credit per unit.");
            }
        }
        catch (Exception exception)
        {
            errors.Add(exception.Message);
        }
        return errors;
    }
}

[Serializable]
public abstract class CharacterSkillModuleRule
{
    public string id = string.Empty;
    [Tooltip("Registered C# behavior reused by this authored module. This ID is required and is not part of combination identity.")]
    public string capabilityId = string.Empty;
    public string displayName = string.Empty;
    public List<CharacterSkillKind> allowedKinds = new List<CharacterSkillKind>();
    public List<CharacterSkillTrigger> allowedTriggers = new List<CharacterSkillTrigger>();
    public List<CharacterSkillTarget> allowedTargets = new List<CharacterSkillTarget>();
    public List<CharacterSkillNumericVariant> variants = new List<CharacterSkillNumericVariant>();
    [Tooltip("Required immutable formula metadata for formulaVersion > 0 generation. Legacy variants are read-only version-0 support.")]
    public CharacterSkillCapabilityFormulaDefinition formula;

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
    [Min(1)] public int initialStatTotal = 60;
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

    [Header("Narrative Formula")]
    public CharacterSkillFormulaPolicyDefinition formulaPolicy =
        new CharacterSkillFormulaPolicyDefinition();

    [SerializeField]
    private List<CharacterSkillDrawbackCapabilityDefinition> drawbackCapabilities =
        new List<CharacterSkillDrawbackCapabilityDefinition>();

    [SerializeReference]
    private List<CharacterSkillModuleRule> modules = new List<CharacterSkillModuleRule>();

    public IReadOnlyList<CharacterSkillModuleRule> Modules => modules;
    public IReadOnlyList<CharacterSkillDrawbackCapabilityDefinition> DrawbackCapabilities =>
        drawbackCapabilities ??= new List<CharacterSkillDrawbackCapabilityDefinition>();

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

    public NarrativeFormulaStrengthPolicy RequireFormulaPolicy()
    {
        if (formulaPolicy == null)
        {
            throw new InvalidOperationException("CharacterSkill formula policy is missing.");
        }
        formulaPolicy.RequireCompleteGenerationContexts();
        formulaPolicy.RequireCatalogSha256();
        if (formulaPolicy.formulaVersion >=
            CharacterSkillFormulaGeneration.DrawbackModuleSelectionFormulaVersion)
            RequireDrawbackCatalog();
        return formulaPolicy.ToRuntime();
    }

    public CharacterSkillDrawbackCapabilityDefinition FindDrawback(string drawbackId)
    {
        return DrawbackCapabilities.FirstOrDefault(value => value != null
            && string.Equals(value.DrawbackId, drawbackId, StringComparison.Ordinal));
    }

    public IReadOnlyList<CharacterSkillDrawbackCapabilityDefinition> RequireDrawbackCatalog()
    {
        CharacterSkillDrawbackCapabilityDefinition[] authored = DrawbackCapabilities
            .Where(value => value != null)
            .OrderBy(value => value.DrawbackId, StringComparer.Ordinal)
            .ToArray();
        int minimumCount = formulaPolicy?.formulaVersion >=
            CharacterSkillFormulaGeneration.RuntimeStatDrawbackFormulaVersion ? 8 : 2;
        if (authored.Length < minimumCount
            || authored.Select(value => value.DrawbackId)
                .Distinct(StringComparer.Ordinal).Count() != authored.Length)
            throw new InvalidOperationException(
                $"CharacterSkill formula requires at least {minimumCount} distinct drawback capabilities.");
        string[] errors = authored.SelectMany(value => value.ValidateDefinition()).ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException(string.Join(" | ", errors));
        return authored;
    }

    [GameplayInternalOnly(
        "The controlled editor catalog builder writes immutable CharacterSkill drawback authoring.",
        "CharacterSkillFormulaCatalogAssetBuilder")]
    public void ApplyDrawbackAuthoring(
        IEnumerable<CharacterSkillDrawbackCapabilityDefinition> values)
    {
        drawbackCapabilities = (values ?? throw new ArgumentNullException(nameof(values)))
            .Where(value => value != null)
            .OrderBy(value => value.DrawbackId, StringComparer.Ordinal)
            .ToList();
    }

    public NarrativeFormulaCapabilityDescriptor RequireFormulaDescriptor(
        CharacterSkillModuleRule module)
    {
        CharacterSkillModuleCapabilityDescriptor capability =
            CharacterSkillModuleCapabilityRegistry.Require(module);
        if (module.formula == null)
        {
            throw new InvalidOperationException(
                $"CharacterSkill module '{module.id ?? string.Empty}' has no formula metadata.");
        }
        NarrativeFormulaCapabilityDescriptor descriptor =
            module.formula.ToRuntime(capability.CapabilityId);
        if (!string.Equals(descriptor.FormatterId, capability.CapabilityId, StringComparison.Ordinal)
            || !string.Equals(descriptor.ApplicatorId, capability.CapabilityId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"CharacterSkill module '{module.id ?? string.Empty}' formula formatter/applicator must use its registered capability ID.");
        }
        return descriptor;
    }

    public IReadOnlyList<NarrativeFormulaCapabilityDescriptor> RequireFormulaCatalog()
    {
        RequireFormulaPolicy();
        CharacterSkillModuleRule[] authored = (modules ?? new List<CharacterSkillModuleRule>())
            .Where(value => value != null).ToArray();
        if (authored.Length == 0
            || authored.Select(value => value.id).Any(string.IsNullOrWhiteSpace)
            || authored.Select(value => value.id).Distinct(StringComparer.Ordinal).Count()
                != authored.Length)
            throw new InvalidOperationException(
                "CharacterSkill formula catalog requires distinct authored module IDs.");
        return authored.Select(RequireFormulaDescriptor).ToArray();
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
            managementModule.allowedKinds ??= new List<CharacterSkillKind>();
            if (!managementModule.allowedKinds.Contains(CharacterSkillKind.Active))
            {
                managementModule.allowedKinds.Add(CharacterSkillKind.Active);
            }
            managementModule.allowedTriggers ??= new List<CharacterSkillTrigger>();
            if (!managementModule.allowedTriggers.Contains(CharacterSkillTrigger.WorkStarted))
            {
                managementModule.allowedTriggers.Add(CharacterSkillTrigger.WorkStarted);
            }
            if (!managementModule.allowedTriggers.Contains(CharacterSkillTrigger.ManualWork))
            {
                managementModule.allowedTriggers.Add(CharacterSkillTrigger.ManualWork);
            }
        }
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
            CharacterSkillTrigger.ManualWork,
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
            capabilityId = id,
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
            capabilityId = id,
            displayName = name,
            allowedKinds = new List<CharacterSkillKind>
            {
                CharacterSkillKind.Active,
                CharacterSkillKind.Passive,
                CharacterSkillKind.Ultimate
            },
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
    private readonly IGameContentCatalog contentCatalog;

    public ResourceCharacterSkillSystemSettingsProvider(IGameContentCatalog contentCatalog)
    {
        this.contentCatalog = contentCatalog
            ?? throw new ArgumentNullException(nameof(contentCatalog));
    }

    public CharacterSkillSystemSettingsSO Settings =>
        contentCatalog.CharacterSkillSettings
        ?? throw new InvalidOperationException(
            "Game content catalog has no character-skill settings.");
}
