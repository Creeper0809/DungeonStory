using System.Collections.Generic;
using UnityEngine;

public interface IBuildingConditionContextPort
{
    bool ShouldSkipConstructionCosts { get; }
    bool CanSpendConstruction(int amount);
    bool TrySpendConstruction(int amount);
}

public interface IBuildingConnectivityQueryPort
{
    bool IsConnectedWithAny(IReadOnlyCollection<Vector2Int> positions);
    bool IsConnected(Vector2Int start, int associatedId);
}

public interface IBuildingCondition
{
    bool IsSatisfy(
        IBuildingConnectivityQueryPort connectivity,
        List<Vector2Int> buildPos,
        IBuildingConditionContextPort context,
        out string errorMessage);

    void OnBuild(IBuildingConditionContextPort context);
}
