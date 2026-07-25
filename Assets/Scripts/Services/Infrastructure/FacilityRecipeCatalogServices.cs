using System;
using System.Collections.Generic;
using System.Linq;

public interface IFacilitySynthesisRecipeCatalog
{
    IReadOnlyList<FacilitySynthesisRecipeSO> GetRecipes();
}

public interface IFacilitySynthesisRecipeQuery
{
    IReadOnlyList<FacilitySynthesisRecipeSO> GetAllRecipes();
    bool IsVisible(FacilitySynthesisRecipeSO recipe, BlueprintResearchState researchState);
    IReadOnlyList<FacilitySynthesisRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState);
    FacilitySynthesisRecipeSnapshot ToSnapshot(
        FacilitySynthesisRecipeSO recipe,
        BlueprintResearchState researchState);
}

public interface IFacilityEvolutionRecipeQuery : IFacilityEvolutionRecipeProvider
{
    bool IsVisible(FacilityEvolutionRecipeSO recipe, BlueprintResearchState researchState);
    IReadOnlyList<FacilityEvolutionRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState);
    IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(
        BuildableObject facility,
        BlueprintResearchState researchState);
}

public sealed class DataCatalogFacilitySynthesisRecipeCatalog : IFacilitySynthesisRecipeCatalog
{
    private readonly IDataCatalog catalog;

    public DataCatalogFacilitySynthesisRecipeCatalog(IDataCatalog catalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<FacilitySynthesisRecipeSO> GetRecipes()
    {
        return catalog
            .GetData<FacilitySynthesisRecipeSO>()
            .Values
            .Where((recipe) => recipe != null && recipe.HasValidData)
            .OrderBy((recipe) => recipe.id)
            .ToArray();
    }
}

public sealed class FacilitySynthesisRecipeQuery : IFacilitySynthesisRecipeQuery
{
    private readonly IFacilitySynthesisRecipeCatalog catalog;
    private readonly IMetaProgressionRuntimeReader metaProgressionReader;

    public FacilitySynthesisRecipeQuery(
        IFacilitySynthesisRecipeCatalog catalog,
        IMetaProgressionRuntimeReader metaProgressionReader)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.metaProgressionReader = metaProgressionReader
            ?? throw new ArgumentNullException(nameof(metaProgressionReader));
    }

    public IReadOnlyList<FacilitySynthesisRecipeSO> GetAllRecipes()
    {
        return catalog.GetRecipes();
    }

    public bool IsVisible(FacilitySynthesisRecipeSO recipe, BlueprintResearchState researchState)
    {
        return FacilitySynthesisService.IsRecipeVisible(recipe, researchState, metaProgressionReader);
    }

    public IReadOnlyList<FacilitySynthesisRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)
    {
        return GetAllRecipes()
            .Where((recipe) => IsVisible(recipe, researchState))
            .ToList();
    }

    public FacilitySynthesisRecipeSnapshot ToSnapshot(
        FacilitySynthesisRecipeSO recipe,
        BlueprintResearchState researchState)
    {
        return FacilitySynthesisService.ToSnapshot(recipe, researchState, metaProgressionReader);
    }
}

public sealed class DataCatalogFacilityEvolutionRecipeProvider : IFacilityEvolutionRecipeProvider
{
    private readonly IDataCatalog catalog;

    public DataCatalogFacilityEvolutionRecipeProvider(IDataCatalog catalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes()
    {
        return catalog
            .GetData<FacilityEvolutionRecipeSO>()
            .Values
            .Where((recipe) => recipe != null && recipe.HasValidData)
            .OrderBy((recipe) => recipe.id)
            .ToArray();
    }
}

public sealed class FacilityEvolutionRecipeQuery : IFacilityEvolutionRecipeQuery
{
    private readonly IFacilityEvolutionRecipeProvider provider;
    private readonly IMetaProgressionRuntimeReader metaProgressionReader;
    private readonly IFacilityEvolutionStateComponentFactory stateComponentFactory;

    public FacilityEvolutionRecipeQuery(
        IFacilityEvolutionRecipeProvider provider,
        IMetaProgressionRuntimeReader metaProgressionReader,
        IFacilityEvolutionStateComponentFactory stateComponentFactory)
    {
        this.provider = provider
            ?? throw new ArgumentNullException(nameof(provider));
        this.metaProgressionReader = metaProgressionReader
            ?? throw new ArgumentNullException(nameof(metaProgressionReader));
        this.stateComponentFactory = stateComponentFactory
            ?? throw new ArgumentNullException(nameof(stateComponentFactory));
    }

    public IReadOnlyList<FacilityEvolutionRecipeSO> GetRecipes()
    {
        return provider.GetRecipes();
    }

    public bool IsVisible(FacilityEvolutionRecipeSO recipe, BlueprintResearchState researchState)
    {
        return FacilityEvolutionService.IsRecipeVisible(recipe, researchState, metaProgressionReader);
    }

    public IReadOnlyList<FacilityEvolutionRecipeSO> GetVisibleRecipes(BlueprintResearchState researchState)
    {
        return GetRecipes()
            .Where((recipe) => IsVisible(recipe, researchState))
            .ToList();
    }

    public IReadOnlyList<FacilityEvolutionRecipeSO> GetSourceCandidates(
        BuildableObject facility,
        BlueprintResearchState researchState)
    {
        return FacilityEvolutionService.GetSourceCandidates(
            facility,
            GetRecipes(),
            researchState,
            this,
            stateComponentFactory);
    }
}

public sealed class DataCatalogFacilityEvolutionRecordTokenDefinitionProvider :
    IFacilityEvolutionRecordTokenDefinitionProvider
{
    private readonly IDataCatalog catalog;

    public DataCatalogFacilityEvolutionRecordTokenDefinitionProvider(IDataCatalog catalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions()
    {
        return catalog
            .GetData<FacilityEvolutionRecordTokenDefinitionSO>()
            .Values
            .Where((definition) => definition != null
                && !string.IsNullOrWhiteSpace(definition.EffectiveId))
            .OrderBy((definition) => definition.EffectiveId)
            .ToArray();
    }

    public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId)
    {
        if (string.IsNullOrWhiteSpace(tokenId))
        {
            return null;
        }

        return GetDefinitions()
            .FirstOrDefault((definition) => string.Equals(
                definition.EffectiveId,
                tokenId,
                StringComparison.Ordinal));
    }
}
