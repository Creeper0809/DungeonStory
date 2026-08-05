using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class CodexRecipeRecorder
{
    public static void RecordResearch(CodexState state, CodexResearchObservationSnapshot snapshot)
    {
        if (state == null || snapshot == null)
        {
            return;
        }

        ApplyEntries(state, snapshot.UnlockEntries);
        ImportSynthesisRecipes(state, snapshot.Recipes);
    }

    public static void RecordSynthesis(CodexState state, CodexRecipeObservationSnapshot snapshot)
    {
        ImportSynthesisRecipes(state, snapshot);
    }

    public static void ImportSynthesisRecipes(
        CodexState state,
        CodexRecipeObservationSnapshot snapshot)
    {
        if (state == null || snapshot == null)
        {
            return;
        }

        ApplyEntries(state, snapshot.Entries);
    }

    private static void ApplyEntries(
        CodexState state,
        System.Collections.Generic.IEnumerable<CodexEntryObservationSnapshot> entries)
    {
        foreach (CodexEntryObservationSnapshot entry in entries
                     ?? Array.Empty<CodexEntryObservationSnapshot>())
        {
            CodexObservationRecorder.Apply(state, entry);
        }
    }
}
