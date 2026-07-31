using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IServiceRoomBuildingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

public sealed class ServiceRoomBuildingPanelPresenter :
    IServiceRoomBuildingPanelPresenter
{
    private readonly IServiceSessionRuntime sessions;
    private readonly IServiceRoomLinkRuntime links;

    public ServiceRoomBuildingPanelPresenter(
        IServiceSessionRuntime sessions,
        IServiceRoomLinkRuntime links)
    {
        this.sessions = sessions
            ?? throw new ArgumentNullException(nameof(sessions));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new();
        if (parent == null || building == null)
        {
            return created;
        }

        BuildingServiceSupportAbility support =
            building.GetServiceSupportAbility();
        if (support != null)
        {
            string linkText = links.TryGetLinkForSupport(
                    building,
                    out ServiceSupportLinkSnapshot link)
                ? $"같은 방 연결 대상: {link.Hub.BuildingData.objectName}"
                : "같은 닫힌 방에 연결 가능한 서비스 허브가 없습니다.";
            AddText(parent, "서비스실 보조 시설", font, 20f,
                DungeonUiTheme.TextPrimary, 34f, created);
            AddText(parent, linkText, font, 14f,
                link != null
                    ? DungeonUiTheme.Accent
                    : DungeonUiTheme.Warning,
                42f, created);
            return created;
        }

        BuildingServiceHubAbility hubAbility = building.GetServiceHubAbility();
        if (hubAbility == null)
        {
            return created;
        }

        ServiceHubSnapshot snapshot = sessions.GetHubSnapshot(building);
        AddText(parent, $"서비스 상태 · {StateLabel(snapshot.State)}",
            font, 20f,
            snapshot.State == ServiceOperatingState.Suspended
                ? DungeonUiTheme.Warning
                : DungeonUiTheme.TextPrimary,
            34f, created);
        AddText(
            parent,
            $"{ModeLabel(snapshot.Mode)} · 용량 {snapshot.Capacity} · "
            + $"예상 대기 {snapshot.EstimatedWaitSeconds:0.#}초\n"
            + $"예상 수입 {snapshot.ExpectedRevenue:N0}골드 · "
            + $"만족도 {snapshot.ExpectedSatisfaction:0.#}",
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            58f,
            created);

        if (!string.IsNullOrWhiteSpace(snapshot.BlockedReason))
        {
            AddText(parent, $"일시 중단: {snapshot.BlockedReason}", font,
                14f, DungeonUiTheme.Warning, 42f, created);
        }

        string[] missing = RequiredFeatures(hubAbility, snapshot.Mode)
            .Where(feature => !links.TryResolveFeature(
                building,
                feature,
                out _,
                out _))
            .Select(FeatureLabel)
            .ToArray();
        if (missing.Length > 0)
        {
            AddText(
                parent,
                $"다음 확장 시설: {string.Join(", ", missing)}\n"
                + "잠겨 있어도 현재 간이 운영은 계속할 수 있습니다.",
                font,
                13f,
                DungeonUiTheme.TextSecondary,
                52f,
                created);
        }

        AddModeButton(
            parent,
            "간이 운영으로 전환",
            ServiceOperationMode.Direct,
            snapshot.Mode,
            building,
            font,
            showFeedback,
            refresh,
            created);
        AddModeButton(
            parent,
            "관리형으로 전환",
            ServiceOperationMode.Managed,
            snapshot.Mode,
            building,
            font,
            showFeedback,
            refresh,
            created);
        AddModeButton(
            parent,
            "자동화로 전환",
            ServiceOperationMode.Automated,
            snapshot.Mode,
            building,
            font,
            showFeedback,
            refresh,
            created);

        return created;
    }

    private void AddModeButton(
        Transform parent,
        string label,
        ServiceOperationMode target,
        ServiceOperationMode current,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        if (target == current)
        {
            return;
        }

        GameObject buttonObject = new GameObject(
            $"ServiceMode{target}",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 42f;
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected: false);
        button.onClick.AddListener(() =>
        {
            ServiceModeChangeResult result =
                sessions.SetMode(building, target);
            showFeedback?.Invoke(result.Message);
            refresh?.Invoke();
        });

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 3f);
        rect.offsetMax = new Vector2(-8f, -3f);
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        created.Add(buttonObject);
    }

    private static IEnumerable<string> RequiredFeatures(
        BuildingServiceHubAbility ability,
        ServiceOperationMode mode) =>
        mode switch
        {
            ServiceOperationMode.Direct =>
                ability.managedRequiredFeatureTags ?? Array.Empty<string>(),
            ServiceOperationMode.Managed =>
                ability.automatedRequiredFeatureTags ?? Array.Empty<string>(),
            _ => Array.Empty<string>()
        };

    private static string StateLabel(ServiceOperatingState state) =>
        state switch
        {
            ServiceOperatingState.Closed => "휴업",
            ServiceOperatingState.Suspended => "일시 중단",
            ServiceOperatingState.Direct => "간이 운영",
            ServiceOperatingState.Managed => "관리형",
            ServiceOperatingState.Automated => "자동화",
            _ => state.ToString()
        };

    private static string ModeLabel(ServiceOperationMode mode) =>
        mode switch
        {
            ServiceOperationMode.Direct => "간이 운영",
            ServiceOperationMode.Managed => "관리형",
            ServiceOperationMode.Automated => "자동화",
            _ => mode.ToString()
        };

    private static string FeatureLabel(string feature) =>
        feature switch
        {
            "service:reception" => "주문 접수대",
            "service:queue" => "순번판",
            "service:heated-serving" => "보온 배식대",
            "service:auto-order" => "자동 주문 장치",
            "service:staffed-checkout" => "분리 계산대",
            "service:display" => "진열대",
            "service:auto-checkout" => "자동 계산대",
            "service:lodging-reception" => "숙박 접수대",
            "service:room-cleanup" => "객실 정리함",
            "service:auto-room-assignment" => "자동 객실 배정판",
            "service:bath-reception" => "목욕 접수대",
            "service:bath-hygiene" => "목욕 위생대",
            "service:auto-water-control" => "자동 급배수 제어기",
            "service:medical-triage" => "의료 분류대",
            "service:medical-call" => "의료 호출판",
            _ => feature
        };

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
            "ServiceRoomText",
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
