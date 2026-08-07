using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class CharacterSummaryViewFormatting
{
    public static void SetSlider(
        Slider slider,
        IReadOnlyDictionary<CharacterCondition, float> stats,
        CharacterCondition condition)
    {
        if (slider == null || stats == null)
        {
            return;
        }

        float rawValue = stats.TryGetValue(condition, out float value) ? value : 0f;
        SetMeter(slider, rawValue / 100f, $"{Mathf.RoundToInt(rawValue)}");
    }

    public static void SetSlider(
        Slider slider,
        IDictionary<CharacterCondition, float> stats,
        CharacterCondition condition)
    {
        if (slider == null || stats == null)
        {
            return;
        }

        float rawValue = stats.TryGetValue(condition, out float value) ? value : 0f;
        SetMeter(slider, rawValue / 100f, $"{Mathf.RoundToInt(rawValue)}");
    }

    public static void SetMeter(
        Slider slider,
        float normalizedValue,
        string valueText)
    {
        if (slider == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(normalizedValue);
        slider.value = clamped;
        Image fill = slider.fillRect != null
            ? slider.fillRect.GetComponent<Image>()
            : null;
        if (fill != null)
        {
            fill.color = DungeonUiTheme.GetMeterColor(clamped);
        }

        Transform row = slider.transform.parent;
        TMP_Text value = row != null
            ? row.Find("Value")?.GetComponent<TMP_Text>()
            : null;
        if (value == null)
        {
            return;
        }

        value.text = valueText;
        value.color = clamped < 0.25f
            ? DungeonUiTheme.Danger
            : clamped < 0.5f
                ? DungeonUiTheme.Warning
                : DungeonUiTheme.TextSecondary;
    }

    public static string GetDisplayName(GameObject targetObject)
    {
        CharacterIdentity identity = targetObject != null
            ? targetObject.GetComponent<CharacterIdentity>()
            : null;
        if (!string.IsNullOrWhiteSpace(identity?.DisplayName))
        {
            return identity.DisplayName;
        }

        return targetObject != null ? targetObject.name : string.Empty;
    }
}
