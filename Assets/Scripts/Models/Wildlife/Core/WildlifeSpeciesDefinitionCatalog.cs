public interface IWildlifeSpeciesDefinitionCatalog
{
    bool TryGetSpecies(
        string speciesId,
        out WildlifeSpeciesDefinition species);
}
