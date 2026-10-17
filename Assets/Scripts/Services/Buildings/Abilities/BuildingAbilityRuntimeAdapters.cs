using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class BuildingCoverAbilityRuntime : IBuildingRuntimeStateAbility, IBuildingVisualRuntimeAbility
{
    private readonly BuildingCoverAbility definition;

    internal BuildingCoverAbilityRuntime(BuildingCoverAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public IBuildingStateModule CreateStateModule(BuildableObject building)
    {
        return CombatCoverDurability.Ensure(
            building,
            definition,
            RequireCombatCoverDurabilityRegistry(building));
    }

    public void ConfigureVisual(BuildableObject building)
    {
        CombatCoverDurability.Ensure(
            building,
            definition,
            RequireCombatCoverDurabilityRegistry(building));
    }

    private static ICombatCoverDurabilityRegistry
        RequireCombatCoverDurabilityRegistry(BuildableObject building)
    {
        return building.RequireCoverDurabilityRegistry()
            as ICombatCoverDurabilityRegistry
            ?? throw new InvalidOperationException(
                $"{nameof(BuildingCoverAbility)} requires a combat cover durability adapter.");
    }
}

internal sealed class BuildingStructuralIntegrityAbilityRuntime : IBuildingRuntimeStateAbility, IBuildingVisualRuntimeAbility
{
    private readonly BuildingStructuralIntegrityAbility definition;

    internal BuildingStructuralIntegrityAbilityRuntime(BuildingStructuralIntegrityAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public IBuildingStateModule CreateStateModule(BuildableObject building)
    {
        return BuildingStructuralIntegrity.Ensure(building, definition);
    }

    public void ConfigureVisual(BuildableObject building)
    {
        BuildingStructuralIntegrity.Ensure(building, definition);
    }
}

internal sealed class BuildingProductionAbilityRuntime : IBuildingRuntimeStateAbility
{
    private readonly BuildingProductionAbility definition;

    internal BuildingProductionAbilityRuntime(BuildingProductionAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public IBuildingStateModule CreateStateModule(BuildableObject building)
    {
        return new BuildingProductionStateModule(building, definition);
    }
}

internal sealed class BuildingLightingAbilityRuntime : IBuildingVisualRuntimeAbility
{
    private readonly BuildingLightingAbility definition;

    internal BuildingLightingAbilityRuntime(BuildingLightingAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public void ConfigureVisual(BuildableObject building)
    {
        ModularFacilityRuntimeEffects.ConfigureLighting(building, definition);
    }
}

internal sealed class BuildingWorkAmountAbilityRuntime : IBuildingWorkAmountRuntimeAbility
{
    private readonly BuildingWorkAmountAbility definition;

    internal BuildingWorkAmountAbilityRuntime(BuildingWorkAmountAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public float GetRequiredWork(BuildableObject building, WorkTypeId workTypeId)
    {
        return WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            ? this.definition.GetConfiguredRequiredWork(FacilityWorkTypeMap.GetRequired(definition))
            : 0f;
    }
}

internal sealed class BuildingExpeditionRecoveryAbilityRuntime : IBuildingUseCompletedRuntimeAbility
{
    private readonly BuildingExpeditionRecoveryAbility definition;

    internal BuildingExpeditionRecoveryAbilityRuntime(BuildingExpeditionRecoveryAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public void ApplyUseCompleted(IBuildingVisitorPort actor, BuildableObject building)
    {
        actor?.ApplyExpeditionRecovery(
            definition.healthHealRatio,
            definition.injuryReduction,
            definition.stressRecovery);
    }
}

internal sealed class BuildingReceptionAbilityRuntime : IBuildingExteriorWorkRuntimeAbility
{
    private readonly BuildingReceptionAbility definition;

    internal BuildingReceptionAbilityRuntime(BuildingReceptionAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }
    public bool SupportsExteriorWork(WorkTypeId workTypeId) => definition.SupportsExteriorWork(workTypeId);

    public bool IsExteriorWorkAvailable(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId)
            && building is ExteriorZoneMarker marker
            && marker.CanRunReceptionWork;
    }

    public float GetExteriorWorkSeconds(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId) ? Mathf.Max(0.1f, definition.workSeconds) : 0f;
    }

    public float GetExteriorWorkUrgency(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!IsExteriorWorkAvailable(actor, building, workTypeId)
            || building is not ExteriorZoneMarker marker)
        {
            return 0f;
        }

        return marker.GetReceptionUrgency();
    }
}

internal sealed class BuildingPatrolPostAbilityRuntime : IBuildingExteriorWorkRuntimeAbility
{
    private readonly BuildingPatrolPostAbility definition;

    internal BuildingPatrolPostAbilityRuntime(BuildingPatrolPostAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }
    public bool SupportsExteriorWork(WorkTypeId workTypeId) => definition.SupportsExteriorWork(workTypeId);

    public bool IsExteriorWorkAvailable(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId)
            && building is ExteriorZoneMarker marker
            && marker.CanRunPatrolWork;
    }

    public float GetExteriorWorkSeconds(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId) ? Mathf.Max(0.1f, definition.workSeconds) : 0f;
    }

    public float GetExteriorWorkUrgency(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!IsExteriorWorkAvailable(actor, building, workTypeId)
            || building is not ExteriorZoneMarker marker)
        {
            return 0f;
        }

        return marker.GetPatrolUrgency();
    }
}

internal sealed class BuildingOutdoorRestAbilityRuntime : IBuildingExteriorWorkRuntimeAbility
{
    private readonly BuildingOutdoorRestAbility definition;

    internal BuildingOutdoorRestAbilityRuntime(BuildingOutdoorRestAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }
    public bool SupportsExteriorWork(WorkTypeId workTypeId) => definition.SupportsExteriorWork(workTypeId);

    public bool IsExteriorWorkAvailable(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!SupportsExteriorWork(workTypeId)
            || actor == null
            || building is not ExteriorZoneMarker marker
            || !marker.IsOutdoorRestSpot)
        {
            return false;
        }

        BuildingVisitorSnapshot visitor = actor.VisitorSnapshot;
        return visitor.Mood < 85f || visitor.ExpeditionStress > 0f;
    }

    public float GetExteriorWorkSeconds(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return SupportsExteriorWork(workTypeId) ? Mathf.Max(0.1f, definition.workSeconds) : 0f;
    }

    public float GetExteriorWorkUrgency(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!IsExteriorWorkAvailable(actor, building, workTypeId))
        {
            return 0f;
        }

        BuildingVisitorSnapshot visitor = actor.VisitorSnapshot;
        float moodNeed = Mathf.Clamp(85f - visitor.Mood, 0f, 85f);
        float stress = visitor.ExpeditionStress;
        return Mathf.Clamp(moodNeed * 0.75f + stress * 0.45f, 15f, 80f);
    }
}

internal sealed class BuildingExteriorMaintenanceAbilityRuntime : IBuildingExteriorWorkRuntimeAbility
{
    private readonly BuildingExteriorMaintenanceAbility definition;

    internal BuildingExteriorMaintenanceAbilityRuntime(BuildingExteriorMaintenanceAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }
    public bool SupportsExteriorWork(WorkTypeId workTypeId) => definition.SupportsExteriorWork(workTypeId);

    public bool IsExteriorWorkAvailable(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!SupportsExteriorWork(workTypeId) || building is not ExteriorZoneMarker marker)
        {
            return false;
        }

        return workTypeId == BuiltInWorkTypeIds.Clean
            ? marker.CanRunExteriorCleanWork
            : marker.CanRunExteriorRepairWork;
    }

    public float GetExteriorWorkSeconds(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return workTypeId == BuiltInWorkTypeIds.Clean
            ? Mathf.Max(0.1f, definition.cleanWorkSeconds)
            : SupportsExteriorWork(workTypeId)
                ? Mathf.Max(0.1f, definition.repairWorkSeconds)
                : 0f;
    }

    public float GetExteriorWorkUrgency(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        if (!IsExteriorWorkAvailable(actor, building, workTypeId)
            || building is not ExteriorZoneMarker marker)
        {
            return 0f;
        }

        return workTypeId == BuiltInWorkTypeIds.Clean
            ? marker.GetCleanUrgency()
            : marker.GetRepairUrgency();
    }
}

internal sealed class BuildingTrainingAbilityRuntime : IBuildingUseCompletedRuntimeAbility
{
    private readonly BuildingTrainingAbility definition;

    internal BuildingTrainingAbilityRuntime(BuildingTrainingAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public void ApplyUseCompleted(IBuildingVisitorPort actor, BuildableObject building)
    {
        if (actor == null || building == null)
        {
            return;
        }

        if (definition.experienceAmount > 0)
        {
            actor.AddExperience(definition.experienceAmount);
        }

        if (Mathf.Approximately(definition.moodAmount, 0f))
        {
            return;
        }

        actor.ApplyMoodFactor(
            $"facility-training:{building.RequirePersistentInstanceId().Value}:{definition.AbilityId}",
            string.IsNullOrWhiteSpace(definition.moodLabel) ? "훈련을 마침" : definition.moodLabel,
            definition.moodAmount,
            Mathf.Max(0f, definition.durationSeconds),
            1);
    }
}

internal sealed class BuildingSecurityAbilityRuntime : IBuildingRuntimeStateAbility
{
    private readonly BuildingSecurityAbility definition;

    internal BuildingSecurityAbilityRuntime(BuildingSecurityAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public IBuildingStateModule CreateStateModule(BuildableObject building)
    {
        return new BuildingSecurityStateModule(building, definition);
    }
}

internal sealed class BuildingCrimeRiskModifierAbilityRuntime : IBuildingCrimeRiskModifier
{
    private readonly BuildingCrimeRiskModifierAbility definition;

    internal BuildingCrimeRiskModifierAbilityRuntime(BuildingCrimeRiskModifierAbility definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public float ModifyCrimePressure(float pressure, FacilityCrimeRiskContext context)
    {
        return Mathf.Max(0f, (Mathf.Max(0f, pressure) * definition.multiplier) + definition.flatOffset);
    }
}
