using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IAnimalHusbandryBuildingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

public sealed class AnimalHusbandryBuildingPanelPresenter :
    IAnimalHusbandryBuildingPanelPresenter
{
    private readonly IAnimalHusbandryQuery husbandryQuery;
    private readonly IAnimalHusbandryCommand husbandryCommands;
    private readonly IWildlifeSpeciesCatalogProvider species;

    public AnimalHusbandryBuildingPanelPresenter(
        IAnimalHusbandryQuery husbandryQuery,
        IAnimalHusbandryCommand husbandryCommands,
        IWildlifeSpeciesCatalogProvider species)
    {
        this.husbandryQuery = husbandryQuery
            ?? throw new ArgumentNullException(nameof(husbandryQuery));
        this.husbandryCommands = husbandryCommands
            ?? throw new ArgumentNullException(nameof(husbandryCommands));
        this.species = species
            ?? throw new ArgumentNullException(nameof(species));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new List<GameObject>();
        if (parent == null
            || building?.BuildingData.GetBeastPenAbility() == null)
        {
            return created;
        }

        BuildingInstanceId penId = GetPenId(building);
        AnimalPenPolicyData policy = husbandryQuery.GetPenPolicy(penId);
        int effectiveCapacity = husbandryQuery.GetEffectivePenCapacity(penId);
        HusbandryAnimalState[] animals = husbandryQuery.Animals
            .Where(state => state.PenId.Equals(penId))
            .ToArray();
        AnimalPenCompatibilityResult compatibility =
            husbandryQuery.EvaluatePen(penId);

        AddText(
            parent,
            "축산 관리",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);
        AddText(
            parent,
            $"가축 {animals.Length}/{effectiveCapacity}마리"
            + $" · 합사 위험 {compatibility.Risk:P0}",
            font,
            15f,
            compatibility.HasDanger
                ? DungeonUiTheme.Warning
                : DungeonUiTheme.TextSecondary,
            30f,
            created);

        foreach (AnimalPenCompatibilityIssue issue in compatibility.Issues.Take(4))
        {
            AddText(
                parent,
                $"주의 · {issue.Kind}"
                + (issue.Parameters.Count > 0
                    ? $" ({string.Join(", ", issue.Parameters)})"
                    : string.Empty),
                font,
                14f,
                DungeonUiTheme.Warning,
                28f,
                created);
        }

        GameObject policyRow = CreateRow(parent, "PenPolicyMain", 42f);
        created.Add(policyRow);
        AddButton(
            policyRow.transform,
            policy.breedingAllowed ? "번식 허용" : "번식 중지",
            font,
            policy.breedingAllowed,
            () => Update(policy, value => value.breedingAllowed = !value.breedingAllowed));
        AddButton(
            policyRow.transform,
            policy.allowRiskyMixing ? "위험 합사 허용" : "위험 합사 경고",
            font,
            policy.allowRiskyMixing,
            () => Update(policy, value => value.allowRiskyMixing = !value.allowRiskyMixing));
        AddButton(
            policyRow.transform,
            "최대 -",
            font,
            false,
            () => Update(
                policy,
                value => value.maximumAnimals = Mathf.Max(1, value.maximumAnimals - 1)));
        AddButton(
            policyRow.transform,
            "최대 +",
            font,
            false,
            () => Update(
                policy,
                value => value.maximumAnimals = Mathf.Min(
                    building.BuildingData.GetBeastPenAbility().capacity,
                    value.maximumAnimals + 1)));

        GameObject limitsRow = CreateRow(parent, "PenPolicyLimits", 42f);
        created.Add(limitsRow);
        AddButton(
            limitsRow.transform,
            $"암컷 {policy.adultFemaleLimit}",
            font,
            false,
            () => Update(policy, value =>
                value.adultFemaleLimit = NextLimit(value.adultFemaleLimit)));
        AddButton(
            limitsRow.transform,
            $"수컷 {policy.adultMaleLimit}",
            font,
            false,
            () => Update(policy, value =>
                value.adultMaleLimit = NextLimit(value.adultMaleLimit)));
        AddButton(
            limitsRow.transform,
            $"새끼 {policy.juvenileLimit}",
            font,
            false,
            () => Update(policy, value =>
                value.juvenileLimit = NextLimit(value.juvenileLimit)));
        AddButton(
            limitsRow.transform,
            policy.protectPregnant ? "임신 보호" : "임신 보호 해제",
            font,
            policy.protectPregnant,
            () => Update(policy, value =>
                value.protectPregnant = !value.protectPregnant));

        foreach (HusbandryAnimalState animal in animals
                     .OrderBy(state => state.SpeciesId.Value, StringComparer.Ordinal)
                     .ThenBy(state => state.AnimalId.Value, StringComparer.Ordinal))
        {
            string speciesName = species.TryGetSpecies(
                animal.SpeciesId.Value,
                out WildlifeSpeciesDefinition definition)
                    ? definition.DisplayName
                    : animal.SpeciesId.Value;
            string sex = animal.Sex == AnimalSex.Female ? "암" : "수";
            string status = animal.Tamed
                ? animal.Pregnant
                    ? "번식 중"
                    : "길들임"
                : $"길들이기 {animal.TamingProgress:P0}";
            AddText(
                parent,
                $"{speciesName} · {sex} · {animal.AgeDays:0.0}일 · {status}"
                + (animal.SlaughterDesignated ? " · 도축 지정" : string.Empty)
                + $"\n{animal.StatusCode}",
                font,
                14f,
                animal.SlaughterDesignated
                    ? DungeonUiTheme.Danger
                    : DungeonUiTheme.TextPrimary,
                48f,
                created);
        }

        return created;

        void Update(
            AnimalPenPolicyData source,
            Action<AnimalPenPolicyData> mutate)
        {
            AnimalPenPolicyData updated = source.Clone();
            mutate(updated);
            if (!husbandryCommands.SetPenPolicy(
                    updated,
                    out AnimalHusbandryFailure failure))
            {
                showFeedback?.Invoke(failure.Code.ToString());
                return;
            }

            showFeedback?.Invoke("축산 정책을 변경했습니다.");
            refresh?.Invoke();
        }
    }

    private static int NextLimit(int current)
    {
        return current >= 12 ? 0 : current + 1;
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
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
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
            label,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected);
        button.onClick.AddListener(() => action?.Invoke());
        buttonObject.GetComponent<LayoutElement>().minWidth = 96f;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 13f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 13f;
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
        ICollection<GameObject> created)
    {
        GameObject textObject = new GameObject(
            "HusbandryText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, fontSize - 4f);
        text.fontSizeMax = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        created.Add(textObject);
    }

    private static BuildingInstanceId GetPenId(BuildableObject building)
    {
        return building.RequirePersistentInstanceId();
    }
}
