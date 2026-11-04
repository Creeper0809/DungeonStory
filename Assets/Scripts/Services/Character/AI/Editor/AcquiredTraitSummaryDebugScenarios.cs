#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

// 저장된 특성의 실제 표시 경로 검사. 모델 호출과 상태 변경 없음.
public static class AcquiredTraitSummaryDebugScenarios
{
    public static void VerifyForBatch()
    {
        var trait = new CharacterAcquiredTraitInstanceState
        {
            instanceId = "acquired-trait-instance:qa-private-id",
            displayName = "흔들림 없는 손",
            formulaVersion = 1,
            manifestationMilestone = 2,
            description = "craft:quality-score AddFlat=2.2; potencyScale=1",
            mechanicalDescription = "craft:quality-score AddFlat=2.2; potencyScale=1",
            narrativeFlavor = "유나는 손끝을 가다듬어 작업을 마무리한다.",
            evidenceFactIds = new List<string> { "fact:qa-private-source" }
        };
        string before = JsonUtility.ToJson(trait);
        string active = Render(trait, "- ");
        Require(active.Contains(trait.displayName) && active.Contains(trait.narrativeFlavor)
                && active.Contains("이정표 2"),
            "Formula summary omitted its stored title, narrative or milestone.");
        Require(!active.Contains(trait.description) && !active.Contains(trait.instanceId)
                && !active.Contains(trait.evidenceFactIds[0]),
            "Player summary exposed internal mechanics or evidence identifiers.");
        Require(JsonUtility.ToJson(trait) == before, "Rendering changed the trait state.");

        trait.erased = true;
        string erased = Render(trait, "- [소거] ");
        Require(erased.StartsWith("- [소거] ", StringComparison.Ordinal)
                && erased.Contains(trait.narrativeFlavor),
            "Erased trait summary lost its status or original narrative.");

        trait.formulaVersion = 0;
        trait.description = "오래된 경험을 바탕으로 주변을 살핀다.";
        string legacy = Render(trait, "- ");
        Require(legacy.Contains(trait.description) && !legacy.Contains(trait.narrativeFlavor),
            "Legacy summary no longer uses its authored description field.");

        trait.formulaVersion = 1;
        trait.narrativeFlavor = " ";
        trait.displayName = string.Empty;
        string missing = Render(trait, "- ");
        Require(missing.Contains("특성 이름 없음")
                && missing.Contains("특성 설명이 저장되어 있지 않습니다.")
                && !missing.Contains(trait.description) && !missing.Contains(trait.instanceId),
            "Missing modern narrative was hidden by unrelated legacy or internal text.");
    }

    private static string Render(CharacterAcquiredTraitInstanceState trait, string prefix)
    {
        MethodInfo append = typeof(CharacterSummaryGrowthPresenter).GetMethod(
            "AppendAcquiredTrait", BindingFlags.NonPublic | BindingFlags.Static);
        Require(append != null, "Actual summary renderer is unavailable.");
        var builder = new StringBuilder();
        append.Invoke(null, new object[] { builder, trait, prefix });
        return builder.ToString();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
