using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal readonly struct InvasionIntruderPathPlanResult
{
    public InvasionIntruderPathPlanResult(
        Queue<GridMoveStep> path,
        bool direct,
        BuildableObject priorityTarget,
        int awarenessVersion,
        float commitmentUntil)
    {
        Path = path ?? new Queue<GridMoveStep>();
        Direct = direct;
        PriorityTarget = priorityTarget;
        AwarenessVersion = awarenessVersion;
        CommitmentUntil = commitmentUntil;
    }

    public Queue<GridMoveStep> Path { get; }
    public bool Direct { get; }
    public BuildableObject PriorityTarget { get; }
    public int AwarenessVersion { get; }
    public float CommitmentUntil { get; }
}

internal static class InvasionIntruderPathPlanning
{
    public static InvasionIntruderPathPlanResult Plan(
        Grid grid,
        Vector2Int start,
        Vector2Int ownerPosition,
        float focus,
        IGridPathSearchBroker pathSearchBroker,
        IRandomStream randomStream,
        InvasionIntruderPatternDefinition pattern,
        ISet<BuildingInstanceId> damagedFacilities,
        int facilityDamageCount,
        IDefenseBreachPlanner breachPlanner,
        IDefenseRaidAwarenessRuntime raidAwareness,
        string raidId,
        InvasionIntruderSettings settings,
        float currentTime)
    {
        Queue<GridMoveStep> path = InvasionIntruderPlanner.GetNextPath(
            grid,
            start,
            ownerPosition,
            focus,
            pathSearchBroker,
            randomStream,
            pattern,
            out bool direct,
            out BuildableObject priorityTarget,
            damagedFacilities,
            facilityDamageCount);
        DefenseRaidAwarenessSnapshot awareness =
            raidAwareness?.GetSnapshot(raidId);
        if (direct
            && path.Count > 0
            && breachPlanner != null
            && awareness != null
            && awareness.KnownRisks.Count > 0)
        {
            Queue<GridMoveStep> riskAware = breachPlanner.GetRiskAwarePath(
                grid,
                start,
                ownerPosition,
                pathSearchBroker,
                awareness.KnownRisks,
                settings.riskTolerance);
            if (riskAware.Count > 0)
            {
                path = riskAware;
                raidAwareness.SetExpectedPath(
                    raidId,
                    path.Select(step => step.To),
                    settings.riskTolerance >= 0.9f
                        ? "작전 목표 직행"
                        : "발견된 방어 위험 회피");
            }
        }
        else if (path.Count > 0)
        {
            raidAwareness?.SetExpectedPath(
                raidId,
                path.Select(step => step.To),
                priorityTarget != null
                    ? "우선 시설 목표"
                    : "현재 작전 경로");
        }

        float commitmentUntil = currentTime + Mathf.Max(
            settings.routeCommitmentSeconds,
            pattern.routeCommitmentSeconds);
        return new InvasionIntruderPathPlanResult(
            path,
            direct,
            priorityTarget,
            awareness?.Version ?? 0,
            commitmentUntil);
    }
}
