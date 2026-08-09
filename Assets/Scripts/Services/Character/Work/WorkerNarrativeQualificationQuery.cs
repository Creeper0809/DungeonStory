using System;
using System.Collections.Generic;

/// <summary>
/// Projects the existing narrative and runtime-profile authorities into the
/// worker-policy qualification contract. Crafting systems must not create a
/// second copy of skill, career, or trait state.
/// </summary>
public sealed class WorkerNarrativeQualificationQuery :
    IWorkerNarrativeQualificationQuery
{
    private readonly ICharacterNarrativeQuery narratives;
    private readonly ICharacterWorldQuery characters;
    private readonly Dictionary<string, CharacterActor> actorsById =
        new(StringComparer.Ordinal);
    private int indexedCharacterVersion = int.MinValue;

    public WorkerNarrativeQualificationQuery(
        ICharacterNarrativeQuery narratives,
        ICharacterWorldQuery characters)
    {
        this.narratives = narratives
            ?? throw new ArgumentNullException(nameof(narratives));
        this.characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
    }

    public int GetSkillExperience(string characterId, string skillId)
    {
        if (!TryGetNarrative(characterId, out CharacterNarrativeSnapshot narrative)
            || string.IsNullOrWhiteSpace(skillId)
            || narrative.SkillExperienceById == null)
        {
            return 0;
        }

        return narrative.SkillExperienceById.TryGetValue(
            skillId.Trim(),
            out int experience)
                ? Math.Max(0, experience)
                : 0;
    }

    public CareerRank GetCareerRank(string characterId, string skillId) =>
        CareerRules.ResolveRank(GetSkillExperience(characterId, skillId));

    public bool HasTrait(string characterId, string traitId)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || string.IsNullOrWhiteSpace(traitId))
        {
            return false;
        }

        string normalizedTraitId = traitId.Trim();
        if (TryGetNarrative(characterId, out CharacterNarrativeSnapshot narrative)
            && ContainsOrdinal(
                narrative.ExpressedHeritableTraitIds,
                normalizedTraitId))
        {
            return true;
        }

        RebuildActorIndexIfNeeded();
        return actorsById.TryGetValue(characterId.Trim(), out CharacterActor actor)
            && actor != null
            && actor.profile != null
            && actor.profile.HasTrait(normalizedTraitId);
    }

    private bool TryGetNarrative(
        string characterId,
        out CharacterNarrativeSnapshot narrative)
    {
        narrative = null;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return false;
        }

        CharacterId id = new(characterId.Trim());
        return id.IsValid && narratives.TryGet(id, out narrative);
    }

    private void RebuildActorIndexIfNeeded()
    {
        if (indexedCharacterVersion == characters.CharacterVersion)
        {
            return;
        }

        actorsById.Clear();
        IReadOnlyList<CharacterActor> current = characters.Characters;
        for (int index = 0; index < current.Count; index++)
        {
            CharacterActor actor = current[index];
            string id = actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
            if (id.Length > 0)
            {
                actorsById[id] = actor;
            }
        }
        indexedCharacterVersion = characters.CharacterVersion;
    }

    private static bool ContainsOrdinal(
        IReadOnlyList<string> values,
        string expected)
    {
        if (values == null)
        {
            return false;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (string.Equals(values[index], expected, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }
}
