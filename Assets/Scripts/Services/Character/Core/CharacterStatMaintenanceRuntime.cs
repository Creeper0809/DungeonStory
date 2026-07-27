using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using Unity.Profiling;
using VContainer.Unity;

public sealed class CharacterStatMaintenanceRuntime : ITickable
{
    private static readonly ProfilerMarker TickMarker =
        new ProfilerMarker("CharacterStatMaintenanceRuntime.Tick");

    private readonly ICharacterWorldQuery characterWorld;
    private readonly IGameClock gameClock;
    private readonly IDynamicFrameWorkBudget frameWorkBudget;
    private readonly List<CharacterActor> actors = new List<CharacterActor>();
    private int actorIndex;
    private int capturedCharacterVersion = -1;

    public CharacterStatMaintenanceRuntime(
        ICharacterWorldQuery characterWorld,
        IGameClock gameClock,
        IDynamicFrameWorkBudget frameWorkBudget)
    {
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.gameClock = gameClock
            ?? throw new ArgumentNullException(nameof(gameClock));
        this.frameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
    }

    public void Tick()
    {
        using (TickMarker.Auto())
        {
            if (gameClock.DeltaTime <= 0f)
            {
                return;
            }

            RefreshSnapshotWhenNeeded();
            if (actors.Count == 0)
            {
                frameWorkBudget.SetBacklog(
                    DynamicFrameWorkDomain.CharacterNeeds,
                    0);
                return;
            }

            if (actorIndex >= actors.Count)
            {
                actorIndex = 0;
            }

            int backlog = actors.Count - actorIndex;
            frameWorkBudget.SetBacklog(
                DynamicFrameWorkDomain.CharacterNeeds,
                backlog);
            double sliceMilliseconds = frameWorkBudget.GetSliceMilliseconds(
                DynamicFrameWorkDomain.CharacterNeeds,
                0.04,
                0.45);
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            int processed = 0;
            float now = gameClock.Time;
            while (actorIndex < actors.Count)
            {
                CharacterActor actor = actors[actorIndex++];
                processed++;
                if (actor != null
                    && !actor.IsDead
                    && actor.CurrentLifecycleState
                        != CharacterLifecycleState.Despawned)
                {
                    actor.Stats?.RunScheduledMaintenance(now);
                }

                if (processed >= 8
                    && ElapsedMilliseconds(started) >= sliceMilliseconds)
                {
                    break;
                }
            }

            frameWorkBudget.ReportConsumed(
                DynamicFrameWorkDomain.CharacterNeeds,
                ElapsedMilliseconds(started));
            if (actorIndex >= actors.Count)
            {
                actorIndex = 0;
                frameWorkBudget.SetBacklog(
                    DynamicFrameWorkDomain.CharacterNeeds,
                    0);
            }
        }
    }

    private void RefreshSnapshotWhenNeeded()
    {
        if (capturedCharacterVersion == characterWorld.CharacterVersion
            && actors.Count > 0)
        {
            return;
        }

        actors.Clear();
        IReadOnlyList<CharacterActor> current = characterWorld.Characters;
        for (int i = 0; i < current.Count; i++)
        {
            actors.Add(current[i]);
        }

        capturedCharacterVersion = characterWorld.CharacterVersion;
        actorIndex = 0;
    }

    private static double ElapsedMilliseconds(long started)
    {
        return (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
    }
}
