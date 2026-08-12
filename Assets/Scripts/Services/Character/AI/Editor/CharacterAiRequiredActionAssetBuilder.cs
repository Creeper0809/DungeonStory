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
        Ensure<AIPrimitiveFieldMeal>("PrimitiveFieldMeal", "야전식 섭취");
        Ensure<AIPrimitiveFloorRest>("PrimitiveFloorRest", "바닥 취침");
        Ensure<AIPrimitiveLatrine>("PrimitiveLatrine", "임시 변소 사용");
        Ensure<AIPrimitiveBucketWash>("PrimitiveBucketWash", "물로 간이 세척");
        EnsureRecreation();
        EnsureNaturalnessSettings();
        GameContentCatalogAssetBuilder.ReindexCharacterAiActions();
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

    private static void EnsureRecreation()
    {
        const string considerationPath =
            "Assets/Resources/SO/AI/Consideration/NeedRecreation.asset";
        ConsiderationFacilityNeed need =
            AssetDatabase.LoadAssetAtPath<ConsiderationFacilityNeed>(
                considerationPath);
        if (need == null)
        {
            need = UnityEngine.ScriptableObject
                .CreateInstance<ConsiderationFacilityNeed>();
            AssetDatabase.CreateAsset(need, considerationPath);
        }
        need.name = "NeedRecreation";
        need.Role = FacilityRole.Entertainment;
        EditorUtility.SetDirty(need);

        string actionPath = $"{Folder}/Recreation.asset";
        AIFacilityRoleAction action =
            AssetDatabase.LoadAssetAtPath<AIFacilityRoleAction>(actionPath);
        if (action == null)
        {
            action = UnityEngine.ScriptableObject
                .CreateInstance<AIFacilityRoleAction>();
            AssetDatabase.CreateAsset(action, actionPath);
        }
        action.name = "Recreation";
        action.actionName = "여가";
        action.Role = FacilityRole.Entertainment;
        SerializedObject serialized = new SerializedObject(action);
        SerializedProperty considerations = serialized.FindProperty(
            "<considerations>k__BackingField");
        considerations.arraySize = 1;
        considerations.GetArrayElementAtIndex(0).objectReferenceValue = need;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(action);
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
