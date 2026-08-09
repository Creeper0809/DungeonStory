using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IApparelBuildingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

/// <summary>
/// Player-facing apparel workflow. The default surface is definition selection
/// and a single clear craft action; sizing, openings, material policy, worker
/// policy and quality repetition live behind progressive disclosure.
/// </summary>
public sealed class ApparelBuildingPanelPresenter : IApparelBuildingPanelPresenter
{
    private static readonly ApparelUseTag[] CategoryOrder =
    {
        ApparelUseTag.Daily,
        ApparelUseTag.Underwear,
        ApparelUseTag.Cold,
        ApparelUseTag.Heat,
        ApparelUseTag.Medical,
        ApparelUseTag.Formal
    };

    private readonly IApparelWorkOrderCommand commands;
    private readonly IApparelWorkOrderQuery orders;
    private readonly IApparelDefinitionCatalog apparel;
    private readonly IWorldItemStackRuntime items;
    private readonly IDungeonDebugModeService debugMode;
    private readonly Dictionary<string, ApparelCraftUiSettings> settingsByFacility =
        new(StringComparer.Ordinal);

    public ApparelBuildingPanelPresenter(
        IApparelWorkOrderCommand commands,
        IApparelWorkOrderQuery orders,
        IApparelDefinitionCatalog apparel,
        IWorldItemStackRuntime items,
        IDungeonDebugModeService debugMode)
    {
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.debugMode = debugMode ?? throw new ArgumentNullException(nameof(debugMode));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new();
        ResearchFacilityCommandKind command = building?.BuildingData?
            .ResearchFacilityCommand ?? ResearchFacilityCommandKind.None;
        if (parent == null
            || ResearchFacilityCommandConsumerRegistry.DomainOwner(command)
                != "apparel-textile")
        {
            return created;
        }

        string facilityId = building.RequirePersistentInstanceId().Value;
        ApparelWorkOrderSaveData[] active = orders.Orders
            .Where(value => value != null
                && value.state is not ApparelWorkOrderState.Completed
                    and not ApparelWorkOrderState.Failed
                && string.Equals(
                    value.facilityInstanceId,
                    facilityId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.orderId, StringComparer.Ordinal)
            .ToArray();

        AddText(parent, "의복·섬유 작업", font, 20f,
            DungeonUiTheme.TextPrimary, 32f, created, "ApparelSectionTitle");
        AddText(
            parent,
            active.Length == 0
                ? "진행 중인 주문이 없습니다."
                : $"진행 중 {active.Length}건",
            font,
            13f,
            active.Length == 0
                ? DungeonUiTheme.TextSecondary
                : DungeonUiTheme.Accent,
            26f,
            created,
            "ApparelQueueSummary");
        RenderActiveOrders(parent, active, font, created);

        switch (command)
        {
            case ResearchFacilityCommandKind.ApparelTailoring:
                RenderTailoring(parent, facilityId, font, showFeedback, refresh, created);
                break;
            case ResearchFacilityCommandKind.HandLaundry:
            case ResearchFacilityCommandKind.PoweredLaundry:
                RenderBatchAction(
                    parent,
                    command == ResearchFacilityCommandKind.PoweredLaundry
                        ? "세탁·건조 시작"
                        : "손세탁 시작",
                    "오염된 의복을 최대 12벌까지 한 배치로 처리합니다.",
                    font,
                    created,
                    () => CreateLaundry(
                        command == ResearchFacilityCommandKind.PoweredLaundry,
                        showFeedback,
                        refresh));
                break;
            case ResearchFacilityCommandKind.IndoorDrying:
                RenderBatchAction(parent, "실내 건조 시작",
                    "젖은 의복을 최대 12벌까지 건조합니다.", font, created,
                    () => CreateDrying(showFeedback, refresh));
                break;
            case ResearchFacilityCommandKind.ApparelRepair:
                RenderBatchAction(parent, "가장 손상된 의복 수선",
                    "내구도 20% 이상인 의복 중 가장 손상된 한 벌을 수선합니다.",
                    font, created, () => CreateRepair(showFeedback, refresh));
                break;
            case ResearchFacilityCommandKind.DressingChange:
                RenderOpeningActions(parent, font, showFeedback, refresh, created);
                break;
        }
        return created;
    }

    private void RenderActiveOrders(
        Transform parent,
        IReadOnlyList<ApparelWorkOrderSaveData> active,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        foreach (ApparelWorkOrderSaveData order in active.Take(3))
        {
            float ratio = order.requiredWork > 0f
                ? Mathf.Clamp01(order.completedWork / order.requiredWork)
                : 0f;
            string label = $"{OrderKind(order.kind)} · "
                + $"{OrderState(order.state)} · "
                + $"{ratio:P0}";
            label = GameplayUiPresentationText.WithDebug(
                label,
                debugMode.IsDeveloperModeEnabled,
                $"order={order.orderId} state={order.state} work={order.completedWork:0.##}/{order.requiredWork:0.##}");
            AddText(parent, label, font, 13f,
                order.state is ApparelWorkOrderState.WaitingForEligibleWorker
                    or ApparelWorkOrderState.TargetCurrentlyUnreachable
                    ? DungeonUiTheme.Warning
                    : DungeonUiTheme.TextPrimary,
                debugMode.IsDeveloperModeEnabled ? 44f : 26f,
                created,
                "ApparelOrderStatus");
        }
        if (active.Count > 3)
        {
            AddText(parent, $"외 {active.Count - 3}건", font, 12f,
                DungeonUiTheme.TextSecondary, 22f, created, "ApparelQueueOverflow");
        }
    }

    private void RenderTailoring(
        Transform parent,
        string facilityId,
        TMP_FontAsset font,
        Action<string> feedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        ApparelCraftUiSettings settings = GetSettings(facilityId);
        AddText(parent, "제작할 종류", font, 16f,
            DungeonUiTheme.TextPrimary, 28f, created, "ApparelCategoryTitle");
        foreach (ApparelUseTag category in CategoryOrder)
        {
            ApparelUseTag captured = category;
            AddAction(
                parent,
                CategoryName(category),
                font,
                created,
                () =>
                {
                    settings.Category = captured;
                    refresh?.Invoke();
                },
                selected: settings.Category == category,
                objectName: $"ApparelCategory_{category}");
        }

        ApparelDefinitionSO[] definitions = apparel.Definitions
            .Where(value => value != null && (value.UseTags & settings.Category) != 0)
            .OrderBy(value => value.DisplayName, StringComparer.Ordinal)
            .ToArray();
        if (definitions.Length == 0)
        {
            AddText(parent, "이 분류에 제작 가능한 의복이 없습니다.", font, 13f,
                DungeonUiTheme.Warning, 28f, created, "ApparelEmptyCategory");
        }
        foreach (ApparelDefinitionSO definition in definitions)
        {
            AddAction(
                parent,
                $"{definition.DisplayName} 제작",
                font,
                created,
                () => QueueCraft(definition, settings, feedback, refresh),
                objectName: $"ApparelCraft_{Sanitize(definition.ApparelId)}");
        }

        AddAction(
            parent,
            settings.AdvancedOpen ? "고급 설정 접기 ▲" : "고급 설정 펼치기 ▼",
            font,
            created,
            () =>
            {
                settings.AdvancedOpen = !settings.AdvancedOpen;
                refresh?.Invoke();
            },
            selected: settings.AdvancedOpen,
            objectName: "ApparelAdvancedToggle");
        if (settings.AdvancedOpen)
        {
            RenderAdvancedSettings(parent, settings, font, refresh, created);
        }
    }

    private void RenderAdvancedSettings(
        Transform parent,
        ApparelCraftUiSettings settings,
        TMP_FontAsset font,
        Action refresh,
        ICollection<GameObject> created)
    {
        AddText(
            parent,
            $"크기 {SizeName(settings.Size)} · 개조 {ModificationName(settings.Modifications)}\n"
            + $"최소 품질 {GameplayUiPresentationText.Quality(settings.MinimumQuality)} · "
            + GameplayUiPresentationText.RepeatMode(settings.RepeatMode, settings.MaximumAttempts)
            + $"\n작업자 {GameplayUiPresentationText.WorkerPolicy(settings.WorkerPolicy)}\n"
            + GameplayUiPresentationText.RejectedOutput(settings.Disposition),
            font,
            13f,
            DungeonUiTheme.TextSecondary,
            74f,
            created,
            "ApparelAdvancedSummary");

        AddChoice(parent, "크기: 소형", settings.Size == ApparelSizeClass.Small,
            () => settings.Size = ApparelSizeClass.Small);
        AddChoice(parent, "크기: 중형", settings.Size == ApparelSizeClass.Medium,
            () => settings.Size = ApparelSizeClass.Medium);
        AddChoice(parent, "크기: 대형", settings.Size == ApparelSizeClass.Large,
            () => settings.Size = ApparelSizeClass.Large);
        AddChoice(parent, "꼬리 구멍", HasModification(settings, ApparelModificationKind.TailOpening),
            () => ToggleModification(settings, ApparelModificationKind.TailOpening));
        AddChoice(parent, "날개 절개", HasModification(settings, ApparelModificationKind.WingSlits),
            () => ToggleModification(settings, ApparelModificationKind.WingSlits));
        AddChoice(parent, "뿔 여유", HasModification(settings, ApparelModificationKind.HornClearance),
            () => ToggleModification(settings, ApparelModificationKind.HornClearance));
        AddChoice(parent, "재료: 최저 비용", settings.MaterialPolicy == ApparelMaterialSelectionPolicy.LowestCost,
            () => settings.MaterialPolicy = ApparelMaterialSelectionPolicy.LowestCost);
        AddChoice(parent, "재료: 최고 보온", settings.MaterialPolicy == ApparelMaterialSelectionPolicy.HighestWarmth,
            () => settings.MaterialPolicy = ApparelMaterialSelectionPolicy.HighestWarmth);
        AddChoice(parent, "재료: 최저 중량", settings.MaterialPolicy == ApparelMaterialSelectionPolicy.LowestWeight,
            () => settings.MaterialPolicy = ApparelMaterialSelectionPolicy.LowestWeight);
        AddChoice(parent, "작업자: 속도 우선",
            IsAnyone(settings.WorkerPolicy, WorkerCandidateSortMode.Fastest),
            () => settings.WorkerPolicy = WorkerSelectionPolicySaveData.Anyone(WorkerCandidateSortMode.Fastest));
        AddChoice(parent, "작업자: 예상 품질 우선",
            IsAnyone(settings.WorkerPolicy, WorkerCandidateSortMode.BestExpectedQuality),
            () => settings.WorkerPolicy = WorkerSelectionPolicySaveData.Anyone(WorkerCandidateSortMode.BestExpectedQuality));
        AddChoice(parent, "작업자: 민첩 7 이상",
            settings.WorkerPolicy?.mode == WorkerSelectionMode.RuleSet,
            () => settings.WorkerPolicy = DexterityPolicy());
        AddAction(parent,
            $"최소 품질 낮추기 · {GameplayUiPresentationText.Quality(settings.MinimumQuality)}",
            font, created, () =>
            {
                settings.MinimumQuality = (CraftsmanshipQualityTier)Mathf.Max(
                    0, (int)settings.MinimumQuality - 1);
                refresh?.Invoke();
            }, objectName: "ApparelQualityDown");
        AddAction(parent,
            $"최소 품질 높이기 · {GameplayUiPresentationText.Quality(settings.MinimumQuality)}",
            font, created, () =>
            {
                settings.MinimumQuality = (CraftsmanshipQualityTier)Mathf.Min(
                    6, (int)settings.MinimumQuality + 1);
                refresh?.Invoke();
            }, objectName: "ApparelQualityUp");
        AddChoice(parent, "불합격품 자동 분해",
            settings.Disposition == RejectedOutputDisposition.AutoDismantle,
            () => settings.Disposition = RejectedOutputDisposition.AutoDismantle);
        AddChoice(parent, "불합격품 보관",
            settings.Disposition == RejectedOutputDisposition.KeepInStorage,
            () => settings.Disposition = RejectedOutputDisposition.KeepInStorage);
        AddChoice(parent, "불합격품 판매 대기",
            settings.Disposition == RejectedOutputDisposition.MarkForSale,
            () => settings.Disposition = RejectedOutputDisposition.MarkForSale);
        AddChoice(parent, "반복: 안전 한도",
            settings.RepeatMode == QualityRepeatLimitMode.SafeLimits,
            () => settings.RepeatMode = QualityRepeatLimitMode.SafeLimits);
        AddChoice(parent, "반복: 목표 품질까지",
            settings.RepeatMode == QualityRepeatLimitMode.UnlimitedUntilSuccess,
            () => settings.RepeatMode = QualityRepeatLimitMode.UnlimitedUntilSuccess);

        void AddChoice(Transform target, string label, bool selected, Action change)
        {
            AddAction(target, label, font, created, () =>
            {
                change();
                refresh?.Invoke();
            }, selected: selected, objectName: "ApparelAdvancedChoice");
        }
    }

    private void QueueCraft(
        ApparelDefinitionSO definition,
        ApparelCraftUiSettings settings,
        Action<string> feedback,
        Action refresh)
    {
        bool success = commands.CreateCraft(
            new ApparelCraftOrderRequest(
                definition.ApparelId,
                settings.Size,
                settings.Modifications,
                settings.MaterialPolicy,
                minimumCraftsmanshipQuality: settings.MinimumQuality,
                workerPolicy: settings.WorkerPolicy,
                rejectedDisposition: settings.Disposition,
                repeatLimitMode: settings.RepeatMode,
                maximumAttempts: settings.MaximumAttempts,
                workBudget: settings.WorkBudget,
                requiredAcceptedCount: settings.RequiredCount),
            out string orderId,
            out DomainFailure failure);
        Complete(success, orderId, failure, feedback, refresh);
    }

    private ApparelCraftUiSettings GetSettings(string facilityId)
    {
        string key = facilityId?.Trim() ?? string.Empty;
        if (!settingsByFacility.TryGetValue(key, out ApparelCraftUiSettings value))
        {
            value = new ApparelCraftUiSettings();
            settingsByFacility.Add(key, value);
        }
        return value;
    }

    private void CreateLaundry(bool powered, Action<string> feedback, Action refresh)
    {
        ItemInstanceId[] targets = FindApparelStates()
            .Where(value => value.State.contamination > 0f)
            .Take(12)
            .Select(value => (ItemInstanceId)value.Stack.ItemInstanceId)
            .ToArray();
        bool success = commands.CreateLaundry(
            targets, powered, out string orderId, out DomainFailure failure);
        Complete(success, orderId, failure, feedback, refresh);
    }

    private void CreateDrying(Action<string> feedback, Action refresh)
    {
        ItemInstanceId[] targets = FindApparelStates()
            .Where(value => value.State.moisture >= 20f
                && value.State.contamination <= 0f)
            .Take(12)
            .Select(value => (ItemInstanceId)value.Stack.ItemInstanceId)
            .ToArray();
        bool success = commands.CreateDrying(
            targets, out string orderId, out DomainFailure failure);
        Complete(success, orderId, failure, feedback, refresh);
    }

    private void CreateRepair(Action<string> feedback, Action refresh)
    {
        (WorldItemStackSnapshot Stack, ApparelInstanceState State) target =
            FindApparelStates()
                .Where(value => value.State.durability >= 20f
                    && value.State.durability < 100f)
                .OrderBy(value => value.State.durability)
                .FirstOrDefault();
        if (target.Stack == null)
        {
            Complete(false, string.Empty,
                new DomainFailure(FailureCode.ApparelWorkOrderInvalid),
                feedback, refresh);
            return;
        }
        bool success = commands.CreateRepair(
            (ItemInstanceId)target.Stack.ItemInstanceId,
            out string orderId,
            out DomainFailure failure);
        Complete(success, orderId, failure, feedback, refresh);
    }

    private void RenderOpeningActions(
        Transform parent,
        TMP_FontAsset font,
        Action<string> feedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        bool any = false;
        foreach (ApparelModificationKind opening in new[]
                 {
                     ApparelModificationKind.TailOpening,
                     ApparelModificationKind.WingSlits,
                     ApparelModificationKind.HornClearance
                 })
        {
            var target = FindApparelStates().FirstOrDefault(value =>
                (value.State.modifications & opening) != 0);
            if (target.Stack == null)
            {
                continue;
            }
            any = true;
            bool closed = (target.State.closedOpenings & opening) != 0;
            AddAction(parent,
                $"{ModificationName(opening)} {(closed ? "다시 열기" : "막기")}",
                font, created, () =>
                {
                    ApparelModificationKind next = closed
                        ? target.State.closedOpenings & ~opening
                        : target.State.closedOpenings | opening;
                    bool success = commands.CreateAlteration(
                        (ItemInstanceId)target.Stack.ItemInstanceId,
                        target.State.size,
                        next,
                        true,
                        out string orderId,
                        out DomainFailure failure);
                    Complete(success, orderId, failure, feedback, refresh);
                });
        }
        if (!any)
        {
            AddText(parent, "여닫을 수 있는 개조 의복이 없습니다.", font, 13f,
                DungeonUiTheme.TextSecondary, 28f, created, "ApparelOpeningEmpty");
        }
    }

    private IEnumerable<(WorldItemStackSnapshot Stack, ApparelInstanceState State)>
        FindApparelStates()
    {
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
        {
            if (stack == null
                || stack.IsReserved
                || string.IsNullOrWhiteSpace(stack.ItemInstanceId)
                || stack.DestinationId.StartsWith(
                    CharacterApparelAggregate.EquippedDestinationPrefix,
                    StringComparison.Ordinal)
                || !apparel.TryGetByItemId(stack.ItemId, out _)
                || !ApparelItemStateCodec.TryRead(
                    stack.Components,
                    out ApparelInstanceState state))
            {
                continue;
            }
            yield return (stack, state);
        }
    }

    private void Complete(
        bool success,
        string orderId,
        DomainFailure failure,
        Action<string> feedback,
        Action refresh)
    {
        feedback?.Invoke(success
            ? GameplayUiPresentationText.OrderCreated(
                orderId,
                debugMode.IsDeveloperModeEnabled)
            : GameplayUiPresentationText.FailureFallback(
                failure,
                debugMode.IsDeveloperModeEnabled));
        refresh?.Invoke();
    }

    private static void RenderBatchAction(
        Transform parent,
        string actionLabel,
        string description,
        TMP_FontAsset font,
        ICollection<GameObject> created,
        Action action)
    {
        AddText(parent, description, font, 13f, DungeonUiTheme.TextSecondary,
            34f, created, "ApparelBatchDescription");
        AddAction(parent, actionLabel, font, created, action,
            selected: true, objectName: "ApparelPrimaryAction");
    }

    private static string OrderKind(ApparelWorkOrderKind kind) => kind switch
    {
        ApparelWorkOrderKind.Craft => "제작",
        ApparelWorkOrderKind.Laundry => "세탁",
        ApparelWorkOrderKind.Drying => "건조",
        ApparelWorkOrderKind.Repair => "수선",
        ApparelWorkOrderKind.Alteration => "개조",
        _ => "의복 작업"
    };

    private static string OrderState(ApparelWorkOrderState value) => value switch
    {
        ApparelWorkOrderState.NeedsRevalidation => "복원 후 조건 확인 중",
        ApparelWorkOrderState.WaitingForMaterials => "재료 운반 대기",
        ApparelWorkOrderState.Ready => "작업 준비 완료",
        ApparelWorkOrderState.InProgress => "작업 중",
        ApparelWorkOrderState.WaitingForOutputSpace => "출력 공간 대기",
        ApparelWorkOrderState.WaitingForEligibleWorker => "조건에 맞는 작업자 대기",
        ApparelWorkOrderState.TargetCurrentlyUnreachable => "현재 조건으로 목표 달성 불가",
        ApparelWorkOrderState.Completed => "완료",
        ApparelWorkOrderState.Failed => "실패",
        _ => "대기"
    };

    private static string CategoryName(ApparelUseTag category) => category switch
    {
        ApparelUseTag.Underwear => "속옷·기초복",
        ApparelUseTag.Cold => "한랭 작업복",
        ApparelUseTag.Heat => "고온 작업복",
        ApparelUseTag.Medical => "의료복",
        ApparelUseTag.Formal => "정장·문화복",
        _ => "일상복"
    };

    private static string SizeName(ApparelSizeClass size) => size switch
    {
        ApparelSizeClass.Small => "소형",
        ApparelSizeClass.Large => "대형",
        _ => "중형"
    };

    private static string ModificationName(ApparelModificationKind value)
    {
        if (value == ApparelModificationKind.None)
        {
            return "없음";
        }
        List<string> labels = new();
        if ((value & ApparelModificationKind.TailOpening) != 0) labels.Add("꼬리 구멍");
        if ((value & ApparelModificationKind.WingSlits) != 0) labels.Add("날개 절개");
        if ((value & ApparelModificationKind.HornClearance) != 0) labels.Add("뿔 여유");
        return labels.Count == 0 ? "없음" : string.Join(", ", labels);
    }

    private static bool HasModification(
        ApparelCraftUiSettings settings,
        ApparelModificationKind value) =>
        (settings.Modifications & value) != 0;

    private static void ToggleModification(
        ApparelCraftUiSettings settings,
        ApparelModificationKind value)
    {
        settings.Modifications = HasModification(settings, value)
            ? settings.Modifications & ~value
            : settings.Modifications | value;
    }

    private static bool IsAnyone(
        WorkerSelectionPolicySaveData policy,
        WorkerCandidateSortMode sort) =>
        policy?.mode == WorkerSelectionMode.Anyone && policy.sortMode == sort;

    private static WorkerSelectionPolicySaveData DexterityPolicy() => new()
    {
        mode = WorkerSelectionMode.RuleSet,
        matchMode = WorkerRequirementMatchMode.All,
        sortMode = WorkerCandidateSortMode.BestExpectedQuality,
        statRequirements = new List<WorkerStatRequirementSaveData>
        {
            new()
            {
                statType = (int)CharacterStatType.Dexterity,
                minimumValue = 7
            }
        }
    };

    private static string Sanitize(string value) => new(
        (value ?? "unknown")
            .Select(character => char.IsLetterOrDigit(character) ? character : '_')
            .ToArray());

    private sealed class ApparelCraftUiSettings
    {
        public ApparelUseTag Category = ApparelUseTag.Daily;
        public ApparelSizeClass Size = ApparelSizeClass.Medium;
        public ApparelModificationKind Modifications = ApparelModificationKind.None;
        public ApparelMaterialSelectionPolicy MaterialPolicy =
            ApparelMaterialSelectionPolicy.LowestCost;
        public CraftsmanshipQualityTier MinimumQuality = CraftsmanshipQualityTier.Normal;
        public RejectedOutputDisposition Disposition = RejectedOutputDisposition.AutoDismantle;
        public QualityRepeatLimitMode RepeatMode = QualityRepeatLimitMode.SafeLimits;
        public int MaximumAttempts = 10;
        public float WorkBudget;
        public int RequiredCount = 1;
        public bool AdvancedOpen;
        public WorkerSelectionPolicySaveData WorkerPolicy =
            WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality);
    }

    private static void AddAction(
        Transform parent,
        string label,
        TMP_FontAsset font,
        ICollection<GameObject> created,
        Action action,
        bool selected = false,
        string objectName = "ApparelAction")
    {
        GameObject root = new(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 38f;
        Button button = root.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected);
        button.onClick.AddListener(() => action?.Invoke());

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 2f);
        rect.offsetMax = new Vector2(-8f, -2f);
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 13f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 13f;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        created.Add(root);
    }

    private static void AddText(
        Transform parent,
        string value,
        TMP_FontAsset font,
        float size,
        Color color,
        float height,
        ICollection<GameObject> created,
        string objectName)
    {
        GameObject root = new(
            objectName,
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = root.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        created.Add(root);
    }
}
