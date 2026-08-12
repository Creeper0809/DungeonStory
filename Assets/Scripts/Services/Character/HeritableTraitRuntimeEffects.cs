using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IHeritableTraitEffectQuery
{
    float GetMultiplier(
        CharacterId characterId,
        HeritableTraitConsequenceKind kind,
        string targetId);
}

public sealed class HeritableTraitEffectQuery : IHeritableTraitEffectQuery
{
    private readonly ICharacterNarrativeQuery narratives;
    private readonly IReadOnlyDictionary<string, HeritableTraitDefinitionSO> definitions;

    public HeritableTraitEffectQuery(
        ICharacterNarrativeQuery narratives,
        IGameContentCatalog content)
    {
        this.narratives = narratives
            ?? throw new ArgumentNullException(nameof(narratives));
        definitions = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<HeritableTraitDefinitionSO>()
            .Where(value => value != null)
            .ToDictionary(value => value.traitId, StringComparer.Ordinal);
    }

    public float GetMultiplier(
        CharacterId characterId,
        HeritableTraitConsequenceKind kind,
        string targetId)
    {
        if (!characterId.IsValid
            || !narratives.TryGet(characterId, out CharacterNarrativeSnapshot narrative))
            return 1f;
        HeritableTraitDefinitionSO[] expressed = narrative.ExpressedHeritableTraitIds
            .Where(definitions.ContainsKey)
            .Select(value => definitions[value])
            .ToArray();
        return 1f + HeritableTraitModifierResolver.ResolveCappedDelta(
            expressed,
            kind,
            targetId);
    }
}
