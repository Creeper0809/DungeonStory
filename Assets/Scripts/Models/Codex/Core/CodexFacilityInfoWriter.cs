using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CodexFacilityInfoWriter
{
    public static void Add(
        CodexState state,
        CodexFacilityObservationSnapshot facility,
        string info,
        CodexInfoSource source)
    {
        if (state == null || facility?.Entry == null || string.IsNullOrWhiteSpace(info))
        {
            return;
        }

        state.AddInfo(
            CodexEntryCategory.Facility,
            facility.Entry.EntryId,
            facility.Entry.Title,
            info,
            source);
    }

    public static string GetFacilityEntryId(int definitionId)
    {
        return $"facility:{definitionId}";
    }
}
