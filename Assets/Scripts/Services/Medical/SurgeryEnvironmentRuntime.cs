using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class SurgeryEnvironmentRuntime
{
    private readonly ISurgicalProcedureCatalog procedures;
    private readonly ISurgicalFacilityQuery facilities;
    private readonly IBuildingWorldQuery buildings;
    private readonly IWorkforceReplanService workforce;
    private readonly IGameClock clock;
    private readonly IEnvironmentalFieldQuery environmentalField;
    private readonly ISurgeryEnvironmentRiskEvaluator riskEvaluator;

    public SurgeryEnvironmentRuntime(
        SurgeryContentServices content,
        SurgeryWorldServices world,
        SurgeryResourceServices resources,
        SurgeryExecutionServices execution)
    {
        procedures = (content ?? throw new ArgumentNullException(nameof(content))).Procedures;
        facilities = content.Facilities;
        buildings = (world ?? throw new ArgumentNullException(nameof(world))).Buildings;
        workforce = (resources ?? throw new ArgumentNullException(nameof(resources))).Workforce;
        environmentalField = resources.EnvironmentalField;
        clock = (execution ?? throw new ArgumentNullException(nameof(execution))).Clock;
        riskEvaluator = execution.EnvironmentRisk;
    }

    public void TickWaiting(SurgeryOrder order, BuildableObject facility)
    {
        SurgeryEnvironmentRiskSnapshot snapshot = riskEvaluator.Evaluate(
            facility.centerPos,
            null,
            order.subject);
        if (!snapshot.Normal)
        {
            order.environmentStableSeconds = 0f;
            order.environmentWait.Set(
                SurgeryStatusCode.EnvironmentUnsafe,
                nextScalarValue: snapshot.Environment.TemperatureC,
                nextSecondaryScalarValue: snapshot.Environment.AirQuality,
                nextTertiaryScalarValue: snapshot.Environment.LightLevel,
                nextStage: order.environmentResumeStage);
            order.statusData.Set(
                SurgeryStatusCode.EnvironmentUnsafe,
                nextScalarValue: snapshot.Environment.TemperatureC,
                nextSecondaryScalarValue: snapshot.Environment.AirQuality,
                nextTertiaryScalarValue: snapshot.Environment.LightLevel,
                nextStage: order.environmentResumeStage);
            RequestRecovery(order, snapshot);
            return;
        }

        order.environmentStableSeconds += Mathf.Max(0f, clock.DeltaTime);
        order.environmentWait.Set(
            SurgeryStatusCode.EnvironmentStabilizing,
            nextScalarValue: order.environmentStableSeconds,
            nextStage: order.environmentResumeStage);
        order.statusData.Set(
            SurgeryStatusCode.EnvironmentStabilizing,
            nextScalarValue: order.environmentStableSeconds,
            nextStage: order.environmentResumeStage);
        if (order.environmentStableSeconds < 5f)
        {
            return;
        }

        order.state = order.environmentResumeStage;
        order.environmentStableSeconds = 0f;
        order.environmentWait.Set(SurgeryStatusCode.None);
        order.environmentRecovery.Set(
            SurgeryStatusCode.EnvironmentRecoveryIdle);
        order.statusData.Set(
            SurgeryStatusCode.EnvironmentRestored,
            nextStage: order.environmentResumeStage);
        workforce.RequestOneWorkerToReplanFor(
            BuiltInWorkTypeIds.Surgery,
            forceInterrupt: true);
    }

    public void EnterWait(
        SurgeryOrder order,
        SurgeryOrderState resumeStage,
        SurgeryEnvironmentRiskSnapshot snapshot)
    {
        order.state = SurgeryOrderState.EnvironmentWaiting;
        order.environmentResumeStage = resumeStage;
        order.environmentStableSeconds = 0f;
        order.environmentWait.Set(
            SurgeryStatusCode.EnvironmentUnsafe,
            nextScalarValue: snapshot.Environment.TemperatureC,
            nextSecondaryScalarValue: snapshot.Environment.AirQuality,
            nextTertiaryScalarValue: snapshot.Environment.LightLevel,
            nextStage: resumeStage);
        order.statusData.Set(
            SurgeryStatusCode.EnvironmentUnsafe,
            nextScalarValue: snapshot.Environment.TemperatureC,
            nextSecondaryScalarValue: snapshot.Environment.AirQuality,
            nextTertiaryScalarValue: snapshot.Environment.LightLevel,
            nextStage: resumeStage);
        order.doctorId = string.Empty;
        RequestRecovery(order, snapshot);
    }

    public bool IsEmergency(SurgeryOrder order)
    {
        if (order != null
            && procedures.TryGet(order.procedureId, out SurgicalProcedureSO procedure)
            && procedure.Urgency == MedicalProcedureUrgency.Emergency)
        {
            return true;
        }

        return order?.subject?.automaticEmergencyDefault == true
            || (order?.procedureId?.IndexOf(
                "emergency",
                StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
    }

    public int GetUrgency(SurgeryOrder order)
    {
        return order != null
            && procedures.TryGet(order.procedureId, out SurgicalProcedureSO procedure)
                ? (int)procedure.Urgency
                : IsEmergency(order)
                    ? (int)MedicalProcedureUrgency.Emergency
                    : (int)MedicalProcedureUrgency.Required;
    }

    public bool IsProcedureFamily(
        string procedureId,
        MedicalProcedureFamily family)
    {
        return procedures.TryGet(procedureId, out SurgicalProcedureSO procedure)
            && procedure.Family == family;
    }

    public float GetCurrentStageBoundary(SurgeryOrder order)
    {
        if (order == null)
        {
            return 0f;
        }

        return order.state switch
        {
            SurgeryOrderState.Anesthetizing => order.anesthesiaWork,
            SurgeryOrderState.Incision => order.anesthesiaWork + order.incisionWork,
            SurgeryOrderState.Procedure =>
                order.anesthesiaWork + order.incisionWork + order.procedureWork,
            SurgeryOrderState.Suturing => order.requiredWork,
            _ => order.completedWork
        };
    }

    public SurgeryOrderState GetNextClinicalStage(SurgeryOrderState state)
    {
        return state switch
        {
            SurgeryOrderState.Anesthetizing => SurgeryOrderState.Incision,
            SurgeryOrderState.Incision => SurgeryOrderState.Procedure,
            SurgeryOrderState.Procedure => SurgeryOrderState.Suturing,
            _ => SurgeryOrderState.Suturing
        };
    }

    public bool RecordClinicalStage(SurgeryOrder order, SurgeryOrderState state)
    {
        order.reachedClinicalStages ??= new List<SurgeryOrderState>();
        if (order.reachedClinicalStages.Contains(state))
        {
            return false;
        }

        order.reachedClinicalStages.Add(state);
        return true;
    }

    public void ApplyCurrentStageRisk(
        SurgeryOrder order,
        CharacterActor doctor,
        BuildableObject facility)
    {
        if (order != null && RecordClinicalStage(order, order.state))
        {
            ApplyRisk(order, doctor, facility);
        }
    }

    public void ApplyRisk(
        SurgeryOrder order,
        CharacterActor doctor,
        BuildableObject facility)
    {
        if (facility == null
            || !environmentalField.TryGetCell(facility.centerPos, out _))
        {
            throw new InvalidOperationException(
                $"Surgery environment cell is unavailable for '{order?.orderId}'.");
        }

        SurgeryEnvironmentRiskSnapshot snapshot = riskEvaluator.Evaluate(
            facility.centerPos,
            doctor,
            order.subject);
        order.risk = riskEvaluator.Apply(order.risk, snapshot, 0.25f);
        if (snapshot.Normal)
        {
            return;
        }

        RequestRecovery(order, snapshot);
        if (IsEmergency(order))
        {
            order.statusData.Set(
                SurgeryStatusCode.EmergencyProcedureContinuing,
                nextScalarValue: snapshot.Environment.TemperatureC,
                nextSecondaryScalarValue: snapshot.Environment.AirQuality,
                nextTertiaryScalarValue: snapshot.Environment.LightLevel,
                nextStage: order.state);
        }
    }

    private void RequestRecovery(
        SurgeryOrder order,
        SurgeryEnvironmentRiskSnapshot snapshot)
    {
        List<string> requests = new List<string>();
        if (snapshot.Environment.TemperatureC < 16f)
        {
            AddRecoveryRequest(
                order,
                requests,
                building =>
                {
                    BuildingThermalEmitterAbility ability =
                        building.BuildingData?.GetAbility<BuildingThermalEmitterAbility>();
                    return ability != null
                        && ability.mode is ThermalEmitterMode.Heat
                            or ThermalEmitterMode.Thermostat;
                },
                "heating/fuel",
                BuiltInWorkTypeIds.Refuel);
        }
        else if (snapshot.Environment.TemperatureC > 28f)
        {
            AddRecoveryRequest(
                order,
                requests,
                building =>
                {
                    BuildingThermalEmitterAbility ability =
                        building.BuildingData?.GetAbility<BuildingThermalEmitterAbility>();
                    return ability != null
                        && ability.mode is ThermalEmitterMode.Cool
                            or ThermalEmitterMode.Thermostat;
                },
                "cooling",
                BuiltInWorkTypeIds.Repair);
        }

        if (snapshot.Environment.AirQuality < 70f)
        {
            AddRecoveryRequest(
                order,
                requests,
                building => building.BuildingData?
                    .GetAbility<BuildingAirExchangeAbility>() != null,
                "ventilation",
                BuiltInWorkTypeIds.Repair);
        }
        if (snapshot.Environment.LightLevel < 70f)
        {
            AddRecoveryRequest(
                order,
                requests,
                building => building.BuildingData?
                    .GetAbility<BuildingLightingAbility>() != null,
                "lighting power/fuel",
                BuiltInWorkTypeIds.Refuel);
        }

        order.environmentRecovery.Set(
            requests.Count == 0
                ? SurgeryStatusCode.EnvironmentRecoveryIdle
                : SurgeryStatusCode.EnvironmentRecoveryRequested,
            nextCountValue: requests.Count);
    }

    private void AddRecoveryRequest(
        SurgeryOrder order,
        ICollection<string> requests,
        Func<BuildableObject, bool> predicate,
        string label,
        WorkTypeId workType)
    {
        BuildableObject source = FindRecoveryFacility(order, predicate);
        requests.Add($"{label} ({FormatFacility(source, order)})");
        workforce.RequestOneWorkerToReplanFor(workType, forceInterrupt: true);
    }

    private BuildableObject FindRecoveryFacility(
        SurgeryOrder order,
        Func<BuildableObject, bool> predicate)
    {
        BuildableObject surgeryFacility = buildings.Buildings.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.isDestroy
            && string.Equals(
                facilities.GetFacilityId(candidate),
                order?.facilityId,
                StringComparison.Ordinal));
        if (surgeryFacility == null)
        {
            return null;
        }

        return buildings.Buildings
            .Where(building => building != null && !building.isDestroy && predicate(building))
            .OrderBy(building => Mathf.Abs(building.centerPos.x - surgeryFacility.centerPos.x)
                + Mathf.Abs(building.centerPos.y - surgeryFacility.centerPos.y))
            .ThenBy(building => facilities.GetFacilityId(building))
            .FirstOrDefault();
    }

    private string FormatFacility(BuildableObject facility, SurgeryOrder order)
    {
        return facility == null
            ? $"no source facility; surgery room {order?.facilityId}"
            : $"{facility.BuildingData?.objectName ?? facility.name}"
                + $"; {facilities.GetFacilityId(facility)}";
    }
}
