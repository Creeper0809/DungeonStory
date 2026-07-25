using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface ICircusBuildingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action refresh);
}

public sealed class CircusBuildingPanelPresenter : ICircusBuildingPanelPresenter
{
    private readonly ICircusRuntime circus;
    private readonly ICaptivityRuntime captivity;
    private readonly IWildlifeCaptureRuntime wildlife;
    private readonly Dictionary<string, CircusLethalityPolicy> selectedLethality =
        new Dictionary<string, CircusLethalityPolicy>(StringComparer.Ordinal);

    public CircusBuildingPanelPresenter(
        ICircusRuntime circus,
        ICaptivityRuntime captivity,
        IWildlifeCaptureRuntime wildlife)
    {
        this.circus = circus ?? throw new ArgumentNullException(nameof(circus));
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action refresh)
    {
        List<GameObject> created = new List<GameObject>();
        if (parent == null
            || building?.BuildingData.GetCircusStageAbility() == null)
        {
            return created;
        }

        string stageKey = GetStageKey(building);
        if (!selectedLethality.TryGetValue(stageKey, out CircusLethalityPolicy lethality))
        {
            lethality = CircusLethalityPolicy.StopWhenDowned;
            selectedLethality[stageKey] = lethality;
        }

        AddText(parent, "공연", font, 21f, DungeonUiTheme.TextPrimary, 34f, created);
        AddText(
            parent,
            $"포로 공연자 {captivity.Captives.Count(item => item.IsActive)}명"
            + $" · 포획 동물 {wildlife.CapturedAnimals.Count}마리",
            font,
            15f,
            DungeonUiTheme.TextSecondary,
            30f,
            created);

        GameObject lethalityRow = CreateRow(parent, "CircusLethality", 42f);
        created.Add(lethalityRow);
        AddButton(lethalityRow.transform, "쓰러지면 중단", font, lethality == CircusLethalityPolicy.StopWhenDowned, () => Select(CircusLethalityPolicy.StopWhenDowned));
        AddButton(lethalityRow.transform, "사고 허용", font, lethality == CircusLethalityPolicy.AllowAccidents, () => Select(CircusLethalityPolicy.AllowAccidents));
        AddButton(lethalityRow.transform, "사망까지", font, lethality == CircusLethalityPolicy.FightToDeath, () => Select(CircusLethalityPolicy.FightToDeath));
        AddButton(lethalityRow.transform, "지정 처형", font, lethality == CircusLethalityPolicy.ExecuteDesignatedTarget, () => Select(CircusLethalityPolicy.ExecuteDesignatedTarget));

        void Select(CircusLethalityPolicy value)
        {
            selectedLethality[stageKey] = value;
            refresh?.Invoke();
        }

        AddText(parent, "프로그램", font, 18f, DungeonUiTheme.TextPrimary, 30f, created);
        for (int offset = 0; offset < circus.Programs.Count; offset += 3)
        {
            GameObject row = CreateRow(parent, $"CircusPrograms_{offset}", 44f);
            created.Add(row);
            foreach (CircusProgramModule program in circus.Programs.Skip(offset).Take(3))
            {
                CircusProgramModule capturedProgram = program;
                AddButton(row.transform, program.displayName, font, false, () =>
                {
                    BuildingCircusStageAbility ability =
                        building.BuildingData.GetCircusStageAbility();
                    string[] performers = captivity.Captives
                        .Where(item => item.IsActive)
                        .OrderByDescending(item => item.performerSkill)
                        .Take(Mathf.Max(1, ability.performerCapacity))
                        .Select(item => item.captiveId)
                        .ToArray();
                    string[] animals = wildlife.CapturedAnimals
                        .Where(item => string.IsNullOrWhiteSpace(item.assignedShowOrderId))
                        .Select(item => item.wildlifeId)
                        .ToArray();
                    circus.TrySchedule(
                        building,
                        capturedProgram.programId,
                        selectedLethality[stageKey],
                        performers,
                        animals,
                        out _,
                        out _);
                    refresh?.Invoke();
                });
            }
        }

        foreach (CircusShowOrder order in circus.Orders
            .Where(item => item.stagePosition == building.centerPos)
            .OrderByDescending(item => item.orderId, StringComparer.Ordinal)
            .Take(4))
        {
            CircusShowOrder capturedOrder = order;
            float progress = order.state == CircusShowState.Composition
                ? order.preparationWorkCompleted
                    / Mathf.Max(0.01f, order.preparationWorkRequired)
                : order.state == CircusShowState.Performing
                    ? order.elapsedShowSeconds
                        / Mathf.Max(0.01f, order.showDurationSeconds)
                    : order.IsTerminal ? 1f : 0f;
            AddText(
                parent,
                $"{GetProgramName(order.programId)} · {FormatState(order.state)}"
                + $" · {Mathf.Clamp01(progress):P0}\n{order.statusMessage}",
                font,
                15f,
                order.state == CircusShowState.Cancelled
                    ? DungeonUiTheme.Danger
                    : DungeonUiTheme.TextPrimary,
                56f,
                created);
            if (!order.IsTerminal)
            {
                GameObject cancelRow = CreateRow(parent, $"CircusCancel_{order.orderId}", 38f);
                created.Add(cancelRow);
                AddButton(cancelRow.transform, "공연 취소", font, false, () =>
                {
                    circus.Cancel(capturedOrder.orderId, "플레이어가 공연을 취소했습니다.");
                    refresh?.Invoke();
                });
            }
        }

        return created;
    }

    private string GetProgramName(string programId)
    {
        return circus.Programs.FirstOrDefault(item =>
            string.Equals(item.programId, programId, StringComparison.Ordinal))
            ?.displayName ?? programId;
    }

    private static string FormatState(CircusShowState state)
    {
        return state switch
        {
            CircusShowState.Composition => "편성·준비",
            CircusShowState.ParticipantEscort => "참가자 호송",
            CircusShowState.AudienceEntering => "관객 입장",
            CircusShowState.Performing => "공연 중",
            CircusShowState.Settlement => "정산",
            CircusShowState.CleanupAndTreatment => "청소·치료",
            CircusShowState.Completed => "완료",
            CircusShowState.Cancelled => "취소",
            _ => state.ToString()
        };
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
        bool selected,
        Action action)
    {
        GameObject buttonObject = new GameObject(
            "CircusButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredWidth = 130f;
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
        ICollection<GameObject> created)
    {
        GameObject textObject = new GameObject(
            "CircusText",
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

    private static string GetStageKey(BuildableObject building)
    {
        return $"{building.id}:{building.centerPos.x}:{building.centerPos.y}";
    }
}
