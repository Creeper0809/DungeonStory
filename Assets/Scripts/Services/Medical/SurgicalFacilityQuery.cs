using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class SurgicalFacilityQuery : ISurgicalFacilityQuery
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IRoomLayoutCache rooms;

    public SurgicalFacilityQuery(
        IBuildingWorldQuery buildings,
        IRoomLayoutCache rooms)
    {
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
    }

    public SurgicalFacilitySnapshot Evaluate(
        BuildableObject primaryFacility,
        SurgeryFacilityTag requiredTags)
    {
        if (primaryFacility == null
            || primaryFacility.isDestroy
            || primaryFacility.IsDamaged)
        {
            return Blocked(primaryFacility);
        }

        ISurgicalFacilityAbility primaryAbility = primaryFacility.BuildingData?
            .Abilities?
            .OfType<ISurgicalFacilityAbility>()
            .FirstOrDefault(ability => ability.IsPrimaryOperatingFacility);
        if (primaryAbility == null)
        {
            return Blocked(primaryFacility);
        }

        if (!rooms.TryGetRoom(primaryFacility, out RoomInstance room)
            || room == null
            || !room.IsUsable)
        {
            return Blocked(primaryFacility);
        }

        List<BuildableObject> supports = room.Furniture
            .Where(candidate => candidate != null
                && candidate != primaryFacility
                && !candidate.isDestroy
                && !candidate.IsDamaged
                && candidate.BuildingData?.Abilities?
                    .OfType<ISurgicalFacilityAbility>()
                    .Any() == true)
            .ToList();
        SurgeryFacilityTag tags = primaryAbility.FacilityTags;
        float sterility = primaryAbility.SterilityBonus;
        float speed = primaryAbility.WorkSpeedMultiplier;
        float success = primaryAbility.SuccessBonus;
        float anesthesia = primaryAbility.AnesthesiaBonus;
        foreach (BuildableObject support in supports)
        {
            foreach (ISurgicalFacilityAbility ability in
                     support.BuildingData.Abilities.OfType<ISurgicalFacilityAbility>())
            {
                tags |= ability.FacilityTags;
                sterility += ability.SterilityBonus;
                speed *= Mathf.Max(0.25f, ability.WorkSpeedMultiplier);
                success += ability.SuccessBonus;
                anesthesia += ability.AnesthesiaBonus;
            }
        }

        SurgeryFacilityTag missing = requiredTags & ~tags;
        DomainFailure blockFailure = missing == SurgeryFacilityTag.None
            ? DomainFailure.None
            : new DomainFailure(
                FailureCode.SurgeryFacilityUnavailable,
                missing.ToString());
        return new SurgicalFacilitySnapshot(
            primaryFacility,
            tags,
            sterility,
            speed,
            success,
            anesthesia,
            supports,
            blockFailure);
    }

    public bool TryFindBestFacility(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        out SurgicalFacilitySnapshot facility,
        out DomainFailure failure)
    {
        facility = default;
        failure = DomainFailure.None;
        if (subject == null || !subject.IsValid || procedure == null)
        {
            failure = new DomainFailure(FailureCode.SurgerySubjectInvalid);
            return false;
        }

        List<SurgicalFacilitySnapshot> candidates =
            GetCandidateFacilities(procedure)
                .ToList();
        if (candidates.Count == 0)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryFacilityUnavailable,
                procedure.ProcedureId);
            return false;
        }

        facility = candidates[0];
        return true;
    }

    public IReadOnlyList<SurgicalFacilitySnapshot> GetCandidateFacilities(
        SurgicalProcedureSO procedure,
        bool includeBlocked = false)
    {
        if (procedure == null)
        {
            return Array.Empty<SurgicalFacilitySnapshot>();
        }

        return buildings.Buildings
            .Where(candidate => candidate != null
                && !candidate.isDestroy
                && candidate.BuildingData?.Abilities?
                    .OfType<ISurgicalFacilityAbility>()
                    .Any(ability => ability.IsPrimaryOperatingFacility) == true)
            .Select(candidate => Evaluate(candidate, procedure.RequiredFacilityTags))
            .Where(snapshot => includeBlocked || snapshot.IsAvailable)
            .OrderByDescending(snapshot => snapshot.SuccessBonus)
            .ThenByDescending(snapshot => snapshot.Sterility)
            .ThenBy(snapshot => GetFacilityId(snapshot.PrimaryFacility), StringComparer.Ordinal)
            .ToList();
    }

    public string GetFacilityId(BuildableObject facility)
    {
        if (facility == null)
        {
            return string.Empty;
        }

        return facility.RequirePersistentInstanceId().Value;
    }

    private static SurgicalFacilitySnapshot Blocked(
        BuildableObject facility)
    {
        return new SurgicalFacilitySnapshot(
            facility,
            SurgeryFacilityTag.None,
            0f,
            1f,
            0f,
            0f,
            Array.Empty<BuildableObject>(),
            new DomainFailure(
                FailureCode.SurgeryFacilityUnavailable,
                facility != null
                    ? facility.RequirePersistentInstanceId().Value
                    : string.Empty));
    }

}
