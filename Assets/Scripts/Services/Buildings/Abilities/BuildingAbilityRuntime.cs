using System.Collections.Generic;

public interface IBuildingVisualRuntimeAbility
{
    void ConfigureVisual(BuildableObject building);
}

public interface IBuildingUseCompletedRuntimeAbility
{
    void ApplyUseCompleted(CharacterActor actor, BuildableObject building);
}

public interface IBuildingWorkCompletionAbility
{
}

public interface IBuildingExteriorWorkRuntimeAbility
{
    bool SupportsExteriorWork(WorkTypeId workTypeId);
    bool IsExteriorWorkAvailable(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId);
    float GetExteriorWorkSeconds(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId);
    float GetExteriorWorkUrgency(CharacterActor actor, BuildableObject building, WorkTypeId workTypeId);
}

public interface IBuildingWorkAmountRuntimeAbility
{
    float GetRequiredWork(BuildableObject building, WorkTypeId workTypeId);
}

public interface IBuildingRuntimeStateAbility
{
    IBuildingStateModule CreateStateModule(BuildableObject building);
}

public interface IBuildingStockCategorySignal
{
    IEnumerable<StockCategory> GetStockCategorySignals();
}

public interface IBuildingCrimeRiskModifier
{
    float ModifyCrimePressure(float pressure, FacilityCrimeRiskContext context);
}
