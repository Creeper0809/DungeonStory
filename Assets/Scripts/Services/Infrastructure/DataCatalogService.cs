using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

public interface IDataCatalog
{
    IReadOnlyDictionary<int, T> GetData<T>() where T : DataScriptableObject;
}

/// <summary>
/// Rebuildable numeric compatibility index projected from the immutable root
/// content catalog. It never owns content and never exposes a mutable map.
/// New domain APIs should prefer their typed stable-ID catalogs directly.
/// </summary>
public sealed class GameContentDataCatalog : IDataCatalog
{
    private readonly Dictionary<Type, IReadOnlyDictionary<int, DataScriptableObject>>
        definitionsByExactType;
    private readonly Dictionary<Type, object> typedViews = new();

    public GameContentDataCatalog(IGameContentCatalog content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        definitionsByExactType = BuildIndices(
            content.GetAll<DataScriptableObject>());
    }

    public IReadOnlyDictionary<int, T> GetData<T>() where T : DataScriptableObject
    {
        Type type = typeof(T);
        if (typedViews.TryGetValue(type, out object cached))
        {
            return (IReadOnlyDictionary<int, T>)cached;
        }

        if (!definitionsByExactType.TryGetValue(
                type,
                out IReadOnlyDictionary<int, DataScriptableObject> definitions))
        {
            throw new InvalidOperationException(
                $"GameContentCatalogSO has no registered {type.Name} definitions.");
        }

        IReadOnlyDictionary<int, T> typed = new ReadOnlyDictionary<int, T>(
            definitions.ToDictionary(
                pair => pair.Key,
                pair => (T)pair.Value));
        typedViews.Add(type, typed);
        return typed;
    }

    private static Dictionary<Type, IReadOnlyDictionary<int, DataScriptableObject>>
        BuildIndices(IEnumerable<DataScriptableObject> definitions)
    {
        Dictionary<Type, Dictionary<int, DataScriptableObject>> mutable = new();
        foreach (DataScriptableObject definition in definitions
                     ?? Array.Empty<DataScriptableObject>())
        {
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "GameContentCatalogSO contains a null data definition.");
            }

            Type type = definition.GetType();
            if (!mutable.TryGetValue(
                    type,
                    out Dictionary<int, DataScriptableObject> byId))
            {
                byId = new Dictionary<int, DataScriptableObject>();
                mutable.Add(type, byId);
            }

            if (!byId.TryAdd(definition.id, definition))
            {
                throw new InvalidOperationException(
                    $"Duplicate {type.Name} numeric compatibility ID "
                    + $"{definition.id}: '{byId[definition.id].name}' and "
                    + $"'{definition.name}'.");
            }
        }

        return mutable.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyDictionary<int, DataScriptableObject>)
                new ReadOnlyDictionary<int, DataScriptableObject>(pair.Value));
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
