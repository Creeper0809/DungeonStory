using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ResearchProgressRules
{
    public const float BaseResearchWorkPerSecond = 4f;

    public static float ClampRequiredWork(float requiredWork) =>
        Mathf.Max(1f, requiredWork);

    public static float ClampProgress(float progress, float requiredWork) =>
        Mathf.Clamp(progress, 0f, ClampRequiredWork(requiredWork));

    public static float AddProgress(
        ref float progress,
        float amount,
        float requiredWork)
    {
        float before = ClampProgress(progress, requiredWork);
        progress = Mathf.Min(
            ClampRequiredWork(requiredWork),
            before + Mathf.Max(0f, amount));
        return progress - before;
    }

    public static float ProgressRatio(float progress, float requiredWork) =>
        Mathf.Clamp01(ClampProgress(progress, requiredWork)
            / ClampRequiredWork(requiredWork));

    public static float CalculateResearchWork(
        float seconds,
        float characterMultiplier,
        float facilityMultiplier,
        float skillBonus)
    {
        float baseWork = Mathf.Max(0f, seconds)
            * BaseResearchWorkPerSecond
            * Mathf.Max(0.05f, characterMultiplier)
            * Mathf.Max(0f, facilityMultiplier);
        return baseWork + Mathf.Max(0f, skillBonus);
    }
}
