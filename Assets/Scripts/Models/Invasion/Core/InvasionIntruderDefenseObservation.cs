using System;

public readonly struct InvasionDefenseObservationSnapshot
{
    public InvasionDefenseObservationSnapshot(
        BuildingInstanceId facilityId,
        string facilityFamilyId,
        int range)
    {
        FacilityId = facilityId;
        FacilityFamilyId = facilityFamilyId ?? string.Empty;
        Range = range;
    }

    public BuildingInstanceId FacilityId { get; }
    public string FacilityFamilyId { get; }
    public int Range { get; }
}

public static class InvasionIntruderDefenseObservation
{
    public const int VisionRange = 4;

    public static bool IsObservable(InvasionDefenseObservationSnapshot snapshot)
    {
        return snapshot.Range > 0 || IsExposedFamily(snapshot.FacilityFamilyId);
    }

    private static bool IsExposedFamily(string familyId)
    {
        return string.Equals(familyId, "defense:detection", StringComparison.Ordinal)
            || string.Equals(familyId, "defense:control", StringComparison.Ordinal)
            || string.Equals(familyId, "defense:supply", StringComparison.Ordinal)
            || string.Equals(familyId, "defense:maintenance", StringComparison.Ordinal)
            || string.Equals(familyId, "defense:launcher", StringComparison.Ordinal);
    }
}
