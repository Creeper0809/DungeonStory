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

public static class DefenseUiLocalizationAssetBuilder
{
    private const string LocalizationRoot = "Assets/Localization";
    private const string KoreanTablePath =
        LocalizationRoot + "/DefenseUI_ko.asset";
    private const string EnglishTablePath =
        LocalizationRoot + "/DefenseUI_en.asset";

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

    private static readonly Entry[] Entries =
    {
        E("None", "없음", "None"),
        E("BuildingFallback", "시설", "Facility"),
        E("PolicyDefaultName", "새 정책 {0}", "New Policy {0}"),
        E("PolicyCopyName", "{0} 사본", "{0} Copy"),
        E("PolicyCreated", "방어 정책 생성: {0}", "Defense policy created: {0}"),
        E("PolicyCreateFailed", "방어 정책을 만들지 못했습니다.", "Could not create the defense policy."),
        E("PolicyDuplicated", "방어 정책 복제: {0}", "Defense policy duplicated: {0}"),
        E("PolicyDuplicateFailed", "방어 정책을 복제하지 못했습니다.", "Could not duplicate the defense policy."),
        E("PolicyDeleted", "정책을 삭제하고 경비를 표준 정책으로 재배정했습니다.", "Policy deleted; guards were reassigned to the standard policy."),
        E("PolicyDeleteFailed", "기본 정책은 삭제할 수 없습니다.", "The standard policy cannot be deleted."),
        E("PolicyUpdated", "방어 정책 갱신: {0}", "Defense policy updated: {0}"),
        E("PolicyUpdateFailed", "방어 정책을 갱신하지 못했습니다.", "Could not update the defense policy."),
        E("PolicyMissing", "방어 정책을 찾지 못했습니다.", "The defense policy was not found."),
        E("PolicyStandard", "표준", "Standard"),
        E("PolicyHold", "사수", "Hold"),
        E("PolicyRetreat", "후퇴", "Retreat"),
        E("PolicySelected", "방어 정책 선택: {0}", "Defense policy selected: {0}"),
        E("PolicySelectionSummary", "정책 {0}개 / 선택 {1}", "{0} policies / Selected: {1}"),
        E("PolicyUnavailable", "사용 가능한 정책이 없습니다.", "No defense policies are available."),
        E("PolicyDeleteConfirmFeedback", "정책 삭제를 한 번 더 확인하세요.", "Confirm policy deletion once more."),
        E("PolicyDetail", "출동 체력 {0:P0} / 후퇴 {1} / 재참전 {2:P0} / 대체자 없음 {3}", "Dispatch health {0:P0} / Retreat {1} / Rejoin {2:P0} / No replacement: {3}"),
        E("GuardAssignmentTargetMissing", "배정할 경비를 찾지 못했습니다.", "The guard to assign was not found."),
        E("GuardPolicyAssigned", "{0}: 정책 배정 완료", "{0}: policy assigned"),
        E("GuardPolicyAssignmentFailed", "정책을 배정하지 못했습니다.", "Could not assign the policy."),
        E("GuardPolicyAssignmentSummary", "직원 {0}명 / 배정할 정책 {1}", "{0} staff / Policy to assign: {1}"),
        E("GuardDetail", "현재 {0} / {1} / 경비 우선순위 {2}", "Current {0} / {1} / Guard priority {2}"),
        E("DutyOff", "비번", "Off duty"),
        E("DutyOn", "당직", "On duty"),
        E("FacilityMissing", "방어시설을 찾을 수 없습니다.", "The defense facility was not found."),
        E("FacilityArmingPolicyChanged", "무장 정책 {0} → {1}", "Arming policy {0} → {1}"),
        E("FacilityArmingPolicyChangeFailed", "무장 정책을 변경하지 못했습니다.", "Could not change the arming policy."),
        E("FacilityServiceRequested", "방어시설 정비를 요청했습니다.", "Defense-facility service requested."),
        E("ThreatFactors", "현재 요인: {0}", "Current factors: {0}"),
        E("ThreatUnavailable", "침공 위협 시스템을 불러오지 못했습니다.", "The invasion-threat system is unavailable."),
        E("ThreatInformationNone", "위협 정보 없음", "No threat information"),
        E("ThreatForecastPending", "대기", "Pending"),
        E("ThreatForecastNone", "없음", "None"),
        E("ThreatSummary", "위협 {0:0.#} / 단계 {1} / 안전 {2:0.#}초 / 예보 {3}", "Threat {0:0.#} / Stage {1} / Safety {2:0.#}s / Forecast {3}"),
        E("IntruderFront", "전선 {0} / 선두 {1} / 예비 {2} / 공방 {3}회", "Front {0} / Lead {1} / Reserve {2} / Exchanges {3}"),
        E("PrimaryTargetFallback", "사장 또는 주요 시설", "Owner or key facility"),
        E("UnknownIntruder", "미확인 침입자", "Unknown intruder"),
        E("IntruderFallback", "침입자", "Intruder"),
        E("IntruderTitle", "{0} / {1}", "{0} / {1}"),
        E("IntruderDetail", "상태 {0} / 집중 {1:0.#} / 목표 {2}\n{3}", "State {0} / Focus {1:0.#} / Target {2}\n{3}"),
        E("IntruderStateUnavailable", "침입 상태를 확인할 수 없습니다.", "Intruder state is unavailable."),
        E("IntruderAdvanceNone", "상태 없음", "No state"),
        E("IntruderAdvanceRallying", "외부 집결 {0}초 / 경비 대기", "Rallying outside {0}s / Guards waiting"),
        E("IntruderAdvanceInterior", "내부 진격 중 / 전선 미형성", "Advancing inside / Front not formed"),
        E("IntruderAdvanceEntrance", "입구 접근 중 / 경비 대기", "Approaching entrance / Guards waiting"),
        E("DefenseHudIdle", "집결 대기 / 침입자가 나타나면 작전·경로·돌파 상태를 표시합니다.", "Waiting for a raid / Operation, route, and breach state appear when intruders arrive."),
        E("Identification.Full", "완전 식별", "Fully identified"),
        E("Identification.Target", "목표 식별", "Target identified"),
        E("Identification.Sign", "징후 식별", "Signs identified"),
        E("Identification.None", "미식별", "Unidentified"),
        E("ExpectedRoute", "예상 경로 {0}칸", "Expected route: {0} cells"),
        E("ExpectedRouteUnknown", "예상 경로 미확인", "Expected route unknown"),
        E("RouteChangeNone", "경로 변경 없음", "No route change"),
        E("DefenseHudBreach", "돌파 / {0} / {1}\n{2} {3:0}/{4:0} / 공격 {5}명 / 예상 {6:0.0}초{7}\n{8} / {9}", "Breach / {0} / {1}\n{2} {3:0}/{4:0} / Attackers {5} / ETA {6:0.0}s{7}\n{8} / {9}"),
        E("EnragedBreachSuffix", " / 격앙된 돌파", " / Enraged breach"),
        E("RallyPhase", "집결 / 진입까지 {0}초", "Rally / Entry in {0}s"),
        E("EngagementPhase", "교전 / {0}", "Engagement / {0}"),
        E("DefenseHudActive", "{0} / {1} / {2}\n{3} / 알려진 위험 {4}곳\n경로 판단: {5}", "{0} / {1} / {2}\n{3} / Known risks {4}\nRoute decision: {5}"),
        E("CampaignOperation", "{0} / 목표 {1} / 정보 신뢰도 {2:P0}", "{0} / Objective {1} / Intelligence confidence {2:P0}"),
        E("CampaignOperationNone", "예정 작전 없음", "No scheduled operation"),
        E("CampaignBranch", "{0} {1:0}", "{0} {1:0}"),
        E("CampaignBranchRecovery", " ({0})", " ({0})"),
        E("CampaignBranchNone", "지부 정보 없음", "No branch information"),
        E("CampaignSummary", "{0}\n최약 지부: {1}", "{0}\nWeakest branch: {1}"),
        E("ReinforcementNone", "이동 중이거나 도착한 동맹 지원군이 없습니다.", "No allied reinforcements are traveling or present."),
        E("ReinforcementRoute", "{0} / {1} / 도착 예정일 {2} / 전력 {3}", "{0} / {1} / ETA day {2} / Strength {3}"),
        E("ReportDefended", "방어 성공", "Defense succeeded"),
        E("ReportFailed", "방어 실패", "Defense failed"),
        E("ReportTitle", "{0} / 위협 {1:0.#}", "{0} / Threat {1:0.#}"),
        E("ReportSummary", "잔여 위험 {0:0.#} / 방어 기여 {1}개 / 손상 {2}개", "Residual risk {0:0.#} / Contributions {1} / Damaged {2}"),
        E("OwnerEvacuationIdle", "침공이 시작되면 사장이 안전한 내부 칸으로 대피합니다.", "The owner will evacuate to a safe interior cell when an invasion starts."),
        E("OwnerEvacuationActive", "{0} / 목표 {1}", "{0} / Target {1}"),
        E("OwnerEvacuationCompleteSuffix", " / 대피 완료", " / Evacuation complete"),
        E("PowerNormal", "전력 정상", "Power online"),
        E("PowerOutage", "정전", "Power outage"),
        E("SupplyNotRequired", "보급 불필요", "No supply required"),
        E("LinkConnected", "연결", "Connected"),
        E("LinkDisconnected", "-", "-"),
        E("FacilityDetail", "{0} / {1} / {2} / 보급 {3} / 건전도 {4:0}%\n감지 {5} / 통제 {6} / 보급 {7} / 정비 {8}", "{0} / {1} / {2} / Supply {3} / Condition {4:0}%\nDetection {5} / Control {6} / Supply {7} / Maintenance {8}"),
        E("FacilityBlocked", "\n차단: {0}", "\nBlocked: {0}"),
        E("Section.DefenseHud", "디펜스 HUD", "Defense HUD"),
        E("Section.InvasionThreat", "침공 위협", "Invasion Threat"),
        E("Section.Campaign", "인간 연합 작전", "Human Coalition Operation"),
        E("Section.Reinforcements", "동맹 지원군", "Allied Reinforcements"),
        E("Section.IntruderTracking", "침입자 추적", "Intruder Tracking"),
        E("Section.OwnerEvacuation", "사장 대피", "Owner Evacuation"),
        E("Section.GuardResponsePolicy", "경비 대응 정책", "Guard Response Policy"),
        E("Section.GuardPolicyAssignment", "경비별 정책 배정", "Guard Policy Assignment"),
        E("Section.DefenseFacilities", "방어 시설", "Defense Facilities"),
        E("Section.CombatReports", "침공 전투 보고", "Invasion Combat Reports"),
        E("ActiveIntruderCount", "활성 침입자 {0}명", "{0} active intruders"),
        E("ActiveFacilityCount", "가동 시설 {0}개", "{0} active facilities"),
        E("CompletedReportCount", "완료 기록 {0}건", "{0} completed reports"),
        E("DungeonSafe", "현재 던전은 안전합니다.", "The dungeon is currently safe."),
        E("Action.Track", "추적", "Track"),
        E("Action.Selected", "선택됨", "Selected"),
        E("Action.Select", "선택", "Select"),
        E("Action.EnableAutoResponse", "자동 출동 켜기", "Enable auto response"),
        E("Action.DisableAutoResponse", "자동 출동 끄기", "Disable auto response"),
        E("Action.MinimumDispatchHealth", "최소 출동 체력 {0:P0}", "Minimum dispatch health {0:P0}"),
        E("Action.RetreatHealth", "후퇴 체력 {0}", "Retreat health {0}"),
        E("Action.HoldWithoutReplacement", "대체자 없으면 사수", "Hold without replacement"),
        E("Action.RetreatWithoutReplacement", "대체자 없어도 후퇴", "Retreat without replacement"),
        E("Action.RejoinHealth", "치료 후 재참전 {0:P0}", "Rejoin after treatment {0:P0}"),
        E("Action.NewPolicy", "새 정책", "New policy"),
        E("Action.DuplicatePolicy", "정책 복제", "Duplicate policy"),
        E("Action.ConfirmDelete", "삭제 확정", "Confirm deletion"),
        E("Action.DeletePolicy", "정책 삭제", "Delete policy"),
        E("Action.Execute", "실행", "Execute"),
        E("Action.Assigned", "배정됨", "Assigned"),
        E("Action.AssignPolicy", "이 정책 배정", "Assign this policy"),
        E("Action.CycleArming", "무장 전환", "Cycle arming"),
        E("Action.ClearJam", "걸림 해제", "Clear jam"),
        E("Action.RequestReload", "재장전 요청", "Request reload"),
        E("Action.Detail", "상세", "Details"),
        E("Help.AutoResponse", "당직 중이고 경비 우선순위가 켜진 직원에게만 적용됩니다.", "Applies only to on-duty staff with guard priority enabled."),
        E("Help.StepFivePercent", "누를 때마다 5%씩 조정합니다.", "Adjusts by 5% each time."),
        E("Help.NoAutomaticRetreat", "0%는 자동 후퇴 없음입니다.", "0% disables automatic retreat."),
        E("Help.HoldWithoutReplacement", "예비 경비가 없을 때 선두 경비의 행동을 결정합니다.", "Controls the lead guard when no reserve is available."),
        E("Help.RejoinHealth", "후퇴한 경비가 다시 출동할 최소 체력입니다.", "Minimum health required for a retreated guard to rejoin."),
        E("Help.NewPolicy", "현재 기본값으로 사용자 정책을 만듭니다.", "Creates a custom policy from the current defaults."),
        E("Help.DuplicatePolicy", "현재 정책을 새 사용자 정책으로 복제합니다.", "Duplicates the current policy as a new custom policy."),
        E("Help.DeletePolicyReassign", "배정된 경비는 표준 정책으로 재배정됩니다.", "Assigned guards will return to the standard policy."),
        E("Help.DeletePolicyConfirm", "한 번 더 눌러 삭제를 확정합니다.", "Press once more to confirm deletion."),

        E("ArmingPolicy.Manual", "수동", "Manual"),
        E("ArmingPolicy.Safe", "안전", "Safe"),
        E("ArmingPolicy.Alert", "경계", "Alert"),
        E("ArmingPolicy.Aggressive", "공격", "Aggressive"),
        E("AttackConcept.None", "없음", "None"),
        E("AttackConcept.Physical", "물리", "Physical"),
        E("AttackConcept.Poison", "독", "Poison"),
        E("AttackConcept.Fire", "화염", "Fire"),
        E("AttackConcept.Lightning", "전격", "Lightning"),
        E("AttackConcept.Ice", "빙결", "Ice"),
        E("AttackConcept.Guard", "경비", "Guard"),
        E("OperationalState.Disarmed", "무장 해제", "Disarmed"),
        E("OperationalState.Preparing", "준비 중", "Preparing"),
        E("OperationalState.Ready", "준비 완료", "Ready"),
        E("OperationalState.Detecting", "감지 중", "Detecting"),
        E("OperationalState.Triggered", "발동", "Triggered"),
        E("OperationalState.Cooldown", "재사용 대기", "Cooldown"),
        E("OperationalState.Reloading", "재장전 중", "Reloading"),
        E("OperationalState.Empty", "보급 없음", "Empty"),
        E("OperationalState.Unpowered", "정전", "Unpowered"),
        E("OperationalState.Faulted", "고장", "Faulted"),
        E("OperationalState.Jammed", "걸림", "Jammed"),
        E("OperationalState.Damaged", "파손", "Damaged"),
        E("OperationalState.Destroyed", "파괴", "Destroyed"),
        E("Operation.FrontalAssault", "정면 공격", "Frontal assault"),
        E("Operation.Siege", "공성", "Siege"),
        E("Operation.FacilitySabotage", "시설 공작", "Facility sabotage"),
        E("Operation.Loot", "약탈", "Loot"),
        E("Operation.CaptiveRescue", "포로 구출", "Captive rescue"),
        E("Operation.OwnerAssassination", "사장 암살", "Owner assassination"),
        E("IntruderState.None", "대기", "Waiting"),
        E("IntruderState.Entering", "진입", "Entering"),
        E("IntruderState.Searching", "탐색", "Searching"),
        E("IntruderState.MovingToOwner", "사장 추적", "Tracking owner"),
        E("IntruderState.MovingToFacility", "시설 추적", "Tracking facility"),
        E("IntruderState.DamagingFacility", "시설 파괴", "Damaging facility"),
        E("IntruderState.InterceptPlanned", "저지 예정", "Interception planned"),
        E("IntruderState.Engaged", "교전", "Engaged"),
        E("IntruderState.FrontBroken", "전선 돌파", "Front broken"),
        E("IntruderState.FinalCombat", "최종 교전", "Final combat"),
        E("IntruderState.Finished", "종료", "Finished"),
        E("IntruderState.Rallying", "외부 집결", "Rallying outside"),
        E("IntruderState.Breaching", "구조물 돌파", "Breaching"),
        E("EngagementState.Dispatching", "출동", "Dispatching"),
        E("EngagementState.InterceptPlanned", "저지 예정", "Interception planned"),
        E("EngagementState.Engaged", "교전", "Engaged"),
        E("EngagementState.ReserveWaiting", "교대 대기", "Reserve waiting"),
        E("EngagementState.Switching", "교대", "Switching"),
        E("EngagementState.Retreating", "후퇴", "Retreating"),
        E("EngagementState.FrontCollapsed", "붕괴", "Front collapsed"),
        E("EngagementState.Completed", "종료", "Completed"),
        E("ThreatStage.Peaceful", "평온", "Peaceful"),
        E("ThreatStage.Warning", "경고", "Warning"),
        E("ThreatStage.Candidate", "침공 후보", "Candidate"),
        E("ThreatStage.Safety", "안전 기간", "Safety period"),
        E("FactionRouteStatus.Traveling", "이동 중", "Traveling"),
        E("FactionRouteStatus.Delayed", "지연", "Delayed"),
        E("FactionRouteStatus.Arrived", "도착", "Arrived"),
        E("FactionRouteStatus.Returning", "귀환 중", "Returning"),
        E("FactionRouteStatus.Lost", "소실", "Lost")
    };

    [MenuItem("Tools/DungeonStory/Content/Update Defense UI Localization")]
    public static void Synchronize()
    {
        Locale koreanLocale = RequireLocale("ko", "Korean");
        Locale englishLocale = RequireLocale("en", "English");
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                DomainFailureLocalizer.DefenseTableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(
                DomainFailureLocalizer.DefenseTableName,
                LocalizationRoot,
                new List<Locale> { koreanLocale, englishLocale });
        if (collection == null)
        {
            throw new InvalidOperationException(
                $"Could not create String Table collection "
                + $"'{DomainFailureLocalizer.DefenseTableName}'.");
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
                DomainFailureLocalizer.DefenseTableName);
        }
        Debug.Log(
            $"DefenseUI localization synchronized: {Entries.Length} strict ko/en keys.");
    }

    public static void Validate()
    {
        StringTableCollection collection =
            LocalizationEditorSettings.GetStringTableCollection(
                DomainFailureLocalizer.DefenseTableName)
            ?? throw new InvalidOperationException("DefenseUI collection is missing.");
        Validate(
            collection,
            collection.GetTable(RequireLocale("ko", "Korean").Identifier) as StringTable,
            collection.GetTable(RequireLocale("en", "English").Identifier) as StringTable);
    }

    private static Entry E(string key, string korean, string english) =>
        new Entry(key, korean, english);

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
            $"Could not create DefenseUI table for '{locale.Identifier.Code}'.");

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
            throw new InvalidOperationException("DefenseUI ko/en tables are required.");
        }

        string[] duplicateKeys = Entries
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateKeys.Length > 0)
        {
            throw new InvalidOperationException(
                "DefenseUI duplicate authored keys: " + string.Join(", ", duplicateKeys));
        }

        string[] expected = Entries.Select(entry => entry.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] actual = collection.SharedData.Entries.Select(entry => entry.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "DefenseUI must contain exactly the authored key set.");
        }

        foreach (Entry entry in Entries)
        {
            string koreanValue = Require(korean, entry.Key);
            string englishValue = Require(english, entry.Key);
            int[] koreanIndexes = GetPlaceholderIndexes(koreanValue);
            int[] englishIndexes = GetPlaceholderIndexes(englishValue);
            if (!koreanIndexes.SequenceEqual(englishIndexes))
            {
                throw new InvalidOperationException(
                    $"DefenseUI placeholder mismatch for '{entry.Key}'.");
            }

            ValidateCompositeFormat(entry.Key, "ko", koreanValue, koreanIndexes);
            ValidateCompositeFormat(entry.Key, "en", englishValue, englishIndexes);
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
        int count = placeholders.Count == 0
            ? 0
            : placeholders[placeholders.Count - 1] + 1;
        object[] arguments = Enumerable.Repeat<object>(0, count).ToArray();
        try
        {
            string.Format(CultureInfo.InvariantCulture, template, arguments);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"DefenseUI '{key}' has an invalid {locale} format.",
                exception);
        }
    }
}
#endif
