using System;
using System.Linq;

internal sealed class WorkTargetEnvironmentPolicy
{
    private readonly AbilityWork work;
    private readonly IEnvironmentWorkPolicy environment;

    public WorkTargetEnvironmentPolicy(
        AbilityWork work,
        IEnvironmentWorkPolicy environment)
    {
        this.work = work ?? throw new ArgumentNullException(nameof(work));
        this.environment = environment;
    }

    public bool CanStartEstimate(
        BuildableObject building,
        WorkTypeId workTypeId,
        bool forced,
        out string reason)
    {
        reason = string.Empty;
        if (environment == null)
        {
            return true;
        }

        WorkEnvironmentAssessment assessment = environment.Assess(
            work.WorkerActor,
            building.centerPos,
            EstimateRemainingSeconds(building, workTypeId),
            ResolveKind(workTypeId),
            forced);
        reason = ToDiagnosticCode(assessment.Failure);
        return assessment.CanStart;
    }

    public bool TryAssessEstimate(
        BuildableObject building,
        WorkTypeId workTypeId,
        out WorkEnvironmentAssessment assessment)
    {
        assessment = default;
        if (environment == null || building == null || !workTypeId.IsValid)
            return false;

        assessment = environment.Assess(
            work.WorkerActor,
            building.centerPos,
            EstimateRemainingSeconds(building, workTypeId),
            ResolveKind(workTypeId),
            forced: false);
        return true;
    }

    public bool CanStartRoute(
        BuildableObject building,
        WorkTypeId workTypeId,
        GridPathSearchResult searchResult,
        bool forced,
        out string reason)
    {
        reason = string.Empty;
        if (environment == null)
        {
            return true;
        }

        GridMoveStep[] route = searchResult?
            .GetMovePathTo(building.centerPos)
            .ToArray()
            ?? Array.Empty<GridMoveStep>();
        WorkEnvironmentAssessment assessment = environment.AssessStart(
            work.WorkerActor,
            building.centerPos,
            route,
            EstimateRemainingSeconds(building, workTypeId),
            ResolveKind(workTypeId),
            forced);
        reason = ToDiagnosticCode(assessment.Failure);
        return assessment.CanStart;
    }

    public bool RequiresForcedConfirmation(
        BuildableObject building,
        WorkTypeId workTypeId,
        GridPathSearchResult searchResult,
        out string warning)
    {
        warning = string.Empty;
        if (environment == null
            || building == null
            || !workTypeId.IsValid
            || work.WorkerActor == null)
        {
            return false;
        }

        GridPathSearchResult resolvedSearch = searchResult;
        if (resolvedSearch == null)
        {
            Grid grid = work.WorkGridResolver.ResolveActiveGrid(work, null);
            resolvedSearch = grid?.SearchPath(work.WorkerActor.GetNowXY());
        }

        GridMoveStep[] route = resolvedSearch?
            .GetMovePathTo(building.centerPos)
            .ToArray()
            ?? Array.Empty<GridMoveStep>();
        WorkEnvironmentAssessment assessment = environment.AssessStart(
            work.WorkerActor,
            building.centerPos,
            route,
            EstimateRemainingSeconds(building, workTypeId),
            ResolveKind(workTypeId),
            forced: true);
        bool requiresConfirmation = assessment.Projection.HasLethalChannel
            || assessment.Projection.WorstBand >= EnvironmentalExposureBand.Critical;
        if (requiresConfirmation)
        {
            warning = ToDiagnosticCode(assessment.Failure);
        }

        return requiresConfirmation;
    }

    private static string ToDiagnosticCode(DomainFailure failure) =>
        failure.IsFailure ? failure.Code.ToString() : string.Empty;

    private float EstimateRemainingSeconds(
        BuildableObject building,
        WorkTypeId workTypeId)
    {
        if (building == null || !workTypeId.IsValid)
        {
            return 60f;
        }

        float remainingWork;
        if (work.WorkOrderRuntime != null
            && work.WorkOrderRuntime.TryGetOrderFor(
                building,
                workTypeId,
                out WorkOrderProgressState order))
        {
            remainingWork = UnityEngine.Mathf.Max(
                0f,
                order.RequiredWork - order.CompletedWork);
        }
        else
        {
            remainingWork = UnityEngine.Mathf.Max(
                0f,
                building.BuildingData != null
                    ? building.BuildingData.GetRequiredWork(workTypeId)
                    : 0f);
        }

        if (remainingWork <= 0f)
        {
            return 60f;
        }

        float speed = work.WorkerActor != null
            ? UnityEngine.Mathf.Max(
                0.05f,
                work.WorkerActor.GetWorkSpeedMultiplier(
                    workTypeId,
                    building))
            : 1f;
        return UnityEngine.Mathf.Clamp(remainingWork / speed, 0.1f, 3600f);
    }

    private static EnvironmentalWorkKind ResolveKind(WorkTypeId workTypeId)
    {
        string id = workTypeId.Value ?? string.Empty;
        return id.IndexOf("research", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("craft", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("medical", StringComparison.OrdinalIgnoreCase) >= 0
            || id.IndexOf("treat", StringComparison.OrdinalIgnoreCase) >= 0
                ? EnvironmentalWorkKind.Precision
                : EnvironmentalWorkKind.General;
    }
}
