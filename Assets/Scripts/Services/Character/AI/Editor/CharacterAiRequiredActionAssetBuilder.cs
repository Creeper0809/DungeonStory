#if UNITY_EDITOR
using UnityEditor;

public static class CharacterAiRequiredActionAssetBuilder
{
    private const string Folder = "Assets/Resources/SO/AI/Action";

    [MenuItem("Tools/DungeonStory/Content/Build Required AI Actions")]
    public static void Build()
    {
        Ensure<AIHaul>("Haul", "운반");
        Ensure<AIRescue>("Rescue", "구조");
        Ensure<AIHunt>("Hunt", "사냥");
        Ensure<AISubstanceUse>("SubstanceUse", "약물 복용");
        Ensure<AIDrink>("Drink", "음수");
        EnsureNaturalnessSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void Ensure<T>(string fileName, string displayName)
        where T : AIActionSet
    {
        string path = $"{Folder}/{fileName}.asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = UnityEngine.ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.actionName = displayName;
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureNaturalnessSettings()
    {
        const string path = "Assets/Resources/SO/AI/CharacterAiNaturalnessSettings.asset";
        CharacterAiNaturalnessSettingsSO asset =
            AssetDatabase.LoadAssetAtPath<CharacterAiNaturalnessSettingsSO>(path);
        if (asset == null)
        {
            asset = UnityEngine.ScriptableObject
                .CreateInstance<CharacterAiNaturalnessSettingsSO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        EditorUtility.SetDirty(asset);
    }
}
#endif
