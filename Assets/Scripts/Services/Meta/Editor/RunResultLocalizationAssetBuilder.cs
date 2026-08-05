#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public static class RunResultLocalizationAssetBuilder
{
    public const string CollectionName = "DomainFailures";

    [MenuItem("Tools/DungeonStory/Content/Update Run Result Localization")]
    public static void Synchronize()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(CollectionName)
            ?? throw new InvalidOperationException(
                $"String Table collection '{CollectionName}' is missing.");
        StringTable korean = collection.GetTable("ko") as StringTable
            ?? throw new InvalidOperationException("Korean String Table is missing.");
        StringTable english = collection.GetTable("en") as StringTable
            ?? throw new InvalidOperationException("English String Table is missing.");

        Set(korean, "RunResultEmpty", "런 결과가 없습니다.");
        Set(korean, "RunResultNextRun", "다음 런");
        Set(english, "RunResultEmpty", "No run result is available.");
        Set(english, "RunResultNextRun", "Next run");

        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(korean);
        EditorUtility.SetDirty(english);
        AssetDatabase.SaveAssets();
        if (LocalizationSettings.HasSettings)
        {
            LocalizationSettings.StringDatabase.ReleaseTable(CollectionName);
        }
        Debug.Log("Run-result localization synchronized: 2 keys, ko/en parity complete.");
    }

    public static void Validate()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(CollectionName)
            ?? throw new InvalidOperationException(
                $"String Table collection '{CollectionName}' is missing.");
        foreach (string locale in new[] { "ko", "en" })
        {
            StringTable table = collection.GetTable(locale) as StringTable
                ?? throw new InvalidOperationException($"'{locale}' String Table is missing.");
            Require(table, "RunResultEmpty");
            Require(table, "RunResultNextRun");
        }
    }

    private static void Set(StringTable table, string key, string value)
    {
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null)
        {
            table.AddEntry(key, value);
            return;
        }

        entry.Value = value;
    }

    private static void Require(StringTable table, string key)
    {
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
        {
            throw new InvalidOperationException(
                $"String Table '{table.LocaleIdentifier}' is missing '{key}'.");
        }
    }
}
#endif
