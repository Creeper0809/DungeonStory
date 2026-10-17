using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

internal sealed class CharacterConsumablesWorldReferences
{
    private readonly ICharacterAiWorldRegistry world;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;

    internal CharacterConsumablesWorldReferences(ICharacterAiWorldRegistry world,
        IRestoreWorldCandidateQuery restoreWorldCandidates)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.restoreWorldCandidates = restoreWorldCandidates;
    }

    internal IReadOnlyList<CharacterActor> GetCharacters()
    {
        if (restoreWorldCandidates != null
            && restoreWorldCandidates.TryGetCharacters(
                out IReadOnlyList<CharacterActor> candidates))
        {
            return candidates;
        }

        // Consumable execution is an active-world concern. Include the active
        // registry as the primary authority while retaining lifetime entries
        // for suspended/restorable actors. This also makes owner replacement
        // atomic from the consumer's perspective when the old owner is
        // destroyed at end-of-frame.
        return CharacterActorCollection.DistinctByGameObject(
            world.Characters.Concat(world.AllCharacters));
    }

    internal IReadOnlyList<BuildableObject> GetBuildings() =>
        restoreWorldCandidates != null
        && restoreWorldCandidates.TryGetBuildings(
            out IReadOnlyList<BuildableObject> candidates)
            ? candidates
            : world.Buildings;

    internal CharacterActor FindActor(CharacterId id) =>
        id.IsValid
            ? GetCharacters().FirstOrDefault(actor => actor != null
                && CharacterPersistentIdentity.Require(actor).Equals(id))
            : null;

    internal CharacterActor RequireActor(CharacterId id) =>
        FindActor(id) ?? throw new InvalidOperationException(
            $"Consumables actor '{id.Value}' is unavailable.");

    internal BuildableObject FindFacility(BuildingInstanceId id) =>
        id.IsValid
            ? GetBuildings().FirstOrDefault(building => building != null
                && building.RequirePersistentInstanceId().Equals(id))
            : null;

}
