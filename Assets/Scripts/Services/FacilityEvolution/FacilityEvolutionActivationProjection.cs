using System;
using VContainer.Unity;

/// <summary>
/// Reconciles room-conditional facility evolution outside read/query paths.
/// Modifier consumers can therefore remain pure while room and facility-state
/// changes still refresh the committed activation snapshot before later work.
/// </summary>
public sealed class FacilityEvolutionActivationProjection :
    IInitializable,
    ITickable
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IFacilityCandidateCache facilityStateVersions;
    private readonly IFacilityEvolutionRuntime evolution;

    private int observedBuildingVersion = -1;
    private int observedFacilityStateVersion = -1;

    public FacilityEvolutionActivationProjection(
        IBuildingWorldQuery buildings,
        IFacilityCandidateCache facilityStateVersions,
        IFacilityEvolutionRuntime evolution)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.facilityStateVersions = facilityStateVersions
            ?? throw new ArgumentNullException(nameof(facilityStateVersions));
        this.evolution = evolution
            ?? throw new ArgumentNullException(nameof(evolution));
    }

    public void Initialize()
    {
        Reconcile(force: true);
    }

    public void Tick()
    {
        Reconcile(force: false);
    }

    private void Reconcile(bool force)
    {
        int buildingVersion = buildings.BuildingVersion;
        int facilityStateVersion = facilityStateVersions.DynamicStateVersion;
        if (!force
            && observedBuildingVersion == buildingVersion
            && observedFacilityStateVersion == facilityStateVersion)
        {
            return;
        }

        observedBuildingVersion = buildingVersion;
        observedFacilityStateVersion = facilityStateVersion;
        foreach (BuildableObject building in buildings.Buildings)
        {
            if (building == null
                || building.isDestroy
                || !building.TryGetComponent(
                    out FacilityEvolutionStateComponent _))
            {
                continue;
            }

            evolution.RefreshRoomActivation(building);
        }
    }
}
