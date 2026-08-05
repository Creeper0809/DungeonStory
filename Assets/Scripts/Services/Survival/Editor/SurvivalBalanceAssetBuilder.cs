using System.IO;
using UnityEditor;
using UnityEngine;

public static class SurvivalBalanceAssetBuilder
{
    public const string AssetPath =
        "Assets/Resources/SO/Survival/SurvivalBalanceSettings.asset";

    [MenuItem("DungeonStory/Build/Survival/Create Or Reset Balance Settings")]
    public static void CreateOrReset()
    {
        string directory = Path.GetDirectoryName(AssetPath);
        if (!AssetDatabase.IsValidFolder(directory))
        {
            Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
        }

        SurvivalBalanceSettingsSO settings =
            AssetDatabase.LoadAssetAtPath<SurvivalBalanceSettingsSO>(
                AssetPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<
                SurvivalBalanceSettingsSO>();
            AssetDatabase.CreateAsset(settings, AssetPath);
        }

        settings.ResetToDefaults();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(
            AssetPath,
            ImportAssetOptions.ForceUpdate);
        Debug.Log(
            "생존 밸런스 데이터 에셋을 표준 초기값으로 생성했습니다: "
            + AssetPath);
    }
}
