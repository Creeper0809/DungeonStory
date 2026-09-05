#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class GameContentCatalogAssetBuilder
{
    private const int GenerationManifestVersion = 2;
    private const string GeneratorVersion = "v18-content-authority-2";
    private const string GenerationManifestPath =
        "Artifacts/QA/game-content-catalog-generation-manifest.json";
    private const string GeneratorSourcePath =
        "Assets/Scripts/Services/Items/Editor/GameContentCatalogAssetBuilder.cs";
    private const string LocalizationGeneratorSourcePath =
        "Assets/Scripts/Services/Items/Editor/DomainFailureLocalizationAssetBuilder.cs";
    private static readonly string[] GeneratorDependencySourcePaths =
    {
        GeneratorSourcePath,
        LocalizationGeneratorSourcePath,
        "Assets/Scripts/Content/GameContentCatalogSO.cs",
        "Assets/Scripts/Content/GameDomainContentCatalogSO.cs",
        "Assets/Scripts/Content/GameMediaCatalogSO.cs",
        "Assets/Scripts/Models/Buildings/Core/BuildingCoreAbilityDefinitions.cs",
        "Assets/Scripts/Models/Buildings/Core/BuildingPrimitives.cs",
        "Assets/Scripts/Models/Economy/Content/DataScriptableObject.cs",
        "Assets/Scripts/Models/Economy/Content/ItemDefinitionCatalogSO.cs",
        "Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs",
        "Assets/Scripts/Models/Economy/Content/ResourceEconomyModels.cs",
        "Assets/Scripts/Models/Economy/Content/WasteProcessingRulesSO.cs",
        "Assets/Scripts/Models/Economy/Content/WasteProcessingValueContracts.cs",
        "Assets/Scripts/Models/FacilityShop/Core/FacilityBlueprintSO.cs",
        "Assets/Scripts/Models/Items/Core/ItemPrimitives.cs",
        "Assets/Scripts/Models/Wildlife/Core/WildlifePrimitives.cs",
        "Assets/Scripts/Services/Buildings/Abilities/BuildingAbilityAccessors.cs",
        "Assets/Scripts/Services/Buildings/SO/BuildingSO.cs",
        "Assets/Scripts/Services/Combat/EquipmentEvolutionContracts.cs",
        "Assets/Scripts/Services/Evolution/EvolutionCatalystEconomyRuntime.cs",
        "Assets/Scripts/Services/FacilityShop/FacilityShopSystem.cs"
    };
    private const string ContentFolder = "Assets/Resources/SO/Content";
    private const string ItemCatalogPath = ContentFolder + "/ItemDefinitionCatalog.asset";
    private const string DomainCatalogPath = ContentFolder + "/GameDomainContentCatalog.asset";
    private const string MediaCatalogPath = ContentFolder + "/GameMediaCatalog.asset";
    private const string WasteRulesPath =
        "Assets/Resources/SO/Economy/WasteProcessingRules.asset";
    private const string LegacySubstanceRoot =
        "Assets/Resources/SO/Economy/Substances";
    private const string RootCatalogPath = "Assets/Resources/SO/GameContentCatalog.asset";
    private static HashSet<string> activeOutputPaths;
    private static HashSet<string> activeTouchedOutputPaths;

    [MenuItem("Tools/DungeonStory/Content/Migrate/Rebuild Explicit Content Catalog...")]
    public static void Rebuild()
    {
        string invocationKind;
        if (Application.isBatchMode)
        {
            if (!IsExplicitBatchModeInvocation())
            {
                throw new InvalidOperationException(
                    "Batchmode content migration must be invoked explicitly with "
                    + "'-executeMethod GameContentCatalogAssetBuilder.Rebuild'.");
            }
            invocationKind = "batchmode-explicit-execute-method";
        }
        else
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Rebuild generated game content?",
                "This migration rewrites generated item definitions, localization, "
                + "and root catalog assets. Ensure unrelated Inspector changes are saved "
                + "or reverted before continuing.",
                "Rebuild generated content",
                "Cancel");
            if (!confirmed)
            {
                Debug.Log("Game content catalog migration cancelled before any asset write.");
                return;
            }
            invocationKind = "editor-confirmed-migration";
        }

        ExecuteConfirmedMigration(invocationKind);
    }

    /// <summary>
    /// Rebuilds only the authoritative item-definition index after another
    /// explicit content builder has created, deleted, or renamed item assets.
    /// This intentionally does not touch localization, domain catalogs, media,
    /// or the root catalog.
    /// </summary>
    public static void ReindexItemDefinitions()
    {
        ItemDefinitionSO[] definitions = AssetDatabase
            .FindAssets("t:ItemDefinitionSO", new[] { "Assets/Resources/SO" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>)
            .Where(definition => definition != null)
            .Distinct()
            .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
            .ToArray();

        ItemDefinitionCatalogSO itemCatalog =
            AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalogSO>(ItemCatalogPath)
            ?? throw new InvalidOperationException(
                $"Required item-definition catalog is missing at '{ItemCatalogPath}'.");
        itemCatalog.SetDefinitions(definitions);
        IReadOnlyList<string> errors = itemCatalog.ValidateCatalog();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Item-definition reindex failed:\n" + string.Join("\n", errors));
        }
        EditorUtility.SetDirty(itemCatalog);
    }

    /// <summary>
    /// Replaces the complete production-recipe slice from its single authored
    /// Resources root. Resource-economy builders must call this after creating
    /// or deleting recipe assets so a physical recipe can never exist outside
    /// the runtime domain catalog.
    /// </summary>
    public static void ReindexProductionRecipes()
    {
        GameDomainContentCatalogSO domainCatalog =
            AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(
                DomainCatalogPath)
            ?? throw new InvalidOperationException(
                $"Required domain content catalog is missing at '{DomainCatalogPath}'.");
        ProductionRecipeSO[] recipes = AssetDatabase
            .FindAssets("t:ProductionRecipeSO", new[]
            {
                "Assets/Resources/SO/Economy/Recipes"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>)
            .Where(recipe => recipe != null)
            .Distinct()
            .OrderBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
            .ToArray();
        HashSet<ProductionRecipeSO> liveRecipes = new HashSet<ProductionRecipeSO>(
            recipes);
        List<ScriptableObject> definitions = domainCatalog.Definitions
            .Where(definition => definition != null
                && (definition is not ProductionRecipeSO recipe
                    || liveRecipes.Contains(recipe)))
            .ToList();
        HashSet<ProductionRecipeSO> alreadyIndexed = definitions
            .OfType<ProductionRecipeSO>()
            .ToHashSet();
        definitions.AddRange(recipes
            .Where(recipe => !alreadyIndexed.Contains(recipe))
            .Cast<ScriptableObject>());

        bool changed = !domainCatalog.Definitions.SequenceEqual(definitions);
        if (changed)
            domainCatalog.SetDefinitions(definitions);
        IReadOnlyList<string> errors = domainCatalog.ValidateCatalog();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Production-recipe reindex failed:\n" + string.Join("\n", errors));
        }
        if (changed)
            EditorUtility.SetDirty(domainCatalog);
    }

    /// <summary>
    /// Replaces only the research-project slice of the authoritative domain
    /// index. Other content builders may intentionally leave shadow or
    /// migration assets under Resources, so a research rebuild must never
    /// indiscriminately index every ScriptableObject it can find.
    /// </summary>
    public static void ReindexResearchProjects()
    {
        GameDomainContentCatalogSO domainCatalog =
            AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(
                DomainCatalogPath)
            ?? throw new InvalidOperationException(
                $"Required domain content catalog is missing at '{DomainCatalogPath}'.");
        ResearchProjectSO[] projects = AssetDatabase
            .FindAssets("t:ResearchProjectSO", new[]
            {
                "Assets/Resources/SO/Research/Projects"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null)
            .Distinct()
            .OrderBy(project => project.ProjectId.Value, StringComparer.Ordinal)
            .ToArray();
        ScriptableObject[] definitions = domainCatalog.Definitions
            .Where(asset => asset != null
                && asset is not ResearchProjectSO
                && !IsLegacyDungeonFactionShadow(asset)
                && !IsLegacyFestivalShadow(asset))
            .Concat(projects.Cast<ScriptableObject>())
            .Distinct()
            .ToArray();

        domainCatalog.SetDefinitions(definitions);
        IReadOnlyList<string> errors = domainCatalog.ValidateCatalog();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Domain-definition reindex failed:\n" + string.Join("\n", errors));
        }
        EditorUtility.SetDirty(domainCatalog);
    }

    public static void ReindexCharacterAiActions()
    {
        GameDomainContentCatalogSO domainCatalog =
            AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(
                DomainCatalogPath)
            ?? throw new InvalidOperationException(
                $"Required domain content catalog is missing at '{DomainCatalogPath}'.");
        AIActionSet[] actions = AssetDatabase
            .FindAssets("t:AIActionSet", new[]
            {
                "Assets/Resources/SO/AI/Action"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AIActionSet>)
            .Where(action => action != null)
            .OrderBy(action => action.name, StringComparer.Ordinal)
            .ToArray();
        ScriptableObject[] definitions = domainCatalog.Definitions
            .Where(asset => asset != null && asset is not AIActionSet)
            .Concat(actions.Cast<ScriptableObject>())
            .Distinct()
            .ToArray();

        domainCatalog.SetDefinitions(definitions);
        IReadOnlyList<string> errors = domainCatalog.ValidateCatalog();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "AI action domain-definition reindex failed:\n"
                + string.Join("\n", errors));
        }
        EditorUtility.SetDirty(domainCatalog);
    }

    /// <summary>
    /// Replaces only the V22 apparel and textile definition slices. The full
    /// catalog migration is deliberately avoided so unrelated authored assets
    /// and open Inspector changes are never rewritten by the apparel builder.
    /// </summary>
    public static void ReindexV22ApparelDefinitions()
    {
        GameDomainContentCatalogSO domainCatalog =
            AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(
                DomainCatalogPath)
            ?? throw new InvalidOperationException(
                $"Required domain content catalog is missing at '{DomainCatalogPath}'.");
        ScriptableObject[] apparel = AssetDatabase.FindAssets(
                "t:ApparelDefinitionSO", new[] { "Assets/Resources/SO/Apparel" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ApparelDefinitionSO>)
            .Where(value => value != null)
            .Cast<ScriptableObject>()
            .ToArray();
        ScriptableObject[] materials = AssetDatabase.FindAssets(
                "t:TextileMaterialDefinitionSO", new[] { "Assets/Resources/SO/Apparel" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TextileMaterialDefinitionSO>)
            .Where(value => value != null)
            .Cast<ScriptableObject>()
            .ToArray();
        ScriptableObject[] textileCrops = AssetDatabase.FindAssets(
                "t:CropDefinitionSO", new[]
                {
                    "Assets/Resources/SO/Economy/Crops/V22Textiles"
                })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CropDefinitionSO>)
            .Where(value => value != null)
            .Cast<ScriptableObject>()
            .ToArray();
        ScriptableObject[] textileGenomes = AssetDatabase.FindAssets(
                "t:CropGenomeDefinitionSO", new[]
                {
                    "Assets/Resources/SO/Economy/CropGenomes/V22Textiles"
                })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<CropGenomeDefinitionSO>)
            .Where(value => value != null)
            .Cast<ScriptableObject>()
            .ToArray();
        ScriptableObject[] textileRecipes = AssetDatabase.FindAssets(
                "t:ProductionRecipeSO", new[]
                {
                    "Assets/Resources/SO/Economy/Recipes/V22Apparel"
                })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>)
            .Where(value => value != null)
            .Cast<ScriptableObject>()
            .ToArray();
        ScriptableObject[] liveV22Definitions = apparel
            .Concat(materials)
            .Concat(textileCrops)
            .Concat(textileGenomes)
            .Concat(textileRecipes)
            .Distinct()
            .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
            .ToArray();
        HashSet<ScriptableObject> liveV22Set = new(liveV22Definitions);
        List<ScriptableObject> definitions = domainCatalog.Definitions
            .Where(value => value != null
                && (!IsV22OwnedDefinition(value)
                    || liveV22Set.Contains(value)))
            .ToList();
        HashSet<ScriptableObject> alreadyIndexed = new(definitions);
        definitions.AddRange(liveV22Definitions
            .Where(value => !alreadyIndexed.Contains(value)));
        bool changed = !domainCatalog.Definitions.SequenceEqual(definitions);
        if (changed)
            domainCatalog.SetDefinitions(definitions);
        IReadOnlyList<string> errors = domainCatalog.ValidateCatalog();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "V22 apparel domain-definition reindex failed:\n"
                + string.Join("\n", errors));
        }
        if (changed)
            EditorUtility.SetDirty(domainCatalog);
    }

    private static bool IsV22OwnedDefinition(ScriptableObject value) =>
        value is ApparelDefinitionSO
        || value is TextileMaterialDefinitionSO
        || IsV22TextileCrop(value)
        || IsV22TextileGenome(value)
        || IsV22TextileRecipe(value);

    private static bool IsV22TextileCrop(ScriptableObject value) =>
        value is CropDefinitionSO crop
        && crop.CropId is "crop:frost-flax"
            or "crop:ember-cotton"
            or "crop:mire-reed"
            or "crop:spore-hemp";

    private static bool IsV22TextileRecipe(ScriptableObject value) =>
        value is ProductionRecipeSO recipe
        && recipe.RecipeId.StartsWith("recipe:v22:", StringComparison.Ordinal);

    private static bool IsV22TextileGenome(ScriptableObject value) =>
        value is CropGenomeDefinitionSO genome
        && (genome.GenomeId.StartsWith("genome:frost-flax:", StringComparison.Ordinal)
            || genome.GenomeId.StartsWith("genome:ember-cotton:", StringComparison.Ordinal)
            || genome.GenomeId.StartsWith("genome:mire-reed:", StringComparison.Ordinal)
            || genome.GenomeId.StartsWith("genome:spore-hemp:", StringComparison.Ordinal));

    private static bool IsLegacyDungeonFactionShadow(ScriptableObject asset)
    {
        if (asset is not DungeonFactionDefinitionSO)
        {
            return false;
        }

        string path = AssetDatabase.GetAssetPath(asset)
            .Replace('\\', '/');
        return path.StartsWith(
            "Assets/Resources/SO/Factions/faction_dungeon_",
            StringComparison.Ordinal)
            && path.EndsWith(".asset", StringComparison.Ordinal);
    }

    private static bool IsLegacyFestivalShadow(ScriptableObject asset)
    {
        if (asset is not FestivalDefinitionSO)
        {
            return false;
        }

        string path = AssetDatabase.GetAssetPath(asset)
            .Replace('\\', '/');
        return path.StartsWith(
            "Assets/Resources/SO/Population/Festivals/",
            StringComparison.Ordinal)
            && path.EndsWith(".asset", StringComparison.Ordinal);
    }

    private static void ExecuteConfirmedMigration(string invocationKind)
    {
        RequireNoDirtyOwnedAssets();
        string[] inputPaths = GetDeterministicInputPaths();
        string localizationProjection =
            DomainFailureLocalizationAssetBuilder.GetCanonicalProvenanceInput();
        string localizationContractHash = ComputeHash(
            Encoding.UTF8.GetBytes(localizationProjection));
        string inputHash = ComputeAggregateHash(inputPaths, localizationProjection);
        activeOutputPaths = new HashSet<string>(StringComparer.Ordinal);
        activeTouchedOutputPaths = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            EnsureFolder("Assets/Resources/SO", "Content");
            ValidateNoLegacySubstanceAssets();
            EnsurePhysicalOfferDefinitions();
            WasteProcessingRulesSO wasteRules = EnsureWasteProcessingRules();
            foreach (string localizationOutput in
                     DomainFailureLocalizationAssetBuilder.RebuildWithoutSaving(
                         RecordTouchedOutput))
            {
                RecordOutput(localizationOutput);
            }

        ItemDefinitionSO[] definitions = AssetDatabase
            .FindAssets("t:ItemDefinitionSO", new[] { "Assets/Resources/SO" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>)
            .Where(definition => definition != null)
            .Distinct()
            .OrderBy(definition => definition.ItemId, StringComparer.Ordinal)
            .ToArray();

        ItemDefinitionCatalogSO itemCatalog = GetOrCreate<ItemDefinitionCatalogSO>(
            ItemCatalogPath);
        itemCatalog.SetDefinitions(definitions);
        EditorUtility.SetDirty(itemCatalog);

        GameDomainContentCatalogSO domainCatalog =
            GetOrCreate<GameDomainContentCatalogSO>(DomainCatalogPath);
        ScriptableObject[] domainDefinitions = AssetDatabase
            .FindAssets(string.Empty, new[] { "Assets/Resources" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
            .Where(asset => asset != null
                && asset is not ItemDefinitionSO
                && asset is not ItemDefinitionCatalogSO
                && asset is not GameContentCatalogSO
                && asset is not GameDomainContentCatalogSO
                && asset is not GameMediaCatalogSO
                && !IsLegacyDungeonFactionShadow(asset)
                && !IsLegacyFestivalShadow(asset))
            .Append(wasteRules)
            .Distinct()
            .ToArray();
        domainCatalog.SetDefinitions(domainDefinitions);
        EditorUtility.SetDirty(domainCatalog);

        IReadOnlyList<string> wasteErrors = wasteRules.ValidateDefinition();
        if (wasteErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Waste-processing rules are invalid:\n"
                + string.Join("\n", wasteErrors));
        }

        GameContentCatalogSO root = GetOrCreate<GameContentCatalogSO>(RootCatalogPath);
        WorldInteractionPresentationCatalogSO presentation =
            AssetDatabase.LoadAssetAtPath<WorldInteractionPresentationCatalogSO>(
                "Assets/Resources/SO/Presentation/WorldInteractionPresentationCatalog.asset")
            ?? throw new InvalidOperationException("World presentation catalog asset is missing.");
        CharacterSkillSystemSettingsSO skillSettings =
            AssetDatabase.LoadAssetAtPath<CharacterSkillSystemSettingsSO>(
                "Assets/Resources/SO/Character/CharacterSkillSystemSettings.asset")
            ?? throw new InvalidOperationException("Character skill settings asset is missing.");
        GameMediaCatalogSO media = GetOrCreate<GameMediaCatalogSO>(MediaCatalogPath);
        media.Configure(
            RequireAsset<DungeonAudioLibrarySO>("Assets/Resources/Audio/DungeonAudioLibrary.asset"),
            AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(
                "Assets/Resources/Audio/DungeonAudioMixer.mixer"),
            RequireAsset<TmpKoreanFontSettingsSO>(
                "Assets/Resources/Config/TMPKoreanFontSettings.asset"),
            RequireAsset<Sprite>("Assets/Resources/Branding/DungeonStoryIcon.png"),
            RequireAsset<Material>("Assets/Resources/Materials/DoorSpriteUnlit.mat"));
        media.ValidateRequiredReferences();
        EditorUtility.SetDirty(media);
        root.Configure(
            itemCatalog,
            presentation,
            skillSettings,
            media,
            new ScriptableObject[] { domainCatalog });
        EditorUtility.SetDirty(root);

            ValidateGeneratedCatalogsBeforeSave(
                root,
                itemCatalog,
                domainCatalog,
                wasteRules);
            SaveOwnedOutputs();
            DomainFailureLocalizationAssetBuilder.ReleaseRuntimeTableAfterSave();

            WriteGenerationManifest(
                invocationKind,
                inputPaths,
                inputHash,
                localizationContractHash);
            Debug.Log(
                $"Explicit content catalog rebuilt: {definitions.Length} item definitions, "
                + $"{domainDefinitions.Length} domain definitions, one required Resources root. "
                + $"Provenance={GenerationManifestPath}");
        }
        catch (Exception exception)
        {
            string touched = activeTouchedOutputPaths == null
                || activeTouchedOutputPaths.Count == 0
                ? "<none recorded>"
                : string.Join(", ", activeTouchedOutputPaths.OrderBy(
                    path => path,
                    StringComparer.Ordinal));
            Debug.LogError(
                "Game content migration failed before completion. No unrelated dirty "
                + "assets were saved. Migration-owned assets may remain dirty or newly "
                + $"created and require review. Recorded outputs=[{touched}]. "
                + $"Failure={exception.GetType().Name}: {exception.Message}");
            throw;
        }
        finally
        {
            activeOutputPaths = null;
            activeTouchedOutputPaths = null;
        }
    }

    private static bool IsExplicitBatchModeInvocation()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], "-executeMethod", StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    arguments[index + 1],
                    nameof(GameContentCatalogAssetBuilder) + "." + nameof(Rebuild),
                    StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static void ValidateNoLegacySubstanceAssets()
    {
        string[] legacyAssets = AssetDatabase.IsValidFolder(LegacySubstanceRoot)
            ? AssetDatabase.FindAssets("t:ScriptableObject", new[] { LegacySubstanceRoot })
            : Array.Empty<string>();
        if (legacyAssets.Length > 0)
        {
            throw new InvalidOperationException(
                "Legacy SubstanceDefinitionSO assets cannot be registered in the domain catalog. "
                + "Author SubstanceItemFeature on ItemDefinitionSO assets instead.");
        }
    }

    private static WasteProcessingRulesSO EnsureWasteProcessingRules()
    {
        RecordOutput(WasteRulesPath);
        WasteProcessingRulesSO existing =
            AssetDatabase.LoadAssetAtPath<WasteProcessingRulesSO>(WasteRulesPath);
        if (existing != null)
        {
            return existing;
        }

        WasteProcessingRulesSO created =
            ScriptableObject.CreateInstance<WasteProcessingRulesSO>();
        created.name = "WasteProcessingRules";
        AssetDatabase.CreateAsset(created, WasteRulesPath);

        SerializedObject serialized = new(created);
        serialized.FindProperty("tickIntervalSeconds").floatValue = 10f;
        serialized.FindProperty("toxicThreshold").floatValue = 80f;

        SerializedProperty origins = serialized.FindProperty("origins");
        origins.arraySize = 4;
        ConfigureOrigin(origins.GetArrayElementAtIndex(0), WasteOriginKind.Plant,
            WasteDispositionKind.Compost, WasteDispositionKind.Store,
            WasteDispositionKind.DirectFeed, WasteDispositionKind.Compost,
            WasteDispositionKind.Fuel, WasteDispositionKind.Incinerate);
        ConfigureOrigin(origins.GetArrayElementAtIndex(1), WasteOriginKind.Animal,
            WasteDispositionKind.DirectFeed, WasteDispositionKind.Store,
            WasteDispositionKind.DirectFeed, WasteDispositionKind.Compost,
            WasteDispositionKind.Fuel, WasteDispositionKind.Incinerate);
        ConfigureOrigin(origins.GetArrayElementAtIndex(2), WasteOriginKind.Mixed,
            WasteDispositionKind.Fuel, WasteDispositionKind.Store,
            WasteDispositionKind.DirectFeed, WasteDispositionKind.Compost,
            WasteDispositionKind.Fuel, WasteDispositionKind.Incinerate);
        ConfigureOrigin(origins.GetArrayElementAtIndex(3), WasteOriginKind.Forbidden,
            WasteDispositionKind.Alchemy, WasteDispositionKind.Store,
            WasteDispositionKind.Alchemy, WasteDispositionKind.Incinerate);

        (WasteOriginKind Origin, WasteDispositionKind Disposition, string Recipe)[]
            recipes =
            {
                (WasteOriginKind.Plant, WasteDispositionKind.Compost,
                    "recipe:compost-plant"),
                (WasteOriginKind.Animal, WasteDispositionKind.Compost,
                    "recipe:compost-animal"),
                (WasteOriginKind.Mixed, WasteDispositionKind.Compost,
                    "recipe:compost-mixed"),
                (WasteOriginKind.Plant, WasteDispositionKind.Fuel,
                    "recipe:low-fuel-plant"),
                (WasteOriginKind.Animal, WasteDispositionKind.Fuel,
                    "recipe:low-fuel-animal"),
                (WasteOriginKind.Mixed, WasteDispositionKind.Fuel,
                    "recipe:low-fuel-rot"),
                (WasteOriginKind.Forbidden, WasteDispositionKind.Alchemy,
                    "recipe:rot-toxin"),
                (WasteOriginKind.Plant, WasteDispositionKind.Incinerate,
                    "recipe:incinerate-plant"),
                (WasteOriginKind.Animal, WasteDispositionKind.Incinerate,
                    "recipe:incinerate-animal"),
                (WasteOriginKind.Mixed, WasteDispositionKind.Incinerate,
                    "recipe:incinerate-mixed"),
                (WasteOriginKind.Forbidden, WasteDispositionKind.Incinerate,
                    "recipe:incinerate-forbidden")
            };
        SerializedProperty recipeRecords = serialized.FindProperty("recipes");
        recipeRecords.arraySize = recipes.Length;
        for (int index = 0; index < recipes.Length; index++)
        {
            SerializedProperty record = recipeRecords.GetArrayElementAtIndex(index);
            record.FindPropertyRelative("origin").enumValueIndex =
                (int)recipes[index].Origin;
            record.FindPropertyRelative("disposition").enumValueIndex =
                (int)recipes[index].Disposition;
            record.FindPropertyRelative("recipeId").stringValue =
                recipes[index].Recipe;
        }

        (WildlifeDietType Diet, WasteOriginKind Origin, float Nutrition,
            float DiseaseChance)[] feeds =
            {
                (WildlifeDietType.Herbivore, WasteOriginKind.Plant, 0.5f, 0.12f),
                (WildlifeDietType.Carnivore, WasteOriginKind.Animal, 0.65f, 0.1f),
                (WildlifeDietType.Carnivore, WasteOriginKind.Mixed, 0.65f, 0.1f),
                (WildlifeDietType.Omnivore, WasteOriginKind.Plant, 0.6f, 0.08f),
                (WildlifeDietType.Omnivore, WasteOriginKind.Animal, 0.6f, 0.08f),
                (WildlifeDietType.Omnivore, WasteOriginKind.Mixed, 0.6f, 0.08f),
                (WildlifeDietType.Scavenger, WasteOriginKind.Plant, 0.85f, 0.02f),
                (WildlifeDietType.Scavenger, WasteOriginKind.Animal, 0.85f, 0.02f),
                (WildlifeDietType.Scavenger, WasteOriginKind.Mixed, 0.85f, 0.02f),
                (WildlifeDietType.Scavenger, WasteOriginKind.Forbidden, 0.85f, 0.02f)
            };
        SerializedProperty feedRecords = serialized.FindProperty("feedRules");
        feedRecords.arraySize = feeds.Length;
        for (int index = 0; index < feeds.Length; index++)
        {
            SerializedProperty record = feedRecords.GetArrayElementAtIndex(index);
            record.FindPropertyRelative("diet").enumValueIndex =
                (int)feeds[index].Diet;
            record.FindPropertyRelative("origin").enumValueIndex =
                (int)feeds[index].Origin;
            record.FindPropertyRelative("nutrition").floatValue =
                feeds[index].Nutrition;
            record.FindPropertyRelative("diseaseChance").floatValue =
                feeds[index].DiseaseChance;
        }

        (string ItemId, WasteOriginKind Origin)[] legacy =
            {
                ("waste:plant-rot", WasteOriginKind.Plant),
                ("waste:animal-rot", WasteOriginKind.Animal),
                ("waste:mixed-rot", WasteOriginKind.Mixed),
                ("waste:forbidden-rot", WasteOriginKind.Forbidden),
                ("wild:rot", WasteOriginKind.Mixed)
            };
        SerializedProperty legacyRecords = serialized.FindProperty("legacyItems");
        legacyRecords.arraySize = legacy.Length;
        for (int index = 0; index < legacy.Length; index++)
        {
            SerializedProperty record = legacyRecords.GetArrayElementAtIndex(index);
            record.FindPropertyRelative("itemId").stringValue = legacy[index].ItemId;
            record.FindPropertyRelative("origin").enumValueIndex =
                (int)legacy[index].Origin;
            record.FindPropertyRelative("contamination").floatValue = 50f;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(created);
        return created;
    }

    private static void ConfigureOrigin(
        SerializedProperty record,
        WasteOriginKind origin,
        WasteDispositionKind defaultDisposition,
        params WasteDispositionKind[] supported)
    {
        record.FindPropertyRelative("origin").enumValueIndex = (int)origin;
        record.FindPropertyRelative("defaultDisposition").enumValueIndex =
            (int)defaultDisposition;
        record.FindPropertyRelative("defaultMaximumFeedContamination").floatValue =
            79f;
        SerializedProperty dispositions =
            record.FindPropertyRelative("supportedDispositions");
        dispositions.arraySize = supported.Length;
        for (int index = 0; index < supported.Length; index++)
        {
            dispositions.GetArrayElementAtIndex(index).enumValueIndex =
                (int)supported[index];
        }
    }

    private static void EnsurePhysicalOfferDefinitions()
    {
        EnsureEvolutionCatalystDefinitions();

        foreach (BuildingSO building in FindAssets<BuildingSO>().Where(building => building.id > 0))
        {
            string itemId = FacilityInstallationKitItemIds.ForBuilding(building);
            GenericItemDefinitionSO item = GetOrCreateGenerated(itemId);
            item.ConfigureCore(
                itemId,
                $"{FacilityShopService.GetBuildingName(building)} 설치 키트",
                "시설을 건설하기 위한 실제 부품과 조립 자재 묶음.",
                StockCategory.General,
                Mathf.Max(1, building.GetConstructionValue()),
                8f,
                1);
            item.SetFeature(new ProductionItemFeature
            {
                kind = ResourceItemKind.FinishedGood
            });
            item.SetFeature(new InstallationItemFeature
            {
                buildingDefinitionId = building.id
            });
            EditorUtility.SetDirty(item);
        }

        foreach (FacilityBlueprintSO blueprint in FindAssets<FacilityBlueprintSO>())
        {
            GenericItemDefinitionSO item = GetOrCreateGenerated(blueprint.PhysicalItemId);
            string displayName = blueprint.DisplayName.EndsWith(
                "설계도",
                StringComparison.Ordinal)
                ? blueprint.DisplayName
                : $"{blueprint.DisplayName} 설계도";
            item.ConfigureCore(
                blueprint.PhysicalItemId,
                displayName,
                string.IsNullOrWhiteSpace(blueprint.description)
                    ? "연구실 보관대에 배치하면 연구 조건을 충족합니다."
                    : blueprint.description,
                StockCategory.Blueprint,
                Mathf.Max(1, blueprint.defaultCost),
                0.15f,
                1);
            item.SetFeature(new ProductionItemFeature
            {
                kind = ResourceItemKind.FinishedGood
            });
            item.SetFeature(new BlueprintItemFeature
            {
                blueprintDefinitionId = blueprint.id,
                targetResearchId = blueprint.TargetResearchProjectId
            });
            EditorUtility.SetDirty(item);
        }

    }

    private static void EnsureEvolutionCatalystDefinitions()
    {
        string[] families =
        {
            "offense",
            "defense",
            "industry",
            "survival",
            "arcane",
            "authority",
            "universal"
        };

        for (int progressionLevel = 1;
             progressionLevel <= EvolutionCatalystProgression.MaximumLevel;
             progressionLevel++)
        {
            int potency = EvolutionCatalystProgression.GetPotencyGrade(
                progressionLevel);
            foreach (string family in families)
            {
                string itemId = EvolutionCatalystItemId.BuildCatalyst(
                    family,
                    progressionLevel);
                GenericItemDefinitionSO catalyst = GetOrCreateGenerated(itemId);
                catalyst.ConfigureCore(
                    itemId,
                    $"{EvolutionCatalystItemDefinitions.GetFamilyDisplayName(family)} 촉매 진행 {progressionLevel} · {potency}등급",
                    "시설 개조와 장비 조율에 사용하는 진화 촉매.",
                    StockCategory.General,
                    EvolutionCatalystItemDefinitions.GetCatalystValue(
                        progressionLevel),
                    0.25f,
                    20);
                catalyst.SetFeature(new ProductionItemFeature
                {
                    kind = ResourceItemKind.FinishedGood
                });
                catalyst.SetFeature(new EvolutionCatalystItemFeature
                {
                    family = family,
                    potency = potency,
                    residue = false
                });
                EditorUtility.SetDirty(catalyst);
            }

            string residueId = EvolutionCatalystItemId.BuildResidue(
                progressionLevel);
            GenericItemDefinitionSO residue = GetOrCreateGenerated(residueId);
            residue.ConfigureCore(
                residueId,
                $"범용 촉매 잔재 진행 {progressionLevel} · {potency}등급",
                "촉매를 분해해 얻은 잔재. 정제하거나 다음 진행 단계로 합칠 수 있다.",
                StockCategory.General,
                Mathf.Max(
                    1,
                    EvolutionCatalystItemDefinitions.GetCatalystValue(
                        progressionLevel) / 3),
                0.1f,
                75);
            residue.SetFeature(new ProductionItemFeature
            {
                kind = ResourceItemKind.FinishedGood
            });
            residue.SetFeature(new EvolutionCatalystItemFeature
            {
                family = "universal",
                potency = potency,
                residue = true
            });
            EditorUtility.SetDirty(residue);
        }
    }

    private static T[] FindAssets<T>() where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Resources/SO" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .Distinct()
            .ToArray();
    }

    private static GenericItemDefinitionSO GetOrCreateGenerated(string itemId)
    {
        const string generatedFolder = "Assets/Resources/SO/Items/Definitions";
        string fileName = string.Concat((itemId ?? string.Empty).Select(character =>
            char.IsLetterOrDigit(character) ? character : '_'));
        string path = $"{generatedFolder}/{fileName}.asset";
        RecordOutput(path);
        GenericItemDefinitionSO asset =
            AssetDatabase.LoadAssetAtPath<GenericItemDefinitionSO>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<GenericItemDefinitionSO>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        RecordOutput(path);
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static T RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        return AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException(
                $"Required content asset is missing: {path}");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void ValidateGeneratedCatalogsBeforeSave(
        GameContentCatalogSO root,
        ItemDefinitionCatalogSO itemCatalog,
        GameDomainContentCatalogSO domainCatalog,
        WasteProcessingRulesSO wasteRules)
    {
        List<string> errors = new();
        errors.AddRange(root.ValidateCatalog());
        errors.AddRange(itemCatalog.ValidateCatalog());
        errors.AddRange(domainCatalog.ValidateCatalog());
        errors.AddRange(wasteRules.ValidateDefinition());
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Generated content validation failed before owned assets were saved:\n"
                + string.Join("\n", errors));
        }
    }

    private static void RequireNoDirtyOwnedAssets()
    {
        string[] dirtyPaths = AssetDatabase.GetAllAssetPaths()
            .Where(IsPotentialOwnedOutputPath)
            .Concat(
                DomainFailureLocalizationAssetBuilder
                    .GetPotentialOutputPathsForPreflight())
            .Distinct(StringComparer.Ordinal)
            .Select(path => new
            {
                Path = path,
                Asset = AssetDatabase.LoadMainAssetAtPath(path)
            })
            .Where(entry => entry.Asset != null && EditorUtility.IsDirty(entry.Asset))
            .Select(entry => entry.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (dirtyPaths.Length > 0)
        {
            throw new InvalidOperationException(
                "Content migration-owned assets already have unsaved changes. "
                + "Save or revert them before running the explicit migration:\n"
                + string.Join("\n", dirtyPaths));
        }
    }

    private static bool IsPotentialOwnedOutputPath(string path)
    {
        return path.StartsWith(
                "Assets/Resources/SO/Items/Definitions/",
                StringComparison.Ordinal)
            || string.Equals(path, ItemCatalogPath, StringComparison.Ordinal)
            || string.Equals(path, DomainCatalogPath, StringComparison.Ordinal)
            || string.Equals(path, MediaCatalogPath, StringComparison.Ordinal)
            || string.Equals(path, WasteRulesPath, StringComparison.Ordinal)
            || string.Equals(path, RootCatalogPath, StringComparison.Ordinal);
    }

    private static void RecordOutput(string path)
    {
        activeOutputPaths?.Add(path);
        activeTouchedOutputPaths?.Add(path);
    }

    private static void RecordTouchedOutput(string path) =>
        activeTouchedOutputPaths?.Add(path);

    private static void SaveOwnedOutputs()
    {
        foreach (string path in activeOutputPaths.OrderBy(value => value, StringComparer.Ordinal))
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null)
            {
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
        AssetDatabase.Refresh();
    }

    private static string[] GetDeterministicInputPaths()
    {
        string[] missingDependencies = GeneratorDependencySourcePaths
            .Where(path => !File.Exists(path))
            .ToArray();
        if (missingDependencies.Length > 0)
        {
            throw new InvalidOperationException(
                "Generated-content provenance dependency is missing:\n"
                + string.Join("\n", missingDependencies));
        }

        HashSet<string> generatorDependencies = new(
            GeneratorDependencySourcePaths,
            StringComparer.Ordinal);
        return AssetDatabase.GetAllAssetPaths()
            .Where(path => path.StartsWith("Assets/Resources/", StringComparison.Ordinal)
                || generatorDependencies.Contains(path))
            .Where(path => File.Exists(path) && !IsGeneratedOutputPath(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsGeneratedOutputPath(string path)
    {
        return path.StartsWith(
                "Assets/Resources/SO/Items/Definitions/",
                StringComparison.Ordinal)
            || string.Equals(path, ItemCatalogPath, StringComparison.Ordinal)
            || string.Equals(path, DomainCatalogPath, StringComparison.Ordinal)
            || string.Equals(path, MediaCatalogPath, StringComparison.Ordinal)
            || string.Equals(path, WasteRulesPath, StringComparison.Ordinal)
            || string.Equals(path, RootCatalogPath, StringComparison.Ordinal);
    }

    private static string ComputeAggregateHash(
        IEnumerable<string> paths,
        string canonicalLocalizationProjection = null)
    {
        StringBuilder fingerprint = new StringBuilder();
        foreach (string path in paths)
        {
            fingerprint.Append(path)
                .Append('|').Append(ComputeFileHash(path))
                .Append('|').Append(AssetDatabase.AssetPathToGUID(path))
                .Append('|').Append(ComputeMetaFileHash(path))
                .Append('\n');
        }
        if (canonicalLocalizationProjection != null)
        {
            fingerprint.Append("@canonical/domain-failure-localization")
                .Append('|')
                .Append(ComputeHash(Encoding.UTF8.GetBytes(
                    canonicalLocalizationProjection)))
                .Append('\n');
        }
        return ComputeHash(Encoding.UTF8.GetBytes(fingerprint.ToString()));
    }

    private static string ComputeFileHash(string path) =>
        ComputeHash(File.ReadAllBytes(path));

    private static string ComputeMetaFileHash(string assetPath)
    {
        string metaPath = assetPath + ".meta";
        return File.Exists(metaPath) ? ComputeFileHash(metaPath) : string.Empty;
    }

    private static string ComputeHash(byte[] bytes)
    {
        using SHA256 sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(bytes))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void WriteGenerationManifest(
        string invocationKind,
        IReadOnlyCollection<string> inputPaths,
        string inputHash,
        string localizationContractHash)
    {
        string[] outputPaths = activeOutputPaths
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        GenerationManifest manifest = new GenerationManifest
        {
            manifestVersion = GenerationManifestVersion,
            generator = nameof(GameContentCatalogAssetBuilder),
            generatorVersion = GeneratorVersion,
            unityVersion = Application.unityVersion,
            invocationKind = invocationKind,
            hashAlgorithm =
                "SHA-256(path|file-sha256|asset-guid|meta-sha256, ordinal path order; canonical localization projection)",
            generatorSourceHashSha256 = ComputeAggregateHash(
                GeneratorDependencySourcePaths),
            inputCount = inputPaths.Count,
            inputHashSha256 = inputHash,
            localizationContractHashSha256 = localizationContractHash,
            outputPaths = outputPaths,
            outputHashes = outputPaths
                .Where(File.Exists)
                .Select(path => new GeneratedOutputHash
                {
                    path = path,
                    sha256 = ComputeFileHash(path),
                    guid = AssetDatabase.AssetPathToGUID(path),
                    metaSha256 = ComputeMetaFileHash(path)
                })
                .ToArray()
        };

        string directory = Path.GetDirectoryName(GenerationManifestPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string temporaryPath = GenerationManifestPath + ".tmp";
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonUtility.ToJson(manifest, true) + Environment.NewLine,
                new UTF8Encoding(false));
            if (File.Exists(GenerationManifestPath))
            {
                File.Replace(temporaryPath, GenerationManifestPath, null);
            }
            else
            {
                File.Move(temporaryPath, GenerationManifestPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    [Serializable]
    private sealed class GenerationManifest
    {
        public int manifestVersion;
        public string generator;
        public string generatorVersion;
        public string unityVersion;
        public string invocationKind;
        public string hashAlgorithm;
        public string generatorSourceHashSha256;
        public int inputCount;
        public string inputHashSha256;
        public string localizationContractHashSha256;
        public string[] outputPaths;
        public GeneratedOutputHash[] outputHashes;
    }

    [Serializable]
    private sealed class GeneratedOutputHash
    {
        public string path;
        public string sha256;
        public string guid;
        public string metaSha256;
    }
}
#endif
