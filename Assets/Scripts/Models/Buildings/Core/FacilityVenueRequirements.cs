using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DungeonStory.Buildings
{
    [Serializable]
    public sealed class FacilityVenueAnchorSelector
    {
        public List<string> exactBuildingDefinitionIds = new();
        public FacilityRole facilityRoles = FacilityRole.None;

        public bool Matches(string definitionId, FacilityRole roles)
        {
            bool exact = exactBuildingDefinitionIds != null
                && exactBuildingDefinitionIds.Contains(
                    definitionId,
                    StringComparer.Ordinal);
            bool role = facilityRoles != FacilityRole.None
                && (roles & facilityRoles) != FacilityRole.None;
            return exact || role;
        }

        public IReadOnlyList<string> Validate(string ownerId)
        {
            List<string> errors = new();
            string owner = string.IsNullOrWhiteSpace(ownerId)
                ? "venue"
                : ownerId;
            string[] exact = exactBuildingDefinitionIds?.ToArray()
                ?? Array.Empty<string>();
            if (exact.Length == 0 && facilityRoles == FacilityRole.None)
                errors.Add($"'{owner}' requires an exact facility or facility role anchor.");
            if (exact.Any(value => !IsCanonicalBuildingDefinitionId(value)))
                errors.Add($"'{owner}' has a non-canonical exact facility anchor.");
            if (exact.Distinct(StringComparer.Ordinal).Count() != exact.Length)
                errors.Add($"'{owner}' has duplicate exact facility anchors.");
            if (!IsValidRoleMask(facilityRoles))
                errors.Add($"'{owner}' has an unknown facility role anchor.");
            return errors;
        }

        private static bool IsCanonicalBuildingDefinitionId(string value) =>
            !string.IsNullOrEmpty(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && value.StartsWith("building:", StringComparison.Ordinal);

        internal static bool IsValidRoleMask(FacilityRole value)
        {
            const FacilityRole known = FacilityRole.Meal
                | FacilityRole.Purchase
                | FacilityRole.Rest
                | FacilityRole.Training
                | FacilityRole.Research
                | FacilityRole.Mana
                | FacilityRole.Logistics
                | FacilityRole.Toilet
                | FacilityRole.Hygiene
                | FacilityRole.Administration
                | FacilityRole.Security
                | FacilityRole.Entertainment
                | FacilityRole.Medical;
            return (value & ~known) == FacilityRole.None;
        }
    }

    [Serializable]
    public sealed class FacilityVenueRequirements
    {
        public FacilityVenueAnchorSelector anchor = new();
        public FacilityRole requiredRoomFacilityRoles = FacilityRole.None;
        public List<FacilityVenueAnchorSelector> requiredRoomFacilities = new();
        [Min(0)] public int minimumSeats;
        [Min(0)] public int minimumTables;
        [Min(0)] public int minimumServiceCapacity;
        [Min(1)] public int minimumEventCells = 1;
        [Min(0)] public int minimumVacantBeds;
        public bool requireResidentHeadroom;
        public bool requireWetBath;
        public bool requireEmergencySurgeryFacility;

        public IReadOnlyList<string> Validate(string ownerId)
        {
            List<string> errors = new();
            string owner = string.IsNullOrWhiteSpace(ownerId)
                ? "venue"
                : ownerId;
            if (anchor == null)
                errors.Add($"'{owner}' requires a venue anchor selector.");
            else
                errors.AddRange(anchor.Validate(owner));
            if (!FacilityVenueAnchorSelector.IsValidRoleMask(
                    requiredRoomFacilityRoles))
                errors.Add($"'{owner}' has unknown required room facility roles.");
            if (requiredRoomFacilities == null
                || requiredRoomFacilities.Any(value => value == null))
                errors.Add($"'{owner}' has a missing required room facility selector.");
            else
            {
                for (int index = 0; index < requiredRoomFacilities.Count; index++)
                    errors.AddRange(requiredRoomFacilities[index].Validate(
                        $"{owner}:room:{index}"));
            }
            if (minimumSeats < 0
                || minimumTables < 0
                || minimumServiceCapacity < 0
                || minimumEventCells < 1
                || minimumVacantBeds < 0)
                errors.Add($"'{owner}' has invalid venue capacity requirements.");
            if (requireResidentHeadroom && minimumVacantBeds < 1)
                errors.Add($"'{owner}' resident headroom requires at least one vacant bed.");
            if (requireWetBath
                && (requiredRoomFacilityRoles & FacilityRole.Hygiene)
                    != FacilityRole.Hygiene)
                errors.Add($"'{owner}' wet bath requires the Hygiene room role.");
            return errors;
        }
    }
}
