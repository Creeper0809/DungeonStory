using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class BuildingOccupancyAssignmentDebugScenarios
{
    private static readonly BindingFlags InstanceFields =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static bool Verify()
    {
        string[] removedStateFields =
        {
            "currentUserCount",
            "visitReservations",
            "expiredVisitReservations",
            "nextVisitReservationExpiry",
            "workerReservation",
            "workerReservationUntil"
        };
        bool buildableOwnsNoReservationState = removedStateFields.All(name =>
            typeof(BuildableObject).GetField(name, InstanceFields) == null);
        bool collaboratorsOwnExpectedState =
            typeof(BuildingOccupancy).GetField("activeUsers", InstanceFields) != null
            && typeof(BuildingOccupancy).GetField("visitReservations", InstanceFields) != null
            && typeof(BuildingAssignment).GetField("workerReservation", InstanceFields) != null
            && typeof(BuildingAssignment).GetField("workerReservationUntil", InstanceFields) != null;
        bool collaboratorsArePlainObjects =
            !typeof(MonoBehaviour).IsAssignableFrom(typeof(BuildingOccupancy))
            && !typeof(MonoBehaviour).IsAssignableFrom(typeof(BuildingAssignment));
        bool publicApiPreserved =
            typeof(BuildableObject).GetMethod(nameof(BuildableObject.CanVisit)) != null
            && typeof(BuildableObject).GetMethod(nameof(BuildableObject.TryBeginUse)) != null
            && typeof(BuildableObject).GetMethod(nameof(BuildableObject.CompleteUse)) != null
            && typeof(BuildableObject).GetMethod(nameof(BuildableObject.TryReserveVisit)) != null
            && typeof(BuildableObject).GetMethod(nameof(BuildableObject.TryReserveWorker)) != null
            && typeof(BuildableObject).GetMethod(nameof(BuildableObject.GetWorkAssignmentStatus)) != null
            && typeof(BuildableObject).GetMethod(nameof(BuildableObject.GetWorkUrgency)) != null;

        return buildableOwnsNoReservationState
            && collaboratorsOwnExpectedState
            && collaboratorsArePlainObjects
            && publicApiPreserved
            && ThrowsArgumentNull(() => new BuildingOccupancy(null))
            && ThrowsArgumentNull(() => new BuildingAssignment(null));
    }

    private static bool ThrowsArgumentNull(Action create)
    {
        try
        {
            create();
        }
        catch (ArgumentNullException)
        {
            return true;
        }

        return false;
    }
}
