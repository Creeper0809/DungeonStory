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

public static class BuildingSummaryUiLocalizationAssetBuilder
{
    private const string Root = "Assets/Localization";
    private static readonly IReadOnlyDictionary<string, string> Korean =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["BuildingSummary.Status"] = "상태  {0}  ·  시설 Lv.{1}",
            ["BuildingSummary.State.Damaged"] = "손상",
            ["BuildingSummary.State.Normal"] = "정상",
            ["BuildingSummary.LocationCategory"] = "위치  ({0}, {1})  ·  {2}",
            ["BuildingSummary.Facility.Usage"] = "이용 {0}/{1}  ·  예약 {2}",
            ["BuildingSummary.Facility.NoVisitUsage"] = "방문 이용 없음",
            ["BuildingSummary.Facility.Roles"] = "용도  {0}",
            ["BuildingSummary.Facility.Work"] = "업무  {0}  ·  필요 직원 {1}",
            ["BuildingSummary.Common.None"] = "없음",
            ["BuildingSummary.Common.Unknown"] = "알 수 없음",

            ["BuildingSummary.Filth.Removed"] = "제거됨",
            ["BuildingSummary.Filth.Type"] = "종류  {0}",
            ["BuildingSummary.Filth.Location"] = "위치  ({0}, {1})  ·  {2}",
            ["BuildingSummary.Filth.SurfaceFloorAndWall"] = "바닥과 벽",
            ["BuildingSummary.Filth.SurfaceFloor"] = "바닥",
            ["BuildingSummary.Filth.Amount"] = "오염량  {0:0.#}  ·  감염도 {1:0.#}%",
            ["BuildingSummary.Filth.Cleanliness"] = "청결 영향  -{0:0.#}  ·  청소 작업량 {1:0.#}",
            ["BuildingSummary.Filth.CleaningPriority"] = "청소 명령  최우선 지정됨",
            ["BuildingSummary.Filth.CleaningAutomatic"] = "청소 명령  자동 우선순위",
            ["BuildingSummary.Filth.Source"] = "원인 인물  {0}",
            ["BuildingSummary.Filth.Type.Waste"] = "배설 오염",
            ["BuildingSummary.Filth.Type.Blood"] = "핏자국",
            ["BuildingSummary.Filth.Type.Rot"] = "부패 오염",
            ["BuildingSummary.Filth.Type.Stain"] = "벽 얼룩",
            ["BuildingSummary.Filth.Type.Unknown"] = "오염",

            ["BuildingSummary.Crafting.RuntimeUnavailable"] = "제작  장비 런타임 없음",
            ["BuildingSummary.Crafting.MaterialsMoving"] = " / 재료 이동 중",
            ["BuildingSummary.Crafting.Order"] = "{0} 작업량 {1:0.#}{2}",
            ["BuildingSummary.Crafting.AvailableNoQueue"] = "제작 가능  {0}  ·  대기 없음",
            ["BuildingSummary.Crafting.Queue"] = "제작 대기  {0}",

            ["BuildingSummary.Stock.RestockNeeded"] = "재고  {0}  ·  보충 필요",
            ["BuildingSummary.Stock.Amount"] = "재고  {0}",
            ["BuildingSummary.Stock.Capacity"] = "재고  {0}/{1}",
            ["BuildingSummary.Warehouse.Amount"] = "창고  {0}",
            ["BuildingSummary.Warehouse.Capacity"] = "창고  {0}/{1}",

            ["BuildingSummary.Construction.Target"] = "대상  {0}",
            ["BuildingSummary.Construction.Location"] = "위치  ({0}, {1})  ·  공사 현장",
            ["BuildingSummary.Construction.Safety"] = "안전  {0}",
            ["BuildingSummary.Construction.NoOrder"] = "상태  공사 주문 없음",
            ["BuildingSummary.Construction.Status"] = "상태  {0}",
            ["BuildingSummary.Construction.Progress"] = "작업  {0:0.#}/{1:0.#}  ·  {2}%",
            ["BuildingSummary.Construction.Material"] = "재료  {0} {1}/{2}",
            ["BuildingSummary.Construction.UnnamedFacility"] = "시설 {0}",
            ["BuildingSummary.Construction.InstallationKit"] = "{0} 설치 키트",
            ["BuildingSummary.Construction.NoMaterials"] = "재료  필요 없음",
            ["BuildingSummary.Construction.ReservedWorker"] = "예약 직원  {0}",

            ["BuildingSummary.WorkOrder.WaitingForMaterials"] = "재료 대기",
            ["BuildingSummary.WorkOrder.Ready"] = "작업 가능",
            ["BuildingSummary.WorkOrder.InProgress"] = "공사 중",
            ["BuildingSummary.WorkOrder.Blocked"] = "막힘",
            ["BuildingSummary.WorkOrder.Completed"] = "완료",
            ["BuildingSummary.WorkOrder.Cancelled"] = "취소됨",
            ["BuildingSummary.WorkOrder.Unknown"] = "알 수 없음",

            ["BuildingSummary.Action.Close"] = "닫기",
            ["BuildingSummary.Action.PrioritizeCleaning"] = "청소 우선",
            ["BuildingSummary.Action.ClearCleaningPriority"] = "우선 해제",
            ["BuildingSummary.Action.Details"] = "상세"
        };

    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["BuildingSummary.Status"] = "Status  {0}  ·  Facility Lv.{1}",
            ["BuildingSummary.State.Damaged"] = "Damaged",
            ["BuildingSummary.State.Normal"] = "Normal",
            ["BuildingSummary.LocationCategory"] = "Location  ({0}, {1})  ·  {2}",
            ["BuildingSummary.Facility.Usage"] = "Usage {0}/{1}  ·  reservations {2}",
            ["BuildingSummary.Facility.NoVisitUsage"] = "No visitor usage",
            ["BuildingSummary.Facility.Roles"] = "Roles  {0}",
            ["BuildingSummary.Facility.Work"] = "Work  {0}  ·  required staff {1}",
            ["BuildingSummary.Common.None"] = "None",
            ["BuildingSummary.Common.Unknown"] = "Unknown",

            ["BuildingSummary.Filth.Removed"] = "Removed",
            ["BuildingSummary.Filth.Type"] = "Type  {0}",
            ["BuildingSummary.Filth.Location"] = "Location  ({0}, {1})  ·  {2}",
            ["BuildingSummary.Filth.SurfaceFloorAndWall"] = "Floor and wall",
            ["BuildingSummary.Filth.SurfaceFloor"] = "Floor",
            ["BuildingSummary.Filth.Amount"] = "Contamination  {0:0.#}  ·  infection {1:0.#}%",
            ["BuildingSummary.Filth.Cleanliness"] = "Cleanliness impact  -{0:0.#}  ·  cleaning work {1:0.#}",
            ["BuildingSummary.Filth.CleaningPriority"] = "Cleaning order  highest priority",
            ["BuildingSummary.Filth.CleaningAutomatic"] = "Cleaning order  automatic priority",
            ["BuildingSummary.Filth.Source"] = "Source character  {0}",
            ["BuildingSummary.Filth.Type.Waste"] = "Waste",
            ["BuildingSummary.Filth.Type.Blood"] = "Bloodstain",
            ["BuildingSummary.Filth.Type.Rot"] = "Rot",
            ["BuildingSummary.Filth.Type.Stain"] = "Wall stain",
            ["BuildingSummary.Filth.Type.Unknown"] = "Filth",

            ["BuildingSummary.Crafting.RuntimeUnavailable"] = "Crafting  equipment runtime unavailable",
            ["BuildingSummary.Crafting.MaterialsMoving"] = " / materials in transit",
            ["BuildingSummary.Crafting.Order"] = "{0} work {1:0.#}{2}",
            ["BuildingSummary.Crafting.AvailableNoQueue"] = "Craftable  {0}  ·  queue empty",
            ["BuildingSummary.Crafting.Queue"] = "Crafting queue  {0}",

            ["BuildingSummary.Stock.RestockNeeded"] = "Stock  {0}  ·  restock needed",
            ["BuildingSummary.Stock.Amount"] = "Stock  {0}",
            ["BuildingSummary.Stock.Capacity"] = "Stock  {0}/{1}",
            ["BuildingSummary.Warehouse.Amount"] = "Warehouse  {0}",
            ["BuildingSummary.Warehouse.Capacity"] = "Warehouse  {0}/{1}",

            ["BuildingSummary.Construction.Target"] = "Target  {0}",
            ["BuildingSummary.Construction.Location"] = "Location  ({0}, {1})  ·  construction site",
            ["BuildingSummary.Construction.Safety"] = "Safety  {0}",
            ["BuildingSummary.Construction.NoOrder"] = "Status  no construction order",
            ["BuildingSummary.Construction.Status"] = "Status  {0}",
            ["BuildingSummary.Construction.Progress"] = "Work  {0:0.#}/{1:0.#}  ·  {2}%",
            ["BuildingSummary.Construction.Material"] = "Material  {0} {1}/{2}",
            ["BuildingSummary.Construction.UnnamedFacility"] = "Facility {0}",
            ["BuildingSummary.Construction.InstallationKit"] = "{0} installation kit",
            ["BuildingSummary.Construction.NoMaterials"] = "Materials  none required",
            ["BuildingSummary.Construction.ReservedWorker"] = "Reserved worker  {0}",

            ["BuildingSummary.WorkOrder.WaitingForMaterials"] = "Waiting for materials",
            ["BuildingSummary.WorkOrder.Ready"] = "Ready",
            ["BuildingSummary.WorkOrder.InProgress"] = "Under construction",
            ["BuildingSummary.WorkOrder.Blocked"] = "Blocked",
            ["BuildingSummary.WorkOrder.Completed"] = "Completed",
            ["BuildingSummary.WorkOrder.Cancelled"] = "Cancelled",
            ["BuildingSummary.WorkOrder.Unknown"] = "Unknown",

            ["BuildingSummary.Action.Close"] = "Close",
            ["BuildingSummary.Action.PrioritizeCleaning"] = "Prioritize cleaning",
            ["BuildingSummary.Action.ClearCleaningPriority"] = "Clear priority",
            ["BuildingSummary.Action.Details"] = "Details"
        };

    [MenuItem("Tools/DungeonStory/Content/Update Building Summary UI Localization")]
    public static void Synchronize()
    {
        Locale koreanLocale = RequireLocale("ko", "Korean");
        Locale englishLocale = RequireLocale("en", "English");
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                BuildingSummaryUiTextQuery.TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(
                BuildingSummaryUiTextQuery.TableName,
                Root,
                new List<Locale> { koreanLocale, englishLocale });
        if (collection == null)
        {
            throw new InvalidOperationException(
                "Could not create BuildingSummaryUI String Table collection.");
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
                BuildingSummaryUiTextQuery.TableName);
        }
        Debug.Log(
            "BuildingSummaryUI localization synchronized: "
            + $"{Korean.Count} keys, ko/en placeholder parity complete.");
    }

    public static void Validate()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                BuildingSummaryUiTextQuery.TableName)
            ?? throw new InvalidOperationException(
                "BuildingSummaryUI String Table collection is missing.");
        Validate(
            collection,
            collection.GetTable("ko") as StringTable
                ?? throw new InvalidOperationException(
                    "BuildingSummaryUI Korean String Table is missing."),
            collection.GetTable("en") as StringTable
                ?? throw new InvalidOperationException(
                    "BuildingSummaryUI English String Table is missing."));
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
            $"{Root}/BuildingSummaryUI_{suffix}.asset") as StringTable
        ?? throw new InvalidOperationException(
            $"Could not create BuildingSummaryUI '{suffix}' String Table.");

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
        string[] englishKeys = English.Keys
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] actual = collection.SharedData.Entries
            .Select(entry => entry.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (!expected.SequenceEqual(englishKeys, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "BuildingSummaryUI authored ko/en keys do not match.");
        }
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"BuildingSummaryUI must contain exactly {Korean.Count} authored keys.");
        }

        foreach (string key in expected)
        {
            string koreanValue = Require(korean, key);
            string englishValue = Require(english, key);
            int[] koreanPlaceholders = GetPlaceholderIndexes(koreanValue);
            int[] englishPlaceholders = GetPlaceholderIndexes(englishValue);
            if (!koreanPlaceholders.SequenceEqual(englishPlaceholders))
            {
                throw new InvalidOperationException(
                    $"BuildingSummaryUI placeholder mismatch for '{key}'.");
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
                $"BuildingSummaryUI '{key}' has an invalid {locale} format.",
                exception);
        }
    }
}
#endif
