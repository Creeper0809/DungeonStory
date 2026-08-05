using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class GrandProjectApplicationAdapter :
    IGrandProjectWorldPort,
    IGrandProjectOperationsPort
{
    private const string OfficeTag = "grand-project-office";

    private readonly IProductionItemGateway items;
    private readonly IBuildingWorldQuery buildings;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly BlueprintResearchRuntime research;
    private readonly IWorkforceReplanService workforce;
    private readonly IFacilityCandidateCache facilityCandidates;

    public GrandProjectApplicationAdapter(
        IProductionItemGateway items,
        IBuildingWorldQuery buildings,
        IWorldDropZoneQuery dropZones,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IWorkforceReplanService workforce,
        IFacilityCandidateCache facilityCandidates)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.dropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(GrandProjectRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.workforce = workforce;
        this.facilityCandidates = facilityCandidates;
    }

    public GrandProjectOfficeSnapshot FindOffice()
    {
        BuildableObject office = buildings.Buildings
            .Where(IsOffice)
            .OrderBy(building => building.centerPos.y)
            .ThenBy(building => building.centerPos.x)
            .FirstOrDefault();
        return office == null
            ? null
            : new GrandProjectOfficeSnapshot(
                office.PersistentInstanceId,
                office.centerPos);
    }

    public bool IsResearchCompleted(string researchId)
    {
        return research.State.Projects.IsCompleted(
            new ResearchProjectId(researchId));
    }

    public Vector2Int ResolveReleasePosition()
    {
        GrandProjectOfficeSnapshot office = FindOffice();
        if (office != null)
        {
            return office.Position;
        }
        return dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff)
            ? dropoff
            : Vector2Int.zero;
    }

    public int CountPending(string itemId, string destinationId) =>
        items.CountPending(itemId, destinationId);
    public int CountDelivered(string itemId, string destinationId) =>
        items.CountDelivered(itemId, destinationId);

    public bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested)
    {
        return items.RequestDelivery(
            itemId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out _);
    }

    public bool ConsumeDelivered(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        out string failureReason) =>
        items.ConsumeDelivered(destinationId, costs, out failureReason);

    public int ReleaseDestination(
        string destinationId,
        Vector2Int releasePosition) =>
        items.ReleaseDestination(destinationId, releasePosition);

    public void PrioritizeDestination(string destinationId) =>
        items.PrioritizeDestination(destinationId);

    public void RequestGrandProjectWorker() =>
        workforce?.RequestOneWorkerToReplanFor(
            BuiltInWorkTypeIds.GrandProject,
            forceInterrupt: false);

    public void RequestHauler() =>
        workforce?.RequestOneHaulerToReplan(forceInterrupt: false);

    public void MarkDynamicStateDirty() =>
        facilityCandidates?.MarkDynamicStateDirty();

    private static bool IsOffice(BuildableObject building)
    {
        return building != null
            && !building.isDestroy
            && building.SupportsWork(BuiltInWorkTypeIds.GrandProject)
            && building.HasSemanticTag(OfficeTag);
    }
}
