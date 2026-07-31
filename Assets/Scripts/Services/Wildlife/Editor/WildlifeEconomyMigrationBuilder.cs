#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class WildlifeEconomyMigrationBuilder
{
    [MenuItem(
        "Tools/DungeonStory/Wildlife/Normalize Economy Resource IDs")]
    public static void Normalize()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:WildlifeSpeciesSO",
            new[] { "Assets/Resources/SO/Wildlife/Species" });
        int changes = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WildlifeSpeciesSO species =
                AssetDatabase.LoadAssetAtPath<WildlifeSpeciesSO>(path);
            if (species == null)
            {
                continue;
            }

            SerializedObject serialized = new SerializedObject(species);
            SerializedProperty yields =
                serialized.FindProperty("butcherYields");
            for (int index = 0; index < yields.arraySize; index++)
            {
                SerializedProperty itemId = yields
                    .GetArrayElementAtIndex(index)
                    .FindPropertyRelative("itemId");
                string normalized = NormalizeId(itemId.stringValue);
                if (!string.Equals(
                    itemId.stringValue,
                    normalized,
                    StringComparison.Ordinal))
                {
                    itemId.stringValue = normalized;
                    changes++;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(species);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"Wildlife economy resource IDs normalized: {changes} yields.");
    }

    private static string NormalizeId(string itemId)
    {
        return itemId switch
        {
            "stock-item:0" => "resource:meat",
            "wild:hide" => "resource:hide",
            "wild:fang" => "resource:fang",
            "wild:rune_dust" => "resource:rune-dust",
            _ => itemId
        };
    }
}
#endif
