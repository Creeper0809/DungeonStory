using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BuildingAbilityRuntimeExtensions
{
    public static IBuildingStateModule CreateStateModule(this BuildingCoverAbility definition, BuildableObject building)
    {
        return new BuildingCoverAbilityRuntime(RequireReceiver(definition)).CreateStateModule(building);
    }

    public static void ConfigureVisual(this BuildingCoverAbility definition, BuildableObject building)
    {
        new BuildingCoverAbilityRuntime(RequireReceiver(definition)).ConfigureVisual(building);
    }

    public static IBuildingStateModule CreateStateModule(this BuildingStructuralIntegrityAbility definition, BuildableObject building)
    {
        return new BuildingStructuralIntegrityAbilityRuntime(RequireReceiver(definition)).CreateStateModule(building);
    }

    public static void ConfigureVisual(this BuildingStructuralIntegrityAbility definition, BuildableObject building)
    {
        new BuildingStructuralIntegrityAbilityRuntime(RequireReceiver(definition)).ConfigureVisual(building);
    }

    public static IBuildingStateModule CreateStateModule(this BuildingProductionAbility definition, BuildableObject building)
    {
        return new BuildingProductionAbilityRuntime(RequireReceiver(definition)).CreateStateModule(building);
    }

    public static void ConfigureVisual(this BuildingLightingAbility definition, BuildableObject building)
    {
        new BuildingLightingAbilityRuntime(RequireReceiver(definition)).ConfigureVisual(building);
    }

    public static float GetRequiredWork(this BuildingWorkAmountAbility definition, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingWorkAmountAbilityRuntime(RequireReceiver(definition)).GetRequiredWork(building, workTypeId);
    }

    public static void ApplyUseCompleted(this BuildingExpeditionRecoveryAbility definition, IBuildingVisitorPort actor, BuildableObject building)
    {
        new BuildingExpeditionRecoveryAbilityRuntime(RequireReceiver(definition)).ApplyUseCompleted(actor, building);
    }

    public static bool IsExteriorWorkAvailable(this BuildingReceptionAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingReceptionAbilityRuntime(RequireReceiver(definition)).IsExteriorWorkAvailable(actor, building, workTypeId);
    }

    public static float GetExteriorWorkSeconds(this BuildingReceptionAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingReceptionAbilityRuntime(RequireReceiver(definition)).GetExteriorWorkSeconds(actor, building, workTypeId);
    }

    public static float GetExteriorWorkUrgency(this BuildingReceptionAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingReceptionAbilityRuntime(RequireReceiver(definition)).GetExteriorWorkUrgency(actor, building, workTypeId);
    }

    public static bool IsExteriorWorkAvailable(this BuildingPatrolPostAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingPatrolPostAbilityRuntime(RequireReceiver(definition)).IsExteriorWorkAvailable(actor, building, workTypeId);
    }

    public static float GetExteriorWorkSeconds(this BuildingPatrolPostAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingPatrolPostAbilityRuntime(RequireReceiver(definition)).GetExteriorWorkSeconds(actor, building, workTypeId);
    }

    public static float GetExteriorWorkUrgency(this BuildingPatrolPostAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingPatrolPostAbilityRuntime(RequireReceiver(definition)).GetExteriorWorkUrgency(actor, building, workTypeId);
    }

    public static bool IsExteriorWorkAvailable(this BuildingOutdoorRestAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingOutdoorRestAbilityRuntime(RequireReceiver(definition)).IsExteriorWorkAvailable(actor, building, workTypeId);
    }

    public static float GetExteriorWorkSeconds(this BuildingOutdoorRestAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingOutdoorRestAbilityRuntime(RequireReceiver(definition)).GetExteriorWorkSeconds(actor, building, workTypeId);
    }

    public static float GetExteriorWorkUrgency(this BuildingOutdoorRestAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingOutdoorRestAbilityRuntime(RequireReceiver(definition)).GetExteriorWorkUrgency(actor, building, workTypeId);
    }

    public static bool IsExteriorWorkAvailable(this BuildingExteriorMaintenanceAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingExteriorMaintenanceAbilityRuntime(RequireReceiver(definition)).IsExteriorWorkAvailable(actor, building, workTypeId);
    }

    public static float GetExteriorWorkSeconds(this BuildingExteriorMaintenanceAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingExteriorMaintenanceAbilityRuntime(RequireReceiver(definition)).GetExteriorWorkSeconds(actor, building, workTypeId);
    }

    public static float GetExteriorWorkUrgency(this BuildingExteriorMaintenanceAbility definition, IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId)
    {
        return new BuildingExteriorMaintenanceAbilityRuntime(RequireReceiver(definition)).GetExteriorWorkUrgency(actor, building, workTypeId);
    }

    public static void ApplyUseCompleted(this BuildingTrainingAbility definition, IBuildingVisitorPort actor, BuildableObject building)
    {
        new BuildingTrainingAbilityRuntime(RequireReceiver(definition)).ApplyUseCompleted(actor, building);
    }

    public static IBuildingStateModule CreateStateModule(this BuildingSecurityAbility definition, BuildableObject building)
    {
        return new BuildingSecurityAbilityRuntime(RequireReceiver(definition)).CreateStateModule(building);
    }

    public static float ModifyCrimePressure(this BuildingCrimeRiskModifierAbility definition, float pressure, FacilityCrimeRiskContext context)
    {
        return new BuildingCrimeRiskModifierAbilityRuntime(RequireReceiver(definition)).ModifyCrimePressure(pressure, context);
    }
    private static TDefinition RequireReceiver<TDefinition>(TDefinition definition) where TDefinition : class =>
        definition ?? throw new NullReferenceException();
}
