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

public static class CharacterNarrativeLocalizationAssetBuilder
{
    private const string LocalizationRoot = "Assets/Localization";
    private const string KoreanTablePath =
        LocalizationRoot + "/CharacterNarrative_ko.asset";
    private const string EnglishTablePath =
        LocalizationRoot + "/CharacterNarrative_en.asset";

    private readonly struct Entry
    {
        public Entry(string key, string korean, string english)
        {
            Key = key;
            Korean = korean;
            English = english;
        }

        public string Key { get; }
        public string Korean { get; }
        public string English { get; }
    }

    private static readonly Entry[] Entries = BuildEntries();

    [MenuItem("Tools/DungeonStory/Content/Update Character Narrative Localization")]
    public static void Synchronize()
    {
        Locale koreanLocale = RequireLocale("ko", "Korean");
        Locale englishLocale = RequireLocale("en", "English");
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                CharacterNarrativeTextQuery.TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(
                CharacterNarrativeTextQuery.TableName,
                LocalizationRoot,
                new List<Locale> { koreanLocale, englishLocale });
        if (collection == null)
        {
            throw new InvalidOperationException(
                "Could not create CharacterNarrative String Table collection.");
        }

        StringTable korean = RequireTable(collection, koreanLocale, KoreanTablePath);
        StringTable english = RequireTable(collection, englishLocale, EnglishTablePath);
        RemoveObsoleteEntries(collection);
        foreach (Entry entry in Entries.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            Set(korean, entry.Key, entry.Korean);
            Set(english, entry.Key, entry.English);
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
                CharacterNarrativeTextQuery.TableName);
        }
        Debug.Log(
            $"CharacterNarrative synchronized: {Entries.Length} strict ko/en keys.");
    }

    public static void Validate()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                CharacterNarrativeTextQuery.TableName)
            ?? throw new InvalidOperationException(
                "CharacterNarrative collection is missing.");
        Validate(
            collection,
            collection.GetTable(RequireLocale("ko", "Korean").Identifier)
                as StringTable,
            collection.GetTable(RequireLocale("en", "English").Identifier)
                as StringTable);
    }

    private static Entry[] BuildEntries()
    {
        List<Entry> entries = new List<Entry>
        {
            E("Fallback.Work", "일", "work"),
            E("Fallback.Place", "그 자리", "the spot"),
            E("Fallback.Someone", "누군가", "Someone"),

            E("Template.GenericStarted",
                "{subject} {place}에서 {workObject} 시작했다.\n{subject} 숨을 고르고 {workObject} 붙잡았다.\n{subject} {place}로 향해 {workObject} 시작했다.\n{subject} 소매를 걷고 {workObject} 들어갔다.",
                "{subject} began {workObject} at {place}.\n{subject} took a breath and began {workObject}.\n{subject} headed to {place} for {workObject}.\n{subject} rolled up their sleeves and began {workObject}."),
            E("Template.GenericCompleted",
                "{subject} {place}에서 {workObject} 마쳤다.\n{subject} {workObject} 끝내고 숨을 돌렸다.\n{subject} {place}를 정리하며 {workObject} 마무리했다.\n{subject} 마지막까지 {workObject} 끝냈다.",
                "{subject} finished {workObject} at {place}.\n{subject} completed {workObject} and caught their breath.\n{subject} wrapped up {workObject} while tidying {place}.\n{subject} carried {workObject} through to the end."),
            E("Template.GenericProgress",
                "{subject} {place}에서 {workObject} 이어갔다.\n{subject} {workObject} 차근차근 진행했다.\n{subject} {place}를 살피며 {workObject} 계속했다.",
                "{subject} continued {workObject} at {place}.\n{subject} made steady progress on {workObject}.\n{subject} kept up {workObject} while watching {place}."),
            E("Template.GenericFailed",
                "{subject} {place}에서 {workObject} 시도했지만 멈췄다.\n{subject} {workObject} 나섰다가 막혀 돌아섰다.\n{subject} {place}에서 {workObject} 끝내지 못했다.",
                "{subject} tried {workObject} at {place} but had to stop.\n{subject} set out for {workObject} but was blocked.\n{subject} could not finish {workObject} at {place}."),
            E("Template.GenericBlocked",
                "{subject} {place}에서 길이 막혀 {workObject} 미뤘다.\n{subject} {workObject} 준비했지만 조건이 맞지 않았다.\n{subject} {place} 앞에서 멈춰 다음 기회를 기다렸다.",
                "{subject} postponed {workObject} after being blocked at {place}.\n{subject} prepared {workObject}, but the conditions were not met.\n{subject} stopped at {place} and waited for another chance at {workObject}."),
            E("Template.Facility",
                "{subject} {place}에 들어가 주변을 살폈다.\n{subject} {place}에서 잠시 숨을 돌렸다.\n{subject} 익숙한 듯 {place}를 이용했다.",
                "{subject} entered {place} and looked around.\n{subject} caught their breath at {place}.\n{subject} used {place} with familiar ease."),
            E("Template.Shopping",
                "{subject} {targetObject} 살피며 값을 가늠했다.\n{subject} {target} 앞에서 한참 고민했다.\n{subject} {targetObject} 챙기고 만족스레 돌아섰다.",
                "{subject} inspected {targetObject} and weighed the price.\n{subject} considered {target} for a while.\n{subject} took {targetObject} and turned away satisfied."),
            E("Template.Stock",
                "{subject} {targetObject} 가지런히 채웠다.\n{subject} 빈자리를 확인하고 {targetObject} 옮겼다.\n{subject} {target} 수량을 다시 세었다.",
                "{subject} stocked {targetObject} neatly.\n{subject} checked the empty space and moved {targetObject}.\n{subject} counted the {target} stock again."),
            E("Template.Health",
                "{subject} 몸 상태를 살피며 잠시 쉬었다.\n{subject} 아픈 곳을 확인하고 천천히 움직였다.\n{subject} 치료를 마치고 조심스레 일어났다.",
                "{subject} paused to check their condition.\n{subject} checked the injury and moved carefully.\n{subject} rose carefully after treatment."),
            E("Template.Duty",
                "{subject} 당직표를 확인하고 자리를 잡았다.\n{subject} 비번을 맞아 긴장을 풀었다.\n{subject} 다음 근무를 위해 장비를 챙겼다.",
                "{subject} checked the duty roster and took position.\n{subject} relaxed as off-duty time began.\n{subject} prepared for the next shift."),
            E("Template.Wait",
                "{subject} 그 자리에서 차분히 기다렸다.\n{subject} 주변을 살피며 순서를 기다렸다.\n{subject} 잠시 멈춰 다음 일을 기다렸다.",
                "{subject} waited calmly at the spot.\n{subject} watched the surroundings while waiting their turn.\n{subject} paused and waited for the next task."),
            E("Template.Social",
                "{subject} 짧게 인사를 나누고 웃었다.\n{subject} 곁의 동료와 잠시 이야기를 나눴다.\n{subject} 어색한 침묵 끝에 먼저 말을 건넸다.",
                "{subject} shared a brief greeting and smiled.\n{subject} talked with a nearby companion for a moment.\n{subject} spoke first after an awkward silence."),
            E("Template.Lifecycle",
                "{subject} 먼지를 털고 다음 자리로 향했다.\n{subject} 돌아온 숨을 고르며 안쪽으로 들어왔다.\n{subject} 떠날 준비를 마치고 발걸음을 옮겼다.",
                "{subject} brushed off the dust and headed onward.\n{subject} caught their breath and stepped inside.\n{subject} finished preparing to leave and moved on."),

            E("Prompt.Shape.OffDuty", "<subject> <reason>으로 비번을 시작했다.", "<subject> began off-duty time because of <reason>."),
            E("Prompt.Shape.Completed.0", "<subject> <place>에서 <work>를 마치고 <result>도 끝냈다.", "<subject> finished <work> at <place> and completed <result>."),
            E("Prompt.Shape.Completed.1", "<subject> <place>에서 <work>와 <result>를 마쳤다.", "<subject> completed <work> and <result> at <place>."),
            E("Prompt.Shape.Completed.2", "<subject> <place>에서 <result>를 끝내고 <work>도 마쳤다.", "<subject> finished <result> at <place>, then completed <work>."),
            E("Prompt.Shape.Completed.3", "<subject> <place>에서 <work>와 <result>를 마쳤다.", "<subject> completed <work> and <result> at <place>."),
            E("Prompt.Shape.Failed.0", "<subject> <place>에서 <action>을 시도했지만 <reason>으로 멈췄다.", "<subject> tried <action> at <place> but stopped because of <reason>."),
            E("Prompt.Shape.Failed.1", "<subject> <action>에 나섰지만 <place>에서 <reason>에 막혔다.", "<subject> set out for <action> but met <reason> at <place>."),
            E("Prompt.Shape.Failed.2", "<subject> <place>에서 <reason>에 막혀 <action>을 멈췄다.", "<subject> stopped <action> after meeting <reason> at <place>."),
            E("Prompt.Shape.Failed.3", "<subject> <action>을 이어가려 했지만 <place>에서 <reason>으로 중단했다.", "<subject> tried to continue <action> but stopped at <place> because of <reason>."),
            E("Prompt.Shape.Started.0", "<subject> <place>에서 <action>을 시작했다.", "<subject> began <action> at <place>."),
            E("Prompt.Shape.Started.1", "<subject> <place>에서 <action>에 착수했다.", "<subject> started <action> at <place>."),
            E("Prompt.Shape.Started.2", "<subject> <place>로 향해 <action>에 착수했다.", "<subject> headed to <place> and started <action>."),
            E("Prompt.Shape.Started.3", "<subject> <place>에서 <action>을 시작했다.", "<subject> began <action> at <place>."),
            E("Prompt.StyleBrief.0", "담백한 현장 기록: 장소에서 행동으로 이어지는 짧고 또렷한 문장.", "Plain field note: a short, clear sentence moving from place to action."),
            E("Prompt.StyleBrief.1", "행동 중심 기록: 행동을 먼저 꺼내고 착수·끝냄을 경쾌하게 표현.", "Action-led note: lead with the action and express beginning or completion briskly."),
            E("Prompt.StyleBrief.2", "이동감 있는 기록: 향하다·들어가다·마무리하다를 자연스럽게 연결.", "Movement-led note: connect heading, entering, and finishing naturally."),
            E("Prompt.StyleBrief.3", "결과 중심 기록: 결과나 변화를 먼저 세우고 마지막에 장소와 행동을 매듭.", "Result-led note: lead with the change, then close with place and action."),
            E("Prompt.SituationBrief.0", "엉뚱한 길로 샜다가 금세 돌아오는 실수.", "A harmless wrong turn followed by a quick return."),
            E("Prompt.SituationBrief.1", "잠깐 허둥댄 뒤 태연한 척하는 버릇.", "A brief fumble followed by pretending nothing happened."),
            E("Prompt.SituationBrief.2", "한 박자 멈칫했다가 머쓱하게 다시 나서는 반응.", "A brief hesitation followed by an awkward recovery."),
            E("Prompt.SituationBrief.3", "딴생각에서 퍼뜩 깨어나 원래 행동을 잇는 반전.", "Snapping out of a distraction and returning to the action."),
            E("Fallback.SituationLead.0", "엉뚱한 길로 샜다가 돌아와", "after returning from a harmless wrong turn"),
            E("Fallback.SituationLead.1", "잠깐 허둥댄 뒤 태연한 척하고", "after a brief fumble, pretending nothing happened"),
            E("Fallback.SituationLead.2", "한 박자 멈칫했다가 머쓱하게", "after an awkward hesitation"),
            E("Fallback.SituationLead.3", "딴생각에서 퍼뜩 깨어나", "after snapping out of a distraction"),
            E("Fallback.OffDuty", "{0} {1} 시작했다.", "started {1} toward {0}."),
            E("Fallback.CompletedThree.0", "{0}에서 {1} 마치고 {2}도 끝냈다.", "finished {1} at {0} and completed {2}."),
            E("Fallback.CompletedThree.1", "{0}에서 {3} {4} 마쳤다.", "completed {3} {4} at {0}."),
            E("Fallback.CompletedThree.2", "{0}에서 {4} 끝내고 {5}도 마쳤다.", "finished {4} at {0}, then completed {5}."),
            E("Fallback.CompletedThree.3", "{0}에서 {3} {4} 마쳤다.", "completed {3} {4} at {0}."),
            E("Fallback.CompletedTwo", "{0}에서 {1} 마무리했다.", "finished {1} at {0}."),
            E("Fallback.Started.0", "{0}에서 {1} 시작했다.", "began {1} at {0}."),
            E("Fallback.Started.1", "{0}에서 {2}에 착수했다.", "started {2} at {0}."),
            E("Fallback.Started.2", "{3} 향해 {2}에 착수했다.", "headed toward {3} and started {2}."),
            E("Fallback.Started.3", "{0}에서 {1} 시작했다.", "began {1} at {0}.")
        };

        AddWorkLabel(entries, "work:operate", "운영", "operation");
        AddWorkLabel(entries, "work:restock", "보충", "restocking");
        AddWorkLabel(entries, "work:repair", "수리", "repair");
        AddWorkLabel(entries, "work:clean", "청소", "cleaning");
        AddWorkLabel(entries, "work:research", "연구", "research");
        AddWorkLabel(entries, "work:guard", "경비", "guard duty");
        AddWorkLabel(entries, "work:reception", "응대", "reception");
        AddWorkLabel(entries, "work:rescue", "구조", "rescue");
        AddWorkLabel(entries, "work:rest", "휴식", "rest");
        AddWorkLabel(entries, "work:craft", "제작", "crafting");
        AddWorkLabel(entries, "work:haul", "운반", "hauling");
        AddWorkLabel(entries, "work:hunt", "사냥", "hunting");
        AddWorkLabel(entries, "work:butcher", "도축", "butchering");
        AddWorkLabel(entries, "work:draw-water", "급수", "drawing water");
        AddWorkLabel(entries, "work:cook", "조리", "cooking");
        AddWorkLabel(entries, "work:treat", "치료", "treatment");
        AddWorkLabel(entries, "work:refuel", "연료 보충", "refueling");
        AddWorkLabel(entries, "work:alchemy-research", "연금 연구", "alchemy research");
        AddWorkLabel(entries, "work:weapon-sales", "무기 판매", "weapon sales");
        AddWorkLabel(entries, "work:cleaning", "청소", "cleaning");

        string[] specialized =
        {
            "work:research", "work:clean", "work:repair", "work:restock",
            "work:guard", "work:reception", "work:craft", "work:haul",
            "work:hunt", "work:butcher", "work:draw-water", "work:cook",
            "work:treat", "work:refuel"
        };
        foreach (string id in specialized)
        {
            bool hunt = string.Equals(id, "work:hunt", StringComparison.Ordinal);
            entries.Add(E(
                "Template.WorkStarted." + id,
                hunt
                    ? "{subject} {target} 흔적을 살피며 사냥에 나섰다.\n{subject} {target} 쪽으로 조용히 발을 옮겼다."
                    : "{subject} {place}에서 {workObject} 시작했다.\n{subject} 숨을 고르고 {workObject} 붙잡았다.\n{subject} {place}로 향해 {workObject} 들어갔다.",
                hunt
                    ? "{subject} followed signs of {target} and began the hunt.\n{subject} moved quietly toward {target}."
                    : "{subject} began {workObject} at {place}.\n{subject} took a breath and began {workObject}.\n{subject} headed to {place} for {workObject}."));
            entries.Add(E(
                "Template.WorkCompleted." + id,
                hunt
                    ? "{subject} {target} 추적을 마치고 돌아왔다.\n{subject} {target} 흔적을 정리하며 사냥을 끝냈다."
                    : "{subject} {place}에서 {workObject} 마쳤다.\n{subject} {workObject} 끝내고 숨을 돌렸다.\n{subject} {place}를 정리하며 {workObject} 마무리했다.",
                hunt
                    ? "{subject} returned after tracking {target}.\n{subject} concluded the hunt after reviewing signs of {target}."
                    : "{subject} finished {workObject} at {place}.\n{subject} completed {workObject} and caught their breath.\n{subject} wrapped up {workObject} while tidying {place}."));
        }

        return entries.ToArray();
    }

    private static void AddWorkLabel(
        ICollection<Entry> entries,
        string id,
        string korean,
        string english) =>
        entries.Add(E("WorkLabel." + id, korean, english));

    private static Entry E(string key, string korean, string english) =>
        new Entry(key, korean, english);

    private static Locale RequireLocale(string code, string name) =>
        LocalizationEditorSettings.GetLocale(code)
        ?? throw new InvalidOperationException($"{name} locale '{code}' is missing.");

    private static StringTable RequireTable(
        StringTableCollection collection,
        Locale locale,
        string path) =>
        collection.GetTable(locale.Identifier) as StringTable
        ?? collection.AddNewTable(locale.Identifier, path) as StringTable
        ?? throw new InvalidOperationException(
            $"Could not create CharacterNarrative table for '{locale.Identifier.Code}'.");

    private static void RemoveObsoleteEntries(StringTableCollection collection)
    {
        HashSet<string> required = new HashSet<string>(
            Entries.Select(entry => entry.Key),
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
        if (korean == null || english == null)
        {
            throw new InvalidOperationException(
                "CharacterNarrative ko/en tables are required.");
        }

        string[] duplicates = Entries.GroupBy(
                entry => entry.Key,
                StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                "CharacterNarrative duplicate keys: "
                + string.Join(", ", duplicates));
        }

        string[] expected = Entries.Select(entry => entry.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] actual = collection.SharedData.Entries
            .Select(entry => entry.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "CharacterNarrative must contain exactly the authored key set.");
        }

        foreach (Entry entry in Entries)
        {
            string koreanValue = Require(korean, entry.Key);
            string englishValue = Require(english, entry.Key);
            string[] koreanPlaceholders = GetPlaceholders(koreanValue);
            string[] englishPlaceholders = GetPlaceholders(englishValue);
            if (!koreanPlaceholders.SequenceEqual(
                    englishPlaceholders,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"CharacterNarrative placeholder mismatch for '{entry.Key}'.");
            }

            ValidateNumericFormat(entry.Key, "ko", koreanValue);
            ValidateNumericFormat(entry.Key, "en", englishValue);
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

    private static string[] GetPlaceholders(string value) =>
        Regex.Matches(value, @"\{(?<key>[A-Za-z][A-Za-z0-9]*|\d+)(?:[^{}]*)\}")
            .Cast<Match>()
            .Select(match => match.Groups["key"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

    private static void ValidateNumericFormat(
        string key,
        string locale,
        string template)
    {
        int[] indexes = Regex.Matches(template, @"\{(?<index>\d+)(?:[^{}]*)\}")
            .Cast<Match>()
            .Select(match => int.Parse(
                match.Groups["index"].Value,
                CultureInfo.InvariantCulture))
            .Distinct()
            .ToArray();
        if (indexes.Length == 0)
        {
            return;
        }

        object[] arguments = Enumerable.Repeat<object>(
            0,
            indexes.Max() + 1).ToArray();
        try
        {
            string.Format(CultureInfo.InvariantCulture, template, arguments);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"CharacterNarrative '{key}' has invalid {locale} formatting.",
                exception);
        }
    }
}
#endif
