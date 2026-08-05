using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class RoomOverlayPresenter : IDisposable
{
    private const string SortingLayerName = "RoomOverlay";
    private const string OutlineSortingLayerName = "RoomOutline";
    private const float FillAlpha = 0.28f;
    private const float InvalidFillAlpha = 0.16f;
    private const float BorderAlpha = 0.92f;
    private const float BorderThickness = 0.075f;
    private const float BorderInset = BorderThickness * 0.5f;
    private const float CeilingOutlineClearance = 0.5f;

    private readonly GameObject root;
    private readonly Sprite sprite;
    private readonly Material material;
    private readonly List<SpriteRenderer> fillRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> borderRenderers = new List<SpriteRenderer>();

    public RoomOverlayPresenter()
    {
        root = new GameObject("RoomInspectionWorldOverlay");
        sprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            1f);
        sprite.name = "RoomInspectionPixel";
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
            ?? Shader.Find("Sprites/Default");
        material = new Material(shader)
        {
            name = "RoomInspectionUnlitMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        root.SetActive(false);
    }

    public int ActiveCellCount { get; private set; }

    public void Show(RoomEnvironmentSnapshot snapshot, Color baseColor)
    {
        if (snapshot?.Room == null || snapshot.Grid == null)
        {
            Clear();
            return;
        }

        root.SetActive(true);
        ActiveCellCount = snapshot.Room.Cells.Count;
        EnsurePool(fillRenderers, ActiveCellCount, "Fill", sortingOrder: 0);

        bool valid = snapshot.Status == RoomEnvironmentStatus.Usable;
        Color fillColor = new Color(
            baseColor.r,
            baseColor.g,
            baseColor.b,
            valid ? FillAlpha : InvalidFillAlpha);
        Color outlineColor = Color.Lerp(
            valid ? DungeonUiTheme.Accent : baseColor,
            Color.white,
            valid ? 0.25f : 0.12f);
        HashSet<Vector2Int> cells = new HashSet<Vector2Int>(snapshot.Room.Cells);
        int fillIndex = 0;
        int borderIndex = 0;
        foreach (Vector2Int cell in snapshot.Room.Cells)
        {
            GetCellGeometry(snapshot.Grid, cell, out Vector3 bottomCenter, out float width, out float height);
            SpriteRenderer fill = fillRenderers[fillIndex++];
            SetQuad(fill, bottomCenter + Vector3.up * (height * 0.5f), width, height, fillColor);

            bool negativeXEdge = !cells.Contains(cell + Vector2Int.left);
            bool positiveXEdge = !cells.Contains(cell + Vector2Int.right);
            bool bottomEdge = !cells.Contains(cell + Vector2Int.down);
            bool topEdge = !cells.Contains(cell + Vector2Int.up);
            float inset = Mathf.Min(BorderInset, Mathf.Min(width, height) * 0.2f);
            // Grid X increases toward world-left, so grid and world edge directions are mirrored.
            float worldLeftInset = positiveXEdge ? inset : 0f;
            float worldRightInset = negativeXEdge ? inset : 0f;
            float verticalBottomInset = bottomEdge ? inset : 0f;
            float ceilingClearance = topEdge
                ? Mathf.Min(CeilingOutlineClearance, height * 0.25f)
                : 0f;
            float verticalTopInset = topEdge ? ceilingClearance + inset : 0f;

            if (negativeXEdge)
            {
                SetBorder(
                    ref borderIndex,
                    bottomCenter + new Vector3(
                        (width * 0.5f) - inset,
                        (height * 0.5f) + ((verticalBottomInset - verticalTopInset) * 0.5f)),
                    BorderThickness,
                    Mathf.Max(BorderThickness, height - verticalBottomInset - verticalTopInset),
                    outlineColor);
            }
            if (positiveXEdge)
            {
                SetBorder(
                    ref borderIndex,
                    bottomCenter + new Vector3(
                        (-width * 0.5f) + inset,
                        (height * 0.5f) + ((verticalBottomInset - verticalTopInset) * 0.5f)),
                    BorderThickness,
                    Mathf.Max(BorderThickness, height - verticalBottomInset - verticalTopInset),
                    outlineColor);
            }
            if (bottomEdge)
            {
                SetBorder(
                    ref borderIndex,
                    bottomCenter + new Vector3(
                        (worldLeftInset - worldRightInset) * 0.5f,
                        inset),
                    Mathf.Max(BorderThickness, width - worldLeftInset - worldRightInset),
                    BorderThickness,
                    outlineColor);
            }
            if (topEdge)
            {
                SetBorder(
                    ref borderIndex,
                    bottomCenter + new Vector3(
                        (worldLeftInset - worldRightInset) * 0.5f,
                        height - ceilingClearance - inset),
                    Mathf.Max(BorderThickness, width - worldLeftInset - worldRightInset),
                    BorderThickness,
                    outlineColor);
            }
        }

        SetActiveCount(fillRenderers, fillIndex);
        SetActiveCount(borderRenderers, borderIndex);
    }

    public void Clear()
    {
        ActiveCellCount = 0;
        root.SetActive(false);
    }

    public void Dispose()
    {
        if (sprite != null)
        {
            UnityEngine.Object.Destroy(sprite);
        }
        if (material != null)
        {
            UnityEngine.Object.Destroy(material);
        }
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }
    }

    private void SetBorder(
        ref int borderIndex,
        Vector3 position,
        float width,
        float height,
        Color baseColor)
    {
        EnsurePool(borderRenderers, borderIndex + 1, "Border", sortingOrder: 1);
        Color color = new Color(baseColor.r, baseColor.g, baseColor.b, BorderAlpha);
        SpriteRenderer border = borderRenderers[borderIndex++];
        border.sortingLayerName = OutlineSortingLayerName;
        border.sortingOrder = 1;
        SetQuad(border, position, width, height, color);
    }

    private void EnsurePool(
        List<SpriteRenderer> pool,
        int required,
        string prefix,
        int sortingOrder)
    {
        while (pool.Count < required)
        {
            GameObject item = new GameObject($"{prefix}_{pool.Count}");
            item.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = SortingLayerName;
            renderer.sortingOrder = sortingOrder;
            pool.Add(renderer);
        }
    }

    private static void SetActiveCount(List<SpriteRenderer> pool, int activeCount)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            pool[i].gameObject.SetActive(i < activeCount);
        }
    }

    private static void SetQuad(
        SpriteRenderer renderer,
        Vector3 position,
        float width,
        float height,
        Color color)
    {
        renderer.transform.position = position;
        renderer.transform.localScale = new Vector3(width, height, 1f);
        renderer.color = color;
        renderer.gameObject.SetActive(true);
    }

    private static void GetCellGeometry(
        Grid grid,
        Vector2Int cell,
        out Vector3 bottomCenter,
        out float width,
        out float height)
    {
        bottomCenter = grid.GetWorldPos(cell);
        width = Mathf.Abs(
            grid.GetWorldPos(new Vector2(cell.x + 1f, cell.y)).x
            - bottomCenter.x);
        height = Mathf.Abs(
            grid.GetWorldPos(new Vector2(cell.x, cell.y + 1f)).y
            - bottomCenter.y);
        width = Mathf.Max(0.01f, width);
        height = Mathf.Max(0.01f, height);
    }
}

internal sealed class RoomInspectionView : IDisposable
{
    private sealed class MetricRow
    {
        public TMP_Text Value;
        public Image Fill;
    }

    private readonly RectTransform upperRightPanel;
    private readonly Vector2 originalPanelSize;
    private readonly Vector2 originalPanelPosition;
    private readonly Action toggleAction;
    private readonly TMP_Text titleText;
    private readonly TMP_Text statusText;
    private readonly TMP_Text structureText;
    private readonly TMP_Text contributorsText;
    private readonly Image roleSwatch;
    private readonly MetricRow spaciousnessRow;
    private readonly MetricRow beautyRow;
    private readonly MetricRow cleanlinessRow;
    private readonly MetricRow impressivenessRow;
    private readonly MetricRow shelterRow;
    private readonly MetricRow temperatureRow;
    private readonly MetricRow ventilationRow;
    private readonly MetricRow lightingRow;

    public RoomInspectionView(
        Canvas canvas,
        RectTransform upperRightPanel,
        ITmpKoreanFontService fontService,
        Action toggleAction)
    {
        this.upperRightPanel = upperRightPanel;
        this.toggleAction = toggleAction ?? throw new ArgumentNullException(nameof(toggleAction));
        originalPanelSize = upperRightPanel.sizeDelta;
        originalPanelPosition = upperRightPanel.anchoredPosition;
        upperRightPanel.sizeDelta += new Vector2(130f, 0f);
        upperRightPanel.anchoredPosition += new Vector2(-65f, 0f);

        ToggleButton = CreateToggleButton(upperRightPanel, fontService);
        ToggleButton.transform.SetSiblingIndex(0);
        ToggleButton.onClick.AddListener(OnToggleClicked);

        PanelObject = CreateUiObject("RoomInspectionPanel", canvas.transform);
        RectTransform panelRect = PanelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.one;
        panelRect.anchorMax = Vector2.one;
        panelRect.pivot = Vector2.one;
        panelRect.anchoredPosition = new Vector2(-16f, -140f);
        panelRect.sizeDelta = new Vector2(390f, 600f);
        Image panelImage = PanelObject.AddComponent<Image>();
        panelImage.color = DungeonUiTheme.Panel;
        panelImage.raycastTarget = true;
        Outline outline = PanelObject.AddComponent<Outline>();
        outline.effectColor = DungeonUiTheme.Border;
        outline.effectDistance = new Vector2(1f, -1f);

        roleSwatch = CreateImage(panelRect, "RoleSwatch", new Vector2(16f, -18f), new Vector2(18f, 18f));
        titleText = CreateText(panelRect, "RoomTitle", fontService, 24f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetTopRect(titleText.rectTransform, 44f, 12f, 16f, 34f);
        statusText = CreateText(panelRect, "RoomStatus", fontService, 16f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetTopRect(statusText.rectTransform, 16f, 50f, 16f, 24f);
        structureText = CreateText(panelRect, "RoomStructure", fontService, 15f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        SetTopRect(structureText.rectTransform, 16f, 80f, 16f, 48f);

        spaciousnessRow = CreateMetricRow(panelRect, fontService, "넓이", 138f);
        beautyRow = CreateMetricRow(panelRect, fontService, "미관", 190f);
        cleanlinessRow = CreateMetricRow(panelRect, fontService, "청결", 242f);
        impressivenessRow = CreateMetricRow(panelRect, fontService, "인상도", 294f);
        shelterRow = CreateMetricRow(panelRect, fontService, "쉼터", 342f);
        temperatureRow = CreateMetricRow(panelRect, fontService, "온도", 388f);
        ventilationRow = CreateMetricRow(panelRect, fontService, "환기", 434f);
        lightingRow = CreateMetricRow(panelRect, fontService, "조명", 480f);

        TMP_Text contributorHeader = CreateText(panelRect, "ContributorHeader", fontService, 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        contributorHeader.text = "성향을 만든 시설";
        SetTopRect(contributorHeader.rectTransform, 16f, 528f, 16f, 22f);
        contributorsText = CreateText(panelRect, "RoomContributors", fontService, 14f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        contributorsText.textWrappingMode = TextWrappingModes.Normal;
        contributorsText.overflowMode = TextOverflowModes.Truncate;
        SetTopRect(contributorsText.rectTransform, 16f, 550f, 16f, 38f);
        PanelObject.SetActive(false);
    }

    public Button ToggleButton { get; }
    public GameObject PanelObject { get; }

    public void SetToggleState(bool selected)
    {
        RoomInspectionToggleVisualState state = ToggleButton.GetComponent<RoomInspectionToggleVisualState>();
        if (state != null)
        {
            state.IsSelected = selected;
        }
        DungeonUiTheme.StyleButton(ToggleButton, selected);
    }

    public void SetToggleInteractable(bool interactable)
    {
        ToggleButton.interactable = interactable;
    }

    public void Render(RoomEnvironmentSnapshot snapshot, Color roomColor)
    {
        if (snapshot == null)
        {
            Clear();
            return;
        }

        PanelObject.SetActive(true);
        roleSwatch.color = roomColor;
        titleText.text = RoomEnvironmentPresentation.GetRoomName(snapshot);
        statusText.text = RoomEnvironmentPresentation.GetStatusLabel(snapshot.Status);
        statusText.color = snapshot.Status switch
        {
            RoomEnvironmentStatus.OpenBoundary => DungeonUiTheme.Danger,
            RoomEnvironmentStatus.MissingDoor => DungeonUiTheme.Warning,
            _ => DungeonUiTheme.Good
        };
        structureText.text = $"면적 {snapshot.Area}칸 · 빈 칸 {snapshot.FreeCells} · 문 {snapshot.DoorCount} · 벽 {snapshot.WallCount}\n"
            + $"시설 {snapshot.Fixtures.Count} · 파손 {snapshot.DamagedFixtures}";
        RenderMetric(spaciousnessRow, snapshot.Spaciousness);
        RenderMetric(beautyRow, snapshot.Beauty);
        RenderMetric(cleanlinessRow, snapshot.Cleanliness);
        RenderMetric(impressivenessRow, snapshot.Impressiveness);
        RenderMetric(shelterRow, snapshot.Shelter);
        RenderTemperatureMetric(
            temperatureRow,
            snapshot.Temperature,
            snapshot.TemperatureC);
        RenderMetric(ventilationRow, snapshot.Ventilation);
        RenderMetric(lightingRow, snapshot.Lighting);
        contributorsText.text = BuildContributorText(snapshot);
    }

    public void Clear()
    {
        PanelObject.SetActive(false);
    }

    public void Dispose()
    {
        if (ToggleButton != null)
        {
            ToggleButton.onClick.RemoveListener(OnToggleClicked);
            UnityEngine.Object.Destroy(ToggleButton.gameObject);
        }
        if (upperRightPanel != null)
        {
            upperRightPanel.sizeDelta = originalPanelSize;
            upperRightPanel.anchoredPosition = originalPanelPosition;
        }
        if (PanelObject != null)
        {
            UnityEngine.Object.Destroy(PanelObject);
        }
    }

    private void OnToggleClicked()
    {
        toggleAction();
    }

    private static Button CreateToggleButton(RectTransform parent, ITmpKoreanFontService fontService)
    {
        GameObject buttonObject = CreateUiObject("RoomInspectionToggle", parent);
        Image image = buttonObject.AddComponent<Image>();
        Button button = buttonObject.AddComponent<Button>();
        buttonObject.AddComponent<RoomInspectionToggleVisualState>();
        button.targetGraphic = image;
        TMP_Text label = CreateText(
            buttonObject.GetComponent<RectTransform>(),
            "Label",
            fontService,
            22f,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        label.text = "방";
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        DungeonUiTheme.StyleButton(button);
        return button;
    }

    private static MetricRow CreateMetricRow(
        RectTransform parent,
        ITmpKoreanFontService fontService,
        string label,
        float top)
    {
        TMP_Text labelText = CreateText(parent, label + "Label", fontService, 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        labelText.text = label;
        SetTopRect(labelText.rectTransform, 16f, top, 220f, 20f);
        TMP_Text valueText = CreateText(parent, label + "Value", fontService, 14f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
        SetTopRect(valueText.rectTransform, 220f, top, 16f, 20f);

        Image background = CreateImage(parent, label + "Background", new Vector2(16f, -(top + 26f)), new Vector2(358f, 12f));
        background.color = DungeonUiTheme.SurfaceMuted;
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 1f);
        backgroundRect.anchorMax = new Vector2(0f, 1f);
        backgroundRect.pivot = new Vector2(0f, 1f);

        Image fill = CreateImage(backgroundRect, label + "Fill", Vector2.zero, Vector2.zero);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.fillAmount = 0f;

        return new MetricRow
        {
            Value = valueText,
            Fill = fill
        };
    }

    private static void RenderMetric(MetricRow row, float value)
    {
        float normalized = Mathf.Clamp01(value / 100f);
        row.Value.text = $"{Mathf.RoundToInt(value)} · {RoomEnvironmentPresentation.GetGrade(value)}";
        row.Fill.fillAmount = normalized;
        row.Fill.color = DungeonUiTheme.GetMeterColor(normalized);
    }

    private static void RenderTemperatureMetric(
        MetricRow row,
        float comfortScore,
        float temperatureC)
    {
        float normalized = Mathf.Clamp01(comfortScore / 100f);
        row.Value.text =
            $"{temperatureC:0.#}°C · {RoomEnvironmentPresentation.GetGrade(comfortScore)}";
        row.Fill.fillAmount = normalized;
        row.Fill.color = DungeonUiTheme.GetMeterColor(normalized);
    }

    private static string BuildContributorText(RoomEnvironmentSnapshot snapshot)
    {
        if (snapshot.RoleContributions.Count == 0)
        {
            return "성향 시설 없음";
        }

        return string.Join("  ·  ", snapshot.RoleContributions.Select((contribution) =>
        {
            string fixtures = contribution.Fixtures.Count > 0
                ? string.Join(", ", contribution.Fixtures.Select(RoomEnvironmentPresentation.GetFixtureName))
                : "없음";
            return $"{RoomEnvironmentPresentation.GetRoleLabel(contribution.Role)}: {fixtures}";
        }));
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static TMP_Text CreateText(
        RectTransform parent,
        string name,
        ITmpKoreanFontService fontService,
        float fontSize,
        FontStyles fontStyle,
        TextAlignmentOptions alignment)
    {
        GameObject obj = CreateUiObject(name, parent);
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = DungeonUiTheme.TextPrimary;
        text.characterSpacing = 0f;
        text.raycastTarget = false;
        fontService.Apply(text);
        return text;
    }

    private static Image CreateImage(
        RectTransform parent,
        string name,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject obj = CreateUiObject(name, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = obj.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static void SetTopRect(
        RectTransform rect,
        float left,
        float top,
        float right,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2((left - right) * 0.5f, -top);
        rect.sizeDelta = new Vector2(-(left + right), height);
    }
}

public sealed class RoomInspectionToggleVisualState : MonoBehaviour
{
    public bool IsSelected { get; set; }
}
