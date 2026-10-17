using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CharacterRuntimeProfileFactory : ICharacterRuntimeProfileFactory
{
    private readonly IReadOnlyDictionary<CharacterArchetypeId, CharacterSO> archetypes;
    private readonly IReadOnlyDictionary<CharacterSpeciesId, CharacterSpeciesSO> species;
    private readonly IReadOnlyDictionary<CharacterTraitId, CharacterTraitSO> traits;

    public CharacterRuntimeProfileFactory(IGameContentCatalog content)
    {
        IGameContentCatalog required = content
            ?? throw new ArgumentNullException(nameof(content));
        archetypes = BuildUnique(
            required.GetAll<CharacterSO>(),
            value => value.DefinitionId,
            "character archetype");
        species = BuildUnique(
            required.GetAll<CharacterSpeciesSO>(),
            value => value.DefinitionId,
            "character species");
        traits = BuildUnique(
            required.GetAll<CharacterTraitSO>(),
            value => value.DefinitionId,
            "character trait");
    }

    public CharacterRuntimeProfile Create(CharacterSpawnRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!archetypes.TryGetValue(request.CharacterArchetypeId, out CharacterSO archetype))
        {
            throw new KeyNotFoundException(
                $"Unknown character archetype '{request.CharacterArchetypeId.Value}'.");
        }

        int maximumExpressedTraits = archetype.IsOwnerCandidate ? 5 : 4;
        if (request.ExpressedTraitIds.Count > maximumExpressedTraits)
        {
            throw new InvalidOperationException(
                $"Character archetype '{archetype.DefinitionId.Value}' is limited to "
                + $"{maximumExpressedTraits} expressed traits.");
        }

        if (!species.TryGetValue(request.PhenotypeSpeciesId, out CharacterSpeciesSO phenotype))
        {
            throw new KeyNotFoundException(
                $"Unknown phenotype species '{request.PhenotypeSpeciesId.Value}'.");
        }

        CharacterTraitSO[] expressed = request.ExpressedTraitIds
            .Select(RequireTrait)
            .ToArray();
        foreach (CharacterTraitId latentTraitId in request.LatentTraitIds)
        {
            RequireTrait(latentTraitId);
        }

        return CreateProfile(
            request,
            archetype,
            phenotype,
            expressed);
    }

#if UNITY_EDITOR
    public static CharacterRuntimeProfile CreateEditorSnapshot(
        CharacterSO archetype,
        IEnumerable<CharacterTraitSO> expressedTraits = null)
    {
        CharacterSpawnRequest request = CharacterSpawnRequest.FromAuthoring(
            archetype,
            expressedTraits);
        return CreateProfile(
            request,
            archetype,
            archetype.species,
            expressedTraits ?? archetype.traits);
    }
#endif

    private static CharacterRuntimeProfile CreateProfile(
        CharacterSpawnRequest request,
        CharacterSO archetype,
        CharacterSpeciesSO species,
        IEnumerable<CharacterTraitSO> expressedTraits)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (archetype == null) throw new ArgumentNullException(nameof(archetype));
        return CharacterRuntimeProfile.Create(request, species, expressedTraits);
    }

    private CharacterTraitSO RequireTrait(CharacterTraitId traitId)
    {
        return traits.TryGetValue(traitId, out CharacterTraitSO trait)
            ? trait
            : throw new KeyNotFoundException(
                $"Unknown character trait '{traitId.Value}'.");
    }

    private static IReadOnlyDictionary<TKey, TValue> BuildUnique<TKey, TValue>(
        IEnumerable<TValue> source,
        Func<TValue, TKey> keySelector,
        string label)
        where TKey : struct
        where TValue : UnityEngine.Object
    {
        Dictionary<TKey, TValue> result = new();
        foreach (TValue value in source ?? Array.Empty<TValue>())
        {
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"The root content catalog contains a null {label}.");
            }

            TKey key = keySelector(value);
            if (!result.TryAdd(key, value))
            {
                throw new InvalidOperationException(
                    $"Duplicate {label} ID '{key}'.");
            }
        }

        return result;
    }
}
