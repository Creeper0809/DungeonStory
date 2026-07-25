public static class CharacterVisitPolicy
{
    public const FacilityRole CustomerInterestRoles =
        FacilityRole.Purchase
        | FacilityRole.Training
        | FacilityRole.Research
        | FacilityRole.Mana
        | FacilityRole.Toilet
        | FacilityRole.Hygiene;

    public const FacilityRole StaffOffDutyInterestRoles =
        FacilityRole.Training
        | FacilityRole.Research
        | FacilityRole.Mana
        | FacilityRole.Toilet
        | FacilityRole.Hygiene;

    public const string StaffPurchaseShopRejectReason = "Staff cannot use purchase shops";

    public static FacilityRole GetInterestRoles(CharacterActor actor)
    {
        return CharacterWorkRoleUtility.TryGetWork(actor, out _)
            ? StaffOffDutyInterestRoles
            : CustomerInterestRoles;
    }

    public static bool IsStaffPurchaseShop(CharacterActor actor, BuildableObject building)
    {
        return CharacterWorkRoleUtility.TryGetWork(actor, out _)
            && building is IRetailFacility
            && building.SupportsFacilityRole(FacilityRole.Purchase);
    }

    public static bool CanVisitBuilding(
        CharacterActor actor,
        BuildableObject building,
        bool alreadyVisited,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (building == null)
        {
            failureReason = "No building";
            return false;
        }

        if (building.isDestroy)
        {
            failureReason = "Building destroyed";
            return false;
        }

        if (alreadyVisited)
        {
            failureReason = "Already visited";
            return false;
        }

        if (!building.CanVisit(actor, out failureReason))
        {
            return false;
        }

        if (IsStaffPurchaseShop(actor, building))
        {
            failureReason = StaffPurchaseShopRejectReason;
            return false;
        }

        return true;
    }

}
