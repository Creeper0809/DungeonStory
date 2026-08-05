using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ResearchSaveValidation
{
    public static bool IsFiniteInRange(float value, float minimum, float maximum)
    {
        return float.IsFinite(value)
            && value >= minimum
            && value <= Math.Max(minimum, maximum);
    }

    public static void RequireCanonicalId(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} id must be non-empty and canonical.");
        }
    }

    public static void RequireCanonicalTextOrEmpty(string value, string label)
    {
        if (value == null
            || (!string.IsNullOrEmpty(value)
                && !string.Equals(value, value.Trim(), StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"{label} must be non-null and canonical.");
        }
    }

    public static float RestoreProgressRatio(
        float savedProgress,
        float requiredWorkAtCapture,
        float currentRequiredWork)
    {
        if (!float.IsFinite(requiredWorkAtCapture)
            || requiredWorkAtCapture < 1f
            || !IsFiniteInRange(savedProgress, 0f, requiredWorkAtCapture))
        {
            throw new InvalidOperationException("Research progress capture is invalid.");
        }

        return Math.Max(1f, currentRequiredWork)
            * (savedProgress / requiredWorkAtCapture);
    }
}
