using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class IndustrialPowerFluidContext
{
    public IndustrialPowerFluidContext(
        IPowerInfrastructureQuery power,
        IPowerInfrastructureCommand powerCommands,
        IFluidInfrastructureQuery water,
        IFluidInfrastructureCommand plumbing)
    {
        Power = power ?? throw new ArgumentNullException(nameof(power));
        PowerCommands = powerCommands
            ?? throw new ArgumentNullException(nameof(powerCommands));
        Water = water ?? throw new ArgumentNullException(nameof(water));
        Plumbing = plumbing
            ?? throw new ArgumentNullException(nameof(plumbing));
    }

    public IPowerInfrastructureQuery Power { get; }
    public IPowerInfrastructureCommand PowerCommands { get; }
    public IFluidInfrastructureQuery Water { get; }
    public IFluidInfrastructureCommand Plumbing { get; }
}

public sealed class IndustrialTransportAutomationContext
{
    public IndustrialTransportAutomationContext(
        IConveyorInfrastructureQuery conveyorQuery,
        IConveyorInfrastructureCommand conveyorCommands,
        IAutomationInfrastructureQuery automationQuery,
        IAutomationInfrastructureCommand automationCommands)
    {
        ConveyorQuery = conveyorQuery
            ?? throw new ArgumentNullException(nameof(conveyorQuery));
        ConveyorCommands = conveyorCommands
            ?? throw new ArgumentNullException(nameof(conveyorCommands));
        AutomationQuery = automationQuery
            ?? throw new ArgumentNullException(nameof(automationQuery));
        AutomationCommands = automationCommands
            ?? throw new ArgumentNullException(nameof(automationCommands));
    }

    public IConveyorInfrastructureQuery ConveyorQuery { get; }
    public IConveyorInfrastructureCommand ConveyorCommands { get; }
    public IAutomationInfrastructureQuery AutomationQuery { get; }
    public IAutomationInfrastructureCommand AutomationCommands { get; }
}

public sealed class IndustrialPresentationContext
{
    public IndustrialPresentationContext(
        IBuildingWorldQuery buildings,
        IIndustrialInfrastructureOverlayService overlays,
        IDomainFailureLocalizer failureLocalizer)
    {
        Buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        Overlays = overlays ?? throw new ArgumentNullException(nameof(overlays));
        FailureLocalizer = failureLocalizer
            ?? throw new ArgumentNullException(nameof(failureLocalizer));
    }

    public IBuildingWorldQuery Buildings { get; }
    public IIndustrialInfrastructureOverlayService Overlays { get; }
    public IDomainFailureLocalizer FailureLocalizer { get; }
}

public sealed class IndustrialFeatureSurfacePresenter :
    IFeatureSurfaceTabPresenter
{
    private const float CardHeight = 96f;
    private const int MaxPowerNodeCards = 40;

    private readonly IPowerInfrastructureQuery power;
    private readonly IPowerInfrastructureCommand powerCommands;
    private readonly IFluidInfrastructureQuery water;
    private readonly IFluidInfrastructureCommand plumbing;
    private readonly IConveyorInfrastructureQuery conveyorQuery;
    private readonly IConveyorInfrastructureCommand conveyorCommands;
    private readonly IAutomationInfrastructureQuery automationQuery;
    private readonly IAutomationInfrastructureCommand automationCommands;
    private readonly IBuildingWorldQuery buildings;
    private readonly IIndustrialInfrastructureOverlayService overlays;
    private readonly IDomainFailureLocalizer failureLocalizer;
    private readonly Dictionary<string, BuildableObject> buildingsByNodeId =
        new Dictionary<string, BuildableObject>(StringComparer.Ordinal);
    private int indexedBuildingVersion = int.MinValue;
    private readonly HashSet<string> expandedConveyorDestinations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> conveyorChoicePages = new(StringComparer.Ordinal);
    private const int ConveyorChoicesPerPage = 8;

    public IndustrialFeatureSurfacePresenter(
        IndustrialPowerFluidContext utilities,
        IndustrialTransportAutomationContext transport,
        IndustrialPresentationContext presentation)
    {
        utilities = utilities ?? throw new ArgumentNullException(nameof(utilities));
        transport = transport ?? throw new ArgumentNullException(nameof(transport));
        presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        power = utilities.Power;
        powerCommands = utilities.PowerCommands;
        water = utilities.Water;
        plumbing = utilities.Plumbing;
        conveyorQuery = transport.ConveyorQuery;
        conveyorCommands = transport.ConveyorCommands;
        automationQuery = transport.AutomationQuery;
        automationCommands = transport.AutomationCommands;
        buildings = presentation.Buildings;
        overlays = presentation.Overlays;
        failureLocalizer = presentation.FailureLocalizer;
    }

    public TabId Id => TabId.Industry;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        EnsureBuildingIndex();
        AddOverlayControls(view);
        AddPower(view);
        AddFluids(view);
        AddConveyors(view);
        AddAutomation(view);
    }

    private void AddOverlayControls(IFeatureSurfaceView view)
    {
        view.AddSection(
            "기반 시설 오버레이",
            "전력, 상수, 하수와 컨베이어 망을 독립적으로 표시합니다.");
        foreach (IndustrialOverlayKind kind in
                 Enum.GetValues(typeof(IndustrialOverlayKind)))
        {
            IndustrialOverlayKind captured = kind;
            bool visible = overlays.IsVisible(captured);
            view.AddDataCard(
                $"IndustryOverlay_{captured}",
                FormatOverlay(captured),
                visible ? "월드에 표시 중" : "숨김",
                visible ? "끄기" : "켜기",
                () =>
                {
                    overlays.SetVisible(captured, !visible);
                    view.RequestRefresh();
                },
                76f);
        }
    }

    private void AddPower(IFeatureSurfaceView view)
    {
        IReadOnlyList<PowerNetworkSnapshot> networks = power.Networks;
        view.AddSection(
            "전력",
            networks.Count == 0
                ? "연결된 전력망이 없습니다."
                : $"전력망 {networks.Count}개");
        foreach (PowerNetworkSnapshot network in networks)
        {
            string detail =
                $"생산 {network.ProductionPerSecond:0.0}/s · 수요 {network.DemandPerSecond:0.0}/s · 공급 {network.SuppliedPerSecond:0.0}/s\n"
                + $"축전 {network.StoredPower:0.0}/{network.StorageCapacity:0.0}"
                + (network.Tripped ? " · 차단기 작동" : string.Empty);
            if (network.CapacitySourceOccurrenceInstanceId.Length > 0)
            {
                float reduction = (1f - network.AvailableSourceMultiplier)
                    * 100f;
                detail +=
                    $"\n가용 전원 {network.NominalAvailableSourcePerSecond:0.0}→{network.AvailableSourcePerSecond:0.0}/s"
                    + $" · {network.CapacitySourceDisplayName} -{reduction:0}%"
                    + (network.CapacitySourceRemainingDays > 0
                        ? $" · 남은 {network.CapacitySourceRemainingDays}일"
                        : " · 오늘 종료");
            }
            view.AddDataCard(
                $"IndustryPower_{network.NetworkId}",
                network.NetworkId,
                detail,
                "새로고침",
                view.RequestRefresh,
                CardHeight);
        }

        foreach (PowerNodeSnapshot node in networks.SelectMany(network => network.Nodes)
                     .Where(value => value.HasControllableConnection)
                     .OrderBy(value => value.BuildingId.Value, StringComparer.Ordinal))
        {
            PowerNodeSnapshot captured = node;
            BuildableObject building = FindBuilding(captured.BuildingId.Value);
            view.AddDataCard(
                $"IndustryPowerConnection_{captured.BuildingId.Value}",
                GetBuildingName(building, captured.BuildingId.Value),
                $"전력 {(captured.ConnectionEnabled ? "연결" : "차단")} · 최대 통과 {captured.MaximumThroughput:0.###}/s",
                captured.ConnectionEnabled ? "전력 차단" : "전력 연결",
                () =>
                {
                    view.ShowFeedback(FormatCommandResult(
                        powerCommands.SetConnectionEnabled(building, !captured.ConnectionEnabled)));
                    view.RequestRefresh();
                },
                82f);
        }

        PowerNodeSnapshot[] consumerNodes = networks
            .SelectMany(network => network.Nodes)
            .Where(node => node.DemandPerSecond > 0f)
            .OrderBy(node => node.Powered)
            .ThenByDescending(node => node.Fault)
            .ThenBy(node => node.Priority)
            .ThenBy(node => node.BuildingId.Value, StringComparer.Ordinal)
            .ToArray();
        foreach (PowerNodeSnapshot node in consumerNodes
                     .Take(MaxPowerNodeCards))
        {
            PowerNodeSnapshot captured = node;
            BuildableObject building = FindBuilding(captured.BuildingId.Value);
            view.AddDataCard(
                $"IndustryPowerPriority_{captured.BuildingId.Value}",
                $"우선순위 {FormatPriority(captured.Priority)}",
                $"{captured.BuildingId.Value} · 공급 {captured.SuppliedFraction * 100f:0}%"
                + (captured.Fault > 0f
                    ? $" · 고장 {captured.Fault:0}"
                    : string.Empty),
                "다음",
                building == null
                    ? () => view.ShowFeedback("대상 시설을 찾을 수 없습니다.")
                    : () =>
                    {
                        PowerPriority next = captured.Priority
                            == PowerPriority.Optional
                                ? PowerPriority.Critical
                                : captured.Priority + 1;
                        InfrastructureCommandResult result =
                            powerCommands.SetPriority(building, next);
                        view.ShowFeedback(FormatCommandResult(result));
                        view.RequestRefresh();
                    },
                82f);
        }
        if (consumerNodes.Length > MaxPowerNodeCards)
        {
            view.AddLabel(
                $"전력 소비 시설 {consumerNodes.Length}개 중 우선 확인 대상 {MaxPowerNodeCards}개를 표시합니다.",
                15f,
                32f);
        }

        foreach (PowerNodeSnapshot node in networks
                     .SelectMany(network => network.Nodes)
                     .Where(node => node.BreakerTripped)
                     .OrderBy(node => node.BuildingId.Value, StringComparer.Ordinal))
        {
            PowerNodeSnapshot captured = node;
            BuildableObject building = FindBuilding(captured.BuildingId.Value);
            view.AddDataCard(
                $"IndustryPowerBreaker_{captured.BuildingId.Value}",
                "차단기 작동",
                $"{GetBuildingName(building, captured.BuildingId.Value)} · 열 {captured.Heat:0}% · 고장 {captured.Fault:0}%",
                "차단기 복구",
                building == null
                    ? () => view.ShowFeedback(
                        "대상 차단기를 찾을 수 없습니다.")
                    : () =>
                    {
                        InfrastructureCommandResult result =
                            powerCommands.ResetBreaker(building);
                        view.ShowFeedback(FormatCommandResult(result));
                        view.RequestRefresh();
                    },
                82f);
        }
    }

    private void AddFluids(IFeatureSurfaceView view)
    {
        IReadOnlyList<FluidNetworkSnapshot> networks = water.Networks;
        view.AddSection(
            "상하수도",
            networks.Count == 0
                ? "연결된 상하수도망이 없습니다."
                : $"유체망 {networks.Count}개");
        foreach (FluidNetworkSnapshot network in networks)
        {
            string detail = network.Channel == UtilityChannel.CleanWater
                ? $"깨끗한 물 {network.CleanWater:0.0} · 비음용수 {network.UnsafeWater:0.0} · 오염수 {network.FoulWater:0.0} / {network.Capacity:0.0}"
                : $"폐수 {network.Wastewater:0.0}/{network.Capacity:0.0}";
            detail +=
                $"\n막힘 {network.Blockage:0}% · 누수 {network.Leak:0}%";
            if (network.HasOverflowRisk)
            {
                detail += " · 역류 위험";
            }

            view.AddDataCard(
                $"IndustryFluid_{network.NetworkId}_{network.Channel}",
                network.Channel == UtilityChannel.CleanWater
                    ? "상수도망"
                    : "하수도망",
                detail,
                "새로고침",
                view.RequestRefresh,
                CardHeight);
        }

        foreach (WaterTransferFacilitySnapshot transfer in
                 water.WaterTransfers)
        {
            WaterTransferFacilitySnapshot captured = transfer;
            BuildableObject building = FindBuilding(captured.BuildingId.Value);
            string detail =
                $"모드 {FormatWaterTransfer(captured.Mode)}"
                + $" · 진행 {captured.Progress01 * 100f:0}%"
                + (captured.Powered ? string.Empty : " · 정전");
            if (captured.Status.IsBlocked)
            {
                detail += $"\n대기: {failureLocalizer.Localize(captured.Status)}";
            }

            view.AddDataCard(
                $"IndustryWaterTransfer_{captured.BuildingId.Value}",
                "물통 충전소",
                detail,
                "모드 변경",
                building == null
                    ? () => view.ShowFeedback(
                        "대상 물통 충전소를 찾을 수 없습니다.")
                    : () =>
                    {
                        WaterContainerTransferMode next = captured.Mode
                            == WaterContainerTransferMode.FeedNetwork
                                ? WaterContainerTransferMode.Disabled
                                : captured.Mode + 1;
                        InfrastructureCommandResult result =
                            plumbing.SetWaterTransferMode(building, next);
                        view.ShowFeedback(FormatCommandResult(result));
                        view.RequestRefresh();
                    },
                CardHeight);
        }
    }

    private void AddConveyors(IFeatureSurfaceView view)
    {
        IReadOnlyList<ConveyorNetworkSnapshot> networks = conveyorQuery.Networks;
        int stalled = networks.Count(network =>
            network.State is ConveyorNetworkState.Stalled
                or ConveyorNetworkState.Deadlocked);
        view.AddSection(
            "컨베이어",
            $"연결망 {networks.Count}개 · 정체/교착 {stalled}개");
        foreach (ConveyorNetworkSnapshot network in networks)
        {
            string detail =
                $"운반물 {network.PayloadCount}개 · 상태 {FormatConveyorState(network.State)}";
            if (network.LongestStallSeconds > 0f)
            {
                detail +=
                    $"\n최장 정지 {network.LongestStallSeconds:0.0}초 · {FormatStallReason(network.PrimaryReason)}";
            }

            if (network.PlannedOverflowBuildingId.IsValid)
            {
                detail += $"\n예정 배출구 {network.PlannedOverflowBuildingId.Value}";
            }

            ConveyorPayloadSnapshot oldest = network.Payloads
                .OrderByDescending(payload => payload.StalledSeconds)
                .FirstOrDefault();
            view.AddDataCard(
                $"IndustryConveyor_{network.NetworkId}",
                network.NetworkId,
                detail,
                oldest != null && oldest.StalledSeconds >= 30f
                    ? "강제 배출"
                    : "새로고침",
                oldest != null && oldest.StalledSeconds >= 30f
                    ? () =>
                    {
                        InfrastructureCommandResult result =
                            conveyorCommands.ApproveOverflow(oldest.PayloadId);
                        view.ShowFeedback(FormatCommandResult(result));
                        view.RequestRefresh();
                    }
                    : view.RequestRefresh,
                CardHeight);

            foreach (ConveyorNodeSnapshot node in network.Nodes)
            {
                ConveyorNodeSnapshot capturedNode = node;
                BuildableObject building = FindBuilding(
                    capturedNode.BuildingId.Value);
                if (!ShouldShowConveyorNode(capturedNode, building))
                {
                    continue;
                }

                ConveyorFilterCriteria filter = capturedNode.Filter
                    ?? new ConveyorFilterCriteria();
                string filterSummary = BuildFilterSummary(filter);
                List<FeatureSurfaceAction> actions =
                    new List<FeatureSurfaceAction>
                    {
                        new FeatureSurfaceAction(
                            $"ConveyorToggle_{capturedNode.BuildingId.Value}",
                            capturedNode.Enabled ? "정지" : "가동",
                            () =>
                            {
                                if (building == null)
                                {
                                    view.ShowFeedback(
                                        "대상 컨베이어를 찾을 수 없습니다.");
                                    return;
                                }

                                InfrastructureCommandResult result =
                                    conveyorCommands.SetNodeEnabled(
                                        building,
                                        !capturedNode.Enabled);
                                view.ShowFeedback(FormatCommandResult(result));
                                view.RequestRefresh();
                            }),
                        new FeatureSurfaceAction(
                            $"ConveyorContamination_{capturedNode.BuildingId.Value}",
                            filter.allowContaminated
                                ? "오염 차단"
                                : "오염 허용",
                            () => ApplyFilterChange(
                                view,
                                building,
                                filter,
                                changed => changed.allowContaminated =
                                    !filter.allowContaminated)),
                        new FeatureSurfaceAction(
                            $"ConveyorFreshness_{capturedNode.BuildingId.Value}",
                            filter.filterFreshness
                                ? "신선도 해제"
                                : "신선도 필터",
                            () => ApplyFilterChange(
                                view,
                                building,
                                filter,
                                changed => changed.filterFreshness =
                                    !filter.filterFreshness)),
                        new FeatureSurfaceAction(
                            $"ConveyorForbidden_{capturedNode.BuildingId.Value}",
                            filter.allowForbidden
                                ? "금지품 차단"
                                : "금지품 허용",
                            () => ApplyFilterChange(
                                view,
                                building,
                                filter,
                                changed => changed.allowForbidden =
                                    !filter.allowForbidden)),
                        new FeatureSurfaceAction(
                            $"ConveyorQuality_{capturedNode.BuildingId.Value}",
                            filter.filterQuality
                                ? "품질 해제"
                                : "품질 필터",
                            () => ApplyFilterChange(
                                view,
                                building,
                                filter,
                                changed => changed.filterQuality =
                                    !filter.filterQuality))
                    };
                if (building?.BuildingData?.GetAbility<BuildingConveyorPortAbility>() != null)
                {
                    view.AddDataCard(
                        $"ConveyorDestination_{capturedNode.BuildingId.Value}", "컨베이어 목적지",
                        string.IsNullOrEmpty(capturedNode.DestinationId) ? "미설정" : capturedNode.DestinationId
                            + (conveyorQuery.GetDestinationChoices(building).Any(choice => choice.DestinationId == capturedNode.DestinationId)
                                ? "" : " · 철거/연결/출력 위치 확인 필요"), "목적지 선택",
                        () =>
                        {
                            if (!expandedConveyorDestinations.Add(capturedNode.BuildingId.Value))
                                expandedConveyorDestinations.Remove(capturedNode.BuildingId.Value);
                            view.RequestRefresh();
                        }, 120f);
                }
                if (building?.BuildingData?.GetAbility<
                        BuildingConveyorOverflowAbility>() != null)
                {
                    actions.Add(new FeatureSurfaceAction(
                        $"ConveyorOverflowPolicy_{capturedNode.BuildingId.Value}",
                        "배출 정책",
                        () =>
                        {
                            ConveyorOverflowPolicy next =
                                capturedNode.OverflowPolicy
                                == ConveyorOverflowPolicy.ManualApproval
                                    ? ConveyorOverflowPolicy
                                        .ReserveWarehouseThenLoose
                                    : capturedNode.OverflowPolicy + 1;
                            InfrastructureCommandResult result =
                                conveyorCommands.SetOverflowPolicy(
                                    building,
                                    next,
                                    capturedNode.ReserveWarehouseId);
                            view.ShowFeedback(FormatCommandResult(result));
                            view.RequestRefresh();
                        }));
                }

                List<FeatureSurfaceStepper> steppers =
                    new List<FeatureSurfaceStepper>();
                if (filter.filterFreshness)
                {
                    steppers.Add(new FeatureSurfaceStepper(
                        $"Freshness_{capturedNode.BuildingId.Value}",
                        "최소 신선도",
                        $"{filter.minimumFreshness01 * 100f:0}%",
                        () => ApplyFilterChange(
                            view,
                            building,
                            filter,
                            changed => changed.minimumFreshness01 =
                                Mathf.Clamp01(
                                    filter.minimumFreshness01 - 0.1f)),
                        () => ApplyFilterChange(
                            view,
                            building,
                            filter,
                            changed => changed.minimumFreshness01 =
                                Mathf.Clamp01(
                                    filter.minimumFreshness01 + 0.1f))));
                }

                if (filter.filterQuality)
                {
                    steppers.Add(new FeatureSurfaceStepper(
                        $"QualityMin_{capturedNode.BuildingId.Value}",
                        "최소 품질",
                        CombatQualityRules.GetDisplayName(
                            filter.minimumQuality),
                        () => ApplyFilterChange(
                            view,
                            building,
                            filter,
                            changed =>
                            {
                                changed.minimumQuality = StepQuality(
                                    filter.minimumQuality,
                                    -1);
                                if (changed.minimumQuality
                                    > changed.maximumQuality)
                                {
                                    changed.maximumQuality =
                                        changed.minimumQuality;
                                }
                            }),
                        () => ApplyFilterChange(
                            view,
                            building,
                            filter,
                            changed =>
                            {
                                changed.minimumQuality = StepQuality(
                                    filter.minimumQuality,
                                    1);
                                if (changed.minimumQuality
                                    > changed.maximumQuality)
                                {
                                    changed.maximumQuality =
                                        changed.minimumQuality;
                                }
                            })));
                    steppers.Add(new FeatureSurfaceStepper(
                        $"QualityMax_{capturedNode.BuildingId.Value}",
                        "최대 품질",
                        CombatQualityRules.GetDisplayName(
                            filter.maximumQuality),
                        () => ApplyFilterChange(
                            view,
                            building,
                            filter,
                            changed =>
                            {
                                changed.maximumQuality = StepQuality(
                                    filter.maximumQuality,
                                    -1);
                                if (changed.maximumQuality
                                    < changed.minimumQuality)
                                {
                                    changed.minimumQuality =
                                        changed.maximumQuality;
                                }
                            }),
                        () => ApplyFilterChange(
                            view,
                            building,
                            filter,
                            changed =>
                            {
                                changed.maximumQuality = StepQuality(
                                    filter.maximumQuality,
                                    1);
                                if (changed.maximumQuality
                                    < changed.minimumQuality)
                                {
                                    changed.minimumQuality =
                                        changed.maximumQuality;
                                }
                            })));
                }

                view.AddControlCard(
                    $"IndustryConveyorNode_{capturedNode.BuildingId.Value}",
                    GetBuildingName(building, capturedNode.BuildingId.Value),
                    filterSummary,
                    steppers,
                    actions,
                    steppers.Count > 1 ? 174f : 134f);
                if (building?.BuildingData?.GetAbility<BuildingConveyorPortAbility>() != null
                    && expandedConveyorDestinations.Contains(capturedNode.BuildingId.Value))
                    AddConveyorDestinations(view, building, capturedNode);
                AddConveyorFilterLists(view, building, capturedNode, filter);
            }
        }
    }

    private void AddConveyorFilterLists(IFeatureSurfaceView view, BuildableObject building,
        ConveyorNodeSnapshot node, ConveyorFilterCriteria filter)
    {
        string prefix = "ConveyorChoices_" + node.BuildingId.Value + "_";
        var actions = new List<FeatureSurfaceAction>();
        void Toggle(string kind, string label) => actions.Add(new FeatureSurfaceAction(kind, label, () =>
        {
            if (!conveyorChoicePages.Remove(prefix + kind)) conveyorChoicePages.Add(prefix + kind, 0);
            view.RequestRefresh();
        }));
        Toggle("Item", "품목 " + filter.itemIds.Count);
        Toggle("Category", "분류 " + filter.stockCategories.Count);
        Toggle("Material", "장비 재료 " + filter.materialIds.Count);
        bool hasOverflow = building?.BuildingData?.GetAbility<BuildingConveyorOverflowAbility>() != null;
        if (hasOverflow) Toggle("Reserve", "예비창고 선택");
        view.AddControlCard(prefix + "Lists", "운반 허용 목록",
            "품목 또는 분류 · 둘 다 비우면 전체. 장비 재료/품질/신선도는 추가 조건입니다.",
            Array.Empty<FeatureSurfaceStepper>(), actions, 134f);
        if (conveyorChoicePages.ContainsKey(prefix + "Item"))
            AddConveyorChoicePage(view, prefix + "Item", conveyorQuery.GetItemFilterChoices(),
                filter.itemIds, id => ApplyFilterChange(view, building, filter, changed =>
                { if (!changed.itemIds.Remove(id)) changed.itemIds.Add(id); }));
        if (conveyorChoicePages.ContainsKey(prefix + "Category"))
            AddConveyorChoicePage(view, prefix + "Category", conveyorQuery.GetStockCategoryFilterChoices()
                .Select(category => new ConveyorFilterChoice(category.ToString(), category.ToString())).ToArray(),
                filter.stockCategories.Select(category => category.ToString()).ToArray(),
                id => ApplyFilterChange(view, building, filter, changed =>
                {
                    StockCategory category = (StockCategory)Enum.Parse(typeof(StockCategory), id);
                    if (!changed.stockCategories.Remove(category)) changed.stockCategories.Add(category);
                }));
        if (conveyorChoicePages.ContainsKey(prefix + "Material"))
        {
            view.AddLabel("장비 인스턴스의 재료를 검사합니다. 일반 원료는 품목/분류에서 선택하세요.", 14f, 44f);
            AddConveyorChoicePage(view, prefix + "Material", conveyorQuery.GetMaterialFilterChoices(),
                filter.materialIds, id => ApplyFilterChange(view, building, filter, changed =>
                { if (!changed.materialIds.Remove(id)) changed.materialIds.Add(id); }));
        }
        if (!hasOverflow) return;
        var warehouses = conveyorQuery.GetReserveWarehouseChoices();
        string reserveStatus = string.IsNullOrEmpty(node.ReserveWarehouseId) ? "미설정" : node.ReserveWarehouseId;
        if (!string.IsNullOrEmpty(node.ReserveWarehouseId)
            && !warehouses.Any(choice => choice.DestinationId == node.ReserveWarehouseId))
            reserveStatus += " · 창고 소실/사용 불가";
        view.AddLabel("정책: " + FormatOverflowPolicy(node.OverflowPolicy) + "\n예비창고: " + reserveStatus, 14f, 52f);
        if (!conveyorChoicePages.ContainsKey(prefix + "Reserve")) return;
        void SelectReserve(string id)
        {
            view.ShowFeedback(FormatCommandResult(conveyorCommands.SetOverflowPolicy(building, node.OverflowPolicy, id)));
            view.RequestRefresh();
        }
        view.AddDataCard(prefix + "ReserveClear", "예비창고 해제", "화물은 벨트에 유지됩니다.",
            "해제", () => SelectReserve(string.Empty), CardHeight);
        AddConveyorChoicePage(view, prefix + "Reserve", warehouses.Select(choice => new ConveyorFilterChoice(
                choice.DestinationId, GetBuildingName(FindBuilding(choice.FacilityId), choice.FacilityId)
                    + $" · {choice.MaximumMassGrams / 1000d:0.###} kg (여유량은 입고 시 검사)")).ToArray(),
            new[] { node.ReserveWarehouseId }, SelectReserve);
    }

    private void AddConveyorChoicePage(IFeatureSurfaceView view, string key,
        IReadOnlyList<ConveyorFilterChoice> choices, IReadOnlyCollection<string> selected, Action<string> select)
    {
        int last = Math.Max(0, (choices.Count - 1) / ConveyorChoicesPerPage);
        int page = Math.Min(conveyorChoicePages[key], last);
        conveyorChoicePages[key] = page;
        view.AddControlCard(key + "Page", $"선택 목록 {page + 1}/{last + 1}", $"전체 {choices.Count}개 · 선택 {selected.Count}개",
            new[] { new FeatureSurfaceStepper(key + "Paging", "페이지", (page + 1).ToString(),
                () => { conveyorChoicePages[key] = Math.Max(0, page - 1); view.RequestRefresh(); },
                () => { conveyorChoicePages[key] = Math.Min(last, page + 1); view.RequestRefresh(); }) },
            Array.Empty<FeatureSurfaceAction>(), 134f);
        foreach (var choice in choices.Skip(page * ConveyorChoicesPerPage).Take(ConveyorChoicesPerPage))
        {
            var captured = choice;
            view.AddDataCard(key + "_" + choice.Id, choice.DisplayName, choice.Id,
                selected.Contains(choice.Id) ? "선택됨" : "선택", () => select(captured.Id), CardHeight);
        }
    }

    private static string FormatOverflowPolicy(ConveyorOverflowPolicy policy) => policy switch
    {
        ConveyorOverflowPolicy.ReserveWarehouseThenLoose => "예비창고 · 실패 시 화물 유지",
        ConveyorOverflowPolicy.AnyCompatibleWarehouseThenLoose => "호환 창고 검색 · 실패 시 화물 유지",
        ConveyorOverflowPolicy.LooseOnly => "명시적 바닥 배출",
        ConveyorOverflowPolicy.ManualApproval => "화물별 승인 후 배출",
        _ => "알 수 없는 정책"
    };

    private void AddConveyorDestinations(IFeatureSurfaceView view, BuildableObject building, ConveyorNodeSnapshot node)
    {
        var choices = conveyorQuery.GetDestinationChoices(building);
        view.AddDataCard("ConveyorDestinationClear_" + node.BuildingId.Value, "목적지 해제",
            "신규 자동 적재를 중지합니다. 이미 운반 중인 화물의 목적지는 유지합니다.", "해제",
            () =>
            {
                view.ShowFeedback(FormatCommandResult(conveyorCommands.SetPortDestination(building, string.Empty)));
                view.RequestRefresh();
            }, CardHeight);
        if (choices.Count == 0)
            view.AddLabel("선택 가능한 연결 없음: 출력 포트를 시설의 입고 위치에 연결하고 먼저 출력 목적지를 설정하세요.", 14f, 52f);
        foreach (var choice in choices)
        {
            var captured = choice;
            string ownerName = GetBuildingName(FindBuilding(choice.OwnerFacilityId), choice.OwnerFacilityId);
            view.AddDataCard("ConveyorDestinationChoice_" + node.BuildingId.Value + "_" + choice.DestinationId,
                ownerName, choice.DestinationId + $"\n입고 위치 {choice.DropPosition} · 용량 {choice.MaximumMassGrams / 1000d:0.###} kg"
                    + " · 품목/전력/잔량은 실제 배송 시 재검증", "선택",
                () =>
                {
                    view.ShowFeedback(FormatCommandResult(conveyorCommands.SetPortDestination(building, captured.DestinationId)));
                    view.RequestRefresh();
                }, CardHeight);
        }
    }

    private void AddAutomation(IFeatureSurfaceView view)
    {
        IReadOnlyList<AutomationFacilitySnapshot> facilities =
            automationQuery.Facilities;
        view.AddSection(
            "자동화",
            facilities.Count == 0
                ? "자동화 모듈이 설치된 시설이 없습니다."
                : $"자동화 시설 {facilities.Count}개");
        foreach (AutomationFacilitySnapshot facility in facilities)
        {
            AutomationFacilitySnapshot captured = facility;
            BuildableObject building = FindBuilding(captured.BuildingId.Value);
            string detail =
                $"모드 {FormatAutomation(captured.Mode)} · 작업 속도 {captured.WorkRate:0.00}x\n"
                + $"정비 {captured.Maintenance:0}% · 고장 {captured.Fault:0}%";
            if (!captured.Operational
                && captured.Status.IsBlocked)
            {
                detail += $"\n중단: {failureLocalizer.Localize(captured.Status)}";
            }

            view.AddDataCard(
                $"IndustryAutomation_{captured.BuildingId.Value}",
                captured.BuildingId.Value,
                detail,
                "모드 변경",
                building == null
                    ? () => view.ShowFeedback("대상 시설을 찾을 수 없습니다.")
                    : () =>
                    {
                        AutomationMode next = captured.Mode
                            == AutomationMode.Automatic
                                ? AutomationMode.Manual
                                : captured.Mode + 1;
                        InfrastructureCommandResult result =
                            automationCommands.SetMode(building, next);
                        view.ShowFeedback(FormatCommandResult(result));
                        view.RequestRefresh();
                    },
                CardHeight);
        }
    }

    private BuildableObject FindBuilding(string nodeId)
    {
        EnsureBuildingIndex();
        return !string.IsNullOrWhiteSpace(nodeId)
            && buildingsByNodeId.TryGetValue(
                nodeId,
                out BuildableObject building)
                ? building
                : null;
    }

    private void EnsureBuildingIndex()
    {
        if (indexedBuildingVersion == buildings.BuildingVersion)
        {
            return;
        }

        indexedBuildingVersion = buildings.BuildingVersion;
        buildingsByNodeId.Clear();
        foreach (BuildableObject building in buildings.Buildings)
        {
            if (building == null || building.IsGridDestroyed)
            {
                continue;
            }

            string nodeId =
                IndustrialInfrastructureIdentity.GetNodeId(building);
            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                buildingsByNodeId[nodeId] = building;
            }
        }
    }

    private static bool ShouldShowConveyorNode(
        ConveyorNodeSnapshot node,
        BuildableObject building)
    {
        if (node == null || !node.Enabled)
        {
            return true;
        }

        BuildingSO data = building?.BuildingData;
        if (data?.GetAbility<BuildingConveyorPortAbility>() != null
            || data?.GetAbility<BuildingConveyorOverflowAbility>() != null)
        {
            return true;
        }

        ConveyorFilterCriteria filter = node.Filter;
        return filter != null
            && (filter.filterQuality
                || filter.filterFreshness
                || !filter.allowContaminated
                || filter.allowForbidden
                || filter.itemIds?.Count > 0
                || filter.stockCategories?.Count > 0
                || filter.materialIds?.Count > 0);
    }

    private static string GetBuildingName(
        BuildableObject building,
        string fallback)
    {
        string displayName = building?.BuildingData?.objectName?.Trim();
        return string.IsNullOrWhiteSpace(displayName)
            ? fallback
            : displayName;
    }

    private static CombatEquipmentQuality StepQuality(
        CombatEquipmentQuality quality,
        int direction)
    {
        int minimum = (int)CombatEquipmentQuality.Awful;
        int maximum = (int)CombatEquipmentQuality.Mythic;
        return (CombatEquipmentQuality)Mathf.Clamp(
            (int)quality + Math.Sign(direction),
            minimum,
            maximum);
    }

    private void ApplyFilterChange(
        IFeatureSurfaceView view,
        BuildableObject building,
        ConveyorFilterCriteria source,
        Action<ConveyorFilterCriteria> mutate)
    {
        if (building == null)
        {
            view.ShowFeedback("대상 컨베이어를 찾을 수 없습니다.");
            return;
        }

        ConveyorFilterCriteria changed = CloneFilter(source);
        mutate?.Invoke(changed);
        InfrastructureCommandResult result =
            conveyorCommands.SetAdvancedFilter(building, changed);
        view.ShowFeedback(FormatCommandResult(result));
        view.RequestRefresh();
    }

    private string FormatCommandResult(InfrastructureCommandResult result) =>
        result.Succeeded
            ? string.Empty
            : failureLocalizer.Localize(result.Failure);

    private static ConveyorFilterCriteria CloneFilter(
        ConveyorFilterCriteria source)
    {
        source ??= new ConveyorFilterCriteria();
        return new ConveyorFilterCriteria
        {
            itemIds = source.itemIds?.ToList() ?? new List<string>(),
            stockCategories = source.stockCategories?.ToList()
                ?? new List<StockCategory>(),
            materialIds = source.materialIds?.ToList()
                ?? new List<string>(),
            allowForbidden = source.allowForbidden,
            filterQuality = source.filterQuality,
            minimumQuality = source.minimumQuality,
            maximumQuality = source.maximumQuality,
            filterFreshness = source.filterFreshness,
            minimumFreshness01 = source.minimumFreshness01,
            maximumFreshness01 = source.maximumFreshness01,
            allowContaminated = source.allowContaminated
        };
    }

    private static string BuildFilterSummary(
        ConveyorFilterCriteria filter)
    {
        List<string> parts = new List<string>();
        if (filter.itemIds?.Count > 0)
        {
            parts.Add($"아이템 {filter.itemIds.Count}개");
        }

        if (filter.stockCategories?.Count > 0)
        {
            parts.Add($"분류 {filter.stockCategories.Count}개");
        }

        if (filter.materialIds?.Count > 0)
        {
            parts.Add($"재질 {filter.materialIds.Count}개");
        }

        if (filter.filterQuality)
        {
            parts.Add($"품질 {filter.minimumQuality}~{filter.maximumQuality}");
        }

        if (filter.filterFreshness)
        {
            parts.Add($"신선도 {filter.minimumFreshness01 * 100f:0}% 이상");
        }

        parts.Add(filter.allowContaminated ? "오염 허용" : "오염 차단");
        parts.Add(filter.allowForbidden ? "금지품 허용" : "금지품 차단");
        return string.Join(" · ", parts);
    }

    private static string FormatOverlay(IndustrialOverlayKind kind) =>
        kind switch
        {
            IndustrialOverlayKind.Power => "전력망",
            IndustrialOverlayKind.CleanWater => "상수도",
            IndustrialOverlayKind.Wastewater => "하수도",
            _ => "컨베이어"
        };

    private static string FormatPriority(PowerPriority priority) =>
        priority switch
        {
            PowerPriority.Critical => "1 의료·생명 유지",
            PowerPriority.Defense => "2 방어·조명·하수",
            PowerPriority.Essential => "3 음식·위생·냉난방",
            PowerPriority.Production => "4 생산·컨베이어",
            _ => "5 비필수"
        };

    private static string FormatAutomation(AutomationMode mode) =>
        mode switch
        {
            AutomationMode.Manual => "수동",
            AutomationMode.PoweredAssist => "전동 보조",
            _ => "자동"
        };

    private static string FormatWaterTransfer(
        WaterContainerTransferMode mode) =>
        mode switch
        {
            WaterContainerTransferMode.BottleFromNetwork =>
                "배관 → 물통",
            WaterContainerTransferMode.FeedNetwork =>
                "물통 → 배관",
            _ => "정지"
        };

    private static string FormatConveyorState(ConveyorNetworkState state) =>
        state switch
        {
            ConveyorNetworkState.Running => "운행 중",
            ConveyorNetworkState.Stalled => "정체",
            ConveyorNetworkState.Deadlocked => "순환 교착",
            ConveyorNetworkState.Unpowered => "정전",
            _ => "정지"
        };

    private static string FormatStallReason(ConveyorStallReason reason) =>
        reason switch
        {
            ConveyorStallReason.InputPortFull => "입력 포트 가득 참",
            ConveyorStallReason.FilterMismatch => "필터 불일치",
            ConveyorStallReason.DestinationFull => "목적지 용량 부족",
            ConveyorStallReason.NextSegmentOccupied => "다음 구간 점유",
            ConveyorStallReason.CyclicDeadlock => "순환 교착",
            ConveyorStallReason.OverflowBlocked => "배출구 막힘",
            ConveyorStallReason.NoRoute => "목적지 경로 없음",
            ConveyorStallReason.PowerUnavailable => "정전",
            ConveyorStallReason.IntentionallyStopped => "수동 정지",
            _ => "정상"
        };
}
