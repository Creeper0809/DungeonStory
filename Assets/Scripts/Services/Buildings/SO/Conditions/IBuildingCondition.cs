using System.Collections.Generic;
using UnityEngine;

public interface IBuildingCondition
{
    public bool IsSatisfy(
        Grid grid,
        List<Vector2Int> buildPos,
        BuildingConditionContext context,
        out string errorMessage);

    public void OnBuild(BuildingConditionContext context);
}

public readonly struct BuildingConditionContext
{
    public static readonly BuildingConditionContext Empty = new BuildingConditionContext(null);

    public BuildingConditionContext(GameData gameData)
        : this(gameData, null)
    {
    }

    public BuildingConditionContext(GameData gameData, IBuildingUnlockStateView buildingUnlockState)
    {
        GameData = gameData;
        BuildingUnlockState = buildingUnlockState;
    }

    public GameData GameData { get; }
    public IBuildingUnlockStateView BuildingUnlockState { get; }
}
