using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

public enum CharacterSkillSemanticsExecutionScope
{
    OffenseBattle,
    OutsidePassive,
    DefenseUltimate,
    ManagementEvent,
    ManagementQuery
}

public enum CharacterSkillSemanticsEffectKind
{
    NoOp,
    BasicDamage,
    FlatHealAndDrain,
    GuardStatus,
    DamageOverTimeStatus,
    VulnerabilityStatus,
    InitiativeDelay,
    AttackModifier,
    StatusCleanse,
    FormationReposition,
    MultiTargetDamage,
    ConditionalBasicDamage,
    CooldownAdjustment,
    DirectHealthDamage,
    CasterHeal,
    ProtectionMood,
    InjuryMoodCleanse,
    DefenseDamageContribution,
    WorkSpeedMultiplier,
    ProductionOutputMultiplier,
    HygieneRestore,
    CleaningSpeedMultiplier,
    RepairSpeedMultiplier,
    StockOutputBonus,
    ResearchWorkBonus,
    LowestNeedRestore,
    MoodFactor,
    PositiveRelationshipBonus,
    RevenueMultiplier
}

[Serializable]
public sealed class CharacterSkillModuleSemanticsDto
{
    public string moduleId = string.Empty;
    public string variantId = string.Empty;
    public string executionScope = string.Empty;
    public string effectKind = string.Empty;
    public List<string> terms = new List<string>();
    public string descriptionKo = string.Empty;

    public CharacterSkillModuleSemanticsDto Clone()
    {
        return new CharacterSkillModuleSemanticsDto
        {
            moduleId = moduleId,
            variantId = variantId,
            executionScope = executionScope,
            effectKind = effectKind,
            terms = terms?.ToList() ?? new List<string>(),
            descriptionKo = descriptionKo
        };
    }
}

[Serializable]
public sealed class CharacterSkillCombinationSemanticsDto
{
    public int schemaVersion;
    public string combinationId = string.Empty;
    public List<CharacterSkillModuleSemanticsDto> modules =
        new List<CharacterSkillModuleSemanticsDto>();
    public string descriptionKo = string.Empty;

    public CharacterSkillCombinationSemanticsDto Clone()
    {
        return new CharacterSkillCombinationSemanticsDto
        {
            schemaVersion = schemaVersion,
            combinationId = combinationId,
            modules = modules?.Where(value => value != null).Select(value => value.Clone()).ToList()
                ?? new List<CharacterSkillModuleSemanticsDto>(),
            descriptionKo = descriptionKo
        };
    }
}

[Serializable]
public sealed class CharacterSkillSemanticsCatalogEntryDto
{
    public string applicability = string.Empty;
    public string contextId = string.Empty;
    public string kind = string.Empty;
    public string moduleId = string.Empty;
    public CharacterSkillCombinationSemanticsDto semantics;
    public string target = string.Empty;
    public string trigger = string.Empty;
    public string ultimateDomain = string.Empty;
    public string variantId = string.Empty;
}

/// <summary>
/// Versioned public projection of the effects that the current CharacterSkill
/// runtimes actually consume. It is deliberately excluded from mechanical
/// identity and combination-ID construction.
/// </summary>
public static class CharacterSkillCombinationSemanticsFactory
{
    public const int SchemaVersion = 1;
    public const string CatalogCoveragePolicy =
        "factory_projected_representative_behavior_contexts";
    public const string CatalogEntryApplicability =
        "representative_behavior_context_only";

    private static readonly string[] ScopeTokens =
    {
        "defense_ultimate",
        "management_event",
        "management_query",
        "offense_battle",
        "outside_passive"
    };

    private static readonly string[] KindTokens = Enum
        .GetValues(typeof(CharacterSkillSemanticsEffectKind))
        .Cast<CharacterSkillSemanticsEffectKind>()
        .Select(EffectKindToken)
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();

    public static IReadOnlyList<string> ExecutionScopeTokens =>
        Array.AsReadOnly(ScopeTokens);

    public static IReadOnlyList<string> EffectKindTokens =>
        Array.AsReadOnly(KindTokens);

    public static CharacterSkillCombinationSemanticsDto Create(
        CharacterSkillAllowedCombination combination,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillSystemSettingsSO settings)
    {
        if (combination == null) throw new ArgumentNullException(nameof(combination));
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        ValidateSettingsCoverage(settings);
        ValidateCombinationAuthority(combination, rule, kind, settings);

        List<CharacterSkillModuleSemanticsDto> modules =
            new List<CharacterSkillModuleSemanticsDto>();
        foreach (CharacterSkillModuleSelection selection in combination.Modules)
        {
            CharacterSkillModuleRule module = settings.FindModule(selection.moduleId);
            CharacterSkillNumericVariant variant = module.FindVariant(selection.variantId);
            string capabilityId = CharacterSkillModuleCapabilityRegistry
                .Require(module)
                .CapabilityId;
            foreach (CharacterSkillSemanticsExecutionScope scope in ResolveScopes(kind, rule, module))
            {
                modules.Add(CreateModuleSemantics(
                    selection,
                    variant,
                    rule,
                    kind,
                    scope,
                    capabilityId));
            }
        }

        CharacterSkillCombinationSemanticsDto result =
            new CharacterSkillCombinationSemanticsDto
            {
                schemaVersion = SchemaVersion,
                combinationId = combination.Id,
                modules = modules,
                descriptionKo = string.Join(" ", modules.Select(value =>
                    $"{ScopeDescriptionKo(value.executionScope)}: {value.descriptionKo}"))
            };
        ValidateDtoShape(result);
        return result;
    }

    public static IReadOnlyList<CharacterSkillSemanticsCatalogEntryDto> CreateCatalogCoverage(
        CharacterSkillSystemSettingsSO settings)
    {
        ValidateSettingsCoverage(settings);
        List<CharacterSkillSemanticsCatalogEntryDto> entries =
            new List<CharacterSkillSemanticsCatalogEntryDto>();
        foreach (CharacterSkillModuleRule module in settings.Modules
                     .OrderBy(value => value.id, StringComparer.Ordinal))
        {
            foreach (CharacterSkillNumericVariant variant in module.variants
                         .OrderBy(value => value.id, StringComparer.Ordinal))
            {
                List<CharacterSkillSemanticsCatalogEntryDto> candidates =
                    BuildCatalogCandidates(module, variant, settings);
                foreach (IGrouping<string, CharacterSkillSemanticsCatalogEntryDto> behavior in
                         candidates.GroupBy(CatalogBehaviorKey, StringComparer.Ordinal))
                {
                    entries.Add(behavior
                        .OrderBy(value => value.contextId, StringComparer.Ordinal)
                        .First());
                }
            }
        }

        entries = entries.OrderBy(value => value.moduleId, StringComparer.Ordinal)
            .ThenBy(value => value.variantId, StringComparer.Ordinal)
            .ThenBy(value => value.contextId, StringComparer.Ordinal)
            .ToList();
        if (entries.Select(value => value.contextId)
                .Distinct(StringComparer.Ordinal).Count() != entries.Count)
        {
            throw new InvalidOperationException(
                "CharacterSkill catalog semantics contain duplicate contextId values.");
        }
        string[] expected = settings.Modules
            .SelectMany(module => module.variants.Select(variant => module.id + "|" + variant.id))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] covered = entries.Select(value => value.moduleId + "|" + value.variantId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!covered.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "CharacterSkill catalog semantics do not cover every authored module/variant.");
        }
        string[] scopes = entries
            .SelectMany(value => value.semantics.modules)
            .Select(value => value.executionScope)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!scopes.SequenceEqual(ScopeTokens, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"CharacterSkill catalog semantics scopes '{string.Join(",", scopes)}' "
                + $"do not cover '{string.Join(",", ScopeTokens)}'.");
        }
        return entries.AsReadOnly();
    }

    public static string GetSemanticKey(
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillModuleSemanticsDto module)
    {
        if (rule == null) throw new ArgumentNullException(nameof(rule));
        ValidateModuleShape(module);
        return string.Join("|",
            kind,
            rule.trigger,
            rule.target,
            rule.ultimateDomain,
            module.moduleId,
            module.variantId,
            module.executionScope);
    }

    public static string SerializeCanonical(CharacterSkillCombinationSemanticsDto semantics)
    {
        ValidateDtoShape(semantics);
        StringBuilder builder = new StringBuilder(512);
        builder.Append('{');
        WriteProperty(builder, "combinationId", semantics.combinationId);
        builder.Append(',');
        WriteProperty(builder, "descriptionKo", semantics.descriptionKo);
        builder.Append(",\"modules\":[");
        for (int index = 0; index < semantics.modules.Count; index++)
        {
            if (index > 0) builder.Append(',');
            WriteModule(builder, semantics.modules[index]);
        }
        builder.Append("],\"schemaVersion\":");
        builder.Append(semantics.schemaVersion.ToString(CultureInfo.InvariantCulture));
        builder.Append('}');
        return builder.ToString();
    }

    public static string SerializeModuleCanonical(CharacterSkillModuleSemanticsDto module)
    {
        ValidateModuleShape(module);
        StringBuilder builder = new StringBuilder(256);
        WriteModule(builder, module);
        return builder.ToString();
    }

    public static bool TryValidateExact(
        CharacterSkillCombinationSemanticsDto supplied,
        CharacterSkillAllowedCombination combination,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillSystemSettingsSO settings,
        out string error)
    {
        try
        {
            string expected = SerializeCanonical(Create(combination, rule, kind, settings));
            string actual = SerializeCanonical(supplied);
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                error = "Public CharacterSkill semantics do not exactly match runtime authority.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static bool TryValidateCanonicalJson(
        string suppliedCanonicalJson,
        CharacterSkillAllowedCombination combination,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillSystemSettingsSO settings,
        out string error)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(suppliedCanonicalJson))
            {
                error = "Canonical CharacterSkill semantics JSON is required.";
                return false;
            }
            string expected = SerializeCanonical(Create(combination, rule, kind, settings));
            if (!string.Equals(expected, suppliedCanonicalJson, StringComparison.Ordinal))
            {
                error = "Canonical CharacterSkill semantics JSON does not exactly match runtime authority.";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static void ValidateSettingsCoverage(CharacterSkillSystemSettingsSO settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        CharacterSkillModuleRule[] modules = (settings.Modules
                ?? Array.Empty<CharacterSkillModuleRule>())
            .ToArray();
        if (modules.Length == 0)
        {
            throw new InvalidOperationException(
                "CharacterSkill semantics require at least one authored module.");
        }

        HashSet<string> seenModules = new HashSet<string>(StringComparer.Ordinal);
        foreach (CharacterSkillModuleRule module in modules)
        {
            if (module == null)
            {
                throw new InvalidOperationException(
                    "CharacterSkill semantics cannot map a null authored module.");
            }
            RequireCanonicalIdentifier(module.id, "moduleId");
            if (!seenModules.Add(module.id))
            {
                throw new InvalidOperationException(
                    $"CharacterSkill semantics found duplicate moduleId '{module.id}'.");
            }
            CharacterSkillModuleCapabilityRegistry.Require(module);
            if (string.IsNullOrWhiteSpace(module.displayName))
            {
                throw new InvalidOperationException(
                    $"CharacterSkill module '{module.id}' has a blank display name.");
            }

            CharacterSkillNumericVariant[] variants = (module.variants
                    ?? new List<CharacterSkillNumericVariant>())
                .ToArray();
            if (variants.Length == 0)
            {
                throw new InvalidOperationException(
                    $"CharacterSkill module '{module.id}' requires at least one authored variant.");
            }

            HashSet<string> seenVariants = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterSkillNumericVariant variant in variants)
            {
                if (variant == null)
                {
                    throw new InvalidOperationException(
                        $"CharacterSkill module '{module.id}' contains a null variant.");
                }
                RequireCanonicalIdentifier(variant.id, "variantId");
                if (!seenVariants.Add(variant.id))
                {
                    throw new InvalidOperationException(
                        $"CharacterSkill module '{module.id}' has duplicate variantId '{variant.id}'.");
                }
                if (string.IsNullOrWhiteSpace(variant.displayName))
                {
                    throw new InvalidOperationException(
                        $"CharacterSkill variant '{module.id}|{variant.id}' has a blank display name.");
                }
                RequireFinite(variant.primaryValue, module.id, variant.id, "primaryValue");
                RequireFinite(variant.secondaryValue, module.id, variant.id, "secondaryValue");
            }
        }
    }

    public static string ExecutionScopeToken(CharacterSkillSemanticsExecutionScope scope)
    {
        return scope switch
        {
            CharacterSkillSemanticsExecutionScope.OffenseBattle => "offense_battle",
            CharacterSkillSemanticsExecutionScope.OutsidePassive => "outside_passive",
            CharacterSkillSemanticsExecutionScope.DefenseUltimate => "defense_ultimate",
            CharacterSkillSemanticsExecutionScope.ManagementEvent => "management_event",
            CharacterSkillSemanticsExecutionScope.ManagementQuery => "management_query",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };
    }

    public static string EffectKindToken(CharacterSkillSemanticsEffectKind kind)
    {
        return kind switch
        {
            CharacterSkillSemanticsEffectKind.NoOp => "no_op",
            CharacterSkillSemanticsEffectKind.BasicDamage => "basic_damage",
            CharacterSkillSemanticsEffectKind.FlatHealAndDrain => "flat_heal_and_drain",
            CharacterSkillSemanticsEffectKind.GuardStatus => "guard_status",
            CharacterSkillSemanticsEffectKind.DamageOverTimeStatus => "damage_over_time_status",
            CharacterSkillSemanticsEffectKind.VulnerabilityStatus => "vulnerability_status",
            CharacterSkillSemanticsEffectKind.InitiativeDelay => "initiative_delay",
            CharacterSkillSemanticsEffectKind.AttackModifier => "attack_modifier",
            CharacterSkillSemanticsEffectKind.StatusCleanse => "status_cleanse",
            CharacterSkillSemanticsEffectKind.FormationReposition => "formation_reposition",
            CharacterSkillSemanticsEffectKind.MultiTargetDamage => "multi_target_damage",
            CharacterSkillSemanticsEffectKind.ConditionalBasicDamage => "conditional_basic_damage",
            CharacterSkillSemanticsEffectKind.CooldownAdjustment => "cooldown_adjustment",
            CharacterSkillSemanticsEffectKind.DirectHealthDamage => "direct_health_damage",
            CharacterSkillSemanticsEffectKind.CasterHeal => "caster_heal",
            CharacterSkillSemanticsEffectKind.ProtectionMood => "protection_mood",
            CharacterSkillSemanticsEffectKind.InjuryMoodCleanse => "injury_mood_cleanse",
            CharacterSkillSemanticsEffectKind.DefenseDamageContribution => "defense_damage_contribution",
            CharacterSkillSemanticsEffectKind.WorkSpeedMultiplier => "work_speed_multiplier",
            CharacterSkillSemanticsEffectKind.ProductionOutputMultiplier => "production_output_multiplier",
            CharacterSkillSemanticsEffectKind.HygieneRestore => "hygiene_restore",
            CharacterSkillSemanticsEffectKind.CleaningSpeedMultiplier => "cleaning_speed_multiplier",
            CharacterSkillSemanticsEffectKind.RepairSpeedMultiplier => "repair_speed_multiplier",
            CharacterSkillSemanticsEffectKind.StockOutputBonus => "stock_output_bonus",
            CharacterSkillSemanticsEffectKind.ResearchWorkBonus => "research_work_bonus",
            CharacterSkillSemanticsEffectKind.LowestNeedRestore => "lowest_need_restore",
            CharacterSkillSemanticsEffectKind.MoodFactor => "mood_factor",
            CharacterSkillSemanticsEffectKind.PositiveRelationshipBonus => "positive_relationship_bonus",
            CharacterSkillSemanticsEffectKind.RevenueMultiplier => "revenue_multiplier",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
    }

    private static List<CharacterSkillSemanticsCatalogEntryDto> BuildCatalogCandidates(
        CharacterSkillModuleRule module,
        CharacterSkillNumericVariant variant,
        CharacterSkillSystemSettingsSO settings)
    {
        List<CharacterSkillSemanticsCatalogEntryDto> candidates =
            new List<CharacterSkillSemanticsCatalogEntryDto>();
        if (module is CharacterCombatSkillModuleRule)
        {
            AddCatalogCandidate(
                candidates,
                module,
                variant,
                settings,
                CharacterSkillKind.Active,
                CharacterSkillTrigger.ManualCombat,
                CharacterUltimateDomain.None);
            foreach (CharacterSkillTrigger trigger in CatalogAllowedTriggers(module))
            {
                AddCatalogCandidate(
                    candidates,
                    module,
                    variant,
                    settings,
                    CharacterSkillKind.Passive,
                    trigger,
                    CharacterUltimateDomain.None);
            }
            AddCatalogCandidate(
                candidates,
                module,
                variant,
                settings,
                CharacterSkillKind.Ultimate,
                CharacterSkillTrigger.ManualCombat,
                CharacterUltimateDomain.Offense);
            AddCatalogCandidate(
                candidates,
                module,
                variant,
                settings,
                CharacterSkillKind.Ultimate,
                CharacterSkillTrigger.InvasionStarted,
                CharacterUltimateDomain.Defense);
        }
        else if (module is CharacterManagementSkillModuleRule)
        {
            if (module.Allows(
                    CharacterSkillKind.Active,
                    CharacterSkillTrigger.ManualWork,
                    CharacterSkillTarget.Self))
            {
                AddCatalogCandidate(
                    candidates,
                    module,
                    variant,
                    settings,
                    CharacterSkillKind.Active,
                    CharacterSkillTrigger.ManualWork,
                    CharacterUltimateDomain.None);
            }
            foreach (CharacterSkillTrigger trigger in CatalogAllowedTriggers(module)
                         .Where(value => !CharacterSkillValidation.WouldSelfTrigger(
                              module,
                              value))
                         .Where(value => CharacterSkillRuntimeEffects.IsManagementModuleReachable(
                             CharacterSkillKind.Passive,
                             value,
                             module)))
            {
                AddCatalogCandidate(
                    candidates,
                    module,
                    variant,
                    settings,
                    CharacterSkillKind.Passive,
                    trigger,
                    CharacterUltimateDomain.None);
            }
            CharacterSkillTrigger? ultimateTrigger = CatalogAllowedTriggers(module)
                .Where(value => !CharacterSkillValidation.WouldSelfTrigger(module, value))
                .Where(value => CharacterSkillRuntimeEffects.IsManagementModuleReachable(
                    CharacterSkillKind.Ultimate,
                    value,
                    module))
                .Where(value => CatalogTarget(
                    module,
                    CharacterSkillKind.Ultimate,
                    value).HasValue)
                .Select(value => (CharacterSkillTrigger?)value)
                .FirstOrDefault();
            if (!ultimateTrigger.HasValue)
            {
                throw new InvalidOperationException(
                    $"CharacterSkill management module '{module.id}' has no authored "
                    + "ultimate trigger with a legal catalog target.");
            }
            AddCatalogCandidate(
                candidates,
                module,
                variant,
                settings,
                CharacterSkillKind.Ultimate,
                ultimateTrigger.Value,
                CharacterUltimateDomain.Management);
        }
        else
        {
            throw new InvalidOperationException(
                $"CharacterSkill catalog semantics cannot classify module '{module.id}'.");
        }

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"CharacterSkill catalog semantics found no applicable context for "
                + $"'{module.id}|{variant.id}'.");
        }
        return candidates;
    }

    private static IEnumerable<CharacterSkillTrigger> CatalogAllowedTriggers(
        CharacterSkillModuleRule module)
    {
        return (module.allowedTriggers != null && module.allowedTriggers.Count > 0
                ? module.allowedTriggers
                : Enum.GetValues(typeof(CharacterSkillTrigger)).Cast<CharacterSkillTrigger>())
            .Distinct()
            .OrderBy(value => value);
    }

    private static void AddCatalogCandidate(
        ICollection<CharacterSkillSemanticsCatalogEntryDto> destination,
        CharacterSkillModuleRule module,
        CharacterSkillNumericVariant variant,
        CharacterSkillSystemSettingsSO settings,
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger,
        CharacterUltimateDomain domain)
    {
        CharacterSkillTarget? target = CatalogTarget(module, kind, trigger);
        if (!target.HasValue)
        {
            return;
        }

        CharacterSkillCandidateRule rule = new CharacterSkillCandidateRule
        {
            ruleId = string.Join(":",
                "character-skill-semantics",
                Snake(module.id),
                Snake(variant.id),
                Snake(kind.ToString()),
                Snake(trigger.ToString()),
                Snake(target.Value.ToString()),
                Snake(domain.ToString())),
            rarity = CharacterSkillRarity.Common,
            budget = variant.cost,
            trigger = trigger,
            target = target.Value,
            ultimateDomain = domain,
            cooldownTurns = 0,
            mechanicalPolicySource = CharacterSkillMechanicalPolicySource.AuthoredRule,
            allowedModuleIds = new List<string> { module.id },
            allowedVariantIds = new List<string> { variant.id }
        };
        CharacterSkillFormationRules.Resolve(
            rule.target,
            Array.Empty<CharacterSkillModuleSelection>(),
            out rule.usableFrom,
            out rule.targetPositions);
        CharacterSkillAllowedCombination[] combinations = CharacterSkillCombinationCatalog
            .Build(rule, settings, kind)
            .ToArray();
        if (combinations.Length != 1)
        {
            throw new InvalidOperationException(
                $"CharacterSkill catalog context '{rule.ruleId}' produced "
                + $"{combinations.Length} combinations instead of exactly one.");
        }

        CharacterSkillCombinationSemanticsDto semantics = Create(
            combinations[0],
            rule,
            kind,
            settings);
        destination.Add(new CharacterSkillSemanticsCatalogEntryDto
        {
            applicability = CatalogEntryApplicability,
            contextId = rule.ruleId,
            kind = kind.ToString(),
            moduleId = module.id,
            semantics = semantics,
            target = rule.target.ToString(),
            trigger = trigger.ToString(),
            ultimateDomain = domain.ToString(),
            variantId = variant.id
        });
    }

    private static CharacterSkillTarget? CatalogTarget(
        CharacterSkillModuleRule module,
        CharacterSkillKind kind,
        CharacterSkillTrigger trigger)
    {
        IEnumerable<CharacterSkillTarget> targets = module.allowedTargets != null
            && module.allowedTargets.Count > 0
                ? module.allowedTargets
                : Enum.GetValues(typeof(CharacterSkillTarget)).Cast<CharacterSkillTarget>();
        return targets.Distinct()
            .OrderBy(value => value)
            .Where(value => module.Allows(kind, trigger, value))
            .Where(value => CharacterSkillValidation.IsTargetCompatible(module, value))
            .Select(value => (CharacterSkillTarget?)value)
            .FirstOrDefault();
    }

    private static string CatalogBehaviorKey(CharacterSkillSemanticsCatalogEntryDto entry)
    {
        string contextClass = string.Equals(
                entry.kind,
                CharacterSkillKind.Ultimate.ToString(),
                StringComparison.Ordinal)
            ? entry.kind + ":" + entry.ultimateDomain
            : entry.kind;
        return contextClass + "|" + string.Join(",", entry.semantics.modules.Select(value =>
            SerializeModuleCanonical(value)));
    }

    private static void ValidateCombinationAuthority(
        CharacterSkillAllowedCombination combination,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillSystemSettingsSO settings)
    {
        if (kind != CharacterSkillKind.Active
            && kind != CharacterSkillKind.Passive
            && kind != CharacterSkillKind.Ultimate)
        {
            throw new InvalidOperationException(
                $"CharacterSkill semantics do not support generated kind '{kind}'.");
        }
        if (string.IsNullOrWhiteSpace(rule.ruleId)
            || !string.Equals(rule.ruleId, rule.ruleId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("CharacterSkill semantics require a canonical nonblank ruleId.");
        }
        if (!string.Equals(combination.RuleId, rule.ruleId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Combination '{combination.Id}' does not belong to rule '{rule.ruleId}'.");
        }
        if (string.IsNullOrWhiteSpace(combination.Id)
            || combination.Modules == null
            || combination.Modules.Count == 0)
        {
            throw new InvalidOperationException(
                "CharacterSkill semantics require a nonblank combinationId and at least one module.");
        }

        string[] selections = combination.Modules.Select(value =>
        {
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"Combination '{combination.Id}' contains a null module selection.");
            }
            RequireCanonicalIdentifier(value.moduleId, "moduleId");
            RequireCanonicalIdentifier(value.variantId, "variantId");
            return value.moduleId + "|" + value.variantId;
        }).ToArray();
        string[] canonical = combination.Modules
            .OrderBy(value => value.moduleId, StringComparer.Ordinal)
            .ThenBy(value => value.variantId, StringComparer.Ordinal)
            .Select(value => value.moduleId + "|" + value.variantId)
            .ToArray();
        if (!selections.SequenceEqual(canonical, StringComparer.Ordinal)
            || combination.Modules.Select(value => value.moduleId)
                .Distinct(StringComparer.Ordinal).Count() != combination.Modules.Count)
        {
            throw new InvalidOperationException(
                $"Combination '{combination.Id}' has duplicate or noncanonical module selections.");
        }

        CharacterSkillAllowedCombination[] matches = CharacterSkillCombinationCatalog
            .Build(rule, settings, kind)
            .Where(value => string.Equals(value.Id, combination.Id, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Combination '{combination.Id}' is not a unique current legal option for rule '{rule.ruleId}'.");
        }
        CharacterSkillAllowedCombination legal = matches[0];
        if (legal.Cost != combination.Cost
            || !string.Equals(legal.Signature, combination.Signature, StringComparison.Ordinal)
            || !string.Equals(legal.MechanicalIdentity, combination.MechanicalIdentity,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Combination '{combination.Id}' does not exactly match current mechanical authority.");
        }
    }

    private static IEnumerable<CharacterSkillSemanticsExecutionScope> ResolveScopes(
        CharacterSkillKind kind,
        CharacterSkillCandidateRule rule,
        CharacterSkillModuleRule module)
    {
        bool combat = module is CharacterCombatSkillModuleRule;
        bool management = module is CharacterManagementSkillModuleRule;
        if (combat == management)
        {
            throw new InvalidOperationException(
                $"CharacterSkill module '{module.id}' has an unsupported runtime module class.");
        }

        if (kind == CharacterSkillKind.Active)
        {
            if (combat)
                return new[] { CharacterSkillSemanticsExecutionScope.OffenseBattle };
            if (management && rule.trigger == CharacterSkillTrigger.ManualWork)
                return new[]
                {
                    CharacterSkillSemanticsExecutionScope.ManagementEvent,
                    CharacterSkillSemanticsExecutionScope.ManagementQuery
                };
            throw DomainMismatch(module.id, kind, rule.ultimateDomain);
        }
        if (kind == CharacterSkillKind.Passive)
        {
            return combat
                ? new[]
                {
                    CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsExecutionScope.OutsidePassive
                }
                : new[]
                {
                    CharacterSkillSemanticsExecutionScope.ManagementEvent,
                    CharacterSkillSemanticsExecutionScope.ManagementQuery
                };
        }
        if (kind != CharacterSkillKind.Ultimate)
        {
            throw new InvalidOperationException($"Unsupported generated CharacterSkill kind '{kind}'.");
        }

        return rule.ultimateDomain switch
        {
            CharacterUltimateDomain.Offense when combat =>
                new[] { CharacterSkillSemanticsExecutionScope.OffenseBattle },
            CharacterUltimateDomain.Defense when combat =>
                new[] { CharacterSkillSemanticsExecutionScope.DefenseUltimate },
            CharacterUltimateDomain.Management when management => new[]
            {
                CharacterSkillSemanticsExecutionScope.ManagementEvent,
                CharacterSkillSemanticsExecutionScope.ManagementQuery
            },
            _ => throw DomainMismatch(module.id, kind, rule.ultimateDomain)
        };
    }

    private static CharacterSkillModuleSemanticsDto CreateModuleSemantics(
        CharacterSkillModuleSelection selection,
        CharacterSkillNumericVariant variant,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        CharacterSkillSemanticsExecutionScope scope,
        string capabilityId)
    {
        return scope switch
        {
            CharacterSkillSemanticsExecutionScope.OffenseBattle =>
                CreateOffenseBattle(selection, variant, capabilityId),
            CharacterSkillSemanticsExecutionScope.OutsidePassive =>
                CreateOutsidePassive(selection, variant, capabilityId),
            CharacterSkillSemanticsExecutionScope.DefenseUltimate =>
                CreateDefenseUltimate(selection, variant, capabilityId),
            CharacterSkillSemanticsExecutionScope.ManagementEvent =>
                CreateManagementEvent(selection, variant, kind, capabilityId),
            CharacterSkillSemanticsExecutionScope.ManagementQuery =>
                CreateManagementQuery(selection, variant, rule, kind, capabilityId),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };
    }

    private static CharacterSkillModuleSemanticsDto CreateOffenseBattle(
        CharacterSkillModuleSelection selection,
        CharacterSkillNumericVariant variant,
        string capabilityId)
    {
        string p = Decimal(variant.primaryValue);
        string s = Decimal(variant.secondaryValue);
        string duration = variant.duration.ToString(CultureInfo.InvariantCulture);
        string count = variant.count.ToString(CultureInfo.InvariantCulture);
        switch (capabilityId)
        {
            case "damage":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.BasicDamage,
                    Terms("damage_formula=calculate_basic_damage*" + p + "+" + s,
                        "hit_count=" + count),
                    $"선택 대상에게 기본 피해×{p}+{s}를 {count}회 적용한다.");
            case "heal":
                string healDescription = $"선택 대상의 체력을 {p} 회복한다.";
                List<string> healTerms = Terms("selected_target_flat_heal=" + p);
                if (variant.secondaryValue > 0f)
                {
                    healTerms.Add("caster_heal_ratio_of_prior_actual_damage=" + s);
                    healTerms.Sort(StringComparer.Ordinal);
                    healDescription += $" 이 모듈보다 먼저 같은 대상 실행 문맥에서 실제로 준 피해의 {s}배만큼 시전자를 회복한다.";
                }
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.FlatHealAndDrain, healTerms, healDescription);
            case "guard":
            case "protect":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.GuardStatus,
                    Terms("selected_target_damage_reduction_ratio=" + p,
                        "turns=" + duration),
                    $"선택 대상에게 {duration}턴 동안 받는 피해를 {p} 비율만큼 줄이는 방어 상태를 부여한다.");
            case "dot":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.DamageOverTimeStatus,
                    Terms("damage_per_turn=" + p, "turns=" + duration),
                    $"선택 대상에게 턴마다 {p} 피해를 주는 상태를 {duration}턴 부여한다.");
            case "vulnerability":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.VulnerabilityStatus,
                    Terms("incoming_damage_increase_ratio=" + p, "turns=" + duration),
                    $"선택 대상이 받는 피해를 {p} 비율만큼 늘리는 취약 상태를 {duration}턴 부여한다.");
            case "delay":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.InitiativeDelay,
                    Terms("initiative_penalty=" + p),
                    $"선택 대상의 행동 순서 수치에 {p}의 지연 패널티를 더한다.");
            case "buff":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.AttackModifier,
                    Terms("attack_modifier_delta=" + p, "movement_speed_change=none",
                        "turns=" + duration),
                    $"선택 대상의 공격 배율에 +{p}를 {duration}턴 부여한다. 이동 속도는 바꾸지 않는다.");
            case "debuff":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.AttackModifier,
                    Terms("attack_modifier_delta=-" + Decimal(Math.Abs(variant.primaryValue)),
                        "movement_speed_change=none", "turns=" + duration),
                    $"선택 대상의 공격 배율에 -{p}를 {duration}턴 부여한다. 이동 속도는 바꾸지 않는다.");
            case "cleanse":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.StatusCleanse,
                    Terms("maximum_removed_statuses=" + count,
                        "removable_statuses=vulnerability,damage_over_time,sedated,mana_blocked,negative_attack_modifier"),
                    $"선택 대상에게서 취약·지속 피해·진정·마나 차단·공격 감소 상태를 최대 {count}개 제거한다.");
            case "reposition":
                int offset = Mathf.RoundToInt(variant.primaryValue);
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.FormationReposition,
                    Terms("formation_bounds=0..2", "rearward_offset=" + offset),
                    $"선택 대상을 후방 방향으로 {offset}칸 이동시키며 진형 행은 0~2 범위로 제한한다.");
            case "multi_target":
                int additional = Math.Max(1, variant.count - 1);
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.MultiTargetDamage,
                    Terms("additional_target_damage_multiplier=0.55",
                        "additional_target_limit=" + additional,
                        "additional_targets=selected_target_living_team_excluding_selected"),
                    $"선택 대상과 같은 팀의 살아 있는 다른 대상 중 최대 {additional}명에게 각각 기본 피해의 0.55배를 준다.");
            case "conditional_amplify":
                string thresholdPercent = Decimal(variant.secondaryValue * 100f);
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.ConditionalBasicDamage,
                    Terms("amplified_effect=separate_basic_damage",
                        "caster_health_relevance=ignored", "comparison=less_than_or_equal",
                        "execution_order=alphabetical_module_id_before_later_damage",
                        "health_ratio_formula=current_health/max(1,max_health)",
                        "health_ratio_subject=selected_target",
                        "health_ratio_threshold=" + s,
                        "separate_basic_damage_multiplier=" + p),
                    $"선택 대상의 현재 체력/max(1, 최대 체력)이 {thresholdPercent}%(비율 {s}) 이하일 때(같음 포함) 기본 피해의 {p}배를 별도 피해로 준다. 시전자 체력은 무관하며 알파벳 모듈 순서상 뒤의 damage보다 먼저 실행된다.");
            case "cooldown_adjust":
                int delta = Mathf.RoundToInt(variant.primaryValue);
                if (delta <= -99)
                {
                    return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                        CharacterSkillSemanticsEffectKind.CooldownAdjustment,
                        Terms("affected_entries=all_existing_target_cooldowns",
                            "result=reset_to_zero", "turn_delta_threshold=-99"),
                        "선택 대상에게 이미 존재하는 모든 재사용 대기 항목을 0으로 초기화한다.");
                }
                return Module(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle,
                    CharacterSkillSemanticsEffectKind.CooldownAdjustment,
                    Terms("affected_entries=all_existing_target_cooldowns",
                        "minimum_turns=0", "turn_delta=" + delta),
                    $"선택 대상에게 이미 존재하는 모든 재사용 대기 항목을 {Math.Abs(delta)}턴 줄이며 0 아래로 내려가지 않는다.");
            default:
                throw MissingMapping(selection, CharacterSkillSemanticsExecutionScope.OffenseBattle);
        }
    }

    private static CharacterSkillModuleSemanticsDto CreateOutsidePassive(
        CharacterSkillModuleSelection selection,
        CharacterSkillNumericVariant variant,
        string capabilityId)
    {
        string p = Decimal(variant.primaryValue);
        switch (capabilityId)
        {
            case "damage":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OutsidePassive,
                    CharacterSkillSemanticsEffectKind.DirectHealthDamage,
                    Terms("damage=max(1," + p + ")", "missing_target_actor=no_op",
                        "recipient=context_target_actor"),
                    $"전투 세션 밖에서는 문맥 대상 캐릭터가 있을 때만 그 대상에게 max(1, {p})의 직접 피해를 주며, 대상 캐릭터가 없으면 효과가 없다.");
            case "heal":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OutsidePassive,
                    CharacterSkillSemanticsEffectKind.CasterHeal,
                    Terms("caster_heal=max(1," + p + ")", "drain_component=none"),
                    $"전투 세션 밖에서는 시전자의 체력을 max(1, {p})만큼 회복하며 피해 흡수 회복은 적용하지 않는다.");
            case "guard":
            case "protect":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OutsidePassive,
                    CharacterSkillSemanticsEffectKind.ProtectionMood,
                    Terms("authored_duration_ignored=true", "authored_primary_ignored=true",
                        "caster_mood_factor=3", "duration_seconds=180", "maximum_stacks=1"),
                    "전투 세션 밖에서는 작성 수치와 무관하게 시전자에게 180초 동안 +3 보호 기분 요인을 최대 1중첩 적용한다.");
            case "cleanse":
                return Module(selection, CharacterSkillSemanticsExecutionScope.OutsidePassive,
                    CharacterSkillSemanticsEffectKind.InjuryMoodCleanse,
                    Terms("removed_caster_mood_factor_id=health:injury"),
                    "전투 세션 밖에서는 시전자의 health:injury 기분 요인 하나를 제거한다.");
            case "dot":
            case "vulnerability":
            case "delay":
            case "buff":
            case "debuff":
            case "reposition":
            case "multi_target":
            case "conditional_amplify":
            case "cooldown_adjust":
                return NoOp(selection, CharacterSkillSemanticsExecutionScope.OutsidePassive,
                    "apply_outside_combat_branch_absent",
                    "전투 세션 밖의 패시브 실행 경로에는 이 모듈 처리 분기가 없어 효과가 없다.");
            default:
                throw MissingMapping(selection, CharacterSkillSemanticsExecutionScope.OutsidePassive);
        }
    }

    private static CharacterSkillModuleSemanticsDto CreateDefenseUltimate(
        CharacterSkillModuleSelection selection,
        CharacterSkillNumericVariant variant,
        string capabilityId)
    {
        string p = Decimal(variant.primaryValue);
        string duration = variant.duration.ToString(CultureInfo.InvariantCulture);
        const string attack = "max(1,5*defender_melee_power_performance)";
        switch (capabilityId)
        {
            case "damage":
                return Module(selection, CharacterSkillSemanticsExecutionScope.DefenseUltimate,
                    CharacterSkillSemanticsEffectKind.DefenseDamageContribution,
                    Terms("damage_contribution=defender_attack*max(0," + p + ")",
                        "defender_attack_formula=" + attack,
                        "outside_damage_target=none"),
                    $"방어 궁극기 피해 합계에 방어자 공격력×{p}를 더한다. 방어자 공격력은 max(1, 5×근접 전투 성능)이며 별도의 외부 대상 피해는 없다.");
            case "dot":
                return Module(selection, CharacterSkillSemanticsExecutionScope.DefenseUltimate,
                    CharacterSkillSemanticsEffectKind.DefenseDamageContribution,
                    Terms("damage_contribution=max(0," + p + ")*max(1," + duration + ")",
                        "damage_over_time_status=none"),
                    $"지속 피해 상태를 만들지 않고 방어 궁극기 피해 합계에 {p}×max(1, {duration})를 더한다.");
            case "conditional_amplify":
                return Module(selection, CharacterSkillSemanticsExecutionScope.DefenseUltimate,
                    CharacterSkillSemanticsEffectKind.DefenseDamageContribution,
                    Terms("contribution_is_unconditional=true",
                        "damage_contribution=defender_attack*max(0," + p + ")*0.5",
                        "defender_attack_formula=" + attack, "health_check=none"),
                    $"현재 체력이나 임계값을 검사하지 않고 방어 궁극기 피해 합계에 방어자 공격력×{p}×0.5를 항상 더한다.");
            case "heal":
                return Module(selection, CharacterSkillSemanticsExecutionScope.DefenseUltimate,
                    CharacterSkillSemanticsEffectKind.CasterHeal,
                    Terms("defender_heal=max(1," + p + ")", "drain_component=none"),
                    $"침입자 피해 계산 전에 방어자의 체력을 max(1, {p})만큼 회복하며 피해 흡수 회복은 적용하지 않는다.");
            case "guard":
            case "protect":
                return Module(selection, CharacterSkillSemanticsExecutionScope.DefenseUltimate,
                    CharacterSkillSemanticsEffectKind.ProtectionMood,
                    Terms("authored_duration_ignored=true", "authored_primary_ignored=true",
                        "defender_mood_factor=3", "duration_seconds=180", "maximum_stacks=1"),
                    "작성 수치와 무관하게 방어자에게 180초 동안 +3 보호 기분 요인을 최대 1중첩 적용하며 피해 합계에는 기여하지 않는다.");
            case "cleanse":
                return Module(selection, CharacterSkillSemanticsExecutionScope.DefenseUltimate,
                    CharacterSkillSemanticsEffectKind.InjuryMoodCleanse,
                    Terms("damage_contribution=none",
                        "removed_defender_mood_factor_id=health:injury"),
                    "방어자의 health:injury 기분 요인 하나를 제거하며 피해 합계에는 기여하지 않는다.");
            case "vulnerability":
            case "delay":
            case "buff":
            case "debuff":
            case "reposition":
            case "multi_target":
            case "cooldown_adjust":
                return NoOp(selection, CharacterSkillSemanticsExecutionScope.DefenseUltimate,
                    "defense_ultimate_branch_absent",
                    "현재 방어 궁극기 실행기에는 이 모듈의 상태·행동·피해 처리 분기가 없어 효과가 없다.");
            default:
                throw MissingMapping(selection, CharacterSkillSemanticsExecutionScope.DefenseUltimate);
        }
    }

    private static CharacterSkillModuleSemanticsDto CreateManagementEvent(
        CharacterSkillModuleSelection selection,
        CharacterSkillNumericVariant variant,
        CharacterSkillKind kind,
        string capabilityId)
    {
        string p = Decimal(variant.primaryValue);
        string activation = kind == CharacterSkillKind.Ultimate
            ? "operating_day_automatic_ultimate"
            : "matching_passive_trigger";
        switch (capabilityId)
        {
            case "cleaning":
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementEvent,
                    CharacterSkillSemanticsEffectKind.HygieneRestore,
                    Terms("activation=" + activation, "caster_hygiene_delta=" + p),
                    $"{ManagementActivationKo(kind)} 시전자의 위생 수치를 {p}만큼 올린다.");
            case "needs":
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementEvent,
                    CharacterSkillSemanticsEffectKind.LowestNeedRestore,
                    Terms("activation=" + activation,
                        "amount=max(0," + p + ")",
                        "recipient=caster",
                        "selected_need=lowest_of_hunger,sleep,fun,excretion,hygiene"),
                    $"{ManagementActivationKo(kind)} 시전자의 허기·수면·재미·배설·위생 중 가장 낮은 수치를 max(0, {p})만큼 올린다.");
            case "mood":
                int seconds = Math.Max(30, variant.duration);
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementEvent,
                    CharacterSkillSemanticsEffectKind.MoodFactor,
                    Terms("activation=" + activation, "caster_mood_factor=" + p,
                        "duration_seconds=" + seconds, "maximum_stacks=1"),
                    $"{ManagementActivationKo(kind)} 시전자에게 {seconds}초 동안 +{p} 기분 요인을 최대 1중첩 적용한다.");
            case "work_speed":
            case "output":
            case "repair":
            case "stock":
            case "research":
            case "relationship":
            case "revenue":
                return NoOp(selection, CharacterSkillSemanticsExecutionScope.ManagementEvent,
                    "apply_outside_combat_branch_absent",
                    "관리 이벤트의 즉시 실행 경로에는 이 모듈 처리 분기가 없어 즉시 효과가 없다.");
            default:
                throw MissingMapping(selection, CharacterSkillSemanticsExecutionScope.ManagementEvent);
        }
    }

    private static CharacterSkillModuleSemanticsDto CreateManagementQuery(
        CharacterSkillModuleSelection selection,
        CharacterSkillNumericVariant variant,
        CharacterSkillCandidateRule rule,
        CharacterSkillKind kind,
        string capabilityId)
    {
        bool ultimate = kind == CharacterSkillKind.Ultimate;
        string p = Decimal(variant.primaryValue);
        string availability = ultimate
            ? "after_first_management_ultimate_use"
            : "passive_trigger_" + Snake(rule.trigger.ToString());

        switch (capabilityId)
        {
            case "work_speed":
                bool workSpeedActive = ultimate
                    || rule.trigger == CharacterSkillTrigger.WorkStarted
                    || rule.trigger == CharacterSkillTrigger.WorkCompleted;
                if (!workSpeedActive)
                {
                    return QueryNoOp(selection, rule.trigger,
                        "work_speed_query_reads_only_work_started_or_work_completed");
                }
                int contributions = ultimate ? 2 : 1;
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
                    CharacterSkillSemanticsEffectKind.WorkSpeedMultiplier,
                    Terms("authored_bonus=" + p, "availability=" + availability,
                        "contributions_per_begin_work=" + contributions,
                        "multiplier_formula=1+clamp(total_bonus,0,1.5)",
                        "snapshot_time=begin_work"),
                    ultimate
                        ? $"관리 궁극기가 한 번 활성화된 뒤 작업 시작 시 {p} 보너스가 WorkStarted·WorkCompleted 조회에 각각 더해져 두 번 합산되고, 총합을 0~1.5로 제한한 뒤 1에 더한 작업 속도 배율을 작업 문맥에 저장한다."
                        : $"{TriggerKo(rule.trigger)} 패시브로 {p}를 작업 속도 보너스 총합에 한 번 더하고, 작업 시작 시 총합을 0~1.5로 제한한 뒤 1에 더한 배율을 작업 문맥에 저장한다.");
            case "output":
                if (!ultimate && rule.trigger != CharacterSkillTrigger.WorkCompleted)
                    return QueryNoOp(selection, rule.trigger,
                        "production_output_query_reads_only_work_completed");
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
                    CharacterSkillSemanticsEffectKind.ProductionOutputMultiplier,
                    Terms("authored_bonus=" + p, "availability=" + availability,
                        "multiplier_formula=1+clamp(total_bonus,0,3)"),
                    $"{ManagementQueryPrefixKo(ultimate, rule.trigger)} 생산량 보너스 총합에 {p}를 더하고, 총합을 0~3으로 제한해 1에 더한 생산량 배율로 사용한다.");
            case "cleaning":
                if (!ultimate && rule.trigger != CharacterSkillTrigger.WorkCompleted)
                    return QueryNoOp(selection, rule.trigger,
                        "cleaning_speed_query_reads_only_work_completed");
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
                    CharacterSkillSemanticsEffectKind.CleaningSpeedMultiplier,
                    Terms("authored_points=" + p, "availability=" + availability,
                        "multiplier_formula=1+clamp(total_points/100,0,2)"),
                    $"{ManagementQueryPrefixKo(ultimate, rule.trigger)} 청소 점수 총합에 {p}를 더하고, 총합/100을 0~2로 제한해 1에 더한 청소 속도 배율로 사용한다.");
            case "repair":
                if (!ultimate && rule.trigger != CharacterSkillTrigger.WorkCompleted)
                    return QueryNoOp(selection, rule.trigger,
                        "repair_speed_query_reads_only_work_completed");
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
                    CharacterSkillSemanticsEffectKind.RepairSpeedMultiplier,
                    Terms("authored_points=" + p, "availability=" + availability,
                        "multiplier_formula=1+clamp(total_points/100,0,2)"),
                    $"{ManagementQueryPrefixKo(ultimate, rule.trigger)} 수리 점수 총합에 {p}를 더하고, 총합/100을 0~2로 제한해 1에 더한 수리 속도 배율로 사용한다.");
            case "stock":
                if (!ultimate && rule.trigger != CharacterSkillTrigger.WorkCompleted)
                    return QueryNoOp(selection, rule.trigger,
                        "stock_output_query_reads_only_work_completed");
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
                    CharacterSkillSemanticsEffectKind.StockOutputBonus,
                    Terms("authored_bonus=" + p, "availability=" + availability,
                        "extra_count_formula=max(0,round_to_int(total_bonus))"),
                    $"{ManagementQueryPrefixKo(ultimate, rule.trigger)} 재고 보너스 총합에 {p}를 더하고 총합을 정수 반올림한 뒤 0 이상으로 제한한 추가 생산 개수로 사용한다.");
            case "research":
                if (!ultimate && rule.trigger != CharacterSkillTrigger.WorkCompleted)
                    return QueryNoOp(selection, rule.trigger,
                        "research_work_query_reads_only_work_completed");
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
                    CharacterSkillSemanticsEffectKind.ResearchWorkBonus,
                    Terms("authored_work_units_per_input_second=" + p,
                        "availability=" + availability,
                        "bonus_formula=max(0,input_seconds)*max(0,total_work_units_per_second)"),
                    $"{ManagementQueryPrefixKo(ultimate, rule.trigger)} 입력 작업 시간 1초마다 작성값 {p}가 포함된 총 연구 작업량을 더한다. 백분율 배율이 아니다.");
            case "relationship":
                if (!ultimate && rule.trigger != CharacterSkillTrigger.RelationshipChanged)
                    return QueryNoOp(selection, rule.trigger,
                        "relationship_query_reads_only_relationship_changed");
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
                    CharacterSkillSemanticsEffectKind.PositiveRelationshipBonus,
                    Terms("authored_points=" + p, "availability=" + availability,
                        "negative_or_zero_sentiment_bonus=none",
                        "positive_formula=clamp(sentiment+total_points/100,-1,1)"),
                    $"{ManagementQueryPrefixKo(ultimate, rule.trigger)} 원래 호감도가 양수일 때만 관계 점수 총합에 {p}를 더해 총합/100을 호감도에 더하고 -1~1로 제한한다. 0 이하 호감도에는 보너스를 적용하지 않는다.");
            case "revenue":
                if (!ultimate && rule.trigger != CharacterSkillTrigger.WorkCompleted)
                    return QueryNoOp(selection, rule.trigger,
                        "revenue_query_reads_only_work_completed");
                return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
                    CharacterSkillSemanticsEffectKind.RevenueMultiplier,
                    Terms("authored_bonus=" + p, "availability=" + availability,
                        "multiplier_formula=1+clamp(total_bonus,0,0.15)"),
                    $"{ManagementQueryPrefixKo(ultimate, rule.trigger)} 수익 보너스 총합에 {p}를 더하고 총합을 0~0.15로 제한해 1에 더한 수익 배율로 사용한다.");
            case "needs":
            case "mood":
                return NoOp(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
                    "management_query_consumer_absent",
                    "현재 관리 수치 조회 경로에는 이 모듈을 읽는 소비자가 없어 조회 효과가 없다.");
            default:
                throw MissingMapping(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery);
        }
    }

    private static CharacterSkillModuleSemanticsDto QueryNoOp(
        CharacterSkillModuleSelection selection,
        CharacterSkillTrigger trigger,
        string reason)
    {
        return Module(selection, CharacterSkillSemanticsExecutionScope.ManagementQuery,
            CharacterSkillSemanticsEffectKind.NoOp,
            Terms("configured_trigger=" + Snake(trigger.ToString()),
                "observable_effect=none", "reason=" + reason),
            $"이 패시브의 {TriggerKo(trigger)} 조건은 현재 이 모듈의 관리 조회 조건과 일치하지 않아 조회 효과가 없다.");
    }

    private static CharacterSkillModuleSemanticsDto NoOp(
        CharacterSkillModuleSelection selection,
        CharacterSkillSemanticsExecutionScope scope,
        string reason,
        string description)
    {
        return Module(selection, scope, CharacterSkillSemanticsEffectKind.NoOp,
            Terms("observable_effect=none", "reason=" + reason), description);
    }

    private static CharacterSkillModuleSemanticsDto Module(
        CharacterSkillModuleSelection selection,
        CharacterSkillSemanticsExecutionScope scope,
        CharacterSkillSemanticsEffectKind effectKind,
        List<string> terms,
        string description)
    {
        CharacterSkillModuleSemanticsDto result = new CharacterSkillModuleSemanticsDto
        {
            moduleId = selection.moduleId,
            variantId = selection.variantId,
            executionScope = ExecutionScopeToken(scope),
            effectKind = EffectKindToken(effectKind),
            terms = terms,
            descriptionKo = description
        };
        ValidateModuleShape(result);
        return result;
    }

    private static List<string> Terms(params string[] values)
    {
        List<string> terms = (values ?? Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        if (terms.Count == 0)
        {
            throw new InvalidOperationException(
                "CharacterSkill module semantics require at least one canonical term.");
        }
        return terms;
    }

    private static void ValidateDtoShape(CharacterSkillCombinationSemanticsDto semantics)
    {
        if (semantics == null) throw new ArgumentNullException(nameof(semantics));
        if (semantics.schemaVersion != SchemaVersion)
        {
            throw new InvalidOperationException(
                $"CharacterSkill semantics schemaVersion must be {SchemaVersion}.");
        }
        if (string.IsNullOrWhiteSpace(semantics.combinationId)
            || !string.Equals(semantics.combinationId, semantics.combinationId.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "CharacterSkill semantics combinationId must be canonical and nonblank.");
        }
        if (string.IsNullOrWhiteSpace(semantics.descriptionKo))
        {
            throw new InvalidOperationException(
                "CharacterSkill semantics descriptionKo is required.");
        }
        if (semantics.modules == null || semantics.modules.Count == 0
            || semantics.modules.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "CharacterSkill semantics modules must be a nonempty ordered list without nulls.");
        }
        foreach (CharacterSkillModuleSemanticsDto module in semantics.modules)
        {
            ValidateModuleShape(module);
        }
        string[] keys = semantics.modules.Select(value =>
                value.moduleId + "|" + value.variantId + "|" + value.executionScope)
            .ToArray();
        if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
        {
            throw new InvalidOperationException(
                "CharacterSkill semantics modules contain a duplicate module/variant/scope entry.");
        }
    }

    private static void ValidateModuleShape(CharacterSkillModuleSemanticsDto module)
    {
        if (module == null) throw new ArgumentNullException(nameof(module));
        RequireCanonicalIdentifier(module.moduleId, "moduleId");
        RequireCanonicalIdentifier(module.variantId, "variantId");
        if (!ScopeTokens.Contains(module.executionScope, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unknown CharacterSkill executionScope '{module.executionScope ?? string.Empty}'.");
        }
        if (!KindTokens.Contains(module.effectKind, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unknown CharacterSkill effectKind '{module.effectKind ?? string.Empty}'.");
        }
        if (string.IsNullOrWhiteSpace(module.descriptionKo))
        {
            throw new InvalidOperationException(
                $"CharacterSkill semantics '{module.moduleId}|{module.variantId}' has a blank descriptionKo.");
        }
        if (module.terms == null || module.terms.Count == 0)
        {
            throw new InvalidOperationException(
                $"CharacterSkill semantics '{module.moduleId}|{module.variantId}' has no terms.");
        }
        string[] sorted = module.terms.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!module.terms.SequenceEqual(sorted, StringComparer.Ordinal)
            || module.terms.Distinct(StringComparer.Ordinal).Count() != module.terms.Count)
        {
            throw new InvalidOperationException(
                $"CharacterSkill semantics '{module.moduleId}|{module.variantId}' terms must be unique and ordinal-sorted.");
        }
        foreach (string term in module.terms)
        {
            if (string.IsNullOrWhiteSpace(term)
                || !string.Equals(term, term.Trim(), StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"CharacterSkill semantics '{module.moduleId}|{module.variantId}' has a blank or noncanonical term.");
            }
            int equals = term.IndexOf('=');
            if (equals <= 0 || equals == term.Length - 1
                || term.IndexOf('=', equals + 1) >= 0)
            {
                throw new InvalidOperationException(
                    $"CharacterSkill semantics term '{term}' must have exactly one nonblank key=value pair.");
            }
            RequireCanonicalIdentifier(term.Substring(0, equals), "term key", allowDot: true);
        }
    }

    private static void WriteModule(
        StringBuilder builder,
        CharacterSkillModuleSemanticsDto module)
    {
        builder.Append('{');
        WriteProperty(builder, "descriptionKo", module.descriptionKo);
        builder.Append(',');
        WriteProperty(builder, "effectKind", module.effectKind);
        builder.Append(',');
        WriteProperty(builder, "executionScope", module.executionScope);
        builder.Append(',');
        WriteProperty(builder, "moduleId", module.moduleId);
        builder.Append(",\"terms\":[");
        for (int index = 0; index < module.terms.Count; index++)
        {
            if (index > 0) builder.Append(',');
            WriteString(builder, module.terms[index]);
        }
        builder.Append("],");
        WriteProperty(builder, "variantId", module.variantId);
        builder.Append('}');
    }

    private static void WriteProperty(StringBuilder builder, string name, string value)
    {
        WriteString(builder, name);
        builder.Append(':');
        WriteString(builder, value);
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        RequireWellFormedUtf16(value);
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append('"');
    }

    private static void RequireWellFormedUtf16(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    throw new InvalidOperationException(
                        "CharacterSkill canonical semantics strings require well-formed UTF-16.");
                }
                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                throw new InvalidOperationException(
                    "CharacterSkill canonical semantics strings require well-formed UTF-16.");
            }
        }
    }

    private static void RequireCanonicalIdentifier(
        string value,
        string label,
        bool allowDot = false)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || !value.All(character => character >= 'a' && character <= 'z'
                || character >= '0' && character <= '9'
                || character == '_'
                || allowDot && character == '.'))
        {
            throw new InvalidOperationException(
                $"CharacterSkill semantics {label} '{value ?? string.Empty}' is not canonical lower-snake text.");
        }
    }

    private static void RequireFinite(
        float value,
        string moduleId,
        string variantId,
        string field)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new InvalidOperationException(
                $"CharacterSkill variant '{moduleId}|{variantId}' {field} must be finite.");
        }
    }

    private static string Decimal(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new InvalidOperationException("CharacterSkill semantics cannot format a non-finite value.");
        }
        return value.ToString("0.#########", CultureInfo.InvariantCulture);
    }

    private static string Snake(string value)
    {
        StringBuilder builder = new StringBuilder(value?.Length ?? 0);
        foreach (char character in value ?? string.Empty)
        {
            if (char.IsUpper(character) && builder.Length > 0) builder.Append('_');
            builder.Append(char.ToLowerInvariant(character));
        }
        return builder.ToString();
    }

    private static string ScopeDescriptionKo(string scope)
    {
        return scope switch
        {
            "offense_battle" => "공격 전투",
            "outside_passive" => "전투 세션 밖 패시브",
            "defense_ultimate" => "방어 궁극기",
            "management_event" => "관리 이벤트 즉시 실행",
            "management_query" => "관리 수치 조회",
            _ => throw new InvalidOperationException($"Unknown scope '{scope}'.")
        };
    }

    private static string ManagementActivationKo(CharacterSkillKind kind)
    {
        return kind == CharacterSkillKind.Ultimate
            ? "운영일 시작 자동 궁극기 실행 때"
            : kind == CharacterSkillKind.Active
                ? "수동 작업 능력 발동 때"
                : "설정된 패시브 발동 때";
    }

    private static string ManagementQueryPrefixKo(
        bool ultimate,
        CharacterSkillTrigger trigger)
    {
        return ultimate
            ? "관리 궁극기가 한 번 활성화된 뒤"
            : trigger == CharacterSkillTrigger.ManualWork
                ? "수동 작업 능력이 활성화된 동안"
                : TriggerKo(trigger) + " 패시브로";
    }

    private static string TriggerKo(CharacterSkillTrigger trigger)
    {
        return trigger switch
        {
            CharacterSkillTrigger.WorkStarted => "작업 시작",
            CharacterSkillTrigger.WorkCompleted => "작업 완료",
            CharacterSkillTrigger.NeedChanged => "욕구 변화",
            CharacterSkillTrigger.MoodChanged => "기분 변화",
            CharacterSkillTrigger.RelationshipChanged => "관계 변화",
            CharacterSkillTrigger.OperatingDayStarted => "운영일 시작",
            CharacterSkillTrigger.BattleStarted => "전투 시작",
            CharacterSkillTrigger.DamageTaken => "피해를 받음",
            CharacterSkillTrigger.EnemyDefeated => "적 처치",
            CharacterSkillTrigger.BattleCompleted => "전투 완료",
            CharacterSkillTrigger.InvasionStarted => "침입 시작",
            CharacterSkillTrigger.ManualCombat => "수동 전투",
            CharacterSkillTrigger.ManualWork => "수동 작업 능력",
            _ => trigger.ToString()
        };
    }

    private static InvalidOperationException MissingMapping(
        CharacterSkillModuleSelection selection,
        CharacterSkillSemanticsExecutionScope scope)
    {
        return new InvalidOperationException(
            $"CharacterSkill semantics have no {ExecutionScopeToken(scope)} mapping for "
            + $"'{selection?.moduleId ?? string.Empty}|{selection?.variantId ?? string.Empty}'.");
    }

    private static InvalidOperationException DomainMismatch(
        string moduleId,
        CharacterSkillKind kind,
        CharacterUltimateDomain domain)
    {
        return new InvalidOperationException(
            $"CharacterSkill module '{moduleId}' does not match kind '{kind}' and domain '{domain}'.");
    }
}
