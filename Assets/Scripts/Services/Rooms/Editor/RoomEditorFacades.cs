public static class RoomRegistry
{
    private static readonly IRoomLayoutCache Cache = new RoomLayoutCache();

    internal static IRoomLayoutCache EditorCache => Cache;

    public static RoomLayout GetLayout(Grid grid)
    {
        return Cache.GetLayout(grid);
    }

    public static bool TryGetRoom(BuildableObject part, out RoomInstance room)
    {
        return Cache.TryGetRoom(part, out room);
    }

    public static void Clear()
    {
        Cache.Clear();
    }
}

public static class RoomFacilityPolicy
{
    private static readonly IRoomFacilityPolicy Policy =
        new RoomFacilityPolicyService(RoomRegistry.EditorCache);

    public static bool IsFacilityRoleAvailable(
        BuildableObject building,
        FacilityRole requestedRole,
        out string rejectReason)
    {
        return Policy.IsFacilityRoleAvailable(building, requestedRole, out rejectReason);
    }

    public static float GetRoomUtilityScore(BuildableObject building, FacilityRole role)
    {
        return Policy.GetRoomUtilityScore(building, role);
    }

    public static int GetEffectiveCapacity(BuildableObject building)
    {
        return Policy.GetEffectiveCapacity(building);
    }

    public static FacilityRoomOperationalProfile GetOperationalProfile(BuildableObject building)
    {
        return Policy.GetOperationalProfile(building);
    }
}
