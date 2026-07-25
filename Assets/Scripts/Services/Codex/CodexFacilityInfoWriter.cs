public static class CodexFacilityInfoWriter
{
    public static void Add(CodexState state, BuildingSO building, string info, CodexInfoSource source)
    {
        if (state == null || building == null)
        {
            return;
        }

        state.AddInfo(
            CodexEntryCategory.Facility,
            GetFacilityEntryId(building),
            FacilityShopService.GetBuildingName(building),
            info,
            source);
    }

    public static string GetFacilityEntryId(BuildingSO building)
    {
        return building != null ? $"facility:{building.id}" : "facility:unknown";
    }
}
