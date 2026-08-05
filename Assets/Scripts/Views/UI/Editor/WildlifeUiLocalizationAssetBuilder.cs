#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public static class WildlifeUiLocalizationAssetBuilder
{
    private const string Root = "Assets/Localization";
    private const string Key = "Wildlife.FoodRaid.RaidActorRemoved";

    [MenuItem("Tools/DungeonStory/Content/Update Wildlife UI Localization")]
    public static void Synchronize()
    {
        Locale koreanLocale = RequireLocale("ko", "Korean");
        Locale englishLocale = RequireLocale("en", "English");
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                WildlifeFoodRaidOutcomeTextLocalizer.TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(
                WildlifeFoodRaidOutcomeTextLocalizer.TableName,
                Root,
                new List<Locale> { koreanLocale, englishLocale });
        if (collection == null)
        {
            throw new InvalidOperationException(
                "Could not create WildlifeUI String Table collection.");
        }

        StringTable korean = RequireTable(collection, koreanLocale, "ko");
        StringTable english = RequireTable(collection, englishLocale, "en");
        foreach (string obsolete in collection.SharedData.Entries
                     .Select(entry => entry.Key)
                     .Where(value => !string.Equals(
                         value,
                         Key,
                         StringComparison.Ordinal))
                     .ToArray())
        {
            collection.RemoveEntry(obsolete);
        }
        Set(korean, Key, "습격 개체가 제거되어 도난이 취소되었습니다.");
        Set(english, Key, "The raid actor was removed, cancelling the theft.");
        Validate(collection, korean, english);

        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(korean);
        EditorUtility.SetDirty(english);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (LocalizationSettings.HasSettings)
        {
            LocalizationSettings.StringDatabase.ReleaseTable(
                WildlifeFoodRaidOutcomeTextLocalizer.TableName);
        }
        Debug.Log("WildlifeUI localization synchronized: 1 key, ko/en parity complete.");
    }

    public static void Validate()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                WildlifeFoodRaidOutcomeTextLocalizer.TableName)
            ?? throw new InvalidOperationException(
                "WildlifeUI String Table collection is missing.");
        Validate(
            collection,
            collection.GetTable("ko") as StringTable
                ?? throw new InvalidOperationException(
                    "WildlifeUI Korean String Table is missing."),
            collection.GetTable("en") as StringTable
                ?? throw new InvalidOperationException(
                    "WildlifeUI English String Table is missing."));
    }

    private static Locale RequireLocale(string code, string name) =>
        LocalizationEditorSettings.GetLocale(code)
        ?? throw new InvalidOperationException(
            $"{name} locale '{code}' is missing.");

    private static StringTable RequireTable(
        StringTableCollection collection,
        Locale locale,
        string suffix) =>
        collection.GetTable(locale.Identifier) as StringTable
        ?? collection.AddNewTable(
            locale.Identifier,
            $"{Root}/WildlifeUI_{suffix}.asset") as StringTable
        ?? throw new InvalidOperationException(
            $"Could not create WildlifeUI '{suffix}' String Table.");

    private static void Set(StringTable table, string key, string value)
    {
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null)
        {
            table.AddEntry(key, value);
        }
        else
        {
            entry.Value = value;
        }
    }

    private static void Validate(
        StringTableCollection collection,
        StringTable korean,
        StringTable english)
    {
        string[] keys = collection.SharedData.Entries
            .Select(entry => entry.Key)
            .ToArray();
        if (keys.Length != 1
            || !string.Equals(keys[0], Key, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "WildlifeUI must contain exactly the authored outcome key.");
        }
        Require(korean, Key);
        Require(english, Key);
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
