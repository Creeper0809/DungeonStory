using System;

public interface ICodexReferenceImporter
{
    void Import(CodexState state, BlueprintResearchState researchState);
}

public sealed class CodexReferenceImporter : ICodexReferenceImporter
{
    private readonly ICodexReferenceCatalog catalog;
    private readonly IFacilitySynthesisRecipeQuery synthesisRecipeQuery;

    public CodexReferenceImporter(
        ICodexReferenceCatalog catalog,
        IFacilitySynthesisRecipeQuery synthesisRecipeQuery)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.synthesisRecipeQuery = synthesisRecipeQuery
            ?? throw new ArgumentNullException(nameof(synthesisRecipeQuery));
    }

    public void Import(CodexState state, BlueprintResearchState researchState)
    {
        if (state == null)
        {
            return;
        }

        foreach (CharacterSpeciesSO species in catalog.Species)
        {
            CodexObservationRecorder.ObserveSpecies(state, species, CodexInfoSource.System);
        }

        foreach (BuildingSO building in catalog.Facilities)
        {
            CodexObservationRecorder.ObserveFacility(state, building, CodexInfoSource.System);
        }

        CodexRecipeRecorder.ImportSynthesisRecipes(state, researchState, synthesisRecipeQuery);
        CodexInvasionRecorder.SeedBreakthroughIntruder(state);
    }
}
