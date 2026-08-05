using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OperationsFeatureSurfaceModel
{
    public string DaySummary { get; set; } = string.Empty;
    public string SettlementSummary { get; set; } = string.Empty;
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OperationsRecruitmentRow
{
    public string CustomerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool CanRecruit { get; set; }
    public bool CanHireMercenary { get; set; }
    public int MercenaryFirstDailyFee { get; set; }
    public bool IsRecruited { get; set; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OperationsStatusRow
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OperationsWastePolicyRow
{
    public WasteOriginKind Origin { get; set; }
    public string OriginLabel { get; set; } = string.Empty;
    public string DispositionLabel { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public float MaximumFeedContamination { get; set; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OperationsExteriorIncidentRow
{
    public string IncidentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
    public bool CanExecutePrimaryAction { get; set; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OperationsMetaUpgradeRow
{
    public string UpgradeId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsMaxLevel { get; set; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OperationsMaintenanceAssignmentRow
{
    public int Index { get; set; }
    public int ActorRuntimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool UsesSelectedPolicy { get; set; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IOperationsFeatureQueryService
{
    OperationsFeatureSurfaceModel Capture(string selectedMaintenancePolicyId);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IOperationsFeatureCommandService
{
    OperationsFeatureCommandResult Recruit(string customerId);
    OperationsFeatureCommandResult HireMercenary(string customerId);
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface ICaptivityFeatureSectionPresenter
{
    void Present(IFeatureSurfaceView view);
}


[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class OperationsFeatureSurfacePresenter : IFeatureSurfaceTabPresenter
{
    private const float CompactCardHeight = 92f;

    private readonly IOperationsFeatureQueryService query;
    private readonly IOperationsFeatureCommandService commands;
    private readonly ICaptivityFeatureSectionPresenter captivitySection;
    private string selectedMaintenancePolicyId =
        EquipmentMaintenancePolicyIds.Standard;
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
            if (captured.CanHireMercenary)
            {
                view.AddDataCard(
                    $"TreasuryAction_HireMercenary_{captured.CustomerId}",
                    $"{captured.Name} 용병 계약",
                    $"고용 당일 첫 일급을 선불로 지급합니다."
                    + $" 이후 매일 {captured.MercenaryFirstDailyFee:N0}골드 안팎을 갱신합니다.",
                    $"{captured.MercenaryFirstDailyFee:N0}골드",
                    () => Execute(
                        view,
                        () => commands.HireMercenary(
                            captured.CustomerId)),
                    CompactCardHeight);
            }
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
                            ? EquipmentMaintenancePolicyIds.Standard
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
