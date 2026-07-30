using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class IndustrialFeatureSurfacePresenter :
    IFeatureSurfaceTabPresenter
{
    private const float CardHeight = 96f;
    private const int MaxPowerNodeCards = 40;

    private readonly IElectricalNetworkRuntime power;
    private readonly IPowerPriorityCommandService powerCommands;
    private readonly IWaterNetworkRuntime water;
    private readonly IPlumbingCommandService plumbing;
    private readonly IConveyorCommandService conveyors;
    private readonly IAutomationRuntime automation;
    private readonly IBuildingWorldQuery buildings;
    private readonly IIndustrialInfrastructureOverlayService overlays;
    private readonly Dictionary<string, BuildableObject> buildingsByNodeId =
        new Dictionary<string, BuildableObject>(StringComparer.Ordinal);
    private int indexedBuildingVersion = int.MinValue;

    public IndustrialFeatureSurfacePresenter(
        IElectricalNetworkRuntime power,
        IPowerPriorityCommandService powerCommands,
        IWaterNetworkRuntime water,
        IPlumbingCommandService plumbing,
        IConveyorCommandService conveyors,
        IAutomationRuntime automation,
        IBuildingWorldQuery buildings,
        IIndustrialInfrastructureOverlayService overlays)
    {
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.powerCommands = powerCommands
            ?? throw new ArgumentNullException(nameof(powerCommands));
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.plumbing = plumbing
            ?? throw new ArgumentNullException(nameof(plumbing));
        this.conveyors = conveyors
            ?? throw new ArgumentNullException(nameof(conveyors));
        this.automation = automation
            ?? throw new ArgumentNullException(nameof(automation));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.overlays = overlays
            ?? throw new ArgumentNullException(nameof(overlays));
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
            view.AddDataCard(
                $"IndustryPower_{network.NetworkId}",
                network.NetworkId,
                detail,
                "새로고침",
                view.RequestRefresh,
                CardHeight);
        }

        PowerNodeSnapshot[] consumerNodes = networks
            .SelectMany(network => network.Nodes)
            .Where(node => node.DemandPerSecond > 0f)
            .OrderBy(node => node.Powered)
            .ThenByDescending(node => node.Fault)
            .ThenBy(node => node.Priority)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        foreach (PowerNodeSnapshot node in consumerNodes
                     .Take(MaxPowerNodeCards))
        {
            PowerNodeSnapshot captured = node;
            BuildableObject building = FindBuilding(captured.NodeId);
            view.AddDataCard(
                $"IndustryPowerPriority_{captured.NodeId}",
                $"우선순위 {FormatPriority(captured.Priority)}",
                $"{captured.NodeId} · 공급 {captured.SuppliedFraction * 100f:0}%"
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
                        view.ShowFeedback(result.Message);
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
                     .OrderBy(node => node.NodeId, StringComparer.Ordinal))
        {
            PowerNodeSnapshot captured = node;
            BuildableObject building = FindBuilding(captured.NodeId);
            view.AddDataCard(
                $"IndustryPowerBreaker_{captured.NodeId}",
                "차단기 작동",
                $"{GetBuildingName(building, captured.NodeId)} · 열 {captured.Heat:0}% · 고장 {captured.Fault:0}%",
                "차단기 복구",
                building == null
                    ? () => view.ShowFeedback(
                        "대상 차단기를 찾을 수 없습니다.")
                    : () =>
                    {
                        InfrastructureCommandResult result =
                            powerCommands.ResetBreaker(building);
                        view.ShowFeedback(result.Message);
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
                 plumbing.WaterTransfers)
        {
            WaterTransferFacilitySnapshot captured = transfer;
            BuildableObject building = FindBuilding(captured.FacilityId);
            string detail =
                $"모드 {FormatWaterTransfer(captured.Mode)}"
                + $" · 진행 {captured.Progress01 * 100f:0}%"
                + (captured.Powered ? string.Empty : " · 정전");
            if (!string.IsNullOrWhiteSpace(captured.BlockedReason))
            {
                detail += $"\n대기: {captured.BlockedReason}";
            }

            view.AddDataCard(
                $"IndustryWaterTransfer_{captured.FacilityId}",
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
                        view.ShowFeedback(result.Message);
                        view.RequestRefresh();
                    },
                CardHeight);
        }
    }

    private void AddConveyors(IFeatureSurfaceView view)
    {
        IReadOnlyList<ConveyorNetworkSnapshot> networks = conveyors.Networks;
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

            if (!string.IsNullOrWhiteSpace(network.PlannedOverflowNodeId))
            {
                detail += $"\n예정 배출구 {network.PlannedOverflowNodeId}";
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
                            conveyors.ApproveOverflow(oldest.PayloadId);
                        view.ShowFeedback(result.Message);
                        view.RequestRefresh();
                    }
                    : view.RequestRefresh,
                CardHeight);

            foreach (ConveyorNodeSnapshot node in network.Nodes)
            {
                ConveyorNodeSnapshot capturedNode = node;
                BuildableObject building = FindBuilding(
                    capturedNode.NodeId);
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
                            $"ConveyorToggle_{capturedNode.NodeId}",
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
                                    conveyors.SetNodeEnabled(
                                        building,
                                        !capturedNode.Enabled);
                                view.ShowFeedback(result.Message);
                                view.RequestRefresh();
                            }),
                        new FeatureSurfaceAction(
                            $"ConveyorContamination_{capturedNode.NodeId}",
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
                            $"ConveyorFreshness_{capturedNode.NodeId}",
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
                            $"ConveyorForbidden_{capturedNode.NodeId}",
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
                            $"ConveyorQuality_{capturedNode.NodeId}",
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
                if (building?.BuildingData?.GetAbility<
                        BuildingConveyorOverflowAbility>() != null)
                {
                    actions.Add(new FeatureSurfaceAction(
                        $"ConveyorOverflowPolicy_{capturedNode.NodeId}",
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
                                conveyors.SetOverflowPolicy(
                                    building,
                                    next,
                                    capturedNode.ReserveWarehouseId);
                            view.ShowFeedback(result.Message);
                            view.RequestRefresh();
                        }));
                }

                List<FeatureSurfaceStepper> steppers =
                    new List<FeatureSurfaceStepper>();
                if (filter.filterFreshness)
                {
                    steppers.Add(new FeatureSurfaceStepper(
                        $"Freshness_{capturedNode.NodeId}",
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
                        $"QualityMin_{capturedNode.NodeId}",
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
                        $"QualityMax_{capturedNode.NodeId}",
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
                    $"IndustryConveyorNode_{capturedNode.NodeId}",
                    GetBuildingName(building, capturedNode.NodeId),
                    filterSummary,
                    steppers,
                    actions,
                    steppers.Count > 1 ? 174f : 134f);
            }
        }
    }

    private void AddAutomation(IFeatureSurfaceView view)
    {
        IReadOnlyList<AutomationFacilitySnapshot> facilities =
            automation.Facilities;
        view.AddSection(
            "자동화",
            facilities.Count == 0
                ? "자동화 모듈이 설치된 시설이 없습니다."
                : $"자동화 시설 {facilities.Count}개");
        foreach (AutomationFacilitySnapshot facility in facilities)
        {
            AutomationFacilitySnapshot captured = facility;
            BuildableObject building = FindBuilding(captured.FacilityId);
            string detail =
                $"모드 {FormatAutomation(captured.Mode)} · 작업 속도 {captured.WorkRate:0.00}x\n"
                + $"정비 {captured.Maintenance:0}% · 고장 {captured.Fault:0}%";
            if (!captured.Operational
                && !string.IsNullOrWhiteSpace(captured.BlockedReason))
            {
                detail += $"\n중단: {captured.BlockedReason}";
            }

            view.AddDataCard(
                $"IndustryAutomation_{captured.FacilityId}",
                captured.FacilityId,
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
                            automation.SetMode(building, next);
                        view.ShowFeedback(result.Message);
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
        int maximum = (int)CombatEquipmentQuality.Legendary;
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
            conveyors.SetAdvancedFilter(building, changed);
        view.ShowFeedback(result.Message);
        view.RequestRefresh();
    }

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

public sealed class IndustrialTabContentPresenter : IUITabContentPresenter
{
    private readonly IElectricalNetworkRuntime power;
    private readonly IWaterNetworkRuntime water;
    private readonly IConveyorRuntime conveyors;
    private readonly IAutomationRuntime automation;

    public IndustrialTabContentPresenter(
        IElectricalNetworkRuntime power,
        IWaterNetworkRuntime water,
        IConveyorRuntime conveyors,
        IAutomationRuntime automation)
    {
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.conveyors = conveyors
            ?? throw new ArgumentNullException(nameof(conveyors));
        this.automation = automation
            ?? throw new ArgumentNullException(nameof(automation));
    }

    public TabId Id => TabId.Industry;

    public string Build()
    {
        int conveyorProblems = conveyors.Networks.Count(network =>
            network.State is ConveyorNetworkState.Stalled
                or ConveyorNetworkState.Deadlocked);
        return string.Join(
            "\n",
            $"전력망: {power.Networks.Count}",
            $"상하수도망: {water.Networks.Count}",
            $"컨베이어망: {conveyors.Networks.Count}",
            $"정체·교착: {conveyorProblems}",
            $"자동화 시설: {automation.Facilities.Count}");
    }
}
