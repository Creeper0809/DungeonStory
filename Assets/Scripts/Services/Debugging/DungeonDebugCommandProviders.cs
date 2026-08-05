using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class DungeonDebugCheatCommandProvider : IDungeonDebugCommandProvider
{
    private readonly IDungeonDebugModeService modeService;

    public DungeonDebugCheatCommandProvider(IDungeonDebugModeService modeService)
    {
        this.modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
    }

    public IEnumerable<IDungeonDebugCommand> GetCommands()
    {
        foreach (DungeonDebugCheat cheat in Enum.GetValues(typeof(DungeonDebugCheat)))
        {
            DungeonDebugCheat captured = cheat;
            yield return new DelegateDungeonDebugCommand(
                $"cheat:{captured}",
                GetDisplayName(captured),
                modeService.IsCheatEnabled(captured) ? "현재 켜짐" : "현재 꺼짐",
                DungeonDebugCategory.Cheats,
                DungeonDebugTargetKind.None,
                _ =>
                {
                    bool enabled = !modeService.IsCheatEnabled(captured);
                    modeService.SetCheat(captured, enabled);
                    return DungeonDebugCommandResult.Succeeded(
                        $"{GetDisplayName(captured)} {(enabled ? "켜짐" : "꺼짐")}");
                },
                mutatesWorld: false);
        }
    }

    public static string GetDisplayName(DungeonDebugCheat cheat)
    {
        return cheat switch
        {
            DungeonDebugCheat.FriendlyInvincible => "아군 무적",
            DungeonDebugCheat.FacilityInvincible => "시설 무적",
            DungeonDebugCheat.FreezeNeeds => "욕구 고정",
            DungeonDebugCheat.PreventBreakdowns => "붕괴 방지",
            DungeonDebugCheat.NoMoneyOrItemCost => "자금·아이템 소비 없음",
            DungeonDebugCheat.FreeConstruction => "무료 건설",
            DungeonDebugCheat.IgnorePlacementRules => "설치 제한 무시",
            DungeonDebugCheat.InstantConstruction => "즉시 건설",
            DungeonDebugCheat.InstantWork => "즉시 작업·연구·제작",
            DungeonDebugCheat.IgnoreUnlocks => "해금 제한 무시",
            DungeonDebugCheat.PauseHumanoidAi => "인간형 AI 정지",
            DungeonDebugCheat.PauseWildlifeAi => "야생동물 AI 정지",
            _ => cheat.ToString()
        };
    }
}

public sealed class DungeonDebugEconomyCommandProvider : IDungeonDebugCommandProvider
{
    private readonly IGameEventBus gameEventBus;
    private readonly IGameMoneyAccount money;
    private readonly IGameCalendar calendar;

    public DungeonDebugEconomyCommandProvider(
        IGameEventBus gameEventBus,
        IGameMoneyAccount money,
        IGameCalendar calendar)
    {
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    public IEnumerable<IDungeonDebugCommand> GetCommands()
    {
        yield return Numeric("economy:add-money", "자금 추가", 1000f, value =>
        {
            money.Add(
                Mathf.RoundToInt(value),
                DebugMoneyContext("debug-add-money"));
            return $"자금 {money.Balance:N0}";
        });
        yield return Numeric("economy:remove-money", "자금 차감", 1000f, value =>
        {
            money.TrySpend(
                Mathf.RoundToInt(value),
                DebugMoneyContext("debug-remove-money"),
                out _);
            return $"자금 {money.Balance:N0}";
        }, dangerous: true);
        yield return Numeric("economy:set-money", "자금 직접 설정", 5000f, value =>
        {
            money.SetBalance(
                Mathf.RoundToInt(value),
                DebugMoneyContext("debug-set-money"));
            return $"자금 {money.Balance:N0}";
        });
        yield return Numeric("time:set-hour", "시간대 설정", 12f, value =>
        {
            int hour = Mathf.Clamp(Mathf.RoundToInt(value), 0, 23);
            calendar.SetDateTime(calendar.Day, hour);
            return $"{hour:00}:00로 설정";
        });
        yield return Numeric("time:advance-hours", "시간 진행", 1f, value =>
        {
            int hours = Mathf.Max(1, Mathf.RoundToInt(value));
            int total = calendar.Hour + hours;
            calendar.SetDateTime(calendar.Day + total / 24, total % 24);
            return $"{hours}시간 진행";
        });
        yield return new DelegateDungeonDebugCommand(
            "time:force-settlement",
            "일일 정산 강제 실행",
            "현재 일자의 정상 정산 이벤트를 발행합니다.",
            DungeonDebugCategory.Cheats,
            DungeonDebugTargetKind.None,
            _ =>
            {
                int nextDay = calendar.Day + 1;
                calendar.SetDateTime(nextDay, calendar.Hour);
                gameEventBus.Publish(new OperatingDayStartedEvent(nextDay));
                return DungeonDebugCommandResult.Succeeded($"{nextDay}일차 정산을 실행했습니다.");
            });
    }

    private static EconomyTransactionContext DebugMoneyContext(string sourceId)
    {
        return new EconomyTransactionContext(
            EconomyTransactionKind.DebugAdjustment,
            sourceId,
            description: "디버그 자금 조정");
    }

    private IDungeonDebugCommand Numeric(
        string id,
        string label,
        float defaultValue,
        Func<float, string> execute,
        bool dangerous = false)
    {
        return new DelegateDungeonDebugCommand(
            id,
            label,
            "팔레트의 수치 입력값을 사용합니다.",
            DungeonDebugCategory.Cheats,
            DungeonDebugTargetKind.None,
            context =>
            {
                string message = execute(context.NumericValue);
                return message.Contains("없음", StringComparison.Ordinal)
                    ? DungeonDebugCommandResult.Failed(message)
                    : DungeonDebugCommandResult.Succeeded(message);
            },
            isDangerous: dangerous,
            defaultNumericValue: defaultValue);
    }

}

public sealed class DungeonDebugItemCommandProvider : IDungeonDebugCommandProvider
{
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IWorldDropZoneQuery dropZoneQuery;
    private readonly IDungeonItemCatalogProvider catalogProvider;

    public DungeonDebugItemCommandProvider(
        IWorldItemStackRuntime itemRuntime,
        IWorldDropZoneQuery dropZoneQuery,
        IDungeonItemCatalogProvider catalogProvider)
    {
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.dropZoneQuery = dropZoneQuery ?? throw new ArgumentNullException(nameof(dropZoneQuery));
        this.catalogProvider = catalogProvider ?? throw new ArgumentNullException(nameof(catalogProvider));
    }

    public IEnumerable<IDungeonDebugCommand> GetCommands()
    {
        foreach (DungeonItemDefinition definition in
                 catalogProvider.All ?? Array.Empty<DungeonItemDefinition>())
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
            {
                continue;
            }

            DungeonItemDefinition captured = definition;
            yield return new DelegateDungeonDebugCommand(
                $"spawn:item:{captured.ItemId}",
                $"{captured.DisplayName} 소환",
                captured.Description,
                DungeonDebugCategory.Spawn,
                DungeonDebugTargetKind.GridCell,
                context => SpawnAt(captured.ItemId, context, captured.DisplayName),
                defaultNumericValue: 10f);
        }

        yield return new DelegateDungeonDebugCommand(
            "spawn:dropoff-general",
            "하차장 보급품 소환",
            "하차장에 일반 재고를 소환합니다.",
            DungeonDebugCategory.Spawn,
            DungeonDebugTargetKind.None,
            context =>
            {
                int amount = Mathf.Clamp(Mathf.RoundToInt(context.NumericValue), 1, 9999);
                bool success = itemRuntime.SpawnStockAtDropoff(
                    StockCategory.General,
                    amount,
                    "디버그",
                    out int spawned);
                return success
                    ? DungeonDebugCommandResult.Succeeded($"하차장에 보급품 {spawned}개를 소환했습니다.")
                    : DungeonDebugCommandResult.Failed("하차장을 찾지 못했습니다.");
            },
            defaultNumericValue: 10f);
        yield return ItemTargetCommand("item:duplicate", "선택 스택 복제", context =>
        {
            WorldItemStackSnapshot stack = context.Target.ItemStack;
            int amount = Mathf.Max(1, Mathf.RoundToInt(context.NumericValue));
            bool success = itemRuntime.SpawnItemAt(
                stack.ItemId,
                amount,
                stack.Position,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned);
            return success
                ? DungeonDebugCommandResult.Succeeded($"{stack.DisplayName} {spawned}개를 복제했습니다.")
                : DungeonDebugCommandResult.Failed("스택을 복제하지 못했습니다.");
        });
        yield return ItemTargetCommand("item:clear-reservation", "예약 해제", context =>
            itemRuntime.TryClearReservation(context.Target.ItemStack.StackId)
                ? DungeonDebugCommandResult.Succeeded("예약을 해제했습니다.")
                : DungeonDebugCommandResult.Failed("해제할 예약이 없습니다."));
        yield return ItemTargetCommand("item:toggle-forbidden", "금지/허용 전환", context =>
        {
            WorldItemStackSnapshot stack = context.Target.ItemStack;
            bool next = !stack.Forbidden;
            return itemRuntime.SetForbidden(stack.StackId, next)
                ? DungeonDebugCommandResult.Succeeded(next ? "스택을 금지했습니다." : "스택을 허용했습니다.")
                : DungeonDebugCommandResult.Failed("스택 상태를 바꾸지 못했습니다.");
        });
        yield return ItemTargetCommand(
            "item:delete",
            "선택 스택 삭제",
            context => itemRuntime.DeleteStack(context.Target.ItemStack.StackId)
                ? DungeonDebugCommandResult.Succeeded("스택을 삭제했습니다.")
                : DungeonDebugCommandResult.Failed("스택을 찾지 못했습니다."),
            dangerous: true);
        yield return new DelegateDungeonDebugCommand(
            "item:clear-loose",
            "Loose 스택 전체 정리",
            "바닥에 놓인 loose 아이템을 모두 삭제합니다.",
            DungeonDebugCategory.Spawn,
            DungeonDebugTargetKind.None,
            _ =>
            {
                int deleted = 0;
                foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks()
                             .Where(stack => stack != null && stack.State == WorldItemStackState.Loose)
                             .ToArray())
                {
                    if (itemRuntime.DeleteStack(stack.StackId)) deleted++;
                }

                return DungeonDebugCommandResult.Succeeded($"{deleted}개 스택을 정리했습니다.");
            },
            isDangerous: true);
    }

    private DungeonDebugCommandResult SpawnAt(
        string itemId,
        DungeonDebugExecutionContext context,
        string displayName)
    {
        int amount = Mathf.Clamp(Mathf.RoundToInt(context.NumericValue), 1, 9999);
        bool success = itemRuntime.SpawnItemAt(
            itemId,
            amount,
            context.Target.GridPosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int spawned);
        return success
            ? DungeonDebugCommandResult.Succeeded($"{displayName} {spawned}개를 소환했습니다.")
            : DungeonDebugCommandResult.Failed("이 칸에는 아이템을 소환할 수 없습니다.");
    }

    private static IDungeonDebugCommand ItemTargetCommand(
        string id,
        string label,
        Func<DungeonDebugExecutionContext, DungeonDebugCommandResult> execute,
        bool dangerous = false)
    {
        return new DelegateDungeonDebugCommand(
            id,
            label,
            "정확히 클릭한 스택에 적용합니다.",
            DungeonDebugCategory.Spawn,
            DungeonDebugTargetKind.ItemPile,
            execute,
            isDangerous: dangerous);
    }
}

public sealed class DungeonDebugWorkCommandProvider : IDungeonDebugCommandProvider
{
    private readonly IWorkOrderRuntime workOrderRuntime;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly BlueprintResearchRuntime research;
    private readonly IFacilityShopCatalog facilityCatalog;
    private readonly IFacilityShopUnlockStateService shopUnlockStateService;

    public DungeonDebugWorkCommandProvider(
        IWorkOrderRuntime workOrderRuntime,
        IWorldItemStackRuntime itemRuntime,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IFacilityShopCatalog facilityCatalog,
        IFacilityShopUnlockStateService shopUnlockStateService)
    {
        this.workOrderRuntime = workOrderRuntime ?? throw new ArgumentNullException(nameof(workOrderRuntime));
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(DungeonDebugWorkCommandProvider)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.facilityCatalog = facilityCatalog ?? throw new ArgumentNullException(nameof(facilityCatalog));
        this.shopUnlockStateService = shopUnlockStateService
            ?? throw new ArgumentNullException(nameof(shopUnlockStateService));
    }

    public IEnumerable<IDungeonDebugCommand> GetCommands()
    {
        yield return BuildingCommand("building:repair", "시설 수리", building =>
        {
            building.SetDamaged(false);
            return $"{building.BuildingData?.objectName ?? building.name} 수리 완료";
        });
        yield return BuildingCommand("building:damage", "시설 파손", building =>
        {
            building.SetDamaged(true);
            return $"{building.BuildingData?.objectName ?? building.name} 파손";
        }, dangerous: true);
        yield return BuildingCommand("building:destroy", "시설 철거", building =>
        {
            string label = building.BuildingData?.objectName ?? building.name;
            building.DestroySelf();
            return $"{label} 철거";
        }, dangerous: true);
        yield return new DelegateDungeonDebugCommand(
            "work:complete-selected",
            "선택 작업 완료",
            "선택한 공사 또는 시설 작업 주문을 완료합니다.",
            DungeonDebugCategory.BuildingWork,
            DungeonDebugTargetKind.Building,
            context =>
            {
                BuildableObject building = context.Target.Building;
                foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
                {
                    if (!workOrderRuntime.TryGetOrderFor(
                            building,
                            definition.WorkTypeId,
                            out WorkOrderProgressState order))
                    {
                        continue;
                    }

                    return workOrderRuntime.DebugCompleteOrder(order.WorkOrderId, out string message)
                        ? DungeonDebugCommandResult.Succeeded(message)
                        : DungeonDebugCommandResult.Failed(message);
                }

                return DungeonDebugCommandResult.Failed("선택한 대상에 작업 주문이 없습니다.");
            });
        yield return new DelegateDungeonDebugCommand(
            "work:cancel-selected",
            "선택 작업 취소",
            "선택한 작업 주문과 재료 예약을 취소합니다.",
            DungeonDebugCategory.BuildingWork,
            DungeonDebugTargetKind.Building,
            context =>
            {
                foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
                {
                    if (workOrderRuntime.TryGetOrderFor(
                            context.Target.Building,
                            definition.WorkTypeId,
                            out WorkOrderProgressState order)
                        && workOrderRuntime.CancelOrder(order.WorkOrderId, refundDeliveredMaterials: true))
                    {
                        return DungeonDebugCommandResult.Succeeded("작업 주문을 취소했습니다.");
                    }
                }

                return DungeonDebugCommandResult.Failed("취소할 작업 주문이 없습니다.");
            },
            isDangerous: true);
        yield return new DelegateDungeonDebugCommand(
            "work:complete-all",
            "모든 작업 주문 완료",
            "모든 공사와 누적 작업 주문을 즉시 완료합니다.",
            DungeonDebugCategory.BuildingWork,
            DungeonDebugTargetKind.None,
            _ =>
            {
                int completed = workOrderRuntime.DebugCompleteAllOrders();
                return DungeonDebugCommandResult.Succeeded($"{completed}개 작업 주문을 완료했습니다.");
            });
        yield return new DelegateDungeonDebugCommand(
            "work:fill-buffer",
            "시설 버퍼 채우기",
            "선택 시설의 현재 작업 주문에 필요한 재료를 즉시 납품합니다.",
            DungeonDebugCategory.BuildingWork,
            DungeonDebugTargetKind.Building,
            context =>
            {
                BuildableObject building = context.Target.Building;
                if (!TryFindOrder(building, out WorkOrderProgressState order))
                {
                    return DungeonDebugCommandResult.Failed("선택한 시설에 작업 주문이 없습니다.");
                }

                int spawned = 0;
                foreach (KeyValuePair<string, int> requirement in
                         order.ItemMaterialRequirements)
                {
                    int buffered = itemRuntime.GetAllStacks()
                        .Where(stack => stack != null
                            && stack.State == WorldItemStackState.FacilityBuffer
                            && string.Equals(
                                stack.DestinationId,
                                order.MaterialDestinationId,
                                StringComparison.Ordinal)
                            && string.Equals(
                                stack.ItemId,
                                requirement.Key,
                                StringComparison.Ordinal))
                        .Sum(stack => stack.Quantity);
                    int missing = Mathf.Max(0, requirement.Value - buffered);
                    if (missing <= 0)
                    {
                        continue;
                    }

                    itemRuntime.SpawnItemAt(
                        requirement.Key,
                        missing,
                        building.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        order.MaterialDestinationId,
                        out int added);
                    spawned += added;
                }

                if (building is ConstructionSite construction)
                {
                    workOrderRuntime.RefreshMaterialsReady(construction);
                }

                return DungeonDebugCommandResult.Succeeded($"{spawned}개 재료를 납품했습니다.");
            });
        yield return new DelegateDungeonDebugCommand(
            "work:empty-buffer",
            "시설 버퍼 비우기",
            "선택 시설의 현재 작업 주문에 납품된 재료를 제거합니다.",
            DungeonDebugCategory.BuildingWork,
            DungeonDebugTargetKind.Building,
            context =>
            {
                if (!TryFindOrder(context.Target.Building, out WorkOrderProgressState order))
                {
                    return DungeonDebugCommandResult.Failed("선택한 시설에 작업 주문이 없습니다.");
                }

                int removed = itemRuntime.RemoveStacksByStateAndDestination(
                    WorldItemStackState.FacilityBuffer,
                    order.MaterialDestinationId);
                return DungeonDebugCommandResult.Succeeded($"{removed}개 재료 스택을 비웠습니다.");
            },
            isDangerous: true);
        yield return new DelegateDungeonDebugCommand(
            "research:complete-all",
            "모든 연구 완료",
            "등록된 모든 설계도 연구를 완료하고 결과 해금을 적용합니다.",
            DungeonDebugCategory.BuildingWork,
            DungeonDebugTargetKind.None,
            _ =>
            {
                if (research == null)
                {
                    return DungeonDebugCommandResult.Failed("연구 런타임이 없습니다.");
                }

                int completed = research.CompleteAllBlueprintsImmediately();
                return DungeonDebugCommandResult.Succeeded($"{completed}개 연구를 완료했습니다.");
            });
        yield return new DelegateDungeonDebugCommand(
            "unlock:all",
            "전체 연구·건물 해금",
            "모든 건물과 설계도를 현재 런에서 해금합니다.",
            DungeonDebugCategory.BuildingWork,
            DungeonDebugTargetKind.None,
            _ =>
            {
                if (research == null)
                {
                    return DungeonDebugCommandResult.Failed("연구 런타임이 없습니다.");
                }

                FacilityShopUnlockState shopState = shopUnlockStateService.GetUnlockState();
                foreach (BuildingSO building in facilityCatalog.Buildings.Where(building => building != null))
                {
                    research.State.UnlockBuilding(building.id);
                    shopState.UnlockBasicPurchaseById(building.id);
                }

                foreach (FacilityBlueprintSO blueprint in facilityCatalog.Blueprints
                             .Where(blueprint => blueprint != null))
                {
                    research.State.MarkCompleted(blueprint);
                    shopState.MarkBlueprintAcquired(blueprint);
                }

                return DungeonDebugCommandResult.Succeeded(
                    $"건물 {facilityCatalog.Buildings.Count}개와 설계도 {facilityCatalog.Blueprints.Count}개를 해금했습니다.");
            });
    }

    private bool TryFindOrder(BuildableObject building, out WorkOrderProgressState order)
    {
        order = null;
        if (building == null)
        {
            return false;
        }

        foreach (WorkTypeDefinition definition in WorkTypeCatalog.All)
        {
            if (workOrderRuntime.TryGetOrderFor(building, definition.WorkTypeId, out order))
            {
                return true;
            }
        }

        return false;
    }

    private static IDungeonDebugCommand BuildingCommand(
        string id,
        string label,
        Func<BuildableObject, string> execute,
        bool dangerous = false)
    {
        return new DelegateDungeonDebugCommand(
            id,
            label,
            "정확히 클릭한 시설에 적용합니다.",
            DungeonDebugCategory.BuildingWork,
            DungeonDebugTargetKind.Building,
            context => DungeonDebugCommandResult.Succeeded(execute(context.Target.Building)),
            isDangerous: dangerous);
    }
}

public sealed class DungeonDebugSurvivalWildlifeCommandProvider : IDungeonDebugCommandProvider
{
    private readonly IWorldFilthQuery filthRuntime;
    private readonly IWorldWaterQuery waterRuntime;
    private readonly IWildlifeRuntime wildlifeRuntime;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IWildlifeEcosystemRuntime ecosystemRuntime;
    private readonly ISurvivalFoodDebugCommand survivalRuntime;

    public DungeonDebugSurvivalWildlifeCommandProvider(
        IWorldFilthQuery filthRuntime,
        IWorldWaterQuery waterRuntime,
        IWildlifeRuntime wildlifeRuntime,
        IWildlifeSpeciesCatalogProvider speciesCatalog,
        IWildlifeEcosystemRuntime ecosystemRuntime,
        ISurvivalFoodDebugCommand survivalRuntime)
    {
        this.filthRuntime = filthRuntime ?? throw new ArgumentNullException(nameof(filthRuntime));
        this.waterRuntime = waterRuntime ?? throw new ArgumentNullException(nameof(waterRuntime));
        this.wildlifeRuntime = wildlifeRuntime ?? throw new ArgumentNullException(nameof(wildlifeRuntime));
        this.speciesCatalog = speciesCatalog ?? throw new ArgumentNullException(nameof(speciesCatalog));
        this.ecosystemRuntime = ecosystemRuntime ?? throw new ArgumentNullException(nameof(ecosystemRuntime));
        this.survivalRuntime = survivalRuntime ?? throw new ArgumentNullException(nameof(survivalRuntime));
    }

    public IEnumerable<IDungeonDebugCommand> GetCommands()
    {
        yield return new DelegateDungeonDebugCommand(
            "survival:add-filth",
            "오염 생성",
            "선택 칸에 감염 위험이 있는 오염을 생성합니다.",
            DungeonDebugCategory.SurvivalWildlife,
            DungeonDebugTargetKind.GridCell,
            context =>
            {
                float amount = Mathf.Clamp(context.NumericValue, 0.1f, 100f);
                WorldFilthSnapshot filth = filthRuntime.AddFilth(
                    WorldFilthType.Waste,
                    context.Target.GridPosition,
                    amount,
                    "debug",
                    0.65f);
                return DungeonDebugCommandResult.Succeeded($"오염 {filth.Amount:0.#} 생성");
            },
            defaultNumericValue: 25f);
        yield return new DelegateDungeonDebugCommand(
            "survival:clear-filth-cell",
            "칸 오염 제거",
            "선택 칸의 모든 오염을 제거합니다.",
            DungeonDebugCategory.SurvivalWildlife,
            DungeonDebugTargetKind.GridCell,
            context =>
            {
                int cleaned = 0;
                foreach (WorldFilthSnapshot filth in filthRuntime.GetAt(context.Target.GridPosition).ToArray())
                {
                    if (filthRuntime.Clean(filth.FilthId, 100000f, out _)) cleaned++;
                }

                return DungeonDebugCommandResult.Succeeded($"{cleaned}개 오염 제거");
            });
        foreach (WorldWaterQuality quality in Enum.GetValues(typeof(WorldWaterQuality)))
        {
            WorldWaterQuality captured = quality;
            yield return new DelegateDungeonDebugCommand(
                $"survival:water:{captured}",
                $"{WaterName(captured)} 수원 생성",
                "선택 칸에 얕은 물 수원을 생성하거나 덮어씁니다.",
                DungeonDebugCategory.SurvivalWildlife,
                DungeonDebugTargetKind.GridCell,
                context =>
                {
                    float capacity = Mathf.Clamp(context.NumericValue, 1f, 10000f);
                    return waterRuntime.DebugCreateSource(
                        context.Target.GridPosition,
                        captured,
                        capacity,
                        GridCellTerrainType.ShallowWater,
                        out string sourceId)
                        ? DungeonDebugCommandResult.Succeeded($"{WaterName(captured)} 수원 {sourceId} 생성")
                        : DungeonDebugCommandResult.Failed("수원을 생성하지 못했습니다.");
                },
                defaultNumericValue: 20f);
        }

        foreach (SurvivalWeatherType weather in Enum.GetValues(typeof(SurvivalWeatherType)))
        {
            SurvivalWeatherType captured = weather;
            yield return new DelegateDungeonDebugCommand(
                $"survival:weather:{captured}",
                $"날씨: {WeatherName(captured)}",
                "현재 생존 날씨를 즉시 변경합니다.",
                DungeonDebugCategory.SurvivalWildlife,
                DungeonDebugTargetKind.None,
                _ =>
                {
                    survivalRuntime.DebugSetWeather(captured);
                    return DungeonDebugCommandResult.Succeeded($"날씨를 {WeatherName(captured)}로 변경했습니다.");
                });
        }

        yield return new DelegateDungeonDebugCommand(
            "survival:spoilage-advance",
            "음식 부패 진행",
            "초 단위로 신선도를 진행합니다.",
            DungeonDebugCategory.SurvivalWildlife,
            DungeonDebugTargetKind.None,
            context =>
            {
                survivalRuntime.DebugAdvanceSpoilage(Mathf.Max(1f, context.NumericValue));
                return DungeonDebugCommandResult.Succeeded($"부패를 {context.NumericValue:0}초 진행했습니다.");
            },
            defaultNumericValue: 120f);
        yield return new DelegateDungeonDebugCommand(
            "survival:spoilage-reset",
            "음식 신선도 초기화",
            "등록된 음식의 신선도와 오염 상태를 초기화합니다.",
            DungeonDebugCategory.SurvivalWildlife,
            DungeonDebugTargetKind.None,
            _ =>
            {
                survivalRuntime.DebugResetSpoilage();
                return DungeonDebugCommandResult.Succeeded("음식 신선도를 초기화했습니다.");
            });

        foreach (WildlifeSpeciesDefinition species in speciesCatalog.All)
        {
            if (species == null) continue;
            WildlifeSpeciesDefinition captured = species;
            yield return new DelegateDungeonDebugCommand(
                $"wildlife:spawn:{captured.SpeciesId}",
                $"{captured.DisplayName} 소환",
                "선택한 외부 칸과 주변에 야생동물을 소환합니다.",
                DungeonDebugCategory.SurvivalWildlife,
                DungeonDebugTargetKind.GridCell,
                context => wildlifeRuntime.DebugSpawn(
                    captured.SpeciesId,
                    Mathf.Clamp(Mathf.RoundToInt(context.NumericValue), 1, 50),
                    context.Target.GridPosition,
                    out _,
                    out string message)
                    ? DungeonDebugCommandResult.Succeeded(message)
                    : DungeonDebugCommandResult.Failed(message),
                defaultNumericValue: 1f);
        }

        yield return WildlifeCommand("wildlife:hunt", "사냥 지정", wildlife =>
            wildlifeRuntime.DesignateHunt(wildlife.WildlifeId, true, priority: false)
                ? "사냥 지정"
                : "사냥 지정 실패");
        yield return WildlifeCommand("wildlife:heal", "동물 회복", wildlife =>
            $"체력 {wildlife.DebugHeal(wildlife.MaxHealth)} 회복");
        yield return WildlifeCommand("wildlife:damage", "동물 피해", wildlife =>
            $"피해 {wildlife.ApplyDamage(Mathf.Max(1, Mathf.RoundToInt(wildlife.MaxHealth * 0.25f)), null)}",
            dangerous: true);
        yield return WildlifeCommand("wildlife:delete", "동물 삭제", wildlife =>
            wildlifeRuntime.DebugDelete(wildlife.WildlifeId) ? "야생동물 삭제" : "삭제 실패",
            dangerous: true);
        yield return new DelegateDungeonDebugCommand(
            "wildlife:delete-all",
            "야생동물 전체 제거",
            "현재 월드의 야생동물을 모두 제거합니다.",
            DungeonDebugCategory.SurvivalWildlife,
            DungeonDebugTargetKind.None,
            _ => DungeonDebugCommandResult.Succeeded(
                $"{wildlifeRuntime.DebugDeleteAll()}마리 제거"),
            isDangerous: true);
        yield return new DelegateDungeonDebugCommand(
            "wildlife:habitat-fill",
            "서식지 자원 채우기",
            "모든 서식지 패치를 최대 자원으로 채웁니다.",
            DungeonDebugCategory.SurvivalWildlife,
            DungeonDebugTargetKind.None,
            _ =>
            {
                foreach (WildlifeHabitatPatch patch in ecosystemRuntime.Patches)
                {
                    patch?.SynchronizeResource(patch.ResourceCapacity, patch.ResourceCapacity);
                }

                return DungeonDebugCommandResult.Succeeded("서식지 자원을 채웠습니다.");
            });
        yield return new DelegateDungeonDebugCommand(
            "wildlife:habitat-empty",
            "서식지 자원 고갈",
            "모든 서식지 패치의 자원을 고갈시킵니다.",
            DungeonDebugCategory.SurvivalWildlife,
            DungeonDebugTargetKind.None,
            _ =>
            {
                foreach (WildlifeHabitatPatch patch in ecosystemRuntime.Patches)
                {
                    patch?.SynchronizeResource(patch.ResourceCapacity, 0f);
                }

                return DungeonDebugCommandResult.Succeeded("서식지 자원을 고갈시켰습니다.");
            },
            isDangerous: true);
    }

    private static IDungeonDebugCommand WildlifeCommand(
        string id,
        string label,
        Func<WildlifeActor, string> execute,
        bool dangerous = false)
    {
        return new DelegateDungeonDebugCommand(
            id,
            label,
            "정확히 클릭한 야생동물에 적용합니다.",
            DungeonDebugCategory.SurvivalWildlife,
            DungeonDebugTargetKind.Wildlife,
            context =>
            {
                string message = execute(context.Target.Wildlife);
                return message.Contains("실패", StringComparison.Ordinal)
                    ? DungeonDebugCommandResult.Failed(message)
                    : DungeonDebugCommandResult.Succeeded(message);
            },
            isDangerous: dangerous);
    }

    private static string WaterName(WorldWaterQuality quality)
    {
        return quality switch
        {
            WorldWaterQuality.Clean => "깨끗한 물",
            WorldWaterQuality.Unsafe => "불결한 물",
            WorldWaterQuality.Foul => "썩은 물",
            _ => quality.ToString()
        };
    }

    private static string WeatherName(SurvivalWeatherType weather)
    {
        return weather switch
        {
            SurvivalWeatherType.Clear => "맑음",
            SurvivalWeatherType.Rain => "비",
            SurvivalWeatherType.Fog => "안개",
            SurvivalWeatherType.HeatWave => "폭염",
            SurvivalWeatherType.ColdSnap => "한파",
            SurvivalWeatherType.Storm => "폭우",
            _ => weather.ToString()
        };
    }
}

public sealed class DungeonDebugDefenseCommandProvider : IDungeonDebugCommandProvider
{
    private readonly InvasionThreatRuntime threat;
    private readonly InvasionDirectorRuntime director;
    private readonly IExteriorIncidentRuntime incidentRuntime;
    private readonly ICharacterWorldQuery characterWorld;

    public DungeonDebugDefenseCommandProvider(
        InvasionSceneRuntimeReferences invasionRuntimes,
        IExteriorIncidentRuntime incidentRuntime,
        ICharacterWorldQuery characterWorld)
    {
        invasionRuntimes = invasionRuntimes
            ?? throw new ArgumentNullException(nameof(invasionRuntimes));
        threat = invasionRuntimes.Threat
            ?? throw new InvalidOperationException(
                $"{nameof(DungeonDebugDefenseCommandProvider)} requires a loaded {nameof(InvasionThreatRuntime)}.");
        director = invasionRuntimes.Director
            ?? throw new InvalidOperationException(
                $"{nameof(DungeonDebugDefenseCommandProvider)} requires a loaded {nameof(InvasionDirectorRuntime)}.");
        this.incidentRuntime = incidentRuntime ?? throw new ArgumentNullException(nameof(incidentRuntime));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public IEnumerable<IDungeonDebugCommand> GetCommands()
    {
        yield return InvasionCommand("defense:invasion", "일반 침공 발동", boss: false);
        yield return InvasionCommand("defense:boss-invasion", "보스 침공 발동", boss: true);
        yield return new DelegateDungeonDebugCommand(
            "defense:set-threat",
            "위협도 설정",
            "현재 침공 위협도를 입력값으로 설정합니다.",
            DungeonDebugCategory.EventsDefense,
            DungeonDebugTargetKind.None,
            context =>
            {
                if (threat == null)
                {
                    return DungeonDebugCommandResult.Failed("침공 위협 Runtime이 없습니다.");
                }

                float value = Mathf.Max(0f, context.NumericValue);
                threat.DebugSetThreat(value);
                return DungeonDebugCommandResult.Succeeded($"위협도 {value:0.#}");
            },
            defaultNumericValue: 100f);
        foreach (ExteriorIncidentKind kind in Enum.GetValues(typeof(ExteriorIncidentKind)))
        {
            if (kind == ExteriorIncidentKind.None) continue;
            ExteriorIncidentKind captured = kind;
            yield return new DelegateDungeonDebugCommand(
                $"event:exterior:{captured}",
                $"외부 사건: {IncidentName(captured)}",
                "정상 외부 사건 런타임을 통해 사건을 시작합니다.",
                DungeonDebugCategory.EventsDefense,
                DungeonDebugTargetKind.None,
                _ => incidentRuntime.TryStartIncident(captured)
                    ? DungeonDebugCommandResult.Succeeded($"{IncidentName(captured)} 사건을 시작했습니다.")
                    : DungeonDebugCommandResult.Failed("사용 가능한 사건 지점이 없습니다."));
        }

        yield return new DelegateDungeonDebugCommand(
            "defense:heal-intruders",
            "활성 침입자 회복",
            "현재 살아 있는 모든 침입자를 완전히 회복합니다.",
            DungeonDebugCategory.EventsDefense,
            DungeonDebugTargetKind.None,
            _ =>
            {
                if (director == null)
                {
                    return DungeonDebugCommandResult.Failed("침공 Director가 없습니다.");
                }

                int count = 0;
                foreach (InvasionIntruderRuntime intruder in director.ActiveIntruders)
                {
                    CharacterActor actor = intruder?.IntruderActor;
                    if (actor == null || actor.IsDead) continue;
                    actor.Heal(actor.MaxHealth);
                    count++;
                }

                return DungeonDebugCommandResult.Succeeded($"{count}명 침입자를 회복했습니다.");
            });
        yield return new DelegateDungeonDebugCommand(
            "defense:kill-intruders",
            "활성 침입자 살해",
            "현재 살아 있는 모든 침입자를 살해합니다.",
            DungeonDebugCategory.EventsDefense,
            DungeonDebugTargetKind.None,
            _ =>
            {
                if (director == null)
                {
                    return DungeonDebugCommandResult.Failed("침공 Director가 없습니다.");
                }

                int count = 0;
                foreach (InvasionIntruderRuntime intruder in director.ActiveIntruders.ToArray())
                {
                    CharacterActor actor = intruder?.IntruderActor;
                    if (actor == null || actor.IsDead) continue;
                    actor.Die("디버그 침공 승리");
                    count++;
                }

                return DungeonDebugCommandResult.Succeeded($"{count}명 침입자를 처치했습니다.");
            },
            isDangerous: true);
        yield return new DelegateDungeonDebugCommand(
            "defense:resolve-victory",
            "침공 승리 처리",
            "활성 침입자를 정상 제압 흐름으로 종료합니다.",
            DungeonDebugCategory.EventsDefense,
            DungeonDebugTargetKind.None,
            _ =>
            {
                if (director == null)
                {
                    return DungeonDebugCommandResult.Failed("침공 Director가 없습니다.");
                }

                int count = 0;
                foreach (InvasionIntruderRuntime intruder in director.ActiveIntruders.ToArray())
                {
                    if (intruder == null)
                    {
                        continue;
                    }

                    intruder.ResolveSuppressedBy(null);
                    count++;
                }

                return count > 0
                    ? DungeonDebugCommandResult.Succeeded($"{count}명 침입자를 제압했습니다.")
                    : DungeonDebugCommandResult.Failed("활성 침공이 없습니다.");
            });
        yield return new DelegateDungeonDebugCommand(
            "defense:resolve-failure",
            "침공 실패 처리",
            "활성 침공을 방어 실패로 종료합니다.",
            DungeonDebugCategory.EventsDefense,
            DungeonDebugTargetKind.None,
            _ =>
            {
                if (director == null)
                {
                    return DungeonDebugCommandResult.Failed("침공 Director가 없습니다.");
                }

                CharacterActor owner = characterWorld.Characters
                    .FirstOrDefault(actor => actor != null && actor.IsOwner);
                int count = 0;
                foreach (InvasionIntruderRuntime intruder in director.ActiveIntruders.ToArray())
                {
                    if (intruder == null)
                    {
                        continue;
                    }

                    intruder.ResolveDefenseFailed(owner);
                    count++;
                }

                return count > 0
                    ? DungeonDebugCommandResult.Succeeded("침공을 방어 실패로 처리했습니다.")
                    : DungeonDebugCommandResult.Failed("활성 침공이 없습니다.");
            },
            isDangerous: true);
    }

    private IDungeonDebugCommand InvasionCommand(string id, string label, bool boss)
    {
        return new DelegateDungeonDebugCommand(
            id,
            label,
            "실제 위협 Runtime과 Director를 통해 정상 디펜스 흐름을 시작합니다.",
            DungeonDebugCategory.EventsDefense,
            DungeonDebugTargetKind.None,
            _ =>
            {
                if (threat == null)
                {
                    return DungeonDebugCommandResult.Failed("침공 위협 Runtime이 없습니다.");
                }

                if (boss)
                {
                    if (director == null)
                    {
                        return DungeonDebugCommandResult.Failed("침공 Director가 없습니다.");
                    }

                    director.ArmNextInvasionAsBoss();
                }

                return threat.ForceCandidateNow()
                    ? DungeonDebugCommandResult.Succeeded(boss ? "보스 침공을 시작했습니다." : "일반 침공을 시작했습니다.")
                    : DungeonDebugCommandResult.Failed("이미 침공 후보가 활성화되어 있습니다.");
            },
            isDangerous: boss);
    }

    private static string IncidentName(ExteriorIncidentKind kind)
    {
        return kind switch
        {
            ExteriorIncidentKind.MerchantCart => "상인 마차",
            ExteriorIncidentKind.Informant => "정보상",
            ExteriorIncidentKind.Thief => "도둑",
            ExteriorIncidentKind.InjuredReturnee => "부상 귀환자",
            _ => kind.ToString()
        };
    }
}

public sealed class DungeonDebugOverlayCommandProvider : IDungeonDebugCommandProvider
{
    private readonly IDungeonDebugModeService modeService;

    public DungeonDebugOverlayCommandProvider(IDungeonDebugModeService modeService)
    {
        this.modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
    }

    public IEnumerable<IDungeonDebugCommand> GetCommands()
    {
        foreach (DungeonDebugOverlayKind overlay in Enum.GetValues(typeof(DungeonDebugOverlayKind)))
        {
            DungeonDebugOverlayKind captured = overlay;
            yield return new DelegateDungeonDebugCommand(
                $"overlay:{captured}",
                OverlayName(captured),
                "화면 범위 디버그 표시를 켜거나 끕니다.",
                DungeonDebugCategory.Overlay,
                DungeonDebugTargetKind.None,
                _ =>
                {
                    bool enabled = !modeService.IsOverlayEnabled(captured);
                    modeService.SetOverlay(captured, enabled);
                    return DungeonDebugCommandResult.Succeeded(
                        $"{OverlayName(captured)} {(enabled ? "켜짐" : "꺼짐")}");
                },
                mutatesWorld: false);
        }

        yield return new DelegateDungeonDebugCommand(
            "overlay:scope",
            "표시 범위 전환",
            "선택 대상만 또는 화면 안 전체를 번갈아 표시합니다.",
            DungeonDebugCategory.Overlay,
            DungeonDebugTargetKind.None,
            _ =>
            {
                DungeonDebugOverlayScope next =
                    modeService.OverlayScope == DungeonDebugOverlayScope.SelectedOnly
                        ? DungeonDebugOverlayScope.VisibleWorld
                        : DungeonDebugOverlayScope.SelectedOnly;
                modeService.SetOverlayScope(next);
                return DungeonDebugCommandResult.Succeeded(
                    next == DungeonDebugOverlayScope.SelectedOnly
                        ? "선택 대상만 표시"
                        : "화면 안 전체 표시");
            },
            mutatesWorld: false);
    }

    private static string OverlayName(DungeonDebugOverlayKind overlay)
    {
        return overlay switch
        {
            DungeonDebugOverlayKind.Grid => "그리드 좌표·영역",
            DungeonDebugOverlayKind.GridOccupancy => "점유 레이어",
            DungeonDebugOverlayKind.Rooms => "방 경계",
            DungeonDebugOverlayKind.BuildingRanges => "시설 범위",
            DungeonDebugOverlayKind.Lighting => "조명 반경",
            DungeonDebugOverlayKind.CharacterAi => "AI 경로·목표",
            DungeonDebugOverlayKind.Hauling => "운반 계획",
            DungeonDebugOverlayKind.Wildlife => "야생동물 영역",
            DungeonDebugOverlayKind.WaterAndFilth => "물·오염",
            DungeonDebugOverlayKind.ExteriorZones => "외부 구역",
            DungeonDebugOverlayKind.Defense => "침입·저지·교전",
            _ => overlay.ToString()
        };
    }
}
