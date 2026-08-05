using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class OperationsSceneContext
{
    public OperationsSceneContext(
        DungeonSceneRuntimeReferences sceneRuntimes,
        RegularCustomerRuntime regularCustomers,
        ProgressionSceneRuntimeReferences progressionRuntimes)
    {
        sceneRuntimes = sceneRuntimes
            ?? throw new ArgumentNullException(nameof(sceneRuntimes));
        progressionRuntimes = progressionRuntimes
            ?? throw new ArgumentNullException(nameof(progressionRuntimes));
        Settlement = sceneRuntimes.Settlement
            ?? throw new InvalidOperationException(
                $"{nameof(OperationsSceneContext)} requires a loaded {nameof(OperatingDaySettlementRuntime)}.");
        RegularCustomers = regularCustomers
            ?? throw new ArgumentNullException(nameof(regularCustomers));
        MetaProgression = progressionRuntimes.MetaProgression
            ?? throw new InvalidOperationException(
                $"{nameof(OperationsSceneContext)} requires a loaded {nameof(MetaProgressionRuntime)}.");
        RunVariables = sceneRuntimes.RunVariables
            ?? throw new InvalidOperationException(
                $"{nameof(OperationsSceneContext)} requires a loaded {nameof(RunVariableRuntime)}.");
    }

    public OperatingDaySettlementRuntime Settlement { get; }
    public RegularCustomerRuntime RegularCustomers { get; }
    public MetaProgressionRuntime MetaProgression { get; }
    public RunVariableRuntime RunVariables { get; }
}

public sealed class OperationsWorldContext
{
    public OperationsWorldContext(
        ISurvivalFoodQuery survivalFood,
        IWildlifeQuery wildlifeQuery,
        IWildlifeEcosystemRuntime wildlifeEcosystem,
        IExteriorZoneQuery exteriorZones,
        IExteriorIncidentRuntime exteriorIncidents,
        IWasteProcessingQuery wasteProcessing)
    {
        SurvivalFood = survivalFood
            ?? throw new ArgumentNullException(nameof(survivalFood));
        WildlifeQuery = wildlifeQuery
            ?? throw new ArgumentNullException(nameof(wildlifeQuery));
        WildlifeEcosystem = wildlifeEcosystem
            ?? throw new ArgumentNullException(nameof(wildlifeEcosystem));
        ExteriorZones = exteriorZones
            ?? throw new ArgumentNullException(nameof(exteriorZones));
        ExteriorIncidents = exteriorIncidents
            ?? throw new ArgumentNullException(nameof(exteriorIncidents));
        WasteProcessing = wasteProcessing
            ?? throw new ArgumentNullException(nameof(wasteProcessing));
    }

    public ISurvivalFoodQuery SurvivalFood { get; }
    public IWildlifeQuery WildlifeQuery { get; }
    public IWildlifeEcosystemRuntime WildlifeEcosystem { get; }
    public IExteriorZoneQuery ExteriorZones { get; }
    public IExteriorIncidentRuntime ExteriorIncidents { get; }
    public IWasteProcessingQuery WasteProcessing { get; }
}

public sealed class OperationsStaffContext
{
    public OperationsStaffContext(
        ICombatEquipmentMaintenanceRuntime maintenanceRuntime,
        IStaffWorkforceQueryService workforceQuery,
        IGameplayFlowDiagnosticsQuery flowDiagnostics)
    {
        MaintenanceRuntime = maintenanceRuntime
            ?? throw new ArgumentNullException(nameof(maintenanceRuntime));
        WorkforceQuery = workforceQuery
            ?? throw new ArgumentNullException(nameof(workforceQuery));
        FlowDiagnostics = flowDiagnostics
            ?? throw new ArgumentNullException(nameof(flowDiagnostics));
    }

    public ICombatEquipmentMaintenanceRuntime MaintenanceRuntime { get; }
    public IStaffWorkforceQueryService WorkforceQuery { get; }
    public IGameplayFlowDiagnosticsQuery FlowDiagnostics { get; }
}

public sealed class OperationsFeatureQueryService : IOperationsFeatureQueryService
{
    private const int MaxVisibleCards = 8;

    private readonly IOperationTabSummaryService operationSummary;
    private readonly OperatingDaySettlementRuntime settlement;
    private readonly RegularCustomerRuntime regularCustomers;
    private readonly MetaProgressionRuntime metaProgression;
    private readonly RunVariableRuntime runVariables;
    private readonly ISurvivalFoodQuery survivalFood;
    private readonly IWildlifeQuery wildlifeQuery;
    private readonly IWildlifeEcosystemRuntime wildlifeEcosystem;
    private readonly IExteriorZoneQuery exteriorZones;
    private readonly IExteriorIncidentRuntime exteriorIncidents;
    private readonly ICombatEquipmentMaintenanceRuntime maintenanceRuntime;
    private readonly IStaffWorkforceQueryService workforceQuery;
    private readonly IGameplayFlowDiagnosticsQuery flowDiagnostics;
    private readonly IWasteProcessingQuery wasteProcessing;

    public OperationsFeatureQueryService(
        IOperationTabSummaryService operationSummary,
        OperationsSceneContext scene,
        OperationsWorldContext world,
        OperationsStaffContext staff)
    {
        this.operationSummary = operationSummary
            ?? throw new ArgumentNullException(nameof(operationSummary));
        scene = scene ?? throw new ArgumentNullException(nameof(scene));
        world = world ?? throw new ArgumentNullException(nameof(world));
        staff = staff ?? throw new ArgumentNullException(nameof(staff));
        settlement = scene.Settlement;
        regularCustomers = scene.RegularCustomers;
        metaProgression = scene.MetaProgression;
        runVariables = scene.RunVariables;
        survivalFood = world.SurvivalFood;
        wildlifeQuery = world.WildlifeQuery;
        wildlifeEcosystem = world.WildlifeEcosystem;
        exteriorZones = world.ExteriorZones;
        exteriorIncidents = world.ExteriorIncidents;
        wasteProcessing = world.WasteProcessing;
        maintenanceRuntime = staff.MaintenanceRuntime;
        workforceQuery = staff.WorkforceQuery;
        flowDiagnostics = staff.FlowDiagnostics;
    }

    public OperationsFeatureSurfaceModel Capture(string selectedMaintenancePolicyId)
    {
        OperationTabSummary summary = operationSummary.Capture();
        EquipmentMaintenancePolicyData selectedMaintenance =
            ResolveMaintenancePolicy(selectedMaintenancePolicyId);
        GameplayFlowDiagnosticsSnapshot flow = flowDiagnostics.Capture();
        return new OperationsFeatureSurfaceModel
        {
            DaySummary = summary.HasGameData
                ? $"Day {summary.Day} / {summary.Hour}:00 / 자금 {summary.HoldingMoney}"
                : "운영 시계를 불러오지 못했습니다.",
            SettlementSummary = CreateSettlementSummary(settlement),
            Recruitment = CreateRecruitmentRows(out string recruitmentSummary),
            RecruitmentSummary = recruitmentSummary,
            SurvivalSummary = CreateSurvivalSummary(out IReadOnlyList<OperationsStatusRow> survivalRows),
            SurvivalRows = survivalRows,
            WasteSummary = CreateWasteSummary(out IReadOnlyList<OperationsWastePolicyRow> wasteRows),
            WastePolicies = wasteRows,
            FlowSummary = flow.Summary,
            FlowRows = flow.Items
                .Select((item, index) => new OperationsStatusRow
                {
                    Index = index,
                    Title = item.Title,
                    Detail = item.Detail
                })
                .ToArray(),
            ExteriorSummary = CreateExteriorSummary(out IReadOnlyList<OperationsStatusRow> exteriorRows),
            ExteriorRows = exteriorRows,
            ExteriorIncidents = exteriorIncidents.IncidentStates
                .Where(incident => incident != null && !incident.IsTerminal)
                .Take(MaxVisibleCards)
                .Select(incident => new OperationsExteriorIncidentRow
                {
                    IncidentId = incident.incidentId,
                    Title = FormatExteriorIncidentKind(incident.kind),
                    Detail = $"{FormatExteriorIncidentStage(incident.stage)}"
                        + $" / 남은 시간 {Mathf.CeilToInt(incident.remainingSeconds)}초"
                        + (incident.offerPrice > 0
                            ? $" / 가격 {incident.offerPrice}"
                            : string.Empty),
                    ActionLabel = incident.kind == ExteriorIncidentKind.MerchantCart
                        ? "화물 구매"
                        : "진행 중",
                    CanExecutePrimaryAction =
                        incident.kind == ExteriorIncidentKind.MerchantCart
                        && incident.receptionApplied
                })
                .ToArray(),
            RunVariableSummary = CreateRunVariableSummary(out IReadOnlyList<OperationsStatusRow> variableRows),
            RunVariableRows = variableRows,
            MetaSummary = CreateMetaSummary(out IReadOnlyList<OperationsMetaUpgradeRow> metaRows),
            MetaUpgrades = metaRows,
            MaintenancePolicies = maintenanceRuntime.Policies
                .Select((policy, index) => CreateMaintenancePolicyRow(
                    policy,
                    index,
                    selectedMaintenance?.id))
                .ToArray(),
            SelectedMaintenancePolicy = selectedMaintenance != null
                ? CreateMaintenancePolicyRow(
                    selectedMaintenance,
                    0,
                    selectedMaintenance.id)
                : null,
            MaintenanceAssignments = CreateMaintenanceAssignments(selectedMaintenance),
            MaintenanceOrders = maintenanceRuntime.Orders
                .Take(MaxVisibleCards)
                .Select((order, index) => new OperationsStatusRow
                {
                    Index = index,
                    Title = order.equipmentInstanceId,
                    Detail = $"{FormatMaintenanceState(order.state)}"
                        + $" / 진행 {order.ProgressRatio:P0}"
                        + $" / 재료 {order.requiredMaterialAmount}"
                        + $" / 작업 {order.completedWork:0.#}/{order.requiredWork:0.#}"
                })
                .ToArray()
        };
    }

    private string CreateWasteSummary(
        out IReadOnlyList<OperationsWastePolicyRow> rows)
    {
        WasteProcessingOverview overview = wasteProcessing.CaptureOverview();
        rows = wasteProcessing.Policies
            .Select(policy => new OperationsWastePolicyRow
            {
                Origin = policy.origin,
                OriginLabel = FormatWasteOrigin(policy.origin),
                DispositionLabel = FormatWasteDisposition(policy.disposition),
                Enabled = policy.enabled,
                MaximumFeedContamination = policy.maximumFeedContamination,
                Detail = $"{FormatWasteDisposition(policy.disposition)}"
                    + $" / 급여 허용 오염도 {policy.maximumFeedContamination:0}"
                    + (policy.enabled ? " / 사용 중" : " / 중지됨")
            })
            .ToArray();
        return $"식물성 {overview.PlantWaste}"
            + $" / 동물성 {overview.AnimalWaste}"
            + $" / 혼합 {overview.MixedWaste}"
            + $" / 금기 {overview.ForbiddenWaste}"
            + $" / 독성 {overview.ToxicWaste}"
            + $" / 처리 중 {overview.ProcessingBills}";
    }

    private static string CreateSettlementSummary(OperatingDaySettlementRuntime settlement)
    {
        if (settlement == null)
        {
            return "정산 정보를 불러오지 못했습니다.";
        }

        OperatingCostForecast forecast = settlement.CurrentOperatingCostForecast;
        string paymentState = forecast.CanPayInFull
            ? $"지급 후 {forecast.AvailableMoney - forecast.TotalDue}"
            : $"부족 {forecast.ExpectedShortfall}";
        return $"유지비 {forecast.MaintenanceCost}"
            + $" + 급여 {forecast.PayrollCost}"
            + $" + 미납 {forecast.OutstandingDebt}"
            + $" = {forecast.TotalDue} / {paymentState}";
    }

    private IReadOnlyList<OperationsRecruitmentRow> CreateRecruitmentRows(
        out string summary)
    {
        RegularCustomerRuntime runtime = regularCustomers;

        IReadOnlyList<RegularCustomerRecord> records = runtime.State.Records
            .OrderByDescending(record =>
                record.Status == RegularCustomerStatus.RecruitCandidate
                && !record.IsRecruited)
            .ThenBy(record => record.IsRecruited)
            .ThenByDescending(record => record.AverageSatisfaction)
            .ThenByDescending(record => record.VisitCount)
            .Take(MaxVisibleCards)
            .ToArray();
        summary = $"기록 {runtime.State.Records.Count}명"
            + $" / 후보 {runtime.State.Records.Count(record => record.Status == RegularCustomerStatus.RecruitCandidate)}명"
            + $" / 영입 완료 {runtime.State.RecruitedCharacters.Count}명";
        return records.Select(record =>
        {
            int mercenaryQuote =
                runtime.GetMercenaryQuote(record.CustomerId);
            return new OperationsRecruitmentRow
            {
                CustomerId = record.CustomerId,
                Name = record.DisplayName,
                Detail = $"{record.SpeciesTag}"
                    + $" / 방문 {record.VisitCount}회"
                    + $" / 만족도 {record.AverageSatisfaction:0.#}"
                    + $" / {record.Status}"
                    + $" / 역할 {RegularCustomerService.FormatCapabilities(record.RecruitCapabilities)}",
                CanRecruit =
                    record.Status
                    == RegularCustomerStatus.RecruitCandidate,
                CanHireMercenary =
                    record.Status
                    == RegularCustomerStatus.RecruitCandidate
                    && mercenaryQuote > 0,
                MercenaryFirstDailyFee = mercenaryQuote,
                IsRecruited = record.IsRecruited
            };
        }).ToArray();
    }

    private string CreateSurvivalSummary(out IReadOnlyList<OperationsStatusRow> rows)
    {
        SurvivalFoodOverview overview = survivalFood.GetOverview();
        IReadOnlyList<WildlifeActor> wildlife = wildlifeQuery.Wildlife;
        WildlifeEcosystemOverview ecosystem = wildlifeEcosystem.GetOverview(wildlife);
        rows = new[]
        {
            new OperationsStatusRow
            {
                Index = 0,
                Title = overview.ShortageDays < 2 ? "식량 부족 위험" : "식량",
                Detail = $"필요 {overview.TodayRequired}"
                    + $" / 창고 {overview.StoredFood}"
                    + $" / 바닥 {overview.LooseFood}"
                    + $" / 사체 {overview.CarcassCount}"
                    + $" / 부패 임박 {overview.SpoilageWarningCount}"
            },
            new OperationsStatusRow
            {
                Index = 1,
                Title = overview.WaterShortageDays < 2 ? "물 부족 위험" : "물·연료·약품",
                Detail = $"물 {overview.StoredWater + overview.LooseWater}"
                    + $" / 연료 {overview.StoredFuel}"
                    + $" / 약품 {overview.StoredMedicine}"
                    + $" / 환자 {overview.SickCount}"
                    + $" / 미치료 {overview.UntreatedCount}"
            },
            new OperationsStatusRow
            {
                Index = 2,
                Title = "야생 생태",
                Detail = $"동물 {ecosystem.AliveWildlifeCount}/{ecosystem.DesiredWildlifeCount}"
                    + $" / 먹이 {ecosystem.FoodAbundance01:P0}"
                    + $" / 물 {ecosystem.WaterAbundance01:P0}"
                    + $" / 포식자 위험 {ecosystem.PredatorDanger01:P0}"
            },
            new OperationsStatusRow
            {
                Index = 3,
                Title = "환경",
                Detail = $"{overview.Weather} {overview.OutdoorTemperature:0.#}도"
                    + $" / 위생 위험 {overview.SanitationRisk:0}"
                    + $" / 질병 위험 {overview.DiseaseRisk:0}"
                    + $" / 야간 위험 {overview.ExteriorNightDanger:0}"
            }
        };
        return $"오늘 식량 {overview.TodayRequired}"
            + $" / 물 {overview.TodayRequiredWater}"
            + $" / 야생동물 {ecosystem.AliveWildlifeCount}마리";
    }

    private string CreateExteriorSummary(out IReadOnlyList<OperationsStatusRow> rows)
    {
        ExteriorActivityOverviewSnapshot overview = exteriorZones.GetOverview();
        rows = exteriorZones.Zones
            .Where(zone => zone != null)
            .Take(MaxVisibleCards)
            .Select((zone, index) => new OperationsStatusRow
            {
                Index = index,
                Title = zone.DisplayName,
                Detail = $"{zone.ZoneType} ({zone.GridPosition.x},{zone.GridPosition.y})"
                    + $" / 청결 {zone.Cleanliness:0}"
                    + $" / 손상 {zone.Damage:0}"
                    + (zone.HasActiveIncident
                        ? $" / 사건 {zone.ActiveIncidentText}"
                        : string.Empty)
            })
            .ToArray();
        return $"구역 {overview.ZoneCount}"
            + $" / 하차장 {overview.DropZoneCount}"
            + $" / 사건 {overview.IncidentCount}"
            + $" / 평균 청결 {overview.AverageCleanliness:0}"
            + $" / 순찰 {overview.AveragePatrolReadiness:0}";
    }

    private string CreateRunVariableSummary(
        out IReadOnlyList<OperationsStatusRow> rows)
    {
        RunVariableRuntime runtime = runVariables;
        if (runtime == null)
        {
            rows = Array.Empty<OperationsStatusRow>();
            return "런 변수를 불러오지 못했습니다.";
        }

        IRunVariableStateView state = runtime.State;
        List<OperationsStatusRow> result = new List<OperationsStatusRow>();
        if (state.StartVariables != null)
        {
            result.Add(new OperationsStatusRow
            {
                Index = result.Count,
                Title = state.HasStarted ? "이번 런" : "런 시작 대기",
                Detail = state.StartVariables.ToSummaryText().Replace("\n", " / ")
            });
        }

        foreach (ActiveRunVariable variable in state.ActiveOperationVariables.Take(
                     MaxVisibleCards - result.Count))
        {
            result.Add(new OperationsStatusRow
            {
                Index = result.Count,
                Title = variable.Definition.title,
                Detail = $"남은 기간 {variable.RemainingDays}일 / {variable.Definition.detail}"
            });
        }

        if (state.CurrentInvasionVariable != null && result.Count < MaxVisibleCards)
        {
            result.Add(new OperationsStatusRow
            {
                Index = result.Count,
                Title = state.CurrentInvasionVariable.title,
                Detail = state.CurrentInvasionVariable.detail
            });
        }

        rows = result;
        return $"시작 {(state.HasStarted ? "완료" : "대기")}"
            + $" / 운영 변수 {state.ActiveOperationVariables.Count}"
            + $" / 침공 {state.CurrentInvasionVariable?.title ?? "없음"}";
    }

    private string CreateMetaSummary(out IReadOnlyList<OperationsMetaUpgradeRow> rows)
    {
        MetaProgressionRuntime runtime = metaProgression;
        if (runtime == null)
        {
            rows = Array.Empty<OperationsMetaUpgradeRow>();
            return "계승 진행을 불러오지 못했습니다.";
        }

        MetaProgressionState state = runtime.State;
        rows = state.Catalog.All
            .OrderBy(definition => definition.branch)
            .ThenBy(definition => definition.id)
            .Select(definition =>
            {
                int level = state.GetUpgradeLevel(definition.id);
                return new OperationsMetaUpgradeRow
                {
                    UpgradeId = definition.id,
                    Title = definition.title,
                    Detail = $"{definition.branch}"
                        + $" / Lv.{level}/{definition.maxLevel}"
                        + $" / 비용 {definition.cost}\n{definition.detail}",
                    IsMaxLevel = level >= definition.maxLevel
                };
            })
            .ToArray();
        return $"보유 화폐 {state.AvailableCurrency}"
            + $" / 누적 {state.LifetimeEarnedCurrency}"
            + $" / 사용 {state.SpentCurrency}";
    }

    private EquipmentMaintenancePolicyData ResolveMaintenancePolicy(string policyId)
    {
        return maintenanceRuntime.Policies.FirstOrDefault(policy =>
                   policy != null
                   && string.Equals(policy.id, policyId, StringComparison.Ordinal))
            ?? maintenanceRuntime.Policies.FirstOrDefault();
    }

    private static OperationsMaintenancePolicyRow CreateMaintenancePolicyRow(
        EquipmentMaintenancePolicyData policy,
        int index,
        string selectedPolicyId)
    {
        return new OperationsMaintenancePolicyRow
        {
            Index = index,
            PolicyId = policy.id,
            DisplayName = policy.displayName,
            Detail = policy.automaticRepair
                ? $"자동 수리 / {policy.sendAtDurability:P0}에 보내고"
                    + $" {policy.returnAtDurability:P0}에 복귀"
                    + $" / 침공 중 탈착 {(policy.allowUnequipDuringInvasion ? "허용" : "금지")}"
                : "수동 수리만",
            IsSelected = string.Equals(policy.id, selectedPolicyId, StringComparison.Ordinal),
            IsCustom = IsCustomMaintenancePolicy(policy.id),
            AutomaticRepair = policy.automaticRepair,
            SendAtDurability = policy.sendAtDurability,
            ReturnAtDurability = policy.returnAtDurability,
            AllowUnequipDuringInvasion = policy.allowUnequipDuringInvasion,
            PreferReplacement = policy.preferReplacement
        };
    }

    private IReadOnlyList<OperationsMaintenanceAssignmentRow> CreateMaintenanceAssignments(
        EquipmentMaintenancePolicyData selected)
    {
        if (selected == null)
        {
            return Array.Empty<OperationsMaintenanceAssignmentRow>();
        }

        return workforceQuery.FindActiveWorkers()
            .Where(actor => actor != null && !actor.IsDead)
            .Select((actor, index) =>
            {
                EquipmentMaintenancePolicyData assigned =
                    maintenanceRuntime.GetPolicy(actor);
                return new OperationsMaintenanceAssignmentRow
                {
                    Index = index,
                    ActorRuntimeId = actor.GetInstanceID(),
                    Name = actor.Identity?.DisplayName ?? actor.name,
                    Detail = $"현재 {assigned?.displayName ?? "표준"}",
                    UsesSelectedPolicy = string.Equals(
                        assigned?.id,
                        selected.id,
                        StringComparison.Ordinal)
                };
            })
            .ToArray();
    }

    private static bool IsCustomMaintenancePolicy(string policyId)
    {
        return policyId is not EquipmentMaintenancePolicyRuntime.StandardPolicyId
            and not EquipmentMaintenancePolicyRuntime.PreventivePolicyId
            and not EquipmentMaintenancePolicyRuntime.ManualPolicyId;
    }

    private static string FormatMaintenanceState(
        CombatEquipmentRepairOrderState state)
    {
        return state switch
        {
            CombatEquipmentRepairOrderState.PendingCombatEnd => "교전 종료 대기",
            CombatEquipmentRepairOrderState.WaitingForDelivery => "운반 대기",
            CombatEquipmentRepairOrderState.Ready => "수리 준비",
            CombatEquipmentRepairOrderState.InProgress => "수리 중",
            CombatEquipmentRepairOrderState.Completed => "완료",
            _ => "취소"
        };
    }

    private static string FormatExteriorIncidentKind(ExteriorIncidentKind kind)
    {
        return kind switch
        {
            ExteriorIncidentKind.MerchantCart => "상인 마차",
            ExteriorIncidentKind.Informant => "정보상",
            ExteriorIncidentKind.Thief => "도둑",
            ExteriorIncidentKind.InjuredReturnee => "부상 귀환자",
            ExteriorIncidentKind.PredatorApproach => "포식자 접근",
            ExteriorIncidentKind.CargoDamage => "화물 훼손 위험",
            _ => "외부 사건"
        };
    }

    private static string FormatExteriorIncidentStage(ExteriorIncidentStage stage)
    {
        return stage switch
        {
            ExteriorIncidentStage.Preparing => "접근 중",
            ExteriorIncidentStage.Active => "대응 중",
            ExteriorIncidentStage.Interacting => "선택 대기",
            ExteriorIncidentStage.Resolved => "해결",
            ExteriorIncidentStage.Failed => "실패",
            ExteriorIncidentStage.TimedOut => "시간 만료",
            _ => "진행 중"
        };
    }

    private static string FormatWasteOrigin(WasteOriginKind origin)
    {
        return origin switch
        {
            WasteOriginKind.Plant => "식물성 부패물",
            WasteOriginKind.Animal => "동물성 부패물",
            WasteOriginKind.Mixed => "혼합 부패물",
            WasteOriginKind.Forbidden => "금기 부패물",
            _ => "원산지 불명"
        };
    }

    private static string FormatWasteDisposition(WasteDispositionKind disposition)
    {
        return disposition switch
        {
            WasteDispositionKind.Store => "보관",
            WasteDispositionKind.DirectFeed => "직접 급여",
            WasteDispositionKind.Compost => "퇴비화",
            WasteDispositionKind.Fuel => "연료화",
            WasteDispositionKind.Alchemy => "연금 가공",
            WasteDispositionKind.Incinerate => "소각",
            _ => "처리 안 함"
        };
    }
}
