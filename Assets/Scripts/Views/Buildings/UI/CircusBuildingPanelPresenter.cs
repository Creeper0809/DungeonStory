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
    private readonly IExternalInfluenceRuntime externalInfluence;
    private readonly IDomainFailureLocalizer failureLocalizer;
    private readonly Dictionary<string, CircusLethalityPolicy> selectedLethality =
        new Dictionary<string, CircusLethalityPolicy>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> panelStatus =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> pendingLethalProgram =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public CircusBuildingPanelPresenter(
        ICircusRuntime circus,
        ICaptivityRuntime captivity,
        IWildlifeCaptureRuntime wildlife,
        IExternalInfluenceRuntime externalInfluence,
        IDomainFailureLocalizer failureLocalizer)
    {
        this.circus = circus ?? throw new ArgumentNullException(nameof(circus));
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.externalInfluence = externalInfluence
            ?? throw new ArgumentNullException(nameof(externalInfluence));
        this.failureLocalizer = failureLocalizer
            ?? throw new ArgumentNullException(nameof(failureLocalizer));
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
        BuildingCircusStageAbility stageAbility =
            building.BuildingData.GetCircusStageAbility();
        string[] availablePerformers = captivity.Captives
            .Where(item => item.IsInCustody)
            .OrderByDescending(item => item.performerSkill)
            .Take(Mathf.Max(1, stageAbility.performerCapacity))
            .Select(item => item.captiveId)
            .ToArray();
        string[] availableAnimals = wildlife.CapturedAnimals
            .Where(item => string.IsNullOrWhiteSpace(item.assignedShowOrderId))
            .Select(item => item.wildlifeId)
            .ToArray();

        AddText(parent, "공연", font, 21f, DungeonUiTheme.TextPrimary, 34f, created);
        AddText(
            parent,
            $"포로 공연자 {captivity.Captives.Count(item => item.IsInCustody)}명"
            + $" · 포획 동물 {wildlife.CapturedAnimals.Count}마리",
            font,
            15f,
            DungeonUiTheme.TextSecondary,
            30f,
            created);

        AddText(
            parent,
            $"명성 {externalInfluence.Renown:0.#} · 공포 "
            + $"{externalInfluence.Dread:0.#} · 적대 소문 "
            + $"{externalInfluence.HostileRumor:0.#}",
            font,
            15f,
            DungeonUiTheme.TextSecondary,
            30f,
            created);
        GameObject influenceRow = CreateRow(
            parent,
            "CircusExternalInfluence",
            42f);
        created.Add(influenceRow);
        AddButton(
            influenceRow.transform,
            externalInfluence.IsDreadDefenseArmed
                ? "다음 침입 약화 예약됨"
                : "공포 15 · 다음 침입 약화",
            font,
            externalInfluence.IsDreadDefenseArmed,
            () =>
            {
                bool armed = externalInfluence.TryArmDreadDefense(
                    out DomainFailure failure);
                panelStatus[stageKey] = armed
                    ? "공포 15를 예약했습니다. 다음 일반 침입은 이동·공격 -10%, 집결 +10초, 보스는 -5%, +5초이며 침입 시작 시 1회 소모됩니다."
                    : failureLocalizer.Localize(failure);
                refresh?.Invoke();
            });

        float rumorReduction = Mathf.Min(
            15f,
            externalInfluence.HostileRumor);
        int renownCost = Mathf.CeilToInt(rumorReduction / 15f * 10f);
        int goldCost = Mathf.CeilToInt(rumorReduction / 15f * 200f);
        GameObject rumorRow = CreateRow(parent, "CircusRumorMitigation", 42f);
        created.Add(rumorRow);
        AddButton(
            rumorRow.transform,
            $"소문 {rumorReduction:0.#}↓ · 명성 {renownCost}",
            font,
            false,
            () => MitigateRumor(
                HostileRumorMitigationMethod.Renown,
                stageKey,
                refresh));
        AddButton(
            rumorRow.transform,
            $"소문 {rumorReduction:0.#}↓ · 골드 {goldCost}",
            font,
            false,
            () => MitigateRumor(
                HostileRumorMitigationMethod.Gold,
                stageKey,
                refresh));

        EcologyRaidSnapshot raid = externalInfluence.GetEcologyRaidSnapshot();
        if (raid.Phase != EcologyRaidPhase.Inactive)
        {
            AddText(
                parent,
                $"생태 습격 {FormatRaidPhase(raid.Phase)}"
                + (raid.Phase == EcologyRaidPhase.Scheduled
                    ? $" · {raid.RemainingSeconds:0.0}초 남음"
                    : string.Empty)
                + $" · 노출 식량 위치 {raid.ExposedFoodPositions.Count}곳"
                + $" · 도난 {raid.StolenQuantity}",
                font,
                14f,
                raid.Phase == EcologyRaidPhase.Resolved
                    ? DungeonUiTheme.TextSecondary
                    : DungeonUiTheme.Danger,
                42f,
                created);
        }
        if (panelStatus.TryGetValue(stageKey, out string status)
            && !string.IsNullOrWhiteSpace(status))
        {
            AddText(
                parent,
                status,
                font,
                14f,
                DungeonUiTheme.TextSecondary,
                48f,
                created);
        }

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
            GameObject row = CreateRow(parent, $"CircusPrograms_{offset}", 64f);
            created.Add(row);
            foreach (CircusProgramModule program in circus.Programs.Skip(offset).Take(3))
            {
                CircusProgramModule capturedProgram = program;
                CircusProgramForecast forecast = circus.GetForecast(
                    building,
                    program.programId,
                    lethality,
                    availablePerformers,
                    availableAnimals);
                string forecastLabel =
                    $"{program.displayName}\n"
                    + $"수입 {forecast.ExpectedRevenue}"
                    + $" · 만족 {forecast.MinimumSatisfaction:0}"
                    + $"~{forecast.MaximumSatisfaction:0}"
                    + $" · 사고 {forecast.AccidentChance:P0}";
                AddButton(row.transform, forecastLabel, font, false, () =>
                {
                    CircusLethalityPolicy selected =
                        selectedLethality[stageKey];
                    CircusProgramForecast currentForecast =
                        circus.GetForecast(
                            building,
                            capturedProgram.programId,
                            selected,
                            availablePerformers,
                            availableAnimals);
                    if (!currentForecast.CanSchedule)
                    {
                        panelStatus[stageKey] =
                            currentForecast.FailureReason;
                        refresh?.Invoke();
                        return;
                    }

                    bool lethal = selected is
                        CircusLethalityPolicy.FightToDeath
                        or CircusLethalityPolicy.ExecuteDesignatedTarget;
                    if (lethal
                        && (!pendingLethalProgram.TryGetValue(
                                stageKey,
                                out string pending)
                            || !string.Equals(
                                pending,
                                capturedProgram.programId,
                                StringComparison.Ordinal)))
                    {
                        pendingLethalProgram[stageKey] =
                            capturedProgram.programId;
                        panelStatus[stageKey] =
                            $"치명 정책 확인: 대상 {availablePerformers.Length}명"
                            + $" · 부상 {currentForecast.InjuryChance:P0}"
                            + $" · 사망 {currentForecast.DeathChance:P0}"
                            + $" · 명성 +{currentForecast.Renown:0.#}"
                            + $" · 공포 +{currentForecast.Dread:0.#}"
                            + $" · 적대 소문 +{currentForecast.HostileRumor:0.#}"
                            + " · 같은 프로그램을 다시 눌러 예약";
                        refresh?.Invoke();
                        return;
                    }

                    bool scheduled = circus.TrySchedule(
                        building,
                        capturedProgram.programId,
                        selected,
                        availablePerformers,
                        availableAnimals,
                        out _,
                        out string failureReason);
                    pendingLethalProgram.Remove(stageKey);
                    panelStatus[stageKey] = scheduled
                        ? $"공연 예약 완료 · 예상 수입 {currentForecast.ExpectedRevenue}"
                            + $" · 만족 {currentForecast.MinimumSatisfaction:0}"
                            + $"~{currentForecast.MaximumSatisfaction:0}"
                            + $" · 사고 {currentForecast.AccidentChance:P0}"
                            + $" · 명성 +{currentForecast.Renown:0.#}"
                            + $" · 공포 +{currentForecast.Dread:0.#}"
                            + $" · 적대 소문 +{currentForecast.HostileRumor:0.#}"
                        : failureReason;
                    refresh?.Invoke();
                }, 190f);
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

    private void MitigateRumor(
        HostileRumorMitigationMethod method,
        string stageKey,
        Action refresh)
    {
        float before = externalInfluence.HostileRumor;
        bool succeeded = externalInfluence.TryMitigateHostileRumor(
            method,
            out float reduced,
            out int cost,
            out DomainFailure failure);
        panelStatus[stageKey] = succeeded
            ? $"소문 수습 완료 · {before:0.#} → "
                + $"{externalInfluence.HostileRumor:0.#}"
                + $" · {FormatMitigationMethod(method)} {cost} 사용"
            : failureLocalizer.Localize(failure);
        refresh?.Invoke();
    }

    private static string FormatMitigationMethod(
        HostileRumorMitigationMethod method)
    {
        return method == HostileRumorMitigationMethod.Renown
            ? "명성"
            : "골드";
    }

    private static string FormatRaidPhase(EcologyRaidPhase phase)
    {
        return phase switch
        {
            EcologyRaidPhase.Scheduled => "예정",
            EcologyRaidPhase.InProgress => "진행",
            EcologyRaidPhase.Resolved => "해결",
            _ => "비활성"
        };
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
        Action action,
        float preferredWidth = 130f)
    {
        GameObject buttonObject = new GameObject(
            "CircusButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredWidth = preferredWidth;
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
        return building.RequirePersistentInstanceId().Value;
    }
}
