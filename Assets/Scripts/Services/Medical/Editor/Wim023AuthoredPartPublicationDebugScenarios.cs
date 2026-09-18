#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Characters;
using UnityEditor;
using UnityEngine;

// Read-only authored-content witness for WIM-023 publication. It deliberately
// checks published Resources/catalog mappings only; live installation belongs
// to the separate surgery runtime coverage.
public static class Wim023AuthoredPartPublicationDebugScenarios
{
    private const string RootCatalogAssetPath =
        "Assets/Resources/SO/GameContentCatalog.asset";
    private const string ItemDefinitionRoot =
        "Assets/Resources/SO/Items/Definitions/";
    private const string RecipeRoot =
        "Assets/Resources/SO/Economy/Recipes/";

    private static readonly string[] TreatmentProcedureIds =
    {
        "procedure:slime-replenishment",
        "procedure:slime-membrane-suture",
        "procedure:slime-core-stabilization",
        "procedure:myconid-hypha-binding",
        "procedure:myconid-spore-cleaning",
        "procedure:harpy-air-sac-suture",
        "procedure:harpy-wing-fixation",
        "procedure:vampire-blood-sac",
        "procedure:demon-mana-core-suture"
    };

    private static readonly Dictionary<string, string> InstalledProcedurePartIds =
        new(StringComparer.Ordinal)
        {
            ["procedure:slime-pseudopod-reshape"] =
                "surgery:prosthetic:pseudopods",
            ["procedure:myconid-core-graft"] =
                "surgery:prosthetic:hypha-core",
            ["procedure:myconid-regrowth"] =
                "surgery:prosthetic:arm:left",
            ["procedure:harpy-feather-regrowth"] =
                "surgery:prosthetic:wing:left",
            ["procedure:harpy-tail-graft"] =
                "surgery:prosthetic:balance-tail",
            ["procedure:orc-skeletal-reinforcement"] =
                "surgery:prosthetic:torso",
            ["procedure:orc-combat-heart"] =
                "surgery:prosthetic:heart/variant/orc-combat-heart",
            ["procedure:vampire-night-eye"] =
                "surgery:prosthetic:night-eye:left",
            ["procedure:beastkin-tail-reconstruction"] =
                "surgery:prosthetic:balance-tail",
            ["procedure:beastkin-sprint-joint"] =
                "surgery:prosthetic:leg:left/variant/beastkin-sprint-joint",
            ["procedure:demon-heat-sac"] =
                "surgery:prosthetic:heat-sac/variant/demon-heat-sac",
            ["procedure:kobold-precision-hand"] =
                "surgery:prosthetic:hand:left",
            ["procedure:kobold-tail-balance"] =
                "surgery:prosthetic:balance-tail/variant/kobold-tail-balance",
            ["procedure:human-neural-assist"] =
                "surgery:prosthetic:brain/variant/human-neural-assist",
            ["procedure:human-precision-prosthetic"] =
                "surgery:prosthetic:arm:left"
        };

    private static readonly RecipeExpectation[] Recipes =
    {
        new("recipe:surgery:prosthetic-arm", "surgery:prosthetic:arm:left", 32f),
        new("recipe:surgery:prosthetic-leg", "surgery:prosthetic:leg:left", 32f),
        new("recipe:surgery:artificial-eye", "surgery:prosthetic:eye:left", 36f),
        new("recipe:surgery:pseudopods", "surgery:prosthetic:pseudopods", 34f),
        new("recipe:surgery:hypha-core", "surgery:prosthetic:hypha-core", 34f),
        new("recipe:surgery:wing", "surgery:prosthetic:wing:left", 34f),
        new("recipe:surgery:balance-tail", "surgery:prosthetic:balance-tail", 34f),
        new("recipe:surgery:reinforced-torso", "surgery:prosthetic:torso", 34f),
        new("recipe:surgery:heart-augmentation", "surgery:prosthetic:heart/variant/orc-combat-heart", 34f),
        new("recipe:surgery:night-eye", "surgery:prosthetic:night-eye:left", 34f),
        new("recipe:surgery:heat-sac", "surgery:prosthetic:heat-sac/variant/demon-heat-sac", 34f),
        new("recipe:surgery:precision-hand", "surgery:prosthetic:hand:left", 34f),
        new("recipe:surgery:brain-assist", "surgery:prosthetic:brain/variant/human-neural-assist", 34f),
        new("recipe:surgery:sprint-joint", "surgery:prosthetic:leg:left/variant/beastkin-sprint-joint", 34f),
        new("recipe:surgery:tail-balance-augmentation", "surgery:prosthetic:balance-tail/variant/kobold-tail-balance", 34f)
    };

    private static readonly PartEffectExpectation[] VariantEffects =
    {
        new(
            "surgery:prosthetic:leg:left/variant/beastkin-sprint-joint",
            "part:beastkin-sprint-joint:move-speed",
            GameplayEffectTargetIds.MoveSpeed,
            GameplayEffectOperation.Multiply,
            1.08f),
        new(
            "surgery:prosthetic:heat-sac/variant/demon-heat-sac",
            "part:demon-heat-sac:heat-exposure",
            GameplayEffectTargetIds.HeatExposure,
            GameplayEffectOperation.Multiply,
            0.80f),
        new(
            "surgery:prosthetic:heart/variant/orc-combat-heart",
            "part:orc-combat-heart:maximum-health",
            GameplayEffectTargetIds.MaximumHealth,
            GameplayEffectOperation.Multiply,
            1.10f),
        new(
            "surgery:prosthetic:balance-tail/variant/kobold-tail-balance",
            "part:kobold-tail-balance:evasion-chance",
            GameplayEffectTargetIds.EvasionChance,
            GameplayEffectOperation.AddFlat,
            0.03f),
        new(
            "surgery:prosthetic:brain/variant/human-neural-assist",
            "part:human-neural-assist:work-speed",
            GameplayEffectTargetIds.WorkSpeed,
            GameplayEffectOperation.Multiply,
            1.05f)
    };

    [MenuItem("DungeonStory/WIM-023/Run Authored Human Supply")]
    public static void RunHumanSupplyMenu()
    {
        Debug.Log(RunHumanSupplyFocused());
    }

    public static string RunHumanSupplyFocused()
    {
        IGameContentCatalog content = new ResourceGameContentCatalog(
            new UnityGameContentRootLoader());
        SurgicalProcedureSO[] procedures = Resources.LoadAll<SurgicalProcedureSO>(
            SurgicalProcedureSO.ResourcePath);
        VerifyHumanCustomerSupply(content, procedures);
        return "WIM023_HUMAN_SUPPLY=PASS;species=Human/id11;"
            + "customer=character-archetype:9010;ownerCandidate=false;"
            + "visitorRecruitment=true;culture=culture:adventurer-frontier;"
            + "anatomy=anatomy:human/brain;"
            + "neuralAssist=34WU/exact-part";
    }

    public static string RunFocused()
    {
        GameContentCatalogSO rootFromResources = Resources.Load<GameContentCatalogSO>(
                GameContentCatalogSO.ResourcePath)
            ?? throw new InvalidOperationException("Root content catalog is missing from Resources.");
        GameContentCatalogSO rootFromAssetDatabase =
            AssetDatabase.LoadAssetAtPath<GameContentCatalogSO>(RootCatalogAssetPath)
            ?? throw new InvalidOperationException("Root content catalog asset is missing.");
        Require(rootFromResources == rootFromAssetDatabase,
            "Resources root does not resolve to the authored root catalog asset.");

        ItemDefinitionCatalogSO itemCatalog = rootFromResources
            .GetItemDefinitions<ItemDefinitionCatalogSO>()
            ?? throw new InvalidOperationException("Root has no item-definition catalog.");
        GameDomainContentCatalogSO domainCatalog = rootFromResources.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .SingleOrDefault()
            ?? throw new InvalidOperationException("Root has no domain content catalog.");

        ItemDefinitionSO[] resourceItems = Resources.LoadAll<ItemDefinitionSO>(
            ItemDefinitionSO.UnifiedResourcePath);
        ProductionRecipeSO[] resourceRecipes = Resources.LoadAll<ProductionRecipeSO>(
            ProductionRecipeSO.ResourcePath);
        SurgicalProcedureSO[] procedures = Resources.LoadAll<SurgicalProcedureSO>(
            SurgicalProcedureSO.ResourcePath);

        VerifySpeciesProcedureMapping(procedures);
        VerifyRecipesAndPublishedItems(
            resourceRecipes,
            resourceItems,
            itemCatalog,
            domainCatalog);
        VerifyVariantEffects(resourceItems, domainCatalog);
        VerifyHumanCustomerSupply(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()),
            procedures);

        return "PASS recipes15(M06/Craft;legacy32/32/36;new12x34);"
            + "species24(9treatment+15install);installedParts13;"
            + "catalogMembership;uniqueOutputLines;physical1.8kg-stack1;variantBindings5;"
            + "humanCustomerSupply";
    }

    public static string RunMaximumHealthBound()
    {
        var item = Resources.LoadAll<ItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath)
            .Single(x => x.ItemId == "surgery:prosthetic:heart/variant/orc-combat-heart");
        var bindings = item.GetFeatureOrDefault<InstalledSurgicalPartEffectItemFeature>().effects;
        var binding = bindings.Single(x => x.definition.TargetId == GameplayEffectTargetIds.MaximumHealth);
        Require(binding.definition.MaximumResult == float.MaxValue
            && binding.definition.ValidateDefinition().Count == 0,
            "Maximum-health final absolute result must not use a multiplier-domain cap.");
        float projected = CharacterGameplayEffectProjector.Resolve(GameplayEffectTargetIds.MaximumHealth,
            100f, new IGameplayEffectSource[] { new AuthoredHeartSource(bindings) }).Value;
        Require(Mathf.Abs(projected - 110f) < .0001f,
            "Authored full-quality heart must project absolute base100 to110, actual=" + projected);
        return "PASS authored Orc heart absolute HP100->110; finite final result bound; no runtime state mutation";
    }

    private sealed class AuthoredHeartSource : IGameplayEffectSource
    {
        public AuthoredHeartSource(IReadOnlyList<GameplayEffectBinding> effects) { Effects = effects; }
        public GameplayEffectSourceRef SourceRef => new(GameplayEffectSourceKind.SurgicalPart, "wim023:authored-heart");
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
    }

    private static void VerifySpeciesProcedureMapping(
        IEnumerable<SurgicalProcedureSO> allProcedures)
    {
        SurgicalProcedureSO[] species = allProcedures
            .Where(procedure => procedure != null
                && procedure.Family != MedicalProcedureFamily.Construct
                && procedure.AllowedSpeciesIds != null
                && procedure.AllowedSpeciesIds.Count > 0)
            .ToArray();
        HashSet<string> expectedIds = new(TreatmentProcedureIds, StringComparer.Ordinal);
        expectedIds.UnionWith(InstalledProcedurePartIds.Keys);

        Require(species.Length == 24
            && species.Select(procedure => procedure.ProcedureId)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(expectedIds),
            "WIM-023 species procedures must remain the exact 24 authored IDs.");

        foreach (string procedureId in TreatmentProcedureIds)
        {
            SurgicalProcedureSO procedure = RequireSingle(
                species,
                value => string.Equals(value.ProcedureId, procedureId, StringComparison.Ordinal),
                procedureId);
            Require(!procedure.TryGetInstallationEffect(out _),
                "Treatment procedure gained an installed-part requirement: " + procedureId);
        }

        foreach (KeyValuePair<string, string> expected in InstalledProcedurePartIds)
        {
            SurgicalProcedureSO procedure = RequireSingle(
                species,
                value => string.Equals(value.ProcedureId, expected.Key, StringComparison.Ordinal),
                expected.Key);
            Require(procedure.TryGetInstallationEffect(out InstallSurgicalPartEffect install)
                && string.Equals(
                    install.requiredItemDefinitionId,
                    expected.Value,
                    StringComparison.Ordinal),
                "Installed procedure has the wrong exact physical part: " + expected.Key);
        }

        Require(species.Count(procedure => procedure.TryGetInstallationEffect(out _)) == 15,
            "WIM-023 must contain exactly fifteen installed-part species procedures.");
        Require(InstalledProcedurePartIds.Values.Distinct(StringComparer.Ordinal).Count() == 13,
            "WIM-023 must map installed procedures to exactly thirteen distinct parts.");
    }

    private static void VerifyRecipesAndPublishedItems(
        IReadOnlyList<ProductionRecipeSO> allRecipes,
        IReadOnlyList<ItemDefinitionSO> resourceItems,
        ItemDefinitionCatalogSO itemCatalog,
        GameDomainContentCatalogSO domainCatalog)
    {
        ProductionRecipeSO[] surgeryRecipes = allRecipes
            .Where(recipe => recipe != null && recipe.RecipeId.StartsWith(
                "recipe:surgery:", StringComparison.Ordinal))
            .ToArray();
        Require(surgeryRecipes.Length == Recipes.Length
            && surgeryRecipes.Select(recipe => recipe.RecipeId)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(Recipes.Select(expected => expected.RecipeId)),
            "Published surgical recipe IDs drifted from the approved fifteen.");

        HashSet<string> outputLineIds = new(StringComparer.Ordinal);
        foreach (RecipeExpectation expected in Recipes)
        {
            ProductionRecipeSO recipe = RequireSingle(
                surgeryRecipes,
                value => string.Equals(value.RecipeId, expected.RecipeId, StringComparison.Ordinal),
                expected.RecipeId);
            string recipePath = AssetDatabase.GetAssetPath(recipe);
            Require(recipePath.StartsWith(RecipeRoot, StringComparison.Ordinal)
                && AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(recipePath) == recipe,
                "Recipe is not the published Resources asset: " + expected.RecipeId);
            Require(domainCatalog.Definitions.Count(definition => definition == recipe) == 1,
                "Recipe is not indexed exactly once by the root domain catalog: "
                + expected.RecipeId);
            Require(string.Equals(recipe.FacilityTag, "m06", StringComparison.Ordinal)
                && string.Equals(recipe.WorkstationTag, "m06", StringComparison.Ordinal)
                && recipe.WorkTypeId == BuiltInWorkTypeIds.Craft
                && Mathf.Approximately(recipe.RequiredWork, expected.RequiredWork),
                "Recipe M06/Craft/work contract drifted: " + expected.RecipeId);

            recipe.ValidateCanonicalOutputLinesOrThrow();
            Require(recipe.Outputs.Count == 1,
                "Surgical recipe must have exactly one physical output: " + expected.RecipeId);
            ProductionOutputDefinition output = recipe.Outputs[0];
            Require(output.Role == ProductionOutputRole.Main
                && string.Equals(output.ItemId, expected.OutputItemId, StringComparison.Ordinal)
                && output.Amount == 1
                && Mathf.Approximately(output.Probability, 1f)
                && string.Equals(output.OutputLineId, expected.OutputLineId, StringComparison.Ordinal)
                && outputLineIds.Add(output.OutputLineId),
                "Surgical recipe output identity or quantity drifted: " + expected.RecipeId);

            ItemDefinitionSO item = RequireSingle(
                resourceItems,
                value => string.Equals(value.ItemId, expected.OutputItemId, StringComparison.Ordinal),
                expected.OutputItemId);
            string itemPath = AssetDatabase.GetAssetPath(item);
            Require(itemPath.StartsWith(ItemDefinitionRoot, StringComparison.Ordinal)
                && AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(itemPath) == item,
                "Recipe output item is not the published Resources asset: "
                + expected.OutputItemId);
            Require(itemCatalog.Definitions.Count(definition => definition == item) == 1,
                "Recipe output item is not indexed exactly once by the root item catalog: "
                + expected.OutputItemId);
            Require(item.TryGetFeature(out ProductionItemFeature production)
                && production.kind == ResourceItemKind.FinishedGood
                && Mathf.Approximately(item.UnitWeight, 1.8f)
                && item.MaxStack == 1
                && item.ValidateDefinition().Count == 0,
                "Published physical surgical part contract drifted: " + expected.OutputItemId);
        }

        foreach (string installedPartId in InstalledProcedurePartIds.Values
                     .Distinct(StringComparer.Ordinal))
        {
            Require(Recipes.Count(expected => string.Equals(
                    expected.OutputItemId,
                    installedPartId,
                    StringComparison.Ordinal)) == 1,
                "Installed procedure part must have exactly one M06 recipe: "
                + installedPartId);
            Require(itemCatalog.Definitions.Count(item => item != null
                    && string.Equals(item.ItemId, installedPartId, StringComparison.Ordinal)) == 1,
                "Installed procedure part must have exactly one catalog definition: "
                + installedPartId);
        }
    }

    private static void VerifyVariantEffects(
        IReadOnlyList<ItemDefinitionSO> items,
        GameDomainContentCatalogSO domainCatalog)
    {
        HashSet<string> variantIds = new(
            VariantEffects.Select(expected => expected.PartItemId),
            StringComparer.Ordinal);

        foreach (PartEffectExpectation expected in VariantEffects)
        {
            ItemDefinitionSO item = RequireSingle(
                items,
                value => string.Equals(value.ItemId, expected.PartItemId, StringComparison.Ordinal),
                expected.PartItemId);
            InstalledSurgicalPartEffectItemFeature feature = item
                .GetFeatureOrDefault<InstalledSurgicalPartEffectItemFeature>();
            GameplayEffectBinding[] bindings = feature?.effects?
                .Where(binding => binding != null)
                .ToArray() ?? Array.Empty<GameplayEffectBinding>();
            Require(bindings.Length == 1
                && string.Equals(bindings[0].bindingId, expected.BindingId, StringComparison.Ordinal)
                && bindings[0].definition != null
                && string.Equals(
                    bindings[0].definition.TargetId,
                    expected.TargetId,
                    StringComparison.Ordinal)
                && bindings[0].definition.Operation == expected.Operation
                && Mathf.Approximately(bindings[0].value, expected.Value)
                && domainCatalog.Definitions.Count(definition =>
                    definition == bindings[0].definition) == 1,
                "Variant surgical-part effect binding drifted: " + expected.PartItemId);
        }

        foreach (string partId in Recipes.Select(expected => expected.OutputItemId)
                     .Where(partId => !variantIds.Contains(partId))
                     .Distinct(StringComparer.Ordinal))
        {
            ItemDefinitionSO item = RequireSingle(
                items,
                value => string.Equals(value.ItemId, partId, StringComparison.Ordinal),
                partId);
            Require(item.GetFeatureOrDefault<InstalledSurgicalPartEffectItemFeature>() == null,
                "Nonvariant surgical part leaked an enhancement effect: " + partId);
        }
    }

    private static void VerifyHumanCustomerSupply(
        IGameContentCatalog content,
        IReadOnlyList<SurgicalProcedureSO> procedures)
    {
        GameDomainContentCatalogSO domainCatalog = content?.Domain
            ?? throw new InvalidOperationException(
                "Root content has no domain catalog for Human supply.");
        CharacterSpeciesSO human = RequireSingle(
            domainCatalog.GetAll<CharacterSpeciesSO>(),
            value => string.Equals(value.speciesTag, "Human", StringComparison.Ordinal),
            "Human species");
        Require(human.id == 11
            && human.ownerSelectable
            && string.IsNullOrWhiteSpace(human.homeFactionId)
            && string.Equals(human.anatomyProfileId, "anatomy:human", StringComparison.Ordinal),
            "Human must remain the neutral base-customer species without a new faction.");
        Require(human.lifeHistory != null
            && human.reproduction != null
            && human.funeralCulture != null
            && human.lifeHistory.SpeciesId.Equals(human.DefinitionId)
            && human.reproduction.SpeciesId.Equals(human.DefinitionId)
            && human.funeralCulture.SpeciesId.Equals(human.DefinitionId)
            && human.lifeHistory.ValidateDefinition().Count == 0
            && human.reproduction.ValidateDefinition().Count == 0,
            "Human lifecycle definitions are missing or invalid.");
        Require(domainCatalog.Definitions.Count(value => value == human) == 1
            && domainCatalog.Definitions.Count(value => value == human.lifeHistory) == 1
            && domainCatalog.Definitions.Count(value => value == human.reproduction) == 1
            && domainCatalog.Definitions.Count(value => value == human.funeralCulture) == 1,
            "Human species and lifecycle assets must each be cataloged exactly once.");

        GameplayEffectBinding[] humanCapacities = human.Effects
            .Where(value => value?.definition != null
                && value.definition.TargetId.StartsWith("capacity:", StringComparison.Ordinal))
            .ToArray();
        Require(humanCapacities.Length == 14
            && humanCapacities.Select(value => value.definition.TargetId)
                .Distinct(StringComparer.Ordinal).Count() == 14
            && humanCapacities.All(value => Mathf.Approximately(value.value, 1f)),
            "Human must have fourteen explicit neutral capacity bindings.");

        AnatomyProfileSO anatomy = RequireSingle(
            domainCatalog.GetAll<AnatomyProfileSO>(),
            value => string.Equals(value.ProfileId, "anatomy:human", StringComparison.Ordinal),
            "anatomy:human");
        Require(string.Equals(anatomy.AnatomyFamily, "humanoid", StringComparison.Ordinal)
            && anatomy.SpeciesIds.Any(value => string.Equals(
                value,
                "Human",
                StringComparison.OrdinalIgnoreCase))
            && anatomy.Nodes.Any(value => string.Equals(
                value.NodeId,
                "brain",
                StringComparison.Ordinal)),
            "Human anatomy must retain humanoid/brain eligibility.");

        CharacterSO customer = RequireSingle(
            domainCatalog.GetAll<CharacterSO>(),
            value => value.id == 9010,
            "character-archetype:9010");
        Require(customer.characterType == CharacterType.Customer
            && customer.role == CharacterRole.Regular
            && customer.species == human
            && string.Equals(customer.SpeciesTag, "Human", StringComparison.Ordinal)
            && customer.DefinitionId.Equals(
                new CharacterArchetypeId("character-archetype:9010")),
            "Human customer archetype is incomplete or points at the wrong species.");
        Require(domainCatalog.Definitions.Count(value => value == customer) == 1
            && !customer.IsOwnerCandidate
            && !domainCatalog.GetAll<CharacterSO>().Any(value => value != null
                && string.Equals(value.SpeciesTag, "Human", StringComparison.Ordinal)
                && value.IsOwnerCandidate),
            "Human must be published once as a regular customer, not as a new owner choice.");
        SpeciesCultureDefinitionSO humanCulture =
            new CharacterNarrativeCatalog(content).RequireDefaultCulture(
                customer.SpeciesTag);
        Require(string.Equals(
                humanCulture.defaultSpeciesId,
                customer.SpeciesTag,
                StringComparison.Ordinal)
            && !string.Equals(
                customer.SpeciesTag,
                "Adventurer",
                StringComparison.Ordinal),
            "The Human customer requires an explicitly authored Human culture without changing its phenotype to Adventurer.");
        Require(CharacterSpawnRules.IsRecruitmentEligible(
                hasSpecies: true,
                ownerSelectable: human.ownerSelectable,
                homeFactionId: human.homeFactionId,
                recruitmentContractUnlocked: false),
            "The base-customer Human path must not require a fabricated faction contract.");

        CharacterSpawnRequest spawn = CharacterSpawnRequest.FromAuthoring(customer);
        CharacterRuntimeProfile profile = new CharacterRuntimeProfileFactory(content)
            .Create(spawn);
        Require(spawn.PhenotypeSpeciesId.Equals(new CharacterSpeciesId("Human"))
            && profile.PhenotypeSpeciesId.Equals(new CharacterSpeciesId("Human"))
            && string.Equals(profile.SpeciesTag, "Human", StringComparison.Ordinal),
            "The authored Human customer does not resolve through the runtime profile factory.");

        SurgicalProcedureSO neuralAssist = RequireSingle(
            procedures,
            value => string.Equals(
                value.ProcedureId,
                "procedure:human-neural-assist",
                StringComparison.Ordinal),
            "procedure:human-neural-assist");
        Require(neuralAssist.AllowedSpeciesIds.Any(value => string.Equals(
                value,
                "human",
                StringComparison.OrdinalIgnoreCase))
            && neuralAssist.AllowedAnatomyFamilies.Any(value => string.Equals(
                value,
                "humanoid",
                StringComparison.OrdinalIgnoreCase))
            && string.Equals(neuralAssist.TargetNodeId, "brain", StringComparison.Ordinal)
            && Mathf.Approximately(neuralAssist.RequiredWork, 34f)
            && neuralAssist.TryGetInstallationEffect(out InstallSurgicalPartEffect install)
            && string.Equals(
                install.requiredItemDefinitionId,
                "surgery:prosthetic:brain/variant/human-neural-assist",
                StringComparison.Ordinal),
            "The Human customer no longer reaches the authored neural-assist procedure contract.");
    }

    private static T RequireSingle<T>(
        IEnumerable<T> values,
        Func<T, bool> predicate,
        string label) where T : UnityEngine.Object
    {
        T[] matches = values.Where(predicate).ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one '{label}', found {matches.Length}.");
        }
        return matches[0];
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RecipeExpectation
    {
        public RecipeExpectation(string recipeId, string outputItemId, float requiredWork)
        {
            RecipeId = recipeId;
            OutputItemId = outputItemId;
            RequiredWork = requiredWork;
            OutputLineId = "output:" + recipeId + "/000/main/" + outputItemId;
        }

        public string RecipeId { get; }
        public string OutputItemId { get; }
        public float RequiredWork { get; }
        public string OutputLineId { get; }
    }

    private sealed class PartEffectExpectation
    {
        public PartEffectExpectation(
            string partItemId,
            string bindingId,
            string targetId,
            GameplayEffectOperation operation,
            float value)
        {
            PartItemId = partItemId;
            BindingId = bindingId;
            TargetId = targetId;
            Operation = operation;
            Value = value;
        }

        public string PartItemId { get; }
        public string BindingId { get; }
        public string TargetId { get; }
        public GameplayEffectOperation Operation { get; }
        public float Value { get; }
    }
}
#endif
