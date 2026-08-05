using System;
using UnityEngine;

public sealed class BuildingDoorCharacterWildlifeAdapter :
    IBuildingDoorTraversalSubjectPort,
    IBuildingDoorAccessSubjectPort,
    IBuildingDoorPolicyInvalidationPort
{
    private readonly ICharacterAiWorldRegistry worldRegistry;

    public BuildingDoorCharacterWildlifeAdapter(
        ICharacterAiWorldRegistry worldRegistry)
    {
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
    }

    public BuildingDoorTraversalSubjects ResolveTraversalSubjects(
        Collider2D collision)
    {
        if (collision == null)
        {
            return default;
        }

        CharacterActor character = collision.GetComponentInParent<CharacterActor>();
        WildlifeActor wildlife = collision.GetComponentInParent<WildlifeActor>();
        return new BuildingDoorTraversalSubjects(
            character != null && character.CompareTag("Character")
                ? character
                : null,
            wildlife != null && wildlife.CanEnterDungeon
                ? wildlife
                : null);
    }

    public bool IsTraversalSubjectAvailable(object subject)
    {
        return subject switch
        {
            CharacterActor character => character != null,
            WildlifeActor wildlife => wildlife != null,
            _ => false
        };
    }

    public void ChangeTraversalSortingLayer(object subject, string layerName)
    {
        switch (subject)
        {
            case CharacterActor character when character != null:
                character.ChangeLayer(layerName);
                break;
            case WildlifeActor wildlife when wildlife != null:
                wildlife.ChangeLayer(layerName);
                break;
        }
    }

    public bool TryResolveDoorAccessSubject(
        UnityEngine.Object subject,
        out BuildingDoorAccessSubjectSnapshot snapshot)
    {
        if (subject is CharacterActor character && character != null)
        {
            BuildingDoorAccessSubjectKind kind = character.IsOwner
                ? BuildingDoorAccessSubjectKind.Owner
                : character.characterType switch
                {
                    CharacterType.Intruder => BuildingDoorAccessSubjectKind.Intruder,
                    CharacterType.Customer => BuildingDoorAccessSubjectKind.Customer,
                    _ => BuildingDoorAccessSubjectKind.Staff
                };
            snapshot = new BuildingDoorAccessSubjectSnapshot(
                CharacterPersistentIdentity.Require(character).Value,
                character.name,
                kind,
                character);
            return true;
        }

        if (subject is WildlifeActor wildlife && wildlife != null)
        {
            snapshot = new BuildingDoorAccessSubjectSnapshot(
                wildlife.WildlifeId,
                wildlife.name,
                BuildingDoorAccessSubjectKind.Wildlife,
                wildlife);
            return true;
        }

        snapshot = default;
        return false;
    }

    public void InvalidateDoorPolicyPaths()
    {
        foreach (CharacterActor character in worldRegistry.Characters)
        {
            if (character?.Brain == null)
            {
                continue;
            }

            character.Brain.ClearPathSearchCache();
            if (character.CanRunAi && !character.Brain.IsManualCommandActive)
            {
                character.Brain.RequestImmediateReplan();
            }
        }
    }
}
