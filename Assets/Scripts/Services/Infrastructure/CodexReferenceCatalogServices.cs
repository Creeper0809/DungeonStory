using System;
using System.Collections.Generic;
using System.Linq;

public interface ICodexReferenceCatalog
{
    IReadOnlyCollection<CharacterSpeciesSO> Species { get; }
    IReadOnlyCollection<BuildingSO> Facilities { get; }
}

public sealed class DataCatalogCodexReferenceCatalog : ICodexReferenceCatalog
{
    private readonly IDataCatalog catalog;
    private readonly ICharacterSpeciesCatalog speciesCatalog;

    public DataCatalogCodexReferenceCatalog(
        IDataCatalog catalog,
        ICharacterSpeciesCatalog speciesCatalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
    }

    public IReadOnlyCollection<CharacterSpeciesSO> Species => speciesCatalog
        .All
        .Where((species) => species != null)
        .OrderBy((species) => species.id)
        .ToArray();

    public IReadOnlyCollection<BuildingSO> Facilities => catalog
        .GetData<BuildingSO>()
        .Values
        .Where((building) => building != null)
        .OrderBy((building) => building.id)
        .ToArray();
}
