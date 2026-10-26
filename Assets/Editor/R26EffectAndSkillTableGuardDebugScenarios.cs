using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class R26EffectAndSkillTableGuardDebugScenarios
{
    private const string RootCatalogPath =
        "Assets/Resources/SO/GameContentCatalog.asset";

    [MenuItem("DungeonStory/R26/Content/Validate Effect and Skill Table Guards")]
    public static void Run()
    {
        VerifyTraitEffectGuards();
        VerifySpeciesEffectGuards();
        VerifySkillRarityTableGuards();
        VerifyRegisteredContent();
        Debug.Log(
            "R26_EFFECT_AND_SKILL_TABLE_GUARDS=PASS; trait duplicate/null; "
            + "species source/duplicate/null; rarity/potential duplicate/null; "
            + "registered 11 species and 149 bindings; registered skill settings");
    }

    private static void VerifyTraitEffectGuards()
    {
        GameplayEffectDefinitionSO definition = CreateEffect(
            "effect:r26:trait-only",
            GameplayEffectSourceKind.Trait);
        CharacterTraitSO trait = ScriptableObject.CreateInstance<CharacterTraitSO>();
        try
        {
            trait.id = 990001;
            trait.traitName = "R26 duplicate effect trait";
            trait.selectionFamilyId = "r26:effect-guard";
            trait.effects = new List<GameplayEffectBinding>
            {
                Binding("binding:r26:duplicate", definition),
                Binding(" binding:r26:duplicate ", definition)
            };
            Require(
                trait.ValidateDefinition().Any(error => error.Contains(
                    "duplicate gameplay effect binding ids",
                    StringComparison.Ordinal)),
                "CharacterTraitSO accepted duplicate trimmed binding IDs.");

            trait.effects = new List<GameplayEffectBinding> { null };
            Require(
                trait.ValidateDefinition().Any(error => error.Contains(
                    "null gameplay effect binding",
                    StringComparison.Ordinal)),
                "CharacterTraitSO silently ignored a null effect binding.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(trait);
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void VerifySpeciesEffectGuards()
    {
        GameplayEffectDefinitionSO traitOnly = CreateEffect(
            "effect:r26:trait-only-for-species",
            GameplayEffectSourceKind.Trait);
        GameplayEffectDefinitionSO speciesEffect = CreateEffect(
            "effect:r26:species",
            GameplayEffectSourceKind.Species);
        CharacterSpeciesSO species = ScriptableObject
            .CreateInstance<CharacterSpeciesSO>();
        try
        {
            species.speciesTag = "species:r26-effect-guard";
            species.ConfigureGameplayEffects(new[]
            {
                Binding("binding:r26:wrong-owner", traitOnly)
            });
            Require(
                species.ValidateGameplayEffects().Any(error => error.Contains(
                    "does not allow source Species",
                    StringComparison.Ordinal)),
                "CharacterSpeciesSO accepted an effect that excludes Species.");

            species.ConfigureGameplayEffects(new[]
            {
                Binding("binding:r26:duplicate", speciesEffect),
                Binding(" binding:r26:duplicate ", speciesEffect)
            });
            Require(
                species.ValidateGameplayEffects().Any(error => error.Contains(
                    "duplicate gameplay effect binding ids",
                    StringComparison.Ordinal)),
                "CharacterSpeciesSO accepted duplicate trimmed binding IDs.");

            species.ConfigureGameplayEffects(new GameplayEffectBinding[] { null });
            Require(
                species.ValidateGameplayEffects().Any(error => error.Contains(
                    "null gameplay effect binding",
                    StringComparison.Ordinal)),
                "CharacterSpeciesSO silently ignored a null effect binding.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(species);
            UnityEngine.Object.DestroyImmediate(speciesEffect);
            UnityEngine.Object.DestroyImmediate(traitOnly);
        }
    }

    private static void VerifySkillRarityTableGuards()
    {
        CharacterSkillSystemSettingsSO settings = ScriptableObject
            .CreateInstance<CharacterSkillSystemSettingsSO>();
        try
        {
            CharacterSkillRarity duplicateRarity = settings.rarityBudgets[0].rarity;
            settings.rarityBudgets.Add(new CharacterRarityBudget
            {
                rarity = duplicateRarity,
                budget = 99
            });
            RequireThrows<InvalidOperationException>(
                () => settings.GetBudget(duplicateRarity),
                "GetBudget accepted a duplicate rarity-budget key.");

            settings.EnsureDefaults();
            settings.rarityBudgets.RemoveAt(settings.rarityBudgets.Count - 1);
            CharacterPotentialGrade duplicatePotential =
                settings.potentialRarityProfiles[0].potential;
            settings.potentialRarityProfiles.Add(new CharacterPotentialRarityProfile
            {
                potential = duplicatePotential,
                rarityWeights = new List<CharacterWeightedRarity>()
            });
            RequireThrows<InvalidOperationException>(
                () => settings.GetRarityWeights(duplicatePotential),
                "GetRarityWeights accepted a duplicate potential-profile key.");

            settings.potentialRarityProfiles.RemoveAt(
                settings.potentialRarityProfiles.Count - 1);
            settings.rarityBudgets.Add(null);
            RequireThrows<InvalidOperationException>(
                () => settings.GetBudget(CharacterSkillRarity.Common),
                "GetBudget silently ignored a null rarity-budget entry.");

            settings.rarityBudgets.RemoveAt(settings.rarityBudgets.Count - 1);
            settings.potentialRarityProfiles.Add(null);
            RequireThrows<InvalidOperationException>(
                () => settings.GetRarityWeights(CharacterPotentialGrade.Ordinary),
                "GetRarityWeights silently ignored a null potential-profile entry.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(settings);
        }
    }

    private static void VerifyRegisteredContent()
    {
        GameContentCatalogSO root = AssetDatabase
            .LoadAssetAtPath<GameContentCatalogSO>(RootCatalogPath)
            ?? throw new InvalidOperationException(
                "Registered root content catalog is missing.");
        GameDomainContentCatalogSO domain = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .Single();
        CharacterSpeciesSO[] species = domain.GetAll<CharacterSpeciesSO>()
            .ToArray();
        int bindingCount = species.Sum(value => value.Effects.Count);
        string[] speciesErrors = species
            .SelectMany(value => value.ValidateGameplayEffects())
            .ToArray();
        Require(
            species.Length == 11 && bindingCount == 149,
            $"Registered species/effect count drifted: species={species.Length}, "
            + $"bindings={bindingCount}.");
        Require(
            speciesErrors.Length == 0,
            "Registered species effect validation failed: "
            + string.Join(" | ", speciesErrors));

        CharacterSkillSystemSettingsSO settings = root
            .GetCharacterSkillSettings<CharacterSkillSystemSettingsSO>()
            ?? throw new InvalidOperationException(
                "Registered character-skill settings are missing.");
        settings.RequireUniqueRarityTableKeys();
        foreach (CharacterSkillRarity rarity in Enum.GetValues(
                     typeof(CharacterSkillRarity)))
            _ = settings.GetBudget(rarity);
        foreach (CharacterPotentialGrade potential in Enum.GetValues(
                     typeof(CharacterPotentialGrade)))
            _ = settings.GetRarityWeights(potential);
    }

    private static GameplayEffectDefinitionSO CreateEffect(
        string effectId,
        GameplayEffectSourceKind allowedSources)
    {
        GameplayEffectDefinitionSO definition = ScriptableObject
            .CreateInstance<GameplayEffectDefinitionSO>();
        definition.Configure(
            990001,
            effectId,
            GameplayEffectTargetIds.WorkSpeed,
            GameplayEffectOperation.Multiply,
            GameplayEffectProjectionPhase.Multiplicative,
            allowedSources,
            GameplayEffectStackingPolicy.StackAll,
            0f,
            10f);
        return definition;
    }

    private static GameplayEffectBinding Binding(
        string bindingId,
        GameplayEffectDefinitionSO definition)
    {
        return new GameplayEffectBinding
        {
            bindingId = bindingId,
            definition = definition,
            value = 1f
        };
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
