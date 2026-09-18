using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class WildlifeCaptureRestoreWorldAdapter :
    IWildlifeCaptureRestoreWorld
{
    private readonly ICharacterAiWorldRegistry world;
    private readonly IRoomLayoutCache rooms;
    private readonly IResourceEconomyContentCatalog contentCatalog;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly Grid grid;
    private readonly IReadOnlyDictionary<string, CharacterActor> characters;
    private readonly IReadOnlyList<WildlifeActor> wildlife;

    internal WildlifeCaptureRestoreWorldAdapter(
        ICharacterAiWorldRegistry world,
        IRoomLayoutCache rooms,
        IGridSystemProvider gridProvider,
        IResourceEconomyContentCatalog contentCatalog,
        IWildlifeSpeciesCatalogProvider speciesCatalog)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
        this.contentCatalog = contentCatalog
            ?? throw new ArgumentNullException(nameof(contentCatalog));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
        if (gridProvider == null)
        {
            throw new ArgumentNullException(nameof(gridProvider));
        }

        gridProvider.TryGetGrid(out grid);
        wildlife = world.Wildlife;
        characters = world.Characters
            .Where(actor => CharacterPersistentIdentity.TryGet(actor, out _))
            .GroupBy(
                actor => CharacterPersistentIdentity.Require(actor).Value,
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
    }

    public bool HasActiveGrid => grid != null;

    public bool HasMatchingLiveWildlife(
        string wildlifeId,
        string speciesId)
    {
        WildlifeActor actor = wildlife.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.WildlifeId,
                wildlifeId,
                StringComparison.Ordinal));
        return actor != null
            && actor.IsAlive
            && string.Equals(
                actor.SpeciesId,
                speciesId,
                StringComparison.Ordinal);
    }

    public bool HasSpecies(string speciesId) =>
        speciesCatalog.TryGetSpecies(speciesId, out _);

    public bool SpeciesSupportsRole(
        string speciesId,
        CapturedWildlifeRoleId roleId) =>
        speciesCatalog.TryGetSpecies(
            speciesId,
            out WildlifeSpeciesDefinition species)
        && (roleId.Equals(CapturedWildlifeRoleIds.Companion)
                && species.CompanionRoleProfile != null
            || roleId.Equals(CapturedWildlifeRoleIds.Haul)
                && species.HaulRoleProfile != null);

    public bool IsEligibleCompanionOwner(string ownerCharacterId) =>
        characters.TryGetValue(
            ownerCharacterId ?? string.Empty,
            out CharacterActor owner)
        && owner != null
        && !owner.IsDead
        && owner.characterType == CharacterType.NPC
        && (owner.CurrentLifecycleState is CharacterLifecycleState.Active
            or CharacterLifecycleState.Downed)
        && CharacterWorkRoleUtility.TryGetWork(owner, out _);

    public bool HasItem(string itemId) =>
        contentCatalog.TryGetItem(itemId, out _);

    public bool IsValidGridPosition(Vector2Int position) =>
        grid != null && grid.IsValidGridPos(position);

    public bool IsCarrierAvailable(string carrierId) =>
        characters.TryGetValue(
            carrierId ?? string.Empty,
            out CharacterActor carrier)
        && carrier != null
        && !carrier.IsDead;

    public bool TryGetValidPenCapacity(
        string penId,
        Vector2Int penPosition,
        out int capacity)
    {
        BuildableObject pen = world.Buildings.FirstOrDefault(building =>
            building != null
            && string.Equals(
                GetPenId(building),
                penId,
                StringComparison.Ordinal));
        BuildingBeastPenAbility ability =
            pen?.BuildingData.GetBeastPenAbility();
        bool valid = pen != null
            && !pen.isDestroy
            && ability != null
            && ability.IsValid
            && rooms.TryGetRoom(pen, out RoomInstance room)
            && room != null
            && room.IsUsable
            && room.ContainsCell(penPosition);
        capacity = valid ? ability.capacity : 0;
        return valid;
    }

    private static string GetPenId(BuildableObject pen)
    {
        return pen != null
            ? pen.RequirePersistentInstanceId().Value
            : string.Empty;
    }
}
