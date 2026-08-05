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

public static class CharacterAiDiagnosticsLocalizationAssetBuilder
{
    private const string Root = "Assets/Localization";
    private static readonly IReadOnlyDictionary<string, string> Korean =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CharacterAI.Factor.Format.Score"] = "{0} {1:0.##}",
            ["CharacterAI.Factor.Format.ScoreWithReason"] = "{0} {1:0.##}({2})",
            ["CharacterAI.Breakdown.Rejected"] = "{0} 탈락: {1}",
            ["CharacterAI.Breakdown.Score"] = "{0} {1:0.#}%",
            ["CharacterAI.Breakdown.ScoreWithFactors"] = "{0} {1:0.#}% · {2}",
            ["CharacterAI.Breakdown.Multiline"] = "{0}\n{1}",
            ["CharacterAI.Intent.Inactive"] = "의도 유지 없음",
            ["CharacterAI.Intent.InactiveWithBreak"] = "의도 유지 없음 · 마지막 중단 {0}",
            ["CharacterAI.Intent.Active"] = "{0} · {1} · {2} · {3} · {4}",
            ["CharacterAI.Intent.Target"] = "대상 {0}",
            ["CharacterAI.Intent.NoTarget"] = "대상 없음",
            ["CharacterAI.Intent.Minimum"] = "최소 {0:0.0}s",
            ["CharacterAI.Intent.Interruptible"] = "중단 가능",
            ["CharacterAI.Intent.Expiry"] = "만료 {0:0.0}s",
            ["CharacterAI.Intent.NoExpiry"] = "만료 없음",
            ["CharacterAI.WorldSignals.Compact"] = "시간 {0} · 구역 {1} · 대기 {2:0}% · 경로 {3:0}% · 날씨 {4:0}% · 주변 {5}",

            ["CharacterAI.Branch.Critical"] = "중단 상태",
            ["CharacterAI.Branch.LockedAction"] = "진행 중 행동",
            ["CharacterAI.Branch.SoftLock"] = "의도 유지",
            ["CharacterAI.Branch.InterruptCheck"] = "행동 중단 검사",
            ["CharacterAI.Branch.MacroGoal"] = "장기 의도",
            ["CharacterAI.Branch.Emergency"] = "긴급 대응",
            ["CharacterAI.Branch.RoutineUtility"] = "일상 선택",
            ["CharacterAI.Branch.SurvivalNeeds"] = "생존",
            ["CharacterAI.Branch.DutyWork"] = "업무",
            ["CharacterAI.Branch.LeisureVisit"] = "여가",
            ["CharacterAI.Branch.ExitDungeon"] = "퇴장",
            ["CharacterAI.Branch.Eat"] = "식사",
            ["CharacterAI.Branch.Rest"] = "휴식",
            ["CharacterAI.Branch.Work"] = "작업",
            ["CharacterAI.Branch.Shopping"] = "소비",
            ["CharacterAI.Branch.LookAround"] = "둘러보기",
            ["CharacterAI.Branch.Wait"] = "대기",
            ["CharacterAI.Branch.Idle"] = "잠깐 멈춤",
            ["CharacterAI.Branch.Toilet"] = "화장실",
            ["CharacterAI.Branch.Hygiene"] = "위생",
            ["CharacterAI.Branch.StopCurrent"] = "이전 중단",
            ["CharacterAI.Branch.ContinueCurrent"] = "이전 유지",

            ["CharacterAI.Intention.Survive"] = "생존",
            ["CharacterAI.Intention.Recover"] = "회복",
            ["CharacterAI.Intention.Work"] = "업무",
            ["CharacterAI.Intention.Logistics"] = "물류",
            ["CharacterAI.Intention.Guard"] = "경비",
            ["CharacterAI.Intention.Hunt"] = "사냥",
            ["CharacterAI.Intention.Leisure"] = "여가",
            ["CharacterAI.Intention.Social"] = "사회",
            ["CharacterAI.Intention.Shop"] = "구매",
            ["CharacterAI.Intention.Exit"] = "퇴장",
            ["CharacterAI.Intention.Idle"] = "대기",
            ["CharacterAI.Intention.None"] = "없음",

            ["CharacterAI.Factor.Need"] = "욕구",
            ["CharacterAI.Factor.Priority"] = "우선순위",
            ["CharacterAI.Factor.Personality"] = "성격",
            ["CharacterAI.Factor.Memory"] = "기억",
            ["CharacterAI.Factor.Distance"] = "거리",
            ["CharacterAI.Factor.Risk"] = "위험",
            ["CharacterAI.Factor.Room"] = "방",
            ["CharacterAI.Factor.Stock"] = "재고",
            ["CharacterAI.Factor.Crowd"] = "혼잡",
            ["CharacterAI.Factor.Reservation"] = "예약",
            ["CharacterAI.Factor.Momentum"] = "흐름",
            ["CharacterAI.Factor.Queue"] = "대기열",
            ["CharacterAI.Factor.Social"] = "사회",
            ["CharacterAI.Factor.Weather"] = "날씨",
            ["CharacterAI.Factor.PathConfidence"] = "경로",
            ["CharacterAI.Factor.Fatigue"] = "피로",
            ["CharacterAI.Factor.Novelty"] = "새로움",
            ["CharacterAI.Factor.Schedule"] = "일정",

            ["CharacterAI.Reason.SurvivalStock"] = "생존 재고",
            ["CharacterAI.Reason.Health"] = "건강",
            ["CharacterAI.Reason.WeatherBurden"] = "날씨 부담",
            ["CharacterAI.Reason.WildlifeThreat"] = "동물 위협",
            ["CharacterAI.Reason.WorkPriority"] = "작업 우선순위",
            ["CharacterAI.Reason.WorkCapacity"] = "일할 여유",
            ["CharacterAI.Reason.Diligence"] = "성실함",
            ["CharacterAI.Reason.WorkHours"] = "근무 시간",
            ["CharacterAI.Reason.PathConfidence"] = "경로 신뢰",
            ["CharacterAI.Reason.RecentFailure"] = "최근 실패",
            ["CharacterAI.Reason.MoodAndFun"] = "기분/재미",
            ["CharacterAI.Reason.RiskCapacity"] = "위험 여유",
            ["CharacterAI.Reason.Enjoyment"] = "즐김 성향",
            ["CharacterAI.Reason.NearbyPeople"] = "주변 사람",
            ["CharacterAI.Reason.Queue"] = "대기열",
            ["CharacterAI.Reason.Weather"] = "날씨",
            ["CharacterAI.Reason.NoUrgentTask"] = "급한 일 없음",
            ["CharacterAI.Reason.NaturalMomentum"] = "자연스러운 유지",
            ["CharacterAI.Reason.LightInteraction"] = "가벼운 상호작용",
            ["CharacterAI.Reason.Queueing"] = "줄 서기",
            ["CharacterAI.Reason.WalkableWeather"] = "걸을 만한 날씨",
            ["CharacterAI.Reason.BaseScore"] = "기본 점수",
            ["CharacterAI.Reason.RecentFlow"] = "최근 흐름",

            ["CharacterAI.Time.Morning"] = "아침",
            ["CharacterAI.Time.Noon"] = "낮",
            ["CharacterAI.Time.Evening"] = "저녁",
            ["CharacterAI.Time.Night"] = "밤",
            ["CharacterAI.Time.Unknown"] = "모름",

            ["CharacterAI.Area.DungeonInterior"] = "던전",
            ["CharacterAI.Area.Entrance"] = "입구",
            ["CharacterAI.Area.DropZone"] = "하차장",
            ["CharacterAI.Area.ExteriorPath"] = "외부길",
            ["CharacterAI.Area.BlockedExterior"] = "막힌 외부",
            ["CharacterAI.Area.Unknown"] = "모름"
        };

    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CharacterAI.Factor.Format.Score"] = "{0} {1:0.##}",
            ["CharacterAI.Factor.Format.ScoreWithReason"] = "{0} {1:0.##} ({2})",
            ["CharacterAI.Breakdown.Rejected"] = "{0} rejected: {1}",
            ["CharacterAI.Breakdown.Score"] = "{0} {1:0.#}%",
            ["CharacterAI.Breakdown.ScoreWithFactors"] = "{0} {1:0.#}% · {2}",
            ["CharacterAI.Breakdown.Multiline"] = "{0}\n{1}",
            ["CharacterAI.Intent.Inactive"] = "No retained intent",
            ["CharacterAI.Intent.InactiveWithBreak"] = "No retained intent · last break {0}",
            ["CharacterAI.Intent.Active"] = "{0} · {1} · {2} · {3} · {4}",
            ["CharacterAI.Intent.Target"] = "Target {0}",
            ["CharacterAI.Intent.NoTarget"] = "No target",
            ["CharacterAI.Intent.Minimum"] = "minimum {0:0.0}s",
            ["CharacterAI.Intent.Interruptible"] = "Interruptible",
            ["CharacterAI.Intent.Expiry"] = "expires in {0:0.0}s",
            ["CharacterAI.Intent.NoExpiry"] = "No expiry",
            ["CharacterAI.WorldSignals.Compact"] = "Time {0} · area {1} · queue {2:0}% · path {3:0}% · weather {4:0}% · nearby {5}",

            ["CharacterAI.Branch.Critical"] = "Interrupted",
            ["CharacterAI.Branch.LockedAction"] = "Action in progress",
            ["CharacterAI.Branch.SoftLock"] = "Retain intent",
            ["CharacterAI.Branch.InterruptCheck"] = "Interrupt check",
            ["CharacterAI.Branch.MacroGoal"] = "Long-term intent",
            ["CharacterAI.Branch.Emergency"] = "Emergency response",
            ["CharacterAI.Branch.RoutineUtility"] = "Routine selection",
            ["CharacterAI.Branch.SurvivalNeeds"] = "Survival",
            ["CharacterAI.Branch.DutyWork"] = "Duty",
            ["CharacterAI.Branch.LeisureVisit"] = "Leisure",
            ["CharacterAI.Branch.ExitDungeon"] = "Exit",
            ["CharacterAI.Branch.Eat"] = "Eat",
            ["CharacterAI.Branch.Rest"] = "Rest",
            ["CharacterAI.Branch.Work"] = "Work",
            ["CharacterAI.Branch.Shopping"] = "Shopping",
            ["CharacterAI.Branch.LookAround"] = "Look around",
            ["CharacterAI.Branch.Wait"] = "Wait",
            ["CharacterAI.Branch.Idle"] = "Pause",
            ["CharacterAI.Branch.Toilet"] = "Toilet",
            ["CharacterAI.Branch.Hygiene"] = "Hygiene",
            ["CharacterAI.Branch.StopCurrent"] = "Stop previous",
            ["CharacterAI.Branch.ContinueCurrent"] = "Continue previous",

            ["CharacterAI.Intention.Survive"] = "Survival",
            ["CharacterAI.Intention.Recover"] = "Recovery",
            ["CharacterAI.Intention.Work"] = "Work",
            ["CharacterAI.Intention.Logistics"] = "Logistics",
            ["CharacterAI.Intention.Guard"] = "Guard",
            ["CharacterAI.Intention.Hunt"] = "Hunt",
            ["CharacterAI.Intention.Leisure"] = "Leisure",
            ["CharacterAI.Intention.Social"] = "Social",
            ["CharacterAI.Intention.Shop"] = "Shopping",
            ["CharacterAI.Intention.Exit"] = "Exit",
            ["CharacterAI.Intention.Idle"] = "Idle",
            ["CharacterAI.Intention.None"] = "None",

            ["CharacterAI.Factor.Need"] = "Need",
            ["CharacterAI.Factor.Priority"] = "Priority",
            ["CharacterAI.Factor.Personality"] = "Personality",
            ["CharacterAI.Factor.Memory"] = "Memory",
            ["CharacterAI.Factor.Distance"] = "Distance",
            ["CharacterAI.Factor.Risk"] = "Risk",
            ["CharacterAI.Factor.Room"] = "Room",
            ["CharacterAI.Factor.Stock"] = "Stock",
            ["CharacterAI.Factor.Crowd"] = "Crowding",
            ["CharacterAI.Factor.Reservation"] = "Reservation",
            ["CharacterAI.Factor.Momentum"] = "Momentum",
            ["CharacterAI.Factor.Queue"] = "Queue",
            ["CharacterAI.Factor.Social"] = "Social",
            ["CharacterAI.Factor.Weather"] = "Weather",
            ["CharacterAI.Factor.PathConfidence"] = "Path",
            ["CharacterAI.Factor.Fatigue"] = "Fatigue",
            ["CharacterAI.Factor.Novelty"] = "Novelty",
            ["CharacterAI.Factor.Schedule"] = "Schedule",

            ["CharacterAI.Reason.SurvivalStock"] = "survival stock",
            ["CharacterAI.Reason.Health"] = "health",
            ["CharacterAI.Reason.WeatherBurden"] = "weather burden",
            ["CharacterAI.Reason.WildlifeThreat"] = "wildlife threat",
            ["CharacterAI.Reason.WorkPriority"] = "work priority",
            ["CharacterAI.Reason.WorkCapacity"] = "capacity to work",
            ["CharacterAI.Reason.Diligence"] = "diligence",
            ["CharacterAI.Reason.WorkHours"] = "work hours",
            ["CharacterAI.Reason.PathConfidence"] = "path confidence",
            ["CharacterAI.Reason.RecentFailure"] = "recent failure",
            ["CharacterAI.Reason.MoodAndFun"] = "mood/fun",
            ["CharacterAI.Reason.RiskCapacity"] = "risk capacity",
            ["CharacterAI.Reason.Enjoyment"] = "enjoyment",
            ["CharacterAI.Reason.NearbyPeople"] = "nearby people",
            ["CharacterAI.Reason.Queue"] = "queue",
            ["CharacterAI.Reason.Weather"] = "weather",
            ["CharacterAI.Reason.NoUrgentTask"] = "no urgent task",
            ["CharacterAI.Reason.NaturalMomentum"] = "natural momentum",
            ["CharacterAI.Reason.LightInteraction"] = "light interaction",
            ["CharacterAI.Reason.Queueing"] = "queueing",
            ["CharacterAI.Reason.WalkableWeather"] = "walkable weather",
            ["CharacterAI.Reason.BaseScore"] = "base score",
            ["CharacterAI.Reason.RecentFlow"] = "recent flow",

            ["CharacterAI.Time.Morning"] = "Morning",
            ["CharacterAI.Time.Noon"] = "Noon",
            ["CharacterAI.Time.Evening"] = "Evening",
            ["CharacterAI.Time.Night"] = "Night",
            ["CharacterAI.Time.Unknown"] = "Unknown",

            ["CharacterAI.Area.DungeonInterior"] = "Dungeon",
            ["CharacterAI.Area.Entrance"] = "Entrance",
            ["CharacterAI.Area.DropZone"] = "Drop zone",
            ["CharacterAI.Area.ExteriorPath"] = "Exterior path",
            ["CharacterAI.Area.BlockedExterior"] = "Blocked exterior",
            ["CharacterAI.Area.Unknown"] = "Unknown"
        };

    [MenuItem("Tools/DungeonStory/Content/Update Character AI Diagnostics Localization")]
    public static void Synchronize()
    {
        Locale koreanLocale = RequireLocale("ko", "Korean");
        Locale englishLocale = RequireLocale("en", "English");
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                CharacterAiDiagnosticsTextQuery.TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(
                CharacterAiDiagnosticsTextQuery.TableName,
                Root,
                new List<Locale> { koreanLocale, englishLocale });
        if (collection == null)
        {
            throw new InvalidOperationException(
                "Could not create CharacterAI String Table collection.");
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
                CharacterAiDiagnosticsTextQuery.TableName);
        }
        Debug.Log(
            "CharacterAI localization synchronized: "
            + $"{Korean.Count} keys, ko/en placeholder parity complete.");
    }

    public static void Validate()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                CharacterAiDiagnosticsTextQuery.TableName)
            ?? throw new InvalidOperationException(
                "CharacterAI String Table collection is missing.");
        Validate(
            collection,
            collection.GetTable("ko") as StringTable
                ?? throw new InvalidOperationException(
                    "CharacterAI Korean String Table is missing."),
            collection.GetTable("en") as StringTable
                ?? throw new InvalidOperationException(
                    "CharacterAI English String Table is missing."));
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
            $"{Root}/CharacterAI_{suffix}.asset") as StringTable
        ?? throw new InvalidOperationException(
            $"Could not create CharacterAI '{suffix}' String Table.");

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
                "CharacterAI authored ko/en keys do not match.");
        }
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"CharacterAI must contain exactly {Korean.Count} authored keys.");
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
                    $"CharacterAI placeholder mismatch for '{key}'.");
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
                $"CharacterAI '{key}' has an invalid {locale} format.",
                exception);
        }
    }
}
#endif
