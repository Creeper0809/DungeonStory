using System.Collections.Generic;

public interface IBuildingVisualRuntimeAbility
{
    void ConfigureVisual(BuildableObject building);
}

public interface IBuildingUseCompletedRuntimeAbility
{
    void ApplyUseCompleted(IBuildingVisitorPort actor, BuildableObject building);
}

public interface IBuildingExteriorWorkRuntimeAbility
{
    bool SupportsExteriorWork(WorkTypeId workTypeId);
    bool IsExteriorWorkAvailable(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId);
    float GetExteriorWorkSeconds(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId);
    float GetExteriorWorkUrgency(IBuildingVisitorPort actor, BuildableObject building, WorkTypeId workTypeId);
}

public interface IBuildingWorkAmountRuntimeAbility
{
    float GetRequiredWork(BuildableObject building, WorkTypeId workTypeId);
}

public interface IBuildingRuntimeStateAbility
{
    IBuildingStateModule CreateStateModule(BuildableObject building);
}

public interface IBuildingCrimeRiskModifier
{
    float ModifyCrimePressure(float pressure, FacilityCrimeRiskContext context);
}
