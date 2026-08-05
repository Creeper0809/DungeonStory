using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

internal interface IInvasionIntruderRestorePort
{
    InvasionIntruderSettings Settings { get; set; }
    InvasionThreatSnapshot ThreatSnapshot { set; }
    InvasionIntruderPatternDefinition Pattern { get; set; }
    BuildableObject PriorityTarget { set; }
    ISet<BuildingInstanceId> DamagedFacilityIds { get; }
    int FacilityDamageCount { set; }
    float RestoredStructureAttackDelay { set; }
    float RestoredTrappedSeconds { set; }
    bool RestoredEnragedBreach { set; }
    bool HasFinalDefenseTarget { set; }
    Vector2Int FinalDefenseTarget { set; }
    float Elapsed { set; }
    float RallyRemainingSeconds { set; }
    bool HasBreachedDungeonInterior { get; set; }
    bool BreachEventRaised { set; }
    float NextDamageTime { set; }
    bool Resolved { set; }
    string RuntimeId { set; }
    InvasionIntruderState State { get; set; }
    IGameClock Clock { get; }
    IInvasionIntruderContext Context { get; }
    IDefenseRaidAwarenessRuntime RaidAwareness { get; }
    IDefenseStatusRuntimeService DefenseStatusRuntimeService { get; }
    CharacterActor Actor { get; }
    Transform Transform { get; }
    bool IsActiveAndEnabled { get; }

    void StopActiveRoutine();
    void RequireRuntimeComponents();
    InvasionIntruderPatternDefinition ResolvePattern(string id);
    void ClearBreachState();
    void RefreshPathRandomStream();
    void StartRestoredInside();
    void StartRestoredEntry(
        Vector3 doorPosition,
        Vector2Int gridPosition,
        bool includeRally);
}

internal sealed class InvasionIntruderRestoreCoordinator
{
    private readonly IInvasionIntruderRestorePort port;
    private bool activationPending;
    private bool startsInside;
    private bool includesRally;
    private Vector3 entryDoorPosition;
    private Vector2Int entryGridPosition;
    private DefenseRaidAwarenessRestoreCandidate raidAwarenessCandidate;

    public InvasionIntruderRestoreCoordinator(
        IInvasionIntruderRestorePort port)
    {
        this.port = port ?? throw new ArgumentNullException(nameof(port));
    }

    public bool TryPrepare(
        CharacterSO data,
        InvasionIntruderPersistenceState source,
        Vector2Int? finalDefenseTarget,
        out string warning)
    {
        DiscardPrepared();
        warning = string.Empty;
        if (data == null || source == null)
        {
            warning = "Active invasion state was incomplete.";
            return false;
        }

        port.StopActiveRoutine();
        port.RequireRuntimeComponents();
        port.Settings = InvasionIntruderPersistenceState.CloneSettings(
            source.Settings);
        port.ThreatSnapshot = default;
        port.Pattern = port.ResolvePattern(port.Settings.patternId);
        port.Settings.patternId = port.Pattern.id;
        port.PriorityTarget = null;
        port.DamagedFacilityIds.Clear();
        port.FacilityDamageCount = source.FacilityDamageCount;
        port.ClearBreachState();
        port.RestoredStructureAttackDelay = source.StructureAttackDelayRemaining;
        port.RestoredTrappedSeconds = source.TrappedSeconds;
        port.RestoredEnragedBreach = source.EnragedBreach;
        port.HasFinalDefenseTarget = finalDefenseTarget.HasValue;
        port.FinalDefenseTarget = finalDefenseTarget.GetValueOrDefault();
        port.Elapsed = source.ElapsedSeconds;
        port.RallyRemainingSeconds = source.RallyRemainingSeconds;
        port.HasBreachedDungeonInterior = source.HasBreachedDungeonInterior
            || InvasionIntruderCombatRules.IsPostBreachState(source.State);
        port.BreachEventRaised = port.HasBreachedDungeonInterior;
        port.NextDamageTime = port.Clock.Time + source.DamageDelayRemaining;
        port.Resolved = false;
        port.RuntimeId = source.RuntimeId;
        port.RefreshPathRandomStream();

        DefenseRaidAwarenessRestoreCandidate preparedAwareness;
        try
        {
            preparedAwareness = port.RaidAwareness.PrepareRestore(
                source.RaidAwareness);
        }
        catch (Exception exception)
        {
            warning = exception.Message;
            return false;
        }

        foreach (BuildingInstanceId damagedFacilityId
                 in source.DamagedFacilityIds)
        {
            if (!port.Context.TryResolveBuilding(damagedFacilityId, out _))
            {
                warning =
                    $"Damaged facility '{damagedFacilityId.Value}' does not resolve to exactly one building.";
                return false;
            }

            port.DamagedFacilityIds.Add(damagedFacilityId);
        }

        RestoreActor(data, source);
        if (!TryResolveActivation(
                source,
                out bool preparedStartsInside,
                out bool preparedIncludesRally,
                out Vector3 preparedDoorPosition,
                out Vector2Int preparedGridPosition,
                out warning))
        {
            return false;
        }

        startsInside = preparedStartsInside;
        includesRally = preparedIncludesRally;
        entryDoorPosition = preparedDoorPosition;
        entryGridPosition = preparedGridPosition;
        raidAwarenessCandidate = preparedAwareness;
        activationPending = true;
        return true;
    }

    public void Publish()
    {
        if (!activationPending || !port.IsActiveAndEnabled)
        {
            throw new InvalidOperationException(
                "No active invasion intruder restore candidate is prepared.");
        }

        port.RaidAwareness.PublishRestore(raidAwarenessCandidate);
        activationPending = false;
        raidAwarenessCandidate = null;
        if (startsInside)
        {
            port.StartRestoredInside();
        }
        else
        {
            port.StartRestoredEntry(
                entryDoorPosition,
                entryGridPosition,
                includesRally);
        }
    }

    public void DiscardPrepared()
    {
        activationPending = false;
        startsInside = false;
        includesRally = false;
        entryDoorPosition = default;
        entryGridPosition = default;
        raidAwarenessCandidate = null;
    }

    private void RestoreActor(
        CharacterSO data,
        InvasionIntruderPersistenceState source)
    {
        port.Transform.position = source.WorldPosition;
        port.Actor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
        port.Actor.Initialize(data);
        port.Actor.Identity?.SetPersistentId(source.RuntimeId);
        port.Actor.ScaleMaxHealth(port.Settings.healthMultiplier);
        port.Actor.Stats.RestorePersistentState(
            source.Conditions,
            source.CurrentHealth,
            source.InjurySeverity,
            source.BaseMood,
            Array.Empty<CharacterMoodFactorSnapshot>());

        DefenseStatusRuntime statusRuntime =
            port.DefenseStatusRuntimeService.GetOrAdd(port.Actor);
        foreach (DefenseStatusKind kind in Enum.GetValues(
                     typeof(DefenseStatusKind)))
        {
            statusRuntime.ClearStatus(kind);
        }

        foreach (DefenseStatusSnapshot status in source.DefenseStatuses)
        {
            statusRuntime.ApplyStatus(
                status.Kind,
                status.Value,
                status.RemainingSeconds,
                status.Stacks);
        }
    }

    private bool TryResolveActivation(
        InvasionIntruderPersistenceState source,
        out bool preparedStartsInside,
        out bool preparedIncludesRally,
        out Vector3 preparedDoorPosition,
        out Vector2Int preparedGridPosition,
        out string warning)
    {
        preparedStartsInside = false;
        preparedIncludesRally = false;
        preparedDoorPosition = default;
        preparedGridPosition = default;
        warning = string.Empty;
        if (source.State is InvasionIntruderState.Rallying
            or InvasionIntruderState.Entering)
        {
            if (!port.Context.TryResolveEntry(out InvasionIntruderEntry entry))
            {
                warning = "The active intruder entrance no longer exists.";
                return false;
            }

            port.State = source.State;
            port.Actor.SetLifecycleState(CharacterLifecycleState.SpawningOutside);
            preparedIncludesRally =
                source.State == InvasionIntruderState.Rallying;
            preparedDoorPosition = entry.DoorPosition;
            preparedGridPosition = entry.GridPosition;
            return true;
        }

        if (!port.Context.TryGetGrid(out Grid grid))
        {
            warning =
                "The dungeon grid was unavailable while restoring an active intruder.";
            return false;
        }

        Vector2Int restoredPosition = source.GridPosition;
        if (!grid.IsValidGridPos(restoredPosition)
            && !grid.TryFindNearestWalkablePosition(
                restoredPosition,
                out restoredPosition))
        {
            warning = "The active intruder has no valid restore position.";
            return false;
        }

        port.Transform.position = grid.GetWorldPos(restoredPosition);
        port.Actor.SetLifecycleState(CharacterLifecycleState.Active);
        port.State = source.State;
        preparedStartsInside = true;
        return true;
    }
}
