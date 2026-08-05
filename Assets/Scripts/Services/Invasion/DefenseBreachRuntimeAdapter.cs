using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DefenseBreachPlan
{
    public DefenseBreachPlan(
        BuildableObject target,
        Vector2Int attackCell,
        Queue<GridMoveStep> approachPath,
        IReadOnlyList<Vector2Int> virtualPath,
        float totalCost)
    {
        Target = target;
        AttackCell = attackCell;
        ApproachPath = approachPath ?? new Queue<GridMoveStep>();
        VirtualPath = virtualPath ?? Array.Empty<Vector2Int>();
        TotalCost = Mathf.Max(0f, totalCost);
    }

    public BuildableObject Target { get; }
    public Vector2Int AttackCell { get; }
    public Queue<GridMoveStep> ApproachPath { get; }
    public IReadOnlyList<Vector2Int> VirtualPath { get; }
    public float TotalCost { get; }
}

public interface IDefenseBreachPlanner
{
    Queue<GridMoveStep> GetRiskAwarePath(
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        IGridPathSearchBroker pathSearchBroker,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        float riskTolerance);
    bool TryPlan(
        string intruderId,
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        IGridPathSearchBroker pathSearchBroker,
        IBuildingStructuralIntegrityRuntime structuralIntegrity,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        float riskTolerance,
        float estimatedStructureDamage,
        out DefenseBreachPlan plan);
    void ReleaseReservation(string intruderId);
    int GetReservedAttackerCount(BuildableObject target);
}

public sealed class DefenseBreachPlanner : IDefenseBreachPlanner
{
    private readonly DefenseBreachPlanningRules rules = new();

    public Queue<GridMoveStep> GetRiskAwarePath(
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        IGridPathSearchBroker pathSearchBroker,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        float riskTolerance)
    {
        return rules.GetRiskAwarePath(
            grid,
            start,
            destination,
            pathSearchBroker,
            knownRisks,
            riskTolerance);
    }

    public bool TryPlan(
        string intruderId,
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        IGridPathSearchBroker pathSearchBroker,
        IBuildingStructuralIntegrityRuntime structuralIntegrity,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        float riskTolerance,
        float estimatedStructureDamage,
        out DefenseBreachPlan plan)
    {
        plan = null;
        if (grid == null || structuralIntegrity == null)
        {
            return false;
        }

        DefenseBreachTargetRuntimeAdapter targets = new(
            grid,
            structuralIntegrity);
        if (!rules.TryPlan(
                intruderId,
                grid,
                start,
                destination,
                pathSearchBroker,
                targets,
                knownRisks,
                riskTolerance,
                estimatedStructureDamage,
                out DefenseBreachPlanSnapshot snapshot)
            || !targets.TryResolve(
                snapshot.Target.TargetId,
                out BuildableObject target))
        {
            return false;
        }

        plan = new DefenseBreachPlan(
            target,
            snapshot.AttackCell,
            snapshot.ApproachPath,
            snapshot.VirtualPath,
            snapshot.TotalCost);
        return true;
    }

    public void ReleaseReservation(string intruderId)
    {
        rules.ReleaseReservation(intruderId);
    }

    public int GetReservedAttackerCount(BuildableObject target)
    {
        return target != null
            ? rules.GetReservedAttackerCount(
                target.RequirePersistentInstanceId())
            : 0;
    }
}

internal sealed class DefenseBreachTargetRuntimeAdapter :
    IDefenseBreachTargetQuery,
    IDefenseBreachTargetCommand
{
    private readonly Grid grid;
    private readonly IBuildingStructuralIntegrityRuntime structuralIntegrity;
    private readonly Dictionary<BuildingInstanceId, BuildableObject> targets =
        new();

    public DefenseBreachTargetRuntimeAdapter(
        Grid grid,
        IBuildingStructuralIntegrityRuntime structuralIntegrity)
    {
        this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        this.structuralIntegrity = structuralIntegrity
            ?? throw new ArgumentNullException(nameof(structuralIntegrity));
    }

    public bool TryGetTargetAt(
        Vector2Int position,
        out DefenseBreachTargetSnapshot target)
    {
        BuildableObject building = grid.GetGridCell(position)
            ?.GetOccupant(GridLayer.Building) as BuildableObject;
        if (!structuralIntegrity.IsBreachable(building)
            || !structuralIntegrity.TryGet(
                building,
                out BuildingStructuralIntegritySnapshot integrity))
        {
            target = default;
            return false;
        }

        target = Capture(building, integrity);
        Remember(target.TargetId, building);
        return true;
    }

    public bool TryGetTarget(
        BuildingInstanceId targetId,
        out DefenseBreachTargetSnapshot target)
    {
        if (!TryResolve(targetId, out BuildableObject building)
            || !structuralIntegrity.TryGet(
                building,
                out BuildingStructuralIntegritySnapshot integrity))
        {
            target = default;
            return false;
        }

        target = Capture(building, integrity);
        return true;
    }

    public DefenseBreachDamageSnapshot ApplyDamage(
        BuildingInstanceId targetId,
        float damage)
    {
        if (!targetId.IsValid)
        {
            return new DefenseBreachDamageSnapshot(
                targetId,
                false,
                false,
                0f,
                default,
                DefenseBreachDamageFailureCode.InvalidTarget);
        }

        if (!TryResolve(targetId, out BuildableObject building))
        {
            return new DefenseBreachDamageSnapshot(
                targetId,
                false,
                false,
                0f,
                default,
                DefenseBreachDamageFailureCode.TargetNotFound);
        }

        BuildingStructuralDamageResult result =
            structuralIntegrity.ApplyDamage(building, damage);
        DefenseBreachTargetSnapshot target = Capture(
            building,
            result.Snapshot);
        return new DefenseBreachDamageSnapshot(
            targetId,
            result.Applied,
            result.Destroyed,
            result.Damage,
            target,
            result.Applied
                ? DefenseBreachDamageFailureCode.None
                : DefenseBreachDamageFailureCode.DamageRejected);
    }

    public bool TryResolve(
        BuildingInstanceId targetId,
        out BuildableObject building)
    {
        building = null;
        return targetId.IsValid
            && targets.TryGetValue(targetId, out building)
            && building != null;
    }

    private static DefenseBreachTargetSnapshot Capture(
        BuildableObject building,
        BuildingStructuralIntegritySnapshot integrity)
    {
        return new DefenseBreachTargetSnapshot(
            building.RequirePersistentInstanceId(),
            building.buildPoses != null
                ? building.buildPoses.ToArray()
                : Array.Empty<Vector2Int>(),
            building.isDestroy,
            integrity.CurrentHitPoints,
            integrity.MaxHitPoints,
            integrity.Toughness,
            integrity.Breachable);
    }

    private void Remember(
        BuildingInstanceId targetId,
        BuildableObject building)
    {
        if (targets.TryGetValue(targetId, out BuildableObject existing)
            && existing != building)
        {
            throw new InvalidOperationException(
                $"Breach target ID '{targetId}' resolves to multiple buildings.");
        }
        targets[targetId] = building;
    }
}
