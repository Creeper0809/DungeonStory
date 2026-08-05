#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GameContentCatalogAssetBuilder
{
    private const string ContentFolder = "Assets/Resources/SO/Content";
    private const string ItemCatalogPath = ContentFolder + "/ItemDefinitionCatalog.asset";
    private const string DomainCatalogPath = ContentFolder + "/GameDomainContentCatalog.asset";
    private const string MediaCatalogPath = ContentFolder + "/GameMediaCatalog.asset";
    private const string WasteRulesPath =
        "Assets/Resources/SO/Economy/WasteProcessingRules.asset";
    private const string LegacySubstanceRoot =
        "Assets/Resources/SO/Economy/Substances";
    private const string RootCatalogPath = "Assets/Resources/SO/GameContentCatalog.asset";

    [MenuItem("Tools/DungeonStory/Content/Rebuild Explicit Content Catalog")]
    public static void Rebuild()
    {
        EnsureFolder("Assets/Resources/SO", "Content");
        ValidateNoLegacySubstanceAssets();
        EnsurePhysicalOfferDefinitions();
        WasteProcessingRulesSO wasteRules = EnsureWasteProcessingRules();
        DomainFailureLocalizationAssetBuilder.Rebuild();

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
                && asset is not GameMediaCatalogSO)
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

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (itemCatalog.ValidateCatalog().Count > 0)
        {
            throw new InvalidOperationException(
                string.Join("\n", itemCatalog.ValidateCatalog()));
        }

        Debug.Log(
            $"Explicit content catalog rebuilt: {definitions.Length} item definitions, "
            + $"{domainDefinitions.Length} domain definitions, one required Resources root.");
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

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
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
}
#endif
