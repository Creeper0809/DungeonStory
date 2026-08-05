using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CodexObservationRecorder
{
    public static void ObserveCharacter(
        CodexState state,
        CodexCharacterObservationSnapshot character)
    {
        Apply(state, character?.Entry);
    }

    public static void ObserveFacility(
        CodexState state,
        CodexFacilityObservationSnapshot facility)
    {
        Apply(state, facility?.Entry);
    }

    public static void Apply(CodexState state, CodexEntryObservationSnapshot observation)
    {
        if (state == null || observation == null)
        {
            return;
        }

        CodexEntryRecord entry = state.GetOrCreate(
            observation.Category,
            observation.EntryId,
            observation.Title);
        foreach (CodexInfoLine line in observation.Lines ?? Array.Empty<CodexInfoLine>())
        {
            entry.AddInfo(line.Text, line.Source);
        }
    }
}
