using System;
using System.Linq;
using UnityEngine;

public enum BuildingDestructiveLossDisposition
{
    BlockedByLiveAuthority = 0,
    Removed = 1,
    RolledBack = 2,
    CommittedWithNotificationFailure = 3,
    RollbackFailed = 4
}

public readonly struct BuildingDestructiveLossResult
{
    public BuildingDestructiveLossResult(
        BuildingDestructiveLossDisposition disposition,
        string failureReason)
    {
        Disposition = disposition;
        FailureReason = failureReason ?? string.Empty;
    }

    public BuildingDestructiveLossDisposition Disposition { get; }
    public string FailureReason { get; }
    public bool Removed =>
        Disposition == BuildingDestructiveLossDisposition.Removed
        || Disposition == BuildingDestructiveLossDisposition.CommittedWithNotificationFailure;
}

public sealed class BuildingDestructiveLossCandidate
{
    internal BuildingDestructiveLossCandidate(
        BuildableObject building,
        BuildingSO buildingData,
        Grid grid,
        GridLayer registeredLayer,
        ProductionFacilityEmptyMutationCandidate productionCandidate)
    {
        Building = building;
        BuildingData = buildingData;
        Grid = grid;
        RegisteredLayer = registeredLayer;
        ProductionCandidate = productionCandidate;
    }

    internal BuildableObject Building { get; }
    internal BuildingSO BuildingData { get; }
    internal Grid Grid { get; }
    internal GridLayer RegisteredLayer { get; }
    internal ProductionFacilityEmptyMutationCandidate ProductionCandidate { get; }
    internal bool IsClosed { get; set; }
}

public interface IBuildingDestructiveLossRuntime
{
    bool TryPrepare(
        BuildableObject building,
        string operationId,
        out BuildingDestructiveLossCandidate candidate,
        out string failureReason);

    BuildingDestructiveLossResult TryCommit(
        BuildingDestructiveLossCandidate candidate);

    bool TryAbort(
        BuildingDestructiveLossCandidate candidate,
        out string failureReason);
}

/// <summary>
/// Removes a building because of combat or structural loss without awarding
/// demolition salvage. This first proves that every production-output owner is
/// empty, then revokes the exact authority and rolls it back if world removal
/// cannot be committed.
/// </summary>
public sealed class BuildingDestructiveLossRuntime :
    IBuildingDestructiveLossRuntime
{
    private readonly IProductionFacilityMutationFence productionFence;
    private readonly IGridTextureProvider gridTextureProvider;

    public BuildingDestructiveLossRuntime(
        IProductionFacilityMutationFence productionFence,
        IGridTextureProvider gridTextureProvider)
    {
        this.productionFence = productionFence
            ?? throw new ArgumentNullException(nameof(productionFence));
        this.gridTextureProvider = gridTextureProvider
            ?? throw new ArgumentNullException(nameof(gridTextureProvider));
    }

    public bool TryPrepare(
        BuildableObject building,
        string operationId,
        out BuildingDestructiveLossCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (building == null
            || building.isDestroy
            || building.BuildingData == null
            || building.Grid == null
            || !building.PersistentInstanceId.IsValid
            || string.IsNullOrEmpty(operationId)
            || !string.Equals(operationId, operationId.Trim(), StringComparison.Ordinal))
        {
            failureReason = "building-destructive-loss-request-invalid";
            return false;
        }

        GridLayer layer = ResolveRegisteredLayer(building);
        if (!building.buildPoses.Any(position => ReferenceEquals(
                building.Grid.GetGridCell(position)?.GetOccupant(layer),
                building)))
        {
            failureReason = "building-destructive-loss-grid-authority-missing";
            return false;
        }

        if (!productionFence.TryPrepareEmpty(
                building,
                ProductionFacilityMutationKind.DestructiveLoss,
                operationId,
                out ProductionFacilityEmptyMutationCandidate productionCandidate,
                out failureReason))
        {
            return false;
        }

        candidate = new BuildingDestructiveLossCandidate(
            building,
            building.BuildingData,
            building.Grid,
            layer,
            productionCandidate);
        return true;
    }

    public BuildingDestructiveLossResult TryCommit(
        BuildingDestructiveLossCandidate candidate)
    {
        if (candidate == null
            || candidate.IsClosed
            || candidate.Building == null
            || candidate.Building.isDestroy)
        {
            return new BuildingDestructiveLossResult(
                BuildingDestructiveLossDisposition.RolledBack,
                "building-destructive-loss-candidate-invalid");
        }

        if (!productionFence.TryCommitAuthorityRevoke(
                candidate.ProductionCandidate,
                out string revokeFailure))
        {
            return RollBack(candidate,
                "building-destructive-loss-revoke-failed:" + revokeFailure);
        }

        bool movement = candidate.RegisteredLayer ==
            candidate.BuildingData.Placement.Layer
            && candidate.BuildingData.Placement.IsMovement;
        if (!candidate.Grid.RemoveOccupant(
                candidate.Building,
                candidate.RegisteredLayer,
                candidate.Building.buildPoses,
                movement))
        {
            return RollBack(candidate,
                "building-destructive-loss-grid-remove-failed");
        }

        try
        {
            gridTextureProvider.Texture.DeleteBuilding(
                candidate.BuildingData,
                candidate.Building.centerPos);
        }
        catch (Exception exception)
        {
            bool worldRestored = RestoreGridAndVisual(
                candidate,
                movement,
                redrawVisual: true,
                out string worldFailure);
            return RollBack(candidate,
                "building-destructive-loss-visual-remove-failed:"
                + exception.GetType().Name + ":" + worldFailure,
                worldRestored);
        }

        if (!productionFence.TryComplete(
                candidate.ProductionCandidate,
                out string completeFailure))
        {
            bool worldRestored = RestoreGridAndVisual(
                candidate,
                movement,
                redrawVisual: true,
                out string worldFailure);
            return RollBack(candidate,
                "building-destructive-loss-complete-failed:"
                + completeFailure + ":" + worldFailure,
                worldRestored);
        }

        candidate.IsClosed = true;
        try
        {
            candidate.Building.DestroySelf();
            return new BuildingDestructiveLossResult(
                BuildingDestructiveLossDisposition.Removed,
                string.Empty);
        }
        catch (Exception exception)
        {
            return new BuildingDestructiveLossResult(
                BuildingDestructiveLossDisposition.CommittedWithNotificationFailure,
                "building-destructive-loss-notification-failed:"
                + exception.GetType().Name + ":" + exception.Message);
        }
    }

    public bool TryAbort(
        BuildingDestructiveLossCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (candidate == null || candidate.IsClosed)
        {
            failureReason = "building-destructive-loss-candidate-closed";
            return false;
        }
        bool aborted = productionFence.TryAbort(
            candidate.ProductionCandidate,
            out failureReason);
        if (aborted)
            candidate.IsClosed = true;
        return aborted;
    }

    private BuildingDestructiveLossResult RollBack(
        BuildingDestructiveLossCandidate candidate,
        string failureReason,
        bool worldRestored = true)
    {
        bool aborted = TryAbort(candidate, out string abortFailure);
        return new BuildingDestructiveLossResult(
            aborted && worldRestored
                ? BuildingDestructiveLossDisposition.RolledBack
                : BuildingDestructiveLossDisposition.RollbackFailed,
            failureReason
            + (aborted ? string.Empty : ":abort-failed:" + abortFailure));
    }

    private bool RestoreGridAndVisual(
        BuildingDestructiveLossCandidate candidate,
        bool movement,
        bool redrawVisual,
        out string failureReason)
    {
        bool gridRestored = candidate.Grid.RegisterOccupant(
            candidate.Building,
            candidate.RegisteredLayer,
            candidate.Building.buildPoses,
            movement);
        bool visualRestored = true;
        if (redrawVisual)
        {
            try
            {
                gridTextureProvider.Texture.DrawBuilding(
                    candidate.BuildingData,
                    candidate.Building.centerPos);
            }
            catch
            {
                visualRestored = false;
            }
        }
        failureReason = "grid-restored=" + gridRestored
            + ",visual-restored=" + visualRestored;
        return gridRestored && visualRestored;
    }

    private static GridLayer ResolveRegisteredLayer(BuildableObject building)
    {
        if (building.buildPoses.Any(position => ReferenceEquals(
                building.Grid.GetGridCell(position)
                    ?.GetOccupant(GridLayer.Construction),
                building)))
        {
            return GridLayer.Construction;
        }
        return building.BuildingData.Placement.Layer;
    }
}
