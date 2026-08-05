using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterSurgeryWindowViewFactory :
    ICharacterSurgeryWindowViewFactory
{
    public ICharacterSurgeryWindowView Create(Transform parent)
    {
        if (parent == null)
        {
            throw new ArgumentNullException(nameof(parent));
        }

        GameObject root = new GameObject(
            "CharacterSurgeryWindow",
            typeof(RectTransform),
            typeof(Image),
            typeof(CharacterSurgeryWindowView));
        root.transform.SetParent(parent, false);
        return root.GetComponent<CharacterSurgeryWindowView>();
    }
}

public sealed class CharacterSurgeryWindowView :
    MonoBehaviour,
    ICharacterSurgeryWindowView
{
    private ICharacterSurgeryWindowQuery query;
    private ICharacterSurgeryWindowCommand commands;
    private SurgeryPlanningSubject subject;
    private ITmpKoreanFontService fonts;
    private Action onClosed;
    private TMP_Text procedureValue;
    private TMP_Text nodeValue;
    private TMP_Text partValue;
    private TMP_Text doctorValue;
    private TMP_Text facilityValue;
    private TMP_Text details;
    private RectTransform panel;
    private IReadOnlyList<SurgeryWindowOption> procedureOptions =
        Array.Empty<SurgeryWindowOption>();
    private IReadOnlyList<SurgeryWindowOption> nodeOptions =
        Array.Empty<SurgeryWindowOption>();
    private IReadOnlyList<SurgeryWindowOption> partOptions =
        Array.Empty<SurgeryWindowOption>();
    private IReadOnlyList<SurgeryWindowOption> doctorOptions =
        Array.Empty<SurgeryWindowOption>();
    private IReadOnlyList<SurgeryWindowOption> facilityOptions =
        Array.Empty<SurgeryWindowOption>();
    private int procedureIndex;
    private int nodeIndex;
    private int partIndex;
    private int doctorIndex;
    private int facilityIndex;

    public GameObject Root => gameObject;

    public void Configure(
        ICharacterSurgeryWindowQuery query,
        ICharacterSurgeryWindowCommand commands,
        SurgeryPlanningSubject subject,
        ITmpKoreanFontService fonts,
        Action onClosed)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.subject = subject ?? throw new ArgumentNullException(nameof(subject));
        this.fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        this.onClosed = onClosed;
        Build();
        RefreshOptions(resetProcedureDependent: true);
    }

    private void Build()
    {
        RectTransform root = transform as RectTransform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        GetComponent<Image>().color = DungeonUiTheme.ModalScrim;

        panel = CreateRect("SurgeryPanel", transform);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        ApplyResponsivePanelSize();
        panel.gameObject.AddComponent<Image>().color = DungeonUiTheme.Panel;
        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 18, 18);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        TMP_Text title = CreateText(
            "Title",
            panel,
            subject.DisplayName,
            25f,
            FontStyles.Bold);
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
        procedureValue = CreateSelector(panel, "Procedure", ChangeProcedure);
        nodeValue = CreateSelector(panel, "Target", delta =>
        {
            nodeIndex = Wrap(nodeIndex + delta, nodeOptions.Count);
            RefreshDetails();
        });
        partValue = CreateSelector(panel, "Part", delta =>
        {
            partIndex = Wrap(partIndex + delta, partOptions.Count);
            RefreshDetails();
        });
        doctorValue = CreateSelector(panel, "Doctor", delta =>
        {
            doctorIndex = Wrap(doctorIndex + delta, doctorOptions.Count);
            RefreshDetails();
        });
        facilityValue = CreateSelector(panel, "Facility", delta =>
        {
            facilityIndex = Wrap(facilityIndex + delta, facilityOptions.Count);
            RefreshDetails();
        });

        details = CreateText("Details", panel, string.Empty, 14f, FontStyles.Normal);
        details.textWrappingMode = TextWrappingModes.Normal;
        details.gameObject.AddComponent<LayoutElement>().preferredHeight = 260f;

        RectTransform buttons = CreateRect("Commands", panel);
        HorizontalLayoutGroup buttonLayout =
            buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 8f;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandWidth = true;
        buttons.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
        CreateButton("Schedule", buttons, "Schedule", Schedule);
        CreateButton("CancelOrder", buttons, "Cancel", CancelOrder, destructive: true);
        CreateButton("Close", buttons, "Close", Close);
        fonts.ApplyToChildren(transform);
    }

    private void ChangeProcedure(int delta)
    {
        procedureIndex = Wrap(procedureIndex + delta, procedureOptions.Count);
        RefreshOptions(resetProcedureDependent: true);
    }

    private void RefreshOptions(bool resetProcedureDependent)
    {
        string procedureId = Current(procedureOptions, procedureIndex).Id;
        SurgeryWindowOptionsProjection projection = query.GetOptions(
            subject,
            procedureId);
        procedureOptions = projection.Procedures;
        procedureIndex = Wrap(procedureIndex, procedureOptions.Count);
        nodeOptions = projection.Nodes;
        nodeIndex = Wrap(nodeIndex, nodeOptions.Count);
        partOptions = projection.Parts;
        doctorOptions = projection.Doctors;
        facilityOptions = projection.Facilities;
        if (resetProcedureDependent)
        {
            partIndex = 0;
            facilityIndex = 0;
        }

        partIndex = Wrap(partIndex, partOptions.Count);
        doctorIndex = Wrap(doctorIndex, doctorOptions.Count);
        facilityIndex = Wrap(facilityIndex, facilityOptions.Count);
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        SurgeryWindowDetailsProjection projection = query.GetDetails(
            subject,
            CurrentSelection());
        procedureValue.text = projection.ProcedureLabel;
        nodeValue.text = projection.NodeLabel;
        partValue.text = projection.PartLabel;
        doctorValue.text = projection.DoctorLabel;
        facilityValue.text = projection.FacilityLabel;
        details.text = projection.BodyText;
    }

    private void Schedule()
    {
        SurgeryUiCommandResult result = commands.Schedule(
            subject,
            CurrentSelection());
        details.text = CharacterSurgeryUiText.FormatScheduleResult(result);
    }

    private void CancelOrder()
    {
        SurgeryUiCommandResult result = commands.Cancel(subject);
        details.text = CharacterSurgeryUiText.FormatCancelResult(result);
    }

    private SurgeryWindowSelection CurrentSelection() =>
        new SurgeryWindowSelection(
            Current(procedureOptions, procedureIndex).Id,
            Current(nodeOptions, nodeIndex).Id,
            Current(partOptions, partIndex).Id,
            Current(doctorOptions, doctorIndex).Id,
            Current(facilityOptions, facilityIndex).Id);

    private void Close()
    {
        onClosed?.Invoke();
        Destroy(gameObject);
    }

    private TMP_Text CreateSelector(
        Transform parent,
        string label,
        Action<int> change)
    {
        RectTransform row = CreateRect($"{label}Row", parent);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
        TMP_Text labelText = CreateText(
            "Label",
            row,
            label,
            15f,
            FontStyles.Bold);
        labelText.gameObject.AddComponent<LayoutElement>().preferredWidth = 112f;
        CreateButton("Previous", row, "<", () => change(-1))
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 46f;
        TMP_Text value = CreateText("Value", row, "-", 15f, FontStyles.Normal);
        value.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        value.alignment = TextAlignmentOptions.MidlineLeft;
        CreateButton("Next", row, ">", () => change(1))
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 46f;
        return value;
    }

    private TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float size,
        FontStyles style)
    {
        RectTransform rect = CreateRect(name, parent);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        fonts.Apply(text);
        return text;
    }

    private Button CreateButton(
        string name,
        Transform parent,
        string label,
        Action action,
        bool destructive = false)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => action?.Invoke());
        TMP_Text text = CreateText("Label", rect, label, 15f, FontStyles.Bold);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 2f);
        textRect.offsetMax = new Vector2(-4f, -2f);
        text.alignment = TextAlignmentOptions.Center;
        DungeonUiTheme.StyleButton(button, destructive: destructive);
        return button;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private void ApplyResponsivePanelSize()
    {
        Rect safe = Screen.safeArea;
        bool portrait = safe.height > safe.width;
        panel.sizeDelta = new Vector2(
            Mathf.Min(portrait ? 820f : 980f, safe.width - 36f),
            Mathf.Min(portrait ? 1180f : 820f, safe.height - 36f));
    }

    private void OnRectTransformDimensionsChange()
    {
        if (panel != null)
        {
            ApplyResponsivePanelSize();
        }
    }

    private static SurgeryWindowOption Current(
        IReadOnlyList<SurgeryWindowOption> options,
        int index) =>
        options != null && options.Count > 0
            ? options[Mathf.Clamp(index, 0, options.Count - 1)]
            : default;

    private static int Wrap(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        value %= count;
        return value < 0 ? value + count : value;
    }
}
