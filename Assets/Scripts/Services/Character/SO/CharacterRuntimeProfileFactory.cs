using System;
using System.Collections.Generic;
using System.Linq;

public readonly struct CharacterArchetypeId : IEquatable<CharacterArchetypeId>
{
    private readonly string value;

    public CharacterArchetypeId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    public bool Equals(CharacterArchetypeId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is CharacterArchetypeId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator CharacterArchetypeId(string value) => new(value);
}

public sealed class CharacterSpawnRequest
{
    public CharacterSpawnRequest(
        CharacterArchetypeId characterArchetypeId,
        CharacterSpeciesId phenotypeSpeciesId,
        string visualVariantId,
        ReproductiveRole reproductiveRole,
        IEnumerable<CharacterTraitId> expressedTraitIds,
        IEnumerable<CharacterTraitId> latentTraitIds = null,
        IReadOnlyDictionary<string, int> innateAptitudes = null)
    {
        if (!characterArchetypeId.IsValid)
        {
            throw new ArgumentException(
                "A valid character archetype ID is required.",
                nameof(characterArchetypeId));
        }

        if (!phenotypeSpeciesId.IsValid)
        {
            throw new ArgumentException(
                "A valid phenotype species ID is required.",
                nameof(phenotypeSpeciesId));
        }

        CharacterArchetypeId = characterArchetypeId;
        PhenotypeSpeciesId = phenotypeSpeciesId;
        VisualVariantId = visualVariantId?.Trim() ?? string.Empty;
        ReproductiveRole = reproductiveRole;
        ExpressedTraitIds = NormalizeTraits(expressedTraitIds, 4, nameof(expressedTraitIds));
        LatentTraitIds = NormalizeTraits(latentTraitIds, 2, nameof(latentTraitIds));
        if (ExpressedTraitIds.Intersect(LatentTraitIds).Any())
        {
            throw new ArgumentException(
                "A trait cannot be both expressed and latent.");
        }

        InnateAptitudes = (innateAptitudes ?? new Dictionary<string, int>())
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => Math.Clamp(pair.Value, 0, 100),
                StringComparer.Ordinal);
    }

    public CharacterArchetypeId CharacterArchetypeId { get; }
    public CharacterSpeciesId PhenotypeSpeciesId { get; }
    public string VisualVariantId { get; }
    public ReproductiveRole ReproductiveRole { get; }
    public IReadOnlyList<CharacterTraitId> ExpressedTraitIds { get; }
    public IReadOnlyList<CharacterTraitId> LatentTraitIds { get; }
    public IReadOnlyDictionary<string, int> InnateAptitudes { get; }

    public static CharacterSpawnRequest FromAuthoring(
        CharacterSO archetype,
        IEnumerable<CharacterTraitSO> expressedTraits = null,
        ReproductiveRole reproductiveRole = ReproductiveRole.None,
        IEnumerable<CharacterTraitSO> latentTraits = null,
        IReadOnlyDictionary<string, int> innateAptitudes = null)
    {
        CharacterSO required = archetype
            ?? throw new ArgumentNullException(nameof(archetype));
        CharacterSpeciesSO species = required.species
            ?? throw new InvalidOperationException(
                $"Character archetype '{required.name}' has no phenotype species.");

        return new CharacterSpawnRequest(
            required.DefinitionId,
            species.DefinitionId,
            required.VisualVariantId,
            reproductiveRole,
            (expressedTraits ?? required.traits ?? Array.Empty<CharacterTraitSO>())
                .Select(RequireTraitId),
            (latentTraits ?? Array.Empty<CharacterTraitSO>()).Select(RequireTraitId),
            innateAptitudes);
    }

    private static CharacterTraitId RequireTraitId(CharacterTraitSO trait)
    {
        if (trait == null || !trait.DefinitionId.IsValid)
        {
            throw new InvalidOperationException(
                "A character spawn request contains an invalid authored trait.");
        }

        return trait.DefinitionId;
    }

    private static IReadOnlyList<CharacterTraitId> NormalizeTraits(
        IEnumerable<CharacterTraitId> source,
        int maximumCount,
        string parameterName)
    {
        CharacterTraitId[] values = (source ?? Array.Empty<CharacterTraitId>())
            .ToArray();
        if (values.Any(value => !value.IsValid))
        {
            throw new ArgumentException(
                "Trait IDs must be valid.",
                parameterName);
        }

        if (values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException(
                "Trait IDs must be unique.",
                parameterName);
        }

        if (values.Length > maximumCount)
        {
            throw new ArgumentException(
                $"At most {maximumCount} trait IDs are allowed.",
                parameterName);
        }

        return values;
    }
}

public interface ICharacterRuntimeProfileFactory
{
    CharacterRuntimeProfile Create(CharacterSpawnRequest request);
}

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

        return CharacterRuntimeProfile.Create(
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
        return CharacterRuntimeProfile.Create(
            request,
            archetype,
            archetype.species,
            expressedTraits ?? archetype.traits);
    }
#endif

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
