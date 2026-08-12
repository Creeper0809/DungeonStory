#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V19CropEcologyContentAssetBuilder
{
    private const string Root = "Assets/Resources/SO/Economy";
    private const string CropFolder = Root + "/Crops";
    private const string ItemFolder = Root + "/Items";
    private const string GenomeFolder = Root + "/CropGenomes";
    private const string ItemCatalogPath =
        "Assets/Resources/SO/Content/ItemDefinitionCatalog.asset";
    private const string DomainCatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";

    private readonly struct CropEcologySpec
    {
        public CropEcologySpec(
            string cropId,
            string seedName,
            CropFamilyGroup group,
            CropDiseaseKind disease)
        {
            CropId = cropId;
            Suffix = cropId.Replace("crop:", string.Empty);
            SeedName = seedName;
            Group = group;
            Disease = disease;
        }
        public string CropId { get; }
        public string Suffix { get; }
        public string SeedName { get; }
        public string SeedItemId => "seed-lot:" + Suffix;
        public string GenomeId => "genome:" + Suffix + ":base";
        public CropFamilyGroup Group { get; }
        public CropDiseaseKind Disease { get; }
    }

    private static readonly CropEcologySpec[] Specs =
    {
        new("crop:twilight-grain", "황혼곡 종자", CropFamilyGroup.Grain, CropDiseaseKind.GrainFiberRust),
        new("crop:ember-root", "잿불뿌리 종자", CropFamilyGroup.Root, CropDiseaseKind.RootRot),
        new("crop:night-grape", "밤포도 종자", CropFamilyGroup.Vine, CropDiseaseKind.LeafVinePowderyMildew),
        new("crop:cave-mushroom", "동굴버섯 종균", CropFamilyGroup.Fungus, CropDiseaseKind.MushroomSporeMold),
        new("crop:bloodleaf", "혈엽 종자", CropFamilyGroup.Leaf, CropDiseaseKind.LeafVinePowderyMildew),
        new("crop:moonflower", "월화 종자", CropFamilyGroup.Leaf, CropDiseaseKind.LeafVinePowderyMildew),
        new("crop:dreamleaf", "몽엽 종자", CropFamilyGroup.Leaf, CropDiseaseKind.LeafVinePowderyMildew),
        new("crop:shade-fiber", "그늘섬유 종자", CropFamilyGroup.Fiber, CropDiseaseKind.GrainFiberRust)
    };

    [MenuItem("DungeonStory/V19/Build Crop Ecology Content")]
    public static void Build()
    {
        EnsureFolder(Root, "CropGenomes");
        ItemDefinitionCatalogSO itemCatalog = AssetDatabase.LoadAssetAtPath<ItemDefinitionCatalogSO>(ItemCatalogPath)
            ?? throw new InvalidOperationException($"Required item catalog is missing at '{ItemCatalogPath}'.");
        GameDomainContentCatalogSO domainCatalog = AssetDatabase.LoadAssetAtPath<GameDomainContentCatalogSO>(DomainCatalogPath)
            ?? throw new InvalidOperationException($"Required domain catalog is missing at '{DomainCatalogPath}'.");
        List<ItemDefinitionSO> seeds = new();
        List<ScriptableObject> genomes = new();
        foreach (CropEcologySpec spec in Specs)
        {
            CropDefinitionSO crop = AssetDatabase.LoadAssetAtPath<CropDefinitionSO>(
                $"{CropFolder}/{spec.Suffix.Replace('-', '_')}.asset")
                ?? AssetDatabase.FindAssets("t:CropDefinitionSO", new[] { CropFolder })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<CropDefinitionSO>)
                    .SingleOrDefault(value => value != null && value.CropId == spec.CropId)
                ?? throw new InvalidOperationException($"Required crop '{spec.CropId}' is missing.");
            CropGenomeDefinitionSO genome = GetOrCreate<CropGenomeDefinitionSO>(
                $"{GenomeFolder}/Genome_{spec.Suffix}.asset");
            genome.id = ResolveGenomeNumericId(spec.GenomeId);
            genome.Configure(spec.GenomeId, spec.CropId, NeutralLoci());
            ResourceItemDefinitionSO seed = GetOrCreate<ResourceItemDefinitionSO>(
                $"{ItemFolder}/seed_lot_{spec.Suffix.Replace('-', '_')}.asset");
            seed.id = ResolveSeedNumericId(spec.SeedItemId);
            seed.Configure(
                spec.SeedItemId,
                spec.SeedName,
                $"{crop.DisplayName} 재배에 사용하는 물리 종자 로트.",
                StockCategory.General,
                ResourceItemKind.Raw,
                spec.Group == CropFamilyGroup.Fungus
                    ? ResourceIngredientTag.Fungus
                    : ResourceIngredientTag.Plant,
                3,
                0.05f,
                40,
                crop.RequiredResearchId);
            seed.ConfigureMarketSaleRate(0f);
            crop.ConfigureEcology(spec.SeedItemId, genome, spec.Group, spec.Disease);
            EditorUtility.SetDirty(genome);
            EditorUtility.SetDirty(seed);
            EditorUtility.SetDirty(crop);
            seeds.Add(seed);
            genomes.Add(genome);
        }
        itemCatalog.SetDefinitions(itemCatalog.Definitions.Concat(seeds));
        domainCatalog.SetDefinitions(domainCatalog.Definitions.Concat(genomes));
        EditorUtility.SetDirty(itemCatalog);
        EditorUtility.SetDirty(domainCatalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"V19_CROP_ECOLOGY_CONTENT=PASS; crops={Specs.Length}; seeds={seeds.Count}; genomes={genomes.Count}");
    }

    private static IReadOnlyList<DiploidLocusSaveData> NeutralLoci() =>
        Enum.GetValues(typeof(CropGenomeLocus)).Cast<CropGenomeLocus>()
            .Select(value => new DiploidLocusSaveData { locus = value }).ToArray();

    private static int ResolveGenomeNumericId(string genomeId) => genomeId switch
    {
        "genome:twilight-grain:base" => 93001,
        "genome:ember-root:base" => 93002,
        "genome:night-grape:base" => 93003,
        "genome:cave-mushroom:base" => 93004,
        "genome:bloodleaf:base" => 93005,
        "genome:moonflower:base" => 93006,
        "genome:dreamleaf:base" => 93007,
        "genome:shade-fiber:base" => 93008,
        _ => throw new InvalidOperationException(
            $"Crop genome '{genomeId}' has no stable numeric compatibility ID.")
    };

    private static int ResolveSeedNumericId(string itemId) => itemId switch
    {
        "seed-lot:twilight-grain" => 93011,
        "seed-lot:ember-root" => 93012,
        "seed-lot:night-grape" => 93013,
        "seed-lot:cave-mushroom" => 93014,
        "seed-lot:bloodleaf" => 93015,
        "seed-lot:moonflower" => 93016,
        "seed-lot:dreamleaf" => 93017,
        "seed-lot:shade-fiber" => 93018,
        _ => throw new InvalidOperationException(
            $"Seed lot '{itemId}' has no stable numeric compatibility ID.")
    };

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            throw new InvalidOperationException($"Content path '{path}' is occupied by the wrong type.");
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
