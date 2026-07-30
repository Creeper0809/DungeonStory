using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface ITreasuryDefenseBuildingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

public sealed class TreasuryDefenseBuildingPanelPresenter :
    ITreasuryDefenseBuildingPanelPresenter
{
    private static readonly int[] ThreatSteps = { 0, 25, 50, 100, 200 };
    private static readonly int[] BudgetSteps = { 0, 150, 300, 600, 1200 };
    private static readonly int[] ProtectedFundSteps = { -1, 0, 500, 1000, 2500 };

    private readonly ITreasuryDefenseRuntime treasuryDefense;
    private readonly IAutoProcurementRuntime procurement;

    public TreasuryDefenseBuildingPanelPresenter(
        ITreasuryDefenseRuntime treasuryDefense,
        IAutoProcurementRuntime procurement)
    {
        this.treasuryDefense = treasuryDefense
            ?? throw new ArgumentNullException(nameof(treasuryDefense));
        this.procurement = procurement
            ?? throw new ArgumentNullException(nameof(procurement));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new();
        if (parent == null
            || building is not DefenseFacility defenseFacility
            || building.BuildingData?.GetAbility<
                BuildingTreasuryPoweredDefenseAbility>() is not
                BuildingTreasuryPoweredDefenseAbility ability)
        {
            return created;
        }

        TreasuryDefensePolicy policy = treasuryDefense.GetPolicy(defenseFacility);
        AddText(
            parent,
            "금고 연동 방어",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);
        AddText(
            parent,
            $"발사당 {Mathf.Max(1, ability.shotCost):N0}골드 · "
            + $"침공 예산 {policy.invasionBudget:N0}골드",
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            30f,
            created);

        GameObject toggleRow = CreateRow(parent, "TreasuryDefenseToggles", 42f);
        created.Add(toggleRow);
        AddButton(
            toggleRow.transform,
            policy.automaticFire ? "자동 발사 켬" : "자동 발사 끔",
            font,
            policy.automaticFire,
            () =>
            {
                policy.automaticFire = !policy.automaticFire;
                Apply(
                    policy,
                    policy.automaticFire
                        ? "자동 발사를 켰습니다."
                        : "자동 발사를 껐습니다.",
                    showFeedback,
                    refresh);
            });
        AddButton(
            toggleRow.transform,
            policy.bossOnly ? "보스 전용" : "모든 침입자",
            font,
            policy.bossOnly,
            () =>
            {
                policy.bossOnly = !policy.bossOnly;
                Apply(
                    policy,
                    policy.bossOnly
                        ? "보스에게만 발사합니다."
                        : "조건을 만족하는 모든 침입자에게 발사합니다.",
                    showFeedback,
                    refresh);
            });

        AddCycleRow(
            parent,
            "최소 위협도",
            policy.minimumThreat.ToString("N0"),
            font,
            () =>
            {
                policy.minimumThreat = Next(
                    policy.minimumThreat,
                    ThreatSteps);
                Apply(
                    policy,
                    $"최소 위협도를 {policy.minimumThreat:N0}(으)로 설정했습니다.",
                    showFeedback,
                    refresh);
            },
            created);
        AddCycleRow(
            parent,
            "침공당 예산",
            $"{policy.invasionBudget:N0}골드",
            font,
            () =>
            {
                policy.invasionBudget = Next(
                    policy.invasionBudget,
                    BudgetSteps);
                Apply(
                    policy,
                    $"침공당 예산을 {policy.invasionBudget:N0}골드로 설정했습니다.",
                    showFeedback,
                    refresh);
            },
            created);
        AddCycleRow(
            parent,
            "보호 자금",
            policy.protectedFunds < 0
                ? $"자동 ({procurement.ProtectedFunds:N0}골드)"
                : $"{policy.protectedFunds:N0}골드",
            font,
            () =>
            {
                policy.protectedFunds = Next(
                    policy.protectedFunds,
                    ProtectedFundSteps);
                Apply(
                    policy,
                    policy.protectedFunds < 0
                        ? "자동 보호 자금을 사용합니다."
                        : $"보호 자금을 {policy.protectedFunds:N0}골드로 설정했습니다.",
                    showFeedback,
                    refresh);
            },
            created);

        string failure = treasuryDefense.GetLastFailureReason(
            policy.facilityPersistentId);
        if (!string.IsNullOrWhiteSpace(failure))
        {
            AddText(
                parent,
                $"발사 중지: {failure}",
                font,
                14f,
                DungeonUiTheme.Warning,
                38f,
                created);
        }

        return created;
    }

    private void Apply(
        TreasuryDefensePolicy policy,
        string feedback,
        Action<string> showFeedback,
        Action refresh)
    {
        treasuryDefense.UpsertPolicy(policy);
        showFeedback?.Invoke(feedback);
        refresh?.Invoke();
    }

    private static void AddCycleRow(
        Transform parent,
        string label,
        string value,
        TMP_FontAsset font,
        Action cycle,
        ICollection<GameObject> created)
    {
        GameObject row = CreateRow(
            parent,
            $"TreasuryDefense{label}",
            42f);
        created.Add(row);
        AddText(
            row.transform,
            $"{label}: {value}",
            font,
            14f,
            DungeonUiTheme.TextPrimary,
            42f,
            created,
            270f);
        AddButton(row.transform, "변경", font, false, cycle);
    }

    private static int Next(int current, IReadOnlyList<int> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == current)
            {
                return values[(i + 1) % values.Count];
            }
        }

        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] > current)
            {
                return values[i];
            }
        }

        return values[0];
    }

    private static GameObject CreateRow(
        Transform parent,
        string name,
        float height)
    {
        GameObject row = new GameObject(
            name,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        row.GetComponent<LayoutElement>().preferredHeight = height;
        return row;
    }

    private static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool selected,
        Action action)
    {
        GameObject buttonObject = new GameObject(
            "TreasuryDefenseButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredWidth = 142f;
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected);
        button.onClick.AddListener(() => action?.Invoke());

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 2f);
        rect.offsetMax = new Vector2(-5f, -2f);
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
    }

    private static void AddText(
        Transform parent,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        float height,
        ICollection<GameObject> created,
        float preferredWidth = -1f)
    {
        GameObject textObject = new GameObject(
            "TreasuryDefenseText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        LayoutElement element = textObject.GetComponent<LayoutElement>();
        element.preferredHeight = height;
        if (preferredWidth > 0f)
        {
            element.preferredWidth = preferredWidth;
        }

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
