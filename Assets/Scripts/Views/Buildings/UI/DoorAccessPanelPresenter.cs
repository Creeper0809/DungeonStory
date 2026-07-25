using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IDoorAccessPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        Door door,
        TMP_FontAsset font,
        Action refresh);
}

public sealed class DoorAccessPanelPresenter : IDoorAccessPanelPresenter
{
    private readonly IDoorAccessCommandService commands;
    private readonly ICharacterAiWorldRegistry world;
    private readonly Dictionary<int, string> searchByDoor =
        new Dictionary<int, string>();

    public DoorAccessPanelPresenter(
        IDoorAccessCommandService commands,
        ICharacterAiWorldRegistry world)
    {
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        Door door,
        TMP_FontAsset font,
        Action refresh)
    {
        List<GameObject> created = new List<GameObject>();
        if (parent == null || door?.AccessPolicy == null)
        {
            return created;
        }

        AddHeader(parent, "문 사용 권한", font, created);
        AddPresetRow(parent, door, font, refresh, created);
        AddGroupToggles(parent, door, font, refresh, created);
        AddCommandRow(parent, door, font, refresh, created);
        AddIndividualRules(parent, door, font, refresh, created);
        return created;
    }

    private void AddPresetRow(
        Transform parent,
        Door door,
        TMP_FontAsset font,
        Action refresh,
        ICollection<GameObject> created)
    {
        GameObject row = CreateRow(parent, "DoorAccessPresets", 42f);
        created.Add(row);
        AddButton(row.transform, "모두 허용", font, () => ApplyPreset(DoorAccessPreset.AllowAll));
        AddButton(row.transform, "직원 전용", font, () => ApplyPreset(DoorAccessPreset.StaffOnly));
        AddButton(row.transform, "손님 구역", font, () => ApplyPreset(DoorAccessPreset.CustomerArea));
        AddButton(row.transform, "감방", font, () => ApplyPreset(DoorAccessPreset.Cell));
        AddButton(row.transform, "동물 우리", font, () => ApplyPreset(DoorAccessPreset.AnimalPen));

        void ApplyPreset(DoorAccessPreset preset)
        {
            commands.ApplyPreset(door, preset);
            refresh?.Invoke();
        }
    }

    private void AddGroupToggles(
        Transform parent,
        Door door,
        TMP_FontAsset font,
        Action refresh,
        ICollection<GameObject> created)
    {
        AddHeader(parent, "허용 그룹", font, created);
        (DoorAccessGroup Group, string Label)[] groups =
        {
            (DoorAccessGroup.Owner, "사장"),
            (DoorAccessGroup.Staff, "직원"),
            (DoorAccessGroup.Customer, "손님"),
            (DoorAccessGroup.Captive, "포로"),
            (DoorAccessGroup.Intruder, "침입자"),
            (DoorAccessGroup.Wildlife, "야생동물"),
            (DoorAccessGroup.CaptiveWildlife, "포획 동물")
        };

        for (int offset = 0; offset < groups.Length; offset += 4)
        {
            GameObject row = CreateRow(parent, $"DoorAccessGroups_{offset}", 38f);
            created.Add(row);
            foreach ((DoorAccessGroup group, string label) in groups.Skip(offset).Take(4))
            {
                bool initial = door.AccessPolicy.IsGroupAllowed(group);
                AddToggle(row.transform, label, font, initial, value =>
                {
                    commands.SetGroupAllowed(door, group, value);
                    refresh?.Invoke();
                });
            }
        }
    }

    private void AddCommandRow(
        Transform parent,
        Door door,
        TMP_FontAsset font,
        Action refresh,
        ICollection<GameObject> created)
    {
        GameObject row = CreateRow(parent, "DoorAccessCommands", 42f);
        created.Add(row);
        AddButton(row.transform, "권한 복사", font, () => commands.CopyPolicy(door));
        AddButton(row.transform, "권한 붙여넣기", font, () =>
        {
            commands.PastePolicy(door);
            refresh?.Invoke();
        });
        AddButton(row.transform, "같은 방 문에 적용", font, () =>
        {
            commands.ApplyPolicyToRoomDoors(door);
            refresh?.Invoke();
        });
    }

    private void AddIndividualRules(
        Transform parent,
        Door door,
        TMP_FontAsset font,
        Action refresh,
        ICollection<GameObject> created)
    {
        AddHeader(parent, "개별 예외", font, created);
        int doorKey = door.GetInstanceID();
        AddSearch(parent, doorKey, font, refresh, created);
        Dictionary<string, string> subjects = BuildSubjectMap(door.AccessPolicy);
        string search = searchByDoor.TryGetValue(doorKey, out string stored)
            ? stored?.Trim() ?? string.Empty
            : string.Empty;
        if (search.Length > 0)
        {
            subjects = subjects
                .Where(pair =>
                    pair.Key.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || pair.Value.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        }
        if (subjects.Count == 0)
        {
            AddStatus(
                parent,
                search.Length > 0
                    ? "검색 결과가 없습니다."
                    : "현재 등록할 인물이나 동물이 없습니다.",
                font,
                created);
            return;
        }

        foreach (KeyValuePair<string, string> entry in subjects
            .OrderBy(pair => pair.Value, StringComparer.Ordinal)
            .Take(24))
        {
            string id = entry.Key;
            DoorAccessIndividualRule rule = door.AccessPolicy.GetIndividualRule(id);
            GameObject row = CreateRow(parent, $"DoorAccessSubject_{Sanitize(id)}", 38f);
            created.Add(row);
            AddLabel(row.transform, entry.Value, font, 180f);
            AddToggle(row.transform, "개별 허용", font, rule == DoorAccessIndividualRule.Allow, value =>
            {
                commands.SetIndividualRule(
                    door,
                    id,
                    value ? DoorAccessIndividualRule.Allow : DoorAccessIndividualRule.GroupDefault);
                refresh?.Invoke();
            });
            AddToggle(row.transform, "개별 차단", font, rule == DoorAccessIndividualRule.Deny, value =>
            {
                commands.SetIndividualRule(
                    door,
                    id,
                    value ? DoorAccessIndividualRule.Deny : DoorAccessIndividualRule.GroupDefault);
                refresh?.Invoke();
            });
            if (rule != DoorAccessIndividualRule.GroupDefault)
            {
                AddButton(row.transform, "예외 삭제", font, () =>
                {
                    commands.SetIndividualRule(
                        door,
                        id,
                        DoorAccessIndividualRule.GroupDefault);
                    refresh?.Invoke();
                });
            }
        }

        if (subjects.Count > 24)
        {
            AddStatus(parent, $"개별 목록 {subjects.Count}명 중 24명 표시", font, created);
        }
    }

    private void AddSearch(
        Transform parent,
        int doorKey,
        TMP_FontAsset font,
        Action refresh,
        ICollection<GameObject> created)
    {
        GameObject inputObject = new GameObject(
            "DoorAccessSearch",
            typeof(RectTransform),
            typeof(Image),
            typeof(TMP_InputField),
            typeof(LayoutElement));
        inputObject.transform.SetParent(parent, false);
        inputObject.GetComponent<LayoutElement>().preferredHeight = 38f;
        inputObject.GetComponent<Image>().color = DungeonUiTheme.SurfaceMuted;

        TMP_Text text = CreateInputText(inputObject.transform, "Text", font);
        TMP_Text placeholder = CreateInputText(
            inputObject.transform,
            "Placeholder",
            font);
        placeholder.text = "이름 또는 영구 ID 검색";
        placeholder.color = new Color(
            DungeonUiTheme.TextSecondary.r,
            DungeonUiTheme.TextSecondary.g,
            DungeonUiTheme.TextSecondary.b,
            0.6f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = inputObject.GetComponent<Image>();
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.SetTextWithoutNotify(
            searchByDoor.TryGetValue(doorKey, out string current)
                ? current
                : string.Empty);
        input.onEndEdit.AddListener(value =>
        {
            searchByDoor[doorKey] = value?.Trim() ?? string.Empty;
            refresh?.Invoke();
        });
        created.Add(inputObject);
    }

    private Dictionary<string, string> BuildSubjectMap(DoorAccessPolicyState policy)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (CharacterActor actor in world.AllCharacters)
        {
            string id = actor?.Identity?.PersistentId?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            string displayName = actor.Identity.DisplayName;
            result[id] = string.IsNullOrWhiteSpace(displayName)
                ? id
                : $"{displayName}  ({id})";
        }

        foreach (WildlifeActor wildlife in world.Wildlife)
        {
            string id = wildlife?.WildlifeId?.Trim();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            result[id] = $"{wildlife.DisplayName}  ({id})";
        }

        foreach (string id in policy.IndividuallyAllowedIds.Concat(policy.IndividuallyDeniedIds))
        {
            if (!string.IsNullOrWhiteSpace(id) && !result.ContainsKey(id))
            {
                result[id] = $"{id}  (현재 부재)";
            }
        }

        return result;
    }

    private static void AddHeader(
        Transform parent,
        string label,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        GameObject header = new GameObject(
            "DoorAccessHeader",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        header.transform.SetParent(parent, false);
        LayoutElement layout = header.GetComponent<LayoutElement>();
        layout.preferredHeight = 34f;
        TMP_Text text = header.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 20f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        created.Add(header);
    }

    private static void AddStatus(
        Transform parent,
        string label,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        GameObject status = new GameObject(
            "DoorAccessStatus",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        status.transform.SetParent(parent, false);
        status.GetComponent<LayoutElement>().preferredHeight = 34f;
        TMP_Text text = status.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 16f;
        text.color = DungeonUiTheme.TextSecondary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        created.Add(status);
    }

    private static GameObject CreateRow(Transform parent, string name, float height)
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
        Action action)
    {
        GameObject buttonObject = new GameObject(
            "Button_" + Sanitize(label),
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredWidth = 112f;
        buttonObject.GetComponent<Image>().color = DungeonUiTheme.Panel;
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button);
        button.onClick.AddListener(() => action?.Invoke());
        AddStretchText(buttonObject.transform, label, font, 14f);
    }

    private static void AddToggle(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool initial,
        Action<bool> changed)
    {
        GameObject root = new GameObject(
            "Toggle_" + Sanitize(label),
            typeof(RectTransform),
            typeof(Image),
            typeof(Toggle),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredWidth = 138f;
        Image hitTarget = root.GetComponent<Image>();
        hitTarget.color = Color.clear;
        hitTarget.raycastTarget = true;
        Toggle toggle = root.GetComponent<Toggle>();

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(root.transform, false);
        RectTransform boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0f, 0.5f);
        boxRect.anchorMax = new Vector2(0f, 0.5f);
        boxRect.sizeDelta = new Vector2(24f, 24f);
        boxRect.anchoredPosition = new Vector2(13f, 0f);
        box.GetComponent<Image>().color = DungeonUiTheme.Panel;

        GameObject check = new GameObject("Check", typeof(RectTransform), typeof(Image));
        check.transform.SetParent(box.transform, false);
        RectTransform checkRect = check.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.2f, 0.2f);
        checkRect.anchorMax = new Vector2(0.8f, 0.8f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;
        check.GetComponent<Image>().color = DungeonUiTheme.Accent;

        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(32f, 0f);
        textRect.offsetMax = Vector2.zero;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 14f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;

        toggle.targetGraphic = hitTarget;
        toggle.graphic = check.GetComponent<Image>();
        toggle.isOn = initial;
        toggle.onValueChanged.AddListener(value => changed?.Invoke(value));
    }

    private static void AddLabel(
        Transform parent,
        string label,
        TMP_FontAsset font,
        float width)
    {
        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        labelObject.transform.SetParent(parent, false);
        labelObject.GetComponent<LayoutElement>().preferredWidth = width;
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 14f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    private static void AddStretchText(
        Transform parent,
        string label,
        TMP_FontAsset font,
        float fontSize)
    {
        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 2f);
        rect.offsetMax = new Vector2(-5f, -2f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = fontSize;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
    }

    private static TMP_Text CreateInputText(
        Transform parent,
        string name,
        TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(10f, 2f);
        rect.offsetMax = new Vector2(-10f, -2f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = font;
        text.fontSize = 14f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static string Sanitize(string value)
    {
        return new string((value ?? string.Empty)
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());
    }
}
