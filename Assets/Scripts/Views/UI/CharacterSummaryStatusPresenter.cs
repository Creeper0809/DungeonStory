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
    private readonly ICharacterEnvironmentStatusQuery environmentStatus;
    private readonly CharacterSummaryPopulationPresenter population;
    private readonly ICharacterApparelQuery apparel;
    private readonly ICharacterApparelCommand apparelCommands;
    private TMP_Text profileText;
    private Slider healthSlider;
    private TMP_Text carrySummaryText;
    private TMP_Text apparelPolicySummaryText;
    private Button apparelPurposeButton;
    private Button apparelDirectPreferenceButton;
    private CharacterActor currentActor;

    public CharacterSummaryStatusPresenter(
        IDungeonItemCatalogProvider itemCatalog,
        IItemHaulingSettingsProvider haulingSettings,
        ISurvivalFoodQuery survivalRuntime,
        ICharacterEnvironmentStatusQuery environmentStatus,
        CharacterSummaryPopulationPresenter population,
        ICharacterApparelQuery apparel,
        ICharacterApparelCommand apparelCommands)
    {
        this.itemCatalog = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
        this.haulingSettings = haulingSettings
            ?? throw new ArgumentNullException(nameof(haulingSettings));
        this.survivalRuntime = survivalRuntime
            ?? throw new ArgumentNullException(nameof(survivalRuntime));
        this.environmentStatus = environmentStatus
            ?? throw new ArgumentNullException(nameof(environmentStatus));
        this.population = population
            ?? throw new ArgumentNullException(nameof(population));
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.apparelCommands = apparelCommands
            ?? throw new ArgumentNullException(nameof(apparelCommands));
    }

    public void BindPopulation(
        TMP_Text summary,
        Button globalPolicy,
        Button characterPermission) =>
        population.Bind(summary, globalPolicy, characterPermission);

    public void RefreshPopulation(CharacterActor actor) =>
        population.Refresh(actor);

    public void ToggleGlobalApprenticeship(CharacterActor actor) =>
        population.ToggleGlobalPolicy(actor);

    public void ToggleCharacterApprenticeship(CharacterActor actor) =>
        population.ToggleCharacterPermission(actor);

    public void Bind(TMP_Text generatedProfileText, Slider generatedHealth, TMP_Text generatedCarrySummary)
    {
        profileText = generatedProfileText;
        healthSlider = generatedHealth;
        carrySummaryText = generatedCarrySummary;
        EnsureApparelPolicyControls(generatedCarrySummary?.transform.parent);
    }

    public void Refresh(CharacterActor actor, CharacterStats stats)
    {
        currentActor = actor;
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
            StringBuilder profileBuilder = new StringBuilder(
                CharacterSummaryHealthStatusTextFormatter.Get(
                    "CharacterSummary.Status.Profile",
                    actorLevel,
                    species,
                    CharacterSummaryTextFormatter.FormatRole(actor.Role),
                    CharacterSummaryTextFormatter.FormatLifecycle(
                        actor.CurrentLifecycleState)));
            AppendLightAdaptationStatus(profileBuilder, actor);
            profileText.text = profileBuilder.ToString();
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
        currentActor = actor;
        RefreshApparelPolicy(actor);
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

    [GameplayEntryPoint("Character summary apparel purpose button")]
    public void CycleApparelPurpose()
    {
        CharacterId characterId = new(currentActor?.Identity?.PersistentId);
        if (!characterId.IsValid)
        {
            return;
        }
        ApparelSelectionPurpose current = apparel.GetPolicy(characterId).Purpose;
        ApparelSelectionPurpose next = current == ApparelSelectionPurpose.Daily
            ? ApparelSelectionPurpose.Work
            : ApparelSelectionPurpose.Daily;
        apparelCommands.TrySetSelectionPurpose(characterId, next, out _);
        RefreshApparelPolicy(currentActor);
    }

    [GameplayEntryPoint("Character summary exact apparel preference button")]
    public void CycleApparelDirectPreference()
    {
        CharacterId characterId = new(currentActor?.Identity?.PersistentId);
        if (!characterId.IsValid)
        {
            return;
        }
        CharacterApparelPolicySnapshot policy = apparel.GetPolicy(characterId);
        IReadOnlyList<ApparelPolicyChoiceSnapshot> choices =
            apparel.GetPolicyChoices(characterId, policy.Purpose);
        int selected = -1;
        for (int index = 0; index < choices.Count; index++)
        {
            if (choices[index].IsDirectPreference)
            {
                selected = index;
                break;
            }
        }

        apparelCommands.TryClearDirectPreferences(
            characterId,
            policy.Purpose,
            out _);
        int next = selected + 1;
        if (next >= 0 && next < choices.Count)
        {
            apparelCommands.TrySetDirectPreference(
                characterId,
                policy.Purpose,
                choices[next].ItemInstanceId,
                out _);
        }
        RefreshApparelPolicy(currentActor);
    }

    private void RefreshApparelPolicy(CharacterActor actor)
    {
        if (apparelPolicySummaryText == null)
        {
            return;
        }
        CharacterId characterId = new(actor?.Identity?.PersistentId);
        if (!characterId.IsValid)
        {
            apparelPolicySummaryText.text = "의복 정책을 사용할 수 없습니다.";
            return;
        }

        CharacterApparelPolicySnapshot policy = apparel.GetPolicy(characterId);
        IReadOnlyList<ApparelPolicyChoiceSnapshot> choices =
            apparel.GetPolicyChoices(characterId, policy.Purpose);
        ApparelDirectPreferenceSnapshot preferenceReference =
            policy.DirectPreferences.FirstOrDefault(value =>
                value.Purpose == policy.Purpose);
        ApparelPolicyChoiceSnapshot preferred = choices.FirstOrDefault(value =>
            value.ItemInstanceId.Equals(preferenceReference.ItemInstanceId));
        string purpose = policy.Purpose == ApparelSelectionPurpose.Work
            ? "작업"
            : "평상";
        string preference = preferenceReference.ItemInstanceId.IsValid
            && !preferred.ItemInstanceId.IsValid
                ? preferenceReference.ItemInstanceId.Value
                    + " (대상 없음 · 예비 선택)"
            : preferred.ItemInstanceId.IsValid
                ? preferred.DisplayName
                + (preferred.IsConditionAvailable
                    && (preferred.IsAvailable || preferred.IsEquipped)
                    ? string.Empty
                    : " (현재 사용 불가 · 예비 선택)")
            : "조건 자동 선택";
        string failure = policy.LastFailure.IsFailure
            ? "\n" + GameplayUiPresentationText.FailureFallback(
                policy.LastFailure,
                debugMode: false)
            : string.Empty;
        apparelPolicySummaryText.text =
            $"의복 · {purpose} · {preference}{failure}";
        SetButtonLabel(apparelPurposeButton, $"목적: {purpose}");
        SetButtonLabel(
            apparelDirectPreferenceButton,
            preferenceReference.ItemInstanceId.IsValid
                ? "직접 지정 변경"
                : "직접 지정");
    }

    private void EnsureApparelPolicyControls(Transform parent)
    {
        if (parent == null || apparelPolicySummaryText != null)
        {
            return;
        }

        Transform existingSummary = parent.Find("ApparelPolicySummary");
        if (existingSummary != null)
        {
            apparelPolicySummaryText = existingSummary.GetComponent<TMP_Text>();
            Transform existingRow = parent.Find("ApparelPolicyCommands");
            apparelPurposeButton = existingRow?.Find("Purpose")?.GetComponent<Button>();
            apparelDirectPreferenceButton = existingRow
                ?.Find("DirectPreference")?.GetComponent<Button>();
            BindApparelButtons();
            return;
        }

        GameObject summaryObject = new GameObject(
            "ApparelPolicySummary",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        summaryObject.transform.SetParent(parent, worldPositionStays: false);
        apparelPolicySummaryText = summaryObject.GetComponent<TextMeshProUGUI>();
        CopyTextStyle(carrySummaryText, apparelPolicySummaryText);
        apparelPolicySummaryText.fontSize = 14f;
        apparelPolicySummaryText.color = DungeonUiTheme.TextSecondary;
        apparelPolicySummaryText.alignment = TextAlignmentOptions.TopLeft;
        apparelPolicySummaryText.textWrappingMode = TextWrappingModes.Normal;
        summaryObject.GetComponent<LayoutElement>().minHeight = 48f;

        GameObject rowObject = new GameObject(
            "ApparelPolicyCommands",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        rowObject.transform.SetParent(parent, worldPositionStays: false);
        HorizontalLayoutGroup row = rowObject.GetComponent<HorizontalLayoutGroup>();
        row.spacing = 6f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;
        rowObject.GetComponent<LayoutElement>().preferredHeight = 40f;
        apparelPurposeButton = CreateApparelButton(
            rowObject.transform,
            "Purpose");
        apparelDirectPreferenceButton = CreateApparelButton(
            rowObject.transform,
            "DirectPreference");
        BindApparelButtons();
    }

    private void BindApparelButtons()
    {
        if (apparelPurposeButton != null)
        {
            apparelPurposeButton.onClick.RemoveListener(CycleApparelPurpose);
            apparelPurposeButton.onClick.AddListener(CycleApparelPurpose);
        }
        if (apparelDirectPreferenceButton != null)
        {
            apparelDirectPreferenceButton.onClick.RemoveListener(
                CycleApparelDirectPreference);
            apparelDirectPreferenceButton.onClick.AddListener(
                CycleApparelDirectPreference);
        }
    }

    private Button CreateApparelButton(Transform parent, string name)
    {
        GameObject buttonObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, worldPositionStays: false);
        Image image = buttonObject.GetComponent<Image>();
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        DungeonUiTheme.StyleButton(button);

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, worldPositionStays: false);
        TMP_Text label = labelObject.GetComponent<TextMeshProUGUI>();
        CopyTextStyle(carrySummaryText, label);
        label.fontSize = 14f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 2f);
        rect.offsetMax = new Vector2(-6f, -2f);
        return button;
    }

    private static void CopyTextStyle(TMP_Text source, TMP_Text target)
    {
        if (source == null || target == null)
        {
            return;
        }
        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TMP_Text label = button?.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (label != null)
        {
            label.text = value ?? string.Empty;
        }
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

    private void AppendLightAdaptationStatus(
        StringBuilder builder,
        CharacterActor actor)
    {
        CharacterId characterId = new(actor?.Identity?.PersistentId);
        if (!characterId.IsValid
            || !environmentStatus.TryGetLightAdaptation(
                characterId,
                out CharacterLightAdaptationSnapshot adaptation)
            || !adaptation.Enabled)
        {
            return;
        }

        builder.AppendLine();
        builder.Append(CharacterSummaryHealthStatusTextFormatter.Get(
            "CharacterSummary.Status.LightAdaptation.Row",
            adaptation.ActualLight,
            adaptation.ComfortableMinimum,
            adaptation.ComfortableMaximum,
            adaptation.Discomfort * 100f,
            adaptation.MoodContribution,
            adaptation.WorkSpeedContribution * 100f));
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
