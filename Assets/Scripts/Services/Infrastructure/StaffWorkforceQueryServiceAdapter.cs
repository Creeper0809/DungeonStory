using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Work;

public interface IStaffWorkforceQueryService
{
    IReadOnlyList<CharacterActor> FindActiveWorkers();
    bool IsActiveWorker(CharacterActor character);
    string GetDisplayName(CharacterActor character);
}

public sealed class StaffWorkforceRuntimeQueryServiceAdapter :
    IStaffWorkforceQueryService
{
    private readonly StaffWorkforceSceneAdapter scene;
    private readonly DungeonStory.Work.StaffWorkforceRuntimeQueryService query;

    public StaffWorkforceRuntimeQueryServiceAdapter(
        StaffWorkforceSceneAdapter scene,
        DungeonStory.Work.StaffWorkforceRuntimeQueryService query)
    {
        this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
        this.query = query ?? throw new ArgumentNullException(nameof(query));
    }

    public IReadOnlyList<CharacterActor> FindActiveWorkers()
    {
        return query.FindActiveWorkers()
            .Select(character => scene.RequireActor(character.CharacterId))
            .ToList();
    }

    public bool IsActiveWorker(CharacterActor character) =>
        query.IsActiveWorker(scene.Capture(character));

    public string GetDisplayName(CharacterActor character) =>
        character == null
            ? string.Empty
            : query.GetDisplayName(scene.Capture(character));
}

public sealed class StaffWorkforceSceneAdapter : IStaffWorkforceSnapshotQuery
{
    private readonly ICharacterWorldQuery characterWorld;
    private readonly Dictionary<CharacterId, CharacterActor> actorsById = new();

    public StaffWorkforceSceneAdapter(ICharacterWorldQuery characterWorld)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public IReadOnlyList<StaffWorkforceSnapshot> CaptureWorkforce()
    {
        actorsById.Clear();
        IReadOnlyList<CharacterActor> current = characterWorld.Characters;
        List<StaffWorkforceSnapshot> result = new(current.Count);
        for (int i = 0; i < current.Count; i++)
        {
            CharacterActor actor = current[i];
            if (actor == null) continue;
            StaffWorkforceSnapshot snapshot = Capture(actor);
            actorsById.Add(snapshot.CharacterId, actor);
            result.Add(snapshot);
        }

        return result;
    }

    public StaffWorkforceSnapshot Capture(CharacterActor actor)
    {
        if (actor == null) return default;
        CharacterId characterId = CharacterPersistentIdentity.Require(actor);
        CharacterIdentity identity = actor.Identity;
        string displayName = identity != null
            && !string.IsNullOrWhiteSpace(identity.DisplayName)
                ? identity.DisplayName
                : actor.name;
        return new StaffWorkforceSnapshot(
            characterId,
            actor.IsDead,
            CharacterWorkRoleUtility.TryGetWork(actor, out _),
            actor.IsOwner,
            displayName);
    }

    public CharacterActor RequireActor(CharacterId characterId)
    {
        if (!actorsById.TryGetValue(characterId, out CharacterActor actor)
            || actor == null)
        {
            throw new InvalidOperationException(
                $"Active workforce actor '{characterId.Value}' is unavailable.");
        }

        return actor;
    }
}
