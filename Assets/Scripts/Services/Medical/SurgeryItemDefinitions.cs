using System;
using System.Collections.Generic;

public static class SurgeryItemDefinitions
{
    public const string OrganPrefix = "surgery:organ:";
    public const string ProstheticPrefix = "surgery:prosthetic:";
    public const string ContaminatedTissueId = "surgery:contaminated-tissue";
    public const string DisinfectantId = "medicine:disinfectant";
    public const string AnestheticId = "medicine:anesthetic";
    public const string ImmunosuppressantId = "medicine:immunosuppressant";
    public const string BloodPackId = "medicine:blood-pack";

    private static readonly IReadOnlyDictionary<string, string> NodeNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["brain"] = "뇌",
            ["eye:left"] = "왼쪽 눈",
            ["eye:right"] = "오른쪽 눈",
            ["heart"] = "심장",
            ["lung:left"] = "왼쪽 폐",
            ["lung:right"] = "오른쪽 폐",
            ["liver"] = "간",
            ["kidney:left"] = "왼쪽 신장",
            ["kidney:right"] = "오른쪽 신장",
            ["stomach"] = "위",
            ["arm:left"] = "왼팔",
            ["arm:right"] = "오른팔",
            ["leg:left"] = "왼다리",
            ["leg:right"] = "오른다리",
            ["core"] = "핵",
            ["sensory-gel"] = "감각 젤",
            ["pseudopods"] = "위족"
        };

    public static string GetOrganItemId(string nodeId) =>
        OrganPrefix + (nodeId?.Trim() ?? "unknown");

    public static string GetProstheticItemId(string nodeId) =>
        ProstheticPrefix + (nodeId?.Trim() ?? "unknown");

    public static bool TryGetDefinition(
        string itemId,
        out DungeonItemDefinition definition)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (normalized.StartsWith(OrganPrefix, StringComparison.Ordinal))
        {
            string nodeId = normalized.Substring(OrganPrefix.Length);
            string name = NodeNames.TryGetValue(nodeId, out string label)
                ? label
                : nodeId;
            definition = new DungeonItemDefinition(
                normalized,
                $"적출 {name}",
                "기증자와 신선도가 보존되는 고유 수술 장기",
                StockCategory.Biological,
                120,
                null,
                0.8f,
                1);
            return true;
        }

        if (normalized.StartsWith(ProstheticPrefix, StringComparison.Ordinal))
        {
            string nodeId = normalized.Substring(ProstheticPrefix.Length);
            string name = NodeNames.TryGetValue(nodeId, out string label)
                ? label
                : nodeId;
            definition = new DungeonItemDefinition(
                normalized,
                $"{name} 보철",
                "수술로 설치하는 고유 보철 부품",
                StockCategory.General,
                180,
                null,
                1.8f,
                1);
            return true;
        }

        definition = normalized switch
        {
            ContaminatedTissueId => new DungeonItemDefinition(
                normalized, "오염 조직", "부패한 장기 조직. 퇴비·연금·소각에 사용한다.",
                StockCategory.Biological, 1, null, 0.6f, 25),
            DisinfectantId => new DungeonItemDefinition(
                normalized, "소독제", "수술실 세정과 감염 억제에 쓰는 약품",
                StockCategory.Medicine, 18, null, 0.2f, 25),
            AnestheticId => new DungeonItemDefinition(
                normalized, "마취제", "수술 중 통증과 환자 불안정을 낮춘다.",
                StockCategory.Medicine, 30, null, 0.15f, 25),
            ImmunosuppressantId => new DungeonItemDefinition(
                normalized, "면역억제제", "이종 장기 거부 반응을 낮추지만 감염 저항도 낮춘다.",
                StockCategory.Medicine, 65, null, 0.1f, 25),
            BloodPackId => new DungeonItemDefinition(
                normalized, "수혈 팩", "출혈 위험이 큰 이식과 응급 수술에 사용한다.",
                StockCategory.Biological, 35, null, 0.5f, 10),
            _ => null
        };
        return definition != null;
    }
}
