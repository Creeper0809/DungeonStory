using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IRunCharacterCatalog
{
    IReadOnlyCollection<CharacterSO> Characters { get; }
}
public interface IOwnerCandidateCatalog
{
    IReadOnlyCollection<CharacterSO> OwnerCandidates { get; }
}

public interface IRunStartVariableCatalog
{
    IReadOnlyCollection<BuildingSO> Buildings { get; }
    IReadOnlyCollection<CharacterSO> Characters { get; }
    IReadOnlyCollection<FacilityBlueprintSO> Blueprints { get; }
}

public sealed class ResourceRunCharacterCatalog : IRunCharacterCatalog
{
    private readonly CharacterSO[] characters;

    [VContainer.Inject]
    public ResourceRunCharacterCatalog(IGameContentCatalog content)
        : this((content ?? throw new ArgumentNullException(nameof(content))).GetAll<CharacterSO>())
    {
    }

    public ResourceRunCharacterCatalog(IEnumerable<CharacterSO> definitions)
    {
        CharacterSO[] loaded = (definitions ?? throw new ArgumentNullException(nameof(definitions)))
            .Where(value => value != null)
            .Distinct()
            .OrderBy(value => value.id)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .ToArray();
        if (loaded.Length == 0)
        {
            throw new InvalidOperationException(
                "The root content catalog contains no authored CharacterSO definitions.");
        }
        IGrouping<int, CharacterSO> duplicate = loaded.GroupBy(value => value.id)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
            throw new InvalidOperationException("Character numeric ID '" + duplicate.Key
                + "' has multiple registered definitions: " + string.Join(", ", duplicate.Select(value => value.name)));
        characters = loaded;
    }

    public IReadOnlyCollection<CharacterSO> Characters => characters;
}

public sealed class ResourceOwnerCandidateCatalog : IOwnerCandidateCatalog
{
    private readonly IRunCharacterCatalog characterCatalog;

    public ResourceOwnerCandidateCatalog(IRunCharacterCatalog characterCatalog)
    {
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
    }

    public IReadOnlyCollection<CharacterSO> OwnerCandidates => characterCatalog
        .Characters
        .Where((candidate) => candidate != null && candidate.IsOwnerCandidate)
        .OrderBy((candidate) => candidate.id)
        .ToArray();
}

public sealed class RunStartVariableCatalog : IRunStartVariableCatalog
{
    private readonly IDataCatalog catalog;
    private readonly IRunCharacterCatalog characterCatalog;

    public RunStartVariableCatalog(IDataCatalog catalog, IRunCharacterCatalog characterCatalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.characterCatalog = characterCatalog
            ?? throw new ArgumentNullException(nameof(characterCatalog));
    }

    public IReadOnlyCollection<BuildingSO> Buildings => catalog
        .GetData<BuildingSO>()
        .Values
        .Where((building) => building != null)
        .ToArray();

    public IReadOnlyCollection<CharacterSO> Characters => characterCatalog.Characters;

    public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => catalog
        .GetData<FacilityBlueprintSO>()
        .Values
        .Where((blueprint) => blueprint != null)
        .ToArray();
}
