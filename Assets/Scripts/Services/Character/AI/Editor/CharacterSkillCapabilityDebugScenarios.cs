#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CharacterSkillCapabilityDebugScenarios
{
    public static bool RunFormulaCatalogRelationScenario()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        try
        {
            VerifyFormulaCatalogRelations(settings);
            Debug.Log("CharacterSkill formula catalog relation scenario passed.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    public static bool RunAll()
    {
        CharacterSkillSystemSettingsSO settings =
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults();
        try
        {
            VerifyFormulaCatalogRelations(settings);
            CharacterSkillCombinationSemanticsFactory.ValidateSettingsCoverage(settings);
            CharacterSkillModuleCapabilityDescriptor multiTarget =
                CharacterSkillModuleCapabilityRegistry.Require(settings.FindModule("multi_target"));
            CharacterSkillModuleCapabilityDescriptor conditionalAmplify =
                CharacterSkillModuleCapabilityRegistry.Require(settings.FindModule("conditional_amplify"));
            CharacterSkillModuleCapabilityDescriptor reposition =
                CharacterSkillModuleCapabilityRegistry.Require(settings.FindModule("reposition"));
            Require(!multiTarget.IsTargetCompatible(CharacterSkillTarget.Self)
                    && !multiTarget.IsTargetCompatible(CharacterSkillTarget.Ally)
                    && multiTarget.IsTargetCompatible(CharacterSkillTarget.Enemy)
                    && multiTarget.IsTargetCompatible(CharacterSkillTarget.AllEnemies),
                "Multi-target damage must reject friendly targets.");
            Require(!conditionalAmplify.IsTargetCompatible(CharacterSkillTarget.Self)
                    && conditionalAmplify.IsTargetCompatible(CharacterSkillTarget.Enemy),
                "Conditional damage amplification must reject friendly targets.");
            Require(reposition.IsTargetCompatible(CharacterSkillTarget.Self)
                    && reposition.IsTargetCompatible(CharacterSkillTarget.Enemy),
                "Reposition must remain valid for either actor side.");
            List<CharacterSkillModuleRule> modules =
                settings.Modules as List<CharacterSkillModuleRule>
                ?? throw new InvalidOperationException(
                    "Transient CharacterSkill settings do not expose the authored module list.");

            CharacterSkillCandidateRule originalRule = Rule(
                "qa:capability:original",
                "damage",
                "light");
            string originalCombinationId = CharacterSkillCombinationCatalog
                .Build(originalRule, settings, CharacterSkillKind.Active)
                .Single()
                .Id;

            CharacterCombatSkillModuleRule alias = new CharacterCombatSkillModuleRule
            {
                id = "arcane_damage",
                capabilityId = "damage",
                displayName = "비전 피해",
                allowedKinds = new List<CharacterSkillKind> { CharacterSkillKind.Active },
                allowedTriggers = new List<CharacterSkillTrigger>
                {
                    CharacterSkillTrigger.ManualCombat
                },
                allowedTargets = new List<CharacterSkillTarget>
                {
                    CharacterSkillTarget.Enemy
                },
                variants = new List<CharacterSkillNumericVariant>
                {
                    new CharacterSkillNumericVariant(
                        "focused",
                        "집중",
                        1.25f,
                        0f,
                        0,
                        1,
                        3)
                }
            };
            modules.Add(alias);

            CharacterSkillCombinationSemanticsFactory.ValidateSettingsCoverage(settings);
            Require(CharacterSkillCombinationSemanticsFactory
                    .CreateCatalogCoverage(settings)
                    .Any(value => value.moduleId == alias.id
                        && value.variantId == "focused"),
                "Dynamic catalog semantics omitted the data-only alias module/variant.");
            Require(CharacterSkillCombinationCatalog
                    .Build(originalRule, settings, CharacterSkillKind.Active)
                    .Single()
                    .Id == originalCombinationId,
                "Adding an alias module changed an existing combination ID.");

            CharacterSkillCandidateRule aliasRule = Rule(
                "qa:capability:alias",
                alias.id,
                alias.variants[0].id);
            CharacterSkillAllowedCombination aliasCombination =
                CharacterSkillCombinationCatalog
                    .Build(aliasRule, settings, CharacterSkillKind.Active)
                    .Single();
            Require(aliasCombination.Modules.Single().moduleId == alias.id,
                "Alias combination did not retain its authored moduleId.");

            CharacterSkillCombinationSemanticsDto semantics =
                CharacterSkillCombinationSemanticsFactory.Create(
                    aliasCombination,
                    aliasRule,
                    CharacterSkillKind.Active,
                    settings);
            CharacterSkillModuleSemanticsDto semanticModule = semantics.modules.Single();
            Require(semanticModule.moduleId == alias.id
                    && semanticModule.variantId == "focused"
                    && semanticModule.effectKind == "basic_damage",
                "Alias module did not reuse damage semantics while retaining authored identity.");

            CharacterSkillInstance skill = new CharacterSkillInstance
            {
                id = "qa:capability:skill",
                ruleId = aliasRule.ruleId,
                combinationId = aliasCombination.Id,
                displayName = "비전 일격",
                description = "등록된 피해 능력을 재사용한다.",
                kind = CharacterSkillKind.Active,
                rarity = CharacterSkillRarity.Common,
                trigger = CharacterSkillTrigger.ManualCombat,
                target = CharacterSkillTarget.Enemy,
                cooldownTurns = 1,
                modules = aliasCombination.Modules.Select(value => value.Clone()).ToList()
            };
            CharacterCombatAbilityDefinition ability =
                CharacterSkillRuntimeEffects.ToCombatAbility(skill, settings);
            Require(ability != null && ability.IsValid && ability.Effects.Count == 1,
                "Alias module did not reuse the registered damage runtime effect.");

            CharacterCombatSkillModuleRule unknown = CloneAlias(alias, "unknown_module", "unknown_capability");
            modules.Add(unknown);
            RequireThrows(
                () => CharacterSkillCombinationSemanticsFactory.ValidateSettingsCoverage(settings),
                "unknown capabilityId");
            modules.Remove(unknown);

            CharacterManagementSkillModuleRule wrongDomain = new CharacterManagementSkillModuleRule
            {
                id = "wrong_domain",
                capabilityId = "damage",
                displayName = "잘못된 분류",
                allowedKinds = new List<CharacterSkillKind> { CharacterSkillKind.Passive },
                allowedTriggers = new List<CharacterSkillTrigger>
                {
                    CharacterSkillTrigger.WorkCompleted
                },
                allowedTargets = new List<CharacterSkillTarget>
                {
                    CharacterSkillTarget.Self
                },
                variants = new List<CharacterSkillNumericVariant>
                {
                    new CharacterSkillNumericVariant("one", "하나", 1f, 0f, 0, 1, 1)
                }
            };
            modules.Add(wrongDomain);
            RequireThrows(
                () => CharacterSkillCombinationSemanticsFactory.ValidateSettingsCoverage(settings),
                "class does not match");
            modules.Remove(wrongDomain);

            Debug.Log("CharacterSkill capability scenarios passed.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static void VerifyFormulaCatalogRelations(
        CharacterSkillSystemSettingsSO settings)
    {
        Require(
            settings.RequireFormulaCatalog().Count
                == CharacterSkillFormulaCatalogAssetBuilder.ExpectedModuleCount,
            "Current CharacterSkill formula catalog did not pass runtime relation validation.");

        CharacterSkillModuleRule damage = settings.FindModule("damage")
            ?? throw new InvalidOperationException("The damage module is missing.");
        CharacterSkillModuleRule conditionalAmplify = settings.FindModule("conditional_amplify")
            ?? throw new InvalidOperationException("The conditional-amplify module is missing.");
        CharacterSkillPairSynergyCostDefinition damagePair = damage.formula
            .pairSynergyCosts.Single(value => string.Equals(
                value.otherCapabilityId,
                "conditional_amplify",
                StringComparison.Ordinal));
        CharacterSkillPairSynergyCostDefinition reversePair = conditionalAmplify.formula
            .pairSynergyCosts.Single(value => string.Equals(
                value.otherCapabilityId,
                "damage",
                StringComparison.Ordinal));
        string originalPairCapabilityId = damagePair.otherCapabilityId;
        try
        {
            damagePair.otherCapabilityId = "qa:unknown-pair-capability";
            RequireThrows(
                () => settings.RequireFormulaCatalog(),
                "an unknown pair-synergy capability 'qa:unknown-pair-capability'");
        }
        finally
        {
            damagePair.otherCapabilityId = originalPairCapabilityId;
        }
        int originalReverseCost = reversePair.cost;
        try
        {
            reversePair.cost = checked(originalReverseCost + 1);
            RequireThrows(
                () => settings.RequireFormulaCatalog(),
                "Pair cost 'damage/conditional_amplify' is not symmetric");
        }
        finally
        {
            reversePair.cost = originalReverseCost;
        }

        CharacterSkillModuleRule guard = settings.FindModule("guard")
            ?? throw new InvalidOperationException("The guard module is missing.");
        CharacterSkillModuleRule protect = settings.FindModule("protect")
            ?? throw new InvalidOperationException("The protect module is missing.");
        int guardForbiddenIndex = guard.formula.forbiddenSynergies.FindIndex(value =>
            string.Equals(value, "protect", StringComparison.Ordinal));
        int protectForbiddenIndex = protect.formula.forbiddenSynergies.FindIndex(value =>
            string.Equals(value, "guard", StringComparison.Ordinal));
        Require(guardForbiddenIndex >= 0 && protectForbiddenIndex >= 0,
            "Current guard/protect forbidden synergy is missing.");
        string originalForbidden = guard.formula.forbiddenSynergies[guardForbiddenIndex];
        try
        {
            guard.formula.forbiddenSynergies[guardForbiddenIndex] =
                "qa:unknown-forbidden-capability";
            RequireThrows(
                () => settings.RequireFormulaCatalog(),
                "an unknown forbidden synergy 'qa:unknown-forbidden-capability'");
        }
        finally
        {
            guard.formula.forbiddenSynergies[guardForbiddenIndex] = originalForbidden;
        }
        protect.formula.forbiddenSynergies.RemoveAt(protectForbiddenIndex);
        try
        {
            RequireThrows(
                () => settings.RequireFormulaCatalog(),
                "Forbidden synergy 'guard/protect' is not symmetric");
        }
        finally
        {
            protect.formula.forbiddenSynergies.Insert(
                protectForbiddenIndex,
                "guard");
        }
    }

    private static CharacterSkillCandidateRule Rule(
        string ruleId,
        string moduleId,
        string variantId)
    {
        return new CharacterSkillCandidateRule
        {
            ruleId = ruleId,
            rarity = CharacterSkillRarity.Common,
            budget = 8,
            trigger = CharacterSkillTrigger.ManualCombat,
            target = CharacterSkillTarget.Enemy,
            ultimateDomain = CharacterUltimateDomain.None,
            cooldownTurns = 1,
            mechanicalPolicySource = CharacterSkillMechanicalPolicySource.AuthoredRule,
            allowedModuleIds = new List<string> { moduleId },
            allowedVariantIds = new List<string> { variantId }
        };
    }

    private static CharacterCombatSkillModuleRule CloneAlias(
        CharacterCombatSkillModuleRule source,
        string moduleId,
        string capabilityId)
    {
        return new CharacterCombatSkillModuleRule
        {
            id = moduleId,
            capabilityId = capabilityId,
            displayName = source.displayName,
            allowedKinds = source.allowedKinds.ToList(),
            allowedTriggers = source.allowedTriggers.ToList(),
            allowedTargets = source.allowedTargets.ToList(),
            variants = source.variants.Select(value => new CharacterSkillNumericVariant(
                value.id,
                value.displayName,
                value.primaryValue,
                value.secondaryValue,
                value.duration,
                value.count,
                value.cost)).ToList()
        };
    }

    private static void RequireThrows(Action action, string expectedMessage)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(expectedMessage, StringComparison.Ordinal),
                $"Expected error containing '{expectedMessage}', got '{exception.Message}'.");
            return;
        }
        throw new InvalidOperationException(
            $"Expected InvalidOperationException containing '{expectedMessage}'.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
