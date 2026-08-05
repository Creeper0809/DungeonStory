#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WorldInteractionPresentationCatalogBuilder
{
    public const string AssetPath =
        "Assets/Resources/SO/Presentation/WorldInteractionPresentationCatalog.asset";

    [MenuItem("DungeonStory/Content/Build World Interaction Presentation Catalog")]
    public static void Build()
    {
        EnsureFolders();
        WorldInteractionPresentationCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<WorldInteractionPresentationCatalogSO>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<WorldInteractionPresentationCatalogSO>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
        }

        catalog.InitializeDefaults();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        Validate(catalog);
        Debug.Log(
            $"Built world interaction presentation catalog: "
            + $"{catalog.PropProfiles.Count} prop profiles.");
    }

    public static void Validate(WorldInteractionPresentationCatalogSO catalog = null)
    {
        catalog ??=
            AssetDatabase.LoadAssetAtPath<WorldInteractionPresentationCatalogSO>(AssetPath);
        if (catalog == null)
        {
            throw new InvalidOperationException(
                $"Missing presentation catalog at {AssetPath}.");
        }

        foreach (CharacterCarryVisualKind kind in Enum.GetValues(
                     typeof(CharacterCarryVisualKind)))
        {
            if (kind == CharacterCarryVisualKind.None)
            {
                continue;
            }

            CharacterPropAttachmentProfile profile =
                catalog.ResolvePropProfile("default", kind);
            if (profile == null
                || !string.Equals(
                    profile.speciesOrAnatomyId,
                    "default",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Missing default prop profile for {kind}.");
            }
        }

        bool duplicate = catalog.PropProfiles
            .Where(profile => profile != null)
            .GroupBy(
                profile => $"{profile.speciesOrAnatomyId.Trim().ToLowerInvariant()}"
                    + $":{profile.carryKind}")
            .Any(group => group.Count() > 1);
        if (duplicate)
        {
            throw new InvalidOperationException(
                "Presentation catalog contains duplicate species/kind profiles.");
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "SO");
        EnsureFolder("Assets/Resources/SO", "Presentation");
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
