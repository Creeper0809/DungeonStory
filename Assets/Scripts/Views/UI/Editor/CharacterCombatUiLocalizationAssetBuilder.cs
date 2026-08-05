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

public static class CharacterCombatUiLocalizationAssetBuilder
{
    private const string Root = "Assets/Localization";
    private static readonly IReadOnlyDictionary<string, string> Korean =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CharacterCombat.FireMode.Aimed"] = "조준",
            ["CharacterCombat.FireMode.Rapid"] = "속사",
            ["CharacterCombat.FireMode.Suppressive"] = "제압",
            ["CharacterCombat.Command.RescueTargetRecovered"] =
                "구조 대상 회복"
        };
    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CharacterCombat.FireMode.Aimed"] = "Aimed fire",
            ["CharacterCombat.FireMode.Rapid"] = "Rapid fire",
            ["CharacterCombat.FireMode.Suppressive"] = "Suppressive fire",
            ["CharacterCombat.Command.RescueTargetRecovered"] =
                "Rescue target recovered"
        };

    [MenuItem("Tools/DungeonStory/Content/Update Character Combat UI Localization")]
    public static void Synchronize()
    {
        Locale koreanLocale = RequireLocale("ko", "Korean");
        Locale englishLocale = RequireLocale("en", "English");
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                CharacterCombatUiTextLocalizer.TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(
                CharacterCombatUiTextLocalizer.TableName,
                Root,
                new List<Locale> { koreanLocale, englishLocale });
        if (collection == null)
        {
            throw new InvalidOperationException(
                "Could not create CharacterCombatUI String Table collection.");
        }

        StringTable korean = RequireTable(collection, koreanLocale, "ko");
        StringTable english = RequireTable(collection, englishLocale, "en");
        RemoveObsoleteEntries(collection);
        foreach (string key in Korean.Keys.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            Set(korean, key, Korean[key]);
            Set(english, key, English[key]);
        }

        Validate(collection, korean, english);
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(korean);
        EditorUtility.SetDirty(english);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (LocalizationSettings.HasSettings)
        {
            LocalizationSettings.StringDatabase.ReleaseTable(
                CharacterCombatUiTextLocalizer.TableName);
        }
        Debug.Log(
            "CharacterCombatUI localization synchronized: "
            + "4 keys, ko/en parity complete.");
    }

    public static void Validate()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                CharacterCombatUiTextLocalizer.TableName)
            ?? throw new InvalidOperationException(
                "CharacterCombatUI String Table collection is missing.");
        Validate(
            collection,
            collection.GetTable("ko") as StringTable
                ?? throw new InvalidOperationException(
                    "CharacterCombatUI Korean String Table is missing."),
            collection.GetTable("en") as StringTable
                ?? throw new InvalidOperationException(
                    "CharacterCombatUI English String Table is missing."));
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
            $"{Root}/CharacterCombatUI_{suffix}.asset") as StringTable
        ?? throw new InvalidOperationException(
            $"Could not create CharacterCombatUI '{suffix}' String Table.");

    private static void RemoveObsoleteEntries(StringTableCollection collection)
    {
        HashSet<string> required = new HashSet<string>(
            Korean.Keys,
            StringComparer.Ordinal);
        foreach (string key in collection.SharedData.Entries
                     .Select(entry => entry.Key)
                     .Where(key => !required.Contains(key))
                     .ToArray())
        {
            collection.RemoveEntry(key);
        }
    }

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
        string[] expected = Korean.Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] actual = collection.SharedData.Entries
            .Select(entry => entry.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "CharacterCombatUI must contain exactly four authored keys.");
        }
        if (!expected.SequenceEqual(
                English.Keys.OrderBy(key => key, StringComparer.Ordinal),
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "CharacterCombatUI authored ko/en keys do not match.");
        }
        foreach (string key in expected)
        {
            Require(korean, key);
            Require(english, key);
        }
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
