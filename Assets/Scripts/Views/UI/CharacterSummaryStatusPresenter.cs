using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects identity, vitality, survival, and carried-stock status.</summary>
public sealed class CharacterSummaryStatusPresenter
{
    private readonly IDungeonItemCatalogProvider itemCatalog;
    private readonly IItemHaulingSettingsProvider haulingSettings;
    private readonly ISurvivalFoodQuery survivalRuntime;
    private TMP_Text profileText;
    private Slider healthSlider;
    private TMP_Text carrySummaryText;

    public CharacterSummaryStatusPresenter(
        IDungeonItemCatalogProvider itemCatalog,
        IItemHaulingSettingsProvider haulingSettings,
        ISurvivalFoodQuery survivalRuntime)
    {
        this.itemCatalog = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
        this.haulingSettings = haulingSettings
            ?? throw new ArgumentNullException(nameof(haulingSettings));
        this.survivalRuntime = survivalRuntime
            ?? throw new ArgumentNullException(nameof(survivalRuntime));
    }

    public void Bind(TMP_Text generatedProfileText, Slider generatedHealth, TMP_Text generatedCarrySummary)
    {
        profileText = generatedProfileText;
        healthSlider = generatedHealth;
        carrySummaryText = generatedCarrySummary;
    }

    public void Refresh(CharacterActor actor, CharacterStats stats)
    {
        RefreshProfileAndVitals(actor, stats);
        RefreshCarrySummary(actor);
    }

    public void RefreshProfileAndVitals(CharacterActor actor, CharacterStats stats)
    {
        if (actor == null)
        {
            return;
        }

        if (profileText != null)
        {
            string species = !string.IsNullOrWhiteSpace(actor.SpeciesTag)
                ? actor.SpeciesTag
                : CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Status.SpeciesUnknown");
            int actorLevel = actor.Progression != null ? actor.Progression.Level : 1;
            profileText.text = CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Status.Profile",
                actorLevel,
                species,
                CharacterSummaryTextFormatter.FormatRole(actor.Role),
                CharacterSummaryTextFormatter.FormatLifecycle(actor.CurrentLifecycleState));
        }

        if (stats == null)
        {
            SetMeter(healthSlider, 0f, "--");
            return;
        }

        float maximum = Mathf.Max(1f, stats.MaxHealth);
        float current = Mathf.Clamp(stats.CurrentHealth, 0f, maximum);
        int injuryPercent = Mathf.RoundToInt(stats.InjurySeverity * 100f);
        SetMeter(
            healthSlider,
            current / maximum,
            injuryPercent > 0
                ? CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Status.Health.Injury",
                    Mathf.RoundToInt(current),
                    Mathf.RoundToInt(maximum),
                    injuryPercent)
                : CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Status.Health.Normal",
                    Mathf.RoundToInt(current),
                    Mathf.RoundToInt(maximum)));
    }

    public void RefreshCarrySummary(CharacterActor actor)
    {
        if (carrySummaryText == null)
        {
            return;
        }

        CharacterCarryInventory inventory = actor != null
            ? actor.GetComponent<CharacterCarryInventory>()
            : null;
        if (inventory == null)
        {
            carrySummaryText.text = CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Status.Carry.Unavailable");
            carrySummaryText.color = DungeonUiTheme.TextSecondary;
            return;
        }

        float currentWeight = inventory.GetCurrentWeight(itemCatalog);
        float baseLimit = inventory.GetBaseCarryLimit();
        float maxAllowed = inventory.GetMaxAllowedWeight(haulingSettings);
        float speedMultiplier = inventory.GetMoveSpeedMultiplier(itemCatalog, haulingSettings);
        bool overloaded = currentWeight > baseLimit + 0.01f;

        StringBuilder builder = new StringBuilder();
        AppendSurvivalStatus(builder, actor);
        builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
            "CharacterSummary.Status.Carry.Weight",
            CharacterSummaryTextFormatter.FormatWeight(currentWeight),
            CharacterSummaryTextFormatter.FormatWeight(baseLimit),
            CharacterSummaryTextFormatter.FormatWeight(maxAllowed)));
        builder.AppendLine(overloaded
            ? CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Status.Carry.Overloaded",
                Mathf.RoundToInt(speedMultiplier * 100f))
            : CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Status.Carry.Normal"));

        IReadOnlyList<CharacterCarriedItemSaveData> items = inventory.Items;
        List<string> entries = items == null
            ? new List<string>()
            : items
                .Where(item => item != null && item.quantity > 0)
                .GroupBy(item => item.itemId ?? string.Empty)
                .Select(group =>
                {
                    DungeonItemDefinition definition = itemCatalog.GetDefinition(group.Key);
                    return $"{definition.DisplayName} x{group.Sum(item => item.quantity)}";
                })
                .Take(4)
                .ToList();
        builder.Append(entries.Count > 0
            ? string.Join(" · ", entries)
            : CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Status.Carry.NoItems"));

        carrySummaryText.text = builder.ToString();
        carrySummaryText.color = overloaded ? DungeonUiTheme.Warning : DungeonUiTheme.TextSecondary;
    }

    private void AppendSurvivalStatus(StringBuilder builder, CharacterActor actor)
    {
        if (!survivalRuntime.TryGetCharacterStatus(actor, out SurvivalCharacterStatus status))
        {
            return;
        }

        string healthLabel = CharacterSummaryTextFormatter.FormatSurvivalHealthState(status.PrimaryState);
        string temperature = status.TemperatureComfort01 >= 0.75f
            ? CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Status.Temperature.Stable")
            : status.TemperatureComfort01 >= 0.45f
                ? CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Status.Temperature.Caution")
                : CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Status.Temperature.Danger");
        string issueSuffix = status.ActiveIssueCount > 1
            ? CharacterSummaryHealthStatusTextFormatter.Get(
                "CharacterSummary.Status.Survival.IssueSuffix",
                status.ActiveIssueCount - 1)
            : string.Empty;
        builder.AppendLine(CharacterSummaryHealthStatusTextFormatter.Get(
            "CharacterSummary.Status.Survival.Row",
            healthLabel,
            issueSuffix,
            status.FoodSummary,
            status.WaterSummary,
            temperature));
    }

    private static void SetMeter(Slider slider, float normalizedValue, string valueText)
    {
        if (slider == null)
        {
            return;
        }

        float clamped = Mathf.Clamp01(normalizedValue);
        slider.value = clamped;
        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill != null)
        {
            fill.color = DungeonUiTheme.GetMeterColor(clamped);
        }

        Transform row = slider.transform.parent;
        TMP_Text value = row != null ? row.Find("Value")?.GetComponent<TMP_Text>() : null;
        if (value != null)
        {
            value.text = valueText;
            value.color = clamped < 0.25f
                ? DungeonUiTheme.Danger
                : clamped < 0.5f
                    ? DungeonUiTheme.Warning
                    : DungeonUiTheme.TextSecondary;
        }
    }
}
