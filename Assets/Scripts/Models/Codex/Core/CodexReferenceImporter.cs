using System;
using UnityEngine.Scripting.APIUpdating;

public interface ICodexReferenceImporter
{
    void Import(CodexState state);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CodexReferenceImporter : ICodexReferenceImporter
{
    private readonly ICodexReferenceSnapshotQueryPort query;

    public CodexReferenceImporter(ICodexReferenceSnapshotQueryPort query)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public void Import(CodexState state)
    {
        if (state == null)
        {
            return;
        }

        CodexReferenceSnapshot snapshot = query.Capture()
            ?? throw new InvalidOperationException("Codex reference query returned null.");
        foreach (CodexCharacterObservationSnapshot character in snapshot.Characters)
        {
            CodexObservationRecorder.ObserveCharacter(state, character);
        }

        foreach (CodexFacilityObservationSnapshot facility in snapshot.Facilities)
        {
            CodexObservationRecorder.ObserveFacility(state, facility);
        }

        CodexRecipeRecorder.ImportSynthesisRecipes(state, snapshot.Recipes);
        CodexInvasionRecorder.SeedBreakthroughIntruder(state);
    }
}
