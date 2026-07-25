#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WildlifeHabitatDecorationAssetBuilder
{
    private const string PalettePath =
        "Assets/Resources/SO/Wildlife/WildlifeHabitatDecorationPalette.asset";
    private const string ArtRoot =
        "Assets/Images/TINY FOREST 2.0 - ADDED WATERFALL/TINY FOREST 2.0 - ADDED WATERFALL/";

    private static readonly string[] FlowerPaths =
    {
        ArtRoot + "FLOWER 1.png",
        ArtRoot + "FLOWER 2.png",
        ArtRoot + "FLOWER 3.png",
        ArtRoot + "FLOWER 4.png",
        ArtRoot + "FLOWER 5.png",
        ArtRoot + "FLOWER 6.png"
    };

    private static readonly string[] TreePaths =
    {
        ArtRoot + "TREE 1 - DAY SUMMER.png",
        ArtRoot + "TREE 2 - DAY SUMMER.png",
        ArtRoot + "TREE 3 - DAY SUMMER.png"
    };

    private static readonly string[] RockPaths =
    {
        ArtRoot + "ROCK 1.png",
        ArtRoot + "ROCK 2.png",
        ArtRoot + "ROCK 3.png"
    };

    [MenuItem("DungeonStory/Content/Build Wildlife Habitat Decorations")]
    public static void BuildFromMenu()
    {
        if (!EnsurePalette(log: true))
        {
            throw new InvalidOperationException("Wildlife habitat decoration palette could not be built.");
        }
    }

    public static bool EnsurePalette(bool log = false)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        List<Sprite> flowers = FlowerPaths
            .SelectMany(LoadSprites)
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToList();
        List<Sprite> trees = TreePaths.Select(LoadSingleSprite).Where(sprite => sprite != null).ToList();
        List<Sprite> rocks = RockPaths.Select(LoadSingleSprite).Where(sprite => sprite != null).ToList();
        if (flowers.Count == 0 || trees.Count == 0 || rocks.Count == 0)
        {
            Debug.LogError(
                $"Wildlife decoration art is incomplete. flowers={flowers.Count}; trees={trees.Count}; rocks={rocks.Count}");
            return false;
        }

        WildlifeHabitatDecorationPaletteSO palette =
            AssetDatabase.LoadAssetAtPath<WildlifeHabitatDecorationPaletteSO>(PalettePath);
        if (palette == null)
        {
            palette = ScriptableObject.CreateInstance<WildlifeHabitatDecorationPaletteSO>();
            AssetDatabase.CreateAsset(palette, PalettePath);
        }

        palette.Configure(flowers, trees, rocks);
        EditorUtility.SetDirty(palette);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(PalettePath, ImportAssetOptions.ForceSynchronousImport);

        bool valid = palette.IsComplete
            && palette.FlowerSprites.Count >= 6
            && palette.TreeSprites.Count >= 3
            && palette.RockSprites.Count >= 3;
        if (log)
        {
            Debug.Log(valid
                ? $"Wildlife habitat decorations ready. flowers={palette.FlowerSprites.Count}; trees={palette.TreeSprites.Count}; rocks={palette.RockSprites.Count}"
                : "Wildlife habitat decoration palette is incomplete.");
        }

        return valid;
    }

    private static IEnumerable<Sprite> LoadSprites(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>();
    }

    private static Sprite LoadSingleSprite(string path)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
