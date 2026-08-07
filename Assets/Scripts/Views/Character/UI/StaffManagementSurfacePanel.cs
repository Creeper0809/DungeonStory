using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

internal sealed class StaffManagementDomainContext
{
    private StaffManagementDomainContext(
        bool isAvailable,
        StaffDiscontentRuntime staffDiscontent,
        ICharacterWorldQuery characterWorld,
        IBuildingWorldQuery buildingWorld,
        IPlayerStaffCommandSource playerCommands,
        ICharacterMoodImpulseQuery moodImpulse,
        IGameEventBus eventBus)
    {
        IsAvailable = isAvailable;
        StaffDiscontent = staffDiscontent;
        CharacterWorld = characterWorld;
        BuildingWorld = buildingWorld;
        PlayerCommands = playerCommands;
        MoodImpulse = moodImpulse;
        EventBus = eventBus;
    }

    internal bool IsAvailable { get; }
    internal StaffDiscontentRuntime StaffDiscontent { get; }
    internal ICharacterWorldQuery CharacterWorld { get; }
    internal IBuildingWorldQuery BuildingWorld { get; }
    internal IPlayerStaffCommandSource PlayerCommands { get; }
    internal ICharacterMoodImpulseQuery MoodImpulse { get; }
    internal IGameEventBus EventBus { get; }

    internal static StaffManagementDomainContext Unavailable() =>
        new StaffManagementDomainContext(
            false,
            null,
            null,
            null,
            null,
            null,
            null);

    internal static StaffManagementDomainContext Create(
        StaffDiscontentRuntime staffDiscontent,
        ICharacterWorldQuery characterWorld,
        IBuildingWorldQuery buildingWorld,
        IPlayerStaffCommandSource playerCommands,
        ICharacterMoodImpulseQuery moodImpulse,
        IGameEventBus eventBus) =>
        new StaffManagementDomainContext(
            true,
            staffDiscontent
                ?? throw new ArgumentNullException(nameof(staffDiscontent)),
            characterWorld
                ?? throw new ArgumentNullException(nameof(characterWorld)),
            buildingWorld
                ?? throw new ArgumentNullException(nameof(buildingWorld)),
            playerCommands
                ?? throw new ArgumentNullException(nameof(playerCommands)),
            moodImpulse
                ?? throw new ArgumentNullException(nameof(moodImpulse)),
            eventBus
                ?? throw new ArgumentNullException(nameof(eventBus)));
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class StaffManagementSurfacePanel
{
    private enum StaffPanelMode
    {
        Priorities,
        Management
    }

    private const float ManagementWidth = 980f;
    private const string NoneText = "없음";
    private const string UndeterminedText = "미정";
    private readonly IStaffManagementSurfaceQuery viewQuery;
    private readonly IStaffManagementSurfaceCommand viewCommands;
    private readonly IStaffWorkPriorityPanelModelBuilder modelBuilder;
    private readonly StaffManagementDomainContext domain;
    private readonly StaffDiscontentRuntime staffDiscontentRuntime;
    private readonly ICharacterWorldQuery characterWorldQuery;
    private readonly IBuildingWorldQuery buildingWorldQuery;
    private readonly IPlayerStaffCommandSource playerStaffCommands;
    private readonly ICharacterMoodImpulseQuery moodImpulseQuery;
    private readonly IGameEventBus gameEventBus;
    private readonly Func<CharacterActor> getSelectedCharacter;
    private readonly Action<CharacterActor> setSelectedCharacter;
    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private StaffPanelMode panelMode;
    private readonly Dictionary<StaffPanelMode, Button> modeButtons =
        new Dictionary<StaffPanelMode, Button>();

    internal StaffManagementSurfacePanel(
        IStaffManagementSurfaceQuery viewQuery,
        IStaffManagementSurfaceCommand viewCommands,
        IStaffWorkPriorityPanelModelBuilder modelBuilder,
        StaffManagementDomainContext domain,
        Func<CharacterActor> getSelectedCharacter,
        Action<CharacterActor> setSelectedCharacter)
    {
        this.viewQuery = viewQuery
            ?? throw new ArgumentNullException(nameof(viewQuery));
        this.viewCommands = viewCommands
            ?? throw new ArgumentNullException(nameof(viewCommands));
        this.modelBuilder = modelBuilder
            ?? throw new ArgumentNullException(nameof(modelBuilder));
        this.domain = domain ?? throw new ArgumentNullException(nameof(domain));
        staffDiscontentRuntime = domain.StaffDiscontent;
        characterWorldQuery = domain.CharacterWorld;
        buildingWorldQuery = domain.BuildingWorld;
        playerStaffCommands = domain.PlayerCommands;
        moodImpulseQuery = domain.MoodImpulse;
        gameEventBus = domain.EventBus;
        this.getSelectedCharacter = getSelectedCharacter
            ?? throw new ArgumentNullException(nameof(getSelectedCharacter));
        this.setSelectedCharacter = setSelectedCharacter
            ?? throw new ArgumentNullException(nameof(setSelectedCharacter));
    }

    internal bool IsManagementMode => panelMode == StaffPanelMode.Management;

    private CharacterActor SelectedCharacter
    {
        get => getSelectedCharacter();
        set => setSelectedCharacter(value);
    }

    private CharacterActor selectedCharacter
    {
        get => SelectedCharacter;
        set => SelectedCharacter = value;
    }

    private RectTransform contentRoot => viewQuery.ContentRoot;
    private Transform tableRoot => viewQuery.TableRoot;
    private TMP_Text titleText => viewQuery.TitleText;
    private int visibleWorkerCount;
    private int visibleCellCount;
    private int VisibleWorkerCount
    {
        set
        {
            visibleWorkerCount = value;
            viewCommands.SetVisibleCounts(visibleWorkerCount, visibleCellCount);
        }
    }
    private int VisibleCellCount
    {
        set
        {
            visibleCellCount = value;
            viewCommands.SetVisibleCounts(visibleWorkerCount, visibleCellCount);
        }
    }

    private IStaffWorkPriorityPanelUiFactory RequireUiFactory() =>
        viewQuery.UiFactory;

    private IStaffWorkPriorityPanelModelBuilder RequireModelBuilder() =>
        modelBuilder;

    private void Refresh() => viewCommands.RequestRefresh();

    internal void BuildModeBar(RectTransform host)
    {
        GameObject bar = RequireUiFactory().CreateUiObject("StaffModeBar", host);
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -48f);
        rect.sizeDelta = new Vector2(0f, 46f);

        HorizontalLayoutGroup layout = RequireUiFactory().AddHorizontalLayoutGroup(bar);
        layout.spacing = 8f;
        layout.padding = new RectOffset(0, 0, 2, 2);
        CreateModeButton(bar.transform, "P1Action_StaffModePriorities", "우선순위", StaffPanelMode.Priorities);
        CreateModeButton(bar.transform, "P1Action_StaffModeManagement", "직원 관리", StaffPanelMode.Management);
        RefreshModeButtons();
    }

    private void CreateModeButton(Transform parent, string actionName, string label, StaffPanelMode mode)
    {
        GameObject buttonObject = RequireUiFactory().CreateUiObject(actionName, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(172f, 40f);
        Image image = RequireUiFactory().AddImage(
            buttonObject,
            panelMode == mode ? DungeonUiTheme.Accent : DungeonUiTheme.SurfaceRaised);
        Button button = RequireUiFactory().AddButton(buttonObject, image);
        modeButtons[mode] = button;
        button.onClick.AddListener(() =>
        {
            panelMode = mode;
            RefreshModeButtons();
            Refresh();
        });
        RequireUiFactory().AddLayoutElement(buttonObject, 172f, 40f);

        TMP_Text text = AddManagementText(buttonObject.transform, label, 18f, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
    }

    private void RefreshModeButtons()
    {
        foreach (KeyValuePair<StaffPanelMode, Button> entry in modeButtons)
        {
            DungeonUiTheme.StyleButton(entry.Value, panelMode == entry.Key);
        }
    }

    internal void BuildStaffManagement(IReadOnlyList<StaffWorkPriorityRowModel> workers)
    {
        if (!domain.IsAvailable)
        {
            VisibleWorkerCount = 0;
            VisibleCellCount = 0;
            AddManagementBanner(
                "직원 관리 준비 중",
                "직원 관리 런타임이 연결된 뒤 사용할 수 있습니다.");
            return;
        }
        if (workers.Count > 0 && (selectedCharacter == null || workers.All((worker) => worker.Character != selectedCharacter)))
        {
            selectedCharacter = workers[0].Character;
        }

        if (titleText != null)
        {
            titleText.text = selectedCharacter != null
                ? $"직원 관리 ({workers.Count}) - {RequireModelBuilder().GetDisplayName(selectedCharacter)}"
                : $"직원 관리 ({workers.Count})";
        }

        VisibleWorkerCount = workers.Count;
        VisibleCellCount = workers.Count;
        contentRoot.sizeDelta = new Vector2(ManagementWidth, 900f + (workers.Count * 74f));

        AddManagementBanner("직원 선택", $"활성 직원 {workers.Count}명");
        if (workers.Count == 0)
        {
            AddManagementBanner("직원 없음", "관리할 수 있는 활성 직원이 없습니다.");
            return;
        }

        for (int i = 0; i < workers.Count; i++)
        {
            StaffWorkPriorityRowModel worker = workers[i];
            bool selected = worker.Character == selectedCharacter;
            CreateManagementCard(
                $"P1Action_StaffSelect_{i}",
                worker.Name,
                FormatWorkerSummary(worker),
                selected ? "선택됨" : "선택",
                () =>
                {
                    selectedCharacter = worker.Character;
                    Refresh();
                },
                68f,
                selected);
        }

        StaffWorkPriorityRowModel selectedWorker = workers.First((worker) => worker.Character == selectedCharacter);
        BuildDutyAndDiscontent(selectedWorker, workers);
        BuildOwnerCommands(selectedWorker, workers);
        BuildCharacterProfile(selectedWorker);
        BuildCharacterAi(selectedWorker);
    }

    private void BuildDutyAndDiscontent(
        StaffWorkPriorityRowModel worker,
        IReadOnlyList<StaffWorkPriorityRowModel> workers)
    {
        AddManagementBanner("직원 상태/근무/휴식", "근무 상태와 기분, 불만 누적을 관리합니다.");
        CreateManagementCard(
            "P1Action_StaffDutyToggle",
            $"{worker.Name} / {GetDutyLabel(worker)}",
            $"기분 {GetCondition(worker.Character, CharacterCondition.MOOD):0.#} / 수면 {GetCondition(worker.Character, CharacterCondition.SLEEP):0.#} / 현재 작업 {(worker.Work.isWorking ? "진행" : "대기")}",
            worker.Work.IsOffDuty ? "근무 복귀" : "휴식 명령",
            () =>
            {
                if (worker.Work.IsOffDuty)
                {
                    worker.Work.SetDutyState(AbilityWork.DutyState.OnDuty);
                }
                else
                {
                    worker.Work.BeginOffDuty("사장 휴식 명령");
                }

                Refresh();
            },
            82f);

        StaffDiscontentRuntime discontent =
            ResolveStaffDiscontentRuntime();
        StaffDiscontentRecord record = null;
        discontent?.State.TryGetRecord(worker.Character, out record);
        AddManagementBanner(
            "직원 불만/반란",
            record != null
                ? $"{record.Stage} / 기분 {record.LastMood:0.#} / 저기분 {record.LowMoodDays}일 / 반란 {record.LocalRebellionDays}일"
                : "아직 처리된 불만 기록이 없습니다.");
        CreateManagementStatusCard(
            "P1State_StaffDiscontent",
            "불만 상태",
            record != null
                ? $"이탈 {BoolText(record.IsDeparted)} / 반란 {BoolText(record.IsInLocalRebellion)} / 사장 위협 {BoolText(record.IsOwnerThreat)} / 격리 {BoolText(record.IsIsolated)} / 제압 {BoolText(record.IsSuppressed)}"
                : "일일 정산 후 기분과 누적 일수에 따라 기록됩니다.",
            82f);

        if (discontent != null && record != null && record.IsInLocalRebellion)
        {
            CharacterActor owner = characterWorldQuery?.Characters
                .FirstOrDefault(actor => actor != null && actor.IsOwner);
            CreateManagementCard(
                "P1Action_StaffRebellionCalm",
                "반란 대응",
                "진정, 격리, 자동 제압은 현재 반란 기록에 직접 적용됩니다.",
                "진정",
                () =>
                {
                    discontent.TryCalmStaff(worker.Character, owner, out StaffRebellionResponseResult result);
                    gameEventBus.ShowNotice(result.Message, NoticeFeedEvent.Grade.NONE);
                    Refresh();
                },
                76f);
            CreateManagementCard(
                "P1Action_StaffRebellionIsolate",
                "반란 직원 격리",
                "사장 위협 확산을 막고 반란 직원을 격리합니다.",
                "격리",
                () =>
                {
                    discontent.TryIsolateRebel(worker.Character, owner, out StaffRebellionResponseResult result);
                    gameEventBus.ShowNotice(result.Message, NoticeFeedEvent.Grade.NONE);
                    Refresh();
                },
                76f);
            CreateManagementCard(
                "P1Action_StaffRebellionAutoSuppress",
                "자동 제압 배정",
                $"경비 우선순위가 활성화된 직원 {workers.Count - 1}명 중 배정합니다.",
                "자동 배정",
                () =>
                {
                    int assigned = discontent.DispatchAutoSuppress(worker.Character);
                    gameEventBus.ShowNotice($"자동 제압 배정: {assigned}명", NoticeFeedEvent.Grade.NONE);
                    Refresh();
                },
                76f);
        }
    }

    private void BuildOwnerCommands(
        StaffWorkPriorityRowModel worker,
        IReadOnlyList<StaffWorkPriorityRowModel> workers)
    {
        IPlayerStaffCommandSource controller = playerStaffCommands;
        AddManagementBanner(
            "사장 우선 명령/반란 제압 명령",
            controller != null
                ? $"명령 직원 {GetObjectName(controller.SelectedActor, "미선택")} / 작업 대상 {GetObjectName(worker.Work.PriorityWorkTarget, NoneText)} / 제압 대상 {GetObjectName(worker.Work.PrioritySuppressActor, NoneText)}"
                : "사장 명령 컨트롤러가 현재 씬에 없습니다.");

        if (controller == null)
        {
            return;
        }

        List<BuildableObject> facilities = (buildingWorldQuery?.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where((facility) => facility != null && !facility.isDestroy)
            .Where((facility) => WorkCommandResolver.TryResolveFacilityCommand(
                worker.Character,
                facility,
                out _,
                out _))
            .ToList();
        for (int i = 0; i < facilities.Count; i++)
        {
            BuildableObject facility = facilities[i];
            CreateManagementCard(
                $"P1Action_OwnerPriority_{i}",
                $"우선 작업: {GetFacilityLabel(facility)}",
                "선택 직원이 수행할 수 있는 시설 작업이면 우선 대상으로 지정합니다.",
                "우선 지정",
                () =>
                {
                    controller.TrySelectActor(worker.Character, out _);
                    bool success = controller.TryIssuePriorityWorkCommand(facility, out string message);
                    gameEventBus.ShowNotice(message, success ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
                    Refresh();
                },
                76f);
        }

        StaffDiscontentRuntime discontent =
            ResolveStaffDiscontentRuntime();
        CharacterActor rebel = workers
            .Select((candidate) => candidate.Character)
            .FirstOrDefault((actor) => actor != worker.Character && discontent != null && discontent.IsRebellionTarget(actor));
        CreateManagementCard(
            "P1Action_OwnerSuppress",
            "반란 제압 우선 명령",
            rebel != null ? $"대상: {rebel.name}" : "현재 제압 가능한 반란 대상이 없습니다.",
            "제압 지정",
            () =>
            {
                controller.TrySelectActor(worker.Character, out _);
                bool success = controller.TryIssueSuppressCommand(rebel, out string message);
                gameEventBus.ShowNotice(message, success ? NoticeFeedEvent.Grade.NONE : NoticeFeedEvent.Grade.WARNING);
                Refresh();
            },
            76f);
    }

    private static string GetObjectName(UnityEngine.Object target, string fallback)
    {
        return target != null ? target.name : fallback;
    }

    private void BuildCharacterProfile(StaffWorkPriorityRowModel worker)
    {
        CharacterIdentity identity = worker.Character.Identity;
        CharacterRuntimeProfile profile = identity != null ? identity.Profile : null;
        string traits = profile != null && profile.TraitDisplayNames.Count > 0
            ? string.Join(", ", profile.TraitDisplayNames)
            : "특성 없음";
        CharacterStats stats = worker.Character.Stats;
        string abilitySummary = BuildCharacterStatSummary(stats);
        AddManagementBanner("캐릭터 프로필/종족/특성", $"{identity?.SpeciesTag ?? UndeterminedText} · {traits}");
        CreateManagementCard(
            "P1Action_StaffProfile",
            identity != null ? identity.DisplayName : worker.Name,
            $"역할 {identity?.Role.ToString() ?? UndeterminedText} · 유형 {identity?.CharacterType.ToString() ?? UndeterminedText}\n{identity?.GetSpeciesShortDescription()}\n특성: {traits}\n{abilitySummary}",
            "프로필 확인",
            () => gameEventBus.ShowNotice($"프로필 확인: {worker.Name}", NoticeFeedEvent.Grade.NONE),
            156f);
    }

    private static string BuildCharacterStatSummary(CharacterStats stats)
    {
        if (stats == null)
        {
            return "능력치 없음";
        }

        return string.Join(
            "\n",
            CharacterStatCatalog.All
                .Select(definition =>
                    $"{definition.DisplayName} {stats.GetCharacterStat(definition.Id)}")
                .Select((text, index) => new { text, row = index / 4 })
                .GroupBy(item => item.row)
                .Select(row => string.Join(" · ", row.Select(item => item.text))));
    }

    private void BuildCharacterAi(StaffWorkPriorityRowModel worker)
    {
        CustomerPersonaRuntime personaRuntime = worker.Character.PersonaRuntime;
        CustomerPersonaData persona = personaRuntime != null ? personaRuntime.Persona : null;
        AddManagementBanner(
            "성격/기분 반응",
            "직원의 성격과 최근 상황 반응을 확인합니다.");
        CreateManagementStatusCard(
            "P1State_StaffPersona",
            $"성격: {FirstValue(persona?.traitName, "기본")}",
            $"{FirstValue(persona?.flavorText, "설명 없음")}\n선호 시설: {FormatTags(persona?.preferredFacilityTags)}",
            104f);
        CreateManagementStatusCard(
            "P1State_StaffMood",
            "최근 기분 반응",
            $"대상 {FirstValue(moodImpulseQuery?.LastAppliedActorName, NoneText)} / 유형 {moodImpulseQuery?.LastAppliedType.ToString() ?? NoneText}",
            104f);
    }

    private StaffDiscontentRuntime ResolveStaffDiscontentRuntime()
    {
        return staffDiscontentRuntime;
    }

    private void AddManagementBanner(string title, string detail)
    {
        GameObject row = RequireUiFactory().CreateUiObject("Section_" + title, tableRoot);
        spawnedObjects.Add(row);
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(ManagementWidth, 54f);
        RequireUiFactory().AddImage(row, DungeonUiTheme.SurfaceRaised);
        RequireUiFactory().AddLayoutElement(row, ManagementWidth, 54f);

        TMP_Text label = AddManagementText(row.transform, $"{title}\n{detail}", 18f, FontStyles.Bold);
        label.color = DungeonUiTheme.Warning;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.margin = new Vector4(12f, 3f, 12f, 3f);
    }

    private void CreateManagementCard(
        string actionName,
        string title,
        string detail,
        string buttonLabel,
        Action onClick,
        float height,
        bool selected = false)
    {
        GameObject card = RequireUiFactory().CreateUiObject(actionName + "_Card", tableRoot);
        spawnedObjects.Add(card);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(ManagementWidth, height);
        RequireUiFactory().AddImage(
            card,
            selected ? Color.Lerp(DungeonUiTheme.Surface, DungeonUiTheme.Accent, 0.28f) : DungeonUiTheme.Surface);
        RequireUiFactory().AddLayoutElement(card, ManagementWidth, height);

        GameObject textObject = RequireUiFactory().CreateUiObject("Text", card.transform);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-160f, -6f);
        TMP_Text text = RequireUiFactory().AddText(textObject);
        text.text = $"<b>{title}</b>\n{detail}";
        text.fontSize = 16f;
        text.color = DungeonUiTheme.TextPrimary;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;

        GameObject buttonObject = RequireUiFactory().CreateUiObject(actionName, card.transform);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-10f, 0f);
        buttonRect.sizeDelta = new Vector2(138f, Mathf.Max(42f, height - 16f));
        Image image = RequireUiFactory().AddImage(buttonObject, DungeonUiTheme.Accent);
        Button button = RequireUiFactory().AddButton(buttonObject, image);
        DungeonUiTheme.StyleButton(button, selected: true);
        button.onClick.AddListener(() => onClick?.Invoke());

        TMP_Text buttonText = AddManagementText(buttonObject.transform, buttonLabel, 16f, FontStyles.Bold);
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.enableAutoSizing = true;
        buttonText.fontSizeMin = 10f;
        buttonText.fontSizeMax = 16f;
    }

    private void CreateManagementStatusCard(string stateName, string title, string detail, float height)
    {
        GameObject card = RequireUiFactory().CreateUiObject(stateName, tableRoot);
        spawnedObjects.Add(card);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(ManagementWidth, height);
        Image image = RequireUiFactory().AddImage(card, DungeonUiTheme.Surface);
        image.raycastTarget = false;
        RequireUiFactory().AddLayoutElement(card, ManagementWidth, height);

        GameObject textObject = RequireUiFactory().CreateUiObject("Text", card.transform);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);
        TMP_Text text = RequireUiFactory().AddText(textObject);
        text.text = $"<b>{title}</b>\n{detail}";
        text.fontSize = 16f;
        text.color = DungeonUiTheme.TextPrimary;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
    }

    private TMP_Text AddManagementText(Transform parent, string value, float fontSize, FontStyles style)
    {
        GameObject textObject = RequireUiFactory().CreateUiObject("Text", parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);
        TMP_Text text = RequireUiFactory().AddText(textObject);
        text.text = value ?? string.Empty;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static string FormatWorkerSummary(StaffWorkPriorityRowModel worker)
    {
        return $"{GetDutyLabel(worker)} / 기분 {GetCondition(worker.Character, CharacterCondition.MOOD):0.#} / 현재 작업 {(worker.Work.isWorking ? "진행" : "대기")}";
    }

    private static string GetDutyLabel(StaffWorkPriorityRowModel worker)
    {
        if (worker.Character.Lifecycle != null
            && worker.Character.Lifecycle.CurrentState == CharacterLifecycleState.OnExpedition)
        {
            return "원정 중";
        }

        return worker.Work.IsOffDuty ? "휴식/비번" : "근무 중";
    }

    private static float GetCondition(CharacterActor actor, CharacterCondition condition)
    {
        return actor != null
            && actor.Stats != null
            && actor.Stats.Stats.TryGetValue(condition, out float value)
                ? value
                : 0f;
    }

    private static string GetFacilityLabel(BuildableObject facility)
    {
        if (facility == null)
        {
            return "시설";
        }

        return facility.BuildingData != null && !string.IsNullOrWhiteSpace(facility.BuildingData.objectName)
            ? facility.BuildingData.objectName
            : facility.name;
    }

    private static string BoolText(bool value)
    {
        return value ? "예" : "아니오";
    }

    private static string FormatTags(IEnumerable<string> tags)
    {
        string value = tags != null
            ? string.Join(", ", tags.Where((tag) => !string.IsNullOrWhiteSpace(tag)))
            : string.Empty;
        return string.IsNullOrWhiteSpace(value) ? NoneText : value;
    }

    private static string FirstValue(params string[] values)
    {
        return values?.FirstOrDefault((value) => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    internal void Clear()
    {
        for (int index = spawnedObjects.Count - 1; index >= 0; index--)
        {
            RequireUiFactory().Release(spawnedObjects[index]);
        }
        spawnedObjects.Clear();
    }
}
