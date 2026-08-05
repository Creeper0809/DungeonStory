using System;
using System.Collections.Generic;
using DungeonStory.Characters;
using Unity.Profiling;
using VContainer.Unity;

public sealed class CharacterStatMaintenanceRuntimeAdapter : ITickable
{
    private static readonly ProfilerMarker TickMarker =
        new("CharacterStatMaintenanceRuntime.Tick");

    private readonly DungeonStory.Characters.CharacterStatMaintenanceRuntime runtime;

    public CharacterStatMaintenanceRuntimeAdapter(
        DungeonStory.Characters.CharacterStatMaintenanceRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public void Tick()
    {
        using (TickMarker.Auto())
        {
            runtime.Tick();
        }
    }
}

public sealed class CharacterStatMaintenanceSceneAdapter :
    ICharacterStatMaintenancePort
{
    private readonly ICharacterWorldQuery characterWorld;
    private readonly Dictionary<CharacterId, CharacterActor> actorsById = new();

    public CharacterStatMaintenanceSceneAdapter(
        ICharacterWorldQuery characterWorld)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
    }

    public int CharacterVersion => characterWorld.CharacterVersion;

    public IReadOnlyList<CharacterId> CaptureCharacterIds()
    {
        actorsById.Clear();
        IReadOnlyList<CharacterActor> current = characterWorld.Characters;
        CharacterId[] result = new CharacterId[current.Count];
        for (int i = 0; i < current.Count; i++)
        {
            CharacterActor actor = current[i];
            if (actor == null)
            {
                result[i] = default;
                continue;
            }

            CharacterId characterId = CharacterPersistentIdentity.Require(actor);
            actorsById.Add(characterId, actor);
            result[i] = characterId;
        }

        return result;
    }

    public void RunScheduledMaintenance(CharacterId characterId, float now)
    {
        if (!actorsById.TryGetValue(characterId, out CharacterActor actor)
            || actor == null
            || actor.IsDead
            || actor.CurrentLifecycleState == CharacterLifecycleState.Despawned)
        {
            return;
        }

        actor.Stats?.RunScheduledMaintenance(now);
    }
}
