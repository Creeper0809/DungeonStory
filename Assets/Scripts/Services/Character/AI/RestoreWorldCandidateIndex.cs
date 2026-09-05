using System;
using System.Collections.Generic;

public interface IRestoreWorldCandidateQuery
{
    int Revision { get; }
    bool TryGetGrid(out Grid grid);
    bool TryGetBuildings(out IReadOnlyList<BuildableObject> buildings);
    bool TryGetCharacters(out IReadOnlyList<CharacterActor> characters);
    bool TryGetWildlife(out IReadOnlyList<WildlifeActor> wildlife);
    bool TryGetExteriorZones(out IReadOnlyList<ExteriorZoneMarker> zones);
}

public interface IRestoreWorldCandidatePublisher
{
    void SetFacilityCandidate(
        Grid grid,
        IReadOnlyList<BuildableObject> buildings);
    void ClearFacilityCandidate();
    void SetCharacterCandidate(IReadOnlyList<CharacterActor> characters);
    void SetCharacterCandidate(
        IReadOnlyList<CharacterActor> characters,
        IReadOnlyList<HaulDeliveryIntentSaveData> haulDeliveryIntents);
    void ClearCharacterCandidate();
    void SetWildlifeCandidate(IReadOnlyList<WildlifeActor> wildlife);
    void ClearWildlifeCandidate();
    void SetExteriorZoneCandidate(
        IReadOnlyList<ExteriorZoneMarker> zones);
    void ClearExteriorZoneCandidate();
}

public interface IRestoreHaulDeliveryIntentCandidateQuery
{
    bool TryGetHaulDeliveryIntents(
        out IReadOnlyList<HaulDeliveryIntentSaveData> intents);
}

/// <summary>
/// Holds read-only views of detached Unity-object candidates while a V18 save
/// transaction is being committed. The ordinary scene registries remain the
/// live authority; this index only redirects queries until final publication.
/// </summary>
public sealed class RestoreWorldCandidateIndex :
    IRestoreWorldCandidateQuery,
    IRestoreWorldCandidatePublisher,
    IRestoreHaulDeliveryIntentCandidateQuery
{
    private Grid facilityGrid;
    private IReadOnlyList<BuildableObject> facilityBuildings;
    private IReadOnlyList<CharacterActor> characters;
    private IReadOnlyList<HaulDeliveryIntentSaveData> haulDeliveryIntents;
    private IReadOnlyList<WildlifeActor> wildlife;
    private IReadOnlyList<ExteriorZoneMarker> exteriorZones;

    public int Revision { get; private set; }

    public bool TryGetGrid(out Grid grid)
    {
        grid = facilityGrid;
        return grid != null;
    }

    public bool TryGetBuildings(out IReadOnlyList<BuildableObject> buildings)
    {
        buildings = facilityBuildings;
        return buildings != null;
    }

    public bool TryGetCharacters(out IReadOnlyList<CharacterActor> candidateCharacters)
    {
        candidateCharacters = characters;
        return candidateCharacters != null;
    }

    public bool TryGetHaulDeliveryIntents(
        out IReadOnlyList<HaulDeliveryIntentSaveData> intents)
    {
        intents = haulDeliveryIntents;
        return intents != null;
    }

    public bool TryGetWildlife(
        out IReadOnlyList<WildlifeActor> candidateWildlife)
    {
        candidateWildlife = wildlife;
        return candidateWildlife != null;
    }

    public bool TryGetExteriorZones(
        out IReadOnlyList<ExteriorZoneMarker> candidateZones)
    {
        candidateZones = exteriorZones;
        return candidateZones != null;
    }

    public void SetFacilityCandidate(
        Grid grid,
        IReadOnlyList<BuildableObject> buildings)
    {
        if (facilityGrid != null || facilityBuildings != null)
        {
            throw new InvalidOperationException(
                "A facility-world restore candidate is already indexed.");
        }

        facilityGrid = grid ?? throw new ArgumentNullException(nameof(grid));
        facilityBuildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        AdvanceRevision();
    }

    public void ClearFacilityCandidate()
    {
        if (facilityGrid == null && facilityBuildings == null)
        {
            return;
        }

        facilityGrid = null;
        facilityBuildings = null;
        AdvanceRevision();
    }

    public void SetCharacterCandidate(IReadOnlyList<CharacterActor> candidateCharacters)
    {
        SetCharacterCandidate(
            candidateCharacters,
            Array.Empty<HaulDeliveryIntentSaveData>());
    }

    public void SetCharacterCandidate(
        IReadOnlyList<CharacterActor> candidateCharacters,
        IReadOnlyList<HaulDeliveryIntentSaveData> candidateHaulDeliveryIntents)
    {
        if (characters != null || haulDeliveryIntents != null)
        {
            throw new InvalidOperationException(
                "A character-world restore candidate is already indexed.");
        }

        characters = candidateCharacters
            ?? throw new ArgumentNullException(nameof(candidateCharacters));
        haulDeliveryIntents = candidateHaulDeliveryIntents
            ?? throw new ArgumentNullException(nameof(candidateHaulDeliveryIntents));
        AdvanceRevision();
    }

    public void ClearCharacterCandidate()
    {
        if (characters == null && haulDeliveryIntents == null)
        {
            return;
        }

        characters = null;
        haulDeliveryIntents = null;
        AdvanceRevision();
    }

    public void SetWildlifeCandidate(
        IReadOnlyList<WildlifeActor> candidateWildlife)
    {
        if (wildlife != null)
        {
            throw new InvalidOperationException(
                "A wildlife restore candidate is already indexed.");
        }

        wildlife = candidateWildlife
            ?? throw new ArgumentNullException(nameof(candidateWildlife));
        AdvanceRevision();
    }

    public void ClearWildlifeCandidate()
    {
        if (wildlife == null)
        {
            return;
        }

        wildlife = null;
        AdvanceRevision();
    }

    public void SetExteriorZoneCandidate(
        IReadOnlyList<ExteriorZoneMarker> candidateZones)
    {
        if (exteriorZones != null)
        {
            throw new InvalidOperationException(
                "An exterior-zone restore candidate is already indexed.");
        }

        exteriorZones = candidateZones
            ?? throw new ArgumentNullException(nameof(candidateZones));
        AdvanceRevision();
    }

    public void ClearExteriorZoneCandidate()
    {
        if (exteriorZones == null)
        {
            return;
        }

        exteriorZones = null;
        AdvanceRevision();
    }

    private void AdvanceRevision()
    {
        unchecked
        {
            Revision++;
        }
    }
}
