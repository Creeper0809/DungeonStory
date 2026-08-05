using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class DungeonTitleTextCatalog
{
    public static string SurvivalPressureModifier(
        DungeonSurvivalPressure pressure)
    {
        return pressure switch
        {
            DungeonSurvivalPressure.Relaxed =>
                "욕구 감소·피로 75% · 회복 110% · 붕괴 유예 45초",
            DungeonSurvivalPressure.Harsh =>
                "욕구 감소·피로 125% · 회복 90% · 붕괴 유예 24초",
            _ =>
                "욕구 감소·피로 100% · 회복 100% · 붕괴 유예 30초"
        };
    }

    public static string SurvivalPressureDescription(
        DungeonSurvivalPressure pressure)
    {
        return pressure switch
        {
            DungeonSurvivalPressure.Relaxed =>
                "생활 대응에 여유가 많고 개인용 물 소비와 결핍 부담이 낮습니다.",
            DungeonSurvivalPressure.Harsh =>
                "욕구와 결핍 부담이 빠르게 악화되며 생활 회복 효율이 낮습니다.",
            _ =>
                "하루 1~1.5회의 식사·음수와 20~25%의 생활 대응 시간을 목표로 합니다."
        };
    }

    public static string DifficultyName(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => "쉬움",
            DungeonDifficulty.Hard => "어려움",
            _ => "보통"
        };
    }

    public static string DifficultyRowSubtitle(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => "적 80%",
            DungeonDifficulty.Hard => "적 125%",
            _ => "기본"
        };
    }

    public static string DifficultyModifier(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy => "전투 보정  적 체력 80% · 공격 80%",
            DungeonDifficulty.Hard =>
                "전투 보정  적 체력 125% · 공격 120% · 주도권 110%",
            _ => "전투 보정  기본 수치"
        };
    }

    public static string DifficultyDescription(DungeonDifficulty difficulty)
    {
        return difficulty switch
        {
            DungeonDifficulty.Easy =>
                "1. 적 전투 수치가 낮아집니다.\n2. 초반 침입 압박이 완만합니다.\n3. 시스템을 익히기 좋은 난이도입니다.",
            DungeonDifficulty.Hard =>
                "1. 적 체력 125%, 공격 120%, 주도권 110%.\n2. 침입과 오펜스 실패 압박이 큽니다.\n3. 장비와 회복 순환을 적극적으로 요구합니다.",
            _ =>
                "1. 기본 전투 수치로 시작합니다.\n2. 운영, 방어, 오펜스를 고르게 요구합니다.\n3. 권장 기준 난이도입니다."
        };
    }

    public static string SlotMetadata(DungeonSaveSlotInfo info)
    {
        if (info == null)
        {
            return "비어 있음";
        }

        if (!info.IsValid)
        {
            return string.IsNullOrWhiteSpace(info.IncompatibilityReason)
                ? "읽을 수 없는 저장 데이터"
                : info.IncompatibilityReason;
        }

        DateTime timestamp = ParseSavedAt(info.SavedAtUtc);
        string date = timestamp == DateTime.MinValue
            ? "저장 시각 없음"
            : timestamp.ToLocalTime().ToString(
                "M월 d일 HH:mm",
                CultureInfo.CurrentCulture);
        string debugBadge = info.DebugModified
            ? " · 디버그 사용"
            : string.Empty;
        string survival = DungeonSurvivalPressureRules.GetDisplayName(
            DungeonSurvivalPressureRules.Normalize(
                info.SurvivalPressureValue));
        return $"{date}\n{Mathf.Max(1, info.Day)}일차 · "
            + $"{Mathf.Max(0, info.Money):N0} 골드 · 생존 {survival}{debugBadge}";
    }

    public static DateTime ParseSavedAt(string value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
    }

    public static string ButtonLabel(string name, string fallback)
    {
        return name switch
        {
            "ContinueLatestButton" => "이어하기",
            "StartNewRunButton" => "새 게임",
            "StartupSettingsButton" => "설정",
            "StartupQuitButton" => "종료",
            "DifficultyEasyButton" => DifficultyName(DungeonDifficulty.Easy)
                + "\n" + DifficultyRowSubtitle(DungeonDifficulty.Easy),
            "DifficultyNormalButton" => DifficultyName(DungeonDifficulty.Normal)
                + "\n" + DifficultyRowSubtitle(DungeonDifficulty.Normal),
            "DifficultyHardButton" => DifficultyName(DungeonDifficulty.Hard)
                + "\n" + DifficultyRowSubtitle(DungeonDifficulty.Hard),
            "DifficultyCancelButton" => "이전",
            "DifficultyNextButton" => "다음",
            _ when name != null
                && name.StartsWith("LoadButton_", StringComparison.Ordinal) =>
                    "불러오기",
            _ when name != null
                && name.StartsWith("DeleteButton_", StringComparison.Ordinal) =>
                    "삭제",
            _ => fallback
        };
    }
}
