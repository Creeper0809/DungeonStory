using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class RunVariablePresentationText
{
    public static string ToSummaryText(this RunStartVariableSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return string.Empty;
        }

        return string.Join("\n", new[]
        {
            $"사장 종족: {TextOrDefault(snapshot.ownerSpeciesTag, "미정")}",
            $"시작 시설 후보: {FormatIds(snapshot.startingFacilityCandidateIds)}",
            $"시작 손님층 후보: {FormatStrings(snapshot.startingGuestSpeciesCandidates)}",
            $"시작 설계도 후보: {FormatIds(snapshot.startingBlueprintCandidateIds)}",
            $"초기 상점 시드: {snapshot.initialShopSeed}",
            $"초기 구조: {TextOrDefault(snapshot.initialDungeonLayoutId, "기본")}",
            $"난이도: {snapshot.runDifficulty} / 위협 계수 {snapshot.threatRiseMultiplier:0.##}"
        });
    }

    public static string ToDetailText(this RunVariableDefinition definition)
    {
        return definition == null
            ? string.Empty
            : string.IsNullOrWhiteSpace(definition.detail)
                ? definition.title
                : definition.detail;
    }

    private static string FormatIds(IEnumerable<int> values)
    {
        string text = values != null ? string.Join(", ", values) : string.Empty;
        return string.IsNullOrWhiteSpace(text) ? "없음" : text;
    }

    private static string FormatStrings(IEnumerable<string> values)
    {
        string text = values != null
            ? string.Join(", ", values.Where(value =>
                !string.IsNullOrWhiteSpace(value)))
            : string.Empty;
        return string.IsNullOrWhiteSpace(text) ? "없음" : text;
    }

    private static string TextOrDefault(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
}
