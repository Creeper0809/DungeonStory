#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V19DiseaseContentAssetBuilder
{
    private const string DomainCatalogPath =
        "Assets/Resources/SO/Content/GameDomainContentCatalog.asset";
    private const string PopulationFolder = "Assets/Resources/SO/Population";
    private const string DiseaseFolder = PopulationFolder + "/Diseases";

    private readonly struct Manifest
    {
        public Manifest(
            string id, string name, DiseaseTransmissionRoute routes,
            int incubation, int contagious, float infection, float severity,
            DiseaseTargetSystem target, bool vaccine, bool chronic = false)
        {
            Id = id; Name = name; Routes = routes; Incubation = incubation;
            Contagious = contagious; Infection = infection; Severity = severity;
            Target = target; Vaccine = vaccine;
            Chronic = chronic;
        }
        public string Id { get; }
        public string Name { get; }
        public DiseaseTransmissionRoute Routes { get; }
        public int Incubation { get; }
        public int Contagious { get; }
        public float Infection { get; }
        public float Severity { get; }
        public DiseaseTargetSystem Target { get; }
        public bool Vaccine { get; }
        public bool Chronic { get; }
    }

    private static readonly Manifest[] Diseases =
    {
        new("disease:cave-flu", "동굴 독감", DiseaseTransmissionRoute.Air,
            2, 6, 0.18f, 25f, DiseaseTargetSystem.Breathing, true),
        new("disease:red-fever", "적열병", DiseaseTransmissionRoute.Droplet | DiseaseTransmissionRoute.Blood,
            3, 10, 0.10f, 55f, DiseaseTargetSystem.Filtration, true),
        new("disease:gut-rot", "장부패증", DiseaseTransmissionRoute.Food | DiseaseTransmissionRoute.Water,
            1, 5, 0.25f, 45f, DiseaseTargetSystem.Digestion, true),
        new("disease:spore-lung", "포자폐증", DiseaseTransmissionRoute.Air,
            4, 12, 0.12f, 50f, DiseaseTargetSystem.Breathing, true),
        new("disease:mana-pox", "마나두창", DiseaseTransmissionRoute.ManaExposure,
            5, 10, 0.08f, 60f, DiseaseTargetSystem.Core, true),
        new("disease:blood-wasting", "혈액소모병", DiseaseTransmissionRoute.Blood,
            7, 20, 0.20f, 70f, DiseaseTargetSystem.Filtration, true),
        new("disease:slime-blight", "점액역병", DiseaseTransmissionRoute.Contact | DiseaseTransmissionRoute.Water,
            2, 10, 0.15f, 65f, DiseaseTargetSystem.Core, true),
        new("condition:core-corrosion", "핵 부식", DiseaseTransmissionRoute.Environment,
            0, 0, 0f, 60f, DiseaseTargetSystem.Core, false, chronic: true)
    };

    [MenuItem("DungeonStory/V19/Build Disease Content")]
    public static void Build()
    {
        EnsureFolder(PopulationFolder, "Diseases");
        GameDomainContentCatalogSO domain = AssetDatabase.LoadAssetAtPath<
            GameDomainContentCatalogSO>(DomainCatalogPath)
            ?? throw new InvalidOperationException(
                $"Required domain catalog is missing at '{DomainCatalogPath}'.");
        List<ScriptableObject> authored = new();
        foreach (Manifest manifest in Diseases)
        {
            string suffix = manifest.Id.Replace(':', '-');
            DiseaseDefinitionSO asset = CreateRequired<DiseaseDefinitionSO>(
                $"{DiseaseFolder}/Disease_{suffix}.asset");
            asset.stableId = manifest.Id;
            asset.displayName = manifest.Name;
            asset.routes = manifest.Routes;
            asset.incubationDays = manifest.Incubation;
            asset.contagiousDays = manifest.Contagious;
            asset.baseInfectionProbability = manifest.Infection;
            asset.baseSeverity = manifest.Severity;
            asset.targetSystem = manifest.Target;
            asset.vaccineAllowed = manifest.Vaccine;
            asset.chronic = manifest.Chronic;
            IReadOnlyList<string> errors = asset.ValidateDefinition();
            if (errors.Count > 0)
                throw new InvalidOperationException(asset.name + ": " + string.Join(" | ", errors));
            EditorUtility.SetDirty(asset);
            authored.Add(asset);
        }
        domain.SetDefinitions(domain.Definitions.Concat(authored));
        EditorUtility.SetDirty(domain);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"V19_DISEASE_CONTENT=PASS; definitions={Diseases.Length}");
    }

    private static T CreateRequired<T>(string path) where T : ScriptableObject
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
        {
            if (MonoScript.FromScriptableObject(existing) != null) return existing;
            if (!path.StartsWith(DiseaseFolder + "/", StringComparison.Ordinal)
                || !AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException($"Broken disease asset '{path}' could not be replaced.");
        }
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
        {
            if (!path.StartsWith(DiseaseFolder + "/", StringComparison.Ordinal)
                || !AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException($"Disease asset path '{path}' is occupied.");
        }
        T created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
