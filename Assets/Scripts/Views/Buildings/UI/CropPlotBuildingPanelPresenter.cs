using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface ICropPlotBuildingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

public sealed class CropPlotBuildingPanelPresenter :
    ICropPlotBuildingPanelPresenter
{
    private readonly ICropPlotRuntime cropPlots;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IItemDefinitionCatalog itemDefinitions;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly Dictionary<string, string> feedbackByPlot =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public CropPlotBuildingPanelPresenter(
        ICropPlotRuntime cropPlots,
        IResourceEconomyContentCatalog catalog,
        IItemDefinitionCatalog itemDefinitions,
        ICharacterWorldQuery characterWorld)
    {
        this.cropPlots = cropPlots ?? throw new ArgumentNullException(nameof(cropPlots));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemDefinitions = itemDefinitions
            ?? throw new ArgumentNullException(nameof(itemDefinitions));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
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
            || building?.BuildingData?.GetAbility<BuildingCropPlotAbility>() == null)
        {
            return created;
        }

        CropPlotSnapshot plot = cropPlots.Plots.FirstOrDefault(candidate =>
            candidate.BuildingId == building.id
            && candidate.Position == building.centerPos);
        if (plot == null)
        {
            return created;
        }

        AddText(
            parent,
            plot.Indoor ? "실내 재배" : "야외 경작",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);
        AddProgress(parent, plot, font, created);
        AddText(
            parent,
            FormatEcology(plot),
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            42f,
            created);
        AddText(
            parent,
            FormatMaterials(plot),
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            42f,
            created);
        if (!string.IsNullOrWhiteSpace(plot.BlockedReason))
        {
            AddText(
                parent,
                plot.BlockedReason,
                font,
                14f,
                DungeonUiTheme.Warning,
                34f,
                created);
        }

        if (feedbackByPlot.TryGetValue(plot.PlotId, out string feedback)
            && !string.IsNullOrWhiteSpace(feedback))
        {
            AddText(
                parent,
                feedback,
                font,
                14f,
                DungeonUiTheme.Warning,
                32f,
                created);
        }

        RenderGoldenHarvestControls(
            parent,
            building,
            plot,
            font,
            showFeedback,
            refresh,
            created);
        RenderTreatmentControls(
            parent,
            building,
            plot,
            font,
            showFeedback,
            refresh,
            created);

        AddText(
            parent,
            "재배 작물",
            font,
            18f,
            DungeonUiTheme.TextPrimary,
            30f,
            created);
        foreach (CropDefinitionSO crop in catalog.Crops
                     .Where(crop => crop != null
                         && (!plot.Indoor || crop.IndoorAllowed))
                     .OrderBy(crop => crop.DisplayName, StringComparer.Ordinal))
        {
            GameObject row = CreateRow(
                parent,
                $"CropChoice_{crop.CropId}",
                54f);
            created.Add(row);
            AddCropText(row.transform, crop, font);
            bool selected = string.Equals(
                crop.CropId,
                plot.CropId,
                StringComparison.Ordinal);
            AddButton(
                row.transform,
                selected ? "재배 중" : "선택",
                font,
                selected,
                !selected,
                () =>
                {
                    bool succeeded = cropPlots.TrySetCrop(
                        building,
                        crop.CropId,
                        out string message);
                    feedbackByPlot[plot.PlotId] = message;
                    showFeedback?.Invoke(message);
                    if (succeeded)
                    {
                        refresh?.Invoke();
                    }
                });
        }

        return created;
    }

    private void RenderTreatmentControls(
        Transform parent,
        BuildableObject building,
        CropPlotSnapshot plot,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        AddText(
            parent,
            "작물 처리",
            font,
            18f,
            DungeonUiTheme.TextPrimary,
            30f,
            created);
        if (plot.TreatmentScheduled)
        {
            string progress = plot.TreatmentRequiredWork <= 0f
                ? string.Empty
                : $" · 작업 {plot.TreatmentCompletedWork:0.#}/"
                    + $"{plot.TreatmentRequiredWork:0.#} WU";
            string delivery = $" · 배송 {plot.TreatmentDeliveredQuantity}/"
                + plot.TreatmentRequiredQuantity;
            AddText(
                parent,
                $"{plot.TreatmentItemName} · {FormatTreatmentPhase(plot.TreatmentPhase)}"
                    + delivery + progress,
                font,
                14f,
                DungeonUiTheme.TextSecondary,
                38f,
                created);
            if (!string.IsNullOrWhiteSpace(plot.TreatmentFailureReason))
            {
                AddText(
                    parent,
                    plot.TreatmentFailureReason,
                    font,
                    13f,
                    DungeonUiTheme.Warning,
                    30f,
                    created);
            }
            bool cancellable = plot.TreatmentPhase is
                CropTreatmentOrderPhase.WaitingForDelivery
                or CropTreatmentOrderPhase.ReadyForWork
                or CropTreatmentOrderPhase.Working;
            GameObject cancelRow = CreateRow(
                parent,
                "CropTreatmentCancel",
                34f);
            created.Add(cancelRow);
            AddButton(
                cancelRow.transform,
                "처리 취소",
                font,
                selected: false,
                interactable: cancellable,
                () =>
                {
                    bool succeeded = cropPlots.TryCancelTreatment(
                        building,
                        out string message);
                    feedbackByPlot[plot.PlotId] = message;
                    showFeedback?.Invoke(message);
                    if (succeeded)
                        refresh?.Invoke();
                });
            return;
        }

        foreach (ResourceItemDefinitionSO item in catalog.Items
                     .Where(value => value != null
                         && value.TryGetCropTreatment(
                             out CropTreatmentPolicy _))
                     .OrderBy(value => value.ItemId, StringComparer.Ordinal))
        {
            ResourceItemDefinitionSO captured = item;
            captured.TryGetCropTreatment(out CropTreatmentPolicy policy);
            bool available = cropPlots.CanScheduleTreatment(
                building,
                captured.ItemId,
                out string unavailable);
            int nextAllowed = policy.Kind switch
            {
                CropTreatmentKind.PestLure => plot.PestLureNextAllowedDay,
                CropTreatmentKind.BotanicalPesticide =>
                    plot.BotanicalPesticideNextAllowedDay,
                CropTreatmentKind.Fungicide => plot.FungicideNextAllowedDay,
                _ => int.MaxValue
            };
            string cooldown = nextAllowed > plot.CurrentAbsoluteDay
                ? $" · 재사용 {nextAllowed - plot.CurrentAbsoluteDay}일"
                : string.Empty;
            GameObject row = CreateRow(
                parent,
                $"CropTreatment_{captured.ItemId}",
                54f);
            created.Add(row);
            AddText(
                row.transform,
                $"{captured.DisplayName} · {policy.RequiredWork:0.#} WU"
                    + $" · 압력 -{policy.EffectAmount:0.#}{cooldown}"
                    + (available || string.IsNullOrWhiteSpace(unavailable)
                        ? string.Empty
                        : $"\n{unavailable}"),
                font,
                13f,
                available
                    ? DungeonUiTheme.TextPrimary
                    : DungeonUiTheme.TextSecondary,
                50f,
                created);
            AddButton(
                row.transform,
                "처리 예약",
                font,
                selected: false,
                interactable: available,
                () =>
                {
                    bool succeeded = cropPlots.TryScheduleTreatment(
                        building,
                        captured.ItemId,
                        out string message);
                    feedbackByPlot[plot.PlotId] = message;
                    showFeedback?.Invoke(message);
                    if (succeeded)
                        refresh?.Invoke();
                });
        }
    }

    private static string FormatTreatmentPhase(CropTreatmentOrderPhase phase) =>
        phase switch
        {
            CropTreatmentOrderPhase.WaitingForDelivery => "운반 대기",
            CropTreatmentOrderPhase.ReadyForWork => "작업 대기",
            CropTreatmentOrderPhase.Working => "작업 중",
            CropTreatmentOrderPhase.InputCommitted => "물리 소비 확정",
            CropTreatmentOrderPhase.OutcomePublished => "결과 승인 대기",
            CropTreatmentOrderPhase.PlotDestroyedLossPending => "파괴 손실 처리",
            _ => "없음"
        };

    private void RenderGoldenHarvestControls(
        Transform parent,
        BuildableObject building,
        CropPlotSnapshot plot,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        if (plot.Phase is not (CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting))
            return;

        CharacterActor[] candidates = characterWorld.Characters
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.Progression.ResolveSelectedTraits()
                    .Any(trait => trait != null && trait.id == 304))
            .OrderBy(
                actor => actor.Identity?.PersistentId ?? string.Empty,
                StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
            return;

        AddText(
            parent,
            string.IsNullOrWhiteSpace(plot.GoldenHarvestHarvesterId)
                ? "황금 수확 · 24시간 숙성 후 위험 수확"
                : $"황금 수확 예약 · {plot.GoldenHarvestHarvesterId}",
            font,
            16f,
            DungeonUiTheme.TextPrimary,
            30f,
            created);
        if (!string.IsNullOrWhiteSpace(plot.GoldenHarvestHarvesterId))
            return;

        foreach (CharacterActor candidate in candidates)
        {
            CharacterActor captured = candidate;
            GameObject row = CreateRow(
                parent,
                $"GoldenHarvest_{captured.Identity.PersistentId}",
                34f);
            created.Add(row);
            AddButton(
                row.transform,
                $"{captured.Identity.DisplayName} 지정",
                font,
                selected: false,
                interactable: true,
                () =>
                {
                    bool succeeded = cropPlots.TryScheduleGoldenHarvest(
                        building,
                        captured,
                        out string message);
                    feedbackByPlot[plot.PlotId] = message;
                    showFeedback?.Invoke(message);
                    if (succeeded)
                        refresh?.Invoke();
                });
        }
    }

    private string FormatMaterials(CropPlotSnapshot plot)
    {
        if (plot.RequiredMaterials.Count == 0)
        {
            return "파종 재료 없음";
        }

        return "파종 재료 · " + string.Join(
            "  /  ",
            plot.RequiredMaterials.Select(requirement =>
            {
                plot.DeliveredMaterials.TryGetValue(
                    requirement.Key,
                    out int delivered);
                string name = catalog.TryGetItem(
                    requirement.Key,
                    out ResourceItemDefinitionSO item)
                        ? item.DisplayName
                        : FormatAuthoredItem(requirement.Key);
                return $"{name} {delivered}/{requirement.Value}";
            }));
    }

    private static string FormatEcology(CropPlotSnapshot plot)
    {
        string cultivar = string.IsNullOrWhiteSpace(plot.CultivarGenomeId)
            ? "종자 대기"
            : plot.CultivarGenomeId;
        string disease = plot.CropDisease == CropDiseaseKind.None
            ? "병해 없음"
            : plot.CropDisease.ToString();
        return $"품종 {cultivar}  /  비옥도 {plot.Fertility:0}  /  "
            + $"해충 {plot.PestPressure:0}  /  병압 {plot.DiseasePressure:0} ({disease})";
    }

    private string FormatAuthoredItem(string itemId)
    {
        if (itemDefinitions.TryGet(
                (ItemDefinitionId)itemId,
                out ItemDefinitionSO definition))
        {
            return definition.StockCategory switch
            {
                StockCategory.Water => "물",
                StockCategory.Fuel => "연료",
                _ => definition.StockCategory.ToString()
            };
        }

        return itemId;
    }

    private static void AddProgress(
        Transform parent,
        CropPlotSnapshot plot,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        float progress = plot.Phase switch
        {
            CropPlotPhase.Sowing => plot.SowProgress,
            CropPlotPhase.Growing => plot.GrowthProgress,
            CropPlotPhase.ReadyToHarvest => 1f,
            CropPlotPhase.Harvesting => plot.HarvestProgress,
            _ => 0f
        };
        GameObject root = new GameObject(
            "CropProgress",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 56f;
        root.GetComponent<Image>().color = DungeonUiTheme.Panel;

        GameObject fillObject = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(Image));
        fillObject.transform.SetParent(root.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = DungeonUiTheme.Accent;
        fill.raycastTarget = false;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 3f);
        labelRect.offsetMax = new Vector2(-8f, -3f);
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = $"{plot.CropName} · {FormatPhase(plot.Phase)}"
            + (progress > 0f ? $" · {progress:P0}" : string.Empty);
        label.font = font;
        label.fontSize = 15f;
        label.color = DungeonUiTheme.TextPrimary;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        created.Add(root);
    }

    private static string FormatPhase(CropPlotPhase phase)
    {
        return phase switch
        {
            CropPlotPhase.Empty => "재배 준비",
            CropPlotPhase.WaitingForMaterials => "재료 운반 대기",
            CropPlotPhase.ReadyToSow => "파종 대기",
            CropPlotPhase.Sowing => "파종 중",
            CropPlotPhase.Growing => "성장 중",
            CropPlotPhase.ReadyToHarvest => "수확 가능",
            CropPlotPhase.Harvesting => "수확 중",
            CropPlotPhase.Blocked => "중단됨",
            _ => "상태 확인 중"
        };
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

    private static void AddCropText(
        Transform parent,
        CropDefinitionSO crop,
        TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(
            "CropLabel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredWidth = 330f;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = $"{crop.DisplayName}\n"
            + $"성장 {crop.GrowthHours:0.#}시간 · 수확 {crop.Yield}";
        text.font = font;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    private static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool selected,
        bool interactable,
        Action action)
    {
        GameObject buttonObject = new GameObject(
            "CropButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredWidth = 118f;
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected);
        button.interactable = interactable;
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
        ICollection<GameObject> created)
    {
        GameObject textObject = new GameObject(
            "CropText",
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
