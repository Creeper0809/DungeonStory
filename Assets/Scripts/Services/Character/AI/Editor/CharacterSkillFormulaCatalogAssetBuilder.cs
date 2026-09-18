using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CharacterSkillFormulaCatalogAssetBuilder
{
    [Flags]
    private enum AppliedAxes
    {
        Magnitude = 1,
        Duration = 2,
        Count = 4,
        TargetCount = 8
    }

    private static readonly IReadOnlyDictionary<string, AppliedAxes> AppliedAxesByCapability =
        new Dictionary<string, AppliedAxes>(StringComparer.Ordinal)
        {
            ["damage"] = AppliedAxes.Magnitude | AppliedAxes.Count,
            ["heal"] = AppliedAxes.Magnitude,
            ["guard"] = AppliedAxes.Magnitude | AppliedAxes.Duration,
            ["dot"] = AppliedAxes.Magnitude | AppliedAxes.Duration,
            ["vulnerability"] = AppliedAxes.Magnitude | AppliedAxes.Duration,
            ["delay"] = AppliedAxes.Magnitude,
            ["buff"] = AppliedAxes.Magnitude | AppliedAxes.Duration,
            ["debuff"] = AppliedAxes.Magnitude | AppliedAxes.Duration,
            ["cleanse"] = AppliedAxes.Count,
            ["protect"] = AppliedAxes.Magnitude | AppliedAxes.Duration,
            ["reposition"] = AppliedAxes.Magnitude,
            ["multi_target"] = AppliedAxes.TargetCount,
            ["conditional_amplify"] = AppliedAxes.Magnitude,
            ["cooldown_adjust"] = AppliedAxes.Magnitude,
            ["work_speed"] = AppliedAxes.Magnitude,
            ["output"] = AppliedAxes.Magnitude,
            ["cleaning"] = AppliedAxes.Magnitude,
            ["repair"] = AppliedAxes.Magnitude,
            ["stock"] = AppliedAxes.Magnitude,
            ["research"] = AppliedAxes.Magnitude,
            ["needs"] = AppliedAxes.Magnitude,
            ["mood"] = AppliedAxes.Magnitude | AppliedAxes.Duration,
            ["relationship"] = AppliedAxes.Magnitude,
            ["revenue"] = AppliedAxes.Magnitude
        };

    public const string SettingsAssetPath =
        "Assets/Resources/SO/Character/CharacterSkillSystemSettings.asset";
    public const int ExpectedModuleCount = 24;
    public const int ExpectedManagementModuleCount = 10;

    [MenuItem("DungeonStory/Narrative Formula/Build CharacterSkill Catalog")]
    public static void Build()
    {
        CharacterSkillSystemSettingsSO settings = RequireSettings();
        Undo.RecordObject(settings, "Build CharacterSkill formula catalog");
        Populate(settings);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"CharacterSkill formula catalog authored and validated: {settings.formulaPolicy.catalogSha256}");
    }

    public static void Populate(CharacterSkillSystemSettingsSO settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        settings.EnsureDefaults();
        settings.formulaPolicy ??= new CharacterSkillFormulaPolicyDefinition();
        settings.formulaPolicy.formulaVersion =
            CharacterSkillFormulaGeneration.EffectiveBoundsFormulaVersion;
        settings.formulaPolicy.triggerCosts = BuildTriggerCosts();
        settings.formulaPolicy.targetCosts = BuildTargetCosts();
        settings.ApplyDrawbackAuthoring(BuildDrawbacks());
        foreach (CharacterSkillModuleRule module in RequireCurrentModules(settings))
        {
            if (module is CharacterManagementSkillModuleRule)
            {
                module.allowedTriggers ??= new List<CharacterSkillTrigger>();
                if (!module.allowedTriggers.Contains(
                        CharacterSkillTrigger.OperatingDayStarted))
                {
                    module.allowedTriggers.Add(
                        CharacterSkillTrigger.OperatingDayStarted);
                }
            }
            module.formula = BuildMathematicalFormula(module);
        }
        settings.formulaPolicy.catalogSha256 = ComputeCanonicalCatalogSha256(settings);
        Validate(settings);
    }

    [MenuItem("DungeonStory/Narrative Formula/Validate CharacterSkill Catalog")]
    public static void ValidateMenu()
    {
        CharacterSkillSystemSettingsSO settings = RequireSettings();
        Validate(settings);
        Debug.Log($"CharacterSkill formula catalog is valid: {settings.formulaPolicy.catalogSha256}");
    }

    public static void Validate(CharacterSkillSystemSettingsSO settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        CharacterSkillModuleRule[] modules = RequireCurrentModules(settings);
        if (modules.OfType<CharacterManagementSkillModuleRule>().Count()
            != ExpectedManagementModuleCount)
        {
            throw new InvalidOperationException(
                $"CharacterSkill formula catalog requires exactly {ExpectedManagementModuleCount} current management modules.");
        }
        if (settings.formulaPolicy == null)
            throw new InvalidOperationException("CharacterSkill formula policy is missing.");
        RequireCompleteEnumMap(
            settings.formulaPolicy.triggerCosts,
            value => value.trigger,
            Enum.GetValues(typeof(CharacterSkillTrigger)).Cast<CharacterSkillTrigger>(),
            "trigger cost");
        RequireCompleteEnumMap(
            settings.formulaPolicy.targetCosts,
            value => value.target,
            Enum.GetValues(typeof(CharacterSkillTarget)).Cast<CharacterSkillTarget>(),
            "target cost");
        foreach (CharacterSkillTriggerFormulaCostDefinition value in settings.formulaPolicy.triggerCosts)
        {
            if (value.frequencyUnits < 0)
                throw new InvalidOperationException($"Trigger '{value.trigger}' has a negative frequency cost unit.");
        }
        foreach (CharacterSkillTargetFormulaCostDefinition value in settings.formulaPolicy.targetCosts)
        {
            if (value.targetCount < 1)
                throw new InvalidOperationException($"Target '{value.target}' has an invalid target count.");
        }
        settings.RequireDrawbackCatalog();
        foreach (CharacterSkillModuleRule module in modules)
        {
            if (string.IsNullOrWhiteSpace(module.displayName)
                || !string.Equals(module.displayName.Trim(), module.displayName, StringComparison.Ordinal))
                throw new InvalidOperationException($"Module '{module.id}' has no canonical display name.");
            RequireDistinctDefined(module.allowedKinds, "kind", module.id);
            RequireDistinctDefined(module.allowedTriggers, "trigger", module.id);
            RequireDistinctDefined(module.allowedTargets, "target", module.id);
            if (module is CharacterManagementSkillModuleRule)
            {
                if (!module.Allows(
                        CharacterSkillKind.Ultimate,
                        CharacterSkillTrigger.OperatingDayStarted,
                        CharacterSkillTarget.Self)
                    || !CharacterSkillValidation.IsTargetCompatible(
                        module,
                        CharacterSkillTarget.Self)
                    || !CharacterSkillFormulaRuntimeContextPolicy
                        .ConsumesAllAppliedAxes(
                            CharacterSkillKind.Ultimate,
                            CharacterSkillTrigger.OperatingDayStarted,
                            CharacterUltimateDomain.Management,
                            module))
                {
                    throw new InvalidOperationException(
                        $"Management module '{module.id}' must be reachable by the current management ultimate context.");
                }

                bool passivePolicyAllows = CharacterSkillFormulaRuntimeContextPolicy
                    .ConsumesAllAppliedAxes(
                        CharacterSkillKind.Passive,
                        CharacterSkillTrigger.OperatingDayStarted,
                        CharacterUltimateDomain.None,
                        module);
                bool passiveReachable = CharacterSkillModuleCapabilityRegistry
                    .Require(module)
                    .IsManagementModuleReachable(
                        CharacterSkillKind.Passive,
                        CharacterSkillTrigger.OperatingDayStarted);
                if (passivePolicyAllows != passiveReachable)
                {
                    throw new InvalidOperationException(
                        $"Management module '{module.id}' has inconsistent OperatingDayStarted passive reachability.");
                }
            }
        }
        NarrativeFormulaCapabilityDescriptor[] descriptors = modules.Select(settings.RequireFormulaDescriptor).ToArray();
        HashSet<string> capabilityIds = new HashSet<string>(
            descriptors.Select(value => value.CapabilityId),
            StringComparer.Ordinal);
        if (AppliedAxesByCapability.Count != ExpectedModuleCount
            || !capabilityIds.SetEquals(AppliedAxesByCapability.Keys))
            throw new InvalidOperationException(
                "CharacterSkill applied-axis registry must cover every current capability exactly once.");
        foreach (NarrativeFormulaCapabilityDescriptor descriptor in descriptors)
        {
            string[] expectedApplied = AppliedParameterIds(
                AppliedAxesByCapability[descriptor.CapabilityId]).ToArray();
            if (!descriptor.AppliedParameterIds.SequenceEqual(expectedApplied, StringComparer.Ordinal))
                throw new InvalidOperationException(
                    $"Capability '{descriptor.CapabilityId}' applied-axis registration is stale.");
            if (descriptor.ForbiddenSynergies.Any(value => !capabilityIds.Contains(value)))
                throw new InvalidOperationException(
                    $"Capability '{descriptor.CapabilityId}' names an unknown forbidden synergy.");
            foreach (string forbidden in descriptor.ForbiddenSynergies)
            {
                NarrativeFormulaCapabilityDescriptor other = descriptors.Single(value =>
                    string.Equals(value.CapabilityId, forbidden, StringComparison.Ordinal));
                if (!other.ForbiddenSynergies.Contains(
                        descriptor.CapabilityId, StringComparer.Ordinal))
                    throw new InvalidOperationException(
                        $"Forbidden synergy '{descriptor.CapabilityId}/{forbidden}' is not symmetric.");
            }
            foreach (NarrativeFormulaPairSynergyCost pair in descriptor.PairSynergyCosts)
            {
                NarrativeFormulaCapabilityDescriptor other = descriptors.SingleOrDefault(value =>
                    string.Equals(value.CapabilityId, pair.OtherCapabilityId, StringComparison.Ordinal));
                NarrativeFormulaPairSynergyCost reverse = other?.PairSynergyCosts.SingleOrDefault(value =>
                    string.Equals(value.OtherCapabilityId, descriptor.CapabilityId, StringComparison.Ordinal));
                if (reverse == null || reverse.Cost != pair.Cost)
                    throw new InvalidOperationException(
                        $"Pair cost '{descriptor.CapabilityId}/{pair.OtherCapabilityId}' is not symmetric.");
            }
        }
        foreach (CharacterSkillDrawbackCapabilityDefinition drawback in
                 settings.RequireDrawbackCatalog())
        {
            NarrativeFormulaCapabilityDescriptor descriptor =
                drawback.RequireFormulaDescriptor();
            if (descriptor.ForbiddenSynergies.Any(value =>
                    !capabilityIds.Contains(value)))
                throw new InvalidOperationException(
                    $"Drawback '{drawback.DrawbackId}' names an unknown positive capability conflict.");
        }
        string expected = ComputeCanonicalCatalogSha256(settings);
        if (!string.Equals(expected, settings.formulaPolicy.RequireCatalogSha256(), StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"CharacterSkill formula catalog SHA mismatch. Expected '{expected}'.");
    }

    public static string ComputeCanonicalCatalogSha256(CharacterSkillSystemSettingsSO settings)
    {
        if (settings == null || settings.formulaPolicy == null)
            throw new ArgumentNullException(nameof(settings));
        StringBuilder canonical = new StringBuilder();
        CharacterSkillFormulaPolicyDefinition policy = settings.formulaPolicy;
        Append(canonical, "formulaVersion", policy.formulaVersion);
        Append(canonical, "baseBudget", policy.baseBudget);
        Append(canonical, "powerScale", policy.powerScale);
        Append(canonical, "softCapK", policy.softCapK.ToString("R", CultureInfo.InvariantCulture));
        Append(canonical, "minimumImportance", policy.minimumImportance.ToString("R", CultureInfo.InvariantCulture));
        Append(canonical, "maximumImportance", policy.maximumImportance.ToString("R", CultureInfo.InvariantCulture));
        NarrativeFormulaDrawbackCreditPolicyDefinition drawback = policy.drawbackCredit
            ?? throw new InvalidOperationException("CharacterSkill drawback-credit policy is missing.");
        Append(canonical, "drawbackPlayerFraction", drawback.playerChoiceMaximumBudgetFraction.ToString("R", CultureInfo.InvariantCulture));
        Append(canonical, "drawbackAutomaticFraction", drawback.automaticMaximumBudgetFraction.ToString("R", CultureInfo.InvariantCulture));
        Append(canonical, "drawbackAbsoluteCap", drawback.absoluteMaximumCredit);
        Append(canonical, "drawbackAutomaticNeedsNegativeEvidence", drawback.requireNegativeEvidenceForAutomatic);
        foreach (float weight in policy.milestoneWeights ?? new List<float>())
            Append(canonical, "milestone", weight.ToString("R", CultureInfo.InvariantCulture));
        foreach (CharacterSkillTriggerFormulaCostDefinition value in (policy.triggerCosts
            ?? new List<CharacterSkillTriggerFormulaCostDefinition>()).OrderBy(value => value.trigger))
            Append(canonical, "trigger", value.trigger + ":" + value.frequencyUnits + ":" + value.guaranteedProc);
        foreach (CharacterSkillTargetFormulaCostDefinition value in (policy.targetCosts
            ?? new List<CharacterSkillTargetFormulaCostDefinition>()).OrderBy(value => value.target))
            Append(canonical, "target", value.target + ":" + value.targetCount);
        foreach (CharacterSkillDrawbackCapabilityDefinition value in settings
                     .RequireDrawbackCatalog()
                     .OrderBy(value => value.DrawbackId, StringComparer.Ordinal))
        {
            Append(canonical, "skillDrawback", value.DrawbackId);
            Append(canonical, "skillDrawbackCapability", value.CapabilityId);
            Append(canonical, "skillDrawbackDisplayName", value.DisplayName);
            Append(canonical, "skillDrawbackDescription", value.Description);
            Append(canonical, "skillDrawbackKind", value.applicationKind);
            Append(canonical, "skillDrawbackEffectId", value.effectDefinition?.EffectId ?? string.Empty);
            Append(canonical, "skillDrawbackTargetId", value.effectDefinition?.TargetId ?? string.Empty);
            Append(canonical, "skillDrawbackOperation", value.effectDefinition?.Operation.ToString() ?? string.Empty);
            Append(canonical, "skillDrawbackStatDeltaPerCredit",
                value.statDeltaPerCredit.ToString("R", CultureInfo.InvariantCulture));
            Append(canonical, "skillDrawbackMaximumCredit", value.MaximumCredit);
            foreach (CharacterSkillKind kind in value.allowedKinds.OrderBy(item => item))
                Append(canonical, "skillDrawbackAllowedKind", kind);
            foreach (CharacterSkillTrigger trigger in value.allowedTriggers.OrderBy(item => item))
                Append(canonical, "skillDrawbackAllowedTrigger", trigger);
            foreach (CharacterNarrativeDomain domain in value.domainAffinities.OrderBy(item => item))
                Append(canonical, "skillDrawbackDomain", domain);
            CharacterSkillCapabilityFormulaDefinition formula = value.formula;
            Append(canonical, "skillDrawbackCosts", string.Join(":", formula.baseCost,
                formula.triggerFrequencyCostPerUnit, formula.guaranteedProcCost,
                formula.areaCostPerExtraTarget, formula.multiEffectCostPerExtraEffect));
            Append(canonical, "skillDrawbackAffinity",
                formula.narrativeAffinity.ToString("R", CultureInfo.InvariantCulture));
            Append(canonical, "skillDrawbackFormatter", formula.formatterId);
            Append(canonical, "skillDrawbackApplicator", formula.applicatorId);
            foreach (string item in Ordered(formula.appliedParameterIds))
                Append(canonical, "skillDrawbackAppliedParameter", item);
            foreach (string item in Ordered(formula.affinityKeys))
                Append(canonical, "skillDrawbackAffinityKey", item);
            foreach (string item in Ordered(formula.conflictGroups))
                Append(canonical, "skillDrawbackConflictGroup", item);
            foreach (CharacterSkillFormulaAxisDefinition axis in formula.parameterRanges
                         .OrderBy(item => item.parameterId, StringComparer.Ordinal))
                Append(canonical, "skillDrawbackAxis", string.Join(":", axis.parameterId,
                    axis.minimumUnits, axis.maximumUnits, axis.quantumUnits,
                    axis.decimalPlaces, axis.costPerQuantum));
        }
        foreach (CharacterSkillModuleRule module in RequireCurrentModules(settings).OrderBy(value => value.id, StringComparer.Ordinal))
        {
            CharacterSkillCapabilityFormulaDefinition formula = module.formula
                ?? throw new InvalidOperationException($"Module '{module.id}' has no formula metadata.");
            CharacterSkillModuleCapabilityDescriptor capability =
                CharacterSkillModuleCapabilityRegistry.Require(module);
            Append(canonical, "module", module.id);
            Append(canonical, "capability", capability.CapabilityId);
            Append(canonical, "capabilityDomain", capability.Domain);
            Append(canonical, "targetPolicy", capability.TargetPolicy);
            Append(canonical, "managementReachability", capability.ManagementReachability);
            foreach (CharacterSkillTrigger trigger in Enum.GetValues(typeof(CharacterSkillTrigger))
                         .Cast<CharacterSkillTrigger>().Where(capability.WouldSelfTrigger))
                Append(canonical, "selfTriggerHazard", trigger);
            Append(canonical, "moduleType", module.GetType().FullName);
            Append(canonical, "displayName", module.displayName ?? string.Empty);
            foreach (CharacterSkillKind value in (module.allowedKinds ?? new List<CharacterSkillKind>()).Distinct().OrderBy(value => value))
                Append(canonical, "allowedKind", value);
            foreach (CharacterSkillTrigger value in (module.allowedTriggers ?? new List<CharacterSkillTrigger>()).Distinct().OrderBy(value => value))
                Append(canonical, "allowedTrigger", value);
            foreach (CharacterSkillTarget value in (module.allowedTargets ?? new List<CharacterSkillTarget>()).Distinct().OrderBy(value => value))
                Append(canonical, "allowedTarget", value);
            Append(canonical, "costs", string.Join(":", formula.baseCost, formula.triggerFrequencyCostPerUnit,
                formula.guaranteedProcCost, formula.areaCostPerExtraTarget, formula.multiEffectCostPerExtraEffect));
            foreach (string value in Ordered(formula.appliedParameterIds)) Append(canonical, "appliedParameter", value);
            Append(canonical, "affinity", formula.narrativeAffinity.ToString("R", CultureInfo.InvariantCulture));
            Append(canonical, "formatter", formula.formatterId);
            Append(canonical, "applicator", formula.applicatorId);
            foreach (string value in Ordered(formula.affinityKeys)) Append(canonical, "affinityKey", value);
            foreach (string value in Ordered(formula.conflictGroups)) Append(canonical, "conflictGroup", value);
            foreach (string value in Ordered(formula.forbiddenSynergies)) Append(canonical, "forbidden", value);
            foreach (CharacterSkillPairSynergyCostDefinition pair in (formula.pairSynergyCosts
                ?? new List<CharacterSkillPairSynergyCostDefinition>()).OrderBy(value => value.otherCapabilityId, StringComparer.Ordinal))
                Append(canonical, "pair", pair.otherCapabilityId + ":" + pair.cost);
            foreach (CharacterSkillFormulaAxisDefinition axis in (formula.parameterRanges
                ?? new List<CharacterSkillFormulaAxisDefinition>()).OrderBy(value => value.parameterId, StringComparer.Ordinal))
                Append(canonical, "axis", string.Join(":", axis.parameterId, axis.minimumUnits, axis.maximumUnits,
                    axis.quantumUnits, axis.decimalPlaces, axis.costPerQuantum));
        }
        return NarrativeInferenceHash.ComputeSha256Utf8(canonical.ToString())
            .Substring("sha256:".Length);
    }

    private static CharacterSkillCapabilityFormulaDefinition BuildMathematicalFormula(
        CharacterSkillModuleRule module)
    {
        string capabilityId = CharacterSkillModuleCapabilityRegistry.Require(module).CapabilityId;
        if (!AppliedAxesByCapability.TryGetValue(capabilityId, out AppliedAxes appliedAxes))
            throw new InvalidOperationException(
                $"Capability '{capabilityId}' has no explicit applied-axis registration.");
        return new CharacterSkillCapabilityFormulaDefinition
        {
            appliedParameterIds = AppliedParameterIds(appliedAxes).ToList(),
            baseCost = BaseCost(capabilityId),
            triggerFrequencyCostPerUnit = 1,
            guaranteedProcCost = 1,
            areaCostPerExtraTarget = 1,
            multiEffectCostPerExtraEffect = 1,
            pairSynergyCosts = BuildPairCosts(capabilityId),
            narrativeAffinity = 1f,
            affinityKeys = new List<string>
            {
                module is CharacterManagementSkillModuleRule ? CharacterNarrativeDomain.Work.ToString() : CharacterNarrativeDomain.Combat.ToString()
            },
            conflictGroups = new List<string> { "capability:" + capabilityId },
            forbiddenSynergies = BuildForbiddenSynergies(capabilityId),
            formatterId = capabilityId,
            applicatorId = capabilityId,
            parameterRanges = BuildMathematicalRanges(capabilityId)
        };
    }

    private static int BaseCost(string capabilityId)
    {
        return capabilityId switch
        {
            "dot" or "vulnerability" or "cleanse" or "reposition" or
                "multi_target" or "stock" or "revenue" => 1,
            "buff" or "debuff" or "protect" or "conditional_amplify" or
                "cooldown_adjust" => 2,
            _ => 0
        };
    }

    private static List<CharacterSkillFormulaAxisDefinition> BuildMathematicalRanges(
        string capabilityId)
    {
        CharacterSkillFormulaAxisDefinition magnitude = Fixed(
            NarrativeFormulaParameterIds.Magnitude, 0, decimalPlaces: 3);
        CharacterSkillFormulaAxisDefinition duration = Fixed(
            NarrativeFormulaParameterIds.Duration, 0);
        CharacterSkillFormulaAxisDefinition count = Fixed(
            NarrativeFormulaParameterIds.Count, 1);
        CharacterSkillFormulaAxisDefinition targetCount = Fixed(
            NarrativeFormulaParameterIds.TargetCount, 1);

        switch (capabilityId)
        {
            case "damage":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 500, 3000, 250, 3, 1);
                count = Range(NarrativeFormulaParameterIds.Count, 1, 4, 1, 0, 2);
                break;
            case "heal":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 5000, 45000, 5000, 3, 2);
                break;
            case "guard":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 100, 600, 50, 3, 1);
                duration = Range(NarrativeFormulaParameterIds.Duration, 1, 5, 1, 0, 2);
                break;
            case "dot":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 2000, 12000, 1000, 3, 1);
                duration = Range(NarrativeFormulaParameterIds.Duration, 1, 6, 1, 0, 2);
                break;
            case "vulnerability":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 100, 550, 50, 3, 1);
                duration = Range(NarrativeFormulaParameterIds.Duration, 1, 5, 1, 0, 2);
                break;
            case "delay":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 1000, 6000, 1000, 3, 3);
                break;
            case "buff":
            case "debuff":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 50, 500, 50, 3, 1);
                duration = Range(NarrativeFormulaParameterIds.Duration, 1, 5, 1, 0, 2);
                break;
            case "cleanse":
                count = Range(NarrativeFormulaParameterIds.Count, 1, 6, 1, 0, 3);
                break;
            case "protect":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 100, 600, 50, 3, 1);
                duration = Range(NarrativeFormulaParameterIds.Duration, 1, 5, 1, 0, 2);
                break;
            case "reposition":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 1000, 2000, 1000, 3, 3);
                break;
            case "multi_target":
                targetCount = Range(NarrativeFormulaParameterIds.TargetCount, 2, 8, 1, 0, 3);
                break;
            case "conditional_amplify":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 100, 1000, 100, 3, 2);
                break;
            case "cooldown_adjust":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 1000, 6000, 1000, 3, 3);
                break;
            case "work_speed":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 50, 800, 50, 3, 1);
                break;
            case "output":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 50, 800, 50, 3, 1);
                break;
            case "cleaning":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 2000, 32000, 2000, 3, 1);
                break;
            case "repair":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 5000, 80000, 5000, 3, 1);
                break;
            case "stock":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 1000, 9000, 1000, 3, 2);
                break;
            case "research":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 2000, 40000, 2000, 3, 1);
                break;
            case "needs":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 2000, 32000, 2000, 3, 1);
                break;
            case "mood":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 2000, 20000, 2000, 3, 1);
                duration = Range(NarrativeFormulaParameterIds.Duration, 60, 420, 60, 0, 1);
                break;
            case "relationship":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 1000, 20000, 1000, 3, 1);
                break;
            case "revenue":
                magnitude = Range(NarrativeFormulaParameterIds.Magnitude, 10, 150, 10, 3, 1);
                break;
            default:
                throw new InvalidOperationException(
                    $"Capability '{capabilityId}' has no mathematical range profile.");
        }

        return new List<CharacterSkillFormulaAxisDefinition>
        {
            magnitude, duration, count, targetCount
        };
    }

    private static IEnumerable<string> AppliedParameterIds(AppliedAxes axes)
    {
        if ((axes & AppliedAxes.Magnitude) != 0) yield return NarrativeFormulaParameterIds.Magnitude;
        if ((axes & AppliedAxes.Duration) != 0) yield return NarrativeFormulaParameterIds.Duration;
        if ((axes & AppliedAxes.Count) != 0) yield return NarrativeFormulaParameterIds.Count;
        if ((axes & AppliedAxes.TargetCount) != 0) yield return NarrativeFormulaParameterIds.TargetCount;
    }

    private static CharacterSkillFormulaAxisDefinition Range(
        string parameterId,
        long minimumUnits,
        long maximumUnits,
        long quantumUnits,
        int decimalPlaces,
        int costPerQuantum)
    {
        return new CharacterSkillFormulaAxisDefinition
        {
            parameterId = parameterId,
            minimumUnits = minimumUnits,
            maximumUnits = maximumUnits,
            quantumUnits = quantumUnits,
            decimalPlaces = decimalPlaces,
            costPerQuantum = costPerQuantum
        };
    }

    private static CharacterSkillFormulaAxisDefinition Fixed(
        string parameterId,
        long units,
        int decimalPlaces = 0) =>
        Range(parameterId, units, units, 1, decimalPlaces, 1);

    private static List<CharacterSkillPairSynergyCostDefinition> BuildPairCosts(string capabilityId)
    {
        Dictionary<string, string> pairs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["damage"] = "conditional_amplify",
            ["conditional_amplify"] = "damage",
            ["work_speed"] = "output",
            ["output"] = "work_speed"
        };
        return pairs.TryGetValue(capabilityId, out string other)
            ? new List<CharacterSkillPairSynergyCostDefinition>
            {
                new CharacterSkillPairSynergyCostDefinition { otherCapabilityId = other, cost = 1 }
            }
            : new List<CharacterSkillPairSynergyCostDefinition>();
    }

    private static List<string> BuildForbiddenSynergies(string capabilityId) =>
        capabilityId switch
        {
            "guard" => new List<string> { "protect" },
            "protect" => new List<string> { "guard" },
            _ => new List<string>()
        };

    private static List<CharacterSkillTriggerFormulaCostDefinition> BuildTriggerCosts()
    {
        return Enum.GetValues(typeof(CharacterSkillTrigger)).Cast<CharacterSkillTrigger>()
            .Select(trigger => new CharacterSkillTriggerFormulaCostDefinition
            {
                trigger = trigger,
                frequencyUnits = trigger is CharacterSkillTrigger.DamageTaken
                    or CharacterSkillTrigger.NeedChanged
                    or CharacterSkillTrigger.MoodChanged
                    or CharacterSkillTrigger.RelationshipChanged ? 3
                    : trigger is CharacterSkillTrigger.EnemyDefeated
                        or CharacterSkillTrigger.WorkStarted
                        or CharacterSkillTrigger.WorkCompleted ? 2 : 1,
                guaranteedProc = true
            }).ToList();
    }

    private static IReadOnlyList<CharacterSkillDrawbackCapabilityDefinition> BuildDrawbacks()
    {
        return new[]
        {
            new CharacterSkillDrawbackCapabilityDefinition
            {
                drawbackId = "character-skill:drawback:combat-cooldown",
                capabilityId = "character-skill-drawback-combat-cooldown",
                displayName = "긴 전투 재정비",
                description = "부정적인 전투 경험 때문에 발동 뒤 재사용 대기시간이 늘어난다. C#이 예산 안에서 증가 턴을 확정한다.",
                applicationKind = CharacterSkillDrawbackApplicationKind.CombatCooldownTurns,
                allowedKinds = new List<CharacterSkillKind>
                {
                    CharacterSkillKind.Active,
                    CharacterSkillKind.Ultimate
                },
                allowedTriggers = new List<CharacterSkillTrigger>
                {
                    CharacterSkillTrigger.ManualCombat,
                    CharacterSkillTrigger.BattleStarted,
                    CharacterSkillTrigger.DamageTaken,
                    CharacterSkillTrigger.EnemyDefeated,
                    CharacterSkillTrigger.BattleCompleted,
                    CharacterSkillTrigger.InvasionStarted
                },
                domainAffinities = new List<CharacterNarrativeDomain>
                {
                    CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Invasion,
                    CharacterNarrativeDomain.Expedition,
                    CharacterNarrativeDomain.Combat
                },
                maximumCredit = 2,
                formula = BuildDrawbackFormula(
                    "character-skill-drawback-combat-cooldown",
                    CharacterNarrativeDomain.Injury,
                    CharacterNarrativeDomain.Survival,
                    CharacterNarrativeDomain.Invasion,
                    CharacterNarrativeDomain.Expedition,
                    CharacterNarrativeDomain.Combat)
            },
            new CharacterSkillDrawbackCapabilityDefinition
            {
                drawbackId = "character-skill:drawback:work-cooldown",
                capabilityId = "character-skill-drawback-work-cooldown",
                displayName = "긴 작업 재정비",
                description = "부정적인 작업 경험 때문에 사용 뒤 다시 지시할 수 있는 날이 늦어진다. C#이 예산 안에서 증가 일수를 확정한다.",
                applicationKind = CharacterSkillDrawbackApplicationKind.WorkCooldownDays,
                allowedKinds = new List<CharacterSkillKind>
                {
                    CharacterSkillKind.Active
                },
                allowedTriggers = new List<CharacterSkillTrigger>
                {
                    CharacterSkillTrigger.ManualWork
                },
                domainAffinities = new List<CharacterNarrativeDomain>
                {
                    CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.FacilityUse
                },
                maximumCredit = 2,
                formula = BuildDrawbackFormula(
                    "character-skill-drawback-work-cooldown",
                    CharacterNarrativeDomain.Work,
                    CharacterNarrativeDomain.FacilityUse)
            },
            StatDrawback(
                "work-speed", "작업 리듬 붕괴", "작업 속도",
                "effect_character_work-speed_multiply.asset",
                new[] { CharacterSkillTrigger.ManualWork, CharacterSkillTrigger.WorkStarted,
                    CharacterSkillTrigger.WorkCompleted, CharacterSkillTrigger.OperatingDayStarted },
                new[] { "work_speed" },
                CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse),
            StatDrawback(
                "research-speed", "집중력 소모", "연구 속도",
                "effect_character_research-speed_multiply.asset",
                new[] { CharacterSkillTrigger.ManualWork, CharacterSkillTrigger.WorkStarted,
                    CharacterSkillTrigger.WorkCompleted, CharacterSkillTrigger.OperatingDayStarted },
                new[] { "research" },
                CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse,
                CharacterNarrativeDomain.Mood),
            StatDrawback(
                "combat-power", "전투 후유증", "전투력",
                "effect_character_combat-power_multiply.asset",
                new[] { CharacterSkillTrigger.ManualCombat, CharacterSkillTrigger.BattleStarted,
                    CharacterSkillTrigger.DamageTaken, CharacterSkillTrigger.EnemyDefeated,
                    CharacterSkillTrigger.BattleCompleted, CharacterSkillTrigger.InvasionStarted },
                Array.Empty<string>(),
                CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Survival,
                CharacterNarrativeDomain.Invasion, CharacterNarrativeDomain.Expedition,
                CharacterNarrativeDomain.Combat),
            StatDrawback(
                "move-speed", "굳은 발걸음", "이동 속도",
                "effect_character_move-speed_multiply.asset",
                new[] { CharacterSkillTrigger.ManualCombat, CharacterSkillTrigger.BattleStarted,
                    CharacterSkillTrigger.DamageTaken, CharacterSkillTrigger.BattleCompleted,
                    CharacterSkillTrigger.InvasionStarted, CharacterSkillTrigger.ManualWork },
                Array.Empty<string>(),
                CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Survival,
                CharacterNarrativeDomain.Invasion, CharacterNarrativeDomain.Expedition,
                CharacterNarrativeDomain.Work),
            StatDrawback(
                "fatigue-rate", "빠른 피로 누적", "피로 누적",
                "effect_character_fatigue-rate_multiply.asset",
                new[] { CharacterSkillTrigger.ManualCombat, CharacterSkillTrigger.BattleStarted,
                    CharacterSkillTrigger.DamageTaken, CharacterSkillTrigger.BattleCompleted,
                    CharacterSkillTrigger.ManualWork, CharacterSkillTrigger.WorkStarted,
                    CharacterSkillTrigger.WorkCompleted },
                Array.Empty<string>(),
                CharacterNarrativeDomain.Injury, CharacterNarrativeDomain.Survival,
                CharacterNarrativeDomain.Expedition, CharacterNarrativeDomain.Combat,
                CharacterNarrativeDomain.Work),
            StatDrawback(
                "accident-chance", "불안정한 작업 습관", "사고 확률",
                "effect_character_accident-chance_multiply.asset",
                new[] { CharacterSkillTrigger.ManualWork, CharacterSkillTrigger.WorkStarted,
                    CharacterSkillTrigger.WorkCompleted, CharacterSkillTrigger.OperatingDayStarted },
                Array.Empty<string>(),
                CharacterNarrativeDomain.Work, CharacterNarrativeDomain.FacilityUse,
                CharacterNarrativeDomain.Injury)
        };
    }

    private static CharacterSkillDrawbackCapabilityDefinition StatDrawback(
        string suffix,
        string displayName,
        string statName,
        string effectAssetName,
        IEnumerable<CharacterSkillTrigger> triggers,
        IEnumerable<string> forbiddenPositiveCapabilityIds,
        params CharacterNarrativeDomain[] domains)
    {
        string capabilityId = "character-skill-drawback-" + suffix;
        return new CharacterSkillDrawbackCapabilityDefinition
        {
            drawbackId = "character-skill:drawback:" + suffix,
            capabilityId = capabilityId,
            displayName = displayName,
            description = $"기술을 장착한 동안 {statName}에 해로운 변화가 적용된다. C#이 예산 안에서 정확한 수치를 확정한다.",
            applicationKind = CharacterSkillDrawbackApplicationKind.EquippedStat,
            effectDefinition = RequireEffectDefinition(effectAssetName),
            statDeltaPerCredit = 0.03f,
            allowedKinds = new List<CharacterSkillKind>
            {
                CharacterSkillKind.Active,
                CharacterSkillKind.Passive,
                CharacterSkillKind.Ultimate
            },
            allowedTriggers = triggers.Distinct().ToList(),
            domainAffinities = domains.Distinct().ToList(),
            maximumCredit = 2,
            formula = BuildDrawbackFormula(
                capabilityId,
                forbiddenPositiveCapabilityIds,
                domains)
        };
    }

    private static GameplayEffectDefinitionSO RequireEffectDefinition(
        string assetName)
    {
        string path = "Assets/Resources/SO/V26/Effects/Definitions/" + assetName;
        return AssetDatabase.LoadAssetAtPath<GameplayEffectDefinitionSO>(path)
            ?? throw new InvalidOperationException(
                $"Missing CharacterSkill burden effect definition at '{path}'.");
    }

    private static CharacterSkillCapabilityFormulaDefinition BuildDrawbackFormula(
        string capabilityId,
        params CharacterNarrativeDomain[] domains)
        => BuildDrawbackFormula(
            capabilityId,
            Array.Empty<string>(),
            domains);

    private static CharacterSkillCapabilityFormulaDefinition BuildDrawbackFormula(
        string capabilityId,
        IEnumerable<string> forbiddenPositiveCapabilityIds,
        params CharacterNarrativeDomain[] domains)
    {
        return new CharacterSkillCapabilityFormulaDefinition
        {
            appliedParameterIds = new List<string>
            {
                NarrativeFormulaParameterIds.Magnitude
            },
            baseCost = 1,
            triggerFrequencyCostPerUnit = 0,
            guaranteedProcCost = 0,
            areaCostPerExtraTarget = 0,
            multiEffectCostPerExtraEffect = 0,
            pairSynergyCosts = new List<CharacterSkillPairSynergyCostDefinition>(),
            narrativeAffinity = 1f,
            affinityKeys = (domains ?? Array.Empty<CharacterNarrativeDomain>())
                .Distinct()
                .OrderBy(value => value)
                .Select(value => value.ToString())
                .ToList(),
            conflictGroups = new List<string> { "drawback:character-skill-cooldown" },
            forbiddenSynergies = (forbiddenPositiveCapabilityIds
                    ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            formatterId = capabilityId,
            applicatorId = capabilityId,
            parameterRanges = new List<CharacterSkillFormulaAxisDefinition>
            {
                Range(NarrativeFormulaParameterIds.Magnitude, 1, 2, 1, 0, 1),
                Fixed(NarrativeFormulaParameterIds.Duration, 0),
                Fixed(NarrativeFormulaParameterIds.Count, 1),
                Fixed(NarrativeFormulaParameterIds.TargetCount, 1)
            }
        };
    }

    private static List<CharacterSkillTargetFormulaCostDefinition> BuildTargetCosts()
    {
        return Enum.GetValues(typeof(CharacterSkillTarget)).Cast<CharacterSkillTarget>()
            .Select(target => new CharacterSkillTargetFormulaCostDefinition
            {
                target = target,
                targetCount = target is CharacterSkillTarget.AllAllies or CharacterSkillTarget.AllEnemies ? 3 : 1
            }).ToList();
    }

    private static CharacterSkillSystemSettingsSO RequireSettings()
    {
        return AssetDatabase.LoadAssetAtPath<CharacterSkillSystemSettingsSO>(SettingsAssetPath)
            ?? throw new InvalidOperationException($"Missing CharacterSkill settings asset at '{SettingsAssetPath}'.");
    }

    private static CharacterSkillModuleRule[] RequireCurrentModules(CharacterSkillSystemSettingsSO settings)
    {
        CharacterSkillModuleRule[] modules = (settings.Modules ?? Array.Empty<CharacterSkillModuleRule>())
            .Where(value => value != null).ToArray();
        if (modules.Length != ExpectedModuleCount
            || modules.Select(value => value.id).Any(string.IsNullOrWhiteSpace)
            || modules.Select(value => value.id).Distinct(StringComparer.Ordinal).Count() != ExpectedModuleCount)
            throw new InvalidOperationException(
                $"CharacterSkill formula catalog requires exactly {ExpectedModuleCount} distinct current modules.");
        return modules;
    }

    private static void RequireCompleteEnumMap<TDefinition, TEnum>(
        IEnumerable<TDefinition> definitions,
        Func<TDefinition, TEnum> selector,
        IEnumerable<TEnum> expected,
        string label)
    {
        TDefinition[] values = (definitions ?? throw new InvalidOperationException($"CharacterSkill {label} map is missing."))
            .ToArray();
        TEnum[] enumValues = expected.ToArray();
        if (values.Any(value => value is null)
            || values.Select(selector).Distinct().Count() != enumValues.Length
            || enumValues.Any(item => values.Count(value => EqualityComparer<TEnum>.Default.Equals(selector(value), item)) != 1))
            throw new InvalidOperationException($"CharacterSkill {label} map must cover every enum value exactly once.");
    }

    private static void RequireDistinctDefined<TEnum>(
        IEnumerable<TEnum> source,
        string label,
        string moduleId)
        where TEnum : struct
    {
        TEnum[] values = (source ?? throw new InvalidOperationException(
            $"Module '{moduleId}' has no allowed {label} declaration.")).ToArray();
        if (values.Length == 0
            || values.Distinct().Count() != values.Length
            || values.Any(value => !Enum.IsDefined(typeof(TEnum), value)))
            throw new InvalidOperationException(
                $"Module '{moduleId}' has an invalid or duplicate allowed {label} declaration.");
    }

    private static IEnumerable<string> Ordered(IEnumerable<string> values) =>
        (values ?? Array.Empty<string>()).OrderBy(value => value, StringComparer.Ordinal);

    private static void Append(StringBuilder builder, string key, object value) =>
        builder.Append(key).Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture)).Append('\n');
}
