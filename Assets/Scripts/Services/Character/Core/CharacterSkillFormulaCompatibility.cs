using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Immutable CharacterSkill formula authority selected by the version/hash pair
/// frozen into a generated skill. Historical authorities deliberately do not
/// read numeric definitions from the current settings asset.
/// </summary>
internal sealed class CharacterSkillFormulaAuthority
{
    private readonly IReadOnlyDictionary<string, NarrativeFormulaCapabilityDescriptor> descriptors;
    private readonly Func<CharacterSkillTrigger, CharacterSkillTarget, NarrativeFormulaGenerationCostContext>
        generationContext;

    public CharacterSkillFormulaAuthority(
        int formulaVersion,
        string catalogSha256,
        IEnumerable<NarrativeFormulaCapabilityDescriptor> descriptors,
        Func<CharacterSkillTrigger, CharacterSkillTarget, NarrativeFormulaGenerationCostContext>
            generationContext,
        NarrativeFormulaDrawbackCreditPolicy drawbackPolicy,
        bool usesCurrentStatDrawbacks,
        bool supportsArchivedStatDrawbacks)
    {
        FormulaVersion = formulaVersion;
        CatalogSha256 = catalogSha256 ?? throw new ArgumentNullException(nameof(catalogSha256));
        this.descriptors = (descriptors ?? throw new ArgumentNullException(nameof(descriptors)))
            .ToDictionary(value => value.CapabilityId, StringComparer.Ordinal);
        this.generationContext = generationContext
            ?? throw new ArgumentNullException(nameof(generationContext));
        DrawbackPolicy = drawbackPolicy ?? throw new ArgumentNullException(nameof(drawbackPolicy));
        UsesCurrentStatDrawbacks = usesCurrentStatDrawbacks;
        SupportsArchivedStatDrawbacks = supportsArchivedStatDrawbacks;
    }

    public int FormulaVersion { get; }
    public string CatalogSha256 { get; }
    public NarrativeFormulaDrawbackCreditPolicy DrawbackPolicy { get; }
    public bool UsesCurrentStatDrawbacks { get; }
    public bool SupportsArchivedStatDrawbacks { get; }

    public NarrativeFormulaCapabilityDescriptor RequireDescriptor(string capabilityId) =>
        descriptors.TryGetValue(capabilityId ?? string.Empty, out NarrativeFormulaCapabilityDescriptor value)
            ? value
            : throw new InvalidOperationException(
                $"Formula authority v{FormulaVersion}/{CatalogSha256} has no capability '{capabilityId ?? string.Empty}'.");

    public NarrativeFormulaGenerationCostContext RequireGenerationCostContext(
        CharacterSkillTrigger trigger,
        CharacterSkillTarget target) => generationContext(trigger, target);
}

/// <summary>
/// Explicit compatibility registry for committed CharacterSkill formula
/// catalogs. Adding a historical identity requires copying its exported
/// immutable numeric catalog here; unknown or hashless identities fail closed.
/// </summary>
internal static class CharacterSkillFormulaCompatibility
{
    // Archive authorities:
    // - v2 FormulaModuleSelectionPilot-20260916-r8/csharp_resolution
    // - v3 FormulaModuleSelectionPhase70-20260917-r3/unity_catalog
    // - v4 FormulaModuleSelectionPhase73-20260917-r1/unity_catalog
    // - v5 FormulaModuleSelectionPhase74-20260917-r2/unity_catalog
    // No complete exported v1 catalog/hash survives in the committed evidence;
    // version 1 therefore remains deliberately unavailable and fail-closed.
    internal const string Version2CatalogSha256 =
        "5d165ab9872c014af6b3d32a0154a214f113c5c819714029593e82fb1a9efe83";
    internal const string Version3CatalogSha256 =
        "fe24f7022b216b457cef56fe1c7bad2b4015c6b6785d46d40252e8c662ee3b60";
    internal const string Version4CatalogSha256 =
        "3b80dc59ad54d3f6952e293889c8e77262d7fa64200d7e92f966ca0f7b40d4e4";
    internal const string Version5CatalogSha256 =
        "adb5e6fea3a46e2e33e1e03bb89120a923811d7099f3b9c5b6fea99397b76333";

    private static readonly NarrativeFormulaDrawbackCreditPolicy ArchivedDrawbackPolicy =
        new NarrativeFormulaDrawbackCreditPolicy(
            playerChoiceMaximumBudgetFraction: 0.25d,
            automaticMaximumBudgetFraction: 0.10d,
            absoluteMaximumCredit: 3,
            requireNegativeEvidenceForAutomatic: true);

    private static readonly IReadOnlyDictionary<string, CharacterSkillFormulaAuthority> Historical =
        BuildHistoricalAuthorities();

    public static CharacterSkillFormulaAuthority RequireAuthority(
        CharacterSkillInstance skill,
        CharacterSkillSystemSettingsSO settings)
    {
        if (skill == null) throw new ArgumentNullException(nameof(skill));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        string key = IdentityKey(skill.formulaVersion, skill.formulaCatalogSha256);
        if (Historical.TryGetValue(key, out CharacterSkillFormulaAuthority historical))
            return historical;

        if (skill.formulaVersion <= CharacterSkillFormulaGeneration.MathematicalScalingFormulaVersion)
        {
            throw new InvalidOperationException(
                $"Formula skill '{skill.id}' uses an unknown, unavailable, or tampered historical formula authority "
                + $"v{skill.formulaVersion}/'{skill.formulaCatalogSha256 ?? string.Empty}'.");
        }

        NarrativeFormulaStrengthPolicy policy = settings.RequireFormulaPolicy();
        string currentHash = settings.formulaPolicy.RequireCatalogSha256();
        if (skill.formulaVersion != policy.FormulaVersion
            || !string.Equals(skill.formulaCatalogSha256, currentHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Formula skill '{skill.id}' uses an unknown or tampered formula authority "
                + $"v{skill.formulaVersion}/'{skill.formulaCatalogSha256 ?? string.Empty}'.");
        }

        return new CharacterSkillFormulaAuthority(
            policy.FormulaVersion,
            currentHash,
            settings.Modules.Where(value => value != null)
                .Select(settings.RequireFormulaDescriptor),
            settings.formulaPolicy.RequireGenerationCostContext,
            settings.formulaPolicy.RequireDrawbackPolicy(),
            usesCurrentStatDrawbacks: true,
            supportsArchivedStatDrawbacks: false);
    }

    public static bool TryValidateArchivedStatDrawback(
        CharacterSkillFormulaAuthority authority,
        CharacterSkillInstance skill,
        out string error)
    {
        error = string.Empty;
        if (authority == null || skill?.drawbackEffect == null
            || !authority.SupportsArchivedStatDrawbacks)
        {
            error = "the selected formula authority does not support archived stat drawbacks";
            return false;
        }

        CharacterSkillDrawbackEffectEnvelope effect = skill.drawbackEffect;
        if (!ArchivedStatDrawbacks.TryGetValue(
                effect.drawbackModuleId ?? string.Empty,
                out ArchivedStatDrawback definition))
        {
            error = "the archived stat drawback ID is unknown";
            return false;
        }

        int severity = effect.severityUnits;
        float expectedValue = definition.HigherIsHarmful
            ? 1f + 0.03f * severity
            : 1f - 0.03f * severity;
        string expectedDrawbackId = definition.DrawbackId + ":+" + severity;
        if (severity is < 1 or > 2
            || !string.Equals(skill.drawbackId, expectedDrawbackId, StringComparison.Ordinal)
            || !string.Equals(effect.effectId, definition.EffectId, StringComparison.Ordinal)
            || !string.Equals(effect.targetId, definition.TargetId, StringComparison.Ordinal)
            || effect.operation != GameplayEffectOperation.Multiply
            || float.IsNaN(effect.value)
            || float.IsInfinity(effect.value)
            || Math.Abs(effect.value - expectedValue) > 0.000001f
            || !string.Equals(effect.displayName, definition.DisplayName, StringComparison.Ordinal))
        {
            error = "the archived stat drawback envelope does not match its frozen definition";
            return false;
        }
        return true;
    }

    private static IReadOnlyDictionary<string, CharacterSkillFormulaAuthority>
        BuildHistoricalAuthorities()
    {
        NarrativeFormulaCapabilityDescriptor[] legacy = BuildDescriptors(expandedRanges: false);
        NarrativeFormulaCapabilityDescriptor[] expanded = BuildDescriptors(expandedRanges: true);
        CharacterSkillFormulaAuthority[] values =
        {
            HistoricalAuthority(2, Version2CatalogSha256, legacy, supportsStatDrawbacks: false),
            HistoricalAuthority(3, Version3CatalogSha256, legacy, supportsStatDrawbacks: false),
            HistoricalAuthority(4, Version4CatalogSha256, legacy, supportsStatDrawbacks: true),
            HistoricalAuthority(5, Version5CatalogSha256, expanded, supportsStatDrawbacks: true)
        };
        return values.ToDictionary(
            value => IdentityKey(value.FormulaVersion, value.CatalogSha256),
            StringComparer.Ordinal);
    }

    private static CharacterSkillFormulaAuthority HistoricalAuthority(
        int version,
        string hash,
        IReadOnlyList<NarrativeFormulaCapabilityDescriptor> descriptors,
        bool supportsStatDrawbacks) =>
        new CharacterSkillFormulaAuthority(
            version,
            hash,
            descriptors,
            ArchivedGenerationContext,
            ArchivedDrawbackPolicy,
            usesCurrentStatDrawbacks: false,
            supportsArchivedStatDrawbacks: supportsStatDrawbacks);

    private static string IdentityKey(int version, string hash) =>
        version + "\n" + (hash ?? string.Empty);

    private static NarrativeFormulaGenerationCostContext ArchivedGenerationContext(
        CharacterSkillTrigger trigger,
        CharacterSkillTarget target)
    {
        int triggerUnits = trigger switch
        {
            CharacterSkillTrigger.DamageTaken or CharacterSkillTrigger.NeedChanged
                or CharacterSkillTrigger.MoodChanged
                or CharacterSkillTrigger.RelationshipChanged => 3,
            CharacterSkillTrigger.EnemyDefeated or CharacterSkillTrigger.WorkStarted
                or CharacterSkillTrigger.WorkCompleted => 2,
            _ => 1
        };
        int targetCount = target is CharacterSkillTarget.AllAllies
            or CharacterSkillTarget.AllEnemies ? 3 : 1;
        return new NarrativeFormulaGenerationCostContext(
            triggerUnits, guaranteedProc: true, targetCount: targetCount);
    }

    private static NarrativeFormulaCapabilityDescriptor[] BuildDescriptors(bool expandedRanges)
    {
        string[] capabilityIds =
        {
            "damage", "heal", "guard", "dot", "vulnerability", "delay",
            "buff", "debuff", "cleanse", "protect", "reposition", "multi_target",
            "conditional_amplify", "cooldown_adjust", "work_speed", "output",
            "cleaning", "repair", "stock", "research", "needs", "mood",
            "relationship", "revenue"
        };
        return capabilityIds.Select(value => BuildDescriptor(value, expandedRanges)).ToArray();
    }

    private static NarrativeFormulaCapabilityDescriptor BuildDescriptor(
        string capabilityId,
        bool expandedRanges)
    {
        IReadOnlyList<string> applied = capabilityId switch
        {
            "damage" => new[] { NarrativeFormulaParameterIds.Magnitude, NarrativeFormulaParameterIds.Count },
            "guard" or "dot" or "vulnerability" or "buff" or "debuff" or "protect"
                or "mood" => new[] { NarrativeFormulaParameterIds.Magnitude, NarrativeFormulaParameterIds.Duration },
            "cleanse" => new[] { NarrativeFormulaParameterIds.Count },
            "multi_target" => new[] { NarrativeFormulaParameterIds.TargetCount },
            _ => new[] { NarrativeFormulaParameterIds.Magnitude }
        };
        int baseCost = capabilityId switch
        {
            "dot" or "vulnerability" or "cleanse" or "reposition" or "multi_target"
                or "stock" or "revenue" => 1,
            "buff" or "debuff" or "protect" or "conditional_amplify"
                or "cooldown_adjust" => 2,
            _ => 0
        };
        string paired = capabilityId switch
        {
            "damage" => "conditional_amplify",
            "conditional_amplify" => "damage",
            "guard" => "protect",
            "protect" => "guard",
            "work_speed" => "output",
            "output" => "work_speed",
            _ => string.Empty
        };
        NarrativeFormulaPairSynergyCost[] pairCosts = paired.Length == 0
            ? Array.Empty<NarrativeFormulaPairSynergyCost>()
            : new[] { new NarrativeFormulaPairSynergyCost(paired, 1) };
        bool management = capabilityId is "work_speed" or "output" or "cleaning"
            or "repair" or "stock" or "research" or "needs" or "mood"
            or "relationship" or "revenue";
        return new NarrativeFormulaCapabilityDescriptor(
            capabilityId,
            expandedRanges ? ExpandedRanges(capabilityId) : LegacyRanges(capabilityId),
            applied,
            baseCost,
            triggerFrequencyCostPerUnit: 1,
            guaranteedProcCost: 1,
            areaCostPerExtraTarget: 1,
            multiEffectCostPerExtraEffect: 1,
            pairSynergyCosts: pairCosts,
            narrativeAffinity: 1d,
            affinityKeys: new[]
            {
                management
                    ? CharacterNarrativeDomain.Work.ToString()
                    : CharacterNarrativeDomain.Combat.ToString()
            },
            conflictGroups: new[] { "capability:" + capabilityId },
            forbiddenSynergies: Array.Empty<string>(),
            formatterId: capabilityId,
            applicatorId: capabilityId);
    }

    private static NarrativeFormulaQuantizedRange[] LegacyRanges(string capabilityId)
    {
        NarrativeFormulaQuantizedRange magnitude = Fixed(NarrativeFormulaParameterIds.Magnitude, 0, 3);
        NarrativeFormulaQuantizedRange duration = Fixed(NarrativeFormulaParameterIds.Duration, 0);
        NarrativeFormulaQuantizedRange count = Fixed(NarrativeFormulaParameterIds.Count, 1);
        NarrativeFormulaQuantizedRange targetCount = Fixed(NarrativeFormulaParameterIds.TargetCount, 1);
        switch (capabilityId)
        {
            case "damage": magnitude = Range("magnitude", 750, 1600, 50, 3, 1); break;
            case "heal": magnitude = Range("magnitude", 4000, 18000, 2000, 3, 1); break;
            case "guard": magnitude = Range("magnitude", 250, 450, 200, 3, 1); duration = Range("duration", 1, 2, 1, 0, 1); break;
            case "dot": magnitude = Range("magnitude", 4000, 7000, 3000, 3, 1); duration = Range("duration", 2, 3, 1, 0, 1); break;
            case "vulnerability": magnitude = Range("magnitude", 200, 400, 200, 3, 1); duration = Range("duration", 1, 2, 1, 0, 1); break;
            case "delay": magnitude = Range("magnitude", 2000, 5000, 3000, 3, 1); break;
            case "buff":
            case "debuff": magnitude = Range("magnitude", 200, 250, 50, 3, 1); duration = Fixed("duration", 2); break;
            case "cleanse": count = Range("count", 1, 8, 7, 0, 1); break;
            case "protect": magnitude = Range("magnitude", 300, 350, 50, 3, 1); duration = Range("duration", 1, 3, 2, 0, 1); break;
            case "reposition": magnitude = Range("magnitude", 1000, 2000, 1000, 3, 1); break;
            case "multi_target": targetCount = Range("targetCount", 2, 8, 6, 0, 1); break;
            case "conditional_amplify": magnitude = Range("magnitude", 350, 700, 350, 3, 1); break;
            case "cooldown_adjust": magnitude = Range("magnitude", 1000, 99000, 98000, 3, 1); break;
            case "work_speed": magnitude = Range("magnitude", 100, 250, 150, 3, 1); break;
            case "output": magnitude = Range("magnitude", 100, 300, 200, 3, 1); break;
            case "cleaning": magnitude = Range("magnitude", 5000, 15000, 10000, 3, 1); break;
            case "repair": magnitude = Range("magnitude", 8000, 25000, 17000, 3, 1); break;
            case "stock": magnitude = Range("magnitude", 1000, 3000, 2000, 3, 1); break;
            case "research": magnitude = Range("magnitude", 5000, 18000, 13000, 3, 1); break;
            case "needs": magnitude = Range("magnitude", 5000, 15000, 10000, 3, 1); break;
            case "mood": magnitude = Range("magnitude", 3000, 8000, 5000, 3, 1); duration = Fixed("duration", 180); break;
            case "relationship": magnitude = Range("magnitude", 2000, 7000, 5000, 3, 1); break;
            case "revenue": magnitude = Range("magnitude", 50, 200, 150, 3, 1); break;
            default: throw new InvalidOperationException($"Unknown archived capability '{capabilityId}'.");
        }
        return new[] { magnitude, duration, count, targetCount };
    }

    private static NarrativeFormulaQuantizedRange[] ExpandedRanges(string capabilityId)
    {
        NarrativeFormulaQuantizedRange magnitude = Fixed(NarrativeFormulaParameterIds.Magnitude, 0, 3);
        NarrativeFormulaQuantizedRange duration = Fixed(NarrativeFormulaParameterIds.Duration, 0);
        NarrativeFormulaQuantizedRange count = Fixed(NarrativeFormulaParameterIds.Count, 1);
        NarrativeFormulaQuantizedRange targetCount = Fixed(NarrativeFormulaParameterIds.TargetCount, 1);
        switch (capabilityId)
        {
            case "damage": magnitude = Range("magnitude", 500, 3000, 250, 3, 1); count = Range("count", 1, 4, 1, 0, 2); break;
            case "heal": magnitude = Range("magnitude", 5000, 45000, 5000, 3, 2); break;
            case "guard": magnitude = Range("magnitude", 100, 600, 50, 3, 1); duration = Range("duration", 1, 5, 1, 0, 2); break;
            case "dot": magnitude = Range("magnitude", 2000, 12000, 1000, 3, 1); duration = Range("duration", 1, 6, 1, 0, 2); break;
            case "vulnerability": magnitude = Range("magnitude", 100, 550, 50, 3, 1); duration = Range("duration", 1, 5, 1, 0, 2); break;
            case "delay": magnitude = Range("magnitude", 1000, 6000, 1000, 3, 3); break;
            case "buff":
            case "debuff": magnitude = Range("magnitude", 50, 500, 50, 3, 1); duration = Range("duration", 1, 5, 1, 0, 2); break;
            case "cleanse": count = Range("count", 1, 6, 1, 0, 3); break;
            case "protect": magnitude = Range("magnitude", 100, 600, 50, 3, 1); duration = Range("duration", 1, 5, 1, 0, 2); break;
            case "reposition": magnitude = Range("magnitude", 1000, 6000, 1000, 3, 3); break;
            case "multi_target": targetCount = Range("targetCount", 2, 8, 1, 0, 3); break;
            case "conditional_amplify": magnitude = Range("magnitude", 100, 1000, 100, 3, 2); break;
            case "cooldown_adjust": magnitude = Range("magnitude", 1000, 6000, 1000, 3, 3); break;
            case "work_speed": magnitude = Range("magnitude", 50, 800, 50, 3, 1); break;
            case "output": magnitude = Range("magnitude", 50, 800, 50, 3, 1); break;
            case "cleaning": magnitude = Range("magnitude", 2000, 32000, 2000, 3, 1); break;
            case "repair": magnitude = Range("magnitude", 5000, 80000, 5000, 3, 1); break;
            case "stock": magnitude = Range("magnitude", 1000, 9000, 1000, 3, 2); break;
            case "research": magnitude = Range("magnitude", 2000, 40000, 2000, 3, 1); break;
            case "needs": magnitude = Range("magnitude", 2000, 32000, 2000, 3, 1); break;
            case "mood": magnitude = Range("magnitude", 2000, 20000, 2000, 3, 1); duration = Range("duration", 60, 420, 60, 0, 1); break;
            case "relationship": magnitude = Range("magnitude", 1000, 20000, 1000, 3, 1); break;
            case "revenue": magnitude = Range("magnitude", 10, 300, 10, 3, 1); break;
            default: throw new InvalidOperationException($"Unknown archived capability '{capabilityId}'.");
        }
        return new[] { magnitude, duration, count, targetCount };
    }

    private static NarrativeFormulaQuantizedRange Range(
        string parameterId,
        long minimumUnits,
        long maximumUnits,
        long quantumUnits,
        int decimalPlaces,
        int costPerQuantum) =>
        new NarrativeFormulaQuantizedRange(
            parameterId, minimumUnits, maximumUnits, quantumUnits,
            decimalPlaces, costPerQuantum);

    private static NarrativeFormulaQuantizedRange Fixed(
        string parameterId,
        long units,
        int decimalPlaces = 0) =>
        Range(parameterId, units, units, 1, decimalPlaces, 1);

    private sealed class ArchivedStatDrawback
    {
        public ArchivedStatDrawback(
            string drawbackId,
            string displayName,
            string effectId,
            string targetId,
            bool higherIsHarmful)
        {
            DrawbackId = drawbackId;
            DisplayName = displayName;
            EffectId = effectId;
            TargetId = targetId;
            HigherIsHarmful = higherIsHarmful;
        }

        public string DrawbackId { get; }
        public string DisplayName { get; }
        public string EffectId { get; }
        public string TargetId { get; }
        public bool HigherIsHarmful { get; }
    }

    private static readonly IReadOnlyDictionary<string, ArchivedStatDrawback> ArchivedStatDrawbacks =
        new[]
        {
            StatDrawback("work-speed", "작업 리듬 붕괴", higherIsHarmful: false),
            StatDrawback("research-speed", "집중력 소모", higherIsHarmful: false),
            StatDrawback("combat-power", "전투 후유증", higherIsHarmful: false),
            StatDrawback("move-speed", "굳은 발걸음", higherIsHarmful: false),
            StatDrawback("fatigue-rate", "빠른 피로 누적", higherIsHarmful: true),
            StatDrawback("accident-chance", "불안정한 작업 습관", higherIsHarmful: true)
        }.ToDictionary(value => value.DrawbackId, StringComparer.Ordinal);

    private static ArchivedStatDrawback StatDrawback(
        string suffix,
        string displayName,
        bool higherIsHarmful) =>
        new ArchivedStatDrawback(
            "character-skill:drawback:" + suffix,
            displayName,
            "effect:character:" + suffix + ":multiply",
            "character:" + suffix,
            higherIsHarmful);
}
