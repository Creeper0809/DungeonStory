using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class OperationsFeatureSurfaceModel
{
    public string DaySummary { get; set; } = string.Empty;
    public string SettlementSummary { get; set; } = string.Empty;
    public string LatestSettlementSummary { get; set; } = string.Empty;
    public bool CanTakeEmergencyFunding { get; set; }
    public IReadOnlyList<OperationsRecruitmentRow> Recruitment { get; set; }
        = Array.Empty<OperationsRecruitmentRow>();
    public string RecruitmentSummary { get; set; } = string.Empty;
    public string SurvivalSummary { get; set; } = string.Empty;
    public IReadOnlyList<OperationsStatusRow> SurvivalRows { get; set; }
        = Array.Empty<OperationsStatusRow>();
    public string WasteSummary { get; set; } = string.Empty;
    public IReadOnlyList<OperationsWastePolicyRow> WastePolicies { get; set; }
        = Array.Empty<OperationsWastePolicyRow>();
    public string FlowSummary { get; set; } = string.Empty;
    public IReadOnlyList<OperationsStatusRow> FlowRows { get; set; }
        = Array.Empty<OperationsStatusRow>();
    public string ExteriorSummary { get; set; } = string.Empty;
    public IReadOnlyList<OperationsStatusRow> ExteriorRows { get; set; }
        = Array.Empty<OperationsStatusRow>();
    public IReadOnlyList<OperationsExteriorIncidentRow> ExteriorIncidents { get; set; }
        = Array.Empty<OperationsExteriorIncidentRow>();
    public string RunVariableSummary { get; set; } = string.Empty;
    public IReadOnlyList<OperationsStatusRow> RunVariableRows { get; set; }
        = Array.Empty<OperationsStatusRow>();
    public string MetaSummary { get; set; } = string.Empty;
    public IReadOnlyList<OperationsMetaUpgradeRow> MetaUpgrades { get; set; }
        = Array.Empty<OperationsMetaUpgradeRow>();
    public IReadOnlyList<OperationsMaintenancePolicyRow> MaintenancePolicies { get; set; }
        = Array.Empty<OperationsMaintenancePolicyRow>();
    public OperationsMaintenancePolicyRow SelectedMaintenancePolicy { get; set; }
    public IReadOnlyList<OperationsMaintenanceAssignmentRow> MaintenanceAssignments { get; set; }
        = Array.Empty<OperationsMaintenanceAssignmentRow>();
    public IReadOnlyList<OperationsStatusRow> MaintenanceOrders { get; set; }
        = Array.Empty<OperationsStatusRow>();
}

public sealed class OperationsRecruitmentRow
{
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool CanRecruit { get; set; }
    public bool IsRecruited { get; set; }
}

public sealed class OperationsStatusRow
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class OperationsWastePolicyRow
{
    public WasteOriginKind Origin { get; set; }
    public string OriginLabel { get; set; } = string.Empty;
    public string DispositionLabel { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public float MaximumFeedContamination { get; set; }
}

public sealed class OperationsExteriorIncidentRow
{
    public string IncidentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public bool CanExecutePrimaryAction { get; set; }
}

public sealed class OperationsMetaUpgradeRow
{
    public string UpgradeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsMaxLevel { get; set; }
}

public sealed class OperationsMaintenancePolicyRow
{
    public int Index { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public bool IsCustom { get; set; }
    public bool AutomaticRepair { get; set; }
    public float SendAtDurability { get; set; }
    public float ReturnAtDurability { get; set; }
    public bool AllowUnequipDuringInvasion { get; set; }
    public bool PreferReplacement { get; set; }
}

public sealed class OperationsMaintenanceAssignmentRow
{
    public int Index { get; set; }
    public int ActorRuntimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool UsesSelectedPolicy { get; set; }
}

public readonly struct OperationsFeatureCommandResult
{
    public OperationsFeatureCommandResult(bool succeeded, string message, string entityId = "")
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
        EntityId = entityId ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string Message { get; }
    public string EntityId { get; }
}

public interface IOperationsFeatureQueryService
{
    OperationsFeatureSurfaceModel Capture(string selectedMaintenancePolicyId);
}

public interface IOperationsFeatureCommandService
{
    OperationsFeatureCommandResult TakeEmergencyFunding();
    OperationsFeatureCommandResult Recruit(string customerId);
    OperationsFeatureCommandResult PurchaseMetaUpgrade(string upgradeId);
    OperationsFeatureCommandResult ToggleMaintenanceAutomaticRepair(string policyId);
    OperationsFeatureCommandResult StepMaintenanceSendAt(string policyId);
    OperationsFeatureCommandResult StepMaintenanceReturnAt(string policyId);
    OperationsFeatureCommandResult ToggleMaintenanceInvasionUnequip(string policyId);
    OperationsFeatureCommandResult ToggleMaintenanceReplacement(string policyId);
    OperationsFeatureCommandResult CreateMaintenancePolicy();
    OperationsFeatureCommandResult DuplicateMaintenancePolicy(string policyId);
    OperationsFeatureCommandResult DeleteMaintenancePolicy(string policyId);
    OperationsFeatureCommandResult AssignMaintenancePolicy(int actorRuntimeId, string policyId);
    OperationsFeatureCommandResult ExecuteExteriorIncident(string incidentId);
    OperationsFeatureCommandResult CycleWasteDisposition(WasteOriginKind origin);
    OperationsFeatureCommandResult StepWasteFeedContamination(WasteOriginKind origin);
    OperationsFeatureCommandResult ToggleWastePolicy(WasteOriginKind origin);
}

public sealed class OperationsFeatureQueryService : IOperationsFeatureQueryService
{
    private const int MaxVisibleCards = 8;

    private readonly IOperationTabSummaryService operationSummary;
    private readonly IOperatingDaySettlementRuntimeProvider settlementProvider;
    private readonly IRegularCustomerRuntimeProvider regularCustomerProvider;
    private readonly IMetaProgressionRuntimeProvider metaProvider;
    private readonly IRunVariableRuntimeProvider runVariableProvider;
    private readonly ISurvivalFoodRuntime survivalFood;
    private readonly IWildlifeQuery wildlifeQuery;
    private readonly IWildlifeEcosystemRuntime wildlifeEcosystem;
    private readonly IExteriorZoneQuery exteriorZones;
    private readonly IExteriorIncidentRuntime exteriorIncidents;
    private readonly ICombatEquipmentMaintenanceRuntime maintenanceRuntime;
    private readonly IStaffWorkforceQueryService workforceQuery;
    private readonly IGameplayFlowDiagnosticsQuery flowDiagnostics;
    private readonly IWasteProcessingRuntime wasteProcessing;

    public OperationsFeatureQueryService(
        IOperationTabSummaryService operationSummary,
        IOperatingDaySettlementRuntimeProvider settlementProvider,
        IRegularCustomerRuntimeProvider regularCustomerProvider,
        IMetaProgressionRuntimeProvider metaProvider,
        IRunVariableRuntimeProvider runVariableProvider,
        ISurvivalFoodRuntime survivalFood,
        IWildlifeQuery wildlifeQuery,
        IWildlifeEcosystemRuntime wildlifeEcosystem,
        IExteriorZoneQuery exteriorZones,
        IExteriorIncidentRuntime exteriorIncidents,
        ICombatEquipmentMaintenanceRuntime maintenanceRuntime,
        IStaffWorkforceQueryService workforceQuery,
        IGameplayFlowDiagnosticsQuery flowDiagnostics,
        IWasteProcessingRuntime wasteProcessing)
    {
        this.operationSummary = operationSummary
            ?? throw new ArgumentNullException(nameof(operationSummary));
        this.settlementProvider = settlementProvider
            ?? throw new ArgumentNullException(nameof(settlementProvider));
        this.regularCustomerProvider = regularCustomerProvider
            ?? throw new ArgumentNullException(nameof(regularCustomerProvider));
        this.metaProvider = metaProvider
            ?? throw new ArgumentNullException(nameof(metaProvider));
        this.runVariableProvider = runVariableProvider
            ?? throw new ArgumentNullException(nameof(runVariableProvider));
        this.survivalFood = survivalFood
            ?? throw new ArgumentNullException(nameof(survivalFood));
        this.wildlifeQuery = wildlifeQuery
            ?? throw new ArgumentNullException(nameof(wildlifeQuery));
        this.wildlifeEcosystem = wildlifeEcosystem
            ?? throw new ArgumentNullException(nameof(wildlifeEcosystem));
        this.exteriorZones = exteriorZones
            ?? throw new ArgumentNullException(nameof(exteriorZones));
        this.exteriorIncidents = exteriorIncidents
            ?? throw new ArgumentNullException(nameof(exteriorIncidents));
        this.maintenanceRuntime = maintenanceRuntime
            ?? throw new ArgumentNullException(nameof(maintenanceRuntime));
        this.workforceQuery = workforceQuery
            ?? throw new ArgumentNullException(nameof(workforceQuery));
        this.flowDiagnostics = flowDiagnostics
            ?? throw new ArgumentNullException(nameof(flowDiagnostics));
        this.wasteProcessing = wasteProcessing
            ?? throw new ArgumentNullException(nameof(wasteProcessing));
    }

    public OperationsFeatureSurfaceModel Capture(string selectedMaintenancePolicyId)
    {
        OperationTabSummary summary = operationSummary.Capture();
        settlementProvider.TryGetRuntime(out OperatingDaySettlementRuntime settlement);
        EquipmentMaintenancePolicyData selectedMaintenance =
            ResolveMaintenancePolicy(selectedMaintenancePolicyId);
        GameplayFlowDiagnosticsSnapshot flow = flowDiagnostics.Capture();
        return new OperationsFeatureSurfaceModel
        {
            DaySummary = summary.HasGameData
                ? $"Day {summary.Day} / {summary.Hour}:00 / 자금 {summary.HoldingMoney}"
                : "운영 시계를 불러오지 못했습니다.",
            SettlementSummary = CreateSettlementSummary(settlement),
            LatestSettlementSummary = CreateLatestSettlementSummary(settlement),
            CanTakeEmergencyFunding = settlement != null && settlement.CanTakeEmergencyFunding,
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
                        + $" / 일반 재료 {order.requiredGeneralMaterials}"
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

    private static string CreateLatestSettlementSummary(
        OperatingDaySettlementRuntime settlement)
    {
        OperatingDayReport report = settlement?.LatestReport;
        return report != null
            ? $"최근 Day {report.day}"
                + $" / 매출 {report.totalRevenue}"
                + $" / 운영비 {report.paidOperatingCost}/{report.totalOperatingCost}"
                + $" / 미납 {report.unpaidOperatingCost}"
                + $" / 사건 {report.incidents?.Count ?? 0}"
            : "아직 완료된 일일 정산이 없습니다.";
    }

    private IReadOnlyList<OperationsRecruitmentRow> CreateRecruitmentRows(
        out string summary)
    {
        if (!regularCustomerProvider.TryGetRuntime(out RegularCustomerRuntime runtime))
        {
            summary = "단골 기록을 불러오지 못했습니다.";
            return Array.Empty<OperationsRecruitmentRow>();
        }

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
        return records.Select(record => new OperationsRecruitmentRow
        {
            CustomerId = record.CustomerId,
            Name = record.DisplayName,
            Detail = $"{record.SpeciesTag}"
                + $" / 방문 {record.VisitCount}회"
                + $" / 만족도 {record.AverageSatisfaction:0.#}"
                + $" / {record.Status}"
                + $" / 역할 {RegularCustomerService.FormatCapabilities(record.RecruitCapabilities)}",
            CanRecruit = record.Status == RegularCustomerStatus.RecruitCandidate,
            IsRecruited = record.IsRecruited
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
        if (!runVariableProvider.TryGetRuntime(out RunVariableRuntime runtime))
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
        if (!metaProvider.TryGetRuntime(out MetaProgressionRuntime runtime))
        {
            rows = Array.Empty<OperationsMetaUpgradeRow>();
            return "계승 진행을 불러오지 못했습니다.";
        }

        MetaProgressionState state = runtime.State;
        rows = MetaProgressionCatalog.All
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

public sealed class OperationsFeatureCommandService : IOperationsFeatureCommandService
{
    private readonly IOperatingDaySettlementRuntimeProvider settlementProvider;
    private readonly IRegularCustomerRuntimeProvider regularCustomerProvider;
    private readonly IMetaProgressionRuntimeProvider metaProvider;
    private readonly ICombatEquipmentMaintenanceRuntime maintenanceRuntime;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IExteriorIncidentRuntime exteriorIncidents;
    private readonly IWasteProcessingRuntime wasteProcessing;

    public OperationsFeatureCommandService(
        IOperatingDaySettlementRuntimeProvider settlementProvider,
        IRegularCustomerRuntimeProvider regularCustomerProvider,
        IMetaProgressionRuntimeProvider metaProvider,
        ICombatEquipmentMaintenanceRuntime maintenanceRuntime,
        ICharacterWorldQuery characterWorld,
        IExteriorIncidentRuntime exteriorIncidents,
        IWasteProcessingRuntime wasteProcessing)
    {
        this.settlementProvider = settlementProvider
            ?? throw new ArgumentNullException(nameof(settlementProvider));
        this.regularCustomerProvider = regularCustomerProvider
            ?? throw new ArgumentNullException(nameof(regularCustomerProvider));
        this.metaProvider = metaProvider
            ?? throw new ArgumentNullException(nameof(metaProvider));
        this.maintenanceRuntime = maintenanceRuntime
            ?? throw new ArgumentNullException(nameof(maintenanceRuntime));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.exteriorIncidents = exteriorIncidents
            ?? throw new ArgumentNullException(nameof(exteriorIncidents));
        this.wasteProcessing = wasteProcessing
            ?? throw new ArgumentNullException(nameof(wasteProcessing));
    }

    public OperationsFeatureCommandResult TakeEmergencyFunding()
    {
        if (!settlementProvider.TryGetRuntime(out OperatingDaySettlementRuntime settlement))
        {
            return Missing("정산");
        }

        bool succeeded = settlement.TryTakeEmergencyFunding(out string message);
        return new OperationsFeatureCommandResult(succeeded, message);
    }

    public OperationsFeatureCommandResult Recruit(string customerId)
    {
        if (!regularCustomerProvider.TryGetRuntime(out RegularCustomerRuntime runtime))
        {
            return Missing("영입");
        }

        bool succeeded = runtime.TryRecruit(
            customerId,
            out RegularCustomerRecruitResult result);
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded
                ? $"영입 성공: {result.Record.DisplayName}"
                    + $" / 역할 {RegularCustomerService.FormatCapabilities(result.Capabilities)}"
                : $"영입 불가: {result.Message}");
    }

    public OperationsFeatureCommandResult PurchaseMetaUpgrade(string upgradeId)
    {
        if (!metaProvider.TryGetRuntime(out MetaProgressionRuntime runtime))
        {
            return Missing("계승 강화");
        }

        int beforeCurrency = runtime.State.AvailableCurrency;
        int beforeLevel = runtime.State.GetUpgradeLevel(upgradeId);
        bool succeeded = runtime.TryPurchaseUpgrade(upgradeId, out string message);
        return new OperationsFeatureCommandResult(
            succeeded,
            $"{message} / Lv.{beforeLevel}->{runtime.State.GetUpgradeLevel(upgradeId)}"
            + $" / 화폐 {beforeCurrency}->{runtime.State.AvailableCurrency}");
    }

    public OperationsFeatureCommandResult ToggleMaintenanceAutomaticRepair(string policyId)
    {
        return UpdateMaintenance(
            policyId,
            policy => policy.automaticRepair = !policy.automaticRepair);
    }

    public OperationsFeatureCommandResult StepMaintenanceSendAt(string policyId)
    {
        return UpdateMaintenance(
            policyId,
            policy => policy.sendAtDurability = StepRatio(policy.sendAtDurability));
    }

    public OperationsFeatureCommandResult StepMaintenanceReturnAt(string policyId)
    {
        return UpdateMaintenance(
            policyId,
            policy => policy.returnAtDurability = StepRatio(policy.returnAtDurability));
    }

    public OperationsFeatureCommandResult ToggleMaintenanceInvasionUnequip(string policyId)
    {
        return UpdateMaintenance(
            policyId,
            policy => policy.allowUnequipDuringInvasion =
                !policy.allowUnequipDuringInvasion);
    }

    public OperationsFeatureCommandResult ToggleMaintenanceReplacement(string policyId)
    {
        return UpdateMaintenance(
            policyId,
            policy => policy.preferReplacement = !policy.preferReplacement);
    }

    public OperationsFeatureCommandResult CreateMaintenancePolicy()
    {
        bool succeeded = maintenanceRuntime.TryCreatePolicy(
            $"새 장비 정책 {maintenanceRuntime.Policies.Count + 1}",
            out EquipmentMaintenancePolicyData created);
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded ? $"장비 정비 정책 생성: {created.displayName}" : "정책을 만들지 못했습니다.",
            created?.id);
    }

    public OperationsFeatureCommandResult DuplicateMaintenancePolicy(string policyId)
    {
        EquipmentMaintenancePolicyData source = FindMaintenancePolicy(policyId);
        EquipmentMaintenancePolicyData duplicate = null;
        bool succeeded = source != null
            && maintenanceRuntime.TryDuplicatePolicy(
                source.id,
                $"{source.displayName} 사본",
                out duplicate);
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded ? $"장비 정비 정책 복제: {duplicate.displayName}" : "정책을 복제하지 못했습니다.",
            succeeded ? duplicate.id : string.Empty);
    }

    public OperationsFeatureCommandResult DeleteMaintenancePolicy(string policyId)
    {
        bool succeeded = maintenanceRuntime.TryDeletePolicy(
            policyId,
            reassignToStandard: true);
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded ? "정책을 삭제하고 표준 정책으로 재배정했습니다." : "기본 정책은 삭제할 수 없습니다.",
            succeeded ? EquipmentMaintenancePolicyRuntime.StandardPolicyId : policyId);
    }

    public OperationsFeatureCommandResult AssignMaintenancePolicy(
        int actorRuntimeId,
        string policyId)
    {
        CharacterActor actor = characterWorld.Characters.FirstOrDefault(candidate =>
            candidate != null && candidate.GetInstanceID() == actorRuntimeId);
        if (actor == null)
        {
            return new OperationsFeatureCommandResult(false, "배정할 캐릭터를 찾지 못했습니다.");
        }

        bool succeeded = maintenanceRuntime.AssignPolicy(actor, policyId);
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded
                ? $"{actor.Identity?.DisplayName ?? actor.name}: 정비 정책 배정 완료"
                : "정비 정책을 배정하지 못했습니다.");
    }

    public OperationsFeatureCommandResult ExecuteExteriorIncident(
        string incidentId)
    {
        bool succeeded = exteriorIncidents.TryExecutePrimaryAction(
            incidentId,
            out string message);
        return new OperationsFeatureCommandResult(succeeded, message);
    }

    public OperationsFeatureCommandResult CycleWasteDisposition(
        WasteOriginKind origin)
    {
        WastePolicyData policy = wasteProcessing.GetPolicy(origin);
        WasteDispositionKind[] values =
            (WasteDispositionKind[])Enum.GetValues(typeof(WasteDispositionKind));
        int start = Array.IndexOf(values, policy.disposition);
        for (int offset = 1; offset <= values.Length; offset++)
        {
            policy.disposition = values[(start + offset) % values.Length];
            if (wasteProcessing.SetPolicy(policy, out _))
            {
                return new OperationsFeatureCommandResult(
                    true,
                    $"{FormatWasteOrigin(origin)} 처리 방식: "
                    + FormatWasteDisposition(policy.disposition));
            }
        }

        return new OperationsFeatureCommandResult(
            false,
            "선택 가능한 폐기 방식이 없습니다.");
    }

    public OperationsFeatureCommandResult StepWasteFeedContamination(
        WasteOriginKind origin)
    {
        WastePolicyData policy = wasteProcessing.GetPolicy(origin);
        float next = Mathf.Round(policy.maximumFeedContamination / 10f) * 10f
            + 10f;
        policy.maximumFeedContamination = next >= 80f ? 0f : next;
        bool succeeded = wasteProcessing.SetPolicy(
            policy,
            out string failureReason);
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded
                ? $"{FormatWasteOrigin(origin)} 급여 허용 오염도: "
                    + $"{policy.maximumFeedContamination:0}"
                : failureReason);
    }

    public OperationsFeatureCommandResult ToggleWastePolicy(
        WasteOriginKind origin)
    {
        WastePolicyData policy = wasteProcessing.GetPolicy(origin);
        policy.enabled = !policy.enabled;
        bool succeeded = wasteProcessing.SetPolicy(
            policy,
            out string failureReason);
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded
                ? $"{FormatWasteOrigin(origin)} 정책 "
                    + (policy.enabled ? "활성화" : "중지")
                : failureReason);
    }

    private OperationsFeatureCommandResult UpdateMaintenance(
        string policyId,
        Action<EquipmentMaintenancePolicyData> mutate)
    {
        EquipmentMaintenancePolicyData source = FindMaintenancePolicy(policyId);
        if (source == null)
        {
            return new OperationsFeatureCommandResult(false, "정비 정책을 찾지 못했습니다.");
        }

        EquipmentMaintenancePolicyData edited = source.Clone();
        mutate(edited);
        bool succeeded = maintenanceRuntime.TryUpdatePolicy(edited);
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded ? $"장비 정비 정책 갱신: {edited.displayName}" : "정책을 갱신하지 못했습니다.");
    }

    private EquipmentMaintenancePolicyData FindMaintenancePolicy(string policyId)
    {
        return maintenanceRuntime.Policies.FirstOrDefault(policy =>
            policy != null
            && string.Equals(policy.id, policyId, StringComparison.Ordinal));
    }

    private static float StepRatio(float current)
    {
        float next = Mathf.Round((Mathf.Clamp01(current) + 0.05f) * 20f) / 20f;
        return next > 1f ? 0f : next;
    }

    private static OperationsFeatureCommandResult Missing(string feature)
    {
        return new OperationsFeatureCommandResult(
            false,
            $"{feature} 시스템을 불러오지 못했습니다.");
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

public sealed class OperationsFeatureSurfacePresenter : IFeatureSurfaceTabPresenter
{
    private const float CompactCardHeight = 92f;

    private readonly IOperationsFeatureQueryService query;
    private readonly IOperationsFeatureCommandService commands;
    private readonly ICaptivityFeatureSectionPresenter captivitySection;
    private string selectedMaintenancePolicyId =
        EquipmentMaintenancePolicyRuntime.StandardPolicyId;
    private string pendingMaintenanceDeleteId = string.Empty;

    public OperationsFeatureSurfacePresenter(
        IOperationsFeatureQueryService query,
        IOperationsFeatureCommandService commands,
        ICaptivityFeatureSectionPresenter captivitySection)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.captivitySection = captivitySection
            ?? throw new ArgumentNullException(nameof(captivitySection));
    }

    public TabId Id => TabId.Operations;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        OperationsFeatureSurfaceModel model = query.Capture(
            selectedMaintenancePolicyId);
        if (model.SelectedMaintenancePolicy != null)
        {
            selectedMaintenancePolicyId = model.SelectedMaintenancePolicy.PolicyId;
        }

        view.AddSection("운영 정산", model.DaySummary);
        view.AddLabel(model.SettlementSummary, 18f, 52f);
        view.AddDataCard(
            "P0Action_OperationEmergencyFunding",
            model.CanTakeEmergencyFunding ? "긴급 융자" : "긴급 융자 사용됨",
            model.LatestSettlementSummary,
            model.CanTakeEmergencyFunding ? "실행" : "상태",
            () => Execute(view, commands.TakeEmergencyFunding),
            CompactCardHeight);

        AddRecruitment(view, model);
        captivitySection.Present(view);
        AddMaintenance(view, model);
        AddStatusSection(view, "작업·물류", model.FlowSummary, "P0State_Flow_", model.FlowRows);
        AddStatusSection(view, "생존", model.SurvivalSummary, "P0State_Survival_", model.SurvivalRows);
        AddWastePolicies(view, model);
        AddStatusSection(view, "외부 활동", model.ExteriorSummary, "P0State_Exterior_", model.ExteriorRows);
        AddExteriorIncidents(view, model);
        AddMeta(view, model);
        AddStatusSection(view, "런 변수", model.RunVariableSummary, "P1State_RunVariable_", model.RunVariableRows);
    }

    private void AddWastePolicies(
        IFeatureSurfaceView view,
        OperationsFeatureSurfaceModel model)
    {
        view.AddSection("부패물 처리", model.WasteSummary);
        foreach (OperationsWastePolicyRow row in model.WastePolicies)
        {
            OperationsWastePolicyRow captured = row;
            view.AddDataCard(
                $"EconomyWasteDisposition_{captured.Origin}",
                captured.OriginLabel,
                captured.Detail,
                captured.DispositionLabel,
                () => Execute(
                    view,
                    () => commands.CycleWasteDisposition(captured.Origin)),
                CompactCardHeight);
            view.AddDataCard(
                $"EconomyWasteContamination_{captured.Origin}",
                $"급여 오염 한도 {captured.MaximumFeedContamination:0}",
                "80 이상 독성 부패물은 한도와 관계없이 직접 급여하지 않습니다.",
                "+10",
                () => Execute(
                    view,
                    () => commands.StepWasteFeedContamination(captured.Origin)),
                CompactCardHeight);
            view.AddDataCard(
                $"EconomyWasteEnabled_{captured.Origin}",
                captured.Enabled ? "정책 사용 중" : "정책 중지됨",
                "중지하면 자동 급여와 처리 주문 생성을 모두 멈춥니다.",
                captured.Enabled ? "중지" : "사용",
                () => Execute(
                    view,
                    () => commands.ToggleWastePolicy(captured.Origin)),
                CompactCardHeight);
        }
    }

    private void AddExteriorIncidents(
        IFeatureSurfaceView view,
        OperationsFeatureSurfaceModel model)
    {
        if (model.ExteriorIncidents.Count == 0)
        {
            return;
        }

        view.AddSection("진행 중 외부 사건", $"{model.ExteriorIncidents.Count}건");
        foreach (OperationsExteriorIncidentRow row in model.ExteriorIncidents)
        {
            OperationsExteriorIncidentRow captured = row;
            view.AddDataCard(
                $"V16Action_ExteriorIncident_{captured.IncidentId}",
                captured.Title,
                captured.Detail,
                captured.ActionLabel,
                () =>
                {
                    if (!captured.CanExecutePrimaryAction)
                    {
                        view.ShowFeedback("사건이 진행 중입니다.");
                        return;
                    }

                    Execute(
                        view,
                        () => commands.ExecuteExteriorIncident(
                            captured.IncidentId));
                },
                CompactCardHeight);
        }
    }

    private void AddRecruitment(IFeatureSurfaceView view, OperationsFeatureSurfaceModel model)
    {
        view.AddSection("단골·영입", model.RecruitmentSummary);
        foreach (OperationsRecruitmentRow row in model.Recruitment)
        {
            OperationsRecruitmentRow captured = row;
            view.AddDataCard(
                $"P0Action_Recruit_{captured.CustomerId}",
                captured.Name,
                captured.Detail,
                captured.IsRecruited
                    ? "영입됨"
                    : captured.CanRecruit
                        ? "영입"
                        : "상태 확인",
                () => Execute(view, () => commands.Recruit(captured.CustomerId)),
                CompactCardHeight);
        }
    }

    private void AddMeta(IFeatureSurfaceView view, OperationsFeatureSurfaceModel model)
    {
        view.AddSection("계승 강화", model.MetaSummary);
        foreach (OperationsMetaUpgradeRow row in model.MetaUpgrades)
        {
            OperationsMetaUpgradeRow captured = row;
            view.AddDataCard(
                $"P0Action_MetaUpgrade_{captured.UpgradeId}",
                captured.Title,
                captured.Detail,
                captured.IsMaxLevel ? "최대" : "구매",
                () => Execute(
                    view,
                    () => commands.PurchaseMetaUpgrade(captured.UpgradeId)),
                CompactCardHeight);
        }
    }

    private void AddMaintenance(
        IFeatureSurfaceView view,
        OperationsFeatureSurfaceModel model)
    {
        OperationsMaintenancePolicyRow selected = model.SelectedMaintenancePolicy;
        view.AddSection(
            "장비 정비 정책",
            selected != null
                ? $"정책 {model.MaintenancePolicies.Count}개"
                    + $" / 선택 {selected.DisplayName}"
                    + $" / 수리 대기 {model.MaintenanceOrders.Count}건"
                : "사용 가능한 정책이 없습니다.");
        foreach (OperationsMaintenancePolicyRow row in model.MaintenancePolicies)
        {
            OperationsMaintenancePolicyRow captured = row;
            view.AddDataCard(
                $"V14Action_MaintenancePolicySelect_{captured.Index}",
                captured.DisplayName,
                captured.Detail,
                captured.IsSelected ? "선택됨" : "선택",
                () =>
                {
                    selectedMaintenancePolicyId = captured.PolicyId;
                    pendingMaintenanceDeleteId = string.Empty;
                    view.ShowFeedback($"장비 정비 정책 선택: {captured.DisplayName}");
                    view.RequestRefresh();
                },
                CompactCardHeight);
        }

        if (selected == null)
        {
            return;
        }

        AddMaintenanceCommand(
            view,
            "V14Action_MaintenanceAuto",
            selected.AutomaticRepair ? "자동 수리 끄기" : "자동 수리 켜기",
            "수리 임계값에 도달한 장비의 자동 정비를 전환합니다.",
            () => commands.ToggleMaintenanceAutomaticRepair(selected.PolicyId));
        AddMaintenanceCommand(
            view,
            "V14Action_MaintenanceSendAt",
            $"수리 보낼 내구도 {selected.SendAtDurability:P0}",
            "누를 때마다 5%씩 조정합니다.",
            () => commands.StepMaintenanceSendAt(selected.PolicyId));
        AddMaintenanceCommand(
            view,
            "V14Action_MaintenanceReturnAt",
            $"복귀 내구도 {selected.ReturnAtDurability:P0}",
            "수리가 끝났다고 판단할 내구도입니다.",
            () => commands.StepMaintenanceReturnAt(selected.PolicyId));
        AddMaintenanceCommand(
            view,
            "V14Action_MaintenanceInvasionUnequip",
            selected.AllowUnequipDuringInvasion
                ? "침공 중 탈착 허용"
                : "침공 중 탈착 금지",
            "교전 중 수리 장비를 벗길 수 있는지 결정합니다.",
            () => commands.ToggleMaintenanceInvasionUnequip(selected.PolicyId));
        AddMaintenanceCommand(
            view,
            "V14Action_MaintenanceReplacement",
            selected.PreferReplacement ? "대체 장비 우선" : "대체 장비 사용 안 함",
            "수리 중 같은 종류의 대체 장비를 우선합니다.",
            () => commands.ToggleMaintenanceReplacement(selected.PolicyId));
        AddMaintenanceCommand(
            view,
            "V14Action_MaintenanceCreate",
            "새 정책",
            "기본값으로 사용자 정책을 만듭니다.",
            commands.CreateMaintenancePolicy,
            selectReturnedEntity: true);
        AddMaintenanceCommand(
            view,
            "V14Action_MaintenanceDuplicate",
            "정책 복제",
            "현재 정책을 사용자 정책으로 복제합니다.",
            () => commands.DuplicateMaintenancePolicy(selected.PolicyId),
            selectReturnedEntity: true);

        if (selected.IsCustom)
        {
            bool confirming = string.Equals(
                pendingMaintenanceDeleteId,
                selected.PolicyId,
                StringComparison.Ordinal);
            view.AddDataCard(
                "V14Action_MaintenanceDelete",
                confirming ? "삭제 확정" : "정책 삭제",
                confirming
                    ? "배정된 캐릭터는 표준 정책으로 재배정됩니다."
                    : "한 번 더 눌러 삭제를 확정합니다.",
                "실행",
                () =>
                {
                    if (!confirming)
                    {
                        pendingMaintenanceDeleteId = selected.PolicyId;
                        view.ShowFeedback("정비 정책 삭제를 한 번 더 확인하세요.");
                    }
                    else
                    {
                        OperationsFeatureCommandResult result =
                            commands.DeleteMaintenancePolicy(selected.PolicyId);
                        selectedMaintenancePolicyId = result.Succeeded
                            ? EquipmentMaintenancePolicyRuntime.StandardPolicyId
                            : selectedMaintenancePolicyId;
                        pendingMaintenanceDeleteId = string.Empty;
                        view.ShowFeedback(result.Message);
                    }

                    view.RequestRefresh();
                },
                CompactCardHeight);
        }

        view.AddSection(
            "캐릭터별 장비 정책",
            $"대상 {model.MaintenanceAssignments.Count}명"
            + $" / 배정할 정책 {selected.DisplayName}");
        foreach (OperationsMaintenanceAssignmentRow assignment in model.MaintenanceAssignments)
        {
            OperationsMaintenanceAssignmentRow captured = assignment;
            view.AddDataCard(
                $"V14Action_MaintenanceAssign_{captured.Index}",
                captured.Name,
                captured.Detail,
                captured.UsesSelectedPolicy ? "배정됨" : "이 정책 배정",
                () => Execute(
                    view,
                    () => commands.AssignMaintenancePolicy(
                        captured.ActorRuntimeId,
                        selected.PolicyId)),
                CompactCardHeight);
        }

        AddStatusSection(
            view,
            "대장작업대 수리 대기열",
            $"활성 주문 {model.MaintenanceOrders.Count}건",
            "V14State_MaintenanceOrder_",
            model.MaintenanceOrders);
    }

    private void AddMaintenanceCommand(
        IFeatureSurfaceView view,
        string actionName,
        string title,
        string detail,
        Func<OperationsFeatureCommandResult> execute,
        bool selectReturnedEntity = false)
    {
        view.AddDataCard(
            actionName,
            title,
            detail,
            "실행",
            () =>
            {
                OperationsFeatureCommandResult result = execute();
                if (selectReturnedEntity
                    && result.Succeeded
                    && !string.IsNullOrWhiteSpace(result.EntityId))
                {
                    selectedMaintenancePolicyId = result.EntityId;
                }

                view.ShowFeedback(result.Message);
                view.RequestRefresh();
            },
            CompactCardHeight);
    }

    private static void AddStatusSection(
        IFeatureSurfaceView view,
        string title,
        string summary,
        string actionPrefix,
        IReadOnlyList<OperationsStatusRow> rows)
    {
        view.AddSection(title, summary);
        foreach (OperationsStatusRow row in rows)
        {
            OperationsStatusRow captured = row;
            view.AddDataCard(
                actionPrefix + captured.Index,
                captured.Title,
                captured.Detail,
                "상태",
                () => view.ShowFeedback(captured.Detail),
                CompactCardHeight);
        }
    }

    private static void Execute(
        IFeatureSurfaceView view,
        Func<OperationsFeatureCommandResult> execute)
    {
        OperationsFeatureCommandResult result = execute();
        view.ShowFeedback(result.Message);
        view.RequestRefresh();
    }
}
