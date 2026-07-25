using System;
using System.Collections.Generic;

public interface IDataCatalog
{
    IReadOnlyDictionary<int, T> GetData<T>() where T : DataScriptableObject;
}

public sealed class DataManagerCatalog : IDataCatalog
{
    private readonly DataManager dataManager;

    public DataManagerCatalog(DataManager dataManager)
    {
        this.dataManager = dataManager
            ?? throw new ArgumentNullException(nameof(dataManager));
    }

    public IReadOnlyDictionary<int, T> GetData<T>() where T : DataScriptableObject
    {
        Dictionary<int, T> data = dataManager.GetData<T>();
        return data
            ?? throw new InvalidOperationException($"{nameof(DataManager)} returned no data for {typeof(T).Name}.");
    }
}

public interface IBuildingDefinitionLookup
{
    BuildingSO GetBuilding(int id);
}

public sealed class BuildingDefinitionLookup : IBuildingDefinitionLookup
{
    private readonly IDataCatalog catalog;

    public BuildingDefinitionLookup(IDataCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public BuildingSO GetBuilding(int id)
    {
        IReadOnlyDictionary<int, BuildingSO> buildings = catalog.GetData<BuildingSO>();
        if (!buildings.TryGetValue(id, out BuildingSO building))
        {
            throw new KeyNotFoundException($"BuildingSO id {id} was not found in {nameof(IDataCatalog)}.");
        }

        return building;
    }
}
