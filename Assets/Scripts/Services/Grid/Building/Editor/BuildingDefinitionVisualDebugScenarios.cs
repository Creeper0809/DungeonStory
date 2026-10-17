#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sirenix.Serialization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;
using SerializationUtility = Sirenix.Serialization.SerializationUtility;

public static class BuildingDefinitionVisualDebugScenarios
{
    private const string LegacyEnum = "GridTexture+TilemapLayer, Assembly-CSharp";
    private const string TextureScriptGuid = "3f70a6933cca9404ea4bc6eaf7c28e08";

    public static void RunForBatch()
    {
        Require(Application.isBatchMode, "Building visual verification requires isolated execution.");
        VerifyLegacyTypeBindings();
        VerifyArchetypeComponents();
        var rows = new List<string> { "kind\tpath\tentries\tresult" };
        foreach (string name in new[] { "Door", "InteriorDoor", "Hallway", "Stair", "Elevator" })
        {
            string path = "Assets/Resources/SO/Building/" + name + ".asset";
            string source = File.ReadAllText(path);
            BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            Require(asset != null, "Missing legacy definition: " + path);
            var expected = ReadDictionary(source, "tiles");
            Require(expected.Count == 1 && asset.tiles != null && asset.tiles.Count == expected.Count,
                "Legacy authored tile dictionary was lost: " + path);
            foreach (var pair in asset.tiles)
            {
                Require(expected.TryGetValue((int)pair.Key, out string reference) && pair.Value != null,
                    "Legacy authored tile key/reference was lost: " + path);
                Require(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pair.Value, out string guid, out long id)
                        && Reference(reference, "guid") == guid
                        && long.Parse(Reference(reference, "fileID"), CultureInfo.InvariantCulture) == id,
                    "Legacy tile reference changed: " + path);
            }
            byte[] bytes = SerializationUtility.SerializeValue(asset.tiles, DataFormat.Binary, out List<Object> references);
            var restored = SerializationUtility.DeserializeValue<Dictionary<BuildingTilemapLayer, Tile>>(
                bytes, DataFormat.Binary, references);
            Require(restored.Count == asset.tiles.Count && asset.tiles.All(p => restored.TryGetValue(p.Key, out Tile tile) && tile == p.Value),
                "Current tile dictionary did not round trip: " + path);
            VerifyDrawAndDelete(asset);
            Require(File.ReadAllText(path) == source, "Verification rewrote an authored building asset.");
            rows.Add($"asset\t{path}\t{expected.Count}\tPASS");
        }
        foreach (string name in new[] { "GameplayScene", "CharacterAiTestScene", "SampleScene" })
            VerifyScene("Assets/Scenes/" + name + ".unity", rows);

        if (!GridVisualDebugScenarios.RunAll(true))
            throw new InvalidOperationException("Building visual alignment regression failed.");
        GridBuildingFactoryDetachedDebugScenarios.Verify();
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output), "Missing isolated building visual evidence.");
        Directory.CreateDirectory(output);
        using (var file = new StreamWriter(new FileStream(Path.Combine(output, "building-definition-visual.tsv"),
                   FileMode.CreateNew, FileAccess.Write, FileShare.Read)))
            foreach (string row in rows) file.WriteLine(row);
        Debug.Log("ARCHITECTURE_BUILDING_VISUAL=PASS; legacy assets=5; scenes=3; scene dictionaries=6; archetypes=8; exact YAML keys/references; legacy generic bindings; current dictionary roundtrip; tile draw/delete; alignment; detached factory");
    }

    private static void VerifyLegacyTypeBindings()
    {
        Require(typeof(GridTexture).GetNestedType("TilemapLayer") == null,
            "Renderer still owns the serialized enum.");
        Require(Type.GetType(LegacyEnum, false) == null,
            "Legacy CLR enum still resolves without the explicit serialization binding.");
        Require(typeof(BuildingTilemapLayer).Assembly.GetName().Name == "DungeonStory.Buildings",
            "Tilemap definition is not in its model assembly.");
        Require(Enum.GetValues(typeof(BuildingTilemapLayer)).Cast<BuildingTilemapLayer>().Select(v => (int)v)
                .SequenceEqual(new[] { 0, 1, 2, 3 }), "Tilemap enum values changed.");
        Require(Enum.GetNames(typeof(BuildingTilemapLayer)).SequenceEqual(new[] { "HALLWAY", "FRONT", "MIDDLE", "BACK" }),
            "Tilemap enum names changed.");
        var binder = TwoWaySerializationBinder.Default;
        Require(binder.BindToType(LegacyEnum) == typeof(BuildingTilemapLayer), "Legacy enum binding failed.");
        foreach (Type value in new[] { typeof(Tile), typeof(Tilemap) })
        {
            string legacy = "System.Collections.Generic.Dictionary`2[[" + LegacyEnum
                + "],[" + value.FullName + ", UnityEngine.TilemapModule]], mscorlib";
            Require(binder.BindToType(legacy) == typeof(Dictionary<,>).MakeGenericType(typeof(BuildingTilemapLayer), value),
                "Legacy generic dictionary binding failed: " + legacy);
        }
        Require(binder.BindToType("System.Collections.Generic.EnumEqualityComparer`1[[" + LegacyEnum + "]], mscorlib")
                == EqualityComparer<BuildingTilemapLayer>.Default.GetType(), "Legacy enum comparer binding failed.");
    }

    private static void VerifyArchetypeComponents()
    {
        Type[] types = { typeof(BuildableObject), typeof(Facility), typeof(Shop), typeof(Door),
            typeof(InteriorDoor), typeof(Hallway), typeof(Stair), typeof(DefenseFacility) };
        Require(typeof(BuildingRuntimeArchetypeKind).Assembly.GetName().Name == "Assembly-CSharp",
            "Authored archetype enum assembly changed.");
        for (int i = 0; i < types.Length; i++)
        {
            var kind = (BuildingRuntimeArchetypeKind)i;
            Require(kind.IsDefined() && kind.ToComponentType() == types[i]
                    && BuildingRuntimeArchetypeKindExtensions.FromComponentType(types[i]) == kind,
                "Archetype type mapping changed: " + kind);
            var go = new GameObject("Archetype component boundary");
            go.SetActive(false);
            try { Require(kind.AddComponent(go).GetType() == types[i], "Archetype component creation changed."); }
            finally { Object.DestroyImmediate(go); }
        }
        Require(!((BuildingRuntimeArchetypeKind)(-1)).IsDefined() && !((BuildingRuntimeArchetypeKind)8).IsDefined(),
            "Invalid archetype became defined.");
        Expect<ArgumentOutOfRangeException>(() => ((BuildingRuntimeArchetypeKind)8).ToComponentType());
        Expect<ArgumentOutOfRangeException>(() => BuildingRuntimeArchetypeKindExtensions.FromComponentType(typeof(GameObject)));
        Expect<ArgumentNullException>(() => BuildingRuntimeArchetypeKind.Generic.AddComponent(null));
        var rejected = new GameObject("Rejected archetype component");
        rejected.SetActive(false);
        try
        {
            Expect<ArgumentOutOfRangeException>(() => ((BuildingRuntimeArchetypeKind)8).AddComponent(rejected));
            Require(rejected.GetComponents<BuildableObject>().Length == 0, "Rejected archetype added a component.");
        }
        finally { Object.DestroyImmediate(rejected); }
    }

    private static void VerifyDrawAndDelete(BuildingSO definition)
    {
        var root = new GameObject("Legacy tile renderer", typeof(UnityEngine.Grid));
        try
        {
            var texture = root.AddComponent<GridTexture>();
            var maps = new Dictionary<BuildingTilemapLayer, Tilemap>();
            foreach (BuildingTilemapLayer key in Enum.GetValues(typeof(BuildingTilemapLayer)))
            {
                var child = new GameObject(key.ToString(), typeof(Tilemap), typeof(TilemapRenderer));
                child.transform.SetParent(root.transform, false);
                maps.Add(key, child.GetComponent<Tilemap>());
            }
            texture.buildingTilemapEven = maps;
            texture.buildingTilemapOdd = maps;
            var position = new Vector2Int(2, 1);
            foreach (bool even in new[] { false, true })
            {
                texture.DrawBuilding(definition.tiles, position, even);
                foreach (var pair in definition.tiles)
                    Require(maps[pair.Key].GetTile(new Vector3Int(-2, 3, 0)) == pair.Value,
                        "Legacy dictionary did not reach its renderer layer.");
                texture.DeleteBuilding(definition.tiles, position, even);
                Require(maps.Values.All(map => map.GetUsedTilesCount() == 0), "Tile deletion left a stale layer.");
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void VerifyScene(string path, ICollection<string> rows)
    {
        string source = File.ReadAllText(path);
        string component = Regex.Split(source, @"(?m)^--- !u!").Single(block => block.Contains("guid: " + TextureScriptGuid));
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var texture = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GridTexture>(true)).Single();
            foreach (var field in new[] { (Name: "buildingTilemapEven", Value: texture.buildingTilemapEven),
                         (Name: "buildingTilemapOdd", Value: texture.buildingTilemapOdd) })
            {
                var expected = ReadDictionary(component, field.Name);
                Require(expected.Count == 3 && field.Value != null && field.Value.Count == expected.Count,
                    "Scene renderer dictionary was lost: " + path + ":" + field.Name);
                foreach (var pair in field.Value)
                {
                    Require(expected.TryGetValue((int)pair.Key, out string reference) && pair.Value != null,
                        "Scene renderer key/reference was lost.");
                    ulong id = GlobalObjectId.GetGlobalObjectIdSlow(pair.Value).targetObjectId;
                    Require(id == ulong.Parse(Reference(reference, "fileID"), CultureInfo.InvariantCulture),
                        "Scene renderer layer points to a different Tilemap.");
                }
                rows.Add($"scene:{field.Name}\t{path}\t{expected.Count}\tPASS");
            }
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        Require(File.ReadAllText(path) == source, "Verification rewrote an authored scene.");
    }

    // Compare loaded dictionaries with the original YAML nodes and Unity references.
    // Expected keys/references are not inferred from the new runtime object.
    private static Dictionary<int, string> ReadDictionary(string source, string field)
    {
        string referencesBlock = source.Split(new[] { "    ReferencedUnityObjects:" }, StringSplitOptions.None)[1]
            .Split(new[] { "    SerializedBytesString:" }, StringSplitOptions.None)[0];
        string[] references = Regex.Matches(referencesBlock, @"(?m)^    - (\{[^\r\n]+\})").Cast<Match>()
            .Select(m => m.Groups[1].Value).ToArray();
        string dictionary = source.Split(new[] { "    - Name: " + field + "\n", "    - Name: " + field + "\r\n" }, StringSplitOptions.None)[1];
        Match nextField = Regex.Match(dictionary, @"(?m)^    - Name: (?!comparer\r?$)[A-Za-z_]");
        if (nextField.Success) dictionary = dictionary.Substring(0, nextField.Index);
        var result = new Dictionary<int, string>();
        foreach (Match pair in Regex.Matches(dictionary,
                     @"Name: \$k\s+Entry: 3\s+Data: (\d+)\s+- Name: \$v\s+Entry: 10\s+Data: (\d+)"))
        {
            int key = int.Parse(pair.Groups[1].Value, CultureInfo.InvariantCulture);
            int index = int.Parse(pair.Groups[2].Value, CultureInfo.InvariantCulture);
            Require(key >= 0 && key <= 3 && index < references.Length, "Unsupported authored dictionary node.");
            result.Add(key, references[index]);
        }
        Require(result.Count > 0, "Authored dictionary nodes were not read: " + field);
        return result;
    }

    private static string Reference(string value, string field)
    {
        Match match = Regex.Match(value, @"\b" + field + @": ([^,}]+)");
        Require(match.Success, "Missing authored Unity reference field: " + field);
        return match.Groups[1].Value.Trim();
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException("Required " + typeof(T).Name + " was not thrown.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
