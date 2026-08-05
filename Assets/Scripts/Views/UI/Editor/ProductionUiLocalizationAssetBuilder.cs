#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public static class ProductionUiLocalizationAssetBuilder
{
    private const string LocalizationRoot = "Assets/Localization";
    private const string KoreanTablePath =
        LocalizationRoot + "/ProductionUI_ko.asset";
    private const string EnglishTablePath =
        LocalizationRoot + "/ProductionUI_en.asset";

    private static readonly IReadOnlyDictionary<string, string> Korean =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Production.Route.Header"] =
                "분기 소비처 · 우선순위 / 가중치 / 최소 비축",
            ["Production.Route.PriorityIncrease"] = "우선 {0} +",
            ["Production.Route.WeightIncrease"] = "가중 {0} +",
            ["Production.Route.MinimumReserveIncrease"] = "최소 {0} +",
            ["Production.Route.Status.DemandReserved"] =
                "{0} | 수요 {1} / 예약 {2}",
            ["Production.Route.Status.Blocked"] = "막힘: {0}",
            ["Production.Route.Status.InactiveConsumer"] =
                "막힘: 비활성 소비처"
        };

    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Production.Route.Header"] =
                "Branch consumers · priority / weight / minimum reserve",
            ["Production.Route.PriorityIncrease"] = "Priority {0} +",
            ["Production.Route.WeightIncrease"] = "Weight {0} +",
            ["Production.Route.MinimumReserveIncrease"] = "Minimum {0} +",
            ["Production.Route.Status.DemandReserved"] =
                "{0} | demand {1} / reserved {2}",
            ["Production.Route.Status.Blocked"] = "blocked: {0}",
            ["Production.Route.Status.InactiveConsumer"] =
                "blocked: inactive consumer"
        };

    [MenuItem("Tools/DungeonStory/Content/Update Production UI Localization")]
    public static void Synchronize()
    {
        Locale koreanLocale = RequireLocale("ko", "Korean");
        Locale englishLocale = RequireLocale("en", "English");
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                ProductionUiTextLocalizer.TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(
                ProductionUiTextLocalizer.TableName,
                LocalizationRoot,
                new List<Locale> { koreanLocale, englishLocale });
        if (collection == null)
        {
            throw new InvalidOperationException(
                $"Could not create String Table collection "
                + $"'{ProductionUiTextLocalizer.TableName}'.");
        }

        StringTable korean = RequireTable(
            collection,
            koreanLocale,
            KoreanTablePath);
        StringTable english = RequireTable(
            collection,
            englishLocale,
            EnglishTablePath);

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
                ProductionUiTextLocalizer.TableName);
        }
        Debug.Log(
            "ProductionUI localization synchronized: "
            + $"{Korean.Count} keys, ko/en placeholder parity complete.");
    }

    public static void Validate()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                ProductionUiTextLocalizer.TableName)
            ?? throw new InvalidOperationException(
                $"String Table collection "
                + $"'{ProductionUiTextLocalizer.TableName}' is missing.");
        Locale koreanLocale = RequireLocale("ko", "Korean");
        Locale englishLocale = RequireLocale("en", "English");
        StringTable korean = collection.GetTable(koreanLocale.Identifier)
            as StringTable
            ?? throw new InvalidOperationException(
                "ProductionUI Korean String Table is missing.");
        StringTable english = collection.GetTable(englishLocale.Identifier)
            as StringTable
            ?? throw new InvalidOperationException(
                "ProductionUI English String Table is missing.");
        Validate(collection, korean, english);
    }

    private static Locale RequireLocale(string code, string displayName) =>
        LocalizationEditorSettings.GetLocale(code)
        ?? throw new InvalidOperationException(
            $"{displayName} locale '{code}' is missing.");

    private static StringTable RequireTable(
        StringTableCollection collection,
        Locale locale,
        string assetPath) =>
        collection.GetTable(locale.Identifier) as StringTable
        ?? collection.AddNewTable(locale.Identifier, assetPath) as StringTable
        ?? throw new InvalidOperationException(
            $"Could not create ProductionUI String Table for "
            + $"'{locale.Identifier.Code}'.");

    private static void RemoveObsoleteEntries(StringTableCollection collection)
    {
        HashSet<string> required = new HashSet<string>(
            Korean.Keys,
            StringComparer.Ordinal);
        string[] obsolete = collection.SharedData.Entries
            .Select(entry => entry.Key)
            .Where(key => !required.Contains(key))
            .ToArray();
        foreach (string key in obsolete)
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
            return;
        }

        entry.Value = value;
    }

    private static void Validate(
        StringTableCollection collection,
        StringTable korean,
        StringTable english)
    {
        string[] expectedKeys = Korean.Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] actualKeys = collection.SharedData.Entries
            .Select(entry => entry.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (!expectedKeys.SequenceEqual(actualKeys, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "ProductionUI must contain exactly the seven authored keys.");
        }

        foreach (string key in expectedKeys)
        {
            string koreanValue = Require(korean, key);
            string englishValue = Require(english, key);
            int[] koreanPlaceholders = GetPlaceholderIndexes(koreanValue);
            int[] englishPlaceholders = GetPlaceholderIndexes(englishValue);
            if (!koreanPlaceholders.SequenceEqual(englishPlaceholders))
            {
                throw new InvalidOperationException(
                    $"ProductionUI placeholder mismatch for '{key}'.");
            }

            ValidateCompositeFormat(key, "ko", koreanValue, koreanPlaceholders);
            ValidateCompositeFormat(key, "en", englishValue, englishPlaceholders);
        }
    }

    private static string Require(StringTable table, string key)
    {
        StringTableEntry entry = table.GetEntry(key);
        if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
        {
            throw new InvalidOperationException(
                $"String Table '{table.LocaleIdentifier}' is missing '{key}'.");
        }
        return entry.Value;
    }

    private static int[] GetPlaceholderIndexes(string value) =>
        Regex.Matches(value, @"\{(?<index>\d+)(?:[^{}]*)\}")
            .Cast<Match>()
            .Select(match => int.Parse(
                match.Groups["index"].Value,
                CultureInfo.InvariantCulture))
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

    private static void ValidateCompositeFormat(
        string key,
        string locale,
        string template,
        IReadOnlyList<int> placeholders)
    {
        int argumentCount = placeholders.Count == 0
            ? 0
            : placeholders[placeholders.Count - 1] + 1;
        object[] arguments = Enumerable.Repeat<object>(0, argumentCount).ToArray();
        try
        {
            string.Format(CultureInfo.InvariantCulture, template, arguments);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"ProductionUI '{key}' has an invalid {locale} format.",
                exception);
        }
    }
}
#endif
