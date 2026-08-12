using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public class StaffWorkPriorityPanel :
    MonoBehaviour,
    IStaffManagementSurfaceQuery,
    IStaffManagementSurfaceCommand
{
    private const string DeferCleaningActionTag = "order:defer-cleaning";
    private const float CharacterColumnWidth = 180f;
    private const float WorkColumnWidth = 98f;
    private const float StatusColumnWidth = 270f;
    private const float RowHeight = 78f;
    private const float HeaderHeight = 56f;
    private const float PanelPadding = 16f;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform rowRoot;
    [SerializeField] private Button rowButtonPrefab;
    [SerializeField] private TMP_Text selectedCharacterText;
    [SerializeField] private bool hideWhenSelectedCharacterCannotWork;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private CharacterActor selectedCharacter;
    private RectTransform contentRoot;
    private RectTransform tableRoot;
    private TMP_Text titleText;
    private int lastWorkerHash;
    private float nextAutoRefreshAt;
    private IStaffWorkPriorityPanelModelBuilder modelBuilder;
    private IStaffWorkPriorityPanelUiFactory uiFactory;
    private IUiClock uiClock;
    private StaffDiscontentRuntime staffDiscontentRuntime;
    private ICharacterWorldQuery characterWorldQuery;
    private IBuildingWorldQuery buildingWorldQuery;
    private IPlayerStaffCommandSource playerStaffCommands;
    private ICharacterMoodImpulseQuery moodImpulseQuery;
    private IGameEventBus gameEventBus;
    private CharacterDirectOrderCostPreviewService directOrderCosts;
    private ICharacterApologyCommand apologyCommands;
    private ICharacterRitualFastingQuery ritualFastingQuery;
    private ICharacterRitualFastingCommand ritualFastingCommands;
    private ICharacterManaQuery manaQuery;
    private IArcaneOverchargeCommand arcaneOverchargeCommands;
    private ICombatEquipmentRuntime combatEquipment;
    private ICharacterPerformanceQuery performance;
    private StaffManagementSurfacePanel managementSurface;
    private IDisposable infoFeedSubscription;
    public int VisibleWorkerCount { get; private set; }
    public int VisibleCellCount { get; private set; }

    [Inject]
    public void ConstructStaffWorkPriorityPanel(
        IStaffWorkPriorityPanelModelBuilder modelBuilder,
        IStaffWorkPriorityPanelUiFactory uiFactory,
        IUiClock uiClock)
    {
        this.modelBuilder = modelBuilder
            ?? throw new ArgumentNullException(nameof(modelBuilder));
        this.uiFactory = uiFactory
            ?? throw new ArgumentNullException(nameof(uiFactory));
        this.uiClock = uiClock
            ?? throw new ArgumentNullException(nameof(uiClock));
        managementSurface = null;
    }

    [Inject]
    public void ConstructStaffManagementDependencies(
        CharacterSceneRuntimeReferences characterRuntimes,
        ICharacterWorldQuery characterWorldQuery,
        IBuildingWorldQuery buildingWorldQuery,
        IPlayerStaffCommandSource playerStaffCommands,
        ICharacterMoodImpulseQuery moodImpulseQuery)
    {
        staffDiscontentRuntime = (characterRuntimes
                ?? throw new ArgumentNullException(nameof(characterRuntimes)))
            .StaffDiscontent
            ?? throw new InvalidOperationException(
                $"{nameof(StaffWorkPriorityPanel)} requires a loaded {nameof(StaffDiscontentRuntime)}.");
        this.characterWorldQuery = characterWorldQuery
            ?? throw new ArgumentNullException(nameof(characterWorldQuery));
        this.buildingWorldQuery = buildingWorldQuery
            ?? throw new ArgumentNullException(nameof(buildingWorldQuery));
        this.playerStaffCommands = playerStaffCommands
            ?? throw new ArgumentNullException(nameof(playerStaffCommands));
        this.moodImpulseQuery = moodImpulseQuery
            ?? throw new ArgumentNullException(nameof(moodImpulseQuery));
        managementSurface = null;
    }

    [Inject]
    public void ConstructStaffWorkPriorityEventBus(IGameEventBus gameEventBus)
    {
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        managementSurface = null;
        SubscribeToInfoFeed();
    }

    [Inject]
    public void ConstructStaffWorkPriorityIdentityCosts(
        CharacterDirectOrderCostPreviewService directOrderCosts)
    {
        this.directOrderCosts = directOrderCosts
            ?? throw new ArgumentNullException(nameof(directOrderCosts));
    }

    [Inject]
    public void ConstructStaffWorkPriorityApology(
        ICharacterApologyCommand apologyCommands)
    {
        this.apologyCommands = apologyCommands
            ?? throw new ArgumentNullException(nameof(apologyCommands));
        managementSurface = null;
    }

    [Inject]
    public void ConstructStaffWorkPriorityRitualFasting(
        ICharacterRitualFastingQuery ritualFastingQuery,
        ICharacterRitualFastingCommand ritualFastingCommands)
    {
        this.ritualFastingQuery = ritualFastingQuery
            ?? throw new ArgumentNullException(nameof(ritualFastingQuery));
        this.ritualFastingCommands = ritualFastingCommands
            ?? throw new ArgumentNullException(nameof(ritualFastingCommands));
        managementSurface = null;
    }

    [Inject]
    public void ConstructStaffWorkPriorityArcane(
        ICharacterManaQuery manaQuery,
        IArcaneOverchargeCommand arcaneOverchargeCommands,
        ICombatEquipmentRuntime combatEquipment,
        ICharacterPerformanceQuery performance)
    {
        this.manaQuery = manaQuery
            ?? throw new ArgumentNullException(nameof(manaQuery));
        this.arcaneOverchargeCommands = arcaneOverchargeCommands
            ?? throw new ArgumentNullException(nameof(arcaneOverchargeCommands));
        this.combatEquipment = combatEquipment
            ?? throw new ArgumentNullException(nameof(combatEquipment));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        managementSurface = null;
    }

    private void Awake()
    {
        panelRoot ??= gameObject;
    }

    private void Start()
    {
        RequireUiFactory().ApplyFonts(transform);
        Refresh();
    }

    private void Update()
    {
        if (!isActiveAndEnabled || uiClock.Time < nextAutoRefreshAt)
        {
            return;
        }

        nextAutoRefreshAt = uiClock.Time + 0.5f;
        int workerHash = CalculateWorkerHash();
        if (workerHash != lastWorkerHash)
        {
            Refresh();
        }
    }

    public void OnTriggerEvent(InfoFeedEvent eventType)
    {
        CharacterActor actor = eventType.Target as CharacterActor;
        if (actor != null && actor.TryGetAbility(out AbilityWork _))
        {
            selectedCharacter = actor;
            Refresh();
            return;
        }

        if (hideWhenSelectedCharacterCannotWork)
        {
            selectedCharacter = null;
            Refresh();
        }
    }

    public void Refresh()
    {
        EnsureLayout();
        BuildTable();
    }

    private void EnsureLayout()
    {
        if (contentRoot != null && tableRoot != null)
        {
            return;
        }

        RectTransform host = ResolveHost();
        ClearHost(host);

        GameObject titleObject = RequireUiFactory().CreateUiObject("Title", host);
        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(0f, 44f);

        titleText = RequireUiFactory().AddText(titleObject);
        titleText.text = "직원 작업 우선순위";
        titleText.color = DungeonUiTheme.TextPrimary;
        titleText.fontSize = 28f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Left;

        RequireManagementSurface().BuildModeBar(host);

        GameObject scrollObject = RequireUiFactory().CreateUiObject("PriorityScrollView", host);
        RectTransform scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = new Vector2(0f, -106f);

        RequireUiFactory().AddImage(scrollObject, DungeonUiTheme.SurfaceMuted);

        ScrollRect scrollRect = RequireUiFactory().AddScrollRect(scrollObject);

        GameObject viewportObject = RequireUiFactory().CreateUiObject("Viewport", scrollRectTransform);
        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(PanelPadding, PanelPadding);
        viewportRect.offsetMax = new Vector2(-PanelPadding, -PanelPadding);

        RequireUiFactory().AddImage(viewportObject, new Color(1f, 1f, 1f, 0.01f));
        RequireUiFactory().AddMask(viewportObject, false);

        GameObject contentObject = RequireUiFactory().CreateUiObject("Content", viewportRect);
        contentRoot = contentObject.GetComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(0f, 1f);
        contentRoot.pivot = new Vector2(0f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;

        RequireUiFactory().AddVerticalLayoutGroup(contentObject);
        RequireUiFactory().AddContentSizeFitter(contentObject);

        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRoot;
        tableRoot = contentRoot;
    }

    private RectTransform ResolveHost()
    {
        Transform body = transform.Find("Body");
        if (body != null && body is RectTransform bodyRect)
        {
            TMP_Text bodyText = body.GetComponent<TMP_Text>();
            if (bodyText != null)
            {
                bodyText.text = string.Empty;
                bodyText.enabled = false;
            }

            return bodyRect;
        }

        if (rowRoot != null && rowRoot is RectTransform rowRect)
        {
            return rowRect;
        }

        RectTransform rect = transform as RectTransform;
        if (rect != null)
        {
            return rect;
        }

        return RequireUiFactory().EnsureRectTransform(gameObject);
    }

    private void ClearHost(RectTransform host)
    {
        if (host == null)
        {
            return;
        }

        for (int i = host.childCount - 1; i >= 0; i--)
        {
            Transform child = host.GetChild(i);
            Transform rowButtonTransform = rowButtonPrefab != null ? rowButtonPrefab.transform : null;
            Transform selectedTextTransform = selectedCharacterText != null ? selectedCharacterText.transform : null;
            if (child == rowButtonTransform || child == selectedTextTransform)
            {
                child.gameObject.SetActive(false);
                continue;
            }

            RequireUiFactory().Release(child.gameObject);
        }
    }

    private void BuildTable()
    {
        if (tableRoot == null)
        {
            return;
        }

        RequireManagementSurface().Clear();
        ClearSpawnedObjects();

        IReadOnlyList<WorkTypeDefinition> workTypes = WorkTaskCatalog.Definitions;
        IReadOnlyList<StaffWorkPriorityRowModel> workers = RequireModelBuilder().BuildRows();
        VisibleWorkerCount = workers.Count;
        VisibleCellCount = workers.Count * workTypes.Count;
        lastWorkerHash = RequireModelBuilder().CalculateWorkerHash(workers);

        if (RequireManagementSurface().IsManagementMode)
        {
            RequireManagementSurface().BuildStaffManagement(workers);
            return;
        }

        if (titleText != null)
        {
            string selectedName = selectedCharacter != null
                ? RequireModelBuilder().GetDisplayName(selectedCharacter)
                : string.Empty;
            titleText.text = string.IsNullOrEmpty(selectedName)
                ? $"직원 작업 우선순위 ({workers.Count})"
                : $"직원 작업 우선순위 ({workers.Count}) - {selectedName}";
        }

        float tableWidth = CharacterColumnWidth + StatusColumnWidth + (WorkColumnWidth * workTypes.Count);
        float tableHeight = HeaderHeight + (RowHeight * Mathf.Max(1, workers.Count));
        contentRoot.sizeDelta = new Vector2(tableWidth, tableHeight);

        if (workers.Count == 0)
        {
            GameObject emptyRow = CreateRow("Empty", tableWidth, HeaderHeight);
            CreateLabelCell(emptyRow.transform, "직원 없음", tableWidth, HeaderHeight, TextAlignmentOptions.Center, true);
            return;
        }

        BuildHeader(workTypes, tableWidth);
        foreach (StaffWorkPriorityRowModel worker in workers)
        {
            BuildWorkerRow(worker, workTypes, tableWidth);
        }
    }

    private void BuildHeader(IReadOnlyList<WorkTypeDefinition> workTypes, float tableWidth)
    {
        GameObject row = CreateRow("Header", tableWidth, HeaderHeight);
        CreateLabelCell(row.transform, "캐릭터", CharacterColumnWidth, HeaderHeight, TextAlignmentOptions.Center, true);

        foreach (WorkTypeDefinition definition in workTypes)
        {
            TMP_Text label = CreateLabelCell(
                row.transform,
                GetWorkTypeLabel(definition),
                WorkColumnWidth,
                HeaderHeight,
                TextAlignmentOptions.Center,
                true);
            label.enableAutoSizing = true;
            label.fontSizeMin = 14f;
            label.fontSizeMax = 20f;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = DungeonUiTheme.TextPrimary;
        }

        CreateLabelCell(row.transform, "상태", StatusColumnWidth, HeaderHeight, TextAlignmentOptions.Center, true);
    }

    private void BuildWorkerRow(StaffWorkPriorityRowModel worker, IReadOnlyList<WorkTypeDefinition> workTypes, float tableWidth)
    {
        GameObject row = CreateRow(worker.Character.name, tableWidth, RowHeight);
        TMP_Text nameLabel = CreateLabelCell(
            row.transform,
            worker.Name,
            CharacterColumnWidth,
            RowHeight,
            TextAlignmentOptions.Left,
            false);
        nameLabel.enableAutoSizing = true;
        nameLabel.fontSizeMin = 13f;
        nameLabel.fontSizeMax = 19f;
        nameLabel.fontStyle = worker.Character == selectedCharacter ? FontStyles.Bold : FontStyles.Normal;
        nameLabel.color = worker.Character == selectedCharacter
            ? DungeonUiTheme.Warning
            : DungeonUiTheme.TextPrimary;

        foreach (WorkTypeDefinition definition in workTypes)
        {
            CreatePriorityCell(row.transform, worker, definition);
        }

        TMP_Text statusLabel = CreateLabelCell(
            row.transform,
            GetWorkerStatus(worker),
            StatusColumnWidth,
            RowHeight,
            TextAlignmentOptions.Left,
            false);
        statusLabel.enableAutoSizing = true;
        statusLabel.fontSizeMin = 10f;
        statusLabel.fontSizeMax = 14f;
        statusLabel.textWrappingMode = TextWrappingModes.Normal;
    }

    private GameObject CreateRow(string name, float width, float height)
    {
        GameObject row = RequireUiFactory().CreateUiObject(name, tableRoot);
        spawnedObjects.Add(row);

        RectTransform rect = row.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);

        RequireUiFactory().AddHorizontalLayoutGroup(row);
        RequireUiFactory().AddLayoutElement(row, width, height);
        return row;
    }

    private TMP_Text CreateLabelCell(
        Transform parent,
        string text,
        float width,
        float height,
        TextAlignmentOptions alignment,
        bool header)
    {
        GameObject cell = CreateCellObject("Label", parent, width, height);
        RequireUiFactory().AddImage(
            cell,
            header ? DungeonUiTheme.SurfaceRaised : DungeonUiTheme.Surface);

        TMP_Text label = AddCellText(cell.transform, text, alignment, header);
        label.fontSize = header ? 16f : 18f;
        label.color = DungeonUiTheme.TextPrimary;
        label.margin = alignment == TextAlignmentOptions.Left
            ? new Vector4(8f, 0f, 4f, 0f)
            : Vector4.zero;

        return label;
    }

    private void CreatePriorityCell(
        Transform parent,
        StaffWorkPriorityRowModel worker,
        WorkTypeDefinition definition)
    {
        WorkTypeId workTypeId = definition.WorkTypeId;
        FacilityWorkType legacyType = FacilityWorkTypeMap.GetRequired(definition);
        WorkPriorityLevel priority = worker.Work.WorkPriorities.GetPriority(workTypeId);
        GameObject cell = CreateCellObject($"Cell_{worker.Character.GetInstanceID()}_{legacyType}", parent, WorkColumnWidth, RowHeight);
        Image image = RequireUiFactory().AddImage(cell, GetPriorityColor(priority, worker.Character == selectedCharacter));

        Button button = RequireUiFactory().AddButton(cell, image);
        WorkTypeId capturedType = workTypeId;
        AbilityWork capturedWork = worker.Work;
        CharacterActor capturedCharacter = worker.Character;
        button.onClick.AddListener(() =>
        {
            WorkPriorityLevel current = capturedWork.WorkPriorities.GetPriority(capturedType);
            WorkPriorityLevel next = current.Next();
            capturedWork.SetWorkPriority(capturedType, next);
            if (StaffWorkPriorityIdentityOrderPolicy.IsDeferredCleaning(
                    capturedType,
                    current,
                    next))
            {
                directOrderCosts?.Apply(capturedCharacter, DeferCleaningActionTag);
            }
            Refresh();
        });

        CharacterDirectOrderCostPreview preview = GetDirectOrderCostPreview(
            worker.Character,
            workTypeId,
            priority,
            priority.Next());
        TMP_Text label = AddCellText(
            cell.transform,
            GetPriorityLabel(priority, preview),
            TextAlignmentOptions.Center,
            true);
        label.enableAutoSizing = preview.HasCost;
        label.fontSize = preview.HasCost ? 18f : 30f;
        label.fontSizeMin = preview.HasCost ? 9f : 30f;
        label.fontSizeMax = preview.HasCost ? 18f : 30f;
        label.fontStyle = FontStyles.Bold;
        label.color = priority == WorkPriorityLevel.Off
            ? DungeonUiTheme.TextSecondary
            : Color.white;

        RequireUiFactory().AddShadow(label.gameObject, new Color(0f, 0f, 0f, 0.9f), new Vector2(1.2f, -1.2f));

    }

    private GameObject CreateCellObject(string name, Transform parent, float width, float height)
    {
        GameObject cell = RequireUiFactory().CreateUiObject(name, parent);
        RectTransform rect = cell.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);

        RequireUiFactory().AddLayoutElement(cell, width, height);
        return cell;
    }

    private TMP_Text AddCellText(Transform parent, string text, TextAlignmentOptions alignment, bool allowAutoSize)
    {
        GameObject textObject = RequireUiFactory().CreateUiObject("Text", parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(3f, 2f);
        rect.offsetMax = new Vector2(-3f, -2f);

        TMP_Text label = RequireUiFactory().AddText(textObject);
        label.text = text;
        label.alignment = alignment;
        label.textWrappingMode = allowAutoSize ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.enableAutoSizing = allowAutoSize;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 32f;
        label.raycastTarget = false;
        return label;
    }

    private int CalculateWorkerHash()
    {
        return RequireModelBuilder().CalculateWorkerHash();
    }

    private static string GetWorkTypeLabel(WorkTypeDefinition definition)
    {
        return definition?.DisplayName ?? string.Empty;
    }

    private CharacterDirectOrderCostPreview GetDirectOrderCostPreview(
        CharacterActor actor,
        WorkTypeId workTypeId,
        WorkPriorityLevel current,
        WorkPriorityLevel next)
    {
        return directOrderCosts != null
            && StaffWorkPriorityIdentityOrderPolicy.IsDeferredCleaning(
                workTypeId,
                current,
                next)
            ? directOrderCosts.Preview(actor, DeferCleaningActionTag)
            : default;
    }

    private static string GetPriorityLabel(
        WorkPriorityLevel priority,
        CharacterDirectOrderCostPreview preview)
    {
        string priorityLabel = priority switch
        {
            WorkPriorityLevel.Priority1 => "1",
            WorkPriorityLevel.Priority2 => "2",
            WorkPriorityLevel.Priority3 => "3",
            _ => "X"
        };
        if (!preview.HasCost)
        {
            return priorityLabel;
        }

        return $"{priorityLabel}\n예상 기분 {preview.MoodDelta:+0.#;-0.#;0} / 스트레스 {preview.StressDelta:+0.#;-0.#;0}";
    }

    private static Color GetPriorityColor(WorkPriorityLevel priority, bool selected)
    {
        Color baseColor = priority switch
        {
            WorkPriorityLevel.Priority1 => DungeonUiTheme.Good,
            WorkPriorityLevel.Priority2 => DungeonUiTheme.Warning,
            WorkPriorityLevel.Priority3 => DungeonUiTheme.SurfaceRaised,
            _ => DungeonUiTheme.SurfaceMuted
        };

        return selected
            ? Color.Lerp(baseColor, DungeonUiTheme.TextPrimary, 0.2f)
            : baseColor;
    }

    private static string GetWorkerStatus(StaffWorkPriorityRowModel worker)
    {
        string status;
        if (worker.Character.Lifecycle != null
            && worker.Character.Lifecycle.CurrentState == CharacterLifecycleState.OnExpedition)
        {
            status = "원정";
        }
        else if (worker.Work.IsOffDuty)
        {
            status = "비번";
        }
        else
        {
            status = worker.Work.isWorking ? "작업중" : "대기";
        }

        string aiSummary = worker.Character.Brain != null
            ? worker.Character.Brain.GetDebugSummary(2)
            : string.Empty;
        if (string.IsNullOrWhiteSpace(aiSummary))
        {
            return status;
        }

        return $"{status}\n{aiSummary}";
    }

    private void ClearSpawnedObjects()
    {
        if (tableRoot != null)
        {
            for (int i = tableRoot.childCount - 1; i >= 0; i--)
            {
                RequireUiFactory().Release(tableRoot.GetChild(i).gameObject);
            }

            spawnedObjects.Clear();
            return;
        }

        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                RequireUiFactory().Release(obj);
            }
        }

        spawnedObjects.Clear();
    }

    private void OnEnable()
    {
        SubscribeToInfoFeed();
        nextAutoRefreshAt = 0f;
    }

    private void OnDisable()
    {
        infoFeedSubscription?.Dispose();
        infoFeedSubscription = null;
    }

    private void SubscribeToInfoFeed()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        infoFeedSubscription ??=
            gameEventBus.Subscribe<InfoFeedEvent>(OnTriggerEvent);
    }

    private IStaffWorkPriorityPanelModelBuilder RequireModelBuilder()
    {
        return modelBuilder
            ?? throw new InvalidOperationException(
                $"{nameof(StaffWorkPriorityPanel)} requires {nameof(IStaffWorkPriorityPanelModelBuilder)} injection before use.");
    }

    private IStaffWorkPriorityPanelUiFactory RequireUiFactory()
    {
        return uiFactory
            ?? throw new InvalidOperationException(
                $"{nameof(StaffWorkPriorityPanel)} requires {nameof(IStaffWorkPriorityPanelUiFactory)} injection before use.");
    }

    private StaffManagementSurfacePanel RequireManagementSurface()
    {
        if (managementSurface != null)
        {
            return managementSurface;
        }

        bool domainAvailable = staffDiscontentRuntime != null
            && characterWorldQuery != null
            && buildingWorldQuery != null
            && playerStaffCommands != null
            && moodImpulseQuery != null
            && gameEventBus != null;
        StaffManagementDomainContext domain = domainAvailable
            ? StaffManagementDomainContext.Create(
                staffDiscontentRuntime,
                characterWorldQuery,
                buildingWorldQuery,
                playerStaffCommands,
                moodImpulseQuery,
                gameEventBus)
            : StaffManagementDomainContext.Unavailable();
        managementSurface = new StaffManagementSurfacePanel(
            this,
            this,
            RequireModelBuilder(),
            domain,
            apologyCommands,
            ritualFastingQuery,
            ritualFastingCommands,
            manaQuery,
            arcaneOverchargeCommands,
            combatEquipment,
            performance,
            () => selectedCharacter,
            actor => selectedCharacter = actor);
        return managementSurface;
    }

    RectTransform IStaffManagementSurfaceQuery.ContentRoot => contentRoot;
    Transform IStaffManagementSurfaceQuery.TableRoot => tableRoot;
    TMP_Text IStaffManagementSurfaceQuery.TitleText => titleText;
    IStaffWorkPriorityPanelUiFactory IStaffManagementSurfaceQuery.UiFactory =>
        RequireUiFactory();

    void IStaffManagementSurfaceCommand.SetVisibleCounts(
        int workerCount,
        int cellCount)
    {
        VisibleWorkerCount = Mathf.Max(0, workerCount);
        VisibleCellCount = Mathf.Max(0, cellCount);
    }

    void IStaffManagementSurfaceCommand.RequestRefresh() => Refresh();
}

public static class StaffWorkPriorityIdentityOrderPolicy
{
    public static bool IsDeferredCleaning(
        WorkTypeId workTypeId,
        WorkPriorityLevel current,
        WorkPriorityLevel next)
    {
        return workTypeId == BuiltInWorkTypeIds.Clean
            && current != WorkPriorityLevel.Off
            && next != WorkPriorityLevel.Priority1;
    }
}
