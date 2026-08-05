using System;
using System.Linq;
using UnityEngine;

public sealed class OperationsFeatureCommandService : IOperationsFeatureCommandService
{
    private readonly RegularCustomerRuntime regularCustomers;
    private readonly MetaProgressionRuntime metaProgression;
    private readonly ICombatEquipmentMaintenanceRuntime maintenanceRuntime;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IExteriorIncidentRuntime exteriorIncidents;
    private readonly IWasteProcessingQuery wasteQuery;
    private readonly IWastePolicyCommand wastePolicyCommands;

    public OperationsFeatureCommandService(
        RegularCustomerRuntime regularCustomers,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        ICombatEquipmentMaintenanceRuntime maintenanceRuntime,
        ICharacterWorldQuery characterWorld,
        IExteriorIncidentRuntime exteriorIncidents,
        IWasteProcessingQuery wasteQuery,
        IWastePolicyCommand wastePolicyCommands)
    {
        this.regularCustomers = regularCustomers
            ?? throw new ArgumentNullException(nameof(regularCustomers));
        metaProgression = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .MetaProgression
            ?? throw new InvalidOperationException(
                $"{nameof(OperationsFeatureCommandService)} requires a loaded {nameof(MetaProgressionRuntime)}.");
        this.maintenanceRuntime = maintenanceRuntime
            ?? throw new ArgumentNullException(nameof(maintenanceRuntime));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.exteriorIncidents = exteriorIncidents
            ?? throw new ArgumentNullException(nameof(exteriorIncidents));
        this.wasteQuery = wasteQuery
            ?? throw new ArgumentNullException(nameof(wasteQuery));
        this.wastePolicyCommands = wastePolicyCommands
            ?? throw new ArgumentNullException(nameof(wastePolicyCommands));
    }

    public OperationsFeatureCommandResult Recruit(string customerId)
    {
        RegularCustomerRuntime runtime = regularCustomers;

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

    public OperationsFeatureCommandResult HireMercenary(string customerId)
    {
        RegularCustomerRuntime runtime = regularCustomers;

        bool succeeded = runtime.TryHireMercenary(
            customerId,
            out RegularCustomerRecruitResult result,
            out int firstDailyFee);
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded
                ? $"용병 계약: {result.Record.DisplayName}"
                    + $" / 선불 {firstDailyFee:N0}골드"
                : $"계약 불가: {result.Message}");
    }

    public OperationsFeatureCommandResult PurchaseMetaUpgrade(string upgradeId)
    {
        MetaProgressionRuntime runtime = metaProgression;
        if (runtime == null)
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
        WastePolicyData policy = wasteQuery.GetPolicy(origin);
        WasteDispositionKind[] values =
            (WasteDispositionKind[])Enum.GetValues(typeof(WasteDispositionKind));
        int start = Array.IndexOf(values, policy.disposition);
        for (int offset = 1; offset <= values.Length; offset++)
        {
            policy.disposition = values[(start + offset) % values.Length];
            if (wastePolicyCommands.SetPolicy(policy).Succeeded)
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
        WastePolicyData policy = wasteQuery.GetPolicy(origin);
        float next = Mathf.Round(policy.maximumFeedContamination / 10f) * 10f
            + 10f;
        policy.maximumFeedContamination = next >= 80f ? 0f : next;
        WastePolicyCommandResult result = wastePolicyCommands.SetPolicy(policy);
        bool succeeded = result.Succeeded;
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded
                ? $"{FormatWasteOrigin(origin)} 급여 허용 오염도: "
                    + $"{policy.maximumFeedContamination:0}"
                : result.Failure.Code.ToString());
    }

    public OperationsFeatureCommandResult ToggleWastePolicy(
        WasteOriginKind origin)
    {
        WastePolicyData policy = wasteQuery.GetPolicy(origin);
        policy.enabled = !policy.enabled;
        WastePolicyCommandResult result = wastePolicyCommands.SetPolicy(policy);
        bool succeeded = result.Succeeded;
        return new OperationsFeatureCommandResult(
            succeeded,
            succeeded
                ? $"{FormatWasteOrigin(origin)} 정책 "
                    + (policy.enabled ? "활성화" : "중지")
                : result.Failure.Code.ToString());
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
