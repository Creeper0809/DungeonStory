using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IPaidFacilityBuildingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

public sealed class PaidFacilityBuildingPanelPresenter :
    IPaidFacilityBuildingPanelPresenter
{
    private readonly IPaidFacilityContractRuntime contracts;

    public PaidFacilityBuildingPanelPresenter(
        IPaidFacilityContractRuntime contracts)
    {
        this.contracts = contracts
            ?? throw new ArgumentNullException(nameof(contracts));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new();
        BuildingPaidFacilityServiceAbility ability = building?
            .BuildingData?
            .GetAbility<BuildingPaidFacilityServiceAbility>();
        if (parent == null || ability == null || ability.cost <= 0)
        {
            return created;
        }

        AddText(
            parent,
            "유료 운영",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);
        AddText(
            parent,
            FormatChargeDescription(ability),
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            34f,
            created);

        if (ability.chargeMode == PaidFacilityChargeMode.DailyContract)
        {
            contracts.SynchronizeFacility(building);
            PaidFacilityContractState contract = contracts.GetContract(building);
            bool active = contract?.active == true;
            AddText(
                parent,
                active
                    ? "계약 상태: 운영 중"
                    : "계약 상태: 중단",
                font,
                14f,
                active ? DungeonUiTheme.Accent : DungeonUiTheme.Warning,
                30f,
                created);

            GameObject button = CreateButton(
                parent,
                active ? "계약 중지" : "당일 비용 지불 후 재개",
                font,
                active,
                () =>
                {
                    if (!contracts.TrySetDailyContractActive(
                            building,
                            !active,
                            out string failureReason))
                    {
                        showFeedback?.Invoke(failureReason);
                        refresh?.Invoke();
                        return;
                    }

                    showFeedback?.Invoke(
                        active
                            ? "시설의 일일 계약을 중지했습니다."
                            : "당일 계약비를 지불하고 시설 운영을 재개했습니다.");
                    refresh?.Invoke();
                });
            created.Add(button);
        }

        string failure = contracts.GetLastFailureReason(building);
        if (!string.IsNullOrWhiteSpace(failure))
        {
            AddText(
                parent,
                $"운영 중지: {failure}",
                font,
                14f,
                DungeonUiTheme.Warning,
                42f,
                created);
        }

        return created;
    }

    private static string FormatChargeDescription(
        BuildingPaidFacilityServiceAbility ability)
    {
        string displayName = string.IsNullOrWhiteSpace(ability.displayName)
            ? "유료 시설 서비스"
            : ability.displayName.Trim();
        string period = ability.chargeMode switch
        {
            PaidFacilityChargeMode.PerUse => "이용할 때마다",
            PaidFacilityChargeMode.PerOrder => "작업 주문마다",
            PaidFacilityChargeMode.DailyContract => "영업일마다",
            _ => "운영 시"
        };
        return $"{displayName} · {period} {ability.cost:N0}골드";
    }

    private static GameObject CreateButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool warning,
        Action action)
    {
        GameObject buttonObject = new GameObject(
            "PaidFacilityContractButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 42f;
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, false);
        if (warning)
        {
            button.targetGraphic.color = DungeonUiTheme.Warning;
        }

        button.onClick.AddListener(() => action?.Invoke());

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 3f);
        rect.offsetMax = new Vector2(-8f, -3f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return buttonObject;
    }

    private static void AddText(
        Transform parent,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        float height,
        ICollection<GameObject> created)
    {
        GameObject textObject = new GameObject(
            "PaidFacilityText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        created.Add(textObject);
    }
}
