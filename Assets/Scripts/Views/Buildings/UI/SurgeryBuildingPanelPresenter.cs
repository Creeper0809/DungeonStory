using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface ISurgeryBuildingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

public sealed class SurgeryBuildingPanelPresenter :
    ISurgeryBuildingPanelPresenter
{
    private readonly ISurgicalFacilityQuery facilities;
    private readonly ISurgeryRuntime surgery;
    private readonly ISurgeryCommandService commands;
    private readonly ISurgicalProcedureCatalog procedures;
    private readonly IWorldItemStackRuntime items;
    private readonly ISurgicalPartRuntime parts;

    public SurgeryBuildingPanelPresenter(
        ISurgicalFacilityQuery facilities,
        ISurgeryRuntime surgery,
        ISurgeryCommandService commands,
        ISurgicalProcedureCatalog procedures,
        IWorldItemStackRuntime items,
        ISurgicalPartRuntime parts)
    {
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        this.surgery = surgery ?? throw new ArgumentNullException(nameof(surgery));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.procedures = procedures
            ?? throw new ArgumentNullException(nameof(procedures));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new();
        ISurgicalFacilityAbility[] abilities = building?.BuildingData?.Abilities?
            .OfType<ISurgicalFacilityAbility>()
            .ToArray() ?? Array.Empty<ISurgicalFacilityAbility>();
        if (parent == null || building == null || abilities.Length == 0)
        {
            return created;
        }

        AddText(
            parent,
            "수술 시설",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);

        SurgeryFacilityTag ownTags = abilities.Aggregate(
            SurgeryFacilityTag.None,
            (current, ability) => current | ability.FacilityTags);
        bool primary = abilities.Any(ability => ability.IsPrimaryOperatingFacility);
        AddText(
            parent,
            $"{(primary ? "집도 시설" : "지원 시설")} · "
            + SurgicalFacilityQuery.FormatTags(ownTags),
            font,
            15f,
            DungeonUiTheme.TextSecondary,
            32f,
            created);

        SurgicalFacilitySnapshot snapshot = primary
            ? facilities.Evaluate(building, SurgeryFacilityTag.None)
            : default;
        if (primary)
        {
            AddText(
                parent,
                snapshot.IsAvailable
                    ? $"무균도 {snapshot.Sterility:P0} · 작업 속도 {snapshot.SpeedMultiplier:P0}"
                        + $" · 성공 보정 {snapshot.SuccessBonus:+0%;-0%;0%}"
                        + $" · 마취 안정 {snapshot.AnesthesiaBonus:P0}"
                    : snapshot.BlockReason,
                font,
                14f,
                snapshot.IsAvailable
                    ? DungeonUiTheme.TextPrimary
                    : DungeonUiTheme.Warning,
                42f,
                created);

            if (snapshot.SupportFacilities.Count > 0)
            {
                string supportNames = string.Join(
                    ", ",
                    snapshot.SupportFacilities
                        .Where(candidate => candidate?.BuildingData != null)
                        .Select(candidate => candidate.BuildingData.objectName)
                        .Distinct(StringComparer.Ordinal));
                AddText(
                    parent,
                    $"같은 방 지원 설비: {supportNames}",
                    font,
                    13f,
                    DungeonUiTheme.TextSecondary,
                    34f,
                    created);
            }
        }

        if (parts.TryGetOrganStorageStatus(
                building,
                out SurgicalOrganStorageSnapshot storage))
        {
            AddText(
                parent,
                $"장기 보관 {storage.StoredParts}/{storage.Capacity}"
                + $" · 냉각 {(storage.Powered ? "작동" : "중단")}"
                + (storage.Powered
                    ? $" · 연료 {storage.FuelSecondsRemaining:0}초"
                    : " · 일반 부패 속도 적용"),
                font,
                14f,
                storage.Powered
                    ? DungeonUiTheme.TextPrimary
                    : DungeonUiTheme.Warning,
                36f,
                created);
        }

        string facilityId = facilities.GetFacilityId(building);
        SurgeryOrder order = surgery.ActiveOrders.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.facilityId,
                facilityId,
                StringComparison.Ordinal));
        if (order == null)
        {
            AddText(
                parent,
                primary ? "현재 예약된 수술이 없습니다." : "대기 중",
                font,
                14f,
                DungeonUiTheme.TextSecondary,
                32f,
                created);
            return created;
        }

        string procedureName = procedures.TryGet(
            order.procedureId,
            out SurgicalProcedureSO procedure)
                ? procedure.DisplayName
                : order.procedureId;
        AddText(
            parent,
            $"{procedureName} · 환자 {order.subject?.displayName ?? order.subject?.subjectId}"
            + (string.IsNullOrWhiteSpace(order.doctorId)
                ? " · 의사 배정 대기"
                : $" · 의사 {order.doctorId}"),
            font,
            15f,
            DungeonUiTheme.TextPrimary,
            44f,
            created);
        created.Add(CreateProgress(
            parent,
            order.Progress01,
            $"{FormatState(order.state)} · {order.Progress01:P0}",
            font));

        foreach (SurgicalMaterialRequirement material in order.materials
                     .Where(candidate => candidate != null)
                     .OrderBy(candidate => candidate.optional)
                     .ThenBy(candidate => candidate.itemId, StringComparer.Ordinal))
        {
            int delivered = CountMaterial(
                order.materialDestinationId,
                material.itemId,
                deliveredOnly: true);
            int inbound = CountMaterial(
                order.materialDestinationId,
                material.itemId,
                deliveredOnly: false) - delivered;
            string itemName = items.CatalogProvider
                .GetDefinition(material.itemId)
                .DisplayName;
            AddText(
                parent,
                $"{itemName} {delivered}/{material.quantity}"
                + (inbound > 0 ? $" · 운반 중 {inbound}" : string.Empty)
                + (material.optional ? " · 선택" : string.Empty),
                font,
                13f,
                delivered >= material.quantity
                    ? DungeonUiTheme.TextSecondary
                    : DungeonUiTheme.Warning,
                27f,
                created);
        }

        if (!string.IsNullOrWhiteSpace(order.status))
        {
            AddText(
                parent,
                order.status,
                font,
                14f,
                IsBlocked(order.state)
                    ? DungeonUiTheme.Warning
                    : DungeonUiTheme.TextSecondary,
                38f,
                created);
        }

        GameObject cancel = CreateButton(parent, "수술 취소", font, () =>
        {
            bool succeeded = commands.TryCancel(
                order.orderId,
                out string message);
            showFeedback?.Invoke(message);
            if (succeeded)
            {
                refresh?.Invoke();
            }
        });
        created.Add(cancel);
        return created;
    }

    private int CountMaterial(
        string destinationId,
        string itemId,
        bool deliveredOnly)
    {
        return items.GetAllStacks()
            .Where(stack =>
                stack != null
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    itemId,
                    StringComparison.Ordinal)
                && (!deliveredOnly
                    || stack.State == WorldItemStackState.FacilityBuffer))
            .Sum(stack => stack.Quantity);
    }

    private static bool IsBlocked(SurgeryOrderState state)
    {
        return state is SurgeryOrderState.PatientWaiting
            or SurgeryOrderState.MaterialsWaiting
            or SurgeryOrderState.Failed;
    }

    private static string FormatState(SurgeryOrderState state)
    {
        return state switch
        {
            SurgeryOrderState.PatientWaiting => "환자 입실 대기",
            SurgeryOrderState.MaterialsWaiting => "재료 운반 대기",
            SurgeryOrderState.Anesthetizing => "마취",
            SurgeryOrderState.Incision => "절개",
            SurgeryOrderState.Procedure => "처치",
            SurgeryOrderState.Suturing => "봉합",
            SurgeryOrderState.Recovering => "회복 관찰",
            SurgeryOrderState.Completed => "완료",
            SurgeryOrderState.Failed => "실패",
            SurgeryOrderState.Cancelled => "취소",
            _ => state.ToString()
        };
    }

    private static GameObject CreateProgress(
        Transform parent,
        float ratio,
        string label,
        TMP_FontAsset font)
    {
        GameObject root = new(
            "SurgeryProgress",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 40f;
        root.GetComponent<Image>().color = DungeonUiTheme.Panel;

        GameObject fillObject = new(
            "Fill",
            typeof(RectTransform),
            typeof(Image));
        fillObject.transform.SetParent(root.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = DungeonUiTheme.Accent;
        fill.raycastTarget = false;

        AddOverlayText(root.transform, label, font);
        return root;
    }

    private static GameObject CreateButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        Action action)
    {
        GameObject root = new(
            "SurgeryCancelButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 42f;
        Button button = root.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected: false);
        button.onClick.AddListener(() => action?.Invoke());
        AddOverlayText(root.transform, label, font);
        return root;
    }

    private static void AddOverlayText(
        Transform parent,
        string value,
        TMP_FontAsset font)
    {
        GameObject labelObject = new(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(parent, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 3f);
        rect.offsetMax = new Vector2(-8f, -3f);
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.color = DungeonUiTheme.TextPrimary;
        text.fontSize = 15f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 11f;
        text.fontSizeMax = 15f;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
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
        GameObject textObject = new(
            "SurgeryText",
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
