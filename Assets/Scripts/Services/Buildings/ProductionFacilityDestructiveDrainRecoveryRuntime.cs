using System;
using System.Linq;
using VContainer.Unity;

public enum ProductionFacilityDestructiveRemovalStatus
{
    DeferredAccepted = 0,
    Conflict = 1,
    RemovedAwaitingCheckpointGc = 2,
    AlreadyRemovedAwaitingCheckpointGc = 3,
    RemovedWithNotificationFailure = 4
}

public readonly struct ProductionFacilityDestructiveRemovalResult
{
    public ProductionFacilityDestructiveRemovalResult(
        ProductionFacilityDestructiveRemovalStatus status,
        string failureReason)
    {
        Status = status;
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionFacilityDestructiveRemovalStatus Status { get; }
    public string FailureReason { get; }
    public bool Removed => Status is
        ProductionFacilityDestructiveRemovalStatus.RemovedAwaitingCheckpointGc
        or ProductionFacilityDestructiveRemovalStatus
            .AlreadyRemovedAwaitingCheckpointGc
        or ProductionFacilityDestructiveRemovalStatus
            .RemovedWithNotificationFailure;
}

public interface IProductionFacilityDestructiveDrainRecoveryRuntime
{
    ProductionFacilityDestructiveRemovalResult RequestAndDrive(
        BuildableObject facility,
        ProductionFacilityDestructiveDrainCause cause);
}

public enum ProductionFacilityWorldRemovalDisposition
{
    Deferred = 0,
    Conflict = 1,
    Applied = 2,
    AlreadyApplied = 3,
    AppliedWithNotificationFailure = 4
}

public readonly struct ProductionFacilityWorldRemovalResult
{
    public ProductionFacilityWorldRemovalResult(
        ProductionFacilityWorldRemovalDisposition disposition,
        string failureReason)
    {
        Disposition = disposition;
        FailureReason = failureReason ?? string.Empty;
    }

    public ProductionFacilityWorldRemovalDisposition Disposition { get; }
    public string FailureReason { get; }
    public bool Applied => Disposition is
        ProductionFacilityWorldRemovalDisposition.Applied
        or ProductionFacilityWorldRemovalDisposition.AlreadyApplied
        or ProductionFacilityWorldRemovalDisposition
            .AppliedWithNotificationFailure;
}

public interface IProductionFacilityDestructiveDrainWorldRemovalPort
{
    ProductionFacilityWorldRemovalResult TryEnsureRemoved(
        BuildingInstanceId facilityId);
}

/// <summary>
/// Exact persistent-ID world removal primitive. Participant and destination
/// effects are outside this boundary and are never rolled back here.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainWorldRemovalPort :
    IProductionFacilityDestructiveDrainWorldRemovalPort
{
    private readonly IBuildingWorldQuery world;
    private readonly IGridTextureProvider textures;

    public ProductionFacilityDestructiveDrainWorldRemovalPort(
        IBuildingWorldQuery world,
        IGridTextureProvider textures)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
    }

    public ProductionFacilityWorldRemovalResult TryEnsureRemoved(
        BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
        {
            return Conflict(
                "production-destructive-world-removal-facility-invalid");
        }

        BuildableObject[] matches = (world.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null
                && value.PersistentInstanceId.Equals(facilityId))
            .ToArray();
        if (matches.Length == 0)
        {
            return new ProductionFacilityWorldRemovalResult(
                ProductionFacilityWorldRemovalDisposition.AlreadyApplied,
                string.Empty);
        }
        if (matches.Length != 1)
        {
            return Conflict(
                "production-destructive-world-removal-facility-duplicate:"
                + facilityId.Value);
        }

        BuildableObject facility = matches[0];
        if (facility.isDestroy)
        {
            return Conflict(
                "production-destructive-world-removal-teardown-incomplete:"
                + facilityId.Value);
        }
        if (facility.Grid == null
            || facility.BuildingData == null
            || facility.buildPoses == null
            || facility.buildPoses.Count == 0)
        {
            return Conflict(
                "production-destructive-world-removal-world-invalid:"
                + facilityId.Value);
        }

        GridLayer layer = ResolveRegisteredLayer(facility);
        if (!facility.buildPoses.Any(position => ReferenceEquals(
                facility.Grid.GetGridCell(position)?.GetOccupant(layer),
                facility)))
        {
            return Conflict(
                "production-destructive-world-removal-grid-authority-missing:"
                + facilityId.Value);
        }

        bool movement = layer == facility.BuildingData.Placement.Layer
            && facility.BuildingData.Placement.IsMovement;
        if (!facility.Grid.RemoveOccupant(
                facility,
                layer,
                facility.buildPoses,
                movement))
        {
            return Deferred(
                "production-destructive-world-removal-grid-remove-deferred:"
                + facilityId.Value);
        }

        try
        {
            textures.Texture.DeleteBuilding(
                facility.BuildingData,
                facility.centerPos);
        }
        catch (Exception exception)
        {
            if (!TryRestoreWorld(
                    facility,
                    layer,
                    movement,
                    redrawVisual: true,
                    out string restoreFailure))
            {
                return Conflict(
                    "production-destructive-world-removal-visual-failed-and-restore-failed:"
                    + exception.GetType().Name + ":" + restoreFailure);
            }
            return Deferred(
                "production-destructive-world-removal-visual-deferred:"
                + exception.GetType().Name);
        }

        string notificationFailure = string.Empty;
        try
        {
            facility.DestroySelf();
        }
        catch (Exception exception)
        {
            notificationFailure =
                "production-destructive-world-removal-notification-failed:"
                + exception.GetType().Name + ":" + exception.Message;
        }

        bool registered = (world.Buildings ?? Array.Empty<BuildableObject>())
            .Any(value => value != null
                && value.PersistentInstanceId.Equals(facilityId));
        bool occupied = facility.buildPoses.Any(position =>
        {
            GridCell cell = facility.Grid?.GetGridCell(position);
            return ReferenceEquals(cell?.GetOccupant(layer), facility)
                || (cell?.GetOccupant(layer) is BuildableObject other
                    && other.PersistentInstanceId.Equals(facilityId));
        });
        if (!facility.isDestroy || registered || occupied)
        {
            return Conflict(
                "production-destructive-world-removal-postcondition-failed:"
                + facilityId.Value);
        }

        return new ProductionFacilityWorldRemovalResult(
            notificationFailure.Length == 0
                ? ProductionFacilityWorldRemovalDisposition.Applied
                : ProductionFacilityWorldRemovalDisposition
                    .AppliedWithNotificationFailure,
            notificationFailure);
    }

    private bool TryRestoreWorld(
        BuildableObject facility,
        GridLayer layer,
        bool movement,
        bool redrawVisual,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!facility.Grid.RegisterOccupant(
                facility,
                layer,
                facility.buildPoses,
                movement))
        {
            failureReason =
                "grid-restore-rejected:" + facility.PersistentInstanceId.Value;
            return false;
        }
        if (redrawVisual)
        {
            try
            {
                textures.Texture.DrawBuilding(
                    facility.BuildingData,
                    facility.centerPos);
            }
            catch (Exception exception)
            {
                failureReason =
                    "visual-restore-failed:" + exception.GetType().Name;
                return false;
            }
        }
        return true;
    }

    private static GridLayer ResolveRegisteredLayer(BuildableObject facility)
    {
        if (facility.buildPoses.Any(position => ReferenceEquals(
                facility.Grid.GetGridCell(position)
                    ?.GetOccupant(GridLayer.Construction),
                facility)))
        {
            return GridLayer.Construction;
        }
        return facility.BuildingData.Placement.Layer;
    }

    private static ProductionFacilityWorldRemovalResult Deferred(
        string reason) => new(
        ProductionFacilityWorldRemovalDisposition.Deferred,
        reason);

    private static ProductionFacilityWorldRemovalResult Conflict(
        string reason) => new(
        ProductionFacilityWorldRemovalDisposition.Conflict,
        reason);
}

/// <summary>
/// Live upper state machine and post-restore forward-retry driver. Restore
/// hooks only queue work; world mutation starts on the next tick.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainRecoveryRuntime :
    IProductionFacilityDestructiveDrainRecoveryRuntime,
    IStartable,
    ITickable,
    IDungeonSaveRestoreCompletedHook,
    IDungeonSaveCaptureGuard
{
    private const int TransitionBudget = 12;

    private readonly IProductionFacilityDestructiveDrainJournalQuery journal;
    private readonly IProductionFacilityDestructiveDrainCoordinator coordinator;
    private readonly IProductionFacilityDestructiveDrainAuthorityRevoker revoker;
    private readonly IProductionFacilityDestructiveDrainWorldRemovalPort world;
    private readonly IBuildingWorldQuery buildings;
    private bool resumeRequested;
    private int observedJournalVersion = -1;
    private string worldBoundaryConflict = string.Empty;

    public ProductionFacilityDestructiveDrainRecoveryRuntime(
        IProductionFacilityDestructiveDrainJournalQuery journal,
        IProductionFacilityDestructiveDrainCoordinator coordinator,
        IProductionFacilityDestructiveDrainAuthorityRevoker revoker,
        IProductionFacilityDestructiveDrainWorldRemovalPort world,
        IBuildingWorldQuery buildings)
    {
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        this.coordinator = coordinator
            ?? throw new ArgumentNullException(nameof(coordinator));
        this.revoker = revoker ?? throw new ArgumentNullException(nameof(revoker));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
    }

    public void Start() => resumeRequested = true;

    public void OnRestoreCompleted() => resumeRequested = true;

    public void Tick()
    {
        if (!resumeRequested && observedJournalVersion == journal.Version)
            return;
        resumeRequested = false;
        observedJournalVersion = journal.Version;
        ProductionFacilityDestructiveDrainEntrySaveData entry = journal
            .CaptureOpen()
            .FirstOrDefault(value => value != null
                && value.phase != ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc);
        if (entry == null)
            return;
        ProductionFacilityDestructiveRemovalResult result =
            DriveExisting(entry.facilityId, entry.cause);
        if (result.Status ==
            ProductionFacilityDestructiveRemovalStatus.DeferredAccepted)
        {
            resumeRequested = true;
        }
        observedJournalVersion = journal.Version;
    }

    public ProductionFacilityDestructiveRemovalResult RequestAndDrive(
        BuildableObject facility,
        ProductionFacilityDestructiveDrainCause cause)
    {
        if (facility == null
            || facility.isDestroy
            || !facility.PersistentInstanceId.IsValid
            || cause == ProductionFacilityDestructiveDrainCause.None
            || !Enum.IsDefined(typeof(ProductionFacilityDestructiveDrainCause), cause))
        {
            return Conflict("production-destructive-drain-request-invalid");
        }
        ProductionFacilityDestructiveRemovalResult result = DriveExisting(
            facility.PersistentInstanceId.Value,
            cause);
        observedJournalVersion = journal.Version;
        resumeRequested = result.Status ==
            ProductionFacilityDestructiveRemovalStatus.DeferredAccepted;
        return result;
    }

    public void ValidateBeforeCapture()
    {
        if (worldBoundaryConflict.Length > 0)
        {
            throw new InvalidOperationException(
                "A destructive drain has an unresolved world-removal conflict: "
                + worldBoundaryConflict);
        }
        foreach (ProductionFacilityDestructiveDrainEntrySaveData entry in
                 journal.CaptureOpen())
        {
            if (entry == null
                || entry.phase !=
                    ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval)
            {
                continue;
            }
            BuildingInstanceId facilityId = (BuildingInstanceId)entry.facilityId;
            int count = (buildings.Buildings ?? Array.Empty<BuildableObject>())
                .Count(value => value != null
                    && value.PersistentInstanceId.Equals(facilityId));
            if (count != 1)
            {
                throw new InvalidOperationException(
                    "A destructive drain has an unacknowledged world-removal boundary: "
                    + entry.operationId + ":world-count=" + count);
            }
        }
    }

    private ProductionFacilityDestructiveRemovalResult DriveExisting(
        string rawFacilityId,
        ProductionFacilityDestructiveDrainCause cause)
    {
        BuildingInstanceId facilityId = (BuildingInstanceId)rawFacilityId;
        if (!facilityId.IsValid)
            return Conflict("production-destructive-drain-facility-invalid");

        ProductionFacilityDestructiveDrainOperationId operationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(facilityId);
        string notificationFailure = string.Empty;
        for (int transition = 0; transition < TransitionBudget; transition++)
        {
            if (!journal.TryGet(
                    operationId,
                    out ProductionFacilityDestructiveDrainEntrySaveData entry))
            {
                ProductionFacilityDestructiveDrainDriveResult started =
                    coordinator.DriveToAuthorityRevoke(cause, facilityId);
                if (started.Status is
                    ProductionFacilityDestructiveDrainDriveStatus.Deferred
                    or ProductionFacilityDestructiveDrainDriveStatus.Conflict)
                {
                    return FromDriveFailure(started);
                }
                continue;
            }
            if (entry.cause != cause)
            {
                return Conflict(
                    "production-destructive-drain-cause-conflict:"
                    + entry.cause + ":" + cause);
            }

            switch (entry.phase)
            {
                case ProductionFacilityDestructiveDrainPhase.Prepared:
                case ProductionFacilityDestructiveDrainPhase.DrainingParticipants:
                case ProductionFacilityDestructiveDrainPhase
                    .AwaitingEmptyVerification:
                {
                    ProductionFacilityDestructiveDrainDriveResult driven =
                        coordinator.DriveToAuthorityRevoke(cause, facilityId);
                    if (driven.Status is
                        ProductionFacilityDestructiveDrainDriveStatus.Deferred
                        or ProductionFacilityDestructiveDrainDriveStatus.Conflict)
                    {
                        return FromDriveFailure(driven);
                    }
                    continue;
                }

                case ProductionFacilityDestructiveDrainPhase
                    .AwaitingAuthorityRevoke:
                {
                    BuildableObject facility = ResolveExactFacility(
                        facilityId,
                        out string resolveFailure);
                    if (facility == null)
                        return Conflict(resolveFailure);
                    ProductionFacilityDestructiveDrainAuthorityConvergenceResult
                        convergence = revoker.TryConverge(
                            facility,
                            cause,
                            operationId,
                            entry.revision);
                    if (!convergence.Succeeded)
                    {
                        return convergence.Disposition ==
                                ProductionFacilityDestructiveDrainAuthorityConvergenceDisposition
                                    .Conflict
                            ? Conflict(convergence.FailureReason)
                            : Deferred(convergence.FailureReason);
                    }
                    ProductionFacilityDestructiveDrainDriveResult revoked =
                        coordinator.RecordAuthorityRevoked(operationId);
                    if (revoked.Status is
                        ProductionFacilityDestructiveDrainDriveStatus.Deferred
                        or ProductionFacilityDestructiveDrainDriveStatus.Conflict)
                    {
                        return FromDriveFailure(revoked);
                    }
                    continue;
                }

                case ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval:
                {
                    ProductionFacilityWorldRemovalResult removed =
                        world.TryEnsureRemoved(facilityId);
                    if (!removed.Applied)
                    {
                        if (removed.Disposition ==
                            ProductionFacilityWorldRemovalDisposition.Conflict)
                        {
                            worldBoundaryConflict = removed.FailureReason;
                        }
                        return removed.Disposition ==
                                ProductionFacilityWorldRemovalDisposition.Conflict
                            ? Conflict(removed.FailureReason)
                            : Deferred(removed.FailureReason);
                    }
                    worldBoundaryConflict = string.Empty;
                    if (removed.Disposition ==
                        ProductionFacilityWorldRemovalDisposition
                            .AppliedWithNotificationFailure)
                    {
                        notificationFailure = removed.FailureReason;
                    }
                    ProductionFacilityDestructiveDrainDriveResult recorded =
                        coordinator.RecordWorldRemoved(operationId);
                    if (recorded.Status is
                        ProductionFacilityDestructiveDrainDriveStatus.Deferred
                        or ProductionFacilityDestructiveDrainDriveStatus.Conflict)
                    {
                        resumeRequested = true;
                        return FromDriveFailure(recorded);
                    }
                    return new ProductionFacilityDestructiveRemovalResult(
                        notificationFailure.Length == 0
                            ? removed.Disposition ==
                                ProductionFacilityWorldRemovalDisposition
                                    .AlreadyApplied
                                ? ProductionFacilityDestructiveRemovalStatus
                                    .AlreadyRemovedAwaitingCheckpointGc
                                : ProductionFacilityDestructiveRemovalStatus
                                    .RemovedAwaitingCheckpointGc
                            : ProductionFacilityDestructiveRemovalStatus
                                .RemovedWithNotificationFailure,
                        notificationFailure);
                }

                case ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc:
                    return new ProductionFacilityDestructiveRemovalResult(
                        ProductionFacilityDestructiveRemovalStatus
                            .AlreadyRemovedAwaitingCheckpointGc,
                        string.Empty);

                default:
                    return Conflict(
                        "production-destructive-drain-phase-invalid:"
                        + entry.phase);
            }
        }
        resumeRequested = true;
        return Deferred(
            "production-destructive-drain-transition-budget-exhausted");
    }

    private BuildableObject ResolveExactFacility(
        BuildingInstanceId facilityId,
        out string failureReason)
    {
        BuildableObject[] matches = (buildings.Buildings
                ?? Array.Empty<BuildableObject>())
            .Where(value => value != null
                && value.PersistentInstanceId.Equals(facilityId))
            .ToArray();
        if (matches.Length == 1)
        {
            failureReason = string.Empty;
            return matches[0];
        }
        failureReason =
            "production-destructive-drain-facility-resolution-invalid:"
            + facilityId.Value + ":count=" + matches.Length;
        return null;
    }

    private static ProductionFacilityDestructiveRemovalResult FromDriveFailure(
        ProductionFacilityDestructiveDrainDriveResult result) =>
        result.Status == ProductionFacilityDestructiveDrainDriveStatus.Conflict
            ? Conflict(result.FailureReason)
            : Deferred(result.FailureReason);

    private static ProductionFacilityDestructiveRemovalResult Deferred(
        string reason) => new(
        ProductionFacilityDestructiveRemovalStatus.DeferredAccepted,
        reason);

    private static ProductionFacilityDestructiveRemovalResult Conflict(
        string reason) => new(
        ProductionFacilityDestructiveRemovalStatus.Conflict,
        reason);
}
