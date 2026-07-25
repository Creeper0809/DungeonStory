using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BuildingAbilityWorkCompletedHandler<TAbility> :
    IBuildingAbilityWorkCompletedHandler
    where TAbility : BuildingAbility
{
    private static readonly Type[] Types = { typeof(TAbility) };

    public IReadOnlyCollection<Type> AbilityTypes => Types;

    public int Apply(BuildingAbility ability, BuildingAbilityWorkContext context)
    {
        if (ability is not TAbility typedAbility)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} cannot handle '{ability?.GetType().FullName ?? "null"}'.");
        }

        return Apply(typedAbility, context);
    }

    protected abstract int Apply(
        TAbility ability,
        BuildingAbilityWorkContext context);
}

public sealed class ProductionBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingProductionAbility>
{
    protected override int Apply(
        BuildingProductionAbility ability,
        BuildingAbilityWorkContext context)
    {
        return ModularFacilityRuntimeEffects.ApplyProduction(
            context.Actor,
            context.Building,
            ability,
            context.WorkTypeId);
    }
}

public sealed class CleaningBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingCleaningAbility>
{
    protected override int Apply(
        BuildingCleaningAbility ability,
        BuildingAbilityWorkContext context)
    {
        return ModularFacilityRuntimeEffects.ApplyCleaning(
            context.Actor,
            context.Building,
            ability,
            context.WorkTypeId);
    }
}

public sealed class SecurityBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingSecurityAbility>
{
    protected override int Apply(
        BuildingSecurityAbility ability,
        BuildingAbilityWorkContext context)
    {
        return ModularFacilityRuntimeEffects.ApplySecurity(
            context.Actor,
            context.Building,
            ability,
            context.WorkTypeId);
    }
}

public sealed class ReceptionBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingReceptionAbility>
{
    protected override int Apply(
        BuildingReceptionAbility ability,
        BuildingAbilityWorkContext context)
    {
        if (!ability.SupportsExteriorWork(context.WorkTypeId)
            || context.Building is not ExteriorZoneMarker marker)
        {
            return 0;
        }

        marker.ApplyReceptionWork(
            ability.readinessGain,
            ability.firstImpressionBonus);
        if (context.Actor != null && !Mathf.Approximately(ability.moodBonus, 0f))
        {
            context.Actor.ApplyMoodFactor(
                $"exterior:reception:{marker.ZoneId}",
                "입구 응대를 마침",
                ability.moodBonus,
                ability.moodDurationSeconds,
                1);
        }

        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Reception,
            CharacterActivityOutcomes.Completed,
            $"{marker.DisplayName}에서 방문객 맞이 준비를 마쳤다.",
            marker,
            reasonCode: "exterior-reception",
            value: marker.ReceptionReadiness));
        return 0;
    }
}

public sealed class PatrolPostBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingPatrolPostAbility>
{
    protected override int Apply(
        BuildingPatrolPostAbility ability,
        BuildingAbilityWorkContext context)
    {
        if (!ability.SupportsExteriorWork(context.WorkTypeId)
            || context.Building is not ExteriorZoneMarker marker)
        {
            return 0;
        }

        marker.ApplyPatrolWork(
            ability.patrolReadinessGain,
            ability.incidentDetectionBonus);
        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Guard,
            CharacterActivityOutcomes.Completed,
            $"{marker.DisplayName} 순찰을 마쳐 외부 동선을 안전하게 했다.",
            marker,
            reasonCode: "exterior-patrol",
            value: marker.PatrolReadiness));
        return 0;
    }
}

public sealed class OutdoorRestBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingOutdoorRestAbility>
{
    protected override int Apply(
        BuildingOutdoorRestAbility ability,
        BuildingAbilityWorkContext context)
    {
        if (!ability.SupportsExteriorWork(context.WorkTypeId)
            || context.Building is not ExteriorZoneMarker marker)
        {
            return 0;
        }

        context.Actor?.ApplyMoodFactor(
            $"exterior:rest:{marker.ZoneId}",
            "바깥 공기를 쐼",
            ability.moodBonus,
            ability.moodDurationSeconds,
            1);
        context.Actor?.Lifecycle?.ApplyExpeditionRecovery(
            0f,
            0f,
            ability.stressRecovery);
        marker.RecordOutdoorRest();
        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Rest,
            CharacterActivityOutcomes.Completed,
            $"{marker.DisplayName}에서 잠깐 숨을 돌렸다.",
            marker,
            reasonCode: "exterior-rest",
            value: ability.moodBonus));
        return 0;
    }
}

public sealed class ExteriorMaintenanceBuildingAbilityHandler :
    BuildingAbilityWorkCompletedHandler<BuildingExteriorMaintenanceAbility>
{
    protected override int Apply(
        BuildingExteriorMaintenanceAbility ability,
        BuildingAbilityWorkContext context)
    {
        if (!ability.SupportsExteriorWork(context.WorkTypeId)
            || context.Building is not ExteriorZoneMarker marker)
        {
            return 0;
        }

        if (context.WorkTypeId == BuiltInWorkTypeIds.Clean)
        {
            marker.ApplyExteriorCleanWork(ability.cleanlinessGain);
            context.Actor?.AddActivity(CharacterActivityEvent.Work(
                FacilityWorkType.Clean,
                CharacterActivityOutcomes.Completed,
                $"{marker.DisplayName} 주변을 치워 외부 동선을 깨끗하게 했다.",
                marker,
                reasonCode: "exterior-clean",
                value: marker.Cleanliness));
            return 0;
        }

        marker.ApplyExteriorRepairWork(ability.damageReduction);
        context.Actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Repair,
            CharacterActivityOutcomes.Completed,
            $"{marker.DisplayName} 주변 손상을 보수했다.",
            marker,
            reasonCode: "exterior-repair",
            value: marker.Damage));
        return 0;
    }
}
