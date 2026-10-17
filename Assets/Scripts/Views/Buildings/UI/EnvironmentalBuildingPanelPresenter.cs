using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IEnvironmentalBuildingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> setStatus,
        Action refresh);
}

public sealed class EnvironmentalBuildingPanelPresenter :
    IEnvironmentalBuildingPanelPresenter
{
    private readonly IEnvironmentalFieldQuery query;
    private readonly IEnvironmentalFieldCommand commands;
    private readonly IFluidInfrastructureQuery fluid;
    private readonly IDomainFailureLocalizer failureLocalizer;

    public EnvironmentalBuildingPanelPresenter(
        IEnvironmentalFieldQuery query,
        IEnvironmentalFieldCommand commands,
        IFluidInfrastructureQuery fluid,
        IDomainFailureLocalizer failureLocalizer)
    {
        this.query = query
            ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands
            ?? throw new ArgumentNullException(nameof(commands));
        this.fluid = fluid
            ?? throw new ArgumentNullException(nameof(fluid));
        this.failureLocalizer = failureLocalizer
            ?? throw new ArgumentNullException(nameof(failureLocalizer));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> setStatus,
        Action refresh)
    {
        List<GameObject> created = new List<GameObject>();
        if (parent == null || building == null)
        {
            return created;
        }

        if (fluid.TryGetWaterCondition(
                building,
                out BuildingWaterConditionSnapshot water))
        {
            created.Add(CreateText(
                parent,
                FormatWaterCondition(water),
                font,
                54f,
                "EnvironmentalWaterConditionText"));
        }

        BuildingThermalEmitterAbility emitter =
            building.BuildingData
                ?.GetAbility<BuildingThermalEmitterAbility>();
        if (emitter?.playerConfigurable != true
            || !query.TryGetTargetTemperature(
                building.centerPos,
                out float target))
        {
            return created;
        }

        GameObject title = CreateText(
            parent,
            $"목표 온도 {target:0.#}°C",
            font);
        created.Add(title);
        GameObject row = CreateRow(parent);
        created.Add(row);
        AddButton(row.transform, "-2°C", font, () =>
            ChangeTarget(building, target - 2f, setStatus, refresh));
        AddButton(row.transform, "+2°C", font, () =>
            ChangeTarget(building, target + 2f, setStatus, refresh));
        return created;
    }

    private static string FormatWaterCondition(
        BuildingWaterConditionSnapshot snapshot)
    {
        string state = snapshot.Recovering
            ? "회복 대기"
            : snapshot.Frozen
                ? "동결"
                : "정상";
        string temperature = snapshot.HasObservedTemperature
            ? $"{snapshot.ObservedTemperatureC:0.#}°C"
            : "온도 관측 불가";
        string text =
            $"배관 {state} · 유량 {snapshot.ThroughputMultiplier * 100f:0}% · {temperature}";
        if (!snapshot.HasSeasonalSource)
        {
            return text;
        }

        text +=
            $" · 동결 ≤{snapshot.FreezeThresholdC:0.#}°C / 회복 ≥{snapshot.RecoveryThresholdC:0.#}°C"
            + $"\n{snapshot.SourceDisplayName}"
            + (snapshot.SourceRemainingDays > 0
                ? $" · 남은 {snapshot.SourceRemainingDays}일"
                : " · 오늘 종료");
        return text;
    }

    private void ChangeTarget(
        BuildableObject building,
        float target,
        Action<string> setStatus,
        Action refresh)
    {
        if (commands.TrySetTargetTemperature(
            building.centerPos,
            target,
            out DomainFailure failure)
            && query.TryGetTargetTemperature(
                building.centerPos,
                out float applied))
        {
            setStatus?.Invoke($"목표 온도를 {applied:0.#}°C로 설정했습니다.");
        }
        else
        {
            setStatus?.Invoke(failureLocalizer.Localize(failure));
        }

        refresh?.Invoke();
    }

    private static GameObject CreateText(
        Transform parent,
        string value,
        TMP_FontAsset font,
        float preferredHeight = 30f,
        string objectName = "EnvironmentalTargetText")
    {
        GameObject root = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = preferredHeight;
        TMP_Text text = root.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = 17f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return root;
    }

    private static GameObject CreateRow(Transform parent)
    {
        GameObject row = new GameObject(
            "EnvironmentalTargetControls",
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout =
            row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childForceExpandWidth = false;
        row.GetComponent<LayoutElement>().preferredHeight = 40f;
        return row;
    }

    private static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        Action action)
    {
        GameObject root = new GameObject(
            "EnvironmentalTargetButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredWidth = 120f;
        Button button = root.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, false);
        button.onClick.AddListener(() => action?.Invoke());

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 14f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }
}
