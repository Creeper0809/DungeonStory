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

    public DataCatalogCodexReferenceCatalog(IDataCatalog catalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public IReadOnlyCollection<CharacterSpeciesSO> Species => catalog
        .GetData<CharacterSpeciesSO>()
        .Values
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
