using System;
using System.Collections.Generic;
using System.Linq;



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
        ExpressedTraitIds = NormalizeTraits(expressedTraitIds, 5, nameof(expressedTraitIds));
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
