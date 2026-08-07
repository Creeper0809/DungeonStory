#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V19ClimateContentAssetBuilder
{
    private const string DomainCatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string WorldFolder = "Assets/Resources/SO/World";
    private const string ClimateFolder = WorldFolder + "/Climate";

    private readonly struct ZoneManifest
    {
        public ZoneManifest(string id, string name, float mean, float amplitude)
        {
            Id = id;
            Name = name;
            Mean = mean;
            Amplitude = amplitude;
        }
        public string Id { get; }
        public string Name { get; }
        public float Mean { get; }
        public float Amplitude { get; }
    }

    private readonly struct FrontManifest
    {
        public FrontManifest(
            string id,
            string name,
            WeatherFrontKind kind,
            int minimumDays,
            int maximumDays,
            float modifier,
            float spring,
            float summer,
            float autumn,
            float winter)
        {
            Id = id;
            Name = name;
            Kind = kind;
            MinimumDays = minimumDays;
            MaximumDays = maximumDays;
            Modifier = modifier;
            Weights = new[] { spring, summer, autumn, winter };
        }
        public string Id { get; }
        public string Name { get; }
        public WeatherFrontKind Kind { get; }
        public int MinimumDays { get; }
        public int MaximumDays { get; }
        public float Modifier { get; }
        public float[] Weights { get; }
    }

    private static readonly ZoneManifest[] Zones =
    {
        new("climate:temperate-cave", "온대 동굴", 14f, 14f),
        new("climate:frost-rift", "서리 균열", 0f, 12f),
        new("climate:ember-wastes", "잿불 황무지", 27f, 8f),
        new("climate:mycelial-depths", "균사 심층", 16f, 5f),
        new("climate:mana-stormlands", "마나 폭풍지", 12f, 16f)
    };

    private static readonly FrontManifest[] Fronts =
    {
        new("weather:clear", "맑음", WeatherFrontKind.Clear, 1, 3, 0f, 35, 45, 30, 35),
        new("weather:rain", "비", WeatherFrontKind.Rain, 2, 4, -3f, 30, 15, 25, 8),
        new("weather:fog", "안개", WeatherFrontKind.Fog, 1, 3, -2f, 15, 8, 20, 18),
        new("weather:heatwave", "폭염", WeatherFrontKind.Heatwave, 2, 5, 10f, 2, 18, 2, 0),
        new("weather:cold-snap", "한파", WeatherFrontKind.ColdSnap, 2, 5, -12f, 8, 1, 8, 30),
        new("weather:storm", "폭풍", WeatherFrontKind.Storm, 1, 2, -6f, 10, 13, 15, 9)
    };

    [MenuItem("DungeonStory/V19/Build Climate Content")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources/SO", "World");
        EnsureFolder(WorldFolder, "Climate");
        GameDomainContentCatalogSO domain = AssetDatabase.LoadAssetAtPath<
            GameDomainContentCatalogSO>(DomainCatalogPath)
            ?? throw new InvalidOperationException(
                $"Required domain catalog is missing at '{DomainCatalogPath}'.");
        List<ScriptableObject> authored = new();
        foreach (ZoneManifest manifest in Zones)
        {
            ClimateZoneDefinitionSO asset = CreateRequired<ClimateZoneDefinitionSO>(
                $"{ClimateFolder}/ClimateZone_{manifest.Id.Split(':')[1]}.asset");
            asset.stableId = manifest.Id;
            asset.displayName = manifest.Name;
            asset.meanTemperatureC = manifest.Mean;
            asset.annualAmplitudeC = manifest.Amplitude;
            asset.localHourOffset = 0;
            RequireValid(asset.ValidateDefinition(), asset.name);
            EditorUtility.SetDirty(asset);
            authored.Add(asset);
        }
        foreach (FrontManifest manifest in Fronts)
        {
            WeatherFrontDefinitionSO asset = CreateRequired<WeatherFrontDefinitionSO>(
                $"{ClimateFolder}/WeatherFront_{manifest.Id.Split(':')[1]}.asset");
            asset.stableId = manifest.Id;
            asset.displayName = manifest.Name;
            asset.kind = manifest.Kind;
            asset.minimumDurationDays = manifest.MinimumDays;
            asset.maximumDurationDays = manifest.MaximumDays;
            asset.temperatureModifierC = manifest.Modifier;
            asset.springWeight = manifest.Weights[0];
            asset.summerWeight = manifest.Weights[1];
            asset.autumnWeight = manifest.Weights[2];
            asset.winterWeight = manifest.Weights[3];
            RequireValid(asset.ValidateDefinition(), asset.name);
            EditorUtility.SetDirty(asset);
            authored.Add(asset);
        }
        domain.SetDefinitions(domain.Definitions.Concat(authored));
        EditorUtility.SetDirty(domain);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        RequireValid(domain.ValidateCatalog(), domain.name);
        Debug.Log($"V19_CLIMATE_CONTENT=PASS; zones={Zones.Length}; fronts={Fronts.Length}");
    }

    private static T CreateRequired<T>(string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            if (MonoScript.FromScriptableObject(existing) != null) return existing;
            if (!path.StartsWith(ClimateFolder + "/", StringComparison.Ordinal)
                || !AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException($"Broken climate asset '{path}' could not be replaced.");
        }
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            if (!path.StartsWith(ClimateFolder + "/", StringComparison.Ordinal)
                || !AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException($"Climate asset path '{path}' is occupied.");
        }
        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static void RequireValid(IReadOnlyList<string> errors, string label)
    {
        if (errors.Count > 0)
            throw new InvalidOperationException(label + ": " + string.Join(" | ", errors));
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
