using System;
using System.Collections.Generic;
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
    private bool isReconciling;
    private bool reconcilePending;

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
        if (isReconciling)
        {
            reconcilePending = true;
            return;
        }

        int buildingVersion = buildings.BuildingVersion;
        int facilityStateVersion = facilityStateVersions.DynamicStateVersion;
        bool mustReconcile = force || reconcilePending;
        reconcilePending = false;
        if (!mustReconcile
            && observedBuildingVersion == buildingVersion
            && observedFacilityStateVersion == facilityStateVersion)
        {
            return;
        }

        IReadOnlyList<BuildableObject> currentBuildings =
            buildings.Buildings ?? Array.Empty<BuildableObject>();
        BuildableObject[] pass = new BuildableObject[currentBuildings.Count];
        for (int index = 0; index < currentBuildings.Count; index++)
        {
            pass[index] = currentBuildings[index];
        }

        bool completed = false;
        isReconciling = true;
        try
        {
            foreach (BuildableObject building in pass)
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

            bool authorityChanged = buildings.BuildingVersion != buildingVersion
                || facilityStateVersions.DynamicStateVersion
                    != facilityStateVersion;
            if (authorityChanged || reconcilePending)
            {
                reconcilePending = true;
            }
            else
            {
                observedBuildingVersion = buildingVersion;
                observedFacilityStateVersion = facilityStateVersion;
            }

            completed = true;
        }
        finally
        {
            isReconciling = false;
            if (!completed)
            {
                reconcilePending = true;
            }
        }
    }
}
